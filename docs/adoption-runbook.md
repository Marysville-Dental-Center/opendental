# Adoption runbook: moving the office from Open Dental 25.3.48 to Open PMS - Dental

Status: 2026-10-24, revised the same day after review (M1-Z r1); milestone M1 switch gate (M1-Z in [specs/port-backlog.md](specs/port-backlog.md)).
Rehearsed on synthetic data with [tools/AdoptionValidation](../tools/AdoptionValidation/README.md); results in
[m1-validation-report.md](m1-validation-report.md). Steps 3 to 6 run the rehearsal's own code, in its order, with the commands its dry run
ran.
Background: [release-process.md](release-process.md) (version guard and adoption tool), [decoupling-plan.md](decoupling-plan.md) (ground rules),
risks R1 and R2 in the backlog.

This is the procedure for the day the office switches. It is written for the office administrator, with a second person checking each
step. Every step says what to run, what to expect, and what to do when the result is different.

## What adoption does, and what it cannot undo

Adoption converts a copy of the Open Dental 25.3.48 database into a database of this program. It is done once, by an administrator, on a
**restored copy**; normal startup never adopts. `Adoption.Run` (run here by `AdoptionValidation adopt`; the program's Adopt Database window
calls the same code):

1. records the start in the `ForkAdoptedFrom` preference
2. drops the office-only column `patient._portal_active_email` and its unique index `portal_active_email_uniq` (owner decision), so family
   members can share an e-mail address again
3. deletes Open Dental's update files (`documentmisc` rows of types `UpdateFiles` and `UpdateFilesSegment`) without reading them
4. blanks the preferences that point at Open Dental's servers
5. sets `DataBaseVersion` and `ProgramVersion` to `99.0.0.0`, so Open Dental programs can no longer use the database
6. writes the fork's version markers and runs the fork migration steps (for a 25.3.48 database they find nothing to change)
7. records the finish and adds a security log entry

All tables are MyISAM: there are no transactions. If anything goes wrong, the way back is the backup. That is why the procedure keeps the
original database untouched and adopts a restored copy of it.

**After the switch, Open Dental programs cannot open the adopted database** (they see a database "newer" than themselves, find no update
files and exit; see the validation report, check V10). **But the original `opendental` stays an ordinary Open Dental database**: an Open Dental
client still pointed at it opens it normally, and whatever is entered there is missing from the adopted database. So every Open Dental client
is removed before the switch (step 2.1), every workstation switches together (step 8), and steps 10 and 11 check that nothing has used the
original since the backup. Going back to Open Dental means going back to the pre-adoption database, and anything entered in this program after
the switch must then be re-entered by hand. Decide the rollback deadline before the day (step 0.9).

### Why steps 3 to 6 run without the program

Starting this program and logging in writes to the database: this computer's row and its heartbeat, the Database Maintenance list, signals,
login records, security log entries. Step 6 compares the adopted copy with its baseline and accepts only the changes adoption and the rehash
make. So the adoption, the rehash and that comparison are done with the command-line tools, exactly as rehearsed, and **nobody starts this
program on `opendental_pms` until step 6.1 has passed**. From step 6.2 on, the program's own writes are normal use.

## Names used below

| Name | Meaning |
|---|---|
| `opendental` | The office database today (Open Dental 25.3.48). Use the real name from `FreeDentalConfig.xml` on any workstation. |
| `opendental_pms` | The adopted copy. Any name works; it must not exist yet. |
| `od_prep`, `od_prep_pms` | The preparation copy of the office database and its adopted dry-run copy (step 0.6, only with the owner's agreement). Names that must not exist yet. |
| `D:\Adoption` | A folder on the database server with room for at least twice the size of the database folder. |
| `<tools>` | A folder on the database server holding a copy of the built `tools\AdoptionValidation\bin\Release` (or `bin\Debug`) folder: `AdoptionValidation.exe`, `SyntheticData.exe`, `allowed-differences.adoption.json`, `sql\business-totals.sql` and `Invoke-RunbookDryRun.ps1`. The tools only ever connect to `127.0.0.1`, so they run **on the database server**. |
| `<mariadb>` | The MariaDB `bin` folder on the server, for example `C:\Program Files\MariaDB 10.11\bin`. |
| `<date>` | The switch date, `yyyy-MM-dd`, for every `--as-of`. All reports of one switch use the same date, so their totals can be compared. |

The commands are PowerShell, run as administrator on the database server. Set `$env:SYNTH_DB_PASSWORD` to the MariaDB root password for the
tools (they pass it to the MariaDB client programs themselves); the MariaDB client programs you run directly ask for it with `-p`.

## Timing

Measured by the dry run of these steps with these commands (`Invoke-RunbookDryRun.ps1`) on a development PC (MariaDB 10.11, SSD), synthetic
data; see the validation report:

| Step | Medium: 101,881 rows, 1,355 patients, 20 MB dump, 36 MB of tables | Large: 777,822 rows, 9,158 patients, 162 MB dump, 286 MB of tables |
|---|---:|---:|
| 3.1 Backup (`mariadb-dump`) | 3.1 s | 4.8 s |
| 3.2 Restore (`AdoptionValidation restore`, with the file check) | 6.6 s | 26.0 s |
| 3.3 Two baseline reports and their comparison | 5.1 s | 26.2 s |
| 4 and 5 Adoption, fork migration again, verifier, rehash (`AdoptionValidation adopt`) | 2.5 s | 7.0 s |
| 6.1 Post-adoption report, comparison, startup check | 3.0 s | 13.6 s |
| 10.1 and 10.2 Go-live checks of the original | 2.6 s | 12.9 s |
| **Total** | **22.9 s** | **90.5 s** |

Restore and the reports grow with the data (about 0.16 s per MB of dump for the restore here); adoption is dominated by one
`ALTER TABLE patient` and the rehash by the number of patients. The office database also holds documents, logs and history the synthetic
data does not, and the server is not this PC. Plan the outage window from the office's own dry run (step 0.6) when the owner allows it, or
else from these figures (step 0.7): the steps above + password re-entry + workstations + smoke test, plus a margin of 50% (dry run) or 200%
(no dry run).

## 0. Preparation (the week before)

0.1. **Owner sign-off.** The owner has signed [m1-validation-report.md](m1-validation-report.md). The release build of this program is ready,
and the same build is installed on one test workstation (pointed at a synthetic or empty database, never at `opendental`).

0.2. **Versions.** On the server: `"<mariadb>\mariadb.exe" -u root -p opendental -e "SELECT PrefName,ValueString FROM preference WHERE PrefName IN ('DataBaseVersion','ProgramVersion','CorruptedDatabase','UpdateInProgressOnComputerName')"`.
Expect `25.3.48.0`, `25.3.48.0`, `0` and blank. Any other version: stop; adoption refuses every version except 25.3.48.0, and the office must
not update Open Dental before the switch.

0.3. **Inventory of Open Dental services and jobs** (for step 2): on the server and on every workstation,

```powershell
Get-Service OpenDent* | Format-Table Name,DisplayName,Status,StartType
Get-CimInstance Win32_Service | Where-Object { $_.PathName -match 'OpenDent' } | Format-Table Name,PathName
Get-ScheduledTask | Where-Object { ($_.Actions | ForEach-Object { $_.Execute }) -match 'OpenDent' } | Format-Table TaskPath,TaskName,State
```

Expect some of: `OpenDentalService` (Open Dental Service), `OpenDentalEConnector` (eConnector), the API service (`OpenDentalAPIService.exe`),
`OpenDentalHL7`, `OpenDentalReplicationService`, the legacy `OpenDentalCustListener`, and scheduled tasks that start Open Dental programs. Also
note the middle tier web service (`OpenDentalServer` in IIS), if the office uses it. Write the list down.

0.4. **Other programs that connect to the database directly** (reporting tools, imaging or phone bridges, backup scripts): list the MariaDB
accounts (`SELECT user,host FROM mysql.user`) and ask each vendor. They are pointed at `opendental_pms` in step 9.

0.5. **Workstations.** For every computer that runs Open Dental, write down: its name, the Open Dental folder (usually
`C:\Program Files (x86)\Open Dental`), and the server, database and user in that folder's `FreeDentalConfig.xml`. This list is used in steps
2.1, 8 and 10. If the user is not `root`, also record that account's rights: `"<mariadb>\mariadb.exe" -u root -p -e "SHOW GRANTS FOR '<user>'@'<host>'"`
(step 2.1 may revoke them on `opendental`).

0.6. **Preparation copy and dry run: only if the owner agrees.** This restores a fresh backup of the office database into a scratch copy on
the office server, to list the passwords to re-enter, to time the backup and the restore, and to run steps 3 to 6 once on real data before
the day. It is office data on the office server, never copied elsewhere; but the owner's decisions so far rule out office data for the
rehearsal, and this one is **still open** (validation report, "For the owner", item 3). Record the owner's answer on the checklist. If the
answer is no, skip to 0.7.

Do all of 0.6 **outside office hours**, with nobody working: the backup locks the tables while it runs.

0.6.1. **Fresh backup first.** It is the safety net for everything below and times step 3.1:

```powershell
New-Item -ItemType Directory D:\Adoption\prep
& "<mariadb>\mariadb-dump.exe" -u root -p --lock-tables --routines --triggers --events --hex-blob --default-character-set=utf8mb4 `
  --result-file=D:\Adoption\prep\opendental-prep.sql opendental
```

Expect exit code 0. Write down how long it took. This is a single-database dump: it has no `CREATE DATABASE` or `USE` lines.

0.6.2. **Restore it as the preparation copy:**

```powershell
& "<tools>\AdoptionValidation.exe" restore --dump D:\Adoption\prep\opendental-prep.sql --database od_prep
```

Expect "Restored N tables into od_prep" and exit code 0; write down the time. The command first checks that `od_prep` does not exist and that
the file names no database (no `USE`, no `CREATE`, `DROP` or `ALTER DATABASE`, no table name qualified with a database), and refuses
otherwise without changing anything (exit code 2). It then loads the file with `mariadb --one-database od_prep`, which on its own also skips
statements meant for another database.

**Never load a backup file with a plain `mariadb ... < file`, and do not use the nightly backup here.** A dump made with `--databases` or
`--all-databases` (common for nightly backups) contains `CREATE DATABASE` and `USE opendental`: loaded "into a scratch database", it replays
`DROP TABLE`, `CREATE TABLE` and `INSERT` into the live `opendental` and silently replaces today's data with last night's. MyISAM has no undo.
If the office's backup is a copy of the database folder (MyISAM files) rather than a dump, a folder copy can stand in for 0.6.1 and 0.6.2:
check that every table is MyISAM (`SELECT ENGINE,COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='opendental' GROUP BY ENGINE`
lists only MyISAM), find the data folder (`SELECT @@datadir`), check that `<datadir>\od_prep` does not exist (`Test-Path` is `False`) and that
`SHOW DATABASES LIKE 'od_prep'` is empty, copy the backup's database folder to the new folder `<datadir>\od_prep` (never into
`<datadir>\opendental`), and run `FLUSH TABLES;`.

0.6.3. **Passwords to re-enter.** Make the exact list for this office (step 7 explains why):
`& "<tools>\AdoptionValidation.exe" credentials --database od_prep > D:\Adoption\credentials-prep.txt`. It prints where each password is
(program link, clearinghouse, preference, ...) and never the value. Collect the passwords from the vendors or the office password manager.

0.6.4. **Dry run of steps 3 to 6:**

```powershell
& "<tools>\Invoke-RunbookDryRun.ps1" -Original od_prep -Copy od_prep_pms -Folder D:\Adoption\dry-run -AsOf <date>
```

It runs steps 3.1, 3.2, 3.3, 4, 5 and 6.1 and the go-live checks of step 10 with the commands below, with `od_prep` standing in for
`opendental` and `od_prep_pms` for `opendental_pms`. It never writes to `od_prep` or `opendental` and stops at the first unexpected result.
Expect `Dry run: PASS`. `D:\Adoption\dry-run\dry-run-timings.txt` holds the time of every step: plan the outage window from it, plus 50%
(see Timing). Any other result: stop, keep the folder (its reports hold row counts, checksums and business totals, no patient data; the script
deletes its backup file) and give `compare.txt`, `adoption-log.txt` and `dry-run-timings.txt` to the developers, with the owner's agreement
for the totals. The switch waits until a dry run passes. To run it again, first `DROP DATABASE od_prep_pms;` and use a new folder.

0.6.5. **Clean up the same night:** `"<mariadb>\mariadb.exe" -u root -p -e "DROP DATABASE od_prep_pms; DROP DATABASE od_prep;"` (check the
names twice; never `opendental`), and delete `D:\Adoption\prep\opendental-prep.sql` (a copy of office data; the nightly backups remain). Keep
`D:\Adoption\dry-run` and `credentials-prep.txt`.

0.7. **If the owner says no to 0.6:**

- **Passwords:** the exact list is made on the day, in step 7, by running `credentials` on `opendental_pms`. So before the day, collect the
  passwords of every integration in the table of step 7 that the office uses, from the office password manager and the vendors, so that step 7
  is only typing.
- **Window:** from the rehearsal's figures. Read the database size (table metadata only, no rows):
  `"<mariadb>\mariadb.exe" -u root -p -e "SELECT ROUND(SUM(DATA_LENGTH+INDEX_LENGTH)/1048576) AS MB FROM information_schema.TABLES WHERE TABLE_SCHEMA='opendental'"`.
  The large rehearsal database occupies about 286 MB this way, and steps 3.1 to 6.1 and the go-live checks took 90 s on it (Timing). Scale
  by size (for example 3 GB: 90 s × 3000 / 286, about 16 minutes) and add 200% for the unknown server (about 50 minutes), then add password
  re-entry, the workstations and the smoke test.
- **Tools check (optional, recommended):** rehearse on the office server with synthetic data only, outside office hours, to check the tools,
  the MariaDB client programs and the server's speed: `& "<tools>\AdoptionValidation.exe" rehearse --size large --runs 1 --keep --tag office --schema-from opendental --out D:\Adoption\rehearsal`
  (it reads only the table definitions of `opendental`, never its rows, and writes only `synth_m1z_office_*` databases), then
  `& "<tools>\Invoke-RunbookDryRun.ps1" -Original synth_m1z_office_r1_src -Copy synth_m1z_office_pms -Folder D:\Adoption\dry-run-synthetic -AsOf 2026-09-30`.
  Expect `Rehearsal: PASS` and `Dry run: PASS`, then drop `synth_m1z_office_r1_src`, `synth_m1z_office_r1_adopted` and `synth_m1z_office_pms`.

0.8. **Disk space.** Free space on the database drive of at least 2.5 times the size of the `opendental` folder in the MariaDB data folder
(backup file, restored copy, original); 4.5 times for the night of step 0.6 (two backup files and two copies at once).

0.9. **Rollback deadline.** Agree with the owner how long Open Dental remains the fallback (for example: until the end of the first working day
after the switch). After the deadline the original database is archived (step 11) and rollback means re-entering the work done since.

0.10. **Open Dental installers.** Keep the Open Dental 25.3.48 installer and the office's registration details at hand; rollback needs them.

## 1. Start of the outage

1.1. Tell everyone to close Open Dental. On the server, check nobody is connected:
`"<mariadb>\mariadb.exe" -u root -p -e "SELECT ID,USER,HOST,DB,COMMAND,TIME FROM information_schema.PROCESSLIST WHERE DB IS NOT NULL"` shows
no connection to `opendental` (your own has no database). A row names the computer (`HOST`) that is still connected.

1.2. Turn off anything else that writes to the database (step 0.4 programs).

## 2. Stop and uninstall the Open Dental services and clients

They connect to the same database, talk to Open Dental's servers, and would keep writing (statements, eServices, scheduled processes). On the
server, and on any workstation from the step 0.3 list:

```powershell
# Stop every Open Dental service, then remove it. Keep the list from step 0.3 for rollback.
Get-Service OpenDent* | Stop-Service -Force
Get-Service OpenDent* | ForEach-Object { sc.exe delete $_.Name }
# Disable the scheduled tasks that start Open Dental programs.
Get-ScheduledTask | Where-Object { ($_.Actions | ForEach-Object { $_.Execute }) -match 'OpenDent' } | Disable-ScheduledTask
```

- **Open Dental Service** (`OpenDentalService`): stops statements, automated messages and the scheduled processes in the `scheduledprocess`
  table.
- **eConnector** (`OpenDentalEConnector`, and the old `OpenDentalCustListener`): stops eServices traffic with Open Dental's servers.
- **API service** (`OpenDentalAPIService`): stops the local REST API (dropped by owner decision).
- **HL7** (`OpenDentalHL7`) and **replication** (`OpenDentalReplicationService`): if present, stop them; this program has its own HL7
  service, set up again after the switch if the office uses HL7.
- **Scheduled tasks** that start Open Dental programs: disabled, not deleted, until the rollback deadline.
- **Middle tier** (`OpenDentalServer` web application in IIS), if used: stop the application pool.

Expect: `Get-Service OpenDent*` returns nothing. If a service will not stop, end its process in Task Manager and repeat.

2.1. **Open Dental clients: remove them from every workstation of the step 0.5 list.** Removing shortcuts is not enough. The original
`opendental` stays an ordinary Open Dental database, and an Open Dental client that still points at it opens it normally (validation report,
check A2): whatever is entered there is missing from `opendental_pms` and would be lost when the original is dropped. On each workstation:

1. Uninstall Open Dental (Settings, Apps; or Control Panel, Programs and Features).
2. If the Open Dental folder is still there, rename `FreeDentalConfig.xml` in it to `FreeDentalConfig.xml.before-switch` (keep it for
   rollback), and delete any Open Dental shortcut left on the desktop, the Start menu or the taskbar.
3. Check: `Get-ChildItem 'C:\Program Files*\Open Dental*' -Recurse -Include OpenDental.exe,FreeDentalConfig.xml -ErrorAction SilentlyContinue`
   lists nothing.
4. Mark it in the table of step 8.

If the workstations connect with their own MariaDB account (step 0.5: the user is not `root`) and `SHOW GRANTS` shows its rights on
`opendental` only (``ON `opendental`.*``), also revoke them now, so that even a forgotten Open Dental client cannot open the original:
`"<mariadb>\mariadb.exe" -u root -p -e "REVOKE ALL PRIVILEGES ON opendental.* FROM '<user>'@'<host>'"`. Keep the `GRANT` lines from step 0.5
for rollback; step 8 grants the same rights on `opendental_pms`. With `root` or rights on `*.*` this is not possible; steps 8 and 10 then catch
a forgotten client.

2.2. **A to Z folder.** Move Open Dental's `Setup.exe` and any `UpdateFiles` folder out of the root of the A to Z folder into
`D:\Adoption\atoz-od-files` (do not touch the patient folders). An Open Dental client in "classic" update mode launches the `Setup.exe` it
finds there.

## 3. Back up and restore the copy

3.1. **Backup** (the rollback point). Write down the time it starts (steps 10 and 11 refer to it):

```powershell
& "<mariadb>\mariadb-dump.exe" -u root -p --lock-tables --routines --triggers --events --hex-blob --default-character-set=utf8mb4 `
  --result-file=D:\Adoption\opendental-pre-adoption.sql opendental
```

Expect exit code 0 and a file of roughly the database size. Copy the file to a second drive or external disk now; it is the rollback.
Also keep the MariaDB data folder copy if the office normally uses file-level backups.

3.2. **Restore it as the copy that is adopted:**

```powershell
& "<tools>\AdoptionValidation.exe" restore --dump D:\Adoption\opendental-pre-adoption.sql --database opendental_pms
```

Expect "Restored N tables into opendental_pms" and exit code 0. It refuses a name that exists and a file that names a database (see step
0.6.2), changing nothing; then check the name and the file. If the load itself fails, the message says so: `DROP DATABASE opendental_pms;` and
run it again.

3.3. **Baseline reports** of both databases (read-only):

```powershell
& "<tools>\SyntheticData.exe" validate --database opendental --as-of <date> --out D:\Adoption\baseline-original.json
& "<tools>\SyntheticData.exe" validate --database opendental_pms --as-of <date> --out D:\Adoption\baseline-copy.json
& "<tools>\SyntheticData.exe" compare D:\Adoption\baseline-original.json D:\Adoption\baseline-copy.json
```

Expect `Unexpected differences: 0` (exit code 0): the backup is complete and restores exactly. Any difference: stop, keep Open Dental running
on `opendental`, and investigate. The reports hold row counts, checksums and totals, no patient data, but keep them with the backup.

**Do not start this program, or any other program, on `opendental_pms` until step 6.1 has passed.**

## 4. Adopt the copy, without starting the program

```powershell
& "<tools>\AdoptionValidation.exe" adopt --database opendental_pms --original opendental --confirm opendental_pms --log D:\Adoption\adoption-log.txt
```

It checks first and changes nothing if a check fails ("Refused: ...", "Nothing was changed.", exit code 2): `--confirm` repeats the database
to adopt; `opendental` is a different, unadopted Open Dental database; nobody is connected to either (step 1.1); and `opendental_pms` has
`opendental`'s tables and exactly its row counts, and was created after it (so the two names are not the wrong way round). Then it adopts and
prints each step with its time; `adoption-log.txt` keeps everything. Expect, in this order:

- "Open Dental 25.3.48 database. It can be adopted." and "Adoption from Open Dental 25.3.48.0 started."
- "Dropped index patient.portal_active_email_uniq." and "Dropped column patient._portal_active_email."
- "Deleted N Open Dental update file row(s)." and "Blanked N preference(s) that pointed at Open Dental's servers: ..."
- "DataBaseVersion and ProgramVersion set to 99.0.0.0." and the fork markers
- every fork migration step "found nothing to change", "The schema matches Open Dental 25.3.48 (OdSchemaCompat)." and "Adoption finished."
- "Fork migration, every step again: 9 steps run, 0 changes." and "The schema matches Open Dental 25.3.48 exactly (422 of 422 tables)."
- the rehash of step 5
- "Adopt: PASS." and exit code 0

If it stops ("Adoption stopped: ...", exit code 1): note the message and run the same command again; every step can be repeated, an
interrupted adoption is finished, and a finished one is not adopted twice. If it stops again, or ends with "Adopt: FAIL" (exit code 6), go to
Rollback.

## 5. Rehash the security hashes

Done by the command of step 4, after the schema verifier: `SecurityHash.RehashNow`, the same code as Database Maintenance, Tools, Security
Hashes. Open Dental's `SecurityHash` values cannot be checked by this program. The rehash gives every patient, and every appointment, claim,
claim procedure, payment split, payment plan, insurance subscriber and verification row in the checked date range, a hash of this program. It
does not change any data you see, keeps every row's last-changed time, and adds a security log entry. Check in `adoption-log.txt`: "Rehash:
every patient, and the other tables' rows dated on or after ...", one line per table ending in "(0 without a fork hash)" where it shows the
window, and "Rehash finished.". If the command stopped before this, run step 4 again: on an adopted copy it skips the adoption and repeats the
migration check, the verifier and the rehash, which change nothing the second time.

## 6. Validate the adopted copy, then start the program

6.1. **Before anyone starts the program:**

```powershell
& "<tools>\SyntheticData.exe" validate --database opendental_pms --as-of <date> --out D:\Adoption\after-adoption.json
& "<tools>\SyntheticData.exe" compare D:\Adoption\baseline-copy.json D:\Adoption\after-adoption.json `
  --allowed "<tools>\allowed-differences.adoption.json" > D:\Adoption\compare.txt
& "<tools>\AdoptionValidation.exe" startup-check --database opendental_pms > D:\Adoption\startup-check.txt
```

Expect in `compare.txt`: `Unexpected differences: 0` (exit code 0). The allowed differences are exactly the ones adoption and the rehash make:
the dropped column, `SecurityHash` columns, preference rows, the deleted update files, adoption's and the rehash's security log entries. Every
business total (aging buckets, production, income, payment plans, insurance estimates, appointments) is equal. In `startup-check.txt`, every
Open Dental release client line says `Exits` with `database writes no`, and the last line is "Every Open Dental release client refuses this
database." (exit code 0). This comparison holds only now: once the program has started (6.2), its own writes are in the database.

6.2. **First start and smoke test.** On the server (or the workstation that will be the update server), install this program's release build
in its own folder (for example `C:\Program Files (x86)\Open PMS - Dental`), never over the Open Dental folder. Start it; in the Choose
Database window enter the server and the database `opendental_pms`. The version guard says the database is adopted and up to date (if it
offers to publish this build, see step 8.1); log in. If it says instead that the database "has not been adopted" and offers the adoption tool, it is pointed at the wrong database: answer No and
correct the database name. Never adopt from a workstation.

Then: open five patients you know in every module (Family, Account, Treatment Plan, Chart, Images, Appointments), plus the records that use
the 24.4 to 25.3 features if the office has them (dated fees, chart letters, mirrored appointments, eForms, pending insurance, group notes,
Check sheets). Print the aging report and compare its totals with the last Open Dental aging report.

Any unexpected difference or wrong screen: go to Rollback (the original is untouched).

## 7. Re-enter the stored passwords

Open Dental encrypted stored passwords with its own proprietary key; this program cannot read them (decoupling plan, ground rule 3;
`CodeBase/Crypto/README.md`). They show as blank, and the integration fails until they are re-entered. Run
`<tools>\AdoptionValidation.exe credentials --database opendental_pms` for the office's exact list (without step 0.6, this is the first time the
list is made), and re-enter each:

| Where | What |
|---|---|
| Setup, Program Links, each enabled bridge | Every property whose name contains "Password" and every masked (high-security) property, per clinic where clinic rows exist. For example: XVWeb, PreXion, Dental Studio, Divvy, Trojan Express Collect, AvaTax, Transworld (SFTP password), PDMP (Bamboo password, state PDMP password), and the keys and secrets of vendor-direct integrations the office enabled (PayConnect, EdgeExpress, PaySimple, DentalXChange, USPS, PostcardMania). |
| Setup, Data Paths (A to Z on SFTP) | The SFTP password, if the A to Z folder is on SFTP. |
| Setup, Clearinghouses | The password of each clearinghouse, and of each clinic override. |
| Setup, Enterprise and Server Connections; Reports setup; Backups | The reporting-server, read-only-server and archive MySQL passwords (`ReportingServerMySqlPassHash`, `ReadOnlyServerMySqlPassHash`, `ArchivePassHash`). |
| Electronic billing (eBill) | The eBill password(s). |
| HL7 definitions | The SFTP password, if HL7 is used. |
| Program links with per-user passwords (for example Oryx) | Each user re-enters their own password. |
| QuickBooks Online, Microsoft 365 | Connect again (re-authorize). |
| Each workstation | The MySQL password in the Choose Database window (`FreeDentalConfig.xml`). |
| Check, re-enter only if it fails | E-mail account passwords (Setup, E-mail) and X-Charge or OpenEdge passwords use a different, open format that may still work: send a test e-mail and run a test card transaction. |

Run `credentials` again afterwards: entries re-entered in this program are stored in its own format and disappear from the list.

## 8. Switch every workstation

8.1. On the update server (the workstation named in Update Setup, `WebServiceServerName`), start the release build and accept "publish this
build" (see [release-process.md](release-process.md)).

8.2. Every other workstation of the step 0.5 list installs this program once, in its own folder as in step 6.2, starts it, is pointed at
`opendental_pms` in the Choose Database window, and logs in. If step 2.1 revoked the workstations' MariaDB account on `opendental`, first grant
it the same rights on `opendental_pms` (the `GRANT` lines of step 0.5 with the database name changed).

Fill this table; every line needs both marks before step 10. A workstation that cannot be switched now stays switched off until it can.

| Workstation (step 0.5) | Open Dental removed, no `OpenDental.exe` or `FreeDentalConfig.xml` left in its folder (2.1) | This program on `opendental_pms`, logged in (8.2) | Initials |
|---|---|---|---|
| | | | |

## 9. Point the other programs at the adopted database

Repoint every program from step 0.4 at `opendental_pms` (same server, same accounts; grant the accounts on `opendental_pms` if they were
database-specific grants). Re-enable the backup jobs for `opendental_pms`.

## 10. Go live

Before announcing it, check that nothing has used the original since the backup of step 3.1:

10.1. **No connection to the original:**
`"<mariadb>\mariadb.exe" -u root -p -e "SELECT ID,USER,HOST,COMMAND,TIME FROM information_schema.PROCESSLIST WHERE DB='opendental'"` shows no
rows. A row names the computer (`HOST`) that is connected: close the program there and finish step 2.1 on it.

10.2. **The original is unchanged:**

```powershell
& "<tools>\SyntheticData.exe" validate --database opendental --as-of <date> --out D:\Adoption\golive-original.json
& "<tools>\SyntheticData.exe" compare D:\Adoption\baseline-original.json D:\Adoption\golive-original.json > D:\Adoption\golive-compare.txt
```

Expect `Unexpected differences: 0` (exit code 0), with no `--allowed`: nothing may differ. Any difference means something wrote to
`opendental` after the backup: treat it as lost work and recover it before going on ("Work entered in the original after the backup", below).

10.3. Announce the end of the outage. Keep `D:\Adoption` (backup, reports, logs) until the rollback deadline has passed and the owner has signed
the switch. Repeating 10.1 and 10.2 (with a new `--out` name each time) at the end of each working day until the deadline costs a few
minutes and finds a forgotten client early.

## 11. After the rollback deadline

11.1. Repeat 10.1 (no rows) and 10.2 with `--out D:\Adoption\predrop-original.json`, compared with `baseline-original.json` (or the newer
reference from "Work entered in the original after the backup"): `Unexpected differences: 0`. Any difference: recover that work first.

11.2. Make a fresh backup of `opendental_pms`. Archive `D:\Adoption\opendental-pre-adoption.sql` offline, then drop the original:
`DROP DATABASE opendental;` (only after the owner agrees). Delete the disabled scheduled tasks of step 2 and the `FreeDentalConfig.xml.before-switch`
files of step 2.1.

## Work entered in the original after the backup

If step 10.1 or 11.1 finds a connection to `opendental`, or 10.2 or 11.1 finds a difference, someone used Open Dental on the original after the
backup. That work is not in `opendental_pms`, and dropping the original (step 11) would lose it.

1. Stop it: find the computer (`HOST` in the process list, or `CompName` below), close Open Dental there and finish step 2.1 on it.
2. List what was done. The comparison names the tables that changed; the security log names the actions, users and patients:
   `"<mariadb>\mariadb.exe" -u root -p opendental -e "SELECT LogDateTime,CompName,UserNum,PatNum,PermType,LogText FROM securitylog WHERE LogDateTime>='<start of step 3.1, yyyy-MM-dd HH:mm:ss>' ORDER BY LogDateTime"`.
   This list shows patients: keep it on the server.
3. Re-enter that work in this program by hand, with a second person checking each item against the list.
4. Record a new reference of the original: `& "<tools>\SyntheticData.exe" validate --database opendental --as-of <date> --out D:\Adoption\baseline-original-2.json`,
   note on the checklist why, and use it instead of `baseline-original.json` in step 11.1 and in Rollback step 2.

Then continue where you were.

## Rollback

Use this when a step above fails, or a problem shows up before the rollback deadline. Anything entered in this program since the switch is
lost and must be re-entered in Open Dental by hand.

1. Close this program on every workstation; stop any program pointed at `opendental_pms`.
2. Check the original is untouched: run `SyntheticData.exe validate --database opendental --as-of <date> --out D:\Adoption\rollback-check.json`
   and `SyntheticData.exe compare D:\Adoption\baseline-original.json D:\Adoption\rollback-check.json` (or the newer reference). Expect
   `Unexpected differences: 0`.
   - If there are differences, something wrote to it: list what with step 2 of "Work entered in the original after the backup". If it is work
     the office keeps, keep the original as it is. Otherwise **restore the pre-adoption backup** instead: `DROP DATABASE opendental;`, then
     `& "<tools>\AdoptionValidation.exe" restore --dump D:\Adoption\opendental-pre-adoption.sql --database opendental`, and compare again with
     `baseline-original.json` (expect 0 unexpected differences).
3. Reinstall the Open Dental 25.3.48 client on the workstations and the Open Dental services on the server from the installer (step 0.10),
   using the lists from steps 0.3 and 0.5; rename `FreeDentalConfig.xml.before-switch` back where it exists. Re-grant the workstations' MariaDB
   account on `opendental` if step 2.1 revoked it. Re-enable the scheduled tasks of step 2. Move `Setup.exe` and `UpdateFiles` back into the A
   to Z folder.
4. Point the other programs (step 0.4) back at `opendental`.
5. Keep `opendental_pms` (renamed or dumped) for the analysis; do not let any Open Dental program near it.

The adopted database itself cannot be "un-adopted": the dropped column, the deleted update files and the sentinel versions are not reversible
in place, by design.

## Checklist

Sign each line with time and initials.

| # | Step | Expected | Done |
|---|---|---|---|
| 0.1 | Validation report signed, release build ready | Owner signature | |
| 0.2 | Versions | 25.3.48.0 / 25.3.48.0, not corrupted, no update running | |
| 0.3 | Services and tasks listed | List filed | |
| 0.4 | Direct database users listed | List filed | |
| 0.5 | Workstations listed, with folder, database and MariaDB user | List filed | |
| 0.6 | Owner's answer on the preparation copy | Yes / no, signed | |
| 0.6 | If yes, outside office hours: fresh backup, `restore` into `od_prep`, `credentials`, dry run | Exit 0 each; `Dry run: PASS`; times filed | |
| 0.6.5 | If yes: `od_prep_pms` and `od_prep` dropped, prep backup deleted | Done the same night | |
| 0.7 | If no: passwords collected, window planned from the rehearsal (+200%) | Plan filed | |
| 0.8 | Disk space | 2.5 × database size free (4.5 × for step 0.6) | |
| 0.9 | Rollback deadline agreed | Date and time | |
| 1 | Everyone out, other writers off | No connections | |
| 2 | Open Dental services stopped and removed, tasks disabled, A to Z Setup moved | `Get-Service OpenDent*` empty | |
| 2.1 | Open Dental removed from every workstation (not just shortcuts), `FreeDentalConfig.xml` renamed; account revoked on `opendental` where possible | Table of step 8, first column complete | |
| 3.1 | Backup | Exit 0, start time noted, copied off the server | |
| 3.2 | `restore` into `opendental_pms` | Exit 0 | |
| 3.3 | Baselines compared | 0 unexpected differences | |
| 4, 5 | `adopt` (adoption, migration check, verifier, rehash) | `Adopt: PASS`, exit 0, log saved | |
| 6.1 | Validation before the first start | 0 unexpected differences; startup check exit 0 | |
| 6.2 | First start, smoke test, aging report | OK | |
| 7 | Passwords re-entered | `credentials` list empty or explained | |
| 8 | Every workstation switched | Table of step 8 complete | |
| 9 | Other programs repointed, backups on `opendental_pms` | Checked | |
| 10.1 | No connection to `opendental` | No rows | |
| 10.2 | `opendental` unchanged since the backup | 0 unexpected differences | |
| 10.3 | Live | Announced | |
| 11.1 | Before dropping: no connection, `opendental` unchanged | No rows; 0 unexpected differences | |
| 11.2 | After the deadline: original archived and dropped | Owner agreed | |
