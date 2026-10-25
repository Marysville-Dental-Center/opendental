# V-EDS: EDS enhanced claim validation (port note)

Package V-EDS of [the port backlog](../specs/port-backlog.md) (wave C2, vendor-direct). Branch `port/v-eds`, cut from `m1/integration-a2-patch`.
Spec: [features-26.1.md](../specs/features-26.1.md), item `v26_1-manage-eds-claim-validation`. Ground rules: [decoupling-plan.md](../decoupling-plan.md)
(rules 6 and 7) and [vendor-direct/README.md](../specs/vendor-direct/README.md).

## Status

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-manage-eds-claim-validation` | **partial** | Spec rules 1 to 5 are built. The extra checks that EDS itself runs in Open Dental 26.1 are **partner-gated**: neither the checks nor the endpoint are public (26.1 open question 4). |

What is built:

- **Rule 1, when the claim is validated.** It is validated when Edit Claim sends any claim, new or existing; when Edit Claim saves a **new** claim that is waiting to be sent; and for every EDS claim in a batch from the Send Claims window. The online part calls EDS's attachment validation (`EDS.ValidateClaim`, `POST https://web2.edsedi.com/api/validateattachment`). That is the only EDS validation call in the public material: the 24.3 manual page `edsattachmentservice` and the code we already have. It uses the office's own EDS login, the clearinghouse's Login ID and password. The password is already stored with CodeBase Secrets (`Secrets.ConcealClearinghouse`) and is sent only in the HTTP Basic header.
- **Rule 2, local EDS checks.** They run inside `Eclaims.GetMissingData` after the format validators and before any network call:
  - billing and treating provider: NPI check digit, plus taxonomy code present and in the 10-character format;
  - payer ID present, and on the EDS payer list (as an electronic payer, and as a dental payer for a dental claim);
  - subscriber ID present;
  - no claim or procedure date in the future;
  - a narrative where EDS says a procedure needs one.

  Errors go into Missing Info and warnings into Warnings, each starting with `EDS: `.
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
- `OpenDentBusiness/VendorDirect/Eds/EdsClaimChecks.cs`: the local checks, NPI and taxonomy rules, reading EDS's answer.
- `OpenDentBusiness/VendorDirect/Eds/EdsClaimValidation.cs`: gates, the GetMissingData hook, local and remote validation, decisions, re-validation merge, last result.
- `OpenDental/Logic/EdsValidationL.cs`: Edit Claim send and save, and Send Claims batch flows (progress window, validation window).
- `OpenDental/Forms/FormEdsValidation.cs` and `.Designer.cs`: the validation window.
- `OpenDental/Forms/FormClaimsSend.Eds.cs`: a partial-class file holding the batch hook.
- `tests/VEDSTests/`: the test project (71 tests).

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
   - Errors: NPI check digit; taxonomy missing or not in the 10-character format; payer ID blank or under 3 characters (the X12 writer would send the paper-claim ID); claim or procedure date in the future.
   - Warnings: payer not on the EDS payer list, or listed as a paper payer or (for a dental claim) a medical payer; an attachment or narrative that EDS says the payer requires while the claim has none.
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
9. **Edit Claim Send.** For an EDS claim, the "Cannot send claim until missing/invalid data is fixed" message is replaced by the validation window, which lists the same text as one "Claim data" row, so there is one list (rule 3).
10. **Payer list.** The payer-list checks run only when EDS payer rows exist (`electid.CommBridge=EDS`, written by `EDS.GetPayerList`). Nothing in the fork calls `EDS.GetPayerList` today (Open Dental ran it from its service), so on most databases these checks stay silent.

## Deviations from the spec

- Rule 2 lists the checks without severities; they are chosen above (INFERRED 3).
- Rule 4 "store": stored in memory, not in the database (INFERRED 7).
- EDS's own 26.1 checks are not built (partner-gated). The rules are built around the documented attachment validation.

## Interplay with packages built in parallel

- **V-DXC2** (`FormClaimAttachment`, DentalXChange PDFs): this package only adds five lines to the EDS branch of `FormClaimAttachment_Shown`. EDS attachments still refuse PDFs (V-DXC2 rule 3), unchanged.
- **M2-CLAIMRCV**: the "send other outstanding claims" prompt (`ClaimL`, not owned here) gets the local EDS checks through `GetMissingData`, but does not call EDS. To add that, call `EdsValidationL.ValidateBatchForSend(listQueueItems,0)` before sending and keep only `ListClaimNumsAllowed`, as `FormClaimsSend.Eds.cs` does.
- **V-DXC1 / XConnect**: independent; it uses a different comm bridge. The EDS batch hook runs right after `ApplyXConnectResultsToSelection()`.
- **Other packages editing `FormClaimEdit.cs` or `FormClaimsSend.cs`** (B-CLAIM, V-PAYSUITE later): the edits are single insertions at three points: the Send path after `GetMissingData`, the save path after the ClaimConnect block, and `FillForm` after `FillDxcAttachmentState()`. The designer edit adds one control to `tabEDS`.
- **`OpenDental.sln` / `OpenDental.Local.slnf` / `OpenDental.csproj`**: additions at the end of the test-project list and next to the XConnect window entries; they may conflict textually with other packages' additions at merge time (keep both).

## Tests

`tests/VEDSTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 71 tests:

- **Pure:**
  - NPI check digit, cross-checked against CMS's formulation;
  - taxonomy format;
  - every local check;
  - reading synthetic EDS answers;
  - send decisions;
  - re-validation merge;
  - the Missing Info round trip;
  - gates;
  - summaries and the last-result text.
- **Database** (a scratch `forkmig_*_veds` database: a fresh 24.3.63 database plus the fork migration runner, synthetic data only, dropped afterwards):
  - the snapshot from the database;
  - the `GetMissingData` hook, for EDS and for other clearinghouses, with and without X12;
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
  - `ValidateClaimNum` with and without a clearinghouse override.
- **Source wiring:**
  - the hooks sit where rule 1 needs them;
  - the new files are in the projects;
  - the new files have no links or promotional words;
  - `EDS.cs` talks only to `web2.edsedi.com`.

Related existing assembly: `VendorDirectTests` (it covers `EDS.cs`/`Eclaims.cs` neighbours: DentalXChange, XConnect). Host check: `tools/Test-NoHqHosts.ps1` passes.

Not tested: the windows themselves (FormEdsValidation, the Edit Claim and Send Claims flows) were not run, because the GUI is not launched in package work; the source-wiring tests cover where they are called. EDS was never called: there is no EDS test account. Rerun the tests against EDS's test environment once the office has one.

## Owner questions

1. Can the office get EDS's documentation (and a test login) for the validation EDS added for Open Dental 26.1? Until then, those checks are partner-gated.
2. Should the online check also run when the EDS attachment service is off? Today it does not, because `validateattachment` belongs to that service.
3. Nothing calls `EDS.GetPayerList` in the fork, so the payer-list checks rarely have a list to compare with. Add an "Update EDS payer list" action? `FormClearinghouseEdit` is not owned by this package.
4. Severities: should a blank or too-short payer ID be an error (today) or a warning (claims go out with the paper-claim ID)?
5. Should the last validation result survive a restart? That needs a new `EtransType` value or a column (foundation-owned).
6. Send Claims window: keep XConnect's rule that Close still sends the claims without problems, or make Close cancel the whole send?
