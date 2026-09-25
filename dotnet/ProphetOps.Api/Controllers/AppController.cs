using Microsoft.AspNetCore.Mvc;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/app")]
public class AppController(IBusinessClock clock) : ControllerBase
{
    [HttpGet("config")]
    public IActionResult Config() => Ok(new
    {
        today = clock.Today.ToString("yyyy-MM-dd"),
        timeZone = clock.TimeZoneId,
    });
}
