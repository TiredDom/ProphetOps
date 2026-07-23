using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize(Policy = "Bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BookingsController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Index()
    {
        var bookings = _db.Bookings
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.Id)
            .ToList()
            .Select(Dto);

        var packages = _db.TravelPackages
            .OrderBy(p => p.Code)
            .ToList()
            .Select(p => new
            {
                id = p.Code,
                backendId = p.Id,
                packageName = p.PackageName,
                destination = p.Destination,
                duration = p.Duration,
                basePrice = p.BasePrice,
                inclusions = p.Inclusions,
                availableSlots = p.AvailableSlots,
                soldCount = p.SoldCount,
                reservedCount = p.ReservedCount,
                status = p.Status,
            });

        return Ok(new { bookings, packages });
    }

    [HttpPost]
    public IActionResult Store([FromBody] BookingRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);
        if (_db.Bookings.Any(b => b.Code == request.Id))
            return BadRequest(new Dictionary<string, string> { ["id"] = "Booking ID already exists." });

        var stock = ValidateAvailability(request, null);
        if (stock.Count > 0) return BadRequest(stock);

        var unusual = UnusualRevenue(request, null);
        if (unusual is not null) return Conflict(new { message = unusual });

        var booking = new Booking();
        Apply(booking, request);
        _db.Bookings.Add(booking);

        ReserveSlots(booking.TravelPackageId, booking.PassengerCount);

        AuditLog.Record(_db, User, AuditLog.Created, "Booking", booking.Code,
            $"{booking.Client}, {booking.PassengerCount} pax, P{booking.GrossRevenue:N0}");
        _db.SaveChanges();

        return Ok(Dto(booking));
    }

    [HttpPut("{code}")]
    public IActionResult Update(string code, [FromBody] BookingRequest request)
    {
        var booking = _db.Bookings.SingleOrDefault(b => b.Code == code);
        if (booking is null) return NotFound();

        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);

        var stock = ValidateAvailability(request, booking);
        if (stock.Count > 0) return BadRequest(stock);

        var unusual = UnusualRevenue(request, booking);
        if (unusual is not null) return Conflict(new { message = unusual });

        var previousPackageId = booking.TravelPackageId;
        var previousPassengers = booking.PassengerCount;
        var before = (booking.Client, booking.PassengerCount, booking.GrossRevenue,
            booking.PaymentStatus, booking.BookingStatus, booking.Destination);

        Apply(booking, request);

        ReleaseSlots(previousPackageId, previousPassengers);
        ReserveSlots(booking.TravelPackageId, booking.PassengerCount);

        var changed = AuditLog.Difference(
            ("Client", before.Client, booking.Client),
            ("Passengers", before.PassengerCount, booking.PassengerCount),
            ("Revenue", before.GrossRevenue, booking.GrossRevenue),
            ("Payment", before.PaymentStatus, booking.PaymentStatus),
            ("Status", before.BookingStatus, booking.BookingStatus),
            ("Destination", before.Destination, booking.Destination));
        if (changed is not null)
            AuditLog.Record(_db, User, AuditLog.Updated, "Booking", booking.Code, changed);

        _db.SaveChanges();

        return Ok(Dto(booking));
    }

    [HttpPost("{code}/void")]
    public IActionResult Void(string code, [FromBody] VoidRequest request)
    {
        var booking = _db.Bookings.SingleOrDefault(b => b.Code == code);
        if (booking is null) return NotFound();
        if (booking.IsVoided) return BadRequest(new Dictionary<string, string> { ["reason"] = "This booking is already voided." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new Dictionary<string, string> { ["reason"] = "Say why this is being voided." });

        booking.VoidedAt = DateTime.UtcNow;
        booking.VoidedBy = User.FindFirst(ClaimTypes.Email)?.Value;
        booking.VoidReason = request.Reason.Trim();

        // The seats go back. A voided booking is not holding anything.
        ReleaseSlots(booking.TravelPackageId, booking.PassengerCount);

        AuditLog.Record(_db, User, AuditLog.Voided, "Booking", booking.Code, booking.VoidReason);
        _db.SaveChanges();

        return Ok(Dto(booking));
    }

    [HttpPost("{code}/restore")]
    public IActionResult Restore(string code)
    {
        var booking = _db.Bookings.SingleOrDefault(b => b.Code == code);
        if (booking is null) return NotFound();
        if (!booking.IsVoided) return BadRequest(new Dictionary<string, string> { ["reason"] = "This booking is not voided." });

        var package = booking.TravelPackageId is int id ? _db.TravelPackages.Find(id) : null;
        if (package is not null && package.AvailableSlots < booking.PassengerCount)
        {
            return BadRequest(new Dictionary<string, string>
            {
                ["reason"] = $"Only {package.AvailableSlots} slots are free, and this booking needs {booking.PassengerCount}.",
            });
        }

        booking.VoidedAt = null;
        booking.VoidedBy = null;
        booking.VoidReason = null;
        ReserveSlots(booking.TravelPackageId, booking.PassengerCount);

        AuditLog.Record(_db, User, AuditLog.Restored, "Booking", booking.Code);
        _db.SaveChanges();

        return Ok(Dto(booking));
    }

    private Dictionary<string, string> ValidateAvailability(BookingRequest request, Booking? existing)
    {
        var errors = new Dictionary<string, string>();
        if (request.PackageId is null) return errors;

        var package = _db.TravelPackages.FirstOrDefault(p => p.Code == request.PackageId);
        if (package is null) return errors;

        var alreadyHeld = existing is not null && existing.TravelPackageId == package.Id
            ? existing.PassengerCount
            : 0;
        var capacity = package.AvailableSlots + alreadyHeld;

        if (request.Y > capacity)
            errors["y"] = capacity == 1
                ? "Only 1 slot is left for this package."
                : $"Only {capacity} slots are left for this package.";

        return errors;
    }

    private void ReserveSlots(int? packageId, int passengers)
    {
        if (packageId is not int id) return;
        var package = _db.TravelPackages.Find(id);
        if (package is null) return;

        package.AvailableSlots = Math.Max(0, package.AvailableSlots - passengers);
        package.SoldCount += passengers;
        package.LastUpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private void ReleaseSlots(int? packageId, int passengers)
    {
        if (packageId is not int id) return;
        var package = _db.TravelPackages.Find(id);
        if (package is null) return;

        package.AvailableSlots += passengers;
        package.SoldCount = Math.Max(0, package.SoldCount - passengers);
        package.LastUpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    [HttpPost("bulk")]
    public IActionResult Bulk([FromBody] BulkRequest request)
    {
        if (request.Ids is null || request.Ids.Length == 0 || (request.Action != "confirm" && request.Action != "paid"))
            return BadRequest();

        var bookings = _db.Bookings.Where(b => request.Ids.Contains(b.Code)).ToList();
        foreach (var booking in bookings)
        {
            if (request.Action == "confirm") booking.BookingStatus = "Confirmed";
            else booking.PaymentStatus = "Paid";
        }
        _db.SaveChanges();

        return Ok(new { updated = bookings.Count });
    }

    private void Apply(Booking booking, BookingRequest request)
    {
        var package = request.PackageId is not null
            ? _db.TravelPackages.FirstOrDefault(p => p.Code == request.PackageId)
            : null;

        booking.Code = request.Id ?? booking.Code;
        booking.BookingDate = DateOnly.Parse(request.Ds!);
        booking.PassengerCount = request.Y;
        booking.Client = request.Client ?? "";
        booking.TravelPackageId = package?.Id;
        booking.PackageName = request.Package ?? "";
        booking.PackageCode = request.PackageId;
        booking.EntryType = request.EntryType ?? "Package preset";
        booking.Destination = request.Destination ?? "";
        booking.GrossRevenue = request.GrossRevenue;
        booking.PaymentStatus = request.PaymentStatus ?? "Pending";
        booking.BookingStatus = request.BookingStatus ?? "Pending";
        booking.StaffAssigned = request.StaffAssigned;
        booking.Source = request.Source ?? "Manual quotation";
        booking.Notes = request.Notes;
    }

    private static Dictionary<string, string> Validate(BookingRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.Ds) || !DateOnly.TryParse(request.Ds, out _))
            errors["ds"] = "Choose the booking date.";
        if (string.IsNullOrWhiteSpace(request.Client))
            errors["client"] = "Enter the client or agency partner.";
        if (string.IsNullOrWhiteSpace(request.Destination))
            errors["destination"] = "Enter the destination.";
        if (string.IsNullOrWhiteSpace(request.Package))
            errors["package"] = "Enter the package name.";
        if (request.Y < 1)
            errors["y"] = "Passenger count must be at least 1.";
        if (request.GrossRevenue < 0)
            errors["grossRevenue"] = "Revenue must be zero or more.";
        return errors;
    }

    private const int UnusualMultiple = 5;
    private const int UnusualSample = 8;

    /// A mistyped figure passes every other check: it is a positive number in the right field.
    /// It also survives longest, because the forecaster reads a spike as a seasonal signal and
    /// carries it for a year. So an entry far above the usual is queried once before it is
    /// saved, and accepted the moment the person says they meant it.
    private string? UnusualRevenue(BookingRequest request, Booking? existing)
    {
        if (request.ConfirmUnusual) return null;
        if (existing is not null && existing.GrossRevenue == request.GrossRevenue) return null;

        var others = _db.Bookings
            .Where(b => existing == null || b.Id != existing.Id)
            .Select(b => b.GrossRevenue)
            .ToList();

        if (others.Count < UnusualSample) return null;

        others.Sort();
        var middle = others.Count / 2;
        var median = others.Count % 2 == 1
            ? others[middle]
            : (others[middle - 1] + others[middle]) / 2;

        if (median <= 0 || request.GrossRevenue < median * UnusualMultiple) return null;

        return $"₱{request.GrossRevenue:N0} is more than {UnusualMultiple} times the usual booking "
            + $"(about ₱{median:N0}). Check for a stray digit, or save again to confirm the amount is right.";
    }

    private static object Dto(Booking b) => new
    {
        id = b.Code,
        backendId = b.Id,
        ds = b.BookingDate.ToString("yyyy-MM-dd"),
        y = b.PassengerCount,
        client = b.Client,
        package = b.PackageName,
        packageId = b.PackageCode,
        entryType = b.EntryType,
        destination = b.Destination,
        grossRevenue = b.GrossRevenue,
        paymentStatus = b.PaymentStatus,
        bookingStatus = b.BookingStatus,
        staffAssigned = b.StaffAssigned,
        source = b.Source,
        notes = b.Notes,
        voided = b.VoidedAt != null,
        voidReason = b.VoidReason,
    };
}
