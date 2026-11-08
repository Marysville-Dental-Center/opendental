# Port note: P262-TOOLS, the tools of Open Dental 26.2 (items 23 to 26 and 30)

Branch `p262/tools-1`, cut from `p262/schema-1` (39be2b04b). Five items of the Tools section of the Open Dental 26.2 release notes, chosen by
the owner on 2026-11-07:

| Item | Release note (paraphrased) | Here |
|---|---|---|
| 23 | a database maintenance method that fixes payments with income transfer problems | Check `PaymentIncomeTransferIssues` |
| 24 | a database maintenance tool that cleans up incorrectly dated conversion payments | Tools tab: Conversion Payment Dates (on demand only) |
| 25 | a Fee Tool that copies fees from one procedure code to another | Fee Tools, tab Copy Code |
| 26 | the latency test tool measures average latency | Test Latency, group Average latency |
| 30 | a database maintenance method that moves redundant insurance verification entries | Check `InsVerifyRedundantEntries` |

Items 27 to 29 (a chat maintenance method, the MariaDB backup tools) are not part of this package.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled; everything is this program's own
code.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html): one line per item, no link, no detail.
- `OpenDentalDocumentation26-2.xml` (the full 26.2 schema), read-only:
  - `payment.PayType`: 0 for an income transfer; `payment.PayAmt`: must equal the sum of the splits; `paysplit.DatePay`: always in sync with
    the payment date. These define items 23 and 24.
  - The `insverify` table: one row for the most recent verification of an insurance plan (benefits) or patient plan (eligibility), never more
    than one per plan, older ones moved to `insverifyhist`, which holds copies of the insverify fields. This defines item 30.
  - `DbmLogFKeyType` has no insurance verification value, so the DBM log of item 30 uses `InsPlan` and `PatPlan` (Open Dental's values).
- `OpenDentalDiffDocumentation26-2.xml`: no table, column or enum value for these items (none is needed).
- Public manual pages: Database Maintenance and its Tools tab (`databasemaintenance.html`, `databasemaintenancetools.html`), Fee Tools
  (`feescheduletools.html`), Test Latency (`testlatency.html`: latency is the round trip between the computer and the database, in
  milliseconds; a good average is under 50 ms). The public manual does not describe the 26.2 versions of these windows yet (the 26.2 manual
  folder is not public), so none of the 26.2 behaviour comes from it.
- Open Dental's public conversion guides (the PDFs under `opendental.com/resources/conversions/`, found by a web search and read as quoted in
  its result): after a conversion, the income transfers made to clean up the converted history are to be back dated to the date of the
  conversion so that they do not affect reports; the transfers Open Dental's conversion makes for families at zero show in reports whose
  range includes the conversion date. This is the only public description of what an incorrectly dated conversion payment is.

## What was built

### Item 23: Database Maintenance check `PaymentIncomeTransferIssues`

`OpenPmsDentalBusiness/Data Interface/DatabaseMaintenances.IncomeTransfers.cs`. An ordinary method of the Checks tab
(`[DbmMethodAttr(HasBreakDown, HasExplain, IsReplicationUnsafe)]`), with an explanation for the Explain button (`OpenPmsDental/Misc/DbmInfo.cs`).
Three groups:

1. **Income transfers saved with a payment type** (fixed): a $0 payment with a payment type whose splits total $0 and move money (at least one
   split is not $0). Aging, the Family Balancer and the other income transfer code only treat a payment without a payment type as an income
   transfer. Fix sets `PayType` to 0, changes no amount, and writes a DBM log entry per payment with the old payment type. Left alone: payments
   on a deposit, card or online payments (`PaymentSource` not None), recurring charges, payments linked to an accounting transaction, payments
   whose splits are all $0 (a declined card) and payments whose splits do not total $0 (those are `PaymentAmtNotMatchPaySplitTotal`'s).
2. **Income transfers with an amount other than $0** (manual): an income transfer must be $0.
3. **Income transfers whose splits do not total $0** (manual): the transfer adds money to or takes money from the account.

Check counts the three groups; Fix fixes group 1 and repeats the manual counts; Breakdown (double click) lists the patient, date, amount and
split total of every payment of the three groups. A transfer without splits is left to `PaymentMissingPaySplit`.

### Item 24: Conversion Payment Dates (Tools tab, on demand only)

`DatabaseMaintenances.ConversionPayments.cs`, `OpenPmsDental/Forms/FormDatabaseMaintConvPayDates.cs`, the Tools tab row in
`FormDatabaseMaintenance.cs` and its handler in `FormDatabaseMaintenance.Tools262.cs`. Never part of Run All: it has no `DbmMethodAttr`.

- The user enters the conversion date and clicks Find. The window lists the income transfers (no payment type, $0, splits totalling $0)
  dated after the conversion date or with no valid date (before 1880), with what their splits are attached to: entries dated on or before the
  conversion date (procedures, adjustments, the split they reverse), entries after it (or an entry that no longer exists), a payment plan, or
  nothing (unearned money).
- The window selects the **suggested** transfers: at least one split on a converted entry, none on a later entry or a payment plan. The user
  can change the selection.
- Change Dates (after the replication check, Payment Edit permission for the conversion date, so the lock date applies, and a confirmation
  that asks for a backup) dates each selected transfer and every one of its splits on the conversion date. Each payment is checked again
  first; one that changed since the list was filled is skipped. Each change writes a DBM log entry (old and new date, number of splits) and a
  Payment Edit audit trail entry for the patient; the log text goes to the DBM log file.

### Item 25: Fee Tools, Copy Code

`OpenPmsDentalBusiness/Data Interface/Fees.CopyToCode.cs` (`Fees` is now a partial class), tab Copy Code in `FormFeeSchedTools`.

- From code and To code (typed, or picked with the Procedure Codes window), every fee schedule (default) or only the fee schedule under
  Select Fees, then Copy after a confirmation that says what is replaced.
- In each fee schedule where the From code has at least one fee, the To code gets exactly the From code's fee rows: the same amounts for the
  same clinics, providers and effective dates, blank overrides (-1) included. A To row with the same clinic, provider and date is updated
  (keeping its number), a missing one is inserted, and a To row the From code does not have in that fee schedule is deleted. Fee schedules
  where the From code has no fee are not changed. Fee schedule groups stay in sync because every clinic's rows are copied. Of two rows with
  the same clinic, provider and date (left from before such duplicates were refused) only the first From row is copied, and a second To row
  is deleted.
- Permissions: the window already needs Fee Schedule Edit. A user restricted to clinics cannot use Copy Code (it changes every clinic's fees).
- Audit: each insert, update and delete makes a Procedure Fee Edit entry (FKey the To code; old and new amount, fee schedule, effective date,
  clinic, provider, the From code) and a Fee Edit entry (FKey the fee), like the other fee tools.
- The result message counts the fee schedules copied and the fees added, changed, deleted and already the same.

### Item 26: Test Latency, Average latency

`OpenPmsDentalBusiness/Misc/LatencyMeasure.cs`, a new group in `FormTestLatency`. Runs the same very small query as the single latency test
(the server version) a chosen number of times (default 10, 1 to 1000), timing each round trip on its own with fractions of a millisecond,
and shows the average, the fastest and the slowest, one decimal. Through the Middle Tier when the workstation uses one, like the single test.

### Item 30: Database Maintenance check `InsVerifyRedundantEntries`

`DatabaseMaintenances.InsVerifyRedundant.cs`; an ordinary method of the Checks tab (`[DbmMethodAttr(IsReplicationUnsafe)]`).

- Check counts the insverify rows that are not the one kept for their insurance plan (benefits) or patient plan (eligibility).
- Fix keeps the row with the latest last-verified date (on a tie, the newest row) and moves every other row into `insverifyhist`: a copy of
  every field, its security hash unchanged (it stays valid, the hashed fields are copied), the current user as `VerifyUserNum`, and the kept
  row's `InsVerifyNum`, so the history of the plan stays with the row the program uses. The copy is saved before the row is deleted. Each move
  writes a DBM log entry (FKey the plan or patient plan) naming both rows, the old row's date, status and assigned user.
- Rows of the verify type None are left alone.
- `InsVerifies.GetOneByFKey`, which reads the latest verification of a plan, now breaks a date tie by the newest row, so the program shows
  the row the fix keeps.

### No migration step, no enum value

The package adds no table, column, preference, enum value or migration step: levels 2026110751 to 2026110759 stay unused (a test checks
it). Database Maintenance inserts the rows of new methods itself when its window opens (`InsertMissingDBMs`).

## INFERRED

Nothing below is stated by the public sources; it is this program's own design.

- **The names** `PaymentIncomeTransferIssues`, `InsVerifyRedundantEntries`, Conversion Payment Dates and Copy Code. Open Dental's names are
  not published; an adopted Open Dental 26.2 database's maintenance rows for its own method names are simply not shown here.
- **Item 23**: which problems count as income transfer issues (the three groups), that only group 1 is fixed automatically, the exclusions
  of group 1, and the wording of the explanation.
- **Item 24**: that the incorrectly dated conversion payments are the income transfers dated after the conversion date (or without a date);
  the rule of the suggested selection; that the conversion date is entered each time (no preference); that the payment and every split get
  the conversion date; Payment Edit for the conversion date; the window.
- **Item 25**: copying per fee schedule with an exact replacement of the To code's rows there; leaving other fee schedules alone; effective
  dates and blank overrides copied; no use for users restricted to clinics; the tab and its controls.
- **Item 26**: the number of round trips (10, up to 1000), no warm-up round trip left out, the fastest and slowest shown, one decimal.
- **Item 30**: which row stays (latest last-verified date, then newest), that the history copy points to the kept row, and the DBM log types
  `InsPlan` and `PatPlan`.

## Not built

- Tools items 27 to 29 (other packages or not chosen).
- No change to the existing checks (`PaymentAmtNotMatchPaySplitTotal`, `PaymentMissingPaySplit`) or to the Family Balancer.
- No UI was run (OpenPmsDental.exe is not launched in this run); the windows are covered by source checks and, for Fee Tools, the in-memory
  layout test of `BPROCTests`.

## Files touched

- New: `OpenPmsDentalBusiness/Data Interface/DatabaseMaintenances.{IncomeTransfers,ConversionPayments,InsVerifyRedundant}.cs`,
  `Data Interface/Fees.CopyToCode.cs`, `Misc/LatencyMeasure.cs`; `OpenPmsDental/Forms/FormDatabaseMaintConvPayDates.cs` and `.Designer.cs`,
  `Forms/FormDatabaseMaintenance.Tools262.cs`; `tests/P262ToolsTests/*`; this note.
- Changed: `Data Interface/Fees.cs` (partial), `Data Interface/InsVerifies.cs` (`GetOneByFKey` tie-break), both `.csproj` files,
  `Forms/FormDatabaseMaintenance.cs` (Tools tab row), `Forms/FormFeeSchedTools.cs` and `.designer.cs` (Copy Code tab),
  `Forms/FormTestLatency.cs` and `.designer.cs` (Average latency), `Misc/DbmInfo.cs` (explanation), `OpenDental.sln`, `OpenDental.Local.slnf`
  (the test project), `tests/BPROCTests/FormLayoutTests.cs` (the sixth Fee Tools tab), `docs/release-notes/next.md`.

## Tests

`tests/P262ToolsTests` (new, 39 tests): one scratch database `test_p262tools_*` built from `db/init/new-database.sql` and the fork migrations,
dropped after the run; synthetic rows only.

- `IncomeTransferDbTests`: group 1 found and fixed with its DBM log, amounts unchanged; deposits, card, recurring and accounting-linked
  payments, all-$0 and unbalanced splits left alone; groups 2 and 3 reported and never changed; breakdown; the attribute.
- `InsVerifyRedundantDbTests`: the latest verification kept, the other moved with every field and the hash, pointing to the kept row; a date
  tie keeps the newest row, the one `GetOneByFKey` reads; three rows; single rows, other verify types and None untouched; the ordering.
- `ConversionPaymentDatesDbTests` and `ConversionSplitKindTests`: which transfers are listed and suggested; Fix dates the payment and every
  split and writes both logs; stale rows are skipped; the split rule.
- `FeeCopyCodeDbTests`: exact copy per fee schedule the From code has, other schedules untouched, one schedule only, audit entries, legacy
  duplicate rows, a second copy changes nothing, invalid codes.
- `LatencyMeasureTests`: the arithmetic, the limits, one run against the database.
- `WiringTests`: the windows call the business methods behind their checks; the two checks carry `DbmMethodAttr`, Conversion Payment Dates
  does not; no fork step at 2026110751 to 2026110759; the project entries.

## Checks (2026-11-07)

- Full build of `OpenDental.Local.slnf` (Debug, x86): 0 errors.
- Targeted runs, with `NoHqTests` (58/58): `P262ToolsTests` 39/39, `FeeTests` 48/48, `BDBMTests` 30/30, `BPROCTests` 43/43,
  `M2INSVTests` 17/17. `ForkMigrationTests`, `ReleaseFlowTests` and `InitSqlTests` were not run (the integration runs them).
- Scratch databases `test_p262tools_*` are dropped after each run.

## Questions for the owner

1. Item 23: may Fix give a $0 payment with a payment type and balanced splits no payment type (chosen, logged), or should the check only
   report it? Should groups 2 and 3 get an automatic fix (for example an income transfer whose splits total $0 but whose amount is not $0
   could get the amount $0), or stay manual (chosen)?
2. Item 24: is "income transfers dated after the conversion date" the right meaning of incorrectly dated conversion payments? Should the
   conversion date be remembered (a preference), and should Open Dental's own conversion transfers be told apart (no column marks them)?
3. Item 25: should the To code's fees that the From code does not have in a copied fee schedule be deleted (chosen, so both codes cost the
   same everywhere in that schedule) or kept? Should users restricted to clinics be able to copy their own clinics' fees?
4. Item 26: is 10 round trips a good default, and should the first round trip (which may open the connection) be left out of the average?
5. Item 30: when the row that is kept has no status or assigned user and a moved row has one, should the kept row take them over (now they
   stay in the history only)?
