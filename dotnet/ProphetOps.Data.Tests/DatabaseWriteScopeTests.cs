using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Data.Tests;

public sealed class DatabaseWriteScopeTests
{
    [Fact]
    public async Task CommitAsync_persists_changes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            await using var scope = await DatabaseWriteScope.BeginAsync(db, DatabaseProviderKind.Sqlite, CancellationToken.None);
            db.Users.Add(new User
            {
                Email = "committed@agency.test",
                Name = "Committed",
                PasswordHash = "hash",
                Role = Roles.OwnerManagement,
            });
            await db.SaveChangesAsync();
            await scope.CommitAsync(CancellationToken.None);
        }

        await using var verification = new AppDbContext(options);
        Assert.Equal("committed@agency.test", Assert.Single(verification.Users).Email);
    }

    [Fact]
    public async Task DisposeAsync_rolls_back_uncommitted_changes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            await using var scope = await DatabaseWriteScope.BeginAsync(db, DatabaseProviderKind.Sqlite, CancellationToken.None);
            db.Users.Add(new User
            {
                Email = "rolled-back@agency.test",
                Name = "Rolled Back",
                PasswordHash = "hash",
                Role = Roles.OwnerManagement,
            });
            await db.SaveChangesAsync();
        }

        await using var verification = new AppDbContext(options);
        Assert.Empty(verification.Users);
    }
}
