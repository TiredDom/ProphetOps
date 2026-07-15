# ProphetOps Setup Guide

Setup for ProphetOps, an internal Decision Support System for Renan-Tina Travels &
Tours built on **ASP.NET Core 8 (C#) + Vue 3 + TypeScript**, with an in-house
**Holt-Winters** demand forecast. The whole system runs as one process on one port:
the .NET API serves the built Vue SPA.

The application and its detailed guide live in `dotnet/` — see **`dotnet/README.md`**.
This file is the quick top-level reference.

## Required tools

```text
.NET 8 SDK        (dotnet --version prints 8.x)
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
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open:

```text
http://localhost:5099/login
```

The SQLite database (`prophetops.db`) is created next to the running app on first launch
and seeded automatically — there is no manual migration step.

## Development (live reload)

Two terminals, only when editing the front end:

```powershell
# Terminal 1 — API
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
`dotnet/README.md` for Windows Service installation and LAN / HTTPS / VPN notes.

## Demo accounts (seeded)

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
