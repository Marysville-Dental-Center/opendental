# V-DXC2: DentalXChange PDF attachments

Package V-DXC2 of [the port backlog](../specs/port-backlog.md) (vendor-direct, M2, wave C1). Branch `port/v-dxc2`, cut from `m1/integration-a2-patch`. Tests: `tests/VDXC2Tests`.

Sources used: the feature spec `v26_1-account-dxc-attach-pdf` in [features-26.1.md](../specs/features-26.1.md), which summarizes the public 26.2 release notes (the change was backported to 26.1.64) and the public DentalXChange overview page. A search of DentalXChange's public pages found no published file types, size limits or encryption rules for attachments. No vendor documentation behind a login, no Open Dental binaries, no office data.

## Status

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-account-dxc-attach-pdf` | built | PDFs from the patient's images or from disk go to DentalXChange as they are, through XConnect or the partner service. Encrypted, empty, oversized and non-PDF files are refused first. `.pdf` is matched in any case. EDS, Snip and Paste are unchanged. Not run against DentalXChange (no test account). |

Nothing in this package needs vendor documentation the code doesn't already have: the PDF goes through the same attachment calls the existing image attachments use. What is not public is DentalXChange's own acceptance rules (size limit, how it recognizes a PDF), so those are INFERRED below and should be confirmed with a DentalXChange test account.

## What was built

### Business layer: `DxcPdfAttachment` (new)

`OpenDentBusiness/VendorDirect/DentalXChange/DxcPdfAttachment.cs`, compiled through the existing `VendorDirect\**\*.cs` wildcard. It makes no network call.

- `IsPdfFileName`: `.pdf` in any case (rule 5). 24.3 used `EndsWith(".pdf")`, so `.PDF` fell through to `Image.FromFile` and failed with a format error.
- `GetProblem` checks, in this order: empty, over `MaxPdfBytes` (10 MB), no `%PDF-` header in the first 1024 bytes, encrypted. Each problem has a message ready to show.
- `IsEncrypted` (rule 2): true when the trailer, or a cross-reference stream dictionary (which is never compressed), has an `/Encrypt` entry, written either as an indirect reference (`/Encrypt 12 0 R`) or as a dictionary (`/Encrypt << ... >>`). Every password-protected PDF has one. Longer names such as `/EncryptMetadata`, and the word in page text, don't count. The check is our own byte scan: the repo's PdfSharp 1.2 can't read many current PDFs.
- `ReadPdfFile` (from disk) checks the size before reading. `ReadPdfDocument` (from the patient's images) reads through `ImageStore.GetDocumentFileBytes`, so the A to Z folder, cloud storage and images stored in the database all work.
- `CreateImageAttachment` builds a `ClaimConnect.ImageAttachment` with `IsPdf` set and the PDF's own bytes. It is never re-encoded as a JPEG the way images are.
- `ThrowIfAnyCannotBeSent` runs first in `ClaimConnect.SendAttachmentImages`, so a bad PDF never reaches DentalXChange. If it does refuse, no call is made and the claim's attachment flags are left as they were.
- `IsPdfAccepted` is true for DentalXChange (ClaimConnect) only. `GetPdfRefusedMessage` gives EDS a clear message (rule 3).
- `CreatePreviewBitmap` draws a PDF icon: a page with a folded corner, a "PDF" band, the file name and, when visible, the page count.
- `CountPages` is a best-effort count of uncompressed `/Type /Page` objects, for display only.

### Sending (rule 1): `OpenDentBusiness/Eclaims/ClaimConnect.cs`

- `ImageAttachment.IsPdf` (new field) and `ImageAttachment.GetFileNameToSend()`. A PDF's display name gets `.pdf` added unless it already ends that way. Images keep their name exactly as before.
- Partner service (`ConvertToAttachmentImage`) and XConnect (`AttachmentAddImage`) both send `GetFileNameToSend()`. The bytes go in the same field as images: base64Binary in the SOAP call, a base64 string in the XConnect JSON.
- Everything else is unchanged: open, add, narrative and submit; the `Misc` and `Submitted` flags from V-DXC1; the partner identity gate; XConnect's own API key.

### Windows

- **Select Image** (`FormImagePickerDXC`, opened from the right-click menus in Account and Treatment Plan). This is the live DentalXChange path, because Open Dental removed the attachment button from Edit Claim.
  - A PDF document in the patient's images now previews as the PDF icon and is sent as the PDF it is. Before, it was refused with "PDF attachments are not supported."
  - New **Send File...** button (bottom right, next to Send). It sends a PDF or an image from disk with the image type, date and narrative entered in the window, and the file name box takes the file's name.
  - The image type must be chosen first, so the user doesn't pick the file twice.
  - A PDF from disk is saved to the patient's images as a `.pdf` document when Save DXC Attachments is on, like mounts. An image from disk is saved as a JPEG, as mounts are.
  - The window stays open afterwards, and the tree is refreshed.
  - The window title ("Select Image") is unchanged.
- **Claim attachment window** (`FormClaimAttachment`).
  - Its file picker takes `.pdf` in any case. For DentalXChange it opens the item editor with the PDF; EDS gets "EDS does not accept PDF attachments."
  - PDF rows show their `.pdf` name, preview as the PDF icon, stay PDFs when edited, and are saved to the patient's images as `.pdf` documents.
  - Its DentalXChange branch is reachable only through code (Open Dental hid it in 24.3), but it follows the spec. The EDS branch is live.
- **Attachment item editor** (`FormClaimAttachmentItemEdit`): `SetPdf` makes the attachment a PDF. Document type, date and file name are entered as before. The X-ray mirror box is hidden for PDFs, since orientation only applies to X-ray images.
- **Snip and Paste** (`FormClaimAttachSnipDXC`, `FormClaimAttachPasteDXC`) are not touched and stay image-only (rule 4).

### Credentials

None are added. Sending uses the clearinghouse login, password and XConnect API key the office already set up, plus the partner identity from V-DXC1.

## Files

- New: `OpenDentBusiness/VendorDirect/DentalXChange/DxcPdfAttachment.cs`.
- New: `tests/VDXC2Tests/` (project, `SyntheticPdf.cs`, `FakeDentalXChange.cs`, `DxcPdfAttachmentTests.cs`, `PdfSendDbTests.cs`, `WiringTests.cs`).
- New: this note.
- Edited (owned by V-DXC2):
  - `OpenDentBusiness/Eclaims/ClaimConnect.cs`: the `IsPdf` field and `GetFileNameToSend`, two file-name lines, and the check at the top of `SendAttachmentImages`.
  - `OpenDental/Forms/FormImagePickerDXC.cs` and `.designer.cs`.
  - `OpenDental/Forms/FormClaimAttachment.cs`.
  - `OpenDental/Forms/FormClaimAttachmentItemEdit.cs`.
- Edited (outside owned):
  - `OpenDental.sln` and `OpenDental.Local.slnf`: the test project.
  - `docs/specs/vendor-direct/dentalxchange.md`: a short "PDF attachments" section.
- No schema change, no fork migration step, no preference, permission or enum change. No `.csproj` edit outside the new test project: the new business file is picked up by the wildcard.

## INFERRED behaviours (settle with a DentalXChange test account)

1. **Size limit: 10 MB per PDF** (`DxcPdfAttachment.MaxPdfBytes`). DentalXChange publishes none. Images keep having no limit of ours, as before.
2. **How DentalXChange recognizes a PDF.** Neither attachment call has a content-type field, either in the partner service WSDL in the repo or in the XConnect request model. So the type travels as the PDF bytes, which start with `%PDF-`, and a file name ending in `.pdf`. The spec's "PDF content type" is met that way. No field was invented.
3. **Every encrypted PDF is refused**, including one with only an owner password (it opens without a password but restricts printing or copying). The spec says "encrypted or password-protected", and DentalXChange or the payer may not be able to process either kind.
4. **Orientation** is sent for PDFs exactly as for images: only when the document type is X-Rays, and then "right".
5. **Send File** in Select Image: the button's place, the order (image type first, then the file), the file name taken from the file, and the window staying open.
6. **Saving locally**: a PDF from disk is saved as an `ImageType.Document` `.pdf` file in the Claim Attachment image category. A PDF picked from the patient's images is not copied again; its own file name is recorded.

## Deviations from the spec

- **No page-1 thumbnail.** The repo has no PDF renderer: PdfSharp 1.2 can't render pages, and adding PDFium would be a new binary. PDFs show the PDF icon instead, which the spec allows ("a page-1 thumbnail or a PDF icon").
- **No thumbnail column.** The claim attachment window's grid has no image column. PDF rows show their `.pdf` file name; the icon shows in the item editor and in Select Image.
- The page count is shown only when the page objects aren't compressed; otherwise the icon has no count.

## Interplay with packages built in parallel

- **V-EDS** (C2) takes over `FormClaimAttachment` from this package. EDS PDFs stay refused through `DxcPdfAttachment.IsPdfAccepted`. If EDS documents PDF support, V-EDS can let EDS through there and add an EDS PDF path. EDS's own `ImageAttachment.Create` still takes an `Image`.
- **V-DXC1** (merged): its attachment flags (`Misc`, `Submitted`), the partner identity gate, the XConnect switch and `ClaimL.ConfirmAddToSubmittedDxcAttachment` are used unchanged.
- No other C1 package owns these files. `ClaimConnect.cs` edits are local: a field, a method and three one-line changes.

## Tests

- **`tests/VDXC2Tests`**, 31 tests.
  - File names in any case, `.pdf` added once, which clearinghouses take PDFs.
  - Header (leading bytes up to 1024 allowed), empty, size limit at and over 10 MB, encrypted variants, look-alikes (page text, `/EncryptMetadata`, a malformed reference), page count.
  - Reading from disk (the size is checked before reading), from the database and from an A to Z folder.
  - The attachment keeps the PDF's bytes; the partner service object and its XML carry the base64 PDF and the `.pdf` name.
  - The PDF icon.
- **End to end against a scratch database**: a fresh 24.3 database upgraded by the fork migration runner, with a synthetic claim, patient, carrier and clearinghouse.
  - **XConnect**: `ClaimConnect.SendAttachmentImages` makes create, add-image and submit calls through a recording `APIRequest.Inst`. The JSON holds the PDF's own base64 bytes, `Perio chart.pdf` and `P6`, sent to `api.dentalxchange.com` with the office's key. A PDF next to an image: the image is still a JPEG with its old name. An encrypted PDF is refused before any call.
  - **Partner service (SOAP)**: the SOAP client's HTTP request is answered in memory through a `WebRequest` prefix registered for the DentalXChange hosts. It refuses when a test hasn't turned it on. openAttachment, addImage and submitAttachment run; the `attachmentImage` element carries the PDF's base64, `Perio chart.pdf` and `P6`, and no orientation.
  - Saving a PDF attachment to the patient's images stores a `.pdf` document.
- **Source checks**: no case-sensitive `.pdf` test is left, the windows call `DxcPdfAttachment`, Snip and Paste don't, and PDFs are checked before the first call.
- **Related existing assembly**: `VendorDirectTests` (V-DXC1's DentalXChange tests and the rest of the vendor-direct code).

## Fix round r1 (`port/v-dxc2-r1`)

The package's own tests passed. Two tests outside the package failed in the wave's runs: `ReleaseFlowTests.AdoptionDbTests.Markers_AreWrittenAsPlainPreferenceRows` ("Unknown database 'releaseflow_adoption'") and `AdoptedCopy_WithSomeMarkersRemoved_IsRefused_AndNeverAdoptedAgain` ("Table 'releaseflow_adoption.preference' doesn't exist").

- **Cause.** No V-DXC2 code is involved: this package touches no adoption, fork version or preference code. `ReleaseFlowTests` used fixed scratch database names (`releaseflow_adoption`, `releaseflow_release`). In the single wave, many worktrees run tests against the one local MariaDB at the same time. Each `ScratchDb.Create` drops and recreates the database, so another run's test removed it, or emptied it, in the middle of ours. That gives exactly those two errors.
- **Fix (test code only, outside owned).** `tests/ReleaseFlowTests/TestSupport.cs` now builds per-run names with `ScratchDb.ScratchName(purpose)`. The pattern is `releaseflow_<UTC yyMMddHHmmss>_<process id>_<random>_<purpose>`, the same as `tests/ForkMigrationTests/TestDb.cs`. `AdoptionDbTests` and `ReleaseChannelDbTests` use it.
  - `Create` and `Drop` refuse any other name.
  - A run's first database test drops scratch databases older than 12 hours (`DropStaleScratchDatabases`), so a crashed run's databases still get cleaned up.
  - The old fixed names are never dropped: runs from worktrees that don't have this fix may still be using them.
- **Tests.** New `tests/ReleaseFlowTests/ScratchDbTests.cs`, 4 tests:
  - names are unique to the run and never the old fixed names;
  - a name over 64 characters throws;
  - `Create` and `Drop` refuse other databases;
  - the stale cleanup drops only scratch databases older than 12 hours. It keeps a newer one, which may belong to a run still going, and any name outside the scheme.
- **Interplay.** Any other package that patches `ReleaseFlowTests` the same way will conflict only in these test files. Keep either version: both give unique names per run.

## Untested

- Nothing was sent to DentalXChange; no test account was available. Their acceptance of PDFs, the size limit and the file-name convention are unconfirmed.
- The WinForms windows were not opened (the GUI is not launched in this run). Their logic is checked by build and source checks only; the business calls behind them are unit-tested.
- Cloud-storage reads of PDF documents (`ImageStore.GetDocumentFileBytes` with cloud storage) were not exercised. The database and A to Z paths were.

## Owner questions

1. What is DentalXChange's real per-file (and per-attachment) size limit for PDFs? Change `DxcPdfAttachment.MaxPdfBytes` to match.
2. Should PDFs with only an owner password (they open without a password) be allowed? They are refused today.
3. Is the Send File button in Select Image wanted, or should PDFs from disk be imported into the patient's images first and sent from there?

## Integration (branch `port/int-5`)

- `tests/ReleaseFlowTests`: `port/int-4` already had the same fix from M1 (`ScratchDb.RunName`, `IsRunName`, `RequireRunName`, `DropStaleRunDatabases`, covered by `ScratchDbNameTests`), so `TestSupport.cs`, `AdoptionDbTests.cs` and `ReleaseChannelDbTests.cs` keep that version, as the fix round section above allows. The four tests of this package's `ScratchDbTests.cs` are kept and now call the kept helper. One difference: the kept `Drop` ignores a name outside the per-run scheme instead of throwing, so `DropAndCreate_RefuseOtherDatabases` became `DropAndCreate_LeaveOtherDatabasesAlone` (Create still refuses those names; Drop is checked to see them as outside the scheme). No test was removed.
