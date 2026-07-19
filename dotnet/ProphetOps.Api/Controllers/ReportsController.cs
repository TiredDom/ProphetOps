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

    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        var bookings = _db.Bookings.Where(b => b.VoidedAt == null).ToList();
        var expenses = _db.Expenses.Where(e => e.VoidedAt == null).ToList();

        var revenue = bookings.Sum(b => b.GrossRevenue);
        var costs = expenses.Sum(e => e.Amount);

        var bookingsByStatus = bookings
            .GroupBy(b => b.BookingStatus)
            .Select(g => new { label = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToList();

        var expensesByCategory = expenses
            .GroupBy(e => e.Category)
            .Select(g => new { label = g.Key, value = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.value)
            .ToList();

        var revenueByPackage = bookings
            .GroupBy(b => b.PackageName)
            .Select(g => new { label = g.Key, value = g.Sum(b => b.GrossRevenue) })
            .OrderByDescending(x => x.value)
            .ToList();

        return Ok(new
        {
            revenue,
            costs,
            profit = revenue - costs,
            counts = new
            {
                bookings = bookings.Count,
                packages = _db.TravelPackages.Count(),
                expenses = expenses.Count,
                users = _db.Users.Count(),
            },
            bookingsByStatus,
            expensesByCategory,
            revenueByPackage,
            generatedAt = DateTime.UtcNow.ToString("MMM d, yyyy"),
        });
    }
}
