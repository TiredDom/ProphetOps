using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class ReportsApiTests : IDisposable
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

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Admin_sees_consolidated_totals()
    {
        var client = await LoginAs("admin@prophetops.local", "admin123");
        var response = await client.GetAsync("/api/reports");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Body(response);
        Assert.True(body.GetProperty("revenue").GetInt32() > 0);
        Assert.Equal(9, body.GetProperty("counts").GetProperty("bookings").GetInt32());
    }

    [Fact]
    public async Task Staff_is_forbidden_from_reports()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        var response = await client.GetAsync("/api/reports");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
