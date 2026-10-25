# Port note: M2-CLAIMRCV, related-claims prompt on receive

Branch `port/m2-claimrcv`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, M2-CLAIMRCV](../specs/port-backlog.md#m2-claimrcv-related-claims-prompt-on-receive). Feature spec: [features-26.1.md](../specs/features-26.1.md#v26_1-account-claim-receive-related-prompt-prompt-to-send-related-outstanding-claims-when-any-claim-is-received). No schema change, no preference, no registry entry and no fork migration step (the level range 2027004001 to 2027004099 is unused).

Sources: Open Dental's public 26.1 release notes (Account: in Edit Claim, receiving a claim of any ordinal offers to send the related unsent claims) and the published 26.1 database document (`claim.ClaimStatus` H described as hold until prior received; `patplan.Ordinal` up to 10). Nothing else is public about the rules, so the details below marked INFERRED are this fork's choices. All wording is written for this fork.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v26_1-account-claim-receive-related-prompt` | built | Every rule of the spec, with the deviations listed below. |

## What changed

The 24.3 code asked for "secondary claims" (claim type S) after any received claim. The lookup is now "the next insurance": a received primary claim still offers its secondary claims, a received secondary claim offers the tertiary claims, and so on up to ordinal 10. Every 24.3 caller of the prompt gets this without a change of its own, because the generalization sits in the two shared methods they call:

- `Claims.GetPrimaryOrSecondaryClaimsNotReceived(list,isSecondaryClaim:true)` now returns `Claims.GetNextOrdinalClaimsNotReceived(list)`. The medical-to-primary branch (`isSecondaryClaim:false`, preference `ClaimMedReceivedPromptForPrimaryClaim`) is unchanged.
- `ClaimL.PromptForPrimaryOrSecondaryClaim` takes its wording and its force-status preference from `ClaimReceiveRelated`. The choices are the same: change the status to Waiting to Send, send now, print now, or do nothing; "Do nothing" is hidden when the force-status preference is on.

Callers covered that way: Edit Claim (`FormClaimEdit.SaveCleanup`, the release note's trigger), claim creation when the new claim is already received (`ClaimL.CreateClaimFromWrapper`), the ERA claim payment window, the ERA automation report's "process secondary claims" item, and Send Claims after an automatic no-assignment receive. ERA automatic processing (`Etranss`, line 985) still never prompts: it moves the next insurance's claims to Waiting to Send silently, as 24.3 did for secondary claims.

### Rules as built

1. **Trigger.** Unchanged: Edit Claim prompts when the claim is saved as Received after payment was entered, the claim is not a preauthorization, and the user has ClaimSend (checked silently). Claim creation prompts when the lead claim is already received and its secondary claim was just created on hold (24.3 condition; the prompt itself now looks for the next insurance).
2. **Related claims** (`Claims.SelectNextOrdinalClaims`, `OpenDentBusiness/Data Interface/Claims.NextOrdinal.cs`), for each received claim R with ordinal r:
   - candidates are the claims of R's patient, not preauthorizations, with a claimproc (not a preauth claimproc) on one of R's procedures, and an ordinal above r;
   - the next ordinal is the lowest candidate ordinal whose claim is not received yet (INFERRED, see below);
   - returned are the candidates at that ordinal with status Unsent (U) or Hold Until Prior Received (H) that have a not-received claimproc on one of R's procedures (the 24.3 claimproc condition).
   - A claim's ordinal is the patient's patplan ordinal for `claim.InsSubNum`; if that patplan is gone, a P claim counts as 1, an S claim as 2, anything else as unknown (no related claims).
3. **Medical to primary** is kept as is.
4. **Recalculation** with `ClaimPrimaryReceivedRecalcSecondary` on now follows a receipt of any ordinal (INFERRED, as the spec says): Edit Claim (`ClaimReceiveRelated.IsRecalcNextInsuranceNeeded`), `Claims.ReceiveAsNoPaymentIfNeeded`, ERA payments (`Etranss.ReceiveEraPayment`) and Outstanding Insurance's zero-claim tool (`RpOutstandingIns.ZeroClaims`). `Claims.CalculateAndUpdateSecondariesFromPrimaries` recalculates the next insurance's claims, including claims on hold for an in-process multi-visit group (I).
5. **Permission** ClaimSend, checked silently as before; the existing claim logs are unchanged.

### UI

- Prompt: "There is at least one unsent claim for the next insurance on the received procedures." Title "Outstanding claims for the next insurance"; choices "Send claim(s) now" and "Print claim(s) now". The medical-to-primary texts are the 24.3 texts, so their translations still match.
- Edit Claim's status drop-down shows H as "Hold Until Prior Received" (`ClaimReceiveRelated.GetClaimStatusText`). The stored value stays "H", and the enum's description in `Claim.cs` (M2-01's file) is unchanged.
- Preferences > Account > Claim Receive: "Prompt for claims of the next insurance" (`PromptForSecondaryClaim`), "Remove 'Do Nothing' from the next insurance claims prompt" (`ClaimPrimaryRecievedForceSecondaryStatus`), "Auto update next insurance claim estimates when a claim is received" (`ClaimPrimaryReceivedRecalcSecondary`). Preference names and values are unchanged.

## Files touched

Owned by this package:
- `OpenDental/Logic/ClaimL.cs`: the prompt's wording and force-status preference (one block in `PromptForPrimaryOrSecondaryClaim`).
- `OpenDentBusiness/Data Interface/Claims.cs`: `partial` on the class, the delegation in `GetPrimaryOrSecondaryClaimsNotReceived`, the lookup in `CalculateAndUpdateSecondariesFromPrimaries`, and the type filter in `ReceiveAsNoPaymentIfNeeded`.
- `OpenDental/Forms/FormClaimEdit.cs`: two places (the status drop-down fill, and the recalculation in `SaveCleanup`).
- `OpenDental/User Controls/Preferences/UserControlAccountClaimReceive.Designer.cs`: three labels.

New: `OpenDentBusiness/Data Interface/Claims.NextOrdinal.cs` (`Claims.GetNextOrdinalClaimsNotReceived`, `SelectNextOrdinalClaims`, `GetClaimOrdinal`, `GetNextOrdinalStatuses`, and the `ClaimReceiveRelated` class), `tests/M2CLAIMRCVTests/`, this note.

Outside the owned list (no foundation registry was touched):
- `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line for the new file.
- `OpenDentBusiness/Data Interface/Etranss.cs` (no owner in this run): one condition in `ReceiveEraPayment`, `claim.ClaimType=="P"` to `!="PreAuth"`.
- `OpenDentBusiness/Reports/RpOutstandingIns.cs` (no owner in this run): one filter in `ZeroClaims`, same change.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.

## INFERRED behaviours

- **Which insurance is "next".** The spec says "the patient's plan with the next higher ordinal". Built as: the lowest later ordinal that has a claim, not yet received, for the received procedures. So a later plan with no claim for these procedures (for example a medical plan between two dental plans) is skipped, and a later insurance that already received them (claims received out of order) is passed over. A sent or waiting claim at the next ordinal stops the search, so a tertiary claim never jumps ahead of an outstanding secondary claim.
- **Dropped plans.** When the received claim's plan is no longer on the patient, a P claim counts as ordinal 1 and an S claim as 2, so the 24.3 secondary-after-primary prompt still works; other claim types get no prompt.
- **Hold for In Process (I).** The spec lists U, H and I as "not received". I claims belong to a multi-visit group whose procedures are not complete, and Edit Claim refuses to change that status by hand, so the prompt never changes, sends or prints them; they are only recalculated (rule 4). The multi-visit logic releases them as before.
- **Recalculation after any receipt** (spec rule 4), extended to the ERA payment and zero-claim paths so every receive path behaves the same.
- **Wording**: the prompt's title and choices, the three preference labels, and "Hold Until Prior Received" (taken from the 26.1 database document's description of H).

## Deviations from the spec

- Claim creation keeps the 24.3 condition (the lead claim is received and its secondary claim was just created). In that path the lead is the primary (or medical) claim, so the generalized prompt offers the claim of the insurance after it, normally that new secondary claim.
- The medical-to-primary preference label ("Remove 'Do Nothing' for primary claims with 'Hold Until Pri Received' or 'Unsent'") is not changed: the longer 26.1 status text would not fit the check box, and the label names a preference, not a status column.
- After "Send now" or "Print now", a next-insurance claim that is received automatically (no assignment of benefits) does not open a further prompt for the insurance after it, as in 24.3; its estimates are recalculated (rule 4).
- `FormClaimInstEdit` also lists "Hold until Pri received", but nothing opens that window, so it is left alone.

## Interplay with packages built at the same time

- **V-EDS, V-PAYSUITE** (also edit `FormClaimEdit.cs`): this package changes only the status drop-down fill (one line near `comboClaimStatus.Items.Clear()`) and the recalculation `if` at the end of `SaveCleanup`. Keep both when merging; neither moves code.
- **M2-ERA** (owns `FormRpEraAutoProcessed`, `FormEtrans835ClaimPay`): the claim payment window needs no change. The automation report's "process secondary claims" item first checks for a claim of type S with status U, H or I and shows "none of them are for secondary claims" otherwise; for a received secondary claim with an unsent tertiary claim it therefore stops before the generalized prompt. Suggested change there: test `Claims.GetNextOrdinalClaimsNotReceived(listClaimProcsForClaim,includeHoldForInProcess:true).Count>0` instead of the type check, and use "next insurance" in the message. If M2-ERA edits `Etranss.cs` near `ReceiveEraPayment`, keep this package's one-condition change.
- **M2-SDV** (owns `AccountModules.cs`): the Account grid shows H as "Hold until Pri received". Use `ClaimReceiveRelated.GetClaimStatusText(ClaimStatus.HoldUntilPriReceived)` (or `ClaimReceiveRelated.HOLD_UNTIL_PRIOR_RECEIVED` through `Lans.g`) to show the 26.1 text.
- **M2-INSV** (owns `PatPlans.cs`): nothing needed; the lookup reads patplans through `PatPlans.GetPatPlansForPats` and handles ordinals up to 10.
- **Foundation (M2-02, PrefInfos.xml)**: the `ControlText` entries for `checkPromptForSecondaryClaim` and `checkClaimPrimaryRecievedForceSecondaryStatus` still carry the 24.3 labels, and the `ClaimMedReceived*` details say "Hold Until Pri Received". The Preferences window matches entries by control name, so nothing breaks; the texts can be refreshed in a registry follow-up.
- `FormClaimPayBatch` (batch insurance payments) still lists only secondary claims (`Claims.GetSecondaryClaims`) when `PromptForSecondaryClaim` is off. Not part of this item; left as is.

## Tests

New project `tests/M2CLAIMRCVTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 37 tests:

- `NextOrdinalSelectionTests` (no database): primary to secondary kept, secondary to tertiary, every ordinal up to 10, sent or waiting next insurance blocks later ones, out-of-order receipts, skipped plans without claims, shared-procedure rule, split claims, preauthorizations, Hold for In Process only for recalculation, other patients, several received claims, dropped plans, ordinal fallbacks.
- `ClaimReceiveRelatedTests` (no database): prompt wording, the 24.3 medical-to-primary texts, force-status preferences, the recalculation decision, the "Hold Until Prior Received" text.
- `NextOrdinalDbTests`: scratch MariaDB database (`forkmig_<time>_<pid>_<random>_claimrcv`, fresh 24.3 plus the fork migration runner, dropped afterwards; synthetic patients, invented carriers and Z codes): the lookup the prompt runs, the medical branch, ordinal 5, recalculation of the tertiary claim after a secondary claim through `CalculateAndUpdateSecondariesFromPrimaries`, `ReceiveAsNoPaymentIfNeeded` and `ZeroClaims`.
- `WiringTests`: source checks of Edit Claim, the prompt, claim creation, the ERA and zero-claim paths and the preference labels (window code has no test harness).

Related existing assemblies run for this package: `VendorDirectTests` (pins the XConnect validation inside `PromptForPrimaryOrSecondaryClaim`) and `ImagingTests` (pins the preauthorization EOB wiring in `FormClaimEdit` and `ClaimL`).

Not tested: the prompt window itself (an `InputBox` dialog) and the Preferences window layout; they were not opened, because the GUI is not launched in package work.

## Questions for the owner

1. Is "the next insurance that has a claim, not yet received, for the received procedures" the rule you want, or strictly patplan ordinal plus one?
2. Claims on Hold for In Process (I) are recalculated but never changed, sent or printed by the prompt. Confirm.
3. Should a next-insurance claim that the prompt sends and that is then received automatically (no assignment of benefits) chain a prompt for the insurance after it? 24.3 does not, and neither does this build.
