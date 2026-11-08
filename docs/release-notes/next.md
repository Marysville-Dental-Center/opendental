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
- the storage library under its new name, and a Database Maintenance check that works again;
- the database schema of Open Dental 26.2;
- Open Dental 26.2's Perio Chart changes: flags for all selected teeth at once, several cells selected in any data row, and the settings
  Show previous exams and Show previous probing values.

The update adds this program's own tables to the database (13 tables, none of Open Dental's schema: five for routine maintenance, seven for
inventory tracking, one for SSH host keys); they start empty, except the stock ledger's opening rows (Inventory tracking, below). It also
brings the Open Dental tables to the Open Dental 26.2 schema (three new tables, seven new columns; Open Dental 26.2 database schema, below).

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

## The storage library has a new name

The library that holds the SFTP option of the images folder is now `OpenPmsDentalStorage.dll` (`docs/port-notes/RENAME.md`, "Storage
rename"). Nothing stored in the database or in a settings file names it, so nothing changes for the office. Setup deletes the file version
0.0 installed under its old name; the release channel never publishes that file again. Whoever copies the Middle Tier into IIS by hand
deletes the old file from its `bin` folder.

## Database Maintenance: every check runs again

The check `ProcedurelogTpAttachedToClaim` failed on every database with an SQL syntax error, inherited from Open Dental 24.3.22, and
Database Maintenance stops at the first error: running all checks (or fixes) never reached the checks after it in the list. The check
works again, so a full run goes through every check.

## Open Dental 26.2 database schema

The update brings the database to the schema of Open Dental 26.2, re-implemented from Open Dental's published 26.2 database documents only
(`docs/port-notes/P262-SCHEMA.md`). It is the base of this version's Open Dental 26.2 features; the features themselves are described in
their own sections.

- **New tables**, created empty: `glidewellacct`, `glidewellmsg` and `labcaseproduct` (lab cases sent to Glidewell).
- **New columns**: the default claim form and the default Glidewell account of a provider (`provider.ClaimFormNum`, `GlidewellAcctNum`, 0
  until set), the failure tracking of API subscriptions (`apisubscription`, four columns; this program has no API event sender, so nothing
  writes them) and the time an insurance blue book row last changed (`insbluebook.DateTStamp`). Existing rows keep every value; the new
  columns start empty, except `insbluebook.DateTStamp`, which holds the time of the update in every existing row.
- **New values** with Open Dental's numbers, so that rows written by Open Dental 26.2 load: the definition category Commlog Referral Types,
  the automation trigger Do Not Bill Insurance Check and the condition Carriers Match, the paint type TextBelow (the tooth chart draws nothing
  for it), the Perio Chart preference of showing only the current exam's probing, a Transworld payment plan link, and values that belong to
  Open Dental HQ or to services this program does not have (two MariaDB backup alerts, an eConnector alert, an HQ permission, an HQ
  definition link), which stay hidden.
- The schema verifier now checks the database against Open Dental 26.2.0, and `OdSchemaCompat` says 26.2.0 after the update. Open Dental
  databases are still adopted only from 25.3.48 (or this program's 24.3.63 line).
- Everything the documents do not publish (NULL or NOT NULL, defaults, keys, the MyISAM engine of the new tables, the numbers of the Glidewell
  order statuses) follows the conventions of the existing schema and is marked INFERRED in the port note.

## Perio Chart (Open Dental 26.2)

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
