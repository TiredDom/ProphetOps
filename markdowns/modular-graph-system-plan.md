# Modular Graph System Plan

Status: current UI reference.

Forecasting examples in this file are chart-pattern examples only. They do not make Meta Prophet the active algorithm direction. For the active algorithm, read `../information/topsis-decision-support-plan.md`.

## Purpose

This plan defines how graphs should be built across ProphetOps so every chart feels contained, premium, reusable, and presentation-ready.

The current Forecasting graph shows the problem clearly: the graph is visually too dominant, uses custom page-specific markup, and can feel like it is stretching inside the panel instead of sitting inside a controlled chart area.

The next implementation should create reusable graph components instead of building every chart directly inside each page.

## Core Rule

Every graph must live inside a controlled graph shell.

Do not place raw chart markup directly into pages unless it is a tiny one-off progress bar.

Use a shared chart component for:

- Forecasting line graph
- Dashboard forecast mini trend
- Sales Analytics revenue trend
- Sales Analytics booking volume
- Revenue vs expenses comparison
- Future report summary graphs

## Recommended Components

Create these later under:

`resources/js/Components/charts/`

### ChartPanel

Purpose:
Reusable chart shell.

Responsibilities:

- Fixed chart header area
- Optional eyebrow/title/action slot
- Controlled chart body
- Optional legend
- Optional short caption
- Consistent padding and border radius
- Prevent overflow outside its panel

Suggested props:

- `eyebrow`
- `title`
- `caption`
- `legend`
- `height`
- `compact`

### LineTrendChart

Purpose:
Reusable line chart for Forecasting and trend charts.

Use for:

- Forecasting main graph
- Dashboard forecast mini trend
- Sales trend
- Booking volume trend

Supported features:

- Actual line
- Forecast line
- Confidence range band
- Forecast-start marker
- Minimal x-axis labels
- Optional y-axis labels
- Empty/loading state

Suggested props:

- `points`
- `series`
- `xKey`
- `yDomain`
- `height`
- `showAxis`
- `showForecastMarker`
- `confidenceRange`

### MiniBarChart

Purpose:
Reusable contained bar chart for simple comparisons.

Use for:

- Short analytics comparisons
- Revenue by destination
- Package performance

Rules:

- Max 5 to 7 visible bars in a compact card
- Use table/list if the data has many categories
- Do not use grouped bars for the main Forecasting page

### ComparisonTrack

Purpose:
Reusable horizontal comparison/progress chart.

Use for:

- Revenue vs expenses
- Cost category share
- Inventory capacity usage

Rules:

- Short labels
- Values aligned at the edge
- Bars remain inside the card

## Chart Containment Standards

Every graph component must follow these standards:

- `width: 100%`
- `max-width: 100%`
- `overflow: hidden`
- stable chart height
- stable inner plotting area
- no layout shift when labels change
- no text outside the graph container
- no graph should push beyond its card
- no horizontal page overflow
- no labels clipped at mobile width

Recommended heights:

- Main Forecasting line graph: 300px to 360px desktop
- Main Forecasting line graph: 240px to 300px mobile
- Dashboard mini trend: 120px to 180px
- Analytics compact chart: 180px to 240px

Recommended CSS approach:

- Use a fixed chart body class such as `.chart-frame`
- Use SVG with `viewBox` for line charts
- Use `preserveAspectRatio="none"` only for simple fills, not labels
- Keep axis labels inside the SVG or in a fixed grid below it
- Use `overflow: hidden` on the shell and `overflow-x: auto` only for dense category charts

## Data Shape Standard

Use one consistent chart data shape where possible.

Recommended Forecasting data shape:

```js
[
    {
        label: 'Jun 01',
        actual: 18,
        forecast: 17,
        lower: 14,
        upper: 20,
        phase: 'actual',
    },
    {
        label: 'Jun 09',
        actual: null,
        forecast: 7,
        lower: 4,
        upper: 10,
        phase: 'forecast',
    },
]
```

Recommended series config:

```js
[
    { key: 'actual', label: 'Actual demand', tone: 'actual' },
    { key: 'forecast', label: 'Forecast demand', tone: 'forecast' },
]
```

This keeps the chart component reusable and avoids page-specific chart logic.

## Forecasting Page Rule

The Forecasting page should use one modular `LineTrendChart`.

It should not use:

- grouped vertical bars
- repeated lower mini charts
- raw chart markup directly inside `ForecastingPreview.vue`
- long explanations under the chart

The graph should be visually contained inside a premium chart frame with:

- one title
- one legend
- one clear chart area
- one nearby Prescriptive DSS Signal panel

## Dashboard Rule

The Dashboard should use a compact graph variant.

It should show:

- forecast direction
- a simple trend line or compact sparkline
- link to Forecasting

It should not show:

- full actual-vs-forecast details
- confidence interval explanation
- technical labels
- formula/data-field details

## Analytics Rule

Sales Analytics can use modular graphs, but keep them contained and practical.

Use:

- `MiniBarChart` for category comparisons
- `LineTrendChart` for monthly or date trends
- `ComparisonTrack` for revenue vs expenses

Avoid:

- too many chart cards on the first screen
- charts that duplicate table content
- chart captions longer than one sentence

## Text Rule

Graphs should not have long paragraphs under them.

Allowed:

- one short caption when it adds business meaning
- one nearby DSS signal card
- one tooltip or modal for extra details later

Avoid visible app terms such as:

- mockup
- mock
- preview
- sample only
- placeholder
- pending integration

Keep prototype disclosure in documentation and presentation notes.

## Implementation Steps For Later

1. Create `resources/js/Components/charts/ChartPanel.vue`.
2. Create `resources/js/Components/charts/LineTrendChart.vue`.
3. Create `resources/js/Components/charts/MiniBarChart.vue` if analytics still needs compact bar charts.
4. Create `resources/js/Components/charts/ComparisonTrack.vue` for revenue/cost and capacity comparisons.
5. Move chart scaling helpers into a small chart utility file if repeated logic grows.
6. Replace the Forecasting page graph with `ChartPanel` + `LineTrendChart`.
7. Replace Dashboard forecast snapshot with a compact `LineTrendChart`.
8. Replace Sales Analytics mini charts with modular chart components.
9. Remove old ad hoc graph CSS after all pages are migrated.

## Acceptance Criteria

- Every chart is inside a reusable chart shell.
- No graph overflows its card on desktop or mobile.
- Chart heights are stable and consistent.
- Forecasting uses one clean line graph.
- Dashboard uses a compact trend graph only.
- Analytics charts use the same spacing and containment rules.
- Long explanations do not sit under charts.
- Graph components can receive new data through props.
- The app UI avoids prototype-disclaimer wording during presentation.

## Out Of Scope

- Real charting libraries unless explicitly requested
- Real Meta Prophet integration
- Real AI-generated prescriptions
- Backend chart endpoints
- Exporting chart images
