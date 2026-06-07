# ProphetOps Route And API Map

This project currently uses Laravel routes that render Inertia pages. Sprint 1 should continue this simple page-route style with frontend mock data unless backend work is explicitly requested.

## Current Implemented Page Routes

### Account Access

Route: `GET /`

Purpose:
Render the frontend-only Login / Account Access page.

Response:
`Inertia::render('Login')`

Permissions:
Public page route. Login processing is prototype-only and frontend-based.

### Login

Route: `GET /login`

Purpose:
Render the frontend-only Login / Account Access page.

Response:
`Inertia::render('Login')`

Permissions:
Public page route. No backend authentication endpoint exists yet.

### Dashboard

Route: `GET /dashboard`

Purpose:
Render the current dashboard page.

Response:
`Inertia::render('Welcome')`

New Sprint 1 direction:
Rework this page into Owner DSS Dashboard / Decision Support Overview.

Permissions:
Frontend-only mock auth and role checks for Sprint 1.

### Legacy Operational Records

Route: `GET /data/operational-records`

Purpose:
Render the current Operational Records frontend workspace.

Response:
`Inertia::render('OperationalRecords')`

New Sprint 1 direction:
Treat this as the predecessor to Bookings / Transactions. It should be reworked, renamed, or replaced when implementing the new required page list.

### Legacy Data Validation

Route: `GET /data/validation`

Purpose:
Render the current Data Validation frontend workspace.

Response:
`Inertia::render('DataValidation')`

New Sprint 1 direction:
Data quality should remain part of the app, but Data Validation is not a required standalone page in the new Sprint 1 plan.

### Inventory

Route: `GET /operations/inventory`

Purpose:
Render the current Inventory frontend workspace.

Response:
`Inertia::render('Inventory')`

New Sprint 1 direction:
Keep this page and align it to package availability, slots, reserved count, sold count, Low/Critical inventory status, and related bookings.

## Target Sprint 1 Page Routes

These routes are planning targets. They may not exist yet.

| Page | Suggested route | Page component |
| --- | --- | --- |
| Login | `/login` | `Login.vue` |
| Owner DSS Dashboard | `/dashboard` | `Dashboard.vue` or reworked `Welcome.vue` |
| Bookings / Transactions | `/bookings` | `Bookings.vue` |
| Inventory | `/inventory` | `Inventory.vue` |
| Expenses / Operational Costs | `/expenses` | `Expenses.vue` |
| Sales Analytics | `/analytics` | `SalesAnalytics.vue` |
| Forecasting Preview | `/forecasting` | `ForecastingPreview.vue` |
| Trajectory Insights | `/trajectory-insights` | `TrajectoryInsights.vue` |
| Reports | `/reports` | `Reports.vue` |
| Users / Access Management | `/users` | `Users.vue` |

Route naming guidance:

- Prefer short business labels over technical labels.
- Use `/bookings`, not `/data/operational-records`, for the new primary records page.
- Use `/analytics`, not `/data/validation`, for business analysis.
- Use `/trajectory-insights` for simulated AI/DSS interpretation.

## Sprint 1 Authentication Routes

Do not add backend authentication endpoints during Sprint 1 frontend work.

Prototype behavior should happen in the frontend:

- Validate against demo accounts.
- Store mock session in localStorage or frontend state.
- Clear mock session on logout.
- Redirect unauthorized users to `/login`.

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

## Future Backend Endpoints

These are future integration ideas, not Sprint 1 requirements.

Authentication:

- `POST /login`
- `POST /logout`
- `GET /user`

Bookings:

- `GET /bookings`
- `POST /bookings`
- `PUT /bookings/{booking}`
- `GET /bookings/{booking}`

Inventory:

- `GET /inventory`
- `POST /inventory`
- `PUT /inventory/{item}`
- `POST /inventory/{item}/adjust`

Expenses:

- `GET /expenses`
- `POST /expenses`
- `PUT /expenses/{expense}`

Reports:

- `GET /reports/{type}`
- `POST /reports/{type}/export`

Forecasting:

- Future backend work may add forecast generation endpoints when Meta Prophet integration is actually implemented.

Important:

- Do not imply these future endpoints exist.
- Do not build them during Sprint 1 frontend-only work unless the user explicitly changes scope.
