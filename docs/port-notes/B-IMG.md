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
- a single image: if its only change is drawings, the stored file is copied (it never holds drawings); if it has a crop, rotation, flip or
  brightness/contrast change, it is drawn with `DrawDocument(g,includeDrawings:false)`;
- PDFs and other files that are not images: the stored file, exactly like Copy.

**Files.**

- `OpenDentBusiness/Imaging/ImageCopyRules.cs` (new): `IsImageFile`, `HasViewEdits` (the crop, rotation, flip and windowing checks of
  `ImageHelper.HasImageBeenEdited`, without the drawing lookup), `MustDrawForCopy(document,includeDrawings)` (Copy still goes through
  `ImageHelper.HasImageBeenEdited`, so plain Copy decides exactly as before) and `GetLogText`.
- `OpenDental/User Controls/ControlImageDisplay.cs`: `ToolBarCopy_Click` and the new `ToolBarCopyWithoutDrawings_Click` call one
  `CopyToClipboard(bool includeDrawings)` (the old copy body, with the three changes above). `DrawMount` and `DrawDocument` gained an optional
  `includeDrawings=true`, so the screen, printing and export are unchanged. `menuPanelMain_Click` now dispatches by menu item instead of by position,
  because the new item shifts the positions.
- `OpenDental/User Controls/ControlImageDisplay.Designer.cs`: `menuItemCopyWithoutDrawings` after `menuItemCopy`; the later items' `Index` values are
  renumbered to match.
- `OpenDental/Main Modules/ControlImages.cs`: the Copy button is a `DropDownButton` with the two-item menu (same tag, so the existing enable/disable
  logic covers it), and a new `ToolBarCopyWithoutDrawings_Click` that forwards to the display. Two small, local edits in this hot file.

**INFERRED.**

- An image whose only change is drawings is copied as its stored file (lossless) rather than redrawn and saved as a 40% quality JPEG. The picture is
  the same; the spec's "render without them, even when the file itself is unedited" is met because the stored file has no drawings.
- The security log entry of Copy without drawings is the Copy text followed by " without drawings" (untranslated, like the rest of the copy log
  text), so the audit trail tells the two commands apart. Plain Copy's text is unchanged.
- Pasting inside the program goes by the copied document or mount (`NodeTypeAndKey`) and builds new documents from the stored files; it has never
  copied drawings, so both commands put the same key on the clipboard. Only what other programs receive (bitmap and file) differs.
- The right-click menu text is not translated, like the other items of that menu.

**Deviation.** The spec's touchpoint `CodeBase/Utilities/ODCloudClient.cs` (`CopyToClipboard`, Open Dental Cloud only) no longer exists: the cloud
edition was removed. Nothing to do there.

**Not covered.** The old Images module (`ControlImagesOld.cs`, not owned by this package and not named by the spec) keeps its single Copy.

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

- `ImageCopyRulesTests`: which files are images, every view edit, PDFs, the log text.
- `ImageCopyRulesDbTests` (scratch database `forkmig_<run>_bimg`, built like the other test projects, dropped afterwards; skipped when the local
  MariaDB does not answer): drawings and AI annotations make Copy draw but not Copy without drawings; view edits make both draw; PDFs never draw; plain
  Copy decides exactly as `ImageHelper.HasImageBeenEdited`; `HasViewEdits` agrees with it when there are no drawings.
- `FontSizeSyncTests`: both directions, every keystroke, invalid and blank text, screen scale, two decimals, zero zoom, round trips, and a fake pair of
  boxes that raise their change event when set (no loop, the typed box never rewritten).
- `WiringTests` (the GUI is not launched, so these read the sources): the split button and its menu; the copy body skips drawings in all three
  paths and keeps the PDF path; `DrawMount`/`DrawDocument` keep drawings by default; the right-click menu order, designer indexes and dispatch by
  item; pasting inside the program never touches drawings; the Edit Text window has no Calculate buttons or focus-loss handlers and wires both boxes
  to the sync; the Details button's place and that its handler only shows text.

Untested: the windows themselves were not opened (no GUI run), so the pixels of a copied image and the look of the split button and the Details
button are checked by the owner.

Checks on this branch: the build of `OpenDental.Local.slnf` has 0 errors and 376 warnings (the baseline); every test assembly passes (BIMGTests
30 of 30, the database tests included); `tools/Test-NoHqHosts.ps1` passes.

## Interplay with packages built in parallel

- **V-PEARL1** (takes `ControlImages.cs` over from this package): this package changed only the Copy button line in `LayoutToolBars` and added
  `ToolBarCopyWithoutDrawings_Click` after `ToolBarCopy_Click`. Pearl annotations are drawings: Copy without drawings leaves them out whatever the
  Pearl layer settings.
- **M2-IMG** (also owns `ControlImageDisplay.cs` and `ImageHelper.cs`): this package did not edit `ImageHelper.cs`. If M2-IMG changes what
  `HasImageBeenEdited` counts as a view edit (for example 16-bit windowing values), `ImageCopyRules.HasViewEdits` must follow; the database test
  `HasViewEdits_AgreesWithHasImageBeenEdited_WhenThereAreNoDrawings` will fail until it does.
- **B-SHELL** (later owner of `ControlImages.cs`): nothing to connect.
- `OpenDentBusiness.csproj`: two `Compile` lines after `Imaging\BitmapDicom.cs`.

## Questions for the owner

1. Should Copy without drawings also be added to the old Images module (`ControlImagesOld.cs`), if any workstation still uses it?
2. Is the log text "... copied to clipboard without drawings" what you want in the audit trail, or should both commands log the same text?
3. Please read the Details message once in the window and say whether the wording fits the office.
