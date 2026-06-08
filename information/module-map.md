# ProphetOps Module Map

This map describes the current project shape and the new Sprint 1 target direction. Use it with `information/sprint-1-direction.md`.

## Project Scaffolding

Purpose:
Foundation setup for a Laravel + Inertia + Vue internal decision-support dashboard.

Main paths:

- Backend: `app/`, `config/`, `routes/`, `database/`
- Frontend: `resources/js/`, `resources/css/`
- Entry points: `resources/js/app.js`, `resources/views/app.blade.php`
- Config: `vite.config.js`, `.env`, `composer.json`, `package.json`

Current status:

- Laravel + Inertia + Vue app is scaffolded.
- Vue pages are in `resources/js/Pages/`.
- Vue components are in `resources/js/Components/`.
- Current styling uses custom CSS in `resources/css/app.css`.
- No real backend feature persistence is implemented yet.

Important notes:

- Keep using the existing stack and conventions.
- Sprint 1 should remain front-end focused with mock/sample data.
- Do not introduce a public website, customer booking portal, payment flow, or external API integration during Sprint 1.

## Authentication / Account Access

Purpose:
Provide prototype-only internal access before entering the ProphetOps workspace.

Main paths:

- Frontend page: `resources/js/Pages/Login.vue`
- Mock auth service: `resources/js/services/mockAuth.js`
- Page routes: `routes/web.php`
- Styling: `resources/css/app.css`

Current status:

- A frontend login page and mock auth service exist.
- Current behavior uses the required demo accounts and role-based access from the new Sprint 1 plan.

Sprint 1 target:

- Demo accounts:
  - owner@prophetops.local / owner123
  - admin@prophetops.local / admin123
  - staff@prophetops.local / staff123
- Store a mock session in localStorage or frontend state.
- Guard routes on the frontend only.
- Logout clears mock session.
- Navigation adapts based on role.

Current implementation:

- `resources/js/services/mockAuth.js` defines demo users, role permissions, default role redirects, and frontend route guards.
- `resources/js/Components/layout/AppShell.vue` applies role-aware navigation and logout behavior on new Sprint 1 pages.

Out of scope:

- Real backend auth
- Password hashing
- Backend sessions
- JWT
- Database-backed users

## Owner DSS Dashboard

Purpose:
Serve as the decision cockpit for owners and management.

Current path:

- Existing page: `resources/js/Pages/Welcome.vue`
- Current route: `GET /dashboard`

Current status:

- Reworked into "Decision Support Overview".
- Shows one Business Gist / DSS Insight Summary near the top.
- Shows 6 KPI cards.
- Shows 3 priority decision cards.
- Includes sales trend, revenue vs expenses, top packages, forecast preview, recent transactions, and recent inventory changes using mock data.

Important notes:

- This should not feel like a generic admin homepage.
- It must answer what is happening, why it matters, and what should be reviewed next.
- Forecast and AI areas must be labeled as sample or simulated.
- Future dashboard forecasting behavior must follow `markdowns/meta-prophet-prescriptive-dss-plan.md`.
- When real forecasting exists, the dashboard should show the latest forecast-driven gist and the top three prescriptive actions, not a dense forecasting workspace.

## Bookings / Transactions

Purpose:
Centralize booking records from Messenger, Google Sheets, Gmail, notebooks, and manual encoding.

Legacy predecessor:

- `resources/js/Pages/OperationalRecords.vue` is legacy and should be removed after active pages are verified.
- Existing route `GET /data/operational-records` is legacy direction and currently redirects to `/bookings`.
- Follow `markdowns/legacy-removal-plan.md` for cleanup.

Sprint 1 target:
Current status:

- Implemented as `resources/js/Pages/Bookings.vue`.
- Current route: `GET /bookings`.
- Uses centralized mock data, search, filters, table, and drawer add/edit/view behavior.

Sprint 1 behavior:

- Create an Attio-like records table.
- Fields: Booking ID, booking date, client/agency partner, destination/package, passenger count, gross revenue, payment status, booking status, staff assigned, notes.
- Include search, date/package/payment/status filters, Add Booking, View, and Edit actions.
- Use a right-side drawer for add/edit/detail workflows.

DSS purpose:

- This page creates the historical data needed for sales monitoring and future forecasting.

## Inventory

Purpose:
Monitor package availability, slots, and operational stock.

Current path:

- Existing page: `resources/js/Pages/Inventory.vue`
- Current route: `GET /inventory`

Current status:

- Align inventory language to travel package availability and operational stock.
- Fields: package name, destination, available slots, sold count, reserved count, status, last updated.
- Include Add Package, Adjust Stock, View Related Bookings, low-stock filter, and Low/Critical warning cards.

DSS behavior:

- If stock is low, show this message or equivalent:
  "Low availability may affect future demand. Review package capacity."

## Expenses / Operational Costs

Purpose:
Record costs needed for financial analysis and DSS interpretation.

Current status:

- Implemented as `resources/js/Pages/Expenses.vue`.
- Current route: `GET /expenses`.

Sprint 1 target:

- Create a page for expense date, category, amount, related destination/package, payment status, and notes.
- Categories: tour operations, marketing, seasonal cost, overhead.
- Include Add Expense, Edit Expense, category/date filters, summary cards, and insight card when costs increase.

DSS behavior:

- Example: "Marketing cost increased this period. Compare with booking conversion."

## Sales Analytics

Purpose:
Provide basic business analysis before full AI forecasting.

Current status:

- Implemented as `resources/js/Pages/SalesAnalytics.vue`.
- Current route: `GET /analytics`.

Sprint 1 target:

- Include monthly sales chart, booking volume chart, revenue by destination, top packages table, revenue vs expenses comparison, and summary cards.
- Summary cards: Top Revenue Route, Highest Passenger Volume, Most Active Month, Costliest Category.

## Forecasting Preview

Purpose:
Prepare the interface for future Meta Prophet integration and forecast-run review.

Current status:

- Implemented as `resources/js/Pages/ForecastingPreview.vue`.
- Current route: `GET /forecasting`.

Sprint 1 target:

- Use mock data only.
- Include 30-day booking projection, 30-day revenue projection, demand trend chart, seasonality notes, data requirements, and forecast status card.
- Required label: "Sample Forecast Preview - Forecast engine integration pending".
- Plan future model output around `ds`, `y`, `yhat`, `yhat_lower`, and `yhat_upper`.
- The detailed forecasting plan lives in `markdowns/meta-prophet-prescriptive-dss-plan.md`.

Important note:

- Do not imply Meta Prophet is already running.

## Trajectory Insights

Purpose:
Show simulated DSS interpretation now and evolve into the prescriptive recommendation layer for forecast-driven decisions.

Current status:

- Implemented as `resources/js/Pages/TrajectoryInsights.vue`.
- Current route: `GET /trajectory-insights`.

Sprint 1 target:

- Use simulated insight cards.
- Categories: sales trend, cost risk, inventory risk, marketing opportunity, demand increase.
- Each insight includes finding, reason, and suggested action.
- Required labels: "Simulated DSS Insight" and "AI trajectory module placeholder".
- Future insight cards should include forecast signal, business meaning, prescribed action, priority, evidence, affected package/destination, and time horizon.
- Use `markdowns/meta-prophet-prescriptive-dss-plan.md` as the source of truth for this evolution.

## Reports

Purpose:
Provide internal documentation cards for owners and stakeholders.

Current status:

- Implemented as `resources/js/Pages/Reports.vue`.
- Current route: `GET /reports`.

Sprint 1 target:

- Report cards: Sales Summary, Inventory Summary, Expense Summary, Forecast Summary, DSS Evaluation Summary.
- Actions: View report, Export PDF placeholder, Export Excel placeholder.
- Export buttons may be disabled or placeholder-only.

## Users / Access Management

Purpose:
Support limited internal access in the prototype.

Current status:

- Implemented as `resources/js/Pages/Users.vue`.
- Current route: `GET /users`.

Sprint 1 target:

- Show user table with name, role, email/username, status, and last login.
- Roles: Owner / Management, Admin, Staff.
- Keep this prototype-only until real backend authentication is requested.

## Data Validation

Purpose:
Data quality remains important, but it is no longer a required standalone Sprint 1 page in the new plan.

Current path:

- Existing page: `resources/js/Pages/DataValidation.vue` is legacy and should be removed after active pages are verified.
- Current legacy route: `GET /data/validation`, redirecting to `/analytics`.

New direction:

- Keep validation ideas as behavior inside Bookings, Inventory, Expenses, Analytics, Forecasting Preview, and Reports.
- Do not make Data Validation compete with the required Sprint 1 pages unless the user asks to restore it.
- Follow `markdowns/legacy-removal-plan.md` for cleanup.

## Navigation

Required labels:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Forecasting
- Trajectory Insights
- Reports
- Users

Current path:

- `resources/js/data/navigation.js`
- `resources/js/Components/layout/Sidebar.vue`

Target behavior:

- Sidebar on desktop.
- Collapsible/mobile navigation on small screens.
- Role-based visibility.
- Restricted pages should not break the UI.
