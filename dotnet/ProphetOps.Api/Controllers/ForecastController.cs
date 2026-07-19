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

    public ForecastController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        var demand = DemandSeriesBuilder.Build(_db, DateOnly.FromDateTime(DateTime.Today));
        var anchor = demand.LastMonth;
        var series = demand.Values.ToList();
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
            dataSource = new
            {
                usingLiveRecords = demand.UsingLiveRecords,
                liveMonthsAvailable = demand.LiveMonthsAvailable,
                minimumMonths = demand.MinimumMonths,
                filledMonths = demand.FilledMonths,
            },
            history,
            steps,
        });
    }
}
