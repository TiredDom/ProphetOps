# ProphetOps Code Navigation

Use this file when you need to know which code files to open first. It is a quick map, not a full explanation of every line.

## Start Here By Task

| Task | Open First | What It Does |
| --- | --- | --- |
| Routes and page access | `routes/web.php` | Defines page routes, write routes, auth protection, and role access labels. |
| Shared backend data | `app/Support/ProphetOpsData.php` | Centralizes demo accounts, role permissions, page transforms, summaries, and DSS helper data. |
| Login and logout | `app/Http/Controllers/AuthController.php` | Handles Laravel session login, logout, throttling, and demo account auth. |
| Role protection | `app/Http/Middleware/EnsureRoleCanAccess.php` | Blocks pages that a user's role should not access. |
| Shared page props | `app/Http/Middleware/HandleInertiaRequests.php` | Sends logged-in user data and shared props to Vue pages. |
| Dashboard | `app/Http/Controllers/DashboardController.php`, `resources/js/Pages/Welcome.vue` | Builds and displays the owner DSS overview. |
| Bookings | `app/Http/Controllers/BookingController.php`, `app/Models/Booking.php`, `resources/js/Pages/Bookings.vue` | Manages booking records, filters, forms, and bulk updates. |
| Package catalog | `app/Http/Controllers/InventoryController.php`, `app/Models/TravelPackage.php`, `resources/js/Pages/Inventory.vue` | Manages travel packages, slots, package status, and availability. |
| Expenses | `app/Http/Controllers/ExpenseController.php`, `app/Models/Expense.php`, `resources/js/Pages/Expenses.vue` | Manages operational cost records and expense summaries. |
| Analytics | `app/Http/Controllers/AnalyticsController.php`, `resources/js/Pages/SalesAnalytics.vue` | Builds sales, demand, revenue, and cost summaries. |
| Forecast | `app/Http/Controllers/ForecastController.php`, `app/Support/HoltWintersForecaster.php`, `app/Support/ForecastInsight.php`, `resources/js/Pages/Forecast.vue` | Projects future monthly revenue from sample sales history using Holt-Winters and explains the result. |
| DSS review signals | `app/Support/ProphetOpsData.php`, `resources/js/Pages/Welcome.vue`, `resources/js/Pages/Reports.vue` | Builds explainable dashboard/report action cards from saved records. |
| Reports | `app/Http/Controllers/ReportController.php`, `resources/js/Pages/Reports.vue` | Shows internal report cards and report summaries. |
| Users | `app/Http/Controllers/UserAccessController.php`, `resources/js/Pages/Users.vue` | Displays internal users and access state. |
| Reusable layout | `resources/js/Components/layout/` | Holds sidebar, top bar, and app shell. |
| Reusable charts | `resources/js/Components/charts/` | Holds shared chart shells and graph components. |
| Reusable records UI | `resources/js/Components/records/` | Holds table, filter, and bulk-action components. |
| Reusable feedback UI | `resources/js/Components/feedback/` | Holds modal, drawer, notice, and empty-state components. |
| Styling | `resources/css/app.css` | Main visual tokens, layouts, and component styles. |
| Tables | `database/migrations/` | Defines database schema. |
| Demo data | `database/seeders/` | Seeds users, packages, bookings, and expenses. |

## Current Algorithm Direction

For algorithm work, open `information/forecasting-holt-winters.md` before touching code.

Implemented areas:

- Holt-Winters additive triple exponential smoothing service/helper
- level, trend, and seasonal component tracking
- monthly revenue projection output
- plain-language explanation

Likely future areas:

- persisted forecast run history
- tunable smoothing parameters
- deeper dashboard or report summaries

## Clean Reading Rule

Open the smallest relevant file first. If the task is about one page, read that page, its controller, its model if any, and `app/Support/ProphetOpsData.php`. Avoid reading old planning files unless the current maps point there.
