# Open PMS - Dental, next version: release notes (unreleased)

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This file collects the notes of the version after 0.0 until it is released; the release renames it to `v<version>.md`.

## Before you install or self-update to this version

**The office database server must have TLS first, but must not require it yet.** From this version the program connects to a database
server over the network only encrypted, and only when the server's certificate comes from the office's own certificate authority and names
the server. There is no setting to turn this off. Version 0.0 connects without TLS, and the workstations pull this version from the
database over that connection, so the server must keep taking connections without TLS until every workstation runs this version. Switch
over in this order ([docs/db-connection-security.md](../db-connection-security.md), section 0):

1. On the server: make the office CA, the server certificate and the client certificate, set `ssl_ca`, `ssl_cert` and `ssl_key`, restart
   MariaDB, and leave `require_secure_transport` OFF. Create the new database users (with the rights the guide gives: the update makes a
   backup database first). The 0.0 workstations keep working.
2. On the update server: install this version with its setup, as an administrator. Setup runs the connection settings tool
   (`OpenPmsDentalDbSettings.exe`), which sets up the office CA certificate and imports the client certificate (or stores the database
   password). Start the program there as an administrator and publish the release.
3. On every other workstation: it pulls the release at its next start and restarts into this version, which then says that the connection
   settings tool must be run. An administrator runs `OpenPmsDentalDbSettings.exe` in the program folder (its window, or `/silent` with
   `/ca=`, `/pfx=` and `/secretsfile=`), or runs this version's setup with `/DBCA=`, `/DBPFX=` (or `/DBTHUMBPRINT=`) and `/DBSECRETSFILE=`:
   on a computer that already has `FreeDentalConfig.xml`, setup runs the tool with exactly the switches given. Setup without those switches
   only checks the settings.
4. When no workstation connects without TLS any more, run `SET GLOBAL require_secure_transport=ON`, add `require_secure_transport=ON` to
   the server's option file, and drop the old database users or make them `REQUIRE SSL`.

A database on the same computer (`localhost`, `127.0.0.1`, `::1`) needs no certificates: such connections are exempt from the program's
rule. But once `require_secure_transport` is ON, MariaDB refuses connections without TLS from the server computer too, so a program on the
server computer must then connect with `<db-server>` (the name the workstations use), not `localhost`, set up like any workstation.

## Database connection security

- **Encrypted connections only, over the network.** `SslMode=VerifyFull` with the office CA (`SslCa`), and a client certificate from the
  computer's certificate store (LocalMachine) or a password. Every connection the program, the reports (ODR, the RDL engine), ServiceManager
  and the eCW diagnostics make goes through one rule, which refuses before anything is sent.
- **This computer's office CA only.** The read-only and reporting servers over the network are verified against the office CA of the
  computer's connection settings; the CA paths in their preferences are used only for a server on the same computer. The tool keeps its
  copy of the office CA in the program folder (`ConnectionSettings\office-ca.pem`), which only administrators can change.
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
