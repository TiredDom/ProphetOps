# Forecasting Page Simplification Plan

Status: historical Meta Prophet planning reference.

Read `information/README.md` and `information/topsis-decision-support-plan.md` first. Meta Prophet is no longer the active capstone algorithm direction unless the team explicitly restores forecasting.

## Purpose

This plan is for simplifying the Sprint 1 Forecasting page before the next implementation pass.

The page should present Meta Prophet forecasting as the main paper feature, but it should not look like a technical formula screen. It should feel like a premium decision-support page that helps a business user understand:

- what demand is expected to do
- how confident the sample forecast looks
- what action the agency should consider next

Internally, Sprint 1 remains mock-only. Do not add backend forecasting, real Meta Prophet execution, real AI generation, database tables, jobs, or external services.

Important presentation rule:

- The app UI should not repeatedly say `mockup`, `mock`, `preview`, `sample only`, `placeholder`, or `forecast engine integration pending`.
- The team can explain during the presentation that Sprint 1 is a mock/prototype.
- Documentation can still say the feature is mock-only.
- The visible app should look like a polished DSS product screen.

## Current Page Review

Current file:

- `resources/js/Pages/ForecastingPreview.vue`

Current route:

- `/forecasting`

Current strengths:

- The page already separates Forecasting from the Dashboard.
- It currently discloses sample/prototype status, but that disclosure should move out of the visible UI.
- It already has mock actual, forecast, and confidence values.
- It links to Trajectory Insights, which supports the prescriptive DSS story.

Current issues:

- The main bar chart looks heavy and can be misread as a visual bug.
- The page has too many graph sections for Sprint 1.
- Revenue Projection and Booking Projection repeat the same idea in smaller charts.
- Seasonality Notes, Prophet Input Fields, and Integration Status create too much text below the main graph.
- Showing `ds`, `y`, `yhat`, `yhat_lower`, and `yhat_upper` on the visible page feels too technical for a business-facing DSS screen.
- The page explains the prototype too much instead of letting the layout communicate the flow.
- Visible words like `mockup`, `preview`, and `sample only` make the presentation feel less professional.

## New Direction

Use one clean line graph as the main Forecasting visual.

The page should answer one simple story:

Historical demand -> sample forecast trend -> confidence range -> suggested DSS action

The Forecasting page should not become a full analyst workspace in Sprint 1.

## Recommended Page Structure

### 1. Page Header

Keep:

- Page title: `Forecasting`
- Small label: `Meta Prophet Forecast`
- Badge: `DSS view`

Simplify copy to one short sentence:

`Demand trend and forecast-driven decision signal for upcoming travel operations.`

Primary action:

- `Review Prescriptive Insights`

Secondary action:

- `Review Bookings`

Avoid:

- Long explanation paragraphs
- Formula terms in the header
- Claims that Prophet is already running
- Visible words such as `mockup`, `mock`, `preview`, `sample only`, `placeholder`, and `pending integration`

### 2. Forecast Summary Cards

Keep a compact row of 3 or 4 summary cards.

Recommended cards:

- Forecast Horizon: `30 days`
- Expected Direction: `Rising`
- Recommended Review: `Capacity and costs`
- DSS Signal: `Review before promos`

Remove or rename:

- `Forecast Input: ds + y`
- Any card that looks like a formula explanation

### 3. Main Forecast Line Graph

Replace the current vertical bar chart with a simple line graph.

Follow the shared graph system plan:

- `modular-graph-system-plan.md`

The graph should be built through a reusable chart shell and line chart component later, not as one-off page markup.

Recommended graph title:

`Demand Forecast Trend`

Graph elements:

- Solid line for historical actual demand
- Purple line for sample forecast demand
- Light purple shaded band for confidence interval
- Subtle vertical marker where forecast starts
- Minimal x-axis date labels
- Minimal y-axis demand labels if needed

Legend:

- Actual demand
- Forecast demand
- Confidence range

Keep the legend short and visually quiet.

Containment:

- graph must stay inside one chart frame
- graph height must be stable on desktop and mobile
- no labels or lines should overflow the panel
- chart should not create horizontal page scrolling
- long descriptions should not sit under the chart

Do not show:

- Formula fields
- Long model explanation
- Multiple bar groups
- Repeated mini charts below the main chart
- UI labels that say the screen is mock, preview, sample, or placeholder

### 4. DSS Prescriptive Summary

Add one clear business recommendation near the graph.

Recommended title:

`Prescriptive DSS Signal`

Recommended content format:

- Signal: demand is forecasted to rise
- Meaning: capacity and costs should be reviewed before more promos
- Suggested action: review supplier slots and campaign costs

This should be business-friendly and explainable. It should connect the forecast to a decision.

Optional layout:

- Place this as a side panel beside the graph on desktop.
- Place it below the graph on mobile.

### 5. Lower Page Content

Remove from the visible main page:

- Revenue Projection mini chart
- Booking Projection mini chart
- Seasonality Notes card
- Prophet Input Fields card
- Integration Status card

If the team still wants this information later, move it into one optional section:

- `Model Notes`
- `Forecast Assumptions`
- `Data Requirements`

For Sprint 1, this optional section should be collapsed, hidden behind a modal, or kept only in documentation.

## Formula And Technical Detail Rule

Do not show formulas or field names on the main app screen.

Keep these in documentation only:

- `ds`
- `y`
- `yhat`
- `yhat_lower`
- `yhat_upper`
- Prophet formula explanations
- error metrics such as MAE, RMSE, and MAPE

Reason:

The app should feel like a professional DSS interface for owners/admins, not a notebook or research appendix.

Where formulas belong:

- paper methodology section
- technical documentation
- future model notes modal, if the user asks for it

## Presentation Disclosure Rule

The app should look polished during presentation. Do not put prototype disclaimers directly on the visible page.

Remove or avoid these visible UI terms:

- `Mockup`
- `Mock`
- `Preview`
- `Sample only`
- `Placeholder`
- `Forecast engine integration pending`
- `AI trajectory module placeholder`

Use professional app labels instead:

- `Forecasting`
- `Meta Prophet Forecast`
- `Demand Forecast Trend`
- `Forecast demand`
- `Confidence range`
- `Prescriptive DSS Signal`
- `Trajectory Insights`

Disclosure still belongs in:

- the presenter script
- project documentation
- paper limitations
- technical handoff notes

## Dashboard Relationship

Dashboard:

- Shows only the compact executive forecast trend.
- Does not show actual-vs-forecast technical detail.
- Links to the Forecasting page.

Forecasting page:

- Shows the full line graph.
- Shows sample confidence range.
- Explains the forecast signal in business terms.
- Links to Trajectory Insights for full prescriptive cards.

Trajectory Insights:

- Shows the complete DSS recommendation list.
- Expands the business meaning and suggested action.

## Implementation Steps For Later

1. Replace the current bar-based `prophet-graph-large` section with a line graph component or SVG/CSS chart inside `ForecastingPreview.vue`.
2. Use existing `prophetForecastSeries` mock data for actual, forecast, and confidence values.
3. Remove the Revenue Projection and Booking Projection mini chart sections from the main page.
4. Remove the visible Seasonality Notes, Prophet Input Fields, and Integration Status cards.
5. Replace technical input wording with business summary cards.
6. Add a concise Prescriptive DSS Signal panel near the main graph.
7. Use product-facing labels instead of mock/prototype disclaimers on the visible page.
8. Update CSS so the graph has stable height, balanced spacing, and premium whitespace on desktop and mobile.
9. Follow `modular-graph-system-plan.md` when creating reusable chart components.

## Acceptance Criteria

- Forecasting page uses one main line graph, not multiple competing charts.
- Forecasting graph is contained inside a reusable chart shell.
- Visible UI avoids mock/prototype disclaimer language.
- No formula fields are visible on the main screen.
- Page copy is short and business-facing.
- Forecasting connects to a prescriptive DSS suggestion.
- Dashboard stays simpler than the Forecasting page.
- Trajectory Insights remains the full recommendation review page.
- No backend, real Prophet integration, or real AI is added in Sprint 1.

## Out Of Scope For Sprint 1

- Running Meta Prophet
- Forecast job scheduling
- Database storage for forecast runs
- Real confidence calculations
- Real AI-generated prescriptions
- Exporting forecast reports
- Showing academic formulas in the app UI
