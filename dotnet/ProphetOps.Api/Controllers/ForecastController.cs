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

    [HttpGet]
    public IActionResult Get()
    {
        var demand = DemandSeriesBuilder.Build(_db, _clock.Today, AllowSampleFallback());
        var anchor = demand.LastMonth;
        var series = demand.Values.ToList();

        if (!demand.UsingLiveRecords && !demand.UsingSample)
        {
            return Ok(new
            {
                method = "Holt-Winters",
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

        var recentMean = series.Count > 0 ? series.Skip(Math.Max(0, series.Count - 6)).Average() : 0;
        var forecastMean = forecastSteps.Count > 0 ? forecastSteps.Average(s => s.Value) : recentMean;
        var changePercent = recentMean != 0 ? Math.Round((forecastMean - recentMean) / recentMean * 100, 1) : 0;
        var direction = changePercent > 1 ? "up" : changePercent < -1 ? "down" : "flat";

        var peak = forecastSteps.OrderByDescending(s => s.Value).FirstOrDefault();
        var peakMonth = peak is not null
            ? anchor.AddMonths(peak.Step).ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : "";
        var peakValue = peak?.Value ?? 0;

        var notes = forecast.Ok
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

    private bool AllowSampleFallback() =>
        _configuration.GetValue<bool>("Demo:Enabled") && !_environment.IsProduction();

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
