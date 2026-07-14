using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class AnalyticsApiTests : IDisposable
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
    public async Task Admin_reads_analytics_with_twelve_months_of_history()
    {
        var client = await LoginAs("admin@prophetops.local", "admin123");
        var response = await client.GetAsync("/api/analytics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Body(response);
        Assert.Equal(12, body.GetProperty("salesHistory").GetArrayLength());
        Assert.True(body.GetProperty("totalRevenue").GetInt32() > 0);
    }

    [Fact]
    public async Task Staff_may_not_read_analytics()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        var response = await client.GetAsync("/api/analytics");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
