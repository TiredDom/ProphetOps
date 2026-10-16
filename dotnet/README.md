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
| `ProphetOps.Forecasting` | Holt-Winters additive triple exponential smoothing (grid-searched), the rolling-origin accuracy test, and the monthly CSV reader |
| `ProphetOps.Domain` | Entities and role/permission rules |
| `ProphetOps.Data` | EF Core context and SQLite migrations, seeding, sample sales history, monthly demand series |
| `ProphetOps.PostgresMigrations` | EF Core migrations for hosted PostgreSQL |
| `ProphetOps.Api` | Cookie-auth Web API, security middleware, forecast and planning endpoints; serves the SPA from `wwwroot` |
| `ProphetOps.Evaluation` | Offline command that tests the forecast on a CSV of monthly totals |
| `ProphetOps.Setup` | Legacy Windows installer (`net8.0`) |
| `client` | Vue 3 + TypeScript SPA (Vite), installable as a progressive web app |
| `*.Tests` | xUnit suites (forecasting, data layer, API + security) |

## Prerequisites

- **Build:** Node.js 18+ (SPA) and the .NET 10 SDK selected by `global.json`.
- **Run (published, self-contained):** nothing; the runtime is bundled.

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
cd client ; npx playwright install chromium ; npm test
```

`dotnet test` runs the forecasting, data and API suites. PostgreSQL provider tests are
skipped unless `PROPHETOPS_TEST_POSTGRES` names a test database. The restore-script tests
start PowerShell 7, which must be able to find the .NET 10 runtime (set `DOTNET_ROOT` when
the SDK is not installed system-wide). `npm test` runs the browser tests: each file starts
its own Vite server on its own port and answers the API with fixed test data.

## Forecast endpoints

Every forecast endpoint needs an Owner or Admin session. `measure` is `revenue` (the
default), `bookings` or `passengers`; counts are by booking date.

| Endpoint | What it gives |
| --- | --- |
| `GET /api/forecast?measure=` | The six-month Holt-Winters forecast with 80% ranges, its fit, baselines and suggestions |
| `GET /api/forecast/evaluation?measure=` | The rolling-origin test: the forecast rerun from each past month on the months before it, compared with what was recorded and with the same month last year |
| `GET /api/export/forecast-test.csv?measure=` | Every comparison in that test, one row each |
| `GET /api/forecast/explanation?measure=` | The forecast in plain words, written by rules from its own figures, and whether Gemini is set up |
| `POST /api/forecast/explanation?measure=` | Gemini's wording of the same figures, or the rules' wording with the reason Gemini's was not used |
| `GET /api/forecast/scenario?basis=share&share=0.42` | Revenue forecast less spending assumed as a share of revenue (default: the share of the last 12 recorded months) or, with `basis=last-year`, as the same month last year; also needs Expenses access |
| `GET /api/forecast/packages?months=3` | Each package's expected passengers over 1 to 6 months (its share of the last 12 months' passengers) against its open slots, with a price to consider where one applies |

When recent months have no bookings recorded, some forecast months may already have
passed; the scenario and the package outlook leave those months out and name them.
Suggested prices only fill in the package form; nothing is saved until a person saves it.

## Test the forecast on your own figures

`ProphetOps.Evaluation` runs the same rolling-origin test offline, on a CSV of monthly
totals that never has to be entered into the application:

```powershell
dotnet run --project ProphetOps.Evaluation -- agency-revenue.csv --out results
```

The CSV has one row per month, `month,value` (for example `2024-01,152000`), in any order,
with an optional header. Every month between the first and the last must be present;
write 0 for a month that really had nothing. Missing or repeated months are refused, not
filled in. It prints the result and, with `--out`, writes `forecast-test-points.csv` and
`forecast-test-summary.csv`. `--horizon` (1 to 12, default 6) and `--season` (default 12)
change the test. Exit codes: 0 tested, 1 too few months, 2 unreadable input. Nothing is
sent anywhere.

## Install as an app

The built client carries a web app manifest, icons and a small service worker, so
browsers that support it (Chrome, Edge, Android) offer **Install ProphetOps** from the
account menu; on iPhone and iPad, use Share, then Add to Home Screen. The worker only
answers page loads it cannot complete with a short offline page: it stores no pages,
records or forecasts, and data requests never pass through it. It is registered by the
built app only, not by the development server. Installing needs HTTPS (or localhost).

## Optional: Gemini wording

The forecast's plain-words explanation is written by rules. Gemini can reword the same
figures when a key is set; without one, nothing is sent anywhere.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Gemini__ApiKey` | empty (off) | Gemini API key. A secret: set it only in the host's dashboard or secret manager. `GEMINI_API_KEY` is read when this is absent. |
| `Gemini__Models` | `gemini-4-argon,gemini-3.8-flash,gemini-3.6-flash,gemini-3.5-flash-lite,gemini-3.1-flash-lite` | The models to try, strongest first, separated by commas (up to 8) |
| `Gemini__ThinkingLevel` | `low` | How much a model thinks before answering: `minimal`, `low`, `medium`, `high`, or `none` to leave it to the model. Not sent to Gemini 2.5 models. |
| `Gemini__TimeoutSeconds` | `20` | Time each model gets, 2 to 60 |
| `Gemini__TotalTimeoutSeconds` | `45` | Time the whole chain gets, up to 120 |
| `Gemini__MaxRequestsPerHour` | `30` | Calls the whole service may send in an hour, fallbacks included, 1 to 1000 |
| `Gemini__CacheHours` | `12` | How long a wording is kept for the same figures, 0 to 72 |

- **What is sent:** the agency-wide monthly totals for the chosen measure, the forecast with
  its ranges, and the accuracy test, already rounded and worded. No client, booking,
  package, staff member or text anyone typed is included. The key travels in the
  `x-goog-api-key` header, never in the address, and is not logged or returned.
- **What is checked:** the answer is used only if it is the JSON asked for, within set
  lengths, and every figure in it is one of the forecast's own, as given or rounded.
  The page renders the wording as plain text.
- **Before setting a key** for the agency's real records, get approval for the spending and
  for sending these figures to Google, and check Google's data terms for the plan in use.

**Fallbacks.** The models are tried in order. A model that answers 429 (rate limit or quota),
a server error, nothing within its time, or 403/404 (not open to this key) is passed over for
the next one, and so is an answer that is out of shape or holds a figure the forecast does
not. A model that was rate-limited rests for as long as Google asks, at least a minute and
longer each time it happens again (up to an hour); one that failed rests 30 seconds and
longer on repeats (up to 10 minutes); one not open to the key rests six hours. Later requests
therefore go straight to a model that is answering. If Google rejects the key itself, the
chain stops at once and the key is not tried again for ten minutes. Each Gemini wording names
the model that wrote it and the stronger ones that could not; when no model can give a usable
wording, ProphetOps' own wording is shown with the reason.

**A key on your own machine.** The API project keeps development secrets outside the
repository, so for local testing store the key with .NET user secrets. The prompt hides what
is typed, so the key never reaches the screen or the command history:

```powershell
dotnet user-secrets set "Gemini:ApiKey" ([System.Net.NetworkCredential]::new('', (Read-Host 'Gemini API key' -AsSecureString)).Password) --project ProphetOps.Api
```

User secrets are read only in the Development environment. Remove the key with
`dotnet user-secrets remove "Gemini:ApiKey" --project ProphetOps.Api`.

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

## Hosted path

The hosted copy runs the `Dockerfile` image on Render with Supabase PostgreSQL and private
Supabase storage. **[HOSTING.md](HOSTING.md)** is the step-by-step operator guide and
**[BACKUP.md](BACKUP.md)** covers encrypted backups and restores. In short:

- `Hosted__Enabled=true` turns on the hosted checks: storage must be set explicitly,
  backups must be encrypted, and with `ApplicationLogin` the database must be PostgreSQL.
  On PostgreSQL the web process never migrates the schema or creates the first owner at
  startup; run `--migrate-database` and `--bootstrap-owner` offline, as HOSTING.md
  describes, with a verified-TLS connection string.
- `Hosted__AccessMode=ApplicationLogin` is the current access mode: staff sign in with
  their ProphetOps accounts, and `CloudflareAccess__Enabled=false`. Without that mode,
  hosted startup requires Cloudflare Access in front of the app: set
  `CloudflareAccess__Enabled=true` with the operator-approved `CloudflareAccess__Issuer`,
  `CloudflareAccess__Audience` and HTTPS `CloudflareAccess__JwksUrl`. The app then
  accepts only the `Cf-Access-Jwt-Assertion` header and still requires the normal
  ProphetOps session cookie.
- `../render.yaml` describes the current hosted shape (free plan, Singapore, manual
  deploys, scheduled backups off while the service sleeps). Its secrets are `sync: false`
  and are entered only in the Render dashboard.
- `../render.preview.yaml` and `../render.preview.singapore.yaml` describe earlier
  disposable previews that keep SQLite in `/tmp/prophetops` and create a preview owner
  from environment secrets at each start. They are not durable.

For such a disposable preview only:

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

Render free instances can restart with empty storage; when that happens a preview
recreates the same owner on the next start. Startup provisioning creates an owner only
when the database has no accounts; on an existing database it leaves the existing
password, role and status unchanged.

## Security posture

- Cookie authentication (`HttpOnly`, `SameSite=Strict`), three role-based policies.
- Antiforgery (double-submit `XSRF-TOKEN` cookie + `X-XSRF-TOKEN` header) enforced
  on every unsafe `/api` request except the auth bootstrap.
- Hardened response headers: CSP, `X-Content-Type-Options`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer`, no `Server` header.
- An `/api` address with no endpoint answers 404 in JSON rather than the app page.

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

Hosts without shell access that keep SQLite (such as the disposable previews) may instead
set `Bootstrap__Owner__Enabled=true` for the first start, together with
`Bootstrap__OwnerName`, `Bootstrap__OwnerEmail`, and `Bootstrap__OwnerPassword` as
environment secrets. Hosted PostgreSQL refuses this; use the offline command in
HOSTING.md. Where Cloudflare Access is in use, give the owner the staff member's Access
email address so the two can be matched. On durable storage, remove those variables
after the account is created. Unlike the explicit `--bootstrap-owner` command, this
startup mode is idempotent on existing accounts and will not reset an existing owner's
password or role.

## Outstanding operator actions

The hosted copy is running. Before handing it to the agency, the operator still must:

- Restore an actual encrypted hosted backup into an isolated scratch database and compare
  record counts and images.
- Keep the backup encryption key, the Data Protection certificate and its password in
  independent custody, with an encrypted backup copy outside the Supabase project.
- Decide whether the service keeps its current Render hostname or gets its own domain.
- Turn on scheduled backups only once the service no longer sleeps, and set a Gemini key
  only after the approvals above.

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
