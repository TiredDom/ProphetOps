# ProphetOps — Implementation Pivot (Laravel → .NET)

> **Read this once the Chapters 1–3 paper is finalized.** It is written in past tense on purpose:
> it records the decisions the paper committed us to, so we can start the code migration and
> adjustments from a settled baseline. Companion: [paper-rewrite-plan.md](paper-rewrite-plan.md).
> Written: 2026-07-08 (before the code work; treat the "we did / we chose" as the agreed plan).

---

## What the paper committed us to

By the time the manuscript was finalized, ProphetOps had been described — honestly and as-built where possible — as:

- A **Decision Support System** for Renan-Tina Travels and Tours (a small B2B travel agency), running on the agency's **private network (LAN + secure VPN)**, never exposed to the public internet.
- **Forecasting via Holt-Winters** (additive triple exponential smoothing), an **in-house implementation** — not Meta/Facebook Prophet, not Python.
- **Automated, rule-based trajectory insights** — explicitly *not* AI/ML. The paper framed the emerging tech honestly as Data Analytics + Predictive Analytics + Automation.
- Hosted on an **always-on Windows machine** (repurposed office PC / old laptop / optional mini-PC) running the app **as a Windows Service**, reachable by every device on the office Wi-Fi over **HTTPS**, and by the owner remotely through a **VPN**.
- Built with **.NET (ASP.NET Core, C#) on the backend and Vue 3 on the front-end**, using **Path B: a REST/JSON API + a Vue SPA** (client-side routing), replacing the original Laravel/Inertia stack.

The rewrite therefore had to make the shipped system match that description.

## Why we pivoted off Laravel

The pain was never PHP the language — it was **deployment**. Laravel needs PHP + extensions, Composer, a Node build, a web server, and something to keep it alive and reachable on a Windows LAN. For an offline, always-on, single-agency box, **.NET was the native Windows citizen**: it runs as a **Windows Service / under IIS**, publishes to a **self-contained `.exe` (no runtime install)**, and integrates cleanly with Windows security. The requirements (secure multi-device LAN + owner remote + always-on independent of the owner's PC) were solved mostly by **deployment architecture** (dedicated always-on box + HTTPS + VPN), and .NET made that architecture cleanest on Windows.

## What we kept vs. rewrote

The migration was **bounded and low-risk** because the backend was tiny (~1,700 LOC, 10 thin controllers, 4 entities) and the bulk of the codebase (~5,000 LOC of Vue/CSS) was **reused**.

| Kept (reused) | Rewritten (Laravel → .NET) |
|---|---|
| Vue 3 pages (9) + components (17) | Eloquent models → **EF Core entities + `DbContext`** |
| The CSS design system ("Ledger" look) | 10 thin controllers → **ASP.NET Core API controllers** |
| Holt-Winters **algorithm logic** (ported ~line-for-line) | `routes/web.php` → **attribute routing** |
| Realistic seed data | Session auth + role gate → **cookie auth + authorization policies** |
| ISO 25010 evaluation flow | Security middleware (CSP/CSRF/headers) → **ASP.NET Core middleware + antiforgery** |

Path B meant the Vue pages had their data-fetching **rewired from Inertia props to API calls** (added Vue Router + an HTTP client). Mechanical, ~a day.

## Target architecture (.NET solution)

```
ProphetOps.sln
├─ ProphetOps.Api           # ASP.NET Core Web API (controllers, auth, middleware, DI)
├─ ProphetOps.Domain        # entities: User, TravelPackage, Booking, Expense; enums; role rules
├─ ProphetOps.Data          # EF Core DbContext, migrations, seeding (HasData)
├─ ProphetOps.Forecasting   # HoltWintersForecaster + ForecastInsight (ported to C#)
└─ client/                  # Vue 3 SPA (existing pages/components/CSS), Vite build → wwwroot
```

## Migration method (module-by-module, verify against the Laravel original)

1. **Port the forecasting first.** `HoltWintersForecaster.php` + `ForecastInsight.php` → C# services. Self-contained, no framework deps. **Prove parity**: same seed data must produce the same forecast numbers, MAPE, and baselines as the PHP version (write unit tests as the oracle).
2. **Entities + DbContext + seed data.** 4 entities, relationships, EF Core migrations, `HasData` seeding (copy the current realistic dataset).
3. **Endpoints one page at a time.** Port each controller to an API endpoint, wire the matching Vue page to it, confirm parity page-by-page (Login → Dashboard → Bookings → Inventory → Expenses → Sales Analytics → Forecast → Reports → Users).
4. **Auth + security.** Cookie auth + authorization policies for the roles (Owner/Management, Staff). Re-establish the hardening we earned: **strict CSP, antiforgery (CSRF), security headers, no-store, remove server banner** — re-verify against the ZAP checklist. Do not lose the httpOnly XSRF behavior's intent.
5. **Front-end wiring.** Vue Router + HTTP client; replace Inertia visits with API calls; preserve the CSS/design system untouched.
6. **Port the test suite.** Re-express the Laravel feature tests (auth redirects, role gates, security headers, CSP strictness, forecasting endpoint) as .NET integration tests.
7. **Cut over.** Keep the Laravel app running as the reference oracle until parity, then switch.

## Deployment (what the paper promised)

- Publish **self-contained** (`dotnet publish -c Release -r win-x64 --self-contained`) → single-folder app, no .NET install on the target.
- Install as a **Windows Service** (`UseWindowsService()`), so it runs 24/7 regardless of who's logged in. Optionally front with **IIS**.
- **HTTPS on the LAN** via a locally-trusted cert (mkcert). Access by hostname/static LAN IP (e.g. `https://prophetops.local`).
- **Remote access via VPN** (Tailscale/WireGuard) — owner joins the private network; **zero public ports**. Keeps the "private, non-public system" story true.
- **Always-on host** = a dedicated box (repurposed PC / old laptop / ~₱6–12k mini-PC), **not** the owner's personal PC.

## Data store decision
SQLite is fine at ~4 concurrent users. For an always-on multi-writer server, consider **SQL Server / PostgreSQL** via EF Core to remove any concurrency doubt and read more "enterprise" — decide at step 2.

## Risks & mitigations
- **Auth/CSRF parity** — security-critical; rebuild deliberately and re-run the ZAP-equivalent checks.
- **Team defensibility** — the team must be able to explain/maintain C# at defense; budget time to walk through the .NET code together.
- **Front-end rewiring regressions** — port page-by-page against the Laravel oracle; visual-verify each.

## First concrete tasks (when we start)
1. Scaffold `ProphetOps.sln` with the four projects + `client/`.
2. Port + unit-test the Holt-Winters engine; prove numeric parity.
3. Stand up EF Core `DbContext` + entities + seed; migrate.
4. First vertical slice: Login → Dashboard end-to-end over the API.
