# ProphetOps Planning Docs

This folder contains current reference notes and older implementation guidance for the ProphetOps DSS prototype.

Before reading this folder, start with `../information/README.md`.

Active capstone direction:

- `ProphetOps: A Business Decision Support System Using Holt-Winters Forecasting for Travel Operations Management`
- Holt-Winters forecasting (additive triple exponential smoothing) is the active algorithm direction, shipped at `/forecast`.
- Older TOPSIS and Meta Prophet planning docs are historical unless the team explicitly restores them.

Read in this order:

1. `../information/README.md`
2. `../information/forecasting-holt-winters.md`
3. `../information/module-map.md`
4. `../information/api-map.md`
5. `../information/database-map.md`
6. `AI READ THIS FIRST.md`
7. `Sprint 1 Premium Design Plan.md`
8. `modular-graph-system-plan.md`
9. `sprint-1-modal-interaction-plan.md`
10. `legacy-removal-plan.md`
11. `page-by-page implementation guide.md`
12. `general knowledge.md`
13. `setup guide.md`

Current Sprint 1 direction:

- Internal decision-support dashboard for Renan-Tina Travels and Tours.
- Laravel-backed local prototype using saved records for core pages.
- Laravel session login with seeded demo accounts.
- Role-aware navigation.
- Stripe + Attio inspired premium business UI.
- Holt-Winters forecasting is the active algorithm direction, shipped at `/forecast` (see `../information/forecasting-holt-winters.md`).
- TOPSIS and Meta Prophet docs are historical unless explicitly restored.
- Graphs should be contained and reusable according to `modular-graph-system-plan.md`.
- Modal and drawer behavior for clickable Sprint 1 actions should follow `sprint-1-modal-interaction-plan.md`.
- Legacy Operational Records and standalone Data Validation cleanup is recorded in `legacy-removal-plan.md`.

## Document Status

| File | Status | Use For |
| --- | --- | --- |
| `AI READ THIS FIRST.md` | Current bridge | Quick assistant rules after reading `information/README.md` |
| `modular-graph-system-plan.md` | Current UI reference | Reusable chart containment and graph rules |
| `legacy-removal-plan.md` | Current cleanup reference | Legacy page/file cleanup history |
| `setup guide.md` | Current local setup reference | Running the project with SQLite |
| `Sprint 1 Premium Design Plan.md` | Historical | Old UI/product direction only |
| `forecasting-page-simplification-plan.md` | Historical | Old Meta Prophet page ideas only |
| `page-by-page implementation guide.md` | Historical | Old page intent only |
| `general knowledge.md` | Historical | Broad context only |
| `sprint-1-modal-interaction-plan.md` | Historical/reference | Modal/drawer ideas only |

Removed from active planning:

- Standalone Operational Records page plan
- Standalone Data Validation page plan

Those ideas are now folded into Bookings / Transactions, data quality behavior, analytics, reports, and future backend work.
