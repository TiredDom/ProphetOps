# ProphetOps

Demand-forecasting decision support system for **Renan-Tina Travels & Tours**, an
internal web application built on **ASP.NET Core 8 (C#)** with a **Vue 3 + TypeScript**
single-page front end. It centralises bookings, package catalog, expenses, analytics,
and reporting, and gives management an in-house **Holt-Winters** demand forecast.

The application is private by design: it runs on the agency's own network as a single
process (the API also serves the built SPA) and is intended to be deployed as a Windows
Service, reachable over HTTPS on the LAN and by the owner remotely through a VPN.

## The app lives in [`dotnet/`](dotnet/)

See **[dotnet/README.md](dotnet/README.md)** for full setup, development, testing,
publishing, and deployment instructions.

Quick start (after cloning):

```powershell
cd dotnet\client ; npm install ; npm run build ; cd ..
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open http://localhost:5099 and sign in with `owner@prophetops.local` / `owner123`.

## Repository layout

| Path | Contents |
| --- | --- |
| `dotnet/` | The application — .NET solution (API, domain, data, forecasting, tests) + Vue client |
| `information/` | Internal design notes, decisions, and the migration record |
| `markdowns/` | Working notes and plans |
| `outputs/` | Security/testing trackers (IAS test cases, vulnerability risk analysis) |
| `SETUP.md` | Setup guide |
| `DEMO.md` | Presentation / demo script |
| `PRODUCT.md` | Product overview |

## About

ProphetOps was migrated from an earlier Laravel/PHP prototype to the current .NET + Vue
stack; the forecasting engine's numerical behaviour was preserved exactly (parity-tested
to 1e-6 against the original). It is an internal decision-support tool — not a public
booking site, payment gateway, or customer portal.
