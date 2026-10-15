using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProphetOps.Data;
using ProphetOps.Forecasting;

namespace ProphetOps.Api;

/// One forecast month: the Holt-Winters figure and its 80% range.
public sealed record FactMonth(string Month, double Value, double Lower, double Upper);

/// How the forecast did on recorded months it had not seen (the rolling-origin test).
public sealed record FactTest(
    string From,
    string To,
    int PastMonths,
    int Forecasts,
    double? Mape,
    double? SameMonthLastYearMape,
    int InsideRange,
    double CoveragePercent,
    double? SmallerMissPercent);

/// Everything an explanation of the forecast may speak of: agency-wide monthly totals, the
/// Holt-Winters forecast with its range, and the accuracy test. No client, booking, package or
/// person is in here, which is what makes it safe to hand to a writing service.
public sealed record ForecastFacts(
    DemandMeasure Measure,
    bool SampleFigures,
    string LastRecordedMonth,
    double LastRecordedValue,
    IReadOnlyList<FactMonth> Months,
    double SixMonthTotal,
    string Direction,
    double ChangePercent,
    string PeakMonth,
    double PeakValue,
    IReadOnlyList<string> PassedMonths,
    FactTest? Test)
{
    /// The facts behind the forecast Home shows for this measure, or null when there is no
    /// forecast yet. Figures are rounded as they are shown: whole pesos or whole counts.
    public static ForecastFacts? Build(AppDbContext db, DateOnly today, bool allowSample, DemandMeasure measure)
    {
        var demand = DemandSeriesBuilder.Build(db, today, allowSample, measure);
        if (!demand.UsingLiveRecords && !demand.UsingSample) return null;

        var series = demand.Values;
        var forecast = HoltWintersForecaster.Forecast(series, DemandSeriesBuilder.SeasonLength, 6);
        if (!forecast.Ok || forecast.Forecast is not { Count: > 0 } steps) return null;

        var anchor = demand.LastMonth;
        string Label(DateOnly month) => month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        var months = steps
            .Select(s => new FactMonth(Label(anchor.AddMonths(s.Step)), Math.Round(s.Value), Math.Round(s.Lower), Math.Round(s.Upper)))
            .ToList();
        var (direction, changePercent) = ForecastTrend.Of(series, steps);
        var peak = ForecastTrend.Peak(steps)!;

        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var last = new DateOnly(anchor.Year, anchor.Month, 1);
        var passed = steps.Select(s => last.AddMonths(s.Step)).Where(m => m < thisMonth).Select(Label).ToList();

        return new ForecastFacts(
            measure,
            demand.UsingSample,
            Label(anchor),
            Math.Round(series[^1]),
            months,
            months.Sum(m => m.Value),
            direction,
            changePercent,
            Label(anchor.AddMonths(peak.Step)),
            Math.Round(peak.Value),
            passed,
            AccuracyTest(series, anchor, Label));
    }

    private static FactTest? AccuracyTest(IReadOnlyList<double> series, DateOnly anchor, Func<DateOnly, string> label)
    {
        var test = RollingOriginEvaluation.Evaluate(series, DemandSeriesBuilder.SeasonLength, 6);
        if (!test.Ok || test.Overall is not { } overall) return null;

        var first = anchor.AddMonths(-(series.Count - 1));
        return new FactTest(
            label(first.AddMonths(test.FirstOrigin)),
            label(first.AddMonths(test.MonthsAvailable - 1)),
            test.Origins,
            overall.Forecasts,
            overall.Mape is { } mape ? Math.Round(mape, 1) : null,
            overall.SeasonalNaiveMape is { } naive ? Math.Round(naive, 1) : null,
            overall.InsideRange,
            Math.Round(overall.Coverage * 100),
            overall.SkillVsSeasonalNaive is { } skill ? Math.Round(skill * 100) : null);
    }

    public string Amount(double value) => Measure switch
    {
        DemandMeasure.Bookings => $"{Whole(value)} {(Math.Round(value) == 1 ? "booking" : "bookings")}",
        DemandMeasure.Passengers => $"{Whole(value)} {(Math.Round(value) == 1 ? "passenger" : "passengers")}",
        _ => "₱" + Whole(value),
    };

    public static string Whole(double value) => Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);

    public static string Percent(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// The facts as the writing service reads them: the same figures, already worded and rounded,
    /// so that it can repeat them rather than work anything out.
    public JsonObject ForWriter()
    {
        var facts = new JsonObject
        {
            ["measure"] = Measure switch
            {
                DemandMeasure.Bookings => "number of bookings a month, counted by booking date",
                DemandMeasure.Passengers => "number of passengers a month, counted by booking date",
                _ => "booking revenue a month, in Philippine pesos (not profit, not cash received)",
            },
            ["figures"] = SampleFigures ? "demonstration figures, not the agency's records" : "the agency's recorded bookings",
            ["method"] = "Holt-Winters (additive) with a 12-month season; each forecast month has an 80% range",
            ["lastRecordedMonth"] = LastRecordedMonth,
            ["lastRecordedFigure"] = Amount(LastRecordedValue),
            ["forecastMonths"] = new JsonArray(Months.Select(m => (JsonNode)new JsonObject
            {
                ["month"] = m.Month,
                ["forecast"] = Amount(m.Value),
                ["likelyRange"] = $"{Amount(m.Lower)} to {Amount(m.Upper)}",
            }).ToArray()),
            ["sixMonthTotal"] = Amount(SixMonthTotal),
            ["direction"] = Direction,
            ["changeAgainstLastSixRecordedMonths"] = (ChangePercent > 0 ? "+" : ChangePercent < 0 ? "-" : "") + Percent(Math.Abs(ChangePercent)),
            ["highestForecastMonth"] = new JsonObject { ["month"] = PeakMonth, ["forecast"] = Amount(PeakValue) },
            ["forecastMonthsAlreadyPassedWithNoBookingsRecorded"] = new JsonArray(PassedMonths.Select(m => (JsonNode)JsonValue.Create(m)).ToArray()),
        };

        facts["accuracyTestOnUnseenMonths"] = Test is null
            ? "not available yet: it needs at least 25 recorded months"
            : new JsonObject
            {
                ["pastMonthsTestedFrom"] = Test.PastMonths,
                ["testedMonths"] = $"{Test.From} to {Test.To}",
                ["forecastsChecked"] = Test.Forecasts,
                ["averageMiss"] = Test.Mape is { } mape ? Percent(mape) : "not available",
                ["sameMonthLastYearAverageMiss"] = Test.SameMonthLastYearMape is { } naive ? Percent(naive) : "not available",
                ["insideTheRange"] = $"{Test.InsideRange} of {Test.Forecasts}",
                ["insideTheRangeShare"] = Percent(Test.CoveragePercent),
                ["rangeAimsFor"] = "about 8 in 10",
                ["smallerMissThanSameMonthLastYear"] = Test.SmallerMissPercent is { } skill ? Percent(skill) : "not available",
            };
        return facts;
    }

    // The peso sign stays readable: this is text for the writer, never put into a page.
    private static readonly JsonSerializerOptions WriterJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string ForWriterJson() => ForWriter().ToJsonString(WriterJson);
}

/// ProphetOps' own wording of the forecast, written by rules from the facts alone. It is always
/// available, and it is what is shown whenever the optional writing service is off or fails.
public static class ForecastWording
{
    public static (string Summary, IReadOnlyList<string> Points) Write(ForecastFacts facts)
    {
        var first = facts.Months[0].Month;
        var last = facts.Months[^1].Month;
        var (subject, verb) = facts.Measure switch
        {
            DemandMeasure.Bookings => ("Bookings", "are"),
            DemandMeasure.Passengers => ("Passenger numbers", "are"),
            _ => ("Booking revenue", "is"),
        };
        var change = ForecastFacts.Percent(Math.Abs(facts.ChangePercent));
        var movement = facts.Direction switch
        {
            "up" => $"forecast to rise about {change} from {first} to {last}, against the last six recorded months,",
            "down" => $"forecast to fall about {change} from {first} to {last}, against the last six recorded months,",
            _ => $"forecast to stay about level from {first} to {last},",
        };
        var summary = $"{subject} {verb} {movement} with the highest month {facts.PeakMonth} at about {facts.Amount(facts.PeakValue)}.";

        var peak = facts.Months.First(m => m.Month == facts.PeakMonth);
        var points = new List<string>
        {
            $"Each month comes with an 80% range: for {facts.PeakMonth}, {facts.Amount(peak.Lower)} to {facts.Amount(peak.Upper)}. " +
            "In about 8 months out of 10 the recorded figure should land inside it.",
            facts.Measure == DemandMeasure.Revenue
                ? $"Across the six months that is about {facts.Amount(facts.SixMonthTotal)} in booking revenue, before any costs."
                : $"Across the six months that is about {facts.Amount(facts.SixMonthTotal)}, counted by booking date.",
        };

        if (facts.Test is { } test)
        {
            var tested = $"Tested on {ForecastFacts.Whole(test.Forecasts)} forecasts of months it had not seen ({test.From} to {test.To})";
            points.Add(test.Mape is { } mape && test.SameMonthLastYearMape is { } naive
                ? $"{tested}, it missed by {ForecastFacts.Percent(mape)} on average, {Against(mape, naive)}, " +
                  $"and the recorded figure fell inside the range in {test.InsideRange} of them."
                : $"{tested}, the recorded figure fell inside the range in {test.InsideRange} of them.");
        }
        else
        {
            points.Add("There are not yet enough recorded months to test it on months it had not seen.");
        }

        if (facts.SampleFigures)
            points.Add("These are demonstration figures, not the agency's records.");
        else if (facts.PassedMonths.Count > 0)
            points.Add($"{Join(facts.PassedMonths)} {(facts.PassedMonths.Count == 1 ? "has" : "have")} already passed with no bookings recorded; recording them brings the forecast up to date.");
        else
            points.Add($"It learns from the bookings recorded up to {facts.LastRecordedMonth}.");

        return (summary, points);
    }

    /// Says which way the comparison went, so a forecast that did worse than the baseline reads so.
    private static string Against(double mape, double naive)
    {
        var baseline = $"the {ForecastFacts.Percent(naive)} of repeating the same month last year";
        if (Math.Round(mape, 1) < Math.Round(naive, 1)) return "less than " + baseline;
        if (Math.Round(mape, 1) > Math.Round(naive, 1)) return "more than " + baseline;
        return "the same as " + baseline;
    }

    private static string Join(IReadOnlyList<string> names) =>
        names.Count <= 1 ? names.FirstOrDefault() ?? "" : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
}
