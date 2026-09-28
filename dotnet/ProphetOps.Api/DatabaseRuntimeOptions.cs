using ProphetOps.Data;

namespace ProphetOps.Api;

public sealed record DatabaseRuntimeOptions(
    DatabaseProviderKind Provider,
    string ConnectionString,
    string? MigrationsAssembly)
{
    public const string PostgresMigrationsAssembly = "ProphetOps.PostgresMigrations";

    public static DatabaseRuntimeOptions FromConfiguration(
        IConfiguration configuration,
        string localSqliteConnectionString,
        bool useMaintenanceConnection = false)
    {
        var provider = configuration["Database:Provider"]?.Trim();
        var hosted = HostedRuntime.IsEnabled(configuration);
        var accessMode = configuration["Hosted:AccessMode"]?.Trim();
        var requiresPostgres = hosted && string.Equals(accessMode, "ApplicationLogin", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(provider))
        {
            if (requiresPostgres)
                throw new InvalidOperationException("Hosted application-login mode requires Database:Provider=postgres.");
            provider = "sqlite";
        }

        if (string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            if (requiresPostgres)
                throw new InvalidOperationException("Hosted application-login mode requires Database:Provider=postgres.");
            return new DatabaseRuntimeOptions(
                DatabaseProviderKind.Sqlite,
                configuration.GetConnectionString("Default") ?? localSqliteConnectionString,
                null);
        }

        if (string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            var connectionName = useMaintenanceConnection ? "Maintenance" : "Default";
            var connectionString = configuration.GetConnectionString(connectionName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"PostgreSQL database provider requires ConnectionStrings:{connectionName}.");
            return new DatabaseRuntimeOptions(DatabaseProviderKind.Postgres, connectionString, PostgresMigrationsAssembly);
        }

        throw new InvalidOperationException("Database:Provider must be 'sqlite' or 'postgres'.");
    }
}
