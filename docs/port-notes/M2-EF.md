# Port note: M2-EF, eForms 25.4 and 26.1

Branch `port/m2-ef`, cut from `port/foundation` (M2-01 schema and CRUD, M2-02 registries). Built in the single-wave run, beside the other M2 packages. Backlog section: [port-backlog.md, M2-EF](../specs/port-backlog.md#m2-ef-eforms-254-and-261). No fork migration step was needed (assigned range 2027004801 to 2027004899, unused).

Sources: Open Dental's public 25.4 and 26.1 release notes, the published database documents (`eformfield.DbLink`/`eformfielddef.DbLink` may hold patient fields, `eformfield.ValueLabel` is a FlowDocument, `eform.DateTimeSubmitted`, `sheet.DateTimeSubmitted`, `eformfield.DateTimeSig`), and the specs `features-25.4.md` and `features-26.1.md`. No Open Dental program was installed, observed or decompiled; no office data was used.

## Features

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-eforms-patfields` | built | Text box, date, check box and radio button fields can link to the office's patient fields; fill, validation, reviewed import with audit, rename follows, missing links shown and skipped. |
| `v25_4-eforms-richtext-paste` | built | RTF (Word, e-mail) and HTML (browsers) pasted into an eForm label keep basic formatting; plain text and in-program copies paste as before. |
| `v25_4-eforms-multi-import` | built | Setup, eForms, Import takes several files: name order, failures listed, " (2)" on clashes, counts at the end, one EFormEdit log per form. |
| `v26_1-chart-eform-sig-datetime` | built | `eformfield.DateTimeSig` is set, cleared and kept per the four rules; "Signed date time" caption under the box on screen and in prints and PDFs. |
| `v26_1-misc-audit-eform-delete` | built | Deleting a saved patient eForm writes an EFormDelete Audit Trail entry with the form, its date, its ID and the submitted date. |
| `v25_4-sheets-datetimesubmitted` | built | "Submitted" box in Fill Sheet (patient form types) and Fill eForm, default to now when a new form is saved signed, Submitted column in Patient Forms. The eClipboard part is excluded. |

## What was built

### Patient fields in eForms (`v25_4-eforms-patfields`)

- `OpenDentBusiness/EForms/EFormPatFields.cs` (new): the link format, the type mapping, the designer list, missing-link detection, fill values, the Currency check, the conversion of answers to stored values, the import plan (`GetImports`) and the import itself (`Import`), and the rename (`UpdateDbLinksForRename`).
- Link format: `PatField:` + `patfielddef.FieldName` (INFERRED; Open Dental's own format is not public, the spec asks to verify it).
- Type mapping as the spec says: Text and Currency to a text box, Date to a date field, Checkbox to a check box, PickList to radio buttons whose stored values and default labels are the pick list items.
- Designer: the four field editors (`FrmEFormTextBoxEdit`, `FrmEFormDateFieldEdit`, `FrmEFormCheckBoxEdit`, `FrmEFormRadioButtonsEdit`) list `PatField:<name>` entries after the built-in links, one per visible definition of a compatible type in Setup order (`EFormL.AddPatFieldDbLinks`). A link whose definition was deleted, is hidden, or changed to an incompatible type shows as `PatField:<name> (missing)` and is kept on save. Picking a link fills an empty label with the field name. Radio buttons get the pick list (`EFormFieldsAvailable.GetRadioDbAll/DbDefault/VisDefault`).
- Fill: `EFormFiller.FillFields` calls `EFormPatFields.FillFields`; text, date and currency show the stored value, a check box is checked when the stored value is "1", radio buttons select the stored item when it is one of their stored values. Missing links start empty.
- Save: `EForms.Validate` rejects a non-number in a text box linked to a Currency field. The existing ten-digit phone check no longer applies to patient field links whose name contains "Phone".
- Import: `FormSheetImport` gets a "Patient Fields" section (`FormSheetImport.PatFields.cs`, two hook lines in `FormSheetImport.cs`). A changed answer is checked for import; an answer that would clear a stored value (blank text, date or currency, an unchecked box) is shown but left unchecked, so the user confirms it, as the spec asks. Missing links and unreadable answers (not a number, not a date, not a pick list item) show in red. The import value can be edited in the grid and is converted again. Each change writes its own PatientFieldEdit entry ("Added/Edited/Deleted patient field ... from eForm #N (description)"), and the import grid's usual SheetEdit summary still covers it.
- Rename: `PatFields.UpdateFieldName` (called by Setup, Patient Field Defs on a rename) also changes the links in `eformfielddef` and in filled `eformfield` rows, refreshes the eForm definition cache and signals other workstations.

### Rich text paste (`v25_4-eforms-richtext-paste`)

- `OpenDentBusiness/EForms/EFormRichPaste.cs` (new): RTF goes through WPF's own RTF reader, HTML through a small parser written for this (no third-party code); both become a simple model, then a `Section` with only these local values: bold, italic, underline, text color (black is left as the default), font size relative to the main size of the pasted text (the main size becomes the label's 11.5 base; others scale and are kept between 50% and 300%, the FontScale range), paragraph alignment, `Margin=0` paragraphs as the internal eForms use, line breaks, bulleted and numbered lists (marker style kept). Dropped: images and embedded objects, tables (text kept, one paragraph per row, cells separated by tabs), hyperlinks (text kept without link color or underline), fonts (the form's font is used), background and highlight colors, script and style content.
- `FrmEFormLabelEdit` attaches `EFormL.RichTextBox_Pasting` to both label boxes (`DataObject.AddPastingHandler`, two lines). The handler swaps the clipboard data for the cleaned XAML. Plain text is unchanged; content that carries WPF XAML (copied inside this program, for example from another label) keeps WPF's own paste, so label-to-label copies lose nothing. "Paste Plain Text" in the context menu is unchanged.

### Several eForms at once (`v25_4-eforms-multi-import`)

- `OpenDentBusiness/EForms/EFormDefImport.cs` (new) and `FrmEFormDefs.butImport_Click` (the file dialog allows several files; one file goes through the same path). Files in name order; a file that is not an exported eForm, has no description, or has an unknown form or field type is skipped and listed; a description already used (ignoring case) gets " (2)", " (3)" and so on; one EFormEdit Audit Trail entry per imported form ("eForm <description> imported from file <name>"); a message with the imported and failed counts and the failed files.

### Signature time (`v26_1-chart-eform-sig-datetime`)

- `EFormFields.SetDateTimeSigs` (called by `FrmEFormFillEdit` before the change check, with `MiscData.GetNowDateTime()`, the server time): a signature that is new or different from the one loaded or last saved gets the time; a cleared box gets MinValue; an unchanged signature keeps its time, so saving again changes nothing; other field types are untouched.
- `CtrlEFormFill` shows "Signed <date> <time>" under a signed box (`AddSigCaption`, a third child of the box's panel, so the code that reads child 1 is unaffected) and prints it under the signature image (`PrintSigCaption`), which also covers Save to Images right after signing.

### Date submitted (`v25_4-sheets-datetimesubmitted`)

- `OpenDentBusiness/EForms/FormSubmittedDates.cs` (new): display, parsing, the default rule and the Patient Forms lookup.
- Fill eForm: a "Submitted" box under Description (`FrmEFormFillEdit.xaml`). Fill Sheet: a "Submitted" box above Add Field, created in `FormSheetFillEdit.Submitted.cs` (new partial file; two hook lines in `FormSheetFillEdit.cs`), shown for patient forms, medical histories and consents, not in the kiosk or for statements. Empty means not submitted. A new form saved with a signature and no date entered gets the current time (the spec's fork decision). A box that still shows the stored value keeps the stored value exactly (the box has no seconds).
- Patient Forms: a Submitted column, filled from one query per refresh (`FormSubmittedDates.GetForPatient`); documents stay empty.

### eForm delete audit (`v26_1-misc-audit-eform-delete`)

- `EForms.GetDeleteLogText` and `EForms.MakeDeleteLogEntry`; `FrmEFormFillEdit.butDelete_Click` calls it after `EForms.Delete`. Text: "eForm <description> dated <date time> (ID <EFormNum>) deleted", plus ", submitted <date time>" when the 25.4 date is set. A new form that was never saved, or a form with no patient, logs nothing.

## INFERRED behaviours

- The link format `PatField:<FieldName>`; matching by name ignores case, as MySQL does.
- Hidden definitions count as missing (the spec: "hidden or deleted defs show as (missing)"), and so does a definition whose type no longer fits the field.
- Patient fields are never Auto Import fields; they import only through the reviewed import. Auto Import's published list does not include them.
- When two eForm fields link to the same patient field, the last one with an answer counts.
- Stored formats: Currency is stored with two decimals unless the stored value is already the same amount; Date as the short date unless the stored value is already the same date; Checkbox "1" or no row (as Setup, Patient Fields does); PickList as the item name (hidden items accepted).
- A blank answer, including an unchecked box, clears a stored value only when the user checks the row in the import window.
- Rich paste: the RTF is preferred over the HTML when both are on the clipboard; the main size is the size most characters have; sizes within a quarter point of the base are left unset; near-black colors (all channels under 24) count as black; HTML headings are bold and sized like a browser's defaults; a list item copied without its list becomes a bulleted list.
- Multi-import validation (description, form type, field types) and the log text.
- Date submitted: shown in Fill Sheet only for patient forms, medical histories and consents; the default "now" uses the workstation time, like `DateTimeSheet`; the signature time uses the server time, as the spec says.
- The delete log date format (short date and time).

## Deviations from the spec

- The spec names `FrmEFormFieldPicker` as the database link picker. In this code base that window picks conditional parents; the database link is chosen in each field editor's combo box, so the patient fields were added there. `FrmEFormFieldPicker` and `FrmEFormSigBoxEdit` were not changed.
- The spec's "Patient Fields group" is the `PatField:` prefix on the entries (the combo box has no groups).
- Fill Sheet: no "Now" button; the spec's fork default (now, when a new form is signed) is used instead, for sheets and eForms alike.

## Changes outside the owned list

- `WpfControlsOD/Frms/FrmEFormTextBoxEdit.xaml.cs`, `FrmEFormDateFieldEdit.xaml.cs`, `FrmEFormCheckBoxEdit.xaml.cs`, `FrmEFormRadioButtonsEdit.xaml.cs`: one hook line each (`EFormL.AddPatFieldDbLinks`), and the default-label line now goes through `EFormL.GetDefaultLabelForDbLink`. No package of this wave owns them (A-EF1/A-EF2/B-EF in M1).
- `OpenDental/Forms/FormSheetImport.cs` (two hook lines) and the new `FormSheetImport.PatFields.cs`; `OpenDental/Forms/FormPatientForms.cs` (the Submitted column). Owned by B-EF in M1; nobody owns them in this wave.
- `OpenDentBusiness/OpenDentBusiness.csproj` (four compile entries), `OpenDental/OpenDental.csproj` (two partial files), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- No registry, enum, preference, permission, CRUD or schema change: EFormEdit, EFormDelete and PatientFieldEdit already exist, and the three columns come from M2-01.

## Interplay with packages built at the same time

- **M2-SHEETS** (non-Latin text, duplicate field warning) also edits `FormSheetFillEdit.cs`. This package adds only two hook lines there (`LoadDateSubmitted();` in Load after the Show In Terminal box, `TrySetDateSubmitted()` in `TryToSaveData` after the Date time parse) and puts the rest in `FormSheetFillEdit.Submitted.cs`, so the merge should be line-local. The Submitted box sits above Add Field; if M2-SHEETS adds a control in that corner, move one of them.
- **M2-AUDIT** logs birthdate changes from eForm import in `Patients.cs`; patient field rows are written by `PatFields`, not `Patients`, so the two do not overlap.
- **M2-01** columns (`eform.DateTimeSubmitted`, `sheet.DateTimeSubmitted`, `eformfield.DateTimeSig`) and their CRUD are used as they are.
- Any package adding projects to `OpenDental.sln`/`OpenDental.Local.slnf` or compile entries next to `EForms\EFormPatListLayout.cs`, `Forms\FormSheetImport.designer.cs` or `Forms\FormSheetFillEdit.designer.cs` will meet these lines in a trivial merge.

## Tests

New project `tests/M2EFTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 46 tests:

- `PatFieldLogicTests`: link format, type mapping, designer list, missing links, pick lists, fill values, Currency check, answer conversion, the import plan (changes, clearing, hidden by conditional logic, unanswered radio buttons, missing links, two fields for one patient field), no Auto Import.
- `SignatureAndSubmittedLogicTests`: the four signature-time rules, the caption, date submitted display/parse/default and the kept seconds, patient form sheet types, the delete log text.
- `RichPasteTests` (WPF on an STA thread): Word-like RTF and a browser clipboard HTML block (kept and dropped formatting, relative sizes, alignment, lists, table text, link text, no images, fonts, backgrounds or script text), fragment markers and offsets, items without their list, CSS colors and sizes, size clamping, which clipboard contents use the normal paste, and loading the pasted section into a label and saving it as label XAML.
- `M2EFDbTests` (scratch database `m2eftest_<time>_<pid>_<random>_db`, built from the seed dump, the 24.3.63 chain and the fork runner, dropped afterwards; stale ones from crashed runs are dropped after 12 hours): fill from stored patient fields, pick lists from Setup, Currency validation and the "Phone" name case, import insert/update/delete with one PatientFieldEdit entry per change and a second import changing nothing, rename updating definitions, filled forms and the cache, signature time and date submitted stored and read back, the Patient Forms lookup for sheets and eForms, the delete audit (and no entry for unsaved or patient-less forms), multiple import (name order, failures, " (2)"/" (3)", fields, one log per form, caches).
- `WiringTests`: the hooks in the windows that the tests can't load (field editors, label editor paste handler, multi-select import, Fill eForm, the sig caption in `CtrlEFormFill`, Fill Sheet, Patient Forms, Sheet Import).

Also run because they cover changed code: `EFormTests` (eForm fill, import, statuses), `SheetTests`.

## Checks (2026-10-25, on the finished branch)

- Build of `OpenDental.Local.slnf` (Debug, x86) from a clean tree: 0 errors, 376 warnings (the baseline).
- Targeted tests, all passed: `M2EFTests` 46/46, `EFormTests` 60/60, `SheetTests` 31/31, `NoHqTests` 49/49.
- `tools/Test-NoHqHosts.ps1`: passed.

## Not tested

- No window was opened (OpenDental.exe is not launched in this run): the field editors' combo boxes, the label editor's real clipboard paste from Word and a browser, the import grid, the Submitted boxes' placement at other zoom levels, and the printed caption are checked by source wiring tests and the business-layer tests only.
- The WPF paste pipeline itself (`DataObject.Pasting` with `FormatToApply=Xaml`) is not driven from a test; the tests load the same XAML into a RichTextBox selection with `TextRange.Load(DataFormats.Xaml)`.
- Topaz signatures (deterministic re-encryption is assumed for "unchanged" detection, as for the built-in signature box).

## Questions for the owner

1. Open Dental's own `DbLink` format for patient fields is not public. If an Open Dental 25.4+ database is ever adopted, its eForm links to patient fields may need a one-time conversion. Is that in scope?
2. Should a blank answer clear a stored patient field by default in the import window (Open Dental's behavior is not public), or stay unchecked as built?
3. Should patient fields be allowed as Auto Import fields?
4. The Submitted box is shown in Fill Sheet only for patient forms, medical histories and consents. Should other sheet types have it too?
