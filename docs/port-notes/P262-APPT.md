# Port note: P262-APPT, Appointments module (Open Dental 26.2 items 4 to 7)

Branch `p262/appt-1`, cut from `p262/schema-1` (39be2b04b). One of the nine feature packages of the Open Dental 26.2 port (owner decision of
2026-11-07): the four items of the release notes' Appointments Module section.

| Item | Release notes (paraphrased) | Status |
|---|---|---|
| 4 | Search for several blockout types from the Advanced Appointment Search | built |
| 5 | Practice and clinic schedule notes can be viewed from the Appointments module | built |
| 6 | The Advanced Appointment Search lists time slots for all selected blockouts in a day | built |
| 7 | Appointment estimates are updated when an insurance plan is saved | built |

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html), Appointments Module section: four one-line bullets with no link
  and no detail.
- Manual pages (the public manual, which describes the version before 26.2; the version-specific `help262` pages are not public and were not
  used):
  - `apptpinboard.html`, Advanced Appointment Search: the fields; up to 15 dates, the first open time slot of each; More shows the next 15
    dates; blockout types with the usage "Block appointments scheduling" are not listed; at least one provider or blockout type is needed.
  - `appointments.html`: clicking an operatory header shows the scheduled providers, schedule notes and the selected clinic's note; the
    Waiting Room, Emp, Prov and Reminders tabs.
  - `schedule.html`, `scheduleeditdaywindow.html`: practice and clinic notes and holidays are entered in the Edit Day window (Practice when
    Headquarters is selected, Clinic for a clinic); the Edit Day page says such a note does not show in the Appointments module (the
    behaviour item 5 changes).
  - `insplan.html`, `insplanedit.html`: nothing about estimates when the plan is saved.
- `OpenDentalDiffDocumentation26-2.xml` and `OpenDentalDocumentation26-2.xml`: no table, column or enum value changes for these items (the
  diff document has none). The full document's description of `schedule.SchedType` value 0 (`ScheduleType.Practice`) says that practice and
  holiday notes show in schedule setup and in the upper left of the Appointments module's operatory bar, with a hover effect. That is the
  only public statement of where item 5 shows the notes; this package follows it.

## What was built

### Items 4 and 6: Advanced Appointment Search with several blockout types

- `FormApptSearchAdvanced`: the Blockout list is a multi-select list (`comboBlockout.SelectionModeMulti`). None stays the first entry and is
  exclusive: picking None clears the blockout types, picking a type clears None, clearing everything selects None
  (`comboBlockout_SelectionChangeCommitted`). Do Not Schedule types are still not listed.
- `ApptSearch.GetSearchResultsForBlockouts` (new, business layer, one Middle Tier call): for each date, the first time slot of every
  selected blockout type that has one that date; `ScheduleOpening.BlockoutDefNum` (new field, not a column) names the type. Stops after
  `dateCount` dates (15 from the window) that have a row. Without providers a slot is an opening in the blockout alone (as
  `GetSearchResults` for one type); with providers it must also be open for one of them in the same operatory at the same time (as
  `GetSearchResultsForBlockoutAndProvider` for one type). The schedules are read once for all the types; each type is then searched on its
  own share (`GetDataForBlockoutType`: the provider schedules and that type's blockouts, as a read for that type alone returns), so a
  blockout of another selected type neither counts as an opening nor blocks one. For one type the result is exactly the 26.1 result
  (tested in both search behaviours, with and without a provider). With providers, only the dates on which a provider has an opening are
  looked at, and the search stops at the 15th date instead of evaluating the whole two years per type.
- The window calls it whenever a blockout type is selected (one or more, with or without providers); with None it calls `GetSearchResults`
  as before. The results grid gets a Blockout column (the type's name) when the results came from a blockout search; the window is 110
  pixels wider for it (the grid widens, the filters move right).
- More starts the day after the last date listed. In 26.1 it started on that date and listed it again; with several rows per date that
  would repeat every row of it, and the manual says More shows the next 15 dates.

### Item 5: practice and clinic schedule notes in the Appointments module

- `Schedules.GetPracticeNotesForApptModule` (new): the practice notes and holidays (`SchedType` Practice) of the schedules the module has
  read for the period (`ControlApptPanel.ListSchedules`, which already holds them: no new query): those of the whole practice (ClinicNum 0,
  Headquarters when clinics are on) and, when clinics are on and a clinic is selected, that clinic's. A note with no text is left out; a
  holiday always counts. Order: date, holidays first, practice before clinic, then as entered.
- `Schedules.GetPracticeNotesText` (new): the tip text, one line per note, worded as the Edit Day window words them: "Holiday: text" or
  "Note: text", with the clinic's abbreviation (or Headquarters) in parentheses when clinics are on; in the week view each line starts
  with its day and date.
- `ControlApptPanel`: when there are such notes, the upper left corner of the operatory header (above the left time bar, empty before)
  shows "Notes" on a pale yellow background (`DrawPracticeNotesCorner`). Hovering over the corner, or clicking it, shows the notes in the
  same tip panel the operatory headers use (`HeaderTipDraw`), wrapped at half the module's width. The list is computed again only when
  the period's schedules or the selected clinic change. The op header tip now always sets its label's width (the notes tip may have
  narrowed it).
- Comments of `Schedule.SchedType` and `ScheduleType.Practice` updated.

### Item 7: appointment estimates when an insurance plan is saved

- `InsPlans.GetPatNumsWithScheduledApptsForPlan` (new): the patients covered by the plan (a patplan on any inssub of the plan) with a
  Scheduled appointment on or after a date.
- `InsPlans.ComputeEstimatesForApptsOnPlan` (new): computes the estimates of those patients (from today), except the patients the caller
  already computed, with `ComputeEstimatesForPatNums`, the same computation `ComputeEstimatesForSubscriber` runs (every procedure that is
  not complete or deleted). `InsPlans.GetPatNumsForSubscriber` (new) is the query `ComputeEstimatesForSubscriber` already ran, now shared.
- `FormInsPlan.butSave_Click`: after the plan, its benefits, the subscriber and the blue book are saved, and before the window closes, it
  computes the plan's appointment estimates for every other covered patient with a scheduled appointment from today on (the subscriber's
  own patients were computed just before, as in 26.1). Only for a user with Insurance Plan Edit: without it nothing this window saves can
  change another patient's estimates. Runs also when the plan is edited from Lists, Insurance Plans (no subscriber; in 26.1 nobody's
  estimates were computed then). Wait cursor; an error leaves the plan saved and says so.

## No schema change, no migration step

No table, column, enum value, preference or row. The levels 2026110711 to 2026110719 are unused. `db/init/new-database.sql` is not touched.

## INFERRED behaviours

The sources name the four changes in one line each. Everything below is this package's reading.

- **Item 6, what a row is:** one row per selected blockout type per date, the first open slot in that type (the manual and the 26.1 window
  call the selected blockout types "Blockouts"). An alternative reading is one row per blockout on the schedule (each block of a selected
  type, in each operatory); see question 1.
- **Item 6, the 15:** the window lists up to 15 dates, not 15 rows; More continues with the next 15 dates.
- Rows of one date are ordered by time, then by the blockout types' order in their definition list.
- The Blockout column, its width, and the wider window.
- None in the Blockout list is exclusive (as the window's provider list intends).
- More starts the day after the last date listed (above).
- **Item 5, where:** the upper left corner of the operatory header, from the database document; the hover tip, from the same text ("hover
  effect"). The corner's look ("Notes", pale yellow), the click, the tip's wording and the dates in the week view are this package's.
- **Item 5, which notes:** practice and holiday notes of the whole practice and of the selected clinic, the same set whose holidays already
  color the module's background; notes without text are left out. Provider and employee notes are not included (they show in the Prov and
  Emp tabs already). The notes are not printed with the schedule.
- **Item 7, whose estimates:** every patient covered by the plan who has a Scheduled appointment today or later; patients with only past,
  planned, broken or Unscheduled List appointments are left out. Their estimates are computed as the subscriber's are (all treatment-planned
  procedures, not only those on the appointment).
- **Item 7, when:** on every save by a user with Insurance Plan Edit, whether or not the plan changed. Other workstations see the new
  estimates when their Appointments module next refreshes (no signal is sent, as for the subscriber's estimates in 26.1).

## Questions for the owner

**Decided as built (owner, 2026-11-07):** every question below is settled as the package built it ("all as recommended").

1. Item 6: one row per selected blockout type per date (built), or one row per blockout on the schedule (every block of a selected type, in
   each operatory)?
2. Item 7: recompute only for patients with a scheduled appointment from today on (built), or for every patient the plan covers (slower on
   large shared plans), or also for planned and Unscheduled List appointments?
3. Item 7: on every save by a user with Insurance Plan Edit (built), or only when the plan or its benefits changed?
4. Item 5: should the practice and clinic notes also print with the schedule, or show in the Emp or Prov tab?
5. More now starts the day after the last date listed (26.1 listed that date again). Keep?

## Tests

New project `tests/P262ApptTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 16 tests:

- `BlockoutSearchDbTests` (8): two types give a row each on the same date; a type that is not selected is not searched; one type gives
  exactly the 26.1 result, in both search behaviours and with a provider; an appointment in a blockout moves its slot; rows of one time
  follow the definition order; the date count counts dates, and More's next search finds the next date; starting before/after apply to
  each type; with a provider each type intersects the provider's operatory and hours; no blockout type is the provider search.
- `PracticeNotesDbTests` (3): which rows the corner shows with clinics off and on (practice, the selected clinic, Headquarters), their order
  and the tip text, in day and week view.
- `PlanEstimatesDbTests` (2): which covered patients get estimates (another subscriber with an appointment tomorrow, an appointment today)
  and which do not (the caller's, no appointment, a past appointment, an Unscheduled List appointment, another plan); without patients
  done, the subscriber's too; no plan, nothing.
- `SourceWiringTests` (3): the window code that cannot run without a screen (the multi-select list and its handler, the search call, the
  Blockout column and More; the corner drawing, tip and click; the save's order and permission check in the insurance plan window).

The database tests use one scratch database, `test_p262appt_<run id>_main`, built from `db/init/new-database.sql` and every fork migration of
this build, with synthetic rows only, dropped afterwards (`P262APPT_KEEP_DB=1` keeps it; `P262APPT_TEST_DB=skip` skips them).

Not tested: the windows themselves (OpenPmsDental.exe is not launched in this run; the source checks stand in for that) and Middle Tier.

## Files touched

- New: `tests/P262ApptTests/*`, this note.
- `OpenPmsDentalBusiness/Db Multi Table/ApptSearch.cs` (`GetSearchResultsForBlockouts`, `GetDataForBlockoutType`,
  `ScheduleOpening.BlockoutDefNum`), `Data Interface/Schedules.cs` (two methods), `Data Interface/InsPlans.cs` (three methods;
  `ComputeEstimatesForSubscriber` uses the shared query), comments in `TableTypes/Schedule.cs` and `Db/Enumerations.cs`.
- `OpenPmsDental/Forms/FormApptSearchAdvanced.cs` and its designer, `OpenPmsDental/UI/Appt/ControlApptPanel.cs`,
  `OpenPmsDental/Forms/FormInsPlan.cs`.
- `OpenDental.sln`, `OpenDental.Local.slnf` (the test project), `docs/release-notes/next.md`.

## Interplay with the other 26.2 packages

- Packages that add their own test projects to `OpenDental.sln` and `OpenDental.Local.slnf` conflict on those lines at integration; keep
  every project.
- `FormInsPlan.butSave_Click` gains a region at its end (Appointment Estimates); a package that edits the same method should keep it last,
  after everything the window saves.
- `ControlApptPanel`: the corner drawing is one call in `OnPaint` and one branch at the top of `HeaderTipDraw` and `OnMouseDown`.
- `ApptSearch`: the 26.1 methods are unchanged; the pinboard's basic search (appointment types restricted to blockout types) still uses
  `GetSearchResults`.

## Checks (2026-11-07)

- Targeted runs, all with `NoHqTests` (58/58): `P262ApptTests` 16/16, `ApptTests` 76/76, `BAPPTTests` 61/61, `M2APPTTests` 37/37,
  `M2ESTTests` 70/70, `M2INSPLANTests` 31/31, `InsuranceTests` 49/49, `M2CARRIERTests` 55/55 and `VBRIDGE1Tests` 37/37 (both read
  `ControlApptPanel.cs` or `FormInsPlan.cs`).
- Scratch databases `test_p262appt_*` are dropped after each run.
