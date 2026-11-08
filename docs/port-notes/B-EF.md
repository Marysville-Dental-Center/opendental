# Port note: B-EF, eForm designer and Patient Forms window

Package B-EF of [../specs/port-backlog.md](../specs/port-backlog.md) (M1 Tier B, wave B1), built on branch `port/b-ef` from
`m1/integration-a2-patch` during the single-wave run. Sources: the public 25.1 and 25.2 release notes and blog post named in the specs, and
the public 24.3 manual page for Patient Forms. Nothing was decompiled or observed in Open Dental. The 25.2 window design is ours, as the spec
proposes, because Open Dental only says that the window was improved.

## Features

| Feature ID | Status | Summary |
|---|---|---|
| `v25_1-eforms-conditional-children-view` | built | View, open and keep the conditional children of a checkbox or radio button field in eForm setup |
| `v25_2-eforms-patient-forms-window` | built (our design) | Status column, Show filter, toolbar with disabled actions and reasons, per-user view |

### v25_1-eforms-conditional-children-view

- The checkbox and radio button editors (`FrmEFormCheckBoxEdit`, `FrmEFormRadioButtonsEdit`) get a **View** button next to the existing
  "conditional parent for this many child fields" count. It is disabled when the count is 0.
- `FrmEFormFieldChildren` (a stub with commented-out code in 24.3) is finished. It shows the parent's label and a grid of its children: Field
  (plain text for rich-text labels, the title for a medication list, "(page break)" for a page condition), Type, and Shows When (Checked or
  Unchecked for a checkbox parent, the values for radio buttons and Ins Pick, "age <18" for a date parent).
- Double-clicking a child opens its own editor through the designer's normal double-click path (`FrmEFormDefEdit.EditFieldFromChildren`),
  so delete, translations and nested parents work as usual. The list and the count refresh afterwards.
- Renaming a parent's label: on Save, when children name the old label, the editor asks whether to change them to the new label. Yes
  updates their `ConditionalParent`, page breaks included. No leaves them on the old label, so they always show (the fill logic only hides a
  child whose parent it finds).
- Matching rule: a child's `ConditionalParent` equals the parent's label cut to 255 characters. In setup the label is always the English text
  (translations are kept in `languagepat`), so setup matches on `ValueLabel`. The helper also takes the English label of a filled,
  translated form (`EFormFields.GetLabelEnglish`, the 25.2 `ValueLabelEnglish` rule); a test covers it.
- Fix in `FrmEFormDefEdit`: after a checkbox or radio editor closes, the field's index is looked up again, because a child deleted from
  the Children window shifts the list. Without this the old index could point at the wrong field or past the end.
- Logic lives in the new `OpenDentBusiness/EForms/EFormFieldChildren.cs`.

INFERRED: the View button and its place, the window title "Conditional Children", the column set, how trigger values are worded, the
Yes/No wording of the rename question, and that No keeps the old name. The release note only says that a parent's children can be viewed.

Not built (outside the spec or not owned): a View button in the date field editor (`FrmEFormDateFieldEdit`, which also shows a child count
and can be a parent; not in this package's files); updating children's `ConditionalValue` when radio button items are renamed (the Ins Pick
editor from A-EF2 already does this for its options).

### v25_2-eforms-patient-forms-window

Built as the spec's proposed scope, points 1 to 4:

1. **Status column.** Sheets: Kiosk (`ShowInTerminal` > 0), Submitted online (`IsWebForm`), Imported. eForms: None, Waiting for patient
   (status 1), Filled (status 2), plus Imported. Stored statuses 3 and 4 show as Filled. Scanned documents have no status.
   - Imported comes from the audit trail, because no sheet flag records an import and, since 25.2, the eForm status doesn't either. The
     Import window (`FormSheetImport`) now writes, on every successful import, a `SheetEdit` entry with `FKey`=SheetNum and the text
     `Sheet #N (description) imported`, and its existing `EFormImport` entry now carries `FKey`=EFormNum (text unchanged). The test is
     `PatientFormsList.IsImportLog`: signature entries (`SheetEdit` with the SheetNum) and pending-insurance entries don't count.
2. **Show filter**: Both, Sheets, eForms. Sheets also lists scanned form documents, which is the list the window had before eForms.
   The choice and the sort order are remembered per user in one `userodpref` row (new `UserOdFkeyType.PatientFormsView` = 114, ValueString
   like `Show=0;Sort=0`).
3. **Toolbar** in a fixed order: Add Sheet, Add eForm | Pre-Fill, Copy, Import | Kiosk. Each button runs the handler the old buttons
   used. An action that doesn't apply to the selection is disabled, and its tooltip says why: Pre-Fill and Copy need one sheet, Import needs
   one eForm or one patient form or medical history sheet (not consent sheets, not documents), Kiosk needs a form in the kiosk queue. The
   old buttons at the bottom and their two hint labels are gone; the hints are now tooltips. The grid gets the freed space.
4. **Sort** radio buttons kept, and remembered per user with the filter.

Also: the grid refreshes after an import (the status changes), and the Kiosk check looks at all of the patient's forms, not only the
filtered rows.

eClipboard and Web Forms: the window has no action, menu item, setting or call for them. Rows those services made in an office database
still show, with neutral words. Deviation from the spec: it proposed "In eClipboard" and "Web form". Those would name services that are not
part of this program (and "In eClipboard" would no longer be true), so the window says "Waiting for patient" and "Submitted online". A
test checks that no status or tooltip names either service.

INFERRED (the whole item is our design, confidence Low in the spec): the status words, showing "None" for an eForm with no status, listing
documents under Sheets, the action rules and tooltip texts, the separators, the audit text for sheet imports, and the fork-only enum number.

Logic lives in the new `OpenDentBusiness/EForms/PatientFormsList.cs`; the window (`FormPatientForms`) only calls it.

## Files

New: `OpenDentBusiness/EForms/EFormFieldChildren.cs`, `OpenDentBusiness/EForms/PatientFormsList.cs`, `tests/BEFTests/` (project, `TestDb.cs`,
`EFormFieldChildrenTests.cs`, `PatientFormsListTests.cs`, `PatientFormsDbTests.cs`), this note.

Changed, owned by B-EF: `OpenDental/Forms/FormPatientForms.cs` and `.designer.cs`, `OpenDental/Forms/FormSheetImport.cs` (only the two
audit entries at the end of the import), `WpfControlsOD/Frms/FrmEFormFieldChildren.xaml(.cs)`, `FrmEFormCheckBoxEdit.xaml(.cs)`,
`FrmEFormRadioButtonsEdit.xaml(.cs)`, `FrmEFormDefEdit.xaml.cs` (the two editor branches and one new method).

Changed outside B-EF's list:

- `OpenDentBusiness/TableTypes/UserOdPref.cs` (foundation-owned enum, M1-02/M2-02): adds `UserOdFkeyType.PatientFormsView=114`. The spec
  asks for the Show filter to be remembered per user in `userodpref`, and no existing key fits. The column is a signed tinyint, so the
  value must be 127 or less; fork values start at 100 to stay clear of Open Dental's numbers (29 at 25.4), and 114 follows this package's
  migration range (2027001401-2027001499).
- `tests/RegistryTests/EnumPinTests.cs` (M1-02's test): `PatientFormsView` added to the `UserOdFkeyType` exceptions of
  `ChangedEnums_HaveNoMembersOutsideThePublishedList`, which otherwise fails on any member that Open Dental 25.3 doesn't publish.
- `OpenDentBusiness/OpenDentBusiness.csproj`: two `Compile` lines for the new files.
- `OpenDental.sln` and `OpenDental.Local.slnf`: the new test project.

No schema change, no migration step (the level range 2027001401-2027001499 is unused), no preference, no permission.

## Interplay with packages built at the same time

- **M2-EF** (eForms 25.4 and 26.1): `v25_4-eforms-multi-import` will select several rows for import. `PatientFormsList.GetActionStates`
  disables Import for more than one selected row and must be relaxed there; the grid's selection mode is set in `FormPatientForms`.
  `v25_4-sheets-datetimesubmitted` may add a column to the Patient Forms grid (`FormPatientForms.FillGrid`). M2-EF owns `EFormImport.cs`:
  auto import writes its `EFormImport` entry with `FKey` 0, so an auto-imported eForm doesn't show Imported. If it should, that entry
  needs `FKey`=EFormNum and "imported" in its text.
- **M2-02** (26.1 registries) owns `UserOdFkeyType` and `EnumPinTests.cs`: keep value 114 and its exception when the enum and the pin test
  move to the 26.1 list (26.1 adds no `UserOdFkeyType` value after 29 as far as the published docs show). Checked against `port/m2-02`:
  it adds `DefaultTestingTeamJM` (29) at the same place, so `UserOdPref.cs` and the `UserOdFkeyType` line of
  `ChangedEnums_HaveNoMembersOutsideThePublishedList` will conflict at merge. Keep both: the enum ends with `DefaultTestingTeamJM` and then
  `PatientFormsView=114`, and the exception list becomes `{ "DefaultTestingTeamJM","PatientFormsView" }`. M2-02's `M202Tests` pin only
  published numbers, so 114 doesn't trip them.
- **B-AUDIT** adds one line to `FormSheetImport` (the preferred-name and clinic audit after `Patients.Update`), in a different hunk from
  this package's two audit entries at the end of the import. Its entries use `PatientEdit`, so they never count as an import here.
- Any other package that adds a fork-only `UserOdFkeyType` value must avoid 114; `BEFTests` fails if two members share it.

## Tests

`tests/BEFTests` (MSTest v1, x86, .NET 4.8): 29 tests.

- `EFormFieldChildrenTests` (12, no database): matching on label, self, deleted and blank labels, 255-character labels, the English label on
  translated forms, rename and its no-op cases, display text for every field type, trigger text, and agreement with the fill logic
  (`EFormFields.SetIsHiddenConditFlags`).
- `PatientFormsListTests` (14, no database): status words, no hosted-service names, import audit texts, action rules and order, the Show
  filter, merging the status columns, kiosk detection, view parsing, and the enum value.
- `PatientFormsDbTests` (3, scratch MariaDB database at the 25.3.48 level, synthetic rows only): statuses from flags, stored statuses and
  audit entries for sheets, eForms and a scanned document (including the entries that must not count), a patient with no forms, and the
  per-user view in `userodpref`.

Assemblies to run for this package: `BEFTests`, `EFormTests` (eForm designer and import code this package edits), `SheetTests` (sheet
import), `RegistryTests` (the enum pin exception), plus `NoHqTests`. Last run on this branch: all passed (BEFTests 29, EFormTests 60,
SheetTests 31, RegistryTests 33, NoHqTests 49); build 0 errors, 374 warnings (baseline); `tools/Test-NoHqHosts.ps1` passed.

Not tested: the windows themselves. The program was not started (owner rule). Worth checking by hand: the toolbar's look and tooltips on
disabled buttons, the layout at 100% and 150% scaling, the Children window and nested editing, the rename question, and that the Show and
Sort choice comes back after reopening the window as the same user.

## Questions for the owner

1. Are "Waiting for patient" and "Submitted online" acceptable for forms that eClipboard and Web Forms left in the office data, instead of
   the spec's "In eClipboard" and "Web form"?
2. Should scanned form documents show under the Sheets filter (as built), or only under Both?
3. For an eForm with no status, show "None" (as built) or leave the cell blank?
4. Imports made before this change, including those Open Dental 25.3 made in the office, are not shown as Imported, because the text and
   key of Open Dental's audit entries are not public. If the office copy shows its `EFormImport` entries carry the EFormNum in `FKey`, they
   can be counted too. **Answered (2026-11-07, with owner question Switch-over 1):** the look at the office database gave setting names
   only, which do not show audit entries, so the switch-over converts none: the audit trail is copied as it is, and nothing changed in the
   code. `PatientFormsList.IsImportLog` already counts an `EFormImport` entry that carries the EFormNum in `FKey` and says "imported" (not a
   pending-insurance entry), whoever wrote it; an Open Dental 25.3 entry written that way shows its form as Imported, and a form without such
   an entry shows its status. Open Dental 25.3.48 also has the settings `EFormImportAllergiesAsOther` and `EFormImportProblemsAsOther`,
   which this program does not read: its import always files an unmatched allergy or problem under "Other", as 24.3 did
   ([SWMAP.md](SWMAP.md)).
5. Is "fork-only enum values from 100 up" the convention you want for tinyint enum columns like `userodpref.FkeyType`?
6. Should renaming radio button items also update the children's trigger values (the Ins Pick editor already does this)?
