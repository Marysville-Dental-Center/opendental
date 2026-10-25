# Port note: B-CLAIM (claims, claim-procedure audit and permissions)

Branch `port/b-claim`, cut from `m1/integration-a2-patch`. Backlog section: [port-backlog.md, B-CLAIM](../specs/port-backlog.md). Tests: `tests/BCLAIMTests` (assembly `BCLAIMTests`).

Sources used: Open Dental's public release notes for 25.1, 25.2 and 25.3, the public manual pages on claim receiving and medical insurance, and the published database docs (25.1 to 25.3 diff XMLs). Nothing was decompiled, observed or copied. Where behaviour is not public it is marked **INFERRED**.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v25_1-misc-claimproc-override-audit` | built | Audit entries for Ins Est Total override and Allowed override changes, from every save path the fork can hook. |
| `v25_2-account-medical-eob-prompt` | partial | Prompt, attach logic and preference checkbox are built and tested. They stay off until the preference name is confirmed and registered (see below). |
| `v25_3-security-inswriteoffedit-clarify` | built | The wording came from M1-02. The 24.3 create, edit and delete paths were checked and already enforce the rule. Added one shared date rule and an explicit check when Delete is clicked. |
| `v25_3-security-claimproc-billedtoins` | built | Code Sent is guarded by permission 252, the same way as Fee Billed. Changes to either field are logged under 252. |
| `v25_1-account-canada-attachment-history` | not built | Excluded by owner decision (the Canadian claim networks were removed). |
| `v25_1-canada-etrans-counters-uncapped` | not built | Excluded by owner decision (the Canadian claim networks were removed). |
| `v25_1-imaging-canada-claim-response-titles` | not built | Excluded by owner decision (the Canadian claim networks were removed). |

## v25_1-misc-claimproc-override-audit

A save that changes `claimproc.InsEstTotalOverride` or `claimproc.AllowedOverride` writes one audit entry per claimproc. The entry shows the old and new values, with -1 shown as "blank". FKey is the ClaimProcNum. The text names the procedure code, the tooth and the carrier (or "Total payment").

- Logic: `ClaimProcs.GetOverrideChangeText`, `CreateAuditTrailEntryForOverrides` and `CreateAuditTrailEntriesForOverrides` in the new partial file `OpenDentBusiness/Data Interface/ClaimProcs.AuditTrail25.cs`.
- Hooks, one or two lines each:
  - `FormClaimProc.butSave_Click`, only when the window saves the claimproc itself (`_doSaveToDb`). This covers estimates edited from the Procedure window, the Treatment Plan module (through the Procedure window), Edit Claim and the institutional claim window.
  - `ClaimProcs.CreateAuditTrailEntryForClaimProcPayment`. It already runs when the payment windows save (`FormClaimPayTotal`, manual ERA posting in `FormEtrans835ClaimPay`) and on "auto receive claims with no assignment of benefits". It covers allowed amounts typed in those grids, plus edits made in Edit Claim Procedure opened from them (those windows pass `doSaveToDb=false`, so each claimproc gets one entry).
  - `ClaimProcs.SetInsEstTotalOverride`: a received preauthorization's estimates are copied into the procedure's estimate override.
  - `FormClaimEdit`, preauthorization payment path: overrides edited in the preauth payment window.
  - `Etranss.TryImportEraClaimData`, automatic branch: allowed overrides that ERA auto-processing clears.
- **INFERRED** permission type (Open Dental's is not public; the published 25.1 docs add no permission for it). As the spec proposes: Claim Procedure Received Edit (125) for Received and Supplemental claimprocs, and Proc Edit (100, logging only) for every other status.
- **INFERRED**, not logged: overrides that the program recomputes by itself. These are the ortho-case estimate overrides (`ClaimProcs` ortho path), and `ComputeBaseEst` capping the allowed override at the fee outside an edit window. Logging them would add an entry on every ortho visit with no user change behind it.
- Known gap: when Edit Claim Procedure is opened with `doSaveToDb=false` and its caller then cancels, nothing is saved. The fork logs override changes only where the save happens, so no wrong override entry is written in that case. The window's other entries are still written on OK, even when the caller then cancels: the write-off and payment entries (as in 24.3) and the new Fee Billed / Code Sent entry, because Edit Claim Procedure is the only place those two fields are edited.

## v25_2-account-medical-eob-prompt (partial)

Only the release-note line is public, so the whole flow is the spec's design (**INFERRED**).

The prompt runs when a medical or institutional claim is received in three places: Edit Claim (save), manual ERA posting, and claim creation (`ClaimL`, when the lead claim is already received). It runs inside the existing "prompt for primary claims" block, just before that prompt, so the attachment goes out with the dental claim. For each of the patient's primary dental claims (ClaimType P, MedType Dental, Unsent or Hold Until Pri Received) that share a procedure with the medical claim, it asks whether to attach the medical EOB:

- **Yes:** if the medical claim's payment already has EOB files (`eobattach`), it offers to attach them; otherwise, or on No, it asks for a file to import. The file is copied into the claim attachment folder (`EmailAttachments` in the A to Z folder) and added as a `claimattach` row. The EoB paperwork keyword is added to `claim.AttachedFlags`, keeping Mail/Elect, Submitted and every other keyword. A ClaimEdit audit entry is written.
- **No:** it continues. Errors are shown and skipped, so the prompt never blocks receiving or sending. It never runs during ERA auto-processing.

The prompt works whether "Prompt for primary claims" is on or off. Files: `OpenDentBusiness/Data Interface/ClaimAttaches.MedicalEob.cs` (new; `ClaimAttaches` made `partial`), `OpenDental/Logic/MedicalEobL.cs` (new), hooks in `FormClaimEdit.cs`, `FormEtrans835ClaimPay.cs` and `ClaimL.cs`. The checkbox "Prompt for Medical EOB on primary claims" is in Preferences > Account > Claim Receive, Medical group (`UserControlAccountClaimReceive`).

**Why partial.** Open Dental's preference name is not public. The 25.2 and 25.3 database docs and the manual pages were checked, and none of them names it. The registry rule (owner decision: preference names come from published docs; `RegistryTests.PrefNameTests.UnconfirmedNames_AreNotRegistered`) keeps the proposed name `ClaimMedReceivedPromptForMedicalEob` out of `PrefName`. So the code reads the preference only once a `PrefName` member with that name exists (`ClaimAttaches.TryGetMedicalEobPrefName`). Until then the checkbox is hidden and the prompt is off. To turn it on, the registry owner (M1-02/M2-02) adds the confirmed name in four places: `PrefName`, `PrefInfos.xml` (control `checkClaimMedReceivedPromptForMedicalEob`), the `Seed25_3` default "0", and the RegistryTests confirmed list. If the confirmed name differs, also change `ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT`. No other change is needed.

**INFERRED / deviations:**

- The spec's "Yes opens the dental claim's attachment step" is simplified: the file is attached directly, without opening a window. For DentalXChange attachments, the user still sends the file from the claim's DentalXChange tab. The Submitted keyword is left unchanged (V-DXC1's rules).
- Scanning is not offered: TWAIN is held for a later stage. Only file import is.
- The batch insurance payment window has no primary-claims prompt in 24.3, so it gets no Medical EOB prompt either.
- The A to Z folder is required. With images stored in the database, the prompt says so and stops.

## v25_3-security-inswriteoffedit-clarify

The permission text ("Insurance Write-off Edit. Also needed to create or delete claim procedures") is M1-02's (`GroupPermission.cs`). Checking the 24.3 code paths found:

- **Create.** Add Estimate in the Procedure window checks 119 with the procedure's `DateEntryC`. A supplemental payment's write-off column checks 119 (`FormClaimPayTotal`). A new total payment can be created, but its write-off field stays read-only without 119 for its date. So no path lets a write-off be added outside the limit. (**INFERRED**: creating a total payment stays allowed, because it is a payment, governed by InsPayCreate.)
- **Edit.** Edit Claim Procedure makes Write-off and Write-off Override read-only without 119 for the date.
- **Delete.** Edit Claim Procedure disables Delete without 119 for the date. Unchecking Do Not Bill Ins in the Procedure window (which deletes and recreates estimates) checks 119.

Added:

- `ClaimProcs.GetInsWriteOffEditDate` and `IsInsWriteOffEditAuthorized` (new partial file `ClaimProcs.WriteOffPermission.cs`). They hold the date rule in one place: the procedure's `DateEntryC`, or the claimproc's `SecDateEntry` for a total payment.
- An explicit check at the top of `FormClaimProc.butDelete_Click`, so the rule holds at the action, not only through the disabled button.

The existing inline checks in `FormProcEdit` and `FormClaimPayTotal` were left as they are (not B-CLAIM files; same rule).

## v25_3-security-claimproc-billedtoins

The rename to `ClaimProcFieldsBilledToInsEdit` (252, value unchanged) and its wording are M1-02's. In `FormClaimProc`:

- Without 252, Code Sent is read-only, like Fee Billed.
- On Save, both fields are applied only with 252 (`ClaimProcs.SetBilledToInsFields`), for new and existing claimprocs. This works with the existing disabled, read-only and hidden cases.
- A change to either field writes an entry under 252 ("Fee billed changed from ... to ...; Code sent changed from ... to ...", FKey ClaimProcNum).

Outside the owned files: `GroupPermissions.HasAuditTrail` now lists 252, so the entries can be filtered in the audit trail. The spec names this line as a touchpoint.

## Files touched

- New: `OpenDentBusiness/Data Interface/ClaimProcs.AuditTrail25.cs`, `ClaimProcs.WriteOffPermission.cs`, `ClaimAttaches.MedicalEob.cs`; `OpenDental/Logic/MedicalEobL.cs`; `tests/BCLAIMTests/*`; this note.
- Changed (B-CLAIM-owned):
  - `OpenDentBusiness/Data Interface/ClaimProcs.cs` (3 lines), `ClaimAttaches.cs` (`partial`), `Etranss.cs` (1 line)
  - `OpenDental/Forms/FormClaimProc.cs`, `FormClaimEdit.cs` (3 lines), `FormEtrans835ClaimPay.cs` (1 line)
  - `OpenDental/Logic/ClaimL.cs` (1 line)
  - `OpenDental/User Controls/Preferences/UserControlAccountClaimReceive.cs` and `.Designer.cs`
- Changed outside the owned list:
  - `OpenDentBusiness/Data Interface/GroupPermissions.cs`: one `case` commented out, so 252 appears in the audit trail.
  - `OpenDentBusiness/OpenDentBusiness.csproj` and `OpenDental/OpenDental.csproj`: compile entries for the new files.
  - `OpenDental.sln` and `OpenDental.Local.slnf`: the test project.
- No schema change, no fork migration step, no CRUD change, no preference or permission rows.

## Interplay with packages built in parallel

- **M1-02 / M2-02 (registries):** registering the Medical EOB preference name lights the feature up (see above). The `RegistryTests` permission tests still pass with 252 in the audit trail.
- **M2-EST, M2-HIST (ClaimProcs.cs):** the B-CLAIM edits there are three lines in `CreateAuditTrailEntryForClaimProcPayment` and `SetInsEstTotalOverride`. Everything else is in new partial files.
- **M2-CLAIMRCV (FormClaimEdit, ClaimL, FormEtrans835ClaimPay):** the 26.1 "prompt for the next insurance" rework keeps the medical-to-primary branch. The `MedicalEobL.PromptToAttachMedicalEob` call should stay just before that branch. `HookSourceTests.MedicalEobPrompt_RunsBeforeEachPrimaryClaimsPrompt` checks it.
- **B-ERA, B-ACCT (dependencies):** they are built against the existing ERA and claim code. The only ERA hook is one line after `ReceiveEraPayment` in the automatic branch.
- **V-DXC1 (merged), V-EDS, V-PAYSUITE:** claim attachments added by the prompt are ordinary `claimattach` rows. The DentalXChange Submitted keyword is not touched.
- **B-SHELL:** `ControlAccount` and `ControlTreat` are not changed by B-CLAIM.

## Tests

`BCLAIMTests` (42 tests):

- Rule and text tests with no database.
- Database tests on a scratch database with a unique per-run name, dropped afterwards, using synthetic Z codes and carriers:
  - override audit from each business path (direct, payment audit path, preauth estimates, list matching);
  - the 252 audit;
  - 119 with a 30-day limit for edit, delete and create, and total payments;
  - 252 for users with and without it;
  - Medical EOB claim selection, EOB lookup, file attach with the A to Z folder, refusal without it, and the preference gate.
- Source checks that the window hooks are present and in the right order.

Related existing assemblies run: `RegistryTests`, `VendorDirectTests`, `ImagingTests`.

Not tested by running code:

- The WinForms dialogs (`MedicalEobL` questions and file dialog, `FormClaimProc` field states, the preferences checkbox). The GUI is not launched; source checks only.
- The ERA automatic-branch hook. It needs an 835 fixture; source check only.
- The Medical EOB prompt end to end, which cannot fire until the preference is registered.

## Owner questions

1. What is Open Dental's preference name for "Prompt for Medical EOB on primary claims"? Or approve the proposed `ClaimMedReceivedPromptForMedicalEob`, so the registry owner can register it.
2. Medical EOB trigger: is the fork's reading right (ask when a medical claim is received, about the unsent primary dental claims)? The spec lists two other readings.
3. Should attaching the medical EOB set the EoB paperwork flag automatically (as built), or leave the flags to the user?
4. Override audit entries for received claimprocs are filed under Claim Procedure Received Edit, which is not in the audit trail's permission filter (24.3 behaviour, M1-02's file). Should it be added, or should all override entries go under Proc Edit?
5. Should overrides that the program recomputes by itself (ortho cases) also be logged?
