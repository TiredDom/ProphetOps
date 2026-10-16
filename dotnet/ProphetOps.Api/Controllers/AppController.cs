using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProphetOps.Api.Controllers;

/// Settings the signed-in app needs: the business date and time zone, and the agency's name. They
/// stay behind sign-in, so a visitor to the sign-in page learns nothing about whose system it is.
[ApiController]
[Route("api/app")]
[Authorize]
public class AppController(IBusinessClock clock, IConfiguration configuration) : ControllerBase
{
    [HttpGet("config")]
    public IActionResult Config() => Ok(new
    {
        today = clock.Today.ToString("yyyy-MM-dd"),
        timeZone = clock.TimeZoneId,
        businessName = (configuration["Business:Name"] ?? "").Trim(),
    });
}
