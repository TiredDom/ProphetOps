using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BusinessCalendarApiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-calendar-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("2026-08-31T15:59:59Z", "2026-08", 100)]
    [InlineData("2026-08-31T16:00:00Z", "2026-09", 200)]
    public async Task Analytics_default_cutoff_uses_business_today(string utcNow, string throughMonth, long lastMonthRevenue)
    {
        using var factory = new CalendarFactory(_directory, DateTimeOffset.Parse(utcNow));
        using var client = await AuthenticatedClient.Login(factory);

        var data = await client.GetFromJsonAsync<JsonElement>("/api/analytics");

        Assert.Equal(throughMonth, data.GetProperty("chartWindow").GetProperty("throughMonth").GetString());
        Assert.Equal(lastMonthRevenue, data.GetProperty("salesHistory")[11].GetProperty("value").GetInt64());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class CalendarFactory(string directory, DateTimeOffset utcNow) : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(directory);
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "false",
                ["Storage:Root"] = Path.Combine(directory, "storage"),
                ["Business:TimeZone"] = "Asia/Manila",
            }));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType == typeof(IBusinessClock)).ToList())
                    services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
                services.AddSingleton<IBusinessClock>(new FixedClock(utcNow));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            if (db.Users.Any()) return;
            db.Users.Add(new User
            {
                Name = "Owner",
                Email = "owner@prophetops.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
                Role = Roles.OwnerManagement,
            });
            db.Bookings.AddRange(
                Booking("AUG", new DateOnly(2026, 8, 15), 100),
                Booking("SEP", new DateOnly(2026, 9, 1), 200));
            db.SaveChanges();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }

        private static Booking Booking(string code, DateOnly date, int revenue) => new()
        {
            Code = code,
            BookingDate = date,
            GrossRevenue = revenue,
            PassengerCount = 1,
            Client = "Agency client",
            PackageName = "Calendar package",
            Destination = "Cebu",
            BookingStatus = "Confirmed",
            PaymentStatus = "Pending",
        };
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IBusinessClock
    {
        public DateTimeOffset UtcNow => utcNow;
        public DateOnly Today
        {
            get
            {
                var local = TimeZoneInfo.ConvertTime(UtcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
                return DateOnly.FromDateTime(local.DateTime);
            }
        }

        public string TimeZoneId => "Asia/Manila";
    }
}
