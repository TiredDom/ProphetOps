# ProphetOps Research Hosting Guide (Render Singapore & Supabase)

This document provides operator instructions for hosting ProphetOps in an undergraduate capstone research environment using Render (Web Service, Singapore region, Free tier) and Supabase (PostgreSQL and S3-compatible private object storage).

---

## 1. Scope & Architecture

ProphetOps is configured for a staff-only research deployment:
- **Compute:** Render Docker Web Service (`region: singapore`, `plan: free`).
- **Access Mode:** `Hosted__AccessMode=ApplicationLogin` with direct cookie authentication. Public registration and external Cloudflare Access gates are disabled.
- **Database:** Supabase managed PostgreSQL (`Database__Provider=postgres`). Schema migrations and owner bootstrapping are strictly offline operations; automatic startup DDL is prohibited.
- **Object Storage:** Supabase S3-compatible private storage (`ObjectStorage__Provider=supabase-s3`) for travel package images.
- **Ephemeral Filesystem:** Render free tier instances spin down after inactivity and do not have persistent disks. `/tmp/prophetops` is used strictly for transient staging. All business data, images, and Data Protection keys reside durably in external services.

---

## 2. Deployment Sequence

Follow this exact deployment order. Do not skip steps or attempt automatic migrations on web startup.

### Step 1: Preserve Existing Data
If upgrading from an earlier deployment or local SQLite store:
- Perform a database backup (e.g. `sqlite3 .dump` or `pg_dump`) or manual export before provisioning or altering hosted environments.
- Verify record counts (users, packages, bookings, expenses, audit entries) and package image assets.

### Step 2: Provision Cloud Resources
1. **Supabase Project:** Create a project in Singapore (`ap-southeast-1`).
2. **PostgreSQL Database:** Copy the exact connection parameters directly from the Supabase dashboard (Connect dialog):
   - Direct connection: Host `db.<project-ref>.supabase.co`, Port `5432`.
   - Shared Session Pooler (IPv4 compatible): Copy the exact pooler host directly from Connect -> Session mode, Port `5432` (do not derive an `aws-0` hostname from the region). Note that connecting via the pooler requires the username format `[ROLE].[PROJECT-REF]`, e.g., `postgres.<project-ref>` or `prophet_app.<project-ref>`.
   - Download the project root certificate / CA from Supabase Settings -> Database if custom CA verification is required by your environment (`prod-ca.crt`).
3. **Private Object Bucket:** Create a private storage bucket (e.g. `prophetops-packages`) in Supabase Storage. Generate S3 Access Key ID and Secret Access Key from project settings.

### Step 3: Explicit Offline Schema Migration
Hosted PostgreSQL does **not** apply migrations automatically on web startup (`DatabaseStartupPlan.ForWeb` enforces `Migrate: false, ValidateSchemaOnly: true`). The schema must be applied offline using the maintenance connection with verified TLS:

```bash
# Set maintenance connection string (Npgsql format with verified TLS)
export ConnectionStrings__Maintenance="Host=<supabase-host>;Port=5432;Database=postgres;Username=postgres;Password=<admin-password>;SSL Mode=VerifyFull;Root Certificate=/path/to/prod-ca.crt"
export Database__Provider=postgres
# Alternatively, if Root Certificate is omitted, ensure the CA is installed in the system trust store or set PGSSLROOTCERT:
# export PGSSLROOTCERT="/path/to/prod-ca.crt"

# Execute offline migration
dotnet ProphetOps.Api.dll --migrate-database
```

Verify that all Entity Framework Core migrations apply cleanly to the `prophetops` schema.

### Step 4: Explicit Offline Owner Bootstrap
Initial owner creation is an explicit offline action. Automatic owner bootstrapping on startup is refused when running hosted PostgreSQL:

```bash
export Bootstrap__OwnerName="Capstone Administrator"
export Bootstrap__OwnerEmail="admin@prophetops.local"
export Bootstrap__OwnerPassword="<strong-offline-password>"

# Execute owner bootstrap
dotnet ProphetOps.Api.dll --bootstrap-owner
```

> [!IMPORTANT]
> Immediately unset `Bootstrap__OwnerName`, `Bootstrap__OwnerEmail`, and `Bootstrap__OwnerPassword`. Never commit or supply bootstrap credentials to the web service environment in Render.

### Step 5: Prepare Runtime Secrets Outside Git
Generate and store the following secrets in an external password manager:
1. **Data Protection Certificate:** An X.509 certificate exported as password-protected PKCS#12 (`.pfx`) and base64-encoded:
   - `DataProtection__CertificateBase64`: Base64 string of the PFX certificate.
   - `DataProtection__CertificatePassword`: Password used to encrypt the PFX.
2. **Backup Encryption Key:** A 32-byte cryptographic key for AES-256-GCM backup envelopes:
   - `Backup__Encryption__Key`: Base64-encoded 32-byte string.
   - `Backup__Encryption__KeyId`: Logical identifier for the key (e.g. `prophetops-key-2026`).
3. **Runtime Database Connection:** Scoped application connection string (`ConnectionStrings__Default`).

### Step 6: Configure Render Environment Variables
Deploy `render.yaml` as a Render Blueprint or configure the service manually. Supply the operator-managed secrets (`sync: false`) via the Render dashboard:

| Environment Variable | Value / Description |
|----------------------|---------------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Demo__Enabled` | `false` |
| `Hosted__Enabled` | `true` |
| `Hosted__AccessMode` | `ApplicationLogin` |
| `Database__Provider` | `postgres` |
| `Transport__PublicHttps` | `true` |
| `CloudflareAccess__Enabled` | `false` |
| `Bootstrap__Owner__Enabled` | `false` |
| `Backup__Scheduled__Enabled` | `false` |
| `Storage__Root` | `/tmp/prophetops` |
| `Business__TimeZone` | `Asia/Manila` |
| `Backup__Postgres__ClientToolsPath` | `/usr/lib/postgresql/18/bin` |
| `ConnectionStrings__Default` | *(Secret: Npgsql runtime connection string)* |
| `DataProtection__CertificateBase64` | *(Secret: Base64 PFX certificate)* |
| `DataProtection__CertificatePassword` | *(Secret: Certificate password)* |
| `Backup__Encryption__Key` | *(Secret: Base64 32-byte key)* |
| `Backup__Encryption__KeyId` | `prophetops-key-2026` |
| `ObjectStorage__Provider` | `supabase-s3` |
| `ObjectStorage__S3__Endpoint` | *(Secret: `https://<project-ref>.supabase.co/storage/v1/s3`)* |
| `ObjectStorage__S3__Bucket` | *(Secret: private bucket name)* |
| `ObjectStorage__S3__Region` | `ap-southeast-1` |
| `ObjectStorage__S3__AccessKeyId` | *(Secret: S3 Access Key ID)* |
| `ObjectStorage__S3__SecretAccessKey` | *(Secret: S3 Secret Access Key)* |
| `ObjectStorage__S3__Prefix` | `""` |
| `ObjectStorage__S3__ForcePathStyle` | `true` |
| `ObjectStorage__S3__TimeoutSeconds` | `30` |

### Step 7: Deploy and Verify
Trigger a manual deploy on Render and monitor container startup.

---

## 3. Database Connectivity & Networking

### Connection String Formatting & Verified TLS
Use standard Npgsql connection-string parameter keywords. **Do not paste raw libpq URIs** and **never disable certificate verification**:

```text
Host=<host>;Port=5432;Database=postgres;Username=prophet_app;Password=<password>;SSL Mode=VerifyFull;Root Certificate=/path/to/prod-ca.crt;Pooling=true;Maximum Pool Size=10;Timeout=15;Command Timeout=30
```
- **Verified Server Identity (`SSL Mode=VerifyFull`):** Always verify the server certificate. Never use `Trust Server Certificate=true` or certificate bypasses in hosted environments. If the CA root is not in the system certificate trust store, pass `Root Certificate=/path/to/prod-ca.crt` or set `PGSSLROOTCERT=/path/to/prod-ca.crt` (supported by both Npgsql and PostgreSQL tools `psql`, `pg_dump`, `pg_restore`). Do not assume or invent pre-installed certificates inside Docker. See https://www.npgsql.org/doc/security.html.

### Direct vs Session Pooler vs Transaction Pooler
Always copy the exact host and port directly from Supabase dashboard (Connect dialog) rather than guessing:
- **Direct Connection (`port 5432` on `db.<project-ref>.supabase.co`):** Direct connection to PostgreSQL. Supported if Render can resolve IPv6 or reach the direct host. Standard username format: `prophet_app` (or `postgres`).
- **Session Pooling (`port 5432` on Supabase Pooler Host):** Copy the exact pooler host directly from Supabase Connect (Session mode, port 5432). Do not derive an `aws-0` hostname from the region. Recommended on free tier when IPv4 routing is needed. When connecting via the pooler, custom roles require the project-ref suffix: `[ROLE].[PROJECT-REF]` (e.g. `prophet_app.<project-ref>`). Behaves as a stateful PostgreSQL connection. See https://supabase.com/docs/guides/database/connecting-to-postgres.
- **Transaction Pooling (`port 6543`, Prohibited for Backups and DDL):** Supabase transaction pooler runs on port 6543. Do **NOT** use transaction pooling for ProphetOps. `pg_dump` uses session-level transactions and exported snapshots (`--snapshot=...`), which fail under transaction poolers. EF Core schema checks and migrations also require session-level locks.

### Role Separation & Data Protection Keys
1. **Maintenance Role (`ConnectionStrings__Maintenance`):** Used strictly offline for `--migrate-database`. Requires DDL permissions (`CREATE TABLE`, `ALTER TABLE`, schema ownership on `prophetops`).
2. **Runtime Role (`ConnectionStrings__Default`):** Used by the running web service. Requires only DML permissions (`SELECT`, `INSERT`, `UPDATE`, `DELETE`) on tables in `prophetops` schema, plus sequence usage.
3. **Data Protection Key Storage:** In hosted PostgreSQL mode, ASP.NET Core Data Protection keys are stored in the database (`prophetops.DataProtectionKeys`) and captured in the database dump; local filesystem XML keys under `keys/` apply only to local SQLite deployments.

---

## 4. Private Object Storage (Supabase S3)

Travel package images are uploaded through `IObjectStorage`:
- **Private Bucket:** Keep bucket access private. The application streams images through authenticated API endpoints (`/api/inventory/{code}/image`).
- **Client Configuration:** Endpoint must be HTTPS (`https://<project-ref>.supabase.co/storage/v1/s3`). `ForcePathStyle` is set to `true`.
- **Ephemeral Host & Transient Staging:** Durable image storage resides in Supabase S3; the local container disk is ephemeral (`/tmp/prophetops`). While image uploads and backup captures may temporarily buffer or stage bytes in `/tmp/prophetops` under configured quotas, durable persistence depends entirely on external object storage. If an image is uploaded while object storage is misconfigured, the upload fails safely.

---

## 5. Post-Deployment Verification Checklist

Before certifying the hosted service for capstone research evaluation, complete the following live checks:

- [ ] **Readiness Probe:** Request `GET /health/ready`. Verify it returns `200 OK` with `{"status":"ready"}`. (If the schema is incomplete, it returns `503 Service Unavailable`).
- [ ] **Liveness Probe:** Request `GET /health/live`. Verify it returns `200 OK` with `{"status":"ok"}`.
- [ ] **Owner Authentication:** Log in with the bootstrapped owner credentials via `POST /api/auth/login`. Confirm receiving session cookie `prophetops` with `HttpOnly`, `SameSite=Strict`, `Secure`.
- [ ] **Antiforgery:** Confirm `XSRF-TOKEN` cookie is set on GET requests and required as `X-XSRF-TOKEN` on POST/PUT mutations.
- [ ] **Package Catalog Flow:** Create a travel package and upload a valid JPEG/PNG image. Confirm image bytes can be viewed at `/api/inventory/{code}/image`.
- [ ] **Core Business Flows:** Execute test transactions for Bookings, Expenses, and Reports. Confirm isolated audit records are created.
- [ ] **Truthful Forecasting State:** Navigate to Forecast view. If fewer than 24 monthly observations (two annual seasonal cycles, `DemandSeriesBuilder.MinimumMonths = 24`) of completed booking records exist, confirm the system truthfully displays an "Insufficient Historical Data" guidance card with status `insufficient-history` rather than hallucinating predictive trajectories.
- [ ] **Persistence Across Restart:** Restart the web service in Render. After the container boots and reaches ready status, verify existing users, travel packages, and uploaded package images remain fully intact.
- [ ] **Backup and Scratch Restore Rehearsal:** Capture an encrypted `.pobak` backup package (via authenticated `POST /api/maintenance/backup/export` or `POST /api/maintenance/backup`) and execute `restore-backup.ps1` to an isolated scratch PostgreSQL database. Compare record counts and image checksums. (Note: No offline backup-capture CLI exists; live captures use the API or direct `pg_dump`).

---

## 6. Operational Constraints & Disclaimers

- **Free-Tier Sleeping:** Render free web services spin down after 15 minutes of inactivity. The first subsequent request will experience a 30–60 second cold start.
- **Disaster Recovery Independence:** A backup bucket placed inside the same Supabase project is **not** an independent disaster-recovery copy. Maintain an off-cloud export of critical research data.
- **Secret Custody:** Keep the Data Protection PFX certificate, its password, and the AES-256 backup encryption key backed up in secure offline custody. If these secrets are lost, encrypted backup packages and encrypted session cookies cannot be recovered.
