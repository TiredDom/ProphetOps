using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DatabaseRuntimeOptions _database;
    private readonly SignInThrottle _throttle;
    private readonly MaintenanceGate _maintenance;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        DatabaseRuntimeOptions database,
        SignInThrottle throttle,
        MaintenanceGate maintenance,
        ILogger<AuthController>? logger = null)
    {
        _db = db;
        _database = database;
        _throttle = throttle;
        _maintenance = maintenance;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthController>.Instance;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var address = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Checked before the password is verified, so a caller already locked out cannot keep
        // spending the server's time on hashing.
        var wait = _throttle.RetryAfter(email, address);
        if (wait > TimeSpan.Zero) return TooManyAttempts(wait);

        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email && u.Status == "Active", HttpContext.RequestAborted);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password ?? "", user.PasswordHash))
        {
            _throttle.RecordFailure(email, address);

            // The count moves on any failure, real account or invented one, so the switch to a
            // wait says nothing about whether the account exists.
            var next = _throttle.RetryAfter(email, address);
            return next > TimeSpan.Zero
                ? TooManyAttempts(next)
                : Unauthorized(new { message = "Use an authorized internal account." });
        }

        if (!CloudflareAccessLogin.MatchesAccessEmail(HttpContext, user.Email))
            return Unauthorized(new { message = "Use an authorized internal account." });

        var admission = _maintenance.TryEnterMutation();
        if (!admission.Allowed)
        {
            Response.Headers.RetryAfter = MaintenanceGate.RetryAfterSeconds.ToString();
            return MaintenanceGate.RetryableResult();
        }
        using var maintenanceLease = admission.Lease!;

        DatabaseWriteScope writeScope;
        try
        {
            writeScope = await DatabaseWriteScope.BeginAsync(_db, _database.Provider, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (MutationTransaction.IsConflict(exception))
        {
            return MutationTransaction.WriteConflict();
        }
        await using (writeScope)
        {
            var currentUser = await LoginAdmission.LoadFreshAuthorizedUserAsync(
                _db,
                user,
                request.Password ?? "",
                HttpContext.RequestAborted);
            if (currentUser is null)
                return Unauthorized(new { message = "Use an authorized internal account." });

            _throttle.RecordSuccess(email, address);

            var writableUser = await _db.Users.SingleAsync(u => u.Id == currentUser.Id, HttpContext.RequestAborted);
            writableUser.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            await writeScope.CommitAsync(HttpContext.RequestAborted);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, currentUser.Id.ToString()),
                new(ClaimTypes.Name, currentUser.Name),
                new(ClaimTypes.Email, currentUser.Email),
                new(ClaimTypes.Role, currentUser.Role),
                new(StaffCookieEvents.SessionVersionClaim, currentUser.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(StaffCookieEvents.SecurityStampClaim, currentUser.SecurityStamp.ToString("D")),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return Ok(new AuthUser(currentUser.Name, currentUser.Email, currentUser.Role, Roles.DefaultPathForRole(currentUser.Role)));
        }
    }

    /// Says plainly that the wait is a wait and how long is left. A locked-out colleague who is
    /// only told "wrong password" retypes the same correct password until she gives up.
    private IActionResult TooManyAttempts(TimeSpan wait)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString();

        return StatusCode(StatusCodes.Status429TooManyRequests, new
        {
            message = $"Too many sign-in attempts. Try again in {seconds} second{(seconds == 1 ? "" : "s")}.",
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Preserve local cookie clearing across all paths
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            return NoContent();
        }

        var admission = _maintenance.TryEnterMutation();
        if (!admission.Allowed)
        {
            Response.Headers.RetryAfter = MaintenanceGate.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return MaintenanceGate.RetryableResult();
        }
        using var maintenanceLease = admission.Lease!;

        try
        {
            await using var writeScope = await DatabaseWriteScope.BeginAsync(_db, _database.Provider, HttpContext.RequestAborted);

            var versionClaim = User.FindFirstValue(StaffCookieEvents.SessionVersionClaim);
            var stampClaim = User.FindFirstValue(StaffCookieEvents.SecurityStampClaim);
            var hasStamp = Guid.TryParse(stampClaim, out var claimStamp) && claimStamp != Guid.Empty;
            var hasVersion = int.TryParse(versionClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var sessionVersion);

            var freshUser = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, HttpContext.RequestAborted);
            if (freshUser is null)
            {
                return NoContent();
            }

            // Revalidate fresh actor cookie/stamp inside write lock before rotating.
            // A request admitted before another revocation must not rotate newer sessions.
            if (!hasStamp || !hasVersion || freshUser.SecurityStamp != claimStamp || freshUser.SessionVersion != sessionVersion)
            {
                _logger.LogInformation("Logout called with stale credentials for user {UserId}; newer session preserved.", userId);
                return NoContent();
            }

            freshUser.SecurityStamp = Guid.NewGuid();
            freshUser.SessionVersion = freshUser.SessionVersion == int.MaxValue ? 1 : freshUser.SessionVersion + 1;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            await writeScope.CommitAsync(HttpContext.RequestAborted);

            return NoContent();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Logout revocation cancelled for user {UserId}.", userId);
            Response.Headers.RetryAfter = "1";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "revocation_cancelled",
                message = "Session revocation was cancelled. Please retry.",
            });
        }
        catch (Exception exception) when (MutationTransaction.IsConflict(exception))
        {
            _logger.LogWarning("Logout revocation encountered write conflict for user {UserId}.", userId);
            Response.Headers.RetryAfter = "1";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "write_conflict",
                message = "Logout revocation encountered a concurrency conflict. Please retry.",
            });
        }
        catch (Exception)
        {
            _logger.LogWarning("Logout revocation failed due to a database error for user {UserId}.", userId);
            Response.Headers.RetryAfter = MaintenanceGate.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "revocation_failed",
                message = "Session could not be revoked on the server. Try again.",
            });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        return Ok(new AuthUser(
            User.FindFirst(ClaimTypes.Name)?.Value ?? "",
            User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            role,
            Roles.DefaultPathForRole(role)));
    }
}
