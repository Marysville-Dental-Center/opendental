# Port note: M2-HIST, Insurance History from code groups

Branch `port/m2-hist`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run, beside the other packages. Backlog section: [port-backlog.md, M2-HIST](../specs/port-backlog.md#m2-hist-insurance-history-from-code-groups). Fork migration level used: 2027004701 (range 2027004701 to 2027004799). Tests: `tests/M2HISTTests` (assembly `M2HISTTests`).

Sources: the 26.1 feature spec (`docs/specs/features-26.1.md`), Open Dental's public 26.1 release notes and the published 26.1 database documents as the spec cites them (the three `codegroup` columns and their rules), and the 24.3 manual pages on Insurance History and Code Groups. Nothing was decompiled, installed or observed, and no office data was used. Behaviour that is not public is marked **INFERRED**.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-setup-codegroup-history-options` | built | Code Group Edit: Show In History, History Proc Code with a "..." picker, Perio Four Quadrants. Code Groups list: Hist column. Rules, warnings, log text. |
| `v26_1-family-inshist-codegroups` | built | Insurance History window and Family module rows from code groups; entering dates with the History Proc Code; 271 imports matched to code groups; frequency checks read the history groups; the ten Insurance History code boxes removed from Preferences; the ten preferences migrated into code groups (fork step) and kept. |

The schema (`codegroup.ShowInHistory`, `HistProcCode`, `IsPerioFourQuads`), the table class fields and the CRUD come from M2-01; nothing was added to them.

## v26_1-family-inshist-codegroups

The rules are in the new `OpenDentBusiness/Data Interface/InsHistCategories.cs`, so they can be tested without the windows.

- **Rows** (`InsHistCategories.GetAll`): one `InsHistCategory` per code group marked Show In History, in code group order (ItemOrder, then CodeGroupNum). A Perio Four Quadrants group gives four rows, UL, UR, LL, LR. Retired (hidden) groups keep their row: the flags are independent (spec rule 5). Each row has a `Key` (CodeGroupNum, plus `:UR` and so on), used as the Family grid row tag.
- **Labels**: a whole-mouth row shows the group name. A quadrant row of the fixed SRP group shows "Perio Scaling UL" and so on, the published labels; a quadrant row of any other group shows its own name and the quadrant (**INFERRED**, so two four-quadrant groups never share labels).
- **Reading history** (`GetProcs`, `GetMostRecentProc`): the caller passes the procedures it counts as history, as in 24.3 (EO and C procedures with an InsHist or Received claimproc on the inssub). A procedure counts for a row when its code is in the group's `ProcCodes` (ranges included, through the existing `ProcCodeRanges`) or is the row's `HistProcCode` (**INFERRED**: otherwise a date entered with a History Proc Code that is not in the group's own list would never read back), and for a quadrant row when its `Surf` contains the quadrant, as 24.3 matched. One date per row, as on this branch's base (see B-INSH below for the 25.1 two dates).
- **Entering a date** (`InsertOrUpdateProcedure`): the 24.3 rules with the row's `HistProcCode` and, for a quadrant row, the quadrant as `Surf`: no procedure, or a completed one older than the date, adds an Existing Other procedure with an InsHist claimproc; a date that is not newer than a completed procedure changes nothing; an Existing Other procedure is moved, with its claimprocs. Recalls are recalculated after an insert (24.3) and after a move (**INFERRED**, the spec's "recall due dates update"). A new procedure needs a valid History Proc Code; without one nothing is saved and `InvalidCode` is returned (24.3 could insert a procedure with CodeNum 0 from the 271 path).
- **Insurance History window** (`FormInsHistSetup`): rows built in code before the layout manager starts, one label and one date box each, sized to fit. With no history group the window says to set them up in Setup, Code Groups. Saving lists rows whose History Proc Code is not valid. The 24.3 delete rule is kept (clearing an EO date deletes that procedure, after a confirmation).
- **Family module** (`ControlFamily`): the insurance history rows come from the same rows; a double-click still opens the window.
- **Eligibility (271)** (`EB271.SetInsuranceHistoryDates`, `InsHistCategories.GetForEligibility`): a limitation line with a DTP*304 date and a MSG segment or a procedure code (EB13) goes first to the row whose `CodeGroupFixed` is the category its MSG names (the same four texts 24.3 knew: bitewings, exams, full mouth/panorex, prophylaxis), otherwise to the row whose `HistProcCode` is the code the line names. Quadrant rows are never picked (a 271 names no quadrant). A line that matches no row, or a row without a valid History Proc Code, is skipped and logged: `Logger.LogVerbose` (folder InsHistory) and an optional list parameter. Both callers (Edit Insurance Plan's eligibility request and batch insurance verification with Change Ins History) go through this method and are unchanged.
- **Frequency limitations** (`ProcedureCodes.GetCodeNumsForAllLimitations`): what the InsHist*Codes preferences added for the fixed frequencies now comes from the history rows of the code group with the same fixed category (their codes and History Proc Code). One line in the private helper `GetInsHistCodeNumsForBenefit`.
- **Preferences, Treatment Plan, General** (`UserControlTreatPlanGeneral`): the ten code boxes are removed; the group box keeps its title and shows a note pointing to Setup, Code Groups.
- **Sheets** keep using `CodeGroupFixed` (spec rule 7); nothing changed there.
- **Kept for older callers, unchanged**: `Procedures.GetMostRecentInsHistProc`, `InsertOrUpdateInsHistProcedure`, `ProcedureCodes.GetCodeNumsForInsHistPref`, `GetByInsHistPref`, `Prefs.GetInsHistPrefNames`, `GetInsHistPrefs`, `EB271.GetPrefNameInsHistDate`, `GetInsHistDate(PrefName)`. Nothing in this branch calls the preference-based ones for Insurance History any more. `Procedures.cs`, `ClaimProcs.cs`, `CodeGroups.cs`, `InsVerifies.cs`, `Prefs.cs` and `FormInsPlan.cs` are not edited.

### Data migration (`OpenDentBusiness/Misc/ForkMigrations/InsHistCodeGroupsStep.cs`, level 2027004701)

Open Dental's own conversion is not public; this is the spec's rule, all **INFERRED**. The rule is in `InsHistCodeGroupsMigration.Plan` (pure, tested without a database); the step reads the rows with plain SQL and writes plain `UPDATE`/`INSERT` statements.

1. For each history category whose preference has a value (for the perio quadrants, any of the four): the code group whose `CodeGroupFixed` matches (Bitewing BW, Pano/FMX PanoFMX, Exam, Prophy, the perio quadrants SRP, Perio Maintenance Perio, Full Mouth Debridement FMDebride), a group in use before a retired one, lowest ItemOrder first. With none, a new group named after the 24.3 row ("Bitewing", "Pano/FMX", "Exam", "Prophy", "Perio Scaling", "Perio Maintenance", "Full Mouth Debridement"), with that `CodeGroupFixed`, after every other group, in no benefit grid (not hidden).
2. `ShowInHistory=1`; `HistProcCode` is the first code of the preference that is in the procedure code list (a range gives its first code in the list, written as the list has it); when none is, the first code as written, so the setting is not lost and Code Group Edit asks for a valid one.
3. `ProcCodes` gets every code and range of the preference that it does not hold yet (a code inside one of its ranges counts as held), appended in the order written. An unchanged list keeps its text exactly.
4. The four perio quadrant preferences share one group with `IsPerioFourQuads=1`; `HistProcCode` comes from UR, else the first of UL, LR, LL with a value. When the quadrants had different codes, every code goes into `ProcCodes` and a note is logged.
5. The preferences are read, never changed or deleted.

Gates: the step throws if the three columns are missing (M2-01's 26.1 step must run first); it runs only on a database from before 25.4 (`Seed26_1Step.IsFromBefore25_4`: 24.3, an adopted 25.3 database), only on its first run (a RunAll rerun of an applied level skips it), and only while no code group is marked Show In History. A preference that is blank gave a "Not Set" row in 24.3; it gives no row from 26.1 on.

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
- One date per row: the base of this branch has the 24.3 single-date window; the 25.1 two dates and the 25.2 edit and remove are B-INSH's (built at the same time, see below).
- The 271 date was read with the format "yyyymmdd", so every imported date fell in January; it is now read as CCYYMMDD (also in the kept `GetInsHistDate(PrefName)`). This is a bug fix outside the spec.
- The spec's "Touchpoints" list for `v26_1-family-inshist-codegroups` names `ControlAppt.cs`, `FormApptsOther.cs` and `UserControlApptGeneral.cs` (a paste from the recall item); the real touchpoints are the files listed below.
- `docs/specs/features-26.1.md` and the 26.1 schema document say `codegroup.CodeGroupFixed` is no longer used by the Insurance History window; the `CodeGroup.CodeGroupFixed` summary in `TableTypes/CodeGroup.cs` (foundation-owned) still says "seven are used in Ins History Window". Not edited here.
- No change to the ForkMigrations README's step table (a shared file other packages also add rows to); the step's own summary documents it.

## Files touched

- New: `OpenDentBusiness/Data Interface/InsHistCategories.cs`, `OpenDentBusiness/Misc/ForkMigrations/InsHistCodeGroupsStep.cs`, `tests/M2HISTTests/*`, this note.
- Edited (owned by M2-HIST in this wave): `OpenDentBusiness/Data Interface/ProcedureCodes.cs` (one line), `OpenDentBusiness/X12/EB271.cs` (edited byte-safely; the file has Windows-1252 characters), `OpenDental/Main Modules/ControlFamily.cs` (the insurance history rows), `OpenDental/Forms/FormInsHistSetup.cs` and `.designer.cs` (rewritten), `OpenDental/Forms/FormCodeGroupEdit.cs`, `.Designer.cs`, `.resx`, `OpenDental/Forms/FormCodeGroups.cs`, `OpenDental/User Controls/Preferences/UserControlTreatPlanGeneral.cs` and `.Designer.cs`.
- Outside the owned list (also in the report):
  - `OpenDentBusiness/Misc/ForkMigrations/ForkMigrationRunner.cs` (foundation-owned): `ValidateStep` accepts the single-wave package levels (yyyy00PPNN, from 2027 on). Without it the assigned level 2027004701 is not a valid yyyyMMddNN level and the runner would refuse every migration. The change is byte-identical to the one on `port/m2-uds`, so the two merge without a conflict; `port/m2-est` instead made its step abstract until the level scheme is settled. Pick one scheme at integration.
  - `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line (`Data Interface\InsHistCategories.cs`, after `CodeGroups.cs`). The step file is picked up by the existing `Misc\ForkMigrations\**\*.cs` wildcard.
  - `OpenDental.sln`, `OpenDental.Local.slnf`: the test project.
- Not edited although the spec or backlog lists them: `CodeGroups.cs`, `InsVerifies.cs`, `Prefs.cs`, `Procedures.cs`, `ClaimProcs.cs`, `FormInsPlan.cs`, `TableTypes/CodeGroup.cs`, `CodeGroupCrud.cs`, `PrefInfos.xml`.
- `OpenDental/Resources/PrefInfos.xml` (foundation-owned) still has the ten `labelInsHist*` entries of the removed boxes. They are harmless (the Preferences window looks entries up from controls), and the registry owner can remove them in a follow-up.

## Interplay with packages built at the same time

- **B-INSH** (25.1 two dates per category, 25.2 edit and remove; branch `port/b-insh`, already built): rewrites `FormInsHistSetup.*` and changes one line of the same `ControlFamily` block, all keyed by `PrefName`. Expect conflicts in both. Suggested merge: take B-INSH's window (two dates, Edit and Remove) and replace its category source:
  - `Prefs.GetInsHistPrefNames()` becomes `InsHistCategories.GetAll()`; `CategoryRow.PrefName` and `InsHistAction.PrefName` become an `InsHistCategory`;
  - `InsHistDates.GetCategoryName(prefName)` becomes `category.Description`; `InsHistDates.GetQuadrant(prefName)` becomes `category.Quadrant`;
  - `ProcedureCodes.GetCodeNumsForInsHistPref(prefName)` plus the quadrant filter become `InsHistCategories.GetProcs(listProcedures,category)` (newest first; B-INSH takes the first two);
  - `ProcedureCodes.GetByInsHistPref(prefName).CodeNum==0` becomes `!InsHistCategories.HasValidHistProcCode(category)`;
  - `Procedures.InsertOrUpdateInsHistProcedure(patient,prefName,...)` becomes `InsHistCategories.InsertOrUpdateProcedure(patient,category,...)`, same parameter order;
  - the "Not Set" state (preference blank) disappears: every row has a code group; a row with an invalid History Proc Code can show dates but not add one;
  - control names built from `prefName.ToString()` should use `category.Key` with ':' removed, or the row index;
  - in `ControlFamily`, keep this branch's loop over `InsHistCategories.GetAll()` and put B-INSH's two-date cell text (`InsHistDates.GetDatesText`) in it, called with the category's procedures.
  B-INSH's tests (`tests/BINSHTests`) set the InsHist*Codes preferences to choose a category's codes; after the merge they must set up a code group with Show In History instead. B-INSH's note says the same about this package.
- **M2-UDS, M2-EST** and any other package with a 2027 level: see `ForkMigrationRunner.cs` above.
- **M2-EST, M2-CAN, B-PROC, A-INS** (`ProcedureCodes.cs`): the change here is one line in `GetInsHistCodeNumsForBenefit`; it should merge or resolve trivially.
- **M2-INSV** (`InsVerifies.cs`): not edited here; batch verification reaches the new mapping through `EB271.SetInsuranceHistoryDates`, whose signature only gained an optional parameter.
- **B-SHELL, V-BRIDGE2** (`ControlFamily.cs`): the change here is limited to the insurance history rows block.
- **M2-INSPLAN** (Edit Insurance Plan layout): the Hist button still opens `FormInsHistSetup(patNum,insSub)`; `FormInsPlan.cs` is not edited here.
- **M2-01 / M2-02**: the three columns, fields and CRUD of M2-01 are used as they are. No preference, permission or enum was added; the ten preference rows stay registered and seeded as before.
- **Adoption rehearsal** (`tools/SyntheticData`): its generator writes no InsHist*Codes preferences, so the step changes nothing there. If it ever writes them, `allowed-differences.adoption.json` needs a rule for the `codegroup` rows this step changes or adds.

## Tests

`tests/M2HISTTests` (MSTest v1, x86, .NET 4.8), 39 tests:

- Without a database: `InsHistCategoriesTests` (rows, order, hidden groups, quadrant rows and labels, keys, matching with ranges and quadrants, newest-first order, the 271 mapping, the preference to category map, the Code Group Edit rules, `Apply`, the log text), `InsHistMigrationPlanTests` (existing and created groups, first code, perio quadrants with and without UR, ranges, merging, retired groups, nothing to do), `EligibilityInsHistTests` (the DTP*304 month, which lines carry a history date, the EB13 code, the four MSG texts).
- Database, a fresh 24.3.63 database upgraded by the fork migration runner (`M2HistDb`, per-run name `forkmig_<run>_m2hist`, dropped afterwards): `FreshDatabaseMigrationTests` (the seed's ten preferences become seven history groups and the same ten rows, the other groups and the preferences are unchanged, a RunAll rerun changes nothing) and `InsHistDbTests` (entering a date, moving it, completed procedures, quadrant rows, an invalid History Proc Code, a 271 import with two matched and three skipped lines and the September date, frequency codes from the history group and not the preference, the cache after saving options).
- Database, a scratch database with the 26.1.70 reference schema (`forkmig_<run>_m2hist_mig`): `InsHistMigrationDbTests` (from 25.3: migrated once and the preferences kept; from 26.1, with rows already set up, and with no preferences: nothing changes).

Checks on the finished branch (2026-10-25):

- Build of `OpenDental.Local.slnf` (Debug, x86): 0 errors, 376 warnings (the baseline); the new test project has no warnings.
- `run-tests.ps1 -Mode Targeted -Include M2HISTTests`: NoHqTests 49/49, M2HISTTests 39/39.
- Related assemblies (`-Include ForkMigrationTests,M201Tests,InsuranceTests,SyntheticData.Tests,ReleaseFlowTests`): ForkMigrationTests 38/38, M201Tests 24 passed and 2 inconclusive (the published-document checks, `M201_OD_DOCS_DIR` not set), InsuranceTests 49/49, SyntheticData.Tests 49/49, NoHqTests 49/49. ReleaseFlowTests had 6 `AdoptionDbTests` failures in that combined run and passed 58/58 when run again on its own: that class uses the fixed database name `releaseflow_adoption`, which collides with other worktrees' runs on the shared server (already reported by M2-01).
- `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- The windows (Insurance History, Code Group Edit, Code Groups, the Treatment Plan preferences) and the Family grid: no test project drives WinForms, and OpenDental.exe is not launched in package work. They compile, and their rules are tested through `InsHistCategories` and `CodeGroupHistoryOptions`.
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
