using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProphetOps.Api.Tests;

public class AuditCoverageTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private Task<HttpClient> Owner() => AuthenticatedClient.Login(_factory);

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement[]> Activity(HttpClient client, string? entityType = null, string? entityCode = null)
    {
        var query = new List<string>();
        if (entityType is not null) query.Add("entityType=" + Uri.EscapeDataString(entityType));
        if (entityCode is not null) query.Add("entityCode=" + Uri.EscapeDataString(entityCode));
        var suffix = query.Count == 0 ? "" : "?" + string.Join("&", query);
        var entries = await client.GetFromJsonAsync<JsonElement[]>("/api/activity" + suffix);
        return entries ?? [];
    }

    private static MultipartFormDataContent FileNamed(string name, string contentType, byte[] content, int? revision = null)
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { part, "file", name } };
        if (revision is not null) form.Add(new StringContent(revision.Value.ToString()), "revision");
        return form;
    }

    private static MultipartFormDataContent Csv(string name, string content)
    {
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent
        {
            { part, "file", name },
            { new StringContent("true"), "confirm" },
        };
    }

    private static Task<HttpResponseMessage> DeleteImage(HttpClient client, string code, int revision)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/inventory/{code}/image")
        {
            Content = JsonContent.Create(new { revision }),
        };
        return client.SendAsync(request);
    }

    private static byte[] Png()
    {
        var bytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    [Fact]
    public async Task Activity_is_visible_to_report_roles_only()
    {
        using var owner = await Owner();
        using var admin = await AuthenticatedClient.Login(_factory, "admin@prophetops.local", "admin123");
        using var staff = await AuthenticatedClient.Login(_factory, "staff@prophetops.local", "staff123");

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/activity")).StatusCode);
    }

    [Fact]
    public async Task Package_expense_and_user_mutations_leave_secret_free_audit_entries()
    {
        using var client = await Owner();
        const string password = "NoLeak-password-284!";

        var createdPackage = await Body(await client.PostAsJsonAsync("/api/inventory", new
        {
            id = "PKG-AUDIT",
            packageName = "Audit Package",
            destination = "Cebu",
            duration = "3D2N",
            basePrice = 12000,
            inclusions = "Meals",
            availableSlots = 8,
            soldCount = 0,
            reservedCount = 0,
            status = "Normal",
        }));
        var packageRevision = createdPackage.GetProperty("revision").GetInt32();

        var updatedPackage = await Body(await client.PutAsJsonAsync("/api/inventory/PKG-AUDIT", new
        {
            id = "PKG-AUDIT",
            packageName = "Audit Package Plus",
            destination = "Cebu",
            duration = "4D3N",
            basePrice = 13000,
            inclusions = "Meals and transfers",
            availableSlots = 7,
            soldCount = 1,
            reservedCount = 0,
            status = "Low",
            revision = packageRevision,
        }));
        packageRevision = updatedPackage.GetProperty("revision").GetInt32();

        var uploaded = await Body(await client.PostAsync("/api/inventory/PKG-AUDIT/image",
            FileNamed("audit.png", "image/png", Png(), packageRevision)));
        packageRevision = uploaded.GetProperty("revision").GetInt32();
        (await DeleteImage(client, "PKG-AUDIT", packageRevision)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/expenses", new
        {
            id = "EXP-AUDIT",
            date = "2026-07-12",
            category = "Marketing",
            amount = 25000,
            relatedPackage = "General",
            paymentStatus = "Pending",
            notes = "Initial",
        })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/expenses/EXP-AUDIT", new
        {
            id = "EXP-AUDIT",
            date = "2026-07-13",
            category = "Transport",
            amount = 30000,
            relatedPackage = "Cebu",
            paymentStatus = "Paid",
            notes = "Updated",
        })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/expenses/EXP-AUDIT/void", new { reason = "Duplicate" })).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/expenses/EXP-AUDIT/restore", null)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/users", new
        {
            name = "Audit User",
            email = "audit-user@example.test",
            role = "Staff",
            password,
            status = "Active",
        })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/users/audit-user@example.test", new
        {
            name = "Audit User",
            email = "audit-user@example.test",
            role = "Staff",
            password,
            status = "Suspended",
        })).EnsureSuccessStatusCode();

        var packageActions = (await Activity(client, "TravelPackage", "PKG-AUDIT"))
            .Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Contains("Created", packageActions);
        Assert.True(packageActions.Count(a => a == "Updated") >= 3);

        var expenseActions = (await Activity(client, "Expense", "EXP-AUDIT"))
            .Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Equal(["Restored", "Voided", "Updated", "Created"], expenseActions.Take(4));

        var userTrail = await Activity(client, "User", "audit-user@example.test");
        Assert.Contains(userTrail, e => e.GetProperty("action").GetString() == "Created");
        Assert.Contains(userTrail, e => e.GetProperty("summary").GetString()?.Contains("Password reset") == true);
        var serialized = JsonSerializer.Serialize(userTrail);
        Assert.DoesNotContain(password, serialized);
        Assert.DoesNotContain("PasswordHash", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejected_mutations_do_not_leave_success_audit_entries()
    {
        using var client = await Owner();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/inventory", new
        {
            id = "PKG-REJECT",
            packageName = "",
            destination = "Cebu",
            basePrice = 12000,
            availableSlots = 1,
            soldCount = 0,
            reservedCount = 0,
            status = "Normal",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/expenses", new
        {
            id = "EXP-REJECT",
            date = "2026-07-12",
            category = "",
            amount = 1000,
            relatedPackage = "General",
            paymentStatus = "Pending",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new
        {
            name = "Reject User",
            email = "reject-user@example.test",
            role = "Staff",
            password = "short",
            status = "Active",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-REJECT",
            ds = "2026-07-12",
            y = 1,
            client = "Reject Client",
            packageId = "MISSING",
            package = "Missing package",
            destination = "Cebu",
            grossRevenue = 12000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            entryType = "Package preset",
            source = "Manual quotation",
        })).StatusCode);

        var serialized = JsonSerializer.Serialize(await Activity(client));
        Assert.DoesNotContain("PKG-REJECT", serialized);
        Assert.DoesNotContain("EXP-REJECT", serialized);
        Assert.DoesNotContain("reject-user@example.test", serialized);
        Assert.DoesNotContain("BKG-REJECT", serialized);
    }

    [Fact]
    public async Task Bulk_booking_update_is_audited_once_and_stale_bulk_is_not()
    {
        using var client = await Owner();

        var created = await Body(await client.PostAsJsonAsync("/api/bookings", new
        {
            id = "BKG-AUDIT-BULK",
            ds = "2026-07-12",
            y = 1,
            client = "Bulk audit",
            package = "Ad hoc",
            destination = "Cebu",
            grossRevenue = 12000,
            paymentStatus = "Pending",
            bookingStatus = "Pending",
            entryType = "Custom quotation",
            source = "Manual quotation",
        }));
        var revision = created.GetProperty("revision").GetInt32();

        (await client.PostAsJsonAsync("/api/bookings/bulk", new
        {
            ids = new[] { "BKG-AUDIT-BULK" },
            action = "confirm",
            revisions = new Dictionary<string, int> { ["BKG-AUDIT-BULK"] = revision },
        })).EnsureSuccessStatusCode();
        var stale = await client.PostAsJsonAsync("/api/bookings/bulk", new
        {
            ids = new[] { "BKG-AUDIT-BULK" },
            action = "paid",
            revisions = new Dictionary<string, int> { ["BKG-AUDIT-BULK"] = revision },
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var actions = (await Activity(client, "Booking", "BKG-AUDIT-BULK"))
            .Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Equal(["Updated", "Created"], actions);
    }

    [Fact]
    public async Task Duplicate_booking_import_with_no_new_rows_does_not_add_an_import_audit()
    {
        using var client = await Owner();
        const string csv = """
            code,date,client,package,destination,passengers,revenue,payment,status,staff,notes
            IMP-AUDIT-1,2026-07-12,Import Audit,Ad hoc,Cebu,2,24000,Pending,Pending,Staff User,First import
            """;
        var before = (await Activity(client, "Booking")).Count(e => e.GetProperty("action").GetString() == "Imported");

        var first = await Body(await client.PostAsync("/api/import/bookings/commit", Csv("bookings.csv", csv)));
        Assert.Equal(1, first.GetProperty("imported").GetInt32());
        var afterFirst = (await Activity(client, "Booking")).Count(e => e.GetProperty("action").GetString() == "Imported");
        Assert.Equal(before + 1, afterFirst);

        var second = await Body(await client.PostAsync("/api/import/bookings/commit", Csv("bookings.csv", csv)));
        Assert.Equal(0, second.GetProperty("imported").GetInt32());
        var afterSecond = (await Activity(client, "Booking")).Count(e => e.GetProperty("action").GetString() == "Imported");
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Stale_package_image_upload_preserves_the_current_photo_and_audit_count()
    {
        using var client = await Owner();

        var first = await Body(await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("photo.png", "image/png", Png(), revision: 1)));
        var firstRevision = first.GetProperty("revision").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/inventory/PKG-101/image",
            FileNamed("stale.png", "image/png", Png(), revision: 1))).StatusCode);

        var fetched = await client.GetAsync("/api/inventory/PKG-101/image");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/png", fetched.Content.Headers.ContentType?.MediaType);
        var imageUpdates = (await Activity(client, "TravelPackage", "PKG-101"))
            .Count(e => e.GetProperty("summary").GetString()?.Contains("Photo") == true);
        Assert.Equal(1, imageUpdates);

        (await DeleteImage(client, "PKG-101", firstRevision)).EnsureSuccessStatusCode();
    }
}
