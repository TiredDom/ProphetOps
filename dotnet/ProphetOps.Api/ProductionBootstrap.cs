using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api;

public static class ProductionBootstrap
{
    public sealed record StartupOwnerProvisioningResult(bool Created, bool ExistingAccountPresent);

    public static void CreateOwner(AppDbContext db, string? name, string? email, string? password) =>
        CreateOwnerAsync(db, DatabaseProviderKind.Sqlite, name, email, password, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task CreateOwnerAsync(
        AppDbContext db,
        DatabaseProviderKind provider,
        string? name,
        string? email,
        string? password,
        CancellationToken cancellationToken)
    {
        var normalizedName = (name ?? "").Trim();
        var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
        if (normalizedName.Length == 0)
            throw new InvalidOperationException("Set the owner's name before running owner setup.");
        if (!MailAddress.TryCreate(normalizedEmail, out var address) || address.Address != normalizedEmail)
            throw new InvalidOperationException("Set a valid owner email before running owner setup.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new InvalidOperationException("Use an owner password of at least 12 characters and at most 72 UTF-8 bytes.");

        await using var scope = await DatabaseWriteScope.BeginAsync(db, provider, cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
            throw new InvalidOperationException("An account already exists. Owner setup cannot be repeated.");

        db.Users.Add(new User
        {
            Name = normalizedName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.OwnerManagement,
            Status = "Active",
        });
        await db.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public static StartupOwnerProvisioningResult CreateOwnerIfEmpty(AppDbContext db, string? name, string? email, string? password) =>
        CreateOwnerIfEmptyAsync(db, DatabaseProviderKind.Sqlite, name, email, password, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task<StartupOwnerProvisioningResult> CreateOwnerIfEmptyAsync(
        AppDbContext db,
        DatabaseProviderKind provider,
        string? name,
        string? email,
        string? password,
        CancellationToken cancellationToken)
    {
        await using var scope = await DatabaseWriteScope.BeginAsync(db, provider, cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
            return new StartupOwnerProvisioningResult(false, true);

        var normalizedName = (name ?? "").Trim();
        var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
        if (normalizedName.Length == 0)
            throw new InvalidOperationException("Set the owner's name before running owner setup.");
        if (!MailAddress.TryCreate(normalizedEmail, out var address) || address.Address != normalizedEmail)
            throw new InvalidOperationException("Set a valid owner email before running owner setup.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new InvalidOperationException("Use an owner password of at least 12 characters and at most 72 UTF-8 bytes.");

        db.Users.Add(new User
        {
            Name = normalizedName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.OwnerManagement,
            Status = "Active",
        });
        await db.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return new StartupOwnerProvisioningResult(true, false);
    }
}
