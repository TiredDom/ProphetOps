using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProphetOps.Data;

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
        Ok(_db.Users
            .OrderBy(u => u.Name)
            .Select(u => new { u.Name, u.Role, u.Email, u.Status })
            .ToList());
}
