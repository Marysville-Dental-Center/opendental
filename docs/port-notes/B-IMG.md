# Port note: B-IMG, Imaging tools

Branch `port/b-img`, cut from `m1/integration-a2-patch`. Wave B1 (built in the single-wave run, next to every other package). Public sources only: the
Open Dental 25.1, 25.2 and 25.3 release-note pages and the 24.3 manual pages the specs cite. No Open Dental binary was run or looked at.

| Feature ID | Status |
|---|---|
| `v25_2-imaging-copy-without-drawings` | built |
| `v25_3-imaging-font-autocalc` | built |
| `v25_1-imaging-scanning-setup-details` | built |

No schema change, no preference, no permission and no fork migration step (the assigned level range 2027000801-2027000899 is unused).

## v25_2-imaging-copy-without-drawings: Copy without drawings

**What it does.** The Imaging module's **Copy** toolbar button is now a split button: clicking it copies as before (drawings included), and its arrow
opens **Copy** and **Copy without drawings**. The image right-click menu has the same two items, one under the other. Copy without drawings keeps the
crop, rotation, flip and brightness/contrast and leaves out every drawing, text, measurement and AI annotation:

- a mount item: drawn without `DrawDrawings`;
- a whole mount: drawn with `DrawMount(g,includeDrawings:false)`;
- a single image: drawn whenever plain Copy would draw it (any crop, rotation, flip, brightness/contrast change or drawing, AI annotations
  included), with `DrawDocument(g,includeDrawings:false)`, so the bitmap and the JPG file on the clipboard have no drawings. An image whose only
  change is drawings is drawn too, as the spec says ("render without them, even when the file itself is unedited"), so Copy and Copy without
  drawings always put the same clipboard formats (a bitmap and a JPG file) on the clipboard for the same image. Only an image plain Copy also
  copies as its stored file (no edits and no drawings) is copied as the stored file;
- PDFs and other files that are not images: the stored file, exactly like Copy.

**Files.**

- `OpenDentBusiness/Imaging/ImageCopyRules.cs` (new): `MustDrawForCopy(document,includeDrawings)` (both commands go through
  `ImageHelper.HasImageBeenEdited`, so plain Copy decides exactly as before and Copy without drawings takes the same path; `includeDrawings` only
  decides whether the drawing step draws the drawings) and `GetLogText`.
- `OpenDental/User Controls/ControlImageDisplay.cs`: `ToolBarCopy_Click` and the new `ToolBarCopyWithoutDrawings_Click` call one
  `CopyToClipboard(bool includeDrawings)` (the old copy body, with the three changes above). `DrawMount` and `DrawDocument` gained an optional
  `includeDrawings=true`, so the screen, printing and export are unchanged. `menuPanelMain_Click` now dispatches by menu item instead of by position,
  because the new item shifts the positions.
- `OpenDental/User Controls/ControlImageDisplay.Designer.cs`: `menuItemCopyWithoutDrawings` after `menuItemCopy`; the later items' `Index` values are
  renumbered to match.
- `OpenDental/Main Modules/ControlImages.cs`: the Copy button is a `DropDownButton` with the two-item menu (same tag, so the existing enable/disable
  logic covers it), and a new `ToolBarCopyWithoutDrawings_Click` that forwards to the display. Two small, local edits in this hot file.

**INFERRED.**

- An image whose only change is drawings is drawn and saved exactly as plain Copy does (a bitmap plus a JPG file at the same quality 40 that plain
  Copy uses), just without the drawings, rather than copied as its stored file. This follows the spec rule and keeps the two commands giving the
  same clipboard formats; copying the stored file would have given programs that read only a bitmap (Paint, email bodies, web forms) nothing, and for
  a DICOM the raw `.dcm`. The JPG quality is not public for Copy without drawings; it is the one plain Copy already uses.
- A drawing counts whether or not it is shown (hidden layers, AI annotations of a turned-off vendor), as in plain Copy's existing
  `ImageHelper.HasImageBeenEdited`; the drawn copy then simply has nothing to leave out.
- The security log entry of Copy without drawings is the Copy text followed by " without drawings" (untranslated, like the rest of the copy log
  text), so the audit trail tells the two commands apart. Plain Copy's text is unchanged.
- Pasting inside the program goes by the copied document or mount (`NodeTypeAndKey`) and builds new documents from the stored files; it has never
  copied drawings, so both commands put the same key on the clipboard. Only the picture other programs receive (in the same bitmap and file
  formats) differs: with or without the drawings.
- The right-click menu text is not translated, like the other items of that menu.

**Deviation.** The spec's touchpoint `CodeBase/Utilities/ODCloudClient.cs` (`CopyToClipboard`, Open Dental Cloud only) no longer exists: the cloud
edition was removed. Nothing to do there.

**Not covered.** The old Images module (`ControlImagesOld.cs`, not owned by this package and not named by the spec) keeps its single Copy.

**Review fix (`port/b-img-r1`).** The first version copied the stored file for an image whose only change was drawings, so Copy without drawings
put only a file (for a DICOM, the raw `.dcm`) on the clipboard where Copy put a bitmap and a JPG. `MustDrawForCopy` now returns
`ImageHelper.HasImageBeenEdited` for both commands, as the spec rule asks; the helpers `HasViewEdits` and `IsImageFile`, used only for the old
decision, were removed.

## v25_3-imaging-font-autocalc: font sizes update as you type

**What it does.** In the Imaging module's **Edit Text** window, typing a valid number in **Actual Size** updates **Apparent Size at Current Zoom** at
once, and the other way round. The **Calculate Apparent** and **Calculate Actual** buttons are gone, and so is the recalculation on focus loss. Blank
or invalid text (letters, below 1, above 100000000, the limits of the boxes) is ignored until it becomes valid. A guard flag stops the box that was
just set from setting the first box back, so the two never update each other in a loop and the box being typed in is never rewritten.

**Files.**

- `OpenDentBusiness/Imaging/ImageDrawFontSizeSync.cs` (new): the two conversions (the same math as before: apparent = actual x zoom / 100 / screen
  scale), two-decimal formatting, parsing with the boxes' rules, and the guarded update.
- `WpfControlsOD/Frms/FrmImageDrawEdit.xaml.cs`: the boxes' `TextChanged` events go to the sync object, built on load (after the caller sets
  `ZoomVal`); loading the actual size fills the apparent size through the same event.
- `WpfControlsOD/Frms/FrmImageDrawEdit.xaml`: the two buttons removed; the Font group box narrowed from 291 to 200 pixels to fit what is left.

**INFERRED.** The group box width (not public). Whether the apparent box follows the box's lower limit of 1 while typing: it does, as the box itself
would flag a smaller value.

## v25_1-imaging-scanning-setup-details: Details button in Scanning Setup

**What it does.** A **Details** button beside **Show Select Scanner Window** in Setup, Imaging (window title "Scanning Setup") opens a message that
explains, in our own words, when to turn the option on: with some Canon scanners, after scanning, some PDFs stop opening in the Imaging module and
list boxes in other windows such as Commlog stop showing their selected row; choosing the scanner before each scan avoids both.

**Files.** `OpenDental/Forms/FormImagingSetup.cs` (the click handler; the button added to the window's translation list) and
`FormImagingSetup.designer.cs` (the button, inside the Documents group box, on the check box's row, overlapping nothing).

**TWAIN.** The button only shows text. No TWAIN code or library was added or changed; the window's only TWAIN calls are still the two of Set Default
Scanner. The decoupling plan's later scanning rewrite should recheck whether the Canon problems still happen and keep the button either way.

**INFERRED.** The exact wording (our own; the public sources give only the two problems). A message box rather than a separate dialog, as the spec
allows.

## Tests

New project `tests/BIMGTests` (MSTest v1, x86, .NET 4.8), in `OpenDental.sln` and `OpenDental.Local.slnf`:

- `ImageCopyRulesTests` (no database: the cases decided before the drawing lookup): every view edit makes both commands draw; PDFs and a missing
  document never draw; the log text.
- `ImageCopyRulesDbTests` (scratch database `forkmig_<run>_bimg`, built like the other test projects, dropped afterwards; skipped when the local
  MariaDB does not answer): an unedited image with no drawings is copied as its file by both; a drawing, an AI annotation, or a drawing on a DICOM
  makes both commands draw (Copy without drawings then leaves the drawings out); view edits make both draw; PDFs never draw; for a table of
  variants, with and without drawings, both commands decide exactly as `ImageHelper.HasImageBeenEdited`.
- `FontSizeSyncTests`: both directions, every keystroke, invalid and blank text, screen scale, two decimals, zero zoom, round trips, and a fake pair of
  boxes that raise their change event when set (no loop, the typed box never rewritten).
- `WiringTests` (the GUI is not launched, so these read the sources): the split button and its menu; the copy body skips drawings in all three
  paths and keeps the PDF path; a single image is copied as its file only through `MustDrawForCopy`, and otherwise drawn into the clipboard
  bitmap with `DrawDocument(g,includeDrawings)`; `DrawMount`/`DrawDocument` keep drawings by default; the right-click menu order, designer
  indexes and dispatch by item; pasting inside the program never touches drawings; the Edit Text window has no Calculate buttons or focus-loss
  handlers and wires both boxes to the sync; the Details button's place and that its handler only shows text.

Untested: the windows themselves were not opened (no GUI run), so the pixels of a copied image and the look of the split button and the Details
button are checked by the owner.

Checks on this branch: the build of `OpenDental.Local.slnf` has 0 errors and 376 warnings (the baseline); the targeted test assemblies pass
(BIMGTests 28 of 28, the database tests included; ImagingTests 43 of 43, which also reads `ControlImageDisplay.cs`; NoHqTests 49 of 49);
`tools/Test-NoHqHosts.ps1` passes. A verifier should run `BIMGTests` and `ImagingTests`.

## Interplay with packages built in parallel

- **V-PEARL1** (takes `ControlImages.cs` over from this package): this package changed only the Copy button line in `LayoutToolBars` and added
  `ToolBarCopyWithoutDrawings_Click` after `ToolBarCopy_Click`. Pearl annotations are drawings: Copy without drawings leaves them out whatever the
  Pearl layer settings.
- **M2-IMG** (also owns `ControlImageDisplay.cs` and `ImageHelper.cs`): this package did not edit `ImageHelper.cs`. Both copy commands decide
  through `ImageHelper.HasImageBeenEdited`, so if M2-IMG changes what it counts as an edit (for example 16-bit windowing values), Copy without
  drawings follows automatically; nothing in this package needs to change.
- **B-SHELL** (later owner of `ControlImages.cs`): nothing to connect.
- `OpenDentBusiness.csproj`: two `Compile` lines after `Imaging\BitmapDicom.cs`.

## Questions for the owner

1. Should Copy without drawings also be added to the old Images module (`ControlImagesOld.cs`), if any workstation still uses it?
2. Is the log text "... copied to clipboard without drawings" what you want in the audit trail, or should both commands log the same text?
3. Please read the Details message once in the window and say whether the wording fits the office.
