# Sprint 1 Modal Interaction Plan

This plan is based on a scan of the current ProphetOps Vue pages and shared components.

Current scope:

- Sprint 1 mockup only.
- No backend work needed.
- No database persistence needed.
- No real export generation needed.
- No real forecasting or AI execution needed.

The goal is to make clicked actions feel complete and professional while keeping the system frontend-only.

## Interaction Rule

Use a drawer when the user needs to add, edit, or review a detailed business record.

Use a modal when the user needs to confirm a short action, preview a small report, read a prototype notice, or make a simple decision.

Use a normal page link when the action naturally moves the user to another full page.

Use inline controls for filters, date ranges, toggles, and simple table/search behavior.

## Active Sprint 1 Pages Scanned

- Login
- Dashboard / `Welcome.vue`
- Bookings
- Inventory
- Expenses
- Sales Analytics
- Forecasting Preview
- Trajectory Insights
- Reports
- Users / Access
- AppShell
- TopBar
- Sidebar
- EmptyState

Legacy pages were also scanned:

- Operational Records
- Data Validation

These legacy pages are not active Sprint 1 pages, but their risky actions are noted in case they are restored later.

## Modal Priority List

### 1. Logout Confirmation Modal

Location:
TopBar / AppShell

Current action:
`Logout`

Recommended behavior:
Open a confirmation modal before clearing the mock session.

Modal content:

- Title: "Log out of ProphetOps?"
- Message: "Your Sprint 1 mock session will be cleared on this device."
- Cancel button: "Stay signed in"
- Confirm button: "Log out"

Why modal:
Logout changes session state and moves the user away from the workspace.

Sprint 1 backend impact:
None. Continue using frontend mock auth.

### 2. Report Preview Modal

Location:
Reports page

Current action:
`View report`

Recommended behavior:
Open a report preview modal instead of doing nothing.

Modal content:

- Report title
- Report type
- Short mock summary
- Included sections
- Sprint 1 label: "Mock report preview"
- Close button

Why modal:
Report previews are short, contained, and do not need a full page during Sprint 1.

Sprint 1 backend impact:
None. Use existing mock report cards.

### 3. Export Placeholder Modal

Location:
Reports page

Current actions:
`Export PDF`, `Export Excel`

Current state:
Disabled.

Recommended behavior:
Keep disabled for the cleanest Sprint 1 experience, or enable them later only to open a placeholder modal.

Modal content if enabled:

- Title: "Export is not available in Sprint 1"
- Message: "PDF and Excel generation are planned for a future phase."
- Primary button: "Close"

Why modal:
If the buttons become clickable, the user needs a clear prototype explanation.

Sprint 1 backend impact:
None. Do not generate files.

### 4. Alerts Modal

Location:
TopBar

Current action:
Bell icon / `View alerts`

Recommended behavior:
Open a small alerts modal or popover showing mock system notices.

Suggested Sprint 1 modal content:

- Low inventory needs review
- Forecasting is sample-only
- Cost monitoring insight available

Why modal:
The button is currently visible but has no clear result. A short modal makes it feel intentional.

Sprint 1 backend impact:
None. Use static/mock alert messages.

### 5. Profile / Access Info Modal

Location:
TopBar

Current action:
Profile button

Recommended behavior:
Open a small access info modal.

Modal content:

- Current demo user
- Role
- Visible pages
- Sprint 1 note: "Frontend mock access only"

Why modal:
Users page is owner-only, but every logged-in user should understand their current mock role.

Sprint 1 backend impact:
None. Read from mock auth service only.

### 6. Unsaved Changes Confirmation Modal

Location:
Bookings, Inventory, Expenses drawers

Current actions:
Drawer close, Cancel, backdrop click

Recommended behavior:
If the drawer form has been edited, show a discard confirmation modal before closing.

Modal content:

- Title: "Discard changes?"
- Message: "The changes in this drawer will not be saved."
- Cancel button: "Keep editing"
- Confirm button: "Discard changes"

Why modal:
Add/edit drawers can contain multiple fields. Closing accidentally should not lose changes silently once form editing becomes richer.

Sprint 1 backend impact:
None. Compare local form state only.

### 7. Save Confirmation Toast, Not Modal

Location:
Bookings, Inventory, Expenses drawers

Current actions:
Save Booking, Save Package, Save Expense

Recommended behavior:
Do not use a modal after save. Use a small success toast or inline confirmation.

Why not modal:
Save is a frequent action. A modal would slow down record entry.

Sprint 1 backend impact:
None.

### 8. Placeholder Action Modal

Location:
Any future Sprint 1 action that looks clickable but is not implemented.

Recommended behavior:
Use one reusable placeholder modal instead of silent buttons.

Modal content:

- Feature name
- Sprint 1 status
- What will happen in a future phase
- Close button

Examples:

- "Export PDF"
- "Generate Forecast"
- "Send Report"
- "Add User"

Sprint 1 backend impact:
None.

## Page-By-Page Classification

| Area | Current action | Recommended Sprint 1 behavior |
| --- | --- | --- |
| Login | Show/hide password | Inline toggle, no modal |
| Login | Demo account buttons | Fill fields inline, no modal |
| Login | Sign In | Validate and redirect, no modal |
| Dashboard | Date range buttons | Inline segmented control, no modal |
| Dashboard | Review Insights | Page link to Trajectory Insights |
| Dashboard | Priority decision buttons | Page links to Inventory, Expenses, Analytics |
| Dashboard | Recent inventory links | Page link to Inventory |
| TopBar | Date pill | Make static or use future date-range popover, no modal needed |
| TopBar | Alert bell | Alerts modal or popover |
| TopBar | Profile button | Profile/access info modal |
| TopBar | Logout | Confirmation modal |
| Bookings | Add Booking | Drawer, already correct |
| Bookings | View booking | Drawer, already correct |
| Bookings | Cancel/close drawer | Optional unsaved changes modal |
| Bookings | Save | Save then close drawer; toast later, not modal |
| Inventory | Add Package | Drawer, already correct |
| Inventory | Adjust Stock | Drawer, already correct |
| Inventory | View package | Drawer, already correct |
| Inventory | Cancel/close drawer | Optional unsaved changes modal |
| Inventory | Save | Save then close drawer; toast later, not modal |
| Expenses | Add Expense | Drawer, already correct |
| Expenses | Edit Expense | Drawer, already correct |
| Expenses | Cancel/close drawer | Optional unsaved changes modal |
| Expenses | Save | Save then close drawer; toast later, not modal |
| Sales Analytics | No action buttons | No modal needed |
| Forecasting Preview | Review Data Requirements | Page link to Bookings |
| Trajectory Insights | No action buttons | No modal needed now |
| Reports | View report | Report preview modal |
| Reports | Export PDF / Excel | Keep disabled or placeholder modal |
| Users | No action buttons | No modal needed now |
| Sidebar | Navigation links | Page links, no modal |
| EmptyState | Add action | Same behavior as page primary action, usually drawer |

## Legacy Page Notes

Operational Records and Data Validation are not active Sprint 1 pages. If they are restored later:

- `Archive Record` should use a confirmation modal.
- `Mark Ready For Reports` can use a lightweight confirmation modal or status toast.
- `Reset Filters`, `Advanced Filters`, `View`, and `Review` should not use modals.
- Add/edit operational record should remain a drawer.

## Reusable Modal Components To Plan

Recommended future components:

- `ConfirmModal`
- `ReportPreviewModal`
- `PlaceholderModal`
- `ProfileAccessModal`
- `AlertsModal`

All modal components should use:

- visible title
- short body copy
- close button
- Escape-to-close behavior
- focus trapped inside the modal
- mobile-friendly width
- clear Cancel/Confirm button order

## Sprint 1 Acceptance Criteria

- No visible button should feel dead when clicked.
- Add/edit/view record actions should stay in drawers.
- Logout should ask for confirmation.
- Reports should preview through a modal.
- Disabled export actions should either stay visibly disabled or explain Sprint 1 limits through a placeholder modal.
- Alerts/profile buttons should open simple mock modals or be changed into non-clickable display elements.
- No modal should require backend data.
- No modal should claim real export, real forecasting, real AI, or real database-backed users.
