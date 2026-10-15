using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class StaffCookieEvents(AppDbContext db) : CookieAuthenticationEvents
{
    public const string SessionVersionClaim = "session_version";
    public const string SecurityStampClaim = "security_stamp";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var hasId = int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out var id);
        var hasVersion = int.TryParse(principal?.FindFirstValue(SessionVersionClaim), NumberStyles.None,
            CultureInfo.InvariantCulture, out var version);
        var stampClaim = principal?.FindFirstValue(SecurityStampClaim);
        var hasStamp = Guid.TryParse(stampClaim, out var claimStamp) && claimStamp != Guid.Empty;

        var user = hasId && hasVersion && hasStamp
            ? await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted)
            : null;

        if (user is not null
            && user.Status == "Active"
            && user.SessionVersion == version
            && user.SecurityStamp != Guid.Empty
            && user.SecurityStamp == claimStamp
            && user.Role == principal?.FindFirstValue(ClaimTypes.Role)
            && user.Email == principal?.FindFirstValue(ClaimTypes.Email))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
