using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ProphetOps.Data;

namespace ProphetOps.PostgresMigrations;

public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=prophetops;Username=prophetops;Password=prophetops";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseConfiguration.Configure(
            options,
            DatabaseProviderKind.Postgres,
            connectionString,
            typeof(PostgresDesignTimeDbContextFactory).Assembly.GetName().Name);
        return new AppDbContext(options.Options);
    }
}
