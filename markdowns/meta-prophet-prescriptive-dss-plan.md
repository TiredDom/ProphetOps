# Meta Prophet And Prescriptive DSS Plan

This is the planning reference for the main forecasting feature of ProphetOps.

The goal is not only to show a forecast. The goal is to use Meta Prophet forecast output as the evidence layer for clear, explainable business recommendations on the dashboard.

Current status:

- Planning only.
- No code implementation in this step.
- Current Sprint 1 pages may use mock/sample data.
- The UI must not imply that Meta Prophet or real AI is already running until the backend integration exists.

## Feature Goal

ProphetOps should support this flow:

Business records -> Forecast dataset -> Meta Prophet forecast -> Prescriptive DSS rules -> Dashboard action guidance

The forecast answers:

- What demand or revenue is likely to happen next?
- Which package, destination, or period may increase or decline?
- How uncertain is the prediction?

The prescriptive DSS layer answers:

- What does the forecast mean for the business?
- What should the owner, admin, or staff review next?
- Which action is most urgent?

## Paper Alignment

This feature should be treated as the core research value of ProphetOps.

Suggested paper framing:

"The system uses Meta Prophet to forecast travel service demand and projected revenue from historical booking records. A prescriptive decision-support layer then translates the forecast, inventory status, and operational cost signals into explainable recommended actions for business users."

Important academic positioning:

- Prophet is the forecasting method.
- The prescriptive layer is the DSS interpretation method.
- Recommendations should be explainable and rule-based first.
- The system should not act like a black-box AI assistant.
- The dashboard must make the owner understand the next action quickly.

## Scope Boundary

Current prototype behavior:

- Forecasting page may show sample projection data.
- Dashboard may show sample forecast-driven insight cards.
- Trajectory Insights may show simulated DSS interpretation.
- Labels must clearly say sample, simulated, or integration pending.

Future real implementation:

- Use backend business records as the source of truth.
- Transform records into Prophet-ready time series data.
- Run Meta Prophet through a backend service or Python runner.
- Store forecast runs and forecast points.
- Generate prescriptive insight cards from stored forecast output.
- Display the latest approved forecast and recommendations on the dashboard.

Do not implement real Prophet, backend jobs, or real AI generation until the project explicitly moves into that phase.

## Forecast Data Pipeline

1. Source data collection

Use internal business records:

- Bookings / Transactions
- Inventory
- Expenses / Operational Costs
- Package and destination references

2. Forecast dataset preparation

Prepare the primary Prophet dataset with:

- `ds`: booking date
- `y`: demand value

Recommended primary `y`:

- passenger count

Optional secondary forecast targets:

- booking count
- gross revenue
- package-level demand
- destination-level demand

Optional future regressors:

- marketing cost
- operational cost
- inventory level
- package capacity
- holiday or seasonal flags
- promo/campaign flag
- destination/package category

3. Data readiness checks

Before a real forecast run, validate:

- Enough historical records exist.
- Dates are complete and correctly formatted.
- Cancelled or invalid bookings are excluded or handled consistently.
- Missing passenger counts, revenue, and cost values are resolved.
- Outliers are reviewed instead of blindly removed.
- Data source range is shown to the user.

4. Prophet forecast output

Each model run should produce:

- forecast run id
- forecast target
- source range
- projection range
- generated timestamp
- model status
- forecast date
- `yhat`
- `yhat_lower`
- `yhat_upper`
- trend signal
- seasonality notes where available

5. Prescriptive DSS transformation

The prescriptive layer reads:

- forecast direction
- confidence interval width
- recent actual performance
- inventory/capacity status
- expense and marketing cost movement
- package or destination context

Then it creates business-readable actions.

6. Dashboard display

The dashboard should show:

- one short forecast-driven business gist
- key forecast KPIs
- up to three highest-priority prescriptions
- a link to the full Forecasting and Trajectory Insights pages

## Prescriptive Rule Examples

Each rule should follow:

Forecast signal -> Business meaning -> Prescribed action

| Forecast signal | Business meaning | Prescribed action |
| --- | --- | --- |
| Demand is forecasted to rise and inventory is low | The agency may lose bookings because available slots may not cover expected demand | Review supplier capacity and reserve additional slots before accepting more promotions |
| Demand is forecasted to rise and costs are stable | There is a revenue opportunity with manageable cost pressure | Prioritize high-demand packages and prepare staff/supplier coordination |
| Demand is forecasted to decline while marketing cost is rising | Spending may be increasing without enough booking conversion | Review campaign performance before approving more promo spend |
| Revenue is forecasted to rise but expenses are rising faster | Profit may weaken even if sales look positive | Check cost categories and negotiate operational expenses where possible |
| Demand is forecasted to decline and inventory is high | Available slots may remain unused | Consider targeted promos or adjust package commitments |
| Forecast confidence interval is wide | The model has high uncertainty | Review data quality and avoid strong operational commitments |
| Peak season is approaching | Booking pressure may increase soon | Prepare capacity, staff assignment, and partner coordination early |
| One destination has a package-level demand spike | A specific route may need operational attention | Prioritize availability and check related cost movement for that package |

These rules can start simple. A transparent rule engine is better for the paper than unexplained AI suggestions.

## Dashboard Integration Plan

The dashboard should become the place where forecast output becomes action.

Recommended dashboard sections:

- Business Gist: one sentence summarizing the latest forecast signal and recommended review.
- Forecast KPI cards: forecasted demand, projected revenue, confidence/status, priority actions.
- Priority prescriptions: maximum three cards on the first screen.
- Forecast preview: compact chart or trend strip, clearly linked to the full Forecasting page.

Dashboard density rule:

- Do not turn the dashboard into a full forecasting workspace.
- Keep the first screen glanceable within 5 seconds.
- Put detailed model output and historical checks on the Forecasting page.
- Put the complete recommendation list on Trajectory Insights or a future Prescriptive Insights page.

## Forecasting Page Plan

The Forecasting page should evolve from "Forecasting Preview" into a "Meta Prophet Forecasting Workspace" only when real backend integration starts.

Required future sections:

- Forecast run status
- Forecast target selector: passenger demand, booking count, revenue
- Forecast horizon selector: 7 days, 30 days, 90 days, monthly
- Source data readiness panel
- Demand forecast chart with confidence interval
- Revenue forecast chart with confidence interval
- Seasonality and trend notes
- Forecast assumptions and limitations
- Latest generated timestamp
- Link to prescriptive recommendations

Until real integration exists, keep labels such as:

- Sample Forecast Preview
- Forecast engine integration pending
- Sample projection based on mock data

## Trajectory Insights / Prescriptive Page Plan

Trajectory Insights should become the interpretation layer for forecast-driven decisions.

The current label can remain "Trajectory Insights" for Sprint 1, but the feature meaning should shift toward prescriptive DSS recommendations.

Each insight card should include:

- Forecast signal or observed data
- Business meaning
- Prescribed action
- Priority: Critical, High, Medium, Low
- Evidence: source metrics or forecast values
- Affected package or destination
- Time horizon
- Status: New, In Review, Resolved

Recommended filters:

- priority
- category
- package/destination
- time horizon
- insight status

Required categories:

- demand increase
- demand decline
- cost risk
- inventory risk
- marketing opportunity
- data quality risk

## Future Backend Architecture

Suggested Laravel services:

- `ForecastDatasetService`
- `ProphetForecastService`
- `PrescriptiveInsightService`
- `ForecastRunService`

Suggested future Python runner:

- `forecasting/prophet_runner.py`

Suggested future tables:

- `forecast_runs`
- `forecast_points`
- `prescriptive_insights`

Suggested flow:

1. Laravel collects clean booking, expense, and inventory records.
2. Laravel prepares a Prophet-ready dataset.
3. Laravel starts a forecast job.
4. Python runs Prophet and returns structured JSON.
5. Laravel stores the forecast run and forecast points.
6. Laravel runs the prescriptive DSS rules.
7. Dashboard reads the latest forecast run and top active prescriptions.

## Implementation Phases

Phase 1: Documentation alignment

- Add this plan.
- Reference it from the active Sprint 1 planning docs.
- Keep current UI honest about sample/simulated data.

Phase 2: Forecast-ready mock data

- Improve mock data shape to match future Prophet input/output.
- Add sample forecast points with confidence values.
- Add sample prescriptive insights with evidence.

Phase 3: Frontend prototype refinement

- Update Dashboard forecast cards.
- Update Forecasting page structure.
- Update Trajectory Insights into a stronger prescriptive DSS page.
- Keep all labels sample/simulated.

Phase 4: Backend data foundation

- Add real business tables.
- Add data quality rules.
- Add dataset preparation logic.

Phase 5: Meta Prophet proof of concept

- Add Python Prophet runner or service.
- Run forecasts from validated records.
- Store forecast runs and forecast points.

Phase 6: Prescriptive DSS engine

- Implement transparent rule-based recommendations.
- Connect recommendations to forecast outputs, inventory, and costs.
- Store insight snapshots for audit and reports.

Phase 7: Dashboard integration

- Replace sample dashboard forecast data with latest stored forecast run.
- Show top prescriptions on the first screen.
- Keep detailed review inside Forecasting and Trajectory Insights.

Phase 8: Evaluation and paper evidence

- Document forecasting accuracy using MAE, RMSE, and MAPE where appropriate.
- Document usability feedback from users or evaluators.
- Connect results to ISO 25010 criteria.

## Acceptance Criteria For The Real Feature

- Forecasting uses real business records, not hardcoded frontend data.
- Prophet input clearly maps to `ds` and `y`.
- Forecast output includes `yhat`, `yhat_lower`, and `yhat_upper`.
- Dashboard shows forecast-driven recommendations, not just charts.
- Every recommendation includes evidence and a prescribed action.
- Users can see when the forecast was generated.
- Users can distinguish forecasted values from actual values.
- Forecast limitations and uncertainty are visible.
- The system never claims AI certainty where the data is uncertain.
- The owner can understand the top recommended action within 5 seconds.

## Immediate Direction

Before coding this feature, implement the next planning-safe step:

- update the Forecasting page plan
- update the Trajectory Insights plan
- update dashboard guidance
- update data map and future route/API notes

After that, the next code phase should only refine mock frontend behavior unless the user explicitly asks to start backend Prophet integration.
