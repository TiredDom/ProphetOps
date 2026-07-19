using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Api;
using ProphetOps.Api.Controllers;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class PackageCsvTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private const string Header = "package,destination,price";

    private const string OneGoodRow = "Siquijor Dive Package,Siquijor,15000";

    private static string Sheet(params string[] rows) => Header + "\n" + string.Join("\n", rows) + "\n";

    [Fact]
    public void Reads_a_clean_file()
    {
        var result = PackageCsv.Parse(
            "package,destination,price,code,duration,inclusions,slots,status\n"
            + "Siquijor Dive Package,Siquijor,15000,PKG-201,3D2N,\"Hotel, dives, transfers\",10,Low\n");

        Assert.Empty(result.Problems);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Siquijor Dive Package", row.Package);
        Assert.Equal("Siquijor", row.Destination);
        Assert.Equal(15_000, row.Price);
        Assert.Equal("PKG-201", row.Code);
        Assert.Equal("3D2N", row.Duration);
        Assert.Equal("Hotel, dives, transfers", row.Inclusions);
        Assert.Equal(10, row.Slots);
        Assert.Equal("Low", row.Status);
        Assert.Equal(2, row.Line);
    }

    [Fact]
    public void Reads_the_columns_wherever_the_sheet_happens_to_put_them()
    {
        var result = PackageCsv.Parse(
            "PRICE, Destination ,package\n"
            + "15000,Siquijor,Siquijor Dive Package\n");

        Assert.Empty(result.Problems);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Siquijor Dive Package", row.Package);
        Assert.Equal(15_000, row.Price);
    }

    [Fact]
    public void Reports_a_column_the_file_does_not_have()
    {
        var result = PackageCsv.Parse("package,destination\nSiquijor Dive Package,Siquijor\n");

        Assert.Empty(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(1, problem.Line);
        Assert.Contains("price", problem.Reason);
    }

    [Theory]
    [InlineData("15000")]
    [InlineData("15,000")]
    [InlineData("₱15,000")]
    [InlineData("P15,000")]
    [InlineData("PHP 15,000")]
    [InlineData("15,000.00")]
    [InlineData(" ₱ 15,000.49 ")]
    public void Reads_a_price_however_the_peso_sign_and_the_commas_were_typed(string written)
    {
        var result = PackageCsv.Parse(Sheet($"Siquijor Dive Package,Siquijor,\"{written}\""));

        Assert.Empty(result.Problems);
        Assert.Equal(15_000, Assert.Single(result.Rows).Price);
    }

    [Fact]
    public void Refuses_a_price_below_zero()
    {
        var result = PackageCsv.Parse(Sheet("Siquijor Dive Package,Siquijor,\"-15,000\""));

        Assert.Empty(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Contains("negative", problem.Reason);
    }

    [Fact]
    public void Refuses_a_status_it_does_not_know()
    {
        var result = PackageCsv.Parse(
            "package,destination,price,status\n"
            + "Siquijor Dive Package,Siquijor,15000,Sold Out\n");

        Assert.Empty(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(2, problem.Line);
        Assert.Contains("'Sold Out' is not a package status", problem.Reason);
        Assert.Contains("Normal, Low, or Critical", problem.Reason);
    }

    [Fact]
    public void Reads_a_status_whatever_its_casing_and_writes_it_properly()
    {
        var result = PackageCsv.Parse(
            "package,destination,price,status\n"
            + "Siquijor Dive Package,Siquijor,15000,critical\n");

        Assert.Empty(result.Problems);
        Assert.Equal("Critical", Assert.Single(result.Rows).Status);
    }

    [Fact]
    public void Takes_blank_or_missing_slots_as_zero()
    {
        var blank = PackageCsv.Parse("package,destination,price,slots\nSiquijor Dive Package,Siquijor,15000,\n");
        var absent = PackageCsv.Parse(Sheet(OneGoodRow));

        Assert.Empty(blank.Problems);
        Assert.Equal(0, Assert.Single(blank.Rows).Slots);
        Assert.Empty(absent.Problems);
        Assert.Equal(0, Assert.Single(absent.Rows).Slots);
    }

    [Fact]
    public void Keeps_a_comma_that_belongs_inside_a_quoted_field()
    {
        var result = PackageCsv.Parse(Sheet("\"Siquijor, Apo Island Twin Tour\",\"Siquijor, Negros\",15000"));

        Assert.Empty(result.Problems);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Siquijor, Apo Island Twin Tour", row.Package);
        Assert.Equal("Siquijor, Negros", row.Destination);
    }

    [Fact]
    public void Names_the_line_a_bad_row_sits_on_and_still_reads_the_good_ones()
    {
        var result = PackageCsv.Parse(Sheet(
            OneGoodRow,
            "Apo Island Day Trip,,9000",
            "Camiguin Loop,Camiguin,11000"));

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new[] { 2, 4 }, result.Rows.Select(r => r.Line));

        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("destination is missing", problem.Reason);
    }

    [Fact]
    public void Reports_a_package_the_file_lists_twice()
    {
        var result = PackageCsv.Parse(Sheet(OneGoodRow, "SIQUIJOR DIVE PACKAGE,Siquijor,16000"));

        Assert.Single(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("already on line 2", problem.Reason);
    }

    [Fact]
    public void Reports_a_code_the_file_uses_twice()
    {
        var result = PackageCsv.Parse(
            Header + ",code\n"
            + OneGoodRow + ",PKG-201\n"
            + "Camiguin Loop,Camiguin,11000,PKG-201\n");

        Assert.Single(result.Rows);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Contains("already used on line 2", problem.Reason);
    }

    [Fact]
    public void Reads_a_file_that_fills_the_row_limit()
    {
        var rows = Enumerable.Range(1, PackageCsv.MaxRows).Select(i => $"Tour {i},Boracay,12000").ToArray();
        var result = PackageCsv.Parse(Sheet(rows));

        Assert.Empty(result.Problems);
        Assert.Equal(PackageCsv.MaxRows, result.Rows.Count);
    }

    [Fact]
    public void Refuses_a_file_longer_than_it_will_import_at_once()
    {
        var rows = Enumerable.Range(1, PackageCsv.MaxRows + 1).Select(i => $"Tour {i},Boracay,12000").ToArray();
        var result = PackageCsv.Parse(Sheet(rows));

        Assert.Empty(result.Rows);
        Assert.Contains(PackageCsv.MaxRows.ToString(), Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public void Says_so_when_there_is_nothing_in_the_file_at_all()
    {
        Assert.Contains("empty", Assert.Single(PackageCsv.Parse("").Problems).Reason);
        Assert.Contains("no packages", Assert.Single(PackageCsv.Parse(Header + "\n").Problems).Reason);
    }

    private async Task<HttpClient> SignedIn(string email = "owner@prophetops.local", string password = "owner123")
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();

        var probe = await client.GetAsync("/api/auth/me");
        var cookie = probe.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(cookie.Split(';')[0]["XSRF-TOKEN=".Length..]);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        return client;
    }

    private static MultipartFormDataContent Upload(string csv, string? confirm = null)
    {
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        var form = new MultipartFormDataContent { { part, "file", "packages.csv" } };
        if (confirm is not null) form.Add(new StringContent(confirm), "confirm");
        return form;
    }

    [Fact]
    public async Task Preview_counts_what_would_come_in_and_writes_nothing()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/packages/preview", Upload(
            "package,destination,price,slots\n"
            + "Siquijor Dive Package,Siquijor,15000,10\n"
            + "Boracay Group Package,Boracay,12000,5\n"
            + ",Cebu,9000,3\n"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("valid").GetInt32());
        Assert.Equal(1, body.GetProperty("skipped").GetInt32());
        Assert.Equal(1, body.GetProperty("duplicates").GetInt32());
        Assert.Equal("Boracay Group Package", body.GetProperty("duplicateNames")[0].GetString());
        Assert.Equal(2, body.GetProperty("destinations").GetInt32());
        Assert.Equal(15, body.GetProperty("totalSlots").GetInt32());
        Assert.Equal(4, body.GetProperty("problems")[0].GetProperty("line").GetInt32());

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        Assert.DoesNotContain("Siquijor Dive Package", listing.ToString());
    }

    [Fact]
    public async Task Commit_saves_new_packages_under_fresh_codes_dated_today()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/packages/commit", Upload(
            "package,destination,price,slots,duration,inclusions,status\n"
            + "Siquijor Dive Package,Siquijor,\"₱15,000\",10,3D2N,\"Hotel, dives, transfers\",low\n",
            confirm: "true"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("IMP-PKG-", body.GetProperty("batch").GetString());
        Assert.Equal(1, body.GetProperty("imported").GetInt32());
        Assert.Equal(0, body.GetProperty("skipped").GetInt32());
        Assert.Equal(10, body.GetProperty("totalSlots").GetInt32());

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        var saved = listing.EnumerateArray()
            .Single(p => p.GetProperty("packageName").GetString() == "Siquijor Dive Package");

        var code = saved.GetProperty("id").GetString()!;
        Assert.StartsWith("PKG-", code);
        Assert.Equal("PKG-".Length + 6, code.Length);
        Assert.Equal(15_000, saved.GetProperty("basePrice").GetInt32());
        Assert.Equal(10, saved.GetProperty("availableSlots").GetInt32());
        Assert.Equal(0, saved.GetProperty("soldCount").GetInt32());
        Assert.Equal("Low", saved.GetProperty("status").GetString());
        Assert.Equal("3D2N", saved.GetProperty("duration").GetString());
        Assert.Equal("Hotel, dives, transfers", saved.GetProperty("inclusions").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = db.TravelPackages.Single(p => p.Code == code);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), package.LastUpdatedAt);
    }

    [Fact]
    public async Task Commit_leaves_a_package_the_catalog_already_holds_under_that_name_alone()
    {
        var client = await SignedIn();
        var before = (await client.GetFromJsonAsync<JsonElement>("/api/inventory")).GetArrayLength();

        var response = await client.PostAsync("/api/import/packages/commit", Upload(Sheet(
            "BORACAY GROUP PACKAGE,Boracay,12000",
            OneGoodRow), confirm: "true"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("imported").GetInt32());
        Assert.Equal(1, body.GetProperty("duplicates").GetInt32());
        Assert.Equal("BORACAY GROUP PACKAGE", body.GetProperty("duplicateNames")[0].GetString());

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        Assert.Equal(before + 1, listing.GetArrayLength());
        Assert.Single(listing.EnumerateArray(), p =>
            string.Equals(p.GetProperty("packageName").GetString(), "Boracay Group Package", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Commit_leaves_a_package_the_catalog_already_holds_under_that_code_alone()
    {
        var client = await SignedIn();
        var before = (await client.GetFromJsonAsync<JsonElement>("/api/inventory")).GetArrayLength();

        var response = await client.PostAsync("/api/import/packages/commit", Upload(
            Header + ",code\n" + "Totally New Name,Boracay,12000,pkg-101\n", confirm: "true"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("imported").GetInt32());
        Assert.Equal(1, body.GetProperty("duplicates").GetInt32());

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        Assert.Equal(before, listing.GetArrayLength());
        Assert.DoesNotContain("Totally New Name", listing.ToString());
    }

    [Fact]
    public async Task Commit_holds_off_until_the_import_is_confirmed()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/packages/commit", Upload(Sheet(OneGoodRow)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/inventory");
        Assert.DoesNotContain("Siquijor Dive Package", listing.ToString());
    }

    [Fact]
    public async Task Commit_records_the_run_once_in_the_activity_trail()
    {
        var client = await SignedIn();

        var response = await client.PostAsync("/api/import/packages/commit", Upload(Sheet(
            OneGoodRow,
            "Camiguin Loop,Camiguin,11000"), confirm: "true"));

        response.EnsureSuccessStatusCode();

        var trail = await client.GetFromJsonAsync<JsonElement>("/api/activity?entityType=TravelPackage");
        var entries = trail.EnumerateArray()
            .Where(e => e.GetProperty("action").GetString() == "Imported")
            .ToList();

        Assert.Single(entries);
        Assert.StartsWith("IMP-PKG-", entries[0].GetProperty("entityCode").GetString());
        Assert.Contains("2 packages imported", entries[0].GetProperty("summary").GetString());
        Assert.Contains("packages.csv", entries[0].GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Package_import_is_closed_to_anyone_not_signed_in()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/import/packages/preview", Upload(Sheet(OneGoodRow)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_can_import_packages_because_the_role_matrix_hands_them_the_catalog()
    {
        Assert.True(Roles.CanAccess(Roles.Staff, "Package Catalog"));

        var client = await SignedIn("staff@prophetops.local", "staff123");
        var response = await client.PostAsync("/api/import/packages/preview", Upload(Sheet(OneGoodRow)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Package_import_asks_for_the_catalog_permission_not_the_controller_booking_one()
    {
        foreach (var name in new[] { nameof(ImportController.PreviewPackages), nameof(ImportController.CommitPackages) })
        {
            var action = typeof(ImportController).GetMethod(name)!;
            var attribute = Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal("Package Catalog", attribute.Policy);
        }
    }
}
