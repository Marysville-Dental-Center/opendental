# Port note: M2-SDV, Service Date View (25.4 and 26.1)

Branch `port/m2-sdv`, cut from `port/foundation` (M1 plus M2-01 and M2-02) in the single-wave run. Backlog section: [port-backlog.md, M2-SDV](../specs/port-backlog.md#m2-sdv-service-date-view-254-and-261). Feature specs: [features-25.4.md](../specs/features-25.4.md#v25_4-account-service-date-view-service-date-view-easier-to-reach-and-use) and [features-26.1.md](../specs/features-26.1.md#v26_1-account-service-date-view-service-date-view-overhaul-plus-statements-in-service-date-view). No fork migration step: the schema (`statement.IsServiceDateView`, M2-01), the preference `BillingDefaultServiceDateView` and the Display Fields lists of category 22 (M2-02) were already on the base. The level range 2027002801 to 2027002899 is unused.

Sources: Open Dental's public release notes for 25.4 and 26.1 and the 24.3 manual page for Service Date View, as cited in the specs, and the published database documents for the column. No Open Dental binary or install was used; no office data. Where the 26.1 behaviour is not public the choice is marked INFERRED below.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-account-service-date-view` | built | The Service Date View window is modeless, follows the Account module's patient and Family/Patient choice and refreshes in place, remembers "Show detailed view" per user, and takes its columns (order, width, description) from Display Fields, Account Module Service Date View (category 22, Clinic optional). Print and Save to Images use the chosen columns. A "Setup Columns" button opens Display Fields for category 22 (Setup permission). Totals stay bold; tooth numbers are converted to the office's nomenclature in the UI only. Reachable from the Statement drop-down ("Service Date View Window") and the account grid's right-click menu. The Main-tab button now switches the grid (26.1, below). |
| `v26_1-account-service-date-view` | built | Account module: the main grid switches between the regular view and the service-date view (Main tab button, right-click "Service Date View"), remembered per user; a "Service Date View details" box (Show tab, visible in that view) adds the insurance estimate and patient portion on every procedure. Double-click opens the row's object. Statements: "Service Date View" box in Statement Options (single and bulk edit) and Billing Options (default from `BillingDefaultServiceDateView`, saved with Save Defaults), stored in `statement.IsServiceDateView`; the main grid of printed, previewed, PDF, e-mailed, archived (Imaging) and CSV-exported statements is then built in service-date layout. Receipts, invoices and date ranges are supported; limited, limited custom and super-family statements keep the regular view. |

## What was built

### One business method for all three views

- `OpenDentBusiness/Reports/ServiceDateViews.cs` (new): `ServiceDateViews.Build(coreRows, options)` builds the rows in display order: per date of service, the procedures with the transactions attached to them indented beneath, then the date's unattached transactions under "Unallocated Trans", then "Total for Date"; "Family Total:" or "Patient Total:" at the end. It is pure (no database or cache), so the window, the Account grid and statements show the same rows from the same query rows. Options: family/patient, detailed, a date-of-service range with a balance forward, receipt, invoice items. Also the row classes, the reader of the query table, and `ToReportTable` (the 24.3 report's DataTable).
- `OpenDentBusiness/Reports/RpServiceDateView.cs` (owned): the 24.3 core query is unchanged except eight columns added to every branch (ClinicNum, ProvNum, ParentNum = PayNum/ClaimNum/PayPlanNum, CodeNum, ProcCode, ToothNum, ToothRange, Surf). The 24.3 display SQL (grouping, totals, ordering, FORMAT) is replaced by `ServiceDateViews.Build`. `GetData(patNum,isFamily,isDetailed)` keeps its signature and columns; new `GetRows` and `GetCoreTable` (middle-tier aware).
- `ServiceDateViewUserPrefs` (same file): per-user settings in `userodpref`, see "Per-user settings".

### Statements

- `OpenDentBusiness/Data Interface/Statements.ServiceDateView.cs` (new partial of `Statements`): `CanUseServiceDateView`, `IsShownInServiceDateView`, `SetServiceDateView`, `GetBillingDefaultServiceDateView`, `GetServiceDateViewMainTable`.
- `OpenDentBusiness/SheetFramework/SheetDataTableUtil.cs` (owned): one hook at the top of `GetTable_StatementMain`. Every statement output (sheet print and preview, PDF for Imaging and e-mail, the CSV export, billing batches through `BillingL`) gets its main grid from there, so all of them follow the stored flag, including a saved statement printed again.
- The service-date table has the columns of the DataSet's own account tables (so the Statement Main Grid display fields, the CSV writer and the sheet code read it unchanged) plus `paramIsBold` (totals; the sheet grid already draws that column bold). One block per account table: `account` is the single patient or the intermingled family, `account<PatNum>` one patient of a statement that is not intermingled, and every row of a patient's block carries that PatNum so the sheet splits the grid per patient as usual. A block with nothing to show gets the regular "No Account Activity" row. The result is kept in the DataSet (table `servicedateview`, keyed by the statement's options) because the sheet code asks for the grid on every redraw.
- The DataSet itself is not changed: aging, balance totals, amount due, insurance estimate, the payment plan grids and the dunning text come from the same account tables in both views (tested).

### Account module (`OpenDental/Main Modules/ControlAccount.ServiceDateView.cs`, new partial)

- A second grid (`gridServiceDateView`) lies over `gridAccount` on the same tab page; only one is visible. Columns from Display Fields category 22. The module's date range applies (services within the range, earlier services in a balance forward row). Family/Patient choice as in the regular grid.
- Selecting rows in the service-date grid selects the matching rows of the hidden regular grid (procedure, adjustment, payment, claim, payment plan charge), so the toolbar and right-click actions that read `gridAccount`'s selection (new claim, adjustment, invoice, refund, sales tax...) work unchanged. Double-click runs the regular grid's double-click on the matching row; a row with no match (for example a credit dated after the date range) opens its object directly.
- Hooks in `ControlAccount.cs` (owned), all one line: constructor, `InitializeOnStartup`, end of `FillMain`, `butServiceDateView_Click` (now toggles the view), `PrintStatement`, More Options and Invoice (new statements start in the grid's view), and the two reads of the "Show Proc Breakdowns" setting (now Fkey 0 only).

### Windows

- `FormRpServiceDateView` (owned, and designer): see the 25.4 row. `PatNum`/`IsFamily` are now properties with private setters; `SetPatient` refreshes in place. `ServiceDateViewGrid` (same file) builds the columns and cells for the window and the Account grid.
- `FormStatementOptions` (owned, and designer): "Service Date View" box below Receipt; disabled and cleared for limited, limited custom and super-family statements and once a statement is sent; tri-state in bulk edit; saved through `Statements.SetServiceDateView`.
- `FormBillingOptions` (owned, and designer): "Service Date View" box beside "Intermingle family members"; default from `BillingDefaultServiceDateView`; Save Defaults stores it (practice-wide); every statement Billing creates gets it (never on super-family statements).
- `FormDisplayFieldCategories`, `FormDisplayFields` (owned): no change needed. The categories window lists every `DisplayFieldCategory` with its description, so "Account Module Service Date View" is already there (test).

## Per-user settings

`UserOdFkeyType` is a registry of Open Dental's published numbers and has no value for these settings, so they are stored under `UserOdFkeyType.AcctProcBreakdown` (13, the Account module's "Show Proc Breakdowns" default) with Fkey 1 ("Show detailed view" of the window and "Service Date View details" of the grid, shared) and Fkey 2 (the Account grid's view). Open Dental and this program keep the proc breakdown setting in the row with Fkey 0; the two places in `ControlAccount.cs` that read that row by type alone now read Fkey 0 only. No registry or enum file was changed.

## INFERRED behaviours

- The Main-tab button becomes the view switch ("Service Date View" / "Transaction View"); the 25.4 window stays, reached from the Statement drop-down and the right-click menu, for printing and saving to Imaging. The grid's view is remembered per user.
- "Show details" in the grid and "Show detailed view" in the window are one per-user setting (default on, as the 24.3 window).
- Grid columns are Display Fields category 22 (M2-02's 24.3-window list) rather than the spec's suggested Date, Patient, Code/Description, Charge, Credits, Balance; InsBal and AcctBal show on procedures with details on, and on totals always, as in 24.3.
- New statements made in the Account module (Statement, Receipt, Walkout, E-mail, More Options, Invoice) start in the view the grid shows.
- Statement layout: procedures and day totals show the date of service, credits their own date; the Balance column shows each procedure's remaining balance (charge less every credit applied, before insurance estimates), each day's net and, on the final total, the ending balance; descriptions of procedures are the regular statement's, other rows keep the report's text; totals bold.
- Date range (statements and the Account grid): services dated within the range, each with all its credits whatever their dates; the services before the range summed into one "Balance Forward" row; later services left out.
- Receipts: the patient payments dated within the statement's range, each under the procedure it pays (whatever the procedure's date), unattached payments on their own date; the procedure's Balance shows what is still owed on it after every credit. No balance forward.
- Invoices: the invoice's own procedures, adjustments and payment plan charges (as attached from the selected Account rows, or auto-selected as before), each with the credits applied to it. No date range.
- Canadian receipts blank the procedure description, code and tooth, as the regular view does.
- Dynamic payment plan credits are grouped by date. The 24.3 query grouped them by the link's date and time, which split one day's credits into several "dates" with the same date shown.
- The Billing default is saved with Save Defaults only when no single clinic is selected (it is a practice preference).

## Deviations from the spec

- "Hide payments" among the statement options: `statement.HidePayment` hides the payment options section of the sheet in both views; it hides no grid rows, so the builder has no such option.
- The spec's 26.1 column list for the grid (see INFERRED) and the window kept as a second way to see the view.
- Electronic statements (EHG, ClaimX XML) are built from the account tables, not from the sheet grid, and stay in the regular layout.
- `AccountModules.cs`, `SheetFiller.cs`, `SheetUtil.cs`, `FormDisplayFieldCategories.cs`, `FormDisplayFields.cs` are in the owned list but needed no change.

## Files touched

- New: `OpenDentBusiness/Reports/ServiceDateViews.cs`, `OpenDentBusiness/Data Interface/Statements.ServiceDateView.cs`, `OpenDental/Main Modules/ControlAccount.ServiceDateView.cs`, `tests/M2SDVTests/*`, this note.
- Changed (owned): `OpenDentBusiness/Reports/RpServiceDateView.cs`, `OpenDentBusiness/SheetFramework/SheetDataTableUtil.cs` (3 lines), `OpenDental/Main Modules/ControlAccount.cs` (hooks, see above), `OpenDental/Forms Reports/FormRpServiceDateView.cs` and designer, `OpenDental/Forms/FormStatementOptions.cs` and designer, `OpenDental/Forms/FormBillingOptions.cs` and designer.
- Changed (shared, one entry each): `OpenDentBusiness/OpenDentBusiness.csproj` (2 Compile items), `OpenDental/OpenDental.csproj` (1 Compile item), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Registries, enum files, TableTypes, CRUD, preferences, seed rows: not changed.

## Interplay with packages built at the same time

- **B-ACCT, B-CLAIM, B-SHELL** (also edit `ControlAccount.cs`): keep the one-line hooks listed above when merging. A new action that reads `gridAccount.SelectedIndices` works in service-date view through the selection mirror. Code that reads the `AcctProcBreakdown` userodpref must read the Fkey 0 row (`ServiceDateViewUserPrefs.GetProcBreakdownPref`).
- **B-PRINT** (`Statements.cs`, sheet printing): this package adds a separate partial file and one hook in `SheetDataTableUtil.GetTable_StatementMain`; if B-PRINT also changes that method, keep the hook as its first statement.
- **M2-SHEETS** (`SheetPrinting.cs`, `FormSheetFillEdit.cs`): statement redraws keep calling `GetTable_StatementMain`; the service-date grid is cached in the DataSet, so no change is needed there.
- **M2-DUN** (C2, depends on M2-SDV): e-mailed statements already use the sheet PDF and so the stored flag. If the HTML e-mail lists transactions, take them from `Statements.GetServiceDateViewMainTable` when `Statements.IsShownInServiceDateView(statement)` so the e-mail matches the PDF.
- **M2-01 / M2-02**: uses `statement.IsServiceDateView`, `PrefName.BillingDefaultServiceDateView` and `DisplayFields26_1` (category 22 lists) as they are.
- **B-ERA**: `FormDisplayFieldCategories.cs` is unchanged here, so B-ERA's handling of category 23 merges without conflict.

## Tests

New project `tests/M2SDVTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 43 tests:

- `ServiceDateViewBuilderTests` (no database): order (procedure, its credits by date, patients by first name, higher procedure number first), unallocated heading, day and overall totals, detailed balances, grouping, date range with balance forward, receipts, invoices, the report table's blanks, the query-table reader, statement-type rules.
- `ServiceDateViewDbTests` (scratch database `m2sdv_<time>_<pid>_<random>_sdv` from the 24.3.63 seed and the fork migrations, dropped afterwards; synthetic family): `RpServiceDateView.GetData` matches the 24.3 display query (kept in `LegacyServiceDateViewReport.cs` as the reference, around the same core query) row by row for the family and each patient, detailed or not, and under PayPlansVersion 1 to 4; the fork columns; statements in both views (intermingled, per patient, single patient, date range, receipt, invoice, limited and super family, saved flag on reprint, totals and aging unchanged, full sheet fill and PDF render); the Billing default; per-user settings beside the proc breakdown row; window and grid cells (columns from display fields, amounts, tooth nomenclature, bold totals).
- `ServiceDateViewUiSourceTests`: the Windows Forms hooks stay in place (source checks; no window is shown).

Checks on the finished branch: build of `OpenDental.Local.slnf` (Debug, x86, full rebuild) 0 errors and 376 warnings (the baseline); targeted tests `M2SDVTests` 43/43, `StatementTests` 8/8, `ReportTests` 28/28, `SheetTests` 31/31, `M202Tests` 39/39, `NoHqTests` 49/49; `tools/Test-NoHqHosts.ps1` passed.

## Not tested

- The windows were not run (OpenDental.exe is not launched in this run): the Account module switch, the selection mirror, double-click, the details box, the right-click and Statement menu items, the window following the patient, and the new boxes in Statement Options and Billing Options are covered by source checks and by the business-layer tests behind them only.
- Middle tier (`GetCoreTable` is middle-tier aware, as the 24.3 `GetData` was), printing to a physical printer, e-mail sending, and electronic billing.

## Questions for the owner

1. New statements made in the Account module start in the grid's view. Keep that, or always start in the regular view unless the box is checked in Statement Options?
2. Statement descriptions of credits keep the report's text ("PatPay- Check", "InsPay- ...", "W/O- ..."). Should statements use friendlier wording for patients?
3. Display Fields category 22 has no plain "Balance" (charge less credits) column, only AcctBal (patient portion, with details). Add one? That is a registry change for M2-02 (`Registry26_1.cs` and its test).
4. Date ranges filter by date of service with a balance forward (credits travel with their service). Is that the wanted meaning, or should a statement list the transactions dated in the range as the regular view does?
5. The per-user settings use `UserOdFkeyType.AcctProcBreakdown` with Fkey 1 and 2. Acceptable, or should the registry owner add fork-only `UserOdFkeyType` values (and their tests)?
6. Should the Billing default also be kept per clinic, like the other Billing Options defaults?
7. Electronic statements stay in the regular layout. Acceptable?
