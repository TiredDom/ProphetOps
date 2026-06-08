# Sprint 1 Legacy Removal Plan

This plan removes old project direction without breaking the active Sprint 1 mockup.

Current scope:

- Planning only.
- No backend work needed.
- No database work needed.
- No deletion should happen until the active Sprint 1 pages are verified.

## What Counts As Legacy

Legacy direction:

- Standalone Operational Records page
- Standalone Data Validation page
- Old `/operations/inventory` route naming
- Old page language that centered generic operational records instead of bookings, expenses, inventory, analytics, forecasting, reports, and DSS insights

Active Sprint 1 replacements:

- Operational Records -> Bookings / Transactions
- Data Validation -> data-quality behavior inside Bookings, Inventory, Expenses, Analytics, Forecasting Preview, and Reports
- `/operations/inventory` -> `/inventory`
- Package references as a separate planning requirement -> Inventory and Bookings package context

## Current Project State

Active pages now exist:

- Login
- Dashboard
- Bookings
- Inventory
- Expenses
- Analytics
- Forecasting
- Trajectory Insights
- Reports
- Users

Legacy routes already have a soft-removal layer:

- `/data/operational-records` redirects to `/bookings`
- `/data/validation` redirects to `/analytics`
- `/operations/inventory` redirects to `/inventory`

Remaining legacy items:

- `resources/js/Pages/OperationalRecords.vue`
- `resources/js/Pages/DataValidation.vue`
- legacy-only CSS for old records and validation layouts
- docs that still describe legacy routes/pages as current workspaces

## Recommended Removal Strategy

Use a staged removal.

Stage 1: Soft removal

- Keep redirects for old URLs so existing links do not break.
- Remove old legacy labels from active docs.
- Make it clear that Bookings, Analytics, and embedded data quality behavior are the active replacements.

Stage 2: Hard removal

- Delete legacy page files after active pages pass visual and route checks.
- Remove legacy-only CSS blocks.
- Keep shared drawer/table/status styles used by active pages.
- Keep historical feature logs and fix logs as history.

Stage 3: Final cleanup

- Search again for legacy references.
- Update remaining non-historical references.
- Run frontend build.
- Check active routes.
- Optionally keep legacy redirects for convenience, or remove them if the user wants strict route cleanup.

## Files To Remove Later

Remove after verification:

- `resources/js/Pages/OperationalRecords.vue`
- `resources/js/Pages/DataValidation.vue`

Do not remove yet:

- `resources/js/Pages/Bookings.vue`
- `resources/js/Pages/Inventory.vue`
- `resources/js/Pages/Expenses.vue`
- `resources/js/Pages/SalesAnalytics.vue`
- `resources/js/Pages/ForecastingPreview.vue`
- `resources/js/Pages/TrajectoryInsights.vue`
- `resources/js/Pages/Reports.vue`
- `resources/js/Pages/Users.vue`

## CSS Cleanup Plan

Remove legacy-only CSS after deleting the legacy page files.

Likely legacy-only groups:

- `.records-page`
- `.records-layout`
- `.records-table-panel`
- `.records-toolbar`
- `.records-table-wrapper`
- `.records-table`
- `.records-side-panel`
- `.validation-page`
- `.validation-intro`
- `.validation-summary-grid`
- `.validation-layout`
- `.validation-queue-panel`
- `.validation-toolbar`
- `.validation-table-wrapper`
- `.validation-table`
- `.validation-empty`
- `.validation-toast`
- `.validation-card-list`
- `.validation-review-card`
- `.simple-validation-*`
- `.simple-records-*`

Do not remove shared styles used by active Sprint 1 pages:

- `.drawer-backdrop`
- `.record-drawer`
- `.sprint-drawer`
- `.drawer-header`
- `.drawer-actions`
- `.record-badge`
- `.table-scroll`
- `.dss-table`
- `.module-intro`
- `.dss-filterbar`
- `.stat-grid`
- `.dss-kpi-grid`

Before deleting any CSS block, search for the class in active files.

## Route Plan

Current redirect behavior is acceptable for Sprint 1:

- `/data/operational-records` -> `/bookings`
- `/data/validation` -> `/analytics`
- `/operations/inventory` -> `/inventory`

Recommended Sprint 1 choice:

- Keep redirects even after deleting legacy pages.
- Do not show these routes in navigation.
- Do not mention them in user-facing UI.

Optional strict cleanup later:

- Remove the redirect routes after the final demo if the project should not support old links.

## Docs Cleanup Plan

Update active planning docs so they no longer describe legacy pages as current workspaces.

Update:

- `information/api-map.md`
- `information/module-map.md`
- `information/sprint-1-direction.md`
- `information/ui-components.md`
- `markdowns/README.md`
- `markdowns/page-by-page implementation guide.md`
- `markdowns/general knowledge.md`

Keep historical references in:

- `information/feature-log.md`
- `information/fix-log.md`

Reason:
History logs should remain honest about what happened before the Sprint 1 reset.

## What To Preserve From Legacy

Keep the useful ideas, not the old pages.

Preserve:

- drawer-based add/edit pattern
- table-to-mobile-card thinking
- guided review language
- data quality awareness
- status badges
- empty states

Move those ideas into active Sprint 1 pages:

- Bookings
- Inventory
- Expenses
- Reports
- Forecasting Preview

Do not preserve:

- standalone Operational Records navigation
- standalone Data Validation navigation
- old route names as primary routes
- old backend plans for `operational_records`
- old page labels that compete with Bookings / Transactions

## Recommended Implementation Sequence

1. Confirm active replacement pages work.
2. Keep redirects in `routes/web.php`.
3. Delete `OperationalRecords.vue`.
4. Delete `DataValidation.vue`.
5. Search for references to deleted components.
6. Remove legacy-only CSS blocks.
7. Update active docs.
8. Run build.
9. Check active routes:
   - `/login`
   - `/dashboard`
   - `/bookings`
   - `/inventory`
   - `/expenses`
   - `/analytics`
   - `/forecasting`
   - `/trajectory-insights`
   - `/reports`
   - `/users`
10. Check legacy redirects still land on active pages.

## Acceptance Criteria

- No active navigation item points to a legacy route.
- No current page tells the user to open Operational Records or Data Validation.
- Bookings is the main record-entry page.
- Data quality is described as behavior, not a standalone page.
- Legacy Vue page files are removed after verification.
- Legacy-only CSS is removed without breaking active pages.
- Historical logs are preserved.
- Build passes.
- Active routes load correctly.
- Legacy URLs either redirect cleanly or are intentionally removed.
