using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class ExpensesApiTests : IDisposable
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

    private static IEnumerable<string> ExpenseCodes(JsonElement body) =>
        body.EnumerateArray().Select(e => e.GetProperty("id").GetString()!);

    [Fact]
    public async Task Lists_seeded_expenses()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var body = await Body(await client.GetAsync("/api/expenses"));

        Assert.Equal(5, body.GetArrayLength());
    }

    [Fact]
    public async Task Staff_is_forbidden_from_expenses()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        var response = await client.GetAsync("/api/expenses");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_may_read_expenses()
    {
        var client = await LoginAs("admin@prophetops.local", "admin123");
        var response = await client.GetAsync("/api/expenses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Creating_an_expense_persists_and_appears_in_the_list()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var create = await client.PostAsJsonAsync("/api/expenses", new
        {
            id = "EXP-NEW1",
            date = "2026-07-10",
            category = "Marketing",
            amount = 12000,
            relatedPackage = "Boracay Group Package",
            paymentStatus = "Pending",
            notes = "",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var body = await Body(await client.GetAsync("/api/expenses"));
        Assert.Contains("EXP-NEW1", ExpenseCodes(body));
        Assert.Equal(6, body.GetArrayLength());
    }

    [Fact]
    public async Task Rejects_an_invalid_expense_with_field_errors()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var response = await client.PostAsJsonAsync("/api/expenses", new
        {
            id = "EXP-BAD1",
            date = "2026-07-10",
            category = "",
            amount = 5000,
            relatedPackage = "Ops",
            paymentStatus = "Pending",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await Body(response);
        Assert.True(errors.TryGetProperty("category", out _));
    }
}
