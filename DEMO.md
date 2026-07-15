# ProphetOps IAS Demo Guide

Use this guide for the Information Assurance and Security demo and the test case tracker walkthrough.

## Demo URL

Build the SPA once, then run the single-process app and open it:

```powershell
cd dotnet\client ; npm run build ; cd ..
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

```text
http://localhost:5099/login
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
5. Open Bookings and show the records table.
6. Add or edit one booking.
7. Open Package Catalog.
8. Add one package and show field validation.
9. Open Expenses.
10. Add or edit one expense.
11. Open Analytics.
12. Open Forecast and review the Demand Forecast outlook, chart, and accuracy.
13. Open Reports.
14. Open Users.
15. Log out.
16. Log in as Staff and show restricted access (only Bookings and Package Catalog).
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
Status: Normal
```

Expected result:

```text
Package is saved and appears in the package catalog.
```

### Invalid Package

```text
Name: (leave blank)
Destination: (leave blank)
```

Expected result:

```text
Validation rejects the package because the name and destination are required.
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

### Demand Forecast

```text
Open /forecast as Owner.
Read the demand outlook summary.
Review the forecast chart.
Check the accuracy figure.
```

Expected result:

```text
The page shows the Holt-Winters demand forecast: an outlook summary, a forecast chart over the sample monthly-revenue history, and an accuracy figure (about 95% in-sample fit, MAPE about 4.8%).
```

## Role Restriction Check

```text
Log in as Staff (staff@prophetops.local / staff123).
```

Expected result:

```text
The sidebar shows only Bookings and Package Catalog. Directly requesting a restricted API (for example /api/expenses) returns HTTP 403 Forbidden.
```

## ZAP Scan Notes

Scan only:

```text
http://localhost:5099/login
```

Do not scan the Vite development addresses (only present when running `npm run dev`):

```text
http://localhost:5173
http://[::1]:5173
```

## Expected ZAP Explanation

The .NET build already applies a hardened security posture, so these are handled at the source:

```text
Antiforgery / CSRF: unsafe API requests require the XSRF-TOKEN cookie echoed in the X-XSRF-TOKEN header.
Security headers: Content-Security-Policy, X-Content-Type-Options, X-Frame-Options: DENY, Referrer-Policy: no-referrer.
Server banner: the Kestrel "Server" response header is disabled, so the stack is not advertised.
Auth cookie: HttpOnly and SameSite=Strict.
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
- Keep ZAP pointed at `http://localhost:5099/login`.
- Run the built single-process app (`dotnet run` after `npm run build`) rather than the two-terminal dev setup, so there is no Vite dev-server scan noise.
