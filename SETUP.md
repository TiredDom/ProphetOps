# ProphetOps Setup Guide

This guide is for running the current ProphetOps capstone prototype locally.
The project uses Laravel, Inertia, Vue, Vite, and a local SQLite database.

## Requirements

- PHP 8.2 or newer
- Composer
- Node.js and npm
- Git

XAMPP is optional. You do not need to open XAMPP for the current local setup because the database is SQLite.

## Install

Run these commands from the project folder:

```bash
composer install
npm install
```

Create the environment file if it is missing:

```bash
copy .env.example .env
php artisan key:generate
```

Make sure the SQLite database file exists:

```bash
New-Item -ItemType File database\database.sqlite -Force
```

Use these important `.env` values for local testing:

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

Prepare the database with demo data:

```bash
php artisan migrate:fresh --seed
```

## Run The Website

For normal development, open two terminals:

```bash
npm run dev
```

```bash
php artisan serve
```

Then open:

```text
http://127.0.0.1:8000/login
```

## Demo Accounts

```text
owner@prophetops.local / owner123
admin@prophetops.local / admin123
staff@prophetops.local / staff123
```

## Main Pages To Test

- `/login`
- `/dashboard`
- `/bookings`
- `/inventory`
- `/expenses`
- `/analytics`
- `/decision-guide`
- `/reports`
- `/users`

The old `/forecasting` route redirects to `/decision-guide` for compatibility.

## Capstone / ZAP Testing Mode

For ZAP or a cleaner local scan, use built frontend assets instead of the Vite dev server:

```bash
npm run build
php artisan serve
```

Then scan:

```text
http://127.0.0.1:8000/login
```

If the page is blank or the browser console mentions Vite, stop the dev server and remove the stale hot file:

```bash
Remove-Item public\hot -ErrorAction SilentlyContinue
```

Then run `npm run build` and restart `php artisan serve`.

## Verification

Run these before final submission or before pushing changes:

```bash
php artisan test
npm run build
```

The test case tracker is stored in:

```text
outputs/tracker-ready/IAS Test Cases Progress Tracker.xlsx
```

## Database Notes

The local database file is:

```text
database/database.sqlite
```

That file is ignored by Git. A new machine should recreate it with:

```bash
New-Item -ItemType File database\database.sqlite -Force
php artisan migrate:fresh --seed
```

## Troubleshooting

If login does not work, run:

```bash
php artisan migrate:fresh --seed
php artisan config:clear
```

If assets do not load, run:

```bash
npm run build
```

If port `8000` is busy, run Laravel on another port:

```bash
php artisan serve --port=8001
```

Then visit:

```text
http://127.0.0.1:8001/login
```

## Project Direction

ProphetOps is an internal business decision support system for travel operations. Keep the interface owner-friendly and use the name "Package Decision Guide" in the UI instead of exposing algorithm-heavy labels like "TOPSIS Ranking" as page names.
