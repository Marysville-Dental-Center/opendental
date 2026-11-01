# Port note: M2-INSPLAN, Edit Insurance Plan layout overhaul

Branch `port/m2-insplan`, cut from `port/foundation` (M2-01 and M2-02 merged); its one commit is the work an earlier run saved when a usage limit stopped it. Finished on `port/m2-insplan-t1`, which merges the integration head `port/int-11` (c2044cf9e) and completes the package (see "Integration on port/int-11"). Backlog section: [port-backlog.md, M2-INSPLAN](../specs/port-backlog.md). No fork migration step, no schema, no preference. Tests: `tests/M2INSPLANTests` (assembly `M2INSPLANTests`).

Sources: the feature specs in `docs/specs/features-26.1.md` and `features-25.4.md`, which cite Open Dental's public 25.4 and 26.1 release notes, the public 26.1 highlights post (fields moved so everything is visible without scrolling; functionality unchanged), the 24.3 manual page on Edit Insurance Plan (the field inventory) and the published 25.4 database documents (the `ExclusionFeeRule` and the two write-off override descriptions). The 26.1 screenshots are not public, so the layout is ours. Nothing was decompiled, installed or observed, and no office data was used. Behaviour that is not public is marked **INFERRED**.

## Status per feature

| Feature ID | Status | Summary |
|---|---|---|
| `v26_1-family-insplan-layout` | built | The four tab pages (Plan Info, Other Ins Info, Canadian, Ortho) and their scrolling panels are gone. Every field is on screen at once in fixed panels; the window is 1340x686 (client, 96 dpi) and fits 1366x768 at 100%. Every 24.3 control is still there with its type, name, event wiring and enable/visibility rules. |
| `v25_4-family-writeoff-override-wording` | built | Exclusions / Annual Max / Age or Frequency Limit labels, "Use Standard Provider Fee" (ExclusionRule's description, from M2-TERM), help text on the labels and combo boxes, "Claims show standard fee, not billed fee". Values and logic unchanged. |
| `v26_1-tools-audit-insplan-drop` | built | "The insurance plan for the carrier <name> (PlanNum <n>) was dropped." The other audit entries of paths that drop a patient from a plan through `PatPlans.Delete` name the PlanNum too (X12 834 import, Edit Insurance Plan's Delete). |

## v26_1-family-insplan-layout

**Layout** (**INFERRED**; `OpenDental/Forms/FormInsPlan.designer.cs`). Coordinates at 96 dpi; `LayoutManager` scales them as before.

1. **Patient plan strip** across the top (`panelPat`, 982x90, the MiscColors background as before): relationship, optional patient ID (Dependant Code in Canada), patient plan ID, order, pending, eligibility last verified and Now, Hist (Insurance History), Ortho (when ortho is on), patient-level Audit Trail, the adjustments list and Add, Drop and its explanation. To its right: Request Electronic Benefits (Last Request, Request, History), Import Benefits (Trojan and IAP, see below) and Benefits Last Verified, Now, Don't Verify.
2. **Plan column** (`panelPlan`, left): "Insurance Plan Information" with the plan-level Audit Trail and Pick From List, plan ID, employer, the carrier group (carrier, phone, address, city/state/zip, electronic ID and Search IDs, send electronically), group name and number (Div. No. in Canada), other subscribers, plan type, fee schedule, Carrier Allowed Amounts (out of network, manual Blue Book, Use Blue Book) and Other Fee Schedules (patient co-pay).
3. **Other Ins Info column** (`panelOrthInfo`, middle): the former Other Ins Info tab (alternate code, don't substitute codes and Subst Codes, PPO substitution write-offs, claims show standard fee, hidden, base units, claim form, COB rule, filing code and subtype, billing type), Write-Offs for Non-Covered Services Overrides, Per Visit Amounts, and the former Ortho tab in a new "Ortho" group (`groupOrtho` around the unchanged `panelOrtho`, so `SetEnabledOrtho` still walks the same children).
4. **Subscriber and benefits column** (right): Subscriber Information (name and Change, subscriber ID, effective dates, release of information, assignment of benefits, note), the Benefit Information grid with a new "Edit Benefits" button (same as double-clicking the grid; there is no tab to hint that the grid is editable any more), the plan note, and the former Canadian tab in `groupCanadian` (plan flag, provincial medical plan, diagnostic and institution codes).
5. **Bottom bar**: Delete, Label, Create new Plan if needed / Change Plan for all subscribers, Save. (24.3 has Save and no OK/Cancel pair; the title-bar close is Cancel, as before.)

**Code** (`OpenDental/Forms/FormInsPlan.Layout26_1.cs`, a new partial file, so `FormInsPlan.cs` only calls it):

- The employer and carrier drop-down lists open under their text boxes: `GetDropDownLocation26_1` sums the containers' offsets instead of the removed tab control's (two lines in the constructor).
- Outside Canada the Canadian group is hidden (it was a tab page that was removed), and at the end of Load the plan note moves down into its space and the benefits grid grows by the same height (`HideCanadian26_1`, `ApplyLayout26_1`, through `LayoutManager.MoveLocation`/`MoveHeight`). With ortho off, `FillOrtho` hides `groupOrtho` where it removed the Ortho tab.
- `ApplyLayout26_1` also sets the 25.4 help text (below).
- `HideEmptyImportBenefits26_1`: see "Deviations".

**Rules unchanged.** No event handler body changed. `FormInsPlan.cs` differs from the base only in the lines listed under "Files touched". Saving, validation, the enable and visibility rules (plan type and the fee schedule fields, CarrierCreate forcing Pick From List, InsPlanEdit and CarrierEdit, InsuranceVerification, Blue Book, EasyHideMedicaid, Trojan, IAP, ortho, Canada, a plan opened from Lists > Insurance Plans) and the permissions are as in 24.3.

**Smallest supported workstation** (our choice, as the spec asks): 1366x768 at 100% scaling. The window is 1356x720 with FormODBase's borders and title bar, which fits above a 48-pixel taskbar. Larger scaling needs a larger screen (125% fits 1920x1080). The 24.3 window (984x696) needed scrolling inside its tabs instead.

**Tab order**: patient strip, subscriber, plan column, Other Ins Info column, benefits grid, Edit Benefits, plan note, Canadian, then the top-right groups and the bottom bar; inside each container top to bottom. Unique in every container (tested).

### Regression checklist (24.3 event handlers and rules)

Written from the 24.3.63 designer and `FormInsPlan.cs` before the rewrite (`tests/M2INSPLANTests/Data/FormInsPlan24_3.events.txt`, `FormInsPlan24_3.controls.txt`) and checked after it by `LayoutDesignerTests` and `FormInsPlanWindowTests`:

- [x] All 44 control event wirings of the 24.3 designer, plus the form's Load and Closing, are still wired to the same handlers: subscriber (CheckAssign_Click, butChange_Click), import and eligibility (butImportTrojan_Click, butIapFind_Click, butBenefitNotes_Click, butGetElectronic_Click, butHistoryElect_Click, butVerifyBenefits_Click), patient plan (listAdj_DoubleClick, butAdjAdd_Click, butAuditPat_Click, butHistory_Click, butPatOrtho_Click, butVerifyPatPlan_Click, butDrop_Click), plan (butAudit_Click, butPick_Click, textEmployer KeyUp/Leave, textCarrier KeyUp/Leave, butPickCarrier_Click, the four address TextChanged, comboElectIDdescript_SelectedIndexChanged, textElectID_Validating, butSearch_Click, butOtherSubscribers_Click, comboPlanType, comboFeeSched, checkUseBlueBook), Other Ins Info (butSubstCodes_Click, comboExclusionFeeRule, comboBillType, comboFilingCode), ortho (comboOrthoClaimType, butPickOrthoProc_Click, butDefaultAutoOrthoProc_Click), benefits (gridBenefits_DoubleClick), bottom bar (butSave_Click, butLabel_Click, butDelete_Click). One new wiring: butBenefits_Click.
- [x] Every one of the 177 kept fields (176 controls and the tooltip component) is declared with its 24.3 type and reachable on the window with no tab page or scrolling panel above it; only the six tab-layout containers are gone.
- [x] US office: only the Canadian group, the ortho group (ortho off) and the empty Import Benefits group are hidden; the plan's data is in the moved controls.
- [x] Canadian office: the Canadian fields show in their group, Dependant Code and Div. No. show, the plan note does not overlap them.
- [x] Ortho on: the ortho group and the patient Ortho button show; `SetEnabledOrtho` still disables the periodic fields for Initial Claim Only.
- [x] From Lists > Insurance Plans (no patient plan, no subscriber): the patient strip, subscriber group, Pick From List and Delete are hidden as in 24.3.
- [x] Exclusions still lists the enum in order (the handler casts the index); the stored value round-trips.
- [x] The employer and carrier drop-downs open right under their text boxes.
- [x] Drop still drops only the chosen plan and writes one InsPlanEdit entry.
- [x] No text is clipped at 96 dpi (labels with GDI, as `ProgramEntry` sets; check boxes, buttons, group titles and combo boxes with GDI+), no control leaves its container, no two siblings overlap (except `butOtherSubscribers`/`comboLinked`, which the code shows one at a time, as in 24.3).

## v25_4-family-writeoff-override-wording

- Labels: "Exclusions", "Annual Max", "Age or Frequency Limit" (was "Age or Frequency Limits"); the group keeps its title.
- Exclusions options: Practice Default / Do Nothing / Use Standard Provider Fee. They come from `ExclusionRule`'s descriptions through `ComboBox.Items.AddEnums`, as before; M2-TERM already renamed `UseUcrFee`'s description (member name and value 2 kept). The earlier run's own option-text function duplicated that and was removed at the merge.
- Help text (`OpenDental/Logic/InsPlanWriteOffL.cs`, the spec's wording), as tooltips on each label and its combo box: Exclusions (Practice Default follows the Family Insurance preference; Do Nothing applies the plan's normal fee and write-off; Use Standard Provider Fee bills the full standard fee with no write-off), Annual Max and Age or Frequency Limit (Yes bills the full standard provider fee with no write-off once the limit is reached; No keeps the normal fee and write-off; Default follows the matching Family Insurance preference). **INFERRED**: the first line of each, naming the preference.
- "Claims show UCR fee, not billed fee" became "Claims show standard fee, not billed fee", with a tooltip (M2-TERM left this string to this package). No text in the window says "UCR" any more; control and column names (`checkClaimsUseUCR`, `insplan.ClaimsUseUCR`) stay.

## v26_1-tools-audit-insplan-drop

`OpenDentBusiness/Data Interface/PatPlans.Drop.cs` (a new partial file of `PatPlans`; `PatPlans.cs` only became `partial`):

- `GetPlanNumLogText(planNum)`: "(PlanNum 123)".
- `GetDropLogText(carrierName,planNum)`: "The insurance plan for the carrier Example Dental (PlanNum 123) was dropped." Used by Edit Insurance Plan's Drop (`DropClickHelper`). The entry's FKey was already the PlanNum.
- X12 834 import (`EtransL.DropPlan`, InsPlanDropPat): "Insurance plan dropped from patient for carrier 'Example Dental' (PlanNum 123) and groupnum ...". It also printed the carrier object (`OpenDentBusiness.Carrier`) instead of its name; now the name. This is a bug fix.
- Edit Insurance Plan's Delete also drops patients through `PatPlans.Delete` (`InsSubs.Delete`, and `InsPlans.Delete` through it), so its two texts name the PlanNum too (**INFERRED**, the spec's rule 2): `GetDeleteSubscriberLogText` "The subscriber Jane Doe with the Subscriber ID 123 (PlanNum 45) was deleted." and `GetDeletePlanLogText` "The insurance plan for the carrier Example Dental (PlanNum 45) was deleted."
- Translation keys are unchanged ("FormInsPlan" and the same English pieces).

Other `PatPlans.Delete`/`DeleteNonContiguous` callers write no audit entry for the drop, and still write none (**INFERRED**: the item puts the PlanNum into existing entries; it adds none): the Trojan bridge's dropped plans (`WpfControlsOD/Bridges/Trojan.cs`, which marks the plan note), clone synchronisation (`Patients.SynchClonePatPlans`, which lists the change in the message it shows), Sheet Import replacing a plan (`FormSheetImport`), `FormInsPlan`'s closing when a new patient plan is cancelled (it undoes its own insert), and the unit tests.

## INFERRED behaviours

- The whole layout: panel order, which fields share a column, the Edit Benefits button, the plan note's place, Ortho as a group in the middle column, Canadian at the bottom of the right column.
- The smallest supported workstation, 1366x768 at 100%.
- The first line of each write-off help text (naming the preference).
- The PlanNum in Edit Insurance Plan's two Delete audit texts.
- Paths that write no audit entry for a drop stay without one.
- Hiding the empty Import Benefits group (below).

## Deviations from the spec

- **Import Benefits** holds only the Trojan and IAP controls (both vendor integrations, off by default). The Load rules already hide them when those programs are off (and the IAP button when there is no subscriber), which left an empty framed box in 24.3; with the fixed layout it stood empty in the top-right corner. It is now hidden when neither has anything to show (`HideEmptyImportBenefits26_1`, ground rule 7: hidden until enabled). It decides from the program rows, with the same conditions as the Load rules, because a control's `Visible` reads false until the window is shown. With Trojan on, the group shows (the Trojan ID is shown read-only even without a subscriber); with IAP on, it shows when a subscriber is open.
- The spec's panel 1 names a "patient-plan note"; Edit Insurance Plan has none (the subscriber note is in the subscriber panel). The spec's panel 5 names OK and Cancel; 24.3 has Save only, which stays.
- The Drop button stays in the patient strip next to its explanation, where 24.3 has it, rather than in the bottom bar: it acts on the patient plan, and the strip is hidden with it when there is none.

## Files touched

- New: `OpenDental/Forms/FormInsPlan.Layout26_1.cs`, `OpenDental/Logic/InsPlanWriteOffL.cs`, `OpenDentBusiness/Data Interface/PatPlans.Drop.cs`, `tests/M2INSPLANTests/*`, this note.
- Owned by this package: `OpenDental/Forms/FormInsPlan.designer.cs` (the layout rewrite), `FormInsPlan.cs` (the two drop-down locations, `HideCanadian26_1` for the removed tab, `groupOrtho.Visible` for the removed tab, the calls to `HideEmptyImportBenefits26_1` and `ApplyLayout26_1`, the three audit texts), `OpenDentBusiness/Data Interface/PatPlans.cs` (`partial`).
- Outside the owned list: `OpenDental/Logic/EtransL.cs` (one log line in `DropPlan`), `OpenDental/OpenDental.csproj` and `OpenDentBusiness/OpenDentBusiness.csproj` (compile items), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Not edited: `OpenDentBusiness/TableTypes/InsPlan.cs` (M2-TERM already changed `ExclusionRule.UseUcrFee`'s description), `PrefInfos.xml` (no entry for this window).

## Interplay with other packages

- **M2-TERM** (merged): the Exclusions text comes from its `ExclusionRule` description; this package changed the remaining "UCR" string it left here (`checkClaimsUseUCR`). Its `TerminologyStringTests` read no FormInsPlan text.
- **M2-HIST** (merged): the Hist button still opens `FormInsHistSetup(patNum,insSub)`; it moved into the patient strip with its handler unchanged.
- **M2-INSV** (merged, "takes files over from"): it changed neither `FormInsPlan` nor `PatPlans.cs`.
- **V-BRIDGE1** (merged): `butIapFind_Click` (opens the IAP sign-in page) and its tooltip are kept as merged; its `WiringTests` still find them.
- **M2-CARRIER** (merged): carriers created here through `Carriers.GetIdentical` start with an empty preauthorization list, as its note says; nothing to change.
- **M2-CLAIMRCV** (merged): relies on `PatPlans.Delete` moving later plans up one ordinal; `PatPlans.Delete` is unchanged.
- **VendorDirectTests** (`DxcPartnerIdentityTests`) reads `FormInsPlan.cs` for partner IDs; none were added.
- Any later package that edits `FormInsPlan.designer.cs` should keep the panel containers and rerun `M2INSPLANTests` (inventory, overlap, fit, tab order, text widths).

## Tests

`tests/M2INSPLANTests` (MSTest v1, x86, .NET 4.8), 31 tests:

- Without a database: `LayoutDesignerTests` (14: reads the designer file; the 24.3 inventory, every control with its type and no tab page or scrolling panel above it, every 24.3 event wiring, the window fits 1366x768 at 100%, every control inside its container, no overlaps, the panels where the spec puts them, unique tab order and its sequence, every text fits at 96 dpi including run-time texts such as "Dependant Code" and "Use Standard Provider Fee", the 25.4 labels, no "UCR"), `WordingAndAuditTextTests` (6: enum values kept, the 25.4 option texts, the help text, the drop and delete audit texts).
- Database, a fresh 24.3.63 database upgraded by the fork migration runner (`InsPlanDb`, per-run name `forkmig_<run>_m2insplan`, dropped afterwards), the real window built and loaded the way ShowDialog loads it (never shown, except one paint far off screen): `FormInsPlanWindowTests` (11: US office, Canadian office, ortho on, opened from the plans list, Import Benefits with Trojan or IAP on and off, the note and grid move, the write-off texts and tooltips, the drop-down positions, the snapshots, Drop's audit entry with two same-name carriers, the 834 drop's entry).
- `Window_PaintsCompletely_UsAndCanadaWithOrtho` saves `Snapshots/FormInsPlan-US.png` and `FormInsPlan-Canada-Ortho.png` in the test output folder for a person to look at.

## Not tested

- OpenDental.exe is not launched in package work, so the window was not used by hand: no click-through of the handlers (Pick From List, Search IDs, Benefits, eligibility requests). Their wiring and bodies are unchanged and their rules are covered as listed above.
- Other DPI settings and font zoom: the layout uses `LayoutManager` as before and the tests run at 96 dpi.

## Questions for the owner

1. Smallest supported workstation: 1366x768 at 100% (window 1356x720). Confirm, or name another; 1920x1080 at 150% (1280x720 effective) does not fit this window, nor did the 24.3 one without scrolling.
2. The layout itself (columns and grouping) is ours; please check it in the program. The snapshots in the test output show it.
3. Edit Insurance Plan's Delete audit texts now name the PlanNum too. Confirm.
4. Should the drops that write no audit entry (Trojan, clone synchronisation, Sheet Import replacing a plan) start writing one? Left as they are.
5. Hiding the empty Import Benefits group when neither Trojan nor IAP is on. Confirm.

## Integration on `port/int-11`

Branch `port/m2-insplan-t1` from `port/m2-insplan`, with `port/int-11` (c2044cf9e) merged in.

- The merge had no conflicts. Since `port/foundation` the integration branch gained every merged package; of those, only V-BRIDGE1 (IAP button body and tooltip) and M2-TERM (`ExclusionRule` description) touch this window, and both are kept.
- Reused instead of duplicated: the Exclusions combo is back to `Items.AddEnums<ExclusionRule>()` (M2-TERM's description), and `InsPlanWriteOffL.GetExclusionRuleText` is gone.
- The merge driver had reordered the M201Tests and M202Tests entries in `OpenDental.sln`; the file is now `port/int-11`'s with only the M2INSPLANTests project and its configuration lines added.
- Finished here: the Delete audit texts, the empty Import Benefits group, labels drawn with GDI in the window tests as in the program (`Application.SetCompatibleTextRenderingDefault(false)` in the assembly setup, so the snapshots match the program), and this note.

### Verification

On `port/m2-insplan-t1` (2026-11-01):

- One full build of `OpenDental.Local.slnf` through the build lane (Debug, x86): 0 errors.
- `run-tests.ps1 -Mode Core` (`test-results/port_m2-insplan-t1.txt`): 70 assemblies, 3,704 tests, 3,704 passed, 0 failed. Among them M2INSPLANTests 31/31, M2TERMTests 21/21, M2HISTTests 43/43, M2INSVTests 17/17, M2CLAIMRCVTests 50/50, VBRIDGE1Tests 37/37, VendorDirectTests 873/873, NoHqTests 49/49. No migration, schema, CRUD or adoption code changed, so the slow assemblies were not added.
- `tools/Test-NoHqHosts.ps1`: passed.

## Integration (branch `port/int-14`)

- Merged onto `port/int-13` (bb150f537) with `git merge --no-ff`. Since `port/int-11` the integration branch gained M2-SDV, M2-RECUR, M2-EMP, M2-NOTES, V-GP and V-BRIDGE2. None of them touches `FormInsPlan`, `PatPlans`, `EtransL.DropPlan`, `ExclusionRule` or another `PatPlans.Delete` caller, so the merge had no conflicts and there is no interplay to connect and no duplicate helper.
- `OpenDental.sln` and `OpenDental.Local.slnf`: the merge driver kept `port/int-13`'s order (VGPTests, VBRIDGE2Tests last) and added only the M2INSPLANTests project and its twelve configuration lines; every project GUID is unique.
- Still no fork migration step, schema, preference, CRUD or adoption change, so the M1-Z adoption tools need nothing.
- On the merged head: one full build through the build lane (0 errors), `run-tests -Mode Full` (80 assemblies, 4,205 tests, none failed; the 2 not passed are M201Tests' published-document checks, inconclusive without `M201_OD_DOCS_DIR` as on earlier heads) and `tools/Test-NoHqHosts.ps1` (passed). M2INSPLANTests 31 of 31, M2TERMTests 21, M2HISTTests 43, M2CLAIMRCVTests 50, VBRIDGE1Tests 37, VBRIDGE2Tests 64, VendorDirectTests 873, AdoptionValidation.Tests 59, ForkMigrationTests 38, SyntheticData.Tests 54.
