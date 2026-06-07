# ProphetOps Sprint 1 Direction

This is the current source of truth for Sprint 1 planning. Use it before changing code.

## Product Identity

ProphetOps is an internal Decision Support System for Renan-Tina Travels and Tours, a B2B travel agency.

It is not a public booking website, customer portal, payment gateway, or marketing site. It exists to help owners, admins, and operations staff understand bookings, inventory, costs, sales, reports, and future forecast readiness.

The app should answer three business questions quickly:

- What is happening in the business?
- Why does it matter?
- What should the owner or staff review next?

## Sprint 1 Goal

Build the front-end shell and main pages using mock/sample data. The system should already feel like a DSS even though forecasting and AI are placeholders.

Use the existing Laravel + Inertia + Vue stack and current project conventions. Do not build backend persistence, real authentication, real forecasting, real AI generation, real exports, external integrations, or customer-facing pages during Sprint 1 unless explicitly requested later.

## Design Direction

Use a minimalist, modern, premium interface inspired by Stripe Dashboard and Attio CRM.

Stripe influence:

- Financial clarity
- Revenue and expense presentation
- Trustworthy dashboard layout
- Clean charts and reports
- Serious business tone

Attio influence:

- Modern record management
- Searchable tables
- Flexible CRM-like data views
- Clean object/detail layouts
- Polished but simple interactions

Visual standards:

- Light-first interface
- Soft gray app background
- White panels
- Thin borders
- Subtle shadows only when useful
- No loud gradients
- No decorative clutter
- Clear icons where appropriate

## Required Sprint 1 Pages

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

Current implementation note:

- Existing `Welcome.vue` should be treated as the current dashboard shell to rework into Owner DSS Dashboard.
- Existing `OperationalRecords.vue` should be treated as a legacy predecessor to Bookings / Transactions.
- Existing `DataValidation.vue` is not a required standalone Sprint 1 page in the new plan. Keep data quality behavior inside the relevant data pages and future backend validation work.
- Existing `Inventory.vue` remains relevant, but it should align to package availability and operational stock language.

## Pseudo Login Scope

Sprint 1 authentication is prototype-only.

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

Expected behavior:

- Store a mock session in localStorage or frontend state.
- Add frontend-only route guarding.
- Logout clears the mock session.
- Navigation adapts by role.
- Restricted pages should not break the UI.

Do not build real password hashing, backend sessions, JWT, database-backed users, or production security in Sprint 1.

## Role Access

Owner / Management:

- Can access all pages.

Admin:

- Can access Dashboard, Bookings, Inventory, Expenses, Analytics, and Reports.

Staff:

- Can access Bookings and Inventory only.

## Required Navigation

Primary navigation labels:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Forecasting
- Trajectory Insights
- Reports
- Users

Use a left sidebar on desktop and collapsible/mobile navigation on small screens.

Navigation states:

- Inactive: transparent background, muted text
- Hover: soft gray background
- Active: light primary-tinted background, primary text, optional left indicator
- Transition: 180ms to 220ms ease-out

## DSS Logic Rule

Every insight must connect data to decision-making.

Required structure:

Observed data -> Business meaning -> Suggested action

Example:

- Observed data: Marketing cost increased by 18%.
- Business meaning: Spending is rising faster than bookings.
- Suggested action: Review campaign performance.

Forecasting and AI labels must be explicit:

- Sample Forecast Preview
- Forecast engine integration pending
- Simulated DSS Insight
- AI trajectory module placeholder

Do not imply that Meta Prophet or an AI module is already running.

## Mock Data Requirements

Mock data must feel realistic for a B2B travel and tours agency and support later forecasting.

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

Centralize mock data so later backend integration can replace it cleanly.

## Page Structure Standard

Every page should have:

1. Clear page title
2. Short subtitle explaining the page purpose
3. One primary action button
4. Main content
5. Secondary details below or behind filters, tabs, drawers, or modals

Use progressive disclosure:

- Show summaries first.
- Put details inside View Details, tabs, drawers, modals, or dedicated pages.
- Avoid long dense sections on the dashboard.
- Do not make any page feel like a spreadsheet dump.

## Interaction Standards

Use a modal for:

- Confirming destructive actions
- Logout confirmation if needed
- Delete confirmation
- Small quick actions with 1 to 3 fields
- Simple status changes
- Short report export options

Use a right-side drawer or slide-over panel for:

- Viewing booking details from a table
- Editing booking records
- Adding bookings with several fields
- Viewing package/inventory details
- Adjusting stock
- Viewing expense details
- Editing expense records
- Showing record activity/history

Use a full page for:

- Dashboard
- Analytics
- Forecasting
- Reports
- Complex multi-section views
- Pages with charts and large tables

Use popovers/dropdowns for:

- Filters
- Date ranges
- Status selection
- Sort options
- Small contextual menus

Default rule:

- If the user needs context from the current table, use a drawer.
- If the user needs to confirm a risky action, use a modal.
- If the user needs to analyze deeply, use a full page.

## Visual Tokens

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
- Button/input height: 40px to 44px
- Border radius: 8px for controls
- Border radius: 10px to 12px for panels

Typography:

- Use Inter, SF Pro, or a similar font.
- Page title: 28px to 32px
- Section title: 18px to 20px
- Card title: 14px to 16px
- Body text: 14px
- Helper text: 12px to 13px
- No negative letter spacing

## Loading, Empty, And Error States

Add reusable skeleton states for:

- Dashboard KPI cards
- Insight cards
- Charts
- Tables
- Forecast preview

Do not skeleton-load the app shell/sidebar every time. The navigation should stay stable.

Every data-heavy page needs a polished empty state with:

- Short explanation
- One recommended action
- Optional icon

Example:

"No bookings recorded yet. Add your first booking to start building the forecasting dataset."

Action:

"Add Booking"

## ISO 25010 Alignment

Functional suitability:

- Pages must match DSS requirements from the study.

Performance efficiency:

- Keep UI lightweight and avoid unnecessary heavy effects.

Usability:

- Navigation must be clear, cards readable, and actions obvious.

Reliability:

- Mock states must be stable and no page should be broken or blank.

## Acceptance Criteria

- Owner login leads to the DSS Dashboard, not a generic homepage.
- Pseudo login works with the demo accounts.
- Mock session persists until logout.
- Role-based navigation works.
- All Sprint 1 pages exist and are navigable.
- Dashboard shows business insights immediately.
- UI feels clean, premium, modern, and trustworthy.
- Mock data is realistic for a B2B travel agency.
- Pages are responsive.
- Navigation active states animate smoothly.
- Skeleton states exist for data-heavy sections.
- Empty states exist.
- Forecasting and AI pages clearly indicate Sprint 1 preview status.
- Dashboard is glanceable within 5 seconds.
- First screen shows only the most important DSS information.
- Each page has one obvious primary action.
- No page looks like a wall of cards or a spreadsheet dump.
