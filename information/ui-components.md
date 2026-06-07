# ProphetOps UI Components

This file tracks reusable UI patterns for the Sprint 1 DSS prototype.

## Current Extracted Components

### AppIcon

Path:
`resources/js/Components/icons/AppIcon.vue`

Purpose:
Provides reusable line icons for navigation, stat cards, buttons, panel headers, and empty states.

Current use:
Sidebar, TopBar, StatCard, ContentPanel, EmptyState, Login, Dashboard, legacy Operational Records, legacy Data Validation, Inventory.

Sprint 1 direction:
Keep using this component unless an icon library is intentionally added. Add icons for Bookings, Expenses, Analytics, Forecasting, Trajectory Insights, Reports, Users, drawers, alerts, and exports as needed.

### Sidebar

Path:
`resources/js/Components/layout/Sidebar.vue`

Purpose:
Displays the grouped left navigation for the internal ProphetOps workspace.

Sprint 1 direction:
Update navigation to the required labels:

- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Forecasting
- Trajectory Insights
- Reports
- Users

It should support role-based visibility, desktop sidebar behavior, and collapsible/mobile navigation.

### TopBar

Path:
`resources/js/Components/layout/TopBar.vue`

Purpose:
Displays the page header area, mobile menu trigger, and account/status actions.

Sprint 1 direction:
Keep the top bar stable across pages. It can show last updated time, local intranet status, profile/logout controls, or page-level context when useful.

### StatCard

Path:
`resources/js/Components/dashboard/StatCard.vue`

Purpose:
Displays compact summary values.

Sprint 1 direction:
Can evolve into `MetricCard`. It should support revenue, bookings, passenger count, operating costs, estimated profit, and low inventory packages.

### ContentPanel

Path:
`resources/js/Components/dashboard/ContentPanel.vue`

Purpose:
Provides a reusable solid panel with title, optional eyebrow/badge, and body content.

Sprint 1 direction:
Use for charts, table summaries, report cards, forecast preview sections, and readable DSS panels.

### EmptyState

Path:
`resources/js/Components/feedback/EmptyState.vue`

Purpose:
Displays a useful empty state with an icon, title, helper text, and optional action.

Sprint 1 direction:
Every data-heavy page should include a polished empty state with one recommended action.

## Recommended Sprint 1 Components

Build or extract these when implementation needs them:

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

## Component Behavior Standards

### PageHeader

Each page should have:

- clear title
- short subtitle
- one primary action button
- optional last updated or status text

### MetricCard

Use for KPI summaries.

Rules:

- One metric per card.
- Short label.
- Clear value.
- Helper text only when it adds business meaning.
- Avoid more than 6 KPI cards on the dashboard.

### InsightCard

Use for DSS interpretation.

Required structure:

- Observed data
- Business meaning
- Suggested action

Labels:

- Risk
- Opportunity
- Warning
- Action
- Trend

### DecisionCard

Use on the dashboard for priority owner review items.

Each card must include:

- Insight type
- Short finding
- Why it matters
- One clear action button

Dashboard limit:
Show up to 3 priority decision cards on the first screen.

### DataTable

Use for Bookings, Inventory, Expenses, Reports, and Users.

Rules:

- Keep columns practical and readable.
- Use horizontal scrolling only when necessary.
- Hide less important details in drawers or row details.
- Dashboard tables must show summaries only.

### DrawerPanel

Use for record detail and medium/large forms.

Use cases:

- View booking details
- Add/edit booking
- View package/inventory details
- Adjust stock
- View/edit expense
- Show record activity/history

Rules:

- Preserve page context.
- Desktop width around 420px to 560px.
- On mobile, become full-screen.
- Use sticky Save/Cancel footer.
- Use sections for longer forms.

### ConfirmModal

Use for risky confirmation only.

Use cases:

- Delete confirmation
- Logout confirmation if needed
- Short export options
- Simple status changes

Rules:

- Keep focused on one decision.
- Always include Cancel and Confirm.
- Destructive actions use danger styling and clear wording.
- Do not put large forms, charts, or tables inside modals.

### SkeletonCard

Use skeleton loading for:

- Dashboard KPI cards
- Insight cards
- Charts
- Tables
- Forecast preview

Do not skeleton-load the full app shell/sidebar on every page transition.

### RoleGuard

Use for Sprint 1 prototype access behavior.

Rules:

- Owner / Management: all pages
- Admin: Dashboard, Bookings, Inventory, Expenses, Analytics, Reports
- Staff: Bookings and Inventory only

Restricted pages should fail gracefully.

## Visual Standards

Use:

- background: #F7F8FA
- surface: #FFFFFF
- primary text: #111827
- secondary text: #6B7280
- border: #E5E7EB
- primary accent: #635BFF or #4F46E5
- success: #16A34A
- warning: #D97706
- danger: #DC2626
- info: #2563EB

Spacing:

- 8px spacing system
- 24px desktop page padding
- 16px mobile page padding
- 20px to 24px card padding
- 16px to 20px grid gaps
- 40px to 44px button/input height

## Existing Page-Level Patterns To Reuse Carefully

Existing Operational Records, Data Validation, and Inventory pages contain useful patterns:

- responsive sidebar shell
- summary cards
- table-to-card mobile behavior
- right-side drawers
- advanced filters behind disclosure
- detail panels
- clear empty states

New Sprint 1 implementation can reuse these patterns, but should align labels and information architecture to the new required pages.

Important:

- Operational Records should become Bookings / Transactions direction.
- Data Validation should become data quality behavior, not a required standalone page.
- Inventory remains a required page and should align to package slots and operational availability.
