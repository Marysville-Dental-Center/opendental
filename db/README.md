# New database

`init/new-database.sql` creates the database a new office starts from. Load it into an empty database and the program opens it: every table of the current schema (the Open Dental 26.1.70 schema and the fork's own, at the latest fork migration level), and the rows the program needs to start.

What is in it:

- preferences, definitions, user groups and their permissions, report rows, claim form layouts and the other default setup rows;
- the user `Admin` with a blank password. The program logs `Admin` on automatically and asks for a new password straight away;
- two placeholder providers (`DOC` "Dentist" and `HYG` "Hygienist", with no license or ID numbers) and six operatories, to rename or replace;
- every program link, all of them off;
- the main window title and the software name (`MainWindowTitle` and `SoftwareName`) set to the product name, "Open PMS - Dental";
- the procedure codes, named by their code only (`Procedure D0120`). They carry no ADA CDT descriptors: an office that has a CDT license enters its own descriptions;
- the version markers of a database at the current fork level: every applied fork migration level, `OdSchemaCompat` 26.1.70, the sentinel `DataBaseVersion` and `ProgramVersion` 99.0.0.0, and an adoption record that says the database was created from this file (`ForkAdoptedFrom` starting with `new=1`), not copied from an Open Dental database.

What is not in it: patients, appointments, schedules, the seed's fictional practice, anything of the PC that generated the file (no computer rows, no security log, no machine or user names), and no time of the run that generated it.

## Create an office database

1. Install MariaDB 10.11 on the database server, with MyISAM as the default storage engine (`default-storage-engine=MyISAM` in `my.ini`).
2. Create an empty database with the character set the tables use. In the `mariadb` (or `mysql`) client or in HeidiSQL's query tab:

   ```sql
   CREATE DATABASE opendental CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci;
   ```

3. Load the file into it, with either tool:
   - client: `mariadb -u root -p opendental < db\init\new-database.sql` (the `mysql` client takes the same arguments);
   - HeidiSQL: select the new database, then run the file with **File > Run SQL file**.

   It takes under a minute. The file never drops anything: loaded into a database that already has tables, it stops with "Table 'account' already exists" at its first `CREATE TABLE`.
4. Point the program at the database. `OpenPmsDental.exe` reads `FreeDentalConfig.xml` from its own folder. Set `ComputerName` (the server, `host:port` for a port other than 3306), `Database`, `User` and `Password`, as in `tools/FreeDentalConfig.dev.xml`, or fill in the same fields in the window that asks for the database when the program starts without a config file.
5. Start the program. It logs on as `Admin` and asks for a password: set one. Then enter the practice's settings: practice name, address and phone (blank in the file), providers, operatories, fee schedules, the A to Z images folder (`DocPath`, `C:\OpenDentImages\` until changed), and the procedure code descriptions.

## Regenerate the file

The file is generated, never edited by hand. Regenerate it after every new fork migration step, and commit it with the step:

```powershell
powershell -ExecutionPolicy Bypass -File tools\New-InitSql.ps1
```

`-BuildLane <path of odbuild.ps1>` builds through the shared build lane of the PC; `-SkipBuild` uses the DevDbTool already built. The script builds `tools/DevDbTool` and runs `DevDbTool --export-init-sql`, which:

1. builds a scratch database on the local MariaDB (`initsql_<run id>`) the way `tools/New-DevDatabase.ps1` does: the seed `UnitTests/Resources/dump.txt`, the frozen convert scripts to 24.3.63, adoption and the fork migrations, the default auto codes, procedure buttons and quick-add procedures; no sample data and no A to Z folder;
2. checks it like `New-DevDatabase.ps1 -VerifyOnly`;
3. removes what belongs to the PC or the run (`tools/DevDbTool/InitSqlScrub.cs` lists every table and preference, with the reason), writes the new-database adoption record, sets the title and software name to "Open PMS - Dental" (the seed holds Open Dental's; a new database is not an adopted copy, so adoption's row check does not apply) and requires a new `Admin` password at the first logon;
4. writes every table's `SHOW CREATE TABLE` and its rows in primary key order, in C#, with the seed's `SET NAMES` and `sql_mode` lines. The same source gives the same bytes on any PC with MariaDB 10.11 (another server version may print `CREATE TABLE` differently);
5. refuses to write the file if it still holds this PC's machine or user name, the scratch database name, a date of the run or an Open Dental host, then drops the scratch database.

Tests: `tests/InitSqlTests` loads the committed file into a scratch database and checks the schema (it must match 26.1.70 apart from the documented exceptions), the version guard (it must open the database with no fork migration pending), the procedure code texts and that no patient, computer or machine name is in it. `tests/ForkMigrationTests/InitSqlRegenerationTests.cs` runs the generator and fails when its output differs from the committed file, so a new fork migration step without a regenerated file fails a test. It builds a database, so it runs with the slow assemblies (`run-tests -Mode Full`, or `-Include ForkMigrationTests`).
