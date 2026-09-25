using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProphetOps.Api;
using ProphetOps.Data;
using ProphetOps.Domain;
using Xunit;

namespace ProphetOps.Api.Tests;

public sealed class OperatorRestoreScriptTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "prophetops-operator-restore-" + Guid.NewGuid().ToString("N"));

    public OperatorRestoreScriptTests() => Directory.CreateDirectory(_root);

    [OperatorRestoreFact]
    public async Task Restore_script_restores_encrypted_fixture_and_rejects_corruption_and_traversal_before_activation()
    {
        var pwsh = PowerShellPath();
        Assert.True(File.Exists(pwsh) || CommandExists(pwsh), "PowerShell 7 must be available for operator restore tests.");
        Assert.True(CommandExists("sqlite3"), "sqlite3 must be available for operator restore tests.");

        var key = Convert.FromBase64String(TestKey());
        var keyFile = Path.Combine(_root, "restore.key");
        await File.WriteAllTextAsync(keyFile, TestKey());
        var valid = await CreateEncryptedPackage("valid", key);

        var destination = Path.Combine(_root, "restore");
        Directory.CreateDirectory(destination);
        var ok = await RunRestore(pwsh, valid, destination, keyFile);

        Assert.Equal(0, ok.ExitCode);
        Assert.True(File.Exists(Path.Combine(destination, "prophetops.db")));
        Assert.Contains("Restore staged successfully", ok.Output, StringComparison.OrdinalIgnoreCase);

        var corruptDestination = Path.Combine(_root, "corrupt-destination");
        Directory.CreateDirectory(corruptDestination);
        var corrupt = await CreateEncryptedPackage("corrupt", key, PackageMutation.CorruptDatabaseAfterManifest);
        var corruptResult = await RunRestore(pwsh, corrupt, corruptDestination, keyFile);

        Assert.NotEqual(0, corruptResult.ExitCode);
        Assert.Empty(Directory.GetFileSystemEntries(corruptDestination));

        var traversalDestination = Path.Combine(_root, "traversal-destination");
        Directory.CreateDirectory(traversalDestination);
        var traversal = await CreateEncryptedPackage("traversal", key, PackageMutation.TraversalEntry);
        var traversalResult = await RunRestore(pwsh, traversal, traversalDestination, keyFile);

        Assert.NotEqual(0, traversalResult.ExitCode);
        Assert.Empty(Directory.GetFileSystemEntries(traversalDestination));
        Assert.False(File.Exists(Path.Combine(_root, "escape.txt")));
    }

    private async Task<string> CreateEncryptedPackage(string name, byte[] key, PackageMutation mutation = PackageMutation.None)
    {
        var packageRoot = Path.Combine(_root, name + "-package");
        var databaseDir = Path.Combine(packageRoot, "database");
        Directory.CreateDirectory(databaseDir);
        var dbPath = Path.Combine(databaseDir, "prophetops.db");
        var migrations = await CreateDatabase(dbPath);
        var hash = await BackupPackageWriter.Sha256File(dbPath, CancellationToken.None);
        var size = new FileInfo(dbPath).Length;
        var manifest = new BackupManifest(
            BackupManifest.CurrentSchemaVersion,
            "operator-restore-" + name,
            DateTimeOffset.UtcNow,
            "ProphetOps",
            "Testing",
            "restore-invalidates-sessions-by-default",
            new BackupEncryptionManifest(BackupEncryptionSettings.Algorithm, "operator-test", "1"),
            new BackupDatabaseManifest("database/prophetops.db", size, hash, Integrity(dbPath)),
            [new BackupFileManifest("database/prophetops.db", "sqlite-database", size, hash, File.GetLastWriteTimeUtc(dbPath))],
            new BackupCounts(1, 0, 0, 0, 0, 0),
            [],
            migrations);
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "manifest.json"),
            JsonSerializer.Serialize(manifest, BackupJsonContext.Default.BackupManifest));

        if (mutation == PackageMutation.CorruptDatabaseAfterManifest)
            await File.AppendAllTextAsync(dbPath, "corrupt-after-manifest");

        var zipPath = Path.Combine(_root, name + BackupEncryptionSettings.ZipExtension);
        ZipFile.CreateFromDirectory(packageRoot, zipPath);
        if (mutation == PackageMutation.TraversalEntry)
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            var entry = archive.CreateEntry("../escape.txt");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("escape");
        }

        var encrypted = Path.Combine(_root, name + BackupEncryptionSettings.EnvelopeExtension);
        await BackupEncryptedEnvelope.EncryptFile(zipPath, encrypted, key, CancellationToken.None);
        return encrypted;
    }

    private static async Task<IReadOnlyList<string>> CreateDatabase(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString())
            .Options;
        IReadOnlyList<string> migrations;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Name = "Restore Owner",
                Email = "restore-owner@agency.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("restore-owner-password-581!"),
                Role = Roles.OwnerManagement,
                Status = "Active",
            });
            await db.SaveChangesAsync();
            migrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        }
        SqliteConnection.ClearAllPools();
        return migrations;
    }

    private async Task<(int ExitCode, string Output)> RunRestore(string pwsh, string package, string destination, string keyFile)
    {
        var script = RestoreScriptPath();
        var start = new ProcessStartInfo(pwsh)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-PackagePath");
        start.ArgumentList.Add(package);
        start.ArgumentList.Add("-DestinationRoot");
        start.ArgumentList.Add(destination);
        start.ArgumentList.Add("-DecryptionKeyFile");
        start.ArgumentList.Add(keyFile);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RestoreScriptPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "dotnet", "scripts", "restore-backup.ps1");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("restore-backup.ps1 was not found.");
    }

    private static bool OperatorRestoreEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_OPERATOR_RESTORE_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    private static string PowerShellPath()
    {
        var configured = Environment.GetEnvironmentVariable("PWSH_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return "pwsh";
    }

    private static bool CommandExists(string command)
    {
        if (File.Exists(command)) return true;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => extensions.Any(extension => File.Exists(Path.Combine(directory, command + extension))));
    }

    private static string Integrity(string dbPath)
    {
        string result;
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            result = command.ExecuteScalar() as string ?? "unknown";
        }
        SqliteConnection.ClearAllPools();
        return result;
    }

    private static string TestKey() => Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private enum PackageMutation
    {
        None,
        CorruptDatabaseAfterManifest,
        TraversalEntry,
    }

    private sealed class OperatorRestoreFactAttribute : FactAttribute
    {
        public OperatorRestoreFactAttribute()
        {
            if (!OperatorRestoreEnabled())
                Skip = "Set RUN_OPERATOR_RESTORE_TESTS=true to exercise restore-backup.ps1 with pwsh and sqlite3.";
        }
    }
}
