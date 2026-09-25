using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ProphetOps.Api;

public sealed class MaintenanceGate
{
    public const int RetryAfterSeconds = 15;
    private readonly object _lock = new();
    private readonly TimeProvider _clock;
    private readonly ILogger<MaintenanceGate> _log;
    private bool _closed;
    private int _activeMutations;
    private TaskCompletionSource _drained = NewDrained();

    public MaintenanceGate(TimeProvider clock, ILogger<MaintenanceGate> log)
    {
        _clock = clock;
        _log = log;
        _drained.SetResult();
    }

    public MutationAdmission TryEnterMutation()
    {
        lock (_lock)
        {
            if (_closed) return MutationAdmission.Rejected();
            _activeMutations++;
            if (_activeMutations == 1 && _drained.Task.IsCompleted) _drained = NewDrained();
            return MutationAdmission.Accepted(new Lease(this));
        }
    }

    public async Task<BackupCaptureLease?> TryBeginBackupCaptureAsync(TimeSpan drainTimeout, CancellationToken cancellationToken)
    {
        Task drained;
        lock (_lock)
        {
            if (_closed) return null;
            _closed = true;
            drained = _drained.Task;
        }

        try
        {
            await drained.WaitAsync(drainTimeout, cancellationToken);
            return new BackupCaptureLease(this, _clock.GetUtcNow());
        }
        catch (TimeoutException)
        {
            _log.LogWarning("Backup capture could not start because active writes did not drain in time.");
            Reopen();
            return null;
        }
        catch
        {
            Reopen();
            throw;
        }
    }

    public static ObjectResult RetryableResult()
    {
        var result = new ObjectResult(new
        {
            code = "maintenance_backup",
            message = "A backup is in progress. Try again shortly.",
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
        };
        return result;
    }

    private void ExitMutation()
    {
        lock (_lock)
        {
            _activeMutations--;
            if (_activeMutations == 0) _drained.TrySetResult();
        }
    }

    private void Reopen()
    {
        lock (_lock)
        {
            _closed = false;
        }
    }

    private static TaskCompletionSource NewDrained() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Lease(MaintenanceGate gate) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.ExitMutation();
        }
    }

    public sealed class BackupCaptureLease(MaintenanceGate gate, DateTimeOffset startedAt) : IDisposable
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private int _disposed;
        public DateTimeOffset StartedAt { get; } = startedAt;
        public TimeSpan Elapsed => _watch.Elapsed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) gate.Reopen();
        }
    }
}

public sealed record MutationAdmission(bool Allowed, IDisposable? Lease)
{
    public static MutationAdmission Accepted(IDisposable lease) => new(true, lease);
    public static MutationAdmission Rejected() => new(false, null);
}
