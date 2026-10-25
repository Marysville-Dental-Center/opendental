# Port note: V-PEARL1, Pearl request statuses

Backlog package V-PEARL1 (vendor-direct, milestone M1, wave slot B2), built on `port/v-pearl1` from `m1/integration-a2-patch`.

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-imaging-pearl-request-statuses` | built | The Pearl bridge writes and reads the 24.4 statuses ErrorUploading (5), ErrorProcessing (6) and NoResults (7). It sends an image again only after a failed upload, a timeout or a processing failure, and the Imaging module shows a separate status text for each case. |

Sources: the 24.4 database documentation diff (`pearlrequest.RequestStatus`, `EnumPearlStatus` 5 to 7, https://www.opendental.com/OpenDentalDiffDocumentation24-4.xml), the public Pearl process page (https://www.opendental.com/site/pearlaiprocess.html: the module shows "polling" and "timed out", and a timed-out image is sent again by hand), and the spec in [features-24.4.md](../specs/features-24.4.md). No Open Dental binary was observed, and no documentation text was copied.

## What changed

The rules live in `PearlRequests` (business layer), and the bridge and the Imaging module call them:

| Status | Polled any more? (`IsRequestHandled`) | Sent again? | How | Status text in the Imaging module |
|---|---|---|---|---|
| none (never sent) | – | yes | upload | panel hidden, as before |
| 0 Polling | no, being polled | no | – | Polling... (same as 24.3) |
| 1 Received | finished | no | – | annotation controls, as before |
| 2 Error | finished | no | – | Pearl rejected this image. |
| 3 TimedOut | no | yes | poll the same request ID again | Timed out. Can be sent again. |
| 4 Uploading | no, being uploaded | no | – | Uploading... (same as 24.3) |
| 5 ErrorUploading | finished | yes | upload again under a new request ID | Upload failed. Can be sent again. |
| 6 ErrorProcessing | finished | yes | poll the same request ID again | Processing failed. Can be sent again. |
| 7 NoResults | finished | no | – | No results for this image. |
| any other value | treated as Error | no | – | as Error |

- **Where the new statuses are written** (`Pearl.cs`):
  - The upload step fails after the request row was inserted, for example when the transfer to the address Pearl gave fails. The row becomes ErrorUploading (`PearlRequests.SetUploadFailed`). Before, it stayed Uploading forever, and the image could never be sent again.
  - The poll call fails. The request becomes ErrorProcessing. Before, it stayed Polling forever. The thread still ends and raises the existing Pearl alert.
  - Pearl finishes but returns no annotations and no tooth parts. The request becomes NoResults instead of Received (`PearlRequests.GetCompletedStatus`).
  - A rejected or deleted result stays Error, and a timeout stays TimedOut, as in 24.3.
- **Sending again** (`PearlRequests.PrepareToSend`, called by the sending thread):
  - TimedOut or ErrorProcessing: the status goes back to Polling and the same request ID is polled. The image isn't uploaded again, and `DateTSent` keeps the first send time.
  - ErrorUploading: the failed row is deleted, and the image is uploaded with a new request ID.
  - Anything else: the thread refuses with the 24.3 message.
  - `Pearl.SetupPearlForSendingSingle` (all send paths, mounts included) and the single-image toolbar path in `ControlImages` use `PearlRequests.CanSendToPearl`. Before, both allowed only TimedOut.
  - When the toolbar refuses, the message says why: still processing, already sent, rejected, or no results.
- **Re-poll fix** (`PearlApiClient.GetOneImageFromPearl`): the client now gets a token before polling. Before, polling ran without a token unless an upload had happened in the same session. A request polled again after a restart, for example a TimedOut row from yesterday, would then have failed and come back as ErrorProcessing every time.
- **Imaging module** (`ControlImages`):
  - The Pearl panel shows the status text of every non-Received status, not just Polling and Uploading.
  - The status label is wider and two lines tall (designer).
  - When a Received image is shown, the panel resets to the annotation controls. Before, a label left over from the previously shown image could stay. The new statuses would have made that 24.3 slip common.

## Files touched

- `OpenDentBusiness/Data Interface/PearlRequests.cs`: `IsRequestHandled` widened. New region with `GetKnownStatus`, `NeedsUpload`, `CanPollAgain`, `CanSendToPearl`, `HasStatusToShow`, `GetStatusText`, `GetCannotSendText`, `GetCompletedStatus`, `PrepareToSend`, `SetUploadFailed`.
- `OpenDentBusiness/Bridges/Pearl/Pearl.cs`: five small hooks (setup check, `PrepareToSend`, `SetUploadFailed`, ErrorProcessing on a failed poll, `GetCompletedStatus`).
- `OpenDentBusiness/Bridges/Pearl/PearlApiClient.cs`: token refresh before a poll (3 lines).
- `OpenDental/Main Modules/ControlImages.cs`: the send check and its message, and the panel status (17 lines). `ControlImages.Designer.cs`: the label's location and size. The original mixed CRLF/LF line endings are kept.
- `UnitTests/UnitTests/PearlTests.cs` (legacy project): expectations updated to the new statuses. It compiles in the local build, but the fork's test runner doesn't run that project.
- New: `tests/VPEARL1Tests/` (added to `OpenDental.sln` and `OpenDental.Local.slnf`), and this note.
- Not touched: `TableTypes/PearlRequest.cs`, which already has the 24.4 values (M1-02). No preferences, permissions, schema or migration steps changed, so the level range 2027002201-2027002299 is unused.

## INFERRED behaviours (not public; settle by the owner's testing)

1. **ErrorUploading only when a row exists.** A failure before the request row is inserted (token, upload address) leaves no row, as in 24.3, so the image can simply be sent again.
2. **Re-upload with a new request ID**, after deleting the failed row. Reusing the ID could collide with Pearl's duplicate-request check, which isn't documented.
3. **A failed poll ends polling as ErrorProcessing.** 24.3 also ended polling on the first failed poll, but left the row as Polling. Retrying a few times before giving up would be possible, but nothing public describes it.
4. **NoResults** = finished (`is_completed`) with no annotations and no tooth parts. A rejected (`is_rejected`) or deleted (`is_deleted`) result stays Error.
5. **The status texts and refusal messages are our own wording.** The public page names only "polling" and "timed out".
6. **On a mount**, any Received image shows the annotation controls. Otherwise the label shows the status of the first mount image that has one, and an in-progress image isn't preferred over a failed one.
7. **Unknown values** (from a newer Open Dental) load unchanged, count as finished, aren't sent again and show the Error text.

## Vendor-direct rules

- No new Pearl calls: only the four endpoints the 24.3 bridge already used (token, upload address, upload, result by request ID), with the office's own Client ID, Client Secret, Organization ID and Office ID from the Pearl program properties.
- Off by default and hidden until enabled: the Pearl program link ships disabled. The toolbar button and the panel appear only when it's enabled (`Programs.IsEnabled`).
- The tests run the real `PearlApiClient` on a fake HTTP layer (`APIRequest.Inst`), so nothing reaches the network, and use made-up settings in a scratch database.
- The Pearl Client Secret is still stored as plain text in `programproperty`, as Open Dental stored it (owner question 1).

## Interplay with packages built at the same time

- **V-PEARL2** (Pearl log level) owns `Pearl.cs`, `PearlApiClient.cs` and `FormProgramLinkEdit` too. My edits are single lines or short blocks inside the catch blocks of `SendOnThreadWorker` and `PollRequest`, the completed branch of `PollRequest`, and `GetOneImageFromPearl`. If V-PEARL2 adds log calls in the same catch blocks, the merge is a side-by-side keep-both.
- **B-SHELL and other imaging packages** editing `ControlImages.cs`: my hunks are inside `ToolBarProgram_Click` (single-document branch) and `ShouldPearlPanelShow` only.
- **B-IMG** (files taken over from it): nothing of B-IMG's is needed.

## Tests

`tests/VPEARL1Tests` (MSTest v1, x86, .NET 4.8):

- `PearlStatusRulesTests` (no database): the published numbers 0 to 7, unknown values read as Error, the handled, upload-again and poll-again rules for every status, distinct status texts, the refusal messages, NoResults detection, and `PrepareToSend` refusing what was already sent.
- `PearlSendDbTests` (scratch database, real client on the fake HTTP layer):
  - happy path to Received, with drawings
  - failed upload to ErrorUploading, then uploaded again with a new request ID
  - failure before the upload leaves no row
  - failed poll to ErrorProcessing, then polled again with the same ID and no upload
  - a TimedOut row from another session polled again, with a token first
  - NoResults and Error not sent again
  - a 202 "still processing" answer keeps polling
  - office rows with every status, plus 9, load and follow the rules
  - the sending thread refuses already-sent images without any HTTP call
  - `SetUploadFailed` touches only Uploading rows of that image
- `WiringTests`: `ControlImages` and `Pearl.cs` call the shared rules. The 24.3 TimedOut-only check and the enum name as the status text are gone.

Run: `VPEARL1Tests` (plus the always-included `NoHqTests`). `ImagingTests` also passes, since `ControlImages` was touched.

Not tested: the Imaging module itself (no GUI launch in this run). The panel layout, label wrapping and refusal message boxes need a look in the program. A real Pearl account wasn't used.

### Fix round r1 (`port/v-pearl1-r1`): ReleaseFlowTests scratch names

The review run of `port/v-pearl1` failed two tests in `ReleaseFlowTests.AdoptionDbTests`, an assembly this package doesn't touch: `OfficeCopy_25_3_48_IsAdopted` (Can't create database 'releaseflow_adoption'; database exists) and `Markers_AreWrittenAsPlainPreferenceRows` (Unknown database 'releaseflow_adoption').

- **Cause.** `AdoptionDbTests` and `ReleaseChannelDbTests` used the fixed names `releaseflow_adoption` and `releaseflow_release`. Every package of the single wave runs tests against the one local MariaDB at the same time. Another run dropped and recreated the same database between this run's `DROP DATABASE IF EXISTS` and `CREATE DATABASE`, then dropped it in the middle of the next test. The V-PEARL1 diff has no file under `tests/ReleaseFlowTests` and no code the adoption tests run. On this branch the assembly passed 63 of 63.
- **Fix** (test code only, in `tests/ReleaseFlowTests`):
  - `TestSupport.cs`: `ScratchDb.RunName(purpose)` gives a per-run name, `releaseflow_<UTC start yyMMddHHmmss>_<process id>_<random>_<purpose>`, which is the `TestDb.ScratchName` scheme of ForkMigrationTests.
  - `ScratchDb.Create` refuses a name that isn't this run's (`RequireRunName`).
  - `ScratchDb.Drop` ignores a name outside the per-run scheme, so the old fixed names are never dropped. An older checkout may still be testing in one.
  - The first `Create` of a run drops other runs' leftovers once they are more than 12 hours old (`DropStaleRunDatabases`).
  - `AdoptionDbTests.DB_NAME` and `ReleaseChannelDbTests.DB_NAME` are now `ScratchDb.RunName("adoption")` and `ScratchDb.RunName("release")`.
- **Tests:** new `ScratchDbNameTests`:
  - the name scheme and the 64-character limit
  - both database test classes use per-run names
  - fixed names and other runs' names are refused
  - `Create` leaves another run's database alone
  - the stale drop removes a 13-hour-old run's database and keeps a 30-minute-old one and a name outside the scheme
- **Interplay.** Other packages' fix rounds may have received the same failure and changed the same `ReleaseFlowTests` files. At the merge, keep one version of the per-run naming; any of them works if no fixed name is left.
- Targeted assemblies for this round: `VPEARL1Tests`, `ImagingTests`, `ReleaseFlowTests` (plus `NoHqTests`).

## Owner questions

1. **Pearl Client Secret.** Should it be stored encrypted (osd1$ via `VendorDirect.SecretValue`, masked and high security), like the other vendor-direct secrets? That needs `FormProgramLinkEdit` (owned by V-PEARL2 / V-BRIDGE1) to encrypt the property, and `PearlApiClient` to read it with `LegacyPolicy.AcceptPlaintext`. Proposed as a small follow-up after the merge.
2. **Stuck rows.** A request left Polling or Uploading by a workstation that closed mid-request stays that way, as in 24.3. Its image can't be sent to Pearl again; only deleting the image removes the row. Should such a row count as timed out after a while, for example 15 minutes without activity? That would also mean recording `DateTChecked` on every poll.
3. **Retries.** Is ending polling on the first failed poll (ErrorProcessing, sent again by hand) acceptable, or should the bridge retry a few times first?
