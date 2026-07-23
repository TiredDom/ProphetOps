# ProphetOps Database And Data Map

ProphetOps now uses local Laravel database persistence for the core demo system. Use this map when changing seeded data, migrations, data transforms, or page props.

## Current Database State

The project currently includes Laravel default migrations plus ProphetOps business tables:

- users
- travel_packages
- bookings
- expenses
- password_reset_tokens
- sessions
- cache
- cache_locks
- jobs
- job_batches
- failed_jobs

The local `.env` is configured for SQLite at `database/database.sqlite` because the local MySQL service was not available during backend setup.

## Current Data Approach

Use database records transformed through `app/Support/ProphetOpsData.php` so Vue pages receive clean camelCase props.

Saved data should support the DSS model from the study.

Current DSS fields:

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

## Forecasting Algorithm Data Requirements

Holt-Winters (additive triple exponential smoothing) is the active capstone algorithm. It runs from `ProphetOpsData::salesHistory()`, a deterministic sample series of 36 months of monthly revenue. See `information/forecasting-holt-winters.md` before adding algorithm fields, migrations, or UI output.

The forecaster only needs an ordered monthly revenue series (period label plus revenue value); the seasonal, trend, and level components are derived internally rather than stored.

Useful current fields for the sales-history series:

- period label (month)
- monthly revenue

TOPSIS was explored earlier but was not adopted. Any TOPSIS fields below are future/not-implemented ideas only. The first TOPSIS version could rank travel packages using standardized internal data.

Possible future TOPSIS fields (not implemented):

- package name
- destination
- duration
- base price
- available slots
- supplier reliability score
- travel type
- business value score
- risk score
- sold count
- reserved count
- gross revenue
- operating cost
- booking volume
- payment or booking status

Likely fields to add later:

- customer priority tags
- criteria weight set
- persisted TOPSIS closeness coefficient
- persisted decision explanation

Do not design the data model around supplier APIs or Facebook APIs. Source data may be manually standardized from sheets, messages, posters, and staff communication.

## Current Data Groups

### Demo Users

Purpose:
Support Laravel login and role-based navigation.

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
Provide the historical business records needed for sales monitoring and decision support.

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

Decision-support fields:

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

Decision-support fields:

- operational cost
- marketing cost
- destination/package

### Analytics

Purpose:
Support sales, demand, and cost summaries from saved bookings and expenses.

Derived values:

- total revenue
- total bookings
- passenger count
- operating costs
- estimated profit
- revenue by destination
- top packages
- costliest category

### Forecast Preview / Planning Trend

Purpose:
Support simple trend review from saved booking records. This is a supporting view; the active capstone algorithm is Holt-Winters forecasting (see the Forecasting Algorithm Data Requirements section).

Current planning fields:

- projection date
- projected bookings
- projected revenue
- confidence/status label
- seasonality note
- data requirement note

Current behavior:
Uses saved booking records and a simple planning trend only. Do not imply Meta Prophet is already running.

Historical forecasting fields such as `yhat`, `yhat_lower`, and `yhat_upper` only matter if the team explicitly restores forecasting.

### DSS Review Signals

Purpose:
Support DSS-style insight cards and future prescriptive recommendations.

Fields:

- insight type: Risk, Opportunity, Warning, Action, Trend
- criterion signal or observed signal
- observed data
- business meaning
- prescribed action
- priority
- evidence
- affected package/destination
- time horizon
- category
- insight status

Current behavior:
Uses saved bookings, package presets, and expenses to produce explainable action cards. No real AI generation is implemented.

## Implemented Business Tables

These tables are implemented locally.

### users

Purpose:
Store authorized internal users with roles.

Fields:

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

Fields:

- id
- code
- booking_date
- passenger_count
- client
- travel_package_id
- package_name
- package_code
- entry_type
- destination
- gross_revenue
- payment_status
- booking_status
- staff_assigned
- source
- notes
- created_at
- updated_at

### travel_packages

Purpose:
Store package presets used by bookings, inventory, and analytics.

Fields:

- id
- code
- package_name
- destination
- duration
- base_price
- inclusions
- travel_type
- supplier_reliability_score
- business_value_score
- risk_score
- available_slots
- sold_count
- reserved_count
- status
- last_updated_at
- created_at
- updated_at

### expenses

Purpose:
Store operational cost records.

Fields:

- id
- code
- expense_date
- category
- amount
- related_package
- payment_status
- notes
- created_at
- updated_at

## Future TOPSIS Database Tables

These are future integration targets, not current requirements.

### topsis_runs

Purpose:
Store each TOPSIS ranking run if the team needs ranking history.

Future fields:

- id
- run_name
- target_decision
- weight_set_name
- criteria_snapshot
- alternatives_snapshot
- status
- error_message
- generated_at
- created_at
- updated_at

Important:

- Do not create this table until TOPSIS persistence is actually in scope.

### topsis_results

Purpose:
Store ranked alternatives from each TOPSIS run.

Future fields:

- id
- topsis_run_id
- alternative_type
- alternative_id
- alternative_name
- normalized_values
- weighted_values
- positive_distance
- negative_distance
- closeness_coefficient
- rank
- explanation
- created_at
- updated_at

Important:

- Do not create this table until the TOPSIS output needs to be persisted.

### insight_snapshots

Purpose:
Store future DSS interpretation outputs after TOPSIS or other decision-support logic is implemented.

Future fields:

- id
- insight_type
- topsis_run_id
- criterion_signal
- observed_data
- business_meaning
- prescribed_action
- priority
- evidence
- affected_package_id
- affected_destination
- time_horizon
- source_period
- status
- generated_at
- created_at
- updated_at

Important:

- Do not create this table until prescriptive DSS integration is actually in scope.

Future naming note:

- If the project needs clearer paper alignment later, `insight_snapshots` can evolve into `prescriptive_insights`.

## Historical Forecast Tables

Older docs mention `forecast_runs` and `forecast_points` for Meta Prophet. Treat those as historical notes only unless the team explicitly restores forecasting as a project feature.
