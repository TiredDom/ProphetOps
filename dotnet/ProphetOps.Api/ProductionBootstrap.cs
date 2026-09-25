using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api;

public static class ProductionBootstrap
{
    public sealed record StartupOwnerProvisioningResult(bool Created, bool ExistingAccountPresent);

    public static void CreateOwner(AppDbContext db, string? name, string? email, string? password)
    {
        var normalizedName = (name ?? "").Trim();
        var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
        if (normalizedName.Length == 0)
            throw new InvalidOperationException("Set the owner's name before running owner setup.");
        if (!MailAddress.TryCreate(normalizedEmail, out var address) || address.Address != normalizedEmail)
            throw new InvalidOperationException("Set a valid owner email before running owner setup.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new InvalidOperationException("Use an owner password of at least 12 characters and at most 72 UTF-8 bytes.");

        using var transaction = db.Database.BeginTransaction();
        if (db.Users.Any())
            throw new InvalidOperationException("An account already exists. Owner setup cannot be repeated.");

        db.Users.Add(new User
        {
            Name = normalizedName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Roles.OwnerManagement,
            Status = "Active",
        });
        db.SaveChanges();
        transaction.Commit();
    }

    public static StartupOwnerProvisioningResult CreateOwnerIfEmpty(AppDbContext db, string? name, string? email, string? password)
    {
        if (db.Users.Any())
            return new StartupOwnerProvisioningResult(false, true);

        CreateOwner(db, name, email, password);
        return new StartupOwnerProvisioningResult(true, false);
    }
}
