# ProphetOps (.NET + Vue)

Demand-forecasting decision support for Renan-Tina Travels & Tours, rebuilt on
ASP.NET Core 10 (Web API) with a Vue 3 + TypeScript single-page front end. The API
serves the built SPA, so the whole system runs as **one web process on one port**.

Application and test projects target .NET 10 and are pinned by `global.json`. The
legacy `ProphetOps.Setup` installer intentionally remains `net8.0` and was left
unchanged.

## Solution layout

| Project | Purpose |
| --- | --- |
| `ProphetOps.Forecasting` | Holt-Winters additive triple exponential smoothing (grid-searched) |
| `ProphetOps.Domain` | Entities and role/permission rules |
| `ProphetOps.Data` | EF Core (SQLite) context, migrations, seeding, sample sales history |
| `ProphetOps.Api` | Cookie-auth Web API, security middleware, serves the SPA from `wwwroot` |
| `client` | Vue 3 + TypeScript SPA (Vite) |
| `*.Tests` | xUnit suites (forecasting parity, data layer, API + security) |

## Prerequisites

- **Build:** Node.js 18+ (SPA) and the .NET 10 SDK selected by `global.json`.
- **Run (published, self-contained):** nothing — the runtime is bundled.

## Develop

Two terminals, live reload:

```powershell
# API on http://localhost:5099
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state).FullName
$env:Business__TimeZone = "Asia/Manila"
dotnet run --project ProphetOps.Api

# SPA on http://localhost:5173 (proxies /api to 5099)
cd client ; npm install ; npm run dev
```

Open http://localhost:5173. Normal startup does not seed accounts. Use the explicit
demonstration settings below for local sample data, or provision the first owner.

## Run as a single process

Build the SPA into the API's `wwwroot`, then run only the API:

```powershell
cd client ; npm run build ; cd ..
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state).FullName
$env:Business__TimeZone = "Asia/Manila"
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open http://localhost:5099.

## Test

```powershell
dotnet test
```

## Publish a standalone legacy/local build

```powershell
.\publish.ps1
```

This builds the SPA and publishes a self-contained win-x64 app to `publish/`
(no .NET install needed on the target). This is the legacy/local release path for an
agency-controlled Windows host. Run it directly:

```powershell
.\publish\ProphetOps.Api.exe
```

It defaults to the Production environment and binds to `http://0.0.0.0:5099`
(all interfaces, LAN-reachable). Configure `Storage__Root` to an absolute writable
directory before starting it; `prophetops.db`, uploads, backups and protected
authentication keys are kept beneath that root. Configure `Business__TimeZone`
explicitly as the agency-approved business timezone. The development examples use
`Asia/Manila`, but go-live acceptance still needs the agency to confirm that setting.
It starts without demo accounts or business records.

For legacy local compatibility only, `Storage__AllowContentRootFallback=true` keeps the
old content-root storage behavior. Do not use that flag for hosted/container releases,
and do not rely on it to migrate existing files; any data or upload migration must be
an explicit, tested operation.

## Legacy/local Windows Service

The host supports `UseWindowsService`, so the published executable can run as a
service (survives logout / reboot):

```powershell
sc.exe create ProphetOps binPath= "C:\ProphetOps\publish\ProphetOps.Api.exe" start= auto
sc.exe start ProphetOps
```

Remove it with `sc.exe delete ProphetOps`.

## Legacy/local LAN, HTTPS, and remote access

- **LAN:** allow inbound TCP 5099 through Windows Firewall; clients reach the app
  at `http://<server-ip>:5099`.
- **HTTPS:** provision a certificate and add a Kestrel HTTPS endpoint (config
  `Kestrel:Endpoints:Https`), then set the URL to `https://0.0.0.0:5443`.
- **Remote:** for this legacy/local path, use the agency's private remote-access
  arrangement rather than exposing an unmanaged office service directly.

## Hosted container path

The application has been prepared for a staff-only hosted container deployment:
storage paths are explicit, the business timezone is explicit, container port binding is
supported, and forwarded headers stay disabled unless a trusted proxy boundary is
configured. This repository does not provision the hosted service, access layer, domain,
object-storage bucket, credentials, DNS, billing limits, notifications, or cutover.

In hosted/container mode, startup also requires origin-side Cloudflare Access validation
to be explicitly configured. Supply the operator-approved values for
`CloudflareAccess__Issuer`, `CloudflareAccess__Audience`, and the HTTPS
`CloudflareAccess__JwksUrl`; the app accepts only the `Cf-Access-Jwt-Assertion` header
and still requires the normal ProphetOps session cookie after Access has admitted the
request.

Two source-only Render blueprints are checked in:

- `../render.preview.yaml` is a temporary free preview. It uses `/tmp/prophetops`,
  disables scheduled backups, and is intentionally non-durable.
- `../render.yaml` is the paid staff-hosting shape. It uses one persistent disk and
  requires encrypted off-host backups through an S3-compatible object store.

The paid hosted environment must include values like these, with real values supplied
only in the provider dashboard or secret manager:

```text
Hosted__Enabled=true
Storage__Root=/app/storage
Business__TimeZone=Asia/Manila
CloudflareAccess__Enabled=true
CloudflareAccess__Issuer=https://<team>.cloudflareaccess.com
CloudflareAccess__Audience=<cloudflare-access-audience>
CloudflareAccess__JwksUrl=https://<team>.cloudflareaccess.com/cdn-cgi/access/certs
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

For a temporary preview only:

```text
Hosted__Enabled=false
Transport__PublicHttps=true
Storage__Root=/tmp/prophetops
Business__TimeZone=Asia/Manila
Bootstrap__Owner__Enabled=true
Bootstrap__OwnerName=<preview-owner-name>
Bootstrap__OwnerEmail=<preview-owner-email>
Bootstrap__OwnerPassword=<unique-preview-password>
Backup__Scheduled__Enabled=false
```

For this disposable preview, keep the unique preview bootstrap secrets while the service
uses `/tmp/prophetops`. Render free instances can restart with empty storage; when that
happens the app recreates the same preview owner on the next start. Managed startup
provisioning creates an owner only when the database has no accounts; on an existing
database it leaves the existing password, role and status unchanged.

## Security posture

- Cookie authentication (`HttpOnly`, `SameSite=Strict`), three role-based policies.
- Antiforgery (double-submit `XSRF-TOKEN` cookie + `X-XSRF-TOKEN` header) enforced
  on every unsafe `/api` request except the auth bootstrap.
- Hardened response headers: CSP, `X-Content-Type-Options`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer`, no `Server` header.

## First agency owner

Run this once from the published application folder, before starting the service.
Use the same database connection settings as the service. No HTTP setup endpoint is
provided, and this command exits without opening a listening port.

```powershell
$env:Bootstrap__OwnerName = Read-Host "Owner name"
$env:Bootstrap__OwnerEmail = Read-Host "Owner email"
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state).FullName
$env:Business__TimeZone = "Asia/Manila"
$password = Read-Host "Owner password" -AsSecureString
$credential = New-Object System.Management.Automation.PSCredential("owner", $password)
$env:Bootstrap__OwnerPassword = $credential.GetNetworkCredential().Password
try {
    .\ProphetOps.Api.exe --bootstrap-owner
    if ($LASTEXITCODE -ne 0) { throw "Owner setup did not complete." }
} finally {
    Remove-Item Env:Bootstrap__OwnerName, Env:Bootstrap__OwnerEmail, Env:Bootstrap__OwnerPassword -ErrorAction SilentlyContinue
    $credential = $null
    $password = $null
}
```

Use a unique password of at least 12 characters and at most 72 UTF-8 bytes. The secret is
passed through the child process environment, not command history or an application
configuration file. The command refuses to run if any account already exists. Existing
installations retain their accounts; do not delete accounts to rerun setup.

Managed hosts without shell access may instead set `Bootstrap__Owner__Enabled=true`
for the first start, together with `Bootstrap__OwnerName`, `Bootstrap__OwnerEmail`, and
`Bootstrap__OwnerPassword` as environment secrets. Use the staff member's real Access
email address so Cloudflare Access email binding can match the ProphetOps account later.
On durable storage, remove those variables after the account is created. In disposable
preview storage, retain the preview-only values so the account can be recreated after
storage resets. Unlike the explicit `--bootstrap-owner` command, this startup mode is
idempotent on existing accounts and will not reset an existing owner's password or role.

## Remaining External Actions

The repository is source-ready only. The operator still must:

- Create and connect the Git provider repository used by Render.

For the temporary free preview:

- Create a Blueprint from `render.preview.yaml`.
- Fill the preview `sync: false` values with disposable, unique preview owner
  credentials and the chosen business timezone.
- Keep the preview bootstrap values for as long as the service uses `/tmp/prophetops`.
  They are not production owner credentials and the preview is not durable.

For paid staff hosting later:

- Create a separate Blueprint from `render.yaml`. Treat it as a new durable service
  shape, not a simple in-place free-tier upgrade.
- Fill all paid-hosting secrets in the provider dashboard or secret manager.
- Provision Cloudflare Access, staff identity policies, domain/DNS and the origin rule.
- Provision the R2/S3 bucket, lifecycle/retention policy, scoped credentials, and
  billing guardrails.
- Generate and store the backup encryption key outside the app host and disk.
- Before changing from disposable preview storage to durable hosted storage, perform a
  controlled data move: export/restore from a verified backup or run an operator-approved
  migration. Do not assume `/tmp` preview data survives a blueprint/service change.
- Run a clean-host restore rehearsal with a real backup before production cutover.

## Demonstration accounts

Demo mode is disabled by default and rejected in Production. For an empty local demo
database, set these in the development terminal before running the API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Demo__Enabled = "true"
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state-demo).FullName
$env:Business__TimeZone = "Asia/Manila"
```

Existing data is left unchanged, even if its audit table is empty. Remove
`Demo__Enabled` from the terminal environment after the demonstration.

| Role | Email | Password |
| --- | --- | --- |
| Owner / Management | owner@prophetops.local | owner123 |
| Admin | admin@prophetops.local | admin123 |
| Staff | staff@prophetops.local | staff123 |

These accounts are for demonstrations only. Agency deployments use the deliberate
first-owner procedure above, not shared demonstration credentials.
