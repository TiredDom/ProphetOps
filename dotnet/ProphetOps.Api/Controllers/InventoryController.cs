using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Policy = "Package Catalog")]
[ServiceFilter(typeof(MutationTransaction))]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly StoragePaths _storage;
    private readonly IBusinessClock _clock;
    private readonly MutationTransaction _transaction;

    public InventoryController(AppDbContext db, StoragePaths storage, IBusinessClock clock, MutationTransaction transaction)
    {
        _db = db;
        _storage = storage;
        _clock = clock;
        _transaction = transaction;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var packages = _db.TravelPackages
            .OrderBy(p => p.Code)
            .ToList()
            .Select(Dto);

        return Ok(packages);
    }

    [HttpPost]
    public IActionResult Store([FromBody] PackageRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);
        if (_db.TravelPackages.Any(p => p.Code == request.Id))
            return Conflict(new { code = "duplicate_code", message = "Package code already exists. Check the saved package before trying again." });

        var package = new TravelPackage();
        Apply(package, request);
        _db.TravelPackages.Add(package);
        AuditLog.Record(_db, User, AuditLog.Created, "TravelPackage", package.Code,
            $"{package.PackageName}, {package.AvailableSlots:N0} slots, P{package.BasePrice:N0}");
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    [HttpPut("{code}")]
    public IActionResult Update(string code, [FromBody] PackageRequest request)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package is null) return NotFound();
        if (request.Revision != package.Revision) return MutationTransaction.Stale();
        if (request.Id != code) return BadRequest(new { id = "Package code cannot be changed." });

        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);

        var before = (package.PackageName, package.Destination, package.Duration, package.BasePrice,
            package.Inclusions, package.AvailableSlots, package.SoldCount, package.ReservedCount, package.Status);
        Apply(package, request);
        var changed = AuditLog.Difference(
            ("Name", before.PackageName, package.PackageName),
            ("Destination", before.Destination, package.Destination),
            ("Duration", before.Duration, package.Duration),
            ("Price", before.BasePrice, package.BasePrice),
            ("Inclusions", before.Inclusions, package.Inclusions),
            ("Available", before.AvailableSlots, package.AvailableSlots),
            ("Sold", before.SoldCount, package.SoldCount),
            ("Reserved", before.ReservedCount, package.ReservedCount),
            ("Status", before.Status, package.Status));
        if (changed is not null)
            AuditLog.Record(_db, User, AuditLog.Updated, "TravelPackage", package.Code, changed);
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    [HttpGet("{code}/image")]
    public IActionResult GetImage(string code)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package?.ImagePath is null) return NotFound();

        var stored = Path.GetFileName(package.ImagePath);
        var contentType = ImageUpload.ContentTypeFor(stored);
        if (contentType is null) return NotFound();

        var path = Path.Combine(ImageFolder(), stored);
        if (!System.IO.File.Exists(path)) return NotFound();

        return PhysicalFile(path, contentType);
    }

    [HttpPost("{code}/image")]
    [RequestSizeLimit(ImageUpload.MaxBytes)]
    public async Task<IActionResult> UploadImage(string code, IFormFile? file, [FromForm] int? revision)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package is null) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest(new Dictionary<string, string> { ["image"] = "Choose an image to upload." });

        if (file.Length > ImageUpload.MaxBytes)
            return BadRequest(new Dictionary<string, string> { ["image"] = "Image must be 4 MB or smaller." });
        if (revision != package.Revision) return MutationTransaction.Stale();

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        var extension = ImageUpload.SniffExtension(buffer);
        if (extension is null)
            return BadRequest(new Dictionary<string, string> { ["image"] = "Upload a JPEG, PNG, or WebP image." });

        var folder = ImageFolder();
        Directory.CreateDirectory(folder);

        var stored = ImageUpload.NewStoredName(extension);
        _transaction.AfterRollback(() => DiscardStored(stored));
        buffer.Position = 0;
        await using (var target = System.IO.File.Create(Path.Combine(folder, stored)))
        {
            await buffer.CopyToAsync(target);
        }

        var previous = package.ImagePath;
        _transaction.AfterCommit(() => QuarantineStored(previous));
        package.ImagePath = stored;
        package.LastUpdatedAt = _clock.Today;
        AuditLog.Record(_db, User, AuditLog.Updated, "TravelPackage", package.Code,
            previous is null ? "Photo uploaded" : "Photo replaced");
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    [HttpDelete("{code}/image")]
    public IActionResult DeleteImage(string code, [FromBody] RevisionRequest request)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package is null) return NotFound();
        if (request.Revision != package.Revision) return MutationTransaction.Stale();

        var previous = package.ImagePath;
        _transaction.AfterCommit(() => QuarantineStored(previous));
        package.ImagePath = null;
        package.LastUpdatedAt = _clock.Today;
        if (previous is not null)
            AuditLog.Record(_db, User, AuditLog.Updated, "TravelPackage", package.Code, "Photo removed");
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    private string ImageFolder() => _storage.PackageImagesPath;

    private void DiscardStored(string? storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)) return;

        var path = Path.Combine(ImageFolder(), Path.GetFileName(storedName));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private void QuarantineStored(string? storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)) return;

        var source = Path.Combine(ImageFolder(), Path.GetFileName(storedName));
        if (!System.IO.File.Exists(source)) return;
        var quarantine = Path.Combine(ImageFolder(), ".quarantine");
        Directory.CreateDirectory(quarantine);
        var target = Path.Combine(quarantine, Path.GetFileName(storedName));
        if (System.IO.File.Exists(target)) return;
        System.IO.File.Move(source, target);
    }

    private void Apply(TravelPackage package, PackageRequest request)
    {
        package.Code = request.Id ?? package.Code;
        package.PackageName = request.PackageName ?? "";
        package.Destination = request.Destination ?? "";
        package.Duration = request.Duration;
        package.BasePrice = request.BasePrice;
        package.Inclusions = request.Inclusions;
        package.AvailableSlots = request.AvailableSlots;
        package.SoldCount = request.SoldCount;
        package.ReservedCount = request.ReservedCount;
        package.Status = request.Status ?? "Normal";
        package.LastUpdatedAt = _clock.Today;
    }

    private static Dictionary<string, string> Validate(PackageRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.Id)) errors["id"] = "Enter a package code.";
        if (string.IsNullOrWhiteSpace(request.PackageName))
            errors["packageName"] = "Enter the package name.";
        if (string.IsNullOrWhiteSpace(request.Destination))
            errors["destination"] = "Enter the destination.";
        if (request.BasePrice < 0)
            errors["basePrice"] = "Base price must be zero or more.";
        if (request.AvailableSlots < 0)
            errors["availableSlots"] = "Available slots must be zero or more.";
        if (request.SoldCount < 0) errors["soldCount"] = "Sold count must be zero or more.";
        if (request.ReservedCount < 0) errors["reservedCount"] = "Reserved count must be zero or more.";
        return errors;
    }

    private static object Dto(TravelPackage p)
    {
        string? imageUrl = p.ImagePath is null
            ? null
            : $"/api/inventory/{Uri.EscapeDataString(p.Code)}/image?v={p.ImagePath}";

        return new
        {
            id = p.Code,
            backendId = p.Id,
            revision = p.Revision,
            packageName = p.PackageName,
            destination = p.Destination,
            duration = p.Duration,
            basePrice = p.BasePrice,
            inclusions = p.Inclusions,
            availableSlots = p.AvailableSlots,
            soldCount = p.SoldCount,
            reservedCount = p.ReservedCount,
            status = p.Status,
            imageUrl,
        };
    }
}

public record PackageRequest(
    string? Id,
    string? PackageName,
    string? Destination,
    string? Duration,
    int BasePrice,
    string? Inclusions,
    int AvailableSlots,
    int SoldCount,
    int ReservedCount,
    string? Status,
    int? Revision = null);
