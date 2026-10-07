using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class SqliteBackupCapture(
    StoragePaths storage,
    ILogger<SqliteBackupCapture> log) : IDatabaseBackupCapture
{
    public bool CanCapture(AppDbContext db)
    {
        var provider = db.Database.ProviderName;
        return provider == null || provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IDatabaseCaptureSession?> StartCaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken)
    {
        var provider = db.Database.ProviderName;
        if (provider != null && !provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"PostgreSQL backup capture is not supported by SqliteBackupCapture. Active provider: {provider}");
        }

        var database = DatabasePath(db);
        if (database is null) return Task.FromResult<IDatabaseCaptureSession?>(null);

        IDatabaseCaptureSession session = new SqliteCaptureSession(db, database, workingDirectory);
        return Task.FromResult<IDatabaseCaptureSession?>(session);
    }

    public async Task<DatabaseCaptureResult?> CaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken)
    {
        await using var session = await StartCaptureAsync(db, workingDirectory, cancellationToken);
        return session is null ? null : await session.ExecuteDumpAsync(cancellationToken);
    }

    private sealed class SqliteCaptureSession(
        AppDbContext db,
        string database,
        string workingDirectory) : IDatabaseCaptureSession
    {
        public async Task<DatabaseCaptureMetadata> GetMetadataAsync(CancellationToken cancellationToken)
        {
            var users = await db.Users.CountAsync(cancellationToken);
            var packages = await db.TravelPackages.CountAsync(cancellationToken);
            var bookings = await db.Bookings.CountAsync(cancellationToken);
            var expenses = await db.Expenses.CountAsync(cancellationToken);
            var audits = await db.AuditEntries.CountAsync(cancellationToken);
            var migrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var images = await db.TravelPackages
                .AsNoTracking()
                .Where(p => p.ImagePath != null)
                .Select(p => p.ImagePath!)
                .Distinct()
                .ToListAsync(cancellationToken);

            var counts = new BackupCounts(users, packages, bookings, expenses, audits, images.Count);
            return new DatabaseCaptureMetadata(counts, migrations, images);
        }

        public async Task<DatabaseCaptureResult> ExecuteDumpAsync(CancellationToken cancellationToken)
        {
            var dbRelative = Path.Combine("database", "prophetops.db");
            var stagedDb = Path.Combine(workingDirectory, dbRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(stagedDb)!);
            CopyDatabase(database, stagedDb);
            File.Delete(stagedDb + "-wal");
            File.Delete(stagedDb + "-shm");
            File.Delete(stagedDb + "-journal");
            var integrity = Integrity(stagedDb);
            if (!string.Equals(integrity, "ok", StringComparison.Ordinal))
                throw new InvalidOperationException("The captured database did not pass SQLite integrity checks.");
            File.Delete(stagedDb + "-wal");
            File.Delete(stagedDb + "-shm");
            File.Delete(stagedDb + "-journal");

            var size = new FileInfo(stagedDb).Length;
            var sha256 = await BackupPackageWriter.Sha256File(stagedDb, cancellationToken);
            var normalized = dbRelative.Replace('\\', '/');

            // Emitted SQLite packages use SchemaVersion 1 and keep Provider: null to maintain 100% compatibility with restore-backup.ps1 line 112
            var manifest = new BackupDatabaseManifest(
                normalized,
                size,
                sha256,
                integrity,
                Provider: null,
                ServerMajor: null,
                DumpFormat: "sqlite-file");

            return new DatabaseCaptureResult(manifest, "sqlite-database");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private string? DatabasePath(AppDbContext db)
    {
        var connectionString = db.Database.GetDbConnection().ConnectionString;
        var connection = new SqliteConnectionStringBuilder(connectionString);
        var source = connection.DataSource;
        if (string.IsNullOrWhiteSpace(source)
            || connection.Mode == SqliteOpenMode.Memory
            || source.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            log.LogInformation("Backup skipped: this instance runs on an in-memory database.");
            return null;
        }

        return Path.IsPathRooted(source) ? source : Path.GetFullPath(Path.Combine(storage.Root, source));
    }

    private static void CopyDatabase(string database, string destination)
    {
        using var source = new SqliteConnection(PathOnly(database));
        using var target = new SqliteConnection(PathOnly(destination));
        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    private static string Integrity(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA integrity_check;";
        return check.ExecuteScalar() as string ?? "unknown";
    }

    private static string PathOnly(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
}
