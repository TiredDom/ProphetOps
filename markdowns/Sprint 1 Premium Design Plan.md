# ProphetOps Sprint 1 Premium Design Plan

## Design Direction

ProphetOps will use a Premium Clarity Dashboard design. This means the interface should feel modern, polished, and professional while still being practical for daily administrative work.

The system should be inspired by Apple Human Interface Guidelines, especially clarity, visual hierarchy, consistency, and ease of use. The design may use subtle Cupertino-style frosted glass effects, but only in areas where readability will not be affected.

## Design Statement

ProphetOps will use a modern Apple HIG-inspired dashboard interface focused on readability, hierarchy, and operational efficiency. The system will apply subtle frosted glass effects to navigation, login, and modal surfaces, while preserving solid high-contrast layouts for tables, forms, reports, and charts. This approach supports usability, performance efficiency, and functional suitability under ISO 25010 while maintaining a premium and professional visual identity.

## Main Design Goals

- Make the system easy to understand for administrators, owners, and operational staff.
- Make daily data entry fast and clear.
- Make business summaries readable at a glance.
- Prepare the interface for forecasting and trajectory insights in later sprints.
- Keep the design premium without making it decorative or distracting.
- Support local intranet use with good performance.

## Visual Style

The visual style should be clean, calm, and business-focused.

Recommended look:

- Light dashboard background
- Solid white panels for important data
- Subtle frosted glass for navigation and modals
- Soft shadows
- Thin borders
- Compact but readable tables
- Clear action buttons
- Minimal chart colors
- Professional spacing

## Color Palette

Recommended colors:

- Background: #F5F7FA
- Main Surface: #FFFFFF
- Glass Surface: rgba(255, 255, 255, 0.72)
- Primary Accent: #2563EB
- Secondary Accent: #0F766E
- Success: #16A34A
- Warning: #D97706
- Danger: #DC2626
- Primary Text: #111827
- Secondary Text: #6B7280
- Border: #E5E7EB

The color palette should avoid looking too playful. ProphetOps should feel like a serious business tool.

## Typography

Use a clean sans-serif font.

Recommended font stack:

```text
Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif
```

Recommended type sizes:

- Page title: 24px to 28px
- Section heading: 18px to 20px
- Card value: 24px to 32px
- Table text: 14px
- Form labels: 13px to 14px
- Helper text: 12px to 13px

Typography should be clear and readable. Avoid oversized headings inside dashboard panels.

## Glassmorphism Usage

Use frosted glass effects only in selected areas.

Allowed areas:

- Login panel
- Sidebar background
- Top navigation bar
- Modal containers
- Floating filter bars
- Small dashboard header areas

Avoid frosted glass in:

- Operational record tables
- Inventory tables
- Expense records
- Financial reports
- Forecast charts
- Form input areas
- Printable reports

Important data should always be displayed on solid, high-contrast surfaces.

## Layout Structure

Use a classic admin dashboard layout.

Main layout:

- Left sidebar navigation
- Top bar with page title, date, and user menu
- Main content area
- Responsive layout for smaller screens

Recommended sidebar items:

- Dashboard
- Operational Records
- Packages
- Expenses
- Inventory
- Reports
- Forecasting
- Users
- Settings

For Sprint 1, Forecasting can be shown as a placeholder because the actual forecasting engine belongs to Sprint 2.

## Navigation Blueprint

ProphetOps should use a grouped left sidebar, a clean sticky top bar, and in-page tabs only for closely related module views. The navigation must communicate that ProphetOps is a Decision Support System for operational data, analytics, forecasting, and internal management. It should not feel like a booking website or customer-facing reservation platform.

### Full Sidebar Structure

#### Overview

- Dashboard

#### Data Workspace

- Operational Records
- Data Validation
- Source Channels

#### Business Records

- Package / Destination References
- Expenses
- Marketing Records

#### Operations

- Inventory
- Inventory Movements
- Low Stock Alerts

#### Reports

- Reports

#### Analytics

- Forecasting
- Trajectory Insights
- Forecast History

#### Administration

- Users
- Roles / Permissions
- System Settings
- Activity Log

### Sprint 1 Minimum Sidebar

#### Overview

- Dashboard

#### Data Workspace

- Operational Records
- Data Validation

#### Business Records

- Package / Destination References
- Expenses

#### Operations

- Inventory

#### Reports

- Reports

#### Analytics

- Forecasting placeholder only

#### Administration

- Users
- Settings

### Suggested Route Paths

| Module | Route |
| --- | --- |
| Dashboard | /dashboard |
| Operational Records | /data/operational-records |
| Data Validation | /data/validation |
| Source Channels | /data/source-channels |
| Package / Destination References | /business/package-destinations |
| Expenses | /business/expenses |
| Marketing Records | /business/marketing-records |
| Inventory | /operations/inventory |
| Inventory Movements | /operations/inventory-movements |
| Low Stock Alerts | /operations/low-stock |
| Reports | /reports |
| Forecasting | /analytics/forecasting |
| Trajectory Insights | /analytics/trajectory-insights |
| Forecast History | /analytics/forecast-history |
| Users | /admin/users |
| Roles / Permissions | /admin/roles |
| System Settings | /settings |
| Activity Log | /admin/activity-log |

### Top Bar Blueprint

The top bar should remain clean and operational. It should support quick filtering and awareness without competing with the main workspace.

Recommended top bar elements:

- Page title
- Short page description
- Global date range picker
- Search
- Quick Add button
- Alerts indicator
- User profile menu

### In-Page Tabs

Use in-page tabs only when a module has closely related subviews. Tabs should use segmented controls or clean underline styling.

Recommended tab groups:

- Dashboard: Overview, Sales, Expenses, Inventory, Data Quality
- Operational Records: All Records, Needs Review, Validated, Source Channels, Archived
- Package / Destination References: Active, Inactive, Categories
- Expenses: All Expenses, Operations, Marketing, Seasonal, Overhead, Monthly Summary
- Inventory: Items, Movements, Low Stock, Adjustments
- Reports: Sales Summary, Expense Summary, Inventory Summary, Data Quality Summary, Export History
- Forecasting later: Revenue Forecast, Passenger Demand, Cost Forecast, Marketing Impact, Model Inputs
- Trajectory Insights later: Trend Direction, Growth/Decline Signals, Risk Indicators, Recommended Focus Areas

### Role-Based Navigation Visibility

- Administrator sees all modules.
- Owner / Management sees dashboard, operational records, validation, references, expenses, marketing records, inventory, reports, forecasting, trajectory insights, and optional activity log.
- Operational Staff sees dashboard, operational records, validation, inventory, and optional source channels, references, expenses, and reports depending on permission.

### Navigation Design Standards

- Use grouped sidebar section labels.
- Use icons beside main navigation items.
- Use a strong active state for the current page.
- Keep the desktop sidebar between 240px and 280px wide.
- Allow the sidebar to collapse with tooltips.
- Keep the top bar sticky.
- Use segmented controls or clean underline tabs for in-page tabs.
- Show disabled or locked states for Sprint 2 modules.

### Navigation Naming Rules

Preferred labels:

- Operational Records
- Data Validation
- Source Channels
- Package / Destination References
- Expenses
- Inventory
- Reports
- Forecasting
- Trajectory Insights

Avoid labels:

- Booking Website
- Online Reservations
- Customer Portal
- Payment Gateway
- Booking Engine

## Sprint 1 Screens

### 1. Login Screen

Purpose: Allow authorized users to access the system.

Design notes:

- Centered login panel
- Subtle frosted glass effect
- ProphetOps name clearly visible
- Simple username/email and password fields
- Primary login button
- Error message area

### 2. Dashboard

Purpose: Give management a quick overview of business activity.

Suggested dashboard cards:

- Total Sales
- Total Operational Records
- Total Expenses
- Net Revenue
- Available Packages
- Low Inventory Alerts

Suggested dashboard sections:

- Recent Operational Records
- Monthly Sales Snapshot
- Expense Overview
- Inventory Status

### 3. Operational Data Intake Page

Purpose: Encode historical and daily internal operational records from Messenger, Google Sheets, Gmail, paper records, notebooks, and manual encoding into one structured dataset.

Suggested fields:

- Record date
- Client or source name if available
- Package or destination reference
- Passenger count
- Gross amount
- Discount amount
- Net amount
- Payment status
- Payment method
- Source channel
- Notes
- Encoded by

Main actions:

- Add operational record
- View operational record
- Edit operational record
- Delete or archive operational record
- Search and filter

### 4. Packages Page

Purpose: Manage travel packages or destinations.

Suggested fields:

- Package name
- Destination
- Price
- Available slots or stock
- Status
- Description

Main actions:

- Add package
- Edit package
- Disable package
- View related operational records

### 5. Expenses Page

Purpose: Track operational, marketing, seasonal, and overhead costs.

Suggested fields:

- Expense date
- Category
- Description
- Amount
- Payment method
- Notes

Main actions:

- Add expense
- Edit expense
- Filter by category
- Filter by date

### 6. Inventory Page

Purpose: Track stock, slots, or business resources.

Suggested fields:

- Item name
- Category
- Current quantity
- Minimum quantity
- Status

Main actions:

- Add inventory item
- Add stock movement
- Deduct stock
- View movement history
- Show low stock warning

### 7. Reports Page

Purpose: Present basic internal summaries for management.

Sprint 1 reports:

- Sales summary
- Expense summary
- Inventory movement summary
- Operational record count summary

Forecasting reports should be reserved for Sprint 2.

## Component Plan

Reusable Vue components should be planned for:

- App layout
- Sidebar
- Top navigation
- Page header
- Stat card
- Data table
- Search bar
- Filter dropdown
- Form input
- Select field
- Date picker
- Modal
- Confirmation dialog
- Status badge
- Empty state
- Loading state

This will make later sprints faster because the same components can be reused for forecasting and analytics screens.

## Button Style

Button hierarchy:

- Primary button: main action such as Add Operational Record
- Secondary button: cancel or supporting action
- Danger button: delete or remove
- Icon button: view, edit, delete, filter, export

Buttons should be simple, readable, and consistent.

## Table Style

Tables are important in ProphetOps, so they should be designed carefully.

Recommended table behavior:

- Clear column labels
- Search function
- Filter function
- Status badges
- Row actions
- Empty state message
- Loading state
- Pagination if records become large

Tables should use solid white backgrounds, not glass backgrounds.

## Chart Style

Sprint 1 may include basic charts only.

Recommended charts:

- Monthly sales line chart
- Expenses by category bar chart
- Inventory status indicator

Chart design rules:

- Minimal grid lines
- Clear labels
- Limited colors
- No decorative chart effects
- Tooltips for values

## Accessibility And Usability

The design should support ISO 25010 usability goals.

Important rules:

- Text must have strong contrast.
- Forms must have clear labels.
- Error messages must be visible and understandable.
- Buttons must clearly show their purpose.
- Tables must be easy to scan.
- Important actions must ask for confirmation.
- The system should remain usable even if blur effects are disabled.

## Performance Considerations

Because the system is intended for local intranet use, the UI should remain lightweight.

Performance rules:

- Avoid excessive blur effects.
- Avoid heavy animations.
- Keep tables efficient.
- Load only needed data per page.
- Use simple charts in Sprint 1.
- Keep the first dashboard load fast.

## Sprint 1 Scope

Included in Sprint 1:

- UI style guide
- Login layout
- Admin dashboard layout
- Sidebar and top navigation
- Operational data intake page design
- Packages page design
- Expenses page design
- Inventory page design
- Basic reports page design
- Database-ready data structure planning
- Reusable component planning

Not included in Sprint 1:

- Meta Prophet forecasting engine
- AI-driven trajectory insights
- Advanced predictive analytics
- External booking website
- Customer-facing portal
- External API integrations

## Sprint 1 Success Criteria

Sprint 1 is successful if:

- Users can understand the layout without training.
- The main admin pages are visually consistent.
- The dashboard shows basic operational summaries.
- Data-heavy screens are readable and organized.
- The design looks modern and premium.
- The interface is ready for Sprint 2 forecasting features.
- The system supports the thesis requirement for a localized admin dashboard.

## Recommended Final Direction

Use a premium operational dashboard style, not a decorative glassmorphism style. The interface should feel Apple-inspired, clean, and polished, but the core design priority should be clarity for business data.

Best summary:

```text
Premium clarity first. Glassmorphism second. Data readability always first.
```

## Expanded Sprint 1 Implementation Description

Sprint 1 is the foundation sprint for the localized ProphetOps admin dashboard. Its purpose is to prove that Renan-Tina Travels and Tours can move away from scattered records in Facebook Messenger, Google Sheets, Gmail, and physical notebooks into one structured internal system.

This sprint should focus on the operational data foundation: authentication, dashboard layout, operational data intake, package and destination reference data, expenses, inventory, and basic non-predictive reports. Meta Prophet forecasting and AI-driven trajectory insights should remain reserved for Sprint 2, once clean historical data is available.

## Main Sprint 1 Objective

Create a working local admin dashboard where authorized users can manage operational records, package and destination reference data, expenses, and inventory in one centralized system.

The Laravel and Vue application should also establish a database structure that is ready for future forecasting, especially by storing dates, monetary values, passenger counts, categories, campaign data, and inventory movements consistently.

## User Roles

### Administrator

The Administrator has full access to the system. This role can manage users, operational records, reports, packages, expenses, inventory, settings, and system configuration.

### Owner / Management

The Owner or Management role is focused on monitoring business performance. This role can review dashboards, check reports, inspect operational records and expenses, and monitor inventory status.

### Operational Staff

The Operational Staff role is focused on daily encoding and operations. This role can add operational records, update payment statuses, record inventory movements, and optionally record permitted expenses depending on agency policy.

## Suggested Permissions

| Area | Administrator | Owner / Management | Operational Staff |
| --- | --- | --- | --- |
| Dashboard | Full access | View access | View access |
| Reports | Full access | View access | Limited view access |
| Operational Records | Add, view, edit, delete/archive | View and review, limited editing depending on policy | Add and edit daily records |
| Packages | Add, view, edit, disable | View and review | View/select as reference data |
| Expenses | Add, view, edit, delete/archive | View and review, limited editing depending on policy | Optional add/edit permission |
| Inventory | Add, view, edit, record movements | View and review | Update stock movements |
| Users | Full access | No access or limited access | No access |
| Settings | Full access | No access or limited access | No access |

## Detailed Database Planning

### users

- id
- name
- email
- password
- role
- status
- last_login_at
- created_at
- updated_at

### travel_packages

- id
- package_name
- destination
- description
- base_price
- available_slots
- status
- created_at
- updated_at

### operational_records

- id
- record_date
- client_source_name
- package_id
- passenger_count
- gross_amount
- discount_amount
- net_amount
- payment_status
- payment_method
- source_channel
- notes
- encoded_by
- created_at
- updated_at

### expenses

- id
- expense_date
- category
- description
- amount
- payment_method
- recorded_by
- notes
- created_at
- updated_at

### inventory_items

- id
- item_name
- category
- current_quantity
- minimum_quantity
- unit
- status
- created_at
- updated_at

### inventory_movements

- id
- inventory_item_id
- movement_type
- quantity
- reason
- related_operational_record_id
- recorded_by
- movement_date
- created_at
- updated_at

### marketing_campaigns Optional

- id
- campaign_name
- platform
- start_date
- end_date
- budget
- actual_spend
- conversions
- notes
- created_at
- updated_at

### Forecasting Readiness Note

The fields record_date, passenger_count, net_amount, expense_date, amount, source_channel, campaign spend, and conversions should be stored cleanly and consistently. These can become inputs for Sprint 2 Meta Prophet forecasting and AI-driven trajectory analytics.

## Page Descriptions And Acceptance Criteria

### Login

Purpose: Allow authorized users to access the internal system.

Acceptance criteria:

- A valid user can log in and is redirected to the dashboard.
- Invalid credentials show a clear error message.
- Inactive users are blocked from accessing the system.
- The login screen follows the Premium Clarity design direction with subtle glass treatment.

### Dashboard

Purpose: Show a quick operational summary for management and staff.

Acceptance criteria:

- The dashboard shows total sales, total operational records, total expenses, net revenue, active packages, and low inventory alerts.
- Recent operational records are visible.
- Basic sales and expense snapshots are displayed.
- The dashboard uses readable cards, solid surfaces, and minimal decorative effects.

### Operational Data Intake

Purpose: Centralize historical and daily internal operational records from fragmented sources into a clean, forecasting-ready structure.

Acceptance criteria:

- Users can add, view, search, filter, and edit operational records.
- Required fields are validated.
- Operational records can be connected to package or destination reference data.
- Payment status and payment method can be tracked.
- Source channel can identify whether the record came from Messenger, Google Sheets, Gmail, paper records, notebooks, or manual encoding.
- Operational record data is stored in a format suitable for future forecasting.

### Packages

Purpose: Manage travel packages and destinations.

Acceptance criteria:

- Users with permission can create and edit packages.
- Packages can be marked active or inactive.
- Active packages can be selected as reference data in operational record forms.
- Package data includes destination, price, availability, and status.

### Expenses

Purpose: Track operational, marketing, seasonal, and overhead costs.

Acceptance criteria:

- Users with permission can record categorized expenses.
- Expenses can be filtered by date and category.
- Monthly expense totals can be summarized.
- Expense records are stored with clean dates, categories, and amounts for future analytics.

### Inventory

Purpose: Track stock, slots, and operational resources.

Acceptance criteria:

- Users can add inventory items.
- Users can record stock in, stock out, and adjustment movements.
- Current quantity updates after each movement.
- Low stock items are highlighted based on minimum quantity.
- Movement history is available for review.

### Reports

Purpose: Present basic internal summaries for Sprint 1.

Acceptance criteria:

- Reports show non-predictive summaries only.
- Sales, expenses, inventory movement, and operational record count summaries are available.
- Forecasting reports are clearly reserved for Sprint 2.

## Sprint 1 Backlog Epics

### Project Foundation

- Set up Laravel project structure.
- Set up Vue frontend structure.
- Configure local database.
- Prepare local intranet deployment assumptions.
- Create initial migrations and seed data.

### Authentication And Access Control

- Create login flow.
- Add roles and user status.
- Protect admin pages.
- Apply role-based permissions.

### Dashboard UI

- Create main layout.
- Add sidebar and top navigation.
- Add summary cards.
- Add recent operational records section.
- Add basic sales and expense snapshots.

### Operational Data Intake

- Create operational records database table.
- Build operational record form.
- Build operational record list table.
- Add search and filter behavior.
- Connect operational records to package and destination reference data.

### Package Management

- Create package database table.
- Build package form.
- Build package list table.
- Add active/inactive package status.

### Expense Management

- Create expense database table.
- Build expense form.
- Build expense list table.
- Add date and category filtering.
- Add monthly totals.

### Inventory Management

- Create inventory item and inventory movement tables.
- Build inventory item form.
- Build stock movement form.
- Update current quantity after movements.
- Add low stock highlighting.

### Basic Reports

- Create sales summary.
- Create expense summary.
- Create inventory movement summary.
- Create operational record count summary.
- Keep all reports non-predictive for Sprint 1.

## Suggested Timeline

| Day | Focus | Main Output |
| --- | --- | --- |
| Day 1 | Planning, roles, database fields, setup | Confirmed scope, schema plan, local project setup |
| Day 2 | Authentication and layout | Login, role access, dashboard shell, navigation |
| Day 3 | Operational records and packages | Operational record CRUD, package CRUD, package selection |
| Day 4 | Expenses and inventory | Expense CRUD, inventory items, stock movements |
| Day 5 | Dashboard, reports, polish, demo | Summary cards, basic reports, UI cleanup, sprint demo |

## Team Distribution

### Backend

- Laravel setup
- Migrations
- Models
- Authentication
- API routes or controllers
- Validation
- Role and permission checks

### Frontend

- Vue layout
- Dashboard cards
- Forms
- Tables
- Modals
- Responsive behavior
- Page interactions

### UI / UX

- Design guide
- Page layouts
- Component styling
- Form clarity
- Table readability
- Usability checks

### QA / Documentation

- Test cases
- Acceptance criteria checklist
- Screenshots
- Sprint review notes
- Thesis alignment documentation

## Demo Script

1. User opens the local ProphetOps system.
2. User logs in with valid credentials.
3. User views the dashboard summary.
4. User adds a travel package.
5. User records an operational data entry connected to the package or destination reference.
6. User records an expense.
7. User updates inventory through a stock movement.
8. User views basic reports.
9. User logs out.

## Sprint 1 Risks

### Too Much Focus On Visual Effects

Risk: The team may spend too much time on glassmorphism or decorative styling.

Mitigation: Limit glass effects to navigation, login, modals, and small floating surfaces. Keep tables, reports, forms, and charts solid and readable.

### Database Not Forecasting-Ready

Risk: Records may be stored without consistent dates, amounts, categories, or passenger counts.

Mitigation: Standardize key fields early, especially record_date, passenger_count, net_amount, expense_date, amount, source_channel, campaign spend, and conversions.

### Sprint Scope Becomes Too Large

Risk: The team may try to include forecasting, AI, external APIs, or a customer-facing portal too early.

Mitigation: Keep Prophet, AI trajectory insights, external integrations, customer-facing portals, and booking-system functionality out of Sprint 1.

### Staff Usability Problems

Risk: Staff may find forms confusing if labels, validation, and workflows are unclear.

Mitigation: Use clear labels, simple forms, helpful validation, compact tables, and predictable navigation.

## Definition Of Done

Sprint 1 is done when:

- The application runs locally.
- Login works for authorized users.
- Inactive users are blocked.
- The dashboard layout is complete.
- Operational records can be created and viewed.
- Packages can be created and viewed.
- Expenses can be created and viewed.
- Inventory items and movements can be created and viewed.
- Basic non-predictive reports are visible.
- The UI follows the Premium Clarity design direction.
- Data is structured consistently for future forecasting.
- The system is ready for Sprint 2 Meta Prophet and AI trajectory integration.
