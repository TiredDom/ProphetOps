namespace ProphetOps.Forecasting;

public record SeriesAnomaly(int Index, double Value, double Median);

/// Finds months whose total sits so far outside the rest that a recording mistake is the more
/// likely explanation.
///
/// Median and median absolute deviation are used rather than mean and standard deviation
/// because the very value being hunted would drag a mean toward itself and inflate a standard
/// deviation, hiding the thing the test is for.
public static class SeriesAnomalies
{
    private const double MadToSigma = 0.6745;
    public const double DefaultThreshold = 3.5;

    public static IReadOnlyList<SeriesAnomaly> Detect(
        IReadOnlyList<double> values,
        double threshold = DefaultThreshold,
        int minimumPoints = 8)
    {
        var found = new List<SeriesAnomaly>();
        if (values.Count < minimumPoints) return found;

        var median = Median(values);
        var deviations = values.Select(v => Math.Abs(v - median)).ToList();
        var mad = Median(deviations);

        // With half or more of the months identical the deviation collapses to zero and every
        // other month would score as infinitely unusual. There is no spread to judge against.
        if (mad <= 0) return found;

        for (int i = 0; i < values.Count; i++)
        {
            var score = MadToSigma * (values[i] - median) / mad;
            if (Math.Abs(score) >= threshold) found.Add(new SeriesAnomaly(i, values[i], median));
        }

        return found;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
