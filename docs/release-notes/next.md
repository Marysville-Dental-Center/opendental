# Open PMS - Dental, next version: release notes (unreleased)

Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.

This file collects the notes of the version after 0.0 until it is released; the release renames it to `v<version>.md`.

## Before you install or self-update to this version

**The office database server must have TLS first.** From this version the program connects to a database server over the network only
encrypted, and only when the server's certificate comes from the office's own certificate authority and names the server. There is no
setting to turn this off. A workstation that self-updates to this version before the server is set up stops at start-up with a message
that names what is missing.

1. Set up the server as [docs/db-connection-security.md](../db-connection-security.md) describes: the office CA, the server certificate,
   the client certificate, `require_secure_transport=ON`, the database users.
2. On every workstation, as an administrator, run the new setup, or after a self-update run `OpenPmsDentalDbSettings.exe` in the program
   folder: give the office CA certificate and import the client certificate (or give the database password).

A database on the same computer (`localhost`, `127.0.0.1`, `::1`) needs nothing: such connections are exempt.

## Database connection security

- **Encrypted connections only, over the network.** `SslMode=VerifyFull` with the office CA (`SslCa`), and a client certificate from the
  computer's certificate store (LocalMachine) or a password. Every connection the program, the reports (ODR, the RDL engine), ServiceManager
  and the eCW diagnostics make goes through one rule, which refuses before anything is sent.
- **One shared client certificate** on every workstation, imported with its private key not exportable; the database password, stored on
  each computer with Windows DPAPI (machine scope), is the fallback. Per-computer certificates later need no change in the program.
- **The connection settings tool** `OpenPmsDentalDbSettings.exe` (administrators only) changes this computer's connection: server,
  database, user, the office CA, the client certificate or the password, with a Test connection button. The installer uses it (new switches
  `/DBCA=`, `/DBPFX=`, `/DBTHUMBPRINT=`, `/DBSECRETSFILE=`).
- **Choose Database is gone from Release builds** (File menu and start-up). When the connection settings are missing or wrong, the program
  says what is wrong and that an administrator should run the tool.
- **Old-style passwords.** A password stored the old way in `FreeDentalConfig.xml` (`Password`, `MySQLPassHash`) is used only for a database
  on the same computer. For a server over the network the program refuses it: store the password again with the tool. New writes never use
  the old format.
- The installer no longer gives Users modify rights on `FreeDentalConfig.xml`.
