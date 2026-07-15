using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class ForecastApiTests : IDisposable
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
    public async Task Admin_reads_a_six_step_forecast()
    {
        var client = await LoginAs("admin@prophetops.local", "admin123");
        var response = await client.GetAsync("/api/forecast");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await Body(response);
        Assert.Equal(6, body.GetProperty("steps").GetArrayLength());

        var mape = body.GetProperty("metrics").GetProperty("mape").GetDouble();
        Assert.True(mape > 0);
        Assert.True(mape < 100);
    }

    [Fact]
    public async Task Staff_may_not_read_the_forecast()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        var response = await client.GetAsync("/api/forecast");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Forecast_includes_a_trajectory_insight()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var body = await Body(await client.GetAsync("/api/forecast"));

        var insight = body.GetProperty("insight");
        var direction = insight.GetProperty("direction").GetString();
        Assert.Contains(direction, new[] { "up", "down", "flat" });
        Assert.False(string.IsNullOrWhiteSpace(insight.GetProperty("peakMonth").GetString()));
        Assert.True(insight.GetProperty("peakValue").GetDouble() > 0);
    }
}
