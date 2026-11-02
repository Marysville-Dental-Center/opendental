# Database connection security (client side)

Status: 2026-11-02. Owner decisions Q1 to Q7 of 2026-11-02, all final. Packages `next/dbsec-1` and its review fixes `next/dbsec-2`. The
server-side set-up is in [docs/db-connection-security.md](../db-connection-security.md).

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
  check costs one dictionary look-up per query; the memory is emptied when this computer's office CA changes.
- `RdlEngine.dll` (LGPL, no reference to the program's assemblies) calls the hook `RdlEngineConfig.CheckConnection` before it creates a
  connection. The program sets the hook to `DbConnectionSecurity.CheckReportConnection` in the constructors of `FormReportForRdl` and
  `FormReportCustom`, the two windows that render reports. The hook refuses every provider other than MySqlConnector and checks
  MySqlConnector strings with the rule.
- `DataConnection.BuildSimpleConnectionString` builds strings that pass: for a server over the network it adds `SslMode=VerifyFull`, `SslCa`
  (this computer's office CA once one is configured, whatever CA the caller names) and, when this computer has a client certificate,
  `CertificateStoreLocation=LocalMachine;CertificateThumbprint=...`.

### 2.1 Loopback

There are two notations for a server, and each has its own test. Neither ever asks DNS.

**A connection string's `Server` value** (`IsLoopbackConnectorServer`), as MySqlConnector reads it: MySqlConnector splits the value at
commas only and hands each entry, exactly as it is, to `Dns.GetHostAddresses`, which answers by itself only for an address that
`IPAddress.TryParse` reads; any other entry goes to the system's name resolution (DNS with the suffix search list, LLMNR, NetBIOS). It never
splits a port out of an entry. So an entry is loopback only when it is `localhost` (any case, nothing around it), or when `IPAddress.TryParse`
reads the entry exactly as it is and `IPAddress.IsLoopback` holds (after `MapToIPv4` for an IPv4-mapped address): `127.0.0.0/8` in any form
`TryParse` accepts, `::1`, `[::1]` (and `[::1]:3306`, which `TryParse` reads as `::1`), `::ffff:127.0.0.1`. `localhost:3306`,
`127.0.0.1:3306`, `[127.0.0.1]`, ` localhost` (with a space, as in `localhost, 127.0.0.1`), `localhost.`, `0.0.0.0` and the computer's own
name or LAN address are names for the network. An empty entry in a list counts as the network. An empty `Server` value (a connection object
made before a database is chosen) names no host and passes. Protocols other than TCP sockets (named pipes, Unix sockets) pass only for
loopback servers.

**The program's own notation** (`IsLoopbackServer`): `ComputerName` in `FreeDentalConfig.xml`, the read-only and reporting server
preferences, the command line, the tool's `/server=`. A server is `host`, `host:port`, `[v6]`, `[v6]:port` or a bare IPv6 address (which
keeps all of its colons). `SplitServer` reads one entry; a comma-separated list is read entry by entry. A server is loopback when every host
is `localhost` or a loopback address as above. This decides whether an old-style password may be read, whether the self-update may pass the
password, and whether a command-line password is accepted.

`BuildSimpleConnectionString` turns the program's notation into what MySqlConnector takes: `Server=` the hosts without ports or brackets
(comma-separated for a list), `Port=` the port (3306 when none is given). It decides loopback with the connector's test on the `Server`
value it writes, so the two notations always agree on the string that is used. A list whose entries name different ports is refused
(MySqlConnector has one `Port` for all hosts).

### 2.2 Over the network

A connection string for any other server must have all of:

1. `SslMode=VerifyFull`.
2. `SslCa`: the office CA certificate file, and the file must exist. Once this computer has an office CA (`DataConnection.OfficeCaPath`,
   set from `FreeDentalConfig.xml` `SslCa` when the program connects), `SslCa` must be that file: a CA named anywhere else (a database
   preference, a connection string) is never trusted.
3. A client credential: `CertificateThumbprint` with `CertificateStoreLocation=LocalMachine`, or a password. The password of the main
   connection can only be the one stored with DPAPI (section 3); the connection settings refuse an old-style password, and a connection
   string to a server over the network, before a string is built.

### 2.3 Refusal messages

`DbConnectionRefusedException` derives from `ApplicationException` (its text is meant for the user) and carries the server and the list of
missing items. Its text never contains a password, a secret or a key.

> Open PMS - Dental did not connect to the database server `<server>`: a connection over the network must be encrypted and the server
> verified, and this computer's connection settings are missing: `<items>`. Run OpenPmsDentalDbSettings.exe in the program folder as an
> administrator to set up the connection.

`<server>` is the `Server` value's entries as they are. When the program's notation would read them as this computer but MySqlConnector
would not (`localhost:3306`, `[127.0.0.1]`), the message adds that MySqlConnector takes each entry as a host name and that the port goes into
`Port=`.

The items, in this order, each only when it applies:

- `server verification (SslMode=VerifyFull, now <mode>)`
- `the office CA certificate file (SslCa)` or `the office CA certificate file <path> (the file does not exist)`
- `this computer's office CA certificate file (SslCa=<office CA>, now <path>)`
- `the client certificate in the computer's certificate store (CertificateStoreLocation=LocalMachine)` (a thumbprint for another store)
- `a client certificate (CertificateThumbprint) or a database password stored on this computer`

Refusals of the connection settings themselves (section 3) end with the same sentence about the tool:

- Old-style password: "The database password in FreeDentalConfig.xml is stored the old way (Password or MySQLPassHash). For the database
  server `<host>` over the network it must be stored by the connection settings tool. ..."
- DPAPI unreadable: "The database password stored on this computer cannot be read: it was stored on another computer, or this computer's
  key changed. ..."
- A password on the command line for a network server: "A database password on the command line is not accepted for the database server
  `<host>` over the network: the password is stored on this computer by the connection settings tool. ..."
- A connection string for a network server (section 3.1): "The connection string (ConnectionString in FreeDentalConfig.xml) names the
  database server `<server>` over the network. Connection strings are for a database on this computer only: ..."
- Release start-up without usable settings: "FreeDentalConfig.xml is missing", "... cannot be read: <XML error>", "... has no database
  connection".

## 3. The connection settings (`FreeDentalConfig.xml`)

The `<DatabaseConnection>` element. `DataConnectionBase.DbConnectionConfig` reads and writes it for every program (the program, ODR, the
tool, ServiceManager); writes keep every other element of the file.

| Element | Meaning |
|---|---|
| `ComputerName` | The server, `host` or `host:port` (unchanged). |
| `Database`, `User` | Unchanged. |
| `SslCa` | Reused: the office CA certificate file (PEM). The tool copies the chosen file to `<program folder>\ConnectionSettings\office-ca.pem` (section 4.4) and writes that path. |
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
| `<ConnectionString>` element for a loopback server (every entry of its `Server` passes the connector's test, section 2.1) | Used as before, through the rule. |
| `<ConnectionString>` element that names any other host, or that MySqlConnector cannot read | Refused in Release and Debug alike (`ChooseDatabaseInfo.ConfigProblem`), with the connection string message: a connection string is for a database on this computer only, whatever it carries (`Password`, `Pwd`, `SslCa`), because its credential is neither the client certificate nor the password stored with DPAPI (Q2). The string is never used: a command-line connection still works, and Debug's Choose Database opens without it. `CentralConnections.TryToConnect` refuses such a string too. The tool's `/check` reports it, and saving with the tool removes the element. |

The office server must have TLS before workstations switch to this version's encrypted connections; the release notes and the server guide
give the order (the server's TLS first with `require_secure_transport` still off, then the update, then `require_secure_transport=ON`).

## 4. The settings tool: `OpenPmsDentalDbSettings.exe`

A WinForms exe in the program folder (`ProgramNames.DbSettingsExeFileName`), `requireAdministrator` in its manifest, the program's icon. It
changes only the connection settings of this computer: server, database, user, the CA certificate, the client certificate (import or
replace a `.pfx`, or a thumbprint of a certificate already in the store), and the switch between certificate and password.

### 4.1 Window

Server, Database, User; Office CA certificate file (Browse); Authentication: Client certificate (Import .pfx..., shows the current
certificate's subject and expiry) or Password (blank keeps the stored one); Test connection; Save; Cancel. Save checks the values, imports
the `.pfx`, copies the CA file, protects the password and writes the file. Test connection uses the values on the screen through the same
rule, with a connection to the database and `SELECT VERSION()`.

A `.pfx` given by `/pfx=` (the installer's page) without a password from a secrets file: the window asks for its password with the password
prompt when it opens, checks it with the file (`ClientCertificateStore.LoadPfxForCheck`, nothing stored), and asks again at Save while it is
still missing. Messages of the window name the window's fields and buttons (Office CA certificate, Import .pfx..., the Password field),
never the secrets file or `/prompt`.

### 4.2 Switches

Names are case-insensitive, `/name=value` or `/name="value"`. No switch: the window, filled from the current file.

| Switch | Meaning |
|---|---|
| `/server=`, `/database=`, `/user=` | The connection. |
| `/ca=<file>` | The office CA certificate file (PEM or DER). |
| `/pfx=<file>` | Import this client certificate (certificate mode). Its password comes from the secrets file or the prompt. |
| `/thumbprint=<hex>` | Use a certificate already in LocalMachine\My (certificate mode, per-computer certificates). |
| `/auth=certificate\|password` | The mode, when neither `/pfx` nor `/thumbprint` says it. |
| `/secretsfile=<file>` | UTF-8 lines `PfxPassword=...` and `DbPassword=...` (the value is everything after the first `=`; blank lines and `#` lines are ignored). The tool reads it and never writes it, logs it or deletes it: the caller deletes it. This is how silent installs pass secrets; interactive runs type them into the window or the prompt. |
| `/prompt` | With `/silent`: ask for missing secrets in a small password dialog instead of failing. (Without `/silent` the window asks.) |
| `/silent` | No window. Apply the values, write the file, exit with a code. |
| `/check` | No window, no change: exit 0 when the current file passes the rule without connecting, 5 when it does not. A `<ConnectionString>` element is checked first (it takes precedence in the program): one that names a host over the network gives 5. |
| `/test` | With `/silent`: test the connection after saving; a failed test gives exit code 4. |
| `/log=<file>` | Append the result line (never a secret) to this file. |

Secrets are never accepted on the command line: a switch named like a password (`/password`, `/dbpassword`, `/pfxpassword`) is an error.
Exit codes: 0 done, 1 bad switches, 2 a value is invalid (CA file, `.pfx`, its password, thumbprint not in the store), 3 writing failed, 4
the connection test failed, 5 the settings do not pass the rule, 6 cancelled.

For an existing install, an administrator sets up the connection without the window with the tool itself, for example
`OpenPmsDentalDbSettings.exe /silent /ca=<office-ca.pem> /pfx=<client.pfx> /secretsfile=<file>` (the server, database and user stay as they
are unless `/server=`, `/database=`, `/user=` are given).

### 4.3 The client certificate

`X509Certificate2Collection.Import(pfx, password, MachineKeySet | PersistKeySet)`, never `Exportable`; the certificate with a private key is
added to LocalMachine\My and BUILTIN\Users get read access to its private key file (CAPI `MachineKeys` or CNG `Keys`), because the
program runs as the signed-in user. A previous client certificate stays in the store (remove it with certlm.msc). Other certificates in the
`.pfx` are not imported.

### 4.4 The office CA copy

Every user of the computer verifies the server against this file, so only an administrator may change it (Q5, Q7). The tool, which runs
elevated, keeps it in the program folder, next to `FreeDentalConfig.xml`: `<program folder>\ConnectionSettings\office-ca.pem`
(`DbConnectionConfig.GetOfficeCaCopyPath`). The program folder of an all-users install is administrator-only (Program Files), and a sub
folder is never part of a release (a release publishes the top-level files only), so the self-update neither carries one computer's copy to
the others nor removes it. `%ProgramData%` is not used: standard users may create folders and files there and would own them.

`OfficeCaFile.WriteCopy`:

- refuses an existing `ConnectionSettings` folder, `office-ca.pem` or `office-ca.pem.new` that is a reparse point (a junction or a symbolic
  link), or whose owner is not BUILTIN\Administrators, SYSTEM, TrustedInstaller or the account the tool runs as (the elevated
  administrator);
- creates the folder with a protected DACL (inheritance removed): SYSTEM and Administrators full control, Users read and execute, owner
  Administrators when the process may set it; an existing folder gets the same DACL;
- writes `office-ca.pem.new` as a new file with an explicit protected `FileSecurity` (SYSTEM and Administrators full control, Users read),
  moves it over the old copy, and sets the same DACL on the result, so nothing of a file a user may have made survives.

## 5. The program

- Release: the start-up never shows Choose Database. When the settings are missing or wrong, or the rule refuses, a message names the
  problem and says to run the tool as an administrator, and the program exits. `File > Choose Database` is not in the menu.
  `TrySaveConnectionSettings` does not write a direct connection (the tool owns the file). A replication failure message exits.
- Debug: Choose Database stays, for local testing. The rule applies to it as everywhere. A settings problem is shown first, then Choose
  Database opens.
- A Middle Tier client configuration (`ServerConnection`; the Middle Tier server is not shipped) starts in Release only with automatic
  log-in, since there is no Choose Database to type the password into.
- Command-line connections (`ServerName=`, `DatabaseName=`, `MySqlUser=`) take the office CA and the client certificate from this computer's
  settings. For a network server a command-line password is refused and the stored DPAPI password is used when the command-line user is
  the configured user.
- The self-update passes the password hash to UpdateFileCopier only for a loopback server; after an update to a network server the
  program reads its stored credentials.
- `CentralConnections.TryToConnect` sets `DataConnection.ClientCertificateThumbprint` and `DataConnection.OfficeCaPath` from this computer's
  settings (ServiceManager does the same from the file) before it connects.
- The read-only and reporting servers (preferences) are built by `BuildSimpleConnectionString` with this computer's client certificate.
  For a server over the network their CA is always this computer's office CA (`DataConnection.GetSslCaForServer`): the CA paths in the
  database preferences (`ReadOnlyServerSslCa`, `ReportingServerSslCa`), which any user with Setup permission or write access to the
  preference table can change, are ignored, and the rule refuses any other CA. Without an office CA on this computer such a connection is
  refused with the rule's message. For a server on this computer the preference CA is used as before (`VerifyCA`). A blank server
  preference means the main server, with the main connection's CA.
  Their passwords stay in the database preferences, as before (section 8).

## 6. The installer (`installer/OpenPmsDental.iss`)

- The connection page asks server, database and user; a second page asks the office CA certificate file and the client `.pfx` (blank: a
  database password instead). Both are skipped when the program folder already has `FreeDentalConfig.xml`. The second page is skipped for a
  loopback server and for per-user installs.
- Silent switches: `/DBSERVER=`, `/DBNAME=`, `/DBUSER=` (as before), `/DBCA=<file>`, `/DBPFX=<file>`, `/DBTHUMBPRINT=<hex>`,
  `/DBSECRETSFILE=<file>` (passed to the tool; the caller deletes it).
- Setup writes the base file as before (no password) and no longer gives Users modify rights on it.
- All-users (administrator) installs, new connection (no `FreeDentalConfig.xml` yet), after the files are installed (`ssPostInstall`):
  - a server over the network, or any security switch: setup runs the tool with the pages' values. Interactive, its window opens filled
    with them and asks for the `.pfx` password or the database password; silent, `/silent` with the secrets file.
  - a loopback server without security switches, interactive: the tool's window opens filled with the pages' values, in password mode
    (`/auth=password`), so that the database password of a server PC or a single-computer office is stored with DPAPI (Release has no
    Choose Database to ask for it). Cancel keeps the file without a password, which suits a database user with a blank password.
  - a loopback server without security switches, silent: no tool (`/DBSECRETSFILE=` with `DbPassword=` runs it, `/silent`).
  - `/log=` points into setup's temporary folder and the tool's result line goes into setup's log. A failure is shown (suppressible) and
    the install finishes; the program then shows the run-the-tool message.
- All-users installs, upgrade (the file exists): setup never passes the connection page's defaults (`localhost`, `opendental`, `root`).
  - Any of `/DBCA=`, `/DBPFX=`, `/DBTHUMBPRINT=`, `/DBSECRETSFILE=` given: setup runs the tool with only the switches that were given
    (`/DBSERVER=`, `/DBNAME=`, `/DBUSER=` too, when given): silent, `/silent`; interactive, the window. A failure is shown (suppressible)
    and logged.
  - No security switch: setup runs `/check` and, interactive, offers to open the tool when the settings fail the rule; silent, it logs the
    result.
- Per-user installs (`/CURRENTUSER`, tests only) cannot run the administrator tool: they write the base file; a network server is logged
  ("needs the connection settings tool") and shown as needing the tool; setup still ends with exit code 0.
- An upgraded v0.0 install keeps the Users modify right its setup gave the file (setup never rewrites an existing file, and the tool's
  save keeps the file's rights). See section 8.

## 7. Paths reviewed

| Path | Shipped | Result |
|---|---|---|
| `DataConnectionBase` (`DataConnection`, `MySqlHelper`, `ConnectionStoreBase`) | yes | Through `NewMySqlConnection`; strings from `BuildSimpleConnectionString`. |
| `OpenPmsDentalBusiness` (`CentralConnections`, `ConnectionStore`, `DataConnectionCancelable`) | yes | Settings through `DbConnectionConfig`; connections through the rule; read-only and reporting servers with the office CA. |
| `OpenPmsDental.exe` (start-up, Choose Database, read-only and report servers, eCW diagnostics, RDL allocators, self-update) | yes | Through the rule; Choose Database Debug only. The eCW diagnostics build `Server=`+the eCW server property with `SslMode=none`, so they reach a loopback eCW database only. |
| `ODR.dll` | yes | The program's string, or `DbConnectionConfig` + `BuildSimpleConnectionString`. |
| `RdlEngine.dll` | yes | `RdlEngineConfig.CheckConnection` hook. (`Definition/DataSource.cs` is not compiled.) |
| `ServiceManager.exe` | yes | Test connection through `DataConnection.SetDb` with this computer's CA and certificate. The service configuration files it writes are for services this fork does not ship and are unchanged. |
| `UpdateFileCopier.exe` | yes | Passes on what the program gave it; no password for a network server. |
| `OpenDentalServiceConfig.xml` (`PrefL.CreateConfigForOpenDentalService`, `ServicesHelper.CreateServiceConfigFile`) | written by the program | For the OpenDentalService, which this fork does not ship; unchanged (it still writes the osd1$ form). |
| Middle Tier server (`DtoProcessor`, `Userods`), `OpenPmsDentalHL7` | no | Connect through `DataConnection`, so the rule applies; their configuration files have no CA or certificate elements, so only a loopback database works for them. |
| `DatabaseIntegrityCheck` | no | Dev-only. Its connection now goes through `NewMySqlConnection`, so with its `SslMode=none` it reaches loopback databases only. |
| `CentralManager`, `MySQLManager` | no | Dev-only; they connect through `DataConnection`, so the rule applies. |
| `DocumentationBuilder` | no | Dev-only, not in the solution filter, unchanged: it reads the config of a local Release build (`..\..\..\OpenPmsDental\bin\Release`), a loopback database. |
| `tools/SyntheticData`, `tools/AdoptionValidation` | no | Dev-only, unchanged: they refuse any server but this computer themselves (`LocalOnly.EnsureLocalHost`, the adoption tools' loopback and SSH-forward checks). |

## 8. Open points for the owner

1. The read-only and reporting servers keep their passwords in the database preferences (`ReadOnlyServerMySqlPassHash`,
   `ReportingServerMySqlPassHash`, the reversible `osd1$` form), as in v0.0. Their connections over the network are forced to
   `VerifyFull` with this computer's office CA (section 5), never with a CA from the preferences. Should those passwords move to this
   computer's DPAPI store (one per workstation, set by the tool), or should those servers use the client certificate only?
2. An upgraded v0.0 install keeps the BUILTIN\Users modify right that v0.0's setup gave `FreeDentalConfig.xml`, so a standard user of
   such a computer can still edit `ComputerName` or `SslCa` there. Should the tool's save (or setup's upgrade) reset the file to the
   program folder's rights? Machine-scope DPAPI and a client key that Users may read already let a local user use the credentials.
