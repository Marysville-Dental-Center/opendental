# AdoptionValidation

The M1-Z switch-gate rehearsal ([docs/specs/port-backlog.md](../../docs/specs/port-backlog.md), section M1-Z) on **synthetic data**. It adopts a
synthetic Open Dental 25.3.48 database with the program's own adoption code and proves the result: schema, row counts, checksums, business
totals, the rows adoption may change, every 24.4 to 25.3 column through the business layer, the version lock-out, and idempotence. Every step
is timed.

It also holds the **steps of the office procedure that run without the program**, [docs/adoption-runbook.md](../../docs/adoption-runbook.md),
which the office runs on a Windows admin PC through an SSH forward to the Ubuntu database server (runbook step 0.0): `float-check` (step 0.2),
`connections` (steps 1.1, 10.1, 11.1 and Rollback), `copy` (step 3.2), `adopt` (steps 4 and 5), `startup-check` (step 6.1), `credentials`
(steps 0.6 and 7), and the dry run of those commands ([Invoke-RunbookDryRun.ps1](Invoke-RunbookDryRun.ps1), step 0.6). The rehearsal copies
with the code of `copy` and adopts with the code of `adopt` ([HeadlessAdoption.cs](HeadlessAdoption.cs)), and takes its post-adoption report
before anyone starts the program, as the office does. The signed-off results are in [docs/m1-validation-report.md](../../docs/m1-validation-report.md).

## Safety

- **Local only.** Connects to `localhost`, `127.0.0.1` or `::1` only (the SyntheticData guard), on `--port`. On the office's admin PC that
  port is the local end of the SSH forward of runbook step 0.0; the forward is the only way from there to the database server. The business
  layer gets the same port (`BusinessLayer.ServerAndPort`). Outbound HTTP is routed to a dead local proxy and makes the run fail with exit
  code 3.
- **No copy of the database is made off the database server.** `copy` makes the copy on the server (`INSERT ... SELECT`), and the row checks
  (`RowComparer`) run on the server. The tools read only what they work on (`adopt` reads the preferences it changes and, for the rehash,
  the keys, dates, amounts and statuses it hashes; `credentials` reads the rows that hold stored passwords) and write only reports (counts,
  checksums, totals), logs and the places where passwords are stored.
- **`rehearse` writes only scratch databases** named `<prefix><tag>_r<n>_src`, `_adopted` and `_snap` (prefix `synth_m1z_` unless `--prefix`),
  and drops them at the end unless `--keep`. `credentials`, `startup-check`, `connections` and `float-check` only read, and work on any local
  database.
- **`copy` only creates a new database.** It refuses a name that exists, a missing original, the same name twice, any connection to either,
  and views, triggers, routines or events it would not carry; afterwards every table's row count and `AUTO_INCREMENT` must equal the
  original's and nobody may have connected meanwhile.
- **Every check that nobody is connected sees every connection**: it refuses an account without the `PROCESS` privilege, which would see
  only its own (`HeadlessAdoption.EnsureSeesAllConnections`).
- **`adopt` changes nothing until its checks pass**: `--confirm` repeats `--database`; `--original` is a different, unadopted Open Dental
  database; nobody else is connected to either; a copy that is not adopted yet has the original's tables and exact row counts and was created
  after it. It writes only the copy it adopts, and ends with a row-by-row check against the original of what adoption changed.
- **`restore` only creates a new database.** It refuses a name that exists, and a backup file that names a database (`USE`, `CREATE`, `DROP` or
  `ALTER DATABASE`, a table name qualified with a database: what a `--databases` or `--all-databases` dump contains), because loading such a file
  would also write to the database it names; with MyISAM there is no undo. It loads with `mariadb --one-database`. The runbook no longer uses
  it; the rehearsal uses it with `--copy dump`.
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

# The runbook's tool commands, on a synthetic database kept by a rehearsal:
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 1 --keep --skip-roundtrip --tag dry --out C:\Temp\m1z-dry-src
.\tools\AdoptionValidation\Invoke-RunbookDryRun.ps1 -Original synth_m1z_dry_r1_src -Copy synth_m1z_dry_pms -Folder C:\Temp\m1z-dry -AsOf 2026-09-30
```

| Command | What it does |
|---|---|
| `rehearse` | The whole rehearsal (below), `--runs` times (default 2). Writes `rehearsal-result.json`, `rehearsal-result.md` and the validation reports (`runN-1-baseline.json` of the source, `runN-1-copy.json` of the copy, `runN-2-adopted.json`) to `--out`. Exit code 0 when every check passes, 6 otherwise. |
| `connections --database DB` | Lists every other connection that has a default database (account, computer, database) and fails (exit code 6) when one uses `DB`; exit code 0 when none does. Read-only. |
| `float-check --database DB` | Counts, per column, the FLOAT and DOUBLE values of `DB` that a logical backup (`mariadb-dump`) would not restore exactly: MariaDB writes a FLOAT as text with 6 significant digits. Names columns, never values. Exit code 0 when there are none, 6 when there are some. Read-only. |
| `copy --original DB --database NEW` | Runbook step 3.2: copies `DB` into the new database `NEW` on the server (`CREATE TABLE ... LIKE`, the `AUTO_INCREMENT` counters, `INSERT ... SELECT` of every stored column in key order), exactly, FLOAT values included (see Safety). Exit code 0 (`Copy: PASS`), 2 when refused (nothing created), 1 when it failed (drop the partial copy). |
| `adopt --database COPY --original ORIGINAL --confirm COPY [--log FILE]` | Runbook steps 4 and 5, without starting the program: `Adoption.Preflight` and `Adoption.Run` (what the Adopt Database window calls), the fork migration with every step again (must change nothing), the schema verifier (must match 25.3.48 exactly), `SecurityHash.RehashNow` (what Database Maintenance's Security Hashes tool calls), a security log entry for the rehash, and the row check against `ORIGINAL` ([AdoptionRowCheck.cs](AdoptionRowCheck.cs)): in `preference` only the markers added, the version sentinels set and addresses of Open Dental's servers blanked; in `documentmisc` only Open Dental's update files deleted; in `securitylog` and `securityloghash` only adoption's and the rehash's entries added; `updatehistory` unchanged. Prints, and appends to `--log`, every step and its time. Exit code 0 (`Adopt: PASS`), 6 when a check failed (`Adopt: FAIL`), 1 when adoption stopped (`Adopt: STOPPED`), 2 when refused (nothing changed). After a fail or a stop the runbook drops the copy and adopts a new one (an adoption cut off after it wrote `ForkSchemaLevel` cannot be finished). On a finished adoption it skips adoption and repeats the rest, which changes nothing but its log entry. |
| `credentials` | Lists the stored passwords the fork cannot read (Open Dental's encryption), so they can be re-entered after adoption. Locations and row numbers only, never values. Read-only. |
| `startup-check` | Shows, for Open Dental 24.3.63, 25.3.48, 25.4 and 26.1 clients, what each would do at startup on the database. Read-only. Exit code 0 when every release client refuses the database, 6 when one would use it. |
| `restore --dump FILE --database NEW` | Checks a backup file and restores it as a new database (see Safety). Exit code 0, or 2 when refused. Not used by the runbook. |
| `roundtrip` | Only the data-layer round trip, on an adopted `synth*` database. |

[Invoke-RunbookDryRun.ps1](Invoke-RunbookDryRun.ps1) `-Original DB -Copy NEW -Folder DIR [-AsOf yyyy-MM-dd] [-Tools DIR] [-Port 3306] [-User root]`
runs the tool commands of runbook steps 0.2 (`float-check`, recorded), 1.1 (`connections`), 3.2 (`copy`), 3.3 (two `SyntheticData validate`
reports and `compare`), 4 and 5 (`adopt`), 6.1 (`validate`, `compare --allowed`, `startup-check`) and the go-live checks 10.1 (`connections`)
and 10.2 (a last report of the original and `compare`), each with `--port` and `--user`: everything before the program's first start. Step
3.1, the folder copy on the database server, is bash on the server and not part of it. It stops at the first unexpected result, writes every
report and log and `dry-run-timings.txt` to the (new, empty) folder, never writes to `-Original`, never drops a database, and needs no
MariaDB client program. The build copies it next to the program.

Options of `rehearse`: `--size small|medium|large` (default medium), `--seed`, `--as-of`, `--runs`, `--fill-remaining N` (default 2: filler
rows in every table the generator leaves empty, so the checksums cover 410 of the 422 tables), `--families`, `--years`, `--copy server|dump`
(default `server`: the runbook's copy; `dump` backs up with `mariadb-dump` and restores with `restore`, found through `MARIADB_BIN`, the `PATH`
or `Program Files\MariaDB*`), `--keep`, `--skip-roundtrip`, `--prefix` (default `synth_m1z_`), `--tag`, `--schema-sql`, `--schema-from`,
`--allowed`. Connection: `--server`, `--port`, `--user`; the password from `SYNTH_DB_PASSWORD`.

## What one run does

1. **Generate** a synthetic 25.3.48 database (SyntheticData, real 25.3.48 schema including the office-only `patient._portal_active_email`).
2. **Office fixture** ([OfficeFixture.cs](OfficeFixture.cs)): the rows an office database has that the generator leaves out because they
   change start-up: Open Dental `UpdateFiles` and `UpdateFilesSegment` rows (placeholder text, not program files) plus one share-screen row
   that must stay; preferences holding Open Dental server addresses (built from the blocked-host list), a vendor address and a help text that
   must stay; Open Dental's version history; three stored passwords in Open Dental's encryption (random bytes); `RandomPrimaryKeys`.
3. **Baseline**: row counts, `CHECKSUM TABLE`, a checksum per column (FLOAT values read exactly, as DOUBLE) and the business totals of
   [business-totals.sql](../SyntheticData/sql/business-totals.sql) (aging buckets, production, income, payment-plan balances, insurance
   estimates, appointments and more).
4. **FLOAT check** of the source (`float-check`, runbook step 0.2); the synthetic values are short, so it finds none.
5. **Copy** the source the way runbook step 3.2 does (`copy`), and check the copy equals the baseline. The copy's report is the runbook's
   `baseline-copy.json`. With `--copy dump`: a `mariadb-dump` backup and the restore instead.
6. **Credential inventory** of the copy.
7. **Before adoption**: the fork's startup guard must refuse the copy (`NeedsAdoption`); the startup model must show an Open Dental 25.3.48
   client would use it (control case).
8. to 10. **The headless adoption** ([HeadlessAdoption.cs](HeadlessAdoption.cs), the code `adopt` runs in runbook steps 4 and 5):
   `Adoption.Preflight` and `Adoption.Run`, the same calls `FormAdoptDatabase` makes (they run the fork migration steps too); the fork migration
   with every step again (`RunAll`), which must change nothing; `SchemaVerifier.VerifyDatabase`, which must find no difference at all from Open
   Dental 25.3.48; `SecurityHash.RehashNow` (Database Maintenance, Tools, Security Hashes), with a record of the hash formats per table and a
   security log entry; the row check against the source.
11. **Validate**, before anything else touches the copy (runbook step 6.1): the adopted copy against the copy's report with SyntheticData's
    comparer and [allowed-differences.adoption.json](../SyntheticData/allowed-differences.adoption.json): row counts, checksums of untouched
    tables, business totals, the dropped column and index, the removed update files, the blanked addresses, the sentinel versions and fork
    markers, the fork's own startup guard. Then a snapshot of `preference`, `securitylog` and `securityloghash` (the `_snap` database) for the
    row-by-row comparisons of the second pass and across runs.
12. **Open Dental-style startup check** ([OdStartupCheck.cs](OdStartupCheck.cs)): every Open Dental release client must exit without converting,
    writing, installing services or copying binaries. Then the row check's result (V11).
13. **Data-layer round trip** ([DataLayerRoundTrip.cs](DataLayerRoundTrip.cs)): for every table with 24.4 to 25.3 columns, through its generated
    CRUD class: load every row and compare each new column with the raw stored value; save every row unchanged and compare the table cell by
    cell; insert a clone, give every new column that Insert or the server does not set a new value (longer than the old width for widened
    columns, with a quote and a non-ASCII letter for text), update, read back and check the raw row; delete the clone.
14. **Second pass**: adoption again (refused), then the headless adoption again with its row check, as an operator re-running runbook step 4
    on the adopted copy would: it skips the adoption, the fork migration and the rehash change nothing, and the verifier still matches. Every
    table but `securitylog` and `securityloghash` equals the first pass by its report; row by row against the snapshot, `preference` is
    identical and those two only gained the rerun's own entry.

Then the runs are compared: identical baselines; identical adopted databases apart from the time of adoption (every other table by its
report, and `preference`, `securitylog` and `securityloghash` row by row, where only `ForkAdoptedFrom`'s value, `securitylog.LogDateTime` and
the `securityloghash.LogHash` computed from it may differ); identical check results.

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
field and CRUD, value comparison, new values for widened columns, the normalization rules, the restore's dump check, the business layer's
port, the `PROCESS` check, the FLOAT check's expression and the new commands' arguments. The integration tests run the whole rehearsal (small
preset, two runs) on scratch databases `synth_m1z_t<time>_<pid>_*` and check every gate check, the timings, that the rehearsal copies and
adopts with the runbook's sequence and its row check, the adopted database itself, that no audit timestamp moved, the credential inventory,
the startup model, the result files, and a negative control (a second guardian renumbering is caught). They also run the runbook's commands:
`copy` refuses an existing, missing or repeated name and a connection, and creates nothing; `copy` is exact where a dump rounds FLOAT values,
which `float-check` finds and the validator now sees; `connections` fails while someone is connected; the business layer connects through the
port it is given; the row check catches changes the whole-table rules let through; an adoption cut off before its markers is finished by
`adopt`, one cut off after them fails closed, and a new copy then adopts; `restore` refuses an existing name and a real `--databases` dump and
leaves the database it names untouched; `adopt` refuses a wrong `--confirm`, a missing or adopted original, the same name twice, swapped names,
a connection to the original and a copy that differs, and changes nothing when it refuses; and `Invoke-RunbookDryRun.ps1` runs out of process
through a relay on another port (standing in for the SSH forward) and passes, after which `adopt` run again through the relay changes nothing
but its log entry. They are inconclusive without a local server or a 25.3.48 schema source; the dump half of the FLOAT test needs the MariaDB
client programs.

## Layout

| File | Contents |
|---|---|
| `Program.cs` | Command line |
| `Rehearsal.cs` | The run, the checks, the timings and the cross-run comparison |
| `HeadlessAdoption.cs` | Adoption, migration, verifier, rehash and row check without the program (rehearsal and `adopt`), `adopt`'s checks, the connection checks |
| `AdoptionRowCheck.cs` | What adoption may change in `preference`, `documentmisc`, `securitylog`, `securityloghash` and `updatehistory`, row by row |
| `RowComparer.cs` | Row-by-row comparison of one table in two databases, done by the server |
| `FloatCheck.cs` | The FLOAT and DOUBLE values a logical backup would round |
| `OfficeFixture.cs` | Office-like rows added before the baseline |
| `DatabaseCopier.cs` | The runbook's server-side copy; backup and restore (`mariadb-dump`, `restore`) |
| `DumpCheck.cs` | The restore's check that a backup file names no database |
| `BusinessLayer.cs` | Connects OpenDentBusiness (with the port); hash counts |
| `OdStartupCheck.cs` | Model of Open Dental's startup version checks |
| `DataLayerRoundTrip.cs` | Load, save and write through the generated CRUD classes |
| `CredentialInventory.cs` | Stored passwords to re-enter |
| `RehearsalResult.cs` | Results, JSON and Markdown |
| `Invoke-AdoptionRehearsal.ps1` | Build-and-run wrapper |
| `Invoke-RunbookDryRun.ps1` | The runbook's tool commands, steps 0.2 to 6.1, 10.1 and 10.2 (the dry run of step 0.6) |
