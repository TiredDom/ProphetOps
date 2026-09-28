using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ProphetOps.Api;
using ProphetOps.Data;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class HostedDatabaseConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "prophetops-db-config-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Local_mode_defaults_to_sqlite_under_storage_root()
    {
        var config = Config(("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var options = DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabasePath);

        Assert.Equal(DatabaseProviderKind.Sqlite, options.Provider);
        Assert.Contains("prophetops.db", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Null(options.MigrationsAssembly);
    }

    [Fact]
    public void Local_sqlite_keeps_legacy_default_connection_string_override()
    {
        var config = Config(
            ("ConnectionStrings:Default", "Data Source=legacy-test.db"),
            ("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var options = DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabaseConnectionString);

        Assert.Equal(DatabaseProviderKind.Sqlite, options.Provider);
        Assert.Equal("Data Source=legacy-test.db", options.ConnectionString);
    }

    [Fact]
    public void Hosted_application_login_requires_postgres_provider()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Hosted:AccessMode", "ApplicationLogin"),
            ("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabasePath));

        Assert.Contains("Database:Provider=postgres", error.Message);
    }

    [Fact]
    public void Hosted_postgres_requires_default_connection_string()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Hosted:AccessMode", "ApplicationLogin"),
            ("Database:Provider", "postgres"),
            ("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabasePath));

        Assert.Contains("ConnectionStrings:Default", error.Message);
    }

    [Fact]
    public void Hosted_postgres_maintenance_uses_maintenance_connection_string()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Hosted:AccessMode", "ApplicationLogin"),
            ("Database:Provider", "postgres"),
            ("ConnectionStrings:Default", "Host=localhost;Database=runtime"),
            ("ConnectionStrings:Maintenance", "Host=localhost;Database=maintenance"),
            ("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var options = DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabaseConnectionString, useMaintenanceConnection: true);

        Assert.Equal(DatabaseProviderKind.Postgres, options.Provider);
        Assert.Equal("Host=localhost;Database=maintenance", options.ConnectionString);
    }

    [Fact]
    public void Hosted_postgres_maintenance_requires_maintenance_connection_string()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Hosted:AccessMode", "ApplicationLogin"),
            ("Database:Provider", "postgres"),
            ("ConnectionStrings:Default", "Host=localhost;Database=runtime"),
            ("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabaseConnectionString, useMaintenanceConnection: true));

        Assert.Contains("ConnectionStrings:Maintenance", error.Message);
    }

    [Fact]
    public void Hosted_cloudflare_legacy_can_still_use_explicit_sqlite()
    {
        var config = Config(
            ("Hosted:Enabled", "true"),
            ("Hosted:AccessMode", "CloudflareAccess"),
            ("Database:Provider", "sqlite"),
            ("Storage:Root", _root));
        var paths = StoragePaths.FromConfiguration(config, Env());

        var options = DatabaseRuntimeOptions.FromConfiguration(config, paths.DatabasePath);

        Assert.Equal(DatabaseProviderKind.Sqlite, options.Provider);
        Assert.Contains("prophetops.db", options.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    private IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value)).Build();

    private IHostEnvironment Env() => new TestEnvironment(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ProphetOps.Api.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
