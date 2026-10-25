using ProphetOps.Data;
using ProphetOps.Forecasting;

namespace ProphetOps.Api;

/// Planning aids built on the Holt-Winters forecast of recorded bookings: what the coming months
/// could leave after costs under a stated spending assumption, and each package's expected demand
/// against its open slots, with a price worth considering. Every figure is an estimate on the
/// assumption it names, and nothing here changes a record: a suggested price only ever fills in the
/// package form, for a person to save or not.
public static class ForecastPlanning
{
    /// The suggested change when demand outruns a package's slots, or fills few of them.
    public const double SuggestedChangePercent = 5;

    /// No suggestion moves a price further than this, either way.
    public const double MaxChangePercent = 10;

    /// A cut is only suggested for a package with at least this many open slots...
    public const int OfferMinimumOpenSlots = 10;

    /// ...when even the high end of its expected demand fills no more than this share of them.
    public const double OfferDemandShare = 0.5;

    public sealed record Estimate(double Value, double Lower, double Upper);

    public sealed record ScenarioMonth(DateOnly Month, Estimate Revenue, double Costs, Estimate AfterCosts);

    public sealed record Scenario(
        bool Ok,
        string? Reason,
        string Basis,
        double? Share,
        double? RecordedShare,
        DateOnly? BasisFrom,
        DateOnly? BasisTo,
        long BasisRevenue,
        long BasisCosts,
        IReadOnlyList<ScenarioMonth> Months,
        IReadOnlyList<DateOnly> PassedMonths,
        DemandSeries Demand);

    public sealed record PriceIdea(string Kind, int CurrentPrice, int SuggestedPrice, double ChangePercent, string Reason);

    public sealed record PackageDemand(
        string Code,
        string Name,
        string Destination,
        int OpenSlots,
        long PassengersInBasis,
        double Share,
        Estimate Expected,
        string Status,
        PriceIdea? Price);

    public sealed record Outlook(
        bool Ok,
        string? Reason,
        IReadOnlyList<DateOnly> Months,
        IReadOnlyList<DateOnly> PassedMonths,
        DateOnly? BasisFrom,
        DateOnly? BasisTo,
        long BasisPassengers,
        Estimate? Agency,
        IReadOnlyList<PackageDemand> Packages,
        DemandSeries Demand);

    private sealed record Ahead(DemandSeries Demand, ForecastResult? Result, IReadOnlyList<(DateOnly Month, ForecastStep Step)> Steps, IReadOnlyList<DateOnly> Passed)
    {
        public bool Ok => Result is { Ok: true } && Demand.UsingLiveRecords;

        public DateOnly LastRecorded => new(Demand.LastMonth.Year, Demand.LastMonth.Month, 1);
    }

    /// The forecast of recorded bookings from this month on. Months the forecast covers that have
    /// already passed (when recent months have nothing recorded) are set apart, not planned for.
    private static Ahead ForecastAhead(AppDbContext db, DateOnly today, DemandMeasure measure)
    {
        var demand = DemandSeriesBuilder.Build(db, today, false, measure);
        if (!demand.UsingLiveRecords) return new Ahead(demand, null, [], []);

        var result = HoltWintersForecaster.Forecast(demand.Values, DemandSeriesBuilder.SeasonLength, 6);
        if (!result.Ok || result.Forecast is null) return new Ahead(demand, result, [], []);

        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var last = new DateOnly(demand.LastMonth.Year, demand.LastMonth.Month, 1);
        var steps = result.Forecast.Select(s => (Month: last.AddMonths(s.Step), Step: s)).ToList();
        return new Ahead(
            demand,
            result,
            steps.Where(s => s.Month >= thisMonth).ToList(),
            steps.Where(s => s.Month < thisMonth).Select(s => s.Month).ToList());
    }

    private const string NoForecast =
        "The forecast needs two full years of recorded bookings before it can be planned with.";

    private const string AllPassed =
        "Every month the forecast covers has already passed, because recent months have no bookings recorded. Record them to bring the forecast up to date.";

    /// What the coming months could leave after costs. Spending is not forecast: with the "share"
    /// basis it keeps a share of the forecast revenue (by default the share it took over the last 12
    /// recorded months); with "last-year" it repeats what was spent in the same month a year earlier.
    public static Scenario CostScenario(AppDbContext db, DateOnly today, string basis, double? share)
    {
        var ahead = ForecastAhead(db, today, DemandMeasure.Revenue);
        if (!ahead.Ok)
            return new Scenario(false, NoForecast, basis, null, null, null, null, 0, 0, [], [], ahead.Demand);

        var basisFrom = ahead.LastRecorded.AddMonths(-11);
        var basisTo = ahead.LastRecorded.AddMonths(1).AddDays(-1);
        var recorded = ReportBuilder.Build(db, today, basisFrom, basisTo).Current;
        double? recordedShare = recorded.Revenue > 0 ? (double)recorded.Costs / recorded.Revenue : null;

        Scenario Fail(string reason, double? used) => new(
            false, reason, basis, used, recordedShare, basisFrom, basisTo, recorded.Revenue, recorded.Costs, [], ahead.Passed, ahead.Demand);

        if (ahead.Steps.Count == 0) return Fail(AllPassed, share ?? recordedShare);

        List<ScenarioMonth> months;
        double? used = null;
        if (basis == "last-year")
        {
            var first = ahead.Steps[0].Month.AddMonths(-12);
            var last = ahead.Steps[^1].Month.AddMonths(-11).AddDays(-1);
            var spent = ReportBuilder.Build(db, today, first, last).Months.ToDictionary(m => m.Start, m => (double)m.Costs);
            months = ahead.Steps.Select(a =>
            {
                var costs = spent.GetValueOrDefault(a.Month.AddMonths(-12));
                return new ScenarioMonth(
                    a.Month,
                    Revenue(a.Step),
                    Math.Round(costs),
                    new Estimate(Math.Round(a.Step.Value - costs), Math.Round(a.Step.Lower - costs), Math.Round(a.Step.Upper - costs)));
            }).ToList();
        }
        else
        {
            used = share ?? recordedShare;
            if (used is not double kept)
                return Fail("No booking revenue is recorded in the last 12 months, so there is no share of spending to start from. Enter a share to try one.", null);

            months = ahead.Steps.Select(a => new ScenarioMonth(
                a.Month,
                Revenue(a.Step),
                Math.Round(a.Step.Value * kept),
                new Estimate(
                    Math.Round(a.Step.Value * (1 - kept)),
                    Math.Round(a.Step.Lower * (1 - kept)),
                    Math.Round(a.Step.Upper * (1 - kept))))).ToList();
        }

        return new Scenario(true, null, basis, used, recordedShare, basisFrom, basisTo, recorded.Revenue, recorded.Costs, months, ahead.Passed, ahead.Demand);
    }

    private static Estimate Revenue(ForecastStep step) =>
        new(Math.Round(step.Value), Math.Round(step.Lower), Math.Round(step.Upper));

    /// Each package's expected passengers over the coming months, against the slots it has open.
    /// The forecast is agency-wide, so a package's part of it is its share of the passengers booked
    /// over the last 12 recorded months; bookings without a package (tailored trips) belong to none.
    public static Outlook PackageOutlook(AppDbContext db, DateOnly today, int monthsAhead)
    {
        var ahead = ForecastAhead(db, today, DemandMeasure.Passengers);
        if (!ahead.Ok) return new Outlook(false, NoForecast, [], [], null, null, 0, null, [], ahead.Demand);
        if (ahead.Steps.Count == 0) return new Outlook(false, AllPassed, [], ahead.Passed, null, null, 0, null, [], ahead.Demand);

        var steps = ahead.Steps.Take(monthsAhead).ToList();
        var agency = new Estimate(
            Math.Round(steps.Sum(s => s.Step.Value), 1),
            Math.Round(steps.Sum(s => s.Step.Lower), 1),
            Math.Round(steps.Sum(s => s.Step.Upper), 1));

        var basisFrom = ahead.LastRecorded.AddMonths(-11);
        var basisTo = ahead.LastRecorded.AddMonths(1).AddDays(-1);
        var booked = db.Bookings
            .Where(b => b.VoidedAt == null && b.BookingDate >= basisFrom && b.BookingDate <= basisTo)
            .Select(b => new { b.TravelPackageId, b.PassengerCount })
            .ToList();
        long total = booked.Sum(b => (long)b.PassengerCount);
        var byPackage = booked.Where(b => b.TravelPackageId != null)
            .GroupBy(b => b.TravelPackageId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(b => (long)b.PassengerCount));

        var span = steps.Count == 1
            ? Label(steps[0].Month)
            : $"{Label(steps[0].Month)} to {Label(steps[^1].Month)}";

        var packages = db.TravelPackages.ToList()
            .Select(p =>
            {
                long passengers = byPackage.GetValueOrDefault(p.Id);
                double share = total > 0 ? (double)passengers / total : 0;
                var expected = new Estimate(
                    Math.Round(agency.Value * share, 1),
                    Math.Round(agency.Lower * share, 1),
                    Math.Round(agency.Upper * share, 1));
                var status = passengers == 0 ? "no-history"
                    : expected.Lower > p.AvailableSlots ? "likely-short"
                    : expected.Upper > p.AvailableSlots ? "could-short"
                    : "enough";
                return new PackageDemand(
                    p.Code, p.PackageName, p.Destination, p.AvailableSlots, passengers, Math.Round(share, 4),
                    expected, status, PriceFor(p.BasePrice, p.AvailableSlots, expected, status, span));
            })
            .OrderBy(p => StatusOrder(p.Status))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Outlook(true, null, steps.Select(s => s.Month).ToList(), ahead.Passed, basisFrom, basisTo, total, agency, packages, ahead.Demand);
    }

    /// A rise when demand is expected to outrun the open slots even at the low end of the range; a
    /// limited offer when even the high end fills under half of plenty of open slots. Prices move
    /// in whole hundreds of pesos and never more than MaxChangePercent either way.
    private static PriceIdea? PriceFor(int price, int open, Estimate expected, string status, string span)
    {
        // A full package has nothing left to price.
        if (price <= 0 || open <= 0) return null;

        if (status == "likely-short")
        {
            int suggested = Math.Min(Hundreds(price * (1 + SuggestedChangePercent / 100)), (int)Math.Floor(price * (1 + MaxChangePercent / 100)));
            if (suggested <= price) return null;
            return new PriceIdea("raise", price, suggested, Change(price, suggested),
                $"About {Whole(expected.Lower)} to {Whole(expected.Upper)} passengers are expected from {span}, more than its {Slots(open)} even at the low end.");
        }

        if (status == "enough" && open >= OfferMinimumOpenSlots && expected.Upper <= open * OfferDemandShare)
        {
            int suggested = Math.Max(Hundreds(price * (1 - SuggestedChangePercent / 100)), (int)Math.Ceiling(price * (1 - MaxChangePercent / 100)));
            if (suggested >= price) return null;
            return new PriceIdea("offer", price, suggested, Change(price, suggested),
                $"At most about {Whole(expected.Upper)} passengers are expected from {span}, under half of its {Slots(open)}.");
        }

        return null;
    }

    private static int Hundreds(double value) => (int)(Math.Round(value / 100, MidpointRounding.AwayFromZero) * 100);

    private static double Change(int from, int to) => Math.Round((to - from) * 100.0 / from, 1);

    private static string Slots(int open) => open == 1 ? "1 open slot" : $"{open} open slots";

    private static string Whole(double value) => Math.Round(value).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private static int StatusOrder(string status) => status switch
    {
        "likely-short" => 0,
        "could-short" => 1,
        "enough" => 2,
        _ => 3,
    };

    public static string Label(DateOnly month) => month.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
