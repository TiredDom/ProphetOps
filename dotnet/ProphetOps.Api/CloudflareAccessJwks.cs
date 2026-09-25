using Microsoft.IdentityModel.Tokens;

namespace ProphetOps.Api;

public interface ICloudflareAccessJwksTransport
{
    Task<string> GetJwksAsync(Uri url, CancellationToken cancellationToken);
}

public sealed class HttpCloudflareAccessJwksTransport(HttpClient http) : ICloudflareAccessJwksTransport
{
    public async Task<string> GetJwksAsync(Uri url, CancellationToken cancellationToken)
        => await http.GetStringAsync(url, cancellationToken);
}

public interface ICloudflareAccessKeyStore
{
    Task<SecurityKey?> FindKeyAsync(string kid, CancellationToken cancellationToken);
}

public sealed class CloudflareAccessJwksCache(
    CloudflareAccessOptions options,
    ICloudflareAccessJwksTransport transport,
    TimeProvider clock,
    ILogger<CloudflareAccessJwksCache> logger) : ICloudflareAccessKeyStore
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyDictionary<string, SecurityKey> _keys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal);
    private DateTimeOffset _expiresAt;

    public async Task<SecurityKey?> FindKeyAsync(string kid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(kid)) return null;

        if (UsableCache().TryGetValue(kid, out var cached))
            return cached;

        var refreshed = await RefreshOnceAsync(cancellationToken);
        if (refreshed.TryGetValue(kid, out var key))
            return key;

        return UsableCache().TryGetValue(kid, out cached) ? cached : null;
    }

    private IReadOnlyDictionary<string, SecurityKey> UsableCache()
        => clock.GetUtcNow() < _expiresAt ? _keys : new Dictionary<string, SecurityKey>(StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, SecurityKey>> RefreshOnceAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                var json = await transport.GetJwksAsync(options.JwksUrl, cancellationToken);
                var keySet = new JsonWebKeySet(json);
                var keys = keySet.Keys
                    .Where(key => !string.IsNullOrWhiteSpace(key.Kid))
                    .ToDictionary(key => key.Kid, key => (SecurityKey)key, StringComparer.Ordinal);
                _keys = keys;
                _expiresAt = clock.GetUtcNow().Add(options.KeyCacheLifetime);
                return _keys;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Cloudflare Access signing keys could not be refreshed.");
                return UsableCache();
            }
            catch (Exception ex) when (ex is HttpRequestException or ArgumentException or SecurityTokenException or System.Text.Json.JsonException)
            {
                logger.LogWarning("Cloudflare Access signing keys could not be refreshed.");
                return UsableCache();
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
