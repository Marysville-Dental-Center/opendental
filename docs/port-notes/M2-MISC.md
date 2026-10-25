# Port note: M2-MISC, small Open Dental 25.4 and 26.1 items

Branch `port/m2-misc`, cut from `port/foundation` (M1 through A2, M2-01 and M2-02). Built in the single-wave run, beside the other packages. Backlog section: [port-backlog.md, M2-MISC](../specs/port-backlog.md#m2-misc-small-254-and-261-items). No fork migration step was needed (level range 2027004401 to 2027004499 unused): the only schema item, `referral.Telephone` varchar(30), is M2-01's step 2026102401, and `AutoCondField.PatientProcedureClinicMismatch = 14` is M2-02's registry value.

Sources: public only. The 25.4 and 26.1 release notes, the 26.1 highlights blog post and `OpenDentalDiffDocumentation26-1.xml` as cited in the feature specs (`docs/specs/features-25.4.md`, `features-26.1.md`), and the 24.3 manual pages they name as baselines. No Open Dental program was installed, observed or decompiled. Tests use synthetic rows only.

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v25_4-misc-diagnostics-info` | built | Help, About, Diagnostics is built by a new local helper, `LocalDiagnostics`, with date and time on the workstation and the database server and their difference, hardware (CPU, cores, memory, OS and build, 32/64-bit, screens, DPI, free disk on the program drive and the image folder drive), program path, .NET runtime and release, server version, time zones. No passwords, hashes or keys. Nothing is sent anywhere. `BugSubmissions.GetDiagnostics` forwards to it. |
| `v25_4-misc-print-window` | partly built | The 25.4 printer rules are a tested business class (`PrintSituationRules`), Setup, Printers shows for every situation the printer used, the rule that picked it ("From Statements setting", "From Default setting", "Windows default") and the printer's state as Windows reports it (`PrinterStatuses`), and Edit Printer shows the state and what a blank printer means and keeps a saved printer that is not installed. Not built here: the same information inside the Print Options window, and "keep the page range when switching printers", because that window (`FrmPrintOptions`) is B-PRINT's 25.3 item and not on this base (see Interplay). "Remember copies per situation for the session" is already in B-PRINT's `PrintOptions.Remember`. |
| `v25_4-family-referral-phone` | built | One phone box of up to 30 characters in Edit Referral, no 10-digit check. A plain 10-digit number is still stored as 10 digits and shown in the office phone format; anything else (extensions, 11 digits, international) is stored as typed. |
| `v26_1-setup-automation-clinic-mismatch` | built | The condition is evaluated in `Automations` (true when a completed procedure's clinic is not the patient's clinic, a patient without a clinic counts as a mismatch, always false with clinics off, false on any trigger but ProcedureComplete). Automation Condition Edit lists it only for the ProcedureComplete trigger and saves comparison None and no text; Edit Automation refuses to save another trigger while the automation has the condition. |
| `v26_1-misc-task-attach-nonmodal` | built | Task Attachments and Task Attachment Edit open with `Show(owner)`. One Task Attachments window per task and one edit window per attachment (one new-attachment window per task): a second click brings the open one to the front. The task window refreshes as attachments change, closes the attachment windows when it closes (saved, deleted or cancelled), and makes the Task Attachments window read-only when the task is deleted. An edit window whose task was deleted tells the user on Save or Delete and discards. |
| `v25_4-chart-perio-direction` | built | A direction chosen with the Current Direction buttons applies to the next keystroke from the current cell (input goes back to the chart's input box), holds while entry stays in the row section (also with Facials First, where the path used to override it on every keystroke), and ends when auto-advance crosses into another section, where the advance sequence sets the direction. The Forward/Reverse label follows every change at once. The arrow keys never change the direction. Voice perio keeps its own direction updates. |

## What was built

### Diagnostics (`v25_4-misc-diagnostics-info`)

- `OpenDentBusiness/Misc/LocalDiagnostics.cs` (new): `GetDiagnostics(patNum)` builds sections Program, Date and time, Computer, Database, Session and Preferences. Each value is read inside its own try/catch (`DiagnosticsText.AddLine`): a failing line prints `ERROR: message` and the rest continue, as before. Hardware through WMI (`Win32_OperatingSystem`, `Win32_Processor`) and .NET (`ComputerInfo`, `Screen.AllScreens`, `GetDiskFreeSpaceEx`, which also works for a network share image folder). The server's clock, version, time zones and default engine come from one query (`GetServerInfo`, middle tier aware), so the clock difference is measured right after the workstation time is read.
- The Preferences section shows the preferences Open Dental's diagnostics showed, without `RegistrationKey`, `RegistrationKeyIsDisabled` (this program has no registration keys) and the two MySQL password hashes. `IsSecretPrefName` also skips any name with pass, secret, token or key in it, in case one is added to the list later.
- The fork's own markers are shown: release version, `OdSchemaCompat`, `ForkSchemaLevel`, `ForkAdoptedFrom`.
- `OpenDentBusiness/Data Interface/BugSubmissions.cs`: `GetDiagnostics` now forwards to `LocalDiagnostics` (kept for callers built against the name). The old body built a `BugSubmission` record, the HQ submission object, to read its fields.
- `OpenDental/Forms/FormAbout.cs`: the Diagnostics button calls `LocalDiagnostics.GetDiagnostics`. The fork identity and the attribution text are unchanged (`AttributionTests` pass).

### Printers (`v25_4-misc-print-window`)

- `OpenDentBusiness/Misc/PrintSituationRules.cs` (new):
  - `PrintSituationRules.Resolve` applies the rules to one computer's rows: the situation's row when it names an installed printer, otherwise the Default situation's row (the program's own default, not Windows'), otherwise the Windows default printer. The prompt comes from the situation's row when it has one, otherwise from the Default row. An empty printer name or no row means "use the Default situation's printer, or the Windows default". Printers that are not installed are passed over and named. These are the rules `OpenDental.PrinterL.SetPrinter` applies when printing (its regions 1 and 2); that file was not changed (see Interplay).
  - `GetSourceText`, `GetPrinterUsedText`, `IsRowNeeded` (a row must exist when Prompt is on, even with no printer chosen), `GetWindowsDefaultPrinterName`.
  - `PrinterStatuses`: the state of every installed printer from `Win32_Printer` (`WorkOffline`, `ExtendedPrinterStatus`, `PrinterStatus`), one query per window with a 5 second timeout; Online, Offline, Paused, Error, or nothing when Windows reports no state.
- `OpenDentBusiness/Data Interface/Printers.cs`: `PutForSit` uses `IsRowNeeded` (same behavior as before; the rule now has one home).
- `OpenDentBusiness/TableTypes/Printer.cs`: the field summaries say what the 25.4 documentation clarifies. Comments only; the CRUD is unchanged.
- `OpenDental/Forms/FormPrinterSetup.cs` and designer: three columns, Printer Used, Rule and Status; the window is 300 pixels wider. Rows of situations without a printer row now have every cell.
- `OpenDental/Forms/FormPrinterEdit.cs` and designer: a state label next to the printer list, a line saying what the first item (default / Windows default) means for this situation, and a saved printer that is not installed on this computer stays selectable as "name (not installed)", so saving without choosing another printer keeps it (24.3 silently switched to the default and saved that).

### Referral phone (`v25_4-family-referral-phone`)

- `OpenDentBusiness/Misc/ReferralPhones.cs` (new): `GetDisplay`, `GetStorage`, `GetError` (only the 30-character column width is checked), `FromPatientPhone`.
- `WpfControlsOD/Frms/FrmReferralEdit.xaml`: the three boxes `(`, `)`, `-` became one box `textPhone`, 161 wide like Other Phone, `MaxLength="30"`, with the same as-you-type phone formatting as Other Phone.
- `WpfControlsOD/Frms/FrmReferralEdit.xaml.cs`: load, save, read-only (patient referrals) and the patient's home phone copied into a new patient referral use `ReferralPhones`. 24.3 showed nothing for a stored value that was not exactly 10 digits and then saved it as empty; such values are now shown and kept.

### Automation condition (`v26_1-setup-automation-clinic-mismatch`)

- `OpenDentBusiness/Data Interface/AutomationClinicMismatches.cs` (new): `IsMismatch`, `IsTriggerAllowed`, `GetSaveError`, `IsConditionMet`, `GetProceduresForTrigger`, `GetCompletedToday`.
- `OpenDentBusiness/Data Interface/Automations.cs`: `CheckAutomationConditions` gets the automation, the trigger and the procedure codes that fired (optional parameters) and has a case for the new field.
- Which procedures are checked: the ones the caller passes as the trigger object (`List<Procedure>` or one `Procedure`), otherwise the patient's procedures set complete today (`DateComplete`, which `Procedures.Insert`/`Update` set to today when a procedure is set complete) with one of the trigger's codes. Only procedures whose code is one of the automation's codes count. INFERRED, see below.
- `OpenDental/Forms/FormAutomationConditionEdit.cs`: new `AutomationTriggerCur` (set by the parent window). The field list keeps a list of the listed fields, because the list index is no longer the enum value (every `(AutoCondField)listCompareField.SelectedIndex` became `GetFieldSelected()`). The field is listed only for ProcedureComplete, or when the condition being edited already uses it; it hides the comparison and text, shows a note, and saves comparison None and an empty text. Loading an existing condition of this field no longer selects comparison index 4 (None is not in the list).
- `OpenDental/Forms/FormAutomationEdit.cs`: passes the trigger chosen in the window to the condition window and refuses to save while the condition is on another trigger.

### Task attachment windows (`v26_1-misc-task-attach-nonmodal`)

- `OpenDental/Forms/TaskAttachmentWindows.cs` (new): `ShowAttachments`, `ShowAttachmentEdit`, `CloseForTask`, `SetTaskDeleted`, and `OpenWindowRegistry<TWindow>`, the open-window bookkeeping (no WinForms code, tested on its own).
- `OpenDental/Forms/FormTaskEdit.cs`: Attachments opens the window non-modally (`OnTaskAttachmentsChanged` refreshes the attachment text and sends the task signal each time an attachment changes); `FormClosed` closes the task's attachment windows; `SetFormToDeletedMode` makes them read-only.
- `OpenDental/Forms/FormTaskAttachments.cs`: Add and double-click open edit windows non-modally; `ActionAttachmentsChanged`; `SetTaskDeleted`.
- `OpenDental/Forms/FormTaskAttachmentEdit.cs`: every `DialogResult` is followed by `Close()` (a modeless window does not close on its own); `IsTaskDeletedDiscard` on Save and Delete.
- `OpenDental/User Controls/UserControlTasks.cs` (`NavAtt_Clicked`): an attachment opened from the task list opens non-modally, owned by the window that holds the list.

### Perio Chart direction (`v25_4-chart-perio-direction`)

- `OpenDental/User Controls/ContrPerio.Direction.cs` (new, partial class): `SetDirectionManual`, `IsDirectionManual`, `AfterAdvanceDirection`, and `PerioDirectionRules` (the rules without the control).
- `OpenDental/User Controls/ContrPerio.cs` (made `partial`):
  - `AdvanceCell(isReverse,setDirection)`; the Left and Right arrow keys pass `setDirection:false`.
  - Facials First (`TryAutoAdvanceFacialsFirst`) honors a direction chosen with the buttons in its section and respects `setDirection`. The direction shown is the direction of entry; before, it followed every move, so Backspace and the arrow keys flipped it. The inner Maxillary First crossing rules no longer set the direction there.
  - Maxillary First section 3 crossing: missing braces raised `DirectionChangedRight` even when the direction did not change, so the Right button lit up while entry still went Left. Fixed.
- `OpenDental/Forms/FormPerio.cs`: the buttons call `SetDirectionManual` and put the input focus back in the chart's input box (24.3 focused the grid control, which often left the focus on the radio button, so the next number went nowhere and the arrow keys switched the radio buttons); the direction events also update the Forward/Reverse label.

## INFERRED behaviors (not public; minimal reasonable choice)

- Diagnostics: the exact 25.4 list of lines and their wording. The spec's list is followed; the fork markers were added.
- Print window: what "minor enhancements" means beyond the clarified rules is not public. Built as described above; the Setup, Printers columns are this fork's choice.
- Printer state mapping from `Win32_Printer` values (Not Available counts as Offline, Stopped Printing as Error).
- Referral phone: 10-digit numbers typed with punctuation are stored as the 10 digits, as 24.3 stored them, so existing letters, sheets and exports that format 10-digit values keep working. Patient referrals copy the home phone as stored (24.3 copied it only when it was exactly 10 digits).
- Clinic mismatch: the existing ProcedureComplete callers (Chart, Appointment, Account, Procedure Edit, `ProcedureL`, `Procedures.CompleteProcedureAutomation`) pass only procedure codes, and they live in hot files of other packages, so they were not changed. Without a procedure list, the condition looks at the patient's procedures set complete today with the trigger's codes; a procedure of the same code completed earlier the same day at another clinic also counts. Only procedures with one of the automation's own codes count. A procedure with no clinic (0) against a patient's clinic counts as a mismatch (the spec's rule is `procedure.ClinicNum != patient.ClinicNum`).
- Clinic mismatch list text: M2-02 gave the enum value the description "Procedure Clinic Differs From Patient Clinic"; the spec's UI name is "Patient Procedure Clinic Mismatch". The registry text was kept (M2-02 owns it).
- Task attachments: a task window that closes discards unsaved changes in its open attachment edit windows; edit windows opened from the task list (not from a task window) are closed too when that task's window closes.
- Perio: a "section" is one of the chart's four row sections (maxillary facial, maxillary lingual, mandibular lingual, mandibular facial). Up/Down arrows did not and do not change the direction.

## Deviations from the spec

- `v25_4-misc-print-window`: the Print Options window part is not built here (B-PRINT's window is not on this base).
- `OpenDental/Logic/PrinterL.cs`, `WpfControlsOD/Logic/PrinterL.cs`, `OpenDental/Logic/AutomationL.cs` and `OpenDentBusiness/Data Interface/AutomationConditions.cs` are in the owned list but needed no change. `PrinterL.cs` is also changed by B-PRINT in this run (it replaces the Windows print dialog in region 3 and makes the class partial); leaving it alone avoids a conflict. The WPF `PrinterL` says only its original author edits it.
- No fork migration step (none needed).

## Interplay with packages built at the same time

- **B-PRINT** (`v25_3-misc-printer-options`, the Print Options window): after the merge, `FrmPrintOptions` can show `PrintSituationRules.GetSourceText(PrintSituationRules.ResolveForThisComputer(situation))` under the printer list and `PrinterStatuses.GetText(PrinterStatuses.GetStatus(PrinterStatuses.GetAll(),printerName))` next to it, and keep its page range when the printer changes. B-PRINT's `PrintOptions.Remember` already covers copies per situation. If B-PRINT's `ShowPrintOptions` resolves the printer itself, it should use `PrintSituationRules.Resolve` so the two cannot drift. No file overlaps with B-PRINT besides the csproj files.
- **B-PERIO** edits `ContrPerio.cs` and `FormPerio.cs` (auto Att Ging rows, row layout). The hunks here do not touch theirs: the class declaration (`partial`), `AdvanceCell`, the Left/Right arrow keys in `OnKeyDown`, `TryAutoAdvanceFacialsFirst`, the section 3 braces in `TryAutoAdvanceLeft`, and in `FormPerio` the radio and direction-event handlers. The new code uses only `GetSection(row)` and the public cell methods, not fixed row numbers, so it does not depend on B-PERIO's layout. Merge B-PERIO's `GetTableRowForSection` as is.
- **B-TASK** and **B-APPT** edit `UserControlTasks.cs` (lines near 428, 1883 and 2124); the change here is only `NavAtt_Clicked` (near 2284).
- **M2-01**: `referral.Telephone varchar(30)` (step 2026102401) is what makes the 30 characters fit. On a database that has not run M2-01's step, a value longer than 10 characters would be cut or refused by the server; the fork migration runner always runs it first.
- **M2-02**: `AutoCondField.PatientProcedureClinicMismatch = 14` and its description are M2-02's. Its note says the switch ignored the value until this package; it is now evaluated.
- **Callers of the ProcedureComplete trigger** (owned by other packages: `ControlChart`, `ControlAppt`, `ControlAccount`, `FormProcEdit`, `FormApptEdit`, `ProcedureL`, `Procedures`): any of them can pass the completed procedures as the trigger object (`AutomationL.Trigger(EnumAutomationTrigger.ProcedureComplete,listProcCodes,patNum,triggerObj:listProcedures)`), and the condition then checks exactly those procedures. Not required; it makes the check exact.

## Files touched

- New: `OpenDentBusiness/Misc/LocalDiagnostics.cs`, `OpenDentBusiness/Misc/PrintSituationRules.cs`, `OpenDentBusiness/Misc/ReferralPhones.cs`, `OpenDentBusiness/Data Interface/AutomationClinicMismatches.cs`, `OpenDental/Forms/TaskAttachmentWindows.cs`, `OpenDental/User Controls/ContrPerio.Direction.cs`, `tests/M2MISCTests/*`, this note.
- Changed (owned): `OpenDentBusiness/Data Interface/{Automations,BugSubmissions,Printers}.cs`, `OpenDentBusiness/TableTypes/Printer.cs` (comments), `OpenDental/Forms/{FormAbout,FormAutomationConditionEdit,FormAutomationEdit,FormPerio,FormPrinterEdit(+Designer),FormPrinterSetup(+designer),FormTaskAttachmentEdit,FormTaskAttachments,FormTaskEdit}.cs`, `OpenDental/User Controls/{ContrPerio,UserControlTasks}.cs`, `WpfControlsOD/Frms/FrmReferralEdit.xaml(.cs)`.
- Changed (shared, small and local): `OpenDentBusiness/OpenDentBusiness.csproj` (4 compile lines, each next to its sibling), `OpenDental/OpenDental.csproj` (2 compile entries), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Foundation-owned registries: none changed.

## Tests

New project `tests/M2MISCTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). Database tests use a scratch MariaDB database on 127.0.0.1 named `m2misc_<time>_<pid>_<random>`, built from the 26.1.70 reference (`SchemaVerifier.GetReference()`), dropped afterwards; stale ones from crashed runs are dropped after 12 hours. Synthetic rows only (procedure codes `Z9001`/`Z9002`, not CDT).

- `DiagnosticsTests`: clock difference text, byte units, .NET release names, time zone text, no secret preferences, one failing line does not stop the rest; against the scratch database every section is there, the server clock, version and database name are read, no password hash or key value appears, `BugSubmissions.GetDiagnostics` returns the local text.
- `PrintRulesTests`: every rule (no rows, Default row, situation row, empty printer with prompt, printers not installed, spooler error), `IsRowNeeded`, the printer state mapping, `PutForSit` keeps a row for Prompt and removes an empty one, `ResolveForThisComputer`.
- `ReferralPhoneTests`: display, storage, error, patient phone; a 30-character phone round-trips through the database.
- `AutomationClinicMismatchTests`: the rules, the save refusal, and `Automations.Trigger` end to end (warns on another clinic, not on the same clinic, warns for a patient without a clinic, never with clinics off, only today's procedures with the automation's codes, procedures passed by the caller win, never on another trigger).
- `TaskAttachmentWindowTests`: the open-window registry and keys; source checks that the windows are opened with `Show`, closed with the task and discard when the task is gone; `Tasks.IsTaskDeleted` against the database.
- `PerioDirectionTests`: the rules; the grid off screen (Maxillary First and Facials First chosen direction, Backspace, arrow keys across sections, auto-advance into the next section, the section 3 event fix); source checks of the window's handlers.
- `UiSourceTests`: About (local diagnostics, attribution kept, no network code), Printer Setup and Edit Printer, Edit Referral, the automation windows.

## Checks (2026-10-25)

- Full rebuild of `OpenDental.Local.slnf` (Debug, x86): 0 errors, 376 warnings (the baseline); none in the new or changed files.
- Targeted tests: `M2MISCTests` 71/71, `AttributionTests` 15/15 (FormAbout), `TaskCategoryTests` 21/21 (source checks of `FormTaskEdit` and `UserControlTasks`), `M202Tests` 39/39 (the `AutoCondField` registry), `NoHqTests` 49/49.
- `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- No window was opened (OpenDental.exe is not launched in this run): the new grid columns and labels, the non-modal windows on screen (focus, owner, bring to front), the referral box's as-you-type formatting, and the radio button focus were checked by source tests only.
- Printer states on real printers (offline network printer, paused printer): only the mapping from the WMI values is tested; on this computer `PrinterStatuses.GetAll` is only checked not to throw.
- The Diagnostics image-folder free space on a network share, and the middle tier path of the server query.
- Voice perio with the new Facials First direction rules.

## Questions for the owner

1. Clinic mismatch: should the ProcedureComplete callers (Chart, Appointment, Account, Procedure Edit) be changed to pass the completed procedures, so the check never looks at other procedures completed earlier the same day? It is a one-line change per call site in files owned by other packages.
2. Clinic mismatch: does a procedure with no clinic (0) on a patient with a clinic count as a mismatch (built: yes, following `procedure.ClinicNum != patient.ClinicNum`)?
3. Referral phone: keep storing a plain 10-digit number as 10 digits (built), or store exactly what was typed?
4. Print window: once B-PRINT is merged, should the Print Options window show the rule and the printer state (described under Interplay), or is Setup, Printers enough?
5. Diagnostics: the patient number and the MySQL user name are shown (as before). Keep them?
