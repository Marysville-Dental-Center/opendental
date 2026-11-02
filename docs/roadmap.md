# Roadmap

Planned work after v0.0, as agreed with the owner. Each item is discussed and scoped with the owner before work starts.

## Next

1. **Library updates (security) and client-side database security**: Newtonsoft.Json 13, MailKit/MimeKit 4 (replacing OpenPop), SSH.NET in
   place of Tamir.SharpSSH, built-in cryptography in place of Org.Mentalis.Security and DiffieHellman, current Google APIs and
   Microsoft.Identity.Client, kept as DLL files in `Required dlls` as today (NuGet comes with the .NET work below). The database connection: TLS required for every connection over the network (the server
   certificate must chain to the office's certificate authority and name the server), connections to the same computer exempt, one shared client
   certificate from the Windows certificate store with a DPAPI-protected password as the fallback, set up by the installer and changed later with
   a small admin-only settings tool; Choose Database is for local testing and debugging only.
2. **Library updates (maintenance)**: WebView2 in place of the Internet Explorer WebBrowser control, a managed Hunspell (no native DLL),
   PDFsharp/MigraDoc 6.5, kept as DLL files in `Required dlls`. PDFsharp 6.5 is AGPL-3.0-only, so the program as distributed is under GPL v3
   terms together with that part. fo-dicom is not updated but removed (owner decision 2026-11-02, option C): the in-house DICOM reader
   (`docs/imaging/dicom-reader.md`) opens the uncompressed, RLE and JPEG Baseline files version 0.0 opened, apart from a few malformed
   ones it now refuses (release notes), and some that 0.0 could not open. JPEG Extended, JPEG Lossless, JPEG-LS and JPEG 2000 files,
   which 0.0 opened through fo-dicom's native codecs, no longer open: their decoders are deferred (Later, below).
3. **Routine maintenance around the practice**: maintenance items the office defines itself (equipment, rooms, systems, compliance routines),
   each with its own schedule, checklist, result fields, assignee and reminder; a due and overdue board; a history of who did what and when.
   The list starts empty.
4. **Inventory tracking**: one storage location; barcode scanning when an order is received and during periodic counts; a "Materials used"
   panel that opens when a procedure is completed, filled with the procedure code's default materials for the assistant to confirm or adjust;
   lot and expiration tracking (soonest-expiring lot first, implant lots recorded on the patient's procedure); low-stock and expiring-soon
   alerts; usage and cost reports.
5. **Storage rename**: `OpenPmsDentalCloud` becomes `OpenPmsDentalStorage`, since it holds only the SFTP storage option.

## Later

### The tooth chart's 3D drawing

Today the chart has three drawing engines in `SparksToothChart`: DirectX 9 through SharpDX (unmaintained since 2019; needs Microsoft's legacy
DirectX End-User Runtime, which a clean Windows does not have), OpenGL through Tao (unmaintained since about 2008) and a flat 2D chart. Open
Dental's newer Sparks3D engine was closed source and was removed, so free text and orthodontic hardware are not drawn.

Plan: finish the WPF 3D chart Open Dental started (`ToothChartWpf`, about 470 lines, written to replace the other engines). WPF draws 3D
through Windows' own DirectX and falls back to software drawing on any PC, so no extra runtime is needed. It reuses the existing tooth models
(`.obj`) and the chart's selection and procedure-colour logic, adds text and orthodontic hardware, and covers the Chart and Treatment Plan
modules, the big chart window, the graphical perio chart and charts printed on sheets. It retires SharpDX, Tao and the DirectX runtime
requirement, and it does not depend on 32-bit. Tests render the chart to images and compare them, to catch visual regressions.

### 32-bit to 64-bit

The program is built as 32-bit today. Decided: go to full 64-bit (no interim large-address-aware flag).

- **TWAIN**: a 64-bit program uses the 64-bit TWAIN data source manager (TWAINDSM 64-bit, free) and needs 64-bit TWAIN data sources. The
  clinic's sensor and camera drivers are a thin 32-bit data source (`.ds`) that starts a separate 32-bit helper program for the device work,
  which uses the sensor vendor's 32-bit libraries. The TWAIN driver project builds 64-bit `.ds` files that start the same 32-bit helpers;
  the helpers and the vendor libraries stay 32-bit, because processes of different bitness can talk to each other. Installed side by side
  with the 32-bit ones.
- **Other 32-bit-only components** (QuickBooks Desktop's SDK, the Schick CDR interface, Topaz if needed) follow the same pattern: a small
  32-bit helper program that the 64-bit program talks to.
- **Other TWAIN devices** (document scanners, a second sensor) need 64-bit drivers; most current devices ship both. For a device with only a
  32-bit driver, an optional 32-bit TWAIN bridge helper loads that driver and passes the images on.
- **Native DLLs**: the library updates above remove most of them (the spell checker becomes managed); `Direct2dWrapper` is built 64-bit from
  our source; WebView2 has 64-bit versions. fo-dicom and its native codecs are gone: the DICOM reader is managed code of our own.
- **Code**: P/Invoke signatures and structures (the TWAIN structures above all) are made pointer-size-safe, with tests that run in both
  bitnesses, while the program still ships as 32-bit.
- **Installer and updates**: 64-bit install folder and prerequisites, and a defined way for an office to move from a 32-bit install to a
  64-bit one, including a mixed set of workstations during the switch.

Prerequisites: the library updates; the WPF tooth chart (SharpDX and Tao gone); the 64-bit `.ds` files from the TWAIN driver project.

### .NET Framework 4.8 to .NET 8 or later

Gains: faster start-up and memory handling, current C#, modern project files and NuGet, monthly security servicing, better high-DPI support
in WinForms. Still Windows-only for WinForms and WPF, which fits: the desktop program stays a Windows program. There is no urgency, because
.NET Framework 4.8 is supported for as long as the Windows versions it ships with.

What has to change: the Middle Tier stays on .NET Framework (owner decision: it shares the business code with the program), so the business
library builds for both frameworks; the generated SOAP web-service clients ("Web References") are regenerated or replaced; the 81 test
projects move from MSTest v1 to MSTest 3; the old-style project files (about 9,600 lines for `OpenPmsDental.csproj`) become SDK-style;
configuration files, Windows services, COM interop and the WebBrowser control need small changes.

Steps, each of which works on its own: SDK-style project files (still .NET Framework); NuGet packages in place of the DLL files in
`Required dlls` (NuGet also works on .NET Framework, so this step can come first on its own); libraries built for
both frameworks; the program and tests on .NET 8 or later; the Middle Tier stays on .NET Framework or is ported later. A smaller step,
.NET Framework 4.8.1, adds native ARM64 support and is only worth it if ARM64 PCs are planned.

### Other planned items

- Open Dental 26.2 features, re-implemented from its public release notes once Open Dental marks 26.2 stable.
- Compressed DICOM decoders, only if such files turn up: JPEG Extended and Lossless, JPEG 2000 and JPEG-LS, through our own thin layer over
  libjpeg-turbo, OpenJPEG and CharLS, plugged into the in-house reader's decoder hook. Today the reader opens uncompressed DICOM, RLE and
  JPEG Baseline, and names the format of any other compressed file. Background: docs/reviews/2026-11-02-dicom-codecs-research.md.
- Server components on Linux, new ones in Rust: a plan first (image storage, backups on the server, publishing releases from the server,
  scheduled jobs). The Middle Tier is the exception and stays on Windows for now.
- The GitHub repository moves to a neutral name.
