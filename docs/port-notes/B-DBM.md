# Port note: B-DBM, Database maintenance tools

Branch `port/b-dbm`, cut from `m1/integration-a2-patch`. Wave B2 (built in the single wave together with every other remaining package).
No schema change and no fork migration step (level range 2027002101-2027002199 unused). No preference, permission or enum change.

Sources: the public release notes (version24_4 to version25_4), the public Database Maintenance Tools manual page and the public API documents
page (for the `_rawBase64_:` note format), as summarized in `docs/specs/features-24.4.md` to `features-25.4.md`. Open Dental publishes only the
names of most of these tools, so the character sets, column lists and messages below are our own and are marked INFERRED where they go beyond
the specs.

## Feature status

| Feature ID | Status | Where |
|---|---|---|
| `v24_4-tools-rawemails-unicode` | built | `DatabaseMaintenances.CleanUpRawEmails` (two small hooks), `DatabaseMaintenances.EmailText.cs`, `Misc/DbmTextCleaner.cs` |
| `v25_1-misc-dbm-email-invalid-chars` | built | `DatabaseMaintenances.EmailText.cs` (`EmailmessageInvalidCharactersInBodyText`) |
| `v25_2-tools-dbm-invalid-chars-gdi` | built | `DatabaseMaintenances.InvalidCharsGdi.cs`, `FormDatabaseMaintInvalidChars` (new), Tools row |
| `v25_3-tools-dbm-whitespace-tools` | built | `[DbmMethodAttr]` removed from `NotesWithTooMuchWhiteSpace`, Tools row "Notes White Space" |
| `v25_3-tools-dbm-employee-supervisor` | built | `DatabaseMaintenances.EmployeeSupervisor.cs` (`EmployeeMissingSupervisor`) |
| `v25_3-tools-dbm-base64-docs` | built | `DatabaseMaintenances.Base64Documents.cs`, `ImageStore.Import` (optional `doAvoidOverwrite`), Tools row "Base64 Documents" |
| `v25_4-tools-dbm-base64-documents` (25.4 twin, owned by M2-VERIFY) | built here, implemented once | same as above; the 25.4 entry settled which field is scanned (`document.Note`) |

## What each feature does

### v24_4-tools-rawemails-unicode: Raw Emails cleans Unicode white space

- Before the old and the re-processed copy of a message are compared (`EmailMessageCrud.UpdateComparison`), their Subject and BodyText are
  normalized: every Unicode space separator (category Zs other than the ASCII space: U+00A0, U+1680, U+2000 to U+200A, U+202F, U+205F, U+3000)
  becomes an ASCII space, and U+200B, U+200C, U+200D and U+FEFF are removed. Line breaks (CR, LF, U+2028, U+2029) and tabs are left alone. The
  normalized new values are what gets saved, so a message that differed only in that white space no longer counts as a change.
- INFERRED: the inbox lookup (`EmailUsername` against `RecipientAddress`) ignores the same white space and surrounding spaces, so a message whose
  stored recipient address carries a trailing no-break space is processed instead of being counted as an error
  (`DatabaseMaintenances.FindEmailAddressForRawEmail`). The spec does not mention the address; it is the one place in the tool where such white
  space makes a message land in the "failed" count.
- Encrypted (Direct) messages are still skipped, as before. The summary still reports cleaned, unchanged and failed counts.
- Not changed: messages whose raw MIME can't be parsed at all still count as failed.

### v25_1-misc-dbm-email-invalid-chars: EmailmessageInvalidCharactersInBodyText

- New Check/Fix method with Open Dental's name, `[DbmMethodAttr(HasBreakDown=true)]`, so the office's `databasemaintenance` row carries over.
- Characters (spec proposal, unchanged): C0 control characters other than tab, CR and LF (U+0000 to U+0008, U+000B, U+000C, U+000E to U+001F),
  and unpaired UTF-16 surrogates.
- Check counts rows with SQL; Break down lists up to 100 messages (EmailMessageNum, PatNum, date); Fix reads 100 rows at a time by
  EmailMessageNum and rewrites `emailmessage.BodyText` only. `RawEmailIn` is never touched (it is the raw MIME, not what is displayed).
- The SQL prefilter matches the stored bytes with a binary REGEXP (`DbmTextCleaner.GetSqlHasControlChars`), so the result does not depend on
  the column collation. Unpaired surrogates can't be stored in UTF-8 columns, so in practice only the control characters are ever found; the C#
  rule still removes surrogates if a value has them.
- Values are written through a query parameter (`UpdateTextColumn`), not `POut.String`/CRUD, because those also replace some symbols
  (U+2600 to U+27FF and some emoji) and line breaks, which a character clean-up must not do.

### v25_2-tools-dbm-invalid-chars-gdi: Invalid Characters (GDI+)

- Tools-tab row "Invalid Characters (GDI+)" opens the new window `FormDatabaseMaintInvalidChars`: a multi-select grid of target columns (all
  selected at first), Scan (counts per column: values, characters, signed values), a preview of the first 50 affected values with each bad
  character shown as `[U+XXXX]`, and Fix with a back-up reminder and an OK/Cancel confirmation.
- Characters: the 25.1 set plus U+FFFE and U+FFFF. U+FFFD (the replacement character) is kept.
- Columns (spec list): patient LName, FName, MiddleI, Preferred, Address, Address2, City, State, Zip, AddrNote, FamFinUrgNote, MedUrgNote,
  ApptModNote; appointment Note and ProcDescript; procedure notes; commlog Note; definition ItemName; sheetfield FieldValue; wikipage PageContent.
- Signed values are reported and never changed: `procnote` rows with a Signature, `commlog` rows with a Signature, every field of a sheet that
  has a filled signature box, and signature boxes themselves.
- One `securitylog` entry under Setup per Fix, with totals and per-column counts. The DBM log file also gets the text.
- Replication: Fix is replication-unsafe. Opening the window runs the same prompt as the unsafe Check/Fix methods
  (`VerifyPermissionToRunReplicationUnsafeMethods`); when it is not passed, the window can scan but Fix stays disabled.
- Closing Database Maintenance after a fix invalidates the caches (definitions may have changed).
- Deviation: the spec says `procedurelog.Note (non-signed notes only)`. Procedure notes live in `procnote` (one row per edit, each with its own
  Signature), so the column is `procnote.Note` and "signed" means that row's Signature.
- INFERRED: rows are read and fixed in batches of 500 by primary key; the preview shows about 30 characters before the first bad character and
  at most 120 characters of the value.

### v25_3-tools-dbm-whitespace-tools: Notes With Too Much White Space on the Tools tab

- `[DbmMethodAttr]` removed from `DatabaseMaintenances.NotesWithTooMuchWhiteSpace`, so it leaves the Checks list and every automatic DBM run.
  Its logic is unchanged.
- New Tools row "Notes White Space": "Trims excessive blank lines and spaces from notes." It asks to continue, runs the method in Fix mode
  (verbose) and shows the log; the log is also saved to the DBM log file.
- An office `databasemaintenance` row for the method may remain; the window builds its lists from the methods that carry the attribute, so the
  row matches nothing (tested).
- `OpenDental/Misc/DbmInfo.cs` keeps its explanation entry for the method (not owned by this package; harmless, no longer reachable).

### v25_3-tools-dbm-employee-supervisor: EmployeeMissingSupervisor

- New `[DbmMethodAttr]` Check/Fix method. Check: "Employees with an invalid supervisor: N". Fix sets `employee.ReportsTo=0` for employees whose
  ReportsTo is not 0 and names no existing employee (negative values included). `ReportsTo=0` is valid.
- The rows are found with a self join and fixed by an explicit list of EmployeeNums (MySQL can't update a table that a subquery of the same
  statement reads).

### v25_3-tools-dbm-base64-docs and v25_4-tools-dbm-base64-documents: Create Documents From Base64 Notes

- Tools row "Base64 Documents". It counts the documents whose `document.Note` starts with `_rawBase64_:` (case-sensitive, as in the Images
  module), also says how many `_download_:` notes there are, asks to continue, converts them all and shows what was created and what was
  skipped (DocNum, PatNum, reason).
- Per document, the same steps as `ControlImageDisplay.DownloadDocumentNoteFile`: decode the bytes, write them to a temporary file with the
  stored extension, `ImageStore.Import` it into the patient's folder (or the database, for in-database storage), copy Description, DocCategory,
  ImgType, DateCreated, ToothNumbers, ProvNum and PrintHeading, and delete the placeholder row. `Import` makes the one ImageCreate log entry per
  document.
- Never overwrites: `ImageStore.Import` got an optional `doAvoidOverwrite` parameter (default false, so every existing caller behaves as
  before). With it, a name already taken by a file in the patient's folder or by another document of the patient becomes `name_2.ext`,
  `name_3.ext` and so on.
- `_download_:` notes are left alone (they would fetch a file from the internet; ground rule 6).
- Skipped and listed, with nothing changed: notes that don't decode, have no or an invalid extension (1 to 10 letters or digits) or no data;
  INFERRED: placeholders whose patient or image category is missing, and placeholders that are part of a mount (the spec's field list has no
  mount fields, and deleting the row would leave an empty mount item).
- The note format accepts the extension with or without its dot (`_rawBase64_:.jpg_...` and `_rawBase64_:jpg_...`); base64 may contain line
  breaks.
- Runs on the workstation (like the Images module), because it writes through this computer's A to Z path. Works for A to Z folder and in-database
  storage (the existing `ImageStore` paths).
- Our fork has no local REST API, so new placeholders are not created any more; the tool exists for databases that come from Open Dental.

## Files

New:
- `OpenDentBusiness/Misc/DbmTextCleaner.cs`
- `OpenDentBusiness/Data Interface/DatabaseMaintenances.Base64Documents.cs`, `.EmailText.cs`, `.EmployeeSupervisor.cs`, `.InvalidCharsGdi.cs`
- `OpenDental/Forms/FormDatabaseMaintInvalidChars.cs` and `.Designer.cs`
- `OpenDental/Forms/FormDatabaseMaintenance.Tools25.cs` (partial class: the three Tools-row handlers)
- `tests/BDBMTests/` (new test project), this note

Changed (small, local hooks):
- `OpenDentBusiness/Data Interface/DatabaseMaintenances.cs`: attribute removed from `NotesWithTooMuchWhiteSpace` (replaced by a summary
  comment); two lines in `CleanUpRawEmails` (inbox lookup through `FindEmailAddressForRawEmail`, `NormalizeRawEmailWhiteSpace` before
  `UpdateComparison`).
- `OpenDentBusiness/Imaging/ImageStore.cs`: optional `doAvoidOverwrite` on `Import(string,long,Patient)` and the private helper
  `SetFileNameNotInFolder`.
- `OpenDental/Forms/FormDatabaseMaintenance.cs`: three rows in `FillGridTools`.
- `OpenDentBusiness/OpenDentBusiness.csproj`, `OpenDental/OpenDental.csproj`: Compile entries for the new files.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the BDBMTests project.

Not changed: `EmailMessages.cs` and `FileAtoZ.cs` (listed as touchpoints, but nothing in them needed to change), table types, CRUD,
preferences, permissions, enums, migrations.

## Tests

`tests/BDBMTests` (MSTest v1, x86, .NET 4.8), 30 tests, on a scratch database `forkmig_<run id>_bdbm` (fresh 24.3.63 database upgraded by the
fork migration runner, dropped afterwards):
- `TextCleanerTests`: character classes, removal, counting, `[U+XXXX]` display, the SQL conditions, base64 note parsing (no database).
- `EmailTextDbTests`: Check/Breakdown/Fix of `EmailmessageInvalidCharactersInBodyText` (only BodyText changes, U+FFFE left for the GDI+ tool,
  batches past 100 rows); white space normalization; inbox lookup; `CleanUpRawEmails` on a synthetic raw message whose recipient address and
  subject carry a no-break space (no failure counted).
- `InvalidCharsGdiDbTests`: the column list, scan and fix over every column kind, signed procnote/commlog/sheet rows unchanged, other symbols
  stored exactly, one Setup log entry with the counts, chosen columns only, preview limit, batches past 500 rows.
- `WhiteSpaceToolDbTests`: the method is no longer a check, an office row for it is harmless, Fix mode still trims.
- `EmployeeSupervisorDbTests`: Check/Fix, valid and zero ReportsTo untouched, the method is a regular check.
- `Base64DocumentsDbTests`: in-database storage (fields copied, placeholders deleted, bad and `_download_:` notes untouched, one ImageCreate log
  per document), A to Z folder (file written, a stray file of the same name is kept and the new file gets `_2`, temporary file deleted), and
  skipped mount/no-patient placeholders.
- `WiringTests`: the three Tools rows, the handlers call the business methods in the right order, the window's Fix is gated by the replication
  check and a confirmation, the Raw Emails hooks, the project entries.

Related existing assemblies run: ImagingTests (ImageStore), EFormTests and RowHashTests (other DatabaseMaintenances partials). All passed.

## Interplay with packages built in parallel

- B-SHELL (B3) takes over `ImageStore.cs` after this package; the change here is one optional parameter and one private helper.
- M2-VERIFY checks `v25_4-tools-dbm-base64-documents` against this implementation; nothing should be left to do.
- B-TIME (`v25_3-manage-timeclock-reportsto`) filters the time clock by Reports To; `EmployeeMissingSupervisor` cleans the ReportsTo values that
  filter would otherwise show as a missing supervisor. No code dependency either way.
- Any other package adding a DBM method should do it in its own `DatabaseMaintenances.*.cs` partial file; this package edits only the three
  hunks listed above in `DatabaseMaintenances.cs`.

## Owner questions

1. Invalid Characters (GDI+): is the column list right for the office? Candidates left out: `patientnote` fields, `procedurelog` text fields,
   `wikipagehist`, eForm field values (`eformfield`). Adding one is a single line in `GetInvalidCharTargets`.
2. Removing U+200C/U+200D (zero-width non-joiner and joiner) in the Raw Emails tool follows the spec, but it changes some emoji sequences and
   some scripts (for example Persian). Keep it?
3. Base64 Documents: placeholders that are part of a mount are skipped (open them in the Images module). Should the tool also log an
   ImageDelete entry for each placeholder row it deletes? The Images module does not, so neither does the tool.
4. Not part of this package, noticed while editing the Tools tab: the "Tokens" row (X-Charge token validation) shows whether or not that
   integration is enabled, which ground rule 7 does not allow. Should it be hidden unless X-Charge is enabled (B-SHELL or a vendor package)?

## Untested

- The windows themselves (the GUI is not launched in this run): the Tools rows, `FormDatabaseMaintInvalidChars` layout and the replication
  prompt path are covered only by source wiring tests.
- Middle tier (`ClientMT`) paths, cloud image storage, Oracle.
- Base64 Documents with JPEG (EXIF rotation) and DICOM files: only text and PNG payloads were tested; those paths are the existing
  `ImageStore.Import` code.
- Raw Emails on real inbox data with attachments (only a synthetic single-part message was run).
