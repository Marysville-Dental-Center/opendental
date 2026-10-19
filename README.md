# Open Dental (Marysville Dental Center)

This is Marysville Dental Center's working copy of the [Open Dental](https://www.opendental.com) source code. `main` follows Open Dental **v24.3**, currently **24.3.63.0**, and our own development branches off it.

Open Dental is a Windows desktop practice management system written in C#: WinForms, with some WPF, on .NET Framework 4.8, backed by MySQL/MariaDB.

## Branches

| Branch | Contents |
|---|---|
| `main` | Our development line, based on upstream v24.3 |
| `24_3` | Upstream Open Dental v24.3 releases (one commit per build, e.g. `version 24.3.63.0`) |
| `21_1` … `24_2` | Older upstream release lines, kept for reference |

In the upstream branches, each commit is one Open Dental build, with a message like `version 24.3.63.0`. To pick up a newer upstream build, merge it from its version branch into `main`.

## Building

### Prerequisites

- Windows 10/11
- **Visual Studio 2022** (any edition) or **Build Tools for Visual Studio 2022**, with:
  - .NET desktop build tools (`Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools`)
  - Desktop development with C++ (`Microsoft.VisualStudio.Workload.VCTools`), for `Direct2dWrapper` (native, toolset v143)
  - Web development build tools (`Microsoft.VisualStudio.Workload.WebBuildTools`), for `OpenDentalServer`
  - .NET Framework 4.8 and 4.7.2 targeting packs
  - Testing tools core (`Microsoft.VisualStudio.Component.TestTools.BuildTools`), for the MSTest v1 unit test projects

One command installs the Build Tools with everything above:

```bash
winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --override "--passive --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --add Microsoft.VisualStudio.Workload.WebBuildTools --add Microsoft.Net.Component.4.8.TargetingPack --add Microsoft.Net.Component.4.8.SDK --add Microsoft.Net.Component.4.7.2.TargetingPack --add Microsoft.VisualStudio.Component.NuGet.BuildTools --add Microsoft.VisualStudio.Component.TestTools.BuildTools"
```

### Build

Open **`OpenDental.Local.slnf`** in Visual Studio, not `OpenDental.sln`, and build `Debug | x86`. From the command line:

```bash
"C:/Program Files (x86)/Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/MSBuild.exe" OpenDental.Local.slnf -restore -p:RestorePackagesConfig=true -p:Configuration=Debug -p:Platform=x86 -m -nodeReuse:false
```

With Visual Studio installed instead of Build Tools, replace `BuildTools` in the path with your edition, e.g. `Community`. A clean build takes about 2 minutes. It produces many compiler warnings and no errors. The main program is `OpenDental\bin\Debug\OpenDental.exe`.

### Why upstream doesn't build out of the box, and what we changed

`OpenDental.sln` references 12 projects that Open Dental doesn't publish, such as `CDT`, `Bridges`, `ODApi`, `ODCrypt`, `OpenDentalHelp`, `VirtualWeb`, `ImagingDeviceManager`, `EHR`, and the web apps. They live in sibling folders outside this repo. Prebuilt DLLs for the ones the desktop app needs ship in `Required dlls/`. We fixed this without editing any upstream `.csproj` files, so upstream updates still merge cleanly:

- **`OpenDental.Local.slnf`** is a solution filter that loads only the 21 projects actually in this repo.
- **`Directory.Build.targets`** runs before references are resolved:
  - Each `ProjectReference` whose project file is missing is replaced with a reference to the matching DLL in `Required dlls/`. The DLL name is the project file name without a leading `x`, so `xCDT.csproj` becomes `CDT.dll`. The build log notes each substitution, and warns if no DLL is found.
  - The Visual Studio folder that holds MSTest v1 (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`) is added to the assembly search path.
- **`.gitignore`** ignores build output (`bin/`, `obj/`, `packages/`, `.vs/`). The `ConversionFiles` already tracked under `OpenDental/bin/Debug` remain tracked.

## Running

`OpenDental.exe` needs a MySQL or MariaDB database with its schema. `tools/New-DevDatabase.ps1` builds a development database from files in this repository, so you don't need Open Dental's installer or trial version.

### Database server

Install [MariaDB Server](https://mariadb.org/download/). We test with 10.11 on `127.0.0.1:3306`, user `root`, blank password. Those are the script's defaults. Two `my.ini` settings match a standard install:

- `sql_mode=` (blank). The convert scripts need it. If the server is stricter, the tool runs `SET GLOBAL sql_mode=''` and warns, the same thing `OpenDental.exe` does at startup.
- `default-storage-engine=MyISAM`. The seed tables are MyISAM, and the convert scripts create new tables in the server's default engine. With another default the database ends up with mixed engines, and the tool warns.

### Create the dev database

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File tools\New-DevDatabase.ps1 -WithSampleData -WriteConfig
```

`-ExecutionPolicy Bypass` applies only to that one process. It's needed because Windows blocks scripts by default. The script builds `tools/DevDbTool`, a small console program, and runs it. DevDbTool isn't in the solution. Building it also builds `OpenDentBusiness`, `CodeBase` and `DataConnectionBase` if they're out of date. It:

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

Then build the solution (see [Build](#build)), start `OpenDental\bin\Debug\OpenDental.exe`, and log in as `Admin` with a blank password. `FreeDentalConfig.xml` and `OpenDentServerConfig.xml` in the repository root are upstream's sample configs.

## Repository layout

| Project | What it is |
|---|---|
| `OpenDental/` | The WinForms desktop application (`OpenDental.exe`): forms, modules, UI |
| `OpenDentBusiness/` | Business logic and data access: table types, `Crud/` (generated), `Db*/`, eClaims, X12, HL7, bridges, sheets, web services |
| `WpfControlsOD/` | WPF controls and newer UI hosted in the WinForms app |
| `CodeBase/` | Shared utilities with no database dependency |
| `DataConnectionBase/` | Low-level MySQL/Oracle connection layer |
| `xCrudGenerator/` | Generates `OpenDentBusiness/Crud/*` from the table types. Run it after changing a table type |
| `OpenDentalServer/` | Middle-tier web service (ASP.NET ASMX, hosted in IIS) |
| `OpenDentHL7/`, `ServiceManager/`, `CentralManager/`, `DatabaseIntegrityCheck/`, `MySQLManager/`, `UpdateFileCopier/`, `WebCamOD/` | Supporting services and tools |
| `OpenDentalCloud/`, `OpenDentalGraph/`, `SparksToothChart/`, `Direct2dWrapper/`, `PasswordVaultWrapper/` | Cloud storage, dashboards, tooth chart rendering, native Direct2D, and Windows credential vault libraries |
| `UnitTests/`, `UnitTestsCore/` | MSTest v1 tests. Most need a live test database |
| `tools/` | Developer tools, not in the solution: `New-DevDatabase.ps1` and `DevDbTool/` build a local dev database (see [Running](#running)) |
| `Required dlls/`, `RequiredServerDlls/` | Third-party and Open Dental–internal prebuilt assemblies |
| `RdlDesign/`, `RdlEngine/`, `RdlViewer/`, `ODR/`, `MobileWeb/`, `TestCanada/`, … | Older or standalone projects, not in the main solution and not built by the filter |

## Upstream resources

- [Source Code](https://www.opendental.com/site/sourcecode.html): Open Dental's own build notes
- [Programming Resources](https://www.opendental.com/site/programmingresources.html): database schema documentation
- [Previous Versions](https://opendentalsoft.com:1943/ODBugTracker/PreviousVersions.aspx): changes per release
- [Subversion (SVN)](https://opendentalsoft.com:23793/svn/opendental): source before 2022 (username **guest**, password **od123**)
- [Downloads](https://www.opendental.com/manual/downloads.html): source before 2007

Open Dental does not accept outside code contributions. See [Programming Assistance](https://www.opendental.com/site/programmingassistance.html). This repository is licensed under the GNU GPL v2; see [LICENSE](LICENSE).
