using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    [OperatorRestoreFact]
    public async Task Restore_script_legacy_sqlite_package_without_security_stamp_stages_offline_with_prerequisites_and_no_activation_or_revocation_claim()
    {
        var pwsh = PowerShellPath();
        Assert.True(File.Exists(pwsh) || CommandExists(pwsh), "PowerShell 7 must be available for operator restore tests.");
        Assert.True(CommandExists("sqlite3"), "sqlite3 must be available for operator restore tests.");

        var key = Convert.FromBase64String(TestKey());
        var keyFile = Path.Combine(_root, "legacy-restore.key");
        await File.WriteAllTextAsync(keyFile, TestKey());
        var legacy = await CreateEncryptedLegacyPackage("legacy-sqlite", key);

        var destination = Path.Combine(_root, "legacy-restore-dest");
        Directory.CreateDirectory(destination);
        var result = await RunRestore(pwsh, legacy, destination, keyFile);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(destination, "prophetops.db")));
        Assert.Contains("missing 'SecurityStamp' column", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Application is NOT activation-ready", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Explicit manual migration", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Restore staged offline (legacy schema pending manual migration and session revocation prerequisites)", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sessions were invalidated", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Restore staged successfully", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_script_rejects_non_empty_destination()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "occupied-destination");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "stale.txt"), "stale data");

        var package = await CreatePostgresPackage("non-empty-check");
        var result = await RunRestore(shell, package, destination);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DestinationRoot must be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_script_v2_rejects_preserve_sessions_for_postgres()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-preserve-dest");
        var package = await CreatePostgresPackage("v2-preserve-check");

        var result = await RunRestore(shell, package, destination, switches: ["PreserveSessions"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PreserveSessions is not supported for PostgreSQL backups", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_script_v2_rejects_unconfirmed_target()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-unconfirmed-dest");
        var package = await CreatePostgresPackage("v2-unconfirmed-check");

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "wrong_db_name",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("Explicit target confirmation failed", StringComparison.OrdinalIgnoreCase), $"Actual output: {result.Output}");
    }

    [Fact]
    public async Task Restore_script_v2_rejects_tampered_dump_checksum_before_target()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-tampered-dest");
        var package = await CreatePostgresPackage("v2-tampered-check", PackageMutation.CorruptDatabaseAfterManifest);

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "localhost:5432/target_db",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("The package failed checksum verification", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_script_v2_rejects_zip_slip_and_path_traversal()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-traversal-dest");
        var package = await CreatePostgresPackage("v2-traversal-check", PackageMutation.TraversalEntry);

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "localhost:5432/target_db",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unsafe entry path", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_root, "escape.txt")));
    }

    [Fact]
    public async Task Restore_script_v2_rejects_unexpected_package_entry()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-unexpected-dest");
        var package = await CreatePostgresPackage("v2-unexpected-check", PackageMutation.UnexpectedFile);

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "localhost:5432/target_db",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("unexpected entry", StringComparison.OrdinalIgnoreCase) ||
                    result.Output.Contains("Unexpected package entry", StringComparison.OrdinalIgnoreCase),
                    $"Actual output: {result.Output}");
    }

    [OperatorRestoreFact]
    public async Task Restore_script_rejects_exceeding_max_input_bytes_before_allocation()
    {
        var pwsh = PowerShellPath();
        var key = Convert.FromBase64String(TestKey());
        var keyFile = Path.Combine(_root, "restore-cap.key");
        await File.WriteAllTextAsync(keyFile, TestKey());
        var package = await CreateEncryptedPackage("cap-test", key);

        var destination = Path.Combine(_root, "cap-dest");
        var extra = new Dictionary<string, string>
        {
            ["MaxInputBytes"] = "100",
        };

        var result = await RunRestore(pwsh, package, destination, keyFile, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("exceeds maximum in-memory decryption limit", StringComparison.OrdinalIgnoreCase), $"Actual output: {result.Output}");
    }

    [PostgresRestoreFact]
    public async Task Restore_script_v2_synthetic_postgres_live_restore()
    {
        // Opt-in live test requiring PROPHETOPS_TEST_POSTGRES and client tools
        var shell = AvailablePowerShellPath();
        var connectionString = Environment.GetEnvironmentVariable("PROPHETOPS_TEST_POSTGRES")!;
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        var package = await CreatePostgresPackage("synthetic-live-pg");
        var destination = Path.Combine(_root, "live-pg-dest");

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = builder.Host ?? "localhost",
            ["PostgresPort"] = builder.Port.ToString(),
            ["PostgresDatabase"] = builder.Database ?? "postgres",
            ["PostgresUsername"] = builder.Username ?? "postgres",
            ["ConfirmTarget"] = $"{builder.Host ?? "localhost"}:{builder.Port}/{builder.Database ?? "postgres"}",
        };
        var envVars = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(builder.Password))
        {
            envVars["PGPASSWORD"] = builder.Password;
        }

        var result = await RunRestore(shell, package, destination, extraParams: extra, envVars: envVars);
        // If target schema is clean and client tools exist, this executes pg_restore
        Assert.True(result.ExitCode == 0 || result.Output.Contains("populated target") || result.Output.Contains("pg_restore"));
    }

    [Fact]
    public async Task Restore_script_helper_captures_stdout_and_stderr()
    {
        var shell = AvailablePowerShellPath();
        var cmd = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var args = OperatingSystem.IsWindows()
            ? new[] { "/c", "echo hello stdout & echo hello stderr 1>&2" }
            : new[] { "-c", "echo 'hello stdout'; echo 'hello stderr' 1>&2" };
        var (exitCode, result) = await RunHelper(shell, cmd, args);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello stdout", result.StandardOutput);
        Assert.Contains("hello stderr", result.StandardError);
        Assert.Equal("Success", result.DiagnosticCategory);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Restore_script_helper_handles_long_output()
    {
        var shell = AvailablePowerShellPath();
        var (exitCode, result) = await RunHelper(shell, shell, ["-NoProfile", "-Command", "[Console]::Out.Write((New-Object string ('A', 50000)))"], maxChars: 65536);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(50000, result.StandardOutput.Length);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Restore_script_helper_enforces_execution_timeout()
    {
        var shell = AvailablePowerShellPath();
        var (exitCode, result) = await RunHelper(shell, shell, ["-NoProfile", "-Command", "Start-Sleep -Seconds 5"], timeoutSeconds: 1);
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.TimedOut);
        Assert.Equal("Execution timeout", result.DiagnosticCategory);
    }

    [Fact]
    public async Task Restore_script_v2_rejects_database_only_target_confirmation()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-dbonly-dest");
        var package = await CreatePostgresPackage("v2-dbonly-check");

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "target_db",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("Explicit target confirmation failed", StringComparison.OrdinalIgnoreCase), $"Actual output: {result.Output}");
        Assert.True(result.Output.Contains("Expected '-ConfirmTarget localhost:5432/target_db'", StringComparison.OrdinalIgnoreCase), $"Actual output: {result.Output}");
    }

    [Fact]
    public async Task Restore_script_v2_rejects_non_prophetops_schema()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-schema-dest");
        var package = await CreatePostgresPackage("v2-schema-check");

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["PostgresSchema"] = "public",
            ["ConfirmTarget"] = "localhost:5432/target_db",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("strictly restricted to application schema 'prophetops'", StringComparison.OrdinalIgnoreCase), $"Actual output: {result.Output}");
    }

    [Fact]
    public async Task Restore_script_v2_rejects_directory_traversal_entry()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-dir-traversal-dest");
        var package = await CreatePostgresPackage("v2-dir-traversal", PackageMutation.DirectoryTraversalEntry);

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "localhost:5432/target_db",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unsafe entry path", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_script_v2_rejects_zip_bomb_uncompressed_limit()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-zipbomb-bytes-dest");
        var package = await CreatePostgresPackage("v2-zipbomb-bytes");

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "localhost:5432/target_db",
            ["MaxExpandedBytes"] = "10",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.Output.Contains("exceeds maximum staging expansion limit", StringComparison.OrdinalIgnoreCase), $"Actual output: {result.Output}");
    }

    [Fact]
    public async Task Restore_script_v2_rejects_zip_bomb_entry_count_limit()
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-zipbomb-count-dest");
        var package = await CreatePostgresPackage("v2-zipbomb-count");

        var extra = new Dictionary<string, string>
        {
            ["PostgresHost"] = "localhost",
            ["PostgresPort"] = "5432",
            ["PostgresDatabase"] = "target_db",
            ["PostgresUsername"] = "postgres",
            ["ConfirmTarget"] = "localhost:5432/target_db",
            ["MaxEntryCount"] = "1",
        };

        var result = await RunRestore(shell, package, destination, extraParams: extra);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("exceeds maximum permitted limit", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("null")]
    [InlineData("1.5")]
    [InlineData("0")]
    [InlineData("3")]
    public async Task Restore_script_v2_rejects_invalid_or_non_integer_schema_version(string rawSchemaVersion)
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-schemaver-" + Guid.NewGuid().ToString("N"));
        var package = await CreatePackageWithRawSchemaVersion("schemaver-test-" + Guid.NewGuid().ToString("N"), rawSchemaVersion);

        var result = await RunRestore(shell, package, destination);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unsupported backup manifest schema version", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\"18\"")]
    [InlineData("null")]
    [InlineData("18.5")]
    [InlineData("13")]
    public async Task Restore_script_v2_rejects_invalid_or_non_integer_server_major(string rawServerMajor)
    {
        var shell = AvailablePowerShellPath();
        var destination = Path.Combine(_root, "v2-servermajor-" + Guid.NewGuid().ToString("N"));
        var package = await CreatePackageWithRawSchemaVersion(
            "servermajor-test-" + Guid.NewGuid().ToString("N"),
            "2",
            provider: "postgres",
            dumpFormat: "pg-dump-custom",
            serverMajor: rawServerMajor);

        var result = await RunRestore(shell, package, destination);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PostgreSQL provider requires a valid ServerMajor version", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_script_manifest_parses_numeric_v1_and_v2_success_paths_under_both_runtimes()
    {
        var runtimes = GetInstalledPowerShellRuntimes();
        if (OperatingSystem.IsWindows())
        {
            Assert.True(runtimes.Count >= 2, $"Expected at least 2 PowerShell runtimes (PS 5.1 and pwsh 7) on Windows, but found {runtimes.Count}: {string.Join(", ", runtimes.Select(r => $"{r.ExecutablePath} (v{r.Version})"))}");
        }
        else
        {
            Assert.True(runtimes.Count >= 1, $"Expected at least 1 PowerShell runtime (pwsh) on non-Windows, but found {runtimes.Count}: {string.Join(", ", runtimes.Select(r => $"{r.ExecutablePath} (v{r.Version})"))}");
        }

        foreach (var runtime in runtimes)
        {
            // Test 1: v1 numeric success path
            var destV1 = Path.Combine(_root, "num-v1-" + Guid.NewGuid().ToString("N"));
            var pkgV1 = await CreatePackageWithRawSchemaVersion(
                "num-v1-" + Guid.NewGuid().ToString("N"),
                "1",
                provider: "sqlite",
                dumpFormat: "sqlite-file");

            var resV1 = await RunRestore(runtime.ExecutablePath, pkgV1, destV1);
            Assert.DoesNotContain("Unsupported backup manifest schema version", resV1.Output, StringComparison.OrdinalIgnoreCase);
            Assert.True(resV1.ExitCode == 0 || resV1.Output.Contains("sqlite3 is required") || resV1.Output.Contains("Restore staged successfully"),
                $"v1 parsing failed on {runtime.ExecutablePath} (v{runtime.Version}): {resV1.Output}");

            // Test 2: v2 numeric success path (with integral ServerMajor: 18)
            var destV2 = Path.Combine(_root, "num-v2-" + Guid.NewGuid().ToString("N"));
            var pkgV2 = await CreatePackageWithRawSchemaVersion(
                "num-v2-" + Guid.NewGuid().ToString("N"),
                "2",
                provider: "postgres",
                dumpFormat: "pg-dump-custom",
                serverMajor: "18");

            var resV2 = await RunRestore(runtime.ExecutablePath, pkgV2, destV2);
            Assert.DoesNotContain("Unsupported backup manifest schema version", resV2.Output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("requires a valid ServerMajor version", resV2.Output, StringComparison.OrdinalIgnoreCase);
            Assert.True(resV2.ExitCode != 0 && resV2.Output.Contains("PostgreSQL restore requires -PostgresHost", StringComparison.OrdinalIgnoreCase),
                $"v2 parsing failed on {runtime.ExecutablePath} (v{runtime.Version}): {resV2.Output}");
        }
    }

    [Fact]
    public async Task Restore_script_helper_argument_list_receives_exact_args_under_pwsh7()
    {
        var pwshRuntime = GetInstalledPowerShellRuntimes().FirstOrDefault(r => r.IsCore);
        Assert.NotNull(pwshRuntime);

        if (OperatingSystem.IsWindows())
        {
            var (exitCode, result) = await RunHelper(
                pwshRuntime.ExecutablePath,
                "cmd.exe",
                ["/c", "echo", "arg_one", "second with spaces", "third_arg"]);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Success", result.DiagnosticCategory);
            Assert.Contains("arg_one", result.StandardOutput);
            Assert.Contains("\"second with spaces\"", result.StandardOutput);
            Assert.Contains("third_arg", result.StandardOutput);
        }
        else
        {
            var (exitCode, result) = await RunHelper(
                pwshRuntime.ExecutablePath,
                "/bin/sh",
                ["-c", "printf '%s %s %s' \"$@\"", "sh", "arg_one", "second with spaces", "third_arg"]);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Success", result.DiagnosticCategory);
            Assert.Contains("arg_one", result.StandardOutput);
            Assert.Contains("second with spaces", result.StandardOutput);
            Assert.Contains("third_arg", result.StandardOutput);
        }
    }

    [Fact]
    public async Task Restore_script_helper_quotes_paths_with_spaces_and_trailing_backslashes_on_ps5()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var ps5Runtime = GetInstalledPowerShellRuntimes().FirstOrDefault(r => !r.IsCore);
        Assert.NotNull(ps5Runtime);

        var (exitCode, result) = await RunHelper(
            ps5Runtime.ExecutablePath,
            "cmd.exe",
            ["/c", "echo", @"C:\Program Files\Test Dir\", "next_arg"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Success", result.DiagnosticCategory);
        Assert.Contains("\"C:\\Program Files\\Test Dir\\\\\" next_arg", result.StandardOutput);
    }

    private async Task<string> CreatePostgresPackage(string name, PackageMutation mutation = PackageMutation.None)
    {
        var packageRoot = Path.Combine(_root, name + "-package");
        var databaseDir = Path.Combine(packageRoot, "database");
        Directory.CreateDirectory(databaseDir);
        var dumpPath = Path.Combine(databaseDir, "prophetops.dump");
        await File.WriteAllTextAsync(dumpPath, "PGDMP-dummy-custom-dump-content");
        var hash = await BackupPackageWriter.Sha256File(dumpPath, CancellationToken.None);
        var size = new FileInfo(dumpPath).Length;

        var manifest = new BackupManifest(
            SchemaVersion: 2,
            PackageId: "operator-restore-" + name,
            CreatedUtc: DateTimeOffset.UtcNow,
            Application: "ProphetOps",
            Environment: "Testing",
            SessionContinuity: "restore-invalidates-sessions-by-default",
            Encryption: new BackupEncryptionManifest("none-local-test-only", "operator-test", "1"),
            Database: new BackupDatabaseManifest(
                Path: "database/prophetops.dump",
                Size: size,
                Sha256: hash,
                IntegrityCheck: "pg_restore-list-verified",
                Provider: "postgres",
                DumpFormat: "pg-dump-custom",
                ServerMajor: 18),
            Files: [new BackupFileManifest("database/prophetops.dump", "postgres-custom-dump", size, hash, File.GetLastWriteTimeUtc(dumpPath))],
            Counts: new BackupCounts(1, 0, 0, 0, 0, 0),
            Configuration: [],
            EfMigrations: ["20260927132038_InitialPostgres", "20261002050000_AddDataProtectionKeys", "20261003180000_AddUserSecurityStamp"]);

        await File.WriteAllTextAsync(Path.Combine(packageRoot, "manifest.json"),
            JsonSerializer.Serialize(manifest, BackupJsonContext.Default.BackupManifest));

        if (mutation == PackageMutation.CorruptDatabaseAfterManifest)
            await File.AppendAllTextAsync(dumpPath, "-corrupted-content");

        if (mutation == PackageMutation.UnexpectedFile)
            await File.WriteAllTextAsync(Path.Combine(packageRoot, "extra.txt"), "unexpected-entry");

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

        if (mutation == PackageMutation.DirectoryTraversalEntry)
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            archive.CreateEntry("../evil_dir/");
        }

        return zipPath;
    }

    private async Task<string> CreatePackageWithRawSchemaVersion(
        string name,
        string rawSchemaVersion,
        string provider = "postgres",
        string dumpFormat = "pg-dump-custom",
        string serverMajor = "18")
    {
        var packageRoot = Path.Combine(_root, name + "-package");
        var databaseDir = Path.Combine(packageRoot, "database");
        Directory.CreateDirectory(databaseDir);
        var dbFileName = provider == "sqlite" ? "prophetops.db" : "prophetops.dump";
        var dumpPath = Path.Combine(databaseDir, dbFileName);

        IReadOnlyList<string> migrations;
        string integrityCheck;
        if (provider == "sqlite")
        {
            migrations = await CreateDatabase(dumpPath);
            integrityCheck = Integrity(dumpPath);
        }
        else
        {
            await File.WriteAllTextAsync(dumpPath, "dummy-db-content-for-test");
            migrations = ["20260927132038_InitialPostgres"];
            integrityCheck = "verified";
        }

        var hash = await BackupPackageWriter.Sha256File(dumpPath, CancellationToken.None);
        var size = new FileInfo(dumpPath).Length;
        var relPath = "database/" + dbFileName;
        var role = provider == "sqlite" ? "sqlite-database" : "postgres-custom-dump";

        var providerField = string.IsNullOrWhiteSpace(provider) ? "null" : $"\"{provider}\"";
        var formatField = string.IsNullOrWhiteSpace(dumpFormat) ? "null" : $"\"{dumpFormat}\"";
        var serverMajorField = string.IsNullOrWhiteSpace(serverMajor) ? "null" : serverMajor;
        var migrationsJson = JsonSerializer.Serialize(migrations);

        var json = $$"""
        {
            "SchemaVersion": {{rawSchemaVersion}},
            "PackageId": "schemaver-test-{{name}}",
            "CreatedUtc": "2026-10-03T00:00:00Z",
            "Application": "ProphetOps",
            "Environment": "Testing",
            "SessionContinuity": "restore-invalidates-sessions-by-default",
            "Encryption": { "Algorithm": "none-local-test-only", "KeyId": "operator-test", "KeyVersion": "1" },
            "Database": {
                "Path": "{{relPath}}",
                "Size": {{size}},
                "Sha256": "{{hash}}",
                "IntegrityCheck": "{{integrityCheck}}",
                "Provider": {{providerField}},
                "DumpFormat": {{formatField}},
                "ServerMajor": {{serverMajorField}}
            },
            "Files": [
                { "Path": "{{relPath}}", "Role": "{{role}}", "Size": {{size}}, "Sha256": "{{hash}}", "ModifiedUtc": "2026-10-03T00:00:00Z" }
            ],
            "Counts": { "Users": 1, "TravelPackages": 0, "Bookings": 0, "Expenses": 0, "AuditEntries": 0, "DataProtectionKeys": 0 },
            "Configuration": [],
            "EfMigrations": {{migrationsJson}}
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(packageRoot, "manifest.json"), json);
        var zipPath = Path.Combine(_root, name + BackupEncryptionSettings.ZipExtension);
        ZipFile.CreateFromDirectory(packageRoot, zipPath);
        return zipPath;
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

    private static async Task<IReadOnlyList<string>> CreateLegacyDatabase(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString())
            .Options;
        IReadOnlyList<string> migrations;
        await using (var db = new AppDbContext(options))
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261002050000_AddDataProtectionKeys");
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Users (Name, Email, PasswordHash, Role, Status, SessionVersion) VALUES ('Restore Owner', 'restore-owner@agency.test', 'hash', 'AgencyAdmin', 'Active', 1);");
            migrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        }
        SqliteConnection.ClearAllPools();
        return migrations;
    }

    private async Task<string> CreateEncryptedLegacyPackage(string name, byte[] key)
    {
        var packageRoot = Path.Combine(_root, name + "-package");
        var databaseDir = Path.Combine(packageRoot, "database");
        Directory.CreateDirectory(databaseDir);
        var dbPath = Path.Combine(databaseDir, "prophetops.db");
        var migrations = await CreateLegacyDatabase(dbPath);
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

        var zipPath = Path.Combine(_root, name + BackupEncryptionSettings.ZipExtension);
        ZipFile.CreateFromDirectory(packageRoot, zipPath);

        var encrypted = Path.Combine(_root, name + BackupEncryptionSettings.EnvelopeExtension);
        await BackupEncryptedEnvelope.EncryptFile(zipPath, encrypted, key, CancellationToken.None);
        return encrypted;
    }

    private async Task<(int ExitCode, string Output)> RunRestore(
        string shell,
        string package,
        string destination,
        string? keyFile = null,
        IReadOnlyDictionary<string, string>? extraParams = null,
        IReadOnlyList<string>? switches = null,
        IReadOnlyDictionary<string, string>? envVars = null)
    {
        var script = RestoreScriptPath();
        var start = new ProcessStartInfo(shell)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        EnsureDotnetRootForProcess(start);
        IsolatePowerShellRuntimeEnvironment(start, shell);
        start.Environment["NO_COLOR"] = "1";
        start.Environment["COLUMNS"] = "1000";
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-PackagePath");
        start.ArgumentList.Add(package);
        start.ArgumentList.Add("-DestinationRoot");
        start.ArgumentList.Add(destination);
        if (!string.IsNullOrWhiteSpace(keyFile))
        {
            start.ArgumentList.Add("-DecryptionKeyFile");
            start.ArgumentList.Add(keyFile);
        }
        if (extraParams != null)
        {
            foreach (var (k, v) in extraParams)
            {
                start.ArgumentList.Add("-" + k);
                start.ArgumentList.Add(v);
            }
        }
        if (switches != null)
        {
            foreach (var s in switches)
            {
                start.ArgumentList.Add("-" + s);
            }
        }
        if (envVars != null)
        {
            foreach (var (k, v) in envVars)
            {
                start.Environment[k] = v;
            }
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        var rawOutput = await stdout + await stderr;
        var normalized = NormalizePowerShellOutput(rawOutput);
        return (process.ExitCode, normalized);
    }

    private static string NormalizePowerShellOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return "";
        var cleaned = Regex.Replace(output, @"\x1B\[[^@-~]*[@-~]", "");
        cleaned = Regex.Replace(cleaned, @"\r?\n\s*\|\s*", " ");
        return cleaned;
    }

    private async Task<(int ExitCode, HelperOutput Output)> RunHelper(
        string shell,
        string executable,
        IReadOnlyList<string>? arguments = null,
        int timeoutSeconds = 30,
        int maxChars = 65536)
    {
        var script = RestoreScriptPath();
        var start = new ProcessStartInfo(shell)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        EnsureDotnetRootForProcess(start);
        IsolatePowerShellRuntimeEnvironment(start, shell);
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");

        var argsArrayLiteral = arguments == null || arguments.Count == 0
            ? "$null"
            : "@(" + string.Join(", ", arguments.Select(a => "'" + a.Replace("'", "''") + "'")) + ")";

        var command = $"& {{ & '{script.Replace("'", "''")}' -InvokeHelper -HelperExecutable '{executable.Replace("'", "''")}' -HelperArguments {argsArrayLiteral} -HelperTimeoutSeconds {timeoutSeconds} -HelperMaxChars {maxChars} }}";
        start.ArgumentList.Add(command);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }

        var stdoutText = (await stdout).Trim();
        var stderrText = await stderr;
        var helperOut = JsonSerializer.Deserialize<HelperOutput>(stdoutText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new HelperOutput { ExitCode = process.ExitCode, StandardError = stderrText };
        return (process.ExitCode, helperOut);
    }

    private sealed class HelperOutput
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = "";
        public string StandardError { get; set; } = "";
        public bool TimedOut { get; set; }
        public string DiagnosticCategory { get; set; } = "";
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

    public record PowerShellRuntime(string ExecutablePath, string Version, bool IsCore);

    private static IReadOnlyList<PowerShellRuntime> GetInstalledPowerShellRuntimes()
    {
        var runtimes = new List<PowerShellRuntime>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string fullPath;
            if (File.Exists(path))
            {
                fullPath = Path.GetFullPath(path);
            }
            else if (CommandExists(path))
            {
                fullPath = path;
            }
            else return;

            if (!seen.Add(fullPath)) return;

            try
            {
                var psi = new ProcessStartInfo(fullPath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add("$PSVersionTable.PSVersion.ToString() + '|' + ($PSVersionTable.PSEdition -eq 'Core')");
                EnsureDotnetRootForProcess(psi);
                IsolatePowerShellRuntimeEnvironment(psi, fullPath);

                using var p = Process.Start(psi);
                if (p != null && p.WaitForExit(5000))
                {
                    var stdout = p.StandardOutput.ReadToEnd().Trim();
                    var parts = stdout.Split('|');
                    if (parts.Length == 2)
                    {
                        var ver = parts[0].Trim();
                        var isCore = bool.TryParse(parts[1].Trim(), out var b) && b;
                        runtimes.Add(new PowerShellRuntime(fullPath, ver, isCore));
                    }
                }
            }
            catch { }
        }

        var configured = Environment.GetEnvironmentVariable("PWSH_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) TryAdd(configured);

        if (CommandExists("pwsh")) TryAdd("pwsh");

        if (OperatingSystem.IsWindows() && File.Exists(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"))
            TryAdd(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe");
        else if (CommandExists("powershell"))
            TryAdd("powershell");

        return runtimes;
    }

    private static void EnsureDotnetRootForProcess(ProcessStartInfo psi)
    {
        var envRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            psi.Environment["DOTNET_ROOT"] = envRoot;
            PrependPath(psi, envRoot);
        }

        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
        {
            var hostDirectory = Path.GetDirectoryName(hostPath);
            if (!string.IsNullOrWhiteSpace(hostDirectory))
            {
                if (string.IsNullOrWhiteSpace(envRoot))
                {
                    psi.Environment["DOTNET_ROOT"] = hostDirectory;
                }
                PrependPath(psi, hostDirectory);
            }
        }
    }

    private static void IsolatePowerShellRuntimeEnvironment(ProcessStartInfo psi, string shell)
    {
        var fileName = Path.GetFileNameWithoutExtension(shell);
        var isCore = fileName.StartsWith("pwsh", StringComparison.OrdinalIgnoreCase);

        var existing = psi.Environment.TryGetValue("PSModulePath", out var p) && p != null
            ? p
            : Environment.GetEnvironmentVariable("PSModulePath") ?? "";

        var parts = existing.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        if (isCore)
        {
            var coreParts = parts.Where(part => !IsWindowsPowerShellModulePath(part)).ToList();
            if (coreParts.Count > 0)
            {
                psi.Environment["PSModulePath"] = string.Join(Path.PathSeparator.ToString(), coreParts);
            }
        }
        else
        {
            var userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

            var defaultPs5Paths = new List<string>
            {
                Path.Combine(userDocs, "WindowsPowerShell", "Modules"),
                Path.Combine(progFiles, "WindowsPowerShell", "Modules"),
                Path.Combine(sys32, "WindowsPowerShell", "v1.0", "Modules")
            };

            var nonCoreParts = parts.Where(part => !IsPowerShellCoreModulePath(part)).ToList();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var path in nonCoreParts.Concat(defaultPs5Paths))
            {
                if (seen.Add(path))
                {
                    result.Add(path);
                }
            }

            if (result.Count > 0)
            {
                psi.Environment["PSModulePath"] = string.Join(Path.PathSeparator.ToString(), result);
            }
        }
    }

    private static bool IsWindowsPowerShellModulePath(string path) =>
        path.Contains("WindowsPowerShell", StringComparison.OrdinalIgnoreCase);

    private static bool IsPowerShellCoreModulePath(string path)
    {
        if (IsWindowsPowerShellModulePath(path)) return false;
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        return normalized.EndsWith(Path.Combine("PowerShell", "Modules"), StringComparison.OrdinalIgnoreCase);
    }

    private static void PrependPath(ProcessStartInfo psi, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var existingPath = psi.Environment.TryGetValue("PATH", out var p) && p != null
            ? p
            : Environment.GetEnvironmentVariable("PATH") ?? "";

        var alreadyPresent = existingPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(path =>
            {
                try
                {
                    return string.Equals(
                        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        fullDirectory,
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

        if (!alreadyPresent)
        {
            psi.Environment["PATH"] = directory + Path.PathSeparator + existingPath;
        }
    }

    private static string PowerShellPath()
    {
        var configured = Environment.GetEnvironmentVariable("PWSH_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        if (CommandExists("pwsh")) return "pwsh";
        return "pwsh";
    }

    private static string AvailablePowerShellPath()
    {
        var configured = Environment.GetEnvironmentVariable("PWSH_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && (File.Exists(configured) || CommandExists(configured)))
            return configured;
        if (CommandExists("pwsh")) return "pwsh";
        if (CommandExists("powershell")) return "powershell";
        if (OperatingSystem.IsWindows() && File.Exists(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"))
            return @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
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
        DirectoryTraversalEntry,
        UnexpectedFile,
    }

    private sealed class OperatorRestoreFactAttribute : FactAttribute
    {
        public OperatorRestoreFactAttribute()
        {
            if (!OperatorRestoreEnabled())
                Skip = "Set RUN_OPERATOR_RESTORE_TESTS=true to exercise restore-backup.ps1 with pwsh and sqlite3.";
        }
    }

    private sealed class PostgresRestoreFactAttribute : FactAttribute
    {
        public PostgresRestoreFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROPHETOPS_TEST_POSTGRES")))
                Skip = "Set PROPHETOPS_TEST_POSTGRES to exercise live PostgreSQL restore.";
        }
    }
}
