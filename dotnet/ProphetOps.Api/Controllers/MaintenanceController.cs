using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProphetOps.Api.Controllers;

[ApiController]
[Route("api/maintenance")]
[Authorize(Policy = "Users")]
public sealed class MaintenanceController : ControllerBase
{
    [HttpPost("backup")]
    public async Task<IActionResult> Backup([FromServices] BackupPackageWriter writer, CancellationToken cancellationToken)
    {
        var result = await writer.RunAsync(cancellationToken);
        if (!result.Success) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Error });
        return Ok(new
        {
            packageId = result.PackageId,
            storedName = result.StoredName,
            manifest = result.Manifest is null ? null : new
            {
                result.Manifest.SchemaVersion,
                result.Manifest.CreatedUtc,
                result.Manifest.Counts,
                encryption = result.Manifest.Encryption.Mode,
            },
        });
    }
}
