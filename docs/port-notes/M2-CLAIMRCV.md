# Port note: M2-CLAIMRCV, related-claims prompt on receive

Branch `port/m2-claimrcv`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run; finished on `port/m2-claimrcv-t1` after merging the integration head `port/int-7` (see Integration below). Backlog section: [port-backlog.md, M2-CLAIMRCV](../specs/port-backlog.md#m2-claimrcv-related-claims-prompt-on-receive). Feature spec: [features-26.1.md](../specs/features-26.1.md#v26_1-account-claim-receive-related-prompt-prompt-to-send-related-outstanding-claims-when-any-claim-is-received). No schema change, no preference, no registry entry and no fork migration step (the level range 2027004001 to 2027004099 is unused).

Sources: Open Dental's public 26.1 release notes (Account: in Edit Claim, receiving a claim of any ordinal offers to send the related unsent claims) and the published 26.1 database document (`claim.ClaimStatus` H described as hold until prior received; `patplan.Ordinal` up to 10). Nothing else is public about the rules, so the details below marked INFERRED are this fork's choices. All wording is written for this fork.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v26_1-account-claim-receive-related-prompt` | built | Every rule of the spec, with the deviations listed below. |

## What changed

The 24.3 code asked for "secondary claims" (claim type S) after any received claim. The lookup is now "the next insurance": a received primary claim still offers its secondary claims, a received secondary claim offers the tertiary claims, and so on up to ordinal 10. Every 24.3 caller of the prompt gets this without a change of its own, because the generalization sits in the two shared methods they call:

- `Claims.GetPrimaryOrSecondaryClaimsNotReceived(list,isSecondaryClaim:true)` now returns `Claims.GetNextOrdinalClaimsNotReceived(list)`. The medical-to-primary branch (`isSecondaryClaim:false`, preference `ClaimMedReceivedPromptForPrimaryClaim`) is unchanged.
- `ClaimL.PromptForPrimaryOrSecondaryClaim` takes its wording and its force-status preference from `ClaimReceiveRelated`. The choices are the same: change the status to Waiting to Send, send now, print now, or do nothing; "Do nothing" is hidden when the force-status preference is on.

Callers covered that way: Edit Claim (`FormClaimEdit.SaveCleanup`, the release note's trigger), claim creation when the new claim is already received (`ClaimL.CreateClaimFromWrapper`), the ERA claim payment window, the ERA automation report's claim menu item (now "Process Unsent Claims of the Next Insurance", whose check uses the same lookup, see Files touched), and Send Claims after an automatic no-assignment receive. ERA automatic processing (`Etranss`, line 985) still never prompts: it moves the next insurance's claims to Waiting to Send silently, as 24.3 did for secondary claims.

### Rules as built

1. **Trigger.** Unchanged: Edit Claim prompts when the claim is saved as Received after payment was entered, the claim is not a preauthorization, and the user has ClaimSend (checked silently). Claim creation prompts when the lead claim is already received and its secondary claim was just created on hold (24.3 condition; the prompt itself now looks for the next insurance).
2. **Related claims** (`Claims.SelectNextOrdinalClaims`, `OpenDentBusiness/Data Interface/Claims.NextOrdinal.cs`), for each received claim R:
   - every claim has a place in the patient's insurance order (`Claims.GetClaimPlace`): its patplan ordinal for `claim.InsSubNum`; if that patplan is gone (the plan was dropped after the claim was made), the ordinal of its claim type (P 1, S 2) placed just before the plan that holds that ordinal today, because `PatPlans.Delete` moves every later plan up one ordinal; any other claim type whose plan is gone has no known place (never related, and offers nothing when received);
   - candidates are the claims of R's patient, not preauthorizations, with a claimproc (not a preauth claimproc) on one of R's procedures, placed after R;
   - 24.3 order kept: after a received primary claim (type P), a secondary claim (type S) comes directly after it, ahead of any other later insurance, when today's order does not place it after R (plans reordered after their claims were made) or when both plans were dropped (their order is then known only from the claim types);
   - the next insurance is the first candidate place whose claim is not received yet (INFERRED, see below);
   - returned are the candidates at that place with status Unsent (U) or Hold Until Prior Received (H) that have a not-received claimproc on one of R's procedures (the 24.3 claimproc condition).
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

New: `OpenDentBusiness/Data Interface/Claims.NextOrdinal.cs` (`Claims.GetNextOrdinalClaimsNotReceived`, `SelectNextOrdinalClaims`, `IsSecondaryAfterPrimary`, `GetClaimPlace`, `GetClaimOrdinal`, `GetNextOrdinalStatuses`, and the `ClaimReceiveRelated` class), `tests/M2CLAIMRCVTests/`, this note.

Outside the owned list:
- `OpenDentBusiness/OpenDentBusiness.csproj`: one `Compile` line for the new file.
- `OpenDentBusiness/Data Interface/Etranss.cs` (no owner in this run): one condition in `ReceiveEraPayment`, `claim.ClaimType=="P"` to `!="PreAuth"`.
- `OpenDentBusiness/Reports/RpOutstandingIns.cs` (no owner in this run): one filter in `ZeroClaims`, same change.
- `OpenDental/Forms/FormRpEraAutoProcessed.cs` (M2-ERA's file; M2-ERA was merged without the change this note suggested to it, so it is made here): the claim menu item reads "Process Unsent Claims of the Next Insurance" and checks `Claims.GetNextOrdinalClaimsNotReceived(listClaimProcsForClaim,includeHoldForInProcess:true)` instead of looking for a claim of type S, with "next insurance" in its two messages. The order of the checks (claims of the next insurance, then the selected claim received) and the prompt it opens are unchanged. Before, a received secondary claim with an unsent tertiary claim stopped at "none of them are for secondary claims".
- `OpenDental/Resources/PrefInfos.xml` (M2-02's registry): the `ControlText` of `checkPromptForSecondaryClaim` and `checkClaimPrimaryRecievedForceSecondaryStatus` now match the relabelled check boxes. The window matches entries by control name first, so this only keeps the registry in step with the designer.
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.

## INFERRED behaviours

- **Which insurance is "next".** The spec says "the patient's plan with the next higher ordinal". Built as: the first later place that has a claim, not yet received, for the received procedures. So a later plan with no claim for these procedures (for example a medical plan between two dental plans) is skipped, and a later insurance that already received them (claims received out of order) is passed over. A sent or waiting claim at the next place stops the search, so a tertiary claim never jumps ahead of an outstanding secondary claim. When every plan is still on the patient, the ordinals decide: with a medical plan first and the dental primary and secondary after it, receiving the medical claim offers the dental primary claim, then the secondary (with the medical-to-primary prompt off, 24.3 offered the secondary claim straight away there).
- **Dropped plans.** Today's ordinals can disagree with old claims: `PatPlans.Delete` (Drop in Edit Insurance Plan) lowers every later patplan's ordinal by one. The review of the first build found the regression this causes with ordinals alone: once the primary plan is dropped, the secondary plan is ordinal 1, the same as the dropped primary claim's fallback, so receiving the old primary claim offered nothing, the ERA automation no longer moved the H secondary claim to Waiting to Send, and its estimates were not recalculated, where 24.3 offered it. Now a claim whose plan was dropped takes its claim type's ordinal (P 1, S 2) just before the plan that holds that ordinal today (`GetClaimPlace`), which matches a single drop: dropped primary, the secondary claim (today ordinal 1) comes next and the tertiary claim waits for it; dropped secondary, its claim comes after the primary and before the tertiary plan (today ordinal 2), and receiving it offers the tertiary claim. When both the primary and the secondary plans were dropped, the 24.3 rule puts the secondary claim directly after the primary claim (rule 2). This covers Edit Claim, the ERA claim payment window, the ERA automation and the recalculation, which all use the lookup. Known limits (neither is a 24.3 regression, since 24.3 never prompted after a secondary claim): with both plans dropped, receiving the secondary claim does not offer the tertiary claim (receiving the primary claim again does); a tertiary or later claim whose plan was dropped has no known place and is never offered.
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
- **M2-ERA** (owns `FormRpEraAutoProcessed`, `FormEtrans835ClaimPay`; merged on `port/int-7`): the claim payment window needs no change. The automation report's claim menu item used to check for a claim of type S with status U, H or I, so a received secondary claim with an unsent tertiary claim stopped before the generalized prompt. M2-ERA was merged without the change this note suggested, so this package makes it (Files touched). M2-ERA's own lines in `Etranss.cs` (`ClaimProcs.LinkToEraClaim`) and `ClaimProcs.cs` do not touch this package's lines.
- **B-ERA** (merged): `EraAutoSplit.SplitDeniedProcsIfNeeded` in the ERA automation and `Claims.InsertSplitClaimForEra` sit next to this package's lines in `Etranss.cs` and `Claims.cs` without sharing any. A claim split off by the ERA keeps its type and subscriber, so it has the same place as the claim it came from.
- **M2-SDV** (owns `AccountModules.cs`; not merged yet): the Account grid shows H as "Hold until Pri received". Use `ClaimReceiveRelated.GetClaimStatusText(ClaimStatus.HoldUntilPriReceived)` (or `ClaimReceiveRelated.HOLD_UNTIL_PRIOR_RECEIVED` through `Lans.g`) to show the 26.1 text.
- **M2-INSV** (owns `PatPlans.cs`; merged): nothing needed; it does not change `PatPlans.Delete`, the lookup reads patplans through `PatPlans.GetPatPlansForPats` and handles ordinals up to 10.
- **Foundation (M2-02, PrefInfos.xml)**: the `ControlText` entries for `checkPromptForSecondaryClaim` and `checkClaimPrimaryRecievedForceSecondaryStatus` now carry this package's labels. The `ClaimMedReceived*` details still say "Hold Until Pri Received" (they describe the medical-to-primary preferences, whose labels are unchanged, see Deviations).
- `FormClaimPayBatch` (batch insurance payments) still lists only secondary claims (`Claims.GetSecondaryClaims`) when `PromptForSecondaryClaim` is off. Not part of this item; left as is.

## Tests

New project `tests/M2CLAIMRCVTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 50 tests:

- `NextOrdinalSelectionTests` (no database): primary to secondary kept, secondary to tertiary, every ordinal up to 10, sent or waiting next insurance blocks later ones, out-of-order receipts, skipped plans without claims, shared-procedure rule, split claims, preauthorizations, Hold for In Process only for recalculation, other patients, several received claims, dropped plans in the states `PatPlans.Delete` leaves (primary dropped: the secondary plan at ordinal 1 with the tertiary claim waiting for it; secondary dropped: the tertiary plan at ordinal 2, not offered with the secondary claim but after it; both dropped), plans that are all still on the patient (the ordinals decide), reordered plans, the P-then-S exception applying to no other pair, `GetClaimPlace` and `GetClaimOrdinal` fallbacks.
- `ClaimReceiveRelatedTests` (no database): prompt wording, the 24.3 medical-to-primary texts, force-status preferences, the recalculation decision, the "Hold Until Prior Received" text.
- `NextOrdinalDbTests`: scratch MariaDB database (`forkmig_<time>_<pid>_<random>_claimrcv`, fresh 24.3 plus the fork migration runner, dropped afterwards; synthetic patients, invented carriers and Z codes): the lookup the prompt runs, the medical branch, ordinal 5, a primary plan dropped through `PatPlans.Delete` with its claim outstanding (secondary claim offered and recalculated, tertiary claim waiting), a secondary plan dropped the same way (secondary claim offered alone, then the tertiary claim after it), recalculation of the tertiary claim after a secondary claim through `CalculateAndUpdateSecondariesFromPrimaries`, `ReceiveAsNoPaymentIfNeeded` and `ZeroClaims`.
- `WiringTests`: source checks of Edit Claim, the prompt, claim creation, the ERA and zero-claim paths, the ERA report's claim menu item, the preference labels and their `PrefInfos.xml` entries (window code has no test harness).

Related existing assemblies run for this package: `VendorDirectTests` (pins the XConnect validation inside `PromptForPrimaryOrSecondaryClaim`), `ImagingTests` (pins the preauthorization EOB wiring in `FormClaimEdit` and `ClaimL`), and `M2ERATests` and `BERATests` (pin other lines of `FormRpEraAutoProcessed`).

## Integration (port/m2-claimrcv-t1)

- Merged `port/int-7` at `1763cc7ae` (every package merged so far) with no conflicts. Of the files this package changes, B-ERA, M2-ERA, M2-EST and B-PERIO changed `Claims.cs`, `Etranss.cs`, `ClaimProcs.cs` or `FormClaimEdit.cs` elsewhere; none of their lines meet this package's.
- Review finding fixed: the dropped-primary regression (INFERRED behaviours, Dropped plans). The first fix kept the secondary claim after a primary claim only when its ordinal was not above the primary claim's; that still let a tertiary claim come with the secondary claim when the secondary plan was dropped (both at ordinal 2). `GetClaimPlace` orders dropped plans, so the 24.3 exception is now needed only for reordered plans and when both plans were dropped.
- No schema change, no fork migration step and no adoption change, so the adoption tools need nothing for this package.
- Checked on the merged branch: full build of `OpenDental.Local.slnf` through the build lane, `run-tests -Mode Core` (61 assemblies, every test passed, including `M2CLAIMRCVTests` 50/50, `M2ERATests`, `BERATests`, `VendorDirectTests` and `ImagingTests`), and `tools/Test-NoHqHosts.ps1`.

Not tested: the prompt window itself (an `InputBox` dialog) and the Preferences window layout; they were not opened, because the GUI is not launched in package work.

## Questions for the owner

1. Is "the next insurance that has a claim, not yet received, for the received procedures" the rule you want, or strictly patplan ordinal plus one?
2. Claims on Hold for In Process (I) are recalculated but never changed, sent or printed by the prompt. Confirm.
3. Should a next-insurance claim that the prompt sends and that is then received automatically (no assignment of benefits) chain a prompt for the insurance after it? 24.3 does not, and neither does this build.

## Integration (branch `port/int-9`)

- Merged last of this round onto `port/int-8` (B-CLAIM, then M2-TERM, M2-SHEETS and V-EDS), with no conflicts.
- **B-CLAIM** (on `port/int-8`): `ClaimL.PromptForPrimaryOrSecondaryClaim` keeps both packages' changes: this package's generalized wording and force-status preference, and B-CLAIM's `ClaimAttaches.SetClaimsWaitingToSend` (two-argument `Claims.Update`, so the claims keep their attachments, the medical EOB among them). Its `doSaveToDb` rule follows the recalculation preference, so with "Auto update next insurance claim estimates" on, a tertiary claim's attachments are loaded before `Claims.CalculateAndUpdateSecondaries` saves it, the same as a secondary claim's. The Medical EOB prompt still runs just before the medical-to-primary prompt in Edit Claim, ERA posting and claim creation (`BCLAIMTests.HookSourceTests` passes). B-CLAIM's owner question 6 (`Claims.CalculateAndUpdate` saves with the one-argument `Claims.Update`, dropping the attachments of claims recalculated by `CalculateAndUpdateSecondariesFromPrimaries`) now covers the next insurance's claims of any ordinal, because this package widened that lookup; it is left to the owner, unchanged.
- **V-EDS** (integration commit): "Send claim(s) now" validates the claims that go to EDS before sending (`EdsValidationL.FilterQueueItemsForSend`, right after the XConnect filter); see V-EDS's Integration (branch `port/int-9`). The EDS window's "no longer waiting to send" message shows status H through `ClaimReceiveRelated.GetClaimStatusText` ("Hold Until Prior Received").
- No fork migration step, schema or row change, so the adoption tools need nothing new. On the merged head: one full build of `OpenDental.Local.slnf` through the build lane (0 errors), `run-tests -Mode Full` (69 assemblies, 3642 tests, none failed; the 2 skipped are M201Tests' published-document checks, skipped as on earlier heads) and `tools/Test-NoHqHosts.ps1` (passed). `M2CLAIMRCVTests` 50 of 50, `BCLAIMTests` 54, `M2ERATests` 44, `BERATests` 73, `VendorDirectTests` 873, `ImagingTests` 43.
