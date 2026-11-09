# Port note: DIRECT-CERT, Direct certificates without Open Dental (open point O-13)

Branch `dev/directcert-1`, cut from `main` (a3f3677d6); the fix rounds after the reviews on `dev/directcert-2`. Open point O-13 of
`docs/specs/net10-plan.md` section 12, answered by the owner on 2026-11-08: "Yes, load Direct provider's certificate from a file". No
schema change, no new enumeration value stored anywhere, no new third-party file. Synthetic certificates only; no network, no Open Dental
server, no real certificate or password.

## What was there

The Certificate button of Edit Email Address (`FormEmailAddressEdit.butRegisterCertificate`) opens `FormEmailCertRegister`. In Open Dental
that window sent a verification code to the address and then uploaded the address's public certificate (`.cer`/`.der`) to Open Dental's
own "Customer Updates" service (`RequestEmailVerificationCode`, `PostEmailCertificate`), which published it for other Direct users. Those
servers are gone from this program (`CustomerUpdatesNoHq` throws `HqNotAvailableException`), so S0-3D restored the window as it was and it
always failed. The certificate the program itself uses was never handled by the window: offices imported their private certificate into
the Local Computer store `NHINDPrivate` with the Windows certificate tools (certlm.msc), and the trust anchors into `NHINDAnchors`.

How the program uses the stores (S0-3D, `EmailMessages`): `EmailPrivateResolver` and `GetCertFromPrivateStore` take the certificates of
`NHINDPrivate` that have their private key (`IsPrivateCertWithKey`), and of those the valid ones (`EmailCertificates.IsCertValid`) that name
the address (subject `E=` blank or the address, and the subject alternative name's rfc822 name the address), or, when the address has none,
its domain (subject `E=` blank or the domain, and the subject alternative name's DNS name the domain). They sign outgoing e-mail (signed
sending, O-1), decrypt received Direct messages and sign Direct messages. The trust anchors of `NHINDAnchors` decide which senders are
trusted (`IsSenderTrusted`, the Direct agent's `TrustAnchorResolver`). The three stores are opened through one seam (`CertStoreLocation` and
the three names), which the tests point at stores of the current user.

## What it does now

`FormEmailCertRegister` (the name is kept, so the project files and the project baseline do not change) is the certificate import of the
address. Its rules are in the business library (`OpenPmsDentalBusiness/Email/DirectCertificateImport.cs`), so the tests run them without
the window:

- **The address's certificate** comes from the `.pfx` or `.p12` file the office's Direct provider (HISP) issued, with its password. Check
  reads the file without storing any key (`EphemeralKeySet`) and shows each certificate it will import: subject, issuer, serial number,
  valid from and to, thumbprint, and whether it names the address or its domain. Import reads the file again, checks it again, and puts the
  certificates into `NHINDPrivate` with their private keys, never exportable (`PersistKeySet`, `MachineKeySet` in production; never
  `Exportable`).
- **The checks**, each refused with a message that says what is wrong:
  - the window's address must be an e-mail address (the Edit Email Address window's user name, the address Direct sends from). The
    window shows it read-only, so Edit Email Address checks it before it opens the window (below), and the import's message says to enter
    the Direct address (name@domain) in Edit Email Address's Username or email address;
  - the file must be a `.pfx` or `.p12` file that opens with the password;
  - at least one certificate in the file must name the address or its domain, by the program's own rule above (so a certificate the
    import takes is one the program then finds); a file with none lists what it holds;
  - the certificates that name the address must have their private key in the file;
  - none of them may be expired or not yet valid; the message gives the date;
  - Windows must find no problem in the certificate's chain that would make the program skip it (`EmailCertificates.IsCertValid`:
    revoked, a bad signature, an issuer out of date).
  A certificate of the file that names neither the address nor its domain (the provider's CA certificate, for example) is not imported. A
  certificate already in the store with a key that can be used (it opens and signs, or decrypts, a test value: `IsKeyUsable`; the
  certificate's own "has a private key" only says that it names one) is not imported again, and the window lists it; one whose key was
  deleted or can't be opened by this user is imported again. Import stays off until Check has passed for the file and password shown; a
  changed file or password turns it off again.
- **The purpose of a certificate** (`GetPurpose`), from its Key Usage and Extended Key Usage extensions, found by OID: signing
  (digitalSignature or nonRepudiation) and encryption (keyEncipherment, or keyAgreement, how an elliptic curve key encrypts); without a Key
  Usage both; an Extended Key Usage that names neither e-mail protection nor every purpose makes it neither; a certificate for both counts
  as both. Some Direct providers issue one certificate for both, others a signing and an encryption certificate for the same address.
- **Replacing.** A certificate of the store is offered for replacement only when it has the same kind (address or domain) and name as a
  certificate of the file, shares a purpose with it, and expires before it does. Those (`ListDetailsReplaced`), with or without their keys,
  valid or not, are replaced only after the user answers Yes; No imports nothing. The question (`GetReplaceQuestion`) says the store "has a
  certificate for this address or its domain, for a purpose of a certificate in the file, that expires before it", lists both sides ("In the
  file" and "Replaced on this computer": subject, purpose, valid from, valid to, a missing or unusable key, thumbprint), warns when a
  replaced certificate is also for a purpose that no certificate of the file has (`GetReplaceWarning`: "No certificate in the file is for
  encryption, which a replaced certificate is also for. Replace it only if your Direct provider told you to."), lists the kept ones, and
  ends "Choose No to import nothing."
  Every other certificate of the store for the address or its domain stays and is listed as kept (`ListDetailsKept`, `GetKeptText`), with
  why: the other kind (a domain certificate serves the other addresses of the domain), another purpose (the signing certificate next to the
  file's encryption certificate), or the same purpose but valid as long as or longer than the file's (last year's file picked by mistake
  while the store holds the renewed certificate: no question, the file's is added next to it, and the import's message says that the
  renewed one was kept). The details box at Check and the message after the import show the same lists.
  The window passes the thumbprints it asked about, and the import refuses (nothing imported) if the store holds one to replace that it
  did not.
  A copy of the same certificate in the store without a usable key is not an older certificate: the file's copy, with its key, takes its
  place without a question.
  The new certificates are added first, then the replaced ones removed. The private key of a replaced certificate is deleted only when this
  window imported it (its friendly name says so), as the connection settings tool does for the database client certificate it replaces;
  the key of a certificate put there another way stays on the computer.
- **What an import stores and takes back.** Import reads the file again with `PersistKeySet`, which writes the keys of all of the file's
  certificates. The keys of the certificates it does not import (another address's, one already in the store) are deleted again at once.
  When the import stops before its certificates are all in the store (the store refuses one, or anything else fails), it removes the ones it
  added (and puts back a copy without a usable key that one of them took the place of) and deletes every key it stored
  (`PrivateImportRun.Undo`); the message says "Nothing was imported." and lists, under "Not removed from this computer again", what could
  not be taken back. A key it does not use that can't be deleted after a successful import is named in the warning. When .NET's import
  itself throws, it gives no certificate, so the import knows of no key to delete; Windows' PFX import is left to clean up after itself.
- **The users of the computer.** In production the program runs as the signed-in Windows user, who must be able to use the key to sign and
  decrypt, so the import gives `BUILTIN\Users` read access to the new private key file, as `OpenPmsDentalDbSettings.ClientCertificateStore`
  does for the database client certificate. If that fails, the import still succeeds and the window says how to give the access in
  certlm.msc.
- **The provider's trust anchor** (a `.cer`, `.crt`, `.der` or `.pem` file holding one public certificate) is imported into `NHINDAnchors`
  after the window shows its subject, issuer, serial and dates and the user answers Yes. A file with a private key, more than one
  certificate, an expired or not yet valid certificate, or one Windows finds a problem in is refused; one already in the store is not added
  again. The file is read with `EphemeralKeySet`, the whole file and each PEM block alike, so a `.pfx` under another name, or one in a PEM
  block, never writes its key anywhere.
- **Administrator rights.** Writing to the Local Computer stores needs them, as it did with certlm.msc. When Windows refuses, the window says
  to start the program with Run as administrator and import again. Each computer that sends or reads Direct messages (or signs e-mail) for
  the address needs the certificate.
- **Security log.** Each import makes a Setup entry with the e-mail address's number (0 for an address not saved yet), the thumbprints, the
  valid-to dates and how many certificates of the store were replaced and kept, in plain English with invariant dates (`yyyy-MM-dd` in the
  invariant culture, whatever the user's calendar), never translated. No password, no address, no key.
- **What the window shows.** Under the address: "The certificate is stored for this user name as typed in Edit Email Address. Sending from
  the address uses it once Edit Email Address is saved with this user name." (the message after the import says so too). The details after
  Check: each certificate the import adds (with its purpose), those of the file already there with their key, the store's it replaces after
  asking, and the store's it keeps, with why. "On this computer now": the store's certificates for the address or its domain (subject,
  purpose, valid from and to, a missing or unusable key, thumbprint) and whether a trust anchor of this computer trusts the address
  (`EmailMessages.IsSenderTrusted`, as the inbox shows it), read with a wait cursor (each key is opened, and the trust check builds the
  chains). A failed re-check at Import clears the details, as Check does. The file dialogs' filter texts are translated (`Lan.g`).
  The button of Edit Email Address shows with or without EHR, because signed e-mail (O-1) uses the same certificate; it asks for the
  address first when the user name is blank, and when the user name is not an e-mail address (the box is "Username or email address", so
  a plain SMTP login is allowed there) it stops with "The Direct certificate is found by the user name. Enter the Direct address
  (name@domain) in Username or email address, then click Certificate." (`GetAddressError`, the import's own rule).
- **Open Dental's servers.** Nothing is sent anywhere. `ICustomerUpdates.RequestEmailVerificationCode` and `PostEmailCertificate` had no
  caller left and were deleted, as the other Customer Updates members were (NoHqTests checks they stay gone).

## Also changed

- `EmailCertificates.IsForAddress` and `IsForDomain`: the name rule of `GetCertificatesForAddress` and `GetCertificatesForDomain`, one copy
  that the import uses too.
- `EmailCertificates.GetCertRfc822Name` and `GetCertDnsName` found the subject alternative name by its friendly name, which Windows localizes
  and leaves null for an OID it does not know: a certificate with such an extension before the subject alternative name threw a
  `NullReferenceException` in the program's certificate search and would have in the import. They compare the OID (2.5.29.17) now. The
  names inside the extension are still read from Windows' text of it (`AsnEncodedData.Format`), as before; on a Windows in another
  language that text may differ (not checked: no such test PC).

## Tests

`tests/LibraryUpdateTests/DirectCertificateImportTests.cs` (15 tests; the project, SDK-style since S1-6, compiles every `.cs` file of its
folder by the default glob, so no item was added), with the stores of S0-3D's tests (`DirectTestStores`: three stores of the current user
with names of their own, made and deleted by the test; the real `NHINDPrivate` and `NHINDAnchors` of the computer are never opened).
Generated certificates only (`DirectTestPki`): a test CA, certificates it issued for a test Direct address with their private keys and a
given Key Usage and Extended Key Usage, one for another address, an expired one, one not yet valid, a `.pfx` without the key, a `.pfx` with
several certificates, a domain certificate, and one with an extension of a private OID; the files are written to a temp folder of the test
and deleted, the password is made at run time and kept in memory only. Every key the import stores is deleted afterwards. The failure tests
use two seams of the import that are null in the program (`ActionAfterKeysStoredForTests`, `ActionBeforeStoreAddForTests`).

- `ImportPrivateFile_PutsTheCertificateIntoThePrivateStoreWithAKeyThatCannotBeExported`: Check shows subject, issuer, serial, dates, the
  purpose, what it names and the key, and makes no store; Import adds the address's certificate only (not the CA of the file), marked, with
  a key whose policy forbids export, whose private parameters can't be read and which no PFX can carry, but which signs;
  `GetCertFromPrivateStore` and `EmailPrivateResolver` find it; the same file again is refused as already there.
- `CheckAndImportPrivateFile_RefuseWhatTheProgramWouldNotUse`: wrong or blank password, a `.cer`, no file, a missing file, an empty file, no
  address, not an address (the message names Edit Email Address's Username or email address), another address's certificate (the message
  lists the file), the address in another domain, another address of
  the domain, no private key, expired, not yet valid, the clock passed in; Import refuses the same way; no store is made.
- `ImportPrivateFile_TakesADomainCertificateForAnAddressOfTheDomain`: a domain certificate signs for every address of the domain; an
  address certificate next to it does not replace it (kept: the other kind, with why) and comes first for its address; a domain certificate
  valid longer replaces the domain one.
- `ImportPrivateFile_ReplacesAnOlderCertificateOnlyAfterAsking`: two older certificates of the same purpose (one imported here, one put
  there another way) and another address's; the question lists the file's certificate and both of the store's and ends "Choose No to import
  nothing."; without the agreement, or with half of it, nothing changes; with it, both are gone, the key of the one imported here is
  deleted and the other's kept, the other address's certificate stays; the log text has the number, thumbprint, last day and counts, and no
  address or password.
- `CheckPrivateFile_OffersOnlyAnOlderCertificateOfTheSamePurpose` (item 1): the store holds the renewed certificate (valid 50 days more)
  and last year's file, still valid for 10 days, is checked: the renewed one is not offered, no question, kept "for the same purpose and
  valid as long as or longer than the one in the file, which is added next to it"; the import asks nothing, says the same and keeps it; a
  file valid longer than both gets the question, which lists exactly those two under "Replaced on this computer", each side with subject,
  purpose, valid from and valid to, names neither older nor newer and ends "Choose No to import nothing."; without the agreement nothing
  changes, with it both are replaced; a certificate that ends at the same moment is not offered.
- `ImportPrivateFile_KeepsSeparateSigningAndEncryptionCertificatesOfOneAddress` (item 1): a signing-only and an encryption-only
  certificate for the same address, imported in either order: the second's check offers nothing and keeps the first ("for another purpose
  than the certificates in the file"), and both stay after each import; a renewed signing certificate offers only the signing one (the
  question's "Replaced" block holds exactly it, with "signing" and both dates) and keeps the encryption one; a certificate for both replaces
  both; a signing certificate that replaces one for both gets the warning that the file has none for encryption.
- `GetPurpose_ReadsTheKeyUsageAndTheExtendedKeyUsage` (item 1): digitalSignature, nonRepudiation, keyEncipherment, keyAgreement,
  dataEncipherment alone, no extensions, client authentication only, client authentication with e-mail protection, every purpose; the
  purpose texts.
- `ImportPrivateFile_StoresTheKeyWithACopyOfTheSameCertificateWithoutItsKey`: a keyless copy in the store ("no private key") is the same
  certificate: no question, and the store then holds it once, with its key.
- `ImportPrivateFile_ImportsAgainWhenTheStoredKeyCannotBeUsed` (item 4): the imported certificate's key is deleted behind the store's back;
  the certificate still says it has a key, but `IsKeyUsable` is false, the window's line says "private key not usable on this computer",
  Check offers the import again instead of refusing, and the import stores a key that signs; then the same file is refused as already
  there.
- `ImportPrivateFile_DeletesWhatItStoredWhenItStopsPartway` (item 3): a `.pfx` with a signing and an encryption certificate for the address
  and another address's, all with keys. The store refuses the second certificate: "Nothing was imported.", the first is removed again and
  all three keys are deleted. Something else fails after the keys were stored: the same. A key that can't be deleted again is named under
  "Not removed from this computer again". On success the other address's key is deleted and the imported one stays; an unused key that
  can't be deleted is named in the warning.
- `GetLogText_IsPlainEnglishWithInvariantDates` (item 6): the same text under en-US, th-TH and ar-SA (other calendars), de-DE and fr-CA;
  no `Lans.g` in it.
- `CheckPrivateFile_ReadsACertificateWithAnExtensionWindowsHasNoNameFor`: the OID fix above.
- `ImportAnchorFile_TrustsTheAddressIssuedUnderTheProvidersAnchor`: the CA as a DER `.cer` is shown, added to the anchors only, and the
  address issued under it is then trusted (`IsSenderTrusted`); the same anchor as `.pem` is refused as already there.
- `CheckAndImportAnchorFile_RefuseWhatIsNotOneValidPublicCertificate`: DER and PEM in `.cer`, `.crt`, `.der` and `.pem` are read; a `.pfx`
  type, no file, a missing file, a `.pfx` under a `.cer` name (with and without a password), a `.pfx` in a PEM block (item 2; with and
  without a password), two certificates, a damaged PEM, not a certificate, expired, not yet valid: refused, nothing written.
- `Window_ImportsThroughTheBusinessLayerAndAsksFirst` (source, as the window can't run in a test): no Open Dental call left, the Yes before
  the replacing and before the anchor, the question that the Yes answers is `GetReplaceQuestion`'s, which holds the new certificate's
  description before the store's, no "older" anywhere in the window, the warning, the certificates already there and the kept ones in the
  details box (item 7), the kept ones in the message after the import, the details cleared by a failed re-check at Import as at Check, the
  user name note in the window and the message, the wait cursor around the store and the trust check, the translated file filters (none
  left in the designer), the thumbprints passed, the log entries, the password cleared, Import off until Check, the password box, Edit
  Email Address's call and its user name check (`GetAddressError` after the blank check and before the window opens, with the message that
  names Username or email address); in the business layer never the Exportable flag, both anchor paths with `EphemeralKeySet` and no import
  with the default key flags (item 2), every key object of `GetRSAPrivateKey` and `GetECDsaPrivateKey` in a `using` and every certificate
  of `ReadStore` disposed (item 5), the machine key set and the users' read access only for the Local Computer.
- `Stores_AreTheLocalComputersNhindStoresInProduction`.

Changed: `DirectRestoredTests.Windows_HaveTheirDirectControlsAgain` (the new call; the window no longer reads certificate names itself)
and `NoHqTests.CustomerUpdates_VendorMembersAreRemoved` (the two deleted members).

Not tested here: the Local Computer stores, the machine key set and the users' read access, which need administrator rights (the launch
checks below).

## Launch checks for the lead

On a test computer, with a test database, as a Windows administrator (Run as administrator), and with generated certificates only, never
a real provider's file. A test CA and the certificates it issues can be made in PowerShell (in `Cert:\CurrentUser\My`, so the test CA is
never trusted anywhere):

```powershell
$ca = New-SelfSignedCertificate -Subject "CN=Direct Test CA, O=Example Test HISP" -KeyUsage CertSign,CRLSign,DigitalSignature `
  -TextExtension @("2.5.29.19={critical}{text}ca=1&pathlength=0") -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(60)
$cert = New-SelfSignedCertificate -Subject "CN=Direct Test, E=direct@hisp.example.test" -Signer $ca `
  -KeyUsage DigitalSignature,KeyEncipherment `
  -TextExtension @("2.5.29.17={text}email=direct@hisp.example.test","2.5.29.37={text}1.3.6.1.5.5.7.3.4") `
  -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(30)
$password = Read-Host -AsSecureString -Prompt "A throwaway password"
Export-PfxCertificate -Cert $cert -FilePath .\direct-test.pfx -Password $password -ChainOption BuildChain
Export-Certificate -Cert $ca -FilePath .\direct-test-ca.cer
```

`-KeyUsage DigitalSignature,KeyEncipherment` and the e-mail protection Extended Key Usage (1.3.6.1.5.5.7.3.4) make a certificate for
signing and encryption, as most providers issue it; `-KeyUsage DigitalSignature` alone makes a signing certificate and
`-KeyUsage KeyEncipherment` alone an encryption certificate (check 4); `-NotAfter` sets its end. `-ChainOption BuildChain` puts the test
CA's certificate into the `.pfx`, as providers often give the chain (the import leaves it out: it names no address); the CA's `.cer` is the
test trust anchor. Afterwards remove the certificates from `CurrentUser\My` with their keys (`Remove-Item` with `-DeleteKey`), delete the
files and forget the password.

1. Setup > E-mail, an address `direct@hisp.example.test`: Certificate opens the window with the address and the note that the
   certificate is stored for the user name as typed. Pick the `.pfx`, type the password, Check: the subject, issuer, serial, dates,
   "Purpose: signing and encryption", "For the address", "Private key: yes". Import: the message, and "On this computer now" lists it.
   In certlm.msc, NHINDPrivate > Certificates holds it, without the test CA; Manage Private Keys shows Users with Read; All Tasks > Export
   offers no "Yes, export the private key". The security log has the Setup entry with the address's number, the valid-to date as
   `yyyy-MM-dd`, the replaced and kept counts, and no address.
2. A wrong password, the `.cer` in the certificate box, a `.pfx` for another address: each refused with its message, nothing added.
3. Import a second generated `.pfx` for the same address and purposes, valid longer than the first (`-NotAfter`): the question says the
   store has a certificate for this address or its domain, for a purpose of a certificate in the file, that expires before it, lists the
   new one ("In the file") and the first ("Replaced on this computer") with their purposes, valid from and valid to, and ends "Choose No to
   import nothing."; No imports nothing; Yes replaces it, and the first one's key file is gone from `%ProgramData%\Microsoft\Crypto` (it
   was imported here). Then Check the first `.pfx` again: nothing is offered, and the details list the second as kept, "for the same
   purpose and valid as long as or longer than the one in the file"; Import asks nothing, its message says the same, and both are in
   NHINDPrivate. Remove the first again in certlm.msc.
4. A signing-only and an encryption-only certificate for one address (`-KeyUsage DigitalSignature` and `-KeyUsage KeyEncipherment`):
   import one, then Check the other: nothing is offered and the first is listed as kept "for another purpose"; Import: both are in
   NHINDPrivate. Import a renewed signing-only certificate (valid longer): only the signing one is offered, the encryption one is kept.
5. In Edit Email Address, a user name that is not an e-mail address (for example `smtplogin`), Certificate: the message names Username or
   email address, and the window does not open.
6. As a standard Windows user afterwards: point the address's outgoing server (SMTP) at a local capture server on 127.0.0.1 that keeps
   the messages it receives (never a real mail server, and no real recipient), save the address, send a signed e-mail from it, and check
   in the captured message that it is signed with the imported certificate (its key is readable by Users).
7. The `.cer` in the trust anchor box, Import...: the details and the question; Yes adds it to NHINDAnchors, and "On this computer now"
   says it is trusted. Importing it again says it is already a trust anchor.
8. Without Run as administrator: Import says that Windows refused and how to import; nothing is added.
9. Remove the test certificates and anchors from NHINDPrivate and NHINDAnchors in certlm.msc afterwards, and put the address's outgoing
   server back.

## For the .NET 10 switch package

What this code needs when the business library moves to .NET 10 (stage 3b); nothing of it builds differently on .NET Framework 4.8 today.

- **Loading certificates (SYSLIB0057).** From .NET 9, `X509Certificate2Collection.Import(byte[])` and its overloads with a password and
  key flags, and the `new X509Certificate2(byte[])` constructors (with or without a password), are obsolete (warning SYSLIB0057).
  Their replacement is `X509CertificateLoader`: `LoadPkcs12Collection(byte[],password,
  X509KeyStorageFlags)` for the `.pfx` or `.p12` file (`CheckPrivate` with `EphemeralKeySet`, `AddPrivate` with `PersistKeySet` and
  `MachineKeySet` or `UserKeySet`; still never `Exportable`), and `LoadCertificate(byte[])` for a public certificate (each PEM block and
  the DER file of `ReadAnchor`, and the copy it returns, `new X509Certificate2(RawData)`). The loader no longer guesses the format: a
  `.pfx` under a `.cer` name then fails `LoadCertificate` and is refused as "not a certificate file" instead of "holds a private key",
  unless `ReadAnchor` tries `LoadPkcs12Collection` (with `EphemeralKeySet`) first to give the clearer message; the anchor test's expected
  texts follow whichever is chosen. `LoadPkcs12Collection` applies `Pkcs12LoaderLimits.Defaults`; a provider's file outside them is
  refused like a damaged one, which is right (never `DangerousNoLimits`). The tests' helpers (`DirectTestPki`, `DirectTestStores.Public`,
  `TestCertificates` in `FakeMailServers.cs`) use the same calls and move the same way.
- **Key file rights.** `File.GetAccessControl` and `File.SetAccessControl` (`GrantUsersReadToPrivateKey`) are not on .NET's `File`; they
  are extension methods of `FileInfo` in `FileSystemAclExtensions` (`new FileInfo(pathKey).GetAccessControl()` and
  `.SetAccessControl(fileSecurity)`, namespace `System.IO`, in the shared framework on Windows, no package).
- **CNG keys.** On .NET, `RSACng.Key` and `ECDsaCng.Key` return a new `CngKey` on every call, which the caller disposes;
  `GetMachinePrivateKeyFilePath` already does, and `TryDeletePrivateKey`'s `Key.Delete()` disposes it. .NET imports a PKCS#12 key into
  CNG unless the file names a CAPI provider, so the CAPI branches stay for the files that do.
- Unchanged: `EphemeralKeySet` is supported on Windows; the store names, `X509Store` and the chain check (`EmailCertificates.IsCertValid`)
  work as they do now.

## Release notes

`docs/release-notes/next.md`, section "Direct certificates: imported from your Direct provider's file", and one line in the overview.
`docs/specs/ehr-rebuild-plan.md` section 3.2's Direct note and launch check 10 no longer say that registering a certificate fails.

## After review (branch `dev/directcert-2`)

- **The replace question called the store's certificate older**, but the store's certificates are never compared by date: with the renewed
  certificate in the store and last year's still valid file picked by mistake, a Yes removed the current certificate (and its key, when
  this window had imported it). The question names no age now, shows both sides and warns when the store's is valid longer (Replacing,
  above). The import still allows the replacement after a Yes: a provider may reissue a certificate with an earlier end, so the dates do not
  decide; the user does, told which one lasts longer. The refusal, the warnings and the log text say "replaced" or "of the store" instead of
  older. (Superseded by the second fix round, item 1: the dates and purposes now decide what is offered.)
- **A user name that is not an e-mail address** opened the window, which then refused at Check with a message to enter the address, in a
  box the window shows read-only. Edit Email Address now checks the user name first and names its box, and the import's message points
  there too.

## Second fix round (branch `dev/directcert-2`, 2026-11-09)

The reviews passed after the first fix round; the lead had these review notes fixed before the merge, each with a test where one can
be written. `main` (S1-6's SDK-style test projects, TC-2, EHR-1 and the others) was merged in first (690d81466). The plan of the round is
44a31adb0; the business layer 3dc25dd86, the window 103794cf5, the tests 268f6fe5a, these documents the commit after them.

1. **Which certificates an import replaces.** Before, every certificate of the store of the same kind (address or domain) and name, even
   one valid at the same time or longer. Some Direct providers issue separate signing and encryption certificates for one address, so
   importing one replaced the other. Now a certificate of the store is offered for replacement only when it has the same purpose as a
   certificate of the file of the same kind (signing or encryption, from the Key Usage and Extended Key Usage extensions; one with both
   counts as both) and it expires before that certificate does (Replacing, above). The question lists exactly those, with both sides'
   subject, purpose, valid from and valid to; the others stay and are listed as kept, with why. This replaces the first round's rule, under
   which the store's certificates were not compared by date and one valid longer was offered with a warning: such a certificate is no longer
   offered, and the import says that it was kept. Tests: `CheckPrivateFile_OffersOnlyAnOlderCertificateOfTheSamePurpose`,
   `ImportPrivateFile_KeepsSeparateSigningAndEncryptionCertificatesOfOneAddress`, `GetPurpose_ReadsTheKeyUsageAndTheExtendedKeyUsage`.
2. **The trust anchor's PEM path** read each PEM block with the default key flags, so a PKCS#12 hidden in a PEM block wrote its key to
   the user's key store until the certificate was released. It reads them with `EphemeralKeySet`, as the private file's check does. Tests:
   `CheckAndImportAnchorFile_RefuseWhatIsNotOneValidPublicCertificate` (a `.pfx` in a PEM block) and the source check of
   `Window_ImportsThroughTheBusinessLayerAndAsksFirst` (no import with the default key flags).
3. **Failure paths that could leave a key behind.** The keys of the file's certificates that are not imported (their deletion's result was
   ignored), and an import that fails partway after some keys were written: the import deletes what it wrote and reports what it could not
   remove (What an import stores and takes back, above). Test: `ImportPrivateFile_DeletesWhatItStoredWhenItStopsPartway`.
4. **"Already in the store with its key"** is true only when the key can be used (it opens and signs or decrypts a test value), not when
   the certificate only says it has one; otherwise the window offers to import again instead of refusing. Test:
   `ImportPrivateFile_ImportsAgainWhenTheStoredKeyCannotBeUsed`.
5. **Disposal.** The key objects of `GetRSAPrivateKey` and `GetECDsaPrivateKey` (in `TryDeletePrivateKey`, `GetMachinePrivateKeyFilePath`
   and the new `IsKeyUsable`), the `CngKey` read for a key file's name, and the certificates read from a store (`ReadStore`, and the
   store's lists of the import) are disposed. Test: the source checks of `Window_ImportsThroughTheBusinessLayerAndAsksFirst`.
6. **The security log text** is plain English with invariant dates and numbers (no translation). Test:
   `GetLogText_IsPlainEnglishWithInvariantDates`.
7. **The window.** The replace question ends "Choose No to import nothing."; the file dialogs' filter texts are translated; a failed
   re-check at Import clears the details as Check does; the details also list the file's certificates already in the store with their
   key (and the kept ones); the window says that the certificate is stored for the user name as typed, which takes effect for sending once
   the address is saved (a note under the address and a line of the message after the import); a wait cursor while the window reads the
   store and the trust anchors. Tests: `Window_ImportsThroughTheBusinessLayerAndAsksFirst` (source) and the question's end in the replace
   tests.
8. **The documents.** The .NET 10 notes for the switch package (above), the launch checks' certificate recipe (Key Usage, e-mail
   protection, a test CA chain) and capture server on 127.0.0.1 (check 6, the signed send), the release notes, and the plan: its spot check
   of section 9.1 no longer says that `FormEmailCertRegister` calls `CustomerUpdatesProxy`, and O-13 is marked done by this package.

Left as they are (owner questions): `ICustomerUpdates` and `CustomerUpdatesProxy`, and the read access of `BUILTIN\Users` to the imported
private keys.

Not changed, noticed on the way: the program's own search of the signing certificate (`EmailMessages.GetCertFromPrivateStore`) and the
Direct agent's resolver take the valid certificates for the address in store order, whatever their purpose; with a separate signing and
encryption certificate for one address, signing may pick the encryption one. The import now keeps both, so the search is where a purpose
would be chosen. (Done in the third fix round, item 2.)

## Third fix round (branch `dev/directcert-2`, 2026-11-09)

The second round's review passed; the lead decided these before the merge, each with a test. The plan (this section, filled in with the
commits and tests when they are done):

1. **Key safety.** One `.pfx` can give two of its certificates one key (a renewal with the same key): Windows stores the key once, in one
   key container, for both. The import deleted the key of a certificate of the file it does not use (and the key of a replaced
   certificate, and in `Undo` every key it stored) without looking whether another certificate still uses that container, so the imported
   certificate could lose its key. Before a container is deleted, its unique name (`CngKey.UniqueName`, `CspKeyContainerInfo.UniqueKeyContainerName`)
   is compared with those of every certificate the import adds, keeps or leaves in the store; only a container none of them uses is
   deleted, and each container once.
2. **The lookup chooses by purpose** (the owner question, decided by the lead). Signing (`GetCertFromPrivateStore`, and the Direct agent's
   sender certificate) prefers a valid certificate for signing, encryption (a recipient's certificate the agent encrypts to) and decryption
   (the office's own certificates the agent decrypts with) one for encryption; of several of that purpose, the one valid longest; without
   one, any valid certificate for the address, as before. Nothing else of S0-3D's send and receive changes.
3. **The warnings after a stop.** When anything throws after the certificates were added, the warnings already collected (a key not
   deleted, a replaced certificate not removed) still reach the result's Warning.
4. **"Never throws".** `IsKeyUsable` and `TryDeletePrivateKey` catch every exception except the critical ones (`OutOfMemoryException`,
   `StackOverflowException`, `ThreadAbortException`) and say not usable or not deleted.
5. **A test of `Undo`** putting back the store's copy without a key that an added certificate took the place of.
6. **A failed `PersistKeySet` import** says that Windows may have left a private key of the file behind, and the release notes say so.
7. **The documents.** The launch checks' recipe makes its certificates with `-Type Custom`; checks 3 and 9 remove the certificates with
   `Remove-Item Cert:\LocalMachine\NHINDPrivate\<thumbprint> -DeleteKey`; the test count matches the tests; the .NET 10 note on
   `RSACng.Key` and `ECDsaCng.Key` says to verify it at the switch.
8. **The source check of the key flags** also finds an `Import` with more than one argument and `DefaultKeySet` or no flags.

Left as they are (owner questions): `ICustomerUpdates` and `CustomerUpdatesProxy`, and the read access of `BUILTIN\Users` to the imported
private keys.
