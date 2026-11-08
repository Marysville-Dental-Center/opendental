# Port note: B-ACCT (Account module and sales tax)

Package B-ACCT from [../specs/port-backlog.md](../specs/port-backlog.md), M1 Tier B, wave B1, built on branch `port/b-acct` from
`m1/integration-a2-patch` in the single-wave run. Sources: Open Dental's public release notes (24.4, 25.1, 25.2, 25.3) and the feature specs. No
Open Dental binary, customer-only manual page or office data was used. Tests: `tests/BACCTTests` (41 tests, synthetic data on a scratch
database).

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-account-salestax-proc` | built | Quick Procs > Sales Tax adds one completed procedure carrying the tax on the highlighted completed procedures |
| `v25_1-account-salestax-sort-bottom` | built | Sales tax rows go last within their date in the ledger and on statements |
| `v25_1-account-salestax-procs-on-claims` | built (no code change needed) | Sales-tax procedures go on claims like any procedure |
| `v25_2-account-prodselect-amount-unpaid` | built | Select Production: "Amount End" reads "Amount Unpaid" |
| `v25_3-account-orthocase-cleanup` | built (INFERRED which text) | Ortho Case window: the long instruction texts are removed |
| `v24_4-reports-dailypayments-merchant-fees` | built | Daily Payments: one "Show merchant fees" box, one Merchant Fee column and one set of totals |

### v24_4-account-salestax-proc

- **Setting.** Preferences > Account > Adjustments, Sales Tax group: new "Sales Tax procedure code" box with a code picker. A blank value turns the
  feature off; any other value must be an existing procedure code (checked on save).
- **Storage.** Open Dental's preference key is not in any public source, and `tests/RegistryTests` keeps unconfirmed names out of `PrefName`. The
  setting is therefore a non-enum preference row named `SalesTaxProcCode` (the spec's proposed name), read and written by name like the fork's other
  non-enum rows (`DxcPartnerIdentity`). The row is inserted the first time the office saves a code, so no migration or seed row is needed and a
  database without it reads as "off". The value is a ProcCode; a value that is no ProcCode but is the CodeNum of an existing code is also accepted
  (INFERRED, in case Open Dental's own row under this name stores a CodeNum). Code: `SalesTaxProcs` in
  `OpenDentBusiness/Data Interface/SalesTaxProcs.cs`.
- **Menu.** Account module, Quick Procs drop-down: "Sales Tax" is the first item, shown only when the code exists. The button keeps our label
  "Quick Procs" (Open Dental 24.4 calls it "Add Proc"). The quick charge definitions keep working: `menuItemQuickProcs_Click` subtracts the number
  of leading items.
- **Rules.**
  - Fee = sum over the highlighted completed procedures of `ClaimProcs.ComputeSalesTax(proc, claimProcs, isEstimate:false)`, i.e. (fee - write-off)
    x SalesTaxPercentage / 100 rounded to cents per procedure. This is exactly what Apply Sales Tax posts as adjustments, so both methods give the
    same total (tested).
  - Only completed procedures count; rows that are not procedures and sales-tax procedures themselves are ignored. Nothing highlighted, nothing
    taxable, or a zero total: a message, and nothing is added.
  - Warning (OK/Cancel) when any highlighted procedure already has a sales-tax adjustment (AdjType = SalesTaxAdjustmentType).
  - The new procedure: status C, ProcDate today, fee = the tax, provider = SalesTaxDefaultProvider, else the patient's primary provider; clinic = the
    patient's clinic, or the current clinic when clinics are on and the patient has none (INFERRED). It is inserted and validated the way quick charge
    procedures are (`FormProcEdit` in quick-add mode: insurance estimates, procedure notes), then the ProcedureComplete automation and
    `Procedures.AfterProcsSetComplete` run.
  - Never taxed itself: `Adjustments.CreateAdjustmentForSalesTax` (Apply Sales Tax by hand and automatic tax) skips it, and the treatment-plan tax
    estimate (`Procedures.ComputeEstimates`) is 0 for it, even when its code is marked taxed.
- **Permissions and log.** `AccountProcsQuickAdd`, then `ProcComplCreate` dated today. One `AccountProcsQuickAdd` security log entry naming the
  code, amount and number of procedures taxed.

### v25_1-account-salestax-sort-bottom

- A row is sales tax when it is a procedure with the configured sales-tax code, or an adjustment whose type is SalesTaxAdjustmentType. Account
  rows do not carry the adjustment type, so the sales-tax AdjNums are collected before sorting (from the adjustment rows `GetAccount` already reads;
  the super-family statement looks them up with one query).
- `SalesTaxLedgerOrder.Wrap` wraps the existing comparisons, so `AccountLineComparer` and `SortRowsForStatmentPrinting` are unchanged: for two rows
  of the same date (and, for statement printing, the same patient) a sales tax row goes after a non-tax row; otherwise the existing comparison
  decides. Tax rows keep the ledger's usual order among themselves (procedures before adjustments). Order across dates and patients is unchanged.
- Applied at all three sort sites in `AccountModules`: the Account module and intermingled statements, statements printed by patient, and
  super-family statements. The payment plan amortization table is not affected.
- With nothing configured (no code, no SalesTaxAdjustmentType) the sort is exactly the 24.3 one. With only SalesTaxAdjustmentType set, sales-tax
  adjustments move last (the spec's rule covers adjustments on their own).

### v25_1-account-salestax-procs-on-claims

- Our 24.3 code never excluded sales-tax procedures (that exclusion was only in Open Dental 24.4's version of the feature), and the new
  sales-tax procedure adds none: it gets insurance estimates like any procedure, Create Claim with nothing highlighted picks it when it needs
  sending, and `AccountModules.CreateClaim` attaches it (tested). An office that does not bill tax marks the code "Do not usually bill to insurance";
  the existing NoBillIns rule and message in `ClaimL` then leave it off (tested). The Canadian 7-procedure limit is unchanged.
- `OpenDental/Logic/ClaimL.cs` and `OpenDentBusiness/X12/X837_5010.cs` (both owned by this package) needed no change: X837 has no per-code filter.

### v25_2-account-prodselect-amount-unpaid

- `FormProdSelect`: the column header "Amount\r\nEnd" reads "Amount\r\nUnpaid" (new translation key). The column still shows `AmountEnd`.

### v25_3-account-orthocase-cleanup

- INFERRED which text Open Dental removed: the release note only says excess text was removed. Removed the two long instruction texts (`label4`,
  the purpose paragraph across the top, and `label2`, the ten numbered steps) with their resx strings. Kept the short "or" label between the banding
  procedure and the Transfer box and the "Codes for banding, visit, and debond get set in Prefs" hint, which carry information.
- The guidance moved to a tooltip on "Banding Procedure" (our wording, condensed).
- The window was compacted to close the gaps the texts left: the banding row moves to the top, the fee, breakdown, visit and date groups move up,
  the Ortho Schedule grid starts higher, and the window is 1019 x 410 instead of 1019 x 617. The grid is anchored on all sides, so a resized window
  still shows more rows.

### v24_4-reports-dailypayments-merchant-fees

- `FormRpPaySheet`: one "Show merchant fees" box replaces "Show CareCredit fees" and "Show PayConnect fees". One Merchant Fee column (before
  Amount), "Total Merchant Fees" and "Total Patient Payments After Fees" for patient payments, "Total Merchant Fees" and "Total Online Payments After
  Fees" for online payments when shown separately, "Total All Merchant Fees" and "Total All Payments After Fees" for the report.
- The table logic moved to `RpPaySheet` (`AddMerchantFeeRows`, `AddAfterFeeColumn`, `AddMerchantFeeColumn`, `IsMerchantFeeSource`,
  `GetMerchantFeeForReport`) so it can be tested; the 24.3 layout is kept: each payment with a fee gets one fee row after its last row, with no
  amount, provider or clinic, and fees show for patient payments only.
- Sources: CareCredit (history, kept although CareCredit is cut from the fork), every PayConnect source, and any other processor source that records
  a MerchantFee (the spec's decision). Not `None`: a payment without a processor only carries a surcharge typed in by hand.
- Signs as in 24.3: CareCredit stores its fee with the opposite sign and is negated; the others are shown as stored; "after fees" = amount + the fee
  as shown.

## Files touched

New:

- `OpenDentBusiness/Data Interface/SalesTaxProcs.cs`: `SalesTaxProcs` (setting, tax total, new procedure, warning lookups) and `SalesTaxLedgerOrder`.
- `OpenDental/Main Modules/ControlAccount.SalesTax.cs`: the Quick Procs > Sales Tax item and its flow (partial class).
- `tests/BACCTTests/` (new test project), `docs/port-notes/B-ACCT.md`.

Changed (owned by B-ACCT):

- `OpenDentBusiness/Data Interface/Adjustments.cs`: 3 lines in `CreateAdjustmentForSalesTax`.
- `OpenDentBusiness/Data Interface/Procedures.cs`: 1 line, the TP tax estimate.
- `OpenDentBusiness/Db Multi Table/AccountModules.cs`: the three sort calls.
- `OpenDentBusiness/Reports/RpPaySheet.cs`: merchant-fee helpers (new region at the end).
- `OpenDental/Main Modules/ControlAccount.cs`: 2 lines (call to add the menu item; index offset in `menuItemQuickProcs_Click`).
- `OpenDental/Forms Reports/FormRpPaySheet.cs` and `.designer.cs`.
- `OpenDental/Forms/FormProdSelect.cs`; `OpenDental/Forms/FormOrthoCase.designer.cs` and `.resx`.
- `OpenDental/User Controls/Preferences/UserControlAccountAdjustments.cs` and `.Designer.cs`.

Shared project files (one entry each): `OpenDentBusiness/OpenDentBusiness.csproj`, `OpenDental/OpenDental.csproj`, `OpenDental.sln`,
`OpenDental.Local.slnf`.

Not changed: foundation registries (`PrefName`, `PrefInfos.xml`, permissions, seed rows, fork migrations), `ClaimProcs.cs`, `ClaimL.cs`,
`X837_5010.cs`.

## INFERRED behaviours and deviations

- Preference key `SalesTaxProcCode` (proposed, unconfirmed) as a non-enum row, inserted on first save; a stored CodeNum is accepted too.
- No `PrefInfos.xml` entry for the new box (foundation-owned): the box has no info popup in Preferences until the registry owner adds one.
- Clinic of the sales-tax procedure when the patient has none and clinics are on: the current clinic.
- Procedures attached to an ortho case are taxed by the procedure method (the adjustment method refuses them, because it attaches adjustments to the
  procedures; the sales-tax procedure attaches nothing).
- The sales-tax procedure goes through the normal procedure path, so a discount plan could apply to it like to any procedure.
- Ortho Case window: which texts were removed, the tooltip and the compacted layout.
- Daily Payments: the report-wide fee total is labelled "Total All Merchant Fees" (24.3 used "Total All CareCredit Fees" / "Total All PayConnect
  Fees"); the spec says "Total Merchant Fees" for both.
- Fork migration range: B-ACCT was given levels 2027000201-2027000299, but `ForkMigrationRunner.ValidateStep` only accepts yyyyMMddNN levels with a
  real date, and month 00 is not one, so a step in that range would stop every migration. This package needed no step.

## Interplay with packages built in parallel

- **B-CLAIM** (depends on B-ACCT; takes over `ClaimL.cs`): do not add any claim exclusion for sales-tax procedures; `SalesTaxProcs.IsSalesTaxProc`
  identifies them if a claim rule ever needs to.
- **B-CLAIM / M2-EST** (own `ClaimProcs.cs` later): the never-taxed rule could move into `ClaimProcs.ComputeSalesTax` itself
  (`if(SalesTaxProcs.IsSalesTaxProc(procedure)) return 0;`); the hooks in `Adjustments.cs` and `Procedures.cs` would then be redundant but harmless.
- **B-PAY**: `PayPlanProductionEntry.cs` adds `ComputeSalesTax(isEstimate:true)` to payment-plan production; it is only non-zero for codes marked
  taxed, so a sales-tax code marked taxed would get a tax estimate there. Suggest the same `IsSalesTaxProc` skip when that file is next edited.
  `PaymentEdit.cs` has its own account-entry sort ("similar to AccountLineComparer"), not changed here.
- **B-PRINT** (statements): statements get the new order through `AccountModules`; nothing to do in `Statements.cs` or the sheet framework.
- **B-SHELL / M2-SDV** (take over `ControlAccount.cs`): the Sales Tax flow lives in `ControlAccount.SalesTax.cs`; keep the two hook lines in
  `contextMenuQuickProcs_Popup` and `menuItemQuickProcs_Click`.
- **Registry owner (M1-02 follow-up / M2-02)**: when the real key is confirmed, rename `SalesTaxProcs.PREF_PROC_CODE` (and the row, with a
  migration step), optionally add it to `PrefName` and give the box a `PrefInfos.xml` entry (control `labelSalesTaxProcCode`, category
  `AccountAdjustments`).

## Questions for the owner

1. What is Open Dental's preference key (and value format) for the 24.4 sales-tax procedure code? The census would show it; until then the fork
   uses `SalesTaxProcCode` holding a ProcCode. **Answered (2026-11-07, owner question Switch-over 1):** the office's Open Dental 25.3.48
   database has `SalesTaxProcCode`, so the key is the one this program uses and the office's code carries over as it is (see "Open Dental's
   name" below). The value format is still not public; a CodeNum is read too.
2. The level range given to this package (2027000201-2027000299) is rejected by the migration runner's level check (month 00). Should ranges use a
   valid date, or should the runner's check change?
3. Ortho Case window: is removing the two long instruction texts (and compacting the window) what you expect? Please check it on screen.
4. Daily Payments: keep the 24.3 signs (PayConnect fees shown as stored, after fees = amount + fee)? Include every processor except "None"?
5. Should the sales-tax procedure be excluded from discount plans, and should procedures attached to an ortho case be refused as in the
   adjustment method?

## Tests and what is not tested

- `tests/BACCTTests` (41 tests): setting storage and validation; the tax total against the adjustment method (with a write-off) and per-procedure
  rounding; taxable procedures; never taxed again (by hand, automation, TP estimate); the double-tax warning lookup; the new procedure's fields and
  provider; ledger order in the Account module, intermingled, by-patient and super-family statements, balances, adjustments-only and nothing
  configured; `SalesTaxLedgerOrder` on its own; claims (needs sending, attached by `CreateClaim`, NoBillIns rule); merchant-fee rows, column and
  totals in memory and on the real report query; window text checks in the source.
- Not tested: the windows themselves (the Account module menu, Preferences, the Daily Payments report output and the Ortho Case layout are checked
  only through source and business-layer tests, because the program is not launched); X12 output of a claim with a sales-tax line; Middle Tier.

## Combined review (2026-11-01, branch `port/review-3`)

- **Finding schema-adoption-2, fixed in the runbook (no behaviour change).** The Sales Tax procedure code is read from `SalesTaxProcCode`, the spec's proposed name, which carries the office's value over only if Open Dental uses the same name. After adoption an office that had changed it in Open Dental 25.3.48 finds the default, and nothing in the runbook, `migration-preview` or `adopt` told the office. `docs/adoption-runbook.md` step 0.11 now has the second person record the value in Open Dental before the day (row 1 of its table), step 7.1 sets it again in this program, and both are checklist rows.
- Test: `RegistryTests.PrefNameTests.UnconfirmedNames_EachBuiltSettingIsRecordedAndSetAgainByTheAdoptionRunbook` (each unconfirmed name is either built, with the row it is read from, or listed as not built; step 0.11 has one table row for each row read; steps 0.11 and 7.1 are on the checklist). It fails on the runbook before this fix.
- The naming rule itself (register Open Dental's real names once they are known, or keep these rows) is left to the owner; it is a question of the combined review.

## Open Dental's name (SWMAP, 2026-11-07, branch `next/swmap-1`)

- The setting names of the office's Open Dental 25.3.48 database (owner question Switch-over 1) include `SalesTaxProcCode`: Open Dental's key
  is the name this program already read and wrote, so the office's sales-tax procedure code carries over at the switch-over without any
  change, and runbook step 0.11 no longer lists it; `migration-preview` shows the code (runbook step 0.2) and step 7.1 compares it.
  Its value format is still not public, so `SalesTaxProcs.GetCodeNumFromSetting` keeps reading a CodeNum as well as a ProcCode. The row stays
  outside `PrefName` (a database from 24.3 has none).
- Tests: `SWMAPTests` (the name, an adopted synthetic 25.3.48 database with a made-up code read as it is),
  `RegistryTests.PrefNameTests.ProposedNames_EachIsReadUnderOpenDentalsName_OrRecordedAndSetAgainByTheAdoptionRunbook` (renamed from
  `UnconfirmedNames_EachBuiltSettingIsRecordedAndSetAgainByTheAdoptionRunbook`). See [SWMAP.md](SWMAP.md).
