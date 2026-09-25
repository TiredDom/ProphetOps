using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class TransportSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-transport-" + Guid.NewGuid().ToString("N"));
    private readonly AccessTokenFactory _access = new();

    [Fact]
    public async Task Hosted_mode_marks_antiforgery_cookie_secure_even_when_backend_request_is_http()
    {
        using var factory = Factory(("Hosted:Enabled", "true"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Add(CloudflareAccessOptions.AssertionHeader, _access.Token());
        var response = await client.SendAsync(request);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var xsrf = cookies.Single(header => header.StartsWith("XSRF-TOKEN="));
        Assert.Contains("secure", xsrf, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", xsrf, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", xsrf, StringComparison.OrdinalIgnoreCase);
        Assert.All(cookies.Where(IsAntiforgeryCookie), cookie => Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_public_https_marks_cookies_secure_without_enabling_access_gate()
    {
        using var factory = Factory(
            ("Hosted:Enabled", "false"),
            ("CloudflareAccess:Enabled", "false"),
            ("Transport:PublicHttps", "true"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await client.GetAsync("/login");

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var xsrf = cookies.Single(header => header.StartsWith("XSRF-TOKEN="));
        Assert.Contains("secure", xsrf, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", xsrf, StringComparison.OrdinalIgnoreCase);
        Assert.All(cookies.Where(IsAntiforgeryCookie), cookie => Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Liveness_endpoint_is_mapped_before_spa_fallback_and_access_gate(bool hosted)
    {
        using var factory = hosted
            ? Factory(("Hosted:Enabled", "true"), ("Backup:Scheduled:Enabled", "false"))
            : Factory(("Hosted:Enabled", "false"), ("CloudflareAccess:Enabled", "false"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Trusted_forwarded_proto_controls_scheme_dependent_cookie_flags()
    {
        using var factory = Factory(
            ("ForwardedHeaders:KnownProxies:0", "127.0.0.1"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);

        var xsrf = response.Headers.GetValues("Set-Cookie").Single(header => header.StartsWith("XSRF-TOKEN="));
        Assert.Contains("secure", xsrf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Spoofed_forwarded_proto_is_ignored_without_a_configured_trusted_boundary()
    {
        using var factory = Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var xsrf = cookies.Single(header => header.StartsWith("XSRF-TOKEN="));
        Assert.DoesNotContain("secure", xsrf, StringComparison.OrdinalIgnoreCase);
        Assert.All(cookies.Where(IsAntiforgeryCookie), cookie => Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAntiforgeryCookie(string cookie) =>
        cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal)
        || cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal);

    private WebApplicationFactory<Program> Factory(params (string Key, string Value)[] extra)
    {
        var root = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        return new TransportFactory(root, extra);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        _access.Dispose();
    }

    private sealed class TransportFactory(string root, (string Key, string Value)[] extra) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "false",
                    ["Storage:Root"] = root,
                    ["Business:TimeZone"] = "Asia/Manila",
                    ["Backup:Scheduled:Enabled"] = "false",
                    ["CloudflareAccess:Enabled"] = "true",
                    ["CloudflareAccess:Issuer"] = AccessTokenFactory.Issuer,
                    ["CloudflareAccess:Audience"] = AccessTokenFactory.Audience,
                    ["CloudflareAccess:JwksUrl"] = "https://access.example.test/certs",
                };
                foreach (var (key, value) in extra) values[key] = value;
                config.AddInMemoryCollection(values);
            });
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(ICloudflareAccessJwksTransport)).ToList())
                    services.Remove(descriptor);
                services.AddSingleton<ICloudflareAccessJwksTransport>(new FakeJwksTransport(AccessTokenFactory.Jwks()));
            });
        }
    }

    private sealed class FakeJwksTransport(string jwks) : ICloudflareAccessJwksTransport
    {
        public Task<string> GetJwksAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(jwks);
    }

    private sealed class AccessTokenFactory : IDisposable
    {
        public const string Issuer = "https://agency.cloudflareaccess.com";
        public const string Audience = "prophetops-access-audience";
        private static readonly JsonWebTokenHandler Handler = new();
        private static readonly RSA Rsa = RSA.Create(2048);
        private static readonly RsaSecurityKey Key = new(Rsa) { KeyId = "transport" };

        public string Token() => Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = new Dictionary<string, object> { ["email"] = "owner@prophetops.local" },
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256),
        });

        public static string Jwks()
        {
            var parameters = Rsa.ExportParameters(false);
            return JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["kty"] = "RSA",
                        ["use"] = "sig",
                        ["kid"] = Key.KeyId,
                        ["alg"] = SecurityAlgorithms.RsaSha256,
                        ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
                        ["e"] = Base64UrlEncoder.Encode(parameters.Exponent),
                    },
                },
            });
        }

        public void Dispose()
        {
        }
    }
}
