<img src="branding/logo-256.png" alt="Open PMS - Dental logo" width="96">

# Open PMS - Dental

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This is a fork of the [Open Dental](https://www.opendental.com) source code, written for one dental practice and published for anyone to use. `main` started from Open Dental **v24.3**, at **24.3.63.0**. Features of later Open Dental versions, up to 26.1, are re-implemented from Open Dental's public documentation, and the database schema follows Open Dental 26.2 (26.2.0, from its published database documents). See [NOTICE](NOTICE) for attribution, [LICENSE](LICENSE) for the GNU General Public License and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for the third-party components the program ships. Every copyright line and GPL notice from Open Dental is kept.

The source files say "either version 2 of the License, or (at your option) any later version", so the program is licensed under the **GNU GPL v2 or later**. LICENSE holds the version 2 text.

The program as distributed includes PDFsharp and MigraDoc 6.5, which are licensed under the **GNU AGPL v3**. The AGPL combines with GPL version 3 but not with version 2, so the program with them is distributed under the **GNU GPL v3** (which "or later" allows), and PDFsharp and MigraDoc keep the AGPL's terms. By section 13 of both licenses, the AGPL's requirements on interaction through a network apply to the combination as a whole. They apply to a modified version, and the PDFsharp built here is itself one (one file replaced, see below), so they cover any version of the program that users reach over a network, not only versions someone else modifies: a hosted Middle Tier, even one without local changes, must prominently offer its users the Corresponding Source of the whole program, such as a link to the release's `OpenPmsDental-<version>-source.zip`. [LICENSE-GPL-3.0.txt](LICENSE-GPL-3.0.txt) and [LICENSE-AGPL-3.0.txt](LICENSE-AGPL-3.0.txt) hold those texts; [NOTICE](NOTICE) explains it.

PDFsharp and MigraDoc 6.5.0 (empira's AGPL source, tag v6.5.0) and WeCantSpell.Hunspell 7.0.1 are built here from their source, git submodules under `third-party/`. The PDFsharp built here replaces one file, empira's wrapper around its closed `PdfSharp.License.dll`, with an open one (a modified version under AGPL section 5a), so every file the setup installs has published source. Each release publishes its complete source, the repository and the submodules at the release commit, as `OpenPmsDental-<version>-source.zip` next to the setup ([docs/third-party-source-builds.md](docs/third-party-source-builds.md)).

The program's namespaces, assemblies and executable carry its code name, `OpenPmsDental` (`OpenPmsDental.exe`, `OpenPmsDentalBusiness.dll`; the mapping from Open Dental's names is in [docs/port-notes/RENAME.md](docs/port-notes/RENAME.md)). The product name, "Open PMS - Dental", appears in the window title, the splash, the About box, the program's messages, the file properties of every shipped assembly (version 0.0), the product identity sent to vendors, and this repository's documents. Names that existing installations and databases depend on keep Open Dental's spelling: the database (tables, columns, preference names, stored enum values), `FreeDentalConfig.xml`, the Middle Tier's web service names and IIS path, and internal type names such as `FormOpenDental`. The solution files keep their names, `OpenDental.sln` and `OpenDental.Local.slnf`.

Open PMS - Dental is a Windows desktop practice management system written in C#: WinForms, with some WPF, on .NET Framework 4.8, backed by MariaDB.

## Installing in an office

A release is a setup program, `OpenPmsDental-<version>-Setup.exe`, with its `SHA256SUMS.txt` and release notes ([docs/release-notes/v0.0.md](docs/release-notes/v0.0.md) for 0.0). `tools/Build-Release.ps1` builds it from a **Release | x86** build ([docs/release-process.md](docs/release-process.md#building-the-installer)). Nothing here contacts Open Dental's servers, and nothing is installed on the database server by this program.

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
- A server the workstations reach over the network must have TLS with a certificate from the office's own CA, and each workstation needs that CA certificate and the client certificate: see [docs/db-connection-security.md](docs/db-connection-security.md), which also gives the order for an office that runs version 0.0.

**A new office's database:** see [db/README.md](db/README.md) and the script `db/init/new-database.sql`.

**An existing Open Dental database** (Open Dental 24.3.63 or 25.3.48 only) is adopted once, on a restored copy: see [Adopting a database](docs/release-process.md#adopting-a-database) and the office procedure in [docs/adoption-runbook.md](docs/adoption-runbook.md).

### Workstations

Prerequisites:

- Windows 10 version 1903 or later, or Windows 11
- **.NET Framework 4.8** (Windows 10 1903 and later and Windows 11 already have it; the setup stops if it is missing)
- **Microsoft Visual C++ 2015-2022 Redistributable, x86** (`vc_redist.x86.exe`; `Direct2dWrapper.dll` needs it)
- **Microsoft Edge WebView2 Runtime, x86** (Evergreen; Windows 11 already has it). Every window that shows a page uses it: the wiki, email previews, PDFs in the Imaging module, vendor pages. The program keeps its WebView2 data per Windows user in `%LOCALAPPDATA%\OpenPMS-Dental\WebView2`.
- For scanners and sensors: the device's own TWAIN driver, which brings the TWAIN data source manager

When the Visual C++ runtime is missing, the setup offers to download Microsoft's installer from Microsoft and run it, or to skip it. The setup only checks for the WebView2 Runtime: when it is missing, the setup says so and how to install it (Microsoft's Evergreen Bootstrapper or Standalone Installer, Windows Update or device management), and it never downloads it.

Steps:

1. Install the program on the **update server** first: the workstation named in the `WebServiceServerName` preference (Setup > Update Setup).
2. Check the setup's SHA-256 against `SHA256SUMS.txt`, then run it as an administrator. It installs into `C:\Program Files (x86)\Open PMS - Dental\`, creates the Start menu shortcut "Open PMS - Dental", and writes `FreeDentalConfig.xml` from its database connection page (server, database, user; never a password). For a database server over the network the next page asks for the office CA certificate and the client certificate (`.pfx`). Setup then opens the connection settings tool, `OpenPmsDentalDbSettings.exe`, which imports the client certificate or stores the database password on this computer with Windows DPAPI; for a database on the same computer it opens in password mode (Cancel when the database user has no password). The setup is not signed, so SmartScreen may warn.
3. Start the program from the Start menu. It never asks for the database password: to change the connection later, an administrator runs `OpenPmsDentalDbSettings.exe` in the program folder.
4. On the update server, the program offers to publish the release at that first start, so the other workstations update to it.
5. On the other workstations repeat steps 2 and 3. A workstation that runs an older build pulls the published one when it starts.

Silent installs, the setup's switches and the uninstall are described in the release notes.

### Updates

A new build reaches the office through the release channel described in [docs/release-process.md](docs/release-process.md): install it on the update server and start it as an administrator; it publishes the release into the database, and every other workstation pulls it at its next start and restarts through `UpdateFileCopier.exe`. Debug builds never publish or pull. A workstation that is not the update server refuses to run a build newer than the published one (exit code 174), so always update the update server first.

## Building

### Prerequisites

- Windows 11, 64-bit
- **Visual Studio 2026** (any edition) or **Build Tools for Visual Studio 2026** (version 18; both install under `Microsoft Visual Studio\18\`), with:
  - .NET desktop build tools (`Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools`)
  - Desktop development with C++ (`Microsoft.VisualStudio.Workload.VCTools`) and its MSVC v143 toolset (`Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64`, MSVC 14.44), for `Direct2dWrapper` (native, toolset v143; the default toolset of 2026 is v145)
  - Web development build tools (`Microsoft.VisualStudio.Workload.WebBuildTools`), for `OpenPmsDentalServer`
  - .NET Framework 4.8 SDK and the 4.8 and 4.7.2 targeting packs (`Microsoft.Net.Component.4.8.SDK`, `Microsoft.Net.Component.4.8.TargetingPack`, `Microsoft.Net.Component.4.7.2.TargetingPack`)
  - Testing tools core (`Microsoft.VisualStudio.Component.TestTools.BuildTools`), for the MSTest v1 test projects
  - NuGet build tools (`Microsoft.VisualStudio.Component.NuGet.BuildTools`) and the Windows 11 SDK 10.0.26100 (`Microsoft.VisualStudio.Component.Windows11SDK.26100`)
- **The .NET SDK 10.0.114** exactly: `global.json` pins it, for the projects that move to the SDK project format (stage 1 of the .NET 10 move, [docs/specs/net10-plan.md](docs/specs/net10-plan.md)). Install it machine-wide, or per user into `%LOCALAPPDATA%\Microsoft\dotnet` (`dotnet-install.ps1 -Version 10.0.114` does that). The SDK that Visual Studio installs itself is a later band and is not used.
- **The offline package folder.** `nuget.config` restores from one local folder only, the one the environment variable `OPMS_NUGET_OFFLINE` names; nothing is downloaded during a build. The project's own build PC keeps it in the git folder (`.git\od-tools\nuget-packages`), which `tools\Initialize-BuildEnvironment.ps1` finds by itself. Elsewhere, put the packages the build restores into a folder of your own and set the variable. Today those are the packages of `Directory.Packages.props` (the libraries and support assemblies whose files `Required dlls/` also holds, byte for byte, and `Mono.Cecil` 0.10.3 for the test projects that read assemblies) and `System.ValueTuple` 4.6.2, which two of them depend on; every `packages.lock.json` names each package with its version and content hash. No project restores into a `packages` folder any more.

One command installs the Build Tools with everything above, from the installer of the Visual Studio 2026 channel (`https://aka.ms/vs/18/stable/vs_buildtools.exe`):

```bash
vs_buildtools.exe --passive --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --add Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64 --add Microsoft.VisualStudio.Workload.WebBuildTools --add Microsoft.Net.Component.4.8.TargetingPack --add Microsoft.Net.Component.4.8.SDK --add Microsoft.Net.Component.4.7.2.TargetingPack --add Microsoft.VisualStudio.Component.NuGet.BuildTools --add Microsoft.VisualStudio.Component.TestTools.BuildTools --add Microsoft.VisualStudio.Component.Windows11SDK.26100
```

### Get the source

PDFsharp/MigraDoc and the WeCantSpell.Hunspell spell checker are built from their upstream source, which this repository references as git submodules instead of copying it (`third-party/PDFsharp`, `third-party/WeCantSpell.Hunspell`; [docs/third-party-source-builds.md](docs/third-party-source-builds.md)). Clone with them, or fetch them after cloning:

```bash
git clone --recurse-submodules <repository address>
git submodule update --init
```

Without them the build stops with "The upstream source is missing".

### Build

Open **`OpenDental.Local.slnf`** in Visual Studio, not `OpenDental.sln`, and build `Debug | x86` (or `Release | x86` for an office). From the command line, in PowerShell at the repository root:

```powershell
. .\tools\Initialize-BuildEnvironment.ps1; Initialize-BuildEnvironment -RepoRoot (Get-Location).Path
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" OpenDental.Local.slnf -restore -p:RestoreLockedMode=true -p:Configuration=Debug -p:Platform=x86 -m -nodeReuse:false
```

`Initialize-BuildEnvironment` sets, for that PowerShell window only, what the build needs: with a per-user .NET SDK, `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` and `PATH`, so MSBuild finds that SDK; `OPMS_NUGET_OFFLINE` when it is not set; no .NET telemetry. The tool scripts under `tools/` call it themselves. With a per-user SDK, start Visual Studio from that window, so it finds the SDK too. With Visual Studio installed instead of Build Tools, replace `BuildTools` in the path with your edition, e.g. `Community`. A clean build produces many compiler warnings and no errors. The main program is `OpenPmsDental\bin\Debug\OpenPmsDental.exe` (`OpenPmsDental\bin\Release\` for a Release build). The build also copies `UpdateFileCopier.exe` and the native file the program loads at run time, `WebView2Loader.dll`, next to it.

### The solution and the solution filter

- **`OpenDental.Local.slnf`** loads the program and its libraries, services and tools, the adoption and synthetic-data tools under `tools/`, and every test project under `tests/`. The older or standalone projects listed under [Repository layout](#repository-layout) are not in it. `OpenDental.sln` lists the same projects, so either one builds the same set. The build scripts read the filter, so add a new project to both.
- **`Directory.Build.targets`** adds the folder that holds MSTest v1 (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`) to the assembly search path and gives the test projects their binding redirects. Every project is built from source: a missing project fails the build, and nothing is swapped for a DLL in `Required dlls/`.
- **`Directory.Build.props`** and **`tests/Directory.Build.props`** hold the settings of the SDK-style projects only (flat `bin\Debug\` and `bin\Release\` output, x86, lock files, no generated assembly info); the classic projects ignore them. **`Directory.Packages.props`** names the version of every NuGet package (central package management); a project's `PackageReference` names none. **`global.json`** pins the .NET SDK, **`nuget.config`** the offline package folder. `tools\Get-ProjectBaseline.ps1` records what each project compiles, embeds and outputs, and compares that with the record in `tools/project-baseline/` taken before the change of project format.
- **Version numbers:** `SharedAssemblyInfo.cs` in the repository root gives every shipped assembly its product name, copyright and release version (`0.0`). Each project's `AssemblyVersion` stays on its own line; for `OpenPmsDental.exe` and `OpenPmsDentalBusiness.dll` that is the compatibility version `24.3.63.0`, which the database checks use ([docs/release-process.md](docs/release-process.md)).
- **We don't merge from upstream.** Open Dental's public source stops at 24.3.63; later features are re-implemented from public documentation ([docs/decoupling-plan.md](docs/decoupling-plan.md)).
- **`.gitignore`** ignores build output (`bin/`, `obj/`, `packages/`, `.vs/`). The `ConversionFiles` already tracked under `OpenPmsDental/bin/Debug` remain tracked.

## Running a development copy

`OpenPmsDental.exe` needs a MariaDB database with its schema. `tools/New-DevDatabase.ps1` builds a development database from files in this repository, so you don't need Open Dental's installer or trial version. It is for development only: its seed is Open Dental's demo database, and its only user has a blank password. An office starts from [db/README.md](db/README.md) instead.

### Database server

We test with MariaDB 10.11 on `127.0.0.1:3306`, user `root`, blank password. Those are the script's defaults. Use the server settings from [Database server](#database-server) above:

- `sql_mode=` (blank). The convert scripts need it. If the server is stricter, the tool runs `SET GLOBAL sql_mode=''` and warns, the same thing `OpenPmsDental.exe` does at startup.
- `default-storage-engine=MyISAM`. The seed tables are MyISAM, and the convert scripts create new tables in the server's default engine. With another default the database ends up with mixed engines, and the tool warns.

### Create the dev database

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File tools\New-DevDatabase.ps1 -WithSampleData -WriteConfig
```

`-ExecutionPolicy Bypass` applies only to that one process. It's needed because Windows blocks scripts by default. The script builds `tools/DevDbTool`, a small console program, and runs it. The full solution build also builds DevDbTool; its `--export-init-sql` command writes the new office database file (`db/README.md`). Building it also builds `OpenPmsDentalBusiness`, `CodeBase` and `DataConnectionBase` if they're out of date. It:

1. Creates the database `opendental_dev` with the `utf8` character set.
2. Loads the seed `UnitTests/Resources/dump.txt`, the 21.1.9 database the unit tests start from.
3. Upgrades it to this build's version by running the convert methods in `OpenPmsDentalBusiness/Misc/ConvertDatabases*.cs`, the same code `OpenPmsDental.exe` runs on an older database.
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
| `-WriteConfig` | Write `OpenPmsDental\bin\Debug\FreeDentalConfig.xml` for this database. An existing file is kept as `.bak` |
| `-AtoZPath <dir>` / `-NoAtoZ` | Use another images folder, or keep the seed's `C:\OpenDentImages\` |
| `-VerifyOnly` | Check an existing database without changing it |
| `-SkipBuild` | Use the already-built `tools\DevDbTool\bin\Debug\DevDbTool.exe` |

`Get-Help .\tools\New-DevDatabase.ps1 -Full` lists every option.

### Point OpenPmsDental.exe at it

`OpenPmsDental.exe` reads its connection from `FreeDentalConfig.xml` in its own folder, `OpenPmsDental\bin\Debug\`. Use one of these:

- Run the script with `-WriteConfig`. Add `-VerifyOnly` to only write the config for an existing database.
- Copy `tools/FreeDentalConfig.dev.xml` to `OpenPmsDental\bin\Debug\FreeDentalConfig.xml` and set `<Database>` if you used `-Name`.
- Pass the connection on the command line: `OpenPmsDental.exe ServerName=127.0.0.1 DatabaseName=opendental_dev MySqlUser=root`.

Then build the solution (see [Build](#build)), start `OpenPmsDental\bin\Debug\OpenPmsDental.exe`, and log in as `Admin` with a blank password.

## Repository layout

| Project | What it is |
|---|---|
| `OpenPmsDental/` | The WinForms desktop application (`OpenPmsDental.exe`): forms, modules, UI |
| `OpenPmsDentalBusiness/` | Business logic and data access: table types, `Crud/` (generated), `Db*/`, eClaims, X12, HL7, bridges, sheets, web services, the fork migrations and the release channel (`Misc/ForkMigrations`, `Misc/ForkRelease`) |
| `WpfControlsOD/` | WPF controls and newer UI hosted in the WinForms app |
| `CodeBase/` | Shared utilities with no database dependency, including stored-secret obfuscation and the `SecurityHash` tamper hashes (`CodeBase/Crypto/`, see its README) |
| `DataConnectionBase/` | Low-level MySQL/MariaDB connection layer |
| `ImagingDeviceManager/`, `ImagingCapture/` | Open source TWAIN scanning and native image capture for the Imaging module ([docs/imaging-capture-design.md](docs/imaging-capture-design.md)) |
| `ODCrypt/` | Standard hash, random and compression helpers |
| `xCrudGenerator/` | Generates `OpenPmsDentalBusiness/Crud/*` from the table types. Run it after changing a table type |
| `OpenPmsDentalServer/` | Middle-tier web service (ASP.NET ASMX, hosted in IIS). Its IIS path stays /OpenDentalServer, so the workstations' saved addresses and logins keep working |
| `OpenPmsDentalHL7/`, `ServiceManager/`, `CentralManager/`, `DatabaseIntegrityCheck/`, `MySQLManager/`, `UpdateFileCopier/` | Supporting services and tools; `UpdateFileCopier.exe` installs a pulled release |
| `OpenPmsDentalStorage/`, `OpenPmsDentalGraph/`, `SparksToothChart/`, `Direct2dWrapper/`, `PasswordVaultWrapper/` | SFTP A to Z storage, dashboards, tooth chart rendering, native Direct2D, and Windows credential vault libraries |
| `tests/` | MSTest v1 test projects, one per package, in the solution filter. Tests that need a database create a scratch database with a unique name on `127.0.0.1:3306` and drop it afterwards |
| `UnitTests/`, `UnitTestsCore/`, `NoHqTests/` | Open Dental's MSTest v1 tests and their helpers, and the checks that no Open Dental servers are contacted. `tools/run-tests.ps1` runs the legacy `UnitTests` project (`Unit Tests.exe`, named `UnitTests` in `-Include`) with the others. It builds its own database `unittest<version>_<time>_<process>` on `127.0.0.1:3306` from the seed dump and drops it at the end (set `OPENPMS_UNITTESTS_KEEPDB=1` to keep it). Its developer windows under `UI Testing/` are opened by hand, not run as tests |
| `tools/` | `New-DevDatabase.ps1` and `DevDbTool/` (development database), `New-InitSql.ps1` (regenerates `db/init/new-database.sql`), `AdoptionValidation/` (adopting an Open Dental database), `SyntheticData/` (synthetic test data), `run-tests.ps1` (the test runner), `Initialize-BuildEnvironment.ps1` (finds MSBuild and vstest of the Build Tools 2026 and sets the build's environment, for the other scripts), `Get-ProjectBaseline.ps1` (records and compares what each project compiles, embeds and outputs; the record is `project-baseline/`), `Test-NoHqHosts.ps1` (fails if code still references Open Dental's hosts), `Test-NoOldNames.ps1` (lists the program's old internal names, for the rename), `Build-Release.ps1` (the release: staging, checks, checksums and the setup) and `Test-Installer.ps1` (a per-user test install and uninstall of the setup) |
| `installer/` | The Inno Setup 6 script of the workstation setup (`OpenPmsDental.iss`) and its information page |
| `docs/` | Design notes, the release process, the adoption runbook, the binaries audit and one port note per package (`docs/port-notes/`) |
| `Required dlls/` | Third-party assemblies the build references. What ships is listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt); the rest is reviewed in [docs/third-party-binaries-audit.md](docs/third-party-binaries-audit.md) |
| `third-party/` | Upstream source the program builds instead of shipping prebuilt files: the git submodules `PDFsharp` (PDFsharp/MigraDoc 6.5.0, empira's AGPL source) and `WeCantSpell.Hunspell` (7.0.1), pinned to their release commits, and `build/`, this project's own project files that compile them in the solution, with the one PDFsharp file the build replaces ([docs/third-party-source-builds.md](docs/third-party-source-builds.md)) |
| `ODR/`, `RdlEngine/`, `RdlViewer/` | RDL reports: `ODR.dll`, the code module report definitions call, and the fyiReporting RDL engine and viewer (LGPL 2.1), built from source in the solution and copied next to `OpenPmsDental.exe` ([docs/port-notes/RDL-SRC.md](docs/port-notes/RDL-SRC.md)) |
| `RdlDesign/` | fyiReporting's RDL report designer (`RdlDesigner.exe`), built in the solution against `RdlEngine/` and `RdlViewer/`; a developer tool, not shipped |
| `DocumentationBuilder/` | Open Dental's documentation tool: the schema documentation (`OpenDentalDocumentation.xml` and the diff against a previous version), the unit test and the preference documentation; built in the solution against the program; a developer tool, not shipped |
| `PluginExample/` | Older standalone project, not in the solution filter and not built |

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

Open Dental does not accept outside code contributions. See [Programming Assistance](https://www.opendental.com/site/programmingassistance.html). This repository is licensed under the GNU GPL v2 or later, and the program as distributed with PDFsharp and MigraDoc under the GNU GPL v3; see [LICENSE](LICENSE), [LICENSE-GPL-3.0.txt](LICENSE-GPL-3.0.txt) and [NOTICE](NOTICE).
