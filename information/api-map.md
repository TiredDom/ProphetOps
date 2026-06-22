# ProphetOps Route And API Map

This project uses Laravel routes that render Inertia pages and a small set of form-style backend endpoints for local persistence. The app is still a local internal DSS prototype, but authentication, roles, bookings, package presets, and expenses now use the database.

## Current Implemented Page Routes

### Account Access

Route: `GET /`

Purpose:
Redirect guests to the login page.

Response:
Redirect to `/login`.

Permissions:
Public route.

### Login

Route: `GET /login`

Purpose:
Render the Login / Account Access page.

Response:
`Inertia::render('Login')`

Permissions:
Public page route. Authenticated users redirect to their role default page.

Route: `POST /login`

Purpose:
Authenticate an active internal user with Laravel sessions.

Response:
Redirect to the intended page or the user's role default page.

Security:
Uses password hashing, session regeneration, active-user checks, and login throttling.

Route: `POST /logout`

Purpose:
Log out the current user and invalidate the Laravel session.

Permissions:
Authenticated users only.

### Dashboard

Route: `GET /dashboard`

Purpose:
Render the current dashboard page.

Response:
`Inertia::render('Welcome')`

New Sprint 1 direction:
Implemented as Owner DSS Dashboard / Decision Support Overview using `Welcome.vue`.

Permissions:
Requires Laravel auth and `role.access:Dashboard`.

### Inventory

Route: `GET /inventory`

Purpose:
Render the current Package Catalog page.

Response:
`Inertia::render('Inventory')`

Permissions:
Requires Laravel auth and `role.access:Package Catalog`.

Write routes:

- `POST /inventory`
- `PUT /inventory/{package:code}`
- `PATCH /inventory/bulk`

### Bookings / Transactions

Route: `GET /bookings`

Purpose:
Render the required Sprint 1 Bookings / Transactions page.

Response:
`Inertia::render('Bookings')`

Permissions:
Requires Laravel auth and `role.access:Bookings`.

Write routes:

- `POST /bookings`
- `PUT /bookings/{booking:code}`
- `PATCH /bookings/bulk`

### Expenses / Operational Costs

Route: `GET /expenses`

Purpose:
Render the required Sprint 1 Expenses / Operational Costs page.

Response:
`Inertia::render('Expenses')`

Permissions:
Requires Laravel auth and `role.access:Expenses`.

Write routes:

- `POST /expenses`
- `PUT /expenses/{expense:code}`
- `PATCH /expenses/bulk`

### Sales Analytics

Route: `GET /analytics`

Purpose:
Render the required Sprint 1 Sales Analytics page.

Response:
`Inertia::render('SalesAnalytics')`

Permissions:
Requires Laravel auth and `role.access:Analytics`.

### Package Decision Guide

Route: `GET /decision-guide`

Purpose:
Render the owner-facing Package Decision Guide for package alternatives.

Response:
`Inertia::render('ForecastingPreview')`

Permissions:
Requires Laravel auth and `role.access:Package Decision Guide`.

Important:
This route uses saved package records and TOPSIS criteria to compare package alternatives. Meta Prophet is not integrated. Keep TOPSIS visible as the method, not as the main page label.

Accepted query inputs:

- `budget`
- `destination`
- `duration`
- `travelType`

### Reports

Route: `GET /reports`

Purpose:
Render the current Reports page.

Response:
`Inertia::render('Reports')`

Permissions:
Requires Laravel auth and `role.access:Reports`.

### Users / Access Management

Route: `GET /users`

Purpose:
Render the current Users / Access Management page.

Response:
`Inertia::render('Users')`

Permissions:
Requires Laravel auth and `role.access:Users`.

## Current Page Routes

These routes are implemented as Inertia page routes.

| Page | Suggested route | Page component |
| --- | --- | --- |
| Login | `/login` | `Login.vue` |
| Owner DSS Dashboard | `/dashboard` | `Welcome.vue` |
| Bookings / Transactions | `/bookings` | `Bookings.vue` |
| Inventory | `/inventory` | `Inventory.vue` |
| Expenses / Operational Costs | `/expenses` | `Expenses.vue` |
| Sales Analytics | `/analytics` | `SalesAnalytics.vue` |
| Package Decision Guide | `/decision-guide` | `ForecastingPreview.vue` |
| Reports | `/reports` | `Reports.vue` |
| Users / Access Management | `/users` | `Users.vue` |

Route naming guidance:

- Prefer short business labels over technical labels.
- Keep `/forecasting` only as a compatibility redirect to `/decision-guide`.
- Put DSS interpretation inside Dashboard summaries, Reports, and the Package Decision Guide instead of a separate Trajectory Insights page.

## Current Authentication Routes

Backend authentication is now implemented.

- `GET /login`
- `POST /login`
- `POST /logout`
- protected page routes use `auth`
- protected modules use `role.access:<module label>`

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

## Current Backend Write Endpoints

Bookings:

- `POST /bookings`
- `PUT /bookings/{booking:code}`
- `PATCH /bookings/bulk`

Package catalog:

- `POST /inventory`
- `PUT /inventory/{package:code}`
- `PATCH /inventory/bulk`

Expenses:

- `POST /expenses`
- `PUT /expenses/{expense:code}`
- `PATCH /expenses/bulk`

## Future Backend Endpoints

These are future integration ideas, not current requirements.

Reports:

- `GET /reports/{type}`
- `POST /reports/{type}/export`

TOPSIS decision support:

- Future backend work may add dedicated TOPSIS API endpoints when run history or external consumption is needed.
- Possible future endpoints include `GET /topsis`, `POST /topsis/rank`, `GET /topsis/runs/{id}`, and `GET /topsis/runs/{id}/results`.
- Do not add these unless persisted TOPSIS runs or a separate API surface become necessary.

Prescriptive DSS:

- Future backend work may add endpoints for stored recommendation cards after TOPSIS output exists.
- Possible future endpoints include `GET /prescriptive-insights`, `PATCH /prescriptive-insights/{id}/status`, and `POST /prescriptive-insights/generate`.
- Recommendations should come from TOPSIS criteria, inventory/capacity status, cost movement, and package/business value.

Important:

- Do not imply these future endpoints exist.
- Do not build them until TOPSIS or prescriptive DSS integration is explicitly in scope.
