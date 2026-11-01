# B-MED: Medical lists and vital signs

Package B-MED of [../specs/port-backlog.md](../specs/port-backlog.md) (M1 Tier B, wave B1), built in the single-wave run on branch `port/b-med` from `m1/integration-a2-patch`. Sources: the public 24.4, 25.2 and 25.3 release notes, the public 24.3 manual pages named in the specs, and the published 25.3 database documentation. No Open Dental binaries were observed, and only synthetic data was used.

No schema change, no preference, no permission and no fork migration step (the assigned level range 2027000601-2027000699 is unused).

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-lists-medication-search` | built | Word search in Lists > Medications (and its selection mode): words in any order, a brand found by its generic's name, RxNorm code for all-digit text. The 500 ms pause timer is kept. |
| `v25_3-medical-medlist-missingtab` | built | The Missing Generic/Brand tab, its grid, its two convert buttons and the tab control are gone from the Medications window. |
| `v25_3-chart-vitalsign-notes` | built | Edit Vital Sign has a multi-line Notes box bound to `vitalsign.Documentation`; the Medical window's vital-signs column is renamed "Notes". |
| `v25_2-chart-eform-allergy-other-convert` | built | An eForm "Other" allergy shows "Patient entered: ..." and a Convert button in the allergy edit window, and a right-click item in the Medical window. |
| `v25_2-chart-eform-problem-other-convert` | built | The same for "Other" problems, in the problem edit window (FrmDiseaseEdit) and the Medical window's Problems tab. |

### v24_4-lists-medication-search

- `Medications.IsSearchMatchWords(medication,genericName,searchText)` is the rule; `Medications.GetListForSearch` applies it to the cache. `Medications.GetListForPicker` (A-MED) now uses it, so the Medications list and every picker that opens it in selection mode share the one search. Hidden medications stay out unless Show Hidden is checked, as A-MED built it.
- `Medications.GetList` and `Medications.IsSearchMatch` (export, import duplicate checks, lookups by name) keep the plain "contains" match.
- INFERRED (Open Dental gives no detail; the spec defines the behavior):
  - Words are split on spaces, tabs and commas, and matched case-insensitively as parts of words ("zorv hc" finds "Zorvatin HCl").
  - A brand is matched against the text the list shows for it: its own name plus its generic's name. So "metformin" lists the generic and its brands, "hcl metformin" finds them in any order, and a brand-plus-generic search ("glucophage metformin") finds the brand. A generic is not found by a brand's name.
  - All-digit text also matches an RxNorm code that starts with those digits (prefix, so results build up while typing). Digits that are part of a name still match the name.
  - A brand of a hidden generic is still found by the generic's name.

### v25_3-medical-medlist-missingtab

- Removed from `FormMedications`: `tabMedications`, `tabAllMedications`, `tabMissing`, `gridMissing`, `butConvertGeneric`, `butConvertBrand`, `FillGridMissing`, `FillTab` and the tab-change handler. The All Medications grid and its buttons now sit directly on the window, in the same places (moved up by the height of the tab strip; the grid is a little taller).
- `MedicationPats.GetAllMissingMedications` now has no caller. It is left in place (the file is not B-MED's); a later cleanup can remove it.

### v25_3-chart-vitalsign-notes

- `FormVitalsignEdit2014`: new `labelNotes` and `textNotes` (multi-line, vertical scroll bar) under the Interventions box; the window is 70 px taller. The box is filled from `vitalsign.Documentation` before any early return of the load method, and saved into it before the insert or update. No new column; existing text shows up in the box.
- `FormMedical` vital-signs grid: the header "Documentation for Followup or Ineligible" is now "Notes". The column still shows `vitalsign.Documentation`.
- Deviation: the spec lists `OpenDentBusiness/TableTypes/Vitalsign.cs` as a touchpoint for its summary comment. It is foundation-owned and the code does not need it, so it is unchanged.

### v25_2-chart-eform-allergy-other-convert and v25_2-chart-eform-problem-other-convert

The public release note says only that eForm "Other" allergies and problems can be converted to listed ones. Everything below follows the spec; the spec's own behavior is itself marked Medium confidence, and the points marked INFERRED are choices made here.

- Rules and database work: `OpenDentBusiness/Data Interface/OtherEntryConverts.cs` (new). An entry is "Other" when its definition is named "Other" (what `EFormImport` creates), ignoring case and surrounding spaces. The typed text is `allergy.Reaction` or `disease.PatNote`.
- The user flow: `WpfControlsOD/Logic/OtherEntryConvertL.cs` (new), shared by `FormAllergyEdit`, `FrmDiseaseEdit` and the right-click items of `FormMedical`. It lives in WpfControlsOD so the WPF problem window can use it, and it opens the WinForms lists through `FormLauncher`.
- Flow:
  1. Requires `PatAllergyListEdit` (allergies) or `PatProblemListEdit` (problems). Adding a definition from the list still requires `AllergyDefEdit` or `ProblemDefEdit`, checked by the lists' own Add buttons.
  2. Opens the Allergy list (`FormAllergySetup`) or the Problem list (`FormDiseaseDefs`) in selection mode, searched for the typed text. Add starts the new definition with the typed text as its description and, once saved, picks it and closes the list.
  3. If the patient already has another active entry with the picked definition (and the "Other" entry is active), asks whether to merge. Yes: the "Other" entry is set inactive (problems: Inactive, dates unchanged) and the existing entry is kept unchanged. No: both stay active.
  4. The typed text is cleared from Reaction or PatNote unless "Keep entered text as reaction" / "Keep entered text as note" is ticked (unticked by default). From the Medical window's right-click item there is no check box, so the user is asked (Yes keeps it); nothing is asked when there is no text.
  5. Saves and logs: `PatAllergyListEdit`, "Allergy 'Other: (text)' converted to '(definition)'." and `PatProblemListEdit`, "Problem 'Other: (text)' converted to '(definition)'.", with ", merged with the patient's active ... (the converted entry was set inactive)" added for a merge.
- Converting never touches other patients who typed the same text. Picking "Other" again is refused.
- INFERRED:
  - A merged "Other" entry is also re-pointed to the picked definition (rather than left as an inactive "Other" row), so no "Other" row is left to convert again and its history reads correctly.
  - Converting from an edit window first takes the window's other edits (date, SNOMED reaction, active box; problem status, dates, note, problem type, functional status), so nothing typed in the window is lost; the window then closes as saved. The problem conversion runs `Diseases.VerifyCanUpdate`, as Save does, so a pregnancy diagnosis cannot be moved off its vital-sign exams' dates.
  - The lists' search: `FormAllergySetup` has no search box in our code (it comes with 26.1, package M2-MED), so B-MED adds a Search box (`labelSearch`, `textSearch`) that is shown only while converting. It keeps definitions whose description contains every word typed. `FormDiseaseDefs` already has a Description search box; it is filled with the typed text, and the list picks one problem.
  - When the Problem list's search hides a newly added problem (the user renamed it), the list stays open so the user can clear the search and pick it.
  - Only saved entries are offered for conversion; the Medical window's right-click item is hidden for rows that are not "Other" entries.

## Files

New:
- `OpenDentBusiness/Data Interface/OtherEntryConverts.cs`
- `WpfControlsOD/Logic/OtherEntryConvertL.cs`
- `OpenDental/Forms/FormAllergySetup.OtherConvert.cs`, `OpenDental/Forms/FormDiseaseDefs.OtherConvert.cs` (partial classes)
- `tests/BMEDTests/` (project, 4 test files, AssemblyInfo)
- `docs/port-notes/B-MED.md`

Changed:
- `OpenDentBusiness/Data Interface/Medications.cs` (new search region; `GetListForPicker` calls it)
- `OpenDental/Forms/FormMedications.cs`, `FormMedications.designer.cs`
- `OpenDental/Forms/FormVitalsignEdit2014.cs`, `FormVitalsignEdit2014.Designer.cs`
- `OpenDental/Forms/FormMedical.cs` (vital-signs header; one call in Load; a new region with the right-click items)
- `OpenDental/Forms/FormAllergyEdit.cs`, `FormAllergyEdit.designer.cs`
- `OpenDental/Forms/FormAllergySetup.cs` (four hooks), `FormAllergySetup.designer.cs` (hidden Search label and box)
- `OpenDental/Forms/FormDiseaseDefs.cs` (three hook lines)
- `WpfControlsOD/Frms/FrmDiseaseEdit.xaml`, `FrmDiseaseEdit.xaml.cs`
- `OpenDentBusiness/OpenDentBusiness.csproj`, `OpenDental/OpenDental.csproj`, `WpfControlsOD/WpfControlsOD.csproj` (one entry per new file)
- `OpenDental.sln`, `OpenDental.Local.slnf` (the test project)

Not changed: foundation registries (table types, CRUD, `PrefInfos.xml`, permissions, enums), `EFormImport.cs` (reference only).

## Interplay with packages built in parallel

- **M2-MED** (wave C1, built at the same time) owns `FormAllergySetup`, `FormDiseaseDefs`, `AllergyDefs`, `DiseaseDefs` and `Allergies`:
  - `v26_1-setup-allergy-search` adds a permanent Search box to `FormAllergySetup`. B-MED's convert-only box uses the same names (`labelSearch`, `textSearch`) on purpose: if both survive a merge, the build fails on the duplicate fields instead of showing two boxes. At integration keep one always-visible box (M2-MED's), drop B-MED's `Visible = false` lines, and keep the two B-MED hooks: `ShowSearchForOtherConvert()` (fills the box with `TextOtherToConvert`) and `FilterForOtherConvert` in `FillGrid` (fold it into M2-MED's filter, or make it a no-op when M2-MED already filters on `textSearch`). M2-MED's "contains" match and B-MED's every-word match differ only for multi-word text.
  - `v26_1-lists-problem-merge-keep` adds a Merge button to `FormDiseaseDefs`. B-MED's three hook lines (`ShowSearchForOtherConvert` first in Load, `SetNewDefForOtherConvert` and `PickNewDefForOtherConvert` in `butAdd_Click`) do not touch merge code. The merge should stay hidden in selection mode, which covers the convert mode too.
  - B-MED adds no method to `Allergies.cs`, `AllergyDefs.cs`, `DiseaseDefs.cs` or `Diseases.cs`; everything is in the new `OtherEntryConverts.cs`.
- **M2-EF** owns `EFormImport.cs`. B-MED relies on the import naming the catch-all definitions "Other"; if that name changes, change `OtherEntryConverts.OTHER_NAME`.
- **A-MED** (merged): `GetListForPicker` keeps its hidden-medication rule; only its matching changed. The A-MED tests in `tests/MedicationTests` still pass.

## Tests

`tests/BMEDTests` (MSTest v1, x86, .NET 4.8), 31 tests. Rule and wiring tests need no database. Database tests build one scratch database (`forkmig_<run id>_bmed`, fresh 24.3.63 plus the fork migration runner, as `tests/SheetTests` does), use synthetic rows only, and drop it afterwards; they are inconclusive when the local MariaDB does not answer or `FORKMIGRATION_TEST_DB=skip`.

- Medication search: words in any order, case, brand by generic name, RxNorm prefix, hidden medications, a brand of a hidden generic, the A-MED contains-match left unchanged for export and import.
- Missing tab: no trace of the tab, its grid, its buttons or the tab control in the window sources; the list, search and timer are kept.
- Vital sign notes: round trip of `vitalsign.Documentation`; the window loads and saves the box; the Medical window header is "Notes".
- Other convert: rules (keep or clear text, merge, status and dates kept, errors, audit texts, word filter), database conversions with audit rows, merges, another patient left untouched, refusing "Other" again, active-only merge candidates; the windows' wiring (permission checks, pre-search, Add defaults).

Not tested: the windows themselves (the GUI is not launched in test runs), so the layout of the new controls, the right-click menus and the WPF message boxes were not seen on screen.

## Questions for the owner

1. Merge: should the "Other" entry be re-pointed to the picked definition and set inactive (built), or left as an inactive "Other" row?
2. Medical-window right-click: is a Yes/No question about keeping the typed text acceptable, or should it open the edit window instead?
3. Medication search by RxNorm code: prefix match (built) or exact match only?
