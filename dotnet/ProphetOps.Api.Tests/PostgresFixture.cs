using Npgsql;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private const string MarkerTable = "prophetops_fixture_marker";
    private readonly string? _baseConnectionString = Environment.GetEnvironmentVariable("PROPHETOPS_TEST_POSTGRES")
        ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");
    private readonly bool _required = string.Equals(
        Environment.GetEnvironmentVariable("PROPHETOPS_REQUIRE_POSTGRES"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    private string? _databaseName;
    private string? _adminConnectionString;

    public string? ConnectionString { get; private set; }
    public bool Available => ConnectionString is not null;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            if (_required)
                throw new InvalidOperationException("PROPHETOPS_REQUIRE_POSTGRES=true but PROPHETOPS_TEST_POSTGRES is not configured.");
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString);
        RequireIsolatedHost(builder.Host);
        var baseDatabase = builder.Database;
        if (string.IsNullOrWhiteSpace(baseDatabase) || baseDatabase.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL fixture requires an isolated base database, not the default postgres database.");

        _databaseName = "prophetops_h1_" + Guid.NewGuid().ToString("N");
        var adminBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = baseDatabase };
        _adminConnectionString = adminBuilder.ConnectionString;

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        var testBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = _databaseName };
        ConnectionString = testBuilder.ConnectionString;
        await using var test = new NpgsqlConnection(ConnectionString);
        await test.OpenAsync();
        await using var mark = new NpgsqlCommand($"CREATE TABLE {MarkerTable} (id integer primary key); INSERT INTO {MarkerTable} (id) VALUES (1);", test);
        await mark.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_adminConnectionString is null || _databaseName is null)
            return;

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using (var marker = new NpgsqlConnection(ConnectionString))
        {
            await marker.OpenAsync();
            await using var verifyMarker = new NpgsqlCommand($"SELECT COUNT(*) FROM {MarkerTable}", marker);
            var markerCount = await verifyMarker.ExecuteScalarAsync();
            if (!Equals(markerCount, 1L))
                throw new InvalidOperationException("Refusing PostgreSQL fixture cleanup because the disposable database marker was not verified.");
        }

        await using (var verify = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname = @name", admin))
        {
            verify.Parameters.AddWithValue("name", _databaseName);
            var found = await verify.ExecuteScalarAsync();
            if (!string.Equals(found as string, _databaseName, StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing PostgreSQL fixture cleanup because the disposable database could not be verified.");
        }

        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid();",
            admin))
        {
            terminate.Parameters.AddWithValue("name", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public void RequireAvailable()
    {
        if (Available)
            return;
        throw new InvalidOperationException(_required
            ? "Required PostgreSQL fixture is unavailable."
            : "PostgreSQL fixture is not configured; set PROPHETOPS_TEST_POSTGRES for provider tests.");
    }

    private static void RequireIsolatedHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("PostgreSQL fixture host is required.");
        var allowed = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("postgres", StringComparison.OrdinalIgnoreCase);
        if (!allowed)
            throw new InvalidOperationException("PostgreSQL fixture refuses non-local hosts. Use a disposable local/CI service, never Supabase or agency databases.");
    }
}
