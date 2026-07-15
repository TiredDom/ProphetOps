using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class UsersApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> LoginAs(string email, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Owner_lists_seeded_users()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(3, body.GetArrayLength());
    }

    [Fact]
    public async Task Admin_is_forbidden_from_users()
    {
        var client = await LoginAs("admin@prophetops.local", "admin123");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Staff_is_forbidden_from_users()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }
}
