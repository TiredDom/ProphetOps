using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ProphetOps.Api;

public interface IObjectStorage
{
    Task PutAsync(string key, Stream body, string contentType, CancellationToken cancellationToken);
    Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, CancellationToken cancellationToken);
    Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed record StoredObject(Stream Content, string ContentType, long Length) : IAsyncDisposable, IDisposable
{
    public void Dispose() => Content.Dispose();

    public async ValueTask DisposeAsync() => await Content.DisposeAsync();
}

public sealed class ObjectStorageUnavailable : Exception
{
    public ObjectStorageUnavailable(string message) : base(message) { }
    public ObjectStorageUnavailable(string message, Exception inner) : base(message, inner) { }
}

public static class ObjectStorageFactory
{
    public static IObjectStorage Create(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var provider = configuration["ObjectStorage:Provider"]?.Trim();
        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "local", StringComparison.OrdinalIgnoreCase))
            return new LocalObjectStorage(services.GetRequiredService<StoragePaths>());

        if (string.Equals(provider, "supabase-s3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase))
        {
            var options = SupabaseS3ObjectStorageOptions.FromConfiguration(configuration);
            var client = services.GetService<ISupabaseS3ObjectClient>();
            return new SupabaseS3ObjectStorage(
                options,
                client ?? new AwsSupabaseS3ObjectClient(options),
                services.GetRequiredService<ILogger<SupabaseS3ObjectStorage>>(),
                ownsClient: client is null);
        }

        throw new InvalidOperationException("ObjectStorage:Provider must be 'local' or 'supabase-s3'.");
    }

    public static void ValidateHostedConfiguration(IConfiguration configuration)
    {
        if (!HostedRuntime.IsEnabled(configuration)) return;
        var databaseProvider = configuration["Database:Provider"]?.Trim();
        if (!string.Equals(databaseProvider, "postgres", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(databaseProvider, "postgresql", StringComparison.OrdinalIgnoreCase)) return;
        if (!string.Equals(configuration["Hosted:AccessMode"]?.Trim(), "ApplicationLogin", StringComparison.OrdinalIgnoreCase)) return;

        var provider = configuration["ObjectStorage:Provider"]?.Trim();
        if (!string.Equals(provider, "supabase-s3", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(provider, "s3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hosted PostgreSQL application-login mode requires ObjectStorage:Provider=supabase-s3.");

        var endpoint = configuration.GetSection("ObjectStorage:S3")["Endpoint"]?.Trim();
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Hosted object storage endpoint must be HTTPS.");
        _ = SupabaseS3ObjectStorageOptions.FromConfiguration(configuration);
    }
}

public sealed class LocalObjectStorage(StoragePaths storage, Action<string>? deleteFile = null) : IObjectStorage
{
    private readonly Action<string> _deleteFile = deleteFile ?? File.Delete;

    public async Task PutAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var partial = path + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var target = File.Create(partial))
            {
                await body.CopyToAsync(target, cancellationToken);
            }
            File.Move(partial, path, overwrite: true);
        }
        catch
        {
            DeleteQuietly(partial);
            throw;
        }
    }

    public async Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream target;
        try
        {
            target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }

        try
        {
            await using (target)
            {
                await body.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }
            return true;
        }
        catch
        {
            // Retain destination file conservatively on failure without unsafe deletion
            throw;
        }
    }

    public Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return Task.FromResult<StoredObject?>(null);
        var contentType = ImageUpload.ContentTypeFor(Path.GetFileName(path));
        if (contentType is null) return Task.FromResult<StoredObject?>(null);
        var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<StoredObject?>(new StoredObject(stream, contentType, stream.Length));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (File.Exists(path)) _deleteFile(path);
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var normalized = NormalizeKey(key);
        if (!normalized.Contains('/'))
            normalized = "packages/" + normalized;
        var full = Path.GetFullPath(Path.Combine(storage.UploadsPath, Path.Combine(normalized.Split('/'))));
        var root = storage.UploadsPath.EndsWith(Path.DirectorySeparatorChar)
            ? storage.UploadsPath
            : storage.UploadsPath + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Object storage keys must stay under the private uploads root.");
        return full;
    }

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || key.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(key))
            throw new InvalidOperationException("Object storage keys must be relative paths.");

        var parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
            throw new InvalidOperationException("Object storage keys must not contain traversal segments.");

        return string.Join('/', parts);
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
}

public sealed record SupabaseS3ObjectStorageOptions(
    Uri Endpoint,
    string Bucket,
    string Region,
    string AccessKeyId,
    string SecretAccessKey,
    string Prefix,
    bool ForcePathStyle,
    int TimeoutSeconds)
{
    public static SupabaseS3ObjectStorageOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("ObjectStorage:S3");
        var endpointText = section["Endpoint"]?.Trim();
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !section.GetValue("AllowInsecureHttp", false)))
            throw new InvalidOperationException("ObjectStorage:S3:Endpoint must be an absolute HTTPS URL.");

        return new SupabaseS3ObjectStorageOptions(
            endpoint,
            Required(section, "Bucket"),
            string.IsNullOrWhiteSpace(section["Region"]) ? "auto" : section["Region"]!.Trim(),
            Required(section, "AccessKeyId"),
            Required(section, "SecretAccessKey"),
            NormalizePrefix(section["Prefix"]),
            section.GetValue("ForcePathStyle", true),
            Math.Clamp(section.GetValue("TimeoutSeconds", 30), 5, 300));
    }

    private static string Required(IConfiguration section, string key)
    {
        var value = section[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("ObjectStorage:S3:" + key + " must be configured.");
        return value;
    }

    public static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return "";
        return string.Join('/', prefix.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)) + "/";
    }
}

public interface ISupabaseS3ObjectClient : IDisposable
{
    Task PutAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken);
    Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken);
    Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed class AwsSupabaseS3ObjectClient : ISupabaseS3ObjectClient
{
    private readonly AmazonS3Client _client;
    private readonly SupabaseS3ObjectStorageOptions _options;

    public AwsSupabaseS3ObjectClient(SupabaseS3ObjectStorageOptions options)
    {
        _options = options;
        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), CreateConfig(options));
    }

    public static AmazonS3Config CreateConfig(SupabaseS3ObjectStorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };
        if (string.Equals(options.Region, "auto", StringComparison.OrdinalIgnoreCase))
        {
            config.AuthenticationRegion = "auto";
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
            config.AuthenticationRegion = options.Region;
        }

        config.ServiceURL = options.Endpoint.ToString().TrimEnd('/');
        return config;
    }

    public Task PutAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken) =>
        _client.PutObjectAsync(SupabaseS3ObjectStorage.CreatePutRequest(_options.Bucket, key, body, contentType, length), cancellationToken);

    public async Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken)
    {
        var request = SupabaseS3ObjectStorage.CreatePutRequest(_options.Bucket, key, body, contentType, length);
        request.IfNoneMatch = "*";
        try
        {
            await _client.PutObjectAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed
            || string.Equals(ex.ErrorCode, "PreconditionFailed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    public async Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
            }, cancellationToken);
            return new StoredObject(new ResponseOwnedStream(response), response.Headers.ContentType, response.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
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

    private sealed class ResponseOwnedStream(GetObjectResponse response) : Stream
    {
        public override bool CanRead => response.ResponseStream.CanRead;
        public override bool CanSeek => response.ResponseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => response.ResponseStream.Length;
        public override long Position { get => response.ResponseStream.Position; set => response.ResponseStream.Position = value; }
        public override void Flush() => response.ResponseStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => response.ResponseStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => response.ResponseStream.Seek(offset, origin);
        public override void SetLength(long value) => response.ResponseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            await response.ResponseStream.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing) response.Dispose();
            base.Dispose(disposing);
        }
    }
}

public sealed class SupabaseS3ObjectStorage(
    SupabaseS3ObjectStorageOptions options,
    ISupabaseS3ObjectClient client,
    ILogger<SupabaseS3ObjectStorage> log,
    bool ownsClient = false) : IObjectStorage, IDisposable, IPrefixRecoverableObjectStorage
{
    public async Task PutAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
    {
        var normalized = ObjectKey(key);
        var length = body.CanSeek ? body.Length : -1;
        await Guard("upload", () => client.PutAsync(normalized, body, contentType, length, cancellationToken));
    }

    public async Task<bool> PutIfNotExistsAsync(string key, Stream body, string contentType, CancellationToken cancellationToken)
    {
        var normalized = ObjectKey(key);
        var length = body.CanSeek ? body.Length : -1;
        return await Guard("conditional-upload", () => client.PutIfNotExistsAsync(normalized, body, contentType, length, cancellationToken));
    }

    public SupabaseS3ObjectStorage WithPrefix(string newPrefix) =>
        new(options with { Prefix = SupabaseS3ObjectStorageOptions.NormalizePrefix(newPrefix) }, client, log, ownsClient: false);

    IObjectStorage IPrefixRecoverableObjectStorage.WithPrefix(string prefix) => WithPrefix(prefix);

    public async Task<StoredObject?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        await Guard("open", () => client.OpenReadAsync(ObjectKey(key), cancellationToken));

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await Guard("delete", () => client.DeleteAsync(ObjectKey(key), cancellationToken));

    public static PutObjectRequest CreatePutRequest(string bucket, string key, Stream stream, string contentType, long length)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
            CannedACL = S3CannedACL.Private,
        };
        request.Headers.ContentLength = length;
        return request;
    }

    private string ObjectKey(string key) => options.Prefix + LocalObjectStorage.NormalizeKey(key);

    private async Task Guard(string operation, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageException(ex))
        {
            LogSafeStorageWarning(operation, ex);
            throw new ObjectStorageUnavailable("Object storage " + operation + " failed.");
        }
    }

    private async Task<T> Guard<T>(string operation, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageException(ex))
        {
            LogSafeStorageWarning(operation, ex);
            throw new ObjectStorageUnavailable("Object storage " + operation + " failed.");
        }
    }

    private void LogSafeStorageWarning(string operation, Exception ex)
    {
        var exceptionType = ex.GetType().Name;
        var statusCode = (ex as AmazonServiceException)?.StatusCode;
        if (statusCode.HasValue)
        {
            log.LogWarning("Object storage {Operation} failed: {ExceptionType} (HTTP {StatusCode}).", operation, exceptionType, (int)statusCode.Value);
        }
        else
        {
            log.LogWarning("Object storage {Operation} failed: {ExceptionType}.", operation, exceptionType);
        }
    }

    private static bool IsStorageException(Exception ex) =>
        ex is AmazonServiceException or AmazonClientException or IOException or HttpRequestException or TaskCanceledException or TimeoutException;

    public void Dispose()
    {
        if (ownsClient) client.Dispose();
    }
}
