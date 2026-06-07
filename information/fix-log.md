# ProphetOps Fix Log

Current direction note:
This log is historical. Older fixes may mention legacy pages such as Operational Records or Data Validation. The active Sprint 1 direction is now `information/sprint-1-direction.md`.

## 2026-06-03 - Restore Navigation Helper Export

Type:
Frontend Fix

Module:
Dashboard / Operational Records / Navigation

Problem:
The Dashboard and Operational Records pages rendered blank because the shared navigation helper was imported as a named export, but the helper file did not export `createNavigationGroups`.

Root Cause:
`resources/js/data/navigation.js` defined `createNavigationGroups` without the `export` keyword.

Fix Summary:
Restored the named export so both Vue pages can import the shared sidebar navigation data.

Files Changed:
- resources/js/data/navigation.js
- information/fix-log.md

Behavior After Fix:
The Dashboard and Operational Records pages can load the shared navigation helper again.

Verification:
Ran a Vue single-file-component parse check for the affected pages and confirmed `resources/js/data/navigation.js` contains `export function createNavigationGroups`. Confirmed `/data/operational-records` returns HTTP 200.

Known Risks:
The browser may need a hard refresh if Vite cached the previous module.

Next AI / Developer Should Know:
If a blank page appears after shared frontend helper edits, check the browser console for failed ES module imports first.

## 2026-06-03 - Fix Search Icon Alignment

Type:
UI Fix

Module:
Operational Records

Problem:
The search icon inside the Operational Records search field was not showing cleanly because the placeholder text overlapped the icon area.

Root Cause:
The shared input padding rule overrode the search-specific left padding, and the icon did not have an explicit stacking layer above the input surface.

Fix Summary:
Moved the search input left-padding rule after the shared input padding rule and added a small z-index plus pointer-event protection to the search icon.

Files Changed:
- resources/css/app.css
- information/fix-log.md

Behavior After Fix:
The search icon should appear centered at the left of the search field, with placeholder text starting after the icon.

Verification:
Confirmed the CSS contains the corrected search icon and padding rules. Confirmed `/data/operational-records` returns HTTP 200.

Known Risks:
The browser may need a normal refresh if Vite has not hot-reloaded the CSS yet.

Next AI / Developer Should Know:
Keep icon padding rules after shared form-control padding rules when an icon sits inside an input.

## 2026-06-03 - Fix Mock Login Redirect Hang

Type:
Frontend Fix

Module:
Authentication / Account Access

Problem:
Clicking Sign In could stay on the loading state instead of opening the dashboard.

Root Cause:
The temporary frontend-only login used Inertia `router.visit` immediately after storing mock auth state. In the local scaffold this transition could hang during the mock-auth redirect.

Fix Summary:
Changed successful mock login navigation to use direct browser navigation with `window.location.assign('/dashboard')`. Already-authenticated login-page visits now use `window.location.replace('/dashboard')`. The mock auth service also handles storage write fallback more defensively.

Files Changed:
- resources/js/Pages/Login.vue
- resources/js/services/mockAuth.js
- information/fix-log.md

Behavior After Fix:
After valid frontend mock login, the browser should leave the login page and open `/dashboard`.

Verification:
Ran Vue single-file-component parse checks for Login, Dashboard, Operational Records, and AppIcon. Confirmed Login uses direct browser navigation for the dashboard redirect.

Known Risks:
The browser may need a hard refresh if Vite cached the previous Login module.

Next AI / Developer Should Know:
For this frontend-only mock auth phase, direct browser navigation is acceptable because the mock session is stored before redirect. Replace this with real backend auth flow later.

## 2026-06-03 - Remove Async Delay From Mock Login

Type:
Frontend Fix

Module:
Authentication / Account Access

Problem:
The Sign In button could still remain stuck at "Checking access..." in the browser.

Root Cause:
The mock login service still had an async timer-based delay, so the temporary frontend flow depended on an unnecessary promise before redirecting.

Fix Summary:
Made `mockLogin` synchronous, moved the short loading feedback into the Login component, and changed temporary route guards to direct browser redirects.

Files Changed:
- resources/js/Pages/Login.vue
- resources/js/services/mockAuth.js
- resources/js/Pages/Welcome.vue
- resources/js/Pages/OperationalRecords.vue
- information/auth-frontend-backend-guide.md
- information/fix-log.md

Behavior After Fix:
Submitting a valid email and password stores mock auth immediately, briefly shows the loading state, then forces the browser to `/dashboard`.

Verification:
Ran Vue single-file-component parse checks for Login, Dashboard, Operational Records, and AppIcon. Confirmed no `await mockLogin` or async mock-login delay remains.

Known Risks:
The browser may need a hard refresh to pick up the new Vite module.

Next AI / Developer Should Know:
Do not add promise-based mock delays to the temporary auth service. Keep the service simple until backend auth replaces it.

## 2026-06-03 - Use File Session For Frontend-Only Pages

Type:
Configuration Fix

Module:
Local Development / Account Access

Problem:
Opening localhost stopped loading because Laravel tried to read sessions from the database while MySQL/XAMPP was not accepting connections.

Root Cause:
`.env` used `SESSION_DRIVER=database`, so even frontend-only pages needed a database connection before rendering.

Fix Summary:
Changed local session, cache, and queue defaults to file/sync-friendly settings for the frontend phase.

Files Changed:
- .env
- .env.example
- information/fix-log.md

Behavior After Fix:
`/login` and `/up` respond without requiring MySQL for basic frontend page rendering.

Verification:
Ran `php artisan config:clear`. Confirmed `http://127.0.0.1:8000/login` returns HTTP 200 and `http://127.0.0.1:8000/up` returns HTTP 200.

Known Risks:
If the browser still appears stuck, refresh the page once. If an old server process is frozen, stop and restart `php artisan serve`.

Next AI / Developer Should Know:
Keep file-based sessions during frontend-only work. When real backend authentication/database work begins, revisit the session/cache/queue settings deliberately.

## 2026-06-06 - Center Stat Card Icons

Type:
UI Fix

Module:
Dashboard / Shared Stat Cards

Problem:
Stat card icons could appear stuck near the top-left of their colored chip instead of centered.

Root Cause:
The broad `.stat-card span` helper-text selector also matched the `.stat-icon` wrapper, overriding its grid centering.

Fix Summary:
Added a dedicated `stat-note` class for the helper text and changed the CSS selector so icon wrappers keep their centered layout.

Files Changed:
- resources/js/Components/dashboard/StatCard.vue
- resources/css/app.css
- information/fix-log.md

Behavior After Fix:
Stat card icons remain visually centered inside their colored backgrounds.

Verification:
Ran Vue component parse checks for StatCard, Dashboard, and Inventory. Production build passed.

Known Risks:
The browser may need a hard refresh if it still shows the old CSS from Vite/browser cache.

Next AI / Developer Should Know:
Avoid broad selectors like `.component span` inside reusable cards because they can accidentally override icon wrappers, badges, and other nested spans.
