using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class PostgresMigrationTests
{
    [PostgresFact]
    public async Task Postgres_migrations_create_expected_business_schema()
    {
        await using var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        fixture.RequireAvailable();

        var options = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseConfiguration.Configure(
            options,
            DatabaseProviderKind.Postgres,
            fixture.ConnectionString!,
            DatabaseRuntimeOptions.PostgresMigrationsAssembly);

        await using var db = new AppDbContext(options.Options);
        await db.Database.MigrateAsync();
        db.Users.Add(new User
        {
            Email = "postgres-ci@agency.test",
            Name = "Postgres CI",
            PasswordHash = "hash",
            Role = Roles.OwnerManagement,
        });
        await db.SaveChangesAsync();

        Assert.Equal("postgres-ci@agency.test", Assert.Single(db.Users).Email);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "select count(*)::int as \"Value\" from information_schema.tables where table_schema = 'prophetops' and table_name = 'Users'")
            .SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "select count(*)::int as \"Value\" from information_schema.tables where table_schema = 'prophetops' and table_name = '__EFMigrationsHistory'")
            .SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "select count(*)::int as \"Value\" from information_schema.tables where table_schema = 'prophetops' and table_name = 'DataProtectionKeys'")
            .SingleAsync());
    }
}
