# Fork migrations

The legacy `ConvertDatabases` chain (`OpenPmsDentalBusiness/Misc/ConvertDatabases*.cs`) is frozen at 24.3.63. Every schema or data change after that is a fork migration step in this folder, run by `ForkMigrationRunner`.

## Adding a step

1. Add a new file with a class derived from `ForkMigrationStep`, marked `[ForkMigration(level,"name")]`. Do not edit another package's step file, and do not register the step anywhere: the runner finds it by reflection.
2. The level is `yyyyMMddNN`: today's date plus a two-digit sequence number, for example `2026110301`. It must be a real date from 2026 on (`ForkMigrationRunner.ValidateStep`), above every existing level, and unique (a test checks this). Only dated levels are valid (owner decision of 2026-11-01): the `yyyy00PPNN` package ranges the single-wave feature port gave each backlog package are retired, and its four package steps were renumbered to `2026110101` to `2026110104` in their old order.
3. Make the step idempotent with the `ForkMigrationContext` helpers (`AddColumnIfMissing`, `EnsureColumnDefinition`, `EnsureIndex`, `CreateTableIfMissing`, `RenameColumnIfNeeded`, ...). They compare against `SHOW CREATE TABLE` and change nothing when the database already matches, so the same step upgrades a 24.3 database and leaves an Open Dental database that already has the change alone.
4. Run a data migration or back-fill only when the step itself just created the column it belongs to (the helpers return `true` when they changed something). Never re-run a conversion Open Dental already did. A back-fill is not an edit of the row: an `UPDATE` must keep the table's ON UPDATE timestamps (`SecDateTEdit`, `DateTStamp`) by also setting them to themselves, as `EnsureColumnDefinition`'s NULL fill and `SecurityHash` do; the adoption rehearsal (`tools/AdoptionValidation`) fails on any audit timestamp that moves.
5. Write column definitions exactly as `SHOW CREATE TABLE` prints them on MariaDB 10.11 (for example `tinyint(4) NOT NULL`, `date NOT NULL DEFAULT '0001-01-01'`), so the schema verifier sees an exact match.
6. Regenerate the CRUD with xCrudGenerator (`CrudGenerator.exe crud=TypeName,...` from `xCrudGenerator\bin\Debug`), never by hand.

## Steps and order

| Level | Step | Package |
|---|---|---|
| 2026101901 to 2026101906 | `OdParity25_3.cs`: the Open Dental 24.4, 25.1, 25.2 and 25.3 schema, then the 25.3.48 keys and definitions of existing columns | M1-01 |
| 2026101907 | `Seed25_3Step.cs`: the `Seed25_3` preference, permission-grant and eForm status rows, only for a database that comes from 24.3 (judged by the `ForkAdoptedFrom` source on an adopted database, otherwise by `DataBaseVersion`) | M1-02 |
| 2026101951 | `XConnectEnableStep.cs`: sets `clearinghouse.EnableXConnect` where 24.3 had XConnect on, only for a database that comes from 24.3 | V-DXC1 |
| 2026101971 | `ReportRows25_3.cs`: the Procedures, Production by Procedure and Insurance Pending Supplementals report rows (added on a database from 24.3; on an Open Dental 25.3 database only a row under another internal name is renamed) | A-RPT |
| 2026102401 to 2026102403 | `OdParity26_1.cs`: the Open Dental 25.4 schema, the 26.1 schema, and `claimpayment.Note` widened to text (the 25.3.48 drift). Columns of one table are added in one `ALTER TABLE` (`OdParity26_1.AlterTable`), so a large MyISAM table such as `claimproc`, `procedurelog` or `procnote` is copied once. The only data migration: existing `evaluation` rows get `GradeOverride` -1 in the statement that adds the column; `NULL` claim payment notes become empty strings before the column turns `NOT NULL`. | M2-01 |
| 2026102501 | `Seed26_1Step.cs`: the 25.4 and 26.1 preference rows, and the 25.4 blockout permission split on a database from before 25.4 | M2-02 |
| 2026110101 | `BShellSteps.cs`: disables the discontinued DXC Patient Credit Score program link and deletes its toolbar buttons, on every database (`DxcCreditScoreRemoved.Plan`, shared with the adoption tools) | B-SHELL |
| 2026110102 | `NoBillInsBackfillStep.cs`: Do Not Bill to Ins decisions moved to `procedurelog.NoBillIns`. Not active: the class is abstract, so the runner skips it; the owner has not decided whether it runs (see `docs/port-notes/M2-EST.md`) | M2-EST |
| 2026110103 | `ReportRowUdsPlus.cs`: the UDS+ report row after the FQHC Dental Sealant Measure, on a database from before 25.4 | M2-UDS |
| 2026110104 | `InsHistCodeGroupsStep.cs`: Insurance History rows from the `InsHist*Codes` preferences to code groups, on a database from before 25.4 | M2-HIST |
| 2026110211 and 2026110212 | `InventorySteps.cs`: the inventory tracking tables `supplyledger` (with the opening balance of every supply in the same step), then `supplylot`, `supplyproccode`, `supplysetting` and `supplyprocpending`, created from `InventoryTables` (a `ForkTableSet`; see "Fork-only tables"); no Open Dental table changes. The preferences, the alert category links and the Inventory Adjust grant are written when the office turns tracking on (`InventoryTracking.SaveSettings`), not by a step. 2026110213 to 2026110219 are kept for this feature. | Inventory tracking (`docs/specs/inventory-tracking.md`) |

The last four were renumbered on 2026-11-01 from the retired package range levels 2027002301, 2027002701, 2027003501 and 2027004701, keeping their order. No office database carried a fork level then.

Startup decides from the set of applied levels, not from the highest one: a step of this build whose level is not in `ForkMigrationsApplied` is pending and runs, even when its level is below the database's `ForkSchemaLevel` (a step merged later with a lower level, or `NoBillInsBackfillStep` once it is turned on). A database whose `ForkMigrationsApplied` or `ForkSchemaLevel` holds a level this build does not have was updated by a newer release, and startup asks for that release (`ForkVersionGuard`). So a step, once released, is never removed or renumbered.

## Hand-off to the version guard and adoption

`ForkMigrationRunnerAdapter` implements M1-03's `IForkMigrationRunner`, so `ForkMigrationHandOff` finds it. Startup (`PrefL.ConvertDB`) and adoption (`Adoption.Run`) run the steps through it; afterwards it sets `OdSchemaCompat` to the verifier's reference version (`26.1.70` since M2-01) when the schema verifier finds an exact match. The adoption allowlist is unchanged (24.3.63 and 25.3.48): an office database is adopted at 25.3.48 and then runs every M1 and M2 step. `tools/New-DevDatabase.ps1` adopts the database it builds, so a development database carries the same markers as an adopted office copy. Running `ForkMigrationRunner.Run()` directly (tests, the UnitTests database) leaves a database the startup guard refuses, because it has `ForkSchemaLevel` without the adoption markers.

## Runner state

Kept in preference rows (no schema change): `ForkMigrationsApplied` (levels that finished), `ForkSchemaLevel` (highest finished level) and `ForkMigrationInProgress` (a step that started and did not finish; while it is set the runner refuses to run, because MyISAM has no transactions and the database needs a restore). The runner runs every step whose level is not in `ForkMigrationsApplied`, and the startup guard (`ForkVersionGuard`) reads the same list, through `ForkDbState.ForkMigrationsApplied` and `IForkMigrationRunner.GetLevels`, to decide whether anything is pending. Callers own backups, version checks and the progress window.

## Schema verifier

`SchemaVerifier` embeds two references:

- `Reference/OdSchema-26.1.70.sql`, the default (`ReferenceVersion`, `GetReference()`, `VerifyDatabase()`): the schema a migrated database has. It is the 25.3.48 reference plus the changes of Open Dental's published database documents (the 25.4 and 26.1 diff documents, checked against the full 26.1 document: 427 tables, every column name, position and type). No Open Dental program or database was used. The documents publish names, order and types only, so every NULL/NOT NULL, default and key of a new or changed column is marked `INFERRED` in a comment above its table. `tools/New-OdReferenceDatabase.ps1` loads it as the empty local database `od_261_schema`.
- `Reference/OdSchema-25.3.48.sql` (`GetReference25_3_48()`, `VerifyDatabase25_3_48()`): the Open Dental 25.3.48 schema built from a schema-only dump by `SchemaVerifier.BuildReferenceDdlFromDump`. The 25.3 steps read key and column definitions from it, and an office database must match it before adoption.

Tables, columns (type, NULL, default, extra, charset, position), keys, engine and charset must match, except for the documented exceptions (`SchemaVerifier.DocumentedExceptions`, the same for both references):

- `patient._portal_active_email` and its unique key `portal_active_email_uniq`: an office-specific customization, not Open Dental schema. The adoption step drops both.
- The 54 Open Dental HQ-only tables that the 24.3 code declares, when present.
- Legacy tables that long-lived databases (including our development seed) still carry: the `anesth*` tables, `files` and `instructor`.
- This program's own tables, declared in `ForkTableSet` classes (see "Fork-only tables" below).

Tests: `tests/ForkMigrationTests` (runner, 25.3 steps, both references) and `tests/M201Tests` (25.4 and 26.1 steps, the 26.1.70 reference, the regenerated CRUD; set `M201_OD_DOCS_DIR` to a folder with the published XML documents to also check the reference against them).

## Fork-only tables

A table that only this program has (a feature of the fork, not Open Dental schema) is declared once, with its exact DDL as `SHOW CREATE TABLE` prints it on MariaDB 10.11, in a class derived from `ForkTableSet` in the feature's own step file (for example `MaintenanceTables` in `MaintenanceSteps.cs`). `ForkTables` finds every set by reflection, as the runner finds steps, and refuses a table declared twice or a name the 26.1.70 reference has. The feature's steps create the tables from those statements with `CreateTableIfMissing`.

- `SchemaVerifier.DocumentedExceptions` allows every fork-only table as an `ExtraTable`, so a migrated database still matches Open Dental 26.1.70 and gets `OdSchemaCompat`. `ForkTables.VerifyDatabase` compares the fork-only tables with their declarations exactly.
- The adoption tools: `tools/SyntheticData/allowed-differences.adoption.json` has a `TableAdded` rule per fork-only table (a test checks the rules equal `ForkTables.GetTableNames()`), and `AdoptionRowCheck` lists them under "Fork tables:" and requires them empty. A step that writes rows into an existing table on adoption also needs a plan and a row check there, so the routine maintenance steps write none.
- Table classes of fork-only tables map to their declaration instead of the reference (`ReferenceSnapshotTests.TableClasses_MapToReferenceColumns`).

Scratch databases that tests build from a reference file must use the reference the code expects, `OdSchema-26.1.70.sql` (or `GetReference()`), since the CRUD writes the 26.1 columns.

One fork-only table gets rows on adoption: the inventory stock ledger `supplyledger`, whose step (2026110211) writes an opening balance per supply with stock, so that `supply.LevelOnHand` is the sum of the ledger from the start. It is the one exception to "created empty": migration-preview lists the rows (`tools/AdoptionValidation/InventoryPlan.cs`), `AdoptionRowCheck` leaves it out of "Fork tables:" and holds it to exactly those rows ("Stock ledger:"), and its `TableAdded` rule says so. Its definition is checked like every other fork-only table.
