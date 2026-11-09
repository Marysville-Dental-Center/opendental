# Port note: DIRECT-CERT-FOLLOWUP (the owner's answers to DIRECT-CERT's questions)

Branch `fix/directcert-followup-1`, cut from `main` (81e701df5, with DIRECT-CERT merged). The owner approved all three parts on
2026-11-09: the CAPI key fix for the connection settings tool, the deletion of the empty Customer Updates shell, and the two other
questions of DIRECT-CERT decided as built. No schema change, no new enumeration value stored anywhere, no new third-party file, no
download. Synthetic certificates and throwaway keys only; no network.

## 1. The connection settings tool deletes and names a CAPI key through CAPI

### What DIRECT-CERT found

DIRECT-CERT's third fix round (7cab838be, its tests 78777bf2e, `docs/port-notes/DIRECT-CERT.md`, "Found by the tests, fixed in
7cab838be" and "CAPI and CNG keys"): Windows stores the private key of a `.pfx` that names no CNG provider (OpenSSL writes none, so most
providers' files) in a CAPI key container, and so a `.pfx` that names a CAPI provider. .NET Framework's `GetRSAPrivateKey` opens such a
key **through CNG**: it gives an `RSACng`, not an `RSACryptoServiceProvider`. That `RSACng`'s `Key.Delete` returns without deleting the
CAPI container, and its `Key.UniqueName` is no file of the CNG folder (`Crypto\Keys`). DIRECT-CERT's fix: look for the CAPI key first,
as the certificate's own `PrivateKey` (`X509Certificate2.PrivateKey as RSACryptoServiceProvider`, made from the certificate's key provider
information, so it opens the container in the key set the key really lives in: user or machine), and delete it through CAPI
(`PersistKeyInCsp=false`, then `Clear`, which deletes the container) and name its file through `CspKeyContainerInfo`
(`UniqueKeyContainerName`, under `Crypto\RSA\MachineKeys` for the machine key set).

### What was wrong in `ClientCertificateStore`

`OpenPmsDentalDbSettings/ClientCertificateStore.cs` (the connection settings tool's client certificate, owner decision Q2 and the answer
(b) of 2026-11-02) had the pattern DIRECT-CERT fixed: `DeletePrivateKey` and `GetPrivateKeyFilePath` asked `GetRSAPrivateKey` and
branched on `RSACng` first, `RSACryptoServiceProvider` second. On .NET Framework the second branch was never reached for a CAPI key:

- `RemoveImported` (a client certificate the tool imported and the saved settings no longer use) took the certificate out of
  LocalMachine\My, but `RSACng.Key.Delete` left its CAPI key container in `%ProgramData%\Microsoft\Crypto\RSA\MachineKeys`, on the
  computer, with no certificate left to find it by.
- `ImportPfx` named the key file in the CNG folder, which has no such file, so `GrantUsersReadToPrivateKey` failed and the import warned
  "the users of this computer may not be able to use its private key: The private key file was not found." The program, which runs as
  the signed-in user, then could not use the client certificate until an administrator gave Users read in certlm.msc.

Measured on this PC (Windows 11, .NET Framework 4.8) with throwaway keys of the current user only (no certificate store), before
writing the tests: a certificate whose key is in a CAPI container of the "Microsoft Enhanced RSA and AES Cryptographic Provider" gives an
`RSACng` from `GetRSAPrivateKey` (its CNG provider name is the CAPI provider's); `CngKey.Exists` is true before and after its
`Key.Delete`, and the container's file under `%APPDATA%\Microsoft\Crypto\RSA\<SID>` is still there; no file of its `UniqueName` is in
`Crypto\Keys`. Deleted through the certificate's `PrivateKey` (`PersistKeyInCsp=false`, `Clear`), the file is gone and the container no
longer opens through CAPI. A CNG RSA and a CNG ECDSA key are deleted by `Key.Delete` (file gone, `CngKey.Exists` false). The tests below
pin each of these.

### The fix (`ClientCertificateStore`)

- `GetCapiRsaPrivateKey` (as DIRECT-CERT's: `PrivateKey as RSACryptoServiceProvider`, null for a CNG key, "Invalid provider type
  specified", or an ECDSA key, `NotSupportedException`) comes first in `DeletePrivateKey` (`PersistKeyInCsp=false`, `Clear`) and in
  `GetPrivateKeyFilePath` (`CspKeyContainerInfo.UniqueKeyContainerName`). The CNG branches follow as before; the `RSACryptoServiceProvider`
  branch after `GetRSAPrivateKey` stays for .NET, which gives a CAPI key so.
- `GetPrivateKeyFilePath` names the file **in the key set the key lives in** (`CspKeyContainerInfo.MachineKeyStore`, `CngKey.IsMachineKey`):
  `GetCapiKeyFilePath` gives `%ProgramData%\Microsoft\Crypto\RSA\MachineKeys\<unique name>` for the machine key set (the tool's import,
  `MachineKeySet`) and `%APPDATA%\Microsoft\Crypto\RSA\<the user's SID>\<unique name>` for the user's; `GetCngKeyFilePath` gives
  `Crypto\Keys\<unique name>` of `%ProgramData%` or `%APPDATA%`. Before, a CAPI key was always named in `RSA\MachineKeys` and a CNG key in
  `%ProgramData%`'s `Keys`; for the tool's own imports (always the machine key set) the result is the same, so nothing changes for them
  but the CAPI case.
- The key objects of `GetRSAPrivateKey` and `GetECDsaPrivateKey`, and the `CngKey` read for a file name, are disposed, as DIRECT-CERT's are.
- `RemoveImported(thumbprint)` is the tool's (LocalMachine\My, as before; `DbSettingsRunner` calls it so). An internal overload takes the
  store's location and name, so the tests use a store of the current user with a name of its own instead of CurrentUser\My (the old test
  used CurrentUser\My, a real store of this PC).

### Every other place that deletes or names a private key

Searched the whole repository (`PersistKeyInCsp`, `CspKeyContainerInfo`, `Key.Delete`, `CngKey`, `UniqueKeyContainerName`,
`KeyContainerName`, `PersistKeySet`, `MachineKeySet`, `UserKeySet`, `DeleteKey`, in `.cs` and `.ps1` files):

| Place | What it does | Verdict |
|---|---|---|
| `OpenPmsDentalDbSettings/ClientCertificateStore.cs` `DeletePrivateKey`, `GetPrivateKeyFilePath` | delete and name the client certificate's key | **Fixed** (above). |
| `OpenPmsDentalBusiness/Email/DirectCertificateImport.cs` `TryDeletePrivateKey`, `GetKeyContainerName` | delete and name the Direct certificate's key | Safe: DIRECT-CERT's own fix, the CAPI key first through the certificate's `PrivateKey`, so in its key set; `GetCapiKeyContainerName` and `GetCngKeyContainerName` include user or machine. |
| `DirectCertificateImport.GetMachinePrivateKeyFilePath` | names the key file for the users' read access | Safe: the CAPI key first; it names only the machine folders, and it is called only for the Local Computer store (`EmailMessages.CertStoreLocation==StoreLocation.LocalMachine`), whose import uses `MachineKeySet` (the same condition sets the flags). |
| `tests/LibraryUpdateTests/DirectCertificateImportTests.cs` `KeyLocation` | the tests' check that a key still exists | Named a CAPI key through CAPI already; it opened it without the key set (always the user's). **Changed:** it records the key set (`MachineKeyStore`, `IsMachineKey`) and opens the key in it. The tests' keys are all the user's, so they behave as before. |
| `tests/DbConnectionSecurityTests/OwnerAnswersTests.cs` `GetKeyName`, `KeyExists` | the old test's check | Named a CAPI key through `GetRSAPrivateKey` (the CNG view), opened a CAPI container without the key set, and used CurrentUser\My. **Replaced** by `ClientCertificateTestStore` and `TestKey` (below). |
| `OpenPmsDentalBusiness/WebBridges/PDMP/PdmpCertificate.cs` `Load` (`UserKeySet`), `CodeBase/Utilities/VendorDirect/VendorHttp.cs` `LoadClientCertificate` (`EphemeralKeySet`, or `UserKeySet`) | load a client certificate for one TLS call | Safe, not the same pattern: no `PersistKeySet`, so Windows' key is temporary and .NET Framework deletes it when the certificate is disposed (the callers dispose it); nothing is deleted or named by hand. |
| `tests/DbConnectionSecurityTests/DevDbTlsScriptTests.cs` `AssertKeyMatches` | `CngKey.Import` of a PKCS#8 file | Safe: an ephemeral key in memory; nothing is stored. |
| `ImagingCapture/UI/FrmCaptureSession.xaml.cs`, `WpfControlsOD/.../CtrlEFormFill.xaml.cs` `Key.Delete` | the keyboard's Delete key | Not a private key. |
| `third-party/PDFsharp` tests | PDFsharp's own signing tests | Not this program's code (a submodule); not touched. |
| `.ps1` scripts | | None deletes or names a private key. |

## 2. The empty Customer Updates shell is removed

Every member of Open Dental's "Customer Updates" web service had been deleted with its last caller, the e-mail certificate registration
last (DIRECT-CERT). What was left, `OpenPmsDentalBusiness/WebServices/CustomerUpdatesProxy.cs` (35 lines: the empty `ICustomerUpdates`,
the empty `CustomerUpdatesNoHq` and `CustomerUpdatesProxy` with `MockCustomerUpdates` and `GetWebServiceInstance`), had no caller in the
program. The owner deleted it (a deliberate deletion, so the tests that pinned the shell go with it).

Every reference, found by searching the whole repository (code, project files, tools, docs):

- **Deleted:** the file. `OpenPmsDentalBusiness.csproj` compiles its folder by the SDK's default glob and lists no item for it, so no
  project file changes. `HqNotAvailableException` (in `WebServiceMainHQNoHq.cs`) stays: `WebServiceMainHQNoHq` throws it.
- **Tests that pinned the shell, changed to check it is gone:**
  - `NoHqTests/NoHqTests.cs`: `CustomerUpdates_FactoryReturnsNoHqAndEveryMethodThrows` (the factory gave `CustomerUpdatesNoHq`, every
    method threw) and `CustomerUpdates_VendorMembersAreRemoved` (eight member names) become `CustomerUpdates_TheEmptyShellIsRemoved`: the
    three types do not resolve (`TypeLookup.AssertRemoved`, which first resolves `WebServiceMainHQProxy` the same way), the file is gone,
    and no `.cs` file of `OpenPmsDentalBusiness`, `OpenPmsDental`, `WpfControlsOD`, `CodeBase`, `OpenPmsDentalServer` or `UnitTests` names
    them (each folder must exist). The `SetUp` no longer resets `MockCustomerUpdates`.
  - `NoHqTests/HqToolsRemovedTests.cs` `HqTools_HostedServiceMembersOfTheHqWebServiceAreRemoved`: the three types do not resolve, instead
    of six members of the interface and `SendAndReceiveUpdateRequestXml` of the proxy.
  - `tests/VendorDirectTests/BridgesRemoval/BridgesDllRemovedTests.cs` `HqProvisioningLeftovers_AreGone`: `ICustomerUpdates` does not
    resolve, instead of having no `RequestCustomerID`.
- **Kept, still passing:** the source checks that a file does not name `CustomerUpdatesProxy` or `CustomerUpdates`
  (`DirectCertificateImportTests` for the certificate window, `CanadaCleanupTests`, `ErxSourceTests`, `OIDInternalsTests`,
  `QboLayoutTests`): they check other files, and stay true. No `LibraryUpdateTests` pin named the types.
- **Not changed:** `tools/project-baseline/items.txt` still has `OpenPmsDentalBusiness|Compile|WebServices\CustomerUpdatesProxy.cs|`. The
  record is taken by `tools/Get-ProjectBaseline.ps1` after a full build, and the lead re-records it after the merges; this is the one
  item this package removes on purpose. The design notes of `docs/specs/vendor-direct/` describe the shell as it was when they were
  written and are left as they are; `WebServiceCustomerUpdates` in comments, `DocumentationBuilder` and the database conversions is Open
  Dental's server, not the shell.

## 3. The owner's two other answers

Recorded in `docs/port-notes/DIRECT-CERT.md` ("The owner's answers (2026-11-09, DIRECT-CERT-FOLLOWUP)"), its O-13 entry in
`docs/specs/net10-plan.md` section 12, and `docs/owner-questions.md` ("Direct certificates follow-up"):

- (a) `BUILTIN\Users` keep read access to the Direct private key, as built.
- (b) A replaced certificate's private key is deleted only if the certificate window imported it, as built.

No code changed for these.

## Also changed

- `docs/specs/db-connection-security.md` section 4.3: a CAPI key is deleted and named through CAPI.
- `docs/release-notes/next.md` ("A replaced client certificate is removed"): for every kind of key, and the users' read access to a CAPI
  key's file. Database connection security is new in the next version, so this is no fix of a released behaviour.
- `docs/specs/net10-plan.md`: the spot check that names `CustomerUpdatesProxy` says it was deleted.

## Tests

`tests/DbConnectionSecurityTests` (SDK-style, every `.cs` file of its folder compiled; no project file change):

- `ClientCertificateTestStore.cs` (helper): a store of the current user with a name of its own (`OpenPmsDbSecTest_<guid>`), made on the
  first add and deleted on `Dispose` (`CertUnregisterSystemStore`), and throwaway keys of the current user's key set, each imported from a
  `.pfx` made in memory as the tool imports one (`PersistKeySet`, never `Exportable`; `UserKeySet` instead of `MachineKeySet`, which needs
  administrator rights): **a CAPI key** (made in a scratch CAPI container of the "Microsoft Enhanced RSA and AES Cryptographic Provider",
  which is deleted before the import, so the `.pfx` names a CAPI provider and Windows stores its key in a CAPI container), **a CNG RSA
  key** and **a CNG ECDSA key** (ephemeral CNG keys, so the `.pfx` names the CNG provider). `TestKey` finds each key by itself, apart from
  the code under test (a CAPI key through CAPI), opens it through its own key store (CAPI or CNG) in its key set, and names its file;
  `Dispose` deletes every key still there. Nothing opens LocalMachine\My, CurrentUser\My or any other real store, and no real certificate
  or key is touched. The `.pfx` password is made at run time and kept in memory.
- `ClientCertificateKeyTests.RemoveImported_DeletesTheKeyOfEachKind_AndNothingElse`: for each of the three kinds, two certificates in the
  store; `RemoveImported` of one gives no warning, the store holds only the other, the removed one's key no longer opens (through CAPI
  for the CAPI key, CNG for the others) and its file is gone; the other's key and file stay and it still signs; a certificate that is not
  there and a blank thumbprint give nothing.
- `ClientCertificateKeyTests.CapiKey_IsNamedAndDeletedThroughCapi_NotThroughTheCngViewOfIt`: DIRECT-CERT's finding for the tool's key, on
  .NET Framework: `GetRSAPrivateKey` gives the CAPI key as an `RSACng` whose `UniqueName` is no file of the CNG folder, and whose
  `Key.Delete` (the tool's old way, done once by hand) leaves the container and its file; `GetPrivateKeyFilePath` names the CAPI file, and
  `RemoveImported` deletes the container and the file.
- `ClientCertificateKeyTests.GetPrivateKeyFilePath_NamesTheKeyFileInTheKeySetOfTheKey`: the machine folders (`%ProgramData%`'s
  `RSA\MachineKeys` and `Keys`) and the user's (`%APPDATA%`'s `RSA\<SID>` and `Keys`); no name, no file; for each kind the path the test
  found by itself, which exists; a certificate without its key gives none.
- `OwnerAnswersTests.RemoveImported_TakesTheCertificateAndItsPrivateKeyOutOfTheStore`: as before (the certificate of
  `TestHelpers.NewCertificate`, a CNG key after the import), in the test store instead of CurrentUser\My.

After each run no key file of the tests is left under `%APPDATA%\Microsoft\Crypto\Keys` or `RSA\<SID>`, and no `OpenPmsDbSecTest_` store
is left (checked by hand after the runs). The experiment before the tests left one CNG key behind when its script stopped; it was found by
its file (created by the script seconds before, RSA 2048, opened by the name in it) and deleted.

Targeted runs (`run-tests.ps1 -Mode Targeted`), after the full lane build (0 errors):

| Assembly | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| NoHqTests | 57 | 57 | 0 | 0 |
| VendorDirectTests | 922 | 922 | 0 | 0 |
| LibraryUpdateTests | 126 | 126 | 0 | 0 |
| DbConnectionSecurityTests | 153 | 143 | 0 | 10 |

The ten skipped DbConnectionSecurityTests are the DevDbTls server tests, inconclusive until the local MariaDB runs with TLS (DB-TLS), as
before this package.

Not tested here: the machine key set and LocalMachine\My, which need administrator rights (the launch checks below).

## Launch checks for the lead

On a test computer, with a test database, as a Windows administrator, and with generated certificates only. A client certificate of a
CAPI key (in `Cert:\CurrentUser\My` only to export it):

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=synthetic-workstations" -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" `
  -KeySpec KeyExchange -KeyUsage DigitalSignature,KeyEncipherment -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") `
  -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(30)
$password = Read-Host -AsSecureString -Prompt "A throwaway password"
Export-PfxCertificate -Cert $cert -FilePath .\client-capi.pfx -Password $password
Remove-Item ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -DeleteKey
```

Without `-Provider` and `-KeySpec` the same command makes a CNG key: export that one as `client-cng.pfx` (the second check). These are
self-signed, so the database server does not accept them; the checks are about the key only, and the connection settings are put back
afterwards.

1. **A CAPI import.** With the connection settings tool, import `client-capi.pfx` (window, or `/pfx=` with `PfxPassword` in a
   `/secretsfile`): no warning about the users' access. In certlm.msc, Personal > Certificates holds it; All Tasks > Manage Private Keys
   shows Users with Read; its key file is in `%ProgramData%\Microsoft\Crypto\RSA\MachineKeys` (the file named by
   `certutil -store My <thumbprint>`'s "Unique container name").
2. **A CNG import** (DCFU2). Import `client-cng.pfx` the same way: the result has no warning that "the users of this computer may not be
   able to use its private key". In certlm.msc, All Tasks > Manage Private Keys on it shows Users with Read, and so does the Security tab
   of its key file in `%ProgramData%\Microsoft\Crypto\Keys` (the file named by `certutil -store My <thumbprint>`'s "Unique container
   name"; `certutil` shows the provider as "Microsoft Software Key Storage Provider").
3. Save the settings with another certificate (the other generated `.pfx`) or with the password: the tool removes the first certificate,
   and its key file is gone from `RSA\MachineKeys`. The same with the CNG one replaced in turn: its file is gone from
   `%ProgramData%\Microsoft\Crypto\Keys`.
4. **A `.pfx` with its CA certificate's key** (DCFU2). Make one, as above, in `Cert:\CurrentUser\My` only to export it (CNG keys):

   ```powershell
   $ca = New-SelfSignedCertificate -Type Custom -Subject "CN=synthetic-office-ca" -KeyUsage CertSign,CRLSign `
     -TextExtension @("2.5.29.19={text}CA=true") -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(60)
   $client = New-SelfSignedCertificate -Type Custom -Subject "CN=synthetic-workstations" -Signer $ca -KeyUsage DigitalSignature,KeyEncipherment `
     -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2") -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(30)
   $both = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
   [void]$both.Add($client); [void]$both.Add($ca)
   $plain = Read-Host -Prompt "A throwaway password"   # Export takes the password as plain text
   [IO.File]::WriteAllBytes("$PWD\client-with-ca.pfx", $both.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $plain))
   Remove-Item ("Cert:\CurrentUser\My\" + $client.Thumbprint) -DeleteKey; Remove-Item ("Cert:\CurrentUser\My\" + $ca.Thumbprint) -DeleteKey
   ```

   List the files of `%ProgramData%\Microsoft\Crypto\Keys`, import `client-with-ca.pfx` with the tool, and list them again: one new file
   only, the client certificate's (its "Unique container name"); LocalMachine\My holds the client certificate and not the CA's.
5. Put the connection settings back, remove what is left with `Remove-Item Cert:\LocalMachine\My\<thumbprint> -DeleteKey`, delete the
   `.pfx` files and forget the passwords.

The refused import and a key that cannot be deleted (DCFU2) are checked by the tests only: Windows gives no simple way to make
LocalMachine\My refuse a certificate or a key refuse its deletion.

## For the .NET 10 switch package

`OpenPmsDentalDbSettings` is a .NET Framework 4.8 project today; when it moves:

- **The CAPI key.** `GetCapiRsaPrivateKey` reads `X509Certificate2.PrivateKey`, obsolete on .NET (SYSLIB0028). .NET's `GetRSAPrivateKey`
  is said to give an `RSACryptoServiceProvider` for a CAPI key, which the existing branches of `TryDeletePrivateKey` (`DeletePrivateKey`
  before DCFU2), `GetKeyContainerName` and `GetPrivateKeyFilePath` handle; verify it with `ClientCertificateKeyTests` and
  `ClientCertificateImportTests` (the CAPI cases), then drop `GetCapiRsaPrivateKey`, as DIRECT-CERT's note says for its own copy.
  `CapiKey_IsNamedAndDeletedThroughCapi_NotThroughTheCngViewOfIt` shows the CNG view only when `GetRSAPrivateKey` gives an `RSACng`, so
  on .NET it checks the CAPI deletion alone.
- **CNG keys.** On .NET, `RSACng.Key` and `ECDsaCng.Key` return a new `CngKey` on each call; `GetPrivateKeyFilePath` disposes it and
  `Key.Delete()` disposes the one it deletes.
- **Loading.** `X509Certificate2Collection.Import(path,password,flags)` (`ImportCollection`) is obsolete from .NET 9 (SYSLIB0057):
  `X509CertificateLoader.LoadPkcs12CollectionFromFile`, with the same flags, never `Exportable`. The test helper's
  `new X509Certificate2(byte[],password,flags)` moves to `X509CertificateLoader.LoadPkcs12` the same way.
- **Key file rights.** `File.GetAccessControl` and `File.SetAccessControl` (`GrantUsersReadToPrivateKey`) are `FileInfo` extension methods
  of `FileSystemAclExtensions` on .NET, as DIRECT-CERT's note says.

## Noticed, not changed (for the lead)

- `ImportPfx` reads the `.pfx` with `PersistKeySet`, which writes the key of every certificate of the file that has one, and adds only
  the client certificate: the keys of the file's other certificates stay in the machine key set, and when the store refuses the client
  certificate its own key stays too. DIRECT-CERT's import deletes such keys ("What an import stores and takes back"); the tool does not.
  Not part of the three approved parts.
- `RemoveImported` takes the certificate out of the store before it deletes the key. When the key cannot be deleted, the warning says the
  certificate "could not be removed from the computer's certificate store" and to remove it in certlm.msc, but the certificate is already
  gone and only its key is left, which certlm.msc can no longer reach.

(Both done in the follow-up round, below.)

## Follow-up round (branch `fix/dcfu2-1`, 2026-11-09)

Cut from `fix/directcert-followup-1` (b1f47d940). The lead decided the two questions above (the same behaviour as DIRECT-CERT's import,
`OpenPmsDentalBusiness/Email/DirectCertificateImport.cs`) and passed on the reviewer's smaller points. The plan is edbc07c2a, the code
fbfcc27f7, the tests 5c895b8ea (with one more change of the code, item 1), these documents the commit after them. No schema change, no
new enumeration value stored anywhere, no new third-party file, no download.

### 1. `ImportPfx` deletes the keys it stored and does not use

`X509Certificate2Collection.Import(..., MachineKeySet | PersistKeySet)` stores the key of every certificate of the `.pfx` that has one,
each in a key container of its own (one container for a key that two certificates of the file share). DirectCertificateImport's way
("What an import stores and takes back", `PrivateImportRun`, `DeleteKeysNotInStore`, `DeleteKeysNotInUse`, `GetKeyContainerName`), now
the tool's too (`ClientCertificateStore`, a copy: `OpenPmsDentalDbSettings` references neither `OpenPmsDentalBusiness` nor anything that
has it):

- The keys this import stored are the file's certificates with a key, right after the read; only those are ever deleted, never a key
  that was on the computer before.
- The store is opened before the keys are stored, so a store that cannot be opened (no administrator rights) leaves nothing behind.
- **After the client certificate is in the store**, the keys of the file's other certificates (its CA's, for example) are deleted, but
  none that the client certificate or a certificate of the store uses (compared by key container: kind, provider, user or machine, unique
  name; each container once). A key that cannot be deleted gives the warning "The client certificate was imported, but a private key of
  another certificate of the file, which the tool stored and does not use, could not be deleted (subject, thumbprint): why. The key is
  left on this computer in <file>: delete that file as an administrator."
- **When anything stops the import before the certificate is in the store** (the store refuses it, the file's check refuses it, anything
  else), every key it stored is deleted, the client certificate's too, with the same exception for a key a certificate of the store uses.
  The error keeps its text and exit code; a key that cannot be deleted adds "A private key the tool stored could not be deleted again
  (subject, thumbprint): why. The key is left on this computer in <file>: delete that file as an administrator."
- **A `PersistKeySet` read that fails** gives no certificate, so no key is known; as DirectCertificateImport says, the error adds "Nothing
  was imported. Windows may have left a private key of the file behind on this computer, which the tool cannot find to delete." (Not
  for the check's `EphemeralKeySet` read, which stores nothing.)
- The store is read anew for the comparison (a new read-only handle), not through the import's open handle, which may keep the
  certificates it read when it was opened (Windows caches a system store per handle and resynchronises it on request,
  `CertControlStore`): a certificate another program put into the store meanwhile counts too. Added while writing the test that gives a
  certificate of the store the key the import just stored; the test was not run without it, so whether the open handle would have
  missed that certificate is not measured. DirectCertificateImport reads its open handle; nothing there needs it (nothing else adds to
  its store during an import).
- The users' read access to the key (`GrantUsersReadToPrivateKey`) is given for LocalMachine only, as DirectCertificateImport gives it;
  the tests' store of the current user imports into the user's key set (`UserKeySet`) and gives none (the key is the user's own).

The certificate the tool takes is still the file's first with a private key (`GetClientCertificate`, unchanged); see Noticed below.

### 2. `RemoveImported` when only the key cannot be deleted

**Order kept: the certificate first, then its key**, as DirectCertificateImport removes a replaced certificate (`RemoveReplaced`: the
store's `Remove`, then `DeleteKeysNotInStore` once they are all out) and takes an import back (`Undo`: a certificate the store does not
let go of keeps its key, "so that the store's entry still works"). The reasons hold for the tool: deleting the key first and then
failing to remove the certificate would leave in LocalMachine\My a certificate that says it has a private key (`HasPrivateKey`, which
`CheckClientCertificate` checks for `/thumbprint=`) whose key is gone; and the key is deleted only when no certificate left in the store
uses the same key container, which can be seen only once the certificate is out (the same `DeleteKeysNotInStore`; new for
`RemoveImported`, which before deleted the key whatever else used it).

**The warning says what happened.** When only the key delete fails: "The client certificate that this tool imported before was removed
from the computer's certificate store (LocalMachine\My), but its private key could not be deleted (subject, thumbprint): why. The key is
left on this computer in <file>: delete that file as an administrator." The file is named before the delete is tried
(`GetPrivateKeyFilePath`); one that cannot be named says so. When the certificate itself cannot be removed, both are still there and the
warning says so, as before, but no longer sends the user to certlm.msc, whose Delete leaves the private key: "Remove it with its private
key as an administrator, in PowerShell: Remove-Item Cert:\LocalMachine\My\<thumbprint> -DeleteKey (certlm.msc's Delete leaves the key)."

`TryDeletePrivateKey` (was `DeletePrivateKey`, which threw) gives why a key was not deleted and never throws, as DirectCertificateImport's.
A test seam (`ActionBeforeKeyDeleteForTests`) makes the delete fail.

### 3 to 7. The reviewer's smaller points

3. `ClientCertificateTestStore`: `CertUnregisterSystemStore`'s result is checked (a store never made gives ERROR_FILE_NOT_FOUND, measured,
   and is no problem), and the store's absence is verified afterwards. A throwaway key is recorded before it is inspected:
   `TestKey.Track` adds the entry to the store's list, keeps a copy of the certificate (the same certificate context, so the same key
   provider information), and only then finds the key; a key it never found is deleted through that copy at the clean-up. `Dispose`
   never throws, so it never hides a test's own failure: what it could not delete (a key, the store) is written to the test's output and
   kept for `ClientCertificateTestStore.AssertCleanedUp`, which `ClientCertificateKeyTests`, `ClientCertificateImportTests` and
   `OwnerAnswersTests` call after every test (`TestCleanup`).
4. `CapiKey_IsNamedAndDeletedThroughCapi_NotThroughTheCngViewOfIt`: the three checks of Windows' own behaviour (the CNG view's file name,
   and that `RSACng.Key.Delete` leaves the CAPI container and its file) say in their failure messages that they pin Windows 11 and .NET
   Framework 4.8, that a later Windows deleting or naming a CAPI key through its CNG view (the key storage provider) makes them fail while
   the tool is still right, and that then they, not the tool, change.
5. `GetCapiRsaPrivateKey` catches every exception but the critical ones (`IsCriticalException`: `OutOfMemoryException`,
   `StackOverflowException`, `ThreadAbortException`), as DirectCertificateImport's copy; so do the new `TryDeletePrivateKey`,
   `GetKeyContainerName` and `TryGetPrivateKeyFilePath`.
6. The launch checks have an explicit CNG import (check 2) and a `.pfx` with its CA's key (check 4), above.
7. `docs/specs/db-connection-security.md` section 4.3 (one line of 238 characters) and `ClientCertificateStore.GetCryptoFolder` (and the
   summary above it) are wrapped to the files' width.

Also changed: `docs/specs/db-connection-security.md` section 4.3 says what the import deletes and what `RemoveImported` reports, and
`docs/release-notes/next.md` ("A replaced client certificate is removed") that no other key of the `.pfx` is left and that a key left is
named with its file.

### Tests

`tests/DbConnectionSecurityTests` (every `.cs` file of its folder compiled; no project file change), each for a CAPI key, a CNG RSA key
and a CNG ECDSA key, in the test store with throwaway keys of the current user:

- `ClientCertificateImportTests` (new), through `ImportPfx`'s internal overload (the test store, the user's key set) and two seams
  (`ActionAfterKeysStoredForTests`, where the test records every key the import stored so that its clean-up deletes it whatever the code
  does, and `ActionBeforeStoreAddForTests`):
  - `ImportPfx_DeletesTheKeyOfTheFilesCaCertificate_AndKeepsTheClientCertificates`: a `.pfx` with a client certificate and the CA
    certificate that issued it, each with its key (`WritePfxWithCa`): the import stores both, takes the client certificate (its
    thumbprint), adds only it, gives no warning; the CA's key no longer opens and its file is gone; the client's stays and signs.
  - `ImportPfx_ARefusedImportDeletesEveryKeyItStored`: (1) the store refuses: the error, exit code 3, both keys and files gone, the store
    empty; (2) anything else stops it after the keys were stored: "The client certificate could not be imported: ..."; (3) the file's
    check refuses it after the keys were stored: its text and exit code 2 stay; (4) the store refuses and the CA's key cannot be deleted
    (a seam): the error names it, its subject and thumbprint and its file, which is still there, and the client's key is gone; (5) a wrong
    password: "Windows may have left a private key...", while the check's read (`LoadPfxForCheck`) does not say so.
  - `ImportPfx_NeverDeletesAKeyThatACertificateOfTheStoreUses`: the test gives a certificate of the store the key the import stored for
    the CA certificate (`AddCertificateUsingKeyOf`, checked by the key's file): after an import, and after a refused one (the client's key
    deleted), that key stays and the certificate signs.
- `ClientCertificateKeyTests`:
  - `RemoveImported_WhenOnlyTheKeyIsNotDeleted_SaysTheCertificateWasRemovedAndWhereItsKeyIs`: the seam makes the key delete fail; the
    warning is the text above with the test's own finding of the key's file, has no "could not be removed", the store is empty, and the key
    and its file are still there (the store's clean-up deletes them).
  - `RemoveImported_KeepsAKeyThatAnotherCertificateOfTheStoreUses`: a second certificate of the store with the same key container: the
    key stays and that certificate signs; when it is removed too, the key and its file go.
  - The three checks of Windows' behaviour in `CapiKey_IsNamedAndDeletedThroughCapi_NotThroughTheCngViewOfIt` (item 4).

After each run, no key file of the tests was left under `%APPDATA%\Microsoft\Crypto\Keys` or `RSA\<SID>` and no `OpenPmsDbSecTest_` store
under `HKCU\Software\Microsoft\SystemCertificates` (the file names of the two folders and the stores listed before the first run and after
each run, and compared; no new name). The test classes' own check (`AssertCleanedUp`) found nothing either.

Targeted runs (`run-tests.ps1 -Mode Targeted`), after the full lane build (0 errors):

| Assembly | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| NoHqTests | 57 | 57 | 0 | 0 |
| LibraryUpdateTests | 126 | 126 | 0 | 0 |
| DbConnectionSecurityTests | 158 | 148 | 0 | 10 |

The ten skipped DbConnectionSecurityTests are the DevDbTls server tests, inconclusive until the local MariaDB runs with TLS, as before.
DbConnectionSecurityTests has five tests more than after the first round (153): the three of `ClientCertificateImportTests` and the two
new ones of `ClientCertificateKeyTests`.

Not tested here, as before: the machine key set and LocalMachine\My, which need administrator rights (the launch checks above).

### Noticed, not changed (for the lead)

- `GetClientCertificate` (the check's and the import's) takes the file's first certificate with a private key. A `.pfx` that lists a CA
  certificate with its key first, and has a client certificate after it, gives the CA certificate, which passes `CheckClientCertificate`
  when it has no Extended Key Usage (most CA certificates have none); the window then shows the CA's subject before the import. The
  tests' files list the client certificate first (and .NET Framework's read keeps that order: the tool took the client certificate).
  Preferring a certificate with the client authentication purpose, or one that is not a CA, would change which certificate the tool
  takes: not part of this round.
- Building this branch through the lane needs `ImportDirectoryPackagesProps=false` in the environment: the worktree sits inside the main
  checkout, whose `Directory.Packages.props` (S2-1, central package versions, merged on `main` after this branch was cut) NuGet otherwise
  finds above the worktree. Merged with `main`, the branch has its own copy and needs nothing.
