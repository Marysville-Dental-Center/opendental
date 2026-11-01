# INIT-SQL: a SQL file that initializes a new database

Package INIT-SQL of the pre-rename run, on branch `pre/init-sql-1` from `main` (d9c177ca6). The owner asked for it on 2026-11-01: "Add a SQL file for fresh db initialization". Before this package the only way to make a database was `tools/New-DevDatabase.ps1` / `tools/DevDbTool`, which needs the source tree and MSBuild. No fork migration step, schema, CRUD or TableTypes change: the generator only reads and scrubs a database the existing steps build.

## What changed

### 1. No ADA CDT text in the seed dump

- `UnitTests/Resources/dump.txt`: the 577 procedurecode rows whose code is `D` plus four digits now have `Descript` `Procedure Dxxxx` and `AbbrDesc` `Dxxxx`. Codes, categories and every other column are unchanged; `LaymanTerm`, `DefaultNote` and the other text columns were blank already. Every other byte of the file is unchanged (CRLF kept).
- The two other codes keep Open Dental's own text: `~GRP~` "Group Note" and `D8670.auto` "Default auto ortho procedure code".
- INFERRED: `AbbrDesc` is the code for every CDT-style code, also for the codes whose abbreviation was blank. One rule is simpler to check, and a convert step of 21.2 (`ConvertDatabases7.cs`, the abbreviations of the 2020 and 2021 codes) fills blank abbreviations with Open Dental's own; with none blank it changes nothing.
- The rest of the seed was checked for descriptor text: procedure buttons (`procbutton`, `procbuttonquick`), auto codes, recall types, coverage categories and spans, definitions (procedure categories, quick-add lists) and code groups hold Open Dental's short labels ("Amalgam", "Crown-PFM", "Seal", "MOD", "Prophy", "4BW") and code references, no ADA descriptor. The default quick-add and procedure-button rows that `DevDatabase.ApplyDefaults` writes are the same kind of labels. No descriptor phrase of the old seed occurs anywhere in the new database file (checked with the old texts as a list, outside the repository).
- No test depended on the old texts (the Core run below passes); no test was changed and none adds ADA text.

### 2. The generator: `DevDbTool --export-init-sql <path>`

- `tools/DevDbTool/InitSqlExport.cs`: builds a scratch database (`initsql_<UTC time>_<pid>_<random>`, or `--database`; it must not exist) the normal way: seed, frozen convert chain to 24.3.63, `StampProgramVersion`, adoption with the fork migrations, `ApplyDefaults`. No sample data, no A to Z folder. Then the `--verify-only` checks (`Program.Verify`), the scrub, the writer, a last check of the text, and the scratch database is dropped (`--keep-scratch` keeps it). The run pins the `en-US` culture: `ProcButtons.SetToDefault` gives Canadian buttons under a Canadian culture.
- `tools/DevDbTool/InitSqlScrub.cs` (data only, the schema is left as the fork migrations made it):
  - emptied, each with its reason in the code: `computer`, `computerpref`, `securitylog`, `securityloghash`, `signalod`, `alertitem` (Open Dental eServices notices), `ehrmeasureevent` (one 2021 event of the seed), `schedule` and `scheduleop` (the seed's 2007 schedules), `zipcode` (fictional), `emailhostingtemplate` (Open Dental's mass e-mail templates, an eService the fork removed; they load images from Open Dental's web site). The seed's birthday reminder rule pointed at one of those templates; its `EmailHostingTemplateNum` becomes 0;
  - providers: `LName` "Dentist", or "Hygienist" for the secondary provider; first name, middle initial, suffix, SSN, license, DEA, NPI, Blue Cross, Medicaid, Canadian office, state Rx, eCW and custom IDs blanked; `providerclinic` DEA, license and state numbers blanked;
  - practice preferences blanked: `PracticeTitle` ("Relaxation Dental"), address, city, state, zip, phone, billing and pay-to phone (and the other billing and pay-to address preferences, which were blank);
  - `DocPath` set to the seed's `C:\OpenDentImages\` (it is already, since no A to Z folder is made);
  - `ForkAdoptedFrom` set to the new-database record (below);
  - `userod.IsPasswordResetRequired=1` for `Admin` (blank password);
  - run timestamps: every `date`, `datetime` and `timestamp` value between the server's start time minus a day and its end time plus a day becomes 2026-01-01 (00:00:00), with one `UPDATE` per table that assigns every date column, so no ON UPDATE stamp moves again (`procedurecode` 143 rows touched by convert scripts, `provider`, `updatehistory`). Preference values that are a date of the run keep their time of day: `AgingServiceTimeDue` (convert 23.x writes today at 02:00) becomes `2026-01-01 02:00:00`.
  - Then it checks: no row of a patient in any table with a `PatNum` column, `patient`, `appointment`, `computer`, `computerpref`, `securitylog` empty, every program link off, every table MyISAM, the version guard says ProceedFork with nothing pending, the adoption record is finished. Any failure stops the export.
- `tools/DevDbTool/InitSqlWriter.cs`: in C#, no client tools. Header comment (no times), the seed's `SET NAMES utf8`, `SQL_MODE`, unique and foreign key check lines, then every table in ordinal name order: `SHOW CREATE TABLE` without `AUTO_INCREMENT=` (the server sets it from the rows), its rows in primary key order as multi-row `INSERT`s of at most about 1 MB each. Values come from the server as text (`CAST(... AS CHAR)` for numbers and dates, `HEX()` for binary columns written as `0x...`), strings are escaped as mysqldump does. A column type it does not know stops the export. LF line ends, UTF-8 without BOM. No `DROP`: in a database that has tables, loading stops at the first `CREATE TABLE`.
- Last check before writing (`InitSqlScrub.FindLeftovers`): the text must not contain the machine name, the user name and domain, the computer names the business layer sees, the user profile path, the repository path, the scratch database name, `JORDANS` (the seed's own PCs), a date of the run, or an Open Dental host (`HqHosts.BlockedDomains`). Otherwise nothing is written.
- `tools/New-InitSql.ps1`: builds DevDbTool through the build lane (`-BuildLane <odbuild.ps1>`) or MSBuild, runs the export, writes `db/init/new-database.sql`. `-SkipBuild`, `-KeepScratchDatabase`, connection options as `New-DevDatabase.ps1`.
- Two runs with different scratch names and times gave the same SHA-256; `tools/New-InitSql.ps1` gave the committed file.

### 3. The file

- `db/init/new-database.sql` (2.4 MB): 441 tables, 21227 rows, fork level 2026110104, `OdSchemaCompat` 26.1.70, every applied level in `ForkMigrationsApplied`, `DataBaseVersion` and `ProgramVersion` 99.0.0.0, no `ForkProgramVersion` (the update server publishes one), Admin with a blank password to be set at the first logon, program links all off, the neutral procedure codes. Legacy tables of the seed (`anesth*`, `files`, `instructor`) are there, as in every dev database: they are documented schema exceptions today.
- `db/.gitattributes`: `init/*.sql text eol=lf`, so the checkout is byte-identical to what the generator writes on any `core.autocrlf`.
- `db/README.md`: how an office creates its database (CREATE DATABASE utf8mb3, load with the mariadb/mysql client or HeidiSQL's Run SQL file, `FreeDentalConfig.xml`, first logon) and how to regenerate the file.
- Loaded with `mariadb.exe` into an empty utf8mb3 database (15 s), it passes `DevDbTool --verify-only`; a second load into the same database stops at `CREATE TABLE account` with "already exists".

### The adoption record of a new database (INFERRED)

`ForkAdoptedFrom` = `new=1;source=24.3.63.0;level=2026110104;adopted=db/init/new-database.sql`.

What reads it:
- `ForkVersionGuard`: on a database with `ForkSchemaLevel` it does not read the record; it needs the sentinel versions, `OdSchemaCompat` and `ForkSchemaLevel`, and decides pending and unknown levels from `ForkMigrationsApplied`. Without `ForkSchemaLevel`, a record with `adopted=` is refused as "markers missing".
- `ForkDbState.IsAdoptionRecordFinished` needs the `adopted=` field, with any value; `HeadlessAdoption` checks it after an adoption. `Adoption.Preflight` says "already adopted" for any database with `ForkSchemaLevel`, so the adoption tools never adopt it again.
- `Adoption.ParseAdoptedFromSource` (the `source=` field as a version) is how fork migration steps decide what a database still needs (`Seed25_3Step.IsFrom24_3`, `Seed26_1Step.IsFromBefore25_4`, `XConnectEnableStep`, the M2-UDS and M2-HIST steps, `Migration26_1Plan`). An unreadable source counts as "unknown origin" and those steps skip their data.

Chosen: `new=1` says the database was created from the file, not copied from an Open Dental database. `source` stays a version, the fork's own 24.3.63 compatibility line: the file's rows really are the seed converted on that line and migrated, exactly like a dev database. A step added later "for databases from before 26.2" then treats a new database like every other database of that line, instead of skipping it as an unknown origin (`source=new` would have done that). `level` records the level the database was created at, which stays while `ForkSchemaLevel` moves on. No `started=` and no times: the file must not change between runs.

## Tests

- `tests/InitSqlTests` (new, in the sln and slnf, Core tier; 18 tests):
  - the file as text: UTF-8 without BOM, LF only, the seed's session lines, only `CREATE TABLE` (all `ENGINE=MyISAM`, no `AUTO_INCREMENT=` counter, in name order) and `INSERT`, no `DROP`/`DELETE`/`UPDATE`/`ALTER`/`CREATE DATABASE`/`USE`; the header names this build's latest fork level and the reference schema (so a new step without a regenerated file fails in the fast tier too); no machine, user or seed PC name, no profile path, no scratch database prefix; no Open Dental host; the procedure codes neutral; the seed dump's procedure codes neutral;
  - the file loaded into a scratch database (`initsqltest_<run id>`, created utf8mb3 as the README says, loaded statement by statement, dropped afterwards): one engine and collation; `SchemaVerifier` matches 26.1.70 with documented exceptions only; `ForkVersionGuard` ProceedFork with no pending and no unknown level, `ForkSchemaLevel` and `ForkMigrationsApplied` equal to this build's, sentinel versions, `OdSchemaCompat`; the adoption record and what `Adoption.Preflight` says of it; procedure codes; no patient, appointment, schedule, computer, audit, signal, zip code, alert or mass e-mail template row and no row of a patient anywhere; every program link off; Admin with a blank password, `IsPasswordResetRequired`, found by the startup auto-logon query; no fictional practice or provider data; the business layer reads the preferences.
- `tests/ForkMigrationTests/InitSqlRegenerationTests.cs` (new file in an existing project; that assembly runs in the Full tier and with `-Include ForkMigrationTests`): runs `tools/DevDbTool/bin/Debug/DevDbTool.exe --export-init-sql` with a `forkmig_` scratch name, checks the scratch database is gone and compares the output with the committed file byte for byte, reporting the first differing line.
- Results: see "Checks" at the end.

## Decisions (INFERRED)

- DevDbTool is now in `OpenDental.sln` and `OpenDental.Local.slnf` (it was deliberately outside), so the full build builds the generator that the regeneration test runs. The test runs the built exe in its own process rather than in the test process: the generator relies on the business layer's static state and on its own `App.config`. `README.md` line 78 now says so.
- The regeneration test lives in `ForkMigrationTests` because `run-tests.ps1` decides its tiers by assembly, and that assembly is one of the slow ones that run in Full and whenever a migration is touched. The checks of the committed file (`InitSqlTests`) run in Core.
- Fictional seed data is scrubbed in the generator only; dev and test databases keep the seed's demo practice, providers and schedules, which tests rely on.
- Kept in the file as default setup rows: the clearinghouse list, the claim form layouts (ADA 2006, 2012 and 2024, UB04, 1500; the rows name background image files, which are not in the database), the operatories, appointment views, user queries, recall types, the ICD-9 list, the payor ID list and the Canadian network rows of the seed. Owner questions below.
- The fixed date for run timestamps is 2026-01-01.
- `MainWindowTitle` ("Open Dental") and `SoftwareName` ("Open Dental Software") stay as the seed has them: the rename is the next run, and a migration step there regenerates this file.

## Owner questions

1. Adoption record of a new database: `new=1;source=24.3.63.0;level=...;adopted=db/init/new-database.sql` (above). Should later lineage-dependent steps treat a new database as one of the 24.3.63 line (this choice), or as its own origin?
2. Should the file keep the ADA claim form layouts (ADA 2006, 2012, 2024 rows of `claimform`/`claimformitem`, which name the image files `ADA2006.gif`, `ADA2012.gif`, `ADA2024_J430.gif`), the clearinghouse list and the Canadian network rows, or start without some of them?
3. The two placeholder providers ("Dentist" `DOC`, "Hygienist" `HYG`) and six operatories: keep them as a starting point, or start with one provider, or none?
4. The legacy tables of the seed (`anesth*`, `files`, `instructor`) are documented schema exceptions and are in the file. Drop them with a fork migration step one day?
5. The dev seed still has the eServices notices and Open Dental's mass e-mail templates (with links to Open Dental's site); only the new database file drops them. Remove them from the seed too?

## Checks

On `pre/init-sql-1` (2026-11-01):

- Generator: two runs with different scratch names and times, and `tools/New-InitSql.ps1` twice, all wrote the same file (SHA-256 `78b083e3...980cf1`, 2,394,834 bytes). Each run takes about a minute and drops its scratch database.
- Round trip: the file loaded with `mariadb.exe` into an empty utf8mb3 database and the scrubbed scratch database it was written from (kept with `--keep-scratch`) give identical `mariadb-dump --order-by-primary --hex-blob` output apart from the `AUTO_INCREMENT` counters, which the file leaves to the server. The loaded database passes `DevDbTool --verify-only`; loading the file a second time stops at the first `CREATE TABLE`. The client tools were used only for these checks, never by the generator.
- One full build of `OpenDental.Local.slnf` through the build lane (0 errors; DevDbTool and InitSqlTests without warnings).
- `run-tests -Mode Core -Include ForkMigrationTests,M201Tests,SyntheticData.Tests,AdoptionValidation.Tests`: 84 assemblies, 5,534 tests, none failed (`InitSqlTests` 18 of 18, `ForkMigrationTests` 40 of 40 with the regeneration test, `AdoptionValidation.Tests` 63, `SyntheticData.Tests` 54, `M201Tests` 24 passed and 2 inconclusive, the published-document checks as on earlier heads, `ReleaseFlowTests` 70, `NoHqTests` 49). Results: `.git/od-tools/test-results/pre_init-sql-1.txt`.
- `tools/Test-NoHqHosts.ps1`: passed (it now also scans DevDbTool and InitSqlTests).
- Not verified: the first logon in `OpenDental.exe` (the GUI is not launched in this run). The automatic logon of a security administrator without a password and the forced password reset are read from `FormOpenDental` and checked through the same query in `InitSqlTests`, not in the program. HeidiSQL was not tried; only the `mariadb` client.
