# ProphetOps Decisions

## 2026-06-01 - Use Laravel + Inertia.js + Vue 3 Stack

Decision:
Use Inertia.js as the bridge between Laravel and Vue instead of a separate API + SPA approach.

Reason:
Inertia eliminates the need for a separate REST API layer. Vue components serve as pages directly, receiving data from Laravel controllers. This is simpler for an internal dashboard that does not need a public API in Sprint 1.

Alternatives considered:

- Laravel API + standalone Vue SPA
- Laravel Blade only

Impact:
All pages are Vue components in `resources/js/Pages/`. Controllers return `Inertia::render()` instead of JSON or Blade views.

## 2026-06-01 - Use Vanilla CSS Instead Of Tailwind For Current UI Work

Decision:
Use vanilla CSS with custom design tokens for the current app styling.

Reason:
The project already uses custom CSS and explicit visual tokens. Continuing the existing style keeps Sprint 1 implementation consistent.

Impact:
Styles should remain in `resources/css/app.css` unless a later task explicitly restructures CSS.

## 2026-06-01 - Use XAMPP MariaDB For Local Development

Decision:
Use XAMPP's MariaDB on port 3306 with root user and no password for local development.

Reason:
This matches the local setup guide and is simple for a school/project environment.

Impact:
Local `.env` should use `DB_CONNECTION=mysql`, `DB_HOST=127.0.0.1`, `DB_PORT=3306`, `DB_USERNAME=root`, and empty `DB_PASSWORD`.

## 2026-06-03 - Use Frontend Mock Auth Before Backend Authentication

Decision:
Build account access as a frontend-only mock authentication flow first.

Reason:
Sprint 1 is focused on frontend scaffolding, navigation, role behavior, and DSS workflow review. Real backend authentication is out of scope until explicitly requested.

Impact:
`resources/js/services/mockAuth.js` stores temporary frontend session state and must be replaced by real Laravel session authentication later.

## 2026-06-06 - Frame Dashboard As A DSS Decision Cockpit

Decision:
The dashboard should present ProphetOps as a Decision Support System, not a generic admin homepage.

Reason:
The system purpose emphasizes decision support, forecasting readiness, and AI-driven trajectory insights. The first screen should show what is happening, why it matters, and what should be reviewed next.

Impact:
Dashboard implementation should prioritize Business Gist, KPIs, priority decision cards, revenue/cost visibility, forecast preview, and recent operational movement.

## 2026-06-06 - Reset Sprint 1 Scope To The New DSS Prototype Plan

Decision:
Use the new Sprint 1 plan as the active product direction.

Required pages:

- Login
- Owner DSS Dashboard
- Bookings / Transactions
- Inventory
- Expenses / Operational Costs
- Sales Analytics
- Forecasting Preview
- Trajectory Insights
- Reports
- Users / Access Management

Reason:
The previous docs over-centered Operational Records, Data Validation, Packages, and Settings. The new plan is clearer for the internal DSS prototype and aligns better with booking history, operating costs, analytics, forecasting preview, and decision support.

Impact:

- Operational Records becomes legacy direction and should be reworked into Bookings / Transactions.
- Data Validation is no longer a required standalone Sprint 1 page.
- Forecasting and AI insight pages must be labeled as sample/simulated placeholders.
- No real auth, backend persistence, real forecasting, AI generation, exports, or external integrations should be built in Sprint 1.

## 2026-06-08 - Plan Meta Prophet Forecasting With Prescriptive DSS Before Coding

Decision:
Treat Meta Prophet forecasting plus prescriptive DSS recommendations as the main future research feature, and plan it before changing implementation.

Reason:
The forecasting feature is central to the paper. It needs a clear separation between the forecasting method and the decision-support recommendation layer so the UI does not become just a chart preview or falsely imply real AI is already running.

Impact:

- `markdowns/meta-prophet-prescriptive-dss-plan.md` is now the reference before changing Dashboard forecast cards, Forecasting Preview, or Trajectory Insights.
- Prophet will be planned as the forecast engine for demand, booking volume, or revenue.
- Prescriptive DSS will translate forecast signals, inventory/capacity status, and cost movement into explainable recommended actions.
- Current Sprint 1 implementation remains sample/simulated until backend forecasting integration is explicitly requested.

## 2026-06-08 - Remove Legacy Direction In Stages

Decision:
Use a staged legacy cleanup plan for Operational Records, standalone Data Validation, and old route names.

Reason:
The active Sprint 1 plan now uses Bookings, Inventory, Expenses, Analytics, Forecasting, Trajectory Insights, Reports, and Users. Leaving old page files and docs in place can confuse future implementation.

Impact:

- `markdowns/legacy-removal-plan.md` is now the source of truth for legacy cleanup.
- Old URLs may keep temporary redirects so existing links do not break.
- Legacy Vue page files and legacy-only CSS should be removed only after active Sprint 1 pages are verified.
- Historical logs should remain intact.
