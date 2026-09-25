using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class FinancialConsistencyTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> Fixture(bool empty = false)
    {
        var client = await AuthenticatedClient.Login(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Bookings.RemoveRange(db.Bookings);
        db.Expenses.RemoveRange(db.Expenses);
        db.SaveChanges();
        if (!empty)
        {
            db.Bookings.AddRange(
                Booking("JUL-1", "2026-07-01", 1000, 2),
                Booking("JUL-2", "2026-07-31", 2000, 3),
                Booking("JUL-VOID", "2026-07-15", 9000, 9, true));
            db.Expenses.AddRange(
                Expense("COST-1", 4000),
                Expense("COST-VOID", 8000, true));
            db.SaveChanges();
        }
        return client;
    }

    private static Booking Booking(string code, string date, int revenue, int passengers = 1, bool voided = false) => new()
    {
        Code = code, BookingDate = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        GrossRevenue = revenue, PassengerCount = passengers, Client = "Agency client",
        PackageName = "July package", Destination = "Cebu", BookingStatus = "Confirmed",
        PaymentStatus = "Pending", VoidedAt = voided ? new DateTime(2026, 8, 1) : null,
    };

    private static Expense Expense(string code, int amount, bool voided = false) => new()
    {
        Code = code, ExpenseDate = new DateOnly(2026, 7, 15), Amount = amount,
        Category = "Transport", VoidedAt = voided ? new DateTime(2026, 8, 1) : null,
    };

    [Fact]
    public async Task Analytics_history_uses_active_booking_revenue()
    {
        using var client = await Fixture();
        var data = await client.GetFromJsonAsync<JsonElement>("/api/analytics?throughMonth=2026-07-01");
        var history = data.GetProperty("salesHistory");
        Assert.Equal(3000, history[11].GetProperty("value").GetInt64());
        Assert.Equal("2026-07", history[11].GetProperty("month").GetString());
        Assert.Equal("Jul 2026", history[11].GetProperty("label").GetString());
        Assert.Equal(2, history[11].GetProperty("bookingCount").GetInt64());
        Assert.Equal(5, history[11].GetProperty("passengerCount").GetInt64());
        Assert.All(history.EnumerateArray().Take(11), p => Assert.Equal(0, p.GetProperty("value").GetInt64()));
        Assert.Equal(3000, data.GetProperty("totalRevenue").GetInt64());
        Assert.Equal(2, data.GetProperty("totalBookings").GetInt64());
        Assert.Equal(1500, data.GetProperty("averageBooking").GetInt64());
    }

    [Fact]
    public async Task Dashboard_keeps_losses_and_excludes_voided_costs()
    {
        using var client = await Fixture();
        var data = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(3000, data.GetProperty("revenue").GetInt64());
        Assert.Equal(4000, data.GetProperty("costs").GetInt64());
        Assert.Equal(-1000, data.GetProperty("estimatedProfit").GetInt64());
        Assert.Equal(2, data.GetProperty("bookings").GetInt64());
        Assert.Equal(1, data.GetProperty("expenses").GetInt64());
        Assert.Equal(3000, data.GetProperty("pendingPayments").GetProperty("amount").GetInt64());
        Assert.Equal("lifetime", data.GetProperty("totalsScope").GetString());
        Assert.True(data.GetProperty("excludesVoided").GetBoolean());
    }

    [Fact]
    public async Task Reports_reconcile_with_database_and_active_export_rows()
    {
        using var client = await Fixture();
        var data = await client.GetFromJsonAsync<JsonElement>("/api/reports");
        Assert.Equal(3000, data.GetProperty("revenue").GetInt64());
        Assert.Equal(4000, data.GetProperty("costs").GetInt64());
        Assert.Equal(-1000, data.GetProperty("profit").GetInt64());
        Assert.Equal(2, data.GetProperty("counts").GetProperty("bookings").GetInt64());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3000, db.Bookings.Where(b => b.VoidedAt == null).Sum(b => (long)b.GrossRevenue));
        Assert.Equal(4000, db.Expenses.Where(e => e.VoidedAt == null).Sum(e => (long)e.Amount));

        using var csv = new TextFieldParser(new StringReader((await client.GetStringAsync("/api/export/bookings.csv")).TrimStart('\uFEFF')));
        csv.SetDelimiters(",");
        var headings = csv.ReadFields()!.ToList();
        var rows = new List<string[]>();
        while (!csv.EndOfData) rows.Add(csv.ReadFields()!);
        var active = rows.Where(r => r[headings.IndexOf("voided")] != "yes").ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(2, active.Count);
        Assert.Equal(3000, active.Sum(r => long.Parse(r[headings.IndexOf("revenue")], CultureInfo.InvariantCulture)));
        Assert.Equal("lifetime", data.GetProperty("totalsScope").GetString());
        Assert.True(data.GetProperty("excludesVoided").GetBoolean());
    }

    [Fact]
    public async Task Chart_window_crosses_years_without_truncating_lifetime_totals()
    {
        using var client = await Fixture(empty: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Bookings.AddRange(
                Booking("OLD", "2024-12-31", 100),
                Booking("FIRST", "2025-01-01", 200),
                Booking("LAST", "2025-12-31", 300),
                Booking("NEXT", "2026-01-01", 400),
                Booking("VOID", "2025-12-31", 9000, voided: true));
            db.SaveChanges();
        }

        var data = await client.GetFromJsonAsync<JsonElement>("/api/analytics?throughMonth=2025-12-15");
        var history = data.GetProperty("salesHistory");
        Assert.Equal(12, history.GetArrayLength());
        Assert.Equal(200, history[0].GetProperty("value").GetInt64());
        Assert.Equal("2025-01", history[0].GetProperty("month").GetString());
        Assert.Equal(300, history[11].GetProperty("value").GetInt64());
        Assert.Equal("Dec 2025", history[11].GetProperty("label").GetString());
        Assert.All(history.EnumerateArray().Skip(1).Take(10), p => Assert.Equal(0, p.GetProperty("value").GetInt64()));
        Assert.Equal(1000, data.GetProperty("totalRevenue").GetInt64());
        Assert.Equal(4, data.GetProperty("totalBookings").GetInt64());
        Assert.Equal("lifetime", data.GetProperty("totalsScope").GetString());
        Assert.True(data.GetProperty("excludesVoided").GetBoolean());
        var window = data.GetProperty("chartWindow");
        Assert.Equal("2025-01", window.GetProperty("fromMonth").GetString());
        Assert.Equal("2025-12", window.GetProperty("throughMonth").GetString());
        Assert.Equal(500, window.GetProperty("revenuePhp").GetInt64());
        Assert.Equal(2, window.GetProperty("bookingCount").GetInt64());
        Assert.Equal(2, window.GetProperty("passengerCount").GetInt64());
        foreach (var path in new[] { "/api/reports", "/api/dashboard" })
        {
            var summary = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal(1000, summary.GetProperty("revenue").GetInt64());
        }
    }

    [Theory]
    [InlineData("analytics")]
    [InlineData("reports")]
    [InlineData("dashboard")]
    public async Task Lifetime_totals_and_breakdowns_exceed_int_without_overflow(string endpoint)
    {
        using var client = await Fixture(empty: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Bookings.AddRange(Booking("BIG-1", "2026-06-01", int.MaxValue), Booking("BIG-2", "2026-07-01", int.MaxValue));
            db.Expenses.AddRange(Expense("BIG-COST-1", int.MaxValue), Expense("BIG-COST-2", int.MaxValue));
            db.SaveChanges();
        }

        var data = await client.GetFromJsonAsync<JsonElement>($"/api/{endpoint}?throughMonth=2026-07-01");
        Assert.Equal(4294967294L, data.GetProperty(endpoint == "analytics" ? "totalRevenue" : "revenue").GetInt64());
        if (endpoint == "analytics")
        {
            Assert.Equal(4294967294L, data.GetProperty("revenueByDestination")[0].GetProperty("value").GetInt64());
            Assert.Equal(int.MaxValue, data.GetProperty("averageBooking").GetInt64());
        }
        else
        {
            Assert.Equal(4294967294L, data.GetProperty("costs").GetInt64());
            Assert.Equal(0, data.GetProperty(endpoint == "reports" ? "profit" : "estimatedProfit").GetInt64());
            if (endpoint == "reports")
            {
                Assert.Equal(4294967294L, data.GetProperty("revenueByPackage")[0].GetProperty("value").GetInt64());
                Assert.Equal(4294967294L, data.GetProperty("expensesByCategory")[0].GetProperty("value").GetInt64());
            }
            else
                Assert.Equal(4294967294L, data.GetProperty("pendingPayments").GetProperty("amount").GetInt64());
        }
    }

    [Fact]
    public async Task Monthly_revenue_and_passengers_exceed_int_without_overflow()
    {
        using var client = await Fixture(empty: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Bookings.AddRange(
                Booking("BIG-1", "2026-07-01", int.MaxValue, int.MaxValue),
                Booking("BIG-2", "2026-07-31", int.MaxValue, int.MaxValue));
            db.SaveChanges();
        }
        var data = await client.GetFromJsonAsync<JsonElement>("/api/analytics?throughMonth=2026-07-01");
        var july = data.GetProperty("salesHistory")[11];
        Assert.Equal(4294967294L, july.GetProperty("value").GetInt64());
        Assert.Equal(4294967294L, july.GetProperty("passengerCount").GetInt64());
        Assert.Equal(2, july.GetProperty("bookingCount").GetInt64());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_or_only_voided_records_show_zero_actuals(bool onlyVoided)
    {
        using var client = await Fixture(empty: true);
        if (onlyVoided)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Bookings.Add(Booking("VOID", "2026-07-01", 9000, voided: true));
            db.Expenses.Add(Expense("VOID", 8000, true));
            db.SaveChanges();
        }
        var analytics = await client.GetFromJsonAsync<JsonElement>("/api/analytics?throughMonth=2026-07-01");
        Assert.All(analytics.GetProperty("salesHistory").EnumerateArray(), p => Assert.Equal(0, p.GetProperty("value").GetInt64()));
        Assert.Equal(0, analytics.GetProperty("totalRevenue").GetInt64());
        Assert.Equal(0, analytics.GetProperty("totalBookings").GetInt64());
        Assert.Equal(0, analytics.GetProperty("averageBooking").GetInt64());
        Assert.Equal(0, analytics.GetProperty("chartWindow").GetProperty("revenuePhp").GetInt64());
        Assert.Empty(analytics.GetProperty("packageMix").EnumerateArray());
        Assert.Empty(analytics.GetProperty("paymentBreakdown").EnumerateArray());
        Assert.Empty(analytics.GetProperty("revenueByDestination").EnumerateArray());
        foreach (var path in new[] { "/api/reports", "/api/dashboard" })
        {
            var summary = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal(0, summary.GetProperty("revenue").GetInt64());
            Assert.Equal(0, summary.GetProperty("costs").GetInt64());
            Assert.Equal(0, summary.GetProperty(path == "/api/reports" ? "profit" : "estimatedProfit").GetInt64());
        }
    }
}
