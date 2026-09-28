using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Api;
using ProphetOps.Data;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class DatabaseReadinessTests
{
    [Fact]
    public async Task Migrated_database_is_ready()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync();

        var status = await DatabaseReadiness.CheckAsync(db, CancellationToken.None);

        Assert.True(status.Ready);
        Assert.Equal("ready", status.Status);
    }

    [Fact]
    public async Task Missing_schema_is_unavailable_without_details()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);

        var status = await DatabaseReadiness.CheckAsync(db, CancellationToken.None);

        Assert.False(status.Ready);
        Assert.Equal("unavailable", status.Status);
        Assert.Null(status.Detail);
    }

    [Fact]
    public async Task Ahead_of_build_schema_is_unavailable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "insert into __EFMigrationsHistory (MigrationId, ProductVersion) values ('99999999999999_FutureMigration', '99.0.0')");

        var status = await DatabaseReadiness.CheckAsync(db, CancellationToken.None);

        Assert.False(status.Ready);
        Assert.Equal("unavailable", status.Status);
        Assert.Null(status.Detail);
    }

    private static AppDbContext Context(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
}
