# CDT

Open source replacement for the proprietary prebuilt `CDT.dll` that used to ship in `Required dlls/`. It keeps the assembly name (`CDT`), the namespace (`CDT`), the static class `Class1`, and every member signature this repository calls, so no call site changed.

It was written from how this repository calls the library and from public algorithm specifications. Nothing was taken from the original binary.

| Area | Members | Summary |
|---|---|---|
| Secrets | `Encrypt`, `Decrypt`, `TryEncrypt`, `TryDecrypt`, `EncryptSftp`, `DecryptSftp`, `ConcealClearinghouse`, `RevealClearinghouse`, `IsEncrypted` | Reversible obfuscation of stored passwords and tokens: AES-256-CBC plus HMAC-SHA256, prefix `osd1$`, fixed public key |
| SecurityHash | `CreateSaltedHash`, `DisposeHashAlgorithm`, `IsCurrentSaltedHash`, `SaltedHashPrefix` | Keyed hashes for the tamper-detection columns: HMAC-SHA256, prefix `osh1$`, fixed public key |
| ADA content | `GetADAcodes`, `GetProcedureCodeSettings`, `GetProcedureCodeTreatAreas`, `GetMandibularCodes`, `GetMaxillaryCodes`, `GetADA2006`, `GetADA2012`, `GetADA2012_J430D`, `GetADA2018_J432`, `GetADA2019_J430`, `GetADA2024_J430`, `GetDC217` | Not shipped. The text getters return `""` and the claim-form images are blank. |
| Registration keys | `GenerateRandKey`, `ValidateKey` | No license gates. Key generation returns `""` and validation always returns true. |

Hashing and AES use the Windows CryptoAPI providers (`SHA256CryptoServiceProvider`, `AesCryptoServiceProvider`), so they also work on machines that enforce the Windows FIPS policy.

## Secrets

### Format

```
osd1$ + base64( IV[16] || AES-256-CBC-PKCS7( UTF-8 plaintext ) || tag[32] )
tag    = HMAC-SHA256( macKey, "osd1$" || IV || ciphertext )      (encrypt-then-MAC)
master = SHA-256( UTF-8 SecretBox.KeyMaterial )
encKey = HMAC-SHA256( master, "osd1 encryption" )
macKey = HMAC-SHA256( master, "osd1 authentication" )
```

Every call uses a fresh random IV, so the same password encrypts differently each time. The SFTP and clearinghouse variants are aliases that use the same format, so a value written by one decrypts with any of them.

| Call | Result |
|---|---|
| `Encrypt("")` or `Encrypt(null)` | `true`, `""`. A blank value stays blank. |
| `Decrypt("")` or `Decrypt(null)` | `true`, `""` |
| `Decrypt` of anything that isn't an intact `osd1$` value (plain text, Open Dental's unprefixed base64 or `odv2e$` values, corrupted data) | `false`, `""` |
| `TryDecrypt` of anything that isn't ciphertext | The input, unchanged |
| `TryEncrypt("")` | `""` |
| `EncryptSftp("")` | `""` |

### Key strategy: one fixed key, public by design

Secrets live in two kinds of places, and both have to decrypt:

1. **The database**: program properties (bridge passwords, SFTP passwords, QuickBooks Online tokens, PDMP credentials), clearinghouse passwords, and preferences such as `ReportingServerMySqlPassHash`, `ReadOnlyServerMySqlPassHash`, `ArchivePassHash` and the eBill password. Every workstation, the middle tier server and the services must be able to decrypt them.
2. **Local files and command lines**: `MySQLPassHash` in `FreeDentalConfig.xml`, the service configs (`OpenDentalServiceConfig.xml`, `OpenDentalWebConfig.xml`, the eConnector config), the CEMT connection store, and the obfuscated passwords that CEMT and the updater pass to `OpenDental.exe` on the command line. These have to decrypt **before any database connection exists**.

We considered and rejected these options:

| Option | Why not |
|---|---|
| A per-install key stored in the database | Fails case 2: the key is needed to connect to the database. |
| A per-machine key (DPAPI) | Fails case 1, because another workstation can't decrypt. It also breaks config files that are copied between machines or read by services running under another account. |
| A key file installed on every workstation and server | Works, but adds a deployment step that locks everyone out if it's missing or differs. It can be added later as a new format (`osd2$`). |

So `osd1` uses one key compiled into the library and derived from a public constant. That meets both requirements with no setup. The `osd1$` prefix makes rotation possible: a future `osd2$` (for example, with a site key file) would make `Decrypt` accept both formats and re-encrypt values on save.

### Threat model

This is **obfuscation at rest, not strong security.**

- **It protects against** casual disclosure: someone reading a password at a glance in a query result, screenshot, support log, config file or database dump, or finding it by searching for plain text. The MAC also catches corrupted values, and it lets code tell ciphertext from plain text.
- **It doesn't protect against** anyone who has this source code (or the built DLL) and a stored value. They can decrypt it. Open Dental's own scheme had the same weakness, since its key was embedded in its DLL.
- **The real controls** are MySQL accounts and grants, network access to the database server, file-system permissions on config files and backups, disk encryption, and Windows account security. For integrations, prefer revocable, least-privilege credentials or tokens.

### Migrating a database from Open Dental

Open Dental's ciphertexts use its proprietary key, so this library can't decrypt them. `Decrypt` returns `false` with `""`, and the UI shows a blank field. After migrating, re-enter:

- bridge and program-link passwords, including SFTP (A to Z) and Transworld
- clearinghouse passwords
- reporting-server, read-only-server and archive MySQL passwords
- the eBill password
- PDMP and Oryx credentials
- the QuickBooks Online connection (re-authorize it)
- `MySQLPassHash` in `FreeDentalConfig.xml`: re-enter the password in the Choose Database window

Callers that use `TryDecrypt` get the stored value back unchanged. A value Open Dental stored as plain text therefore keeps working. A value Open Dental encrypted is passed through as-is, and authentication fails until the password is re-entered.

## SecurityHash

### Format

```
osh1$ + base64( HMAC-SHA256( key, UTF-8 text ) )
master     = SHA-256( UTF-8 SaltedHash.KeyMaterial )
rowKey     = HMAC-SHA256( master, "osh1 row" )       appointment, claim, claimproc, payplan, paysplit
patientKey = HMAC-SHA256( master, "osh1 patient" )   patient (isPatientTable: true)
```

A hash is 49 characters, which fits the `varchar(255)` `SecurityHash` columns. `CreateSaltedHash` is thread safe. `DisposeHashAlgorithm` releases the cached providers, and the next call recreates them.

### Threat model

This is tamper detection only. It flags a row that someone changed directly in the database, for example a payment amount edited in a query tool, without recomputing the hash. Anyone who reads this file can compute valid hashes.

A secret per-database key would add little. Anyone able to write these tables could also read a key stored in the same database. Open Dental's hash relied on a secret inside its DLL. Treat the warning triangle as a hint. The security log and database permissions are the real controls.

### What gets verified

`Patients.IsPatientHashValid` checks every patient. For the other tables, `IsAppointmentHashValid`, `IsClaimHashValid` and the others check only rows dated on or after `SecurityHash.GetHashingDate()`, which is one month ago or 2025-03-27, whichever is later. For appointments, that includes all future ones.

### First run against a database hashed by Open Dental

The hashes Open Dental wrote can't be recomputed here. Comparing them with `osh1$` hashes would flag every recent row and every patient as tampered. Worse, the "only rehash rows that are already valid" logic would then never fix them. To prevent this:

- `SecurityHash.IsHashValid(stored, computed)` is true when the hashes match, **or** when the stored hash is a legacy hash: non-empty and without the `osh1$` prefix. All six `Is*HashValid` methods use it. A blank hash still counts as unhashed, as before.
- A legacy row counts as valid, so it gets an `osh1$` hash the next time it's saved.
- `SecurityHash.UpdateHashing` now also rehashes patients whose hash is legacy, not just blank ones.

### Admin rehash step

After moving a database over from Open Dental, run the rehash once so every verified row carries an `osh1$` hash:

- `SecurityHash.UpdateHashing()` runs automatically, in background threads, on the workstation that performs the next program version update (`PrefL`).
- `SecurityHash.RehashNow()` runs the same work on demand, on the calling thread, even if it already ran in this process. No menu item calls it yet.

It rehashes every patient with a blank or legacy hash, and every appointment, claim, claim procedure, payment split and payment plan in the verified date range. Like Open Dental's own update-time rehash, it also rewrites the hash of a recent row that was tampered with. Run it right after migrating, before anyone edits data directly in the database.

Until the rehash runs, legacy hashes are accepted without verification. Rows written by this program are verified normally.

## ADA content

The American Dental Association's CDT code text and claim-form artwork are copyrighted and licensed by the ADA, so they aren't included:

- The code-text getters return `""`. Callers already treat that as an empty list. Procedure codes come from a user import, which needs an ADA license.
- The claim-form background getters return a new, fully transparent 1×1 image on each call. Callers may dispose it. Print claims on pre-printed forms.

## Tests

`tests/CryptoTests` holds MSTest v1 tests that need no database. They cover round trips, pass-through behavior, tamper rejection, a pinned `osd1$` value cross-checked with Python's `hmac` and with .NET's `AesManaged`, and `osh1$` known answers computed independently with Python.
