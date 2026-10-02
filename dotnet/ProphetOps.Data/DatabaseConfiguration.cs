using Microsoft.EntityFrameworkCore;

namespace ProphetOps.Data;

public static class DatabaseConfiguration
{
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        DatabaseProviderKind provider,
        string connectionString,
        string? migrationsAssembly = null) =>
        provider switch
        {
            DatabaseProviderKind.Sqlite => options.UseSqlite(connectionString),
            DatabaseProviderKind.Postgres => options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "prophetops");
                if (!string.IsNullOrWhiteSpace(migrationsAssembly))
                    npgsql.MigrationsAssembly(migrationsAssembly);
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider."),
        };
}
