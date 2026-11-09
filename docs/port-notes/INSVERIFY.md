# Port note: INSVERIFY, the Insurance Verification List keeps a patient's eligibility row when the keys meet

Branch `fix/insverify-1`, cut from `main` (81e701df5). A bug fix the owner approved, as the lead recommended it. One line of SQL changes, in
`InsVerifies.GetVerifyGridListQuery`; no schema change, no enumeration value, no preference, no new file shipped. Synthetic data only.

## The bug

The Insurance Verification List (`FormInsVerificationList`: the Verification List, Past Due, Assign Standard and Assign Medicaid grids)
lists, for each patient with an appointment in the range, the patient plan's eligibility verification ("Pat", `insverify.VerifyType`
`PatientEnrollment`, `FKey` the `PatPlanNum`) and the plan's benefits verification ("Ins", `InsuranceBenefit`, `FKey` the `PlanNum`) that
are due. When a plan's `PlanNum` equals its patient plan's `PatPlanNum`, and both verifications were due, the list showed only the benefits
row: the patient's eligibility row was missing (with both year checks off or both on, the combined row had no "Pat" half). The batch
verification (`InsVerifies.TryBatchPatInsVerify`) works from the same list and asks the clearinghouse about the "Pat" rows only, so it
skipped that patient as a benefits-only row: its eligibility was never verified automatically.

`insplan` and `patplan` count their keys separately, and they run close together (a subscriber's own plan usually gets one patient plan),
so equal keys are common in real databases; in a new database both start at 1. The stage 1 gate's UnitTests runs found it: the
`InsVerifiesIntegrationTests` passed or failed with the key values, so with the class order (S1-6, section 7).

## The cause

`GetVerifyGridListQuery` builds two queries, one joining `insverify` on the plan's benefits row and one on the patient plan's eligibility
row (Open Dental split the old OR join so MySQL can use the `FKey` index), and wraps their `UNION ALL` in

    SELECT * FROM (...) AS iv GROUP BY PatPlanNum,FKey HAVING MAX(DateLastVerified) ORDER BY AptDateTime

Both rows of one patient plan carry its `PatPlanNum`; the benefits row's `FKey` is the `PlanNum` and the eligibility row's the `PatPlanNum`.
When the two keys are equal the rows have the same `PatPlanNum` and `FKey`, fall into one group, and the group gives one row. MariaDB returns
the values of the first row it reads for a group: the union's first part, the benefits row. The tests show it: on the unfixed query the
patient plan's only row was `InsuranceBenefit:<key>`.

## The fix

The group names the verify type too:

    SELECT * FROM (...) AS iv GROUP BY PatPlanNum,VerifyType,FKey HAVING MAX(DateLastVerified) ORDER BY AptDateTime

Both parts of the union select `insverify.*`, so `VerifyType` is a column of `iv`. A comment at the line says why.

Everything else stays as it was:

- **The same rows collapse.** A group of `PatPlanNum,FKey` held rows of two verify types only when `PlanNum` equals `PatPlanNum`: every
  benefits row of a patient plan has `FKey` = its `PlanNum`, every eligibility row `FKey` = its `PatPlanNum`. So a patient with several
  appointments in the range, or a key with two `insverify` rows (the redundant entries Database Maintenance's `InsVerifyRedundantEntries`
  moves to the history, P262-TOOLS), still gives one row per patient plan, verify type and key (tested).
- **The HAVING** is Open Dental's and unchanged. It does not choose a row: `MAX(DateLastVerified)` in a condition is the date as a number
  (10101 for `0001-01-01`), so it is false only for a group whose latest date is the zero date `0000-00-00` (the column is `NOT NULL`), and
  such a group is not listed. The program never writes the zero date (`POut.Date(DateTime.MinValue)` is `0001-01-01`); only a row written
  outside the program can have it. Checked on the local MariaDB 10.11.
- **Which row of a group is kept** is still the first one read. Neither setting nor clause makes it deterministic: the program runs with a
  blank `sql_mode` (`MiscData.SetSqlMode` at start; Database Maintenance repeats it), and with `ONLY_FULL_GROUP_BY` MariaDB refuses this
  `SELECT *` with `GROUP BY` (error 1055, checked); the HAVING only filters. It decides which appointment a row shows when the patient has
  several in the range, and which of two redundant `insverify` rows is listed: the same as in Open Dental (question 1 below).
- **The ORDER BY AptDateTime** is Open Dental's (24.3.15) and unchanged; it orders the rows the groups kept.

## The callers

- `GetVerifyGridListQuery` is private. Its only caller is `GetVerifyGridList` (once for the standard list, once for the Medicaid list).
- `GetVerifyGridList` is called by `FormInsVerificationList.GetRowsForGrid` (all four grids) and `InsVerifies.TryBatchPatInsVerify` (the
  batch verification of Setup > Scheduled Processes; nothing in the program runs it yet, O-11 of `docs/specs/net10-plan.md`, and the
  UnitTests call it), and by M2INSVTests.
- `TableToListInsVerifyGridObjects` turns the rows into grid rows. It pairs an eligibility row and a benefits row of the same patient plan
  and plan (same note and status) into one row when both year checks are on or both off, and keeps them as two rows otherwise; it skips a
  second benefits row of a plan already listed and a second eligibility row of a patient plan already listed. It always matched rows by
  `PatPlanNum` and `PlanNum`, never by the grouping, so it handles a patient whose keys are equal now exactly as any other patient. No
  caller relied on the collapse.
- No report, no API and no other query uses this query (the local REST API was dropped). `GetInsVerifyCleanupQuery`, the 30-day
  unassignment, has its own query (a `UNION` grouped by `InsVerifyNum`) and is not affected.

What a user sees: the patient whose keys are equal now has a "Pat" row (or a combined "Pat/Ins" row) like every other patient, and the
batch verification sends that patient's eligibility request.

## Open Dental's history

From the public source in this repository (`git log -p --follow` on `OpenDentBusiness/Data Interface/InsVerifies.cs`):

| Versions | What the query did |
|---|---|
| up to 23.3.53, 24.1.24 | One row per appointment and verification; no grouping. |
| 23.3.54, 24.1.25, 24.2.1 (JobNum 53236) | `insverify` joined through a subquery that kept, per `VerifyType,FKey`, the rows with the latest `DateLastVerified`. Grouped by the verify type: no collapse of the two kinds. |
| 24.1.71, 24.2.28, 24.3.1 | The subquery removed (`insverify` joined directly); the union wrapped in `SELECT * FROM (...) AS iv GROUP BY PatPlanNum,FKey HAVING MAX(DateLastVerified)`, with `ORDER BY AptDateTime` still inside; `GetOneByFKey` got `ORDER BY DateLastVerified DESC LIMIT 1`. The verify type left the grouping: the bug starts here. |
| 24.1.86, 24.2.48, 24.3.15 | `ORDER BY AptDateTime` moved out of the derived table (MariaDB ignores an ORDER BY there without a LIMIT) to the end of the outer query. The grouping unchanged. |
| 24.3.63 (the last public source, the fork's base) | As 24.3.15. |
| 24.4 to 26.1 | Not public. The fork does not decompile Open Dental, and the public release notes and the feature specs written from them say nothing about this list's rows. |

So there was no fix of Open Dental's to port: this is the fork's own. The fork's file is 24.3.63's for the grouping; M2-INSV (25.4's day
range and carrier pick) changed the dates and the carrier condition only.

## Tests

New project `tests/INSVERIFYTests` (MSTest v1, x86, SDK-style on `tests/Directory.Build.props`; in `OpenDental.sln` and
`OpenDental.Local.slnf`, wired in its own commit). It references the business library and UnitTestsCore (for the batch verification's
setup), not the program.

- `InsvDb`: the scratch database `insv_<run id>_main` on 127.0.0.1:3306: the committed `db/init/new-database.sql`, then
  `ForkMigrationRunner.Run()`; dropped after the run (`INSVERIFY_KEEP_DB=1` keeps it); leftovers of earlier `insv_` runs older than 12 hours
  are dropped; skipped (inconclusive) when the server does not answer or `INSVERIFY_TEST_DB=skip`. No other database is touched.
- `VerifyGridKeyTests` (database). Each test sets the next keys of `insplan` and `patplan` itself (`ALTER TABLE ... AUTO_INCREMENT`, above
  every key either table holds or handed out) and checks that the plan and patient plan got them, so no test depends on the keys earlier
  tests used:
  - `GridList_PlanNumEqualsPatPlanNum_ListsTheEligibilityAndTheBenefitsRow`: both year checks off: one combined row with both verifications.
  - `GridList_PlanNumEqualsPatPlanNum_SeparateRowsWhenOnlyOneYearCheckIsOn`: a "Pat" row and an "Ins" row.
  - `GridList_DifferentKeys_ListsTheEligibilityAndTheBenefitsRow`: the control; passes before and after the fix.
  - `Query_PlanNumEqualsPatPlanNum_OneRowPerPatientPlanVerifyTypeAndKey`: the query itself (the private method, by reflection) with two
    appointments in the range and a second benefits row of the plan: exactly one benefits row and one eligibility row for the patient plan.
  - `BatchVerify_PlanNumEqualsPatPlanNum_VerifiesTheEligibilityRow`: `TryBatchPatInsVerify` with a carrier trusted for eligibility and a
    synthetic 271 (`x270Controller.FakeResponseOverride271`, nothing is sent): the patient's eligibility row is verified (state Success,
    `DateLastVerified` today) and the benefits row is left alone.
- `QuerySourceTests` (no database): the outer `GROUP BY` line, `HAVING` and `ORDER BY` included, as it is now; and both parts of the union
  select `insverify.*` and join `insverify` on one verify type each.

The existing InsVerify tests in UnitTests are the 33 of `InsVerifyTests` and the 7 of `InsVerifiesIntegrationTests`. For the reverse class
order, a temporary build (not committed) listed UnitTests' 164 `.cs` files in the reverse of the default glob's order (taken from the
compiler command line of the lane build) with `EnableDefaultCompileItems` off; MSTest v1 runs the classes in that order, and
`InsVerifiesIntegrationTests` ran 69th instead of about 1,700th.

| Run (`.git/od-tools/test-results/`) | Query | Result |
|---|---|---|
| `insverify-1-before-fix.txt` | unfixed | INSVERIFYTests 2 of 7: the control and the union check pass; the two list tests, the query test, the batch test and the `GROUP BY` check fail as expected. |
| `insverify-1-reverse-before-fix.txt` | unfixed, UnitTests reversed | UnitTests 1,721 of 1,731 (3 skipped), the 7 failures all of `InsVerifiesIntegrationTests`: `NullReferenceException`, the batch verification returned no row. The S1-6 finding, reproduced. NoHqTests 58 of 58. |
| `insverify-1-reverse-after-fix.txt` | fixed, UnitTests reversed | UnitTests 1,728 of 1,731 (the 3 skipped Topaz signature tests, as at the gate), INSVERIFYTests 7 of 7, NoHqTests 58 of 58. |
| `insverify-1-final.txt` | fixed, default order | UnitTests 1,728 of 1,731 (3 skipped), INSVERIFYTests 7 of 7, NoHqTests 58 of 58. |

All 40 InsVerify tests of UnitTests pass in both orders after the fix. The two ClaimsTests that S1-6 saw fail in a reverse-order run (after
FeesTests) passed in both reverse runs here. Full lane build of `OpenDental.Local.slnf` (Debug, x86): 0 errors.

Not run: M2INSVTests (its scratch databases are `forkmig_` ones, outside this package's naming; the coordinator's Full run covers it). Its
list tests give every patient one plan and one patient plan, so their keys are likely equal there too, but they only check which patients
are listed, and a patient was listed before the fix as well (by the benefits row). Also not run: the window itself (no window is opened in
this run) and the Middle Tier (`GetVerifyGridList` is a `Meth.GetObject` call whose shape did not change).

## Questions for the owner

1. Within one group the row kept is still the first one read (Open Dental's behaviour since 24.1.71). With two `insverify` rows for one key
   the list can show the older one, while `GetOneByFKey` and Database Maintenance's fix keep the latest; with several appointments in the
   range, which appointment's date a row shows is not defined. Open Dental 23.3.54 to 24.2.1 picked the latest verification (JobNum 53236).
   Should the list pick the latest verification and the earliest appointment of each group? Not done here: the approved fix keeps every
   other behaviour of the query.

## Release notes

A section "Insurance Verification List: no patient left out" in `docs/release-notes/next.md`.
