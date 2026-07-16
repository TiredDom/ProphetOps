# AI Read This First - ProphetOps Workflow Guide

Read this before changing code or documentation.

Current source of truth:

1. `information/README.md`
2. `information/forecasting-holt-winters.md`
3. `information/module-map.md`
4. `information/api-map.md`
5. `information/database-map.md`

Older Sprint 1 and Meta Prophet documents are historical unless the user explicitly restores those directions.

## Current Product Direction

ProphetOps is an internal Decision Support System for Renan-Tina Travels and Tours.

It is not:

- a public booking website
- a customer portal
- a payment gateway
- a marketing landing page
- an external API integration project

Current system state:

- Laravel + Inertia + Vue local web system.
- SQLite database at `database/database.sqlite`.
- Backend Laravel session login and role access are active.
- Bookings, package catalog, expenses, analytics, reports, and dashboard use saved records.
- Holt-Winters (additive triple exponential smoothing) is the active algorithm direction.
- TOPSIS and Meta Prophet are historical unless the user explicitly restores those directions.

Current pages:

1. Login
2. Owner DSS Dashboard
3. Bookings / Transactions
4. Inventory
5. Expenses / Operational Costs
6. Sales Analytics
7. Forecast
8. Reports
9. Users / Access Management

Before implementing a specific task, read only the related map:

1. Modules/pages: `information/module-map.md`
2. Routes/controllers: `information/api-map.md`
3. Database/data transforms: `information/database-map.md`
4. UI components: `information/ui-components.md`
5. Algorithm methodology: `information/forecasting-holt-winters.md`

## Current Scope Rules

Do build:

- backend-backed internal pages
- saved business records
- Laravel session login with demo accounts
- role-aware navigation
- reusable UI components
- skeleton and empty states
- clear DSS insight cards
- explainable Holt-Winters forecasting and decision-support data

Do not build unless explicitly requested:

- JWT
- real AI generation
- real PDF/Excel exports
- external API integrations
- customer-facing booking pages
- public website pages
- payment flows
- supplier/Facebook API integrations
- Meta Prophet forecasting

## Required Demo Accounts

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

Role behavior:

- Owner / Management: all pages
- Admin: Dashboard, Bookings, Inventory, Expenses, Analytics, Reports
- Staff: Bookings and Inventory only

## DSS Rule

Every insight card must connect data to decision-making.

Use this structure:

Observed data -> Business meaning -> Suggested action

Visible app UI should use polished labels such as:

- Decision Support
- Forecast
- Business Priority
- Recommended Review

Do not imply Meta Prophet, AI analysis, or external API automation is already running.

## Forecast DSS Rule

The active paper feature is a Holt-Winters-supported Business Decision Support System.

Before changing the Dashboard or Forecast (`resources/js/Pages/Forecast.vue`, served at `/forecast`) pages, read:

- `information/forecasting-holt-winters.md`

Methodology direction:

- Holt-Winters (additive triple exponential smoothing) forecasts monthly revenue from the sample sales history.
- The algorithm is implemented from first principles so it stays explainable to owners, admins, and staff.
- Output should include the forecast series, level/trend/seasonal components, recommended review action, and plain-language reason.
- Fragmented source data from Google Sheets, messages, posters, and manual communication is valid for the study.

## Git Rule

Never push directly to `main`.

For Codex-created branches, use the `codex/` prefix unless the user asks for a different branch name.

Recommended flow:

```bash
git checkout main
git pull origin main
git checkout -b codex/short-description
```

Before committing:

- Check changed files.
- Do not revert user changes.
- Keep docs/code changes scoped to the request.

Commit message examples:

```bash
git commit -m "docs: align sprint 1 planning"
git commit -m "feat: add pseudo login roles"
git commit -m "fix: correct dashboard route guard"
```

## AI Assistant Rules

- Work with the current project state.
- Do not overwrite unrelated user changes.
- Keep ProphetOps internal-facing.
- Prefer existing Vue/Laravel/Inertia patterns.
- Use backend records and `app/Support/ProphetOpsData.php` for shared page data.
- Use business language instead of technical jargon in the UI.
- Keep the dashboard glanceable within 5 seconds.
- Keep Holt-Winters forecasting and DSS output explainable, not black-box.
- Treat old TOPSIS, Meta Prophet, and AI notes as historical unless restored.

Last updated: June 19, 2026
