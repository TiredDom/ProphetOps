using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;

namespace ProphetOps.Api;

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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            if (!await db.Database.CanConnectAsync(timeout.Token))
                return Result.Unavailable();

            var applied = await db.Database.GetAppliedMigrationsAsync(timeout.Token);
            var appliedIds = applied.ToArray();
            if (appliedIds.Length == 0)
                return Result.Unavailable();

            var expectedIds = db.Database.GetMigrations().ToArray();
            if (!appliedIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
                return Result.Unavailable();

            return Result.Available;
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or InvalidOperationException
            or DbException
            or Microsoft.Data.Sqlite.SqliteException
            or Npgsql.NpgsqlException)
        {
            return Result.Unavailable();
        }
    }

    public static async Task EnsureReadyAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var readiness = await CheckAsync(db, cancellationToken);
        if (!readiness.Ready)
            throw new InvalidOperationException("Database schema validation failed.");
    }
}
