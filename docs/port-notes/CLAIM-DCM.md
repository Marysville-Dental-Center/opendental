# CLAIM-DCM: DICOM X-rays on insurance claims

Package CLAIM-DCM. Branch `m16/claim-dcm-1`, cut from `main` (9095f3e80). Tests: `tests/CLAIMDCMTests` (new). Nothing is renamed; the rename to Open PMS Dental comes later.

Before this package, a DICOM document could never go on a claim. Edit Claim's image attachment window (`FormImageSelectClaimAttach`) refused every file that `ImageHelper.HasImageExtension` does not list, and `.dcm` is not listed. The DentalXChange image picker refused it by extension, and the file pickers handed `.dcm` files to GDI+, which cannot open them. So a 16-bit X-ray that ImagingCapture's `DicomIntraOralWriter` stores as DICOM (Explicit VR Little Endian, uncompressed MONOCHROME2, with its window in `Document.WindowingMin`/`WindowingMax`) had no way onto a claim.

Now every place a claim image can be chosen accepts DICOM. It renders an 8-bit copy through the program's own DICOM display path, with the stored window and the crop, rotation and flip the Imaging module applies, and attaches that copy in the format the path already uses: JPEG everywhere. The DICOM file is never sent or stored as the claim attachment, and the stored DICOM file and its document row are never changed. A DICOM file the program cannot read shows a clear message, and nothing is attached.

Sources: this repository only (the capture design, `docs/imaging-capture-design.md` 7.8, and the code). No Open Dental binary was inspected, and no vendor documentation was used. All test data is synthetic. No database and no network are used by the new tests.

## 1. Every way an image reaches a claim

| # | Path | Where | What it attaches | DICOM before | DICOM now |
|---|---|---|---|---|---|
| 1 | Edit Claim, Attachments, **Add** (and the Canadian tab's Add) | `FormClaimEdit.butAttachAdd_Click` / `butCanadianAttachAdd_Click` -> `FormImageSelectClaimAttach` | A `claimattach` row whose file is a copy in the EmailAttachments folder (`EmailAttaches.GetAttachPath()`). JPEG/PNG documents are copied as they are; edited ones are meant to be re-rendered (see question 1). | A DICOM document was refused ("Invalid file. Only images may be attached"). | `SaveAttachmentDicom`: an 8-bit JPEG copy (quality 100, `.jpg`), named in Edit Claim as the document's name with `.jpg`. |
| 1a | ... **Import** in that window | `FormImageSelectClaimAttach.butImport_Click` | Imports the file into the patient's images (Claim Attachments category), then attaches it as in 1. | `.dcm`: "The file does not have a valid format." | The `.dcm` is read first. If it reads, it is imported as the DICOM it is (as the Imaging module imports it), then attached as the JPEG copy. If not, a message, and nothing is imported or attached. |
| 1b | ... **Paste Image** / snip in that window | `GetImageFromClipboard`, `ImportImage` | A clipboard image, or a file on the clipboard, imported as a JPEG document, then attached. | A `.dcm` file on the clipboard: "Invalid file type". | The `.dcm` file is pasted as its 8-bit copy (see "Files that are not documents" below). |
| 2 | Edit Claim's image attachments afterwards: Open, double-click, Rename, Remove, **Export** | `FormClaimEdit.menuItemOpen_Click`, `listAttachments_DoubleClick`, `butExportHelper` | Opens the `claimattach` file in the Windows viewer, or copies it to `ClaimAttachExportPath`. | Not reachable. | Unchanged code. The file is the JPEG copy, so it opens, prints and exports like any JPEG. |
| 3 | The `claimattach` rows and files | `ClaimAttach`, `Claims.Update` (rows), EmailAttachments folder (files), `ClaimAttaches` | `DisplayedFileName`, `ActualFileName`, `ImageReferenceId` | — | No schema change. No path writes a row whose `ActualFileName` is a `.dcm` file. |
| 4 | DentalXChange **Select Image** (right-click menu in Account and Treatment Plan; V-DXC2) | `FormImagePickerDXC` | A document, a mount, or a file from disk (Send File), sent with `ClaimConnect.SendAttachmentImages` as a JPEG (`ImageAttachment.Create`), or a PDF as it is (V-DXC2). A `claimattach` row; with Save DXC Attachments on, mounts and files are saved to the patient's images. | A DICOM document was refused by its extension; a `.dcm` from disk failed in GDI+. A mount with DICOM images already worked (`MountHelper` windows DICOM), but an unreadable DICOM item threw or left a blank slot. | DICOM documents and `.dcm` files are sent as the 8-bit copy (JPEG). A DICOM document is treated like a mount afterwards: the row never names the `.dcm`; with Save DXC Attachments on, the JPEG that was sent is saved to the patient's images and the row names it. A mount with an unreadable DICOM image is refused with a message that lists the files. Send File offers `*.dcm`. The preview shows the copy. |
| 5 | DentalXChange **Paste** (right-click menu) | `FormClaimAttachPasteDXC` | Clipboard images or files, sent as JPEGs; saved to the patient's images when Save DXC Attachments is on. | A `.dcm` on the clipboard: "invalid file types". | Pasted as its 8-bit copy. Still image-only (no PDFs, V-DXC2). |
| 6 | DentalXChange **Snip** (right-click menu) | `FormClaimAttachSnipDXC` | A screen snip. | — | Unchanged: a screen capture is already 8-bit. |
| 7 | Claim attachment window, **EDS** (Edit Claim, EDS tab, Add Attachment; V-EDS) and its dormant DentalXChange branch | `FormClaimAttachment`, `FormClaimAttachmentItemEdit`, `EDS.SaveAttachments` | Images from disk, the clipboard or a snip, sent as JPEGs (`EDS.ImageAttachment.Create`; PDFs refused for EDS, V-DXC2); saved to the patient's images when Save EDS Attachments is on. | A `.dcm` from disk: "The file does not have a valid image format." | Add Image and Paste take a `.dcm` as its 8-bit copy, sent and saved as a JPEG. |
| 8 | DentalXChange PDF attachments (V-DXC2) | `DxcPdfAttachment` | A PDF's own bytes. | — | Unchanged. A DICOM image never becomes a PDF; it goes as a JPEG, like every image DentalXChange gets. |
| 9 | The sends themselves | `ClaimConnect.SendAttachmentImages`, `ClaimConnect.AddAttachmentImage` (partner service and XConnect), `EDS.SaveAttachments` | Base64 image bytes. | — | New guard, `ClaimAttachDicom.ThrowIfAnyIsDicom`, before the first call: an attachment whose bytes are a DICOM file (`DICM` after the 128-byte preamble) is refused. The windows never build one; this only catches a DICOM file that reached the list some other way. |
| 10 | Printing claim attachments | `FormClaimPrint` (paper claims) | The claim form prints counts and flags only (`RadiographsNumAttached`, `AttachedImagesNum`, `IsEnclosuresAttached`, the attachment ID). No attachment image is printed by the claim. | — | Unchanged. An attachment image is printed by opening it (path 2) or from the Imaging module, whose DICOM display path this package reuses. |
| 11 | X12 837 attachment references | `X837_5010` / `X837_4010` | PWK (2300) carries only the claim's `AttachmentID` (from DentalXChange or EDS) and the paperwork flags; the old Tesia file export is commented out. NTE no longer carries the ID. | — | Unchanged: no X12 segment references an attachment file. |
| 12 | CDAnet (Canada) attachments | `CanadianOutput.SendAttachments` / `GetInfoForAttachments` | Reads the `claimattach` files of path 1; images are converted to JPG; TXT, DOC and PDF go as they are. | — | Unchanged. The file of a DICOM attachment is now the JPEG copy. A `.dcm` file placed there by hand still fails as "not a supported file type", so it is not sent either. |

## 2. What was built

### Business layer: `OpenDentBusiness/Imaging/ClaimAttachDicom.cs` (new)

No database writes, no network.

- `ReadDocument(document, patFolder)`: the raw pixels through the Imaging module's own path, `ImageStore.OpenBitmapDicom` -> `DicomHelper.GetFromFile`/`GetFromStream`/`GetFromBase64` -> `DicomHelper.PixelReader` (DicomSubsetReader first, then fo-dicom). It works for the A to Z folder, cloud storage and images stored in the database. `ReadFile(path)` does the same for a file on disk. Anything unreadable becomes an `ODException` with one clear message (`GetUnreadableMessage`): missing, empty, not DICOM, truncated, a format neither reader decodes, or fewer pixels than the size.
- `Render(document, bitmapDicom)`: the Edit Claim copy. It calls `ImageHelper.GetImageCroppedDicom`, the same function the DICOM thumbnails and `GetImageCropped` use: `DicomHelper.ApplyWindowing` with the stored window (legacy 12-bit-scale values included; 0/0 means the full range), then crop, rotation and flip. It renders on `document.Copy()`, so nothing about the document changes.
- `Render(document, bitmapDicom, listImageDraws)`: the DentalXChange picker's copy. It is the same, plus the document's drawings, because that picker sends every other image with its drawings (`ImageHelper.GetBitmapOfDocumentFromDb`). With no drawings, the two renders are pixel-identical (tested).
- `RenderFile(bitmapDicom)`: for a `.dcm` that is not one of the patient's documents. It uses the window `ImageStore.Import` would store for that file (`DicomHelper.CalculateWindowingOnImport`, on a copy), and no crop, rotation or flip.
- `FindDocumentForFile`: a `.dcm` chosen from disk or the clipboard that is a document's file in the patient's folder uses that document's settings.
- `BuildClaimAttachFile`: the Edit Claim file. It is the JPEG at quality 100, 24-bit; whatever a rotation or crop leaves uncovered is black. The name shown is `<document name>.jpg`.
- `IsDicomFileName`: `.dcm` in any case, as `ImageStore.Import` decides. A document named `.DCM` (which `OpenBitmapDicom` does not open) is read from its file bytes with the same reader.
- `IsDicomBytes`, `ThrowIfAnyIsDicom` (DentalXChange and EDS lists): the send guard of path 9.

### Small changes to shared code

- `ImageHelper.ConvertCropIfNeeded(document, bitmap, doSave)` and `ImageHelper.GetImageCroppedDicom(document, bitmapDicom, doSaveCropConversion)`: new overloads. The old signatures call them with `true`, so they behave exactly as before. With `false`, a pre-21.4 crop is converted in the document passed in, and nothing is saved. Claims use this on a copy, so attaching never writes the document row.
- `MountHelper.GetBitmapOfMountFromDb(mountNum, listDocumentsDicomUnreadable)`: new overload. The old one is unchanged. With a list, a DICOM item that cannot be read is listed instead of throwing or leaving a silent blank, so the picker can refuse the mount with a message.

### Windows (`OpenDental`)

- `Logic/ClaimAttachDicomL.cs` (new): `GetBitmapForDocument`, `GetBitmapForFile` and `CanReadFile`, each with one message for an unreadable file; `GetMountUnreadableMessage`.
- `FormImageSelectClaimAttach`: `SaveAttachmentDicom`, plus the Import and Paste handling (paths 1, 1a, 1b). On cloud storage, the read and the upload run under the usual progress window.
- `FormImagePickerDXC` (path 4), `FormClaimAttachment` (path 7), `FormClaimAttachPasteDXC` (path 5).
- `HasImageExtension` is unchanged on purpose (design 7.8): its callers take it to mean "GDI+ can open this file".

## 3. INFERRED decisions

1. **JPEG for Edit Claim's copy, at quality 100.** Edit Claim attachments are JPEGs in practice (the perio chart is saved as `.jpg`; CDAnet converts images to JPG), and quality 100 is what `ImageStore.Import` uses for radiographs and claim attachments. PNG would be lossless; see question 2.
2. **Edit Claim shows `<name>.jpg`**, not the `.dcm` name, so the list says what is attached.
3. **Drawings follow each path.** Edit Claim leaves them out (it leaves them out for every image, through `ApplyDocumentSettingsToImage`). The DentalXChange picker includes them (it includes them for every image, through `GetBitmapOfDocumentFromDb`). EDS, Paste and Send File take files, which have no drawings.
4. **A `.dcm` that is not a document** (from disk or the clipboard, outside the patient's folder, or with no document of that name) uses the window the import would store. A `.dcm` in the patient's folder that is a document uses that document's stored window, crop, rotation and flip.
5. **DentalXChange picker, DICOM document:** the row's `ActualFileName` is the saved JPEG copy when Save DXC Attachments is on, and empty when it is off. That is what mounts get. Before, a document's row named the document's own file; for DICOM that would be the `.dcm`, so it is not done.
6. **Edit Claim's Import of a `.dcm`** stores the DICOM in the patient's images (that is what Import does with every file), and the claim gets the JPEG copy. Edit Claim's Paste of a `.dcm` file imports the 8-bit copy, as it imports every pasted file as a JPEG.
7. **A mount with an unreadable DICOM image is not sent.** Before, it went with an empty slot, or the window threw. A missing JPEG item behaves as before (an empty slot).
8. **A pre-21.4 crop** is converted on the copy only. The Imaging module would save that conversion the next time it shows the image; attaching does not.
9. **The send guard** refuses PS3.10 files (`DICM` at offset 128). A raw DICOM data set without the preamble is not recognized; no path in the program produces one.

## 4. Questions for the owner

1. **Existing bug, not changed:** in `FormImageSelectClaimAttach.SaveAttachment`, a non-DICOM image with crop, rotation, flip or windowing set, on a local A to Z folder, gets a `claimattach` row but no file. The branch `if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ)` renders only for cloud storage, where `Bitmap.FromFile` on a cloud path would fail, and the local branch below it sets the row without copying. It looks inverted. DICOM does not go through it. Fix it in a follow-up?
2. JPEG (quality 100) or PNG for Edit Claim's copy of an X-ray? PNG is lossless and larger. CDAnet converts it to JPG anyway.
3. Should the DentalXChange picker default the image type to X-Rays for a DICOM radiograph? Today the user always picks the type.
4. CDAnet allows `DIC` (DICOM) attachments. The rule for this package is that a DICOM file is never sent, so CDAnet gets the JPEG copy. Confirm.
5. A document whose name ends in `.DCM` (upper case) is not shown as DICOM by the Imaging module (`ImageStore.OpenBitmapDicom` and the display path test `EndsWith(".dcm")`), although `ImageStore.Import` imports it as DICOM. Claims accept it. Should the display path accept any case too?
6. Should Edit Claim's DICOM copy include the drawings? Today no Edit Claim copy does.

## 5. Tests

New project `tests/CLAIMDCMTests` (added to `OpenDental.sln` and `OpenDental.Local.slnf`). The preference cache is filled in memory and a temporary folder stands in for the patient's A to Z folder; there is no database and no network. The X-rays come from `DicomIntraOralWriter`: a 64 x 32 image whose halves are raw 25600/38400 (16-bit, 100/150 on the 0-255 windowing scale) or 1600/2400 (12-bit, the same 100/150), so the stored window 50-200 gives exactly 85 and 170.

- `ClaimRenderTests`:
  - The capture writer's files are read by the subset reader, and the claim path gets the same pixels.
  - 16-bit and 12-bit renders give 64 x 32 and exactly 85/170.
  - A window of 0/0 gives the full range; a legacy 12-bit-scale window (800-3200) shows as 50-200 and is not written back.
  - Rotation, flip and crop are kept; a pre-21.4 crop converts on a copy with no database.
  - The drawings render equals the Edit Claim render.
  - Edit Claim's file for 16-bit and 12-bit is a JPEG of 64 x 32 with 85/170 (within 2) and the `.jpg` name.
  - The DICOM file's hash and the document's settings are unchanged, and nothing is written to the patient's folder.
  - `.DCM` documents are read.
  - A file from disk uses the import window.
  - `FindDocumentForFile` and the file-name rules.
- `ClearinghouseAttachmentTests`:
  - DentalXChange (`ClaimConnect.ImageAttachment.Create`, 16-bit and 12-bit, and a pasted `.dcm`) and EDS (`EDS.ImageAttachment.Create`, `.jpg`, one page) get an 8-bit JPEG of the right size and values.
  - The guard refuses DICOM bytes for both clearinghouses.
- `MalformedDicomTests`: bytes that are not DICOM, a capture file cut to 500 bytes, a preamble with junk after it, a `.DCM` with junk, an empty file and a missing file. Each is refused by every path's entry point (`BuildClaimAttachFile`, `ReadDocument`, `ReadFile`) with the clear message naming the file, and the file is unchanged.
- `WiringTests` (source checks, since the windows are not opened in tests):
  - Each window routes DICOM through the new code before its old refusal.
  - Edit Claim never copies the `.dcm` and never updates the document.
  - The picker never names a DICOM document as the row's file.
  - The sends check before their first call.

### Results (2026-11-01, on `m16/claim-dcm-1`)

- Full build through the shared build lane (no `-Projects`): succeeded, 0 errors.
- Targeted run (`run-tests.ps1 -Mode Targeted`), all passed, none skipped:

  | Assembly | Passed |
  |---|---|
  | CLAIMDCMTests (new) | 26/26 |
  | ImagingTests | 43/43 |
  | M2IMGTests | 25/25 |
  | DicomReaderTests | 42/42 |
  | VDXC2Tests | 31/31 |
  | VEDSTests | 83/83 |
  | BCLAIMTests | 55/55 |
  | ImagingCaptureTests | 751/751 |
  | NoHqTests | 53/53 |

- `tools/Test-NoHqHosts.ps1`: passed.
- Not verified: the windows were not opened (the OpenDental GUI is not launched in this work). No send reached DentalXChange or EDS (no test accounts). Cloud storage and images stored in the database are covered by the shared read path, not by a test of their own. No real sensor's DICOM was used. The full suite runs once everything is merged.

## 6. Question 1 fixed (owner, 2026-11-07)

The owner answered question 1: fix it. `FormImageSelectClaimAttach.SaveAttachment` no longer branches on the storage type to decide whether
an edit is applied. For every file that is not DICOM it now reads the document's file as bytes (the patient's folder, or a download from cloud
storage), builds the bytes to attach with the new `OpenDentBusiness/Imaging/ClaimAttachImage.cs` (`BuildFileBytes`), and writes them under the
new name where the attachment points: `File.WriteAllBytes` in the local EmailAttachments folder, or `CloudStorage.Upload`. The attachment row
is made only after the file was written.

- **Unedited files** (no crop, rotation, flip or window, the same test the window always made: `ClaimAttachImage.HasImageSettings`), and
  files that are not images (TXT, DOC, PDF), are attached byte for byte, as `File.Copy` did. With cloud storage they now work too: the old
  code stopped at `File.Exists` on the cloud path ("File not found").
- **Edited images** are attached as the Imaging module shows them, without drawings (as before): `ImageHelper.ApplyDocumentSettingsToImage`
  on a copy of the document, a crop saved before 21.4 converted in memory only, so the document never changes. They are written in the format
  of their extension (JPEG at quality 100, as the DICOM copy; PNG, BMP, GIF, TIFF as they are), so the extension says what the file holds; an
  image whose format GDI+ cannot write (for example `.tga`) is attached as `.png` (`GetAttachExtension`). Before, the cloud branch wrote
  GDI+'s default PNG under the original extension.
- Tests: `tests/CLAIMDCMTests/ClaimAttachImageTests.cs` (each setting counts; unedited and non-image files are the file itself; rotated,
  flipped, cropped and windowed images as shown, in their own format; the document is never changed; the `.png` fallback; source checks that
  the one attachment row is made only after the local write or the upload; a local-folder run of the same steps). `WiringTests` now expects
  the DICOM branch before `ClaimAttachImage.BuildFileBytes` instead of before `File.Copy`.
