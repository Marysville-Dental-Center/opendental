# Database connection security (client side)

Status: 2026-11-02. Owner decisions Q1 to Q7 of 2026-11-02, all final. Package `next/dbsec-1`. The server-side set-up is in
[docs/db-connection-security.md](../db-connection-security.md).

## 1. The decisions

| | Decision |
|---|---|
| Q1 | The client only. Server, firewall and network configuration are out of scope apart from the short server guide. |
| Q2 | One shared client certificate on every workstation, read from the Windows certificate store (LocalMachine\My) by thumbprint, its private key imported as non-exportable. A password stored DPAPI-encrypted per computer (machine scope) is the fallback for databases not set up for certificates. Per-computer certificates later must need no client change. |
| Q3 | Refuse to connect when the connection cannot be encrypted and the server verified, with a clear message naming what is missing. No override. |
| Q4 | Connections to the same computer (`localhost`, `127.0.0.1` and every other loopback address, `::1`) are exempt. Every connection over the network must be encrypted. |
| Q5 | `SslMode=VerifyFull`: the server certificate must chain to the office CA (`SslCa`, the office CA certificate file) and name the host the computers connect to. |
| Q6 | The installer sets the connection up (a page plus silent switches). Choose Database is for local testing and debugging only: Release builds do not offer it; Debug builds keep it, and the rule still applies there. |
| Q7 | A small admin-only settings tool in the program folder changes the connection settings only. The installer uses the same tool. |

## 2. The rule

One class decides, `DataConnectionBase.DbConnectionSecurity`. Every connection a shipped binary opens goes through it before any network
connection is attempted:

- `DbConnectionSecurity.NewMySqlConnection(connectionString)` is the only place a shipped binary creates a `MySqlConnection`
  (`DataConnection`, `MySqlHelper`, `DataConnectionCancelable`, `ODR.DataConnection`, the RDL allocator classes). It parses the string with
  `MySqlConnectionStringBuilder` and throws `DbConnectionRefusedException` when the rule fails. A string that passed is remembered, so the
  check costs one dictionary look-up per query.
- `RdlEngine.dll` (LGPL, no reference to the program's assemblies) calls the hook `RdlEngineConfig.CheckConnection` before it creates a
  connection. The program installs the hook (`RdlConnectionCheck.Install`) before it renders a report. The hook refuses every provider other
  than MySqlConnector and checks MySqlConnector strings with the rule.
- `DataConnection.BuildSimpleConnectionString` builds strings that pass: for a server over the network it adds `SslMode=VerifyFull`, `SslCa`
  and, when this computer has a client certificate, `CertificateStoreLocation=LocalMachine;CertificateThumbprint=...`.

### 2.1 Loopback

A server is on the same computer when every host of its `Server` value is a loopback host. Hosts are the comma-separated entries of the
value, each without its port (`host:port`, `[v6]:port`, `[v6]`; a bare IPv6 address keeps all of its colons). A host is loopback when it is
`localhost` (any case), or when `IPAddress.TryParse` reads it and `IPAddress.IsLoopback` is true (`127.0.0.0/8`, `::1`, an IPv4-mapped
loopback such as `::ffff:127.0.0.1`). DNS is never asked. Any other name, `localhost.` with a trailing dot, `0.0.0.0` and the computer's
own name or LAN address count as the network. An empty `Server` value (a connection object made before a database is chosen) names no
remote host and passes. Protocols other than TCP sockets (named pipes, Unix sockets) pass only for loopback hosts.

### 2.2 Over the network

A connection string for any other server must have all of:

1. `SslMode=VerifyFull`.
2. `SslCa`: the office CA certificate file, and the file must exist.
3. A client credential: `CertificateThumbprint` with `CertificateStoreLocation=LocalMachine`, or a password. The password of the main
   connection can only be the one stored with DPAPI (section 3); the connection settings refuse an old-style password before a string is
   built.

### 2.3 Refusal messages

`DbConnectionRefusedException` derives from `ApplicationException` (its text is meant for the user) and carries the server and the list of
missing items. Its text never contains a password, a secret or a key.

> Open PMS - Dental did not connect to the database server `<host>`: a connection over the network must be encrypted and the server
> verified, and this computer's connection settings are missing: `<items>`. Run OpenPmsDentalDbSettings.exe in the program folder as an
> administrator to set up the connection.

The items, in this order, each only when it applies:

- `server verification (SslMode=VerifyFull, now <mode>)`
- `the office CA certificate file (SslCa)` or `the office CA certificate file <path> (the file does not exist)`
- `the client certificate in the computer's certificate store (CertificateStoreLocation=LocalMachine)` (a thumbprint for another store)
- `a client certificate (CertificateThumbprint) or a database password stored on this computer`

Refusals of the connection settings themselves (section 3) end with the same sentence about the tool:

- Old-style password: "The database password in FreeDentalConfig.xml is stored the old way (Password or MySQLPassHash). For the database
  server `<host>` over the network it must be stored by the connection settings tool. ..."
- DPAPI unreadable: "The database password stored on this computer cannot be read: it was stored on another computer, or this computer's
  key changed. ..."
- A password on the command line for a network server: "A database password on the command line is not accepted for the database server
  `<host>` over the network: the password is stored on this computer by the connection settings tool. ..."
- Release start-up without usable settings: "FreeDentalConfig.xml is missing", "... cannot be read: <XML error>", "... has no database
  connection".

## 3. The connection settings (`FreeDentalConfig.xml`)

The `<DatabaseConnection>` element. `DataConnectionBase.DbConnectionConfig` reads and writes it for every program (the program, ODR, the
tool, ServiceManager); writes keep every other element of the file.

| Element | Meaning |
|---|---|
| `ComputerName` | The server, `host` or `host:port` (unchanged). |
| `Database`, `User` | Unchanged. |
| `SslCa` | Reused: the office CA certificate file (PEM). The tool copies the chosen file to `%ProgramData%\OpenPMS-Dental\Database\office-ca.pem` and writes that path. |
| `ClientCertThumbprint` | New. The SHA-1 thumbprint (40 hex digits) of the client certificate in LocalMachine\My. Present: certificate mode. |
| `MySQLPassDpapi` | New. `dpapi1$` + Base64 of `ProtectedData.Protect(UTF-8 password, fixed entropy, LocalMachine)`. Present: the password. |
| `Password`, `MySQLPassHash` | Old style (plain text, `osd1$`). Read only for a loopback server. Every write leaves them empty (older readers expect the elements). |
| `NoShowOnStartup` | Kept as it is. Release builds ignore it (they never show Choose Database). |

Certificate mode and password mode are exclusive in what the tool writes; a reader that finds both uses both. New writes never use
`osd1$`: `CentralConnections.TrySaveConnectionSettings` (Debug builds only, section 5) writes `MySQLPassDpapi`.

DPAPI with machine scope means any process on this computer can decrypt the password and no other computer can (Q2). The file's ACL is
what keeps other local users out; the installer no longer gives Users modify rights on it (section 6).

The DPAPI helper is `CodeBase.MachineSecret` (`System.Security.Cryptography.ProtectedData`, `DataProtectionScope.LocalMachine`, a fixed
entropy). Its exceptions never carry the secret.

### 3.1 Upgrading a v0.0 configuration

| v0.0 configuration | This version |
|---|---|
| Loopback server, any password (the developer configuration: `localhost`, `root`, blank) | Connects as before. |
| Network server, `Password` or `MySQLPassHash` not empty | Refused with the old-style password message. |
| Network server, no password, no CA | Refused: the rule's message (CA and credential missing). |
| `<ConnectionString>` element | Checked by the rule like any string. |

The office server must have TLS before this version is installed or self-updated to (release notes).

## 4. The settings tool: `OpenPmsDentalDbSettings.exe`

A WinForms exe in the program folder (`ProgramNames.DbSettingsExeFileName`), `requireAdministrator` in its manifest, the program's icon. It
changes only the connection settings of this computer: server, database, user, the CA certificate, the client certificate (import or
replace a `.pfx`, or a thumbprint of a certificate already in the store), and the switch between certificate and password.

### 4.1 Window

Server, Database, User; Office CA certificate file (Browse); Authentication: Client certificate (Import .pfx..., shows the current
certificate's subject and expiry) or Password (blank keeps the stored one); Test connection; Save; Cancel. Save checks the values, imports
the `.pfx` (asking for its password), copies the CA file, protects the password and writes the file. Test connection uses the values on
the screen through the same rule, with a connection to the database and `SELECT VERSION()`.

### 4.2 Switches

Names are case-insensitive, `/name=value` or `/name="value"`. No switch: the window, filled from the current file.

| Switch | Meaning |
|---|---|
| `/server=`, `/database=`, `/user=` | The connection. |
| `/ca=<file>` | The office CA certificate file (PEM or DER). |
| `/pfx=<file>` | Import this client certificate (certificate mode). Its password comes from the secrets file or the prompt. |
| `/thumbprint=<hex>` | Use a certificate already in LocalMachine\My (certificate mode, per-computer certificates). |
| `/auth=certificate\|password` | The mode, when neither `/pfx` nor `/thumbprint` says it. |
| `/secretsfile=<file>` | UTF-8 lines `PfxPassword=...` and `DbPassword=...`. The tool reads it and never writes it, logs it or deletes it: the caller deletes it. |
| `/prompt` | Ask for missing secrets in a small password dialog (interactive installs). |
| `/silent` | No window. Apply the values, write the file, exit with a code. |
| `/check` | No window, no change: exit 0 when the current file passes the rule without connecting, 5 when it does not. |
| `/test` | With `/silent`: test the connection after saving; a failed test gives exit code 4. |
| `/log=<file>` | Append the result line (never a secret) to this file. |

Secrets are never accepted on the command line: a switch named like a password (`/password`, `/dbpassword`, `/pfxpassword`) is an error.
Exit codes: 0 done, 1 bad switches, 2 a value is invalid (CA file, `.pfx`, its password, thumbprint not in the store), 3 writing failed, 4
the connection test failed, 5 the settings do not pass the rule, 6 cancelled.

### 4.3 The client certificate

`X509Certificate2Collection.Import(pfx, password, MachineKeySet | PersistKeySet)`, never `Exportable`; the certificate with a private key is
added to LocalMachine\My and BUILTIN\Users get read access to its private key file (CAPI `MachineKeys` or CNG `Keys`), because the
program runs as the signed-in user. A previous client certificate stays in the store (remove it with certlm.msc). Other certificates in the
`.pfx` are not imported.

## 5. The program

- Release: the start-up never shows Choose Database. When the settings are missing or wrong, or the rule refuses, a message names the
  problem and says to run the tool as an administrator, and the program exits. `File > Choose Database` is not in the menu.
  `TrySaveConnectionSettings` does not write a direct connection (the tool owns the file). A replication failure message exits.
- Debug: Choose Database stays, for local testing. The rule applies to it as everywhere.
- Command-line connections (`ServerName=`, `DatabaseName=`, `MySqlUser=`) take the office CA and the client certificate from this computer's
  settings. For a network server a command-line password is refused and the stored DPAPI password is used when the command-line user is
  the configured user.
- The self-update passes the password hash to UpdateFileCopier only for a loopback server; after an update to a network server the
  program reads its stored credentials.
- The read-only and reporting servers (preferences) are built by `BuildSimpleConnectionString` with their own `SslCa` or this computer's,
  and this computer's client certificate. Their passwords stay in the database preferences, as before.

## 6. The installer (`installer/OpenPmsDental.iss`)

- The connection page asks server, database and user; a second page asks the office CA certificate file and the client `.pfx` (blank: a
  database password instead). Both are skipped when the program folder already has `FreeDentalConfig.xml`. The second page is skipped for a
  loopback server and for per-user installs.
- Silent switches: `/DBSERVER=`, `/DBNAME=`, `/DBUSER=` (as before), `/DBCA=<file>`, `/DBPFX=<file>`, `/DBTHUMBPRINT=<hex>`,
  `/DBSECRETSFILE=<file>` (passed to the tool; the caller deletes it).
- Setup writes the base file as before (no password) and no longer gives Users modify rights on it.
- All-users (administrator) installs: after the files are installed, setup runs the tool with the page's values and `/prompt` (interactive)
  or `/silent` with the secrets file (silent), and `/log=` in setup's temporary folder; the tool's result is in setup's log. A failure is
  shown (suppressible) and the install finishes; the program then shows the run-the-tool message. On an upgrade (the file exists) setup runs
  `/check` and, interactive, offers to open the tool when the settings fail the rule.
- Per-user installs (`/CURRENTUSER`, tests only) cannot run the administrator tool: they write the base file; a network server is logged
  and shown as needing the tool.

## 7. Paths reviewed

| Path | Shipped | Result |
|---|---|---|
| `DataConnectionBase` (`DataConnection`, `MySqlHelper`, `ConnectionStoreBase`) | yes | Through `NewMySqlConnection`; strings from `BuildSimpleConnectionString`. |
| `OpenPmsDentalBusiness` (`CentralConnections`, `ConnectionStore`, `DataConnectionCancelable`) | yes | Settings through `DbConnectionConfig`; connections through the rule. |
| `OpenPmsDental.exe` (start-up, Choose Database, read-only and report servers, eCW diagnostics, RDL allocators, self-update) | yes | Through the rule; Choose Database Debug only. |
| `ODR.dll` | yes | The program's string, or `DbConnectionConfig` + `BuildSimpleConnectionString`. |
| `RdlEngine.dll` | yes | `RdlEngineConfig.CheckConnection` hook. (`Definition/DataSource.cs` is not compiled.) |
| `ServiceManager.exe` | yes | Test connection through `DataConnection.SetDb` with this computer's CA and certificate. The service configuration files it writes are for services this fork does not ship and are unchanged. |
| `UpdateFileCopier.exe` | yes | Passes on what the program gave it; no password for a network server. |
| Middle Tier server (`DtoProcessor`, `Userods`), `OpenPmsDentalHL7` | no | Connect through `DataConnection`, so the rule applies; their configuration files have no CA or certificate elements, so only a loopback database works for them. |
| `DatabaseIntegrityCheck`, `DocumentationBuilder`, `CentralManager`, `MySQLManager`, `tools/*` | no | Dev-only, unchanged; they are used against loopback databases only. |
