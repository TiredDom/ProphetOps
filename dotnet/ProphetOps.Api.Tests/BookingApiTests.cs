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
    public async Task Creating_a_booking_decrements_the_linked_package_slots()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");

        var before = await Body(await client.GetAsync("/api/bookings"));
        var pkgBefore = before.GetProperty("packages").EnumerateArray()
            .First(p => p.GetProperty("id").GetString() == "PKG-101");
        var slotsBefore = pkgBefore.GetProperty("availableSlots").GetInt32();
        var soldBefore = pkgBefore.GetProperty("soldCount").GetInt32();

        var create = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-SLOT1",
            ds = "2026-07-12",
            y = 4,
            client = "Slot Test Co.",
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

        var after = await Body(await client.GetAsync("/api/bookings"));
        var pkgAfter = after.GetProperty("packages").EnumerateArray()
            .First(p => p.GetProperty("id").GetString() == "PKG-101");
        Assert.Equal(Math.Max(0, slotsBefore - 4), pkgAfter.GetProperty("availableSlots").GetInt32());
        Assert.Equal(soldBefore + 4, pkgAfter.GetProperty("soldCount").GetInt32());
    }

    private static async Task<JsonElement> Package(HttpClient client, string code)
    {
        var body = await Body(await client.GetAsync("/api/bookings"));
        return body.GetProperty("packages").EnumerateArray()
            .First(p => p.GetProperty("id").GetString() == code);
    }

    private static object BookingPayload(string id, string packageId, int passengers) => new
    {
        id,
        ds = "2026-07-14",
        y = passengers,
        client = "Edit Test Co.",
        packageId,
        entryType = packageId is null ? "Custom quotation" : "Package preset",
        package = packageId is null ? "Custom island hop" : "Boracay Group Package",
        destination = "Boracay",
        grossRevenue = 12000 * passengers,
        paymentStatus = "Pending",
        bookingStatus = "Pending",
        staffAssigned = "Staff User",
        source = packageId is null ? "Manual quotation" : "Package preset",
        notes = "",
    };

    [Fact]
    public async Task Editing_passenger_count_reconciles_the_package_slots()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var before = await Package(client, "PKG-102");
        var slots = before.GetProperty("availableSlots").GetInt32();
        var sold = before.GetProperty("soldCount").GetInt32();

        await client.PostAsJsonAsync("/api/bookings", BookingPayload("BKG-EDIT1", "PKG-102", 4));
        var edit = await client.PutAsJsonAsync("/api/bookings/BKG-EDIT1", BookingPayload("BKG-EDIT1", "PKG-102", 6));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var after = await Package(client, "PKG-102");
        Assert.Equal(slots - 6, after.GetProperty("availableSlots").GetInt32());
        Assert.Equal(sold + 6, after.GetProperty("soldCount").GetInt32());
    }

    [Fact]
    public async Task Moving_a_booking_to_another_package_moves_the_slots()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var a0 = await Package(client, "PKG-101");
        var b0 = await Package(client, "PKG-102");
        var aSlots = a0.GetProperty("availableSlots").GetInt32();
        var bSlots = b0.GetProperty("availableSlots").GetInt32();

        await client.PostAsJsonAsync("/api/bookings", BookingPayload("BKG-EDIT2", "PKG-101", 3));
        await client.PutAsJsonAsync("/api/bookings/BKG-EDIT2", BookingPayload("BKG-EDIT2", "PKG-102", 3));

        var a1 = await Package(client, "PKG-101");
        var b1 = await Package(client, "PKG-102");
        Assert.Equal(aSlots, a1.GetProperty("availableSlots").GetInt32());
        Assert.Equal(bSlots - 3, b1.GetProperty("availableSlots").GetInt32());
    }

    [Fact]
    public async Task Converting_a_booking_to_a_tailored_quotation_releases_the_slots()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var before = await Package(client, "PKG-102");
        var slots = before.GetProperty("availableSlots").GetInt32();

        await client.PostAsJsonAsync("/api/bookings", BookingPayload("BKG-EDIT3", "PKG-102", 5));
        await client.PutAsJsonAsync("/api/bookings/BKG-EDIT3", BookingPayload("BKG-EDIT3", null, 5));

        var after = await Package(client, "PKG-102");
        Assert.Equal(slots, after.GetProperty("availableSlots").GetInt32());
    }

    [Fact]
    public async Task Overbooking_a_package_is_rejected()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var pkg = await Package(client, "PKG-101");
        var slots = pkg.GetProperty("availableSlots").GetInt32();

        var response = await client.PostAsJsonAsync("/api/bookings",
            BookingPayload("BKG-OVER1", "PKG-101", slots + 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Body(response)).TryGetProperty("y", out _));

        var after = await Package(client, "PKG-101");
        Assert.Equal(slots, after.GetProperty("availableSlots").GetInt32());
    }

    [Fact]
    public async Task Editing_a_booking_beyond_remaining_slots_is_rejected()
    {
        var client = await LoginAs("owner@prophetops.local", "owner123");
        var slots = (await Package(client, "PKG-105")).GetProperty("availableSlots").GetInt32();

        await client.PostAsJsonAsync("/api/bookings", BookingPayload("BKG-OVER2", "PKG-105", 2));
        var edit = await client.PutAsJsonAsync("/api/bookings/BKG-OVER2",
            BookingPayload("BKG-OVER2", "PKG-105", slots + 1));

        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        var after = await Package(client, "PKG-105");
        Assert.Equal(slots - 2, after.GetProperty("availableSlots").GetInt32());
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
