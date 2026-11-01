# V-EDS: EDS enhanced claim validation (port note)

Package V-EDS of [the port backlog](../specs/port-backlog.md) (wave C2, vendor-direct). Branch `port/v-eds`, cut from `m1/integration-a2-patch`; fix round
`port/v-eds-s1`; finished on `port/v-eds-t1`, which merges the integration head `port/int-7` (see Integration).
Spec: [features-26.1.md](../specs/features-26.1.md), item `v26_1-manage-eds-claim-validation`. Ground rules: [decoupling-plan.md](../decoupling-plan.md)
(rules 6 and 7) and [vendor-direct/README.md](../specs/vendor-direct/README.md).

## Status

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-manage-eds-claim-validation` | built (partner-gated part) | Spec rules 1 to 5 are built. In rule 2, the narrative check is built with the only public source of which procedures need a narrative: EDS's last answer for the claim (INFERRED 11). That answer needs the EDS attachment service, which is off by default, so in the default setup the narrative check never fires (see Deviations and owner question 7). **Partner-gated:** the extra checks that EDS itself runs in Open Dental 26.1. Neither the checks nor their endpoint are public (26.1 open question 4). They plug in at `IEdsEnhancedValidator`. Not run against EDS (no test account). |

What is built:

- **Rule 1, when the claim is validated.** It is validated when Edit Claim sends any claim, new or existing; when Edit Claim saves a **new** claim that is waiting to be sent; and for every EDS claim in a batch from the Send Claims window. The online part calls EDS's attachment validation (`EDS.ValidateClaim`, `POST https://web2.edsedi.com/api/validateattachment`). That is the only EDS validation call in the public material: the 24.3 manual page `edsattachmentservice` and the code we already have. It uses the office's own EDS login, the clearinghouse's Login ID and password. The password is already stored with CodeBase Secrets (`Secrets.ConcealClearinghouse`) and is sent only in the HTTP Basic header.
- **Rule 2, local EDS checks.** They run inside `Eclaims.GetMissingData` after the format validators and before any network call:
  - billing and treating provider: NPI check digit, plus taxonomy code present and in the 10-character format;
  - payer ID present (a warning, see INFERRED 3), and on the EDS payer list (as an electronic payer, and as a dental payer for a dental claim);
  - subscriber ID present;
  - no claim or procedure date in the future;
  - a narrative for each procedure that **EDS's last answer** for this claim said needs one (INFERRED 11). This is the only local narrative check, and it is limited: it has nothing to go on until EDS has answered for the claim on this workstation, so it needs the EDS attachment service (off by default).

  Errors go into Missing Info and warnings into Warnings, each starting with `EDS: `.

  An EDS error in Missing Info has the same effect as any other Missing Info, not only on sending. With **"Require claims to have no missing data"** (`PrefName.ClaimEditRequireNoMissingData`) on, `ClaimL.CreateClaim` refuses the new claim and deletes it. **Add Attachment** refuses too ("Cannot add attachments until missing data is fixed"), in Edit Claim (`OpenAttachmentForm`) and in the right-click attachment workflows of the Account and Treatment Plan modules. So only a problem that the claim must not go out with is an error. A problem that today's claims already go out with, such as a blank payer ID, is a warning.
- **Rule 3, one list of problems.** One validation window per claim, `FormEdsValidation`, shows every problem: what the format validators found (one row), the local EDS checks, and what EDS returned. Errors block sending. Warnings show **Send Anyway**. The window also has Edit Claim and Re-validate, and a re-validation marks problems that went away as Resolved. A batch shows one window, grouped by claim.
- **Rule 4, last result.** The claim's last validation is kept on the workstation and shown in two places: on Edit Claim's EDS tab (a new label) and under the attachment check in the claim attachment window's status box.
- **Rule 5, EDS unreachable.** A failed call, a timeout (10 s on save, 30 s on send), a refused login or an unreadable answer is never a silent block. The window shows "EDS could not be reached: ..." and the user chooses Send Anyway or Close. Saving is never blocked.

What is partner-gated: EDS's own "enhanced" checks. `IEdsEnhancedValidator` (`OpenDentBusiness/VendorDirect/Eds/EdsValidationModels.cs`) is where they plug in. It is null, so nothing is called. When it is set, its problems merge into the same list, and an exception it throws becomes a failed call (covered by a test with a fake). It needs EDS's documentation and the office's EDS test account.

### Off by default, hidden until enabled (ground rules 6 and 7)

- Only claims whose clinic-level clearinghouse has Comm Bridge = EDS get any of this. Other clearinghouses are unchanged, and so is EDS Medical (`EdsMedical`).
- EDS is called only when the office has turned on the EDS attachment service in the clearinghouse ("Allow attachment send", `clearinghouse.IsAttachmentSendAllowed`, off by default) **and** has entered its EDS Login ID and password. Otherwise only the local checks run, and the window says why.
- The new Edit Claim label is blank unless the claim goes to EDS. The new texts describe what the feature does: no links, logos or promotion (`EdsWiringTests.NewFiles_AreVendorNeutral_AndCallOnlyEds`).
- No schema change, no preference, no permission, no migration step. Permissions and audit are unchanged: ClaimSend, as before.

## Files

New:
- `OpenDentBusiness/VendorDirect/Eds/EdsValidationModels.cs`: issue and result types, send decisions, `IEdsEnhancedValidator`.
- `OpenDentBusiness/VendorDirect/Eds/EdsClaimSnapshot.cs`: the claim data the checks look at, read once from the database.
- `OpenDentBusiness/VendorDirect/Eds/EdsClaimChecks.cs`: the local checks (including the narrative check of INFERRED 11), NPI and taxonomy rules, reading EDS's answer.
- `OpenDentBusiness/VendorDirect/Eds/EdsClaimValidation.cs`: gates, the GetMissingData hook, local and remote validation, decisions, re-validation merge, last result, and the procedure codes EDS last said need a narrative.
- `OpenDental/Logic/EdsValidationL.cs`: Edit Claim send and save, and Send Claims batch flows (progress window, validation window).
- `OpenDental/Forms/FormEdsValidation.cs` and `.Designer.cs`: the validation window.
- `OpenDental/Forms/FormClaimsSend.Eds.cs`: a partial-class file holding the batch hook.
- `tests/VEDSTests/`: the test project (82 tests).

Changed (small, local hooks):
- `OpenDentBusiness/Eclaims/Eclaims.cs`: one call at the end of `GetMissingData`.
- `OpenDentBusiness/Eclaims/EDS.cs`: `ValidateClaim(claim,timeout)`, `PostJsonWithTimeout` (HttpClient, with a time limit and readable errors), `HttpHandlerForTests`, and the two timeouts. The existing calls (`ValidateClaim(claim)`, attachments, 270, 277, 835, payer list) are untouched.
- `OpenDental/Forms/FormClaimEdit.cs`: three insertions (Send, Save of a new claim, the EDS tab label). `FormClaimEdit.designer.cs`: one label on the EDS tab.
- `OpenDental/Forms/FormClaimsSend.cs`: one call after `ApplyXConnectResultsToSelection()`.
- `OpenDental/Forms/FormClaimAttachment.cs`: five lines in the EDS branch of `Shown`.
- `OpenDental/OpenDental.csproj` (four Compile entries), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).

## INFERRED behaviours

1. **The online check is the attachment validation.** The 26.1 note says only that EDS claims get stronger validation. The 24.3 manual says Send or Save on a new claim validates it with EDS, and the only EDS validation call known publicly is `validateattachment`. So that is the call made, now also on Send for existing claims and in batches.
2. **The online check needs the attachment service.** It runs only with "Allow attachment send" on, because the call belongs to EDS's attachment service, and turning it on is the office's opt-in.
3. **Severities.**
   - Errors: NPI check digit; taxonomy missing or not in the 10-character format; claim or procedure date in the future.
   - Warnings:
     - payer ID blank or under 3 characters. Before this package such claims went out: the X12 writer sends the paper-claim payer ID 06126 (`X837_5010.GetCarrierElectID`), and the X12 validator does not flag it. New carriers default to sending electronically with a blank payer ID. As an error it would stop those claims in Edit Claim and Send Claims, block Add Attachment, and make `ClaimEditRequireNoMissingData` delete new claims. As a warning it is listed in the validation window, with Send Anyway, until the owner answers question 4;
     - payer not on the EDS payer list, or listed as a paper payer or (for a dental claim) a medical payer;
     - an attachment or narrative that EDS says the payer requires while the claim has none, and the local narrative check that repeats EDS's last answer (INFERRED 11).
   - EDS unreachable: the user decides (rule 5).
4. **Reading EDS's answer.** `claimLevelResponse=true`, or a procedure with `response=true`, means "attachment required", as the 24.3 attachment window reads it. It counts as met once the claim has an EDS attachment ID or sent attachments. A required narrative counts as met by the claim's narrative or by sent attachments, because EDS sends narratives inside Narrative-type attachments. EDS's comments and attachment reasons go into the message.
5. **No duplicates with the X12 validator.** When the clearinghouse uses an X12 format, the checks that `X837_5010.Validate` already makes are left to it: NPI format, subscriber ID present, and an override taxonomy whose length is not 10. EDS adds what it does not check: the NPI check digit (CMS's published Luhn rule with the 80840 prefix), the taxonomy format, the payer, and the dates.
6. **Claim types.** Primary, secondary, other and preauthorization claims are validated; capitation claims are not. Preauthorizations skip the future-date checks.
7. **The last result is kept in memory**, per workstation, until the program closes. Persisting it would need a schema change or a new `EtransType` value, both foundation-owned. See the owner questions.
8. **Batch sends.** EDS is called at send time, not when the Send Claims window loads, so `ClaimsSendWindowValidatesOnLoad` does not hit the network; the local checks still run on load through `GetMissingData`. As with XConnect (V-DXC1):
   - one window covers every claim with problems, grouped by claim;
   - claims with errors are deselected and stay in the queue;
   - Send Anyway also sends the claims with warnings or an unreachable EDS;
   - Close sends only the claims without problems.

   For EDS claims, this replaces the batch-wide "Not allowed to send e-claims with missing information" refusal.
9. **Edit Claim Send.** For an EDS claim, the "Cannot send claim until missing/invalid data is fixed" message is replaced by the validation window, which lists the same text as one "Claim data" row, so there is one list (rule 3). When the user re-validated in the window and then chose Send, `EdsValidationL.ValidateBeforeSend` reads the claim's Missing Info and Warnings again (`RefreshMissingData`, finish round `port/v-eds-t1`). Edit Claim checks Missing Info right after, so a problem fixed in the meantime, for example by another workstation, no longer stops the send with the old message. A problem that is still there still stops it.
10. **Payer list.** The payer-list checks run only when EDS payer rows exist (`electid.CommBridge=EDS`, written by `EDS.GetPayerList`). Nothing in the fork calls `EDS.GetPayerList` today (Open Dental ran it from its service), so on most databases these checks stay silent.
11. **Which procedures need a narrative (rule 2).** No public source says which procedure codes need a narrative, and the fork has no such list: the procedure code table has no flag for it, and CDT descriptors may not be copied. EDS's attachment validation does say it, per procedure: a procedure answer with `response=true` and a Narrative document.
    - **What is remembered.** Each time EDS answers for a claim, `EdsClaimValidation` keeps the procedure codes EDS named, per claim, on this workstation and in memory, as with the last result (INFERRED 7). The next answer replaces them. EDS's procID is the procedure's own code (`ProcedureCodes.GetForClaim`), so a line matches on that code or on the code sent.
    - **The check.** `EdsClaimChecks.GetLocalIssues` gives a warning for each claim line with one of those codes while the claim has neither a narrative nor an attachment (the same test as EDS's own answer). The warning reaches Send Claims and Missing Info's Warnings without a network call, and stays listed when EDS is unreachable at the next send.
    - **No duplicates.** When EDS answers again, its fresh answer replaces the local warning.
    - **Default setup.** With the attachment service off, EDS is never asked, so the check stays silent.
    - **Middle Tier.** `GetMissingData` runs on the server, which cannot see the workstation's memory. There the check runs only in the validation window flows (`ValidateLocal` runs on the workstation).
12. **Claims sent, deleted or held while the batch window is open** (fix round `port/v-eds-s1`). The validation window opened from Send Claims has Edit Claim, and Edit Claim can send, delete or change the status of the claim; another workstation can too. `FormClaimsSend.RefreshAndValidateSelections` dropped sent and deleted claims before the window opened, and nothing looked again afterwards, so the batch could send a claim a second time (a duplicate e-claim) or crash on a deleted claim (`ClaimL.SendClaimSendQueueItems` reads `Claims.GetClaim(...).MedType`). Now:
    - **A result that cannot be sent.** `EdsValidationResult.NotSendableReason` (`IsNotSendable`) marks a claim that must not go out. `GetSendDecision` gives Block for it, whatever else the result says, so Send Anyway does not send it either. The result carries one error row (`ClaimNotSendable`) that says why, so the window lists it. It is not kept as the claim's last validation, and a re-validation that returns it shows the earlier problems neither as open nor as Resolved; the claim's patient, date and carrier stay in the row.
    - **Deleted claims.** `ValidateClaimNum` (Re-validate, and the re-validation after Edit Claim) and `ValidateLocal` return such a result for a deleted claim, in every window. Before, a deleted claim was "skipped", which counts as Send.
    - **Claims no longer waiting to send.** From the Send Claims window only, `ValidateClaimNum(...,isWaitingToSendRequired:true)` also returns such a result for a claim whose status is no longer W (waiting to send) or P (probably sent), the two statuses the Send Claims queue lists: sent (S), received (R), unsent (U) or on hold (H, I). Edit Claim's own Send does not apply this rule, because it may resend a sent claim on purpose.
    - **The final list is read again.** When the batch window closes (and on the path without a window), `EdsValidationL.ValidateBatchForSend` computes `ListClaimNumsAllowed` with `EdsClaimValidation.GetClaimNumsToSendForBatch`, which reads the claims from the database and drops every claim that was deleted or is no longer W/P, re-validated or not. Claims in that state when the batch starts are not validated at all. Both kinds go in the new `BatchOutcome.ListClaimNumsNotWaiting`.
    - **Send Claims.** `ApplyEdsResultsToSelection` deselects those claims, warns once ("...will not be sent again and will be removed from the grid"), and refreshes the grid (`FillGrid(true)`, which keeps the other selections), so they disappear from the queue as `RefreshAndValidateSelections` would show it.

## Deviations from the spec

- Rule 2 lists the checks without severities; they are chosen above (INFERRED 3). The payer-ID check is a warning, not an error, so claims that went out before still go out.
- Rule 2, "a narrative is present when a procedure code requires one": only partly local. The check runs locally in `GetMissingData`, but it knows which procedures need a narrative only from EDS's last answer for the claim (INFERRED 11). That answer needs the EDS attachment service, which is off by default, so by default the check never fires. A fully local check needs a list of codes that require a narrative, and no public source gives one (owner question 7).
- Rule 4 "store": stored in memory, not in the database (INFERRED 7).
- EDS's own 26.1 checks are not built (partner-gated). The rules are built around the documented attachment validation.

## Interplay with packages built in parallel

- **V-DXC2** (`FormClaimAttachment`, DentalXChange PDFs): this package only adds five lines to the EDS branch of `FormClaimAttachment_Shown`. EDS attachments still refuse PDFs (V-DXC2 rule 3), unchanged.
- **M2-CLAIMRCV**: the "send other outstanding claims" prompt (`ClaimL`, not owned here) gets the local EDS checks through `GetMissingData`, but does not call EDS. To add that, call `EdsValidationL.ValidateBatchForSend(listQueueItems,0)` before sending, then drop every claim in `ListClaimNumsNotWaiting` and every claim in `ListClaimNumsValidated` that is not in `ListClaimNumsAllowed`, as `FormClaimsSend.Eds.cs` does.
- **V-DXC1 / XConnect**: independent; it uses a different comm bridge. The EDS batch hook runs right after `ApplyXConnectResultsToSelection()`.
- **Other packages editing `FormClaimEdit.cs` or `FormClaimsSend.cs`** (B-CLAIM, V-PAYSUITE later): the edits are single insertions at three points: the Send path after `GetMissingData`, the save path after the ClaimConnect block, and `FillForm` after `FillDxcAttachmentState()`. The designer edit adds one control to `tabEDS`.
- **`OpenDental.sln` / `OpenDental.Local.slnf` / `OpenDental.csproj`**: additions at the end of the test-project list and next to the XConnect window entries; they may conflict textually with other packages' additions at merge time (keep both).

## Integration (`port/v-eds-t1`)

- **Merged:** `port/int-7` at `1763cc7ae`, with no conflicts. The `.sln` and `.slnf` keep `VEDSTests` next to `VDXC2Tests` and the other merged test projects.
- **V-DXC2 on the base.** It changed `FormClaimAttachment` (DentalXChange PDFs: `DxcPdfAttachment`, the DentalXChange branches of `FillGrid`, `GetSelectedImage` and saving). This package's five lines in the EDS branch of `FormClaimAttachment_Shown` sit apart from those changes. EDS attachments still refuse PDFs through `DxcPdfAttachment.IsPdfAccepted`.
- **B-PERIO on the base.** It changed three sizes in `FormClaimEdit.butAttachPerioHelper`, apart from this package's three insertions.
- **No duplicate helpers.** The base has no NPI check digit or taxonomy format helper (the X12 validators check only the NPI's length), so `EdsClaimChecks` keeps its own. The scratch database still comes from `tests/ForkMigrationTests/TestDb.cs`, as in the other test projects.
- **No fork migration step, schema or preference**, so the adoption tools (`tools/AdoptionValidation`) have nothing to expect from this package.
- **Not merged yet: M2-CLAIMRCV and B-CLAIM.** The hooks they need are described in "Interplay" above.

## Tests

`tests/VEDSTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 82 tests:

- **Pure:**
  - NPI check digit, cross-checked against CMS's formulation;
  - taxonomy format;
  - every local check, including the payer-ID warning (no Missing Info, Send Anyway offered) and the narrative check from EDS's last answer;
  - reading synthetic EDS answers, including the procedure codes that need a narrative;
  - send decisions, including a claim that cannot be sent (never sent, even with Send Anyway; not kept as the last result; which statuses count as waiting to send);
  - re-validation merge, including a claim that can no longer be sent (header kept, nothing marked Resolved);
  - the Missing Info round trip;
  - gates;
  - summaries and the last-result text.
- **Database** (a scratch `forkmig_*_veds` database: a fresh 24.3.63 database plus the fork migration runner, synthetic data only, dropped afterwards):
  - the snapshot from the database;
  - the `GetMissingData` hook, for EDS and for other clearinghouses, with and without X12;
  - a blank or 2-character payer ID: Missing Info stays empty, including the new-claim call that `ClaimL.CreateClaim` makes, and the problem is a warning;
  - the narrative check across validations. Silent before EDS answers. After an answer asks for a narrative, it is listed in `GetMissingData` Warnings and stays listed when EDS is down. A fresh answer lists it once. It is met by the claim's narrative and forgotten when EDS stops asking. It is silent with the attachment service off;
  - the EDS call through a scripted `HttpMessageHandler`:
    - the URL, the Basic header with the revealed office login, and the JSON body (password never in the body);
    - warnings from EDS;
    - HTTP 500 and 401;
    - timeout;
    - an unreadable or empty answer;
  - EDS not called when the attachment service is off, or when there are local errors;
  - non-EDS claims skipped;
  - an attachment already sent;
  - the enhanced-validator seam;
  - `ValidateClaimNum` with and without a clearinghouse override, and for a deleted claim (cannot be sent, not skipped);
  - a batch whose claims are sent, deleted, held or marked probably sent after validation (INFERRED 12): re-validated as the batch window does, and also without a re-validation, neither the sent, the deleted nor the held claim is in the final list (`GetClaimNumsToSendForBatch`, which `ListClaimNumsAllowed` comes from), even with Send Anyway; the P claim still goes; Edit Claim's own re-validation still allows resending a sent claim.
- **Source wiring:**
  - the hooks sit where rule 1 needs them, and Edit Claim's Send reads Missing Info again after a re-validation in the window;
  - the batch window re-validates with the waiting-to-send rule, every batch path reads the claims again, and Send Claims deselects and drops the claims that are no longer waiting;
  - the new files are in the projects;
  - the new files have no links or promotional words;
  - `EDS.cs` talks only to `web2.edsedi.com`.

Related existing assembly: `VendorDirectTests` (it covers `EDS.cs`/`Eclaims.cs` neighbours: DentalXChange, XConnect). Host check: `tools/Test-NoHqHosts.ps1` passes.

Not tested: the windows themselves (FormEdsValidation, the Edit Claim and Send Claims flows) were not run, because the GUI is not launched in package work; the source-wiring tests cover where they are called. EDS was never called: there is no EDS test account. Rerun the tests against EDS's test environment once the office has one.

## Owner questions

1. Can the office get EDS's documentation (and a test login) for the validation EDS added for Open Dental 26.1? Until then, those checks are partner-gated.
2. Should the online check also run when the EDS attachment service is off? Today it does not, because `validateattachment` belongs to that service.
3. Nothing calls `EDS.GetPayerList` in the fork, so the payer-list checks rarely have a list to compare with. Add an "Update EDS payer list" action? `FormClearinghouseEdit` is not owned by this package.
4. Severities: a blank or too-short payer ID is a **warning** today, so these claims still go out with the paper-claim ID 06126 as before. Should it be an error? An error goes into Missing Info. That stops the claim in Edit Claim and Send Claims and blocks Add Attachment. With "Require claims to have no missing data" on, it also makes claim creation refuse and delete the new claim.
5. Should the last validation result survive a restart? That needs a new `EtransType` value or a column (foundation-owned).
6. Send Claims window: keep XConnect's rule that Close still sends the claims without problems, or make Close cancel the whole send?
7. Narratives (rule 2): no public source says which procedure codes need a narrative, so the local check only repeats EDS's last answer for the claim, and with the attachment service off it never fires. How should this be filled in? Options:
   - the office keeps its own list, for example a new procedure-code flag or a preference (foundation-owned schema);
   - EDS's answers are remembered per payer and procedure code across claims (and persisted, see question 5);
   - the check stays as it is.

## Integration (branch `port/int-9`)

- Merged onto `port/int-8` (which adds B-CLAIM) after M2-TERM and M2-SHEETS, with no conflicts: `FormClaimEdit.cs` keeps this package's three insertions beside B-CLAIM's Medical EOB hook, and M2-CLAIMRCV (merged next) changes other lines of it.
- **B-CLAIM**: its Medical EOB attachments are ordinary `claimattach` rows, so the EDS checks and the attachment window see them like any other attachment. Nothing to connect.
- **M2-CLAIMRCV** (integration commit): the "Send claim(s) now" choice of the prompt for the next insurance's claims (`ClaimL.PromptForPrimaryOrSecondaryClaim`) now validates the claims that go to EDS before sending, as the Interplay section above describes. New `EdsValidationL.FilterQueueItemsForSend`, called right after the XConnect filter (`FilterQueueItemsByXConnect`) and before `SendClaimSendQueueItems`:
  - it runs `ValidateBatchForSend(listQueueItems,0)` (each claim's own clearinghouse) and keeps the claims that do not go to EDS plus the EDS claims allowed; claims with errors, a cancelled validation and claims no longer waiting to send (`ListClaimNumsNotWaiting`) are not sent;
  - the prompt has set the claims to Waiting to Send just before, so they pass the waiting-to-send rule; the window opens as from Send Claims (modal, Edit Claim opens the claim);
  - not inside `SendClaimSendQueueItems`, so the Send Claims window does not validate twice;
  - known limits: the prompt's queue items carry no Missing Info yet, so format problems stay with the 24.3 refusal in `SendClaimSendQueueItems` ("Not allowed to send e-claims with missing information") instead of the window's "Claim data" row; and the not-sendable row's advice ("Refresh the Send Claims window") is worded for the Send Claims window.
  - Checked by `EdsWiringTests.NextInsurancePrompt_ValidatesEdsClaimsBeforeSending`.
- **One claim status text** (integration commit): M2-CLAIMRCV added `ClaimReceiveRelated.GetClaimStatusText` for the 26.1 "Hold Until Prior Received" text. The "no longer waiting to send" message (`EdsClaimValidation.GetClaimStatusText`) now takes its text from it, so a held claim reads the same there as in Edit Claim (`EdsDbTests` checks it).
- No fork migration step, schema or row change, so the adoption tools need nothing new. On the merged head: one full build of `OpenDental.Local.slnf` through the build lane (0 errors), `run-tests -Mode Full` (69 assemblies, 3642 tests, none failed; the 2 skipped are M201Tests' published-document checks, skipped as on earlier heads) and `tools/Test-NoHqHosts.ps1` (passed). `VEDSTests` 83 of 83, `VendorDirectTests` 873.
