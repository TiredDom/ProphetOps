using System.Net;
using System.Text;
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

/// The accuracy test of the forecast on recorded months it had not seen, as the owner reads it and
/// downloads it.
public class ForecastEvaluationApiTests
{
    private const string StaffEmail = "staff.evaluation@prophetops.local";
    private const string StaffPassword = "staff-evaluation-1";

    /// Thirty months of bookings, February 2024 to July 2026: one booking a month whose revenue
    /// rises and peaks at the same time each year.
    private static Action<AppDbContext> Months(int count) => db =>
    {
        var start = new DateOnly(2024, 2, 1);
        for (var i = 0; i < count; i++)
        {
            var month = start.AddMonths(i);
            db.Bookings.Add(new Booking
            {
                Code = $"EVAL-{month:yyyyMM}",
                BookingDate = new DateOnly(month.Year, month.Month, 10),
                PassengerCount = 2 + i % 3,
                Client = "Evaluation client",
                PackageName = "Evaluation package",
                Destination = "Evaluation destination",
                GrossRevenue = (int)Math.Round(100_000 + 2_000 * i + 30_000 * Math.Sin(2 * Math.PI * i / 12)),
                PaymentStatus = "Paid",
                BookingStatus = "Confirmed",
            });
        }
    };

    [Fact]
    public async Task The_owner_reads_how_each_forecast_did_on_months_it_had_not_seen()
    {
        using var factory = new EvaluationFactory(Months(30));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/evaluation"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("revenue", body.GetProperty("measure").GetString());
        Assert.Equal("PHP", body.GetProperty("unit").GetString());
        Assert.Equal("live", body.GetProperty("dataSource").GetProperty("status").GetString());
        Assert.Equal(30, body.GetProperty("monthsAvailable").GetInt32());
        Assert.Equal(6, body.GetProperty("origins").GetInt32());
        Assert.Equal("February 2026", body.GetProperty("firstTestedMonth").GetString());
        Assert.Equal("July 2026", body.GetProperty("lastTestedMonth").GetString());

        var byHorizon = body.GetProperty("byHorizon").EnumerateArray().ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, byHorizon.Select(h => h.GetProperty("step").GetInt32()));
        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, byHorizon.Select(h => h.GetProperty("forecasts").GetInt32()));

        var overall = body.GetProperty("overall");
        Assert.Equal(21, overall.GetProperty("forecasts").GetInt32());
        Assert.True(overall.GetProperty("mape").GetDouble() >= 0);
        Assert.InRange(overall.GetProperty("coverage").GetDouble(), 0, 1);

        var points = body.GetProperty("points").EnumerateArray().ToList();
        Assert.Equal(21, points.Count);
        var first = points[0];
        Assert.Equal("January 2026", first.GetProperty("madeAfter").GetString());
        Assert.Equal("February 2026", first.GetProperty("month").GetString());
        Assert.Equal(1, first.GetProperty("step").GetInt32());
        // The recorded figure is the February 2026 booking, month 24 of the series.
        Assert.Equal(Math.Round(100_000 + 2_000 * 24 + 30_000 * Math.Sin(2 * Math.PI * 24 / 12)), first.GetProperty("actual").GetDouble());
        // The baseline repeats February 2025, month 12.
        Assert.Equal(Math.Round(100_000 + 2_000 * 12 + 30_000 * Math.Sin(2 * Math.PI * 12 / 12)), first.GetProperty("sameMonthLastYear").GetDouble());
    }

    [Fact]
    public async Task The_count_measures_are_tested_the_same_way()
    {
        using var factory = new EvaluationFactory(Months(30));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/evaluation?measure=passengers"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("passengers", body.GetProperty("unit").GetString());
        var first = body.GetProperty("points")[0];
        Assert.Equal(2 + 24 % 3, first.GetProperty("actual").GetDouble());
    }

    [Fact]
    public async Task An_unknown_measure_is_refused()
    {
        using var factory = new EvaluationFactory(Months(30));
        using var client = await AuthenticatedClient.Login(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/forecast/evaluation?measure=profit")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/export/forecast-test.csv?measure=profit")).StatusCode);
    }

    [Fact]
    public async Task Too_few_months_say_so_instead_of_inventing_a_score()
    {
        using var factory = new EvaluationFactory(Months(20));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/evaluation"));

        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal(25, body.GetProperty("monthsRequired").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("reason").GetString()));
        Assert.Empty(body.GetProperty("points").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("overall").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/export/forecast-test.csv")).StatusCode);
    }

    [Fact]
    public async Task Staff_and_signed_out_visitors_cannot_see_the_test()
    {
        using var factory = new EvaluationFactory(Months(30));
        using var anonymous = factory.CreateClient();
        using var staff = await AuthenticatedClient.Login(factory, StaffEmail, StaffPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/forecast/evaluation")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/export/forecast-test.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/forecast/evaluation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/export/forecast-test.csv")).StatusCode);
    }

    [Fact]
    public async Task The_test_downloads_as_one_row_per_comparison()
    {
        using var factory = new EvaluationFactory(Months(30));
        using var client = await AuthenticatedClient.Login(factory);

        var response = await client.GetAsync("/api/export/forecast-test.csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("prophetops-forecast-test-revenue-", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("made_after,month,months_ahead,recorded,forecast,lower_80,upper_80,same_month_last_year,inside_range", lines[0]);
        Assert.Equal(22, lines.Length);
        Assert.StartsWith("2026-01,2026-02,1,", lines[1]);
        Assert.All(lines.Skip(1), line => Assert.Matches(@",(yes|no)$", line));
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private sealed class EvaluationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-forecast-evaluation-" + Guid.NewGuid().ToString("N"));

        public EvaluationFactory(Action<AppDbContext> prepare)
        {
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
            db.Users.Add(new User
            {
                Name = "Agency staff",
                Email = StaffEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(StaffPassword),
                Role = Roles.Staff,
                Status = "Active",
            });
            prepare(db);
            db.SaveChanges();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "false",
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
