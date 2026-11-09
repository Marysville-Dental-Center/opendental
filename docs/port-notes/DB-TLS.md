# Port note: DB-TLS (O-12, the local test database runs encrypted)

Package O-12 of the .NET 10 plan (`docs/specs/net10-plan.md` section 12, O-12, answered by the owner on 2026-11-08: yes), built on branch
`dev/dbtls-1` from `main` (99bb32282). The owner runs one script once as administrator, `tools/Enable-LocalDbTls.ps1`, which turns on
TLS in the local MariaDB service on `127.0.0.1:3306`; from then on the program's development set-up, the test helpers and the dev tools
connect encrypted and verify the server against the script's test CA. S2-2's TLS and certificate-login test runs against this server, so
O-8's temporary second server is not needed.

The program's own connection rule (`docs/specs/db-connection-security.md`) does not change.

## 1. What this PC has (read-only survey, 2026-11-08)

Read through SQL on `127.0.0.1:3306` (the dev connection, `root`) and from files; nothing was changed.

| What | Found |
|---|---|
| Service | `MariaDB`, running, automatic start, account `NT SERVICE\MariaDB` (a virtual service account). Command line: `mysqld.exe --defaults-file=<install folder>\data\my.ini MariaDB`, the MariaDB 10.11 installer's layout (`%ProgramFiles%\MariaDB 10.11`). |
| Version | `10.11.19-MariaDB`, `version_compile_os` Win64. |
| TLS library | `version_ssl_library` = `WolfSSL 5.9.1` (MariaDB's Windows builds are linked with wolfSSL; `have_openssl` = `NO`). |
| TLS state | `have_ssl` = `DISABLED` (compiled in, not configured); `ssl_ca`, `ssl_capath`, `ssl_cert`, `ssl_key`, `ssl_cipher`, `ssl_crl`, `ssl_crlpath` empty; `tls_version` = `TLSv1.2,TLSv1.3` (the 10.11 default); `require_secure_transport` = `OFF`. The dev session's `Ssl_cipher` is empty: today every connection is unencrypted. |
| Option file | `my.ini` in the data folder, 633 bytes, ASCII, LF line ends (one CRLF), sections `[mysqld]` and `[client]`; `port=3306`, `bind-address=127.0.0.1` (only this computer can connect), `sql_mode=''` and the other Open Dental settings; no TLS line. ACL: inherited from the data folder (SYSTEM, Administrators, `NT SERVICE\MariaDB` and the developer's account full control, Users read). |
| Own certificate | None. MariaDB 10.11 does not make one; only MariaDB 11.4 and later generate a self-signed certificate at start-up when none is configured. |
| Tools that make PEM files without a download | Windows PowerShell 5.1 on .NET Framework 4.8 (release 534296): `System.Security.Cryptography.X509Certificates.CertificateRequest` and `SubjectAlternativeNameBuilder` (.NET Framework 4.7.2 and later) make a CA and certificates in memory, and an ephemeral `RSACng` key exports as PKCS#8 (`CngKey.Export(CngKeyBlobFormat.Pkcs8PrivateBlob)`, export policy `AllowPlaintextExport`); tried in memory, nothing written. `New-SelfSignedCertificate` (PKI module) and `certutil.exe` exist too, but they work through the certificate store (keys land in a store before they can be exported). MariaDB's `bin` folder has no `openssl.exe`. No C# step through the build lane is needed. |
| Client | `mariadb.exe` 15.1 (10.11.19) in the same `bin` folder: `--ssl` on by default (falls back to plain when the server has no TLS), `--ssl-ca`, `--ssl-verify-server-cert` (off by default), `--tls-version`. |
| Lane and test scripts | `run-tests.ps1` (the repository's and the coordinator's copy) and the build lane do not connect to the database. They need no change: the switch below is an environment variable, which the test processes inherit. |

## 2. Design

To be completed with the package.
