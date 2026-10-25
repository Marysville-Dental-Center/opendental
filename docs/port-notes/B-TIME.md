# Port note: B-TIME, time clock and time cards

Package B-TIME (M1 Tier B, wave B1) from [../specs/port-backlog.md](../specs/port-backlog.md). Built on `port/b-time` from `m1/integration-a2-patch`, in the single-wave run.

Sources: the public 25.1 and 25.3 release notes, the public 24.4 manual pages for the time clock and the time card, and the feature specs in [../specs/features-25.1.md](../specs/features-25.1.md) and [../specs/features-25.3.md](../specs/features-25.3.md). The public release notes say only that the Time Card "Totals for Period" section changed (25.1) and that the time clock got a Reports To filter (25.3); every detail beyond that comes from the specs or is marked INFERRED below.

No schema change, no preference, no permission, no fork migration step (the assigned level range 2027001201-2027001299 is unused).

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v25_1-manage-timecard-totals-grid` | built | The Time Card window's total boxes are replaced by a one-row "Totals for Period" grid with a Decimal Format / Colon Format choice; the Breaks view and printing use it too. |
| `v25_3-manage-timeclock-reportsto` | built | The Manage module's employee time clock has a Reports To filter that shows a supervisor's direct reports. |

### v25_1-manage-timecard-totals-grid: built

- `FormTimeCard`: the 18 total boxes and labels (Regular Time, Overtime, Rate2, Rate3, PTO, Protected Leave, each in h:mm and decimal) are removed. In their place, under the time card grid:
  - `gridTotals`, titled "Totals for Period", one row. A blank first column as wide as Date, Day, In and Out, then Total, Adjust, Rate2, Rate3, PTO, OT, PL and Day, at the time card grid's widths, so each total sits under its column. Day is the period's regular time (24.3's "Regular Time" box).
  - Two radio buttons, **Decimal Format** and **Colon Format**.
- The totals come from `TimeCardTotals.Calculate` (new, `OpenDentBusiness/Data Interface/TimeCardTotals.cs`). It uses 24.3's math: only completed clock events count toward Total and Day. Adjust is the override, else the automatic value, plus regular-hours adjustments that are neither PTO nor protected leave. Rate2, Rate3 and OT are each the override, else the automatic value, and OT also adds adjustment overtime. Clock event overtime comes out of Day. PTO and protected leave come from adjustments.
- Format: each time the window opens, it starts from `TimeCardsUseDecimalInsteadOfColon` (`TimeCardTotals.GetDefaultFormat`). Switching only changes this window and never writes the preference. Colon shows seconds only when `TimeCardShowSeconds` is on. Decimal is hours with two decimals.
- The Breaks view shows the same grid, with the totals of the break events. 24.3 hid the totals there.
- Print: the totals print as one row under the printed columns (label "Totals for Period", then each total under its column), in the chosen format, on every page as before.

### v25_3-manage-timeclock-reportsto: built

- `ControlManage`: a **Reports To** label and combo sit above the employee grid, under **Filter by Name**. The grid moved down 22 pixels to make room.
- Options: **All**, then each non-hidden employee that at least one employee in the time clock list reports to, sorted by name (`EmployeeReportsTo.GetSupervisors`, new, `OpenDentBusiness/Data Interface/EmployeeReportsTo.cs`).
- Choosing a supervisor shows only that supervisor's direct reports (`employee.ReportsTo` = the supervisor). This works together with the clinic filter (which picks the list) and the name filter.
- The choice is remembered per user for the session, in memory only. A supervisor who is no longer offered (hidden, deleted, or with no reports left in the list) falls back to All, and the remembered choice is cleared.
- The filter only changes which rows the grid shows. As with the name filter, the list passed to Time Card and Manage is unchanged.

## Files touched

New:

- `OpenDentBusiness/Data Interface/TimeCardTotals.cs`: `EnumTimeCardTotalsFormat` (window state only, never stored), `TimeCardPeriodTotals`, `TimeCardTotals`.
- `OpenDentBusiness/Data Interface/EmployeeReportsTo.cs`
- `OpenDental/Main Modules/ControlManage.ReportsTo.cs`: a partial class with the filter's UI code, so `ControlManage.cs` only gets a two-line hook in `FillEmps`.
- `tests/BTIMETests/` (new MSTest v1 project, x86, .NET 4.8) and this note.

Changed:

- `OpenDental/Forms/FormTimeCard.cs`, `FormTimeCard.designer.cs`
- `OpenDental/Main Modules/ControlManage.cs` (hook in `FillEmps` only) and `ControlManage.designer.cs` (two new controls; `gridEmp` moved from y 40, height 220 to y 62, height 198). The designer file has mixed line endings in git, and they were kept byte for byte.
- `OpenDentBusiness/OpenDentBusiness.csproj` and `OpenDental/OpenDental.csproj`: one `Compile` line each per new file.
- `OpenDental.sln` and `OpenDental.Local.slnf`: the new test project.

Not touched, though listed under Owns: `ClockEvents.cs`, `Employees.cs`, `TimeCardRules.cs`, `FormTimeCardManage.cs`, `FormTimeCardSetup.cs`. The spec suggests giving `ClockEvents.Format` a variant that takes the format as a parameter. Instead, `TimeCardTotals.FormatTotal` lives in the new file, so `ClockEvents.cs` (also owned by M2-EMP in this wave) stays untouched.

## INFERRED behaviors (public sources do not say)

1. The format choice changes only the totals grid and the printed totals row. The time card grid's cells keep following the preference through `ClockEvents.Format`, as in 24.3.
2. The choice is not remembered between openings; the window always starts from the preference (as the spec proposes).
3. A zero total shows `0:00` (or `0:00:00` with seconds) and `0.00`, not a blank cell, so the totals row always shows every figure. 24.3's colon boxes were blank for zero, while its decimal boxes showed 0.00.
4. Colon totals drop seconds rather than rounding to the nearest minute, the same as 24.3's boxes (`ToStringHmm`). The public manual says "rounded to the nearest minute", but the 24.3 code truncates, and the spec says to keep the 24.3 math.
5. The totals row is printed in the columns rather than as the 24.3 list of labeled lines. Like 24.3, it is printed on every page.
6. Reports To lists direct reports only. It does not list the supervisor themselves or indirect reports (the spec says VERIFY).
7. Supervisors are offered only when someone in the current time clock list (after the clinic filter) reports to them. A supervisor at another clinic is still offered when their reports are in this list. An employee whose Reports To points at themselves is not a supervisor.
8. The remembered choice lives in memory per user (`Security.CurUser.UserNum`) for the session. It is not saved to `userodpref`: that needs a new `UserOdFkeyType` value, and the enum belongs to the registry packages.
9. Reports To is not added to the Manage Time Cards window (`FormTimeCardManage`). The 25.3 release notes mention the Employee Time Clock only (the spec says VERIFY).
10. Manage Time Cards' Print All and Print Selected still print 24.3's labeled totals in both formats. The 25.1 note covers the Time Card window only.

## Deviations from the spec

- No `ClockEvents.Format(TimeSpan,format)` overload; `TimeCardTotals.FormatTotal` does that job (see Files touched).
- The totals grid's first column is blank. The spec lists only the numeric columns, so a spacer as wide as Date, Day, In and Out keeps them lined up.

## Interplay with packages built in parallel

- **M2-EMP** (`v26_1-manage-timecard-future-clockout`, `v26_1-lists-employee-title`) also owns `ClockEvents.cs`, `Employees.cs` and `FormTimeCard`. B-TIME does not touch `ClockEvents.cs` or `Employees.cs`. In `FormTimeCard`, B-TIME changes only `Initialize`, the two view radio handlers, the end of `FillMain`, the print totals and the designer's total boxes. If M2-EMP lets a clock-out be in the future, that event is complete and counts toward Total and Day, the same as in the grid's own Day and Week cells. An employee title, if it is shown, does not affect the Reports To filter.
- **B-DBM** (`v25_3-tools-dbm-employee-supervisor`) adds the `EmployeeMissingSupervisor` check, which resets a `ReportsTo` that points at a deleted employee. Until then, `GetSupervisors` already skips missing and hidden supervisors, so such employees show only under All.
- **B-SHELL** owns all main module controls later. The only lines B-TIME added to `ControlManage.cs` are in `FillEmps`, and `InitializeOnStartup`'s `Lan.C` list is untouched (the label is translated in `FillComboReportsTo`).

## Tests

`tests/BTIMETests` (28 tests):

- `TimeCardTotalsTests` (no database): every rule of `Calculate` (finished and unfinished events, Adjust override and automatic, overtime out of Day, Rate2 and Rate3 overrides, PTO, protected leave and overtime adjustments, breaks), both formats with and without seconds, negatives, more than 24 hours, thousands, zero, the column order, and the default format from the preference.
- `EmployeeReportsToTests` (no database): the supervisor list, direct reports only, combined with the name filter, the clinic-filtered list, hidden and missing supervisors, falling back to All, and the per-user session memory.
- `TimeClockDbTests` (scratch MariaDB database `btime_<time>_<pid>_<random>` with the 25.3.48 schema, dropped afterwards): totals from rows saved and read back through `ClockEvents.Refresh` and `TimeAdjusts.Refresh`, for the Time Card and Breaks views, in both formats; supervisors read from the employee cache; hiding a supervisor falls back to All.
- `TimeClockUiSourceTests`: the windows are wired to the tested rules, the old total boxes are gone, and the Time Card window never writes the preference.

Not tested: the WinForms windows themselves (the desktop client is never launched): the on-screen alignment of the totals grid under the time card grid at different zoom levels, the print layout, and the combo's look in the Manage module.

## Questions for the owner

1. Should the Decimal/Colon choice in the Time Card window also switch the time card grid's own cells, or stay totals-only (as built)?
2. Should Reports To also list the supervisor themselves, or indirect reports?
3. Should the Reports To choice be saved between sessions (needs a new `UserOdFkeyType` value from the registry owner)?
4. Should Manage Time Cards get the Reports To filter and print its time cards with the single-format totals row?

## Integration (branch `port/int-2`)

- `TimeClockDbTests` built its scratch database from `SchemaVerifier.GetReference25_3_48()`. On top of the foundation (M2-01) the generated `EmployeeCrud` writes the 26.1 column `employee.Title`, so the two tests that insert an employee failed with "Unknown column 'Title'". The scratch database now uses `SchemaVerifier.GetReference()` (26.1.70), the schema the CRUD matches, as BPERIOTests does since `port/int-1`. No test was changed or removed.
