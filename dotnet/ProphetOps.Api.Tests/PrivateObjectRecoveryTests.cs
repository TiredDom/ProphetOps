using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class PrivateObjectRecoveryTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "prophetops-recovery-test-" + Guid.NewGuid().ToString("N"));
    private readonly SqliteConnection _dbConnection;
    private readonly AppDbContext _db;

    public PrivateObjectRecoveryTests()
    {
        Directory.CreateDirectory(_testDir);
        _dbConnection = new SqliteConnection("Data Source=:memory:");
        _dbConnection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_dbConnection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try { _db.Dispose(); } catch { }
        try { _dbConnection.Dispose(); } catch { }
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Recovery_hosted_supabase_s3_fails_closed_before_destination_writes()
    {
        // Upstream Supabase S3ProtocolHandler unconditionally enables isUpsert: true and does not enforce IfNoneMatch.
        // Recovery must fail closed before any destination write.
        var staging = CreateStagingEnvironment("pkg-s3-failclosed", "sqlite");
        var imgBytes = "HOSTED-S3-IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/hero.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-101",
            PackageName = "Hero Tour",
            ImagePath = "packages/hero.png",
        });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = CreateS3Config("test-bucket", "recovery-prefix/");
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/recovery-prefix/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "recovery-prefix/",
            ConfirmProject: "fake");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.ExclusiveCreateUnsupported, result.FailureCategory);
        Assert.Contains("Hosted S3 recovery is blocked", result.Message);
        Assert.Contains("isUpsert: true", result.Message);
        Assert.Empty(fakeStorage.Objects);
    }

    [Fact]
    public async Task Fail_closed_on_hosted_s3_alias_before_destination_write()
    {
        var staging = CreateStagingEnvironment("pkg-s3-alias", "sqlite");
        var imgBytes = "S3-ALIAS-IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/hero.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-S3",
            PackageName = "S3 Tour",
            ImagePath = "packages/hero.png",
        });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        // Provider configured as 's3' instead of 'supabase-s3'
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "s3",
                ["ObjectStorage:S3:Endpoint"] = "https://fake.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:S3:AccessKeyId"] = "key",
                ["ObjectStorage:S3:SecretAccessKey"] = "secret",
                ["Storage:Root"] = staging.RootDir,
                ["Storage:AllowContentRootFallback"] = "true",
            })
            .Build();

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "fake");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.ExclusiveCreateUnsupported, result.FailureCategory);
        Assert.Contains("Hosted S3 recovery is blocked", result.Message);
        Assert.Empty(fakeStorage.Objects);
    }

    [Fact]
    public async Task Hosted_s3_recovery_succeeds_with_native_recovery_creator_and_verified_s3_readback()
    {
        var staging = CreateStagingEnvironment("pkg-native-success", "sqlite");
        var imgBytes = "NATIVE-IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/hero.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-NATIVE",
            PackageName = "Native Tour",
            ImagePath = "packages/hero.png",
        });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var creator = new FakeNativeRecoveryObjectCreator((key, stream, contentType, len) =>
        {
            // Simulate native REST upload storing into fakeStorage for S3 readback
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            fakeStorage.Store(key, ms.ToArray(), contentType);
            return Task.FromResult(true); // Created
        });

        var config = CreateS3Config("test-bucket", "rec/", staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "fake");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.ExpectedObjectCount);
        Assert.Equal(1, result.VerifiedObjectCount);
        Assert.Single(fakeStorage.Objects);
    }

    [Fact]
    public async Task Hosted_s3_recovery_accepts_existing_identical_object_on_native_retry()
    {
        var staging = CreateStagingEnvironment("pkg-native-retry", "sqlite");
        var imgBytes = "IDENTICAL-IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/hero.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-RETRY",
            PackageName = "Retry Tour",
            ImagePath = "packages/hero.png",
        });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        fakeStorage.Store("packages/hero.png", imgBytes, "image/png");

        var creator = new FakeNativeRecoveryObjectCreator((key, stream, contentType, len) =>
        {
            return Task.FromResult(false); // 409 Duplicate detected
        });

        var config = CreateS3Config("test-bucket", "rec/", staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "fake");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.ExpectedObjectCount);
        Assert.Equal(1, result.VerifiedObjectCount);
    }

    [Fact]
    public async Task Hosted_s3_recovery_fails_and_preserves_conflicting_differing_object()
    {
        var staging = CreateStagingEnvironment("pkg-native-conflict", "sqlite");
        var imgBytes = "NEW-IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/hero.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-CONFLICT",
            PackageName = "Conflict Tour",
            ImagePath = "packages/hero.png",
        });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var conflictingBytes = "EXISTING-DIFFERENT-BYTES"u8.ToArray();
        fakeStorage.Store("packages/hero.png", conflictingBytes, "image/png");

        var creator = new FakeNativeRecoveryObjectCreator((key, stream, contentType, len) =>
        {
            return Task.FromResult(false); // 409 Duplicate detected
        });

        var config = CreateS3Config("test-bucket", "rec/", staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "fake");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.ConditionalCreateConflict, result.FailureCategory);
        Assert.Equal(conflictingBytes, fakeStorage.Objects["packages/hero.png"].Bytes); // Preserved unmodified
    }

    [Fact]
    public async Task Recovery_preserves_existing_destination_object_when_provider_ignores_conditional_create()
    {
        // 1. For hosted Supabase S3: writes are completely blocked, so existing objects cannot be overwritten
        var staging = CreateStagingEnvironment("pkg-preserve", "sqlite");
        var imgBytes = "NEW-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/item.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "ITM", PackageName = "Item", ImagePath = "packages/item.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        // Pre-seed an existing object with different content
        var existingBytes = "EXISTING-DIFFERENT-DATA"u8.ToArray();
        fakeStorage.Store("packages/item.png", existingBytes, "image/png");

        var s3Config = CreateS3Config("test-bucket", "rec/");
        var runner = new PrivateObjectRecoveryRunner(_db, s3Config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions("supabase-s3:test-bucket/rec/", StagingManifestPath: staging.ManifestPath, RecoveryPrefix: "rec/", ConfirmProject: "fake"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.ExclusiveCreateUnsupported, result.FailureCategory);
        // Existing object remains untouched
        Assert.Equal(existingBytes, fakeStorage.Objects["packages/item.png"].Bytes);
    }

    [Fact]
    public async Task Recovery_redacts_credentials_from_database_and_storage_exceptions_in_logs_and_results()
    {
        var staging = CreateStagingEnvironment("pkg-redact", "sqlite");
        var imgBytes = "REDACT-BYTES"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/redact.png", imgBytes, imgHash);
        staging.WriteManifest();

        // Secret tokens that must never appear in logs or error messages
        var dbSecret = "DB_SUPER_SECRET_PASSWORD_98765";
        var storageSecret = "STORAGE_SECRET_TOKEN_43210";

        // Injected secret-bearing database failure
        _db.TravelPackages.Add(new TravelPackage { Code = "R", PackageName = "R", ImagePath = "packages/redact.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage
        {
            ThrowOnPut = new IOException($"Failed connecting to https://s3.supabase.test?auth={storageSecret}")
        };

        var config = CreateLocalConfig(staging.RootDir);
        var collectingLogger = new CollectingLogger<PrivateObjectRecoveryRunner>();
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, collectingLogger);

        var destination = $"local:{staging.StoragePaths.UploadsPath}";
        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions(destination, StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.StorageUnavailable, result.FailureCategory);
        Assert.DoesNotContain(storageSecret, result.Message);

        foreach (var logged in collectingLogger.LoggedMessages)
        {
            Assert.DoesNotContain(storageSecret, logged);
            Assert.DoesNotContain(dbSecret, logged);
        }
    }

    [Theory]
    [InlineData("{\"packageId\": \"truncated\"")] // Malformed JSON (truncated)
    [InlineData("{\"packageId\": \"pkg\", \"schemaVersion\": 99, \"databaseProvider\": \"sqlite\", \"stagedAtUtc\": \"2026-10-04T00:00:00Z\", \"status\": \"StagedOfflinePendingObjectStorageUpload\", \"images\": []}")] // Unsupported version
    [InlineData("{\"packageId\": \"\", \"schemaVersion\": 2, \"databaseProvider\": \"sqlite\", \"stagedAtUtc\": \"2026-10-04T00:00:00Z\", \"status\": \"StagedOfflinePendingObjectStorageUpload\", \"images\": []}")] // Empty packageId
    [InlineData("{\"packageId\": \"../escape\", \"schemaVersion\": 2, \"databaseProvider\": \"sqlite\", \"stagedAtUtc\": \"2026-10-04T00:00:00Z\", \"status\": \"StagedOfflinePendingObjectStorageUpload\", \"images\": []}")] // Invalid packageId characters
    [InlineData("{\"packageId\": \"pkg\", \"schemaVersion\": 2, \"databaseProvider\": \"sqlite\", \"stagedAtUtc\": \"2026-10-04T00:00:00Z\", \"status\": \"WrongStatus\", \"images\": []}")] // Unsupported status
    [InlineData("{\"packageId\": \"pkg\", \"schemaVersion\": 2, \"databaseProvider\": \"sqlite\", \"stagedAtUtc\": \"2026-10-04T00:00:00Z\", \"status\": \"StagedOfflinePendingObjectStorageUpload\", \"images\": [{\"databaseKey\": \"packages/a.png\", \"relativePath\": \"packages/a.png\", \"size\": -1, \"sha256\": \"0000000000000000000000000000000000000000000000000000000000000000\"}]}")] // Negative size
    [InlineData("{\"packageId\": \"pkg\", \"schemaVersion\": 2, \"databaseProvider\": \"sqlite\", \"stagedAtUtc\": \"2026-10-04T00:00:00Z\", \"status\": \"StagedOfflinePendingObjectStorageUpload\", \"images\": [{\"databaseKey\": \"packages/a.png\", \"relativePath\": \"packages/a.png\", \"size\": 10, \"sha256\": \"INVALID-SHA\"}]}")] // Invalid SHA-256 syntax
    [InlineData("[1, 2, 3]")] // Non-object JSON root
    public async Task Recovery_rejects_malformed_json_metadata(string malformedJson)
    {
        var stagingDir = Path.Combine(_testDir, "malformed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);
        var manifestPath = Path.Combine(stagingDir, "recovery-manifest.json");
        await File.WriteAllTextAsync(manifestPath, malformedJson);

        var config = CreateLocalConfig(stagingDir);
        var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = stagingDir });
        var runner = new PrivateObjectRecoveryRunner(_db, config, paths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{paths.UploadsPath}", StagingManifestPath: manifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_manifest_rejects_duplicate_json_properties()
    {
        var jsonWithDuplicates = """
{
  "packageId": "pkg-dup",
  "packageId": "pkg-dup-2",
  "schemaVersion": 2,
  "databaseProvider": "sqlite",
  "stagedAtUtc": "2026-10-04T00:00:00Z",
  "status": "StagedOfflinePendingObjectStorageUpload",
  "images": []
}
""";
        var stagingDir = Path.Combine(_testDir, "dup-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);
        var manifestPath = Path.Combine(stagingDir, "recovery-manifest.json");
        await File.WriteAllTextAsync(manifestPath, jsonWithDuplicates);

        var config = CreateLocalConfig(stagingDir);
        var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = stagingDir });
        var runner = new PrivateObjectRecoveryRunner(_db, config, paths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{paths.UploadsPath}", StagingManifestPath: manifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_manifest_rejects_case_aliased_json_properties()
    {
        var jsonWithCaseAlias = """
{
  "packageId": "pkg-alias",
  "PackageId": "pkg-alias-2",
  "schemaVersion": 2,
  "databaseProvider": "sqlite",
  "stagedAtUtc": "2026-10-04T00:00:00Z",
  "status": "StagedOfflinePendingObjectStorageUpload",
  "images": []
}
""";
        var stagingDir = Path.Combine(_testDir, "case-alias-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);
        var manifestPath = Path.Combine(stagingDir, "recovery-manifest.json");
        await File.WriteAllTextAsync(manifestPath, jsonWithCaseAlias);

        var config = CreateLocalConfig(stagingDir);
        var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = stagingDir });
        var runner = new PrivateObjectRecoveryRunner(_db, config, paths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{paths.UploadsPath}", StagingManifestPath: manifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_rejects_cross_provider_mismatch()
    {
        // Target DB is SQLite, but manifest declares Postgres
        var staging = CreateStagingEnvironment("pkg-cross-provider", "postgres");
        staging.WriteManifest();

        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata, result.FailureCategory);
        Assert.Contains("Failed to parse recovery manifest", result.Message);
    }

    [Theory]
    [InlineData("packages/photo.png", "packages/photo.png")] // Exact duplicate key
    [InlineData("packages/photo.png", "packages/PHOTO.png")] // Case-insensitive key collision
    [InlineData("packages\\backslash.png", "packages/other.png")] // Backslash in key
    [InlineData("/packages/rooted.png", "packages/other.png")] // Rooted slash in key
    [InlineData("C:/packages/drive.png", "packages/other.png")] // Rooted drive in key
    [InlineData("packages/../escaped.png", "packages/other.png")] // Traversal in key
    public async Task Recovery_rejects_path_and_key_aliasing(string key1, string key2)
    {
        var stagingDir = Path.Combine(_testDir, "alias-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);
        var manifestPath = Path.Combine(stagingDir, "recovery-manifest.json");

        var manifest = new PrivateObjectRecoveryManifest(
            "pkg-alias",
            2,
            "sqlite",
            DateTimeOffset.UtcNow,
            "StagedOfflinePendingObjectStorageUpload",
            [
                new RecoverableImageRecord(key1, "packages/p1.png", 10, "0000000000000000000000000000000000000000000000000000000000000001"),
                new RecoverableImageRecord(key2, "packages/p2.png", 10, "0000000000000000000000000000000000000000000000000000000000000002")
            ]);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));

        var config = CreateLocalConfig(stagingDir);
        var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = stagingDir });
        var runner = new PrivateObjectRecoveryRunner(_db, config, paths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{paths.UploadsPath}", StagingManifestPath: manifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_requires_exact_match_with_database_references()
    {
        var hash = "0000000000000000000000000000000000000000000000000000000000000001";

        // Case A: Database has packages/p1.png, manifest has packages/p2.png
        var stagingDirA = Path.Combine(_testDir, "db-match-a");
        Directory.CreateDirectory(stagingDirA);
        var manifestPathA = Path.Combine(stagingDirA, "recovery-manifest.json");
        var manifestA = new PrivateObjectRecoveryManifest("pkg-a", 2, "sqlite", DateTimeOffset.UtcNow, "StagedOfflinePendingObjectStorageUpload",
            [new RecoverableImageRecord("packages/p2.png", "packages/p2.png", 10, hash)]);
        await File.WriteAllTextAsync(manifestPathA, JsonSerializer.Serialize(manifestA));

        _db.TravelPackages.Add(new TravelPackage { Code = "A", PackageName = "A", ImagePath = "packages/p1.png" });
        await _db.SaveChangesAsync();

        var configA = CreateLocalConfig(stagingDirA);
        var pathsA = StoragePaths.FromConfiguration(configA, new MockHostEnvironment { ContentRootPath = stagingDirA });
        var runnerA = new PrivateObjectRecoveryRunner(_db, configA, pathsA, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var resultA = await runnerA.RunAsync(new PrivateObjectRecoveryOptions($"local:{pathsA.UploadsPath}", StagingManifestPath: manifestPathA), CancellationToken.None);

        Assert.False(resultA.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DatabaseReferenceMismatch, resultA.FailureCategory);
        Assert.Contains("missing from the validated recovery set", resultA.Message);

        // Case B: Manifest has extra image packages/extra.png not referenced in database
        var stagingDirB = Path.Combine(_testDir, "db-match-b");
        Directory.CreateDirectory(stagingDirB);
        var manifestPathB = Path.Combine(stagingDirB, "recovery-manifest.json");
        var manifestB = new PrivateObjectRecoveryManifest("pkg-b", 2, "sqlite", DateTimeOffset.UtcNow, "StagedOfflinePendingObjectStorageUpload",
            [
                new RecoverableImageRecord("packages/p1.png", "packages/p1.png", 10, hash),
                new RecoverableImageRecord("packages/extra.png", "packages/extra.png", 10, hash)
            ]);
        await File.WriteAllTextAsync(manifestPathB, JsonSerializer.Serialize(manifestB));

        var configB = CreateLocalConfig(stagingDirB);
        var pathsB = StoragePaths.FromConfiguration(configB, new MockHostEnvironment { ContentRootPath = stagingDirB });
        var runnerB = new PrivateObjectRecoveryRunner(_db, configB, pathsB, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var resultB = await runnerB.RunAsync(new PrivateObjectRecoveryOptions($"local:{pathsB.UploadsPath}", StagingManifestPath: manifestPathB), CancellationToken.None);

        Assert.False(resultB.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DatabaseReferenceMismatch, resultB.FailureCategory);
        Assert.Contains("not referenced by any package", resultB.Message);
    }

    [Fact]
    public async Task Recovery_enforces_manifest_size_and_readback_stream_length_bounds()
    {
        // 1. Oversized manifest file (> 2 MB)
        var stagingDir = Path.Combine(_testDir, "bounds-test");
        Directory.CreateDirectory(stagingDir);
        var bigManifestPath = Path.Combine(stagingDir, "recovery-manifest.json");
        // Create 2.5 MB dummy file
        await using (var fs = new FileStream(bigManifestPath, FileMode.Create))
        {
            fs.SetLength(2500000);
        }

        var config = CreateLocalConfig(stagingDir);
        var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = stagingDir });
        var runner = new PrivateObjectRecoveryRunner(_db, config, paths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{paths.UploadsPath}", StagingManifestPath: bigManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata, result.FailureCategory);
        Assert.Contains("maximum permitted size", result.Message);
    }

    [Fact]
    public async Task Recovery_zero_images_validates_clean_staging_and_rejects_unexpected_files()
    {
        var staging = CreateStagingEnvironment("pkg-zero-dirty", "sqlite");
        staging.WriteManifest();

        // Drop an unreferenced file into staged-images/
        var unexpected = Path.Combine(staging.StagedImagesDir, "rogue.exe");
        await File.WriteAllTextAsync(unexpected, "MALWARE");

        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath, result.FailureCategory);
        Assert.Contains("Unexpected file", result.Message);

        // Remove rogue file: zero-image package now succeeds
        File.Delete(unexpected);
        var cleanResult = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);
        Assert.True(cleanResult.Success, cleanResult.Message);
        Assert.Equal(0, cleanResult.ExpectedObjectCount);
        Assert.Equal(0, cleanResult.VerifiedObjectCount);
    }

    [Fact]
    public async Task Recovery_local_object_storage_exclusive_creation_and_retry_readback()
    {
        var staging = CreateStagingEnvironment("pkg-local-excl", "sqlite");
        var imgBytes = "LOCAL-DISK-BYTES"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/local.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "LOC", PackageName = "Local", ImagePath = "packages/local.png" });
        await _db.SaveChangesAsync();

        var localUploadsDir = Path.Combine(_testDir, "local-destination-uploads");
        Directory.CreateDirectory(localUploadsDir);

        var config = CreateLocalConfig(Path.Combine(_testDir, "local-dest-root"));
        var storagePaths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = Path.Combine(_testDir, "local-dest-root") });
        var localStorage = new LocalObjectStorage(storagePaths);

        var runner = new PrivateObjectRecoveryRunner(_db, config, storagePaths, localStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var expectedDestination = $"local:{storagePaths.UploadsPath}";

        // First run: newly creates object
        var result1 = await runner.RunAsync(new PrivateObjectRecoveryOptions(expectedDestination, StagingManifestPath: staging.ManifestPath), CancellationToken.None);
        Assert.True(result1.Success, result1.Message);
        Assert.Equal(1, result1.VerifiedObjectCount);

        var destFilePath = Path.Combine(storagePaths.UploadsPath, "packages", "local.png");
        Assert.True(File.Exists(destFilePath));
        Assert.Equal(imgBytes, await File.ReadAllBytesAsync(destFilePath));

        // Second run (retry): verifies existing identical object on disk
        var result2 = await runner.RunAsync(new PrivateObjectRecoveryOptions(expectedDestination, StagingManifestPath: staging.ManifestPath), CancellationToken.None);
        Assert.True(result2.Success, result2.Message);
        Assert.Equal(1, result2.VerifiedObjectCount);
        Assert.Equal(imgBytes, await File.ReadAllBytesAsync(destFilePath));

        // Tamper with destination object to test conflict preservation
        await File.WriteAllTextAsync(destFilePath, "CORRUPTED-ON-DISK");
        var result3 = await runner.RunAsync(new PrivateObjectRecoveryOptions(expectedDestination, StagingManifestPath: staging.ManifestPath), CancellationToken.None);
        Assert.False(result3.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.ConditionalCreateConflict, result3.FailureCategory);
        // Preserved unmodified
        Assert.Equal("CORRUPTED-ON-DISK", await File.ReadAllTextAsync(destFilePath));
    }

    [Fact]
    public async Task Recovery_rejects_missing_staged_source_file()
    {
        var staging = CreateStagingEnvironment("pkg-102", "sqlite");
        staging.AddManifestEntryOnly("packages/missing.png", 100, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "P", PackageName = "P", ImagePath = "packages/missing.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath);
        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.MissingSourceFile, result.FailureCategory);
        Assert.Empty(fakeStorage.Objects);
    }

    [Fact]
    public async Task Recovery_rejects_non_canonical_database_reference()
    {
        var staging = CreateStagingEnvironment("pkg-noncanonical-db", "sqlite");
        var imgBytes = "IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/tour.png", imgBytes, imgHash);
        staging.WriteManifest();

        // Non-canonical DB reference containing traversal
        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-BAD-KEY",
            PackageName = "Bad Tour",
            ImagePath = "packages/../packages/tour.png"
        });
        await _db.SaveChangesAsync();

        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath, result.FailureCategory);
        Assert.Contains("Invalid database image reference key", result.Message);
    }

    [Fact]
    public async Task Recovery_rejects_symlink_or_reparse_point_at_staging_root()
    {
        if (!OperatingSystem.IsWindows()) return;

        var realDir = Path.Combine(_testDir, "real-staging-root");
        Directory.CreateDirectory(realDir);
        var linkDir = Path.Combine(_testDir, "link-staging-root");

        // Create junction linkDir -> realDir
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkDir}\" \"{realDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(5000);
        if (!Directory.Exists(linkDir)) return;

        var manifestPath = Path.Combine(linkDir, "recovery-manifest.json");
        await File.WriteAllTextAsync(manifestPath, """
{
  "packageId": "pkg-root-reparse",
  "schemaVersion": 2,
  "databaseProvider": "sqlite",
  "stagedAtUtc": "2026-10-04T00:00:00Z",
  "status": "StagedOfflinePendingObjectStorageUpload",
  "images": []
}
""");

        var config = CreateLocalConfig(realDir);
        var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = realDir });
        var runner = new PrivateObjectRecoveryRunner(_db, config, paths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{paths.UploadsPath}", StagingPath: linkDir, StagingManifestPath: manifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath, result.FailureCategory);
        Assert.Contains("reparse point", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Redaction_comprehensive_database_midstream_wrapper_and_manifest_errors()
    {
        const string dbSecret = "SUPER_SECRET_DB_PASSWORD_XYZ123";
        const string streamSecret = "SUPER_SECRET_STREAM_TOKEN_456";
        const string wrapperSecret = "SUPER_SECRET_STORAGE_KEY_789";
        const string manifestSecret = "SUPER_SECRET_MANIFEST_STRING_999";

        var staging = CreateStagingEnvironment("pkg-comprehensive-redact", "sqlite");
        var imgBytes = "IMAGE-DATA"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/photo.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "P", PackageName = "P", ImagePath = "packages/photo.png" });
        await _db.SaveChangesAsync();

        var logger = new CollectingLogger<PrivateObjectRecoveryRunner>();

        // 1. Storage wrapper error with embedded secret
        var failingStorage = new FakeRecoverableObjectStorage
        {
            ThrowOnPut = new InvalidOperationException($"AWS Auth Failed: {wrapperSecret}")
        };
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, failingStorage, logger);
        var res1 = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);
        Assert.False(res1.Success);
        Assert.DoesNotContain(wrapperSecret, res1.Message);

        // 2. Malformed manifest with secret token
        var malformedManifestPath = Path.Combine(staging.RootDir, "secret-manifest.json");
        await File.WriteAllTextAsync(malformedManifestPath, $"{{\"packageId\": \"{manifestSecret}\", \"schemaVersion\": INVALID_JSON}}");
        var res2 = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: malformedManifestPath), CancellationToken.None);
        Assert.False(res2.Success);
        Assert.DoesNotContain(manifestSecret, res2.Message);

        // Verify logger collected zero secrets
        foreach (var msg in logger.LoggedMessages)
        {
            Assert.DoesNotContain(dbSecret, msg);
            Assert.DoesNotContain(streamSecret, msg);
            Assert.DoesNotContain(wrapperSecret, msg);
            Assert.DoesNotContain(manifestSecret, msg);
        }
    }

    [Fact]
    public async Task Recovery_rejects_tampered_file_length()
    {
        var staging = CreateStagingEnvironment("pkg-103", "sqlite");
        var actualBytes = "SHORT"u8.ToArray();
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(actualBytes));
        staging.AddStagedImage("packages/tampered-len.png", actualBytes, actualHash, declaredSize: 5000);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "P", PackageName = "P", ImagePath = "packages/tampered-len.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath);
        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.TamperedOrCorruptedSourceFile, result.FailureCategory);
        Assert.Empty(fakeStorage.Objects);
    }

    [Fact]
    public async Task Recovery_rejects_tampered_file_sha256()
    {
        var staging = CreateStagingEnvironment("pkg-104", "sqlite");
        var actualBytes = "REAL-BYTES"u8.ToArray();
        staging.AddStagedImage("packages/tampered-hash.png", actualBytes, "0000000000000000000000000000000000000000000000000000000000000000");
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "P", PackageName = "P", ImagePath = "packages/tampered-hash.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath);
        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.TamperedOrCorruptedSourceFile, result.FailureCategory);
        Assert.Empty(fakeStorage.Objects);
    }

    [Theory]
    [InlineData("supabase-s3:wrong-bucket/rec/")]
    [InlineData("supabase-s3:test-bucket/wrong-prefix/")]
    [InlineData("local:C:\\uploads")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Recovery_rejects_wrong_destination_confirmation(string? wrongConfirmation)
    {
        var staging = CreateStagingEnvironment("pkg-108", "sqlite");
        staging.WriteManifest();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = CreateS3Config("test-bucket", "rec/");
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(wrongConfirmation, StagingManifestPath: staging.ManifestPath, RecoveryPrefix: "rec/", ConfirmProject: "fake");
        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch, result.FailureCategory);
        Assert.Contains("Destination confirmation mismatch", result.Message);
    }

    [Fact]
    public async Task Recovery_refuses_local_storage_fallback_in_hosted_mode()
    {
        var staging = CreateStagingEnvironment("pkg-fallback-hosted", "sqlite");
        staging.WriteManifest();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "local",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.UnsupportedProviderCombination, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_refuses_local_storage_fallback_when_postgres_provider_is_configured()
    {
        var staging = CreateStagingEnvironment("pkg-fallback-pg", "postgres");
        staging.WriteManifest();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "local",
                ["Database:Provider"] = "postgres",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);
        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.UnsupportedProviderCombination, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_fails_when_destination_readback_returns_null()
    {
        var staging = CreateStagingEnvironment("pkg-null-readback", "sqlite");
        var imgBytes = "BYTES"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/pic.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "P", PackageName = "P", ImagePath = "packages/pic.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage { FailReadbackNull = true };
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DestinationVerificationFailed, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_fails_when_destination_readback_hash_mismatches()
    {
        var staging = CreateStagingEnvironment("pkg-corrupt-readback", "sqlite");
        var imgBytes = "ORIGINAL"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/pic.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "P", PackageName = "P", ImagePath = "packages/pic.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage { CorruptReadbackBytes = "CORRUPTED-ON-READBACK"u8.ToArray() };
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DestinationVerificationFailed, result.FailureCategory);
    }

    [Fact]
    public async Task Recovery_handles_cancellation_gracefully()
    {
        var staging = CreateStagingEnvironment("pkg-cancel", "sqlite");
        var imgBytes = "DATA"u8.ToArray();
        staging.AddStagedImage("packages/cancel.png", imgBytes, Convert.ToHexStringLower(SHA256.HashData(imgBytes)));
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage { Code = "C", PackageName = "C", ImagePath = "packages/cancel.png" });
        await _db.SaveChangesAsync();

        var fakeStorage = new FakeRecoverableObjectStorage();
        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await runner.RunAsync(new PrivateObjectRecoveryOptions($"local:{staging.StoragePaths.UploadsPath}", StagingManifestPath: staging.ManifestPath), cts.Token);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.Cancelled, result.FailureCategory);
    }

    [Fact]
    public void Recovery_options_from_args_parses_all_arguments_correctly()
    {
        var args = new[]
        {
            "--restore-private-objects",
            "--confirm-destination", "supabase-s3:my-bucket/my-rec/",
            "--staging-dir", @"C:\staging\recovery",
            "--staging-manifest", @"C:\staging\recovery\recovery-manifest.json",
            "--recovery-prefix", "my-rec/",
            "--timeout-seconds", "120",
        };

        var config = new ConfigurationBuilder().Build();
        var options = PrivateObjectRecoveryOptions.FromArgs(args, config);

        Assert.Equal("supabase-s3:my-bucket/my-rec/", options.ConfirmDestination);
        Assert.Equal(@"C:\staging\recovery", options.StagingPath);
        Assert.Equal(@"C:\staging\recovery\recovery-manifest.json", options.StagingManifestPath);
        Assert.Equal("my-rec/", options.RecoveryPrefix);
        Assert.Equal(120, options.TimeoutSeconds);
    }

    [Fact]
    public async Task Program_offline_private_object_recovery_flag_runs_and_exits_without_starting_web_host()
    {
        var dir = Path.Combine(_testDir, "program-flag-test");
        Directory.CreateDirectory(dir);
        var storageDir = Path.Combine(dir, "storage");
        Directory.CreateDirectory(storageDir);

        var psi = new ProcessStartInfo
        {
            FileName = ResolveDotnetSdk(),
            ArgumentList = { typeof(Program).Assembly.Location, "--restore-private-objects", "--confirm-destination", "invalid:dest" },
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        ConfigureDotnetEnvironment(psi, psi.FileName);
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["Storage__Root"] = storageDir;
        psi.Environment["Business__TimeZone"] = "Asia/Manila";
        psi.Environment["ConnectionStrings__Default"] = $"Data Source={Path.Combine(dir, "app.db")}";

        using var proc = Process.Start(psi);
        Assert.NotNull(proc);
        var exited = proc.WaitForExit(30000);
        Assert.True(exited, "Process should have exited quickly in offline mode.");
        Assert.Equal(1, proc.ExitCode);

        var err = await proc.StandardError.ReadToEndAsync();
        Assert.Contains("Destination confirmation mismatch", err);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Recovery_operator_script_contract_zero_one_and_multiple_images(string newline)
    {
        var stubBin = Path.Combine(_testDir, "fixture-bin");
        EnsureScriptStubs(stubBin);

        var pwsh = ResolvePowerShell();
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../scripts/restore-backup.ps1"));

        // Helper to run restore-backup.ps1 and test runner
        async Task RunScriptAndVerifyRunner(string name, int imageCount)
        {
            var testPkgDir = Path.Combine(_testDir, "script-contract-" + name);
            Directory.CreateDirectory(testPkgDir);
            var contentDir = Path.Combine(testPkgDir, "content");
            var dbDir = Path.Combine(contentDir, "database");
            Directory.CreateDirectory(dbDir);
            var dumpPath = Path.Combine(dbDir, "prophetops.dump");
            await File.WriteAllTextAsync(dumpPath, "PGDMP-test-" + name);

            var filesList = new List<BackupFileManifest>
            {
                new("database/prophetops.dump", "postgres-custom-dump", new FileInfo(dumpPath).Length,
                    await BackupPackageWriter.Sha256File(dumpPath, CancellationToken.None), File.GetLastWriteTimeUtc(dumpPath))
            };

            for (int i = 0; i < imageCount; i++)
            {
                var imgDir = Path.Combine(contentDir, "uploads", "packages");
                Directory.CreateDirectory(imgDir);
                var imgFile = Path.Combine(imgDir, $"tour_{i}.png");
                var imgBytes = System.Text.Encoding.UTF8.GetBytes($"IMAGE-CONTENT-{name}-{i}");
                await File.WriteAllBytesAsync(imgFile, imgBytes);
                var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
                filesList.Add(new($"uploads/packages/tour_{i}.png", "package-image", imgBytes.Length, imgHash, DateTimeOffset.UtcNow));
            }

            var manifest = new BackupManifest(
                SchemaVersion: 2,
                PackageId: "operator-contract-" + name,
                CreatedUtc: DateTimeOffset.UtcNow,
                Application: "ProphetOps",
                Environment: "Testing",
                SessionContinuity: "restore-invalidates-sessions-by-default",
                Encryption: new BackupEncryptionManifest("none-local-test-only", "test", "1"),
                Database: new BackupDatabaseManifest(
                    Path: "database/prophetops.dump",
                    Size: new FileInfo(dumpPath).Length,
                    Sha256: await BackupPackageWriter.Sha256File(dumpPath, CancellationToken.None),
                    IntegrityCheck: "verified",
                    Provider: "postgres",
                    DumpFormat: "pg-dump-custom",
                    ServerMajor: 18),
                Files: filesList,
                Counts: new BackupCounts(1, imageCount, 0, 0, 0, 0),
                Configuration: [],
                EfMigrations: ["20260927132038_InitialPostgres", "20261002050000_AddDataProtectionKeys", "20261003180000_AddUserSecurityStamp"]);

            await File.WriteAllTextAsync(Path.Combine(contentDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, BackupJsonContext.Default.BackupManifest));

            var zipPath = Path.Combine(testPkgDir, "pkg.prophetops-backup.zip");
            ZipFile.CreateFromDirectory(contentDir, zipPath);

            var destRoot = Path.Combine(testPkgDir, "dest");
            Directory.CreateDirectory(destRoot);

            var psi = new ProcessStartInfo(pwsh)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("-PackagePath");
            psi.ArgumentList.Add(zipPath);
            psi.ArgumentList.Add("-DestinationRoot");
            psi.ArgumentList.Add(destRoot);
            psi.ArgumentList.Add("-PostgresHost");
            psi.ArgumentList.Add("localhost");
            psi.ArgumentList.Add("-PostgresPort");
            psi.ArgumentList.Add("5432");
            psi.ArgumentList.Add("-PostgresDatabase");
            psi.ArgumentList.Add("test_db");
            psi.ArgumentList.Add("-PostgresUsername");
            psi.ArgumentList.Add("test_user");
            psi.ArgumentList.Add("-ConfirmTarget");
            psi.ArgumentList.Add("localhost:5432/test_db");

            psi.Environment["PATH"] = stubBin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            psi.Environment["NO_COLOR"] = "1";
            psi.Environment["COLUMNS"] = "1000";
            psi.Environment["TEST_STUB_NEWLINE"] = newline;
            ConfigureDotnetEnvironment(psi, ResolveDotnetSdk());


            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var proc = Process.Start(psi)!;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                proc.Kill(entireProcessTree: true);
                await proc.WaitForExitAsync();
                throw new TimeoutException("Operator restore script exceeded execution timeout.");
            }

            var outStr = await stdoutTask;
            var errStr = await stderrTask;
            var combined = Regex.Replace(Regex.Replace(outStr + errStr, @"\x1B\[[^@-~]*[@-~]", ""), @"\r?\n\s*\|\s*", " ");

            // Script must fail activation (exit code != 0) with explicit activation blocker
            Assert.NotEqual(0, proc.ExitCode);
            Assert.True(combined.Contains("application activation is BLOCKED", StringComparison.OrdinalIgnoreCase),
                $"Actual output: {outStr}\nSTDERR: {errStr}");

            // Recovery manifest must exist
            var recoveryManifestPath = Path.Combine(destRoot, "recovery", "recovery-manifest.json");
            Assert.True(File.Exists(recoveryManifestPath), $"Recovery manifest missing at {recoveryManifestPath}");

            // Setup isolated in-memory DB matching package's images
            await using var conn = new SqliteConnection("Data Source=:memory:");
            await conn.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
            await using var runnerDb = new AppDbContext(dbOptions);
            await runnerDb.Database.EnsureCreatedAsync();

            for (int i = 0; i < imageCount; i++)
            {
                runnerDb.TravelPackages.Add(new TravelPackage
                {
                    Code = $"PKG{i}",
                    PackageName = $"Tour {i}",
                    ImagePath = $"packages/tour_{i}.png"
                });
            }
            await runnerDb.SaveChangesAsync();

            var fakeStorage = new FakeRecoverableObjectStorage();
            var config = CreateS3Config("test-bucket", "rec/", destRoot, dbProvider: "postgres");
            var paths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = destRoot });
            var runner = new PrivateObjectRecoveryRunner(runnerDb, config, paths, fakeStorage, NullLogger<PrivateObjectRecoveryRunner>.Instance);

            var recoveryResult = await runner.RunAsync(new PrivateObjectRecoveryOptions("supabase-s3:test-bucket/rec/", StagingManifestPath: recoveryManifestPath, RecoveryPrefix: "rec/", ConfirmProject: "fake"), CancellationToken.None);

            if (imageCount == 0)
            {
                // Zero-image package succeeds offline
                Assert.True(recoveryResult.Success, recoveryResult.Message);
                Assert.Equal(0, recoveryResult.ExpectedObjectCount);
                Assert.Equal(0, recoveryResult.VerifiedObjectCount);
            }
            else
            {
                // Non-zero image packages on hosted S3 fail closed before destination write
                Assert.False(recoveryResult.Success);
                Assert.Equal(PrivateObjectRecoveryFailureCategories.ExclusiveCreateUnsupported, recoveryResult.FailureCategory);
                Assert.Equal(imageCount, recoveryResult.ExpectedObjectCount);
                Assert.Equal(0, recoveryResult.VerifiedObjectCount);
            }
        }

        await RunScriptAndVerifyRunner("zero-images", 0);
        await RunScriptAndVerifyRunner("one-image", 1);
        await RunScriptAndVerifyRunner("two-images", 2);
    }

    [Fact]
    public async Task Hosted_recovery_cli_prefix_override_binds_both_creator_and_readback()
    {
        var staging = CreateStagingEnvironment("pkg-cli-override", "sqlite");
        var imgBytes = "CLI-OVERRIDE-BYTES"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/hero.png", imgBytes, imgHash);
        staging.WriteManifest();

        _db.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-CLI",
            PackageName = "CLI Override Tour",
            ImagePath = "packages/hero.png",
        });
        await _db.SaveChangesAsync();

        var fakeS3 = new PrefixAwareFakeS3Storage("test-bucket", "configured-prefix/");
        var capturingHandler = new CapturingHttpMessageHandler
        {
            OnCreated = (key, bytes, ct) => fakeS3.StoreDirect(key, bytes, ct)
        };
        var httpClient = new HttpClient(capturingHandler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.storage.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "configured-prefix/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
                ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_test_key_12345",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var creator = new SupabaseRecoveryObjectCreator(
            config,
            recoveryApiKey: "sb_secret_test_key_12345",
            httpClient: httpClient,
            recoveryPrefix: "cli-override-prefix/");

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/cli-override-prefix/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "cli-override-prefix/",
            ConfirmProject: "testref");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.ExpectedObjectCount);
        Assert.Equal(1, result.VerifiedObjectCount);

        // Assert full native URI and full S3 readback key and bucket
        Assert.Equal("https://testref.supabase.co/storage/v1/object/test-bucket/cli-override-prefix/packages/hero.png", capturingHandler.LastRequestUri?.ToString());
        Assert.Equal("cli-override-prefix/packages/hero.png", fakeS3.LastReadbackKey);
        Assert.Equal("test-bucket", fakeS3.LastReadbackBucket);
    }

    [Theory]
    [InlineData("supabase-s3", true)]
    [InlineData("s3", true)]
    [InlineData("supabase-s3", false)]
    public async Task Hosted_recovery_configured_recovery_prefix_and_fallback_prefix_bind_both_creator_and_readback(string providerAlias, bool useRecoveryPrefix)
    {
        var packageId = $"pkg-cfg-{providerAlias}-{(useRecoveryPrefix ? "rec" : "fallback")}";
        var staging = CreateStagingEnvironment(packageId, "sqlite");
        var imgBytes = "CFG-PREFIX-BYTES"u8.ToArray();
        var imgHash = Convert.ToHexStringLower(SHA256.HashData(imgBytes));
        staging.AddStagedImage("packages/item.png", imgBytes, imgHash);
        staging.WriteManifest();

        using var dbConn = new SqliteConnection("Data Source=:memory:");
        dbConn.Open();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(dbConn).Options;
        using var testDb = new AppDbContext(dbOptions);
        testDb.Database.EnsureCreated();

        testDb.TravelPackages.Add(new TravelPackage
        {
            Code = "PKG-CFG",
            PackageName = "Config Prefix Tour",
            ImagePath = "packages/item.png",
        });
        await testDb.SaveChangesAsync();

        var expectedPrefix = useRecoveryPrefix ? "configured-rec/" : "fallback-pfx/";
        var fakeS3 = new PrefixAwareFakeS3Storage("my-bucket", expectedPrefix);
        var capturingHandler = new CapturingHttpMessageHandler
        {
            OnCreated = (key, bytes, ct) => fakeS3.StoreDirect(key, bytes, ct)
        };
        var httpClient = new HttpClient(capturingHandler);

        var configDict = new Dictionary<string, string?>
        {
            ["Hosted:Enabled"] = "true",
            ["ObjectStorage:Provider"] = providerAlias,
            ["ObjectStorage:S3:Endpoint"] = "https://myref.storage.supabase.co/storage/v1/s3",
            ["ObjectStorage:S3:Bucket"] = "my-bucket",
            ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://myref.supabase.co",
            ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_service_key_987",
            ["Database:Provider"] = "sqlite",
            ["Storage:Root"] = staging.RootDir,
        };
        if (useRecoveryPrefix)
        {
            configDict["ObjectStorage:S3:RecoveryPrefix"] = "configured-rec/";
        }
        else
        {
            configDict["ObjectStorage:S3:Prefix"] = "fallback-pfx/";
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        var creator = new SupabaseRecoveryObjectCreator(
            config,
            recoveryApiKey: "sb_secret_service_key_987",
            httpClient: httpClient,
            recoveryPrefix: null); // omitted -> use configured

        var runner = new PrivateObjectRecoveryRunner(testDb, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: $"{providerAlias}:my-bucket/{expectedPrefix}",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: null,
            ConfirmProject: "myref");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal($"https://myref.supabase.co/storage/v1/object/my-bucket/{expectedPrefix}packages/item.png", capturingHandler.LastRequestUri?.ToString());
        Assert.Equal($"{expectedPrefix}packages/item.png", fakeS3.LastReadbackKey);
        Assert.Equal("my-bucket", fakeS3.LastReadbackBucket);
    }

    [Fact]
    public async Task Hosted_recovery_creator_destination_mismatch_fails_closed_before_writes()
    {
        var staging = CreateStagingEnvironment("pkg-creator-mismatch", "sqlite");
        staging.WriteManifest();

        var fakeS3 = new PrefixAwareFakeS3Storage("test-bucket", "rec/");
        var capturingHandler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(capturingHandler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
                ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_secret_12345",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        // Concrete creator configured with a different prefix
        var creator = new SupabaseRecoveryObjectCreator(
            config,
            recoveryApiKey: "sb_secret_secret_12345",
            httpClient: httpClient,
            recoveryPrefix: "different-prefix/");

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "testref");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch, result.FailureCategory);
        Assert.Contains("Recovery creator destination", result.Message);
        Assert.Equal(0, capturingHandler.RequestCount);
        Assert.Empty(fakeS3.Objects);
    }

    [Theory]
    [InlineData(null, "Hosted recovery requires project confirmation")] // Missing --confirm-project
    [InlineData("wrongref", "Project confirmation mismatch")] // Mismatched project ref
    public async Task Hosted_recovery_requires_confirm_project_matching_endpoints_and_rejects_mismatch(string? confirmProject, string expectedErrorSubstring)
    {
        var staging = CreateStagingEnvironment("pkg-project-confirm", "sqlite");
        staging.WriteManifest();

        var fakeS3 = new PrefixAwareFakeS3Storage("test-bucket", "rec/");
        var capturingHandler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(capturingHandler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
                ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_secret_12345",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var creator = new SupabaseRecoveryObjectCreator(
            config,
            recoveryApiKey: "sb_secret_secret_12345",
            httpClient: httpClient,
            recoveryPrefix: "rec/");
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance, creator);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: confirmProject);

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch, result.FailureCategory);
        Assert.Contains(expectedErrorSubstring, result.Message);
        Assert.Equal(0, capturingHandler.RequestCount);
        Assert.Empty(fakeS3.Objects);
    }

    [Fact]
    public async Task Hosted_recovery_endpoint_project_mismatch_fails_closed_before_writes()
    {
        var staging = CreateStagingEnvironment("pkg-endpoint-mismatch", "sqlite");
        staging.WriteManifest();

        var fakeS3 = new PrefixAwareFakeS3Storage("test-bucket", "rec/");
        var capturingHandler = new CapturingHttpMessageHandler();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref1.storage.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref2.supabase.co",
                ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_secret_12345",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: "supabase-s3:test-bucket/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "testref1");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidConfiguration, result.FailureCategory);
        Assert.Contains("does not match S3 endpoint ref", result.Message);
        Assert.Equal(0, capturingHandler.RequestCount);
        Assert.Empty(fakeS3.Objects);
    }

    [Fact]
    public async Task Local_recovery_does_not_require_confirm_project()
    {
        var staging = CreateStagingEnvironment("pkg-local-no-project", "sqlite");
        staging.WriteManifest();

        var config = CreateLocalConfig(staging.RootDir);
        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, new FakeRecoverableObjectStorage(), NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: $"local:{staging.StoragePaths.UploadsPath}",
            StagingManifestPath: staging.ManifestPath,
            ConfirmProject: null); // null confirm project

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.True(result.Success, result.Message);
    }

    [Theory]
    [InlineData("../rec/")]
    [InlineData("rec/../secret/")]
    [InlineData("rec\\backslash/")]
    [InlineData("rec%20space/")]
    public async Task Hosted_recovery_rejects_malformed_or_traversal_recovery_prefixes(string malformedPrefix)
    {
        var staging = CreateStagingEnvironment("pkg-bad-prefix", "sqlite");
        staging.WriteManifest();

        var fakeS3 = new PrefixAwareFakeS3Storage("test-bucket", "rec/");
        var capturingHandler = new CapturingHttpMessageHandler();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = "test-bucket",
                ["ObjectStorage:S3:RecoveryPrefix"] = malformedPrefix,
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
                ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_secret_12345",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: $"supabase-s3:test-bucket/{malformedPrefix}",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: malformedPrefix,
            ConfirmProject: "testref");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidConfiguration, result.FailureCategory);
        Assert.Equal(0, capturingHandler.RequestCount);
        Assert.Empty(fakeS3.Objects);
    }

    [Theory]
    [InlineData("../bucket")]
    [InlineData("my/bucket")]
    [InlineData("bucket with spaces")]
    [InlineData("bucket%20escaped")]
    public async Task Hosted_recovery_rejects_malformed_or_traversal_buckets(string malformedBucket)
    {
        var staging = CreateStagingEnvironment("pkg-bad-bucket", "sqlite");
        staging.WriteManifest();

        var fakeS3 = new PrefixAwareFakeS3Storage(malformedBucket, "rec/");
        var capturingHandler = new CapturingHttpMessageHandler();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hosted:Enabled"] = "true",
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = "https://testref.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = malformedBucket,
                ["ObjectStorage:S3:RecoveryPrefix"] = "rec/",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = "https://testref.supabase.co",
                ["ObjectStorage:Recovery:ServiceKey"] = "sb_secret_secret_12345",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = staging.RootDir,
            })
            .Build();

        var runner = new PrivateObjectRecoveryRunner(_db, config, staging.StoragePaths, fakeS3, NullLogger<PrivateObjectRecoveryRunner>.Instance);

        var options = new PrivateObjectRecoveryOptions(
            ConfirmDestination: $"supabase-s3:{malformedBucket}/rec/",
            StagingManifestPath: staging.ManifestPath,
            RecoveryPrefix: "rec/",
            ConfirmProject: "testref");

        var result = await runner.RunAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PrivateObjectRecoveryFailureCategories.InvalidConfiguration, result.FailureCategory);
        Assert.Equal(0, capturingHandler.RequestCount);
        Assert.Empty(fakeS3.Objects);
    }

    private TestStagingEnvironment CreateStagingEnvironment(string packageId, string dbProvider)
    {
        var stagingDir = Path.Combine(_testDir, packageId);
        Directory.CreateDirectory(stagingDir);
        return new TestStagingEnvironment(stagingDir, packageId, dbProvider);
    }

    private static IConfiguration CreateS3Config(string bucket, string prefix, string? storageRoot = null, string? dbProvider = "sqlite", string projectRef = "fake")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "supabase-s3",
                ["ObjectStorage:S3:Endpoint"] = $"https://{projectRef}.supabase.co/storage/v1/s3",
                ["ObjectStorage:S3:Bucket"] = bucket,
                ["ObjectStorage:S3:RecoveryPrefix"] = prefix,
                ["ObjectStorage:S3:AccessKeyId"] = "key",
                ["ObjectStorage:S3:SecretAccessKey"] = "secret",
                ["ObjectStorage:Recovery:SupabaseProjectUrl"] = $"https://{projectRef}.supabase.co",
                ["Database:Provider"] = dbProvider ?? "sqlite",
                ["Storage:Root"] = storageRoot ?? Path.GetTempPath(),
                ["Storage:AllowContentRootFallback"] = "true",
            })
            .Build();
    }

    private static IConfiguration CreateLocalConfig(string rootDir)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "local",
                ["Database:Provider"] = "sqlite",
                ["Storage:Root"] = rootDir,
                ["Storage:AllowContentRootFallback"] = "true",
            })
            .Build();
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<string> LoggedMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(formatter(state, exception));
            if (exception != null)
            {
                LoggedMessages.Add(exception.ToString());
            }
        }
    }

    private sealed class TestStagingEnvironment
    {
        public string RootDir { get; }
        public string StagedImagesDir { get; }
        public string ManifestPath { get; }
        public string PackageId { get; }
        public string DbProvider { get; }
        public StoragePaths StoragePaths { get; }
        public List<RecoverableImageRecord> Images { get; } = [];

        public TestStagingEnvironment(string rootDir, string packageId, string dbProvider)
        {
            RootDir = rootDir;
            PackageId = packageId;
            DbProvider = dbProvider;
            StagedImagesDir = Path.Combine(rootDir, "staged-images");
            Directory.CreateDirectory(StagedImagesDir);
            ManifestPath = Path.Combine(rootDir, "recovery-manifest.json");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Root"] = rootDir,
                    ["Storage:AllowContentRootFallback"] = "true",
                })
                .Build();
            StoragePaths = StoragePaths.FromConfiguration(config, new MockHostEnvironment { ContentRootPath = rootDir });
        }

        public void AddStagedImage(string dbKey, byte[] bytes, string sha256, long? declaredSize = null)
        {
            var relPath = dbKey.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(StagedImagesDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, bytes);

            Images.Add(new RecoverableImageRecord(dbKey, dbKey, declaredSize ?? bytes.Length, sha256));
        }

        public void AddManifestEntryOnly(string dbKey, long size, string sha256)
        {
            Images.Add(new RecoverableImageRecord(dbKey, dbKey, size, sha256));
        }

        public void WriteManifest()
        {
            var manifest = new PrivateObjectRecoveryManifest(
                PackageId,
                2,
                DbProvider,
                DateTimeOffset.UtcNow,
                "StagedOfflinePendingObjectStorageUpload",
                Images);

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ManifestPath, json);
        }
    }

    private sealed class FakeRecoverableObjectStorage : IObjectStorage, IPrefixRecoverableObjectStorage
    {
        private readonly object _lock = new();
        private readonly string _prefix;
        public Dictionary<string, (byte[] Bytes, string ContentType)> Objects { get; }
        public List<string> DeletedKeys { get; } = [];
        public bool FailReadbackNull { get; set; }
        public byte[]? CorruptReadbackBytes { get; set; }
        public Exception? ThrowOnPut { get; set; }

        public FakeRecoverableObjectStorage(string prefix = "", Dictionary<string, (byte[] Bytes, string ContentType)>? sharedObjects = null)
        {
            _prefix = prefix;
            Objects = sharedObjects ?? new(StringComparer.Ordinal);
        }

        public IObjectStorage WithPrefix(string prefix) =>
            new FakeRecoverableObjectStorage(prefix, Objects)
            {
                FailReadbackNull = FailReadbackNull,
                CorruptReadbackBytes = CorruptReadbackBytes,
                ThrowOnPut = ThrowOnPut
            };

        public void Store(string key, byte[] bytes, string contentType)
        {
            lock (_lock)
            {
                Objects[key] = (bytes, contentType);
            }
        }

        public Task PutAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
        {
            if (ThrowOnPut != null) throw ThrowOnPut;
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            using var ms = new MemoryStream();
            body.CopyTo(ms);
            var bytes = ms.ToArray();
            lock (_lock)
            {
                Objects[fullKey] = (bytes, contentType);
            }
            return Task.CompletedTask;
        }

        public Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
        {
            if (ThrowOnPut != null) throw ThrowOnPut;
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            using var ms = new MemoryStream();
            body.CopyTo(ms);
            var bytes = ms.ToArray();

            lock (_lock)
            {
                if (Objects.ContainsKey(fullKey))
                {
                    return Task.FromResult(false);
                }

                Objects[fullKey] = (bytes, contentType);
                return Task.FromResult(true);
            }
        }

        public Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken)
        {
            if (FailReadbackNull) return Task.FromResult<StoredObject?>(null);

            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            (byte[] Bytes, string ContentType) item;
            lock (_lock)
            {
                if (!Objects.TryGetValue(fullKey, out item) && !Objects.TryGetValue(key, out item))
                    return Task.FromResult<StoredObject?>(null);
            }

            var bytes = CorruptReadbackBytes ?? item.Bytes;
            var ms = new MemoryStream(bytes);
            return Task.FromResult<StoredObject?>(new StoredObject(ms, item.ContentType, bytes.Length));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            lock (_lock)
            {
                DeletedKeys.Add(fullKey);
                Objects.Remove(fullKey);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeS3State
    {
        public Dictionary<string, (byte[] Bytes, string ContentType)> Objects { get; } = new(StringComparer.Ordinal);
        public string? LastReadbackKey { get; set; }
        public string? LastReadbackBucket { get; set; }
    }

    private sealed class PrefixAwareFakeS3Storage : IObjectStorage, IPrefixRecoverableObjectStorage
    {
        private readonly string _prefix;
        private readonly FakeS3State _state;
        public string Bucket { get; }
        public Dictionary<string, (byte[] Bytes, string ContentType)> Objects => _state.Objects;
        public string? LastReadbackKey => _state.LastReadbackKey;
        public string? LastReadbackBucket => _state.LastReadbackBucket;

        public PrefixAwareFakeS3Storage(string bucket = "test-bucket", string prefix = "", FakeS3State? state = null)
        {
            Bucket = bucket;
            _prefix = prefix;
            _state = state ?? new FakeS3State();
        }

        public IObjectStorage WithPrefix(string newPrefix) =>
            new PrefixAwareFakeS3Storage(Bucket, newPrefix, _state);

        public void StoreDirect(string fullKey, byte[] bytes, string contentType)
        {
            Objects[fullKey] = (bytes, contentType);
        }

        public Task PutAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
        {
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            using var ms = new MemoryStream();
            body.CopyTo(ms);
            Objects[fullKey] = (ms.ToArray(), contentType);
            return Task.CompletedTask;
        }

        public Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
        {
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            if (Objects.ContainsKey(fullKey)) return Task.FromResult(false);
            using var ms = new MemoryStream();
            body.CopyTo(ms);
            Objects[fullKey] = (ms.ToArray(), contentType);
            return Task.FromResult(true);
        }

        public Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken)
        {
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            _state.LastReadbackKey = fullKey;
            _state.LastReadbackBucket = Bucket;
            if (!Objects.TryGetValue(fullKey, out var item))
            {
                return Task.FromResult<StoredObject?>(null);
            }
            return Task.FromResult<StoredObject?>(new StoredObject(new MemoryStream(item.Bytes), item.ContentType, item.Bytes.Length));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            var fullKey = string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}{key.TrimStart('/')}";
            Objects.Remove(fullKey);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Uri? LastRequestUri => Requests.LastOrDefault()?.RequestUri;
        public int RequestCount => Requests.Count;
        public Func<HttpRequestMessage, HttpResponseMessage>? CustomResponder { get; set; }
        public Action<string, byte[], string>? OnCreated { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (CustomResponder != null)
            {
                return CustomResponder(request);
            }

            if (request.Content != null && OnCreated != null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                var contentType = request.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var uri = request.RequestUri!.AbsolutePath;
                var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var keySegments = segments.Skip(4);
                var objectKey = string.Join('/', keySegments);
                OnCreated(objectKey, bytes, contentType);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.Created)
            {
                Content = new StringContent("{\"Key\":\"created\"}")
            };
        }
    }

    private static string ResolveDotnetSdk()
    {
        var configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && DotnetHasSdk(configuredHost))
        {
            return configuredHost;
        }

        var configuredRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var rootedDotnet = Path.Combine(configuredRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (DotnetHasSdk(rootedDotnet))
            {
                return rootedDotnet;
            }
        }

        if (DotnetHasSdk("dotnet"))
        {
            return "dotnet";
        }

        throw new InvalidOperationException("A .NET SDK is required for this test. Set DOTNET_HOST_PATH or DOTNET_ROOT to an SDK installation, or place dotnet on PATH.");
    }

    private static string ResolvePowerShell()
    {
        var configured = Environment.GetEnvironmentVariable("PWSH_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && (File.Exists(configured) || CommandExists(configured)))
        {
            return configured;
        }

        if (CommandExists("pwsh")) return "pwsh";
        if (CommandExists("powershell")) return "powershell";
        if (OperatingSystem.IsWindows() && File.Exists(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"))
        {
            return @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        }

        return "pwsh";
    }

    private static bool CommandExists(string command)
    {
        if (File.Exists(command)) return true;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => extensions.Any(extension => File.Exists(Path.Combine(directory, command + extension))));
    }

    private static bool DotnetHasSdk(string dotnetCommand)
    {
        if (string.IsNullOrWhiteSpace(dotnetCommand)) return false;
        if ((dotnetCommand.Contains(Path.DirectorySeparatorChar) || dotnetCommand.Contains(Path.AltDirectorySeparatorChar)) && !File.Exists(dotnetCommand))
        {
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo(dotnetCommand)
            {
                ArgumentList = { "--list-sdks" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            ConfigureDotnetEnvironment(psi, dotnetCommand);

            using var process = Process.Start(psi);
            if (process == null || !process.WaitForExit(10000))
            {
                try { process?.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static void ConfigureDotnetEnvironment(ProcessStartInfo psi, string dotnetCommand)
    {
        var root = DotnetRootFromCommand(dotnetCommand);
        root ??= Environment.GetEnvironmentVariable("DOTNET_ROOT");

        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            psi.Environment["DOTNET_ROOT"] = root;
            PrependPath(psi, root);
        }

        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
        {
            var hostDirectory = Path.GetDirectoryName(hostPath);
            if (!string.IsNullOrWhiteSpace(hostDirectory))
            {
                PrependPath(psi, hostDirectory);
            }
        }
    }

    private static string? DotnetRootFromCommand(string dotnetCommand)
    {
        if (string.IsNullOrWhiteSpace(dotnetCommand)) return null;
        if (!dotnetCommand.Contains(Path.DirectorySeparatorChar) && !dotnetCommand.Contains(Path.AltDirectorySeparatorChar))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(dotnetCommand);
        if (!File.Exists(fullPath)) return null;
        return Path.GetDirectoryName(fullPath);
    }

    private static void PrependPath(ProcessStartInfo psi, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var existingPath = psi.Environment.TryGetValue("PATH", out var p) && p != null
            ? p
            : Environment.GetEnvironmentVariable("PATH") ?? "";

        var alreadyPresent = existingPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(path =>
            {
                try
                {
                    return string.Equals(
                        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        fullDirectory,
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

        if (!alreadyPresent)
        {
            psi.Environment["PATH"] = directory + Path.PathSeparator + existingPath;
        }
    }

    private static string ResolveApiProjectPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../ProphetOps.Api/ProphetOps.Api.csproj"));

    internal static void EnsureScriptStubs(string stubDir)
    {
        Directory.CreateDirectory(stubDir);
        var pgRestore = OperatingSystem.IsWindows() ? Path.Combine(stubDir, "pg_restore.exe") : Path.Combine(stubDir, "pg_restore");
        var psql = OperatingSystem.IsWindows() ? Path.Combine(stubDir, "psql.exe") : Path.Combine(stubDir, "psql");
        var marker = Path.Combine(stubDir, "stub-v3.marker");
        if (File.Exists(pgRestore) && File.Exists(psql) && File.Exists(marker)) return;

        var sdkPath = ResolveDotnetSdk();
        var sdkDir = DotnetRootFromCommand(sdkPath);
        if (!string.IsNullOrEmpty(sdkDir))
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", sdkDir);
        }

        var parentDir = Path.GetDirectoryName(stubDir) ?? stubDir;
        var stubProjDir = Path.Combine(parentDir, "stub-src");
        Directory.CreateDirectory(stubProjDir);

        var prog = """
using System;
using System.IO;

class Program
{
    static int Main(string[] args)
    {
        var stubNl = Environment.GetEnvironmentVariable("TEST_STUB_NEWLINE");
        if (stubNl == "LF" || stubNl == "\n")
        {
            Console.Out.NewLine = "\n";
        }
        else if (stubNl == "CRLF" || stubNl == "\r\n")
        {
            Console.Out.NewLine = "\r\n";
        }

        var eventLog = Environment.GetEnvironmentVariable("TEST_STUB_EVENT_LOG");
        void LogEvent(string ev)
        {
            if (!string.IsNullOrEmpty(eventLog))
            {
                File.AppendAllText(eventLog, ev + Environment.NewLine);
            }
        }

        var line = string.Join(" ", args);
        if (line.Contains("--version"))
        {
            Console.WriteLine("pg_restore (PostgreSQL) 18.0");
            return 0;
        }

        if (line.Contains("--single-transaction"))
        {
            var requireSchema = Environment.GetEnvironmentVariable("TEST_STUB_REQUIRE_SCHEMA");
            if (requireSchema == "1" || requireSchema == "true")
            {
                bool hasSchema = false;
                if (!string.IsNullOrEmpty(eventLog) && File.Exists(eventLog))
                {
                    var content = File.ReadAllText(eventLog);
                    if (content.Contains("SCHEMA_CREATE_SUCCESS"))
                    {
                        hasSchema = true;
                    }
                }
                if (!hasSchema)
                {
                    Console.Error.WriteLine("pg_restore: error: could not execute query: ERROR: schema \"prophetops\" does not exist");
                    return 1;
                }
            }

            LogEvent("PG_RESTORE_INVOKED");
            var sentinel = Environment.GetEnvironmentVariable("TEST_STUB_PG_RESTORE_SENTINEL");
            if (!string.IsNullOrEmpty(sentinel))
            {
                File.WriteAllText(sentinel, "pg_restore_invoked");
            }
            return 0;
        }

        var schemaFail = Environment.GetEnvironmentVariable("TEST_STUB_PSQL_SCHEMA_FAIL");
        if ((schemaFail == "1" || schemaFail == "true") && line.Contains("CREATE SCHEMA"))
        {
            LogEvent("SCHEMA_CREATE_FAILED");
            Console.Error.WriteLine("ERROR: permission denied for database target_db");
            return 1;
        }

        if (line.Contains("CREATE SCHEMA"))
        {
            LogEvent("SCHEMA_CREATE_SUCCESS");
            return 0;
        }

        var populated = Environment.GetEnvironmentVariable("TEST_STUB_PSQL_POPULATED");
        if ((populated == "1" || populated == "true") && line.Contains("SUM(c)"))
        {
            LogEvent("CLEAN_CHECK_POPULATED");
            Console.WriteLine("3");
            return 0;
        }

        if (line.Contains("SUM(c)"))
        {
            LogEvent("CLEAN_CHECK");
            Console.WriteLine("0");
            return 0;
        }

        if (line.Contains("relname"))
        {
            Console.WriteLine("Users");
            Console.WriteLine("TravelPackages");
            Console.WriteLine("Bookings");
            Console.WriteLine("Expenses");
            Console.WriteLine("AuditEntries");
            Console.WriteLine("__EFMigrationsHistory");
            return 0;
        }
        if (line.Contains("MigrationId"))
        {
            Console.WriteLine("20260927132038_InitialPostgres");
            Console.WriteLine("20261002050000_AddDataProtectionKeys");
            Console.WriteLine("20261003180000_AddUserSecurityStamp");
            return 0;
        }
        if (line.Contains("SecurityStamp") && line.Contains("information_schema"))
        {
            Console.WriteLine("SecurityStamp");
            return 0;
        }
        return 0;
    }
}
""";

        foreach (var name in new[] { "pg_restore", "psql" })
        {
            var appDir = Path.Combine(stubProjDir, name);
            Directory.CreateDirectory(appDir);
            var csproj = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>{name}</AssemblyName>
  </PropertyGroup>
</Project>
""";
            File.WriteAllText(Path.Combine(appDir, $"{name}.csproj"), csproj);
            File.WriteAllText(Path.Combine(appDir, "Program.cs"), prog);

            var psi = new ProcessStartInfo(sdkPath)
            {
                ArgumentList = { "build", "-c", "Release", "-o", stubDir, Path.Combine(appDir, $"{name}.csproj") },
                WorkingDirectory = appDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            ConfigureDotnetEnvironment(psi, sdkPath);
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to launch dotnet build for {name}.");
            var exited = proc.WaitForExit(30000);
            if (!exited || proc.ExitCode != 0)
            {
                var stdout = proc.StandardOutput.ReadToEnd();
                var err = proc.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Failed to build required test stub {name} (ExitCode {proc.ExitCode}): STDOUT: {stdout} STDERR: {err}");
            }

            if (!OperatingSystem.IsWindows())
            {
                var builtFile = Path.Combine(stubDir, name);
                if (File.Exists(builtFile))
                {
                    var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                               UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                               UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                    File.SetUnixFileMode(builtFile, mode);
                }
            }
        }

        File.WriteAllText(marker, "stub-v3");
    }

    private sealed class FakeNativeRecoveryObjectCreator(
        Func<string, Stream, string, long, Task<bool>> handler,
        SupabaseRecoveryDestination? destination = null) : ISupabaseRecoveryObjectCreator
    {
        public SupabaseRecoveryDestination Destination { get; } = destination ?? new SupabaseRecoveryDestination("fake", "test-bucket", "rec/");

        public Task<bool> TryCreateAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken)
            => handler(key, body, contentType, length);
    }

    private sealed class MockHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ProphetOps";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
