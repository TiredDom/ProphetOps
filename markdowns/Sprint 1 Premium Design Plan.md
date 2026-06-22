# ProphetOps Sprint 1 Premium Design Plan

Status: historical UI planning reference.

This is no longer the active source of truth for product direction, backend state, or algorithm direction. Read `information/README.md` first. The current system is backend-backed, and TOPSIS is the active capstone algorithm direction.

## Project Context

ProphetOps is an internal Decision Support System for Renan-Tina Travels and Tours, a B2B travel agency.

The agency currently uses fragmented tools such as Messenger, Google Sheets, Gmail, and notebooks. ProphetOps should centralize bookings, inventory, operational costs, sales analytics, reports, and future forecasting.

This is not a public booking website. It is an internal business decision-support dashboard for owners, admins, and travel operations staff.

## Sprint 1 Goal

Build the front-end shell and main pages using mock/sample data.

Forecasting and AI insights can be placeholders, but the app must already feel like a DSS.

The main future research feature is Meta Prophet forecasting with a prescriptive DSS layer. Use `meta-prophet-prescriptive-dss-plan.md` before changing forecasting, dashboard forecast cards, or trajectory/prescriptive insight behavior.

The app should communicate immediately:

- what is happening in the business
- why it matters
- what may happen next
- what action the owner should review

Use the existing project stack and conventions. This project uses Laravel, Inertia, Vue, Vite, and custom CSS.

## Design Direction

Use a minimalist, modern, premium interface inspired by Stripe Dashboard and Attio CRM.

Use Stripe as inspiration for:

- financial clarity
- revenue and expense presentation
- trustworthy dashboard layout
- clean charts and reports
- serious business tone

Use Attio as inspiration for:

- modern record management
- searchable tables
- flexible CRM-like data views
- clean object/detail layouts
- polished but simple interactions

## Visual Standards

Use:

- light-first interface
- soft gray app background
- white panels
- thin borders
- soft shadows only when useful
- clean icons where appropriate
- clear visual hierarchy

Avoid:

- loud gradients
- decorative clutter
- marketing hero sections
- public booking language
- walls of equal-weight cards
- spreadsheet dumps

Color tokens:

- Background: #F7F8FA
- Surface: #FFFFFF
- Primary text: #111827
- Secondary text: #6B7280
- Border: #E5E7EB
- Primary accent: #635BFF or #4F46E5
- Success: #16A34A
- Warning: #D97706
- Danger: #DC2626
- Info: #2563EB

Spacing:

- Use an 8px spacing system.
- Page padding: 24px desktop, 16px mobile
- Card padding: 20px to 24px
- Grid gap: 16px to 20px
- Section gap: 24px to 32px
- Button height: 40px to 44px
- Input height: 40px to 44px
- Border radius: 8px for controls
- Border radius: 10px to 12px for panels

Typography:

- Use Inter, SF Pro, or similar.
- Page title: 28px to 32px
- Section title: 18px to 20px
- Card title: 14px to 16px
- Body text: 14px
- Helper text: 12px to 13px
- No negative letter spacing

Animations:

- Hover/focus transitions: 120ms to 160ms
- Sidebar/nav active transition: 180ms to 220ms
- Optional page content fade: 120ms max
- Card hover: subtle border or shadow only
- No flashy animations

## Authentication Scope

Implement pseudo login only.

Do not build:

- real authentication
- password hashing
- backend sessions
- JWT
- database-backed users

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

After login:

- store a mock session in localStorage or frontend state
- add frontend-only route guarding
- logout clears the mock session
- clearly treat this as a Sprint 1 prototype limitation

Role behavior:

- Owner / Management: can access all pages
- Admin: can access Dashboard, Bookings, Inventory, Expenses, Analytics, and Reports
- Staff: can access Bookings and Inventory only

Navigation should adapt based on role. Restricted pages should not break the UI.

## Required Pages

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

## Primary Navigation

Use a left sidebar on desktop and a collapsible/mobile navigation on small screens.

Navigation items:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Forecasting
- Trajectory Insights
- Reports
- Users

Navbar active state:

- Inactive: transparent background, muted text
- Hover: soft gray background
- Active: light primary-tinted background, primary text, optional left indicator
- Transition: 180ms to 220ms ease-out

## Page 1: Login

Purpose:
Secure internal access prototype.

Include:

- ProphetOps name/logo
- Subtitle: "Decision Support System for Travel Operations"
- Email/username field
- Password field
- Login button
- Demo account hint
- Small note: "Authorized personnel only"

Do not include:

- public registration
- customer booking links
- marketing landing page sections

## Page 2: Owner DSS Dashboard

This is the most important page. Do not make it a generic admin homepage.

Page title:
"Decision Support Overview"

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

Business Gist example:

"Sales are currently above last month's pace, but operating costs are increasing. Recommended action: Review high-cost destinations before approving new promos."

KPI cards:

- Total Revenue
- Total Bookings
- Passenger Count
- Operating Costs
- Estimated Profit
- Low Inventory Packages

Decision cards:

Each decision card must show:

- Insight type: Risk / Opportunity / Warning / Action
- Short finding
- Why it matters
- One clear action button

Example:

- Title: Low Inventory Risk
- Finding: Boracay Package has only 4 slots left.
- Meaning: This may affect expected demand next week.
- Button: Review Inventory

Dashboard density limits:

- Show only 1 main Business Gist card at the top.
- Show maximum 6 KPI cards.
- Show maximum 3 priority decision cards on the first screen.
- Recent transactions should show only 5 rows.
- Recent inventory changes should show only 5 rows.
- Additional insights should appear lower on the page or inside Trajectory Insights.

## Page 3: Bookings / Transactions

Purpose:
Centralize booking records from Messenger, Sheets, Gmail, and notebooks.

Create a clean Attio-like records table.

Fields:

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

- Search
- Filters by date, package, payment status, booking status
- Add Booking button
- View action
- Edit action

DSS purpose:
This page creates the historical data needed for sales monitoring and forecasting.

## Page 4: Inventory

Purpose:
Monitor package availability, slots, or operational stock.

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
- Filter low-stock items
- Warning cards for Low/Critical inventory

DSS behavior:
If stock is low, show:

"Low availability may affect future demand. Review package capacity."

## Page 5: Expenses / Operational Costs

Purpose:
Record costs needed for financial analysis.

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

DSS behavior example:

"Marketing cost increased this period. Compare with booking conversion."

## Page 6: Sales Analytics

Purpose:
Provide basic business analysis before full AI forecasting.

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

## Page 7: Forecasting Preview

Purpose:
Prepare the interface for Meta Prophet integration.

Sprint 1 uses mock data only.

Include:

- 30-day booking projection
- 30-day revenue projection
- Demand trend chart
- Seasonality notes
- Data requirements
- Forecast status card

Required label:

"Sample Forecast Preview - Forecast engine integration pending"

Do not imply that Meta Prophet is already running.

Future direction:

- Real Prophet output should include forecast values, confidence intervals, source range, projection range, and generated timestamp.
- Forecasting should feed the dashboard and the prescriptive DSS layer.
- Follow `meta-prophet-prescriptive-dss-plan.md` before implementing the real forecasting feature.

## Page 8: Trajectory Insights

Purpose:
Show AI/DSS business interpretation.

Sprint 1 can use simulated/mock insight cards.

Categories:

- Sales trend
- Cost risk
- Inventory risk
- Marketing opportunity
- Demand increase

Each insight must include:

- Finding
- Reason
- Suggested action

Insight format:

- Insight Type: Risk / Opportunity / Trend
- Finding: Bookings increased for Package A.
- Reason: Passenger count is higher than the previous period.
- Suggested Action: Prepare inventory and review pricing.

Required labels:

- "Simulated DSS Insight"
- "AI trajectory module placeholder"

Future direction:

- Evolve this page into the prescriptive DSS review space.
- Each recommendation should show forecast signal, business meaning, prescribed action, priority, and evidence.
- Keep recommendations explainable and rule-based first.

## Page 9: Reports

Purpose:
Internal documentation for owners and stakeholders.

Report cards:

- Sales Summary
- Inventory Summary
- Expense Summary
- Forecast Summary
- DSS Evaluation Summary

Actions:

- View report
- Export PDF placeholder
- Export Excel placeholder

Export buttons may be disabled or placeholder for Sprint 1.

## Page 10: Users / Access Management

Purpose:
Support limited internal access.

Show user table:

- Name
- Role
- Email/username
- Status
- Last login

Roles:

- Owner / Management: full access
- Admin: bookings, inventory, expenses, analytics, reports
- Staff: bookings and inventory only

## Mock Data Requirements

Mock data must support the DSS model from the study.

Include:

- ds: booking date
- y: passenger count or demand value
- gross revenue
- operational cost
- marketing cost
- destination/package
- inventory level
- booking status
- payment status

Mock data should feel realistic for a B2B travel and tours agency.

## DSS Logic Rule

Every insight card must connect data to decision-making.

Use:

Observed data -> Business meaning -> Suggested action

Example:

- Observed: Marketing cost increased by 18%.
- Meaning: Spending is rising faster than bookings.
- Action: Review campaign performance.

## Academic / Prototype Labels

All forecasting and AI insight sections must be clearly labeled as simulated or sample for Sprint 1.

Use labels such as:

- Sample Forecast Preview
- Forecast engine integration pending
- Simulated DSS Insight
- AI trajectory module placeholder

Do not imply that Meta Prophet or AI analysis is already running.

## Modal, Drawer, And Page Standards

Use a modal for:

- confirming destructive actions
- logout confirmation if needed
- delete confirmation
- small quick actions with 1 to 3 fields
- simple status changes
- short report export options

Modal rules:

- Keep modals focused on one decision.
- Do not put large forms, charts, or tables inside modals.
- Always include clear Cancel and Confirm actions.
- Destructive actions must use danger styling and clear wording.

Use a right-side drawer / slide-over panel for:

- viewing booking details from a table
- editing booking records
- adding bookings with several fields
- viewing package/inventory details
- adjusting stock
- viewing expense details
- editing expense records
- showing record activity/history

Drawer rules:

- Drawer should preserve page context.
- Desktop drawer width should be around 420px to 560px.
- On mobile, drawer can become a full-screen panel.
- Use a sticky footer for Save/Cancel actions.
- Use sections inside drawers if the form has many fields.
- Do not make users lose their place in a table after closing a drawer.

Use a full page for:

- dashboard
- analytics
- forecasting
- reports
- complex multi-section views
- pages with charts and large tables

Use popovers/dropdowns for:

- filters
- date range selection
- status selection
- sort options
- small contextual menus

Use inline editing only for:

- small status changes
- simple table fields where accidental edits are unlikely

Default rule:

- If the user needs context from the current table, use a drawer.
- If the user needs to confirm a risky action, use a modal.
- If the user needs to analyze information deeply, use a full page.

## Anti-Cramped UI Rules

The interface must prioritize clarity for non-technical users.

Use progressive disclosure:

- Show summaries first.
- Put details inside View Details, tabs, drawers, modals, or dedicated pages.
- Avoid long dense sections on the dashboard.

Page structure:

1. Clear page title
2. Short subtitle explaining the page purpose
3. One primary action button
4. Main content
5. Secondary details below or behind filters/tabs

Do not place too many equal-weight cards beside each other.

Create visual hierarchy:

- Primary: DSS gist, urgent risk, main KPI
- Secondary: supporting charts and tables
- Tertiary: metadata, notes, timestamps

Tables:

- Keep columns practical and readable.
- Use horizontal scrolling only if necessary.
- Hide less important details in row details, drawers, or View actions.
- Do not show every field at once on small screens.
- Dashboard tables must be summaries only, not full data dumps.

Cards:

- One card should communicate one idea only.
- Warning cards must have one clear action button.
- Avoid multiple competing buttons inside the same card.
- Use generous whitespace.

Non-technical user rule:

If a business owner cannot understand the screen in 5 seconds, simplify it.

## Loading And Empty States

Add reusable skeleton loading states for:

- Dashboard KPI cards
- Insight cards
- Charts
- Tables
- Forecast preview

Do not skeleton-load the entire app shell/sidebar every time.

Every data-heavy page should have a polished empty state.

Empty state should include:

- short explanation
- one recommended action
- optional icon

Example:

"No bookings recorded yet. Add your first booking to start building the forecasting dataset."

Button:

"Add Booking"

## Accessibility And UX

- Buttons and controls should be around 40px minimum height.
- Use visible focus states.
- Do not rely on color alone; use labels like Stable, Low, Critical.
- Keep actions close to the insight/problem they solve.
- Use clear business language.
- Avoid technical jargon unless necessary.
- Keep help/support placement consistent.
- Prevent broken or blank pages.

## ISO 25010 Alignment

Functional suitability:
Pages match DSS requirements from the study.

Performance efficiency:
Lightweight UI and no unnecessary heavy effects.

Usability:
Clear navigation, readable cards, obvious actions.

Reliability:
Stable mock states and no broken empty pages.

## Recommended Reusable Components

- AppShell
- Sidebar
- TopBar
- PageHeader
- MetricCard
- InsightCard
- DecisionCard
- StatusBadge
- DataTable
- FilterBar
- SearchInput
- ChartCard
- EmptyState
- SkeletonCard
- DrawerPanel
- ConfirmModal
- FormSection
- RoleGuard

## Acceptance Criteria

- Owner login leads to the DSS Dashboard, not a generic homepage.
- Pseudo login works with demo accounts.
- Mock session persists until logout.
- Role-based navigation works.
- All Sprint 1 pages exist and are navigable.
- Dashboard shows business insights immediately.
- UI feels like Stripe + Attio: clean, premium, modern, trustworthy.
- Mock data is realistic for a B2B travel agency.
- Pages are responsive.
- Navigation active states animate smoothly.
- Skeleton states exist for data-heavy sections.
- Empty states exist.
- Forecasting and AI pages clearly indicate they are Sprint 1 previews.
- The UI does not feel crowded on desktop or mobile.
- Dashboard is glanceable within 5 seconds.
- First screen shows only the most important DSS information.
- Details are available, but not forced onto the user immediately.
- Each page has one obvious primary action.
- No page should look like a wall of cards or a spreadsheet dump.
