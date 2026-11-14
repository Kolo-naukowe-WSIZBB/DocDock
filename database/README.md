# database

Informacje o dokumentach i wyszukiwanie.

## Co tu trafia

- implementacja kontraktu bazy (SQLite)
- schemat bazy i migracje
- testy na prawdziwej bazie

The initial SQLite schema and seed data are in
[`migrations/001_initial_schema.sql`](./migrations/001_initial_schema.sql).
Apply migrations in version order when initializing or upgrading a database.
The migration version is recorded in `schema_migrations`; each migration should
be applied only once.

## Czego tu nie ma

- samych plików dokumentów, tylko informacje o nich. Pliki trzyma `storage/`.

## Może korzystać z

Tylko z `contracts/`.
