# Authentication Frontend To Backend Guide

Sprint 1 authentication is prototype-only. Treat it as a user experience scaffold, not production security.

## 1. Current Frontend-Only Behavior

ProphetOps currently has a frontend Login page and mock auth service.

Current files:

- `resources/js/Pages/Login.vue`
- `resources/js/services/mockAuth.js`
- `routes/web.php`
- `resources/css/app.css`

Current behavior may need to be adjusted to match the new Sprint 1 plan:

- Use only the required demo accounts.
- Store a mock session in localStorage or frontend state.
- Add frontend-only route guarding.
- Clear mock session on logout.
- Adapt navigation by role.

## 2. Required Demo Accounts

Owner / Management:

- Email: `owner@prophetops.local`
- Password: `owner123`
- Access: all pages

Admin:

- Email: `admin@prophetops.local`
- Password: `admin123`
- Access: Dashboard, Bookings, Inventory, Expenses, Analytics, Reports

Staff:

- Email: `staff@prophetops.local`
- Password: `staff123`
- Access: Bookings and Inventory only

## 3. Sprint 1 Role Behavior

Navigation should adapt based on role.

Restricted pages should not break the UI. If a user attempts to access a restricted page, redirect them to the best allowed page or show a clear internal access message.

Recommended redirects:

- Owner / Management: `/dashboard`
- Admin: `/dashboard`
- Staff: `/bookings`

## 4. Login Page Requirements

The Login page should include:

- ProphetOps name/logo
- Subtitle: "Decision Support System for Travel Operations"
- Email/username field
- Password field
- Login button
- Demo account hint
- Small note: "Authorized personnel only"

Do not include:

- Public registration
- Customer booking links
- Marketing landing page sections

## 5. What Must Not Be Built In Sprint 1

Do not build:

- Real authentication
- Password hashing
- Backend sessions
- JWT
- Database-backed users
- Production authorization

Do not store real passwords in localStorage, sessionStorage, project docs, or console logs.

## 6. Mock Session Guidance

The mock session can store:

- user name
- email
- role
- allowed navigation/page list
- mock login timestamp

The mock session must not store:

- plaintext password
- hashed password
- sensitive token

## 7. Frontend Guard Guidance

Prototype route guards can be implemented in frontend page setup or a shared helper.

For Sprint 1:

- Unauthenticated users go to `/login`.
- Authenticated users should not be stuck on `/login`.
- Role-restricted routes should fail gracefully.
- Logout clears mock state and returns to `/login`.

Future backend authentication will replace this behavior.

## 8. Future Backend Endpoints

These are future integration ideas, not Sprint 1 requirements:

- `POST /login`
- `POST /logout`
- `GET /user`

When backend work begins, Laravel session authentication is the simplest fit for the current Inertia setup.

Expected future login request:

```json
{
  "email": "admin@example.com",
  "password": "user-entered-password",
  "remember_me": true
}
```

Expected future user response:

```json
{
  "user": {
    "id": 1,
    "name": "Administrator",
    "email": "admin@example.com",
    "role": "Admin",
    "access": "internal"
  }
}
```

## 9. Replacement Plan For Real Auth

When real auth is in scope, replace:

- frontend demo-account check
- localStorage mock session
- frontend-only route protection
- mock role data

Keep:

- Login page layout
- helpful frontend validation
- loading and error states
- role-aware navigation behavior
- clear internal-only language

## 10. Security Reminders

Frontend validation is not security.

Future backend work must:

- validate email and password
- verify credentials securely
- authorize by role
- block inactive users
- protect internal-only pages
- sanitize and validate all input
- use secure session or token handling

AI handoff:

- Module: Authentication / Account Access
- Current scope: Sprint 1 pseudo login only
- Main files: `resources/js/Pages/Login.vue`, `resources/js/services/mockAuth.js`, `routes/web.php`
- Next implementation need: update mock auth to the required demo accounts and roles
