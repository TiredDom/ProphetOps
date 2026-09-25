using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public class BookingConcurrencyTests : IDisposable
{
    private readonly ConcurrentFactory _factory = new();

    [Fact]
    public async Task Two_bookings_competing_for_one_seat_commit_once()
    {
        using var first = await Login();
        using var second = await Login();
        _factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PostAsJsonAsync("/api/bookings", Booking("RACE-1")),
            second.PostAsJsonAsync("/api/bookings", Booking("RACE-2")));

        AssertRace(results);
        _factory.Read(db =>
        {
            Assert.Equal(0, db.TravelPackages.Single().AvailableSlots);
            Assert.Equal(1, db.TravelPackages.Single().SoldCount);
            Assert.Equal(1, db.Bookings.Count());
            Assert.Equal(1, db.AuditEntries.Count(a => a.EntityType == "Booking"));
        });
    }

    [Fact]
    public async Task Duplicate_code_does_not_reserve_twice()
    {
        using var first = await Login();
        using var second = await Login();
        _factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PostAsJsonAsync("/api/bookings", Booking("SAME")),
            second.PostAsJsonAsync("/api/bookings", Booking("SAME")));
        AssertRace(results);
        _factory.Read(db =>
        {
            Assert.Single(db.Bookings);
            Assert.Equal(1, db.TravelPackages.Single().SoldCount);
            Assert.Single(db.AuditEntries.Where(a => a.EntityType == "Booking"));
        });
    }

    [Fact]
    public async Task Edit_and_void_cannot_both_apply_the_same_revision()
    {
        using var first = await Login();
        using var second = await Login();
        (await first.PostAsJsonAsync("/api/bookings", Booking("EDIT-VOID"))).EnsureSuccessStatusCode();
        _factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PutAsJsonAsync("/api/bookings/EDIT-VOID", Booking("EDIT-VOID", client: "Changed")),
            second.PostAsJsonAsync("/api/bookings/EDIT-VOID/void", new { reason = "Cancelled", revision = 1 }));
        AssertRace(results);
        _factory.Read(db =>
        {
            var booking = db.Bookings.Single();
            var package = db.TravelPackages.Single();
            Assert.Equal(booking.IsVoided ? 1 : 0, package.AvailableSlots);
            Assert.Equal(booking.IsVoided ? 0 : 1, package.SoldCount);
            Assert.Equal(2, db.AuditEntries.Count(a => a.EntityType == "Booking"));
        });
    }

    [Fact]
    public async Task Restore_competing_with_new_booking_cannot_overbook()
    {
        using var first = await Login();
        using var second = await Login();
        (await first.PostAsJsonAsync("/api/bookings", Booking("RESTORE"))).EnsureSuccessStatusCode();
        (await first.PostAsJsonAsync("/api/bookings/RESTORE/void", new { reason = "Cancelled", revision = 1 })).EnsureSuccessStatusCode();
        _factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PostAsJsonAsync("/api/bookings/RESTORE/restore", new { revision = 2 }),
            second.PostAsJsonAsync("/api/bookings", Booking("NEW")));
        AssertRace(results);
        _factory.Read(db =>
        {
            Assert.Equal(1, db.Bookings.Count(b => b.VoidedAt == null));
            Assert.Equal(0, db.TravelPackages.Single().AvailableSlots);
            Assert.Equal(1, db.TravelPackages.Single().SoldCount);
            Assert.Equal(3, db.AuditEntries.Count(a => a.EntityType == "Booking"));
        });
    }

    [Fact]
    public async Task Package_edit_cannot_overwrite_a_concurrent_reservation()
    {
        using var first = await Login();
        using var second = await Login();
        _factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PutAsJsonAsync("/api/inventory/ONE", new
            {
                id = "ONE", packageName = "Renamed", destination = "Cebu", basePrice = 10,
                availableSlots = 1, soldCount = 0, reservedCount = 0, status = "Normal", revision = 1,
            }),
            second.PostAsJsonAsync("/api/bookings", Booking("STOCK")));
        Assert.True(results[1].IsSuccessStatusCode);
        Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(2, _factory.Gate.DistinctConnections);
        _factory.Read(db =>
        {
            Assert.Equal(0, db.TravelPackages.Single().AvailableSlots);
            Assert.Equal(1, db.TravelPackages.Single().SoldCount);
        });
    }

    [Fact]
    public async Task Concurrent_owner_suspensions_keep_an_active_owner()
    {
        using var first = await Login();
        using var second = await AuthenticatedClient.Login(_factory, "second@example.test", "owner123");
        _factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PutAsJsonAsync("/api/users/second@example.test", new { name = "Second", email = "second@example.test", role = Roles.OwnerManagement, status = "Suspended" }),
            second.PutAsJsonAsync("/api/users/owner@prophetops.local", new { name = "Owner", email = "owner@prophetops.local", role = Roles.OwnerManagement, status = "Suspended" }));
        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(2, _factory.Gate.DistinctConnections);
        _factory.Read(db => Assert.Equal(1, db.Users.Count(u => u.Role == Roles.OwnerManagement && u.Status == "Active")));
    }

    [Fact]
    public async Task Voided_edit_never_reserves_and_stale_or_missing_revision_is_rejected()
    {
        using var client = await Login();
        var created = await client.PostAsJsonAsync("/api/bookings", Booking("VOIDED"));
        created.EnsureSuccessStatusCode();
        var row = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, row.GetProperty("revision").GetInt32());
        (await client.PostAsJsonAsync("/api/bookings/VOIDED/void", new { reason = "Cancelled", revision = 1 })).EnsureSuccessStatusCode();
        var stale = await client.PutAsJsonAsync("/api/bookings/VOIDED", Booking("VOIDED"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var missing = await client.PutAsJsonAsync("/api/bookings/VOIDED", Booking("VOIDED", revision: null));
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        var edited = await client.PutAsJsonAsync("/api/bookings/VOIDED", Booking("VOIDED", revision: 2, passengers: 3));
        edited.EnsureSuccessStatusCode();
        Assert.Equal(3, (await edited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32());
        _factory.Read(db =>
        {
            Assert.True(db.Bookings.Single().IsVoided);
            Assert.Equal(1, db.TravelPackages.Single().AvailableSlots);
            Assert.Equal(0, db.TravelPackages.Single().SoldCount);
        });
    }

    [Fact]
    public async Task Unknown_package_id_is_rejected_without_saving_a_preset_booking()
    {
        using var client = await Login();

        var response = await client.PostAsJsonAsync("/api/bookings", Booking("UNKNOWN-PKG", packageId: "MISSING"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Choose a package that still exists.", errors!["packageId"]);
        _factory.Read(db =>
        {
            Assert.Empty(db.Bookings);
            Assert.Empty(db.AuditEntries.Where(a => a.EntityType == "Booking"));
        });
    }

    [Fact]
    public async Task Held_sqlite_writer_lock_returns_a_bounded_write_conflict()
    {
        using var client = await Login();
        await using var locked = new SqliteConnection($"Data Source={_factory.DatabasePath};Pooling=False;Default Timeout=5");
        await locked.OpenAsync();
        await using var transaction = locked.BeginTransaction(deferred: false);

        var elapsed = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/bookings", Booking("BUSY"));
        elapsed.Stop();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("write_conflict", conflict.GetProperty("code").GetString());
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), $"Conflict took {elapsed.Elapsed}.");
        await transaction.RollbackAsync();
        _factory.Read(db => Assert.Empty(db.Bookings));
    }

    private Task<HttpClient> Login() => AuthenticatedClient.Login(_factory);

    private void AssertRace(HttpResponseMessage[] results)
    {
        Assert.Equal(2, _factory.Gate.DistinctConnections);
        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    private static object Booking(string code, int? revision = 1, int passengers = 1, string client = "Client",
        string? packageId = "ONE") => new
    {
        id = code, ds = "2026-09-14", y = passengers, client, packageId, entryType = "Package preset",
        package = "One seat", destination = "Cebu", grossRevenue = 10, paymentStatus = "Pending",
        bookingStatus = "Pending", source = "Manual quotation", revision,
    };

    public void Dispose()
    {
        _factory.Dispose();
        _factory.Cleanup();
    }

    private sealed class ConcurrentFactory : WebApplicationFactory<Program>
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-concurrency-" + Guid.NewGuid().ToString("N"));
        public RequestGate Gate { get; } = new();
        public string DatabasePath => Path.Combine(_directory, "race.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_directory);
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "false",
                ["Storage:Root"] = Path.Combine(_directory, "storage"),
                ["Business:TimeZone"] = "Asia/Manila",
            }));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) || d.ServiceType == typeof(DbContextOptions)).ToList())
                    services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={DatabasePath};Pooling=False;Default Timeout=5"));
                services.Configure<MvcOptions>(options => options.Filters.Add(Gate));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            Read(db =>
            {
                if (db.Users.Any()) return;
                db.Users.AddRange(new User { Name = "Owner", Email = "owner@prophetops.local", PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"), Role = Roles.OwnerManagement },
                    new User { Name = "Second", Email = "second@example.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"), Role = Roles.OwnerManagement });
                db.TravelPackages.Add(new TravelPackage { Code = "ONE", PackageName = "One seat", Destination = "Cebu", AvailableSlots = 1 });
                db.SaveChanges();
            });
        }

        public void Read(Action<AppDbContext> action)
        {
            using var scope = Services.CreateScope();
            action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public void Cleanup() => Directory.Delete(_directory, true);
    }

    // Both requests hold independent open connections before either enters its write transaction.
    private sealed class RequestGate : IAsyncActionFilter, IOrderedFilter
    {
        public int Order => -2000;
        private TaskCompletionSource? _release;
        private int _arrivals;
        private readonly HashSet<object> _connections = new();
        public int DistinctConnections => _connections.Count;

        public void Arm() => _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var release = _release;
            AppDbContext? opened = null;
            if (release is not null)
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                await db.Database.OpenConnectionAsync();
                opened = db;
                lock (_connections) _connections.Add(db.Database.GetDbConnection());
                if (Interlocked.Increment(ref _arrivals) == 2)
                {
                    _release = null;
                    release.SetResult();
                }
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            try { await next(); }
            finally { if (opened is not null) await opened.Database.CloseConnectionAsync(); }
        }
    }
}
