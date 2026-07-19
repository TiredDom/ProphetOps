using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Policy = "Bookings")]
public class ImportController : ControllerBase
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly string[] AcceptedTypes = { "text/csv", "application/vnd.ms-excel", "text/plain" };

    private readonly AppDbContext _db;

    public ImportController(AppDbContext db) => _db = db;

    [HttpPost("bookings/preview")]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Preview(IFormFile? file)
    {
        var (content, failure) = await Read(file);
        if (failure is not null) return failure;

        var result = BookingCsv.Parse(content);
        var existing = ExistingCodes();
        var duplicates = result.Rows
            .Where(r => r.Code is not null && existing.Contains(r.Code))
            .Select(r => r.Code!)
            .ToList();

        var dates = result.Rows.Select(r => r.Date).ToList();

        return Ok(new
        {
            valid = result.Rows.Count,
            skipped = result.Problems.Count,
            duplicates = duplicates.Count,
            duplicateCodes = duplicates.Take(20),
            from = dates.Count == 0 ? null : dates.Min().ToString("yyyy-MM-dd"),
            to = dates.Count == 0 ? null : dates.Max().ToString("yyyy-MM-dd"),
            months = dates.Select(d => d.Year * 12 + d.Month).Distinct().Count(),
            passengers = result.Rows.Sum(r => r.Passengers),
            totalRevenue = result.Rows.Sum(r => (long)r.Revenue),
            problems = result.Problems.Select(Note),
            warnings = result.Warnings.Select(Note),
        });
    }

    [HttpPost("bookings/commit")]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Commit(IFormFile? file, [FromForm] string? confirm)
    {
        if (!IsYes(confirm))
            return Bad("confirm", "Preview the file and confirm before it is saved.");

        var (content, failure) = await Read(file);
        if (failure is not null) return failure;

        var result = BookingCsv.Parse(content);
        if (result.Rows.Count == 0)
            return Bad("file", "Nothing in this file could be imported. Preview it to see why.");

        var codes = ExistingCodes();
        var imported = new List<Booking>();
        var alreadyHeld = new List<string>();

        foreach (var row in result.Rows)
        {
            if (row.Code is not null && codes.Contains(row.Code))
            {
                alreadyHeld.Add(row.Code);
                continue;
            }

            var code = row.Code ?? NewCode(codes);
            codes.Add(code);

            // Left unattached to a package on purpose. These are bookings that already
            // happened, and linking them would take slots off inventory that is on sale now.
            var booking = new Booking
            {
                Code = code,
                BookingDate = row.Date,
                PassengerCount = row.Passengers,
                Client = row.Client,
                TravelPackageId = null,
                PackageName = row.Package,
                PackageCode = null,
                EntryType = "Custom quotation",
                Destination = row.Destination,
                GrossRevenue = row.Revenue,
                PaymentStatus = row.PaymentStatus,
                BookingStatus = row.BookingStatus,
                StaffAssigned = row.Staff,
                Source = "Imported",
                Notes = row.Notes,
            };

            _db.Bookings.Add(booking);
            imported.Add(booking);
        }

        var batch = $"IMP-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        AuditLog.Record(_db, User, AuditLog.Imported, "Booking", batch,
            Summary(imported, result.Problems.Count, alreadyHeld.Count, file!.FileName));
        _db.SaveChanges();

        return Ok(new
        {
            batch,
            imported = imported.Count,
            skipped = result.Problems.Count + alreadyHeld.Count,
            duplicates = alreadyHeld.Count,
            duplicateCodes = alreadyHeld.Take(20),
            from = imported.Count == 0 ? null : imported.Min(b => b.BookingDate).ToString("yyyy-MM-dd"),
            to = imported.Count == 0 ? null : imported.Max(b => b.BookingDate).ToString("yyyy-MM-dd"),
            totalRevenue = imported.Sum(b => (long)b.GrossRevenue),
            problems = result.Problems.Select(Note),
            warnings = result.Warnings.Select(Note),
        });
    }

    private HashSet<string> ExistingCodes() =>
        _db.Bookings.Select(b => b.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NewCode(HashSet<string> taken)
    {
        while (true)
        {
            var code = "IMP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            if (taken.Add(code)) return code;
        }
    }

    private static string Summary(List<Booking> imported, int skipped, int duplicates, string fileName)
    {
        var parts = new List<string>();
        var name = Path.GetFileName(fileName ?? "");
        var source = string.IsNullOrWhiteSpace(name) ? "an uploaded file" : Clean(name);

        parts.Add(imported.Count == 1
            ? $"1 booking imported from {source}"
            : $"{imported.Count} bookings imported from {source}");

        if (imported.Count > 0)
        {
            parts.Add($"{imported.Min(b => b.BookingDate):yyyy-MM-dd} to {imported.Max(b => b.BookingDate):yyyy-MM-dd}");
            parts.Add($"₱{imported.Sum(b => (long)b.GrossRevenue):N0}");
        }

        if (skipped > 0) parts.Add($"{skipped} rows could not be read");
        if (duplicates > 0) parts.Add($"{duplicates} already in the system");

        return string.Join("; ", parts);
    }

    /// The file name is the caller's to choose and it is being written into a record other
    /// people read later, so it is trimmed to something that cannot carry more than a name.
    private static string Clean(string name)
    {
        var kept = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsControl(c)) continue;
            kept.Append(c);
        }

        var text = kept.ToString().Trim();
        return text.Length <= 80 ? text : text[..80];
    }

    private static async Task<(string? Content, IActionResult? Failure)> Read(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, Bad("file", "Choose a CSV file to import."));

        if (file.Length > MaxBytes)
            return (null, Bad("file", "The file must be 2 MB or smaller. Split it and import the parts separately."));

        var type = (file.ContentType ?? "").Split(';')[0].Trim();
        if (!AcceptedTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            return (null, Bad("file", "Export the sheet as CSV and upload that."));

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        if (!LooksLikeText(bytes))
            return (null, Bad("file", "That file is not a CSV. Export the sheet as CSV and upload that."));

        return (Encoding.UTF8.GetString(bytes), null);
    }

    /// A spreadsheet saved as .xlsx or a renamed image will arrive with a plausible name and
    /// content type, because both of those belong to whoever is uploading. What the bytes are
    /// is the only part they cannot dress up.
    private static bool LooksLikeText(byte[] bytes)
    {
        var limit = Math.Min(bytes.Length, 8000);
        for (var i = 0; i < limit; i++)
        {
            var b = bytes[i];
            if (b == 0) return false;
            if (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D) return false;
        }

        return true;
    }

    private static bool IsYes(string? value) =>
        value is not null && (bool.TryParse(value, out var yes) && yes
            || value is "1" or "on"
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static BadRequestObjectResult Bad(string field, string message) =>
        new(new Dictionary<string, string> { [field] = message });

    private static object Note(BookingCsvProblem problem) => new
    {
        line = problem.Line,
        reason = problem.Reason,
    };
}
