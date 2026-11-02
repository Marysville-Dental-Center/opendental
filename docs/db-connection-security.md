# Encrypted database connections: the server side

From the version after 0.0, Open PMS - Dental connects to a database server over the network only encrypted, and only when the server's
certificate comes from the office's own certificate authority (CA) and names the server
([docs/specs/db-connection-security.md](specs/db-connection-security.md)). A connection to the same computer (`localhost`, `127.0.0.1`,
`::1`) is exempt. **Set the server up as below before any workstation installs or self-updates to that version.**

This guide covers the certificates and the MariaDB settings only. Firewalls, the network and backups of the keys are the office's own
business. Placeholders: `<office-ca>` is a name for the office CA (for example "Example Dental Office CA"), `<db-server>` the name the
workstations use for the database server, exactly as they type it into the connection settings (for example `db.example.test`).

Run the commands on a trusted computer with OpenSSL 1.1.1 or later (`openssl version`). Keep `office-ca.key` off the network, for example on
an encrypted USB drive: whoever has it can issue certificates the workstations trust.

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

In the server's option file (`my.ini` on Windows, a file under `/etc/mysql/mariadb.conf.d/` on Linux), section `[mariadb]`:

```ini
[mariadb]
ssl_ca=/etc/mysql/ssl/office-ca.pem
ssl_cert=/etc/mysql/ssl/server-cert.pem
ssl_key=/etc/mysql/ssl/server-key.pem
require_secure_transport=ON
tls_version=TLSv1.2,TLSv1.3
```

Use forward slashes in Windows paths too (`C:/ProgramData/MariaDB/ssl/office-ca.pem`). Restart MariaDB, then check from the server:

```sql
SHOW GLOBAL VARIABLES LIKE 'have_ssl';               -- YES
SHOW GLOBAL VARIABLES LIKE 'require_secure_transport'; -- ON
```

`require_secure_transport=ON` refuses every TCP connection without TLS, including one from the server computer itself. If the program also
runs on the server computer, give it `<db-server>` in its connection settings, not `localhost`.

## 5. The database users

The workstations' user, authenticated by the client certificate:

```sql
CREATE USER 'opmsd'@'%'
  REQUIRE SUBJECT '/CN=opmsd-workstations' AND ISSUER '/CN=<office-ca>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, CREATE TEMPORARY TABLES, LOCK TABLES
  ON `opendental`.* TO 'opmsd'@'%';
```

Replace `%` with the office's network (for example `'opmsd'@'192.0.2.%'`) and `opendental` with the database. A database not set up for
certificates uses the password fallback, still over TLS (the program refuses anything else):

```sql
CREATE USER 'opmsd_pw'@'%' IDENTIFIED BY '<a long random password>' REQUIRE SSL;
```

The installer or the tool stores that password on each workstation with DPAPI (`DbPassword=` in a secrets file, or typed in the tool).

## 6. The workstations

Interactive: run the setup as an administrator and give the server, database, user, `office-ca.pem` and `client.pfx` on the connection
pages; the connection settings tool opens and asks for the `.pfx` password. Silent:

```bat
OpenPmsDental-<version>-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /DBSERVER=<db-server> /DBNAME=opendental /DBUSER=opmsd ^
  /DBCA=\\<file-share>\office-ca.pem /DBPFX=\\<file-share>\client.pfx /DBSECRETSFILE=C:\Temp\dbsecrets.txt
del C:\Temp\dbsecrets.txt
```

`dbsecrets.txt` holds the line `PfxPassword=...` (or `DbPassword=...` for the password fallback); delete it right after setup. Later
changes: `OpenPmsDentalDbSettings.exe` in the program folder, as an administrator.

## 7. Renewing

Renew the server certificate before it expires (section 2, the same CA): the workstations need no change. A new client certificate:
import it on every workstation with the tool. A new office CA: every workstation needs the new `office-ca.pem` (the tool's CA field or
`/ca=`) before the server switches to certificates from it.
