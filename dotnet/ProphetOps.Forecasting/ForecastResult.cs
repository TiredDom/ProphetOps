namespace ProphetOps.Forecasting;

public sealed record HoltWintersParams(double Alpha, double Beta, double Gamma);

public sealed record TuningInfo(string Strategy, int ValidationSize, double? ValidationMae);

public sealed record ForecastStep(int Step, double Value, double Lower, double Upper);

public sealed record ForecastMetrics(double Mae, double Rmse, double Mape, int SampleSize);

public sealed record Baselines(double SeasonalNaiveMae, double NaiveMae);

public sealed record FinalState(double Level, double Trend);

public sealed class ForecastResult
{
    public bool Ok { get; init; }

    public string? Method { get; init; }
    public int SeasonLength { get; init; }
    public int Horizon { get; init; }
    public HoltWintersParams? Params { get; init; }
    public TuningInfo? Tuning { get; init; }

    public double?[]? Fitted { get; init; }
    public IReadOnlyList<ForecastStep>? Forecast { get; init; }
    public ForecastMetrics? Metrics { get; init; }
    public Baselines? Baselines { get; init; }
    public FinalState? Final { get; init; }

    public string? Reason { get; init; }
    public int? MonthsAvailable { get; init; }
    public int? MonthsRequired { get; init; }

    internal static ForecastResult NotEnoughHistory(int count, int minRequired, int seasonLength) => new()
    {
        Ok = false,
        Reason = "Holt-Winters needs at least two full seasonal cycles of history.",
        MonthsAvailable = count,
        MonthsRequired = minRequired,
        SeasonLength = seasonLength,
    };
}
