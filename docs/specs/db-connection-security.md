# Database connection security (client side)

Status: 2026-11-02. Owner decisions Q1 to Q7 and the follow-up decisions S1 to S4 of 2026-11-02, all final. Packages `next/dbsec-1`, its
review fixes `next/dbsec-2`, the follow-up `next/dbsec-scope-1`, and the test of the account's rights `next/fu-grants-1` (section 9.5).
The server-side set-up is in
[docs/db-connection-security.md](../db-connection-security.md).

The owner's follow-up: "We are only touching the Open Dental account and auth, and the goal is to not use root anymore."

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
| S1 | Scope. The rule (encryption with `VerifyFull`, the office CA, the client certificate or the DPAPI password, refusal before connecting) is for the program's own database account: the main connection of `FreeDentalConfig.xml` and everything that reuses its settings (the reports through ODR and RDL, ServiceManager's test, the settings tool, the self-update). The read-only and reporting servers configured in the database preferences work exactly as before this work: their own server, user, password and `SslCa` preferences, with the old `SslMode` behaviour (section 5.1). An empty server preference is the main connection and stays under the rule. |
| S2 | Not root. The installer's `/DBUSER` default and its connection page default are `opmsd`, the guide's account. The settings tool warns (never refuses) about `root` for a server over the network (section 4.5). The server guide gives the account the narrowest rights the program needs (section 9), not `ALL PRIVILEGES ON *.*`. The Debug-only and same-computer defaults stay `root` for developers (section 4.5). |
| S3 | The launch command (decision a): kept as built. A database password on the program's command line is refused for a server over the network, and the password or certificate stored on the computer is used; the program's own user-login parameters stay as they are (section 5). |
| S4 | Offices on v0.0 uninstall v0.0 and install the new version: no in-place upgrade handling for v0.0. Its uninstaller removes the program folder with the `FreeDentalConfig.xml` its setup wrote. The release is published only when v0.0 is uninstalled on every workstation, so that no v0.0 pulls it in place (section 3.1). The upgrade checks for later versions stay as built (sections 3.1 and 6). |

## 2. The rule

One class decides, `DataConnectionBase.DbConnectionSecurity`. The rule is for the program's own database account (S1). Every connection a
shipped binary opens goes through it before any network connection is attempted:

- `DbConnectionSecurity.NewMySqlConnection(connectionString)` is the only place a shipped binary creates a `MySqlConnection`
  (`DataConnection`, `MySqlHelper`, `DataConnectionCancelable` through `NewMySqlConnectionWithoutPooling`, `ODR.DataConnection`, the RDL
  allocator classes). It parses the string with `MySqlConnectionStringBuilder` and throws `DbConnectionRefusedException` when the rule
  fails. A string that passed is remembered, so the check costs one dictionary look-up per query; the memory is emptied when this
  computer's office CA changes.
- The one exception (S1, section 5.1): a string that `DataConnection.BuildPreferenceServerConnectionString` built in this process for a
  read-only or reporting server of its own passes as it did before the rule (`IsPreferenceServerConnection`). The exemption is the string
  itself, character for character, registered by that builder (`AllowPreferenceServerConnection`, internal to `DataConnectionBase`), never
  a string because of what it contains.
- `RdlEngine.dll` (LGPL, no reference to the program's assemblies) calls the hook `RdlEngineConfig.CheckConnection` before it creates a
  connection. The program sets the hook to `DbConnectionSecurity.CheckReportConnection` in the constructors of `FormReportForRdl` and
  `FormReportCustom`, the two windows that render reports. The hook refuses every provider other than MySqlConnector and checks
  MySqlConnector strings with the rule.
- `DataConnection.BuildSimpleConnectionString`, the builder of the program's own account, builds strings that pass: for a server over the
  network it adds `SslMode=VerifyFull`, `SslCa` (this computer's office CA once one is configured, whatever CA the caller names) and, when
  this computer has a client certificate, `CertificateStoreLocation=LocalMachine;CertificateThumbprint=...`.

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

**The program's own notation** (`IsLoopbackServer`): `ComputerName` in `FreeDentalConfig.xml`, the command line, the tool's `/server=`
(the read-only and reporting server preferences use the same notation, but their connections are outside the rule, section 5.1). A
server is `host`, `host:port`, `[v6]`, `[v6]:port` or a bare IPv6 address (which keeps all of its colons). `SplitServer` reads one entry;
a comma-separated list is read entry by entry. A server is loopback when every host is `localhost` or a loopback address as above. This
decides whether an old-style password may be read, whether the self-update may pass the password, whether a command-line password is
accepted, and whether the settings tool warns about `root`.

`BuildSimpleConnectionString` turns the program's notation into what MySqlConnector takes: `Server=` the hosts without ports or brackets
(comma-separated for a list), `Port=` the port (3306 when none is given). It decides loopback with the connector's test on the `Server`
value it writes, so the two notations always agree on the string that is used. A list whose entries name different ports is refused
(MySqlConnector has one `Port` for all hosts).

### 2.2 Over the network

A connection string for any other server must have all of:

1. `SslMode=VerifyFull`.
2. `SslCa`: the office CA certificate file, and the file must exist. Once this computer has an office CA (`DataConnection.OfficeCaPath`,
   set from `FreeDentalConfig.xml` `SslCa` when the program connects), `SslCa` must be that file: a CA named anywhere else (a connection
   string, a caller's own CA) is never trusted for the program's own account.
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
what keeps other local users out; the installer no longer gives Users modify rights on it (section 6), and every save of the tool sets the
file's rights back to the program folder's (owner decision of 2026-11-02, `DbSettingsRunner.ResetConfigFileRights`): every explicit rule
is removed, so a Users modify right a v0.0 setup gave or anyone added is gone, the folder's rules are inherited, and the owner becomes
BUILTIN\Administrators. `DbConnectionConfig.SaveFile` replaces the file with `File.Replace`, which on its own keeps the old file's rules.

The DPAPI helper is `CodeBase.MachineSecret` (`System.Security.Cryptography.ProtectedData`, `DataProtectionScope.LocalMachine`, a fixed
entropy). Its exceptions never carry the secret.

### 3.1 Configurations in the old form

Offices on v0.0 uninstall v0.0 before they install this version (S4): its uninstaller removes the whole program folder, including the
`FreeDentalConfig.xml` its setup wrote, so no v0.0 file is upgraded in place and no handling for one was added. That holds only because
the release is published after v0.0 is uninstalled on every workstation: v0.0 pulls any newer published build at its next start
(v0.0's `docs/release-process.md`, "A newer build is published": Pull) and installs it over itself, keeping its file with the Users
modify right of v0.0's setup, and the program never checks that file's ACL (only a save with the tool resets it, section 3) (`CentralConnections` reads `SslCA`, the office CA,
from it as it is). The guide's section 0 and the release notes therefore have the update server answer Cancel to the publish offer
(`PrefL.CheckProgramVersion`) until v0.0 is uninstalled everywhere, then publish with Update Setup > Recopy, and have a workstation that
pulled the build anyway uninstalled through Apps (the entry is still v0.0's) before setup runs, never set up with the tool alone. The
checks below stay as built, for any file in the old form the program still meets (a developer's configuration, a file copied in by
hand, a later version's upgrade):

| Configuration | This version |
|---|---|
| Loopback server, any password (the developer configuration: `localhost`, `root`, blank) | Connects as before. |
| Network server, `Password` or `MySQLPassHash` not empty | Refused with the old-style password message. |
| Network server, no password, no CA | Refused: the rule's message (CA and credential missing). |
| `<ConnectionString>` element for a loopback server (every entry of its `Server` passes the connector's test, section 2.1) | Used as before, through the rule. |
| `<ConnectionString>` element that names any other host, or that MySqlConnector cannot read | Refused in Release and Debug alike (`ChooseDatabaseInfo.ConfigProblem`), with the connection string message: a connection string is for a database on this computer only, whatever it carries (`Password`, `Pwd`, `SslCa`), because its credential is neither the client certificate nor the password stored with DPAPI (Q2). The string is never used: a command-line connection still works, and Debug's Choose Database opens without it. `CentralConnections.TryToConnect` refuses such a string too. The tool's `/check` reports it, and saving with the tool removes the element. |

The office server must have TLS before workstations switch to this version's encrypted connections; the release notes and the server guide
give the order (the server's TLS first with `require_secure_transport` still off, then uninstalling v0.0 and installing this version on
each workstation, the update server first, publishing the release only when v0.0 is uninstalled everywhere, then
`require_secure_transport=ON`).

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
program runs as the signed-in user. Other certificates in the `.pfx` are not imported.

`PersistKeySet` stores the key of every certificate of the file that has one. As the Direct certificate import does (DCFU2,
`docs/port-notes/DIRECT-CERT-FOLLOWUP.md`), the tool deletes again the keys it stored and does not use: those of the file's other
certificates (its CA's, for example) once the client certificate is in the store, and every one, the client certificate's too, when the
store refuses the certificate or anything else stops the import. It deletes only keys of the file's certificates, and never one that the
client certificate or a certificate of the store uses (compared by key container, each container once). A key it cannot delete is named,
with its file, in the result.

A `.pfx` whose certificate the store already holds with a usable private key (it opens and signs, or decrypts, a test value), as when
setup runs again with the same file, is not added again, as the Direct certificate import leaves such a certificate (DCFU2 fix round):
the store's entry stays as it is, with its key; every key the import stored is deleted, the client certificate's own too; and the users
get read access to the entry's key. The store's add would give the entry the new key and leave the old one on the computer, readable by
the users and named by no certificate. An entry whose key cannot be used (deleted, or it does not open) is added again and takes the
import's key.

The certificate it replaces (owner decision of 2026-11-02): the tool removes a client certificate from the store, with its private key
(CNG `Delete`, or the CAPI key container), only when it imported that certificate itself and a save stops using it (another certificate,
or the password). A CAPI key (a `.pfx` that names no CNG provider, as OpenSSL writes it, or one that names a CAPI provider) is deleted
and its file named through CAPI (`X509Certificate2.PrivateKey`, in the key set the key lives in), since .NET Framework's
`GetRSAPrivateKey` opens it through CNG, as an `RSACng` whose `Key.Delete` leaves the container (DIRECT-CERT-FOLLOWUP). The certificate
goes first, then its key, which stays when another certificate of the store uses it; when only the key cannot be deleted, the result
says that the certificate was removed and names the key's file, which certlm.msc no longer shows. It records the thumbprint of what it
imported in `<program folder>\ConnectionSettings\imported-client-certificate.txt` (`ImportedCertificateRecord`), written with the
protection of the office CA copy (section 4.4), not in `FreeDentalConfig.xml`, whose content must never decide what the tool deletes.
Only a certificate the store did not have before its `.pfx` was imported is recorded; one named with `/thumbprint=`, or put into the
store some other way, is never recorded and never removed (remove it with certlm.msc). A missing or untrusted record (a link, or a file
an administrator does not own) means none. The certificate goes only after the new settings are saved
(`DbSettingsRunner.PlanImportedCertificate`); when it cannot be removed, the result says so, with the command that removes it with its
key (certlm.msc's Delete leaves the key), and the save stands.

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
  Administrators (the tool always runs elevated; only the tests run this code without elevation, and then their own account also gets full
  control, since it is not an administrator then); an existing folder gets the same DACL;
- writes `office-ca.pem.new` as a new file with an explicit protected `FileSecurity` (SYSTEM and Administrators full control, Users read),
  moves it over the old copy, and sets the same DACL on the result, so nothing of a file a user may have made survives.

### 4.5 The user root (S2)

`DbSettingsRunner.GetRootUserWarning(server,user)`: for the user `root` (any case, spaces trimmed) and a server over the network
(`IsLoopbackServer` false) it returns "Warning: the database user root is MariaDB's administrator account. For a server over the network
the office should use its own account for the program (for example opmsd), with only the rights the server guide gives it
(docs/db-connection-security.md, section 5)."; otherwise "". It is a warning, never a refusal:

- `Apply` saves as asked and appends the warning to its result message (exit code 0), so a silent run (the installer's) logs it.
- The window shows it in the status line when the Server or User box is left and when it opens, and asks "Save with the user root
  anyway?" before Save.

Left as `root`, for developers on their own computer: `CentralConnections.GetChooseDatabaseConnectionSettings`' fallback when
`FreeDentalConfig.xml` cannot be read (Debug builds only; Release names the problem and exits), Choose Database's database list
(`CentralConnections.GetDatabases`, Debug only, `root` when the user box is empty), and no warning for `root` on the same computer
(`localhost`, a loopback address), the developer configuration of `README.md`. The set-up windows of the read-only and reporting servers
also still list databases as `root` when their user box is empty (section 5.1, as on main); that is the account of those servers, not the
program's own.

## 5. The program

- Release: the start-up never shows Choose Database. When the settings are missing or wrong, or the rule refuses, a message names the
  problem and says to run the tool as an administrator, and the program exits. `File > Choose Database` is not in the menu.
  `TrySaveConnectionSettings` does not write a direct connection (the tool owns the file). A replication failure message exits.
- Debug: Choose Database stays, for local testing. The rule applies to it as everywhere. A settings problem is shown first, then Choose
  Database opens.
- A Middle Tier client configuration (`ServerConnection`; the Middle Tier server is not shipped) starts in Release only with automatic
  log-in, since there is no Choose Database to type the password into.
- Command-line connections (S3, decision a, kept as built): `ServerName=`, `DatabaseName=`, `MySqlUser=` take the office CA and the client
  certificate from this computer's settings (`CentralConnections.ApplyCommandLineConnection`). For a server over the network a database
  password on the command line (`MySqlPassword=`, `MySqlPassHash=`, `MySqlPassObfuscated=`) is refused with the command-line message of
  section 2.3, and the password stored with DPAPI (or the client certificate) of this computer is used when the command-line user is the
  configured user. For a server on this computer the command-line password is used as before. The program's own user-login parameters
  (`UserName=`, `OdPassword=`, `OdPassObfuscated=`, `PassHash=`) are unchanged. A silent update (`IsSilentUpdate=true`) to a server over
  the network needs no database password on the command line.
- The self-update passes the password hash to UpdateFileCopier only for a loopback server; after an update to a network server the
  program reads its stored credentials.
- `CentralConnections.TryToConnect` sets `DataConnection.ClientCertificateThumbprint` and `DataConnection.OfficeCaPath` from this computer's
  settings (ServiceManager does the same from the file) before it connects.
- The read-only and reporting servers of the database preferences: section 5.1.

### 5.1 Read-only and reporting servers (S1)

A read-only or reporting server whose preference names a server (`ReadOnlyServerCompName`, `ReportingServerCompName` not empty) connects
exactly as on main: its own server, database, user and password (`...MySqlPassHash`, the reversible `osd1$` form, as before) from the
preferences, and the old `SslMode` behaviour with its own CA preference (`ReadOnlyServerSslCa`, `ReportingServerSslCa`): `SslMode=none`
without one, `SslMode=VerifyCA;SslCa=<preference>` with one. It is outside the rule, on the owner's decision; the office's own business is
that server's account and TLS. next/dbsec-2's forced office CA and client certificate for these servers are undone.

The connections:

| Where | What it does now |
|---|---|
| `ConnectionStore` (OpenPmsDentalBusiness), the delegates `GetDentalOfficeReportServerFromPrefC` and `GetDentalOfficeReadOnlyServerFromPrefC` | main's code again, plus: a preference that names a server marks the connection (`CentralConnectionBase.MarkPreferenceServer`, `IsPreferenceServer`). Its CA is the preference's only (main fell back to the main connection's CA when the preference had none; that CA is now always the office CA, so the fallback would give such a server `VerifyCA` against the office CA, which no v0.0 office had). |
| `ConnectionStoreBase.SetDbT` (reports and read-only reads on their own thread: `ReportsComplex.RunFuncOnReportServer`, `RunFuncOnReadOnlyServer`, `DataAction`) | a marked connection under `DentalOfficeReadOnlyServer` or `DentalOfficeReportServer` goes to `DataConnection.SetDbTPreferenceServer`; every other connection (an empty preference, any `ConnectionStore.xml` entry, an override) goes to `SetDbT` and the rule. `ConnectionStoreBase.SetDb` refuses a marked connection: it is never the main connection. |
| `FormOpenDental.CheckProgramVersionsReportandReadOnly`, `CheckCorruptedReportandRead`, `CheckUpdateInProgressReportandRead` (start-up) | main's arguments again, built by `BuildPreferenceServerConnectionString` (only when the preference names a server). |
| The set-up windows' database lists (`FormReportSetup`, `UserControlServerConnections`, `UserControlEnterpriseReports`) | `DataConnection.CreateForPreferenceServer` with the window's CA field (Enterprise reports: the reporting server's CA preference), never this computer's client certificate. On main they took the main connection's CA by default. |
| `DataConnectionCancelable` (user queries on the reporting server, cancellable) | `NewMySqlConnectionWithoutPooling`: the registered string keeps its exemption; only `Pooling` is changed. |
| The Connection Lost window (`FormOpenDentalThreads.BeginDataConnectionLostThread`), for a connection lost on any thread | only tests the lost connection (`DataConnection.TestConnectionString`, the check `StartConnectionErrorRetry` runs). Upstream's `SetDb(e.ConnectionString)` made the lost string the main (static) connection, so a lost read-only or reporting connection became the connection of every thread without a context of its own; the window's thread keeps its context too, from which its exit handler starts the program's threads again (`SetThreads`). `DataConnection.SetDb(string)` refuses a registered string (`DbConnectionRefusedException`, before any connection). |

The safety points:

- **An empty preference is the main connection.** It is not marked, its values are the main connection's (server, user, password, CA,
  or its connection string), and it is checked by the rule. `BuildPreferenceServerConnectionString` and `CreateForPreferenceServer` throw
  `ArgumentException` for an empty server.
- **Nothing of the main connection goes to such a server.** Its password is the preference's (`SetDbTPreferenceServer` stores `""` for
  none, never null, so `DataConnection.GetMysqlPass` never falls back to the main connection's DPAPI password on that thread). The
  builder never writes a client certificate (`AllowPreferenceServerConnection` refuses a string with `CertificateThumbprint`,
  `CertificateStoreLocation`, `CertificateFile`, `SslCert` or `SslKey`), nor the office CA in place of the preference's CA. Values are
  quoted (`QuoteValue`), so a preference cannot add an option such as a certificate or another server. On a thread whose context is such a
  server (`_isPreferenceServerT`), what the thread builds again for that server from its context (`GetLowConnectionString`, the lower user,
  `DataConnection(string database)`, a new `CommandTimeout`) is built the same way; a new `CommandTimeout` there changes the thread's
  connection only, never the main one. A child thread (`ODThread`) inherits the string and keeps its way through.
- **The main connection never skips the rule through this.** The exemption is a set of exact strings that only
  `BuildPreferenceServerConnectionString` adds to; nothing is exempt because of its server, user, options or anything else it contains. The
  main connection's strings come from `BuildSimpleConnectionString`, which writes `SslMode=VerifyFull` for every server over the network,
  so they never equal one (the builder of preference servers writes only `none` or `VerifyCA`). A `ConnectionString` element in
  `FreeDentalConfig.xml` that is exactly such a string is still refused for a server over the network (`GetConnectionStringProblem`,
  before it is used), and `IsPreferenceServer` can never come from a file (`XmlIgnore`, no public setter). Only marked connections under
  the two preference names are connected this way, and only on their own thread: nothing sets the main (static) connection to such a
  string. `ConnectionStoreBase.SetDb` refuses a marked connection, `DataConnection.SetDb(string)` refuses a registered string (as the main
  string or as its lower user's), and the Connection Lost window only tests a lost connection (`TestConnectionString`) without setting the
  main connection or its own thread's context. The `SetDb` overload with server, database and user builds with `BuildSimpleConnectionString`
  and is under the rule.

Tests: `tests/DbConnectionSecurityTests/PreferenceServerTests.cs`, `PreferenceServerDatabaseTests.cs` (the local MariaDB, scratch
databases `test_dbsec_*`).

## 6. The installer (`installer/OpenPmsDental.iss`)

- The connection page asks server, database and user (defaults `localhost`, `opendental` and, since S2, `opmsd`, the guide's account;
  the page says the user is the program's own account, not `root`); a second page asks the office CA certificate file and the client
  `.pfx` (blank: a database password instead). Both are skipped when the program folder already has `FreeDentalConfig.xml`. The second page
  is skipped for a loopback server and for per-user installs.
- Silent switches: `/DBSERVER=`, `/DBNAME=`, `/DBUSER=` (as before; `/DBUSER` defaults to `opmsd`), `/DBCA=<file>`, `/DBPFX=<file>`,
  `/DBTHUMBPRINT=<hex>`, `/DBSECRETSFILE=<file>` (passed to the tool; the caller deletes it). `root` for a server over the network is
  accepted; the tool's result line in setup's log carries the warning of section 4.5.
- Setup writes the base file as before (no password) and no longer gives Users modify rights on it.
- All-users (administrator) installs, new connection (no `FreeDentalConfig.xml` yet), after the files are installed (`ssPostInstall`):
  - a server over the network, or any security switch: setup runs the tool with the pages' values. Interactive, its window opens filled
    with them and asks for the `.pfx` password or the database password; silent, `/silent` with the secrets file.
  - a loopback server without security switches, interactive: the tool's window opens filled with the pages' values, in password mode
    (`/auth=password`), so that the database password of a server PC or a single-computer office is stored with DPAPI (Release has no
    Choose Database to ask for it). Cancel keeps the file without a password, which suits a database user with a blank password.
  - a loopback server without security switches, silent: no tool (`/DBSECRETSFILE=` with `DbPassword=` runs it, `/silent`).
  - `/log=` points into setup's temporary folder and the tool's result line goes into setup's log. A failure is shown (suppressible) and
    the install finishes; the program then shows the run-the-tool message. A silent install then ends with exit code 10
    (`DbSettingsFailedExitCode`, through `GetCustomSetupExitCode`; owner decision of 2026-11-02), so a deployment script sees it; Inno
    Setup's own codes are 0 to 8. Interactive installs end with 0.
- All-users installs, upgrade (the file exists; for the versions after this one, S4): setup never passes the connection page's defaults
  (`localhost`, `opendental`, `opmsd`).
  - Any of `/DBCA=`, `/DBPFX=`, `/DBTHUMBPRINT=`, `/DBSECRETSFILE=` given: setup runs the tool with only the switches that were given
    (`/DBSERVER=`, `/DBNAME=`, `/DBUSER=` too, when given): silent, `/silent`; interactive, the window. A failure is shown (suppressible)
    and logged; a silent install ends with exit code 10.
  - No security switch: setup runs `/check` and, interactive, offers to open the tool when the settings fail the rule; silent, it logs the
    result and, when the settings fail the rule or the tool could not start, ends with exit code 10.
- Per-user installs (`/CURRENTUSER`, tests only) cannot run the administrator tool: they write the base file; a network server is logged
  ("needs the connection settings tool") and shown as needing the tool; setup still ends with exit code 0.
- v0.0 is uninstalled before this version is installed (S4), and its uninstaller removes the program folder with the
  `FreeDentalConfig.xml` to which its setup gave Users modify rights, so no computer keeps that right, on the condition of section 3.1:
  the release is published only after v0.0 is uninstalled on every workstation, and a workstation that pulled it anyway is uninstalled
  through Apps before setup runs. Setup still never rewrites an existing file, for the upgrades of later versions. The tool's save sets the
  file's rights back to the program folder's (section 3, owner decision of 2026-11-02), so a v0.0 file that a pull kept in place loses its
  Users modify right at the first save with the tool; until that save it keeps it.

## 7. Paths reviewed

The connection paths. "Rule" is the program's own account under the rule (sections 2 and 5); "as before" is a read-only or reporting
server of its own (section 5.1).

| Path | Shipped | Result |
|---|---|---|
| `DataConnectionBase` (`DataConnection`, `MySqlHelper`, `ConnectionStoreBase`) | yes | Rule: through `NewMySqlConnection`, strings from `BuildSimpleConnectionString`. As before: `BuildPreferenceServerConnectionString`, `SetDbTPreferenceServer` and `CreateForPreferenceServer`, for marked connections only. |
| Main connection: start-up (`CentralConnections.TryToConnect`), Choose Database (Debug only), the command line | yes | Rule. Settings through `DbConnectionConfig`; a command-line database password refused for a server over the network (S3). |
| Reports (`ReportsComplex.RunFuncOnReportServer`, `RunFuncOnReadOnlyServer`, `DataConnectionCancelable`, ODR, the RDL hook) with no read-only or reporting server preference | yes | Rule: the main connection (`ConnectionStore`'s unmarked connection). |
| The same with a server in the read-only or reporting preference | yes | As before (section 5.1): `ConnectionStore` marks it, `ConnectionStoreBase.SetDbT` connects it with its own server, user, password and CA preference. The ODR and RDL connections of such a thread reuse its string, which passes as registered. |
| Start-up checks of those servers (`FormOpenDental`: program version, corrupted database, update in progress) and their set-up windows' database lists | yes | As before (section 5.1). |
| The Connection Lost window (`FormOpenDentalThreads.BeginDataConnectionLostThread`) | yes | Tests the lost connection only (`TestConnectionString`); never sets the main connection, so a lost read-only or reporting connection stays on its own thread (section 5.1). |
| `OpenPmsDental.exe` (eCW diagnostics, RDL allocators, self-update) | yes | Rule. The eCW diagnostics build `Server=`+the eCW server property with `SslMode=none`, so they reach a loopback eCW database only. |
| Backup to another server (`FormBackup`'s archive tab, `MiscData.MakeABackup(serverName,user,pass)`) | yes | Rule, with the user and password typed there and this computer's office CA and certificate. |
| `ODR.dll` | yes | The program's string (the thread's), or `DbConnectionConfig` + `BuildSimpleConnectionString`. |
| `RdlEngine.dll` | yes | `RdlEngineConfig.CheckConnection` hook. (`Definition/DataSource.cs` is not compiled.) |
| `OpenPmsDentalDbSettings.exe` | yes | Rule: Test connection and `/test` build with the tool's own values (`DbSettingsRunner.TestConnection`). |
| `ServiceManager.exe` | yes | Rule: test connection through `DataConnection.SetDb` with this computer's CA and certificate. The service configuration files it writes are for services this fork does not ship and are unchanged. |
| `UpdateFileCopier.exe` | yes | Passes on what the program gave it; no password for a network server. |
| `OpenDentalServiceConfig.xml` (`PrefL.CreateConfigForOpenDentalService`, `ServicesHelper.CreateServiceConfigFile`) | written by the program | For the OpenDentalService, which this fork does not ship; unchanged (it still writes the osd1$ form). |
| Middle Tier server (`DtoProcessor`, `Userods`), `OpenPmsDentalHL7` | no | Connect through `DataConnection`, so the rule applies; their configuration files have no CA or certificate elements, so only a loopback database works for them. |
| `DatabaseIntegrityCheck` | no | Not in the setup, but a side program of the product (it links `SharedAssemblyInfo.cs`). Its connection goes through `NewMySqlConnection`, so with its `SslMode=none` it reaches loopback databases only. It does not get the local test server's CA (O-12, DB-TLS.md section 3: no `DevDbTls` in a product project), so it no longer reaches that server once the server requires TLS. |
| `CentralManager`, `MySQLManager` | no | Not in the setup, side programs of the product (they link `SharedAssemblyInfo.cs`); they connect through `DataConnection`, so the rule applies. `CentralManager` passes no CA to `SetDb`/`SetDbT` for a central connection; it does not get the local test server's CA either (O-12, as `DatabaseIntegrityCheck`), so it no longer reaches that server once the server requires TLS. |
| `DocumentationBuilder`, `xCrudGenerator` | no | Developer tools, never released and no product project (they do not link `SharedAssemblyInfo.cs`; a test checks it). `DocumentationBuilder` (in the solution filter since S1-5 of the .NET 10 plan) reads the config of a local Release build (`..\..\..\OpenPmsDental\bin\Release`), a loopback database, and creates its `MySqlConnection` directly; `xCrudGenerator` connects through `DataConnection.SetDb`. With O-12 both give the local test server the test CA through `DevDbTls` (`Apply`, `GetSslCa`), so it is reached with `SslMode=VerifyCA` once the CA exists; every other server as before (`xCrudGenerator`'s upstream server over the network is refused by the rule without an office CA). |
| `tools/SyntheticData`, `tools/AdoptionValidation` | no | Dev-only: they refuse any server but this computer themselves (`LocalOnly.EnsureLocalHost`, the adoption tools' loopback and SSH-forward checks). With the local test server's CA (O-12, below) they reach `127.0.0.1:3306` with `SslMode=VerifyCA`; the local end of an SSH forward as before. |
| `DataConnectionBase.DevDbTls` (O-12, [docs/port-notes/DB-TLS.md](../port-notes/DB-TLS.md)) | in `DataConnectionBase.dll`, never called by a shipped binary or a product project (a test checks it) | For the development set-up, the test helpers, the dev tools and the developer tools `DocumentationBuilder` and `xCrudGenerator` only: the local test server (`localhost` or a loopback address on port 3306) gets `SslMode=VerifyCA` with the test CA of `tools/Enable-LocalDbTls.ps1` once it exists; the program's own loopback branch with a CA (`BuildSimpleConnectionString`, `SslMode=VerifyCA`) is what the helpers' `SetDb` calls build. The rule above does not change. |

## 8. Open points for the owner

Answered on 2026-11-02:

- The former question 1 (the read-only and reporting servers' passwords and CA): S1. They keep their passwords in the database
  preferences (`ReadOnlyServerMySqlPassHash`, `ReportingServerMySqlPassHash`, the reversible `osd1$` form) and their own CA preferences,
  outside the rule, as in v0.0 (section 5.1). Such a server without a CA preference is reached without TLS, and with one by `VerifyCA`
  (the chain, not the host name), as before.
- The former question 2 (the Users modify right of a v0.0 `FreeDentalConfig.xml`): S4. v0.0 is uninstalled first, which removes that file,
  and the release is published only when v0.0 is uninstalled on every workstation, so that no v0.0 pulls it in place (section 3.1).
- The former question 3 (`sql_mode` at start-up): the program keeps stopping when it must set `sql_mode` and cannot, and its message
  names the one-line fix of the guide, `sql_mode=""` in the `[mariadb]` section of the server's option file
  (`MiscData.GetSetSqlModeFailedMessage`).
- The replaced client certificate: the tool removes it, with its private key, but only one it imported itself (section 4.3).
- A silent setup whose connection settings step fails ends with exit code 10 (section 6).
- `FreeDentalConfig.xml`: every save with the tool sets its rights back to the program folder's (section 3).
- The read-only and reporting servers keep their old TLS behaviour (S1, section 5.1): nothing changes.
- The former open point 1 (the account rights of section 9 untested): tested on the local MariaDB 10.11 with a temporary account on
  2026-11-02 (section 9.5), not yet on the office server. The owner took the test's recommendations as they stand: `INDEX` is out of the
  standing grant (granted only to convert an Open Dental database older than 17.4.46, section 9.4), the guide removes MariaDB's default
  rights on `test` databases before it creates the account, and it says what the backup, `DROP` and `CREATE` grants allow beyond what the
  program does.
- The next open point 1 (the database-level rights not shown by refusal, because the development server's `PUBLIC` role holds nearly every
  right on `test` and `test\_%` databases): the owner allowed a test with a temporary account on an office database named outside
  `test\_%`, an exception to the scratch-database naming (answer of 2026-11-02). The second test of section 9.5 did that: every right of
  the three grants was shown to be needed by refusal, and every right they leave out was refused. The grants stay as they are. One claim
  of the guide was wrong and is corrected: MariaDB checks `CREATE` and `INSERT` for an `ALTER TABLE` only when it renames the table, not
  for every `ALTER TABLE` (section 9.1). The test is kept as `tests/DbGrantsTests`, to run again before the switch-over.
- The test found one program bug, fixed: Database Maintenance's `ProcedurelogTpAttachedToClaim` had an SQL syntax error since Open
  Dental 24.3.22 (a column line without a comma), so it failed in Check and Fix on every database, and the maintenance window, which stops
  at the first error, never ran the methods after it.

Both answered by the owner on 2026-11-07 (`docs/owner-questions.md`, "The program's database account", DB-SEC 1 and DB-SEC 2):

1. (DB-SEC 1) Two of the three server-wide rights exist only because of two old checks: every backup asks whether the server
   replicates (`SHOW MASTER STATUS`, `SHOW SLAVE STATUS`: `BINLOG MONITOR`, `REPLICA MONITOR`), and flushes each copied table
   (`FLUSH TABLE`: `RELOAD`). Treat "access denied" on the replication question as "no replication" and skip the flush, so that the
   account needs no server-wide right? **Answered: keep the three server-wide rights** (section 5 of the guide); the backup is unchanged.
2. (DB-SEC 2) The Backup window's archive tab can back up to another server first
   (`MiscData.MakeABackup(serverName,user,pass,doVerify:true)`), with an account of that server typed in the window. That path copied
   rows with `DataConnection.BulkCopy` (`MySqlBulkLoader`, a `LOAD DATA LOCAL INFILE`), which MySqlConnector refuses without
   `AllowLoadLocalInfile=true`, so the backup stopped at the first table with rows (second test, section 9.5). **Answered (the
   recommendation): copy the rows with `INSERT` statements.** Built: `MakeABackup` still reads each table in batches of 10,000 rows
   (`documentmisc` one row at a time) and still reports "Backing up table: <table>" and, with `doVerify`, the row counts; each batch
   is written with ordinary `INSERT` statements (`MiscData.GetBackupInsertStatements`: at most 1,000 rows and about 4 MB of data per
   statement, a larger row alone, every value a parameter, so NULLs, bytes, dates and numbers arrive as they were read). No connection
   sets `AllowLoadLocalInfile`, and `DataConnection.BulkCopy` is removed, so nothing writes a temporary file of the rows any more. The
   target account needs `CREATE`, `INSERT` and `SELECT` on the backup database, as for a backup on the same server. Tested on two
   scratch databases of the local MariaDB (`tests/DbConnectionSecurityTests/BackupToAnotherServerTests.cs`).

## 9. The rights of the program's database account (S2)

The guide ([docs/db-connection-security.md](../db-connection-security.md), section 5) gives the account, `opmsd` (and `opmsd_pw` for the
password fallback), the narrowest rights with which the program runs, updates the database and makes the backup before every update, in
place of `ALL PRIVILEGES ON *.*`. **Tested on a local MariaDB 10.11 server on 2026-11-02 (section 9.5), not yet on the office server:**
with these grants the program did everything it does, each right was shown to be needed by a refusal, and every right they leave out was
refused. The privilege names are MariaDB 10.11's (`BINLOG MONITOR` is 10.5.2's name for `REPLICATION CLIENT`; `REPLICA MONITOR`, alias
`SLAVE MONITOR`, exists from 10.5.9, and `SHOW GRANTS` prints it as `SLAVE MONITOR`). Line numbers are those of `next/fu-grants-1`, the
package of the second test. `<database>` is the office database.

```sql
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, CREATE TEMPORARY TABLES ON `<database>`.* TO ...;
GRANT SELECT, INSERT, CREATE, ALTER ON `<database>backup\_%`.* TO ...;
GRANT RELOAD, BINLOG MONITOR, REPLICA MONITOR ON *.* TO ...;
```

Before it creates the account, the guide removes the rights MariaDB's own setup can give the `PUBLIC` role, and so every account, on
`test`.* and `test\_%`.* (the 10.11 server on Windows of the test had them), and says never to give the office database such a name.

### 9.1 The office database

| Right | Why | Evidence |
|---|---|---|
| `SELECT`, `INSERT`, `UPDATE`, `DELETE` | Every module (the `Crud` classes); the connection test writes a preference, so without `UPDATE` the program does not start. `CHECK TABLE`, `REPAIR TABLE` and `OPTIMIZE TABLE` (database maintenance, the backup-repair-optimize after a MariaDB version change) need `SELECT` and `INSERT` only. | `DataConnectionBase/DataConnection.cs:816`; `OpenPmsDentalBusiness/Data Interface/DatabaseMaintenances.cs:256`, `:326`, `:368`; `DataConnectionBase/DataConnection.cs:877` |
| `CREATE` | Fork migration steps create tables; work tables of reports and maintenance; the large-table helper's copy (`CREATE TABLE ... LIKE`) and the new name of its `RENAME TABLE`, for which MariaDB also checks `INSERT`. An `ALTER TABLE` that renames nothing does not need it: MariaDB checks `CREATE` and `INSERT` only for a rename's new name (the second test added columns and keys without either). With `DROP`, it also allows creating the office database again. | `OpenPmsDentalBusiness/Misc/ForkMigrations/ForkMigrationContext.cs:67`; `DatabaseMaintenances.cs:11482`; `OpenPmsDentalBusiness/Db/LargeTableHelper.cs:585`, `:587`, `:757`, `:759`; `OpenPmsDentalBusiness/Data Interface/EhrMeasures.cs:533` |
| `DROP` | Dropping those work tables; `TRUNCATE` needs `DROP` (family aging, the A/R dashboard, recall triggers); database maintenance's `DROP TABLE IF EXISTS` of the old `signal` table; the large-table helper's `RENAME TABLE` and the `DROP TABLE` of its old copy. The helper copies a table only when the hidden preference `UpdateAlterLargeTablesDirectly` is off and the table needs more than one batch; a new database has the preference on, and the helper then runs one `ALTER TABLE`. A database-level `DROP` also allows `DROP DATABASE <database>`, the whole office database: there is no narrower right that allows the statements above, so backups kept off the server are the protection. | `DatabaseMaintenances.cs:239`, `:11491`; `EhrMeasures.cs:531`; `OpenPmsDentalBusiness/Data Interface/DashboardARs.cs:34`; `OpenPmsDentalBusiness/Data Interface/RecallTypes.cs:369`; `OpenPmsDentalBusiness/Db Multi Table/Ledgers.cs:114`; `LargeTableHelper.cs:734`, `:755`, `:757`, `:783` |
| `ALTER` | Fork migration steps (columns, keys, character sets), adoption, re-enabling the keys of a restored backup, dropping redundant indexes, converting a table to the default storage engine; every index the program adds or drops (`ALTER TABLE ... ADD INDEX`, `DROP INDEX`); `RENAME TABLE` of the large-table helper (also `DROP`, `CREATE`, `INSERT`, all on this database). | `ForkMigrationContext.cs:78`, `:107`, `:119`, `:133`, `:135`, `:147`, `:157`, `:168`; `OpenPmsDentalBusiness/Misc/ForkMigrations/Adoption.cs:210`, `:217`; `OpenPmsDentalBusiness/Db Multi Table/MiscData.cs:602`; `DatabaseMaintenances.cs:12083`; `LargeTableHelper.cs:555`, `:585`, `:716`, `:727`, `:757` |
| `CREATE TEMPORARY TABLES` | User queries (the query window, the query examples) may create temporary tables; the program lets them through. In the first test only the query window used it; the second showed a user query with `CREATE TEMPORARY TABLE` refused without it, and an index on such a table needs no `INDEX`. | `OpenPmsDentalBusiness/Db/Db.cs:104`, `:171` |

### 9.2 The backup databases

Before every database update the program copies the office database into a new database: `<database>backup_MM_dd_yyyy` (today's
date), or with `_1`, `_2`, ... added while that name exists. `MiscData.MakeABackup` (`MiscData.cs:177`, `:185` to `:188`). It runs:

- before the pending fork migration steps (Release builds): `OpenPmsDental/Logic/PrefL.cs:156`, through `Shared.MakeABackup`
  (`OpenPmsDental/Misc/ClassesShared.cs:122`, `:125`);
- before the frozen Open Dental update chain: `OpenPmsDental/Misc/ClassConvertDatabase.cs:160`, `:220`;
- after a MariaDB version change (backup, repair, optimize): `PrefL.cs:412`, and before the MySQL 4.1 conversion: `PrefL.cs:433`;
- from the Backup window, database maintenance, the InnoDB tool and the fee schedule tools: `OpenPmsDental/Forms/FormBackup.cs:281`,
  `OpenPmsDental/Forms/FormDatabaseMaintenance.cs:416`, `:531`, `OpenPmsDental/Forms/FormInnoDb.cs:51`, `:88`,
  `OpenPmsDental/Forms/FormFeeScheds.cs:334`, `OpenPmsDental/Forms/FormDatabaseMaintTemp.cs:89`.

A failed backup stops the update ("Backup failed. Your database has not been altered."). The grant `` `<database>backup\_%`.* `` (a
MariaDB wildcard: `\_` is a literal `_`, `%` any name) covers exactly these names:

| Right | Why | Evidence |
|---|---|---|
| `CREATE` | `CREATE DATABASE` of the backup, and its tables from `SHOW CREATE TABLE` of the office database (`SELECT` there). | `MiscData.cs:189`, `:203`, `:211` |
| `ALTER` | `ALTER TABLE ... DISABLE KEYS` when the `BackupIndexesDisabled` preference is on, which it is unless the office turned it off (a new database and the Open Dental update that added the preference both set `1`), so every default backup issues it, once for each table it copies. With the preference off a backup needs no `ALTER` (second test). | `MiscData.cs:212`, `:215`; `db/init/new-database.sql:25814`; `OpenPmsDentalBusiness/Misc/ConvertDatabases8.cs:2248` |
| `INSERT` | `INSERT INTO <backup>.<table> SELECT * FROM <database>.<table>`. | `MiscData.cs:221` |
| `SELECT` | The row counts that verify the copy, only in `MakeABackup(doVerify:true)` (the archive tab's backup). Every other backup runs without it (second test). | `MiscData.cs:258` |

`SHOW DATABASES` (`MiscData.cs:178`) needs no right: it lists the databases the account has rights on, so it sees the existing backup names
of the wildcard. What the grant allows beyond the program's use: the account can create any database whose name starts with
`<database>backup_`, fill it, add tables to it, read every backup and alter the backups' tables (`ALTER` includes `DROP COLUMN` and
changing the engine). It cannot update, delete, truncate, drop or rename anything in them. The second test showed each of these. The
account never drops a backup; the office's administrator does.

### 9.3 Server-wide rights

Each of the three was shown to be needed in both tests (section 9.5): without any one of them every backup fails.

| Right | Why | Evidence |
|---|---|---|
| `RELOAD` | `FLUSH TABLE <backup>.<table>` after each table the backup copies on the same server; `FLUSH TABLES` before the Backup window's file copy. | `MiscData.cs:226`; `MiscData.cs:598` through `FormBackup.cs:307` |
| `BINLOG MONITOR` | Every backup on MariaDB first asks whether the server replicates: `SHOW MASTER STATUS`, when the program's `replicationserver` table is empty and the `DatabaseGlobalVariablesDontSet` preference is off. An "access denied" fails the backup. The replication check of the frozen update chain asks the same (it issued both statements in the test). | `MiscData.cs:153`; `OpenPmsDentalBusiness/Data Interface/ReplicationServers.cs:282`, `:286`, `:290`; `OpenPmsDentalBusiness/Misc/ConvertDatabases7.cs:28` |
| `REPLICA MONITOR` | The same question, `SHOW SLAVE STATUS`. | `ReplicationServers.cs:292`, `:271`; `ConvertDatabases7.cs:30` |

### 9.4 Rights the account does not get

| Not granted | Where the program would need it | Why it is left out |
|---|---|---|
| `SUPER` | `SET GLOBAL sql_mode=''` at every start when the server's `sql_mode` is neither blank nor `NO_AUTO_CREATE_USER` (`MiscData.cs:552`, `:557`; `OpenPmsDental/Main Modules/FormOpenDental.cs:1288`, failure message `:1293`). `SET GLOBAL max_allowed_packet` (`MiscData.cs:472`) has no caller. | The guide sets `sql_mode=""` in the server's option file, so the statement never runs (section 8, the former question 3). Without `SUPER` it is refused (1227), so the option-file line is required. |
| `INDEX` | `CREATE INDEX` and `DROP INDEX ... ON` of three Open Dental steps on MariaDB: `ConvertDatabases5.cs:7024` (17.4.46), `:7031`, `:7034` (17.4.51), `:7065` and `:7073` (17.4.53, whose failure is ignored). Every other `CREATE INDEX` of `ConvertDatabases1.cs` to `ConvertDatabases5.cs`, `DbSchema.cs` and the table types is in an Oracle branch, `ConvertDatabases1.cs:4970` among them. The program's own index changes are `ALTER TABLE` (9.1, `ALTER`). A user query's `CREATE INDEX` on its own temporary table (`Db.cs:171`) is not privilege-checked (MariaDB's documentation; the second test confirmed it). | Only to convert an Open Dental database older than 17.4.46: the guide grants it for that first start and revokes it after, or the administrator runs that start. No statement of either test needed it, the chain from 21.1.9, the adoption, the fork steps, database maintenance and backups included; the 17.4.46 step itself was refused without it (1142) and allowed with it. |
| `BINLOG ADMIN` | `SET SESSION sql_log_bin=0` around the backup's statements, only on a replicating MariaDB server (`MiscData.cs:114`, used at `:204`, `:222`, `:231`, `:242`). Refused without it (1227). | Only a replicating office; the guide says to add it there. |
| `REPLICATION SLAVE ADMIN` | `STOP SLAVE; START SLAVE` of the replication set-up window (`OpenPmsDental/Forms/FormReplicationSetup.cs:186`). | An administrator's task, with the administrator's own account. The replication check of the frozen update chain (`ConvertDatabases7.cs:28`, `:30`, used at `:1562`) needs the two monitor rights above. |
| `PROCESS`, `CONNECTION ADMIN` | No `SHOW PROCESSLIST` or `information_schema.PROCESSLIST` in a shipped binary. Cancelling a user query is `KILL QUERY` of the account's own connection (`OpenPmsDentalBusiness/Db/DataConnectionCancelable.cs:147`): it works, and `KILL QUERY` of another account's connection is refused (1095). | Not needed. |
| `FILE` | No `SELECT ... INTO OUTFILE` or `LOAD DATA` for the program's account (refused, 1227 and 1045; `LOAD_FILE` returns NULL). Nothing uses `MySqlBulkLoader` or `LOAD DATA LOCAL INFILE` since 2026-11-07: a backup to another server copies its rows with `INSERT` statements (section 8, DB-SEC 2). | Not needed. |
| `SHOW DATABASES` | Choose Database's list (`OpenPmsDentalBusiness/Data Interface/CentralConnections.cs:197`) is Debug only and lists what the account may use. | Not needed. |
| `CREATE USER`, `GRANT OPTION` | `DbAdminMysql` (`DataConnectionBase/DbAdminMysql.cs:164`, `:241`) is used only by MySQLManager, which is not shipped. | Not needed. |
| `CREATE`, `DROP` of other databases | `<database>_archive` (`MiscData.cs:281`, `:335`) has no caller. `LargeTableHelper.RenameDatabase` (`LargeTableHelper.cs:175`, `:197`) runs only in an Open Dental 18.x step of the frozen chain (`ConvertDatabases5.cs:8804`). | Not needed for a 24.3 or later database. |
| `LOCK TABLES`, `CREATE VIEW`, `SHOW VIEW`, `TRIGGER`, `CREATE ROUTINE`, `ALTER ROUTINE`, `EXECUTE`, `EVENT`, `REFERENCES` | No statement of a shipped binary; the only trigger statement is Oracle's (`OpenPmsDentalBusiness/Db/DbSchema.cs:408`). `LOCK TABLES`, `CREATE PROCEDURE`, `CREATE EVENT` (1044), `CREATE VIEW` and `CREATE TRIGGER` (1142) are refused. | Not needed. |
| `SELECT` on `mysql.*` | `mysql.wsrep_cluster` only on a Galera cluster with `wsrep_on=ON` (`ReplicationServers.cs:302`). `SELECT` on `mysql.user` and `mysql.db` is refused (1142), `SHOW GRANTS` for another account too (1044). | Not needed without Galera. |

A read-only or reporting server of its own (section 5.1) has its own account on its own server; its rights are the office's business
and not part of this list.

### 9.5 The tests on a local MariaDB 10.11 (2026-11-02)

The owner allowed testing the rights on the local MariaDB 10.11 (Windows) of the development computer with a temporary account. In both
tests the account had exactly the grants above, on a scratch office database and its backups, with synthetic data; the account and every
database of the test were dropped afterwards, and no other user, role or server setting was changed. The server's global `sql_mode` was
blank and binary logging was off.

**The first test**, on scratch databases named `test_int_...`:

- **Covered.** Start-up; the modules' reads and writes; work tables; user queries, and cancelling them with `KILL QUERY`; the fork
  migration runner and each of its context helpers; the large-table helper; four backups (the default, `BackupIndexesDisabled` off, with
  verification, and not as an automatic backup); `CHECK`, `OPTIMIZE` and `REPAIR` of every table; all 201 database maintenance methods,
  in Check and in Fix; the release channel; and the frozen Open Dental chain from 21.1.9 to 24.3.63, followed by the adoption with every
  fork step. 19,422 statements ran, and none was refused.
- **Server-wide rights, shown by refusal.** Without `RELOAD`, `FLUSH TABLE` of a backup table and the Backup window's `FLUSH TABLES` are
  refused (1227), and all four backups fail at their first table. Without `BINLOG MONITOR`, `SHOW MASTER STATUS` is refused in
  `ReplicationServers.IsUsingReplication`: the database maintenance window's replication check fails, and so does every backup, before
  `CREATE DATABASE`. Without `REPLICA MONITOR`, `SHOW SLAVE STATUS` is refused in the same places.
- **Database-level rights: by statement, not by refusal.** The server's `PUBLIC` role holds nearly every right on `test`.* and
  `test\_%`.* (MariaDB's own setup gave it), and the test's databases fall under `test\_%`, so revoking a database-level right refused
  nothing. The second test was made for that.
- **The host.** `'opmsd'@'localhost'` matched TCP connections to `127.0.0.1` on a server that resolves names (`skip_name_resolve=0`), as
  the guide's note on the server computer says. The second test found the same.

**The second test**, `tests/DbGrantsTests`: the account `'opmsd_granttest'@'localhost'` with a random password, the office database
`opmsdgrant_od` built from `db/init/new-database.sql`, its backups `opmsdgrant_odbackup_...`. The names are outside `test%`, so the
`PUBLIC` role's rights did not apply, and the test refuses to start when they would. Everything ran as the account through the program's
own code (`DataConnection` and the connection rule; a loopback server, so without TLS). 616 checks, none failed.

- **Allowed with exactly the grants.** The start-up as far as it runs without a window: the connection test, the preference cache,
  `PrefL.CheckForkVersionGuard`, the indexes of a restored backup, `PrefL.CheckMySqlVersion`, `MiscData.SetSqlMode` (nothing to set),
  the fork migration hand-off of `PrefL.RunForkMigrations` with the backup a Release build makes first and ten pending steps (2026110201
  to 2026110213: the routine maintenance and inventory tables created again, the stock ledger's opening balance written),
  `PrefL.CheckProgramVersion`, the computer's preferences, every cache, the log-on and the signals. Reads and writes on `patient`,
  `commlog`, `adjustment`, `procedurelog`, `appointment`, `definition`, `preference`, `securitylog` and `signalod`. `TRUNCATE` of the
  A/R dashboard, the recall triggers and family aging; a user query with a temporary table and an index on it; cancelling a running user
  query; every `ForkMigrationContext` helper; the keys of a restored backup re-enabled; the redundant index tool; the conversion of a table
  to the default storage engine; the large-table helper both ways (one `ALTER TABLE`, and with `UpdateAlterLargeTablesDirectly` off a
  table of three batches renamed, copied and dropped); the replication question. Database maintenance: the table check, all 201 methods
  in Check and in Fix with their last-run dates, the tools that work on the database (special characters, nulls, the Etrans work table,
  active treatment plans, missing patients, ortho chart duplicates, estimates, payment plan payments, duplicate e-mails, insurance plan
  types) and repair and optimize of every table. Backups: the default one, with `BackupIndexesDisabled` off, not as an automatic backup
  (the Backup window), the archive tab's with the row counts on the same server, the backup-repair-optimize of a MariaDB version change,
  and `FLUSH TABLES`. The release channel: publishing (twice), reading the manifest, downloading and checking the release, deleting Open
  Dental's update files. The Open Dental update chain from 21.1.9 to 24.3.63 (after its backup), `Adoption.Preflight` and `Adoption.Run`
  (the office's generated patient column and its unique key dropped, every fork migration step), the version guard and the 26.1.70
  schema verifier, which found an exact match.
- **Each granted right is needed.** Revoked alone, each made these calls of the program fail with "access denied", and they were allowed
  again once it was granted back:

  | Right | Refused without it |
  |---|---|
  | office `SELECT` | the preference cache, `Patients.GetPat` (1142) |
  | office `INSERT` | `Commlogs.Insert`, `OPTIMIZE TABLE`, `ALTER TABLE ... RENAME TO`, the large-table helper's `RENAME TABLE` (1142) |
  | office `UPDATE` | the start-up's connection test, so the program cannot start; `Prefs.UpdateString` (1142) |
  | office `DELETE` | `Commlogs.Delete` (1142) |
  | office `CREATE` | `ForkMigrationContext.CreateTableIfMissing`, the Etrans tool's work table, `ALTER TABLE ... RENAME TO`, the large-table helper's `RENAME TABLE` (1142) |
  | office `DROP` | `TRUNCATE` of the A/R dashboard and of family aging, the Etrans tool's `DROP TABLE IF EXISTS`, `ALTER TABLE ... RENAME TO`, the large-table helper (1142) |
  | office `ALTER` | `AddColumnIfMissing`, `EnsureIndex`, re-enabling the keys of a restored backup, the large-table helper (1142) |
  | office `CREATE TEMPORARY TABLES` | a user query with `CREATE TEMPORARY TABLE` (1044) |
  | backup `CREATE` | every backup, at its `CREATE DATABASE` (1044) |
  | backup `INSERT` | every backup, at its first `INSERT ... SELECT` (1142) |
  | backup `ALTER` | the default backup, at its first `DISABLE KEYS` (1142); with `BackupIndexesDisabled` off the backup was allowed |
  | backup `SELECT` | the archive tab's backup, at its row counts (1142); the default backup was allowed |
  | `RELOAD` | every backup (`FLUSH TABLE`), the Backup window's `FLUSH TABLES` (1227) |
  | `BINLOG MONITOR` | every backup and the maintenance window's replication check (`SHOW MASTER STATUS`, 1227) |
  | `REPLICA MONITOR` | every backup, `ReplicationServers.GetSlaveStatus` (`SHOW SLAVE STATUS`, 1227) |

- **An `ALTER TABLE` needs `ALTER` only.** Without `INSERT`, without `CREATE`, and without both, `ALTER TABLE` adding a column and a key
  and changing a column's definition was allowed: MariaDB checks those two rights only for the new name of a rename. The guide said every
  `ALTER TABLE` needs them; corrected (section 9.1). The grants do not change: `CREATE` and `INSERT` are needed for the reasons above.
- **Refused, as the guide leaves them out.** `INDEX`: `CREATE INDEX` (Open Dental's 17.4.46 step itself, invoked as the chain invokes
  it) and `DROP INDEX ... ON` (1142); granted for a first start as the guide says, both were allowed, and refused again once it was
  revoked. Other databases: `CREATE DATABASE` of `<database>_new`, of `<database>backupx1` (an unescaped `_` in the grant would cover it)
  and of `<database>backup`, `DROP DATABASE` and `USE` of another database (1044), `SELECT` from it (1142). In a backup: `UPDATE`,
  `DELETE`, `TRUNCATE`, `DROP TABLE`, `RENAME TABLE`, `ALTER TABLE ... RENAME` (1142) and `DROP DATABASE` (1044); `SELECT`, `INSERT`,
  `CREATE TABLE` and `ALTER TABLE ... DROP COLUMN` were allowed. `SELECT` on `mysql.user` and `mysql.db` (1142), `SHOW GRANTS` for another
  account (1044); `SELECT ... INTO OUTFILE` (1227) and `LOAD DATA INFILE` (1045), while `LOAD_FILE` returned NULL; `GRANT` (1044);
  `CREATE USER`, `SET GLOBAL sql_mode`, `SET SESSION sql_log_bin` and `STOP SLAVE` (1227); `KILL QUERY` of another account's connection
  (1095), while `information_schema.PROCESSLIST` listed the account's own connections only; `LOCK TABLES`, `CREATE PROCEDURE`, `CREATE
  EVENT` (1044), `CREATE VIEW` and `CREATE TRIGGER` (1142).
- **The office database itself.** As the account, `DROP DATABASE <database>` and `CREATE DATABASE <database>` were allowed, as the guide
  warns.
- **`PUBLIC` seen.** `SHOW DATABASES` as the account listed `test`, which only the `PUBLIC` role's rights make visible to it.
- **Stopped for other reasons.** The archive tab's backup to another server stops at its first table with rows before any right is
  checked (section 8, open point 2). The EHR measures, whose reports also build work tables, need an EHR provider key, so the test used
  the Etrans tool's work table instead. Database Maintenance's `ProcedurelogTpAttachedToClaim` failed with a syntax error on every
  database; fixed (section 8).
- **What neither test could show.** An office server: its MariaDB version, a `sql_mode` that is not blank (only `SET GLOBAL sql_mode`
  itself was shown refused), binary logging or replication (`BINLOG ADMIN`), TLS (the tests connected over loopback), and an office's
  own data and preferences. Try the grants on a test copy before the switch-over.

**Running the second test again**, before the switch-over, on the local MariaDB of a development computer where `root` has a blank
password: build `DbGrantsTests`, set `DBGRANTS_ACCOUNT` and `DBGRANTS_DATABASE` to names that exist nowhere on that server (the test
refuses to start otherwise, or when the `PUBLIC` role covers the database name), optionally `DBGRANTS_REPORT` (the report file; default
`%TEMP%\dbgrants-report-<time>.txt`) and `DBGRANTS_SKIP_CHAIN=1` (without the update chain and the adoption), and run the test assembly
(`vstest.console.exe tests\DbGrantsTests\bin\Debug\DbGrantsTests.dll /Platform:x86`). Without the two names the test is inconclusive and
connects nowhere, so the full test runs pass it by. It took about a minute here; the report lists every check with MariaDB's answer.
