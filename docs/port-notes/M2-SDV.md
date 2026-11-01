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
- `OpenDentBusiness/Reports/RpServiceDateView.cs` (owned): the 24.3 core query is unchanged except eight columns added to every branch (ClinicNum, ProvNum, ParentNum = PayNum/ClaimNum/PayPlanNum, CodeNum, ProcCode, ToothNum, ToothRange, Surf). The 24.3 display SQL (grouping, totals, ordering, FORMAT) is replaced by `ServiceDateViews.Build`. `GetData(patNum,isFamily,isDetailed)` keeps its signature and columns; new `GetRows` and `GetCoreTable` (middle-tier aware). `GetCoreTable(patNum,isFamily,includePayPlanSplits)`: the patient-payment branch keeps the 24.3 filter (payment plan splits under `PayPlansVersion` 2 only) unless `includePayPlanSplits` is true; `GetRows` sets it for receipts only (see "Receipts" under INFERRED).
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
- `FormDisplayFieldCategories`, `FormDisplayFields` (owned): no change needed. The categories window lists every `DisplayFieldCategory` with its description; since B-ERA it skips a category with no available fields (`DisplayFields.HasAvailableFields`), and category 22 has M2-02's fields, so "Account Module Service Date View" is listed (test). No column of category 22 is required, so B-ERA's `IsRequiredField` needs no entry for it.

## Per-user settings

`UserOdFkeyType` is a registry of Open Dental's published numbers and has no value for these settings, so they are stored under `UserOdFkeyType.AcctProcBreakdown` (13, the Account module's "Show Proc Breakdowns" default) with Fkey 1 ("Show detailed view" of the window and "Service Date View details" of the grid, shared) and Fkey 2 (the Account grid's view). Open Dental and this program keep the proc breakdown setting in the row with Fkey 0; the two places in `ControlAccount.cs` that read that row by type alone now read Fkey 0 only. No registry or enum file was changed.

## INFERRED behaviours

- The Main-tab button becomes the view switch ("Service Date View" / "Transaction View"); the 25.4 window stays, reached from the Statement drop-down and the right-click menu, for printing and saving to Imaging. The grid's view is remembered per user.
- "Show details" in the grid and "Show detailed view" in the window are one per-user setting (default on, as the 24.3 window).
- Grid columns are Display Fields category 22 (M2-02's 24.3-window list) rather than the spec's suggested Date, Patient, Code/Description, Charge, Credits, Balance; InsBal and AcctBal show on procedures with details on, and on totals always, as in 24.3.
- New statements made in the Account module (Statement, Receipt, Walkout, E-mail, More Options, Invoice) start in the view the grid shows.
- Statement layout: procedures and day totals show the date of service, credits their own date; the Balance column shows each procedure's remaining balance (charge less every credit applied, before insurance estimates), each day's net and, on the final total, the ending balance (receipts: see below); descriptions of procedures are the regular statement's, other rows keep the report's text; totals bold.
- Date range (statements and the Account grid): services dated within the range, each with all its credits whatever their dates; the services before the range summed into one "Balance Forward" row; later services left out.
- Receipts: the patient payments dated within the statement's range, each under the procedure it pays (whatever the procedure's date), unattached payments on their own date; the procedure's Balance shows what is still owed on it after every credit. The totals' Balance (and AcctBal) add up the rows shown: each listed procedure's own balance, plus the unattached payments; a payment listed under its procedure is already in that procedure's balance and is not counted again (a payment on a procedure the view does not list, such as one not complete, counts as an unattached one). So a total never leaves out the procedure's credits that are not on the receipt (insurance, write-offs, attached adjustments, earlier payments). No balance forward. Payments made on a payment plan are listed in every `PayPlansVersion` (26.1 rule 3, "all of today's payments"): the receipt reads the core query with `includePayPlanSplits`, while the view itself (window, Account grid, other statements) keeps the 24.3 report's rule of showing those splits under version 2 only. The regular receipt lists them under versions 2 and 4 (`AccountModules` leaves payment plan splits to the payment plan grid in versions 1 and 3).
- Invoices: the invoice's own procedures, adjustments and payment plan charges (as attached from the selected Account rows, or auto-selected as before), each with the credits applied to it. No date range.
- Canadian receipts blank the procedure description, code and tooth, as the regular view does.
- Dynamic payment plan credits are grouped by date. The 24.3 query grouped them by the link's date and time, which split one day's credits into several "dates" with the same date shown.
- The Billing default is saved with Save Defaults only when no single clinic is selected (it is a practice preference).

## Deviations from the spec

- "Hide payments" among the statement options: `statement.HidePayment` hides the payment options section of the sheet in both views; it hides no grid rows, so the builder has no such option.
- The spec's 26.1 column list for the grid (see INFERRED) and the window kept as a second way to see the view.
- Electronic statements (EHG, ClaimX XML) are built from the account tables, not from the sheet grid, and stay in the regular layout.
- `SheetFiller.cs`, `FormDisplayFieldCategories.cs`, `FormDisplayFields.cs` are in the owned list but needed no change. `AccountModules.cs` and `SheetUtil.cs` changed only for the packages merged before (see "Integration on `port/m2-sdv-t3`").

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
- **B-ERA**: `FormDisplayFieldCategories.cs` is unchanged here, so B-ERA's handling of category 23 merges without conflict. Its "has available fields" filter keeps category 22 listed (source test).

## Review fix (branch `port/m2-sdv-t1`)

- Receipts in Service Date View left out payments made on a payment plan whenever `PayPlansVersion` was not 2 (the 24.3 core query's patient-payment branch keeps `paysplit.PayPlanNum=0` in versions 1, 3 and 4), so a receipt for a payment plan installment came out as "No Account Activity". Fixed in `RpServiceDateView.GetRows`/`GetCoreTable` (`includePayPlanSplits` for receipts, not for invoices). The review's statement that the regular receipt has them in every version is only partly right: `AccountModules` reads them in every version but leaves them out of the account rows in versions 1 and 3 (`PayPlanVersions.DoNotAge`, `AgeCreditsOnly`); the new test pins both behaviours.
- Tests: `Build_Receipt_KeepsPaymentPlanPayments` (builder) and `Statement_ServiceDateView_Receipt_PayPlanPaymentsInEveryPayPlanVersion` (database: a payment today with one split on the plan alone and one on the plan's procedure, under versions 1, 3, 4 and 2: both splits on the receipt, only today's payments, the procedure the attached split pays; the regular receipt under 2 and 4 only; the view itself under 2 only). The 24.3 reference report in `LegacyServiceDateViewReport.cs` calls the core query with `includePayPlanSplits` false.

## Integration (branch `port/m2-sdv-t1`, merged with `port/int-7`)

- Conflicts only in `OpenDentBusiness.csproj` (this package's `Statements.ServiceDateView.cs` beside B-PRINT's `Statements.EmailName.cs`) and `OpenDental.csproj` (`ControlAccount.ServiceDateView.cs` beside B-ACCT's `ControlAccount.SalesTax.cs`): both entries kept. `ControlAccount.cs`, `FormStatementOptions.cs`, `Statements.cs` and `AccountModules.cs` merged on their own.
- B-ACCT's Sales Tax quick proc and B-SHELL's program-link selection read `gridAccount.SelectedIndices`; in service-date view the selection mirror keeps those indices on the matching regular rows, so both work in either view. B-ACCT's sales-tax order (25.1) applies to the regular account tables; the service-date view keeps its own order by date of service: a sales-tax procedure is a service of its own, and a sales-tax adjustment sits under the procedure it is attached to, or among its date's unallocated rows.
- M2-DUN's HTML statement e-mail builds its body from message tags and lists no transactions, so nothing more to connect; the PDF attachment follows the stored flag. B-PRINT's attachment name and inserter barcode work on the sheet around the main grid and are unaffected.
- No fork migration step (as before), so the adoption tools need nothing from this package.

## Review fix (branch `port/m2-sdv-t3`)

- Receipt totals in Service Date View contradicted their own procedure rows. `FilterForReceipt` keeps each listed procedure but only the range's patient payments, while the procedure row's Balance is charge less every credit (`ProcCredits`); `MakeTotal` then summed charge less credit over the filtered raw rows, so every credit not on the receipt dropped out of "Total for Date" and "Family Total:"/"Patient Total:" (the statement grid prints that value in its Balance column). Example from the database test's family, receipt for its second date: procedure 1 shows 40.00 and procedure 3 shows 60.00, but both totals showed 110.00 because the attached adjustment of 10 was left out. Fixed in `ServiceDateViews.Build`/`MakeTotal`: for receipts (not invoices) the Balance and AcctBal of the day totals and the overall total add up the rows shown (`GetReceiptRowBalance`: a procedure adds its own Balance or AcctBal; a credit listed under a procedure the receipt shows adds nothing; any other row adds charge less credit). InsBal already summed the listed procedures only. Invoices, date ranges, other statements and the view itself are unchanged.
- Tests: `Build_Receipt_PaymentsInRangeWithTheProceduresTheyPay` now checks the day totals (10 and -15) and the overall total (-5) for Balance and AcctBal; new `Build_Receipt_TotalsAddUpTheRowsShown` (a procedure paid off by insurance, an attached adjustment and today's payment totals 0; a procedure with pending insurance gives Balance 60 and AcctBal 10; a payment on a procedure the view does not list counts once); `Statement_ServiceDateView_Receipt_PaymentsOfTheDayWithTheirProcedures` checks that both bold total rows show 100.00 (40.00 plus 60.00).

## Integration on `port/m2-sdv-t3` (merged with `port/int-9`)

- The merge with `port/int-9` (B-CLAIM, M2-TERM, M2-SHEETS, V-EDS, M2-CLAIMRCV) had no conflicts; none of those packages changed this package's files except `SheetDrawingJob.cs` (not owned here), and the statement sheet tests (fill and PDF render) pass on the merged code.
- M2-CLAIMRCV asked this package (owner of `AccountModules.cs`) to show claim status H with the 26.1 text: the description of a claim row in the account tables (the Account grid) now reads `Lans.g("ContrAccount",ClaimReceiveRelated.HOLD_UNTIL_PRIOR_RECEIVED)` ("Hold Until Prior Received") instead of "Hold until Pri received".
- M2-SHEETS asked this package (owner of `SheetUtil.cs`) to measure growing text fields with the font they are drawn with: `CalculateHeights` and `CalculateHeightsPageBreakForText` now build the measuring font from `SheetFonts.GetFontNameForText(field.FontName,field.FieldValue)`. Latin text resolves to the field's own font, so nothing changes for it; text in a script the font lacks is measured with the fallback font `SheetTextDrawing` draws it with, so its last line is no longer cut.
- `ServiceDateViewUiSourceTests.OwnedFiles_FollowThePackagesMergedBefore` pins both.
- Still no fork migration step, so the adoption tools need nothing from this package.

## Tests

New project `tests/M2SDVTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 47 tests:

- `ServiceDateViewBuilderTests` (no database): order (procedure, its credits by date, patients by first name, higher procedure number first), unallocated heading, day and overall totals, detailed balances, grouping, date range with balance forward, receipts, invoices, the report table's blanks, the query-table reader, statement-type rules.
- `ServiceDateViewDbTests` (scratch database `m2sdv_<time>_<pid>_<random>_sdv` from the 24.3.63 seed and the fork migrations, dropped afterwards; synthetic family): `RpServiceDateView.GetData` matches the 24.3 display query (kept in `LegacyServiceDateViewReport.cs` as the reference, around the same core query) row by row for the family and each patient, detailed or not, and under PayPlansVersion 1 to 4; the fork columns; statements in both views (intermingled, per patient, single patient, date range, receipt, invoice, limited and super family, saved flag on reprint, totals and aging unchanged, full sheet fill and PDF render); the Billing default; per-user settings beside the proc breakdown row; window and grid cells (columns from display fields, amounts, tooth nomenclature, bold totals).
- `ServiceDateViewUiSourceTests`: the Windows Forms hooks stay in place (source checks; no window is shown).

Checks on the first branch (`port/m2-sdv`): build of `OpenDental.Local.slnf` (Debug, x86, full rebuild) 0 errors and 376 warnings (the baseline); targeted tests `M2SDVTests` 43/43, `StatementTests` 8/8, `ReportTests` 28/28, `SheetTests` 31/31, `M202Tests` 39/39, `NoHqTests` 49/49; `tools/Test-NoHqHosts.ps1` passed.

Checks on `port/m2-sdv-t1` (after the merge with `port/int-7`): full build of `OpenDental.Local.slnf` through the build lane, 0 errors; `run-tests -Mode Core`, 61 assemblies, all passed (`M2SDVTests` 45/45, `StatementTests` 8/8, `ReportTests` 31/31, `SheetTests` 31/31, `M202Tests` 39/39, `BERATests` 73/73, `BACCTTests` 41/41, `BPRINTTests` 71/71, `M2DUNTests` 35/35, `NoHqTests` 49/49); results in `.git/od-tools/test-results/port_m2-sdv-t1.txt`. `tools/Test-NoHqHosts.ps1` passed. The slow migration and adoption assemblies were not run: this package has no migration, schema or adoption change.

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
