# Port note: M2-EMP, employee titles and future clock-outs

Branch `port/m2-emp`, built from `port/foundation` (which already holds M2-01's 26.1 schema and CRUD and M2-02's 26.1 registries) in the single-wave run. Backlog section: [port-backlog.md, M2-EMP](../specs/port-backlog.md#m2-emp-employee-titles-and-future-clock-outs). Feature specs: [features-26.1.md](../specs/features-26.1.md) (`v26_1-manage-timecard-future-clockout`, `v26_1-lists-employee-title`). No fork migration step: the schema (`employee.Title` and its key) comes from M2-01, the definition category setup from M2-02. Level range 2027004101 to 2027004199 is unused.

Sources: the public 26.1 and 26.2 release notes the specs cite, the published 26.1 database documents (names and numbers only), and the 24.3 code of this fork. No Open Dental binary or install was used, and no documentation text was copied.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v26_1-lists-employee-title` | built | Title drop-down in Edit Employee, Title column in the Employees list. Uses `employee.Title` (M2-01) and the Employee Titles definition category with its delete and hide rules (M2-02). |
| `v26_1-manage-timecard-future-clockout` | built | Edit Clock Event accepts a clock-out dated after today for TimecardsEditAll users after a confirmation, breaks still refuse it, the employee stays clocked in until then (status, Manage module buttons, clock in and out), the time card shows the date of a later-day clock-out, and the save is logged. Rules INFERRED, see below. |

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
- **Time card** (`FormTimeCard`): the Out column (In for breaks) shows the date in front of the time when it falls on a later date than the clock-in, and widens only when some row needs that. Totals count the stored times as they are.

## Files touched

- New: `OpenDentBusiness/Data Interface/ClockEvents.FutureClockOut.cs` (rules, `EnumClockEventFutureDate`), `OpenDentBusiness/Data Interface/Employees.Titles.cs`, `tests/M2EMPTests/` (project), this note.
- Owned, changed: `OpenDentBusiness/Data Interface/ClockEvents.cs` (class now `partial`; `IsClockedIn`, `ClockIn`, `ClockOut`), `OpenDentBusiness/Data Interface/Employees.cs` (class now `partial`; `UpdateClockStatus` query), `OpenDental/Forms/FormClockEventEdit.cs`, `OpenDental/Forms/FormEmployeeEdit.cs` and `.designer.cs`, `OpenDental/Forms/FormEmployeeSelect.cs`, `OpenDental/Forms/FormTimeCard.cs`.
- Outside the owned list (also in the report): `OpenDental/Main Modules/ControlManage.cs` (one condition, the hook above), `OpenDentBusiness/OpenDentBusiness.csproj` (two `Compile` lines next to `ClockEvents.cs` and `Employees.cs`), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project). No registry, enum file, preference, permission, CRUD or migration file was changed.

## INFERRED behaviours

Nothing public describes Open Dental's 26.1 rules for future clock-outs or the title column; these are this fork's choices.

- Only dates count, as in 24.3: a clock-out later today was already accepted without a question and still is. The confirmation appears only for a date after today.
- TimecardsEditAll is required for a clock-out dated after today (the window already uses it for the status list).
- Breaks keep refusing future dates on both sides; an event counts as a break if it was one or is being changed into one in the same edit.
- The confirmation adds one sentence to the spec's text: a clock-out on a later day always makes the entry span two dates, and Calc Daily (`TimeCardRules.ValidatePayPeriod`) lists such entries as errors. The user learns that before saving.
- Status text while the clock-out is ahead: "Manual Entry", the text 24.3 already used for a clock-out later today, rather than "Working". It is a clocked-in status (never Home or Lunch). Like in 24.3, it stays until the next clock-in, clock-out or clock event edit; nothing refreshes it when the time passes.
- Clocking in while a clock-out is ahead is refused, and clocking out replaces the entered clock-out with now. 24.3 let a clock-in start a second, overlapping event when a clock-out was entered for later today; that case is now refused too.
- Security log: one entry per save of a future clock-out whose times changed, under TimecardsEditAll. The spec says "as clock-event edits are logged today", but 24.3 logs only clock-event deletes, so this entry is new and limited to future clock-outs.
- The "now" for the clock buttons follows `LocalTimeOverridesServerTime`, like the stamps `ClockIn` and `ClockOut` write.
- Employees list: the Title column sits after "MI", 100 pixels wide; the search box does not search titles (display only).

## Deviations from the spec

- Rule 5 ("still shows as clocked in"): shown as "Manual Entry", see above.
- Confirmation text: one added sentence, see above.
- `FormTimeCard` gained the later-date display; the spec lists the file as a touchpoint without saying what changes.
- `ControlManage.cs` is not in the package's owned list; the one-line hook keeps the Manage module's buttons consistent with rule 5.

## Interplay with packages built at the same time

- **M2-01 / M2-02** (already on the base): this package only reads `employee.Title`, `DefCat.EmployeeTitles` and the definition rules.
- **ControlManage.cs**: no C1 package owns it. If another package edits the same `if` in `SelectEmpI` (the employee selection handler that enables the clock buttons), keep the `|| ClockEvents.HasFutureClockOut(clockEvent)` part.
- **OpenDentBusiness.csproj, OpenDental.sln, OpenDental.Local.slnf**: every package adds lines; merge the lists.
- No other C1 package lists `ClockEvents`, `Employees`, `FormClockEventEdit`, `FormEmployeeEdit`, `FormEmployeeSelect` or `FormTimeCard`.

## Tests

New project `tests/M2EMPTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `FutureClockOutTests`: the date rules (tomorrow with and without TimecardsEditAll, breaks, changing to or from a break, clock-in tomorrow, today and earlier), `HasFutureClockOut`, the time card cell text and column width, the log text.
- `EmployeeTitleTests`: DefCat 56, picker contents (order, hidden only when current, other categories and missing numbers), title text with "(hidden)".
- `EmployeeClockDbTests`: scratch MariaDB database with the 26.1.70 reference schema and a unique per-run name (`m2emp_<time>_<pid>_<random>_emp`), dropped afterwards: title save and load through `Employees`, title text and picker from the cache, in-use title cannot be deleted, `UpdateClockStatus` with a clock-out tomorrow ("Manual Entry", where 24.3 said "Home") and with past clock-outs (unchanged), clock in refused and clock out replacing the entered clock-out, past clock-outs unchanged, stored times kept.
- `WindowHookTests`: source checks that the windows call these rules (the program is never started).

Targeted run: `M2EMPTests` and `M202Tests` (Employee Titles definition rules), plus `NoHqTests` from the shared runner. Not tested: the windows themselves (no GUI run), and the Manage module buttons beyond the source check.

## Questions for the owner

1. While a clock-out is ahead, should the status read "Working" instead of "Manual Entry"? (24.3 shows "Manual Entry" for a clock-out later today; this package keeps that text for later days.)
2. A clock-out on a later day always spans two dates, which Calc Daily reports as an error. Is that acceptable, or should future clock-outs be limited to later today (which 24.3 already allowed) plus overnight shifts?
3. Should every clock-event edit be logged with old and new times (the spec assumed it already was), or only future clock-outs as now?
4. Should the Employees list search box also match titles?
