using System.Data.Common;
using System.Security.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProphetOps.Data;

namespace ProphetOps.Api;

public enum DatabaseReadinessFailureCategory
{
    None = 0,
    CaFileMissing,
    CaFileUnreadable,
    ConnectionFailure,
    TlsFailure,
    AuthenticationFailure,
    TimeoutFailure,
    MigrationHistoryReadFailure,
    MissingMigrations,
    MigrationMismatch
}

public sealed record DatabaseReadinessReport(
    bool Ready,
    DatabaseReadinessFailureCategory? FailureCategory = null,
    string? DiagnosticMessage = null)
{
    public static readonly DatabaseReadinessReport ReadyReport = new(true);
}

public static class DatabaseReadiness
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public sealed record Result(bool Ready, string Status, string? Detail = null)
    {
        public static readonly Result Available = new(true, "ready");
        public static Result Unavailable(string? detail = null) => new(false, "unavailable", detail);
    }

    public static async Task<Result> CheckAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var report = await DiagnoseAsync(db, cancellationToken);
        return report.Ready ? Result.Available : Result.Unavailable();
    }

    public static async Task EnsureReadyAsync(AppDbContext db, ILogger? logger, CancellationToken cancellationToken = default)
    {
        var report = await DiagnoseAsync(db, cancellationToken);
        if (!report.Ready)
        {
            logger?.LogError("Database readiness check failed: [{Category}] {Message}", report.FailureCategory, report.DiagnosticMessage);
            throw new InvalidOperationException($"Database schema validation failed: [{report.FailureCategory}] {report.DiagnosticMessage}");
        }
    }

    public static Task EnsureReadyAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
        EnsureReadyAsync(db, null, cancellationToken);

    public static async Task<DatabaseReadinessReport> DiagnoseAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        // 1. Check CA certificate file if configured
        var rootCert = ExtractRootCertificatePath(db);
        if (!string.IsNullOrWhiteSpace(rootCert))
        {
            var caReport = CheckCaCertificate(rootCert);
            if (caReport != null)
                return caReport;
        }

        // 2. Check Database Connection
        var connReport = await CheckConnectionAsync(db, cancellationToken);
        if (connReport != null)
            return connReport;

        // 3. Check Migration History and Applied Migrations
        var migReport = await CheckMigrationsAsync(db, cancellationToken);
        if (migReport != null)
            return migReport;

        return DatabaseReadinessReport.ReadyReport;
    }

    public static string? ExtractRootCertificatePath(AppDbContext db)
    {
        try
        {
            var connStr = db.Database.GetConnectionString();
            if (!string.IsNullOrWhiteSpace(connStr) &&
                (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true ||
                 connStr.Contains("Root Certificate", StringComparison.OrdinalIgnoreCase) ||
                 connStr.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase)))
            {
                var builder = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
                if (!string.IsNullOrWhiteSpace(builder.RootCertificate))
                {
                    return builder.RootCertificate;
                }
            }
        }
        catch
        {
            // Ignore connection string parse errors; connection check will handle invalid connection strings
        }

        var envCert = Environment.GetEnvironmentVariable("PGSSLROOTCERT");
        if (!string.IsNullOrWhiteSpace(envCert))
        {
            return envCert;
        }

        return null;
    }

    public static DatabaseReadinessReport? CheckCaCertificate(string caPath)
    {
        if (!File.Exists(caPath))
        {
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.CaFileMissing,
                DiagnosticMessage: $"CA certificate file is missing at path '{caPath}'.");
        }

        try
        {
            using var stream = File.Open(caPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0)
            {
                return new DatabaseReadinessReport(
                    Ready: false,
                    FailureCategory: DatabaseReadinessFailureCategory.CaFileUnreadable,
                    DiagnosticMessage: $"CA certificate file at path '{caPath}' is empty (0 bytes).");
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.CaFileUnreadable,
                DiagnosticMessage: $"CA certificate file at path '{caPath}' is unreadable (permission denied).");
        }
        catch (Exception)
        {
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.CaFileUnreadable,
                DiagnosticMessage: $"CA certificate file at path '{caPath}' is unreadable (I/O error).");
        }

        return null;
    }

    private static async Task<DatabaseReadinessReport?> CheckConnectionAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await db.Database.OpenConnectionAsync(timeout.Token);
            return null;
        }
        catch (Exception ex)
        {
            var isTimeout = timeout.IsCancellationRequested || ex is OperationCanceledException or TimeoutException;
            var failureCategory = CategorizeConnectionException(ex, isTimeout);
            var diagnosticMessage = FormatConnectionFailureMessage(failureCategory);
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: failureCategory,
                DiagnosticMessage: diagnosticMessage);
        }
        finally
        {
            try
            {
                await db.Database.CloseConnectionAsync();
            }
            catch
            {
                // Suppress close error
            }
        }
    }

    public static DatabaseReadinessFailureCategory CategorizeConnectionException(Exception ex, bool isTimeout = false)
    {
        if (isTimeout)
        {
            return DatabaseReadinessFailureCategory.TimeoutFailure;
        }

        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is OperationCanceledException or TimeoutException)
            {
                return DatabaseReadinessFailureCategory.TimeoutFailure;
            }

            if (current is FileNotFoundException)
            {
                return DatabaseReadinessFailureCategory.CaFileMissing;
            }

            if (current is UnauthorizedAccessException)
            {
                return DatabaseReadinessFailureCategory.CaFileUnreadable;
            }

            if (current is Npgsql.PostgresException pgEx)
            {
                if (pgEx.SqlState is "28P01" or "28000")
                {
                    return DatabaseReadinessFailureCategory.AuthenticationFailure;
                }
            }

            if (current is AuthenticationException or System.Security.Cryptography.CryptographicException)
            {
                return DatabaseReadinessFailureCategory.TlsFailure;
            }

            var typeName = current.GetType().FullName ?? current.GetType().Name;
            if (typeName.Contains("Tls", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("Ssl", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseReadinessFailureCategory.TlsFailure;
            }

            var msg = current.Message;
            if (msg.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("handshake", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseReadinessFailureCategory.TlsFailure;
            }

            if (msg.Contains("password authentication failed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("authentication failed", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseReadinessFailureCategory.AuthenticationFailure;
            }

            if (msg.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseReadinessFailureCategory.TimeoutFailure;
            }
        }

        return DatabaseReadinessFailureCategory.ConnectionFailure;
    }

    private static string FormatConnectionFailureMessage(DatabaseReadinessFailureCategory category)
    {
        return category switch
        {
            DatabaseReadinessFailureCategory.CaFileMissing => "Database connection failed: CA certificate file is missing.",
            DatabaseReadinessFailureCategory.CaFileUnreadable => "Database connection failed: CA certificate file is unreadable (permission denied).",
            DatabaseReadinessFailureCategory.AuthenticationFailure => "Database connection failed: authentication rejected by server.",
            DatabaseReadinessFailureCategory.TlsFailure => "Database connection failed: TLS/SSL handshake or certificate validation failure.",
            DatabaseReadinessFailureCategory.TimeoutFailure => "Database connection failed: connection attempt timed out.",
            _ => "Database connection failed: unable to establish connection to database server.",
        };
    }

    private static async Task<DatabaseReadinessReport?> CheckMigrationsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        IReadOnlyList<string> applied;
        try
        {
            var result = await db.Database.GetAppliedMigrationsAsync(timeout.Token);
            applied = result.ToArray();
        }
        catch (Exception)
        {
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.MigrationHistoryReadFailure,
                DiagnosticMessage: "Failed to read migration history from database (migration table query failed).");
        }

        if (applied.Count == 0)
        {
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.MissingMigrations,
                DiagnosticMessage: "No applied migrations found in database (database schema is empty).");
        }

        string[] expected;
        try
        {
            expected = db.Database.GetMigrations().ToArray();
        }
        catch (Exception)
        {
            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.MigrationHistoryReadFailure,
                DiagnosticMessage: "Failed to resolve expected migrations from application assembly.");
        }

        if (!applied.SequenceEqual(expected, StringComparer.Ordinal))
        {
            var pending = expected.Except(applied, StringComparer.Ordinal).ToArray();
            var unrecognised = applied.Except(expected, StringComparer.Ordinal).ToArray();

            var detail = (pending.Length, unrecognised.Length) switch
            {
                ( > 0, > 0) => $"Migration mismatch: {pending.Length} pending migration(s) and {unrecognised.Length} unrecognised migration(s) found (applied: {applied.Count}, expected: {expected.Length}).",
                ( > 0, 0) => $"Migration mismatch: {pending.Length} pending migration(s) found (applied: {applied.Count}, expected: {expected.Length}).",
                _ => $"Migration mismatch: {unrecognised.Length} unrecognised migration(s) found (applied: {applied.Count}, expected: {expected.Length}).",
            };

            return new DatabaseReadinessReport(
                Ready: false,
                FailureCategory: DatabaseReadinessFailureCategory.MigrationMismatch,
                DiagnosticMessage: detail);
        }

        return null;
    }
}
