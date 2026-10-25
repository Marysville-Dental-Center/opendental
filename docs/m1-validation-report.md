# M1 validation report: the adoption rehearsal (switch gate M1-Z)

Status: **ready for the owner's sign-off**, 2026-10-24, revised after review (M1-Z r1). Branch `m1/m1-z-r1` (from `m1/m1-z`); rehearsal and
dry runs at commit `9dead7006`, build and tests at `53422a6f6` (which only labels two times in `adopt`'s log as UTC). Synthetic data only (owner decision of 2026-10-18: no office census, no office data). Procedure for the office:
[adoption-runbook.md](adoption-runbook.md). Tooling: [tools/AdoptionValidation](../tools/AdoptionValidation/README.md).

The first version of this report rested on a cutover check that had never been rehearsed: the runbook started the program on the adopted copy
before taking the post-adoption report, which the rehearsal never did (F8). In this revision the office runs the rehearsed sequence itself:
runbook steps 3 to 6 use the rehearsal's own code, without the program, and that sequence was dry-run with the runbook's own commands
(section "Runbook dry run").

## Verdict

The rehearsal passed, twice in a row, on the medium synthetic database (422 tables, 101,881 rows): **25 of 25 checks in each run and 3 of 3
checks across the runs**. A single run on the large preset (777,822 rows) also passed 25 of 25 and gives the timing data for the outage plan.
The runbook's steps 3.1 to 6 and its go-live checks, run with the runbook's own commands on a medium and a large synthetic database, passed
both times.

- Adoption drops `patient._portal_active_email` and its unique index, deletes Open Dental's update files, blanks Open Dental's server
  addresses, and sets the version sentinels and fork markers. The adopted schema then matches Open Dental 25.3.48 **exactly** (no documented
  exception needed).
- Nothing else changes. Row counts are the same except for the rows adoption is meant to add or delete. 410 of 422 tables are
  byte-identical (row count, `CHECKSUM TABLE`, every column checksum). The 12 touched tables differ only in the allowed ways. All 146 business
  totals are unchanged, except the one that counts the dropped column.
- All 139 columns new or changed between 24.3 and 25.3.48, in 50 tables, load, save and write through OpenDentBusiness. The exceptions are
  three documented ones: two binary chat attachment columns and the deprecated `deletedobject` table, all preserved untouched.
- Every Open Dental release client modelled (24.3.63, 25.3.48, 25.4, 26.1; workstation and update server; classic update view) refuses the
  adopted database without converting it, writing to it, installing services or copying binaries.
- Repeating adoption, the fork migration, the verifier and the rehash changes nothing (apart from the rehash's own security log entry). Two
  independent runs produce identical databases.
- The office runs the same sequence: the rehearsal adopts with `HeadlessAdoption`, the code `AdoptionValidation adopt` runs in runbook steps 4
  and 5, and compares the adopted copy with the restored copy's report, as runbook step 6.1 does, before anything else touches the copy.

One defect was found and fixed during the gate (F1 below): the rehash moved every rehashed row's audit timestamp. The review of the first
version found three defects in the procedure, fixed in this revision (F8 to F10). The owner has open decisions (section "For the owner");
none blocks the switch, and the runbook has a path for either answer.

## Scope and method

| Task step (M1-Z) | How it was done | Evidence |
|---|---|---|
| 1. Generate a medium 25.3.48 database | `tools/SyntheticData`, medium preset (600 families, 2 years, seed 20261018, as of 2026-09-30) on the real 25.3.48 schema (`fox-db-schema.sql`, checked against `od_253_schema`), plus 2 filler rows in every table the generator leaves empty and an office fixture (see below) | G1 |
| 2. Record the validation baseline | Row counts, `CHECKSUM TABLE ... EXTENDED`, a checksum per column, and 146 business totals (aging buckets, production, income, write-offs, deposits, insurance estimates, payment-plan debits, credits, payments and balances, appointments, dated fees, accounting, statements, feature counts) | `run1-1-baseline.json`; G1 |
| 3. Run the adoption tool on a copy | `mariadb-dump` backup, restore into a new database with the runbook's restore (file check, new name only, `--one-database`), check the copy equals the baseline (its report is the runbook's `baseline-copy.json`), then `HeadlessAdoption`, the code `AdoptionValidation adopt` runs in runbook step 4: `Adoption.Preflight` and `Adoption.Run`, the calls `FormAdoptDatabase` makes (the GUI itself was not launched) | G2, A1-A4 |
| 4. Fork migration and schema verifier | In the same class: `ForkMigrationRunner.Run` with every step again (`RunAll`), `SchemaVerifier.VerifyDatabase`; then the rehash (`SecurityHash.RehashNow`, runbook step 5) | M1, M2, H1 |
| 5. Validate | Compare the adopted copy with the restored copy's report under `allowed-differences.adoption.json` (runbook step 6.1), before anything else touches it; direct checks of the column, index, update files, addresses, sentinels and markers; the fork's guard; the Open Dental startup model; the data-layer round trip | V1-V10, R1, R2 |
| 6. Run it all twice | Within each run, a second pass on the adopted copy (the headless adoption again, as an operator re-running step 4 would); and the whole run twice from a fresh generation, compared run against run | I1-I4, X1-X3 |
| 7. Time each step | Every step timed in both runs; one extra large run; the runbook dry run timed per step | Timings below |
| 8. Runbook | [adoption-runbook.md](adoption-runbook.md); its steps 3.1 to 6 and its go-live checks dry-run with its own commands (`Invoke-RunbookDryRun.ps1`) on a medium and a large synthetic database, and on a small one in every test run | Runbook dry run below |
| 9. This report | | |

**Office fixture.** The generator leaves out rows that change start-up behaviour, so the rehearsal adds what an office database carries,
all synthetic, before the baseline:
- 4 Open Dental update-file rows (placeholder text), plus 1 share-screen row that must survive
- 4 preferences holding Open Dental server addresses (built from the fork's blocked-host list), plus a vendor address and a help text that
  must survive
- 4 rows of Open Dental version history
- 3 stored passwords in Open Dental's encryption (random bytes): a bridge password, a clearinghouse password and a reporting-server password
- `RandomPrimaryKeys`, which every insert through the business layer reads

**Environment.** Windows 11 Pro, MariaDB 10.11.19 on `127.0.0.1:3306` (the local development server; `fox-db` was never contacted), Debug x86
build of this branch. All tools refuse any server other than this machine and block outbound HTTP; no run reported a blocked request.

**Owner decisions applied.** Synthetic data only; `patient._portal_active_email` dropped by adoption; chat and dental-school features
excluded (their tables were filled with a few rows each and survive untouched: checksums and round trip); public sources only (the Open
Dental startup model follows the published GPL 24.3.63 source, not the licensed client); everything local.

## Timings

Seconds per step. Medium is the gate (two runs); large is one extra run for the outage plan. The data-layer round trip and the second pass are
rehearsal-only checks and are not part of the office procedure.

| Step | Medium, run 1 | Medium, run 2 | Large, run 1 |
|---|---:|---:|---:|
| Generate synthetic 25.3.48 database | 7.6 | 7.1 | 50.8 |
| Add office fixture | 0.0 | 0.0 | 0.1 |
| Record baseline report | 1.8 | 1.7 | 12.1 |
| Back up (`mariadb-dump`; 20.4 MB medium, 161.6 MB large) | 3.1 | 3.1 | 4.8 |
| Restore the copy (with the file check) | 6.2 | 6.3 | 25.9 |
| Validate the restored copy | 2.2 | 2.3 | 12.8 |
| Credential inventory | 0.0 | 0.1 | 0.0 |
| Preflight | 0.0 | 0.0 | 0.0 |
| Adopt (`Adoption.Run`, including its fork migration steps) | 0.6 | 0.4 | 1.0 |
| Fork migration (all steps again) | 0.1 | 0.1 | 0.1 |
| Schema verifier | 0.3 | 0.2 | 0.3 |
| Rehash (`SecurityHash.RehashNow`) | 0.8 | 0.8 | 4.3 |
| Record post-adoption report and compare | 1.8 | 1.7 | 13.1 |
| Open Dental-style startup check | 0.0 | 0.0 | 0.0 |
| Data-layer round trip (rehearsal only) | 12.3 | 12.5 | 86.8 |
| Record post-round-trip report (rehearsal only) | 1.8 | 1.8 | 13.4 |
| Second pass (rehearsal only) | 2.4 | 2.3 | 14.9 |
| **Total** | **41.0** | **40.5** | **240.4** |

For the office procedure (backup, restore, three reports, adoption, verifier, rehash), the large run needs about 75 seconds on this PC; the
runbook dry run below, which adds the go-live report and runs every command as a separate program, took 90.5 seconds. The office
database's size is unknown (there is no census), so the runbook times the office's own dry run in the week before (step 0.6), or plans from
these figures with a larger margin (step 0.7).

## Runbook dry run

`Invoke-RunbookDryRun.ps1` runs the runbook's steps 3.1 to 6.1 and the go-live checks of step 10 with the runbook's own commands, each as a
separate program, as the office runs them: `mariadb-dump` with the runbook's options, `AdoptionValidation restore`, `SyntheticData validate`
for both databases and `compare`, `AdoptionValidation adopt`, `SyntheticData validate` and `compare --allowed allowed-differences.adoption.json`,
`AdoptionValidation startup-check`, the process list of the stand-in original, and a last report of the original compared with its baseline.
It ran on synthetic databases kept by a rehearsal (`rehearse --runs 1 --keep --skip-roundtrip`, office fixture included) standing in for
`opendental`, as of 2026-09-30. Seconds per step:

| Runbook step | Medium (101,881 rows, 35.5 MB of tables) | Large (777,822 rows, 285.8 MB of tables) |
|---|---:|---:|
| 3.1 Back up the original (`mariadb-dump`) | 3.1 | 4.8 |
| 3.2 Restore the copy (`AdoptionValidation restore`) | 6.6 | 26.0 |
| 3.3 Two baseline reports and their comparison | 5.1 | 26.2 |
| 4 and 5 `AdoptionValidation adopt` (checks, adoption, migration again, verifier, rehash) | 2.5 | 7.0 |
| 6.1 Post-adoption report, comparison, startup check | 3.0 | 13.6 |
| 10.1 and 10.2 No connection to the original; the original unchanged | 2.6 | 12.9 |
| **Total** | **22.9** | **90.5** |
| Result | `Dry run: PASS` | `Dry run: PASS` |

In both: the baselines of the original and the copy were identical (0 differences); `adopt` found the copy had the original's 422 tables and
row counts and newer tables, and printed "Adoption finished.", "Fork migration, every step again: 9 steps run, 0 changes.", "The schema
matches Open Dental 25.3.48 exactly (422 of 422 tables).", "Rehash finished." and "Adopt: PASS"; the post-adoption comparison found
`Unexpected differences: 0` (50 allowed, counting the per-table `CHECKSUM TABLE` lines); the startup check ended "Every Open Dental release
client refuses this database." (exit code 0); no connection used the original; and the original's last report equalled its baseline (0
differences). The script deleted its backup file at the end. The test suite runs the same script out of process on a small database in every
run (`RunbookDryRun_PassesWithTheRunbookCommands`), then runs `adopt` again on the adopted copy, which changes nothing but its own security
log entry.

## Findings

**F1. Fixed: the rehash moved every rehashed row's audit timestamp.** `SecurityHash.RehashNow` (and the update-time `UpdateHashing`) wrote
`SecurityHash` with a plain `UPDATE`. MariaDB's `ON UPDATE CURRENT_TIMESTAMP` therefore set `DateTStamp` or `SecDateTEdit` to the rehash
time. On the first run this showed as 6 unexpected differences: `appointment.DateTStamp`, `claim.SecDateTEdit`, `claimproc.SecDateTEdit`,
`insverify.SecDateTEdit`, `patient.DateTStamp` and `paysplit.SecDateTEdit`. On the office database it would have stamped every patient, and
every recent appointment, claim, claim procedure, payment split and insurance row, with the adoption time. Commit `5c61ba62d` makes each hash
`UPDATE` also write `Col=Col` for the table's ON UPDATE columns. `RowHashDatabaseTests.RehashNow_KeepsTheAuditTimestamps` covers it; it fails
without the change. The integration test `AdoptionAndRehash_KeepEveryAuditTimestamp` checks that no timestamp moved on any hashed table.

**F2. First-save normalizations (no change needed; for the owner's information).** Saving a row through the generated CRUD layer writes two
kinds of stored value differently, without changing what the program reads:
- a `NULL` text column becomes `''`, because `PIn.String` reads `NULL` as `""`
- a blank enum stored as text becomes the name of the value it was read as

The row's ON UPDATE timestamp then moves, because the row did change. On the synthetic data these cells were affected:
- `benefit.ToothRange`: `NULL` saved as `''`, 363 rows (a 25.3 column)
- `insplan.GroupNum`: `NULL` saved as `''`, 2 rows (widened in 25.2)
- `document.ExternalSource`: blank saved as `None`, 2,344 rows
- `computerpref.ComputerOS`: blank saved as `Win32S`, 4 rows

On the office database the same happens the first time a user saves such a row. `benefit.ToothRange` is the likely case: rows that predate
25.3 can hold `NULL` (inference; there is no census). The meaning is the same in the program. Only an outside report that counts `NULL` and
`''` separately would notice. The round trip fails on any other kind of change.

**F3. Model, not the licensed client (residual risk R1).** The Open Dental startup check is an executable model of `ClassConvertDatabase.Convert`
and `PrefL.CheckProgramVersion` from the published 24.3.63 source. Open Dental 25.3 is closed source. Backlog R1 (strategy item 5) proposes a
one-time confirmation with the licensed client on an adopted copy, with the owner's agreement. Two limits are reported, not hidden:
- A **Debug** build of Open Dental skips its version check and would open the adopted database. Open Dental ships only Release builds.
- Before its version checks, an Open Dental client may update the `MySqlVersion` preference and back up, repair and optimize tables if the
  MariaDB server version changed, and it re-enables indexes on a restored backup. These steps do not depend on version numbers. They change
  no schema and no business data, but they are writes.

The runbook therefore removes the Open Dental clients and services (steps 2 and 2.1) instead of relying on the lock-out alone.

**F4. Stored passwords must be re-entered (expected; ground rule 3).** The fork cannot read values Open Dental encrypted with its own key.
`AdoptionValidation credentials` lists them by location and row, never by value. On the rehearsal copy it found the 3 fixture passwords and 5
filler rows (`programproperty`, `hl7def`, `ebill`). The filler rows are listed correctly: their values are not in the fork's format. The
office list is made in runbook step 0.6.3 if the owner allows a preparation copy, otherwise during the outage in step 7. The full list of
places to re-enter passwords is in runbook step 7.

**F5. Adoption's security log entry depends on `RandomPrimaryKeys`.** Without that preference (absent from plain synthetic data),
`Adoption.Run` skips its closing security log entry silently: the call is wrapped in `SwallowAnyException`. Every Open Dental database has the
preference, and the fixture adds it. With it, the entry is written, and so is the rehash's entry that `HeadlessAdoption` adds as Database
Maintenance's Security Hashes tool does (V1: `securitylog` 2701 to 2703). No change made.

**F6. Payment-plan hashes outside the window are cleared, as Open Dental does.** The rehash clears every `payplan.SecurityHash` and rehashes
only plans dated in the checked window (one month): 15 of 15 synthetic plans end up blank. This is Open Dental's own algorithm, and blank means
"not hashed", not "tampered". Recorded here because R2 item 6 asks for the rehash record.

**F7. Excluded and legacy data survive.** The chat tables (`chat`, `chatmsg` with `SeqCount`, `chatattach`, `chatreaction`, `chatuserattach`,
`chatuserod`, `sequencecounter`) and the dental-school tables and columns hold rows. They are untouched by adoption and round-trip through
their classes. `deletedobject` has no data-layer class (deprecated in 24.3) and is preserved byte for byte. `claimpayment.Note` stays
`varchar(255)`, as in 25.3.48 (the documented drift). The round trip fills it to exactly 255 characters.

**F8. Fixed (review): the runbook's cutover check would have failed on a correct adoption, and was never rehearsed.** The first runbook
adopted through the program: start it on the copy and accept the adoption tool (step 4.2), restart it (4.4), rehash in Database Maintenance
(5), and only then take the post-adoption report and require "Unexpected differences: 0" (6), any difference meaning Rollback. But a start
and a log-in write to tables the allowed list does not cover: `Computers.UpdateHeartBeat` on every start (`computer.LastHeartBeat`), a new
`computer` row for a computer not in the table yet (`Computers.EnsureComputerInDB`), the Database Maintenance list (`InsertMissingDBMs`), and
likely `computerpref`, `signalod`, `activeinstance` and the log-in tables. The 12 tables the synthetic data leaves empty are exactly such
start-up and log-in tables (`activeinstance`, `alertitem`, `alertread`, `alertsub`, `centralconnection`, `databasemaintenance`,
`loginattempt`, `registrationkey`, `replicationserver`, `scheduledprocess`, `sessiontoken`, `signalod`), and the rehearsal never started the
program, so it could not see this. On the day, a correct adoption would have forced a rollback, or a judgement by hand during the outage.
Fix: the adoption, the migration check, the verifier and the rehash run in one class, `HeadlessAdoption`; the rehearsal adopts with it, and
the office runs it as `AdoptionValidation adopt` (runbook steps 4 and 5). The post-adoption report and comparison (6.1) come before the
program's first start (6.2), and the rehearsal compares with the restored copy's report exactly as 6.1 does. The allowed list is unchanged:
nothing it does not cover runs before the comparison. The sequence was dry-run with the runbook's commands (above). Because the command is
easier to mistype than the window was to misuse, `adopt` refuses, and changes nothing, when `--confirm` does not repeat the name, the original
is missing, adopted or the same database, someone is connected to either, the copy's tables or row counts differ from the original's, or the
names are the wrong way round (the copy's tables are not newer); the integration tests try each.

**F9. Fixed (review): the preparation restore could overwrite the live database.** The first runbook's step 0.5 restored "the latest nightly
backup into a scratch database" on the live MyISAM server, during office hours, with no command, no fresh backup first and no safeguard, and
called it an owner decision although the owner has not made it (item 3 below). Nightly dumps are often made with `--databases` or
`--all-databases`, which write `CREATE DATABASE` and `USE opendental`: loaded into a "scratch" database, such a file replays `DROP TABLE`,
`CREATE TABLE` and `INSERT` into the live database and replaces today's data with last night's, with no undo. Fix: runbook step 0.6 depends
on the owner's answer and runs outside office hours. It starts with a fresh single-database backup and restores it with
`AdoptionValidation restore`, which refuses a name that exists and any file that names a database (`USE`, `CREATE`, `DROP` or
`ALTER DATABASE`, a qualified table name) before changing anything, and loads with `mariadb --one-database`. Plain `mariadb < file` and the
nightly file are ruled out; a folder-copy backup is restored under a new folder name. If the owner says no (step 0.7), the password list is
made on the day (step 7, `credentials` on `opendental_pms`) with the passwords collected beforehand, the window is planned from these timings
with a 200% margin, and the tools can be checked on the office server with synthetic data. The rehearsal and step 3.2 restore with the same
code; a test feeds it a real `--databases` dump and checks that it refuses and that the database the dump names is untouched.

**F10. Fixed (review): nothing enforced or checked that all workstations switch together.** The first runbook allowed removing only the Open
Dental shortcuts (step 2.1), which leaves `OpenDental.exe` and `FreeDentalConfig.xml` pointing at `opendental`. That database stays live and
unadopted, and an Open Dental 25.3.48 client opens it normally (check A2: "Proceeds"). No go-live step re-checked the connections to
`opendental` or compared it with its baseline, and step 11 then dropped it: work entered in Open Dental after the backup would have been lost
without notice. Fix: step 2.1 requires uninstalling Open Dental and renaming its `FreeDentalConfig.xml` on every workstation of a list made
in step 0.5, and revoking the workstations' MariaDB account on `opendental` where it has its own grant; step 8 has a sign-off table per
workstation; before going live (10.1, 10.2) and again before dropping the original (11.1), no connection may use `opendental` and its report
must equal `baseline-original.json` with no allowed differences. Any difference is treated as lost work, with a procedure to find it in the
security log and re-enter it. The dry run runs these server-side checks.

## Not covered by this rehearsal

- **Office data.** By owner decision, there is no census and no copy of office data. Volumes, enum values actually in use, bridges in use and
  the real password list come from the runbook's preparation on the office server: the dry run of steps 3 to 6 on a preparation copy (step
  0.6) if the owner agrees (item 3 below), otherwise the outage itself (step 0.7).
- **Golden totals from Open Dental 25.3.48** (backlog M1-Z work item 2, R2 item 7). Comparing the fork's reports with Open Dental's own
  needs the licensed client and office data. The rehearsal proves instead that adoption changes no total. The runbook adds a comparison with
  the office's last Open Dental aging report.
- **The program's own start.** The adoption, the rehash and the post-adoption comparison no longer go through the program: the office runs
  the same code from the command line, in the rehearsed order, before anyone starts the program (runbook steps 4 to 6.1), and that sequence
  was dry-run with the runbook's commands. Not exercised here (launching `OpenDental.exe` was not allowed in this run): the first start,
  log-in, smoke test and publishing (steps 6.2 and 8), which come after the gate. Their writes (this computer's row and heartbeat, the
  Database Maintenance list, signals, log-ins, security log) are normal use; no comparison covers them, and none needs to. The Adopt Database
  window and Database Maintenance's Security Hashes tool call the same code but are not used on the day.
- **The workstations.** Removing Open Dental from each workstation (step 2.1) and switching it (step 8) are office work. The server-side checks
  that catch a forgotten one (steps 10.1, 10.2 and 11.1) are part of the dry run.
- **Full-size timing.** The largest rehearsal has 777,822 rows. The office server's own times come from its dry run (step 0.6), or the window
  is planned from these figures with a 200% margin (step 0.7).

## For the owner

1. **Sign-off of this report**, which opens the switch (M1-Z exit criterion).
2. Decide whether to confirm the lock-out once with the licensed Open Dental 25.3.48 client on a synthetic adopted copy (F3; R1 item 5).
3. Decide whether the preparation may restore a fresh backup of the office database into a scratch copy on the office server, outside office
   hours, to list the passwords, time the steps and dry-run steps 3 to 6 on real data (runbook step 0.6). This is office data on office
   hardware, not a development copy, and nothing leaves the server. If not, runbook step 0.7 applies: the password list is made during the
   outage, and the window is planned from the rehearsal with a larger margin.
4. Agree the rollback deadline (runbook step 0.9).

| | Name | Decision | Date | Signature |
|---|---|---|---|---|
| Owner | | Approve the switch / not yet | | |
| Owner | | Preparation copy (item 3): yes / no | | |
| Administrator | | Runbook read and understood | | |

## Build and test results

Build (at `53422a6f6`): `MSBuild OpenDental.Local.slnf` (Debug, x86). A full rebuild gave 0 errors and 376 warnings, the same as the
baseline before this branch; AdoptionValidation and its tests add no warnings. `tools/Test-NoHqHosts.ps1` passed (5,990 files in 47 projects).

Tests: every test assembly under the solution's `bin\Debug` folders, run with `vstest.console.exe /Platform:x86`. All passed, none skipped or
inconclusive, 1,823 tests in total. AdoptionValidation.Tests has 8 new tests: the restore's dump check (4), the rehearsal's adoption sequence,
`restore` refusing an existing name and a real `--databases` dump, `adopt` refusing every wrong invocation without changing anything, and
the runbook dry run out of process followed by `adopt` run again.

| Assembly | Passed |
|---|---:|
| NoHqTests | 49 |
| AccountingTests | 26 |
| ApptTests | 76 |
| AttributionTests | 15 |
| ChartLetterTests | 45 |
| CryptoTests | 40 |
| EFormTests | 60 |
| FeeTests | 48 |
| ForkMigrationTests | 37 |
| GroupNoteTests | 54 |
| ImagingTests | 43 |
| InsuranceTests | 49 |
| MedicationTests | 11 |
| PayPlanTests | 29 |
| QueryMonitorTests | 30 |
| RegistryTests | 33 |
| ReleaseFlowTests | 58 |
| ReportTests | 28 |
| RowHashTests | 22 |
| SheetTests | 31 |
| SignatureTests | 55 |
| StatementTests | 8 |
| TaskCategoryTests | 21 |
| VendorDirectTests | 872 |
| AdoptionValidation.Tests | 34 |
| SyntheticData.Tests | 49 |

## How to reproduce

```powershell
& "C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe" OpenDental.Local.slnf -restore `
  -p:RestorePackagesConfig=true -p:Configuration=Debug -p:Platform=x86 -m -nodeReuse:false -v:minimal
$env:ADOPTION_REHEARSAL_COMMIT=(git rev-parse --short HEAD)
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 2 --out C:\Temp\m1z
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size large --runs 1 --out C:\Temp\m1z-large
# The runbook dry run, on a synthetic database kept by a rehearsal (drop the three synth_m1z_dry_* databases afterwards):
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 1 --keep --skip-roundtrip --tag dry --out C:\Temp\m1z-dry-src
.\tools\AdoptionValidation\Invoke-RunbookDryRun.ps1 -Original synth_m1z_dry_r1_src -Copy synth_m1z_dry_pms -Folder C:\Temp\m1z-dry -AsOf 2026-09-30
vstest.console.exe tools\AdoptionValidation\AdoptionValidation.Tests\bin\Debug\AdoptionValidation.Tests.dll /Platform:x86
```

The same seed gives the same database, so the row counts, checksums and totals below repeat exactly; only timings, scratch names and the
adoption times differ.

## Appendix: the rehearsal's own results (medium, two runs)

Generated by `AdoptionValidation rehearse` (`rehearsal-result.md`), unedited apart from the heading levels.

Overall: **PASS**. 2 runs, size medium, seed 20261018, as of 2026-09-30, filler rows 2, server 10.11.19-MariaDB, started 2026-10-25 04:03:17Z, commit 9dead7006.

### Step timings (seconds)

| Step | Run 1 | Run 2 |
|---|---:|---:|
| Generate synthetic 25.3.48 database | 7.6 | 7.1 |
| Add office fixture (update files, server addresses, stored passwords) | 0.0 | 0.0 |
| Record baseline report | 1.8 | 1.7 |
| Back up (mariadb-dump) | 3.1 | 3.1 |
| Restore the copy | 6.2 | 6.3 |
| Validate the restored copy | 2.2 | 2.3 |
| Credential inventory | 0.0 | 0.1 |
| Preflight | 0.0 | 0.0 |
| Adopt (Adoption.Run, including its fork migration steps) | 0.6 | 0.4 |
| Fork migration (all steps again) | 0.1 | 0.1 |
| Schema verifier | 0.3 | 0.2 |
| Rehash (SecurityHash.RehashNow) | 0.8 | 0.8 |
| Record post-adoption report and compare | 1.8 | 1.7 |
| Open Dental-style startup check | 0.0 | 0.0 |
| Data-layer round trip (load and save through OpenDentBusiness) | 12.3 | 12.5 |
| Record post-round-trip report | 1.8 | 1.8 |
| Second pass (adoption, migration, verifier, rehash, report) | 2.4 | 2.3 |
| **Total** | **41.0** | **40.5** |

### Run 1: PASS

Source synth_m1z_261025040317_31684_r1_src (422 tables, 101,881 rows; seed=20261018;size=medium;families=600;years=2;asOf=2026-09-30;tool=SyntheticData 1), adopted copy synth_m1z_261025040317_31684_r1_adopted (copy by mariadb-dump backup and restore).

| Check | Result | Detail |
|---|---|---|
| G1 Generated database is consistent | PASS | 422 tables, 101,881 rows, 410 tables with rows; coverage 72 of 72 24.4-25.3 items; 15 consistency checks, 0 failed |
| G2 Restored copy equals the baseline | PASS | row counts, CHECKSUM TABLE, every column checksum and every total equal |
| A1 Fork startup guard refuses the unadopted copy | PASS | NeedsAdoption (exit code 171) |
| A2 Startup model: an Open Dental 25.3.48 client uses the unadopted copy (control) | PASS | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no; a 25.4.30 update server: PublishesUpdateAndProceeds; conversion yes, database writes yes, services installed yes, binaries copied from the database no, Setup.exe tried no |
| A3 Preflight accepts the copy | PASS | Open Dental 25.3.48 database. It can be adopted. |
| A4 Adoption finished | PASS | dropped column True, indexes portal_active_email_uniq, 4 update-file rows deleted, 4 preferences blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl) |
| M1 Fork migration changes nothing on the adopted copy | PASS | 9 steps run (2026101901, 2026101902, 2026101903, 2026101904, 2026101905, 2026101906, 2026101907, 2026101951, 2026101971), 0 changes |
| M2 Schema matches Open Dental 25.3.48 exactly | PASS | 422 of 422 tables; 0 differences, 0 mismatches (no documented exception needed) |
| H1 Rehash gives every patient, and every row in the verified window, a fork hash | PASS | rehash window from 2026-09-24; appointment: 5846 rows, 551 in the rehash window (0 without a fork hash); 551 fork hashes, 4343 other, 952 blank; claim: 3523 rows, 34 in the rehash window (0 without a fork hash); 34 fork hashes, 3064 other, 425 blank; claimproc: 11055 rows, 334 in the rehash window (0 without a fork hash); 334 fork hashes, 8017 other, 2704 blank; inssub: 421 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 69 other, 352 blank; insverify: 624 rows, 17 in the rehash window (0 without a fork hash); 17 fork hashes, 607 other, 0 blank; insverifyhist: 304 rows, 9 in the rehash window (0 without a fork hash); 9 fork hashes, 295 other, 0 blank; patient: 1355 rows, 1355 in the rehash window (0 without a fork hash); 1355 fork hashes, 0 other, 0 blank; payplan: 15 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 0 other, 15 blank; paysplit: 6878 rows, 60 in the rehash window (0 without a fork hash); 60 fork hashes, 6121 other, 697 blank; rehash recorded in the security log |
| V1 Row counts unchanged except where adoption deletes or adds rows | PASS | 422 tables before, 422 after; 101,881 rows before, 101,886 after; changed: documentmisc 5 -> 1, preference 25 -> 30, securitylog 2701 -> 2703, securityloghash 2 -> 4 |
| V2 Checksums of untouched tables identical | PASS | 410 of 422 tables have identical row counts, CHECKSUM TABLE and column checksums; touched (each by an allowed adoption change): appointment, claim, claimproc, documentmisc, insverify, insverifyhist, patient, payplan, paysplit, preference, securitylog, securityloghash |
| V3 Every difference is an allowed adoption difference | PASS | 38 allowed, 0 unexpected |
| V4 Business totals unchanged (aging, production, income, payment plans and the rest) | PASS | 145 of 146 totals identical; changed: TotalChanged patients.portal_active_email_nonnull (651 -> error: Unknown column '_portal_active_email' in 'WHERE')  [allowed: The column it counts is dropped by adoption.] |
| V5 patient._portal_active_email and its unique index dropped | PASS | column gone, index portal_active_email_uniq gone; two active patients may now share an e-mail address |
| V6 Open Dental UpdateFiles rows removed | PASS | 4 UpdateFiles/UpdateFilesSegment rows before, 0 after; other documentmisc rows kept: 1 of 1 |
| V7 Open Dental server addresses blanked, other preferences kept | PASS | 4 address preferences, 0 still set; vendor address kept True; help text kept True |
| V8 Sentinel versions and fork markers set | PASS | DataBaseVersion 99.0.0.0, ProgramVersion 99.0.0.0, ForkSchemaLevel 2026101971 (latest step 2026101971), OdSchemaCompat 25.3.48, ForkAdoptedFrom source=25.3.48.0;started=2026-10-25T04:03:39Z;adopted=2026-10-25T04:03:39Z |
| V9 Fork startup guard opens the adopted copy | PASS | ProceedFork, pending migrations False |
| V10 Startup model: every Open Dental release client refuses the adopted copy | PASS | 9 of 9 release-client scenarios exit without converting, writing, installing services or copying binaries;  (a Debug build of Open Dental skips its version check: Proceeds; Open Dental ships Release builds only) |
| R1 Every 24.4-25.3 column loads, saves and writes through OpenDentBusiness | PASS | 50 tables, 139 columns, 32,465 rows loaded and saved, 129 new values written and read back; documented exceptions: chatattach.Thumbnail; chatattach.FileData; deletedobject; first-save normalizations (same meaning in the program): benefit.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (363 cells); benefit.ToothRange: NULL saved as '' (363 cells); computerpref.ComputerOS: blank or unknown enum text saved as 'Win32S' (4 cells); document.DateTStamp: ON UPDATE timestamp of a row the save normalized (2344 cells); document.ExternalSource: blank or unknown enum text saved as 'None' (2344 cells); insplan.GroupNum: NULL saved as '' (2 cells); insplan.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (2 cells) |
| R2 Round trip leaves the database unchanged apart from the first-save normalizations | PASS | only the normalized columns changed: benefit.SecDateTEdit; benefit.ToothRange; computerpref.ComputerOS; document.DateTStamp; document.ExternalSource; insplan.GroupNum; insplan.SecDateTEdit |
| I1 A second adoption is refused | PASS | This database is already adopted (source=25.3.48.0;started=2026-10-25T04:03:39Z;adopted=2026-10-25T04:03:39Z). Nothing to do. |
| I2 A second fork migration changes nothing and the schema still matches | PASS | adoption skipped (already adopted); 9 steps run, 0 changes; verifier 0 differences |
| I3 A second rehash changes nothing | PASS | every hash already current: appointment 551, claim 34, claimproc 334, inssub 0, insverify 17, insverifyhist 9, patient 1355, payplan 0, paysplit 60 |
| I4 The whole second pass leaves the database unchanged | PASS | row counts and every checksum equal to the first pass, apart from the rerun's own security log entry (1 row) |

### Run 2: PASS

Source synth_m1z_261025040317_31684_r2_src (422 tables, 101,881 rows; seed=20261018;size=medium;families=600;years=2;asOf=2026-09-30;tool=SyntheticData 1), adopted copy synth_m1z_261025040317_31684_r2_adopted (copy by mariadb-dump backup and restore).

| Check | Result | Detail |
|---|---|---|
| G1 Generated database is consistent | PASS | 422 tables, 101,881 rows, 410 tables with rows; coverage 72 of 72 24.4-25.3 items; 15 consistency checks, 0 failed |
| G2 Restored copy equals the baseline | PASS | row counts, CHECKSUM TABLE, every column checksum and every total equal |
| A1 Fork startup guard refuses the unadopted copy | PASS | NeedsAdoption (exit code 171) |
| A2 Startup model: an Open Dental 25.3.48 client uses the unadopted copy (control) | PASS | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no; a 25.4.30 update server: PublishesUpdateAndProceeds; conversion yes, database writes yes, services installed yes, binaries copied from the database no, Setup.exe tried no |
| A3 Preflight accepts the copy | PASS | Open Dental 25.3.48 database. It can be adopted. |
| A4 Adoption finished | PASS | dropped column True, indexes portal_active_email_uniq, 4 update-file rows deleted, 4 preferences blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl) |
| M1 Fork migration changes nothing on the adopted copy | PASS | 9 steps run (2026101901, 2026101902, 2026101903, 2026101904, 2026101905, 2026101906, 2026101907, 2026101951, 2026101971), 0 changes |
| M2 Schema matches Open Dental 25.3.48 exactly | PASS | 422 of 422 tables; 0 differences, 0 mismatches (no documented exception needed) |
| H1 Rehash gives every patient, and every row in the verified window, a fork hash | PASS | rehash window from 2026-09-24; appointment: 5846 rows, 551 in the rehash window (0 without a fork hash); 551 fork hashes, 4343 other, 952 blank; claim: 3523 rows, 34 in the rehash window (0 without a fork hash); 34 fork hashes, 3064 other, 425 blank; claimproc: 11055 rows, 334 in the rehash window (0 without a fork hash); 334 fork hashes, 8017 other, 2704 blank; inssub: 421 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 69 other, 352 blank; insverify: 624 rows, 17 in the rehash window (0 without a fork hash); 17 fork hashes, 607 other, 0 blank; insverifyhist: 304 rows, 9 in the rehash window (0 without a fork hash); 9 fork hashes, 295 other, 0 blank; patient: 1355 rows, 1355 in the rehash window (0 without a fork hash); 1355 fork hashes, 0 other, 0 blank; payplan: 15 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 0 other, 15 blank; paysplit: 6878 rows, 60 in the rehash window (0 without a fork hash); 60 fork hashes, 6121 other, 697 blank; rehash recorded in the security log |
| V1 Row counts unchanged except where adoption deletes or adds rows | PASS | 422 tables before, 422 after; 101,881 rows before, 101,886 after; changed: documentmisc 5 -> 1, preference 25 -> 30, securitylog 2701 -> 2703, securityloghash 2 -> 4 |
| V2 Checksums of untouched tables identical | PASS | 410 of 422 tables have identical row counts, CHECKSUM TABLE and column checksums; touched (each by an allowed adoption change): appointment, claim, claimproc, documentmisc, insverify, insverifyhist, patient, payplan, paysplit, preference, securitylog, securityloghash |
| V3 Every difference is an allowed adoption difference | PASS | 38 allowed, 0 unexpected |
| V4 Business totals unchanged (aging, production, income, payment plans and the rest) | PASS | 145 of 146 totals identical; changed: TotalChanged patients.portal_active_email_nonnull (651 -> error: Unknown column '_portal_active_email' in 'WHERE')  [allowed: The column it counts is dropped by adoption.] |
| V5 patient._portal_active_email and its unique index dropped | PASS | column gone, index portal_active_email_uniq gone; two active patients may now share an e-mail address |
| V6 Open Dental UpdateFiles rows removed | PASS | 4 UpdateFiles/UpdateFilesSegment rows before, 0 after; other documentmisc rows kept: 1 of 1 |
| V7 Open Dental server addresses blanked, other preferences kept | PASS | 4 address preferences, 0 still set; vendor address kept True; help text kept True |
| V8 Sentinel versions and fork markers set | PASS | DataBaseVersion 99.0.0.0, ProgramVersion 99.0.0.0, ForkSchemaLevel 2026101971 (latest step 2026101971), OdSchemaCompat 25.3.48, ForkAdoptedFrom source=25.3.48.0;started=2026-10-25T04:04:21Z;adopted=2026-10-25T04:04:21Z |
| V9 Fork startup guard opens the adopted copy | PASS | ProceedFork, pending migrations False |
| V10 Startup model: every Open Dental release client refuses the adopted copy | PASS | 9 of 9 release-client scenarios exit without converting, writing, installing services or copying binaries;  (a Debug build of Open Dental skips its version check: Proceeds; Open Dental ships Release builds only) |
| R1 Every 24.4-25.3 column loads, saves and writes through OpenDentBusiness | PASS | 50 tables, 139 columns, 32,465 rows loaded and saved, 129 new values written and read back; documented exceptions: chatattach.Thumbnail; chatattach.FileData; deletedobject; first-save normalizations (same meaning in the program): benefit.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (363 cells); benefit.ToothRange: NULL saved as '' (363 cells); computerpref.ComputerOS: blank or unknown enum text saved as 'Win32S' (4 cells); document.DateTStamp: ON UPDATE timestamp of a row the save normalized (2344 cells); document.ExternalSource: blank or unknown enum text saved as 'None' (2344 cells); insplan.GroupNum: NULL saved as '' (2 cells); insplan.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (2 cells) |
| R2 Round trip leaves the database unchanged apart from the first-save normalizations | PASS | only the normalized columns changed: benefit.SecDateTEdit; benefit.ToothRange; computerpref.ComputerOS; document.DateTStamp; document.ExternalSource; insplan.GroupNum; insplan.SecDateTEdit |
| I1 A second adoption is refused | PASS | This database is already adopted (source=25.3.48.0;started=2026-10-25T04:04:21Z;adopted=2026-10-25T04:04:21Z). Nothing to do. |
| I2 A second fork migration changes nothing and the schema still matches | PASS | adoption skipped (already adopted); 9 steps run, 0 changes; verifier 0 differences |
| I3 A second rehash changes nothing | PASS | every hash already current: appointment 551, claim 34, claimproc 334, inssub 0, insverify 17, insverifyhist 9, patient 1355, payplan 0, paysplit 60 |
| I4 The whole second pass leaves the database unchanged | PASS | row counts and every checksum equal to the first pass, apart from the rerun's own security log entry (1 row) |

### Across runs

| Check | Result | Detail |
|---|---|---|
| X1 Every run starts from an identical database | PASS | 2 baselines identical (row counts, every checksum, every total) |
| X2 Every run ends with an identical adopted database | PASS | 2 adopted databases identical, apart from the adoption time in preference and securitylog |
| X3 Every run gives the same check results | PASS | 2 runs, 25 checks each |

### Details of run 1

Office fixture: 4 Open Dental update-file rows and 1 other documentmisc row; 4 preferences with Open Dental addresses, 2 that must be kept; 4 updatehistory rows; 3 stored passwords in Open Dental's encryption (preference ReportingServerMySqlPassHash, programproperty XVWeb Password, clearinghouse Fictional Claims Exchange).

#### Key business totals

| Total | Before | After |
|---|---:|---:|
| patients.families | 600 | 600 |
| production.gross | 1682667.00 | 1682667.00 |
| production.net | 1579953.32 | 1579953.32 |
| production.writeoffs | 96057.00 | 96057.00 |
| income.patient | 767507.19 | 767507.19 |
| income.insurance | 691001.69 | 691001.69 |
| income.deposits | 1436758.06 | 1436758.06 |
| aging.stored.bal_0_30 | 49283.89 | 49283.89 |
| aging.stored.bal_31_60 | 9013.68 | 9013.68 |
| aging.stored.bal_61_90 | 7702.74 | 7702.74 |
| aging.stored.bal_over_90 | 49478.76 | 49478.76 |
| aging.stored.bal_total | 115479.07 | 115479.07 |
| aging.stored.ins_est | 38177.78 | 38177.78 |
| aging.stored.payplan_due | 3069.31 | 3069.31 |
| aging.ledger_total | 115479.07 | 115479.07 |
| insurance.outstanding_estimate | 38177.78 | 38177.78 |
| insurance.treatment_plan_estimates | 176505.60 | 176505.60 |
| payplans.balance | 9128.29 | 9128.29 |
| payplans.principal_due | 14427.03 | 14427.03 |
| appointments.future_scheduled | 507 | 507 |
| appointments.mirrored | 1690 | 1690 |
| fees.rows_dated | 142 | 142 |
| accounting.totals.debits | 1679226.47 | 1679226.47 |

#### Differences adoption made (all allowed)

- ColumnChanged appointment.SecurityHash (12572210759711 -> 12544800561387)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged claim.SecurityHash (7523732762616 -> 7542675149352)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged claimproc.SecurityHash (23581417588163 -> 23575717718012)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- RowsChanged documentmisc (5 -> 1)  [allowed: Adoption deletes Open Dental's UpdateFiles rows.]
- ColumnChanged documentmisc.DateCreated (10069374261 -> 1538566696)  [allowed: Adoption deletes Open Dental's UpdateFiles rows.]
- ColumnChanged documentmisc.DocMiscNum (9769361567 -> 1174942932)  [allowed: Adoption deletes Open Dental's UpdateFiles rows.]
- ColumnChanged documentmisc.DocMiscType (7861376959 -> 3630997879)  [allowed: Adoption deletes Open Dental's UpdateFiles rows.]
- ColumnChanged documentmisc.FileName (6379039644 -> 942913718)  [allowed: Adoption deletes Open Dental's UpdateFiles rows.]
- ColumnChanged documentmisc.RawBase64 (9667482446 -> 1779909166)  [allowed: Adoption deletes Open Dental's UpdateFiles rows.]
- ColumnChanged insverify.SecurityHash (1390900252556 -> 1394331695558)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged insverifyhist.SecurityHash (628167813450 -> 626646706119)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnRemoved patient._portal_active_email  [allowed: Owner decision: adoption drops the office-specific generated column and its unique key.]
- ColumnChanged patient.SecurityHash (2927956709010 -> 2860196771227)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged payplan.SecurityHash (25963509457 -> 33942964343)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged paysplit.SecurityHash (14601550875872 -> 14612268801073)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- RowsChanged preference (25 -> 30)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences.]
- ColumnChanged preference.Comments (62404309912 -> 73897142823)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences.]
- ColumnChanged preference.PrefName (57697714827 -> 65925037523)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences.]
- ColumnChanged preference.PrefNum (53764528705 -> 64321672810)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences.]
- ColumnChanged preference.ValueString (55961409849 -> 70327624351)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences.]
- RowsChanged securitylog (2701 -> 2703)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.CompName (5768061947850 -> 5774613957664)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.DateTPrevious (5796485454011 -> 5799171712283)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.DefNum (5841742043169 -> 5848959101928)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.DefNumError (5841742043169 -> 5848959101928)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.FKey (5674734116250 -> 5681951175009)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.LogDateTime (5858673777980 -> 5861581982525)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.LogSource (5841742043169 -> 5848959101928)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.LogText (5784631387789 -> 5788633885903)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.PatNum (5827962492072 -> 5835179550831)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.PermType (5759588115431 -> 5762400254393)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.SecurityLogNum (5811630809453 -> 5814862251304)  [allowed: Adoption may log its own actions.]
- ColumnChanged securitylog.UserNum (5695692550860 -> 5702909609619)  [allowed: Adoption may log its own actions.]
- RowsChanged securityloghash (2 -> 4)  [allowed: Adoption may log its own actions.]
- ColumnChanged securityloghash.LogHash (7673420916 -> 8943591418)  [allowed: Adoption may log its own actions.]
- ColumnChanged securityloghash.SecurityLogHashNum (4891611139 -> 8594418635)  [allowed: Adoption may log its own actions.]
- ColumnChanged securityloghash.SecurityLogNum (4891611139 -> 11670974803)  [allowed: Adoption may log its own actions.]
- TotalChanged patients.portal_active_email_nonnull (651 -> error: Unknown column '_portal_active_email' in 'WHERE')  [allowed: The column it counts is dropped by adoption.]

#### Adoption log (what AdoptionValidation adopt prints in runbook steps 4 and 5)

```
Open Dental 25.3.48 database. It can be adopted.
Adoption from Open Dental 25.3.48.0 started.
Dropped index patient.portal_active_email_uniq.
Dropped column patient._portal_active_email.
Deleted 4 Open Dental update file row(s).
Blanked 4 preference(s) that pointed at Open Dental's servers: UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl.
DataBaseVersion and ProgramVersion set to 99.0.0.0.
Fork markers written: OdSchemaCompat 25.3.48, ForkSchemaLevel 0.
Running fork migration steps up to level 2026101971.
Fork migration step 2026101901 Open Dental 24.4 schema found nothing to change.
Fork migration step 2026101902 Open Dental 25.1 schema found nothing to change.
Fork migration step 2026101903 Open Dental 25.2 schema found nothing to change.
Fork migration step 2026101904 Open Dental 25.3 schema found nothing to change.
Fork migration step 2026101905 Open Dental 25.3.48 keys on existing columns found nothing to change.
Fork migration step 2026101906 Open Dental 25.3.48 definitions of existing columns found nothing to change.
Fork migration step 2026101907 Open Dental 25.3 seed rows for databases from 24.3 found nothing to change.
Fork migration step 2026101951 XConnect switch for clearinghouses that used XConnect in 24.3 found nothing to change.
Fork migration step 2026101971 Report rows for Procedures, Production by Procedure and Insurance Pending Supplementals found nothing to change.
Fork migrations: 9 step(s) run, 0 already done. Fork schema level 2026101971.
The schema matches Open Dental 25.3.48 (OdSchemaCompat).
Adoption finished.
Fork migration, every step again: 9 steps run, 0 changes.
The schema matches Open Dental 25.3.48 exactly (422 of 422 tables).
Rehash: every patient, and the other tables' rows dated on or after 2026-09-24.
  appointment: 5846 rows, 551 in the rehash window (0 without a fork hash); 551 fork hashes, 4343 other, 952 blank
  claim: 3523 rows, 34 in the rehash window (0 without a fork hash); 34 fork hashes, 3064 other, 425 blank
  claimproc: 11055 rows, 334 in the rehash window (0 without a fork hash); 334 fork hashes, 8017 other, 2704 blank
  inssub: 421 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 69 other, 352 blank
  insverify: 624 rows, 17 in the rehash window (0 without a fork hash); 17 fork hashes, 607 other, 0 blank
  insverifyhist: 304 rows, 9 in the rehash window (0 without a fork hash); 9 fork hashes, 295 other, 0 blank
  patient: 1355 rows, 1355 in the rehash window (0 without a fork hash); 1355 fork hashes, 0 other, 0 blank
  payplan: 15 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 0 other, 15 blank
  paysplit: 6878 rows, 60 in the rehash window (0 without a fork hash); 60 fork hashes, 6121 other, 697 blank
Rehash finished.
```

#### Rehash record

| Table | Rows | In rehash window | Before: fork / other / blank | After: fork / other / blank | Window rows without a fork hash after |
|---|---:|---:|---|---|---:|
| appointment | 5846 | 551 | 0 / 4894 / 952 | 551 / 4343 / 952 | 0 |
| claim | 3523 | 34 | 0 / 3098 / 425 | 34 / 3064 / 425 | 0 |
| claimproc | 11055 | 334 | 0 / 8098 / 2957 | 334 / 8017 / 2704 | 0 |
| inssub | 421 | 0 | 0 / 69 / 352 | 0 / 69 / 352 | 0 |
| insverify | 624 | 17 | 0 / 624 / 0 | 17 / 607 / 0 | 0 |
| insverifyhist | 304 | 9 | 0 / 304 / 0 | 9 / 295 / 0 | 0 |
| patient | 1355 | 1355 | 0 / 573 / 782 | 1355 / 0 / 0 | 0 |
| payplan | 15 | 0 | 0 / 15 / 0 | 0 / 0 / 15 | 0 |
| paysplit | 6878 | 60 | 0 / 6181 / 697 | 60 / 6121 / 697 | 0 |

#### Open Dental-style startup check

| Database | Client | Outcome |
|---|---|---|
| adopted copy | Open Dental 25.3.48, update server | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 25.3.48, workstation | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 24.3.63, update server | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 24.3.63, workstation | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 25.4.30, update server | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 25.4.30, workstation | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 26.1.70, update server | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 26.1.70, workstation | Exits (exit code 312); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| adopted copy | Open Dental 25.3.48, classic update view, Setup.exe in A to Z | Exits (exit code 0); conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried yes |
| adopted copy | Open Dental 25.3.48, Debug build | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| unadopted copy | Open Dental 25.3.48, update server | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| unadopted copy | Open Dental 25.3.48, workstation | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| unadopted copy | Open Dental 24.3.63, update server | Exits (exit code 0); conversion no, database writes no, services installed no, binaries copied from the database yes, Setup.exe tried no |
| unadopted copy | Open Dental 24.3.63, workstation | Exits (exit code 0); conversion no, database writes no, services installed no, binaries copied from the database yes, Setup.exe tried no |
| unadopted copy | Open Dental 25.4.30, update server | PublishesUpdateAndProceeds; conversion yes, database writes yes, services installed yes, binaries copied from the database no, Setup.exe tried no |
| unadopted copy | Open Dental 25.4.30, workstation | Exits (exit code 0); conversion no, database writes no, services installed no, binaries copied from the database yes, Setup.exe tried no |
| unadopted copy | Open Dental 26.1.70, update server | PublishesUpdateAndProceeds; conversion yes, database writes yes, services installed yes, binaries copied from the database no, Setup.exe tried no |
| unadopted copy | Open Dental 26.1.70, workstation | Exits (exit code 0); conversion no, database writes no, services installed no, binaries copied from the database yes, Setup.exe tried no |
| unadopted copy | Open Dental 25.3.48, classic update view, Setup.exe in A to Z | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |
| unadopted copy | Open Dental 25.3.48, Debug build | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no |

#### Data-layer round trip

| Table | Class | Rows | 24.4-25.3 columns | Loaded | Rows saved | New values written | Result |
|---|---|---:|---:|---:|---:|---:|---|
| appointment | Appointment | 5846 | 1 | 1 | 5846 | 1 | ok |
| apptview | ApptView | 2 | 1 | 1 | 2 | 1 | ok |
| asapcomm | AsapComm | 5 | 1 | 1 | 5 | 1 | ok |
| benefit | Benefit | 370 | 1 | 1 | 370 | 1 | ok |
| branding | Branding | 3 | 4 | 4 | 3 | 4 | ok |
| chat | Chat | 3 | 1 | 1 | 3 | 1 | ok |
| chatattach | ChatAttach | 1 | 4 | 2 | 1 | 2 | ok (chatattach.Thumbnail: Binary column; the CRUD generator has no binary type and chat is excluded by owner decision (ReferenceSnapshotTests).; chatattach.FileData: Binary column; the CRUD generator has no binary type and chat is excluded by owner decision (ReferenceSnapshotTests).) |
| chatmsg | ChatMsg | 44 | 8 | 8 | 44 | 8 | ok |
| chatreaction | ChatReaction | 10 | 3 | 3 | 10 | 3 | ok |
| chatuserattach | ChatUserAttach | 10 | 5 | 5 | 10 | 5 | ok |
| chatuserod | ChatUserod | 7 | 11 | 11 | 7 | 11 | ok |
| claimpayment | ClaimPayment | 776 | 1 | 1 | 776 | 1 | ok |
| clearinghouse | Clearinghouse | 2 | 1 | 1 | 2 | 1 | ok |
| codegroup | CodeGroup | 13 | 2 | 2 | 13 | 2 | ok |
| computerpref | ComputerPref | 4 | 1 | 1 | 4 | 1 | ok |
| creditcard | CreditCard | 38 | 1 | 1 | 38 | 1 | ok |
| deletedobject | DeletedObject | 4 | 3 | 0 | 0 | 0 | ok (deletedobject: Deprecated table that 24.3 already had (DeletedObject is marked Obsolete and has no CRUD class); no code reads or writes it.) |
| document | Document | 2344 | 3 | 3 | 2344 | 3 | ok |
| eclipboardsheetdef | EClipboardSheetDef | 2 | 1 | 1 | 2 | 1 | ok |
| eformfield | EFormField | 10578 | 7 | 7 | 10578 | 7 | ok |
| eformfielddef | EFormFieldDef | 25 | 5 | 5 | 25 | 5 | ok |
| emailmessage | EmailMessage | 317 | 1 | 1 | 317 | 1 | ok |
| eobattach | EobAttach | 172 | 1 | 1 | 172 | 1 | ok |
| fee | Fee | 358 | 1 | 1 | 358 | 1 | ok |
| feeschednote | FeeSchedNote | 5 | 7 | 7 | 5 | 4 | ok |
| histappointment | HistAppointment | 3111 | 2 | 2 | 3111 | 2 | ok |
| imagedraw | ImageDraw | 836 | 1 | 1 | 836 | 1 | ok |
| inspending | InsPending | 46 | 10 | 10 | 46 | 10 | ok |
| insplan | InsPlan | 12 | 1 | 1 | 12 | 1 | ok |
| inssub | InsSub | 421 | 1 | 1 | 421 | 1 | ok |
| insverify | InsVerify | 624 | 1 | 1 | 624 | 1 | ok |
| insverifyhist | InsVerifyHist | 304 | 1 | 1 | 304 | 1 | ok |
| journalentry | JournalEntry | 537 | 3 | 3 | 537 | 3 | ok |
| medication | Medication | 15 | 1 | 1 | 15 | 1 | ok |
| payplan | PayPlan | 15 | 1 | 1 | 15 | 1 | ok |
| payplancharge | PayPlanCharge | 186 | 1 | 1 | 186 | 1 | ok |
| payplantemplate | PayPlanTemplate | 3 | 1 | 1 | 3 | 1 | ok |
| queryfilter | QueryFilter | 4 | 2 | 2 | 4 | 2 | ok |
| reqneeded | ReqNeeded | 2 | 1 | 1 | 2 | 1 | ok |
| reqstudent | ReqStudent | 2 | 1 | 1 | 2 | 1 | ok |
| schoolapproval | SchoolApproval | 4 | 13 | 13 | 4 | 11 | ok |
| schoolcourse | SchoolCourse | 2 | 4 | 4 | 2 | 4 | ok |
| schoolcoursedef | SchoolCourseDef | 2 | 2 | 2 | 2 | 2 | ok |
| schoolcoursesched | SchoolCourseSched | 6 | 8 | 8 | 6 | 8 | ok |
| sequencecounter | SequenceCounter | 1 | 2 | 2 | 1 | 2 | ok |
| sheetfield | SheetField | 440 | 1 | 1 | 440 | 1 | ok |
| statement | Statement | 4589 | 1 | 1 | 4589 | 1 | ok |
| task | Task | 75 | 2 | 2 | 75 | 2 | ok |
| taskhist | TaskHist | 75 | 2 | 2 | 75 | 2 | ok |
| transaction | Transaction | 218 | 1 | 1 | 218 | 1 | ok |

#### Stored passwords to re-enter (credential inventory of the restored copy)

- Program link synthetic filler 1: synthetic filler 1, clinic 1 [programproperty 1]
- Program link XVWeb: Password [programproperty 4]
- Clearinghouse Fictional Claims Exchange: password [clearinghouse 1]
- Preference ReportingServerMySqlPassHash [preference 25]
- HL7 definition synthetic filler 1: SFTP password [hl7def 1]
- HL7 definition synthetic filler 2: SFTP password [hl7def 2]
- Electronic billing (eBill), clinic 1: password [ebill 1]
- Electronic billing (eBill), clinic 2: password [ebill 2]
