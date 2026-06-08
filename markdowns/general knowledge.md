# General Knowledge: ProphetOps AI Development Guide

## Purpose

This guide tells future assistants and developers how to think about ProphetOps before implementing features.

ProphetOps is a localized internal Decision Support System for Renan-Tina Travels and Tours, a B2B travel agency. It should centralize bookings, package availability, operational costs, sales analytics, reports, forecast previews, and simulated DSS trajectory insights.

## Product Summary

The agency currently relies on fragmented tools such as Messenger, Google Sheets, Gmail, notebooks, and manual records.

ProphetOps should help internal users understand:

- what is happening in the business
- why it matters
- what may happen next
- what action should be reviewed

The target users are owners, admins, and travel operations staff.

## Scope Reminder

This is not a public booking site.

Do not turn ProphetOps into:

- a customer booking website
- a public travel marketplace
- a payment system
- a customer portal
- a marketing landing page
- an external API integration platform

Sprint 1 is front-end focused and should use mock/sample data unless backend endpoints already exist and the user asks to connect them.

## Technology Direction

Use the project's existing stack and conventions:

- Laravel
- Inertia.js
- Vue 3
- Vite
- Custom CSS

Vue pages live in:

```text
resources/js/Pages/
```

Vue components live in:

```text
resources/js/Components/
```

Shared mock data should be centralized under a clear frontend data path when implemented.

## Design Direction

Use a minimalist, modern, premium business interface inspired by Stripe Dashboard and Attio CRM.

Stripe inspiration:

- financial clarity
- revenue and expense presentation
- trustworthy dashboard layout
- clean charts and reports
- serious business tone

Attio inspiration:

- modern record management
- searchable tables
- flexible CRM-like data views
- clean object/detail layouts
- polished but simple interactions

Visual rules:

- light-first interface
- soft gray background
- white panels
- thin borders
- subtle shadows only when useful
- no loud gradients
- no decorative clutter
- clear icons where helpful

## Sprint 1 Pages

Build toward these pages:

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

Deprecated active planning:

- Standalone Operational Records page
- Standalone Data Validation page
- Settings page
- Package references as a separate Sprint 1 requirement

These concepts may still inform Bookings, Inventory, Analytics, Reports, and future backend work.

Legacy cleanup:

- Follow `legacy-removal-plan.md` before deleting old page files, old CSS blocks, or old route redirects.
- Keep historical feature/fix logs intact.

## Role Model

Owner / Management:

- can access all pages

Admin:

- can access Dashboard, Bookings, Inventory, Expenses, Analytics, and Reports

Staff:

- can access Bookings and Inventory only

Restricted pages should not break the UI.

## Pseudo Login

Sprint 1 uses demo accounts only:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

Do not build real auth in Sprint 1.

No password hashing, backend sessions, JWT, database-backed users, or production security should be added unless the user changes scope.

## DSS Insight Standard

Every insight must connect data to decision-making.

Use:

Observed data -> Business meaning -> Suggested action

Good example:

- Observed data: Boracay Package has only 4 slots left.
- Business meaning: This may affect expected demand next week.
- Suggested action: Review package capacity.

Avoid:

- generic motivational text
- unexplained metrics
- vague AI claims
- technical labels that business owners cannot understand

## Forecasting And AI Labels

Forecasting and AI pages are placeholders in Sprint 1.

Required labels:

- Sample Forecast Preview
- Forecast engine integration pending
- Simulated DSS Insight
- AI trajectory module placeholder

Never imply that Meta Prophet or an AI model is already running.

## Page Structure Rule

Each page should have:

1. Clear page title
2. Short subtitle explaining purpose
3. One primary action button
4. Main content
5. Secondary details below or behind filters, tabs, drawers, or modals

Use progressive disclosure:

- summaries first
- details on demand
- short dashboard tables
- drawers for record details
- modals only for focused confirmations

## Interaction Rules

Use a modal for:

- destructive confirmation
- logout confirmation if needed
- small quick actions with 1 to 3 fields
- simple status changes
- short export options

Use a drawer for:

- booking details
- booking add/edit forms
- inventory details
- stock adjustment
- expense details
- record activity/history

Use a full page for:

- dashboard
- analytics
- forecasting
- reports
- complex multi-section views

Use dropdowns/popovers for:

- filters
- date ranges
- status selection
- sorting
- contextual menus

## Data Requirements

Mock data should include:

- ds: booking date
- y: passenger count or demand value
- gross revenue
- operational cost
- marketing cost
- destination/package
- inventory level
- booking status
- payment status

Keep mock data realistic for a B2B travel and tours agency.

## Documentation Maintenance

When a feature or planning direction changes, update the relevant docs:

- `information/sprint-1-direction.md`
- `information/module-map.md`
- `information/api-map.md`
- `information/database-map.md`
- `information/ui-components.md`
- `information/decisions.md`
- `information/feature-log.md`
- `markdowns/Sprint 1 Premium Design Plan.md`
- `markdowns/page-by-page implementation guide.md`

Use the `information` folder for living project maps and the `markdowns` folder for broader guidance and implementation plans.

## Final Principle

If a business owner cannot understand the screen in 5 seconds, simplify it.
