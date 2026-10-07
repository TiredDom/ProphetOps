using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ProphetOps.Api;
using ProphetOps.Data;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BackupManifestTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "prophetops-manifest-tests-" + Guid.NewGuid().ToString("N"));

    public BackupManifestTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Manifest_parsing_accepts_valid_v1_manifest_and_infers_sqlite_provider()
    {
        const string json = """
        {
            "SchemaVersion": 1,
            "PackageId": "prophetops-20261003T100000Z-test",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.db",
                "Size": 1024,
                "Sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "IntegrityCheck": "ok"
            },
            "Files": [],
            "Counts": { "Users": 1, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        Assert.Equal(1, manifest.SchemaVersion);
        manifest.Validate();
        Assert.Equal("sqlite", manifest.ResolveDatabaseProvider());
    }

    [Fact]
    public void Manifest_parsing_accepts_valid_v2_manifest_with_postgres_provider()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "prophetops-20261003T100000Z-pgtest",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Production",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "aes-256-gcm-envelope", "KeyId": "op-key-1", "EnvelopeVersion": "1" },
            "Database": {
                "Path": "database/prophetops.dump",
                "Size": 2048,
                "Sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "IntegrityCheck": "pg_restore-list-verified",
                "Provider": "postgres",
                "ServerMajor": 18,
                "DumpFormat": "pg-dump-custom"
            },
            "Files": [],
            "Counts": { "Users": 2, "TravelPackages": 5, "Bookings": 10, "Expenses": 3, "AuditEntries": 20, "PackageImages": 5 },
            "Configuration": [],
            "EfMigrations": ["20260927000000_InitialPostgres"]
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.SchemaVersion);
        manifest.Validate();
        Assert.Equal("postgres", manifest.ResolveDatabaseProvider());
        Assert.Equal(18, manifest.Database.ServerMajor);
        Assert.Equal("pg-dump-custom", manifest.Database.DumpFormat);
    }

    [Fact]
    public void Manifest_parsing_accepts_valid_v2_manifest_with_sqlite_provider()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "prophetops-20261003T100000Z-sqlitetest",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.db",
                "Size": 1024,
                "Sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "IntegrityCheck": "ok",
                "Provider": "sqlite",
                "DumpFormat": "sqlite-file"
            },
            "Files": [],
            "Counts": { "Users": 1, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        manifest.Validate();
        Assert.Equal("sqlite", manifest.ResolveDatabaseProvider());
    }

    [Fact]
    public void Manifest_parsing_rejects_schema_version_less_than_1()
    {
        const string json = """
        {
            "SchemaVersion": 0,
            "PackageId": "invalid-version",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": { "Path": "database/prophetops.db", "Size": 1024, "Sha256": "abc", "IntegrityCheck": "ok" },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("Unsupported backup manifest schema version: 0", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_schema_version_greater_than_2()
    {
        const string json = """
        {
            "SchemaVersion": 3,
            "PackageId": "future-version",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": { "Path": "database/prophetops.db", "Size": 1024, "Sha256": "abc", "IntegrityCheck": "ok" },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("Unsupported backup manifest schema version: 3", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_v1_manifest_with_postgres_provider()
    {
        const string json = """
        {
            "SchemaVersion": 1,
            "PackageId": "v1-pg-mismatch",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.dump",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "postgres"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("PostgreSQL backup packages require manifest SchemaVersion 2", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_invalid_sqlite_dump_format()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "invalid-sqlite-format",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.db",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "sqlite",
                "DumpFormat": "pg-dump-custom"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("Invalid dump format 'pg-dump-custom' for SQLite provider", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_invalid_postgres_dump_format()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "invalid-pg-format",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.dump",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "postgres",
                "DumpFormat": "sqlite-file"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("Invalid dump format 'sqlite-file' for PostgreSQL provider", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_unsupported_database_provider()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "unsupported-provider",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.sql",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "oracle"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("Unsupported database provider in manifest: 'oracle'", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_v2_manifest_with_missing_provider()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "v2-missing-provider",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.db",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "DumpFormat": "sqlite-file"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("Manifest SchemaVersion 2 requires an explicit database provider", ex.Message);
        var ex2 = Assert.Throws<InvalidOperationException>(() => manifest.ResolveDatabaseProvider());
        Assert.Contains("Manifest SchemaVersion 2 requires an explicit database provider", ex2.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_v2_manifest_with_missing_dump_format_for_sqlite()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "v2-missing-dumpformat-sqlite",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.db",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "sqlite"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("SchemaVersion 2 manifests require a non-empty DumpFormat", ex.Message);
    }

    [Fact]
    public void Manifest_parsing_rejects_v2_manifest_with_missing_dump_format_for_postgres()
    {
        const string json = """
        {
            "SchemaVersion": 2,
            "PackageId": "v2-missing-dumpformat-pg",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.dump",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "postgres",
                "ServerMajor": 18
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("SchemaVersion 2 manifests require a non-empty DumpFormat", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Manifest_parsing_rejects_v2_postgres_manifest_with_invalid_server_major(int? serverMajor)
    {
        var serverMajorJson = serverMajor.HasValue ? serverMajor.Value.ToString() : "null";
        var json = $$"""
        {
            "SchemaVersion": 2,
            "PackageId": "v2-invalid-server-major",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": {
                "Path": "database/prophetops.dump",
                "Size": 1024,
                "Sha256": "abc",
                "IntegrityCheck": "ok",
                "Provider": "postgres",
                "ServerMajor": {{serverMajorJson}},
                "DumpFormat": "pg-dump-custom"
            },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
        Assert.NotNull(manifest);
        var ex = Assert.Throws<InvalidOperationException>(() => manifest.Validate());
        Assert.Contains("ServerMajor", ex.Message);
    }

    [Fact]
    public async Task SqliteBackupCapture_extracts_sqlite_file_and_verifies_integrity()
    {
        var dbPath = Path.Combine(_tempDir, "source.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage"));
        var capture = new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance);

        await using var testDb = new AppDbContext(options);
        var working = Path.Combine(_tempDir, "working");
        Directory.CreateDirectory(working);

        var result = await capture.CaptureAsync(testDb, working, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("sqlite-database", result.FileRole);
        Assert.Equal("database/prophetops.db", result.DatabaseManifest.Path);
        Assert.Equal("ok", result.DatabaseManifest.IntegrityCheck);
        Assert.True(File.Exists(Path.Combine(working, "database", "prophetops.db")));
    }

    [Fact]
    public async Task SqliteBackupCapture_returns_null_for_in_memory_sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage"));
        var capture = new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance);
        var working = Path.Combine(_tempDir, "working");

        var result = await capture.CaptureAsync(db, working, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task SqliteBackupCapture_rejects_non_sqlite_database_without_routing_to_sqlite_connection_builder()
    {
        // Simulate a PostgreSQL connection string that would be invalid or misrouted in SQLite
        var pgConnectionString = "Host=postgres.local;Database=prophetops;Username=prophet_app;Password=secret;";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(pgConnectionString).Options;
        await using var db = new AppDbContext(options);

        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage"));
        var capture = new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance);
        var working = Path.Combine(_tempDir, "working");

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => capture.CaptureAsync(db, working, CancellationToken.None));
        Assert.Contains("PostgreSQL backup capture is not supported by SqliteBackupCapture", ex.Message);
    }

    [Fact]
    public async Task BackupPackageWriter_rejects_postgres_database_explicitly()
    {
        var services = new ServiceCollection();
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-pg"));
        services.AddSingleton(storage);
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql("Host=localhost;Database=prophetops;Username=prophet_app;Password=secret;"));
        var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder().Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            new LocalBackupStorage(storage, config),
            new LocalObjectStorage(storage),
            new BackupPackageFileOperations(),
            new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance),
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("Backup capture is not supported for provider", ex.Message);
    }

    [Fact]
    public async Task BackupPackageWriter_emits_schema_version_1_and_null_provider_for_sqlite()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-sqlite"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        var dbPath = storage.DatabasePath;
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(connectionString));
        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var config = new ConfigurationBuilder().Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            new LocalBackupStorage(storage, config),
            new LocalObjectStorage(storage),
            new BackupPackageFileOperations(),
            new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance),
            NullLogger<BackupPackageWriter>.Instance);

        var package = await writer.CreatePackageUnderGate(CancellationToken.None);
        Assert.NotNull(package);
        Assert.Equal(1, package.Manifest.SchemaVersion);
        Assert.Null(package.Manifest.Database.Provider);
        Assert.Equal("sqlite", package.Manifest.ResolveDatabaseProvider());
    }

    [Fact]
    public async Task BackupPackageWriter_validates_manifest_before_serializing_package()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-invalid-manifest"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        var dbPath = storage.DatabasePath;
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(connectionString));
        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var config = new ConfigurationBuilder().Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var writer = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            new LocalBackupStorage(storage, config),
            new LocalObjectStorage(storage),
            new BackupPackageFileOperations(),
            new InvalidCapture(),
            NullLogger<BackupPackageWriter>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.CreatePackageUnderGate(CancellationToken.None));
        Assert.Contains("Invalid dump format 'invalid-format' for SQLite provider", ex.Message);
    }

    [Fact]
    public async Task PruneAfterVerifiedUpload_retains_quarantined_images_fail_safe_when_retained_manifest_fails_validation()
    {
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-quarantine-failsafe"));
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        var dbPath = storage.DatabasePath;
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(connectionString));
        var sp = services.BuildServiceProvider();

        // 1. Stage a quarantined image
        var quarantineDir = Path.Combine(storage.PackageImagesPath, ".quarantine");
        Directory.CreateDirectory(quarantineDir);
        var quarantinedFile = Path.Combine(quarantineDir, "evidence-keep.png");
        await File.WriteAllTextAsync(quarantinedFile, "quarantined-evidence");

        // 2. Put an invalid backup package into local backup storage
        var backupDir = Path.Combine(storage.BackupStagingPath, "independent");
        Directory.CreateDirectory(backupDir);
        var invalidPackageZip = Path.Combine(backupDir, "invalid-pkg.prophetops-backup.zip");

        const string invalidManifestJson = """
        {
            "SchemaVersion": 99,
            "PackageId": "invalid-schema-99",
            "CreatedUtc": "2026-10-03T10:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Mode": "none-local-test-only" },
            "Database": { "Path": "database/prophetops.db", "Size": 100, "Sha256": "abc", "IntegrityCheck": "ok" },
            "Files": [],
            "Counts": { "Users": 0, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "PackageImages": 0 },
            "Configuration": [],
            "EfMigrations": []
        }
        """;

        using (var zip = ZipFile.Open(invalidPackageZip, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("manifest.json");
            using var entryStream = entry.Open();
            using var entryWriter = new StreamWriter(entryStream);
            await entryWriter.WriteAsync(invalidManifestJson);
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backup:Keep"] = "1"
        }).Build();
        var hostEnvironment = new TestEnvironment { ContentRootPath = storage.Root };
        var packageWriter = new BackupPackageWriter(
            sp.GetRequiredService<IServiceScopeFactory>(),
            storage,
            config,
            hostEnvironment,
            new FixedClock(),
            new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance),
            new LocalBackupStorage(storage, config),
            new LocalObjectStorage(storage),
            new BackupPackageFileOperations(),
            new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance),
            NullLogger<BackupPackageWriter>.Instance);

        // Act: consumer path execution
        await packageWriter.PruneAfterVerifiedUpload(CancellationToken.None);

        // Assert: Quarantined evidence must NOT have been pruned (fail-safe retention)
        Assert.True(File.Exists(quarantinedFile), "Quarantined images must be preserved when retained backup package manifest fails validation.");
    }

    [Fact]
    public void DI_resolves_BackupPackageWriter_with_IDatabaseBackupCapture_safely()
    {
        var services = new ServiceCollection();
        var storage = CreateStoragePaths(Path.Combine(_tempDir, "storage-di"));
        services.AddSingleton(storage);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new TestEnvironment { ContentRootPath = storage.Root });
        services.AddSingleton<IBusinessClock>(new FixedClock());
        services.AddSingleton(new MaintenanceGate(TimeProvider.System, NullLogger<MaintenanceGate>.Instance));
        services.AddSingleton<IBackupStorage>(sp => new LocalBackupStorage(storage, sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IObjectStorage>(new LocalObjectStorage(storage));
        services.AddSingleton<IBackupPackageFileOperations>(new BackupPackageFileOperations());
        services.AddScoped<IDatabaseBackupCapture, SqliteBackupCapture>();
        services.AddLogging();
        services.AddScoped<BackupPackageWriter>();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<BackupPackageWriter>();
        Assert.NotNull(writer);
    }

    private sealed class InvalidCapture : IDatabaseBackupCapture
    {
        public bool CanCapture(AppDbContext db) => true;

        public Task<IDatabaseCaptureSession?> StartCaptureAsync(AppDbContext db, string workingDirectory, CancellationToken cancellationToken)
        {
            var dbDir = Path.Combine(workingDirectory, "database");
            Directory.CreateDirectory(dbDir);
            File.WriteAllText(Path.Combine(dbDir, "prophetops.db"), "content");
            var invalidManifest = new BackupDatabaseManifest(
                "database/prophetops.db", 7, "abc", "ok",
                Provider: "sqlite", DumpFormat: "invalid-format");
            IDatabaseCaptureSession session = new InvalidSession(invalidManifest);
            return Task.FromResult<IDatabaseCaptureSession?>(session);
        }

        private sealed class InvalidSession(BackupDatabaseManifest manifest) : IDatabaseCaptureSession
        {
            public Task<DatabaseCaptureMetadata> GetMetadataAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new DatabaseCaptureMetadata(new BackupCounts(0, 0, 0, 0, 0, 0), [], []));

            public Task<DatabaseCaptureResult> ExecuteDumpAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new DatabaseCaptureResult(manifest, "sqlite-database"));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock : IBusinessClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
        public string TimeZoneId => "UTC";
    }

    private static StoragePaths CreateStoragePaths(string root)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = root })
            .Build();
        return StoragePaths.FromConfiguration(config, new TestEnvironment { ContentRootPath = root });
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

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
