using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class DashboardThisMonthTests
{
    [Fact]
    public async Task This_month_compares_with_the_same_days_last_month_and_skips_voided_and_later_records()
    {
        // Midday 13 October in Manila.
        using var factory = new ClockFactory(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero));
        using var client = await Fixture(factory, db =>
        {
            db.Bookings.AddRange(
                Booking("OCT-1", new DateOnly(2026, 10, 1), 1000),
                Booking("OCT-13", new DateOnly(2026, 10, 13), 2000),
                Booking("OCT-14", new DateOnly(2026, 10, 14), 5000),
                Booking("OCT-VOID", new DateOnly(2026, 10, 5), 9000, voided: true),
                Booking("SEP-1", new DateOnly(2026, 9, 1), 400),
                Booking("SEP-13", new DateOnly(2026, 9, 13), 600),
                Booking("SEP-14", new DateOnly(2026, 9, 14), 7000));
            db.Expenses.AddRange(
                Expense("OCT-COST", new DateOnly(2026, 10, 2), 300),
                Expense("OCT-COST-VOID", new DateOnly(2026, 10, 3), 800, voided: true),
                Expense("SEP-COST", new DateOnly(2026, 9, 10), 100),
                Expense("SEP-COST-LATE", new DateOnly(2026, 9, 20), 900));
        });

        var data = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        var current = data.GetProperty("thisMonth").GetProperty("current");
        var previous = data.GetProperty("thisMonth").GetProperty("previous");

        Assert.Equal("2026-10-01", current.GetProperty("from").GetString());
        Assert.Equal("2026-10-13", current.GetProperty("to").GetString());
        Assert.Equal(3000, current.GetProperty("revenue").GetInt64());
        Assert.Equal(2, current.GetProperty("bookings").GetInt32());
        Assert.Equal(300, current.GetProperty("expenses").GetInt64());

        Assert.Equal("2026-09-01", previous.GetProperty("from").GetString());
        Assert.Equal("2026-09-13", previous.GetProperty("to").GetString());
        Assert.Equal(1000, previous.GetProperty("revenue").GetInt64());
        Assert.Equal(2, previous.GetProperty("bookings").GetInt32());
        Assert.Equal(100, previous.GetProperty("expenses").GetInt64());
    }

    [Fact]
    public async Task The_previous_range_stops_at_the_end_of_a_shorter_month()
    {
        // Midday 31 March in Manila; February has 28 days in 2026.
        using var factory = new ClockFactory(new DateTimeOffset(2026, 3, 31, 4, 0, 0, TimeSpan.Zero));
        using var client = await Fixture(factory, db =>
        {
            db.Bookings.AddRange(
                Booking("FEB-28", new DateOnly(2026, 2, 28), 250),
                Booking("MAR-31", new DateOnly(2026, 3, 31), 750));
        });

        var data = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        var current = data.GetProperty("thisMonth").GetProperty("current");
        var previous = data.GetProperty("thisMonth").GetProperty("previous");

        Assert.Equal("2026-03-31", current.GetProperty("to").GetString());
        Assert.Equal(750, current.GetProperty("revenue").GetInt64());
        Assert.Equal("2026-02-01", previous.GetProperty("from").GetString());
        Assert.Equal("2026-02-28", previous.GetProperty("to").GetString());
        Assert.Equal(250, previous.GetProperty("revenue").GetInt64());
    }

    private static async Task<HttpClient> Fixture(ClockFactory factory, Action<AppDbContext> seed)
    {
        var client = await AuthenticatedClient.Login(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Bookings.RemoveRange(db.Bookings);
        db.Expenses.RemoveRange(db.Expenses);
        db.SaveChanges();
        seed(db);
        db.SaveChanges();
        return client;
    }

    private static Booking Booking(string code, DateOnly date, int revenue, bool voided = false) => new()
    {
        Code = code,
        BookingDate = date,
        GrossRevenue = revenue,
        PassengerCount = 1,
        Client = "Agency client",
        PackageName = "Month package",
        Destination = "Cebu",
        BookingStatus = "Confirmed",
        PaymentStatus = "Paid",
        VoidedAt = voided ? new DateTime(2026, 10, 6) : null,
    };

    private static Expense Expense(string code, DateOnly date, int amount, bool voided = false) => new()
    {
        Code = code,
        ExpenseDate = date,
        Amount = amount,
        Category = "Transport",
        VoidedAt = voided ? new DateTime(2026, 10, 6) : null,
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
