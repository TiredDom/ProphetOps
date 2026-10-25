# ProphetOps IAS Demo Guide

Use this guide for the Information Assurance and Security demo and the test case tracker walkthrough.

## Demo URL

Build the SPA once, then run the single-process app and open it:

```powershell
cd dotnet\client
npm run build
cd ..
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Demo__Enabled = "true"
$env:Storage__Root = (New-Item -ItemType Directory -Force .local-state-demo).FullName
$env:Business__TimeZone = "Asia/Manila"
dotnet run --project ProphetOps.Api --urls http://localhost:5099
```

```text
http://localhost:5099/login
```

## Demo Accounts

Demo records are created only on an empty database with the explicit setting above.
An existing database is not topped up or given a fabricated audit history. Remove
`Demo__Enabled` from the terminal environment after the demonstration; never use demo
mode with agency records or a Production environment.

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
4. Open Home and review the forecast: the headline, the chart with its 80% range, and the suggestions.
5. Open Monthly figures and model details: In plain words, the monthly breakdown, and accuracy on months it had not seen.
6. Open Bookings and show the records table.
7. Add or edit one booking.
8. Open Packages: the demand outlook, May run short, and one package's expected demand.
9. Add one package and show field validation.
10. Open Expenses and show the cost scenario below the list.
11. Add or edit one expense.
12. Open Reports.
13. Open Team & access.
14. Log out.
15. Log in as Staff and show restricted access (only Bookings and Packages).
16. Run or show ZAP results.

## Test Case Inputs

### Booking Add

```text
Client or agency partner: Assurance Travel Group
Package: PKG-104 / Cebu and Bohol Combo
Booking date: 2026-06-22
Passengers: 4
Price: 48000
Payment status: Pending
Booking status: Reserved
```

Expected result:

```text
Booking is saved and appears in the bookings table.
```

### Package Add

```text
Package ID: PKG-999
Package name: Demo Bohol Package
Destination: Bohol
Duration: 2D1N
Price per passenger: 12000
Open slots: 10
Status: Normal
```

Expected result:

```text
Package is saved and appears in the packages list.
```

### Invalid Package

```text
Package name: (leave blank)
Destination: (leave blank)
```

Expected result:

```text
Validation rejects the package because the name and destination are required.
```

### Expense Add

```text
Date: 2026-06-22
Category: Tour operations
Amount: 5000
Related package or operation: Cebu and Bohol Combo
Payment status: Pending
Notes: Demo expense for IAS test case.
```

Expected result:

```text
Expense is saved with the next expense ID and appears in the expenses table.
```

### Demand Forecast

```text
Open Home as Owner (an old /forecast link opens the same page at the model details).
Read the forecast headline and review the chart.
Open Monthly figures and model details.
Read In plain words and Accuracy on months it had not seen.
```

Expected result:

```text
Home shows the Holt-Winters forecast of booking revenue: the headline, a chart with its 80% range, and suggestions. The model details show the monthly breakdown, the forecast in plain words, the Average miss (its fit on the months it learned from) and the rolling-origin test on months it had not seen. With the demonstration records, repeating the same month last year misses less than Holt-Winters on that test; say so plainly, since these records are demonstration data, not agency evidence.
```

## Role Restriction Check

```text
Log in as Staff (staff@prophetops.local / staff123).
```

Expected result:

```text
The sidebar shows only Bookings and Packages. Directly requesting a restricted API (for example /api/expenses) returns HTTP 403 Forbidden.
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
