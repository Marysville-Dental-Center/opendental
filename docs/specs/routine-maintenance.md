# Routine maintenance around the practice

Owner decisions of 2026-11-02 (final). Roadmap item 3 of "Next" (`docs/roadmap.md`).

The office defines its own maintenance items (any piece of equipment, a room, a system or a compliance routine) and records each time one
is done. Nothing is hard-coded: there is no built-in list of equipment or routines and no starter template. The list starts empty.

## Contents

1. Tables
2. Schedule rules
3. Status: due, due soon, overdue
4. Result fields and validation
5. Windows: board, item setup, recording, history
6. Alerts
7. Permissions and the audit trail
8. Menus
9. Schema migration and the adoption tools
10. Decisions taken here (owner questions)

## 1. Tables

Five fork-only tables (not Open Dental schema), created by fork migration steps 2026110201 to 2026110205. All are MyISAM, utf8mb3, with a
`bigint` primary key named `<Table>Num`, as Open Dental's own tables. Rows are never written by a migration step.

### maintitem: one maintenance item

| Column | Type | Meaning |
|---|---|---|
| `MaintItemNum` | `bigint(20) NOT NULL AUTO_INCREMENT` | Primary key. |
| `ItemName` | `varchar(255) NOT NULL` | Short name shown on the board, for example "Steam sterilizer spore test". Required. |
| `Description` | `text NOT NULL` | Free text: what to do, where, which supplies. |
| `EquipmentNum` | `bigint(20) NOT NULL` | FK to `equipment.EquipmentNum` (Manage, Equipment). 0 when the item is not linked to an asset. |
| `ScheduleType` | `tinyint(4) NOT NULL` | `EnumMaintScheduleType`: 0 Interval, 1 Weekdays, 2 AfterCompletion. |
| `IntervalCount` | `int(11) NOT NULL` | N of "every N units" or "N units after the last completion". 1 or more. Not used by Weekdays. |
| `IntervalUnit` | `tinyint(4) NOT NULL` | `EnumMaintIntervalUnit`: 0 Days, 1 Weeks, 2 Months, 3 Years. Years only for Interval. |
| `Weekdays` | `int(11) NOT NULL` | Weekdays schedule: a bit per day, bit `(int)DayOfWeek` (Sunday 1, Monday 2, ... Saturday 64). 0 otherwise. |
| `DateAnchor` | `date NOT NULL DEFAULT '0001-01-01'` | The first due date (Interval, AfterCompletion) or the first day the weekday schedule applies (Weekdays). Required. |
| `LeadDays` | `int(11) NOT NULL` | Reminder lead time in days, 0 to 365: the item is "due soon" from this many days before its due date. |
| `UserNumAssigned` | `bigint(20) NOT NULL` | FK to `userod.UserNum`: the user who does the task. 0 when assigned to a group or to nobody. |
| `UserGroupNumAssigned` | `bigint(20) NOT NULL` | FK to `usergroup.UserGroupNum`: a user group as the role that does the task. 0 when assigned to a user or to nobody. At most one of the two assignee columns is set. |
| `IsHidden` | `tinyint(4) NOT NULL` | Retired. Hidden items leave the board (unless "Show hidden" is checked) and raise no alerts; their history is kept. |
| `SecUserNumEntry` | `bigint(20) NOT NULL` | The user who added the item. Never updated. |
| `SecDateTEntry` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When the item was added (server time). |
| `SecDateTEdit` | `timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()` | Last change, kept by the server. |

Keys: `EquipmentNum`, `UserNumAssigned`, `UserGroupNumAssigned`, `SecUserNumEntry`.

### maintstep: the item's ordered checklist

| Column | Type | Meaning |
|---|---|---|
| `MaintStepNum` | `bigint(20) NOT NULL AUTO_INCREMENT` | Primary key. |
| `MaintItemNum` | `bigint(20) NOT NULL` | FK to `maintitem`. |
| `ItemOrder` | `int(11) NOT NULL` | 0-based position in the checklist. |
| `Description` | `varchar(255) NOT NULL` | The step, for example "Drain and refill the reservoir". |

Key: `MaintItemNum`.

### maintfield: the item's result fields

| Column | Type | Meaning |
|---|---|---|
| `MaintFieldNum` | `bigint(20) NOT NULL AUTO_INCREMENT` | Primary key. |
| `MaintItemNum` | `bigint(20) NOT NULL` | FK to `maintitem`. |
| `ItemOrder` | `int(11) NOT NULL` | 0-based position. |
| `FieldName` | `varchar(255) NOT NULL` | Label, for example "Chamber temperature". Required. |
| `FieldType` | `tinyint(4) NOT NULL` | `EnumMaintFieldType`: 0 PassFail, 1 Number, 2 Text, 3 Date, 4 Attachment (5 ChecklistStep is only used in `maintrecordvalue`). |
| `Unit` | `varchar(255) NOT NULL` | Number fields: the unit shown next to the value, for example "°C" or "psi". |
| `HasMin` | `tinyint(4) NOT NULL` | Number fields: a lower limit applies. |
| `ValueMin` | `double NOT NULL` | The lower limit (inclusive). |
| `HasMax` | `tinyint(4) NOT NULL` | Number fields: an upper limit applies. |
| `ValueMax` | `double NOT NULL` | The upper limit (inclusive). |
| `IsRequired` | `tinyint(4) NOT NULL` | A completion cannot be recorded without a value. |

Key: `MaintItemNum`.

### maintrecord: one completion

| Column | Type | Meaning |
|---|---|---|
| `MaintRecordNum` | `bigint(20) NOT NULL AUTO_INCREMENT` | Primary key. |
| `MaintItemNum` | `bigint(20) NOT NULL` | FK to `maintitem`. |
| `DateDone` | `date NOT NULL DEFAULT '0001-01-01'` | The day the task was done. Today by default; may be earlier, never later than today. |
| `DateDue` | `date NOT NULL DEFAULT '0001-01-01'` | The due date this completion closes: the item's due date when it was recorded. |
| `UserNum` | `bigint(20) NOT NULL` | Who did the task and recorded it: always the logged-on user, set by the program. |
| `Note` | `text NOT NULL` | Free text. |
| `IsFlagged` | `tinyint(4) NOT NULL` | A result failed (Fail) or a number was outside its limits. |
| `SecDateTEntry` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When it was recorded (server time), set by the program. |

Keys: `MaintItemNum`, `UserNum`, `DateDone`. A completion is never edited or deleted.

### maintrecordvalue: the checklist and results of one completion

Each value keeps a copy of its step's or field's label and unit, so the history reads the same after the item's steps and fields change.

| Column | Type | Meaning |
|---|---|---|
| `MaintRecordValueNum` | `bigint(20) NOT NULL AUTO_INCREMENT` | Primary key. |
| `MaintRecordNum` | `bigint(20) NOT NULL` | FK to `maintrecord`. |
| `MaintStepNum` | `bigint(20) NOT NULL` | FK to `maintstep` for a checklist line, otherwise 0. |
| `MaintFieldNum` | `bigint(20) NOT NULL` | FK to `maintfield` for a result, otherwise 0. |
| `ItemOrder` | `int(11) NOT NULL` | Checklist lines first, then results, each in the item's order. |
| `FieldType` | `tinyint(4) NOT NULL` | `EnumMaintFieldType`; 5 ChecklistStep for a checklist line. |
| `Label` | `varchar(255) NOT NULL` | The step text or field name at the time. |
| `Unit` | `varchar(255) NOT NULL` | The unit at the time (Number). |
| `ValueText` | `text NOT NULL` | The value, stored culture-independent: ChecklistStep and PassFail "1" (done, pass) or "0" (not done, fail), Number in invariant culture ("4.5"), Date `yyyy-MM-dd`, Text as typed, Attachment the file name in the item's folder. Empty when left blank. |
| `IsFlagged` | `tinyint(4) NOT NULL` | Fail, or a number outside its limits. |

Keys: `MaintRecordNum`, `MaintStepNum`, `MaintFieldNum`.

## 2. Schedule rules

All dates are calendar days without time. "Today" is the server's date (`MiscData.GetNowDateTime`), so every workstation agrees.

### Occurrences

- **Interval, every N days or weeks from the anchor**: the anchor, then every N (or 7×N) days after it.
- **Interval, every N months or years from the anchor**: occurrence k is `anchor.AddMonths(k×N)` (years: `AddYears(k×N)`), always counted from
  the anchor, never from the previous occurrence, so a month end does not drift: anchor January 31, monthly, gives February 28 (29 in a leap
  year), March 31, April 30, May 31. An anchor on February 29, yearly, gives February 28 in other years and February 29 again in leap years.
- **Weekdays**: every day on or after the anchor whose weekday is checked (for example Monday, Wednesday and Friday). At least one weekday.
- **AfterCompletion, N days, weeks or months after the last completion**: there is one open occurrence only, the latest `DateDone` plus N
  units (`AddMonths` for months, so January 31 plus one month is February 28 or 29). With no completion yet, the anchor.

No occurrence is earlier than the anchor.

### The due date

The **anchor is the first due date**. An item set up with an anchor in the past and never done is overdue from the anchor; an office that
starts a schedule now sets the anchor to the next date the task is due (the setup window defaults it to today).

A completion closes the occurrence it was recorded against (its `DateDue`, the item's due date at that moment), whether it was done early or
late, and also every occurrence up to the day it was done. So for Interval and Weekdays:

- `covered` = the latest `max(DateDue, DateDone)` over the item's completions (none: nothing is covered);
- the **due date** is the first occurrence after `covered` (none covered: the first occurrence, which is the anchor for Interval and the
  first checked weekday on or after the anchor for Weekdays).

Examples, monthly from November 1: done October 29 (early, recorded against November 1) gives December 1; not done until December 15
(recorded against November 1) gives January 1, because December 1 is also closed. For AfterCompletion the due date is the latest
`DateDone` plus N units, whatever the earlier due dates were.

For a **Weekdays** schedule "due" means due on that day: a Monday/Wednesday/Friday item is due on Monday; on Tuesday, not yet recorded, it is
overdue (since Monday); a completion on Tuesday closes Monday and Tuesday, so the next due date is Wednesday.

Changing an item's schedule or anchor later keeps its completions; the due date is recalculated from the new schedule and the old completions
(`covered` does not depend on the schedule).

## 3. Status: due, due soon, overdue

With `due` the due date, `today` the server's date and `lead` the item's LeadDays:

| Status | When |
|---|---|
| Overdue | `due < today`. "Days" shows how many days overdue. |
| Due soon | `today <= due <= today + lead`, which includes due today (shown as "Today"). |
| Upcoming | `due > today + lead`. |
| Hidden | The item is hidden; no status is worked out. |

The board sorts overdue first (oldest due date first), then due soon, then upcoming, each by due date, then by name; hidden items last.

## 4. Result fields and validation

When a completion is recorded:

- **PassFail**: Pass or Fail. Fail flags the value.
- **Number**: a number in the user's format, stored in invariant culture. With HasMin, a value below ValueMin is out of range; with HasMax, a
  value above ValueMax is out of range (the limits are inclusive). An out-of-range value is flagged and still saved: the user is warned and
  confirms. A field may have only one limit. When both are set, ValueMin must not be greater than ValueMax (checked in setup).
- **Text**: any text.
- **Date**: a valid date.
- **Attachment**: a file copied into the images folder (A to Z folder) under `Maintenance\<MaintItemNum>\`, named
  `yyyyMMdd-HHmmss_<original name>`, through the program's FileAtoZ helpers (so cloud storage works too). The value is the file name. When the
  office stores images in the database, attachments cannot be added and a required attachment field blocks recording, with a message.
- **IsRequired**: a required field must have a value. A PassFail field always has one once chosen; a blank optional field is saved empty.
- **Checklist steps**: each is ticked or not. Recording with unticked steps asks for confirmation; unticked steps are saved as not done.

The record is flagged (`maintrecord.IsFlagged`) when any value is flagged.

## 5. Windows

All windows are WinForms windows of the program (`FormODBase`, `GridOD`, the program's buttons, combo boxes and validated text boxes), like
the Equipment and Supply Inventory windows next to them in the Manage module.

### Maintenance board (FormMaintBoard)

Opened by the Maintenance button in the Manage module and by the alerts. It needs no permission beyond the Manage module.

- Filters: **Assigned to** (All, Unassigned, then each user and each user group that is an assignee of some item), **Mine only** (items
  assigned to the logged-on user or to a user group the user belongs to), **Show hidden**.
- Grid columns: Status, Due, Days ("3 overdue", "Today", "in 5"), Item, Assigned To, Schedule (for example "Every 3 months from 11/01/2026",
  "Mon, Wed, Fri", "2 weeks after last done"), Last Done (date and user), Equipment. Overdue rows are light red, due soon rows light yellow.
- Buttons: **Record** (also double-click a row): records a completion of the selected item. **Add Item**, **Edit Item**: item setup.
  **History** (the selected item's completions) and **All History**. **Print** and **Export** of the grid, as other grids print
  (`GridOD.PrintPage`) and export (`GridOD.Export`).

### Item setup (FormMaintItemEdit)

Name, Description, Equipment (none or an item of the Equipment list), schedule (type, N, unit, weekdays, anchor), reminder lead time in
days, Assigned To (nobody, a user or a user group), Hidden. Two grids with Add, Edit, Delete, Up and Down: **Checklist** steps (edited in a
small text box) and **Result fields** (FormMaintFieldEdit: name, type, unit, minimum, maximum, required). A line under the schedule shows the
next due date the current settings give. **Delete** removes an item that has no completion; an item with history can only be hidden.
Validation: a name; N from 1 to 1000; at least one weekday for a weekday schedule; no Years for AfterCompletion; a valid anchor; lead time
0 to 365; field names; minimum not above maximum.

### Recording a completion (FormMaintRecordEdit)

Shows the item's name, description and the due date the completion closes, the date done (today; may be changed to an earlier day, never to
a later one), the **checklist** (click a line to tick it), the **results** (double-click a line to enter its value; an attachment line opens
a file picker; out-of-range numbers and Fail are marked) and a note. The user and the time are recorded automatically; there is no second
sign-off. OK saves the completion, its values and the audit entry, and updates the item's alerts at once.

### History (FormMaintHistory)

Per item (from the board's History button) or overall (All History), for a date range (default: the last 12 months) of the date done.
Columns: Date Done, Item (overall only), Done By, Due, Late (days after the due date, blank when on time), Flagged, Results (each filled
result as "name: value", so a printout shows them), Note, Recorded (date and time). A second
grid shows the selected completion's checklist and results; **Open Attachment** opens an attached file. **Print** and **Export** as on the
board.

## 6. Alerts

Reminders use the program's alert system (`alertitem`, the Alerts menu of the main window):

- **AlertType 127 MaintenanceDueSoon** ("Maintenance Due Soon") while an item's status is Due soon: it has entered its lead time.
- **AlertType 126 MaintenanceOverdue** ("Maintenance Overdue") while it is Overdue. It replaces the due-soon alert.
- Why 127 and 126, not 10000: `alertitem.Type`, `alertcategorylink.AlertType` and `alertsub.Type` are `tinyint(4)` (-128 to 127) in Open
  Dental's schema, so `FORK-ENUMS.md` gives the tinyint enums their own rule: fork-only values count down from 127.
- Recipients: the assigned user, or every user of the assigned user group (hidden users excluded). Each recipient gets an alert of their own
  (`UserNum` set), so it reaches them whatever alert categories they subscribe to, and nobody else sees it. An item with no assignee raises no
  alert; the board shows it.
- `FKey` is the MaintItemNum, `ClinicNum` -1 (every clinic), severity Low for due soon and High for overdue, actions Mark As Read and Open
  Form, `FormToOpen` FormType 127 FormMaintenanceBoard (opens the board; `alertitem.FormToOpen` is `tinyint(4)` too). There is no Delete action: the alert goes away by itself.
- **One reminder per due date**: `ItemValue` holds the due date the alert is about, as `yyyy-MM-dd` (the same on every workstation, whatever
  its culture or language). It is part of the alert's identity; the user does not see it (no Show Item Value action). So each occurrence gets
  a reminder of its own: when a completion is recorded and the next due date is already within the lead time (always the case when the
  interval is not longer than the lead time, for example a weekly or weekday item with the default lead time of 7 days), the old alert is
  deleted with its read marks and a new, unread one is added. The Alerts menu then shows it as new.
- **Without duplicates**: `MaintAlerts.Sync` works out the wanted set (one per item, type and recipient, for the item's current due date)
  and compares it with the existing maintenance alerts, matched on type, item (`FKey`), recipient (`UserNum`) and due date (`ItemValue`),
  never on the description, which depends on the workstation's culture and language. A wanted alert that exists is kept; when only its text
  or options differ (a renamed item, or a description written by a workstation in another culture) it is updated in place, so its read state
  survives. A missing one is added, extra copies of one alert (two workstations syncing at the same moment) are deleted, and alerts no
  longer wanted (recorded, so the due date moved on; hidden; reassigned; moved from due soon to overdue) are deleted with their read marks.
  Running it again changes nothing.
- When: every 10 minutes on each workstation (a thread of the main window), right after a completion is recorded, and after an item is
  saved.

## 7. Permissions and the audit trail

Fork-only permission values (`docs/port-notes/FORK-ENUMS.md`, range 10000-10049; the alert and window values above are in the tinyint
range 127-120, because `alertitem.Type`, `alertcategorylink.AlertType`, `alertsub.Type` and `alertitem.FormToOpen` are `tinyint(4)`):

- **EnumPermType 10000 MaintenanceSetup** ("Maintenance Setup"): add, edit, hide and delete maintenance items, their checklists and fields.
- **EnumPermType 10001 MaintenanceRecord** ("Maintenance Record Completion"): record a completion.

Both sit in the security tree under Manage Module, "Routine Maintenance". Viewing the board and the history needs only the Manage module.
No migration grants them: an administrator gives them to the user groups that need them (Setup, Security), as for any new permission.

Audit trail (`securitylog`, FKey the MaintItemNum):

- MaintenanceSetup: "Maintenance item added: <name>", "Maintenance item edited: <name> (<what changed>)", "Maintenance item hidden: <name>",
  "Maintenance item shown again: <name>", "Maintenance item deleted: <name>".
- MaintenanceRecord: "Maintenance recorded: <name>, done <date>, due <date>" plus ", flagged" when a value was flagged.

## 8. Menus

- Manage module, Daily group: **Maintenance** button, opens the board.
- Alerts menu: a maintenance alert's Open Form opens the board.
- Item setup and history are reached from the board.

## 9. Schema migration and the adoption tools

- Steps `2026110201` to `2026110205` (`OpenPmsDentalBusiness/Misc/ForkMigrations/MaintenanceSteps.cs`) each create one table with
  `CreateTableIfMissing`, from the DDL in `MaintenanceTables` (a `ForkTableSet`). They write no row, so adoption changes nothing else.
  Levels 2026110206 to 2026110209 stay free for this package.
- `SchemaVerifier` compares a database with the Open Dental 26.1.70 reference; a table that reference lacks is an `ExtraTable` difference.
  Fork-only tables are declared once, with their DDL, in `ForkTableSet` classes (`ForkTables`, found by reflection like the migration
  steps). `SchemaVerifier.DocumentedExceptions` allows them as extra tables, so `OdSchemaCompat` is still set, and `ForkTables.VerifyDatabase`
  checks their definitions exactly.
- The adoption tools: `tools/SyntheticData/allowed-differences.adoption.json` has a `TableAdded` rule per fork table, and
  `AdoptionValidation`'s row check (`AdoptionRowCheck`) lists fork tables apart from the Open Dental 25.4 and 26.1 tables and requires
  them empty.

## 10. Decisions taken here (owner questions)

Taken as the simplest option consistent with the decisions above; each can change.

1. No permission is granted by the migration; the administrator grants Maintenance Setup and Maintenance Record Completion.
2. Items without an assignee raise no alert (the board shows them).
3. A completion cannot be edited or deleted after it is saved.
4. An item without completions can be deleted; with completions it can only be hidden.
5. Items belong to the whole practice, not to a clinic; alerts show in every clinic.
6. The anchor is the first due date, so an anchor in the past makes a never-done item overdue at once.
7. Years are not offered for "after the last completion" (the decision lists days, weeks and months).
8. The alert types and the alert's window are 127, 126 and 127 instead of the 10000 range, because their columns are `tinyint(4)`
   (`FORK-ENUMS.md`, "tinyint enums"); widening Open Dental's columns was not chosen, since the schema must stay Open Dental's.
9. The reminder check runs every 10 minutes on every workstation that is logged on; the sync makes that safe (no duplicates).
