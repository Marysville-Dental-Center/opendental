# Port note: B-INSH (insurance history, benefit editing and plan list)

Branch `port/b-insh`, cut from `m1/integration-a2-patch`. Backlog section: [port-backlog.md, B-INSH](../specs/port-backlog.md). Tests: `tests/BINSHTests` (assembly `BINSHTests`).

Sources used: Open Dental's public release notes for 25.1, 25.2, 25.3 and 25.4, the public manual pages on Insurance History, Edit Benefit, Other Benefits and the Insurance Plans list, and the published 25.3 database docs (`InsBlueBookRuleType` value 5). Nothing was decompiled, observed or copied. Where behaviour is not public it is marked **INFERRED**.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v25_1-family-inshist-two-dates` | built | Two dates per category (Most Recent, Previous) in the Insurance History window. The Family module grid shows both dates. |
| `v25_2-family-ins-history-edit-remove` | built | Each date has Edit (date picker) and Remove buttons, and the same two actions on its right-click menu. Completed dates are read-only. Permission checks and security log. |
| `v25_2-family-edit-benefit-ui` | built | Edit Benefit window redesign: Applies to radio buttons, inputs enabled by type, combos instead of list boxes, a preview line, and a check that names a field the type ignores. |
| `v25_3-family-insplans-innet` | built | In Net column in Lists > Insurance Plans. The grid can now be sorted by any column. |
| `v25_3-family-bluebook-providerfee` | built | The enum rename came from M1-02. This package changes the wording in the Blue Book rules window and in the estimate log. The preference keeps its database name. |

## v25_1-family-inshist-two-dates and v25_2-family-ins-history-edit-remove

The rules are in the new class `InsHistDates` (`OpenDentBusiness/Data Interface/InsHistDates.cs`), so they can be tested without the window. `Procedures.cs`, `Prefs.cs`, `Recalls.cs` and `EB271.cs` are not edited.

- **Which dates are shown.** `GetProcsForInsSub` loads the procedures the 24.3 window used: the patient's EO and C procedures that have an InsHist or Received claimproc on this inssub. `GetMostRecentProcs` keeps those with the category's codes (`ProcedureCodes.GetCodeNumsForInsHistPref`; the perio quadrants also match on `Surf`, as `Procedures.GetMostRecentInsHistProc` does). They are sorted newest first, and the first two are shown.
- **Window** (`FormInsHistSetup`). The rows are built in code from `Prefs.GetInsHistPrefNames()`, before the layout manager starts, so they scale like designer controls. Each row has a label, two date boxes, and Edit and Remove buttons for each box. A category whose preference is empty shows "Not Set" and is disabled, as in 24.3.
  - An empty box takes a typed date, or a date from Edit (the existing `FormCalendar` dialog).
  - An EO date can be typed over, changed with Edit, or cleared with Remove. Changes are applied when you click Save.
  - A date from a completed procedure (any status other than EO) is read-only. Its buttons are disabled, and its tooltip says "From a completed procedure on <date>; edit it in the Chart."
- **Saving** (`GetActions`, `ApplyActions`). A date in an empty box creates an EO procedure with the first code of the category's preference, plus an InsHist claimproc (the 24.3 `Procedures.InsertOrUpdateInsHistProcedure`). A changed EO date moves the procedure and its claimprocs. A cleared EO box deletes the procedure through `Procedures.Delete`. That call refuses a procedure that is attached to a claim, a payment or an adjustment, so completed procedures, claims and adjustments are never touched. A failed delete is reported instead of being swallowed, as 24.3 did. `Recalls.Synch` runs once after any change, so removing or moving a date recalculates the recall, as adding one already did.
- **Checks.** The two dates of a category may not be on the same day. Only categories the user changed are checked, so history that already holds two procedures on one day does not block the window. Removals are confirmed once, listing each date.
- **Permissions and log.** Remove needs ProcDelete, and Edit (or typing over an EO date) needs ProcExistingEdit. Both are checked with `Procedures.GetDateForPermCheck` (the entry date for EO), so the permissions' date limits apply. Each is checked when the button is clicked and again for every action when you save. Edits are logged under ProcExistingEdit and removals under ProcDelete, for example "Insurance History, Prophy: Z1110 date changed from 9/15/2025 to 10/2/2025". A new date needs no permission and writes no log entry, as in 24.3.
- **Family module.** `ControlFamily` has a one-line change: the history cell shows `InsHistDates.GetDatesText`, for example "3/1/2026, 9/15/2025". "No History" is unchanged.
- **271 import** (`EB271.SetInsuranceHistoryDates`): unchanged. It still updates only the most recent date (the spec says to keep it this way).
- **INFERRED:**
  - The spec says both "the Previous date must be older than the Most Recent" and "re-sort on save". The fork does not refuse two dates entered out of order. It saves them, and the window and the grid show them newest first when they load again. That way a date newer than a read-only completed date can be entered in the empty Previous box.
  - 25.1 lets the user type a newer date over a completed date to create a new EO procedure. 25.2 makes completed dates read-only, and the fork follows 25.2. So a category whose two dates are both completed procedures can't take a newer EO date in this window. Owner question 1.
  - A date that is saved but not shown, because two more recent procedures are on file, gets a note after saving. This is the spec's "a note explains why" for the case where a more recent completed procedure keeps its place.
  - When a completed and an EO procedure fall on the same day, the completed one is listed first.
  - A procedure with no valid date (year before 1880) shows as a blank box, as 24.3 showed "No History". Leaving the box blank does not delete it.
  - Remove clears the box and the delete happens on Save (with the confirmation), not at once. This keeps the 24.3 save-or-close behaviour of the window.

## v25_2-family-edit-benefit-ui

The rules are in the new class `BenefitEditRules` (`OpenDentBusiness/Logic/BenefitEditRules.cs`). The window keeps every control name, so code written against the old names still compiles. The six list boxes (`listCategory`, `listBenefitType`, `listTimePeriod`, `listQuantityQualifier`, `listCoverageLevel`, `listTreatArea`) are now `OpenDental.UI.ComboBox` controls with the same names. The new logic is in a new partial file, `OpenDental/Forms/FormBenefitEdit.Redesign.cs`. `FormBenefitEdit.cs` gets four hooks: a `LoadRedesign()` call at the end of Load, one line in the type-changed handler, the Applies to read in Save, and the ignored-field check after `Benefits.GetValidationErrors`.

- **Applies to:** Category (with "None" for a general benefit), Procedure code, or Code group. Only the picked input is enabled and saved. Picking Procedure code or Code group without filling it in is refused. On load, the window picks the radio button from the benefit: code first, then code group, then category.
- **Inputs by type** (`IsFieldUsed`). Percent is used by coinsurance. Amount is used by limitations, deductibles and copayments (the types that 24.3's checks allow an amount for). Quantity and qualifier are used by limitations, waiting periods and copayments (per visit). Treatment area is used by limitations, and Teeth by exclusions (A-INS's existing rule). Time period and coverage level are always enabled: plan imports set a time period on coinsurance, and X12 sends both with any benefit. An input that the type doesn't use is disabled, unless it already holds a value. Then it stays enabled so the user can see it and clear it.
- **Preview line** at the bottom, built from the inputs as they change, for example "Limitation: 2 per Calendar Year, Individual, Category: Preventive". Quantities read "through age 18", "every 5 years", "6 months" (waiting period), "2 in the Last 12 Months". "General" means no category, code or code group. "Patient Override" is added when that box is checked.
- **Checks.** `Benefits.GetValidationErrors` (24.3 and A-INS) stays as the backstop. `GetIgnoredFieldErrors` adds one message per field the type ignores, naming the field, for example "Quantity is not used by Exclusion benefits. Clear it, or pick another type."
- **INFERRED:** the ignored-field check skips a value the benefit already had when the window opened, if the type is unchanged. So benefits saved by older versions or imported from a 271, such as 24.3's exclusions with a 0 percent, still save unchanged. A new value, or any value after the type was changed, is checked.
- The 25.3 Teeth field (A-INS) is kept as it was, and the 25.3 Other Benefits grid is untouched. `FormInsBenefits.cs` needed no change.

## v25_3-family-insplans-innet

- `InsPlans.GetBigList` selects `insplan.PlanType` and returns two new columns: `PlanType`, and `InNet` ("X" or blank). The new `InsPlans.IsInNetworkPlanType` is true for "p" (PPO percentage and PPO fixed benefit), "f" (Medicaid or flat copay) and "c" (capitation), and false for blank (category percentage). The other callers of `GetBigList` (`FormInsPlansOverrides`, `FrmInsPlanSelect`) are unaffected.
- `FormInsPlans` adds an "In Net" column after "Subs", and turns on `AllowSortingByColumn` because the spec says the column is sortable. Sorting reorders the grid rows, so each row now keeps its `DataRow` in `Tag`. Double-click, Combine and the re-selection after a refresh read the plan from the row instead of indexing into `_table`. Without this, sorting would make those actions act on the wrong plan.
- **INFERRED:** every column can be sorted, not only In Net. Sorting is plain text, so Subs sorts as text.

## v25_3-family-bluebook-providerfee

- `InsBlueBookRuleType.ProviderFee = 5` with the description "Provider Fee" was already done by M1-02.
- This package changes the wording:
  - Blue Book rules window: grid cell "Provider fee", label "Provider Fee Percent", and the message "Please enter a Provider Fee Percent between 0 and 100".
  - Estimate log (`BlueBookEstimateData.GetAllowed`): "Rule Type: Provider Fee" and "(80% of provider fee)".
- The calculation is unchanged: the provider's fee times the percentage, with no Blue Book history read.
- The preference keeps its database name `InsBlueBookUcrFeePercent`. The VERIFY item (did 25.3.48 or only 25.4 rename the preference row?) can't be settled from public sources, because the database docs don't list preference rows. Keeping the name is safe either way. Owner question 2.

## Files touched

- New:
  - `OpenDentBusiness/Data Interface/InsHistDates.cs`
  - `OpenDentBusiness/Logic/BenefitEditRules.cs`
  - `OpenDental/Forms/FormBenefitEdit.Redesign.cs`
  - `tests/BINSHTests/*`
  - this note
- Rewritten (owned): `OpenDental/Forms/FormInsHistSetup.cs`, `FormInsHistSetup.designer.cs`, `FormBenefitEdit.designer.cs`.
- Edited (owned):
  - `OpenDental/Forms/FormBenefitEdit.cs` (hooks)
  - `FormInsPlans.cs`, `FormInsPlans.designer.cs`
  - `FormInsBlueBookRules.cs`, `FormInsBlueBookRules.designer.cs` (strings)
  - `OpenDental/Main Modules/ControlFamily.cs` (one line)
  - `OpenDentBusiness/Data Interface/InsPlans.cs`
  - `OpenDentBusiness/Data Interface/InsBlueBooks.cs` (two strings)
- Shared project files, one entry each:
  - `OpenDentBusiness/OpenDentBusiness.csproj` (two compile items)
  - `OpenDental/OpenDental.csproj` (one compile item)
  - `OpenDental.sln` and `OpenDental.Local.slnf` (the test project)
- No foundation-owned registry (TableTypes, Crud, Pref.cs, PrefInfos.xml, permissions, enums, seed rows) was changed. There is no schema change and no fork migration step. The level range 2027002001-2027002099 is unused.

## Interplay with packages built in parallel

- **M2-HIST** (26.1: Insurance History categories from code groups) owns the same `FormInsHistSetup`, `ControlFamily`, `EB271`, `Procedures` and `Prefs`.
  - Expect a conflict in `FormInsHistSetup.*`, where both packages rewrite the window. The rows here are built in a loop from a category list (`AddCategoryRows`, `CategoryRow`), so moving to code-group rows means replacing `Prefs.GetInsHistPrefNames()` and the code lookup. The two-date, edit and remove rules in `InsHistDates` take the procedures and code list as parameters and work for any category.
  - In `ControlFamily` the conflict is one line in the history rows.
  - Neither `Procedures.cs` nor `EB271.cs` is edited here.
  - Where M2-HIST adds a quadrant code group, `InsHistDates.GetQuadrant` must follow it.
- **M2-EST** (26.1 age limits by teeth, 25.4 tooth exclusions) also edits `FormBenefitEdit` and `FormInsBenefits`.
  - Control names are unchanged, and `SetTeethEnabled` and the teeth parse in `butSave_Click` are untouched, so M2-EST's change to them should merge or resolve with little work.
  - The list boxes are now combos. `GetSelected`, `SetSelectedEnum`, `SelectedIndex`, `Items.AddEnums` and `Items.AddList` behave the same; `Items.RemoveAt` doesn't exist on the combo.
  - If M2-EST lets age limits use teeth, `BenefitEditRules.IsFieldUsed(...,Teeth)` should also return true for an age limit, so the preview and the ignored-field check agree.
  - Designer conflicts are likely: the hint label text and the positions.
- **M2-TERM** (25.4 "Standard Provider Fee" terminology) edits the same strings in `FormInsBlueBookRules` and `InsBlueBooks`. Take M2-TERM's wording at merge. The BINSH test only checks that the log says "Provider Fee", "provider fee)" and no "UCR", so "Standard Provider Fee" still passes. The label text changed here is one line in `FormInsBlueBookRules.designer.cs`.
- **B-SHELL** (main window) also owns `ControlFamily.cs`. The one-line change here doesn't touch its areas.
- **M2-INSPLAN / V-BRIDGE / M2-HIST** edit `FormInsPlan`. Its Hist button still opens `FormInsHistSetup(patNum,insSub)`, which keeps its signature.

## Tests

`tests/BINSHTests` (MSTest v1, x86, .NET 4.8) has 36 tests:

- Rule tests with no database: `InsHistDatesTests`, `BenefitEditRulesTests`, the pure parts of `InsPlanListTests` and `BlueBookProviderFeeTests`.
- Database tests on a per-run scratch database (`forkmig_<run>_binsh`, fresh 24.3.63 database upgraded by the fork migration runner, dropped afterwards):
  - Insurance History: insert, change, remove, completed procedures left alone, the security log, perio quadrants, an invalid code, hidden-date notes, one subscriber's history only, and recall recalculation.
  - `GetBigList` In Net.
  - The Blue Book provider-fee estimate and its log text.

Verified run: `run-tests.ps1 -Mode Targeted -Include BINSHTests,InsuranceTests`, giving NoHqTests 49/49, BINSHTests 36/36 and InsuranceTests 49/49.

Untested (no test project drives the WinForms windows, and the GUI is not launched in package work):

- the window layouts of Insurance History and Edit Benefit, and their control wiring (enable and disable, preview refresh, right-click menu, tooltip, date picker);
- In Net sorting and the row-tag selection in `FormInsPlans`;
- the Family grid cell.

## Owner questions

1. Insurance History: when both dates of a category are completed procedures, should the window still allow a newer EO date (25.1's "a newer date creates a new EO"), for example through a third "Add date" action? The fork follows 25.2 (completed dates are read-only) and allows nothing in that case.
2. Blue Book: does the office's 25.3.48 database still have the preference row `InsBlueBookUcrFeePercent`, or a renamed row? The fork reads `InsBlueBookUcrFeePercent`.
3. Edit Benefit: confirm the inputs per type in practice, especially quantity and qualifier on copayments (per visit), and amount on copayments. The fork allows both, because 24.3's checks did.
