# Port note: V-BRIDGE2, Trophy Enhanced tags, Clever One, Clerri

Branch `port/v-bridge2`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, V-BRIDGE2](../specs/port-backlog.md#v-bridge2-trophy-enhanced-tags-clever-one-clerri). Feature specs: [features-25.4.md](../specs/features-25.4.md) (`v25_4-bridges-trophy-enhanced-tags`, `v25_4-bridges-cleverone`, `v25_4-family-clerri`).

No fork migration step, no schema change, no preference and no permission. The program rows are inserted at runtime with `Enabled=0` the first time Setup > Program Links opens, the way the vendor-direct links do it (`IProgramLinkSetup.EnsureProgram`, [vendor-direct README](../specs/vendor-direct/README.md)). The level range 2027005301 to 2027005399 is unused.

Sources, all public and read on 2026-10-25: the 25.4 release notes (version25_4.html), the setup pages bridgeclerri.html, clerri.html, bridgecleverone.html and bridgetrophyenhanced.html on opendental.com, and the 24.3 manual's bridgetrophyenhanced.html as the baseline. Only field names, defaults and technical values (the default program path, `-D[PatNum]`) were taken; every text in the code and in this note is written for this fork. No Open Dental binary, install or database was used, and no vendor was contacted or called.

## Feature IDs

| Feature ID | Status | Summary |
|---|---|---|
| `v25_4-bridges-trophy-enhanced-tags` | built | A blank Command line keeps the 24.3 arguments (`-P<folder> -N<last, first>`). A filled one is sent instead, with the patient tags of `Patients.ReplacePatient` and the fork's `[TrophyFolder]` tag (the full patient folder) filled in. A filled Command line with a blank Storage Path skips the folder steps (for setups that find the patient by an argument such as `-D[PatNum]`); `[TrophyFolder]` is then blank. The Command line box of the Trophy (Enhanced) link shows the tags as a tool tip. |
| `v25_4-bridges-cleverone` | built (hand-off INFERRED) | New program link `CleverOne`, "Clever One by VATECH": disabled, path `C:\Program Files\VATECH\CleverOne\Bin\CleverOne.exe`, property "Enter 0 to use PatientNum, or 1 to use ChartNum" = 0, a Chart module button "Clever One". It opens in the standard program link window, so Hide Button for Clinics, toolbars and the button image work as for any link. The button shows only after the office enables the link. The EzDenti link is unchanged. |
| `v25_4-family-clerri` | partial (partner-gated) | New program link `Clerri`: disabled, a Family module button "Clerri", and its own setup window with the fields of the public page (Clinic when clinics are on, Enabled, Show Toolbar Button, read-only Internal Name and Description, Client Key). The button opens the office's Clerri dashboard in the default browser. Clerri's dashboard address and how it takes the key and the patient are not public, so the office enters the address Clerri gives it (Dashboard Address); until Clerri documents it, that part is partner-gated. No data sync (owner decision: plain launch only; Open Dental's sync runs through its eConnector and API, which the fork removed). |

## How each link works

### Trophy (Enhanced)

- `OpenDentBusiness/VendorDirect/ProgramLinks/TrophyEnhancedCommandLine.cs`: `UsesCommandLine`, `SkipsFolders`, `GetDefaultArguments` (the 24.3 arguments, unchanged: no space after `-P` or `-N`, quotes removed from the names), `Build` (tags), `CommandLineHint`.
- `WpfControlsOD/Bridges/TrophyEnhanced.cs`: three small changes. The storage path check and the folder steps are skipped only when `SkipsFolders` is true; after the 24.3 arguments are built, a filled Command line replaces them. The plug-in hooks run as before (`SendData_end` sees the final arguments). Folder creation and numbered mode still run first, so `[TrophyFolder]` is valid.
- Tags: every tag `Patients.ReplacePatient` knows (for example `[PatNum]`, `[ChartNumber]`, `[LName]`, `[FName]`, `[Birthdate_yyyyMMdd]`), then `[TrophyFolder]` in any case. The Command line is sent as the office wrote it; only the 24.3 arguments remove quotes from names.

### Clever One by VATECH

- `OpenDentBusiness/VendorDirect/ProgramLinks/CleverOneProgram.cs`: `EnsureProgram` (inserts the row, path, button and property only when missing; never changes what the office set), `BuildLinkageXml`, `GetLinkageFilePath`, `UsesChartNumber`.
- `WpfControlsOD/Bridges/CleverOne.cs`: deletes an old `linkage.xml` next to the program, writes the patient's, then starts Clever One. Without a patient it starts Clever One without one. If the file can't be written (for example a read-only Program Files folder) the user is told and Clever One starts without the patient.
- `OpenDental/Logic/VendorDirect/CleverOneProgramLinkSetup.cs`: registers the link with Program Links and opens it in `FormProgramLinkEdit`.
- `Programs.ScrubExportedPatientData` deletes Clever One's `linkage.xml` on exit, as for EzDenti and Ez3Di.

### Clerri

- `OpenDentBusiness/VendorDirect/ProgramLinks/ClerriProgram.cs`: `EnsureProgram`, the Family module button (`IsToolbarButtonShown`, `SetToolbarButtonShown`, `CanChangeToolbarButton`), `ValidateDashboardAddress`, `BuildDashboardUrl`, `GetDashboardUrl`, `ReadClientKey` (the clinic's key, else the default), `HasAnyClientKey`.
- `OpenDentBusiness/VendorDirect/ProgramLinks/ProgramLinkLaunchLog.cs`: one line per launch in the workstation's log folder (`ProgramLinks`): program, user, clinic and PatNum, no names.
- `WpfControlsOD/Bridges/Clerri.cs`: builds the address for the current clinic and patient, logs the launch, opens it in the default browser.
- `OpenDental/Forms/FormClerriSetup.cs` (+ designer, resx) and `ClerriProgramLinkSetup` in the same file: the setup window.
- Rules:
  - The Client Key is a credential: stored only through `VendorCredentials.WriteSecret` (osd1$, masked, high security), never shown again, editable only with Manage High Security Program Properties; a change is written to the security log. A plain-text key (typed into the generic property editor) is accepted and encrypted on the next save; an Open Dental ciphertext must be entered again.
  - One key per clinic, plus the default (ClinicNum 0) that clinics without a key, or with a cleared one, use.
  - The Dashboard Address is practice-wide. Only `[ClientKey]` (URL-encoded) and `[PatNum]` (blank without a patient) are replaced, so no other patient data can reach the address. Only an absolute `https` address with a host and no user name is opened, so the button can never start a program.
  - Enabling needs a valid address, and a key when the address uses `[ClientKey]`.
  - Show Toolbar Button adds or removes only the Family module button, and can't be changed while the link is enabled (saved or checked), as the public page says.
- No eConnector status line (the eConnector is removed). No links, logos or sign-up text.

## Files

New:

- `OpenDentBusiness/VendorDirect/ProgramLinks/` `TrophyEnhancedCommandLine.cs`, `CleverOneProgram.cs`, `ClerriProgram.cs`, `ProgramLinkLaunchLog.cs` (compiled by the existing `VendorDirect\**\*.cs` wildcard, so `OpenDentBusiness.csproj` is unchanged)
- `WpfControlsOD/Bridges/` `CleverOne.cs`, `Clerri.cs`
- `OpenDental/Forms/` `FormClerriSetup.cs`, `FormClerriSetup.Designer.cs`, `FormClerriSetup.resx`, `FormProgramLinkEdit.Bridges2.cs` (the tool tip, a partial-class file)
- `OpenDental/Logic/VendorDirect/CleverOneProgramLinkSetup.cs`
- `tests/VBRIDGE2Tests/` (new test project)

Changed (each change small and local):

- `WpfControlsOD/Bridges/TrophyEnhanced.cs` (owned): see above.
- `WpfControlsOD/Logic/ProgramL.cs` (owned): two dispatch branches after CleaRay.
- `OpenDental/Forms/FormProgramLinkEdit.cs` (owned): one hook line at the end of Load.
- `OpenDentBusiness/TableTypes/Program.cs` (foundation-owned): `ProgramName.Clerri` and `ProgramName.CleverOne`, in alphabetical order after CleaRay. M2-02's note leaves the `ProgramName` values to the vendor packages. The enum is stored as text, so the numbers of later members moving is harmless; the vendor-direct members stay last as `FoundationLayoutTests` requires.
- `OpenDentBusiness/Data Interface/Programs.cs`: three lines in `ScrubExportedPatientData`.
- `WpfControlsOD/WpfControlsOD.csproj`, `OpenDental/OpenDental.csproj`: compile lines for the new files.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the test project.

Owned but not changed: `OpenDental/Main Modules/ControlFamily.cs` (the Family module already shows enabled program link buttons for `EnumToolBar.FamilyModule` and dispatches them to `ProgramL.Execute`) and `OpenDentBusiness/Data Interface/Patients.cs` (`ReplacePatient` is called as it is; `[TrophyFolder]` lives in the bridge).

## INFERRED behaviours

- The `ProgName` values `Clerri` and `CleverOne` (Open Dental's are not public; 25.4 open question 1).
- Clever One hand-off: the EzDent-i `linkage.xml` layout (spec fork default; VERIFY with VATECH), without the SSN. Birthday is written dd/MM/yyyy whatever the regional settings; gender is Female or Male (Unknown and Other are written as Male, as EzDent-i does).
- Clever One property values: only "1" means ChartNumber; anything else, blank included, sends the PatNum.
- Trophy: `[TrophyFolder]` (fork tag, from the spec); the blank-Storage-Path behaviour (from the current public page, which says a blank Storage Path needs a `-D[PatNum]` argument); tags replaced in any case.
- Clerri: the Dashboard Address field and its two tags, the https-only rule, the per-clinic key fallback to the default, the requirement for a key before enabling, the patient context (`[PatNum]`, the spec's assumption), and the local launch log (the spec's "generic program-link log (fork)").
- Clerri Show Toolbar Button is the presence of the Family module `toolbutitem` row; "can't be changed while enabled" is read as "while saved as enabled or checked".

## Deviations from the spec

- No ConvertDatabases or fork migration step inserts the Clever One rows (the spec suggests a ConvertDatabases step). The rows are inserted at runtime when Program Links opens, as for every vendor-direct link; the effect is the same (a disabled link with the published defaults), with no migration level used.
- Clerri: an extra Dashboard Address field, because the address is not public. The spec's classification check ("build the launch only after Clerri confirms") is overridden by the owner decision "plain program-link launch only"; the launch is built and stays disabled until the office enters what Clerri gives it.
- The launch log is a local log file, not the security log: a security-log entry needs a permission type, which is a registry change (M2-02's) this package does not own. See the owner questions.
- Trophy: not built from the current public page, because neither is in the 25.4 spec and the version that added them is not public: an "Enter 0 to use Patient Num or 1 to use ChartNum" property, and a blank "Path of file to open" for new Carestream Imaging 8 setups (with a blank path the link still says the program is not available). The tags already cover `[ChartNumber]`.

## Interplay with packages built in parallel

- **V-PAYSUITE, V-GP**: also add `ProgramName` values. If they append after `PostcardMania`, `VendorDirectTests.FoundationLayoutTests.ProgramName_VendorDirectNamesAreAppendedLast` and `WiringTests.ProgramName_NewValuesAreAlphabeticalAndTheVendorDirectOnesStayLast` here fail; inserting alphabetically (as here) keeps both green. A merge conflict in `Program.cs` is resolved by keeping every new member.
- **V-PEARL2, B-SHELL** (earlier owners of `FormProgramLinkEdit.cs`): this package adds one line at the end of `FormProgramLinkEdit_Load` and a partial-class file; keep both sides on merge.
- **M2-AUDIT, M2-HIST** (earlier owners of `Patients.cs` and `ControlFamily.cs`): no edits here, so no conflict.
- **Other packages adding `IProgramLinkSetup` classes**: `ProgramLinkSetupRegistry` finds them all by reflection; no shared registration file.
- `OpenDental.csproj`, `WpfControlsOD.csproj`, `OpenDental.sln`, `OpenDental.Local.slnf`: additive lines only.

## Tests

New project `tests/VBRIDGE2Tests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 58 tests:

- `TrophyEnhancedCommandLineTests`: sample command lines (blank, `-D[PatNum]`, quoted `[TrophyFolder]`, case, `$` in paths, skipped folders).
- `CleverOneProgramTests`: the linkage.xml content and layout, no SSN, PatNum or ChartNumber, regional settings, defaults.
- `ClerriProgramTests`: address building, encoding, tag case, no other tags, https-only (file, cmd, javascript, mailto, user info rejected), the toolbar rule, the launch log line.
- `WiringTests`: `ProgramName` order, the dispatch branches, project files, the setup registrations, the encrypted key save, the one-line hook, the exit scrub, no web addresses or promotion in the new files.
- `ProgramLinksDbTests` (scratch MariaDB database `vbridge2_<time>_<pid>_<random>` with the 26.1.70 reference schema, dropped afterwards; skipped when `VBRIDGE2_TEST_DB=skip` or no local server): the inserted rows and buttons, a second run, office changes kept, buttons only when enabled, Show Toolbar Button, keys encrypted per clinic with the default fallback, unreadable and plain-text keys, and the Trophy tags with the real `Patients.ReplacePatient`.

Run for this package: `VBRIDGE2Tests` 58/58, `VendorDirectTests` 873/873, `NoHqTests` 49/49. Also run because `ProgramName` (in `TableTypes/Program.cs`) changed: `RegistryTests` 33/33, `M202Tests` 39/39, `ForkMigrationTests` 38/38. `tools/Test-NoHqHosts.ps1` passes.

Not tested: the setup window and the tool tip on screen, starting Clever One or Trophy, opening the browser, writing `linkage.xml` into a real program folder, and the middle tier.

## Questions for the owner

1. Clerri (25.4 open question 10): is a plain launch useful without the data sync? If yes, the office needs Clerri's dashboard address (and whether it takes the key and the patient number) to fill in Dashboard Address.
2. Clerri launches are logged to a local file. Should they go to the security log instead? That needs an audit-only permission type from the registry owner (M2-02).
3. Clever One: please confirm the hand-off with VATECH (file name, folder, fields). The default program folder is under Program Files, which ordinary users often can't write to.
4. Trophy (Enhanced): build the ChartNum property and the blank-path Carestream Imaging 8 setup from the current public page?
5. The `ProgName` values `Clerri` and `CleverOne` are the fork's. They only matter if an Open Dental database from 25.4 or later is ever adopted (its rows would then sit beside ours).
