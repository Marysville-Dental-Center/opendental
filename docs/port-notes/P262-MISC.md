# Port note: P262-MISC, Open Dental 26.2 items 37, 39 and 41

Branch `p262/misc-1`, cut from `p262/schema-1` (39be2b04b). Three of the Open Dental 26.2 changes the owner chose on 2026-11-07: vital signs
imported from eForms (item 37), the definition category for Referral Commlog Types (item 39) and the preference that switches a patient between
the Prophy and Perio recall based on completed procedures (item 41). Everything is this program's own code.

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html). They give one line per item and no link: "Vital signs can be
  imported from eForms" (eServices), "New definition category for Referral Commlog Types" and "New preference to automatically switch patients'
  prophy or perio recall based on completed procedures" (Miscellaneous).
- `OpenDentalDiffDocumentation26-2.xml` and `OpenDentalDocumentation26-2.xml` (26.2.0.0):
  - `DefCat` 57 `CommLogReferralTypes`: "Commlog Referral Types. Similar to Commlog Types, but are used for Referral Commlogs."
  - `commlog.CommType`: a definition of `CommLogTypes` when `ReferralNum` is 0, of `CommLogReferralTypes` when `ReferralNum` is set.
  - `vitalsign`: Height in inches, Weight in pounds, BpSystolic and BpDiastolic in mmHg, Pulse in beats per minute; "We will default to Body
    Height=8302-2" and "Body Weight=29463-7" (the LOINC exam codes); HeightExamCode and WeightExamCode "Can be blank if BP only".
  - `eformfield.AutoImport`: the list of database links Auto Import is implemented for. It has no vital sign link.
  - `recall`, `recalltype`, `recalltrigger`: a patient has one recall per type; DatePrevious comes from completed (C, EC, EO) trigger
    procedures.
- The public manual pages (https://www.opendental.com/manual/...) were read for the windows involved: Referral Setup (the Communication Item
  for Referral window: no Type field before 26.2), Recall Types (Prophy and Perio are mutually exclusive, their triggers cross over, Set Perio
  changes the recall type), Recall Setup, Vital Signs (entry window), Import Patient Forms. They are the 24.4 manual; the 26.2 help pages are not
  public. None of them describes these three items.
- The 26.2 schema was already complete on `p262/schema-1` for these items: `DefCat.CommLogReferralTypes` (57) is defined (P262-SCHEMA). Item 37
  and item 41 need no schema change in the documents (no new column, table or enum value). Nothing was missing.

## What was built

### Item 37: vital signs from eForms (`OpenPmsDentalBusiness/EForms/EFormVitalSigns.cs`, new)

- Five database links for eForm text fields: `vitalHeight`, `vitalWeight`, `vitalBpSystolic`, `vitalBpDiastolic`, `vitalPulse`
  (`EFormFieldsAvailable.GetList_TextBox`). Picking one gives the field a label naming the unit ("Height (inches)", `EFormL`).
- Answers are read by `EFormVitalSigns.TryParse`: a number (current culture, or a period as the decimal mark), optionally followed by its unit
  (in, inches, "; lb, lbs, pounds, #; mmHg; bpm); height also in feet and inches (5'10", 5' 10, 5 ft 10 in, 5ft). Blood pressure and pulse are
  whole numbers. Plausible ranges: height 10 to 108 in, weight 1 to 1500 lb, systolic 50 to 300, diastolic 20 to 200, pulse 20 to 300.
- Saving a form (`EForms.Validate`) rejects an answer that can't be read or is out of range ("Pulse (beats per minute): not a number."); empty
  is allowed; fields hidden by conditional logic are not checked.
- The eForm Import window (`FormSheetImport`, new partial `FormSheetImport.VitalSigns.cs`) has a "Vital Signs, taken <date>" section: one row
  per answered vital sign, Current Value = the patient's latest entry with its date, Import Value = the value read (checked) or why it can't be
  imported (red, unchecked; double click to type a value). Import adds one `vitalsign` row from the checked rows (`EFormVitalSigns.Import`),
  logged as EFormImport ("Vital signs added from eForm #N (...), taken ...: ..."). It shows in the Medical window's Vital Signs tab.
- The entry is built as the vital sign window builds a new one (`CreateVitalsign`): measurements not imported stay 0; BMI percentile for a
  patient aged 3 to 16 on January 1 (else -1) with LOINC 59576-9; the weight code (SNOMED CT 238131007 or 248342006) for a patient 18 or older;
  LOINC 8302-2 and 29463-7 when height or weight is imported. Each code only when it is in the office's LOINC or SNOMED CT table.
- Never Auto Import (the 26.2 document's list has no vital sign; `IsAutoImportAllowed` stays false). No prefill.

### Item 39: Commlog Referral Types (`OpenPmsDentalBusiness/Logic/CommlogReferralTypes.cs`, new)

- Setup, Definitions, Commlog Referral Types (`DefL`): color (with No Color), hide, rename; no value (Commlog Types' Usage values automate
  patient commlogs only) and no delete, like Commlog Types. Guidelines text written for it.
- The Communication Item for Referral window (`FrmCommReferral`) has a Type list: None, then the visible types in list order, plus the entry's
  own type when it is hidden ("Name (hidden)"); a type number that is not a definition of the category (deleted outside this program) is shown as
  "Unknown type #N" and kept on save.
- Edit Referral, Add Comm (`FrmReferralEdit`, the only place referral commlogs are made): a new entry starts with the first visible type in
  list order (`GetDefaultCommType`), or none when there is no type. Before, `CommType` was always 0.
- Edit Referral's commlog grid has a Type column (the type's name, the cell shaded with its color).
- `Defs.IsDefinitionInUse`: a referral type is in use when a referral commlog (`ReferralNum` set) has it, so hiding one warns as for other
  categories.
- `Commlog.CommType`'s summary follows the 26.2 document.

### Item 41: automatic Prophy and Perio switch (`OpenPmsDentalBusiness/Logic/RecallProphyPerioSwitch.cs`, new)

- The setting: a check box in Setup, Recall Types ("Automatically switch between the Prophy and Perio recall based on completed
  procedures"), saved at once with an Audit Trail entry (Setup) and a preference signal. Off by default. Its preference row
  (`ForkRecallSwitchProphyPerio`, the fork's own) is not in `PrefName`; it is read and written by name with the shared helpers for fork-owned
  rows (`PrefC.GetRawOrDefault`, `PrefC.UpsertRaw`, as `ModuleSwitchBlur` does) and inserted the first time the office turns the setting on: a
  database without the row reads as off, so no migration step and no adoption change are needed.
- `Recalls.Synch` (one patient) calls `RecallProphyPerioSwitch.SwitchIfNeeded` with the patient's recalls before it works out whether the
  patient is a perio patient. The rules (`Decide`, pure):
  1. Off: nothing is read or changed.
  2. The special Prophy and Perio types (Setup, Recall Types) must both be set, be different types and both have a trigger; otherwise nothing.
     The Prophy type's triggers are the prophy procedures (they include the child prophy codes; the Child Prophy type has no triggers), the
     Perio type's are the perio procedures. A code that triggers both types counts as neither.
  3. Completed means status C, EC or EO, the statuses that set a recall's previous date.
  4. Only a completed trigger procedure dated after the patient's Prophy or Perio recall's previous date counts. So the switch happens once, on
     the sync that follows the completion (every completion path already syncs, and opening the patient's recalls syncs too); a back-dated
     procedure, history entered later, a deleted procedure or a sync with nothing new changes nothing; a type set by hand stays until the next
     trigger procedure is completed; turning the setting on switches nobody for earlier procedures.
  5. The procedures of the newest date decide: only perio ones, Perio; only prophy ones, Prophy; both (or only codes that trigger both),
     nothing.
  6. No Prophy or Perio recall yet: Synch makes a Perio recall instead of a Prophy recall when perio procedures decide.
  7. Never switched: a disabled recall; a patient with both a Prophy and a Perio recall; a patient who is not active (Synch skips them).
  8. Switching changes the recall's type and sets its interval to the new type's default interval, exactly what Set Perio and Set Prophy do;
     Synch then sets the previous date, the due dates and clears the status and note as for any new previous date. Logged as RecallEdit
     ("Recall changed from Prophy to Perio by Recall Sync, automatic Prophy and Perio switch (D4910 completed on ...)").
- Synch All (Recall Types, Sync; `Recalls.SynchAllPatients`) does not switch anyone.

### Migration

None. Levels 2026110771 to 2026110779 stay free (a test checks no step uses them): the definition category is Open Dental's and was added with
the 26.2 schema, no definition ships with the program, and the switch setting's row is written when the office first changes it.
`db/init/new-database.sql` is not affected by this package.

## INFERRED

No public source states any of these; they are this program's choices.

- Item 37: the link names; that the fields start empty (each form is a new measurement); the accepted formats, units and ranges; that systolic
  must be higher than diastolic (else the systolic value is not imported); the entry's date (the day the form was submitted, else the day it
  shows, else today); one entry per form from the rows left checked; an identical entry for the same date is not added again (importing a form
  twice is harmless); a hidden or empty field is not imported, and the last field with an answer counts when two link to the same vital sign;
  that the codes are set as the vital sign window sets them; the validation on save; the import window section.
- Item 39: the options of the category (color, no value, no delete); the default type of a new entry (first visible, else none); that None can
  be chosen; that a hidden or unknown type is kept; the Type column and its color; that no definition is shipped; that only referral commlogs
  count for "in use".
- Item 41: the preference row's name and that it lives outside `PrefName`; where the setting is (Recall Types window); every rule above (the
  trigger codes as the measure of "prophy" and "perio" procedures, the statuses, "newer than the previous date", the newest date deciding,
  both kinds or shared codes changing nothing, the Perio first recall, disabled recalls and double special recalls left alone, the interval
  reset, Synch All not switching).

## Not built

- Vital signs are not added to sheets (the release note names eForms only), and there is no Auto Import for them.
- Paint type TextBelow (`ToothPaintingType` 18) belongs to the tooth chart work and is not drawn.
- No UI was run (OpenPmsDental.exe is not launched in this run); the windows' wiring is checked from their source.

## Questions for the owner

**Decided as built (owner, 2026-11-07):** every question below is settled as the package built it ("all as recommended").

1. Commlog Referral Types: ship a few default types (for example Phone Call, Letter, Email, Visit) in a migration step, or leave the
   category empty for each office to fill (chosen)?
2. Vital signs: are the ranges right (height 10 to 108 in, weight 1 to 1500 lb, systolic 50 to 300, diastolic 20 to 200, pulse 20 to 300)?
   Should metric answers (cm, kg) be converted, or stay rejected (chosen)?
3. Vital signs: should a form start with the patient's latest values (prefill), or empty (chosen)?
4. Prophy and Perio switch: when the deciding procedure is deleted (or set back to planned), should the recall switch back? Now it stays.
5. Prophy and Perio switch: when the newest date has both a prophy and a perio procedure, Perio, or no change (chosen)?
6. Prophy and Perio switch: should EC and EO procedures switch (chosen, as they set the previous date), or only C?
7. Prophy and Perio switch: should Synch All also switch patients (it would switch on procedures completed before the setting was turned on),
   or not (chosen)?
8. The setting is in Setup, Recall Types next to the special types. Should it be in Recall Setup or the Preferences window instead?
9. The preference row `ForkRecallSwitchProphyPerio` is this program's own; Open Dental's name is not public. When an Open Dental 26.2 database
   is adopted one day, the office's setting will not carry over unless the name is confirmed (as for the other unconfirmed names of
   `docs/adoption-runbook.md` step 0.11). Keep it as it is until then?

## Files touched

- New: `OpenPmsDentalBusiness/EForms/EFormVitalSigns.cs`, `OpenPmsDentalBusiness/Logic/CommlogReferralTypes.cs`,
  `OpenPmsDentalBusiness/Logic/RecallProphyPerioSwitch.cs`, `OpenPmsDental/Forms/FormSheetImport.VitalSigns.cs`, `tests/P262MiscTests/*`, this
  note.
- Changed: `OpenPmsDentalBusiness/EForms/EFormFieldsAvailable.cs`, `Data Interface/EForms.cs` (save check), `Data Interface/Defs.cs` (in use),
  `Data Interface/Recalls.cs` (Synch), `TableTypes/Commlog.cs` (comment), `OpenPmsDentalBusiness.csproj`; `OpenPmsDental/Forms/FormSheetImport.cs`,
  `Forms/FormRecallTypes.cs` and `.designer.cs`, `Logic/DefL.cs`, `OpenPmsDental.csproj`; `WpfControlsOD/Frms/FrmCommReferral.xaml` and `.xaml.cs`,
  `Frms/FrmReferralEdit.xaml.cs`, `Logic/EFormL.cs`; `OpenDental.sln`, `OpenDental.Local.slnf`; `docs/release-notes/next.md`.

## Checks (2026-11-07)

- Full build of `OpenDental.Local.slnf` (Debug, x86): 0 errors, no warning in a file this package touched.
- Targeted run with `NoHqTests` 58/58: `P262MiscTests` 75/75 (none skipped: the database tests ran on the scratch database), `EFormTests`
  60/60, `M2EFTests` 46/46, and the assemblies whose source checks read files this package changed: `M2MISCTests` 78/78 (Edit Referral),
  `TaskCategoryTests` 21/21 (`DefL`), `M2AUDITTests` 28/28 (`FormSheetImport`). There is no `RecallTests` assembly (the recall tests of
  `UnitTests` are not run in targeted mode). `ForkMigrationTests` was left out as the task says; this package adds no fork step.
- `P262MiscTests`: the switch rules on objects (`RecallSwitchDecideTests`, 17) and through `Recalls.Synch` (`RecallSwitchDbTests`, 15: setting
  row, off, both directions, manual choice, back-dated, planned and EO procedures, first recall, disabled, inactive patient, both kinds on one
  date, Perio type without triggers); Commlog Referral Types (`CommlogReferralTypesTests` 6, `CommlogReferralTypesDbTests` 3); vital signs
  (`EFormVitalSignsTests` 19, `EFormVitalSignsDbTests` 7: import, no duplicate, errors, current value, no prefill, save check); window wiring
  from the source (`WiringSourceTests` 8, including no step at 2026110771 to 2026110779).
- Scratch databases `test_p262misc_*` are dropped after each run.
