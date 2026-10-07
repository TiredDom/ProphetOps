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
    public const int SchemaVersion1 = 1;
    public const int SchemaVersion2 = 2;
    public const int CurrentSchemaVersion = SchemaVersion1;

    public string ResolveDatabaseProvider()
    {
        if (Database is null)
            throw new InvalidOperationException("Manifest Database is required.");

        if (!string.IsNullOrWhiteSpace(Database.Provider))
        {
            var p = Database.Provider.Trim();
            if (string.Equals(p, "postgresql", StringComparison.OrdinalIgnoreCase)) return "postgres";
            return p.ToLowerInvariant();
        }

        if (SchemaVersion == SchemaVersion1)
        {
            return "sqlite";
        }

        throw new InvalidOperationException($"Manifest SchemaVersion {SchemaVersion} requires an explicit database provider.");
    }

    public void Validate()
    {
        if (SchemaVersion < SchemaVersion1 || SchemaVersion > SchemaVersion2)
            throw new InvalidOperationException($"Unsupported backup manifest schema version: {SchemaVersion}");

        if (string.IsNullOrWhiteSpace(PackageId))
            throw new InvalidOperationException("Manifest PackageId is required.");

        if (Database is null)
            throw new InvalidOperationException("Manifest Database is required.");

        var provider = ResolveDatabaseProvider();
        if (string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            if (SchemaVersion >= SchemaVersion2)
            {
                if (string.IsNullOrWhiteSpace(Database.DumpFormat))
                    throw new InvalidOperationException("SchemaVersion 2 manifests require a non-empty DumpFormat.");
                if (!string.Equals(Database.DumpFormat, "sqlite-file", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Invalid dump format '{Database.DumpFormat}' for SQLite provider.");
            }
            else
            {
                if (Database.DumpFormat is not null && !string.Equals(Database.DumpFormat, "sqlite-file", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Invalid dump format '{Database.DumpFormat}' for SQLite provider.");
            }
        }
        else if (string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            if (SchemaVersion < SchemaVersion2)
                throw new InvalidOperationException("PostgreSQL backup packages require manifest SchemaVersion 2 or newer.");

            if (string.IsNullOrWhiteSpace(Database.DumpFormat))
                throw new InvalidOperationException("SchemaVersion 2 manifests require a non-empty DumpFormat.");

            if (!string.Equals(Database.DumpFormat, "pg-dump-custom", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Invalid dump format '{Database.DumpFormat}' for PostgreSQL provider.");

            if (!Database.ServerMajor.HasValue || Database.ServerMajor.Value <= 0)
                throw new InvalidOperationException($"PostgreSQL backup packages require a valid positive ServerMajor version, got '{Database.ServerMajor}'.");
        }
        else
        {
            throw new InvalidOperationException($"Unsupported database provider in manifest: '{provider}'.");
        }
    }
}

public sealed record BackupEncryptionManifest(string Mode, string? KeyId, string? EnvelopeVersion = null, string? Nonce = null);

public sealed record BackupDatabaseManifest(
    string Path,
    long Size,
    string Sha256,
    string IntegrityCheck,
    string? Provider = null,
    int? ServerMajor = null,
    string? DumpFormat = null);

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
