using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize(Policy = "Reports")]
public class ActivityController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActivityController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Index([FromQuery] string? entityType, [FromQuery] string? entityCode, [FromQuery] int take = 100)
    {
        var query = _db.AuditEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityCode)) query = query.Where(a => a.EntityCode == entityCode);

        var entries = query
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToList()
            .Select(a => new
            {
                at = a.At,
                actor = a.Actor,
                actorName = a.ActorName,
                action = a.Action,
                entityType = a.EntityType,
                entityCode = a.EntityCode,
                summary = a.Summary,
            });

        return Ok(entries);
    }
}
