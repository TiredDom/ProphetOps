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

    public InventoryController(AppDbContext db) => _db = db;

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

    private static object Dto(TravelPackage p) => new
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
    };
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
