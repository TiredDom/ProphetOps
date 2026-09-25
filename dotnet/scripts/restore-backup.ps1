param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $DestinationRoot,

    [string] $DecryptionKeyFile,

    [switch] $PreserveSessions
)

$ErrorActionPreference = "Stop"

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
        foreach ($entry in $zip.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.FullName) -or $entry.FullName.EndsWith("/")) { continue }
            $normalized = $entry.FullName.Replace('\', '/')
            if ([IO.Path]::IsPathRooted($normalized) -or $normalized.Contains("../") -or $normalized.StartsWith("../")) {
                throw "Backup archive contains an unsafe entry path."
            }
            if ($entrySeen.ContainsKey($normalized)) { throw "Backup archive contains duplicate entry paths." }
            $entrySeen[$normalized] = $true
            $resolved = Resolve-InStage $stage $normalized
            $null = $resolved
            $allowed = $normalized -eq "manifest.json" -or $normalized.StartsWith("database/") -or $normalized.StartsWith("uploads/") -or $normalized.StartsWith("keys/") -or $normalized.StartsWith("config/")
            if (-not $allowed) { throw "Backup archive contains an unexpected entry." }
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
    if ($manifest.SchemaVersion -ne 1) {
        throw "Unsupported backup manifest schema."
    }

    $seen = @{}
    foreach ($file in $manifest.Files) {
        if ($seen.ContainsKey($file.Path)) { throw "Duplicate manifest path." }
        $seen[$file.Path] = $true
        $path = Resolve-InStage $stage $file.Path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "The package is missing a manifest file."
        }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -ne $file.Sha256) {
            throw "The package failed checksum verification."
        }
    }

    foreach ($entry in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($stage, $entry.FullName).Replace('\', '/')
        $allowed = $relative -eq "manifest.json" -or $relative.StartsWith("database/") -or $relative.StartsWith("uploads/") -or $relative.StartsWith("keys/") -or $relative.StartsWith("config/")
        if (-not $allowed -or (-not $seen.ContainsKey($relative) -and $relative -ne "manifest.json")) {
            throw "Unexpected package entry."
        }
    }

    $database = Resolve-InStage $stage $manifest.Database.Path
    $databaseFile = $manifest.Files | Where-Object { $_.Path -eq $manifest.Database.Path -and $_.Role -eq "sqlite-database" } | Select-Object -First 1
    if ($null -eq $databaseFile -or $databaseFile.Sha256 -ne $manifest.Database.Sha256 -or $databaseFile.Size -ne $manifest.Database.Size) {
        throw "Database manifest entry is inconsistent."
    }
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

    Copy-Item -LiteralPath $database -Destination (Join-Path $DestinationRoot "prophetops.db")
    $uploads = Join-Path $stage "uploads"
    if (Test-Path -LiteralPath $uploads) {
        Copy-Item -LiteralPath $uploads -Destination (Join-Path $DestinationRoot "uploads") -Recurse
    }

    if ($PreserveSessions) {
        $keys = Join-Path $stage "keys"
        if (Test-Path -LiteralPath $keys) {
            Copy-Item -LiteralPath $keys -Destination (Join-Path $DestinationRoot "keys") -Recurse
        }
    }

    $sessionMode = if ($PreserveSessions.IsPresent) { "preserved" } else { "invalidated" }
    Write-Host "Restore staged successfully. Sessions were $sessionMode."
} finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    if ($archivePath -ne $PackagePath -and (Test-Path -LiteralPath $archivePath)) {
        Remove-Item -LiteralPath $archivePath -Force
    }
}
