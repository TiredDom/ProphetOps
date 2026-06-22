# ProphetOps Information Index

Start here before reading long logs or editing the project.

## Current Capstone Direction

Working title:

`ProphetOps: A Business Decision Support System Using TOPSIS for Travel Operations Management`

Current system state:

- Laravel + Inertia + Vue local web system.
- SQLite database at `database/database.sqlite`.
- Backend login, roles, bookings, package catalog, expenses, analytics, reports, and dashboard are working.
- TOPSIS package comparison is implemented on `/decision-guide` as the owner-facing Package Decision Guide. `/forecasting` redirects there for older links.
- Legacy Operational Records and Data Validation pages/routes are removed.
- Meta Prophet is no longer the active capstone algorithm direction unless the team explicitly restores it.
- Active algorithm direction is TOPSIS for travel operations decision support.

## What To Read

Use this quick map instead of opening every file.

| Need | Read |
| --- | --- |
| Current project status and where modules live | `information/module-map.md` |
| Routes, pages, and backend endpoints | `information/api-map.md` |
| Database tables, seed data, and data transforms | `information/database-map.md` |
| Code files to open first by task | `information/code-navigation.md` |
| Active algorithm direction | `information/topsis-decision-support-plan.md` |
| UI components and reusable frontend patterns | `information/ui-components.md` |
| Major decisions and why they happened | `information/decisions.md` |
| Older Sprint 1 UI plans | `markdowns/README.md`, then the specific historical file |
| Recent work summary | Top entries of `information/feature-log.md` |
| Historical bug fixes | `information/fix-log.md` |
| Legacy cleanup state | `markdowns/legacy-removal-plan.md` |
| Deeper UI/design plans | `markdowns/README.md` |

## Reading Rules

- Read `information/README.md` first.
- Read only the specific map for the task.
- Treat `feature-log.md` and `fix-log.md` as history, not active instructions.
- Treat old Meta Prophet docs as historical unless the user asks to return to Prophet forecasting.
- Do not use old mock-auth or mock-data instructions; current auth/data are backend-backed.
- If a document conflicts with this index, this index wins unless the user explicitly chooses the older direction.

## Current Safe Assumptions

- No public booking website.
- No payment gateway.
- No supplier/Facebook API dependency.
- Supplier/package data may come from Google Sheets, messages, posters, and manual communication.
- Manual encoding is valid because fragmented source data is part of the research problem.
- TOPSIS should compare operational/package alternatives from standardized internal data while the visible UI stays owner-friendly.
