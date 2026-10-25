# Port note: M2-PAYW, Payment window

Branch `port/m2-payw`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, M2-PAYW](../specs/port-backlog.md#m2-payw-payment-window). Specs: [features-25.4.md](../specs/features-25.4.md#v25_4-account-incometransfer-limit-limit-manual-income-transfers-to-the-amount-of-patient-payments) and [features-26.1.md](../specs/features-26.1.md#v26_1-account-payment-invoice-filter-payment-window-invoice-filter-on-outstanding-charges).

Sources: the public release notes (version25_4.html, version26_1.html) and the published 26.1 database document (`statement.IsInvoice`, `payplancharge.StatementNum`), through the specs. No Open Dental binary, install or customer-only page was used. No fork migration step was needed (assigned range 2027002901 to 2027002999 unused). No schema or registry change: the preference `IncomeTransfersLimitToPatPayments`, its PrefInfos entry and its seed row come from M2-02.

## Feature IDs

| Feature ID | Status | Summary |
|---|---|---|
| `v25_4-account-incometransfer-limit` | built | Preferences > Account > Payments checkbox "Limit manual income transfers to the amount of patient payments" (off by default). With it on, saving a manual income transfer in the Payment window is blocked when it takes more from a source than the patient payments that source holds; the message names each source, what the transfer takes and what is available. |
| `v26_1-account-payment-invoice-filter` | built | Payment window, Outstanding Charges filter group: an "Invoice" drop-down ("All", then the family's invoices that still have a balance, newest first, shown as date, number and total). Picking one limits Outstanding Charges, and so the Pay button and manual splits, to that invoice's procedures, adjustments and payment plan charges. |

## v25_4-account-incometransfer-limit

- **Rule** (`OpenDentBusiness/Data Interface/IncomeTransferLimits.cs`). A transfer's negative splits take from a *source*: the procedure, adjustment or payment plan the split names; otherwise the patient's unearned income, all unearned types together (the Payment window pools unearned types in transfer mode, so a transfer split can carry a different type than the money it takes); otherwise the patient's unallocated income. Provider and clinic do not split a source either, for the same reason. Per source, the splits of the transfer are netted; if the transfer takes money overall, the amount taken may not exceed the sum of the stored paysplits of every *other* payment on that source (half-cent tolerance). Insurance payments (claimprocs) and credit adjustments never appear as paysplits, so they can never be moved into patient credit, and no source can give more than it holds.
- **Only new taking is checked.** The transfer's own stored splits (from before this edit) are compared first: a source is checked only when the transfer now takes more from it than the stored version did. An old transfer, saved before the preference was turned on, can be reopened and saved unchanged.
- **Where it runs.** `FormPayment.SavePaymentToDb`, in the existing income-transfer branch (`FormPayment.IncomeTransferLimit.cs`, one hook line). Manual transfers are made in the Payment window ("None (Income Transfer)"); that includes the windows opened after an insurance payment by the "provider income transfer" preference (`FormClaimEdit.ShowProviderTransferWindow`) and by the allocate-unearned prompt (`ClaimL.AllocateUnearnedPayment`), which both open the Payment window as an income transfer.
- **Automatic tools are not limited.** The Income Transfer Manager's Transfer and FIFO buttons insert their payments directly and never reach this check. The Payment window's own Transfer button (automatic transfer when "None" is checked) records a fingerprint of the splits it made; if the user saves them unchanged, the check is skipped. Any change to those splits makes it a manual transfer and the check runs.
- **Preference UI.** `UserControlAccountPayments`: checkbox `checkIncomeTransfersLimitToPatPayments` under Payments, below "Income transfers treat negative production as income" (the Payments group grew one row; the two groups below moved down 23 px). Changing it writes a Setup security-log entry. A blocked transfer is not logged.
- **Permissions.** Unchanged (PaymentCreate / PaymentEdit as before).

## v26_1-account-payment-invoice-filter

- **Members** (`OpenDentBusiness/Db Multi Table/PaymentInvoiceFilter.cs`). An invoice is a `statement` with `IsInvoice=1`. Members are the procedures, adjustments and payment plan charges whose `StatementNum` is the invoice (this is how the Account module's "Invoice" button builds invoices in this program, `ControlAccount`), plus any `stmtlink` rows of type Proc, Adj or PayPlanCharge on it. Deleted procedures are not members. Invoice copies are the same statement, so they appear once.
- **Which invoices are listed.** Invoices of the family's patients (or of the super family when it is shown), found through the statement's patient or through any member that belongs to the family. Only invoices with at least one member that still owed money before this payment are listed; the invoice currently picked stays listed even after this payment pays it off, so the filter does not reset under the user. With no invoice to list, the drop-down shows "All" and is disabled.
- **Display.** `{invoice date}  #{statement number}  {total}`. Total = procedure fees times quantity + adjustment amounts + payment plan charge principal and interest of the members.
- **Filtering.** Applied in `FillGridCharges` after the existing filters (logical AND with patient, provider, clinic, type, amounts, dates and codes), for all Group By modes. "Show All Charges" still works inside the invoice. Changing the filter only refills the grids; current splits are kept. The Pay button allocates over the rows the grid shows, so it pays only the invoice's charges.
- **UI.** `FormPayment.designer.cs`: `labelInvoiceOutstandingFilter` ("Invoice:") and `comboInvoiceOutstandingFilter` (single selection, `SelectionChangeCommitted` → `FilterChangeCommittedChargesTreatPlan`) as a fourth row of `groupBoxFilteringOutstanding` (height 80 → 103); `tabControlCharges` moved from y 84 to 107 and its height from 296 to 273, so its bottom edge is unchanged. Code in `FormPayment.InvoiceFilter.cs`; two hook lines in `FormPayment.cs`.

## INFERRED behaviours

- The source granularity (procedure / adjustment / payment plan / patient's unearned income of any type / patient's unallocated income) and the "net of every other payment's stored splits" measure of what a source holds.
- Only the increase over the transfer's stored version is checked.
- The Payment window's automatic Transfer button counts as automatic only while its result is unchanged.
- Half-cent tolerance; the message wording; the Setup log text.
- Invoice membership through both `StatementNum` columns and `stmtlink`; deleted procedures excluded; the total formula; the display format; "still owed before this payment" as the balance test; the picked invoice kept in the list; the drop-down disabled when empty.
- The Treat Plan tab is not filtered by invoice (invoices hold completed work; the filter group is shared by both tabs).

## Deviations from the spec

- Spec rule 1 for the income transfer limit says "per source paysplit". This program does not fill `paysplit.FSplitNum` ("No longer used" in 24.3), so a transfer leg cannot be traced to the payment it came from. The limit is per source instead (see above). It keeps the release note's promises: no insurance income or adjustment moves into patient credit, and nothing takes more than the patient payments a source holds.
- Spec rule 2 ("sources must be real patient payments, not other transfers' positive legs") is not applied literally: money that an earlier transfer moved onto a procedure or into unearned counts as patient money there. Otherwise a payment allocated by one transfer could never be reallocated. With the limit on, no source can go below zero, so transfer legs carry only patient money; legs made before the preference was turned on are the exception (owner question 1).
- The spec names `stmtlink` as the invoice-member link. In this program invoices are built with the `StatementNum` columns; both are read.
- Spec touchpoints `PaySplits.cs`, `StmtLinks.cs`, `Statements.cs` and `PaymentEdit.cs` were not changed: the logic is in two new files and `FormPayment.cs` has four one-line hooks (marked `//M2-PAYW`), as the single-wave rules ask.

## Files

- New: `OpenDentBusiness/Data Interface/IncomeTransferLimits.cs`, `OpenDentBusiness/Db Multi Table/PaymentInvoiceFilter.cs`, `OpenDental/Forms/FormPayment.IncomeTransferLimit.cs`, `OpenDental/Forms/FormPayment.InvoiceFilter.cs`, `tests/M2PAYWTests/*`, this note.
- Changed (owned): `OpenDental/Forms/FormPayment.cs` (4 hook lines), `OpenDental/Forms/FormPayment.designer.cs` (2 controls, filter group height, charges tab position), `OpenDental/User Controls/Preferences/UserControlAccountPayments.cs` and `.Designer.cs`.
- Changed outside the owned list: `OpenDentBusiness/OpenDentBusiness.csproj` (2 `Compile` lines), `OpenDental/OpenDental.csproj` (2 `Compile` entries for the partial files), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project). No registry, enum, `Pref.cs`, `PrefInfos.xml`, table type or CRUD change.

## Interplay with packages built at the same time

- **V-GP** (takes `FormPayment` over in C2): the hooks are single lines; the designer change is two new controls plus three changed values (`groupBoxFilteringOutstanding.Size` 603×103, `tabControlCharges.Location` 498,107 and `.Size` 608×273). If V-GP also moves `tabControlCharges`, keep it below the taller filter group.
- **M2-02**: the preference, its PrefInfos entry (control name `checkIncomeTransfersLimitToPatPayments`, category AccountPayments) and Seed26_1's row are used as registered; `M202Tests.Prefs26_1Tests` still passes.
- **M2-SDV** (Statements, AccountModules) and **M2-DUN** (billing/invoices): no shared files. If either starts building invoices through `stmtlink`, the filter already reads those rows.
- **A-ACCT / Income Transfer Manager**: not touched; its automatic tools stay unlimited.

## Tests

New project `tests/M2PAYWTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `IncomeTransferLimitRuleTests` (no database): sources, netting, insurance and credit adjustments blocked, unearned per patient with types pooled, unallocated per patient, stored transfers, rounding, order, message, fingerprint.
- `InvoiceFilterRuleTests` (no database): membership (both parts of a payment plan charge), filtering, balance before this payment, listing order and the kept choice, display text.
- `IncomeTransferLimitDbTests` and `InvoiceFilterDbTests`: scratch MariaDB database (`forkmig_<time>_<pid>_<random>_m2payw`, built like `tools/New-DevDatabase.ps1`, migrated by the fork runner, dropped afterwards), synthetic data: the seeded preference default, real payments, adjustments, an insurance-overpaid procedure, chained transfers, invoices linked by `StatementNum` and by `stmtlink`, a limited statement ignored, totals, and the Pay button's allocation over the invoice-filtered charges built by `PaymentEdit`.
- `WiringTests`: source checks of the four hooks, the designer controls and the preference checkbox against its PrefInfos entry.

Targeted run: `M2PAYWTests`, `M202Tests` (plus `NoHqTests`): all pass. Not tested: the windows themselves (the GUI is not launched in this run): the drop-down's look, the message box, the Preferences layout.

## Questions for the owner

1. Transfers saved before the preference was turned on may already have moved insurance or adjustment credit into unearned; that money now counts as patient money on its new source. Acceptable, or should the check also exclude money that arrived through transfers (which would block reallocating any transferred payment)?
2. The Payment window's automatic Transfer button result is not limited (as the spec's "automatic tools are unaffected"). Confirm, or should it be checked like a manual transfer?
3. Should the Payment window open with an invoice already picked when it is opened from an invoice (for example from the statement window)? The release note does not say; nothing preselects one now.
4. Invoice total in the drop-down is the gross total of the members, not what is still owed. Prefer the remaining balance?
