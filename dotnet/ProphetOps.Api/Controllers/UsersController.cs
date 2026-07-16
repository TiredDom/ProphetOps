using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;
using ProphetOps.Domain;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "Users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Get() =>
        Ok(_db.Users.OrderBy(u => u.Name).ToList().Select(Dto));

    [HttpGet("roles")]
    public IActionResult RoleOptions() =>
        Ok(Roles.All.Select(r => new { name = r, access = Roles.AccessSummary(r) }));

    [HttpPost]
    public IActionResult Store([FromBody] UserRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var errors = Validate(request, email, isNew: true);
        if (errors.Count > 0) return BadRequest(errors);

        var user = new User
        {
            Name = request.Name!.Trim(),
            Email = email,
            Role = request.Role!,
            Status = Normalize(request.Status),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password!),
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        return Ok(Dto(user));
    }

    [HttpPut("{email}")]
    public IActionResult Update(string email, [FromBody] UserRequest request)
    {
        var key = (email ?? "").Trim().ToLowerInvariant();
        var user = _db.Users.SingleOrDefault(u => u.Email == key);
        if (user is null) return NotFound();

        var errors = Validate(request, key, isNew: false);
        if (errors.Count > 0) return BadRequest(errors);

        var currentEmail = (User.FindFirst(ClaimTypes.Email)?.Value ?? "").Trim().ToLowerInvariant();
        var isSelf = currentEmail == key;
        var nextStatus = Normalize(request.Status);

        if (isSelf && nextStatus != "Active")
            return BadRequest(new Dictionary<string, string> { ["status"] = "You cannot suspend your own account." });
        if (isSelf && request.Role != user.Role)
            return BadRequest(new Dictionary<string, string> { ["role"] = "You cannot change your own access level." });

        var losesOwner = user.Role == Roles.OwnerManagement
            && (request.Role != Roles.OwnerManagement || nextStatus != "Active");
        if (losesOwner)
        {
            var otherActiveOwners = _db.Users.Count(u =>
                u.Email != key && u.Role == Roles.OwnerManagement && u.Status == "Active");
            if (otherActiveOwners == 0)
                return BadRequest(new Dictionary<string, string> { ["role"] = "Keep at least one active owner account." });
        }

        user.Name = request.Name!.Trim();
        user.Role = request.Role!;
        user.Status = nextStatus;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        _db.SaveChanges();

        return Ok(Dto(user));
    }

    private Dictionary<string, string> Validate(UserRequest request, string email, bool isNew)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = "Enter the person's name.";
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            errors["email"] = "Enter a valid email address.";
        else if (isNew && _db.Users.Any(u => u.Email == email))
            errors["email"] = "That email already has an account.";
        if (string.IsNullOrWhiteSpace(request.Role) || !Roles.All.Contains(request.Role))
            errors["role"] = "Choose an access level.";
        if (isNew && (string.IsNullOrWhiteSpace(request.Password) || request.Password!.Length < 8))
            errors["password"] = "Set a temporary password of at least 8 characters.";
        else if (!isNew && !string.IsNullOrEmpty(request.Password) && request.Password!.Length < 8)
            errors["password"] = "A new password must be at least 8 characters.";

        return errors;
    }

    private static string Normalize(string? status) =>
        string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase) ? "Suspended" : "Active";

    private static object Dto(User u) => new
    {
        name = u.Name,
        email = u.Email,
        role = u.Role,
        status = u.Status,
        lastLoginAt = u.LastLoginAt,
    };
}
