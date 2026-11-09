# Port note: DB-TLS (O-12, the local test database runs encrypted)

Package O-12 of the .NET 10 plan (`docs/specs/net10-plan.md` section 12, O-12, answered by the owner on 2026-11-08: yes), built on branch
`dev/dbtls-1` from `main` (99bb32282, with `main` merged in at e1262eab8); the review's fixes on `dev/dbtls-2` (section 8), and there the
fix round after the two final reviews (section 9, with `main` merged in at 447b09374: S1-6's SDK-style test projects and TC-2). The owner
runs one script once as administrator,
`tools/Enable-LocalDbTls.ps1`, which turns on TLS in the local MariaDB service on `127.0.0.1:3306`. From then on the program's development
set-up, the test helpers and the dev tools connect encrypted and verify the server against the script's test CA. S2-2's TLS and
certificate-login test runs against this server, so O-8's temporary second server is not needed.

The program's own connection rule (`docs/specs/db-connection-security.md`) does not change. Nothing in this package changed the MariaDB
server, its option file or its service: the survey only read through SQL and files, and the script has not been run for real.

## 1. What this PC has (read-only survey, 2026-11-08)

Read through SQL on `127.0.0.1:3306` (the dev connection, `root`) and from files; nothing was changed.

| What | Found |
|---|---|
| Service | `MariaDB`, running, automatic start, account `NT SERVICE\MariaDB` (a virtual service account). Command line: `mysqld.exe "--defaults-file=<install folder>\data\my.ini" MariaDB`, the MariaDB 10.11 installer's layout (`%ProgramFiles%\MariaDB 10.11`). |
| Version | `10.11.19-MariaDB`, `version_compile_os` Win64. |
| TLS library | `version_ssl_library` = `WolfSSL 5.9.1` (MariaDB's Windows builds are linked with wolfSSL; `have_openssl` = `NO`). |
| TLS state | `have_ssl` = `DISABLED` (compiled in, not configured); `ssl_ca`, `ssl_capath`, `ssl_cert`, `ssl_key`, `ssl_cipher`, `ssl_crl`, `ssl_crlpath` empty; `tls_version` = `TLSv1.2,TLSv1.3` (the 10.11 default); `require_secure_transport` = `OFF`. The dev session's `Ssl_cipher` is empty: today every connection is unencrypted. |
| Option file | `my.ini` in the data folder, 633 bytes, ASCII, LF line ends (one CRLF), sections `[mysqld]` and `[client]`; `port=3306`, `bind-address=127.0.0.1` (only this computer can connect), `sql_mode=''` and the other Open Dental settings; no TLS line. ACL: inherited from the data folder (SYSTEM, Administrators, `NT SERVICE\MariaDB` and the developer's account full control, Users read). |
| Own certificate | None. MariaDB 10.11 does not make one; only MariaDB 11.4 and later generate a self-signed certificate at start-up when none is configured. |
| Tools that make PEM files without a download | Windows PowerShell 5.1 on .NET Framework 4.8 (release 534296): `System.Security.Cryptography.X509Certificates.CertificateRequest` and `SubjectAlternativeNameBuilder` (.NET Framework 4.7.2 and later) make a CA and certificates in memory, and an ephemeral `RSACng` key exports as PKCS#8 (`CngKey.Export(CngKeyBlobFormat.Pkcs8PrivateBlob)`, export policy `AllowPlaintextExport`). `New-SelfSignedCertificate` (PKI module) and `certutil.exe` exist too, but they work through the certificate store (keys land in a store before they can be exported). MariaDB's `bin` folder has no `openssl.exe`. No C# step through the build lane is needed. |
| Client | `mariadb.exe` 15.1 (10.11.19) in the same `bin` folder: `--ssl` on by default (falls back to plain when the server has no TLS), `--ssl-ca`, `--ssl-verify-server-cert` (off by default), `--tls-version`. |
| Host names | An in-process TLS handshake on a loopback port (no database) with a certificate for `localhost` and `127.0.0.1` from such a CA: .NET Framework's `SslStream` reports no name mismatch for the target `127.0.0.1`, only the expected untrusted-root error, which MySqlConnector's `SslCa` check then accepts. So `SslMode=VerifyFull` works for the IP address as well as for `localhost`. |
| Lane and test scripts | `run-tests.ps1` (the repository's and the coordinator's copy) and the build lane do not connect to the database. They need no change: the switch below is an environment variable, which the test processes inherit. |

## 2. The server side: `tools/Enable-LocalDbTls.ps1`

ASCII, Windows PowerShell 5.1. It changes nothing before it has printed its plan and you have typed `YES`.

**Checks (every mode).** The session is elevated (else it refuses; `-DryRun` and `-TestCertificatesFolder` need no elevation). The service
(`-ServiceName`, default `MariaDB`) exists and runs `mysqld.exe` or `mariadbd.exe` with `--defaults-file=`. That `my.ini` is UTF-8 (or
ASCII) text whose bytes come back unchanged from the decoding (else it refuses with the offset of the first other byte: writing such a file
back would change bytes the script does not mean to touch), reads no other option file (a line starting `!include` or `!includedir` is
refused: options there are out of its sight), and says `port=3306` (or no port) and `bind-address=127.0.0.1`. The process that listens on
`127.0.0.1:3306` (`Get-NetTCPConnection -LocalPort 3306 -State Listen`, no elevation needed) is the service's own (`Win32_Service`
`ProcessId`), and no other process listens on `0.0.0.0` or `::` port 3306, which would take the connections to `127.0.0.1:3306` while the
service's socket is closed; this is checked before the client sends anything there and again after every restart (the restart waits until
it holds). The server that answers on `127.0.0.1:3306` reports port 3306, bind address `127.0.0.1` and that `my.ini`'s data folder. Every
connection of the script goes to `127.0.0.1:3306` over TCP with the service's own `mariadb.exe`, as `-User` (default `root`), the password
from `MYSQL_PWD` when set.

**Enable (no mode switch).** Refuses when `my.ini` already has a TLS line (`ssl`, `ssl_ca`, `ssl_cert`, `ssl_key`, `ssl_capath`,
`ssl_cipher`, `tls_version`, `require_secure_transport`), when the server already has TLS, or when a backup, a certificate folder or a
temporary `my.ini.opms-new` of an earlier run exists. It refuses a dev folder or a client folder (steps 3 and 4) that

- is not `<a user profile's %LOCALAPPDATA%>\OpenPmsDental\DevDbTls` (`DevDbTlsClient` for the client folder) of a profile of this computer
  (the registry's profile list, readable without elevation, and the running account's own), in an `AppData\Local` that exists; these are
  the only folders `-Revert` and `-RequireSecureTransport` accept from the record (below), so an enable run never records one they would
  refuse;
- is a drive root, is or holds a user profile, or is or overlaps the MariaDB folder, the server's data folder or the `opms-dev-tls` folder;
- exists and holds anything but the files the script writes there (`ca.pem`; `client-cert.pem` and `client-key.pem`), a subfolder
  included, so the run replaces only its own files and its ACL hides nothing of anyone else's;
- is inside the repository, or (the client folder) is the dev folder, inside it or around it.

Then:

1. Makes in memory a test CA (RSA 3072, SHA-256, 10 years, `CA:true, pathlen 0`, keyCertSign and cRLSign) and a server certificate
   (RSA 2048, 5 years, `CN=localhost`, SAN `DNS:localhost` and `IP:127.0.0.1`, serverAuth). The CA's private key is never written
   anywhere: nothing else can ever be signed with it. **No client certificate by default**: the owner approved a test CA and a server
   certificate; S2-2's client certificate (RSA 2048, 5 years, `CN=opms-dbtls-test-client`, clientAuth) is made only with
   `-WithClientCertificate`, once the owner has said yes to it (section 10). Because the CA's key is gone after the run, a client
   certificate can only be made in the same run as the CA: a later yes means `-Revert` and the script again with the switch (a new CA,
   which the dev tools pick up from the same path).
2. Writes `ca.pem`, `server-cert.pem` and `server-key.pem` (PKCS#8) to `<MariaDB folder>\opms-dev-tls`, next to `bin` and `data`. Not inside
   `data`: MariaDB takes every folder there for a database. The folder and files get a protected ACL: owner Administrators, SYSTEM and
   Administrators full control, the service account (`NT SERVICE\MariaDB`) read, nobody else. These are the only keys the server reads.
3. Writes the CA certificate (the public part only) as `ca.pem` to `-DevFolder`, default `%LOCALAPPDATA%\OpenPmsDental\DevDbTls` of the
   account that runs the script. The folder is created when missing (and `OpenPmsDental` with it when that is missing too) and gets a
   protected ACL (nothing inherited): `-DevUser` (default that account), SYSTEM and Administrators full control, nobody else; an existing
   folder has its ACL replaced so. Every worktree of that account finds the CA there. **That folder never holds a private key**
   (`DevDbTlsTests.TheCaFolder_NeverHoldsAPrivateKey_...` checks it where the script ran).
4. Only with `-WithClientCertificate`: writes `client-cert.pem` and `client-key.pem` to `-ClientFolder`, default `DevDbTlsClient` beside
   the dev folder (`%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient`, `DevDbTls.GetDefaultClientFolder()`), a folder of its own, created when
   missing, with a protected ACL: `-DevUser` read only, SYSTEM and Administrators full control. Nothing is ever written to the repository.
5. Copies `my.ini` to `my.ini.before-opms-dev-tls` and adds at the end of its `[mysqld]` section (every other byte of the file stays as it
   was, line ends and a BOM included). The comment lines record where steps 3 and 4 wrote (the `client-folder` line only with
   `-WithClientCertificate`) and the parent folder the run creates for them (the `created-parent` line only when `OpenPmsDental` did not
   exist); `-Revert` and `-RequireSecureTransport` read them back:

   ```ini
   # BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)
   # dev-folder=<dev user's %LOCALAPPDATA%>/OpenPmsDental/DevDbTls
   # client-folder=<dev user's %LOCALAPPDATA%>/OpenPmsDental/DevDbTlsClient
   # created-parent=<dev user's %LOCALAPPDATA%>/OpenPmsDental
   ssl_ca=<MariaDB folder>/opms-dev-tls/ca.pem
   ssl_cert=<MariaDB folder>/opms-dev-tls/server-cert.pem
   ssl_key=<MariaDB folder>/opms-dev-tls/server-key.pem
   tls_version=TLSv1.2,TLSv1.3
   # END Open PMS - Dental local test TLS
   ```

6. Restarts the service (connections drop for a few seconds), waits until `127.0.0.1:3306` answers and its listener is the service's new
   process, and checks a TLS session (`have_ssl=YES`, `Ssl_cipher` not empty), then the server certificate against the CA copy (`--ssl-ca`
   with `--ssl-verify-server-cert`). When anything fails after the first change (exit code 3), it restores `my.ini` from the backup and
   restarts, removes the backup (the run refused to start with one there, so it is the run's own), and undoes the files: a `ca.pem`,
   `client-cert.pem` or `client-key.pem` that existed before the run gets its bytes and access rules back, one the run added is removed, a
   dev or client folder that existed gets its access rules back, one the run created is removed when it is empty and so is the
   `OpenPmsDental` parent it created, the `opms-dev-tls` folder is removed, and so is `my.ini.opms-new` when a write left it. When the undo
   fails too (exit code 4), the message names what to restore and remove by hand, and the files that held something before the run.

It does not set `require_secure_transport`: unencrypted connections keep working until the lead's `-RequireSecureTransport` step.

**The record in `my.ini`.** `-RequireSecureTransport` and `-Revert` never recompute the dev folder or the client folder from the account
that runs them: they take them from the block's `# dev-folder=` and `# client-folder=` lines, so an enable run with `-DevUser`/`-DevFolder`
for another account is undone in that account's folders. A `-DevFolder` or `-ClientFolder` that names another folder than the recorded
one is refused (the same folder written another way is accepted). They refuse a recorded folder that no enable run can have written: a
dev folder that is not `<a user profile's %LOCALAPPDATA%>\OpenPmsDental\DevDbTls` of this computer, a client folder that is not
`...\OpenPmsDental\DevDbTlsClient` (each judged in its full form, so `..` segments and relative paths are refused), and a
`# created-parent=` line that is not the parent folder of one of them; the message says that something else wrote that line and that
`my.ini` is restored and the files removed by hand. Only when `my.ini` has no block of this script (someone removed it) do they fall back
to `-DevFolder`, else the running account's default (the same rule applies to it), and the `-Revert` plan then says that a copy in another
account's folder is not touched.

**`-RequireSecureTransport`** (the lead's later step). Needs the script's block in `my.ini`, the CA copy in the recorded dev folder, a
working verified TLS connection against it, and no `my.ini.opms-new`. Rewrites the block (the record kept) with
`require_secure_transport=ON`, restarts, and checks that a verified TLS connection works and an unencrypted one is refused; otherwise it
puts the block back as it was and restarts (exit code 3).

**`-Revert`.** Restores `my.ini` from `my.ini.before-opms-dev-tls` (when `my.ini` was changed since, the plan lists the lines that the restore
drops), restarts, checks an unencrypted connection, then removes the `opms-dev-tls` folder, `ca.pem` in the recorded dev folder,
`client-cert.pem` and `client-key.pem` in the recorded client folder (each folder too when it is then empty), the recorded created
parent (`OpenPmsDental`) when it is then empty, and the backup. The plan lists each path.

**`-DryRun`** with any mode: the checks and the plan, no change, no elevation. **`-TestCertificatesFolder <folder>`**: writes only a
certificate set (the CA certificate, the server certificate and key, and with `-WithClientCertificate` the client certificate and key) to
an empty folder outside the repository; the tests use it. `-WithClientCertificate` goes with the enable run or `-TestCertificatesFolder`
only; `-ClientFolder` needs it (or names the recorded folder for the later modes).

Exit codes: 0 done, nothing to do, or a dry run; 1 refused; 2 you did not type `YES`; 3 the change failed and was undone; 4 the change
failed and could not be undone (the message says what to do by hand).

## 3. The client side: `DataConnectionBase.DevDbTls`

For the development set-up, the test helpers and the dev tools only. The program never calls it; `DevDbTlsTests.TheProgramNeverCallsDevDbTls`
checks every C# file outside `tests`, `tools`, `UnitTests`, `UnitTestsCore`, `NoHqTests` and the two developer tools `DocumentationBuilder`
and `xCrudGenerator` (below; `TheDevToolsThatCallDevDbTls_AreNotProductProjects` checks that they are no product project). The class is in
`DataConnectionBase.dll` because every test project and dev tool already references that assembly, so no project file had to change.

- **The CA.** `OPMS_DB_TLS_CA` (a file that must exist), else `%LOCALAPPDATA%\OpenPmsDental\DevDbTls\ca.pem` when it exists, else none.
- **The local test server.** `localhost` or a loopback address on port 3306, or on a port named in `OPMS_DB_TLS_RELAY_PORTS` (a test's TCP
  relay to it; `AdoptionValidation.Tests`' `TcpRelay` registers its port while it runs). Any other server or port is left exactly as the
  caller had it: the local end of an SSH forward to another server (the runbook's `13306`) must never be offered the test CA.
- **With the CA**: `Apply(connectionString)` sets `SslMode=VerifyCA;SslCa=<ca>` (replacing any SslMode); `GetSslCa(server)` returns the
  CA path for `DataConnection.SetDb`, `SetDbT`, the `DataConnection` constructor or `BuildSimpleConnectionString`, whose loopback branch then
  writes `SslMode=VerifyCA;SslCa=<ca>`, as it always did for a loopback server with a CA; `GetClientArguments(host,port)` gives
  `--ssl-ca=<ca> --ssl-verify-server-cert` for `mariadb.exe` and `mariadb-dump.exe`.
- **Without the CA**: everything exactly as before (the string unchanged, `""`, no arguments), so nothing breaks before the owner's script.
- **`OPMS_DB_REQUIRE_TLS=1`** (also `true`, `yes`; `0`, `false`, `no` or unset: off; anything else is an error, so a typing error never turns
  it off), the gates' switch: a missing CA for the local test server is an error (`ApplicationException` naming the CA path, the variable
  and the script). Most helpers turn a connection error into an inconclusive test, as they do when no server answers; the canary is
  `DevDbTlsServerTests`, which fails outright when TLS is required and the CA is missing.
- `VerifyCA` (the chain against the test CA) rather than `VerifyFull` for the helpers, as the program's own loopback strings with a CA; the
  server certificate names `localhost` and `127.0.0.1`, so S2-2 can use `VerifyFull` (`DevDbTlsServerTests` checks both names).

`tools/SyntheticData` references nothing of the program, so it has the same rules in its own `Infrastructure/LocalTls.cs` (`Db.Open`;
`SslMode=None` as before without the CA and for other ports). `tools/DevDbTls.ps1` has them for the scripts. A test checks that the three
name the same variables, file and port.

### What connects through it

| Where | Change |
|---|---|
| 89 helpers and test files under `tests/` (every `ScratchDb`, `TestDb` and the like) | `new MySqlConnection(DevDbTls.Apply(...))` and `SetDb`/`SetDbT(...,DevDbTls.GetSslCa(server))`, made mechanically; every other line as it was. |
| `tests/DbConnectionSecurityTests` | The database tests pass the CA (`BuildSimpleConnectionString`, `SetDb`, `new DataConnection`, `MiscData.MakeABackup(sslCa:)`, the tool's `TestConnection` and `CaFile`). The read-only server tests seed the read-only server's own CA preference with the test CA, so it connects the old way with `SslMode=VerifyCA` (and `SslMode=none` without the CA). `DataConnection_Loopback_ConnectsWithoutTls_AsBefore` keeps proving the program's loopback string without a CA is `SslMode=none`; once the server requires TLS it proves the server refuses it. |
| `tests/DbGrantsTests/GrantServer.cs`, `tests/LibraryUpdateTests/EmailMessagesTests.cs` | The CA in the connection string and in the `CentralConnectionBase` override. |
| `tests/DbGrantsTests/GrantRun.Program.cs`, `GrantRun.Refusals.cs` | `MiscData.MakeABackup(serverName:...)` passes `sslCa:DevDbTls.GetSslCa(...)`. The backup to "another server" (`127.0.0.1:3306`) already reached the CA through the `DataConnection(server,...)` constructor's fallback to the thread's `SslCa` (set by the run's `SetDb` with the CA), so this makes it explicit rather than changing what it connects with. |
| `tests/BSERVERTests/ReplicationDelayDbTests.cs` | The read-only server override goes to `ConnectionStoreBase.OverrideConnection` with `SslCa`, because `CentralConnection`'s conversion to `CentralConnectionBase` does not carry `SslCA` (an observation about the program, not changed here). |
| `OpenPmsDentalBusiness/WebServices/OpenDentalServerMockIIS.cs` | The Middle Tier mock server takes an optional `sslCa` (default `""`) and passes it to its thread's `SetDbT`: the program's existing loopback-with-CA branch, so the connection rule does not change and the program still never calls `DevDbTls`. Before, the server side of every mock test that gives it a server built `SslMode=none`, whatever the client side had. |
| `tests/MiddleTierMockTests/ScratchDb.cs`, `ScratchDbCopy.cs` (`NewMockServer`), `tests/EhrTests/Support/ScratchDb.cs` (`UseMockMiddleTier`), `UnitTests/UnitTests/ReportsTests.cs` | The mock server gets `sslCa:DevDbTls.GetSslCa(server)`. |
| `UnitTests/Misc/TestBase.cs` | The `DentalOffice` override (the `DataAction.RunPractice` threads, e.g. `StatementT.CreateStatement`) gets `SslCa=DevDbTls.GetSslCa(TestServer)`, as in `EmailMessagesTests`. |
| `UnitTestsCore/Misc/DatabaseTools.cs` | `SetDbConnection` (the legacy `UnitTests` and its windows) passes `GetSslCa(serverAddr)`. |
| `tools/DevDbTool/DevDatabase.cs` | Both connections pass `GetSslCa`; a remote server (`--allow-remote-server`) gets `""`, as before. |
| `tools/New-DevDatabase.ps1` | `-WriteConfig` adds an `SslCa` element with the CA's path to `FreeDentalConfig.xml` for the local test server, so `OpenPmsDental.exe` (Debug) connects with `SslMode=VerifyCA`: `CentralConnections.TryToConnect` passes the configuration's `SslCa` to `SetDb`. Without the CA the element is left out, as before. |
| `tools/New-OdReferenceDatabase.ps1` | `mariadb.exe` gets the TLS arguments. |
| `tools/AdoptionValidation` | `BusinessLayer.Connect` passes `GetSslCa`; `DatabaseCopier` gives `mariadb-dump.exe` and `mariadb.exe` the TLS arguments (quoted); the tests' `mariadb.exe` call too; `TcpRelay` registers its port. On the office's admin PC (no test CA, or the forward's port) nothing changes. |
| `tools/SyntheticData/Infrastructure/Db.cs` | `LocalTls.Apply` instead of the fixed `SslMode=None`. |
| `tests/ToothChartImageTests/Support/ChartTestDb.cs` (TC-2, from `main` in the fix round) | `DevDbTls.Apply` and `SetDb(...,DevDbTls.GetSslCa(Server))`, as every other helper. |
| `DocumentationBuilder/DataConnection.cs` | Both strings it builds (`SslMode=none`) go through `DevDbTls.Apply`; it creates its `MySqlConnection` directly, so the local test server gets `SslMode=VerifyCA` with the CA, every other server as before. |
| `xCrudGenerator/CrudGenHelper.cs` | `ConnectToDatabase`'s `SetDb` gets `DevDbTls.GetSslCa(server)` (the program's loopback branch with a CA). `ConnectToDatabaseM` (upstream's server over the network, which the rule refuses without an office CA) is unchanged. |

**The dev-only programs outside `tools`** (item 7 of section 9), decided by one rule: a developer tool that is never released and is no
product project (it does not link `SharedAssemblyInfo.cs`, which every shipped project and every side program does) gets the CA from
`DevDbTls`, because its connections are no rule of the program's; a product project keeps its connections as they are, because `DevDbTls`
in it would put the developer's CA path and the `OPMS_DB_*` switches into a program of the product (`TheProgramNeverCallsDevDbTls` keeps
checking it).

| Program | Covered | Why |
|---|---|---|
| `DocumentationBuilder` | yes | Developer tool, not shipped (README), no product project. |
| `xCrudGenerator` | yes | Generates `OpenPmsDentalBusiness/Crud`; never released, no product project. |
| `DatabaseIntegrityCheck` (`FormDatabaseCheck.OpenConnection`, its `SslMode=none` string) | **no** | A side program of the product (in the source, not in the setup; it links `SharedAssemblyInfo.cs`). |
| `CentralManager` (`CentralConnectionHelper.SetCentralConnection`'s `SetDbT`, `FormCentralManager`'s `SetDb`, both with no CA) | **no** | A side program of the product, as above. Its connections come from its central database's connection list; giving them a CA is the program's connection work, not O-12's. |

Both stop reaching the local test server once it requires TLS (section 5 step 4): their loopback strings have `SslMode=none`. Nothing runs
them in a test or a gate.

**Program paths that pass `sslCa=""` for a server they name** (not covered: they are the program's own code, which O-12 does not change).
Each builds `SslMode=none` for the local test server, so a test that reaches one connects without TLS today and fails once the server
requires it; no test in the repository reaches one with the local server, as far as the scan's shapes and a search show:

- `ConnectionStoreBase.SetDb(ConnectionNames,...)` (`DataConnectionBase/ConnectionStoreBase.cs` about line 255; `SetDbT` passes the
  entry's `SslCa`, `SetDb` does not);
- `DataAction.GetT(fn,server,db,user,...)` and `DataAction.Run(action,server,...)` (`OpenPmsDentalBusiness/Db/DataAction.cs` about 108);
- `LargeTableHelper`'s copy to another server (`OpenPmsDentalBusiness/Db/LargeTableHelper.cs` about 426, `SetDbT(_serverTo,...)`);
- the archive database (`OpenPmsDentalBusiness/Db Multi Table/MiscData.cs` about 449, `SetDb(archiveServerName,...)`);
- the Middle Tier's `OpenDentalServerConfig.xml` reader, `Userods.LoadDatabaseInfoFromFile` (`OpenPmsDentalBusiness/Data
  Interface/Userods.cs` about 685): its `ComputerName`/`User`/`Password` elements give `SetDb(server,...)` with no CA (the file has no
  `SslCa` element); only its `ConnectionString` element can carry TLS options (section 5 step 2).

`DevDbTlsTests.EveryTestAndToolConnectionToTheLocalServer_GoesThroughDevDbTls` reads every C# file under `tests`, `tools`, `UnitTests`,
`UnitTestsCore`, `NoHqTests`, `DocumentationBuilder` and `xCrudGenerator` and fails for a call that connects to a server it names without
the test CA:

- `new MySqlConnection(...)` whose argument is not `DevDbTls.Apply(...)`;
- `DbConnectionSecurity.NewMySqlConnection(...)` or `NewMySqlConnectionWithoutPooling(...)` whose argument is neither
  `DevDbTls.Apply(...)` nor a `DataConnection.BuildSimpleConnectionString(...)` call whose `sslCa` argument (`sslCa:` or the sixth) is
  `DevDbTls.GetSslCa(...)`;
- `SetDb`/`SetDbT` with a server (seven or more arguments, the seventh the database type) without `DevDbTls.GetSslCa(...)`;
- `new DataConnection(...)` with a server (five or more arguments) without it;
- a `new CentralConnectionBase { ... }` initializer with `ServerName=` and no `SslCa=DevDbTls.GetSslCa(...)`;
- every `new CentralConnection { ... }` initializer with `ServerName=`, whatever it sets: its conversion to `CentralConnectionBase` drops
  `SslCA`, so the message points to `new CentralConnectionBase { ServerName=...,SslCa=DevDbTls.GetSslCa(...) }`;
- `MiscData.MakeABackup(...)` with `serverName:` (or a server as its first argument) without `sslCa:DevDbTls.GetSslCa(...)`;
- `new OpenDentalServerMockIIS(...)` (any namespace prefix) with `server:` (or a server as its first argument) without it;

apart from a short list of deliberate exceptions, each with its file, its kind of call and its reason (the TLS tests' own strings and their
CA-less refusal check, servers over the network that the rule refuses before connecting, strings that are only created and never opened,
`LoopbackDatabaseTests`' connection without a CA on purpose, the read-only server's own thread string, the grants run's helper that passes
the CA itself, and `DocumentationBuilder`'s `ConnStr`, set from `DevDbTls.Apply` just before). Calls of these names that name no server
(they take the thread's own connection, as `new OpenDentalServerMockIIS()`, `MakeABackup()` or `NewMySqlConnection("")`) are not checked.
It also asserts that it found calls of every kind; `TheConnectionScan_FindsEachCallShape_WithAndWithoutTheCa` checks its reading of each
shape, with and without the CA, on synthetic source. A package that adds a helper after this one is merged gets the fix in the failure
message. Connections the scan cannot see by name (a connection string built in program code from the thread's context) follow from the
helpers' own `SetDb` with the CA.

## 4. The owner's steps (run once)

Before: tell the lead, so that no build-lane or test run uses the database during the restart.

1. Open Windows PowerShell with **Run as administrator** (from the account that runs the dev tools and tests, so that the CA lands in its
   `%LOCALAPPDATA%`).
2. Go to a checkout of `main` that has this package:

   ```powershell
   cd <repository folder>
   ```

3. Look at the plan (changes nothing):

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1 -DryRun
   ```

4. Turn TLS on, and type `YES` when it asks:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1
   ```

   This makes the test CA and the server certificate only, as approved on 2026-11-08; the plan's step 1 says `No client certificate`.
   It ends with `TLS is on: TLSv1.3 (or TLSv1.2), cipher ...` and `The server certificate was verified against ...\ca.pem`. A `WARNING`
   line about the verification goes to the lead (the tests check it with the program's connector); `FAILED` with `Undone:` means nothing
   stayed changed.

When the elevated window runs as another account than the one that runs the tests, add
`-DevUser <DOMAIN>\<user> -DevFolder <that user's %LOCALAPPDATA%>\OpenPmsDental\DevDbTls` (the plan says so when it notices). The script
records that folder in `my.ini`, so the undo below finds it without repeating the switches.

Only if the owner has said yes to S2-2's client certificate (section 10), add `-WithClientCertificate` to step 4 (it is made with the CA,
so not later without a new CA). Its key goes to `%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient` (with `-DevFolder`: `DevDbTlsClient` beside
it), never to the CA's folder.

**To undo it** (also as administrator): `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1 -Revert`, then
`YES`. It removes the files in the folders the enable run recorded in `my.ini` (the plan lists them), also another account's after
`-DevUser`/`-DevFolder`, and the `OpenPmsDental` folder when the enable run created it and it is then empty; a `-DevFolder` that names
another folder is refused, and so is a record that no enable run can have written (section 2). The dev tools and tests then connect as
before (they find no CA).

## 5. The lead's steps afterwards

1. Run the TLS tests with the requirement on: `$env:OPMS_DB_REQUIRE_TLS='1'`, then a Targeted run of `DbConnectionSecurityTests`,
   `SyntheticData.Tests` and `NoHqTests`. Every `DevDbTlsServerTests` test passes, except
   `WhenTheServerRequiresTls_AConnectionWithoutTlsIsRefused` (inconclusive until step 4).
2. The coordinator's next Full run with `OPMS_DB_REQUIRE_TLS=1`. Every connection the scan of section 3 checks (the helpers' strings,
   `NewMySqlConnection` with the program's string, `SetDb`/`SetDbT`, `DataConnection` with a server, the `CentralConnectionBase` overrides,
   `MakeABackup` to a server, and the Middle Tier mock server's own thread) then connects with TLS and the test CA, and so does every thread
   that takes its context from them. What the run cannot show before step 4 is a connection outside those shapes that still has no CA (the
   program paths of section 3 that pass `sslCa=""` for a server, `DatabaseIntegrityCheck`, `CentralManager`): the server accepts it. Step
   4's run is the check for that (a connection without TLS is then refused and its test fails), so run the Full run again after step 4.
   **The Middle Tier gate** (the 3b gate's real Middle Tier on the build PC, S3b-6): its `OpenDentalServerConfig.xml` must name the database
   with a `<ConnectionString>` element that carries the TLS options, for example
   `<ConnectionString>Server=127.0.0.1;Port=3306;Database=<db>;User ID=<user>;Password=<password>;SslMode=VerifyCA;SslCa=<the CA path of
   DevDbTls.GetCaPath()></ConnectionString>`, not the `ComputerName`/`User`/`Password` elements, which `Userods.LoadDatabaseInfoFromFile`
   turns into `SslMode=none` for this server (the file has no `SslCa` element); or S3b-6 adds an `SslCa` element to that file and passes
   it to `SetDb`. Once step 4 has run, the elements' form is refused by the server.
3. Tell the other sessions first: once the server requires TLS, a worktree without this package fails its database tests (its `SetDb`
   helpers write `SslMode=none`). Every worktree that still runs database tests merges or rebases onto `main` with this package. The
   uncovered programs of section 3 (`DatabaseIntegrityCheck`, `CentralManager`) no longer reach the local server from then on.
4. As administrator: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1 -RequireSecureTransport`, then `YES`.
   It checks that a verified TLS connection works and an unencrypted one is refused (and undoes the line otherwise). Run step 1 again: now
   `WhenTheServerRequiresTls_AConnectionWithoutTlsIsRefused` and `DataConnection_Loopback_ConnectsWithoutTls_AsBefore` show the refusal.
5. Confirm with the owner right before the drop, then rebuild the old test database over the encrypted connection:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools\New-DevDatabase.ps1 -Force -WithSampleData -WriteConfig
   ```

   `-Force` drops `opendental_dev` first (it refuses a database with more than 200 patients); `-WriteConfig` writes `FreeDentalConfig.xml`
   with the `SslCa` element, so the Debug program connects encrypted too.
6. S2-2's TLS test needs nothing more: the server's `ssl_ca` is the test CA and its certificate names `127.0.0.1` and `localhost`, so it
   connects with `VerifyFull` (the server listens on `127.0.0.1` only, so there is no other address of the build PC to use). Its
   certificate-login part needs a client certificate signed by that CA, which the script makes only with `-WithClientCertificate` and only
   in the same run as the CA. So it waits for the owner's answer (section 10). With a yes: `-Revert`, then the enable run again with
   `-WithClientCertificate` (a new CA; the dev tools find it at the same path), as administrator, with the other sessions told first as
   for any restart. Then `client-cert.pem` and `client-key.pem` in `DevDbTls.GetDefaultClientFolder()` (`%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient`)
   are a client certificate the server's CA signed, so a `REQUIRE X509` account accepts it; the test imports that pair through a CAPI and
   a CNG key provider into the current user's store. Until then `DevDbTlsServerTests.ClientCertificateForS22_ChainsToTheServersCa` is
   inconclusive. A new CA for other reasons (new certificates five years from now) is the same `-Revert` and enable run;
   `DevDbTlsServerTests.ServerCertificate_IsValidForAnotherMonth` fails a month before the server certificate expires.

## 6. Checks run here

Without the owner's script (no CA yet), so the server tests were inconclusive by design:

- The script, not elevated: no switch, `-RequireSecureTransport` and `-Revert` each print `REFUSED: This script changes the MariaDB service
  and must run elevated: start PowerShell with "Run as administrator" (or use -DryRun to see the plan).` and exit 1, before any plan.
  `-Revert -RequireSecureTransport` is refused (one mode at a time); a `-TestCertificatesFolder` inside the repository or not empty is
  refused. `-Revert -WithClientCertificate` and `-RequireSecureTransport -WithClientCertificate` print `REFUSED: -WithClientCertificate
  goes with the enable run (or -TestCertificatesFolder) only: the client certificate is made with the CA.`; `-ClientFolder` without
  `-WithClientCertificate` prints `REFUSED: -ClientFolder needs -WithClientCertificate (the enable run), or names the recorded client
  folder for -Revert or -RequireSecureTransport.`; each exits 1 before the elevation check.
- `-DryRun` on this PC after the fix round (exit 0; the user profile, the account and the process id shortened here; `OpenPmsDental`
  does not exist yet in that profile, so the run would create it and records it):

  ```text
  Not elevated: a dry run only. The real run must be started as administrator.
  Service:        MariaDB (Running), account NT SERVICE\MariaDB
  Server program: C:\Program Files\MariaDB 10.11\bin\mysqld.exe
  Option file:    C:\Program Files\MariaDB 10.11\data\my.ini
  Listener:       127.0.0.1:3306, process <pid> (mysqld), the service's own
  Server:         10.11.19-MariaDB on 127.0.0.1:3306, data folder C:\Program Files\MariaDB 10.11\data\
  TLS now:        have_ssl=DISABLED, require_secure_transport=0, tls_version=TLSv1.2,TLSv1.3, this session's cipher ''

  Plan:
    1. Make a test CA (its private key stays in memory and is discarded) and a server certificate for localhost and 127.0.0.1.
       No client certificate (-WithClientCertificate is not given): S2-2's certificate login waits for the owner's yes. It can only be
       made in the same run as the CA, so it then means -Revert and this script again with -WithClientCertificate.
    2. Create C:\Program Files\MariaDB 10.11\opms-dev-tls (owner Administrators; SYSTEM and Administrators full control, NT SERVICE\MariaDB read) with
       ca.pem, server-cert.pem and server-key.pem.
    3. Write the CA certificate (public part only) to C:\Users\<user>\AppData\Local\OpenPmsDental\DevDbTls\ca.pem.
       The folder is created with a protected ACL (nothing inherited): <DOMAIN>\<user>, SYSTEM and
       Administrators full control, nobody else. No private key goes to this folder.
       C:\Users\<user>\AppData\Local\OpenPmsDental does not exist and is created for it (-Revert removes it again when it is then empty).
    4. Copy C:\Program Files\MariaDB 10.11\data\my.ini to C:\Program Files\MariaDB 10.11\data\my.ini.before-opms-dev-tls, then add at the end of its [mysqld] section (the comment lines
       record the folders above; -Revert and -RequireSecureTransport read them from there):
         # BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)
         # dev-folder=C:/Users/<user>/AppData/Local/OpenPmsDental/DevDbTls
         # created-parent=C:/Users/<user>/AppData/Local/OpenPmsDental
         ssl_ca=C:/Program Files/MariaDB 10.11/opms-dev-tls/ca.pem
         ssl_cert=C:/Program Files/MariaDB 10.11/opms-dev-tls/server-cert.pem
         ssl_key=C:/Program Files/MariaDB 10.11/opms-dev-tls/server-key.pem
         tls_version=TLSv1.2,TLSv1.3
         # END Open PMS - Dental local test TLS
    5. Restart the service MariaDB (connections drop for a few seconds) and check a TLS session and the server
       certificate against the CA. If the server does not come back with TLS, the backup is restored, the files this run
       replaced are put back as they were and the files and folders it added are removed.
    require_secure_transport is not set: unencrypted connections keep working until -RequireSecureTransport.

  Dry run: nothing was changed.
  ```

  `-DryRun -WithClientCertificate` has the client step in its own folder, and the record line for it:

  ```text
    1. Make a test CA (its private key stays in memory and is discarded) and a server certificate for localhost and 127.0.0.1.
       With -WithClientCertificate also a client certificate (clientAuth) for the certificate-login test of S2-2.
    ...
    4. Write the client certificate and key to C:\Users\<user>\AppData\Local\OpenPmsDental\DevDbTlsClient\client-cert.pem and C:\Users\<user>\AppData\Local\OpenPmsDental\DevDbTlsClient\client-key.pem:
       a folder of its own, not the CA's. The folder is created with a protected ACL (nothing
       inherited): <DOMAIN>\<user> read only, SYSTEM and Administrators full control.
       C:\Users\<user>\AppData\Local\OpenPmsDental does not exist and is created for it (-Revert removes it again when it is then empty).
    5. Copy ... then add at the end of its [mysqld] section (...):
         # BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)
         # dev-folder=C:/Users/<user>/AppData/Local/OpenPmsDental/DevDbTls
         # client-folder=C:/Users/<user>/AppData/Local/OpenPmsDental/DevDbTlsClient
         # created-parent=C:/Users/<user>/AppData/Local/OpenPmsDental
         ...
  ```

  Each of these unelevated dry runs exits 1 with its `REFUSED:` line before any plan, and writes nothing (the fix round, items 1 and 6):

  | Switches | Refusal |
  |---|---|
  | `-DevFolder "C:\Program Files\MariaDB 10.11"`, and `...\MariaDB 10.11\data\DevDbTls` | `... is or overlaps the MariaDB folder C:\Program Files\MariaDB 10.11.` |
  | `-DevFolder C:\` | `-DevFolder C:\ is a drive root.` |
  | `-DevFolder C:\Users\<user>` | `... is or holds the user profile C:\Users\<user>.` |
  | `-DevFolder <a temporary folder>\OpenPmsDental\DevDbTls`, `-DevFolder %LOCALAPPDATA%\OpenPmsDental\Other` | `... is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTls of this computer. Only such a folder is accepted: -Revert and -RequireSecureTransport take no other from the record in my.ini.` |
  | `-WithClientCertificate -ClientFolder %LOCALAPPDATA%\OpenPmsDental\DevDbTls\client` | `... and -DevFolder ... overlap. The client key goes to a folder of its own, never where the dev tools read the CA.` |
  | `-WithClientCertificate -ClientFolder %LOCALAPPDATA%\OpenPmsDental\ClientElsewhere` | `... is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTlsClient of this computer. ...` |
  | `-Revert -DevFolder <a temporary folder>\x` | `The dev folder ..., from -DevFolder (or this account's default), is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTls of this computer, the only dev folder this script writes to. ...` |

  `-DryRun -RequireSecureTransport` refuses (no block yet); `-DryRun -Revert` says there is nothing to revert.
- `New-DevDatabase.ps1`'s config writing, run on a temporary folder with `OPMS_DB_TLS_CA`: one `SslCa` element after `MySQLPassHash` with the
  CA, none without a CA or for another port; with `OPMS_DB_REQUIRE_TLS=1` and no CA the CA look-up stops with the message.

Tests (Targeted runs, all passed): `DbConnectionSecurityTests` 140 (130 passed, 10 inconclusive: the nine `DevDbTlsServerTests` and
`DataConnection_Loopback_WithTheLocalTestServersCa_VerifyCa`, until the CA exists), `SyntheticData.Tests` 58 (57 passed, the pre-existing
`RealDump_Has422Tables` inconclusive), `MiddleTierMockTests` 125, `ReleaseFlowTests` 120 (119 passed), `LibraryBindingTests` 12, `NoHqTests`
58, `EhrTests` 88 (its helper came from `main`), and for the hand-edited helpers `BSERVERTests` 57, `LibraryUpdateTests` 100,
`AdoptionValidation.Tests` 69 and `DbGrantsTests` 3 (2 passed, the grants run inconclusive without its variables). The full build has no
error. `ForkMigrationTests` is named in the package's list but left out under the
rule for every package (the coordinator runs it with the init file); its helper change is the same mechanical one as the others.

After the review's fixes (`dev/dbtls-2`, full build without error), Targeted runs, all passed: `DbConnectionSecurityTests` 144 (134
passed, the same 10 inconclusive), `MiddleTierMockTests` 125, `EhrTests` 88, `DbGrantsTests` 3 (2 passed, as before), `SyntheticData.Tests`
58 (57 passed), `ReleaseFlowTests` 120 (119 passed), `LibraryBindingTests` 12, `NoHqTests` 58, and the legacy `UnitTests` 1731 (1728
passed, the 3 skipped as before) for `TestBase` and `ReportsTests`. `ForkMigrationTests` again left out under the same rule (its helper is
not touched by the fixes).

After the fix round (section 9, `main` merged in; full lane build without error), Targeted runs, all passed: `DbConnectionSecurityTests`
150 (140 passed, the same 10 inconclusive until the CA exists), `MiddleTierMockTests` 125, `SyntheticData.Tests` 58 (57 passed),
`ReleaseFlowTests` 120 (119 passed), `LibraryBindingTests` 12, `EhrTests` 88, `NoHqTests` 58, and for TC-2's `ChartTestDb`
`ToothChartImageTests` 119 (116 passed, its three recording and gallery tools skipped as they are on `main`). `ForkMigrationTests` and
`InitSqlTests` left out under the rule for every package.

New tests:

- `tests/DbConnectionSecurityTests/DevDbTlsTests.cs` (no server): the connection-string choices of section 3, the program's loopback string
  with the CA, the command-line arguments, the client folder beside (never inside) the CA's folder and no private key in the CA's folder
  where the script ran, the source checks (the program never calls `DevDbTls`; the scan of section 3 and its reading of each call shape).
- `tests/DbConnectionSecurityTests/DevDbTlsScriptTests.cs`: the script's certificate set from `-TestCertificatesFolder`, checked offline (the
  CA's constraints and usages; the server certificate's names read from the DER, `DNS:localhost` and `IP:127.0.0.1`, serverAuth only; with
  `-WithClientCertificate` the client certificate's clientAuth; both chain to the CA, SHA-256, valid 5 years; each PKCS#8 key matches its
  certificate and signs; no CA key anywhere; a second run makes a new CA, and without the switch no client certificate or key); the
  `my.ini` edit on synthetic LF, CRLF, BOM and unterminated files through the script's own functions; the record of the folders in the
  block, read back for `-Revert` and `-RequireSecureTransport` (another account's folders, a `-DevFolder` or `-ClientFolder` that disagrees
  refused, the same folder written another way accepted, the fallback without a block); the refusals without elevation and of the client
  switches outside the enable run; a dry run that leaves `my.ini` byte for byte as it was, writes no client key by default, records the
  dev folder, and with `-WithClientCertificate` puts the key in the client folder. From the fix round (section 9), all without elevation
  on the script's own functions with scratch folders and synthetic files: the enable run's folder checks (each refusal of item 1 and the
  accepted cases, this account's defaults among them); the record checks of item 6 (each refusal, and `Get-Paths` taking a record in this
  account's own `%LOCALAPPDATA%` and refusing one elsewhere); the listener check on synthetic listeners and on this computer's real one
  (the MariaDB service's own process); ANSI and UTF-16 files and `!include` lines refused, UTF-8 with and without a BOM read; the undo of
  a failed run and `-Revert`'s removal of the created parent on scratch folders (bytes and access rules back, the added files gone); and
  the dry-run test also checks the listener line, the dev user's full control, the created-parent record and the seven refusals above.
- `tests/DbConnectionSecurityTests/DevDbTlsTests.cs`, from the fix round: the scan's new kinds (`NewMySqlConnection` with the builder's
  `sslCa`, `CentralConnection` always flagged) with and without the CA on synthetic source, the scan over the two developer tools, and
  `TheDevToolsThatCallDevDbTls_AreNotProductProjects`.
- `tests/DbConnectionSecurityTests/DevDbTlsServerTests.cs` (once the CA exists; a failure with `OPMS_DB_REQUIRE_TLS=1` and no CA): a
  helper's connection is encrypted (`Ssl_cipher`, TLS 1.2 or 1.3) and the server offers TLS 1.2 and 1.3 only; the program's
  `DataConnection` with the helpers' CA is encrypted; a `dbtls_` scratch database is created, filled, read and dropped over TLS; `VerifyFull`
  works for `127.0.0.1` and `localhost`; another CA is refused (`VerifyCA` and `VerifyFull`), so the verification is real; the server
  certificate is valid for another month; S2-2's client certificate (in the client folder, when the script ran with
  `-WithClientCertificate`) chains to the CA; `mariadb.exe` with the TLS arguments is encrypted;
  with `require_secure_transport` a connection without TLS, and the program's loopback string without a CA, are refused.
- `tools/SyntheticData/SyntheticData.Tests/LocalTlsTests.cs`: `LocalTls`.

## 7. Files

- New: `tools/Enable-LocalDbTls.ps1`, `tools/DevDbTls.ps1`, `DataConnectionBase/DevDbTls.cs`, `tools/SyntheticData/Infrastructure/LocalTls.cs`,
  the four test files above, this note.
- Changed: the helpers and files of section 3, `tools/FreeDentalConfig.dev.xml` (its comment), `docs/specs/db-connection-security.md`
  (section 7: the dev tools' rows, the side programs' rows and a row for `DevDbTls`). In the program's assemblies only `OpenPmsDentalBusiness/WebServices/OpenDentalServerMockIIS.cs`
  (the test-only mock server: an optional `sslCa`, default `""`, which no shipped code path passes). In the fix round also
  `tests/ToothChartImageTests/Support/ChartTestDb.cs` (from `main`), and the two developer tools `DocumentationBuilder/DataConnection.cs`
  and `xCrudGenerator/CrudGenHelper.cs` (no product project).
- Not changed: the program's connection rule and every shipped code path, `run-tests.ps1` and the build lane (they do not connect),
  `docs/specs/net10-plan.md` (the lead's; O-12's row can point here).

## 8. The review's fixes (`dev/dbtls-2`)

1. **The client certificate was made by default and its key written next to the published CA.** Now opt-in (`-WithClientCertificate`,
   off by default, section 2 step 1), so the documented owner command makes only what the owner approved on 2026-11-08 (a test CA and a
   server certificate for `127.0.0.1` and `localhost`). With the switch, the pair goes to a folder of its own (`DevDbTlsClient`, `-DevUser`
   read only), never to the CA's folder, which the dev tools read and which never holds a private key. The plan, the header help, sections
   2, 4 and 5.6 and the script tests match.
2. **`-Revert` recomputed the dev folder from the account that runs it.** The enable run now records the dev folder (and the client
   folder) in its `my.ini` block; `-Revert` and `-RequireSecureTransport`'s CA check read the record and refuse a `-DevFolder` or
   `-ClientFolder` that disagrees, so an enable run for another account (`-DevUser`/`-DevFolder`) is undone there, and a stale `ca.pem`
   can no longer make the helpers ask a server without TLS for `VerifyCA`. Without a record the plan says that another account's folder is
   not touched.
3. **The Middle Tier mock server connected its own thread without TLS.** `OpenDentalServerMockIIS` takes `sslCa` and passes it to `SetDbT`
   (the program's loopback-with-CA branch; the program never calls `DevDbTls`); `NewMockServer` (both), EhrTests' `UseMockMiddleTier` and
   the legacy `ReportsTests` pass `DevDbTls.GetSslCa`. The scan now checks `new OpenDentalServerMockIIS(` with a server.
4. **Two more connection shapes skipped `DevDbTls`.** The legacy `UnitTests`' `DentalOffice` override now has `SslCa` (real: `ConnectionStoreBase.SetDbT`
   passes `conn.SslCa`, which was null, so those threads built `SslMode=none`). The grants run's `MakeABackup(serverName:...)` calls pass
   `sslCa` explicitly; for the backup to `127.0.0.1:3306` this was not a live gap (the `DataConnection(server,...)` constructor falls back
   to the thread's `SslCa` when `sslCa` is `""`, and the run's `SetDb` had set the CA), but the call no longer depends on that fallback and
   the scan can check it. The scan now also checks `new DataConnection(` with a server, `CentralConnectionBase`/`CentralConnection`
   initializers with `ServerName=` and `MakeABackup` with a server (section 3); run against the old caller code it flags exactly the eight
   call sites of findings 3 and 4. Section 3 and section 5 step 2 say what the Full run with the switch does and does not prove.

## 9. The fix round after the final reviews (`dev/dbtls-2`, 2026-11-09)

Two independent final reviews called the documented default command safe to run; the lead had their minor findings fixed before the owner
runs the script. First `main` was merged in (35cf85ecf, no conflict: this branch changed no project file, and S1-6's SDK-style test projects
compile every `.cs` file under their folders); the scan then found main's new `ChartTestDb` (TC-2), which now connects through `DevDbTls`
(b50a30111; EhrTests' `ScratchDb` already did).

The script (f8bf7f03f; its tests c45deb823, section 6):

1. **Folders.** `-DevFolder` and `-ClientFolder` are refused when the folder exists and holds anything but the script's own files, or is
   or overlaps the MariaDB folder, the server's data folder or the `opms-dev-tls` folder, or is a drive root or is or holds a user profile
   (`Get-FolderProblem`, with `Test-FolderOverlap`); and, so that an enable run never records a folder that item 6 refuses, each must be a
   profile's `%LOCALAPPDATA%\OpenPmsDental\DevDbTls` (`DevDbTlsClient`) in an `AppData\Local` that exists. The plan says that the folder is
   created (or its ACL replaced) with a protected ACL and that the dev user gets full control (it said "readable by").
2. **The listener.** The process behind `127.0.0.1:3306` (`Get-NetTCPConnection`) must be the service's own (`Win32_Service.ProcessId`),
   and no other process may listen on `0.0.0.0` or `::` port 3306; before the client sends anything there, and after every restart.
3. **`!include`.** A `my.ini` with a line starting `!include` (also `!includedir`) is refused.
4. **Encoding.** A `my.ini` whose bytes do not come back unchanged from the decoding is refused, with the offset of the first such byte.
5. **The undo.** A failed enable run puts back the files it replaced (bytes and access rules) and the access rules of the folders that
   were there, removes what it added, the `OpenPmsDental` parent it created when that is empty, and `my.ini.opms-new`; a run refuses to
   start when `my.ini.opms-new` exists. The enable run records the parent it creates (`# created-parent=`), and `-Revert` removes it when
   it is empty.
6. **The record.** `-Revert` and `-RequireSecureTransport` refuse a recorded dev or client folder that is not a profile's
   `%LOCALAPPDATA%\OpenPmsDental\DevDbTls` or `\DevDbTlsClient` (judged in its full form), and a recorded created parent that is not the
   parent of one of them (`Get-RecordProblem`); the fallback's `-DevFolder` the same.

The connections (370e2ac28):

7. **The dev-only programs outside `tools`**, decided per program by one rule (section 3): `DocumentationBuilder` and `xCrudGenerator`
   are developer tools and no product project, so they get the CA from `DevDbTls` (no rule of the program's changes);
   `DatabaseIntegrityCheck` and `CentralManager` are side programs of the product (they link `SharedAssemblyInfo.cs`), so they are named
   as not covered and stop reaching the local server once it requires TLS. `docs/specs/db-connection-security.md` section 7's rows say
   so. `TheDevToolsThatCallDevDbTls_AreNotProductProjects` checks the rule.
8. **The scan reads `NewMySqlConnection`.** `DbConnectionSecurity.NewMySqlConnection(...)` and `...WithoutPooling(...)` must go through
   `DevDbTls.Apply`, or take a `BuildSimpleConnectionString(...)` whose `sslCa` argument is `DevDbTls.GetSslCa(...)`; the deliberate
   network and CA-less strings (RuleTests, PreferenceServerTests, LoopbackDatabaseTests, PreferenceServerDatabaseTests' thread string,
   DevDbTlsServerTests' refusal check, the grants run's helper) are on the allow-list, which names the kind of call too.
9. **`new CentralConnection { ServerName=... }`** is always flagged, whatever it sets, and the message points to
   `new CentralConnectionBase { ServerName=...,SslCa=DevDbTls.GetSslCa(...) }`. A negative run (main's `ChartTestDb` and a temporary,
   unbuilt file with both new shapes) flagged exactly its four calls.

The notes (this note's sections 3 and 5):

10. Section 3 lists the program paths that pass `sslCa=""` for a server (`ConnectionStoreBase.SetDb`, `DataAction.GetT`/`Run` with a
    server, `LargeTableHelper` about 426, `MiscData` about 449, `Userods.LoadDatabaseInfoFromFile` about 685, the Middle Tier's
    `OpenDentalServerConfig.xml` reader) as not covered; section 5 step 2 says that the Middle Tier gate's `OpenDentalServerConfig.xml`
    names its database with a `<ConnectionString>` carrying `SslMode=VerifyCA;SslCa=<ca>` (or S3b-6 adds `SslCa` to that file).

Nothing here changed the MariaDB service, `my.ini` or a certificate store; the script ran only unelevated (dry runs and its functions on
scratch folders), and every database connection went to `127.0.0.1:3306`.

## 10. Owner question

- **S2-2's client certificate.** Should the script make a client certificate signed by the test CA, for S2-2's certificate-login test (a
  `REQUIRE X509` test account on the local server; the key in `%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient`, readable by the dev account)?
  Until the answer is yes the script makes none and that part of S2-2 waits. A yes after the enable run means `-Revert` and the enable run
  again with `-WithClientCertificate` (a new CA), because the CA's key is discarded after each run.
