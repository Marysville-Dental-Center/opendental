# B-PERIO: Perio chart (port note)

Package B-PERIO from [../specs/port-backlog.md](../specs/port-backlog.md), M1 Tier B, wave B1. Branch `port/b-perio`, built on `m1/integration-a2-patch` in the single-wave run. Sources: Open Dental's public 24.4 and 25.2 release notes, the public 24.3 manual pages and the published database documentation, as summarized in [../specs/features-24.4.md](../specs/features-24.4.md) and [../specs/features-25.2.md](../specs/features-25.2.md). No Open Dental binary was observed and no customer-only manual page was read.

## Feature status

| Feature ID | Status | Summary |
|---|---|---|
| `v24_4-chart-perio-implant-removed` | built | An implant procedure whose graphics are hidden counts as removed: no "i" after the tooth number, and the tooth is treated as missing again. |
| `v24_4-chart-perio-attached-gingiva` | built | New read-only **auto Att Ging** row (MGJ minus probing depth) in the three sections that have MGJ. Calculated like auto CAL, never stored. |
| `v24_4-chart-perio-recession-term` | built | Perio Setup check box that relabels the gingival margin row "Recession", reverting to "Ging Marg" while the displayed exam has hyperplasia. |
| `v25_2-chart-perio-print-birthdate` | built | The printed (and saved) Graphical Perio Chart shows the birthdate on its own line under the patient name. |

## What was built

### v24_4-chart-perio-implant-removed

- `PerioMeasures.IsImplantTooth` (new, OpenDentBusiness) holds the rule: a C, EC or EO procedure on the tooth whose code paints as an implant and whose `procedurelog.HideGraphics` is false. A newer implant procedure that is not hidden keeps the tooth an implant.
- `ContrPerio.IsImplant` calls it. The control already clears its cached procedure list each time a Perio Chart opens (constructor), and the Perio Chart is modal, so a removal made in the Chart module is seen the next time the chart opens.
- `FormPerio.GetSkippedTeeth` uses `ContrPerio.IsImplant` with `PerioTreatImplantsAsNotMissing`, so a removed implant on a missing tooth is skipped again (no code change needed there).
- `FormPerioGraphical` no longer draws an implant for a tooth whose implant procedure is hidden.

### v24_4-chart-perio-attached-gingiva

- New row **auto Att Ging** in `ContrPerio`, in the maxillary facial, mandibular lingual and mandibular facial sections (the maxillary lingual has no MGJ row, so none there). The value is MGJ minus probing depth of the same site for the displayed exam, blank when either is blank; zero and negative values are shown as calculated. No "red if" threshold, so it is never drawn red.
- It is filled while the exam loads and recalculated whenever a probing or MGJ cell is entered or cleared. Older exams are not shown in it (same as auto CAL), and a stored `periomeasure` row with SequenceType 8 is ignored.
- Mouse clicks and the up/down arrow keys skip it, as they skip auto CAL. Data entry never lands on it.
- Never stored: `ContrPerio.SaveCurExam` skips calculated types, and `PerioMeasures.Insert`, `InsertMany`, `Update` and `Sync` refuse CAL and AttGing rows (`PerioMeasures.IsCalculatedSequence`). `PerioSequenceType.AttGing=8` was already registered by M1-02.
- Layout: the grid is three short rows (39 px at 96 dpi) taller: 681 instead of 642. It still fits the 608x685 control in FormPerio, so the window is unchanged. The printed chart and the saved image include the row.
- Fixed row numbers had to go, because every row below the first auto Att Ging row moved:
  - `ContrPerio.GetPerioCell` worked out tooth and side from fixed row boundaries (12, 20, 30); it now uses the section layout.
  - FormPerio's voice-navigation helpers (`GetColRowForMobility`, `GetColRowForTooth`, `GoToProbing`, `GoToSequenceType`) used fixed rows; they now call the new `ContrPerio.GetTableRowForSection(section,sequenceType)`.
  - FormPerio's text-chart printout: print size 602x642 becomes 602x681, the F/L side letters move down with the rows, and the summary below the grid starts 20 px lower (a 21 px gap instead of 40, so the summary still fits on the page).
- The perio image attached to a claim (Claim Edit > *Perio*, and the Canadian attach-perio button, both `FormClaimEdit.butAttachPerioHelper`) builds its own grid and draws it into a bitmap of fixed size. The 24.3 sizes (grid and grid bitmap 665 high, attached image 715 high with the grid at y=50) cut off the bottom 16 px of the taller grid: the bottom border and the last mandibular facial probing row. All three heights grow by the same 3*13 px (grid 602x704, grid bitmap Scale(704), image Scale(754)), which leaves the 23 px of spare room the 24.3 image had. Worked out from the row arithmetic for every zoom from 100% to 400% at 100% Windows scaling, the bitmap height always holds the grid (a higher Windows scaling only makes the bitmap larger, because the claim's own grid ignores it). Widths are unchanged; at some zooms other than 100% the grid's right edge (up to a few px) already fell outside the Scale(602) width in 24.3, and that is left as it was.

### v24_4-chart-perio-recession-term

- Perio Setup (`FormPerioSetup`) has a new check box, *Use the term "Recession" (reverts to Gingival Margin when hyperplasia is charted)*. Saving a change signals the preference cache and logs "Perio Setup: Recession term on/off" under Setup.
- With the option on, these labels read "Recession" instead of "Ging Marg"/"Gingival Margin", unless the displayed exam has hyperplasia (any gingival margin stored as 101 to 119):
  - the gingival margin rows of `ContrPerio`, on screen and printed
  - FormPerio's count label (`label10`) and the *Ging Marg +* check box (becomes *Recession +*)
  - the printed summary line "Teeth with Recession greater than or equal to"
  - the *Gingival Margin* color label in Graphical Perio Setup (`FormPerioGraphicalSetup`). That window has no exam, so only the option counts there.
- Stored values never change; only the labels do. Entering or clearing a hyperplasia value flips the label at once (`ContrPerio.GingMarginTermChanged`, which FormPerio listens to).

### v25_2-chart-perio-print-birthdate

- `FormPerioGraphical.RenderPerioPrintout`, which serves printing, print preview and Save to Images, draws "DOB: " plus the birthdate in the current culture's short date format on its own line between the patient name and the exam date. The line is skipped when the patient has no birthdate (a year before 1880). The rule is `PerioMeasures.GetBirthdateForPrint`.

## Files touched

- `OpenDentBusiness/Data Interface/PerioMeasures.cs`: new "Perio Chart rules" region (attached gingiva, calculated types, hyperplasia, Recession term and its preference, implant rule, birthdate), plus the write guards in Insert, InsertMany, Update and Sync.
- `OpenDental/User Controls/ContrPerio.cs`: row layout, auto Att Ging calculation, label refresh and the `GingMarginTermChanged` event, `GetTableRowForSection`, `HasHyperplasia`, `GetGingMarginTerm`, implant rule, layout-based `GetPerioCell`, arrow keys and mouse skip, no red for auto Att Ging, save guard.
- `OpenDental/Forms/FormPerio.cs`: voice-navigation rows from the grid, labels follow the term, print size and positions, Setup refreshes the labels.
- `OpenDental/Forms/FormPerioSetup.cs`, `FormPerioSetup.Designer.cs`: the Recession check box (the window is 30 px taller).
- `OpenDental/Forms/FormPerioGraphicalSetup.cs`: the Gingival Margin label.
- `OpenDental/Forms/FormPerioGraphical.cs`: removed implants and the birthdate line.
- `OpenDental/Forms/FormClaimEdit.cs` (outside this package, see below): the three sizes in `butAttachPerioHelper`.
- New test project `tests/BPERIOTests` (55 tests), registered in `OpenDental.sln` and `OpenDental.Local.slnf`.

No schema change, no fork migration step (the level range 2027000501-2027000599 is unused), no CRUD change, and no foundation registry changed.

### Outside this package's files (outsideOwned)

- `OpenDental/Forms/FormClaimEdit.cs`, a shared hot file: three numbers in `butAttachPerioHelper` (`665` becomes `665+3*13` twice, `715` becomes `715+3*13`), no other change. The claim perio attachment is a consumer of `ContrPerio` and could not stay correct otherwise. Other packages that edit FormClaimEdit at the same time should merge cleanly: the hunk is three lines inside that one method.

## INFERRED behaviors (not public; settle by the owner's testing)

1. **Preference name of the Recession option.** Open Dental's name is not in any public document. The option is stored in a `preference` row named `PerioUseRecessionTerm`, the name the spec proposes, read and written by name through `PerioMeasures.PrefNameUseRecessionTerm`. It is deliberately not added to `PrefName`: `tests/RegistryTests/PrefNameTests.cs` keeps unconfirmed names out of the registry. A database without the row reads as off (the 24.3 behavior), and the row is only inserted when someone first turns the option on, so a database whose office never uses it is not changed. If the office's adopted database stores the option under another name, the setting does not carry over; the office ticks the box again.
2. **Where the row sits.** Between Ging Marg and MGJ in each section, so MGJ stays next to the probing rows and the heavy divider lines keep their places. Label "auto Att Ging", by analogy with "auto CAL".
3. **"Ging Marg +" with the Recession term.** The spec lists the check box among the labels that follow the setting, so it reads "Recession +" while the term is Recession. Ticking it still enters coronal (hyperplasia) values, and the first such value turns every label back to "Ging Marg".
4. **Which labels check hyperplasia.** Every label in FormPerio and the printout follows the displayed exam; Graphical Perio Setup has no exam and follows the option only.
5. **Birthdate line text.** "DOB: " plus the short date, matching the "(DOB: ...)" that the text perio printout already shows.
6. **Text printout spacing.** The gap between the grid and the summary shrinks from 40 to 21 px; the summary ends about 20 px lower than in 24.3.

## Deviations from the spec

- The spec's touchpoints name `OpenDentBusiness/TableTypes/Pref.cs` for the Recession preference. Pref.cs is not changed, for the reason in INFERRED 1.
- `PerioMeasures.Insert/InsertMany/Update/Sync` also refuse CAL rows, not just AttGing. CAL has always been "never saved to the db" (enum comment), and no code writes it.
- `OpenDentBusiness/Data Interface/PerioExams.cs` `CreateNewExam`/`IsToothImplant` still uses the 24.3 implant rule. It has no caller in this fork (it served the removed API and eClipboard), and the file is outside this package.

## Interplay with packages built at the same time

- **M2-MISC** (`v25_4-chart-perio-direction`, perio direction radio buttons) also owns `ContrPerio.cs` and `FormPerio.cs`. This package's hunks are in the row layout, data loading and entry, voice navigation, labels and printing, not in the direction radio buttons or `SetLabelFwdReverse`, so the merge should be mechanical. Any new code there must not use fixed row numbers: use `ContrPerio.GetTableRowForSection` (rows below the maxillary facial probing rows moved).
- **M2-01/M2-02 (registries)** or a census: once Open Dental's preference name for the Recession option is confirmed, register it in `PrefName`, move it from `RegistryTests.PrefNameTests.ArrayUnconfirmedProposedNames` to the confirmed list, and point `PerioMeasures.PrefNameUseRecessionTerm` at it. If the confirmed name differs from `PerioUseRecessionTerm`, add a fork migration step that renames an existing `PerioUseRecessionTerm` row.
- **B-DBM** (database maintenance): no new check is needed. If one is added for `periomeasure` rows with SequenceType 7 or 8, deleting them is safe, as nothing reads them.

## Tests

`tests/BPERIOTests` (MSTest v1, x86, .NET 4.8), 55 tests:

- `PerioRulesTests` (no database): attached gingiva values, blanks, zero and negatives; calculated types; the Recession label matrix and hyperplasia; the preference name stays out of `PrefName`; implant rule (statuses, hidden graphics, replacement implant); birthdate format by culture and the 1880 cut-off.
- `PerioDbTests` (scratch database): Insert, InsertMany, Update and Sync never write AttGing or CAL rows; the Recession preference reads off without a row, writes no row when saved off, inserts once, updates, and reads a row written elsewhere.
- `ContrPerioTests` (scratch database; the real `ContrPerio` control, created and drawn off screen): row layout, height and the new row numbers; tooth and side of cells; auto Att Ging values, blanks, old exams, stored rows ignored, recalculation on entry and clearing, never saved, skipped by the arrow keys, never red (pixel check against a red MGJ); the claim perio attachment, drawn at the sizes read from `FormClaimEdit.butAttachPerioHelper`, holds the grid's bottom border and the probing depth in its bottom row (six exams, with an empty cell as the negative control); Recession labels per displayed exam, label flips on entering and clearing hyperplasia with the change event, stored values unchanged; implant marker until removal and after a replacement.
- `PerioUiSourceTests`: FormPerio no longer uses fixed rows, prints the taller grid and relabels from the term; the claim perio attachment's grid, grid bitmap and image are at least 642+3*13 px high and the image holds the grid bitmap below the header; Perio Setup loads, saves and logs the option on both save paths; Graphical Perio Setup label; Graphical Perio implant check and birthdate line order.

Database tests use one scratch database per run (`bperio_<UTC time>_<pid>_<random>`, Open Dental 25.3.48 reference schema, synthetic rows only) on 127.0.0.1:3306, dropped at the end; stale ones from crashed runs are dropped after 12 hours. `BPERIO_TEST_DB=skip` skips them, `BPERIO_KEEP_DB=1` keeps the database.

Not tested automatically: the FormPerio, Perio Setup, Graphical Perio and Claim Edit windows themselves (the desktop client is never launched by tests), saving the claim attachment file, real printing and print preview, the Sparks3D/DirectX graphical chart, and voice commands.

Run for this package: `BPERIOTests` and `RegistryTests` (its `PrefNameTests` keeps `PerioUseRecessionTerm` out of `PrefName`), plus `NoHqTests`. Last run (fix r1, the claim perio attachment): Debug x86 build 0 errors, 376 warnings (baseline); BPERIOTests 55/55, RegistryTests 33/33, NoHqTests 49/49; `tools/Test-NoHqHosts.ps1` passed. With the 24.3 sizes put back in FormClaimEdit, the two new claim attachment tests fail ("Grid control height 665"; "The bottom border (y=679) is inside the 665 pixel high bitmap"). One earlier RegistryTests run failed `Seed25_3DbTests.OpenDental25_3Database_IsLeftAlone` with "Table 'registry_m1_02_tests.preference' doesn't exist" and passed when run again: that test class uses a fixed database name, so runs from other worktrees at the same time can drop it under each other. It is not caused by this package; the fix (a unique per-run name) belongs to the RegistryTests owner.

## Questions for the owner

1. Do you know Open Dental's preference name for the 24.4 "Recession" option (for example from Perio Setup in your 25.3 database)? Until then it is stored as `PerioUseRecessionTerm`.
2. With the Recession term on, should the *Ging Marg +* check box read "Recession +" (as built), or keep "Ging Marg +" because it enters hyperplasia?
3. Is "auto Att Ging" between Ging Marg and MGJ the place you expect, or should it sit on the other side of MGJ, next to the probing rows?
