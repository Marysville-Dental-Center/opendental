# Port note: M2-SHEETS, sheets in non-Latin scripts and the duplicate field warning

Branch `port/m2-sheets`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run; review fixes on `port/m2-sheets-s1` and `port/m2-sheets-t1` (the latter merged with the integration head `port/int-7`, see Integration below). Backlog section: [port-backlog.md, M2-SHEETS](../specs/port-backlog.md#m2-sheets-sheets-non-latin-text-and-duplicate-field-warning). Feature specs: [features-25.4.md](../specs/features-25.4.md) (`v25_4-sheets-nonlatin`, `v25_4-sheets-duplicate-field-warning`). No schema change, no registry entry and no fork migration step: the level range 2027003701 to 2027003799 is unused.

Sources: the 25.4 release notes (https://www.opendental.com/site/version25_4.html, one line per feature), the public 24.3 manual page on sheet translations (https://www.opendental.com/manual243/sheetsetuplanguage.html) and the feature specs. The public material says only that sheets support non-Latin languages and that adding a duplicate field warns; everything below about how is this fork's own design, marked INFERRED where it is a behaviour choice. No Open Dental binary, install or gated manual page was used.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-sheets-nonlatin` | built | PDF output, font fallback (screen, print, PDF), right-to-left layout, language list, Unicode round trip. See the deviations: right-to-left, complex-script text and text in fonts the PDF library cannot embed go into PDFs as images. |
| `v25_4-sheets-duplicate-field-warning` | built | New input, output and checkbox fields and pasted fields. OK/Cancel instead of Continue/Cancel buttons. |

## What was built

### Non-Latin text (`v25_4-sheets-nonlatin`)

- **One helper for all sheet text in print and PDF.** `SheetDrawingJob` no longer calls `GraphicsHelper.DrawString`/`DrawStringX` itself: its 12 PDF calls (combo box, the five grid titles, the three grid footers, the two signature lines, text fields) go to `SheetTextDrawing.DrawStringX`, and its 4 print calls to `SheetTextDrawing.DrawString`. The `XFont` objects stay as they were and describe the requested font.
  - Latin text is handed to `GraphicsHelper` untouched, so every existing sheet prints and makes PDFs exactly as before (tested byte for byte and pixel for pixel). For PDFs, "Latin" means everything the old WinAnsi encoding holds (Latin-1 plus the Windows-1252 extras such as the euro sign and typographic quotes).
  - Other text in PDFs:
    - Cyrillic, Greek, Armenian, Georgian, Hangul syllables and the like, in a font the PDF library can embed: written as text with `XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.Always)` (the options ImageHelper and GridOD use), so it can be selected, searched and copied.
    - Right-to-left text (Hebrew, Arabic and the other right-to-left scripts) and complex scripts (Indic scripts such as Hindi, Thai, Lao, Khmer, Myanmar, Tibetan, Mongolian, conjoining Hangul jamo): drawn by the print engine (GDI+, which shapes and orders them) onto a transparent 300 dpi image placed at the field. PDFsharp 1.2 (the version in `Required dlls`) draws characters one by one, left to right, with no shaping: Hebrew came out reversed and Arabic unjoined in a probe.
    - Text whose fallback font PDFsharp 1.2 cannot load: also an image. PDFsharp 1.2 rejects fonts that Windows keeps in font collections (`.ttc`; "Font has no usable platform or encoding ID"), which are all Chinese and Japanese system fonts (Microsoft YaHei, JhengHei, SimSun, Yu Gothic, MS Gothic), Nirmala UI and Cambria on Windows 11. `SheetFonts.CanEmbedInPdf` probes each font once.
  - The image is wrapped like the printed text (the 4% larger layout box of `GraphicsHelper.DrawString`), shows only whole lines that fit (always at least the first), and is anchored like `GraphicsHelper.DrawStringX` anchors text, so left, right and centered text lines up with the other text of the PDF (checked by rendering a test PDF with the Windows PDF renderer). A field taller than about a page gets a lower resolution (at least 100 dpi), so one image stays under 12 million pixels. If drawing the image fails, the text is written as text, as before 25.4.
- **Font fallback** (`SheetFonts.GetFontNameForText`). When the field's font has no glyph for some character, the whole field is drawn in the first installed fallback font for the text's scripts that has all its glyphs: Segoe UI (and Arial, Times New Roman, Tahoma) for Cyrillic, Greek, Hebrew and Arabic; Microsoft YaHei, then JhengHei, SimSun and others for Chinese; Yu Gothic, Meiryo, MS Gothic for Japanese (kana in the text); Malgun Gothic for Korean (Hangul); Nirmala UI and the older Indic fonts for Indic scripts; Leelawadee UI and Tahoma for Thai; and so on, then general fonts. Glyph coverage comes from Windows (`GetGlyphIndices`) and is cached. Latin text never changes font, so sheets that use symbol fonts are unaffected. Used:
  - on screen: Fill Sheet text drawing, the in-place edit box, click-to-caret mapping and combo boxes (`FormSheetFillEdit`), and static text in the Sheet Def editor (`FormSheetDefEdit.DrawStringField`);
  - in print: through `SheetTextDrawing.DrawString`, plus the statement patient-name grid title;
  - in PDFs: through `SheetTextDrawing.DrawStringX`.
- **Right-to-left text.** Text whose first strong letter is right-to-left (Unicode bidi rule P2) is laid out right-to-left (`StringFormatFlags.DirectionRightToLeft`) on screen, in the designer, in print and in PDFs (`SheetTextDrawing.ApplyDirection`). Field positions and the designed alignment stay as they are (INFERRED, see below).
- **Language list.** The Sheet Def editor's language list and "Add New" list name languages through `SheetScripts.GetLanguageDisplayName`: the `CultureInfo` display name, followed by the native name when that is in a non-Latin script, for example "Russian (русский)" or "Chinese (中文)". Latin languages and custom languages show as before. Any culture in `LanguagesUsedByPatients` was already accepted.
- **Unicode input.** Fill Sheet input already accepts any text and the sheet tables are utf8 (utf8mb3); a database test confirms text in 11 scripts survives the CRUD round trip in `sheetfield` and `sheetfielddef`, including the `Language` code. VERIFY result for emoji (out of scope): on the local MariaDB 10.11 with OpenDental.exe's connection settings, a character beyond the Basic Multilingual Plane is stored as U+FFFD and the rest of the text is kept.

### Duplicate field warning (`v25_4-sheets-duplicate-field-warning`)

- `SheetFieldDefs.GetDuplicateKey`, `GetDuplicates` and `GetDuplicateDisplayName` (new methods in the owned data class) decide what counts as the same field:
  - input and output fields: same field type and field name; "misc" fields by their user-defined (reportable) name;
  - checkboxes and radio buttons: field name plus radio button value; "misc" checkboxes by radio button group plus user-defined name;
  - always within the same language and layout mode, ignoring case and surrounding spaces (as the database compares);
  - never checked: static text, lines, rectangles, images, patient images, signature boxes, grids, combo boxes, special fields, screen charts, mobile headers, fields without a name, and "misc" fields without a user-defined name.
- `FormSheetDefEdit.ConfirmDuplicateFields` asks before adding: after the field dialog of Add Output Text, Add Input Field and Add Check Box, and before a paste (Ctrl+V, the Paste button, Alt+click stamping). The message lists the duplicated fields (up to 10); OK adds them, Cancel does not.
- No double prompts: copying fields that conflict already asks (existing 24.3 prompt in `CopyControlsToMemory`); when the user agrees there, pasting that copy does not ask again. Likewise, after the user agrees once to paste a copy, further pastes of the same copy (stamping) do not ask again.
- Modifier keys after the box (fix round s1). The box is modal, so it swallows the key-up of the Ctrl (Ctrl+V) or Alt (Alt+click stamp) that started the paste, and `FormSheetDefEdit_KeyUp` never clears `_isCtrlDown` or `_isAltDown`. Before the fix, after the box a Ctrl+V paste left Ctrl "down" (clicks toggled the selection; V, Z or C alone pasted, undid or copied), and an Alt+click stamp left Alt "down" (every click stamped another copy after OK, or asked again after Cancel) until Alt was pressed again. `ConfirmDuplicateFields` now resets `_isCtrlDown`, `_isAltDown` and the cursor whenever it has shown the box, after OK and after Cancel, as `CopyControlsToMemory` already does after its own box. `PasteControlsFromMemory` saves the Alt state before the box (`isAltStamp`) and uses it for the cascade offset rule, so an Alt+click stamp still does not move the offset of the next Ctrl+V. The add-field paths share the reset; it is harmless there (no key is held when a button opens a field dialog).
- Tests for that (fix round t1). The editor asks the question through `_funcAskDuplicateFields`, a private field that defaults to the real OK/Cancel box and that the window never changes, so `SheetDefEditKeyTests` can answer it without a box. The answer comes back without a key-up, just as the real box swallows it.

## Files touched

- New (business layer): `OpenDentBusiness/SheetFramework/SheetScripts.cs` (script classification, right-to-left detection, language names, `SheetScriptGroup`), `SheetFonts.cs` (fallback, glyph coverage, PDF embedding probe), `SheetTextDrawing.cs` (print and PDF text drawing, `ApplyDirection`).
- Changed, owned: `OpenDentBusiness/SheetFramework/SheetDrawingJob.cs` (16 call sites renamed to the helper, one font name), `OpenDentBusiness/Data Interface/SheetFieldDefs.cs` (three methods appended), `OpenDental/Forms/FormSheetDefEdit.cs` (warning method with the modifier key reset, four hooks, two fields (the accepted copy and the question), the saved Alt state for the paste offset, language names, designer font and direction), `OpenDental/Forms/FormSheetFillEdit.cs` (five one-line hooks).
- Owned but unchanged: `OpenDental/SheetFramework/SheetPrinting.cs` (its print and PDF entry points only call `SheetDrawingJob`).
- Shared, small: `OpenDentBusiness/OpenDentBusiness.csproj` (3 compile entries), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Tests: `tests/M2SHEETSTests/` (new). It references `OpenDental.csproj` (as M2CANTests and others do) for the in-memory editor tests.
- Nothing outside the owned list besides the project and solution files; no foundation registry was touched.

## INFERRED behaviours

- Right-to-left text keeps the designed alignment physically: a left-aligned field shows Hebrew at its left edge, a right-aligned one at its right edge; only the reading order (where punctuation and numbers go, line order) follows the text. Chosen so existing sheets whose Hebrew or Arabic fields were right-aligned by hand do not flip.
- Paragraph direction follows the first strong letter of the field's text, so "Name: שלום" reads left-to-right with a right-to-left word, and "שלום 123." reads right-to-left.
- Fallback replaces the font of the whole field, not of single characters, so screen, print and PDF measure and wrap the same way. The fallback font keeps the field's size and bold setting.
- The fallback lists (which fonts, in which order) are this fork's choice; the spec names Segoe UI, Microsoft YaHei, Malgun Gothic and Nirmala UI.
- Duplicate identity details: an input and an output field of the same name are not duplicates of each other; combo boxes are not checked (the spec does not list them); comparison ignores case and surrounding spaces.
- The warning appears once per new field or per paste operation, not once per field of a paste, and not again for the same copy (see above).

## Deviations from the spec

- **PDF text as images.** The spec asks for Unicode font options at every `XFont` site. Done where the PDF library can lay the text out; right-to-left text, complex scripts and fonts PDFsharp 1.2 cannot embed are placed as 300 dpi transparent images instead. They look right and print sharply, but cannot be selected, searched or copied in the PDF, and make the file larger. Replacing PDFsharp 1.2 with a library that shapes text and reads font collections would remove this; that is beyond this package.
- **XFont sites.** The spec counted 14 `XFont` sites; the file now has 15 (A-SIG added the "signed by" line). The 12 that carry text other than fixed English labels go through the helper (the grid titles and footers too, since a translated or currency text can be non-Latin). Not routed: the screen chart tooth-surface letters (fixed one-letter codes drawn with `gx.DrawString`) and the DEBUG-only calibration page.
- **Buttons.** The warning uses the standard OK/Cancel message box with "Click OK to continue adding it" instead of buttons labelled Continue and Cancel.
- **"Copy".** The spec lists new, paste and copy. Copying does not add a field; the existing 24.3 copy prompt already warns about conflicting fields, and the new check runs on paste.

## Interplay with packages built at the same time

- **M2-SDV** owns `SheetUtil.cs` this wave. `SheetUtil.CalculateHeights` measures growing text fields with the field's own font (`GraphicsHelper.MeasureStringH`). With a fallback font (often taller, such as Microsoft YaHei), a field with growth behaviour can come out a little short and its last line be cut. Suggested follow-up after the merge: measure with `new Font(SheetFonts.GetFontNameForText(field.FontName,field.FieldValue),...)` there. Fields without growth behaviour are not affected.
- **M2-EF** (wave C2, merged on `port/int-7`) adds the "date submitted" box to `FormSheetFillEdit` (two hook lines, the rest in `FormSheetFillEdit.Submitted.cs`); the five hooks here are one-line changes in the paint, edit-box and hit-test code and do not touch that area. The merge was clean.
- **GridOD** (shared hot file, not owned) draws grid cells in PDFs with Unicode options for non-ASCII text but no font fallback and no shaping: Chinese patient names in a statement grid still show as missing glyphs in PDFs, and Hebrew as reversed. Not changed here; a later grid package could call `SheetFonts.GetFontNameForText` and `SheetTextDrawing` from `GridOD.DrawStringX`.
- `OpenDentBusiness/SheetFramework/SheetPrinting2.cs` (`DrawFieldSpecial`, tooth chart legend labels) and `GraphicsHelper.cs` are unchanged.

## Tests

`tests/M2SHEETSTests` (MSTest v1, x86): 53 tests, all passing on the build computer (Windows 11, local MariaDB 10.11).

- `ScriptTests`: WinAnsi and Latin detection, right-to-left detection (first strong letter), complex scripts, script groups, language names.
- `FontFallbackTests`: the fallback rule with made-up fonts (order, kana and Hangul priority, best coverage, characters that draw nothing), and the real fonts of the test computer (inconclusive when a font is missing): Arial keeps Cyrillic and Greek; Chinese in Arial uses Microsoft YaHei, Korean Malgun Gothic, Hindi Nirmala UI, Hebrew in a font without Hebrew Segoe UI; Microsoft YaHei cannot be embedded.
- `PdfTextTests`: whole sheet PDFs through `SheetDrawingJob.CreatePdf`, read back with PDFsharp: Latin output byte-identical to the old helper with a WinAnsi font; Cyrillic and Greek as Type0/Identity-H fonts with every code point in the ToUnicode map; Korean in an embedded Malgun Gothic; Hebrew, Arabic, Hindi, Thai and mixed text as one transparent 300 dpi image each; Chinese as an image; the resolution cap for a huge field; a sheet with every script and field kind. Set `M2SHEETS_PDF_DIR` to keep that PDF for a look.
- `PrintTextTests`: `ApplyDirection`; Latin print output pixel-identical to `GraphicsHelper.DrawString`; right-to-left text on the designed side; every script draws.
- `DuplicateFieldTests`: every identity rule above.
- `SheetDefEditKeyTests` (fix round t1): the Sheet Def editor built in memory (never shown, no database) with a synthetic patient form whose "misc" input field (reportable name "Reason for visit") is selected and copied with Ctrl+C. The tests call the window's KeyDown, KeyUp and panel MouseDown/MouseUp handlers as Windows would and answer the duplicate field question with a stand-in that, like the real box, sends no key-up. Ctrl+V answered OK or Cancel leaves Ctrl up: a plain click on blank sheet clears the selection and V alone neither pastes nor asks. Alt+click answered Cancel or OK leaves Alt up and the cursor default: the next plain click neither asks nor stamps, and the stamp does not move the Ctrl+V cascade offset. Pressing the key again works as before: an accepted copy pastes or stamps without asking again, and the key stays down until its key-up when no box was shown. A paste that duplicates nothing shows no box and keeps Ctrl down. With the reset removed from `ConfirmDuplicateFields`, these tests fail.
- `WiringTests`: the windows and `SheetDrawingJob` call the helpers (source checks). `SheetDefEdit_DuplicateFieldBoxResetsTheModifierKeys` (fix round s1, adjusted in t1 for `_funcAskDuplicateFields`) reads the method bodies of the editor (comments, strings and chars skipped when matching braces) and checks that Ctrl+V and Alt+click reach the paste, that nothing returns between the duplicate field box and the reset of Ctrl, Alt and the cursor (so OK and Cancel both reset, and only when the box was shown), and that the paste saves the Alt state before the box and decides the cascade offset only from that saved state.
- `UnicodeDbTests`: text in 11 scripts round-trips through the `sheetfield`, `sheetdef` and `sheetfielddef` CRUD on a scratch database built from the 26.1.70 reference schema.

Related existing assemblies run: `SheetTests` (check sheet PDFs through `SheetDrawingJob`), `SignatureTests` (signature lines and the A-SIG source checks), `NoHqTests`.

## Owner questions

1. Right-to-left alignment: keep the designed side (built), or treat "left" as "start" so Hebrew and Arabic in a left-aligned field hug the right edge?
2. Is it acceptable that right-to-left, Indic and Chinese/Japanese text in sheet PDFs is an image (not searchable or copyable), until the PDF library is replaced?
3. Should output fields be part of the duplicate warning? The spec says yes, but the same output field (patient name, date) is often placed twice on purpose, and every such add now asks.
4. The language list now appends the native name for non-Latin languages ("Russian (русский)"). Keep, or show only the English name as before?

## Not tested

- The windows themselves (the real Sheet Def editor warning box, Fill Sheet drawing, editing and caret placement with fallback fonts and right-to-left text): OpenDental.exe is not launched in this run. The editor's key handling after the box is tested in memory with a stand-in for the box; that the real modal box swallows the key-up is the review's finding, not observed here.
- A physical printer: the print path was drawn to bitmaps with the same code.
- Other Windows versions and font sets: tested on Windows 11 only. Fallback choices and which fonts PDFsharp can embed depend on the installed fonts and on whether Windows ships a font as a single file or as a collection.
- Growth-behaviour heights with fallback fonts (`SheetUtil`, M2-SDV's file).

## Integration (branch `port/m2-sheets-t1`, merged with `port/int-7`)

- `port/int-7` (commit `1763cc7ae`) merged without conflicts. Packages on it that touch this package's files:
  - **B-PRINT**: five `if(IsAutoSaveCopyDue())` lines and the removed "Autosave to Images" box in `FormSheetFillEdit`; untouched by this package's hooks.
  - **M2-EF**: `LoadDateSubmitted()` and `TrySetDateSubmitted()` in `FormSheetFillEdit` (see Interplay).
  - **The 25.3 statement inserter barcode** (`FormSheetDefEdit.InserterBarcode.cs`, `StatementInserterBarcode.cs`, one line in `SheetPrintingJob`): draws bars only, no text, so it needs no fallback font or PDF text helper.
  - No package on `port/int-7` changed `SheetDrawingJob.cs`, `SheetFieldDefs.cs` or `SheetPrinting.cs`, and none added sheet text drawing that bypasses `SheetTextDrawing`. No helper on the base duplicates `SheetScripts`, `SheetFonts` or `SheetTextDrawing`.
- **M2-SDV** is not on `port/int-7`; the `SheetUtil` growth-height follow-up above still applies when it merges.
- No schema, no fork migration step and no adoption change: the M1-Z adoption tools need nothing from this package.
