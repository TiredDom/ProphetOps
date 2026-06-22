# ProphetOps IAS Demo Guide

Use this guide for the Information Assurance and Security demo and the test case tracker walkthrough.

## Demo URL

Open the local website:

```text
http://127.0.0.1:8000/login
```

Use the built/demo mode for ZAP and presentation:

```bash
npm run build
php artisan serve
```

## Demo Accounts

Use Owner for the main walkthrough:

```text
owner@prophetops.local / owner123
```

Other accounts:

```text
admin@prophetops.local / admin123
staff@prophetops.local / staff123
```

Invalid login test:

```text
wrong@example.com / wrongpass
```

## Smooth Demo Order

1. Open `/login`.
2. Show invalid login rejection.
3. Log in as Owner.
4. Open Dashboard.
5. Open Bookings and show search/filter.
6. Add or edit one booking.
7. Open Package Catalog.
8. Add one package and show invalid score validation.
9. Open Expenses.
10. Add or edit one expense.
11. Open Analytics.
12. Open Package Decision Guide and run the comparison.
13. Open Reports.
14. Open Users.
15. Log out.
16. Log in as Staff and show restricted access.
17. Run or show ZAP results.

## Test Case Inputs

### Booking Add

```text
Client: Assurance Travel Group
Package: PKG-105 / Bohol Countryside Tour
Date: 2026-06-22
Passenger count: 4
Revenue: 48000
Payment: Pending
Status: Reserved
Staff: Staff User
Source: Messenger
Notes: Demo booking for IAS test case.
```

Expected result:

```text
Booking is saved and appears in the bookings table.
```

### Package Add

```text
Code: PKG-999
Name: Demo Bohol Package
Destination: Bohol
Duration: 2D1N
Base price: 12000
Available slots: 10
Travel type: Cultural
Supplier reliability: 88
Business value: 84
Risk: 20
Status: Normal
```

Expected result:

```text
Package is saved and appears in the package catalog.
```

### Invalid Package Score

```text
Supplier reliability: 120
```

Expected result:

```text
Validation rejects the value because scores must be 0 to 100.
```

### Expense Add

```text
Code: EXP-999
Date: 2026-06-22
Category: Tour operations
Amount: 5000
Related package: Bohol Countryside Tour
Payment status: Pending
Notes: Demo expense for IAS test case.
```

Expected result:

```text
Expense is saved and appears in the expenses table.
```

### Package Decision Guide

```text
Budget: 12000
Destination: Bohol
Duration: 2D1N
Travel Type: Cultural
```

Expected result:

```text
The page compares package options and shows the best-ranked package using TOPSIS.
```

## Tracker Row To Fix

The only outdated row in the earlier submitted tracker is `ID-026`.

Replace `ID-026 Trajectory Insights` with:

```text
Module: Legacy Route Compatibility
Test Case Scenario: Old forecasting link redirects to the Package Decision Guide.
Action:
1. Open /forecasting with a query string.
2. Confirm redirect keeps the query.
Actual Input: /forecasting?duration=3D2N
Pass: PASSED
Product Quality Component: Compatibility
Comments/Suggestion: Legacy links still land on the current owner-facing page.
```

Test URL:

```text
http://127.0.0.1:8000/forecasting?duration=3D2N
```

Expected result:

```text
Redirects to /decision-guide?duration=3D2N
```

## ZAP Scan Notes

Scan only:

```text
http://127.0.0.1:8000/login
```

Do not scan:

```text
http://localhost:5173
http://[::1]:5173
```

Those are Vite development addresses and can create noisy CSP findings.

## Expected ZAP Explanation

Fixable alerts should be handled:

```text
Cookie No HttpOnly Flag: fixed for XSRF-TOKEN and session cookies.
X-Powered-By response leak: fixed for Laravel and local static assets.
```

Informational alerts may remain:

```text
Modern Web Application
Session Management Response Identified
```

These are informational observations and can be marked as accepted or noted in the tracker if they appear.

## Presentation Tips

- Use the Owner account for most screens.
- Use the Staff account only when proving role restrictions.
- Do not improvise random data during the demo.
- Keep ZAP pointed at `http://127.0.0.1:8000/login`.
- If the page looks blank, stop the server, run `npm run build`, then restart `php artisan serve`.
