# ProphetOps Decisions

## 2026-06-01 - Use Laravel + Inertia.js + Vue 3 Stack

Decision:
Use Inertia.js as the bridge between Laravel and Vue instead of a separate API + SPA approach.

Reason:
Inertia eliminates the need for a separate REST API layer. Vue components serve as pages directly, receiving data from Laravel controllers. This is simpler for an internal admin dashboard that does not need a public API.

Alternatives Considered:
- Laravel API + standalone Vue SPA (more boilerplate, needs API auth, CORS, separate routing)
- Laravel Blade only (limited interactivity, harder to build rich dashboard components)

Impact:
All pages are Vue components in resources/js/Pages/. Controllers return Inertia::render() instead of JSON or Blade views. No need for axios API calls for page navigation.

## 2026-06-01 - Use Vanilla CSS Instead of Tailwind

Decision:
Use vanilla CSS with custom design tokens instead of Tailwind CSS.

Reason:
The Sprint 1 design plan specifies a custom color palette, typography, and component styles. Vanilla CSS gives full control and matches the Premium Clarity Dashboard design direction without framework overhead.

Alternatives Considered:
- Tailwind CSS (faster utility-based styling but adds framework dependency and diverges from design plan's explicit token system)

Impact:
All styles use CSS custom properties defined in resources/css/app.css. Components use class-based styling.

## 2026-06-01 - Use XAMPP MariaDB

Decision:
Use XAMPP's MariaDB on port 3306 with root user and no password for local development.

Reason:
Simpler setup than MySQL Workbench for local development. XAMPP is already installed and includes phpMyAdmin for visual database management.

Alternatives Considered:
- MySQL 8.0 via MySQL Workbench (requires password configuration, more setup)

Impact:
.env uses DB_CONNECTION=mysql, DB_HOST=127.0.0.1, DB_PORT=3306, DB_USERNAME=root, DB_PASSWORD=(empty).
