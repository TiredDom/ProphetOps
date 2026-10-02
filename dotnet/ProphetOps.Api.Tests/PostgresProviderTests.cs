using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class PostgresProviderTests
{
    [PostgresFact]
    public async Task Postgres_migrations_are_repeatable_on_clean_fixture_databases()
    {
        await using var first = await OpenFixture();
        await using var second = await OpenFixture();

        await using var firstDb = Context(first.ConnectionString!);
        await using var secondDb = Context(second.ConnectionString!);

        await firstDb.Database.MigrateAsync();
        await secondDb.Database.MigrateAsync();

        var firstMigrations = (await firstDb.Database.GetAppliedMigrationsAsync()).ToList();
        var secondMigrations = (await secondDb.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Contains(firstMigrations, migration => migration.Contains("InitialPostgres", StringComparison.Ordinal));
        Assert.Contains(firstMigrations, migration => migration.Contains("AddObjectCleanupEntries", StringComparison.Ordinal));
        Assert.Contains(secondMigrations, migration => migration.Contains("InitialPostgres", StringComparison.Ordinal));
        Assert.Contains(secondMigrations, migration => migration.Contains("AddObjectCleanupEntries", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task Postgres_round_trips_business_mappings()
    {
        await using var fixture = await OpenFixture();
        await using var db = Context(fixture.ConnectionString!);
        await db.Database.MigrateAsync();

        var package = new TravelPackage
        {
            Code = "PG-MAP",
            PackageName = "Mapping",
            Destination = "Cebu",
            AvailableSlots = 2,
            BasePrice = 12345,
            LastUpdatedAt = new DateOnly(2026, 9, 28),
        };
        db.TravelPackages.Add(package);
        db.Bookings.Add(new Booking
        {
            Code = "PG-BOOK",
            BookingDate = new DateOnly(2026, 9, 28),
            Client = "Client",
            TravelPackage = package,
            PackageName = package.PackageName,
            PackageCode = package.Code,
            EntryType = "Package preset",
            Destination = package.Destination,
            GrossRevenue = 12345,
            PaymentStatus = "Pending",
            BookingStatus = "Pending",
            Source = "Manual quotation",
        });
        db.AuditEntries.Add(new AuditEntry
        {
            At = DateTime.UtcNow,
            Actor = "ci",
            ActorName = "CI",
            Action = "Test",
            EntityType = "Booking",
            EntityCode = "PG-BOOK",
        });
        await db.SaveChangesAsync();

        package.AvailableSlots = 1;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var stored = await db.Bookings.Include(b => b.TravelPackage).SingleAsync(b => b.Code == "PG-BOOK");
        Assert.Equal(new DateOnly(2026, 9, 28), stored.BookingDate);
        Assert.Equal(12345, stored.GrossRevenue);
        Assert.Equal(2, stored.TravelPackage!.Revision);
        Assert.Equal(new DateOnly(2026, 9, 28), stored.TravelPackage.LastUpdatedAt);
        Assert.Single(db.AuditEntries);
    }

    [PostgresFact]
    public async Task Postgres_advisory_lock_times_out_second_writer()
    {
        await using var fixture = await OpenFixture();
        await using var first = Context(fixture.ConnectionString!);
        await first.Database.MigrateAsync();
        await using var second = Context(fixture.ConnectionString!);
        await using var held = await DatabaseWriteScope.BeginAsync(first, DatabaseProviderKind.Postgres, CancellationToken.None);

        var elapsed = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            DatabaseWriteScope.BeginAsync(second, DatabaseProviderKind.Postgres, CancellationToken.None));
        elapsed.Stop();

        Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), $"Lock timeout took {elapsed.Elapsed}.");
    }

    [PostgresFact]
    public async Task Concurrent_postgres_bootstrap_creates_only_one_owner()
    {
        await using var fixture = await OpenFixture();
        await using (var setup = Context(fixture.ConnectionString!))
            await setup.Database.MigrateAsync();

        static async Task<Exception?> TryCreate(string connectionString, string email)
        {
            try
            {
                await using var db = Context(connectionString);
                await ProductionBootstrap.CreateOwnerAsync(
                    db,
                    DatabaseProviderKind.Postgres,
                    "Owner",
                    email,
                    "Concurrent-owner-password-581!",
                    CancellationToken.None);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var results = await Task.WhenAll(
            TryCreate(fixture.ConnectionString!, "first@agency.test"),
            TryCreate(fixture.ConnectionString!, "second@agency.test"));

        await using var verify = Context(fixture.ConnectionString!);
        Assert.Single(verify.Users);
        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is InvalidOperationException);
    }

    private static async Task<PostgresFixture> OpenFixture()
    {
        var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        fixture.RequireAvailable();
        return fixture;
    }

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
}
