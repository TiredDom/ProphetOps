using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProphetOps.Api;
using ProphetOps.Data;
using Xunit;

namespace ProphetOps.Api.Tests;

public class CloudflareAccessTests : IDisposable
{
    private const string Issuer = "https://agency.cloudflareaccess.com";
    private const string Audience = "prophetops-access-audience";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-access-" + Guid.NewGuid().ToString("N"));
    private readonly AccessTokenFactory _tokens = new();

    [Fact]
    public async Task Health_live_is_the_only_data_free_bypass()
    {
        using var factory = HostedFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/assets/index.js")]
    [InlineData("/dashboard")]
    [InlineData("/api/bookings")]
    [InlineData("/api/inventory/PKG-101/image")]
    [InlineData("/api/export/bookings.csv")]
    [InlineData("/api/auth/login")]
    [InlineData("/not-a-real-route")]
    public async Task Hosted_gate_rejects_everything_else_without_access_assertion(string path)
    {
        using var factory = HostedFactory();
        using var client = factory.CreateClient();

        var response = path == "/api/auth/login"
            ? await client.PostAsJsonAsync(path, new { email = "owner@prophetops.local", password = "owner123" })
            : await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("raw-token", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cf_authorization_cookie_is_ignored()
    {
        using var factory = HostedFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "CF_Authorization=" + _tokens.Valid());

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Valid_access_without_app_cookie_keeps_existing_api_unauthorized_behavior()
    {
        using var factory = HostedFactory();
        using var client = ClientWithAccess(factory);

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_access_matching_login_email_permits_normal_login()
    {
        using var factory = HostedFactory();
        using var client = ClientWithAccess(factory, email: "OWNER@prophetops.local");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Valid_access_mismatched_login_email_rejects_login()
    {
        using var factory = HostedFactory();
        using var client = ClientWithAccess(factory, email: "other@prophetops.local");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "owner@prophetops.local", password = "owner123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Matching_access_and_app_session_permits_authenticated_request()
    {
        using var factory = HostedFactory();
        using var client = await LoginWithAccess(factory);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Mismatched_access_and_app_session_is_rejected_after_cookie_authentication()
    {
        using var factory = HostedFactory();
        using var client = await LoginWithAccess(factory);
        client.DefaultRequestHeaders.Remove(CloudflareAccessOptions.AssertionHeader);
        client.DefaultRequestHeaders.Add(CloudflareAccessOptions.AssertionHeader, _tokens.Valid(email: "other@prophetops.local"));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Existing_app_roles_still_apply_after_access_validation()
    {
        using var factory = HostedFactory();
        using var client = await LoginWithAccess(factory, email: "staff@prophetops.local", password: "staff123");

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TokenCase.Forged)]
    [InlineData(TokenCase.Expired)]
    [InlineData(TokenCase.WrongAudience)]
    [InlineData(TokenCase.WrongIssuer)]
    [InlineData(TokenCase.WrongAlgorithm)]
    [InlineData(TokenCase.MissingEmail)]
    public async Task Invalid_tokens_are_rejected(TokenCase tokenCase)
    {
        using var factory = HostedFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CloudflareAccessOptions.AssertionHeader, _tokens.For(tokenCase));

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_or_blank_kid_rejects_without_a_refresh()
    {
        var transport = new FakeJwksTransport(_tokens.Jwks());
        using var factory = HostedFactory(transport);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CloudflareAccessOptions.AssertionHeader, _tokens.Valid(kid: null));

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, transport.Requests);
    }

    [Fact]
    public async Task Unknown_kid_triggers_exactly_one_refresh_for_that_request()
    {
        var transport = new FakeJwksTransport(_tokens.Jwks());
        using var factory = HostedFactory(transport);
        using var client = ClientWithAccess(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookings")).StatusCode);
        var before = transport.Requests;
        client.DefaultRequestHeaders.Remove(CloudflareAccessOptions.AssertionHeader);
        client.DefaultRequestHeaders.Add(CloudflareAccessOptions.AssertionHeader, _tokens.Valid(key: _tokens.RotatedKey, kid: "rotated"));

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before + 1, transport.Requests);
    }

    [Fact]
    public async Task Rotated_key_is_accepted_after_one_bounded_refresh()
    {
        var transport = new FakeJwksTransport(_tokens.Jwks());
        using var factory = HostedFactory(transport);
        using var client = ClientWithAccess(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookings")).StatusCode);
        var before = transport.Requests;
        transport.Jwks = _tokens.Jwks(_tokens.PrimaryKey, _tokens.RotatedKey);
        client.DefaultRequestHeaders.Remove(CloudflareAccessOptions.AssertionHeader);
        client.DefaultRequestHeaders.Add(CloudflareAccessOptions.AssertionHeader, _tokens.Valid(key: _tokens.RotatedKey, kid: "rotated"));

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before + 1, transport.Requests);
    }

    [Fact]
    public async Task Jwks_outage_uses_still_valid_cached_key()
    {
        var transport = new FakeJwksTransport(_tokens.Jwks());
        using var factory = HostedFactory(transport);
        using var client = ClientWithAccess(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookings")).StatusCode);
        transport.Throw = true;

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Jwks_outage_without_cache_fails_closed()
    {
        var transport = new FakeJwksTransport(_tokens.Jwks()) { Throw = true };
        using var factory = HostedFactory(transport);
        using var client = ClientWithAccess(factory);

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, transport.Requests);
    }

    [Fact]
    public async Task Malformed_jwks_fails_closed_without_exposing_key_material()
    {
        var transport = new FakeJwksTransport("not-json");
        using var factory = HostedFactory(transport);
        using var client = ClientWithAccess(factory);

        var response = await client.GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, transport.Requests);
        Assert.DoesNotContain("not-json", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CloudflareAccess:Enabled", null, "CloudflareAccess:Enabled")]
    [InlineData("CloudflareAccess:Issuer", null, "CloudflareAccess:Issuer")]
    [InlineData("CloudflareAccess:Audience", null, "CloudflareAccess:Audience")]
    [InlineData("CloudflareAccess:JwksUrl", "http://example.test/certs", "CloudflareAccess:JwksUrl")]
    public void Hosted_startup_fails_closed_when_access_configuration_is_incomplete(string key, string? value, string expected)
    {
        var overrides = new Dictionary<string, string?> { [key] = value };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(AccessConfigurationValues(overrides))
            .Build();

        var validation = Assert.Throws<InvalidOperationException>(() => CloudflareAccessOptions.FromConfiguration(config));
        Assert.Contains(expected, validation.Message);

        using var factory = HostedFactory(overrides: overrides);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    private static Dictionary<string, string?> AccessConfigurationValues(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Demo:Enabled"] = "true",
            ["Hosted:Enabled"] = "true",
            ["Business:TimeZone"] = "Asia/Manila",
            ["Backup:Scheduled:Enabled"] = "false",
            ["CloudflareAccess:Enabled"] = "true",
            ["CloudflareAccess:Issuer"] = Issuer,
            ["CloudflareAccess:Audience"] = Audience,
            ["CloudflareAccess:JwksUrl"] = "https://access.example.test/certs",
        };
        if (overrides is not null)
        {
            foreach (var (overrideKey, overrideValue) in overrides) values[overrideKey] = overrideValue;
        }

        return values;
    }

    private AccessFactory HostedFactory(FakeJwksTransport? transport = null, Dictionary<string, string?>? overrides = null)
        => new(_directory, transport ?? new FakeJwksTransport(_tokens.Jwks()), overrides);

    private HttpClient ClientWithAccess(AccessFactory factory, string email = "owner@prophetops.local")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(CloudflareAccessOptions.AssertionHeader, _tokens.Valid(email: email));
        return client;
    }

    private async Task<HttpClient> LoginWithAccess(AccessFactory factory,
        string email = "owner@prophetops.local", string password = "owner123")
    {
        var client = ClientWithAccess(factory, email);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    public void Dispose()
    {
        _tokens.Dispose();
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public enum TokenCase
    {
        Forged,
        Expired,
        WrongAudience,
        WrongIssuer,
        WrongAlgorithm,
        MissingEmail,
    }

    private sealed class AccessFactory(
        string directory,
        FakeJwksTransport transport,
        Dictionary<string, string?>? overrides) : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(directory, Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = AccessConfigurationValues(overrides);
                values["Storage:Root"] = _storageRoot;
                config.AddInMemoryCollection(values);
            });
            builder.ConfigureServices(services =>
            {
                _connection.Open();
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType == typeof(ICloudflareAccessJwksTransport)).ToList()) services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<ICloudflareAccessJwksTransport>(transport);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }
    }

    private sealed class FakeJwksTransport(string jwks) : ICloudflareAccessJwksTransport
    {
        public string Jwks { get; set; } = jwks;
        public bool Throw { get; set; }
        public int Requests { get; private set; }

        public Task<string> GetJwksAsync(Uri url, CancellationToken cancellationToken)
        {
            Assert.Equal("https", url.Scheme);
            Requests++;
            if (Throw) throw new HttpRequestException("JWKS unavailable.");
            return Task.FromResult(Jwks);
        }
    }

    private sealed class AccessTokenFactory : IDisposable
    {
        private readonly JsonWebTokenHandler _handler = new();
        private readonly RSA _primary = RSA.Create(2048);
        private readonly RSA _rotated = RSA.Create(2048);
        private readonly RSA _forged = RSA.Create(2048);

        public RsaSecurityKey PrimaryKey { get; }
        public RsaSecurityKey RotatedKey { get; }

        public AccessTokenFactory()
        {
            PrimaryKey = new RsaSecurityKey(_primary) { KeyId = "primary" };
            RotatedKey = new RsaSecurityKey(_rotated) { KeyId = "rotated" };
        }

        public string Valid(string email = "owner@prophetops.local", RsaSecurityKey? key = null, string? kid = "primary")
            => Token(email: email, key: key ?? PrimaryKey, kid: kid);

        public string For(TokenCase tokenCase) => tokenCase switch
        {
            TokenCase.Forged => Token(key: new RsaSecurityKey(_forged) { KeyId = PrimaryKey.KeyId }),
            TokenCase.Expired => Token(expires: DateTime.UtcNow.AddMinutes(-10)),
            TokenCase.WrongAudience => Token(audience: "wrong-audience"),
            TokenCase.WrongIssuer => Token(issuer: "https://wrong.cloudflareaccess.com"),
            TokenCase.WrongAlgorithm => Token(algorithm: SecurityAlgorithms.RsaSha512),
            TokenCase.MissingEmail => Token(email: null),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenCase)),
        };

        public string Token(
            string? email = "owner@prophetops.local",
            RsaSecurityKey? key = null,
            string? kid = "primary",
            string issuer = Issuer,
            string audience = Audience,
            string algorithm = SecurityAlgorithms.RsaSha256,
            DateTime? expires = null)
        {
            key ??= PrimaryKey;
            key.KeyId = kid;
            var claims = new Dictionary<string, object>();
            if (email is not null) claims["email"] = email;
            return _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                Claims = claims,
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                Expires = expires ?? DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(key, algorithm),
            });
        }

        public string Jwks(params RsaSecurityKey[] keys)
        {
            if (keys.Length == 0) keys = [PrimaryKey];
            var jwks = keys.Select(key =>
            {
                var parameters = key.Rsa!.ExportParameters(false);
                return new Dictionary<string, string>
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["kid"] = key.KeyId,
                    ["alg"] = SecurityAlgorithms.RsaSha256,
                    ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
                    ["e"] = Base64UrlEncoder.Encode(parameters.Exponent),
                };
            }).ToArray();
            return JsonSerializer.Serialize(new { keys = jwks });
        }

        public void Dispose()
        {
            _primary.Dispose();
            _rotated.Dispose();
            _forged.Dispose();
        }
    }
}
