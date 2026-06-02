# ProphetOps Database Map

## users (Laravel default)

Purpose:
Store authorized system users with roles.

Important Fields:
id, name, email, password, created_at, updated_at

Relationships:
Will be extended with role field in Sprint 1 feature work.

Used By:
Authentication, access control.

Forecasting Relevance:
None directly. Used for audit trails (encoded_by, recorded_by).

## sessions (Laravel default)

Purpose:
Track active user sessions for database-backed session driver.

## cache / cache_locks (Laravel default)

Purpose:
Database-backed cache storage.

## jobs / job_batches / failed_jobs (Laravel default)

Purpose:
Queue system tables for background job processing.

## password_reset_tokens (Laravel default)

Purpose:
Password reset functionality.

---

Sprint 1 feature tables (operational_records, travel_packages, expenses, inventory_items, inventory_movements) will be added during the next development phase.
