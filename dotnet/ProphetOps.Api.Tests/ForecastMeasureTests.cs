using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

/// The forecast can be asked for booking revenue, the number of bookings or the number of
/// passengers. The Holt-Winters method is the same; only what each month adds up changes.
public class ForecastMeasureTests
{
    // Two years of bookings, January 2024 to December 2025, two bookings a month with passenger
    // counts that differ from month to month and from the revenue, plus one voided booking.
    private static void TwoYears(AppDbContext db)
    {
        var start = new DateOnly(2024, 1, 1);
        for (var i = 0; i < 24; i++)
        {
            var month = start.AddMonths(i);
            db.Bookings.Add(Booking($"A-{month:yyyyMM}", new DateOnly(month.Year, month.Month, 5), 2 + i % 5, 100_000 + i * 1_500));
            db.Bookings.Add(Booking($"B-{month:yyyyMM}", new DateOnly(month.Year, month.Month, 19), 3 + (i * 7) % 4, 60_000 + i * 900));
        }

        var voided = Booking("VOID-202512", new DateOnly(2025, 12, 10), 40, 900_000);
        voided.VoidedAt = new DateTime(2025, 12, 11, 0, 0, 0, DateTimeKind.Utc);
        voided.VoidReason = "Entered twice";
        db.Bookings.Add(voided);
        db.SaveChanges();
    }

    private static Booking Booking(string code, DateOnly date, int passengers, int revenue) => new()
    {
        Code = code,
        BookingDate = date,
        PassengerCount = passengers,
        Client = "Measure client",
        PackageName = "Measure package",
        Destination = "Measure destination",
        GrossRevenue = revenue,
        PaymentStatus = "Paid",
        BookingStatus = "Confirmed",
    };

    private static double ExpectedPassengers(int i) => (2 + i % 5) + (3 + (i * 7) % 4);

    [Fact]
    public async Task A_passenger_forecast_learns_from_monthly_passenger_totals()
    {
        using var factory = new MeasureFactory("Production", demoEnabled: false, TwoYears);
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast?measure=passengers"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("passengers", body.GetProperty("measure").GetString());
        Assert.Equal("passengers", body.GetProperty("unit").GetString());
        Assert.Equal(6, body.GetProperty("steps").GetArrayLength());
        Assert.Equal("December 2025", body.GetProperty("dataSource").GetProperty("lastRecordedMonth").GetString());

        // The last twelve recorded months are 2025, months 12 to 23 of the series. The voided
        // booking's 40 passengers are not in December.
        var history = body.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("value").GetDouble()).ToList();
        Assert.Equal(Enumerable.Range(12, 12).Select(ExpectedPassengers).ToList(), history);
    }

    [Fact]
    public async Task A_booking_count_forecast_counts_bookings_not_passengers()
    {
        using var factory = new MeasureFactory("Production", demoEnabled: false, TwoYears);
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast?measure=bookings"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("bookings", body.GetProperty("measure").GetString());
        Assert.All(body.GetProperty("history").EnumerateArray(), h => Assert.Equal(2, h.GetProperty("value").GetDouble()));
    }

    [Fact]
    public async Task Count_forecasts_never_dip_below_zero()
    {
        using var factory = new MeasureFactory("Production", demoEnabled: false, TwoYears);
        using var client = await AuthenticatedClient.Login(factory);

        foreach (var measure in new[] { "bookings", "passengers" })
        {
            var body = await Body(await client.GetAsync($"/api/forecast?measure={measure}"));
            Assert.All(body.GetProperty("steps").EnumerateArray(), s =>
            {
                Assert.True(s.GetProperty("lower").GetDouble() >= 0, $"{measure} lower bound below zero");
                Assert.True(s.GetProperty("value").GetDouble() >= s.GetProperty("lower").GetDouble());
            });
        }
    }

    [Fact]
    public async Task Revenue_stays_the_default_and_keeps_its_notes()
    {
        using var factory = new MeasureFactory("Production", demoEnabled: false, TwoYears);
        using var client = await AuthenticatedClient.Login(factory);

        var plain = await Body(await client.GetAsync("/api/forecast"));
        var named = await Body(await client.GetAsync("/api/forecast?measure=revenue"));
        var counts = await Body(await client.GetAsync("/api/forecast?measure=passengers"));

        Assert.Equal("revenue", plain.GetProperty("measure").GetString());
        Assert.Equal("PHP", plain.GetProperty("unit").GetString());
        Assert.Equal(plain.GetProperty("steps").ToString(), named.GetProperty("steps").ToString());
        Assert.NotEmpty(plain.GetProperty("insight").GetProperty("notes").EnumerateArray());

        // The written notes talk about revenue in pesos, so a count forecast carries none.
        Assert.Empty(counts.GetProperty("insight").GetProperty("notes").EnumerateArray());
    }

    [Fact]
    public async Task An_unknown_measure_is_refused()
    {
        using var factory = new MeasureFactory("Production", demoEnabled: false, TwoYears);
        using var client = await AuthenticatedClient.Login(factory);

        var response = await client.GetAsync("/api/forecast?measure=profit");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Counts_never_borrow_the_sample_revenue_history()
    {
        using var factory = new MeasureFactory("Development", demoEnabled: true);
        using var client = await AuthenticatedClient.Login(factory);

        var revenue = await Body(await client.GetAsync("/api/forecast"));
        var passengers = await Body(await client.GetAsync("/api/forecast?measure=passengers"));

        Assert.Equal("sample", revenue.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.False(passengers.GetProperty("ok").GetBoolean());
        Assert.Equal("insufficient-history", passengers.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.False(passengers.GetProperty("dataSource").GetProperty("usingSample").GetBoolean());
        Assert.Empty(passengers.GetProperty("steps").EnumerateArray());
    }

    [Fact]
    public async Task Staff_may_not_read_a_count_forecast_either()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "staff@prophetops.local", password = "staff123" });
        login.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/forecast?measure=passengers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private sealed class MeasureFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-forecast-measure-" + Guid.NewGuid().ToString("N"));
        private readonly string _environment;
        private readonly bool _demoEnabled;

        public MeasureFactory(string environment, bool demoEnabled, Action<AppDbContext>? prepare = null)
        {
            _environment = environment;
            _demoEnabled = demoEnabled;
            Directory.CreateDirectory(_storageRoot);
            _connection.Open();

            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
            db.Database.Migrate();
            db.Users.Add(new User
            {
                Name = "Agency owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
                Status = "Active",
            });
            prepare?.Invoke(db);
            db.SaveChanges();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = _demoEnabled.ToString(),
                    ["Storage:Root"] = _storageRoot,
                    ["Business:TimeZone"] = "Asia/Manila",
                }));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(IBusinessClock)).ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<IBusinessClock>(new FixedClock());
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
                if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
            }
        }
    }

    private sealed class FixedClock : IBusinessClock
    {
        public DateTimeOffset UtcNow => new(2026, 8, 15, 4, 0, 0, TimeSpan.Zero);
        public DateOnly Today => new(2026, 8, 15);
        public string TimeZoneId => "Asia/Manila";
    }
}
