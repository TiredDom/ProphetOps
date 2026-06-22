# ProphetOps Sprint 1 Direction

This file is legacy Sprint 1 planning with current-state corrections. For the quickest current source of truth, read `information/README.md` first.

Current active capstone direction:

- Title: `ProphetOps: A Business Decision Support System Using TOPSIS for Travel Operations Management`
- TOPSIS is the active algorithm direction.
- Meta Prophet and AI forecasting plans are historical unless the team explicitly restores them.
- The current app is backend-backed with Laravel sessions and SQLite local persistence.

## Product Identity

ProphetOps is an internal Decision Support System for Renan-Tina Travels and Tours, a B2B travel agency.

It is not a public booking website, customer portal, payment gateway, or marketing site. It exists to help owners, admins, and operations staff understand bookings, package availability, costs, sales, reports, and TOPSIS-supported operational choices.

The app should answer three business questions quickly:

- What is happening in the business?
- Why does it matter?
- What should the owner or staff review next?

## Current Build Goal

Maintain the working Laravel + Inertia + Vue system and add a defensible TOPSIS decision-support module.

Use the existing Laravel + Inertia + Vue stack and current project conventions. Backend persistence, session login, and role access are already implemented. Do not build external supplier APIs, customer-facing booking pages, payment flows, real AI generation, or Meta Prophet forecasting unless explicitly requested later.

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
7. Package Decision Guide
8. Reports
9. Users / Access Management

Current implementation note:

- Existing `Welcome.vue` should be treated as the current dashboard shell to rework into Owner DSS Dashboard.
- Legacy `OperationalRecords.vue` has been removed. Bookings / Transactions is the active records page.
- Legacy `DataValidation.vue` has been removed. Keep data quality behavior inside the relevant data pages and future backend validation work.
- Existing `Inventory.vue` remains relevant, but it should align to package availability and operational stock language.
- Legacy cleanup should follow `markdowns/legacy-removal-plan.md`.

## Authentication Scope

Authentication now uses Laravel sessions and seeded internal users.

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

Expected behavior:

- Use Laravel sessions only.
- Do not add remember-device behavior.
- Authenticated users can redirect to their role default page.
- Guard protected routes on the backend with `auth` and `role.access`.
- Logout invalidates the backend session.
- Navigation adapts by role.
- Restricted pages should not break the UI.

Do not build JWT, public registration, self-service user creation, or enterprise production security unless explicitly requested later.

## Role Access

Owner / Management:

- Can access all pages.

Admin:

- Can access Dashboard, Bookings, Inventory, Expenses, Analytics, Package Decision Guide, and Reports.

Staff:

- Can access Bookings and Inventory only.

## Required Navigation

Primary navigation labels:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Package Decision Guide
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

Decision-support presentation wording:

- Do not show repeated visible labels such as "mockup", "mock", "preview", "sample only", "placeholder", or "forecast engine integration pending" inside the app UI.
- Use polished product-facing labels such as "Decision Support", "Package Decision Guide", "Business Priority", and "Recommended Review".
- Keep limitations in documentation, presenter script, paper limitations, and AI handoff notes.

Do not imply that Meta Prophet or an AI module is running.

## TOPSIS Decision Support Direction

The active research feature is TOPSIS-based travel operations decision support.

Use `information/topsis-decision-support-plan.md` before changing Dashboard, Package Decision Guide, Package Catalog, Reports, or decision-support behavior.

Feature direction:

- TOPSIS should rank package or operational alternatives from standardized internal data.
- The DSS layer should convert criteria scores into explainable recommended actions.
- Dashboard recommendations should be based on package fit, capacity status, supplier reliability, cost, and business value.
- Every recommendation must show the criteria, business meaning, and suggested action.
- Visible app UI should not look like a rough mockup during presentation.
- Package Decision Guide, dashboard, analytics, and report graphs should follow `markdowns/modular-graph-system-plan.md` so charts stay contained and reusable.

## Saved Data Requirements

Saved data must feel realistic for a B2B travel and tours agency and support TOPSIS ranking.

Include:

- ds: booking date
- y: passenger count or demand value, where still used in analytics
- gross revenue
- operational cost
- marketing cost
- destination/package
- inventory level
- booking status
- payment status
- supplier reliability
- package duration
- available slots
- package price

Use backend records and transforms through `app/Support/ProphetOpsData.php`.

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
- Package Decision Guide
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
- Package Decision Guide charts

Do not skeleton-load the app shell/sidebar every time. The navigation should stay stable.

Every data-heavy page needs a polished empty state with:

- Short explanation
- One recommended action
- Optional icon

Example:

"No bookings recorded yet. Add your first booking to start building the decision-support dataset."

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
- TOPSIS and DSS status is documented outside the visible app UI; app screens use polished product-facing labels.
- Dashboard is glanceable within 5 seconds.
- First screen shows only the most important DSS information.
- Each page has one obvious primary action.
- No page looks like a wall of cards or a spreadsheet dump.
