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
| `v25_1-sheets-autosave-images` | built | Fill Sheet has no "Autosave to Images" box; a save that changed the sheet copies it to Imaging when the layout says so. |
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

- `SheetAutoSaveImages` (new, business layer) holds the rules; `FormSheetFillEdit.AutoSaveImages.cs` (new partial) applies them. The five
  places that used `_isAutoSave && checkSaveToImages.Checked` (E-mail, Create PDF, Print, Print/E-mail, OK) call `IsAutoSaveCopyDue()`.
- A copy is written when the sheet has a patient, a non-hidden image category is flagged Autosave Forms ("U") (kept from 24.3 even when the
  layout names its own category), the sheet's layout has the option on, the window is not in a kiosk/terminal, and the save changed the sheet.
- "Changed": a fingerprint of the sheet (date to the minute, description, internal note, kiosk order, patient, and every field's type, name,
  radio value/group, position, size and value, signatures included) taken when the window is shown, compared after the save; field order and
  line-ending differences do not count. A new sheet's first save always counts. After a copy the fingerprint moves on.
- `FormSheetDef`: the option now reads **Copy to Imaging when saved**; the category override is kept.
- **INFERRED:** sheets filled at a kiosk are not copied there (as in 24.3); they are copied the next time the office saves them with a change.

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
- Page ranges: System.Drawing calls the page handler for every page whatever the range says, so `PageRangePrintController` lets the handler
  draw every page (its page state must advance) but sends only the pages in the range to the printer, and stops after the last one. WPF uses
  `PageRangeDocumentPaginator`.
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
- Barcode: Code 39 only (no DataMatrix, no ZXing).
- Export: one Export button with a menu in the 25.3 windows (two buttons in Transaction History, as the 24.4 spec asks).
- `SheetFiller.cs` and `GridOD.cs` (named as touchpoints) needed no change.

## Interplay with packages built in parallel

- **M2-MISC** (owns `PrinterL` in C1; `v25_4-misc-print-window`, "minor enhancements to the Print window"): should build on `FrmPrintOptions`
  and `PrinterL.PrintOptions.cs` from this package rather than on the Windows dialog. Its base does not have them.
- **M2-SHEETS / M2-EF** (`FormSheetFillEdit`, `FormSheetDefEdit`, `SheetPrinting`): B-PRINT's edits there are small: five `if(IsAutoSaveCopyDue())`
  lines, the removed checkbox block, one statement e-mail line, one `case` in `DrawSpecial`, and one check in `butSave_Click`. New logic is in
  the partial files. A merge conflict, if any, is in those lines only.
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
4. Kiosk-filled sheets are copied to Imaging only when the office later saves them with a change. Should a completed kiosk sheet be copied
   when the patient finishes instead?
5. Print Options appears when a print situation has Prompt on and from every print preview's Print button. Should it also appear for prints
   that go straight to the printer (for example grid prints in Release builds)?
6. Should the remembered print choices be cleared at log-off (currently kept until the program closes)?
7. `GridOD.Export` (24.3 text/".xls" export) now has no callers. Remove it in a later cleanup?

## Tests

`tests/BPRINTTests` (61 tests): export text rules and a grid export, e-mail names, Code 39 patterns against the published symbology, barcode
content/placement/clear zone, stamping synthetic PDF print documents, the Billing window batch matching, Print Options rules, page-range
printing (System.Drawing, inconclusive without an installed printer; WPF on an STA thread), the copy-to-Imaging rules, and source checks of the
windows. Database tests (one scratch database `forkmig_<run>_bprint`, dropped at the end): the preference row, the barcode on printed pages,
per-statement layouts, and the layout's copy option.

Related existing assemblies to run: `SheetTests`, `SignatureTests` (reads `FormSheetFillEdit`), `StatementTests`.

Not tested automatically: the windows themselves (Print Options, Billing Defaults, Edit CheckBox tooltips, Fill Sheet saves), printing to a
real printer, scanning a printed barcode with an inserter, and the middle-tier path of the preference write.
