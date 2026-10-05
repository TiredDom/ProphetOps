[CmdletBinding(DefaultParameterSetName = "Restore")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Restore")]
    [string] $PackagePath,

    [Parameter(Mandatory = $true, ParameterSetName = "Restore")]
    [string] $DestinationRoot,

    [Parameter(ParameterSetName = "Restore")]
    [string] $DecryptionKeyFile,

    [Parameter(ParameterSetName = "Restore")]
    [switch] $PreserveSessions,

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresHost,

    [Parameter(ParameterSetName = "Restore")]
    [int] $PostgresPort = 5432,

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresDatabase,

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresUsername,

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresPasswordFile,

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresSchema = "prophetops",

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresSslMode,

    [Parameter(ParameterSetName = "Restore")]
    [string] $PostgresSslRootCert,

    [Parameter(ParameterSetName = "Restore")]
    [string] $ConfirmTarget,

    [Parameter(ParameterSetName = "Restore")]
    [long] $MaxInputBytes = 33554432,

    [Parameter(ParameterSetName = "Restore")]
    [long] $MaxExpandedBytes = 1073741824,

    [Parameter(ParameterSetName = "Restore")]
    [int] $MaxEntryCount = 10000,

    [Parameter(Mandatory = $true, ParameterSetName = "InvokeHelper")]
    [switch] $InvokeHelper,

    [Parameter(Mandatory = $true, ParameterSetName = "InvokeHelper")]
    [string] $HelperExecutable,

    [Parameter(ParameterSetName = "InvokeHelper")]
    [string[]] $HelperArguments,

    [Parameter(ParameterSetName = "InvokeHelper")]
    [int] $HelperTimeoutSeconds = 30,

    [Parameter(ParameterSetName = "InvokeHelper")]
    [int] $HelperMaxChars = 65536
)

$ErrorActionPreference = "Stop"

# Keep PowerShell Desktop/Core module paths isolated when this script is launched from a different host edition.
$sep = [System.IO.Path]::PathSeparator
$paths = @($env:PSModulePath -split $sep | Where-Object { $_ })
if ($PSVersionTable.PSEdition -eq "Core") {
    $env:PSModulePath = @($paths | Where-Object {
        $_ -notmatch '(?i)[\\/]WindowsPowerShell[\\/]Modules'
    }) -join $sep
} else {
    $sep = [System.IO.Path]::PathSeparator
    $filtered = @($paths | Where-Object {
        $_ -and
        $_ -notmatch '(?i)[\\/]PowerShell[\\/]Modules'
    })
    $sysPsModules = Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\Modules'
    if ($filtered -notcontains $sysPsModules -and (Test-Path -LiteralPath $sysPsModules)) {
        $filtered += $sysPsModules
    }
    $env:PSModulePath = $filtered -join $sep
}

function Test-IsIntegralNumber($val) {
    return ($null -ne $val) -and (
        $val -is [int] -or
        $val -is [long] -or
        $val -is [int16] -or
        $val -is [byte] -or
        $val -is [sbyte] -or
        $val -is [uint16] -or
        $val -is [uint32] -or
        $val -is [uint64]
    )
}

function Get-FileSha256([string] $filePath) {
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Cannot calculate SHA256: file not found: $filePath"
    }
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($filePath)
    try {
        $hashBytes = $hasher.ComputeHash($stream)
        return [BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    } finally {
        $stream.Dispose()
        $hasher.Dispose()
    }
}

function Ensure-SafeProcessRunnerLoaded {
    if (-not ([System.Management.Automation.PSTypeName]'ProphetOps.Restore.SafeProcessRunner').Type) {
        Add-Type -TypeDefinition @"
        using System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.IO;
        using System.Text;
        using System.Threading;
        using System.Threading.Tasks;

        namespace ProphetOps.Restore
        {
            public sealed class ProcessResult
            {
                public int ExitCode { get; set; }
                public string StandardOutput { get; set; }
                public string StandardError { get; set; }
                public bool TimedOut { get; set; }
                public string DiagnosticCategory { get; set; }
            }

            public static class SafeProcessRunner
            {
                public static ProcessResult Run(
                    string executable,
                    string[] arguments,
                    IDictionary<string, string> environment,
                    int timeoutSeconds,
                    int maxChars)
                {
                    var result = new ProcessResult();
                    result.StandardOutput = "";
                    result.StandardError = "";
                    result.DiagnosticCategory = "None";
                    using (var process = new Process())
                    {
                        var psi = process.StartInfo;
                        psi.FileName = executable;
                        psi.UseShellExecute = false;
                        psi.RedirectStandardOutput = true;
                        psi.RedirectStandardError = true;
                        psi.CreateNoWindow = true;

                        var argListProp = typeof(ProcessStartInfo).GetProperty("ArgumentList");
                        if (argListProp != null)
                        {
                            var list = argListProp.GetValue(psi, null) as System.Collections.IList;
                            if (list != null && arguments != null)
                            {
                                for (int i = 0; i < arguments.Length; i++)
                                {
                                    list.Add(arguments[i]);
                                }
                            }
                        }
                        else if (arguments != null)
                        {
                            var sb = new StringBuilder();
                            for (int i = 0; i < arguments.Length; i++)
                            {
                                if (sb.Length > 0) sb.Append(" ");
                                sb.Append(QuoteArgument(arguments[i]));
                            }
                            psi.Arguments = sb.ToString();
                        }

                        var envProp = typeof(ProcessStartInfo).GetProperty("Environment");
                        if (envProp != null)
                        {
                            var env = envProp.GetValue(psi, null) as IDictionary<string, string>;
                            if (env != null)
                            {
                                var keysToRemove = new List<string>();
                                foreach (var key in env.Keys)
                                {
                                    if (key.StartsWith("PG", StringComparison.OrdinalIgnoreCase))
                                    {
                                        keysToRemove.Add(key);
                                    }
                                }
                                for (int i = 0; i < keysToRemove.Count; i++)
                                {
                                    env.Remove(keysToRemove[i]);
                                }
                                if (environment != null)
                                {
                                    foreach (var kvp in environment)
                                    {
                                        env[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                        else
                        {
                            var keysToRemove = new List<string>();
                            foreach (string key in psi.EnvironmentVariables.Keys)
                            {
                                if (key.StartsWith("PG", StringComparison.OrdinalIgnoreCase))
                                {
                                    keysToRemove.Add(key);
                                }
                            }
                            for (int i = 0; i < keysToRemove.Count; i++)
                            {
                                psi.EnvironmentVariables.Remove(keysToRemove[i]);
                            }
                            if (environment != null)
                            {
                                foreach (var kvp in environment)
                                {
                                    psi.EnvironmentVariables[kvp.Key] = kvp.Value;
                                }
                            }
                        }

                        try
                        {
                            if (!process.Start())
                            {
                                result.ExitCode = -1;
                                result.DiagnosticCategory = "Process launch failure";
                                return result;
                            }
                        }
                        catch (Exception)
                        {
                            result.ExitCode = -1;
                            result.DiagnosticCategory = "Process launch failure";
                            return result;
                        }

                        using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                        {
                            var stdoutTask = ReadStreamBoundedAsync(process.StandardOutput, maxChars, timeoutCts.Token);
                            var stderrTask = ReadStreamBoundedAsync(process.StandardError, maxChars, timeoutCts.Token);

                            bool exited = process.WaitForExit(timeoutSeconds * 1000);
                            if (!exited)
                            {
                                result.TimedOut = true;
                                result.ExitCode = -1;
                                result.DiagnosticCategory = "Execution timeout";
                                KillProcessTreeAndReap(process);
                                return result;
                            }

                            var waitReaders = Task.WhenAll(stdoutTask, stderrTask);
                            bool readersFinished = waitReaders.Wait(TimeSpan.FromSeconds(5));
                            if (!readersFinished)
                            {
                                result.TimedOut = true;
                                result.ExitCode = -1;
                                result.DiagnosticCategory = "Reader drain timeout";
                                KillProcessTreeAndReap(process);
                                return result;
                            }

                            result.ExitCode = process.ExitCode;
                            result.StandardOutput = stdoutTask.Result;
                            result.StandardError = stderrTask.Result;
                            result.DiagnosticCategory = CategorizeError(result.StandardError, result.ExitCode);
                            return result;
                        }
                    }
                }

                private static string QuoteArgument(string arg)
                {
                    if (string.IsNullOrEmpty(arg)) return "\"\"";
                    if (!arg.Contains(" ") && !arg.Contains("\t") && !arg.Contains("\"")) return arg;
                    var sb = new StringBuilder();
                    sb.Append('"');
                    int backslashes = 0;
                    for (int i = 0; i < arg.Length; i++)
                    {
                        char c = arg[i];
                        if (c == '\\')
                        {
                            backslashes++;
                        }
                        else if (c == '"')
                        {
                            sb.Append('\\', backslashes * 2 + 1);
                            sb.Append('"');
                            backslashes = 0;
                        }
                        else
                        {
                            sb.Append('\\', backslashes);
                            sb.Append(c);
                            backslashes = 0;
                        }
                    }
                    sb.Append('\\', backslashes * 2);
                    sb.Append('"');
                    return sb.ToString();
                }

                private static void KillProcessTreeAndReap(Process process)
                {
                    try
                    {
                        var killMethod = typeof(Process).GetMethod("Kill", new Type[] { typeof(bool) });
                        if (killMethod != null)
                        {
                            killMethod.Invoke(process, new object[] { true });
                        }
                        else
                        {
                            process.Kill();
                        }
                    }
                    catch { }
                    try { process.WaitForExit(5000); } catch { }
                }

                private static async Task<string> ReadStreamBoundedAsync(StreamReader reader, int maxChars, CancellationToken ct)
                {
                    var buffer = new char[4096];
                    var sb = new StringBuilder();
                    int read;
                    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        if (sb.Length < maxChars)
                        {
                            int remaining = maxChars - sb.Length;
                            int toAppend = Math.Min(read, remaining);
                            sb.Append(buffer, 0, toAppend);
                        }
                    }
                    return sb.ToString();
                }

                public static string CategorizeError(string stderr, int exitCode)
                {
                    if (exitCode == 0) return "Success";
                    if (string.IsNullOrEmpty(stderr)) return "Process exited with code " + exitCode;
                    var text = stderr.ToLowerInvariant();
                    if (text.Contains("password authentication failed") || text.Contains("authentication failed"))
                        return "Authentication failure";
                    if (text.Contains("connection refused") || text.Contains("could not connect") || text.Contains("timeout") || text.Contains("no route to host"))
                        return "Connection or network failure";
                    if (text.Contains("does not exist") || text.Contains("not found") || text.Contains("missing"))
                        return "Database object not found";
                    if (text.Contains("permission denied") || text.Contains("must be member") || text.Contains("access denied"))
                        return "Permission denied";
                    if (text.Contains("corrupt") || text.Contains("invalid header") || text.Contains("not a valid archive") || text.Contains("unsupported version"))
                        return "Archive format or integrity failure";
                    if (text.Contains("no space left") || text.Contains("quota exceeded") || text.Contains("disk full"))
                        return "Disk space or quota failure";
                    return "Process exited with code " + exitCode;
                }
            }
        }
"@
    }
}

function Invoke-SafeProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,
        [string[]] $ArgumentList,
        [System.Collections.Generic.IDictionary[string, string]] $Environment,
        [int] $TimeoutSeconds = 60,
        [int] $MaxChars = 65536
    )
    Ensure-SafeProcessRunnerLoaded
    return [ProphetOps.Restore.SafeProcessRunner]::Run($FilePath, $ArgumentList, $Environment, $TimeoutSeconds, $MaxChars)
}

if ($PSCmdlet.ParameterSetName -eq "InvokeHelper") {
    Ensure-SafeProcessRunnerLoaded
    $res = Invoke-SafeProcess -FilePath $HelperExecutable -ArgumentList $HelperArguments -TimeoutSeconds $HelperTimeoutSeconds -MaxChars $HelperMaxChars
    $outputObj = @{
        ExitCode = $res.ExitCode
        StandardOutput = $res.StandardOutput
        StandardError = $res.StandardError
        TimedOut = $res.TimedOut
        DiagnosticCategory = $res.DiagnosticCategory
    }
    Write-Output (ConvertTo-Json $outputObj)
    if ($res.ExitCode -ne 0) {
        exit $res.ExitCode
    }
    exit 0
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Backup package was not found."
}

if (Test-Path -LiteralPath $DestinationRoot) {
    $existing = Get-ChildItem -LiteralPath $DestinationRoot -Force
    if ($existing.Count -gt 0) {
        throw "DestinationRoot must be empty. Refusing to overwrite a live or non-empty destination."
    }
} else {
    New-Item -ItemType Directory -Path $DestinationRoot | Out-Null
}

$stage = Join-Path ([IO.Path]::GetTempPath()) ("prophetops-restore-" + [Guid]::NewGuid().ToString("N"))
$archivePath = $PackagePath

function Resolve-InStage([string] $Root, [string] $Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw "Manifest paths must be relative." }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    $base = [IO.Path]::GetFullPath($Root)
    if (-not $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escapes the package."
    }
    return $full
}

function Get-RelativePathCompat([string] $BasePath, [string] $FullPath) {
    $baseNorm = $BasePath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($FullPath.StartsWith($baseNorm, [StringComparison]::OrdinalIgnoreCase)) {
        return $FullPath.Substring($baseNorm.Length).Replace('\', '/')
    }
    return $FullPath.Replace('\', '/')
}

if ($PackagePath.EndsWith(".prophetops-backup.pobak", [StringComparison]::OrdinalIgnoreCase)) {
    if ([string]::IsNullOrWhiteSpace($DecryptionKeyFile)) {
        throw "Encrypted backup packages require -DecryptionKeyFile."
    }
    if (-not (Test-Path -LiteralPath $DecryptionKeyFile -PathType Leaf)) {
        throw "Decryption key file was not found."
    }
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw "Encrypted backup restore requires PowerShell 7 or newer for AES-GCM support."
    }
    if ($MaxInputBytes -le 0) {
        throw "MaxInputBytes must be a positive number of bytes."
    }
    $fileInfo = Get-Item -LiteralPath $PackagePath
    if ($fileInfo.Length -gt ($MaxInputBytes + 44)) {
        throw "Backup package size ($($fileInfo.Length) bytes) exceeds maximum in-memory decryption limit ($MaxInputBytes bytes). Increase -MaxInputBytes or verify the package."
    }
    $key = [Convert]::FromBase64String((Get-Content -LiteralPath $DecryptionKeyFile -Raw).Trim())
    if ($key.Length -ne 32) {
        throw "DecryptionKey must decode to exactly 32 bytes."
    }
    $bytes = [IO.File]::ReadAllBytes($PackagePath)
    $magic = [Text.Encoding]::ASCII.GetBytes("POBAK")
    for ($i = 0; $i -lt $magic.Length; $i++) {
        if ($bytes[$i] -ne $magic[$i]) { throw "Unsupported encrypted backup envelope." }
    }
    $offset = $magic.Length
    $version = $bytes[$offset]; $offset++
    if ($version -ne 1) { throw "Unsupported encrypted backup envelope version." }
    $nonceLength = $bytes[$offset]; $offset++
    $tagLength = $bytes[$offset]; $offset++
    if ($nonceLength -ne 12 -or $tagLength -ne 16) { throw "Unsupported encrypted backup envelope parameters." }
    $nonce = $bytes[$offset..($offset + $nonceLength - 1)]; $offset += $nonceLength
    $tag = $bytes[$offset..($offset + $tagLength - 1)]; $offset += $tagLength
    $lengthBytes = $bytes[$offset..($offset + 7)]; [Array]::Reverse($lengthBytes); $offset += 8
    $cipherLength = [BitConverter]::ToInt64($lengthBytes, 0)
    if ($cipherLength -lt 0 -or $cipherLength -ne ($bytes.Length - $offset)) { throw "Malformed encrypted backup envelope." }
    if ($cipherLength -gt $MaxInputBytes) {
        throw "Backup envelope payload ($cipherLength bytes) exceeds maximum in-memory decryption limit ($MaxInputBytes bytes)."
    }
    $ciphertext = $bytes[$offset..($bytes.Length - 1)]
    $plaintext = New-Object byte[] $cipherLength
    $aes = [Security.Cryptography.AesGcm]::new($key, 16)
    try {
        $aes.Decrypt($nonce, $ciphertext, $tag, $plaintext)
    } finally {
        $aes.Dispose()
    }
    $archivePath = Join-Path ([IO.Path]::GetTempPath()) ("prophetops-decrypted-" + [Guid]::NewGuid().ToString("N") + ".zip")
    [IO.File]::WriteAllBytes($archivePath, $plaintext)
} elseif (-not $PackagePath.EndsWith(".prophetops-backup.zip", [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsupported backup package extension."
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entrySeen = @{}
        $totalUncompressed = 0
        $entryCount = 0
        foreach ($entry in $zip.Entries) {
            $entryCount++
            if ($entryCount -gt $MaxEntryCount) {
                throw "Backup archive entry count ($entryCount) exceeds maximum permitted limit ($MaxEntryCount)."
            }
            if ([string]::IsNullOrWhiteSpace($entry.FullName)) { continue }
            $normalized = $entry.FullName.Replace('\', '/')

            # Validate path safety for ALL entries including directory entries before trailing slash check
            if ([IO.Path]::IsPathRooted($normalized) -or $normalized.StartsWith("/") -or $normalized.StartsWith("\") -or $normalized.Contains("../") -or $normalized.Contains("..\") -or $normalized -eq ".." -or $normalized.StartsWith("../") -or $normalized.StartsWith("..\")) {
                throw "Backup archive contains an unsafe entry path."
            }
            if ($entrySeen.ContainsKey($normalized)) { throw "Backup archive contains duplicate entry paths." }
            $entrySeen[$normalized] = $true

            $totalUncompressed += $entry.Length
            if ($totalUncompressed -gt $MaxExpandedBytes) {
                throw "Backup archive uncompressed size ($totalUncompressed bytes) exceeds maximum staging expansion limit ($MaxExpandedBytes bytes)."
            }

            if ($normalized.EndsWith("/")) {
                $dirPrefix = $normalized.TrimEnd('/') + "/"
                $allowedDir = $dirPrefix -eq "database/" -or $dirPrefix -eq "uploads/" -or $dirPrefix -eq "keys/" -or $dirPrefix -eq "config/" -or $dirPrefix.StartsWith("database/") -or $dirPrefix.StartsWith("uploads/") -or $dirPrefix.StartsWith("keys/") -or $dirPrefix.StartsWith("config/")
                if (-not $allowedDir) { throw "Backup archive contains an unexpected entry." }
            } else {
                $resolved = Resolve-InStage $stage $normalized
                $null = $resolved
                $allowed = $normalized -eq "manifest.json" -or $normalized.StartsWith("database/") -or $normalized.StartsWith("uploads/") -or $normalized.StartsWith("keys/") -or $normalized.StartsWith("config/")
                if (-not $allowed) { throw "Backup archive contains an unexpected entry." }
            }
        }
    } finally {
        $zip.Dispose()
    }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $stage
    $manifestPath = Join-Path $stage "manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "The package has no manifest."
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (-not (Test-IsIntegralNumber $manifest.SchemaVersion) -or $manifest.SchemaVersion -lt 1 -or $manifest.SchemaVersion -gt 2) {
        throw "Unsupported backup manifest schema version."
    }

    $provider = $null
    if ($manifest.SchemaVersion -eq 1) {
        if ($null -ne $manifest.Database.Provider -and $manifest.Database.Provider -ne "sqlite") {
            throw "Manifest schema version 1 does not support provider '$($manifest.Database.Provider)'."
        }
        $provider = "sqlite"
    } else {
        if ([string]::IsNullOrWhiteSpace($manifest.Database.Provider)) {
            throw "Manifest schema version 2 requires an explicit Database.Provider."
        }
        $provider = $manifest.Database.Provider.ToLowerInvariant()
        if ($provider -ne "sqlite" -and $provider -ne "postgres") {
            throw "Unsupported database provider '$provider'."
        }
    }

    if ($provider -eq "sqlite") {
        if (-not [string]::IsNullOrWhiteSpace($manifest.Database.DumpFormat) -and $manifest.Database.DumpFormat -ne "sqlite-file") {
            throw "SQLite provider does not support dump format '$($manifest.Database.DumpFormat)'."
        }
    } elseif ($provider -eq "postgres") {
        if ($manifest.Database.DumpFormat -ne "pg-dump-custom") {
            throw "PostgreSQL provider requires dump format 'pg-dump-custom', got '$($manifest.Database.DumpFormat)'."
        }
        if (-not (Test-IsIntegralNumber $manifest.Database.ServerMajor) -or $manifest.Database.ServerMajor -lt 14) {
            throw "PostgreSQL provider requires a valid ServerMajor version (>= 14)."
        }
        if ($PreserveSessions.IsPresent) {
            throw "PreserveSessions is not supported for PostgreSQL backups. Sessions are always invalidated on PostgreSQL restore."
        }
    }

    $seen = @{}
    foreach ($file in $manifest.Files) {
        if ($seen.ContainsKey($file.Path)) { throw "Duplicate manifest path." }
        $seen[$file.Path] = $true
        $path = Resolve-InStage $stage $file.Path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "The package is missing a manifest file: $($file.Path)"
        }
        $hash = Get-FileSha256 $path
        if ($hash -ne $file.Sha256) {
            throw "The package failed checksum verification for $($file.Path)."
        }
        $size = (Get-Item -LiteralPath $path).Length
        if ($size -ne $file.Size) {
            throw "The package failed size verification for $($file.Path)."
        }
    }

    foreach ($entry in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        $relative = Get-RelativePathCompat $stage $entry.FullName
        $allowed = $relative -eq "manifest.json" -or $relative.StartsWith("database/") -or $relative.StartsWith("uploads/") -or $relative.StartsWith("keys/") -or $relative.StartsWith("config/")
        if (-not $allowed -or (-not $seen.ContainsKey($relative) -and $relative -ne "manifest.json")) {
            throw "Unexpected package entry: $relative"
        }
    }

    $database = Resolve-InStage $stage $manifest.Database.Path
    $expectedRole = if ($provider -eq "sqlite") { "sqlite-database" } else { "postgres-custom-dump" }
    $databaseFile = $manifest.Files | Where-Object { $_.Path -eq $manifest.Database.Path -and $_.Role -eq $expectedRole } | Select-Object -First 1
    if ($null -eq $databaseFile -or $databaseFile.Sha256 -ne $manifest.Database.Sha256 -or $databaseFile.Size -ne $manifest.Database.Size) {
        throw "Database manifest entry is inconsistent."
    }

    if ($provider -eq "sqlite") {
        $sqlite = Get-Command sqlite3 -ErrorAction SilentlyContinue
        if ($null -eq $sqlite) {
            throw "sqlite3 is required to verify database integrity before restore activation."
        }
        $integrity = & $sqlite.Source $database "PRAGMA integrity_check;"
        if ($LASTEXITCODE -ne 0 -or $integrity -ne "ok") {
            throw "The restored database failed SQLite integrity verification."
        }
        $tables = & $sqlite.Source $database "SELECT name FROM sqlite_master WHERE type = 'table';"
        foreach ($table in @("Users", "TravelPackages", "Bookings", "Expenses", "AuditEntries", "__EFMigrationsHistory")) {
            if ($tables -notcontains $table) { throw "The restored database is missing a core table." }
        }
        $migrations = & $sqlite.Source $database "SELECT MigrationId FROM __EFMigrationsHistory;"
        if ($migrations.Count -ne $manifest.EfMigrations.Count) {
            throw "The restored database migration history does not match the manifest."
        }
        foreach ($migration in $manifest.EfMigrations) {
            if ($migrations -notcontains $migration) { throw "The restored database migration history does not match the manifest." }
        }

        $targetDb = Join-Path $DestinationRoot "prophetops.db"
        Copy-Item -LiteralPath $database -Destination $targetDb
        $uploads = Join-Path $stage "uploads"
        if (Test-Path -LiteralPath $uploads) {
            Copy-Item -LiteralPath $uploads -Destination (Join-Path $DestinationRoot "uploads") -Recurse
        }

        # Check whether the restored SQLite schema contains the SecurityStamp column
        $tableInfo = & $sqlite.Source $targetDb "PRAGMA table_info(Users);"
        $hasSecurityStamp = $tableInfo | Where-Object { $_ -match '\bSecurityStamp\b' }

        if ($hasSecurityStamp) {
            if (-not $PreserveSessions) {
                $rotateSql = "UPDATE Users SET SecurityStamp = lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(6)));"
                & $sqlite.Source $targetDb $rotateSql
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to rotate SecurityStamp during SQLite restore."
                }
            } else {
                $keys = Join-Path $stage "keys"
                if (Test-Path -LiteralPath $keys) {
                    Copy-Item -LiteralPath $keys -Destination (Join-Path $DestinationRoot "keys") -Recurse
                }
            }

            $sessionMode = if ($PreserveSessions.IsPresent) { "preserved" } else { "invalidated" }
            Write-Host "Restore staged successfully. Sessions were $sessionMode."
        } else {
            # Legacy package missing SecurityStamp column must remain staged/offline
            Write-Host "WARNING: Restored SQLite database is missing 'SecurityStamp' column (legacy schema without security stamp)."
            Write-Host "Application is NOT activation-ready. Explicit manual migration (e.g. dotnet ef database update) and session revocation prerequisites are required before starting traffic. Automatic hosted startup DDL is prohibited."
            Write-Host "Restore staged offline (legacy schema pending manual migration and session revocation prerequisites)."
        }
    } elseif ($provider -eq "postgres") {
        if ([string]::IsNullOrWhiteSpace($PostgresHost)) {
            throw "PostgreSQL restore requires -PostgresHost."
        }
        if ($PostgresPort -le 0) {
            throw "PostgreSQL restore requires a valid positive -PostgresPort."
        }
        if ([string]::IsNullOrWhiteSpace($PostgresDatabase)) {
            throw "PostgreSQL restore requires -PostgresDatabase."
        }
        if ([string]::IsNullOrWhiteSpace($PostgresUsername)) {
            throw "PostgreSQL restore requires -PostgresUsername."
        }

        # Password strictly from environment or secure password file; NEVER accepted via CLI arguments
        $postgresPassword = $env:PGPASSWORD
        if ([string]::IsNullOrWhiteSpace($postgresPassword) -and -not [string]::IsNullOrWhiteSpace($PostgresPasswordFile)) {
            if (Test-Path -LiteralPath $PostgresPasswordFile -PathType Leaf) {
                $postgresPassword = (Get-Content -LiteralPath $PostgresPasswordFile -Raw).Trim()
            } else {
                throw "PostgresPasswordFile was not found."
            }
        }

        # Schema strictly restricted to exact 'prophetops'
        if ($PostgresSchema -ne "prophetops") {
            throw "PostgreSQL restore is strictly restricted to application schema 'prophetops'. Refusing schema '$PostgresSchema'."
        }

        # Explicit target confirmation MUST match full host:port/database exactly
        $expectedTargetId = "$PostgresHost`:$PostgresPort/$PostgresDatabase"
        if ($ConfirmTarget -ne $expectedTargetId) {
            throw "Explicit target confirmation failed. Expected '-ConfirmTarget $expectedTargetId', but received '$ConfirmTarget'. Refusing restore."
        }

        $pgRestoreCmd = Get-Command pg_restore -ErrorAction SilentlyContinue
        if ($null -eq $pgRestoreCmd) {
            throw "pg_restore executable was not found. Please install PostgreSQL client tools (e.g. postgresql-client-18)."
        }
        $psqlCmd = Get-Command psql -ErrorAction SilentlyContinue
        if ($null -eq $psqlCmd) {
            throw "psql executable was not found. Please install PostgreSQL client tools (e.g. postgresql-client-18)."
        }

        # Client tool version check
        $versionRes = Invoke-SafeProcess -FilePath $pgRestoreCmd.Source -ArgumentList @("--version") -TimeoutSeconds 30 -MaxChars 65536
        if ($versionRes.ExitCode -ne 0) {
            throw "Failed to determine pg_restore client version (Diagnostic category: $($versionRes.DiagnosticCategory))."
        }
        $clientMajor = 0
        if ($versionRes.StandardOutput -match '(?:pg_restore\s+(?:\(PostgreSQL\)\s+)?)(\d+)') {
            $clientMajor = [int]$matches[1]
        } elseif ($versionRes.StandardOutput -match '\b(\d+)\.\d+\b') {
            $clientMajor = [int]$matches[1]
        } else {
            throw "Failed to parse pg_restore major version from output."
        }
        if ($clientMajor -lt $manifest.Database.ServerMajor) {
            throw "pg_restore client version ($clientMajor) is older than backup server version ($($manifest.Database.ServerMajor)). pg_restore must be equal to or newer than backup server version."
        }

        $pgEnv = [System.Collections.Generic.Dictionary[string, string]]::new()
        if (-not [string]::IsNullOrEmpty($postgresPassword)) { $pgEnv["PGPASSWORD"] = $postgresPassword }
        if (-not [string]::IsNullOrEmpty($PostgresSslMode)) { $pgEnv["PGSSLMODE"] = $PostgresSslMode }
        if (-not [string]::IsNullOrEmpty($PostgresSslRootCert)) { $pgEnv["PGSSLROOTCERT"] = $PostgresSslRootCert }

        # Comprehensive pg_catalog clean schema check covering tables, views, materialized views, sequences, foreign/partitioned tables, functions, and composite/domain/enum types
        $cleanCheckSql = "SELECT COALESCE(SUM(c), 0) FROM (SELECT count(*) AS c FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'prophetops' AND c.relkind IN ('r', 'v', 'm', 'S', 'f', 'p') UNION ALL SELECT count(*) AS c FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'prophetops' UNION ALL SELECT count(*) AS c FROM pg_catalog.pg_type t JOIN pg_catalog.pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'prophetops' AND t.typtype IN ('c', 'd', 'e')) q;"
        $psqlCleanArgs = @("-h", $PostgresHost, "-p", "$PostgresPort", "-U", $PostgresUsername, "-d", $PostgresDatabase, "-v", "ON_ERROR_STOP=1", "-X", "-A", "-t", "-c", $cleanCheckSql)
        $cleanRes = Invoke-SafeProcess -FilePath $psqlCmd.Source -ArgumentList $psqlCleanArgs -Environment $pgEnv -TimeoutSeconds 30 -MaxChars 65536
        if ($cleanRes.ExitCode -ne 0) {
            throw "Target database clean application schema check failed (Category: $($cleanRes.DiagnosticCategory), ExitCode: $($cleanRes.ExitCode))."
        }
        $objCount = 0
        if (-not [int]::TryParse($cleanRes.StandardOutput.Trim(), [ref]$objCount)) {
            throw "Failed to parse existing object count from target database check."
        }
        if ($objCount -gt 0) {
            throw "Destination database '$PostgresDatabase' application schema '$PostgresSchema' is not empty (found $objCount existing objects). Refusing to restore to a populated target. Restore requires an empty application schema."
        }

        # Guarded establishment of fixed 'prophetops' application namespace if missing before filtered pg_restore
        $schemaSql = "CREATE SCHEMA IF NOT EXISTS prophetops;"
        $psqlSchemaArgs = @("-h", $PostgresHost, "-p", "$PostgresPort", "-U", $PostgresUsername, "-d", $PostgresDatabase, "-v", "ON_ERROR_STOP=1", "-X", "-A", "-t", "-c", $schemaSql)
        $schemaRes = Invoke-SafeProcess -FilePath $psqlCmd.Source -ArgumentList $psqlSchemaArgs -Environment $pgEnv -TimeoutSeconds 30 -MaxChars 65536
        if ($schemaRes.ExitCode -ne 0) {
            throw "Target database application schema establishment failed (Category: $($schemaRes.DiagnosticCategory), ExitCode: $($schemaRes.ExitCode))."
        }

        # Execute pg_restore in single transaction
        $restoreArgs = @(
            "-h", $PostgresHost,
            "-p", "$PostgresPort",
            "-U", $PostgresUsername,
            "-d", $PostgresDatabase,
            "-n", $PostgresSchema,
            "--single-transaction",
            "--exit-on-error",
            "--no-owner",
            "--no-privileges",
            $database
        )
        $restoreRes = Invoke-SafeProcess -FilePath $pgRestoreCmd.Source -ArgumentList $restoreArgs -Environment $pgEnv -TimeoutSeconds 600 -MaxChars 65536
        if ($restoreRes.ExitCode -ne 0) {
            throw "PostgreSQL restore execution failed (Category: $($restoreRes.DiagnosticCategory), ExitCode: $($restoreRes.ExitCode))."
        }

        # Verify restored tables
        $tableCheckSql = "SELECT c.relname FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'prophetops' AND c.relkind = 'r';"
        $psqlTableArgs = @("-h", $PostgresHost, "-p", "$PostgresPort", "-U", $PostgresUsername, "-d", $PostgresDatabase, "-v", "ON_ERROR_STOP=1", "-X", "-A", "-t", "-c", $tableCheckSql)
        $tableRes = Invoke-SafeProcess -FilePath $psqlCmd.Source -ArgumentList $psqlTableArgs -Environment $pgEnv -TimeoutSeconds 30 -MaxChars 65536
        if ($tableRes.ExitCode -ne 0) {
            throw "Target database restored table verification failed (Category: $($tableRes.DiagnosticCategory), ExitCode: $($tableRes.ExitCode))."
        }
        $restoredTables = $tableRes.StandardOutput.Split([char[]]@("`r", "`n"), [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() }
        foreach ($tbl in @("Users", "TravelPackages", "Bookings", "Expenses", "AuditEntries", "__EFMigrationsHistory")) {
            if ($restoredTables -notcontains $tbl) {
                throw "The restored PostgreSQL database is missing core table '$tbl'."
            }
        }

        # Verify migrations
        $migSql = "SELECT ""MigrationId"" FROM ""$PostgresSchema"".""__EFMigrationsHistory"";"
        $psqlMigArgs = @("-h", $PostgresHost, "-p", "$PostgresPort", "-U", $PostgresUsername, "-d", $PostgresDatabase, "-v", "ON_ERROR_STOP=1", "-X", "-A", "-t", "-c", $migSql)
        $migRes = Invoke-SafeProcess -FilePath $psqlCmd.Source -ArgumentList $psqlMigArgs -Environment $pgEnv -TimeoutSeconds 30 -MaxChars 65536
        if ($migRes.ExitCode -ne 0) {
            throw "Target database restored migration history verification failed (Category: $($migRes.DiagnosticCategory), ExitCode: $($migRes.ExitCode))."
        }
        $restoredMigs = $migRes.StandardOutput.Split([char[]]@("`r", "`n"), [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() }
        if ($restoredMigs.Count -ne $manifest.EfMigrations.Count) {
            throw "The restored database migration history count ($($restoredMigs.Count)) does not match the manifest ($($manifest.EfMigrations.Count))."
        }
        foreach ($m in $manifest.EfMigrations) {
            if ($restoredMigs -notcontains $m) {
                throw "The restored database is missing migration '$m' recorded in manifest."
            }
        }

        # Verify SecurityStamp column on Users table
        $stampColSql = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'prophetops' AND table_name = 'Users' AND column_name = 'SecurityStamp';"
        $psqlStampColArgs = @("-h", $PostgresHost, "-p", "$PostgresPort", "-U", $PostgresUsername, "-d", $PostgresDatabase, "-v", "ON_ERROR_STOP=1", "-X", "-A", "-t", "-c", $stampColSql)
        $stampColRes = Invoke-SafeProcess -FilePath $psqlCmd.Source -ArgumentList $psqlStampColArgs -Environment $pgEnv -TimeoutSeconds 30 -MaxChars 65536
        if ($stampColRes.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($stampColRes.StandardOutput)) {
            throw "PostgreSQL restore failed: 'SecurityStamp' column is missing from prophetops.Users (legacy schema without security stamp). Manual schema migration is required; automatic hosted startup DDL is prohibited. Application must remain offline."
        }

        # Offline restore rotates stamps with gen_random_uuid() only (no historical-version collision)
        $stampRotateSql = "UPDATE prophetops.""Users"" SET ""SecurityStamp"" = gen_random_uuid();"
        $psqlRotateArgs = @("-h", $PostgresHost, "-p", "$PostgresPort", "-U", $PostgresUsername, "-d", $PostgresDatabase, "-v", "ON_ERROR_STOP=1", "-X", "-A", "-t", "-c", $stampRotateSql)
        $rotateRes = Invoke-SafeProcess -FilePath $psqlCmd.Source -ArgumentList $psqlRotateArgs -Environment $pgEnv -TimeoutSeconds 30 -MaxChars 65536
        if ($rotateRes.ExitCode -ne 0) {
            throw "PostgreSQL restore failed to rotate SecurityStamp (Category: $($rotateRes.DiagnosticCategory), ExitCode: $($rotateRes.ExitCode))."
        }

        # Private object staging & manifest verification for hosted offline recovery
        $attemptTrackedFiles = [System.Collections.Generic.List[string]]::new()
        try {
            $recoveryDir = Join-Path $DestinationRoot "recovery"
            $stagedImagesDir = Join-Path $recoveryDir "staged-images"
            if (-not (Test-Path -LiteralPath $stagedImagesDir)) {
                New-Item -ItemType Directory -Path $stagedImagesDir -Force | Out-Null
            }

            $imageFiles = @($manifest.Files | Where-Object { $_.Role -eq "package-image" })
            $stagedImageList = [System.Collections.Generic.List[object]]::new()
            foreach ($imgFile in $imageFiles) {
                $sourcePath = Resolve-InStage $stage $imgFile.Path
                $dbKey = $imgFile.Path
                if ($dbKey.StartsWith("uploads/")) {
                    $dbKey = $dbKey.Substring(8)
                }
                $relPath = $dbKey.Replace('/', [IO.Path]::DirectorySeparatorChar)
                $destPath = Join-Path $stagedImagesDir $relPath
                $destDir = [IO.Path]::GetDirectoryName($destPath)
                if (-not (Test-Path -LiteralPath $destDir)) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }

                # Preserve prior objects: refuse blind overwrite
                if (Test-Path -LiteralPath $destPath) {
                    throw "Destination object path already exists and cannot be overwritten: $destPath"
                }

                Copy-Item -LiteralPath $sourcePath -Destination $destPath -Force
                $attemptTrackedFiles.Add($destPath)

                # Destination hash and size verification
                $destHash = Get-FileSha256 $destPath
                if ($destHash -ne $imgFile.Sha256) {
                    throw "Staged object destination checksum mismatch for $($imgFile.Path)."
                }
                $destLen = (Get-Item -LiteralPath $destPath).Length
                if ($destLen -ne $imgFile.Size) {
                    throw "Staged object destination size mismatch for $($imgFile.Path)."
                }

                $stagedImageList.Add([ordered]@{
                    databaseKey = $dbKey
                    relativePath = $dbKey
                    size = [long]$imgFile.Size
                    sha256 = $imgFile.Sha256
                })
            }

            # Write verified staging recovery manifest outside uploaded images directory using exclusive creation
            $manifestOutPath = Join-Path $recoveryDir "recovery-manifest.json"
            if (Test-Path -LiteralPath $manifestOutPath) {
                throw "Recovery manifest already exists and cannot be overwritten: $manifestOutPath"
            }

            $recoveryManifest = [ordered]@{
                packageId = $manifest.PackageId
                schemaVersion = [int]$manifest.SchemaVersion
                databaseProvider = $provider
                stagedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
                status = "StagedOfflinePendingObjectStorageUpload"
                images = $stagedImageList
            }
            $recoveryManifestJson = ConvertTo-Json $recoveryManifest -Depth 10

            $manifestFs = [System.IO.FileStream]::new($manifestOutPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try {
                $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
                $manifestWriter = [System.IO.StreamWriter]::new($manifestFs, $utf8NoBom)
                try {
                    $manifestWriter.Write($recoveryManifestJson)
                } finally {
                    $manifestWriter.Dispose()
                }
            } finally {
                $manifestFs.Dispose()
            }
            $attemptTrackedFiles.Add($manifestOutPath)
        } catch {
            foreach ($tracked in $attemptTrackedFiles) {
                if (Test-Path -LiteralPath $tracked) {
                    Remove-Item -LiteralPath $tracked -Force -ErrorAction SilentlyContinue
                }
            }
            throw "Failed to stage private objects. Staged files for this attempt were rolled back. Application must remain OFFLINE. Error: $($_.Exception.Message)"
        }

        # Fail activation with explicit prerequisite: hosted private objects must be uploaded before traffic activation
        Write-Host "PostgreSQL schema restored, SecurityStamp rotated, and private objects staged offline."
        throw "PostgreSQL restore staged schema and objects, but application activation is BLOCKED. Provider-aware hosted private object restore/upload is required before traffic activation."
    }
} finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    if ($archivePath -ne $PackagePath -and (Test-Path -LiteralPath $archivePath)) {
        Remove-Item -LiteralPath $archivePath -Force
    }
}
