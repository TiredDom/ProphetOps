using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Data.Tests;

public class RuntimeUpgradeMigrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "prophetops-runtime-upgrade-" + Guid.NewGuid());

    [Fact]
    public void Latest_migrations_apply_to_a_copied_pre_plan_a_database()
    {
        Directory.CreateDirectory(_directory);
        var sourcePath = Path.Combine(_directory, "old-schema.db");
        var copiedPath = Path.Combine(_directory, "copied-old-schema.db");

        CreateOldSchemaDatabase(sourcePath);
        Assert.Equal(1, CountRows(sourcePath, "Users"));
        Assert.Equal(1, CountRows(sourcePath, "TravelPackages"));
        Assert.Equal(1, CountRows(sourcePath, "Bookings"));
        File.Copy(sourcePath, copiedPath);
        Assert.Equal(1, CountRows(copiedPath, "Users"));
        Assert.Equal(1, CountRows(copiedPath, "TravelPackages"));
        Assert.Equal(1, CountRows(copiedPath, "Bookings"));

        using var db = Open(copiedPath);
        db.Database.Migrate();
        Assert.Equal(1, CountRows(copiedPath, "Users"));
        Assert.Equal(1, CountRows(copiedPath, "TravelPackages"));
        Assert.Equal(1, CountRows(copiedPath, "Bookings"));

        Assert.EndsWith("AddObjectCleanupEntries", db.Database.GetAppliedMigrations().Last());
        Assert.Equal(0, CountRows(copiedPath, "ObjectCleanupEntries"));
        var user = Assert.Single(db.Users);
        Assert.Equal("owner@agency.test", user.Email);
        Assert.Equal(1, user.SessionVersion);

        var package = Assert.Single(db.TravelPackages);
        Assert.Equal("COPY-MIG", package.Code);
        Assert.Equal(1, package.Revision);

        var booking = Assert.Single(db.Bookings);
        Assert.Equal("COPY-BKG", booking.Code);
        Assert.Equal(1, booking.Revision);
        Assert.Equal(42000, booking.GrossRevenue);
    }

    private static void CreateOldSchemaDatabase(string path)
    {
        using var source = new SqliteConnection("Data Source=:memory:");
        source.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(source).Options;
        using var db = new AppDbContext(options);
        db.GetService<IMigrator>().Migrate("20260719041840_AddVoidAndAuditTrail");
        db.Database.ExecuteSqlInterpolated($"""
            INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "Role", "Status") VALUES
                ({1}, {"Agency owner"}, {"owner@agency.test"}, {BCrypt.Net.BCrypt.HashPassword("Copied-schema-581!")}, {Roles.OwnerManagement}, {"Active"});
            """);
        db.Database.ExecuteSqlInterpolated($"""
            INSERT INTO "TravelPackages"
                ("Id", "Code", "PackageName", "Destination", "Duration", "BasePrice", "Inclusions",
                 "AvailableSlots", "SoldCount", "ReservedCount", "Status", "LastUpdatedAt", "ImagePath")
            VALUES
                ({7}, {"COPY-MIG"}, {"Copied schema package"}, {"Bohol"}, {"3D2N"}, {14000}, {"Ferry"},
                 {5}, {3}, {1}, {"Normal"}, {"2026-07-01"}, {"old.png"});
            """);
        db.Database.ExecuteSqlInterpolated($"""
            INSERT INTO "Bookings"
                ("Id", "Code", "BookingDate", "PassengerCount", "Client", "TravelPackageId",
                 "PackageName", "PackageCode", "EntryType", "Destination", "GrossRevenue",
                 "PaymentStatus", "BookingStatus", "StaffAssigned", "Source", "Notes",
                 "VoidReason", "VoidedAt", "VoidedBy")
            VALUES
                ({9}, {"COPY-BKG"}, {"2026-07-15"}, {3}, {"Copied schema client"}, {7},
                 {"Copied schema package"}, {"COPY-MIG"}, {"Package preset"}, {"Bohol"}, {42000},
                 {"Pending"}, {"Reserved"}, {"Staff User"}, {"Imported"}, {"Keep me"},
                 {null}, {null}, {null});
            """);
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static AppDbContext Open(string path)
    {
        var connection = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        return new AppDbContext(options);
    }

    private static int CountRows(string path, string table)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""SELECT COUNT(*) FROM "{table}";""";
        return Convert.ToInt32(command.ExecuteScalar());
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
}
