# ProphetOps UI Components

## Extracted Components

## AppIcon

Purpose:
Provides reusable line icons for navigation, stat cards, buttons, panel headers, and empty states.

Path:
resources/js/Components/icons/AppIcon.vue

Used In:
Sidebar, TopBar, StatCard, ContentPanel, EmptyState, Welcome page actions.

Notes:
Created as a local icon component because no icon library is currently wired into the project. If the team later installs Lucide, this component can be replaced or mapped to Lucide icons.

## Sidebar

Purpose:
Displays the grouped left navigation for the internal ProphetOps workspace.

Path:
resources/js/Components/layout/Sidebar.vue

Used In:
resources/js/Pages/Welcome.vue

Notes:
Uses the grouped navigation structure and keeps Forecasting as a locked placeholder. Supports responsive drawer behavior through the `isOpen` prop and emits `item-selected` so the parent page can close the drawer after a nav selection.

## TopBar

Purpose:
Displays the sticky page header, date action, alert action, and profile action.

Path:
resources/js/Components/layout/TopBar.vue

Used In:
resources/js/Pages/Welcome.vue

Notes:
Uses subtle glass treatment, which matches the Premium Clarity rules for top navigation surfaces. Includes the responsive hamburger/menu trigger through the `toggle-sidebar` event.

## StatCard

Purpose:
Displays compact dashboard summary values.

Path:
resources/js/Components/dashboard/StatCard.vue

Used In:
resources/js/Pages/Welcome.vue

Notes:
Uses solid white panels for readability.

## ContentPanel

Purpose:
Provides a reusable solid dashboard panel with eyebrow, title, optional badge, and body slot.

Path:
resources/js/Components/dashboard/ContentPanel.vue

Used In:
resources/js/Pages/Welcome.vue

Notes:
Used for Operational Records Intake, Data Quality Status, and Basic Summaries.

## EmptyState

Purpose:
Displays a useful empty state with an icon, title, helper text, and optional action.

Path:
resources/js/Components/feedback/EmptyState.vue

Used In:
resources/js/Pages/Welcome.vue

Notes:
Used to avoid bare zero-value states and guide users toward adding operational records.

Planned components for Sprint 1 (from Sprint 1 Premium Design Plan):

- AppLayout
- PageHeader
- DataTable
- FilterBar
- SearchInput
- FormInput
- SelectField
- DateField
- Modal
- ConfirmationDialog
- StatusBadge
- LoadingState
- ErrorState

Components will be placed in resources/js/Components/{category}/.
