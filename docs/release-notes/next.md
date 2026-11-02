# Open PMS - Dental, next version: release notes (unreleased)

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This file collects the notes of the version after 0.0 until it is released; the release renames it to `v<version>.md`.

## Before you install this version

**Offices on version 0.0: uninstall 0.0 first, then install this version with its setup.** There is no in-place upgrade or self-update
from 0.0 to this version (owner decision of 2026-11-02). The 0.0 uninstaller removes the whole program folder, including the
`FreeDentalConfig.xml` its setup wrote, so this version's setup then makes a new install and its connection pages (or silent switches)
set up the connection. Setup's upgrade path for an existing `FreeDentalConfig.xml` stays for the versions after this one.

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
   database password). Start the program there as an administrator; it backs up and updates the database. Publish the release for the
   versions after this one.
3. On every other workstation: uninstall 0.0, then run this version's setup as an administrator, interactive or with `/DBSERVER=`,
   `/DBNAME=`, `/DBUSER=`, `/DBCA=`, `/DBPFX=` (or `/DBTHUMBPRINT=`) and `/DBSECRETSFILE=`.
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
- The installer no longer gives Users modify rights on `FreeDentalConfig.xml`.
