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
  A certificate of the file that names neither the address nor its domain is not imported. A certificate already in the store with its key
  is not imported again (the window says so).
- **Replacing.** The older certificates of the store for the same address (address certificates) or the same domain (domain certificates),
  with or without their keys, valid or not, are listed, and the import replaces them only after the user answers Yes; No imports nothing.
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
- **Open Dental's servers.** Nothing is sent anywhere. `ICustomerUpdates.RequestEmailVerificationCode` and `PostEmailCertificate` had no
  caller left and were deleted, as the other Customer Updates members were (NoHqTests checks they stay gone).

## Tests

`tests/LibraryUpdateTests/DirectCertificateImportTests.cs`, with the stores of S0-3D's tests (`DirectTestStores`: three stores of the current
user with names of their own, made and deleted by the test; the real `NHINDPrivate` and `NHINDAnchors` of the computer are never opened).
Generated certificates only: a test CA, a certificate it issued for a test Direct address with its private key, one for another address, an
expired one, one not yet valid, one without its key, and a domain certificate; the `.pfx` files are written to a temp folder of the test and
deleted, their passwords are made at run time and kept in memory only. Every key the tests persist is deleted afterwards.
