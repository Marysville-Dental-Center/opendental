# M1 validation report: the adoption rehearsal (switch gate M1-Z)

Status: **ready for the owner's sign-off**, 2026-10-25, second revision after review (M1-Z r2). Branch `m1/m1-z-r2` (from `m1/m1-z-r1`);
build, tests, rehearsal and dry runs at commit `26f967c0b` (the commits after it change only this report and the runbook). Synthetic data
only (owner decision of 2026-10-18: no office census, no office data). Procedure for the office: [adoption-runbook.md](adoption-runbook.md). Tooling: [tools/AdoptionValidation](../tools/AdoptionValidation/README.md).

The first version of this report rested on a cutover check that had never been rehearsed: the runbook started the program on the adopted copy
before taking the post-adoption report (F8). The first revision ran the rehearsed sequence without the program, but its runbook assumed a
Windows database server, and the office's runs Ubuntu (F11); it copied the database with a dump, which rounds FLOAT values and would have
stopped a correct switch (F12); it resumed interrupted adoptions that cannot be resumed (F13); it left this program usable after a rollback
(F14); and this report claimed more than some checks showed (F15). This revision fixes them. The runbook's tool commands are now the commands
the dry run ran, in its order, through a forwarded port as on the office's admin PC. Its commands on the Ubuntu server itself are standard
ones but were not run here, because there is no Ubuntu server in this environment ("Not covered by this rehearsal").

## Verdict

The rehearsal passed, twice in a row, on the medium synthetic database (422 tables, 101,881 rows): **26 of 26 checks in each run and 3 of 3
checks across the runs**. The runbook's tool commands of steps 0.2, 1.1 and 3.2 to 6.1 (everything before the program's first start) and
the go-live checks of steps 10.1 and 10.2, run as the runbook gives them through a forwarded port on a medium synthetic database, passed
(`Dry run: PASS`), and so did the same commands on the large synthetic database (777,822 rows).

- Adoption drops `patient._portal_active_email` and its unique index, deletes Open Dental's update files, blanks Open Dental's server
  addresses, and sets the version sentinels and fork markers. The adopted schema then matches Open Dental 25.3.48 **exactly** (no documented
  exception needed).
- Nothing else changes. Row counts are the same except for the rows adoption is meant to add or delete. 410 of the 422 tables (398 that hold
  rows, and the 12 that are empty in both) are byte-identical (row count, `CHECKSUM TABLE`, every column checksum). The 12 touched tables
  differ only in the allowed ways. In the four that the allowed list can only allow whole (`preference`, `documentmisc`, `securitylog`,
  `securityloghash`), a row-by-row check against the source found only adoption's own rows (V11). All 146 business totals are unchanged,
  except the one that counts the dropped column.
- Of the 139 columns new or changed between 24.3 and 25.3.48 (50 tables), 134 load and save unchanged through OpenDentBusiness, and 129 of
  them were given a new value, written and read back. The other 5 of the 134 (feeschednote `SecUserNumEntry`, `SecDateEntry`, `SecDateTEdit`;
  schoolapproval `SecDateEntry`, `SecDateTEdit`) are set by Insert or the server, so they were loaded and saved but not given new values. Not
  loaded at all: `chatattach.Thumbnail` and `FileData` (binary) and the 3 columns of the deprecated `deletedobject` table, preserved untouched
  (checksums).
- Every Open Dental release client modelled (24.3.63, 25.3.48, 25.4, 26.1; workstation and update server; classic update view) refuses the
  adopted database without converting it, writing to it, installing services or copying binaries.
- Repeating adoption, the fork migration, the verifier and the rehash changes no other table; in `securitylog` and `securityloghash` it adds
  the rehash's own entry, one row each, and changes no existing row (I4, row by row).
- Two runs end with identical adopted databases, apart from the time of adoption: every table but `preference`, `securitylog` and
  `securityloghash` by its report, and those three row by row, where only the value of `ForkAdoptedFrom`, `securitylog.LogDateTime` and the
  `securityloghash.LogHash` computed from it differ (X2).
- The office runs what was rehearsed: the rehearsal copies with the code of `AdoptionValidation copy` (runbook step 3.2), adopts with
  `HeadlessAdoption`, the code `AdoptionValidation adopt` runs in runbook steps 4 and 5, and compares the adopted copy with the copy's report,
  as runbook step 6.1 does, before anything else touches the copy. The commands that run on the database server itself (step 3.1's folder
  copy, the rollback) are not part of this (F11, "Not covered").

One defect was found and fixed during the gate (F1 below): the rehash moved every rehashed row's audit timestamp. The review of the first
version found three defects in the procedure (F8 to F10), and the review of the first revision five more and the overstated claims (F11 to
F15); all are fixed in this revision. The owner has open decisions (section "For the owner"); none blocks the switch, and the runbook has a
path for either answer.

## Scope and method

| Task step (M1-Z) | How it was done | Evidence |
|---|---|---|
| 1. Generate a medium 25.3.48 database | `tools/SyntheticData`, medium preset (600 families, 2 years, seed 20261018, as of 2026-09-30) on the real 25.3.48 schema (`fox-db-schema.sql`, checked against `od_253_schema`), plus 2 filler rows in every table the generator leaves empty and an office fixture (see below) | G1 |
| 2. Record the validation baseline | Row counts, `CHECKSUM TABLE ... EXTENDED`, a checksum per column (FLOAT values read exactly), and 146 business totals (aging buckets, production, income, write-offs, deposits, insurance estimates, payment-plan debits, credits, payments and balances, appointments, dated fees, accounting, statements, feature counts) | `run1-1-baseline.json`; G1 |
| 3. Run the adoption tool on a copy | The FLOAT check of runbook step 0.2; the copy made on the server with the code of `AdoptionValidation copy` (runbook step 3.2: new name only, nobody connected, every table's row count and `AUTO_INCREMENT` checked); check the copy equals the baseline (its report is the runbook's `baseline-copy.json`); then `HeadlessAdoption`, the code `AdoptionValidation adopt` runs in runbook step 4: `Adoption.Preflight` and `Adoption.Run`, the calls `FormAdoptDatabase` makes (the GUI itself was not launched) | G2, A1-A4 |
| 4. Fork migration and schema verifier | In the same class: `ForkMigrationRunner.Run` with every step again (`RunAll`), `SchemaVerifier.VerifyDatabase`; then the rehash (`SecurityHash.RehashNow`, runbook step 5) and the row check against the source | M1, M2, H1 |
| 5. Validate | Compare the adopted copy with the copy's report under `allowed-differences.adoption.json` (runbook step 6.1), before anything else touches it; direct checks of the column, index, update files, addresses, sentinels and markers; the fork's guard; the Open Dental startup model; the row check of `preference`, `documentmisc`, `securitylog` and `securityloghash` (which the allowed list can only allow whole) and `updatehistory`; the data-layer round trip | V1-V11, R1, R2 |
| 6. Run it all twice | Within each run, a second pass on the adopted copy (the headless adoption again, as an operator re-running step 4 would); and the whole run twice from a fresh generation, compared run against run (the tables that hold the time of adoption row by row) | I1-I4, X1-X3 |
| 7. Time each step | Every step timed in both runs; the runbook dry run timed per step | Timings below |
| 8. Runbook | [adoption-runbook.md](adoption-runbook.md); its tool commands (steps 0.2, 1.1, 3.2 to 6.1, 10.1 and 10.2) dry-run as it gives them (`Invoke-RunbookDryRun.ps1`) through a forwarded port on a medium synthetic database, and on a small one through a relay in every test run. Its commands on the Ubuntu server and the workstations were not run here ("Not covered") | Runbook dry run below |
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
build of this branch. The dry run reached that server through a relay on `127.0.0.1:13306`, standing in for the SSH forward of runbook step
0.0. The server was shared with other test runs throughout (some 25 other connections, 3 to 14 statements running whenever it was
sampled; see "Timings"), so the times below are above what an idle server needs. All tools refuse any server other than this machine and
block outbound HTTP; no run reported a blocked request.

**Owner decisions applied.** Synthetic data only; `patient._portal_active_email` dropped by adoption; chat and dental-school features
excluded (their tables were filled with a few rows each and survive untouched: checksums and round trip); public sources only (the Open
Dental startup model follows the published GPL 24.3.63 source, not the licensed client); everything local.

## Timings

Seconds per step. Medium is the gate (two runs, with the round trip); large is one extra run without the round trip, kept as the source of
the large dry run below. The data-layer round trip and the second pass are rehearsal-only checks and are not part of the office procedure.

**The development server was busy throughout.** Other test runs shared it the whole time (some 25 other connections, most of them creating,
altering and filling their own scratch databases). The same work took very different times: generating the medium database 15.5 s in run 1
and 71.2 s in run 2 (7.6 s on the idle server of the first revision), and the server-side copy, which runs 422 `CREATE TABLE` statements,
15.0 s and 64.7 s. These figures are therefore upper bounds, not estimates, and they say little about how the time grows with the size of the
data: the large copy took 80.7 s against 15.0 to 64.7 s for the medium one, with 7.6 times the rows.

| Step | Medium, run 1 | Medium, run 2 | Large, run 1 |
|---|---:|---:|---:|
| Generate synthetic 25.3.48 database | 15.5 | 71.2 | 106.9 |
| Add office fixture | 0.1 | 0.1 | 0.1 |
| Record baseline report | 3.1 | 2.4 | 30.1 |
| FLOAT values a logical backup would round (`float-check`, runbook step 0.2) | 0.3 | 0.2 | 3.5 |
| Copy on the server (`AdoptionValidation copy`, runbook step 3.2) | 15.0 | 64.7 | 80.7 |
| Validate the copy | 4.4 | 2.7 | 15.3 |
| Credential inventory | 0.2 | 0.1 | 0.1 |
| Preflight | 0.0 | 0.0 | 0.0 |
| Adopt (`Adoption.Run`, including its fork migration steps) | 2.2 | 2.2 | 1.6 |
| Fork migration (all steps again) | 0.3 | 0.2 | 0.1 |
| Schema verifier | 1.4 | 1.2 | 0.3 |
| Rehash (`SecurityHash.RehashNow`) | 2.1 | 1.1 | 5.7 |
| Row check against the original | 0.2 | 0.1 | 0.3 |
| Record post-adoption report and compare | 5.2 | 2.6 | 17.5 |
| Open Dental-style startup check | 0.0 | 0.0 | 0.0 |
| Data-layer round trip (rehearsal only) | 40.7 | 22.5 | not run |
| Record post-round-trip report (rehearsal only) | 3.9 | 2.4 | not run |
| Second pass (rehearsal only) | 6.2 | 3.1 | 17.9 |
| **Total** | **100.8** | **176.9** | **280.1** |

For the office's tool commands (FLOAT check, copy, two reports, adoption, verifier, rehash, row check, post-adoption report) the large run
needed about 155 seconds on this busy server, 81 of them for the copy.

The office database's size is unknown (there is no census), so the runbook times the office's own dry run in the week before (step 0.6), or
plans from these figures with a larger margin (step 0.7).

## Runbook dry run

`Invoke-RunbookDryRun.ps1` runs the runbook's tool commands exactly as the runbook gives them, each as a separate program, with `-Port 13306`
as on the office's admin PC: `AdoptionValidation float-check` (step 0.2) and `connections` (1.1), `AdoptionValidation copy` (3.2),
`SyntheticData validate` for both databases and `compare` (3.3), `AdoptionValidation adopt` (4 and 5), `SyntheticData validate`,
`compare --allowed allowed-differences.adoption.json` and `AdoptionValidation startup-check` (6.1), `connections` (10.1), and a last report of
the original compared with its first one (10.2): everything before the program's first start. It ran on synthetic databases kept by a
rehearsal (`rehearse --runs 1 --keep --skip-roundtrip`, office fixture included) standing in for `opendental`, as of 2026-09-30, through a
relay on `127.0.0.1:13306` to the server's port 3306, standing in for the SSH forward of runbook step 0.0. The relay accepted 13 connections in each
run; the test suite checks that `adopt`'s business layer, the one part that used to ignore `--port`, connects through it (F11). Step 3.1, the folder copy on the database server, is bash on the server and is not part of it ("Not covered").
Seconds per step, on the busy server described under "Timings" (the copy took 51.1 s for the medium database here, against 15.0 s in
rehearsal run 1):

| Runbook step | Medium (101,881 rows) | Large (777,822 rows) |
|---|---:|---:|
| 0.2 FLOAT values a dump would round (`float-check`) | 0.5 | 1.6 |
| 1.1 No connection uses the original (`connections`) | 0.3 | 0.4 |
| 3.2 Copy on the server (`AdoptionValidation copy`) | 51.1 | 86.5 |
| 3.3 Reports of both and their comparison | 6.0 | 36.1 |
| 4 and 5 `AdoptionValidation adopt` (checks, adoption, migration again, verifier, rehash, row check) | 4.9 | 14.2 |
| 6.1 Post-adoption report, comparison, startup check | 4.1 | 23.3 |
| 10.1 No connection uses the original (`connections`) | 0.4 | 0.5 |
| 10.2 The original unchanged | 3.0 | 26.6 |
| **Total** | **70.3** | **189.2** |
| Result | `Dry run: PASS` | `Dry run: PASS` |

In both: `float-check` found no FLOAT or DOUBLE value a dump would round (164 columns in 74 tables); no connection used the original before
or after; `copy` copied the 422 tables and checked "every table's row count and AUTO_INCREMENT equal" the original's; the reports of the
original and the copy were identical (0 differences); `adopt` found the copy had the original's 422 tables and row counts and newer tables,
and printed "Adoption finished.", "Fork migration, every step again: 9 steps run, 0 changes.", "The schema matches Open Dental 25.3.48 exactly
(422 of 422 tables).", "Rehash finished.", the five lines of the row check (5 preferences added, 2 set to 99.0.0.0, 4 blanked, no other
row changed or removed; 4 update-file rows deleted and nothing else; 2 security log entries added, 1 adoption and 1 rehash, no existing row
changed; 2 `securityloghash` rows for them; `updatehistory` identical) and "Adopt: PASS"; the post-adoption comparison found
`Unexpected differences: 0` (50 allowed, counting the per-table `CHECKSUM TABLE` lines, all of the kinds runbook step 6.1 lists); the startup
check ended "Every Open Dental release client refuses this database." (exit code 0); and the original's last report equalled its first one (0
differences). No backup file was written: the copy is made by the server. The test suite runs the same script out of process on a small
database through a relay on a random port in every run (`RunbookDryRun_PassesThroughAForwardedPort_WithTheRunbookCommands`), then runs `adopt`
again through the relay on the adopted copy, which adds one security log entry and changes nothing else.

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
program's first start (6.2), and the rehearsal compares with the copy's report exactly as 6.1 does. The allowed list is unchanged:
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
code; a test feeds it a real `--databases` dump and checks that it refuses and that the database the dump names is untouched. (Superseded in
the second revision: the runbook restores no backup file at all any more. The preparation and the day copy the database on the server
itself, F11 and F12. `restore` and its tests remain.)

**F10. Fixed (review): nothing enforced or checked that all workstations switch together.** The first runbook allowed removing only the Open
Dental shortcuts (step 2.1), which leaves `OpenDental.exe` and `FreeDentalConfig.xml` pointing at `opendental`. That database stays live and
unadopted, and an Open Dental 25.3.48 client opens it normally (check A2: "Proceeds"). No go-live step re-checked the connections to
`opendental` or compared it with its baseline, and step 11 then dropped it: work entered in Open Dental after the backup would have been lost
without notice. Fix: step 2.1 requires uninstalling Open Dental and renaming its `FreeDentalConfig.xml` on every workstation of a list made
in step 0.5, and revoking the workstations' MariaDB account on `opendental` where it has its own grant; step 8 has a sign-off table per
workstation; before going live (10.1, 10.2) and again before dropping the original (11.1), no connection may use `opendental` and its report
must equal `baseline-original.json` with no allowed differences. Any difference is treated as lost work, with a procedure to find it in the
security log and re-enter it. The dry run runs these server-side checks.

**F11. Fixed (second review): the runbook was written for a Windows database server; the office's runs Ubuntu.** The first revision ran every
command "as administrator on the database server" in PowerShell, with the tools copied there and `<mariadb>` in `C:\Program Files\MariaDB
10.11\bin`. The office schema dump the project works from reads "Host: fox-db" and "Server version 10.11.14-MariaDB-0ubuntu0.24.04.1"
(`D:/Dev/opendental-schema/fox-db-schema.sql`, lines 4 to 6): Ubuntu 24.04's MariaDB package, dumped from a Windows client. The tools are .NET
Framework x86 programs and cannot run there, and they refuse every server but the computer they run on (`LocalOnly`, deliberately without an
override), so restore, the reports, adopt, the startup check and the dry run could not have run as written; Ubuntu's `root@localhost` also
logs in only through the local socket, which the tools cannot use. Nothing stated or checked the platform; on the day the operator would have
been left, after Open Dental was removed everywhere, to improvise (adopting through the program, which brings back F8, or copying office data
to a PC). Fix: runbook step 0.0 records the platform read-only (`lsb_release`, `@@version`, `@@version_compile_os`, `@@datadir`, `@@port`,
the table engines, routines, triggers, events and views) and stops on a server that is not Linux and on any table that is not MyISAM. The
commands on the server are bash (`sudo mariadb`, `systemctl`, `cp`, `diff`); the tools run on a Windows admin PC and reach the server's MariaDB through an SSH
local forward (`ssh -N -L 127.0.0.1:13306:127.0.0.1:3306`), as a dedicated account with a password (`adoption_admin`, created in step 0.0.2,
dropped in step 11.2), so `LocalOnly` stays meaningful: the forward is the one visible way to the server. No copy of the office database is
made off the server: the copy is made by the server (F12). The tools on the admin PC read through the forward only what they work on (`adopt`
reads the preferences it changes and, for the rehash, the keys, dates, amounts and statuses it hashes; `credentials` reads the rows that hold
stored passwords) and write only reports, logs and the password locations to its disk. Two defects of the tools came out of this, and are
fixed with tests:
- `BusinessLayer.Connect` passed the host without the port to `DataConnection`, which then uses 3306. Through a forward on another port,
  `adopt`'s business layer (the adoption itself, the verifier, the rehash) would have connected to port 3306 of the admin PC, not to the
  server its own checks had looked at. It now passes `host:port` (`BusinessLayer_PassesThePortToDataConnection`,
  `BusinessLayer_ConnectsThroughTheGivenPort`).
- Every "nobody is connected" check read `information_schema.PROCESSLIST`, which shows other accounts' connections only to an account with the
  `PROCESS` privilege. With a narrower account than `root` the checks would have passed whoever was connected. They now refuse such an
  account (`EnsureSeesAllConnections`; `ConnectionChecks_NeedThePROCESSPrivilege`).

The dry run ran in this topology: the tools on the PC through a relay on port 13306 standing in for the forward (a test does the same on a
random port in every run). What could not run here, because there is no Ubuntu server in this environment, is listed under "Not covered".

Three more gaps of the new procedure were closed before sign-off. The Open Dental services stay connected to `opendental` day and night, so
`connections`, `copy` and `adopt` would have refused on the preparation night: step 0.6 now stops the services first and starts them again
the same night, and on the day step 1.1 expects their connections until step 2 stops them (step 3.1 then requires none). The forward
arrives at `127.0.0.1` on `fox-db`, so step 0.0.1 now checks that MariaDB listens there (`@@bind_address`) and which port to forward to.
And the tools do read some rows through the forward while they work (the rehash's keys, dates, amounts and statuses), so the runbook and
this report now say that no copy of the database is made off the server, not that no row leaves it.

**F12. Fixed (second review, carry-over C5): a logical backup rounds FLOAT values, and the cutover check would then have stopped a correct
switch.** MariaDB writes a FLOAT as text with 6 significant digits, so `mariadb-dump` with the runbook's options turns 0.1234567 into
0.123457 and 34.285713 into 34.2857 (measured on 10.11.19). The validator's column checksum, a CRC of the value as text, printed both sides
with the same 6 digits and could not see it; only `CHECKSUM TABLE` could, and `compare` reports a `CHECKSUM TABLE` change with no other
difference in its table as unexpected (exit code 5). The office schema has 31 FLOAT columns, and Open Dental computes at least one with more
than 6 digits (`imagedraw.FontSize`, `OpenDental/User Controls/ControlImageDisplay.cs` lines 2632 and 2809). So the first revision's step 3.3
(backup, restore, compare) would have stopped a correct switch after Open Dental had been removed everywhere, and its backup and adopted copy
would have carried rounded values. DOUBLE values are written exactly (17 digits: 0.1 + 0.2 comes back as 0.30000000000000004, measured).
Fix:
- The copy that is adopted is made by the server (`AdoptionValidation copy`, runbook step 3.2: `CREATE TABLE ... LIKE`, the `AUTO_INCREMENT`
  counters, `INSERT ... SELECT` of every stored column in key order), which copies the stored values themselves. It refuses an existing
  name, a connection, and views, triggers, routines or events it would not carry, and checks every table's row count and counter afterwards.
- The rollback point is a folder copy of the MyISAM database with MariaDB stopped, checked byte for byte with `diff -rq` (step 3.1).
- `AdoptionValidation float-check` counts the values a dump would round (`c <> CAST(CAST(c AS CHAR) AS FLOAT)`, and the same for DOUBLE): runbook
  step 0.2 and the dry run. On the day nothing depends on it; it tells the owner what the office's nightly backups would round if they are
  dumps.
- The validator's column checksum reads a FLOAT widened to DOUBLE, which is exact, so a rounded value now shows as a `ColumnChanged` line for
  its column. `compare` adds a note to a `CHECKSUM TABLE`-only difference ("every value reads the same, but the stored rows differ"), and
  runbook step 3.3 says what it means and that the owner decides with the developers, never during the outage.

Tests: `Copy_IsExact_WhereADumpRoundsFloatValues` (two long values and a short one in a FLOAT column: `float-check` finds exactly the two,
the server-side copy keeps them and every `AUTO_INCREMENT`, a dump and restore rounds them and the comparison names the column) and
`ColumnChecksums_SeeAFloatRoundedTo6Digits`. The synthetic data's FLOAT values are short, so the rehearsal itself finds none (G2). Inference
from the code, for the owner: programs read FLOAT values through the same 6-digit text (Open Dental writes them with `SOut.Float`, 7 digits,
and reads back what the server prints), so a rounded value shows the same in the program; only the stored bits and server-side sums differ.

**F13. Fixed (second review, carry-over C1): an interrupted adoption can be resumed only before its markers are written.** The first revision
said every step can be repeated and an interrupted adoption is finished by running `adopt` again. The reviewer interrupted `Adoption.Run`
after each step on synthetic copies: after steps 2 to 5 a rerun passed and matched an uninterrupted adoption; after step 6 (`ForkSchemaLevel`
written) every rerun ended "Adopt: FAIL" (exit code 6), and a process killed inside a fork migration step exited 1 on every rerun. It fails
closed, but the runbook relied on it. Fix: the runbook never resumes: after "Adopt: STOPPED" or "Adopt: FAIL" it drops the copy and makes a new
one from the untouched original (step 4), which takes minutes; `adopt`'s messages, its usage text and `release-process.md` say so. Test
`InterruptedAdoption_IsFinishedBeforeTheMarkers_FailsClosedAfter_AndANewCopyAdopts` cuts an adoption off after step 3 (a rerun finishes it,
and its report equals that of run 1's adopted copy in every table but the three that hold the time of adoption) and in step 7 (`adopt` fails
and asks for a new copy; a new copy then adopts).

**F14. Fixed (second review): after a rollback, this program still opened the adopted database.** Rollback step 1 only said to close this
program, and step 5 kept `opendental_pms` "renamed or dumped" (MariaDB cannot rename a database). Every workstation kept this program and a
`FreeDentalConfig.xml` pointing at `opendental_pms`, which opens normally, so work entered there the next day would have gone to a database the
office no longer reads, and nothing would have noticed. Fix: Rollback step 1 removes this program from every workstation with a sign-off table;
step 2 checks that nobody is connected to `opendental_pms`, lists the work entered in it since the switch (it stays on the server), dumps it
and only then drops it, and revokes the workstations' grants on it. Carry-over C6 is fixed in the same place: before the folder copy is
restored over a modified original, the modified original is moved aside, out of the data folder, so it can be analysed.

**F15. Fixed (second review, carry-overs C2, C3 and C4): claims that went beyond the checks, and checks that allowed whole tables.**
- R1 said every new or changed column "loads, saves and writes". Of the 139 columns, 134 were loaded and saved and 129 were given a new value;
  5 are set by Insert or the server and were not (feeschednote `SecUserNumEntry`, `SecDateEntry`, `SecDateTEdit`; schoolapproval
  `SecDateEntry`, `SecDateTEdit`). The check is renamed and its detail lists both groups (verdict and appendix).
- X2 and I4 skipped `preference`, `securitylog` and `securityloghash` entirely, while their text said "identical apart from the adoption
  time". They now compare these tables row by row (`RowComparer`, on the server), from a snapshot taken right after adoption: across runs only
  `ForkAdoptedFrom`'s value, `securitylog.LogDateTime` and the `securityloghash.LogHash` computed from it differ; in the second pass,
  `preference` is identical and the two log tables only gained the rerun's entry. "410 of 422 tables byte-identical" now says that 12 of them
  are empty in both.
- `allowed-differences.adoption.json` allowed any change in `preference`, `documentmisc`, `securitylog`, `securityloghash` and
  `updatehistory`. Adoption does not touch `updatehistory`, so its rule is gone. The other four a report can only allow whole, so `adopt` now
  ends with a row check against the original (`AdoptionRowCheck`, rehearsal check V11): only the markers added, the version sentinels set and
  addresses of Open Dental's servers blanked in `preference`; only Open Dental's update files deleted from `documentmisc`; only adoption's and
  the rehash's entries added to `securitylog` and `securityloghash`, with no existing row changed; `updatehistory` identical. Runbook step 4
  has the operator check these lines and initial the "Blanked" list, and step 6.1 has the second person read the Allowed section of
  `compare.txt`. Negative control: `RowCheck_CatchesChangesTheWholeTableRulesAllow`.
- Step 2 stopped only services named `OpenDent*`. It now also stops and removes every service whose program path names an Open Dental
  file, and expects both inventory queries of step 0.3 to come back empty.

**F16. Not changed (carry-over C7, performance only): `SecurityHash.KeepTimestamps`.** On a connection made from a connection string
(`DataConnection.GetDatabaseName()` empty) it asks the server for the current database once per hashed row, and its cache of each table's ON
UPDATE columns is never cleared in the process. Neither affects correctness on the day: `adopt` connects with `SetDb` and a database name, so
there is no extra query, and the fork migration runs before the first hash is written, so the cache holds the migrated columns. The program
itself also connects with a database name. Left for a later clean-up.

## Not covered by this rehearsal

- **Office data.** By owner decision, there is no census and no copy of office data. Volumes, enum values actually in use, bridges in use, the
  FLOAT values a dump would round, and the real password list come from the runbook's preparation on the office server: the FLOAT check (step
  0.2) and the dry run of the tool commands on a copy made on the server (step 0.6) if the owner agrees (item 3 below), otherwise the outage
  itself (step 0.7).
- **The database server's own commands.** There is no Ubuntu server in this environment, so the commands the runbook runs on `fox-db` itself
  were not run: the platform check and the account of step 0.0, the folder copy of step 3.1 with MariaDB stopped (`systemctl`, `cp -a`,
  `diff -rq`), the listing, dump and drop of Rollback step 2, the folder restore of Rollback step 3, and the drops of steps 0.6.4 and 11.2. They
  are standard commands, each with an expected output and a stop when it is missing. They first run on `fox-db` in step 0.6 (folder copy and
  dry run, with the owner's agreement) or in step 0.7's tools and server check (synthetic data), and otherwise on the day.
- **The SSH forward and the tools' account.** The dry run reached MariaDB through a relay on port 13306 of the same PC, not through `ssh`, and as
  `root`, not `adoption_admin`; the `PROCESS` check is unit-tested and `root` passes it. Step 0.0.4 checks the forward and the account on the
  office's admin PC before anything depends on them. The network between the admin PC and `fox-db` carries the reports and the rows the
  tools read (the rehash's above all), not the copy; it adds time to every tool command (not measured).
- **Golden totals from Open Dental 25.3.48** (backlog M1-Z work item 2, R2 item 7). Comparing the fork's reports with Open Dental's own
  needs the licensed client and office data. The rehearsal proves instead that adoption changes no total. The runbook adds a comparison with
  the office's last Open Dental aging report.
- **The program's own start.** The copy, the adoption, the rehash and the post-adoption comparison do not go through the program: the office
  runs the same code from the command line, in the rehearsed order, before anyone starts the program (runbook steps 3.2 to 6.1), and those
  commands were dry-run as the runbook gives them. Not exercised here (launching `OpenDental.exe` was not allowed in this run): the first start,
  log-in, smoke test and publishing (steps 6.2 and 8), which come after the gate. Their writes (this computer's row and heartbeat, the
  Database Maintenance list, signals, log-ins, security log) are normal use; no comparison covers them, and none needs to. The Adopt Database
  window and Database Maintenance's Security Hashes tool call the same code but are not used on the day.
- **The workstations.** Removing Open Dental from each workstation (step 2.1), switching it (step 8) and, in a rollback, removing this program
  again (Rollback step 1) are office work. The checks that catch a forgotten one (steps 10.1, 10.2 and 11.1) are part of the dry run.
- **Full-size timing.** The rehearsal databases are far smaller than the office's, and the development server was shared with other test runs
  while these figures were measured (section "Timings"). The office server's own times come from its dry run (step 0.6), or the window is
  planned from these figures with a 200% margin (step 0.7).

## For the owner

1. **Sign-off of this report**, which opens the switch (M1-Z exit criterion).
2. Decide whether to confirm the lock-out once with the licensed Open Dental 25.3.48 client on a synthetic adopted copy (F3; R1 item 5).
3. Decide whether the preparation may make a copy of the office database on the office server, outside office hours, to list the passwords,
   time the steps and dry-run the tool commands on real data (runbook step 0.6). This is office data on office hardware, not a development
   copy; the copy is made by the server and stays there, and it is dropped the same night. The Open Dental services are stopped while it
   runs (they would count as connected), so eServices wait until they are started again the same night. If not, runbook step 0.7 applies: the password list is made during the outage, and the window
   is planned from the rehearsal with a larger margin.
4. Agree the rollback deadline (runbook step 0.9).
5. Decide whether the rollback point (runbook step 3.1, a folder copy of the database on `fox-db`) may also be copied to the office's backup
   disk. Without that second copy it is on the server only: it protects against a failed adoption and against mistakes, not against losing the
   server's disk during the switch (the office's own nightly backups still do).
6. For information: the FLOAT check (runbook step 0.2, F12) tells you whether the office's nightly backups, if they are `mariadb-dump` files,
   restore every FLOAT value exactly. The switch does not depend on the answer.

| | Name | Decision | Date | Signature |
|---|---|---|---|---|
| Owner | | Approve the switch / not yet | | |
| Owner | | Preparation dry run (item 3): yes / no | | |
| Owner | | Rollback point also on the backup disk (item 5): yes / no | | |
| Administrator | | Runbook read and understood | | |

## Build and test results

Build (at `26f967c0b`): `MSBuild OpenDental.Local.slnf -restore` (Debug, x86): 0 errors and 376 warnings, the baseline of the first build of
the review, so this revision adds none. `tools/Test-NoHqHosts.ps1` passed (5,997 files in 47 projects).

Tests: every test assembly under the solution's `bin\Debug` folders, run with `vstest.console.exe /Platform:x86` on the build of
`26f967c0b`. All passed, none skipped or inconclusive, 1,836 tests in total. The 24 assemblies outside `tools` contain no code changed by
this revision. AdoptionValidation.Tests (44) has 10 new tests: the business layer's port, the `PROCESS` check, the FLOAT check's expression
and the new commands' arguments (4, no server), and with the server: the business layer through a relay, `copy` refusing and creating
nothing, `connections` while someone is connected, `copy` exact where a dump rounds FLOAT values, the row check's negative control, and an
adoption cut off before and after its markers; the dry run test now runs through a relay on another port. On the shared server it needed
838 s; in the run of all 26 assemblies, three at a time, the test script stopped it at its 20-minute limit per assembly, after 43 results (none
failed) and before its cleanup had dropped all of its scratch databases, so it was run again on its own: 44 of 44 passed. SyntheticData.Tests (52)
has 3 new tests (FLOAT values read exactly, against a database and as SQL, and no rule for `updatehistory`).

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
| AdoptionValidation.Tests | 44 |
| SyntheticData.Tests | 52 |

## How to reproduce

```powershell
& "C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe" OpenDental.Local.slnf -restore `
  -p:RestorePackagesConfig=true -p:Configuration=Debug -p:Platform=x86 -m -nodeReuse:false -v:minimal
$env:ADOPTION_REHEARSAL_COMMIT=(git rev-parse --short HEAD)
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 2 --out C:\Temp\m1z
# The runbook dry run, on a synthetic database kept by a rehearsal, through a forwarded port as on the admin PC. Any TCP forward from
# 127.0.0.1:13306 to the local MariaDB stands in for the SSH forward of runbook step 0.0; this uses the test project's relay, which is x86,
# so run these lines in Windows PowerShell (x86), %SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe:
tools\AdoptionValidation\bin\Debug\AdoptionValidation.exe rehearse --size medium --runs 1 --keep --skip-roundtrip --tag dry --out C:\Temp\m1z-dry-src
$relayAssembly=[Reflection.Assembly]::LoadFrom("$PWD\tools\AdoptionValidation\AdoptionValidation.Tests\bin\Debug\AdoptionValidation.Tests.dll")
$relay=[Activator]::CreateInstance($relayAssembly.GetType('AdoptionValidation.Tests.TcpRelay',$true),[object[]]@(3306,13306))
.\tools\AdoptionValidation\Invoke-RunbookDryRun.ps1 -Original synth_m1z_dry_r1_src -Copy synth_m1z_dry_pms -Folder C:\Temp\m1z-dry -AsOf 2026-09-30 -Port 13306
$relay.Dispose()
# Then drop synth_m1z_dry_r1_src, synth_m1z_dry_r1_adopted, synth_m1z_dry_r1_snap and synth_m1z_dry_pms.
vstest.console.exe tools\AdoptionValidation\AdoptionValidation.Tests\bin\Debug\AdoptionValidation.Tests.dll /Platform:x86
```

The same seed gives the same database, so the row counts, checksums and totals below repeat exactly; only timings, scratch names and the
adoption times differ, and so do the rehash's counts, because its window starts one month before the day it runs (`SecurityHash.GetHashingDate`):
the rows it hashes, and with them the `SecurityHash` checksums, depend on that day. (The runs for this report used
`--prefix synth_rvf60913_` for their scratch names.)

## Appendix: the rehearsal's own results (medium, two runs)

Generated by `AdoptionValidation rehearse` (`rehearsal-result.md`), unedited apart from the heading levels.

Overall: **PASS**. 2 runs, size medium, seed 20261018, as of 2026-09-30, filler rows 2, server 10.11.19-MariaDB, started 2026-10-25 08:27:47Z, commit 26f967c0b.

### Step timings (seconds)

| Step | Run 1 | Run 2 |
|---|---:|---:|
| Generate synthetic 25.3.48 database | 15.5 | 71.2 |
| Add office fixture (update files, server addresses, stored passwords) | 0.1 | 0.1 |
| Record baseline report | 3.1 | 2.4 |
| FLOAT values a logical backup would round (float-check) | 0.3 | 0.2 |
| Copy on the server (AdoptionValidation copy) | 15.0 | 64.7 |
| Validate the copy | 4.4 | 2.7 |
| Credential inventory | 0.2 | 0.1 |
| Preflight | 0.0 | 0.0 |
| Adopt (Adoption.Run, including its fork migration steps) | 2.2 | 2.2 |
| Fork migration (all steps again) | 0.3 | 0.2 |
| Schema verifier | 1.4 | 1.2 |
| Rehash (SecurityHash.RehashNow) | 2.1 | 1.1 |
| Row check against the original | 0.2 | 0.1 |
| Record post-adoption report and compare | 5.2 | 2.6 |
| Open Dental-style startup check | 0.0 | 0.0 |
| Data-layer round trip (load and save through OpenDentBusiness) | 40.7 | 22.5 |
| Record post-round-trip report | 3.9 | 2.4 |
| Second pass (adoption, migration, verifier, rehash, row check, report) | 6.2 | 3.1 |
| **Total** | **100.8** | **176.9** |

### Run 1: PASS

Source synth_rvf60913_med_r1_src (422 tables, 101,881 rows; seed=20261018;size=medium;families=600;years=2;asOf=2026-09-30;tool=SyntheticData 1), adopted copy synth_rvf60913_med_r1_adopted (copy by the runbook's server-side copy, AdoptionValidation copy).

| Check | Result | Detail |
|---|---|---|
| G1 Generated database is consistent | PASS | 422 tables, 101,881 rows, 410 tables with rows; coverage 72 of 72 24.4-25.3 items; 15 consistency checks, 0 failed |
| G2 The copy equals the baseline | PASS | row counts, CHECKSUM TABLE, every column checksum (FLOAT values exactly) and every total equal; float-check of the source: 164 FLOAT and DOUBLE column(s) in 74 table(s) checked; no value that a logical backup (mariadb-dump) would round |
| A1 Fork startup guard refuses the unadopted copy | PASS | NeedsAdoption (exit code 171) |
| A2 Startup model: an Open Dental 25.3.48 client uses the unadopted copy (control) | PASS | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no; a 25.4.30 update server: PublishesUpdateAndProceeds; conversion yes, database writes yes, services installed yes, binaries copied from the database no, Setup.exe tried no |
| A3 Preflight accepts the copy | PASS | Open Dental 25.3.48 database. It can be adopted. |
| A4 Adoption finished | PASS | dropped column True, indexes portal_active_email_uniq, 4 update-file rows deleted, 4 preferences blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl) |
| M1 Fork migration changes nothing on the adopted copy | PASS | 9 steps run (2026101901, 2026101902, 2026101903, 2026101904, 2026101905, 2026101906, 2026101907, 2026101951, 2026101971), 0 changes |
| M2 Schema matches Open Dental 25.3.48 exactly | PASS | 422 of 422 tables; 0 differences, 0 mismatches (no documented exception needed) |
| H1 Rehash gives every patient, and every row in the verified window, a fork hash | PASS | rehash window from 2026-09-25; appointment: 5846 rows, 542 in the rehash window (0 without a fork hash); 542 fork hashes, 4352 other, 952 blank; claim: 3523 rows, 26 in the rehash window (0 without a fork hash); 26 fork hashes, 3072 other, 425 blank; claimproc: 11055 rows, 285 in the rehash window (0 without a fork hash); 285 fork hashes, 8037 other, 2733 blank; inssub: 421 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 69 other, 352 blank; insverify: 624 rows, 15 in the rehash window (0 without a fork hash); 15 fork hashes, 609 other, 0 blank; insverifyhist: 304 rows, 8 in the rehash window (0 without a fork hash); 8 fork hashes, 296 other, 0 blank; patient: 1355 rows, 1355 in the rehash window (0 without a fork hash); 1355 fork hashes, 0 other, 0 blank; payplan: 15 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 0 other, 15 blank; paysplit: 6878 rows, 53 in the rehash window (0 without a fork hash); 53 fork hashes, 6128 other, 697 blank; rehash recorded in the security log |
| V1 Row counts unchanged except where adoption deletes or adds rows | PASS | 422 tables before, 422 after; 101,881 rows before, 101,886 after; changed: documentmisc 5 -> 1, preference 25 -> 30, securitylog 2701 -> 2703, securityloghash 2 -> 4 |
| V2 Checksums of untouched tables identical | PASS | 410 of 422 tables (398 that hold rows and 12 empty in both) have identical row counts, CHECKSUM TABLE and column checksums; touched (each by an allowed adoption change): appointment, claim, claimproc, documentmisc, insverify, insverifyhist, patient, payplan, paysplit, preference, securitylog, securityloghash |
| V3 Every difference is an allowed adoption difference | PASS | 38 allowed, 0 unexpected |
| V4 Business totals unchanged (aging, production, income, payment plans and the rest) | PASS | 145 of 146 totals identical; changed: TotalChanged patients.portal_active_email_nonnull (651 -> error: Unknown column '_portal_active_email' in 'WHERE')  [allowed: The column it counts is dropped by adoption.] |
| V5 patient._portal_active_email and its unique index dropped | PASS | column gone, index portal_active_email_uniq gone; two active patients may now share an e-mail address |
| V6 Open Dental UpdateFiles rows removed | PASS | 4 UpdateFiles/UpdateFilesSegment rows before, 0 after; other documentmisc rows kept: 1 of 1 |
| V7 Open Dental server addresses blanked, other preferences kept | PASS | 4 address preferences, 0 still set; vendor address kept True; help text kept True |
| V8 Sentinel versions and fork markers set | PASS | DataBaseVersion 99.0.0.0, ProgramVersion 99.0.0.0, ForkSchemaLevel 2026101971 (latest step 2026101971), OdSchemaCompat 25.3.48, ForkAdoptedFrom source=25.3.48.0;started=2026-10-25T08:28:27Z;adopted=2026-10-25T08:28:29Z |
| V9 Fork startup guard opens the adopted copy | PASS | ProceedFork, pending migrations False |
| V10 Startup model: every Open Dental release client refuses the adopted copy | PASS | 9 of 9 release-client scenarios exit without converting, writing, installing services or copying binaries;  (a Debug build of Open Dental skips its version check: Proceeds; Open Dental ships Release builds only) |
| V11 Row by row, adoption changed only its own rows of preference, documentmisc, securitylog, securityloghash and updatehistory | PASS | preference: 5 added (ForkAdoptedFrom, OdSchemaCompat, ForkSchemaLevel, ForkMigrationInProgress, ForkMigrationsApplied); 2 set to 99.0.0.0 (DataBaseVersion, ProgramVersion); 4 blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl); no other row changed or removed. documentmisc: 4 Open Dental update-file row(s) deleted, none left; 0 other row(s) deleted; 0 added; 0 changed. securitylog: 2 entries added (1 adoption, 1 rehash); no existing row changed or removed. securityloghash: 2 added, each for an added security log entry; no existing row changed or removed. updatehistory: identical row by row. |
| R1 Every 24.4-25.3 column loads and saves through OpenDentBusiness; every writable one writes | PASS | 50 tables, 139 columns new or changed since 24.3: 134 loaded and saved unchanged (32,465 rows saved), and 129 of them given a new value, written and read back; 5 loaded and saved but not given a new value, because Insert or the server sets them (CRUD types TimeStamp, DateEntry, DateTEntry, ExcludeFromUpdate: feeschednote.SecUserNumEntry, feeschednote.SecDateEntry, feeschednote.SecDateTEdit, schoolapproval.SecDateEntry, schoolapproval.SecDateTEdit); 5 not loaded, documented exceptions preserved untouched (chatattach.Thumbnail, chatattach.FileData, deletedobject (3 columns)); first-save normalizations (same meaning in the program): benefit.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (363 cells); benefit.ToothRange: NULL saved as '' (363 cells); computerpref.ComputerOS: blank or unknown enum text saved as 'Win32S' (4 cells); document.DateTStamp: ON UPDATE timestamp of a row the save normalized (2344 cells); document.ExternalSource: blank or unknown enum text saved as 'None' (2344 cells); insplan.GroupNum: NULL saved as '' (2 cells); insplan.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (2 cells) |
| R2 Round trip leaves the database unchanged apart from the first-save normalizations | PASS | only the normalized columns changed: benefit.SecDateTEdit; benefit.ToothRange; computerpref.ComputerOS; document.DateTStamp; document.ExternalSource; insplan.GroupNum; insplan.SecDateTEdit |
| I1 A second adoption is refused | PASS | This database is already adopted (source=25.3.48.0;started=2026-10-25T08:28:27Z;adopted=2026-10-25T08:28:29Z). Nothing to do. |
| I2 A second fork migration changes nothing and the schema still matches | PASS | adoption skipped (already adopted); 9 steps run, 0 changes; verifier 0 differences; row check against the source passed |
| I3 A second rehash changes nothing | PASS | every hash already current: appointment 542, claim 26, claimproc 285, inssub 0, insverify 15, insverifyhist 8, patient 1355, payplan 0, paysplit 53 |
| I4 The whole second pass leaves the database unchanged | PASS | every table except securitylog and securityloghash has the row counts and checksums of the first pass; row by row against the snapshot taken after adoption, preference is identical, and securitylog and securityloghash only gained the rerun's own entry (1 and 1 row), with no existing row changed or removed |

### Run 2: PASS

Source synth_rvf60913_med_r2_src (422 tables, 101,881 rows; seed=20261018;size=medium;families=600;years=2;asOf=2026-09-30;tool=SyntheticData 1), adopted copy synth_rvf60913_med_r2_adopted (copy by the runbook's server-side copy, AdoptionValidation copy).

| Check | Result | Detail |
|---|---|---|
| G1 Generated database is consistent | PASS | 422 tables, 101,881 rows, 410 tables with rows; coverage 72 of 72 24.4-25.3 items; 15 consistency checks, 0 failed |
| G2 The copy equals the baseline | PASS | row counts, CHECKSUM TABLE, every column checksum (FLOAT values exactly) and every total equal; float-check of the source: 164 FLOAT and DOUBLE column(s) in 74 table(s) checked; no value that a logical backup (mariadb-dump) would round |
| A1 Fork startup guard refuses the unadopted copy | PASS | NeedsAdoption (exit code 171) |
| A2 Startup model: an Open Dental 25.3.48 client uses the unadopted copy (control) | PASS | Proceeds; conversion no, database writes no, services installed no, binaries copied from the database no, Setup.exe tried no; a 25.4.30 update server: PublishesUpdateAndProceeds; conversion yes, database writes yes, services installed yes, binaries copied from the database no, Setup.exe tried no |
| A3 Preflight accepts the copy | PASS | Open Dental 25.3.48 database. It can be adopted. |
| A4 Adoption finished | PASS | dropped column True, indexes portal_active_email_uniq, 4 update-file rows deleted, 4 preferences blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl) |
| M1 Fork migration changes nothing on the adopted copy | PASS | 9 steps run (2026101901, 2026101902, 2026101903, 2026101904, 2026101905, 2026101906, 2026101907, 2026101951, 2026101971), 0 changes |
| M2 Schema matches Open Dental 25.3.48 exactly | PASS | 422 of 422 tables; 0 differences, 0 mismatches (no documented exception needed) |
| H1 Rehash gives every patient, and every row in the verified window, a fork hash | PASS | rehash window from 2026-09-25; appointment: 5846 rows, 542 in the rehash window (0 without a fork hash); 542 fork hashes, 4352 other, 952 blank; claim: 3523 rows, 26 in the rehash window (0 without a fork hash); 26 fork hashes, 3072 other, 425 blank; claimproc: 11055 rows, 285 in the rehash window (0 without a fork hash); 285 fork hashes, 8037 other, 2733 blank; inssub: 421 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 69 other, 352 blank; insverify: 624 rows, 15 in the rehash window (0 without a fork hash); 15 fork hashes, 609 other, 0 blank; insverifyhist: 304 rows, 8 in the rehash window (0 without a fork hash); 8 fork hashes, 296 other, 0 blank; patient: 1355 rows, 1355 in the rehash window (0 without a fork hash); 1355 fork hashes, 0 other, 0 blank; payplan: 15 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 0 other, 15 blank; paysplit: 6878 rows, 53 in the rehash window (0 without a fork hash); 53 fork hashes, 6128 other, 697 blank; rehash recorded in the security log |
| V1 Row counts unchanged except where adoption deletes or adds rows | PASS | 422 tables before, 422 after; 101,881 rows before, 101,886 after; changed: documentmisc 5 -> 1, preference 25 -> 30, securitylog 2701 -> 2703, securityloghash 2 -> 4 |
| V2 Checksums of untouched tables identical | PASS | 410 of 422 tables (398 that hold rows and 12 empty in both) have identical row counts, CHECKSUM TABLE and column checksums; touched (each by an allowed adoption change): appointment, claim, claimproc, documentmisc, insverify, insverifyhist, patient, payplan, paysplit, preference, securitylog, securityloghash |
| V3 Every difference is an allowed adoption difference | PASS | 38 allowed, 0 unexpected |
| V4 Business totals unchanged (aging, production, income, payment plans and the rest) | PASS | 145 of 146 totals identical; changed: TotalChanged patients.portal_active_email_nonnull (651 -> error: Unknown column '_portal_active_email' in 'WHERE')  [allowed: The column it counts is dropped by adoption.] |
| V5 patient._portal_active_email and its unique index dropped | PASS | column gone, index portal_active_email_uniq gone; two active patients may now share an e-mail address |
| V6 Open Dental UpdateFiles rows removed | PASS | 4 UpdateFiles/UpdateFilesSegment rows before, 0 after; other documentmisc rows kept: 1 of 1 |
| V7 Open Dental server addresses blanked, other preferences kept | PASS | 4 address preferences, 0 still set; vendor address kept True; help text kept True |
| V8 Sentinel versions and fork markers set | PASS | DataBaseVersion 99.0.0.0, ProgramVersion 99.0.0.0, ForkSchemaLevel 2026101971 (latest step 2026101971), OdSchemaCompat 25.3.48, ForkAdoptedFrom source=25.3.48.0;started=2026-10-25T08:32:46Z;adopted=2026-10-25T08:32:48Z |
| V9 Fork startup guard opens the adopted copy | PASS | ProceedFork, pending migrations False |
| V10 Startup model: every Open Dental release client refuses the adopted copy | PASS | 9 of 9 release-client scenarios exit without converting, writing, installing services or copying binaries;  (a Debug build of Open Dental skips its version check: Proceeds; Open Dental ships Release builds only) |
| V11 Row by row, adoption changed only its own rows of preference, documentmisc, securitylog, securityloghash and updatehistory | PASS | preference: 5 added (ForkAdoptedFrom, OdSchemaCompat, ForkSchemaLevel, ForkMigrationInProgress, ForkMigrationsApplied); 2 set to 99.0.0.0 (DataBaseVersion, ProgramVersion); 4 blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl); no other row changed or removed. documentmisc: 4 Open Dental update-file row(s) deleted, none left; 0 other row(s) deleted; 0 added; 0 changed. securitylog: 2 entries added (1 adoption, 1 rehash); no existing row changed or removed. securityloghash: 2 added, each for an added security log entry; no existing row changed or removed. updatehistory: identical row by row. |
| R1 Every 24.4-25.3 column loads and saves through OpenDentBusiness; every writable one writes | PASS | 50 tables, 139 columns new or changed since 24.3: 134 loaded and saved unchanged (32,465 rows saved), and 129 of them given a new value, written and read back; 5 loaded and saved but not given a new value, because Insert or the server sets them (CRUD types TimeStamp, DateEntry, DateTEntry, ExcludeFromUpdate: feeschednote.SecUserNumEntry, feeschednote.SecDateEntry, feeschednote.SecDateTEdit, schoolapproval.SecDateEntry, schoolapproval.SecDateTEdit); 5 not loaded, documented exceptions preserved untouched (chatattach.Thumbnail, chatattach.FileData, deletedobject (3 columns)); first-save normalizations (same meaning in the program): benefit.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (363 cells); benefit.ToothRange: NULL saved as '' (363 cells); computerpref.ComputerOS: blank or unknown enum text saved as 'Win32S' (4 cells); document.DateTStamp: ON UPDATE timestamp of a row the save normalized (2344 cells); document.ExternalSource: blank or unknown enum text saved as 'None' (2344 cells); insplan.GroupNum: NULL saved as '' (2 cells); insplan.SecDateTEdit: ON UPDATE timestamp of a row the save normalized (2 cells) |
| R2 Round trip leaves the database unchanged apart from the first-save normalizations | PASS | only the normalized columns changed: benefit.SecDateTEdit; benefit.ToothRange; computerpref.ComputerOS; document.DateTStamp; document.ExternalSource; insplan.GroupNum; insplan.SecDateTEdit |
| I1 A second adoption is refused | PASS | This database is already adopted (source=25.3.48.0;started=2026-10-25T08:32:46Z;adopted=2026-10-25T08:32:48Z). Nothing to do. |
| I2 A second fork migration changes nothing and the schema still matches | PASS | adoption skipped (already adopted); 9 steps run, 0 changes; verifier 0 differences; row check against the source passed |
| I3 A second rehash changes nothing | PASS | every hash already current: appointment 542, claim 26, claimproc 285, inssub 0, insverify 15, insverifyhist 8, patient 1355, payplan 0, paysplit 53 |
| I4 The whole second pass leaves the database unchanged | PASS | every table except securitylog and securityloghash has the row counts and checksums of the first pass; row by row against the snapshot taken after adoption, preference is identical, and securitylog and securityloghash only gained the rerun's own entry (1 and 1 row), with no existing row changed or removed |

### Across runs

| Check | Result | Detail |
|---|---|---|
| X1 Every run starts from an identical database | PASS | 2 baselines identical (row counts, every checksum, every total) |
| X2 Every run ends with an identical adopted database, apart from the time of adoption | PASS | 2 adopted databases identical: every table except preference, securitylog and securityloghash by its report (row counts, CHECKSUM TABLE, every column checksum, every total); those three row by row, where they differ only in the time of adoption: the value of ForkAdoptedFrom, securitylog.LogDateTime (2 rows) and the securityloghash.LogHash computed from it (2 rows) |
| X3 Every run gives the same check results | PASS | 2 runs, 26 checks each |

### Details of run 1

Office fixture: 4 Open Dental update-file rows and 1 other documentmisc row; 4 preferences with Open Dental addresses, 2 that must be kept; 4 updatehistory rows; 3 stored passwords in Open Dental's encryption (preference ReportingServerMySqlPassHash, programproperty XVWeb Password, clearinghouse Fictional Claims Exchange).

FLOAT check of the source (runbook step 0.2): 164 FLOAT and DOUBLE column(s) in 74 table(s) checked; no value that a logical backup (mariadb-dump) would round.

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

- ColumnChanged appointment.SecurityHash (12572210759711 -> 12538375485082)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged claim.SecurityHash (7523732762616 -> 7535392404282)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged claimproc.SecurityHash (23581417588163 -> 23562050295670)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- RowsChanged documentmisc (5 -> 1)  [allowed: Adoption deletes Open Dental's UpdateFiles rows (checked row by row by adopt).]
- ColumnChanged documentmisc.DateCreated (10069374261 -> 1538566696)  [allowed: Adoption deletes Open Dental's UpdateFiles rows (checked row by row by adopt).]
- ColumnChanged documentmisc.DocMiscNum (9769361567 -> 1174942932)  [allowed: Adoption deletes Open Dental's UpdateFiles rows (checked row by row by adopt).]
- ColumnChanged documentmisc.DocMiscType (7861376959 -> 3630997879)  [allowed: Adoption deletes Open Dental's UpdateFiles rows (checked row by row by adopt).]
- ColumnChanged documentmisc.FileName (6379039644 -> 942913718)  [allowed: Adoption deletes Open Dental's UpdateFiles rows (checked row by row by adopt).]
- ColumnChanged documentmisc.RawBase64 (9667482446 -> 1779909166)  [allowed: Adoption deletes Open Dental's UpdateFiles rows (checked row by row by adopt).]
- ColumnChanged insverify.SecurityHash (1390900252556 -> 1392817533999)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged insverifyhist.SecurityHash (628167813450 -> 624122455924)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnRemoved patient._portal_active_email  [allowed: Owner decision: adoption drops the office-specific generated column and its unique key.]
- ColumnChanged patient.SecurityHash (2927956709010 -> 2860196771227)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged payplan.SecurityHash (25963509457 -> 33942964343)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- ColumnChanged paysplit.SecurityHash (14601550875872 -> 14607388579999)  [allowed: Adoption rehashes every row with the fork's HMAC (risk R2, item 6).]
- RowsChanged preference (25 -> 30)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences (checked row by row by adopt).]
- ColumnChanged preference.Comments (62404309912 -> 73897142823)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences (checked row by row by adopt).]
- ColumnChanged preference.PrefName (57697714827 -> 65925037523)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences (checked row by row by adopt).]
- ColumnChanged preference.PrefNum (53764528705 -> 64321672810)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences (checked row by row by adopt).]
- ColumnChanged preference.ValueString (55961409849 -> 71797404324)  [allowed: Adoption writes fork markers and version sentinels and blanks the update and HQ preferences (checked row by row by adopt).]
- RowsChanged securitylog (2701 -> 2703)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.CompName (5768061947850 -> 5774613957664)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.DateTPrevious (5796485454011 -> 5799171712283)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.DefNum (5841742043169 -> 5848959101928)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.DefNumError (5841742043169 -> 5848959101928)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.FKey (5674734116250 -> 5681951175009)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.LogDateTime (5858673777980 -> 5865231284573)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.LogSource (5841742043169 -> 5848959101928)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.LogText (5784631387789 -> 5788411782893)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.PatNum (5827962492072 -> 5835179550831)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.PermType (5759588115431 -> 5762400254393)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.SecurityLogNum (5811630809453 -> 5814862251304)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- ColumnChanged securitylog.UserNum (5695692550860 -> 5702909609619)  [allowed: Adoption and the rehash log their own actions (checked row by row by adopt: rows only added).]
- RowsChanged securityloghash (2 -> 4)  [allowed: Follows securitylog (checked row by row by adopt: rows only added).]
- ColumnChanged securityloghash.LogHash (7673420916 -> 13031664599)  [allowed: Follows securitylog (checked row by row by adopt: rows only added).]
- ColumnChanged securityloghash.SecurityLogHashNum (4891611139 -> 8594418635)  [allowed: Follows securitylog (checked row by row by adopt: rows only added).]
- ColumnChanged securityloghash.SecurityLogNum (4891611139 -> 11670974803)  [allowed: Follows securitylog (checked row by row by adopt: rows only added).]
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
Rehash: every patient, and the other tables' rows dated on or after 2026-09-25.
  appointment: 5846 rows, 542 in the rehash window (0 without a fork hash); 542 fork hashes, 4352 other, 952 blank
  claim: 3523 rows, 26 in the rehash window (0 without a fork hash); 26 fork hashes, 3072 other, 425 blank
  claimproc: 11055 rows, 285 in the rehash window (0 without a fork hash); 285 fork hashes, 8037 other, 2733 blank
  inssub: 421 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 69 other, 352 blank
  insverify: 624 rows, 15 in the rehash window (0 without a fork hash); 15 fork hashes, 609 other, 0 blank
  insverifyhist: 304 rows, 8 in the rehash window (0 without a fork hash); 8 fork hashes, 296 other, 0 blank
  patient: 1355 rows, 1355 in the rehash window (0 without a fork hash); 1355 fork hashes, 0 other, 0 blank
  payplan: 15 rows, 0 in the rehash window (0 without a fork hash); 0 fork hashes, 0 other, 15 blank
  paysplit: 6878 rows, 53 in the rehash window (0 without a fork hash); 53 fork hashes, 6128 other, 697 blank
Rehash finished.
Row check against synth_rvf60913_med_r1_src (what adoption may change in preference, documentmisc, securitylog, securityloghash and updatehistory):
  preference: 5 added (ForkAdoptedFrom, OdSchemaCompat, ForkSchemaLevel, ForkMigrationInProgress, ForkMigrationsApplied); 2 set to 99.0.0.0 (DataBaseVersion, ProgramVersion); 4 blanked (UpdateServerAddress, WebServiceHQServerURL, UpdateWebsitePath, SyntheticFuturePortalUrl); no other row changed or removed.
  documentmisc: 4 Open Dental update-file row(s) deleted, none left; 0 other row(s) deleted; 0 added; 0 changed.
  securitylog: 2 entries added (1 adoption, 1 rehash); no existing row changed or removed.
  securityloghash: 2 added, each for an added security log entry; no existing row changed or removed.
  updatehistory: identical row by row.
```

#### Rehash record

| Table | Rows | In rehash window | Before: fork / other / blank | After: fork / other / blank | Window rows without a fork hash after |
|---|---:|---:|---|---|---:|
| appointment | 5846 | 542 | 0 / 4894 / 952 | 542 / 4352 / 952 | 0 |
| claim | 3523 | 26 | 0 / 3098 / 425 | 26 / 3072 / 425 | 0 |
| claimproc | 11055 | 285 | 0 / 8098 / 2957 | 285 / 8037 / 2733 | 0 |
| inssub | 421 | 0 | 0 / 69 / 352 | 0 / 69 / 352 | 0 |
| insverify | 624 | 15 | 0 / 624 / 0 | 15 / 609 / 0 | 0 |
| insverifyhist | 304 | 8 | 0 / 304 / 0 | 8 / 296 / 0 | 0 |
| patient | 1355 | 1355 | 0 / 573 / 782 | 1355 / 0 / 0 | 0 |
| payplan | 15 | 0 | 0 / 15 / 0 | 0 / 0 / 15 | 0 |
| paysplit | 6878 | 53 | 0 / 6181 / 697 | 53 / 6128 / 697 | 0 |

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

#### Stored passwords to re-enter (credential inventory of the copy)

- Program link synthetic filler 1: synthetic filler 1, clinic 1 [programproperty 1]
- Program link XVWeb: Password [programproperty 4]
- Clearinghouse Fictional Claims Exchange: password [clearinghouse 1]
- Preference ReportingServerMySqlPassHash [preference 25]
- HL7 definition synthetic filler 1: SFTP password [hl7def 1]
- HL7 definition synthetic filler 2: SFTP password [hl7def 2]
- Electronic billing (eBill), clinic 1: password [ebill 1]
- Electronic billing (eBill), clinic 2: password [ebill 2]
