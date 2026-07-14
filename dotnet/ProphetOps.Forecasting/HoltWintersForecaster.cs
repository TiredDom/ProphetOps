namespace ProphetOps.Forecasting;

public static class HoltWintersForecaster
{
    private static readonly double[] AlphaGrid = { 0.05, 0.1, 0.2, 0.3, 0.4, 0.5 };
    private static readonly double[] BetaGrid = { 0.0, 0.05, 0.1, 0.2, 0.3 };
    private static readonly double[] GammaGrid = { 0.05, 0.1, 0.2, 0.3, 0.4, 0.5 };

    private const int MaxValidation = 6;
    private const int MinValidation = 4;
    private const double IntervalZ = 1.28;

    public static ForecastResult Forecast(
        IReadOnlyList<double> rawValues,
        int seasonLength = 12,
        int horizon = 6,
        HoltWintersParams? fixedParams = null)
    {
        var values = new double[rawValues.Count];
        for (int i = 0; i < rawValues.Count; i++) values[i] = rawValues[i];

        int count = values.Length;
        int minRequired = 2 * seasonLength;

        if (count < minRequired || seasonLength < 2 || horizon < 1)
            return ForecastResult.NotEnoughHistory(count, minRequired, seasonLength);

        Tuning tuning = fixedParams is not null
            ? new Tuning(fixedParams.Alpha, fixedParams.Beta, fixedParams.Gamma, "manual", 0, null)
            : GridSearch(values, seasonLength);

        RunState run = Run(values, seasonLength, tuning.Alpha, tuning.Beta, tuning.Gamma);
        double[] projection = Project(run, horizon);

        double residualRmse = Rmse(run.Errors);
        var forecast = new List<ForecastStep>(horizon);

        for (int stepIndex = 0; stepIndex < projection.Length; stepIndex++)
        {
            int step = stepIndex + 1;
            double value = projection[stepIndex];
            double margin = IntervalZ * residualRmse * Math.Sqrt(step);

            double lower = value - margin;
            if (value >= 0) lower = Math.Max(lower, 0);
            lower = Math.Min(lower, value);

            forecast.Add(new ForecastStep(step, Round(value, 2), Round(lower, 2), Round(value + margin, 2)));
        }

        var fitted = new double?[count];
        for (int i = 0; i < count; i++)
            fitted[i] = run.Fitted[i] is { } f ? Round(f, 2) : null;

        return new ForecastResult
        {
            Ok = true,
            Method = "Holt-Winters (additive triple exponential smoothing)",
            SeasonLength = seasonLength,
            Horizon = horizon,
            Params = new HoltWintersParams(Round(tuning.Alpha, 3), Round(tuning.Beta, 3), Round(tuning.Gamma, 3)),
            Tuning = new TuningInfo(tuning.Strategy, tuning.ValidationSize, tuning.Mae is { } m ? Round(m, 2) : null),
            Fitted = fitted,
            Forecast = forecast,
            Metrics = Metrics(values, run),
            Baselines = EvaluateBaselines(values, seasonLength),
            Final = new FinalState(Round(run.Level, 2), Round(run.Trend, 2)),
        };
    }

    private static RunState Run(double[] values, int seasonLength, double alpha, double beta, double gamma)
    {
        int count = values.Length;

        double level = Sum(values, 0, seasonLength) / seasonLength;
        double secondCycleMean = Sum(values, seasonLength, seasonLength) / seasonLength;
        double trend = (secondCycleMean - level) / seasonLength;

        var seasonal = new double[count];
        for (int i = 0; i < seasonLength; i++)
            seasonal[i] = values[i] - level;

        var fitted = new double?[count];
        var errors = new List<double>(count);

        for (int t = seasonLength; t < count; t++)
        {
            double previousSeasonal = seasonal[t - seasonLength];

            double oneStep = level + trend + previousSeasonal;
            fitted[t] = oneStep;
            errors.Add(values[t] - oneStep);

            double newLevel = alpha * (values[t] - previousSeasonal) + (1 - alpha) * (level + trend);
            double newTrend = beta * (newLevel - level) + (1 - beta) * trend;
            seasonal[t] = gamma * (values[t] - newLevel) + (1 - gamma) * previousSeasonal;

            level = newLevel;
            trend = newTrend;
        }

        return new RunState(level, trend, seasonal, fitted, errors, count, seasonLength);
    }

    private static double[] Project(RunState run, int horizon)
    {
        var outp = new double[horizon];
        for (int h = 1; h <= horizon; h++)
        {
            int seasonIndex = run.Count - run.SeasonLength + ((h - 1) % run.SeasonLength);
            double season = seasonIndex >= 0 && seasonIndex < run.Seasonal.Length ? run.Seasonal[seasonIndex] : 0.0;
            outp[h - 1] = run.Level + h * run.Trend + season;
        }
        return outp;
    }

    private static Tuning GridSearch(double[] values, int seasonLength)
    {
        int count = values.Length;
        int validation = Math.Min(MaxValidation, count - 2 * seasonLength);
        bool useHoldout = validation >= MinValidation;

        double bestAlpha = 0, bestBeta = 0, bestGamma = 0, bestMae = double.PositiveInfinity;
        bool have = false;

        foreach (double alpha in AlphaGrid)
        foreach (double beta in BetaGrid)
        foreach (double gamma in GammaGrid)
        {
            double mae;
            if (useHoldout)
            {
                var train = Slice(values, 0, count - validation);
                RunState run = Run(train, seasonLength, alpha, beta, gamma);
                double[] projection = Project(run, validation);
                mae = Mae(Slice(values, count - validation, validation), projection);
            }
            else
            {
                RunState run = Run(values, seasonLength, alpha, beta, gamma);
                mae = run.Errors.Count > 0 ? AbsMean(run.Errors) : double.PositiveInfinity;
            }

            if (!have || mae < bestMae)
            {
                bestAlpha = alpha; bestBeta = beta; bestGamma = gamma; bestMae = mae;
                have = true;
            }
        }

        return new Tuning(
            bestAlpha, bestBeta, bestGamma,
            useHoldout ? "holdout" : "in-sample",
            useHoldout ? validation : 0,
            bestMae);
    }

    private static ForecastMetrics Metrics(double[] values, RunState run)
    {
        double sumAbs = 0, sumSq = 0, sumPct = 0;
        int nAbs = 0, nPct = 0;

        for (int t = 0; t < run.Fitted.Length; t++)
        {
            if (run.Fitted[t] is not { } fitted) continue;

            double error = values[t] - fitted;
            sumAbs += Math.Abs(error);
            sumSq += error * error;
            nAbs++;

            if (values[t] != 0.0)
            {
                sumPct += Math.Abs(error / values[t]);
                nPct++;
            }
        }

        int sampleSize = Math.Max(nAbs, 1);
        return new ForecastMetrics(
            Round(sumAbs / sampleSize, 2),
            Round(Math.Sqrt(sumSq / sampleSize), 2),
            Round(sumPct / Math.Max(nPct, 1) * 100, 2),
            nAbs);
    }

    private static Baselines EvaluateBaselines(double[] values, int seasonLength)
    {
        int count = values.Length;
        double seasonalAbs = 0, naiveAbs = 0;
        int n = 0;

        for (int t = seasonLength; t < count; t++)
        {
            seasonalAbs += Math.Abs(values[t] - values[t - seasonLength]);
            naiveAbs += Math.Abs(values[t] - values[t - 1]);
            n++;
        }

        int denom = Math.Max(n, 1);
        return new Baselines(Round(seasonalAbs / denom, 2), Round(naiveAbs / denom, 2));
    }

    private static double Mae(double[] actual, double[] predicted)
    {
        int length = Math.Min(actual.Length, predicted.Length);
        if (length == 0) return double.PositiveInfinity;

        double sum = 0.0;
        for (int i = 0; i < length; i++) sum += Math.Abs(actual[i] - predicted[i]);
        return sum / length;
    }

    private static double Rmse(IReadOnlyList<double> errors)
    {
        if (errors.Count == 0) return 0.0;
        double squared = 0.0;
        foreach (double e in errors) squared += e * e;
        return Math.Sqrt(squared / errors.Count);
    }

    private static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static double Sum(double[] values, int start, int length)
    {
        double s = 0.0;
        for (int i = start; i < start + length; i++) s += values[i];
        return s;
    }

    private static double[] Slice(double[] values, int start, int length)
    {
        var outp = new double[length];
        Array.Copy(values, start, outp, 0, length);
        return outp;
    }

    private static double AbsMean(IReadOnlyList<double> errors)
    {
        double sum = 0.0;
        foreach (double e in errors) sum += Math.Abs(e);
        return sum / errors.Count;
    }

    private readonly record struct Tuning(double Alpha, double Beta, double Gamma, string Strategy, int ValidationSize, double? Mae);

    private readonly record struct RunState(
        double Level, double Trend, double[] Seasonal, double?[] Fitted,
        IReadOnlyList<double> Errors, int Count, int SeasonLength);
}
