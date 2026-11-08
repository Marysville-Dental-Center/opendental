# Port note: P262-PERIO, Perio Chart (Open Dental 26.2 items 14 to 16)

Branch `p262/perio-1`, cut from `p262/schema-1` (39be2b04b). One of the nine feature packages of the Open Dental 26.2 port (owner decision of
2026-11-07): the Perio Chart items of the release notes' Chart Module section. Item 12 of the same section (the extraction graphic) belongs
to the WPF tooth chart work and is not here; nothing in the 3D/2D tooth chart drawing is changed, and the paint type TextBelow (18) is not
drawn.

| Item | Release notes (paraphrased) | Status |
|---|---|---|
| 14 | Several flags can be added at once to all selected teeth on the Perio Chart | built |
| 15 | Several cells can be selected in any editable data row of the Perio Chart | built |
| 16 | New settings show values of past exams on the Perio Chart | built |

## Sources

Public sources only, as the owner decided. No Open Dental program was installed, observed or decompiled, and no office data was used.

- The 26.2 release notes (https://www.opendental.com/site/version26_2.html), Chart Module section: one line per item, no link, no detail.
- The public Perio Chart manual page (`manual/perio.html`, which the site labels Manual v24.4; the version pages `help254`, `help261`,
  `help262` are for customers only and were not read): cells are entered by clicking, "click and drag" selects several cells to enter data
  in all of them; the flags (P, C, B, S) sit on the probing rows; flags go on a skipped tooth by selecting its gray cell; teeth are selected
  by their numbers for SkipTeeth; the check box Show current exam only, a per-user setting; up to five earlier exams are grayed.
- The public Perio Chart feature page (`site/0_periochart.html`), which describes the window as 26.2 has it: the exams panel has **Show
  previous exams** ("Show or hide data for previously completed perio exams") and **Show previous probing values** ("Show or hide probing
  values for previously completed perio exams"); earlier exams are grayed out, the selected exam is in dark text.
- `OpenDentalDiffDocumentation26-2.xml`: one change for these items, `userodpref.FkeyType` value 31 `PerioShowCurrentProbingOnly`, "Whether
  or not to display probing values for only the currently selected exam in the Perio Chart. When true, this shows only probing values from
  the current exam." (already in place on `p262/schema-1`).
- `OpenDentalDocumentation26-2.xml`: value 8 `PerioCurrentExamOnly` is now described as showing "values, both probing and non-probing, from
  the current exam only". No table, column or other enum value concerns these items.

## What was built

### Item 14: flags on every site of the selected teeth

- Teeth are selected as before, by clicking their numbers (the selection SkipTeeth uses). With teeth selected and no cell selected, the
  flag buttons Bleeding, Suppuration, Plaque and Calculus (and their keys B or the space bar, S, P, C, typed or pressed on the grid) add the
  flag to all six probing sites (three facial, three lingual) of every selected tooth on the selected exam
  (`ContrPerio.ToggleFlagOnSelectedTeeth`). The teeth stay selected, so several flags are added one after another; a site keeps its other
  flags.
- When every one of those sites already has the flag, the same click removes it from all of them instead
  (`PerioMeasures.IsFlagOnEverySite`, `GetBleedingWithFlag`).
- Skipped teeth among the selected ones get nothing; a flag still goes on a skipped tooth by clicking its gray cell.
- With Triplets on, a flag goes to the teeth once (not three times).
- Clicking a cell, or dragging over cells, unselects the teeth (clicking a cell already did); flags then go to the cells as before. The
  changed sites are saved with the exam as flags entered one by one are (the `BleedSupPlaqCalc` rows). A read-only exam (no Perio Edit for
  its date) gets nothing.
- The four flag buttons' tool tips say what they do with teeth selected; the SkipTeeth tip says how teeth are selected.

### Item 15: several cells in any editable data row

- A drag (`ContrPerio.OnMouseMove`) now selects cells in every row that takes data: the selected exam's probing row (as before), Mobility,
  Furc, Ging Marg and MGJ, in every section that has them. Never auto CAL, auto Att Ging, the probing rows of earlier exams, the tooth numbers
  or the date column (`ContrPerio.IsEditableCell`). Mobility's only cell per tooth is the middle one; a drag along the row selects it as it
  passes, the side cells are skipped as a click on them always was.
- Every cell of one drag is of the row type where the drag started; cells of other rows the mouse crosses are ignored. As before, a drag
  over the probing rows may take the facial and lingual probing rows of the selected exam together, and skipped teeth are passed over.
- One number then fills every selected cell (auto CAL and auto Att Ging follow each), Delete clears them all, and the selection stays (no
  advance), as for probing before. A Ging Marg entry with Ging Marg + or Ctrl gives every cell the hyperplasia value. Flags typed with
  several non-probing cells selected change nothing (flags belong to probing sites).
- A mouse press that moves within one cell is now a click on that cell (the direction follows it), also on a probing cell; before 26.2 such
  a press on a probing cell selected it without setting the direction.

### Item 16: Show previous exams and Show previous probing values

- The check box Show current exam only is replaced by two check boxes in the exams panel (`FormPerio`): **Show previous exams** (where the
  old one was) and **Show previous probing values** under it. Both are checked when the user has no setting.
- Show previous exams unchecked: the grid shows the selected exam only, probing and non-probing: no rows for earlier exams, and no grayed
  values of earlier exams in the Mobility, Furc, Ging Marg and MGJ rows (`ContrPerio.LoadData` loads the selected exam only). Show previous
  probing values is then disabled (it keeps its own value).
- Show previous exams checked and Show previous probing values unchecked: earlier exams get no probing rows; the other rows still show the
  latest earlier value, grayed, where the selected exam has none. This is what Show current exam only did before.
- Both checked: the earlier exams' probing rows (up to five) and the grayed values, as with Show current exam only unchecked before.
- Per user, in Open Dental's `userodpref` rows: `PerioCurrentExamOnly` (8) is the opposite of Show previous exams,
  `PerioShowCurrentProbingOnly` (31) the opposite of Show previous probing values (`PerioMeasures.GetShowPreviousExams`,
  `GetShowPreviousProbing`, `SaveShowPrevious`). Read when the Perio Chart opens, saved when it closes (a missing row is inserted, as the
  window has always done for value 8); a change signals the userodpref cache.
- `ContrPerio.SetShowPrevious(showPreviousExams,showPreviousProbing)` is the grid's setting. `SetShowCurrentExamOnly` stays for the grid's
  behaviour before 26.2 (it only hides earlier probing rows). A grid nobody sets, such as the perio image attached to a claim (Claim Edit,
  Perio), shows what it showed before: the selected exam's probing rows and the grayed earlier values.
- The comments of `UserOdFkeyType.PerioCurrentExamOnly` and `PerioShowCurrentProbingOnly` describe their use. Their numbers are unchanged.

## No schema change, no migration step

No table, column, enum value, preference or default row: the two settings are per-user `userodpref` rows written when a user closes the
Perio Chart, and value 31 is already registered on `p262/schema-1`. The levels 2026110741 to 2026110749 are unused.
`db/init/new-database.sql` is not touched.

## INFERRED behaviours

The release notes name the three changes in one line each, and the feature page names the two settings. Everything below is this package's
reading.

- **Item 14, how teeth are selected and flagged:** with the existing tooth selection (clicking tooth numbers) and the existing flag buttons
  and keys, when no cell is selected. "Multiple flags" is read as several flags in turn on the same selection; there is no window to pick
  several flags at once.
- **Item 14, which sites:** all six probing sites of each tooth, facial and lingual, of the selected exam.
- **Item 14, a second click** on a flag that every such site already has removes it (a way to undo); otherwise the flag is added where it
  is missing and never removed.
- **Item 14, skipped teeth** are left out of the selection's flags (the manual puts a flag on a skipped tooth only through its gray cell).
- **Item 14, the selection stays** after a flag, unlike SkipTeeth, which clears it.
- **Item 15, which rows:** probing (selected exam), Mobility, Furc, Ging Marg and MGJ; not the calculated rows or earlier exams.
- **Item 15, one row type per drag**, the type where the drag started; skipped teeth are passed over in every row, as in probing before.
- **Item 15, Mobility:** the middle cell of each tooth only; the side cells stay unselectable.
- **Item 15, a press that moves within one cell** is a click.
- **Item 16, the defaults** (both checked when the user has no row) and the meaning of an existing row: a user who had Show current exam only
  checked (value 8 true) now finds Show previous exams unchecked, which also hides the grayed earlier values in the non-probing rows (the
  26.2 description of value 8). No row is converted; see question 3.
- **Item 16, Show previous probing values** is disabled while Show previous exams is unchecked, and keeps its value.
- **Item 16, the probing rows** with previous probing hidden are the same layout as Show current exam only before (the selected exam's
  probing next to the other rows).
- The check boxes' tool tips (the feature page's wording) and their places (the second under the first, in the exams panel).

## Questions for the owner

**Decided as built (owner, 2026-11-07):** every question below is settled as the package built it ("all as recommended").

1. Item 14: should a flag button with teeth selected also remove the flag when every site has it (built), or only ever add?
2. Item 14: should the selected teeth's flags skip the skipped teeth (built), or flag every selected tooth?
3. Item 16: a user who had Show current exam only checked now sees Show previous exams unchecked, which also hides the grayed values of
   earlier exams in the Ging Marg, MGJ, Furc and Mobility rows. Keep (as built, Open Dental 26.2's meaning of the stored row), or convert
   such users once (a fork migration step) to Show previous exams checked and Show previous probing values unchecked, which shows them
   exactly what they saw before?
4. Item 15: should a drag over the Mobility row also select a tooth when it starts or ends on a side cell of it (it selects the teeth whose
   middle cell it passes, as built)?

## Tests

New project `tests/P262PerioTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`), 36 tests. The real grid
(`ContrPerio`) is created at 96 dpi and driven off screen through its own mouse handlers (`ContrPerioDriven` raises them at cell centres).

- `FlagTeethTests` (9): flags on every site of two selected teeth and several flags in turn; the keys; removal when every site has it and
  completion otherwise, other flags kept; skipped teeth left out, and the gray cell still taking a flag; saving; an older selected exam; a
  cell click and a drag replacing the tooth selection; read only.
- `MultiCellTests` (10): drags in Furc, Ging Marg, MGJ and probing of every section; Mobility's middle cells; one entry for every cell with
  auto CAL, and saving Ging Marg, Mobility and lingual Furc; Delete; the row type of the start; calculated rows and an earlier exam's
  probing row; skipped teeth in every row; flags with several Furc or probing cells; a press within one cell as a click (direction event);
  read only.
- `ShowPreviousTests` (11): the grid with each combination (date rows, grayed values in four rows, an earlier exam's flag), a drawn check of
  the grayed value with a positive control, an older selected exam, the unconfigured grid and `SetShowCurrentExamOnly` as before 26.2,
  entry with previous exams hidden; the per-user rows (defaults, values 8 and 31, no second insert, update, per user, a row from before
  26.2).
- `FlagRulesTests` (2): the add-or-remove rule without a database.
- `PerioWindowSourceTests` (4): the window code that cannot run without a screen (the check boxes' texts, events, load, save, the grid
  call and the disabled state; the flag buttons' calls and tool tips; no overlap with the window's other controls).

The database tests use one scratch database, `test_p262perio_<run id>`, with the reference schema (26.2.0) and synthetic rows only, dropped
afterwards (`P262PERIO_KEEP_DB=1` keeps it; `P262PERIO_TEST_DB=skip` skips them).

Not tested: the Perio Chart window itself (OpenPmsDental.exe is not launched in this run; the source checks stand in for that), real mouse
input and focus, printing, and Middle Tier.

## Files touched

- New: `tests/P262PerioTests/*`, this note.
- `OpenPmsDental/User Controls/ContrPerio.cs`: the two settings and `SetShowPrevious`, loading without earlier exams, the selected teeth's
  flags (`HasSelectedTeeth`, `GetSelectedTeeth`, `ToggleFlagOnSelectedTeeth`), the flag keys with teeth selected, the drag in every editable
  row (`IsEditableCell`), a press within one cell as a click, flags skipping non-probing cells of a multiple selection.
- `OpenPmsDental/Forms/FormPerio.cs` and its designer: the two check boxes (load, save, refill, disabled state), the flag and SkipTeeth tool
  tips.
- `OpenPmsDentalBusiness/Data Interface/PerioMeasures.cs`: region "Perio Chart rules (Open Dental 26.2)" (flag rule, the per-user
  settings). `OpenPmsDentalBusiness/TableTypes/UserOdPref.cs`: two comments.
- `OpenDental.sln`, `OpenDental.Local.slnf` (the test project), `docs/release-notes/next.md`.

## Interplay with the other 26.2 packages

- Packages that add their own test projects to `OpenDental.sln` and `OpenDental.Local.slnf` conflict on those lines at integration; keep
  every project.
- The tooth chart package (item 12 and TextBelow) does not touch the Perio Chart files; this package does not touch the tooth chart.
- `FormClaimEdit.butAttachPerioHelper` (the claim's perio image) is unchanged and shows what it showed before.

## Checks (2026-11-07)

- Full Debug x86 build: 0 errors.
- Targeted run, with `NoHqTests` (58/58): `P262PerioTests` 36/36, `BPERIOTests` 55/55 and `M2MISCTests` 78/78 (both drive or read
  `ContrPerio` and `FormPerio`).
- Scratch databases `test_p262perio_*` (and `BPERIOTests`' own `bperio_*`) are dropped after each run.
