using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Data;

namespace ProphetOps.Api;

/// Copies the database into a backups/ folder on a timer.
///
/// The agency runs this on one office PC. Until now a failed disk took every booking, expense and
/// forecast the business has ever recorded with it, and there was nothing to restore from.
public sealed class BackupService : BackgroundService
{
    private const string Prefix = "prophetops-";
    private const string Extension = ".db";

    /// Long enough for migrations and seeding to finish before the first copy is taken.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BackupService> _log;
    private readonly string _contentRoot;
    private readonly TimeSpan _interval;
    private readonly int _keep;

    public BackupService(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<BackupService> log)
    {
        _scopes = scopes;
        _log = log;
        _contentRoot = environment.ContentRootPath;
        _interval = TimeSpan.FromHours(Math.Max(1, configuration.GetValue("Backup:IntervalHours", 24)));
        _keep = Math.Max(1, configuration.GetValue("Backup:Keep", 7));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(SettleDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                Backup();
                await Task.Delay(_interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down. Not a fault.
        }
    }

    private void Backup()
    {
        try
        {
            var database = DatabasePath();
            if (database is null) return;

            var folder = Path.Combine(Path.GetDirectoryName(database)!, "backups");
            Directory.CreateDirectory(folder);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var finished = Path.Combine(folder, $"{Prefix}{stamp}{Extension}");
            var partial = finished + ".partial";

            File.Delete(partial);
            Copy(database, partial);

            if (!IsIntact(partial))
            {
                File.Delete(partial);
                _log.LogError("Backup discarded: the copy for {Stamp} did not pass its integrity check.", stamp);
                return;
            }

            // The file only takes the name the restore instructions mention once it is complete
            // and checked, so a service killed mid-write cannot leave behind something that
            // reads as a finished backup.
            File.Move(partial, finished, overwrite: true);

            _log.LogInformation(
                "Backup written: {File} ({Kilobytes} KB).", finished, new FileInfo(finished).Length / 1024);

            Sweep(folder);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Backup failed. The application continues and will try again next time.");
        }
    }

    /// SQLite's own backup API rather than File.Copy: the database is in use, and a byte-for-byte
    /// copy can catch a half-finished write or miss commits still sitting in the write-ahead log,
    /// producing a file that only turns out to be corrupt on the day it is needed.
    private static void Copy(string database, string destination)
    {
        using var source = new SqliteConnection(PathOnly(database));
        using var target = new SqliteConnection(PathOnly(destination));

        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    private static bool IsIntact(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

        connection.Open();

        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA integrity_check;";
        return check.ExecuteScalar() as string == "ok";
    }

    private void Sweep(string folder)
    {
        var stale = Directory.GetFiles(folder, $"{Prefix}*{Extension}")
            .Where(f => f.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(_keep)
            .ToList();

        foreach (var file in stale)
        {
            try
            {
                File.Delete(file);
                _log.LogInformation("Backup pruned: {File}.", file);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not delete the old backup {File}.", file);
            }
        }
    }

    private string? DatabasePath()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connection = new SqliteConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
        var source = connection.DataSource;

        if (string.IsNullOrWhiteSpace(source)
            || connection.Mode == SqliteOpenMode.Memory
            || source.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogInformation("Backups skipped: this instance runs on an in-memory database.");
            return null;
        }

        // A Windows Service starts in system32, so a relative path has to be resolved against the
        // application's own folder rather than whatever the working directory happens to be.
        return Path.IsPathRooted(source) ? source : Path.GetFullPath(Path.Combine(_contentRoot, source));
    }

    /// Pooling off on purpose. Closing a pooled connection hands it back to the pool with the file
    /// still open, and the finished copy then cannot be renamed out of its .partial name.
    private static string PathOnly(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
}
