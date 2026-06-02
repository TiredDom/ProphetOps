# ProphetOps Page-By-Page Implementation Guide

## Purpose

This guide explains how each major ProphetOps page should be implemented based on the thesis scope, current sidebar navigation, and Premium Clarity Dashboard design direction.

Use this before building page routes, Vue pages, forms, tables, modals, database screens, and future report or forecasting views.

## Product Context

ProphetOps is a localized internal Decision Support System for Renan-Tina Travels and Tours.

The system centralizes messy internal operational records from sources such as Facebook Messenger, Google Sheets, Gmail, paper records, and notebooks. It supports operational data intake, validation, sales and expense monitoring, inventory tracking, internal reports, and later Meta Prophet forecasting with AI-driven trajectory insights.

## Scope Rule

ProphetOps must remain internal-facing.

Do not design or implement ProphetOps as:

- a customer booking website
- an online reservation engine
- a payment gateway system
- a customer portal
- an external booking API platform
- a public travel package marketplace

Travel or passenger-related information should be treated only as internal historical operational data for reports, decision support, and future forecasting.

## Design Rule

Every page should follow the Premium Clarity Dashboard style:

- Apple/Cupertino-inspired
- clean and professional
- premium internal SaaS dashboard
- subtle frosted or layered navigation
- solid readable data panels
- restrained shadows
- crisp typography
- calm blue and teal accent system
- highly readable tables, forms, reports, and cards
- responsive layout with working sidebar and hamburger behavior

Do not make pages look like marketing landing pages. ProphetOps is an internal operational dashboard.

## Shared Page Structure

Most pages should follow this structure:

1. App layout with grouped sidebar and top bar.
2. Compact page header with title, short description, and primary action.
3. Summary cards only when they help the page purpose.
4. Filter or search bar when the page contains records.
5. Main solid data panel with table, list, form, or report content.
6. Empty state with useful guidance when no data exists.
7. Modal or side panel for create, edit, review, or confirmation actions.
8. Responsive behavior for tablet and mobile-width browser checks.

## Shared Role Expectations

Administrator:

- Full access to all pages.
- Can create, edit, archive, delete where policy allows, and manage settings/users.

Owner / Management:

- Can view operational performance, review records, inspect reports, and monitor inventory.
- Editing may be limited depending on policy.

Operational Staff:

- Can encode daily operational records.
- Can update validation-related fields where permitted.
- Can record inventory movements and permitted expenses.
- Should not manage users or system settings unless explicitly allowed.

## Shared Status Language

Use these terms consistently:

- Draft
- Needs Review
- Validated
- Archived
- Active
- Inactive
- Low Stock
- Out of Stock
- Planned
- Locked

Use `Operational Records` instead of `Bookings` for internal data intake screens.

---

# 1. Dashboard

## Page Purpose

The Dashboard is the first operational overview screen after login. It shows the readiness of the internal Decision Support System, summarizes current operational records, expenses, inventory, reports, and future forecasting readiness.

It supports the thesis by showing how scattered operational data can become centralized, visible, and decision-ready.

## Sprint Scope

Sprint 1.

The dashboard should show foundation-level summaries only. It should not perform prediction or AI analysis yet.

## Main User Actions

Administrator:

- View overall system readiness.
- Add an operational record.
- Review data validation status.
- Navigate to operational modules.
- Check setup progress.

Owner / Management:

- View sales, expense, inventory, and data-quality snapshots.
- Check whether the system is ready for reports and future forecasting.

Operational Staff:

- See what data still needs to be encoded or validated.
- Use quick actions to add records or review data.

## Key UI Sections

- Compact page header with current user context.
- System readiness or internal DSS status summary.
- Primary action: Add Record.
- Secondary action: Review Data.
- Summary stat cards:
  - Total Sales
  - Operational Records
  - Expenses
  - Low Stock
- Data Foundation Checklist.
- Operational Records Intake source summary.
- Data Quality Status panel.
- Basic Reports preview panel.
- Locked or planned Forecasting indicator.

## Data Fields

Dashboard values should be derived from:

- operational record count
- validated record count
- total sales / net amount
- total expenses
- inventory item count
- low stock item count
- draft records
- needs review records
- report availability

## Empty State

When no data exists, the dashboard should not feel blank.

Show:

- No records encoded yet.
- Start by adding an operational record.
- Data Foundation Checklist.
- Source channel readiness.
- Empty stat card values such as `0` or `PHP 0.00`.

## Statuses Or Filters

Useful dashboard statuses:

- System Ready
- Data Intake Ready
- Validation Structured
- Reports Basic
- Forecasting Planned

No complex filtering is required in Sprint 1, but a global date range picker may be planned.

## Acceptance Criteria

- Dashboard loads after login.
- Dashboard uses the Premium Clarity layout.
- Summary cards show readable values and helper text.
- Empty-data state guides users toward setup actions.
- Forecasting is clearly planned or locked.
- No customer-facing or reservation language appears.
- Responsive layout works on desktop, tablet, and mobile-width browser sizes.

## Design Notes

- Keep the intro compact.
- Use solid cards for operational values.
- Use subtle frosted treatment only on navigation and top bar.
- Use calm blue for primary actions.
- Use teal/green for data readiness and validation.
- Avoid oversized hero styling.

## Future Notes

Later sprints may add:

- live chart summaries
- forecasting readiness score
- Prophet model status
- trajectory insight cards
- trend direction indicators

Do not add predictive claims during Sprint 1.

---

# 2. Operational Records

## Page Purpose

The Operational Records page is the main intake workspace for messy internal records from Messenger, Google Sheets, Gmail, paper records, and notebooks.

It supports the thesis by replacing fragmented manual records with structured operational data that can later feed reports and forecasting.

## Sprint Scope

Sprint 1.

This is one of the most important Sprint 1 pages because future reports and forecasting depend on clean operational records.

## Main User Actions

Administrator:

- Add, view, edit, archive, and delete or restore records depending on policy.
- Review all records from all source channels.
- Assign validation status.

Owner / Management:

- Review operational records.
- Filter by date, source channel, package/destination, payment status, or validation status.

Operational Staff:

- Encode records from internal sources.
- Update payment status and notes.
- Save records as Draft or Needs Review.

## Key UI Sections

- Page header with Add Record button.
- Summary cards:
  - Total Records
  - Draft
  - Needs Review
  - Validated
- Filter bar:
  - search
  - date range
  - source channel
  - validation status
  - payment status
  - package/destination
- Operational records table.
- Add/Edit Record modal or page.
- View Record details panel.
- Archive confirmation dialog.
- Empty state for no records.

## Data Fields

Important fields:

- record_date
- client_source_name
- package_destination_id
- passenger_count
- gross_amount
- discount_amount
- net_amount
- payment_status
- payment_method
- source_channel
- validation_status
- notes
- encoded_by
- created_at
- updated_at

Optional fields:

- reference_code
- source_link_or_reference
- attachment_note
- reviewed_by
- reviewed_at

## Empty State

Show:

- No operational records encoded yet.
- Start by adding the first internal record from Messenger, Sheets, Gmail, or paper notes.
- Primary action: Add Record.
- Secondary action: View Data Validation Guide or Source Channels.

The empty state should feel like a clean admin workflow, not a tutorial banner.

## Statuses Or Filters

Validation statuses:

- Draft
- Needs Review
- Validated
- Archived

Source channels:

- Messenger
- Google Sheets
- Gmail
- Paper Record
- Notebook
- Manual Encoding

Payment statuses:

- Unpaid
- Partial
- Paid
- Refunded
- Cancelled or Void if needed for internal historical cleanup

## Acceptance Criteria

- Users can add an operational record.
- Required fields are validated.
- Records appear in a readable table.
- Users can search and filter records.
- Users can view record details.
- Validation status is visible.
- Source channel is visible.
- Empty state is useful before records exist.
- No wording implies a customer booking engine.

## Design Notes

- Keep the table solid white with high contrast.
- Use compact rows with readable 14px text.
- Use status badges for validation and payment states.
- Use a side panel or modal for record details.
- Make Add Record the dominant action.
- Do not overuse glass effects.

## Future Notes

Later sprints may add:

- bulk import helpers
- CSV cleanup tools
- forecasting input tagging
- anomaly warnings
- AI-assisted trajectory notes

Do not connect this page to external booking APIs.

---

# 3. Data Validation

## Page Purpose

The Data Validation page helps users review and classify operational records so only clean records are used in reports and future forecasting.

It supports the thesis by improving data quality before analytics and Meta Prophet forecasting.

## Sprint Scope

Sprint 1.

Validation workflow should exist before forecasting begins.

## Main User Actions

Administrator:

- Review all records.
- Change validation statuses.
- Archive invalid or duplicate records.
- Assign correction notes.

Owner / Management:

- Review records that need checking.
- Confirm that key records are validated.

Operational Staff:

- See records needing correction.
- Update missing fields if permitted.
- Mark records for review.

## Key UI Sections

- Page header with validation summary.
- Status tabs:
  - Draft
  - Needs Review
  - Validated
  - Archived
- Data quality summary cards.
- Records needing review table.
- Missing field indicators.
- Validation checklist panel.
- Record review modal or side panel.
- Empty state for no records needing review.

## Data Fields

Important fields:

- record_id
- record_date
- source_channel
- client_source_name
- package_destination
- passenger_count
- net_amount
- payment_status
- validation_status
- validation_notes
- encoded_by
- reviewed_by
- reviewed_at

Data quality indicators:

- missing date
- missing amount
- missing passenger count
- missing package/destination reference
- invalid amount
- duplicate candidate

## Empty State

If no records exist:

- No records available for validation.
- Add operational records first to begin validation.

If all records are validated:

- All available records are validated.
- Validated records can now support reports.

## Statuses Or Filters

Statuses:

- Draft
- Needs Review
- Validated
- Archived

Filters:

- source channel
- date range
- missing fields
- encoded by
- reviewed by
- package/destination

## Acceptance Criteria

- Records are grouped by validation status.
- Users can update validation status.
- Missing important fields are clearly shown.
- Validation notes can be saved.
- Validated records are distinguishable from unvalidated records.
- Page remains readable with many records.

## Design Notes

- Use teal/green accents for validated states.
- Use amber accents for Needs Review.
- Use muted gray for Archived.
- Avoid alarming red unless data is truly invalid.
- Keep validation rows compact and scannable.

## Future Notes

Later sprints may add:

- automated validation suggestions
- duplicate detection
- AI-assisted cleanup notes
- forecasting readiness scoring

Do not make AI decisions final without human review.

---

# 4. Package / Destination References

## Page Purpose

The Package / Destination References page standardizes package, destination, route, itinerary, and category labels.

It supports the thesis by keeping operational records consistent, which improves reports and future forecasting quality.

## Sprint Scope

Sprint 1.

Reference data should be created early because operational records need consistent package and destination links.

## Main User Actions

Administrator:

- Add, edit, activate, deactivate, and archive references.
- Manage categories.
- Review usage in operational records.

Owner / Management:

- View reference list.
- Check active/inactive destinations.
- Review performance by destination later through reports.

Operational Staff:

- Select references while encoding operational records.
- View active references.

## Key UI Sections

- Page header with Add Reference button.
- Summary cards:
  - Active References
  - Inactive References
  - Categories
  - Records Using References
- Reference table.
- Filters:
  - status
  - destination
  - category
  - route
- Add/Edit Reference modal.
- Disable/archive confirmation dialog.
- Empty state for no references.

## Data Fields

Important fields:

- reference_name
- destination
- route
- itinerary_summary
- category
- base_price
- status
- notes
- created_by
- created_at
- updated_at

Optional fields:

- season_tag
- expected_duration
- supplier_notes
- internal_code

## Empty State

Show:

- No package or destination references yet.
- Create references first so operational records can be standardized.
- Primary action: Add Reference.

## Statuses Or Filters

Statuses:

- Active
- Inactive
- Archived

Filters:

- destination
- category
- route
- season
- status

## Acceptance Criteria

- Users can create references.
- Active references are available for operational record forms.
- Inactive references are not accidentally selected for new records unless allowed.
- References can be searched and filtered.
- Reference names are readable and consistent.

## Design Notes

- Treat this as reference data, not a public package catalog.
- Avoid promotional cards or marketing-style package layouts.
- Use dense but readable tables.
- Show usage count if available.

## Future Notes

Later sprints may connect references to:

- destination performance reports
- passenger demand forecasting
- revenue forecasting by destination
- trajectory insights for growth or decline

---

# 5. Expenses

## Page Purpose

The Expenses page tracks operational, marketing, seasonal, overhead, supplier, transportation, and miscellaneous costs.

It supports the thesis by centralizing financial records and allowing management to compare income, costs, and net performance.

## Sprint Scope

Sprint 1.

Expenses should be tracked before advanced analytics because cost history is important for future forecasting and trajectory insights.

## Main User Actions

Administrator:

- Add, edit, archive, and delete or restore expense records depending on policy.
- Manage categories.
- Review all expenses.

Owner / Management:

- Review expense summaries.
- Filter costs by date, category, or payment method.
- Compare operational and marketing costs.

Operational Staff:

- Encode permitted expenses.
- Add notes or receipts reference if allowed.

## Key UI Sections

- Page header with Add Expense button.
- Summary cards:
  - Total Expenses
  - Operations
  - Marketing
  - Overhead
- Filter bar:
  - date range
  - category
  - payment method
  - recorded by
- Expenses table.
- Add/Edit Expense modal.
- Monthly summary panel.
- Empty state.

## Data Fields

Important fields:

- expense_date
- category
- description
- amount
- payment_method
- recorded_by
- notes
- created_at
- updated_at

Suggested categories:

- Operations
- Marketing
- Seasonal
- Overhead
- Supplier
- Transportation
- Miscellaneous

Optional fields:

- supplier_name
- receipt_reference
- related_package_destination_id
- campaign_id

## Empty State

Show:

- No expenses recorded yet.
- Start by encoding operational or marketing costs.
- Primary action: Add Expense.

## Statuses Or Filters

Filters:

- date range
- category
- payment method
- recorded by
- package/destination reference

Optional statuses:

- Recorded
- Needs Review
- Archived

## Acceptance Criteria

- Users can add expense records.
- Amounts are stored as numeric values.
- Categories are visible and consistent.
- Expenses can be filtered by date/category.
- Monthly totals can be summarized.
- Empty state guides the user.

## Design Notes

- Use amber accents carefully for cost categories or warnings.
- Do not make expense cards visually alarming.
- Tables should be solid, compact, and readable.
- Use right-aligned numeric amounts.

## Future Notes

Later sprints may add:

- cost forecasting
- marketing spend impact
- destination profitability analysis
- AI trajectory recommendations based on rising costs

---

# 6. Inventory

## Page Purpose

The Inventory page tracks internal resources, items, stock movements, low stock, and adjustments.

It supports the thesis by improving operational visibility and reducing weak inventory tracking.

## Sprint Scope

Sprint 1.

Inventory records and movements should be implemented before advanced reporting.

## Main User Actions

Administrator:

- Add, edit, archive inventory items.
- Record stock in, stock out, and adjustments.
- Review movement history.

Owner / Management:

- Monitor low stock.
- Review inventory status.
- Check movement summaries.

Operational Staff:

- Update stock movements.
- Record adjustments when permitted.

## Key UI Sections

- Page header with Add Item and Record Movement actions.
- Summary cards:
  - Total Items
  - Low Stock
  - Out of Stock
  - Recent Movements
- Tabs:
  - Items
  - Movements
  - Low Stock
  - Adjustments
- Inventory items table.
- Movement history table.
- Add/Edit Item modal.
- Record Movement modal.
- Low stock empty or alert state.

## Data Fields

Inventory item fields:

- item_name
- category
- current_quantity
- minimum_quantity
- unit
- status
- notes
- created_at
- updated_at

Inventory movement fields:

- inventory_item_id
- movement_type
- quantity
- reason
- related_operational_record_id
- recorded_by
- movement_date
- notes

Movement types:

- Stock In
- Stock Out
- Adjustment

## Empty State

If no items:

- No inventory items yet.
- Add the first item to begin tracking stock.

If no low stock:

- No low stock alerts.
- Inventory levels are currently clear.

## Statuses Or Filters

Statuses:

- Active
- Inactive
- Low Stock
- Out of Stock
- Archived

Filters:

- category
- status
- movement type
- date range
- recorded by

## Acceptance Criteria

- Users can create inventory items.
- Users can record stock movements.
- Current quantity updates after movements.
- Low stock is highlighted based on minimum quantity.
- Movement history is visible.
- Empty and low-stock states are clear.

## Design Notes

- Use green/teal for normal inventory.
- Use amber for low stock.
- Use red only for out of stock or critical warnings.
- Keep movement history readable and compact.

## Future Notes

Later sprints may add:

- inventory trend reports
- supply risk indicators
- AI trajectory warnings for recurring low stock

---

# 7. Reports

## Page Purpose

The Reports page provides descriptive internal summaries for management.

It supports the thesis by turning centralized operational data into decision-support information.

## Sprint Scope

Sprint 1 for descriptive reports only.

Predictive reports and forecasting outputs belong to Sprint 2 or later.

## Main User Actions

Administrator:

- View all reports.
- Export or print internal summaries if implemented.
- Review data quality before reports.

Owner / Management:

- Review sales, expense, inventory, and data quality summaries.
- Filter summaries by date range.

Operational Staff:

- View limited summaries if permitted.

## Key UI Sections

- Page header with report date range.
- Report tabs:
  - Sales Summary
  - Expense Summary
  - Inventory Summary
  - Data Quality Summary
  - Export History
- Summary cards.
- Charts or simple visual summaries.
- Report table.
- Export action.
- Empty state when no validated data exists.

## Data Fields

Sales summary:

- record_date
- package_destination
- passenger_count
- gross_amount
- discount_amount
- net_amount

Expense summary:

- expense_date
- category
- amount
- payment_method

Inventory summary:

- item_name
- current_quantity
- minimum_quantity
- movement_count
- status

Data quality summary:

- draft_count
- needs_review_count
- validated_count
- archived_count
- source_channel

## Empty State

Show:

- No report data available yet.
- Add and validate operational records to generate summaries.
- Link or button: Go to Operational Records.

## Statuses Or Filters

Filters:

- date range
- source channel
- package/destination
- expense category
- inventory category
- validation status

Tabs:

- Sales Summary
- Expense Summary
- Inventory Summary
- Data Quality Summary
- Export History

## Acceptance Criteria

- Reports show descriptive summaries only.
- Report data uses validated or clearly labeled records.
- Users can filter by date range.
- Empty state explains what data is needed.
- Export/print features are optional but planned clearly if not implemented.
- Forecasting is not presented as active in Sprint 1.

## Design Notes

- Reports must be solid and highly readable.
- Avoid decorative chart effects.
- Use minimal chart colors.
- Numeric values should align cleanly.
- Use clear labels and date context.

## Future Notes

Later sprints may add:

- forecast reports
- trajectory insight reports
- model input diagnostics
- AI-generated management summaries

---

# 8. Forecasting

## Page Purpose

The Forecasting page will eventually show Meta Prophet-based time-series forecasts using clean historical operational data.

It supports the thesis by turning centralized operational records into forecasted revenue, demand, cost, or marketing impact insights.

## Sprint Scope

Sprint 2 or later.

In Sprint 1, this page should be locked, disabled, or shown as a placeholder. It should not run forecasting yet.

## Main User Actions

Sprint 1:

- View locked or planned forecasting page.
- See what data is required before forecasting can be activated.

Sprint 2 or later:

Administrator:

- Run or refresh forecasts.
- Review model inputs.
- View forecast history.

Owner / Management:

- Review forecast results.
- Compare historical and forecasted trends.

Operational Staff:

- Usually view-only or no access depending on policy.

## Key UI Sections

Sprint 1 placeholder:

- Locked Forecasting status.
- Data requirements checklist.
- Required validated records count.
- Explanation that forecasting depends on historical data quality.

Future full page:

- Forecast type tabs:
  - Revenue Forecast
  - Passenger Demand
  - Cost Forecast
  - Marketing Impact
  - Model Inputs
- Forecast chart.
- Historical data table.
- Forecast confidence or uncertainty notes.
- Forecast history panel.

## Data Fields

Future forecasting inputs:

- record_date
- passenger_count
- gross_amount
- net_amount
- expense_date
- expense_amount
- package_destination_id
- source_channel
- campaign_spend
- conversions
- validation_status

Forecast metadata:

- forecast_type
- generated_at
- model_version
- input_date_range
- forecast_horizon
- generated_by

## Empty State

Sprint 1:

- Forecasting is not active yet.
- Validate operational records first.
- Forecasts will be available after enough clean historical data exists.

Future:

- No forecast history yet.
- Run the first forecast after selecting model inputs.

## Statuses Or Filters

Future statuses:

- Locked
- Ready
- Generated
- Needs More Data
- Stale

Future filters:

- forecast type
- date range
- package/destination
- model run
- generated by

## Acceptance Criteria

Sprint 1:

- Page is visibly locked or planned.
- It explains required data without pretending forecasting is active.
- No fake predictions are shown.

Sprint 2 or later:

- Forecasts are generated from validated historical data.
- Forecast charts are readable.
- Model inputs are visible.
- Forecast history is stored.

## Design Notes

- Use polished locked states.
- Do not make placeholder content look broken.
- Use calm blue/teal accents.
- Forecast charts must be solid, readable, and not decorative.
- Explain uncertainty clearly.

## Future Notes

Future work includes:

- Python forecasting engine
- Meta Prophet integration
- model input preparation
- forecast history
- AI-driven trajectory insights

---

# 9. Users

## Page Purpose

The Users page manages limited internal user access for ProphetOps.

It supports the thesis by keeping the system internal, controlled, and role-based.

## Sprint Scope

Sprint 1.

Basic user roles and access controls should be implemented early.

## Main User Actions

Administrator:

- Add users.
- Edit user details.
- Assign roles.
- Activate or deactivate users.
- Reset passwords if implemented.

Owner / Management:

- May view user list if permitted.
- Usually cannot manage system users unless policy allows.

Operational Staff:

- No access to user management.

## Key UI Sections

- Page header with Add User button.
- User summary cards:
  - Active Users
  - Inactive Users
  - Admins
  - Staff
- Users table.
- Add/Edit User modal.
- Role selector.
- Status selector.
- Deactivate confirmation dialog.
- Empty state.

## Data Fields

Important fields:

- name
- email
- password
- role
- status
- last_login_at
- created_at
- updated_at

Roles:

- Administrator
- Owner / Management
- Operational Staff

Statuses:

- Active
- Inactive

## Empty State

Show:

- No additional users yet.
- Add users to allow staff and management to access ProphetOps.
- Primary action: Add User.

## Statuses Or Filters

Filters:

- role
- status
- last login
- search by name or email

Statuses:

- Active
- Inactive

## Acceptance Criteria

- Admin can create users.
- Admin can assign roles.
- Inactive users cannot access the system.
- User table is searchable.
- Staff cannot access user management.

## Design Notes

- Keep this page clear and secure-feeling.
- Use role badges.
- Avoid clutter.
- Use confirmation for deactivation.
- Do not expose sensitive password values.

## Future Notes

Later sprints may add:

- activity logs per user
- password reset flows
- more granular permissions
- audit reports

---

# 10. Settings

## Page Purpose

The Settings page manages local system preferences, source channels, categories, and basic configuration.

It supports the thesis by keeping ProphetOps configurable for the local intranet environment and internal operational workflows.

## Sprint Scope

Sprint 1 for basic configuration.

Advanced automation, forecasting settings, and model configuration belong to later sprints.

## Main User Actions

Administrator:

- Configure source channels.
- Manage expense categories.
- Manage inventory categories.
- Manage package/destination categories.
- Configure local system preferences.
- Review basic intranet deployment settings.

Owner / Management:

- View selected settings if permitted.

Operational Staff:

- Usually no access or limited view-only access.

## Key UI Sections

- Page header.
- Settings categories:
  - General
  - Source Channels
  - Expense Categories
  - Inventory Categories
  - Package / Destination Categories
  - Access Preferences
  - Local System
- Settings forms.
- Category tables.
- Add/Edit category modal.
- Save confirmation.
- Empty state for categories.

## Data Fields

General settings:

- system_name
- agency_name
- local_timezone
- default_date_range

Source channel fields:

- channel_name
- status
- description

Category fields:

- category_name
- category_type
- status
- description

Access preferences:

- default_role
- inactive_user_policy

Local system fields:

- deployment_mode
- local_network_notes
- backup_notes

## Empty State

Show:

- No categories configured yet.
- Add categories to keep records consistent.
- Primary action depends on selected settings section, such as Add Source Channel or Add Category.

## Statuses Or Filters

Statuses:

- Active
- Inactive
- Archived

Filters:

- setting section
- category type
- status

## Acceptance Criteria

- Admin can update basic settings.
- Admin can manage source channels and categories.
- Settings are grouped clearly.
- Non-admin users cannot access restricted settings.
- Category changes support consistent records.

## Design Notes

- Keep settings calm and organized.
- Use section tabs or grouped panels.
- Avoid long unstructured forms.
- Use confirmation for important changes.
- Keep system configuration internal-facing.

## Future Notes

Later sprints may add:

- forecasting model settings
- backup/restore workflows
- audit log configuration
- AI trajectory insight preferences
- advanced permission controls

---

# Recommended Implementation Order

Use this order for practical Sprint 1 development:

1. Authentication and protected layout.
2. Dashboard route and reusable layout components.
3. Package / Destination References.
4. Operational Records.
5. Data Validation.
6. Expenses.
7. Inventory.
8. Reports.
9. Users.
10. Settings.
11. Forecasting placeholder only.

## Final Reminder

ProphetOps should feel like a premium internal Decision Support System.

Every page should help answer:

- What operational data exists?
- Is the data clean enough to trust?
- What does management need to review?
- What records are ready for reports?
- What data will support future forecasting?

If a page starts to feel like a customer-facing website, public catalog, or reservation system, it is drifting away from the thesis scope.
