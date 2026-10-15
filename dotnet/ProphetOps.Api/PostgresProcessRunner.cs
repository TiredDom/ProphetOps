using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ProphetOps.Api;

public sealed class PostgresProcessRunner(
    IConfiguration configuration,
    ILogger<PostgresProcessRunner> log) : IPostgresProcessRunner
{
    public const int MaxOutputLength = 65536;

    public async Task<int> GetClientMajorVersionAsync(CancellationToken cancellationToken)
    {
        var executable = ResolveExecutable("pg_dump");
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--version");
        IsolatePostgresEnvironment(psi);

        var result = await ExecuteProcessAsync(psi, TimeSpan.FromSeconds(30), null, null, null, cancellationToken);
        if (result.ExitCode != 0)
        {
            var category = CategorizeError(result.StandardError, result.ExitCode);
            throw new InvalidOperationException($"Failed to determine pg_dump version (exit code {result.ExitCode}). Category: {category}.");
        }

        return ParseVersion(result.StandardOutput);
    }

    public static int ParseVersion(string versionOutput)
    {
        if (string.IsNullOrWhiteSpace(versionOutput))
            throw new InvalidOperationException("pg_dump version output was empty.");

        // Matches formats like:
        // "pg_dump (PostgreSQL) 18.0"
        // "pg_dump (PostgreSQL) 16.3 (Ubuntu 16.3-1ubuntu1)"
        // "pg_dump 17.2"
        var match = Regex.Match(versionOutput, @"(?:pg_dump\s+(?:\(PostgreSQL\)\s+)?)(\d+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major) || major <= 0)
        {
            match = Regex.Match(versionOutput, @"\b(\d+)\.\d+\b");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out major) || major <= 0)
            {
                throw new InvalidOperationException("Failed to parse PostgreSQL client major version from version output.");
            }
        }

        return major;
    }

    public static string MapSslModeToLibPq(SslMode sslMode) => sslMode switch
    {
        SslMode.Disable => "disable",
        SslMode.Allow => "allow",
        SslMode.Prefer => "prefer",
        SslMode.Require => "require",
        SslMode.VerifyCA => "verify-ca",
        SslMode.VerifyFull => "verify-full",
        _ => throw new ArgumentOutOfRangeException(nameof(sslMode), $"Unsupported Npgsql SslMode: {sslMode}")
    };

    public static void IsolatePostgresEnvironment(ProcessStartInfo psi)
    {
        var pgKeys = psi.Environment.Keys
            .Where(k => k.StartsWith("PG", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in pgKeys)
        {
            psi.Environment.Remove(key);
        }
    }

    public static string CategorizeError(string? stderr, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return $"Process exited with code {exitCode}";

        var text = stderr.ToLowerInvariant();
        if (text.Contains("password authentication failed") || text.Contains("authentication failed"))
            return "Authentication failure";
        if (text.Contains("connection refused") || text.Contains("could not connect") || text.Contains("timeout") || text.Contains("no route to host"))
            return "Connection or network failure";
        if (text.Contains("does not exist") || text.Contains("not found") || text.Contains("schema") && text.Contains("missing"))
            return "Database object not found";
        if (text.Contains("permission denied") || text.Contains("must be member") || text.Contains("access denied"))
            return "Permission denied";
        if (text.Contains("archive is corrupt") || text.Contains("corrupt") || text.Contains("invalid header") || text.Contains("unsupported version") || text.Contains("not a valid archive"))
            return "Archive format or integrity failure";
        if (text.Contains("no space left") || text.Contains("quota exceeded") || text.Contains("disk full"))
            return "Disk space or quota failure";

        return $"Process exited with code {exitCode}";
    }

    public async Task<ProcessExecutionResult> RunDumpAsync(
        PostgresDumpParameters parameters,
        CancellationToken cancellationToken)
    {
        if (parameters.StagingMaxBytes.HasValue && parameters.StagingMaxBytes.Value <= 0)
        {
            throw new InvalidOperationException("Backup staging quota must be a positive number of bytes.");
        }

        log.LogInformation("Invoking pg_dump for schema '{Schema}' with snapshot '{SnapshotId}'", parameters.Schema, parameters.SnapshotId);
        var executable = ResolveExecutable("pg_dump");
        var psi = BuildDumpProcessStartInfo(executable, parameters);

        return await ExecuteProcessAsync(psi, parameters.Timeout ?? TimeSpan.FromMinutes(5),
            parameters.StagingPath, parameters.StagingMaxBytes, parameters.Password, cancellationToken);
    }

    public static ProcessStartInfo BuildDumpProcessStartInfo(string executable, PostgresDumpParameters parameters)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-h");
        psi.ArgumentList.Add(parameters.Host);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(parameters.Port.ToString());
        psi.ArgumentList.Add("-U");
        psi.ArgumentList.Add(parameters.Username);
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(parameters.Database);
        psi.ArgumentList.Add("-n");
        psi.ArgumentList.Add(parameters.Schema);
        psi.ArgumentList.Add("-Fc");
        psi.ArgumentList.Add("-O");
        psi.ArgumentList.Add("-x");
        psi.ArgumentList.Add($"--snapshot={parameters.SnapshotId}");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(parameters.OutputFilePath);

        IsolatePostgresEnvironment(psi);

        if (!string.IsNullOrEmpty(parameters.Password))
            psi.Environment["PGPASSWORD"] = parameters.Password;
        if (!string.IsNullOrEmpty(parameters.SslMode))
            psi.Environment["PGSSLMODE"] = parameters.SslMode;
        if (!string.IsNullOrEmpty(parameters.SslRootCert))
            psi.Environment["PGSSLROOTCERT"] = parameters.SslRootCert;

        return psi;
    }

    public async Task<ProcessExecutionResult> RunRestoreListAsync(
        string dumpPath,
        CancellationToken cancellationToken)
    {
        var executable = ResolveExecutable("pg_restore");
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(dumpPath);
        IsolatePostgresEnvironment(psi);

        return await ExecuteProcessAsync(psi, TimeSpan.FromMinutes(2), null, null, null, cancellationToken);
    }

    public async Task<ProcessExecutionResult> RunRestoreAsync(
        PostgresRestoreParameters parameters,
        CancellationToken cancellationToken)
    {
        if (parameters.StagingMaxBytes.HasValue && parameters.StagingMaxBytes.Value <= 0)
        {
            throw new InvalidOperationException("Backup staging quota must be a positive number of bytes.");
        }

        log.LogInformation("Invoking pg_restore for schema '{Schema}' from '{DumpFilePath}'", parameters.Schema, parameters.DumpFilePath);
        var executable = ResolveExecutable("pg_restore");
        var psi = BuildRestoreProcessStartInfo(executable, parameters);

        return await ExecuteProcessAsync(psi, parameters.Timeout ?? TimeSpan.FromMinutes(5),
            parameters.StagingPath, parameters.StagingMaxBytes, parameters.Password, cancellationToken);
    }

    public static ProcessStartInfo BuildRestoreProcessStartInfo(string executable, PostgresRestoreParameters parameters)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-h");
        psi.ArgumentList.Add(parameters.Host);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(parameters.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-U");
        psi.ArgumentList.Add(parameters.Username);
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(parameters.Database);
        psi.ArgumentList.Add("-n");
        psi.ArgumentList.Add(parameters.Schema);
        psi.ArgumentList.Add("--single-transaction");
        psi.ArgumentList.Add("--exit-on-error");
        psi.ArgumentList.Add("--no-owner");
        psi.ArgumentList.Add("--no-privileges");
        psi.ArgumentList.Add(parameters.DumpFilePath);

        IsolatePostgresEnvironment(psi);

        if (!string.IsNullOrEmpty(parameters.Password))
            psi.Environment["PGPASSWORD"] = parameters.Password;
        if (!string.IsNullOrEmpty(parameters.SslMode))
            psi.Environment["PGSSLMODE"] = parameters.SslMode;
        if (!string.IsNullOrEmpty(parameters.SslRootCert))
            psi.Environment["PGSSLROOTCERT"] = parameters.SslRootCert;

        return psi;
    }

    public string ResolveExecutable(string toolName)
    {
        var configuredDir = configuration["Backup:Postgres:ClientToolsPath"];
        var extensions = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat" } : new[] { "" };
        if (!string.IsNullOrWhiteSpace(configuredDir))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(configuredDir, toolName + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, toolName + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }

        throw new FileNotFoundException($"The '{toolName}' executable was not found. Please install postgresql-client-18 or configure Backup:Postgres:ClientToolsPath.");
    }

    public async Task<ProcessExecutionResult> ExecuteProcessAsync(
        ProcessStartInfo psi,
        TimeSpan timeout,
        string? stagingPath,
        long? stagingMaxBytes,
        string? secretToMask,
        CancellationToken cancellationToken)
    {
        if (stagingMaxBytes.HasValue && stagingMaxBytes.Value <= 0)
        {
            throw new InvalidOperationException("Backup staging quota must be a positive number of bytes.");
        }

        using var process = new Process { StartInfo = psi };

        var toolName = Sanitize(Path.GetFileName(psi.FileName), secretToMask);
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process '{toolName}'.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Do not attach inner exception to avoid disclosure via ex.ToString()
            throw new InvalidOperationException($"Failed to launch PostgreSQL tool '{toolName}'. Ensure client tools are installed and accessible.");
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(executionCts.Token);

        var stdoutTask = ReadStreamBoundedAsync(process.StandardOutput, MaxOutputLength, executionCts.Token);
        var stderrTask = ReadStreamBoundedAsync(process.StandardError, MaxOutputLength, executionCts.Token);

        Task quotaMonitor = Task.CompletedTask;
        if (!string.IsNullOrWhiteSpace(stagingPath) && stagingMaxBytes.HasValue && stagingMaxBytes.Value > 0)
        {
            quotaMonitor = Task.Run(async () =>
            {
                while (!monitorCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, monitorCts.Token).ConfigureAwait(false);
                    if (Directory.Exists(stagingPath))
                    {
                        var currentSize = CalculateDirectorySize(stagingPath);
                        if (currentSize > stagingMaxBytes.Value)
                        {
                            try { process.Kill(entireProcessTree: true); } catch { }
                            throw new InvalidOperationException(
                                $"Backup staging hard quota exceeded: staging directory size ({currentSize} bytes) exceeded configured limit of {stagingMaxBytes.Value} bytes.");
                        }
                    }
                }
            }, monitorCts.Token);
        }

        try
        {
            var exitTask = process.WaitForExitAsync(executionCts.Token);
            var completed = await Task.WhenAny(exitTask, quotaMonitor).ConfigureAwait(false);

            if (completed == quotaMonitor && quotaMonitor.IsFaulted)
            {
                // Monitor faulted (e.g. quota exceeded). Promptly kill execution and reap.
                try { process.Kill(entireProcessTree: true); } catch { }
                try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }

                monitorCts.Cancel();
                try { await quotaMonitor.ConfigureAwait(false); } catch { }
                try { await stdoutTask.ConfigureAwait(false); } catch { }
                try { await stderrTask.ConfigureAwait(false); } catch { }

                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(quotaMonitor.Exception!.GetBaseException()).Throw();
            }

            await exitTask.ConfigureAwait(false);

            // Process has exited. Promptly stop monitor.
            monitorCts.Cancel();
            try
            {
                await quotaMonitor.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (quotaMonitor.IsFaulted)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(quotaMonitor.Exception!.GetBaseException()).Throw();
                }
            }

            // Allow readers to drain to EOF on normal exit under an appropriate completion timeout (5 seconds)
            var drainTimeout = TimeSpan.FromSeconds(5);
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(drainTimeout, executionCts.Token).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            var sanitizedStdout = Sanitize(stdout, secretToMask);
            var sanitizedStderr = Sanitize(stderr, secretToMask);

            return new ProcessExecutionResult(process.ExitCode, sanitizedStdout, sanitizedStderr, TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }

            monitorCts.Cancel();
            try { await quotaMonitor.ConfigureAwait(false); } catch { }
            try { await stdoutTask.ConfigureAwait(false); } catch { }
            try { await stderrTask.ConfigureAwait(false); } catch { }

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return new ProcessExecutionResult(-1, "", $"Process '{Path.GetFileName(psi.FileName)}' timed out after {timeout.TotalSeconds} seconds.", TimedOut: true);
            }
            throw;
        }
        catch
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }

            monitorCts.Cancel();
            try { await quotaMonitor.ConfigureAwait(false); } catch { }
            try { await stdoutTask.ConfigureAwait(false); } catch { }
            try { await stderrTask.ConfigureAwait(false); } catch { }

            throw;
        }
    }

    public static async Task<string> ReadStreamBoundedAsync(
        StreamReader reader,
        int maxChars,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var sb = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (sb.Length < maxChars)
            {
                var remaining = maxChars - sb.Length;
                var toAppend = Math.Min(read, remaining);
                sb.Append(buffer, 0, toAppend);
            }
        }
        return sb.ToString();
    }

    public static string Sanitize(string output, string? secretToMask)
    {
        if (string.IsNullOrEmpty(output)) return "";
        var sanitized = output;
        if (!string.IsNullOrWhiteSpace(secretToMask))
        {
            sanitized = sanitized.Replace(secretToMask, "[REDACTED]");
        }
        return sanitized;
    }

    public static long CalculateDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            long total = 0;
            foreach (var file in files)
            {
                total += new FileInfo(file).Length;
            }
            return total;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to determine staging directory size at '{path}': {ex.Message}", ex);
        }
    }
}
