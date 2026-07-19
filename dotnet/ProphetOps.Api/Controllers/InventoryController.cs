using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Policy = "Package Catalog")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public InventoryController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
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
            return BadRequest(new Dictionary<string, string> { ["code"] = "Package code already exists." });

        var package = new TravelPackage();
        Apply(package, request);
        _db.TravelPackages.Add(package);
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    [HttpPut("{code}")]
    public IActionResult Update(string code, [FromBody] PackageRequest request)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package is null) return NotFound();

        var errors = Validate(request);
        if (errors.Count > 0) return BadRequest(errors);

        Apply(package, request);
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
    public async Task<IActionResult> UploadImage(string code, IFormFile? file)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package is null) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest(new Dictionary<string, string> { ["image"] = "Choose an image to upload." });

        if (file.Length > ImageUpload.MaxBytes)
            return BadRequest(new Dictionary<string, string> { ["image"] = "Image must be 4 MB or smaller." });

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        var extension = ImageUpload.SniffExtension(buffer);
        if (extension is null)
            return BadRequest(new Dictionary<string, string> { ["image"] = "Upload a JPEG, PNG, or WebP image." });

        var folder = ImageFolder();
        Directory.CreateDirectory(folder);

        var stored = ImageUpload.NewStoredName(extension);
        buffer.Position = 0;
        await using (var target = System.IO.File.Create(Path.Combine(folder, stored)))
        {
            await buffer.CopyToAsync(target);
        }

        DiscardStored(package.ImagePath);
        package.ImagePath = stored;
        package.LastUpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    [HttpDelete("{code}/image")]
    public IActionResult DeleteImage(string code)
    {
        var package = _db.TravelPackages.SingleOrDefault(p => p.Code == code);
        if (package is null) return NotFound();

        DiscardStored(package.ImagePath);
        package.ImagePath = null;
        package.LastUpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);
        _db.SaveChanges();

        return Ok(Dto(package));
    }

    private string ImageFolder() => Path.Combine(_env.ContentRootPath, "uploads", "packages");

    private void DiscardStored(string? storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)) return;

        var path = Path.Combine(ImageFolder(), Path.GetFileName(storedName));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
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
        package.LastUpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static Dictionary<string, string> Validate(PackageRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.PackageName))
            errors["packageName"] = "Enter the package name.";
        if (string.IsNullOrWhiteSpace(request.Destination))
            errors["destination"] = "Enter the destination.";
        if (request.BasePrice < 0)
            errors["basePrice"] = "Base price must be zero or more.";
        if (request.AvailableSlots < 0)
            errors["availableSlots"] = "Available slots must be zero or more.";
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
    string? Status);
