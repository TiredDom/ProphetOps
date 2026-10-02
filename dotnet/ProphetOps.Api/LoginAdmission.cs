using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api;

public static class LoginAdmission
{
    public static async Task<User?> LoadFreshAuthorizedUserAsync(
        AppDbContext db,
        User initiallyVerified,
        string password,
        CancellationToken cancellationToken)
    {
        var fresh = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == initiallyVerified.Id && u.Status == "Active", cancellationToken);
        if (fresh is null)
            return null;
        if (fresh.Email != initiallyVerified.Email
            || fresh.Role != initiallyVerified.Role
            || fresh.SessionVersion != initiallyVerified.SessionVersion
            || fresh.PasswordHash != initiallyVerified.PasswordHash)
            return null;
        return BCrypt.Net.BCrypt.Verify(password, fresh.PasswordHash) ? fresh : null;
    }
}
