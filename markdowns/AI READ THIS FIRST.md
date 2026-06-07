# AI Read This First - ProphetOps Workflow Guide

Read this before changing code or documentation.

## Current Product Direction

ProphetOps is an internal Decision Support System for Renan-Tina Travels and Tours.

It is not:

- a public booking website
- a customer portal
- a payment gateway
- a marketing landing page
- an external API integration project

Sprint 1 is a front-end prototype with mock/sample data.

Required Sprint 1 pages:

1. Login
2. Owner DSS Dashboard
3. Bookings / Transactions
4. Inventory
5. Expenses / Operational Costs
6. Sales Analytics
7. Forecasting Preview
8. Trajectory Insights
9. Reports
10. Users / Access Management

Before implementing, read:

1. `information/sprint-1-direction.md`
2. `markdowns/Sprint 1 Premium Design Plan.md`
3. `markdowns/page-by-page implementation guide.md`
4. `information/module-map.md`
5. `information/api-map.md`
6. `information/ui-components.md`

## Sprint 1 Scope Rules

Do build:

- front-end shell and pages
- mock/sample data
- pseudo login with demo accounts
- role-aware navigation
- reusable UI components
- skeleton and empty states
- clear DSS insight cards

Do not build unless explicitly requested:

- real authentication
- password hashing
- backend sessions
- JWT
- database-backed users
- real forecasting engine integration
- real AI generation
- real PDF/Excel exports
- external API integrations
- customer-facing booking pages
- public website pages

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

Forecasting and AI sections must use labels such as:

- Sample Forecast Preview
- Forecast engine integration pending
- Simulated DSS Insight
- AI trajectory module placeholder

Do not imply Meta Prophet or AI analysis is already running.

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
- Use mock data in centralized files for Sprint 1.
- Use business language instead of technical jargon in the UI.
- Keep the dashboard glanceable within 5 seconds.
- Keep forecasting and AI clearly labeled as placeholders.

Last updated: June 6, 2026
