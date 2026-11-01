# Port note: M2-ERA, ERA report filters and claimproc-to-ERA link

Branch `port/m2-era`, cut from `port/foundation` (M1 complete plus M2-01 and M2-02). Built in the single-wave run, beside the other M1 Tier B and M2 packages.

## Sources

Public sources only, as the owner decided: the 25.4 release notes (<https://www.opendental.com/site/version25_4.html>), the 24.3 manual page of the report (<https://www.opendental.com/manual243/reporterasautomaticallyprocess.html>) and the 26.1 diff document (<https://www.opendental.com/OpenDentalDiffDocumentation26-1.xml>), through the specs in `docs/specs/features-25.4.md` and `docs/specs/features-26.1.md`. No Open Dental program was installed, observed or decompiled, and no office data was used. Everything the sources do not say is marked INFERRED below.

## Features

| ID | Status | Notes |
|---|---|---|
| `v25_4-reports-era-autoprocessed-filters` | built | ERAs Automatically Processed report: **Show PreAuths** and **Show $0 Payments**, both checked by default (24.3 results). Unchecking hides ERAs whose claims are all predeterminations, or whose total payment is $0. Remembered per user for the session (deviation: not in `userodpref`, see below). |
| `v26_1-account-era-claimproc-link` | built | `claimproc.Etrans835AttachNum` (M2-01's column) is set on the claimprocs an ERA enters, manual and automatic, and read first by `Hx835_Claim.IsProcessed`, so the ERA window, the report, the ERA status and automatic processing see each ERA claim's own payment. Cleared when the claimproc is un-received, the ERA claim is detached or re-attached to another claim, or the ERA or claim is deleted. Deviation: not cleared when the claim payment (check) is deleted, see below. |

No schema change (M2-01 added the column and its key), no migration step (the level range 2027003801-2027003899 is unused), no preference, no permission, no registry entry.

## What was built

### ERA report filters (25.4)

- `OpenDental/Forms/FormRpEraAutoProcessed.cs` and `.Designer.cs`: two checkboxes at the top right (`checkShowPreAuths`, `checkShowZeroPayments`), part of the window's filter controls, so ticking one refreshes the list like the other filters. The window passes them to `EtransL.GetEraDataFiltered` and remembers them.
- `OpenDental/Logic/EtransL.cs`, `GetEraDataFiltered`: two optional parameters (default true, so the ERA list `FormEtrans835s` and every other caller is unchanged); when either is false it calls the business filter after the query.
- `OpenDentBusiness/Data Interface/Etrans835s.cs`:
  - `RemoveHiddenForAutoProcessedReport(listEtrans,listEtrans835s,doShowPreAuths,doShowZeroPayments)` removes hidden ERAs from both lists at the same index. The ERA text is loaded (one query, `EtransMessageTexts.GetMessageTexts`) only when PreAuths are hidden.
  - `IsHiddenForAutoProcessedReport(...)`: $0 is `etrans835.InsPaid` (BPR02, the ERA's total payment) equal to zero; a negative total (a reversal that takes money back) is not $0.
  - `IsPreauthOnly(messageText835,tranSetId835)`: at least one claim and every CLP02 is 25 (Predetermination Pricing Only), the same test as `Hx835_Claim.IsPreauth`. Reads the X12 segments only (no claim matching, no database). It looks at the EOB the etrans stands for (ST02 = `TranSetId835`, or the first EOB, as `X835` does); an etrans from 14.2 and older with a blank `TranSetId835` stands for every EOB in the file, and all of them must be preauth-only.
  - `EraAutoProcessedReportFilters`: the per-user memory (UserNum to the two choices), in memory for the life of the program.

### Claimproc-to-ERA link (26.1)

The link is the `etrans835attach` row of the ERA claim: one 835 (EtransNum), one CLP loop (ClpSegmentIndex), attached to one claim.

- Setting it: `Etranss.TryImportEraClaimData` is the one place where an ERA claim's payment is put on claimprocs, for the manual "enter payment" flow of the ERA window (`EtransL.ImportEraClaimData`, then `FormEtrans835ClaimPay`) and for automatic processing alike. One hook line there calls `ClaimProcs.LinkToEraClaim`, which sets the link in memory on the claimprocs it entered and on the By Total line when there is one. `Etranss.ReceiveEraPayment` saves them (the automatic path at once; the manual path when the user clicks OK in the ERA payment window, so a cancelled window stores nothing). Only claimprocs with a status an ERA posts (Received, Supplemental, Preauth, CapClaim) get it.
- Hand-entered claimprocs have 0: claimprocs start with 0, and `ClaimProcs.CreateSuppClaimProcs` (the Edit Claim window's Supplemental button, which copies received claimprocs) now sets 0 on the copy. An ERA import that creates supplementals links them afterwards.
- Clearing it (`OpenDentBusiness/Data Interface/ClaimProcs.Era.cs`):
  - un-received: `ClaimProcs.Update` calls `ClearEtrans835AttachNumIfNotReceived` before it saves, so a claimproc set back to Not Received (or any status an ERA does not post) loses the link, from any window;
  - detached: `Etrans835Attaches.DeleteMany` (both overloads) clears the claimprocs that point at the rows it deletes. That covers detaching the ERA claim (`DetachEraClaim`), attaching it to another claim (`CreateForClaim`), deleting the ERA (`Etranss.Delete835`), deleting the claim (`Claims.Delete`) and the database maintenance tool that removes orphaned rows.
  - `CreateForClaim` deletes and inserts the attach row again when it is asked for a new one (`isNewAttachNeeded`). When the new row is for the same claim, the claimprocs that were linked to the old row are moved to the new one. The query runs only when the in-memory list shows such a row (not on a new ERA import).
- Reading it: `Hx835_Claim.IsProcessed` (X835.cs) asks `EraClaimProcLinks` (new, `OpenDentBusiness/X12/EraClaimProcLinks.cs`) first:
  1. If a claimproc of the claim (or of a claim split from it in the ERA payment window) is linked to this ERA claim and still received, and no procedure claimproc of the claim waits as Not Received, the ERA claim is processed.
  2. Otherwise the claimprocs linked to a different ERA claim (an attach row that exists) are left out, and the rest go through the matching that 24.3 used. Claimprocs without a link (rows from before the upgrade, or entered by hand) and claimprocs whose row no longer exists keep the old behaviour exactly.
  - The ERA claim's own rows include the CLP loops the 835 split off (`Hx835_Claim.IsSplitClaim`), which are entered together, and the rows of claims split from it in the ERA payment window (same 835 and CLP loop, another ClaimNum).
  - `Hx835_ShortClaimProc` carries `Etrans835AttachNum` (constructor, `RefreshForClaims` query, `TableToList`).
  - Through `IsProcessed` the link drives the "Recd" column of the ERA window (`FormEtrans835Edit`) and of the report, `X835.GetStatus` (the stored ERA status), the "already processed" check of automatic processing, and the ERA window's choice between opening the claim and entering payment.
- Warnings:
  - `EtransL.ImportEraClaimData` reads the database again before entering payment and asks "Payment from this ERA claim was already entered on this claim. Entering it again would post the payment twice. Continue?" when claimprocs are linked to this ERA claim (a window open on two workstations, or a claim with one procedure set back to Not Received).
  - Same payment imported twice: with the link, a second copy of the same 835 no longer looks processed (its claimprocs came from the first copy). So that automatic processing does not post it again, `EraAutomationResult.CanClaimBeAutoProcessed` holds a claim back with "The claim was already paid from another ERA with the same check or EFT trace number. This ERA may have been received twice." when the claim's received claimprocs came from a different ERA whose stored `etrans835` has the same trace number (TRN02) and payer name. The ERA window asks the same as a Yes/No question. A real later payment, reversal or correction comes with its own payment (another trace number), so it is not affected; a claim twice in one ERA is the same etrans and is not affected either.

## INFERRED behaviours

- 25.4 filters: "all claims are predeterminations" means every CLP02 = 25 (an ERA with no claims is never hidden); "$0 payment" means BPR02 = 0 (negative totals are not hidden); the checkbox texts and placement; that an old multi-EOB etrans is hidden only when every EOB is preauth-only.
- 26.1 link: which claimprocs are "entered from the ERA" (the claimprocs `TryImportEraClaimData` fills plus the By Total line); the statuses that keep it (Received, Supplemental, Preauth, CapClaim); that the CLP loops of an 835 split claim share one link (the first loop's row); that linked-and-received makes the ERA claim processed unless a procedure of the claim is Not Received; that claimprocs linked to another existing ERA claim never count for this one; that a link to a row that no longer exists counts as no link; re-pointing the link when the attach row is made again for the same claim; the double-post warning and the same-payment check (trace number and payer) and their texts.

## Deviations from the spec

- **Not cleared when the claim payment is deleted** (spec rule 2, second item). Deleting the insurance check leaves the claimprocs received with the ERA's amounts, so they still come from the ERA claim, and the ERA window must still show it as processed. Clearing would also fire on every cancelled finalize: `FormClaimEdit.FormFinalizePaymentHelper`, which the ERA window's "Finalize Payment" uses, deletes the new claim payment when the user cancels, so every cancel would have wiped the links of the whole ERA. The link is cleared when the payment is really taken back (the claimprocs are un-received). Test: `ClaimPaymentDeleted_LinkKept`. This also left `ClaimPayments.cs` (V-PAYSUITE's file) untouched.
- **Per-user memory is for the session only.** A `userodpref` row needs a new `UserOdFkeyType` value, which belongs to the registry packages (M2-02), and the fork-only convention used by B-EF (100 plus the package's migration range) gives 138 here, above the column's tinyint limit of 127. The choices are kept per user in memory (`EraAutoProcessedReportFilters`) until the program closes, like B-TIME and B-PROC did. Owner question 1.
- `FormEtrans835ClaimPay`, `FormEtrans835ClaimSelect` and `FormEtrans835Edit` are in the owned list and touchpoints but needed no change: the link is set in `TryImportEraClaimData`, which the ERA payment window's claimprocs come from, and the windows read it through `IsProcessed`. Leaving them alone also keeps clear of B-ERA's and B-CLAIM's edits to the same forms.
- The extra same-payment check in automatic processing is not in the spec; it keeps the link from turning a duplicate ERA into a double posting (see Warnings).

## Files

New: `OpenDentBusiness/Data Interface/ClaimProcs.Era.cs` (the backlog's partial file), `OpenDentBusiness/X12/EraClaimProcLinks.cs`, `tests/M2ERATests/` (project, `EraDb.cs`, `Era835Builder.cs`, `ReportFilterRuleTests.cs`, `ReportFilterDbTests.cs`, `ClaimProcLinkRuleTests.cs`, `ClaimProcLinkDbTests.cs`, `HookSourceTests.cs`), this note.

Changed, owned by M2-ERA: `OpenDentBusiness/Data Interface/Etrans835s.cs`, `OpenDentBusiness/Data Interface/Etrans835Attaches.cs`, `OpenDentBusiness/X12/X835.cs` (the `IsProcessed` hook, `Hx835_ShortClaimProc`, one check in `CanClaimBeAutoProcessed`; edited byte for byte, the file is Windows-1252), `OpenDental/Logic/EtransL.cs`, `OpenDental/Forms/FormRpEraAutoProcessed.cs` and `.Designer.cs`.

Changed outside M2-ERA's list (also in the report):

- `OpenDentBusiness/Data Interface/ClaimProcs.cs` (hot file; M1-02 made it partial, B-CLAIM edits it in this wave): two lines. `Update` calls `ClearEtrans835AttachNumIfNotReceived(claimProc)` before the CRUD update; `CreateSuppClaimProcs` sets `Etrans835AttachNum=0` on the copy. Neither is near B-CLAIM's hunks (lines 754 and 1855 of the base).
- `OpenDentBusiness/Data Interface/Etranss.cs` (B-CLAIM; B-ERA also edits it): one line in `TryImportEraClaimData`, `ClaimProcs.LinkToEraClaim(...)`, right before `if(isAutomatic) {`. B-CLAIM adds a line after the `ReceiveEraPayment` call two lines below, B-ERA adds lines near line 958; two unchanged lines separate the hunks.
- `OpenDentBusiness/OpenDentBusiness.csproj`: two `Compile` lines, placed next to `Etrans835s.cs` and `Etrans835Attaches.cs` (away from the lines other packages add after `ClaimProcs.cs` and `X277.cs`).
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.

## Interplay with packages built at the same time

- **B-ERA** (M1 Tier B, in this wave): edits `X835.cs` (`GetCarcCodesNoAutoProcessForClaim`, `EraAutomationResult` made partial, the CARC message at the top of `CanClaimBeAutoProcessed`, `CreateMessage`), `Etranss.cs` (auto split before `CanClaimBeAutoProcessed`), `FormEtrans835Edit`, `FormEtrans835ClaimSelect` and `FormRpEraAutoProcessed` (a "CARC Hold" column in the claims grid). The hunks of this branch are elsewhere in each file: the `IsProcessed` hook sits about 50 lines above B-ERA's `GetCarcCodesNoAutoProcessForClaim` change, the same-payment check sits after the "already processed" check about 25 lines below B-ERA's CARC lines, and the report changes are in the load, defaults and ERA grid code, not the claims grid. Behaviour: B-ERA's auto split moves denied procedures to a new claim before `TryImportEraClaimData` runs; the remaining claimprocs are linked to the original claim's row, the split claim's claimprocs stay Not Received without a link, and the ERA claim shows processed (as B-ERA's `AutoSplit_EraStillPointsAtTheOriginalClaimAndShowsProcessed` expects). Its "CARC Hold" column uses `IsProcessed` and so follows the link.
- **B-CLAIM**: edits `ClaimProcs.cs`, `Etranss.cs` and `FormEtrans835ClaimPay.cs` (allowed-override audit after `ReceiveEraPayment`, medical EOB prompt). The link field is not audited (it is not a payment amount). No overlap with the lines here.
- **M2-01**: owns the column, its key and the CRUD. `ClaimProc.Equals` does not compare `Etrans835AttachNum`, so `ClaimProcs.Synch` does not save a change of only the link; nothing here relies on `Synch` for it (the ERA path saves with `ClaimProcs.Update`/`Insert`, the clearing paths use their own SQL or `Update`).
- **M2-CLAIMRCV** (`Claims`, `FormClaimEdit`, `ClaimL`): no shared lines. Receiving a claim from the Edit Claim window enters claimprocs by hand (no link).
- **V-PAYSUITE** owns `ClaimPayments.cs`; not edited (see the deviation).

## Tests

New project `tests/M2ERATests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`). 44 tests.

- `ReportFilterRuleTests` (no database needed): `IsPreauthOnly` (all preauth, mixed, no claims, not X12, EOB choice for new and old etrans, agreement with `X835`), `IsHiddenForAutoProcessedReport` combinations, list removal in step, the per-user memory.
- `ClaimProcLinkRuleTests` (in-memory ERAs): linked claim processed even after a hand change of the amount (the old matching says not processed); unlinked rows keep the old matching; links to missing rows fall back; the same claim in two ERAs (the old matching took the first ERA's claimprocs for the second); the same claim twice in one ERA (reversal and correction); an 835 split claim entered together; a procedure still Not Received; the status rules; `IsSamePayment`.
- `ClaimProcLinkDbTests` and `ReportFilterDbTests`: a scratch MariaDB database on 127.0.0.1:3306 built like `tools/New-DevDatabase.ps1` and upgraded by the fork migration runner (unique name `forkmig_<time>_<pid>_<random>_m2era`, dropped at the end; synthetic patients, Z codes, payer and 835s). Automatic processing and the manual flow (`TryImportEraClaimData` + `ReceiveEraPayment`, including the By Total line) store the link; a cancelled manual entry stores nothing; Supplemental copies and a claim received by hand have 0; un-receiving, detaching, deleting the ERA and attaching to another claim clear it; making the row again for the same claim keeps it; deleting the claim payment keeps it; a later ERA for the same claim stays unprocessed until its own payment is entered (and the old behaviour returns when links are 0); a second copy of the same payment is held back by automatic processing; reversal and correction in one ERA are entered on their own; the report filter reads the ERA text from the database.
- `HookSourceTests`: the hook lines in `X835.cs`, `ClaimProcs.cs`, `Etranss.cs`, `Etrans835Attaches.cs`, `EtransL.cs` and the report window are present and in the right order, so a merge that drops one fails here.

Targeted runs: `M2ERATests` 44/44, plus `InsuranceTests`, `ImagingTests` and `ReportTests` (they exercise claimproc updates, which now pass through the `Update` hook) all passing; `tools/Test-NoHqHosts.ps1` passes.

Not tested automatically: the two checkboxes on screen (the window was not opened; the wiring is covered by `HookSourceTests` and the filter by the business tests), the two Yes/No prompts of `EtransL` (UI), and Middle Tier.

## Questions for the owner

1. Should the report's Show PreAuths and Show $0 Payments choices survive a restart? That needs a `UserOdFkeyType` value from the registry owner (the B-EF convention would give 138, above the tinyint column's 127).
2. The spec asks to clear the link when the claim payment is deleted; this branch keeps it (see the deviation). Confirm.
3. Automatic processing now holds back a claim whose payment came from another ERA with the same trace number and payer (a duplicate import). Keep this check, or post such claims as before?

## Integration (branch `port/int-6`)

- Merged without conflicts. B-ERA (merged on `port/int-1`) asked for the 26.1 link to be written for the claimprocs automatic processing posts after an auto split; it is, because the split runs before `TryImportEraClaimData`, which sets the link. New test `BERATests.EraAutoProcessDbTests.AutoSplit_PostedClaimProcsAreLinkedToTheEraClaim`: the paid claimproc points at the original claim's `etrans835attach` row, and the claimproc split off to the new claim stays Not Received with no link. B-ERA's `AutoSplit_EraStillPointsAtTheOriginalClaimAndShowsProcessed` passes through the linked `IsProcessed`.
- `tests/BERATests` and `tests/M2ERATests` each keep their own `EraDb` and `Era835Builder`: they are test scaffolding of two test projects with different needs (M2-ERA's builder writes preauthorizations, claim statuses and trace numbers), not a shared product helper.
