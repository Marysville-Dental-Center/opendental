# Port note: B-CLAIM (claims, claim-procedure audit and permissions)

Branch `port/b-claim`, cut from `m1/integration-a2-patch`; review fixes on `port/b-claim-r1` and `port/b-claim-s1`; finished on `port/b-claim-t1` with the integration head `port/int-7` (1763cc7ae) merged in (see Integration on port/int-7 below). Backlog section: [port-backlog.md, B-CLAIM](../specs/port-backlog.md). Tests: `tests/BCLAIMTests` (assembly `BCLAIMTests`).

Sources used: Open Dental's public release notes for 25.1, 25.2 and 25.3, the public manual pages on claim receiving and medical insurance, and the published database docs (25.1 to 25.3 diff XMLs). Nothing was decompiled, observed or copied. Where behaviour is not public it is marked **INFERRED**.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v25_1-misc-claimproc-override-audit` | built | Audit entries for Ins Est Total override and Allowed override changes, from every save path the fork can hook. |
| `v25_2-account-medical-eob-prompt` | built | Prompt, attach logic and preference checkbox. Off by default. Until Open Dental's preference name is confirmed, the setting is kept in the fork-owned preference row `ForkClaimMedReceivedPromptForMedicalEob` (see below). |
| `v25_3-security-inswriteoffedit-clarify` | built | The wording came from M1-02. The 24.3 create, edit and delete paths were checked. Manual ERA posting let a write-off be typed without 119 and now checks it; the others already enforced the rule. Added one shared date rule and an explicit check when Delete is clicked. |
| `v25_3-security-claimproc-billedtoins` | built | Code Sent is guarded by permission 252, the same way as Fee Billed. Changes to either field are logged under 252. |
| `v25_1-account-canada-attachment-history` | not built | Excluded by owner decision (the Canadian claim networks were removed). |
| `v25_1-canada-etrans-counters-uncapped` | not built | Excluded by owner decision (the Canadian claim networks were removed). |
| `v25_1-imaging-canada-claim-response-titles` | not built | Excluded by owner decision (the Canadian claim networks were removed). |

## v25_1-misc-claimproc-override-audit

A save that changes `claimproc.InsEstTotalOverride` or `claimproc.AllowedOverride` writes one audit entry per claimproc. The entry shows the old and new values, with -1 shown as "blank". FKey is the ClaimProcNum. The text names the procedure code, the tooth and the carrier (or "Total payment").

- Logic: `ClaimProcs.GetOverrideChangeText`, `CreateAuditTrailEntryForOverrides` and `CreateAuditTrailEntriesForOverrides` in the new partial file `OpenDentBusiness/Data Interface/ClaimProcs.AuditTrail25.cs`.
- Hooks, one or two lines each:
  - `FormClaimProc.butSave_Click`, only when the window saves the claimproc itself (`_doSaveToDb`). This covers estimates edited from the Procedure window, the Treatment Plan module (through the Procedure window), Edit Claim and the institutional claim window.
  - `ClaimProcs.CreateAuditTrailEntryForClaimProcPayment`. It already runs when `FormClaimPayTotal` saves and on "auto receive claims with no assignment of benefits". It covers allowed amounts typed in that grid, plus edits made in Edit Claim Procedure opened from it (that window passes `doSaveToDb=false`, so each claimproc gets one entry). It skips the overrides when `isPaymentFromERA` is true, because manual ERA posting audits them against the database instead (next item).
  - `FormEtrans835ClaimPay` (manual ERA posting). Before this window opens, `EtransL.ImportEraClaimData` calls `Etranss.TryImportEraClaimData(isAutomatic:false)`, which puts the 835 allowed amounts into the claimprocs' `AllowedOverride` in memory. The window's own old copies therefore already hold the ERA values. So `FormEtrans835ClaimPay_Load` also reads the claim's claimprocs from the database (`_listClaimProcsDb`, before anything is saved). Then `butSave_Click`, right after `Etranss.ReceiveEraPayment` saves, calls `ClaimProcs.CreateAuditTrailEntriesForOverrides(ListClaimProcs,_listClaimProcsDb)`. A claimproc saved with a blank allowed override and posted from an ERA with $80 allowed gets "Allowed override changed from blank to $80.00". A value the user retypes in the grid (or in Edit Claim Procedure opened from it) is in the same single entry, compared with the database. The list is `ListClaimProcs`, every claimproc `ReceiveEraPayment` saves, not only the grid rows (rows it hides are unchanged and log nothing).
    - The database copies include the supplemental claimprocs that `TryImportEraClaimData` pre-inserts (copies of the received claimprocs). Their entry compares with that inserted copy, the same as supplemental rows in `FormClaimPayTotal`. A new total payment row has no database copy and gets no override entry (it is a new row, like in the other paths).
  - `ClaimProcs.SetInsEstTotalOverride`: a received preauthorization's estimates are copied into the procedure's estimate override. This also covers preauthorizations posted from an ERA (`ReceiveEraPayment` calls it).
  - `FormClaimEdit`, preauthorization payment path: overrides edited in the preauth payment window.
  - `Etranss.TryImportEraClaimData`, automatic branch: allowed overrides that ERA auto-processing clears. Its old copies are taken on entry, before the ERA values are applied, so it needs no database read.
- **INFERRED** permission type (Open Dental's is not public; the published 25.1 docs add no permission for it). As the spec proposes: Claim Procedure Received Edit (125) for Received and Supplemental claimprocs, and Proc Edit (100, logging only) for every other status.
- **INFERRED**, not logged: overrides that the program recomputes by itself. These are the ortho-case estimate overrides (`ClaimProcs` ortho path), and `ComputeBaseEst` capping the allowed override at the fee outside an edit window. Logging them would add an entry on every ortho visit with no user change behind it.
- Known gap: when Edit Claim Procedure is opened with `doSaveToDb=false` and its caller then cancels, nothing is saved. The fork logs override changes only where the save happens, so no wrong override entry is written in that case. The window's other entries are still written on OK, even when the caller then cancels: the write-off and payment entries (as in 24.3) and the new Fee Billed / Code Sent entry, because Edit Claim Procedure is the only place those two fields are edited.

## v25_2-account-medical-eob-prompt

Only the release-note line is public, so the whole flow is the spec's design (**INFERRED**).

The prompt runs when a medical or institutional claim is received in three places: Edit Claim (save), manual ERA posting, and claim creation (`ClaimL`, when the lead claim is already received). It runs inside the existing "prompt for primary claims" block, just before that prompt, so the attachment goes out with the dental claim. For each of the patient's primary dental claims (ClaimType P, MedType Dental, Unsent or Hold Until Pri Received) that share a procedure with the medical claim, it asks whether to attach the medical EOB:

- **Yes:** if the medical claim's payment already has EOB files (`eobattach`), it offers to attach them; otherwise, or on No, it asks for a file to import. The file is copied into the claim attachment folder (`EmailAttachments` in the A to Z folder) and added as a `claimattach` row. The EoB paperwork keyword is added to `claim.AttachedFlags`, keeping Mail/Elect, Submitted and every other keyword. A ClaimEdit audit entry is written.
- **No:** it continues. Errors are shown and skipped, so the prompt never blocks receiving or sending. It never runs during ERA auto-processing.

**The attachment survives the primary claims prompt.** That prompt (`ClaimL.PromptForPrimaryOrSecondaryClaim`) reloads the dental claims through `Claims.GetPrimaryOrSecondaryClaimsNotReceived`, which does not load `Claim.Attachments`. For every choice but Do nothing it set them to Waiting to Send with the one-argument `Claims.Update`, which deletes the claim's `claimattach` rows and re-inserts the (empty) list. So the EOB attached a moment before, and any attachment the claim already had, were gone before the claim was queued, sent or printed, while `AttachedFlags` still said EoB (a 24.3 behaviour that the Medical EOB prompt made visible). Fixed in B-CLAIM's own files: the loop is now `ClaimAttaches.SetClaimsWaitingToSend` (in `ClaimAttaches.MedicalEob.cs`), which saves each claim with the two-argument `Claims.Update(claim,claimOld)`. That overload updates only the changed claim columns and leaves `claimattach` alone. In the primary-to-secondary branch with "recalculate secondary estimates" on, the prompt does not save the claims itself; `Claims.CalculateAndUpdateSecondaries` saves them with the one-argument `Claims.Update`. There the helper loads each claim's attachments first, so that save keeps them too. `Claims.cs` (B-ERA, M2-CLAIMRCV) is not changed. Known 24.3 gap left as it is: `Claims.CalculateAndUpdateSecondariesFromPrimaries` (ERA posting, auto receive with no assignment of benefits, the outstanding insurance report) reloads secondary claims the same way and saves them through `Claims.CalculateAndUpdate`, so their attachments are still dropped there. The Medical EOB prompt never attaches to secondary claims, and the fix belongs in `Claims.cs` (owner question 6).

The prompt works whether "Prompt for primary claims" is on or off. Files: `OpenDentBusiness/Data Interface/ClaimAttaches.MedicalEob.cs` (new; `ClaimAttaches` made `partial`), `OpenDental/Logic/MedicalEobL.cs` (new), hooks in `FormClaimEdit.cs`, `FormEtrans835ClaimPay.cs` and `ClaimL.cs`. The checkbox "Prompt for Medical EOB on primary claims" is in Preferences > Account > Claim Receive, Medical group (`UserControlAccountClaimReceive`).

**Preference storage.** Open Dental's preference name is not public. The 25.2 and 25.3 database docs and the manual pages were checked, and none of them names it. The registry rule (owner decision: preference names come from published docs; `RegistryTests.PrefNameTests.UnconfirmedNames_AreNotRegistered`) keeps the proposed name `ClaimMedReceivedPromptForMedicalEob` out of `PrefName`. The s1 attempt therefore hid the checkbox and kept the prompt off. On t1 it uses the pattern the other packages with unconfirmed names settled on after the merge (B-APPT `ApptTimeBars`, B-SHELL `ModuleSwitchBlur`, B-SERVER `RpProcNote`): the setting is the fork-owned preference row `ForkClaimMedReceivedPromptForMedicalEob` (`ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT_FORK`), read with B-SERVER's `PrefC.GetRawOrDefault` and written with `PrefC.UpsertRaw` (`ClaimAttaches.IsMedicalEobPromptEnabled` / `SetMedicalEobPromptEnabled`, no SQL of B-CLAIM's own).

- A missing row reads as off, so neither a 24.3 database nor an adopted Open Dental 25.3.48 database needs a fork migration step, and the adoption tools see no change. Saving Preferences with the box unticked writes no row; the row is inserted the first time the box is ticked. Preferences sends the usual Prefs signal when the value changes.
- The checkbox "Prompt for Medical EOB on primary claims" is always shown. The proposed Open Dental name is never read or written while it is unconfirmed, because an Open Dental database could hold that name with another meaning.
- Once the registry owner (M1-02/M2-02) confirms the name and adds it to `PrefName` (plus `PrefInfos.xml` control `checkClaimMedReceivedPromptForMedicalEob`, the seed default "0" and the RegistryTests confirmed list), `ClaimAttaches.GetMedicalEobPrefNameInUse` switches to it with no other change. If the confirmed name differs, also change `ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT`. An office that ticked the box before then keeps an orphan `Fork...` row; a fork migration step can copy it to the confirmed name, or the office ticks the box again.
- **INFERRED**: a medical preauthorization's EOB is an estimate, so a received medical preauth does not prompt (`GetDentalClaimsForMedicalEob` returns no claims for ClaimType PreAuth). Edit Claim already skipped preauths; manual ERA posting of a medical preauth now does too.

**INFERRED / deviations:**

- The spec's "Yes opens the dental claim's attachment step" is simplified: the file is attached directly, without opening a window. For DentalXChange attachments, the user still sends the file from the claim's DentalXChange tab. The Submitted keyword is left unchanged (V-DXC1's rules).
- Scanning is not offered: TWAIN is held for a later stage. Only file import is.
- The batch insurance payment window has no primary-claims prompt in 24.3, so it gets no Medical EOB prompt either.
- The A to Z folder is required. With images stored in the database, the prompt says so and stops.

## v25_3-security-inswriteoffedit-clarify

The permission text ("Insurance Write-off Edit. Also needed to create or delete claim procedures") is M1-02's (`GroupPermission.cs`). Checking the 24.3 code paths found:

- **Create.** Add Estimate in the Procedure window checks 119 with the procedure's `DateEntryC`. A supplemental payment's write-off column checks 119 (`FormClaimPayTotal`). A new total payment can be created, but its write-off field stays read-only without 119 for its date. Manual ERA posting (`FormEtrans835ClaimPay`) did **not** check 119: its Writeoff column was editable whenever Include Writeoffs was checked. For a supplemental ERA payment, `Etranss.TryImportEraClaimData` pre-inserts the supplemental claimprocs (a create path), so a user limited to 30 days could type a write-off on a procedure entered 90 days ago and save it through `Etranss.ReceiveEraPayment`, the delete-and-recreate dodge the 25.3 wording closes. Ordinary manual ERA posting had the same gap (a typed write-off replaced the ERA's). The same window's **Write-off** button (`butWriteOff_Click`) had it too: it sets each procedure's write-off to its unpaid amount, which `butSave_Click` saves through `ReceiveEraPayment`, and for a PPO plan the Include Writeoffs box is forced on and hidden, so one click and Save wrote off the unpaid balance of a procedure entered 90 days ago for a user limited to 30 days. Both fixed (see Added). With that, no path in the B-CLAIM windows lets a write-off be added outside the limit. (**INFERRED**: creating a total payment stays allowed, because it is a payment, governed by InsPayCreate.)
- **Edit.** Edit Claim Procedure makes Write-off and Write-off Override read-only without 119 for the date.
- **Delete.** Edit Claim Procedure disables Delete without 119 for the date. Unchecking Do Not Bill Ins in the Procedure window (which deletes and recreates estimates) checks 119.

Added:

- `ClaimProcs.GetInsWriteOffEditDate` and `IsInsWriteOffEditAuthorized` (new partial file `ClaimProcs.WriteOffPermission.cs`). They hold the date rule in one place: the procedure's `DateEntryC`, or the claimproc's `SecDateEntry` for a total payment.
- An explicit check at the top of `FormClaimProc.butDelete_Click`, so the rule holds at the action, not only through the disabled button.
- `ClaimProcs.IsInsWriteOffEditAuthorizedForAll` (same file) and its use in manual ERA posting. `FormEtrans835ClaimPay_Load` works out `_isWriteOffEditable` once: true only when the user has 119 for every claimproc the posting saves (`ListClaimProcs`, each with its procedure from `_listProcedures`; a total payment uses its `SecDateEntry`). The Writeoff column is built with `isEditable:isWOIncluded && _isWriteOffEditable`, like the Write-off column of `FormClaimPayTotal`. The ERA's write-offs still post; they just cannot be retyped. Edit Claim Procedure opened from a row keeps its own 119 check.
  - The Write-off button follows the same flag: `butWriteOff.Enabled=_isWriteOffEditable;` in `FormEtrans835ClaimPay_Load` (as `FormClaimPayTotal` does), and `butWriteOff_Click` refuses with a message before it sets any write-off, so the rule holds at the action, not only through the disabled button (fixed on t1; the s1 fix covered only the column).
  - `HookSourceTests.EraPosting_EveryWriteOffAssignmentFollowsInsWriteOffEdit` scans the window for every statement that sets a claimproc's `WriteOff`. Each must be in a method that checks `_isWriteOffEditable`, or be one of the three that need no check: reading the grid cell (`SaveGridChanges`; the column is read-only without 119), restoring the ERA's own write-offs (`checkIncludeWOPercCoPay_CheckedChanged`) and the throw-away copies for the credit check (`GetListClaimProcHypothetical`). A new bulk fill without the check fails this test.
  - **INFERRED**: the total payment row an ERA adds is not saved yet (no `SecDateEntry`). It is being created now, so it is checked with today's date: it needs 119, but no date limit stops it. Checking it with an empty date would lock the column for every ERA with a total payment.
  - **INFERRED**: one flag for the whole column (the grid has no per-row editability), so one procedure outside the limit locks the column for all rows, as in `FormClaimPayTotal`.
  - Not changed: the Include Writeoffs checkbox. It only chooses between the ERA's write-offs and none (the 24.3 preference `EraIncludeWOPercCoPay`); it never lets an amount be typed.

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
  - `OpenDentBusiness/Data Interface/ClaimProcs.cs` (5 lines), `ClaimAttaches.cs` (`partial`), `Etranss.cs` (1 line)
  - `OpenDental/Forms/FormClaimProc.cs`, `FormClaimEdit.cs` (3 lines), `FormEtrans835ClaimPay.cs` (about 15 lines: the Medical EOB prompt, the database copies and override audit for ERA posting, and the 119 check of the Writeoff column and the Write-off button)
  - `OpenDental/Logic/ClaimL.cs` (1 line for the Medical EOB prompt; the Waiting to Send loop in `PromptForPrimaryOrSecondaryClaim` replaced by one call to `ClaimAttaches.SetClaimsWaitingToSend`)
  - `OpenDental/User Controls/Preferences/UserControlAccountClaimReceive.cs` and `.Designer.cs`
- Changed outside the owned list:
  - `OpenDentBusiness/Data Interface/GroupPermissions.cs`: one `case` commented out, so 252 appears in the audit trail.
  - `OpenDentBusiness/OpenDentBusiness.csproj` and `OpenDental/OpenDental.csproj`: compile entries for the new files.
  - `OpenDental.sln` and `OpenDental.Local.slnf`: the test project.
- No schema change, no fork migration step, no CRUD change, no permission rows. The only preference row is the fork-owned `ForkClaimMedReceivedPromptForMedicalEob`, written by Preferences the first time the Medical EOB box is ticked (never by a migration).

## Interplay with packages built in parallel

- **M1-02 / M2-02 (registries):** registering the confirmed Medical EOB preference name moves the setting from the fork-owned row to that preference (see above). The `RegistryTests` permission tests still pass with 252 in the audit trail, and `ClaimMedReceivedPromptForMedicalEob` stays in the unconfirmed list.
- **B-SERVER:** the Medical EOB setting uses its `PrefC.GetRawOrDefault` / `PrefC.UpsertRaw` helpers.
- **M2-EST, M2-HIST (ClaimProcs.cs):** the B-CLAIM edits there are a few lines in `CreateAuditTrailEntryForClaimProcPayment` (the override call, guarded by `!isPaymentFromERA`) and `SetInsEstTotalOverride`. Everything else is in new partial files.
- **M2-CLAIMRCV (FormClaimEdit, ClaimL, FormEtrans835ClaimPay):** the 26.1 "prompt for the next insurance" rework keeps the medical-to-primary branch. The `MedicalEobL.PromptToAttachMedicalEob` call should stay just before that branch. `HookSourceTests.MedicalEobPrompt_RunsBeforeEachPrimaryClaimsPrompt` checks it. B-CLAIM also changed the Waiting to Send loop of `ClaimL.PromptForPrimaryOrSecondaryClaim` into one call to `ClaimAttaches.SetClaimsWaitingToSend`, so expect a small conflict there if the rework rewrites that method. Whatever replaces it must not save the reloaded claims with the one-argument `Claims.Update` (it deletes their attachments, the medical EOB among them): keep the call, or use `Claims.Update(claim,claimOld)`. `HookSourceTests.PrimaryClaimsPrompt_SavesTheClaimRowAndKeepsAttachments` checks it. The same applies to any new "next insurance" prompt that saves claims loaded with `Claims.GetClaimsFromClaimNums` or `Claims.Refresh`.
- **B-ERA, B-ACCT (dependencies), M2-ERA:** they are built against the existing ERA and claim code. In `FormEtrans835ClaimPay`, B-CLAIM adds the `_isWriteOffEditable` field, one line in `FormEtrans835ClaimPay_Load` (after `_listProcedures` is filled, before `FillGridProcedures`) `&& _isWriteOffEditable` on the Writeoff column, `butWriteOff.Enabled=_isWriteOffEditable;` after it, and a guard at the top of `butWriteOff_Click`. A package that adds another write-off entry point to ERA posting (a new column, a bulk fill) must honour the same flag. `HookSourceTests.EraPosting_WriteOffColumnFollowsInsWriteOffEdit` checks the column and the button, and `EraPosting_EveryWriteOffAssignmentFollowsInsWriteOffEdit` fails for any new statement in the window that sets `WriteOff` without the check. The ERA hooks are one line after `ReceiveEraPayment` in the automatic branch of `Etranss.TryImportEraClaimData`, and in `FormEtrans835ClaimPay` (B-ERA and M2-ERA also list it): the `_listClaimProcsDb` field, one line in `FormEtrans835ClaimPay_Load` and one line after `Etranss.ReceiveEraPayment` in `butSave_Click`. If another package adds a save of the claimprocs before that point, or posts ERA payments through another window, it should keep the database copies taken before its first save and call `ClaimProcs.CreateAuditTrailEntriesForOverrides` once after its last save. `HookSourceTests.OverrideAudit_ManualEraPostingComparesWithTheDatabase` checks the hooks.
- **V-DXC1 (merged), V-EDS, V-PAYSUITE:** claim attachments added by the prompt are ordinary `claimattach` rows. The DentalXChange Submitted keyword is not touched.
- **B-SHELL:** `ControlAccount` and `ControlTreat` are not changed by B-CLAIM.

## Tests

`BCLAIMTests` (54 tests):

- Rule and text tests with no database.
- Database tests on a scratch database with a unique per-run name, dropped afterwards, using synthetic Z codes and carriers:
  - override audit from each business path (direct, payment audit path, preauth estimates, list matching);
  - manual ERA posting, in the window's order (working copies with the ERA allowed amount, old copies taken after that, database copies, `Etranss.ReceiveEraPayment`, then both audits): exactly one "blank to $80.00" entry; a value retyped in the grid gives one entry against the database; a pre-inserted supplemental claimproc is logged and the untouched received one is not; the ERA payment audit path no longer logs overrides itself;
  - the 252 audit;
  - 119 with a 30-day limit for edit, delete and create, and total payments; manual ERA posting (`IsInsWriteOffEditAuthorizedForAll`): a supplemental payment on a procedure entered 90 days ago, one old procedure among recent ones, a new and a saved total payment, and no 119 at all;
  - 252 for users with and without it;
  - Medical EOB claim selection (no prompt for a medical preauthorization), EOB lookup, file attach with the A to Z folder, refusal without it;
  - the Medical EOB setting: off while the fork-owned row is missing, saving the default writes no row, ticking the box inserts it, the value survives a cache reload, an unchanged save reports no change; a row with the unconfirmed Open Dental name is neither read nor written;
  - the primary claims prompt after the Medical EOB attach: the claims reloaded by `Claims.GetPrimaryOrSecondaryClaimsNotReceived`, set to Waiting to Send by `ClaimAttaches.SetClaimsWaitingToSend`, keep both the medical EOB and an earlier attachment; a secondary claim recalculated by `Claims.CalculateAndUpdateSecondaries` keeps its attachment.
- Source checks that the window hooks are present and in the right order, that the primary claims prompt no longer saves its claims with the one-argument `Claims.Update`, that the ERA Writeoff column and Write-off button follow the 119 result, that every statement of manual ERA posting that sets a write-off is covered by it, and that the preferences checkbox reads and saves the fork-owned row.

Related existing assemblies run: `RegistryTests`, `VendorDirectTests`, `ImagingTests` (s1); on t1, after the int-7 merge, the whole Core run (61 assemblies, all passing; `test-results/port_b-claim-t1.txt` in the tools folder). The slow migration and adoption assemblies were not needed: B-CLAIM changes no migration, schema, CRUD or adoption code.

Not tested by running code:

- The WinForms dialogs (`MedicalEobL` questions and file dialog, `FormClaimProc` field states, the preferences checkbox). The GUI is not launched; source checks only.
- The ERA automatic-branch hook, and `Etranss.TryImportEraClaimData` itself on the manual path. They need an 835 fixture; source checks only. The manual-path tests set the ERA values on the working copies the way `TryImportEraClaimData` does.
- The Medical EOB prompt end to end (its questions and file dialog are WinForms); its business steps are tested one by one above.

## Integration on port/int-7

- `port/int-7` (1763cc7ae: M1, the 26.1 schema, and the B, M2 and V packages merged so far) merged into `port/b-claim-t1` with no conflicts. The `.sln`/`.slnf` merge driver kept `BCLAIMTests` beside the other test projects.
- Adapted to the merged packages: the Medical EOB setting now uses B-SERVER's fork-owned preference helpers, the pattern B-APPT, B-SHELL and B-SERVER use for their unconfirmed names (B-APPT's note asked the integrator to pick one approach; this is it). No duplicate helper was kept.
- Checked against the merged ERA packages: B-ERA (auto split in `Etranss.TryAutoProcessEraEob`) and M2-ERA (`ClaimProcs.LinkToEraClaim` in `TryImportEraClaimData`, two lines in `ClaimProcs.cs`) sit next to the B-CLAIM hooks without overlap; neither adds a write-off entry point to manual ERA posting. B-CLAIM does not change the By Total or write-off rules that `EraAutoSplit` repeats. M2-CAN's Pay Labs in `FormClaimPayTotal` leaves write-offs alone.
- Adoption: B-CLAIM adds or activates no fork migration step and changes no schema or rows during migration, so `migration-preview`, `AdoptionRowCheck` and `allowed-differences.adoption.json` need nothing new.

## Owner questions

1. What is Open Dental's preference name for "Prompt for Medical EOB on primary claims"? Or approve the proposed `ClaimMedReceivedPromptForMedicalEob`, so the registry owner can register it. Until then the setting works from the fork-owned row `ForkClaimMedReceivedPromptForMedicalEob`.
2. Medical EOB trigger: is the fork's reading right (ask when a medical claim is received, about the unsent primary dental claims)? The spec lists two other readings.
3. Should attaching the medical EOB set the EoB paperwork flag automatically (as built), or leave the flags to the user?
4. Override audit entries for received claimprocs are filed under Claim Procedure Received Edit, which is not in the audit trail's permission filter (24.3 behaviour, M1-02's file). Should it be added, or should all override entries go under Proc Edit?
5. Should overrides that the program recomputes by itself (ortho cases) also be logged?
6. `Claims.CalculateAndUpdate` saves with the one-argument `Claims.Update`, which drops the attachments of claims loaded without them (secondary claims recalculated after a primary is received, outside the prompt). Should the owner of `Claims.cs` (B-ERA / M2-CLAIMRCV) switch it to the two-argument overload?
