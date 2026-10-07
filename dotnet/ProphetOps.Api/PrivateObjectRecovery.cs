using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed record RecoverableImageRecord(
    [property: JsonPropertyName("databaseKey")] string DatabaseKey,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("contentType")] string? ContentType = null);

public sealed record PrivateObjectRecoveryManifest(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("databaseProvider")] string DatabaseProvider,
    [property: JsonPropertyName("stagedAtUtc")] DateTimeOffset StagedAtUtc,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("images")] IReadOnlyList<RecoverableImageRecord> Images);

public interface IPrefixRecoverableObjectStorage : IObjectStorage
{
    IObjectStorage WithPrefix(string prefix);
}

public sealed record PrivateObjectRecoveryOptions(
    string? ConfirmDestination,
    string? ConfirmProject = null,
    string? StagingPath = null,
    string? StagingManifestPath = null,
    string? RecoveryPrefix = null,
    int TimeoutSeconds = 300)
{
    public static PrivateObjectRecoveryOptions FromArgs(string[] args, IConfiguration configuration)
    {
        string? confirmDest = null;
        string? confirmProject = null;
        string? stagingPath = null;
        string? manifestPath = null;
        string? recoveryPrefix = null;
        var timeout = configuration.GetValue("ObjectStorage:Recovery:TimeoutSeconds", 300);

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--confirm-destination", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                confirmDest = args[++i].Trim();
            }
            else if (string.Equals(arg, "--confirm-project", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                confirmProject = args[++i].Trim();
            }
            else if (string.Equals(arg, "--staging-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                stagingPath = args[++i].Trim();
            }
            else if (string.Equals(arg, "--staging-manifest", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                manifestPath = args[++i].Trim();
            }
            else if (string.Equals(arg, "--recovery-prefix", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                recoveryPrefix = args[++i].Trim();
            }
            else if (string.Equals(arg, "--timeout-seconds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                && int.TryParse(args[++i], out var parsedTimeout))
            {
                timeout = Math.Clamp(parsedTimeout, 5, 3600);
            }
        }

        if (string.IsNullOrWhiteSpace(recoveryPrefix))
        {
            recoveryPrefix = configuration["ObjectStorage:S3:RecoveryPrefix"]?.Trim();
        }
        if (string.IsNullOrWhiteSpace(recoveryPrefix))
        {
            recoveryPrefix = configuration["ObjectStorage:S3:Prefix"]?.Trim();
        }

        return new PrivateObjectRecoveryOptions(confirmDest, confirmProject, stagingPath, manifestPath, recoveryPrefix, timeout);
    }
}

public sealed record PrivateObjectRecoveryResult(
    bool Success,
    string PackageId,
    string DestinationIdentity,
    int ExpectedObjectCount,
    int VerifiedObjectCount,
    string FailureCategory,
    string Message)
{
    public static PrivateObjectRecoveryResult Completed(string packageId, string destinationIdentity, int expected, int verified, string message) =>
        new(true, packageId, destinationIdentity, expected, verified, PrivateObjectRecoveryFailureCategories.None, message);

    public static PrivateObjectRecoveryResult Failed(string packageId, string destinationIdentity, int expected, int verified, string failureCategory, string message) =>
        new(false, packageId, destinationIdentity, expected, verified, failureCategory, message);
}

public static class PrivateObjectRecoveryFailureCategories
{
    public const string None = "None";
    public const string DestinationConfirmationMismatch = "Destination confirmation mismatch";
    public const string InvalidConfiguration = "Invalid configuration";
    public const string UnsupportedProviderCombination = "Unsupported provider combination";
    public const string ExclusiveCreateUnsupported = "Exclusive-create unsupported";
    public const string InvalidRecoveryMetadata = "Invalid recovery metadata";
    public const string InvalidKeyOrPath = "Invalid key or path";
    public const string TamperedOrCorruptedSourceFile = "Tampered or corrupted source file";
    public const string MissingSourceFile = "Missing source file";
    public const string DatabaseReferenceMismatch = "Database reference mismatch";
    public const string ConditionalCreateConflict = "Conditional-create conflict";
    public const string DestinationVerificationFailed = "Destination verification failed";
    public const string StorageUnavailable = "Storage unavailable";
    public const string Timeout = "Timeout";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Implements provider-aware offline private object recovery.
/// TRUST BOUNDARY:
/// Unencrypted backup archives and local operator staging are operator-controlled, not signed backups.
/// All staged images, paths, keys, lengths, and hashes are validated prior to any destination write.
/// Database references are validated read-only against the recovery set.
/// Writes enforce atomic no-overwrite conditional creation.
/// Retries on existing identical objects succeed only after full byte-for-byte readback verification.
/// Differing existing objects are treated as conflicts and are preserved unmodified without deletion.
/// </summary>
public sealed class PrivateObjectRecoveryRunner
{
    private const long MaxManifestFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    private const int MaxImageRecordsCount = 10000;
    private const long MaxSingleImageSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const long MaxTotalImagesSizeBytes = 10L * 1024 * 1024 * 1024; // 10 GB

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly StoragePaths _storagePaths;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<PrivateObjectRecoveryRunner> _log;
    private readonly TimeProvider _timeProvider;
    private readonly ISupabaseRecoveryObjectCreator? _recoveryObjectCreator;

    public PrivateObjectRecoveryRunner(
        AppDbContext db,
        IConfiguration configuration,
        StoragePaths storagePaths,
        IObjectStorage objectStorage,
        ILogger<PrivateObjectRecoveryRunner> log,
        TimeProvider? timeProvider = null,
        ISupabaseRecoveryObjectCreator? recoveryObjectCreator = null)
    {
        _db = db;
        _configuration = configuration;
        _storagePaths = storagePaths;
        _objectStorage = objectStorage;
        _log = log;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _recoveryObjectCreator = recoveryObjectCreator;
    }

    public PrivateObjectRecoveryRunner(
        AppDbContext db,
        IConfiguration configuration,
        StoragePaths storagePaths,
        IObjectStorage objectStorage,
        ILogger<PrivateObjectRecoveryRunner> log,
        ISupabaseRecoveryObjectCreator? recoveryObjectCreator)
        : this(db, configuration, storagePaths, objectStorage, log, null, recoveryObjectCreator)
    {
    }

    public async Task<PrivateObjectRecoveryResult> RunAsync(PrivateObjectRecoveryOptions options, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds), _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            return await ExecuteRecoveryAsync(options, ct);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _log.LogWarning("Private object recovery timed out after {TimeoutSeconds} seconds.", options.TimeoutSeconds);
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                options.ConfirmDestination ?? "unknown",
                0,
                0,
                PrivateObjectRecoveryFailureCategories.Timeout,
                $"Recovery operation timed out after {options.TimeoutSeconds} seconds.");
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("Private object recovery was cancelled by caller.");
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                options.ConfirmDestination ?? "unknown",
                0,
                0,
                PrivateObjectRecoveryFailureCategories.Cancelled,
                "Recovery operation was cancelled.");
        }
        catch (ObjectStorageUnavailable)
        {
            _log.LogWarning("Private object recovery storage unavailable.");
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                options.ConfirmDestination ?? "unknown",
                0,
                0,
                PrivateObjectRecoveryFailureCategories.StorageUnavailable,
                "Object storage destination is unavailable.");
        }
        catch (Exception ex)
        {
            var errorType = ex.GetType().Name;
            _log.LogError("Private object recovery encountered unexpected failure: {ErrorType}", errorType);
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                options.ConfirmDestination ?? "unknown",
                0,
                0,
                PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                $"Private object recovery failed: unexpected error of type {errorType}.");
        }
    }

    private async Task<PrivateObjectRecoveryResult> ExecuteRecoveryAsync(PrivateObjectRecoveryOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var configuredProvider = _configuration["ObjectStorage:Provider"]?.Trim() ?? "local";
        var isHosted = HostedRuntime.IsEnabled(_configuration);
        var dbProvider = _configuration["Database:Provider"]?.Trim() ?? "sqlite";
        var isPostgres = string.Equals(dbProvider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dbProvider, "postgresql", StringComparison.OrdinalIgnoreCase);

        // 1. Destination identity & confirmation verification
        string expectedDestinationIdentity;
        IObjectStorage effectiveStorage = _objectStorage;

        if (string.Equals(configuredProvider, "supabase-s3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuredProvider, "s3", StringComparison.OrdinalIgnoreCase))
        {
            var bucket = _configuration.GetSection("ObjectStorage:S3")["Bucket"]?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(bucket))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                    "ObjectStorage:S3:Bucket must be configured.");
            }

            if (bucket.Contains('/') || bucket.Contains('\\') || bucket.Contains("..") || bucket.Contains('%') || bucket.Any(char.IsWhiteSpace))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                    $"Invalid storage bucket '{bucket}': ambiguous, whitespace, or traversal characters are prohibited.");
            }

            var prefix = options.RecoveryPrefix;
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = _configuration.GetSection("ObjectStorage:S3")["RecoveryPrefix"]?.Trim();
            }
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = _configuration.GetSection("ObjectStorage:S3")["Prefix"]?.Trim();
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                    "Hosted S3 recovery requires a dedicated recovery destination prefix (configured via ObjectStorage:S3:RecoveryPrefix or --recovery-prefix).");
            }

            if (prefix.Contains('\\') || prefix.Contains("..") || prefix.Contains('%'))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                    $"Invalid recovery prefix '{prefix}': traversal or ambiguous characters are prohibited.");
            }

            var normalizedPrefix = SupabaseS3ObjectStorageOptions.NormalizePrefix(prefix);
            foreach (var segment in normalizedPrefix.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "." || segment == "..")
                {
                    return PrivateObjectRecoveryResult.Failed(
                        "unknown",
                        options.ConfirmDestination ?? "",
                        0, 0,
                        PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                        "Recovery prefix cannot contain dot or dot-dot path segments.");
                }
            }

            expectedDestinationIdentity = $"{configuredProvider.ToLowerInvariant()}:{bucket}/{normalizedPrefix}";

            if (!IsDestinationConfirmed(options.ConfirmDestination, configuredProvider, bucket, normalizedPrefix))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch,
                    $"Destination confirmation mismatch. Expected '{expectedDestinationIdentity}', but received '{options.ConfirmDestination ?? "(none)"}'.");
            }

            // Hosted recovery project confirmation check
            var (expectedProjectRef, projectError) = ExtractAndValidateProjectRef(_configuration);
            if (projectError != null || string.IsNullOrWhiteSpace(expectedProjectRef))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    expectedDestinationIdentity,
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.InvalidConfiguration,
                    projectError ?? "Failed to extract valid project reference from configured endpoints.");
            }

            if (string.IsNullOrWhiteSpace(options.ConfirmProject))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    expectedDestinationIdentity,
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch,
                    "Hosted recovery requires project confirmation (--confirm-project <project-ref>).");
            }

            if (!string.Equals(options.ConfirmProject, expectedProjectRef, StringComparison.Ordinal))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    expectedDestinationIdentity,
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch,
                    $"Project confirmation mismatch. Expected '{expectedProjectRef}', but received '{options.ConfirmProject}'.");
            }

            // Verify recovery creator destination matches runner destination
            if (_recoveryObjectCreator != null)
            {
                if (!string.Equals(_recoveryObjectCreator.Destination.Bucket, bucket, StringComparison.Ordinal) ||
                    !string.Equals(_recoveryObjectCreator.Destination.RecoveryPrefix, normalizedPrefix, StringComparison.Ordinal) ||
                    !string.Equals(_recoveryObjectCreator.Destination.ProjectRef, expectedProjectRef, StringComparison.Ordinal))
                {
                    return PrivateObjectRecoveryResult.Failed(
                        "unknown",
                        expectedDestinationIdentity,
                        0, 0,
                        PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch,
                        $"Recovery creator destination ({_recoveryObjectCreator.Destination.ProjectRef}:{_recoveryObjectCreator.Destination.Bucket}/{_recoveryObjectCreator.Destination.RecoveryPrefix}) does not match runner destination ({expectedProjectRef}:{bucket}/{normalizedPrefix}).");
                }
            }

            if (_objectStorage is SupabaseS3ObjectStorage s3Storage)
            {
                effectiveStorage = s3Storage.WithPrefix(normalizedPrefix);
            }
            else if (_objectStorage is IPrefixRecoverableObjectStorage prefixStorage)
            {
                effectiveStorage = prefixStorage.WithPrefix(normalizedPrefix);
            }
        }
        else if (string.Equals(configuredProvider, "local", StringComparison.OrdinalIgnoreCase))
        {
            if (isHosted || isPostgres)
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.UnsupportedProviderCombination,
                    "Hosted PostgreSQL recovery strictly requires ObjectStorage:Provider=supabase-s3. It refuses local object storage fallback.");
            }

            var localRoot = _storagePaths.UploadsPath;
            expectedDestinationIdentity = $"local:{localRoot}";

            if (!IsLocalDestinationConfirmed(options.ConfirmDestination, localRoot))
            {
                return PrivateObjectRecoveryResult.Failed(
                    "unknown",
                    options.ConfirmDestination ?? "",
                    0, 0,
                    PrivateObjectRecoveryFailureCategories.DestinationConfirmationMismatch,
                    $"Destination confirmation mismatch. Expected '{expectedDestinationIdentity}', but received '{options.ConfirmDestination ?? "(none)"}'.");
            }
        }
        else
        {
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                options.ConfirmDestination ?? "",
                0, 0,
                PrivateObjectRecoveryFailureCategories.UnsupportedProviderCombination,
                $"Unsupported ObjectStorage:Provider '{configuredProvider}'.");
        }

        // 2. Resolve and parse manifest
        var manifestPath = ResolveManifestPath(options);
        if (manifestPath is null || !File.Exists(manifestPath))
        {
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                expectedDestinationIdentity,
                0, 0,
                PrivateObjectRecoveryFailureCategories.MissingSourceFile,
                $"Recovery manifest was not found at '{manifestPath ?? "(null)"}'.");
        }

        // Bounded manifest file size check
        var manifestFileInfo = new FileInfo(manifestPath);
        if (manifestFileInfo.Length > MaxManifestFileSizeBytes)
        {
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                expectedDestinationIdentity,
                0, 0,
                PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata,
                $"Recovery manifest exceeds maximum permitted size of {MaxManifestFileSizeBytes} bytes.");
        }

        PrivateObjectRecoveryManifest recoveryManifest;
        string imagesBaseDir;
        try
        {
            var manifestBytes = await ReadAllBytesBoundedAsync(manifestPath, MaxManifestFileSizeBytes, ct);
            (recoveryManifest, imagesBaseDir) = ParseAndValidateManifest(manifestBytes, manifestPath, isPostgres, options.StagingPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorType = ex.GetType().Name;
            _log.LogError("Failed to parse recovery manifest: {ErrorType}", errorType);
            return PrivateObjectRecoveryResult.Failed(
                "unknown",
                expectedDestinationIdentity,
                0, 0,
                PrivateObjectRecoveryFailureCategories.InvalidRecoveryMetadata,
                $"Failed to parse recovery manifest ({errorType}).");
        }

        // 3. Strict 1-to-1 canonical key and path validation
        var seenKeysOrdinal = new HashSet<string>(StringComparer.Ordinal);
        var seenKeysCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPathsOrdinal = new HashSet<string>(StringComparer.Ordinal);
        var seenPathsCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedRelativeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var img in recoveryManifest.Images)
        {
            try
            {
                ValidateKeyString(img.DatabaseKey);
                ValidateRelativePath(img.RelativePath);
            }
            catch (Exception ex)
            {
                var errorType = ex.GetType().Name;
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    $"Invalid key or path in recovery manifest ({errorType}).");
            }

            var normalizedKey = LocalObjectStorage.NormalizeKey(img.DatabaseKey);
            if (!seenKeysOrdinal.Add(normalizedKey) || !seenKeysCase.Add(normalizedKey))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    $"Duplicate or aliased object key detected: '{img.DatabaseKey}'.");
            }

            var normRel = NormalizeRelativePathForComparison(img.RelativePath);
            if (!seenPathsOrdinal.Add(normRel) || !seenPathsCase.Add(normRel))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    $"Duplicate or aliased staged relative path detected: '{img.RelativePath}'.");
            }

            expectedRelativeFiles.Add(normRel);
        }

        // 4. Validate restored database references read-only (EXACT 1-to-1 matching)
        List<string> dbReferencedImages;
        try
        {
            dbReferencedImages = await _db.TravelPackages
                .AsNoTracking()
                .Where(p => p.ImagePath != null && p.ImagePath != "")
                .Select(p => p.ImagePath!)
                .Distinct()
                .ToListAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorType = ex.GetType().Name;
            _log.LogError("Database reference validation query failed: {ErrorType}", errorType);
            return PrivateObjectRecoveryResult.Failed(
                recoveryManifest.PackageId,
                expectedDestinationIdentity,
                recoveryManifest.Images.Count,
                0,
                PrivateObjectRecoveryFailureCategories.DatabaseReferenceMismatch,
                $"Database reference validation query failed with {errorType}.");
        }

        var normalizedDbKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dbKey in dbReferencedImages)
        {
            string normalizedDbKey;
            try
            {
                ValidateKeyString(dbKey);
                normalizedDbKey = LocalObjectStorage.NormalizeKey(dbKey);
                if (!string.Equals(dbKey, normalizedDbKey, StringComparison.Ordinal))
                {
                    return PrivateObjectRecoveryResult.Failed(
                        recoveryManifest.PackageId,
                        expectedDestinationIdentity,
                        recoveryManifest.Images.Count,
                        0,
                        PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                        "Database references non-canonical object key.");
                }
            }
            catch (Exception ex)
            {
                var errorType = ex.GetType().Name;
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    $"Invalid database image reference key ({errorType}).");
            }

            normalizedDbKeys.Add(normalizedDbKey);

            if (!seenKeysOrdinal.Contains(normalizedDbKey))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.DatabaseReferenceMismatch,
                    "Restored database references image missing from the validated recovery set.");
            }
        }

        // Exact match requirement: every recovery image must be referenced by the database (no orphan/unreferenced images)
        foreach (var img in recoveryManifest.Images)
        {
            var normKey = LocalObjectStorage.NormalizeKey(img.DatabaseKey);
            if (!normalizedDbKeys.Contains(normKey))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.DatabaseReferenceMismatch,
                    $"Recovery set contains image '{img.DatabaseKey}' which is not referenced by any package in the restored database.");
            }
        }

        // 5. Staging directory validation (symlinks, escapes, and unexpected files)
        // Must run even for zero-image packages before returning success!
        var imagesBaseDirFull = Path.GetFullPath(imagesBaseDir);
        var imagesBaseDirWithSep = imagesBaseDirFull.EndsWith(Path.DirectorySeparatorChar)
            ? imagesBaseDirFull
            : imagesBaseDirFull + Path.DirectorySeparatorChar;

        if (Directory.Exists(imagesBaseDirFull))
        {
            var rootAttr = File.GetAttributes(imagesBaseDirFull);
            if ((rootAttr & FileAttributes.ReparsePoint) != 0)
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    "Staging directory root is or contains a symbolic link or reparse point.");
            }

            var manifestFullPath = Path.GetFullPath(manifestPath);
            var stagingDirs = new Queue<string>();
            stagingDirs.Enqueue(imagesBaseDirFull);

            while (stagingDirs.Count > 0)
            {
                var currentDir = stagingDirs.Dequeue();
                foreach (var actualEntry in Directory.EnumerateFileSystemEntries(currentDir))
                {
                    if (string.Equals(actualEntry, manifestFullPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entryAttr = File.GetAttributes(actualEntry);
                    if ((entryAttr & FileAttributes.ReparsePoint) != 0)
                    {
                        return PrivateObjectRecoveryResult.Failed(
                            recoveryManifest.PackageId,
                            expectedDestinationIdentity,
                            recoveryManifest.Images.Count,
                            0,
                            PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                            $"Staged entry '{Path.GetFileName(actualEntry)}' is or contains a symbolic link or reparse point.");
                    }

                    if ((entryAttr & FileAttributes.Directory) != 0)
                    {
                        stagingDirs.Enqueue(actualEntry);
                    }
                    else
                    {
                        var rel = Path.GetRelativePath(imagesBaseDirFull, actualEntry);
                        var normRel = NormalizeRelativePathForComparison(rel);

                        if (!expectedRelativeFiles.Contains(normRel))
                        {
                            return PrivateObjectRecoveryResult.Failed(
                                recoveryManifest.PackageId,
                                expectedDestinationIdentity,
                                recoveryManifest.Images.Count,
                                0,
                                PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                                "Unexpected file found in staging directory.");
                        }
                    }
                }
            }
        }

        // Zero-image package handling: verified clean staging and clean DB references
        if (recoveryManifest.Images.Count == 0)
        {
            _log.LogInformation("Recovery manifest specifies 0 package images. Zero destination writes needed.");
            return PrivateObjectRecoveryResult.Completed(
                recoveryManifest.PackageId,
                expectedDestinationIdentity,
                0,
                0,
                "Package contains zero images. Database references verified.");
        }

        // 6. Pre-flight check staged files on disk (existence, length, hash)
        foreach (var img in recoveryManifest.Images)
        {
            var stagedFile = Path.GetFullPath(Path.Combine(imagesBaseDirFull, img.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!stagedFile.StartsWith(imagesBaseDirWithSep, StringComparison.OrdinalIgnoreCase))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    $"Staged image path '{img.RelativePath}' escapes staging directory.");
            }

            if (HasReparsePointOrSymlink(stagedFile, imagesBaseDirFull))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.InvalidKeyOrPath,
                    $"Staged image '{img.RelativePath}' is or contains a symbolic link or reparse point.");
            }

            if (!File.Exists(stagedFile))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.MissingSourceFile,
                    $"Staged image file '{img.RelativePath}' was not found.");
            }

            var fileInfo = new FileInfo(stagedFile);
            if (fileInfo.Length != img.Size)
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.TamperedOrCorruptedSourceFile,
                    $"Staged image '{img.RelativePath}' length mismatch (expected {img.Size}, found {fileInfo.Length}).");
            }

            var sourceHash = await ComputeSha256Async(stagedFile, ct);
            if (!string.Equals(sourceHash, img.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    0,
                    PrivateObjectRecoveryFailureCategories.TamperedOrCorruptedSourceFile,
                    $"Staged image '{img.RelativePath}' SHA-256 mismatch (expected {img.Sha256}, found {sourceHash}).");
            }
        }

        // 7. Hosted S3 Destination Write Safety Gate
        var isS3Provider = string.Equals(configuredProvider, "supabase-s3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuredProvider, "s3", StringComparison.OrdinalIgnoreCase);

        if (isS3Provider && _recoveryObjectCreator == null)
        {
            _log.LogError("Hosted S3 recovery is blocked: upstream Supabase S3 protocol handler unconditionally enables upsert (isUpsert: true) and does not enforce atomic IfNoneMatch conditional creation, and no native recovery object creator is configured.");
            return PrivateObjectRecoveryResult.Failed(
                recoveryManifest.PackageId,
                expectedDestinationIdentity,
                recoveryManifest.Images.Count,
                0,
                PrivateObjectRecoveryFailureCategories.ExclusiveCreateUnsupported,
                "Hosted S3 recovery is blocked: upstream S3 putObject handler enforces upsert (isUpsert: true). Destination writes fail closed without a configured native recovery helper.");
        }

        // 8. Execute atomic no-overwrite uploads and bounded readback verification (for supported exclusive providers like LocalObjectStorage or native recovery creator)
        int verifiedCount = 0;

        foreach (var img in recoveryManifest.Images)
        {
            var stagedFile = Path.GetFullPath(Path.Combine(imagesBaseDirFull, img.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var contentType = img.ContentType ?? ImageUpload.ContentTypeFor(stagedFile) ?? "application/octet-stream";
            var fileInfo = new FileInfo(stagedFile);

            bool created;
            try
            {
                await using var uploadStream = new FileStream(stagedFile, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                if (isS3Provider)
                {
                    created = await _recoveryObjectCreator!.TryCreateAsync(img.DatabaseKey, uploadStream, contentType, fileInfo.Length, ct);
                }
                else
                {
                    created = await effectiveStorage.PutIfNotExistsAsync(img.DatabaseKey, uploadStream, contentType, ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorType = ex.GetType().Name;
                _log.LogWarning("Conditional upload failed: {ErrorType}", errorType);
                return PrivateObjectRecoveryResult.Failed(
                    recoveryManifest.PackageId,
                    expectedDestinationIdentity,
                    recoveryManifest.Images.Count,
                    verifiedCount,
                    PrivateObjectRecoveryFailureCategories.StorageUnavailable,
                    $"Conditional upload failed ({errorType}).");
            }

            if (created)
            {
                // New object created: bounded readback verification
                StoredObject? stored;
                try
                {
                    stored = await effectiveStorage.OpenReadAsync(img.DatabaseKey, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var errorType = ex.GetType().Name;
                    _log.LogWarning("Readback open failed for {Key}: {ErrorType}", img.DatabaseKey, errorType);
                    return PrivateObjectRecoveryResult.Failed(
                        recoveryManifest.PackageId,
                        expectedDestinationIdentity,
                        recoveryManifest.Images.Count,
                        verifiedCount,
                        PrivateObjectRecoveryFailureCategories.DestinationVerificationFailed,
                        $"Destination readback open failed for '{img.DatabaseKey}'.");
                }

                if (stored is null)
                {
                    return PrivateObjectRecoveryResult.Failed(
                        recoveryManifest.PackageId,
                        expectedDestinationIdentity,
                        recoveryManifest.Images.Count,
                        verifiedCount,
                        PrivateObjectRecoveryFailureCategories.DestinationVerificationFailed,
                        $"Destination readback failed for '{img.DatabaseKey}': object was not found after upload.");
                }

                await using (stored)
                {
                    if (stored.Length >= 0 && stored.Length != img.Size)
                    {
                        return PrivateObjectRecoveryResult.Failed(
                            recoveryManifest.PackageId,
                            expectedDestinationIdentity,
                            recoveryManifest.Images.Count,
                            verifiedCount,
                            PrivateObjectRecoveryFailureCategories.DestinationVerificationFailed,
                            $"Destination readback length mismatch for '{img.DatabaseKey}' (expected {img.Size}, found {stored.Length}).");
                    }

                    var readbackResult = await StreamSha256BoundedAsync(stored.Content, img.Size, ct);
                    if (readbackResult.Exceeded || readbackResult.BytesRead != img.Size || !string.Equals(readbackResult.Hash, img.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return PrivateObjectRecoveryResult.Failed(
                            recoveryManifest.PackageId,
                            expectedDestinationIdentity,
                            recoveryManifest.Images.Count,
                            verifiedCount,
                            PrivateObjectRecoveryFailureCategories.DestinationVerificationFailed,
                            $"Destination readback SHA-256 or length mismatch for '{img.DatabaseKey}'.");
                    }
                }

                verifiedCount++;
            }
            else
            {
                // Object already existed: retry verification
                // Requirement 5: "On retry, an already existing object may be accepted only after full byte verification against the same expected image; a different object is a conflict and must remain unchanged."
                StoredObject? existing;
                try
                {
                    existing = await effectiveStorage.OpenReadAsync(img.DatabaseKey, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var errorType = ex.GetType().Name;
                    _log.LogWarning("Retry readback open failed for {Key}: {ErrorType}", img.DatabaseKey, errorType);
                    return PrivateObjectRecoveryResult.Failed(
                        recoveryManifest.PackageId,
                        expectedDestinationIdentity,
                        recoveryManifest.Images.Count,
                        verifiedCount,
                        PrivateObjectRecoveryFailureCategories.ConditionalCreateConflict,
                        $"Destination retry readback open failed for '{img.DatabaseKey}'.");
                }

                if (existing is null)
                {
                    return PrivateObjectRecoveryResult.Failed(
                        recoveryManifest.PackageId,
                        expectedDestinationIdentity,
                        recoveryManifest.Images.Count,
                        verifiedCount,
                        PrivateObjectRecoveryFailureCategories.ConditionalCreateConflict,
                        $"Existing destination object '{img.DatabaseKey}' could not be opened for verification.");
                }

                await using (existing)
                {
                    if (existing.Length >= 0 && existing.Length != img.Size)
                    {
                        // Differing object: conflict, preserve unchanged
                        _log.LogWarning("Destination object {Key} already exists with differing length ({ExistingLength} vs {ExpectedLength}). Preserving existing object unmodified.",
                            img.DatabaseKey, existing.Length, img.Size);
                        return PrivateObjectRecoveryResult.Failed(
                            recoveryManifest.PackageId,
                            expectedDestinationIdentity,
                            recoveryManifest.Images.Count,
                            verifiedCount,
                            PrivateObjectRecoveryFailureCategories.ConditionalCreateConflict,
                            $"Existing destination object '{img.DatabaseKey}' has differing length ({existing.Length} != {img.Size}). Preserved unmodified.");
                    }

                    var existingHashResult = await StreamSha256BoundedAsync(existing.Content, img.Size, ct);
                    if (existingHashResult.Exceeded || existingHashResult.BytesRead != img.Size || !string.Equals(existingHashResult.Hash, img.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        // Differing hash: conflict, preserve unchanged
                        _log.LogWarning("Destination object {Key} already exists with differing SHA-256 hash. Preserving existing object unmodified.", img.DatabaseKey);
                        return PrivateObjectRecoveryResult.Failed(
                            recoveryManifest.PackageId,
                            expectedDestinationIdentity,
                            recoveryManifest.Images.Count,
                            verifiedCount,
                            PrivateObjectRecoveryFailureCategories.ConditionalCreateConflict,
                            $"Existing destination object '{img.DatabaseKey}' has differing hash. Preserved unmodified.");
                    }
                }

                // Identical object verified at destination
                _log.LogInformation("Destination object {Key} already exists and has identical byte content and hash. Accepted on retry.", img.DatabaseKey);
                verifiedCount++;
            }
        }

        return PrivateObjectRecoveryResult.Completed(
            recoveryManifest.PackageId,
            expectedDestinationIdentity,
            recoveryManifest.Images.Count,
            verifiedCount,
            $"Private object recovery verified {verifiedCount}/{recoveryManifest.Images.Count} images at '{expectedDestinationIdentity}'.");
    }

    private string? ResolveManifestPath(PrivateObjectRecoveryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.StagingManifestPath))
        {
            return Path.GetFullPath(options.StagingManifestPath);
        }

        if (!string.IsNullOrWhiteSpace(options.StagingPath))
        {
            var manifestInStaging = Path.Combine(options.StagingPath, "recovery-manifest.json");
            if (File.Exists(manifestInStaging)) return manifestInStaging;
            return manifestInStaging;
        }

        var candidate1 = Path.Combine(_storagePaths.Root, "recovery", "recovery-manifest.json");
        if (File.Exists(candidate1)) return candidate1;

        var candidate2 = Path.Combine(_storagePaths.BackupStagingPath, "recovery", "recovery-manifest.json");
        if (File.Exists(candidate2)) return candidate2;

        var candidate3 = Path.Combine(_storagePaths.UploadsPath, "recovery-manifest.json");
        if (File.Exists(candidate3)) return candidate3;

        return candidate1;
    }

    private static void ValidateNoDuplicateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in element.EnumerateObject())
            {
                if (!seenProperties.Add(prop.Name))
                {
                    throw new InvalidOperationException($"Duplicate or case-aliased JSON property '{prop.Name}' detected.");
                }
                ValidateNoDuplicateJsonProperties(prop.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateNoDuplicateJsonProperties(item);
            }
        }
    }

    private static (PrivateObjectRecoveryManifest Manifest, string ImagesBaseDir) ParseAndValidateManifest(
        byte[] manifestBytes,
        string manifestPath,
        bool isPostgresTarget,
        string? explicitStagingDir)
    {
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        if (manifestBytes.AsSpan().StartsWith(utf8Bom))
        {
            manifestBytes = manifestBytes[3..];
        }
        using var doc = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions { MaxDepth = 16 });
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Recovery manifest root must be a JSON object.");

        ValidateNoDuplicateJsonProperties(root);

        var manifest = JsonSerializer.Deserialize<PrivateObjectRecoveryManifest>(manifestBytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize recovery manifest.");

        // Strict schema version validation
        if (manifest.SchemaVersion != 2)
            throw new InvalidOperationException($"Unsupported recovery manifest schema version {manifest.SchemaVersion}. Expected 2.");

        // Strict PackageId validation
        if (string.IsNullOrWhiteSpace(manifest.PackageId) || manifest.PackageId.Length > 128
            || !Regex.IsMatch(manifest.PackageId, @"^[a-zA-Z0-9_\-\.]+$"))
        {
            throw new InvalidOperationException($"Invalid recovery manifest PackageId '{manifest.PackageId}'. Must be 1-128 alphanumeric, hyphen, underscore, or period characters.");
        }

        // Strict bidirectional database provider validation
        if (string.IsNullOrWhiteSpace(manifest.DatabaseProvider))
            throw new InvalidOperationException("Recovery manifest database provider must not be empty.");

        var manifestProvider = manifest.DatabaseProvider.Trim();
        var isManifestPostgres = string.Equals(manifestProvider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifestProvider, "postgresql", StringComparison.OrdinalIgnoreCase);
        var isManifestSqlite = string.Equals(manifestProvider, "sqlite", StringComparison.OrdinalIgnoreCase);

        if (!isManifestPostgres && !isManifestSqlite)
            throw new InvalidOperationException($"Unsupported recovery manifest database provider '{manifestProvider}'.");

        if (isPostgresTarget && !isManifestPostgres)
            throw new InvalidOperationException($"Manifest database provider '{manifestProvider}' is incompatible with PostgreSQL target.");

        if (!isPostgresTarget && isManifestPostgres)
            throw new InvalidOperationException($"Manifest database provider '{manifestProvider}' is incompatible with SQLite target.");

        // Strict status validation
        if (!string.Equals(manifest.Status, "StagedOfflinePendingObjectStorageUpload", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unsupported recovery manifest status '{manifest.Status}'. Expected 'StagedOfflinePendingObjectStorageUpload'.");

        if (manifest.StagedAtUtc == default)
            throw new InvalidOperationException("Recovery manifest stagedAtUtc must be a valid non-default timestamp.");

        // Strict images collection bounds
        if (manifest.Images == null)
            throw new InvalidOperationException("Recovery manifest images collection cannot be null.");

        if (manifest.Images.Count > MaxImageRecordsCount)
            throw new InvalidOperationException($"Recovery manifest images count ({manifest.Images.Count}) exceeds maximum permitted ({MaxImageRecordsCount}).");

        long totalDeclaredBytes = 0;
        foreach (var img in manifest.Images)
        {
            if (img == null)
                throw new InvalidOperationException("Recovery manifest image record cannot be null.");

            if (string.IsNullOrWhiteSpace(img.DatabaseKey))
                throw new InvalidOperationException("Recovery manifest image databaseKey cannot be empty.");

            if (string.IsNullOrWhiteSpace(img.RelativePath))
                throw new InvalidOperationException("Recovery manifest image relativePath cannot be empty.");

            if (img.Size < 0 || img.Size > MaxSingleImageSizeBytes)
                throw new InvalidOperationException($"Recovery manifest image '{img.DatabaseKey}' has invalid size {img.Size} (must be between 0 and {MaxSingleImageSizeBytes} bytes).");

            totalDeclaredBytes += img.Size;
            if (totalDeclaredBytes > MaxTotalImagesSizeBytes)
                throw new InvalidOperationException($"Total declared images size exceeds maximum permitted limit ({MaxTotalImagesSizeBytes} bytes).");

            if (string.IsNullOrWhiteSpace(img.Sha256) || img.Sha256.Length != 64 || !Regex.IsMatch(img.Sha256, @"^[0-9a-f]{64}$"))
                throw new InvalidOperationException($"Recovery manifest image '{img.DatabaseKey}' has invalid SHA-256 syntax '{img.Sha256}'. Expected 64 lowercase hexadecimal characters.");
        }

        string imagesBaseDir;
        if (!string.IsNullOrWhiteSpace(explicitStagingDir))
        {
            imagesBaseDir = Path.GetFullPath(explicitStagingDir);
        }
        else
        {
            var stagedImagesDir = Path.Combine(manifestDir, "staged-images");
            imagesBaseDir = Directory.Exists(stagedImagesDir) ? stagedImagesDir : manifestDir;
        }

        return (manifest, imagesBaseDir);
    }

    private static async Task<byte[]> ReadAllBytesBoundedAsync(string filePath, long maxBytes, CancellationToken ct)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        if (fs.Length > maxBytes)
            throw new InvalidOperationException($"File exceeds maximum permitted size of {maxBytes} bytes.");

        using var ms = new MemoryStream((int)fs.Length);
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
                throw new InvalidOperationException($"File exceeds maximum permitted size of {maxBytes} bytes.");
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static readonly Regex ProjectApiHostRegex = new(@"^([a-z0-9-]+)\.supabase\.co$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ProjectStorageHostRegex = new(@"^([a-z0-9-]+)\.storage\.supabase\.co$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static (string? ProjectRef, string? Error) ExtractAndValidateProjectRef(IConfiguration configuration)
    {
        var s3EndpointStr = configuration["ObjectStorage:S3:Endpoint"]?.Trim();
        if (string.IsNullOrWhiteSpace(s3EndpointStr))
        {
            return (null, "ObjectStorage:S3:Endpoint must be configured.");
        }

        if (!Uri.TryCreate(s3EndpointStr, UriKind.Absolute, out var s3Uri) || s3Uri.Scheme != Uri.UriSchemeHttps)
        {
            return (null, "ObjectStorage:S3:Endpoint must be a valid HTTPS URL.");
        }

        if (!s3Uri.IsDefaultPort && s3Uri.Port != 443)
        {
            return (null, "ObjectStorage:S3:Endpoint must use the default HTTPS port (443).");
        }

        if (!string.IsNullOrEmpty(s3Uri.UserInfo) || !string.IsNullOrEmpty(s3Uri.Query) || !string.IsNullOrEmpty(s3Uri.Fragment))
        {
            return (null, "ObjectStorage:S3:Endpoint must not contain user credentials, queries, or fragments.");
        }

        var s3Path = s3Uri.AbsolutePath.TrimEnd('/');
        if (s3Path != "/storage/v1/s3")
        {
            return (null, "ObjectStorage:S3:Endpoint path must be '/storage/v1/s3'.");
        }

        string s3Ref;
        var storageHostMatch = ProjectStorageHostRegex.Match(s3Uri.Host);
        if (storageHostMatch.Success)
        {
            s3Ref = storageHostMatch.Groups[1].Value;
        }
        else
        {
            var apiHostMatch = ProjectApiHostRegex.Match(s3Uri.Host);
            if (apiHostMatch.Success)
            {
                s3Ref = apiHostMatch.Groups[1].Value;
            }
            else
            {
                return (null, $"Unrecognized or custom S3 endpoint host '{s3Uri.Host}'. Hosted recovery requires documented Supabase endpoint formats (<ref>.storage.supabase.co or <ref>.supabase.co).");
            }
        }

        var projectUrlStr = configuration["ObjectStorage:Recovery:SupabaseProjectUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(projectUrlStr))
        {
            projectUrlStr = configuration["ObjectStorage:Recovery:ProjectUrl"]?.Trim();
        }

        if (!string.IsNullOrWhiteSpace(projectUrlStr))
        {
            if (!Uri.TryCreate(projectUrlStr, UriKind.Absolute, out var projectUri) || projectUri.Scheme != Uri.UriSchemeHttps)
            {
                return (null, "ObjectStorage:Recovery:SupabaseProjectUrl must be a valid HTTPS URL.");
            }

            if (!projectUri.IsDefaultPort && projectUri.Port != 443)
            {
                return (null, "ObjectStorage:Recovery:SupabaseProjectUrl must use the default HTTPS port (443).");
            }

            if (!string.IsNullOrEmpty(projectUri.UserInfo) || !string.IsNullOrEmpty(projectUri.Query) || !string.IsNullOrEmpty(projectUri.Fragment))
            {
                return (null, "ObjectStorage:Recovery:SupabaseProjectUrl must not contain user credentials, queries, or fragments.");
            }

            if (projectUri.AbsolutePath != "/" && !string.IsNullOrEmpty(projectUri.AbsolutePath))
            {
                return (null, "ObjectStorage:Recovery:SupabaseProjectUrl must be a root project URL without path segments.");
            }

            var projectHostMatch = ProjectApiHostRegex.Match(projectUri.Host);
            if (!projectHostMatch.Success)
            {
                return (null, $"Recovery project URL host '{projectUri.Host}' is not a valid documented Supabase project host (<ref>.supabase.co).");
            }

            var projectRef = projectHostMatch.Groups[1].Value;
            if (!string.Equals(s3Ref, projectRef, StringComparison.Ordinal))
            {
                return (null, $"Recovery project URL ref '{projectRef}' does not match S3 endpoint ref '{s3Ref}'.");
            }
        }

        return (s3Ref, null);
    }

    private static bool IsDestinationConfirmed(string? confirmInput, string provider, string bucket, string prefix)
    {
        if (string.IsNullOrWhiteSpace(confirmInput)) return false;

        var input = confirmInput.Trim();
        var colonIdx = input.IndexOf(':');
        if (colonIdx <= 0) return false;

        var inputProvider = input[..colonIdx].Trim();
        var inputRemainder = input[(colonIdx + 1)..].Trim();

        var providerMatches = (string.Equals(inputProvider, "supabase-s3", StringComparison.OrdinalIgnoreCase) || string.Equals(inputProvider, "s3", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(provider, "supabase-s3", StringComparison.OrdinalIgnoreCase) || string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase));

        if (!providerMatches) return false;

        var expectedRemainder = $"{bucket}/{prefix}";
        return string.Equals(inputRemainder, expectedRemainder, StringComparison.Ordinal);
    }

    private static bool IsLocalDestinationConfirmed(string? confirmInput, string localRoot)
    {
        if (string.IsNullOrWhiteSpace(confirmInput)) return false;

        var input = confirmInput.Trim();
        var colonIdx = input.IndexOf(':');
        if (colonIdx <= 0) return false;

        var inputProvider = input[..colonIdx].Trim();
        if (!string.Equals(inputProvider, "local", StringComparison.OrdinalIgnoreCase)) return false;

        var inputPath = Path.GetFullPath(input[(colonIdx + 1)..].Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedPath = Path.GetFullPath(localRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(inputPath, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateKeyString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Key cannot be empty.");
        if (key.Length > 256)
            throw new InvalidOperationException("Key exceeds maximum length of 256 characters.");
        if (key.Contains('\\'))
            throw new InvalidOperationException("Backslashes are not permitted in object keys.");
        if (key.StartsWith('/') || key.StartsWith('\\') || Path.IsPathRooted(key) || Regex.IsMatch(key, @"^[a-zA-Z]:"))
            throw new InvalidOperationException("Rooted or drive-letter paths are not permitted in object keys.");

        var segments = key.Split('/', StringSplitOptions.None);
        if (segments.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("Empty segments or double slashes are not permitted in object keys.");
        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException("Traversal segments are not permitted in object keys.");

        if (!key.StartsWith("packages/", StringComparison.Ordinal))
            throw new InvalidOperationException("Database object keys must reside under 'packages/'.");
    }

    private static void ValidateRelativePath(string rel)
    {
        if (string.IsNullOrWhiteSpace(rel))
            throw new InvalidOperationException("Relative path cannot be empty.");
        if (rel.Length > 256)
            throw new InvalidOperationException("Relative path exceeds maximum length of 256 characters.");
        if (rel.Contains('\\'))
            throw new InvalidOperationException("Backslashes are not permitted in relative paths.");
        if (rel.StartsWith('/') || rel.StartsWith('\\') || Path.IsPathRooted(rel) || Regex.IsMatch(rel, @"^[a-zA-Z]:"))
            throw new InvalidOperationException("Rooted or drive-letter paths are not permitted in relative paths.");

        var segments = rel.Split('/', StringSplitOptions.None);
        if (segments.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("Empty segments or double slashes are not permitted in relative paths.");
        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException("Traversal segments are not permitted in relative paths.");
    }

    private static string NormalizeRelativePathForComparison(string path)
    {
        return string.Join('/', path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool HasReparsePointOrSymlink(string filePath, string rootPath)
    {
        var fullFile = Path.GetFullPath(filePath);
        var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var fileInfo = new FileInfo(fullFile);
        if (fileInfo.Exists)
        {
            if (fileInfo.LinkTarget is not null || (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }

        var dir = Path.GetDirectoryName(fullFile);
        while (dir != null && dir.Length >= fullRoot.Length)
        {
            var dirInfo = new DirectoryInfo(dir);
            if (dirInfo.Exists && (dirInfo.LinkTarget != null || (dirInfo.Attributes & FileAttributes.ReparsePoint) != 0))
                return true;

            if (string.Equals(dir, fullRoot, StringComparison.OrdinalIgnoreCase))
                break;

            dir = Path.GetDirectoryName(dir);
        }

        return false;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var res = await StreamSha256BoundedAsync(stream, long.MaxValue, ct);
        return res.Hash;
    }

    private static async Task<(string Hash, long BytesRead, bool Exceeded)> StreamSha256BoundedAsync(Stream stream, long expectedLength, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        long totalBytes = 0;
        int read;

        // Cap maximum bytes to read to expectedLength + 1 byte so overlong destination streams are detected without reading to EOF
        long maxToRead = expectedLength < long.MaxValue ? expectedLength + 1 : long.MaxValue;

        while (totalBytes < maxToRead && (read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maxToRead - totalBytes)), ct)) > 0)
        {
            sha256.TransformBlock(buffer, 0, read, null, 0);
            totalBytes += read;
        }

        if (expectedLength < long.MaxValue && totalBytes > expectedLength)
        {
            return ("", totalBytes, true); // Exceeded expected length
        }

        sha256.TransformFinalBlock([], 0, 0);
        var hash = Convert.ToHexStringLower(sha256.Hash!);
        return (hash, totalBytes, false);
    }
}
