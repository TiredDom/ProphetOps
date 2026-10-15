using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ProphetOps.Api;

public sealed record SupabaseRecoveryDestination(
    string ProjectRef,
    string Bucket,
    string RecoveryPrefix);

public interface ISupabaseRecoveryObjectCreator
{
    SupabaseRecoveryDestination Destination { get; }
    Task<bool> TryCreateAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken);
}

public sealed partial class SupabaseRecoveryObjectCreator : ISupabaseRecoveryObjectCreator, IDisposable
{
    private const int MaxErrorResponseBodyBytes = 65536; // 64 KiB
    private static readonly Regex ApiHostRegex = new(@"^([a-z0-9-]+)\.supabase\.co$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StorageHostRegex = new(@"^([a-z0-9-]+)\.storage\.supabase\.co$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly Uri _baseEndpoint;
    private readonly string _apiKey;
    private readonly bool _isLegacyJwt;
    private readonly bool _disposeClient;
    private readonly TimeSpan _perRequestTimeout;

    public SupabaseRecoveryDestination Destination { get; }

    public SupabaseRecoveryObjectCreator(
        IConfiguration configuration,
        string? recoveryApiKey = null,
        HttpClient? httpClient = null,
        string? recoveryPrefix = null,
        TimeSpan? perRequestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredProvider = configuration["ObjectStorage:Provider"]?.Trim() ?? "local";
        var isS3 = string.Equals(configuredProvider, "supabase-s3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuredProvider, "s3", StringComparison.OrdinalIgnoreCase);

        if (!isS3)
        {
            throw new InvalidOperationException($"SupabaseRecoveryObjectCreator is only valid for hosted S3 providers, not '{configuredProvider}'.");
        }

        // 1. Validate S3 Endpoint
        var s3EndpointStr = configuration["ObjectStorage:S3:Endpoint"]?.Trim();
        if (string.IsNullOrWhiteSpace(s3EndpointStr))
        {
            throw new InvalidOperationException("ObjectStorage:S3:Endpoint must be configured.");
        }

        if (!Uri.TryCreate(s3EndpointStr, UriKind.Absolute, out var s3Uri) || s3Uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("ObjectStorage:S3:Endpoint must be a valid HTTPS URL.");
        }

        if (!s3Uri.IsDefaultPort && s3Uri.Port != 443)
        {
            throw new InvalidOperationException("ObjectStorage:S3:Endpoint must use the default HTTPS port (443).");
        }

        if (!string.IsNullOrEmpty(s3Uri.UserInfo) || !string.IsNullOrEmpty(s3Uri.Query) || !string.IsNullOrEmpty(s3Uri.Fragment))
        {
            throw new InvalidOperationException("ObjectStorage:S3:Endpoint must not contain user credentials, queries, or fragments.");
        }

        var s3Path = s3Uri.AbsolutePath.TrimEnd('/');
        if (s3Path != "/storage/v1/s3")
        {
            throw new InvalidOperationException("ObjectStorage:S3:Endpoint path must be '/storage/v1/s3'.");
        }

        string s3Ref;
        var storageHostMatch = StorageHostRegex.Match(s3Uri.Host);
        if (storageHostMatch.Success)
        {
            s3Ref = storageHostMatch.Groups[1].Value;
        }
        else
        {
            var apiHostMatch = ApiHostRegex.Match(s3Uri.Host);
            if (apiHostMatch.Success)
            {
                s3Ref = apiHostMatch.Groups[1].Value;
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized or custom S3 endpoint host '{s3Uri.Host}'. Hosted recovery requires documented Supabase endpoint formats (<ref>.storage.supabase.co or <ref>.supabase.co).");
            }
        }

        // 2. Validate Project URL
        var projectUrlStr = configuration["ObjectStorage:Recovery:SupabaseProjectUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(projectUrlStr))
        {
            projectUrlStr = configuration["ObjectStorage:Recovery:ProjectUrl"]?.Trim();
        }
        if (string.IsNullOrWhiteSpace(projectUrlStr))
        {
            throw new InvalidOperationException("ObjectStorage:Recovery:SupabaseProjectUrl must be configured for hosted private object recovery.");
        }

        if (!Uri.TryCreate(projectUrlStr, UriKind.Absolute, out var projectUri) || projectUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("ObjectStorage:Recovery:SupabaseProjectUrl must be a valid HTTPS URL.");
        }

        if (!projectUri.IsDefaultPort && projectUri.Port != 443)
        {
            throw new InvalidOperationException("ObjectStorage:Recovery:SupabaseProjectUrl must use the default HTTPS port (443).");
        }

        if (!string.IsNullOrEmpty(projectUri.UserInfo) || !string.IsNullOrEmpty(projectUri.Query) || !string.IsNullOrEmpty(projectUri.Fragment))
        {
            throw new InvalidOperationException("ObjectStorage:Recovery:SupabaseProjectUrl must not contain user credentials, queries, or fragments.");
        }

        if (projectUri.AbsolutePath != "/" && !string.IsNullOrEmpty(projectUri.AbsolutePath))
        {
            throw new InvalidOperationException("ObjectStorage:Recovery:SupabaseProjectUrl must be a root project URL without path segments.");
        }

        var projectHostMatch = ApiHostRegex.Match(projectUri.Host);
        if (!projectHostMatch.Success)
        {
            throw new InvalidOperationException($"Recovery project URL host '{projectUri.Host}' is not a valid documented Supabase project host (<ref>.supabase.co).");
        }
        var projectRef = projectHostMatch.Groups[1].Value;

        // Prove project identity matches between S3 endpoint and API project URL
        if (!string.Equals(s3Ref, projectRef, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Recovery project URL ref '{projectRef}' does not match S3 endpoint ref '{s3Ref}'.");
        }

        // 3. Validate Bucket and Prefix
        var bucket = configuration["ObjectStorage:S3:Bucket"]?.Trim();
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidOperationException("ObjectStorage:S3:Bucket must be configured.");
        }
        if (bucket.Contains('/') || bucket.Contains('\\') || bucket.Contains("..") || bucket.Contains('%') || bucket.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException($"Invalid storage bucket '{bucket}': ambiguous, whitespace, or traversal characters are prohibited.");
        }

        var effectivePrefix = recoveryPrefix;
        if (string.IsNullOrWhiteSpace(effectivePrefix))
        {
            effectivePrefix = configuration["ObjectStorage:S3:RecoveryPrefix"]?.Trim();
        }
        if (string.IsNullOrWhiteSpace(effectivePrefix))
        {
            effectivePrefix = configuration["ObjectStorage:S3:Prefix"]?.Trim();
        }

        if (string.IsNullOrWhiteSpace(effectivePrefix))
        {
            throw new InvalidOperationException("Hosted recovery requires a dedicated recovery destination prefix.");
        }

        if (effectivePrefix.Contains('\\') || effectivePrefix.Contains("..") || effectivePrefix.Contains('%'))
        {
            throw new InvalidOperationException($"Invalid recovery prefix '{effectivePrefix}': traversal or ambiguous characters are prohibited.");
        }

        var normalizedPrefix = SupabaseS3ObjectStorageOptions.NormalizePrefix(effectivePrefix);
        foreach (var segment in normalizedPrefix.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "." || segment == "..")
            {
                throw new InvalidOperationException("Recovery prefix cannot contain dot or dot-dot path segments.");
            }
        }

        Destination = new SupabaseRecoveryDestination(projectRef, bucket, normalizedPrefix);

        // 4. Validate Recovery API Key
        var rawKey = recoveryApiKey ?? Environment.GetEnvironmentVariable("ObjectStorage__Recovery__SupabaseApiKey") ?? "";
        _apiKey = rawKey.Trim();

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("ObjectStorage__Recovery__SupabaseApiKey environment variable is required for hosted private object recovery.");
        }

        if (_apiKey.Contains('\r') || _apiKey.Contains('\n'))
        {
            throw new InvalidOperationException("API key contains invalid newline characters.");
        }

        if (_apiKey.StartsWith("sb_pub_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Publishable API keys cannot be used for recovery.");
        }

        if (_apiKey.StartsWith("sb_secret_", StringComparison.Ordinal))
        {
            _isLegacyJwt = false;
        }
        else if (_apiKey.StartsWith("ey", StringComparison.Ordinal))
        {
            var parts = _apiKey.Split('.');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException("Invalid JWT structure: must contain 3 parts.");
            }

            string payloadJson;
            try
            {
                payloadJson = DecodeBase64Url(parts[1]);
            }
            catch
            {
                throw new InvalidOperationException("Invalid JWT payload encoding.");
            }

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("role", out var roleProp) ||
                    roleProp.ValueKind != JsonValueKind.String ||
                    !string.Equals(roleProp.GetString(), "service_role", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("JWT role must be 'service_role'; 'anon' or user tokens are not permitted.");
                }
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("Invalid JWT payload JSON.");
            }

            _isLegacyJwt = true;
        }
        else
        {
            throw new InvalidOperationException("Invalid recovery API key format.");
        }

        // Build base endpoint URL: https://<project-ref>.supabase.co/storage/v1/object/
        var projectBaseStr = $"https://{projectRef}.supabase.co/storage/v1/object/";
        _baseEndpoint = new Uri(projectBaseStr, UriKind.Absolute);

        // Per-request timeout
        if (perRequestTimeout.HasValue)
        {
            _perRequestTimeout = perRequestTimeout.Value;
        }
        else
        {
            var timeoutSeconds = configuration.GetValue("ObjectStorage:Recovery:TimeoutSeconds", 30);
            _perRequestTimeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 300));
        }

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false, // Strictly reject redirects
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            };
            _httpClient = new HttpClient(handler, disposeHandler: true);
            _disposeClient = true;
        }
    }

    public async Task<bool> TryCreateAsync(string key, Stream body, string contentType, long length, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(body);

        var normalizedKey = LocalObjectStorage.NormalizeKey(key);

        var encodedBucket = Uri.EscapeDataString(Destination.Bucket);
        var prefixSegments = Destination.RecoveryPrefix.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var encodedPrefix = string.Join("/", prefixSegments);
        if (!string.IsNullOrEmpty(encodedPrefix))
        {
            encodedPrefix += "/";
        }

        var keySegments = normalizedKey.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var encodedKey = string.Join("/", keySegments);
        var relativeRequestPath = $"{encodedBucket}/{encodedPrefix}{encodedKey}";
        var targetUri = new Uri(_baseEndpoint, relativeRequestPath);

        using var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
        request.Headers.Add("x-upsert", "false");
        request.Headers.Add("apikey", _apiKey);

        if (_isLegacyJwt)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var content = new StreamContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Headers.ContentLength = length;
        request.Content = content;

        using var perRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        perRequestCts.CancelAfter(_perRequestTimeout);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, perRequestCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (perRequestCts.IsCancellationRequested)
        {
            throw new TimeoutException("Supabase storage request timed out.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Supabase storage create request failed ({ex.GetType().Name}).");
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            if (statusCode >= 300 && statusCode <= 399)
            {
                throw new InvalidOperationException($"Supabase storage endpoint returned unexpected redirect status {statusCode}. Redirects are prohibited.");
            }

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.BadRequest)
            {
                byte[] responseBytes;
                try
                {
                    responseBytes = await ReadResponseBodyBoundedAsync(response.Content, MaxErrorResponseBodyBytes, perRequestCts.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (perRequestCts.IsCancellationRequested)
                {
                    throw new TimeoutException("Supabase storage request timed out.");
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"Supabase storage error response for HTTP {statusCode} failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Supabase storage error response read failed ({ex.GetType().Name}).");
                }

                if (IsDocumentedDuplicateResponse(response.StatusCode, responseBytes))
                {
                    return false; // Validated existing object detected
                }
            }

            throw new InvalidOperationException($"Supabase storage create request failed with HTTP {statusCode}.");
        }
    }

    private static async Task<byte[]> ReadResponseBodyBoundedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        int totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
            {
                throw new InvalidOperationException($"Supabase storage error response exceeded maximum allowed size ({maxBytes / 1024} KiB).");
            }
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static bool IsDocumentedDuplicateResponse(HttpStatusCode statusCode, byte[] responseBytes)
    {
        if (responseBytes == null || responseBytes.Length == 0) return false;

        try
        {
            using var doc = JsonDocument.Parse(responseBytes, new JsonDocumentOptions { MaxDepth = 4 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (!ValidateNoDuplicateProperties(root)) return false;

            string? error = null;
            string? code = null;
            string? message = null;

            if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                error = errorProp.GetString()?.Trim();

            if (root.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
                code = codeProp.GetString()?.Trim();

            if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                message = msgProp.GetString()?.Trim();

            bool isDuplicateError = IsDuplicateErrorCode(error);
            bool isDuplicateCode = IsDuplicateErrorCode(code);
            bool isDuplicateMessage = IsDuplicateErrorMessage(message);

            if (error != null && !isDuplicateError) return false;
            if (code != null && !isDuplicateCode) return false;

            if (statusCode == HttpStatusCode.Conflict)
            {
                if (isDuplicateError || isDuplicateCode) return true;
                if (isDuplicateMessage && error == null && code == null) return true;
                return false;
            }

            if (statusCode == HttpStatusCode.BadRequest)
            {
                if (isDuplicateError || isDuplicateCode) return true;
                if (isDuplicateMessage && error == null && code == null) return true;
                return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDuplicateErrorCode(string? code) =>
        string.Equals(code, "Duplicate", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "already_exists", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "ResourceAlreadyExists", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "KeyAlreadyExists", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "Asset already exists", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "EntityAlreadyExists", StringComparison.OrdinalIgnoreCase);

    private static bool IsDuplicateErrorMessage(string? message) =>
        string.Equals(message, "The resource already exists", StringComparison.OrdinalIgnoreCase)
        || string.Equals(message, "already exists", StringComparison.OrdinalIgnoreCase)
        || string.Equals(message, "Asset already exists", StringComparison.OrdinalIgnoreCase);

    private static bool ValidateNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in element.EnumerateObject())
            {
                if (!names.Add(prop.Name)) return false;
                if (!ValidateNoDuplicateProperties(prop.Value)) return false;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (!ValidateNoDuplicateProperties(item)) return false;
            }
        }
        return true;
    }

    private static string DecodeBase64Url(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        var bytes = Convert.FromBase64String(padded);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
