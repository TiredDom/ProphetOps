using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BackupRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "prophetops-backup-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Backup_package_restores_database_images_keys_and_report_totals_to_empty_destination()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory);

        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Manifest);
        Assert.Equal("none-local-test-only", result.Manifest!.Encryption.Mode);
        Assert.DoesNotContain(result.Manifest.Configuration, fact => fact.Value?.Contains(_root, StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(result.Manifest.Files, file => file.Role == "package-image" && file.Path.EndsWith("backup.png", StringComparison.Ordinal));
        Assert.Contains(result.Manifest.Files, file => file.Role == "data-protection-key");

        var restoreRoot = Path.Combine(_root, "restore");
        Directory.CreateDirectory(restoreRoot);
        RestoreVerified(result.ArchivePath!, restoreRoot, preserveSessions: false);

        var restoredDb = Path.Combine(restoreRoot, "prophetops.db");
        Assert.Equal("ok", Integrity(restoredDb));
        await using var db = Open(restoredDb);
        Assert.True(await db.Users.AnyAsync(u => u.Email == "owner@prophetops.local"));
        Assert.True(await db.Bookings.AnyAsync(b => b.Code == "B04-BOOK"));
        Assert.True(await db.Expenses.AnyAsync(e => e.Code == "B04-EXP"));
        Assert.True(await db.AuditEntries.AnyAsync(a => a.EntityCode == "B04-BOOK"));
        Assert.True(await db.Bookings.Where(b => b.VoidedAt == null).SumAsync(b => b.GrossRevenue) >= 7000);
        Assert.True(await db.Expenses.Where(e => e.VoidedAt == null).SumAsync(e => e.Amount) >= 1200);
        Assert.True(File.Exists(Path.Combine(restoreRoot, "uploads", "packages", "backup.png")));
        Assert.False(Directory.Exists(Path.Combine(restoreRoot, "keys")));
    }

    [Fact]
    public async Task Manual_backup_is_owner_only()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory);

        using var owner = await AuthenticatedClient.Login(factory);
        var ok = await owner.PostAsync("/api/maintenance/backup", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var admin = await AuthenticatedClient.Login(factory, "admin@prophetops.local", "admin123");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/maintenance/backup", null)).StatusCode);

        using var staff = await AuthenticatedClient.Login(factory, "staff@prophetops.local", "staff123");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync("/api/maintenance/backup", null)).StatusCode);
    }

    [Fact]
    public async Task Maintenance_gate_rejects_new_mutations_and_login_but_allows_reads()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory);
        using var client = await AuthenticatedClient.Login(factory);
        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<MaintenanceGate>();
        using var capture = await gate.TryBeginBackupCaptureAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(capture);

        var report = await client.GetAsync("/api/reports");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);

        var mutation = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "B04-BLOCK",
            ds = "2026-09-01",
            y = 1,
            client = "Blocked",
            package = "Blocked",
            destination = "Bohol",
            grossRevenue = 100,
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mutation.StatusCode);
        Assert.True(mutation.Headers.Contains("Retry-After"));

        using var fresh = factory.CreateClient();
        var login = await fresh.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
    }

    [Fact]
    public async Task Failed_drain_reopens_admission_and_writes_no_package()
    {
        using var factory = new BackupFactory(_root);
        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<MaintenanceGate>();
        var admission = gate.TryEnterMutation();
        Assert.True(admission.Allowed);

        var capture = await gate.TryBeginBackupCaptureAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None);

        Assert.Null(capture);
        admission.Lease!.Dispose();
        Assert.True(gate.TryEnterMutation().Allowed);
    }

    [Fact]
    public async Task Missing_referenced_image_fails_without_visible_package()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory, writeImage: false);

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        var independent = Path.Combine(_root, "backups", "independent");
        Assert.False(Directory.Exists(independent) && Directory.GetFiles(independent, "*.prophetops-backup.zip").Any());
    }

    [Fact]
    public async Task Failed_upload_does_not_prune_existing_good_backup()
    {
        var storage = new FakeBackupStorage();
        using var factory = new BackupFactory(_root, storage);
        await SeedFixture(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var ok = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.True(ok.Success, ok.Error);
        }

        storage.FailUpload = true;
        using (var scope = factory.Services.CreateScope())
        {
            var failed = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.False(failed.Success);
        }

        Assert.Single(storage.Stored);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Failed_verification_does_not_prune_existing_good_backup()
    {
        var storage = new FakeBackupStorage();
        using var factory = new BackupFactory(_root, storage);
        await SeedFixture(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var ok = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.True(ok.Success, ok.Error);
        }
        var backups = Path.Combine(_root, "backups");
        var plaintextBefore = Directory.Exists(backups)
            ? Directory.GetFiles(backups, "*.prophetops-backup.zip", SearchOption.TopDirectoryOnly).Length
            : 0;

        storage.FailVerification = true;
        using (var scope = factory.Services.CreateScope())
        {
            var failed = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.False(failed.Success);
        }

        Assert.Single(storage.Stored);
        Assert.Single(storage.Deleted);
        var plaintextAfter = Directory.Exists(backups)
            ? Directory.GetFiles(backups, "*.prophetops-backup.zip", SearchOption.TopDirectoryOnly).Length
            : 0;
        Assert.Equal(plaintextBefore, plaintextAfter);
    }

    [Fact]
    public async Task Staging_write_failure_leaves_no_visible_package_or_plaintext_zip()
    {
        var operations = new FailingPackageOperations { FailZip = true };
        using var factory = new BackupFactory(_root, operations: operations);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "backups"), "*.prophetops-backup.*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Encryption_failure_removes_plaintext_zip_and_partial_envelope()
    {
        var operations = new FailingPackageOperations { FailEncryption = true };
        using var factory = new BackupFactory(_root, operations: operations, encryptionKey: TestKey());
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        var backups = Path.Combine(_root, "backups");
        Assert.Empty(Directory.Exists(backups)
            ? Directory.GetFiles(backups, "*.prophetops-backup.*", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task Hosted_backup_requires_operator_supplied_encryption_key()
    {
        using var factory = new BackupFactory(_root, hosted: true);
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("capture", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hosted_backup_with_encryption_but_default_local_storage_fails_closed()
    {
        using var factory = new BackupFactory(_root, hosted: true, encryptionKey: TestKey());
        await SeedFixture(factory);

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("upload", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hosted_backup_uploads_encrypted_package_to_s3_compatible_storage()
    {
        var objectStore = new FakeS3BackupClient();
        using var factory = new BackupFactory(_root, hosted: true, encryptionKey: TestKey(),
            backupStorageKind: "r2", objectStorage: objectStore);
        await SeedFixture(factory);

        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.StartsWith("s3://prophetops-backups/release/", result.ArchivePath, StringComparison.Ordinal);
        Assert.EndsWith(BackupEncryptionSettings.EnvelopeExtension, result.StoredName, StringComparison.Ordinal);
        Assert.Single(objectStore.Objects);
        Assert.Contains(objectStore.Operations, operation => operation == "Put");
        Assert.Contains(objectStore.Operations, operation => operation == "Hash");
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "backups"), "*.prophetops-backup.*", SearchOption.TopDirectoryOnly));

        using (var scope = factory.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            var quarantine = Path.Combine(paths.PackageImagesPath, ".quarantine");
            Directory.CreateDirectory(quarantine);
            await File.WriteAllTextAsync(Path.Combine(quarantine, "old.png"), "old");
            await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().PruneAfterVerifiedUpload(CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(quarantine, "old.png")));
        }
    }

    [Fact]
    public async Task S3_verification_downloads_and_hashes_stored_content_not_only_metadata()
    {
        var objectStore = new FakeS3BackupClient { CorruptReadHash = true };
        using var factory = new BackupFactory(_root, hosted: true, encryptionKey: TestKey(),
            backupStorageKind: "r2", objectStorage: objectStore);
        await SeedFixture(factory);

        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("checksum", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(objectStore.Operations, operation => operation == "Delete");
    }

    [Fact]
    public async Task S3_provider_failure_returns_failed_backup_without_pruning_existing_good_object()
    {
        var objectStore = new FakeS3BackupClient();
        using var factory = new BackupFactory(_root, hosted: true, encryptionKey: TestKey(),
            backupStorageKind: "r2", objectStorage: objectStore);
        await SeedFixture(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var ok = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.True(ok.Success, ok.Error);
        }
        var storedBefore = objectStore.Objects.Count;

        objectStore.FailPut = true;
        using (var scope = factory.Services.CreateScope())
        {
            var failed = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.False(failed.Success);
        }

        Assert.Equal(storedBefore, objectStore.Objects.Count);
    }

    [Fact]
    public async Task Aws_s3_put_request_uses_r2_compatible_streaming_flags()
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "request" + BackupEncryptionSettings.EnvelopeExtension);
        await File.WriteAllTextAsync(archive, "request");
        var package = new BackupPackage(
            "request-package",
            archive,
            await BackupPackageWriter.Sha256File(archive, CancellationToken.None),
            new FileInfo(archive).Length,
            EmptyManifest());
        await using var stream = File.OpenRead(archive);

        var request = AwsS3BackupClient.CreatePutRequest("bucket", "prefix/request.pobak", stream, package);

        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.Equal("bucket", request.BucketName);
        Assert.Equal("prefix/request.pobak", request.Key);
        Assert.Equal(package.Sha256, request.Metadata["x-amz-meta-sha256"]);
    }

    [Fact]
    public async Task Encrypted_package_is_not_a_zip_and_decrypts_only_with_the_intended_key()
    {
        var key = TestKey();
        var storage = new FakeBackupStorage();
        using var factory = new BackupFactory(_root, storage, hosted: true, encryptionKey: key);
        await SeedFixture(factory);

        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.EndsWith(BackupEncryptionSettings.EnvelopeExtension, result.ArchivePath);
        Assert.Throws<InvalidDataException>(() => ZipFile.OpenRead(result.ArchivePath!).Dispose());
        Assert.Equal(BackupEncryptionSettings.Algorithm, result.Manifest!.Encryption.Mode);

        var decrypted = Path.Combine(_root, "decrypted.zip");
        await BackupEncryptedEnvelope.DecryptFile(result.ArchivePath!, decrypted, Convert.FromBase64String(key), CancellationToken.None);
        using (var archive = ZipFile.OpenRead(decrypted))
        {
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("database/prophetops.db"));
        }

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            BackupEncryptedEnvelope.DecryptFile(result.ArchivePath!, Path.Combine(_root, "wrong.zip"), Convert.FromBase64String(TestKey(7)), CancellationToken.None));

        var tampered = Path.Combine(_root, "tampered.pobak");
        File.Copy(result.ArchivePath!, tampered);
        var bytes = await File.ReadAllBytesAsync(tampered);
        bytes[^1] ^= 0x7F;
        await File.WriteAllBytesAsync(tampered, bytes);
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            BackupEncryptedEnvelope.DecryptFile(tampered, Path.Combine(_root, "tampered.zip"), Convert.FromBase64String(key), CancellationToken.None));
    }

    [Fact]
    public async Task Corrupt_package_hash_rejects_restore_before_destination_activation()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory);
        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        var corrupt = CorruptDatabaseEntry(result.ArchivePath!);

        var restoreRoot = Path.Combine(_root, "corrupt-restore");
        Directory.CreateDirectory(restoreRoot);
        Assert.Throws<InvalidOperationException>(() => RestoreVerified(corrupt, restoreRoot, preserveSessions: false));
        Assert.Empty(Directory.GetFileSystemEntries(restoreRoot));
    }

    [Fact]
    public async Task Restore_rejects_traversal_manifest_paths_before_activation()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory);
        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        var traversal = RewriteManifest(result.ArchivePath!, manifest =>
            manifest with { Files = manifest.Files.Select((file, index) => index == 0 ? file with { Path = "../escape.db" } : file).ToList() });
        var restoreRoot = Path.Combine(_root, "traversal-restore");
        Directory.CreateDirectory(restoreRoot);

        Assert.Throws<InvalidOperationException>(() => RestoreVerified(traversal, restoreRoot, preserveSessions: false));
        Assert.Empty(Directory.GetFileSystemEntries(restoreRoot));
    }

    [Fact]
    public async Task Restore_rejects_schema_migration_mismatch_before_activation()
    {
        using var factory = new BackupFactory(_root);
        await SeedFixture(factory);
        BackupRunResult result;
        using (var scope = factory.Services.CreateScope())
            result = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);

        var mismatch = RewriteManifest(result.ArchivePath!, manifest => manifest with { EfMigrations = ["missing-migration"] });
        var restoreRoot = Path.Combine(_root, "schema-restore");
        Directory.CreateDirectory(restoreRoot);

        Assert.Throws<InvalidOperationException>(() => RestoreVerified(mismatch, restoreRoot, preserveSessions: false));
        Assert.Empty(Directory.GetFileSystemEntries(restoreRoot));
    }

    [Fact]
    public void Restore_rejects_malicious_zip_entry_before_parent_path_is_touched()
    {
        var package = Path.Combine(_root, "malicious.zip");
        Directory.CreateDirectory(_root);
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../escape.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("owned");
        }
        var restoreRoot = Path.Combine(_root, "zip-slip-restore");
        Directory.CreateDirectory(restoreRoot);

        Assert.Throws<InvalidOperationException>(() => RestoreVerified(package, restoreRoot, preserveSessions: false));
        Assert.False(File.Exists(Path.Combine(_root, "..", "escape.txt")));
        Assert.Empty(Directory.GetFileSystemEntries(restoreRoot));
    }

    [Fact]
    public async Task Quarantined_image_is_retained_when_encrypted_retained_package_cannot_be_inspected()
    {
        var storage = new FakeBackupStorage();
        using var factory = new BackupFactory(_root, storage, hosted: true, encryptionKey: TestKey());
        await SeedFixture(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var ok = await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().RunAsync(CancellationToken.None);
            Assert.True(ok.Success, ok.Error);
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            var quarantine = Path.Combine(paths.PackageImagesPath, ".quarantine");
            Directory.CreateDirectory(quarantine);
            await File.WriteAllTextAsync(Path.Combine(quarantine, "old.png"), "old");
            await scope.ServiceProvider.GetRequiredService<BackupPackageWriter>().PruneAfterVerifiedUpload(CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(quarantine, "old.png")));
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static async Task SeedFixture(WebApplicationFactory<Program> factory, bool writeImage = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        Directory.CreateDirectory(storage.PackageImagesPath);
        Directory.CreateDirectory(storage.KeysPath);
        await File.WriteAllTextAsync(Path.Combine(storage.KeysPath, "key.xml"), "<key />");
        if (writeImage)
            await File.WriteAllBytesAsync(storage.UploadedPackageImage("backup.png"), Png());

        var package = new TravelPackage
        {
            Code = "B04-PKG",
            PackageName = "B04 Package",
            Destination = "Bohol",
            BasePrice = 7000,
            AvailableSlots = 9,
            ImagePath = "backup.png",
        };
        db.TravelPackages.Add(package);
        db.Bookings.Add(new Booking
        {
            Code = "B04-BOOK",
            BookingDate = new DateOnly(2026, 9, 1),
            PassengerCount = 2,
            Client = "B04 Client",
            PackageName = "B04 Package",
            Destination = "Bohol",
            GrossRevenue = 7000,
            PaymentStatus = "Paid",
            BookingStatus = "Confirmed",
        });
        db.Expenses.Add(new Expense
        {
            Code = "B04-EXP",
            ExpenseDate = new DateOnly(2026, 9, 2),
            Category = "Transport",
            Amount = 1200,
            PaymentStatus = "Paid",
        });
        db.AuditEntries.Add(new AuditEntry
        {
            At = DateTime.UtcNow,
            Actor = "owner@prophetops.local",
            ActorName = "Owner",
            Action = AuditLog.Created,
            EntityType = "Booking",
            EntityCode = "B04-BOOK",
            Summary = "backup fixture",
        });
        db.SaveChanges();
    }

    private static string CorruptDatabaseEntry(string packagePath)
    {
        var folder = Path.Combine(Path.GetTempPath(), "prophetops-corrupt-" + Guid.NewGuid().ToString("N"));
        var corrupt = Path.Combine(Path.GetTempPath(), "prophetops-corrupt-" + Guid.NewGuid().ToString("N") + ".zip");
        ZipFile.ExtractToDirectory(packagePath, folder);
        File.AppendAllText(Path.Combine(folder, "database", "prophetops.db"), "corruption");
        ZipFile.CreateFromDirectory(folder, corrupt);
        Directory.Delete(folder, recursive: true);
        return corrupt;
    }

    private static string RewriteManifest(string packagePath, Func<BackupManifest, BackupManifest> rewrite)
    {
        var folder = Path.Combine(Path.GetTempPath(), "prophetops-manifest-" + Guid.NewGuid().ToString("N"));
        var rewritten = Path.Combine(Path.GetTempPath(), "prophetops-manifest-" + Guid.NewGuid().ToString("N") + ".zip");
        ZipFile.ExtractToDirectory(packagePath, folder);
        var manifestPath = Path.Combine(folder, "manifest.json");
        var manifest = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), BackupJsonContext.Default.BackupManifest)!;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(rewrite(manifest), BackupJsonContext.Default.BackupManifest));
        ZipFile.CreateFromDirectory(folder, rewritten);
        Directory.Delete(folder, recursive: true);
        return rewritten;
    }

    private static void RestoreVerified(string packagePath, string destinationRoot, bool preserveSessions)
    {
        if (Directory.GetFileSystemEntries(destinationRoot).Any())
            throw new InvalidOperationException("Destination must be empty.");

        var staging = Path.Combine(Path.GetTempPath(), "prophetops-restore-test-" + Guid.NewGuid().ToString("N"));
        ValidateZipEntriesBeforeExtraction(packagePath, staging);
        ZipFile.ExtractToDirectory(packagePath, staging);
        try
        {
            var manifestPath = Path.Combine(staging, "manifest.json");
            var manifest = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), BackupJsonContext.Default.BackupManifest)
                ?? throw new InvalidOperationException("Manifest missing.");
            ValidateExtractedPackage(staging, manifest);
            foreach (var file in manifest.Files)
            {
                var path = SafePath(staging, file.Path);
                var info = new FileInfo(path);
                if (!File.Exists(path) || info.Length != file.Size
                    || BackupPackageWriter.Sha256File(path, CancellationToken.None).GetAwaiter().GetResult() != file.Sha256)
                    throw new InvalidOperationException("Package hash verification failed.");
            }
            if (!manifest.Files.Any(file => file.Path == manifest.Database.Path && file.Role == "sqlite-database"
                    && file.Sha256 == manifest.Database.Sha256 && file.Size == manifest.Database.Size))
                throw new InvalidOperationException("Database manifest entry is inconsistent.");
            if (Integrity(SafePath(staging, manifest.Database.Path)) != "ok")
                throw new InvalidOperationException("SQLite integrity failed.");
            ValidateCoreSchema(SafePath(staging, manifest.Database.Path), manifest);

            Directory.CreateDirectory(destinationRoot);
            File.Copy(SafePath(staging, manifest.Database.Path), Path.Combine(destinationRoot, "prophetops.db"));
            CopyTree(Path.Combine(staging, "uploads"), Path.Combine(destinationRoot, "uploads"));
            if (preserveSessions && Directory.Exists(Path.Combine(staging, "keys")))
                CopyTree(Path.Combine(staging, "keys"), Path.Combine(destinationRoot, "keys"));
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void ValidateZipEntriesBeforeExtraction(string packagePath, string staging)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Name)))
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.Contains("../", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe archive entry path.");
            if (!seen.Add(normalized)) throw new InvalidOperationException("Duplicate archive entry path.");
            _ = SafePath(staging, normalized);
            var allowedRoot = normalized.StartsWith("database/", StringComparison.Ordinal)
                || normalized.StartsWith("uploads/", StringComparison.Ordinal)
                || normalized.StartsWith("keys/", StringComparison.Ordinal)
                || normalized.StartsWith("config/", StringComparison.Ordinal)
                || normalized == "manifest.json";
            if (!allowedRoot) throw new InvalidOperationException("Unexpected archive entry.");
        }
    }

    private static void ValidateExtractedPackage(string staging, BackupManifest manifest)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (!seen.Add(file.Path)) throw new InvalidOperationException("Duplicate manifest path.");
            _ = SafePath(staging, file.Path);
        }

        var expected = manifest.Files.Select(file => file.Path).Append("manifest.json").ToHashSet(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(staging, path).Replace('\\', '/');
            var allowedRoot = relative.StartsWith("database/", StringComparison.Ordinal)
                || relative.StartsWith("uploads/", StringComparison.Ordinal)
                || relative.StartsWith("keys/", StringComparison.Ordinal)
                || relative.StartsWith("config/", StringComparison.Ordinal)
                || relative == "manifest.json";
            if (!allowedRoot || !expected.Contains(relative))
                throw new InvalidOperationException("Unexpected package entry: " + relative);
        }
    }

    private static string SafePath(string staging, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidOperationException("Manifest path must be relative.");
        var full = Path.GetFullPath(Path.Combine(staging, relative));
        var root = Path.GetFullPath(staging);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Manifest path escapes the package.");
        return full;
    }

    private static void ValidateCoreSchema(string dbPath, BackupManifest manifest)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
        connection.Open();
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            using var reader = command.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
        }
        foreach (var table in new[] { "Users", "TravelPackages", "Bookings", "Expenses", "AuditEntries", "__EFMigrationsHistory" })
            if (!tables.Contains(table)) throw new InvalidOperationException("Core schema table is missing.");

        var migrations = new HashSet<string>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) migrations.Add(reader.GetString(0));
        }
        if (manifest.EfMigrations.Count != migrations.Count || !manifest.EfMigrations.All(migrations.Contains))
            throw new InvalidOperationException("Migration history does not match the manifest.");
    }

    private static void CopyTree(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static AppDbContext Open(string dbPath) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()).Options);

    private static string Integrity(string dbPath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return command.ExecuteScalar() as string ?? "unknown";
    }

    private static byte[] Png()
    {
        var bytes = new byte[32];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    private static string TestKey(byte fill = 42) => Convert.ToBase64String(Enumerable.Repeat(fill, 32).ToArray());

    private static BackupManifest EmptyManifest() => new(
        BackupManifest.CurrentSchemaVersion,
        "empty",
        DateTimeOffset.UtcNow,
        "ProphetOps",
        "Testing",
        "restore-invalidates-sessions-by-default",
        new BackupEncryptionManifest(BackupEncryptionSettings.Algorithm, "test", "1"),
        new BackupDatabaseManifest("database/prophetops.db", 0, new string('0', 64), "ok"),
        [],
        new BackupCounts(0, 0, 0, 0, 0, 0),
        [],
        []);

    private sealed class BackupFactory : WebApplicationFactory<Program>
    {
        private readonly string _root;
        private readonly IBackupStorage? _storage;
        private readonly bool _hosted;
        private readonly string? _encryptionKey;
        private readonly IBackupPackageFileOperations? _operations;
        private readonly string? _backupStorageKind;
        private readonly IS3BackupClient? _objectStorage;

        public BackupFactory(string root, IBackupStorage? storage = null, bool hosted = false,
            string? encryptionKey = null, IBackupPackageFileOperations? operations = null,
            string? backupStorageKind = null, IS3BackupClient? objectStorage = null)
        {
            _root = root;
            _storage = storage;
            _hosted = hosted;
            _encryptionKey = encryptionKey;
            _operations = operations;
            _backupStorageKind = backupStorageKind;
            _objectStorage = objectStorage;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "true",
                ["Storage:Root"] = _root,
                ["Business:TimeZone"] = "Asia/Manila",
                ["Backup:Keep"] = "1",
                ["Backup:Scheduled:Enabled"] = "false",
                ["Backup:LocalStoragePath"] = Path.Combine(_root, "backups", "independent"),
                ["Hosted:Enabled"] = _hosted.ToString(),
                ["CloudflareAccess:Enabled"] = _hosted.ToString(),
                ["CloudflareAccess:Issuer"] = "https://agency.cloudflareaccess.com",
                ["CloudflareAccess:Audience"] = "audience",
                ["CloudflareAccess:JwksUrl"] = "https://access.example.test/certs",
                ["Backup:Encryption:Key"] = _encryptionKey,
                ["Backup:Encryption:KeyId"] = _encryptionKey is null ? null : "test-key",
                ["Backup:Storage"] = _backupStorageKind,
                ["Backup:S3:Endpoint"] = _backupStorageKind is null ? null : "http://object-storage.example.test",
                ["Backup:S3:AllowInsecureHttp"] = _backupStorageKind is null ? null : "true",
                ["Backup:S3:Bucket"] = _backupStorageKind is null ? null : "prophetops-backups",
                ["Backup:S3:Region"] = _backupStorageKind is null ? null : "auto",
                ["Backup:S3:AccessKeyId"] = _backupStorageKind is null ? null : "test-access-key",
                ["Backup:S3:SecretAccessKey"] = _backupStorageKind is null ? null : "test-secret-key",
                ["Backup:S3:Prefix"] = _backupStorageKind is null ? null : "release",
            }));
            if (_storage is not null)
            {
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IBackupStorage)).ToList())
                        services.Remove(descriptor);
                    services.AddScoped(_ => _storage);
                });
            }
            if (_operations is not null)
            {
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IBackupPackageFileOperations)).ToList())
                        services.Remove(descriptor);
                    services.AddScoped(_ => _operations);
                });
            }
            if (_objectStorage is not null)
            {
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IS3BackupClient)).ToList())
                        services.Remove(descriptor);
                    services.AddSingleton(_objectStorage);
                });
            }
        }
    }

    private sealed class FakeBackupStorage : IBackupStorage
    {
        public bool FailUpload { get; set; }
        public bool FailVerification { get; set; }
        public List<StoredBackup> Stored { get; } = [];
        public List<StoredBackup> Deleted { get; } = [];

        public Task<BackupUploadResult> UploadAsync(BackupPackage package, CancellationToken cancellationToken)
        {
            if (FailUpload) throw new BackupStorageUnavailable("expired credentials");
            var stored = new StoredBackup(Path.GetFileName(package.ArchivePath), DateTimeOffset.UtcNow,
                package.ArchivePath, package.Sha256, package.Size);
            Stored.Insert(0, stored);
            return Task.FromResult(new BackupUploadResult(stored.Name, stored.Path, stored.Sha256, stored.Size));
        }

        public Task<BackupVerificationResult> VerifyAsync(BackupUploadResult upload, BackupPackage package, CancellationToken cancellationToken) =>
            Task.FromResult(FailVerification
                ? new BackupVerificationResult(false, "verification failed")
                : new BackupVerificationResult(true));

        public Task<IReadOnlyList<StoredBackup>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredBackup>>(Stored.ToList());

        public Task DeleteAsync(StoredBackup backup, CancellationToken cancellationToken)
        {
            Deleted.Add(backup);
            Stored.RemoveAll(stored => stored.Name == backup.Name);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPackageOperations : IBackupPackageFileOperations
    {
        private readonly BackupPackageFileOperations _inner = new();
        public bool FailZip { get; set; }
        public bool FailEncryption { get; set; }

        public void CreateZip(string sourceDirectory, string destination)
        {
            if (FailZip) throw new IOException("simulated full disk");
            _inner.CreateZip(sourceDirectory, destination);
        }

        public Task EncryptFile(string plaintextPath, string encryptedPath, byte[] key, CancellationToken cancellationToken)
        {
            if (FailEncryption) throw new IOException("simulated encryption write failure");
            return _inner.EncryptFile(plaintextPath, encryptedPath, key, cancellationToken);
        }
    }

    private sealed class FakeS3BackupClient : IS3BackupClient
    {
        public Dictionary<string, StoredObject> Objects { get; } = new(StringComparer.Ordinal);
        public List<string> Operations { get; } = [];
        public bool CorruptReadHash { get; set; }
        public bool FailPut { get; set; }

        public async Task PutAsync(string key, BackupPackage package, CancellationToken cancellationToken)
        {
            Operations.Add("Put");
            if (FailPut) throw new IOException("object storage unavailable");
            Objects[key] = new StoredObject(
                await File.ReadAllBytesAsync(package.ArchivePath, cancellationToken),
                package.Sha256,
                DateTimeOffset.UtcNow);
        }

        public Task<S3BackupObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken)
        {
            Operations.Add("Metadata");
            return Task.FromResult(Objects.TryGetValue(key, out var stored)
                ? new S3BackupObjectMetadata(stored.Bytes.Length, stored.Sha256)
                : null);
        }

        public async Task<S3BackupObjectHash?> HashObjectAsync(string key, CancellationToken cancellationToken)
        {
            Operations.Add("Hash");
            if (!Objects.TryGetValue(key, out var stored)) return null;
            await using var stream = new MemoryStream(stored.Bytes);
            var hash = await BackupPackageWriter.Sha256Stream(stream, cancellationToken);
            return new S3BackupObjectHash(stored.Bytes.Length, CorruptReadHash ? new string('0', 64) : hash);
        }

        public Task<IReadOnlyList<S3BackupObject>> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            Operations.Add("List");
            return Task.FromResult<IReadOnlyList<S3BackupObject>>(Objects
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(pair => new S3BackupObject(pair.Key, pair.Value.CreatedUtc, pair.Value.Bytes.Length))
                .OrderByDescending(item => item.CreatedUtc)
                .ToList());
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Operations.Add("Delete");
            Objects.Remove(key);
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private sealed record StoredObject(byte[] Bytes, string Sha256, DateTimeOffset CreatedUtc);
}
