namespace ProphetOps.Forecasting;

/// One forecast made at a past month, compared with what was then recorded. The forecast used only
/// the months before <see cref="Origin"/>; <see cref="Target"/> is the month it was aiming at.
public sealed record BacktestPoint(
    int Origin,
    int Target,
    int Step,
    double Actual,
    double Forecast,
    double Lower,
    double Upper,
    double SeasonalNaive)
{
    public bool InsideRange => Actual >= Lower && Actual <= Upper;
}

/// How the forecasts made a given number of months ahead did, against the "same month last year"
/// baseline. Percentages are null when every actual was zero.
public sealed record HorizonAccuracy(
    int Step,
    int Forecasts,
    double Mae,
    double? Mape,
    double SeasonalNaiveMae,
    double? SeasonalNaiveMape,
    int InsideRange,
    double Coverage);

/// Every horizon together. Skill is how much smaller the Holt-Winters miss is than the baseline's
/// (0.25 means a quarter smaller; below zero means the baseline did better).
public sealed record OverallAccuracy(
    int Forecasts,
    double Mae,
    double? Mape,
    double SeasonalNaiveMae,
    double? SeasonalNaiveMape,
    int InsideRange,
    double Coverage,
    double? SkillVsSeasonalNaive);

public sealed class BacktestResult
{
    public bool Ok { get; init; }
    public string? Reason { get; init; }

    public int SeasonLength { get; init; }
    public int Horizon { get; init; }
    public int MonthsAvailable { get; init; }
    public int MonthsRequired { get; init; }

    /// Index of the first month forecast, and how many past months the test stood at.
    public int FirstOrigin { get; init; }
    public int Origins { get; init; }

    public IReadOnlyList<HorizonAccuracy> ByHorizon { get; init; } = [];
    public OverallAccuracy? Overall { get; init; }
    public IReadOnlyList<BacktestPoint> Points { get; init; } = [];
}

/// A rolling-origin test of the Holt-Winters forecast on recorded months it has not seen. The test
/// stands at each past month in turn, forecasts the next months with exactly the method the
/// application uses (including its own tuning) from the earlier months only, and compares each
/// forecast with what was recorded. The same months are forecast by repeating the same month a year
/// earlier, the usual baseline for seasonal series.
public static class RollingOriginEvaluation
{
    public static BacktestResult Evaluate(IReadOnlyList<double> values, int seasonLength = 12, int horizon = 6)
    {
        int count = values.Count;
        int minTrain = 2 * seasonLength;
        int required = minTrain + 1;

        if (seasonLength < 2 || horizon < 1 || count < required)
        {
            return new BacktestResult
            {
                Ok = false,
                Reason = "The test needs two full seasonal cycles to learn from and at least one later month to check.",
                SeasonLength = seasonLength,
                Horizon = horizon,
                MonthsAvailable = count,
                MonthsRequired = required,
            };
        }

        var points = new List<BacktestPoint>();
        for (int origin = minTrain; origin < count; origin++)
        {
            var training = new double[origin];
            for (int i = 0; i < origin; i++) training[i] = values[i];

            ForecastResult forecast = HoltWintersForecaster.Forecast(training, seasonLength, horizon);
            if (!forecast.Ok || forecast.Forecast is null) continue;

            foreach (ForecastStep step in forecast.Forecast)
            {
                int target = origin + step.Step - 1;
                if (target >= count) break;

                // The latest recorded month of the same season, as the forecaster's own projection
                // picks it: a year before the target for anything up to twelve months ahead.
                int naiveIndex = origin - seasonLength + ((step.Step - 1) % seasonLength);
                points.Add(new BacktestPoint(
                    origin,
                    target,
                    step.Step,
                    values[target],
                    step.Value,
                    step.Lower,
                    step.Upper,
                    values[naiveIndex]));
            }
        }

        var byHorizon = Enumerable.Range(1, horizon)
            .Select(h => points.Where(p => p.Step == h).ToList())
            .Where(group => group.Count > 0)
            .Select(group => Summarise(group[0].Step, group))
            .ToList();

        return new BacktestResult
        {
            Ok = points.Count > 0,
            Reason = points.Count > 0 ? null : "No forecast could be compared with a recorded month.",
            SeasonLength = seasonLength,
            Horizon = horizon,
            MonthsAvailable = count,
            MonthsRequired = required,
            FirstOrigin = minTrain,
            Origins = count - minTrain,
            ByHorizon = byHorizon,
            Overall = points.Count > 0 ? Overall(points) : null,
            Points = points,
        };
    }

    private static HorizonAccuracy Summarise(int step, IReadOnlyList<BacktestPoint> group)
    {
        int inside = group.Count(p => p.InsideRange);
        return new HorizonAccuracy(
            step,
            group.Count,
            Round(group.Average(p => Math.Abs(p.Actual - p.Forecast))),
            Mape(group, p => p.Forecast),
            Round(group.Average(p => Math.Abs(p.Actual - p.SeasonalNaive))),
            Mape(group, p => p.SeasonalNaive),
            inside,
            Math.Round((double)inside / group.Count, 4, MidpointRounding.AwayFromZero));
    }

    private static OverallAccuracy Overall(IReadOnlyList<BacktestPoint> points)
    {
        double mae = points.Average(p => Math.Abs(p.Actual - p.Forecast));
        double naiveMae = points.Average(p => Math.Abs(p.Actual - p.SeasonalNaive));
        int inside = points.Count(p => p.InsideRange);
        return new OverallAccuracy(
            points.Count,
            Round(mae),
            Mape(points, p => p.Forecast),
            Round(naiveMae),
            Mape(points, p => p.SeasonalNaive),
            inside,
            Math.Round((double)inside / points.Count, 4, MidpointRounding.AwayFromZero),
            naiveMae > 0 ? Math.Round(1 - mae / naiveMae, 4, MidpointRounding.AwayFromZero) : null);
    }

    /// Mean absolute percentage error over the months with a non-zero actual.
    private static double? Mape(IEnumerable<BacktestPoint> points, Func<BacktestPoint, double> predicted)
    {
        var usable = points.Where(p => p.Actual != 0).ToList();
        if (usable.Count == 0) return null;
        return Round(usable.Average(p => Math.Abs((p.Actual - predicted(p)) / p.Actual)) * 100);
    }

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
