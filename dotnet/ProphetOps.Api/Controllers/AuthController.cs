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

    public AuthController(AppDbContext db) => _db = db;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var user = _db.Users.SingleOrDefault(u => u.Email == email && u.Status == "Active");

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password ?? "", user.PasswordHash))
            return Unauthorized(new { message = "Use an authorized internal account." });

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
