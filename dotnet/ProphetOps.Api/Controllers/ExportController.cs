using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;

namespace ProphetOps.Api.Controllers;

/// Hands the records back as CSV files. Bookings and packages come back in the shape the import
/// endpoints read, so those exports are complete copies rather than views: voided bookings ride
/// along, marked. Expenses have no importer and can be limited to a period.
[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBusinessClock _clock;

    public ExportController(AppDbContext db, IBusinessClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet("bookings.csv")]
    [Authorize(Policy = "Bookings")]
    public IActionResult Bookings()
    {
        var rows = _db.Bookings
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.Id)
            .ToList()
            .Select(b => new[]
            {
                b.Code,
                b.BookingDate.ToString("yyyy-MM-dd"),
                b.Client,
                b.PackageName,
                b.Destination,
                Whole(b.PassengerCount),
                Whole(b.GrossRevenue),
                b.PaymentStatus,
                b.BookingStatus,
                b.StaffAssigned ?? "",
                b.Notes ?? "",
                b.Source,
                b.IsVoided ? "yes" : "",
                b.VoidReason ?? "",
            });

        return Download("bookings",
            "code,date,client,package,destination,passengers,revenue,payment,status,staff,notes,source,voided,void_reason",
            rows);
    }

    [HttpGet("packages.csv")]
    [Authorize(Policy = "Package Catalog")]
    public IActionResult Packages()
    {
        var rows = _db.TravelPackages
            .OrderBy(p => p.Code)
            .ToList()
            .Select(p => new[]
            {
                p.Code,
                p.PackageName,
                p.Destination,
                p.Duration ?? "",
                Whole(p.BasePrice),
                p.Inclusions ?? "",
                Whole(p.AvailableSlots),
                Whole(p.SoldCount),
                Whole(p.ReservedCount),
                p.Status,
            });

        return Download("packages",
            "code,package,destination,duration,price,inclusions,slots,sold,reserved,status",
            rows);
    }

    /// Expenses go to the accountant a period at a time, so this one can be limited to a range
    /// of dates (inclusive, either end optional). Voided expenses ride along, marked, as they do
    /// in the bookings export.
    [HttpGet("expenses.csv")]
    [Authorize(Policy = "Expenses")]
    public IActionResult Expenses([FromQuery] string? from, [FromQuery] string? to)
    {
        var errors = new Dictionary<string, string>();
        var start = ReadDate(from, "from", errors);
        var end = ReadDate(to, "to", errors);
        if (start is not null && end is not null && start > end)
            errors["to"] = "The end date is before the start date.";
        if (errors.Count > 0) return BadRequest(errors);

        var rows = _db.Expenses
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.Id)
            .ToList()
            .Where(e => (start is null || e.ExpenseDate >= start) && (end is null || e.ExpenseDate <= end))
            .Select(e => new[]
            {
                e.Code,
                e.ExpenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.Category,
                Whole(e.Amount),
                e.RelatedPackage,
                e.PaymentStatus,
                e.Notes ?? "",
                e.IsVoided ? "yes" : "",
                e.VoidReason ?? "",
            });

        var range = start is null && end is null
            ? null
            : $"prophetops-expenses-{start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "start"}"
              + $"-to-{end?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "latest"}.csv";

        return Download("expenses",
            "code,date,category,amount,related_package,payment,notes,voided,void_reason",
            rows,
            range);
    }

    /// The Reports page as a spreadsheet: one row per month of the period and a total, the same
    /// figures the page shows. Revenue after costs is gross booking revenue less recorded expenses.
    [HttpGet("report.csv")]
    [Authorize(Policy = "Reports")]
    public IActionResult Report([FromQuery] string? from, [FromQuery] string? to)
    {
        if (!ReportBuilder.TryReadPeriod(from, to, out var start, out var end, out var errors))
            return BadRequest(errors);

        var report = ReportBuilder.Build(_db, _clock.Today, start, end);
        var rows = report.Months
            .Select(m => new[]
            {
                m.Start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Whole(m.Bookings),
                Whole(m.Passengers),
                Whole(m.Revenue),
                Whole(m.Costs),
                Whole(m.AfterCosts),
            })
            .Append(new[]
            {
                "total",
                Whole(report.Current.Bookings),
                Whole(report.Current.Passengers),
                Whole(report.Current.Revenue),
                Whole(report.Current.Costs),
                Whole(report.Current.AfterCosts),
            });

        var name = report.From is null && report.Through is null
            ? null
            : $"prophetops-report-{report.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "start"}"
              + $"-to-{report.Through?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "latest"}.csv";

        return Download("report",
            "month,bookings,passengers,revenue,costs,revenue_after_costs",
            rows,
            name);
    }

    private static DateOnly? ReadDate(string? value, string field, Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors[field] = "Use a date written as yyyy-mm-dd.";
        return null;
    }

    private FileContentResult Download(string subject, string header, IEnumerable<string[]> rows, string? fileName = null)
    {
        var text = new StringBuilder(header).Append("\r\n");
        foreach (var row in rows)
            text.Append(string.Join(",", row.Select(Field))).Append("\r\n");

        // The byte order mark is for Excel, which otherwise reads peso signs and accented
        // client names as mojibake.
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(text.ToString()))
            .ToArray();

        return File(bytes, "text/csv; charset=utf-8",
            fileName ?? $"prophetops-{subject}-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private static readonly char[] Reserved = { ',', '"', '\r', '\n' };

    private static readonly System.Text.RegularExpressions.Regex PlainNumber = new(@"^-?\d+$");

    /// The apostrophe is how spreadsheets themselves write a value that must not be evaluated,
    /// so it survives a round trip through them and through our own importer.
    private static string Field(string value)
    {
        // A plain whole number, such as a month that lost money, is data rather than a formula.
        if (PlainNumber.IsMatch(value)) return value;

        var guarded = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t'
            ? "'" + value
            : value;

        return guarded.IndexOfAny(Reserved) >= 0
            ? "\"" + guarded.Replace("\"", "\"\"") + "\""
            : guarded;
    }

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);
}
