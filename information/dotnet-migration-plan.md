# ProphetOps — .NET + TypeScript Migration Plan (start → finish)

Companion to [dotnet-implementation-pivot.md](dotnet-implementation-pivot.md) (the *why*).
This is the *how*: the ordered phases, each with a definition of done and how it is verified.

## End state (what "finished" means)

A single system running 100% on **ASP.NET Core (C#) + Vue 3 with TypeScript**, with **no**
Laravel/PHP left in the repository. It runs as a Windows Service on an always-on office box,
reachable over the LAN via HTTPS and remotely by the owner via VPN, and its as-built behaviour
matches what Chapters 1–3 describe (Holt-Winters forecasting, three roles, the four entities).

## Ground rules (apply to every phase)

- **Laravel is the oracle.** The Laravel app keeps running as the reference. Each ported module is
  proven to reproduce Laravel's behaviour before it is trusted, then Laravel is retired at the end.
- **No code comments.** Names carry the meaning. (Markdown docs like this are fine.)
- **Backend C#/.NET, front-end Vue 3 + TypeScript.** No Node backend.
- **Three roles:** Owner/Management (full), Admin (all except user management), Staff (Bookings +
  Package Catalog). A seeded admin account exists.
- **Data model truth:** the only real foreign key is Booking → TravelPackage
  (`travel_package_id`). `staff_assigned` and `related_package` are plain text, not FKs. Expenses
  store `payment_status`.
- **Paper consistency:** by cutover, the forecast headline number, the three roles, and the stack
  must all be literally true of the shipped .NET system.

## Current persistence reality (important — the migration fixes this)

The Laravel app is **split-brain** on data. A real SQLite database exists and is seeded (3 users,
6 packages, 9 bookings, 5 expenses), and controller **writes do persist** (`Booking::create`,
`update`, bulk). But every screen **reads from the static `ProphetOpsData` arrays**, not the
database — so a created record is saved yet never appears in the list, and analytics and the
forecast input are computed from the static sample rather than stored data.

The .NET rebuild makes the **database the single source of truth**: reads and writes both go
through EF Core, and lists, analytics, and the Holt-Winters input all come from stored rows. This
is genuine end-to-end persistence (SQLite file survives restarts and reboots; never EF in-memory)
and it makes the manuscript's CRUD/forecasting claims literally true.

## Toolchain status

| Tool | State |
| --- | --- |
| .NET SDK 8.0.422 | ✅ installed (user profile) |
| PHP 8.2 (runs the Laravel oracle) | ✅ present |
| Node 22 (Vite build for the SPA) | ✅ present |

---

## Phase 0 — Solution scaffold

Goal: one buildable solution containing the forecasting engine and the empty project shells.

- `dotnet new sln` → `ProphetOps.sln`; add projects: `ProphetOps.Api`, `ProphetOps.Domain`,
  `ProphetOps.Data`, `ProphetOps.Forecasting`, plus the test project.
- Create the `feature/dotnet-migration` git branch.

Done when: `dotnet build ProphetOps.sln` succeeds and the forecasting tests run from the solution.

## Phase 1 — Forecasting engine parity ✅ DONE

Goal: the C# Holt-Winters engine reproduces the PHP engine exactly.

- `ProphetOps.Forecasting/HoltWintersForecaster.cs` — line-for-line port (away-from-zero rounding
  and grid-search order preserved).
- `oracle.json` captured from the PHP engine; parity + behavioural tests.

Done: **8/8 tests green**, C# output matches the PHP oracle to 1e-6.

## Phase 2 — Domain + Data layer (EF Core)

Goal: the four entities and the seeded database exist in .NET, and the paper's forecast number is
re-confirmed from the ported data.

- `ProphetOps.Domain`: `User`, `TravelPackage`, `Booking`, `Expense` + role enum + role→permission
  rules. Pure C#, no framework dependency.
- `ProphetOps.Data`: `AppDbContext`, entity configurations (the single Booking→TravelPackage FK;
  `staff_assigned` / `related_package` as strings), EF Core migration, and `HasData` seeding ported
  from `App\Support\ProphetOpsData` (users incl. the admin, packages, bookings, expenses, and the
  36-month sample sales history).
- Re-confirm the number: run the C# forecaster on the ported sales history and check it reproduces
  the PHP/paper figure.

Done when: `dotnet ef database update` builds the SQLite database with the seeded rows, and a
data-parity test confirms the seed row counts and the forecast number match the PHP source.

## Phase 3 — API skeleton, auth, and the first vertical slice (Login → Dashboard)

Goal: log in through the Vue SPA hitting the .NET API and land on a working Dashboard.

- `ProphetOps.Api`: ASP.NET Core Web API, DI, `AppDbContext` wired, cookie authentication,
  authorization policies for the three roles.
- Security middleware re-established: strict Content-Security-Policy, antiforgery/CSRF, secure
  headers, no-store, server banner removed.
- Endpoints: `auth` (login, logout, me) and `dashboard`.
- Front-end: add Vue Router + a **typed TypeScript API client**; convert the Login and Dashboard
  pages from Inertia props to API calls.

Done when: a user can log in from the SPA, reach the Dashboard with real seeded data, unauthenticated
requests redirect, the Staff role is correctly gated, and the security headers are present.

## Phase 4 — Remaining endpoints + pages (page-by-page parity)

Goal: every screen works over the API with behaviour matching Laravel.

Port, one page at a time, wiring the matching Vue page and checking parity against the Laravel oracle:
Bookings → Inventory (packages) → Expenses → Sales Analytics → Forecast → Reports → Users.

Include the business logic: decrement available slots and update sold/reserved on a new booking;
aggregate revenue and cost; feed the forecaster from the analytics data; produce reports.

Every list, analytics figure, and forecast input must read from the database through EF Core — not
from a static sample set — so a created or edited record immediately appears everywhere (fixing the
current split-brain). The forecaster is fed from the stored booking/revenue rows.

Done when: each page reads and writes through the database with parity to Laravel, a newly created
record shows up in its list and flows into analytics/forecasts, and each page has an integration test.

## Phase 5 — Front-end TypeScript completion

Goal: the whole SPA is TypeScript, with the design system untouched.

- Convert the remaining Vue components to `<script setup lang="ts">`; type props, emits, and the API
  DTOs (shared response types).
- Keep the CSS design system exactly as-is; visually verify each page against the Laravel look.

Done when: `vue-tsc` type-checks clean, the Vite build emits to `wwwroot`, and the pages render
identically to the current app.

## Phase 6 — Security hardening + test-suite port

Goal: the security posture earned in Laravel is proven again in .NET.

- Re-express the Laravel feature tests as .NET integration tests: auth redirects, role gates, CSP
  strictness, security headers, the forecasting endpoint.
- Re-run the ZAP-equivalent checks against the running .NET app; close any findings.

Done when: the integration suite is green and the ZAP checklist passes on the .NET build.

## Phase 7 — Deployment packaging

Goal: the system runs the way the paper promises.

- `dotnet publish -c Release -r win-x64 --self-contained` → single-folder app, no runtime install.
- Install as a Windows Service (`UseWindowsService`), optionally behind IIS.
- HTTPS on the LAN via a locally trusted cert (mkcert); reach it by hostname/static IP.
- Remote access via VPN (Tailscale/WireGuard); zero public ports.
- Runs on a dedicated always-on box, not the owner's PC.

Done when: it runs as a service on a Windows host, is reachable over the LAN via HTTPS and remotely
via VPN, and survives a reboot.

## Phase 8 — Cutover & cleanup

Goal: retire Laravel and leave a clean repository.

- Switch off the Laravel app; delete the PHP code (`app/`, `routes/`, `composer.json`, the PHP
  forecaster, PHP tests) and the Inertia glue.
- Reorganise the repo around the .NET solution + `client/`; update `information/` docs.
- Final paper-consistency pass: confirm the forecast number, the three roles, and the stack
  described in Chapters 1–3 are all literally true of the shipped system.

Done when: the repository builds and runs entirely on .NET + Vue/TypeScript, Laravel is gone, and
the docs match reality.

---

## Target repository layout (end state)

```
ProphetOps.sln
├─ ProphetOps.Api            request handling, auth, middleware, DI
├─ ProphetOps.Domain         entities + role rules (no framework dependency)
├─ ProphetOps.Data           EF Core DbContext, migrations, seed
├─ ProphetOps.Forecasting    Holt-Winters engine (done)
├─ ProphetOps.*.Tests        unit + integration tests
└─ client/                   Vue 3 + TypeScript SPA (existing pages/components/CSS)
```

## Progress tracker

| Phase | Status |
| --- | --- |
| 0 — Solution scaffold | ▶ next |
| 1 — Forecasting parity | ✅ done |
| 2 — Domain + Data (EF Core) | ☐ |
| 3 — API + Login→Dashboard slice | ☐ |
| 4 — Remaining endpoints/pages | ☐ |
| 5 — Front-end TypeScript | ☐ |
| 6 — Security + test-suite port | ☐ |
| 7 — Deployment packaging | ☐ |
| 8 — Cutover & cleanup | ☐ |

## Risks & mitigations

- **Auth/CSRF parity** — security-critical; rebuild deliberately and re-run the ZAP checks (Phase 6).
- **Front-end rewiring regressions** — port page-by-page against the Laravel oracle; visually verify.
- **Team defensibility** — the team must be able to explain/maintain C# and TypeScript at the
  defense; budget time to walk the code together.
- **Paper drift** — the forecast number, roles, and stack must stay true to the manuscript; the
  Phase 8 consistency pass is the backstop.
