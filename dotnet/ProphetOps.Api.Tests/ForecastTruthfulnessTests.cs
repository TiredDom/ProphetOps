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

public class ForecastTruthfulnessTests
{
    [Fact]
    public async Task Production_without_demo_reports_forecast_unavailable_when_history_is_empty()
    {
        using var factory = new ForecastFactory("Production", demoEnabled: false);
        using var client = await AuthenticatedClient.Login(factory);

        var forecast = await Body(await client.GetAsync("/api/forecast"));
        var dashboard = await Body(await client.GetAsync("/api/dashboard"));

        Assert.False(forecast.GetProperty("ok").GetBoolean());
        Assert.Empty(forecast.GetProperty("steps").EnumerateArray());
        Assert.Equal("insufficient-history", forecast.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.False(forecast.GetProperty("dataSource").GetProperty("usingSample").GetBoolean());
        Assert.Equal(0, forecast.GetProperty("dataSource").GetProperty("recordedMonths").GetInt32());

        var dashboardForecast = dashboard.GetProperty("forecast");
        Assert.False(dashboardForecast.GetProperty("ok").GetBoolean());
        Assert.Equal("insufficient-history", dashboardForecast.GetProperty("status").GetString());
        Assert.Equal(0, dashboardForecast.GetProperty("nextValue").GetDouble());
        Assert.Equal(0, dashboardForecast.GetProperty("accuracy").GetInt32());
        Assert.Equal(0, dashboardForecast.GetProperty("dataSource").GetProperty("recordedMonths").GetInt32());
    }

    [Fact]
    public async Task Production_without_demo_reports_short_live_history_without_sample_projection()
    {
        using var factory = new ForecastFactory("Production", demoEnabled: false, db =>
            FillMonths(db, new DateOnly(2025, 1, 1), 6));
        using var client = await AuthenticatedClient.Login(factory);

        var forecast = await Body(await client.GetAsync("/api/forecast"));
        var dashboard = await Body(await client.GetAsync("/api/dashboard"));

        Assert.False(forecast.GetProperty("ok").GetBoolean());
        Assert.Empty(forecast.GetProperty("steps").EnumerateArray());
        Assert.Equal("insufficient-history", forecast.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.False(forecast.GetProperty("dataSource").GetProperty("usingSample").GetBoolean());
        Assert.Equal(6, forecast.GetProperty("dataSource").GetProperty("recordedMonths").GetInt32());

        var dashboardForecast = dashboard.GetProperty("forecast");
        Assert.False(dashboardForecast.GetProperty("ok").GetBoolean());
        Assert.Equal(0, dashboardForecast.GetProperty("nextValue").GetDouble());
        Assert.Equal(6, dashboardForecast.GetProperty("dataSource").GetProperty("recordedMonths").GetInt32());
    }

    [Fact]
    public async Task Production_without_demo_forecasts_from_sufficient_live_history()
    {
        using var factory = new ForecastFactory("Production", demoEnabled: false, db =>
            FillMonths(db, new DateOnly(2024, 1, 1), 24));
        using var client = await AuthenticatedClient.Login(factory);

        var forecast = await Body(await client.GetAsync("/api/forecast"));

        Assert.True(forecast.GetProperty("ok").GetBoolean());
        Assert.Equal(6, forecast.GetProperty("steps").GetArrayLength());
        Assert.Equal("live", forecast.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.True(forecast.GetProperty("dataSource").GetProperty("usingLiveRecords").GetBoolean());
        Assert.False(forecast.GetProperty("dataSource").GetProperty("usingSample").GetBoolean());
        Assert.True(forecast.GetProperty("steps")[0].GetProperty("value").GetDouble() > 0);
    }

    [Fact]
    public async Task Explicit_non_production_demo_may_use_sample_history_but_labels_it()
    {
        using var factory = new ForecastFactory("Development", demoEnabled: true);
        using var client = await AuthenticatedClient.Login(factory);

        var forecast = await Body(await client.GetAsync("/api/forecast"));
        var dashboard = await Body(await client.GetAsync("/api/dashboard"));

        Assert.True(forecast.GetProperty("ok").GetBoolean());
        Assert.Equal(6, forecast.GetProperty("steps").GetArrayLength());
        Assert.Equal("sample", forecast.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.True(forecast.GetProperty("dataSource").GetProperty("usingSample").GetBoolean());
        Assert.False(forecast.GetProperty("dataSource").GetProperty("usingLiveRecords").GetBoolean());

        var dashboardForecast = dashboard.GetProperty("forecast");
        Assert.True(dashboardForecast.GetProperty("ok").GetBoolean());
        Assert.Equal("sample", dashboardForecast.GetProperty("status").GetString());
        Assert.True(dashboardForecast.GetProperty("dataSource").GetProperty("usingSample").GetBoolean());
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static void FillMonths(AppDbContext db, DateOnly from, int months)
    {
        for (var i = 0; i < months; i++)
        {
            var month = from.AddMonths(i);
            db.Bookings.Add(new Booking
            {
                Code = $"LIVE-{month:yyyyMM}",
                BookingDate = new DateOnly(month.Year, month.Month, 8),
                PassengerCount = 2,
                Client = "Live client",
                PackageName = "Live package",
                Destination = "Live destination",
                GrossRevenue = 100000 + (i * 1250),
                PaymentStatus = "Paid",
                BookingStatus = "Confirmed",
            });
        }
        db.SaveChanges();
    }

    private sealed class ForecastFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-forecast-truth-" + Guid.NewGuid().ToString("N"));
        private readonly string _environment;
        private readonly bool _demoEnabled;
        private readonly Action<AppDbContext>? _prepare;

        public ForecastFactory(string environment, bool demoEnabled, Action<AppDbContext>? prepare = null)
        {
            _environment = environment;
            _demoEnabled = demoEnabled;
            _prepare = prepare;
            Directory.CreateDirectory(_storageRoot);
            _connection.Open();

            using var db = OpenContext();
            db.Database.Migrate();
            db.Users.Add(new User
            {
                Name = "Agency owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
                Status = "Active",
            });
            _prepare?.Invoke(db);
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

        private AppDbContext OpenContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

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
