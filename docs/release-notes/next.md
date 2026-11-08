# Open PMS - Dental, next version: release notes (unreleased)

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This file collects the notes of the version after 0.0 until it is released; the release renames it to `v<version>.md`.

What this version brings, each in its section below (the roadmap's "Done in the next version", `docs/roadmap.md`):

- encrypted database connections with the program's own database account;
- the security updates of the libraries (SSH.NET, MailKit and MimeKit 4, Newtonsoft.Json 13, MSAL, the Google APIs), with remembered SSH
  host keys;
- the maintenance updates: WebView2 in place of Internet Explorer, a managed spell checker and PDFsharp and MigraDoc 6.5 (these two built
  from their source), and the program's own DICOM reader in place of fo-dicom;
- routine maintenance around the practice, and inventory tracking for materials;
- Open Dental 25.3's settings read under Open Dental's own names, so the office's values carry over at the switch-over, and the eight
  Open Dental 25.3 options this program had been built without;
- the storage library under its new name, and a Database Maintenance check that works again;
- the fixes and changes of the owner's answers of 2026-11-07: a declined PayConnect card is never recorded as paid, an edited X-ray attached
  to a claim is saved, the Insurance History switch-over leaves the frequency code groups alone, NDde.dll is gone (three program links
  that used it work without it), the X-ray capture session is on for new installs, and the archive backup to another server works;
- fixes from the window checks: the PDF viewers close their browsers with their windows, a closing that is held up before Windows ends
  the process ends itself after about 20 seconds and logs where it stood, a deleted image no longer leaves its thumbnail, the log-off
  prompt never logs off while the user is answering it, and a tidier Edit Email Address window;
- Open Dental 26.2: its database schema and the changes the owner chose on 2026-11-07:
  - Appointments: several blockout types in the Advanced Appointment Search, the practice and clinic schedule notes in the Appointments
    module, and appointment estimates brought up to date when an insurance plan is saved;
  - billing and statements: a Statement Options window, a redesigned Statement window, an Electronic Billing window and Billing Defaults
    per clinic;
  - claims and insurance: Status History and Misc next to General in Edit Claim, a default claim form per provider (the billing provider's
    first), a detailed warning when combining insurance plans, and an automation trigger for Do Not Bill to Ins;
  - the Perio Chart: flags for all selected teeth at once, several cells selected in any data row, and the settings Show previous exams and
    Show previous probing values;
  - tools: two Database Maintenance checks, a tool that dates conversion clean-up income transfers on the conversion date, a Fee Tool that
    copies fees from one procedure code to another, and an average in Test Latency;
  - reports: redesigned Display Settings and Security Permissions in Report Setup, and prior uncollected balances and a Pay Plan column in
    the Patient Portion Uncollected report, whose adjustments now count once;
  - vital signs imported from eForms, types for referral commlogs, and an optional automatic switch between a patient's Prophy and Perio
    recall;
  - program links: Allied Star Connect and OrthoComposite added (off; OrthoComposite with its public arguments), Trojan Express Collect and
    Suni retired, and a Partner ID for Medit Link that assigns the cases created from the Chart to the office's lab (the office still sends
    them from Medit Link);
  - Glidewell lab cases, ready but not yet able to send: hidden in Program Links until the program can connect to Glidewell, and no patient
    data is sent without a business associate agreement with Glidewell.

The update adds this program's own tables to the database (13 tables, none of Open Dental's schema: five for routine maintenance, seven for
inventory tracking, one for SSH host keys); they start empty, except the stock ledger's opening rows (Inventory tracking, below). It also
brings the Open Dental tables to the Open Dental 26.2 schema (three new tables, seven new columns; "Open Dental 26.2: the database schema",
below).

## Before you install this version

**Offices on version 0.0: uninstall 0.0 first, then install this version with its setup.** There is no in-place upgrade from 0.0 to
this version (owner decision of 2026-11-02), and no self-update from 0.0 as long as this version is published only after 0.0 is
uninstalled on every workstation (steps 2 and 3 below): 0.0 pulls any newer published build at its next start and installs it over
itself, which would keep 0.0's `FreeDentalConfig.xml` and the modify rights its setup gave every user on it. The 0.0 uninstaller removes
the whole program folder, including that file, so this version's setup then makes a new install and its connection pages (or silent
switches) set up the connection. Setup's upgrade path for an existing `FreeDentalConfig.xml` stays for the versions after this one.

**The office database server must have TLS first, but must not require it yet.** From this version the program connects to a database
server over the network only encrypted, and only when the server's certificate comes from the office's own certificate authority and names
the server. There is no setting to turn this off. Version 0.0 connects without TLS, so the server must keep taking connections without
TLS until every workstation runs this version. Switch over in this order ([docs/db-connection-security.md](../db-connection-security.md),
section 0):

1. On the server: make the office CA, the server certificate and the client certificate, set `ssl_ca`, `ssl_cert`, `ssl_key` and
   `sql_mode=""`, restart MariaDB, and leave `require_secure_transport` OFF. Create the program's own database account, `opmsd`, with
   only the rights the guide lists (the office database, the backup databases the update makes first, and three server-wide rights),
   after removing the rights MariaDB's own setup may give every account on `test` databases (the guide, section 5); the program no
   longer uses `root`. The guide's list was tested on a local MariaDB 10.11 server (the program did everything it does, each right was
   shown to be needed, and every other right was refused), not yet on an office server: try it on a test copy first. The 0.0
   workstations keep working.
2. On the update server: uninstall 0.0, then install this version with its setup, as an administrator. Setup runs the connection settings
   tool (`OpenPmsDentalDbSettings.exe`), which sets up the office CA certificate and imports the client certificate (or stores the
   database password). Start the program there as an administrator; it backs up and updates the database. **Do not publish yet:** when
   the program offers to publish this version (at every start until it is published), answer Cancel, and do not start it with
   `IsSilentUpdate=true`, which publishes by itself when nothing is published yet.
3. On every other workstation: uninstall 0.0, then run this version's setup as an administrator, interactive or with `/DBSERVER=`,
   `/DBNAME=`, `/DBUSER=`, `/DBCA=`, `/DBPFX=` (or `/DBTHUMBPRINT=`) and `/DBSECRETSFILE=`. A workstation that already pulled this version
   (it was published too early) is uninstalled the same way first, through Apps: the "Open PMS - Dental" entry there is still 0.0's. Never
   set such a workstation up only with the connection settings tool, or by running this setup over it: both keep 0.0's
   `FreeDentalConfig.xml` and its rights. Answer Cancel to a publish offer here too; a workstation that says to run the new release on the
   update server first starts once it is published. When 0.0 is uninstalled on every workstation, publish this version on the update
   server with Update Setup > Recopy.
4. When no workstation connects without TLS any more, run `SET GLOBAL require_secure_transport=ON`, add `require_secure_transport=ON` to
   the server's option file, and drop the old database users (the `root` accounts used over the network) or make them `REQUIRE SSL`.

A database on the same computer (`localhost`, `127.0.0.1`, `::1`) needs no certificates: such connections are exempt from the program's
rule. But once `require_secure_transport` is ON, MariaDB refuses connections without TLS from the server computer too, so a program on the
server computer must then connect with `<db-server>` (the name the workstations use), not `localhost`, set up like any workstation.

## Database connection security

- **Encrypted connections only, over the network, for the program's own database account.** `SslMode=VerifyFull` with the office CA
  (`SslCa`), and a client certificate from the computer's certificate store (LocalMachine) or a password. The connection of
  `FreeDentalConfig.xml` and everything that uses it (the reports through ODR and the RDL engine, ServiceManager's test, the settings tool,
  the self-update, the eCW diagnostics) goes through one rule, which refuses before anything is sent. The tool keeps its copy of the office
  CA in the program folder (`ConnectionSettings\office-ca.pem`), which only administrators can change.
- **Read-only and reporting servers as before.** A read-only or reporting server named in Setup connects as it did in 0.0: its own
  server, user and password from the preferences, and its own CA setting (no CA: unencrypted; a CA: encrypted, the certificate checked
  against that CA). It is never sent this computer's client certificate or stored password. Without such a server the reports use the
  program's own connection, under the rule.
- **Not root.** Setup's database user defaults to `opmsd`, the program's own account in the server guide, which lists the narrow rights
  it needs in place of `root`'s. The connection settings tool warns (it does not refuse) when `root` is the user for a server over the
  network.
- **Shortcuts that start the program with a connection.** `ServerName=`, `DatabaseName=` and `MySqlUser=` still work, with this computer's
  office CA and certificate. A database password on the launch command is refused for a server over the network; the password or
  certificate stored on the computer is used. The program's own log-in parameters (`UserName=`, `OdPassword=`) are unchanged.
- **One shared client certificate** on every workstation, imported with its private key not exportable; the database password, stored on
  each computer with Windows DPAPI (machine scope), is the fallback. Per-computer certificates later need no change in the program.
- **The connection settings tool** `OpenPmsDentalDbSettings.exe` (administrators only) changes this computer's connection: server,
  database, user, the office CA, the client certificate or the password, with a Test connection button. The installer uses it (new switches
  `/DBCA=`, `/DBPFX=`, `/DBTHUMBPRINT=`, `/DBSECRETSFILE=`). An interactive new install with a database on the same computer opens it in
  password mode, so that the database password is stored (Cancel when the database user has none).
- **Choose Database is gone from Release builds** (File menu and start-up). When the connection settings are missing or wrong, the program
  says what is wrong and that an administrator should run the tool.
- **Old-style passwords.** A password stored the old way in `FreeDentalConfig.xml` (`Password`, `MySQLPassHash`) is used only for a database
  on the same computer. For a server over the network the program refuses it: store the password again with the tool. New writes never use
  the old format.
- **Connection strings.** A `<ConnectionString>` element in `FreeDentalConfig.xml` is used only for a database on the same computer. One
  that names a server over the network is refused: set the connection up with the tool, which removes the element.
- The installer no longer gives Users modify rights on `FreeDentalConfig.xml`, and every save with the connection settings tool sets the
  file's rights back to the program folder's (any right of Users on the file is removed).
- **A replaced client certificate is removed.** When a save with the tool stops using a client certificate (a new `.pfx`, or the password),
  the tool removes the old one from the computer's store with its private key, but only a certificate it imported itself; one named by
  thumbprint or put there another way stays.
- **Setup exit code 10.** A silent setup that installed the files but could not finish the connection settings (the tool failed, or an
  existing install's settings fail the connection rule) ends with exit code 10, so deployment scripts see it; the setup log says why.

## Library updates for security

The libraries below replace unmaintained ones or versions with known problems. They are still DLL files in `Required dlls`, listed with
their licences in `THIRD-PARTY-NOTICES.txt`.

- **SSH: SSH.NET 2026.0.0** (with Bouncy Castle Cryptography 2.7.0) in place of SharpSSH, which was unmaintained since 2013, and its two
  cryptography libraries (Mentalis.org Security Library and DiffieHellman). It serves every SSH connection of the program: the SFTP image
  storage, the Transworld (TSI) uploads, the Denti-Cal and Mercury clearinghouses and the HL7 service's SFTP mode. Host keys are now
  remembered, and DSA host keys are not supported (next section).
- **E-mail: MailKit and MimeKit 4.18.2** in place of OpenPop.NET (POP3) and MimeKit 2. POP3 signs in with the user name and password as
  before, and with Use SSL it uses TLS from the first byte; MailKit checks the server's certificate and its revocation, and the program
  keeps that check (owner decision of 2026-11-02). The program still adds no background check of POP3 inboxes (owner decision of
  2026-11-02: no POP3 polling).
- **Newtonsoft.Json 13.0.4** in place of 9.
- **Microsoft 365 sign-in: MSAL.NET 4.92.0.** It reads the token caches the older MSAL stored for Microsoft 365 mailboxes, but writes its
  own format from then on (owner decision of 2026-11-02: accepted).
- **Gmail: the Google APIs client libraries 1.78.0**, without the platform shims of the old version.
- **Upgrading.** Setup removes `Tamir.SharpSSH.dll`, `Org.Mentalis.Security.dll`, `DiffieHellman.dll` and `OpenPop.dll` from the program
  folder. A workstation that updates itself through the release channel copies the new files over the old ones and deletes nothing, so it
  keeps such files a release no longer ships; nothing loads them, and the release channel never publishes them again (owner decision of
  2026-11-02: it stays that way).

## SSH servers: their host keys are remembered

Every SSH connection the program makes (the SFTP image storage, the Transworld (TSI) uploads, the Denti-Cal and Mercury clearinghouses and
the HL7 service's SFTP mode) now checks the server's host key against the one the program remembers. Version 0.0 checked no host key at
all. The design is in `docs/specs/ssh-host-keys.md`.

- **First connection.** The first successful connection to a server (host name and port) stores its key type and SHA-256 fingerprint, with
  when and by whom, in the new table `sshhostkey`. The Audit Trail gets an entry under the new type SSH Host Keys.
- **Every later connection must present the same key.** A server that presents another key is refused during the key exchange, before the
  user name, the password or any file is sent. The message names the server and both fingerprints, and the refused key is recorded.
  Every path shows that message, the Transworld (TSI) ones included: Manage, A/R Manager shows it instead of offering to edit the
  Transworld program link (and offers Setup, SSH Host Keys to a Security Admin), sending or updating accounts names the clinics skipped
  for it, and the automated suspend message reports it instead of "not enabled or not setup properly".
- **When a server's key really changed** (a new server, a reinstalled one, a clearinghouse that renewed its key), a user with Security Admin
  opens Setup, SSH Host Keys (or the SSH Host Keys button on the SFTP tab of Setup, Data Paths), compares the new fingerprint with the one the
  server's administrator gives, and clicks Accept Changed Key. Remove forgets a stored key, so the next connection stores the key the server
  presents then. Both are in the Audit Trail.
- **After the update** no key is stored yet: the first connection to each server stores its key. An office that wants to be sure should
  compare the stored fingerprints in Setup, SSH Host Keys with the ones its servers' administrators give.
- **DSA keys** (`ssh-dss`) are not supported, as SSH.NET does not offer them: a server with only a DSA key cannot be reached (owner
  decision of 2026-11-02: accepted).

## WebView2 in place of Internet Explorer

Every window that showed a page with the Internet Explorer control (the wiki and its previews, the e-mail windows and previews, the
clearinghouse's benefits response, the EHR education pages and the CCD view, the eCW panel, the preference details) now uses Microsoft Edge
WebView2, as the windows that had it already did. The design is in `docs/port-notes/LIB-WEB.md`.

- **The WebView2 Runtime is needed.** Windows 11 includes it. Setup never downloads it: when it is missing, setup says so and how an
  administrator installs it from Microsoft. The optional download of the Visual C++ runtime stays as it was (owner decision of 2026-11-02).
- **One message when a browser cannot start.** A missing, damaged or blocked runtime is reported once, with what an administrator can do;
  the payment and PDF windows no longer show a second dialog after it (owner decision of 2026-11-02).
- **Pages are served from memory.** Wiki pages, e-mails (received ones included), their images and the CCD are never written to disk while
  they are shown (owner decision of 2026-11-02), so a patient's e-mail never sits in the temp folder.
- **Received e-mail is safer.** Scripts never run in the pages the program shows itself (wiki pages, e-mails, the CCD), and a received e-mail loads nothing from the web until the
  user clicks Show Images (Internet Explorer loaded web images at once; kept so, owner decision of 2026-11-02). Links open in the default
  browser.
- **Find in page** (Ctrl+F) is back where Internet Explorer had it, with the browser's print preview (Ctrl+P) and zoom (kept, owner decision
  of 2026-11-02).
- **A CCD that came with a stylesheet shows as XML**: the program never runs a received stylesheet (the XML view is enough, owner decision
  of 2026-11-02).
- **The browser's profile** is in `%LOCALAPPDATA%\OpenPMS-Dental\WebView2`, per Windows user. The old profile folder,
  `%LOCALAPPDATA%\WindowsFormsWebView2`, is left alone (owner decision of 2026-11-02).

## The spell checker: WeCantSpell.Hunspell

The spell check of every text box (the red underline, the suggestions, Add to Dictionary) now uses WeCantSpell.Hunspell 7.0.1, a managed
port of Hunspell, in place of NHunspell and its native Hunspell 1.2. The dictionary (en_US) and the office's own words are unchanged. The
port follows Hunspell 1.7.2 with later upstream fixes, so a few words may be judged or suggested differently. It is built by this project
from its GitHub source (a git submodule) and ships under the GNU LGPL 2.1 or later, with no MPL text (owner decision of 2026-11-02). Setup
removes `NHunspell.dll` and `Hunspellx86.dll`.

## PDFsharp and MigraDoc 6.5, built from source

Statements, sheets, reports, treatment plans and claim attachments are made with PDFsharp and MigraDoc 6.5.0 in place of 1.2.

- **Licence.** PDFsharp and MigraDoc 6.5 are under the GNU AGPL, version 3, so the program as distributed is conveyed under the GNU GPL,
  version 3, with them under the AGPL (`NOTICE`, the setup's License Agreement page, Help > About > View Licenses). The program's own source
  files stay "GPL version 2 or later". Every PDF's Producer and Creator properties say "(AGPL license)"; nothing is drawn on the pages, no
  key is needed and no network connection is opened.
- **Built from empira's source** (owner decision of 2026-11-02, route B): the git submodule `third-party/PDFsharp` at tag v6.5.0, compiled
  by this project, with one file replaced by an open one that holds the AGPL licence check, so empira's closed `PdfSharp.License.dll` is
  neither needed nor shipped (`docs/third-party-source-builds.md`).
- **The source archive.** Every release publishes `OpenPmsDental-<version>-source.zip` next to the setup: the program's source at the
  release commit with the source of PDFsharp, MigraDoc and WeCantSpell.Hunspell as built (the submodules). It is the Corresponding Source of
  everything the setup installs.
- **Over a network.** Because the PDFsharp built here is a modified version, the AGPL's network clause applies to any version of the
  program that users reach over a network, a hosted Middle Tier without local changes included: it must offer its users the Corresponding
  Source. The Middle Tier is not shipped; that offer is built when it is first shipped (owner decision of 2026-11-02).
- **Upgrading.** Setup removes `PdfSharp.dll`, `PdfSharp.Charting.dll` and `MigraDoc.Rendering.dll` of version 0.0.

## DICOM: fo-dicom is gone, the program reads DICOM itself

fo-dicom 4.0.7, the DICOM library Open Dental shipped (`Dicom.Core.dll` and `Dicom.Native.dll`, under the Microsoft Public License, which is not compatible with the GNU GPL), is no longer part of the program. Every DICOM image is now read by the program's own reader, written from the DICOM standard (NEMA PS3) alone. The design is in `docs/imaging/dicom-reader.md`.

- **What opens as before.** The DICOM files version 0.0 showed open and show the same, pixel for pixel, apart from the two corrections below and the few files listed under "What version 0.0 showed and this version refuses": the program's own 16-bit X-rays, and files from other programs in Implicit VR Little Endian, Explicit VR Little Endian, Explicit VR Big Endian or Deflated Explicit VR Little Endian, with or without the 128-byte preamble, including files whose header breaks the standard in ways version 0.0 put up with (an Implicit VR data set labelled Explicit VR, blank VR fields in the short or the long layout, a File Meta Information header in Implicit VR, a preamble without the "DICM" marker), gray (MONOCHROME1 and MONOCHROME2, 8, 12 and 16 bits) or RGB color shown in gray, the first frame of multi-frame files, and RLE Lossless and JPEG Baseline compression, progressive JPEG included, also when the JPEG data has stray bytes between its parts or junk after its end. Brightness and contrast, thumbnails, mounts, cropping, printing, e-mail and the 8-bit JPEG copy attached to claims work as before, except that a 12-bit image whose values are all even now gets a usable starting brightness and contrast on import, like any other 12-bit image (version 0.0 gave it a black-and-white one).
- **What opens now and did not before.** Signed images (such as CT exports), bit depths other than 8, 12 and 16 (for example 10 or 14 bits, with the same starting brightness and contrast as the image at 12 bits), YBR_FULL, YBR_FULL_422 and palette color images, color JPEG Baseline images whatever color space they are labelled with, and Encapsulated Uncompressed files. 16-bit color and signed 8-bit images, which version 0.0 showed scrambled, now show correctly (the two corrections).
- **Safeguards.** A file built to keep a DICOM reader busy for minutes is refused with a message instead of freezing the Imaging module: more than a million elements, a deflated file whose data before the pixels inflates past 64 MB, more than 10,000 blank VR fields whose layout can only be told by reading past their value, or a JPEG image of more than 32 scans. Ordinary images come nowhere near these limits (a progressive JPEG has about ten scans), but version 0.0 would have tried to open such files.
- **What version 0.0 showed and this version refuses**, each with a message naming the reason: a JPEG image of more than 32 scans (the safeguard above); a JPEG Baseline image whose JPEG data is not the size the DICOM header gives (version 0.0 showed it cut down, slanted or with made-up rows); JPEG data with no end-of-image marker (version 0.0 froze on some of these and showed others); and, in a deflated file only, a blank VR field holding more than 64 KB whose first bytes happen to look like the start of another field.
- **Compressed formats that open in this version:** RLE Lossless, JPEG Baseline (Process 1, the usual "lossy JPEG" of DICOM) and Encapsulated Uncompressed.
- **Compressed formats that do not open:** JPEG Extended (12-bit JPEG), JPEG Lossless, JPEG-LS, JPEG 2000, High-Throughput JPEG 2000 (HTJ2K), JPEG XL and DICOM video. Version 0.0 opened JPEG Extended, JPEG Lossless, JPEG-LS and JPEG 2000 through fo-dicom's native codecs; this version does not. Opening such an image in the Imaging module says which format it is and that this version does not open it, instead of showing an image; a mount names it next to the mount position. The image is never changed or lost: it can still be imported and stays in the patient's images. Decoders for these formats are deferred (roadmap, Later: only if such files turn up), not part of a coming update. An office that receives such images from another program (for example a CBCT or panoramic unit that exports JPEG 2000) should have that program export uncompressed or JPEG Baseline DICOM.
- **Upgrading.** Setup removes `Dicom.Core.dll` and `Dicom.Native.dll` from the program folder. The Visual C++ runtime is still needed, by `Direct2dWrapper.dll`.
- **Licences.** `THIRD-PARTY-NOTICES.txt` no longer lists fo-dicom, and the program's license list no longer carries the MS-PL.
- **Owner decisions of 2026-11-02.** All of the above stays as described: the message for the compressed formats that do not open, the two
  corrections, progressive JPEG with at most 32 scans, and the refusal of a JPEG image whose size differs from the DICOM header and of JPEG
  data with no end-of-image marker.

## Routine maintenance around the practice

The office defines its own maintenance items (any piece of equipment, a room, a system or a compliance routine) and records each time one is
done. Nothing is built in: the list starts empty. The design is in `docs/specs/routine-maintenance.md`.

- **The board.** Manage module, Maintenance: every item with its status (overdue, due soon, upcoming), due date, assignee, schedule and last
  completion, filtered by assignee or to the user's own items, with Print and Export.
- **Items.** A name and description, an optional link to an item of the Equipment list, a schedule (every N days, weeks, months or years
  from a first due date; on chosen weekdays; or N units after the last completion), a reminder lead time, an assignee (a user or a user
  group), an ordered checklist and result fields (pass/fail, a number with optional limits, text, a date, an attached file). An item with
  history can be hidden, not deleted.
- **Recording.** A completion records the date done, the ticked steps, the results and a note, with the user and the time; an out-of-range
  number or a Fail flags it. A recorded completion is never deleted; its ticks, results and note can be corrected by the user who recorded it
  or by a user with Maintenance Setup, and every correction is in the Audit Trail.
- **History**, per item or for all items, with the checklist and the results in the grid, so a printout or an export shows them.
- **Reminders** through the Alerts menu: Maintenance Due Soon within the lead time and Maintenance Overdue after the due date, to the
  assignee, or, for an item without one, to every user with Maintenance Setup.
- **Permissions.** Maintenance Setup (set up items, correct any completion) and Maintenance Record Completion, in the security tree under
  Manage Module. The update grants both, once, to every user group that has Security Admin; other groups get them from an administrator.
  The board and the history need only the Manage module.

## Inventory tracking for materials

Stock of supplies, built on the existing Supply Inventory windows. **It is off until the office turns it on** (Supply Inventory,
Inventory, Setup), on every existing and every new database; while it is off nothing is deducted. The design is in
`docs/specs/inventory-tracking.md`.

- **Barcode scanning** with a keyboard-wedge scanner when an order is received, during a stock count and in the Materials used panel.
- **Materials used.** When procedures whose codes have default materials (Supply Inventory, Inventory, Default Materials of Procedure Codes)
  are completed, the Materials used panel opens with those materials; the assistant confirms or changes them, and what is confirmed is
  deducted. A cancelled panel, or a completion without a user, deducts nothing: the procedures wait on the Awaiting Materials list.
  Un-completing or deleting a procedure puts back what it used.
- **Lots and expiry.** A supply can be tracked by lot, with a lot number and an expiry date; the lot that expires first is used first. For
  implants the lot is recorded with the patient's procedure; an implant used without a lot number asks for the number on the package, or a
  reason, which goes to the Audit Trail.
- **Receiving** an order with lot numbers and expiry dates, **stock counts** saved line by line as they are counted (any user who may count
  can resume the office's open count on any workstation; posting writes the differences with a reason), and **manual adjustments**.
- **Alerts**: Inventory Low Stock (on hand at or below the supply's new reorder level) and Inventory Expiring (lots within 30 days of their
  expiry date, or past it; the days are a setting, 0 turns it off).
- **Usage reports**: quantity and cost used, by item or by procedure code, for a date range.
- **Permissions.** Inventory Adjust (posting and discarding counts, adjustments, correcting a lot). The first time the office turns tracking
  on, it is granted to every user group that has Supplier Edit.
- **After the update** the stock ledger starts with one opening row for every supply whose on-hand quantity is not 0, so the ledger and the
  stock agree; supplies, orders, suppliers and categories are unchanged.

## Open Dental 25.3's settings under their own names

Settings that Open Dental 24.4 to 25.3 added and that this program had kept under names of its own (payment plans and the global lock date,
the perio "Recession" wording, provider time on the operatory background, the secondary provider's color in a time bar, the Medical EOB
prompt, the automatic split of ERAs by denial codes, the read delay after a write, the blur when switching modules) are now read and saved
under Open Dental's own names (`docs/port-notes/SWMAP.md`). The office's values carry over at the switch-over as they are; adoption changes
no preference row, and `migration-preview` (runbook step 0.2) lists them with what this program shows. On a database that version 0.0 used,
the update moves the values 0.0 saved under its own names to Open Dental's names (fork migration step 2026110731).

## Eight more Open Dental 25.3 options

Each is read and saved under Open Dental's own setting name, so the office's value carries over at the switch-over as it is; a database
without the setting gets the default named here. Changing one makes an Audit Trail entry. What Open Dental means by most of them is not
public: the owner questions "ODOPTS" list what to confirm (`docs/port-notes/ODOPTS.md`).

- Perio Chart: auto Att Ging values at or below a threshold are red, counted and printed (`PerioRedAttGing`; "Numbers in red", Att Ging; none by default).
- Global Security Settings: a switch for domain login in the Change User of signature boxes (`DomainLoginForSigBoxes`; on by default).
- Preferences, Account, Claim Receive: a procedure fee changed in the procedure window, in Edit All or by an appointment's provider change updates the unsent secondary claims (`ClaimUpdateFeeForSecondary`; off by default).
- eForm Setup: import a typed allergy that matches no list entry as Other, or skip it; a skipped one is listed in the form's Audit Trail entry and shown to whoever saves the form (`EFormImportAllergiesAsOther`; on by default).
- eForm Setup: the same for typed problems, skipped ones recorded and shown alike (`EFormImportProblemsAsOther`; on by default).
- Preferences, Treatment Plan, Frequency Limitations: the procedure code Insurance History uses for perio scaling (`InsHistPerioScalingCode`; blank by default).
- Billing Defaults, Send Setup: e-mail new statements when billing creates them, never, after asking or always (`StatementAutoSendEmail`; never by default). Open Dental's window of that name sends Message-to-Pay links instead, a service this program does not have, so an Always carried over from Open Dental asks before e-mailing until Send Setup is saved here (kept so by the owner on 2026-11-07).
- Send Setup: the same for texts (Message-to-Pay by text in Open Dental), which are not sent until a texting provider is chosen (`StatementAutoSendSMS`; never by default).

## The storage library has a new name

The library that holds the SFTP option of the images folder is now `OpenPmsDentalStorage.dll` (`docs/port-notes/RENAME.md`, "Storage
rename"). Nothing stored in the database or in a settings file names it, so nothing changes for the office. Setup deletes the file version
0.0 installed under its old name; the release channel never publishes that file again. Whoever copies the Middle Tier into IIS by hand
deletes the old file from its `bin` folder.

## Database Maintenance: every check runs again

The check `ProcedurelogTpAttachedToClaim` failed on every database with an SQL syntax error, inherited from Open Dental 24.3.22, and
Database Maintenance stops at the first error: running all checks (or fixes) never reached the checks after it in the list. The check
works again, so a full run goes through every check.

## PayConnect: a declined card is a decline, never a payment

PayConnect 2 can answer a charge with success and a status of Declined. The recurring charges recorded such a card as paid, with the full
amount, and sent no failure e-mail (owner question M2-RECUR 6). Now any PayConnect answer to a sale, an authorization or a terminal charge
whose status is not an approval is a decline everywhere a charge is recorded:

- **Recurring charges:** the charge is marked declined (and the card inactivated when "Automatically inactivate declined cards" is on), it
  gets the $0 payment every declined charge gets as the record that it was tried, with no receipt, never a payment of the amount, and with
  the failure e-mail turned on the patient is e-mailed for a Declined status, as for other declines.
- **The payment window:** the charge shows as declined; no receipt is printed and no card is saved from it.

The other card processors' recurring charges were checked and record a decline correctly.

## Claim attachments: an edited X-ray is saved

Attaching an X-ray or other image that was cropped, rotated, flipped or brightness-adjusted in the Imaging module to a claim (Edit Claim's
attachments) listed the attachment but saved no picture file when the images folder is local (owner question CLAIM-DCM 1); with SFTP image
storage it saved the image unedited. The image is now attached as the Imaging module shows it (without drawings), in its own format, and
always saved where the attachment points, locally and in SFTP storage alike. Unedited images, text, Word and PDF files are attached as
they are, also from SFTP storage.

## Switch-over: Insurance History leaves the frequency code groups alone

At the switch-over the ten Insurance History code boxes of Open Dental 25.3 (Preferences, Treatment Plan) become Insurance History rows from
code groups, as in Open Dental 26.1. The owner decided on 2026-11-07 (switch-over question 4) that the existing code groups, which also hold
the frequency and age limitations, stay exactly as they are: each category whose box had codes gets a new code group used only for
Insurance History, "Bitewing History", "Exam History", "Perio Scaling History" (with four quadrant rows) and so on, after every other group,
with the box's codes and its first code as History Proc Code. These groups are in no frequency or age limit and have no fixed category, so
insurance estimates, sheets and frequency checks are unchanged. The old settings are kept. An eligibility response (271) that names a
category, such as bitewings, still finds the history row through the codes of that category's code group.

## DentX, Dexis Integrator and Vipersoft: the program links work without NDde.dll

These three program links send the patient to another program over Windows DDE. They did it through `NDde.dll`, whose own licence does not
fit the GNU GPL (owner question REL-FILES 2). Now they do it through the program's own small DDE client, which sends the same commands, so
they work as before: set them up, turn them on and use their buttons in Program Links as always. Dexis itself is unchanged. **Upgrading:**
setup deletes `NDde.dll` from the program folder; the release channel never publishes it again. `THIRD-PARTY-NOTICES.txt` and the license
list no longer name NDde.

## X-ray capture: on for every new install

After counsel's review of the capture's regulatory position, with no changes, the owner decided on 2026-11-07 that every new install starts
with the program's own X-ray capture session on (`[Session] Enabled=1` is now the default of each computer's capture settings; setup writes
none). It runs on a computer once an administrator has saved Capture settings there (Setup > Imaging > Devices, the device); until then
Mount / Acquire says the capture settings were never saved and works the older way. `[Session] Enabled=0` is still the rollback.

- **16 bits stay the default** (`Allow16Bit=1`): a computer takes 16-bit X-rays once a Test capture passed for its sensor, as before.
- **No patient X-rays before the phantom comparison:** on the same line-pair and step-wedge phantom, at least the same line pairs and the
  same steps must be visible as in the current imaging program's image of that phantom, and left and right must match the phantom's lead
  markers for every sensor size; the dentist signs off against these criteria (`docs/imaging/intraoral-capture-setup.md`, sections 1 and
  12).
- **Twain Radiograph only:** the sensor is set up only as a "Twain Radiograph" device, never as a batch ("Twain Multi") device for patients.

## Backup: the archive backup to another server works

The Backup window's archive tab can back up the database to another server first. That backup stopped at the first table with rows, because
it loaded the rows with `LOAD DATA LOCAL INFILE`, which the database connector refuses unless the connection allows local files (owner
question DB-SEC 2). It now copies the rows with ordinary `INSERT` statements, in the same batches and with the same progress and row checks;
no connection allows local files, and no temporary file of the rows is written. The account typed for the other server needs `CREATE`,
`INSERT` and `SELECT` on the backup database. The program's own database account keeps its three server-wide rights (owner question
DB-SEC 1, answered on 2026-11-07).

## Fixes from the window checks

- **Closing the program.** In one window check the program's process stayed alive after File > Exit, following a long session; its cause
  is not known yet. If the closing is held up before Windows ends the process, the program now ends itself after about 20 seconds and writes
  where it stood into the `Exit` folder of its log (`Logger\Exit` next to the program, or in My Documents when verbose logging is on). A hang
  in Windows' last step of ending the process, while it unloads the program's libraries, is not covered: the program can no longer act
  there. The EOB, amendment and Imaging module PDF viewers now close their browsers with their windows, a second PDF shown while the viewer
  starts no longer reports that the WebView2 Runtime could not start, and the Imaging module's Print category no longer leaves a browser
  running until the program ends.
- **Deleted images.** Deleting an image or a mount in the Imaging module (or anywhere else) also deletes its thumbnail in the patient's
  `Thumbnails` folder; before, a deleted image stayed on disk as its small copy. Thumbnails left by earlier deletes stay until removed by
  hand.
- **Log off and close prompt.** "Are you sure you would like to log off?" (and "...close?") still goes ahead by itself after 15 seconds when
  nobody touches it, but once you move the mouse onto it or press a key it waits for you; X and Esc mean No.
- **Edit Email Address.** The Email Authentication box is as large as what it shows: one line with only Password, and the chosen sign-in's
  box beside the choices when Gmail or Microsoft 365 is used. Every option is still there.

## Open Dental 26.2: the database schema

The update brings the database to the schema of Open Dental 26.2, re-implemented from Open Dental's published 26.2 database documents only
(`docs/port-notes/P262-SCHEMA.md`). It is the base of this version's Open Dental 26.2 features; the features themselves are described in
their own sections.

- **New tables**, created empty: `glidewellacct`, `glidewellmsg` and `labcaseproduct` (lab cases sent to Glidewell).
- **New columns**: the default claim form and the default Glidewell account of a provider (`provider.ClaimFormNum`, `GlidewellAcctNum`, 0
  until set), the failure tracking of API subscriptions (`apisubscription`, four columns; this program has no API event sender, so nothing
  writes them) and the time an insurance blue book row last changed (`insbluebook.DateTStamp`). Existing rows keep every value; the new
  columns start empty, except `insbluebook.DateTStamp`, which holds the time of the update in every existing row.
- **New values** with Open Dental's numbers, so that rows written by Open Dental 26.2 load: the definition category Commlog Referral Types,
  the automation trigger Do Not Bill Insurance Check and the condition Carriers Match, the paint type TextBelow (the tooth chart does not draw
  it yet, so the procedure code windows do not offer it; a procedure code that already has it keeps it), the Perio Chart preference of showing only the current exam's probing, a Transworld payment plan link, and values that belong to
  Open Dental HQ or to services this program does not have (two MariaDB backup alerts, an eConnector alert, an HQ permission, an HQ
  definition link), which stay hidden.
- The schema verifier now checks the database against Open Dental 26.2.0, and `OdSchemaCompat` says 26.2.0 after the update. Open Dental
  databases are still adopted only from 25.3.48 (or this program's 24.3.63 line).
- Everything the documents do not publish (NULL or NOT NULL, defaults, keys, the MyISAM engine of the new tables, the numbers of the Glidewell
  order statuses) follows the conventions of the existing schema and is marked INFERRED in the port note.

## Open Dental 26.2: the Appointments module

The Appointments module changes of Open Dental 26.2, re-implemented from Open Dental's public release notes, manual pages and database
documents (`docs/port-notes/P262-APPT.md`). No database change.

- **Advanced Appointment Search: several blockout types at once.** The Blockout list of the Advanced Appointment Search window (Pinboard,
  Advanced) now takes several blockout types; None alone still searches the providers only. For each date the results list the first time
  slot of every selected blockout type that has one that date, so a date can have several rows; a new Blockout column names the type. The
  window shows up to 15 dates (not rows), and More now starts the day after the last date listed (it used to list that date again). With
  providers selected, every slot is also open for one of them in the same operatory, as before. With one blockout type the results are the
  same as before. The window is a little wider for the new column.
- **Practice and clinic schedule notes in the Appointments module.** When the day (or week) shown has a practice note or holiday, or one
  of the selected clinic, entered in the Edit Day window, the upper left corner of the operatory header shows "Notes". Hovering over it, or
  clicking it, lists them ("Holiday: ..." and "Note: ...", with the clinic, or Headquarters, when clinics are on; with the date in the week
  view). Before, these notes showed only in Schedule Setup.
- **Appointment estimates when an insurance plan is saved.** Saving the insurance plan window (from a patient or from Lists, Insurance
  Plans) now also brings the insurance estimates up to date for every patient the plan covers who has a scheduled appointment today or
  later, not only for the subscriber's own patients as before. It runs for users with the Insurance Plan Edit permission (the only users
  who can change the plan or its benefits); a plan with many such patients takes a moment longer to save.

## Open Dental 26.2: billing and statements

Four changes of Open Dental 26.2, re-implemented from its public release notes only (`docs/port-notes/P262-BILLING.md`). Every statement,
billing and electronic billing setting keeps its value: nothing is converted, and no database row is added by the update.

- **Statement Options window.** The Account module's Statement button still prints the default statement when clicked. Its dropdown
  arrow, which showed a menu, now opens the Statement Options window: the default statement and every entry of the former menu (Walkout,
  Email, Receipt, Invoice, Limited, Limited (Custom), More Options, the Service Date View window), each with a short description.
  Double-click one to run it.
- **The Statement window is redesigned.** The window opened by More Options, by a statement in the Billing List or by a saved statement is
  laid out in boxes (Statement, Invoice, Date Range, Contents, then the notes). It has the same options and works as before.
- **Electronic Billing window.** The electronic billing settings (method, credit cards, vendor codes, the account, user name, password and
  addresses per clinic, PDF and output path) moved from Billing Defaults to their own window. Open it from Billing Defaults (Electronic
  Billing...) or from the Billing List (Defaults).
- **Billing Defaults per clinic.** Billing Defaults is reorganized into Statements (for the whole practice) and Messages. When clinics are
  turned on, the Messages (the General Message, the subject, body, body type and autograph of e-mailed statements, and the invoice note) can
  be set per clinic: pick the clinic and clear "Use the practice defaults for this clinic". Statements e-mailed to a patient and invoices
  then use the patient's clinic's messages; a clinic that keeps the practice defaults, and every clinic while clinics are off, uses the
  practice's. A clinic's General Message is the same one Billing Options saves with the clinic's filter defaults.
- **A clinic keeps following the practice for the messages it has not changed.** Every clinic that saved Billing Options defaults before
  this update has its own General Message and uses the practice's other messages. Changing that clinic's General Message (or any one
  message) in Billing Defaults saves only what you changed: the clinic keeps the practice's e-mail subject, body, body type, autograph and
  invoice note, and later changes to them still reach it. A clinic that you take off the practice defaults by clearing "Use the practice
  defaults for this clinic" starts from a copy of all of them.
- **Billing Options saves only the clinic you selected.** Save Defaults in Billing Options, with one clinic selected, now writes that
  clinic's defaults and nothing of any other clinic. A clinic with its own messages in Billing Defaults but no saved Billing Options filters
  keeps following the practice filter defaults, and a clinic set to use the practice defaults in Billing Defaults stays on them. Billing
  Options shows the selected clinic's own General Message, also when the window opens.

## Open Dental 26.2: claims and insurance

Four changes of Open Dental 26.2 (its release notes, Account Module and Insurance), written from Open Dental's public release notes and
database documents only (`docs/port-notes/P262-CLAIMS.md`). Open Dental's 26.2 manual is not public, so where the documents say only what
changed and not how, this program's own design is used and marked INFERRED in the port note. No database step: the 26.2 schema already has
the provider column and the automation values.

- **Edit Claim: Status History and Misc next to General.** The tabs are now General, Status History, Misc, Attachments, Medical (and
  Canadian), so claim tracking and the resend fields (Correction Type, Original Reference Num) are one click away. Nothing on the tabs
  changed. Open Dental moved them too; where they went in its window is not published.
- **A default claim form per provider.** Edit Provider has a Default Claim Form. New claims and preauthorizations take the billing
  provider's default claim form, or, when the billing provider has none, the treating provider's, instead of the insurance plan's (which
  starts as the practice default). "None" on both keeps the plan's, as before.
  Existing claims keep their claim form, and the claim form can still be changed on each claim. A claim form that is a provider's default
  cannot be deleted; the message names the providers.
- **Combining insurance plans: a detailed warning.** Combine Insurance Plans now lists every field of the other plans that does not match the
  plan you selected (carrier, employer, group, fee schedules, claim form, filing code, plan type and every other plan setting), with the
  value kept and the value lost, and whether a plan's benefits differ (they are replaced by the selected plan's). You can still cancel. When
  every plan matches, the earlier short warning shows.
- **A new automation trigger: Do Not Bill to Ins.** `DoNotBillInsuranceCheck` fires when Do Not Bill to Ins is checked in the procedure
  edit window and the procedure changes to not bill insurance (unchecking does not fire it). Its own condition, Carriers Match, picks a
  carrier from the list and holds when one of the patient's insurance plans is with that carrier; several Carriers Match conditions on one
  automation mean any one of the carriers. The condition is offered only for this trigger, and an automation that has it cannot be saved
  with another trigger. Combining carriers (Lists > Carriers > Combine) moves these conditions to the carrier that is kept, so the
  automation keeps firing.

## Open Dental 26.2: the Perio Chart

The Perio Chart changes of Open Dental 26.2, re-implemented from Open Dental's public release notes, Perio Chart pages and database
documents (`docs/port-notes/P262-PERIO.md`). No database change.

- **Flags for all selected teeth.** Select teeth by clicking their numbers (as for SkipTeeth), then click Bleeding, Suppuration, Plaque or
  Calculus (or press B or the space bar, S, P, C): the flag goes on all six sites of every selected tooth of the selected exam. The teeth
  stay selected, so several flags can be added in turn; a site keeps its other flags. Clicking a flag that every one of those sites already
  has takes it off them again. Skipped teeth get no flags this way (click a skipped tooth's gray cell, as before). Clicking or dragging over
  cells unselects the teeth.
- **Several cells in any data row.** Dragging over cells now selects several cells in the Mobility, Furc, Ging Marg and MGJ rows too, not
  only in the probing row; one number fills them all and Delete clears them all. A drag stays in the kind of row where it started; auto CAL,
  auto Att Ging and earlier exams' rows are never selected, and skipped teeth are passed over.
- **Show previous exams and Show previous probing values.** These two check boxes replace Show current exam only, per user. Show previous
  exams unchecked shows the selected exam only, in every row: no rows for earlier exams and no grayed earlier values in the Mobility, Furc,
  Ging Marg and MGJ rows. Show previous probing values unchecked hides only the earlier exams' probing rows; the other rows still show the
  grayed earlier values (what Show current exam only did). Both are checked for a user who never changed them. A user who had Show current
  exam only checked finds Show previous exams unchecked, which now also hides the grayed earlier values; to see those again, check Show
  previous exams and uncheck Show previous probing values.

## Open Dental 26.2: tools

Five tools of Open Dental 26.2, re-implemented from Open Dental's public release notes and database documents only
(`docs/port-notes/P262-TOOLS.md`). Open Dental does not publish how its versions work, so the rules below are this program's own and marked
INFERRED in the port note. No database change.

- **Database Maintenance: `PaymentIncomeTransferIssues`.** A new check for income transfers (payments without a payment type) that do not
  balance. Fix gives no payment type to $0 payments that were saved with a payment type although their splits only move money (they total
  $0), so that aging and the other income transfer tools treat them as the income transfers they are; no amount changes, and each change is
  in the DBM log. Payments on a deposit, card and online payments, recurring charges and payments linked to the accounting module are left
  alone. Income transfers with an amount, or whose splits do not total $0, are listed for a manual fix (double click the check; Explain says
  how).
- **Database Maintenance: `InsVerifyRedundantEntries`.** A new check for insurance verifications kept twice for the same insurance plan or
  patient plan. Fix keeps the latest verification and moves the others into the verification history, with every field; nothing is lost.
  When two have the same date, the program now always shows the newest, the one Fix keeps.
- **Database Maintenance, Tools tab: Conversion Payment Dates.** After a data conversion, the income transfers that clean up the converted
  history belong on the conversion date, so that they do not change the reports of later days. Enter the conversion date and click Find: the
  window lists the income transfers dated after it (or without a date) and selects the ones that only move money of entries dated on or
  before the conversion date. Change Dates dates the selected transfers and their splits on the conversion date, after a confirmation; it
  needs Payment Edit for that date, and each change is in the audit trail and the DBM log. The moved splits keep valid integrity hashes, so
  the account does not show them as changed outside the program. It runs only when you open it, never with Run All.
- **Fee Tools: Copy Code.** A new tab copies the fees of one procedure code to another, in every fee schedule or only in the one under Select
  Fees. In each fee schedule where the first code has fees, the second code gets the same fees for every clinic, provider and effective
  date, and its other fees there are deleted; fee schedules where the first code has no fee are not changed. Every change is in the audit
  trail (Procedure Fee Edit and Fee Edit). Users restricted to clinics cannot use it.
- **Test Latency: average latency.** Tools, Misc Tools, Test Latency has a third test that runs the small latency query several times (10
  unless you change it, up to 1000) and shows the average, the fastest and the slowest round trip in milliseconds.

## Open Dental 26.2: reports

Four Reports changes of Open Dental 26.2, re-implemented from its public release notes only (`docs/port-notes/P262-REPORTS.md`). Every
report setting and permission keeps its value: nothing is converted, and no database row is added by the update.

- **Report Setup: Display Settings redesigned.** Setup, Reports (or Setup in the Reports window), Display Settings: one list of every report,
  under a row per category (Production & Income, Daily, Monthly, Lists, Public Health) in the order of the Reports window, instead of a small
  grid per category. Each report shows its display name (click to rename it), its internal name, and its Favorite and Hidden boxes; Up and
  Down move the selected report within its category. Up and Down now also work when a category's stored order has gaps.
- **Report Setup: Security Permissions redesigned.** The same list with the Allowed box per report for the selected user group, Set All and
  Set None, and a count of the reports the group may run. Hidden reports are marked "(hidden)", as before. The report permissions window
  of the Central Enterprise Management Tool has the same list. A group is given "every report" only when every listed report is allowed
  (before, a leftover permission of a report no longer listed could count for a missing one).
- **Patient Portion Uncollected: prior uncollected balances.** Check Show prior uncollected balances (then Refresh) to also list, for every
  patient with a procedure completed in the date range, the procedures completed before the From date whose patient portion is still not
  collected. They are shaded and listed first; the printout lists them in their own section, "Prior Uncollected Balances", with its own
  totals. The box starts unchecked each time the report opens.
- **Patient Portion Uncollected: Pay Plan column.** The amount of each procedure put on a payment plan: the treatment credits of a
  patient payment plan, or the amount of a dynamic payment plan (its override, or what remains of the procedure when it has none).
  Insurance payment plans do not count. The Uncollected column is unchanged: payments made through a payment plan count in Patient Paid, as
  before. The report window is a little wider for the new column.
- **Patient Portion Uncollected: each adjustment counts once.** An adjustment with several payment splits attached to it counted its
  amount once per split (since Open Dental 26.1). It now counts once, less those splits, in the rows of the date range and in the prior
  rows, so the Adjustment and Uncollected amounts of such procedures are right and differ from before.

## Open Dental 26.2: vital signs from eForms, referral commlog types, automatic Prophy and Perio recall

Three of Open Dental 26.2's smaller changes, re-implemented from Open Dental's public release notes and database documents
(`docs/port-notes/P262-MISC.md`). No database change; no preference or definition is added by the update.

- **Vital signs can be imported from eForms.** A text field of an eForm can now be linked (Database Link) to the patient's height (inches),
  weight (pounds), blood pressure (systolic and diastolic, mmHg) or pulse (beats per minute). The fields start empty. Saving a form checks that
  each answer is a number in a plausible range; height may also be written in feet and inches (5'10"), and a unit after the number (lbs, in,
  mmHg, bpm) is accepted. The eForm Import window shows a "Vital Signs" section with the patient's latest values and the answers; importing
  adds one vital sign entry (Medical window, Vital Signs tab) dated the day the form was submitted, and the Audit Trail records it. Vital signs
  are never imported automatically (Auto Import), and importing the same form again does not add the same entry twice.
- **Commlog Referral Types.** The new definition category (Setup, Definitions, Commlog Referral Types) holds the types of the commlog entries
  made for a referral source (Edit Referral, Add Comm), as Commlog Types do for patient commlogs. The Communication Item for Referral window
  has a Type list; a new entry starts with the first type that is not hidden, or none when the office has added no type (no type ships with
  the program). The referral's commlog list shows the type, in the type's color. Entries made before this version keep no type.
- **Automatic switch between the Prophy and Perio recall.** A new setting in Setup, Recall Types, off by default: "Automatically switch
  between the Prophy and Perio recall based on completed procedures". When it is on, completing a procedure that triggers the Perio recall
  type changes a patient's Prophy recall to Perio, and completing a procedure that triggers the Prophy type changes a Perio recall to Prophy,
  as the Set Perio and Set Prophy buttons do (the interval becomes the new type's default); a patient's first recall is Perio when the first
  trigger procedure is a perio one. Only a procedure newer than the recall's previous date switches it, so a type set by hand stays until the
  next trigger procedure is completed, and turning the setting on switches nobody for procedures completed before. Nothing switches when the
  newest date has both kinds of procedure, when the recall is disabled, when the patient has both a Prophy and a Perio recall, or when the
  Prophy and Perio types do not both have triggers. Each switch is recorded in the Audit Trail.

## Open Dental 26.2: program links

Built from Open Dental's public 26.2 release notes and setup pages only (`docs/port-notes/P262-LINKS.md`). New links appear in Setup >
Program Links the first time it is opened, disabled; their buttons show only once the office enables them, and both stay off unless the
office uses the program (owner decision of 2026-11-07). Only Allied Star Connect's input is unpublished: Alliedstar does not publish the
file layout or any arguments Connect reads, so an office that uses it asks Alliedstar. OrthoComposite's arguments `--lastname`,
`--firstname` and `--dob` are public, in its Microsoft Store listing (https://apps.microsoft.com/detail/9p2mp6p71zk9), and ship as the
link's default.

- **Allied Star Connect** (new): a Chart module button that writes the selected patient (id, name, birthdate, gender and the first email
  address) to the .json file in the link's Info file path, then starts Connect (default path `C:\Program Files\Alliedstar\Connect\Connect.exe`).
  The folders of the Info file path must already exist; the program does not create them and says so. Without a patient, an old info file
  is removed first, so Connect can never open the previous patient. The file is removed when the program closes. The layout of the file is
  this program's own, because Alliedstar has not published one: if Connect does not open the patient, ask Alliedstar which layout it reads.
- **OrthoComposite** (new): a Chart module button that starts OrthoComposite with the patient's first name, last name and birthdate
  (MM/dd/yyyy). The link's Patient arguments property holds OrthoComposite's own public arguments, `--lastname "[LName]" --firstname
  "[FName]" --dob "[Birthdate]"`, which open its patient list with the search filled in (an office may change them); only the tokens [FName],
  [LName] and [Birthdate] are filled in, so no other patient data can be sent. Enter where OrthoComposite is in Path of file to open (a
  name without a folder, or a `shell:` target, is passed to Windows unchecked).
- **OrthoComposite's public arguments** (fork migration step 2026110801): a link set up before this version with blank Patient arguments gets
  OrthoComposite's arguments; arguments an office typed are never changed. The link's built-in note is updated only where the link then
  sends those arguments; a link with the office's own arguments keeps its note.
- **Medit Link**: a new Partner ID property (blank, sent nowhere) assigns every case created with the Chart's Medit Link button to that
  partner lab in Medit Link, as Medit documents (for example Glidewell, partner ID 7307395). The patient's values filled into the link's
  arguments lose any double quotes, backslashes and control characters, so a name can no longer break the arguments or add a key such as
  PartnerID; ordinary names are sent exactly as before.
- **Trojan Express Collect** (retired): the service is no longer offered. The link is listed as "(discontinued)", cannot be enabled or
  started, and the update disables it and removes its buttons (fork migration step 2026110781); its settings stay in the database. Nearly
  every database has one such button on the Account module toolbar (Open Dental's 2019 update added it for every office, hidden while the
  link was off), so the update log normally shows one button removed. The Trojan benefit link is not affected.
- **Suni** (removed): Suni's direct capture was already removed in version 0.0; nothing is left that uses the Suni settings. The database
  columns stay, as in Open Dental 26.2.
- **Ez3D-i** stays (owner decision), although Open Dental 26.2 removed it.

## Open Dental 26.2: Glidewell lab cases

Lab cases are to be sent to the Glidewell dental lab from the Edit Lab Case window, for a scan from an iTero or Medit scanner, with the
office's own Glidewell account, directly to Glidewell (`docs/port-notes/P262-GLIDEWELL.md`).

**This version cannot send yet, and the link is hidden.** Glidewell has not published the interface practice management programs use, so the
program contains no connection to Glidewell and nothing leaves the practice. Until the connection exists, Setup, Program Links does not list
Glidewell lab cases, so it cannot be set up or turned on, and the Edit Lab Case window is unchanged (owner decision of 2026-11-07). The office
itself asks Glidewell for API access. Once the connection is added, it is off until the office turns it on in Setup, Program Links, Glidewell
lab cases, which needs a Glidewell account with its account number and API key, and **no patient data is sent until the setup records that a
signed business associate agreement with Glidewell is on file**, as for the postcard service; status checks wait for it too. Everything
around the connection is in place:

- **Accounts** (Setup, Program Links, Glidewell lab cases): the account number, the API key (stored encrypted, never shown again, changed only
  by a user with Manage High Security Program Properties) and the providers whose lab cases use the account by default. An API key that
  Open Dental stored cannot be read and has to be entered again. An API key longer than 127 characters is refused with a message, since it
  would not fit in the database once encrypted; nothing is saved.
- **Edit Lab Case window**: a Glidewell group with the account, the scanner, the lab case's products (material, shade, teeth, notes; saved with
  the lab case), Send to Glidewell, and for a sent case Check Status and Details (status, tracking number and carrier).
- **What would be sent**, listed before sending: the patient's name, patient number, gender and birthdate, the provider's number and license
  number, and the lab case's number, instructions and products.
- A lab case sent to Glidewell cannot be deleted; its products are deleted with an unsent lab case, and they follow the lab case when two
  patients are merged.
- Lab cases an Open Dental database already sent to Glidewell keep their stored status and details, which the Details button shows.
- **Teeth of a product**: filled from the procedures of the lab case's appointment (or planned appointment), formatted as the Chart shows a
  tooth range; "From Procedures" fills them again, and what the user types is kept.
- **Sending a case today**: the setup window lists Glidewell's own ways to send a case, in Glidewell's words, which need no connection from
  this program: the 3Shape TRIOS, iTero and Medit Link scanner connections; for Alliedstar, CEREC, DEXIS, Heron IOS and Planmeca, a pointer
  to the steps on Glidewell's Submit a Digital Case page; STL files from any other scanner uploaded through Glidewell's My Account portal
  (an office not registered for it calls Glidewell at 800-854-7256); and the GL-421 paper Rx.
