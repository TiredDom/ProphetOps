# ProphetOps Backup And Restore Guide

This document defines the backup package architecture, operator procedures, and restore workflows for ProphetOps across both local SQLite development and hosted PostgreSQL environments.

---

## 1. Backup Architecture & Manifest Versions

ProphetOps backup packages capture a consistent snapshot of the application state, business records, referenced package images, and Data Protection keys.

### Package Artifact Formats
- **Unencrypted Archive (`.prophetops-backup.zip`):** Used only in local/development environments when `Backup:Encryption:Key` is not configured.
- **Encrypted Envelope (`.prophetops-backup.pobak`):** Authenticated AES-256-GCM envelope (`POBAK` v1 header) wrapping the zipped package payload. Used whenever `Backup__Encryption__Key` is supplied. Mandatory for hosted environments.

### Manifest Schema Versions
- **SchemaVersion 1 (Legacy / SQLite):** Captures SQLite database file (`database/prophetops.db`), referenced image files under `uploads/packages/`, Data Protection XML keys under `keys/`, and core entity counts.
- **SchemaVersion 2 (PostgreSQL):** Captures custom-format database archive (`database/prophetops.dump`, `DumpFormat: "pg-dump-custom"`), server major version (`ServerMajor`), referenced image bytes, and entity counts (users, travel packages, bookings, expenses, audit entries, package images). In hosted PostgreSQL mode, Data Protection keys reside directly in the database (`prophetops.DataProtectionKeys` table) and are captured within the database dump; filesystem XML keys under `keys/` apply only to local SQLite deployments.

---

## 2. API Endpoints

### 2.1 Remote Storage Upload: `POST /api/maintenance/backup`

> [!IMPORTANT]
> The remote upload endpoint `POST /api/maintenance/backup` **uploads to off-host storage and returns JSON metadata**; it does **not** stream or download a file to the caller.

1. **Authorization:** Restricted to the Owner role (`[Authorize(Policy = "Users")]`).
2. **Maintenance Gate:** Acquires the system-wide `MaintenanceGate` to prevent concurrent mutation operations during backup generation.
3. **Storage Upload:** Generates the package and uploads it to the configured `IBackupStorage` adapter (e.g., S3).
4. **Response:** Returns JSON metadata containing `packageId`, `storedName`, and `manifest` summary.
5. **Hosted Local Upload Refusal:** When running in hosted mode (`Hosted__Enabled=true`), `LocalBackupStorage` actively refuses uploads to the local container disk (`BackupStorageUnavailable: Hosted backups require independently configured off-host storage.`).
6. **Scheduled vs Manual Upload:** Setting `Backup__Scheduled__Enabled=false` disables the background recurring backup worker, but does not convert the upload endpoint into a file export.

### 2.2 Manual Encrypted Backup Export: `POST /api/maintenance/backup/export`

For operator-driven manual backups without requiring an external S3 backup bucket or upload account:
1. **Authorization & CSRF:** Owner-only (`[Authorize(Policy = "Users")]`). Protected by antiforgery middleware; requests must supply the CSRF cookie and `X-XSRF-TOKEN` header. There is no unauthenticated or GET download route.
2. **Encryption Guarantee:** Requires a valid 32-byte key in `Backup__Encryption__Key`. Fails safely with a 503 error if encryption is unconfigured or invalid; **never** streams unencrypted plaintext ZIP bytes, even in local development mode.
3. **Gate Concurrency:** Acquires the `MaintenanceGate` to capture a consistent point-in-time snapshot, then **releases the gate before streaming starts** so that long or slow client downloads do not block normal application writes.
4. **Streaming & Artifact Cleanup:** Streams the completed `.prophetops-backup.pobak` envelope directly with `application/octet-stream`, `Cache-Control: no-store`, and attachment filename `prophetops-<timestamp>.prophetops-backup.pobak`. The temporary staged file is guaranteed to be deleted immediately upon response stream completion, client disconnect, or stream initialization failure.
5. **Independent Artifact Isolation:** Each export operation stages its own isolated file and removes only its owned artifact upon completion, preserving unrelated existing backups.

#### Operator Download Procedure

To download an encrypted backup without hardcoding credentials into shell history:

```powershell
# 1. Prompt securely for owner credentials and hosted service URL
$cred = Get-Credential -UserName "admin@prophetops.local" -Message "Enter ProphetOps Owner Credentials"
$baseUrl = Read-Host -Prompt "Enter hosted ProphetOps base URL (e.g. https://<your-service>.onrender.com)"

# 2. Authenticate to establish session cookie
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginPayload = @{
    email = $cred.UserName
    password = [System.Net.NetworkCredential]::new("", $cred.Password).Password
} | ConvertTo-Json

$loginResponse = Invoke-WebRequest -Uri "$baseUrl/api/auth/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginPayload `
    -WebSession $session

# 3. Perform authenticated GET request to issue fresh XSRF-TOKEN cookie for this session
$meResponse = Invoke-WebRequest -Uri "$baseUrl/api/auth/me" `
    -Method Get `
    -WebSession $session

# 4. Extract and URI-decode XSRF token from session cookies
$xsrfCookie = $session.Cookies.GetCookies($baseUrl) | Where-Object { $_.Name -eq "XSRF-TOKEN" }
if (-not $xsrfCookie -or [string]::IsNullOrWhiteSpace($xsrfCookie.Value)) {
    throw "XSRF-TOKEN cookie was not issued by the application. Verify the service URL and credentials."
}
$xsrfToken = [System.Uri]::UnescapeDataString($xsrfCookie.Value)

# 5. Request manual backup export using the fresh CSRF token
$timestamp = Get-Date -Format "yyyyMMddTHHmmssZ"
$outFile = "prophetops-export-$timestamp.prophetops-backup.pobak"

Invoke-WebRequest -Uri "$baseUrl/api/maintenance/backup/export" `
    -Method Post `
    -Headers @{ "X-XSRF-TOKEN" = $xsrfToken } `
    -WebSession $session `
    -OutFile $outFile

Write-Host "Encrypted backup export successfully saved to $outFile"
```

> [!CAUTION]
> **Receiving bytes is not independent backup/restore verification.** Storing the downloaded archive on the operator workstation secures the snapshot outside the ephemeral host, but does not prove the data is recoverable. To certify recovery:
> 1. Store the backup package, the encryption key, and the Data Protection certificate/password in separate secure offline vaults.
> 2. Perform at least one rehearsal restore to an isolated scratch database using `dotnet/scripts/restore-backup.ps1`.
> 3. Verify restored record counts and image checksums against the manifest. Do not claim that restore rehearsal occurred merely because the export endpoint returned bytes.

---

## 3. Local Development Mode (SQLite)

For local development and testing:
- **Database:** SQLite file located at `storage/prophetops.db`.
- **Capture:** `SqliteBackupCapture` uses `VACUUM INTO` to produce a consistent point-in-time copy.
- **Images:** Stored locally in `storage/uploads/packages/`.
- **Keys:** File system XML keys stored in `storage/keys/`.

### Local Restore
```powershell
# Unencrypted package restore
pwsh dotnet/scripts/restore-backup.ps1 `
    -PackagePath ./backup.prophetops-backup.zip `
    -DestinationRoot ./staged-restore

# Encrypted package restore
pwsh dotnet/scripts/restore-backup.ps1 `
    -PackagePath ./backup.prophetops-backup.pobak `
    -DestinationRoot ./staged-restore `
    -DecryptionKeyFile ./secret.key
```

---

## 4. Hosted Production Mode (PostgreSQL)

### Prerequisites
- **Client Tools:** `pg_dump`, `pg_restore`, and `psql` (PostgreSQL 18) installed and discoverable via `Backup__Postgres__ClientToolsPath` (`/usr/lib/postgresql/18/bin`) or `PATH`.
- **Connection:** Direct or session-pooler connection to PostgreSQL. Transaction pooling must **not** be used for backup captures.
- **Encryption Key:** 32-byte base64 key configured in `Backup__Encryption__Key`.

### PostgreSQL Restore Procedure
The restore script `dotnet/scripts/restore-backup.ps1` executes safe, verified PostgreSQL restores to a scratch database:

```powershell
# Set password in environment or pass via -PostgresPasswordFile
$env:PGPASSWORD = "<scratch-db-password>"

pwsh dotnet/scripts/restore-backup.ps1 `
    -PackagePath ./production.prophetops-backup.pobak `
    -DestinationRoot ./restore-workspace `
    -DecryptionKeyFile ./backup-aes.key `
    -PostgresHost "db.scratch.local" `
    -PostgresPort 5432 `
    -PostgresDatabase "prophetops_scratch" `
    -PostgresUsername "postgres" `
    -PostgresSchema "prophetops" `
    -ConfirmTarget "db.scratch.local:5432/prophetops_scratch"
```

### Safety Guarantees in `restore-backup.ps1`:
1. **Empty Schema Requirement:** Queries `pg_catalog` to confirm the `prophetops` schema is completely empty before executing `pg_restore`. Refuses to overwrite existing tables.
2. **Single Transaction:** Invokes `pg_restore` with `--single-transaction --exit-on-error --no-owner --no-privileges`.
3. **Core Table Verification:** Asserts that `Users`, `TravelPackages`, `Bookings`, `Expenses`, `AuditEntries`, and `__EFMigrationsHistory` were restored.
4. **Migration History Verification:** Verifies all Entity Framework migrations recorded in the manifest match the restored database.
5. **Session Revocation:** Verifies `SecurityStamp` exists on `prophetops.Users` and immediately rotates all user security stamps using `gen_random_uuid()` to invalidate existing authentication cookies.
6. **Private Image Staging:** Stages package images to `<DestinationRoot>/recovery/staged-images/` and verifies their SHA-256 hashes and lengths against the manifest.
7. **Offline Object Recovery:** Prepares the destination for offline image recovery using `dotnet ProphetOps.Api.dll --restore-private-objects`.

---

## 5. Key & Secret Custody Rules

1. **Keep Secrets Outside Backups:** The Data Protection PFX certificate, certificate password, and AES-256 backup encryption key are **never** bundled into the backup package.
2. **Store Offsite:** Store the backup encryption key and Data Protection certificate in an independent password vault. If the key is lost, `.pobak` archives cannot be decrypted.
3. **Independent Backup Storage:** Storing backups in an S3 bucket inside the same Supabase project as the live database does not constitute an independent disaster-recovery strategy. Use an isolated cloud account or offsite repository.
