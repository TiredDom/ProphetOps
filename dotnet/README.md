# ProphetOps (.NET + Vue)

Demand-forecasting decision support for Renan-Tina Travels & Tours, rebuilt on
ASP.NET Core 8 (Web API) with a Vue 3 + TypeScript single-page front end. The API
serves the built SPA, so the whole system runs as **one process on one port**.

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

- **Build:** Node.js 18+ (SPA) and the .NET 8 SDK.
- **Run (published, self-contained):** nothing — the runtime is bundled.

## Develop

Two terminals, live reload:

```powershell
# API on http://localhost:5099
dotnet run --project ProphetOps.Api

# SPA on http://localhost:5173 (proxies /api to 5099)
cd client ; npm install ; npm run dev
```

Open http://localhost:5173.

## Run as a single process

Build the SPA into the API's `wwwroot`, then run only the API:

```powershell
cd client ; npm run build ; cd ..
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open http://localhost:5099.

## Test

```powershell
dotnet test
```

## Publish a standalone build

```powershell
.\publish.ps1
```

This builds the SPA and publishes a self-contained win-x64 app to `publish/`
(no .NET install needed on the target). Run it directly:

```powershell
.\publish\ProphetOps.Api.exe
```

It defaults to the Production environment and binds to `http://0.0.0.0:5099`
(all interfaces, LAN-reachable). `prophetops.db` (SQLite) is created next to the
executable on first run and seeded automatically.

## Install as a Windows Service

The host supports `UseWindowsService`, so the published executable can run as a
service (survives logout / reboot):

```powershell
sc.exe create ProphetOps binPath= "C:\ProphetOps\publish\ProphetOps.Api.exe" start= auto
sc.exe start ProphetOps
```

Remove it with `sc.exe delete ProphetOps`.

## LAN, HTTPS, and remote access

- **LAN:** allow inbound TCP 5099 through Windows Firewall; clients reach the app
  at `http://<server-ip>:5099`.
- **HTTPS:** provision a certificate and add a Kestrel HTTPS endpoint (config
  `Kestrel:Endpoints:Https`), then set the URL to `https://0.0.0.0:5443`.
- **Remote:** expose the LAN service through the agency VPN rather than the public
  internet.

## Security posture

- Cookie authentication (`HttpOnly`, `SameSite=Strict`), three role-based policies.
- Antiforgery (double-submit `XSRF-TOKEN` cookie + `X-XSRF-TOKEN` header) enforced
  on every unsafe `/api` request except the auth bootstrap.
- Hardened response headers: CSP, `X-Content-Type-Options`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer`, no `Server` header.

## Default accounts (seeded)

| Role | Email | Password |
| --- | --- | --- |
| Owner / Management | owner@prophetops.local | owner123 |
| Admin | admin@prophetops.local | admin123 |
| Staff | staff@prophetops.local | staff123 |

Change these before any real deployment.
