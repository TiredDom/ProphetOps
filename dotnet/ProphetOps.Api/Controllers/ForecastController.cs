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
        var series = SampleSalesHistory.MonthlyRevenue(2026, 7).Select(v => (double)v).ToList();
        var forecast = HoltWintersForecaster.Forecast(series, 12, 6);

        var metrics = forecast.Metrics;
        var mape = metrics?.Mape ?? 0;
        var accuracy = forecast.Ok ? Math.Max(0, (int)Math.Round(100 - mape)) : 0;

        var recent = series.Skip(Math.Max(0, series.Count - 12)).ToList();
        var history = recent
            .Select((value, index) =>
            {
                var back = recent.Count - 1 - index;
                return new { label = back == 0 ? "M0" : "M-" + back, value };
            })
            .ToList();

        var steps = (forecast.Forecast ?? new List<ForecastStep>())
            .Select(s => new { step = s.Step, value = s.Value, lower = s.Lower, upper = s.Upper })
            .ToList();

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
            history,
            steps,
        });
    }
}
