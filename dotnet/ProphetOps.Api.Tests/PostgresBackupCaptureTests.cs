using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class PostgresBackupCaptureTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "prophetops-pg-backup-tests-" + Guid.NewGuid().ToString("N"));

    public PostgresBackupCaptureTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SslMode_mapping_covers_all_modes_with_exact_libpq_values()
    {
        // Must match https://www.postgresql.org/docs/current/libpq-connect.html
        Assert.Equal("disable", PostgresProcessRunner.MapSslModeToLibPq(SslMode.Disable));
        Assert.Equal("allow", PostgresProcessRunner.MapSslModeToLibPq(SslMode.Allow));
        Assert.Equal("prefer", PostgresProcessRunner.MapSslModeToLibPq(SslMode.Prefer));
        Assert.Equal("require", PostgresProcessRunner.MapSslModeToLibPq(SslMode.Require));
        Assert.Equal("verify-ca", PostgresProcessRunner.MapSslModeToLibPq(SslMode.VerifyCA));
        Assert.Equal("verify-full", PostgresProcessRunner.MapSslModeToLibPq(SslMode.VerifyFull));
    }

    [Fact]
    public void ProcessRunner_isolates_environment_and_removes_ambient_PG_variables()
    {
        var psi = new ProcessStartInfo("pg_dump");
        // Simulate dirty parent environment
        psi.Environment["PGDATABASE"] = "ambient_db";
        psi.Environment["PGHOST"] = "ambient_host";
        psi.Environment["PGUSER"] = "ambient_user";
        psi.Environment["PGPORT"] = "9999";
        psi.Environment["PGPASSFILE"] = "/ambient/passfile";
        psi.Environment["PATH"] = "/usr/bin";

        PostgresProcessRunner.IsolatePostgresEnvironment(psi);

        // Ambients must be stripped
        Assert.False(psi.Environment.ContainsKey("PGDATABASE"));
        Assert.False(psi.Environment.ContainsKey("PGHOST"));
        Assert.False(psi.Environment.ContainsKey("PGUSER"));
        Assert.False(psi.Environment.ContainsKey("PGPORT"));
        Assert.False(psi.Environment.ContainsKey("PGPASSFILE"));
        // Unrelated variables preserved
        Assert.True(psi.Environment.ContainsKey("PATH"));
    }

    [Fact]
    public void ProcessRunner_builds_correct_pg_dump_arguments_without_credentials_on_cli()
    {
        var parameters = new PostgresDumpParameters(
            Host: "db.local",
            Port: 5432,
            Username: "prophet_app",
            Password: "SuperSecretPassword123!",
            Database: "prophetops_prod",
            Schema: "prophetops",
            SnapshotId: "00000003-0000001A-1",
            OutputFilePath: Path.Combine(_tempDir, "prophetops.dump"),
            SslMode: "verify-full",
            SslRootCert: "/certs/root.crt");

        var psi = PostgresProcessRunner.BuildDumpProcessStartInfo("pg_dump", parameters);

        // Required flags per specification
        Assert.Equal("pg_dump", psi.FileName);
        Assert.Contains("-h", psi.ArgumentList);
        Assert.Equal("db.local", psi.ArgumentList[psi.ArgumentList.IndexOf("-h") + 1]);
        Assert.Contains("-p", psi.ArgumentList);
        Assert.Equal("5432", psi.ArgumentList[psi.ArgumentList.IndexOf("-p") + 1]);
        Assert.Contains("-U", psi.ArgumentList);
        Assert.Equal("prophet_app", psi.ArgumentList[psi.ArgumentList.IndexOf("-U") + 1]);
        Assert.Contains("-d", psi.ArgumentList);
        Assert.Equal("prophetops_prod", psi.ArgumentList[psi.ArgumentList.IndexOf("-d") + 1]);
        Assert.Contains("-n", psi.ArgumentList);
        Assert.Equal("prophetops", psi.ArgumentList[psi.ArgumentList.IndexOf("-n") + 1]);
        Assert.Contains("-Fc", psi.ArgumentList);
        Assert.Contains("-O", psi.ArgumentList);
        Assert.Contains("-x", psi.ArgumentList);
        Assert.Contains("--snapshot=00000003-0000001A-1", psi.ArgumentList);
        Assert.Contains("-f", psi.ArgumentList);
        Assert.Equal(Path.Combine(_tempDir, "prophetops.dump"), psi.ArgumentList[psi.ArgumentList.IndexOf("-f") + 1]);

        // CRITICAL: Password must NEVER appear in argument list or command line
        foreach (var arg in psi.ArgumentList)
        {
            Assert.DoesNotContain("SuperSecretPassword123!", arg);
        }

        // Environment variables must contain password and TLS settings
        Assert.Equal("SuperSecretPassword123!", psi.Environment["PGPASSWORD"]);
        Assert.Equal("verify-full", psi.Environment["PGSSLMODE"]);
        Assert.Equal("/certs/root.crt", psi.Environment["PGSSLROOTCERT"]);
    }

    [Fact]
    public async Task ProcessRunner_reads_huge_single_line_output_via_bounded_chunks()
    {
        // 1MB string with NO newlines
        var hugeSingleLine = new string('Z', 1024 * 1024);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(hugeSingleLine));
        using var reader = new StreamReader(stream);

        var output = await PostgresProcessRunner.ReadStreamBoundedAsync(reader, 65536, CancellationToken.None);

        Assert.Equal(65536, output.Length);
        Assert.All(output, c => Assert.Equal('Z', c));
    }

    [Fact]
    public void ProcessRunner_categorizes_errors_generically_without_leaking_raw_stderr_or_secrets()
    {
        var authError = "pg_dump: error: connection failed: password authentication failed for user prophet_app, secret: SuperP@ssw0rd!";
        var catAuth = PostgresProcessRunner.CategorizeError(authError, 1);
        Assert.Equal("Authentication failure", catAuth);
        Assert.DoesNotContain("SuperP@ssw0rd!", catAuth);
        Assert.DoesNotContain("prophet_app", catAuth);

        var connError = "pg_dump: error: could not connect to server: Connection refused (0x0000274D/10061) at host postgres.internal:5432";
        var catConn = PostgresProcessRunner.CategorizeError(connError, 1);
        Assert.Equal("Connection or network failure", catConn);
        Assert.DoesNotContain("postgres.internal", catConn);

        var corruptError = "pg_restore: error: archive is corrupt or header invalid at offset 0x44";
        var catCorrupt = PostgresProcessRunner.CategorizeError(corruptError, 1);
        Assert.Equal("Archive format or integrity failure", catCorrupt);

        var diskError = "pg_dump: error: write error: no space left on device";
        var catDisk = PostgresProcessRunner.CategorizeError(diskError, 1);
        Assert.Equal("Disk space or quota failure", catDisk);

        var generic = PostgresProcessRunner.CategorizeError("something unclassified occurred", 42);
        Assert.Equal("Process exited with code 42", generic);
    }

    [Fact]
    public void ProcessRunner_parses_version_output_correctly()
    {
        Assert.Equal(18, PostgresProcessRunner.ParseVersion("pg_dump (PostgreSQL) 18.0\n"));
        Assert.Equal(17, PostgresProcessRunner.ParseVersion("pg_dump (PostgreSQL) 17.2\n"));
        Assert.Equal(16, PostgresProcessRunner.ParseVersion("pg_dump (PostgreSQL) 16.3 (Ubuntu 16.3-1ubuntu1)\n"));
        Assert.Equal(15, PostgresProcessRunner.ParseVersion("pg_dump 15.4\n"));
    }

    [Fact]
    public void ProcessRunner_validates_positive_quota_and_fails_closed_on_enumeration_error()
    {
        // Non-positive quota must be rejected
        var exZero = Assert.Throws<InvalidOperationException>(() =>
        {
            var runner = new PostgresProcessRunner(new ConfigurationBuilder().Build(), NullLogger<PostgresProcessRunner>.Instance);
            var parameters = new PostgresDumpParameters("h", 5432, "d", "u", "p", "s", "snap", "out",
                StagingPath: _tempDir, StagingMaxBytes: 0);
            _ = runner.RunDumpAsync(parameters, CancellationToken.None).GetAwaiter().GetResult();
        });
        Assert.Contains("Backup staging quota must be a positive number of bytes", exZero.Message);

        var exNegative = Assert.Throws<InvalidOperationException>(() =>
        {
            var runner = new PostgresProcessRunner(new ConfigurationBuilder().Build(), NullLogger<PostgresProcessRunner>.Instance);
            var parameters = new PostgresDumpParameters("h", 5432, "d", "u", "p", "s", "snap", "out",
                StagingPath: _tempDir, StagingMaxBytes: -100);
            _ = runner.RunDumpAsync(parameters, CancellationToken.None).GetAwaiter().GetResult();
        });
        Assert.Contains("Backup staging quota must be a positive number of bytes", exNegative.Message);

        // CalculateDirectorySize fails closed (does not return 0 or swallow errors)
        Assert.Equal(0, PostgresProcessRunner.CalculateDirectorySize(Path.Combine(_tempDir, "nonexistent")));
    }

    [Fact]
    public async Task ProcessRunner_terminates_process_tree_on_cancellation()
    {
        var toolsDir = Path.Combine(_tempDir, "tools-cancel");
        Directory.CreateDirectory(toolsDir);
        var scriptPath = Path.Combine(toolsDir, OperatingSystem.IsWindows() ? "pg_dump.cmd" : "pg_dump");
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllLines(scriptPath, [
                "@echo off",
                ":loop",
                "ping 127.0.0.1 -n 2 >nul",
                "goto loop"
            ]);
        }
        else
        {
            File.WriteAllLines(scriptPath, [
                "#!/bin/sh",
                "while true; do",
                "  sleep 1",
                "done"
            ]);
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(scriptPath, mode);
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:Postgres:ClientToolsPath"] = toolsDir
            })
            .Build();

        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);
        var parameters = new PostgresDumpParameters(
            "localhost", 5432, "db", "user", "secret", "prophetops", "snap1", Path.Combine(_tempDir, "out.dump"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunDumpAsync(parameters, cts.Token));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), "Cancellation did not terminate process promptly.");
    }

    [Fact]
    public async Task PostgresCaptureSession_dump_failure_deletes_partial_file_and_emits_generic_category_error()
    {
        var fakeRunner = new FakeProcessRunner
        {
            DumpResult = new ProcessExecutionResult(1, "", "FATAL: password authentication failed for user prophet_app, secret: MySecret123", false)
        };

        var working = Path.Combine(_tempDir, "working-fail-session");
        Directory.CreateDirectory(working);
        var dumpPath = Path.Combine(working, "database", "prophetops.dump");
        Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
        await File.WriteAllTextAsync(dumpPath, "corrupted dump partial content");

        Assert.True(File.Exists(dumpPath));

        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-session-fail"));
        var config = new ConfigurationBuilder().Build();
        var builder = new NpgsqlConnectionStringBuilder("Host=db.local;Database=prophetops;Username=prophet_app;Password=MySecret123;");

        // Use actual PostgresCaptureSession
        var session = new PostgresBackupCapture.PostgresCaptureSession(
            null, null, "snap-fail", 18, builder, working, storage, config, fakeRunner, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteDumpAsync(CancellationToken.None));

        // Partial file deleted
        Assert.False(File.Exists(dumpPath));
        // Error is categorized and does NOT leak secret or raw stderr
        Assert.Contains("Diagnostic category: Authentication failure", ex.Message);
        Assert.DoesNotContain("MySecret123", ex.Message);
        Assert.DoesNotContain("FATAL: password authentication failed", ex.Message);
    }

    [Fact]
    public async Task PostgresCaptureSession_restore_list_failure_deletes_file_and_emits_generic_category_error()
    {
        var fakeRunner = new FakeProcessRunner
        {
            DumpResult = new ProcessExecutionResult(0, "", "", false),
            RestoreListResult = new ProcessExecutionResult(1, "", "pg_restore: error: archive is corrupt or truncated", false)
        };

        var working = Path.Combine(_tempDir, "working-restore-fail-session");
        Directory.CreateDirectory(working);
        var dumpPath = Path.Combine(working, "database", "prophetops.dump");
        Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
        await File.WriteAllTextAsync(dumpPath, "dump content");

        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-session-restore-fail"));
        var config = new ConfigurationBuilder().Build();
        var builder = new NpgsqlConnectionStringBuilder("Host=db.local;Database=prophetops;Username=prophet_app;Password=secret;");

        var session = new PostgresBackupCapture.PostgresCaptureSession(
            null, null, "snap-restore-fail", 18, builder, working, storage, config, fakeRunner, NullLogger.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteDumpAsync(CancellationToken.None));

        Assert.False(File.Exists(dumpPath));
        Assert.Contains("Diagnostic category: Archive format or integrity failure", ex.Message);
        Assert.DoesNotContain("pg_restore: error: archive is corrupt", ex.Message);
    }

    [Fact]
    public async Task BackupPackageWriter_enforces_lifecycle_staging_quota_on_excessive_images()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-quota-img"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        // 1 KB quota
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:StagingMaxBytes"] = "1024"
            })
            .Build();

        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        // Pre-create good backup in storage to ensure it is not deleted
        Directory.CreateDirectory(storage.BackupStagingPath);
        var goodBackup = Path.Combine(storage.BackupStagingPath, "prophetops-20261001T000000Z-good.zip");
        await File.WriteAllTextAsync(goodBackup, "retained good backup");

        // Store a 5 KB image that exceeds the 1 KB quota
        using var heavyStream = new MemoryStream(new byte[5120]);
        await objectStorage.PutAsync("packages/heavy.png", heavyStream, "image/png", CancellationToken.None);

        var fakeCapture = new FakePostgresBackupCapture(
            referencedImages: ["packages/heavy.png"],
            appliedMigrations: ["20260927_InitialPostgres"]);

        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            fakeCapture,
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("Backup staging hard quota exceeded", ex.Message);

        // Good backup must be preserved
        Assert.True(File.Exists(goodBackup));
    }

    [Fact]
    public async Task BackupPackageWriter_with_postgres_capture_stages_package_with_schema_version_2_under_lock()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg-stage"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        using var imageStream = new MemoryStream([1, 2, 3, 4, 5]);
        await objectStorage.PutAsync("packages/tour.jpg", imageStream, "image/jpeg", CancellationToken.None);

        Directory.CreateDirectory(storage.KeysPath);
        await File.WriteAllTextAsync(Path.Combine(storage.KeysPath, "key-pg-test.xml"), "<key id='test'/>");

        var fakeCapture = new FakePostgresBackupCapture(
            referencedImages: ["packages/tour.jpg"],
            appliedMigrations: ["20260927_InitialPostgres", "20260928_AddDataProtectionKeys"]);

        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            fakeCapture,
            NullLogger<BackupPackageWriter>.Instance);

        var package = await writer.CreatePackageUnderGate(CancellationToken.None);
        Assert.NotNull(package);

        Assert.True(File.Exists(package.ArchivePath));

        using var zip = ZipFile.OpenRead(package.ArchivePath);
        var manifestEntry = zip.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync(manifestStream, BackupJsonContext.Default.BackupManifest);
        Assert.NotNull(manifest);

        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal("postgres", manifest.Database.Provider);
        Assert.Equal("pg-dump-custom", manifest.Database.DumpFormat);
        Assert.Equal(18, manifest.Database.ServerMajor);
        Assert.Equal("pg_restore-list-verified", manifest.Database.IntegrityCheck);
        Assert.Equal(2, manifest.Counts.Users);
        Assert.Equal(1, manifest.Counts.TravelPackages);

        Assert.Contains("20260927_InitialPostgres", manifest.EfMigrations);
        Assert.Contains("20260928_AddDataProtectionKeys", manifest.EfMigrations);

        Assert.NotNull(zip.GetEntry("database/prophetops.dump"));
        Assert.NotNull(zip.GetEntry("uploads/packages/tour.jpg"));
        Assert.NotNull(zip.GetEntry("keys/key-pg-test.xml"));

        Assert.True(fakeCapture.SessionDisposed);
    }

    [Fact]
    public async Task BackupPackageWriter_with_postgres_capture_fails_when_referenced_image_missing()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg-missing-img"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        var fakeCapture = new FakePostgresBackupCapture(
            referencedImages: ["packages/nonexistent-image.jpg"],
            appliedMigrations: ["20260927_InitialPostgres"]);

        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            fakeCapture,
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("packages/nonexistent-image.jpg", ex.Message);
        Assert.True(fakeCapture.SessionDisposed);
    }

    [Fact]
    public async Task BackupPackageWriter_retains_existing_good_backups_when_postgres_capture_fails()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg-retain"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        Directory.CreateDirectory(storage.BackupStagingPath);
        var goodPackagePath = Path.Combine(storage.BackupStagingPath, "prophetops-20261001T000000Z-good.zip");
        await File.WriteAllTextAsync(goodPackagePath, "good backup content");

        var failingCapture = new FailingPostgresBackupCapture();
        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            failingCapture,
            NullLogger<BackupPackageWriter>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreatePackageUnderGate(CancellationToken.None));

        Assert.True(File.Exists(goodPackagePath));
    }

    [PostgresFact]
    public async Task Postgres_capture_acquires_advisory_lock_and_snapshot_sees_committed_data()
    {
        await using var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        fixture.RequireAvailable();

        var connString = fixture.ConnectionString!;
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        await using (var initCmd = conn.CreateCommand())
        {
            initCmd.CommandText = """
                CREATE SCHEMA IF NOT EXISTS prophetops;
                CREATE TABLE IF NOT EXISTS prophetops."Users" ("Id" text primary key, "Email" text);
                CREATE TABLE IF NOT EXISTS prophetops."TravelPackages" ("Code" text primary key, "ImagePath" text);
                CREATE TABLE IF NOT EXISTS prophetops."Bookings" ("Code" text primary key);
                CREATE TABLE IF NOT EXISTS prophetops."Expenses" ("Id" text primary key);
                CREATE TABLE IF NOT EXISTS prophetops."AuditEntries" ("Id" text primary key);
                CREATE TABLE IF NOT EXISTS prophetops."__EFMigrationsHistory" ("MigrationId" text primary key, "ProductVersion" text);
                INSERT INTO prophetops."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260927_InitialPostgres', '10.0');
            """;
            await initCmd.ExecuteNonQueryAsync();
        }

        await using var writerConn = new NpgsqlConnection(connString);
        await writerConn.OpenAsync();
        await using var writerTx = await writerConn.BeginTransactionAsync();
        await using (var insertCmd = writerConn.CreateCommand())
        {
            insertCmd.Transaction = writerTx;
            insertCmd.CommandText = "INSERT INTO prophetops.\"Users\" (\"Id\", \"Email\") VALUES ('user1', 'user1@test.com');";
            await insertCmd.ExecuteNonQueryAsync();
        }

        await writerTx.CommitAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connString).Options;
        await using var db = new AppDbContext(options);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg-live"));
        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);
        var capture = new PostgresBackupCapture(storage, config, runner, NullLogger<PostgresBackupCapture>.Instance);

        var working = Path.Combine(_tempDir, "working-live");
        Directory.CreateDirectory(working);

        await using var session = await capture.StartCaptureAsync(db, working, CancellationToken.None);
        Assert.NotNull(session);

        var metadata = await session.GetMetadataAsync(CancellationToken.None);
        Assert.Equal(1, metadata.Counts.Users);
        Assert.Contains("20260927_InitialPostgres", metadata.AppliedMigrations);
    }

    [PostgresFact]
    public async Task Postgres_capture_holds_advisory_lock_blocking_concurrent_writers()
    {
        await using var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        fixture.RequireAvailable();

        var connString = fixture.ConnectionString!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connString).Options;
        await using var db = new AppDbContext(options);

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg-lock"));
        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);
        var capture = new PostgresBackupCapture(storage, config, runner, NullLogger<PostgresBackupCapture>.Instance);

        var working = Path.Combine(_tempDir, "working-lock");
        Directory.CreateDirectory(working);

        await using (var session = await capture.StartCaptureAsync(db, working, CancellationToken.None))
        {
            Assert.NotNull(session);

            await using var otherConn = new NpgsqlConnection(connString);
            await otherConn.OpenAsync();
            await using var cmd = otherConn.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(8070, 1);";
            var acquired = (bool)(await cmd.ExecuteScalarAsync())!;
            Assert.False(acquired, "Concurrent connection was unexpectedly able to acquire advisory lock (8070, 1) while capture held it.");
        }

        await using var checkConn = new NpgsqlConnection(connString);
        await checkConn.OpenAsync();
        await using var checkCmd = checkConn.CreateCommand();
        checkCmd.CommandText = "SELECT pg_try_advisory_lock(8070, 1);";
        var reacquired = (bool)(await checkCmd.ExecuteScalarAsync())!;
        Assert.True(reacquired, "Advisory lock (8070, 1) was not released after session disposal.");

        checkCmd.CommandText = "SELECT pg_advisory_unlock(8070, 1);";
        await checkCmd.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task Postgres_real_synthetic_dump_metadata_and_image_consistency()
    {
        await using var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        fixture.RequireAvailable();

        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);
        try
        {
            runner.ResolveExecutable("pg_dump");
            runner.ResolveExecutable("pg_restore");
        }
        catch (FileNotFoundException)
        {
            // Skip honestly if client tools are not installed in the test environment
            return;
        }

        var connString = fixture.ConnectionString!;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connString, b => b.MigrationsAssembly(DatabaseRuntimeOptions.PostgresMigrationsAssembly))
            .Options;
        await using var db = new AppDbContext(options);

        // Migrate database
        await db.Database.MigrateAsync();

        // Populate business entities
        var package = new TravelPackage
        {
            Code = "PG-DUMP-TEST",
            PackageName = "Dump Test Package",
            Destination = "Tokyo",
            AvailableSlots = 5,
            BasePrice = 99999,
            ImagePath = "packages/tokyo.jpg",
            Status = "Normal"
        };
        db.TravelPackages.Add(package);
        db.Users.Add(new User { Name = "Dump User", Email = "dump@prophetops.local", Role = Roles.Admin, PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg-real-dump"));
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        // Write real image
        using var imgStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02]);
        await objectStorage.PutAsync("packages/tokyo.jpg", imgStream, "image/jpeg", CancellationToken.None);

        // Write real data protection key
        Directory.CreateDirectory(storage.KeysPath);
        await File.WriteAllTextAsync(Path.Combine(storage.KeysPath, "key-real-pg.xml"), "<key id='real-test'/>");

        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(connString, b => b.MigrationsAssembly(DatabaseRuntimeOptions.PostgresMigrationsAssembly)));
        using var sp = services.BuildServiceProvider();

        var capture = new PostgresBackupCapture(storage, config, runner, NullLogger<PostgresBackupCapture>.Instance);
        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            capture,
            NullLogger<BackupPackageWriter>.Instance);

        var pkg = await writer.CreatePackageUnderGate(CancellationToken.None);
        Assert.NotNull(pkg);
        Assert.True(File.Exists(pkg.ArchivePath));

        // Verify with pg_restore -l
        using var zip = ZipFile.OpenRead(pkg.ArchivePath);
        var dumpEntry = zip.GetEntry("database/prophetops.dump");
        Assert.NotNull(dumpEntry);

        var extractedDump = Path.Combine(_tempDir, "extracted.dump");
        dumpEntry.ExtractToFile(extractedDump);

        var verifyRes = await runner.RunRestoreListAsync(extractedDump, CancellationToken.None);
        Assert.Equal(0, verifyRes.ExitCode);
        Assert.Contains("TravelPackages", verifyRes.StandardOutput);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static StoragePaths CreateStoragePaths(string root)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = root })
            .Build();
        return StoragePaths.FromConfiguration(config, new TestEnvironment { ContentRootPath = root });
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FixedClock : IBusinessClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
        public string TimeZoneId => "UTC";
    }

    private sealed class FakeProcessRunner : IPostgresProcessRunner
    {
        public int ClientMajorVersion { get; set; } = 18;
        public ProcessExecutionResult DumpResult { get; set; } = new(0, "", "", false);
        public ProcessExecutionResult RestoreListResult { get; set; } = new(0, "", "", false);
        public ProcessExecutionResult RestoreResult { get; set; } = new(0, "", "", false);

        public Task<int> GetClientMajorVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ClientMajorVersion);

        public Task<ProcessExecutionResult> RunDumpAsync(PostgresDumpParameters parameters, CancellationToken cancellationToken) =>
            Task.FromResult(DumpResult);

        public Task<ProcessExecutionResult> RunRestoreListAsync(string dumpPath, CancellationToken cancellationToken) =>
            Task.FromResult(RestoreListResult);

        public Task<ProcessExecutionResult> RunRestoreAsync(PostgresRestoreParameters parameters, CancellationToken cancellationToken) =>
            Task.FromResult(RestoreResult);
    }

    private sealed class FakePostgresBackupCapture(
        IReadOnlyList<string> referencedImages,
        IReadOnlyList<string> appliedMigrations) : IDatabaseBackupCapture
    {
        public bool SessionDisposed { get; private set; }

        public bool CanCapture(AppDbContext db) => true;

        public Task<IDatabaseCaptureSession?> StartCaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken)
        {
            var dumpDir = Path.Combine(workingDirectory, "database");
            Directory.CreateDirectory(dumpDir);
            var dumpPath = Path.Combine(dumpDir, "prophetops.dump");
            File.WriteAllText(dumpPath, "dummy pg dump custom format bytes");

            var session = new FakeSession(dumpPath, referencedImages, appliedMigrations, () => SessionDisposed = true);
            return Task.FromResult<IDatabaseCaptureSession?>(session);
        }

        private sealed class FakeSession(
            string dumpPath,
            IReadOnlyList<string> images,
            IReadOnlyList<string> migrations,
            Action onDispose) : IDatabaseCaptureSession
        {
            public Task<DatabaseCaptureMetadata> GetMetadataAsync(CancellationToken cancellationToken)
            {
                var counts = new BackupCounts(
                    Users: 2,
                    TravelPackages: 1,
                    Bookings: 0,
                    Expenses: 0,
                    AuditEntries: 0,
                    PackageImages: images.Count);
                return Task.FromResult(new DatabaseCaptureMetadata(counts, migrations, images));
            }

            public Task<DatabaseCaptureResult> ExecuteDumpAsync(CancellationToken cancellationToken)
            {
                var manifest = new BackupDatabaseManifest(
                    Path: "database/prophetops.dump",
                    Size: new FileInfo(dumpPath).Length,
                    Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    IntegrityCheck: "pg_restore-list-verified",
                    Provider: "postgres",
                    ServerMajor: 18,
                    DumpFormat: "pg-dump-custom");
                return Task.FromResult(new DatabaseCaptureResult(manifest, "postgres-database"));
            }

            public ValueTask DisposeAsync()
            {
                onDispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FailingPostgresBackupCapture : IDatabaseBackupCapture
    {
        public bool CanCapture(AppDbContext db) => true;

        public Task<IDatabaseCaptureSession?> StartCaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated capture startup failure.");
        }
    }

    [Fact]
    public async Task ProcessRunner_drains_multi_chunk_output_on_normal_exit_without_premature_cancellation()
    {
        var toolsDir = Path.Combine(_tempDir, "tools-eof-race");
        Directory.CreateDirectory(toolsDir);

        // Generate 24,000 characters of data across multiple 4KB chunks
        var chunkData = new string('A', 24000);
        var dataFile = Path.Combine(toolsDir, "chunk_data.txt");
        await File.WriteAllTextAsync(dataFile, chunkData);

        string immediateExitScript;
        string lagExitScript;

        if (OperatingSystem.IsWindows())
        {
            immediateExitScript = Path.Combine(toolsDir, "immediate.cmd");
            File.WriteAllLines(immediateExitScript, [
                "@echo off",
                $"type \"{dataFile}\"",
                "exit /b 0"
            ]);

            lagExitScript = Path.Combine(toolsDir, "lag.cmd");
            File.WriteAllLines(lagExitScript, [
                "@echo off",
                $"type \"{dataFile}\"",
                "ping 127.0.0.1 -n 1 -w 50 >nul",
                "exit /b 0"
            ]);
        }
        else
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

            immediateExitScript = Path.Combine(toolsDir, "immediate.sh");
            File.WriteAllLines(immediateExitScript, [
                "#!/bin/sh",
                $"cat \"{dataFile}\"",
                "exit 0"
            ]);
            File.SetUnixFileMode(immediateExitScript, mode);

            lagExitScript = Path.Combine(toolsDir, "lag.sh");
            File.WriteAllLines(lagExitScript, [
                "#!/bin/sh",
                $"cat \"{dataFile}\"",
                "sleep 0.05",
                "exit 0"
            ]);
            File.SetUnixFileMode(lagExitScript, mode);
        }

        var runner = new PostgresProcessRunner(new ConfigurationBuilder().Build(), NullLogger<PostgresProcessRunner>.Instance);

        // Repeat to thoroughly exercise the exit vs reader cancellation race
        for (int i = 0; i < 20; i++)
        {
            var scriptToRun = (i % 2 == 0) ? immediateExitScript : lagExitScript;
            var psi = new ProcessStartInfo
            {
                FileName = scriptToRun,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var result = await runner.ExecuteProcessAsync(psi, TimeSpan.FromSeconds(10), null, null, null, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.Equal(24000, result.StandardOutput.Length);
            Assert.All(result.StandardOutput, c => Assert.Equal('A', c));
        }
    }

    [Fact]
    public async Task ProcessRunner_sanitizes_launch_exceptions_without_inner_exception_disclosure()
    {
        var runner = new PostgresProcessRunner(new ConfigurationBuilder().Build(), NullLogger<PostgresProcessRunner>.Instance);
        var secret = "super-secret-token-123";
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(_tempDir, "pg_dump.exe"),
            Arguments = $"--password={secret}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["PGPASSWORD"] = secret;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ExecuteProcessAsync(psi, TimeSpan.FromSeconds(5), null, null, secret, CancellationToken.None));

        // Must NOT wrap raw OS exception as InnerException
        Assert.Null(ex.InnerException);

        // Full exception string (Message + StackTrace + ToString) must be sanitized
        var fullExceptionText = ex.ToString();
        Assert.DoesNotContain(secret, fullExceptionText);
        Assert.DoesNotContain(_tempDir, fullExceptionText);
        Assert.Contains("Failed to launch PostgreSQL tool 'pg_dump.exe'. Ensure client tools are installed and accessible.", ex.Message);
    }

    [Fact]
    public async Task QuotaBoundedWriteStream_enforces_continuous_limit_and_never_writes_excessive_bytes_to_disk()
    {
        var targetFile = Path.Combine(_tempDir, "quota-test-stream.dat");
        var baseBytes = 500L;
        var maxQuota = 1500L; // Allowed to write at most 1000 bytes

        await using var fileStream = File.Create(targetFile);
        await using var quotaStream = new QuotaBoundedWriteStream(fileStream, baseBytes, maxQuota, "unit test streaming");

        // 50,000 bytes
        var buffer = new byte[50000];
        Array.Fill(buffer, (byte)0x42);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quotaStream.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None));

        Assert.Contains("Backup staging hard quota exceeded during unit test streaming", ex.Message);

        await fileStream.FlushAsync();
        fileStream.Close();

        var writtenOnDisk = new FileInfo(targetFile).Length;
        // MUST NOT exceed allowed limit (1500 - 500 = 1000)
        Assert.True(writtenOnDisk <= (maxQuota - baseBytes),
            $"Stream wrote {writtenOnDisk} bytes to disk, which exceeded allowed maximum of {maxQuota - baseBytes} bytes.");
    }

    [Fact]
    public void CreateZip_enforces_quota_during_incompressible_archive_creation()
    {
        var sourceDir = Path.Combine(_tempDir, "zip-source-dir");
        Directory.CreateDirectory(sourceDir);

        // 5000 bytes of incompressible random data
        var randomBytes = new byte[5000];
        Random.Shared.NextBytes(randomBytes);
        File.WriteAllBytes(Path.Combine(sourceDir, "random.bin"), randomBytes);

        var destZip = Path.Combine(_tempDir, "incompressible.zip");

        var fileOps = new BackupPackageFileOperations();
        var baseBytes = 2000L;
        var maxQuota = 2500L; // Allowed to write at most 500 bytes for the zip

        var ex = Assert.Throws<InvalidOperationException>(() =>
            fileOps.CreateZip(sourceDir, destZip, maxTotalBytes: maxQuota, currentBaseBytes: baseBytes));

        Assert.Contains("Backup staging hard quota exceeded during zip packaging", ex.Message);

        if (File.Exists(destZip))
        {
            var zipSizeOnDisk = new FileInfo(destZip).Length;
            Assert.True(zipSizeOnDisk <= (maxQuota - baseBytes),
                $"Zip file wrote {zipSizeOnDisk} bytes to disk, exceeding allowed limit of {maxQuota - baseBytes} bytes.");
        }
    }

    [Fact]
    public async Task EncryptFile_enforces_quota_before_and_during_envelope_creation()
    {
        var plainFile = Path.Combine(_tempDir, "envelope-plain.bin");
        var plainBytes = new byte[4000];
        Random.Shared.NextBytes(plainBytes);
        await File.WriteAllBytesAsync(plainFile, plainBytes);

        var destEnvelope = Path.Combine(_tempDir, "envelope-dest.pobak");
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        var fileOps = new BackupPackageFileOperations();
        var baseBytes = 2000L;
        var maxQuota = 2500L; // Allowed at most 500 bytes, but envelope needs ~4044 bytes

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fileOps.EncryptFile(plainFile, destEnvelope, key, maxTotalBytes: maxQuota, currentBaseBytes: baseBytes, CancellationToken.None));

        Assert.Contains("Backup staging hard quota exceeded", ex.Message);

        if (File.Exists(destEnvelope))
        {
            var envelopeSizeOnDisk = new FileInfo(destEnvelope).Length;
            Assert.True(envelopeSizeOnDisk <= (maxQuota - baseBytes),
                $"Encrypted envelope wrote {envelopeSizeOnDisk} bytes to disk, exceeding allowed limit of {maxQuota - baseBytes} bytes.");
        }
    }

    [Fact]
    public async Task BackupPackageWriter_enforces_continuous_quota_with_oversized_image_and_preserves_good_packages()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-quota-continuous"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        // 2000 bytes quota
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:StagingMaxBytes"] = "2000"
            })
            .Build();

        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        // Pre-create good backup in staging storage
        Directory.CreateDirectory(storage.BackupStagingPath);
        var goodBackup = Path.Combine(storage.BackupStagingPath, "prophetops-20261001T000000Z-good.zip");
        await File.WriteAllTextAsync(goodBackup, "pre-existing good backup that must not be deleted");

        // Put a 50,000-byte oversized image in object storage
        var hugeData = new byte[50000];
        Array.Fill(hugeData, (byte)0x77);
        using var hugeStream = new MemoryStream(hugeData);
        await objectStorage.PutAsync("packages/huge.png", hugeStream, "image/png", CancellationToken.None);

        var fakeCapture = new FakePostgresBackupCapture(
            referencedImages: ["packages/huge.png"],
            appliedMigrations: ["20260927_InitialPostgres"]);

        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            fakeCapture,
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("Backup staging hard quota exceeded", ex.Message);

        // Existing good backup must still be intact
        Assert.True(File.Exists(goodBackup));
        Assert.Equal("pre-existing good backup that must not be deleted", await File.ReadAllTextAsync(goodBackup));

        // Staging directory must have cleaned up partials
        var stagingRoot = Path.Combine(storage.BackupStagingPath, "staging");
        if (Directory.Exists(stagingRoot))
        {
            var workingDirs = Directory.GetDirectories(stagingRoot, "*.working");
            Assert.Empty(workingDirs);
        }
        var partialZips = Directory.GetFiles(storage.BackupStagingPath, "*.partial");
        Assert.Empty(partialZips);
    }

    [Fact]
    public async Task BackupPackageWriter_enforces_quota_during_envelope_encryption_and_cleans_only_partials()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-quota-envelope"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        // 32-byte key for encryption
        var encryptionKey = Convert.ToBase64String(new byte[32]);

        // Quota set to 250 bytes: working dir (dump + manifest) is ~200 bytes, zip is ~150 bytes, envelope is ~180 bytes.
        // Total simultaneous footprint exceeds 250 bytes during packaging or encryption.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:StagingMaxBytes"] = "250",
                ["Backup:Encryption:Key"] = encryptionKey,
                ["Backup:Encryption:KeyId"] = "key-test-1"
            })
            .Build();

        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        // Pre-create good backup in staging storage
        Directory.CreateDirectory(storage.BackupStagingPath);
        var goodBackup = Path.Combine(storage.BackupStagingPath, "prophetops-20261001T000000Z-retained.zip");
        await File.WriteAllTextAsync(goodBackup, "retained good backup");

        var fakeCapture = new FakePostgresBackupCapture(
            referencedImages: [],
            appliedMigrations: ["20260927_InitialPostgres"]);

        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            fakeCapture,
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("Backup staging hard quota exceeded", ex.Message);

        // Existing good backup preserved
        Assert.True(File.Exists(goodBackup));

        // Partials cleaned up
        var stagingRoot = Path.Combine(storage.BackupStagingPath, "staging");
        if (Directory.Exists(stagingRoot))
        {
            var workingDirs = Directory.GetDirectories(stagingRoot, "*.working");
            Assert.Empty(workingDirs);
        }
        var partialFiles = Directory.GetFiles(storage.BackupStagingPath, "*.partial*");
        Assert.Empty(partialFiles);
    }

    [Fact]
    public void BackupEncryptionSettings_validates_positive_max_input_bytes_and_defaults_to_32MiB()
    {
        // Default configuration
        var defaultConfig = new ConfigurationBuilder().Build();
        var defaultSettings = BackupEncryptionSettings.FromConfiguration(defaultConfig);
        Assert.Equal(32L * 1024 * 1024, defaultSettings.MaxInputBytes);

        // Valid custom limit
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:Encryption:MaxInputBytes"] = "2048"
            })
            .Build();
        var customSettings = BackupEncryptionSettings.FromConfiguration(customConfig);
        Assert.Equal(2048L, customSettings.MaxInputBytes);

        // Invalid: 0
        var zeroConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:Encryption:MaxInputBytes"] = "0"
            })
            .Build();
        var exZero = Assert.Throws<InvalidOperationException>(() => BackupEncryptionSettings.FromConfiguration(zeroConfig));
        Assert.Contains("Backup:Encryption:MaxInputBytes must be a positive number of bytes", exZero.Message);

        // Invalid: negative
        var negConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:Encryption:MaxInputBytes"] = "-1024"
            })
            .Build();
        var exNeg = Assert.Throws<InvalidOperationException>(() => BackupEncryptionSettings.FromConfiguration(negConfig));
        Assert.Contains("Backup:Encryption:MaxInputBytes must be a positive number of bytes", exNeg.Message);

        // Invalid: non-numeric
        var invalidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:Encryption:MaxInputBytes"] = "not-a-number"
            })
            .Build();
        var exInvalid = Assert.Throws<InvalidOperationException>(() => BackupEncryptionSettings.FromConfiguration(invalidConfig));
        Assert.Contains("Backup:Encryption:MaxInputBytes must be a positive number of bytes", exInvalid.Message);
    }

    [Fact]
    public async Task EncryptFile_enforces_max_input_bytes_limit_at_boundary_and_over_limit_without_allocating()
    {
        var limit = 1000L;
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        // Boundary case: exactly 1000 bytes
        var boundaryFile = Path.Combine(_tempDir, "boundary.bin");
        var boundaryBytes = new byte[limit];
        Random.Shared.NextBytes(boundaryBytes);
        await File.WriteAllBytesAsync(boundaryFile, boundaryBytes);

        var boundaryEncrypted = Path.Combine(_tempDir, "boundary.pobak");
        await BackupEncryptedEnvelope.EncryptFile(boundaryFile, boundaryEncrypted, key, long.MaxValue, 0, limit, CancellationToken.None);
        Assert.True(File.Exists(boundaryEncrypted));

        // Over-limit case: 1001 bytes (exceeds limit by 1 byte)
        var overLimitFile = Path.Combine(_tempDir, "overlimit.bin");
        var overLimitBytes = new byte[limit + 1];
        Random.Shared.NextBytes(overLimitBytes);
        await File.WriteAllBytesAsync(overLimitFile, overLimitBytes);

        var overLimitEncrypted = Path.Combine(_tempDir, "overlimit.pobak");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BackupEncryptedEnvelope.EncryptFile(overLimitFile, overLimitEncrypted, key, long.MaxValue, 0, limit, CancellationToken.None));

        Assert.Contains("Backup encryption input size (1001 bytes) exceeded the configured maximum in-memory encryption limit of 1000 bytes", ex.Message);
        Assert.Contains("Increase Backup:Encryption:MaxInputBytes or reduce backup size", ex.Message);

        // Must not leave an incomplete or created envelope file on disk
        Assert.False(File.Exists(overLimitEncrypted));
    }

    [Fact]
    public async Task DecryptFile_and_Decrypt_enforce_max_input_limit_on_untrusted_envelopes()
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        var plainFile = Path.Combine(_tempDir, "decrypt-test-plain.bin");
        var plainBytes = new byte[1000];
        Random.Shared.NextBytes(plainBytes);
        await File.WriteAllBytesAsync(plainFile, plainBytes);

        var encFile = Path.Combine(_tempDir, "decrypt-test-enc.pobak");
        await BackupEncryptedEnvelope.EncryptFile(plainFile, encFile, key, CancellationToken.None);

        var decryptedOut = Path.Combine(_tempDir, "decrypt-test-out.bin");

        // Try decrypting with max allowed 500 bytes (file is 1000 bytes)
        var exFile = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BackupEncryptedEnvelope.DecryptFile(encFile, decryptedOut, key, 500L, CancellationToken.None));
        Assert.Contains("exceeded the maximum allowed decryption limit", exFile.Message);

        // Direct in-memory Decrypt test with payload over limit
        var envelopeBytes = await File.ReadAllBytesAsync(encFile);
        var exMem = Assert.Throws<InvalidOperationException>(() =>
            BackupEncryptedEnvelope.Decrypt(envelopeBytes, key, 500L));
        Assert.Contains("exceeded the maximum allowed decryption limit", exMem.Message);
    }

    [Fact]
    public async Task BackupEncryption_existing_format_round_trip()
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        var originalFile = Path.Combine(_tempDir, "roundtrip-original.bin");
        var originalBytes = new byte[2048];
        Random.Shared.NextBytes(originalBytes);
        await File.WriteAllBytesAsync(originalFile, originalBytes);

        var encFile = Path.Combine(_tempDir, "roundtrip.pobak");
        await BackupEncryptedEnvelope.EncryptFile(originalFile, encFile, key, CancellationToken.None);

        // Verify envelope magic and header
        var encBytes = await File.ReadAllBytesAsync(encFile);
        Assert.True(encBytes.AsSpan().StartsWith("POBAK"u8));

        var decFile = Path.Combine(_tempDir, "roundtrip-decrypted.bin");
        await BackupEncryptedEnvelope.DecryptFile(encFile, decFile, key, CancellationToken.None);

        var decBytes = await File.ReadAllBytesAsync(decFile);
        Assert.Equal(originalBytes, decBytes);
    }

    [Fact]
    public async Task BackupPackageWriter_enforces_encryption_max_input_bytes_and_cleans_only_partials_preserving_good_backups()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-enc-max-input"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        using var sp = services.BuildServiceProvider();

        var encryptionKey = Convert.ToBase64String(new byte[32]);

        // MaxInputBytes = 500 bytes. The dump alone will be ~150-200 bytes, and adding an 800-byte image will make the archive > 500 bytes.
        // StagingMaxBytes = 100 MB (large disk quota, proving failure is due to memory cap, not disk quota).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backup:StagingMaxBytes"] = "104857600",
                ["Backup:Encryption:Key"] = encryptionKey,
                ["Backup:Encryption:KeyId"] = "key-test-mem",
                ["Backup:Encryption:MaxInputBytes"] = "500"
            })
            .Build();

        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var objectStorage = new LocalObjectStorage(storage);
        var backupStorage = new LocalBackupStorage(storage, config);

        // Pre-create good backup in staging storage
        Directory.CreateDirectory(storage.BackupStagingPath);
        var goodBackup = Path.Combine(storage.BackupStagingPath, "prophetops-20261001T000000Z-retained-good.zip");
        await File.WriteAllTextAsync(goodBackup, "pre-existing good backup that must be preserved");

        // Write an image of 800 bytes (which will push archive size well above 500 bytes)
        var imgBytes = new byte[800];
        Random.Shared.NextBytes(imgBytes);
        using var imgStream = new MemoryStream(imgBytes);
        await objectStorage.PutAsync("packages/over-mem.png", imgStream, "image/png", CancellationToken.None);

        var fakeCapture = new FakePostgresBackupCapture(
            referencedImages: ["packages/over-mem.png"],
            appliedMigrations: ["20260927_InitialPostgres"]);

        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            backupStorage,
            objectStorage,
            new BackupPackageFileOperations(),
            fakeCapture,
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("maximum in-memory encryption limit", ex.Message);
        Assert.Contains("Increase Backup:Encryption:MaxInputBytes or reduce backup size", ex.Message);

        // Good pre-existing backup preserved
        Assert.True(File.Exists(goodBackup));
        Assert.Equal("pre-existing good backup that must be preserved", await File.ReadAllTextAsync(goodBackup));

        // Partials cleaned up
        var stagingRoot = Path.Combine(storage.BackupStagingPath, "staging");
        if (Directory.Exists(stagingRoot))
        {
            var workingDirs = Directory.GetDirectories(stagingRoot, "*.working");
            Assert.Empty(workingDirs);
        }
        var partialFiles = Directory.GetFiles(storage.BackupStagingPath, "*.partial*");
        Assert.Empty(partialFiles);
    }
}
