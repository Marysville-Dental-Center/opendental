# Port note: P262-CLAIMS, Open Dental 26.2 claims and insurance (items 10, 20, 21, 22)

Branch `p262/claims-1`, cut from `p262/schema-1` (39be2b04b). One of the nine feature packages of the Open Dental 26.2 port (owner decision of
2026-11-07). It builds on the schema foundation (`docs/port-notes/P262-SCHEMA.md`): `provider.ClaimFormNum`, `EnumAutomationTrigger` 11 and
`AutoCondField` 15 were already there and are not changed.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html), read 2026-11-07. The items, in the page's order:
  - 10 (Account Module): "Moved Status History and Misc settings in the Edit Claim window".
  - 20 (Insurance): "Option to set the default claim form at a provider level".
  - 21 (Insurance): "When combining insurance plans, there is a new detailed warning for mismatched fields".
  - 22 (Insurance): "Automation trigger added when changing a procedure to not bill to insurance".
  None of the four items links to a manual page.
- `OpenDentalDiffDocumentation26-2.xml` and `OpenDentalDocumentation26-2.xml` (version 26.2.0.0):
  - `automation.Autotrigger`: `DoNotBillInsuranceCheck` "11-When clicking in the proc edit window."
  - `automationcondition.CompareField`: `CarriersMatch` "15- Only used for the DoNotBillInsuranceClick automation trigger. Condition based on
    if a patient's insurance carriers match those in the CompareString. CompareString will hold a CarrierNum. Comparison=None."
  - The `automationcondition` table summary: "Multiple conditions for a single automation are usually ANDed together. But for
    DoNotBillInsuranceCheck, the carriers are ORed."
  - `provider.ClaimFormNum`: "FK to claimForm.ClaimFormNum. Sets the default claim form created for the provider. This takes priority over
    pref.DefaultClaimForm and insplan.ClaimFormNum."
  - The full `insplan` column list (the fields item 21 compares).
- The public online manual (https://www.opendental.com/manual/) is the 24.4 manual: its Edit Claim pages (`claimedit.html`,
  `claimtabmisc.html`, `claimtabstatus.html`) show the layout before 26.2 (tabs General, Attachments, Misc, Medical, Status History) and its
  Automation page (`automation.html`) predates the new trigger. The versioned manuals after 24.4 (`help254/`, `help262/`) are closed to the
  public (an access notice for customers), so no 26.2 manual page was read. Nothing else public describes these items.

## What was built

### Item 22: the Do Not Bill to Ins automation trigger and the Carriers Match condition

- `AutomationDoNotBillIns` (new, `OpenPmsDentalBusiness/Data Interface/AutomationDoNotBillIns.cs`):
  - `DoesFire(isNoBillInsOld,isNoBillInsNew)`: true when the box is now checked and was not checked before (unchecked, or mixed).
  - `IsTriggerAllowed`, `GetSaveError`: Carriers Match works only with `DoNotBillInsuranceCheck`, after the 26.1 clinic mismatch condition's
    pattern (`AutomationClinicMismatches`).
  - `GetCarrierNum` (the CarrierNum in CompareString), `IsAnyCarrierMatch` (ORed), `IsConditionMet` (false for any other trigger),
    `GetCarrierNumsForPat` (the carriers of the patient's patplans), `GetDisplayText` (the carrier's name for the windows).
- `Automations.CheckAutomationConditions`: all Carriers Match conditions of one automation are evaluated together, ORed with each other, and
  the result is ANDed with the automation's other conditions (the documents' "the carriers are ORed").
- `FormProcEdit.checkNoBillIns_Click`: after the claimprocs, the procedure flag (25.4) and the estimates are saved, the trigger fires through
  `AutomationL.Trigger` with the procedure as trigger object, when `DoesFire` says so.
- `FormAutomationConditionEdit`: Carriers Match is listed only when the automation's trigger is `DoNotBillInsuranceCheck` (or the condition
  already uses it); the carrier is picked with `...` from the carrier list (`FormCarriers` in select mode) and shown by name in a read-only
  box; it saves the CarrierNum with Comparison None; a short text says what the condition does.
- `FormAutomationEdit`: the conditions grid shows the carrier's name for Carriers Match, and saving an automation that has Carriers Match with
  another trigger is refused with a message (as for the 26.1 clinic mismatch condition).
- The trigger offers every action except the appointment and Rx ones (the existing rules of the window); it takes no procedure codes.
- Carrier Combine (`Carriers.Combine`, Lists > Carriers > Combine): `AutomationDoNotBillIns.CombineCarriers` changes every Carriers
  Match condition that holds a combined carrier to hold the kept carrier, next to the 26.1 preauthorization merge and before the combined
  carriers are deleted. Without it the plans would carry the kept carrier while the condition still named a deleted one, so the automation
  would silently stop firing and the windows would show a bare number. Two conditions of one automation that end up on the same carrier are
  harmless (ORed). Conditions of other fields are not touched. `FormCarriers` signals `InvalidType.Automation` with the carriers; the
  trigger and the automation windows read the conditions from the database, so nothing cached goes stale.

### Item 20: the default claim form at the provider level

- `ClaimFormDefaults` (new, `OpenPmsDentalBusiness/Data Interface/ClaimFormDefaults.cs`): `GetClaimFormNumForNewClaim(provNumTreat,
  claimFormNumPlan)` returns the treating provider's `ClaimFormNum` when it is set and the claim form exists, otherwise the plan's;
  `Pick` is the rule; `GetProviderAbbrsUsing(claimFormNum)` lists the providers whose default it is.
- Every place that creates a claim or a preauthorization sets `claim.ClaimForm` through it once the treating provider is known:
  `AccountModules.CreateClaim` (Create Claim in the Account Module, batch and secondary claims), `Claims.CreateClaimForRepeatCharge`,
  `Claims.CreateClaimForOrthoProc`, `PreAuthL` and the treatment plan preauthorization (`ControlTreat`). Printing already uses
  `claim.ClaimForm` first, so the provider's form is printed.
- Edit Provider (`FormProvEdit`): "Default Claim Form" on the General tab (right column, below the eRx box): "None (use the plan's claim
  form)" or a claim form; hidden claim forms are listed only when they are this provider's.
- `ClaimForms.Delete` refuses a claim form that is a provider's default, like one an insurance plan uses; the Claim Forms window then names
  the providers.

### Item 21: the detailed warning of Combine Insurance Plans

- `InsPlanMergeMismatches` (new, `OpenPmsDentalBusiness/Data Interface/InsPlanMergeMismatches.cs`): `GetMismatches(kept,others)` gives one
  row per field of each other plan that differs from the kept plan (`InsPlanMismatch`: field, name, plan, value kept, value lost), with the
  values shown by name (carrier, employer, fee schedules, claim form, filing code and subtype, billing type, ortho procedure, plan type,
  renew month, Yes/No); `GetBenefitMismatches` adds one "Benefits" row for each plan whose plan-level benefits are not the same set as the
  kept plan's (any order); `GetFields` is the list of compared fields: every `insplan` column of the 26.2 document except the key and the
  three security columns.
- `FormInsPlansMergeWarning` (new window, "Combine Insurance Plans - Mismatched Fields"): the selected plan, then a grid (Plan Combined,
  Field, Selected Plan (kept), This Plan (lost)), and Combine / Cancel. `FormInsPlansMerge.butOK_Click` shows it when anything differs;
  Combine goes on with the merge as before, Cancel goes back to the plan list. With no mismatch, the earlier short warning shows as before.
  The window's text keeps that warning's two points (no unmerge, not from two workstations at once).

### Item 10: Status History and Misc in Edit Claim

- `FormClaimEdit.designer.cs`: the tab order is now General, Status History, Misc, Attachments, Medical, Canadian (was General, Attachments,
  Misc, Medical, Canadian, Status History). The custom tab control orders tabs as they are added, so only the order of the
  `tabControlMain.Controls.Add` lines changed; nothing on the tabs changed, and no code selects a tab by index.

### No database step

Nothing new is stored: the provider column and both enum values come from the schema foundation (level 2026110701), the condition stores a
CarrierNum in the existing `CompareString`. The levels 2026110731 to 2026110739 kept for this package are unused. No fork-only enum value.

## INFERRED behaviours

The sources give the four one-line release notes and the document texts quoted above; everything else is this program's own design:

1. **Item 10, the whole layout.** The release note says only that Status History and the Misc settings moved within the window; where they
   went is in the 26.2 manual, which is not public. Moving the two tabs to right after General is a guess at the intent (bring claim tracking
   and the resend fields forward), not a copy of Open Dental's window. It is the part of this package least likely to match Open Dental.
2. **Item 20, which provider.** The document says "the default claim form created for the provider"; this program uses the claim's
   treating provider (the provider of the procedures, as `claim.ProvTreat` is set at creation), the provider whose other claim settings
   (the e-claim billing provider override) are read the same way. The billing provider is the other candidate.
3. **Item 20, when.** At creation only, for claims and preauthorizations: the provider's form is written into `claim.ClaimForm`, so it shows in
   Edit Claim and can be changed there; changing the claim's provider later does not change its form; existing claims are not changed. A
   provider whose form was deleted falls back to the plan's (deleting it is refused now anyway). "Takes priority over pref.DefaultClaimForm and
   insplan.ClaimFormNum" is read as: over the plan's form, which itself starts as the practice default.
4. **Item 20, deleting a claim form** a provider uses is refused (Open Dental's documents say nothing about it).
5. **Item 21, the fields.** "Mismatched fields" is read as every user-visible `insplan` column (the key and the security columns are left
   out). The names follow the Edit Insurance Plan window. Plan notes and texts are compared exactly.
6. **Item 21, benefits.** Not a field, but combining deletes the other plans' benefits, so one Benefits row says when a plan's plan-level
   benefits differ from the kept plan's. Patient-level benefit overrides are not compared (combining keeps them).
7. **Item 21, the window.** A grid window with Combine and Cancel, shown only when something differs; the earlier short warning when nothing
   does. Open Dental's window and wording are not published.
8. **Item 22, when it fires.** "When clicking in the proc edit window" and "changing a procedure to not bill to insurance" are read as: the
   click on Do Not Bill to Ins in the procedure edit window that changes the box to checked (from unchecked or mixed), after the change is
   saved. Unchecking does not fire it, and neither do the other ways an estimate becomes not billed (the procedure code and insurance plan
   defaults when estimates are made, the box of the Edit Claim Procedure window).
9. **Item 22, the patient's carriers** are the carriers of the patient's current plans (every patplan, any ordinal), not only the plans of the
   procedure's estimates; a plan the patient only subscribes to without a patplan does not count.
10. **Item 22, the windows.** Carriers Match is offered only for its trigger, and saving it with another trigger is refused, following the
    documents' "Only used for ..." and the 26.1 clinic mismatch condition; the carrier is picked from the list and shown by name; the trigger
    takes no procedure codes.
11. **Item 22, ORed.** Several Carriers Match conditions are ORed with each other and the rest of the automation's conditions are ANDed with
    that result, as the table summary says; an automation with Carriers Match under another trigger (written elsewhere) never fires.
12. **Item 22, Carrier Combine.** The documents do not say what combining carriers does to a Carriers Match condition; it follows the kept
    carrier, as the plans, the electronic transactions and (26.1) the preauthorization lists do.
13. The display texts (labels, messages, the warning window) are this program's own wording.

## Not built

- The 26.2 manual's Edit Claim layout (not public; INFERRED 1).
- Firing the trigger from anywhere but the procedure edit window (INFERRED 8).
- No translation rows for the new texts (the translation tables are not shipped by this program's packages).
- No UI was run (OpenPmsDental.exe is not launched in this run); the windows are checked in their source by `WindowSourceTests`.

## Files touched

- New: `OpenPmsDentalBusiness/Data Interface/{AutomationDoNotBillIns,ClaimFormDefaults,InsPlanMergeMismatches}.cs`,
  `OpenPmsDental/Forms/FormInsPlansMergeWarning.{cs,designer.cs,resx}`, `tests/P262ClaimsTests/*`, this note.
- Changed: `Automations.cs` (Carriers Match), `Carriers.cs` and `FormCarriers.cs` (Carrier Combine), `ClaimForms.cs` (delete check), `Claims.cs` and `AccountModules.cs` (claim form of new claims),
  `TableTypes/Automation.cs` and `TableTypes/AutomationCondition.cs` (comments only), `FormProcEdit.cs`, `FormAutomationEdit.cs`,
  `FormAutomationConditionEdit.cs`, `FormProvEdit.cs` and its designer, `FormClaimEdit.designer.cs` (tab order), `FormInsPlansMerge.cs`,
  `FormClaimForms.cs`, `PreAuthL.cs`, `ControlTreat.cs`, both project files, `OpenDental.sln`, `OpenDental.Local.slnf`,
  `docs/release-notes/next.md`.

## Checks (2026-11-07)

- Full build of `OpenDental.Local.slnf` (Debug, x86): 0 errors.
- Targeted runs, all with `NoHqTests` (58/58): `P262ClaimsTests` 39/39 (new: rules, the trigger through `Automations.Trigger` on the
  scratch database, claim forms of new claims end to end through the repeat charge claim, the merge mismatches with real names, and the window
  wiring), `BCLAIMTests` 55/55, `InsuranceTests` 49/49, `M2CLAIMFORMTests` 16/16, and the assemblies that run the changed code or read the
  changed sources: `M2MISCTests` 78/78 (automation windows), `M2ESTTests` 70/70 (the Do Not Bill to Ins box), `M2NOTESTests` 51/51,
  `M2CARRIERTests` 55/55, `BTPTests` 36/36, `M2TERMTests` 21/21, `M2SDVTests` 47/47, `VEDSTests` 83/83, `VendorDirectTests` 873/873.
- `BSHELLTests` 67 of 68: its source checks of `ControlTreat.cs` pass; `DxcCreditScoreRemovedTests.Step_HasItsDatedLevel_AndPassesValidation`
  fails on the schema foundation already, not because of this package (it adds no step): it requires every fork step outside the
  `FeatureSteps` namespace to come before the DXC step (2026110101), and P262-SCHEMA's `OdParity26_2Schema` (2026110701, namespace
  `ForkMigrations`) comes after it. Left for the schema package or the integration.
- Scratch database `test_p262claims_*`, dropped after the run. `ForkMigrationTests` was not run (no fork step here; its
  `InitSqlRegenerationTests` fails on every 26.2 branch until the integration regenerates `db/init/new-database.sql`).

## Questions for the owner

1. Item 10: the Edit Claim layout is a guess (Status History and Misc moved to right after General). Can a 26.2 manual page or screenshot of
   the Edit Claim window be supplied, so the layout can be matched, or is this order acceptable?
2. Item 20: the treating provider's default claim form is used. Should it be the billing provider's instead (or the billing provider's first,
   then the treating provider's)?
3. Item 20: should changing the treating provider on an unsent claim also change its claim form to that provider's default?
4. Item 21: the warning also lists a difference in benefits, which the release note does not mention. Keep it?
5. Item 22: should the trigger also fire when an estimate is marked not billed in the Edit Claim Procedure window, or only from the
   procedure edit window as the document says?
6. Item 22: should the trigger take procedure codes (fire only for some procedures), like Procedure Complete?
