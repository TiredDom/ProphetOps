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
        await ArmAntiforgery(client);
        return client;
    }

    private static async Task ArmAntiforgery(HttpClient client)
    {
        var probe = await client.GetAsync("/api/auth/me");
        if (!probe.Headers.TryGetValues("Set-Cookie", out var cookies)) return;
        var xsrf = cookies.FirstOrDefault(c => c.StartsWith("XSRF-TOKEN="));
        if (xsrf is null) return;
        var value = xsrf.Split(';')[0]["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(value));
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Owner_lists_seeded_users()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
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

    [Fact]
    public async Task Roles_endpoint_returns_the_three_access_levels()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var body = await Body(await client.GetAsync("/api/users/roles"));

        Assert.Equal(3, body.GetArrayLength());
        var names = body.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToList();
        Assert.Contains("Owner / Management", names);
        Assert.Contains("Staff", names);
    }

    [Fact]
    public async Task Owner_creates_an_account_that_can_sign_in()
    {
        var owner = await LoginAs("owner@prophetops.local", "owner123");

        var create = await owner.PostAsJsonAsync("/api/users", new
        {
            name = "New Staffer",
            email = "new@renantina.ph",
            role = "Staff",
            password = "welcome123",
            status = "Active",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        Assert.Equal(4, (await Body(await owner.GetAsync("/api/users"))).GetArrayLength());

        var fresh = _factory.CreateClient();
        var login = await fresh.PostAsJsonAsync("/api/auth/login",
            new { email = "new@renantina.ph", password = "welcome123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_a_short_password()
    {
        var owner = await LoginAs("owner@prophetops.local", "owner123");

        var response = await owner.PostAsJsonAsync("/api/users", new
        {
            name = "Too Short",
            email = "short@renantina.ph",
            role = "Staff",
            password = "1234567",
            status = "Active",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Body(response)).TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_email()
    {
        var owner = await LoginAs("owner@prophetops.local", "owner123");

        var response = await owner.PostAsJsonAsync("/api/users", new
        {
            name = "Clashing",
            email = "owner@prophetops.local",
            role = "Admin",
            password = "another123",
            status = "Active",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Body(response)).TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Owner_cannot_suspend_their_own_account()
    {
        var owner = await LoginAs("owner@prophetops.local", "owner123");

        var response = await owner.PutAsJsonAsync("/api/users/owner@prophetops.local", new
        {
            name = "Maria Santos",
            email = "owner@prophetops.local",
            role = "Owner / Management",
            password = "",
            status = "Suspended",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Body(response)).TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Suspending_a_user_blocks_their_sign_in()
    {
        var owner = await LoginAs("owner@prophetops.local", "owner123");

        var update = await owner.PutAsJsonAsync("/api/users/staff@prophetops.local", new
        {
            name = "Staff User",
            email = "staff@prophetops.local",
            role = "Staff",
            password = "",
            status = "Suspended",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var fresh = _factory.CreateClient();
        var login = await fresh.PostAsJsonAsync("/api/auth/login",
            new { email = "staff@prophetops.local", password = "staff123" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
