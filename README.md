# ProphetOps

An internal decision support system for **Renan-Tina Travels & Tours**, a
business-to-business wholesale travel agency. It replaces a workflow spread across
messaging apps, spreadsheets, email and paper notebooks with one private web
application, and pairs that record-keeping with an in-house demand forecast.

The application is private by design. It runs on the agency's own always-on machine as a
single process — the API serves the built single-page client — deployed as a Windows
Service, reachable over HTTPS on the office network and by the owner remotely through a
VPN. It is never exposed to the public internet, and the sign-in screen is deliberately
unbranded.

## What it does

| Module | Purpose |
| --- | --- |
| **Dashboard** | The current state of the business at a glance, plus anything needing attention |
| **Bookings** | Ready-made package bookings and tailored client quotations, with a void trail rather than deletion |
| **Package Catalog** | Travel packages with pricing, photos, and slot tracking (available / sold / reserved) |
| **Expenses** | Operating costs by category, optionally tied to a package |
| **Sales Analytics** | Revenue, package mix, payment status, and revenue by destination |
| **Forecast** | Monthly demand forecasts with prediction intervals and plain-language trajectory insights |
| **Reports** | Consolidated revenue, cost and profit summaries for a chosen period |
| **Users** | Accounts and role assignment |

Bookings, the catalog and expenses all feed the analytics and the forecast, so the
numbers a manager reads come from the same records the staff enter.

### Forecasting

The analytical core is an in-house implementation of **Holt-Winters additive triple
exponential smoothing**, which separates a demand series into level, trend and
seasonality. Its three smoothing constants are tuned by grid search against a held-out
validation window, and every forecast is benchmarked against naive and seasonal-naive
baselines. Results carry prediction intervals and a rule-based narrative describing
direction, percentage change, and peak month.

This is deterministic classical statistics — no machine learning, no external service,
no network call. The same inputs always produce the same forecast, and the arithmetic
can be followed by hand.

### Access

Three roles, enforced on the server for every request:

| Role | Sees |
| --- | --- |
| **Owner / Management** | Everything, including account management |
| **Admin** | Everything except account management |
| **Staff** | Bookings and Package Catalog only |

Expenses, analytics and forecasts stay closed to Staff by default. Every create, edit,
void and restore is written to an audit trail with its author.

## Built with

- **ASP.NET Core 8 (C#)** — REST/JSON API, authentication, authorisation, business logic
- **Vue 3 + TypeScript** — single-page client built with Vite, served by the API
- **Entity Framework Core + SQLite** — typed data access, parameterised queries, migrations
- **Kestrel** hosted as a Windows Service, with scheduled database backups

Security is layered rather than bolted on: hashed passwords, sign-in throttling with
escalating lockouts, antiforgery/CSRF validation, a strict Content-Security-Policy,
hardened response headers, and role checks applied server-side.

## Running it

The application lives in [`dotnet/`](dotnet/). After cloning:

```powershell
cd dotnet\client ; npm install ; npm run build ; cd ..
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open http://localhost:5099 and sign in with `owner@prophetops.local` / `owner123`.

See **[dotnet/README.md](dotnet/README.md)** for development, testing, publishing and
deployment, **[SETUP.md](SETUP.md)** for a first-time setup walkthrough, and
**[DEMO.md](DEMO.md)** for the demonstration script.

## Scope

ProphetOps is an internal operations and decision-support tool for one agency. It is not
a public booking site, a payment gateway, or a customer-facing portal, and it does not
attempt to be a general-purpose product.
