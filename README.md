# Open PMS - Dental

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This is Marysville Dental Center's fork of the [Open Dental](https://www.opendental.com) source code. `main` started from Open Dental **v24.3**, at **24.3.63.0**. Features of later Open Dental versions, up to 26.1, are re-implemented from Open Dental's public documentation, and the database schema follows Open Dental 26.1.70. See [NOTICE](NOTICE) for attribution, [LICENSE](LICENSE) for the GNU General Public License and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for the third-party components the program ships. Every copyright line and GPL notice from Open Dental is kept.

The source files say "either version 2 of the License, or (at your option) any later version", so the program is licensed under the **GNU GPL v2 or later**. LICENSE holds the version 2 text.

The program's namespaces, assemblies and executable (`OpenDental.exe`) still carry Open Dental's names. They are renamed in a final, separate step. The product name already appears in the About box, the window title (when the office kept Open Dental's default title), the file properties of every shipped assembly (product "Open PMS - Dental", version 0.0), the product identity sent to vendors, and this repository's documents.

Open PMS - Dental is a Windows desktop practice management system written in C#: WinForms, with some WPF, on .NET Framework 4.8, backed by MariaDB.

## Installing in an office

There is no installer yet. Until there is, a release is the output folder of a **Release | x86** build (see [Building](#building)), copied by hand. Nothing here contacts Open Dental's servers, and nothing is installed on the database server by this program.

### Database server

The office installs [MariaDB Server](https://mariadb.org/download/) **10.11** itself. These settings go in the `[mysqld]` section of `my.ini` (Windows) or `/etc/mysql/mariadb.conf.d/50-server.cnf` (Debian and Ubuntu):

```ini
[mysqld]
sql_mode=
default-storage-engine=MyISAM
max_allowed_packet=40M
```

- `sql_mode` must be blank. If it isn't, the program runs `SET GLOBAL sql_mode=''` at startup, which needs an account with that privilege.
- The tables are MyISAM; with another default engine, new tables end up in a different engine.
- `max_allowed_packet` must be at least 40 MB. Smaller values make large inserts (images, releases stored in the database) fail with "Please add the following to your my.ini file: max_allowed_packet=40000000".
- Give each office its own database account, and back the database up.

**A new office's database:** see [db/README.md](db/README.md) and the script `db/init/new-database.sql`.

**An existing Open Dental database** (Open Dental 24.3.63 or 25.3.48 only) is adopted once, on a restored copy: see [Adopting a database](docs/release-process.md#adopting-a-database) and the office procedure in [docs/adoption-runbook.md](docs/adoption-runbook.md).

### Workstations

Prerequisites:

- Windows 10 version 1903 or later, or Windows 11
- **.NET Framework 4.8** (Windows 10 1903 and later and Windows 11 already have it)
- **Microsoft Visual C++ 2015-2022 Redistributable, x86** (`vc_redist.x86.exe`; the DICOM codecs and `Direct2dWrapper.dll` need it)
- **Microsoft Edge WebView2 Runtime, x86** (Evergreen; Windows 11 already has it)
- For scanners and sensors: the device's own TWAIN driver, which brings the TWAIN data source manager

Steps:

1. Install the program on the **update server** first: the workstation named in the `WebServiceServerName` preference (Setup > Update Setup).
2. Copy the release folder to `C:\Program Files (x86)\Open PMS - Dental\`. Don't copy a `FreeDentalConfig.xml` from another computer.
3. Start `OpenDental.exe` once **as an administrator**. The first start creates `FreeDentalConfig.xml` in the program folder, which needs administrator rights there. In the Choose Database window enter the server, the database, the database user and its password.
4. On the update server, the program offers to publish the release at that first start, so the other workstations update to it.
5. Create a Start menu shortcut named "Open PMS - Dental" that points to `OpenDental.exe`.
6. On the other workstations repeat steps 2, 3 and 5. A workstation that runs an older build pulls the published one when it starts.

### Updates

A new build reaches the office through the release channel described in [docs/release-process.md](docs/release-process.md): install it on the update server and start it as an administrator; it publishes the release into the database, and every other workstation pulls it at its next start and restarts through `UpdateFileCopier.exe`. Debug builds never publish or pull. A workstation that is not the update server refuses to run a build newer than the published one (exit code 174), so always update the update server first.

## Building

### Prerequisites

- Windows 10/11
- **Visual Studio 2022** (any edition) or **Build Tools for Visual Studio 2022**, with:
  - .NET desktop build tools (`Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools`)
  - Desktop development with C++ (`Microsoft.VisualStudio.Workload.VCTools`), for `Direct2dWrapper` (native, toolset v143)
  - Web development build tools (`Microsoft.VisualStudio.Workload.WebBuildTools`), for `OpenDentalServer`
  - .NET Framework 4.8 and 4.7.2 targeting packs
  - Testing tools core (`Microsoft.VisualStudio.Component.TestTools.BuildTools`), for the MSTest v1 test projects

One command installs the Build Tools with everything above:

```bash
winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --override "--passive --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --add Microsoft.VisualStudio.Workload.WebBuildTools --add Microsoft.Net.Component.4.8.TargetingPack --add Microsoft.Net.Component.4.8.SDK --add Microsoft.Net.Component.4.7.2.TargetingPack --add Microsoft.VisualStudio.Component.NuGet.BuildTools --add Microsoft.VisualStudio.Component.TestTools.BuildTools"
```

### Build

Open **`OpenDental.Local.slnf`** in Visual Studio, not `OpenDental.sln`, and build `Debug | x86` (or `Release | x86` for an office). From the command line:

```bash
"C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe" OpenDental.Local.slnf -restore -p:RestorePackagesConfig=true -p:Configuration=Debug -p:Platform=x86 -m -nodeReuse:false
```

With Visual Studio installed instead of Build Tools, replace `BuildTools` in the path with your edition, e.g. `Community`. A clean build produces many compiler warnings and no errors. The main program is `OpenDental\bin\Debug\OpenDental.exe` (`OpenDental\bin\Release\` for a Release build). The build also copies `UpdateFileCopier.exe` and the two native files the program loads at run time, `Hunspellx86.dll` and `WebView2Loader.dll`, next to it.

### The solution and the solution filter

- **`OpenDental.Local.slnf`** loads the program and its libraries, services and tools, the adoption and synthetic-data tools under `tools/`, and every test project under `tests/`. The older or standalone projects listed under [Repository layout](#repository-layout) are not in it. `OpenDental.sln` lists the same projects, so either one builds the same set. The build scripts read the filter, so add a new project to both.
- **`Directory.Build.targets`** only adds the folder that holds MSTest v1 (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`) to the assembly search path. Every project is built from source: a missing project fails the build, and nothing is swapped for a DLL in `Required dlls/`.
- **Version numbers:** `SharedAssemblyInfo.cs` in the repository root gives every shipped assembly its product name, copyright and release version (`0.0`). Each project's `AssemblyVersion` stays on its own line; for `OpenDental.exe` and `OpenDentBusiness.dll` that is the compatibility version `24.3.63.0`, which the database checks use ([docs/release-process.md](docs/release-process.md)).
- **We don't merge from upstream.** Open Dental's public source stops at 24.3.63; later features are re-implemented from public documentation ([docs/decoupling-plan.md](docs/decoupling-plan.md)).
- **`.gitignore`** ignores build output (`bin/`, `obj/`, `packages/`, `.vs/`). The `ConversionFiles` already tracked under `OpenDental/bin/Debug` remain tracked.

## Running a development copy

`OpenDental.exe` needs a MariaDB database with its schema. `tools/New-DevDatabase.ps1` builds a development database from files in this repository, so you don't need Open Dental's installer or trial version. It is for development only: its seed is Open Dental's demo database, and its only user has a blank password. An office starts from [db/README.md](db/README.md) instead.

### Database server

We test with MariaDB 10.11 on `127.0.0.1:3306`, user `root`, blank password. Those are the script's defaults. Use the server settings from [Database server](#database-server) above:

- `sql_mode=` (blank). The convert scripts need it. If the server is stricter, the tool runs `SET GLOBAL sql_mode=''` and warns, the same thing `OpenDental.exe` does at startup.
- `default-storage-engine=MyISAM`. The seed tables are MyISAM, and the convert scripts create new tables in the server's default engine. With another default the database ends up with mixed engines, and the tool warns.

### Create the dev database

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File tools\New-DevDatabase.ps1 -WithSampleData -WriteConfig
```

`-ExecutionPolicy Bypass` applies only to that one process. It's needed because Windows blocks scripts by default. The script builds `tools/DevDbTool`, a small console program, and runs it. The full solution build also builds DevDbTool; its `--export-init-sql` command writes the new office database file (`db/README.md`). Building it also builds `OpenDentBusiness`, `CodeBase` and `DataConnectionBase` if they're out of date. It:

1. Creates the database `opendental_dev` with the `utf8` character set.
2. Loads the seed `UnitTests/Resources/dump.txt`, the 21.1.9 database the unit tests start from.
3. Upgrades it to this build's version by running the convert methods in `OpenDentBusiness/Misc/ConvertDatabases*.cs`, the same code `OpenDental.exe` runs on an older database.
4. Applies the default auto codes, procedure buttons and appointment quick-add procedures, as the unit tests' `FreshFromDump` does.
5. Sets `ProgramVersion`, creates an A to Z images folder at `%USERPROFILE%\OpenDentImagesDev\<name>` and points `DocPath` at it.
6. With `-WithSampleData`, adds a few fictional providers, operatories, patients, schedules and appointments.
7. Checks the result: `DataBaseVersion` and `ProgramVersion` match the program version, no conversion is left half-done, and every table and column declared by the code's table types exists. HQ-only tables are skipped.

It takes about a minute. The database starts as the seed's fictional demo practice, with two providers and six operatories. The only user is **`Admin`, with a blank password**.

The tool talks only to the database server. It sends all .NET web requests to a dead local proxy, so if anything tries to call out over HTTP, the tool lists the addresses and exits with code 3. By default it refuses servers other than `localhost`/`127.0.0.1`. `-Force` refuses to drop a database that holds more than 200 patients.

| Option | Meaning |
|---|---|
| `-Name <db>` | Database name (default `opendental_dev`) |
| `-Server`, `-Port`, `-User`, `-Password` | Connection (default `127.0.0.1`, `3306`, `root`, blank) |
| `-Force` | Drop and recreate the database if it exists |
| `-WithSampleData` | Add the fictional sample practice data |
| `-WriteConfig` | Write `OpenDental\bin\Debug\FreeDentalConfig.xml` for this database. An existing file is kept as `.bak` |
| `-AtoZPath <dir>` / `-NoAtoZ` | Use another images folder, or keep the seed's `C:\OpenDentImages\` |
| `-VerifyOnly` | Check an existing database without changing it |
| `-SkipBuild` | Use the already-built `tools\DevDbTool\bin\Debug\DevDbTool.exe` |

`Get-Help .\tools\New-DevDatabase.ps1 -Full` lists every option.

### Point OpenDental.exe at it

`OpenDental.exe` reads its connection from `FreeDentalConfig.xml` in its own folder, `OpenDental\bin\Debug\`. Use one of these:

- Run the script with `-WriteConfig`. Add `-VerifyOnly` to only write the config for an existing database.
- Copy `tools/FreeDentalConfig.dev.xml` to `OpenDental\bin\Debug\FreeDentalConfig.xml` and set `<Database>` if you used `-Name`.
- Pass the connection on the command line: `OpenDental.exe ServerName=127.0.0.1 DatabaseName=opendental_dev MySqlUser=root`.

Then build the solution (see [Build](#build)), start `OpenDental\bin\Debug\OpenDental.exe`, and log in as `Admin` with a blank password.

## Repository layout

| Project | What it is |
|---|---|
| `OpenDental/` | The WinForms desktop application (`OpenDental.exe`): forms, modules, UI |
| `OpenDentBusiness/` | Business logic and data access: table types, `Crud/` (generated), `Db*/`, eClaims, X12, HL7, bridges, sheets, web services, the fork migrations and the release channel (`Misc/ForkMigrations`, `Misc/ForkRelease`) |
| `WpfControlsOD/` | WPF controls and newer UI hosted in the WinForms app |
| `CodeBase/` | Shared utilities with no database dependency, including stored-secret obfuscation and the `SecurityHash` tamper hashes (`CodeBase/Crypto/`, see its README) |
| `DataConnectionBase/` | Low-level MySQL/MariaDB connection layer |
| `ImagingDeviceManager/`, `ImagingCapture/` | Open source TWAIN scanning and native image capture for the Imaging module ([docs/imaging-capture-design.md](docs/imaging-capture-design.md)) |
| `ODCrypt/` | Standard hash, random and compression helpers |
| `xCrudGenerator/` | Generates `OpenDentBusiness/Crud/*` from the table types. Run it after changing a table type |
| `OpenDentalServer/` | Middle-tier web service (ASP.NET ASMX, hosted in IIS) |
| `OpenDentHL7/`, `ServiceManager/`, `CentralManager/`, `DatabaseIntegrityCheck/`, `MySQLManager/`, `UpdateFileCopier/` | Supporting services and tools; `UpdateFileCopier.exe` installs a pulled release |
| `OpenDentalCloud/`, `OpenDentalGraph/`, `SparksToothChart/`, `Direct2dWrapper/`, `PasswordVaultWrapper/` | Cloud storage, dashboards, tooth chart rendering, native Direct2D, and Windows credential vault libraries |
| `tests/` | MSTest v1 test projects, one per package, in the solution filter. Tests that need a database create a scratch database with a unique name on `127.0.0.1:3306` and drop it afterwards |
| `UnitTests/`, `UnitTestsCore/`, `NoHqTests/` | Open Dental's MSTest v1 tests (most need a live test database) and the checks that no Open Dental servers are contacted |
| `tools/` | `New-DevDatabase.ps1` and `DevDbTool/` (development database), `New-InitSql.ps1` (regenerates `db/init/new-database.sql`), `AdoptionValidation/` (adopting an Open Dental database), `SyntheticData/` (synthetic test data), `run-tests.ps1` (the test runner), `Test-NoHqHosts.ps1` (fails if code still references Open Dental's hosts), `Test-NoOldNames.ps1` (lists the program's old internal names, for the rename) |
| `docs/` | Design notes, the release process, the adoption runbook, the binaries audit and one port note per package (`docs/port-notes/`) |
| `Required dlls/` | Third-party assemblies the build references. What ships is listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt); the rest is reviewed in [docs/third-party-binaries-audit.md](docs/third-party-binaries-audit.md) |
| `ODR/`, `RdlEngine/`, `RdlViewer/` | RDL reports: `ODR.dll`, the code module report definitions call, and the fyiReporting RDL engine and viewer (LGPL 2.1), built from source in the solution and copied next to `OpenDental.exe` ([docs/port-notes/RDL-SRC.md](docs/port-notes/RDL-SRC.md)) |
| `RdlDesign/`, `DocumentationBuilder/`, `PluginExample/` | Older or standalone projects, not in the solution filter and not built |

## Branches

| Branch | Contents |
|---|---|
| `main` | Our development line: Open Dental 24.3.63 and the fork's own work |
| `24_3` | Upstream Open Dental v24.3 releases (one commit per build, e.g. `version 24.3.63.0`), kept for reference |
| `21_1` to `24_2` | Older upstream release lines, kept for reference |

The upstream branches are not merged into `main` any more (see [The solution and the solution filter](#the-solution-and-the-solution-filter)).

## Upstream resources

- [Source Code](https://www.opendental.com/site/sourcecode.html): Open Dental's own build notes
- [Programming Resources](https://www.opendental.com/site/programmingresources.html): database schema documentation
- [Previous Versions](https://opendentalsoft.com:1943/ODBugTracker/PreviousVersions.aspx): changes per release
- [Subversion (SVN)](https://opendentalsoft.com:23793/svn/opendental): source before 2022 (username **guest**, password **od123**)
- [Downloads](https://www.opendental.com/manual/downloads.html): source before 2007

Open Dental does not accept outside code contributions. See [Programming Assistance](https://www.opendental.com/site/programmingassistance.html). This repository is licensed under the GNU GPL v2 or later; see [LICENSE](LICENSE) and [NOTICE](NOTICE).
