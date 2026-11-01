# Port note: V-PEARL2, Pearl troubleshooting log level

Backlog package V-PEARL2 (M2, wave C1, vendor slot). Branch `port/v-pearl2`, cut from `m1/integration-a2-patch`.

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-imaging-pearl-logging` | built | A "Log Level" property on the Pearl program link controls how much the Pearl bridge writes to a local log in `Logger\Pearl`. |

Sources: the 26.1 release notes (one line: new log settings for diagnosing the Pearl bridge), the public Pearl bridge page (bridgepearlai.html: the property is named "Log Level", defaults to 0, and is set to 2 only while troubleshooting) and the public Loggers page (the Pearl log lives in `Logger\Pearl` under the program folder). Nothing else about the behaviour is public. Everything below that these pages don't state is marked INFERRED.

## What it does

- **Setup.** Setup > Program Links > Pearl shows a "Log Level" row. Double-clicking it offers three choices instead of a free-text box: 0 (errors only, the default), 1 (errors, uploads and results) and 2 (verbose, for troubleshooting). The row is practice-wide (`programproperty`, ClinicNum 0), like the bridge's other rows.
- **The row.** A database from Open Dental 26.1 already has it. A database from 24.3 or 25.3 doesn't. The row is inserted, with value 0, the first time someone opens the Pearl program link window. Until then a missing row reads as 0, which is the same thing as the default.
- **The log.** Lines go through `CodeBase.Logger` into its dated files in the `Pearl` subfolder (`Logger\Pearl\yy-MM-dd\yy-MM-dd (001).txt`). Logger adds the time stamp and runs its usual age-based cleanup. Nothing is sent anywhere. Line format: time stamp, level (`ERROR`, `INFO`, `VERBOSE`), message, then `document N, request <id>` when they are known, separated by tabs.
- **What each level writes.** The level is read from the program link cache on every line, so a change takes effect on the next line without a restart.
  - **0, errors.** These are always written:
    - an HTTP call that failed: status 300 or higher, no connection, or a timeout. The line gives the endpoint, the status and the time taken.
    - a result that Pearl rejected or deleted. This is the failed-result case that the 24.3 code leaves as a TODO in `Pearl.cs`.
    - why the bridge's worker for an image stopped: a failed upload, a failed check for results, an image already sent, or the 10-minute wait running out.
  - **1, information.** Errors, plus one line per uploaded image (with the time taken) and one per completed result (how many annotations and tooth parts, after how many checks and how long).
  - **2, verbose.** All of the above, plus:
    - every API call: method, endpoint path, HTTP status and time taken
    - the start of each upload, with the image size in pixels
    - each check for results while Pearl is still working: the check number and the seconds since the first check
- **Never logged.** No headers or bodies are read at all, so none of these reach the log:
  - the client secret (in the token request)
  - the access token (in its response, and in the Authorization header)
  - patient fields (in the image request)
  - image bytes

  The presigned upload URL is never written either: upload calls are logged as "presigned image upload". As a second layer, every line is scrubbed before it is written. The configured client secret (stored plain or as `osd1$`), Bearer/Basic credentials, JWTs, secret-looking key/value pairs (`client_secret`, `access_token`, `signature`, `policy`, `x-amz-*`, ...), S3 signature elements in error documents and long opaque strings become `[redacted]`. URLs lose their query string, storage (`amazonaws.com`) URLs are dropped entirely, and lines are kept on one line and cut at 2000 characters. Logging never throws, and it never changes what the bridge does.

## Files

New:
- `OpenDentBusiness/VendorDirect/Pearl/PearlLog.cs` holds the property name and default, the level parsing, `EnsureLogLevelProperty`, the writer, the scope (document and request, as an `AsyncLocal`, so it also reaches the HTTP handler that `APIRequest` runs on a task), the scrubber and the handler factory.
- `OpenDentBusiness/VendorDirect/Pearl/PearlLoggingHandler.cs` is a `DelegatingHandler` that writes one line per HTTP call.
- `OpenDentBusiness/VendorDirect/Pearl/PearlApiClientLogging.cs` wraps `IPearlApiClient` and writes the per-image lines: uploads, results, failed results and polling progress.
- `tests/VPEARL2Tests/` holds the test project.
- `docs/port-notes/V-PEARL2.md` is this file.

The new business files sit in the `VendorDirect` folder only because the business project compiles that folder by wildcard, so no `.csproj` edit was needed. They use the bridge's own namespace, `OpenDentBusiness.Pearl`, so the bridge files needed no new `using` lines.

Edited. Each change is a hook of one line, or a few lines:
- `OpenDentBusiness/Bridges/Pearl/PearlApiClient.cs`, 4 lines:
  - `Inst` wraps the real client in `PearlApiClientLogging`.
  - The three `HttpClient`s are built on `PearlLog.CreateHttpHandler`: the API client, the client recreated on token refresh, and the presigned upload client. They use the same `HttpClientHandler` transport as before.
- `OpenDentBusiness/Bridges/Pearl/Pearl.cs`, 1 line: the worker's exception handler calls `PearlLog.LogWorkerFailure` before it makes the user's alert.
- `OpenDental/Forms/FormProgramLinkEdit.cs`, 11 inserted lines:
  - `Load` calls `PearlLog.EnsureLogLevelProperty(ProgramCur)`. For any program other than Pearl it does nothing.
  - `gridMain_CellDoubleClick` gets a `case nameof(ProgramName.Pearl)` that shows the three levels in the existing `ShowComboBoxForProgramProperty` picker.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the test project.

Not changed: `CodeBase/Utilities/Logger.cs`. The package owns it, but nothing in it needed to change. No preference, enum, permission, table, CRUD or fork migration step was added.

## INFERRED behaviour

- The meaning of 1. The public page documents only 0 and 2. We use the fork's `CodeBase.LogLevel` numbers (0 Error, 1 Information, 2 Verbose) and the split in the 26.1 spec.
- Values above 2 read as verbose. Blank, negative or non-numeric values read as 0.
- Exactly which events each level writes, the line format and the wording of the lines.
- The level is practice-wide, not per workstation, as a `programproperty` row with ClinicNum 0 implies.
- Error-level lines include failed HTTP statuses (with endpoint and status) as well as the worker's final failure. One failed upload therefore gives two error lines: the call, and the outcome.
- A request whose result never comes is forgotten by the logger an hour after its upload or first check. Polling stops at 10 minutes.
- The three display texts in the picker, and the picker itself. Open Dental may use a free-text box.

## Deviations from the spec

- **No migration step.** The spec says "add the property with default 0 in a ConvertDatabases step". The legacy chain is frozen, and the assigned fork migration range (2027002501 to 2027002599) is not a valid `yyyyMMddNN` level, because the month would be 00. `ForkMigrationRunner.ValidateStep` refuses such a level, and `GetSteps` would then refuse every step. B-PRINT ran into the same problem. Instead, the row is inserted at runtime with `VendorCredentials.EnsureSetting`, which is the vendor-direct rule (docs/specs/vendor-direct/README.md). A missing row behaves exactly like the default.
- **The failed-result TODO (spec rule 4) is logged without editing the TODO line in `Pearl.cs`.** The wrapping client logs a rejected or deleted result as soon as it comes back, before `Pearl.cs` marks the request. V-PEARL1 is rewriting that branch in parallel.
- **Debug builds log less.** In a Debug build the bridge simulates Pearl and never calls the API (`ODBuild.IsDebug()` in `Pearl.cs`, unchanged), so a Debug build writes only worker failures. Release builds write everything described above.

## Interplay with packages built in parallel

- **V-PEARL1** (Pearl request statuses, M1 B2) edits `Pearl.cs` (around the resend check ~120, the timeout ~222 and the result branch ~370), `PearlApiClient.cs`, `PearlRequests.cs` and `ControlImages.cs`. Our hunks don't overlap with those areas: `Pearl.cs` line 152 (the exception handler) and `PearlApiClient.cs` lines 13, 17, 21 and 34 (the field initializers and the refreshed client). They should merge cleanly. Expected interplay with V-PEARL1's changes:
  - Its new statuses need nothing from us. A NoResults result logs as "Results received: 0 annotations, 0 tooth parts". Rejected or deleted results log as errors, whatever status V-PEARL1 stores.
  - If V-PEARL1 keeps polling after a failed check, the logger keeps that request's check count and document.
  - If V-PEARL1 re-polls a timed-out request, the logger reads the document of a request it did not upload once from `pearlrequest` (`PearlRequests.GetOneByRequestId`), so the lines still name the document.
  - `UnitTests/PearlTests` replaces `PearlApiClient.Inst` with the mock, and the legacy unit test mode turns Logger off, so those tests are unaffected.
- **V-BRIDGE1** and **B-SHELL** (this run) also edit `FormProgramLinkEdit.cs`. Our changes are two insertions, in `Load` (after the path override lines) and in the double-click `switch` (after the PDMP case). If V-BRIDGE1 adds a case at the same place, the merge needs a trivial hand resolution: keep both cases.
- **V-BRIDGE2** (C3) takes `FormProgramLinkEdit.cs` over from this package later. Nothing to hand over.

## Vendor-direct rules

- The integration stays off by default. The Pearl program link keeps `Enabled=0`, and the log only has something to say once the office enables the bridge and sends images. The "Log Level" row appears in the program link's own grid, nowhere else.
- No new credentials. The office's existing Pearl client ID and secret are used unchanged.
  - The 24.3 bridge stores the client secret as typed (plain text in `programproperty`), and this package doesn't change that storage.
  - The log never contains the secret in either form.
  - Moving it to `osd1$` is listed under owner questions.
- No call reaches Pearl or the storage service in the tests: HTTP goes to a scripted fake handler, `APIRequest` is a fake where needed, and the API client is the bridge's mock.
- No vendor text, logos or links were added.
- Privacy: lines carry document numbers and request IDs, never patient names. Error text that Pearl returns is logged after redaction, so it could contain whatever Pearl echoes back. The log stays on the workstation.

## Tests

`tests/VPEARL2Tests`: 55 tests, MSTest v1, x86, .NET 4.8. Run them with the shared runner (`-Include VPEARL2Tests`); NoHqTests is always added.
- `PearlLogLevelTests` covers:
  - value parsing, and that the numbers follow `CodeBase.LogLevel`
  - what levels 0, 1 and 2 write, and that a change applies to the next line
  - that an unreadable property, or a writer that throws, never throws
  - the document/request scope
  - worker failures
- `PearlLogScrubTests`:
  - the client secret is redacted, plain and `osd1$`; a too-short secret isn't
  - tokens and JWTs, presigned URLs, S3 signature XML and form fields, URLs with query or user info, and long opaque strings are redacted
  - lines stay on one line and are cut at the limit
- `PearlLoggingHandlerTests`, with a fake HTTP handler:
  - success is verbose and failed statuses are errors; transport errors and timeouts are errors and still throw
  - presigned uploads never show their address
  - headers and bodies are never logged
  - the scope reaches the handler through `APIRequest`'s `Task.Run`
- `PearlApiClientLoggingTests`, with the bridge's mock:
  - uploads at each level, upload failures, and a missing request ID
  - completed results with counts and progress, polling progress, and rejected or deleted results as errors
  - failed checks, the document lookup for a request not uploaded here, expiry of old requests, and pass-through members
- `PearlWiringTests`:
  - `PearlApiClient.Inst` is the logging client over the real one
  - all three of the bridge's `HttpClient`s run on the logging handler, including the one made on token refresh (through a fake `APIRequest`)
  - source checks: the worker hook sits before the alert, and the setup window inserts the row before filling the grid and offers the picker
- `PearlLoggerFolderTests`: the real `Logger` writes to `Logger\Pearl\yy-MM-dd`, with time stamps, in a temporary folder.
- `PearlLogLevelDbTests`, on a scratch MariaDB database with a unique per-run name (`TestDb`), dropped afterwards:
  - a missing row reads as 0, and the row is inserted once, also with a stale cache
  - stored levels are read through the cache
  - the stored client secret never reaches a line
  - an older request's document is found in `pearlrequest`

Related existing assemblies: none in the fork's suite cover these files. `UnitTests/PearlTests` (the legacy UnitTests project, not part of the fork's runner) exercises the same worker with the mock and is unaffected.

## Not tested

- The Program Links window itself (the row appearing and the picker), because the GUI may not be launched here. It is covered only by the source-shape test and the business-layer tests.
- Calls against Pearl's real API. There are no credentials and none may be used, and the HTTP shape is unchanged.
- Logger's age-based cleanup of old Pearl files. That is existing Logger code, which this package doesn't change.

## Owner questions

1. The fork migration level range assigned to this package (2027002501 to 2027002599) can't be used: the runner requires `yyyyMMddNN` from 2026 on. Future packages that need a step need a range of real dates, such as 2026102551 to 2026102599.
2. The Pearl client secret is still stored as plain text, as Open Dental stored it. Should a follow-up store it as `osd1$`? That would mean `SecretValue` with `LegacyPolicy.AcceptPlaintext`, a masked high-security row, and re-encryption on the next save, like the other vendor-direct secrets.
3. Are the INFERRED meanings acceptable? These are level 1 (uploads and results), values above 2 read as verbose, and a practice-wide setting.
4. Is it acceptable that error text Pearl returns (after redaction) goes into the local log? It could echo request details. The alternative is to log only the HTTP status for failed calls.
