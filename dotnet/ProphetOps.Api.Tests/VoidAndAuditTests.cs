using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class VoidAndAuditTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> SignedIn()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "owner@prophetops.local", password = "owner123" });
        login.EnsureSuccessStatusCode();

        var probe = await client.GetAsync("/api/auth/me");
        var cookie = probe.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(cookie.Split(';')[0]["XSRF-TOKEN=".Length..]);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        return client;
    }

    private static object NewBooking(string id, int revenue = 90_000, int pax = 2, string? packageId = null) => new
    {
        id,
        ds = "2026-07-12",
        y = pax,
        client = "Void Test",
        packageId,
        package = packageId is null ? "Ad hoc" : "Boracay Group Package",
        destination = "Cebu",
        grossRevenue = revenue,
        paymentStatus = "Pending",
        bookingStatus = "Pending",
        entryType = packageId is null ? "Custom quotation" : "Package preset",
        source = "Manual quotation",
    };

    private static async Task<int> Revenue(HttpClient client)
    {
        var dash = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        return dash.GetProperty("revenue").GetInt32();
    }

    [Fact]
    public async Task Voiding_a_booking_takes_its_revenue_out_of_the_totals()
    {
        var client = await SignedIn();

        var before = await Revenue(client);
        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-VOID-1", 90_000))).EnsureSuccessStatusCode();
        Assert.Equal(before + 90_000, await Revenue(client));

        var voided = await client.PostAsJsonAsync("/api/bookings/BKG-VOID-1/void", new { reason = "Duplicate entry" });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);

        Assert.Equal(before, await Revenue(client));
    }

    [Fact]
    public async Task A_voided_booking_gives_its_seats_back_and_takes_them_again_when_restored()
    {
        var client = await SignedIn();

        var packages = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        var target = packages.EnumerateArray().First(p => p.GetProperty("availableSlots").GetInt32() >= 4);
        var code = target.GetProperty("id").GetString()!;
        var free = target.GetProperty("availableSlots").GetInt32();

        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-VOID-2", 90_000, 3, code))).EnsureSuccessStatusCode();

        var afterBooking = await Slots(client, code);
        Assert.Equal(free - 3, afterBooking);

        (await client.PostAsJsonAsync("/api/bookings/BKG-VOID-2/void", new { reason = "Client cancelled" })).EnsureSuccessStatusCode();
        Assert.Equal(free, await Slots(client, code));

        (await client.PostAsJsonAsync("/api/bookings/BKG-VOID-2/restore", new { })).EnsureSuccessStatusCode();
        Assert.Equal(free - 3, await Slots(client, code));
    }

    private static async Task<int> Slots(HttpClient client, string code)
    {
        var packages = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        return packages.EnumerateArray()
            .First(p => p.GetProperty("id").GetString() == code)
            .GetProperty("availableSlots").GetInt32();
    }

    [Fact]
    public async Task Voiding_requires_a_reason()
    {
        var client = await SignedIn();
        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-VOID-3"))).EnsureSuccessStatusCode();

        var blank = await client.PostAsJsonAsync("/api/bookings/BKG-VOID-3/void", new { reason = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var missing = await client.PostAsJsonAsync("/api/bookings/BKG-VOID-3/void", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task A_booking_cannot_be_voided_twice()
    {
        var client = await SignedIn();
        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-VOID-4"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/bookings/BKG-VOID-4/void", new { reason = "Wrong client" })).EnsureSuccessStatusCode();

        var again = await client.PostAsJsonAsync("/api/bookings/BKG-VOID-4/void", new { reason = "Again" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task A_voided_booking_still_appears_in_the_list_marked_as_voided()
    {
        var client = await SignedIn();
        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-VOID-5"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/bookings/BKG-VOID-5/void", new { reason = "Recorded in error" })).EnsureSuccessStatusCode();

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        var row = listing.GetProperty("bookings").EnumerateArray()
            .First(b => b.GetProperty("id").GetString() == "BKG-VOID-5");

        Assert.True(row.GetProperty("voided").GetBoolean());
        Assert.Equal("Recorded in error", row.GetProperty("voidReason").GetString());
    }

    [Fact]
    public async Task Voiding_an_expense_takes_it_out_of_costs()
    {
        var client = await SignedIn();

        var before = (await client.GetFromJsonAsync<JsonElement>("/api/dashboard")).GetProperty("costs").GetInt32();

        (await client.PostAsJsonAsync("/api/expenses", new
        {
            id = "EXP-VOID-1",
            date = "2026-07-12",
            category = "Marketing",
            amount = 25_000,
            relatedPackage = "General",
            paymentStatus = "Paid",
        })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/expenses/EXP-VOID-1/void", new { reason = "Entered twice" })).EnsureSuccessStatusCode();

        var after = (await client.GetFromJsonAsync<JsonElement>("/api/dashboard")).GetProperty("costs").GetInt32();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Every_change_leaves_a_trail_naming_who_made_it()
    {
        var client = await SignedIn();

        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-TRAIL-1", 90_000))).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/bookings/BKG-TRAIL-1", NewBooking("BKG-TRAIL-1", 110_000))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/bookings/BKG-TRAIL-1/void", new { reason = "Superseded" })).EnsureSuccessStatusCode();

        var trail = await client.GetFromJsonAsync<JsonElement>("/api/activity?entityType=Booking&entityCode=BKG-TRAIL-1");
        var actions = trail.EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();

        Assert.Contains("Created", actions);
        Assert.Contains("Updated", actions);
        Assert.Contains("Voided", actions);

        var edit = trail.EnumerateArray().First(e => e.GetProperty("action").GetString() == "Updated");
        Assert.Contains("Revenue", edit.GetProperty("summary").GetString());
        Assert.Equal("owner@prophetops.local", edit.GetProperty("actor").GetString());
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_leaves_no_trail_entry()
    {
        var client = await SignedIn();

        (await client.PostAsJsonAsync("/api/bookings", NewBooking("BKG-TRAIL-2"))).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/bookings/BKG-TRAIL-2", NewBooking("BKG-TRAIL-2"))).EnsureSuccessStatusCode();

        var trail = await client.GetFromJsonAsync<JsonElement>("/api/activity?entityType=Booking&entityCode=BKG-TRAIL-2");
        var actions = trail.EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();

        Assert.Equal(["Created"], actions);
    }
}
