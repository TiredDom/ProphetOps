using System.Globalization;
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
    private readonly IBusinessClock _clock;

    public AnalyticsController(AppDbContext db, IBusinessClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] DateOnly? throughMonth = null)
    {
        var cutoff = throughMonth ?? _clock.Today;
        var anchor = new DateOnly(cutoff.Year, cutoff.Month, 1);
        if (anchor < new DateOnly(1, 12, 1))
            return BadRequest(new { message = "The chart cutoff must allow twelve calendar months." });
        var firstMonth = anchor.AddMonths(-11);
        var monthly = MonthlyActuals.Build(_db, anchor).ToDictionary(m => m.Month);
        var recent = Enumerable.Range(0, 12)
            .Select(i => firstMonth.AddMonths(i))
            .Select(month => monthly.GetValueOrDefault(month) ?? new MonthlyActual(month, 0, 0, 0))
            .ToList();

        var salesHistory = recent.Select(m => new
            {
                month = m.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                label = m.Month.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                value = m.RevenuePhp,
                bookingCount = m.BookingCount,
                passengerCount = m.PassengerCount,
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
            .Select(g => new { label = g.Key, value = g.Sum(b => (long)b.GrossRevenue) })
            .OrderByDescending(x => x.value)
            .ToList();

        var totalRevenue = bookings.Sum(b => (long)b.GrossRevenue);
        var totalBookings = bookings.Count;
        var averageBooking = totalBookings > 0 ? (long)Math.Round((double)totalRevenue / totalBookings) : 0;

        return Ok(new
        {
            totalsScope = "lifetime",
            excludesVoided = true,
            chartWindow = new
            {
                fromMonth = firstMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                throughMonth = anchor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                revenuePhp = recent.Sum(m => m.RevenuePhp),
                bookingCount = recent.Sum(m => m.BookingCount),
                passengerCount = recent.Sum(m => m.PassengerCount),
            },
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
