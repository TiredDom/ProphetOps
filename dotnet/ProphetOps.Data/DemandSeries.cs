namespace ProphetOps.Data;

public enum DemandSeriesSource
{
    LiveRecords,
    SampleSeries,
}

public class DemandSeries
{
    public required IReadOnlyList<double> Values { get; init; }

    /// The month the last value belongs to. Forecast step 1 is the month after this.
    public required DateOnly LastMonth { get; init; }

    public required DemandSeriesSource Source { get; init; }

    /// Complete months of real booking history available, whatever the chosen source.
    public required int LiveMonthsAvailable { get; init; }

    /// Months inside the live range that carried no bookings and were filled with zero.
    public required int FilledMonths { get; init; }

    public required int MinimumMonths { get; init; }

    public bool UsingLiveRecords => Source == DemandSeriesSource.LiveRecords;
}

public static class DemandSeriesBuilder
{
    public const int SeasonLength = 12;

    /// Holt-Winters needs two complete cycles before it can separate level, trend, and season.
    public const int MinimumMonths = 2 * SeasonLength;

    private static readonly DateOnly SampleAnchor = new(2026, 7, 1);

    /// Builds the monthly revenue series the forecaster consumes.
    ///
    /// Live booking history is preferred, but only once it covers MinimumMonths. Below that the
    /// deterministic sample series is used so the published accuracy figures stay reproducible and
    /// the agency still sees a working forecast; callers surface which source was used.
    public static DemandSeries Build(AppDbContext db, DateOnly today)
    {
        // The running month is still accumulating bookings. Including it would read as a demand
        // collapse every time someone opens the page mid-month, so the series stops last month.
        var lastComplete = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);

        var monthly = db.Bookings
            .Where(b => b.BookingDate <= lastComplete.AddMonths(1).AddDays(-1))
            .AsEnumerable()
            .GroupBy(b => new DateOnly(b.BookingDate.Year, b.BookingDate.Month, 1))
            .ToDictionary(g => g.Key, g => (double)g.Sum(b => b.GrossRevenue));

        var liveMonths = 0;
        var filled = 0;
        var live = new List<double>();

        if (monthly.Count > 0)
        {
            var first = monthly.Keys.Min();

            // Stop at the last month that actually has bookings rather than at the last complete
            // calendar month. If entry is running behind, trailing empty months are almost always
            // unrecorded rather than genuinely zero, and feeding them in would teach the model a
            // collapse to zero. Gaps *inside* the range are still kept, because those shift the
            // seasonal alignment if dropped.
            var lastRecorded = monthly.Keys.Max();
            var end = lastRecorded < lastComplete ? lastRecorded : lastComplete;

            for (var month = first; month <= end; month = month.AddMonths(1))
            {
                if (monthly.TryGetValue(month, out var value))
                {
                    live.Add(value);
                }
                else
                {
                    live.Add(0);
                    filled++;
                }
            }
            liveMonths = live.Count;
        }

        if (liveMonths >= MinimumMonths)
        {
            return new DemandSeries
            {
                Values = live,
                LastMonth = new DateOnly(monthly.Keys.Min().Year, monthly.Keys.Min().Month, 1).AddMonths(liveMonths - 1),
                Source = DemandSeriesSource.LiveRecords,
                LiveMonthsAvailable = liveMonths,
                FilledMonths = filled,
                MinimumMonths = MinimumMonths,
            };
        }

        return new DemandSeries
        {
            Values = SampleSalesHistory.MonthlyRevenue(SampleAnchor.Year, SampleAnchor.Month)
                .Select(v => (double)v)
                .ToList(),
            LastMonth = SampleAnchor,
            Source = DemandSeriesSource.SampleSeries,
            LiveMonthsAvailable = liveMonths,
            FilledMonths = 0,
            MinimumMonths = MinimumMonths,
        };
    }
}
