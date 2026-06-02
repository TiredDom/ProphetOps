# General Knowledge: ProphetOps AI Development Guide

## Purpose

This file is a persistent guide for any AI assistant or developer working on ProphetOps.

Read this before making code changes. The purpose is to keep the system consistent even when different teammates use different AI sessions. It explains the project scope, naming rules, modular coding practices, documentation habits, and expected development style.

The goal is to make ProphetOps easier to understand, easier to extend, and less likely to drift away from the thesis scope.

## Project Summary

ProphetOps is a localized Decision Support System for Renan-Tina Travels and Tours.

The system is intended for internal operations, not public customers. It centralizes messy operational data from sources such as Facebook Messenger, Google Sheets, Gmail, paper records, and notebooks. The system then supports reporting, inventory tracking, future forecasting, and AI-driven trajectory insights.

## Scope Reminder

ProphetOps should focus on:

- internal operational data intake
- record centralization
- data validation
- sales and financial summaries
- expense tracking
- inventory tracking
- internal reports
- future Meta Prophet forecasting
- future AI-driven trajectory insights
- limited user access
- local Wi-Fi or intranet deployment

ProphetOps should not become:

- a customer booking website
- an online reservation engine
- a payment gateway system
- a customer portal
- an external booking API integration
- a public travel package marketplace

Booking-related information should be treated only as internal historical operational data. It is useful because passenger counts, transaction dates, sales values, destinations, and costs can become inputs for reports and future forecasting.

## Technology Direction

The expected stack is:

- Laravel for the backend
- Vue for the frontend
- Python for the later forecasting engine
- Meta Prophet for the later time-series forecasting module
- A relational database for structured operational records

The first development focus should be the local admin dashboard and structured data foundation. Forecasting should be implemented only after the operational records and data validation flow are stable.

## Design Direction

Use the Premium Clarity Dashboard style.

This means:

- modern and professional
- clean and readable
- Apple HIG-inspired, but not a direct copy
- subtle frosted glass only for navigation, login, or modal surfaces
- solid high-contrast panels for tables, forms, charts, and reports
- compact but readable dashboard layout
- clear visual hierarchy
- consistent spacing, typography, and button behavior

Do not overuse glassmorphism. ProphetOps is a decision support dashboard, so readability is more important than decorative effects.

## Preferred Language And Naming

Use names that match the thesis scope.

Preferred terms:

- Operational Records
- Operational Data Intake
- Data Validation
- Source Channels
- Package / Destination References
- Expenses
- Inventory
- Inventory Movements
- Reports
- Forecasting
- Trajectory Insights
- Admin Dashboard
- Local Intranet

Avoid terms that imply the wrong scope:

- Booking Website
- Booking Engine
- Online Reservation
- Customer Portal
- Payment Gateway
- Public Marketplace

Acceptable but less precise:

- Transactions
- Bookings
- Package Catalog

When in doubt, use `Operational Records` instead of `Bookings`.

## Core Development Rule

Build ProphetOps by module.

Each module should have a clear purpose, clear files, and clear responsibilities. Avoid mixing unrelated features in one large file.

Recommended modules:

- Authentication
- Dashboard
- Operational Records
- Data Validation
- Source Channels
- Package / Destination References
- Expenses
- Inventory
- Reports
- Forecasting
- Trajectory Insights
- Users and Access Control
- Settings

## Modular Coding Practice

### Keep Files Focused

Each file should have one main responsibility.

Good patterns:

- A controller handles request flow.
- A model represents database data.
- A request class handles validation.
- A service or action handles business logic.
- A Vue page handles page-level layout.
- A Vue component handles a reusable UI pattern.

Avoid:

- very large controllers
- repeated form logic across pages
- business rules hidden inside UI components
- unrelated modules inside one file
- hardcoded values repeated across the app

### Separate Backend Logic From Frontend Display

The backend should be the source of truth for validation, permissions, and important business rules.

The frontend should focus on:

- page layout
- display states
- form interaction
- user feedback
- filters and sorting UI
- visual summaries

The backend should handle:

- validation rules
- authorization
- database writes
- inventory quantity changes
- data status transitions
- report calculations where consistency matters
- forecasting preparation later

### Reuse Components

Create reusable Vue components for repeated UI patterns.

Recommended reusable components:

- App layout
- Sidebar
- Top bar
- Page header
- Stat card
- Data table
- Filter bar
- Search input
- Form input
- Select field
- Date field
- Modal
- Confirmation dialog
- Status badge
- Empty state
- Loading state
- Error state

Reusable components should be simple, predictable, and easy to use across modules.

### Use Consistent Data Statuses

Operational records should support a clear data quality flow.

Recommended statuses:

- Draft
- Needs Review
- Validated
- Archived

Meaning:

- Draft: newly encoded or incomplete
- Needs Review: requires checking before use in reports or forecasting
- Validated: clean enough for reports and future forecasting
- Archived: no longer active but kept for history

### Keep Features Reviewable

Build features in small, complete slices.

Good feature scope:

- Build login page
- Build operational record form
- Build operational records table
- Build inventory movements page
- Build basic reports page

Too small:

- Add one button
- Add one label
- Add one icon

Too large:

- Build the entire admin system
- Finish all reports and analytics
- Implement forecasting and dashboard together

A good feature should be understandable, testable, and reviewable.

## Suggested Laravel Organization

Follow the actual project conventions once the codebase exists. If no convention exists yet, use this structure as a guide.

Backend responsibilities may be organized through:

- routes
- controllers
- models
- request validation classes
- services or actions
- policies or permission checks
- database migrations
- seeders
- resources or serializers if needed

Example pattern for Operational Records:

```text
Model:
OperationalRecord

Controller:
OperationalRecordController

Requests:
StoreOperationalRecordRequest
UpdateOperationalRecordRequest

Service:
OperationalRecordService

Migration:
create_operational_records_table
```

## Suggested Vue Organization

Follow the actual frontend structure once the codebase exists. If no convention exists yet, organize pages and components by responsibility.

Recommended page groups:

```text
pages/
  Dashboard/
  OperationalRecords/
  DataValidation/
  PackageDestinations/
  Expenses/
  Inventory/
  Reports/
  Users/
  Settings/
```

Recommended component groups:

```text
components/
  layout/
  navigation/
  dashboard/
  tables/
  forms/
  feedback/
  modals/
  badges/
```

## Database Planning Principles

Database tables should support both current operations and future forecasting.

Important rules:

- Store dates consistently.
- Store amounts as numeric values.
- Store passenger counts as numeric values.
- Store source channels clearly.
- Store validation status clearly.
- Keep package and destination names consistent through references.
- Avoid storing important data only inside notes fields.
- Avoid ambiguous field names.

Important future forecasting fields:

- record date
- passenger count
- net amount
- gross amount
- operational costs
- marketing spend
- destination or package reference
- source channel
- validation status

## Recommended Information Folder

Create an `information` folder to act as high-level project memory.

Recommended files:

```text
information/
  module-map.md
  feature-log.md
  fix-log.md
  decisions.md
  database-map.md
  api-map.md
  ui-components.md
```

These files should be summaries, not full documentation dumps. Keep them short, accurate, and useful.

## module-map.md

Purpose:

Explain each module, what it does, and where its files are located.

Suggested format:

```markdown
## Operational Records

Purpose:
Centralizes messy internal records from Messenger, Google Sheets, Gmail, paper records, notebooks, and manual encoding.

Main Paths:
- Backend:
- Frontend:
- Database:
- Routes:

Current Status:

Important Notes:
```

## feature-log.md

Purpose:

Summarize completed features so future developers and AI assistants can quickly understand what already exists.

Suggested format:

```markdown
## YYYY-MM-DD - Feature Name

Module:

Summary:

Files Added:

Files Updated:

User-Facing Behavior:

How To Verify:

Notes For Future Work:
```

## fix-log.md

Purpose:

Summarize bug fixes and important changes after each fix.

This helps future AI sessions and teammates understand what changed without scanning the entire project.

Suggested format:

```markdown
## YYYY-MM-DD - Short Fix Title

Type:
Bug Fix / UI Fix / Backend Fix / Data Fix / Configuration Fix

Module:

Problem:

Root Cause:

Fix Summary:

Files Changed:
- path/to/file

Behavior After Fix:

Verification:

Known Risks:

Next AI / Developer Should Know:
```

## decisions.md

Purpose:

Record important technical, design, and scope decisions.

Suggested format:

```markdown
## YYYY-MM-DD - Decision Title

Decision:

Reason:

Alternatives Considered:

Impact:
```

Examples:

- Use Operational Records instead of Booking Management.
- Keep Forecasting for the later forecasting phase.
- Use Premium Clarity Dashboard design.
- Use solid panels for data-heavy tables and forms.
- Keep ProphetOps internal and not customer-facing.

## database-map.md

Purpose:

Summarize database tables and what they are for.

Suggested format:

```markdown
## operational_records

Purpose:

Important Fields:

Relationships:

Used By:

Forecasting Relevance:
```

## api-map.md

Purpose:

Summarize important routes or API endpoints.

Suggested format:

```markdown
## Operational Records

Route / Endpoint:

Purpose:

Request Data:

Response Data:

Permissions:
```

## ui-components.md

Purpose:

Track reusable UI components and when to use them.

Suggested format:

```markdown
## DataTable

Purpose:

Used In:

Props / Inputs:

States:

Notes:
```

## After Each Feature Or Fix

After every meaningful feature or fix, update the `information` folder.

Minimum update:

- Add a short entry to `feature-log.md` or `fix-log.md`.
- Mention the module.
- Mention the changed files.
- Mention what behavior changed.
- Mention how it was verified.
- Mention any important context for the next AI or developer.

Do not paste large code blocks into the information files. Summaries should be short and high signal.

## AI Handoff Summary Standard

Every meaningful completed change should leave a short AI handoff summary.

Use this format:

```markdown
AI Handoff:
- Module:
- What changed:
- Main files:
- How to verify:
- Important context:
- Next likely task:
```

Example:

```markdown
AI Handoff:
- Module: Operational Records
- What changed: Added operational record form with source channel and validation status fields.
- Main files: resources/js/pages/OperationalRecords/Create.vue, app/Http/Requests/StoreOperationalRecordRequest.php
- How to verify: Create a record with Messenger as source channel and confirm it appears in the records table.
- Important context: This is internal data intake, not a booking form.
- Next likely task: Add filters for source channel and validation status.
```

## AI Reading Order

When starting work, read in this order:

1. `markdowns/general knowledge.md`
2. `information/module-map.md`
3. Relevant entry in `information/feature-log.md`
4. Relevant entry in `information/fix-log.md`
5. Relevant module files

Only scan the whole project if the issue is global, unclear, or not documented.

## AI Development Behavior

When modifying the project:

- Follow existing code patterns first.
- Keep changes scoped to the relevant module.
- Prefer reusable components for repeated UI.
- Do not rename concepts casually.
- Do not introduce customer-facing booking behavior.
- Do not add forecasting logic before the data foundation is ready.
- Keep UI readable and consistent.
- Update project memory after meaningful changes.

## Review Checklist Before Considering Work Complete

Before considering a feature or fix complete, confirm:

- The change matches the ProphetOps thesis scope.
- The change keeps the system internal-facing.
- The change uses correct naming.
- The UI follows Premium Clarity design.
- Data-heavy areas remain readable.
- Required fields are validated.
- Error, empty, and loading states are handled where relevant.
- The change was manually verified.
- The relevant `information` file was updated.

## Final Principle

ProphetOps should be easy to understand from its modules and summaries.

Good project memory means future developers and AI assistants should be able to answer:

- What does this module do?
- Where are its files?
- What changed recently?
- Why was this design chosen?
- How do I verify it?

If those questions are easy to answer, the project is organized well.
