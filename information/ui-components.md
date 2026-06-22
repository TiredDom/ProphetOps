# ProphetOps UI Components

This file tracks reusable UI patterns for the current ProphetOps DSS prototype.

## Current Extracted Components

### AppIcon

Path:
`resources/js/Components/icons/AppIcon.vue`

Purpose:
Provides reusable line icons for navigation, stat cards, buttons, panel headers, and empty states.

Current use:
Sidebar, TopBar, StatCard, ContentPanel, EmptyState, Login, Dashboard, Bookings, Inventory, Expenses, Analytics, Package Decision Guide, Reports, and Users.

Sprint 1 direction:
Keep using this component unless an icon library is intentionally added. Add icons for Bookings, Expenses, Analytics, Package Decision Guide, Reports, Users, drawers, alerts, and exports as needed.

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
- Package Decision Guide
- Reports
- Users

It should support role-based visibility, desktop sidebar behavior, and collapsible/mobile navigation.

### TopBar

Path:
`resources/js/Components/layout/TopBar.vue`

Purpose:
Displays the page header area, mobile menu trigger, and account/status actions.

Sprint 1 direction:
Keep the top bar stable across pages. It now supports user role/profile display and logout controls through the shared app shell.

### AppShell

Path:
`resources/js/Components/layout/AppShell.vue`

Purpose:
Wraps current pages with the sidebar, top bar, role-aware navigation, shared Inertia auth props, and logout behavior.

Used In:
Dashboard, Bookings, Inventory, Expenses, Analytics, Package Decision Guide, Reports, and Users.

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
Use for table summaries, report cards, and readable DSS panels. For charts, prefer the future chart components from `markdowns/modular-graph-system-plan.md` so every graph stays contained and consistent.

### Chart Components

Path:
`resources/js/Components/charts/`

Purpose:
Provide reusable graph shells and chart types for Package Decision Guide, Dashboard, Analytics, and Reports.

Current components:

- `ChartPanel`
- `LineTrendChart`
- `MiniBarChart`
- `ComparisonTrack`

Sprint 1 direction:
Use these shared chart components instead of page-specific chart markup when a chart appears on Dashboard, Package Decision Guide, Analytics, or Reports.

Containment rules:

- Every graph must have a stable height.
- Every graph must stay inside its card/panel.
- No chart should create horizontal page overflow.
- Axis labels and legends must stay inside the graph frame.
- Long descriptions should be moved to DSS signal cards, modals, documentation, or presentation notes.

### EmptyState

Path:
`resources/js/Components/feedback/EmptyState.vue`

Purpose:
Displays a useful empty state with an icon, title, helper text, and optional action.

Sprint 1 direction:
Every data-heavy page should include a polished empty state with one recommended action.

### ActionNotice

Path:
`resources/js/Components/feedback/ActionNotice.vue`

Purpose:
Displays reusable inline feedback for form errors, save confirmations, warnings, and page-level status messages.

Sprint 1 direction:
Use this instead of one-off alert markup. Every notice should have a clear title, short business-readable message, semantic tone, and accessible live-region behavior.

### AppModal

Path:
`resources/js/Components/feedback/AppModal.vue`

Purpose:
Provides the shared focused dialog pattern for logout, report preview, export preparation, alerts, profile/access information, and the workspace guide.

Sprint 1 direction:
Use modals for short focused decisions or information. Do not place large forms, charts, or dense tables inside modals.

### AppDrawer

Path:
`resources/js/Components/feedback/AppDrawer.vue`

Purpose:
Provides the shared right-side drawer pattern for record detail, add/edit, and review workflows.

Sprint 1 direction:
Use drawers when the user needs table context while editing or reviewing a business record.

### Record Components

Path:
`resources/js/Components/records/`

Current components:

- `FilterBar`
- `DataTableFrame`
- `BulkActionBar`

Purpose:
Keeps record-heavy pages consistent with reusable filter layout, keyboard-focusable table frames, and multi-select action handling.

Sprint 1 direction:
Use these on Bookings, Inventory, Expenses, Users, Reports, and other dense record pages instead of duplicating table shell, filter, or selection-action markup.

## Recommended Sprint 1 Components

Build or extract these when implementation needs them:

- AppShell: implemented
- Sidebar
- TopBar
- PageHeader
- MetricCard
- InsightCard
- DecisionCard
- StatusBadge
- DataTableFrame: implemented
- FilterBar: implemented
- BulkActionBar: implemented
- SearchInput
- ChartCard
- ChartPanel: implemented
- LineTrendChart: implemented
- MiniBarChart: implemented
- ComparisonTrack: implemented
- EmptyState
- ActionNotice: implemented
- SkeletonCard
- AppDrawer: implemented
- AppModal: implemented
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

Future prescriptive DSS structure:

- TOPSIS criterion signal or observed data
- Business meaning
- Prescribed action
- Priority
- Evidence
- Affected package or destination
- Time horizon

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
- Short review title
- One concise business sentence
- One clear action button

Do not show separate Signal, Meaning, Evidence, and Action labels on dashboard cards. Put the full explanation inside focused guide drawers, report details, or the relevant record page.

When connected to TOPSIS later, each dashboard decision card should come from the latest ranking output or prescriptive DSS insight and should remain explainable.

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
- Discarding unsaved drawer changes

Rules:

- Keep focused on one decision.
- Always include Cancel and Confirm.
- Destructive actions use danger styling and clear wording.
- Do not put large forms, charts, or tables inside modals.

Sprint 1 modal plan:
Use `markdowns/sprint-1-modal-interaction-plan.md` before implementing modal behavior.

Recommended modal variants:

- Logout confirmation
- Report preview
- Export/feature placeholder
- Alerts
- Profile/access information
- Unsaved changes confirmation

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
- Admin: Dashboard, Bookings, Inventory, Expenses, Analytics, Package Decision Guide, Reports
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

Legacy Operational Records and Data Validation contained useful patterns that should stay folded into the current pages:

- responsive sidebar shell
- summary cards
- table-to-card mobile behavior
- right-side drawers
- advanced filters behind disclosure
- detail panels
- clear empty states

Current implementation can reuse these patterns, but should align labels and information architecture to the active required pages.

Important:

- Operational Records should become Bookings / Transactions direction.
- Data Validation should become data quality behavior, not a required standalone page.
- Inventory remains a required page and should align to package slots and operational availability.
- Legacy page and CSS cleanup should follow `markdowns/legacy-removal-plan.md`.
