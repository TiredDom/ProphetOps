using ProphetOps.Data;

namespace ProphetOps.Api;

public interface IDatabaseBackupCapture
{
    bool CanCapture(AppDbContext db);

    Task<IDatabaseCaptureSession?> StartCaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken);

    async Task<DatabaseCaptureResult?> CaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken)
    {
        await using var session = await StartCaptureAsync(db, workingDirectory, cancellationToken);
        return session is null ? null : await session.ExecuteDumpAsync(cancellationToken);
    }
}

public interface IDatabaseCaptureSession : IAsyncDisposable
{
    Task<DatabaseCaptureMetadata> GetMetadataAsync(CancellationToken cancellationToken);

    Task<DatabaseCaptureResult> ExecuteDumpAsync(CancellationToken cancellationToken);
}

public sealed record DatabaseCaptureMetadata(
    BackupCounts Counts,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> ReferencedImageKeys);

public sealed record DatabaseCaptureResult(
    BackupDatabaseManifest DatabaseManifest,
    string FileRole);
