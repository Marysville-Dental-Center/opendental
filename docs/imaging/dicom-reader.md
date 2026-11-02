# The in-house DICOM reader

Open PMS - Dental reads DICOM with its own code. fo-dicom 4.0.7 (MS-PL), the library Open Dental used, is gone: the owner chose on 2026-11-02 (option C) to drop it rather than move to fo-dicom 5, because the MS-PL is not compatible with the GNU GPL. The reader is written from the public DICOM standard only: NEMA PS3.3 (C.7.6.3, the Image Pixel module), PS3.5 (data structures and encoding, Annex A transfer syntaxes, Annex G RLE), PS3.6 (Table A-1, the transfer syntax UIDs) and PS3.10 (the file format). No code was copied or translated from fo-dicom or any other implementation.

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

The encoding is decided from the data where writers broke the standard in ways fo-dicom 4 read:

- **An Implicit VR data set labelled Explicit VR.** When the transfer syntax is Explicit VR Little Endian (native, encapsulated or deflated, after inflating), the first element of the data set is looked at: if bytes 4 and 5 are neither VR letters nor two spaces, and its length read as Implicit VR fits the file, the whole data set is read as Implicit VR Little Endian. The transfer syntax reported is still the file's.
- **Two spaces where the VR goes.** An Explicit VR element (little or big endian) whose VR is 0x20 0x20 is read as follows. Any other bytes that are not a VR of PS3.5 Table 6.2-1 are `Malformed`, as before (fo-dicom found no Rows in such files and showed nothing).
  - On a tag the reader keeps, it takes the VR the reader knows, with that VR's layout: two reserved bytes and a 32-bit length for Pixel Data and the palette tables (OW) and the Extended Offset Table (OV), a 16-bit length for the image attributes.
  - On any other tag the reader knows no VR (fo-dicom took the long layout where its dictionary gave a long VR, such as a sequence, UT or OB, and for private tags, and the 16-bit layout for LO, CS and the like), so the layout is told from the data. Bytes 6 and 7 other than 00 00 are a 16-bit length. When they are 00 00, bytes 8 to 11 read as a 32-bit length decide: undefined (FFFFFFFF) is a sequence, skipped item by item (in the data set's encoding, or in Implicit VR Little Endian as UN when the first item of undefined length starts with an element that has no VR letters); a length past what is left of the file is the 16-bit layout with an empty value. Otherwise the 16-bit reading, an empty value followed by the next element at byte 8, is checked: if those 8 bytes do not look like the next header (a tag of the same or a later group with VR letters or two spaces, 00 00 after a VR of the long layout, or, inside an item, the Item Delimitation Item), the long layout is taken. If they do, the reader looks at the 8 bytes after the 32-bit reading's value: the long layout when they look like the next header or the data ends there, the 16-bit layout otherwise. So a blank-VR private OB, sequence or UT reads as fo-dicom read it, and so does an empty blank-VR LO or CS.
  - The look past the value seeks and comes back (a file or a memory stream) or, in a stream that cannot seek (a deflated data set), reads ahead in memory, at most 64 KB (`MaxLookAheadBytes`); farther than that the value is taken as empty, so a deflated file whose blank-VR value is over 64 KB and starts with bytes that look like an element header is declined. At most 10,000 such looks are made in one file (`MaxBlankVrLookAheads`, `TooLarge` past it); a real file has a handful.

### Files with and without the PS3.10 header

Exactly what this reader accepts, and what fo-dicom 4.0.7 did with the same synthetic file (each checked with the version 0.0 `Dicom.Core.dll` and the old `DicomHelper` path; the cases are in `ReaderCases.cs`, the values in `FoDicomBaseline.cs`):

| Layout | This reader | fo-dicom 4 |
|---|---|---|
| 128-byte preamble, "DICM", File Meta Information (PS3.10 7.1) | Read | Read |
| File Meta Information (group 0002) at byte 0, no preamble, no "DICM" | Read | Read |
| File Meta Information in Implicit VR Little Endian (against PS3.10 7.1), after "DICM" or at byte 0: the first group 0002 element has no VR letters where the VR goes (at byte 0 its 32-bit length must also fit the file) | Read; group 0002 is read with 32-bit lengths, then the Transfer Syntax UID decides as usual | Read |
| PS3.10 header whose File Meta Information has no Transfer Syntax UID | Read; the data set's encoding is told as in the next row | Read |
| A data set with no File Meta Information (no preamble, no "DICM"): Implicit VR Little Endian, Explicit VR Little Endian or Explicit VR Big Endian | Read. Explicit VR when the first element has VR letters (or two spaces) where the VR goes, big endian when its group number reads smaller that way, otherwise Implicit VR. The first element's group must be 0004 or from 0008 to 00FF, standard or private, in any tag order, and in Implicit VR its length must fit the file. | Read where checked (first group 0008 or 0009 in both encodings; 0040 or 0004 in Implicit VR), except an Explicit VR data set with group 0004 first, which it did not show |
| A preamble of 128 zero bytes without "DICM", then a data set (or File Meta Information) | Read: the zeros are skipped and what follows is told as at byte 0 | Read the Implicit VR data set (in Implicit VR the zeros are 16 empty elements of group 0000); not the Explicit VR one |
| A first element of group 0000 to 0003 or 0005 to 0007, or above 00FF, with no File Meta Information | `NotDicom` (a file of zeros has group 0000) | Not shown where checked (first group 0100, and 4000 in Explicit VR) |
| An Implicit VR first element whose length is past the end of the file | `NotDicom` | Not shown |
| "DICM" at byte 0 without the preamble | `NotDicom` | Refused (threw) |
| A preamble that is not zeros, then a data set without "DICM" | `NotDicom` | Not checked |
| An Explicit VR data set labelled Implicit VR | `Malformed` | Not shown |

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
- **The import window of 9 to 11 bits stored.** Shifted up to 12 bits, such data holds only every 2nd, 4th or 8th value of the 12-bit histogram that `CalculateWindowingOnImport` searches. The search therefore steps by the data step (the lowest bit set in any non-zero value, at most 8: `DicomHelper.GetHistogramStep`) and stops at the first bin under 150 times the step, so the window matches the one the same image gets at 12 bits instead of collapsing to a black-and-white threshold. With a step of 1, every ordinary 12-bit image, the window is exactly what it always was; a 12-bit image whose values are all even gets step 2 (`ImportWindowBitDepthTests`).
- **YBR_FULL, YBR_FULL_422 and PALETTE COLOR** become RGB, then gray, and so does JPEG Baseline labelled YBR_PARTIAL_422. fo-dicom's path declined them (its photometric check knew only MONOCHROME1, MONOCHROME2 and RGB).
- **16-bit color** is taken at 8 bits (the high byte of each sample). fo-dicom's path averaged the first bytes of the 16-bit samples as if they were 8-bit triples and kept Bits Allocated 16, which showed a dark, scrambled image.
- **Signed 8-bit gray**: fo-dicom's path showed the two's complement bytes as unsigned (-1 white next to a black 0); it is now moved up like every other signed image.

Not shown, with a reason (`Unsupported`): floating point pixel data, Bits Allocated 1 or 32, photometric interpretations other than the six above (YBR_PARTIAL_422, YBR_PARTIAL_420, YBR_ICT and YBR_RCT as native pixel data or as what a decoder returns: they describe compressed data, and a decoder is given them, see below; CMYK, ARGB, HSV, XYB), signed color or palette values, segmented palette tables, no pixel data.

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

Before the decoder is called, the file's attributes are checked only for size, bits and samples, and every photometric interpretation PS3.5 8.2 allows for compressed data is accepted: MONOCHROME1, MONOCHROME2, PALETTE COLOR, RGB, YBR_FULL, YBR_FULL_422, and YBR_PARTIAL_422, YBR_PARTIAL_420, YBR_ICT and YBR_RCT (the last four with 3 samples per pixel; YBR_ICT and YBR_RCT are what color JPEG 2000 and HTJ2K with the multiple-component transform carry, PS3.5 8.2.4). What is shown (the table above, unsigned color) is decided on the decoder's answer, so a JPEG 2000 decoder that returns RGB for a YBR_ICT file is shown.

`DicomFrameDecoderRegistry.Default` lists which transfer syntaxes have a decoder. Decoders for the other compressed formats are deferred (docs/roadmap.md, Later: only if such files turn up, through a thin layer over libjpeg-turbo, OpenJPEG and CharLS). One would plug in with `DicomFrameDecoderRegistry.Default.Register(decoder)`; a decoder registered later for a transfer syntax replaces the earlier one.

In-house decoders of this version:

| Transfer syntax | Decoder |
|---|---|
| RLE Lossless (1.2.840.10008.1.2.5) | `DicomRleDecoder`, PS3.5 Annex G: the 64-byte header, one segment per byte of each sample (most significant first), the runs of G.3.2 |
| JPEG Baseline (Process 1) (1.2.840.10008.1.2.4.50) | `DicomJpegBaselineDecoder`, through the .NET Framework's own JPEG decoder (GDI+, `System.Drawing`), with no color management. It applies the stream's own color transform, so YBR_FULL_422, YBR_PARTIAL_422 and YBR_FULL come back as RGB. The stream's markers are walked first (ITU-T T.81 B.1, B.2), the way the decoder reads them (GDI+'s decoder is the IJG one, as its messages show; each rule below was checked against it): from the start of image marker to the first end of image marker, ignoring whatever follows it; every marker segment by its length (APPn, COM and DNL of length 0 or 1 are their length bytes alone); between segments, before the frame header as after it, every byte up to the next marker passed over (entropy-coded data, fill bytes, stray bytes and FF 00, which the decoder discards). It requires a frame header of baseline, extended or progressive Huffman coding (fo-dicom 4 decoded progressive streams under this transfer syntax, so they stay), 8-bit precision, one or three components as Samples per Pixel says, the size of Rows and Columns, an end of image marker, and at most 32 scans (`MaxScans`; see Hardening); a second start of image marker, a scan before the frame header, a reserved marker or a JPEG extension marker (such as JPEG-LS's), where the decoder stops, are declined. Its pixels equal what fo-dicom's native IJG decoder gave for the same streams, and stay within 3 of WPF's decoder. |
| Encapsulated Uncompressed Explicit VR Little Endian (1.2.840.10008.1.2.1.98) | `DicomEncapsulatedUncompressedDecoder`: each fragment is one native frame (fo-dicom 4 did not know this transfer syntax) |

Every other compressed transfer syntax of PS3.6 (JPEG Extended, the retired JPEG processes, JPEG Lossless, JPEG-LS, JPEG 2000 and its Part 2, HTJ2K, JPEG XL, Deflated Image Frame Compression), video (MPEG-2, H.264, H.265), pixel data that is not in the file (JPIP Referenced), the retired MIME and XML encodings, the SMPTE ST 2110 streams and transfer syntaxes the standard does not list are declined as `UnsupportedCompression`. `DicomHelper.GetNotReadMessage` turns that into "This DICOM image is stored as <the PS3.6 name>. This version of the program does not open that format. ..." (with what the office can do: keep the image, have the program that made it export uncompressed or JPEG Baseline DICOM). The Imaging module shows it when such a document is opened, and the mount message names it next to the mount position. Such a file is still imported and kept, with no window, so that a version with a decoder for it, if one is ever built (docs/roadmap.md, Later), opens it.

## Hardening

- **Never throws.** `ReadFileResult`, `ReadStreamResult` and `Read` return a status (`Read`, `NotDicom`, `UnsupportedCompression`, `Unsupported`, `Malformed`, `TooLarge`, `FileError`) and a reason. `DicomHelper.GetFromFile`, `GetFromStream` and `GetFromBase64` return null.
- **Bounded streaming read.** The stream is read forward once; the only look back is after a look past the value of a blank-VR element (see Encodings), which a seekable stream does by seeking ahead and back and a non-seekable one by reading at most 64 KB ahead into memory. Every length is checked against what is left before it is read or skipped: a seekable stream knows its end, so a value or a pixel data length past it is refused without reading; a non-seekable one (a deflated data set) is read, never trusted, and a declared length longer than the data is found when the data ends. Skipped values are skipped by seeking when the stream can seek, otherwise read through in 80 KB pieces; on such a stream a frame or fragment is read in 4 MB pieces, so memory grows with the bytes really there, not with the length a file declares.
- **Memory caps.** Only the kept attributes are allocated (each at most 256 KB: palette tables are at most 128 KB), the first frame (at most 2^26 pixels, 8192 x 8192, `MaxFramePixels`, refused from Rows and Columns before anything is allocated) and that frame's compressed bytes (at most 256 MB, `MaxCompressedFrameBytes`; at most 65536 fragments). A deflated data set may inflate to at most 64 MB before its Pixel Data (`MaxInflatedBytesBeforePixelData`: only the pixels need to be large) and to at most 1 GB in all (`MaxInflatedBytes`). The subset reader's 256 MB file limit does not apply: a file of any size is read up to its first frame, so a large multi-frame volume costs one frame.
- **Work caps.** Memory caps do not bound time: a deflated file of 1.5 MB can hold a gigabyte of 8-byte elements, and reading each header costs about half a microsecond, which made such a file take over a minute on the UI thread. The reader reads at most 1,000,000 element, item, delimiter and fragment headers in one file (`MaxElements`, `TooLarge` past it), about half a second; a real image's data set has a few thousand. In a JPEG stream each scan costs GDI+ a pass over every block, and an end-of-band run lets a few bytes cover a whole scan, so a third of a megabyte of progressive JPEG with 2000 scans took half a minute: `DicomJpegBaselineDecoder` counts the scans (every SOS marker the decoder would see, found by walking the stream the way the decoder reads it: see the decoder table) and declines more than 32 (`Unsupported`) before GDI+ runs. Because the walk passes over bytes outside the marker segments exactly where the decoder does, before the frame header as after it, no bytes (FF 00 and a length, stray bytes, a decoy segment) can make it count fewer scans than the decoder decodes. A sequential stream has at most one scan per component and encoders write progressive streams of about ten; fo-dicom decoded progressive streams of any number of scans. Telling the layout of an element with two spaces for its VR may take a look past its value (see Encodings), at most 10,000 times in one file (`MaxBlankVrLookAheads`, `TooLarge` past it), which bounds the seeks to well under a second.
- **Sequences** of undefined length nested deeper than 16 are refused (`Malformed`); sequences and items of defined length are skipped by their length without looking inside, so their depth costs nothing.
- **Malformed** files are declined with a reason: truncation anywhere, lengths past the end, undefined lengths where none may be, items or delimiters out of place, an unknown VR in Explicit VR (two spaces excepted, see Encodings), a value that is not the size its VR needs, missing image attributes, Bits Stored, High Bit, Pixel Representation or Planar Configuration out of range, a broken Basic Offset Table, fragment, RLE header or segment, a JPEG stream that is cut off or does not match the attributes.

### What version 0.0 showed and this version declines

Checked with the version 0.0 `Dicom.Core.dll` and `Dicom.Native.dll` and the old `DicomHelper` path, outside the repository:

| File | Version 0.0 (fo-dicom 4) | This reader |
|---|---|---|
| JPEG Baseline of more than 32 scans | Decoded every scan (half a minute for a third of a megabyte at 8192 x 8192) | `Unsupported` (Work caps) |
| JPEG Baseline whose frame header gives another size than Rows and Columns | Showed it at Rows x Columns: cut down when smaller, slanted when Columns differs, with made-up rows when Rows is larger | `Malformed`, naming both sizes |
| JPEG Baseline with no end of image marker | Never returned on some (the stream cut in its scan, or right after its last scan), showed others (the same stream followed by 00 00) | `Malformed` ("cut off") |
| Deflated data set with a blank-VR element in the long layout whose value is over 64 KB and starts like an element header | Read (UN for a private tag) | `Malformed` (the value is taken as empty; see Encodings). The same element in a file that is not deflated is read. |

Apart from these and the two corrections above, every case of `ReaderCases.cs` that fo-dicom showed reads the same, and so do the JPEG oddities of the decoder table that were checked (stray bytes and FF 00 before or after the frame header, junk, FF 00, FF FF or an FF pad after the end of image marker, an APP0 length 2 short, COM and APPn of length 0 or 1, TEM, RST and DNL between segments).

### Owner decisions (2026-11-02)

The owner kept the reader as described here ([owner-questions.md](../owner-questions.md), "Library updates and the DICOM reader"):

- Compressed DICOM without a decoder shows the message that names its format; the decoders are deferred (docs/roadmap.md, Later).
- The two corrections stay: signed 8-bit and 16-bit RGB images show correctly, not as version 0.0 showed them.
- Progressive JPEG under JPEG Baseline stays, with the 32-scan cap.
- A JPEG Baseline image whose frame header gives another size than Rows and Columns stays declined, and so does a JPEG stream with no end
  of image marker.

## Tests

`tests/DicomReaderTests`:

- `ReaderCases.cs`: 129 synthetic files made in the test from the standard (no patient image), one for each encoding, layout, photometric interpretation, bit depth and High Bit, signed and unsigned, rescale and window, multi-frame, a 1200 x 1000 image, RLE (the test's own RLE encoder), JPEG Baseline (two fixed streams from the .NET Framework's `JpegBitmapEncoder`, the color one also with junk or an FF pad after its end, stray bytes before its DQT and an APP0 length 2 short, and a progressive stream written in the test from T.81 Annex G), Encapsulated Uncompressed, twelve compressed formats this version does not decode, and the mislabelled layouts of the Encodings and PS3.10 header sections (`MislabelledEncodings`, blank VRs in both layouts included).
- `FoDicomBaseline.cs`: what fo-dicom 4.0.7 with its native codecs read from each of these exact files through the old `DicomHelper` path (size, bit depth, photometric interpretation, SHA-256 of the values), recorded before fo-dicom was removed; the 17 cases added after the first review and the 13 added after the second were recorded the same way outside the repository with the version 0.0 DLLs, each time in a run that gave every earlier value again. The in-house reader gives the same for every case fo-dicom showed, except the two deliberate differences above, whose old images are checked to be what the record says.
- Values computed from the standard for every image fo-dicom's path did not show; RLE and JPEG round trips; the decoder seam with a plugged-in decoder, including color JPEG 2000 labelled YBR_ICT or YBR_RCT; a 3.75 GB file of which only the first frame is read; deflated data sets that would inflate past either limit; floods of a million tiny elements, items and fragments; progressive JPEG up to and past 32 scans, and 8192 x 8192 with 2000 scans, also behind FF 00, stray bytes or a decoy segment before the frame header; JPEG streams the decoder stops at; the layout of blank VRs in seekable, non-seekable and deflated streams, and a flood of them; truncation at every byte, broken lengths, sequences, encapsulation, RLE and JPEG frames, and random damage, all declined without an exception.
- `FoDicomRemovedTests`: fo-dicom is not in the repository, any project, the source or a build output; the release check refuses it and an upgrade removes it.
