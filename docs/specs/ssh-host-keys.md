# Remembered SSH host keys

Owner decision of 2026-11-02 (tier 1, question 1): the program remembers the host key of every SSH server it connects to, in the
database, and refuses a server whose key changed until an administrator accepts the new key. Package `next/fu-ssh-1`, on the Next batch
after SSH.NET 2026 replaced Tamir.SharpSSH (`next/libt1-2`).

Before: version 0.0 never verified a host key (SharpSSH ran with `StrictHostKeyChecking=no`; SSH.NET 2024 had no handler). The SSH.NET
port (`next/libt1-2`) pinned the first key a server presented for the life of the process only (`SshHostKeys`, in memory), so every restart
trusted whatever key the server presented.

## Contents

1. What the office gets
2. Which connections
3. How a connection is checked
4. Table
5. The SSH Host Keys window
6. Permissions and the audit trail
7. Messages
8. Middle Tier, services and caching
9. Schema migration and the adoption tools
10. Tests
11. Decisions taken here (owner questions)

## 1. What the office gets

- **Trust on first use.** The first successful connection to a server (host name and port) stores the key it presented: its key type and
  SHA-256 fingerprint, when, and by which user. The audit trail gets an entry.
- **The same key, every time.** Every later connection, from any workstation or service, must present that key. A server that presents
  another key, of the same type or another type, is refused **during the key exchange, before the user name, the password or any file is
  sent**, with a message naming the server and both fingerprints. The refused key is recorded on the server's row, so an administrator sees
  it, and the audit trail gets an entry.
- **An administrator accepts a changed key.** Setup, SSH Host Keys (Security Admin) lists the stored keys. For a server whose key was
  refused it shows the stored and the new fingerprint side by side; Accept Changed Key replaces the stored key with the refused one. Remove
  forgets a stored key, so the next connection stores the key the server presents then. Both are in the audit trail.
- **DSA keys stay unsupported** (owner accepted on 2026-11-02, tier 1 question 2): SSH.NET 2026 does not offer `ssh-dss`, so a server that has only a DSA key cannot be
  reached, as with the SSH.NET port before this package. Ed25519, ECDSA (nistp256, nistp384, nistp521) and RSA keys work.

## 2. Which connections

Every SSH connection of the program is made by `OpenPmsDentalStorage` (the only project that references SSH.NET), through
`SshHostKeyCheck`, which needs a host key store (`ISshHostKeyStore`) for every client it creates; there is no way to make an SSH client
without one. The business layer's store is `SshHostKeys.Store` (the `sshhostkey` table). The callers:

| Connection | Where | How |
|---|---|---|
| SFTP image storage (Setup, Data Paths, SFTP) | `CloudStorage` (upload, download, delete, thumbnail, move, copy) | `Sftp.*` task states |
| Transworld (TSI) uploads and the connection test | `TsiTransLogs`, `FormArManager`, `ControlManage` | `Sftp.Upload`, `Sftp.IsConnectionValid` |
| Denti-Cal clearinghouse | `DentiCal.Launch` | `SftpSession` |
| Mercury clearinghouse | `MercuryDE.Launch` | `SftpSession` |
| HL7 SFTP mode (the HL7 service) | `ServiceHL7` | `SftpSession` |

EDS Medical (`EdsMedical`) sends claims over HTTPS, not SSH; it only had an unused SharpSSH `using` before the port. No other program of the
solution makes SSH connections (a test checks that only `OpenPmsDentalStorage` references SSH.NET and that every SSH client it creates goes
through the check).

## 3. How a connection is checked

`SshHostKeyCheck` is attached to each SSH.NET client when it is created (`SshHostKeyCheck.Attach`) and makes every connect of that client
(`SshHostKeyCheck.Connect`; `Sftp`'s `ConnectIfNeeded` and `IsConnectionValid`, and `SftpSession.Connect`, call it):

1. Before connecting, on the caller's thread, it reads the stored key of the server from the store (host in lower case, trimmed, and port).
   If the store cannot be read, the connection is refused (nothing is sent to the server).
2. SSH.NET connects. During the key exchange SSH.NET raises `HostKeyReceived` with the host key the server presents, and checks that the
   server holds its private key (the server signs the exchange hash with it); a key that fails either check ends the key exchange. The check
   compares the presented key (the key type read from the key blob, and the SHA-256 fingerprint) with the stored key:
   - no stored key: trusted for now (first use), and remembered for this connection;
   - the same key type and fingerprint: trusted;
   - anything else: not trusted. SSH.NET then ends the key exchange; it never sends the user name or the password (authentication only
     starts after the key exchange).
   A later key exchange on the same connection (a re-key) must present the same key again.
3. If the connection was refused for its host key, the check records the refused key on the server's row (`SshHostKeys.RecordRefused`) and
   throws `SshHostKeyRefusedException` (an `SshConnectionException`) with the message of section 7.
4. If the connection succeeded (signed in) and no key was stored, the check stores the key (`SshHostKeys.InsertFirstUse`). Two
   workstations may connect to a new server at the same moment: the table's unique key on host and port lets only one row in, and the store
   returns the key that is stored afterwards. If that is not the key this connection saw, the check disconnects and refuses, as in 3. If the
   key cannot be stored, the check disconnects and refuses too, so that no connection runs on a key the program has not remembered.

A connection that fails before it is signed in (wrong password, network error) stores nothing: only a successful connection stores a key.
The key type is read from the host key blob, not from the negotiated algorithm, so one RSA key is one key under `rsa-sha2-512`,
`rsa-sha2-256` and `ssh-rsa`. SSH.NET offers the same host key algorithms in the same order every time, so a server presents the same key
type every time; a key of another type is refused like a changed key (a man in the middle could otherwise get past the check by offering a
type not seen yet).

Host names are compared without case. A server reached by two names (or by its name and its IP address) has two rows, as in OpenSSH's
`known_hosts`.

## 4. Table

One fork-only table (not Open Dental schema), created by fork migration step 2026110221 (`SshHostKeySteps.cs`, `SshHostKeyTableStep`), from
its declaration in `SshHostKeyTables` (a `ForkTableSet`). MyISAM, utf8mb3. No migration step writes a row into it. Levels 2026110222 to
2026110229 stay free for this feature.

### sshhostkey: one remembered host key per server

| Column | Type | Meaning |
|---|---|---|
| `SshHostKeyNum` | `bigint(20) NOT NULL AUTO_INCREMENT` | Primary key. |
| `Host` | `varchar(255) NOT NULL` | The host name or IP address as the connection names it, trimmed and in lower case. |
| `Port` | `int(11) NOT NULL` | The TCP port. |
| `KeyType` | `varchar(255) NOT NULL` | The stored key's type as its key blob names it, for example `ssh-ed25519`, `ecdsa-sha2-nistp256`, `ssh-rsa`. |
| `FingerprintSha256` | `varchar(255) NOT NULL` | The stored key's SHA-256 fingerprint, base64 without padding, as OpenSSH prints it after `SHA256:`. |
| `UserNum` | `bigint(20) NOT NULL` | FK to `userod.UserNum`: who stored the key (the user whose connection stored it, or the administrator who accepted it). 0 for a service (the HL7 service). |
| `DateTStored` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When the key was stored or accepted (the database server's time). |
| `IsAccepted` | `tinyint(4) NOT NULL` | 0: stored on first use. 1: accepted by an administrator (Accept Changed Key). |
| `RefusedKeyType` | `varchar(255) NOT NULL` | The type of the last key the server presented that was refused. Empty when none was refused since the key was stored. |
| `RefusedFingerprintSha256` | `varchar(255) NOT NULL` | Its SHA-256 fingerprint. Empty when none. |
| `DateTRefused` | `datetime NOT NULL DEFAULT '0001-01-01 00:00:00'` | When a connection last refused that key. MinValue when none. |

Keys: `HostPort` (`UNIQUE`, on `Host` and `Port`: one row per server), `UserNum`.

Accept Changed Key replaces `KeyType` and `FingerprintSha256` with the refused key, sets `IsAccepted`, `UserNum` (the administrator) and
`DateTStored`, and clears the three refused columns. Remove deletes the row.

## 5. The SSH Host Keys window

`FormSshHostKeys`, Setup, SSH Host Keys (in the Setup menu's list, after Spell Check), and the SSH Host Keys button on the SFTP tab of
Setup, Data Paths. Opening it needs Security Admin.

- The grid lists every stored key: server (`host:port`), key type, SHA-256 fingerprint, stored (date and time, user, "first use" or
  "accepted"), and the changed key a connection refused (type, fingerprint, when), if any. Rows with a refused key are highlighted.
- Below the grid, the selected row's stored key and refused key in full, in read-only boxes, so they can be compared with the fingerprint
  the server's administrator gives (for example `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` on the server).
- **Accept Changed Key** (only for a row with a refused key) asks for confirmation, naming the server, the old and the new fingerprint, and
  replaces the stored key with the refused one. The business layer accepts only the refused key the window showed: if another connection
  recorded a different refused key in the meantime, it refuses and the window refreshes.
- **Remove** asks for confirmation and deletes the row; the next connection to the server stores the key it presents then.
- **Close**.

## 6. Permissions and the audit trail

- The window and both changes need **Security Admin**. `SshHostKeys.AcceptRefusedKey` and `SshHostKeys.Delete` check it themselves, on the
  server side of the Middle Tier, and throw without changing anything when the user does not have it.
- Storing a first key and recording a refused key need no permission: they happen as part of any user's (or the HL7 service's) connection.
- Every entry is logged under a new audit-only permission type, **`EnumPermType.SshHostKeys` (10100, "SSH Host Keys")**, the first value of
  this feature's range 10100 to 10149 (`docs/port-notes/FORK-ENUMS.md`). It is never granted and is not in the security tree; the Audit
  Trail can filter on it. FKey is the `SshHostKeyNum`. The entries:
  - `SSH host key stored on first use: sftp.example.test:22, ssh-ed25519 SHA256:...`
  - `SSH host key refused: sftp.example.test:22 presented ssh-ed25519 SHA256:..., the stored key is ssh-ed25519 SHA256:...`. Written once
    per refused key: a refusal of the key that is already recorded as refused only updates `DateTRefused`, so the HL7 service, which tries
    every minute, does not fill the audit trail.
  - `SSH host key accepted: sftp.example.test:22, ssh-ed25519 SHA256:... (old) replaced by ssh-ed25519 SHA256:... (new)`
  - `SSH host key removed: sftp.example.test:22, ssh-ed25519 SHA256:.... The next connection stores the key the server presents.`

## 7. Messages

A refused connection says (the text of `SshHostKeyRefusedException`, shown by each caller as its connection error, and written to the HL7
service's event log):

> The SSH host key of sftp.example.test:22 has changed: its fingerprint changed. Stored key: ssh-ed25519 SHA256:AAAA... The server now
> presents: ssh-ed25519 SHA256:BBBB... The connection was refused before the user name and password were sent. If the server's key really
> changed, a user with Security Admin can compare it with the fingerprint the server's administrator gives and accept it in Setup, SSH Host
> Keys. Otherwise someone else may be answering for the server.

"its key type changed" instead of "its fingerprint changed" when the type differs. When the store cannot be read or written, the message
names the server and says the connection was refused because its stored host key could not be read (or stored), with the database error.

Where it goes: the Denti-Cal and Mercury windows show it as their connection error, the HL7 service writes it to its event log, an SFTP
image upload or download fails with it, and the SFTP checks of Setup, Data Paths (Save) and of the program's start-up show it in place of
their general "not available" message (`Sftp.FileExists` with `doThrowHostKeyRefusal`). `Sftp.FileExists` otherwise still answers only
whether the file exists.

The Transworld (TSI) paths show it too, never the program link messages, because retyping the program link does not help when the server's
key changed. Each of them tests the connection first, with `Sftp.IsConnectionValid` and `doThrowHostKeyRefusal` (directly, or through
`TsiTransLogs.ValidateClinicSftpDetails` with `out refusalMessage`), which throws the refusal instead of answering "not valid":

- **Manage, A/R Manager** (`ControlManage.butManageAR_Click`): when no clinic's details connect and a connection was refused for its host
  key, the refusal (each message once, for clinics that share the server) replaces "An SFTP connection could not be made ... Would you like
  to edit the Transworld program link now?". A user with Security Admin is asked whether to open Setup, SSH Host Keys; after the window
  closes the connection is tested again. Any other user gets the message only.
- **Sending or updating accounts in the A/R Manager** (`FormArManager`): a clinic skipped because its server was refused is reported with
  the refusal, naming the clinic (each message once, with the clinics it applies to). When that leaves no clinic to send for, nothing is
  sent and the "until the program link is setup" message is not shown.
- **The automated suspend message** (`TsiTransLogs.SuspendGuar`, when a TSI payment plan is made in the Account module): returns "The SFTP
  server of the Transworld program link (for the guarantor's clinic, ...) was refused for its SSH host key, so the suspend message was not
  sent." followed by the refusal, instead of "The Transworld program link is not enabled ... or is not setup properly."

In every case the refusal is also recorded on the row and in the audit trail, where the SSH Host Keys window shows it.

## 8. Middle Tier, services and caching

- `SshHostKeys` follows the Meth pattern: every method runs on the Middle Tier server for a Middle Tier client. The check itself runs on the
  computer that connects (as the SFTP code always did), and calls the store on the caller's thread, before and after the SSH.NET connect,
  never from SSH.NET's own thread.
- The HL7 service connects as its service user (`UserNum` 0).
- The table is not cached: each connection reads its server's row (one indexed query), so a key an administrator accepts or removes counts
  on every workstation from its next connection, with no signal.

## 9. Schema migration and the adoption tools

- Step 2026110221 (`SshHostKeyTableStep`) creates `sshhostkey` with `CreateTableIfMissing`; on a database that already has it the step
  changes nothing. No row is written.
- `SchemaVerifier.DocumentedExceptions` allows the table as an extra table (through `ForkTables`), so a migrated database still matches Open
  Dental 26.1.70, and `ForkTables.VerifyDatabase` checks its definition exactly.
- The adoption tools: `tools/SyntheticData/allowed-differences.adoption.json` has a `TableAdded` rule for it, and adopt's row check lists it
  under "Fork tables:" and requires it empty (an adopted database has no stored key; the first connection stores it).
- `db/init/new-database.sql` is regenerated by the integration, not by this package.

## 10. Tests

`tests/SshHostKeyTests` (MSTest, x86), scratch databases `test_fussh_*` on the local MariaDB, synthetic keys made with BouncyCastle for each
run, no network:

- The check with a fake store: a first use is trusted and stored only after a successful connect; the same key connects; a changed
  fingerprint and a changed key type are refused, recorded and explained; a connect that fails stores nothing; a store that cannot be read
  refuses; another workstation's key stored first refuses and disconnects; a re-key must present the same key; one RSA key under every
  signature algorithm is one key.
- A real SSH.NET client against an in-process fake SSH server on 127.0.0.1 (version exchange, curve25519-sha256 key exchange, an Ed25519
  host key, nothing after the key exchange): with a stored key that differs, the client refuses the key and never sends `SSH_MSG_NEWKEYS`,
  so it never reaches authentication; with the stored key, or with none, it accepts the key (sends `NEWKEYS`). `Sftp.IsConnectionValid`
  with `doThrowHostKeyRefusal` throws the refusal (server and both fingerprints) and still answers false for any other failure; the TSI
  connection test (`TsiTransLogs.ValidateClinicSftpDetails` with `out refusalMessage`) returns it with the database store.
- The business layer on a scratch database: first use stores the key with its user, time and audit entry; the same key and a second store
  of the same server change nothing; a refused key is recorded once in the audit trail; Accept Changed Key and Remove, with their audit
  entries; both refused without Security Admin; Accept refused when the refused key changed meanwhile.
- The step on an empty scratch database (the table is created exactly as declared) and on one that already has it (nothing changes), the
  levels, the fork table declaration and the adoption rule.
- Wiring: only `OpenPmsDentalStorage` references SSH.NET; every SSH.NET client it creates is attached to the check; every caller passes
  `SshHostKeys.Store`; every caller of `Sftp.IsConnectionValid` asks for the refusal, and the TSI paths show it (section 7); the window and
  the menu entry exist.

`tests/LibraryUpdateTests` (`SftpTests`) keeps its SFTP path tests and uses the new check with an in-memory store.

## 11. Decisions taken here (owner questions)

Taken as the simplest option consistent with the owner's decision; the owner may change them.

1. **The refused key is recorded on the server's row**, so the administrator accepts exactly the key a connection was refused for, and a
   refusal seen only by the HL7 service shows in the window too. There is no "probe the server now" button and no typing of a fingerprint.
2. **One audit entry per refused key**, not per refused connection (the HL7 service retries every minute).
3. **No alert** is raised on a refusal; the caller's error message and the audit trail say it. An alert could follow if the owner wants one.
4. **The first key is stored after the connection signed in**, not as soon as the server presented it, so a server that refuses the
   password does not get its key stored.
5. **Not cached** (section 8).
6. **Servers by name and port, case ignored**; a server's name and its IP address are two servers.
