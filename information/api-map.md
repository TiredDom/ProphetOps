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
Implemented as Owner DSS Dashboard / Decision Support Overview using `Welcome.vue`.

Permissions:
Frontend-only mock auth and role checks for Sprint 1.

### Legacy Operational Records

Route: `GET /data/operational-records`

Purpose:
Legacy route only. This old URL should no longer be treated as an active Sprint 1 page.

Response:
Redirect to `/bookings`.

New Sprint 1 direction:
Bookings / Transactions is the active replacement. Follow `markdowns/legacy-removal-plan.md` before deleting legacy files or route redirects.

### Legacy Data Validation

Route: `GET /data/validation`

Purpose:
Legacy route only. Standalone Data Validation should no longer be treated as an active Sprint 1 page.

Response:
Redirect to `/analytics`.

New Sprint 1 direction:
Data quality should remain behavior inside active pages. Follow `markdowns/legacy-removal-plan.md` before deleting legacy files or route redirects.

### Inventory

Route: `GET /operations/inventory`

Purpose:
Legacy route name for Inventory.

Response:
Redirect to `/inventory`.

New Sprint 1 direction:
Implemented as package availability, slots, reserved count, sold count, and Low/Critical inventory status.

### Bookings / Transactions

Route: `GET /bookings`

Purpose:
Render the required Sprint 1 Bookings / Transactions page.

Response:
`Inertia::render('Bookings')`

Permissions:
Frontend-only mock role guard. Owner / Management, Admin, and Staff can access this page.

### Expenses / Operational Costs

Route: `GET /expenses`

Purpose:
Render the required Sprint 1 Expenses / Operational Costs page.

Response:
`Inertia::render('Expenses')`

Permissions:
Frontend-only mock role guard. Owner / Management and Admin can access this page.

### Sales Analytics

Route: `GET /analytics`

Purpose:
Render the required Sprint 1 Sales Analytics page.

Response:
`Inertia::render('SalesAnalytics')`

Permissions:
Frontend-only mock role guard. Owner / Management and Admin can access this page.

### Forecasting Preview

Route: `GET /forecasting`

Purpose:
Render the required Sprint 1 Forecasting Preview page.

Response:
`Inertia::render('ForecastingPreview')`

Permissions:
Frontend-only mock role guard. Owner / Management can access this page in the current prototype.

Important:
This route uses sample mock data only. Meta Prophet is not integrated.

Future note:
When real forecasting starts, this route should review stored forecast runs and Prophet output. The detailed plan is in `markdowns/meta-prophet-prescriptive-dss-plan.md`.

### Trajectory Insights

Route: `GET /trajectory-insights`

Purpose:
Render the required Sprint 1 Trajectory Insights page.

Response:
`Inertia::render('TrajectoryInsights')`

Permissions:
Frontend-only mock role guard. Owner / Management can access this page in the current prototype.

Important:
This route uses simulated DSS insight cards only. No real AI generation is implemented.

Future note:
This route should evolve into the prescriptive DSS review page where forecast signals are translated into explainable recommended actions.

### Reports

Route: `GET /reports`

Purpose:
Render the required Sprint 1 Reports page.

Response:
`Inertia::render('Reports')`

Permissions:
Frontend-only mock role guard. Owner / Management and Admin can access this page.

### Users / Access Management

Route: `GET /users`

Purpose:
Render the required Sprint 1 Users / Access Management page.

Response:
`Inertia::render('Users')`

Permissions:
Frontend-only mock role guard. Owner / Management can access this page.

## Target Sprint 1 Page Routes

These routes are now implemented as frontend page routes.

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
- Possible future endpoints include `GET /forecast-runs`, `POST /forecast-runs`, `GET /forecast-runs/{id}`, and `GET /forecast-runs/{id}/prescriptions`.
- Do not add these until backend forecasting integration is explicitly in scope.

Prescriptive DSS:

- Future backend work may add endpoints for stored recommendation cards after forecast output exists.
- Possible future endpoints include `GET /prescriptive-insights`, `PATCH /prescriptive-insights/{id}/status`, and `POST /prescriptive-insights/generate`.
- Recommendations should come from forecast output, inventory/capacity status, and cost movement.

Important:

- Do not imply these future endpoints exist.
- Do not build them during Sprint 1 frontend-only work unless the user explicitly changes scope.
