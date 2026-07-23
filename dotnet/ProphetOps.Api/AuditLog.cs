using System.Security.Claims;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api;

/// Records who did what to which record.
///
/// Three roles share this system and every one of them can edit money. Without a trail, a
/// figure that turns out to be wrong has no history and no author, and the only way to ask
/// what happened is to ask people to remember.
public static class AuditLog
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Voided = "Voided";
    public const string Restored = "Restored";
    public const string Imported = "Imported";

    public static void Record(
        AppDbContext db,
        ClaimsPrincipal user,
        string action,
        string entityType,
        string entityCode,
        string? summary = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            At = DateTime.UtcNow,
            Actor = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? "unknown",
            ActorName = user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name ?? "Unknown",
            Action = action,
            EntityType = entityType,
            EntityCode = entityCode,
            Summary = summary,
        });
    }

    /// Names only the fields that actually moved, so the trail reads as a list of changes
    /// rather than a copy of the record at every save.
    public static string? Difference(params (string Field, object? Before, object? After)[] fields)
    {
        var moved = fields
            .Where(f => !Equals(f.Before?.ToString(), f.After?.ToString()))
            .Select(f => $"{f.Field} {Show(f.Before)} → {Show(f.After)}")
            .ToList();

        return moved.Count == 0 ? null : string.Join("; ", moved);
    }

    private static string Show(object? value) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s)) ? "—" : value.ToString()!;
}
