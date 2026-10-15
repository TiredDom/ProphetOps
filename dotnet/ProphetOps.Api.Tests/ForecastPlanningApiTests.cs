using System.Net;
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

/// The cost scenario and the package demand outlook, worked through on figures simple enough to
/// check by hand: the same bookings every month, so the forecast is flat and every share is exact.
public class ForecastPlanningApiTests
{
    private const string StaffEmail = "staff.planning@prophetops.local";
    private const string StaffPassword = "staff-planning-1";

    /// February 2024 to July 2026, every month: package A twice with 3 passengers (₱60,000 each),
    /// package B once with 1 passenger (₱10,000), a tailored trip with 2 passengers (₱30,000), and
    /// spending of 40% of the month's ₱160,000, with ₱36,000 more each December. Package C has
    /// never been booked.
    private static Action<AppDbContext> Steady(int months, int openOnA = 3) => db =>
    {
        var a = Package(db, "PKG-A", "Package A", 20_000, open: openOnA);
        var b = Package(db, "PKG-B", "Package B", 10_000, open: 60);
        Package(db, "PKG-C", "Package C", 15_000, open: 5);
        db.SaveChanges();

        var start = new DateOnly(2024, 2, 1);
        for (var i = 0; i < months; i++)
        {
            var month = start.AddMonths(i);
            Booking(db, $"A1-{month:yyyyMM}", month, a, 3, 60_000);
            Booking(db, $"A2-{month:yyyyMM}", month, a, 3, 60_000);
            Booking(db, $"B-{month:yyyyMM}", month, b, 1, 10_000);
            Booking(db, $"T-{month:yyyyMM}", month, null, 2, 30_000);
            db.Expenses.Add(new Expense
            {
                Code = $"EXP-{month:yyyyMM}",
                ExpenseDate = new DateOnly(month.Year, month.Month, 20),
                Category = "Tour operations",
                Amount = month.Month == 12 ? 100_000 : 64_000,
                RelatedPackage = "Monthly tour operations",
                PaymentStatus = "Paid",
            });
        }
    };

    private static TravelPackage Package(AppDbContext db, string code, string name, int price, int open)
    {
        var package = new TravelPackage { Code = code, PackageName = name, Destination = "Planning destination", BasePrice = price, AvailableSlots = open, Status = "Normal" };
        db.TravelPackages.Add(package);
        return package;
    }

    private static void Booking(AppDbContext db, string code, DateOnly month, TravelPackage? package, int passengers, int revenue) =>
        db.Bookings.Add(new Booking
        {
            Code = code,
            BookingDate = new DateOnly(month.Year, month.Month, 8),
            PassengerCount = passengers,
            Client = "Planning client",
            TravelPackage = package,
            PackageName = package?.PackageName ?? "Tailored trip",
            PackageCode = package?.Code,
            EntryType = package is null ? "Custom quotation" : "Package preset",
            Destination = "Planning destination",
            GrossRevenue = revenue,
            PaymentStatus = "Paid",
            BookingStatus = "Confirmed",
        });

    [Fact]
    public async Task By_default_spending_keeps_the_share_of_revenue_it_took_over_the_last_twelve_months()
    {
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/scenario"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("share", body.GetProperty("basis").GetString());
        // August 2025 to July 2026: ₱1,920,000 booked, ₱804,000 spent (one December at ₱100,000).
        var period = body.GetProperty("basisPeriod");
        Assert.Equal("August 2025", period.GetProperty("from").GetString());
        Assert.Equal("July 2026", period.GetProperty("to").GetString());
        Assert.Equal(1_920_000, period.GetProperty("revenue").GetInt64());
        Assert.Equal(804_000, period.GetProperty("costs").GetInt64());
        Assert.Equal(0.4188, body.GetProperty("recordedShare").GetDouble());
        Assert.Equal(0.4188, body.GetProperty("share").GetDouble());

        var months = body.GetProperty("months").EnumerateArray().ToList();
        Assert.Equal(6, months.Count);
        Assert.Equal("August 2026", months[0].GetProperty("month").GetString());
        Assert.Equal("January 2027", months[5].GetProperty("month").GetString());
        Assert.Empty(body.GetProperty("passedMonths").EnumerateArray());
        foreach (var month in months)
        {
            var revenue = month.GetProperty("revenue").GetDouble();
            Assert.InRange(revenue, 159_000, 161_000);
            Assert.InRange(month.GetProperty("costs").GetDouble(), revenue * 0.41875 - 1, revenue * 0.41875 + 1);
            Assert.InRange(month.GetProperty("afterCosts").GetDouble(), revenue * 0.58125 - 1, revenue * 0.58125 + 1);
        }
        var total = body.GetProperty("total");
        Assert.Equal(months.Sum(m => m.GetProperty("afterCosts").GetDouble()), total.GetProperty("afterCosts").GetDouble());
    }

    [Fact]
    public async Task A_share_given_by_the_owner_replaces_the_recorded_one()
    {
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/scenario?share=0.5"));

        Assert.Equal(0.5, body.GetProperty("share").GetDouble());
        Assert.Equal(0.4188, body.GetProperty("recordedShare").GetDouble());
        var first = body.GetProperty("months")[0];
        Assert.InRange(first.GetProperty("costs").GetDouble() - first.GetProperty("revenue").GetDouble() * 0.5, -1, 1);
    }

    [Fact]
    public async Task Repeating_last_year_spends_what_the_same_month_cost_a_year_earlier()
    {
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/scenario?basis=last-year"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("last-year", body.GetProperty("basis").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("share").ValueKind);
        var months = body.GetProperty("months").EnumerateArray().ToDictionary(m => m.GetProperty("month").GetString()!);
        Assert.Equal(64_000, months["August 2026"].GetProperty("costs").GetDouble());
        Assert.Equal(100_000, months["December 2026"].GetProperty("costs").GetDouble());
        var december = months["December 2026"];
        Assert.Equal(december.GetProperty("revenue").GetDouble() - 100_000, december.GetProperty("afterCosts").GetDouble());
        Assert.Equal(december.GetProperty("revenueLower").GetDouble() - 100_000, december.GetProperty("afterCostsLower").GetDouble());
    }

    [Fact]
    public async Task Each_package_takes_its_share_of_the_expected_passengers_against_its_open_slots()
    {
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/packages"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal(3, body.GetProperty("monthsAhead").GetInt32());
        Assert.Equal(new[] { "August 2026", "September 2026", "October 2026" },
            body.GetProperty("months").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(108, body.GetProperty("basisPeriod").GetProperty("passengers").GetInt64());
        Assert.InRange(body.GetProperty("agency").GetProperty("value").GetDouble(), 26.5, 27.5);

        var packages = body.GetProperty("packages").EnumerateArray().ToList();
        Assert.Equal(new[] { "PKG-A", "PKG-B", "PKG-C" }, packages.Select(p => p.GetProperty("code").GetString()));

        // Package A carries two thirds of the passengers: about 18 expected, 3 slots open.
        var a = packages[0];
        Assert.Equal("likely-short", a.GetProperty("status").GetString());
        Assert.Equal(0.6667, a.GetProperty("share").GetDouble());
        Assert.InRange(a.GetProperty("expected").GetProperty("value").GetDouble(), 17.5, 18.5);
        var raise = a.GetProperty("price");
        Assert.Equal("raise", raise.GetProperty("kind").GetString());
        Assert.Equal(20_000, raise.GetProperty("currentPrice").GetInt32());
        Assert.Equal(21_000, raise.GetProperty("suggestedPrice").GetInt32());
        Assert.Equal(5, raise.GetProperty("changePercent").GetDouble());
        Assert.Contains("3 open slots even at the low end", raise.GetProperty("reason").GetString());

        // Package B: about 3 expected against 60 open slots, so a limited offer.
        var b = packages[1];
        Assert.Equal("enough", b.GetProperty("status").GetString());
        var offer = b.GetProperty("price");
        Assert.Equal("offer", offer.GetProperty("kind").GetString());
        Assert.Equal(9_500, offer.GetProperty("suggestedPrice").GetInt32());
        Assert.Equal(-5, offer.GetProperty("changePercent").GetDouble());

        // Package C has no bookings to go on and gets no price.
        var c = packages[2];
        Assert.Equal("no-history", c.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, c.GetProperty("price").ValueKind);
    }

    [Fact]
    public async Task A_full_package_is_flagged_but_has_nothing_left_to_price()
    {
        using var factory = new PlanningFactory(Steady(30, openOnA: 0), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var body = await Body(await client.GetAsync("/api/forecast/packages"));

        var a = body.GetProperty("packages").EnumerateArray().Single(p => p.GetProperty("code").GetString() == "PKG-A");
        Assert.Equal("likely-short", a.GetProperty("status").GetString());
        Assert.Equal(0, a.GetProperty("openSlots").GetInt32());
        Assert.Equal(JsonValueKind.Null, a.GetProperty("price").ValueKind);
    }

    [Fact]
    public async Task Months_that_have_already_passed_are_not_planned_for()
    {
        // The records end in July 2026; by March 2027 every forecast month is behind us.
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2027, 3, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var scenario = await Body(await client.GetAsync("/api/forecast/scenario"));
        var outlook = await Body(await client.GetAsync("/api/forecast/packages"));

        Assert.False(scenario.GetProperty("ok").GetBoolean());
        Assert.Contains("already passed", scenario.GetProperty("reason").GetString());
        Assert.Equal(6, scenario.GetProperty("passedMonths").GetArrayLength());
        Assert.False(outlook.GetProperty("ok").GetBoolean());
        Assert.Contains("already passed", outlook.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Too_little_history_says_so()
    {
        using var factory = new PlanningFactory(Steady(20), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var scenario = await Body(await client.GetAsync("/api/forecast/scenario"));
        var outlook = await Body(await client.GetAsync("/api/forecast/packages"));

        Assert.False(scenario.GetProperty("ok").GetBoolean());
        Assert.Contains("two full years", scenario.GetProperty("reason").GetString());
        Assert.False(outlook.GetProperty("ok").GetBoolean());
        Assert.Empty(outlook.GetProperty("packages").EnumerateArray());
    }

    [Theory]
    [InlineData("/api/forecast/scenario?basis=guess", "basis")]
    [InlineData("/api/forecast/scenario?share=3", "share")]
    [InlineData("/api/forecast/scenario?share=lots", "share")]
    [InlineData("/api/forecast/packages?months=0", "months")]
    [InlineData("/api/forecast/packages?months=7", "months")]
    [InlineData("/api/forecast/packages?months=three", "months")]
    public async Task Unreadable_choices_are_refused_with_the_field_named(string url, string field)
    {
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2026, 8, 15));
        using var client = await AuthenticatedClient.Login(factory);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.TryGetProperty(field, out _), $"No message for {field}");
    }

    [Fact]
    public async Task Staff_and_signed_out_visitors_cannot_plan_with_the_forecast()
    {
        using var factory = new PlanningFactory(Steady(30), new DateOnly(2026, 8, 15));
        using var anonymous = factory.CreateClient();
        using var staff = await AuthenticatedClient.Login(factory, StaffEmail, StaffPassword);

        foreach (var url in new[] { "/api/forecast/scenario", "/api/forecast/packages" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(url)).StatusCode);
        }
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private sealed class PlanningFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "prophetops-forecast-planning-" + Guid.NewGuid().ToString("N"));
        private readonly DateOnly _today;

        public PlanningFactory(Action<AppDbContext> prepare, DateOnly today)
        {
            _today = today;
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
                services.AddSingleton<IBusinessClock>(new FixedClock(_today));
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

    private sealed class FixedClock(DateOnly today) : IBusinessClock
    {
        public DateTimeOffset UtcNow => new(today.Year, today.Month, today.Day, 4, 0, 0, TimeSpan.Zero);
        public DateOnly Today => today;
        public string TimeZoneId => "Asia/Manila";
    }
}
