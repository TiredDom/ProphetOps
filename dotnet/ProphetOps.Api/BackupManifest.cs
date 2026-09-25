using System.Text.Json.Serialization;

namespace ProphetOps.Api;

public sealed record BackupManifest(
    int SchemaVersion,
    string PackageId,
    DateTimeOffset CreatedUtc,
    string Application,
    string Environment,
    string SessionContinuity,
    BackupEncryptionManifest Encryption,
    BackupDatabaseManifest Database,
    IReadOnlyList<BackupFileManifest> Files,
    BackupCounts Counts,
    IReadOnlyList<BackupConfigurationFact> Configuration,
    IReadOnlyList<string> EfMigrations)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record BackupEncryptionManifest(string Mode, string? KeyId, string? EnvelopeVersion = null, string? Nonce = null);

public sealed record BackupDatabaseManifest(
    string Path,
    long Size,
    string Sha256,
    string IntegrityCheck);

public sealed record BackupFileManifest(
    string Path,
    string Role,
    long Size,
    string Sha256,
    DateTimeOffset LastWriteUtc);

public sealed record BackupCounts(
    int Users,
    int TravelPackages,
    int Bookings,
    int Expenses,
    int AuditEntries,
    int PackageImages);

public sealed record BackupConfigurationFact(string Name, string Status, string? Value = null);

public sealed record BackupPackage(
    string PackageId,
    string ArchivePath,
    string Sha256,
    long Size,
    BackupManifest Manifest);

public sealed record BackupRunResult(
    bool Success,
    string? PackageId,
    string? ArchivePath,
    string? StoredName,
    string? Error,
    BackupManifest? Manifest)
{
    public static BackupRunResult Failed(string error) => new(false, null, null, null, error, null);
    public static BackupRunResult Completed(BackupPackage package, BackupUploadResult upload) =>
        new(true, package.PackageId, upload.Path, upload.Name, null, package.Manifest);
}

[JsonSerializable(typeof(BackupManifest))]
public sealed partial class BackupJsonContext : JsonSerializerContext;
