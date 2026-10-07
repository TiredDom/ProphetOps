using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task Diagnose_returns_ready_report_for_migrated_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync();

        var report = await DatabaseReadiness.DiagnoseAsync(db, CancellationToken.None);

        Assert.True(report.Ready);
        Assert.Null(report.FailureCategory);
        Assert.Null(report.DiagnosticMessage);
    }

    [Fact]
    public async Task Diagnose_identifies_missing_migrations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);

        var report = await DatabaseReadiness.DiagnoseAsync(db, CancellationToken.None);

        Assert.False(report.Ready);
        Assert.Equal(DatabaseReadinessFailureCategory.MissingMigrations, report.FailureCategory);
        Assert.Contains("No applied migrations found", report.DiagnosticMessage);
    }

    [Fact]
    public async Task Diagnose_identifies_migration_mismatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "insert into __EFMigrationsHistory (MigrationId, ProductVersion) values ('99999999999999_FutureMigration', '99.0.0')");

        var report = await DatabaseReadiness.DiagnoseAsync(db, CancellationToken.None);

        Assert.False(report.Ready);
        Assert.Equal(DatabaseReadinessFailureCategory.MigrationMismatch, report.FailureCategory);
        Assert.Contains("Migration mismatch", report.DiagnosticMessage);
    }

    [Fact]
    public async Task Diagnose_identifies_missing_ca_file()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "prophetops-missing-ca-" + Guid.NewGuid().ToString("N") + ".crt");
        var connStr = $"Host=localhost;Database=test;Username=user;Password=pass;SSL Mode=VerifyFull;Root Certificate={nonExistentPath};";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr)
            .Options;
        await using var db = new AppDbContext(options);

        var report = await DatabaseReadiness.DiagnoseAsync(db, CancellationToken.None);

        Assert.False(report.Ready);
        Assert.Equal(DatabaseReadinessFailureCategory.CaFileMissing, report.FailureCategory);
        Assert.Contains("CA certificate file is missing", report.DiagnosticMessage);
    }

    [Fact]
    public async Task Diagnose_identifies_unreadable_ca_file()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "prophetops-locked-ca-" + Guid.NewGuid().ToString("N") + ".crt");
        await File.WriteAllTextAsync(tempFile, "CERTIFICATE-DATA");

        try
        {
            // Lock the file exclusively so any read attempt fails
            await using var lockStream = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var connStr = $"Host=localhost;Database=test;Username=user;Password=pass;SSL Mode=VerifyFull;Root Certificate={tempFile};";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connStr)
                .Options;
            await using var db = new AppDbContext(options);

            var report = await DatabaseReadiness.DiagnoseAsync(db, CancellationToken.None);

            Assert.False(report.Ready);
            Assert.Equal(DatabaseReadinessFailureCategory.CaFileUnreadable, report.FailureCategory);
            Assert.Contains("unreadable", report.DiagnosticMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Theory]
    [InlineData("password authentication failed", DatabaseReadinessFailureCategory.AuthenticationFailure)]
    [InlineData("SSL handshake failed", DatabaseReadinessFailureCategory.TlsFailure)]
    [InlineData("certificate validation failed", DatabaseReadinessFailureCategory.TlsFailure)]
    [InlineData("connection attempt timed out", DatabaseReadinessFailureCategory.TimeoutFailure)]
    [InlineData("connection refused", DatabaseReadinessFailureCategory.ConnectionFailure)]
    public void CategorizeConnectionException_maps_messages_correctly(string message, DatabaseReadinessFailureCategory expected)
    {
        var ex = new Npgsql.NpgsqlException(message);
        var category = DatabaseReadiness.CategorizeConnectionException(ex);
        Assert.Equal(expected, category);
    }

    [Fact]
    public void CategorizeConnectionException_distinguishes_typed_exceptions()
    {
        Assert.Equal(DatabaseReadinessFailureCategory.TlsFailure,
            DatabaseReadiness.CategorizeConnectionException(new AuthenticationException("Remote certificate invalid")));
        Assert.Equal(DatabaseReadinessFailureCategory.TlsFailure,
            DatabaseReadiness.CategorizeConnectionException(new CryptographicException("Bad certificate")));
        Assert.Equal(DatabaseReadinessFailureCategory.TimeoutFailure,
            DatabaseReadiness.CategorizeConnectionException(new TimeoutException("Timed out")));
        Assert.Equal(DatabaseReadinessFailureCategory.TimeoutFailure,
            DatabaseReadiness.CategorizeConnectionException(new Exception(), isTimeout: true));
        Assert.Equal(DatabaseReadinessFailureCategory.CaFileMissing,
            DatabaseReadiness.CategorizeConnectionException(new FileNotFoundException("CA file missing")));
        Assert.Equal(DatabaseReadinessFailureCategory.CaFileUnreadable,
            DatabaseReadiness.CategorizeConnectionException(new UnauthorizedAccessException("Permission denied")));
        Assert.Equal(DatabaseReadinessFailureCategory.ConnectionFailure,
            DatabaseReadiness.CategorizeConnectionException(new SocketException((int)SocketError.ConnectionRefused)));
    }

    [Fact]
    public async Task EnsureReadyAsync_logs_category_and_throws_sanitized_exception()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        // DB has no migrations applied -> MissingMigrations

        var logger = new TestLogger();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseReadiness.EnsureReadyAsync(db, logger, CancellationToken.None));

        Assert.Contains("[MissingMigrations]", ex.Message);
        Assert.Contains("No applied migrations found", ex.Message);
        Assert.Single(logger.Messages);
        Assert.Contains("[MissingMigrations]", logger.Messages[0]);
    }

    [Fact]
    public async Task Diagnostics_never_leak_credentials_connection_strings_or_certificate_contents()
    {
        var secretPassword = "SuperSecretPassword123!@#";
        var secretUsername = "sensitive_owner_user";
        var secretHost = "secret-pg-host.supabase.co";
        var nonExistentCa = Path.Combine(Path.GetTempPath(), "prophetops-leak-test-" + Guid.NewGuid().ToString("N") + ".crt");

        var connStr = $"Host={secretHost};Port=5432;Database=prophetops;Username={secretUsername};Password={secretPassword};SSL Mode=VerifyFull;Root Certificate={nonExistentCa};";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr)
            .Options;
        await using var db = new AppDbContext(options);

        var logger = new TestLogger();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseReadiness.EnsureReadyAsync(db, logger, CancellationToken.None));

        var report = await DatabaseReadiness.DiagnoseAsync(db, CancellationToken.None);

        var allOutput = ex.Message + " " + (report.DiagnosticMessage ?? "") + " " + string.Join(" ", logger.Messages);

        // Assert strictly zero leakage of sensitive data
        Assert.DoesNotContain(secretPassword, allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secretUsername, allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Username=", allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secretHost, allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN CERTIFICATE", allOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("masterKey", allOutput, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext Context(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private sealed class TestLogger : ILogger
    {
        public readonly List<string> Messages = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
