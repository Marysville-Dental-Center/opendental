# Port note: M2-MED, Problem merge, Allergy Setup search, allergy DBM check

Branch `port/m2-med`, cut from `port/foundation` (which holds M2-01 and M2-02). Built in the single-wave run, beside the other M1 Tier B and M2 packages. Backlog section: [port-backlog.md, M2-MED](../specs/port-backlog.md#m2-med-problem-merge-allergy-search-allergy-dbm). Feature specs: [features-26.1.md](../specs/features-26.1.md).

## Sources

Public sources only: the 26.1 release notes (<https://www.opendental.com/site/version26_1.html>) as summarized in the feature specs, and the 24.3 public manual page on the allergy master list that the specs cite. No Open Dental program was installed, observed or decompiled, and no office data was used. Where 26.1's behavior is not public, this note marks the choice as **INFERRED**.

No schema, preference, permission or enum change was needed: M2-01 and M2-02 add nothing for these items, and this package adds nothing to their registries. No fork migration step (the assigned range 2027004201 to 2027004299 is unused).

## Features

| ID | Status | Notes |
|---|---|---|
| `v26_1-lists-problem-merge-keep` | built | The whole merge, since this fork had none: Merge button in Setup > Problems, a dialog that asks which problem to keep, `DiseaseDefs.Merge`. |
| `v26_1-setup-allergy-search` | built | Search box above the Allergy Setup grid, filtering as the user types. |
| `v26_1-tools-dbm-missing-allergydefs` | built | DBM check `AllergyWithInvalidAllergyDef` in the new partial file `DatabaseMaintenances.Allergies.cs`. |

## What was built

### Problem merge (`v26_1-lists-problem-merge-keep`)

- **Problems window** (`OpenDental/Forms/FormDiseaseDefs.cs` and designer): a **Merge** button under Add. It is hidden in selection mode and enabled only while exactly two rows are selected. Outside selection mode the grid now allows several selected rows (it allowed one before; Up and Down already handled several).
- Before the merge the window saves its pending changes (edits, new problems, deletions, order), which it otherwise saves only on closing, so both problems have numbers and the merge sees current data. Their security log entries are written at that moment.
- **Merge Problems dialog** (`OpenDental/Forms/FormDiseaseDefMerge.cs`, new): an explanation that says the merge can't be undone, then two radio buttons. Each shows the problem's name, its ICD-10 and SNOMED CT codes, whether it is hidden, and how many patients have it. Then OK and Cancel. OK asks once more, naming the problem kept and the one deleted.
- **`DiseaseDefs.Merge(keep, discard)`** (`OpenDentBusiness/Data Interface/DiseaseDefs.cs`) moves every reference from the discarded definition to the kept one, then deletes the discarded definition. The references, found by grepping `DiseaseDefNum`:
  - `disease.DiseaseDefNum` (patient problems)
  - `rxalert.DiseaseDefNum`
  - `eduresource.DiseaseDefNum`
  - `familyhealth.DiseaseDefNum`
  - `reminderrule.CriterionFK` where `ReminderCriterion` is Problem (allergy and medication rules are left alone)
  - `ehrtrigger.ProblemDefNumList` (CDS triggers; a space-wrapped list, see `ReplaceNumInSpacedList`)
  - the `ProblemsIndicateNone` preference
- The kept definition is never changed: its name, codes and hidden flag stay, and nothing is copied from the discarded one (business rule 4).
- Afterwards the window refreshes the problem cache (and the preference cache when the preference moved), closes the gap in the item order, selects the kept problem, writes the ProblemDefEdit security log entry, and shows a summary.
- Security log text: `Problem "<discarded>" (#n) merged into "<kept>" (#m). Patient problems moved: k.` This is the spec's wording plus the numbers, so two problems with the same name can be told apart.
- Permission: ProblemDefEdit, with no new permission (26.1 adds none).

### Allergy Setup search (`v26_1-setup-allergy-search`)

- `OpenDental/Forms/FormAllergySetup.cs` and designer: a **Search** box above the grid. It has the focus when the window opens, and the grid filters as the user types.
- Business rule 1 is in `AllergyDefs.IsSearchMatch` and `AllergyDefs.FilterForSetup`: the description contains the search text, ignoring case and spaces around the text, combined with Show Hidden. A blank search shows exactly what the window showed before.
- The window reads every definition once (on open, and after Add, Edit and Merge) and filters in memory, so typing doesn't query the database. Selection mode works on the filtered rows (rule 2). Rows that were selected and are still visible stay selected when the filter changes; hidden-by-filter rows are unselected, so Merge only ever gets two visible rows (rule 3). After Add, Edit or Merge the search text stays and the grid refreshes (rule 4).

### DBM check `AllergyWithInvalidAllergyDef` (`v26_1-tools-dbm-missing-allergydefs`)

- `OpenDentBusiness/Data Interface/DatabaseMaintenances.Allergies.cs` (new partial file), modeled on `DiseaseWithInvalidDiseaseDef`. Database Maintenance lists it by reflection, like every `[DbmMethodAttr]` method; it is marked replication unsafe because Fix may insert a definition.
- Check counts `allergy` rows whose `AllergyDefNum` matches no `allergydef` row (AllergyDefNum 0 included). Fix finds or creates the definition **UNKNOWN ALLERGY** (`AllergyDefs.GetOrInsertUnknown`; a new one is not hidden), points those allergies at it (`Allergies.SetAllergyDefNum`; nothing else in the rows changes), writes one DBM log entry per allergy against its patient, and reports the count. The patients keep their allergy records.

## INFERRED behaviors

- Merge dialog: neither choice starts checked, so the user must pick the survivor (the point of the 26.1 change), and OK asks a second time. The patient count is the number of distinct patients with that problem.
- A patient who had both problems keeps both rows (rule 3); the merge counts these patients before anything moves and the summary says they now have the problem twice.
- A CDS trigger that listed both problems keeps the kept number once.
- `ProblemsIndicateNone`: when it pointed to the discarded problem, it moves to the kept one (for the common "None" and "NONE" duplicate). The summary says so.
- Sheet and eForm definitions refer to problems by name (`problem:<name>` in `sheetfielddef.FieldName` and `eformfielddef.DbLink`), not by number. The merge doesn't change them. It counts the definitions that name the discarded problem exactly (the code compares names exactly) and the summary asks the office to edit them. When both problems have the same name the fields still match, and nothing is reported.
- MyISAM has no transactions. The merge moves references first and deletes the discarded definition last; if it stops part way, the discarded definition still exists and running the same merge again finishes it (tested).
- The Allergy Setup search is a plain "contains" on the whole search text, not a per-word match like the Problems window's search.
- DBM: an existing UNKNOWN ALLERGY definition is reused, a visible one first; the name match ignores case, as MySQL compares it. If only a hidden one exists it is used as it is (not unhidden), the same as `DiseaseWithInvalidDiseaseDef` does with UNKNOWN PROBLEM. "Flagged for review" means the visible UNKNOWN ALLERGY name in the patient's list plus the DBM log entry per allergy; no allergy field is changed.

## Deviations from the spec

- None in behavior. The security log text adds the two definition numbers and the count of patient problems moved.
- The allergy merge keeps its 24.3 behavior (the first selected row survives); 26.1's "choose which to keep" note is about problems only.

## Files

- `OpenDentBusiness/Data Interface/DiseaseDefs.cs`: merge methods in a new region at the end of the class, and `DiseaseDefMergeResult` in the same file.
- `OpenDentBusiness/Data Interface/AllergyDefs.cs`: search and UNKNOWN ALLERGY helpers in a new region at the end of the class.
- `OpenDentBusiness/Data Interface/Allergies.cs`: `GetWithInvalidAllergyDef`, `SetAllergyDefNum` at the end of the class.
- `OpenDentBusiness/Data Interface/DatabaseMaintenances.Allergies.cs` (new).
- `OpenDental/Forms/FormDiseaseDefs.cs`, `FormDiseaseDefs.designer.cs`, `FormDiseaseDefMerge.cs` (new), `FormDiseaseDefMerge.Designer.cs` (new; no resx, like other forms this fork added).
- `OpenDental/Forms/FormAllergySetup.cs`, `FormAllergySetup.designer.cs`.
- `OpenDentBusiness/OpenDentBusiness.csproj` (one line), `OpenDental/OpenDental.csproj` (the two new form files, next to FormDiseaseDefs).
- `tests/M2MEDTests/` (new), `OpenDental.sln`, `OpenDental.Local.slnf`.

No foundation-owned registry was changed.

## Interplay with packages built at the same time

- **B-MED** (M1 Tier B) owns the same data classes and forms in its own wave (`Allergies`, `AllergyDefs`, `DiseaseDefs`, `FormAllergySetup`, `FormDiseaseDefs`, ...), and both are built at the same time in this run. This package only appends new regions at the end of those classes and adds a few lines inside the two forms' `Load` and `FillGrid`. If B-MED also appends at the end of a class, a merge keeps both blocks. If B-MED's eForm "Other problem" conversion creates problem definitions, merging duplicates afterwards is exactly what the new Merge is for.
- **Other DBM packages** (M2-SHELL's `SchedulesDeleteDuplicateBlockouts`, B-DBM) add methods to `DatabaseMaintenances`. This package added only a new partial file and one line in `OpenDentBusiness.csproj`, right after `DatabaseMaintenances.cs`; another package's line for its own partial file may land next to it, and both lines stay.
- `AllergyDefs.IsSearchMatch` mirrors A-MED's `Medications.IsSearchMatch`; B-MED's medication search can use the same idea, and nothing here depends on it.

## Tests

`tests/M2MEDTests` (MSTest v1, x86, .NET 4.8):

- `RuleTests.cs`, no database: the search match and filter (case, blank text, hidden, order), `ReplaceNumInSpacedList` (no partial-number matches, each number once), the merge dialog text, the security log and summary text, the Middle Tier shape of the result, and the DBM method's attribute and signature.
- `ProblemMergeDbTests.cs` and `AllergyDbmDbTests.cs`: scratch databases (`m2med_<time>_<pid>_<random>_<purpose>`) built from the 26.1.70 reference schema embedded in OpenDentBusiness, synthetic rows only, dropped afterwards (`M2MED_KEEP_DB=1` keeps them, `M2MED_TEST_DB=skip` skips). They cover every reference the merge moves, references to other problems left alone, the kept definition's fields unchanged, both rows kept for patients who had both, the preference move, form field counts, refusals, a rerun after an interrupted merge, the order gap closing, and the DBM check and fix (visible UNKNOWN ALLERGY, reuse, DBM log rows, valid allergies untouched, a second check finds nothing).

Results: `M2MEDTests` 28/28 and `EFormTests` 60/60 (it uses `AllergyDefs` and `Allergies`), plus `NoHqTests` 49/49; `tools/Test-NoHqHosts.ps1` passed.

Not tested: the WinForms windows themselves (Merge button state, the Merge Problems dialog, the search box and its focus), because the GUI is not launched in this run, and the Middle Tier path.

## Owner questions

1. Should a problem merge also rewrite sheet and eForm field definitions that name the discarded problem (`problem:<name>`)? Right now it counts them and asks the office to edit them.
2. When the discarded problem is the one set in "Problems indicate none", the preference moves to the kept problem. Is that right, or should the merge refuse?
3. Should the allergy merge also ask which allergy to keep, as the problem merge now does? 26.1's release note mentions problems only.
4. The DBM check reuses a hidden UNKNOWN ALLERGY if that is the only one. Should it unhide it instead?

## Integration (branch `port/int-7`)

- `FormAllergySetup` conflicted with B-MED (merged on `port/int-2`), which had added a Search box with the same names, shown only while converting an eForm "Other" allergy. As B-MED's note asked, one box stays: this package's, always shown, with its layout and its "contains" filter. While converting, B-MED's hooks still work: `ShowSearchForOtherConvert` fills the box with the typed text and sets the title, and `FillGrid` passes a blank search to `AllergyDefs.FilterForSetup` and lets `FilterForOtherConvert` keep its every-word match. The list is read before the convert text fills the box (the box's TextChanged now filters the list). B-MED's `Visible = false` lines, its second TextChanged handler and its own `textSearch_TextChanged` are gone.
- `FormDiseaseDefs` merged without conflicts: Merge stays hidden in selection mode, which covers B-MED's convert mode.
- B-DBM (merged) added no allergy check, so `AllergyWithInvalidAllergyDef` is the only one.
