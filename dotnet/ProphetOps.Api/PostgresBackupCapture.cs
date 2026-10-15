using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class PostgresBackupCapture(
    StoragePaths storage,
    IConfiguration configuration,
    IPostgresProcessRunner runner,
    ILogger<PostgresBackupCapture> log) : IDatabaseBackupCapture
{
    public bool CanCapture(AppDbContext db)
    {
        var provider = db.Database.ProviderName;
        return provider != null && provider.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IDatabaseCaptureSession?> StartCaptureAsync(
        AppDbContext db,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var provider = db.Database.ProviderName;
        if (provider != null && !provider.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"PostgreSQL backup capture is not supported for provider '{provider}'.");
        }

        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string is empty or missing.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            // Determine actual server major version from database
            int serverMajor;
            await using (var versionCmd = connection.CreateCommand())
            {
                versionCmd.CommandText = "SHOW server_version_num;";
                var scalar = await versionCmd.ExecuteScalarAsync(cancellationToken);
                if (scalar is null || !int.TryParse(scalar.ToString(), out var num) || num <= 0)
                {
                    throw new InvalidOperationException("Failed to determine PostgreSQL server version from database connection.");
                }
                serverMajor = num / 10000;
            }

            if (serverMajor < 14)
            {
                throw new InvalidOperationException($"PostgreSQL server major version {serverMajor} is below the minimum supported version (14).");
            }

            // Verify client tool version compatibility before taking snapshot
            var clientMajor = await runner.GetClientMajorVersionAsync(cancellationToken);
            if (clientMajor < serverMajor)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL client tools version mismatch: pg_dump major version ({clientMajor}) is older than server major version ({serverMajor}). Client tools must be at least as new as the database server.");
            }

            // 1. Acquire session-level advisory lock in read-committed mode BEFORE starting repeatable read transaction.
            // This guarantees any concurrent writer holding the lock commits/rolls back before our repeatable read snapshot is taken.
            await using (var lockCmd = connection.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(8070, 1);";
                lockCmd.CommandTimeout = Math.Max(5, configuration.GetValue("Backup:DrainTimeoutSeconds", 15));
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Begin repeatable read transaction AFTER advisory lock is granted
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

            // 3. Acquire transaction-level lock to guard against unexpected connection reuse
            await using (var xactLockCmd = connection.CreateCommand())
            {
                xactLockCmd.Transaction = transaction;
                xactLockCmd.CommandText = "SELECT pg_advisory_xact_lock(8070, 1);";
                await xactLockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 4. Export snapshot under the repeatable read transaction
            string snapshotId;
            await using (var snapCmd = connection.CreateCommand())
            {
                snapCmd.Transaction = transaction;
                snapCmd.CommandText = "SELECT pg_export_snapshot();";
                var result = await snapCmd.ExecuteScalarAsync(cancellationToken);
                snapshotId = result?.ToString() ?? throw new InvalidOperationException("Failed to export PostgreSQL snapshot.");
            }

            return new PostgresCaptureSession(
                connection,
                transaction,
                snapshotId,
                serverMajor,
                builder,
                workingDirectory,
                storage,
                configuration,
                runner,
                log);
        }
        catch
        {
            await TryCleanupConnection(connection);
            throw;
        }
    }

    private static async Task TryCleanupConnection(NpgsqlConnection connection)
    {
        try
        {
            if (connection.State == ConnectionState.Open)
            {
                await using var unlock = connection.CreateCommand();
                unlock.CommandText = "SELECT pg_advisory_unlock(8070, 1);";
                await unlock.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        catch { }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    public sealed class PostgresCaptureSession(
        NpgsqlConnection? connection,
        NpgsqlTransaction? transaction,
        string snapshotId,
        int serverMajor,
        NpgsqlConnectionStringBuilder builder,
        string workingDirectory,
        StoragePaths storage,
        IConfiguration configuration,
        IPostgresProcessRunner runner,
        ILogger log) : IDatabaseCaptureSession
    {
        public async Task<DatabaseCaptureMetadata> GetMetadataAsync(CancellationToken cancellationToken)
        {
            if (connection is null || transaction is null)
            {
                return new DatabaseCaptureMetadata(new BackupCounts(0, 0, 0, 0, 0, 0), [], []);
            }

            var users = await QueryCountAsync("SELECT count(*) FROM prophetops.\"Users\";", cancellationToken);
            var packages = await QueryCountAsync("SELECT count(*) FROM prophetops.\"TravelPackages\";", cancellationToken);
            var bookings = await QueryCountAsync("SELECT count(*) FROM prophetops.\"Bookings\";", cancellationToken);
            var expenses = await QueryCountAsync("SELECT count(*) FROM prophetops.\"Expenses\";", cancellationToken);
            var audits = await QueryCountAsync("SELECT count(*) FROM prophetops.\"AuditEntries\";", cancellationToken);

            var migrations = new List<string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT \"MigrationId\" FROM prophetops.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    migrations.Add(reader.GetString(0));
                }
            }

            var images = new List<string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT \"ImagePath\" FROM prophetops.\"TravelPackages\" WHERE \"ImagePath\" IS NOT NULL;";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        var path = reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(path) && !images.Contains(path))
                            images.Add(path);
                    }
                }
            }

            var counts = new BackupCounts(
                Users: users,
                TravelPackages: packages,
                Bookings: bookings,
                Expenses: expenses,
                AuditEntries: audits,
                PackageImages: images.Count);

            return new DatabaseCaptureMetadata(counts, migrations, images);
        }

        public async Task<DatabaseCaptureResult> ExecuteDumpAsync(CancellationToken cancellationToken)
        {
            var relativePath = Path.Combine("database", "prophetops.dump");
            var outputPath = Path.Combine(workingDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var hardQuota = configuration.GetValue<long?>("Backup:StagingMaxBytes")
                ?? (5L * 1024 * 1024 * 1024); // default 5 GB hard quota

            if (hardQuota <= 0)
            {
                throw new InvalidOperationException("Backup staging quota must be a positive number of bytes.");
            }

            var dumpParams = new PostgresDumpParameters(
                Host: string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host,
                Port: builder.Port > 0 ? builder.Port : 5432,
                Database: builder.Database ?? "prophetops",
                Username: builder.Username ?? "postgres",
                Password: builder.Password,
                Schema: "prophetops",
                SnapshotId: snapshotId,
                OutputFilePath: outputPath,
                SslMode: PostgresProcessRunner.MapSslModeToLibPq(builder.SslMode),
                SslRootCert: builder.RootCertificate,
                Timeout: TimeSpan.FromSeconds(configuration.GetValue("Backup:Postgres:DumpTimeoutSeconds", 300)),
                StagingPath: storage.BackupStagingPath,
                StagingMaxBytes: hardQuota);

            var dumpResult = await runner.RunDumpAsync(dumpParams, cancellationToken);
            if (dumpResult.ExitCode != 0)
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                var category = PostgresProcessRunner.CategorizeError(dumpResult.StandardError, dumpResult.ExitCode);
                throw new InvalidOperationException($"PostgreSQL pg_dump failed with exit code {dumpResult.ExitCode}. Diagnostic category: {category}.");
            }

            var verifyResult = await runner.RunRestoreListAsync(outputPath, cancellationToken);
            if (verifyResult.ExitCode != 0)
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                var category = PostgresProcessRunner.CategorizeError(verifyResult.StandardError, verifyResult.ExitCode);
                throw new InvalidOperationException($"PostgreSQL pg_restore verification failed with exit code {verifyResult.ExitCode}. Diagnostic category: {category}.");
            }

            var size = new FileInfo(outputPath).Length;
            var sha256 = await BackupPackageWriter.Sha256File(outputPath, cancellationToken);
            var normalized = relativePath.Replace('\\', '/');

            var manifest = new BackupDatabaseManifest(
                Path: normalized,
                Size: size,
                Sha256: sha256,
                IntegrityCheck: "pg_restore-list-verified",
                Provider: "postgres",
                ServerMajor: serverMajor,
                DumpFormat: "pg-dump-custom");

            log.LogInformation("PostgreSQL backup dump completed and verified at {OutputPath} (SHA-256: {Sha})", outputPath, sha256);
            return new DatabaseCaptureResult(manifest, "postgres-database");
        }

        private async Task<int> QueryCountAsync(string sql, CancellationToken cancellationToken)
        {
            if (connection is null || transaction is null) return 0;
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(scalar);
        }

        public async ValueTask DisposeAsync()
        {
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    await transaction.DisposeAsync();
                }
                catch { }
            }

            if (connection != null)
            {
                await TryCleanupConnection(connection);
            }
        }
    }
}
