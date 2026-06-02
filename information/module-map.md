# ProphetOps Module Map

## Project Scaffolding

Purpose:
Foundation setup for the Laravel + Vue + Inertia.js admin dashboard.

Main Paths:
- Backend: app/, config/, routes/, database/
- Frontend: resources/js/, resources/css/
- Entry Points: resources/js/app.js, resources/views/app.blade.php
- Config: vite.config.js, .env, composer.json, package.json

Current Status:
Scaffolded. Laravel 12.61.0, Vue 3, Inertia.js v3.1.0 installed and verified.

Important Notes:
- Uses Inertia.js for single-codebase Laravel+Vue (no separate API layer needed)
- Vanilla CSS with custom design tokens (no Tailwind)
- Inter font from Google Fonts
- Vue pages go in resources/js/Pages/
- Vue components go in resources/js/Components/
