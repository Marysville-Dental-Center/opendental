# RENAME: the internal code names are Open PMS Dental

**Reading older port notes:** the port notes and specs in this folder were written before the rename and keep the paths and names of their
time. Read them with the mapping below: `OpenDental\Forms\FormX.cs` is now `OpenPmsDental\Forms\FormX.cs`, `OpenDentBusiness\...` is
`OpenPmsDentalBusiness\...`, `OpenDental.exe` is `OpenPmsDental.exe`, the namespace `OpenDental.UI` is `OpenPmsDental.UI`, and so on. Their
text is not rewritten; it is the record of what was done.

The rename ran in three stages on branches `rn/rename-1` (the scripted rename), `rn/rename-2` (tests, tools, visible text, docs and the
leftover names) and `rn/rename-3` (verification). It is the owner's last step before the installer and the v0.0 release. The plan is the rename inventory's sections 2 to 6;
[REN-PROOF.md](REN-PROOF.md) made the code safe to rename first.

## Mapping (owner decisions)

| Before | After |
|---|---|
| `OpenDental` (exe, assembly, root namespace, folder, `OpenDental.csproj`) | `OpenPmsDental` (`OpenPmsDental.exe`, `OpenPmsDental\OpenPmsDental.csproj`). The `OpenDental` namespace becomes `OpenPmsDental` everywhere it is declared (the exe, WpfControlsOD's Frms and Bridges, the business layer's `OpenDental.UI`, CentralManager): `OpenPmsDental.UI`, `OpenPmsDental.Bridges`, ... |
| `OpenDentBusiness` | `OpenPmsDentalBusiness` |
| `OpenDentalGraph` | `OpenPmsDentalGraph` |
| `OpenDentalCloud` | `OpenPmsDentalCloud` |
| `OpenDentalServer` (project, assembly, namespace, `ServiceMain.asmx` Class=) | `OpenPmsDentalServer` |
| `OpenDentHL7` | `OpenPmsDentalHL7` |

Each new name is one word: the Middle Tier needs every root namespace to equal its assembly name (DtoProcessor, DtoObject;
`NoHqTests/RenameGuardTests` and `BSERVERTests/MiddleTierNamesTests` guard it).

Run-time names (`CodeBase/Utilities/ProgramNames.cs`, pinned by `ReleaseFlowTests/ProgramNamesTests.Values_AreTheOpenPmsDentalNames`):

| Constant | Value |
|---|---|
| `ProcessName`, `ExeFileName` | `OpenPmsDental`, `OpenPmsDental.exe` |
| `BusinessDllFileName`, `GraphDllFileName`, `CloudDllFileName` | `OpenPmsDentalBusiness.dll`, `OpenPmsDentalGraph.dll`, `OpenPmsDentalCloud.dll` |
| `LocalAppDataFolderName`, `ProgramDataFolderName` | `OpenPMS-Dental` |
| `TempFolderName` | `openpms-dental` (`UpdateFileCopierTempFolderName` stays `updatefilecopier`) |
| `EventLogSource` | `OpenPMS-Dental` |
| `HL7ServiceName`, `EventLogSourceHL7`, `EventLogSourceHL7Main` | `OpenPmsDentalHL7`, `OpenPmsDentalHL7`, `OpenPmsDentalHL7.Main` |
| `ServicePrefix` | `OpenPmsDental`. The program starts only services with this prefix (`IsProgramServiceName`, `ServicesHelper.GetServicesToStart`). `LegacyServicePrefix` `OpenDent`, Open Dental's prefix, is also recognised where installed services are looked up or listed (`HasServicePrefix`: the HL7 service finding its own name, ServiceManager's list, which shows them as Open Dental's), so an installation from before the rename is still found; such a service is never started automatically |
| `DefaultInstallFolder` | `C:\Program Files (x86)\Open PMS - Dental` |
| `LegacyLocalAppDataFolderName` | stays `OpenDental`: chart letters still scan it for unsaved work |
| `HL7ServiceFallbackName` | stays Open Dental's run-time default `OpenDentalHL7`, used only as the name the HL7 service runs under when no installed service with either prefix was installed from its folder. The HL7 setups of an adopted database saved that name (HL7Def.HL7ServiceName, the eCW HL7ServiceName property) and the service refuses to start when its name differs. Everywhere else the new name applies; the HL7 setup windows suggest `HL7ServiceName` |

The display name is "Open PMS - Dental" (`ProductIdentity.ProductName`).

## Kept as they are

- The file names `OpenDental.sln` and `OpenDental.Local.slnf` (the shared build lane, run-tests, Test-NoHqHosts and the local merge driver
  use them; the project entries inside them are renamed).
- CodeBase, DataConnectionBase, WpfControlsOD (assembly) and the `WpfControls` namespace, SparksToothChart, UpdateFileCopier,
  ServiceManager, CentralManager, DatabaseIntegrityCheck, MySQLManager, PasswordVaultWrapper, ODCrypt, ODR and `ODR.Query`, xCrudGenerator,
  Direct2dWrapper, ImagingCapture, ImagingDeviceManager, the test projects and tools, and the x-prefixed project file names.
- Every OD* type name and the `FormOpenDental` class name (translation keys and plugin hook names), every enum member (stored as strings;
  `EnumImageAnnotVendor.OpenDental`, `AccountingSoftware.OpenDental`), `PrefName` members and preference rows, the database, its tables and
  columns.
- `FreeDentalConfig.xml` and `OpenDentalServerConfig.xml`; the Middle Tier's SOAP namespace URI, IIS path (`/OpenDentalServer`) and web
  reference (`OpenPmsDentalBusiness.OpenDentalServer.ServiceMain`); the Credential Manager prefix `OpenDental Middle Tier:`; the A to Z
  default `C:\OpenDentImages\`; the release special folders.
- The attribution, GPL and copyright headers; text about Open Dental itself (adoption, version guard, migration step names, OdStartupCheck,
  the adoption log prefix "Database adopted from Open Dental "); the frozen `ConvertDatabases*.cs` literals.
- Values sent to vendors and other systems: the X12 submitter name and ETIN and the GS02 note, EHG's UploaderName, Podium (also a stored
  property description), DemandForce, the QuickBooks Desktop app name, Oryx, Sirona, Vipersoft, Shining3D's `--source "OpenDental"`,
  CallFire's campaign name, the EHR HL7 sending application and assigning authority, the QRDA software name, the Infobutton requester and the
  Direct acknowledgement agent name.
- The pre-rename name of an HL7 service (`OpenDentHL7`) where it is looked up.

## Stage 1 (`rn/rename-1`)

- `tools/Rename-ToOpenPmsDental.ps1`: the scripted rename (re-runnable on a straggling branch made before the rename). It moves the six
  project folders and project files with git mv, renames code tokens (a small C# lexer keeps strings, comments and enum members), the XAML
  namespaces, project and solution entries, configuration sections, ResourceManager names, the CrudTable overrides, xCrudGenerator's paths
  and namespaces, and the `ProgramNames` values. The scripted commits (`01c141339`, and `897f748dc`, the same script run again for the
  tools' repository paths) are in `.git-blame-ignore-revs`.
- The legacy service prefix (`LegacyServicePrefix`, `HasServicePrefix`).

## Stage 2 (`rn/rename-2`)

- **Tests and tools:** every test, test helper and tool that read a source by its old folder, loaded an old exe or dll, built an old type
  name by string or wrote synthetic old file names now names the new ones (scripted token pass, then reviewed). The token pass is
  `tools/Rename-TokenPass.py`; it ran over the tests and tools, then the product code, then the docs (`--docs`). `ProgramNamesTests` pins
  every value and checks the old and the new literals stay out of the routed sites; `AttributionTests` asserts the renamed assembly and
  namespace. Tests that check removed things still take their names from `typeof`.
- **Visible text:** the strings that name this program say "Open PMS - Dental" (splash, designer title, logoff warning, restart prompts,
  shutdown messages, UpdateFileCopier, Choose Database and log-on, the clinic, backup, merge, QuickBooks, XCharge, EHR and capture settings
  texts, the eCW and HL7 server labels). Strings about Open Dental itself and values sent to other systems are kept. A reworded `Lan.g`
  text is a new translation key.
- **Comments and leftovers:** comments, XML docs, commented-out code, DocumentationBuilder, the HL7 service's event log messages and debug
  title, and the Diagnostics labels name the new projects. The code analysis rule set is `OpenPmsDental.ruleset`.
- **xCrudGenerator:** `CrudGenerator.exe crud=*` regenerates all 477 committed CRUD files with zero diff, so it writes the new namespaces
  itself. (It also wrote CRUD for three HQ-only table types whose CRUD ORG-CLEANUP had deleted; those files were not kept. The six
  `WebForms_*` CRUD files are skipped by the generator by design; their namespace is the renamed `NamespaceOverride`.)
- **New database:** the INIT-SQL generator sets `MainWindowTitle` and `SoftwareName` to "Open PMS - Dental" in `db/init/new-database.sql`
  only. A new database is not an adopted copy; an adopted database keeps Open Dental's values and shows the product name at display time.
- **Plug-ins:** third-party plug-ins built against Open Dental's assemblies do not load (owner decision D5). The adoption tool's
  `migration-preview` ends with one line listing the program rows that name a `PluginDllName`. PluginExample uses the new names and compares
  its version with the program's assembly version.
- **Docs:** README, NOTICE, the decoupling plan, release process, adoption runbook (its leftover-client check still looks for
  `OpenDental.exe` only in `Open Dental*` folders), the AdoptionValidation, SyntheticData, CodeBase crypto and fork migration READMEs,
  `db/README.md`, the capture design and setup guide.
- **Leftover scanner:** `tools/Test-NoOldNames.ps1 -Strict` passes. Every old name left is in its allow-list with a reason (attribution and
  GPL headers, enum members and stored values, vendor and other wire values, the Middle Tier contract, the solution file names, frozen
  ConvertDatabases, text about Open Dental itself, the pre-rename names still looked up, and docs history).

## Stage 3 (`rn/rename-3`): verification

Synthetic data and the local server (127.0.0.1:3306) only; the program's windows were never started. Every check passed, so the
rename needed no fix. One test was added.

| Check | Result |
|---|---|
| Cold builds of `OpenDental.Local.slnf` through the build lane (a fresh worktree, no `bin` or `obj`) | **Debug x86:** 0 errors, 374 warnings, the same as main. **Release x86:** 0 errors, 1,114 warnings. There is no Release baseline from main (the lane log has no Release build). 745 of the Release warnings are XML documentation warnings (CS1591, CS1570, CS1587, CS1572, CS1573, CS1658, CS1584). They come from CodeBase, OpenPmsDental and OpenPmsDentalBusiness, which write their XML documentation file only in Release. `git blame` puts each of their 712 lines in a commit from before main (two are in `FormSplash.Designer.cs`, which the compiler reports in other letter case; they are the same lines on main). So the rename added none of them. The other warnings are the Debug build's. |
| Old names in the output | No file whose name starts with `OpenDent` (no `OpenDental*.exe`, no `OpenDentBusiness*.dll`) in any `bin` or `obj` folder of either configuration. |
| Files in `OpenPmsDental\bin\Debug` and `\Release` | Both folders hold `OpenPmsDental.exe`, `OpenPmsDentalBusiness.dll`, `ODR.dll`, `ImagingCapture.dll`, `ImagingDeviceManager.dll`, `Hunspellx86.dll`, `WebView2Loader.dll` and every `ForkReleases.ListBuildIdFiles` file. |
| Full suite (`run-tests -Mode Full`, Debug) | 88 assemblies; 5,807 tests found, 5,805 passed, 0 failed. Two were skipped, as in stage 2: the M201Tests checks against Open Dental's published database documents, which run only when `M201_OD_DOCS_DIR` is set. Stage 2 had the same counts: 5,807 found, and 5,801 passed in its full run plus the 4 it fixed and re-ran. The rename guards are part of this run: NoHqTests `RenameGuardTests`, and BSERVERTests `MiddleTierNamesTests`, whose in-process Middle Tier round trips include LogInWeb. |
| `tools/Test-NoOldNames.ps1 -Strict` | Passed: 8,113 files scanned, 0 old names outside the allow-list, 3,626 allowed (3,631 with this note, which as a port note is history). |
| `tools/Test-NoHqHosts.ps1` | Passed: 7,196 files in 115 projects, 26 allowed references. |
| `tools/New-DevDatabase.ps1 -Name rn_<digits> -Force -SkipBuild -NoAtoZ` (DevDbTool and AdoptionValidation built through the lane first) | Exit code 0. The database was adopted (DataBaseVersion 99.0.0.0, fork schema level 2026110104, OdSchemaCompat 26.1.70), all 16 fork migration steps ran, and verification passed: the schema matches Open Dental 26.1.70. The database was dropped afterwards. |
| AdoptionValidation `rehearse --size medium --runs 1 --prefix synth_rn3_<digits>_` | PASS, 31 of 31 checks, in 36 s. Among them: V10 "every Open Dental release client refuses the adopted copy" (10 of 10 release-client scenarios); A1, the fork's startup guard refuses the unadopted copy; M2, the schema is exactly 26.1.70; V11, the row-by-row check; R1 and R2, the data-layer round trip through `OpenPmsDentalBusiness`; I1 to I4, idempotence. Its scratch databases were dropped. |
| Release channel (ReleaseFlowTests, Debug, after the new test) | 91 of 91 passed, among them the publish and pull tests: `PublishThenPull_RoundTripsEveryFile`, `Republish_ReplacesThePreviousRelease`, the publish and pull decisions, `PublishFilter_KeepsLocalSettingsLocal` and `BuildId_ChangesWithAnyOwnAssembly`. |

**Added:** `ReleaseManifestTests.BuildIdFiles_AreAllInTheReleaseOutput`. The suite runs from the Debug build, but releases are
published from the Release build, so the existing check (`BuildIdFiles_AreAllInTheBuildOutput`) never saw `bin\Release`. The new
test checks `OpenPmsDental\bin\Release` by name:
- every `ListBuildIdFiles` file is there;
- no file there starts with Open Dental's prefix, such as a stale `OpenDental.exe` or `OpenDentBusiness.dll`;
- the build id of a copy of those files changes when only `OpenPmsDental.exe` or only `OpenPmsDentalBusiness.dll` changes.

It is inconclusive until the Release configuration is built. From now on a full run finds 5,808 tests, and passes 5,806 of them only
where Release has been built.

Not checked here: the rename inventory's §6 items that need the running program or an office database. These are the launch and
module walk-through, CentralManager, the RDL reports, the cursors, the second instance and the Event Log sources (left to the owner's
first start after the installer), and the deferred scan of the office database.

## A branch made before the rename

A branch made before the rename (for example an unfinished feature port branch) still has the old folders and names. Bring it over with
the two scripted passes, then build and test. Both tools are on main only: run main's copies with the branch's worktree as the root. On main
both change nothing (`Rename-ToOpenPmsDental.ps1 -DryRun` reports "Nothing to change", and the token pass's keeps are what the stage 2
review kept), so on the branch they change only what still has the old names.

1. In the branch's own worktree, with a clean tree, list the files the branch changed since it left main:
   `git diff --name-only --diff-filter=AMR (git merge-base HEAD main) HEAD > $env:TEMP\branch-files.txt`.
2. **The scripted rename:** `powershell -NoProfile -ExecutionPolicy Bypass -File <main>\tools\Rename-ToOpenPmsDental.ps1 -Root .
   -ReportFile $env:TEMP\rename-report.txt`. It moves files still in an old project folder into the new one and renames the code tokens.
   Read the report, then commit the result alone ("... (scripted)").
3. **The token pass** over the branch's own files: `Get-Content $env:TEMP\branch-files.txt | python -I <main>\tools\Rename-TokenPass.py
   --docs . $env:TEMP\tokenpass.txt`. It renames the old names left in strings, comments and docs; the paths of step 1 still work after
   the folders moved. Review the diff against "Kept as they are" above (text about Open Dental itself and values sent to other systems
   keep their names, as in stage 2), commit the pass alone ("... (scripted)"), then the review's fixes. Every other file comes from main
   in the merge, which already has the reviewed result. (`--ls-files` instead of a list takes every tracked file; `--dry-run` only counts.)
4. **Visible text:** neither pass touches "Open Dental" written as two words. Strings the branch added that name this program say
   "Open PMS - Dental"; text about Open Dental itself stays.
5. **Merge main.** Lines both sides renamed the same way merge cleanly; resolve the rest.
6. **Check, build and test:** `tools\Test-NoOldNames.ps1 -Strict` (rename what it lists, or add a narrow allow-list entry with its
   reason), `tools\Test-NoHqHosts.ps1`, a build of the whole `OpenDental.Local.slnf` through the build lane (Debug x86; Release x86 too
   before a release), and the tests (`run-tests -Mode Full`, or `-Mode Targeted` with the assemblies the branch touches).
7. Optionally add the two scripted commits to `.git-blame-ignore-revs`.

## Not done here

- The installer (Inno Setup): it must register the Event Log sources `OpenPMS-Dental`, `OpenPmsDentalHL7` and `OpenPmsDentalHL7.Main`
  (writing to an unregistered source needs admin rights), create the shortcuts to `OpenPmsDental.exe`, and use the install folder
  `C:\Program Files (x86)\Open PMS - Dental`.
- There is no in-place upgrade from a pre-rename install (the copier never deletes the old `OpenDental.exe` and DLLs); dev machines
  reinstall into a clean folder. No release has been published to an office database.
- The office database checks the inventory defers (a read-only scan of the office database for stored type names, the plug-in rows) wait for
  the switch-over.
