# Port note: INSVERIFY, the Insurance Verification List keeps a patient's eligibility row when the keys meet

Branch `fix/insverify-1`, cut from `main` (81e701df5). A bug fix the owner approved, as the lead recommended it. One line of SQL changes, in
`InsVerifies.GetVerifyGridListQuery`; no schema change, no enumeration value, no preference, no new file shipped. Synthetic data only.

Follow-up round **INSV2** on branch `fix/insv2-1` (from `fix/insverify-1` b6edbf00d, with `main` 14643fb03 merged in: S2-1 and TC-3): the
lead's decision on question 1, the review's corrections to this note, and the batch test's clean-up. See [INSV2](#insv2-the-latest-verification-and-the-earliest-appointment).
Still no schema change, no enumeration value, no preference.

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
- **The HAVING** is Open Dental's. It does not choose a row: `MAX(DateLastVerified)` in a condition is the date as a number (10101 for
  `0001-01-01`), so it is false only for a group whose latest date is the zero date `0000-00-00` (the column is `NOT NULL`), and such a
  group is not listed. Checked on the local MariaDB 10.11. One thing changes with the group: when the keys are equal, a patient plan's
  eligibility half and its benefits half are now groups of their own, so a half whose `DateLastVerified` is `0000-00-00` is now dropped on
  its own; before, it shared a group with the other half, and that half's date kept the group. The program never writes that date: its
  default, and `POut.Date(DateTime.MinValue)`, is `0001-01-01`, so only a row written outside the program can have it.
- **Which row of a group is kept** was still the first one read. Neither setting nor clause makes it deterministic: the program runs with a
  blank `sql_mode` (`MiscData.SetSqlMode` at start; Database Maintenance repeats it), and with `ONLY_FULL_GROUP_BY` MariaDB refuses this
  `SELECT *` with `GROUP BY` (error 1055, checked); the HAVING only filters. It decided which appointment a row shows when the patient has
  several in the range, and which of two redundant `insverify` rows is listed: the same as in Open Dental (question 1 below). INSV2 makes
  the choice deterministic.
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
| the public 23.3.1 and 24.1.1 | One row per appointment and verification; no grouping. `ORDER BY AptDateTime` at the end of the union. |
| 23.3.54 (45ba599f9), 24.1.25 (e2fd75af2), 24.2.1 (JobNum 53236) | `insverify` joined through a subquery that kept, per `VerifyType,FKey`, the rows with the latest `DateLastVerified`, before the list's conditions. Still no grouping: the list (`TableToListInsVerifyGridObjects`) kept the first row of each plan and patient plan in `AptDateTime` order, so the earliest appointment. `GetOneByFKey` got `ORDER BY DateLastVerified DESC LIMIT 1` in the same versions. No collapse of the two kinds. |
| 24.1.71, 24.2.28, 24.3.1 | The subquery removed (`insverify` joined directly, so MySQL uses the `FKey` index); the union wrapped in `SELECT * FROM (...) AS iv GROUP BY PatPlanNum,FKey HAVING MAX(DateLastVerified)`, with `ORDER BY AptDateTime` still inside. The verify type left the grouping: the bug starts here. Which row of a group is kept is no longer defined. |
| 24.1.86, 24.2.48, 24.3.15 | `ORDER BY AptDateTime` moved out of the derived table (MariaDB ignores an ORDER BY there without a LIMIT) to the end of the outer query. The grouping unchanged. |
| 24.3.63 (the last public source, the fork's base) | As 24.3.15. |
| 24.4 to 26.1 | Not public. The fork does not decompile Open Dental, and the public release notes and the feature specs written from them say nothing about this list's rows. |

So there was no fix of Open Dental's to port: this is the fork's own. The fork's file is 24.3.63's for the grouping; M2-INSV (25.4's day
range and carrier pick) changed the dates and the carrier condition only. INSV2 brings back the choice of 23.3.54 to 24.2.1 (the latest
verification, the earliest appointment) on 24.1.71's direct, indexed join.

## Tests

New project `tests/INSVERIFYTests` (MSTest v1, x86, SDK-style on `tests/Directory.Build.props`; in `OpenDental.sln` and
`OpenDental.Local.slnf`, wired in its own commit). It references the business library and UnitTestsCore (for the batch verification's
setup), not the program.

- `InsvDb`: the scratch database `insv2_<run id>_main` on 127.0.0.1:3306 (`insv_` before INSV2): the committed `db/init/new-database.sql`,
  then `ForkMigrationRunner.Run()`; dropped after the run (`INSVERIFY_KEEP_DB=1` keeps it); leftovers of earlier `insv2_` runs older than 12
  hours are dropped; skipped (inconclusive) when the server does not answer or `INSVERIFY_TEST_DB=skip`. No other database is touched.
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
    `DateLastVerified` today) and the benefits row is left alone. Since INSV2 it reads the preferences it changes first and writes them back
    in a `finally` block (see INSV2).
- `QuerySourceTests` (no database): the outer `GROUP BY` line, `HAVING` and `ORDER BY` included, as it is now; and both parts of the union
  select `insverify.*` and join `insverify` on one verify type each. INSV2 changed the first check to the new shape (below).

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
   other behaviour of the query. **Decided by the lead (INSV2): yes, as 23.3.54 to 24.2.1 did; done in the next section.**

## INSV2: the latest verification and the earliest appointment

The lead's decision on question 1: each row shows the **latest verification** of its patient plan, verify type and key and the patient's
**earliest appointment** in the date range, as Open Dental 23.3.54 to 24.2.1 did (JobNum 53236), keeping the two parts of the union that let
MySQL and MariaDB use `insverify.FKey` as an index (the reason for 24.1.71) and the HAVING's filter.

### The query

- **The latest verification.** Both parts of the union join `insverify` a second time, as `insverifylater`, on the same `VerifyType` and
  `FKey` for a row verified later, or verified on the same date and inserted later (a higher `InsVerifyNum`), and keep the row only when
  there is none (`insverifylater.InsVerifyNum IS NULL`, in `whereClause`, which both parts end with). The tie is the one `GetOneByFKey`
  (`ORDER BY DateLastVerified DESC,InsVerifyNum DESC`) and Database Maintenance's `InsVerifyRedundantEntries` (P262-TOOLS) break the same way.
  As in 24.2.1, the latest row is chosen before the list's conditions (due date, year checks, assigned user, status), which apply to it: a
  key whose latest verification is not due is not listed for an older row that is, and a user or status filter looks at the latest row.
  The second join is on `FKey` like the first, so both use the index; 24.2.1 joined a subquery grouping the whole table instead.
- **The earliest appointment.** `mainQuery` numbers the rows of each patient plan, verify type and key by `AptDateTime`, then `AptNum`
  (`ROW_NUMBER() OVER(PARTITION BY patplan.PatPlanNum,insverify.VerifyType,insverify.FKey ORDER BY appointment.AptDateTime,appointment.AptNum)
  AS AptOrder`), and the outer query keeps `AptOrder=1`. The numbering is done in each part of the union: each part joins `insverify` on one
  verify type, so no group spans both parts. Only appointments that pass the list's conditions are numbered (in the range, scheduled or
  complete, the clinic filter, and with a year check the verification due by that appointment's date). The appointment's clinic comes with it.
- **No GROUP BY.** With one row left per group, the outer query is
  `SELECT * FROM (...) AS iv WHERE AptOrder=1 AND DateLastVerified ORDER BY AptDateTime,AptNum,PatPlanNum,VerifyType`. A `GROUP BY` on
  top changed nothing but cost a temporary table (about 65 ms of the 7-day query below, in one comparison of medians), so it went.
- **The HAVING's filter stays**, as `DateLastVerified` alone: on the one row of a group it is exactly `HAVING MAX(DateLastVerified)`, false
  only for the zero date `0000-00-00` (checked on MariaDB 10.11: `0000-00-00` false; `0001-01-01`, `0000-01-01`, `2020-00-00`, a real date
  true, in `WHERE d` and in `GROUP BY ... HAVING MAX(d)` alike).
- **The order is complete**: `AptDateTime`, then `AptNum`, `PatPlanNum`, `VerifyType` (a row's key follows from its patient plan and verify
  type). `TableToListInsVerifyGridObjects` lists a plan's benefits row once, with the first of its rows, so two family members on one plan
  with appointments at the same time no longer decide by read order whose row carries it: the lower `AptNum`.
- **Server version.** `ROW_NUMBER() OVER` needs MariaDB 10.2 or MySQL 8.0; it is the first window function in the program. The program's
  server is MariaDB 10.11 (README, `docs/adoption-runbook.md`), but the start-up check (`PrefL.CheckMySqlVersion`) still accepts MySQL 5.0
  and later: on an older server this list would fail with an SQL error. A question for the lead below.

What a user sees: a plan or patient plan with a leftover second verification record shows the latest one, and is not listed as due when
that one is recent; a patient with several appointments in the range shows the earliest (and its clinic). The batch verification works from
the same rows, so it verifies (updates) the latest record of a key.

### Cost

A synthetic data set in the scratch database `insv2_perf_261109` (dropped afterwards; scripts in the session's scratch folder, not
committed): the nine tables the query reads, from the committed `db/init/new-database.sql`; 20,000 patients, each with one patient plan;
16,000 plans of 200 carriers (4,000 shared by two patients; for those 16,000 the `PlanNum` equals the `PatPlanNum`); 3 clinics; 38,000
`insverify` rows (an eligibility row per patient plan, a benefits row per plan, 1,000 redundant rows of each kind; one in ten never
verified; the rest verified up to 400 days ago); 200,000 appointments over the year before and after today (274 a day; past ones complete,
future ones scheduled, one in twenty broken; clinics 0 to 3). In the default range of 7 days (`InsVerifyAppointmentScheduledDays`: today
through day 7) that is 2,192 appointments, 98 patients with more than one; in 60 days 16,714 and 3,766. The query as
`GetVerifyGridListQuery` writes it with every filter on "All", plans without a filing code, and verifications older than 30 days for both
kinds (the defaults are 30 days for eligibility, 90 for benefits); "old" is `fix/insverify-1`, "new" this round. Server time of the statement (`ANALYZE FORMAT=JSON`, `r_total_time_ms`, no rows sent), old and new
interleaved, on the development PC's MariaDB 10.11.19 (MyISAM; other work was running on the PC, so the minimum is the steadier figure):

| Range, year checks | Rows old / new | Old: min, median | New: min, median | Runs |
|---|---|---|---|---|
| 7 days, off (the default) | 3,712 / 3,685 | 289 ms, 336 ms | 347 ms, 373 ms | 15 each |
| 7 days, both on | 3,727 / 3,701 | 290 ms, 329 ms | 323 ms, 376 ms | 15 each |
| 60 days, off | 20,469 / 20,290 | 2,177 ms, 2,311 ms | 2,316 ms, 2,604 ms | 9 each |

The new query costs one more index lookup per row of each part (the second `insverify` join) and the numbering (a sort of each part's
rows): 33 to 58 ms in the default range (11 to 20 per cent), 140 to 290 ms in 60 days (6 to 13 per cent); it grows with the rows in the
range, not with the table. For scale, an office with 50 to 100 appointments a day has a fifth to a third of these rows, so a few
milliseconds to about 20 more in its default range. Two variants measured and not taken: the second join in the outer query on the numbered
rows instead of in each part (the same time; fewer lookups only when patients have several appointments), and 24.2.1's subquery of the
latest dates (20 to 40 ms less than the new query here, about 20 ms more than the old one; but it reads the whole `insverify` table in each
part, which grows with the office's history, and it is what 24.1.71 removed).

The rows the new query leaves out are exactly the keys whose latest verification is not due (27 in 7 days, 179 in 60 days); it adds none.
Of the rows both list, the old query showed an older verification than the latest for 113 (7 days) and 571 (60 days) keys: MariaDB read
the first inserted row of a key first. It happened to show the earliest appointment for every row here: the range scan reads scheduled
appointments by date, and the 7- and 60-day ranges hold no completed ones; a range that does (from a past date) reads them in another
order.

`EXPLAIN` (7 days, year checks off). Old, each part (`DERIVED` and `UNION`): `appointment` range on `StatusDate`; `clinic`, `patient`,
`inssub`, `insplan`, `carrier` `eq_ref` on their primary keys; `patplan` `ref` on `indexPatNum`; `insverify` `ref` on `FKey` (rows 2,
"Using where"); then `<derived2>` ALL, "Using temporary; Using filesort" (the GROUP BY and the ORDER BY). New, each part: the same, then
`insverifylater` `ref` on `FKey` (rows 2, "Using where; Not exists"), and "Using temporary" on `appointment` for the numbering; then
`<derived2>` ALL, "Using where; Using filesort" (no temporary table for a GROUP BY). Both `insverify` joins use the `FKey` index in both
parts.

### Tests

New in `tests/INSVERIFYTests` (`VerifyGridChoiceTests.cs`, a second part of the class `VerifyGridKeyTests`, so the same helpers; each key
gets its rows in the order the test names, the rows inserted with the plan and the patient plan deleted first):

- `GridList_TwoRowsForOneKey_ShowsTheLatestVerification_LatestInsertedFirst` and `..._LatestInsertedSecond`: both keys of a patient whose
  keys are equal get two due rows (verified 100 and 40 days ago); the list shows the latest of each, whichever was inserted first.
- `GridList_TwoRowsOnTheSameDate_ShowsTheRowInsertedLater`: the tie.
- `GridList_LatestVerificationNotDue_KeyNotListedForAnOlderRow`: the eligibility verified 5 days ago and an older due row inserted after
  it: no eligibility row, the benefits row alone.
- `GridList_PlanNumEqualsPatPlanNum_ALaterBenefitsVerificationDoesNotHideTheEligibilityRow`: the latest is per verify type: a later
  benefits row with the same `FKey` does not rule out the eligibility row.
- `GridList_SeveralAppointments_ShowsTheEarliestInTheRange`: four more appointments, created after the first: a later one, the earliest
  (in its own clinic), a broken one earlier in the range and a completed one yesterday; both verifications show the earliest and its clinic.
- `GridList_TwoAppointmentsAtTheSameTime_ShowsTheLowerAptNum`.
- `GridList_PlanOfTwoPatientsAtTheSameTime_TheBenefitsRowGoesWithTheLowerAptNum`: a family member on the subscriber's plan with an
  appointment at the same time, created after: the subscriber's row is the combined one, the family member's the eligibility alone.
- `Query_TwoRowsForOneKeyAndSeveralAppointments_OneRowWithTheLatestVerificationAndTheEarliestAppointment`: the query itself: two rows for
  the patient plan, benefits first, each with the latest verification and the earliest appointment.
- `QuerySourceTests`: `GetVerifyGridListQuery_OneRowPerPatientPlanVerifyTypeAndKey` (no `GROUP BY`; the numbering line; the outer query,
  its filter and order) replaces the `GROUP BY` check; `GetVerifyGridListQuery_BothUnionPartsKeepOnlyTheLatestVerificationOfTheirKey` pins
  the second join and its `IS NULL` in both parts.

The review's point on test hygiene: `BatchVerify_PlanNumEqualsPatPlanNum_VerifiesTheEligibilityRow` changed `PracticeDefaultProv`, the
practice phone and address (`PracticeAddress`, `PracticeCity`, `PracticeST`, `PracticeZip`), `ClearinghouseDefaultEligibility` and the five
`InsBatchVerify*` preferences in the shared scratch database and left them for later tests. It now reads them from the database first,
writes them back in a `finally` block (and refreshes the cache), and checks database and cache afterwards. The provider and clearinghouse it
creates stay, like every test's rows.

| Run (`.git/od-tools/test-results/`) | Query | Result |
|---|---|---|
| `insv2-1-before-fix.txt` | INSVERIFY's | INSVERIFYTests 8 of 17: the 6 unchanged tests of INSVERIFY pass (the batch test with its clean-up); the 2 source checks and 7 of the 9 new database tests fail as expected. `..._LatestInsertedFirst` and the shared-plan test pass by read order (MariaDB happens to read the first inserted row and the lower `AptNum` first). |
| `insv2-1-after-fix.txt` | INSV2 | INSVERIFYTests 17 of 17, NoHqTests 58 of 58. |
| `insv2-1-final.txt` | INSV2, after the full build | INSVERIFYTests 17 of 17, M2INSVTests 17 of 17, NoHqTests 58 of 58, UnitTests 1,728 of 1,731 (the 3 skipped Topaz signature tests); all 40 InsVerify tests of UnitTests pass. |

M2INSVTests ran this round (its `forkmig_` scratch databases are allowed here) and passes. Full lane build of `OpenDental.Local.slnf` (Debug,
x86): 0 errors. Not run: the window itself (no window is opened) and the Middle Tier (`GetVerifyGridList`'s shape did not change).

After merging `main` (S2-1), `tests/INSVERIFYTests/packages.lock.json` was deleted and the lane's locked restore wrote it again in version 2
(the packages of the business library and UnitTestsCore, `System.ValueTuple` as Transitive), so the locked restore of
`OpenDental.Local.slnf` (`tools/Build-Release.ps1`) does not stop at NU1004. The project file already had S2-1's shape, as
`tests/InventoryTests`: no package of its own, MySqlConnector a HintPath until S2-2.

### For the lead

1. The window function needs MariaDB 10.2 or MySQL 8.0, while `PrefL.CheckMySqlVersion` lets the program start on MySQL 5.0 and later. The
   office runs MariaDB 10.11, so nothing changes there; should the start-up check (or the release notes) name MariaDB 10.2 / MySQL 8.0 as
   the minimum? Without window functions the choice of appointment would need a join back to the appointment per row or a choice in C#.

## Release notes

A section "Insurance Verification List: no patient left out" in `docs/release-notes/next.md`; INSV2 added its second paragraph (the latest
verification and the earliest appointment).
