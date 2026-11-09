# Port note: DIRECT-CERT, Direct certificates without Open Dental (open point O-13)

Branch `dev/directcert-1`, cut from `main` (a3f3677d6). Open point O-13 of `docs/specs/net10-plan.md` section 12, answered by the owner on
2026-11-08: "Yes, load Direct provider's certificate from a file". No schema change, no new enumeration value, no new third-party file.
Synthetic certificates only; no network, no Open Dental server, no real certificate or password.

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
  - the window's address must be an e-mail address (the Edit Email Address window's user name, the address Direct sends from);
  - the file must be a `.pfx` or `.p12` file that opens with the password;
  - at least one certificate in the file must name the address or its domain, by the program's own rule above (so a certificate the
    import takes is one the program then finds); a file with none lists what it holds;
  - the certificates that name the address must have their private key in the file;
  - none of them may be expired or not yet valid; the message gives the date;
  - Windows must find no problem in the certificate's chain that would make the program skip it (`EmailCertificates.IsCertValid`:
    revoked, a bad signature, an issuer out of date).
  A certificate of the file that names neither the address nor its domain (the provider's CA certificate, for example) is not imported. A
  certificate already in the store with its key is not imported again (the window says so). Import stays off until Check has passed for
  the file and password shown; a changed file or password turns it off again.
- **Replacing.** The older certificates of the store for the same address (address certificates) or the same domain (domain certificates),
  with or without their keys, valid or not, are listed, and the import replaces them only after the user answers Yes; No imports nothing.
  The window passes the thumbprints it asked about, and the import refuses (nothing imported) if the store holds an older one it did not.
  An address certificate does not replace a domain certificate (the domain certificate serves the other addresses of the domain), and a
  copy of the same certificate without its key is replaced by the file's certificate with its key.
  The new certificates are added first, then the older ones removed. The private key of an older certificate is deleted only when this
  window imported it (its friendly name says so), as the connection settings tool does for the database client certificate it replaces;
  the key of a certificate put there another way stays on the computer.
- **The users of the computer.** In production the program runs as the signed-in Windows user, who must be able to use the key to sign and
  decrypt, so the import gives `BUILTIN\Users` read access to the new private key file, as `OpenPmsDentalDbSettings.ClientCertificateStore`
  does for the database client certificate. If that fails, the import still succeeds and the window says how to give the access in
  certlm.msc.
- **The provider's trust anchor** (a `.cer`, `.crt`, `.der` or `.pem` file holding one public certificate) is imported into `NHINDAnchors`
  after the window shows its subject, issuer, serial and dates and the user answers Yes. A file with a private key, more than one
  certificate, an expired or not yet valid certificate, or one Windows finds a problem in is refused; one already in the store is not added
  again.
- **Administrator rights.** Writing to the Local Computer stores needs them, as it did with certlm.msc. When Windows refuses, the window says
  to start the program with Run as administrator and import again. Each computer that sends or reads Direct messages (or signs e-mail) for
  the address needs the certificate.
- **Security log.** Each import makes a Setup entry with the e-mail address's number (0 for an address not saved yet), the thumbprints, the
  valid-to dates and how many older certificates were replaced. No password, no address, no key.
- **What the window shows.** "On this computer now": the store's certificates for the address or its domain (subject, last day, a missing
  key, thumbprint) and whether a trust anchor of this computer trusts the address (`EmailMessages.IsSenderTrusted`, as the inbox shows it).
  The button of Edit Email Address shows with or without EHR, because signed e-mail (O-1) uses the same certificate; it asks for the
  address first when the user name is blank.
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

`tests/LibraryUpdateTests/DirectCertificateImportTests.cs` (10 tests; the project compiles every `.cs` file of its folder, so no item was
added), with the stores of S0-3D's tests (`DirectTestStores`: three stores of the current user with names of their own, made and deleted
by the test; the real `NHINDPrivate` and `NHINDAnchors` of the computer are never opened). Generated certificates only (`DirectTestPki`): a
test CA, certificates it issued for a test Direct address with their private keys, one for another address, an expired one, one not yet
valid, a `.pfx` without the key, a domain certificate, and one with an extension of a private OID; the files are written to a temp folder
of the test and deleted, the password is made at run time and kept in memory only. Every key the import stores is deleted afterwards.

- `ImportPrivateFile_PutsTheCertificateIntoThePrivateStoreWithAKeyThatCannotBeExported`: Check shows subject, issuer, serial, dates, what
  it names and the key, and makes no store; Import adds the address's certificate only (not the CA of the file), marked, with a key whose
  policy forbids export, whose private parameters can't be read and which no PFX can carry, but which signs; `GetCertFromPrivateStore` and
  `EmailPrivateResolver` find it; the same file again is refused as already there.
- `CheckAndImportPrivateFile_RefuseWhatTheProgramWouldNotUse`: wrong or blank password, a `.cer`, no file, a missing file, an empty file, no
  address, not an address, another address's certificate (the message lists the file), the address in another domain, another address of
  the domain, no private key, expired, not yet valid, the clock passed in; Import refuses the same way; no store is made.
- `ImportPrivateFile_TakesADomainCertificateForAnAddressOfTheDomain`: a domain certificate signs for every address of the domain; an
  address certificate next to it does not replace it and comes first for its address; a newer domain certificate replaces the domain one.
- `ImportPrivateFile_ReplacesAnOlderCertificateOnlyAfterAsking`: two older certificates (one imported here, one put there another way) and
  another address's; without the agreement, or with half of it, nothing changes; with it, both are gone, the key of the one imported here
  is deleted and the other's kept, the other address's certificate stays; the log text has the number, thumbprint, last day and count, and
  no address or password.
- `ImportPrivateFile_ReplacesTheSameCertificateWithoutItsKey`: a keyless copy in the store is replaced by the certificate with its key.
- `CheckPrivateFile_ReadsACertificateWithAnExtensionWindowsHasNoNameFor`: the OID fix above.
- `ImportAnchorFile_TrustsTheAddressIssuedUnderTheProvidersAnchor`: the CA as a DER `.cer` is shown, added to the anchors only, and the
  address issued under it is then trusted (`IsSenderTrusted`); the same anchor as `.pem` is refused as already there.
- `CheckAndImportAnchorFile_RefuseWhatIsNotOneValidPublicCertificate`: DER and PEM in `.cer`, `.crt`, `.der` and `.pem` are read; a `.pfx`
  type, no file, a missing file, a `.pfx` under a `.cer` name (with and without a password), two certificates, a damaged PEM, not a
  certificate, expired, not yet valid: refused, nothing written.
- `Window_ImportsThroughTheBusinessLayerAndAsksFirst` (source, as the window can't run in a test): no Open Dental call left, the Yes before
  the replacing and before the anchor, the thumbprints passed, the log entries, the password cleared, Import off until Check, the
  password box and file filters, Edit Email Address's call; in the business layer never the Exportable flag, the machine key set and the
  users' read access only for the Local Computer.
- `Stores_AreTheLocalComputersNhindStoresInProduction`.

Changed: `DirectRestoredTests.Windows_HaveTheirDirectControlsAgain` (the new call; the window no longer reads certificate names itself)
and `NoHqTests.CustomerUpdates_VendorMembersAreRemoved` (the two deleted members).

Not tested here: the Local Computer stores, the machine key set and the users' read access, which need administrator rights (the launch
checks below).

## Launch checks for the lead

On a test computer, with a test database, as a Windows administrator (Run as administrator), and with generated certificates only, never
a real provider's file. A generated `.pfx` can be made in PowerShell with
`New-SelfSignedCertificate -Subject "CN=Direct Test, E=direct@hisp.example.test" -TextExtension @("2.5.29.17={text}email=direct@hisp.example.test") -CertStoreLocation Cert:\CurrentUser\My`,
exported with `Export-PfxCertificate` and a throwaway password, then removed from `CurrentUser\My` with its key (`Remove-Item` with
`-DeleteKey`); its `.cer` (`Export-Certificate`) is a test trust anchor. Delete the files and forget the password after the checks.

1. Setup > E-mail, an address `direct@hisp.example.test`: Certificate opens the window with the address. Pick the `.pfx`, type the
   password, Check: the subject, issuer, serial, dates, "For the address", "Private key: yes". Import: the message, and "On this computer
   now" lists it. In certlm.msc, NHINDPrivate > Certificates holds it; Manage Private Keys shows Users with Read; All Tasks > Export offers
   no "Yes, export the private key". The security log has the Setup entry with the address's number and no address.
2. A wrong password, the `.cer` in the certificate box, a `.pfx` for another address: each refused with its message, nothing added.
3. Import a second generated `.pfx` for the same address: the question lists the first; No imports nothing; Yes replaces it, and the
   first one's key file is gone from `%ProgramData%\Microsoft\Crypto` (it was imported here).
4. The `.cer` in the trust anchor box, Import...: the details and the question; Yes adds it to NHINDAnchors, and "On this computer now"
   says it is trusted. Importing it again says it is already a trust anchor.
5. Without Run as administrator: Import says that Windows refused and how to import; nothing is added.
6. As a standard Windows user afterwards: send a signed e-mail from the address and check that it is signed with the imported
   certificate (its key is readable by Users).
7. Remove the test certificates and anchors from NHINDPrivate and NHINDAnchors in certlm.msc afterwards.

## Release notes

`docs/release-notes/next.md`, section "Direct certificates: imported from your Direct provider's file", and one line in the overview.
`docs/specs/ehr-rebuild-plan.md` section 3.2's Direct note and launch check 10 no longer say that registering a certificate fails.
