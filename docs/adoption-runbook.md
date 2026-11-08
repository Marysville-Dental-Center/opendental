# Adoption runbook: moving the office from Open Dental 25.3.48 to Open PMS - Dental

Status: 2026-10-25, third revision after review (M1-Z r3), plus steps 0.11 and 7.1 (combined review, 2026-11-01), which since 2026-11-07
list only the five settings Open Dental has no name for (the others carry over under Open Dental's names, SWMAP); milestone M1 switch
gate (M1-Z in [specs/port-backlog.md](specs/port-backlog.md)).
Rehearsed on synthetic data with [tools/AdoptionValidation](../tools/AdoptionValidation/README.md); results in
[m1-validation-report.md](m1-validation-report.md). The tool commands of steps 0.2, 1.1, 3.2 to 6.1 (everything before the program's first
start), 10.1 and 10.2 are the commands the dry run ran, in its order, through a forwarded port as on the admin PC (step 0.0). The commands
on the database server itself (bash: steps 0.0, 3.1, 11.2 and Rollback 2 and 3) and on the workstations were not rehearsed; they first run in
step 0.6 (with the owner's agreement) or in the optional check of step 0.7, otherwise on the day.
Background: [release-process.md](release-process.md) (version guard and adoption tool), [decoupling-plan.md](decoupling-plan.md) (ground rules),
risks R1 and R2 in the backlog.

This is the procedure for the day the office switches. It is written for the office administrator, with a second person checking each
step. Every step says what to run, where, what to expect, and what to do when the result is different. Names in angle brackets, such as
`<db-server>` (the office database server's host name) and `<admin>`, stand for the office's own values: fill them in before the day
("Names used below").

## What adoption does, and what it cannot undo

Adoption converts a copy of the Open Dental 25.3.48 database into a database of this program, with the schema of Open Dental 26.1.70 (the
version this program follows; the office's database stays at 25.3.48 until then). It is done once, by an administrator, on a
**copy**; normal startup never adopts. `Adoption.Run` (run here by `AdoptionValidation adopt`; the program's Adopt Database window calls the
same code):

1. records the start in the `ForkAdoptedFrom` preference
2. drops the office-only column `patient._portal_active_email` and its unique index `portal_active_email_uniq` (owner decision), so family
   members can share an e-mail address again
3. deletes Open Dental's update files (`documentmisc` rows of types `UpdateFiles` and `UpdateFilesSegment`) without reading them
4. blanks the preferences that point at Open Dental's servers
5. sets `DataBaseVersion` and `ProgramVersion` to `99.0.0.0`, so Open Dental programs can no longer use the database
6. writes the fork's version markers and runs the fork migration steps. For a 25.3.48 database the 24.4 to 25.3 schema and seed steps find
   nothing to change. One step may rename report rows: Open Dental 24.4 and 25.2 added three reports (Production by Procedure, Procedures
   (individual), Insurance Pending Supplementals) whose `displayreport` rows Open Dental's own update wrote under internal names that are not
   published. A row of one of them under a name this program does not know, with a matching description, gets this program's name (its
   `InternalName` only), so this program opens that report and the report permissions carry over. Then the Open Dental 25.4 and 26.1 steps
   bring the schema to 26.1.70: five new tables (empty) and 28 new columns (each holding its default in every existing row), claim payment
   notes as long text (a note that is NULL becomes empty; the payment's last-changed time is kept) and the insurance verification
   `DateLastAssigned` as a date and time (midnight of the same date). They add the 25.4 and 26.1 preferences with their defaults, and split
   the blockout permission as Open Dental 25.4 did: grants of PhoneExtension under its 25.3 number (272) move to 274, because 25.4 gave 272 to
   BlockoutAdd, and every user group that may edit blockouts (Blockouts) also gets BlockoutAdd and BlockoutDelete with the same date limits,
   so nobody loses an ability. Then the DXC Patient Credit Score step disables that program link, which Open Dental 25.2 removed because
   DentalXChange discontinued the service (its `program` row's `Enabled` only; the row and its text stay), and deletes its toolbar buttons, if
   the office database still has it enabled or on a toolbar (what Open Dental's own 25.2 update did to the row is not published; most likely
   nothing is left to change). The Trojan Express Collect step (2026110781) does the same for that link, which Open Dental 26.2 deprecated
   because the service is no longer offered. Open Dental's 2019 update (19.2.29) gave every database that link's row with one Account module
   button ("TrojanCollect"), whether or not the office used it, so on nearly every office database this step deletes that one button (the
   row, disabled unless the office used the link, stays with its settings). The button showed nowhere while the link was off. Then the UDS+ report step adds the Standard Reports row of the Open Dental 25.4 UDS+ report ("FQHC UDS+
   Dental Services", Public Health) right after the FQHC Dental Sealant Measure, moves the later Public Health rows down one place, and lets the
   user groups that may run the sealant measure run it too (hidden when the sealant measure is hidden). Then the Insurance History step moves
   the ten Insurance History code preferences of Open Dental 25.3 (Preferences, Treatment Plan) into code groups, as Open Dental 26.1 shows
   Insurance History from code groups: each category whose preference has codes gets a new code group used only for Insurance History
   ("Bitewing History", "Exam History", ...; Show In History, the preference's codes, its first code as History Proc Code, four quadrant
   rows for perio scaling; in no frequency or age limit), after every other group. The existing code groups, the frequency and age limit
   groups among them, are not changed (owner decision of 2026-11-07); the preferences themselves are kept. Then the routine
   maintenance steps create this program's own maintenance tables (empty) and give every user group that has Security Admin the two new
   routine maintenance permissions (Maintenance Setup and Maintenance Record Completion), and the SSH host keys step creates the empty table
   in which the program remembers each SSH server's host key. The settings step (2026110731) moves only rows that release 0.0 wrote under
   names of its own, which an Open Dental database never has, so it changes nothing here. Step 0.2 lists all these changes in advance
   (`migration-preview`); step 4 checks that adoption made exactly them.
7. records the finish and adds a security log entry

All tables are MyISAM: there are no transactions. If anything goes wrong, the way back is the original. That is why the procedure never
writes to the original database: it adopts a copy, and keeps an exact folder copy of the original as the rollback point.

**After the switch, Open Dental programs cannot open the adopted database** (they see a database "newer" than themselves, find no update
files and exit; see the validation report, check V10). **But the original `opendental` stays an ordinary Open Dental database**: an Open Dental
client still pointed at it opens it normally, and whatever is entered there is missing from the adopted database. So every Open Dental client
is removed before the switch (step 2.1), every workstation switches together (step 8), and steps 10 and 11 check that nothing has used the
original since the backup. The same holds the other way after a rollback, which removes this program from every workstation and drops the
adopted database (Rollback, steps 1 and 2). Going back to Open Dental means going back to the original, and anything entered in this program
after the switch must then be re-entered by hand. Decide the rollback deadline before the day (step 0.9).

**The settings Open Dental added in 24.4 to 25.3 carry over** under Open Dental's own names, which the setting names of the office database
gave (owner question Switch-over 1, answered 2026-11-07; [port-notes/SWMAP.md](port-notes/SWMAP.md)): this program reads the office's rows as
they are, and adoption does not change them. `migration-preview` lists them with the value this program will show (step 0.2); step 7.1
compares them. Five settings of this program have no Open Dental setting in the office database; they start at their defaults.

**Adoption does not carry over two things**, and `adopt` does not list them: the stored passwords (step 7), and those five settings of this
program (step 0.11), which Open Dental 25.3.48 has no preference for. The second person looks for them in Open Dental before the day (step
0.11), most likely finds none, and anything found is set again in step 7.1.

### Why steps 3 to 6.1 run without the program

Starting this program and logging in writes to the database: this computer's row and its heartbeat, the Database Maintenance list, signals,
login records, security log entries. Step 6.1 compares the adopted copy with its report from before adoption and accepts only the kinds of
change adoption and the rehash make; eight tables, `preference`, `documentmisc`, `securitylog`, `securityloghash`, `displayreport`,
`grouppermission`, `codegroup` and `toolbutitem`, and the column `program.Enabled`, it can only accept as a whole, and step 4 has checked
those row by row. So the copy, the adoption,
the rehash and that comparison are done with the
command-line tools, exactly as rehearsed, and **nobody starts this program on `opendental_pms` until step 6.1 has passed**. From step 6.2 on,
the program's own writes are normal use.

## Where each command runs

The office database server is `<db-server>`. This runbook is written for MariaDB 10.11 on Linux, installed from a Debian or Ubuntu
package, as on the server the project's schema dump was taken from. Step 0.0 confirms this before anything else; the commands below are
written for it. The tools (`AdoptionValidation.exe`, `SyntheticData.exe`) are Windows programs, so they run on a Windows PC and
reach the server through an SSH forward. They refuse every server but the PC itself (127.0.0.1), whatever the command line says; the
forward is the one, visible, way from there to `<db-server>`.

| Mark | Where | How |
|---|---|---|
| **[server]** | `<db-server>` | `bash` in an SSH session, `ssh <admin>@<db-server>`, as an administrator who can use `sudo`. MariaDB commands there run as `sudo mariadb ...`: the Debian and Ubuntu MariaDB packages let the system administrator in through the local socket without a MariaDB password. |
| **[admin PC]** | One Windows 10 or 11 PC of the office, the same one throughout | PowerShell. Every window starts with the lines of step 0.0.4; the tools then reach `<db-server>` through the forward of step 0.0.3, as the MariaDB account of step 0.0.2. |
| **[workstation]** | Each computer of the step 0.5 list | Its own settings and programs. |

**No copy of the office database is made off `<db-server>` in this procedure.** The copy that is adopted is made by the server itself (step
3.2), and the rollback point is a folder copy on the server (step 3.1). The tools on the admin PC read through the forward (encrypted by
SSH) only what they work on, and keep none of it: `adopt` reads the preferences it changes and, for the rehash, the keys, dates, amounts and
statuses of the rows it hashes; `credentials` reads the rows that hold stored passwords to tell their format, and prints only where they are.
What they write to the admin PC's disk is reports and logs: row counts, checksums, business totals, and names of tables, columns,
preferences, program links and computers. Keeping a second copy of the rollback point off the server, on the office's backup disk, is the
owner's decision (validation report, "For the owner", item 5).

## Names used below

| Name | Meaning |
|---|---|
| `<db-server>`, `<admin>` | The database server's host name, and an administrator account on it that can use `sudo` and log in by SSH. |
| `<db-server-ip>` | The database server's network address, where the workstations' `FreeDentalConfig.xml` gives the server by address. |
| `opendental` | The office database today (Open Dental 25.3.48). Use the real name from `FreeDentalConfig.xml` on any workstation. |
| `opendental_pms` | The adopted copy. Any name works; it must not exist yet. |
| `od_prep_pms` | The adopted copy of the preparation dry run (step 0.6, only with the owner's agreement). A name that must not exist yet. |
| `adoption_admin` | The MariaDB account the tools use (step 0.0.2). |
| `<datadir>` | MariaDB's data folder on `<db-server>` (`@@datadir`, step 0.0.1; normally `/var/lib/mysql/`). The database `opendental` is the folder `<datadir>opendental`. The bash commands below write it as `/var/lib/mysql/`: if step 0.0.1 shows another folder, use that one instead, and the database's real name instead of `opendental`. |
| `<backup>` | `/var/backups/opendental-adoption` on `<db-server>`: the folder copies and dumps of this procedure. Never inside `<datadir>`. |
| `C:\Adoption` | A folder on the admin PC for the reports and logs. |
| `<tools>` | A folder on the admin PC holding a copy of the built `tools\AdoptionValidation\bin\Release` (or `bin\Debug`) folder: `AdoptionValidation.exe`, `SyntheticData.exe`, their DLLs, `allowed-differences.adoption.json`, `sql\business-totals.sql` and `Invoke-RunbookDryRun.ps1`. They need .NET Framework 4.8, which Windows 10 and 11 include, and no MariaDB program on the PC. |
| `<date>` | The switch date, `yyyy-MM-dd`, for every `--as-of`. All reports of one switch use the same date, so their totals can be compared. |

## Timing

Measured by the dry run of these steps with these commands (`Invoke-RunbookDryRun.ps1`) on a development PC (MariaDB 10.11, SSD), synthetic
data, through a forwarded port as on the admin PC (validation report, "Runbook dry run"). That server was shared with other test runs at the
time, so these are upper bounds: the same medium copy took 28 s in one rehearsal run, 24 s in another and 17 s in the dry run. Step 3.1 runs on the server and was
not measured: it copies the database folder once and reads it twice (`cp` and `diff`), so allow two to three times what copying a file of the
database's size takes on `<db-server>`, plus a minute to stop and start MariaDB.

| Step | Medium: 101,886 rows, 1,355 patients, 36 MB of tables | Large: 777,827 rows, 9,158 patients, 286 MB of tables |
|---|---:|---:|
| 0.2 and 1.1 FLOAT check, fork migration preview, no connection to the original | 1.4 s | 2.5 s |
| 3.2 Copy on the server (`AdoptionValidation copy`) | 17.1 s | 70.7 s |
| 3.3 Two reports and their comparison | 5.6 s | 29.9 s |
| 4 and 5 Adoption, fork migration again, verifier, rehash, row check (`AdoptionValidation adopt`) | 6.6 s | 12.0 s |
| 6.1 Post-adoption report, comparison, startup check | 3.6 s | 16.6 s |
| 10.1 and 10.2 Go-live checks of the original | 3.8 s | 16.1 s |
| **Total** | **38.1 s** | **147.8 s** |

The copy and the reports grow with the data; adoption is dominated by one `ALTER TABLE patient` and the rehash by the number of patients.
The office database also holds documents, logs and history the synthetic data does not, and the server is not this PC, and the forward adds
the network between the admin PC and `<db-server>` (the reports and the rows the rehash reads cross it; the copy does not). Plan the outage window from the office's own dry run (step
0.6) when the owner allows it, or else from these figures (step 0.7): the steps above + step 3.1 + password and setting re-entry (step 7) +
workstations + smoke test, plus a margin of 50% (dry run) or 200% (no dry run).

## 0. Preparation (the week before)

0.0. **Platform, access and the tools' account.** Do this first; every later step depends on it.

0.0.1. **[server] Record the platform** (read-only). `ssh <admin>@<db-server>`, then:

```bash
lsb_release -ds; uname -m; systemctl is-active mariadb
sudo mariadb -e "SELECT @@version, @@version_compile_os, @@datadir, @@port, @@bind_address, @@skip_name_resolve"
sudo mariadb -e "SELECT ENGINE, COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='opendental' GROUP BY ENGINE"
sudo mariadb -e "SELECT ROUTINE_TYPE, ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA='opendental'; SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE EVENT_OBJECT_SCHEMA='opendental'; SELECT EVENT_NAME FROM information_schema.EVENTS WHERE EVENT_SCHEMA='opendental'; SELECT TABLE_NAME FROM information_schema.VIEWS WHERE TABLE_SCHEMA='opendental'"
sudo mariadb -e "SHOW DATABASES"
```

Expect: a Debian or Ubuntu release, `x86_64`, `active`; a version `10.11.x-MariaDB-` with the package's suffix (for example
`10.11.x-MariaDB-0+deb12u1`), a compile OS containing `linux`, the data folder
(normally `/var/lib/mysql/`) and port `3306`; one line `MyISAM 422`; no routine, trigger, event or view. Write every value on the
checklist; `<datadir>` below is the data folder shown here. `SHOW DATABASES` lists, besides `opendental`, the system databases
(`information_schema`, `mysql`, `performance_schema`, `sys`): note any other one, because step 3.1 stops the MariaDB server for a few minutes
and so stops it too.

- **Not Linux** (for example `Win64`): the office database has moved to a Windows server. Stop: this runbook is written for a Linux server,
  and the developers must revise it before the day.
- **`@@bind_address`** must let a program on `<db-server>` itself reach MariaDB at `127.0.0.1`, where the forward of step 0.0.3 arrives: `0.0.0.0`,
  `::`, `*`, blank or `NULL` (every address), or a list that includes `127.0.0.1`. If it names only another address (for example only the
  server's network address), stop and ask the developers before the day. Do not change the server's settings yourself: the workstations
  depend on them.
- **A port other than `3306`**: write it down and use it in place of the second `3306` in the forward of step 0.0.3.
- **No SSH login** to `<db-server>` (no SSH server, or no account that can use `sudo`): stop and ask the developers. Do not open MariaDB on
  `<db-server>` to the network for the tools instead; they refuse any address but the PC's own anyway.
- **A table that is not MyISAM**, or a routine, trigger, event or view: stop and tell the developers. The folder copy of step 3.1 is a
  complete backup only of MyISAM tables, and the copy of step 3.2 refuses what it would not carry.

0.0.2. **[server] The tools' MariaDB account.** With the Debian and Ubuntu packages, MariaDB's `root` logs in only through the server's local socket, which a program on
another computer cannot use, so the tools get an account of their own with a password. Choose a password of at least 20 letters and digits
(no other characters: the tools pass it in a connection string) and keep it in the office password manager. Then `sudo mariadb` and, at the
MariaDB prompt:

```sql
CREATE USER 'adoption_admin'@'localhost' IDENTIFIED BY '<password>';
CREATE USER 'adoption_admin'@'127.0.0.1' IDENTIFIED BY '<password>';
GRANT ALL PRIVILEGES ON *.* TO 'adoption_admin'@'localhost';
GRANT ALL PRIVILEGES ON *.* TO 'adoption_admin'@'127.0.0.1';
exit
```

then `sudo rm -f /root/.mariadb_history /root/.mysql_history` (the client's history file would keep the password). The account can only log
in from `<db-server>` itself (the forward of step 0.0.3 arrives there as `localhost` or `127.0.0.1`, depending on `@@skip_name_resolve`, hence both
lines). It needs every privilege: it creates `opendental_pms`, and its checks that nobody is connected need `PROCESS` to see other accounts'
connections (the tools refuse an account without it). It is dropped in step 11.2, or at the end of a rollback.

0.0.3. **[admin PC] The forward.** Windows 10 and 11 include the OpenSSH client. In a PowerShell window of its own, which stays open for as
long as the tools are used:

```powershell
ssh -N -L 127.0.0.1:13306:127.0.0.1:3306 <admin>@<db-server>
```

(The second `3306` is the port of step 0.0.1.) The first time, `ssh` shows the server's key fingerprint: compare it with `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` run [server],
and answer `yes` only if they match. Type the password of `<admin>`; nothing more is printed while the forward runs. From then on, port 13306 of
the admin PC is MariaDB on `<db-server>`. (13306, not 3306, so that it can never be mistaken for a MariaDB the admin PC might run itself.) If the
window is closed or the connection drops, start it again; the tools then fail to connect, and change nothing.

0.0.4. **[admin PC] Every PowerShell window that runs the tools starts with:**

```powershell
$env:SYNTH_DB_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR((Read-Host 'adoption_admin password' -AsSecureString)))
$av = '<tools>\AdoptionValidation.exe'; $sd = '<tools>\SyntheticData.exe'
$db = @('--port','13306','--user','adoption_admin')
New-Item -ItemType Directory -Force C:\Adoption | Out-Null
```

The password is asked for and kept only in this window (not in its history). Every tool command below ends with `@db`, which points it at
the forward and the account. **Check**: `& $av connections --database opendental @db` lists the connections of the workstations that have
Open Dental open, each "from" its computer name, and ends "N connection(s) use opendental" (exit code 6) during office hours, or "No connection
uses opendental." (exit code 0) when everyone is out. Either shows that the tools reach `<db-server>` and see every connection. "Refused: ... lacks
the PROCESS privilege" means the grants of step 0.0.2 are missing; a connection error means the forward is not running or the password is
wrong.

0.1. **Owner sign-off.** The owner has signed [m1-validation-report.md](m1-validation-report.md). The release build of this program is ready,
and the same build is installed on one test workstation (pointed at a synthetic or empty database, never at `opendental`).

0.2. **Versions and FLOAT values.** [server] `sudo mariadb opendental -e "SELECT PrefName,ValueString FROM preference WHERE PrefName IN ('DataBaseVersion','ProgramVersion','CorruptedDatabase','UpdateInProgressOnComputerName')"`.
Expect `25.3.48.0`, `25.3.48.0`, `0` and blank. Any other version: stop; adoption refuses every version except 25.3.48.0, and the office must
not update Open Dental before the switch.

Then, outside office hours (it reads every table that has FLOAT or DOUBLE columns, and MyISAM makes writers wait while a table is read),
[admin PC] `& $av float-check --database opendental @db > C:\Adoption\float-check.txt`. Exit code 0 ("no value that a logical backup
(mariadb-dump) would round") or 6 (a list of columns and counts) are both normal. MariaDB writes a FLOAT as text with 6 significant digits,
and Open Dental computes some FLOAT values with more (an image's font size, for example), so a logical backup (`mariadb-dump`) and restore
rounds those: every program shows them with the same 6 digits before and after, but the stored values differ. This procedure does not use a
logical backup to copy the database (steps 3.1 and 3.2 copy the stored values exactly), so nothing is to be done on the day; keep the file
for the owner, because it says what the office's own nightly backups would round if they are `mariadb-dump` files (validation report, F12).

Then, at any time (it reads only the `displayreport`, `grouppermission`, `program`, `toolbutitem`, `codegroup` and `procedurecode` tables, the
preferences, and which claim payment notes are NULL),
the **fork migration preview**: [admin PC]
`& $av migration-preview --database opendental @db | Tee-Object C:\Adoption\migration-preview.txt` (exit code 0). It lists what adoption's
fork migration (step 4) will change in the rows of `opendental`: the report rows of item 6 of "What adoption does". It prints one line for
each of the three reports:

- `... is renamed; the adoption log says "Renamed displayreport <old name> to <new name> (same description)".`: a rename;
- `... already has this name; nothing is renamed.`: nothing to do for that report;
- `... rows under names this program does not know have a matching description ...; nothing is renamed.` (two candidates, so neither is
  taken) or `... no row has this name or a matching description under an unknown name; nothing is renamed.`: nothing changes in the
  database, but this program will not find that report's row (Report Setup will not list it). Give the line to the developers before the
  day; it does not stop the switch.

The line `Adoption's fork migration will rename N displayreport row(s), changing only their InternalName: ...` (or `... will rename no
displayreport row.`) sums them up. **Write each rename (old name, new name, DisplayReportNum) on the checklist**, or "none": step 4 must show
exactly these. No rename at all is just as normal.

The last lines are the Open Dental 25.4, 26.1 and 26.2 steps of item 6 and the feature steps after them:

- `Steps 2026102401 to 2026102403 and 2026110701 (Open Dental 25.4, 26.1 and 26.2 schema) bring the schema to Open Dental 26.2.0: 8 new
  table(s), empty (...); 35 new column(s) in 24 table(s) ...; ... claimpayment.Note (text NOT NULL: N NULL note(s) become empty) ...`;
- `Step 2026102501 (Open Dental 25.4 and 26.1 seed rows) inserts N preference(s) with their defaults: ...` (10 on an Open Dental database:
  the names are this program's own);
- `Step 2026102501 splits the blockout permission (...): it moves N Open Dental 25.3 PhoneExtension grant(s) from 272 to 274, then grants
  BlockoutAdd (272) to N user group(s) and grants BlockoutDelete (273) to N user group(s), with the FKey and date limits of their Blockouts
  grant.` (or `... BlockoutDelete is already configured, so it is not granted`);
- `Step 2026110101 (DXC Patient Credit Score program link) changes nothing: the discontinued DXC Patient Credit Score program link (ProgramNum
  N) is already disabled and has no toolbar button.` (what Open Dental's own 25.2 update most likely left; `... no program row is the DXC
  Patient Credit Score link ...` is just as normal), or `Step 2026110101 (DXC Patient Credit Score program link): the discontinued DXC Patient
  Credit Score program link (ProgramNum N) stays, with its text; the step disables 1 program row(s) of it (ProgramNum N), Enabled only and
  deletes its N toolbar button(s) (ToolButItemNum N on <toolbar>, ...) ...`. It quotes the lines the adoption log will show ("Deleted N toolbar
  button(s) of the discontinued DXC Patient Credit Score program link (...)", "Disabled the discontinued DXC Patient Credit Score program link
  (ProgramNum N)"). Its buttons pointed at a service that no longer exists, so the office loses nothing;
- `Step 2026110781 (Trojan Express Collect program link): the discontinued Trojan Express Collect program link (ProgramNum N) stays, with
  its text and properties; the step deletes its 1 toolbar button(s) (ToolButItemNum N on AccountModule) ...`, quoting the adoption log line
  "Deleted 1 toolbar button(s) of the discontinued Trojan Express Collect program link (ToolButItemNum N)". This is the normal line: Open
  Dental's 19.2.29 update gave every database the row, disabled unless the office used the link, with that one Account module button
  ("TrojanCollect"). If the office used Trojan Express Collect, the line also says `the step disables 1 program row(s) of it (ProgramNum N),
  Enabled only` and quotes "Disabled the discontinued Trojan Express Collect program link (ProgramNum N)". `... changes nothing: ...` (in the
  DXC step's words) appears only where the row is disabled and someone removed its button, or there is no row. Open Dental 26.2 deprecated
  the link because the service is no longer offered (package P262-LINKS);
- `Step 2026110103 (UDS+ report row) adds the displayreport row ODUdsPlus ("FQHC UDS+ Dental Services", Public Health, ItemOrder N) right
  after the FQHC Dental Sealant Measure (DisplayReportNum N), moving N later Public Health row(s) down one place, ItemOrder only (...); it
  gives the N user group(s) that may run the sealant measure (user group ...) the Reports permission for it ...` (on an Open Dental 25.3.48
  database; `... at the end of Public Health` when there is no sealant measure row, and `... copies no report permission` when no group has a
  grant for it alone). It quotes the lines the adoption log will show ("Added displayreport ODUdsPlus", "Copied N report permission(s) ...").
- `Step 2026110104 (Insurance History rows from code groups) adds N code group(s) used only for Insurance History and changes no existing
  code group (...): Added code group 'Bitewing History' (ItemOrder N), used only for Insurance History (not in frequency or age limits),
  History Proc Code ..., codes ...; ...` with one part per added group ("with four quadrant rows" for perio scaling), then "Insurance
  History: the existing code groups, ... were not changed; ...". These parts are the lines the adoption log will show. On a database whose
  ten Insurance History preferences are all empty it says `changes nothing: none of the ten InsHist*Codes preferences has a value`.
- `Step 2026110206 (routine maintenance permissions) adds N grouppermission row(s) (FKey 0, no date limit) for the N user group(s) with
  Security Admin: MaintenanceSetup to N (user group ...); MaintenanceRecord to N (user group ...).` It quotes the lines the adoption log will
  show ("Granted MaintenanceSetup to N Security Admin user group(s)", the same for MaintenanceRecord). Every Open Dental database has a
  Security Admin group, so this is normal: the people who run the program's security get the new routine maintenance permissions and can
  give them to others. `... changes nothing: each of the N user group(s) with Security Admin has both permissions already.` is just as
  normal.
- `Steps 2026110211 to 2026110213 (inventory tracking) create the tables of this program supplyledger, supplylot, supplyproccode,
  supplysetting, supplyprocpending, supplycount, supplycountline and change no Open Dental table; the stock ledger gets N opening balance
  row(s), one per supply with stock (SupplyNum ...), LevelOnHand rounded to 2 decimals at the supply's price; supply itself is not changed.
  Tracking stays off ...` (or `... no supply has stock (supply.LevelOnHand), so the stock ledger starts empty. ...`). The stock ledger is the
  one table of this program that adoption writes rows into; step 4 holds it to exactly these rows, and the others, the stock counts too, to
  empty.
- `Settings read under Open Dental's names (adoption keeps these rows as they are; ...): Sales Tax procedure code (B-ACCT): <code or blank>
  [SalesTaxProcCode=...]; Prompt for Medical EOB on primary claims (B-CLAIM): ticked [ClaimCheckForMedicalEOB='1']; ...` with one part per
  setting of "Settings that carry over by Open Dental's name" (step 0.11): what this program will show (ticked, not ticked, a code or a
  number of milliseconds) and the office's stored rows (`no row` for a setting the office never changed). Adoption changes none of these
  rows. **Write the shown values on the checklist** (step 0.2): step 7.1 compares them with this program's windows.
- `Step 2026110731 (settings under Open Dental's names) changes no preference row: the database has no setting row of release v0.0.` This
  step only moves rows that a database release 0.0 used wrote under this program's own names; an Open Dental database has none.
- `Plug-ins: N program row(s) name a plug-in DLL (PluginDllName); a plug-in built for Open Dental does not load in this program ...: <ProgName>
  (ProgramNum N, enabled, <dll>); ...` (or `Plug-ins: no program row names a plug-in DLL ...`). Adoption changes none of these rows, but a
  plug-in built against Open Dental's assemblies stops loading after the switch, without a message. Give the list to the developers before
  the day: each such plug-in must be rebuilt against this program's assemblies or done without.

**Write the numbers on the checklist**: NULL notes, preferences, PhoneExtension grants moved, user groups for BlockoutAdd and for
BlockoutDelete, the DXC Patient Credit Score row disabled (ProgramNum, or "none") and its buttons deleted (ToolButItemNum, or "none"), the
same two for the Trojan Express Collect row, the
UDS+ row's DisplayReportNum of the sealant measure, the Public Health rows moved, the user groups granted the UDS+
report, the user groups granted the two routine maintenance permissions, the code groups added for Insurance History (name and History Proc
Code), and the opening balance rows of the stock ledger. Step 4 must show exactly these. Zero PhoneExtension grants is
normal (Open Dental's Set All grants it, so it is there only where
someone used Set All).

0.3. **Inventory of Open Dental services and jobs** (for step 2): on every Windows computer of the office that runs Open Dental, its services
or its scheduled tasks (the workstations, and any Windows server; `<db-server>` runs none of them, being Linux):

```powershell
Get-Service OpenDent* | Format-Table Name,DisplayName,Status,StartType
Get-CimInstance Win32_Service | Where-Object { $_.PathName -match 'OpenDent' } | Format-Table Name,PathName
Get-ScheduledTask | Where-Object { ($_.Actions | ForEach-Object { $_.Execute }) -match 'OpenDent' } | Format-Table TaskPath,TaskName,State
```

Expect some of: `OpenDentalService` (Open Dental Service), `OpenDentalEConnector` (eConnector), the API service (`OpenDentalAPIService.exe`),
`OpenDentalHL7`, `OpenDentalReplicationService`, the legacy `OpenDentalCustListener`, and scheduled tasks that start Open Dental programs. The
second command also finds services installed under another name. Also note the middle tier web service (`OpenDentalServer` in IIS), if the
office uses it. Write the list down, per computer.

0.4. **Other programs that use the database directly** (reporting tools, imaging or phone bridges, backup scripts): list the MariaDB accounts
[server] `sudo mariadb -e "SELECT user,host FROM mysql.user"`, the jobs on `<db-server>` (`sudo crontab -l; ls /etc/cron.d /etc/cron.daily;
systemctl list-timers --all --no-pager`), and ask each vendor. They are paused in step 1.2 and pointed at `opendental_pms` in step 9.

0.5. **Workstations.** For every computer that runs Open Dental, write down: its name, the Open Dental folder (usually
`C:\Program Files (x86)\Open Dental`), and the server, database and user in that folder's `FreeDentalConfig.xml`. This list is used in steps
2.1, 8 and 10, and in Rollback step 1. If the user is not `root`, also record that account's rights: [server] `sudo mariadb -e "SHOW GRANTS FOR '<user>'@'<host>'"`
(step 2.1 may revoke them on `opendental`).

0.6. **Preparation dry run: only if the owner agrees.** This makes a copy of the office database on `<db-server>` (it never leaves the server), to
list the passwords to re-enter, to time the steps, and to run every tool command of the day once on real data before the day. It is office
data on the office server; but the owner's decisions so far rule out office data for the rehearsal, and this one is **still open**
(validation report, "For the owner", item 3). Record the owner's answer on the checklist. If the answer is no, skip to 0.7.

Do all of 0.6 **outside office hours**, with nobody working: the copy and the reports read every table, and `connections`, `copy` and
`adopt` refuse to run while anyone is connected to `opendental`, **the Open Dental services included** (they stay connected day and night).

0.6.0. **Nobody connected, for the night.** First check the disk space (step 0.8). Every Open Dental program closed on every workstation. On
each computer of the step 0.3 list, in
PowerShell as administrator, stop (do not remove) every Open Dental service that is running: `Get-Service OpenDent* | Stop-Service -Force`,
and `Stop-Service -Name <name> -Force` for each service of the list under another name. Pause the programs and jobs of step 0.4. Then
[admin PC] (window as in 0.0.4) `& $av connections --database opendental @db`: expect "No connection uses opendental." (exit code 0); a line
names the computer that is still connected. Until 0.6.5, the services' work (eServices such as the patient portal and text messages,
statements, scheduled processes) waits.

0.6.1. **[server] Folder copy first**, as in step 3.1 but into `<backup>/prep`. It is the safety net for the night, and it times step 3.1
(the two `date` lines):

```bash
sudo mkdir -p /var/backups/opendental-adoption/prep && sudo chmod 700 /var/backups/opendental-adoption /var/backups/opendental-adoption/prep
date
sudo test ! -e /var/backups/opendental-adoption/prep/opendental-files && sudo systemctl stop mariadb && sudo cp -a /var/lib/mysql/opendental /var/backups/opendental-adoption/prep/opendental-files && sudo diff -rq /var/lib/mysql/opendental /var/backups/opendental-adoption/prep/opendental-files && echo IDENTICAL
sudo systemctl start mariadb; systemctl is-active mariadb; date
```

Expect `IDENTICAL` and `active`, and write down the time between the two dates (`<datadir>`: see "Names used below"). Without
`IDENTICAL`: stop the preparation, start the services again (0.6.5), and give the output to the developers.

0.6.2. **[admin PC] Dry run of the tool commands** (one command, from a window started as in 0.0.4):

```powershell
& '<tools>\Invoke-RunbookDryRun.ps1' -Original opendental -Copy od_prep_pms -Folder C:\Adoption\dry-run -AsOf <date> -Port 13306 -User adoption_admin
```

It runs steps 0.2 (FLOAT check and fork migration preview), 1.1, 3.2, 3.3, 4, 5 and 6.1 and the go-live checks 10.1 and 10.2 with the
commands below, with `od_prep_pms` standing in for `opendental_pms`, and checks that `adopt`'s log names exactly the report row renames the
preview listed. It never writes to `opendental` and stops at the first unexpected result. Expect `Dry run: PASS`.
`C:\Adoption\dry-run\dry-run-timings.txt` holds the time of every step: plan the outage window from it and 0.6.1, plus 50% (see Timing). Any
other result: stop, keep the folder (its reports hold row counts, checksums and business totals, no patient data) and give `compare.txt`,
`adoption-log.txt` and `dry-run-timings.txt` to the developers, with the owner's agreement for the totals. The switch waits until a dry run
passes. To run it again, first drop `od_prep_pms` (0.6.4) and use a new folder.

0.6.3. **[admin PC] Passwords to re-enter.** Make the exact list for this office (step 7 explains why):
`& $av credentials --database od_prep_pms @db > C:\Adoption\credentials-prep.txt`. It prints where each password is (program link,
clearinghouse, preference, ...) and never the value. Collect the passwords from the vendors or the office password manager.

0.6.4. **[server] Clean up the same night:** `sudo mariadb -e "DROP DATABASE od_prep_pms"` (check the name twice; never `opendental`) and
`sudo rm -r /var/backups/opendental-adoption/prep` (a copy of office data; the nightly backups remain). Keep `C:\Adoption\dry-run` and
`credentials-prep.txt`.

0.6.5. **Services back on, the same night.** On each computer of the step 0.3 list, start again the services that 0.6.0 stopped
(`Start-Service -Name <name>`), and resume the programs and jobs of step 0.4. Check [admin PC]: `& $av connections --database opendental @db`
lists the services' connections again (exit code 6).

0.7. **If the owner says no to 0.6:**

- **Passwords:** the exact list is made on the day, in step 7, by running `credentials` on `opendental_pms`. So before the day, collect the
  passwords of every integration in the table of step 7 that the office uses, from the office password manager and the vendors, so that step 7
  is only typing.
- **Window:** from the rehearsal's figures. Read the database size (table metadata only, no rows): [server]
  `sudo mariadb -e "SELECT ROUND(SUM(DATA_LENGTH+INDEX_LENGTH)/1048576) AS MB FROM information_schema.TABLES WHERE TABLE_SCHEMA='opendental'"`.
  Scale the large dry run's total (Timing: 148 s for 286 MB of tables) by size, for example for 3 GB: 148 s × 3000 / 286, about 26 minutes.
  Add step 3.1 (Timing) and 200% for the unknown server, then password re-entry, the workstations and the smoke test.
- **Tools and server check (optional, recommended):** outside office hours, with synthetic data only, through the forward:
  `& $av rehearse --size large --runs 1 --keep --skip-roundtrip --tag office --schema-from opendental --out C:\Adoption\rehearsal @db`
  (it reads only the table definitions of `opendental`, never its rows, and writes only `synth_m1z_office_*` databases), then
  `& '<tools>\Invoke-RunbookDryRun.ps1' -Original synth_m1z_office_r1_src -Copy synth_m1z_office_pms -Folder C:\Adoption\dry-run-synthetic -AsOf 2026-09-30 -Port 13306 -User adoption_admin`,
  and [server] step 3.1's folder copy of the synthetic database. It stops MariaDB for a moment, so first stop the Open Dental services as
  in 0.6.0, and start them again afterwards as in 0.6.5:

  ```bash
  sudo mkdir -p /var/backups/opendental-adoption/synthetic && sudo chmod 700 /var/backups/opendental-adoption /var/backups/opendental-adoption/synthetic
  sudo test ! -e /var/backups/opendental-adoption/synthetic/files && sudo systemctl stop mariadb && sudo cp -a /var/lib/mysql/synth_m1z_office_r1_src /var/backups/opendental-adoption/synthetic/files && sudo diff -rq /var/lib/mysql/synth_m1z_office_r1_src /var/backups/opendental-adoption/synthetic/files && echo IDENTICAL
  sudo systemctl start mariadb; systemctl is-active mariadb
  ```

  Expect `Rehearsal: PASS`, `Dry run: PASS`, `IDENTICAL` and `active`. Then [server] drop `synth_m1z_office_r1_src`, `synth_m1z_office_r1_adopted`,
  `synth_m1z_office_r1_snap` and `synth_m1z_office_pms` (`sudo mariadb -e "DROP DATABASE <name>"`, each name checked twice), and
  `sudo rm -r /var/backups/opendental-adoption/synthetic`.

0.8. **Disk space.** [server] `sudo du -sh <datadir>opendental` and `df -h <datadir> /var/backups`. The disk of `<datadir>` needs free space of
at least 1.5 times the size of the `opendental` folder (for `opendental_pms`), and the disk of `/var/backups` at least 1.2 times (the folder
copy); 2.7 times if they are the same disk. The same for the night of step 0.6.

0.9. **Rollback deadline.** Agree with the owner how long Open Dental remains the fallback (for example: until the end of the first working day
after the switch). After the deadline the original database is archived (step 11) and rollback means re-entering the work done since.

0.10. **Open Dental installers.** Keep the Open Dental 25.3.48 installer and the office's registration details at hand; rollback needs them.

0.11. **Settings that do not carry over** (see "What adoption does"). These five settings of this program have no preference in the office's
Open Dental 25.3.48 database (its setting names show none; [port-notes/SWMAP.md](port-notes/SWMAP.md)), so this program keeps each in the
row of its own in the third column, and after adoption each shows its default. Open Dental may still offer a matching box somewhere (the
inserter barcode, for example, may belong to its statement layout). In the week before the day, in Open Dental 25.3.48, the second person
looks for each box below and writes in the fourth column what it holds: ticked or not ticked, the number, or the folder; most likely the
box is not there, and is written "not found". Change nothing in Open Dental. The switch does not wait for this table. These are settings,
no patient data; keep the filled table with the checklist. Step 7.1 sets each value found again in this program.

| # | Window and box (as this program names them) | Read from (package) | In Open Dental 25.3.48 (0.11) | Set in this program (7.1) | Initials |
|---|---|---|---|---|---|
| 1 | Preferences, Server Connections - General, Replication: "Write delay for signals (ms)" | `ForkReplicationSignalLookbackMs` (B-SERVER) | | | |
| 2 | Report Setup, Misc Settings: "Include all procedures with no notes in the Incomplete Procedures Report" | `ForkReportsIncompleteProcsAllNoNotes` (B-SERVER) | | | |
| 3 | Billing Defaults: "Print inserter barcode on statements" | `BillingStatementInserterBarcode` (B-PRINT) | | | |
| 4 | Wiki Setup, Read-only copy of the wiki: on or off, and its folder | `ForkWikiReadOnlyExportPath` (B-WIKI) | | | |
| 5 | Wiki Setup, Read-only copy of the wiki: "Update the copy after each save" | `ForkWikiReadOnlyAutoExport` (B-WIKI) | | | |

The defaults after adoption: rows 2 and 3 not ticked, row 1 `0`, row 4 blank (no read-only copy), row 5 ticked.

**Settings that carry over by Open Dental's name.** This program reads these from the office's own rows, under the names in the second
column, so nothing is recorded here: `migration-preview` (step 0.2) lists what each will show, and step 7.1 compares them.

| Window and box (as this program names them) | Package: Open Dental's row | Note |
|---|---|---|
| Preferences, Account - General, Adjustments, Sales Tax: "Sales Tax procedure code" | B-ACCT: `SalesTaxProcCode` | A procedure code, or the CodeNum of one |
| Preferences, Account - General, Claim - Receive, Claims Medical: "Prompt for Medical EOB on primary claims" | B-CLAIM: `ClaimCheckForMedicalEOB` | |
| Preferences, Appointment - General, Appearance: "Show provider time on the operatory background in the provider's color" | B-APPT: `ApptProvbarShowOnOps` | |
| Preferences, Appointment - General, Appearance: "No provider time set: show the secondary provider color in the time bar" | B-APPT: `ApptUseSecProvTimeIfEmpty` | |
| Preferences, Main Window - General, Miscellaneous: "Blur when switching between modules to prevent accidental clicking" | B-SHELL: `BlurModuleOnLoad` | |
| Preferences, Manage - General, ERA: "Auto split ERAs with CARC denial codes listed above" | B-ERA: `EraAutoProcessSplitByCarcCodes` | |
| Preferences, Server Connections - General, Replication: "Read delay after write (ms)" | B-SERVER: `ReplicationWriteReadDelayEnabled` and `ReplicationWriteReadDelayMS` | The milliseconds count only while Enabled is 1 |
| Lock Date (the global lock date): "Payment plans are not affected by the global lock" | B-PAY: `PayPlansIncludeInGlobalLock` | **Inverted**: Open Dental's 1 (payment plans included in the global lock) shows as not ticked, 0 as ticked |
| Perio Setup (from the Perio Chart): "Use the term "Recession" (reverts to Gingival Margin when hyperplasia is charted)" | B-PERIO: `PerioRecessionInsteadOfGM` | |
| Perio Chart, Numbers in red: "Att Ging (<=)" | ODOPTS: `PerioRedAttGing` | Blank or no row: no threshold |
| Global Security Settings, Domain Login: "Offer domain login in signature boxes' Change User" | ODOPTS: `DomainLoginForSigBoxes` | No row: ticked |
| Preferences, Account - General, Claim - Receive, Secondary Claims: "Update the fee on unsent secondary claims when a procedure fee changes" | ODOPTS: `ClaimUpdateFeeForSecondary` | No row: not ticked. Ticked: the procedure window, Edit All and an appointment's provider change all update the unsent secondary claims of a re-priced procedure (owner question ODOPTS 3) |
| eForm Setup: "Import a typed allergy that matches no allergy in the list as Other" | ODOPTS: `EFormImportAllergiesAsOther` | No row: ticked. 0: a typed allergy or problem that matches no list entry is not auto-imported (it is listed in the form's Audit Trail entry and shown to whoever saves the form); settle ODOPTS 4 before the day |
| eForm Setup: "Import a typed problem that matches no problem in the list as Other" | ODOPTS: `EFormImportProblemsAsOther` | No row: ticked. 0: a typed allergy or problem that matches no list entry is not auto-imported (it is listed in the form's Audit Trail entry and shown to whoever saves the form); settle ODOPTS 4 before the day |
| Preferences, Treat Plan - General, Frequency Limitations, Insurance History: "Perio scaling procedure code" | ODOPTS: `InsHistPerioScalingCode` | Blank: each perio scaling code group's own History Proc Code |
| Billing Defaults, Send Setup: "By e-mail" | ODOPTS: `StatementAutoSendEmail` | **The meaning differs from Open Dental's**: Open Dental's Statement Send Setup sends Message-to-Pay links when statements are created (the 25.1 release notes); this program e-mails the statement itself (INFERRED). Before the day, settle owner question ODOPTS 6 in this order: does the office use the setting for Message-to-Pay, does it want it to mean "e-mail the statement" here, and only then which value is which (read here as 0 Never, 1 Ask, 2 Always). A stored Always asks before e-mailing until Statement Send Setup is saved in this program; saving it there with Always makes e-mail statements go out as soon as billing creates them |
| Billing Defaults, Send Setup: "By text" | ODOPTS: `StatementAutoSendSMS` | In Open Dental most likely Message-to-Pay by text. Nothing is texted: this program has no texting provider |

## 1. Start of the outage

1.1. Tell everyone to close Open Dental. [admin PC] (window as in 0.0.4, forward running) `& $av connections --database opendental @db`. Each
line "connection ... from <computer>:<port> to opendental" names a computer that is still connected. The Open Dental services stay connected
until step 2 stops them, so lines from the computers that run them (step 0.3 list) are expected here (exit code 6). For any other computer:
close Open Dental there, and run the command again until only the services' lines remain, or "No connection uses opendental." (exit code 0).
Step 3.1 repeats this check after step 2, and there it must say "No connection uses opendental.".

1.2. Pause anything else that writes to the database (step 0.4 programs and jobs).

## 2. Stop and uninstall the Open Dental services and clients

They connect to the same database, talk to Open Dental's servers, and would keep writing (statements, eServices, scheduled processes). On each
computer of the step 0.3 list, in PowerShell as administrator:

```powershell
# Stop every Open Dental service, then remove it. Keep the list from step 0.3 for rollback.
Get-Service OpenDent* | Stop-Service -Force
Get-Service OpenDent* | ForEach-Object { sc.exe delete $_.Name }
# Services under another name that run an Open Dental program (step 0.3, second command): stop and remove each by its name.
Get-CimInstance Win32_Service | Where-Object { $_.PathName -match 'OpenDent' } | ForEach-Object { Stop-Service -Name $_.Name -Force; sc.exe delete $_.Name }
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

Expect, run again on each computer: `Get-Service OpenDent*` and the `Win32_Service` query of step 0.3 (`PathName -match 'OpenDent'`) both
return nothing, and the scheduled-task query lists only tasks in state `Disabled`. If a service will not stop, end its process in Task Manager
and repeat. A service of the 0.3 list that runs no Open Dental file but still uses the database (a bridge, for example) belongs to step 1.2.

2.1. **Open Dental clients: remove them from every workstation of the step 0.5 list.** Removing shortcuts is not enough. The original
`opendental` stays an ordinary Open Dental database, and an Open Dental client that still points at it opens it normally (validation report,
check A2): whatever is entered there is missing from `opendental_pms` and would be lost when the original is dropped. On each workstation:

1. Uninstall Open Dental (Settings, Apps; or Control Panel, Programs and Features).
2. If the Open Dental folder is still there, rename `FreeDentalConfig.xml` in it to `FreeDentalConfig.xml.before-switch` (keep it for
   rollback), and delete any Open Dental shortcut left on the desktop, the Start menu or the taskbar.
3. Check: `Get-ChildItem 'C:\Program Files*\Open Dental*' -Recurse -Include OpenDental.exe,FreeDentalConfig.xml -ErrorAction SilentlyContinue`
   lists nothing. An `OpenDental.exe` is always Open Dental's own program (this program is `OpenPmsDental.exe`). This program keeps the
   file name `FreeDentalConfig.xml` in its own folder, so the check looks only in the `Open Dental*` folders.
4. Mark it in the table of step 8.

If the workstations connect with their own MariaDB account (step 0.5: the user is not `root`) and `SHOW GRANTS` shows its rights on
`opendental` only (``ON `opendental`.*``), also revoke them now, so that even a forgotten Open Dental client cannot open the original:
[server] `sudo mariadb -e "REVOKE ALL PRIVILEGES ON opendental.* FROM '<user>'@'<host>'"`. Keep the `GRANT` lines from step 0.5 for rollback;
step 8 grants the same rights on `opendental_pms`. With `root` or rights on `*.*` this is not possible; steps 8 and 10 then catch a forgotten
client.

2.2. **A to Z folder.** Move Open Dental's `Setup.exe` and any `UpdateFiles` folder out of the root of the A to Z folder into a folder
`atoz-od-files` beside it (do not touch the patient folders). An Open Dental client in "classic" update mode launches the `Setup.exe` it
finds there.

**From here on, Open Dental is no longer installed anywhere. If any later step says to stop, go to Rollback**: it reinstalls Open Dental on the
untouched original.

## 3. Back up and copy

3.1. **[server] The rollback point: a folder copy of the database, with MariaDB stopped.** First [admin PC] repeat 1.1: now that step 2 has
stopped the services, it must say "No connection uses opendental." (exit code 0). Write down the time the first `date` prints (steps 10, 11
and "Work entered in the original after the backup" refer to it). The commands use the data folder `/var/lib/mysql/`; if step 0.0.1 showed
another `<datadir>`, use that instead:

```bash
sudo mkdir -p /var/backups/opendental-adoption && sudo chmod 700 /var/backups/opendental-adoption
date
sudo test ! -e /var/backups/opendental-adoption/opendental-files && sudo systemctl stop mariadb && sudo cp -a /var/lib/mysql/opendental /var/backups/opendental-adoption/opendental-files && sudo diff -rq /var/lib/mysql/opendental /var/backups/opendental-adoption/opendental-files && echo IDENTICAL
sudo systemctl start mariadb; systemctl is-active mariadb
```

Expect `IDENTICAL`, then `active`. The long line stops at the first part that fails: the target folder already exists (`cp` would copy
into it), MariaDB did not stop, the copy failed, or `diff` found a difference. The last line always starts MariaDB again. Without
`IDENTICAL`: move the target folder aside if it exists (`sudo mv /var/backups/opendental-adoption/opendental-files /var/backups/opendental-adoption/opendental-files-failed`)
and run the step again; a second failure: Rollback. If MariaDB does not start: stop, read `sudo journalctl -u mariadb -n 50`, and go to
Rollback when it runs again.

Every table is MyISAM (step 0.0.1), so with the server stopped the folder holds the whole database, and `diff` shows the copy is byte for byte
the same, FLOAT values included. The forward of the admin PC survives the restart. If the owner agreed to a second copy off the server
(validation report, "For the owner", item 5), make it now, for example
`sudo tar -C /var/backups/opendental-adoption -czf /media/<backup disk>/opendental-files.tgz opendental-files`.

3.2. **[admin PC] The copy that is adopted, made by the server:**

```powershell
& $av copy --original opendental --database opendental_pms @db | Tee-Object C:\Adoption\copy.txt
```

Expect "Copy: PASS" (exit code 0). It refuses, and creates nothing (exit code 2), when `opendental_pms` exists, `opendental` does not, someone
is connected to either, or `opendental` has a view, trigger, routine or event it would not carry. Then it copies every table with its
definition, its `AUTO_INCREMENT` counter and every stored value, on the server, and checks every table's row count and counter. If it
fails (exit code 1, "Copy: FAIL"): [server] `sudo mariadb -e "DROP DATABASE opendental_pms"`, then run it once more; a second failure: Rollback.

3.3. **[admin PC] Reports of both databases** (read-only):

```powershell
& $sd validate --database opendental --as-of <date> --out C:\Adoption\baseline-original.json @db
& $sd validate --database opendental_pms --as-of <date> --out C:\Adoption\baseline-copy.json @db
& $sd compare C:\Adoption\baseline-original.json C:\Adoption\baseline-copy.json | Tee-Object C:\Adoption\compare-baselines.txt
```

Expect `Unexpected differences: 0` (exit code 0): the copy is exact (row counts, `CHECKSUM TABLE`, a checksum of every column with FLOAT
values read exactly, and every business total). The reports hold row counts, checksums and totals, no patient data; keep them.

Any difference: do not adopt. [server] `sudo mariadb -e "DROP DATABASE opendental_pms"`, repeat 3.2 and 3.3 once, and if a difference remains,
go to Rollback and give `compare-baselines.txt` to the developers. A line `ChecksumTableChanged <table>` with `[note: row count and every column
checksum are equal ...]` and no other line for that table means the stored rows of that table differ while every value reads the same (a
row or date format, or a value the column checksums cannot see). The server-side copy copies the stored values and the table definition, so
this is not expected; it is still a stop, and whether such a table may be accepted is decided by the owner with the developers, never during
the outage.

**Do not start this program, or any other program, on `opendental_pms` until step 6.1 has passed.**

## 4. Adopt the copy, without starting the program

```powershell
& $av adopt --database opendental_pms --original opendental --confirm opendental_pms --log C:\Adoption\adoption-log.txt @db
```

It checks first and changes nothing if a check fails ("Refused: ...", "Nothing was changed.", exit code 2): `--confirm` repeats the database
to adopt; `opendental` is a different, unadopted Open Dental database; nobody is connected to either (step 1.1); and `opendental_pms` has
`opendental`'s tables and exactly its row counts, and was created after it (so the two names are not the wrong way round). Then it adopts and
prints each step with its time; `adoption-log.txt` keeps everything. Expect, in this order:

- "Open Dental 25.3.48 database. It can be adopted." and "Adoption from Open Dental 25.3.48.0 started."
- "Dropped index patient.portal_active_email_uniq." and "Dropped column patient._portal_active_email."
- "Deleted N Open Dental update file row(s)." and "Blanked N preference(s) that pointed at Open Dental's servers: ..." Check the names: they
  are preferences that held an address of Open Dental's servers (such as `UpdateServerAddress`, `WebServiceHQServerURL`, `UpdateWebsitePath`).
  The second person initials the list on the checklist.
- "DataBaseVersion and ProgramVersion set to 99.0.0.0." and the fork markers
- every Open Dental 24.4 to 25.3 fork migration step (2026101901 to 2026101951) "found nothing to change", and step 2026101971 ("Report rows
  for Procedures, Production by Procedure and Insurance Pending Supplementals") too unless step 0.2 listed renames: then it "changed the
  database:", followed by one line "Renamed displayreport <old name> to <new name> (same description)" for each rename of the step 0.2 list
  and no other line
- steps 2026102401 ("Open Dental 25.4 schema") and 2026102402 ("Open Dental 26.1 schema") "changed the database:", each followed by its
  `ALTER TABLE` and `CREATE TABLE` statements; step 2026102403 ("Open Dental 26.1: claimpayment.Note as text") by
  ``UPDATE `claimpayment` SET `Note`='',`SecDateTEdit`=`SecDateTEdit` WHERE `Note` IS NULL -- N row(s)`` (N the NULL notes of step 0.2;
  without NULL notes the line still shows, with 0) and its `ALTER TABLE`
- step 2026102501 ("Open Dental 25.4 and 26.1 seed rows") "changed the database:", followed by one line "Inserted preference <name>" for each
  preference of the step 0.2 list, "Moved N Open Dental 25.3 PhoneExtension grant(s) from 272 to 274" if step 0.2 counted any, and "Granted
  BlockoutAdd to N user group(s)" and "Granted BlockoutDelete to N user group(s)" with the step 0.2 numbers
- step 2026110101 ("Disable the discontinued DXC Patient Credit Score program link") "found nothing to change" if step 0.2 said it changes
  nothing; otherwise "changed the database:", followed by the lines step 0.2 quoted: "Deleted N toolbar button(s) of the discontinued DXC
  Patient Credit Score program link (ToolButItemNum ...)" if it listed buttons, and "Disabled the discontinued DXC Patient Credit Score program
  link (ProgramNum N)" if it listed an enabled row
- step 2026110781 ("Disable the discontinued Trojan Express Collect program link"): the same, with the Trojan Express Collect lines of step 0.2;
  normally "changed the database:" followed by the one line "Deleted 1 toolbar button(s) of the discontinued Trojan Express Collect program
  link (ToolButItemNum N)" (the Account module button Open Dental's 19.2.29 update gave every database)
- step 2026110103 ("Report row for the UDS+ report") "changed the database:", followed by "Added displayreport ODUdsPlus" (with "(hidden, as
  the sealant measure is)" if it is hidden) and, if step 0.2 listed user groups, "Copied N report permission(s) from ODDentalSealantMeasure to
  ODUdsPlus" with the step 0.2 number
- step 2026110104 ("Insurance History rows from the InsHist preferences to code groups (26.1)") "changed the database:", followed by the
  lines of step 0.2's Insurance History list ("Added code group '<name>' (ItemOrder N), used only for Insurance History ..."), each once,
  and "Insurance History: ..." notes: that the existing code groups were not changed, and for perio quadrant preferences that held
  different codes
- step 2026110206 ("Routine maintenance: grant Maintenance Setup and Maintenance Record Completion to the Security Admin groups") "changed the
  database:", followed by "Granted MaintenanceSetup to N Security Admin user group(s)" and "Granted MaintenanceRecord to N Security Admin user
  group(s)" with the step 0.2 numbers (or "found nothing to change" if step 0.2 said it changes nothing); then
  "Fork migrations: 26 step(s) run, 0 already done. Fork schema level 2026110213.", "The schema matches Open Dental 26.1.70 (OdSchemaCompat)."
  and "Adoption finished."
- "Fork migration, every step again: 26 steps run, 0 changes." and "The schema matches Open Dental 26.1.70 exactly (427 of 427 tables), plus 12 table(s) of this program that match their declarations."
- the rehash of step 5
- "Row check against opendental ..." with fourteen lines and no line starting `FAIL`: `preference` (the markers added, "N of the N 25.4 and 26.1
  preference(s) migration-preview lists added with their defaults", `DataBaseVersion` and `ProgramVersion` set to 99.0.0.0, the preferences of
  the "Blanked" line blanked, "no other row changed or removed"), `documentmisc` (the update-file rows deleted, "0 other row(s) deleted; 0
  added; 0 changed"), `securitylog` ("2 entries added (1 adoption, 1 rehash); no existing row changed or removed"), `securityloghash`,
  `displayreport` ("N of the N rename(s) migration-preview lists, InternalName only (...); the UDS+ report row added as migration-preview
  lists (ODUdsPlus, DisplayReportNum N, ItemOrder N), N of the N later Public Health row(s) moved down one place, ItemOrder only; no other row
  or column changed, no other row added, none removed"), `grouppermission` ("N of the N Open Dental 25.3 PhoneExtension grant(s) moved from
  272 to 274; BlockoutAdd granted to N of N user group(s), BlockoutDelete granted to N of N user group(s), as migration-preview lists, ...;
  Reports for the UDS+ report row granted to N of the N user group(s) that may run the FQHC Dental Sealant Measure, as migration-preview
  lists; MaintenanceSetup and MaintenanceRecord for the user groups with Security Admin: N of the N planned row(s) added (FKey 0, no date
  limit), as migration-preview lists; no other row changed, added or removed"), `codegroup` ("N of the N code group(s) migration-preview lists marked Show In History as
  planned, ProcCodes and the history columns only (...); N of the N planned code group(s) added with the planned values (CodeGroupNum ...); no
  other row or column changed, none removed"), `program` ("N of the N DXC Patient Credit Score row(s) migration-preview lists disabled,
  Enabled only (ProgramNum N); no other row or column changed, none added or removed", or "identical row by row (migration-preview lists no
  change)"; when step 0.2 listed a Trojan Express Collect row to disable (an office that used the link), "N of the N Trojan Express Collect
  row(s) ..." follows the DXC part, or replaces it if no DXC row was listed), `toolbutitem` ("N of the N DXC Patient Credit Score toolbar
  button(s) migration-preview lists deleted (ToolButItemNum ...); no other row changed, added or removed", or "identical row by row ...",
  with the Trojan Express Collect buttons the same way: normally "1 of the 1 Trojan Express Collect toolbar button(s) migration-preview
  lists deleted (ToolButItemNum N)", after the DXC part if there is one), "Changed columns:" ("claimpayment.Note: N of the N NULL note(s)
  migration-preview counts now empty, every other note unchanged", and `insverify` and `insverifyhist.DateLastAssigned` "the original date at
  midnight in all N row(s)"), "New tables and columns:" ("5 new table(s), all empty (...); 28 new column(s) in 21 table(s) hold their default
  in every row."), "Fork tables:" ("12 new table(s) of this program, all empty (maintfield, maintitem, maintrecord, maintrecordvalue,
  maintstep, sshhostkey, supplycount, supplycountline, supplylot, supplyproccode, supplyprocpending, supplysetting).": the routine maintenance
  tables, the SSH host keys table (no server's key is stored yet: the first connection to each server stores it) and the inventory tables
  but the stock ledger, so no stock count is open), "Stock ledger:" ("supplyledger added (inventory tracking), N of the N
  opening balance row(s) migration-preview lists and no other row.") and `updatehistory` ("identical row by row")
- "Adopt: PASS." with the report rows renamed (or "the fork migration renamed no report row"), the UDS+ report row added and, if step 0.2
  listed them, the DXC Patient Credit Score and Trojan Express Collect rows disabled and their buttons deleted, and exit code 0

The second person compares the renames, the inserted preferences, the moved and granted permissions, the NULL notes, the DXC Patient Credit
Score row and buttons, the Trojan Express Collect button (and row, if the office used it) and the Insurance History code groups in the log with the step 0.2 list on the checklist. `adopt`'s row check has already held them to `opendental` as it is now; if they differ from the step 0.2
list, someone changed Report Setup, the security settings, a claim payment, the program links, the code groups or the Insurance History
preferences in Open Dental since then: run `migration-preview` on
`opendental` again (it only reads), and if its list now equals the log, note that on the checklist and go on; otherwise stop and go to
Rollback.

**If it stops or fails, do not run it again on the same copy.** "Adopt: STOPPED" (exit code 1) or "Adopt: FAIL" (exit code 6): rename
`adoption-log.txt` to `adoption-log-1.txt` and keep it, [server] `sudo mariadb -e "DROP DATABASE opendental_pms"`, and repeat 3.2, 3.3 and 4 on
a new copy (`opendental` is unchanged). A second failure: Rollback. (An adoption cut off before it writes its markers is finished by running
`adopt` again, but one cut off later is not: the copy then counts as adopted with an unfinished record and `adopt` fails, closed. A new copy
is right in both cases and takes minutes; validation report, F13.) "Refused" (exit code 2) changed nothing: correct what it says and run it
again.

## 5. Rehash the security hashes

Done by the command of step 4, after the schema verifier: `SecurityHash.RehashNow`, the same code as Database Maintenance, Tools, Security
Hashes. Open Dental's `SecurityHash` values cannot be checked by this program. The rehash gives every patient, and every appointment, claim,
claim procedure, payment split, payment plan, insurance subscriber and verification row in the checked date range, a hash of this program. It
does not change any data you see, keeps every row's last-changed time, and adds a security log entry. Check in `adoption-log.txt`: "Rehash:
every patient, and the other tables' rows dated on or after ...", one line per table ending in "(0 without a fork hash)" where it shows the
window, and "Rehash finished.".

## 6. Validate the adopted copy, then start the program

6.1. **[admin PC] Before anyone starts the program:**

```powershell
& $sd validate --database opendental_pms --as-of <date> --out C:\Adoption\after-adoption.json @db
& $sd compare C:\Adoption\baseline-copy.json C:\Adoption\after-adoption.json --allowed '<tools>\allowed-differences.adoption.json' | Tee-Object C:\Adoption\compare.txt
& $av startup-check --database opendental_pms @db | Tee-Object C:\Adoption\startup-check.txt
```

Expect in `compare.txt`: `Unexpected differences: 0` (exit code 0), and in its "Allowed differences" only lines of these kinds (the second
person reads them all):

- `ColumnRemoved patient._portal_active_email` and `TotalChanged patients.portal_active_email_nonnull`;
- `ColumnChanged <table>.SecurityHash` for the rehashed tables (appointment, claim, claimproc, inssub, insverify, insverifyhist, patient,
  payplan, paysplit);
- lines for the tables `preference`, `documentmisc`, `securitylog`, `securityloghash`, `displayreport`, `grouppermission`, `codegroup` and
  `toolbutitem`. A report can only allow these eight tables as a whole; step 4's row check compared them with `opendental` row by row and
  printed exactly what changed in them. For `displayreport` that is the renames of step 0.2 and the UDS+ report row with the moved Public Health rows (`RowsChanged
  displayreport` one row up, and `ColumnChanged displayreport.<column>` for its columns, because an added row changes every column's
  checksum). For `codegroup` it is the Insurance History groups step 0.2 listed as added: `RowsChanged codegroup` and
  `ColumnChanged codegroup.<column>` for its columns (an added row changes every column's checksum); step 4's row check printed that no
  existing row changed. For `toolbutitem` it is the DXC Patient Credit Score and Trojan Express Collect buttons of step 0.2, if it listed any
  (`RowsChanged toolbutitem` and `ColumnChanged toolbutitem.<column>`);
- `ColumnChanged program.Enabled`, only if step 0.2 listed the DXC Patient Credit Score or the Trojan Express Collect row as disabled: a
  report can only allow it as a whole column, and step 4's row check printed that only those rows changed, `Enabled` only;
- this program's own tables of inventory tracking (steps 2026110211 to 2026110213): `TableAdded` for `supplylot`, `supplyproccode`,
  `supplysetting`, `supplyprocpending`, `supplycount` and `supplycountline` (0 rows; step 4's row check printed them under "Fork tables:",
  all empty) and for `supplyledger`, the
  one table of this program adoption writes rows into (one opening balance row per supply with stock, as step 0.2 listed; step 4's row check
  printed "Stock ledger:" with exactly those rows). These steps change no Open Dental table; inventory tracking stays off until the office
  turns it on;
- the Open Dental 25.4 and 26.1 schema: `TableAdded` for `deletedobj`, `paysuitepayment`, `paysuitepaymentdetail`, `schoolcourseenrollee`
  and `schoolcourseinstructor` (0 rows), `ColumnAdded <table>.<column>` for the 28 new columns (`allowed-differences.adoption.json` names
  each), and `ColumnChanged` for `claimpayment.Note` (if step 0.2 counted NULL notes) and `insverify` and `insverifyhist.DateLastAssigned`
  (if those tables hold rows). A report can only allow these as whole columns; step 4's row check printed that the new tables are empty, the
  new columns hold their defaults, only NULL notes changed and every `DateLastAssigned` is its old date at midnight;
- this program's own tables (routine maintenance, steps 2026110201 to 2026110205): `TableAdded` for `maintfield`, `maintitem`, `maintrecord`,
  `maintrecordvalue` and `maintstep` (0 rows); step 4's row check printed them under "Fork tables:", all empty;
- this program's own table of SSH host keys (step 2026110221): `TableAdded` for `sshhostkey` (0 rows); step 4's row check printed it under
  "Fork tables:", empty;
- `ChecksumTableChanged` lines "explained by the differences listed for this table", for the tables above.

A line for any other table in the Allowed section: stop, go to Rollback, and give `compare.txt` to the developers. Every business total
(aging buckets, production, income, payment plans, insurance estimates, appointments) is equal. In `startup-check.txt`, every Open Dental
release client line says `Exits` with `database writes no`, and the last line is "Every Open Dental release client refuses this database."
(exit code 0). This comparison holds only now: once the program has started (6.2), its own writes are in the database.

6.2. **[workstation] First start and smoke test.** On the update server (the workstation named in Update Setup, `WebServiceServerName`),
install this program's release build in its own folder (for example `C:\Program Files (x86)\Open PMS - Dental`), never over the Open Dental
folder. Start it (`OpenPmsDental.exe`); in the Choose Database window enter the server (`<db-server>` or `<db-server-ip>`, as in the workstations' `FreeDentalConfig.xml`) and the database
`opendental_pms`. The version guard says the database is adopted and up to date (if it offers to publish this build, see step 8.1); log in. If
it says instead that the database "has not been adopted" and offers the adoption tool, it is pointed at the wrong database: answer No and
correct the database name. Never adopt from a workstation.

Then: open five patients you know in every module (Family, Account, Treatment Plan, Chart, Images, Appointments), plus the records that use
the 24.4 to 25.3 features if the office has them (dated fees, chart letters, mirrored appointments, eForms, pending insurance, group notes,
Check sheets). Print the aging report and compare its totals with the last Open Dental aging report.

Any unexpected difference or wrong screen: go to Rollback (the original is untouched).

## 7. Re-enter the stored passwords and the recorded settings

Open Dental encrypted stored passwords with its own proprietary key; this program cannot read them (decoupling plan, ground rule 3;
`CodeBase/Crypto/README.md`). They show as blank, and the integration fails until they are re-entered. Run [admin PC]
`& $av credentials --database opendental_pms @db` for the office's exact list (without step 0.6, this is the first time the list is made), and
re-enter each:

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

7.1. **[workstation] The settings of step 0.11.** In this program (the workstation of step 6.2), open each window of the table of step 0.11
and set each box to the value written in its fourth column: tick or untick it, type the number or the folder. A box that already shows that
value needs nothing (the value is the default). Save each window (OK or Save; Preferences saves all its pages at once). The second person
then opens each window again, compares every box with the fourth column, writes "set" in the fifth column and initials the row. A row written
"not found" in step 0.11 stays as it is. A value that this program refuses: write "refused" in the fifth column and give the row to the
developers; that setting stays at its default until they answer, and the switch goes on. Then the second person opens each window of the
table "Settings that carry over by Open Dental's name" and compares every box with the value the `migration-preview` line "Settings read
under Open Dental's names" showed (step 0.2), without changing anything; a box that differs, or a sales tax procedure code this program does
not find, is given to the developers, and the switch goes on.

## 8. Switch every workstation

8.1. On the update server (the workstation named in Update Setup, `WebServiceServerName`), start the release build and accept "publish this
build" (see [release-process.md](release-process.md)).

8.2. Every other workstation of the step 0.5 list installs this program once, in its own folder as in step 6.2, starts it, is pointed at
`opendental_pms` in the Choose Database window, and logs in. If step 2.1 revoked the workstations' MariaDB account on `opendental`, first grant
it the same rights on `opendental_pms` [server] (the `GRANT` lines of step 0.5 with the database name changed, run with `sudo mariadb -e`).

Fill this table; every line needs both marks before step 10. A workstation that cannot be switched now stays switched off until it can.

| Workstation (step 0.5) | Open Dental removed, no `OpenDental.exe` or `FreeDentalConfig.xml` left in its folder (2.1) | This program on `opendental_pms`, logged in (8.2) | Initials |
|---|---|---|---|
| | | | |

## 9. Point the other programs at the adopted database

Repoint every program and job from step 0.4 at `opendental_pms` (same server, same accounts; grant the accounts on `opendental_pms` if they
were database-specific grants) and resume them. Set the office's backup jobs on `<db-server>` to back up `opendental_pms`.

## 10. Go live

Before announcing it, check that nothing has used the original since the backup of step 3.1:

10.1. **[admin PC] No connection to the original:** `& $av connections --database opendental @db`. Expect "No connection uses opendental."
(exit code 0). A line names the computer that is connected: close the program there and finish step 2.1 on it.

10.2. **[admin PC] The original is unchanged:**

```powershell
& $sd validate --database opendental --as-of <date> --out C:\Adoption\golive-original.json @db
& $sd compare C:\Adoption\baseline-original.json C:\Adoption\golive-original.json | Tee-Object C:\Adoption\golive-compare.txt
```

Expect `Unexpected differences: 0` (exit code 0), with no `--allowed`: nothing may differ. Any difference means something wrote to
`opendental` after the backup: treat it as lost work and recover it before going on ("Work entered in the original after the backup", below).

10.3. Announce the end of the outage. Keep `C:\Adoption` and `<backup>` until the rollback deadline has passed and the owner has signed the
switch. Repeating 10.1 and 10.2 (with a new `--out` name each time) at the end of each working day until the deadline costs a few minutes and
finds a forgotten client early.

## 11. After the rollback deadline

11.1. Repeat 10.1 (exit code 0) and 10.2 with `--out C:\Adoption\predrop-original.json`, compared with `baseline-original.json` (or the newer
reference from "Work entered in the original after the backup"): `Unexpected differences: 0`. Any difference: recover that work first.

11.2. [server] Make sure the office's backup of `opendental_pms` has run (step 9). Archive `<backup>/opendental-files` where the owner decides
(it holds the office data), then, only after the owner agrees, drop the original and the tools' account:
`sudo mariadb -e "DROP DATABASE opendental; DROP USER 'adoption_admin'@'localhost', 'adoption_admin'@'127.0.0.1'"`. Close the forward on the
admin PC. Delete the disabled scheduled tasks of step 2 and the `FreeDentalConfig.xml.before-switch` files of step 2.1.

## Work entered in the original after the backup

If step 10.1 or 11.1 finds a connection to `opendental`, or 10.2 or 11.1 finds a difference, someone used Open Dental on the original after the
backup. That work is not in `opendental_pms`, and dropping the original (step 11) would lose it.

1. Stop it: find the computer (the "from" of `connections`, or `CompName` below), close Open Dental there and finish step 2.1 on it.
2. List what was done. The comparison names the tables that changed; the security log names the actions, users and patients:
   [server] `sudo mariadb opendental -e "SELECT LogDateTime,CompName,UserNum,PatNum,PermType,LogText FROM securitylog WHERE LogDateTime>='<start of step 3.1, yyyy-MM-dd HH:mm:ss>' ORDER BY LogDateTime" | sudo tee /var/backups/opendental-adoption/work-in-original.txt`
   (the times are the workstations' local times, as on the server's clock). This list shows patients: it stays on `<db-server>`; read it there.
3. Re-enter that work in this program by hand, with a second person checking each item against the list.
4. Record a new reference of the original: [admin PC] `& $sd validate --database opendental --as-of <date> --out C:\Adoption\baseline-original-2.json @db`,
   note on the checklist why, and use it instead of `baseline-original.json` in step 11.1 and in Rollback step 3.

Then continue where you were.

## Rollback

Use this when a step above says so, or a problem shows up before the rollback deadline. Anything entered in this program since the switch is
lost and must be re-entered in Open Dental by hand (step 2 lists it). The original `opendental` is untouched by this procedure, so rolling back
is mostly undoing step 2 and 2.1, in this order. Steps that concern something not done yet (for example `opendental_pms` before step 3.2)
are skipped.

1. **Remove this program from every workstation.** On each workstation of the step 0.5 list: close this program, uninstall it (or, if that is
   not possible now, rename `FreeDentalConfig.xml` in its folder to `FreeDentalConfig.xml.rolled-back`), and fill this table. Stop every
   program and job that step 9 pointed at `opendental_pms`.

   | Workstation (step 0.5) | This program removed, or its `FreeDentalConfig.xml` renamed | Open Dental reinstalled, on `opendental`, logged in (step 4 below) | Initials |
   |---|---|---|---|
   | | | | |

2. **Take `opendental_pms` out of use.** [admin PC] `& $av connections --database opendental_pms @db` until it says "No connection uses
   opendental_pms." (exit code 0); a line names the computer that still has this program open. Then [server]:
   - if this program was used after step 6.2, list what was entered, for re-entry in Open Dental (it shows patients; it stays on `<db-server>`):
     `sudo mariadb opendental_pms -e "SELECT LogDateTime,CompName,UserNum,PatNum,PermType,LogText FROM securitylog WHERE LogDateTime>='<time of step 6.2>' ORDER BY LogDateTime" | sudo tee /var/backups/opendental-adoption/work-in-pms.txt`;
   - keep it for the analysis, then drop it, so that no program can open it any more:

   ```bash
   sudo mariadb-dump --lock-tables --routines --triggers --events --hex-blob --default-character-set=utf8mb4 \
     --result-file=/var/backups/opendental-adoption/opendental_pms-rollback-$(date +%Y%m%d-%H%M).sql opendental_pms \
     && sudo mariadb -e "DROP DATABASE opendental_pms" && echo DUMPED-AND-DROPPED
   ```

   Expect `DUMPED-AND-DROPPED`; the drop runs only after the dump succeeded. (This logical dump rounds FLOAT values as step 0.2 describes; for
   the analysis that does not matter.) If step 8.2 granted the workstations' account on `opendental_pms`, revoke it:
   `sudo mariadb -e "REVOKE ALL PRIVILEGES ON opendental_pms.* FROM '<user>'@'<host>'"`. A program still pointed at `opendental_pms` now fails
   to open it instead of taking work the office no longer reads.
3. **Check the original is untouched.** [admin PC] `& $sd validate --database opendental --as-of <date> --out C:\Adoption\rollback-check.json @db`
   and `& $sd compare C:\Adoption\baseline-original.json C:\Adoption\rollback-check.json` (or the newer reference). Expect `Unexpected differences: 0`.
   - If there are differences, something wrote to it after the backup: list what with step 2 of "Work entered in the original after the
     backup". If it is work the office keeps, keep the original as it is. Otherwise **restore the folder copy of step 3.1**, keeping the
     modified original, so whatever wrote to it can be analysed or recovered later:

     ```bash
     sudo systemctl stop mariadb && sudo mv /var/lib/mysql/opendental /var/backups/opendental-adoption/opendental-modified-$(date +%Y%m%d-%H%M) && sudo cp -a /var/backups/opendental-adoption/opendental-files /var/lib/mysql/opendental && sudo diff -rq /var/backups/opendental-adoption/opendental-files /var/lib/mysql/opendental && echo IDENTICAL
     sudo systemctl start mariadb; systemctl is-active mariadb
     ```

     Expect `IDENTICAL`, then `active`, then compare again with `baseline-original.json` (expect 0 unexpected differences). Without
     `IDENTICAL`, do nothing more and call the developers: both the folder copy and the modified original are kept. The modified folder is
     outside `<datadir>`, so it is no database and no program can use it; the developers can bring it back under another name to analyse it.
4. **Reinstall Open Dental.** The Open Dental 25.3.48 client on the workstations (the table of step 1) and the Open Dental services from the
   installer (step 0.10), using the lists from steps 0.3 and 0.5; rename `FreeDentalConfig.xml.before-switch` back where it exists. Re-grant the
   workstations' MariaDB account on `opendental` if step 2.1 revoked it. Re-enable the scheduled tasks of step 2. Move `Setup.exe` and
   `UpdateFiles` back into the A to Z folder.
5. **Point the other programs back at `opendental`** (step 0.4) and resume them, backup jobs included.
6. When the analysis no longer needs it: [server] drop the tools' account
   (`sudo mariadb -e "DROP USER 'adoption_admin'@'localhost', 'adoption_admin'@'127.0.0.1'"`), close the forward, and delete `<backup>` once the
   owner agrees.

The adopted database itself cannot be "un-adopted": the dropped column, the deleted update files and the sentinel versions are not reversible
in place, by design.

## Checklist

Sign each line with time and initials.

| # | Step | Expected | Done |
|---|---|---|---|
| 0.0.1 | Platform recorded | Debian or Ubuntu, MariaDB 10.11 (version with its package suffix, compile OS linux), `<datadir>` and port written down; bind address lets `127.0.0.1` in; MyISAM 422; no routine, trigger, event or view; other databases noted | |
| 0.0.2 | `adoption_admin` created, history file removed | Password in the password manager | |
| 0.0.3, 0.0.4 | Forward and tools checked from the admin PC | `connections` lists the workstations' connections, or none | |
| 0.1 | Validation report signed, release build ready | Owner signature | |
| 0.2 | Versions; FLOAT check filed; fork migration preview | 25.3.48.0 / 25.3.48.0, not corrupted, no update running; `float-check` exit 0 or 6, file kept; `migration-preview` exit 0, each report row rename written here (old name, new name, DisplayReportNum), or "none"; NULL notes ___, preferences ___, PhoneExtension grants moved ___, user groups BlockoutAdd ___ / BlockoutDelete ___; DXC Patient Credit Score row disabled ___ (ProgramNum or none), buttons deleted ___; Trojan Express Collect row disabled ___ (ProgramNum or none), buttons deleted ___; UDS+ row after DisplayReportNum ___, rows moved ___, user groups granted ___; Insurance History code groups added ___ (name and History Proc Code); settings read under Open Dental's names, each shown value written here (or the line kept with the file); step 2026110731 changes no preference row | |
| 0.3 | Services and tasks listed, per computer (both service queries) | List filed | |
| 0.4 | Direct database users and jobs listed | List filed | |
| 0.5 | Workstations listed, with folder, database and MariaDB user | List filed | |
| 0.6 | Owner's answer on the preparation dry run | Yes / no, signed | |
| 0.6 | If yes, outside office hours: services stopped, nobody connected, folder copy, dry run, `credentials` | `connections` exit 0; `IDENTICAL`, `active`; `Dry run: PASS`; times filed | |
| 0.6.4, 0.6.5 | If yes: `od_prep_pms` dropped, prep folder copy deleted, services started again | Done the same night | |
| 0.7 | If no: passwords collected, window planned from the rehearsal (+200%); tools and server check if done | Plan filed | |
| 0.8 | Disk space | 1.5 × / 1.2 × the database folder free (2.7 × on one disk) | |
| 0.9 | Rollback deadline agreed | Date and time | |
| 0.11 | Settings that do not carry over, recorded in Open Dental 25.3.48 by the second person | Table of step 0.11, fourth column filled for all 5 rows (a value, or "not found") | |
| 1.1 | Everyone out | `connections`: only the services' connections left, or none | |
| 1.2 | Other writers paused | Checked | |
| 2 | Open Dental services stopped and removed, tasks disabled, A to Z Setup moved | Both service queries empty, tasks `Disabled`, on every computer of 0.3 | |
| 2.1 | Open Dental removed from every workstation (not just shortcuts), `FreeDentalConfig.xml` renamed; account revoked on `opendental` where possible | Table of step 8, first column complete | |
| 3.1 | Nobody connected; folder copy, start time noted | `connections` exit 0; `IDENTICAL`, `active`; off-server copy if the owner agreed | |
| 3.2 | `copy` into `opendental_pms` | `Copy: PASS` | |
| 3.3 | Reports compared | 0 unexpected differences | |
| 4, 5 | `adopt` (adoption, migration check, verifier, rehash, row check) | `Adopt: PASS`, exit 0, log saved; "Blanked" list initialled; report row renames, the DXC Patient Credit Score and Trojan Express Collect rows and buttons, the UDS+ row and its grants, and the Insurance History code groups equal the 0.2 list | |
| 6.1 | Validation before the first start | 0 unexpected differences, Allowed section read (`displayreport`, `grouppermission`, `codegroup` and `toolbutitem` as whole tables, `program.Enabled` as a whole column); startup check exit 0 | |
| 6.2 | First start, smoke test, aging report | OK | |
| 7 | Passwords re-entered | `credentials` list empty or explained | |
| 7.1 | Recorded settings set again in this program | Table of step 0.11, fifth column "set" (or "refused", given to the developers) and initialled by the second person on every row not "not found"; the settings that carry over by Open Dental's name show the values `migration-preview` listed (or the differences given to the developers) | |
| 8 | Every workstation switched | Table of step 8 complete | |
| 9 | Other programs repointed, backups on `opendental_pms` | Checked | |
| 10.1 | No connection to `opendental` | `connections` exit 0 | |
| 10.2 | `opendental` unchanged since the backup | 0 unexpected differences | |
| 10.3 | Live | Announced | |
| 11.1 | Before dropping: no connection, `opendental` unchanged | Exit 0; 0 unexpected differences | |
| 11.2 | After the deadline: original archived and dropped, `adoption_admin` dropped | Owner agreed | |
