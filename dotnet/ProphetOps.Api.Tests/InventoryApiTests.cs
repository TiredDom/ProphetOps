using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class InventoryApiTests : IDisposable
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

    private static IEnumerable<string> PackageCodes(JsonElement body) =>
        body.EnumerateArray().Select(p => p.GetProperty("id").GetString()!);

    [Fact]
    public async Task Lists_seeded_packages()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var body = await Body(await client.GetAsync("/api/inventory"));

        Assert.Equal(6, body.GetArrayLength());
    }

    [Fact]
    public async Task Staff_may_read_packages()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        var response = await client.GetAsync("/api/inventory");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_package_persists_and_appears_in_the_list()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var create = await client.PostAsJsonAsync("/api/inventory", new
        {
            id = "PKG-NEW1",
            packageName = "Palawan Explorer",
            destination = "Palawan",
            duration = "4D3N",
            basePrice = 32000,
            inclusions = "Flights, hotel, tours",
            availableSlots = 20,
            soldCount = 0,
            reservedCount = 0,
            status = "Normal",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var body = await Body(await client.GetAsync("/api/inventory"));
        Assert.Contains("PKG-NEW1", PackageCodes(body));
        Assert.Equal(7, body.GetArrayLength());
    }

    [Fact]
    public async Task Rejects_an_invalid_package_with_field_errors()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var response = await client.PostAsJsonAsync("/api/inventory", new
        {
            id = "PKG-BAD1",
            packageName = "",
            destination = "",
            basePrice = 1000,
            availableSlots = 5,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await Body(response);
        Assert.True(errors.TryGetProperty("packageName", out _));
    }
}
