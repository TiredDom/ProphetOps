# ProphetOps Backup And Restore

B04 adds full backup packages and a hosted S3-compatible storage adapter. It does not
provision Render, Cloudflare, R2, a bucket, credentials, notifications, or recovery
operations.

## Package Contents

Each package is a `.prophetops-backup.zip` containing captured bytes:

- `manifest.json`
- `database/prophetops.db`
- referenced `uploads/packages/*` files
- Data Protection key files under `keys/`, when present
- non-secret operational configuration facts

The manifest records schema version, UTC creation time, SQLite integrity result, EF
migrations, file sizes, SHA-256 hashes, row counts, encryption mode, and session
continuity behavior. It does not include credentials, tokens, connection strings,
absolute storage paths, Cloudflare values, full hostnames, or provider secrets.

Local/test packages may be labelled `none-local-test-only` for encryption. When
`Backup__Encryption__Key` is supplied, packages are written as authenticated
AES-256-GCM envelopes with the `.prophetops-backup.pobak` extension; independent-copy
checksums cover the encrypted artifact. Hosted independent uploads require both an
operator-supplied encryption key from outside the app host/disk and an independently
configured off-host storage adapter. The checked-in local storage adapter is for local
and test packages only.

## Hosted Object Storage

Set `Backup__Storage=r2` or `Backup__Storage=s3` to use the S3-compatible adapter. The
adapter uses the AWS SDK for .NET, uploads the encrypted package as a stream with the
R2-compatible payload-signing/checksum flags, records SHA-256 metadata, downloads and
hashes the stored object before pruning, lists retained backup objects for retention,
and deletes only verified older objects.

Required hosted settings:

```text
Backup__Scheduled__Enabled=true
Backup__Storage=r2
Backup__Encryption__Key=<base64-32-byte-key>
Backup__Encryption__KeyId=<operator-key-id>
Backup__S3__Endpoint=https://<account-id>.r2.cloudflarestorage.com
Backup__S3__Bucket=<bucket-name>
Backup__S3__Region=auto
Backup__S3__AccessKeyId=<scoped-access-key-id>
Backup__S3__SecretAccessKey=<scoped-secret-access-key>
Backup__S3__Prefix=prophetops/production/
```

When hosted scheduled backups are enabled, startup validates that encryption and
S3-compatible storage are configured. Temporary non-durable preview deployments must set
`Backup__Scheduled__Enabled=false` explicitly.

## Restore

Restore to a new empty destination only:

```powershell
dotnet/scripts/restore-backup.ps1 -PackagePath C:\backups\prophetops.prophetops-backup.zip -DestinationRoot C:\ProphetOps\restore
```

Encrypted packages require PowerShell 7 or newer and an explicit file containing the
base64 32-byte key:

```powershell
dotnet/scripts/restore-backup.ps1 -PackagePath C:\backups\prophetops.prophetops-backup.pobak -DestinationRoot C:\ProphetOps\restore -DecryptionKeyFile C:\secure\prophetops-backup.key
```

By default, restored sessions are invalidated. Preserve Data Protection keys only when
the operator deliberately chooses it after verification:

```powershell
dotnet/scripts/restore-backup.ps1 -PackagePath C:\backups\prophetops.prophetops-backup.zip -DestinationRoot C:\ProphetOps\restore -PreserveSessions
```

The script refuses non-empty destinations, rejects path traversal and unexpected package
entries, verifies manifest file hashes and core schema/migration history, and requires
`sqlite3` to pass `PRAGMA integrity_check` before copying into the destination. A
clean-host rehearsal remains an operational gate before production use.

When retained backup packages are encrypted, the app cannot inspect their manifests
without an operator key. Quarantined package images are retained in that case rather than
deleted.

## Operational Gates

- Agency-approved backup frequency and recovery target.
- Independent storage provisioning and scoped credentials.
- Recovery key custody outside the app host/disk.
- Named operator notification channel.
- Clean-host restore rehearsal with real deployment configuration.
- R2 bucket policy, retention, billing limits, and credential rotation.
