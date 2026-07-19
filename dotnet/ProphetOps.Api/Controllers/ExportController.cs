using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;

namespace ProphetOps.Api.Controllers;

/// Hands the records back as CSV files the import endpoints can read again unchanged, so an
/// export is a complete copy rather than a view: voided bookings ride along, marked.
[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExportController(AppDbContext db) => _db = db;

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

    private FileContentResult Download(string subject, string header, IEnumerable<string[]> rows)
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
            $"prophetops-{subject}-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private static readonly char[] Reserved = { ',', '"', '\r', '\n' };

    /// A client named "=HYPERLINK(...)" is data, and stays data when the file is opened in a
    /// spreadsheet. The apostrophe is how spreadsheets themselves write a value that must not
    /// be evaluated, so it survives a round trip through them and through our own importer.
    private static string Field(string value)
    {
        var guarded = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t'
            ? "'" + value
            : value;

        return guarded.IndexOfAny(Reserved) >= 0
            ? "\"" + guarded.Replace("\"", "\"\"") + "\""
            : guarded;
    }

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);
}
