namespace ProphetOps.Api;

public interface IPostgresProcessRunner
{
    Task<int> GetClientMajorVersionAsync(
        CancellationToken cancellationToken);

    Task<ProcessExecutionResult> RunDumpAsync(
        PostgresDumpParameters parameters,
        CancellationToken cancellationToken);

    Task<ProcessExecutionResult> RunRestoreListAsync(
        string dumpPath,
        CancellationToken cancellationToken);

    Task<ProcessExecutionResult> RunRestoreAsync(
        PostgresRestoreParameters parameters,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

public sealed record PostgresDumpParameters(
    string Host,
    int Port,
    string Database,
    string Username,
    string? Password,
    string Schema,
    string SnapshotId,
    string OutputFilePath,
    string? SslMode = null,
    string? SslRootCert = null,
    TimeSpan? Timeout = null,
    string? StagingPath = null,
    long? StagingMaxBytes = null);

public sealed record PostgresRestoreParameters(
    string Host,
    int Port,
    string Database,
    string Username,
    string? Password,
    string Schema,
    string DumpFilePath,
    string? SslMode = null,
    string? SslRootCert = null,
    TimeSpan? Timeout = null,
    string? StagingPath = null,
    long? StagingMaxBytes = null);

public sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);
