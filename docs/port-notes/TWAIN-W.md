# Port note: TWAIN-W, capture display fixes and the in-house DICOM reader (CAP-W1, CAP-W2, CAP-DR)

Branch `pre/twain-w-1`, cut from `main` d9c177ca6 (2026-11-01). The packages are the last three of the capture design's work breakdown: [imaging-capture-design.md](../imaging-capture-design.md) sections 7.8, 7.9, 12.1 and 15 (decisions 6 and 7). The owner's defaults are the design's recommendations, **6a** (pull the 16-bit windowing into the capture track, with legacy stored values read compatibly) and **7a** (an in-house reader for the uncompressed subset behind `IDicomPixelReader`), applied on 2026-11-01. No schema, preference, CRUD, namespace, assembly or project name changed.

Sources: the public DICOM standard (NEMA PS3.5 Data Structures and Encoding: 6.2, 7.1, 7.5, 8.1.1, Annex A; PS3.10 Media Storage and File Format: 7.1; PS3.3 C.7.6.3 Image Pixel module), the capture design and [M2-IMG.md](M2-IMG.md). No Open Dental binary was opened or inspected, and no documentation text was copied. fo-dicom (MS-PL) stays the unchanged fallback; its behaviour was checked only by running it in the tests.

## What M2-IMG already covered (CAP-W2)

The design's CAP-W2 row, item by item, against `main` with M2-IMG:

| CAP-W2 item (design 7.8) | On `main` from M2-IMG | Added here |
|---|---|---|
| `ApplyWindowing` scales by BitsStored: x16 for 12-bit, x256 for 16-bit | Built: `DicomHelper.GetRawPerWindowingStep` (1 for 8-bit, 16 for 12-bit, 256 for BitsStored 16). The fact from the imaging session, that `BitmapDicom.cs` divided every 16-bit image by 16, no longer holds on `main`. | Nothing. |
| `CalculateWindowingOnImport` returns values on the 0-255 scale for 16-bit | Built: 0/255 for BitsStored 16 (was 0/4095). | Nothing. |
| Legacy stored values above 255 are read as the 12-bit scale and divided by 16 for display; nothing is written back | Partly: `ClampWindowing` counted every value above 255 as 255. That is right for 4095, the only value 24.3's own code paths wrote above 255 (the old 16-bit import and the 12-bit fallback), but it turned any other 12-bit-scale value (256-4094) into 255. | `DicomHelper.GetWindowingOnDisplayScale`: a value above 255 is divided by 16 (4095 reads as 255, 1600 as 100), then clamped; `ApplyWindowing` uses it for both ends. Display only. |
| Tests: 4095, other 12-bit-scale minimums and maximums, and 0/0; a 0/4095 window on BitsStored 16 renders as before | 4095 and 0/0 (M2IMGTests `WindowingTests`). | `LegacyWindowingTests`: other 12-bit-scale windows on 16-bit images render as the old divide-by-16 did, the 12-bit mixed-scale window (min on 0-255, max 4095), 0/0 for 12 and 16 bits, nothing written back. |

The 8-bit routines of `ImageHelper` (`ApplyColorSettings`, `CopyWithBrightContrast`) keep M2-IMG's clamp: they work on ordinary 8-bit images, where the legacy rule has no meaning.

Also fixed in `ApplyWindowing`: a window of one value (min equal to max after reading, for example 4095/4095) divided 0 by 0 for the pixels at that value. OpenDentBusiness compiles with checked arithmetic, so the cast of NaN to a byte threw an `OverflowException` and the image did not load. Such pixels now show white, like the pixels above the window.

## CAP-W1: IDicomPixelReader and the explicit .dcm branches

- `OpenDentBusiness/Imaging/IDicomPixelReader.cs` (new): `IDicomPixelReader` (`ReadFile`, `ReadStream`, null when the reader does not read the file) and `DicomPixelReaderChain`, which asks one reader and then the other. For a stream, the fallback starts at the position the first reader started at; a stream that cannot seek is copied into memory first.
- `DicomHelper` (`BitmapDicom.cs`): `SubsetReader`, `FoDicomReader` (the old fo-dicom code, moved unchanged into a private class) and `PixelReader` (the chain of the two). `GetFromFile`, `GetFromStream` and `GetFromBase64` read through `PixelReader`, so every display path (Imaging module, mounts, printing and export through `ImageHelper.LoadBitmap`, thumbnails, `ImageStore.OpenBitmapDicom` for every storage type) uses the in-house reader for the subset.
- `ImageHelper.GetImageCropped`: an explicit `.dcm` branch before the `HasImageExtension` check. The pixels come from `ImageStore.OpenBitmapDicom` (which reads through `DicomHelper.PixelReader`) and are windowed with the document's stored window (`ApplyWindowing`, with the legacy rule). Then they are cropped, rotated and flipped by `ApplyDocumentSettingsToImage` with CROP, FLIP and ROTATE only: the 8-bit color function is left out, because the window already is the brightness and contrast (applying it twice would saturate the image; tested). `ImageHelper.GetImageCroppedDicom(Document, BitmapDicom)` is public for callers that already hold the pixels.
- `Documents.GetThumbnail`: the "not an image" check lets `.dcm` through (`ImageHelper.IsDicomFileName`), and `GetImageCropped` does the rest. The thumbnail is saved as `Thumbnails\<file name>`, as `ControlImageDisplay.CreateDocumentThumbnail` already saves one for a DICOM document it shows (a PNG under the document's name). On `main`, a `.dcm` document showed "Thumbnail not available" in the image selector, the Chart module and anywhere else `GetThumbnail` is used; it now shows the image.
- Unchanged on purpose (design 7.8): `ImageHelper.HasImageExtension` and `ImageStore.HasImageExtension` do not list `.dcm`, because their 16 callers take it to mean "GDI+ can open this file". The print category still skips DICOM.

## CAP-DR: the in-house reader

`OpenDentBusiness/Imaging/DicomSubsetReader.cs` (new), written from PS3.5 and PS3.10 only. It reads exactly the subset `ImagingCapture`'s `DicomIntraOralWriter` writes, and any other file in the same form:

- a PS3.10 file: 128-byte preamble, `DICM`, File Meta Information (group 0002, Explicit VR Little Endian), with a group length that matches when present;
- Transfer Syntax UID 1.2.840.10008.1.2.1 (Explicit VR Little Endian), so the pixels are native;
- one grayscale frame: Samples per Pixel 1, MONOCHROME1 or MONOCHROME2, Bits Allocated 8 or 16, Bits Stored 8, 12 or 16 (not more than Bits Allocated), High Bit = Bits Stored - 1, Pixel Representation 0, Number of Frames absent or 1, Pixel Data (7FE0,0010) as OB or OW of defined length with at least Rows x Columns samples.

Every other element is skipped by its length. Sequences of defined or undefined length, with items of defined or undefined length, are walked to their delimiters, nested up to 16 deep; image attributes inside items (an icon image, for example) are ignored. The values it returns are the ones the old fo-dicom path returned: the stored samples, with the bits above High Bit cleared when Bits Stored is less than 16 (fo-dicom does the same; tested), and no inversion for MONOCHROME1.

Anything else is declined with a status and a reason, never an exception:

- **NotSubset**: no preamble or prefix, another transfer syntax, encapsulated (compressed) pixel data, a UN element of undefined length, or more than 256 MB;
- **Unsupported**: color, signed values, several frames, other bit depths, a High Bit other than Bits Stored - 1, floating point or no pixel data;
- **Malformed**: a length past the end, an unknown or lower-case VR, non-zero reserved bytes, tags out of order or repeated, an item outside a sequence, a missing delimiter, a missing Image Pixel attribute, Rows or Columns 0, pixel data shorter than Rows x Columns;
- **FileError**: the file or stream could not be read.

`DicomHelper` then hands the file to fo-dicom exactly as before. Bounds: a file is read only up to 256 MB; the first 64 KB are read first, so a file in another transfer syntax costs one small read; every length is checked against the bytes really there before it is used; the pixel array is sized from Rows x Columns only after the pixel data is known to hold that many samples. A 65535 x 65535 header over 8 bytes of pixels is refused from the lengths, not by trying to allocate 8 GB (tested).

Fact (b) from the imaging session holds: the writer's files (Explicit VR Little Endian, PS3.10 File Meta, uncompressed MONOCHROME2, Bits Allocated 16, 2.25 UIDs, intra-oral sequences of defined length) are read by the in-house reader alone. A test reads them in a fresh application domain and checks that fo-dicom (`Dicom.Core`) is never loaded; an implicit VR file in the same test still loads it.

## Other change

`Documents.InsertMissing` (its `.dcm` branch): a file with a category prefix is renamed before the branch runs, but the branch read it at the old name, so `GetFromFile` returned null and `CalculateWindowingOnImport(null)` threw, which stopped the scan of the patient's folder. It now reads the file at its new name, and a `.dcm` no reader can read is still attached, with no windowing (INFERRED 11).

## 16-bit capture stays off

Nothing here turns 16-bit capture on. `CaptureBitDepthDecision` is unchanged and still checks only `[Device.N] Bits=16`, `[Twain] Allow16Bit` (default 0), the gated settings check, the verification record and the target (fact (c)): it never waited for CAP-W, and it does not now. The owner has not chosen the 16-bit filing route (CAP-3 3), and design 7.8's other conditions are still open: the DS's 16-bit delivery verified on the clinic's sensor (W5, no hardware run), a Test capture verification record, claim attachment of a `.dcm` (FormImageSelectClaimAttach still refuses it; B-CLAIM or later), counsel's review (decision 18). Decision 7 is now settled by default (7a), and CAP-W1 and CAP-W2 are present.

## Decisions (INFERRED)

1. Every file the subset reader declines goes to fo-dicom unchanged, including a file in the subset's encoding that it finds malformed. A file fo-dicom read before is still read, by one reader or the other.
2. Signed data (Pixel Representation 1) is declined. fo-dicom's path never showed it either (it returns no image for signed data; tested), so nothing changes for such files. Showing it would need an offset or rescale, which is an owner choice (question 1).
3. Several frames are declined (fo-dicom shows the first frame, as before); Number of Frames 1 is read.
4. A value of odd length outside the pixel data (not conformant, PS3.5 7.1.1) is skipped by its length and the file is read; pixel data longer than needed (a pad byte or more) is read up to Rows x Columns.
5. Top-level tags must ascend and not repeat, so an attribute can never be read twice; elements inside items are only skipped, so their order is not checked.
6. Limits: 256 MB per file, 16 levels of sequences, 64 KB for the first read. Larger or deeper files go to fo-dicom.
7. The legacy windowing rule applies to every DICOM window, 12- and 16-bit, with whole-number division: a window whose ends are multiples of 16 renders within one gray level of the old divide-by-16, others within two (4095 reads as 255, not 255.9).
8. The 12-bit import's fallback still stores 4095 when it finds no edge (unchanged, M2IMGTests pins it); the reading rule shows it as 255.
9. `.dcm` is recognized case-sensitively, the same test as `ImageStore.OpenBitmapDicom` and the Imaging module's display path, so a file shown there as DICOM gets a DICOM thumbnail and a `.DCM` file behaves as before.
10. In cloud storage, `GetImageCropped` downloads a `.dcm` through `ImageStore.OpenBitmapDicom` instead of `CloudStorage.GetThumbnail`. That costs the same: the SFTP thumbnail task downloads the whole file too, and GDI+ could not have read the DICOM bytes it returns. In-database storage now gets DICOM thumbnails too, through `GetFromBase64`.
11. `InsertMissing` attaches an unreadable `.dcm` with no windowing, as it attaches any other file that cannot be shown, instead of stopping with an exception.

## Removed

Nothing was removed. The fo-dicom code of `GetFromFile`, `GetFromStream` and `GetFromDicomFile` moved, unchanged, into `DicomHelper.FoDicomPixelReader`. fo-dicom still ships, because every DICOM file outside the subset needs it (decision 7a keeps it for those until the owner decides otherwise).

## Files

| File | Change |
|---|---|
| `OpenDentBusiness/Imaging/IDicomPixelReader.cs` (new) | `IDicomPixelReader`, `DicomPixelReaderChain` |
| `OpenDentBusiness/Imaging/DicomSubsetReader.cs` (new) | the in-house reader, `DicomSubsetReadResult`, `DicomSubsetReadStatus` |
| `OpenDentBusiness/Imaging/BitmapDicom.cs` | the three readers, `GetFromFile`/`GetFromStream` through the chain, `GetWindowingOnDisplayScale`, the one-value window guard |
| `OpenDentBusiness/Imaging/ImageHelper.cs` | `IsDicomFileName`, the `.dcm` branch of `GetImageCropped`, `GetImageCroppedDicom` |
| `OpenDentBusiness/Data Interface/Documents.cs` | the `.dcm` branches of `GetThumbnail` and `InsertMissing` |
| `OpenDentBusiness/OpenDentBusiness.csproj` | two Compile items |
| `tests/DicomReaderTests/` (new), `OpenDental.sln`, `OpenDental.Local.slnf` | the test project |
| `docs/imaging-capture-owner-questions.md` | the Today lines of CAP design 6 and 7, and the CAP design 25 line |

## Changes outside the owned list

- `tests/ImagingCaptureTests/DicomCompatibilityTests.cs`: 6 lines. The design's one compatibility test reads the writer's files through `DicomHelper`, which now uses the in-house reader for them, so fo-dicom was no longer exercised (nor loaded, which the test's dictionary check needs). It now also reads the same bytes through `DicomHelper.FoDicomReader` and checks those pixels too.
- `docs/port-notes/CAP.md`: its open item "CAP-W1, CAP-W2 and CAP-DR are not started" now points here.

## Not done, and follow-ups for other owners

- **The windowing slider** (`ControlImageDisplay.SetWindowingSlider`, `WindowingSlider.SetBounds`, not owned) still shows a stored value above 255 as 255, while the picture now reads 256-4094 as the 12-bit scale. 4095, the only such value 24.3 wrote, shows the same either way. Moving the slider stores 0-255 values as before (question 2).
- **`ImageStore.ImportDocument`** (not owned) still calls `CalculateWindowingOnImport` on a null result when an imported `.dcm` cannot be read; the import then fails with a NullReferenceException instead of a message, as on `main`.
- **Removing fo-dicom** later also needs `OpenDental/Forms/FormClaimAttachment.cs`, which imports `Dicom.Imaging.Mathematics` (no use of it was found there).
- Claim attachment of `.dcm`, the DICOM print category and a finer windowing slider stay with their owners, as design 7.8 and 12.1 say.
- Nothing has been checked with a real 16-bit sensor image or opened on screen in OpenDental.exe (the GUI was not launched); the thumbnail and crop paths ran in tests with a temporary A to Z folder.

## Before CAP-W reaches the clinic: a read-only query

Design 7.8 asks for a count of the `.dcm` documents and their stored windows before CAP-W ships. This query only reads; run it on the clinic's database with a read-only account (it was not run here; the fork's work never connects to the clinic server):

```sql
SELECT COUNT(*) AS DicomDocuments,
  SUM(WindowingMax=0) AS NeverWindowed,
  SUM(WindowingMin=0 AND WindowingMax=255) AS FullRange,
  SUM(WindowingMin=0 AND WindowingMax=4095) AS OldImportWindow,
  SUM(WindowingMin BETWEEN 1 AND 255 AND WindowingMax=4095) AS MixedScaleWindow,
  SUM(WindowingMax BETWEEN 1 AND 255 AND NOT (WindowingMin=0 AND WindowingMax=255)) AS SliderWindow,
  SUM((WindowingMin>255 OR WindowingMax>255) AND WindowingMax<>4095) AS OtherTwelveBitScale
FROM document WHERE FileName LIKE '%.dcm';
```

Only two groups look different after CAP-W, and only for one bit depth each, which the file header tells (not the database): SliderWindow rows of BitsStored 16 files (shown as 25.4 intends), and MixedScaleWindow rows of BitsStored 12 files (nearly black before, now shown). OtherTwelveBitScale should be 0; any such row is now read on the 12-bit scale. OldImportWindow rows look as before.

## Questions for the owner

1. (CAP design 7) Files outside the subset keep going to fo-dicom. Should the in-house reader later take more (signed grayscale with an offset, the first frame of multi-frame files), or should some files stop being read so fo-dicom can go?
2. (CAP design 6) Should the windowing slider show a legacy stored value above 255 the way the picture now reads it (divided by 16)? That is a change in `ControlImageDisplay`, for its owner.
3. (CAP design 6) Should the 12-bit import store 255 instead of 4095 when it finds no edge, so new rows are on the 0-255 scale? They look the same either way.
4. (CAP design 6) Who runs the read-only query above on the clinic database before CAP-W ships, and should staff be told about the two groups that change?
5. An unreadable `.dcm` dropped into a patient's folder is now attached without windowing, instead of stopping the folder scan with an error. Confirm.

## Tests

New project `tests/DicomReaderTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`; no database), 42 tests:

- `DicomSubsetReaderTests`: the capture writer's 12- and 16-bit files with odd sizes (exact samples, and the same result as fo-dicom); `GetFromFile`, `GetFromStream`, `GetFromBase64` and `SubsetReader.ReadFile` agree; 8-bit with an odd sample count and a pad byte, 8-bit in OW, 12-bit with bits above High Bit set, 8 bits stored in 16, MONOCHROME1 and MONOCHROME2, extra pixel bytes, an odd-length value, fo-dicom's own Explicit VR files; sequences and items of defined and undefined length with an icon image inside, empty sequences, a private UN element; 200 nested sequences declined without a stack overflow and 16 read; implicit VR, big endian, deflated and JPEG transfer syntaxes declined, and an implicit VR file read by fo-dicom through `DicomHelper`; encapsulated pixel data; no preamble or prefix; signed 12- and 16-bit data declined, with `DicomHelper` giving what fo-dicom gives (no image); 12 unsupported images and 28 malformed files (22 in the data set, 6 in sequences), each declined with a reason, and a UN element of undefined length; every truncation of a writer file declined; 4000 randomly damaged copies with no exception; non-seekable streams, a stream at an offset, a 1.4 MB file through the header check, a 200 KB file of another transfer syntax read only for its first 64 KB, a stream claiming 2 GB declined without a read, missing files, null input and a stream that throws.
- `DicomPixelReaderChainTests`: the chain asks the fallback only when the first reader declines, from the same stream position, and buffers a non-seekable stream; `DicomHelper`'s readers; the application-domain test above.
- `LegacyWindowingTests`: the display-scale table, the old 16-bit import window, other 12-bit-scale windows on 16-bit images against the old divide-by-16, the 12-bit mixed-scale window, 0/0, one-value windows without an exception.
- `DicomThumbnailTests` (in-memory preference cache, local A to Z in a temporary folder): `GetImageCropped` windows once and then rotates, flips and crops a capture writer file; a legacy 12-bit-scale window reads as intended and the document is not changed; `GetThumbnail` makes a real 100 x 100 thumbnail, saves it as `Thumbnails\<file name>` and uses it next time; broken, truncated and missing `.dcm` files give "Thumbnail not available" without an exception; `IsDicomFileName` and the unchanged `HasImageExtension`.

Run (2026-11-01), Targeted: DicomReaderTests 42/42, ImagingTests 43/43, M2IMGTests 25/25, BIMGTests 28/28, ImagingCaptureTests 751/751, ImagingDeviceManagerTests 501/501, NoHqTests 43 passed and 6 skipped of 49 (before the full build; 49 of 49 in Core). Full build through the build lane: 0 errors. `tools/Test-NoHqHosts.ps1`: passed. `run-tests -Mode Core` (80 assemblies, no migration, schema, release-channel or adoption code touched, so no heavy assemblies added): every assembly passed (DONE ok=True), DicomReaderTests 42/42, ImagingCaptureTests 751/751 with the vendor-neutrality scan of the edited `CAP.md`.
