# Port note: B-ERA (ERA processing and ERA display fields)

Branch `port/b-era`, cut from `m1/integration-a2-patch`. Backlog section: [port-backlog.md, B-ERA](../specs/port-backlog.md). Built in the single-wave run, at the same time as the other packages, so the hooks into shared files are kept to a few lines and the logic lives in new files.

Sources: public only. The Open Dental release notes for 24.4, 25.1, 25.2 and 25.4 (the ERA display fields were backported to 25.3.78) and the published database documentation (`DisplayFieldCategory.ERA = 23`). Nothing below copies Open Dental text. Where the public sources say nothing, the behaviour is this fork's choice and is marked **INFERRED**.

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-manage-era-mismatch-match` | built | Already allowed in our 24.3.43 code (OK/Cancel instead of a hard stop). The warnings now show the claim's value and the value the ERA expects. Payment entry for a mismatched claim checked by test. |
| `v25_1-manage-era-autosplit-carc` | built | New option "Auto split ERAs with CARC denial codes listed above" (off by default). Automatic processing moves the denied procedures to a new unsent claim and processes the rest. |
| `v25_2-manage-era-carc-logic` | built (design is ours) | GROUP-CODE entries (`CO-97`), $0.00 adjustments never hold a claim back, the check runs on each claim after a split, and the message and the report name the matching entry ("CO-97 on procedure Z0220"). Save-time validation and a tooltip. |
| `v25_3-account-era-displayfields` | built | Setup, Display Fields, category ERA (23): the office chooses, orders, sizes and renames the columns of the Claims Paid grid of the ERA window. |
| `v25_4-claims-era-displayfields` | built here (implement once) | The 25.4 detail: the 24.3 columns are the defaults; Recd and Patient can't be removed; fork extras Subscriber ID, Allowed and Matched Claim. M2-VERIFY only needs to compare. |

### v24_4-manage-era-mismatch-match

- `FormEtrans835ClaimSelect.butOK_Click` uses `EraClaimMismatch` (new, `OpenDentBusiness/X12/EraClaimMismatch.cs`) for the fee and date checks and the warning texts. The fee warning shows the claim fee and the ERA's CLP03; the date warning shows the claim's date and the ERA's service date range; a split ERA claim (several CLP segments) shows the ERA fee its procedures should add up to.
- The checks are the 24.3 ones, moved into the business layer so they can be tested: a reversal's fee compares by absolute value; a preauthorization whose ERA reports 01/01/1900 never mismatches.
- Verified, not changed: `FormEtrans835ClaimSelect.FillClaimDetails` shows ERA lines that match no procedure in red with IsMatch "N". Payment entry (`Etranss.TryImportEraClaimData`, manual mode) leaves an unmatched procedure at zero and puts the unmatched ERA line's money on a By Total row the user must allocate; automatic mode refuses such a claim. Both are covered by `MismatchedClaim_ManualEntryPutsUnmatchedLinesOnATotalRow_AutomaticRefuses`.

### v25_1-manage-era-autosplit-carc

- **Preference.** A checkbox under "Don't auto-process claims w/ these CARCs" in Setup, Preferences, Manage (`UserControlManageGeneral`). It is disabled while the code list is empty, and saving with an empty list turns it off.
- **Storage (deviation).** Open Dental's real preference name is not public, and `tests/RegistryTests/PrefNameTests` forbids registering the proposed name `EraAutoSplitCarcDenials` in `PrefName` until it is confirmed. So the setting is a fork-owned preference row, `ForkEraAutoSplitCarcDenials`, outside the `PrefName` enum (the same pattern as `ForkDxcPartnerClient`): the row is written the first time the office saves the setting, and a missing row reads as off. No migration step and no `Pref.cs` or `PrefInfos.xml` change was needed, so the assigned level range 2027000401 to 2027000499 is unused. An office that turned the option on in Open Dental 25.1 or later has to turn it on again here.
- **Rules** (`EraAutoSplit.GetPlan` and `EraAutoSplit.SplitDeniedProcsIfNeeded`, new file `OpenDentBusiness/X12/EraAutoSplit.cs`), for each matched ERA claim during automatic processing (Auto Process button and automatic processing at import):
  - no listed CARC: processed as before;
  - option off: not processed, as in 24.3;
  - a listed CARC on a claim-level adjustment: not split, not processed;
  - every procedure line carries a listed CARC: not split, not processed;
  - a preauthorization, a reversal, or a claim the payer reported in several CLP segments: never split;
  - the claim is already received, or a procedure on it is already received, or it is a preauth or capitation claim: never split;
  - posting the rest of the claim would need a By Total row, which automatic processing refuses (for example an adjustment on the claim itself that is not listed, such as OA-23 $5, so the claim payment differs from the sum of the lines): not split, not processed;
  - otherwise the lines with listed CARCs are split off.
- **Split.** `Claims.InsertSplitClaimForEra` (new, in `Claims.cs`) runs the same steps as the Split Claim button of the ERA payment window: `Claims.InsertSplitClaim`, an `etrans835attach` row for the new claim on the same CLP, and the multi-visit groups of the claim's procedures. The new claim is set to Unsent (U). The rest of the claim is then posted from a copy of the ERA claim without the split lines (`EraAutoSplit.GetRemainingClaim`), so the denied lines are not looked for on it and the claim totals still balance.
- **Notes and audit.** One line per split ("Auto split: patient, claim #A: Z0220 moved to new unsent claim #B (CO-97 on procedure Z0220).") goes into the automatic processing message (`Claims Split Automatically: n`), on top of the etrans note (the Note column of the ERA list), and into a `ClaimEdit` security log entry with the original ClaimNum as FKey.
- **When a split is refused,** the CARC message says why ("Auto split was not possible: ...").

### v25_2-manage-era-carc-logic

Only "additional logic" is public. Built as proposed in the spec (`OpenDentBusiness/X12/EraCarcRules.cs`):

1. **GROUP-CODE entries.** A bare entry (`97`) matches the reason code under any group code, as in 24.3; `CO-97` matches only that pair. Group codes CO, PR, OA, PI and CR. Entries are trimmed and compared without regard to case (24.3 compared them exactly). An entry that is not GROUP-CODE is kept whole, so every value 24.3 accepted matches what it matched before.
2. **$0.00 adjustments** never hold a claim back. Note: our 24.3 835 parser (`X835.ProcessCAS`) already drops CAS amounts of zero, so this rule only matters for adjustments built some other way.
3. **Each resulting claim is checked on its own.** After a split, the no-auto-process check and every other automatic processing check run on the remaining claim. A split happens only when the remaining claim passes all of them, all its lines match a procedure, and its payment needs no By Total row (`EraAutoSplit.WouldNeedByTotalPayment`, the same arithmetic as `Etranss.TryImportEraClaimData`), so a split is always followed by its payment. All these checks run before anything is written to the database.
4. **The matching entry is named:** in the automatic processing message ("CO-97 on procedure Z0220", "PR-16 on the claim") and in a new last column, "CARC Hold", of the claims grid in the ERAs Automatically Processed report (filled for claims that are not received).
- **UI.** A tooltip on the text box explains the syntax. Saving checks every entry: 1 to 5 letters or digits, optionally preceded by CO, PR, OA, PI or CR and a dash.

### v25_3-account-era-displayfields and v25_4-claims-era-displayfields

- `DisplayFields` is now `partial`; the ERA lists and helpers are in the new `OpenDentBusiness/Data Interface/DisplayFields.Era.cs`, reached from `GetDefaultList` and `GetAllAvailableList` through two early returns.
- Internal names are the 24.3 column titles (`Recd`, `Patient`, `DateService`, `Claim Identifier`, `Payor Control#`, `Status`, `ClaimFee`, `InsPaid`, `PatPort`, `Deduct`, `Writeoff`); the default list is that set in 24.3 order and widths. Extras: `Subscriber ID`, `Allowed`, `Matched Claim`.
- Recd and Patient can't be removed: `FormDisplayFields` refuses (through the category-neutral `DisplayFields.IsRequiredField`), and the ERA window puts them back if a stored list lacks them.
- Rows are stored like every other category (`displayfield` with `Category=23`). In this version display fields are not per clinic or per user, so neither are these.
- `FormEtrans835Edit` builds `gridClaimDetails` from the list (description, width, order) and fills cells by internal name. Patient and Status take up the free width as in 24.3; when the chosen columns are wider than the grid, the grid scrolls sideways instead. The window's "is everything received" check on close now finds the Recd column by name, because the office can move it.
- `FormDisplayFieldCategories` no longer lists a category that has no fields in this program (today only placeholder 22, the 25.4 Account service date view), and lists it as soon as its package adds fields.

## Files

Owned by B-ERA: `OpenDentBusiness/Data Interface/Claims.cs` (two methods added after `InsertSplitClaim`), `OpenDentBusiness/Data Interface/DisplayFields.cs` (`partial` and two early returns), `OpenDentBusiness/X12/X835.cs` (CARC check, `partial` on `EraAutomationResult`, one line each in the CARC message and the automatic processing message), `OpenDental/Forms/FormDisplayFieldCategories.cs`, `FormDisplayFields.cs`, `FormEtrans835ClaimSelect.cs`, `FormEtrans835Edit.cs`, `FormRpEraAutoProcessed.cs`, `OpenDental/User Controls/Preferences/UserControlManageGeneral.cs` and `.Designer.cs`. `FormEtrans835ClaimPay.cs` is owned but unchanged.

New: `OpenDentBusiness/X12/EraCarcRules.cs`, `EraAutoSplit.cs`, `EraClaimMismatch.cs`, `OpenDentBusiness/Data Interface/DisplayFields.Era.cs`, `tests/BERATests/` (new test project), this note.

Outside the owned list:

- `OpenDentBusiness/Data Interface/Etranss.cs`: one statement in `TryAutoProcessEraEob`, just before `CanClaimBeAutoProcessed`, that calls `EraAutoSplit.SplitDeniedProcsIfNeeded` and replaces the ERA claim with the remaining copy. The automatic processing loop lives there, so the feature can't work without it. B-CLAIM owns this file later (B2).
- `OpenDentBusiness/OpenDentBusiness.csproj`: four `Compile` lines for the new files, next to `X12\X835.cs` and `Data Interface\DisplayFields.cs`.
- `OpenDental.sln` and `OpenDental.Local.slnf`: the new test project.

Not changed: `Pref.cs`, `PrefInfos.xml`, `DisplayField.cs` (the enum value 23 was already there from M1-01), CRUD, migrations.

## INFERRED behaviours

- The auto split never splits a line that has a listed CARC but still carries an insurance payment (not a plain denial; splitting it would lose the payment and the ERA total would no longer match the claim payment). Such a claim is held back as in 24.3, with the reason.
- The new claim is Unsent (U), as the spec says; its date sent and other fields are whatever `InsertSplitClaim` copies (the same as a manual split). Its claim identifier is a new one (`ClaimIdPrefix` plus ClaimNum), as for a manual split.
- A split is made only when the remaining claim passes every other automatic processing check; otherwise nothing changes and all the reasons are reported for the whole claim.
- The By Total check before a split repeats the arithmetic of `TryImportEraClaimData` for an original payment where every line matches a procedure: the remaining claim's deductible, allowed amount and insurance payment, less the sums of its lines, must each round to $0.00, and so must the write-off when `EraAutoPostWriteOff` posts write-offs for the claim (`EraAutoSplit.CanAutoPostWriteOff`; a negative difference counts as zero, as there). Because the split lines' amounts are taken out of both the claim totals and the lines, this is the same as asking whether the whole ERA claim balances. When it does not, the claim is held back with "Auto split was not possible: the rest of the claim would need a By Total payment." The arithmetic is copied, not shared, because `Etranss.cs` is B-CLAIM's file; if B-CLAIM changes the By Total rule in `TryImportEraClaimData`, `WouldNeedByTotalPayment` must follow.
- Security log: `ClaimEdit` (the spec's proposal), FKey = original ClaimNum, new ClaimNum in the text.
- `etrans835attach` rows are ordered by `DateTimeEntry` and the first row of a CLP is the claim the ERA pays. An automatic split inserts its row within the same second as the original attach, so `Claims.MoveEraSplitAttachAfterOriginal` moves the split row one second later when needed.
- ERA display fields: the internal names are the 24.3 column titles (Open Dental's names for 25.3.78+ are not public). A stored row with a name this program does not know is ignored when the grid is drawn (never deleted); if nothing usable is left the defaults are used.
- The extras' content: Subscriber ID is the NM1*IL ID (or NM1*QC when there is none); Allowed is the claim's insurance paid plus patient responsibility; Matched Claim is the attached ClaimNum, or "Detached".
- CARC entries are compared without regard to case and trimmed. An entry `PI-45` will not match a payer's PI-45, because the 24.3 parser already rewrites group PI with reason 45 to CO.

## Interplay with packages built in parallel

- **B-CLAIM** (owns `Etranss.cs`, `ClaimProcs.cs`, `FormEtrans835ClaimPay.cs` in B2): keep the one-statement hook in `TryAutoProcessEraEob` (a wiring test checks it runs before `CanClaimBeAutoProcessed` and the import). `EraAutoSplit.WouldNeedByTotalPayment` and `CanAutoPostWriteOff` repeat the By Total and write-off rules of `TryImportEraClaimData`; a change to those rules there must be made in both places (or `TryImportEraClaimData` can call the two helpers). If B-CLAIM wants the manual Split Claim button to share code with the automatic one, `butSplitProcs_Click` can call `Claims.InsertSplitClaimForEra(_claim,listClaimProcsSelected,ListClaimProcs,_x835.EtransSource.EtransNum,_hx835_Claim.ClpSegmentIndex,isUnsent:false,out _)`; I left that window untouched to avoid a collision. If B-CLAIM adds claim-procedure audit entries, the automatic split already writes a `ClaimEdit` entry.
- **M2-ERA** (X835, `FormRpEraAutoProcessed`, the ERA forms, `Etrans835Attaches`): the report's claims grid has a new last column "CARC Hold"; add new filters around it. The 26.1 claimproc-to-ERA link should also be written for the claimprocs posted after an auto split (they are posted by the same `TryImportEraClaimData` call). `GetCarcCodesNoAutoProcessForClaim` now returns match descriptions, not bare codes.
- **M2-02** (DisplayFields.cs available lists, 25.4/26.1 preferences): `DisplayFields` is now `partial`; put new categories' lists in the existing switch as usual (the ERA early returns sit before the switch). When the census or the owner confirms Open Dental's name for the auto split preference, register it in `PrefName`, seed it, and switch `EraAutoSplit.IsEnabled`/`SetEnabled` to it (copying the fork row's value once). The comment on `DisplayFieldCategory.ERA` in `DisplayField.cs` ("No fields yet in this program") is now stale; it is a foundation file, so I left it.
- **M2-SDV** (`FormDisplayFieldCategories`, `FormDisplayFields`, category 22): category 22 is hidden until `GetAllAvailableList` returns fields for it. `FormDisplayFields.butRight_Click` now refuses to remove fields for which `DisplayFields.IsRequiredField` is true; add category 22's required fields there if it has any.
- **M2-CLAIMRCV** (`Claims.cs`): two new methods after `InsertSplitClaim`, nothing else changed.
- **M2-VERIFY**: `v25_4-claims-era-displayfields` is implemented here with the 25.4 detail.

## Questions for the owner

1. What is Open Dental's preference name for "Auto split ERAs with CARC denial codes listed above" in the office database (census M1-00)? Until it is known, the office must turn the option on again in this program (it is stored as `ForkEraAutoSplitCarcDenials`). **Answered (2026-11-07, owner question Switch-over 1):** `EraAutoProcessSplitByCarcCodes`; the code reads it now (see "Open Dental's name" below).
2. Should a line that carries a listed CARC but still has an insurance payment be split off too (today: never; the claim is held back)?
3. Is Unsent the right status for the new claim, or should it keep the original's status like a manual split (the spec says Unsent)?
4. Are the three extra ERA columns (Subscriber ID, Allowed, Matched Claim) useful, or should the ERA category offer only the 24.3 set?
5. Should the manual Split Claim button also write the `ClaimEdit` audit entry (today only the automatic split does)?

## Tests

New project `tests/BERATests` (MSTest v1, x86, .NET 4.8), in `OpenDental.sln` and `OpenDental.Local.slnf`:

- `EraCarcRulesTests`, `EraAutoSplitPlanTests`, `EraDisplayFieldsTests`, `EraClaimMismatchTests`: no database.
- `WiringTests`: read the sources of the windows that can't be opened in a test run and of the shared-file hooks.
- `EraAutoProcessDbTests`: a scratch database (`forkmig_<run id>_bera`, dropped afterwards) built from the 24.3 dump and the fork migration runner, synthetic patients, Z codes and a hand-built 835 (its CLP04 is the lines' payment less the claim-level adjustments, as a payer balances it): the auto split end to end (split claim, payment finalized, attach order, ERA shows processed, etrans note, audit entry), option off, every line denied, listed claim-level code, unlisted claim-level adjustment that would need a By Total row (nothing split: one attach row, both claimprocs on the original claim, status S, no note or audit entry, the reason in the error), the `EraAutoPostWriteOff` rule, qualified entry of another group, $0.00 code, other checks failing, the fork preference row, and manual versus automatic payment entry for a mismatched claim.
- `EraAutoSplitPlanTests` also covers the By Total arithmetic: a balanced remaining claim, an unlisted claim-level adjustment, a claim-level deductible, and a write-off difference (counts only when write-offs are posted and positive; sub-cent differences round away).

Not tested: the windows themselves (Preferences, Display Fields, the ERA window and its grid, the claim select warnings, the report column) were not opened, because the GUI is not launched in this run; their wiring is checked by `WiringTests` and their logic by the business-layer tests.

Verification (2026-10-25, after the two interrupted-run commits were finished): `OpenDental.Local.slnf` Debug x86 builds with 0 errors and 376 warnings (the baseline). Targeted tests pass: `BERATests` 66/66, `RegistryTests` 33/33, `NoHqTests` 49/49; `tools/Test-NoHqHosts.ps1` passes. A verifier should run `BERATests` and `RegistryTests`.

Fix round r1 (branch `port/b-era-r1`, 2026-10-25): the auto split committed its changes before the By Total refusal of `TryImportEraClaimData` could run, so an ERA claim with an unlisted claim-level adjustment (OA-23 $5) got split and then left unpaid. `SplitDeniedProcsIfNeeded` now refuses the split before the point of no return when the rest of the claim would need a By Total row. The new database test fails without the fix (a split note is written) and passes with it. Build: 0 errors, 376 warnings. Targeted tests: `BERATests` 72/72, `RegistryTests` 33/33, `NoHqTests` 49/49; `tools/Test-NoHqHosts.ps1` passes. `ForkMigrationTests` is not needed, because this package adds no migration step and changes no TableTypes or Crud.

## Combined review (2026-11-01, branch `port/review-2`)

- **Finding interplay-insurance-2, fixed.** After an auto split, `Etranss.TryAutoProcessEraEob` looked up the next insurance's claims from the claimprocs it had received and set them to Waiting to Send without asking anyone. A secondary claim that held both the paid procedure and the denied one (now on the new unsent primary claim) was queued with a line the primary insurance had not adjudicated, the "bogus" secondary claim `X837_5010` describes (sent with date paid 0001-01-01). Without the auto split (24.3) such an ERA claim is held back, so its secondary claim stays on hold. M2-CLAIMRCV's 26.1 lookup extends the same problem to later ordinals.
- Fix: `SplitDeniedProcsIfNeeded` records the moved procedures, each with its new claim, in `EraAutomationResult.DictSplitClaimNumsByProcNum`. The new `EraAutoSplit.KeepNextInsuranceClaimsOnHold`, one hook line in `TryAutoProcessEraEob` right after the next-insurance lookup, takes every claim that holds one of those procedures out of the list, so its status is not changed. For each such claim a line ("Auto split: patient, claim #N of the next insurance left on hold: procedures moved to claim #M.") goes into the automatic processing message and on top of the etrans note, like the split line. The claim's estimates were already recalculated when the rest of the claim was received (`ReceiveEraPayment`), and the usual next-insurance lookup releases it when the split claim is received. A next-insurance claim with none of the split procedures still moves to Waiting to Send.
- **INFERRED**: the whole claim of the next insurance waits for the split claim; it is not split itself, so the paid procedure's secondary part is not sent on its own.
- Tests (`EraAutoProcessDbTests`): `AutoSplit_SecondaryClaimWithTheDeniedProcStaysOnHold` (failed before the fix: the claim was W; now H, with the note, and offered by the lookup once the split claim is received) and `AutoSplit_SecondaryClaimOfThePaidProcOnly_IsWaitingToSend`; `WiringTests.AutoProcessing_SplitsBeforeTheChecks` checks the hook sits between the lookup and the Waiting to Send loop.
- Question for the owner: 6. When an auto split leaves a secondary claim on hold, should the secondary claim be split as well, so the paid procedure's secondary part goes out at once (today the whole secondary claim waits for the split claim)?

## Combined review (2026-11-01, branch `port/review-3`)

- **Finding schema-adoption-2, fixed in the runbook (no behaviour change).** "Auto split ERAs with CARC denial codes listed above" is read from the fork-owned row `ForkEraAutoSplitCarcDenials` (owner question 1). After adoption an office that had changed it in Open Dental 25.3.48 finds the default, and nothing in the runbook, `migration-preview` or `adopt` told the office. `docs/adoption-runbook.md` step 0.11 now has the second person record the value in Open Dental before the day (row 6 of its table), step 7.1 sets it again in this program, and both are checklist rows.
- Test: `RegistryTests.PrefNameTests.UnconfirmedNames_EachBuiltSettingIsRecordedAndSetAgainByTheAdoptionRunbook` (each unconfirmed name is either built, with the row it is read from, or listed as not built; step 0.11 has one table row for each row read; steps 0.11 and 7.1 are on the checklist). It fails on the runbook before this fix.
- The naming rule itself (register Open Dental's real names once they are known, or keep these rows) is left to the owner; it is a question of the combined review.

## Open Dental's name (SWMAP, 2026-11-07, branch `next/swmap-1`)

- The setting names of the office's Open Dental 25.3.48 database (owner question Switch-over 1) give Open Dental's name of "Auto split ERAs
  with CARC denial codes listed above": `EraAutoProcessSplitByCarcCodes` (1/0). `EraAutoSplit.PREF_NAME` is now that name, read and written
  by name outside `PrefName`, so the office's value carries over at the switch-over as it is; a missing row still reads as off.
- The row release 0.0 wrote (`ForkEraAutoSplitCarcDenials`) moves to this name in the fork migration step 2026110731 (`OdSettingNamesStep`);
  runbook step 0.11 no longer lists the setting.
- Tests: `BERATests.EraAutoProcessDbTests.AutoSplitPreference_OpenDentalsRowWrittenOnFirstChange`, `SWMAPTests`. See [SWMAP.md](SWMAP.md).
