> **Rename inventory, 2026-11-01: a snapshot.** A read-only analysis of `main` at 1db2c6d86, made before the organization cleanup, the rename and the v0.0 release. It uses the names and paths of that time (OpenDental, OpenDentBusiness and so on), and "you" is the owner it was written for. It lists everything the rename of the internal code names could break and proposes the mapping. The safety work it asked for was done by package REN-PROOF (docs/port-notes/REN-PROOF.md) and the rename itself is described in docs/port-notes/RENAME.md (the final names are OpenPmsDental, OpenPmsDentalBusiness and so on; the solution file names were kept).

# Rename inventory: internal code names to Open PMS Dental

I only read the repository: main 1db2c6d86, plus cap/cap-4 c7dd54cb8 where noted. I changed nothing. You asked to skip the office switch-over for now, so every check below uses dev or synthetic databases. Checks that need the office database are marked as deferred.

## 1. Bottom line

- **The rename is big but mostly mechanical.**
  - 6,026 of 8,631 tracked files contain an old name as a whole word, on 29,475 lines.
  - Renaming the folders moves 6,079 files.
  - The compiler catches almost all of it.
- **No database row that the program reads holds a .NET type or assembly name.** The rename itself needs no data migration (§3.6).
- **The real risk is about 45 runtime strings the compiler cannot check:**
  - Middle Tier dispatch
  - the release build id and publish exclusions
  - process, service, folder and event-log names
  - resource names and WPF pack URIs
  - the prebuilt ODR.dll
- **About 110 test assertions would still pass but stop checking anything** (§3.10).
- **Order of work:**
  1. Preconditions.
  2. Make the code safe to rename, while it still has today's names.
  3. One scripted rename commit.
  4. Visible text, docs and the installer.
  5. Verification.
- **Timing: this must land before any release is published to an office database.** There is no in-place upgrade path from a pre-rename install (§3.2).

## 2. Proposed mapping

| Today | Proposed |
|---|---|
| `OpenDental` (exe, AssemblyName, RootNamespace, folder, `OpenDental.csproj`) | `OpenPmsDental` (`OpenPmsDental.exe`, `OpenPmsDental/OpenPmsDental.csproj`). The `OpenDental` namespace becomes `OpenPmsDental` everywhere it is declared: OpenDental (1,939 files), WpfControlsOD Frms/Bridges (287), OpenDentBusiness `OpenDental.UI` (10), CentralManager (1). |
| `OpenDental.exe.config` | `OpenPmsDental.exe.config`. It follows the exe automatically. The section name `OpenDental.Properties.Settings` must change too (OpenDental/app.config:5,9; Settings.settings GeneratedClassNamespace). |
| `OpenDentBusiness` | `OpenPmsDentalBusiness` (dll, namespace, folder, csproj) |
| `OpenDentalGraph` | `OpenPmsDentalGraph` |
| `OpenDentalCloud` | `OpenPmsDentalCloud`, or delete it in the organization review if nothing uses it any more |
| `OpenDentalServer` | `OpenPmsDentalServer` (assembly, namespace, project, `ServiceMain.asmx:1 Class=`). Keep the SOAP namespace URI and the `OpenDentalServerConfig.xml` file name. |
| `OpenDentHL7` | `OpenPmsDentalHL7`, default service name the same (today Program.cs:25 says "OpenDentalHL7" and ProjectInstaller.cs:19 says "OpenDentHL7") |
| `OpenDentalWpf` | Delete it. Nothing references it and it is in neither the sln nor the slnf. |
| `OpenDental.sln`, `OpenDental.Local.slnf` | `OpenPmsDental.sln`, `OpenPmsDental.Local.slnf` (your choice; 36 test/tool files locate the repo root by these names) |
| File-system and log token | `OpenPMS-Dental`: %LOCALAPPDATA%, %ProgramData% and %TEMP% subfolders, and the Event Log source. This is already `ProductIdentity.ProductToken` (CodeBase/Utilities/VendorDirect/ProductIdentity.cs:13), and cap-4 already uses %ProgramData%\OpenPMS-Dental\Imaging (ImagingDeviceManager/Config/LocalCaptureConfig.cs:15-32). |
| Windows service prefix | `OpenPmsDental` |
| Display name | `Open PMS - Dental`. Already in ProductIdentity.cs:11, ReleaseManifest.cs:28 and FormAbout.cs:71. Used for title, splash, installer AppName and Start menu. Install folder `C:\Program Files (x86)\Open PMS - Dental`, as adoption-runbook.md:668 already says. |
| Installer | `OpenPmsDental-<release>-Setup.exe` (Inno Setup). There is no installer in the repo yet (decoupling-plan.md:103,110). `packaging/` is an old Debian/Mono package, and Makefile and mkfolders.cmd are leftovers too; delete them. |
| Registry | Nothing product-named to rename. The code reads or writes no `Software\Open Dental` key. The keys it does touch are below this table. The only new registry work is registering the Event Log source, which belongs in the installer. |
| Mutexes and pipes | None exist. There is no named Mutex, pipe server, RegisterWindowMessage or WM_COPYDATA; the only pipe is a vendor's client (WpfControlsOD/Bridges/DentalTek.cs:49). |
| Config | Keep `FreeDentalConfig.xml` and `OpenDentalServerConfig.xml` (see decision D3) |
| Keep as they are | CodeBase, DataConnectionBase, WpfControlsOD (assembly) / WpfControls (namespace), SparksToothChart, UpdateFileCopier, ServiceManager, CentralManager, DatabaseIntegrityCheck, MySQLManager, PasswordVaultWrapper, ODCrypt, ODR, xCrudGenerator, Direct2dWrapper, ImagingCapture and ImagingDeviceManager (cap-4), test projects, tools, the x-prefixed project files, every OD* type name, and the FormOpenDental class |

Registry keys the code touches today, none of them product-named:
- Control Panel\Desktop (ProgramEntry.cs:82).
- The IE emulation value, keyed by the process name. It is re-created when the program runs (MiscUtils.cs:442-450).
- Service keys under HKLM\System\CurrentControlSet\Services\<name>.
- MIME type lookups.

**Why `OpenPmsDental` as one word:**
1. **The Middle Tier needs the root namespace to equal the assembly name, as a single word.**
   - DtoProcessor.cs:327-340 takes the first dotted segment of the method name as the assembly name and calls `Type.GetType(ns+"."+class+","+asm)`.
   - DtoObject.cs:268-300 resolves parameter types with `Type.GetType(name+","+SubstringBefore(name,"."))`.
   - A dotted root (`OpenPms.Dental.Business`), or a namespace that differs from its assembly, breaks every Middle Tier call.
2. **C# identifiers cannot contain a hyphen.** .NET naming guidelines write acronyms of three or more letters as `Pms`.
3. **One prefix for every branded piece.** It is the identifier form of the existing ProductToken.
4. **Short neutral names (`Pms.exe`, `Business.dll`) cost just as much** but lose the product link and risk name collisions.

**Why keep the neutral names and the "OD" abbreviations:**
- They do not spell out the trademark.
- ODR is named inside RDL report definitions (`<CodeModule>ODR.dll</CodeModule>` and class `ODR.Query` in OpenDental/Resources/Unsched Treat Plans.rdl:3-9, ReportAccountingBalanceSheet.txt:895-897, ReportAccountingGenLedger.txt:753-755) and possibly in the office's own custom reports.
- Class names are translation keys. Lans.cs:97-99 and Lan.cs:26 use `GetType().Name` as `language.ClassType`. They are also plugin hook names (227 `Plugins.Hook*` call sites, 16 starting with "FormOpenDental.").
- The `WpfControls` namespace is compared as a string at run time (FrmODBase.cs:553, FilterControlsAndAction.cs:94). Renaming it would silently turn off focus and keyboard handling.
- SparksToothChart is named after Jordan Sparks.

## 3. What the rename can break

### 3.1 Middle Tier (OpenDentalServer)

- **The naming rule above.**
- **Hard-coded names:**
  - DataTransferObject.cs:52 `Type.GetType("OpenDentBusiness."+strNodeName)`.
  - Security.cs:432 `dto.MethodName="OpenDentBusiness.Security.LogInWeb"`. If missed, Middle Tier login fails.
  - DtoProcessor.cs:368-372 prefixes "OpenDentBusiness." for old two-part names.
- **These follow the rename automatically:** Meth.cs:61,90 (built from `DeclaringType.Namespace`) and RemotingClient.cs:337 (`nameof`).
- **JSON DTOs use TypeNameHandling.Auto (DtoObject.cs:180).** They are never stored, so they are safe as long as client and server run the same build. FormOpenDental.cs:406-416 already requires the exe and the business dll to have the same AssemblyVersion.
- **Keep the wire contract:** the SOAP namespace and action `http://www.open-dent.com/OpenDentalServer` (ServiceMain.asmx.cs:13; Web References/OpenDentalServer/Reference.cs:29,76; allowlisted in tools/Test-NoHqHosts.ps1:57).
- **Keep the deployment path.** The IIS folder name appears in:
  - the default URL (OpenDentBusiness/Properties/Settings.settings:6, `http://localhost/OpenDentalServer/ServiceMain.asmx`);
  - each workstation's `WebServiceUri` in FreeDentalConfig.xml;
  - `centralconnection.ServiceURI`;
  - Windows Credential Manager entries keyed `"OpenDental Middle Tier:"+uri` (PasswordVaultWrapper/WindowsPasswordVaultWrapper.cs:12-48).

  Renaming the IIS folder or the vault prefix loses saved logins.
- **Whether the office runs a Middle Tier is still an open question** (docs/port-notes/B-SERVER.md:125).

### 3.2 Release channel and self-update (release binaries stored in MariaDB)

- **The build id can silently stop covering the program.**
  - `ForkReleases.ListBuildIdFiles` (ForkReleases.cs:80-92) lists the files by name: OpenDental.exe, OpenDentBusiness.dll, WpfControlsOD.dll, OpenDentalGraph.dll, OpenDentalCloud.dll, and so on.
  - `ComputeBuildId` skips missing files without a word (ForkReleases.cs:99-104).
  - If the list is not updated, the id no longer covers the exe or the business dll. A rebuilt program keeps the same id, and workstations never pull it.
- **Publish exclusions are by file name.** `ShouldPublishFile` (ForkReleases.cs:277-287) excludes FreeDentalConfig.xml, OpenDentalServerConfig.xml, OpenDentalReplicationServiceConfig.xml and others. Renaming a config file without updating this list would publish the update server's database connection settings to every workstation.
- **UpdateFileCopier works by exe name:**
  - it kills processes named "OpenDental" (UpdateFileCopier/FormMain.cs:83);
  - it relaunches `OpenDental.exe` (FormMain.cs:428);
  - it defaults to C:\Program Files\Open Dental (Program.cs:26,29);
  - its temp folders are %TEMP%\opendental\updatefilecopier (FormMain.cs:45,51).
- **Close-time cleanup keys on substrings.** On close the program empties its temp folder but keeps paths containing "updatefilecopier" or "UpdateFiles" (FormOpenDental.cs:7096-7106). Keep that lowercase folder segment.
- **The pull path** is `PrefL.PullRelease` and `OpenFileCopier` (PrefL.cs:331-364, 541-598). It prefers the copier from the download (PrefL.cs:363).
- **These follow the project:** the build-time stamp (ForkReleases.cs:124-133; OpenDental.csproj ForkWriteBuildStamp at 9554-9575 and CopyUpdateFileCopier at 9577-9579).
- **There is no in-place upgrade from a pre-rename install.**
  1. The copier copies files but never deletes any, so the old OpenDental.exe and old DLLs stay in the folder.
  2. Shortcuts still start the old exe.
  3. Its build id, computed over the old file names, never matches the published one, so it pulls again on every start.

  So the rename must come before the first release is published to any office database. The owner's plan already cuts v0.0 after the rename, and there is no fork install in the office. Dev machines should reinstall into a clean folder. If an upgrade path is ever needed, add a "removed files" list to the manifest and have the copier delete them, or ship a stub OpenDental.exe.
- **Keep unchanged:** the manifest product name "Open PMS - Dental" (ReleaseManifest.cs:28), and the special folders "Sparks3D" and "OpenDentalReplicationService" (ForkReleases.cs:74-77; UpdateFileCopier FormMain.cs:37-40).

### 3.3 Process, service, folder and log names (runtime strings)

**Process names**
- ProgramEntry.cs:43 detects a second instance with `ProcessName.StartsWith("OpenDental")`. The flag is never read (FormOpenDental.cs:67, ProgramEntry.cs:119). With `OpenPmsDentalHL7`, a prefix check would also match the HL7 service, so use an exact match.
- FormOpenDental.cs:6929: the shutdown message compares the process name with "OpenDental".
- CentralManager starts the program with `Process.Start("OpenDental.exe",args)` (CentralManager/Db/CentralConnectionHelper.cs:51-56).
- **SFTP stash cleanup can delete live data.** It treats a stash as in use only if its process name starts with "OpenDental" (OpenDentalCloud/Storage/Sftp.cs:148). After the rename it would delete the stashes of running instances under %ProgramData%\OpenDental\Stash (Sftp.cs:61).

**Folders**
- %LOCALAPPDATA%\OpenDental\ holds:
  - WindowSizes.txt (LocalWindowSizes.cs:21);
  - QueryMonitorFilterGroups.txt (QueryFilters.cs:294);
  - WaitingRoomWindow.txt (FormWaitingRoom.cs:157-161);
  - ChartLetters, the working copies of letters with unsaved edits (ChartLetterL.cs:34-41). Its own comment says the old folder must still be scanned after a rename.
- <drive>\ProgramData\OpenDental\Logs, for web apps only (Logger.cs:177).
- %TEMP%\opendental is used by `PrefC.GetTempFolderPath` (PrefC.cs:458-489, 45 call sites), ImageStore.cs:226, EmailAttaches.cs:152 and UpdateFileCopier (FormMain.cs:45,51).

**Hazard: renaming the temp folder can empty the whole %TEMP% folder on dev and test machines.**
- If the temp folder does not exist and the `TempFolderDateFirstCleaned` preference is less than a month old, `GetTempFolderPath` deletes every non-.exe, non-.cs file in the user's entire %TEMP% (PrefC.cs:465-486).
- Startup triggers it within 7 days of that date (FormOpenDental.cs:1319-1320).
- A freshly converted database sets the date to CURDATE() (ConvertDatabases3.cs:7186).
- So a new folder name would wipe %TEMP% on every dev or test machine within a week of running New-DevDatabase. Remove this one-time cleanup first.

**Event Log**
- Sources "OpenDental" (NTPv4.cs:35,54; WindowsTime.cs:40) and "OpenDentHL7" / "OpenDentHL7.Main" (EcwOld.cs:93,99; OpenDentHL7/Program.cs:23,52).
- These calls are unguarded. Writing to an unregistered source tries to create it, which needs admin rights and otherwise throws. The installer must register the new source.

**Windows services**
- The "OpenDent" prefix is used in four places, which must change together:
  - `ServicesHelper.GetAllOpenDentServices` (ServicesHelper.cs:246-249);
  - ServiceManager (FormMain.cs:42), whose FormServiceManage.cs:93-94 rejects any other name;
  - the HL7 service finding its own name (OpenDentHL7/Program.cs:25-47);
  - ProjectInstaller.cs:19.
- At startup the program starts every installed "OpenDent*" service except eConnector and CustListener (FormOpenDentalThreads.cs:597-615). That includes services Open Dental left behind. A new prefix stops this, which is an improvement.
- Keep Open Dental's service exe names as identifiers of its leftovers (FormServiceManage.cs:101-104; PrefL.cs:473-515).

### 3.4 Resources, XAML and configuration

- **ResourceManager names are hard-coded in generated files:** OpenDental/Properties/Resources.Designer.cs:42 ("OpenDental.Properties.Resources"), and the same line in OpenDentBusiness and OpenDentalGraph. A mismatch with RootNamespace throws MissingManifestResourceException the first time a resource is used.
- **Cursors are looked up by type namespace.** `new Cursor(GetType(),"EraseCircle.cur")` (FormSheetFillEdit.cs:542; ControlImageDisplay.cs:151-166) looks for `<namespace>.<file>`. The cursors are embedded as RootNamespace plus file name (OpenDental.csproj EmbeddedResource entries). So RootNamespace must equal the code namespace.
- **Form .resx files are fine.** Their resource names follow the class namespace, and no .resx stores an assembly-qualified custom type.
- **WPF pack URIs name the assembly:** Button.xaml.cs:102 and PictureBox.xaml.cs:66 ("/WPFControlsOD;component/Resources/"), TabControl.cs:34 ("WpfControlsOD;component/Themes/TabControlDict.xaml"). These matter only if WpfControlsOD is renamed.
- **XAML (203 files):**
  - x:Class="OpenDental.*" (155) and clr-namespace:OpenDental (160);
  - `clr-namespace:OpenDental.UI;assembly=OpenDentBusiness` in FrmChartLetterProperties.xaml:7, FrmCommItem.xaml:8, FrmDocSign.xaml:7, FrmTestAllControls.xaml:8 and SignatureBoxWrapper.xaml:7;
  - the same text built as strings in WpfConverter.cs:113-122.

  The XAML compiler catches the files; nothing catches the WpfConverter strings.
- **app.config section names must equal the settings class name** (OpenDental/app.config:5,9; OpenDentBusiness/app.config:5). If they differ, settings silently fall back to their defaults.
- **Assembly attributes:**
  - AssemblyTitle and AssemblyProduct "OpenDental…" in the AssemblyInfo.cs:8,12 files of OpenDental, OpenDentBusiness, OpenDentHL7, OpenDentalCloud, OpenDentalGraph and OpenDentalServer.
  - `Application.ProductName` is used as a caption (FormEligibilityResponseDisplay.cs:560).
  - SparksToothChart sets AssemblyCompany "Open Dental Software" (AssemblyInfo.cs:25).
  - **AssemblyVersion must stay 24.3.63.0** in the exe and the business dll, or startup exits (FormOpenDental.cs:406-416).
- **InternalsVisibleTo names only test assemblies** (CodeBase AssemblyInfo.cs:34, ODCrypt :22, OpenDentBusiness :26, tools), so it is unaffected.
- **Directory.Build.targets:19-33** swaps a missing ProjectReference for a DLL named after the project file minus a leading x. Renaming a project file changes the DLL name it expects.

### 3.5 Reflection by name

- **The fork migration runner is safe.**
  - It finds its steps with IsAssignableFrom over the assembly (ForkMigrationHandOff.cs:56-73; ForkMigrationRunner.cs:27-50).
  - Applied steps are stored by numeric level (ForkMigrationsApplied, release-process.md:38).
  - FullName appears only in error text.
- **Plugins stop loading, silently.**
  - They are loaded with `Assembly.LoadFile` plus a type named `<dll>.Plugin` deriving from PluginBase (Plugins.cs:110-118; PluginLoader.cs:72-76), and errors are swallowed.
  - Every third-party plugin compiled against OpenDentBusiness or OpenDental will stop loading. Type forwarding cannot help, because the namespaces change too.
  - Hook names are strings; keep them. ControlApptPanel.cs:3615 even embeds "OpenDentBusiness.UI…".
  - PluginExample/ must be updated.
- **DexisIntegrator.cs:115** compares `FullName=="OpenDental.FormOpenDental"`. It falls back to OpenForms[0], so it degrades rather than crashes.
- **AdoptionValidation** filters `x.Namespace=="OpenDentBusiness"` (DataLayerRoundTrip.cs:89) and builds `"OpenDentBusiness.Crud."+…` (:133; DataLayerRoundTripTests.cs:53). After the rename every table shows as unmapped, which fails loudly.
- **The prebuilt `Required dlls/ODR.dll` references OpenDentBusiness, CodeBase and DataConnectionBase by name** (the assembly-reference strings are in the binary). After the rename, the RDL reports that load it fail: Unsched Treat Plans, accounting balance sheet, general ledger. Build ODR from ODR/xODR.csproj first; this is already planned (third-party-binaries-audit.md:331). The prebuilt PasswordVaultWrapper.dll also names "OpenDental" but is unused (audit :332).
- **xCrudGenerator has hard-coded paths and namespaces:**
  - Form1.cs:13-14, 105, 158-164; CrudGenDataInterface.cs:125-128, 334, 392; CrudGenHelper.cs:690-694; PatientDataGenerator.cs:152; CrudSchemaForUnitTest.cs:19.
  - `FieldType.ToString()` writes fully qualified names into generated code (Form1.cs:1928).
  - Six `[CrudTable(CrudLocationOverride=@"..\..\..\OpenDentBusiness\WebTypes\WebForms\Crud", NamespaceOverride="OpenDentBusiness.WebTypes.WebForms.Crud")]` attributes (WebForms_Log.cs:5, WebForms_Preference.cs:9, WebForms_Sheet.cs:8, WebForms_SheetDef.cs:10, WebForms_SheetField.cs:9, WebForms_SheetFieldDef.cs:10).
  - Check after the rename: regenerating every CRUD file must give zero diff.
- **Already safe:** FormLauncherHelper.cs:21-75 (uses typeof), ODR/FormParameterInput.cs:242,370 ("ODR.", which is kept).

### 3.6 Data in the database

**The program reads no stored type names.** I searched every serializer that could embed them:
- TypeNameHandling appears only in:
  - DtoObject.cs:180, which is never stored;
  - TsiTransLog.cs:94,104-106, which serializes with the declared type List<TsiTrans>. TsiTrans has only primitive fields (TsiTransLog.cs:230-238), so no `$type` is written.
- DataContractSerializer and BinaryFormatter are not used for storage. The [DataContract] classes in PayConnect2 go through Newtonsoft JSON.
- The ODGraph JSON uses no type handling (ODGraph.cs:79,159-176).
- Translation keys are short type names, not full names.

**One legacy preference holds a type name, and nothing reads it.** `ImageStore='OpenDental.Imaging.FileStore'` (ConvertDatabases1.cs:6557,6664; TestCanada dump). Its only use is commented out (FormBackup.cs:37).

**Enum member names are stored as strings** in 33 table types (EnumAsString) and in program.ProgName. Never rename enum members, including:
- EnumImageAnnotVendor.OpenDental (ImageDraw.cs:243)
- AccountingSoftware.OpenDental (Pref.cs:2919)
- AlertType.OpenDentalServiceDown (AlertItem.cs:142)
- LogSources.OpenDentalService (SecurityLog.cs:90)
- the EnumUserodType members (Userod.cs:148-160)

The rename script must skip `<identifier>.OpenDental`; there are 7 such sites.

**What an adopted 25.3.48 database already holds that names Open Dental:**
- `MainWindowTitle` = 'Open Dental' (seed UnitTests/Resources/dump.txt:24097). The title bar comes from it (PatientL.cs:105,154), whatever the code says.
- `SoftwareName` = 'Open Dental Software' (dump.txt:24434; ConvertDatabases3.cs:4219). It appears in the About copyright line (FormAbout.cs:24,37) and in restart prompts (FormOpenDental.cs:3415-3437; FormUpdateInProgress.cs:45).
- Preference names OpenDentalVendor, OpenDentalServiceComputerName, OpenDentalServiceHeartbeat and OpenDentalHelpCaptureFormName (Pref.cs:1617-1622).
- Shining3D's program.CommandLine `--source "OpenDental"` (ConvertDatabases8.cs:400-408), a value the vendor expects.
- DocPath C:\OpenDentImages\.
- Middle Tier URIs.
- program.PluginDllName rows.
- language and languageforeign rows keyed by class names.
- displayreport rows with OD* internal names.

Changing any of these rows would trip AdoptionRowCheck ("no other row may change", AdoptionRowCheck.cs:19-22). So show the product name at display time rather than migrating data.

**What Open Dental leaves on workstations:**
- FreeDentalConfig.xml in its folder (adoption-runbook.md:304)
- Credential Manager entries "OpenDental Middle Tier:…"
- services named OpenDent*
- the Event Log source "OpenDental"
- the IE emulation value "OpenDental.exe"

### 3.7 Visible text and the About box

- **Text that names this program:**
  - FormOpenDental.Designer.cs:192 "Open Dental" (replaced at run time by MainWindowTitle)
  - FormSplash.Designer.cs:51 "Welcome to Open Dental"
  - FrmLogoffWarning.xaml:12
  - ProgramEntry.cs:95
  - FormOpenDental.cs:6927-6933
  - UpdateFileCopier FormMain.cs:106,407,409
  - FormChooseDatabase.Designer.cs:176
  - FormLogOn.cs:153
  - Clinics.cs:465,482
  - FormBackup.cs:653,714
  - FormEClinicalWorks.cs:133,206
  - FormLetterMerges.cs:264,466,547
  - FormUpdateSetup.cs:52
- **In all, 174 product-code lines carry an "Open Dental" string**, not counting comments, tests, tools or the frozen ConvertDatabases files. About half name this program and need rewording. The rest correctly name Open Dental itself and must stay.
- **Translations:** changing an English Lan.g string orphans its translations, because translation rows are keyed by the English text (translation is skipped for en-US, Lan.cs:200).
- **About box:**
  - The product name and attribution come from constants (FormAbout.cs:27-28,71-74).
  - The copyright line is built from SoftwareName (FormAbout.cs:37). If SoftwareName changed, the line would credit Jordan Sparks's copyright to the new product. Hard-code Open Dental's copyright line first.
  - AttributionTests.cs:176-183 asserts the assembly and namespace are still "OpenDental". Flip it in the rename commit.
- **FrmBencoSetup.xaml.cs:47-64** switches MainWindowTitle and SoftwareName between the Open Dental and Benco values. This is reseller branding.

### 3.8 Programs that launch OpenDental.exe, and callbacks

- **Nothing to break here:** no file associations, URI schemes, DDE server or COM server. DDE is used only as a client (DentX.cs:85, DexisIntegrator.cs:120, Vipersoft.cs:97).
- **Programs that start OpenDental.exe:**
  - CentralManager (CentralConnectionHelper.cs:51)
  - UpdateFileCopier (FormMain.cs:428)
  - eClinicalWorks, which launches the program from inside eCW (ControlChart.cs:4934; arguments EcwConfigPath and WebServiceIsEcw at FormOpenDental.cs:7259,7288)
  - any vendor or shortcut that uses the command line (PatNum=, ChartNumber=, SSN=, UserName=, module=, show=…, FormOpenDental.cs:7244-7290)
- **Keep every command-line argument name.** Outside configurations that hold the old path must be updated; the installer creates new shortcuts.

### 3.9 Adoption tools

- **The lockout of Open Dental clients does not depend on any exe or assembly name.**
  - It is the version sentinel: adoption sets DataBaseVersion and ProgramVersion to 99.0.0.0 (Adoption.cs:163-165; ForkVersions.cs:35).
  - The rehearsal runs OdStartupCheck, a model of Open Dental's own startup checks (OdStartupCheck.cs:98-170), for 25.3.48, 24.3.63, 25.4.30 and 26.1.70 clients (OdStartupCheck.cs:235-250; Rehearsal.cs:699-716).
  - So the lockout is unaffected as long as those preference names and the sentinel stay.
- **The runbook finds leftover Open Dental clients by file name:** `Get-ChildItem 'C:\Program Files*\Open Dental*' -Include OpenDental.exe,FreeDentalConfig.xml` (adoption-runbook.md:471,721).
  - After the rename, an OpenDental.exe is always Open Dental's.
  - FreeDentalConfig.xml also exists in the fork's folder, so if that name is kept the check must stay limited to "Open Dental*" folders.
- **Keep these exactly:**
  - the log prefix AdoptionRowCheck matches, "Database adopted from Open Dental " (AdoptionRowCheck.cs:55; Adoption.cs:194);
  - the migration step names "Open Dental 24.4 schema"… (OdParity25_3.cs:13-346, OdParity26_1.cs:19-167, the Seed*Step files);
  - the log lines quoted in the runbook (adoption-runbook.md:241-260).
- **DataLayerRoundTrip's namespace strings** must change (§3.5).

### 3.10 Tests and tools

**Tests that would pass without checking anything after the rename.** They look up removed types by their old full name and expect null:
- HqToolsRemovedTests.cs:98 (51 names)
- EServicesRemovedTests.cs:81 (38)
- RefDataTests.cs:42-50
- PayConnect1RemovedTests.cs:65-67
- DropboxRemovedTests.cs:29
- BridgesDllRemovedTests.cs:242 (compares a namespace string)
- MedicationListAndVitalsTests.cs:23 (checks designer text)

That is about 110 checks. Rewrite them to take the namespace from typeof(...) and assert that a known type still resolves.

**Tests that fail loudly** (fine, but they need updating):
- 114 test files (752 lines) read sources by folder, for example `TestPaths.ReadRepoFile("OpenDental",…)`.
- 36 files find the repo root by "OpenDental.sln" or the slnf (for example AttributionTests.cs:19-23).
- CdtRetiredTests.cs:105 loads "OpenDental.exe", and its FindType asserts not null (:134-137).
- BridgesDllRemovedTests.cs:165,187 read OpenDental.exe.config.
- ReleaseChannelDbTests.cs:23-24,60,133,145,152 use synthetic OpenDental.exe and OpenDentBusiness.dll files.
- UnitTests ParamCheckTests.cs:44,602,678; GpWiringSourceTests.cs:200; DropboxRemovedTests.cs:130; AttributionTests.cs:176-183.

**Tools:**
- New-DevDatabase.ps1:141,145 (writes OpenDental\bin\<cfg>\FreeDentalConfig.xml)
- FreeDentalConfig.dev.xml:5-6
- the Test-NoHqHosts.ps1 allowlist paths (:54-70) and slnf path (:220)
- New-OdReferenceDatabase.ps1:67
- outside the repo: the build lane `tools\odbuild.ps1` builds by sln project name, and your memory notes cite OpenDental\bin\Debug paths

**Stale binaries:** the bin folders keep the old OpenDental.exe and OpenDentBusiness.dll. Tests that load files by path could load those. Delete bin and obj before the post-rename test run.

## 4. Must NOT be renamed

- **Database:** table, column and index names; PrefName members and preference rows (including OpenDentalVendor, OpenDentalServiceComputerName, OpenDentalServiceHeartbeat, OpenDentalHelpCaptureFormName, MainWindowTitle, SoftwareName, DataBaseVersion, ProgramVersion, TempFolderDateFirstCleaned); the fork markers (ForkSchemaLevel, OdSchemaCompat, ForkAdoptedFrom, ForkProgramVersion, ForkMigrationsApplied, ForkMigrationInProgress); DocMiscType values (UpdateFiles and UpdateFilesSegment, ForkRelease* 100 and 101); displayreport OD* internal names; every enum member name.
- **Database names:** the default "opendental" (CentralConnections.cs:634), opendental_pms, opendental_dev.
- **The Open Dental client lockout:** sentinel 99.0.0.0 (ForkVersions.cs:35), the adoption allowlist (:39-42), CompatVersion 24.3.63.0 and AssemblyVersion 24.3.63.0 on every assembly.
- **Attribution and licences:** the attribution text (README.md:3, NOTICE:4, FormAbout.cs:73-74); GPL and copyright headers (10 files carry "Open Dental GPL license Copyright (C) 2003 Jordan Sparks"); the Jordan Sparks, MySQL, MariaDB and ADA lines in the About box; FormLicense's "OpenDental" licence entry (FormLicense.cs:36).
- **Text that is about Open Dental itself:** adoption, version-guard and migration messages; step names; AlertItem's ReservedEnumValue texts; OdStartupCheck; the SchemaVerifier headers and `OdSchema-*.sql` resource names.
- **The frozen ConvertDatabases*.cs string literals.**
- **Translation and plugin keys:** class names used as translation keys (FormOpenDental and the rest), the Lan.g key strings ("OpenDental" in PopupHelper.cs:147, "FormOpenDental"), and the 227 plugin hook names.
- **Values sent to vendors** (a separate vendor-direct decision):
  - the X12 837 submitter "OPEN DENTAL SOFTWARE" and Open Dental's ETIN (X837_5010.cs:2285-2316)
  - EHG UploaderName=OpenDental (EHG_statements.cs:463)
  - Podium (Podium.cs:190)
  - DemandForce (DemandForce.cs:85)
  - the QuickBooks Desktop app name (QuickBooks.cs:29)
  - the Oryx API path (Oryx.cs:37)
  - Sirona (Sirona.cs:63,96,126) and Vipersoft (Vipersoft.cs:62)
  - Shining3D `--source "OpenDental"`
- **Middle Tier and machine-level names:** the SOAP namespace URI, the Credential Manager prefix "OpenDental Middle Tier:", the A-to-Z default C:\OpenDentImages, the special folders, ODR and ODR.Query, and Open Dental's service exe names.

## 5. Order of operations

0. **Gates:**
   - All functional work is merged and no worktree has pending work. Today 21 local branches are unmerged, among them cap/cap-4: 290 files, about 67.5k insertions, of which 37 files mention the old names.
   - The organization review has deleted dead projects and parts: OpenDentalWpf, MobileWeb, DataClassesOld, packaging/, Makefile, mkfolders.cmd, and the out-of-repo entries in the sln.
   - ODR is built from source.
   - No release has been published to any office database.
   - You have approved the mapping and decisions D1-D7.
   - A baseline is saved: cold builds of both configurations, test counts, the rehearsal report, a release round trip.
1. **Make the code safe to rename, still on today's names.** These are normal commits that change no behaviour:
   - replace the namespace strings in Middle Tier, DexisIntegrator and AdoptionValidation with values derived from typeof;
   - make the absence tests safe to rename;
   - move every exe, process, folder, Event Log and service-prefix name into one constants class and route all of §3.2-3.3 through it, including ListBuildIdFiles and the ShouldPublishFile list;
   - remove the %TEMP% wipe;
   - make ChartLetters also scan the old folder;
   - show the product name at display time when the title and SoftwareName hold Open Dental's defaults, and decouple the About copyright line;
   - use exact process-name matches;
   - add guard tests: every ListBuildIdFiles file exists in the build output; every business-layer table, data-interface and CRUD type has a namespace that starts with the assembly name; the pack URIs resolve;
   - add a leftover-name scanner with an allowlist (like Test-NoHqHosts.ps1);
   - make the repo-root finders look for LICENSE and NOTICE.
2. **One scripted rename commit.** Check the script into tools/ so it can be run again on a straggling branch. Add the commit to .git-blame-ignore-revs.
   - It changes: assembly names, RootNamespace, namespace declarations, using directives, fully qualified references, XAML x:Class / clr-namespace / assembly=, the ResourceManager strings in designers, app.config sections, Settings namespaces, ProjectReferences, sln and slnf, xCrudGenerator paths and namespaces, the CrudTable overrides, asmx Class=, AssemblyTitle and AssemblyProduct.
   - It moves folders and project files with git mv, flips the central constants to the new names, and flips AttributionTests.
   - Product-code string literals are not touched except through an explicit list. Enum members after a dot are skipped.
   - Test string literals are rewritten too.
3. **Visible text:** the reviewed list of about 85 lines. Then docs: README:7, NOTICE:15-17, decoupling-plan.md:110, release-process.md, the AdoptionValidation README and the runbook. Add a mapping note to the historical port notes rather than rewriting their paths.
4. **The installer:** Inno Setup with AppName "Open PMS - Dental", a new AppId, install folder `Open PMS - Dental`, the attribution and GPL pages, and Event Log source registration. Never use the Open Dental folder.
5. **Verification** (§6). Then cut v0.0.

## 6. Verification

1. **Delete every bin and obj folder.** Then do cold Debug and Release x86 builds of the slnf through the build lane: 0 errors, warnings no higher than the baseline. Search the output for any OpenDental*.exe or OpenDentBusiness*.dll.
2. **Full suite** (about 1,800 tests): the number found and passed equals the baseline, and no new inconclusive results. Then the leftover scanner (zero unlisted tokens), Test-NoHqHosts.ps1, and an xCrudGenerator regenerate-all with zero diff.
3. **Dev database round trip and launch:**
   - Run `tools\New-DevDatabase.ps1 -WithSampleData -WriteConfig` into the new bin folder, start OpenPmsDental.exe and log in as Admin.
   - Check every module, the title bar, the splash, and About (name, attribution, 0.0, copyright lines).
   - Check Images module cursors, a WPF Frm with images, and the TabControl theme.
   - Run the Unsched Treat Plans and general ledger RDL reports (these load ODR).
   - Check that a chart letter edit, Query Monitor filters and the Waiting Room position are written under %LOCALAPPDATA%\OpenPMS-Dental.
   - Start two instances; send a shutdown signal; launch with `PatNum=` on the command line; launch from CentralManager; check that NTP or time-set events are logged under the new source.
4. **Release round trip on the dev database:** publish from folder A as the update server, then pull into folder B holding an older build. UpdateFileCopier must verify, copy, and relaunch OpenPmsDental.exe. The build id must change when only the exe changes, and FreeDentalConfig.xml must not be published.
5. **Middle Tier:** the UnitTests MiddleTier tests, BSERVERTests, and an in-process DtoProcessor round trip that includes LogInWeb.
6. **Adoption rehearsal on a synthetic copy of the 25.3.48 schema (od_253_schema).** Its report must match the baseline except for build ids, including "Every Open Dental release client refuses this database.", the AdoptionRowCheck results and the data-layer round trip.
7. **Deferred, because the office switch-over is skipped for now:** with owner approval, a read-only scan of the office database's text columns for "OpenDentBusiness", "OpenDental." and ", OpenDental", and a count of program rows with a PluginDllName. The runbook updates also wait.

## 7. Size

- **Files:** 6,026 of 8,631 files and 29,475 lines contain an old name; 2,183 of those lines are in docs and Markdown, 3,773 in tests and tools.
- **Whole-word tokens:**

  | Name | Tokens | Files |
  |---|---|---|
  | OpenDental | 21,142 | 3,129 |
  | OpenDentBusiness | 9,034 | 4,542 |
  | WpfControls | 791 | |
  | WpfControlsOD | 365 | 90 |
  | OpenDentHL7 | 164 | |
  | OpenDentalGraph | 163 | |
  | OpenDentalCloud | 125 | |
  | OpenDentalServer | 55 | |
  | OpenDentalWpf | 22 | |

- **Namespace declarations:** OpenDental 2,251, OpenDentBusiness 1,926, OpenDentalGraph 38, OpenDentalCloud 11, OpenDentHL7 4, OpenDentalServer 1.
- **using directives:** OpenDentBusiness 2,754, OpenDental 776.
- **Fully qualified `OpenDental.X` references in designer files:** 14,095.
- **XAML:** 203 files.
- **Projects:** 102 csproj files reference a renamed project (100 reference OpenDentBusiness, 21 reference OpenDental). Seven project files are renamed, plus the sln and slnf.
- **Folder moves:** 6,079 files (OpenDental 3,208, OpenDentBusiness 2,122, WpfControlsOD 606, OpenDentalGraph 90, OpenDentalWpf 22, OpenDentalCloud 13, OpenDentHL7 11, OpenDentalServer 7). Without WpfControlsOD and OpenDentalWpf: 5,451.
- **Hand-reviewed runtime strings:** about 45. **Visible text:** 174 lines, about 85 to change. **Tests:** 114 files and 752 lines of path references, 36 sln locators, about 140 lines of type-name strings, about 110 checks that would pass without checking anything. **Docs:** 83 files, 1,827 lines of path references.
- **Effort, one lane** (it cannot be split: it touches about 6,000 files): making the code rename-proof 1-2 days, scripted rename and compile fixes about 1 day, tests and docs about 1 day, verification about 1 day. **About 4-5 days in total.**

## 8. Your decisions

| # | Decision | My suggestion |
|---|---|---|
| D1 | The identifier mapping in §2 | OpenPmsDental* as single words; `OpenPMS-Dental` for folders and logs; `Open PMS - Dental` for display |
| D2 | Window title and SoftwareName | Show the product name at display time, rather than a data step that AdoptionRowCheck would flag |
| D3 | FreeDentalConfig.xml | Keep the name. If renamed, read the old name as a fallback and exclude both names from publishing. |
| D4 | Middle Tier IIS path and Credential Manager prefix | Keep both |
| D5 | Third-party plugins break | Accept it; add a migration-preview line listing plugin rows |
| D6 | WpfControlsOD and the OD* names | Keep them in this step (keep the WpfControls namespace in any case) |
| D7 | Benco branding toggle | Remove it under ground rule 7, or keep it in step with the new title |