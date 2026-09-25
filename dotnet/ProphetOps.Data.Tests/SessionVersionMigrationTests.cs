using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Data.Tests;

public class SessionVersionMigrationTests
{
    [Fact]
    public void AddSessionVersion_preserves_existing_users_and_initializes_version_one()
    {
        const string previousMigration = "20260719041840_AddVoidAndAuditTrail";
        const string sessionMigration = "20260911114236_AddSessionVersion";
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var activeHash = BCrypt.Net.BCrypt.HashPassword("Active-migration-test-581!");
        var suspendedHash = BCrypt.Net.BCrypt.HashPassword("Suspended-migration-test-581!");
        List<(int Id, string Name, string Email, string PasswordHash, string Role, string Status)> before;

        using (var db = new AppDbContext(options))
        {
            db.GetService<IMigrator>().Migrate(previousMigration);
            Assert.Equal(previousMigration, db.Database.GetAppliedMigrations().Last());
            // Insert through the historical schema, which has no SessionVersion column yet.
            db.Database.ExecuteSqlInterpolated($"""
                INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "Role", "Status") VALUES
                    ({41}, {"Active owner"}, {"active@agency.test"}, {activeHash}, {Roles.OwnerManagement}, {"Active"}),
                    ({83}, {"Suspended admin"}, {"suspended@agency.test"}, {suspendedHash}, {Roles.Admin}, {"Suspended"});
                """);
            before = ReadUsers(db);
            Assert.Equal(2, before.Count);
        }

        using (var db = new AppDbContext(options))
            db.GetService<IMigrator>().Migrate(sessionMigration);

        using (var db = new AppDbContext(options))
        {
            Assert.Equal(sessionMigration, db.Database.GetAppliedMigrations().Last());
            Assert.Equal(before, ReadUsers(db));
            Assert.Equal(new[] { 1, 1 }, db.Users.OrderBy(user => user.Id).Select(user => user.SessionVersion).ToArray());
        }
    }

    [Fact]
    public void AddMutationRevisions_preserves_existing_bookings_and_packages_and_initializes_revision_one()
    {
        const string previousMigration = "20260911114236_AddSessionVersion";
        const string revisionMigration = "20260914013000_AddMutationRevisions";
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        List<(int Id, string Code, string Name, int AvailableSlots, int SoldCount, int ReservedCount, int BasePrice)> packagesBefore;
        List<(int Id, string Code, int TravelPackageId, int PassengerCount, int GrossRevenue)> bookingsBefore;

        using (var db = new AppDbContext(options))
        {
            db.GetService<IMigrator>().Migrate(previousMigration);
            Assert.Equal(previousMigration, db.Database.GetAppliedMigrations().Last());
            db.Database.ExecuteSqlInterpolated($"""
                INSERT INTO "TravelPackages"
                    ("Id", "Code", "PackageName", "Destination", "Duration", "BasePrice", "Inclusions",
                     "AvailableSlots", "SoldCount", "ReservedCount", "Status", "LastUpdatedAt", "ImagePath")
                VALUES
                    ({501}, {"PKG-MIG"}, {"Migration package"}, {"Cebu"}, {"3D2N"}, {12000}, {"Meals"},
                     {7}, {3}, {2}, {"Normal"}, {"2026-07-01"}, {"old.png"});
                """);
            db.Database.ExecuteSqlInterpolated($"""
                INSERT INTO "Bookings"
                    ("Id", "Code", "BookingDate", "PassengerCount", "Client", "TravelPackageId",
                     "PackageName", "PackageCode", "EntryType", "Destination", "GrossRevenue",
                     "PaymentStatus", "BookingStatus", "StaffAssigned", "Source", "Notes",
                     "VoidReason", "VoidedAt", "VoidedBy")
                VALUES
                    ({601}, {"BKG-MIG"}, {"2026-07-15"}, {3}, {"Migration client"}, {501},
                     {"Migration package"}, {"PKG-MIG"}, {"Package preset"}, {"Cebu"}, {36000},
                     {"Pending"}, {"Reserved"}, {"Staff User"}, {"Imported"}, {"Keep me"},
                     {null}, {null}, {null});
                """);
            packagesBefore = ReadPackages(db);
            bookingsBefore = ReadBookings(db);
        }

        using (var db = new AppDbContext(options))
            db.GetService<IMigrator>().Migrate(revisionMigration);

        using (var db = new AppDbContext(options))
        {
            Assert.Equal(revisionMigration, db.Database.GetAppliedMigrations().Last());
            Assert.Equal(packagesBefore, ReadPackages(db));
            Assert.Equal(bookingsBefore, ReadBookings(db));
            Assert.Equal(new[] { 1 }, db.TravelPackages.OrderBy(p => p.Id).Select(p => p.Revision).ToArray());
            Assert.Equal(new[] { 1 }, db.Bookings.OrderBy(b => b.Id).Select(b => b.Revision).ToArray());
        }
    }

    private static List<(int Id, string Name, string Email, string PasswordHash, string Role, string Status)> ReadUsers(AppDbContext db)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Id, Name, Email, PasswordHash, Role, Status FROM Users ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rows = new List<(int, string, string, string, string, string)>();
        while (reader.Read())
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return rows;
    }

    private static List<(int Id, string Code, string Name, int AvailableSlots, int SoldCount, int ReservedCount, int BasePrice)> ReadPackages(AppDbContext db)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Id, Code, PackageName, AvailableSlots, SoldCount, ReservedCount, BasePrice FROM TravelPackages ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rows = new List<(int, string, string, int, int, int, int)>();
        while (reader.Read())
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        return rows;
    }

    private static List<(int Id, string Code, int TravelPackageId, int PassengerCount, int GrossRevenue)> ReadBookings(AppDbContext db)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Id, Code, TravelPackageId, PassengerCount, GrossRevenue FROM Bookings ORDER BY Id";
        using var reader = command.ExecuteReader();
        var rows = new List<(int, string, int, int, int)>();
        while (reader.Read())
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)));
        return rows;
    }
}
