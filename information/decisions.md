# ProphetOps Decisions

Newest decisions override older planning notes unless the user explicitly asks to restore an older direction.

## 2026-07-02 - Adopt Holt-Winters Demand Forecasting And Remove TOPSIS

Decision:
Drop TOPSIS and the `Package Decision Guide`, and adopt Holt-Winters demand forecasting as the active named algorithm for the capstone, surfaced at `/forecast`.

Reason:
The adviser asked for an algorithm-centered direction, and Holt-Winters (additive triple exponential smoothing) is an explainable forecasting algorithm implemented from first principles rather than a black-box model. Framing it as "an algorithm, not a model" satisfies that requirement while keeping the study defensible.

Impact:

- Active owner-facing route: `/forecast` (`App\Http\Controllers\ForecastController` rendering `resources/js/Pages/Forecast.vue`, guarded by `role.access:Forecast`).
- TOPSIS was removed: `app/Support/TopsisDecisionSupport.php`, `app/Http/Controllers/ForecastingController.php`, `resources/js/Pages/ForecastingPreview.vue`, the standalone Trajectory Insights page, and `tests/Feature/TopsisDecisionSupportTest.php` are deleted. There is no longer a `Package Decision Guide`, `/decision-guide` page, or TOPSIS ranking view.
- `/forecasting` and `/decision-guide` are now both compatibility redirects to `/forecast`.
- `information/forecasting-holt-winters.md` is the source of truth for the algorithm methodology; supporting classes are `app/Support/HoltWintersForecaster.php` and `app/Support/ForecastInsight.php`, driven by `ProphetOpsData::salesHistory()`.
- The two 2026-06-19 TOPSIS entries below are superseded by this decision.

## 2026-06-19 - Present TOPSIS Through An Owner-Friendly Guide

Decision:
Use `Package Decision Guide` as the visible app module and keep TOPSIS as the method label inside that guide.

Reason:
The owner and staff do not need a technical page name to use the system. The capstone still uses TOPSIS academically, but the product should tell the owner which package fits the current business priorities.

Impact:

- Active owner-facing route: `/decision-guide`.
- `/forecasting` is only a compatibility redirect.
- The former standalone Trajectory Insights page was removed from navigation so DSS interpretation lives in Dashboard summaries, Reports, and the Package Decision Guide.
- Documentation should prefer owner-facing labels unless it is explaining the algorithm.

## 2026-06-19 - Use TOPSIS As The Active Capstone Algorithm

Decision:
Use TOPSIS as the active named algorithm for the capstone direction.

Reason:
The adviser asked for an algorithm-centered direction and advised not to title the study around Meta Prophet. TOPSIS is easier to defend for a capstone because it ranks travel package or operations alternatives through clear criteria and weights instead of requiring a full forecasting model.

Impact:

- Active title: `ProphetOps: A Business Decision Support System Using TOPSIS for Travel Operations Management`.
- `information/topsis-decision-support-plan.md` is the source of truth for algorithm planning.
- Meta Prophet and old forecasting plans are historical unless the team explicitly restores forecasting.
- Supplier/Facebook API integration remains out of scope because source data is fragmented across Google Sheets, messages, posters, and manual communication.
- Current backend data should be extended toward package criteria, criteria weights, and explainable TOPSIS ranking output.

## 2026-06-19 - Treat The Local Backend As Current System State

Decision:
Treat the Laravel session login, role access, SQLite database, bookings, package catalog, expenses, analytics, reports, and dashboard as the current working system state.

Reason:
The prototype moved beyond frontend-only Sprint 1 mock behavior. Keeping old mock-auth instructions as active guidance would confuse future work.

Impact:

- Current auth/data instructions live in `information/module-map.md`, `information/api-map.md`, and `information/database-map.md`.
- Old mock-auth and mock-data docs are historical only.
- Demo accounts remain owner/admin/staff, but they are backed by Laravel users and hashed passwords.

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
