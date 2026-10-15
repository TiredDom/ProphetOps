using ProphetOps.Forecasting;

namespace ProphetOps.Api;

/// Which way the forecast goes and where it peaks, read the same way wherever it is put into words:
/// the forecast months' average against the last six recorded months, and the highest forecast month.
internal static class ForecastTrend
{
    public static (string Direction, double ChangePercent) Of(IReadOnlyList<double> series, IReadOnlyList<ForecastStep> steps)
    {
        var recentMean = series.Count > 0 ? series.Skip(Math.Max(0, series.Count - 6)).Average() : 0;
        var forecastMean = steps.Count > 0 ? steps.Average(s => s.Value) : recentMean;
        var changePercent = recentMean != 0 ? Math.Round((forecastMean - recentMean) / recentMean * 100, 1) : 0;
        var direction = changePercent > 1 ? "up" : changePercent < -1 ? "down" : "flat";
        return (direction, changePercent);
    }

    public static ForecastStep? Peak(IReadOnlyList<ForecastStep> steps) => steps.OrderByDescending(s => s.Value).FirstOrDefault();
}
