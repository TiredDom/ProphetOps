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
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/export/expenses.csv")).StatusCode);
    }

    private const string ExpenseHeadings =
        "code,date,category,amount,related_package,payment,notes,voided,void_reason";

    private async Task<HttpClient> SignedInAs(string email, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task CreateExpense(HttpClient client, string code, string date, int amount)
    {
        var created = await client.PostAsJsonAsync("/api/expenses", new
        {
            id = code,
            date,
            category = "Marketing",
            amount,
            relatedPackage = "Boracay Group Package",
            paymentStatus = "Pending",
            notes = "Export check",
        });
        created.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Expense_export_opens_with_a_byte_order_mark_and_the_exact_headings()
    {
        var client = await SignedIn();
        var count = (await client.GetFromJsonAsync<JsonElement>("/api/expenses")).GetArrayLength();

        var (bytes, text, response) = await Fetch(client, "/api/export/expenses.csv");
        var lines = Lines(text).Where(l => l.Length > 0).ToArray();

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(ExpenseHeadings, lines[0]);
        Assert.Equal(count, lines.Length - 1);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);

        var disposition = response.Content.Headers.GetValues("Content-Disposition").Single();
        Assert.Contains("attachment", disposition);
        Assert.Contains("prophetops-expenses-", disposition);
    }

    [Fact]
    public async Task Expense_export_can_be_limited_to_a_period()
    {
        var client = await SignedIn();
        await CreateExpense(client, "EXP-JUL1", "2026-07-10", 12_000);
        await CreateExpense(client, "EXP-AUG1", "2026-08-20", 8_000);

        var (_, text, response) = await Fetch(client, "/api/export/expenses.csv?from=2026-07-01&to=2026-07-31");
        var rows = Lines(text).Skip(1).Where(l => l.Length > 0).ToArray();

        Assert.Contains(rows, r => r.StartsWith("EXP-JUL1,2026-07-10,Marketing,12000,"));
        Assert.DoesNotContain(rows, r => r.StartsWith("EXP-AUG1,"));
        Assert.All(rows, r =>
        {
            var date = r.Split(',')[1];
            Assert.InRange(string.CompareOrdinal(date, "2026-07-01"), 0, int.MaxValue);
            Assert.InRange(string.CompareOrdinal(date, "2026-07-31"), int.MinValue, 0);
        });

        var disposition = response.Content.Headers.GetValues("Content-Disposition").Single();
        Assert.Contains("prophetops-expenses-2026-07-01-to-2026-07-31.csv", disposition);
    }

    [Fact]
    public async Task Expense_export_with_only_a_start_date_runs_to_the_latest_expense()
    {
        var client = await SignedIn();
        await CreateExpense(client, "EXP-JUL2", "2026-07-10", 5_000);
        await CreateExpense(client, "EXP-AUG2", "2026-08-20", 6_000);

        var (_, text, _) = await Fetch(client, "/api/export/expenses.csv?from=2026-08-01");
        var rows = Lines(text).Skip(1).Where(l => l.Length > 0).ToArray();

        Assert.Contains(rows, r => r.StartsWith("EXP-AUG2,"));
        Assert.DoesNotContain(rows, r => r.StartsWith("EXP-JUL2,"));
    }

    [Fact]
    public async Task A_voided_expense_is_in_the_export_and_marked()
    {
        var client = await SignedIn();
        await CreateExpense(client, "EXP-VOID1", "2026-07-12", 4_500);

        var voided = await client.PostAsJsonAsync("/api/expenses/EXP-VOID1/void", new { reason = "Entered twice" });
        voided.EnsureSuccessStatusCode();

        var (_, text, _) = await Fetch(client, "/api/export/expenses.csv");
        var line = Lines(text).Single(l => l.StartsWith("EXP-VOID1,"));

        Assert.Contains(",yes,", line);
        Assert.EndsWith("Entered twice", line);
    }

    [Theory]
    [InlineData("from=2026-13-01")]
    [InlineData("to=yesterday")]
    [InlineData("from=2026-08-01&to=2026-07-01")]
    public async Task Expense_export_refuses_a_period_it_cannot_read(string query)
    {
        var client = await SignedIn();

        var response = await client.GetAsync("/api/export/expenses.csv?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staff_cannot_export_expenses()
    {
        var client = await SignedInAs("staff@prophetops.local", "staff123");

        var response = await client.GetAsync("/api/export/expenses.csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        var row = listing.GetProperty("bookings")[0];
        var code = row.GetProperty("id").GetString()!;
        var revision = row.GetProperty("revision").GetInt32();

        var voided = await client.PostAsJsonAsync($"/api/bookings/{code}/void", new { reason = "Double entry", revision });
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

        // The status is derived from the remaining slots, so it moves whenever the demonstration
        // data is regenerated. What has to hold is that the export carries across what the module
        // reports, not that it carries across one particular value.
        var live = (await client.GetFromJsonAsync<JsonElement>("/api/inventory"))
            .EnumerateArray()
            .Single(p => p.GetProperty("id").GetString() == "PKG-101")
            .GetProperty("status").GetString();
        Assert.Equal(live, boracay.Status);
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
