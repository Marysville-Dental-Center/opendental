# Port note: P262-GLIDEWELL, Glidewell lab cases (Open Dental 26.2 items 1 and 2)

Branch `p262/glidewell-1`, cut from `p262/schema-1` (the 26.2 schema foundation, `docs/port-notes/P262-SCHEMA.md`). Owner decision of
2026-11-07: port 26.2 items 1 and 2; item 3 (the integration works with Open Dental Cloud) is skipped, as this program has no cloud edition.

**Status: partial.** Everything the public sources allow is built: the program link, the office's Glidewell accounts, the lab case products,
the Glidewell part of the Edit Lab Case window, the request and the stored answers, sending and status checks with their rules, and one
isolated client interface with a fake for the tests. **The transport is blocked:** Glidewell publishes no interface for practice management
programs (no address, sign-in scheme, call paths or field formats), so this version's client sends nothing and says why. What is still needed
is listed under "Blocked: what is needed".

**Owner decisions of 2026-11-07** (integration `next/int262-1`; "Owner decisions" below): the office itself asks Glidewell for API access;
no patient data goes to Glidewell until Setup records a signed business associate agreement with Glidewell (the postcard service's rule);
the program link is hidden in Program Links until a transport exists. Every other question: decided as built.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled; no office data was used.

- **Release notes** (https://www.opendental.com/site/version26_2.html), section "Integration with Glidewell": lab cases are sent
  electronically from the Edit Lab Case window; it works with Medit and iTero scanners; it works with Open Dental Cloud (item 3, skipped). The
  section links to no manual page.
- **Open Dental's database documents** for 26.2 (`OpenDentalDiffDocumentation26-2.xml`, `OpenDentalDocumentation26-2.xml`, read-only in
  `.git/od-tools/od-docs/26-2`): the tables `glidewellacct` (a Glidewell billing account; several providers can share one; account number,
  encrypted API key, time of the last scanner sign-in), `glidewellmsg` (one row per lab case sent: inserted on the first send, then only its
  response and status are updated, no history; the account it was sent with, never the provider's default; the scanner it was sent for; the
  request and response fields listed below), `labcaseproduct` (product from a list Glidewell supplies, material, shade, teeth, notes) and
  `provider.GlidewellAcctNum` (the provider's default account; several providers can share one), and the enumeration
  `EnumGlidewellOrderStatus` (eight statuses, `P262-SCHEMA`).
  - Request fields (`RawJsonRequest`): CreateVendorOrder (required, true by default), VendorName (required, "iTero"; Medit "may be integrated
    in the future"), OrderId (required, the lab case number), DoctorId (the provider number), LicenseNumber, PatientFirstName and
    PatientLastName (required), PatientId (the patient number), PatientGender, PatientDateOfBirth, Notes.
  - Response fields (`RawJsonResponse`): VendorOrderId, VendorName ("iTero" or "Medit"), VendorPracticeId, VendorDoctorId, DoctorId,
    patientId, PatientFirstName, PatientLastName, PatientDateOfBirth, Patientgender, ToothRange, Products, Shade, OutboundTrackingNumber,
    OutboundCarrier, Notes. `VendorRxId` comes from "the Glidewell Register Response"; in the real workflow Glidewell sends it to the office's
    scanner, where the user picks the case.
- **The public manual.** The public pages for the Edit Lab Case window (`manual/labcaseedit.html`) and Laboratories
  (`manual/laboratories.html`) are the 24.4 manual and do not mention Glidewell; the 26.x manual is not public ("blocked from public access").
  No public manual page describes this feature.
- **Glidewell's public material** (searched 2026-11-07): its "submit a digital case" pages (how an office pairs an iTero or Medit scanner with
  the lab), its partner pages and a press release of 2026-10-15 about its first practice management integration (with another vendor). None
  documents an interface for practice management programs: no address, authentication, calls, field formats, status values or product list.

## What was built

### Program link and accounts

- `ProgramName.Glidewell` (appended after `MeetInTheCloud`; `program.ProgName` stores the name, so its number does not matter at runtime;
  see "For the integrator" for the agreed append order). The row "Glidewell lab cases" is inserted at
  runtime with `Enabled=0` (`GlidewellProgram.EnsureProgram`, through `Programs.InsertIfMissing`), like every vendor-direct program link, the
  first time Setup, Program Links opens, with the business associate agreement's two property rows. **No migration step:** levels 2026110791 to 2026110799 stay unused.
- **Setup, Program Links, Glidewell lab cases** (`FormGlidewellSetup`, registered by `GlidewellProgramLinkSetup`): Enabled, the office's
  accounts (account number, API key status, default for which providers), the business associate agreement box (since 2026-11-07), and the
  reason this version cannot send. **Hidden** in Program Links while this build has no transport (`GlidewellProgram.IsShownInProgramLinks`,
  owner decision of 2026-11-07): the row is inserted (Enabled=0) but not listed, so the window cannot be opened and the link cannot be turned
  on. Turning it on needs at
  least one account with an account number and an API key.
- **Glidewell account** (`FormGlidewellAcctEdit`): account number (unique), API key (masked; blank keeps the stored one; changed only by a user
  with Manage High Security Program Properties, logged under that permission), the last scanner sign-in (read-only), and the providers whose
  default account it is (`provider.GlidewellAcctNum`; a provider has one default). Delete clears the providers' defaults and is refused once a
  lab case was sent with the account.
- The API key is stored with this program's own format (`osd1$`, `SecretValue`); a key stored by another program (Open Dental's ciphertext, or
  plain text) is never used and reads as "Enter again" (`LegacyPolicy.RejectPlaintext`).
- **Key length** (fix of `p262/glidewell-2`): `glidewellacct.ApiKey` is `varchar(255)` in the 26.2 documents, and the stored value is `osd1$`
  plus base64 of IV (16 bytes), ciphertext (whole 16-byte blocks) and HMAC (32 bytes): 241 characters for a 127-byte key, 261 for a 128-byte
  key. The program runs MySQL with a blank `sql_mode`, so the database would cut a longer value short without an error, and the shortened value
  would never decrypt ("Enter again" for good, and a security log entry for a change that did not happen). `GlidewellAccts.SetApiKey` now
  refuses a stored value longer than 255 characters (`GlidewellApiKeyResult.TooLong`, nothing changed), and `GlidewellAccts.SaveAccount`
  (the account window's save: key, row and security log entries) saves and logs nothing then. The window says "The API key is too long to
  store (at most 127 characters)." The limit is 127 UTF-8 bytes, so a key of non-ASCII characters is refused earlier; Glidewell's keys are
  not published ("need example") and are expected to be ASCII. The key box takes 255 characters, not 127, on purpose: a text box cuts a
  pasted text at its MaxLength without a word, so a box of 127 would save a longer pasted key shortened; with 255 the longer key reaches the
  check and is refused with the message.
- Data classes: `GlidewellAccts`, `GlidewellMsgs`, `LabCaseProducts` (Middle Tier safe).

### Edit Lab Case window

A "Glidewell" group (right side, under Tracking) **only while the program link is on**; otherwise the window is unchanged and nothing Glidewell
is read or written.

- **Account**: the provider's default account, or the office's only account, or none (the user picks). Changing the provider picks that
  provider's default until the user picks an account. Fixed once sent.
- **Scanner**: iTero or Medit. Fixed once sent.
- **Products**: material, shade, teeth, notes (`FormLabCaseProductEdit`); saved with the lab case (Save, New Slip, Send), deleted with it.
- **Send to Glidewell**: checks the account and whether this version can send before saving anything; then saves the lab case, checks the
  rest (program link on, one send per lab case, the patient's first and last name), asks for confirmation naming the account, the scanner and
  every item sent, and sends. On success the lab case's `glidewellmsg` row is inserted; on any failure nothing is written.
- **Check Status** (once sent): asks Glidewell with the account the case was sent with and updates the same row (response always, status when
  it is one of the eight). **Details**: everything stored (status, scanner, account, VendorRxId, scanner order, carrier, tracking number,
  products, shade, teeth, Glidewell's notes); never the API key.
- A lab case sent to Glidewell cannot be deleted, and a new lab case that was sent is kept when the window is cancelled.
- Patient merge moves `glidewellmsg.PatNum` and `labcaseproduct.PatNum` with the lab case (`Patients.StringArrayPatNumForeignKeys`).

### The client interface (vendor-direct)

`OpenPmsDentalBusiness/VendorDirect/Glidewell/GlidewellClient.cs`:

- `IGlidewellClient`: `IsAvailable`, `UnavailableReason`, `Register(credentials, requestJson)`, `GetStatus(credentials, glidewellMsg)`, each
  returning a `GlidewellCallResult` (Success with the body, Failed with a reason, or Blocked). `GlidewellCredentials` carries the account
  number and the decrypted key for one call only (never logged; `ToString` leaves the key out).
- `GlidewellClientBlocked`: this version's client. Never opens a connection; every call is Blocked with the reason shown to the user.
- `GlidewellClients.Current`: the client in use (the blocked one unless a transport or a test fake is set).
- `GlidewellLabCases` holds every rule and is independent of the transport: a later transport only implements `IGlidewellClient`, calling
  Glidewell directly from the workstation with the account's own credentials. No package file contains a web address, Open Dental's servers or
  network code (a wiring test checks this).

### Tests

`tests/P262GlidewellTests` (new; in `OpenDental.sln` and `OpenDental.Local.slnf`), 44 tests (two added for the owner decisions of
2026-11-07: the business associate agreement and the hidden link): the request's documented fields in order and
the INFERRED formats, escaping, the notes with products, reading answers (case-insensitive names, lists, not-JSON), the status names, the
blocked client; on a `test_p262glidewell_` scratch database at the 26.2.0 reference schema (dropped afterwards): the program row, encrypted
keys and keys of other programs, the key length (127 characters saved and read back, 128 refused with nothing saved or logged), provider
defaults, account delete, product sync (including a key of another lab case's product and a stored
product list value), lab case delete, the merge list, and sending and status checks with `FakeGlidewellClient` (success, the one-send rule,
failures and exceptions write nothing, the blocked client, off until the office's credentials are entered, required names, the account the
case was sent with, unknown or missing status, lost key or account); source checks of the wiring, the vendor-direct rule, neutrality and the
too-long key message.

## INFERRED behaviours

The sources give the tables, the field names and the eight statuses. Everything below is this program's own choice.

1. The program link's name (`Glidewell`) and description; Open Dental's `program.ProgName` for it is not published. Off until the office turns
   it on, and turning it on needs one complete account.
2. Account numbers are unique; the provider defaults are set from the account window.
3. Request formats: OrderId, DoctorId and PatientId are JSON numbers; CreateVendorOrder is a JSON boolean; PatientDateOfBirth is `yyyy-MM-dd`,
   or null when the birthdate is unknown; PatientGender is Male, Female, Other or Unknown; VendorName is "iTero" or "Medit" (the response's
   spelling); text is trimmed; the body is one line in the documented order.
4. LicenseNumber is `provider.StateLicense` (not the per-clinic license of `providerclinic`).
5. The request has no product field, so Notes are the lab case's instructions followed by "Products:" and one line per product.
6. Reading answers: names are matched ignoring case (the documents spell them inconsistently). VendorRxId is read from a "VendorRxId" field of
   the register response. The status is read from "OrderStatus", else "Status", by name ignoring case, spaces, hyphens and underscores; "In Lab"
   (the documents' example) is InProgress and "Canceled" is Cancelled; numbers are not read. A register answer without a status is Initialized
   ("registered and awaiting scan submission"). An unknown or missing status keeps the stored one; the answer is still stored.
7. One send per lab case: a second send is refused (the documents allow one Glidewell case per lab case). Two workstations sending the same
   case at the same moment are not prevented.
8. Status checks use the row's account (documented) and need its key; a deleted account or a lost key stops the check without calling.
9. A lab case sent to Glidewell cannot be deleted; its products are deleted with an unsent lab case.
10. Products are edited in the Edit Lab Case window while the program link is on (the documents do not say where). `GlidewellProduct` is never
    chosen, because Glidewell's list is not published: a new product row stores 0 and a stored value is kept and shown as a number. 0 may be a
    real product in Open Dental's list (owner question 3). The teeth are typed, not taken from the procedures.
11. `glidewellacct.DateTimeExpires` is shown, never written (nothing in this version signs in to a scanner).
12. Sending does not set the lab case's Sent date (the scan is not taken yet when the case is registered).
13. No audit trail entry for sending or checking status (the documents name no permission); account changes are logged under Setup and key
    changes under Manage High Security Program Properties.
14. The confirmation before sending lists every item sent, and the setup window says the same.
15. The API key limit of 127 UTF-8 bytes follows from the documented column width and this program's storage format, not from Glidewell:
    Glidewell's key format is not published. A longer key is refused, never shortened.

## Blocked: what is needed

The transport (`IGlidewellClient` implementation) needs, from Glidewell's partner documentation or a Glidewell contact:

1. The addresses of the interface (production and testing) and its sign-in scheme: how the account number and API key are sent, whether a key
   is issued to an office directly or only to a practice management vendor, and whether an integrator (partner) credential is needed as well.
2. The register call: method, path, body (confirm the documented fields and their types, the gender and date formats, VendorName values) and
   its answer (where VendorRxId and the status are).
3. The status call: by what it finds a case (OrderId, VendorRxId or VendorOrderId), its answer, the status values and how they map to the eight
   statuses, errors and limits.
4. The product list (`EnumGlidewellProductName`), with Open Dental's numbers for it, and whether products are sent at all.
5. How the scan from an iTero or Medit scanner is matched to the case (the scanner-side step the user does), for the user instructions.
6. A test account (sandbox) to run the transport against, and then the office's own production account number and API key.

With these, the transport is one class implementing `IGlidewellClient`, set as `GlidewellClients.Current`, plus its tests; nothing else
changes.

## Not built

- The transport (above). No call reaches Glidewell in this version.
- Item 3 (Open Dental Cloud).
- Picking a product from Glidewell's list, and taking the teeth from the lab case's procedures.
- No UI was run (OpenPmsDental.exe is not launched in this run); the windows are checked by build and source tests only.

## Files touched

- New: `OpenPmsDentalBusiness/VendorDirect/Glidewell/{GlidewellClient,GlidewellJson,GlidewellLabCases,GlidewellProgram}.cs`,
  `OpenPmsDentalBusiness/Data Interface/{GlidewellAccts,GlidewellMsgs,LabCaseProducts}.cs`,
  `OpenPmsDental/Forms/{FormGlidewellSetup,FormGlidewellAcctEdit,FormLabCaseProductEdit}.{cs,Designer.cs,resx}`,
  `OpenPmsDental/Forms/FormLabCaseEdit.Glidewell.cs`, `tests/P262GlidewellTests/*`, this note.
- Changed: `OpenPmsDental/Forms/FormProgramLinks.cs` (hidden until a transport exists, 2026-11-07), `TableTypes/Program.cs` (`ProgramName.Glidewell`), `Data Interface/LabCases.cs` (`Delete`), `Data Interface/Patients.cs` (merge
  list), `OpenPmsDental/Forms/FormLabCaseEdit.cs` and `.designer.cs`, `OpenPmsDentalBusiness.csproj`, `OpenPmsDental.csproj`, `OpenDental.sln`,
  `OpenDental.Local.slnf`, `docs/release-notes/next.md`.
- No schema, enum value, migration step or preference.

## For the integrator

**Done on `next/int262-1` (2026-11-07):** the agreed `ProgramName` order and the `Take(2)` relaxation of `P262LinksTests/WiringTests.cs`.

- **`ProgramName` append order.** `p262/links-2` also appends to `ProgramName` right after `MeetInTheCloud` (`AlliedstarConnect`,
  `OrthoComposite`), so `TableTypes/Program.cs` gets a textual conflict there. The agreed order is the Links names first, then Glidewell:
  `MeetInTheCloud, AlliedstarConnect, OrthoComposite, Glidewell`. The numbers do not matter at runtime (`program.ProgName` stores the name),
  and no released database holds them.
- **Tests at that spot.** Since `p262/glidewell-3`, `GlidewellWiringTests.ProgramName_GlidewellIsAppended_ExistingNumbersKept` no longer
  needs Glidewell to be last or directly after `MeetInTheCloud`: it checks that `ProgramName.Glidewell` is defined, that its number is above
  `MeetInTheCloud`'s, and that earlier values keep theirs (`ZImage+1==Microsoft365Email`). `P262LinksTests/WiringTests.cs` (lines 20-21 on
  `p262/links-2`) still asserts that exactly `{AlliedstarConnect, OrthoComposite}` follow `MeetInTheCloud`; when the two branches are
  integrated, relax it the same way: compare only the first two names after `MeetInTheCloud` (`Take(2)` instead of the whole tail) and keep
  `AlliedstarConnect==MeetInTheCloud+1`, which holds in the agreed order.

## Checks (2026-11-07)

- Full build of `OpenDental.Local.slnf` (Debug, x86): 0 errors.
- Targeted run (`P262GlidewellTests`, `NoHqTests`): `P262GlidewellTests` 40/40, `NoHqTests` 58/58. Scratch databases `test_p262glidewell_*`
  are dropped after the run. `ForkMigrationTests` was left out as instructed.
- After the key length fix (`p262/glidewell-2`): full build 0 errors; Targeted run `P262GlidewellTests` 42/42, `NoHqTests` 58/58.
- After the `ProgramName` test relaxation (`p262/glidewell-3`): full build 0 errors; Targeted run `P262GlidewellTests` 42/42, `NoHqTests`
  58/58; no `test_p262glidewell_` database left.
- Integration `next/int262-1` (the owner decisions): `P262GlidewellTests` 44/44 (two new tests: sending and status blocked without the
  agreement; the hidden link and the setup wiring), `P262LinksTests` with the `Take(2)` relaxation above.

## Owner decisions (2026-11-07)

Answered by the owner on 2026-11-07 ("all as recommended"); built on `next/int262-1`.

- **Question 1, API access:** the office itself asks Glidewell for API access (its account number and API key, and a test account). Revisit
  if Glidewell deals only with software vendors under a partner agreement.
- **Question 2, business associate agreement:** sending patient data to Glidewell stays blocked until Setup records that a signed business
  associate (data) agreement with Glidewell is on file, the rule the postcard service follows (`PostcardManiaProgram.IsBaaOnFile`,
  `AdvertisingPostcards.RequireBaaOnFile`). Built: the program row's ClinicNum 0 properties "BAA On File" ("0" or "1") and "BAA Confirmed"
  (who recorded it and when), inserted by `GlidewellProgram.EnsureProgram`; the setup window's box "A signed business associate agreement
  with Glidewell is on file", logged under Setup; `GlidewellProgram.IsBaaOnFile`. `GlidewellLabCases.GetSendProblems` (sending) and
  `CheckStatus` (a status check names the patient's lab case) refuse without calling the client while it is not recorded, and the Edit Lab
  Case window says so before it saves anything.
- **Question 8, Program Links:** the Glidewell lab cases program link is hidden in Program Links until a transport exists
  (`FormProgramLinks` leaves the row out while `GlidewellProgram.IsShownInProgramLinks` is false, which it is while
  `GlidewellClients.Current` is the blocked client). A row an Open Dental 26.2 database turned on keeps working as before (the Edit Lab Case
  group and the stored details), with sending blocked.
- **Every other question (3 to 7 and 9): decided as built** (owner, 2026-11-07).

## Questions for the owner (answered on 2026-11-07, above)

1. Who asks Glidewell for its partner documentation and a test account: the project (as the program's vendor) or each office? If Glidewell
   gives keys only to practice management vendors under a partner agreement, does the project sign one?
2. Patient data goes to the lab with every case: does the office need a business associate or data agreement with Glidewell before sending,
   and should sending stay blocked until it is recorded (as for postcards)?
3. Products: keep storing 0 in `labcaseproduct.GlidewellProduct` for new products until the list is published, or hide product entry until
   then?
4. Should a lab case sent to Glidewell stay undeletable, or be deletable with its Glidewell row?
5. Should sending set the lab case's Sent date?
6. Should sending be written to the audit trail, and under which permission?
7. Products are added to the request's Notes, since the request has no product field. Keep, or send only the instructions?
8. The Glidewell lab cases program link shows in Program Links although this version cannot send: keep (products and stored statuses are
   usable), or hide it until a transport exists?
9. If Glidewell's API keys turn out to be longer than 127 characters, widen `glidewellacct.ApiKey` (a fork schema change against Open Dental's
   documented `varchar(255)`, which an Open Dental database would not have) or store the key elsewhere?
