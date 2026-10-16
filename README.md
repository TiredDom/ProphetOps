# ProphetOps

An internal decision support system for **Renan-Tina Travels & Tours**, a
business-to-business wholesale travel agency. It replaces a workflow spread across
messaging apps, spreadsheets, email and paper notebooks with one private web
application, and pairs that record-keeping with an in-house demand forecast.

The application is private by design, and it runs as one web process: the API serves
the built single-page client. The agency's hosted copy runs on Render (free tier,
Singapore) with its records in Supabase PostgreSQL and package photos in private
Supabase storage; staff sign in with their ProphetOps accounts. A self-contained
Windows build for an office machine remains available as the legacy/local path.

## What it does

| Page | Purpose |
| --- | --- |
| **Home** | The Holt-Winters forecast with its 80% range, suggestions drawn from it, the forecast in plain words, this month so far, what needs attention, and recent activity |
| **Bookings** | Ready-made package bookings and tailored quotations, with a void trail rather than deletion |
| **Packages** | Travel packages with prices, photos and slots (open / sold / reserved), and each package's expected demand against its open slots |
| **Expenses** | Operating costs by category, optionally tied to a package, with spending suggestions and a cost scenario on the revenue forecast |
| **Reports** | Revenue, costs and revenue after costs for any period, compared like for like with the period before, broken down by package, destination, spending category and payment |
| **Team & access** | Accounts and role assignment (Owner only) |

Bookings, packages and expenses all feed the reports and the forecast, so the numbers a
manager reads come from the same records the staff enter. ProphetOps can also be
installed on a phone or computer from the browser. It stays online-only: records and
forecasts always come from the server.

### Forecasting

The analytical core is an in-house implementation of **Holt-Winters additive triple
exponential smoothing**, which separates a monthly series into level, trend and
seasonality. Its three smoothing constants are tuned by grid search against a held-out
window, and each forecast is compared with naive and seasonal-naive baselines. It can
forecast booking revenue (the main measure), bookings or passengers, six months ahead,
each month with an 80% prediction range.

- **Accuracy on unseen months:** a rolling-origin test reruns the forecast from each past
  month using only the months before it, and compares every forecast with what was then
  recorded and with repeating the same month last year. Results can be downloaded as
  CSV, and `ProphetOps.Evaluation` runs the same test offline on a CSV of monthly totals.
- **Decision aids built on the forecast:** a cost scenario (what the coming months could
  leave after costs under a stated spending assumption), each package's expected demand
  against its open slots, and prices to consider. A suggested price only fills in the
  package form; a person reviews and saves it.
- **In plain words:** ProphetOps words the forecast by rules from its own figures. Where
  the agency sets a Gemini API key, Gemini can reword the same figures, trying its models
  strongest first and stepping down when one is busy, at its limit or failing. It is given
  only monthly totals, the forecast and its accuracy test, and its wording is shown only if
  every figure in it is one of the forecast's own.

The forecast itself is deterministic classical statistics: no machine learning and no
network call. The same inputs always produce the same forecast, and the arithmetic can be
followed by hand. Gemini, when set up, only rewords; it never produces a figure.

### Access

Three roles, enforced on the server for every request:

| Role | Sees |
| --- | --- |
| **Owner / Management** | Everything, including Team & access |
| **Admin** | Everything except Team & access |
| **Staff** | Bookings and Packages only |

The forecast, expenses and reports stay closed to Staff. Every create, edit, void and
restore is written to an audit trail with its author.

## Built with

- **ASP.NET Core 10 (C#)**: REST/JSON API, authentication, authorisation, business logic
- **Vue 3 + TypeScript**: single-page client built with Vite and served by the API,
  installable as a progressive web app
- **Entity Framework Core**: SQLite for local and office installations, PostgreSQL when hosted
- **Kestrel** as one web process, in a Docker container when hosted

Security is layered rather than bolted on: hashed passwords, sign-in throttling with
escalating lockouts, antiforgery/CSRF validation, a strict Content-Security-Policy,
hardened response headers, and role checks applied server-side.

Application and test projects target .NET 10 and are pinned through `global.json`.
The legacy `ProphetOps.Setup` installer intentionally remains `net8.0`.

## Running it

The application lives in [`dotnet/`](dotnet/). After cloning:

```powershell
cd dotnet\client
npm install
npm run build
cd ..
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Demo__Enabled = "true"
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state-demo).FullName
$env:Business__TimeZone = "Asia/Manila"
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

Open http://localhost:5099 and sign in with `owner@prophetops.local` / `owner123`.

These commands explicitly enable demonstration data on an empty development database.
Normal startup does not create accounts or sample records, and Production rejects demo
mode. Remove `Demo__Enabled` from the terminal environment after the demonstration.
`Storage__Root` must be an absolute writable directory; the example above creates an
isolated local folder for the demo database, uploads, backups and authentication keys.
`Business__TimeZone` must be set deliberately; the example uses Asia/Manila.
For an agency installation, follow the first-owner setup in `dotnet/README.md` instead.

See **[dotnet/README.md](dotnet/README.md)** for development, testing, the forecast
endpoints and settings, publishing and deployment, **[SETUP.md](SETUP.md)** for a
first-time setup walkthrough, and **[DEMO.md](DEMO.md)** for the demonstration script.

## Hosting

- **[dotnet/HOSTING.md](dotnet/HOSTING.md)** is the operator guide for the hosted copy:
  Render (Docker, free tier, Singapore), Supabase PostgreSQL and private Supabase storage,
  sign-in through ProphetOps itself, offline migrations and owner setup, and the checks to
  run after a deploy. **[dotnet/BACKUP.md](dotnet/BACKUP.md)** covers encrypted backups
  and restores.
- `render.yaml` describes that hosted shape. Its secrets are marked `sync: false` and are
  entered only in the Render dashboard. Deploys are manual, and scheduled backups are off
  while the free service sleeps.
- `render.preview.yaml` and `render.preview.singapore.yaml` describe earlier disposable
  previews that keep a SQLite database in `/tmp`. They are not durable and must not be
  represented as production.
- No real domains, staff identities, bucket names, account IDs, credentials or public
  evaluator accounts are committed.

## Scope

ProphetOps is an internal operations and decision-support tool for one agency. It is not
a public booking site, a payment gateway, or a customer-facing portal, and it does not
attempt to be a general-purpose product.
