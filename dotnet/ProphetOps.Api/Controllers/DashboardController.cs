using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Forecasting;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "Dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        var revenue = _db.Bookings.Sum(b => b.GrossRevenue);
        var costs = _db.Expenses.Sum(e => e.Amount);

        var series = SampleSalesHistory.MonthlyRevenue(2026, 7).Select(v => (double)v).ToList();
        var forecast = HoltWintersForecaster.Forecast(series, 12, 6);
        var accuracy = forecast.Ok ? Math.Max(0, (int)Math.Round(100 - forecast.Metrics!.Mape)) : 0;

        return Ok(new
        {
            revenue,
            costs,
            estimatedProfit = Math.Max(revenue - costs, 0),
            bookings = _db.Bookings.Count(),
            packages = _db.TravelPackages.Count(),
            expenses = _db.Expenses.Count(),
            forecast = new
            {
                method = "Holt-Winters",
                horizon = 6,
                accuracy,
                mape = forecast.Ok ? forecast.Metrics!.Mape : 0,
                nextValue = forecast.Ok ? forecast.Forecast![0].Value : 0,
            },
            lastUpdated = DateTime.UtcNow.ToString("MMM d, yyyy"),
        });
    }
}
