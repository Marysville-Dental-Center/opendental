# Port note: V-BRIDGE2, Trophy Enhanced tags, Clever One, Clerri

Branch `port/v-bridge2`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run, then finished on `port/v-bridge2-t1` with the integration head `port/int-9` (8d077f202) merged in (see [Integration with port/int-9](#integration-with-portint-9)), and fixed on `port/v-bridge2-t3` with `port/int-10` (f252347b2) merged in (see [Review fix and port/int-10](#review-fix-and-portint-10)). Backlog section: [port-backlog.md, V-BRIDGE2](../specs/port-backlog.md#v-bridge2-trophy-enhanced-tags-clever-one-clerri). Feature specs: [features-25.4.md](../specs/features-25.4.md) (`v25_4-bridges-trophy-enhanced-tags`, `v25_4-bridges-cleverone`, `v25_4-family-clerri`).

No fork migration step, no schema change, no preference and no permission. The program rows are inserted at runtime with `Enabled=0` the first time Setup > Program Links opens, the way the vendor-direct links do it (`IProgramLinkSetup.EnsureProgram`, [vendor-direct README](../specs/vendor-direct/README.md)), through V-BRIDGE1's `BridgeLinkRows.EnsureProgram`. No migration level is used, so the adoption tools (migration-preview, AdoptionRowCheck, the allowed differences) have nothing new to expect: an adopted 25.3.48 database gets these rows only when someone opens Program Links, never from the migration run.

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

- `OpenDentBusiness/VendorDirect/ProgramLinks/CleverOneProgram.cs`: `GetDefinition` and `EnsureProgram` (`BridgeLinkRows.EnsureProgram`: inserts the row, path, note, button and property only when missing; never changes what the office set), `BuildLinkageXml`, `GetLinkageFilePath`, `UsesChartNumber(Program)` (the saved property through `BridgePatientTokens.IsChartNumberSelected`). The row's Notes text says that the hand-off is the EzDent-i one without the SSN, and to ask VATECH if Clever One does not open the patient.
- `WpfControlsOD/Bridges/CleverOne.cs`: deletes an old `linkage.xml` next to the program, writes the patient's, then starts Clever One. Without a patient it starts Clever One without one. If the file can't be written (for example a read-only Program Files folder) the user is told and Clever One starts without the patient.
- `OpenDental/Logic/VendorDirect/CleverOneProgramLinkSetup.cs`: registers the link with Program Links; a `StandardProgramLinkSetup` (V-BRIDGE1), so it opens in `FormProgramLinkEdit`.
- `Programs.ScrubExportedPatientData` deletes Clever One's `linkage.xml` on exit, as for EzDenti and Ez3Di.

### Clerri

- `OpenDentBusiness/VendorDirect/ProgramLinks/ClerriProgram.cs`: `GetDefinition` and `EnsureProgram` (the row and button through `BridgeLinkRows.EnsureProgram`, the two property rows through `VendorCredentials` because they need the credential flags), the Family module button (`IsToolbarButtonShown`, `SetToolbarButtonShown`, `CanChangeToolbarButton`, `ShouldSaveToolbarButton`, `SaveToolbarButtonAndEnabled`), `ValidateDashboardAddress`, `BuildDashboardUrl`, `GetDashboardUrl`, `ReadClientKey` (the clinic's key, else the default), `HasAnyClientKey`.
- `OpenDentBusiness/VendorDirect/ProgramLinks/ProgramLinkLaunchLog.cs`: one line per launch in the workstation's log folder (`ProgramLinks`): program, user, clinic and PatNum, no names.
- `WpfControlsOD/Bridges/Clerri.cs`: builds the address for the current clinic and patient, logs the launch, opens it in the default browser.
- `OpenDental/Forms/FormClerriSetup.cs` (+ designer, resx) and `ClerriProgramLinkSetup` in the same file: the setup window.
- Rules:
  - The Client Key is a credential: stored only through `VendorCredentials.WriteSecret` (osd1$, masked, high security), never shown again, editable only with Manage High Security Program Properties; a change is written to the security log. A plain-text key (typed into the generic property editor) is accepted and encrypted on the next save; an Open Dental ciphertext must be entered again.
  - One key per clinic, plus the default (ClinicNum 0) that clinics without a key, or with a cleared one, use.
  - The Dashboard Address is practice-wide. Only `[ClientKey]` (URL-encoded) and `[PatNum]` (blank without a patient) are replaced, so no other patient data can reach the address. Only an absolute `https` address with a host and no user name is opened, so the button can never start a program; the address, and the finished one with the tags filled in, must also pass `BridgeLaunch.CheckWebAddress` (V-BRIDGE1), which refuses the hosts this program never contacts (`HqHosts`).
  - Enabling needs a valid address, and a key when the address uses `[ClientKey]`.
  - Show Toolbar Button adds or removes only the Family module button, and can't be changed while the link is enabled (saved or checked), as the public page says. On save, the button is changed whenever the link was saved as disabled (`ClerriProgram.ShouldSaveToolbarButton`), even if Enabled was checked afterwards: checking Enabled locks the box but keeps the value the user picked, and `ClerriProgram.SaveToolbarButtonAndEnabled` saves that value first, while the link is still disabled, then Enabled. So what the window shows is what is saved.
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
- `OpenDentBusiness/TableTypes/Program.cs` (foundation-owned): `ProgramName.Clerri` and `ProgramName.CleverOne`, appended after V-BRIDGE1's `DTXStudioAcquire`, so no existing value changes its number (the convention on the integration branch; `VendorDirectTests.FoundationLayoutTests` and `VBRIDGE1Tests` check the earlier blocks). M2-02's note leaves the `ProgramName` values to the vendor packages.
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

- **Later packages that add `ProgramName` values** (for example V-GP; V-PAYSUITE is excluded by the owner): append after `CleverOne`. `WiringTests.ProgramName_NewValuesAreAppendedAfterVBridge1_ExistingNumbersKept` checks only that `Clerri` and `CleverOne` follow `DTXStudioAcquire`, so later members after them keep it green. A merge conflict in `Program.cs` is resolved by keeping every new member, in merge order.
- **V-PEARL2, B-SHELL** (earlier owners of `FormProgramLinkEdit.cs`): this package adds one line at the end of `FormProgramLinkEdit_Load` (after B-SHELL's discontinued-link block) and a partial-class file; keep both sides on merge.
- **M2-AUDIT, M2-HIST** (earlier owners of `Patients.cs` and `ControlFamily.cs`): no edits here, so no conflict.
- **Other packages adding `IProgramLinkSetup` classes**: `ProgramLinkSetupRegistry` finds them all by reflection; no shared registration file.
- `OpenDental.csproj`, `WpfControlsOD.csproj`, `OpenDental.sln`, `OpenDental.Local.slnf`: additive lines only.

## Integration with port/int-9

`port/int-9` (8d077f202) was merged into `port/v-bridge2-t1`. What changed here because of the packages already on it:

- **Conflict, `FormProgramLinkEdit.cs`**: B-SHELL's discontinued-link block (a discontinued link can't be enabled or get buttons) and this package's `SetCommandLineHint();` line both sat at the end of `FormProgramLinkEdit_Load`. Both kept, the block first. `BSHELLTests.DxcCreditScoreRemovedTests` and `WiringTests.FormProgramLinkEdit_HasOneHookLine` check each side.
- **`ProgramName` order**: V-BRIDGE1 appended `Overjet`, `DTXStudio` and `DTXStudioAcquire` after `PostcardMania` so existing values keep their numbers. `Clerri` and `CleverOne` were inserted alphabetically after `CleaRay` on the package branch, which renumbered every later member. They now follow `DTXStudioAcquire`, the integration branch's convention. The values are stored as text (`program.ProgName`) and no code casts them to numbers, so this changes no stored data.
- **Helpers reused from V-BRIDGE1** instead of keeping this package's copies:
  - `BridgeLinkRows.EnsureProgram` (with a `BridgeLinkDefinition`) inserts the Clever One and Clerri rows and buttons. The behaviour is the same as before (the row once, with its defaults; missing ClinicNum 0 properties; an existing row untouched). Clever One's row also gets a Notes text now, like the V-BRIDGE1 rows.
  - `BridgePatientTokens.IsChartNumberSelected` is the PatientNum/ChartNum rule for Clever One (only "1" sends the ChartNumber, as before).
  - `StandardProgramLinkSetup` opens Clever One in the standard Program Link window (it also locks the toolbars of a static program, which Clever One is not).
  - `BridgeLaunch.CheckWebAddress` checks the Clerri dashboard address. Besides the https rule this package already had, it refuses hosts this program never contacts (`HqHosts`), for the saved address and for the finished one (a tag in the host). The user-name rule stays on top of it.
- **No other overlap**: none of the merged packages touches Trophy (Enhanced), Clever One, Clerri, `ProgramL.cs` dispatch for them, or `Programs.ScrubExportedPatientData`'s Clever One lines. No package on `port/int-9` adds a program-link launch log or an audit permission for program links, so `ProgramLinkLaunchLog` stays (owner question 2).
- **Adoption**: no fork migration step is added or activated, so migration-preview, AdoptionRowCheck and `allowed-differences.adoption.json` are unchanged.

## Review fix and port/int-10

`port/int-10` (f252347b2: M2-AUDIT's birthdate audit and M2-MISC) was merged into `port/v-bridge2-t3` without conflicts. M2-AUDIT changes `Patients.cs` (owned here but not edited by this package) and nothing in `ReplacePatient`; neither package touches the program links, `ProgramL.cs`, `FormProgramLinkEdit.cs` or `ControlFamily.cs`, so no code changed because of them and no helper on the base replaces one of this package's.

Review finding fixed (spec, `FormClerriSetup.cs`): Show Toolbar Button was lost when it was changed in the same session as checking Enabled. Checking Enabled greys the box out (`CanChangeToolbarButton`) but kept the user's value, and the save applied the button change only while the box was editable, so it was skipped: a link saved disabled with its button removed could be enabled with no Family module button, and the window then showed the box unchecked and locked. The reverse (box cleared, Enabled checked) kept the button. Now the save decides from the saved state: `ClerriProgram.ShouldSaveToolbarButton(isEnabledSaved,isShownSaved,isShownChecked)` (saved as disabled and different from the saved button), and `ClerriProgram.SaveToolbarButtonAndEnabled` changes the button first, while the link is still saved as disabled, then saves Enabled; the window calls it in place of its own two blocks. A link saved as enabled still never gets a button change (its box is locked from the start). The other fix the review offered, putting the box back to its saved value when it locks, was not taken: it would also drop the user's pick, only visibly.

New tests: `ClerriProgramTests.ToolbarButton_SavedFromTheSavedStateNotFromTheLockedBox` (the rule), `ProgramLinksDbTests.Clerri_ButtonPickedWhileDisabledThenEnabledInTheSameSave_BothSaved` (the review's example against the scratch database), `Clerri_ButtonClearedWhileDisabledThenEnabledInTheSameSave_BothSaved` (the reverse) and `Clerri_ButtonNotChangedWhileSavedEnabled`; `WiringTests` checks that the window calls `SaveToolbarButtonAndEnabled` and no longer tests `checkShowToolbarButton.Enabled` on save.

## Tests

New project `tests/VBRIDGE2Tests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 64 tests:

- `TrophyEnhancedCommandLineTests`: sample command lines (blank, `-D[PatNum]`, quoted `[TrophyFolder]`, case, `$` in paths, skipped folders).
- `CleverOneProgramTests`: the linkage.xml content and layout, no SSN, PatNum or ChartNumber, regional settings, defaults, the row definition (path, button, property, note).
- `ClerriProgramTests`: address building, encoding, tag case, no other tags, https-only (file, cmd, javascript, mailto, user info rejected), blocked hosts (saved, and reached through a tag in the host), the toolbar rule, the launch log line.
- `WiringTests`: `ProgramName` order (appended after V-BRIDGE1's values, nothing renumbered), the dispatch branches, project files, the setup registrations (Clever One through `StandardProgramLinkSetup`), the encrypted key save, the one-line hook, the exit scrub, no web addresses or promotion in the new files.
- `ProgramLinksDbTests` (scratch MariaDB database `vbridge2_<time>_<pid>_<random>` with the 26.1.70 reference schema, dropped afterwards; skipped when `VBRIDGE2_TEST_DB=skip` or no local server): the inserted rows, note and buttons, a second run, office changes kept, the saved PatientNum/ChartNum choice, buttons only when enabled, Show Toolbar Button, keys encrypted per clinic with the default fallback, unreadable and plain-text keys, and the Trophy tags with the real `Patients.ReplacePatient`.

Final run on `port/v-bridge2-t3` (after the `port/int-10` merge and the Show Toolbar Button fix), through the shared runner: one full build of `OpenDental.Local.slnf` (0 errors), then `run-tests -Mode Core -Include ForkMigrationTests,M201Tests,SyntheticData.Tests,AdoptionValidation.Tests` (the slow assemblies because the package changes `TableTypes/Program.cs`): 72 assemblies, 3812 tests, 3810 passed, 0 failed, 2 skipped (M201Tests' two checks against the published 26.1 documents, which skip when the documents are not on this computer). Among them `VBRIDGE2Tests` 64/64, `VBRIDGE1Tests` 37/37, `VendorDirectTests` 873/873, `BSHELLTests` 66/66, `M2AUDITTests` 28/28, `M2MISCTests` 78/78, `RegistryTests` 33/33, `M202Tests` 39/39, `ForkMigrationTests` 38/38, `AdoptionValidation.Tests` 56/56, `SyntheticData.Tests` 53/53, `NoHqTests` 49/49. `tools/Test-NoHqHosts.ps1` passes.

Earlier run on `port/v-bridge2-t1` (after the `port/int-9` merge): 70 assemblies, 3700 passed, 0 failed, 2 skipped.

Not tested: the setup window and the tool tip on screen, starting Clever One or Trophy, opening the browser, writing `linkage.xml` into a real program folder, and the middle tier.

## Questions for the owner

1. Clerri (25.4 open question 10): is a plain launch useful without the data sync? If yes, the office needs Clerri's dashboard address (and whether it takes the key and the patient number) to fill in Dashboard Address.
2. Clerri launches are logged to a local file. Should they go to the security log instead? That needs an audit-only permission type from the registry owner (M2-02).
3. Clever One: please confirm the hand-off with VATECH (file name, folder, fields). The default program folder is under Program Files, which ordinary users often can't write to.
4. Trophy (Enhanced): build the ChartNum property and the blank-path Carestream Imaging 8 setup from the current public page?
5. The `ProgName` values `Clerri` and `CleverOne` are the fork's. They only matter if an Open Dental database from 25.4 or later is ever adopted (its rows would then sit beside ours).
