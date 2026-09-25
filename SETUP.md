# ProphetOps Setup Guide

Setup for ProphetOps, an internal Decision Support System for Renan-Tina Travels &
Tours built on **ASP.NET Core 10 (C#) + Vue 3 + TypeScript**, with an in-house
**Holt-Winters** demand forecast. The whole system runs as one web process on one port:
the .NET API serves the built Vue SPA.

Application and test projects use .NET 10 pinned by `global.json`. The legacy
`ProphetOps.Setup` installer intentionally remains `net8.0` and was left unchanged.

The application and its detailed guide live in `dotnet/` — see **`dotnet/README.md`**.
This file is the quick top-level reference.

## Required tools

```text
.NET 10 SDK       (pinned by global.json)
Node.js 18+ and npm   (to build the SPA)
Git
```

No PHP, Composer, Laravel, or XAMPP — those belonged to the earlier prototype and have
been removed.

## One-pass setup

From the repository root in PowerShell:

```powershell
cd dotnet\client
npm install
npm run build
cd ..
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state).FullName
$env:Business__TimeZone = "Asia/Manila"
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open:

```text
http://localhost:5099/login
```

The SQLite database (`prophetops.db`) is created under `Storage__Root` on first launch,
along with upload, backup and authentication-key folders. Migrations run automatically,
but accounts and demonstration records are not created. Set up the first agency owner
using the one-shot command in `dotnet/README.md`.

## Development (live reload)

Two terminals, only when editing the front end:

```powershell
# Terminal 1 — API
$env:Storage__Root = (New-Item -ItemType Directory -Force dotnet\.local-state).FullName
$env:Business__TimeZone = "Asia/Manila"
dotnet run --project dotnet\ProphetOps.Api --urls http://localhost:5099

# Terminal 2 — SPA with hot reload (proxies /api to 5099)
cd dotnet\client ; npm run dev
```

Open http://localhost:5173.

## Test

```powershell
cd dotnet
dotnet test
```

Expected: all suites pass — forecasting parity, data layer, and API + security.

## Publish / deploy

```powershell
cd dotnet
.\publish.ps1
.\publish\ProphetOps.Api.exe
```

Produces a self-contained win-x64 build (no .NET install needed on the target). See
`dotnet/README.md` for the legacy/local Windows Service release path, LAN / HTTPS
notes, and the separate planned hosted-container path. The hosted access layer,
domain, backup storage and production cutover are not provisioned by this quickstart.

## Demo accounts (explicit development mode)

For a demonstration only, enable the following before starting the API against an empty
development database. Production rejects this setting, and existing records are left alone.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Demo__Enabled = "true"
$env:Storage__Root = (New-Item -ItemType Directory -Force dotnet\.local-state-demo).FullName
$env:Business__TimeZone = "Asia/Manila"
```

Remove `Demo__Enabled` from the terminal environment when finished. Do not enable it for
an agency installation.

```text
owner@prophetops.local / owner123      Owner / Management — full access
admin@prophetops.local / admin123      Admin — all modules except Users
staff@prophetops.local / staff123      Staff — Bookings + Package Catalog only
```

Invalid-login test: `wrong@example.com / wrongpass`.

## Main routes

```text
/login  /dashboard  /forecast  /analytics
/bookings  /inventory  /expenses  /reports  /users
```

## Project rule

Keep ProphetOps an internal business Decision Support System. Do not turn it into a
public booking website, payment gateway, customer portal, or marketing site.
