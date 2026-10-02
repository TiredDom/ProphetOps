using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public AuthController(AppDbContext db, DatabaseRuntimeOptions database, SignInThrottle throttle, MaintenanceGate maintenance)
    {
        _db = db;
        _database = database;
        _throttle = throttle;
        _maintenance = maintenance;
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
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
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
