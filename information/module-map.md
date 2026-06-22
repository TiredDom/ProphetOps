# ProphetOps Module Map

This map describes the current project shape after the local backend implementation. Use it with `information/sprint-1-direction.md`.

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
- Core backend persistence is implemented for users, bookings, package presets, and expenses.

Important notes:

- Keep using the existing stack and conventions.
- Keep the Laravel + Inertia + Vue structure simple and modular.
- Do not introduce a public website, customer booking portal, payment flow, or external API integration unless the user explicitly changes scope.

## Algorithm / TOPSIS Decision Support

Purpose:
Provide the active capstone algorithm direction.

Current source of truth:

- `information/topsis-decision-support-plan.md`

Current status:

- TOPSIS is the active algorithm direction.
- Meta Prophet is historical unless the team explicitly restores forecasting.
- The system should rank travel package or operations alternatives through explainable criteria and weights.
- Current package records support the first TOPSIS ranking module.
- The visible owner-facing Package Decision Guide is available at `/decision-guide`; `/forecasting` is only a compatibility redirect.

Expected decisions:

- Which package should staff recommend for a client or agency partner.
- Which package should admins prioritize, monitor, or promote.
- Which package has risk because of low slots, weak data, or low business value.
- Which option best fits budget, destination, duration, capacity, supplier reliability, and business value.

Important notes:

- Do not depend on Facebook or supplier APIs.
- Fragmented source data from sheets, messages, posters, and manual communication is part of the research problem.
- Keep TOPSIS outputs explainable in business language, not formula-heavy UI copy.

## Authentication / Account Access

Purpose:
Provide internal access before entering the ProphetOps workspace.

Main paths:

- Frontend page: `resources/js/Pages/Login.vue`
- Backend controller: `app/Http/Controllers/AuthController.php`
- Role middleware: `app/Http/Middleware/EnsureRoleCanAccess.php`
- Shared Inertia auth props: `app/Http/Middleware/HandleInertiaRequests.php`
- Frontend access helpers: `resources/js/services/authAccess.js`
- Page routes: `routes/web.php`
- Styling: `resources/css/app.css`

Current status:

- The login page now posts to Laravel authentication.
- Laravel sessions, hashed demo passwords, and backend role checks are active.
- Navigation adapts from Inertia auth props instead of browser session storage.

Current target:

- Demo accounts:
  - owner@prophetops.local / owner123
  - admin@prophetops.local / admin123
  - staff@prophetops.local / staff123
- Use Laravel sessions only.
- Remove remember-device behavior.
- Keep Login as an intentional sign-in page for guests.
- Guard protected routes on the backend with `auth` and `role.access`.
- Logout invalidates the Laravel session.
- Navigation adapts based on role.

Current implementation:

- `app/Support/ProphetOpsData.php` defines demo accounts, role permissions, default role paths, and shared data transforms.
- `resources/js/Components/layout/AppShell.vue` applies role-aware navigation and posts logout to `/logout`.

Out of scope:

- JWT
- Self-service user creation
- Public registration

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
- Includes sales trend, revenue vs expenses, top packages, TOPSIS-ready decision preview, recent transactions, and recent inventory changes using saved database records.

Important notes:

- This should not feel like a generic admin homepage.
- It must answer what is happening, why it matters, and what should be reviewed next.
- Forecast and AI areas must not imply that Meta Prophet or real AI is already running.
- Future dashboard DSS behavior should follow `information/topsis-decision-support-plan.md`.
- When TOPSIS ranking exists, the dashboard should show the best decision gist and the top three review actions, not a dense algorithm workspace.

## Bookings / Transactions

Purpose:
Centralize booking records from Messenger, Google Sheets, Gmail, notebooks, and manual encoding.

Legacy predecessor:

- `resources/js/Pages/OperationalRecords.vue` was removed.
- Old legacy redirects were removed. `/bookings` is the only active records route.

Current status:

- Implemented as `resources/js/Pages/Bookings.vue`.
- Current route: `GET /bookings`.
- Uses database-backed records, search, filters, table, drawer add/edit/view behavior, and bulk updates.

Current behavior:

- Create an Attio-like records table.
- Fields: Booking ID, booking date, client/agency partner, destination/package, passenger count, gross revenue, payment status, booking status, staff assigned, notes.
- Include search, date/package/payment/status filters, Add Booking, View, and Edit actions.
- Use a right-side drawer for add/edit/detail workflows.

DSS purpose:

- This page creates the historical data needed for sales monitoring and decision-support ranking.

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
- Data is stored in `travel_packages`.

DSS behavior:

- If stock is low, show this message or equivalent:
  "Low availability may affect package decisions. Review package capacity."

## Expenses / Operational Costs

Purpose:
Record costs needed for financial analysis and DSS interpretation.

Current status:

- Implemented as `resources/js/Pages/Expenses.vue`.
- Current route: `GET /expenses`.
- Data is stored in `expenses`.

Current behavior:

- Create a page for expense date, category, amount, related destination/package, payment status, and notes.
- Categories: tour operations, marketing, seasonal cost, overhead.
- Include Add Expense, Edit Expense, category/date filters, summary cards, and insight card when costs increase.

DSS behavior:

- Example: "Marketing cost increased this period. Compare with booking conversion."

## Sales Analytics

Purpose:
Provide basic business analysis for DSS interpretation and TOPSIS-ready decision support.

Current status:

- Implemented as `resources/js/Pages/SalesAnalytics.vue`.
- Current route: `GET /analytics`.

Current behavior:

- Include monthly sales chart, booking volume chart, revenue by destination, top packages table, revenue vs expenses comparison, and summary cards.
- Summary cards: Top Revenue Route, Highest Passenger Volume, Most Active Month, Costliest Category.

## Package Decision Guide

Purpose:
Compare package alternatives using TOPSIS criteria and explainable business output.

Current status:

- Implemented as `resources/js/Pages/ForecastingPreview.vue`.
- Current route: `GET /decision-guide`.
- Old route: `GET /forecasting` redirects to `/decision-guide`.

Current target:

- Use saved package catalog data and TOPSIS criteria fields.
- Accept budget, destination, duration, and travel type priority inputs.
- Show compared alternatives, fit score/closeness coefficient, criteria weights, and explanation.
- Keep formulas/details in documentation or tests instead of formula-heavy UI copy.
- The backend ranking service lives in `app/Support/TopsisDecisionSupport.php`.
- The active algorithm plan lives in `information/topsis-decision-support-plan.md`.

Important note:

- Do not imply Meta Prophet, AI generation, or external supplier API automation is running.

## DSS Review Signals

Purpose:
Support dashboard and report review cards with explainable DSS-style signals.

Current status:

- No standalone route. The former Trajectory Insights page was removed to keep the navigation focused.
- Helper data still lives in `app/Support/ProphetOpsData.php` for dashboard and reports.

Current behavior:

- Use saved bookings, packages, and expenses to build current DSS-style insight cards.
- Categories: sales trend, cost risk, inventory risk, marketing opportunity, demand increase.
- Each insight includes finding, reason, and suggested action.
- Avoid visible UI labels such as "simulated", "AI placeholder", "mock", "preview", and "sample only".
- Future insight cards should include criterion signal, business meaning, prescribed action, priority, evidence, affected package/destination, and time horizon.
- Use `information/topsis-decision-support-plan.md` as the source of truth for this evolution.

## Reports

Purpose:
Provide internal documentation cards for owners and stakeholders.

Current status:

- Implemented as `resources/js/Pages/Reports.vue`.
- Current route: `GET /reports`.

Current behavior:

- Report cards: Sales Summary, Inventory Summary, Expense Summary, TOPSIS Decision Summary, DSS Evaluation Summary.
- Actions: View report, Export PDF, Export Excel.
- Export actions may open an availability modal if not implemented yet, but the main UI should avoid placeholder wording.

## Users / Access Management

Purpose:
Support limited internal access.

Current status:

- Implemented as `resources/js/Pages/Users.vue`.
- Current route: `GET /users`.
- User records come from the backend.

Current behavior:

- Show user table with name, role, email/username, status, and last login.
- Roles: Owner / Management, Admin, Staff.
- Keep user self-service and public registration out of scope.

## Data Validation

Purpose:
Data quality remains important, but it is no longer a required standalone Sprint 1 page in the new plan.

Previous path:

- Existing page `resources/js/Pages/DataValidation.vue` was removed.
- Old legacy redirects were removed. `/analytics` is the active analysis route.

New direction:

- Keep validation ideas as behavior inside Bookings, Inventory, Expenses, Analytics, Package Decision Guide, and Reports.
- Do not make Data Validation compete with the required Sprint 1 pages unless the user asks to restore it.

## Navigation

Required labels:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Package Decision Guide
- Reports
- Users

Current path:

- `resources/js/data/navigation.js`
- `resources/js/Components/layout/Sidebar.vue`

Target behavior:

- Sidebar on desktop.
- Collapsible/mobile navigation on small screens.
- Role-based visibility.
- Sprint 1 demo access: Owner sees all pages, Admin sees Dashboard through Reports including Package Decision Guide, Staff sees Bookings and Inventory.
- Restricted pages should not break the UI.
