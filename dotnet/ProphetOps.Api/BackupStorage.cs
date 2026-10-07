using System.Globalization;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ProphetOps.Api;

public interface IBackupStorage
{
    Task<BackupUploadResult> UploadAsync(BackupPackage package, CancellationToken cancellationToken);
    Task<BackupVerificationResult> VerifyAsync(BackupUploadResult upload, BackupPackage package, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredBackup>> ListAsync(CancellationToken cancellationToken);
    Task DeleteAsync(StoredBackup backup, CancellationToken cancellationToken);
}

public sealed record BackupUploadResult(string Name, string Path, string Sha256, long Size);
public sealed record BackupVerificationResult(bool Verified, string? Error = null);
public sealed record StoredBackup(string Name, DateTimeOffset CreatedUtc, string Path, string Sha256, long Size);

public static class BackupStorageFactory
{
    public static IBackupStorage Create(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var storage = services.GetRequiredService<StoragePaths>();
        var kind = configuration["Backup:Storage"]?.Trim();
        if (string.IsNullOrWhiteSpace(kind) || string.Equals(kind, "local", StringComparison.OrdinalIgnoreCase))
            return new LocalBackupStorage(storage, configuration);

        if (string.Equals(kind, "s3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "r2", StringComparison.OrdinalIgnoreCase))
        {
            var options = S3BackupStorageOptions.FromConfiguration(configuration);
            var client = services.GetService<IS3BackupClient>();
            return new S3BackupStorage(
                options,
                client ?? new AwsS3BackupClient(options),
                services.GetRequiredService<ILogger<S3BackupStorage>>(),
                ownsClient: client is null);
        }

        throw new InvalidOperationException("Backup:Storage must be 'local', 's3', or 'r2'.");
    }

    public static void ValidateHostedSchedule(IConfiguration configuration)
    {
        if (!HostedRuntime.IsEnabled(configuration) || !BackupScheduleOptions.FromConfiguration(configuration).Enabled)
            return;

        var encryption = BackupEncryptionSettings.FromConfiguration(configuration);
        if (!encryption.Enabled)
            throw new InvalidOperationException("Hosted scheduled backups require Backup:Encryption:Key.");
        var kind = configuration["Backup:Storage"]?.Trim();
        if (!string.Equals(kind, "s3", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(kind, "r2", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hosted scheduled backups require Backup:Storage=s3 or Backup:Storage=r2.");
        _ = S3BackupStorageOptions.FromConfiguration(configuration);
    }
}

public sealed class LocalBackupStorage(StoragePaths storage, IConfiguration configuration) : IBackupStorage
{
    private readonly string _root = configuration["Backup:LocalStoragePath"] ?? Path.Combine(storage.BackupStagingPath, "independent");

    public async Task<BackupUploadResult> UploadAsync(BackupPackage package, CancellationToken cancellationToken)
    {
        if (HostedRuntime.IsEnabled(configuration))
            throw new BackupStorageUnavailable("Hosted backups require independently configured off-host storage.");

        Directory.CreateDirectory(_root);
        var name = Path.GetFileName(package.ArchivePath);

        var finished = Path.Combine(_root, name);
        var partial = finished + ".partial";
        File.Delete(partial);
        await using (var source = File.OpenRead(package.ArchivePath))
        await using (var target = File.Create(partial))
        {
            await source.CopyToAsync(target, cancellationToken);
        }
        File.Move(partial, finished, overwrite: true);
        return new BackupUploadResult(name, finished, await BackupPackageWriter.Sha256File(finished, cancellationToken), new FileInfo(finished).Length);
    }

    public Task<BackupVerificationResult> VerifyAsync(BackupUploadResult upload, BackupPackage package, CancellationToken cancellationToken)
    {
        if (!File.Exists(upload.Path))
            return Task.FromResult(new BackupVerificationResult(false, "Stored package is missing."));
        if (upload.Size != package.Size || !string.Equals(upload.Sha256, package.Sha256, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new BackupVerificationResult(false, "Stored package checksum does not match."));
        return Task.FromResult(new BackupVerificationResult(true));
    }

    public Task<IReadOnlyList<StoredBackup>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root)) return Task.FromResult<IReadOnlyList<StoredBackup>>([]);
        var backups = Directory.GetFiles(_root, "*.prophetops-backup.*")
            .Where(path => path.EndsWith(BackupEncryptionSettings.ZipExtension, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(BackupEncryptionSettings.EnvelopeExtension, StringComparison.OrdinalIgnoreCase))
            .Select(path => new StoredBackup(
                Path.GetFileName(path),
                File.GetCreationTimeUtc(path),
                path,
                BackupPackageWriter.Sha256File(path, cancellationToken).GetAwaiter().GetResult(),
                new FileInfo(path).Length))
            .OrderByDescending(backup => backup.CreatedUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<StoredBackup>>(backups);
    }

    public Task DeleteAsync(StoredBackup backup, CancellationToken cancellationToken)
    {
        if (File.Exists(backup.Path)) File.Delete(backup.Path);
        return Task.CompletedTask;
    }
}

public sealed class BackupStorageUnavailable : Exception
{
    public BackupStorageUnavailable(string message) : base(message) { }
    public BackupStorageUnavailable(string message, Exception inner) : base(message, inner) { }
}

public sealed record S3BackupStorageOptions(
    Uri Endpoint,
    string Bucket,
    string Region,
    string AccessKeyId,
    string SecretAccessKey,
    string Prefix,
    bool ForcePathStyle,
    int TimeoutSeconds)
{
    public static S3BackupStorageOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Backup:S3");
        var endpointText = section["Endpoint"]?.Trim();
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps
                && !section.GetValue("AllowInsecureHttp", false)))
            throw new InvalidOperationException("Backup:S3:Endpoint must be an absolute HTTPS URL.");

        var bucket = Required(section, "Bucket");
        var region = section["Region"]?.Trim();
        if (string.IsNullOrWhiteSpace(region)) region = "auto";

        var accessKeyId = Required(section, "AccessKeyId");
        var secretAccessKey = Required(section, "SecretAccessKey");
        var prefix = NormalizePrefix(section["Prefix"]);
        var forcePathStyle = section.GetValue("ForcePathStyle", true);
        var timeout = Math.Clamp(section.GetValue("TimeoutSeconds", 30), 5, 300);
        return new S3BackupStorageOptions(endpoint, bucket, region, accessKeyId, secretAccessKey, prefix, forcePathStyle, timeout);
    }

    private static string Required(IConfiguration section, string key)
    {
        var value = section[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Backup:S3:" + key + " must be configured.");
        return value;
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return "";
        return string.Join('/', prefix.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)) + "/";
    }
}

public interface IS3BackupClient : IDisposable
{
    Task PutAsync(string key, BackupPackage package, CancellationToken cancellationToken);
    Task<S3BackupObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken);
    Task<S3BackupObjectHash?> HashObjectAsync(string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<S3BackupObject>> ListAsync(string prefix, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed record S3BackupObjectMetadata(long Size, string? Sha256);
public sealed record S3BackupObjectHash(long Size, string Sha256);
public sealed record S3BackupObject(string Key, DateTimeOffset CreatedUtc, long Size);

public sealed class AwsS3BackupClient : IS3BackupClient
{
    private readonly AmazonS3Client _client;
    private readonly S3BackupStorageOptions _options;

    public AwsS3BackupClient(S3BackupStorageOptions options)
    {
        _options = options;
        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), CreateConfig(options));
    }

    public static AmazonS3Config CreateConfig(S3BackupStorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };
        if (!string.Equals(options.Region, "auto", StringComparison.OrdinalIgnoreCase))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
            config.AuthenticationRegion = options.Region;
        }
        else
        {
            config.AuthenticationRegion = "auto";
        }

        config.ServiceURL = options.Endpoint.ToString().TrimEnd('/');
        return config;
    }

    public async Task PutAsync(string key, BackupPackage package, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(package.ArchivePath);
        var request = CreatePutRequest(_options.Bucket, key, stream, package);
        await _client.PutObjectAsync(request, cancellationToken);
    }

    public static PutObjectRequest CreatePutRequest(string bucket, string key, Stream stream, BackupPackage package)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };
        request.Metadata.Add("sha256", package.Sha256);
        request.Metadata.Add("package-id", package.PackageId);
        return request;
    }

    public async Task<S3BackupObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key,
            }, cancellationToken);
            return new S3BackupObjectMetadata(response.ContentLength, Metadata(response, "sha256"));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<S3BackupObjectHash?> HashObjectAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
            }, cancellationToken);
            var sha = await BackupPackageWriter.Sha256Stream(response.ResponseStream, cancellationToken);
            return new S3BackupObjectHash(response.ContentLength, sha);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<S3BackupObject>> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        var objects = new List<S3BackupObject>();
        string? continuation = null;
        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                Prefix = prefix,
                ContinuationToken = continuation,
            }, cancellationToken);
            objects.AddRange(response.S3Objects.Select(item =>
            {
                var lastModified = item.LastModified.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(item.LastModified.Value, DateTimeKind.Utc))
                    : DateTimeOffset.UtcNow;
                return new S3BackupObject(item.Key, lastModified, item.Size.GetValueOrDefault());
            }));
            continuation = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (!string.IsNullOrEmpty(continuation));

        return objects;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    public void Dispose() => _client.Dispose();

    private static string? Metadata(GetObjectMetadataResponse response, string key)
    {
        foreach (var candidate in new[] { key, "x-amz-meta-" + key })
        {
            var value = response.Metadata[candidate];
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }
}

public sealed class S3BackupStorage(
    S3BackupStorageOptions options,
    IS3BackupClient client,
    ILogger<S3BackupStorage> log,
    bool ownsClient = false) : IBackupStorage, IDisposable
{
    public async Task<BackupUploadResult> UploadAsync(BackupPackage package, CancellationToken cancellationToken)
    {
        var key = ObjectKey(Path.GetFileName(package.ArchivePath));
        await GuardStorage("upload", () => client.PutAsync(key, package, cancellationToken));
        return new BackupUploadResult(Path.GetFileName(package.ArchivePath), RemotePath(key), package.Sha256, package.Size);
    }

    public async Task<BackupVerificationResult> VerifyAsync(BackupUploadResult upload, BackupPackage package, CancellationToken cancellationToken)
    {
        var key = KeyFromPath(upload.Path);
        var metadata = await GuardStorage("metadata", () => client.GetMetadataAsync(key, cancellationToken));
        if (metadata is null)
            return new BackupVerificationResult(false, "Stored package is missing.");
        if (metadata.Size != package.Size)
            return new BackupVerificationResult(false, "Stored package size does not match.");
        if (!string.Equals(metadata.Sha256, package.Sha256, StringComparison.OrdinalIgnoreCase))
            return new BackupVerificationResult(false, "Stored package checksum metadata does not match.");

        var content = await GuardStorage("content verification", () => client.HashObjectAsync(key, cancellationToken));
        if (content is null)
            return new BackupVerificationResult(false, "Stored package is missing.");
        if (content.Size != package.Size || !string.Equals(content.Sha256, package.Sha256, StringComparison.OrdinalIgnoreCase))
            return new BackupVerificationResult(false, "Stored package content checksum does not match.");

        return new BackupVerificationResult(true);
    }

    public async Task<IReadOnlyList<StoredBackup>> ListAsync(CancellationToken cancellationToken)
    {
        var objects = await GuardStorage("list", () => client.ListAsync(options.Prefix, cancellationToken));
        return objects
            .Select(item => new StoredBackup(Path.GetFileName(item.Key), item.CreatedUtc, RemotePath(item.Key), "", item.Size))
            .Where(backup => backup.Name.EndsWith(BackupEncryptionSettings.ZipExtension, StringComparison.OrdinalIgnoreCase)
                || backup.Name.EndsWith(BackupEncryptionSettings.EnvelopeExtension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(backup => backup.CreatedUtc)
            .ToList();
    }

    public async Task DeleteAsync(StoredBackup backup, CancellationToken cancellationToken)
    {
        await GuardStorage("delete", () => client.DeleteAsync(KeyFromPath(backup.Path), cancellationToken));
    }

    private string ObjectKey(string name) => options.Prefix + name;

    private string RemotePath(string key) => "s3://" + options.Bucket + "/" + key;

    private string KeyFromPath(string path)
    {
        var prefix = "s3://" + options.Bucket + "/";
        if (path.StartsWith(prefix, StringComparison.Ordinal))
            return path[prefix.Length..];
        return ObjectKey(Path.GetFileName(path));
    }

    private async Task GuardStorage(string operation, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (IsStorageException(ex))
        {
            log.LogWarning(ex, "S3 backup {Operation} failed.", operation);
            throw new BackupStorageUnavailable("S3 backup " + operation + " failed.", ex);
        }
    }

    private async Task<T> GuardStorage<T>(string operation, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (IsStorageException(ex))
        {
            log.LogWarning(ex, "S3 backup {Operation} failed.", operation);
            throw new BackupStorageUnavailable("S3 backup " + operation + " failed.", ex);
        }
    }

    private static bool IsStorageException(Exception ex) =>
        ex is AmazonServiceException or AmazonClientException or IOException or HttpRequestException or TaskCanceledException or TimeoutException;

    public void Dispose()
    {
        if (ownsClient) client.Dispose();
    }
}
