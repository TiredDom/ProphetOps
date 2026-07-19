using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BookingCsvTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private const string Header = "date,client,package,destination,passengers,revenue";

    private const string OneGoodRow = "2024-03-05,Cruz Family,Boracay Escape,Boracay,4,45000";

    private static string Sheet(params string[] rows) => Header + "\n" + string.Join("\n", rows) + "\n";

    [Fact]
    public void Reads_a_clean_file()
    {
        var result = BookingCsv.Parse(Sheet(OneGoodRow));

        Assert.Empty(result.Problems);
        var row = Assert.Single(result.Rows);
        Assert.Equal(new DateOnly(2024, 3, 5), row.Date);
        Assert.Equal("Cruz Family", row.Client);
        Assert.Equal("Boracay Escape", row.Package);
        Assert.Equal("Boracay", row.Destination);
        Assert.Equal(4, row.Passengers);
        Assert.Equal(45_000, row.Revenue);
        Assert.Equal(2, row.Line);
    }

    [Fact]
    public void Reads_the_columns_wherever_the_sheet_happens_to_put_them()
    {
        var result = BookingCsv.Parse(
            "Revenue, PASSENGERS ,Destination,package,Client,date\n"
            + "45000,4,Boracay,Boracay Escape,Cruz Family,2024-03-05\n");

        Assert.Empty(result.Problems);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Cruz Family", row.Client);
        Assert.Equal(45_000, row.Revenue);
        Assert.Equal(4, row.Passengers);
    }

    [Fact]
    public void Keeps_a_comma_that_belongs_inside_a_quoted_field()
    {
        var result = BookingCsv.Parse(Sheet(
            "2024-03-05,\"Cruz, Reyes and Santos\",Boracay Escape,\"Boracay, Aklan\",4,45000"));

        Assert.Empty(result.Problems);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Cruz, Reyes and Santos", row.Client);
        Assert.Equal("Boracay, Aklan", row.Destination);
    }

    [Fact]
    public void Reads_a_doubled_quote_as_the_one_quote_it_stands_for()
    {
        var result = BookingCsv.Parse(Sheet(
            "2024-03-05,\"The \"\"Big Four\"\" Group\",Boracay Escape,Boracay,4,45000"));

        Assert.Empty(result.Problems);
        Assert.Equal("The \"Big Four\" Group", Assert.Single(result.Rows).Client);
    }

    [Fact]
    public void Reads_a_file_that_opens_with_a_byte_order_mark()
    {
        var result = BookingCsv.Parse("\uFEFF" + Sheet(OneGoodRow));

        Assert.Empty(result.Problems);
        Assert.Equal("Cruz Family", Assert.Single(result.Rows).Client);
    }

    [Fact]
    public void Reads_the_same_rows_whether_the_lines_end_in_crlf_or_lf()
    {
        var unix = BookingCsv.Parse(Sheet(OneGoodRow, "2024-04-06,Reyes Group,Palawan Discovery,Palawan,2,60000"));
        var windows = BookingCsv.Parse(Sheet(OneGoodRow, "2024-04-06,Reyes Group,Palawan Discovery,Palawan,2,60000")
            .Replace("\n", "\r\n"));

        Assert.Empty(windows.Problems);
        Assert.Equal(unix.Rows.Count, windows.Rows.Count);
        Assert.Equal(unix.Rows.Select(r => (r.Line, r.Date, r.Client, r.Revenue)),
            windows.Rows.Select(r => (r.Line, r.Date, r.Client, r.Revenue)));
    }

    [Theory]
    [InlineData("2024-03-05", 2024, 3, 5)]
    [InlineData("25/12/2024", 2024, 12, 25)]
    [InlineData("12/25/2024", 2024, 12, 25)]
    [InlineData("5 Mar 2024", 2024, 3, 5)]
    [InlineData("05 March 2024", 2024, 3, 5)]
    public void Accepts_the_date_formats_a_spreadsheet_is_likely_to_hold(string written, int year, int month, int day)
    {
        var result = BookingCsv.Parse(Sheet($"{written},Cruz Family,Boracay Escape,Boracay,4,45000"));

        Assert.Empty(result.Problems);
        Assert.Equal(new DateOnly(year, month, day), Assert.Single(result.Rows).Date);
    }

    [Fact]
    public void Reads_a_date_that_could_go_either_way_as_day_first_and_says_it_did()
    {
        var result = BookingCsv.Parse(Sheet("03/04/2024,Cruz Family,Boracay Escape,Boracay,4,45000"));

        Assert.Empty(result.Problems);
        Assert.Equal(new DateOnly(2024, 4, 3), Assert.Single(result.Rows).Date);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(2, warning.Line);
        Assert.Contains("day first", warning.Reason);
    }

    [Fact]
    public void Says_nothing_about_a_slash_date_that_can_only_be_read_one_way()
    {
        var result = BookingCsv.Parse(Sheet("25/12/2024,Cruz Family,Boracay Escape,Boracay,4,45000"));

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Names_the_line_of_a_date_it_cannot_read()
    {
        var result = BookingCsv.Parse(Sheet("last Tuesday,Cruz Family,Boracay Escape,Boracay,4,45000"));

        Assert.Empty(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Contains("is not a date", problem.Reason);
    }

    [Theory]
    [InlineData("45000")]
    [InlineData("45,000")]
    [InlineData("₱45,000")]
    [InlineData("P45,000")]
    [InlineData("PHP 45,000")]
    [InlineData("45 000")]
    [InlineData("45,000.00")]
    [InlineData(" ₱ 45,000.49 ")]
    public void Reads_an_amount_however_the_peso_sign_and_the_commas_were_typed(string written)
    {
        var result = BookingCsv.Parse(Sheet($"2024-03-05,Cruz Family,Boracay Escape,Boracay,4,\"{written}\""));

        Assert.Empty(result.Problems);
        Assert.Equal(45_000, Assert.Single(result.Rows).Revenue);
    }

    [Fact]
    public void Refuses_an_amount_below_zero()
    {
        var result = BookingCsv.Parse(Sheet("2024-03-05,Cruz Family,Boracay Escape,Boracay,4,\"-45,000\""));

        Assert.Empty(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Contains("negative", problem.Reason);
    }

    [Fact]
    public void Reports_a_column_the_file_does_not_have()
    {
        var result = BookingCsv.Parse("date,client,package,destination,passengers\n"
            + "2024-03-05,Cruz Family,Boracay Escape,Boracay,4\n");

        Assert.Empty(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(1, problem.Line);
        Assert.Contains("revenue", problem.Reason);
    }

    [Fact]
    public void Names_the_line_a_bad_row_sits_on_and_still_reads_the_good_ones()
    {
        var result = BookingCsv.Parse(Sheet(
            OneGoodRow,
            "2024-03-06,,Palawan Discovery,Palawan,2,60000",
            "2024-03-07,Santos Family,Cebu Weekend,Cebu,3,30000"));

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new[] { 2, 4 }, result.Rows.Select(r => r.Line));

        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("client is missing", problem.Reason);
    }

    [Fact]
    public void Gives_every_reason_a_row_was_left_out_rather_than_only_the_first()
    {
        var result = BookingCsv.Parse(Sheet("2024-03-05,,Boracay Escape,Boracay,none,45000"));

        var problem = Assert.Single(result.Problems);
        Assert.Contains("client is missing", problem.Reason);
        Assert.Contains("'none' is not a whole number of passengers", problem.Reason);
    }

    [Fact]
    public void Ignores_lines_with_nothing_on_them()
    {
        var result = BookingCsv.Parse(Header + "\n\n" + OneGoodRow + "\n   \n"
            + "2024-03-07,Santos Family,Cebu Weekend,Cebu,3,30000\n\n");

        Assert.Empty(result.Problems);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new[] { 3, 5 }, result.Rows.Select(r => r.Line));
    }

    [Fact]
    public void Reads_a_file_that_fills_the_row_limit()
    {
        var result = BookingCsv.Parse(Sheet(Enumerable.Repeat(OneGoodRow, BookingCsv.MaxRows).ToArray()));

        Assert.Empty(result.Problems);
        Assert.Equal(BookingCsv.MaxRows, result.Rows.Count);
    }

    [Fact]
    public void Refuses_a_file_longer_than_it_will_import_at_once()
    {
        var result = BookingCsv.Parse(Sheet(Enumerable.Repeat(OneGoodRow, BookingCsv.MaxRows + 1).ToArray()));

        Assert.Empty(result.Rows);
        Assert.Contains(BookingCsv.MaxRows.ToString(), Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public void Reports_a_reference_the_file_uses_twice()
    {
        var result = BookingCsv.Parse(
            Header + ",code\n"
            + OneGoodRow + ",BKG-9001\n"
            + "2024-03-07,Santos Family,Cebu Weekend,Cebu,3,30000,BKG-9001\n");

        Assert.Single(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("already used on line 2", problem.Reason);
    }

    [Fact]
    public void Reports_a_row_carrying_more_values_than_the_headings_cover()
    {
        var result = BookingCsv.Parse(Sheet("2024-03-05,Cruz Family,Boracay Escape,Boracay,4,45,000"));

        Assert.Empty(result.Rows);
        Assert.Contains("comma", Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public void Says_so_when_there_is_nothing_in_the_file_at_all()
    {
        Assert.Contains("empty", Assert.Single(BookingCsv.Parse("").Problems).Reason);
        Assert.Contains("no bookings", Assert.Single(BookingCsv.Parse(Header + "\n").Problems).Reason);
    }

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

    private static MultipartFormDataContent Upload(byte[] content, string contentType, string? confirm)
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent { { part, "file", "history.csv" } };
        if (confirm is not null) form.Add(new StringContent(confirm), "confirm");
        return form;
    }

    private static MultipartFormDataContent Upload(string csv, string contentType = "text/csv", string? confirm = null) =>
        Upload(Encoding.UTF8.GetBytes(csv), contentType, confirm);

    private static async Task<int> FreeSlots(HttpClient client)
    {
        var packages = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        return packages.EnumerateArray().Sum(p => p.GetProperty("availableSlots").GetInt32());
    }

    [Fact]
    public async Task Preview_counts_what_would_come_in_and_writes_nothing()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/bookings/preview", Upload(Sheet(
            "2024-03-05,Cruz Family,Boracay Escape,Boracay,4,\"₱45,000\"",
            "2024-04-06,,Palawan Discovery,Palawan,2,60000",
            "2024-05-07,Santos Family,Cebu Weekend,Cebu,3,30000")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("valid").GetInt32());
        Assert.Equal(1, body.GetProperty("skipped").GetInt32());
        Assert.Equal(75_000, body.GetProperty("totalRevenue").GetInt64());
        Assert.Equal("2024-03-05", body.GetProperty("from").GetString());
        Assert.Equal("2024-05-07", body.GetProperty("to").GetString());
        Assert.Equal(2, body.GetProperty("months").GetInt32());
        Assert.Equal(3, body.GetProperty("problems")[0].GetProperty("line").GetInt32());

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        Assert.DoesNotContain("Cruz Family", listing.GetProperty("bookings").ToString());
    }

    [Fact]
    public async Task Commit_saves_the_rows_and_leaves_the_package_slots_where_they_were()
    {
        var client = await SignedIn();
        var before = await FreeSlots(client);

        var response = await client.PostAsync("/api/import/bookings/commit", Upload(Sheet(
            "2024-03-05,Cruz Family,Boracay Escape,Boracay,4,\"₱45,000\"",
            "2024-04-06,Reyes Group,Palawan Discovery,Palawan,2,60000"), confirm: "true"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("imported").GetInt32());
        Assert.Equal(0, body.GetProperty("skipped").GetInt32());

        Assert.Equal(before, await FreeSlots(client));

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        var saved = listing.GetProperty("bookings").EnumerateArray()
            .Single(b => b.GetProperty("client").GetString() == "Cruz Family");

        Assert.StartsWith("IMP-", saved.GetProperty("id").GetString());
        Assert.Equal("Imported", saved.GetProperty("source").GetString());
        Assert.Equal("Custom quotation", saved.GetProperty("entryType").GetString());
        Assert.Equal("Boracay Escape", saved.GetProperty("package").GetString());
        Assert.Equal(45_000, saved.GetProperty("grossRevenue").GetInt32());
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("packageId").ValueKind);
    }

    [Fact]
    public async Task Commit_keeps_the_reference_the_sheet_already_carries()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/bookings/commit", Upload(
            Header + ",code\n" + OneGoodRow + ",SHEET-77\n", confirm: "true"));

        response.EnsureSuccessStatusCode();

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        Assert.Contains("SHEET-77", listing.GetProperty("bookings").ToString());
    }

    [Fact]
    public async Task Commit_leaves_a_booking_the_system_already_has_alone()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/bookings/commit", Upload(
            Header + ",code\n"
            + OneGoodRow + ",BKG-2401\n"
            + "2024-03-07,Santos Family,Cebu Weekend,Cebu,3,30000,BKG-9002\n", confirm: "true"));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("imported").GetInt32());
        Assert.Equal(1, body.GetProperty("duplicates").GetInt32());

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        Assert.DoesNotContain("Cruz Family", listing.GetProperty("bookings").ToString());
    }

    [Fact]
    public async Task Commit_records_the_run_once_in_the_activity_trail()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/bookings/commit", Upload(Sheet(
            OneGoodRow,
            "2024-04-06,Reyes Group,Palawan Discovery,Palawan,2,60000"), confirm: "true"));

        response.EnsureSuccessStatusCode();

        var trail = await client.GetFromJsonAsync<JsonElement>("/api/activity?entityType=Booking");
        var entries = trail.EnumerateArray()
            .Where(e => e.GetProperty("action").GetString() == "Imported")
            .ToList();

        Assert.Single(entries);
        Assert.Contains("2 bookings imported", entries[0].GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Commit_holds_off_until_the_import_is_confirmed()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/bookings/commit", Upload(Sheet(OneGoodRow)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        Assert.DoesNotContain("Cruz Family", listing.GetProperty("bookings").ToString());
    }

    [Fact]
    public async Task Refuses_a_file_that_is_not_text_whatever_it_is_called()
    {
        var client = await SignedIn();

        // A workbook rather than the CSV export of one. Both the name and the content type are
        // the caller's to choose, so neither is what decides.
        var workbook = new byte[64];
        new byte[] { 0x50, 0x4B, 0x03, 0x04 }.CopyTo(workbook, 0);

        var response = await client.PostAsync("/api/import/bookings/preview",
            Upload(workbook, "text/csv", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refuses_a_content_type_that_is_not_a_csv()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/bookings/preview",
            Upload(Sheet(OneGoodRow), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_is_closed_to_anyone_not_signed_in()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/import/bookings/preview", Upload(Sheet(OneGoodRow)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
