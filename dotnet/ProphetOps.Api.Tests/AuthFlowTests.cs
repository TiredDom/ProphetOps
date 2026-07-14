using System.Net;
using System.Net.Http.Json;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class AuthFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthFlowTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string email, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Login_with_valid_owner_credentials_succeeds()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<AuthUser>();
        Assert.Equal(Roles.OwnerManagement, user!.Role);
        Assert.Equal("/dashboard", user.DefaultPath);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_returns_real_data_when_authenticated()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var response = await client.GetAsync("/api/dashboard");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"revenue\"", json);
        Assert.Contains("Holt-Winters", json);
    }

    [Fact]
    public async Task Users_endpoint_gates_by_role()
    {
        var staff = await LoginAs("staff@prophetops.local", "staff123");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/users")).StatusCode);

        var owner = await LoginAs("owner@prophetops.local", "owner123");
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/dashboard");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Contains("default-src 'self'",
            string.Join(" ", response.Headers.GetValues("Content-Security-Policy")));
    }
}
