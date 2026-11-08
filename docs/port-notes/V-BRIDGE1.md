# V-BRIDGE1: Overjet, DTX Studio, Insurance Answers Plus

Package V-BRIDGE1 of [the port backlog](../specs/port-backlog.md) (vendor-direct, M1 Tier B, wave B1). Branch `port/v-bridge1`, cut from `m1/integration-a2-patch`. Tests: `tests/VBRIDGE1Tests`.

Sources used: the public release notes (version25_1.html, version25_2.html), the public setup pages bridgeoverjet.html, bridgedtxstudio.html, bridgedtxstudioacquire.html and iaplus.html, and the feature specs ([features-25.1.md](../specs/features-25.1.md), [features-25.2.md](../specs/features-25.2.md)). No vendor documentation, no Open Dental binaries, no office data.

## Status

| Feature ID | Status | Summary |
|---|---|---|
| `v25_1-setup-overjet-bridge` | partial (partner-gated part) | Built: program row, setup, toolbar button, opening the vendor's address with or without a patient. Partner-gated: the names of the patient parameters. |
| `v25_2-bridges-dtx-studio` | partial (partner-gated part) | Built: both program rows with the public defaults, setup, toolbar buttons, the patient and program-file checks, the Birthdate Format. Partner-gated: the `DTXsync.exe` command-line syntax. |
| `v25_2-family-iaplus-bridge-update` | built | The IAP button opens the office's IAP sign-in page. `FormIap`, the `iaplus10.dll` calls and the plan and benefit import are removed. |

"Partner-gated" means that the part needs the vendor's own integration documentation, which is not public. In both cases the office can finish the setup in Program Links once it has the vendor's guide, without a code change (see below). Until then nothing about the patient is sent.

## What was built

### Common: the program rows (all three features)

- The rows are inserted **at runtime**, disabled, when Setup > Program Links opens. Each link registers an `IProgramLinkSetup` (`OpenDental/Logic/VendorDirect/BridgeProgramLinkSetups.cs`), which `ProgramLinkSetupRegistry` finds by reflection. Double-clicking one of these links opens the standard Program Link window. `FormProgramLinks.cs` is not edited.
- `BridgeLinkRows.EnsureProgram` (`OpenDentBusiness/VendorDirect/ProgramLinks/`) inserts the `program` row through `Programs.InsertIfMissing`, which handles two workstations racing. Only when it inserted the row, it also writes the default path, note and toolbar rows. Missing ClinicNum 0 `programproperty` rows are always added. A row that already exists keeps its own settings and toolbars, for example one Open Dental created in the office database.
- Toolbar buttons show only once the office enables the link: `ToolButItems.GetForToolBar` filters on `Program.Enabled` (ground rule 7). A test checks this.
- No credentials are stored. Each vendor's own sign-in page or program handles sign-in, so CodeBase Secrets aren't needed. No HTTP calls are made.
- `ProgramName` gets `Overjet`, `DTXStudio` and `DTXStudioAcquire`, appended after `PostcardMania` so existing values keep their numbers.

### Overjet (25.1)

- Row: ProgDesc "Overjet", Path `https://clinic.overjet.ai/app/bridge_redirect` (the public default), Main toolbar, button text "Overjet".
- Button (`WpfControlsOD/Bridges/Overjet.cs` → `OverjetLink.GetLaunch`):
  - With no patient, the address opens as it is (the vendor's sign-in page).
  - With a patient, the address gets the "Patient URL parameters" property with the patient's values filled in and percent-encoded.
  - While that property is blank (the default), the plain address opens and no patient data is sent.
- The address, and the finished address with the patient's values, must be `https://` and not on a blocked host (`HqHosts` through `VendorUrlPolicy`). Plain http is refused even to localhost.
- Properties: "Enter 0 to use PatientNum, or 1 to use ChartNum" (0), and "Patient URL parameters" (blank).

### DTX Studio and DTX Studio Acquire (25.2)

- Two rows: ProgDesc "DTX Studio" and "DTX Studio Acquire".
  - Path `C:\Program Files\DTX Studio Clinic\DTXsync.exe`.
  - Imaging Module and Main toolbars.
  - Button texts "DTX Studio" and "DTX Studio Acquire".
- Properties:
  - "Birthdate Format" (`MM/dd/yyyy`, from the public pages).
  - "Enter 0 to use PatientNum, or 1 to use ChartNum" (0).
  - "Patient arguments" (blank).
- The program's "Command line" field holds the additional properties. They are added after the patient arguments.
- Button (`WpfControlsOD/Bridges/DTXStudio.cs` → `DtxStudioLink.GetLaunch`) checks, in this order:
  1. No patient: "Please select a patient first."
  2. A blank path.
  3. A missing program file: the message shows the path.
  4. Blank patient arguments: a message saying they need setting up from the vendor's integration guide. Nothing is started.
  5. An unusable Birthdate Format.

  When all of these pass, `DTXsync.exe` starts with the patient arguments filled in. Double quotes and control characters are removed from the values.
- The Acquire note says that a patient DTX Studio doesn't have yet is sent anyway, and DTX Studio decides what to do.

### Insurance Answers Plus (25.2)

- `butIapFind_Click` in `FormInsPlan.cs` now only calls `Iap.OpenLoginPage()`, which opens the IAP row's "Path of file to open" in the browser. The roughly 420-line import that used to follow is removed. No patient or plan data is sent.
- A blank address shows "Set the IAP login address in Program Links." So does a desktop path left over from 24.3, such as `C:\IAPlus\`. An address that isn't `https://`, or is on a blocked host, is refused.
- `WpfControlsOD/Bridges/Iap.cs` is rewritten as the launcher: the DllImports are gone. `OpenDental/Bridges/FormIap.*` are deleted and removed from `OpenDental.csproj`.
- Conversion rule (from the spec), run by `IapLink.EnsureProgram` when Program Links opens: if the IAP row still has a desktop path, it is disabled, the path is blanked and the note is replaced.
  - This happens once: a blank path or a web address is never touched again.
  - An office's own `http://` address is kept, but the button refuses to open it.
- The visibility of the IAP button is unchanged: it shows only when the IAP link is enabled, it is hidden when a plan is edited from the plan list, and it is disabled without InsPlanEdit. Its designer tooltip was wrong ("Edit all the similar plans at once") and now says what it does.

### Program Link window

`FormProgramLinkEdit.butSave_Click` has a five-line hook, `BridgeLinkRows.GetPathSaveProblem`. It refuses to save a non-blank path for Overjet or IAP that isn't a full `https://` address on an allowed host. Other links aren't checked.

## Files

- New: `OpenDentBusiness/VendorDirect/ProgramLinks/BridgeLinkRows.cs`, `BridgeLaunch.cs`, `OverjetLink.cs`, `DtxStudioLink.cs`, `IapLink.cs` (compiled through the existing `VendorDirect\**\*.cs` wildcard).
- New: `WpfControlsOD/Bridges/Overjet.cs`, `WpfControlsOD/Bridges/DTXStudio.cs`, `OpenDental/Logic/VendorDirect/BridgeProgramLinkSetups.cs`.
- Rewritten: `WpfControlsOD/Bridges/Iap.cs`.
- Deleted: `OpenDental/Bridges/FormIap.cs`, `FormIap.designer.cs`, `FormIap.resx`.
- Edited (owned):
  - `WpfControlsOD/Logic/ProgramL.cs`: two dispatch branches.
  - `OpenDental/Forms/FormInsPlan.cs`: the body of `butIapFind_Click` only.
  - `FormInsPlan.designer.cs`: one tooltip.
  - `OpenDental/Forms/FormProgramLinkEdit.cs`: the save hook.
- Edited (shared):
  - `WpfControlsOD/WpfControlsOD.csproj`: 2 lines added.
  - `OpenDental/OpenDental.csproj`: the FormIap entries removed, 1 line added.
  - `OpenDental.sln` and `OpenDental.Local.slnf`: the test project.
- Outside the package's files:
  - `OpenDentBusiness/TableTypes/Program.cs`: 3 appended `ProgramName` values. This is an M1-02/M2-02 registry; the feature can't work without the names.
  - `tests/VendorDirectTests/Foundation/FoundationLayoutTests.cs`: `ProgramName_VendorDirectNamesAreAppendedLast` checked that the last four names were the F0 vendor names. It now checks the four names right after `ZImage`, so later appends don't break it. Its intent is unchanged.

## INFERRED behaviours (our choices; public sources are silent)

1. **ProgName values** are `Overjet`, `DTXStudio` and `DTXStudioAcquire` (the specs' proposals). Open Dental's real names aren't in public sources. See the owner questions. *Confirmed on 2026-11-07: the office's Open Dental 25.3.48 database uses exactly these names (owner question 1).*
2. **The rows are inserted at runtime, not by a fork migration step.** Two reasons:
   - The assigned level range (2027001701–2027001799) fails `ForkMigrationRunner.ValidateStep`, which requires `yyyyMMddNN`, and "20270017" has month 00. A step in that range would stop every migration.
   - The fork's vendor-direct convention already inserts program rows from Program Links (`Programs.InsertIfMissing`).

   Effect: the rows appear the first time someone opens Setup > Program Links. That is the only place they can be enabled anyway.
3. **Patient templates.** The vendors' parameter and argument formats aren't public, so we copied none. The office enters them from the vendor's guide, with these tokens:
   - `[PatientID]`: PatNum, or ChartNumber when the property says 1.
   - `[PatNum]`, `[ChartNumber]`, `[LName]`, `[FName]`.
   - `[Birthdate]`: `yyyy-MM-dd` for Overjet, the Birthdate Format for DTX.
   - `[Gender]`: M, F, O or U.

   Templates are replaced in one pass, so a value that looks like a token is not replaced again. Values are percent-encoded in URLs. On a command line, quotes and control characters are removed, and the template supplies any quotes.
4. **Overjet gets the PatNum/ChartNum property** (the spec's proposal). The public page lists none.
5. **The DTX Studio Acquire button text** defaults to "DTX Studio Acquire", so the two buttons can be told apart. Open Dental's page says "DTX Studio".
6. **The IAP address lives in "Path of file to open"**, and no default is shipped. The public page gives no address.
7. **IAP button gating is kept as in 24.3**: hidden when a plan is edited from the plan list, disabled without InsPlanEdit. The Benefit Notes button still shows when IAP or Trojan is enabled, so notes from old imports stay readable.
8. **Plain http is refused** for the web links, even to localhost. The spec says "Accept only https://".
9. **Birthdate Format validation**: a format that throws, or that contains no date field at all (it prints itself back), is refused. It is checked only when the template uses `[Birthdate]`. Dates use the invariant culture, so `/` stays `/`.

## Deviations from the spec

- The spec says the rows come from "a conversion step if missing". They are inserted at runtime instead (INFERRED 2). They are still inserted only if missing, disabled, with the public defaults.
- The spec expected no change to `FormProgramLinkEdit`. A small save check was added for the two web links.
- The spec's "Additional properties: free text appended to the command line" is the program's existing "Command line" field. The patient arguments are a separate property.

## Interplay with packages built in parallel

- **B-SHELL** (B3) takes over `FormProgramLinkEdit`, and removes the DXC credit-score dispatch next to the new DTX branch in `ProgramL`. Expect at most a trivial adjacent-line merge in `ProgramL.cs`.
- **V-PEARL2** (C1) and **V-BRIDGE2** (C3) edit `FormProgramLinkEdit.cs`. The hook here is five lines in `butSave_Click`, before `ProgramCur.ProgName=...`.
  - V-BRIDGE2 also adds `ProgramL` branches and `ProgramName` values (Trophy enhanced tags, Clever One, Clerri). Its appends will touch the same enum tail; resolve by keeping both blocks. `FoundationLayoutTests` already tolerates more names after the F0 block.
  - V-BRIDGE2 can reuse `BridgeLinkRows`, `BridgeLaunch` and `StandardProgramLinkSetup` for its rows.
- **M2-HIST, M2-INSPLAN, V-PAYSUITE** edit `FormInsPlan.cs`. This package only replaced the body of `butIapFind_Click` (about lines 1482–1905 on the base) and one designer tooltip line. A package that edits nothing inside that method merges cleanly.
- **M1-02 / M2-02** own `ProgramName`. If the census shows other ProgName values, rename the three members there. Stored rows match by name, and no numbers are persisted.

## Owner questions

1. **ProgName values.** Please check the office database:
   `SELECT ProgramNum,ProgName,ProgDesc,Enabled,Path FROM program WHERE ProgName LIKE '%overjet%' OR ProgName LIKE '%dtx%' OR ProgName='IAP';`
   If Open Dental's names differ from `Overjet`, `DTXStudio` and `DTXStudioAcquire`, the enum members must be renamed before release. Otherwise a second, disabled row is added, and the office's own row falls back to the generic launcher. The `programproperty` descriptions of those rows also tell us whether Open Dental had a PatNum/ChartNum or other property we should match. **Answered (2026-11-07, owner question Switch-over 1):** the office's program table has links named exactly `Overjet`, `DTXStudio`, `DTXStudioAcquire` and `IAP`, this program's names, so nothing is renamed and no second link is added (see "The office's link names" below). The property descriptions of those rows were not part of the answer; a property of Open Dental's that this program does not know stays as it is, unread.
2. **Migration level range.** 2027001701–2027001799 is not a valid `yyyyMMddNN` level: `ForkMigrationRunner.ValidateStep` throws for month 00. This package needed no step. Other packages given ranges of the same shape will hit this.
3. **Overjet.** To open a patient directly, we need the `bridge_redirect` parameter names from Overjet. Once known, should we ship a default "Patient URL parameters" template, or leave it to the office?
4. **DTX Studio.** We need the `DTXsync.exe` command-line specification from DTX/Envista, including how Acquire differs. The same question applies: ship default "Patient arguments", or leave them to the office?
5. **IAP.** Which sign-in address should the office enter? Should the IAP button also show when a plan is edited from the insurance plan list? It is hidden there, as in 24.3.

## Tests

`tests/VBRIDGE1Tests` (MSTest v1, x86, .NET 4.8) has 37 tests:

- **`LaunchRulesTests`** (no database):
  - Overjet: URLs with and without a patient, encoding, PatNum/ChartNumber, https and blocked hosts.
  - DTX: the order of the checks, argument filling, Birthdate Format, quote and control-character stripping, the appended command line, gender codes.
  - IAP: blank and desktop paths, https only, nothing added to the address.
  - Shared: one-pass tokens, the save rule, a message for every problem.
- **`ProgramRowsDbTests`**: a scratch database, built like `tools/New-DevDatabase.ps1`, upgraded by the fork migration runner and dropped afterwards. It checks:
  - the rows are inserted disabled, with the public defaults, only once;
  - the toolbar button is hidden until the link is enabled;
  - a row the office already has keeps its settings and only gets missing properties;
  - the IAP desktop-path cleanup runs once, and web addresses are left alone;
  - launches read the rows and properties.
- **`WiringTests`** (sources):
  - `ProgramL` dispatch, and the bridges calling the rules;
  - the IAP import and `FormIap` are gone, and the IAP button calls the launcher;
  - the four setups are registered, and the save check comes before the save;
  - `ProgramName` is appended with numbers kept;
  - the notes and descriptions have no promotional wording and no Open Dental host.

Related existing assemblies: `VendorDirectTests` (the `ProgramName` layout test and the vendor-direct registry) and `ForkMigrationTests` (a TableTypes file changed, enum only).

Not tested: the windows themselves (the GUI isn't launched), starting a real browser or `DTXsync.exe`, and the vendors' real pages and programs.

## The office's link names (SWMAP, 2026-11-07, branch `next/swmap-1`)

- Owner question Switch-over 1 / V-BRIDGE1 1: the office's Open Dental 25.3.48 program table has links named exactly `Overjet`, `DTXStudio`,
  `DTXStudioAcquire` and `IAP`. These are `ProgramName.Overjet`, `DTXStudio`, `DTXStudioAcquire` and `IAP`, so no name changes:
  `Programs.InsertIfMissing` (SQL, as MariaDB compares names) and `Programs.GetCur` (exact) both find the office's row, `BridgeLinkRows.EnsureProgram`
  inserts no second row and leaves the office's row, its settings and its toolbar buttons as they are, and adds only the ClinicNum 0
  properties the row lacks. The IAP clean-up still applies only to a desktop path left over from 24.3; a web address is kept.
- Test: `SWMAPTests.AdoptedOd253Tests.ProgramLinks_ReuseTheOfficeRows_NoSecondLink` (an adopted synthetic 25.3.48 database with the four rows,
  made-up paths and buttons: after the four setups run, one row per name with its ProgramNum, Enabled, path, command line and note, the two
  office buttons and no new one, the office's Birthdate Format kept and the missing Patient arguments property added). See
  [SWMAP.md](SWMAP.md).
