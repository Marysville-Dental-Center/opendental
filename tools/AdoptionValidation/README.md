# AdoptionValidation

The M1-Z switch-gate rehearsal ([docs/specs/port-backlog.md](../../docs/specs/port-backlog.md), section M1-Z) on **synthetic data**. It adopts a
synthetic Open Dental 25.3.48 database with the program's own adoption code and proves the result: schema, row counts, checksums, business
totals, every 24.4 to 25.3 column through the business layer, the version lock-out, and idempotence. Every step is timed.

The procedure for the office itself is [docs/adoption-runbook.md](../../docs/adoption-runbook.md). The signed-off results are in
[docs/m1-validation-report.md](../../docs/m1-validation-report.md).

## Safety

- **Local only.** Connects to `localhost`, `127.0.0.1` or `::1` only (the SyntheticData guard). Outbound HTTP is routed to a dead local proxy and
  makes the run fail with exit code 3.
- **Writes only scratch databases** named `synth_m1z_<tag>_r<n>_src` and `synth_m1z_<tag>_r<n>_adopted`, and drops them at the end unless
  `--keep`. `credentials` and `startup-check` only read, and work on any local database.
- **No office data, no Open Dental program.** The data comes from [tools/SyntheticData](../SyntheticData/README.md). The "Open Dental startup
  check" is a model of the version checks in the published 24.3.63 source, not the licensed client.

## Quick start

```powershell
# Medium preset, two full runs, results in .\adoption-rehearsal-<time>:
.\tools\AdoptionValidation\Invoke-AdoptionRehearsal.ps1

# Or run the program directly:
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 2 --out C:\Temp\m1z
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe credentials --database <database>
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe startup-check --database <database>
```

| Command | What it does |
|---|---|
| `rehearse` | The whole rehearsal (below), `--runs` times (default 2). Writes `rehearsal-result.json`, `rehearsal-result.md` and the validation reports (`runN-1-baseline.json`, `runN-2-adopted.json`) to `--out`. Exit code 0 when every check passes, 6 otherwise. |
| `credentials` | Lists the stored passwords the fork cannot read (Open Dental's encryption), so they can be re-entered after adoption. Locations and row numbers only, never values. Read-only. |
| `startup-check` | Shows, for Open Dental 24.3.63, 25.3.48, 25.4 and 26.1 clients, what each would do at startup on the database. Read-only. |
| `roundtrip` | Only the data-layer round trip, on an adopted `synth*` database. |

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
4. **Back up and restore** the copy that is adopted (`mariadb-dump`, then the `mariadb` client), and check the copy equals the baseline.
5. **Credential inventory** of the copy.
6. **Before adoption**: the fork's startup guard must refuse the copy (`NeedsAdoption`); the startup model must show an Open Dental 25.3.48
   client would use it (control case).
7. **Preflight and adoption**: `Adoption.Preflight` and `Adoption.Run`, the same calls `FormAdoptDatabase` makes (they run the fork migration
   steps too).
8. **Fork migration and schema verifier**: every step again (`RunAll`), which must change nothing; `SchemaVerifier.VerifyDatabase` must find
   no difference at all from Open Dental 25.3.48.
9. **Rehash**: `SecurityHash.RehashNow` (Database Maintenance, Tools, Security Hashes), with a record of the hash formats per table.
10. **Validate** against the baseline with SyntheticData's comparer and
    [allowed-differences.adoption.json](../SyntheticData/allowed-differences.adoption.json): row counts, checksums of untouched tables,
    business totals, the dropped column and index, the removed update files, the blanked addresses, the sentinel versions and fork markers, the
    fork's own startup guard.
11. **Open Dental-style startup check** ([OdStartupCheck.cs](OdStartupCheck.cs)): every Open Dental release client must exit without converting,
    writing, installing services or copying binaries.
12. **Data-layer round trip** ([DataLayerRoundTrip.cs](DataLayerRoundTrip.cs)): for every table with 24.4 to 25.3 columns, through its generated
    CRUD class: load every row and compare each new column with the raw stored value; save every row unchanged and compare the table cell by
    cell; insert a clone, give every new column a new value (longer than the old width for widened columns, with a quote and a non-ASCII letter
    for text), update, read back and check the raw row; delete the clone.
13. **Second pass**: adoption again (refused), the fork migration again (no change), the verifier again, the rehash again (no change), and a
    report that must equal the first pass.

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
field and CRUD, value comparison, new values for widened columns and the normalization rules. The integration tests run the whole rehearsal
(small preset, two runs) on scratch databases `synth_m1z_t<time>_<pid>_*` and check every gate check, the timings, the adopted database itself,
that no audit timestamp moved, the credential inventory, the startup model, the result files, and a negative control (a second guardian
renumbering is caught). They are inconclusive without a local server or a 25.3.48 schema source.

## Layout

| File | Contents |
|---|---|
| `Program.cs` | Command line |
| `Rehearsal.cs` | The run, the checks, the timings and the cross-run comparison |
| `OfficeFixture.cs` | Office-like rows added before the baseline |
| `DatabaseCopier.cs` | Backup and restore (`mariadb-dump`) or SQL copy |
| `BusinessLayer.cs` | Connects OpenDentBusiness; hash counts |
| `OdStartupCheck.cs` | Model of Open Dental's startup version checks |
| `DataLayerRoundTrip.cs` | Load, save and write through the generated CRUD classes |
| `CredentialInventory.cs` | Stored passwords to re-enter |
| `RehearsalResult.cs` | Results, JSON and Markdown |
| `Invoke-AdoptionRehearsal.ps1` | Build-and-run wrapper |
