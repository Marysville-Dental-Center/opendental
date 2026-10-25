# AdoptionValidation

The M1-Z switch-gate rehearsal ([docs/specs/port-backlog.md](../../docs/specs/port-backlog.md), section M1-Z) on **synthetic data**. It adopts a
synthetic Open Dental 25.3.48 database with the program's own adoption code and proves the result: schema, row counts, checksums, business
totals, every 24.4 to 25.3 column through the business layer, the version lock-out, and idempotence. Every step is timed.

It also holds the **headless steps of the office procedure**, [docs/adoption-runbook.md](../../docs/adoption-runbook.md): `restore` (steps 0.6
and 3.2), `adopt` (steps 4 and 5), `startup-check` (step 6), `credentials` (steps 0.6 and 7), and the dry run of steps 3 to 6
([Invoke-RunbookDryRun.ps1](Invoke-RunbookDryRun.ps1), step 0.6). The rehearsal adopts its copy with the same code as `adopt`
([HeadlessAdoption.cs](HeadlessAdoption.cs)), so the office runs exactly the sequence that was rehearsed, and takes its post-adoption report
before anyone starts the program. The signed-off results are in [docs/m1-validation-report.md](../../docs/m1-validation-report.md).

## Safety

- **Local only.** Connects to `localhost`, `127.0.0.1` or `::1` only (the SyntheticData guard). Outbound HTTP is routed to a dead local proxy and
  makes the run fail with exit code 3.
- **`rehearse` writes only scratch databases** named `synth_m1z_<tag>_r<n>_src` and `synth_m1z_<tag>_r<n>_adopted`, and drops them at the end
  unless `--keep`. `credentials` and `startup-check` only read, and work on any local database.
- **`restore` only creates a new database.** It refuses a name that exists, and a backup file that names a database (`USE`, `CREATE`, `DROP` or
  `ALTER DATABASE`, a table name qualified with a database: what a `--databases` or `--all-databases` dump contains), because loading such a file
  would also write to the database it names; with MyISAM there is no undo. It loads with `mariadb --one-database`.
- **`adopt` changes nothing until its checks pass**: `--confirm` repeats `--database`; `--original` is a different, unadopted Open Dental
  database; nobody else is connected to either; a copy that is not adopted yet has the original's tables and exact row counts and was created
  after it. It writes only the copy it adopts.
- **No office data, no Open Dental program.** The rehearsal's data comes from [tools/SyntheticData](../SyntheticData/README.md). The "Open
  Dental startup check" is a model of the version checks in the published 24.3.63 source, not the licensed client.

## Quick start

```powershell
# Medium preset, two full runs, results in .\adoption-rehearsal-<time>:
.\tools\AdoptionValidation\Invoke-AdoptionRehearsal.ps1

# Or run the program directly:
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 2 --out C:\Temp\m1z
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe credentials --database <database>
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe startup-check --database <database>

# The runbook's steps 3.1 to 6 with its own commands, on a synthetic database kept by a rehearsal:
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 1 --keep --tag dry --out C:\Temp\m1z-dry-src
.\tools\AdoptionValidation\Invoke-RunbookDryRun.ps1 -Original synth_m1z_dry_r1_src -Copy synth_m1z_dry_pms -Folder C:\Temp\m1z-dry -AsOf 2026-09-30
```

| Command | What it does |
|---|---|
| `rehearse` | The whole rehearsal (below), `--runs` times (default 2). Writes `rehearsal-result.json`, `rehearsal-result.md` and the validation reports (`runN-1-baseline.json` of the source, `runN-1-copy.json` of the restored copy, `runN-2-adopted.json`) to `--out`. Exit code 0 when every check passes, 6 otherwise. |
| `restore --dump FILE --database NEW` | Runbook steps 0.6 and 3.2: checks the backup file and restores it as a new database (see Safety). Exit code 0, or 2 when refused. |
| `adopt --database COPY --original ORIGINAL --confirm COPY [--log FILE]` | Runbook steps 4 and 5, without starting the program: `Adoption.Preflight` and `Adoption.Run` (what the Adopt Database window calls), the fork migration with every step again (must change nothing), the schema verifier (must match 25.3.48 exactly), `SecurityHash.RehashNow` (what Database Maintenance's Security Hashes tool calls) and a security log entry for the rehash. Prints, and appends to `--log`, every step and its time. Exit code 0 (`Adopt: PASS`), 6 when a check failed, 1 when adoption stopped (run it again to finish), 2 when refused (nothing changed). On an adopted copy it skips adoption and repeats the rest, which changes nothing but its log entry. |
| `credentials` | Lists the stored passwords the fork cannot read (Open Dental's encryption), so they can be re-entered after adoption. Locations and row numbers only, never values. Read-only. |
| `startup-check` | Shows, for Open Dental 24.3.63, 25.3.48, 25.4 and 26.1 clients, what each would do at startup on the database. Read-only. Exit code 0 when every release client refuses the database, 6 when one would use it. |
| `roundtrip` | Only the data-layer round trip, on an adopted `synth*` database. |

[Invoke-RunbookDryRun.ps1](Invoke-RunbookDryRun.ps1) `-Original DB -Copy NEW -Folder DIR [-AsOf yyyy-MM-dd] [-Tools DIR] [-MariaDbBin DIR] [-KeepBackup]`
runs runbook steps 3.1 to 6 and the go-live check of step 10 with the runbook's commands: `mariadb-dump`, `restore`, three
`SyntheticData validate` reports and their comparisons, `adopt`, `startup-check`, the process list and a last report of the original. It stops at
the first unexpected result, writes every report and log and `dry-run-timings.txt` to the (new, empty) folder, and deletes the backup file at the
end unless `-KeepBackup`. It never writes to `-Original` and never drops a database. The build copies it next to the program.

Options of `rehearse`: `--size small|medium|large` (default medium), `--seed`, `--as-of`, `--runs`, `--fill-remaining N` (default 2: filler
rows in every table the generator leaves empty, so the checksums cover 410 of the 422 tables), `--families`, `--years`, `--copy dump|sql`
(default: a real `mariadb-dump` backup and restore when the client programs are found, through `MARIADB_BIN`, the `PATH` or
`Program Files\MariaDB*`), `--keep`, `--skip-roundtrip`, `--tag`, `--schema-sql`, `--schema-from`, `--allowed`. Connection: `--server`,
`--port`, `--user`; the password from `SYNTH_DB_PASSWORD`.

## What one run does

1. **Generate** a synthetic 25.3.48 database (SyntheticData, real 25.3.48 schema including the office-only `patient._portal_active_email`).
2. **Office fixture** ([OfficeFixture.cs](OfficeFixture.cs)): the rows an office database has that the generator leaves out because they
   change start-up: Open Dental `UpdateFiles` and `UpdateFilesSegment` rows (placeholder text, not program files) plus one share-screen row
   that must stay; preferences holding Open Dental server addresses (built from the blocked-host list), a vendor address and a help text that
   must stay; Open Dental's version history; three stored passwords in Open Dental's encryption (random bytes); `RandomPrimaryKeys`.
3. **Baseline**: row counts, `CHECKSUM TABLE`, a checksum per column and the business totals of
   [business-totals.sql](../SyntheticData/sql/business-totals.sql) (aging buckets, production, income, payment-plan balances, insurance
   estimates, appointments and more).
4. **Back up and restore** the copy that is adopted (`mariadb-dump`, then the runbook's restore: the file check, a new database,
   `mariadb --one-database`), and check the copy equals the baseline. The copy's report is the runbook's `baseline-copy.json`.
5. **Credential inventory** of the copy.
6. **Before adoption**: the fork's startup guard must refuse the copy (`NeedsAdoption`); the startup model must show an Open Dental 25.3.48
   client would use it (control case).
7. to 9. **The headless adoption** ([HeadlessAdoption.cs](HeadlessAdoption.cs), the code `adopt` runs in runbook steps 4 and 5):
   `Adoption.Preflight` and `Adoption.Run`, the same calls `FormAdoptDatabase` makes (they run the fork migration steps too); the fork migration
   with every step again (`RunAll`), which must change nothing; `SchemaVerifier.VerifyDatabase`, which must find no difference at all from Open
   Dental 25.3.48; `SecurityHash.RehashNow` (Database Maintenance, Tools, Security Hashes), with a record of the hash formats per table and a
   security log entry.
10. **Validate**, before anything else touches the copy (runbook step 6): the adopted copy against the restored copy's report with SyntheticData's
    comparer and [allowed-differences.adoption.json](../SyntheticData/allowed-differences.adoption.json): row counts, checksums of untouched
    tables, business totals, the dropped column and index, the removed update files, the blanked addresses, the sentinel versions and fork
    markers, the fork's own startup guard.
11. **Open Dental-style startup check** ([OdStartupCheck.cs](OdStartupCheck.cs)): every Open Dental release client must exit without converting,
    writing, installing services or copying binaries.
12. **Data-layer round trip** ([DataLayerRoundTrip.cs](DataLayerRoundTrip.cs)): for every table with 24.4 to 25.3 columns, through its generated
    CRUD class: load every row and compare each new column with the raw stored value; save every row unchanged and compare the table cell by
    cell; insert a clone, give every new column a new value (longer than the old width for widened columns, with a quote and a non-ASCII letter
    for text), update, read back and check the raw row; delete the clone.
13. **Second pass**: adoption again (refused), then the headless adoption again, as an operator re-running runbook step 4 would: it skips the
    adoption, the fork migration and the rehash change nothing, the verifier still matches, and the report equals the first pass apart from the
    rerun's own security log entry.

Then the runs are compared: identical baselines, identical adopted databases (apart from the adoption time), identical check results.

### First-save normalizations

Saving a row through the CRUD layer writes two kinds of stored values differently, without changing what the program reads: a `NULL` text
column becomes `''` (`PIn.String` reads `NULL` as `""`), and an enum stored as text (`CrudSpecialColType.EnumAsString`) that is blank becomes the
name of the value it was read as. The row's ON UPDATE timestamp then moves too. The round trip lists these per column and fails on any other
change. On the synthetic data they are `benefit.ToothRange` and `insplan.GroupNum` (`NULL`), `document.ExternalSource` and
`computerpref.ComputerOS` (blank enum text).

## Tests

`AdoptionValidation.Tests` (in `OpenDental.sln` and `OpenDental.Local.slnf`):

```
vstest.console.exe tools\AdoptionValidation\AdoptionValidation.Tests\bin\Debug\AdoptionValidation.Tests.dll /Platform:x86
```

Unit tests cover the startup model (every Open Dental release refuses an adopted database; the unadopted control cases; what happens if update
files or addresses were left), the round trip's column list against SyntheticData's coverage lists, that every 24.4 to 25.3 column has a class
field and CRUD, value comparison, new values for widened columns, the normalization rules, and the restore's dump check (every way a dump can
name a database; a single-database dump passes; long lines). The integration tests run the whole rehearsal (small preset, two runs) on
scratch databases `synth_m1z_t<time>_<pid>_*` and check every gate check, the timings, that the rehearsal adopts with the runbook's sequence,
the adopted database itself, that no audit timestamp moved, the credential inventory, the startup model, the result files, and a negative
control (a second guardian renumbering is caught). They also run the runbook's commands: `restore` refuses an existing name and a real
`--databases` dump and leaves the database it names untouched; `adopt` refuses a wrong `--confirm`, a missing or adopted original, the same
name twice, swapped names, a connection to the original and a copy that differs, and changes nothing when it refuses; and
`Invoke-RunbookDryRun.ps1` runs steps 3.1 to 6 out of process and passes, after which `adopt` run again changes nothing but its log entry.
They are inconclusive without a local server, a 25.3.48 schema source or (for the commands) the MariaDB client programs.

## Layout

| File | Contents |
|---|---|
| `Program.cs` | Command line |
| `Rehearsal.cs` | The run, the checks, the timings and the cross-run comparison |
| `HeadlessAdoption.cs` | Adoption, migration, verifier and rehash without the program (rehearsal and `adopt`), and `adopt`'s checks |
| `OfficeFixture.cs` | Office-like rows added before the baseline |
| `DatabaseCopier.cs` | Backup and restore (`mariadb-dump`, the runbook's restore) or SQL copy |
| `DumpCheck.cs` | The restore's check that a backup file names no database |
| `BusinessLayer.cs` | Connects OpenDentBusiness; hash counts |
| `OdStartupCheck.cs` | Model of Open Dental's startup version checks |
| `DataLayerRoundTrip.cs` | Load, save and write through the generated CRUD classes |
| `CredentialInventory.cs` | Stored passwords to re-enter |
| `RehearsalResult.cs` | Results, JSON and Markdown |
| `Invoke-AdoptionRehearsal.ps1` | Build-and-run wrapper |
| `Invoke-RunbookDryRun.ps1` | Runbook steps 3.1 to 6 with the runbook's commands (the dry run of step 0.6) |
