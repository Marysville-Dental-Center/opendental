# Adoption runbook: moving the office from Open Dental 25.3.48 to Open PMS - Dental

Status: 2026-10-24, milestone M1 switch gate (M1-Z in [specs/port-backlog.md](specs/port-backlog.md)). Rehearsed on synthetic data with
[tools/AdoptionValidation](../tools/AdoptionValidation/README.md); results in [m1-validation-report.md](m1-validation-report.md). Background:
[release-process.md](release-process.md) (version guard and adoption tool), [decoupling-plan.md](decoupling-plan.md) (ground rules), risks R1
and R2 in the backlog.

This is the procedure for the day the office switches. It is written for the office administrator, with a second person checking each
step. Every step says what to run, what to expect, and what to do when the result is different.

## What adoption does, and what it cannot undo

Adoption converts a copy of the Open Dental 25.3.48 database into a database of this program. It is done once, by an administrator, on a
**restored copy**; normal startup never adopts. `Adoption.Run` (window **Adopt Database**):

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
files and exit; see the validation report, check V10). All workstations switch together. Going back to Open Dental means going back to the
pre-adoption database, and anything entered in this program after the switch must then be re-entered by hand. Decide the rollback deadline
before the day (step 0.8).

## Names used below

| Name | Meaning |
|---|---|
| `opendental` | The office database today (Open Dental 25.3.48). Use the real name from `FreeDentalConfig.xml` on any workstation. |
| `opendental_pms` | The adopted copy. Any name works; it must not exist yet. |
| `D:\Adoption` | A folder on the database server with room for at least twice the size of the database folder. |
| `<tools>` | A folder on the database server holding the built `tools\SyntheticData` and `tools\AdoptionValidation` programs (copy their `bin\Release` or `bin\Debug` folders). Both only ever connect to `127.0.0.1`, so they run **on the database server**. |
| `<mariadb>` | The MariaDB `bin` folder on the server, for example `C:\Program Files\MariaDB 10.11\bin`. |

The commands are PowerShell, run as administrator on the database server. Set `$env:SYNTH_DB_PASSWORD` to the MariaDB root password for the
tools; the MariaDB client programs ask for it with `-p`.

## Timing

Measured by the rehearsal on a development PC (MariaDB 10.11, SSD), synthetic data; see the validation report for every step:

| Step | Medium: 101,881 rows, 1,355 patients, 20 MB dump | Large: 777,822 rows, 9,158 patients, 162 MB dump |
|---|---:|---:|
| Backup (`mariadb-dump`) | 3.3 s | 4.7 s |
| Restore into the copy | 6.2 s | 23.5 s |
| Validation report (once; steps 3.3 and 6 need three) | 1.9 s | 12.7 s |
| Adoption, including the fork migration steps | 0.6 s | 1.0 s |
| Schema verifier | 0.3 s | 0.2 s |
| Rehash | 1.4 s | 7.5 s |

Restore and the reports grow with the data (about 0.15 s per MB of dump for the restore here); adoption is dominated by one
`ALTER TABLE patient` and the rehash by the number of patients. The office database also holds documents, logs and history the synthetic
data does not, so **time the backup and the restore on the office server in the week before** (step 0.6; neither changes the office
database) and plan the outage window as: backup + restore + 3 reports + adoption + rehash + password re-entry + smoke test, plus a margin of
50%.

## 0. Preparation (the week before)

0.1. **Owner sign-off.** The owner has signed [m1-validation-report.md](m1-validation-report.md). The release build of this program is ready,
and the same build is installed on one test workstation.

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

0.5. **Passwords to re-enter.** Make the exact list for this office (step 7 explains why): restore the latest nightly backup into a scratch
database on the server (owner decision: this is office data on the office server, never copied elsewhere) and run
`<tools>\AdoptionValidation.exe credentials --database <scratch>`. It prints where each password is (program link, clearinghouse,
preference, ...) and never the value. Collect the passwords from the vendors or the office password manager. Drop the scratch database
afterwards.

0.6. **Time the backup and the restore** with the commands of steps 3.1 and 3.2 against the scratch database, and plan the window
(see Timing).

0.7. **Disk space.** Free space on the database drive of at least 2.5 times the size of the `opendental` folder in the MariaDB data folder
(backup file, restored copy, original).

0.8. **Rollback deadline.** Agree with the owner how long Open Dental remains the fallback (for example: until the end of the first working day
after the switch). After the deadline the original database is archived (step 11) and rollback means re-entering the work done since.

0.9. **Open Dental installers.** Keep the Open Dental 25.3.48 installer and the office's registration details at hand; rollback needs them.

## 1. Start of the outage

1.1. Tell everyone to close Open Dental. On the server, check nobody is connected:
`"<mariadb>\mariadb.exe" -u root -p -e "SHOW PROCESSLIST"` shows no connections to `opendental` except your own.

1.2. Turn off anything else that writes to the database (step 0.4 programs).

## 2. Stop and uninstall the Open Dental services

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

2.1. **Open Dental clients.** On every workstation, uninstall Open Dental (Apps and Features) or at least remove its shortcuts. The adopted
database refuses them anyway, but an Open Dental client that finds a "newer" database may offer to download or run a Setup program.

2.2. **A to Z folder.** Move Open Dental's `Setup.exe` and any `UpdateFiles` folder out of the root of the A to Z folder into
`D:\Adoption\atoz-od-files` (do not touch the patient folders). An Open Dental client in "classic" update mode launches the `Setup.exe` it
finds there.

## 3. Back up and restore the copy

3.1. **Backup** (the rollback point):

```powershell
& "<mariadb>\mariadb-dump.exe" -u root -p --lock-tables --routines --triggers --events --hex-blob --default-character-set=utf8mb4 `
  --result-file=D:\Adoption\opendental-pre-adoption.sql opendental
```

Expect exit code 0 and a file of roughly the database size. Copy the file to a second drive or external disk now; it is the rollback.
Also keep the MariaDB data folder copy if the office normally uses file-level backups.

3.2. **Restore it as the copy that is adopted:**

```powershell
& "<mariadb>\mariadb.exe" -u root -p -e "CREATE DATABASE opendental_pms CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci"
cmd /c """<mariadb>\mariadb.exe"" -u root -p --default-character-set=utf8mb4 opendental_pms < D:\Adoption\opendental-pre-adoption.sql"
```

3.3. **Baseline reports** of both databases (read-only):

```powershell
& "<tools>\SyntheticData.exe" validate --database opendental --as-of 2026-12-31 --out D:\Adoption\baseline-original.json
& "<tools>\SyntheticData.exe" validate --database opendental_pms --as-of 2026-12-31 --out D:\Adoption\baseline-copy.json
& "<tools>\SyntheticData.exe" compare D:\Adoption\baseline-original.json D:\Adoption\baseline-copy.json
```

Use the switch date for `--as-of`. Expect `Unexpected differences: 0` (exit code 0): the backup is complete and restores exactly. Any
difference: stop, keep Open Dental running on `opendental`, and investigate. The reports hold row counts, checksums and totals, no patient
data, but keep them with the backup.

## 4. Adopt the copy

4.1. On the server (or the workstation that will be the update server), install this program's release build and point it at the copy:
in the Choose Database window enter the server and the database `opendental_pms`.

4.2. Start it. The version guard refuses the unadopted copy with "This is an Open Dental 25.3.48 database that has not been adopted" and
offers the adoption tool. Click **Yes**.

4.3. In **Adopt Database**: check the status says "Open Dental 25.3.48 database. It can be adopted.", tick that this is a restored copy with a
backup, type `opendental_pms`, click **Adopt** and confirm. Expect the log to end with "Adoption finished." and these lines on the way:
"Dropped index patient.portal_active_email_uniq", "Dropped column patient._portal_active_email", "Deleted N Open Dental update file row(s)",
"Blanked N preference(s)", "DataBaseVersion and ProgramVersion set to 99.0.0.0", the fork migration steps "found nothing to change", and "The
schema matches Open Dental 25.3.48 (OdSchemaCompat)". Save the log text to `D:\Adoption\adoption-log.txt`.

If adoption stops with an error: note it, click **Adopt** again (every step can be repeated and an interrupted adoption finishes). If it still
fails, go to Rollback.

4.4. Close the program and start it again. It now opens normally (the guard says the database is adopted and up to date).

## 5. Rehash the security hashes

Open Dental's `SecurityHash` values cannot be checked by this program. In **Database Maintenance, Tools, Security Hashes**, run the rehash
(Security Admin permission). It gives every patient, and every appointment, claim, claim procedure, payment split, payment plan, insurance
subscriber and verification row in the checked date range, a hash of this program. It does not change any data you see and keeps every row's
last-changed time. Record the time it ran in `D:\Adoption\adoption-log.txt`.

## 6. Validate the adopted copy

```powershell
& "<tools>\SyntheticData.exe" validate --database opendental_pms --as-of 2026-12-31 --out D:\Adoption\after-adoption.json
& "<tools>\SyntheticData.exe" compare D:\Adoption\baseline-copy.json D:\Adoption\after-adoption.json `
  --allowed "<tools>\allowed-differences.adoption.json" > D:\Adoption\compare.txt
& "<tools>\AdoptionValidation.exe" startup-check --database opendental_pms > D:\Adoption\startup-check.txt
```

Expect in `compare.txt`: `Unexpected differences: 0`. The allowed differences are exactly the ones adoption makes: the dropped column,
`SecurityHash` columns, preference rows, the deleted update files, adoption's own security log entry. Every business total (aging buckets,
production, income, payment plans, insurance estimates, appointments) is equal. In `startup-check.txt`, every Open Dental release client
line says `Exits` with `database writes no`.

Then, in the program: open five patients you know in every module (Family, Account, Treatment Plan, Chart, Images, Appointments), plus the
records that use the 24.4 to 25.3 features if the office has them (dated fees, chart letters, mirrored appointments, eForms, pending
insurance, group notes, Check sheets). Print the aging report and compare its totals with the last Open Dental aging report.

Any unexpected difference or wrong screen: go to Rollback (the original is untouched).

## 7. Re-enter the stored passwords

Open Dental encrypted stored passwords with its own proprietary key; this program cannot read them (decoupling plan, ground rule 3;
`CodeBase/Crypto/README.md`). They show as blank, and the integration fails until they are re-entered. Run
`<tools>\AdoptionValidation.exe credentials --database opendental_pms` for the office's exact list, and re-enter each:

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

## 8. Publish the program to the other workstations

On the update server (the workstation named in Update Setup, `WebServiceServerName`), start the release build and accept "publish this
build". Every other workstation installs this program once (or pulls the published release, see [release-process.md](release-process.md)) and
is pointed at `opendental_pms` in the Choose Database window. All workstations switch together.

## 9. Point the other programs at the adopted database

Repoint every program from step 0.4 at `opendental_pms` (same server, same accounts; grant the accounts on `opendental_pms` if they were
database-specific grants). Re-enable the backup jobs for `opendental_pms`.

## 10. Go live

Announce the end of the outage. Keep `D:\Adoption` (backup, reports, logs) until the rollback deadline has passed and the owner has signed
the switch.

## 11. After the rollback deadline

Make a fresh backup of `opendental_pms`. Archive `D:\Adoption\opendental-pre-adoption.sql` offline, then drop the original:
`DROP DATABASE opendental;` (only after the owner agrees). Delete the disabled scheduled tasks of step 2.

## Rollback

Use this when a step above fails, or a problem shows up before the rollback deadline. Anything entered in this program since the switch is
lost and must be re-entered in Open Dental by hand.

1. Close this program on every workstation; stop any program pointed at `opendental_pms`.
2. Check the original is untouched: run `SyntheticData.exe validate --database opendental --as-of <same date> --out D:\Adoption\rollback-check.json`
   and `SyntheticData.exe compare D:\Adoption\baseline-original.json D:\Adoption\rollback-check.json`. Expect `Unexpected differences: 0`.
   - If there are differences (something wrote to it), **restore the pre-adoption backup** instead:
     `DROP DATABASE opendental; CREATE DATABASE opendental CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci;` then load
     `D:\Adoption\opendental-pre-adoption.sql` into it with the command of step 3.2 (database `opendental`), and compare again with
     `baseline-original.json` (expect 0 unexpected differences).
3. Reinstall the Open Dental 25.3.48 client on the workstations and the Open Dental services on the server from the installer (step 0.9),
   using the list from step 0.3. Re-enable the scheduled tasks of step 2. Move `Setup.exe` and `UpdateFiles` back into the A to Z folder.
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
| 0.5 | Password list made, passwords collected | `credentials` output filed | |
| 0.6 | Backup and restore timed | Window planned | |
| 0.7 | Disk space | 2.5 × database size free | |
| 0.8 | Rollback deadline agreed | Date and time | |
| 1 | Everyone out, other writers off | No connections | |
| 2 | Open Dental services stopped and removed, tasks disabled, clients removed, A to Z Setup moved | `Get-Service OpenDent*` empty | |
| 3.1 | Backup | Exit 0, copied off the server | |
| 3.2 | Restore into `opendental_pms` | Exit 0 | |
| 3.3 | Baselines compared | 0 unexpected differences | |
| 4 | Adoption | "Adoption finished.", log saved | |
| 5 | Rehash | Done, time logged | |
| 6 | Validation | 0 unexpected differences; startup check all `Exits`; smoke test and aging report OK | |
| 7 | Passwords re-entered | `credentials` list empty or explained | |
| 8 | Workstations switched | All on this program | |
| 9 | Other programs repointed, backups on `opendental_pms` | Checked | |
| 10 | Live | Announced | |
| 11 | After the deadline: original archived and dropped | Owner agreed | |
