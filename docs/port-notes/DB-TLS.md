# Port note: DB-TLS (O-12, the local test database runs encrypted)

Package O-12 of the .NET 10 plan (`docs/specs/net10-plan.md` section 12, O-12, answered by the owner on 2026-11-08: yes), built on branch
`dev/dbtls-1` from `main` (99bb32282, with `main` merged in at e1262eab8); the review's fixes on `dev/dbtls-2` (section 8). The owner runs
one script once as administrator,
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
(`-ServiceName`, default `MariaDB`) exists and runs `mysqld.exe` or `mariadbd.exe` with `--defaults-file=`; that `my.ini` says `port=3306`
(or no port) and `bind-address=127.0.0.1`; the server that answers on `127.0.0.1:3306` reports port 3306, bind address `127.0.0.1` and that
`my.ini`'s data folder, so it is this service. Every connection of the script goes to `127.0.0.1:3306` over TCP with the service's own
`mariadb.exe`, as `-User` (default `root`), the password from `MYSQL_PWD` when set.

**Enable (no mode switch).** Refuses when `my.ini` already has a TLS line (`ssl`, `ssl_ca`, `ssl_cert`, `ssl_key`, `ssl_capath`,
`ssl_cipher`, `tls_version`, `require_secure_transport`), when the server already has TLS, or when a backup or certificate folder of an
earlier run exists. Then:

1. Makes in memory a test CA (RSA 3072, SHA-256, 10 years, `CA:true, pathlen 0`, keyCertSign and cRLSign) and a server certificate
   (RSA 2048, 5 years, `CN=localhost`, SAN `DNS:localhost` and `IP:127.0.0.1`, serverAuth). The CA's private key is never written
   anywhere: nothing else can ever be signed with it. **No client certificate by default**: the owner approved a test CA and a server
   certificate; S2-2's client certificate (RSA 2048, 5 years, `CN=opms-dbtls-test-client`, clientAuth) is made only with
   `-WithClientCertificate`, once the owner has said yes to it (section 9). Because the CA's key is gone after the run, a client
   certificate can only be made in the same run as the CA: a later yes means `-Revert` and the script again with the switch (a new CA,
   which the dev tools pick up from the same path).
2. Writes `ca.pem`, `server-cert.pem` and `server-key.pem` (PKCS#8) to `<MariaDB folder>\opms-dev-tls`, next to `bin` and `data`. Not inside
   `data`: MariaDB takes every folder there for a database. The folder and files get a protected ACL: owner Administrators, SYSTEM and
   Administrators full control, the service account (`NT SERVICE\MariaDB`) read, nobody else. These are the only keys the server reads.
3. Writes the CA certificate (the public part only) as `ca.pem` to `-DevFolder`, default `%LOCALAPPDATA%\OpenPmsDental\DevDbTls` of the
   account that runs the script, with a protected ACL: `-DevUser` (default that account), SYSTEM and Administrators. Every worktree of that
   account finds it there. **That folder never holds a private key** (`DevDbTlsTests.TheCaFolder_NeverHoldsAPrivateKey_...` checks it
   where the script ran).
4. Only with `-WithClientCertificate`: writes `client-cert.pem` and `client-key.pem` to `-ClientFolder`, default `DevDbTlsClient` beside
   the dev folder (`%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient`, `DevDbTls.GetDefaultClientFolder()`), a folder of its own with a
   protected ACL: `-DevUser` read only, SYSTEM and Administrators full control. It refuses a client folder that is the dev folder, inside
   it or around it. Both folders are refused inside the repository; nothing is ever written to the repository.
5. Copies `my.ini` to `my.ini.before-opms-dev-tls` and adds at the end of its `[mysqld]` section (every other byte of the file stays as it
   was, line ends and a BOM included). The two comment lines record where steps 3 and 4 wrote (the `client-folder` line only with
   `-WithClientCertificate`); `-Revert` and `-RequireSecureTransport` read them back:

   ```ini
   # BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)
   # dev-folder=<dev user's %LOCALAPPDATA%>/OpenPmsDental/DevDbTls
   # client-folder=<dev user's %LOCALAPPDATA%>/OpenPmsDental/DevDbTlsClient
   ssl_ca=<MariaDB folder>/opms-dev-tls/ca.pem
   ssl_cert=<MariaDB folder>/opms-dev-tls/server-cert.pem
   ssl_key=<MariaDB folder>/opms-dev-tls/server-key.pem
   tls_version=TLSv1.2,TLSv1.3
   # END Open PMS - Dental local test TLS
   ```

6. Restarts the service (connections drop for a few seconds), waits for `127.0.0.1:3306`, and checks a TLS session (`have_ssl=YES`,
   `Ssl_cipher` not empty), then the server certificate against the CA copy (`--ssl-ca` with `--ssl-verify-server-cert`). When the server
   does not come back with TLS, it restores the backup, restarts again and removes the new files (exit code 3).

It does not set `require_secure_transport`: unencrypted connections keep working until the lead's `-RequireSecureTransport` step.

**The record in `my.ini`.** `-RequireSecureTransport` and `-Revert` never recompute the dev folder or the client folder from the account
that runs them: they take them from the block's `# dev-folder=` and `# client-folder=` lines, so an enable run with `-DevUser`/`-DevFolder`
for another account is undone in that account's folders. A `-DevFolder` or `-ClientFolder` that names another folder than the recorded
one is refused (the same folder written another way is accepted). Only when `my.ini` has no block of this script (someone removed it) do
they fall back to `-DevFolder`, else the running account's default, and the `-Revert` plan then says that a copy in another account's
folder is not touched.

**`-RequireSecureTransport`** (the lead's later step). Needs the script's block in `my.ini`, the CA copy in the recorded dev folder and a
working verified TLS connection against it. Rewrites the block (the record kept) with `require_secure_transport=ON`, restarts, and checks
that a verified TLS connection works and an unencrypted one is refused; otherwise it puts the block back as it was and restarts (exit
code 3).

**`-Revert`.** Restores `my.ini` from `my.ini.before-opms-dev-tls` (when `my.ini` was changed since, the plan lists the lines that the restore
drops), restarts, checks an unencrypted connection, then removes the `opms-dev-tls` folder, `ca.pem` in the recorded dev folder,
`client-cert.pem` and `client-key.pem` in the recorded client folder (each folder too when it is then empty) and the backup. The plan
lists each path.

**`-DryRun`** with any mode: the checks and the plan, no change, no elevation. **`-TestCertificatesFolder <folder>`**: writes only a
certificate set (the CA certificate, the server certificate and key, and with `-WithClientCertificate` the client certificate and key) to
an empty folder outside the repository; the tests use it. `-WithClientCertificate` goes with the enable run or `-TestCertificatesFolder`
only; `-ClientFolder` needs it (or names the recorded folder for the later modes).

Exit codes: 0 done, nothing to do, or a dry run; 1 refused; 2 you did not type `YES`; 3 the change failed and was undone; 4 the change
failed and could not be undone (the message says what to do by hand).

## 3. The client side: `DataConnectionBase.DevDbTls`

For the development set-up, the test helpers and the dev tools only. The program never calls it; `DevDbTlsTests.TheProgramNeverCallsDevDbTls`
checks every C# file outside `tests`, `tools`, `UnitTests`, `UnitTestsCore` and `NoHqTests`. The class is in `DataConnectionBase.dll`
because every test project and dev tool already references that assembly, so no project file had to change.

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

`DevDbTlsTests.EveryTestAndToolConnectionToTheLocalServer_GoesThroughDevDbTls` reads every C# file under `tests`, `tools`, `UnitTests`,
`UnitTestsCore` and `NoHqTests` and fails for a call that connects to a server it names without the test CA:

- `new MySqlConnection(...)` whose argument is not `DevDbTls.Apply(...)`;
- `SetDb`/`SetDbT` with a server (seven or more arguments, the seventh the database type) without `DevDbTls.GetSslCa(...)`;
- `new DataConnection(...)` with a server (five or more arguments) without it;
- a `new CentralConnectionBase { ... }` or `new CentralConnection { ... }` initializer with `ServerName=` and no `SslCa=DevDbTls.GetSslCa(...)`;
- `MiscData.MakeABackup(...)` with `serverName:` (or a server as its first argument) without `sslCa:DevDbTls.GetSslCa(...)`;
- `new OpenDentalServerMockIIS(...)` (any namespace prefix) with `server:` (or a server as its first argument) without it;

apart from a short list of deliberate exceptions, each with its reason (the TLS tests' own strings, servers over the network that the rule
refuses before connecting, and `LoopbackDatabaseTests`' connection without a CA on purpose). Calls of these names that name no server (they
take the thread's own connection, as `new OpenDentalServerMockIIS()` or `MakeABackup()`) are not checked. It also asserts that it found
calls of every kind; `TheConnectionScan_FindsEachCallShape_WithAndWithoutTheCa` checks its reading of each shape on synthetic source. A
package that adds a helper after this one is merged gets the fix in the failure message. Connections the scan cannot see by name (a
connection string built in program code from the thread's context) follow from the helpers' own `SetDb` with the CA.

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

Only if the owner has said yes to S2-2's client certificate (section 9), add `-WithClientCertificate` to step 4 (it is made with the CA,
so not later without a new CA). Its key goes to `%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient` (with `-DevFolder`: `DevDbTlsClient` beside
it), never to the CA's folder.

**To undo it** (also as administrator): `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1 -Revert`, then
`YES`. It removes the files in the folders the enable run recorded in `my.ini` (the plan lists them), also another account's after
`-DevUser`/`-DevFolder`; a `-DevFolder` that names another folder is refused. The dev tools and tests then connect as before (they find
no CA).

## 5. The lead's steps afterwards

1. Run the TLS tests with the requirement on: `$env:OPMS_DB_REQUIRE_TLS='1'`, then a Targeted run of `DbConnectionSecurityTests`,
   `SyntheticData.Tests` and `NoHqTests`. Every `DevDbTlsServerTests` test passes, except
   `WhenTheServerRequiresTls_AConnectionWithoutTlsIsRefused` (inconclusive until step 4).
2. The coordinator's next Full run with `OPMS_DB_REQUIRE_TLS=1`. Every connection the scan of section 3 checks (the helpers' strings,
   `SetDb`/`SetDbT`, `DataConnection` with a server, the `CentralConnectionBase` overrides, `MakeABackup` to a server, and the Middle Tier
   mock server's own thread) then connects with TLS and the test CA, and so does every thread that takes its context from them. What the
   run cannot show before step 4 is a connection outside those shapes that still has no CA: the server accepts it. Step 4's run is the
   check for that (a connection without TLS is then refused and its test fails), so run the Full run again after step 4.
3. Tell the other sessions first: once the server requires TLS, a worktree without this package fails its database tests (its `SetDb`
   helpers write `SslMode=none`). Every worktree that still runs database tests merges or rebases onto `main` with this package.
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
   in the same run as the CA. So it waits for the owner's answer (section 9). With a yes: `-Revert`, then the enable run again with
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
- `-DryRun` on this PC (exit 0; the user profile shortened here):

  ```text
  Not elevated: a dry run only. The real run must be started as administrator.
  Service:        MariaDB (Running), account NT SERVICE\MariaDB
  Server program: C:\Program Files\MariaDB 10.11\bin\mysqld.exe
  Option file:    C:\Program Files\MariaDB 10.11\data\my.ini
  Server:         10.11.19-MariaDB on 127.0.0.1:3306, data folder C:\Program Files\MariaDB 10.11\data\
  TLS now:        have_ssl=DISABLED, require_secure_transport=0, tls_version=TLSv1.2,TLSv1.3, this session's cipher ''

  Plan:
    1. Make a test CA (its private key stays in memory and is discarded) and a server certificate for localhost and 127.0.0.1.
       No client certificate (-WithClientCertificate is not given): S2-2's certificate login waits for the owner's yes. It can only be
       made in the same run as the CA, so it then means -Revert and this script again with -WithClientCertificate.
    2. Create C:\Program Files\MariaDB 10.11\opms-dev-tls (owner Administrators; SYSTEM and Administrators full control, NT SERVICE\MariaDB read) with
       ca.pem, server-cert.pem and server-key.pem.
    3. Write the CA certificate (public part only) to %LOCALAPPDATA%\OpenPmsDental\DevDbTls\ca.pem,
       the folder readable by <DOMAIN>\<user>, SYSTEM and Administrators only. No private key goes to this folder.
    4. Copy C:\Program Files\MariaDB 10.11\data\my.ini to C:\Program Files\MariaDB 10.11\data\my.ini.before-opms-dev-tls, then add at the end of its [mysqld] section (the comment lines
       record the folders above; -Revert and -RequireSecureTransport read them from there):
         # BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)
         # dev-folder=C:/Users/<user>/AppData/Local/OpenPmsDental/DevDbTls
         ssl_ca=C:/Program Files/MariaDB 10.11/opms-dev-tls/ca.pem
         ssl_cert=C:/Program Files/MariaDB 10.11/opms-dev-tls/server-cert.pem
         ssl_key=C:/Program Files/MariaDB 10.11/opms-dev-tls/server-key.pem
         tls_version=TLSv1.2,TLSv1.3
         # END Open PMS - Dental local test TLS
    5. Restart the service MariaDB (connections drop for a few seconds) and check a TLS session and the server
       certificate against the CA. If the server does not come back with TLS, the backup is restored and the files removed.
    require_secure_transport is not set: unencrypted connections keep working until -RequireSecureTransport.

  Dry run: nothing was changed.
  ```

  `-DryRun -WithClientCertificate` has the client step in its own folder, and the record line for it:

  ```text
    1. Make a test CA (its private key stays in memory and is discarded) and a server certificate for localhost and 127.0.0.1.
       With -WithClientCertificate also a client certificate (clientAuth) for the certificate-login test of S2-2.
    ...
    4. Write the client certificate and key to %LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient\client-cert.pem and %LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient\client-key.pem:
       a folder of its own, not the CA's, <DOMAIN>\<user> read only, SYSTEM and Administrators full control.
    5. Copy ... then add at the end of its [mysqld] section (...):
         # BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)
         # dev-folder=C:/Users/<user>/AppData/Local/OpenPmsDental/DevDbTls
         # client-folder=C:/Users/<user>/AppData/Local/OpenPmsDental/DevDbTlsClient
         ...
  ```

  `-DryRun -WithClientCertificate -ClientFolder %LOCALAPPDATA%\OpenPmsDental\DevDbTls\client` is refused (`... overlap. The client key
  goes to a folder of its own, never where the dev tools read the CA.`). `-DryRun -RequireSecureTransport` refuses (no block yet);
  `-DryRun -Revert` says there is nothing to revert.
- `New-DevDatabase.ps1`'s config writing, run on a temporary folder with `OPMS_DB_TLS_CA`: one `SslCa` element after `MySQLPassHash` with the
  CA, none without a CA or for another port; with `OPMS_DB_REQUIRE_TLS=1` and no CA the CA look-up stops with the message.

Tests (Targeted runs, all passed): `DbConnectionSecurityTests` 140 (130 passed, 10 inconclusive: the nine `DevDbTlsServerTests` and
`DataConnection_Loopback_WithTheLocalTestServersCa_VerifyCa`, until the CA exists), `SyntheticData.Tests` 58 (57 passed, the pre-existing
`RealDump_Has422Tables` inconclusive), `MiddleTierMockTests` 125, `ReleaseFlowTests` 120 (119 passed), `LibraryBindingTests` 12, `NoHqTests`
58, `EhrTests` 88 (its helper came from `main`), and for the hand-edited helpers `BSERVERTests` 57, `LibraryUpdateTests` 100,
`AdoptionValidation.Tests` 69 and `DbGrantsTests` 3 (2 passed, the grants run inconclusive without its variables). The full build has no
error. `ForkMigrationTests` is named in the package's list but left out under the
rule for every package (the coordinator runs it with the init file); its helper change is the same mechanical one as the others.

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
  dev folder, and with `-WithClientCertificate` puts the key in the client folder.
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
  (section 7: the dev tools' row and a row for `DevDbTls`). In the program's assemblies only `OpenPmsDentalBusiness/WebServices/OpenDentalServerMockIIS.cs`
  (the test-only mock server: an optional `sslCa`, default `""`, which no shipped code path passes).
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

## 9. Owner question

- **S2-2's client certificate.** Should the script make a client certificate signed by the test CA, for S2-2's certificate-login test (a
  `REQUIRE X509` test account on the local server; the key in `%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient`, readable by the dev account)?
  Until the answer is yes the script makes none and that part of S2-2 waits. A yes after the enable run means `-Revert` and the enable run
  again with `-WithClientCertificate` (a new CA), because the CA's key is discarded after each run.
