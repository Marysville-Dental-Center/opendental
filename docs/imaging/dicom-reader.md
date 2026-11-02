# The in-house DICOM reader

Open PMS - Dental reads every DICOM file with its own code. fo-dicom 4.0.7 (MS-PL), the library Open Dental used, is gone: the owner chose on 2026-11-02 (option C) to drop it rather than move to fo-dicom 5, because the MS-PL is not compatible with the GNU GPL. The reader is written from the public DICOM standard only: NEMA PS3.3 (C.7.6.3, the Image Pixel module), PS3.5 (data structures and encoding, Annex A transfer syntaxes, Annex G RLE), PS3.6 (Table A-1, the transfer syntax UIDs) and PS3.10 (the file format). No code was copied or translated from fo-dicom or any other implementation.

The capture writer (`ImagingCapture/Dicom/DicomIntraOralWriter.cs`) is unchanged: it writes uncompressed Explicit VR Little Endian files.

## Where it sits

| Piece | File | Job |
|---|---|---|
| `IDicomPixelReader` | `OpenPmsDentalBusiness/Imaging/IDicomPixelReader.cs` | The contract every display path reads through: a file or stream in, a `BitmapDicom` or null out. Never throws. |
| `DicomHelper.PixelReader` | `OpenPmsDentalBusiness/Imaging/BitmapDicom.cs` | `DicomPixelReaderChain(SubsetReader, FullReader)`, used by `GetFromFile`, `GetFromStream` and `GetFromBase64`. |
| `DicomSubsetReader` | `OpenPmsDentalBusiness/Imaging/DicomSubsetReader.cs` | The small strict reader of the files the capture writes (CAP-DR, design 7.8). Unchanged. |
| `DicomImageReader` | `OpenPmsDentalBusiness/Imaging/DicomImageReader.cs` | The full reader: every encoding, the image kinds below, encapsulated pixel data. |
| `IDicomFrameDecoder`, `DicomFrameDecoderRegistry` | `OpenPmsDentalBusiness/Imaging/DicomFrameDecoders.cs` | The one decoder seam, and the in-house decoders. |
| `DicomTransferSyntaxes` | `OpenPmsDentalBusiness/Imaging/DicomTransferSyntaxes.cs` | Every transfer syntax of PS3.6 Table A-1, with its name and kind. |

`BitmapDicom` stays the result, so the Imaging module, mounts, thumbnails (`Documents.GetThumbnail`), cropping and export (`ImageHelper.GetImageCropped`), printing, e-mail and the claim attachment's 8-bit JPEG copy (`ClaimAttachDicom`, package CLAIM-DCM) show DICOM as before: `ImageStore.OpenBitmapDicom`, `DicomHelper.ApplyWindowing` and `DicomHelper.CalculateWindowingOnImport` are unchanged.

### Why the subset reader stays first

`DicomHelper.PixelReader` is the subset reader followed by the full reader, not the full reader alone:

- The files the program writes itself, its 16-bit radiograph masters, keep going through the reader the 16-bit default was reviewed and signed off with (design 7.8, condition 7), with nothing changed in that path.
- The subset reader looks at the first 64 KB and declines anything else, so the cost for other files is small.
- The full reader reads every subset file to the same `BitmapDicom` (tested on every capture writer file and on every case below), so the chain could become the full reader alone later without any visible change.

The full reader reads from the start of the file the subset reader declined. A stream that cannot seek is copied into memory first, at most 32 MB of it; a longer one goes to the full reader only, which reads it forward. The program passes only files and memory streams.

## What it reads

### Encodings (PS3.5 section 7, Annex A)

- Implicit VR Little Endian (1.2.840.10008.1.2), and the retired Papyrus 3 Implicit VR Little Endian (1.2.840.10008.1.20).
- Explicit VR Little Endian (1.2.840.10008.1.2.1).
- Explicit VR Big Endian (1.2.840.10008.1.2.2, retired): values, items and delimiters in big endian; OW pixel data and palette tables are 16-bit words.
- Deflated Explicit VR Little Endian (1.2.840.10008.1.2.1.99): everything after the File Meta Information is deflated (RFC 1951, no zlib header), inflated with `System.IO.Compression.DeflateStream`.
- Sequences and items of defined and undefined length, nested up to 16 deep; UN of undefined length (Implicit VR Little Endian inside, PS3.5 6.2.2); elements of odd length, skipped by their length; tags out of order.

### Files with and without the PS3.10 header

What fo-dicom 4 opened, checked on synthetic files before it was removed, and what this reader accepts:

| Layout | Accepted |
|---|---|
| 128-byte preamble, "DICM", File Meta Information (PS3.10 7.1) | Yes |
| File Meta Information (group 0002, Explicit VR Little Endian) at byte 0, no preamble, no "DICM" | Yes |
| A data set with no File Meta Information at all (no preamble, no "DICM"): Implicit VR Little Endian, Explicit VR Little Endian or Explicit VR Big Endian | Yes. The encoding is told from the first element: VR letters where the VR goes mean Explicit VR, and big endian when the group number reads smaller that way. The first element must be of an even group from 0008 to 0028, and in Implicit VR its length must fit the file; otherwise the file is not DICOM. |
| PS3.10 header whose File Meta Information has no Transfer Syntax UID | Yes, the data set's encoding is told the same way |
| "DICM" at byte 0 without the preamble | No, as fo-dicom refused it too (`NotDicom`) |
| 128 bytes, then a data set without "DICM" | No (`NotDicom`) |

A data set without a transfer syntax is read as native pixel data; encapsulated pixel data needs the transfer syntax.

### Images (PS3.3 C.7.6.3)

| Photometric Interpretation | Samples, bits | `BitmapDicom` |
|---|---|---|
| MONOCHROME1, MONOCHROME2 | 1 sample; 8 or 16 bits allocated; any Bits Stored from 1 up; any High Bit from Bits Stored - 1 to Bits Allocated - 1; unsigned or signed | The stored values, gray, MONOCHROME1 not inverted (the windowing handles it, as before) |
| RGB | 3 samples, planar configuration 0 or 1, 8 or 16 bits allocated | Gray (R+G+B)/3, 8 bits, `PhotometricInterp` RGB |
| YBR_FULL | the same | Converted to RGB first (the inverse of PS3.3 C.7.6.3.1.2), then gray |
| YBR_FULL_422 | native: 8 bits, Y1 Y2 Cb Cr for each two pixels of a row; in JPEG: the stream's own subsampling | The same |
| PALETTE COLOR | 1 sample, 8 or 16 bits; tables of 8 or 16 bits per entry, any first mapped value | Through the tables (an index below the first entry takes the first, one past the last takes the last), then gray |

The values are what the old path through fo-dicom gave wherever it showed an image:

- the Bits Stored bits ending at High Bit, the other bits cleared (PS3.5 8.1.1);
- no Rescale Slope or Intercept and no VOI window: `BitmapDicom` carries neither, as before; the program's own windowing (`CalculateWindowingOnImport`, `ApplyWindowing`) applies;
- the first frame of a multi-frame image (the program shows only frame 0, as before); the other frames are never read;
- color averaged to gray.

What fo-dicom's path did not show is now shown in the same terms:

- **Signed values** (Pixel Representation 1) are moved up by half their range, 2^(Bits Stored - 1), so that they are unsigned in the same order: the most negative value is 0.
- **Bits Stored other than 8, 12 and 16** is shifted up to the next of those depths, the ones the windowing knows: 8 bits allocated show 8; in 16 bits allocated, up to 8 show 8, 9 to 12 show 12, 13 to 16 show 16.
- **YBR_FULL, YBR_FULL_422 and PALETTE COLOR** become RGB, then gray. fo-dicom's path declined them (its photometric check knew only MONOCHROME1, MONOCHROME2 and RGB).
- **16-bit color** is taken at 8 bits (the high byte of each sample). fo-dicom's path averaged the first bytes of the 16-bit samples as if they were 8-bit triples and kept Bits Allocated 16, which showed a dark, scrambled image.
- **Signed 8-bit gray**: fo-dicom's path showed the two's complement bytes as unsigned (-1 white next to a black 0); it is now moved up like every other signed image.

Not shown, with a reason (`Unsupported`): floating point pixel data, Bits Allocated 1 or 32, photometric interpretations other than the six above (YBR_PARTIAL_*, YBR_ICT and YBR_RCT outside a decoder, CMYK, ARGB, HSV, XYB), signed color or palette values, segmented palette tables, no pixel data.

### Encapsulated pixel data (PS3.5 A.4)

The reader parses the Pixel Data items itself: the Basic Offset Table item, the fragments, the Sequence Delimitation Item, and the Extended Offset Table (7FE0,0001) when the Basic Offset Table is empty. The first frame is:

1. the fragments from the first offset up to the second offset of the Basic Offset Table, or of the Extended Offset Table;
2. with one frame, every fragment;
3. with one fragment per frame (RLE Lossless, PS3.5 G.5; Encapsulated Uncompressed, A.4.11), the first fragment;
4. otherwise the fragments up to the first one that ends with FFD9 (the JPEG end of image marker, also the JPEG 2000 end of codestream), as PS3.5 A.4 lets a reader find frames.

### The decoder seam

```csharp
public interface IDicomFrameDecoder {
	string Name { get; }
	IReadOnlyList<string> TransferSyntaxUids { get; }
	DicomFrameDecodeResult Decode(string transferSyntaxUid,byte[] frameBytes,DicomFrameInfo frameInfo);
}
```

A decoder gets the transfer syntax, the first frame's compressed bytes (its fragments put together) and the image attributes of the file (`DicomFrameInfo`: Rows, Columns, Samples per Pixel, Photometric Interpretation, Planar Configuration, Bits Allocated, Bits Stored, High Bit, Pixel Representation, Number of Frames, frame index). It returns the uncompressed pixels exactly as one frame of native Explicit VR Little Endian pixel data would hold them, with the attributes of those pixels (a JPEG decoder returns YBR data as RGB). `DicomFrameDecodeResult.Failed` declines a broken frame (`Malformed`), `NotSupported` a feature the decoder lacks (`Unsupported`). The reader checks the answer like any other pixel data; a decoder that throws or answers nonsense declines the file, never the program.

`DicomFrameDecoderRegistry.Default` lists which transfer syntaxes have a decoder. A later package plugs one in with `DicomFrameDecoderRegistry.Default.Register(decoder)`; a decoder registered later for a transfer syntax replaces the earlier one.

In-house decoders of this version:

| Transfer syntax | Decoder |
|---|---|
| RLE Lossless (1.2.840.10008.1.2.5) | `DicomRleDecoder`, PS3.5 Annex G: the 64-byte header, one segment per byte of each sample (most significant first), the runs of G.3.2 |
| JPEG Baseline (Process 1) (1.2.840.10008.1.2.4.50) | `DicomJpegBaselineDecoder`, through the .NET Framework's own JPEG decoder (GDI+, `System.Drawing`), with no color management. It applies the stream's own color transform, so YBR_FULL_422 and YBR_FULL come back as RGB. The frame header is checked first: 8-bit precision, baseline, extended or progressive Huffman coding, one or three components as Samples per Pixel says, the size of Rows and Columns, and an end of image marker. Its pixels equal what fo-dicom's native IJG decoder gave for the same streams, and stay within 3 of WPF's decoder. |
| Encapsulated Uncompressed Explicit VR Little Endian (1.2.840.10008.1.2.1.98) | `DicomEncapsulatedUncompressedDecoder`: each fragment is one native frame (fo-dicom 4 did not know this transfer syntax) |

Every other compressed transfer syntax of PS3.6 (JPEG Extended, the retired JPEG processes, JPEG Lossless, JPEG-LS, JPEG 2000 and its Part 2, HTJ2K, JPEG XL, Deflated Image Frame Compression), video (MPEG-2, H.264, H.265), pixel data that is not in the file (JPIP Referenced), the retired MIME and XML encodings, the SMPTE ST 2110 streams and transfer syntaxes the standard does not list are declined as `UnsupportedCompression`. `DicomHelper.GetNotReadMessage` turns that into "This DICOM image is stored as <the PS3.6 name>. This version of the program cannot open that format yet." The Imaging module shows it when such a document is opened, and the mount message names it next to the mount position. Such a file is still imported and kept, with no window, so that a later version that has the decoder opens it.

## Hardening

- **Never throws.** `ReadFileResult`, `ReadStreamResult` and `Read` return a status (`Read`, `NotDicom`, `UnsupportedCompression`, `Unsupported`, `Malformed`, `TooLarge`, `FileError`) and a reason. `DicomHelper.GetFromFile`, `GetFromStream` and `GetFromBase64` return null.
- **Bounded streaming read.** The stream is read forward once. Every length is checked against what is left before it is read or skipped: a seekable stream knows its end, so a value or a pixel data length past it is refused without reading; a non-seekable one (a deflated data set) is read, never trusted, and a declared length longer than the data is found when the data ends. Skipped values are skipped by seeking when the stream can seek, otherwise read through in 80 KB pieces.
- **Memory caps.** Only the kept attributes are allocated (each at most 256 KB: palette tables are at most 128 KB), the first frame (at most 2^26 pixels, 8192 x 8192, `MaxFramePixels`, refused from Rows and Columns before anything is allocated) and that frame's compressed bytes (at most 256 MB, `MaxCompressedFrameBytes`; at most 65536 fragments). A deflated data set may inflate to at most 1 GB (`MaxInflatedBytes`). The subset reader's 256 MB file limit does not apply: a file of any size is read up to its first frame, so a large multi-frame volume costs one frame.
- **Sequences** of undefined length nested deeper than 16 are refused (`Malformed`); sequences and items of defined length are skipped by their length without looking inside, so their depth costs nothing.
- **Malformed** files are declined with a reason: truncation anywhere, lengths past the end, undefined lengths where none may be, items or delimiters out of place, an unknown VR in Explicit VR, a value that is not the size its VR needs, missing image attributes, Bits Stored, High Bit, Pixel Representation or Planar Configuration out of range, a broken Basic Offset Table, fragment, RLE header or segment, a JPEG stream that is cut off or does not match the attributes.

## Tests

`tests/DicomReaderTests`:

- `ReaderCases.cs`: 99 synthetic files made in the test from the standard (no patient image), one for each encoding, layout, photometric interpretation, bit depth and High Bit, signed and unsigned, rescale and window, multi-frame, a 1200 x 1000 image, RLE (the test's own RLE encoder), JPEG Baseline (two fixed streams from the .NET Framework's `JpegBitmapEncoder`), Encapsulated Uncompressed, and twelve compressed formats this version does not decode.
- `FoDicomBaseline.cs`: what fo-dicom 4.0.7 with its native codecs read from each of these exact files through the old `DicomHelper` path (size, bit depth, photometric interpretation, SHA-256 of the values), recorded before fo-dicom was removed. The in-house reader gives the same for every case fo-dicom showed, except the two deliberate differences above, whose old images are checked to be what the record says.
- Values computed from the standard for every image fo-dicom's path did not show; RLE and JPEG round trips; the decoder seam with a plugged-in decoder; a 3.75 GB file of which only the first frame is read; a deflated data set that would inflate past the limit; truncation at every byte, broken lengths, sequences, encapsulation, RLE and JPEG frames, and random damage, all declined without an exception.
- `FoDicomRemovedTests`: fo-dicom is not in the repository, any project, the source or a build output; the release check refuses it and an upgrade removes it.
