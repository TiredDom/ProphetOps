using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class UnusualBookingTests : IDisposable
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

    private static object Booking(string id, int revenue, bool confirm = false) => new
    {
        id,
        ds = "2026-07-10",
        y = 2,
        client = "Typo Check",
        package = "Ad hoc",
        destination = "Cebu",
        grossRevenue = revenue,
        paymentStatus = "Pending",
        bookingStatus = "Pending",
        entryType = "Custom quotation",
        source = "Manual quotation",
        confirmUnusual = confirm,
    };

    [Fact]
    public async Task An_ordinary_amount_saves_without_being_queried()
    {
        var client = await SignedIn();
        var response = await client.PostAsJsonAsync("/api/bookings", Booking("BKG-NORMAL", 95_000));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_amount_far_above_the_usual_is_queried_before_it_is_saved()
    {
        var client = await SignedIn();
        var response = await client.PostAsJsonAsync("/api/bookings", Booking("BKG-TYPO", 12_500_000));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Contains("stray digit", body!["message"]);

        var listing = await client.GetFromJsonAsync<Dictionary<string, object>>("/api/bookings");
        Assert.DoesNotContain("BKG-TYPO", listing!["bookings"].ToString());
    }

    [Fact]
    public async Task The_same_amount_saves_once_the_person_confirms_they_meant_it()
    {
        var client = await SignedIn();

        var queried = await client.PostAsJsonAsync("/api/bookings", Booking("BKG-BIG", 12_500_000));
        Assert.Equal(HttpStatusCode.Conflict, queried.StatusCode);

        var confirmed = await client.PostAsJsonAsync("/api/bookings", Booking("BKG-BIG", 12_500_000, confirm: true));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task Editing_a_booking_without_touching_the_amount_is_not_queried_again()
    {
        var client = await SignedIn();

        var created = await client.PostAsJsonAsync("/api/bookings", Booking("BKG-EDIT", 12_500_000, confirm: true));
        created.EnsureSuccessStatusCode();

        var edited = await client.PutAsJsonAsync("/api/bookings/BKG-EDIT", new
        {
            id = "BKG-EDIT",
            ds = "2026-07-10",
            y = 3,
            client = "Renamed Client",
            package = "Ad hoc",
            destination = "Cebu",
            grossRevenue = 12_500_000,
            paymentStatus = "Paid",
            bookingStatus = "Confirmed",
            entryType = "Custom quotation",
            source = "Manual quotation",
        });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }
}
