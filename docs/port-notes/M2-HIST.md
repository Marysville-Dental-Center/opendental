# Port note: M2-HIST, Insurance History from code groups

Branch `port/m2-hist`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run, beside the other packages. Finished on `port/m2-hist-t1`, which merges the integration head `port/int-8` (45b919048) and fixes the review findings (see "Integration and review fixes" at the end). Backlog section: [port-backlog.md, M2-HIST](../specs/port-backlog.md#m2-hist-insurance-history-from-code-groups). Fork migration level used: 2026110104 (renumbered from 2027004701 on 2026-11-01; the package range 2027004701 to 2027004799 is retired). Tests: `tests/M2HISTTests` (assembly `M2HISTTests`).

Sources: the 26.1 feature spec (`docs/specs/features-26.1.md`), Open Dental's public 26.1 release notes and the published 26.1 database documents as the spec cites them (the three `codegroup` columns and their rules), and the 24.3 manual pages on Insurance History and Code Groups. Nothing was decompiled, installed or observed, and no office data was used. Behaviour that is not public is marked **INFERRED**.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-setup-codegroup-history-options` | built | Code Group Edit: Show In History, History Proc Code with a "..." picker, Perio Four Quadrants. Code Groups list: Hist column. Rules, warnings, log text. |
| `v26_1-family-inshist-codegroups` | built | Insurance History window (B-INSH's two dates, Edit and Remove) and Family module rows from code groups; entering dates with the History Proc Code; 271 imports matched to code groups; frequency checks read the history groups; the ten Insurance History code boxes removed from Preferences; the ten preferences migrated into code groups (fork step) and kept, with the adoption tools expecting the step's changes; Reset Code Groups gives the history rows. |

The schema (`codegroup.ShowInHistory`, `HistProcCode`, `IsPerioFourQuads`), the table class fields and the CRUD come from M2-01; nothing was added to them.

## v26_1-family-inshist-codegroups

The rules are in the new `OpenDentBusiness/Data Interface/InsHistCategories.cs`, so they can be tested without the windows.

- **Rows** (`InsHistCategories.GetAll`): one `InsHistCategory` per code group marked Show In History, in code group order (ItemOrder, then CodeGroupNum). A Perio Four Quadrants group gives four rows, UL, UR, LL, LR. Retired (hidden) groups keep their row: the flags are independent (spec rule 5). Each row has a `Key` (CodeGroupNum, plus `:UR` and so on), used as the Family grid row tag.
- **Labels**: a whole-mouth row shows the group name. A quadrant row of the fixed SRP group shows "Perio Scaling UL" and so on, the published labels; a quadrant row of any other group shows its own name and the quadrant (**INFERRED**, so two four-quadrant groups never share labels).
- **Reading history** (`GetProcs`, `GetMostRecentProc`): the caller passes the procedures it counts as history, as in 24.3 (EO and C procedures with an InsHist or Received claimproc on the inssub). A procedure counts for a row when its code is in the group's `ProcCodes` (ranges included, through the existing `ProcCodeRanges`) or is the row's `HistProcCode` (**INFERRED**: otherwise a date entered with a History Proc Code that is not in the group's own list would never read back), and for a quadrant row when its `Surf` contains the quadrant, as 24.3 matched. The window and the Family grid show two dates per row (B-INSH's `InsHistDates.GetMostRecentProcs`, now on these rows: newest first, a completed procedure before an EO one on the same day); the 271 import updates the most recent one (`GetMostRecentProc`, the 24.3 order).
- **Entering a date** (`InsertOrUpdateProcedure`): the 24.3 rules with the row's `HistProcCode` and, for a quadrant row, the quadrant as `Surf`: no procedure, or a completed one older than the date, adds an Existing Other procedure with an InsHist claimproc; a date that is not newer than a completed procedure changes nothing; an Existing Other procedure is moved, with its claimprocs. Recalls are recalculated after an insert (24.3) and after a move (**INFERRED**, the spec's "recall due dates update"). A new procedure needs a valid History Proc Code; without one nothing is saved and `InvalidCode` is returned (24.3 could insert a procedure with CodeNum 0 from the 271 path).
- **Insurance History window** (`FormInsHistSetup`): B-INSH's window (25.1 two dates, 25.2 Edit and Remove, completed dates read-only) with the rows of `InsHistCategories.GetAll()`, built in code before the layout manager starts, a label, two date boxes and their Edit and Remove buttons each, Save under the last row and the window sized to fit. Save keeps the designer's top-left anchor (see review fix 1). With no history group the window says to set them up in Setup, Code Groups. A row whose History Proc Code is not valid shows its dates, has a tooltip on its label, and a new date for it is not saved (`InsHistDates.ApplyActions` says so). Saving goes through `InsHistDates.GetActions`/`ApplyActions`, which now take an `InsHistCategory` and write through `InsHistCategories.InsertOrUpdateProcedure`.
- **Family module** (`ControlFamily`): the insurance history rows come from the same rows, each cell with both dates (`InsHistDates.GetDatesText`) or "No History"; the row tag is the row's `Key`, and a double-click still opens the window.
- **Eligibility (271)** (`EB271.SetInsuranceHistoryDates`, `InsHistCategories.GetForEligibility`): a limitation line with a DTP*304 date and a MSG segment or a procedure code (EB13) goes first to the row whose `CodeGroupFixed` is the category its MSG names (the same four texts 24.3 knew: bitewings, exams, full mouth/panorex, prophylaxis), otherwise to the row whose `HistProcCode` is the code the line names. Quadrant rows are never picked (a 271 names no quadrant). A line that matches no row, or a row without a valid History Proc Code, is skipped and logged: `Logger.LogVerbose` (folder InsHistory) and an optional list parameter. Both callers (Edit Insurance Plan's eligibility request and batch insurance verification with Change Ins History) go through this method and are unchanged.
- **Frequency limitations** (`ProcedureCodes.GetCodeNumsForAllLimitations`): what the InsHist*Codes preferences added for the fixed frequencies now comes from the history rows of the code group with the same fixed category (their codes and History Proc Code). One line in the private helper `GetInsHistCodeNumsForBenefit`.
- **Preferences, Treatment Plan, General** (`UserControlTreatPlanGeneral`): the ten code boxes are removed; the group box keeps its title and shows a note pointing to Setup, Code Groups.
- **Sheets** keep using `CodeGroupFixed` (spec rule 7); nothing changed there.
- **Kept for older callers, unchanged**: `Procedures.GetMostRecentInsHistProc`, `InsertOrUpdateInsHistProcedure`, `ProcedureCodes.GetCodeNumsForInsHistPref`, `GetByInsHistPref`, `Prefs.GetInsHistPrefNames`, `GetInsHistPrefs`, `EB271.GetPrefNameInsHistDate`, `GetInsHistDate(PrefName)`. Nothing in this branch calls the preference-based ones for Insurance History any more. `Procedures.cs`, `ClaimProcs.cs`, `CodeGroups.cs`, `InsVerifies.cs`, `Prefs.cs` and `FormInsPlan.cs` are not edited.

### Data migration (`OpenDentBusiness/Misc/ForkMigrations/InsHistCodeGroupsStep.cs`, level 2026110104, renumbered from 2027004701 on 2026-11-01)

**Changed on 2026-11-07** (owner decision, switch-over question 4): the step now changes no existing code group and adds a group used only
for Insurance History for each category; see "Owner decision of 2026-11-07" at the end. The rule below is the one before that change.

Open Dental's own conversion is not public; this is the spec's rule, all **INFERRED**. The rule is in `InsHistCodeGroupsMigration.Plan` (pure, tested without a database); the step reads the rows with plain SQL and writes plain `UPDATE`/`INSERT` statements.

1. For each history category whose preference has a value (for the perio quadrants, any of the four): the code group whose `CodeGroupFixed` matches (Bitewing BW, Pano/FMX PanoFMX, Exam, Prophy, the perio quadrants SRP, Perio Maintenance Perio, Full Mouth Debridement FMDebride), a group in use before a retired one, lowest ItemOrder first. With none, a new group named after the 24.3 row ("Bitewing", "Pano/FMX", "Exam", "Prophy", "Perio Scaling", "Perio Maintenance", "Full Mouth Debridement"), with that `CodeGroupFixed`, after every other group, in no benefit grid (not hidden).
2. `ShowInHistory=1`; `HistProcCode` is the first code of the preference that is in the procedure code list (a range gives its first code in the list, written as the list has it); when none is, the first code as written, so the setting is not lost and Code Group Edit asks for a valid one.
3. `ProcCodes` gets every code and range of the preference that it does not hold yet (a code inside one of its ranges counts as held), appended in the order written. An unchanged list keeps its text exactly.
4. The four perio quadrant preferences share one group with `IsPerioFourQuads=1`; `HistProcCode` comes from UR, else the first of UL, LR, LL with a value. When the quadrants had different codes, every code goes into `ProcCodes` and a note is logged.
5. The preferences are read, never changed or deleted.

Gates: the step throws if the three columns are missing (M2-01's 26.1 step must run first); it runs only on a database from before 25.4 (`Seed26_1Step.IsFromBefore25_4`: 24.3, an adopted 25.3 database), only on its first run (a RunAll rerun of an applied level skips it), and only while no code group is marked Show In History. A preference that is blank gave a "Not Set" row in 24.3; it gives no row from 26.1 on.

One plan for the step and the adoption tools: `InsHistCodeGroupsMigration.CreatePlan(isFromBefore25_4,isLevelApplied,prefs,codeGroups,procCodes)` applies the gates and the rule above and returns a `MigrationPlan` (the groups as read, the groups to update and to add, the notes, `GetReasonUnchanged`, `GetLogLines`). The step reads the rows (`ReadPrefs`, `ReadCodeGroups`, `ReadProcCodes`), writes the plan (`Run`, plain `UPDATE`/`INSERT`) and logs one line per group ("Code group 'BWs' (CodeGroupNum 3) shown in Insurance History, History Proc Code Z0272, codes added: ...", "Added code group 'Exam' (Exam, ItemOrder 3), shown in Insurance History, ...") plus the notes, instead of its SQL. AdoptionValidation's `migration-preview` prints the same plan for an Open Dental 25.3.48 database and `adopt`'s row check holds the adopted copy to it (see review fix 2).

On the 24.3.63 seed (`UnitTests/Resources/dump.txt`), whose ten preferences are set, the step marks the seven fixed groups and the Insurance History window shows the same ten rows as before (tested).

## v26_1-setup-codegroup-history-options

- **Code Group Edit** (`FormCodeGroupEdit`): "Show In History" and "Perio Four Quadrants" check boxes next to the other options, and a "History Proc Code" box with a "..." button (the procedure code picker) under Proc Codes. History Proc Code and Perio Four Quadrants are enabled only while Show In History is checked. The window is 30 pixels taller.
- **Rules** (`CodeGroupHistoryOptions` in `InsHistCategories.cs`): History Proc Code is required with Show In History, must be in the procedure code list and must be a single code (no list, no range); a code typed in another case is saved as the list has it. Perio Four Quadrants requires Show In History. Unchecking Show In History clears both on save (`Apply`). When the History Proc Code is not one of the group's own codes, a Yes/No warning says frequency limitations will not count the history it creates.
- **Warnings**: deleting a group marked Show In History, and unchecking Show In History, warn that its Insurance History row disappears and that existing history procedures are kept. Hiding (retiring) a group marked Show In History shows a note that it keeps its row (see deviations). The old delete text "Sheet fields and Insurance History that use it" now names only sheet fields, and the fixed-group explanation (`label5`) says history dates from eligibility responses are matched to fixed groups first.
- **Code Groups list** (`FormCodeGroups`): a "Hist" column after "Age". Order of rows is the history row order (item order), already editable with the up and down buttons.
- **Log**: the existing "Code group '<name>' edited:" Setup entry also names "shown in history" or "removed from history", "history proc code from 'X' to 'Y'", and "perio four quadrants on/off".
- **Cache**: saving already sets the code group cache invalid (`DataValid.SetInvalid(InvalidType.CodeGroups)`), so the Insurance History rows follow at the next open.

## INFERRED behaviours

- Every rule of the migration above.
- The History Proc Code always counts as history for its row, even when not in the group's own codes.
- Quadrant row labels for groups other than the fixed SRP group use the group name.
- Recalls are recalculated after a moved Existing Other date, not only after an insert.
- A row without a valid History Proc Code shows its history, but entering a new date for it saves nothing and is reported.
- 271 lines: a line with a DTP*304 date needs a MSG segment or an EB13 procedure code; quadrant rows are never chosen; skipped lines go to the verbose log only (no message to the user).
- A created group carries the category's `CodeGroupFixed`, so later 271 imports and sheet fields (for example the last exam date) find it.

## Deviations from the spec

- Hidden groups and the history row: setup rule 5 says hiding a history group "warns that its history row disappears", while the family rule 5 says a hidden group still shows in history and the two flags are independent. The fork follows the family rule (the row stays) and shows a note instead of a warning when hiding. Owner question 2.
- The 271 date was read with the format "yyyymmdd", so every imported date fell in January; it is now read as CCYYMMDD (also in the kept `GetInsHistDate(PrefName)`). This is a bug fix outside the spec.
- The spec's "Touchpoints" list for `v26_1-family-inshist-codegroups` names `ControlAppt.cs`, `FormApptsOther.cs` and `UserControlApptGeneral.cs` (a paste from the recall item); the real touchpoints are the files listed below.
- `docs/specs/features-26.1.md` and the 26.1 schema document say `codegroup.CodeGroupFixed` is no longer used by the Insurance History window; the `CodeGroup.CodeGroupFixed` summary in `TableTypes/CodeGroup.cs` (foundation-owned) still says "seven are used in Ins History Window". Not edited here.
- No change to the ForkMigrations README's step table (a shared file other packages also add rows to); the step's own summary documents it.

## Files touched

- New: `OpenDentBusiness/Data Interface/InsHistCategories.cs`, `OpenDentBusiness/Misc/ForkMigrations/InsHistCodeGroupsStep.cs`, `tools/AdoptionValidation/InsHistCodeGroupPlan.cs`, `tests/M2HISTTests/*`, this note.
- Edited (owned by M2-HIST in this wave): `OpenDentBusiness/Data Interface/ProcedureCodes.cs` (one line), `OpenDentBusiness/X12/EB271.cs` (edited byte-safely; the file has Windows-1252 characters), `OpenDental/Main Modules/ControlFamily.cs` (the insurance history rows), `OpenDental/Forms/FormInsHistSetup.cs` and `.designer.cs` (rewritten), `OpenDental/Forms/FormCodeGroupEdit.cs`, `.Designer.cs`, `.resx`, `OpenDental/Forms/FormCodeGroups.cs`, `OpenDental/User Controls/Preferences/UserControlTreatPlanGeneral.cs` and `.Designer.cs`.
- Outside the owned list (also in the report):
  - `OpenDentBusiness/Misc/ForkMigrations/ForkMigrationRunner.cs` (foundation-owned): this branch's own copy of the package-level rule was dropped at the `port/int-8` merge; the integration's `IsReservedPackageLevel` (yyyy00PPNN from 2027 on) accepts 2027004701, which is unique on the integration head.
  - `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line (`Data Interface\InsHistCategories.cs`, after `CodeGroups.cs`). The step file is picked up by the existing `Misc\ForkMigrations\**\*.cs` wildcard.
  - `OpenDental.sln`, `OpenDental.Local.slnf`: the test project. `tests/M2HISTTests` now also references `OpenDental.csproj` (the window layout test).
  - B-INSH's files, moved onto the code-group rows at the merge: `OpenDentBusiness/Data Interface/InsHistDates.cs`, `tests/BINSHTests/InsHistDatesTests.cs`, `InsHistDbTests.cs`.
  - B-PROC's `OpenDentBusiness/Data Interface/CodeGroupDefaults.cs` (`ApplyHistoryDefaults`) and `CodeGroupResets.cs` (one call), as B-PROC's note asked.
  - The M1-Z adoption tools: `tools/AdoptionValidation/MigrationPreview.cs`, `AdoptionRowCheck.cs`, `OfficeFixture.cs`, `Rehearsal.cs`, `README.md`, `AdoptionValidation.Tests/Migration26_1Tests.cs`, `RehearsalIntegrationTests.cs`; `tools/SyntheticData/allowed-differences.adoption.json`, `README.md`, `SyntheticData.Tests/UnitTests.cs`; `docs/adoption-runbook.md`.
- Not edited although the spec or backlog lists them: `CodeGroups.cs`, `InsVerifies.cs`, `Prefs.cs`, `Procedures.cs`, `ClaimProcs.cs`, `FormInsPlan.cs`, `TableTypes/CodeGroup.cs`, `CodeGroupCrud.cs`, `PrefInfos.xml`.
- `OpenDental/Resources/PrefInfos.xml` (foundation-owned) still has the ten `labelInsHist*` entries of the removed boxes. They are harmless (the Preferences window looks entries up from controls), and the registry owner can remove them in a follow-up.

## Interplay with packages built at the same time

- **B-INSH** (25.1 two dates per row, 25.2 Edit and Remove): merged on `port/int-8` first; this branch moved its window, `InsHistDates` and its tests onto the code-group rows (see "Integration and review fixes").
- **M2-UDS, M2-EST** and any other package with a 2027 level: the integration's `IsReservedPackageLevel`; this step's level 2027004701 is unique.
- **M2-EST, M2-CAN, B-PROC, A-INS** (`ProcedureCodes.cs`): the change here is one line in `GetInsHistCodeNumsForBenefit`; it should merge or resolve trivially.
- **M2-INSV** (`InsVerifies.cs`): not edited here; batch verification reaches the new mapping through `EB271.SetInsuranceHistoryDates`, whose signature only gained an optional parameter.
- **B-SHELL, V-BRIDGE2** (`ControlFamily.cs`): the change here is limited to the insurance history rows block.
- **M2-INSPLAN** (Edit Insurance Plan layout): the Hist button still opens `FormInsHistSetup(patNum,insSub)`; `FormInsPlan.cs` is not edited here.
- **M2-01 / M2-02**: the three columns, fields and CRUD of M2-01 are used as they are. No preference, permission or enum was added; the ten preference rows stay registered and seeded as before.
- **Adoption rehearsal** (`tools/SyntheticData`, `tools/AdoptionValidation`): the step does change an adopted Open Dental 25.3.48 database whenever its InsHist*Codes preferences hold codes (an office's do, and so does the rehearsal with `--config-from`). The adoption tools now expect exactly the step's plan; see review fix 2.

## Tests

`tests/M2HISTTests` (MSTest v1, x86, .NET 4.8), 43 tests:

- Without a database: `InsHistCategoriesTests` (rows, order, hidden groups, quadrant rows and labels, keys, matching with ranges and quadrants, newest-first order, the 271 mapping, the preference to category map, the Code Group Edit rules, `Apply`, the log text), `InsHistMigrationPlanTests` (existing and created groups, first code, perio quadrants with and without UR, ranges, merging, retired groups, nothing to do), `EligibilityInsHistTests` (the DTP*304 month, which lines carry a history date, the EB13 code, the four MSG texts), `CodeGroupDefaultsHistoryTests` (the CDT defaults give the 24.3 rows with their History Proc Codes; a CDA default that keeps the office's codes gets its row).
- Database, a fresh 24.3.63 database upgraded by the fork migration runner (`M2HistDb`, per-run name `forkmig_<run>_m2hist`, dropped afterwards): `FreshDatabaseMigrationTests` (the seed's ten preferences become seven history groups and the same ten rows, the other groups and the preferences are unchanged, a RunAll rerun changes nothing), `InsHistDbTests` (entering a date, moving it, completed procedures, quadrant rows, an invalid History Proc Code, a 271 import with two matched and three skipped lines and the September date, frequency codes from the history group and not the preference, the cache after saving options) and `InsHistWindowLayoutTests` (the Insurance History window built in memory, never shown, with 0, 1, 2, 3, 4, 7, 10 and 13 rows and with a four-quadrant group: every row's controls, Save below the last row and inside the window; it fails with the old bottom anchor, Save at y=-102 with no row).
- Database, a scratch database with the 26.1.70 reference schema (`forkmig_<run>_m2hist_mig`): `InsHistMigrationDbTests` (from 25.3: migrated once and the preferences kept; the plan read before the step is what it writes and logs, `Plan_IsWhatRunDoes`; from 26.1, with rows already set up, and with no preferences: nothing changes).

Elsewhere: `tests/BINSHTests` (B-INSH's 36 tests, now on code-group rows), `AdoptionValidation.Tests` (`Migration26_1Tests.InsHistPlanLines_SayWhatTheStepChanges`; the rehearsal's M6, `Adoption_MovesTheInsHistPreferencesIntoCodeGroups`, `RowCheck_CatchesCodeGroupChangesTheWholeTableRuleAllows`, and the preview and adoption-log expectations), `SyntheticData.Tests` (`CodeGroup_IsAllowedAsAWholeTable`).

Checks on the finished branch (`port/m2-hist-t1`, 2026-10-31): see "Verification" at the end.

## Not tested

- The windows (Code Group Edit, Code Groups, the Treatment Plan preferences) and the Family grid, and the Insurance History window's behaviour (Edit, Remove, the tooltip, saving): OpenDental.exe is not launched in package work. They compile, and their rules are tested through `InsHistCategories`, `InsHistDates` and `CodeGroupHistoryOptions`. The Insurance History window's layout is tested in memory (`InsHistWindowLayoutTests`).
- The two 271 callers end to end (Edit Insurance Plan's eligibility request, batch verification); `EB271.SetInsuranceHistoryDates` itself is tested with synthetic 271 lines.
- An office-size database: the step reads every code group and procedure code once and writes at most seven rows.

## Questions for the owner

1. Quadrant row labels: "Perio Scaling UL" for the fixed SRP group (published labels) and the group name for any other four-quadrant group. Or should every quadrant row show its group name?
2. Hidden groups: the spec contradicts itself (warn that the row disappears, or keep the row). The fork keeps the row and shows a note. Confirm.
3. Groups the migration creates (when a fixed category had no group) carry that category's `CodeGroupFixed`, so sheet fields such as the last exam date start finding them. Confirm, or create them without a fixed category.
4. Eligibility: should the MSG texts FLUORIDE and SEALANTS go to the Fluoride and Sealant fixed groups when those are marked Show In History? Today they match only by procedure code. (Sealant history without a tooth is of little use.)
5. A preference that was blank showed a "Not Set" row in 24.3; from 26.1 that category has no row until a group is marked Show In History. Acceptable?
6. Eligibility lines that match no row are only in the verbose log. Should Edit Insurance Plan's eligibility request tell the user which lines were skipped?
7. The 271 date fix (dates were all read as January) changes what imports write. Confirm.
8. Remove the ten orphaned `labelInsHist*` entries from `PrefInfos.xml` (registry owner)?

## Integration and review fixes (branch `port/m2-hist-t1`, on `port/int-8`)

### Merge with `port/int-8` (45b919048)

- **B-INSH** (two dates per row, Edit and Remove) was merged on the integration branch first. Its window and `InsHistDates` were keyed by the InsHist*Codes preferences; the merge keeps B-INSH's window and rules and gives them this package's rows, as both port notes proposed:
  - `InsHistDates.GetMostRecentProcs`, `GetDatesText`, `GetActions` take an `InsHistCategory` (and an optional CodeNum-to-code function) instead of a `PrefName` and a code list; `InsHistAction.PrefName` became `InsHistAction.InsHistCategory`; `GetCategoryName` and `GetQuadrant` are gone (the row's `Description` and `Quadrant`).
  - `ApplyActions` writes new and moved dates through `InsHistCategories.InsertOrUpdateProcedure` (new optional `doSynchRecalls`, false there, because `ApplyActions` synchs the recall once at the end); a new date for a row without a valid History Proc Code is reported, not saved.
  - `FormInsHistSetup`: B-INSH's window with rows from `InsHistCategories.GetAll()`; the "Not Set" state is gone; control names use the row index; the heading names code groups.
  - `ControlFamily`: the code-group rows with B-INSH's two-date cell text.
  - `tests/BINSHTests`: each test sets up its own code groups marked Show In History (the fork migration marks the seed's groups, so the tests clear the history flags first) instead of the preferences; `GetCategoryName_TheTenCategories` became `Rows_TheCodeGroupsMarkedShowInHistory`. No other test was removed.
- **ForkMigrationRunner**: the integration's `IsReservedPackageLevel` kept.
- `OpenDentBusiness.csproj`: both sides' compile items kept.
- **B-PROC** (Reset Code Groups) asked M2-HIST to give the default groups the 26.1 history options. `CodeGroupDefaults.ApplyHistoryDefaults` (**INFERRED**, the 26.1 defaults are not public): a default group of one of the seven 24.3 history categories (BW, Pano/FMX, Exam, Prophy, SRP, Perio, FMDebride) that has codes shows in Insurance History with its first code (the start of a range) as History Proc Code, SRP with four quadrant rows; `CodeGroupResets.KeepCodesForEmptyDefaults` applies it again when a CDA default takes the office's codes. On the CDT defaults that gives the ten 24.3 rows with the codes the 24.3 preferences held by default. Before, a reset left no Insurance History row at all.
- Checked against the merged packages and unchanged: M2-INSV (batch verification still calls `EB271.SetInsuranceHistoryDates`), M2-EST and M2-CAN (`ProcedureCodes.GetInsHistCodeNumsForBenefit` reads the history groups), B-CLAIM (`ClaimProcs.cs` not edited here).

### Review fix 1: the Save button of the dynamic window

The finding was right for this branch's window: its designer anchored Save Bottom|Right on a 308 px client area, so setting `Location` and then `ClientSize` in `AddCategoryRows` moved Save a second time (WinForms keeps the distance to the old bottom edge), and with one to three rows Save ended above the window. The merged window (B-INSH's designer) keeps Save anchored top left and places it from the last row (`y+SAVE_GAP`), and sizes the window from Save's bottom; the no-rows note counts as a row. `InsHistWindowLayoutTests` builds the window in memory on an STA thread with 0 to 13 rows and checks every row's controls and that Save is below the last row and inside the window; with the bottom anchor put back it fails (Save at y=-102 with no row).

### Review fix 2: the adoption rehearsal and the codegroup changes

The finding was right: the note's "changes nothing in the rehearsal" held only without `--config-from`, and an office's 25.3.48 database always has the preferences. Covered the way `port/int-6` covered M2-UDS's report row step:

- **One plan** (`InsHistCodeGroupsMigration.CreatePlan`, above), shared by the step's `Run`, `migration-preview` and `adopt`'s row check.
- `tools/AdoptionValidation/InsHistCodeGroupPlan.cs` (new): the plan for a database on the server, read with plain SQL (the ten preferences, `codegroup` with or without the 26.1 columns, `procedurecode`, the applied levels); `Lines()` is what `migration-preview` prints (runbook step 0.2), including the adoption log's lines.
- `MigrationPreview`: prints it after the UDS+ plan.
- `AdoptionRowCheck.CheckCodeGroup` (new): the planned groups may change only `ProcCodes` (to the planned codes) and the three 26.1 columns (to the planned values); each planned new group must be one added row with exactly the planned values; any other changed, added or removed row fails. `CheckNewTablesAndColumns` still holds the 26.1 columns of every other row to their defaults. `codegroup` is in `ArrayWholeTables`.
- `allowed-differences.adoption.json`: `codegroup` as a whole table (an added row changes every column checksum), held row by row by adopt; the `ColumnAdded` rules of the three columns stay.
- The rehearsal: the office fixture sets the ten preferences with SyntheticData's codes (one code the perio maintenance group lacks) and makes the generated full mouth debridement group the office's own, so the step marks six groups, appends a code and adds one group; new check M6 (the step changed exactly what the preview listed, the row check found it, the adoption log names each group, the preferences are kept); V11 names `codegroup`.
- Docs: the runbook (item 6, step 0.2 and its checklist row, step 4's log and row-check lines, now 16 steps and level 2027004701, step 6.1's whole tables), the AdoptionValidation and SyntheticData READMEs.
- No guard was loosened: the whole-table rule for `codegroup` is the same kind as `displayreport`'s, and adopt's row check is stricter than before for the table (it was not compared at all).

### Verification

On `port/m2-hist-t1` after the `port/int-8` merge and the fixes (2026-10-31):

- One full build of `OpenDental.Local.slnf` through the build lane (Debug, x86): 0 errors.
- `run-tests.ps1 -Mode Core -Include AdoptionValidation.Tests,ForkMigrationTests,M201Tests,SyntheticData.Tests` (`test-results/port_m2-hist-t1.txt`): 66 assemblies, 3,482 tests, 3,480 passed, 0 failed; the 2 not passed are M201Tests' published-document checks, inconclusive without `M201_OD_DOCS_DIR`. Among them M2HISTTests 43/43, BINSHTests 36/36, BPROCTests 43/43, AdoptionValidation.Tests 59/59 (the whole rehearsal, two runs, with M6), ForkMigrationTests 38/38, SyntheticData.Tests 54/54, NoHqTests 49/49.
- `InsHistWindowLayoutTests` fails with the old bottom anchor put back in the designer (Save at y=-102 with no row), and passes with the fix.
- `tools/Test-NoHqHosts.ps1`: passed.

## Integration (branch `port/int-11`)

- Merged onto `port/int-10` after M2-CARRIER, with no conflicts and no integration commit of its own. Since `port/int-8` the integration branch gained M2-TERM, M2-SHEETS, V-EDS, M2-CLAIMRCV, M2-AUDIT, M2-MISC and M2-CARRIER; none of them changed this package's files, the fork migrations or the M1-Z adoption tools, so the step, the shared plan and the row check merged as they were.
- **Level** 2027004701 is still the only one in the M2-HIST range on the merged head (the other 2027 levels are 2027002301, B-SHELL, and 2027003501, M2-UDS).
- **M2-CARRIER**: both packages read code lists through the shared `ProcCodeRanges` (Insurance History rows, the step's merge, the carrier's preauthorization list); neither added a parser of its own. They share no file. M2-CARRIER's scratch databases run the fork runner on the 24.3 seed, so they now also get this step's code group changes; its tests use made-up `Z` codes and read no code groups, and pass unchanged.
- **M2-SHEETS**: sheets' static text fields (dateLastBW and the like) still use `CodeGroupFixed`, as spec rule 7 asks; nothing to connect. **M2-CLAIMRCV**: "Hold Until Prior Received" is a claim status; history still counts completed procedures on received claimprocs. **V-EDS, M2-TERM, M2-AUDIT, M2-MISC**: nothing in common.
- Adoption: this step's changes are expected as before (`AdoptionValidation.Tests` 59 of 59, with the rehearsal's M6; `SyntheticData.Tests` 54; `ForkMigrationTests` 38; `M201Tests` 24 passed and 2 inconclusive). On the merged head: one full build of `OpenDental.Local.slnf` through the build lane (0 errors), `run-tests -Mode Full` (73 assemblies, 3850 tests, none failed; the 2 not passed are M201Tests' published-document checks) and `tools/Test-NoHqHosts.ps1` (passed). `M2HISTTests` 43 of 43, `BINSHTests` 36, `BPROCTests` 43, `M2INSVTests` 17, `M2ESTTests` 69.

## Combined review 1 (branch `port/review-1`, 2026-11-01)

- **Level.** Owner decision of 2026-11-01: levels are dated `yyyyMMddNN` only, every new step above every existing one, and the package ranges are retired. `InsHistCodeGroupsStep` is now 2026110104 (renumbered from 2027004701), still the last step, after B-SHELL (2026110101, from 2027002301), M2-EST (2026110102, from 2027002701, not active) and M2-UDS (2026110103, from 2027003501). `ForkMigrationRunner.IsReservedPackageLevel` is removed. The step, `InsHistCodeGroupPlan` and the rehearsal read the level from the step, so only the expected texts of the tests, the runbook and the READMEs changed ("Fork schema level 2026110104").

## Owner decision of 2026-11-07: the existing code groups stay unchanged (switch-over question 4)

The combined review asked whether the codes of the old Insurance History boxes should be added to the matching frequency code group (which
widens its frequency limitation in insurance estimates) or go into a group that only feeds Insurance History. The owner chose the second: the
step must leave the existing frequency-limit code groups unchanged and put the codes into a code group used only for Insurance History. No
office database ran the step's earlier rule (it was released in v0.0, which was never adopted, and the owner reinstalls rather than upgrades
v0.0), so the step itself changed, at the same level 2026110104.

**The rule now** (`InsHistCodeGroupsMigration.Plan`, `OpenPmsDentalBusiness/Misc/ForkMigrations/InsHistCodeGroupsStep.cs`), for each history
category whose preference has a value (the four perio quadrants count as one):

1. A **new** code group named after the 24.3 row with " History" ("Bitewing History", "Pano/FMX History", "Exam History", "Prophy History",
   "Perio Scaling History", "Perio Maintenance History", "Full Mouth Debridement History"; " 2", " 3" and so on when a group of that name
   exists), after every other group, in the order of the 24.3 window.
2. It is used only for Insurance History: `ShowInHistory=1`, `ShowInFrequency`, `ShowInAgeLimit` and `ShowInOther` 0, not hidden, and
   `CodeGroupFixed` None, so no frequency limitation (`Benefits.Is...Frequency`, `CodeGroups.GetOneForCodeGroupFixed`), age limit, sheet field
   ("date of last exam" and the like), `ProcedureCodes.GetCodeNumsForAllLimitations` or Reset Code Groups takes it for a category's group. That
   also answers M2-HIST question 3 (the created groups carry no fixed category now).
3. `ProcCodes` holds the preference's codes and ranges in the order written; `HistProcCode` and the perio rules (UR first, one group with
   `IsPerioFourQuads=1`, a note when the quadrants had different codes) are as before.
4. **No existing code group changes**, the frequency and age limit groups among them: the plan has no updates (`MigrationPlan.ListUpdated` is
   empty), and `Run` writes only `INSERT`s.
5. The gates are unchanged (a database from before 25.4, the step's first run, no group marked Show In History yet), and the preferences are
   kept.

**Log text.** One line per added group, "Added code group 'Exam History' (ItemOrder 13), used only for Insurance History (not in frequency or
age limits), History Proc Code D0120, codes D0120,D0150" (`DescribeAdd`), then the perio note if any, then "Insurance History: the existing
code groups, the frequency and age limit groups among them, were not changed; the codes of the old Insurance History settings are only in
the added groups." (`NOTE_EXISTING_UNCHANGED`).

**Eligibility (271).** A history group without `CodeGroupFixed` can no longer be found by the category a response's MSG text names (bitewings,
exams, full mouth/panorex, prophylaxis). `InsHistCategories.GetForEligibility` gained a last rule: when no history row has that category, the
line goes to the history row whose History Proc Code is one of the codes of that category's code group (the office's bitewing group, say).
The response's own procedure code (EB13) still comes first. Quadrant rows are still never picked.

**Insurance History rows.** The rows read as the group names: "Exam History" and so on; the perio scaling rows "Perio Scaling History UL" and
so on (a four-quadrant group other than the fixed SRP group shows its name and the quadrant, M2-HIST question 1).

**The adoption tools follow the plan.** `migration-preview` (runbook step 0.2) now says "adds N code group(s) used only for Insurance History
and changes no existing code group", `adopt`'s row check (`AdoptionRowCheck.CheckCodeGroup`, unchanged logic) holds every existing row to its
original and each added row to the plan and prints "N of the N planned code group(s) used only for Insurance History added with the planned
values; no existing row or column changed", and the rehearsal's check M6 expects seven added groups on the office fixture and no changed one
(`OfficeFixture.ArrayInsHistGroupsAdded`). The runbook, the AdoptionValidation and SyntheticData READMEs say the same.

**Tests.** `InsHistMigrationPlanTests` (rewritten: new groups only, the existing ones untouched, names and numbering, order, perio, retired
groups left alone, the log lines), `FreshDatabaseMigrationTests` (the 24.3.63 seed: seven history-only groups added, every column of every
seed group unchanged, the ten rows with their new labels), `InsHistMigrationDbTests` (from 25.3: the office's groups unchanged, three groups
added; `Plan_IsWhatRunDoes` with an existing "Exam History"), `InsHistCategoriesTests.GetForEligibility_AHistoryOnlyGroup_...` (the 271 rule),
and in `AdoptionValidation.Tests` `Migration26_1Tests.InsHistPlanLines_SayWhatTheStepChanges` and the rehearsal's expectations
(`RehearsalIntegrationTests`: the preview, M6, the row check's negative control, the adoption log).
