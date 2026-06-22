# ProphetOps Page-By-Page Implementation Guide

Status: historical page planning reference.

Read `information/README.md` first. The current system is already backend-backed, and TOPSIS is the active capstone algorithm direction. Use this file only for old UI/page intent, not current backend or algorithm instructions.

## Shared Rules

Use the existing Laravel + Inertia + Vue stack.

Use mock/sample data for Sprint 1.

Use a light-first premium business UI inspired by Stripe Dashboard and Attio CRM.

Every page should have:

1. Clear page title
2. Short subtitle
3. One primary action
4. Main content
5. Secondary details below or behind filters, tabs, drawers, or modals

Every DSS insight should use:

Observed data -> Business meaning -> Suggested action

Forecasting and AI prototype status should be explained in documentation and presentation notes. Visible app screens should avoid labels such as "mockup", "mock", "preview", "sample only", and "placeholder".

For clicked actions, use `sprint-1-modal-interaction-plan.md`.

Sprint 1 interaction summary:

- Use drawers for Add, Edit, View, and Adjust actions on business records.
- Use modals for logout confirmation, report viewing, availability notices, alerts, profile/access information, and discard-changes confirmation.
- Use normal page links for dashboard decision actions and navigation.
- Keep filters and date ranges inline.
- Use `modular-graph-system-plan.md` before adding or refactoring graphs.

For removing old pages and route names, use `legacy-removal-plan.md`.

## Shared Navigation

Primary labels:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Forecasting
- Trajectory Insights
- Reports
- Users

Role visibility:

- Owner / Management: all pages
- Admin: Dashboard, Bookings, Inventory, Expenses, Analytics, Forecasting, Trajectory Insights, Reports
- Staff: Bookings and Inventory only

## 1. Login

Page title:
ProphetOps

Subtitle:
"Decision Support System for Travel Operations"

Purpose:
Provide internal access for the Sprint 1 prototype.

Primary action:
Login

Include:

- email/username field
- password field
- demo account hint
- "Authorized personnel only" note
- validation and error states
- simple loading state

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

Acceptance criteria:

- Valid demo account logs in.
- Invalid credentials show a clear message.
- Owner/Admin redirect to Dashboard.
- Staff redirects to Bookings.
- Logout clears mock session.
- No public registration or customer booking links appear.

## 2. Owner DSS Dashboard

Page title:
"Decision Support Overview"

Purpose:
Give owners and management a decision cockpit, not a generic homepage.

Primary action:
Review Priority Decisions

Include:

- Date range filter: Today, This Month, This Quarter
- Last updated timestamp
- Local Intranet status
- Business Gist / DSS Insight Summary card
- KPI cards
- Priority decision cards
- Sales trend chart
- Revenue vs expenses chart
- Top performing packages
- Forecast preview using mock data
- Recent transactions
- Recent inventory changes

KPI cards:

- Total Revenue
- Total Bookings
- Passenger Count
- Operating Costs
- Estimated Profit
- Low Inventory Packages

Density limits:

- 1 main Business Gist card
- maximum 6 KPI cards
- maximum 3 priority decision cards on first screen
- 5 recent transaction rows
- 5 recent inventory change rows

Decision card format:

- Insight type
- Finding
- Why it matters
- One action button

Acceptance criteria:

- The first screen answers what is happening, why it matters, and what should be reviewed.
- Forecast preview is clearly marked as sample.
- No chart or table overwhelms the first screen.

## 3. Bookings / Transactions

Page title:
"Bookings & Transactions"

Purpose:
Centralize booking records from Messenger, Sheets, Gmail, and notebooks.

Primary action:
Add Booking

Table fields:

- Booking ID
- Booking date
- Client / agency partner
- Destination / package
- Passenger count
- Gross revenue
- Payment status
- Booking status
- Staff assigned
- Notes

Include:

- search
- filters by date, package, payment status, booking status
- View action
- Edit action
- right-side drawer for add/edit/view
- empty state for no bookings

DSS purpose:
This page creates historical data for sales monitoring and future forecasting.

Acceptance criteria:

- Records are searchable and filterable.
- Table is readable on desktop.
- Mobile view avoids showing every column at once.
- Booking detail/edit preserves table context through a drawer.
- Mock data includes `ds`, `y`, gross revenue, package, booking status, and payment status.

## 4. Inventory

Page title:
"Inventory"

Purpose:
Monitor package availability, slots, and operational stock.

Primary action:
Add Package

Fields:

- Package name
- Destination
- Available slots
- Sold count
- Reserved count
- Status: Normal, Low, Critical
- Last updated

Include:

- Add package
- Adjust stock
- View related bookings
- Low-stock filter
- warning cards for Low/Critical inventory
- right-side drawer for details and stock adjustment

DSS behavior:

"Low availability may affect future demand. Review package capacity."

Acceptance criteria:

- Low and Critical states are labeled, not color-only.
- Inventory changes appear in dashboard summaries.
- Package availability connects to Bookings where possible.

## 5. Expenses / Operational Costs

Page title:
"Expenses & Operational Costs"

Purpose:
Record costs needed for financial analysis.

Primary action:
Add Expense

Fields:

- Expense date
- Category: tour operations, marketing, seasonal cost, overhead
- Amount
- Related destination/package
- Payment status
- Notes

Include:

- Add expense
- Edit expense
- Filter by category/date
- Summary cards
- Insight card when costs increase
- right-side drawer for add/edit/view

DSS behavior:

"Marketing cost increased this period. Compare with booking conversion."

Acceptance criteria:

- Costs can be understood by category and period.
- Expense summaries feed dashboard profit and cost cards.
- Cost insights follow observed data -> meaning -> action.

## 6. Sales Analytics

Page title:
"Sales Analytics"

Purpose:
Provide basic business analysis before full AI forecasting.

Primary action:
Review Top Routes

Include:

- Monthly sales chart
- Booking volume chart
- Revenue by destination
- Top packages table
- Revenue vs expenses comparison
- Summary cards

Summary cards:

- Top Revenue Route
- Highest Passenger Volume
- Most Active Month
- Costliest Category

Acceptance criteria:

- Analytics uses mock booking and expense data.
- Charts are readable and not decorative.
- Summary cards explain business meaning.
- Page does not claim to run AI or Prophet.

## 7. Forecasting

Page title:
"Forecasting"

Purpose:
Prepare the interface for Meta Prophet integration and future forecast-run review.

Primary action:
Review Prescriptive Insights

Presentation UI rule:
Do not show visible labels such as "mockup", "mock", "preview", "sample only", "placeholder", or "forecast engine integration pending" on the app screen. Keep those disclosures in documentation, presenter script, or paper limitations.

Include:

- One main demand forecast line graph
- Actual demand line
- Forecast demand line
- Light confidence range
- Compact forecast summary cards
- Prescriptive DSS signal near the graph

Graph rule:
Use a modular chart shell and reusable line graph. The graph must stay contained inside its panel on desktop and mobile.

Future real integration:

- Use Meta Prophet to forecast demand, booking volume, or revenue.
- Store generated forecast runs before showing them on the dashboard.
- Show generated timestamp, source date range, projection range, and model status.
- Link forecast signals to prescriptive DSS recommendations.

Acceptance criteria:

- Page uses mock data only.
- Visible app copy stays professional and does not repeatedly say the page is mock/prototype/sample.
- Visible page copy stays business-facing and does not show formulas.
- No copy implies real Meta Prophet output exists.
- Simplification follows `forecasting-page-simplification-plan.md`.
- Future planning follows `meta-prophet-prescriptive-dss-plan.md`.

## 8. Trajectory Insights

Page title:
"Trajectory Insights"

Purpose:
Show DSS business interpretation now and evolve into prescriptive forecast-driven recommendations.

Primary action:
Review Suggested Actions

Presentation UI rule:
Do not show visible labels that call the page simulated, AI placeholder, mock, or preview. Keep those disclosures in documentation, presenter script, or paper limitations.

Insight categories:

- Sales trend
- Cost risk
- Inventory risk
- Marketing opportunity
- Demand increase

Each insight includes:

- Finding
- Reason
- Suggested action

Future prescriptive DSS format:

- Forecast signal or observed data
- Business meaning
- Prescribed action
- Priority
- Evidence
- Affected package or destination
- Time horizon

Acceptance criteria:

- Every card follows observed data -> meaning -> action.
- Insights are explainable and documentation clarifies Sprint 1 uses prototype data.
- Suggestions are practical for travel operations.
- No real AI generation is implied.
- Future planning follows `meta-prophet-prescriptive-dss-plan.md`.

## 9. Reports

Page title:
"Reports"

Purpose:
Provide internal documentation for owners and stakeholders.

Primary action:
View Sales Summary

Report cards:

- Sales Summary
- Inventory Summary
- Expense Summary
- Forecast Summary
- DSS Evaluation Summary

Actions:

- View report
- Export PDF
- Export Excel

Acceptance criteria:

- Export actions open a short availability modal if not implemented yet, without using placeholder wording on the main page.
- Reports use mock data.
- Report summaries are business-readable.
- Forecast report prototype status is documented outside the visible app UI.

## 10. Users / Access Management

Page title:
"Users & Access"

Purpose:
Support limited internal access.

Primary action:
Review Access Roles

Table fields:

- Name
- Role
- Email/username
- Status
- Last login

Roles:

- Owner / Management: full access
- Admin: bookings, inventory, expenses, analytics, forecasting, trajectory insights, reports
- Staff: bookings and inventory only

Acceptance criteria:

- User table shows the three demo users.
- Role access is clear and consistent with navigation.
- Page does not imply real database-backed user management exists.
- Any add/edit user action is disabled, hidden, or marked prototype-only unless requested.

## Recommended Implementation Order

1. Update pseudo login and role model.
2. Update app shell and navigation labels.
3. Rework dashboard into Decision Support Overview.
4. Build centralized mock data.
5. Keep legacy Operational Records replaced by Bookings / Transactions.
6. Align Inventory to package availability.
7. Add Expenses.
8. Add Sales Analytics.
9. Add Forecasting.
10. Add Trajectory Insights.
11. Add Reports.
12. Add Users / Access Management.
13. Add shared skeleton, empty, drawer, modal, status badge, and table components where helpful.

## Out Of Scope

- Real authentication
- Real database persistence
- Real forecasting engine integration
- Real AI generation
- Real export generation
- External API integrations
- Customer-facing booking pages
- Public website pages

## Final Reminder

If the page does not help an owner understand what is happening, why it matters, or what to review next, simplify it.
