# ProphetOps Complete Setup Guide

This file is the single source of truth for setting up ProphetOps on another laptop or for an AI assistant preparing the project. Follow the commands in order and do not switch to XAMPP/MySQL unless the team intentionally changes the database plan.

## Project Summary

ProphetOps is a Laravel + Inertia + Vue capstone prototype for an internal travel-operations Decision Support System. The current research-facing decision method is TOPSIS, shown in the UI as the owner-friendly **Package Decision Guide**.

The project is meant to run locally for the IAS demo and ZAP testing.

## Correct Branch

Use this branch:

```text
codex/simplified-pages
```

Fresh clone:

```powershell
git clone https://github.com/TiredDom/ProphetOps.git
cd ProphetOps
git checkout codex/simplified-pages
```

Existing clone:

```powershell
cd C:\Users\User\Desktop\ProphetOps
git checkout codex/simplified-pages
git pull
```

## Required Tools

Install these before running the app:

```text
PHP 8.2 or newer
Composer
Node.js and npm
Git
```

XAMPP is optional. The current project uses SQLite, so MySQL/MariaDB is not needed for the normal demo.

## One-Pass Setup

Run these from the project root in PowerShell:

```powershell
composer install
npm install

if (!(Test-Path .env)) { Copy-Item .env.example .env }
php artisan key:generate

New-Item -ItemType File database\database.sqlite -Force
php artisan migrate:fresh --seed

npm run build
php artisan serve --host=127.0.0.1 --port=8000
```

Then open:

```text
http://127.0.0.1:8000/login
```

## Environment Values

The `.env` file should use SQLite:

```env
APP_NAME=ProphetOps
APP_ENV=local
APP_DEBUG=true
APP_URL=http://127.0.0.1:8000

DB_CONNECTION=sqlite

SESSION_DRIVER=file
CACHE_STORE=file
QUEUE_CONNECTION=sync
```

If `.env` was edited, clear cached config:

```powershell
php artisan config:clear
```

## Database

The local database file is:

```text
database/database.sqlite
```

This file is ignored by Git. Recreate it on a new laptop with:

```powershell
New-Item -ItemType File database\database.sqlite -Force
php artisan migrate:fresh --seed
```

Do not open XAMPP just for this setup. Use XAMPP only if the project is intentionally migrated to MySQL later.

## Demo Accounts

Use Owner for the main demo:

```text
owner@prophetops.local / owner123
```

Other seeded accounts:

```text
admin@prophetops.local / admin123
staff@prophetops.local / staff123
```

Invalid login test:

```text
wrong@example.com / wrongpass
```

## Main Routes

```text
/login
/dashboard
/bookings
/inventory
/expenses
/analytics
/decision-guide
/reports
/users
```

Legacy route:

```text
/forecasting
```

Expected behavior:

```text
/forecasting?duration=3D2N redirects to /decision-guide?duration=3D2N
```

## Demo Mode

For the IAS presentation, use built assets:

```powershell
npm run build
php artisan serve --host=127.0.0.1 --port=8000
```

Do not run `npm run dev` during the ZAP scan unless you are actively developing frontend code. Built mode is cleaner and avoids Vite-related scan noise.

## Development Mode

Use two terminals only when editing frontend code live.

Terminal 1:

```powershell
npm run dev
```

Terminal 2:

```powershell
php artisan serve --host=127.0.0.1 --port=8000
```

If you later return to demo/ZAP mode, stop Vite and remove the hot file:

```powershell
Remove-Item public\hot -ErrorAction SilentlyContinue
npm run build
php artisan serve --host=127.0.0.1 --port=8000
```

## Verification

Run these before demo, before pushing, or after pulling on a teammate laptop:

```powershell
php artisan test
npm run build
```

Expected current result:

```text
24 tests passing
Vite build succeeds
```

## IAS Demo Guide

Use this file for the exact presentation flow and test inputs:

```text
DEMO.md
```

It includes booking, package, expense, Package Decision Guide, and ZAP test values.

## ZAP Scan

Scan only:

```text
http://127.0.0.1:8000/login
```

Do not scan:

```text
http://localhost:5173
http://[::1]:5173
```

Those are Vite development server addresses and can create noisy Content Security Policy findings.

Expected fixed items:

```text
Cookie No HttpOnly Flag: fixed for XSRF-TOKEN and session cookies.
X-Powered-By response leak: fixed for Laravel responses and local static assets.
```

Informational alerts may remain:

```text
Modern Web Application
Session Management Response Identified
```

Those are informational observations, not necessarily vulnerabilities.

## Common Problems

### Blank Page

Use built assets and remove any stale Vite hot file:

```powershell
Remove-Item public\hot -ErrorAction SilentlyContinue
npm run build
php artisan serve --host=127.0.0.1 --port=8000
```

### Login Does Not Work

Reset seeded users and data:

```powershell
php artisan migrate:fresh --seed
php artisan config:clear
```

Then use:

```text
owner@prophetops.local / owner123
```

### Database Error

Make sure SQLite exists:

```powershell
New-Item -ItemType File database\database.sqlite -Force
php artisan migrate:fresh --seed
```

### Port 8000 Already In Use

Run on another port:

```powershell
php artisan serve --host=127.0.0.1 --port=8001
```

Then open:

```text
http://127.0.0.1:8001/login
```

### ZAP Still Shows Old Alerts

Start a new ZAP session or delete old alerts, then rescan:

```text
http://127.0.0.1:8000/login
```

Restart Laravel after security/header changes:

```powershell
php artisan serve --host=127.0.0.1 --port=8000
```

## AI Assistant Notes

If an AI assistant is setting this up, do not ask whether to use MySQL, XAMPP, Docker, Vite dev mode, or a cloud database. Use this local SQLite flow unless the user explicitly changes the deployment target.

Default action sequence:

```powershell
git checkout codex/simplified-pages
git pull
composer install
npm install
if (!(Test-Path .env)) { Copy-Item .env.example .env }
php artisan key:generate
New-Item -ItemType File database\database.sqlite -Force
php artisan migrate:fresh --seed
npm run build
php artisan test
php artisan serve --host=127.0.0.1 --port=8000
```

Open:

```text
http://127.0.0.1:8000/login
```

Project rule: keep ProphetOps as an internal business Decision Support System. Do not turn it into a public booking website, payment gateway, customer portal, or marketing site.
