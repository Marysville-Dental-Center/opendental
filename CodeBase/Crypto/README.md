# Secrets and TamperHash

Stored-secret obfuscation and the `SecurityHash` tamper hashes, built on .NET's own crypto providers. They live in CodeBase, the lowest layer, because `DataConnectionBase` needs them to read connection passwords before any database connection exists.

They replace the proprietary prebuilt `CDT.dll` that Open Dental shipped in `Required dlls/`, and the in-repo `CDT` project that briefly stood in for it. The code was written from how this repository called the library and from public algorithm specifications. Nothing was taken from the original binary. The formats are unchanged, so values already written by the `CDT` project keep decrypting and verifying.

| Class | Members | Summary |
|---|---|---|
| `CodeBase.Secrets` | `Encrypt`, `Decrypt`, `TryEncrypt`, `TryDecrypt`, `EncryptSftp`, `DecryptSftp`, `ConcealClearinghouse`, `RevealClearinghouse`, `IsEncrypted`, `Prefix` | Reversible obfuscation of stored passwords and tokens: AES-256-CBC plus HMAC-SHA256, deterministic, prefix `osd1$`, fixed public key |
| `CodeBase.TamperHash` | `Compute`, `IsCurrentFormat`, `Prefix`, `ReleaseProviders` | Keyed hashes for the tamper-detection columns: HMAC-SHA256, prefix `osh1$`, fixed public key. `OpenDentBusiness.Misc.SecurityHash` decides which rows are hashed and verified. |

`SecretBox`, `SaltedHash` and `HmacSha256` are the internal implementations.

Hashing and AES use the Windows CryptoAPI providers (`SHA256CryptoServiceProvider`, `AesCryptoServiceProvider`), so they also work on machines that enforce the Windows FIPS policy.

The key-material strings in `SecretBox` and `SaltedHash` still mention `CDT`. They are frozen key input, not references: changing a single character would make every stored `osd1$` value undecryptable and every `osh1$` hash invalid.

## Secrets

### Format

```
osd1$ + base64( IV[16] || AES-256-CBC-PKCS7( UTF-8 plaintext ) || tag[32] )
IV     = first 16 bytes of HMAC-SHA256( ivKey, "osd1$" || UTF-8 plaintext )   (synthetic IV)
tag    = HMAC-SHA256( macKey, "osd1$" || IV || ciphertext )                  (encrypt-then-MAC)
master = SHA-256( UTF-8 SecretBox.KeyMaterial )
encKey = HMAC-SHA256( master, "osd1 encryption" )
macKey = HMAC-SHA256( master, "osd1 authentication" )
ivKey  = HMAC-SHA256( master, "osd1 iv" )
```

Encryption is **deterministic**: the IV is derived from the plaintext, so the same secret always encrypts to the same `osd1$` value. Decryption reads the IV from the value and doesn't care how it was chosen. The SFTP and clearinghouse variants are aliases that use the same format, so a value written by one decrypts with any of them.

#### Why deterministic

Existing code compares stored ciphertexts instead of decrypting them, so it relies on equal secrets giving equal ciphertexts. With a random IV, these comparisons would stop matching:

- `FormPayConnectSetup.SynchWithHQ` keeps a clinic on the HQ credentials when its stored `Password` equals HQ's. With a random IV, no clinic would match after the first save. Those clinics would then keep the old password when HQ's changed, and their card payments would fail.
- `FormTransworldSetup` folds a clinic back into HQ when its settings, including the encrypted `SftpPassword`, equal HQ's. It also logs `ManageHighSecurityProgProperties` when a value differs from the one loaded. With a random IV, every save would log a false "SftpPassword ... was altered" entry.
- `Prefs.UpdateString` and the `ProgramProperties` updates would report a change on every save.

The synthetic IV is an HMAC of the plaintext under its own key. Two different secrets get different IVs, unless a truncated 128-bit HMAC collides, so CBC never reuses an IV for different data. The only extra thing an observer learns is **whether two stored secrets are equal** (for example, that two clinics share a password). That's acceptable under the obfuscation-only threat model below, because anyone with this source code can decrypt both values anyway.

Changing the IV derivation wouldn't break decryption, but every unchanged secret would encrypt to a new value. Each would then look altered once, on its next save. `SecretsTests.SyntheticIv_MatchesKnownAnswers` pins the derivation.

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

So `osd1` uses one key compiled into CodeBase and derived from a public constant. That meets both requirements with no setup. The `osd1$` prefix makes rotation possible: a future `osd2$` (for example, with a site key file) would make `Decrypt` accept both formats and re-encrypt values on save.

### Threat model

This is **obfuscation at rest, not strong security.**

- **It protects against** casual disclosure: someone reading a password at a glance in a query result, screenshot, support log, config file or database dump, or finding it by searching for plain text. The MAC also catches corrupted values, and it lets code tell ciphertext from plain text.
- **It doesn't protect against** anyone who has this source code (or the built DLL) and a stored value. They can decrypt it. Open Dental's own scheme had the same weakness, since its key was embedded in its DLL.
- **The real controls** are MySQL accounts and grants, network access to the database server, file-system permissions on config files and backups, disk encryption, and Windows account security. For integrations, prefer revocable, least-privilege credentials or tokens.

### Migrating a database from Open Dental

Open Dental's ciphertexts use its proprietary key, so `Secrets` can't decrypt them. `Decrypt` returns `false` with `""`, and the UI shows a blank field. After migrating, re-enter:

- bridge and program-link passwords, including SFTP (A to Z) and Transworld
- clearinghouse passwords
- reporting-server, read-only-server and archive MySQL passwords
- the eBill password
- PDMP and Oryx credentials
- the QuickBooks Online connection (re-authorize it)
- `MySQLPassHash` in `FreeDentalConfig.xml`: re-enter the password in the Choose Database window

Callers that use `TryDecrypt` get the stored value back unchanged. A value Open Dental stored as plain text therefore keeps working. A value Open Dental encrypted is passed through as-is, and authentication fails until the password is re-entered.

The `DatabaseIntegritiesWhiteList` preference also holds Open Dental ciphertext. Open Dental HQ sent the plugin and warning list, and Open Dental saved it there encrypted. Nothing needs re-entering: `DatabaseIntegrities.RefreshCacheFromPref` reads it with `Decrypt` rather than `TryDecrypt`, so an undecryptable value is ignored and the list stays empty. That's the same state as when HQ is unreachable: plugins load and no integrity warnings show. `TryDecrypt` would have passed the ciphertext to the XML parser, which throws, and that exception would surface each time a module opened with a patient selected. The preference row is left as it is; clearing its value is optional.

## SecurityHash

### Format

```
osh1$ + base64( HMAC-SHA256( key, UTF-8 text ) )
master     = SHA-256( UTF-8 SaltedHash.KeyMaterial )
rowKey     = HMAC-SHA256( master, "osh1 row" )       appointment, claim, claimproc, payplan, paysplit
patientKey = HMAC-SHA256( master, "osh1 patient" )   patient (isPatientTable: true)
```

A hash is 49 characters, which fits the `varchar(255)` `SecurityHash` columns. `TamperHash.Compute` is thread safe. `TamperHash.ReleaseProviders` releases the cached providers, and the next call recreates them.

### Threat model

This is tamper detection only. It flags a row that someone changed directly in the database, for example a payment amount edited in a query tool, without recomputing the hash. Anyone who reads this file can compute valid hashes.

A secret per-database key would add little. Anyone able to write these tables could also read a key stored in the same database. Open Dental's hash relied on a secret inside its DLL. Treat the warning triangle as a hint. The security log and database permissions are the real controls.

### What gets verified

`Patients.IsPatientHashValid` checks every patient. For the other tables, `IsAppointmentHashValid`, `IsClaimHashValid` and the others check only rows dated on or after `SecurityHash.GetHashingDate()`, which is one month ago or 2025-03-27, whichever is later. For appointments, that includes all future ones.

The Open Dental 25.1 and 25.2 columns use the row key too, with `|` between fields and dates without their time (backlog package A-HASH):

| Table | Hashed fields | Checked rows |
|---|---|---|
| `insverify` | `UserNum`, `DefNum`, `DateLastAssigned` | edited (`SecDateTEdit`) on or after the hashing date |
| `insverifyhist` | the same fields, copied from `insverify`, so the copied hash stays valid | the same |
| `inssub` | `PlanNum`, `Subscriber`, `SubscriberID`, `DateTerm` | the same |

When the program saves one of these rows, it computes a new hash only if the stored one is blank, still valid, or Open Dental's (`SecurityHash.DoRehashOnSave`). A row changed outside the program keeps its old hash, even if a user then edits it, so the warning stays until an administrator runs the rehash tool. The Insurance Verification List shows the triangle for the selected row.

### First run against a database hashed by Open Dental

The hashes Open Dental wrote can't be recomputed here. Comparing them with `osh1$` hashes would flag every recent row and every patient as tampered. Worse, the "only rehash rows that are already valid" logic would then never fix them. To prevent this:

- `SecurityHash.IsHashValid(stored, computed)` is true when the hashes match, **or** when the stored hash is a legacy hash: non-empty and without the `osh1$` prefix. All six `Is*HashValid` methods use it. A blank hash still counts as unhashed, as before.
- A legacy row counts as valid, so it gets an `osh1$` hash the next time it's saved.
- `SecurityHash.UpdateHashing` now also rehashes patients whose hash is legacy, not just blank ones.

### Admin rehash step

After moving a database over from Open Dental, run the rehash once so every verified row carries an `osh1$` hash:

- `SecurityHash.UpdateHashing()` runs automatically, in background threads, on the workstation that performs the next program version update (`PrefL`).
- `SecurityHash.RehashNow()` runs the same work on demand, on the calling thread, even if it already ran in this process. **Database Maintenance, Tools tab, Security Hashes** runs it after a confirmation prompt. The tool needs the Security Admin permission and a direct database connection (not the middle tier), and it makes a security log entry.

It rehashes every patient with a blank or legacy hash, and every appointment, claim, claim procedure, payment split, payment plan, insurance subscriber, insurance verification and verification history row in the verified date range. Like Open Dental's own update-time rehash, it also rewrites the hash of a recent row that was tampered with. Run it right after migrating, before anyone edits data directly in the database.

Until the rehash runs, legacy hashes are accepted without verification. Rows written by this program are verified normally.

To rehash **every** patient, including patients changed outside the program, run the Database Maintenance method `SecurityHashRehashPatientTable` (Open Dental 25.1). Check counts the patients whose hash does not match (never hashed, Open Dental's, changed outside the program). Fix needs Security Admin, rewrites the hashes in batches of 1,000 (`SecurityHash.RehashPatientTable`, keeping `DateTStamp`), and makes one security log entry with the count. It is a one-off method, so after a fix it moves to the Old tab and a routine Fix All does not run it again.

## What the CDT library also did, and what replaced it

- **ADA content.** The American Dental Association's CDT code text and claim-form artwork are copyrighted and licensed by the ADA, so they aren't included. Procedure codes, their descriptions and treatment areas come straight from the `procedurecode` table, which is loaded into the database directly. Procedure Code Tools no longer offers the D-code and treatment-area options, and the startup reset of ADA descriptions is gone. Claim-form items that name an ADA or DC-217 background image (`ClaimFormItems.IsUnshippedBackground`) draw nothing; print those claims on pre-printed forms.
- **Registration keys.** Removed with their callers. There is no license gate.

## Tests

`tests/CryptoTests` holds MSTest v1 tests (`SecretsTests`, `TamperHashTests`) that need no database. They cover round trips, pass-through behavior, tamper rejection, a pinned `osd1$` value cross-checked with Python's `hmac` and with .NET's `AesManaged`, and `osh1$` known answers computed independently with Python.
