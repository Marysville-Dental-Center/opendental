# Port note: M2-EMP, employee titles and future clock-outs

Branch `port/m2-emp` (fix round s1 on `port/m2-emp-s1`: the printed time card; fix round t3 on `port/m2-emp-t3`: Database Maintenance), built from `port/foundation` (which already holds M2-01's 26.1 schema and CRUD and M2-02's 26.1 registries) in the single-wave run. Backlog section: [port-backlog.md, M2-EMP](../specs/port-backlog.md#m2-emp-employee-titles-and-future-clock-outs). Feature specs: [features-26.1.md](../specs/features-26.1.md) (`v26_1-manage-timecard-future-clockout`, `v26_1-lists-employee-title`). No fork migration step: the schema (`employee.Title` and its key) comes from M2-01, the definition category setup from M2-02. Level range 2027004101 to 2027004199 is unused.

Sources: the public 26.1 and 26.2 release notes the specs cite, the published 26.1 database documents (names and numbers only), and the 24.3 code of this fork. No Open Dental binary or install was used, and no documentation text was copied.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v26_1-lists-employee-title` | built | Title drop-down in Edit Employee, Title column in the Employees list. Uses `employee.Title` (M2-01) and the Employee Titles definition category with its delete and hide rules (M2-02). |
| `v26_1-manage-timecard-future-clockout` | built | Edit Clock Event accepts a clock-out dated after today for TimecardsEditAll users after a confirmation, breaks still refuse it, the employee stays clocked in until then (status, Manage module buttons, clock in and out), the time card shows the date of a later-day clock-out, the save is logged, and Database Maintenance no longer counts or overwrites such a clock-out. Rules INFERRED, see below. |

## What was built

### Employee titles (`v26_1-lists-employee-title`)

- **Edit Employee** (`FormEmployeeEdit`): a "Title" drop-down under "Reports to", with "None" plus the visible titles in definition order. A hidden title is listed (with "(hidden)") only when it is the employee's current one. Saving stores the definition number in `employee.Title`; "None" stores 0. The two check boxes below moved down one row to make room.
- **Employees list** (`FormEmployeeSelect`): a "Title" column after "MI" showing the title's name, with "(hidden)" when the title is hidden, and blank for no title or a number that is not an Employee Titles definition. The Export button includes the column.
- **Setup, Definitions**: nothing new here. M2-02 already lists the category (add, rename, hide, reorder, delete) and refuses to delete a title an employee uses; M2-01's question 5 (hide the category until this package ships) no longer applies.
- Business layer: `Employees.GetTitlesForPicker` and `Employees.GetTitleText` in the new partial file `Employees.Titles.cs`. Nothing else reads the title (display only, as the spec says).

### Future clock-outs (`v26_1-manage-timecard-future-clockout`)

- **Edit Clock Event** (`FormClockEventEdit`): the rules live in `ClockEvents.CheckFutureDates` (new partial file `ClockEvents.FutureClockOut.cs`); the window calls it where it used to refuse a second time dated after today.
  1. The first time (clock-in, or start of a break) dated after today is refused, as before.
  2. A clock-out dated after today on a Home or Lunch event is allowed for users with TimecardsEditAll, after "Clock-out is in the future. Save anyway?". The confirmation comes after every other check of the window, just before the save.
  3. Without TimecardsEditAll, or on a break (or an event being changed to or from a break), the old refusal stays, with the old messages.
  4. Clock-out after clock-in, the pay period limit and the overtime and adjustment checks are unchanged.
  5. When the times changed, the save writes a security log entry (TimecardsEditAll) with the employee and the old and new clock-in and clock-out times.
- **Clock status** (`Employees.UpdateClockStatus`): the 24.3 query ignored any clock-out after 23:59:59 today, so a clock-out entered for tomorrow made the employee show as "Home" from an older event. The bound is gone; such an event now sorts first and shows "Manual Entry", which is what 24.3 already showed for a clock-out later today (the employee is still clocked in until then).
- **Clock buttons** (`ClockEvents`): an event whose clock-out is still ahead counts as clocked in. `IsClockedIn` returns true; `ClockIn` refuses ("Already clocked in until ...") instead of starting a second, overlapping event; `ClockOut` for Home or Lunch replaces the entered clock-out with now (the employee leaves earlier than entered), and a Break starts a break as usual.
- **Manage module** (`ControlManage.SelectEmpI`, one-line hook): for such an event the Clock Out button and status list are enabled and Clock In is disabled, as for any clocked-in employee.
- **Database Maintenance** (`DatabaseMaintenances.ClockEventInFuture`, fix round t3): the 24.3 check counted every clock-out more than 15 minutes ahead as "Time card entries invalid", and Fix set it to the time of the run. Run after a clock-out was entered for a later day, Fix cut the shift short (for example 08:00 to tomorrow 06:00 became 08:00 to 16:00 when Fix ran at 16:00), the employee then counted as clocked out, and nothing kept the old time. Now the clock-out half of the check (`TimeDisplayed2` more than 15 minutes ahead), in Check and in Fix, covers only breaks (`ClockStatus=2`), whose second time is the return and which `CheckFutureDates` and `HasFutureClockOut` still refuse. The clock-in half (`TimeDisplayed1`) is unchanged: a clock-in still may not be in the future, on any event.
- **Time card** (`FormTimeCard`): the Out column (In for breaks) shows the date in front of the time when it falls on a later date than the clock-in, and widens only when some row needs that. Totals count the stored times as they are.
- **Printed time card** (`FormTimeCard.pd_PrintPage`, fix round s1): the printout draws every cell one line high, and its Out/In column is 60 units (hundredths of an inch). The first build printed the screen's "10/26/2026 6:00 AM" there, which wrapped at the space and left only the date on paper, for overnight shifts of 24.3 data too. Now:
  1. A clock event's second time prints time first, then the date of a later day ("6:00 AM 10/26/2026", `ClockEvents.GetSecondTimePrintText`), through the new `GetPrintCellText`. Same-day rows print the time alone, as in 24.3.
  2. When some event shows such a date, the Out/In column widens until the widest of those texts fits, measured with the print font, and the Note column (the last one) gives up the same width, so the printout keeps its total width inside the margins (`ClockEvents.GetSecondTimePrintWidthMoved`). The Note column keeps at least 30 units, room for its header.
  3. Without later-day rows nothing changes: same widths and text as 24.3.

  On en-US the whole text fits without clinics (Note 130 becomes about 70, or about 55 with seconds). With clinics (Note 80) the column grows by at most 50, so a long one such as "11:59 PM 12/31/2026" wraps its date away. The time still prints in full, because it comes first.

## Files touched

- New: `OpenDentBusiness/Data Interface/ClockEvents.FutureClockOut.cs` (rules, `EnumClockEventFutureDate`), `OpenDentBusiness/Data Interface/Employees.Titles.cs`, `tests/M2EMPTests/` (project), this note.
- Owned, changed: `OpenDentBusiness/Data Interface/ClockEvents.cs` (class now `partial`; `IsClockedIn`, `ClockIn`, `ClockOut`), `OpenDentBusiness/Data Interface/Employees.cs` (class now `partial`; `UpdateClockStatus` query), `OpenDental/Forms/FormClockEventEdit.cs`, `OpenDental/Forms/FormEmployeeEdit.cs` and `.designer.cs`, `OpenDental/Forms/FormEmployeeSelect.cs`, `OpenDental/Forms/FormTimeCard.cs`.
- Outside the owned list (also in the report): `OpenDental/Main Modules/ControlManage.cs` (one condition, the hook above), `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs` (fix round t3: two conditions in `ClockEventInFuture`, the Check and Fix queries on `TimeDisplayed2`, now limited to breaks), `OpenDentBusiness/OpenDentBusiness.csproj` (two `Compile` lines next to `ClockEvents.cs` and `Employees.cs`), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project). No registry, enum file, preference, permission, CRUD or migration file was changed.

## INFERRED behaviours

Nothing public describes Open Dental's 26.1 rules for future clock-outs or the title column; these are this fork's choices.

- Only dates count, as in 24.3: a clock-out later today was already accepted without a question and still is. The confirmation appears only for a date after today.
- TimecardsEditAll is required for a clock-out dated after today (the window already uses it for the status list).
- Breaks keep refusing future dates on both sides; an event counts as a break if it was one or is being changed into one in the same edit.
- The confirmation adds one sentence to the spec's text: a clock-out on a later day always makes the entry span two dates, and Calc Daily (`TimeCardRules.ValidatePayPeriod`) lists such entries as errors. The user learns that before saving.
- Status text while the clock-out is ahead: "Manual Entry", the text 24.3 already used for a clock-out later today, rather than "Working". It is a clocked-in status (never Home or Lunch). Like in 24.3, it stays until the next clock-in, clock-out or clock event edit; nothing refreshes it when the time passes.
- Clocking in while a clock-out is ahead is refused, and clocking out replaces the entered clock-out with now. 24.3 let a clock-in start a second, overlapping event when a clock-out was entered for later today; that case is now refused too.
- Security log: one entry per save of a future clock-out whose times changed, under TimecardsEditAll. The spec says "as clock-event edits are logged today", but 24.3 logs only clock-event deletes, so this entry is new and limited to future clock-outs.
- Database Maintenance (`ClockEventInFuture`): a clock-out more than 15 minutes ahead is invalid only on a break, the one event type that still refuses a future second time; Home and Lunch clock-outs ahead are left alone in Check and in Fix. A clock-in more than 15 minutes ahead stays invalid on every event, as in 24.3. The 15-minute margin and the messages are unchanged. Nothing checks whether the user who entered a future clock-out had TimecardsEditAll; the window enforces that when saving.
- The "now" for the clock buttons follows `LocalTimeOverridesServerTime`, like the stamps `ClockIn` and `ClockOut` write.
- Employees list: the Title column sits after "MI", 100 pixels wide; the search box does not search titles (display only).
- Printed time card: the date of a later-day second time follows the time (the screen puts it first, where right alignment lines the times up). The room comes from the Note column only, down to 30 units; Clinic and the other columns keep their 24.3 widths.

## Deviations from the spec

- Rule 5 ("still shows as clocked in"): shown as "Manual Entry", see above.
- Confirmation text: one added sentence, see above.
- `FormTimeCard` gained the later-date display, on screen and on paper; the spec lists the file as a touchpoint without saying what changes.
- `ControlManage.cs` is not in the package's owned list; the one-line hook keeps the Manage module's buttons consistent with rule 5.

## Interplay with packages built at the same time

- **M2-01 / M2-02** (already on the base): this package only reads `employee.Title`, `DefCat.EmployeeTitles` and the definition rules.
- **ControlManage.cs**: no C1 package owns it. If another package edits the same `if` in `SelectEmpI` (the employee selection handler that enables the clock buttons), keep the `|| ClockEvents.HasFutureClockOut(clockEvent)` part.
- **DatabaseMaintenances.cs**: shared by many packages. If another package edits `ClockEventInFuture`, keep the `whereBreak` condition on both `TimeDisplayed2` queries; without it Fix overwrites future clock-outs.
- **OpenDentBusiness.csproj, OpenDental.sln, OpenDental.Local.slnf**: every package adds lines; merge the lists.
- No other C1 package lists `ClockEvents`, `Employees`, `FormClockEventEdit`, `FormEmployeeEdit`, `FormEmployeeSelect` or `FormTimeCard`. B-TIME (wave B1) also changed `FormTimeCard`; see Integration below.

## Tests

New project `tests/M2EMPTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `FutureClockOutTests`: the date rules (tomorrow with and without TimecardsEditAll, breaks, changing to or from a break, clock-in tomorrow, today and earlier), `HasFutureClockOut`, the time card cell text and column width, the log text. The printed time card: the print text (time first), the width moved from the Note column (none without a later-day row, rounding, never negative, the Note minimum), and a measurement with the real print font in printer units on en-US. That last test shows the first build's text leaving only the date in the 60-unit column. It then checks that the clock-out time always prints in full, and that without clinics the date fits too, with and without seconds.
- `EmployeeTitleTests`: DefCat 56, picker contents (order, hidden only when current, other categories and missing numbers), title text with "(hidden)".
- `EmployeeClockDbTests`: scratch MariaDB database with the 26.1.70 reference schema and a unique per-run name (`m2emp_<time>_<pid>_<random>_emp`), dropped afterwards: title save and load through `Employees`, title text and picker from the cache, in-use title cannot be deleted, `UpdateClockStatus` with a clock-out tomorrow ("Manual Entry", where 24.3 said "Home") and with past clock-outs (unchanged), clock in refused and clock out replacing the entered clock-out, past clock-outs unchanged, stored times kept. Fix round t3: `DatabaseMaintenance_FutureClockOut_NotCountedAndNotChanged` runs `DatabaseMaintenances.ClockEventInFuture` in Check and Fix: a Home and a Lunch event with a clock-out tomorrow are not counted, Fix leaves both clock-outs as they were and the employee stays clocked in; with a future clock-in and a break whose return is two hours ahead added, Check counts 2, Fix corrects those two (to the time of the run) and still leaves the Home and Lunch clock-outs unchanged.
- `WindowHookTests`: source checks that the windows call these rules (the program is never started), including that `pd_PrintPage` widens the Out/In column before laying out the columns and prints every cell through `GetPrintCellText`.

Targeted run: `M2EMPTests` and `M202Tests` (Employee Titles definition rules), plus `NoHqTests` from the shared runner. Not tested: the windows themselves (no GUI run), an actual printout (the print test measures with a screen graphics object set to printer units, not a printer), and the Manage module buttons beyond the source check.

## Integration (branch `port/m2-emp-t1`, on `port/int-7`)

`port/int-7` merged into the package branch. Two text conflicts and one semantic one, all with B-TIME (`docs/port-notes/B-TIME.md`), which changed `FormTimeCard` in the same wave:

- `OpenDentBusiness.csproj`: both packages' `Compile` lines kept (`ClockEvents.FutureClockOut.cs` and B-TIME's `TimeCardTotals.cs`, `Employees.Titles.cs` and B-TIME's `EmployeeReportsTo.cs`).
- `FormTimeCard.cs`: this package's `GetPrintCellText` and B-TIME's `PrintTotalsRow` both kept. The printed totals row takes its positions from `intArrayColPos`, which already includes the Out/In column widened above, so the totals print under their columns as they are.
- `FormTimeCard.FillGridTotals` (B-TIME's on-screen Totals for Period grid): its first column was a fixed `70+45+64+64`, the 24.3 widths of Date, Day, In and Out. With a later-day clock-out this package widens the second-time column to 130, which would have left every total 66 pixels left of its column. The spacer now adds up `gridMain`'s own first four column widths (the fixed sum stays as the fallback when the grid has fewer columns). `FillMain` sets the columns before it calls `FillGridTotals`, so the widths are current. New source check `WindowHookTests.TimeCard_TotalsStayUnderTheirColumns`.
- B-TIME's totals count a future clock-out as a completed event (Total and Day), the same as the time card grid's own Day and Week cells, as B-TIME's note says. Nothing to change.
- `ControlManage.cs`: B-TIME's hook is in `FillEmps`, this package's in `SelectEmpI`; both merged on their own.
- Still no fork migration step and no schema or row change, so the adoption tools (migration-preview, AdoptionRowCheck, allowed differences) need nothing from this package.

Checks (2026-10-31): `M2EMPTests` (34) and `BTIMETests` (28) targeted; the full build through the lane succeeded; `run-tests -Mode Core` passed, 61 assemblies (`test-results/port_m2-emp-t1.txt`); `tools/Test-NoHqHosts.ps1` passed. The slow migration and adoption assemblies were not run: this package changes no migration, schema, TableType, CRUD or adoption code.

## Integration and fix round t3 (branch `port/m2-emp-t3`, on `port/int-9`)

`port/int-9` (8d077f202) merged into `port/m2-emp-t1` without conflicts. None of the packages merged since `port/int-7` (B-CLAIM, M2-TERM, M2-SHEETS, V-EDS, M2-CLAIMRCV) changed `ClockEvents`, `Employees`, `FormClockEventEdit`, `FormEmployeeEdit`, `FormEmployeeSelect`, `FormTimeCard`, `ControlManage` or `DatabaseMaintenances`, and none added a helper this package duplicates (their scratch-database helpers stay per test project, as this one's does).

Review finding fixed: Database Maintenance's `ClockEventInFuture` counted and overwrote future clock-outs (see "Database Maintenance" under What was built). Still no fork migration step and no schema or row change: the DBM fix changes rows only when a user runs Fix, as 24.3 did, so the adoption tools need nothing from this package.

## Questions for the owner

1. While a clock-out is ahead, should the status read "Working" instead of "Manual Entry"? (24.3 shows "Manual Entry" for a clock-out later today; this package keeps that text for later days.)
2. A clock-out on a later day always spans two dates, which Calc Daily reports as an error. Is that acceptable, or should future clock-outs be limited to later today (which 24.3 already allowed) plus overnight shifts?
3. Should every clock-event edit be logged with old and new times (the spec assumed it already was), or only future clock-outs as now?
4. Should the Employees list search box also match titles?
5. Printed time card with clinics: should the Clinic column also give room, so a long later-day date always prints in full, or is the time alone acceptable there?
