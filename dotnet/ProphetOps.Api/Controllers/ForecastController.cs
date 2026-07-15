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
    [HttpGet]
    public IActionResult Get()
    {
        var anchor = new DateOnly(2026, 7, 1);
        var series = SampleSalesHistory.MonthlyRevenue(anchor.Year, anchor.Month).Select(v => (double)v).ToList();
        var forecast = HoltWintersForecaster.Forecast(series, 12, 6);

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
            },
            history,
            steps,
        });
    }
}
