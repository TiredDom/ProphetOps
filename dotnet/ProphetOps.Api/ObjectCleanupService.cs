using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;

namespace ProphetOps.Api;

public enum ObjectRetentionDecision
{
    SafeToDelete,
    Retain,
    Unknown,
}

public interface IObjectRetentionPolicy
{
    Task<ObjectRetentionDecision> CanDeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class ConservativeObjectRetentionPolicy : IObjectRetentionPolicy
{
    public Task<ObjectRetentionDecision> CanDeleteAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(ObjectRetentionDecision.Unknown);
}

public sealed class ObjectCleanupService(
    IServiceScopeFactory scopes,
    IObjectStorage objects,
    IObjectRetentionPolicy retention,
    TimeProvider time,
    ILogger<ObjectCleanupService> log,
    MaintenanceGate? gate = null,
    DatabaseRuntimeOptions? database = null)
{
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        IDisposable? gateLease = null;
        if (gate is not null)
        {
            var admission = gate.TryEnterMutation();
            if (!admission.Allowed)
            {
                log.LogInformation("Object cleanup deferred because MaintenanceGate is closed for backup/maintenance.");
                return 0;
            }
            gateLease = admission.Lease;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = time.GetUtcNow().UtcDateTime;
            var entries = await db.ObjectCleanupEntries
                .Where(entry => entry.EligibleAtUtc <= now)
                .OrderBy(entry => entry.Id)
                .Take(25)
                .ToListAsync(cancellationToken);
            var deleted = 0;

            var provider = database?.Provider ?? (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true
                ? DatabaseProviderKind.Postgres
                : DatabaseProviderKind.Sqlite);

            foreach (var entry in entries)
            {
                if (await db.TravelPackages.AsNoTracking().AnyAsync(package => package.ImagePath == entry.ObjectKey, cancellationToken))
                    continue;

                var decision = await retention.CanDeleteAsync(entry.ObjectKey, cancellationToken);
                if (decision != ObjectRetentionDecision.SafeToDelete)
                    continue;

                try
                {
                    await using var writeScope = await DatabaseWriteScope.BeginAsync(db, provider, cancellationToken);

                    if (await db.TravelPackages.AsNoTracking().AnyAsync(package => package.ImagePath == entry.ObjectKey, cancellationToken))
                        continue;

                    await objects.DeleteAsync(entry.ObjectKey, cancellationToken);
                    db.ObjectCleanupEntries.Remove(entry);
                    await db.SaveChangesAsync(cancellationToken);
                    await writeScope.CommitAsync(cancellationToken);
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectStorageUnavailable)
                {
                    entry.Attempts++;
                    entry.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    entry.EligibleAtUtc = now.AddMinutes(Math.Min(60, entry.Attempts));
                    await db.SaveChangesAsync(cancellationToken);
                    log.LogWarning(ex, "Object cleanup failed for {ObjectKey}.", entry.ObjectKey);
                }
            }

            return deleted;
        }
        finally
        {
            gateLease?.Dispose();
        }
    }
}
