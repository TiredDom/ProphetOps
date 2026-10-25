using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "Reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBusinessClock _clock;

    public ReportsController(AppDbContext db, IBusinessClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// Totals for a period (inclusive yyyy-MM-dd bounds, either end optional), or for all records
    /// when no period is given. Voided bookings and expenses are always left out.
    [HttpGet]
    public IActionResult Get([FromQuery] string? from, [FromQuery] string? to)
    {
        if (!ReportBuilder.TryReadPeriod(from, to, out var start, out var end, out var errors))
            return BadRequest(errors);

        var report = ReportBuilder.Build(_db, _clock.Today, start, end);
        var current = report.Current;

        return Ok(new
        {
            totalsScope = start is null && end is null ? "lifetime" : "period",
            excludesVoided = true,
            period = new
            {
                from = Day(report.From),
                to = Day(report.To),
                through = Day(report.Through),
            },
            revenue = current.Revenue,
            costs = current.Costs,
            profit = current.AfterCosts,
            counts = new
            {
                bookings = current.Bookings,
                passengers = current.Passengers,
                packages = _db.TravelPackages.Count(),
                expenses = current.Expenses,
                users = _db.Users.Count(),
            },
            previous = report.Previous is { } previous
                ? new
                {
                    from = Day(previous.From),
                    to = Day(previous.To),
                    revenue = previous.Revenue,
                    costs = previous.Costs,
                    profit = previous.AfterCosts,
                    bookings = previous.Bookings,
                    passengers = previous.Passengers,
                }
                : null,
            monthly = report.Months.Select(m => new
            {
                month = m.Start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                label = m.Start.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                revenue = m.Revenue,
                costs = m.Costs,
                afterCosts = m.AfterCosts,
                bookings = m.Bookings,
                passengers = m.Passengers,
            }),
            bookingsByStatus = report.BookingsByStatus.Select(l => new { label = l.Label, value = l.Count }),
            bookingsByPayment = report.BookingsByPayment.Select(l => new { label = l.Label, value = l.Count, revenue = l.Value }),
            expensesByCategory = report.ExpensesByCategory.Select(l => new { label = l.Label, value = l.Value, count = l.Count }),
            revenueByPackage = report.RevenueByPackage.Select(l => new { label = l.Label, value = l.Value, count = l.Count }),
            revenueByDestination = report.RevenueByDestination.Select(l => new { label = l.Label, value = l.Value, count = l.Count }),
            generatedAt = _clock.Today.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
        });
    }

    private static string? Day(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
