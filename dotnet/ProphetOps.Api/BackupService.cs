namespace ProphetOps.Api;

public sealed record BackupScheduleOptions(bool Enabled)
{
    public static BackupScheduleOptions FromConfiguration(IConfiguration configuration) =>
        new(configuration.GetValue("Backup:Scheduled:Enabled", true));
}

/// Writes full backup packages on a timer.
public sealed class BackupService : BackgroundService
{
    /// Long enough for migrations and seeding to finish before the first copy is taken.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BackupService> _log;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;

    public BackupService(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<BackupService> log)
    {
        _scopes = scopes;
        _log = log;
        _enabled = BackupScheduleOptions.FromConfiguration(configuration).Enabled;
        _interval = TimeSpan.FromHours(Math.Max(1, configuration.GetValue("Backup:IntervalHours", 24)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled) return;

        try
        {
            await Task.Delay(SettleDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Backup(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down. Not a fault.
        }
    }

    private async Task Backup(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<BackupPackageWriter>();
            var result = await writer.RunAsync(cancellationToken);
            if (!result.Success) _log.LogError("Backup package failed: {Reason}", result.Error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or BackupStorageUnavailable)
        {
            _log.LogError(ex, "Backup failed. The application continues and will try again next time.");
        }
    }
}
