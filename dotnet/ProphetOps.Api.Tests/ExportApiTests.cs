using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class ExportApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private const string BookingHeadings =
        "code,date,client,package,destination,passengers,revenue,payment,status,staff,notes,source,voided,void_reason";

    private const string PackageHeadings =
        "code,package,destination,duration,price,inclusions,slots,sold,reserved,status";

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

    private static async Task<(byte[] Bytes, string Text, HttpResponseMessage Response)> Fetch(
        HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return (bytes, Encoding.UTF8.GetString(bytes), response);
    }

    private static string[] Lines(string text) =>
        text.TrimStart((char)0xFEFF).Split("\r\n");

    [Fact]
    public async Task Export_is_closed_to_anyone_not_signed_in()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/export/bookings.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/export/packages.csv")).StatusCode);
    }

    [Fact]
    public async Task Booking_export_opens_with_a_byte_order_mark_and_the_exact_headings()
    {
        var (bytes, text, response) = await Fetch(await SignedIn(), "/api/export/bookings.csv");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(BookingHeadings, Lines(text)[0]);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);

        var disposition = response.Content.Headers.GetValues("Content-Disposition").Single();
        Assert.Contains("attachment", disposition);
        Assert.Contains("prophetops-bookings-", disposition);
        Assert.Contains(".csv", disposition);
    }

    [Fact]
    public async Task Package_export_opens_with_a_byte_order_mark_and_the_exact_headings()
    {
        var (bytes, text, response) = await Fetch(await SignedIn(), "/api/export/packages.csv");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(PackageHeadings, Lines(text)[0]);

        var disposition = response.Content.Headers.GetValues("Content-Disposition").Single();
        Assert.Contains("attachment", disposition);
        Assert.Contains("prophetops-packages-", disposition);
    }

    [Fact]
    public async Task A_client_written_as_a_formula_comes_back_unable_to_run()
    {
        var client = await SignedIn();

        var created = await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-EVIL1",
            ds = "2026-07-10",
            y = 2,
            client = "=HYPERLINK(\"http://evil\")",
            package = "Ad hoc",
            destination = "Cebu",
            grossRevenue = 90_000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            entryType = "Custom quotation",
            source = "Manual quotation",
        });
        created.EnsureSuccessStatusCode();

        var (_, text, _) = await Fetch(client, "/api/export/bookings.csv");

        Assert.Contains("'=HYPERLINK(", text);

        var row = BookingCsv.Parse(text).Rows.Single(r => r.Code == "BKG-EVIL1");
        Assert.StartsWith("'", row.Client);
        Assert.Contains("HYPERLINK(\"http://evil\")", row.Client);
    }

    [Fact]
    public async Task A_voided_booking_is_in_the_export_and_marked()
    {
        var client = await SignedIn();

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        var code = listing.GetProperty("bookings")[0].GetProperty("id").GetString()!;

        var voided = await client.PostAsJsonAsync($"/api/bookings/{code}/void", new { reason = "Double entry" });
        voided.EnsureSuccessStatusCode();

        var (_, text, _) = await Fetch(client, "/api/export/bookings.csv");
        var line = Lines(text).Single(l => l.StartsWith(code + ","));

        Assert.Contains(",yes,", line);
        Assert.EndsWith("Double entry", line);
    }

    [Fact]
    public async Task Package_export_round_trips_through_the_package_importer()
    {
        var client = await SignedIn();
        var count = (await client.GetFromJsonAsync<JsonElement>("/api/inventory")).GetArrayLength();

        var (_, text, _) = await Fetch(client, "/api/export/packages.csv");
        var parsed = PackageCsv.Parse(text);

        Assert.Empty(parsed.Problems);
        Assert.Equal(count, parsed.Rows.Count);

        var boracay = parsed.Rows.Single(r => r.Code == "PKG-101");
        Assert.Equal("Boracay Group Package", boracay.Package);
        Assert.Equal(12_000, boracay.Price);
        Assert.Equal("Low", boracay.Status);
    }

    [Fact]
    public async Task Booking_export_round_trips_through_the_booking_importer()
    {
        var client = await SignedIn();
        var count = (await client.GetFromJsonAsync<JsonElement>("/api/bookings"))
            .GetProperty("bookings").GetArrayLength();

        var (_, text, _) = await Fetch(client, "/api/export/bookings.csv");
        var parsed = BookingCsv.Parse(text);

        Assert.Empty(parsed.Problems);
        Assert.Empty(parsed.Warnings);
        Assert.Equal(count, parsed.Rows.Count);
    }
}
