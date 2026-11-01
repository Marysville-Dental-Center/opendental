# REN-PROOF: make the code safe to rename, still with today's names

Package REN-PROOF of the pre-rename run, on branch `pre/ren-proof-1` from `main`. It does step 1 of the rename plan (the rename inventory's
sections 3 and 5, actions R04 to R08, R16 and the guard tests). Nothing is renamed here: every name keeps today's value, so the program behaves
as before except where noted under "Behaviour changes". The rename itself is the next run.

## What changed

### One place for the program's run-time names (R06)

`CodeBase/Utilities/ProgramNames.cs` holds the names the program uses for itself that the compiler cannot check. Every site below reads them from
there, so the rename changes these values and nothing else. The file uses only the framework, because UpdateFileCopier compiles a linked copy of
it (it replaces CodeBase.dll while it runs; `UpdateFileCopier.csproj` links it like the other CodeBase files).

| Constant | Value today | Used by |
|---|---|---|
| `ProcessName`, `ExeFileName`, `IsProgramProcessName` | `OpenDental`, `OpenDental.exe`; whole-name match, ignoring case | ProgramEntry (second instance), FormOpenDental (shutdown message), SFTP stash liveness, UpdateFileCopier (kill and relaunch), CentralManager (`Process.Start`), the build id |
| `BusinessDllFileName`, `GraphDllFileName`, `CloudDllFileName`, `UpdateFileCopierExeFileName` | today's file names | the build id; FormOpenDental's close-time clean-up keeps the copier |
| `ConfigFileName`, `MiddleTierConfigFileName`, `ReplicationServiceConfigFileName` | `FreeDentalConfig.xml`, `OpenDentalServerConfig.xml`, `OpenDentalReplicationServiceConfig.xml` | `ForkReleases.ShouldPublishFile`; DtoProcessor reads the Middle Tier config |
| `LocalAppDataFolderName` | `OpenDental` | window sizes, Query Monitor filters, Waiting Room position, chart letter working copies |
| `LegacyLocalAppDataFolderName` | `OpenDental`, and it stays that after the rename | chart letters scan it for unsaved work (`GetLocalAppDataFolders`) |
| `ProgramDataFolderName` | `OpenDental` | the SFTP download stash, web-app logs (Logger) |
| `TempFolderName`, `UpdateFileCopierTempFolderName` | `opendental`, `updatefilecopier` | `PrefC.GetTempFolderPath`, ImageStore and EmailAttaches temp files, UpdateFileCopier's backups, FormOpenDental's close-time clean-up |
| `EventLogSource` | `OpenDental` | NTPv4, WindowsTime |
| `HL7ServiceName`, `EventLogSourceHL7`, `EventLogSourceHL7Main` | `OpenDentHL7`, `OpenDentHL7`, `OpenDentHL7.Main` | the HL7 installer and service, every HL7 Event Log write (OpenDentBusiness/HL7, HL7old, OpenDentHL7) |
| `ServicePrefix` | `OpenDent` | `ServicesHelper.GetAllOpenDentServices`, ServiceManager (add and name check), the HL7 service finding its own name |
| `DefaultInstallFolder` | `C:\Program Files\Open Dental` | UpdateFileCopier when started without a destination (debugging only) |

### Names taken from types (R04)

- Middle Tier: `Security.LogInWebMethodName` builds `"<namespace>.Security.LogInWeb"` from `typeof(Security)`; DtoProcessor compares the class
  and method with `nameof`; the legacy two-part name gets `typeof(DtoProcessor).Namespace` in front; `DataTransferObject.Deserialize` finds the
  DTO type in `typeof(DataTransferObject).Namespace`.
- DexisIntegrator finds the main window as `typeof(FrmODBase).Namespace+".FormOpenDental"` (FormOpenDental is in the exe, which WpfControlsOD
  cannot reference; both share the namespace).
- AdoptionValidation: `DataLayerRoundTrip.TableNamespace`, `CrudNamespace` and `GetCrudType` come from `TableBase` and `PatientCrud`; the
  round trip and its test use them.

### Release channel (R07)

- `ForkReleases.ListBuildIdFiles` uses the constants and now also lists `ImagingDeviceManager.dll` and `ImagingCapture.dll` (REL-20).
- `ShouldPublishFile` excludes the three configuration files through the constants.
- `ListSpecialDirs` (the "Sparks3D" line) is untouched; package BIN-A owns it.

### The %TEMP% wipe is gone (R08)

`PrefC.GetTempFolderPath` used to empty the user's whole %TEMP% (every file but .exe and .cs) the first time it created `%TEMP%\opendental`
within a month of `TempFolderDateFirstCleaned`. A new folder name (the rename, or a dev database, whose conversion sets the date to today) would
have set it off. It now only creates the folder. The startup call in FormOpenDental that existed only to trigger the clean-up is removed. The
`TempFolderDateFirstCleaned` preference stays in the database, unused.

### Chart letters and the SFTP stash (item 6)

- `ChartLetterL.GetWorkingRoots` lists today's working folder and the `%LOCALAPPDATA%` folder builds before the rename used (one folder today);
  `RecoverOrphanedEdits` scans each. New edit sessions still use `GetWorkingRoot` (today's folder).
- The SFTP stash counts as in use only when its process is this program by its whole name (`ProgramNames.ProcessName`, or the current process's
  own name), never by the prefix "OpenDental".

### Repository-root locators (item 7)

The test helpers find the repository by `LICENSE` plus `NOTICE`, not by `OpenDental.sln`: NoHqTests (one helper, `RemovedAssembliesTests.GetRepositoryRoot`),
and the existing helper of each test project (AttributionTests, BPERIOTests, BSERVERTests, BSHELLTests, BTASKTests, BTIMETests, ImagingDeviceManagerTests,
M202Tests, M2ADJTests, M2APPTTests, M2AUDITTests, M2CANTests, M2CLAIMFORMTests, M2EMPTests, M2MISCTests, M2SHELLTests, M2TERMTests, M2UDSTests,
RegistryTests, ReleaseFlowTests, ReportTests, SignatureTests, TaskCategoryTests, VBRIDGE1Tests, VBRIDGE2Tests, VendorDirectTests, VGPTests). The
checks that read the solution and solution filter (CdtRetiredTests, HqToolsRemovedTests) find them by extension. No tool located the root by the
solution name.

### Tests that keep checking after the rename (R05)

- HqToolsRemovedTests (33 removed and 20 kept names) and EServicesRemovedTests (25 and 14): the names are relative to the business layer's
  namespace and go through `NoHqTests/TypeLookup.cs`, which first asserts that `Patient` resolves the same way.
- RefDataTests: the exe's window names use the entry point's namespace (`TypeLookup.GetEntryNamespace`), WpfControlsOD's use FrmODBase's.
- PayConnect1RemovedTests: namespaces from `transType` and `Patient`, with both asserted to resolve; a missing project folder fails instead of
  being skipped.
- DropboxRemovedTests: the cloud assembly by `ProgramNames.CloudDllFileName`, its namespace from the `Sftp` type.
- BridgesDllRemovedTests: the DoseSpot check uses `typeof(Patient).Namespace` and first asserts that `.Bridges` holds types; a missing project folder
  fails instead of being skipped.
- MedicationListAndVitalsTests: looks for `UI.TabControl` under any namespace, and proves the pattern on a window that keeps its tab control.
- RegistryTests and M202Tests: the enum lookup prefers the business layer's namespace by `typeof(Patient).Namespace`.

### Guard tests (item 9)

- `NoHqTests/RenameGuardTests`: every table type, CRUD type and data-interface type (the classes whose source sends Middle Tier calls through
  `Meth.Get*` or `RemotingClient.Process*`) has the assembly's name as its root namespace, the rule DtoProcessor and DtoObject depend on; the
  WpfControlsOD pack URIs name the built assembly and everything they load (the TabControl theme, every `Resource` item, every `BitmapFileName`) is in
  its `g.resources`; `PrefC.GetTempFolderPath` leaves other files in %TEMP% alone.
- `BSERVERTests/MiddleTierNamesTests` (the UnitTests Middle Tier tests do not run through the runner): an in-process round trip, serialize then
  deserialize then the server's own `DtoProcessor.DtoInformation`, for LogInWeb, the legacy two-part name, a Meth-style name with a `Patient`
  parameter, and a class in a sub-namespace (`Eclaims.Eclaims`).
- `ReleaseFlowTests`: every build-id file is in `OpenDental\bin\<configuration>` and changes the build id; the publish filter uses the constants;
  `ProgramNamesTests` checks exact process matching, the folder list, today's values, that the routed sites have no literal left, and that the linked
  copy stays self-contained.
- `AdoptionValidation.Tests`: the table and CRUD lookups find known types.

### Leftover-name scanner (R16)

`tools/Test-NoOldNames.ps1` lists every whole-word OpenDental, OpenDentBusiness, OpenDentalGraph, OpenDentalCloud, OpenDentalServer and
OpenDentHL7 in the repository's text files that its allow-list does not cover (case-sensitive, so database names such as `opendental_dev` are not
reported). The allow-list: ConvertDatabases files, docs history (port notes, specs, the binaries audit, the M1 report), licence and attribution lines,
enum members and preference values stored in the database, vendor wire values (Sirona, Vipersoft, EHG), the Middle Tier's SOAP namespace, IIS path,
Credential Manager prefix and web reference name, translation keys, the one plugin hook name, Open Dental's host names and its leftover-file check
in the runbook, and the pre-rename folder constant. Entries allow only the matched name (`Match`) or a whole line (`Line`).

Report mode (default) prints a summary and exits 0; `-Strict` exits 1 while names remain, for the rename run to turn on. Today it reports 27,535
names on 26,935 lines in 5,856 files, with 3,227 allowed. It runs in about 10 seconds.

## Behaviour changes

- %TEMP% is never emptied (R08).
- Process-name checks match the whole name, ignoring case. The second-instance flag is never read; the shutdown message and the stash differ only for
  a process whose name merely starts with "OpenDental" (none ships).
- The build id now covers ImagingDeviceManager.dll and ImagingCapture.dll, so every existing install computes a new build id once. No release has
  been published to an office database, so nothing pulls because of it.
- One HL7 Event Log write used the source "OpenDentalHL7" (MessageParser, future-dated procedure), which nothing registers; it now uses the HL7
  service's source like every other HL7 message. INFERRED.
- The HL7 service's fallback name, used when no installed service matches its folder, is now the installer's default `OpenDentHL7` (Open Dental
  had `OpenDentalHL7` there, unlike its installer). INFERRED.

## Decisions (INFERRED)

- The constants class is `CodeBase.ProgramNames` in `CodeBase/Utilities/`, next to the other utilities; `ProductIdentity` (the vendor-facing
  product name) is left as it is.
- Only chart letters scan the pre-rename `%LOCALAPPDATA%` folder, because only they can hold unsaved work there. Window sizes, Query Monitor filters
  and the Waiting Room position start fresh after the rename.
- PrefL's three `UpdateFileCopier.exe` literals and the managers' process names (CentralManager, DatabaseIntegrityCheck, ServiceManager) stay
  literals: the rename keeps those names.
- Which docs are history for the scanner: `docs/port-notes`, `docs/specs`, `docs/third-party-binaries-audit.md` and `docs/m1-validation-report.md`.
  README, NOTICE, the runbook, release-process, decoupling-plan, the imaging design and the tool READMEs are reported, for the rename's docs step.
- The scanner reads files by extension (text formats only) and skips bin, obj, packages and tool folders; it scans comments too.

## Not done here, and why

- CodeBaseStandard (dead, not in the solution) links Logger.cs, which now uses ProgramNames, without linking ProgramNames.cs. It is not built, and
  the organization review deletes it; editing its project file would collide with that deletion.
- The rename itself, the visible text, the About box copyright line, the window title at display time and AttributionTests' expected names: the next run.
- ReleaseChannelDbTests' synthetic file names stay literals: its expected-file line also names `Sparks3D`, which package BIN-A changes.
- VPEARL1Tests still creates `%TEMP%\opendental` to dodge the old wipe; harmless now.

## Owner questions

1. The rename run will flip `ProgramNames`. Should `TempFolderName` become `OpenPMS-Dental` like the other folders, or stay lowercase
   (`openpms-dental`)? It is only ever created by the program.
2. Should window sizes, Query Monitor filters and the Waiting Room position also be read from the pre-rename `%LOCALAPPDATA%\OpenDental` folder
   after the rename, or start fresh (chosen here)?

## Files outside this package's list (kept minimal)

`OpenDentBusiness/Misc/LocalWindowSizes.cs`, `OpenDentBusiness/Data Interface/QueryFilters.cs`, `OpenDental/Forms/FormWaitingRoom.cs`,
`CodeBase/Utilities/Logger.cs`, `OpenDentBusiness/Imaging/ImageStore.cs`, `OpenDentBusiness/Data Interface/EmailAttaches.cs`,
`CodeBase/Utilities/ServicesHelper.cs`, `ServiceManager/FormMain.cs`, `ServiceManager/FormServiceManage.cs`, `OpenDentHL7/*.cs`,
`OpenDentBusiness/HL7/*.cs`, `OpenDentBusiness/HL7old/*.cs`, `OpenDentBusiness/Misc/NTPv4.cs`, `OpenDentBusiness/Misc/WindowsTime.cs` (sites
routed through the constants, one line each), FormOpenDental's startup temp call and close-time clean-up (next to the owned shutdown check),
`CodeBase/xCodeBase.csproj` and `UpdateFileCopier/UpdateFileCopier.csproj` (one Compile item each), `NoHqTests/NoHqTests.csproj` (two Compile
items), the new test files in BSERVERTests and ReleaseFlowTests, and the enum lookups in RegistryTests and M202Tests.

## Tests

- Build: full `OpenDental.Local.slnf` build through the lane, 0 errors, no warnings in the changed files.
- Targeted: NoHqTests 53/53, ReleaseFlowTests 76/76, BSERVERTests 55/55, VendorDirectTests 872 passed (1 skipped as before), BMEDTests 31/31,
  AttributionTests 15/15, VBRIDGE1Tests 37/37.
- Core with ReleaseFlowTests and AdoptionValidation.Tests: see the result below.
- `tools/Test-NoHqHosts.ps1`: passed. `tools/Test-NoOldNames.ps1`: report mode, exit 0; `-Strict` exits 1 today, as intended.
