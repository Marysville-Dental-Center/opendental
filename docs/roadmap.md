# Roadmap

Planned work after v0.0, as agreed with the owner. Each item is discussed and scoped with the owner before work starts.

## Next

Scheduled with the owner on 2026-11-07, in this order (the Open Dental 26.2 features, first on that list, are done; see below):

1. **.NET 10** for everything, the Middle Tier included, now that the 26.2 work has landed, because the move changes every project file. Its
   last stage is the switch to 64-bit, once the TWAIN driver project's 64-bit data sources are ready. See below.
2. **The tooth chart's 3D drawing** on WPF 3D with Helix Toolkit 3.x, after .NET 10, with Open Dental 26.2's extraction graphic and its
   "text below the chart" paint type. See below.
3. **Compressed DICOM decoders**, parked until such files turn up. See below.

Not scheduled yet: the next release, the office switch-over, the server components on Linux, the GitHub repository move, and Open Dental
26.3.

The owner questions of the work done in the next version (the SSH host key defaults, the server-wide rights the backup needs, and the
Backup window's backup to another server) were answered on 2026-11-07, and every other open question was accepted at today's behaviour
("skip the rest"). The questions of the Open Dental 26.2 work were answered the same day ("all as recommended"). `docs/owner-questions.md`
records the answers.

## Done in the next version

Built for the version after 0.0; what each one changes is in its release notes, `docs/release-notes/next.md`.

1. **Library updates (security) and client-side database security**: the tier 1 library updates (SSH.NET, MailKit/MimeKit 4, Newtonsoft.Json
   13, MSAL and the Google APIs) with remembered SSH host keys, and encrypted database connections with the program's own database account.
2. **Library updates (maintenance)**: WebView2 in place of the Internet Explorer control, the managed WeCantSpell.Hunspell spell checker,
   PDFsharp/MigraDoc 6.5 (the spell checker and PDFsharp/MigraDoc built from their source), and the in-house DICOM reader in place of
   fo-dicom.
3. **Routine maintenance around the practice**.
4. **Inventory tracking**.
5. **Storage rename**: `OpenPmsDentalCloud` becomes `OpenPmsDentalStorage`, since it holds only the SFTP storage option.
6. **Fixes before the switch-over, from the owner's answers of 2026-11-07**: a declined PayConnect 2 charge is a decline, an edited X-ray
   attached to a claim is saved, the Insurance History step leaves the frequency code groups unchanged, the DentX, Dexis Integrator and
   Vipersoft links and NDde.dll are removed, the capture session is on for new installs, and the backup to another server works.
7. **Open Dental 26.2 features**: the changes the owner chose on 2026-11-07, on Open Dental's 26.2.0 database schema (appointments, billing
   and statements, claims and insurance, the perio chart, tools, reports, eForms and recalls, program links, and Glidewell lab cases without
   a connection to Glidewell yet), with Open Dental 25.3's settings read under their own names and its eight options built. The extraction
   graphic and the "text below the chart" paint type stay with the tooth chart.

## Plans

### .NET Framework 4.8 to .NET 10

Target: .NET 10, the long-term release, supported until November 14, 2028 (.NET 8 and .NET 9 support ends on November 10, 2026; .NET 12, the
next long-term release, is expected in late 2027). Gains: faster start-up and memory handling, current C#, modern project files and NuGet,
monthly security servicing, better high-DPI support in WinForms, and current libraries (Helix Toolkit 3.x for the tooth chart among them).
Still Windows-only for WinForms and WPF, which fits: the desktop program stays a Windows program.

Decided with the owner on 2026-11-07:

- **Everything moves**, with no project left on .NET Framework and nothing built for both. That includes the Middle Tier: its classic
  ASP.NET web service (one method that hands each request to the business code) becomes a small ASP.NET Core service over HTTPS on
  Windows; the program reaches it with a plain HTTPS call instead of the generated SOAP proxy. The 4,458 business methods that can run
  on the Middle Tier keep their messages unchanged. A Linux Middle Tier is deferred to the Linux server plan (the business library would
  first need its Windows-only parts moved out: pictures, fonts and drawing, WinForms, WPF, the registry).
- **Deployment**: framework-dependent; the installer installs the .NET 10 Desktop Runtime, so the updates stored in the database stay small.
- **Libraries**: NuGet packages replace the DLL files in `Required dlls`, from a local package folder the build lane restores from offline,
  with lock files; each batch of downloads is approved by the owner. The generated SOAP clients of services the fork removed are deleted and
  the rest regenerated or replaced; any BinaryFormatter use is replaced; tests built on AppDomains are rewritten; COM interop stays.
- **Stages**, each ending with a full test run: (1) SDK-style project files, still on .NET Framework 4.8; (2) NuGet packages; (3) the
  program, the business library, the tests and the Middle Tier on .NET 10, still 32-bit; (4) MSTest v1 to MSTest 3; (5) 64-bit, once the
  TWAIN driver project's 64-bit data sources are ready (see "32-bit to 64-bit").

A read-only survey of every project, library, API and tool the move touches is under way; the detailed stage plan and any further owner
questions follow from it.

### The tooth chart's 3D drawing

Today the chart has three drawing engines in `SparksToothChart`: DirectX 9 through SharpDX (unmaintained since 2019; needs Microsoft's legacy
DirectX End-User Runtime, which a clean Windows does not have), OpenGL through Tao (unmaintained since about 2008) and a flat 2D chart. Open
Dental's newer Sparks3D engine was closed source and was removed, so free text and orthodontic hardware are not drawn.

Plan (owner, 2026-11-07): a WPF 3D chart built with Helix Toolkit 3.x (MIT, the most widely used 3D toolkit for .NET desktop programs), on
.NET 10, replacing the DirectX and OpenGL engines; the flat 2D chart stays as an option. WPF draws 3D through Windows' own DirectX and falls
back to software drawing on any PC, so no extra runtime is needed. It reuses the existing tooth models (`.obj`) and the chart's selection and
procedure-colour logic, matches today's 3D look, adds free text, orthodontic hardware, Open Dental 26.2's extraction graphic and its "text
below the chart" paint type, and covers the Chart and Treatment Plan modules, the big chart window, the graphical perio chart and charts
printed on sheets. A saved DirectX or OpenGL choice moves to the new chart. It retires SharpDX, Tao and the DirectX runtime requirement, and
it does not depend on 32-bit. Tests render the chart to images and compare them, and the owner checks a gallery of rendered charts before it
reaches main.

### 32-bit to 64-bit

The program is built as 32-bit today. Decided: go to full 64-bit (no interim large-address-aware flag), as the last stage of the .NET 10
move (owner, 2026-11-07), once the TWAIN driver project's 64-bit data sources are ready.

- **TWAIN**: a 64-bit program uses the 64-bit TWAIN data source manager (TWAINDSM 64-bit, free) and needs 64-bit TWAIN data sources. The
  clinic's sensor and camera drivers are a thin 32-bit data source (`.ds`) that starts a separate 32-bit helper program for the device work,
  which uses the sensor vendor's 32-bit libraries. The TWAIN driver project builds 64-bit `.ds` files that start the same 32-bit helpers;
  the helpers and the vendor libraries stay 32-bit, because processes of different bitness can talk to each other (the driver project
  confirmed on 2026-11-07 that nothing in its design stops this). Installed side by side with the 32-bit ones. Decided (owner,
  2026-11-07): this program's installer installs the 64-bit TWAINDSM; the driver package only checks that it is there.
- **Other 32-bit-only components** (QuickBooks Desktop's SDK, the Schick CDR interface, Topaz): the office uses none of them (owner,
  2026-11-07), so they are switched off in the 64-bit build. If an office needs one later, it gets the same pattern: a small 32-bit helper
  program that the 64-bit program talks to.
- **Other TWAIN devices** (document scanners, a second sensor) need 64-bit drivers; most current devices ship both. For a device with only a
  32-bit driver, an optional 32-bit TWAIN bridge helper loads that driver and passes the images on.
- **Native DLLs**: the library updates removed most of them (the spell checker is managed); `Direct2dWrapper` is built 64-bit from our
  source; WebView2 has 64-bit versions. fo-dicom and its native codecs are gone: the DICOM reader is managed code of our own.
- **Code**: P/Invoke signatures and structures (the TWAIN structures above all) are made pointer-size-safe, with tests that run in both
  bitnesses.
- **Installer and updates**: 64-bit install folder and prerequisites, and a defined way for an office to move from a 32-bit install to a
  64-bit one, including a mixed set of workstations during the switch.

Prerequisites: .NET 10 (stages 1 to 4) and the 64-bit `.ds` files from the TWAIN driver project. Until the WPF tooth chart replaces them,
the old DirectX and OpenGL chart engines must work in 64-bit, or the chart falls back to its 2D drawing.

### Compressed DICOM decoders

Only if such files turn up: JPEG Extended and Lossless, JPEG 2000 and JPEG-LS, through our own thin layer over libjpeg-turbo, OpenJPEG and
CharLS, plugged into the in-house reader's decoder hook. Today the reader opens uncompressed DICOM, RLE and JPEG Baseline, and names the
format of any other compressed file. Background: docs/reviews/2026-11-02-dicom-codecs-research.md.

### Not scheduled yet

- The next release and the office switch-over (`docs/adoption-runbook.md`).
- Server components on Linux, new ones in Rust: a plan first (image storage, backups on the server, publishing releases from the server,
  scheduled jobs). The Middle Tier is the exception: it moves to .NET 10 on Windows now, and a Linux Middle Tier waits for this plan (owner,
  2026-11-07: "Defer Linux middle tier").
- The GitHub repository moves to a neutral name.
- Open Dental 26.3, once Open Dental marks it stable.
