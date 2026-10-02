using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class BackupPackageWriter(
    IServiceScopeFactory scopes,
    StoragePaths storage,
    IConfiguration configuration,
    IHostEnvironment environment,
    IBusinessClock clock,
    MaintenanceGate gate,
    IBackupStorage backupStorage,
    IObjectStorage objectStorage,
    IBackupPackageFileOperations files,
    ILogger<BackupPackageWriter> log)
{
    public async Task<BackupRunResult> RunAsync(CancellationToken cancellationToken)
    {
        using var capture = await gate.TryBeginBackupCaptureAsync(
            TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("Backup:DrainTimeoutSeconds", 10))),
            cancellationToken);
        if (capture is null) return BackupRunResult.Failed("Active writes did not drain before the backup timeout.");

        BackupPackage? package;
        try
        {
            package = await CreatePackageUnderGate(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BackupRunResult.Failed("Backup package capture failed.");
        }
        if (package is null) return BackupRunResult.Failed("No file-backed database is available to back up.");

        capture.Dispose();

        BackupUploadResult upload;
        try
        {
            upload = await backupStorage.UploadAsync(package, cancellationToken);
            var verification = await backupStorage.VerifyAsync(upload, package, cancellationToken);
            if (!verification.Verified)
            {
                CleanupFailedUploadArtifact(package);
                await TryDeleteFailedUpload(upload, cancellationToken);
                return BackupRunResult.Failed(verification.Error ?? "Stored backup verification failed.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BackupStorageUnavailable)
        {
            CleanupFailedUploadArtifact(package);
            return BackupRunResult.Failed("Independent backup upload failed.");
        }

        await PruneAfterVerifiedUpload(cancellationToken);
        CleanupUploadedSourceArtifact(package, upload);
        log.LogInformation("Backup package completed: {PackageId} ({Kilobytes} KB).", package.PackageId, package.Size / 1024);
        return BackupRunResult.Completed(package, upload);
    }

    public async Task<BackupPackage?> CreatePackageUnderGate(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var database = DatabasePath(db);
        if (database is null) return null;

        var stamp = clock.UtcNow.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var packageId = "prophetops-" + stamp + "-" + Guid.NewGuid().ToString("N")[..8];
        var stagingRoot = Path.Combine(storage.BackupStagingPath, "staging");
        var working = Path.Combine(stagingRoot, packageId + ".working");
        var encryption = BackupEncryptionSettings.FromConfiguration(configuration);
        if (HostedRuntime.IsEnabled(configuration) && !encryption.Enabled)
            throw new InvalidOperationException("Hosted independent backup upload requires Backup:Encryption:Key from outside the app host.");
        var archive = Path.Combine(storage.BackupStagingPath, packageId + BackupEncryptionSettings.ZipExtension);
        var partial = archive + ".partial";

        if (Directory.Exists(working)) Directory.Delete(working, recursive: true);
        File.Delete(partial);
        Directory.CreateDirectory(working);

        try
        {
            var dbRelative = Path.Combine("database", "prophetops.db");
            var stagedDb = Path.Combine(working, dbRelative);
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

            var imageFiles = await CopyReferencedImages(db, working, cancellationToken);
            var keyFiles = CopyDataProtectionKeys(working);
            var config = ConfigurationInventory();
            var migrations = await db.Database.GetAppliedMigrationsAsync(cancellationToken);

            var manifestFiles = new List<BackupFileManifest>
            {
                await ManifestFile(working, dbRelative, "sqlite-database", cancellationToken),
            };
            manifestFiles.AddRange(await Task.WhenAll(imageFiles.Select(path => ManifestFile(working, path, "package-image", cancellationToken))));
            manifestFiles.AddRange(await Task.WhenAll(keyFiles.Select(path => ManifestFile(working, path, "data-protection-key", cancellationToken))));

            var manifest = new BackupManifest(
                BackupManifest.CurrentSchemaVersion,
                packageId,
                clock.UtcNow,
                "ProphetOps",
                environment.EnvironmentName,
                "restore-invalidates-sessions-by-default",
                EncryptionManifest(encryption),
                new BackupDatabaseManifest(
                    Normalize(dbRelative),
                    new FileInfo(stagedDb).Length,
                    await Sha256File(stagedDb, cancellationToken),
                    integrity),
                manifestFiles.OrderBy(file => file.Path, StringComparer.Ordinal).ToList(),
                new BackupCounts(
                    db.Users.Count(),
                    db.TravelPackages.Count(),
                    db.Bookings.Count(),
                    db.Expenses.Count(),
                    db.AuditEntries.Count(),
                    imageFiles.Count),
                config,
                migrations.ToList());

            var manifestPath = Path.Combine(working, "manifest.json");
            await File.WriteAllTextAsync(manifestPath,
                JsonSerializer.Serialize(manifest, BackupJsonContext.Default.BackupManifest),
                cancellationToken);

            ValidateManifestFiles(working, manifest);
            files.CreateZip(working, partial);
            File.Move(partial, archive, overwrite: true);

            if (encryption.Enabled)
            {
                var encrypted = Path.Combine(storage.BackupStagingPath, packageId + BackupEncryptionSettings.EnvelopeExtension);
                var encryptedPartial = encrypted + ".partial";
                File.Delete(encryptedPartial);
                try
                {
                    await files.EncryptFile(archive, encryptedPartial, encryption.Key!, cancellationToken);
                    File.Move(encryptedPartial, encrypted, overwrite: true);
                }
                catch
                {
                    DeleteQuietly(archive);
                    DeleteQuietly(encryptedPartial);
                    DeleteQuietly(encrypted);
                    throw;
                }
                DeleteQuietly(archive);
                archive = encrypted;
            }

            return new BackupPackage(packageId, archive, await Sha256File(archive, cancellationToken),
                new FileInfo(archive).Length, manifest);
        }
        finally
        {
            try
            {
                if (Directory.Exists(working)) Directory.Delete(working, recursive: true);
                if (File.Exists(partial)) File.Delete(partial);
            }
            catch (IOException)
            {
                log.LogWarning("Backup staging cleanup could not finish.");
            }
        }
    }

    private static void CleanupFailedUploadArtifact(BackupPackage package)
    {
        if (!package.ArchivePath.EndsWith(BackupEncryptionSettings.EnvelopeExtension, StringComparison.OrdinalIgnoreCase))
            DeleteQuietly(package.ArchivePath);
    }

    private async Task TryDeleteFailedUpload(BackupUploadResult upload, CancellationToken cancellationToken)
    {
        try
        {
            await backupStorage.DeleteAsync(new StoredBackup(upload.Name, clock.UtcNow, upload.Path, upload.Sha256, upload.Size), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BackupStorageUnavailable)
        {
            log.LogWarning("Failed backup upload could not be removed after verification failed.");
        }
    }

    private static void CleanupUploadedSourceArtifact(BackupPackage package, BackupUploadResult upload)
    {
        if (!File.Exists(upload.Path)
            || !string.Equals(Path.GetFullPath(package.ArchivePath), Path.GetFullPath(upload.Path), StringComparison.OrdinalIgnoreCase))
            DeleteQuietly(package.ArchivePath);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public async Task PruneAfterVerifiedUpload(CancellationToken cancellationToken)
    {
        var keep = Math.Max(1, configuration.GetValue("Backup:Keep", 7));
        var backups = await backupStorage.ListAsync(cancellationToken);
        foreach (var backup in backups.OrderByDescending(b => b.CreatedUtc).Skip(keep))
        {
            await backupStorage.DeleteAsync(backup, cancellationToken);
        }
        PruneQuarantinedImages(backups.Take(keep).Select(b => b.Path).ToList());
    }

    public static async Task<string> Sha256File(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await Sha256Stream(stream, cancellationToken);
    }

    public static async Task<string> Sha256Stream(Stream stream, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string? DatabasePath(AppDbContext db)
    {
        var connection = new SqliteConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
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

    private async Task<List<string>> CopyReferencedImages(AppDbContext db, string working, CancellationToken cancellationToken)
    {
        var copied = new List<string>();
        var images = db.TravelPackages
            .AsNoTracking()
            .Where(package => package.ImagePath != null)
            .Select(package => package.ImagePath!)
            .Distinct()
            .ToList();

        foreach (var image in images)
        {
            await using var source = await objectStorage.OpenReadAsync(image, cancellationToken);
            if (source is null)
                throw new FileNotFoundException("A referenced package image is missing.");
            var relative = Path.Combine("uploads", "packages", Path.GetFileName(image));
            var target = Path.Combine(working, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var output = File.Create(target))
            {
                await source.Content.CopyToAsync(output, cancellationToken);
            }
            copied.Add(relative);
        }
        return copied;
    }

    private List<string> CopyDataProtectionKeys(string working)
    {
        var copied = new List<string>();
        if (!Directory.Exists(storage.KeysPath)) return copied;
        foreach (var source in Directory.GetFiles(storage.KeysPath, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(source);
            var relative = Path.Combine("keys", name);
            var target = Path.Combine(working, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            copied.Add(relative);
        }
        return copied;
    }

    private IReadOnlyList<BackupConfigurationFact> ConfigurationInventory() =>
    [
        new("storage-root", "configured", "redacted"),
        new("business-timezone", string.IsNullOrWhiteSpace(configuration["Business:TimeZone"]) ? "missing" : "configured", configuration["Business:TimeZone"]),
        new("hosted-mode", HostedRuntime.IsEnabled(configuration) ? "configured" : "not-configured"),
        new("cloudflare-access", configuration.GetValue<bool>("CloudflareAccess:Enabled") ? "configured" : "not-configured"),
        new("forwarded-headers", ForwardedHeadersSetup.HasTrustedBoundary(configuration) ? "configured" : "not-configured"),
        new("backup-encryption", BackupEncryptionSettings.FromConfiguration(configuration).Enabled ? BackupEncryptionSettings.Algorithm : BackupEncryptionSettings.None),
        new("backup-storage", configuration["Backup:Storage"] ?? "local"),
    ];

    private BackupEncryptionManifest EncryptionManifest(BackupEncryptionSettings settings)
    {
        return settings.Enabled
            ? new BackupEncryptionManifest(BackupEncryptionSettings.Algorithm, settings.KeyId, "1")
            : new BackupEncryptionManifest(BackupEncryptionSettings.None, null);
    }

    private static async Task<BackupFileManifest> ManifestFile(string root, string relative, string role, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, relative);
        var info = new FileInfo(path);
        return new BackupFileManifest(
            Normalize(relative),
            role,
            info.Length,
            await Sha256File(path, cancellationToken),
            info.LastWriteTimeUtc);
    }

    private static void ValidateManifestFiles(string root, BackupManifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            var path = Path.GetFullPath(Path.Combine(root, file.Path));
            var fullRoot = Path.GetFullPath(root);
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidOperationException("Backup manifest references a missing file.");
        }
    }

    private void PruneQuarantinedImages(IReadOnlyList<string> retainedPackages)
    {
        var quarantine = Path.Combine(storage.PackageImagesPath, ".quarantine");
        if (!Directory.Exists(quarantine)) return;
        HashSet<string> needed;
        try
        {
            needed = ReadReferencedImages(retainedPackages);
        }
        catch (Exception)
        {
            log.LogWarning("Quarantined package images were retained because backup manifests could not be inspected.");
            return;
        }

        foreach (var file in Directory.GetFiles(quarantine))
        {
            if (needed.Contains(Path.GetFileName(file))) continue;
            try { File.Delete(file); }
            catch (IOException) { log.LogWarning("Quarantined package image cleanup could not finish."); }
        }
    }

    private static HashSet<string> ReadReferencedImages(IEnumerable<string> packages)
    {
        var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages.Where(File.Exists))
        {
            if (package.EndsWith(BackupEncryptionSettings.EnvelopeExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Encrypted backup package cannot be inspected without an operator key.");
            using var archive = ZipFile.OpenRead(package);
            var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("Backup package has no manifest.");
            using var stream = manifestEntry.Open();
            var manifest = JsonSerializer.Deserialize(stream, BackupJsonContext.Default.BackupManifest)
                ?? throw new InvalidOperationException("Backup package manifest is invalid.");
            foreach (var file in manifest.Files.Where(file => file.Role == "package-image"))
                needed.Add(Path.GetFileName(file.Path));
        }
        if (packages.Any(package => !File.Exists(package)))
            throw new InvalidOperationException("Remote backup package cannot be inspected from local storage.");
        return needed;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string PathOnly(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
}
