# Compressed DICOM in the 32-bit program: decoder options (research, 2026-11-02)

This is read-only research, dated 2026-11-02. The repo was read at commit 226fb9e15. Nothing was built, downloaded or run. Where the research and the verification disagreed, the verification wins. Bracketed keys such as [FSF] point to the Sources list at the end. Repo paths are relative to the repository root.

## Status after this research (owner decisions, 2026-11-02)

The research was asked while the plan was still to move to fo-dicom 5. Two decisions followed it:

1. **fo-dicom is dropped entirely** (option C of the discussion), not moved to version 5. fo-dicom was only the fallback reader behind the
   in-house subset reader, and its MS-PL licence is the open owner question named below. The in-house reader (written from the public DICOM
   standard) now reads every uncompressed encoding, colour, signed and multi-frame images and 10- to 15-bit data, and decodes RLE and JPEG
   Baseline itself. Every other compressed format goes through one decoder hook and, until a decoder is plugged in, gets a clear message
   naming the format.
2. **No compressed-DICOM decoders for now.** Nothing in the office produces compressed DICOM: the X-ray sensor and the intraoral camera hand
   over uncompressed images and never DICOM, the program stores what it acquires as TIFF, PNG or JPEG, and a pano unit's test export was
   uncompressed Explicit VR Little Endian with 14 bits stored (which the old readers rejected and the new reader opens).

So the fo-dicom-based options below (A, C, D, E and F) no longer apply. If compressed DICOM files ever turn up, the route is option B: our
own thin decode layer over libjpeg-turbo, OpenJPEG and CharLS, plugged into the in-house reader's decoder hook rather than into fo-dicom.
That work is on the roadmap under Later.


## 1. The short answer

In a 32-bit program, fo-dicom 5.2.6 can already open four of the listed transfer syntaxes with its own managed code: RLE, Deflated, and JPEG Lossless .57 and .70 (decode only). So the gap is JPEG Baseline and Extended (.50, .51), JPEG-LS (.80, .81), JPEG 2000 (.90, .91) and HTJ2K (.201-.203) [FO5-core][FO5-setup]. No fo-dicom.Codecs release has a win-x86 build, the 6.0 prereleases included, so the gap needs other codecs [FOC-a5][FOC-b1].

**Recommendation:** write our own thin decode-only layer (GPL) over x86 builds of three current libraries:
- libjpeg-turbo for the JPEG family;
- OpenJPEG for JPEG 2000 and HTJ2K;
- CharLS for JPEG-LS.

All three have GPL-compatible licences [FSF], and the same C# code runs with their x64 builds after the 64-bit move. First count which transfer syntaxes real dental files use, and build only what shows up. Until each piece is in, keep fo-dicom 4's x86 codecs for the missing syntaxes. If the program can drop 32-bit Windows, a 64-bit helper program with the stock fo-dicom.Codecs is the faster way to cover everything.

## 2. What we actually need to decode

**The program only decodes.**
- It has one DICOM writer, and it writes uncompressed Explicit VR Little Endian (`ImagingCapture/Dicom/DicomIntraOralWriter.cs:438`).
- The in-house reader reads exactly that subset and declines everything else (`OpenPmsDentalBusiness/Imaging/DicomSubsetReader.cs:46-62`). So our own captures never need a codec.
- The only transcode is the decompression in `OpenPmsDentalBusiness/Imaging/BitmapDicom.cs:215`, a `Clone` to Explicit VR LE.
- DICOM export is a non-goal (`docs/imaging-capture-design.md:128`). No encoder is needed.

**What the fo-dicom path accepts today, whatever the codec.** A replacement codec only has to match this, not exceed it.
- Photometric interpretation must be MONOCHROME1, MONOCHROME2 or RGB (`BitmapDicom.cs:194-197`, enum at `:332-337`). A colour JPEG labelled YBR_FULL_422 is therefore rejected today, before any decoding.
- Bits Allocated must be 8 or 16 (`:199-202`). Bits Stored must be 8, 12 or 16 (`:203-206`), so 10-bit and 14-bit files are rejected.
- Only frame 0 is read (`:217`).
- Only unsigned grey or 24-bit RGB is accepted, and RGB is averaged to grey (`:218-235`). Signed data is rejected.
- So the bit depths we need are: 8 for .50; 8 and 12 for .51; up to 16 for the lossless syntaxes and for JPEG 2000.

**Where compressed files come from:**
- the Imaging module's Import (`OpenPmsDentalBusiness/Imaging/ImageStore.cs:519-569`) and the byte-array import (`ImageStore.cs:663`);
- files that other software drops into a patient's image folder (`Data Interface/Documents.cs:577-597`);
- import or paste in the claim windows (`docs/port-notes/CLAIM-DCM.md:16-17`);
- cloud and in-database storage, read from bytes (`ImageStore.cs:267-280`);
- documents in an adopted Open Dental database (`README.md:37`). These display today through the same x86 fo-dicom 4 codecs, so they must keep displaying.

**What public dental sources say:**
- The DICOM dental CD profile uses only Explicit VR LE for intra-oral and DX images [PS311-K3].
- Romexis 6.0 lists only uncompressed syntaxes [ROMEXIS]. One check could not read the PDF's text.
- Sidexis 4 imports and exports Explicit VR LE only [SIDEXIS].
- CS Imaging 8 accepts .50, .51, .70, .90 and .91 as a storage SCP, and sends files in the syntax they are stored in [CSI8].
- The CS 9600 unit proposes .70 [CS9600].
- No dental source was found for JPEG-LS or HTJ2K.

My inference: most files arrive uncompressed. .70 is the most likely compressed syntax, then JPEG 2000 and JPEG Baseline/Extended.

**Coverage, syntax by syntax:**

| Syntax | fo-dicom 5 core, 32-bit | Today (fo-dicom 4 x86) | Seen in dental sources |
|---|---|---|---|
| .50 JPEG Baseline | no | yes [FO4-WIKI] | CS Imaging 8 accepts |
| .51 JPEG Extended 12-bit | no | yes | CS Imaging 8 accepts |
| .57 Lossless P14 | yes, decode only [FO5-core] | yes | none found |
| .70 Lossless P14 SV1 | yes, decode only (1 component 2-16 bits, 3 components 8 bits only) [FO5-LL:57-83] | yes | CS 9600, CS Imaging 8 |
| .80/.81 JPEG-LS | no | yes | none found |
| .90/.91 JPEG 2000 | no | yes | CS Imaging 8 accepts |
| .201-.203 HTJ2K | no; the syntaxes are declared but "not actual codec" [FO5-LOG] | no, never supported | none found |
| .5 RLE | yes, managed [FO5-core] | yes | none found |
| .1.2.1.99 Deflated | yes, through DeflateStream [FO5-READER:278-289] | yes | none found |

**So the regression against today is six syntaxes:** .50, .51, .80, .81, .90 and .91. Two smaller gaps sit inside .57 and .70:
- colour lossless above 8 bits throws [FO5-LL:57-83];
- the managed decoder appears to ignore the point transform, so a stream with Pt > 0 would decode wrongly. This comes from reading the code and was not tested [FO5-LLIMPL].

**Tests:** the repo has no real compressed test file. `git ls-files '*.dcm'` returns 0, and the tests use synthetic stand-ins only (`tests/DicomReaderTests/DicomSubsetReaderTests.cs:193-231`, `tests/DicomReaderTests/TestFiles.cs:39`).

## 3. Comparison

On licences: the FSF rates BSD-2, BSD-3, IJG, zlib and MIT as GPL-compatible. It rates Apache-2.0 as compatible with GPL v3 only, and MS-PL as incompatible [FSF]. MS-PL is already an open owner question (`docs/owner-questions.md:38`, `THIRD-PARTY-NOTICES.txt:166-172`).

| Option | Syntaxes and bit depths | Licence and GPL v3 fit | Works in the 32-bit program | Effort | Main risks | At the 64-bit move |
|---|---|---|---|---|---|---|
| **A. fo-dicom 5.2.6 core, managed (built in)** | .5, .1.2.1.99; .57 and .70 decode only (1 component 2-16 bits, 3 components 8 bits) [FO5-LL:57-83] | MS-PL, incompatible. Same question as fo-dicom itself, nothing new | Yes. netstandard2.0, on by default [FO5-SETUP:117] | S (comes with the move) | The tests only check for a non-null render [FO5-TEST:16-24]; Pt > 0 apparently ignored [FO5-LLIMPL] | Unchanged |
| **B1. Own layer + libjpeg-turbo 3.2.1 x86** | .50 8-bit; .51 8/12-bit; .57/.70 2-16 bits, grey and colour [LJT-H][LJT-CL] | IJG + BSD-3 + zlib: compatible. IJG credit line required [LJT-LIC] | Yes. Release ships vc-x86 and gcc-x86 builds [LJT-REL] (contents unchecked), or a CMake Win32 build. cdecl, size_t → UIntPtr [LJT-H:41] | M | Colour/YBR handling is ours. In 3.2, `tj3Init` is a macro; P/Invoke `tj3InitVersion(type, 3002000)` [LJT-H:1339-1344] | Same C#, x64 DLL |
| **B2. Own layer + OpenJPEG 2.5.4 x86** | .90/.91 up to 16 bits; HTJ2K decode for HT-only streams; MIXED is rejected [OPJ-NEWS][OPJ-MIXED] | BSD-2: compatible [OPJ-LIC] | Yes. Release ships a windows-x86 zip [OPJ-REL] (unchecked). API is __stdcall in a Win32 DLL, stream callbacks are cdecl [OPJ-H] | M | HTJ2K untested on DICOM samples. Older versions had CVEs, so stay current [OPJ-NEWS]. Structs must be pointer-size neutral | Same C#, x64 DLL |
| **B3. Own layer + CharLS.Native 4.1.3** | .80/.81, 2-16 bits [CLS-SRC FrameInfo.cs] | BSD-3: compatible [CLS-CAT] | Yes. lib/net48 + charls-2-x86.dll, picks x86 at run time, upstream CI tests net48 x86 [CLS-CAT][CLS-SRC] | S | Needs VC++ ≥ 14.51 [CLS-PKG], but our installer checks only Major ≥ 14 (`installer/OpenPmsDental.iss:142`). No .targets file, so copy the DLL by hand. Colour interleave is ours | Same package; charls-2-x64.dll is already in it [CLS-CAT] |
| **B4. OpenJPH 0.33.0 built x86** | HTJ2K only, HT-only, encode + decode [OJPH README:5] | BSD-2: compatible | Source build only, no binaries. 0.33.0 fixes an MSVC x86 build crash (#395) and an i686 SSE bug (#404). Upstream tests 32-bit MinGW, not MSVC Win32 [OJPH] | M | Not needed for decoding if B2 is used | Same |
| **C. Fork of fo-dicom.Codecs with a Win32 Dicom.Native.dll** | All 12 compressed (HTJ2K HT-only) | Wrapper MS-PL, and a modified fork stays MS-PL (3(D)) [FOC-SRC LICENSE.txt]. Adds a second MS-PL component | Only after fixes [FOC-SRC]: add a Win32 build configuration; JPEG-LS `ulong`→UIntPtr (`DicomJpegLsCodec.cs:203`); HTJ2K StdCall→Cdecl and struct sizes (`DicomHtJpeg2000Codec.cs:15,22,112-124`); J2K StdCall→Cdecl (`DicomJpeg2000Codec.cs:508ff`); `Platform.cs:34` | M, plus fork upkeep | ABI mistakes corrupt memory silently. `Jpeg.cpp:490-495` assumes a 64-bit size_t. Bundled OpenJPH 0.32.0 lacks the 0.33.0 fixes. No upstream x86 CI | Go back to stock, or keep the fork |
| **D. Stock fo-dicom.Codecs.dll + our x86 native DLL with matching exports** | All 12 | MS-PL, as C | Maybe. Relies on OSArchitecture reporting X64 under WOW64: likely [GNSI], not run | M | Breaks silently when upstream changes a signature. x86 stdcall name decoration. Fails on 32-bit Windows (`Platform.cs:67`) | Drop |
| **E. Keep fo-dicom 4.0.7 x86 codecs beside fo-dicom 5 for compressed files** | .50, .51, .57, .70, .80, .81, .90, .91, .5; 8/12/16 bits; no HTJ2K [FO4][FO4-WIKI] | MS-PL, today's state [FO4-CAT] | Yes, ships today (`OpenPmsDentalBusiness/OpenPmsDentalBusiness.csproj:91-93`) | S | Very old codecs: IJG libjpeg 6b (1998), OpenJPEG 1.5.3, CharLS 2.0-era API [FO4]. The 4.x line ended in 2021 [FO4-CAT]. Two fo-dicom copies in one process | Must go (x86 only). The release that ships fo-dicom.Codecs must overwrite the same-named Dicom.Native.dll (`docs/release-process.md:100`) |
| **F. 64-bit helper program: fo-dicom 5.2.6 + fo-dicom.Codecs 5.16.7 win-x64 (.NET Fx 4.8 x64, kept alive, stdio pipes)** | All 12 compressed (HTJ2K HT-only). README states only Baseline 8-bit and Extended 12-bit; other 16-bit depths unconfirmed [FOC-README] | MS-PL, but in a separate program over a pipe, which is the GPL FAQ's separate-programs case [GPLFAQ]. Only if the helper's own code is MIT or MS-PL. Counsel. 5.16.7 bundles IJG 6b (from DCMTK) and omits the OpenJPH notice [FOC-5167] | Not in-process. 64-bit Windows only, but setup still accepts 32-bit Windows 10 (`OpenPmsDental.iss:51,75`) | M + release pipeline changes | Staging needs x86 native files and top-level files (`docs/release-process.md:148-149,159`). New VC++ x64 prerequisite (setup installs x86 only, `OpenPmsDental.iss:106`). Cost per image not measured. Must be built x64 and needs the native DLL copied by hand [FO-1680]. Plus: a codec crash ends only the helper [CSE] | Fold in-process with NativeTranscoderManager; delete the helper or keep it for isolation |
| **F2. Variant of F: every file the in-house reader declines goes to the helper** | All of F, plus Implicit VR LE and Big Endian | No MS-PL left in the GPL program (fo-dicom is used only in `BitmapDicom.cs`); NDde, Apache-2.0 and LGPL parts remain (`THIRD-PARTY-NOTICES.txt:154-180`) | Only the in-house subset reads in-process; on 32-bit Windows nothing else does | M | Gives up the in-process 32-bit decode of .57/.70/RLE/Deflate. The helper must average RGB to grey to match today (`BitmapDicom.cs:225-229`) | As F |
| **F3. Variant of F: transcode compressed files at import** | The gap syntaxes | As F | Later reads are in-process; import needs the helper | S on top of F | Rewrites stored files in place (`Documents.cs:579-580`). Misses `ImageStore.cs:663` and cloud/database documents (`ImageStore.cs:267-279`). Old files still need the helper. Lossy flag must stay "01" [PS33-C76] | Not needed |
| **G1. OpenJPEG.net (managed port of OpenJPEG 2.5.3)** | .90/.91; HTJ2K decode (ported, untested) | BSD-2. Must add the missing upstream notice to ht_dec.cs [OPJNET][HTDEC] | Yes (net462, net471, netstandard2.0) | L | 4-5 times slower than native [OPJNET]. One or two maintainers. 16-bit and signed unconfirmed. No tests | Throwaway |
| **G2. JpegLibrary 0.4.32 (managed)** | .50; .51 (one exact 12-bit test); .57/.70 (8-bit tests only) [JPEGLIB] | MIT + Apache-2.0 parts: fine under v3, not v2-only | Yes (net461) | M | Dormant (last release 2021). 16-bit lossless issue #16 open | Throwaway |
| **G3. cscharls 0.1.1 (managed)** | .80/.81 | BSD-3 | Yes (net20, net452) [CSCHARLS] | M | A 2017 port of CharLS 1.x, unmaintained | Throwaway |
| **G4. ImageSharp 2.1.13 (managed)** | .50, .51 (8/12) [IS2] | Apache-2.0: v3 only | Yes (net472) | M | 5 advisories, 3 high [IS2]. Recovering exact 12-bit values unconfirmed. No lossless | Throwaway |
| **G5. LibJpeg.NET, StbImageSharp, Windows WIC** | .50 at 8 bits only [LJNET][STB][WIC] | BSD-style; MIT/Unlicense; OS component | Yes | M | 8-bit only. WIC assumes JFIF colour [WIC] | Throwaway |
| **H. In-house C# port of rii-mango JPEGLosslessDecoder** | .57/.70 | MIT: compatible [RII] | Yes | M | Duplicates fo-dicom core. Must be ported from the MIT Java, not from fo-dicom's MS-PL C# [FO5-LL:14-16] | Useful only if fo-dicom leaves |

**Ruled out:**
- **FoDicom.PureCodecs 0.5.1**: MS-PL, and no public source was found, so the GPL v3 Corresponding Source could not be provided. Its coverage is a README claim only [PURE].
- **CoreJ2K and CSJ2K**: they carry the JJ2000 notice, which Gentoo calls GPL-3-incompatible [JJ2000].
- **Melville.CSJ2K**: net9.0 only [MCSJ].
- **CharLS.Managed**: net8.0 only [CLSM].
- **ImageSharp 3.x**: net6.0 and the Six Labors Split License [IS3].
- **Commercial J2K and JPEG-LS packages**: they cannot ship under the GPL.

## 4. Recommendation

1. **Count first.** Run a read-only scan of the Transfer Syntax UID (0002,0010) over an office's image folders. The existing `ParseFileMeta` already reads it (`DicomSubsetReader.cs:333-372`). Then collect compressed test files for each syntax found, because the repo has none.
2. **Move to fo-dicom 5 with its managed codecs** for RLE, Deflate, .57 and .70. Register our own `TranscoderManager` subclass that adds core's codecs plus ours. This is needed because `DefaultTranscoderManager` is sealed and scans only fo-dicom.core, and `AddTranscoderManager<T>` replaces it [FO5-TM][FO5-SETUP:175-177].
3. **Bridge:** until each native piece is in, send the missing syntaxes down the existing fo-dicom 4 x86 path (option E). That is no worse than today and effort S.
4. **Build the own decode layer**, in the order the count suggests. The default order is B1 (.50/.51), then B2 (.90/.91 and HTJ2K), then B3 (.80/.81). Each piece is a codec core (bytes in, pixels out) plus a thin `IDicomCodec` adapter.
   - Once B1 is in, consider using it for .57 and .70 too. It handles colour above 8 bits and the point transform.
   - Fix the installer's VC++ check (`OpenPmsDental.iss:142`) and its download link (`:106`) [VCR].
   - Add each DLL to the allow-list and to `THIRD-PARTY-NOTICES.txt`, with the IJG credit line (`docs/release-process.md:154`).

**Why this one:**
- It is the only choice that closes the gap in-process without new MS-PL code. Its libraries are BSD-2, BSD-3, IJG or zlib [FSF]. The thin fo-dicom adapter only stays under the existing owner question.
- It works on every Windows that setup accepts, 32-bit Windows 10 included.
- The libraries are current: libjpeg-turbo 3.2.1 (2026-10-27) [LJT-REL], CharLS.Native 4.1.3 (2026-10-13) [CLS-PKG], OpenJPEG 2.5.4 (2025-09-20) [OPJ-REL]. Today's fo-dicom 4 carries libjpeg 6b from 1998 and OpenJPEG 1.5.3 [FO4].
- It survives the 64-bit move. The same C# runs with the x64 DLLs: CharLS.Native already ships one [CLS-CAT], and fo-dicom.Codecs builds the same libraries for x64 [FOC-SRC build_x64]. The codec cores could also feed the in-house reader if fo-dicom is dropped later (`docs/owner-questions.md:38`).

**What it costs:**
- Effort L in total.
- Native code runs in-process, so a memory fault in a codec ends the program. That is the same as today [CSE].
- Colour handling is ours to write.
- HTJ2K MIXED streams stay undecodable. Nothing proposed here decodes them [OPJ-MIXED][OJPH][PS35-A44].

**Fallback:** if the x86 builds or the wrappers stall, use the 64-bit helper (option F). This requires accepting that 32-bit Windows is dropped. Windows 11 is 64-bit only [W11], and Windows 10 support ended 2025-10-14 [W10]. The helper is effort M plus release-pipeline changes. It isolates codec crashes and folds back in-process at the 64-bit move. If neither path is ready, the fo-dicom 4 bridge (option E) can last until the 64-bit move.

**Not recommended:**
- C and D: MS-PL, plus silent memory corruption when the ABI is wrong.
- G1-G5: stale or one-person projects, unproven above 8 bits, and slower.

## 5. What remains unconfirmed

- The real mix of transfer syntaxes in dental files. No count has been made, some dental sources were search snippets only, and the Romexis PDF text could not be read in one check.
- Nothing was built or run. Not checked: the contents of the libjpeg-turbo vc-x86 installer and the OpenJPEG windows-x86 zip, and whether OpenJPH builds with MSVC x86 Release (vcpkg patches 0.32.0 for it; the CI result was not seen).
- Whether the installer's `aka.ms/vs/17` link delivers VC++ 14.51 or newer.
- The pixel accuracy of fo-dicom's managed lossless decoder. The Pt > 0 problem comes from reading the code, and its tests do not compare pixels [FO5-TEST].
- OpenJPEG HTJ2K decoding on real DICOM HTJ2K files.
- 16-bit support of fo-dicom.Codecs for JPEG-LS, JPEG 2000 and HTJ2K, and its official .NET Framework support (a maintainer says it works [FO-1680]).
- OSArchitecture under WOW64 in .NET Framework 4.8 (matters only for D).
- The helper's cost per image (F).
- Licence texts:
  - gnu.org was fetched in one check and was unreachable in two others.
  - In 2008 the FSF list briefly called MS-PL compatible with GPL v3 [FSF-2008].
  - The separate-programs reading of the GPL FAQ needs counsel.
  - The JJ2000 verdict rests on Gentoo metadata.
- FoDicom.PureCodecs: its source, provenance and class names.
- Two managed-decoder details: whether JpegLibrary handles 16-bit lossless, and whether ImageSharp gives back exact 12-bit values.
- How many offices still run 32-bit Windows.
- The OpenJPH "CVE-2026-111151" commit title (the CVE id was not verified).

## Sources

- [FSF] https://www.gnu.org/licenses/license-list.html (#ModifiedBSD, #FreeBSD, #ijg, #ZLib, #Expat, #apache2, #ms-pl)
- [FSF-2008] https://lists.nongnu.org/archive/html/www-commits/2008-02/msg00167.html
- [GPLFAQ] https://www.gnu.org/licenses/gpl-faq.html#MereAggregation (2001 copy: https://lists.gnome.org/archives/foundation-list/2001-December/msg00099.html)
- [FO5-core] https://github.com/fo-dicom/fo-dicom/tree/5.2.6/FO-DICOM.Core/Imaging/Codec
- [FO5-SETUP] https://raw.githubusercontent.com/fo-dicom/fo-dicom/5.2.6/FO-DICOM.Core/Setup.cs (:117, :175-177)
- [FO5-TM] .../5.2.6/FO-DICOM.Core/Imaging/Codec/TranscoderManager.cs (:46, :54-55, :101); DefaultTranscoderManager.cs (:15, :38-50)
- [FO5-LL] .../Imaging/Codec/JpegLossless/DicomJpegLosslessDecoder.cs (:14-16, :57-83)
- [FO5-LLIMPL] .../Imaging/Codec/JpegLossless/DicomJpegLosslessDecoderImpl.cs
- [FO5-READER] .../FO-DICOM.Core/IO/Reader/DicomReader.cs (:278-289)
- [FO5-LOG] https://raw.githubusercontent.com/fo-dicom/fo-dicom/5.2.6/ChangeLog.md (5.1.3)
- [FO5-TEST] https://raw.githubusercontent.com/fo-dicom/fo-dicom/5.2.6/Tests/FO-DICOM.Tests/Imaging/DicomJpegLossessTest.cs
- [FOC-a5] https://api.nuget.org/v3/catalog0/data/2026.09.15.21.52.48/fo-dicom.codecs.6.0.0-alpha5.json
- [FOC-b1] https://api.nuget.org/v3/catalog0/data/2026.07.21.14.17.32/fo-dicom.codecs.6.0.0-beta1.json
- [FOC-SRC] https://github.com/Efferent-Health/fo-dicom.Codecs (master 82ace639: Codec/Platform.cs, Codec/DicomJpegLsCodec.cs, Codec/DicomHtJpeg2000Codec.cs, Codec/DicomJpeg2000Codec.cs, Native/Dicom.Imaging.Codec.Jpeg.cpp, Native/Windows64/build_x64/Dicom.Native.vcxproj, LICENSE.txt)
- [FOC-5167] https://api.github.com/repos/Efferent-Health/fo-dicom.Codecs/contents/Native/Common?ref=5.16.7 ; https://github.com/Efferent-Health/fo-dicom.Codecs/issues/148
- [FOC-README] https://raw.githubusercontent.com/Efferent-Health/fo-dicom.Codecs/5.16.7/README.md
- [FO-1680] https://github.com/fo-dicom/fo-dicom/issues/1680
- [FO4] https://github.com/fo-dicom/fo-dicom/tree/4.0.7/Native (Desktop/DICOM.Native.vcxproj, libijg8/jversion.h, OpenJPEG/opj_config.h)
- [FO4-CAT] https://api.nuget.org/v3/catalog0/data/2021.09.10.09.33.13/fo-dicom.desktop.4.0.8.json
- [FO4-WIKI] https://github.com/fo-dicom/fo-dicom/wiki/Supported-Transfer-Syntaxes
- [LJT-REL] https://api.github.com/repos/libjpeg-turbo/libjpeg-turbo/releases/latest
- [LJT-H] https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/main/src/turbojpeg.h
- [LJT-CL] https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/main/ChangeLog.md (2.1.91, 3.0.90)
- [LJT-LIC] https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/main/LICENSE.md ; README.ijg:143-145
- [OPJ-REL] https://api.github.com/repos/uclouvain/openjpeg/releases/latest
- [OPJ-NEWS] https://raw.githubusercontent.com/uclouvain/openjpeg/master/NEWS.md
- [OPJ-MIXED] https://github.com/uclouvain/openjpeg/blob/v2.5.4/src/lib/openjp2/j2k.c (:11008-11013)
- [OPJ-H] https://github.com/uclouvain/openjpeg/blob/v2.5.4/src/lib/openjp2/openjpeg.h (OPJ_CALLCONV, stream callback typedefs)
- [OPJ-LIC] https://raw.githubusercontent.com/uclouvain/openjpeg/master/LICENSE
- [CLS-CAT] https://api.nuget.org/v3/catalog0/data/2026.10.13.20.58.41/charls.native.4.1.3.json
- [CLS-PKG] https://www.nuget.org/packages/CharLS.Native/4.1.3
- [CLS-SRC] https://github.com/team-charls/charls-native-dotnet (src/Interop.cs, src/FrameInfo.cs, .github/workflows/windows-build.yml:59-74)
- [OJPH] https://github.com/aous72/OpenJPH (README.md:5; pull/395; pull/404; .github/workflows/ccp-workflow.yml:121,137-140,192)
- [VCR] https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist
- [GNSI] https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getnativesysteminfo
- [CSE] https://learn.microsoft.com/en-us/dotnet/api/system.runtime.exceptionservices.handleprocesscorruptedstateexceptionsattribute
- [PS311-K3] https://dicom.nema.org/medical/dicom/current/output/chtml/part11/sect_K.3.html
- [PS33-C76] https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.7.6.html
- [PS35-A44] https://dicom.nema.org/medical/dicom/current/output/chtml/part05/sect_A.4.4.html
- [ROMEXIS] https://planmeca.com/globalassets/planmeca-modernization/software/downloads/dicom_conformance_statement_romexis_6_0.pdf
- [SIDEXIS] https://assets.dentsplysirona.com/flagship/de-de/produkte/bildgebende-systeme/pdfs/SIDEXIS4_DICOM_conformance_statement_V2.0.pdf
- [CSI8] https://www.carestreamdental.com/globalassets/resource-library/software/imaging-software/cs-imaging/v8/english/cs-imaging-dicom-conformance-statement-1.8.pdf
- [CS9600] https://www.carestreamdental.com/globalassets/resource-library/hardware/extraoral-imaging/cs-9600-family/cs-9600/english/cs-9600-dicom-conformance-statement-6.pdf
- [W11] https://www.microsoft.com/en-us/windows/windows-11-specifications
- [W10] https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro
- [OPJNET] https://github.com/notBald/OpenJPEG.net
- [HTDEC] https://raw.githubusercontent.com/uclouvain/openjpeg/master/src/lib/openjp2/ht_dec.c
- [JPEGLIB] https://github.com/yigolden/JpegLibrary (issues/16) ; https://api.nuget.org/v3/registration5-semver1/jpeglibrary/index.json
- [CSCHARLS] https://api.nuget.org/v3/catalog0/data/2018.12.03.07.35.06/cscharls.0.1.1.json
- [IS2] https://api.nuget.org/v3-flatcontainer/sixlabors.imagesharp/2.1.13/sixlabors.imagesharp.nuspec ; https://www.nuget.org/packages/SixLabors.ImageSharp/2.1.13
- [IS3] https://api.nuget.org/v3-flatcontainer/sixlabors.imagesharp/3.1.12/sixlabors.imagesharp.nuspec
- [LJNET] https://github.com/BitMiracle/libjpeg.net/blob/master/LibJpeg/Classic/JpegConstants.cs
- [STB] https://raw.githubusercontent.com/nothings/stb/master/stb_image.h (:23)
- [WIC] https://learn.microsoft.com/en-us/windows/win32/wic/-wic-codec-native-pixel-formats
- [RII] https://github.com/rii-mango/JPEGLosslessDecoder
- [PURE] https://api.nuget.org/v3-flatcontainer/fodicom.purecodecs/0.5.1/fodicom.purecodecs.nuspec
- [JJ2000] https://raw.githubusercontent.com/cinderblocks/CoreJ2K/master/COPYRIGHT-JJ2000-5.1 ; https://mirrors.edge.kernel.org/gentoo-portage/dev-java/jai-imageio-jpeg2000/metadata.xml
- [MCSJ] https://api.nuget.org/v3/registration5-semver1/melville.csj2k/index.json
- [CLSM] https://api.nuget.org/v3/registration5-semver1/charls.managed/index.json