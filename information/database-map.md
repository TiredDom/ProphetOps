# ProphetOps Database And Mock Data Map

Sprint 1 is front-end focused. Use mock/sample data unless backend persistence is explicitly requested later.

## Current Database State

The project currently includes Laravel default migrations only:

- users
- password_reset_tokens
- sessions
- cache
- cache_locks
- jobs
- job_batches
- failed_jobs

No ProphetOps business tables are implemented yet.

## Sprint 1 Data Approach

Use centralized mock data files on the frontend so backend integration can replace them later.

Mock data must support the DSS model from the study.

Required fields:

- ds: booking date
- y: passenger count or demand value
- gross revenue
- operational cost
- marketing cost
- destination/package
- inventory level
- booking status
- payment status

Data should feel realistic for a B2B travel and tours agency.

## Suggested Mock Data Groups

### Demo Users

Purpose:
Support pseudo login and role-based navigation.

Fields:

- name
- role
- email
- password
- status
- last login

Demo accounts:

- owner@prophetops.local / owner123
- admin@prophetops.local / admin123
- staff@prophetops.local / staff123

### Bookings / Transactions

Purpose:
Provide the historical business records needed for sales monitoring and future forecasting.

Fields:

- booking id
- booking date
- client / agency partner
- destination / package
- passenger count
- gross revenue
- payment status
- booking status
- staff assigned
- notes

Forecasting fields:

- ds: booking date
- y: passenger count or demand value
- gross revenue
- destination/package

### Inventory

Purpose:
Track travel package availability, slots, and operational stock.

Fields:

- package name
- destination
- available slots
- sold count
- reserved count
- status: Normal, Low, Critical
- last updated
- inventory level

### Expenses / Operational Costs

Purpose:
Support cost analysis and profit estimation.

Fields:

- expense date
- category: tour operations, marketing, seasonal cost, overhead
- amount
- related destination/package
- payment status
- notes

Forecasting/DSS fields:

- operational cost
- marketing cost
- destination/package

### Analytics

Purpose:
Support sales, demand, and cost summaries from mock bookings and expenses.

Derived values:

- total revenue
- total bookings
- passenger count
- operating costs
- estimated profit
- revenue by destination
- top packages
- costliest category

### Forecast Preview

Purpose:
Prepare the UI shape for future Meta Prophet integration.

Fields:

- projection date
- projected bookings
- projected revenue
- confidence/status label
- seasonality note
- data requirement note

Required label:

"Sample Forecast Preview - Forecast engine integration pending"

### Trajectory Insights

Purpose:
Support simulated DSS insight cards.

Fields:

- insight type: Risk, Opportunity, Warning, Action, Trend
- observed data
- business meaning
- suggested action
- category
- placeholder label

Required labels:

- "Simulated DSS Insight"
- "AI trajectory module placeholder"

## Future Database Tables

These are future backend planning targets, not Sprint 1 frontend requirements.

### users

Purpose:
Store authorized internal users with roles.

Future fields:

- id
- name
- email
- password
- role
- status
- last_login_at
- created_at
- updated_at

### bookings

Purpose:
Store centralized booking and transaction records.

Future fields:

- id
- booking_code
- booking_date
- client_name
- agency_partner
- package_id
- passenger_count
- gross_revenue
- payment_status
- booking_status
- staff_assigned_id
- notes
- created_at
- updated_at

### travel_packages

Purpose:
Store package and destination references used by bookings, inventory, and analytics.

Future fields:

- id
- package_name
- destination
- base_price
- capacity
- status
- notes
- created_at
- updated_at

### inventory_items

Purpose:
Store package availability, slots, or operational stock.

Future fields:

- id
- package_id
- available_slots
- sold_count
- reserved_count
- status
- last_updated_by
- notes
- created_at
- updated_at

### inventory_movements

Purpose:
Track package slot adjustments and operational stock movements.

Future fields:

- id
- inventory_item_id
- movement_type
- quantity
- movement_date
- related_booking_id
- reason
- notes
- created_at
- updated_at

### expenses

Purpose:
Store operational cost records.

Future fields:

- id
- expense_date
- category
- amount
- package_id
- payment_status
- notes
- created_at
- updated_at

### forecast_runs

Purpose:
Store future Meta Prophet forecast outputs after real integration.

Future fields:

- id
- forecast_type
- source_range_start
- source_range_end
- projection_range_start
- projection_range_end
- status
- generated_at
- created_at
- updated_at

Important:

- Do not create this table until forecasting integration is actually in scope.

### insight_snapshots

Purpose:
Store future AI/DSS interpretation outputs after real integration.

Future fields:

- id
- insight_type
- observed_data
- business_meaning
- suggested_action
- source_period
- status
- generated_at
- created_at
- updated_at

Important:

- Do not create this table until AI trajectory integration is actually in scope.
