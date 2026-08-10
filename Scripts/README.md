# SQPortal database scripts

The application never creates or alters tables. These two scripts are the only
way schema reaches a database, and they are the source of truth for it.

| Script        | When to run                                                        |
|---------------|--------------------------------------------------------------------|
| `init.sql`    | Once, against a **new empty database**. Creates the full current schema. |
| `upgrade.sql` | Once, against an **existing database** created from any earlier version of these scripts. Brings it to the current schema. Every step is guarded, so re-running is safe. Back up first. |

After either script, start the app: `SeedLookups()` fills empty lookup tables
with defaults, and `BackfillCaseNumbers()` numbers any legacy cases.

## Version tracking

Both scripts record the schema version in `dbo.SchemaVersions`. At startup the
app checks that table and logs a critical message naming the script to run if
the database is behind — a missed script surfaces as one clear log line, not
scattered runtime errors.

Current schema version: **006**.

## Keeping schema and code in sync

Any table or index change must be made in three places together:

- `init.sql` (fresh installs)
- `upgrade.sql` (existing databases, as a new guarded step) — and bump the
  version recorded there, in `init.sql`, and in `Program.cs`
  (`requiredSchemaVersion`)
- the matching entity class in `Models/Entities/` and, for indexes and
  filters, `Data/SQPortalDbContext.cs` (`OnModelCreating`)

## History

The incremental scripts `002`–`006` and the one-off renumbering fix were
consolidated into `upgrade.sql`; the originals remain available in git
history.
