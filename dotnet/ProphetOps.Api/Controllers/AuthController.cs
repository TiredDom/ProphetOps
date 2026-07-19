using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SignInThrottle _throttle;

    public AuthController(AppDbContext db, SignInThrottle throttle)
    {
        _db = db;
        _throttle = throttle;
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

        var user = _db.Users.SingleOrDefault(u => u.Email == email && u.Status == "Active");

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

        _throttle.RecordSuccess(email, address);

        user.LastLoginAt = DateTime.UtcNow;
        _db.SaveChanges();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Ok(new AuthUser(user.Name, user.Email, user.Role, Roles.DefaultPathForRole(user.Role)));
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
