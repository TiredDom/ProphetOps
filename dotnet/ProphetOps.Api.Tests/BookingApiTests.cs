using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BookingApiTests : IDisposable
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

    private static IEnumerable<string> BookingCodes(JsonElement body) =>
        body.GetProperty("bookings").EnumerateArray().Select(b => b.GetProperty("id").GetString()!);

    [Fact]
    public async Task Lists_seeded_bookings_and_packages()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var body = await Body(await client.GetAsync("/api/bookings"));

        Assert.Contains("BKG-2401", BookingCodes(body));
        Assert.Equal(9, body.GetProperty("bookings").GetArrayLength());
        Assert.Equal(6, body.GetProperty("packages").GetArrayLength());
    }

    [Fact]
    public async Task Staff_may_read_bookings()
    {
        var client = await LoginAs("staff@prophetops.local", "staff123");
        var response = await client.GetAsync("/api/bookings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_booking_persists_and_appears_in_the_list()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var create = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-NEW1",
            ds = "2026-07-10",
            y = 4,
            client = "Test Partner",
            packageId = "PKG-101",
            entryType = "Package preset",
            package = "Boracay Group Package",
            destination = "Boracay",
            grossRevenue = 48000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            staffAssigned = "Staff User",
            source = "Package preset",
            notes = "",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var body = await Body(await client.GetAsync("/api/bookings"));
        Assert.Contains("BKG-NEW1", BookingCodes(body));
        Assert.Equal(10, body.GetProperty("bookings").GetArrayLength());
    }

    [Fact]
    public async Task Rejects_an_invalid_booking_with_field_errors()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-BAD1",
            ds = "2026-07-10",
            y = 0,
            client = "",
            package = "",
            destination = "",
            grossRevenue = 1000,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await Body(response);
        Assert.True(errors.TryGetProperty("client", out _));
        Assert.True(errors.TryGetProperty("y", out _));
    }

    [Fact]
    public async Task Bulk_confirm_updates_booking_status()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-BULK1",
            ds = "2026-07-10",
            y = 2,
            client = "Bulk Client",
            package = "Ad hoc",
            destination = "Cebu",
            grossRevenue = 20000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            entryType = "Manual quotation",
            source = "Manual quotation",
        });

        var bulk = await client.PostAsJsonAsync("/api/bookings/bulk",
            new { ids = new[] { "BKG-BULK1" }, action = "confirm" });
        Assert.Equal(HttpStatusCode.OK, bulk.StatusCode);

        var body = await Body(await client.GetAsync("/api/bookings"));
        var created = body.GetProperty("bookings").EnumerateArray()
            .First(b => b.GetProperty("id").GetString() == "BKG-BULK1");
        Assert.Equal("Confirmed", created.GetProperty("bookingStatus").GetString());
    }
}
