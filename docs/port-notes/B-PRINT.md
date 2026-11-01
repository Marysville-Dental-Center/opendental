# B-PRINT: statements, sheets, printing and grid export

Backlog package B-PRINT (M1 Tier B, wave B1), branch `port/b-print`, built in the single wave against `m1/integration-a2-patch`.
Sources: Open Dental's public release notes for 24.4, 25.1 and 25.3, the 24.3 manual pages named in the feature specs, and the published
database documents. Nothing was observed in an Open Dental program. Where the public material says what changed but not how, the behaviour
below is the most reasonable minimal one and is marked **INFERRED**.

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-manage-accounting-export` | built | Transaction History (`FormJournal`) has **Export .txt** and **Export .csv** toolbar buttons. Both use the shared export (`GridODExport`), which the Chart of Accounts (`FormAccounting`) now uses too. |
| `v25_3-misc-export-csv-txt` | built | The Export button of every other window that exported a grid opens a small menu with **Export .csv** and **Export .txt**. |
| `v25_3-manage-statement-email-lastname` | built | An e-mailed statement's PDF is named `Statement_<LastName>.pdf`. |
| `v25_3-manage-statement-barcode` | built | Optional Code 39 inserter barcode on every printed statement page. |
| `v25_1-sheets-autosave-images` | built | Fill Sheet has no "Autosave to Images" box; a save that changed the sheet copies it to Imaging when the layout says so, including the patient's save at a kiosk or terminal (deviation, see below). |
| `v25_3-sheets-checkbox-cleanup` | built | Edit CheckBox (sheets) loses its long instruction labels; the guidance is kept as tooltips. |
| `v25_3-misc-printer-options` | built | The program's own **Print Options** window (printer, copies, collate, page range) replaces the Windows print dialog, and also opens from the Print button of every print preview. |

## How each feature works

### Export .csv and Export .txt (24.4 and 25.3)

- `OpenDentBusiness/UI/GridODExport.cs` (new): the text rules and the save step, as extension methods of `GridOD`, so `GridOD.cs` is not
  edited. `.csv` follows RFC 4180 (a field with a comma, a quote or a line break is quoted, inner quotes doubled). `.txt` is tab-separated with
  tabs and line breaks inside a field turned into spaces, never quoted. First row = column headings, CRLF line ends, UTF-8 with a byte order
  mark. The save dialog starts in the `ExportPath` preference folder (created when missing) and suggests the grid title (or the window's old
  export name) as the file name. An empty grid shows "Nothing to export".
- A field that a spreadsheet would run as a formula (`=`, `+`, `@`, or `-` when it is not a number) gets a leading space. 24.3's Chart of
  Accounts export already did this for headings; it now applies to every field. **INFERRED** (protects exports opened in Excel).
- Windows: `FormJournal` (two toolbar buttons), `FormAccounting` (its two existing menu items now call the shared code), and a drop-down menu on
  the Export button of `FormTransactionEdit`, `FormReconcileEdit`, `FormEmployeeSelect`, the three TSI grids of `FormArManager`,
  `FormRpProcNote`, `FormRpProcedures` and `FormRpInsPendingSupplementals` (every caller of `GridOD.Export`). **INFERRED:** a menu on the
  existing button instead of two buttons, so no designer file of those windows changes. `GridOD.Export` (24.3 text or ".xls") is left in place
  with no callers.

### E-mailed statements carry the last name (25.3)

- `Statements.GetEmailPdfDisplayName(patient)` (new partial `Statements.EmailName.cs`): `Statement_<LastName>.pdf`. Only the last name of
  the statement's patient (the guarantor for a family statement) is used, never the first name, birth date or account number. Characters
  Windows refuses in file names and control characters are dropped, runs of white space become one space, leading and trailing dots and
  spaces are trimmed, and the name is cut to 40 characters. No usable last name: `Statement.pdf` as in 24.3.
- Used by the Billing window, Statement Options, Fill Sheet (statement e-mail), the Account module's e-mail path
  (`EmailMessages.CreateEmailMessageForStatement`) and `BillingL`'s default callbacks. The patient-portal receipt e-mail
  (`Statements.EmailStatementPatientPortal`) is an excluded eService path and is unchanged.
- **INFERRED:** the last name goes in the attachment name only; the subject still comes from the Billing Defaults subject template (which can
  already hold patient tags).

### Inserter barcode on statements (25.3)

The release note says only that statements can carry a barcode for envelope-inserting machines. Symbology, content, placement and the
setting's name are not public, so all of the following is this fork's design (**INFERRED**):

- Setting: **Print inserter barcode on statements** in Billing Defaults, off by default. Stored as the preference row
  `BillingStatementInserterBarcode` (`1`/`0`). The row is written the first time the office turns the barcode on
  (`StatementInserterBarcode.SetEnabled`, same pattern as the DentalXChange partner preference); a database without the row reads as off. No
  fork migration step, no `PrefName` entry and no `PrefInfos.xml` entry, so no foundation registry changes.
- Symbology: Code 39 (`OpenDentBusiness/Misc/Code39Barcode.cs`, written from the public symbology; no barcode library), narrow:wide 1:3,
  10-unit quiet zones, drawn black on a white box.
- Content, 12 digits per page: statement sequence number in the print run (5 digits, from 1, wraps after 99999), page number (3), page count
  (3), end-of-set flag (1 on the statement's last page). Example `000070020021` = 7th statement, page 2 of 2.
- Placement: a **Special** field `StatementInserterBarcode` on a custom statement layout (the sheet editor now shows the Special button for
  Statement layouts and offers this one field, 2 x 0.25 inch by default, drawn as a sample barcode). Its first-page position is used on every
  page. Without the field (internal layout, or a field that does not fit on the first page) the barcode goes in the top-right margin
  (0.25 inch from the right edge, 0.16 to 0.39 inch from the top; the internal statement layout has nothing there).
- Clear zone: saving a statement layout whose barcode touches other fields of the same language and layout mode asks "Save anyway?".
- Where it prints: statements printed through `SheetPrinting.Print` (Statement Options, Fill Sheet, Account module; each copy of a statement
  printed on its own is its own set, so the sequence is the copy number), and the Billing window's print document (the PDF opened for
  printing): `StatementInserterBarcodeBatch` records each printed statement's page count as `BillingL` adds its pages, then stamps the
  document before it is saved, one sequence for the whole run, each statement placed by its own layout. If the printed statements cannot be
  matched to the page counts, every barcode goes in the default spot; pages that cannot be accounted for are never stamped.
- Never on the copy saved to Imaging, e-mailed statements or electronic statements. The Fill Sheet "Create PDF" of a statement has no barcode
  either (**INFERRED**: the spec's "PDF statements" is read as the Billing window's print PDF).

### Sheet copies to Imaging (25.1)

- `SheetAutoSaveImages` and `SheetAutoSaveTracker` (new, business layer) hold the rules; `FormSheetFillEdit.AutoSaveImages.cs` (new partial)
  applies them. The five places that used `_isAutoSave && checkSaveToImages.Checked` (E-mail, Create PDF, Print, Print/E-mail, OK) call
  `IsAutoSaveCopyDue()`.
- A copy is written when the sheet has a patient, a non-hidden image category is flagged Autosave Forms ("U") (kept from 24.3 even when the
  layout names its own category), the sheet's layout has the option on, and the save changed the sheet.
- "Changed": a fingerprint of the sheet (date to the minute, description, internal note, kiosk order, patient, and every field's type, name,
  radio value/group, position, size and value, signatures included) taken when the window is shown (`SheetAutoSaveTracker.TakeSnapshot`),
  compared after the save; field order and line-ending differences do not count. A new sheet's first save always counts. After a copy the
  fingerprint moves on.
- Kiosk and terminal: the patient's save follows the same rules, so a form filled in and signed at the kiosk reaches Imaging when the patient
  clicks OK there. Staff who later open it and click OK without changes write nothing more; staff who correct it get another copy. The copy is
  made inside `IsAutoSaveCopyDue` in a try/catch: a copy that fails at the kiosk (for example a kiosk computer that cannot reach the image
  folder) shows the patient no error and writes a `SheetEdit` audit trail entry for the patient instead (**INFERRED**). "To Kiosk" makes no copy,
  as in 24.3, so a blank form sent to the kiosk is never copied.
- `FormSheetDef`: the option now reads **Copy to Imaging when saved**; the category override is kept.

### Edit CheckBox cleanup (25.3)

- Removed: the two instruction labels inside the radio-button group boxes (`label9`, `label1`). Shortened: `labelRequired` ("Same for the
  whole group"), `labelMiscInstructions` ("One Reportable Name per button"), `labelYesNo` ("Pair with a "No" box").
- The full guidance is kept as tooltips on the group boxes, Required, Reportable Name and the Yes/No radio buttons
  (`FormSheetFieldCheckBox.Instructions.cs`, new partial). The eForm checkbox window is unchanged. **INFERRED** wording (our own).

### Print Options window (25.3)

- `WpfControlsOD/Frms/FrmPrintOptions` (new WPF window): printer (installed printers, pre-selected by the print situation's rules or the last
  choice), the print situation's name, copies (1 to 999), collate, pages (All, or From/To), Print and Cancel.
- `PrinterL.SetPrinter` shows it instead of the Windows `PrintDialog` whenever the situation has Prompt on, and when the caller forces it.
  The WinForms print preview's Print button and the WPF print preview's Print button (and Ctrl+P) force it (**INFERRED:** the areas 25.3
  added). WPF printing (`WpfControlsOD/Logic/PrinterL`) now prints to the printer the situation rules pick and shows the window when Prompt is on.
- The last choice (printer, copies, collate) is remembered per print situation until the program closes; a page range is never remembered.
  A remembered printer that is no longer installed is ignored. **INFERRED:** memory is not cleared on log-off.
- The remembered choice only pre-fills the window. A print that shows no window, WinForms or WPF, prints to the printer the situation's
  rules pick (Setup, Printers; the Windows default when none), one copy, collated, every page (`PrintOptions.GetChoiceToPrint` /
  `GetChoiceForUnpromptedPrint`, used by `PrintOptionsWpf.TryPrintFixedDocument`; WinForms `PrinterL.SetPrinter` already behaved so). The
  memory is shared by WinForms and WPF printing and every WPF print uses the Default situation, so a choice made in any print preview's window
  never sends a later eForm, child care map or message-box print to another printer or in several copies.
- Page ranges: System.Drawing calls the page handler for every page whatever the range says, so `PageRangePrintController` lets the handler
  draw every page (its page state must advance) but sends only the pages in the range to the printer. Pages before and after the range are
  drawn on a small scratch bitmap and thrown away. The controller never ends the print early: the handler runs to its own last page, because
  `FormQuery.pd2_PrintPage` resets its page state (`_headerPrinted`, `_tablePrinted`, `_totalsPrinted`, `_summaryPrinted`) only there, and a
  print cut short after the range left that state behind for the next preview or print (no title on page 1, or the totals and summary right
  after page 1's rows). WPF uses `PageRangeDocumentPaginator`.
- The controller is the only thing that applies the range. `PageRangePrintController.ApplyTo` (called by `PrinterL.TrySetPrinter` and the old
  `PrinterL.SetPrinter(PrintDocument,...)`) wraps the document's controller and then puts `PrinterSettings.PrintRange` back to `AllPages`.
  Three print paths apply `FromPage`/`ToPage` themselves: `FormReportComplex.pd2_PrintPage` (every standard report),
  `FormQuery.pd2_PrintPage` (User Query) and `RdlViewer.Print` (RDL reports through `FormReportForRdl`/`FormReportCustom`). They skip the
  earlier pages and send page `FromPage` first, so with both applying the range, pages 3-5 printed only page 5 and pages 2-2 printed nothing.
  With `AllPages` they draw every page to the end and the controller sends `FromPage` to `ToPage`, exactly as for the handlers that ignore
  the range. The printed pages are the ones 24.3 printed for the same range; the difference is that the handler also lays out the pages
  after the range (not drawn on paper), so it finishes and resets its own state as after a full print. No change in those three files.
- Kept: the `Printing` audit entry, virtual (print-to-file) printers (choosing another printer turns print-to-file off), remote printing
  (never prompts), the printer-number override.

## Files

New: `OpenDentBusiness/UI/GridODExport.cs`, `OpenDentBusiness/Data Interface/Statements.EmailName.cs`, `OpenDentBusiness/Misc/Code39Barcode.cs`,
`OpenDentBusiness/Misc/PrintOptions.cs`, `OpenDentBusiness/SheetFramework/SheetAutoSaveImages.cs`,
`OpenDentBusiness/SheetFramework/StatementInserterBarcode.cs`, `OpenDental/Logic/PrinterL.PrintOptions.cs`,
`OpenDental/Forms/FormSheetFillEdit.AutoSaveImages.cs`, `OpenDental/Forms/FormSheetDefEdit.InserterBarcode.cs`,
`OpenDental/Forms/FormSheetFieldCheckBox.Instructions.cs`, `WpfControlsOD/Frms/FrmPrintOptions.xaml(.cs)`, `WpfControlsOD/Logic/PrintOptionsWpf.cs`,
`tests/BPRINTTests/*`.

Changed (owned by B-PRINT): `FormAccounting(.designer)`, `FormBilling`, `FormBillingDefaults(.Designer)`, `FormJournal`, `FormSheetDef.designer`,
`FormSheetFieldCheckBox.designer`, `FormSheetFillEdit(.designer)`, `FormStatementOptions`, `OpenDental/Logic/PrinterL.cs`,
`WpfControlsOD/Frms/FrmPrintPreview.xaml.cs`, `SheetFieldsAvailable.cs`, and the export windows (`FormTransactionEdit`, `FormReconcileEdit`,
`FormEmployeeSelect`, `FormArManager`, `FormRpProcNote`, `FormRpProcedures`, `FormRpInsPendingSupplementals`: one line each).

Changed outside B-PRINT's file list (one-line or few-line hooks): `OpenDentBusiness/Data Interface/SheetDefs.cs` (Special button for Statement
layouts), `OpenDentBusiness/Data Interface/EmailMessages.cs` and `OpenDentBusiness/Logic/BillingL.cs` (attachment name),
`OpenDental/Forms/FormSheetDefEdit.cs` (draw the barcode field, clear-zone check on save), `OpenDental/SheetFramework/SheetPrintingJob.cs`
(barcode on printed statement pages), `WpfControlsOD/Logic/PrinterL.cs` (WPF print through Print Options), `OpenDental/UI/FormPrintPreview.cs`
(Print button forces the window), the three `.csproj` files, `OpenDental.sln`, `OpenDental.Local.slnf`.

Foundation registries: none changed (no TableTypes, Crud, `Pref.cs`, `PrefInfos.xml`, permissions, security tree, display fields, enum files or
seed rows). The only new stored value is the runtime preference row `BillingStatementInserterBarcode`.

## Deviations from the specs

- The feature spec proposed a fork migration step for the barcode preference; there is none. The row is written when the office first turns
  the barcode on. The interrupted run had a step at level 2027001501 (this package's assigned range); that level is not a valid `yyyyMMddNN`
  date (month 00), so `ForkMigrationRunner.ValidateStep` would have thrown and stopped every migration. Removed.
- Sheet copies to Imaging, kiosk and terminal: the spec proposed keeping 24.3's "no copy in a terminal" and making the copy when the office
  later saves or imports the sheet. This package makes the copy at the patient's save in the terminal instead, because a saved sheet carries
  nothing that says its last save still owes a copy:
  - `sheet.DateTSheetEdited` moves on with every OK, changed or not (`FormSheetFillEdit.TryToSaveData`; the column's own documentation says so).
    A rule such as "no Autosave Forms document of the patient is newer than the sheet's last save" is therefore also true after any unchanged
    OK of any sheet, and the next unchanged OK would copy again: every second unchanged OK would write a copy, against the spec's "opening and
    closing without changes writes nothing".
  - A copy in Imaging is not linked to its sheet (description `PatientForm<DocNum>`), so a check per patient cannot tell one sheet's copy from
    another's. A patient usually fills in several forms at the kiosk; the copy made when staff review the first would hide the others.
  - A per-sheet marker would need a new column or a stored value outside this package's files (and the package's migration level range cannot
    be used, see owner question 1).
  24.3 copied a kiosk sheet only at the office's later OK (box checked by default), so offices still get one copy per kiosk form, made earlier.
  Because nothing waits for a later save, `FormSheetImport` (B-EF) needs no import hook for this feature.
- Export: one Export button with a menu in the 25.3 windows (two buttons in Transaction History, as the 24.4 spec asks).
- `SheetFiller.cs` and `GridOD.cs` (named as touchpoints) needed no change.

## Interplay with packages built in parallel

- **M2-MISC** (owns `PrinterL` in C1; `v25_4-misc-print-window`, "minor enhancements to the Print window"): should build on `FrmPrintOptions`
  and `PrinterL.PrintOptions.cs` from this package rather than on the Windows dialog. Its base does not have them.
- **M2-SHEETS / M2-EF** (`FormSheetFillEdit`, `FormSheetDefEdit`, `SheetPrinting`): B-PRINT's edits there are small: five `if(IsAutoSaveCopyDue())`
  lines, the removed checkbox block and its two comment lines, one statement e-mail line, one `case` in `DrawSpecial`, and one check in
  `butSave_Click`. New logic is in the partial files. A merge conflict, if any, is in those lines only.
- **B-EF** (`FormSheetImport`): nothing to do for the copy to Imaging. Kiosk and terminal sheets get their copy when the patient saves them, so
  an import does not have to make one. If B-EF's import saves the sheet through `FormSheetFillEdit`, the usual rule applies (a copy only when
  that save changed the sheet).
- **Reports and User Query** (`FormReportComplex`, `FormQuery`, `RdlViewer`): untouched. A package that adds its own page-range handling to a
  print job should leave the range to `PageRangePrintController` (the settings read `AllPages` once the controller is in place).
- **M2-DUN** (`BillingL`, `Statements`, `EmailMessages`) and **B-DBM** (`EmailMessages`): one line each (`DisplayedFileName`). Statement code
  of B-PRINT is in the new partial `Statements.EmailName.cs`.
- **M2-SDV** (`FormStatementOptions`): one line (`DisplayedFileName`).
- **B-SERVER** (`FormRpProcNote`), **M2-EMP** (`FormEmployeeSelect`): one line each in `butExport_Click`.
- The solution files and the three `.csproj` files get entries next to other packages' entries; expect trivial merge conflicts there.

## Owner questions

1. The level range given for this package (2027001501 to 2027001599) fails the runner's `yyyyMMddNN` check (month 00). Other packages with
   2027 ranges will hit the same refusal; either the ranges or `ForkMigrationRunner.ValidateStep` need to change. B-PRINT needs no step.
2. Inserter barcode: does the office's folding/inserting machine read Code 39 with this content (sequence, page, page count, end-of-set), or
   does it need another format (OMR marks, 2D code, a different field order)? Is the top-right margin a usable default spot for its scanner?
3. Should the e-mail subject also carry the last name, or only the attachment name (current)?
4. Kiosk-filled sheets are copied to Imaging when the patient saves them at the kiosk (the spec proposed waiting for the office's save; see
   Deviations). Is that acceptable, and do all kiosk computers reach the image folder? A copy that fails at the kiosk is only noted in the audit
   trail. If the office wants the copy to wait for staff review, a per-sheet "copy owed" marker (a new column) would be needed.
5. Print Options appears when a print situation has Prompt on and from every print preview's Print button. Should it also appear for prints
   that go straight to the printer (for example grid prints in Release builds)?
6. Should the remembered print choices be cleared at log-off (currently kept until the program closes)?
7. `GridOD.Export` (24.3 text/".xls" export) now has no callers. Remove it in a later cleanup?

## Tests

`tests/BPRINTTests` (71 tests): export text rules and a grid export, e-mail names, Code 39 patterns against the published symbology, barcode
content/placement/clear zone, stamping synthetic PDF print documents, the Billing window batch matching, Print Options rules, page-range
printing (System.Drawing, inconclusive without an installed printer; WPF on an STA thread), the copy-to-Imaging rules, and source checks of the
windows. Database tests (one scratch database `forkmig_<run>_bprint`, dropped at the end): the preference row, the barcode on printed pages,
per-statement layouts, and the layout's copy option.

Page ranges are printed to a print preview with three kinds of page handler: one that skips the earlier pages itself (as `FormQuery` and
`FormReportComplex` do), one that reads the range when printing starts (as `RdlViewer` does) and one that ignores the range. For pages 3-5 of
7 and 2-2 each sends exactly those pages, once each. With the `AllPages` reset removed, the first two fail (pages 3-5 gave only page 5).
A fourth handler is modelled on `FormQuery` (title on the first page only, state reset only when the handler reaches its own end, nothing
reset when printing starts, only the page and row counters reset before each print as `PrintReport` does): pages 1-2 of 5 and then all pages
print the title on page 1 and all five pages again; pages 1-5 of 6 (table and totals inside the range, summary after it) and then all pages
print six pages, not page 1 with the totals and summary under its rows. With the old early stop after `ToPage` both fail.
`UnpromptedPrint_UsesTheRulesPrinterWithOneCopy_NeverTheRememberedChoice` remembers another printer with three copies for the Default
situation and checks that a print without the window resolves to the rules' printer with one copy, while the window still opens with the
remembered choice.
The copy-to-Imaging tests follow one kiosk form through the kiosk save, a second kiosk OK, two unchanged office OKs and an office correction:
one copy until the correction, then a second.

Related existing assemblies to run: `SheetTests`, `SignatureTests` (reads `FormSheetFillEdit`), `StatementTests`.

Not tested automatically: the windows themselves (Print Options, Billing Defaults, Edit CheckBox tooltips, Fill Sheet saves), printing to a
real printer, scanning a printed barcode with an inserter, and the middle-tier path of the preference write.

## Review fixes (branch `port/b-print-r1`)

- Page ranges were applied twice for reports, User Query and RDL reports (pages 3-5 printed only page 5, pages 2-2 nothing). Fixed in
  `PageRangePrintController.ApplyTo` (settings back to `AllPages` once the controller is in place); `PrinterL.ApplyPageRange` calls it.
- Kiosk and terminal sheets never reached Imaging unless staff changed them. Fixed by copying at the patient's save in the terminal (the
  reviewers' second option); the reviewers' first option (a pending copy detected from document dates) was not used, for the reasons under
  Deviations.

## Review fixes (branch `port/b-print-s1`)

- WPF prints without the Print Options window used the remembered choice of the Default situation (printer, copies, collation), which every
  WinForms print preview's window also writes, so a choice of 2 copies on printer B in any preview sent every later eForm, child care map,
  message-box and error-report print there in 2 copies. Fixed: `PrintOptionsWpf.TryPrintFixedDocument` gets its choice from
  `PrintOptions.GetChoiceToPrint`, which returns the remembered choice only when the window will be shown (to pre-fill it) and otherwise the
  rules' printer with one copy (`GetChoiceForUnpromptedPrint`), matching WinForms `PrinterL.SetPrinter`. Test:
  `PrintOptionsTests.UnpromptedPrint_UsesTheRulesPrinterWithOneCopy_NeverTheRememberedChoice`, plus a source check in `WiringTests`.
  (Reviewer items 1 and 3 describe the same defect.)
- User Query printing after a page-range print lost its title and could stop after page 1, because `PageRangePrintController.OnEndPage`
  ended the print after `ToPage` and `FormQuery` resets its page state only at its own end. Fixed: the controller no longer ends the print
  early; pages after the range are laid out on the scratch bitmap and discarded. Tests:
  `PrintOptionsTests.PageRange_QueryLikeHandler_ARangePrintLeavesNoStateBehindForTheNextPrint` (new `FormQuery`-like document) and the
  updated `PageRange_OnlyThePagesInTheRangeReachThePrinter` (the handler now draws all 5 pages, 2 of them printed).

## Integration (branch `port/int-2`)

- B-SERVER added the shared helpers for fork-owned preference rows (`PrefC.GetRawOrDefault`, `PrefC.UpsertRawNoCache`). `StatementInserterBarcode.IsEnabled` and `UpsertPref` now use them for `BillingStatementInserterBarcode` instead of their own copy of the same code; the behaviour is unchanged (a missing row reads as off, the row is inserted the first time the barcode is turned on, `UpsertPref` keeps its Middle Tier call).
