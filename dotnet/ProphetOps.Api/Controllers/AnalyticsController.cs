using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "Analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        const int anchorYear = 2026;
        const int anchorMonth = 7;

        var history = SampleSalesHistory.MonthlyRevenue(anchorYear, anchorMonth);
        var anchor = new DateOnly(anchorYear, anchorMonth, 1);
        var recent = history.Skip(Math.Max(0, history.Count - 12)).ToList();

        var salesHistory = recent
            .Select((value, i) => new
            {
                label = anchor.AddMonths(-(recent.Count - 1 - i)).ToString("MMM"),
                value,
            })
            .ToList();

        var bookings = _db.Bookings.Where(b => b.VoidedAt == null).ToList();

        var packageMix = bookings
            .GroupBy(b => b.PackageName)
            .Select(g => new { label = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToList();

        var paymentBreakdown = bookings
            .GroupBy(b => b.PaymentStatus)
            .Select(g => new { label = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToList();

        var revenueByDestination = bookings
            .GroupBy(b => b.Destination)
            .Select(g => new { label = g.Key, value = g.Sum(b => b.GrossRevenue) })
            .OrderByDescending(x => x.value)
            .ToList();

        var totalRevenue = bookings.Sum(b => b.GrossRevenue);
        var totalBookings = bookings.Count;
        var averageBooking = totalBookings > 0 ? (int)Math.Round((double)totalRevenue / totalBookings) : 0;

        return Ok(new
        {
            salesHistory,
            packageMix,
            paymentBreakdown,
            revenueByDestination,
            totalRevenue,
            totalBookings,
            averageBooking,
        });
    }
}
