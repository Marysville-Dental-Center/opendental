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

Open Dental needs a MySQL/MariaDB database with its schema. The only supported way to create the initial database is to install the [Open Dental trial version](https://www.opendental.com/trial.html). After that, point the built `OpenDental.exe` at that database. `FreeDentalConfig.xml` and `OpenDentServerConfig.xml` in the repo root are sample connection configs.

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
| `Required dlls/`, `RequiredServerDlls/` | Third-party and Open Dental–internal prebuilt assemblies |
| `RdlDesign/`, `RdlEngine/`, `RdlViewer/`, `ODR/`, `MobileWeb/`, `TestCanada/`, … | Older or standalone projects, not in the main solution and not built by the filter |

## Upstream resources

- [Source Code](https://www.opendental.com/site/sourcecode.html): Open Dental's own build notes
- [Programming Resources](https://www.opendental.com/site/programmingresources.html): database schema documentation
- [Previous Versions](https://opendentalsoft.com:1943/ODBugTracker/PreviousVersions.aspx): changes per release
- [Subversion (SVN)](https://opendentalsoft.com:23793/svn/opendental): source before 2022 (username **guest**, password **od123**)
- [Downloads](https://www.opendental.com/manual/downloads.html): source before 2007

Open Dental does not accept outside code contributions. See [Programming Assistance](https://www.opendental.com/site/programmingassistance.html). This repository is licensed under the GNU GPL v2; see [LICENSE](LICENSE).
