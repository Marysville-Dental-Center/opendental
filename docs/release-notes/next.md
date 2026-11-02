# Open PMS - Dental, next version: release notes (unreleased)

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This file collects the notes of the version after 0.0 until it is released; the release renames it to `v<version>.md`.

Each section below was written by the package that made the change; the sections stand on their own.

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
   only the rights the guide lists (the office database, the backup databases the update makes first, and three server-wide rights);
   the program no longer uses `root`. The guide's list is untested against a real server: try it on a test copy first. The 0.0
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
