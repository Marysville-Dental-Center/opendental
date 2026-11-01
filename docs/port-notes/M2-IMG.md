# Port note: M2-IMG, Imaging: 16-bit windowing and the PDF JavaScript preference

Branch `port/m2-img`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, M2-IMG](../specs/port-backlog.md#m2-img-imaging-16-bit-windowing-pdf-javascript-preference). No fork migration step was needed, so none of the assigned levels (2027004301 to 2027004399) is used.

Sources: Open Dental's published database documents (the `document.WindowingMax` description in OpenDentalDocumentation25-3.xml and 26-1.xml), the 26.1 release notes cited by the spec, and the feature specs in `docs/specs/`. No Open Dental binary or install was used, and no documentation text was copied. Everything else is marked INFERRED below.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-imaging-16bit-windowing` | built | True 16-bit DICOM radiographs are windowed on their 12 most significant bits (raw value / 256 against the 0-255 windowing values); 12-bit and 8-bit images show exactly as before. |
| `v26_1-imaging-pdf-javascript-pref` | built | PDFs shown in the Imaging module run no embedded JavaScript unless Preferences > Imaging > General > "Allow JavaScript when viewing PDFs" is checked. Verified against the real WebView2 PDF viewer (Runtime 155.0.4283.62) by an automated test. |

## v25_4-imaging-16bit-windowing

- `DicomHelper.GetRawPerWindowingStep` (new, `OpenDentBusiness/Imaging/BitmapDicom.cs`): how many raw values one windowing step covers. It returns 1 for 8-bit data, 16 for 12-bit data (as in 24.3), and 256 when **BitsStored** is 16. BitsStored decides, not BitsAllocated, because 12-bit images are stored in 16-bit words.
- `DicomHelper.ApplyWindowing` divides raw values by that factor instead of a fixed 16. This is the only change to the pixel loop. Every caller gets the fix: the Imaging module (`ControlImageDisplay`), mounts (`MountHelper`), printing and export (`ImageHelper.LoadBitmap`).
- `DicomHelper.CalculateWindowingOnImport` now stores 0-255 for true 16-bit images. Before, it stored 0-4095, which only displayed as the full range because of the old divide-by-16.
- Stored windowing values keep their 0-255 meaning, as the spec requires. A 16-bit image the user already adjusted on the slider now shows that window as intended, without the saturation.
- `ImageHelper`: the two "todo: if 16 bit" notes are replaced by a note that 12- and 16-bit data is windowed in `DicomHelper` before it reaches these 8-bit routines. Their byte casts now go through `DicomHelper.ClampWindowing`.

## v26_1-imaging-pdf-javascript-pref

- The preference (`PrefName.ImagingAllowPdfJavascript`, bool, default `0`), its PrefInfos.xml hover entry and its seed row come from M2-02. Nothing was added to the registries.
- `ImageHelper.IsPdfJavascriptAllowed()` (new) reads it with `PrefC.GetBoolSilent(..., false)`. A missing or unreadable preference means scripts stay off.
- `ODWebView2.SetScriptEnabled(bool)` (new, `CodeBase/Controls/ODWebView2.cs`) sets `CoreWebView2Settings.IsScriptEnabled`, which takes effect on the next navigation. If it is called before `Init`, the value is kept and applied in `CoreWebView2_InitializationCompleted`. Other `ODWebView2` users (eRx, payment pages, FormWebView, ...) never call it, so they keep WebView2's default (scripts on).
- Hooks, one line each, right before every PDF navigation, so the preference is read for each PDF and a change takes effect on the next PDF:
  - `ControlImageDisplay.LoadPdf` and `ControlImageDisplay.PrintPDF`
  - `ControlImagesOld.LoadPdf` (the pre-2020 Imaging module, still selectable in the same Preferences page)
- Preferences > Imaging > General (`UserControlImagingGeneral`): a new checkbox `checkImagingAllowPdfJavascript`, "Allow JavaScript when viewing PDFs", as the last row of the Functionality group. The group grew by one row and the Default Measurement Scale group moved down 31 pixels. The checkbox is filled and saved like the other checkboxes on that page. The change is logged like other preference changes, and the hover help is M2-02's PrefInfos entry.
- PDFs opened in an external viewer (`PdfLaunchWindow`) are not affected (spec rule 4).
- Spec rule 2 (does the setting reach Edge's PDF viewer?): it does. `PdfJavascriptWebView2Tests` opens a made-up PDF in the same `ODWebView2` wrapper inside a small off-screen window. The PDF's open action hides a black form field with JavaScript. The test reads screen captures: with scripts off, 6815 black samples (field shown); with scripts on, 887 (only the page's own bar, so the script ran); off again, 6815; off set before initialization, 6815. With scripts off, the viewer and its toolbar still work. The WebView2 environment-option fallback in rule 2 was therefore not needed. That fallback would also have been risky: every `ODWebView2` shares one user-data folder, and WebView2 refuses a second environment with different options on the same folder.

## Files touched

| File | Change |
|---|---|
| `OpenDentBusiness/Imaging/BitmapDicom.cs` | `GetRawPerWindowingStep`, `ClampWindowing`, the windowing loop, the 16-bit import value |
| `OpenDentBusiness/Imaging/ImageHelper.cs` | `IsPdfJavascriptAllowed`; clamped byte casts and notes in `ApplyColorSettings` and `CopyWithBrightContrast` |
| `CodeBase/Controls/ODWebView2.cs` | `SetScriptEnabled`, applied on initialization too |
| `OpenDental/User Controls/ControlImageDisplay.cs` | two hook lines (LoadPdf, PrintPDF) |
| `OpenDental/User Controls/Preferences/UserControlImagingGeneral.cs` and `.Designer.cs` | the checkbox, fill and save |
| `OpenDental/Main Modules/ControlImagesOld.cs` | one hook line (outside the owned list, see below) |
| `tests/M2IMGTests/` (new), `OpenDental.sln`, `OpenDental.Local.slnf` | the test project |

## INFERRED behaviours

1. Stored windowing values above 255 count as 255. This covers 16-bit images imported before this change (stored 0-4095) and the 12-bit import's fallback of 4095. The windowing slider already clamps to 255, so the picture now matches the slider. For 16-bit images the picture is the same as before, within one gray level of rounding (tested). A 12-bit image with a stored 4095 used to show far too dark; it now shows the full range.
2. A DICOM document whose WindowingMax is 0 ("no windowing", the column's documented default) shows the full range, as the slider does. In 24.3 every non-black pixel showed as white.
3. True 16-bit images still get no histogram auto-windowing on import (as in 24.3). They get the full range, now stored as 0-255. Open Dental's own 25.4 import logic is not public.
4. Only BitsStored = 16 gets the new factor. Images with 8 bits stored in 16-bit words keep the 24.3 factor of 16, so the windowing stored for them still shows the same.
5. The pre-2020 Imaging module and printing a PDF from the Imaging module follow the preference too. The spec names only `ControlImageDisplay`'s viewer, but both are part of "PDFs shown in the Imaging module".
6. The checkbox position: last row of the Functionality group.

## Deviations from the spec

- `ControlImagesOld.cs` is not in the package's owned list. It got one hook line (see INFERRED 5).
- `ImageHelper`'s 8-bit routines now clamp out-of-range windowing values. A value of 300 used to wrap to 44 in the byte cast. This was not asked for, but it sits in the touchpoint the spec names, and no correct stored value changes.

## Changes outside the owned list (also in the report)

- `OpenDental/Main Modules/ControlImagesOld.cs`: one line in `LoadPdf`.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.
- No registry, enum, TableTypes, Crud, Pref.cs, PrefInfos.xml or csproj change (the new code lives in files the project already compiles).

## Interplay with packages built at the same time

- **M2-02**: owns the preference, its PrefInfos.xml entry (`checkImagingAllowPdfJavascript`, category ImagingGeneral) and the Seed26_1 default `0`. This package uses all three unchanged.
- **Packages that use `ODWebView2` for web pages** (V-GP, V-PAYSUITE payment pages; existing eRx and payment forms): no change in behaviour. `SetScriptEnabled` is opt-in, and its private state starts as "not set". A package that also edits `ODWebView2.cs` should keep `_isScriptEnabled`, `SetScriptEnabled` and the three lines at the end of `CoreWebView2_InitializationCompleted`.
- **Packages editing `ControlImageDisplay.cs` or `ControlImagesOld.cs`** later (M2-SHELL in C4, a later Pearl package): keep the `SetScriptEnabled(ImageHelper.IsPdfJavascriptAllowed())` line right before each PDF navigation. `PdfJavascriptPrefTests` checks that every navigation in both files has it.
- **ImagingTests (A-IMG)**: its wiring tests read the same two files; they still pass.

## Tests

New project `tests/M2IMGTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `WindowingTests`: the factor table, 16-bit bright values no longer saturate (with the 24.3 result for contrast), a window maps to raw × 256, and 12-bit and 8-bit output is identical to the 24.3 loop over a sweep of values and windows. It also covers legacy 0-4095 on 16-bit (full range, within one gray level of 24.3), WindowingMax 0, clamping, the import values for 8-, 12- (histogram pins) and 16-bit, and the 8-bit routines' clamp.
- `DicomFileTests`: uncompressed 12- and 16-bit DICOM files made with fo-dicom, read back through `DicomHelper.GetFromStream` and `GetFromFile`, imported and windowed.
- `PdfJavascriptPrefTests`: the rule with an in-memory preference cache ("0", "1", missing), the seed default, and source checks. The source checks cover: every PDF navigation in `ControlImageDisplay` and `ControlImagesOld` applies the preference first, `ODWebView2` applies it on navigation and on initialization, and the Preferences page fills and saves it. The checkbox name and text must match the PrefInfos entry, and the layout must still fit.
- `PdfJavascriptDbTests`: a scratch MariaDB database (fresh 24.3.63, upgraded by the fork migration runner, unique per-run name, dropped afterwards). The preference arrives as `0`, PDF scripts are off, and saving the checkbox turns them on and off.
- `PdfJavascriptWebView2Tests`: the real WebView2 PDF viewer check described above, plus a check that the made-up PDF's cross-reference table is valid. It is inconclusive when the WebView2 Runtime is missing, the window cannot draw, or `M2IMG_SKIP_WEBVIEW2=1`. It creates and deletes its own temporary data folder, never the shared `WindowsFormsWebView2` folder.

Run with ImagingTests (the existing tests over the same files): all pass.

## Questions for the owner

1. PDFs shown elsewhere (eForms, statements, sheets) are outside this preference, as in the spec. Should the same setting, or a second one, cover them?
2. True 16-bit images still import with the full range rather than an automatic window computed from the image, as in 24.3. Should they get the same automatic windowing as 12-bit images? That needs a real 16-bit sensor sample to tune the thresholds.
3. 12-bit images whose stored windowing is 4095 (the import's fallback when it finds no edge) now show the full range instead of a nearly black picture. Confirm this is wanted. It only changes images that were unreadable before.

## Integration (branch `port/int-7`)

- Merged without conflicts. No other merged package changed `BitmapDicom.cs`, `ImageHelper.cs`, `ODWebView2.cs` or the Imaging General preferences page since this package's base. Every PDF navigation in `ControlImageDisplay` and `ControlImagesOld` (B-IMG's edits included) still applies the preference first; `PdfJavascriptPrefTests` checks it. B-IMG's copy commands use `ImageHelper.HasImageBeenEdited`, which this package did not change.
