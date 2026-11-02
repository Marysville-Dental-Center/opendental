# Port note: REL-FILES (release files and product identity)

Package REL-FILES of the run before the rename, built on branch `pre/rel-files-1` from `main` (d9c177ca6). Sources: the read-only review of
`main` (report-release.md sections 1.6, 3 and 4, actions REL-12, REL-15, REL-18, REL-19 and REL-26; report-rename.md 3.6 and 3.7 with
decisions D2 and D7; report-organization.md F11). No Open Dental binary was opened. Of two third-party DLLs (Microsoft's `WebView2Loader.dll` and `Hunspellx86.dll`)
only the PE machine field was read (both are x86). Only the tests touched a database: per-run scratch databases on 127.0.0.1.

## What changed

### 1. Native files the build did not copy

`OpenDental.csproj` copies `Required dlls\Hunspellx86.dll` and `Required dlls\WebView2Loader.dll` (both x86) to the top of the output folder,
as `None` items with `CopyToOutputDirectory=PreserveNewest` and no condition, so Debug and Release builds both have them. Without
`Hunspellx86.dll` every `ODtextBox` throws in a Release build (`ODtextBox.cs:207-208`; only the Debug branch copied the file at run time).
Without `WebView2Loader.dll` every WebView2 window fails (`CodeBase/Controls/ODWebView2.cs`). A release publishes every top-level file, so
both now reach the other workstations too.

Projects that reference `OpenDental.csproj` (test projects) get the two files in their own output as well; that is harmless.

### 2. License list

`FormLicense` (Help > About > View Licenses) and `FormRegistrationKey` show one list, built by `LicenseList` in `FormLicense.cs`:

1. **Open PMS - Dental (GNU GPL v2 or later)**, selected by default: the attribution, the GPL notice of the source headers ("either version 2
   of the License, or (at your option) any later version") and the GPL version 2 text (`Resources/GPL.txt`, now a resource).
2. **Third-party notices**: `THIRD-PARTY-NOTICES.txt` from the repository root, embedded as the `ThirdPartyNotices` resource (a `ResXFileRef`
   to `..\..\THIRD-PARTY-NOTICES.txt`, so there is one copy of the file).
3. One entry per component listed in that file, in its order: the component's notice, then the project's own license text where Open Dental's
   list had one (MigraDoc, PDFsharp, Newtonsoft.Json, SharpDX, SSH.NET, SharpSSH, Mentalis, NHunspell, NDde, Tao, TWAIN, Pearl), otherwise
   the standard text in the notices file that the component's License field names.

`THIRD-PARTY-NOTICES.txt` lists every third-party component the release ships (not the files packages BIN-A and BIN-B delete, nor Open
Dental's own closed binaries) with its files, version, copyright holders, license and source
address, then the MIT, BSD 3-Clause, MS-PL and Apache 2.0 texts. Its format (`--- Component: name`, `--- License: title`, `Field:` lines) is
what `LicenseList.ParseNotices` reads, and `tests/AttributionTests/ReleaseIdentityTests.cs` checks both.

New in the list, as report-release.md 1.4 asked: the GPL itself, MySqlConnector, MimeKit, the MIT .NET libraries (`System.*`,
`Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Extensions.Logging.Abstractions`), MSAL.NET, Google APIs, fo-dicom, RDL Project, NHunspell with
the native Hunspell and the en_US dictionary, Direct Project, OpenPop.NET and the WebView2 SDK.

### 3. Version metadata

`SharedAssemblyInfo.cs` in the repository root is linked (`<Compile Include="..\SharedAssemblyInfo.cs"><Link>Properties\SharedAssemblyInfo.cs</Link>`)
into the 19 shipped projects: CentralManager, CodeBase, DatabaseIntegrityCheck, DataConnectionBase, ImagingCapture, ImagingDeviceManager,
MySQLManager, ODCrypt, OpenDental, OpenDentalCloud, OpenDentalGraph, OpenDentalServer, OpenDentBusiness, OpenDentHL7, PasswordVaultWrapper,
ServiceManager, SparksToothChart, UpdateFileCopier and WpfControlsOD. It sets:

| Attribute | Value |
|---|---|
| `AssemblyProduct` | `Open PMS - Dental` |
| `AssemblyCompany` | `Open PMS - Dental contributors` (INFERRED) |
| `AssemblyCopyright` | `Copyright (C) 2003 and later years, Jordan Sparks, D.M.D., Open Dental Software, Inc. and contributors. Changes in Open PMS - Dental: Copyright (C) 2026 and later years, Open PMS - Dental contributors. GNU GPL v2 or later.` |
| `AssemblyInformationalVersion` | `0.0` (moved here from `OpenDental/Properties/AssemblyInfo.cs`) |

Each project's own `AssemblyInfo.cs` lost its `AssemblyProduct`, `AssemblyCompany` and `AssemblyCopyright` lines (among them CentralManager's
"Microsoft" company and copyright, and the "Copyright (c) 2006" template lines) and keeps its title, description, `AssemblyVersion` and
`AssemblyFileVersion`. No version number changed: `OpenDental.exe` and `OpenDentBusiness.dll` stay on `24.3.63.0` and `OpenDental.exe`'s
file version follows it (`VersionSourceTests`). The odd ones keep their odd versions (UpdateFileCopier file version 13.1.0.0, ServiceManager
15.4.1.0, CentralManager 1.0.0.0, SparksToothChart 4.7.1.0, OpenDentalGraph 16.1.1.0, OpenDentalCloud 16.4.0.0); only their product,
company and copyright changed. `tests/ReleaseFlowTests/ReleaseFilesTests.cs` pins every one of them.

Nothing reads the company or product name to build a path: there are no user-scoped settings and no `Application.UserAppDataPath` or registry
use. `Application.ProductName` is used once, as a message box caption (`FormEligibilityResponseDisplay.cs:560`), which now reads "Open PMS -
Dental".

### 4. Code that read the release version as a version number

With the shared file every shipped program's `Application.ProductVersion` is `0.0`, not only `OpenDental.exe`'s. Each site:

| Site | Was | Now | Why |
|---|---|---|---|
| `ClaimConnect.cs` `Launch` (DentalXChange `UploaderVersion`) | `Application.ProductVersion` | `ForkVersions.CompatVersion` (24.3.63) | Sent to the vendor: the compatibility version |
| `ClaimConnect.cs` `Benefits270`, `GetPayerList` (`credentials.version`) | `Application.ProductVersion` | `ForkVersions.CompatVersion` (24.3.63.0) | Sent to the vendor |
| `CentralConnections.cs:243` middle tier login | `Application.ProductVersion` | `ForkVersions.CompatVersion.ToString(4)` | The middle tier compares it with its own `OpenDentBusiness` version; "0.0" always passed |
| `DemandForce.cs:57` `managementSystemVersion` | `new Version(Application.ProductVersion)` | `ForkVersions.CompatVersion` | Written for DemandForce to read |
| `FormUpdateSetup.cs:102` Recopy | `new Version(Application.ProductVersion)` | `ForkVersions.CompatVersion` | A version number passed on (the parameter is unused today); never shown |
| `FormUpdate.cs:26-27` | label and lookup by `Application.ProductVersion` | label: release version (`AboutBoxInfo.GetReleaseVersion`); lookup: `AboutBoxInfo.GetUpdateHistory` | Shown: the release version. The "Since" date: see below |
| `OpenDentHL7/ServiceHL7.cs:105` (outside this package) | `Application.ProductVersion` against `ProgramVersion` | `ForkVersions.CompatVersion.ToString(4)` | OpenDentHL7.exe's product version became "0.0" with the shared file; without this the service would refuse every database. Same behaviour as before |

The "Since" date in About and Update (INFERRED): `updatehistory` rows are written under the release version when the update server publishes a
release (`PrefL.PublishRelease`) and under the compatibility version by the legacy chain and the development database tool. Both windows now
look up the release version first, then the compatibility version (`AboutBoxInfo.GetUpdateHistoryVersions`). Before, About used only the
assembly version and Update only the release version.

### 5. Product name at display time (decision D2) and Benco (decision D7)

- `AboutBoxInfo.GetDisplayName` shows `Open PMS - Dental` when the stored `MainWindowTitle` or `SoftwareName` is Open Dental's default ("Open
  Dental", "Open Dental Software"; ignoring case and surrounding spaces) or one of the two values the Benco switch wrote. Any other value, an
  office's own title, is shown as stored. The preference rows are only read, so adoption's row check (`AdoptionRowCheck`) is unaffected.
- Used by the title bar (`PatientL.GetMainTitle`, `GetMainTitleSamePat`), the three restart prompts in `FormOpenDental.IsDbConnectionSafe`,
  and `FormUpdateInProgress`.
- About writes Open Dental's copyright line out: `"Open Dental Software "+Lan.g(this,"Copyright 2003-")+<year>+", Jordan Sparks, D.M.D."`,
  which is what the line said with Open Dental's default `SoftwareName`. It no longer changes when `SoftwareName` does.
- `FrmBencoSetup.UpdateBencoSettings` only switched `MainWindowTitle` and `SoftwareName` between Open Dental's values and Benco's reseller
  branding when the Benco program link was enabled or disabled. It is removed (ground rule 7). The program link itself (enable, path,
  description, toolbar buttons) is unchanged. A database that still holds the Benco values shows the product name instead (INFERRED: the two
  Benco values are in the default list because the removed switch was the only way back).

### 6. README.md and NOTICE

- README says "GNU GPL v2 or later" and quotes the source headers; the attribution sentence is unchanged, word for word, as is NOTICE's.
- New section "Installing in an office": MariaDB 10.11 with `sql_mode=` blank, `default-storage-engine=MyISAM` and `max_allowed_packet` of at
  least 40 MB (`DataConnection.cs:782` asks for 40000000), one account per office and backups; a new office's database
  (`db/README.md`, `db/init/new-database.sql`, from package INIT-SQL); adopting an Open Dental 24.3.63 or 25.3.48 database; the workstation
  prerequisites (.NET Framework 4.8, VC++ 2015-2022 x86, WebView2 runtime x86, the scanner's TWAIN driver); install steps (update server
  first, `C:\Program Files (x86)\Open PMS - Dental\`, first start as administrator because it creates `FreeDentalConfig.xml`, Start menu
  shortcut); and the update path (`docs/release-process.md`, exit 174).
- F11 corrections: no "21 projects" count (the filter now loads the program, the tools and every test project; no number, so it does not go
  stale again), no list of 12 missing projects (the substitution target substitutes nothing today), no prebuilt Open Dental DLLs in `Required
  dlls`, no advice to merge upstream builds (decoupling plan ground rule 1), and `tools/` and `tests/` are described as in the solution
  filter. The development database is described as development only.
- NOTICE says "version 2 or (at your option) any later version", points to `THIRD-PARTY-NOTICES.txt` and names the real menu path (Help >
  About > View Licenses).

## Removed, and why

| Removed | Why |
|---|---|
| License list entry "OpenDental" and `Resources/opendentallicense.txt` | Open Dental's paid registration-key terms; they contradict the GPL |
| "OpenDental API End User" and `Resources/opendentalapienduserlicense.txt` | The local API was removed |
| "CDT" and `Resources/CDT Content End User License.txt` and `.rtf` (both resources) | ADA licence text; no CDT content is included (ground rule 4) |
| "Sparks3D" and `Resources/Sparks3D.txt` | Proprietary; package BIN-A/BIN-B removes Sparks3D |
| "Stdole" and `Resources/stdole.txt` | A Visual Studio 2015 pre-release evaluation licence; `stdole.dll` may not be redistributed and is removed |
| "Drifty" and `Resources/Ionic.txt` | Ionic Framework's MIT text, shown for DotNetZip by mistake; DotNetZip is removed |
| "Zxing" and `Resources/Zxing.txt` | zxing is removed |
| "AForge", "Angular", "Bouncy Castle", "BSD", "Microsoft", "Oracle", "Tao_Freeglut" and their texts | No file of theirs ships: AForge, Angular and the CodePlex Foundation BSD text (Ajax Control Toolkit) belong to nothing in the output; Bouncy Castle is MimeKit's missing dependency; "Microsoft" was the old .NET Library EULA, while every shipped Microsoft library is MIT or the WebView2 SDK licence; Oracle's developer-tools terms cover no shipped file; freeglut is not shipped. INFERRED for each of these from the output folder and the audit |
| `FrmBencoSetup.UpdateBencoSettings` | Reseller branding (D7) |
| `using Ionic.Zip;` in `FormUpdateSetup.cs` | Unused; keeps the file compiling once DotNetZip is removed |

`Resources/MySql.txt` (a GPL v2 copy that no list showed) is left as it was.

## Left for later

- Other readers of `Application.ProductVersion`, outside this package, still get "0.0" and need no change for v0.0: `ControlAppt.cs:272` shows
  it ("OD Version:", shown, so the release version is right; the rename rewords the label), `FormProgramLinkEdit.cs:453` parses it into an
  unused variable (works with "0.0"), `CentralManager/FormCentralChooseDatabase.cs:215` sends it to the middle tier (was "1.0.0.0"; the
  server only refuses a newer client, so the outcome is unchanged; switching it to `ForkVersions.CompatVersion` is a one-line follow-up),
  `UnitTests/FormUnitTests.cs:70` (test harness) and `PluginExample` (not built).
- Other readers of `SoftwareName` (`FormCdsTriggerEdit`, `FormCreditCardEdit`, `FormEhrSettings`, `FormPatListResultsEHR2014`) still show the
  stored value; the rename run can switch them to `AboutBoxInfo.GetSoftwareName()`.
- `FormUpdateSetup`'s Recopy label names `C:\Program Files\Open Dental\`, and `FrmBencoSetup`'s save message says "restart Open Dental":
  visible text for the rename run.
- `DemandForce.cs` still writes `managementSystemName="Open Dental"` (the name DemandForce's import expects; owner question).
- `docs/decoupling-plan.md:109` still says the "Since" date is looked up by the assembly version.
- OpenDentHL7 still refuses an adopted database (sentinel `ProgramVersion` 99.0.0.0, report-release.md 3); this package only kept its
  behaviour.
- The installer must ship `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt` in the program folder and verify each copyright line against the
  exact package's own licence file; the LGPL 2.1 and MPL 1.1 texts are referenced by address, not embedded.
  Done for v0.0 (the release package): the installer ships the three files and `LICENSE-LGPL-2.1.txt`, the FSF's LGPL 2.1 text, which
  `THIRD-PARTY-NOTICES.txt` names for the RDL Project, NHunspell and Hunspell, and the en_US word list. NHunspell and Hunspell ship under
  the LGPL 2.1 option of their tri-licence, so no MPL 1.1 text is needed (`docs/owner-questions.md`, "Decided by default for v0.0"). The
  release package did not re-check the copyright lines against the packages' own licence files.

## Owner questions

1. fo-dicom (`Dicom.Core.dll`, `Dicom.Native.dll`) is MS-PL, which is not compatible with the GNU GPL. Keep it, replace it, or drop DICOM?
2. NDde (`NDde.dll`) uses its own Shared Source License in the style of MS-PL. Is it acceptable next to the GPL?
3. The Google APIs client libraries are Apache 2.0, compatible with GPL v3 but not v2. The program is "v2 or later", so it can be distributed
   under v3; is that the intended reading, or should Gmail go?
4. `AssemblyCompany`: "Open PMS - Dental contributors" (INFERRED), or "Marysville Dental Center"?
5. The Hunspell en_US dictionary embedded in `WpfControlsOD` is the OpenOffice.org en_US dictionary (48,245 words). Its terms are taken from
   that dictionary's README (word list LGPL, affix file BSD-style); please confirm, or replace it with a current SCOWL build.
6. Showing the product name for the two Benco values too (INFERRED), not only for Open Dental's defaults: agreed?
7. DemandForce's extract still names "Open Dental" as the management system: keep it (DemandForce's format) or send the product name?
8. `RdlEngine.dll` and `RdlViewer.dll` are prebuilt 2.9.3 binaries whose source is in the repository but has not been checked against them
   (LGPL 2.1 asks for the corresponding source). Build them from source before v0.0?

## Files outside this package

- `OpenDentHL7/ServiceHL7.cs`: the version check uses `ForkVersions.CompatVersion` (three lines), because the shared assembly info changed
  OpenDentHL7.exe's product version.
- `Properties/AssemblyInfo.cs` of the 19 shipped projects: the product, company and copyright lines the shared file now sets are removed (the
  compiler refuses duplicates). No other line changed.
- `docs/release-process.md`: two lines, the release version now lives in `SharedAssemblyInfo.cs`.

## Tests

- `tests/ReleaseFlowTests/VersionSourceTests.cs`: reads the release version from `SharedAssemblyInfo.cs` and checks `OpenDental.csproj` links it
  (the only change the move required).
- New `tests/ReleaseFlowTests/ReleaseFilesTests.cs` (6 tests): the shared file's attributes; the link and the unchanged `AssemblyVersion` and
  `AssemblyFileVersion` in every shipped project; product name, release version and copyright of the built assemblies; the output folder lists
  `Hunspellx86.dll` and `WebView2Loader.dll` (byte copies of `Required dlls`, x86); their copy items have no condition; the version call
  sites.
- New `tests/AttributionTests/ReleaseIdentityTests.cs` (17 tests): the license list (GPL first and default, one entry per shipped component with
  its license, nothing removed comes back, the resources are gone, both forms use it), the notices file (the embedded copy equals the file,
  every shipped file and source listed, the GPL questions marked, nothing that must not ship), the display name, the About copyright line, the
  "Since" lookup order, Benco, README and NOTICE.
- `AttributionTests.cs` needed no change: the attribution, NOTICE's "GNU General Public License, version 2" and About's copyright lines still pass.
- Full build of `OpenDental.Local.slnf` (Debug x86, through the build lane): 0 errors.
- `run-tests.ps1 -Mode Core -Include ReleaseFlowTests`: 79 assemblies, 5,356 tests, all passed (AttributionTests 32, ReleaseFlowTests 76,
  NoHqTests 49).
- `tools/Test-NoHqHosts.ps1`: passed (6,868 files in 106 projects).
