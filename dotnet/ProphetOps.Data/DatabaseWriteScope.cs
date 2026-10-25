using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ProphetOps.Data;

public sealed class DatabaseWriteScope : IAsyncDisposable
{
    private readonly DbTransaction? _transaction;
    private readonly IDbContextTransaction _contextTransaction;
    private bool _committed;

    private DatabaseWriteScope(DbTransaction? transaction, IDbContextTransaction contextTransaction)
    {
        _transaction = transaction;
        _contextTransaction = contextTransaction;
    }

    public static async Task<DatabaseWriteScope> BeginAsync(
        AppDbContext db,
        DatabaseProviderKind provider,
        CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);

        if (provider == DatabaseProviderKind.Sqlite)
        {
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            connection.DefaultTimeout = 5;
            var transaction = connection.BeginTransaction(deferred: false);
            var contextTransaction = await db.Database.UseTransactionAsync(transaction, cancellationToken);
            return new DatabaseWriteScope(transaction, contextTransaction!);
        }

        if (provider == DatabaseProviderKind.Postgres)
        {
            var contextTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s';", cancellationToken);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8070, 1);", cancellationToken);
                return new DatabaseWriteScope(null, contextTransaction);
            }
            catch
            {
                await contextTransaction.RollbackAsync(CancellationToken.None);
                await contextTransaction.DisposeAsync();
                throw;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.");
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
            await _contextTransaction.CommitAsync(cancellationToken);
        else
            await _transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_committed)
            {
                if (_transaction is null)
                    await _contextTransaction.RollbackAsync();
                else
                    await _transaction.RollbackAsync();
            }
        }
        finally
        {
            await _contextTransaction.DisposeAsync();
            if (_transaction is not null)
                await _transaction.DisposeAsync();
        }
    }
}
