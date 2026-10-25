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

    [HttpPost("backup/export")]
    public async Task<IActionResult> Export([FromServices] BackupPackageWriter writer, CancellationToken cancellationToken)
    {
        BackupPackage? package;
        try
        {
            package = await writer.CreateExportAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Backup package export failed." });
        }

        if (package is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Backup package export unavailable. Active writes did not drain or backup encryption is not configured." });
        }

        var fileName = Path.GetFileName(package.ArchivePath);
        return new AutoDeletingFileResult(package.ArchivePath, "application/octet-stream", fileName);
    }
}
