using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Data.Tests;

public class BootstrapTests
{
    [Fact]
    public void Demo_seeding_leaves_a_partially_populated_database_alone()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.Migrate();
        db.Users.Add(new User { Email = "existing@agency.test", Role = Roles.OwnerManagement, PasswordHash = "keep-this-hash" });
        db.Bookings.Add(new Booking { Code = "EXISTING", Client = "Existing client", GrossRevenue = 800 });
        db.SaveChanges();

        DbSeeder.Seed(db);

        Assert.Equal("keep-this-hash", Assert.Single(db.Users).PasswordHash);
        Assert.Equal("EXISTING", Assert.Single(db.Bookings).Code);
        Assert.Empty(db.TravelPackages);
        Assert.Empty(db.Expenses);
        Assert.Empty(db.AuditEntries);
    }
}
