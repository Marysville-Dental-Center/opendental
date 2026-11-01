# Port note: B-APPT (Appointments module and appointment permissions)

Branch `port/b-appt`, cut from `m1/integration-a2-patch`. Backlog section: [port-backlog.md, B-APPT](../specs/port-backlog.md). Tests: `tests/BAPPTTests` (assembly `BAPPTTests`).

Sources used: Open Dental's public release notes for 24.4, 25.1, 25.2 and 25.3, the public manual pages on appointment views, time bars, the waiting room and task setup, and the published database documentation (25.3 and the 25.2/25.3 diff XMLs, checked for preference names). Nothing was decompiled, observed or copied, and no Open Dental wording was reused for labels. Where behaviour is not public it is marked **INFERRED**.

No fork migration step was needed, so the assigned level range (2027001901 to 2027001999) is unused. No foundation-owned registry (TableTypes, Crud, Pref.cs, PrefInfos.xml, GroupPermission(s).cs, UserControlSecurityTree.cs, DisplayFields.cs, enum files, seed rows) was changed.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-appts-copy-apptview` | built | Copy View button in Setup > Appointments > Appointment Views. Copies the view and all of its items, places it after the original, opens it; Cancel deletes it. |
| `v25_2-appts-provider-timebars-op-background` | built | Preference checkbox; with it on, scheduled provider time is painted in a light tint of the provider's color on each operatory column (day and week view). The setting is kept in a fork-owned preference row (see "Preference names"). |
| `v25_2-appts-waiting-room-window` | built | "Wait Rm" toolbar button opens the waiting room list in its own non-modal window (one instance, remembered position, double-click jumps to the appointment). |
| `v25_3-appts-timebar-secondary-color` | built | Preference checkbox; with it on, an appointment with no provider time in either pattern shows the secondary provider's color over its whole time bar. Same storage as above. |
| `v25_3-security-apptcreate-pinboard` | built | Every path that turns a planned appointment into a scheduled one checks Appointment Create and never Appointment Move. Wording is M1-02's. |
| `v25_3-security-apptmove-scope` | built | Unscheduling a complete appointment needs Appointment Move and Completed Appointment Edit; sending a planned appointment to the pinboard no longer needs Appointment Move. Wording is M1-02's. |
| `v25_1-appt-multiple-completed-tasks` | built | With "multiple tasks per appointment" off, only open tasks count toward the one-task limit (Task button, attach-from-task window, task tree copy and cut/paste). |

## Preference names (applies to both time bar items)

Open Dental's preference names for these two settings are not in any public source: the 25.2 and 25.3 release notes give only the label, and the published database documentation (including the 25.3 diff XML) names neither. The registry rule (`tests/RegistryTests/PrefNameTests.cs`, `UnconfirmedNames_AreNotRegistered`) forbids registering the proposed names `ApptProvTimeBarsOnOpBackground` and `ApptTimeBarSecondaryColorNoProvTime` in `PrefName`, so:

- Each setting lives in a fork-owned preference row, `ForkApptProvTimeBarsOnOpBackground` and `ForkApptTimeBarSecondaryColorNoProvTime`, read and written by name (`OpenDentBusiness/Misc/ApptModule/ApptTimeBars.cs`). Nothing is added to `PrefName`, `PrefInfos.xml` or `Seed25_3`.
- A missing row reads as off. The row is inserted the first time the setting is turned on (`ApptTimeBars.UpdateBoolPref`), so no migration step writes to an Open Dental 25.3.48 database (`Od253MigrationTests` stays at zero changes) and turning a never-saved setting off writes nothing.
- Hand-over: `ApptTimeBars.GetPrefNameInUse` switches to the Open Dental name by itself as soon as a `PrefName` member with the proposed name exists. When the owner confirms the real names, the registry owner (M1-02/M2-02) adds them to `PrefName`, `PrefInfos.xml` (controls `checkProvTimeOnOpBackground`, `checkTimeBarSecondaryColor`, category `ApptAppearance`), the seed defaults ("0") and the RegistryTests confirmed list, and corrects `PREF_NAME_PROPOSED_*` if the confirmed name differs. An office that had already ticked the fork setting ticks it once more (or a small data step copies the fork row's value).
- Deviation from the B-CLAIM approach (which hides its unconfirmed Medical EOB preference until the name is registered): these two are display-only settings that default to off, so they are usable now. The integrator may choose either approach for consistency.

## v24_4-appts-copy-apptview

- `ApptViews.CopyView(apptViewNum)` (new partial file `OpenDentBusiness/Misc/ApptModule/ApptViewCopy.cs`; `ApptViews` made `partial`): copies every `apptview` column except the key (clinic, rows per increment, scheduled-provider filters and times, stacking, scroll start and dynamic scroll, bubble setting, minimum width, waiting room name format, scheduled-provider days, `ShowMirroredAppts`), and every `apptviewitem` row including the `IsMobile` rows. Users' remembered views (`userodapptview`) are not copied. Middle-tier aware.
- Order: the copy gets the source's `ItemOrder`+1; the views after it in the same clinic move down one. Other clinics' lists are untouched. The F-key follows from the position, as before.
- Description: "<original> (copy)", shortened to fit `varchar(255)`. **INFERRED** suffix (the spec's decision).
- UI: `FormApptViews` gets a "Copy View" button (enabled only with a view selected), which copies, refreshes the caches and opens `FormApptViewEdit` on the copy with `IsNew` (Cancel deletes the copy, like Add) and the new `IsCopy` flag (keeps the copied bubble setting instead of the new-view default). The copy starts on the source view's clinic; the edit window's existing clinic validation applies if the user changes it.
- Security log under Setup, written only after the copy is saved: "Appointment view '<name>' copied to '<new name>'". **INFERRED**: logging only on OK, so a cancelled copy leaves no entry.

## v25_2-appts-provider-timebars-op-background

- Reading chosen (spec open question 4): the spec's "proposed behavior", **INFERRED**: with the preference on, each operatory column paints the provider schedules assigned to it (the same source as `Schedules.GetProvSchedsForOp`) in the provider's color lightened 70% toward white, instead of the generic open color. Hygienists use their own color. The far-left provider bars and the narrow operatory time bars still show; holidays (as today), blockouts, grid lines and appointments draw as before. The tint also applies in the week view.
- Overlapping providers in the same operatory and time split the column width equally, ordered by who starts first (ties by ProvNum) (`ApptTimeBars.GetOpBackgroundSegments`).
- Printing: tint only in full-color mode; "less color" and grayscale keep today's output.
- Files: `OpenDental/UI/Appt/ControlApptPanel.TimeBars.cs` (new partial), two one-line hooks in `ControlApptPanel.DrawMainBackground` (week and day branches), checkbox in Preferences > Appts > Appearance, new "Time Bars" group (`UserControlApptAppearance`). Label is ours: "Show provider time on the operatory background in the provider's color".

## v25_3-appts-timebar-secondary-color

- With the preference on and no `X` in either `Pattern` or `PatternSecondary`, the time bar on the left of the appointment is filled with the secondary provider's color: the hygienist for an appointment not flagged hygiene, otherwise the dentist. No secondary provider: the bar stays as before. Grayscale printing desaturates the color, as for provider time. Length and production are not affected.
- Drawn before the pattern loops, so the increment lines still show; it fills the rounded left-side path, so the corners stay round.
- `ProvBarShading` (the per-provider columns at the far left) is left alone, as the spec asks when 25.3.48 cannot be checked. **INFERRED**.
- Files: `ControlApptPanel.TimeBars.cs`, one-line hook in `ControlApptPanel.DrawOneAppt`, checkbox in the same "Time Bars" group. Label is ours: "No provider time set: show the secondary provider color in the time bar".

## v25_2-appts-waiting-room-window

- "Wait Rm" button on the Appointments toolbar after Delete (`ControlAppt.WaitingRoom.cs`, one-line hook in `ControlAppt.LayoutToolBar`). **INFERRED** location (the blog only says "a new Wait Rm button"). The docked grid stays as it is.
- `FormWaitingRoom` (new, non-modal, shown with the main window as owner): same columns, rows, name format, clinic and view filters, and alert color as the docked grid. The row logic is `ApptWaitingRoom.GetRows` (new, tested); the docked grid's own `FillWaitingRoom` is unchanged, so the two are kept in step by the same rules rather than shared code (to keep `ControlAppt.cs`, which B-SHELL and M2-APPT also edit, untouched).
- Refresh: wait times tick every second; the window takes the Appointments module's waiting room table whenever it is newer (module refreshes, appointment signals), and reads the database itself every 60 seconds, so it keeps working while the user is in another module.
- Single instance: a second click restores and brings the window to the front. It closes with the other open windows on log off (`FormOpenDental.CloseOpenForms`).
- Double-click: selects the patient and goes to the Appointments module on today's date with that appointment selected.
- Size and position are remembered per Windows user on this computer in `%LOCALAPPDATA%\OpenDental\WaitingRoomWindow.txt` (nothing in the database). A saved position that is off every screen is ignored. **INFERRED** storage.
- `Appointments.GetPeriodWaitingRoomTable` now also returns `AptNum` and `PatNum` (two columns added; existing columns unchanged).

## v25_3-security-apptcreate-pinboard and v25_3-security-apptmove-scope

Rules in `OpenDentBusiness/Misc/ApptModule/ApptPermissionRules.cs` (tested), wired in:

- `AppointmentL.ValidateApptUnsched`: Appointment Move for every status; a complete appointment also needs Completed Appointment Edit (24.3 checked only the latter). This covers the toolbar Unsched button, the appointment menu, Break > Unscheduled List and Edit Appointment's break options.
- `AppointmentL.ValidateApptToPinboard`, `FormApptEdit.butPin_Click`, `FormApptsOther.butPin_Click`: a planned appointment is copied onto the pinboard and needs no Appointment Move; any other status still does. Patient restriction and archived/deceased checks stay.
- `ControlChart.butPin_Click` (Planned Appointments grid): the up-front Move check is replaced by one that applies only when a selected row's scheduled appointment (Complete, Scheduled, Unscheduled or Broken) is sent, which is a move; rows with no scheduled appointment copy the planned appointment and need no Move.
- Dropping a planned appointment off the pinboard already checked Appointment Create in 24.3 (`ControlAppt.pinBoard_ApptMovedFromPinboard`) and checks nothing for Move; unchanged. The Planned Appointment Tracker's "Send to Pinboard" never checked Move; its Edit Appointment Pin path now follows the planned rule above. Moving from the Unscheduled List stays unblocked.
- Drags inside the schedule and drags to the pinboard (`ControlApptPanel`) already require Move for every status (plus Completed Appointment Edit for complete ones); unchanged.
- Audit entries are unchanged (A-APPT's detail is used).

## v25_1-appt-multiple-completed-tasks

Rules in `OpenDentBusiness/Misc/ApptModule/ApptTasks.cs` (tested). "Complete" means `TaskStatus` Done.

- Edit Appointment's Task button (`FormApptEdit.butTask_Click`): one open task opens it; several open tasks show Tasks for Appointment (which lists completed ones too); no open task starts a new task even if completed ones exist. With the preference allowing multiple tasks it always starts a new task, as before.
- Attaching an appointment from Task Edit (`FormApptsOther` with `IsForTask`): only appointments with an open task are hidden (`ApptTasks.GetApptNumsWithOpenTask`; the 24.3 `Appointments.GetApptNumsAttachedToTask` is left in place).
- Task tree (`UserControlTasks`): copying a completed task attached to an appointment is allowed; after pasting a cut task the clipboard is cleared only for an open task.
- Send to Multiple (`FormTaskListSelect`): unchanged. It only applies to a new task, which is open, so several copies would still be several open tasks on one appointment. **INFERRED**.
- Reopening a completed task while another open task exists: allowed without a warning (the limit applies when creating or attaching). The spec proposed "warn but allow"; the warning belongs in `FormTaskEdit`, which B-APPT does not own (A-TASK, then M2-MISC). Owner question below.

## Files touched

New: `OpenDentBusiness/Misc/ApptModule/{ApptPermissionRules,ApptTasks,ApptTimeBars,ApptViewCopy,ApptWaitingRoom}.cs`, `OpenDental/Forms/FormWaitingRoom.cs` (+ `.Designer.cs`), `OpenDental/Main Modules/ControlAppt.WaitingRoom.cs`, `OpenDental/UI/Appt/ControlApptPanel.TimeBars.cs`, `tests/BAPPTTests/*`, this note.

Changed (all owned by B-APPT except the shared build files): `OpenDentBusiness/Data Interface/Appointments.cs` (waiting room columns), `ApptViews.cs` (`partial`), `OpenDental/Logic/AppointmentL.cs`, `OpenDental/Main Modules/ControlAppt.cs` (1 line), `ControlChart.cs` (pin check), `OpenDental/UI/Appt/ControlApptPanel.cs` (3 hook lines), `OpenDental/Forms/FormApptEdit.cs`, `FormApptViewEdit.cs`, `FormApptViews.cs` (+ designer), `FormApptsOther.cs`, `OpenDental/User Controls/Preferences/UserControlApptAppearance.cs` (+ designer), `OpenDental/User Controls/UserControlTasks.cs` (two conditions). Shared build files: `OpenDentBusiness/OpenDentBusiness.csproj` (one wildcard for `Misc\ApptModule\**`), `OpenDental/OpenDental.csproj` (four entries, one block), `OpenDental.sln`, `OpenDental.Local.slnf` (the test project).

Two files (`ControlAppt.cs`, `ControlApptPanel.cs`) are stored with mixed CRLF/LF line endings; the edits keep each line's original ending so their diffs show only the added lines.

## Interplay with packages built in parallel

- **B-TASK** (depends-on; also edits `UserControlTasks.cs` and `Tasks.cs`): B-APPT does not touch `Tasks.cs` and changes only the two appointment-limit conditions in `UserControlTasks.Copy_Clicked` and `Paste_Clicked`; B-TASK's filter work is elsewhere in the file. A textual conflict, if any, is resolved by keeping both.
- **B-SHELL** (B3, also edits `ControlAppt.cs`, `ControlChart.cs`): B-APPT's edits are one toolbar line in `LayoutToolBar` and the `butPin_Click` check.
- **M2-APPT** (also edits `ControlAppt.cs`, `ControlApptPanel.cs`, `Schedules.cs`): B-APPT adds hook lines only; drawing logic is in the new partial file. `Schedules.cs` is not touched.
- **M2-MISC** (`UserControlTasks.cs`, `FormTaskEdit.cs`): the reopen warning (above) would go in `FormTaskEdit`.
- **M2-CARRIER** (`Appointments.cs`): only `GetPeriodWaitingRoomTable` changed.
- **M1-02 / M2-02** (registries): nothing changed; the preference hand-over above is theirs to trigger.

## Tests

`tests/BAPPTTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `PermissionRuleTests`, `TimeBarRuleTests`, `WaitingRoomRowTests`, `ApptTaskRuleTests`: the rules, no database.
- `BAPPTDbTests`: a per-run scratch database (`bappttest_<time>_<pid>_<random>`, dropped afterwards) on 127.0.0.1: Copy View columns, items, order, remembered views and description length; the time bar preference rows (missing reads off, insert on first turn-on, no duplicates, cache); the open-task query; the waiting room table's new keys.
- `UiSourceTests`: source checks that the WinForms hooks call the tested rules (the GUI is never launched).

Related existing assemblies to run: `ApptTests` (A-APPT, same appointment files), `TaskCategoryTests` (task tree and task tables), `RegistryTests` (the unconfirmed preference names stay unregistered). `ForkMigrationTests` is not needed: no step, table or CRUD changed.

Checks run on the branch (2026-10-25): `OpenDental.Local.slnf` Debug x86 full rebuild, 0 errors and 376 warnings (none in files this package touched); targeted run of `BAPPTTests` (61 passed), `ApptTests` (76), `TaskCategoryTests` (21), `RegistryTests` (33) and `NoHqTests` (49), all passed with the database tests running against the local MariaDB; `tools/Test-NoHqHosts.ps1` passed.

## Untested

- Anything visual or interactive: the tint and secondary color on screen and in print preview, the Copy View button flow, the Wait Rm window (single instance, refresh while in another module, double-click jump, remembered position, closing on log off), and the preference checkboxes in the Preferences window. Covered only by rule tests and source checks.
- Middle-tier (ClientMT) paths of `ApptViews.CopyView`, `ApptTasks.GetApptNumsWithOpenTask` and `ApptTimeBars.UpsertPrefRaw`.
- Behaviour on a real Open Dental 25.3.48 office database (the office's own settings for the two time bar preferences cannot be read until their names are known).

## Owner questions

1. What are Open Dental's real preference names for "provider time bars on operatory background" (25.2) and "secondary color in the time bar when no provider time is set" (25.3)? Once confirmed, the registry owner registers them and the code switches to them (see "Preference names").
2. Is the chosen reading of the 25.2 item right (scheduled provider time tinted on the operatory column), or should the far-left provider bar time be drawn on the operatory background instead?
3. Reopening a completed task while the appointment already has an open task: keep it silent (current), or add a warning in Task Edit (M2-MISC's file)?
4. Should the far-left provider columns (`ProvBarShading`) also follow the secondary color preference?
5. Is the toolbar the right place for "Wait Rm", and is a per-Windows-user file the right place to remember its position (a workstation-wide setting would need a `computerpref` column)?
