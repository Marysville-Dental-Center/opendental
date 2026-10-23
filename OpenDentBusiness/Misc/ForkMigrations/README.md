# Fork migrations

The legacy `ConvertDatabases` chain (`OpenDentBusiness/Misc/ConvertDatabases*.cs`) is frozen at 24.3.63. Every schema or data change after that is a fork migration step in this folder, run by `ForkMigrationRunner`.

## Adding a step

1. Add a new file with a class derived from `ForkMigrationStep`, marked `[ForkMigration(level,"name")]`. Do not edit another package's step file, and do not register the step anywhere: the runner finds it by reflection.
2. The level is `yyyyMMddNN`: today's date plus a two-digit sequence number, for example `2026110301`. Levels must be unique (a test checks this).
3. Make the step idempotent with the `ForkMigrationContext` helpers (`AddColumnIfMissing`, `EnsureColumnDefinition`, `EnsureIndex`, `CreateTableIfMissing`, `RenameColumnIfNeeded`, ...). They compare against `SHOW CREATE TABLE` and change nothing when the database already matches, so the same step upgrades a 24.3 database and leaves an Open Dental database that already has the change alone.
4. Run a data migration or back-fill only when the step itself just created the column it belongs to (the helpers return `true` when they changed something). Never re-run a conversion Open Dental already did.
5. Write column definitions exactly as `SHOW CREATE TABLE` prints them on MariaDB 10.11 (for example `tinyint(4) NOT NULL`, `date NOT NULL DEFAULT '0001-01-01'`), so the schema verifier sees an exact match.
6. Regenerate the CRUD with xCrudGenerator (`CrudGenerator.exe crud=TypeName,...` from `xCrudGenerator\bin\Debug`), never by hand.

## Steps and order

| Level | Step | Package |
|---|---|---|
| 2026101901 to 2026101906 | `OdParity25_3.cs`: the Open Dental 24.4, 25.1, 25.2 and 25.3 schema, then the 25.3.48 keys and definitions of existing columns | M1-01 |
| 2026101907 | `Seed25_3Step.cs`: the `Seed25_3` preference, permission-grant and eForm status rows, only for a database that comes from 24.3 (judged by the `ForkAdoptedFrom` source on an adopted database, otherwise by `DataBaseVersion`) | M1-02 |

## Hand-off to the version guard and adoption

`ForkMigrationRunnerAdapter` implements M1-03's `IForkMigrationRunner`, so `ForkMigrationHandOff` finds it. Startup (`PrefL.ConvertDB`) and adoption (`Adoption.Run`) run the steps through it; afterwards it sets `OdSchemaCompat` to `25.3.48` when the schema verifier finds an exact match. `tools/New-DevDatabase.ps1` adopts the database it builds, so a development database carries the same markers as an adopted office copy. Running `ForkMigrationRunner.Run()` directly (tests, the UnitTests database) leaves a database the startup guard refuses, because it has `ForkSchemaLevel` without the adoption markers.

## Runner state

Kept in preference rows (no schema change): `ForkMigrationsApplied` (levels that finished), `ForkSchemaLevel` (highest finished level) and `ForkMigrationInProgress` (a step that started and did not finish; while it is set the runner refuses to run, because MyISAM has no transactions and the database needs a restore). Callers own backups, version checks and the progress window.

## Schema verifier

`SchemaVerifier` compares a database with `Reference/OdSchema-25.3.48.sql`, the Open Dental 25.3.48 schema built from a schema-only dump by `SchemaVerifier.BuildReferenceDdlFromDump`. Tables, columns (type, NULL, default, extra, charset, position), keys, engine and charset must match, except for the documented exceptions in `SchemaVerifier.DocumentedExceptions25_3_48`:

- `patient._portal_active_email` and its unique key `portal_active_email_uniq`: an office-specific customization, not Open Dental schema. The adoption step drops both.
- The 54 Open Dental HQ-only tables that the 24.3 code declares, when present.
- Legacy tables that long-lived databases (including our development seed) still carry: the `anesth*` tables, `files` and `instructor`.

Tests: `tests/ForkMigrationTests`.
