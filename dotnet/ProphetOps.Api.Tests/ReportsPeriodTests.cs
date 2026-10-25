using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

/// Reports for a period: totals, the matching earlier period, month-by-month rows and the export.
public class ReportsPeriodTests
{
    // Midday 13 October 2026 in Manila.
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 4, 0, 0, TimeSpan.Zero);

    private static void Records(AppDbContext db)
    {
        db.Bookings.AddRange(
            Booking("Y25-JAN", new DateOnly(2025, 1, 5), 1000, 2),
            Booking("Y25-MAR", new DateOnly(2025, 3, 10), 2000, 3),
            Booking("Y25-OCT-A", new DateOnly(2025, 10, 10), 500, 1),
            Booking("Y25-OCT-B", new DateOnly(2025, 10, 20), 700, 1),
            Booking("Y25-DEC", new DateOnly(2025, 12, 1), 900, 1),
            Booking("Y26-JAN", new DateOnly(2026, 1, 7), 1500, 2),
            Booking("Y26-APR", new DateOnly(2026, 4, 2), 2500, 4),
            Booking("Y26-OCT-A", new DateOnly(2026, 10, 5), 800, 1),
            Booking("Y26-OCT-B", new DateOnly(2026, 10, 12), 300, 1),
            Booking("Y26-FUTURE", new DateOnly(2026, 10, 20), 5000, 6),
            Booking("Y26-VOID", new DateOnly(2026, 2, 14), 9999, 9, voided: true));
        db.Expenses.AddRange(
            Expense("C25-FEB", new DateOnly(2025, 2, 1), 400),
            Expense("C26-FEB", new DateOnly(2026, 2, 1), 600),
            Expense("C26-OCT", new DateOnly(2026, 10, 1), 100),
            Expense("C26-VOID", new DateOnly(2026, 3, 1), 9999, voided: true));
    }

    [Fact]
    public async Task This_year_runs_to_today_and_compares_with_the_same_days_last_year()
    {
        using var factory = new ClockFactory(Now);
        using var client = await Fixture(factory);

        var data = await client.GetFromJsonAsync<JsonElement>("/api/reports?from=2026-01-01&to=2026-12-31");

        Assert.Equal("period", data.GetProperty("totalsScope").GetString());
        Assert.Equal("2026-10-13", data.GetProperty("period").GetProperty("through").GetString());
        Assert.Equal(5100, data.GetProperty("revenue").GetInt64());
        Assert.Equal(700, data.GetProperty("costs").GetInt64());
        Assert.Equal(4400, data.GetProperty("profit").GetInt64());
        Assert.Equal(4, data.GetProperty("counts").GetProperty("bookings").GetInt32());
        Assert.Equal(8, data.GetProperty("counts").GetProperty("passengers").GetInt64());

        var previous = data.GetProperty("previous");
        Assert.Equal("2025-01-01", previous.GetProperty("from").GetString());
        Assert.Equal("2025-10-13", previous.GetProperty("to").GetString());
        Assert.Equal(3500, previous.GetProperty("revenue").GetInt64());
        Assert.Equal(400, previous.GetProperty("costs").GetInt64());
        Assert.Equal(3, previous.GetProperty("bookings").GetInt32());
        Assert.Equal(6, previous.GetProperty("passengers").GetInt64());
    }

    [Fact]
    public async Task A_whole_past_month_compares_with_the_whole_month_before()
    {
        using var factory = new ClockFactory(Now);
        using var client = await Fixture(factory);

        var data = await client.GetFromJsonAsync<JsonElement>("/api/reports?from=2026-04-01&to=2026-04-30");

        Assert.Equal(2500, data.GetProperty("revenue").GetInt64());
        var previous = data.GetProperty("previous");
        Assert.Equal("2026-03-01", previous.GetProperty("from").GetString());
        Assert.Equal("2026-03-31", previous.GetProperty("to").GetString());
        Assert.Equal(0, previous.GetProperty("revenue").GetInt64());
        Assert.Equal(0, previous.GetProperty("costs").GetInt64());
    }

    [Fact]
    public async Task Monthly_rows_cover_the_period_up_to_today_and_keep_empty_months()
    {
        using var factory = new ClockFactory(Now);
        using var client = await Fixture(factory);

        var data = await client.GetFromJsonAsync<JsonElement>("/api/reports?from=2026-01-01&to=2026-12-31");
        var months = data.GetProperty("monthly").EnumerateArray().ToList();

        Assert.Equal(10, months.Count);
        Assert.Equal("2026-01", months[0].GetProperty("month").GetString());
        Assert.Equal("2026-10", months[^1].GetProperty("month").GetString());
        Assert.Equal(1500, months[0].GetProperty("revenue").GetInt64());
        Assert.Equal(600, months[1].GetProperty("costs").GetInt64());
        Assert.Equal(0, months[1].GetProperty("revenue").GetInt64());
        Assert.Equal(0, months[7].GetProperty("bookings").GetInt32());
        Assert.Equal(1100, months[9].GetProperty("revenue").GetInt64());
        Assert.Equal(1000, months[9].GetProperty("afterCosts").GetInt64());
    }

    [Fact]
    public async Task Without_a_period_the_totals_are_lifetime_and_have_nothing_to_compare()
    {
        using var factory = new ClockFactory(Now);
        using var client = await Fixture(factory);

        var data = await client.GetFromJsonAsync<JsonElement>("/api/reports");

        Assert.Equal("lifetime", data.GetProperty("totalsScope").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("previous").ValueKind);
        Assert.Equal(15200, data.GetProperty("revenue").GetInt64());
        Assert.Equal(1100, data.GetProperty("costs").GetInt64());
    }

    [Theory]
    [InlineData("from=2026-13-01")]
    [InlineData("to=yesterday")]
    [InlineData("from=2026-05-01&to=2026-04-01")]
    public async Task An_unreadable_period_is_refused(string query)
    {
        using var factory = new ClockFactory(Now);
        using var client = await Fixture(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/export/report.csv?" + query)).StatusCode);
    }

    [Fact]
    public async Task The_report_export_lists_each_month_and_a_total()
    {
        using var factory = new ClockFactory(Now);
        using var client = await Fixture(factory);

        var response = await client.GetAsync("/api/export/report.csv?from=2026-01-01&to=2026-12-31");
        response.EnsureSuccessStatusCode();
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart((char)0xFEFF);
        var lines = text.Split("\r\n").Where(l => l.Length > 0).ToList();

        Assert.Equal("month,bookings,passengers,revenue,costs,revenue_after_costs", lines[0]);
        Assert.Equal(12, lines.Count);
        Assert.Equal("2026-02,0,0,0,600,-600", lines[2]);
        Assert.Equal("total,4,8,5100,700,4400", lines[^1]);

        var disposition = response.Content.Headers.GetValues("Content-Disposition").Single();
        Assert.Contains("prophetops-report-2026-01-01-to-2026-10-13.csv", disposition);
    }

    [Fact]
    public async Task Staff_may_not_export_the_report()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "staff@prophetops.local", password = "staff123" });
        login.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/export/report.csv")).StatusCode);
    }

    private static async Task<HttpClient> Fixture(ClockFactory factory)
    {
        var client = await AuthenticatedClient.Login(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Bookings.RemoveRange(db.Bookings);
        db.Expenses.RemoveRange(db.Expenses);
        db.SaveChanges();
        Records(db);
        db.SaveChanges();
        return client;
    }

    private static Booking Booking(string code, DateOnly date, int revenue, int passengers, bool voided = false) => new()
    {
        Code = code,
        BookingDate = date,
        GrossRevenue = revenue,
        PassengerCount = passengers,
        Client = "Report client",
        PackageName = "Report package",
        Destination = "Cebu",
        BookingStatus = "Confirmed",
        PaymentStatus = "Paid",
        VoidedAt = voided ? new DateTime(2026, 2, 15) : null,
    };

    private static Expense Expense(string code, DateOnly date, int amount, bool voided = false) => new()
    {
        Code = code,
        ExpenseDate = date,
        Amount = amount,
        Category = "Transport",
        VoidedAt = voided ? new DateTime(2026, 3, 2) : null,
    };

    private sealed class ClockFactory(DateTimeOffset utcNow) : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IBusinessClock)).ToList())
                    services.Remove(descriptor);
                services.AddSingleton<IBusinessClock>(new FixedClock(utcNow));
            });
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IBusinessClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public DateOnly Today =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);

        public string TimeZoneId => "Asia/Manila";
    }
}
