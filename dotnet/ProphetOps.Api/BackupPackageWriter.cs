using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed class BackupPackageWriter
{
    private readonly IServiceScopeFactory _scopes;
    private readonly StoragePaths _storage;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IBusinessClock _clock;
    private readonly MaintenanceGate _gate;
    private readonly IBackupStorage _backupStorage;
    private readonly IObjectStorage _objectStorage;
    private readonly IBackupPackageFileOperations _files;
    private readonly IDatabaseBackupCapture _databaseCapture;
    private readonly ILogger<BackupPackageWriter> _log;

    [ActivatorUtilitiesConstructor]
    public BackupPackageWriter(
        IServiceScopeFactory scopes,
        StoragePaths storage,
        IConfiguration configuration,
        IHostEnvironment environment,
        IBusinessClock clock,
        MaintenanceGate gate,
        IBackupStorage backupStorage,
        IObjectStorage objectStorage,
        IBackupPackageFileOperations files,
        IDatabaseBackupCapture databaseCapture,
        ILogger<BackupPackageWriter> log)
    {
        _scopes = scopes;
        _storage = storage;
        _configuration = configuration;
        _environment = environment;
        _clock = clock;
        _gate = gate;
        _backupStorage = backupStorage;
        _objectStorage = objectStorage;
        _files = files;
        _databaseCapture = databaseCapture;
        _log = log;
    }

    public BackupPackageWriter(
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
        : this(scopes, storage, configuration, environment, clock, gate, backupStorage, objectStorage, files,
               new SqliteBackupCapture(storage, NullLogger<SqliteBackupCapture>.Instance), log)
    {
    }

    public async Task<BackupRunResult> RunAsync(CancellationToken cancellationToken)
    {
        using var capture = await _gate.TryBeginBackupCaptureAsync(
            TimeSpan.FromSeconds(Math.Max(1, _configuration.GetValue("Backup:DrainTimeoutSeconds", 10))),
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
            upload = await _backupStorage.UploadAsync(package, cancellationToken);
            var verification = await _backupStorage.VerifyAsync(upload, package, cancellationToken);
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
        _log.LogInformation("Backup package completed: {PackageId} ({Kilobytes} KB).", package.PackageId, package.Size / 1024);
        return BackupRunResult.Completed(package, upload);
    }

    public async Task<BackupPackage?> CreateExportAsync(CancellationToken cancellationToken)
    {
        BackupEncryptionSettings encryption;
        try
        {
            encryption = BackupEncryptionSettings.FromConfiguration(_configuration);
            if (!encryption.Enabled)
            {
                _log.LogWarning("Manual backup export refused: Backup:Encryption:Key is not configured.");
                return null;
            }
        }
        catch (InvalidOperationException ex)
        {
            _log.LogWarning(ex, "Manual backup export refused: Backup:Encryption configuration is invalid.");
            return null;
        }

        using var capture = await _gate.TryBeginBackupCaptureAsync(
            TimeSpan.FromSeconds(Math.Max(1, _configuration.GetValue("Backup:DrainTimeoutSeconds", 10))),
            cancellationToken);
        if (capture is null)
        {
            _log.LogWarning("Manual backup export refused: Active writes did not drain before timeout.");
            return null;
        }

        BackupPackage? package;
        try
        {
            package = await CreatePackageUnderGate(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _log.LogWarning(ex, "Manual backup export package capture failed.");
            return null;
        }

        // Release the write gate lease BEFORE returning the package so mutations can proceed immediately
        capture.Dispose();

        if (package is null) return null;

        // Ensure export never returns an unencrypted archive
        if (!package.ArchivePath.EndsWith(BackupEncryptionSettings.EnvelopeExtension, StringComparison.OrdinalIgnoreCase))
        {
            DeleteQuietly(package.ArchivePath);
            _log.LogError("Manual backup export produced unencrypted archive '{Path}'. Export cancelled.", package.ArchivePath);
            return null;
        }

        _log.LogInformation("Manual backup export package prepared: {PackageId} ({Kilobytes} KB).", package.PackageId, package.Size / 1024);
        return package;
    }

    public async Task<BackupPackage?> CreatePackageUnderGate(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var provider = db.Database.ProviderName;
        if (!_databaseCapture.CanCapture(db))
        {
            throw new NotSupportedException($"Backup capture is not supported for provider '{provider}'.");
        }

        var stamp = _clock.UtcNow.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var packageId = "prophetops-" + stamp + "-" + Guid.NewGuid().ToString("N")[..8];
        var stagingRoot = Path.Combine(_storage.BackupStagingPath, "staging");
        var working = Path.Combine(stagingRoot, packageId + ".working");
        var encryption = BackupEncryptionSettings.FromConfiguration(_configuration);
        if (HostedRuntime.IsEnabled(_configuration) && !encryption.Enabled)
            throw new InvalidOperationException("Hosted independent backup upload requires Backup:Encryption:Key from outside the app host.");
        var archive = Path.Combine(_storage.BackupStagingPath, packageId + BackupEncryptionSettings.ZipExtension);
        var partial = archive + ".partial";
        string? encrypted = null;
        string? encryptedPartial = null;

        var stagingQuota = _configuration.GetValue<long?>("Backup:StagingMaxBytes")
            ?? (5L * 1024 * 1024 * 1024);
        if (stagingQuota <= 0)
        {
            throw new InvalidOperationException("Backup staging quota must be a positive number of bytes.");
        }

        if (Directory.Exists(working)) Directory.Delete(working, recursive: true);
        File.Delete(partial);
        Directory.CreateDirectory(working);

        try
        {
            await using var session = await _databaseCapture.StartCaptureAsync(db, working, cancellationToken);
            if (session is null) return null;

            var metadata = await session.GetMetadataAsync(cancellationToken);
            var imageFiles = await CopyReferencedImages(metadata.ReferencedImageKeys, working, partial, archive, stagingQuota, cancellationToken);
            var keyFiles = CopyDataProtectionKeys(working, partial, archive, stagingQuota);
            EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive), stagingQuota, "key and image staging");

            var config = ConfigurationInventory();

            var captureResult = await session.ExecuteDumpAsync(cancellationToken);
            var stagedDbManifest = captureResult.DatabaseManifest;
            EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive), stagingQuota, "database dump staging");

            var manifestFiles = new List<BackupFileManifest>
            {
                await ManifestFile(working, stagedDbManifest.Path, captureResult.FileRole, cancellationToken),
            };
            manifestFiles.AddRange(await Task.WhenAll(imageFiles.Select(path => ManifestFile(working, path, "package-image", cancellationToken))));
            manifestFiles.AddRange(await Task.WhenAll(keyFiles.Select(path => ManifestFile(working, path, "data-protection-key", cancellationToken))));

            var schemaVersion = string.Equals(stagedDbManifest.Provider, "postgres", StringComparison.OrdinalIgnoreCase)
                ? BackupManifest.SchemaVersion2
                : BackupManifest.SchemaVersion1;

            var manifest = new BackupManifest(
                schemaVersion,
                packageId,
                _clock.UtcNow,
                "ProphetOps",
                _environment.EnvironmentName,
                "restore-invalidates-sessions-by-default",
                EncryptionManifest(encryption),
                stagedDbManifest,
                manifestFiles.OrderBy(file => file.Path, StringComparer.Ordinal).ToList(),
                metadata.Counts,
                config,
                metadata.AppliedMigrations);

            manifest.Validate();

            var manifestJson = JsonSerializer.Serialize(manifest, BackupJsonContext.Default.BackupManifest);
            var manifestBytes = System.Text.Encoding.UTF8.GetByteCount(manifestJson);
            var baseBeforeManifest = CurrentCaptureStagingSize(working, partial, archive);
            if (baseBeforeManifest + manifestBytes > stagingQuota)
            {
                throw new InvalidOperationException(
                    $"Backup staging hard quota exceeded during manifest staging: projected staging size ({baseBeforeManifest + manifestBytes} bytes) exceeded configured limit of {stagingQuota} bytes.");
            }

            var manifestPath = Path.Combine(working, "manifest.json");
            await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);
            EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive), stagingQuota, "manifest staging");

            ValidateManifestFiles(working, manifest);
            var baseBeforeZip = CurrentCaptureStagingSize(working, partial, archive);
            _files.CreateZip(working, partial, stagingQuota, baseBeforeZip);
            EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive), stagingQuota, "zip packaging");

            File.Move(partial, archive, overwrite: true);
            EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive), stagingQuota, "archive finalization");

            if (encryption.Enabled)
            {
                encrypted = Path.Combine(_storage.BackupStagingPath, packageId + BackupEncryptionSettings.EnvelopeExtension);
                encryptedPartial = encrypted + ".partial";
                File.Delete(encryptedPartial);
                try
                {
                    var baseBeforeEnc = CurrentCaptureStagingSize(working, partial, archive);
                    await _files.EncryptFile(archive, encryptedPartial, encryption.Key!, stagingQuota, baseBeforeEnc, encryption.MaxInputBytes, cancellationToken);
                    EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive, encryptedPartial, encrypted), stagingQuota, "envelope encryption");
                    File.Move(encryptedPartial, encrypted, overwrite: true);
                    EnforceStagingQuota(CurrentCaptureStagingSize(working, partial, archive, null, encrypted), stagingQuota, "envelope finalization");
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
        catch
        {
            DeleteQuietly(archive);
            DeleteQuietly(partial);
            DeleteQuietly(encryptedPartial);
            DeleteQuietly(encrypted);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(working)) Directory.Delete(working, recursive: true);
                if (File.Exists(partial)) File.Delete(partial);
                if (encryptedPartial != null && File.Exists(encryptedPartial)) File.Delete(encryptedPartial);
            }
            catch (IOException)
            {
                _log.LogWarning("Backup staging cleanup could not finish.");
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
            await _backupStorage.DeleteAsync(new StoredBackup(upload.Name, _clock.UtcNow, upload.Path, upload.Sha256, upload.Size), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BackupStorageUnavailable)
        {
            _log.LogWarning("Failed backup upload could not be removed after verification failed.");
        }
    }

    private static void CleanupUploadedSourceArtifact(BackupPackage package, BackupUploadResult upload)
    {
        if (!File.Exists(upload.Path)
            || !string.Equals(Path.GetFullPath(package.ArchivePath), Path.GetFullPath(upload.Path), StringComparison.OrdinalIgnoreCase))
            DeleteQuietly(package.ArchivePath);
    }

    private static void DeleteQuietly(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
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
        var keep = Math.Max(1, _configuration.GetValue("Backup:Keep", 7));
        var backups = await _backupStorage.ListAsync(cancellationToken);
        foreach (var backup in backups.OrderByDescending(b => b.CreatedUtc).Skip(keep))
        {
            await _backupStorage.DeleteAsync(backup, cancellationToken);
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

    private static long CurrentCaptureStagingSize(string working, string partial, string archive, string? encryptedPartial = null, string? encrypted = null)
    {
        long total = 0;
        if (Directory.Exists(working))
        {
            total += PostgresProcessRunner.CalculateDirectorySize(working);
        }
        if (File.Exists(partial))
        {
            total += new FileInfo(partial).Length;
        }
        if (File.Exists(archive))
        {
            total += new FileInfo(archive).Length;
        }
        if (encryptedPartial != null && File.Exists(encryptedPartial))
        {
            total += new FileInfo(encryptedPartial).Length;
        }
        if (encrypted != null && File.Exists(encrypted))
        {
            total += new FileInfo(encrypted).Length;
        }
        return total;
    }

    private static void EnforceStagingQuota(long currentSize, long quotaBytes, string stepDescription)
    {
        if (currentSize > quotaBytes)
        {
            throw new InvalidOperationException(
                $"Backup staging hard quota exceeded during {stepDescription}: current staging size ({currentSize} bytes) exceeded configured limit of {quotaBytes} bytes.");
        }
    }

    private async Task<List<string>> CopyReferencedImages(
        IEnumerable<string> images,
        string working,
        string partial,
        string archive,
        long quotaBytes,
        CancellationToken cancellationToken)
    {
        var copied = new List<string>();
        foreach (var image in images)
        {
            await using var source = await _objectStorage.OpenReadAsync(image, cancellationToken);
            if (source is null)
                throw new FileNotFoundException($"A referenced package image is missing: '{image}'.");

            var baseSize = CurrentCaptureStagingSize(working, partial, archive);
            if (baseSize >= quotaBytes)
            {
                throw new InvalidOperationException(
                    $"Backup staging hard quota exceeded before package image staging: current staging size ({baseSize} bytes) reached or exceeded configured limit of {quotaBytes} bytes.");
            }

            var relative = Path.Combine("uploads", "packages", Path.GetFileName(image));
            var target = Path.Combine(working, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using (var fileStream = File.Create(target))
            await using (var quotaStream = new QuotaBoundedWriteStream(fileStream, baseSize, quotaBytes, "package image staging"))
            {
                await source.Content.CopyToAsync(quotaStream, cancellationToken);
            }
            copied.Add(relative);

            var currentSize = CurrentCaptureStagingSize(working, partial, archive);
            EnforceStagingQuota(currentSize, quotaBytes, "package image staging");
        }
        return copied;
    }

    private List<string> CopyDataProtectionKeys(string working, string partial, string archive, long quotaBytes)
    {
        var copied = new List<string>();
        if (!Directory.Exists(_storage.KeysPath)) return copied;
        foreach (var source in Directory.GetFiles(_storage.KeysPath, "*", SearchOption.TopDirectoryOnly))
        {
            var baseSize = CurrentCaptureStagingSize(working, partial, archive);
            var keyLen = new FileInfo(source).Length;
            if (baseSize + keyLen > quotaBytes)
            {
                throw new InvalidOperationException(
                    $"Backup staging hard quota exceeded during data protection key staging: projected staging size ({baseSize + keyLen} bytes) exceeded configured limit of {quotaBytes} bytes.");
            }

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
        new("business-timezone", string.IsNullOrWhiteSpace(_configuration["Business:TimeZone"]) ? "missing" : "configured", _configuration["Business:TimeZone"]),
        new("hosted-mode", HostedRuntime.IsEnabled(_configuration) ? "configured" : "not-configured"),
        new("cloudflare-access", _configuration.GetValue<bool>("CloudflareAccess:Enabled") ? "configured" : "not-configured"),
        new("forwarded-headers", ForwardedHeadersSetup.HasTrustedBoundary(_configuration) ? "configured" : "not-configured"),
        new("backup-encryption", BackupEncryptionSettings.FromConfiguration(_configuration).Enabled ? BackupEncryptionSettings.Algorithm : BackupEncryptionSettings.None),
        new("backup-storage", _configuration["Backup:Storage"] ?? "local"),
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
        var quarantine = Path.Combine(_storage.PackageImagesPath, ".quarantine");
        if (!Directory.Exists(quarantine)) return;
        HashSet<string> needed;
        try
        {
            needed = ReadReferencedImages(retainedPackages);
        }
        catch (Exception)
        {
            _log.LogWarning("Quarantined package images were retained because backup manifests could not be inspected.");
            return;
        }

        foreach (var file in Directory.GetFiles(quarantine))
        {
            if (needed.Contains(Path.GetFileName(file))) continue;
            try { File.Delete(file); }
            catch (IOException) { _log.LogWarning("Quarantined package image cleanup could not finish."); }
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
            manifest.Validate();
            foreach (var file in manifest.Files.Where(file => file.Role == "package-image"))
                needed.Add(Path.GetFileName(file.Path));
        }
        if (packages.Any(package => !File.Exists(package)))
            throw new InvalidOperationException("Remote backup package cannot be inspected from local storage.");
        return needed;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
