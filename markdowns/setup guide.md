# ProphetOps Setup Guide

## Purpose

This guide explains how to set up and run ProphetOps locally using XAMPP, Laravel, Vue, and Inertia.js.

Use this guide when opening the project on a new computer, after cloning the repository, or when checking if the scaffold is working.

## Required Tools

Install or prepare these tools first:

- XAMPP
- PHP 8.2 or higher
- Composer
- Node.js
- npm
- Git

XAMPP is used mainly for the local MariaDB/MySQL database.

## Project Folder

The current local project folder is:

```text
C:\Users\User\Desktop\ProphetOps
```

Run project commands from this folder.

## XAMPP Setup

1. Open XAMPP Control Panel.
2. Start Apache if you want phpMyAdmin access.
3. Start MySQL.
4. Open phpMyAdmin if needed:

```text
http://localhost/phpmyadmin
```

5. Create a database named:

```text
prophetops
```

If the database already exists, you do not need to create it again.

## Environment File

The project should have a `.env` file.

If `.env` is missing, copy `.env.example` and rename the copy to `.env`.

Recommended local database settings:

```env
APP_NAME=ProphetOps
APP_ENV=local
APP_DEBUG=true
APP_URL=http://127.0.0.1:8000

DB_CONNECTION=mysql
DB_HOST=127.0.0.1
DB_PORT=3306
DB_DATABASE=prophetops
DB_USERNAME=root
DB_PASSWORD=
```

The empty `DB_PASSWORD` is normal for many default XAMPP installations.

## First-Time Install

Run these commands from the project folder.

Install PHP dependencies:

```bash
composer install
```

Install frontend dependencies:

```bash
npm install
```

Generate the Laravel application key:

```bash
php artisan key:generate
```

Run database migrations:

```bash
php artisan migrate
```

If migration fails, make sure XAMPP MySQL is running and the `prophetops` database exists.

## Running The Website

ProphetOps needs two local development servers while developing.

### Terminal 1: Start Vue / Vite

```bash
npm run dev
```

Keep this terminal open.

### Terminal 2: Start Laravel

```bash
php artisan serve
```

Keep this terminal open too.

## Website URL

Open this in your browser:

```text
http://127.0.0.1:8000
```

If the scaffold is working, you should see the ProphetOps welcome screen with:

```text
System Ready
```

## Quick Scaffold Check

Use this checklist to confirm the scaffold is successful:

- XAMPP MySQL is running.
- `.env` exists.
- `composer install` completed.
- `npm install` completed.
- `php artisan migrate` completed.
- `npm run dev` is running.
- `php artisan serve` is running.
- Browser opens `http://127.0.0.1:8000`.
- The ProphetOps welcome screen appears.

## Common Issues

### Database Connection Error

Cause:
XAMPP MySQL may not be running, or the database name may not match `.env`.

Fix:

1. Start MySQL in XAMPP.
2. Open phpMyAdmin.
3. Confirm the `prophetops` database exists.
4. Confirm `.env` uses `DB_DATABASE=prophetops`.

### Application Key Missing

Cause:
Laravel does not have an app key yet.

Fix:

```bash
php artisan key:generate
```

### Vite Assets Not Loading

Cause:
The frontend dev server is not running.

Fix:

```bash
npm run dev
```

Then refresh the browser.

### Port 8000 Already In Use

Cause:
Another Laravel server or app is using port 8000.

Fix:

```bash
php artisan serve --port=8001
```

Then open:

```text
http://127.0.0.1:8001
```

### Port 5173 Already In Use

Cause:
Another Vite server is already running.

Fix:
Stop the old Vite terminal, or let Vite choose another available port if prompted.

## Daily Development Startup

For normal daily work:

1. Open XAMPP.
2. Start MySQL.
3. Open the ProphetOps folder in your code editor.
4. Open Terminal 1 and run:

```bash
npm run dev
```

5. Open Terminal 2 and run:

```bash
php artisan serve
```

6. Visit:

```text
http://127.0.0.1:8000
```

## Daily Shutdown

When done working:

1. Stop the Laravel terminal with `Ctrl + C`.
2. Stop the Vite terminal with `Ctrl + C`.
3. Stop MySQL in XAMPP if no other project needs it.

## Notes For AI Assistants

Before changing code, read:

```text
markdowns/general knowledge.md
information/module-map.md
information/feature-log.md
information/decisions.md
```

Keep ProphetOps internal-facing. Do not turn it into a public booking website, payment gateway, or customer portal.
