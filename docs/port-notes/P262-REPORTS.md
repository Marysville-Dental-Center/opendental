# Port note: P262-REPORTS, Reports (Open Dental 26.2 items 31 to 34)

Branch `p262/reports-1`, cut from `p262/schema-1` (39be2b04b). One of the 26.2 feature packages the owner chose on 2026-11-07.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html), section Reports. The four items are one line each, with no link:
  - item 31: "Redesigned layout of Report Display Settings";
  - item 32: "Redesigned layout of Report Security Permissions";
  - item 33: "New option to show prior uncollected balances on the Patient Portion Uncollected report";
  - item 34: "Pay Plan column added to Patient Portion Uncollected report".
- The public online manual pages of the windows involved: Reports Setup (`manual/reportssetup.html`), Report Setup: Display Settings
  (`manual/reportdisplaysettings.html`), Report Setup: Security Permissions (`manual/reportsecurity.html`) and Patient Portion Uncollected
  Report (`manual/reportdailypatportuncollected.html`). The public manual is headed v24.4 and describes the windows before 26.2 (a grid per
  category; the report's columns Date, Patient Name, Procedure, Fee, Patient, Adjustment, Patient Paid, Uncollected; "Treatment credits from
  Payment Plans are excluded" from Patient Paid). It says nothing of the 26.2 layouts, the option or the column. The versioned help pages
  (`help262/...`, `help254/...`) answer only with an access notice for customers, so they were not used.
- Open Dental's 26.2 database documents (`OpenDentalDiffDocumentation26-2.xml`, `OpenDentalDocumentation26-2.xml`): the diff document has
  no table, column or enum value for these items (no `displayreport`, `grouppermission`, `payplan`, `payplancharge`, `payplanlink` or
  `userodpref` change, no preference). The full document's `payplanlink` (AmountOverride: "Optional override if full amount of object is
  not desired") and `payplancharge` (ChargeType 1, Production, the credit of a payment plan that is not dynamic) are what the Pay Plan
  column reads; both are unchanged since 26.1.

Everything about the windows beyond the four lines above is this program's own design and is marked INFERRED below.

## What was built

### Items 31 and 32: the Display Settings and Security Permissions tabs of Report Setup

- Both tabs (one user control, `UserControlReportSetup`, also used by the report permissions of the Central Enterprise Management Tool)
  show **one list of every report**, with a shaded row per category (Production & Income, Daily, Monthly, Lists, Public Health: the titles
  of the 26.1 grids) and the category's reports under it in the order of the Reports window. Before 26.2 each category had its own small
  grid with its own scroll bar. The internal name of each report is a column (**Internal Name**) instead of the label under the grids that
  showed it for the selected report only.
- **Display Settings**: columns Display Name (click to type a new name), Internal Name, Favorite and Hidden (click to toggle); Up and Down
  move the selected report within its category; a line at the top says so.
- **Security Permissions**: User Group, Set All, Set None and a count of the reports the group may run ("Reports allowed: 12 / 70") at the
  top; columns Report (a hidden report is marked "(hidden)", as before), Internal Name and Allowed (click to toggle).
- Every setting of 26.1 is kept, with its rules: a favorite is never hidden and a hidden report never a favorite; a group with every report
  has one Reports permission with FKey 0; denying one report of such a group lists every other report (the Arizona Primary Care reports
  only when the office uses them); allowing the last report a group lacks turns its permissions back into FKey 0. Arizona Primary Care is
  not listed, as in 26.1. The tab, window and control sizes are unchanged, so Report Setup and the CEMT window need no change of layout.
- The rules moved out of the window into the business layer, where they are tested: `ReportSetupLayout` (rows, category titles, Up and
  Down, Favorite and Hidden) and `ReportPermissionEdit` (Allowed, Set All, Set None, the count).
- Two corrections on the way:
  - Up and Down find the neighbouring report by its place in the category and then number the category's reports 0, 1, 2 ... The 26.1 code
    looked reports up by `ItemOrder == row`, which picked the wrong report (or none) when a category's orders had a gap or a duplicate.
  - "Every report allowed" is decided by comparing the reports with the group's permissions; 26.1 compared their counts, so a leftover
    permission of a report no longer listed could stand in for a missing one and give the group every report (tested).
- Report Setup's Save takes a display name that is still being typed (`CommitPendingEdit`), and switching tabs keeps it too.

### Item 33: prior uncollected balances on the Patient Portion Uncollected report

- A check box **Show prior uncollected balances** under the dates. Checked (and Refresh), the report also lists, for every patient who has
  a procedure completed in the date range (in the selected clinics), that patient's procedures completed **before the From date** whose
  patient portion is not collected, computed like the other rows. They are shaded, and a line under the list says what the shaded rows are.
  A patient whose procedures of the date range are all paid still gets the prior rows.
- The prior rows come first (they are older); the rows of the date range follow, the same as without the option. The grid can still be
  sorted by any column.
- A prior procedure counts the adjustments attached to it dated up to the To date; a procedure of the date range, as before, the
  adjustments dated in the date range. Payments attached to the procedure count whatever their date, as before. The clinic filter applies to
  the prior procedures too.
- Print: the subtitle "Includes prior uncollected balances", the procedures of the date range under "Patient Portion Uncollected" and the
  prior procedures under "Prior Uncollected Balances", each with its own totals. A section without rows is left out.
- `RpPatPortionUncollected.GetPatUncollected` takes `doShowPrior` (default false, so other callers and the middle tier call as before) and
  returns an `IsPrior` column (1 on a prior row).

### Item 34: the Pay Plan column

- A **Pay Plan** column after Uncollected, on screen and in print: the amount of the procedure put on a payment plan.
  - Patient payment plans: the sum of the plan's treatment credits attached to the procedure (`payplancharge` rows with ChargeType Credit
    and the procedure's ProcNum, on a plan that is not an insurance payment plan, `payplan.PlanNum` 0).
  - Dynamic payment plans: the link's amount override (`payplanlink`, LinkType Procedure); a link without an override puts the whole
    remaining value of the procedure on the plan, which the column shows as the procedure's fee less insurance, plus adjustments, less the
    payments made outside a payment plan (never below zero).
  - Insurance payment plans and charges due (ChargeType Debit) do not count. A procedure on no plan shows 0.
- The column does not change the Uncollected column or which procedures are listed: payments made through a payment plan and attached to
  the procedure count in Patient Paid, as before. It shows on prior rows too.

### Migration

None. The four items need no table, column, enum value, preference or row: levels 2026110761 to 2026110769 stay unused (a test checks it).
Nothing these items need was missing from `p262/schema-1`.

## INFERRED

Each point is this program's own choice; the public sources state none of them.

- Items 31 and 32: the whole layout (one list with category rows instead of a grid per category; the category titles; the Internal Name
  column instead of the label; the instruction line; the count of allowed reports; the column widths), and that "every setting kept" means
  the 26.1 settings and rules above, unchanged except for the two corrections. That the two items are one control serving both tabs, as in
  26.1. That Arizona Primary Care stays unlisted.
- Item 33: what "prior uncollected balances" are: the uncollected procedures completed before the From date of the patients who have a
  procedure completed in the date range. The other reading, every uncollected procedure before the From date of every patient, is what
  running the report from an earlier From date already gives, so it was not taken. Also: the check box's text (the release note's words),
  that it starts unchecked and is not remembered (no preference), the clinic filter on prior procedures, the adjustments dated up to the To
  date, the order (prior rows first), the shading and its line, and the separate print section with its own totals.
- Item 34: what the column holds (the amount on a payment plan, as the manual's note that Patient Paid excludes payment plan credits
  suggests, rather than the payments made through a plan), its place after Uncollected, that it does not reduce Uncollected, the value of a
  dynamic payment plan link without an override, that insurance payment plans are left out and closed plans are not, and the rounding to
  cents. The print column's width.

## Observation (26.1 behaviour, not changed)

- The report's adjustment subquery joins `paysplit` on `AdjNum` before it sums `adjustment.AdjAmt`, so an adjustment with two or more
  payment splits attached counts its amount once per split. The rows of the date range keep this 26.1 behaviour for parity; the prior rows
  use the same subquery (owner question 5).

## Owner questions

1. Report Setup: is one list with category rows, the internal name as a column and the count of allowed reports the redesign wanted? Should
   the list also have a search box, or a way to allow or deny a whole category at once?
2. Item 33: are the prior balances those of the patients seen in the date range (built), or every uncollected procedure before the From
   date?
3. Item 33: should the check box be remembered (per user, or as a Report Setup default), instead of starting unchecked?
4. Item 34: should the Pay Plan column show the amount put on payment plans (built) or the payments received through them? Should a
   procedure on a payment plan be left out of the report, or its Uncollected reduced by the Pay Plan amount?
5. The 26.1 adjustment subquery counts an adjustment once per payment split attached to it (above). Fix it here (the totals of some rows
   would change), or keep parity?

## Tests

New test assembly `tests/P262ReportsTests` (in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `PatPortionUncollectedDbTests` (scratch database `test_p262reports_*` from `db/init/new-database.sql` and the fork migrations, dropped
  afterwards; synthetic rows): the rows of the date range as before 26.2 (fee, patient portion, adjustments of the range only, payments,
  uncollected), and the same with the option; the prior rows of patients seen in the range only (a patient seen only after the range or
  not at all gets none), their adjustments up to the To date, paid prior procedures left out, the order, the clinic filter; the Pay Plan
  column for patient payment plan credits (insurance payment plans and charges due left out, plan payments in Patient Paid, Uncollected
  unchanged), dynamic payment plan links with and without an override, and on prior rows.
- `ReportSetupLayoutTests` (no database): the categories and their titles, the rows and their order (hidden reports included, Arizona
  Primary Care not, an empty category keeps its row), Up and Down within a category with a gap in the orders, Favorite and Hidden, and the
  Allowed cell, Set All and Set None (FKey 0, Arizona Primary Care in use or not, the leftover permission case).
- `WindowSourceTests`: the one list and the controls kept, every setting and the public members the CEMT window uses, Save taking the name
  being typed, the option, the column and the print sections of the report window, and no fork migration step at 2026110761 to 2026110769.

`ReportTests` (`ReportSetup_NamesTheNewReports` reads `GetInternalName` of the control) and `M2UDSTests` (`ReportSetup_NamesTheReport`) still
pass unchanged: `GetInternalName` stays in the control.

## Checks (2026-11-07)

- Full build of `OpenDental.Local.slnf` (Debug, x86): 0 errors (`OpenPmsDental`, `xCentralManager` and the test projects included).
- Targeted run, with `NoHqTests` 58/58: `P262ReportsTests` 22/22 (the database tests ran on a scratch database, none inconclusive),
  `ReportTests` 46/46, `M2UDSTests` 35/35 and `BSERVERTests` 55/55 (both read the changed Report Setup source). `ForkMigrationTests`,
  `ReleaseFlowTests` and `InitSqlTests` were not run (the integration runs them; this package adds no step).
- Scratch databases `test_p262reports_*` (and the `rpttest_`/`forkmig_` ones of `ReportTests`) are dropped after the runs.
- No window was opened: the layouts were checked from their source only (no screen in this run). The new layouts need a look in the program.

## Files touched

- New: `OpenPmsDentalBusiness/Logic/ReportSetupLayout.cs`, `OpenPmsDentalBusiness/Logic/ReportPermissionEdit.cs`, `tests/P262ReportsTests/*`,
  this note.
- Changed: `OpenPmsDental/User Controls/UserControlReportSetup.cs` and `.Designer.cs` (the one list), `OpenPmsDental/Forms/FormReportSetup.cs`
  (Save takes the name being typed), `OpenPmsDentalBusiness/Reports/RpPatPortionUncollected.cs` (prior rows, Pay Plan and IsPrior columns),
  `OpenPmsDental/Forms Reports/FormRpPatPortionUncollected.cs` and `.designer.cs` (the option, the column, the shading, the print sections;
  the window is wider), `OpenPmsDentalBusiness/OpenPmsDentalBusiness.csproj`, `OpenDental.sln`, `OpenDental.Local.slnf`,
  `docs/release-notes/next.md`.
- Not changed: `CentralManager/FormCentralReportSetup.*` (the control keeps its public members and size), the schema, the fork migrations.
