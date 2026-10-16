using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Forecasting;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/forecast")]
[Authorize(Policy = "Forecast")]
public class ForecastController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBusinessClock _clock;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ForecastController(AppDbContext db, IBusinessClock clock, IConfiguration configuration, IHostEnvironment environment)
    {
        _db = db;
        _clock = clock;
        _configuration = configuration;
        _environment = environment;
    }

    /// The same Holt-Winters forecast for one of three monthly measures: booking revenue (the
    /// default and the study's main measure), the number of bookings, or the number of passengers.
    /// Counts are by booking date; no departure dates are recorded.
    [HttpGet]
    public IActionResult Get([FromQuery] string? measure)
    {
        if (!TryReadMeasure(measure, out var chosen))
            return BadRequest(new Dictionary<string, string> { ["measure"] = ForecastMeasures.Choices });

        var demand = DemandSeriesBuilder.Build(_db, _clock.Today, AllowSampleFallback(), chosen);
        var anchor = demand.LastMonth;
        var series = demand.Values.ToList();

        if (!demand.UsingLiveRecords && !demand.UsingSample)
        {
            return Ok(new
            {
                method = "Holt-Winters",
                measure = MeasureName(chosen),
                unit = UnitName(chosen),
                seasonLength = 12,
                horizon = 6,
                ok = false,
                accuracy = 0,
                @params = new { alpha = 0, beta = 0, gamma = 0 },
                metrics = new { mae = 0, rmse = 0, mape = 0, sampleSize = 0 },
                baselines = new { seasonalNaiveMae = 0, naiveMae = 0 },
                insight = new
                {
                    direction = "flat",
                    changePercent = 0,
                    peakMonth = "",
                    peakValue = 0,
                    notes = Array.Empty<object>(),
                },
                dataSource = DataSource(demand),
                history = Array.Empty<object>(),
                steps = Array.Empty<object>(),
            });
        }

        var forecast = HoltWintersForecaster.Forecast(series, DemandSeriesBuilder.SeasonLength, 6);

        var metrics = forecast.Metrics;
        var mape = metrics?.Mape ?? 0;
        var accuracy = forecast.Ok ? Math.Max(0, (int)Math.Round(100 - mape)) : 0;

        var recent = series.Skip(Math.Max(0, series.Count - 12)).ToList();
        var history = recent
            .Select((value, index) =>
            {
                var back = recent.Count - 1 - index;
                var month = anchor.AddMonths(-back).ToString("MMM", CultureInfo.InvariantCulture);
                return new { label = back == 0 ? "M0" : "M-" + back, month, value };
            })
            .ToList();

        var forecastSteps = forecast.Forecast ?? new List<ForecastStep>();
        var steps = forecastSteps
            .Select(s => new
            {
                step = s.Step,
                month = anchor.AddMonths(s.Step).ToString("MMM", CultureInfo.InvariantCulture),
                monthLabel = anchor.AddMonths(s.Step).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                value = s.Value,
                lower = s.Lower,
                upper = s.Upper,
            })
            .ToList();

        var (direction, changePercent) = ForecastTrend.Of(series, forecastSteps);

        var peak = ForecastTrend.Peak(forecastSteps);
        var peakMonth = peak is not null
            ? anchor.AddMonths(peak.Step).ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : "";
        var peakValue = peak?.Value ?? 0;

        var notes = forecast.Ok && chosen == DemandMeasure.Revenue
            ? TrajectoryInsights.Build(new TrajectoryInput
            {
                Steps = forecastSteps
                    .Select(s => new TrajectoryStep(
                        anchor.AddMonths(s.Step).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                        s.Value,
                        s.Lower,
                        s.Upper))
                    .ToList(),
                Direction = direction,
                ChangePercent = changePercent,
                LastRecordedLabel = anchor.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                LastRecordedValue = series.Count > 0 ? series[^1] : 0,
                Mape = mape,
                Accuracy = accuracy,
                Mae = metrics?.Mae ?? 0,
                SeasonalNaiveMae = forecast.Baselines?.SeasonalNaiveMae ?? 0,
                UnusualMonths = demand.UsingLiveRecords
                    ? SeriesAnomalies.Detect(series)
                        .Select(a => anchor.AddMonths(a.Index - (series.Count - 1))
                            .ToString("MMMM yyyy", CultureInfo.InvariantCulture))
                        .ToList()
                    : [],
            })
            : new List<TrajectoryNote>();

        return Ok(new
        {
            method = "Holt-Winters",
            measure = MeasureName(chosen),
            unit = UnitName(chosen),
            seasonLength = 12,
            horizon = 6,
            ok = forecast.Ok,
            accuracy,
            @params = new
            {
                alpha = forecast.Params?.Alpha ?? 0,
                beta = forecast.Params?.Beta ?? 0,
                gamma = forecast.Params?.Gamma ?? 0,
            },
            metrics = new
            {
                mae = metrics?.Mae ?? 0,
                rmse = metrics?.Rmse ?? 0,
                mape,
                sampleSize = metrics?.SampleSize ?? 0,
            },
            baselines = new
            {
                seasonalNaiveMae = forecast.Baselines?.SeasonalNaiveMae ?? 0,
                naiveMae = forecast.Baselines?.NaiveMae ?? 0,
            },
            insight = new
            {
                direction,
                changePercent,
                peakMonth,
                peakValue,
                notes = notes.Select(n => new { kind = n.Kind, text = n.Text }),
            },
            dataSource = DataSource(demand),
            history,
            steps,
        });
    }

    /// How the forecast has done on recorded months it had not seen. The test stands at each past
    /// month in turn, forecasts the following months from the earlier ones with the same Holt-Winters
    /// method, and compares each forecast with what was recorded, beside repeating the same month a
    /// year earlier. It reads the same monthly series the forecast does.
    [HttpGet("evaluation")]
    public IActionResult Evaluation([FromQuery] string? measure)
    {
        if (!TryReadMeasure(measure, out var chosen))
            return BadRequest(new Dictionary<string, string> { ["measure"] = ForecastMeasures.Choices });

        var demand = DemandSeriesBuilder.Build(_db, _clock.Today, AllowSampleFallback(), chosen);
        var usable = demand.UsingLiveRecords || demand.UsingSample;
        var test = usable
            ? RollingOriginEvaluation.Evaluate(demand.Values, DemandSeriesBuilder.SeasonLength, 6)
            : null;

        var first = demand.LastMonth.AddMonths(-(demand.Values.Count - 1));
        string Month(int index) => first.AddMonths(index).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        if (test is null || !test.Ok)
        {
            return Ok(new
            {
                method = "Rolling-origin test",
                measure = MeasureName(chosen),
                unit = UnitName(chosen),
                ok = false,
                reason = test?.Reason ?? "Holt-Winters needs at least two full years of recorded months before it can be tested.",
                seasonLength = DemandSeriesBuilder.SeasonLength,
                horizon = 6,
                monthsAvailable = test?.MonthsAvailable ?? demand.RecordedMonths,
                monthsRequired = test?.MonthsRequired ?? DemandSeriesBuilder.MinimumMonths + 1,
                origins = 0,
                dataSource = DataSource(demand),
                byHorizon = Array.Empty<object>(),
                overall = (object?)null,
                points = Array.Empty<object>(),
            });
        }

        return Ok(new
        {
            method = "Rolling-origin test",
            measure = MeasureName(chosen),
            unit = UnitName(chosen),
            ok = true,
            reason = (string?)null,
            seasonLength = test.SeasonLength,
            horizon = test.Horizon,
            monthsAvailable = test.MonthsAvailable,
            monthsRequired = test.MonthsRequired,
            origins = test.Origins,
            firstTestedMonth = Month(test.FirstOrigin),
            lastTestedMonth = Month(test.MonthsAvailable - 1),
            dataSource = DataSource(demand),
            byHorizon = test.ByHorizon.Select(h => new
            {
                step = h.Step,
                forecasts = h.Forecasts,
                mae = h.Mae,
                mape = h.Mape,
                seasonalNaiveMae = h.SeasonalNaiveMae,
                seasonalNaiveMape = h.SeasonalNaiveMape,
                insideRange = h.InsideRange,
                coverage = h.Coverage,
            }),
            overall = new
            {
                forecasts = test.Overall!.Forecasts,
                mae = test.Overall.Mae,
                mape = test.Overall.Mape,
                seasonalNaiveMae = test.Overall.SeasonalNaiveMae,
                seasonalNaiveMape = test.Overall.SeasonalNaiveMape,
                insideRange = test.Overall.InsideRange,
                coverage = test.Overall.Coverage,
                skillVsSeasonalNaive = test.Overall.SkillVsSeasonalNaive,
            },
            points = test.Points.Select(p => new
            {
                madeAfter = Month(p.Origin - 1),
                month = Month(p.Target),
                step = p.Step,
                actual = p.Actual,
                forecast = p.Forecast,
                lower = p.Lower,
                upper = p.Upper,
                sameMonthLastYear = p.SeasonalNaive,
                insideRange = p.InsideRange,
            }),
        });
    }

    /// The forecast in plain words, as ProphetOps words it by rules from the forecast's own figures.
    /// It says whether Gemini is set up to word the same figures instead (the POST below).
    [HttpGet("explanation")]
    public IActionResult Explanation([FromQuery] string? measure, [FromServices] ForecastExplainer explainer)
    {
        if (!TryReadMeasure(measure, out var chosen))
            return BadRequest(new Dictionary<string, string> { ["measure"] = ForecastMeasures.Choices });

        var facts = ForecastFacts.Build(_db, _clock.Today, AllowSampleFallback(), chosen);
        return Ok(ExplanationBody(chosen, facts, facts is null ? null : ForecastExplainer.Rules(facts), explainer));
    }

    /// Asks Gemini to word the forecast, when the agency has set it up, trying its models strongest
    /// first. Gemini is given only the agency's monthly totals, the forecast and its accuracy test,
    /// never a client, a booking or a person, and a wording is used only if every figure in it is
    /// one of the forecast's own. When no model can give one, or Gemini is not set up or rejects
    /// the key, the rules' wording comes back with the reason.
    [HttpPost("explanation")]
    public async Task<IActionResult> ExplainWithGemini(
        [FromQuery] string? measure,
        [FromServices] ForecastExplainer explainer,
        CancellationToken cancellationToken)
    {
        if (!TryReadMeasure(measure, out var chosen))
            return BadRequest(new Dictionary<string, string> { ["measure"] = ForecastMeasures.Choices });

        var facts = ForecastFacts.Build(_db, _clock.Today, AllowSampleFallback(), chosen);
        var explanation = facts is null ? null : await explainer.WithGeminiAsync(facts, cancellationToken);
        return Ok(ExplanationBody(chosen, facts, explanation, explainer));
    }

    private static object ExplanationBody(DemandMeasure measure, ForecastFacts? facts, Explanation? explanation, ForecastExplainer explainer) => new
    {
        measure = MeasureName(measure),
        ok = explanation is not null,
        reason = explanation is null
            ? "The forecast needs two full years of recorded months before it can be put into words."
            : null,
        source = explanation?.Source,
        model = explanation?.Model,
        passedOver = explanation?.PassedOver ?? [],
        fallbackReason = explanation?.FallbackReason,
        summary = explanation?.Summary,
        points = explanation?.Points ?? [],
        aiAvailable = explainer.AiAvailable,
        aiModels = explainer.Models,
        facts = facts?.ForWriter(),
    };

    /// What the coming months could leave after costs under one stated spending assumption:
    /// spending keeps a share of the revenue forecast ("share", by default the share it took over
    /// the last 12 recorded months, or the one given), or repeats the same month a year earlier
    /// ("last-year"). Spending itself is not forecast.
    [HttpGet("scenario")]
    [Authorize(Policy = "Expenses")]
    public IActionResult Scenario([FromQuery] string? basis, [FromQuery] string? share)
    {
        var errors = new Dictionary<string, string>();
        var chosen = string.IsNullOrWhiteSpace(basis) ? "share" : basis.Trim().ToLowerInvariant();
        if (chosen is not ("share" or "last-year")) errors["basis"] = "Use share or last-year.";

        double? kept = null;
        if (!string.IsNullOrWhiteSpace(share))
        {
            if (double.TryParse(share, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && double.IsFinite(value) && value is >= 0 and <= 2)
                kept = value;
            else
                errors["share"] = "Use a share of revenue between 0 and 2, that is 0% to 200%.";
        }
        if (errors.Count > 0) return BadRequest(errors);

        var s = ForecastPlanning.CostScenario(_db, _clock.Today, chosen, kept);
        object? period = s.BasisFrom is DateOnly from && s.BasisTo is DateOnly to
            ? new { from = ForecastPlanning.Label(from), to = ForecastPlanning.Label(to), revenue = s.BasisRevenue, costs = s.BasisCosts }
            : null;
        object? total = s.Ok
            ? new
            {
                revenue = s.Months.Sum(m => m.Revenue.Value),
                costs = s.Months.Sum(m => m.Costs),
                afterCosts = s.Months.Sum(m => m.AfterCosts.Value),
                afterCostsLower = s.Months.Sum(m => m.AfterCosts.Lower),
                afterCostsUpper = s.Months.Sum(m => m.AfterCosts.Upper),
            }
            : null;

        return Ok(new
        {
            ok = s.Ok,
            reason = s.Reason,
            basis = s.Basis,
            share = s.Share is double used ? Math.Round(used, 4) : (double?)null,
            recordedShare = s.RecordedShare is double recorded ? Math.Round(recorded, 4) : (double?)null,
            basisPeriod = period,
            passedMonths = s.PassedMonths.Select(ForecastPlanning.Label),
            months = s.Months.Select(m => new
            {
                month = ForecastPlanning.Label(m.Month),
                revenue = m.Revenue.Value,
                revenueLower = m.Revenue.Lower,
                revenueUpper = m.Revenue.Upper,
                costs = m.Costs,
                afterCosts = m.AfterCosts.Value,
                afterCostsLower = m.AfterCosts.Lower,
                afterCostsUpper = m.AfterCosts.Upper,
            }),
            total,
            dataSource = DataSource(s.Demand),
        });
    }

    /// Each package's expected passengers over the coming months (1 to 6, default 3) against the
    /// slots it has open, from its share of the passengers booked over the last 12 recorded months,
    /// with a price worth considering where demand clearly outruns or undershoots the slots.
    [HttpGet("packages")]
    public IActionResult Packages([FromQuery] string? months)
    {
        var horizon = 3;
        if (!string.IsNullOrWhiteSpace(months) && (!int.TryParse(months, NumberStyles.None, CultureInfo.InvariantCulture, out horizon) || horizon is < 1 or > 6))
            return BadRequest(new Dictionary<string, string> { ["months"] = "Use 1 to 6 months." });

        var o = ForecastPlanning.PackageOutlook(_db, _clock.Today, horizon);
        object? period = o.BasisFrom is DateOnly from && o.BasisTo is DateOnly to
            ? new { from = ForecastPlanning.Label(from), to = ForecastPlanning.Label(to), passengers = o.BasisPassengers }
            : null;
        object? agency = o.Agency is { } a ? new { value = a.Value, lower = a.Lower, upper = a.Upper } : null;

        return Ok(new
        {
            ok = o.Ok,
            reason = o.Reason,
            monthsAhead = horizon,
            months = o.Months.Select(ForecastPlanning.Label),
            passedMonths = o.PassedMonths.Select(ForecastPlanning.Label),
            basisPeriod = period,
            agency,
            packages = o.Packages.Select(p => new
            {
                code = p.Code,
                name = p.Name,
                destination = p.Destination,
                openSlots = p.OpenSlots,
                passengersInBasis = p.PassengersInBasis,
                share = p.Share,
                expected = new { value = p.Expected.Value, lower = p.Expected.Lower, upper = p.Expected.Upper },
                status = p.Status,
                price = p.Price is { } x
                    ? new { kind = x.Kind, currentPrice = x.CurrentPrice, suggestedPrice = x.SuggestedPrice, changePercent = x.ChangePercent, reason = x.Reason }
                    : null,
            }),
            dataSource = DataSource(o.Demand),
        });
    }

    private static bool TryReadMeasure(string? value, out DemandMeasure measure) =>
        ForecastMeasures.TryRead(value, out measure);

    private static string MeasureName(DemandMeasure measure) => ForecastMeasures.Name(measure);

    private static string UnitName(DemandMeasure measure) => ForecastMeasures.Unit(measure);

    private bool AllowSampleFallback() => ForecastMeasures.AllowSampleFallback(_configuration, _environment);

    private static object DataSource(DemandSeries demand) => new
    {
        status = DemandStatus(demand),
        usingLiveRecords = demand.UsingLiveRecords,
        usingSample = demand.UsingSample,
        label = demand.UsingSample
            ? "Sample demonstration data"
            : demand.UsingLiveRecords
                ? "Live booking history"
                : "Insufficient booking history",
        liveMonthsAvailable = demand.LiveMonthsAvailable,
        recordedMonths = demand.RecordedMonths,
        minimumMonths = demand.MinimumMonths,
        filledMonths = demand.FilledMonths,
        lastRecordedMonth = demand.LiveMonthsAvailable > 0
            ? demand.LastMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : null,
    };

    private static string DemandStatus(DemandSeries demand) => demand.Source switch
    {
        DemandSeriesSource.LiveRecords => "live",
        DemandSeriesSource.SampleSeries => "sample",
        _ => "insufficient-history",
    };
}
