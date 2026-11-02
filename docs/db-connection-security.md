# Encrypted database connections: the server side

From the version after 0.0, Open PMS - Dental connects to a database server over the network only encrypted, and only when the server's
certificate comes from the office's own certificate authority (CA) and names the server
([docs/specs/db-connection-security.md](specs/db-connection-security.md)). This is for the program's own database account, the
connection in `FreeDentalConfig.xml`, and no longer `root` (section 5). A connection to the same computer (`localhost`, `127.0.0.1`,
`::1`) is exempt. A read-only or reporting server named in the program's preferences (Setup) keeps working as before, with its own
account and its own CA setting (spec, section 5.1); this guide does not cover it. **Switch over in the order below**: version 0.0 connects
without TLS, so the server must keep taking connections without TLS until every workstation runs the new version.

This guide covers the certificates and the MariaDB settings only. Firewalls, the network and backups of the keys are the office's own
business. Placeholders: `<office-ca>` is a name for the office CA (for example "Example Dental Office CA"), `<db-server>` the name the
workstations use for the database server, exactly as they type it into the connection settings (for example `db.example.test`),
`<office-network>` the workstations' addresses as MariaDB matches them (for example `192.0.2.%`).

Run the commands on a trusted computer with OpenSSL 1.1.1 or later (`openssl version`). Keep `office-ca.key` off the network, for example on
an encrypted USB drive: whoever has it can issue certificates the workstations trust.

## 0. The order of the switch-over

1. **TLS on the server, not yet required.** Make the certificates (sections 1 to 3), set `ssl_ca`, `ssl_cert`, `ssl_key` and `sql_mode`
   and restart MariaDB with `require_secure_transport` still OFF (section 4). Create the program's database account (section 5). The v0.0
   workstations keep working: they connect as before, without TLS, with their old user.
2. **The update server.** Uninstall version 0.0 there first (Windows Settings > Apps, or Control Panel > Programs and Features). Its
   uninstaller removes the whole program folder, including the `FreeDentalConfig.xml` its setup wrote. Then install the new version with
   its setup, as an administrator (section 6): setup runs the connection settings tool, which sets up the office CA and the client
   certificate or the password. Start the program there as an administrator: it makes the backup database and updates the database
   (section 5 lists the rights this needs). **Do not publish the new version yet.** At this start, and at every start until it is
   published, the program offers to publish it ([docs/release-process.md](release-process.md), `PrefL.CheckProgramVersion`): answer
   **Cancel**. Do not start it with `IsSilentUpdate=true` either: a silent start publishes by itself when nothing is published yet. A
   workstation that still runs version 0.0 pulls any newer published build at its next start and installs it over itself, and that keeps
   the `FreeDentalConfig.xml` to which 0.0's setup gave every user of the computer modify rights: a user who is not an administrator could
   then point the program at another CA and another server, to which it would send the stored password or present the client
   certificate.
3. **The other workstations.** On each, uninstall version 0.0 the same way, then run the new setup as an administrator, with its pages or
   with the silent switches of section 6. Uninstall first also on a workstation that already runs the new build because it pulled it (the
   release was published too early): the "Open PMS - Dental" entry in Apps is still the one of 0.0, and its uninstaller removes the
   program folder with the old `FreeDentalConfig.xml`. Never set such a workstation up only with the connection settings tool, or by
   running the new setup over it: both keep that file and its rights. When the program offers to publish on a workstation, answer
   **Cancel** there too; when it says to
   run the new release on the update server first (Update Setup names another computer as the update server), close it: it starts once
   the release is published. When version 0.0 is uninstalled on every workstation, publish the release on the update server with Update
   Setup > **Recopy** ([docs/release-process.md](release-process.md)). There is no in-place upgrade from version 0.0 (owner decision of
   2026-11-02) as long as the release is published only then: setup's upgrade path, for a program folder that already has
   `FreeDentalConfig.xml`, is for the versions after this one.
4. **TLS required.** When no workstation connects without TLS any more (section 4.1), turn on `require_secure_transport`, keep it in the
   option file, and drop the old users (such as the `root` accounts v0.0 used over the network) or make them require TLS. From then on a
   program on the server computer itself must connect with `<db-server>`, not `localhost` (section 4.1).

## 1. The office CA

```sh
openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 \
  -keyout office-ca.key -out office-ca.pem \
  -subj "/CN=<office-ca>" \
  -addext "basicConstraints=critical,CA:TRUE" \
  -addext "keyUsage=critical,keyCertSign,cRLSign"
```

OpenSSL asks for a pass phrase for `office-ca.key`; give one. `office-ca.pem` is the office CA certificate: it is public, and every
workstation gets it (the installer's "Office CA certificate file", the tool's `/ca=`).

## 2. The server certificate

The name in `subjectAltName` must be the name the workstations connect to (`<db-server>`). Add every other name or address they use, for
example `subjectAltName=DNS:<db-server>,DNS:<db-server-short-name>,IP:192.0.2.10`.

```sh
openssl req -new -newkey rsa:2048 -nodes \
  -keyout server-key.pem -out server.csr \
  -subj "/CN=<db-server>"

cat > server.ext <<'EOF'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:<db-server>
EOF

openssl x509 -req -in server.csr -CA office-ca.pem -CAkey office-ca.key -CAcreateserial \
  -out server-cert.pem -days 825 -sha256 -extfile server.ext

openssl verify -CAfile office-ca.pem server-cert.pem
```

`server-key.pem` has no pass phrase, because MariaDB reads it at start-up. Only the account MariaDB runs as may read it.

## 3. The client certificate (one for every workstation)

One shared certificate for all workstations (owner decision Q2). Its private key is imported on each workstation as not exportable.

```sh
openssl req -new -newkey rsa:2048 -nodes \
  -keyout client-key.pem -out client.csr \
  -subj "/CN=opmsd-workstations"

cat > client.ext <<'EOF'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=clientAuth
EOF

openssl x509 -req -in client.csr -CA office-ca.pem -CAkey office-ca.key -CAcreateserial \
  -out client-cert.pem -days 825 -sha256 -extfile client.ext

openssl pkcs12 -export -in client-cert.pem -inkey client-key.pem -certfile office-ca.pem \
  -name "Open PMS - Dental workstations" -keypbe AES-256-CBC -certpbe AES-256-CBC -macalg SHA256 \
  -out client.pfx
```

OpenSSL asks for an export password: the `.pfx` password the installer or the tool asks for (`PfxPassword=` in a secrets file). Then delete
`client-key.pem` and `client.csr`. Print the subject and the issuer the way MariaDB compares them:

```sh
openssl x509 -in client-cert.pem -noout -subject -issuer -nameopt compat
```

which prints `subject=/CN=opmsd-workstations` and `issuer=/CN=<office-ca>`.

Certificates per computer later need no change on the workstations: issue one per computer (for example `-subj "/CN=<computer>"`), import
it with the tool (`/pfx=`, or `/thumbprint=` for one already in the computer's store), and give the database user `REQUIRE ISSUER` only, or
one user per computer.

## 4. MariaDB

In the server's option file (`my.ini` on Windows, a file under `/etc/mysql/mariadb.conf.d/` on Linux), section `[mariadb]`, for step 1 of
the order:

```ini
[mariadb]
ssl_ca=/etc/mysql/ssl/office-ca.pem
ssl_cert=/etc/mysql/ssl/server-cert.pem
ssl_key=/etc/mysql/ssl/server-key.pem
tls_version=TLSv1.2,TLSv1.3
# The program needs a blank sql_mode. Set here, the program's account needs no SUPER right to set it at every start (section 5).
sql_mode=""
# require_secure_transport=ON comes in step 4, when every workstation runs the new version (section 4.1).
```

Use forward slashes in Windows paths too (`C:/ProgramData/MariaDB/ssl/office-ca.pem`). Restart MariaDB, then check from the server:

```sql
SHOW GLOBAL VARIABLES LIKE 'have_ssl';                 -- YES
SHOW GLOBAL VARIABLES LIKE 'sql_mode';                 -- blank (NO_AUTO_CREATE_USER also does)
SHOW GLOBAL VARIABLES LIKE 'require_secure_transport'; -- OFF until step 4
```

### 4.1 Requiring TLS (step 4)

First make sure no session without TLS is left. The new users (section 5) can only connect with TLS, so it is enough that no session of an
old user remains, while every workstation has the program running:

```sql
SELECT USER, HOST FROM information_schema.PROCESSLIST WHERE USER NOT IN ('opmsd','opmsd_pw','system user','event_scheduler');
```

Only your own administrator session should be listed. With `performance_schema=ON` (it is OFF by default in MariaDB) this lists every
session without TLS directly:

```sql
SELECT t.PROCESSLIST_USER, t.PROCESSLIST_HOST FROM performance_schema.threads t
  JOIN performance_schema.status_by_thread s ON s.THREAD_ID=t.THREAD_ID AND s.VARIABLE_NAME='Ssl_version'
  WHERE t.PROCESSLIST_ID IS NOT NULL AND s.VARIABLE_VALUE='';
```

Then require TLS now and after every restart, and retire the old users:

```sql
SET GLOBAL require_secure_transport=ON;
DROP USER '<old-user>'@'<old-host>';   -- or: ALTER USER '<old-user>'@'<old-host>' REQUIRE SSL;
```

and add `require_secure_transport=ON` to the `[mariadb]` section of the option file.

`require_secure_transport=ON` refuses every TCP connection without TLS, including one from the server computer itself, where the program
connects to `localhost` without TLS (such a connection is exempt from the program's rule, but MariaDB refuses it). If the program also runs
on the server computer, give it `<db-server>` in its connection settings, not `localhost`, and set it up like any workstation.

## 5. The program's database account

The program no longer uses `root` (owner decision of 2026-11-02). The office gives it an account of its own, `opmsd` in this guide (the
installer's default user), with only the rights it needs to run, to update the database and to make the backup that comes before every
update. `<database>` is the office database (`opendental` by default). **This list is untested against a real server**: try it on a test
copy (a restored backup on a test server) before step 2 of the switch-over. The spec lists where in the program each right is needed
([specs/db-connection-security.md](specs/db-connection-security.md), section 9).

The workstations' account, authenticated by the client certificate:

```sql
CREATE USER 'opmsd'@'<office-network>'
  REQUIRE SUBJECT '/CN=opmsd-workstations' AND ISSUER '/CN=<office-ca>';

-- The office database.
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, INDEX, CREATE TEMPORARY TABLES
  ON `<database>`.* TO 'opmsd'@'<office-network>';

-- The backup databases: before every database update the program copies the office database into <database>backup_MM_dd_yyyy
-- (<database>backup_MM_dd_yyyy_1, _2, ... when that name exists). Without them the update stops with "Backup failed. Your database
-- has not been altered."
GRANT SELECT, INSERT, CREATE, ALTER
  ON `<database>backup\_%`.* TO 'opmsd'@'<office-network>';

-- Server-wide: FLUSH TABLE after each table the backup copies (RELOAD), and the question every backup asks whether the server
-- replicates, SHOW MASTER STATUS and SHOW SLAVE STATUS (BINLOG MONITOR, REPLICA MONITOR). None of them reads or changes data.
GRANT RELOAD, BINLOG MONITOR, REPLICA MONITOR ON *.* TO 'opmsd'@'<office-network>';
```

In a database name in `GRANT`, `_` and `%` are wildcards: write every `_` of `<database>` as `\_` in both grants (for `opendental_pms`:
`` `opendental\_pms`.* `` and `` `opendental\_pmsbackup\_%`.* ``), so that they cover exactly these databases. The backup grant is a
wildcard on purpose: each backup is a new database, and the grant lets the account create those and nothing else. It never needs to drop
them; remove old backups with your own administrator account.

What the account does not get, and why it does not need it:

- No `SUPER`. At every start the program sets the server's `sql_mode` to blank when it is anything else, which needs `SUPER`; with
  `sql_mode=""` in the option file (section 4) it never tries. Without that line every start stops with "Unable to set global sql mode."
- No `ALL PRIVILEGES`, no `GRANT OPTION`, no `CREATE USER`, no `FILE`, no `PROCESS`, no `SHOW DATABASES`: the program's own statements
  need none of them. Cancelling a running user query (`KILL QUERY`) stops the account's own query only.
- A server that replicates (a replication set-up in the program, or binary logging with replicas) also needs `BINLOG ADMIN` for the
  backup, which then turns binary logging off for its own session (`SET SESSION sql_log_bin`). The program's replication set-up window
  (stop and start the replica) needs `REPLICATION SLAVE ADMIN`: run it as your administrator, not as `opmsd`.
- `RELOAD` also allows the other `FLUSH` statements (and some `RESET` statements) server-wide. It gives no access to any data.

A database not set up for certificates uses the password fallback, still over TLS (the program refuses anything else), with the same
three grants (`'opmsd_pw'@'<office-network>'` in place of `'opmsd'@'<office-network>'`):

```sql
CREATE USER 'opmsd_pw'@'<office-network>' IDENTIFIED BY '<a long random password>' REQUIRE SSL;
```

The installer or the tool stores that password on each workstation with DPAPI (`DbPassword=` in a secrets file, or typed in the tool).

The connection settings tool warns, without refusing, when `root` is the user for a server over the network. A program on the server
computer that connects to `localhost` (section 6) does so without TLS until step 4, so it needs a password account for the server's own
address without `REQUIRE SSL` (`'opmsd_pw'@'localhost'`, or `'opmsd_pw'@'127.0.0.1'` when the server does not resolve names), with the
same three grants. From step 4 on it connects with `<db-server>` like any workstation.

## 6. The workstations

**Version 0.0 first.** A workstation with version 0.0 is uninstalled before the new setup runs (section 0): its uninstaller removes the
program folder with the `FreeDentalConfig.xml` its setup wrote, so the new setup makes a new install. Nothing of the v0.0 connection is
carried over. This holds only while the new release is not published: publish it when 0.0 is uninstalled on every workstation (section 0,
steps 2 and 3). A workstation that pulled the new build anyway is uninstalled through Apps like any 0.0 workstation (the entry is still
0.0's) before the setup runs.

**A new install** (the program folder has no `FreeDentalConfig.xml`). Interactive: run the setup as an administrator and give the server,
database, user (`opmsd` by default, section 5), `office-ca.pem` and `client.pfx` on the connection pages; the connection settings tool
opens filled with them and asks for the `.pfx` password (or, without a `.pfx`, for the database password). Silent:

```bat
OpenPmsDental-<version>-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /LOG=C:\Temp\setup.log /DBSERVER=<db-server> /DBNAME=opendental ^
  /DBUSER=opmsd /DBCA=\\<file-share>\office-ca.pem /DBPFX=\\<file-share>\client.pfx /DBSECRETSFILE=C:\Temp\dbsecrets.txt
del C:\Temp\dbsecrets.txt
```

`dbsecrets.txt` holds the line `PfxPassword=...` (or `DbPassword=...` for the password fallback); delete it right after setup. The tool's
result line is in the setup log.

**An existing install of a later version** (setup keeps its `FreeDentalConfig.xml`). Setup run with any of `/DBCA=`, `/DBPFX=`,
`/DBTHUMBPRINT=`, `/DBSECRETSFILE=` runs the tool with only the switches given (silent: `/silent`; interactive: the tool's window), so the
silent command above sets up an upgraded workstation as well; `/DBSERVER=`, `/DBNAME=` and `/DBUSER=` change the server, database and user
only when they are given. Without those switches setup only checks the settings against the rule (and, interactive, offers the tool's
window when they fail). To change the settings of an installed workstation, run the tool itself from an elevated command prompt:

```bat
start "" /wait "C:\Program Files (x86)\Open PMS - Dental\OpenPmsDentalDbSettings.exe" /silent /ca=\\<file-share>\office-ca.pem ^
  /pfx=\\<file-share>\client.pfx /secretsfile=C:\Temp\dbsecrets.txt /log=C:\Temp\dbsettings.log /test
echo %ERRORLEVEL%
del C:\Temp\dbsecrets.txt
```

Exit code 0 means done; the others are in the spec (section 4.2), and the log line says what failed. `/test` connects once after saving.
Without `/silent` the tool's window opens instead.

**The server computer or a single-computer office** (the program connects to `localhost`): an interactive setup opens the tool's window in
password mode, to store the database password of that user with DPAPI (Cancel for a user without a password). Silent: `/DBSECRETSFILE=`
with a `DbPassword=...` line. Once `require_secure_transport` is ON (section 4.1), such a computer connects with `<db-server>` instead.

Later changes: `OpenPmsDentalDbSettings.exe` in the program folder, as an administrator. The tool keeps its copy of the office CA in the
program folder (`ConnectionSettings\office-ca.pem`), which only administrators may change.

**Shortcuts and scripts that start the program with a connection** (owner decision of 2026-11-02, kept as built). `ServerName=`,
`DatabaseName=` and `MySqlUser=` still choose the database, and the connection always takes this computer's office CA and client
certificate. A database password on the launch command (`MySqlPassword=`, `MySqlPassHash=`, `MySqlPassObfuscated=`) is refused for a
server over the network: the program uses the password or the certificate the tool stored on this computer, for the user configured
there. For a server on the same computer it is accepted as before. The program's own log-in on the launch command (`UserName=`,
`OdPassword=`, `OdPassObfuscated=`) stays as it was.

## 7. Renewing

Renew the server certificate before it expires (section 2, the same CA): the workstations need no change. A new client certificate:
import it on every workstation with the tool. A new office CA: every workstation needs the new `office-ca.pem` (the tool's CA field or
`/ca=`) before the server switches to certificates from it.
