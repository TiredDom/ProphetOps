using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class PostgresBookingConcurrencyTests
{
    [PostgresFact]
    public async Task Postgres_two_bookings_competing_for_one_seat_commit_once()
    {
        await using var fixture = await OpenMigratedFixture();
        using var factory = new PostgresConcurrentFactory(fixture.ConnectionString!);
        using var first = await AuthenticatedClient.Login(factory);
        using var second = await AuthenticatedClient.Login(factory);

        factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PostAsJsonAsync("/api/bookings", Booking("PG-RACE-1")),
            second.PostAsJsonAsync("/api/bookings", Booking("PG-RACE-2")));

        Assert.Equal(2, factory.Gate.DistinctConnections);
        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        factory.Read(db =>
        {
            Assert.Equal(0, db.TravelPackages.Single().AvailableSlots);
            Assert.Equal(1, db.TravelPackages.Single().SoldCount);
            Assert.Single(db.Bookings);
            Assert.Single(db.AuditEntries.Where(a => a.EntityType == "Booking"));
        });
    }

    [PostgresFact]
    public async Task Postgres_concurrent_owner_suspensions_keep_an_active_owner()
    {
        await using var fixture = await OpenMigratedFixture();
        using var factory = new PostgresConcurrentFactory(fixture.ConnectionString!);
        using var first = await AuthenticatedClient.Login(factory);
        using var second = await AuthenticatedClient.Login(factory, "second@example.test", "owner123");

        factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PutAsJsonAsync("/api/users/second@example.test", new { name = "Second", email = "second@example.test", role = Roles.OwnerManagement, status = "Suspended" }),
            second.PutAsJsonAsync("/api/users/owner@prophetops.local", new { name = "Owner", email = "owner@prophetops.local", role = Roles.OwnerManagement, status = "Suspended" }));

        Assert.Equal(2, factory.Gate.DistinctConnections);
        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        factory.Read(db => Assert.Equal(1, db.Users.Count(u => u.Role == Roles.OwnerManagement && u.Status == "Active")));
    }

    [PostgresFact]
    public async Task Postgres_package_revision_rejects_concurrent_reservation_overwrite()
    {
        await using var fixture = await OpenMigratedFixture();
        using var factory = new PostgresConcurrentFactory(fixture.ConnectionString!);
        using var first = await AuthenticatedClient.Login(factory);
        using var second = await AuthenticatedClient.Login(factory);

        factory.Gate.Arm();
        var results = await Task.WhenAll(
            first.PutAsJsonAsync("/api/inventory/ONE", new
            {
                id = "ONE",
                packageName = "Renamed",
                destination = "Cebu",
                basePrice = 10,
                availableSlots = 1,
                soldCount = 0,
                reservedCount = 0,
                status = "Normal",
                revision = 1,
            }),
            second.PostAsJsonAsync("/api/bookings", Booking("PG-STOCK")));

        Assert.True(results[1].IsSuccessStatusCode);
        Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        factory.Read(db =>
        {
            var package = db.TravelPackages.Single();
            Assert.Equal(0, package.AvailableSlots);
            Assert.Equal(1, package.SoldCount);
            Assert.Equal(results[0].IsSuccessStatusCode ? 3 : 2, package.Revision);
        });
    }

    [PostgresFact]
    public async Task Postgres_stale_package_revision_is_rejected_without_stock_or_audit_change()
    {
        await using var fixture = await OpenMigratedFixture();
        using var factory = new PostgresConcurrentFactory(fixture.ConnectionString!);
        using var client = await AuthenticatedClient.Login(factory);
        (await client.PostAsJsonAsync("/api/bookings", Booking("PG-STALE"))).EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync("/api/inventory/ONE", new
        {
            id = "ONE",
            packageName = "Stale Rename",
            destination = "Cebu",
            basePrice = 10,
            availableSlots = 1,
            soldCount = 0,
            reservedCount = 0,
            status = "Normal",
            revision = 1,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stale_revision", conflict.GetProperty("code").GetString());
        factory.Read(db =>
        {
            var package = db.TravelPackages.Single();
            Assert.Equal("One seat", package.PackageName);
            Assert.Equal(0, package.AvailableSlots);
            Assert.Equal(1, package.SoldCount);
            Assert.Equal(2, package.Revision);
            Assert.Single(db.AuditEntries.Where(a => a.EntityType == "Booking"));
            Assert.Empty(db.AuditEntries.Where(a => a.EntityType == "TravelPackage"));
        });
    }

    private static async Task<PostgresFixture> OpenMigratedFixture()
    {
        var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        fixture.RequireAvailable();
        await using var db = Context(fixture.ConnectionString!);
        await db.Database.MigrateAsync();
        return fixture;
    }

    private static object Booking(string code) => new
    {
        id = code, ds = "2026-09-14", y = 1, client = "Client", packageId = "ONE",
        entryType = "Package preset", package = "One seat", destination = "Cebu",
        grossRevenue = 10, paymentStatus = "Pending", bookingStatus = "Pending",
        source = "Manual quotation", revision = 1,
    };

    private static AppDbContext Context(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseConfiguration.Configure(
            options,
            DatabaseProviderKind.Postgres,
            connectionString,
            DatabaseRuntimeOptions.PostgresMigrationsAssembly);
        return new AppDbContext(options.Options);
    }

    private sealed class PostgresConcurrentFactory(string connectionString) : WebApplicationFactory<Program>
    {
        public RequestGate Gate { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:Enabled"] = "false",
                ["Hosted:Enabled"] = "false",
                ["Storage:Root"] = Path.Combine(Path.GetTempPath(), "prophetops-pg-concurrency-" + Guid.NewGuid().ToString("N")),
                ["Business:TimeZone"] = "Asia/Manila",
            }));
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType == typeof(DatabaseRuntimeOptions)).ToList())
                    services.Remove(descriptor);
                services.AddSingleton(new DatabaseRuntimeOptions(
                    DatabaseProviderKind.Postgres,
                    connectionString,
                    DatabaseRuntimeOptions.PostgresMigrationsAssembly));
                services.AddDbContext<AppDbContext>(options => DatabaseConfiguration.Configure(
                    options,
                    DatabaseProviderKind.Postgres,
                    connectionString,
                    DatabaseRuntimeOptions.PostgresMigrationsAssembly));
                services.Configure<MvcOptions>(options => options.Filters.Add(Gate));
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            Read(db =>
            {
                if (db.Users.Any()) return;
                db.Users.AddRange(
                    new User { Name = "Owner", Email = "owner@prophetops.local", PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"), Role = Roles.OwnerManagement },
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
    }

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
