using System.Globalization;
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
        var bookings = _db.Bookings.Where(b => b.VoidedAt == null).ToList();
        var packages = _db.TravelPackages.ToList();

        var revenue = bookings.Sum(b => b.GrossRevenue);
        var costs = _db.Expenses.Where(e => e.VoidedAt == null).Sum(e => e.Amount);

        var demand = DemandSeriesBuilder.Build(_db, DateOnly.FromDateTime(DateTime.Today));
        var anchor = demand.LastMonth;
        var series = demand.Values.ToList();
        var forecast = HoltWintersForecaster.Forecast(series, DemandSeriesBuilder.SeasonLength, 6);
        var mape = forecast.Ok ? forecast.Metrics!.Mape : 0;
        var accuracy = forecast.Ok ? Math.Max(0, (int)Math.Round(100 - mape)) : 0;

        var forecastSteps = forecast.Forecast ?? new List<ForecastStep>();
        var recentMean = series.Count > 0 ? series.Skip(Math.Max(0, series.Count - 6)).Average() : 0;
        var forecastMean = forecastSteps.Count > 0 ? forecastSteps.Average(s => s.Value) : recentMean;
        var changePercent = recentMean != 0 ? Math.Round((forecastMean - recentMean) / recentMean * 100, 1) : 0;
        var direction = changePercent > 1 ? "up" : changePercent < -1 ? "down" : "flat";

        var peak = forecastSteps.OrderByDescending(s => s.Value).FirstOrDefault();
        var peakMonth = peak is not null
            ? anchor.AddMonths(peak.Step).ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : "";
        var peakValue = peak?.Value ?? 0;

        var lowStock = packages.Where(p => p.Status == "Low" || p.Status == "Critical").ToList();
        if (lowStock.Count == 0)
            lowStock = packages.Where(p => p.AvailableSlots <= 5).ToList();
        var lowStockPackages = lowStock
            .OrderBy(p => p.AvailableSlots)
            .Select(p => new
            {
                code = p.Code,
                packageName = p.PackageName,
                destination = p.Destination,
                availableSlots = p.AvailableSlots,
                status = p.Status,
            })
            .ToList();

        var unpaid = bookings.Where(b => b.PaymentStatus != "Paid").ToList();
        var pendingPayments = new
        {
            count = unpaid.Count,
            amount = unpaid.Sum(b => b.GrossRevenue),
        };

        var recentBookings = bookings
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.Id)
            .Take(5)
            .Select(b => new
            {
                code = b.Code,
                client = b.Client,
                package = b.PackageName,
                destination = b.Destination,
                grossRevenue = b.GrossRevenue,
                paymentStatus = b.PaymentStatus,
                bookingStatus = b.BookingStatus,
                ds = b.BookingDate.ToString("yyyy-MM-dd"),
            })
            .ToList();

        return Ok(new
        {
            revenue,
            costs,
            estimatedProfit = Math.Max(revenue - costs, 0),
            bookings = bookings.Count,
            packages = packages.Count,
            expenses = _db.Expenses.Count(e => e.VoidedAt == null),
            forecast = new
            {
                method = "Holt-Winters",
                horizon = 6,
                accuracy,
                mape,
                nextValue = forecast.Ok ? forecast.Forecast![0].Value : 0,
                direction,
                changePercent,
                peakMonth,
                peakValue,
            },
            lowStockPackages,
            pendingPayments,
            recentBookings,
            lastUpdated = DateTime.UtcNow.ToString("MMM d, yyyy"),
        });
    }
}
