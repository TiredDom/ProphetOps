using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class PostgresRestoreRunnerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "prophetops-pg-restore-tests-" + Guid.NewGuid().ToString("N"));

    public PostgresRestoreRunnerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void BuildRestoreProcessStartInfo_sets_single_transaction_exit_on_error_no_owner_no_privileges_and_schema()
    {
        var parameters = new PostgresRestoreParameters(
            Host: "db.staging.internal",
            Port: 5432,
            Database: "prophetops_staging",
            Username: "operator_user",
            Password: "secret_db_password_123!",
            Schema: "prophetops",
            DumpFilePath: @"C:\staging\dump.dump");

        var psi = PostgresProcessRunner.BuildRestoreProcessStartInfo("pg_restore", parameters);

        Assert.Equal("pg_restore", psi.FileName);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.True(psi.CreateNoWindow);

        // Verify required flags
        Assert.Contains("-h", psi.ArgumentList);
        Assert.Equal("db.staging.internal", psi.ArgumentList[psi.ArgumentList.IndexOf("-h") + 1]);

        Assert.Contains("-p", psi.ArgumentList);
        Assert.Equal("5432", psi.ArgumentList[psi.ArgumentList.IndexOf("-p") + 1]);

        Assert.Contains("-U", psi.ArgumentList);
        Assert.Equal("operator_user", psi.ArgumentList[psi.ArgumentList.IndexOf("-U") + 1]);

        Assert.Contains("-d", psi.ArgumentList);
        Assert.Equal("prophetops_staging", psi.ArgumentList[psi.ArgumentList.IndexOf("-d") + 1]);

        Assert.Contains("-n", psi.ArgumentList);
        Assert.Equal("prophetops", psi.ArgumentList[psi.ArgumentList.IndexOf("-n") + 1]);

        Assert.Contains("--single-transaction", psi.ArgumentList);
        Assert.Contains("--exit-on-error", psi.ArgumentList);
        Assert.Contains("--no-owner", psi.ArgumentList);
        Assert.Contains("--no-privileges", psi.ArgumentList);
        Assert.Contains(@"C:\staging\dump.dump", psi.ArgumentList);

        // Strict refusal of dangerous / destructive flags
        Assert.DoesNotContain("--clean", psi.ArgumentList);
        Assert.DoesNotContain("--if-exists", psi.ArgumentList);
        Assert.DoesNotContain("-c", psi.ArgumentList);

        // Password MUST NOT appear in argument list
        Assert.DoesNotContain("secret_db_password_123!", psi.ArgumentList);

        // Password must be in private process environment
        Assert.True(psi.Environment.ContainsKey("PGPASSWORD"));
        Assert.Equal("secret_db_password_123!", psi.Environment["PGPASSWORD"]);
    }

    [Fact]
    public void BuildRestoreProcessStartInfo_isolates_environment_and_sets_ssl_mode()
    {
        var parameters = new PostgresRestoreParameters(
            Host: "db.staging.internal",
            Port: 5432,
            Database: "prophetops_staging",
            Username: "operator_user",
            Password: null,
            Schema: "prophetops",
            DumpFilePath: @"C:\staging\dump.dump",
            SslMode: "verify-full",
            SslRootCert: @"C:\certs\root.crt");

        var psi = PostgresProcessRunner.BuildRestoreProcessStartInfo("pg_restore", parameters);

        Assert.Equal("verify-full", psi.Environment["PGSSLMODE"]);
        Assert.Equal(@"C:\certs\root.crt", psi.Environment["PGSSLROOTCERT"]);
        Assert.False(psi.Environment.ContainsKey("PGPASSWORD"));
    }

    [Fact]
    public void IsolatePostgresEnvironment_strips_ambient_PG_variables()
    {
        var psi = new ProcessStartInfo();
        psi.Environment["PGHOST"] = "malicious.host";
        psi.Environment["PGUSER"] = "malicious.user";
        psi.Environment["PGPASSWORD"] = "malicious.password";
        psi.Environment["PGPORT"] = "9999";
        psi.Environment["PGDATABASE"] = "malicious.db";
        psi.Environment["OTHER_VAR"] = "keep_me";

        PostgresProcessRunner.IsolatePostgresEnvironment(psi);

        Assert.False(psi.Environment.ContainsKey("PGHOST"));
        Assert.False(psi.Environment.ContainsKey("PGUSER"));
        Assert.False(psi.Environment.ContainsKey("PGPASSWORD"));
        Assert.False(psi.Environment.ContainsKey("PGPORT"));
        Assert.False(psi.Environment.ContainsKey("PGDATABASE"));
        Assert.True(psi.Environment.ContainsKey("OTHER_VAR"));
    }

    [Fact]
    public async Task RunRestoreAsync_enforces_positive_staging_quota_if_specified()
    {
        var config = new ConfigurationBuilder().Build();
        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);

        var parameters = new PostgresRestoreParameters(
            Host: "localhost",
            Port: 5432,
            Database: "test",
            Username: "test",
            Password: null,
            Schema: "prophetops",
            DumpFilePath: "dummy.dump",
            StagingMaxBytes: 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunRestoreAsync(parameters, CancellationToken.None));

        Assert.Contains("Backup staging quota must be a positive number of bytes", ex.Message);
    }

    [Fact]
    public async Task ExecuteProcessAsync_masks_secret_in_stdout_and_stderr()
    {
        var config = new ConfigurationBuilder().Build();
        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);

        var secret = "super_confidential_secret_token";
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c echo secret: {secret} & echo err: {secret} 1>&2")
            : new ProcessStartInfo("sh")
            {
                ArgumentList = { "-c", $"echo \"secret: {secret}\"; echo \"err: {secret}\" >&2" }
            };
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        var result = await runner.ExecuteProcessAsync(psi, TimeSpan.FromSeconds(10), null, null, secret, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(secret, result.StandardOutput);
        Assert.DoesNotContain(secret, result.StandardError);
        Assert.Contains("[REDACTED]", result.StandardOutput);
        Assert.Contains("[REDACTED]", result.StandardError);
    }

    [Fact]
    public async Task ExecuteProcessAsync_kills_and_reaps_process_on_cancellation()
    {
        var config = new ConfigurationBuilder().Build();
        var runner = new PostgresProcessRunner(config, NullLogger<PostgresProcessRunner>.Instance);

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping.exe", "127.0.0.1 -n 30")
            : new ProcessStartInfo("sleep", "30");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.ExecuteProcessAsync(psi, TimeSpan.FromMinutes(1), null, null, null, cts.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }
}
