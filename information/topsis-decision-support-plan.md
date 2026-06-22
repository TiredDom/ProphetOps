# TOPSIS Decision Support Plan

This is the active algorithm direction for the capstone pivot.

## Locked Research Direction

Title:

`ProphetOps: A Business Decision Support System Using TOPSIS for Travel Operations Management`

The system remains a business Decision Support System. TOPSIS is the named algorithm inside the DSS, used to rank travel operation or package alternatives based on multiple criteria.

## Why TOPSIS

TOPSIS is suitable because ProphetOps needs to compare several alternatives using multiple criteria. It is stronger than a simple weighted sum but still explainable for capstone defense.

The basic decision question is:

`Which available travel option is closest to the ideal choice and farthest from the worst choice?`

## Decision Scope

The TOPSIS module should support decisions such as:

- Which package should staff recommend for a client inquiry?
- Which package should admin prioritize or promote?
- Which package is risky because of low slots, cost, or unreliable supplier data?
- Which option best fits budget, destination, duration, capacity, and business value?

## Data Reality

Do not design around supplier APIs.

Supplier/package information is fragmented and may come from:

- Google Sheets
- Messenger or chat messages
- supplier posters
- direct verbal/manual communication
- internal staff encoding

The system should standardize this information in the package catalog before applying TOPSIS.

## Candidate Criteria

Initial criteria:

- Budget fit
- Destination match
- Duration fit
- Available slots or capacity
- Supplier reliability
- Profit or business value
- Travel type match
- Risk level

Keep criteria editable later if possible, but the first implementation can use fixed default weights.

## Criteria Types

Benefit criteria:

- destination match
- available slots
- supplier reliability
- profit/business value
- travel type match

Cost criteria:

- price gap from budget
- operational risk
- duration mismatch

## Suggested First Weights

Use simple defaults first:

| Criterion | Weight |
| --- | ---: |
| Budget fit | 0.20 |
| Destination match | 0.20 |
| Available slots | 0.15 |
| Supplier reliability | 0.15 |
| Duration fit | 0.10 |
| Travel type match | 0.10 |
| Profit/business value | 0.10 |

Weights must total 1.00.

## Output Requirements

The result should show:

- ranked alternatives
- TOPSIS score or closeness coefficient
- best option
- criteria summary
- explanation in business language
- warning if no option is a strong match

Example explanation:

`Rank 1 was selected because it fits the target destination and budget, has available slots, and has stronger supplier reliability than the other options.`

## Implementation Roadmap

1. Done: Add TOPSIS service/helper in the backend.
2. Done: Add seed/sample package fields needed for scoring.
3. Done: Add request validation for decision criteria.
4. Done: Add controller page prop for rankings.
5. Done: Add UI page for entering priorities and viewing ranked options.
6. Done: Add tests for ranking order, empty data, invalid weights, and tied results.
7. Done: Update dashboard/trajectory wording from forecast/AI language to TOPSIS decision support.
8. Next: Update capstone paper sections to match TOPSIS.

Implemented files:

- `app/Support/TopsisDecisionSupport.php`
- `app/Http/Controllers/ForecastingController.php`
- `resources/js/Pages/ForecastingPreview.vue`
- `database/migrations/2026_06_19_000050_add_topsis_fields_to_travel_packages_table.php`
- `tests/Feature/TopsisDecisionSupportTest.php`

## Paper Sections To Update

- Title
- Abstract
- Statement of the Problem
- Scope and Limitations
- Conceptual Framework / IPO
- Related Literature
- Algorithm discussion
- Research Instrument
- Data Gathering Procedure
- Evaluation task script

## What Not To Claim

- Do not claim Meta Prophet is running.
- Do not claim AI-generated recommendations.
- Do not claim supplier/Facebook API integration.
- Do not claim customer-facing booking support.
