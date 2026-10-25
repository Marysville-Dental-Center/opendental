# Port note: M2-NOTES, procedure notes and group notes

Branch `port/m2-notes`, cut from `port/foundation` (M2-01 and M2-02 merged). Built in the single-wave run, beside the other packages. Backlog section: [port-backlog.md, M2-NOTES](../specs/port-backlog.md#m2-notes-procedure-notes-and-group-notes). No fork migration step and no schema or registry change: the three `procnote` columns and the not-in-database `Procedure` fields come from M2-01.

Sources: the 25.4 and 26.1 feature specs (`docs/specs/features-25.4.md`, `features-26.1.md`) and the public pages they cite (release notes for 25.4 and 26.1, the 26.1 database diff for the `procnote` columns, the 24.3 manual pages for Procedure Info and Group Note). No Open Dental program was installed, observed or decompiled; no office data was used.

## Features

| ID | Status | Summary |
|---|---|---|
| `v25_4-chart-groupnote-size` | built | Group Note window is resizable and opens at 990 x 690 (was 615 x 645, fixed). The Notes box grows in both directions, the procedures grid keeps its height (222) and scrolls, the signature boxes stay at the bottom. The size the user leaves it at is remembered per user on the workstation (fork). |
| `v25_4-chart-procnote-merge` | built | Procedure Info and the Group Note window remember the newest `procnote` row when they open. On save, when a newer row exists and this save writes a note row, the new WPF window `FrmProcNoteMerge` shows their note (read-only, with user and time), my note (read-only) and an editable merged note (their note, a separator line, my added text), with Keep Mine, Keep Theirs, Save Merged and Cancel. Signatures, alerts and the audit entry follow the spec. |
| `v26_1-chart-groupnote-second-signature` | built | Group Note window has a second signature box ("Signature 2") with its own user and Change User button (with the domain login option). Saved in `procnote.UserNum2/Signature2/SigIsTopaz2` with every note row. Editing the note, date or attached procedures clears both; the second signer must be another user; the auto-note block, Topaz, lock and the "signature cleared" alert apply to both boxes. The Chart's progress notes show a note as signed when either box is signed and list both signers. |

## How it works

### Group Note window size (25.4)

- `FormProcGroup.designer.cs`: `ClientSize` 990 x 690, no `FixedSingle` border and no disabled maximize box. Anchors: Notes box Top|Bottom|Left|Right (880 x 231); grid Bottom|Left|Right (864 x 222, so it keeps its height); Add/Remove/Save Bottom|Right; Delete, both signature rows and the permission label Bottom|Left. The top rows (dates, user, Lock, Append, Invalidate) keep their positions.
- `FormProcGroup.ApplyRememberedSize` (Load): the opening size becomes the minimum width (so the second box always fits) and a minimum height of 560 (scaled); a remembered size is scaled to the screen, kept within the minimum and the screen's working area, and the window is re-centered. `FormProcGroup_FormClosed` stores the unscaled size when the window is not maximized.
- `OpenDentBusiness/Misc/LocalWindowSizes.cs` (new): `%LocalAppData%\OpenDental\WindowSizes.txt`, one tab-separated line per window and user. Never throws; a bad file means the default size.

### Procedure Note Merge (25.4)

- `ProcNotes.GetNewestForProc` (new): newest row by `EntryDateTime`, then the highest `ProcNoteNum` (two saves in the same second).
- `OpenDentBusiness/Data Interface/ProcNoteMerges.cs` (new): the rules. `IsMergeNeeded` (a newer row by number or time, and this save writes a note row), `GetAddedText` / `GetMergedDefault` (pre-fill), `GetResultText`, `IsResignNeeded`, `GetTheirSignersToAlert`, `GetLogPermission` (Edit Note on Completed Procedure for C/EC/EO, otherwise Edit Procedure) and `GetLogText`.
- `WpfControlsOD/Frms/FrmProcNoteMerge.xaml(.cs)` (new): the window. Returns `MergeChoice` and `NoteMerged`.
- Procedure Info: `OpenDental/Forms/FormProcEdit.NoteMerge.cs` (new partial file). `FormProcEdit.cs` has two one-line hooks: `NoteMerge_Load()` at the start of the load, and `if(!NoteMerge_TryResolve()) { return; }` at the start of `SaveAndClose`, before anything is written (it covers Save, Set Complete and Lock).
- Group Note window: `TryResolveNoteMerge` runs after `EntriesAreValid` in Save and Lock.
- After any choice but Cancel, the window's "old" copy takes the newer note's text, user and signatures, and the newer row becomes the new baseline, so `Procedures.Update` compares against what is in the database now and the window does not ask again.
  - **Keep Mine**: my text and my signature (drawn over my text, so it stays valid).
  - **Keep Theirs**: their text, user and signatures are put back into the window; no note row is written (tested), my other edits are saved. In the Group Note window, if the date or the attached procedures were changed here, their signatures no longer fit and are cleared; the user is asked to sign again when there were any.
  - **Save Merged**: the merged text. A signature drawn in the window over a different text is cleared and the window stays open for signing again ("any merge result different from the signed text clears the signature").
  - **Cancel**: back to the window, nothing saved.
- Alerts: when Keep Mine or Save Merged replaces a signed newer note, its signer(s) get the existing "signature cleared" alert when subscribed, unless they are the user at the keyboard or a switched user. In the Group Note window this goes through the existing prompt and alert code (the old copy now holds their signer); Procedure Info alerts directly and skips a signer that its own save code already alerted.
- Audit: one entry per resolved merge, written after the save: "Procedure note merged with changes by X" (Save Merged), "saved over changes by X" (Keep Mine), "changes discarded; kept the note saved by X" (Keep Theirs), under the note's permission (see above).

### Second signature (26.1)

- `OpenDentBusiness/Data Interface/GroupNoteSignatures.cs` (new): the rules. `GetSignatureKey2`, `CanEditNoteText`, `CanSignBox2`, `GetSignersError`, `GetSecondSignerToAlert`, alert and log texts, and the progress notes' Signed column.
- Group Note window (`FormProcGroup.cs`): box 2 is filled with its key on load, cleared with box 1 when the note text, the date or the attached procedures change, saved by `SaveSignature2` (blank box: `UserNum2` 0), checked in `AreSecondSignatureEntriesValid` (auto-note block, invalid signature, different signer), prompts before a saved second signature is removed, alerts the second signer when someone else cleared it, and logs "Second signature by X" / "... cleared" in the group note's Edit Procedure entry. Change User for box 2 logs a domain-login switch like box 1.
- Saving (`Procedures.Update`, two inserted lines): a new note row is written also when the second signature changed (`ProcNotes.IsSecondSignatureChanged`), and `ProcNotes.SetSecondSignature` decides the row's second signature: as the caller set it when changed; none when the text changed (it was made over the old text); otherwise carried from the newest row with the same text. The last rule keeps a second signature when a caller that never loaded it (most procedure readers, for example `GetOneProc`) saves a note with only the first signature changed.
- Loading: the Group Note window gets the procedure from `Procedures.GetOneProc`, which does not select the new columns; `FormProcGroup.LoadSecondSignature` takes them from the newest row when that row is the note shown. So the readers in `Procedures.cs` (owned by M2-HIST in this wave) did not need to change.
- Chart (`ChartModules.GetProgNotes`, three inserted lines): the procnote query also selects `UserNum2` and `Sig2Present`; the Signed column reads "Signed" when only the first box is signed (unchanged), "Signed: A, B" when both are, "Signed: B" when only the second is. Audit mode adds "(signature 2: B)" after a version's header. Printed progress notes print the grid, so they show the same.
- `ProcNotes.GetIsProcNoteSigned` (Account module) counts either signature.

## INFERRED behaviour (not public; minimal reasonable choice)

1. **Second signature key.** The spec says both signatures are made over the same key data. The second key uses the same fields (procedure date, entry date, user, note, attached procedures) with the second signer (`UserNum2`) in place of the note's user. Each signature is bound to the note and to its own signer; changing the first box's user does not invalidate the second signature. Open Dental's 26.1 key is not public: a second signature written by Open Dental 26.1 could show as invalid here (no 26.1 Open Dental database can be adopted yet).
2. **Editing a note signed by two.** "Only its signer or Group Note Edit Signed, applied to either signature" is read as: every signed box must be the user's own. So the first signer alone may not edit (and clear) a note the second signer signed, without the permission.
3. **Co-signing.** The second box can be signed by a user who may not edit the text (box blank or theirs, or Group Note Edit Signed), when the note can be saved and the user may sign notes. This is the dentist-and-hygienist case in the release notes.
4. **Second signer different from the first** only when both boxes are signed. The first box's user while it is blank is only the note's author.
5. **Merge also on a signature-only save.** The spec asks for the merge only when the user changed the text. A save that writes only a signature also writes a new note row with the older text, so it would put the older text back over the newer one; the window asks in that case too.
6. **Older automatic merge.** When the office has `ProcNoteConcurrencyMerge` on (the 24.3 custom merge in `Procedures.SetNote`), the merge window is not shown and notes merge as before.
7. **Window size per user on the workstation**, in a local file, not in the database: screens differ between workstations, and there is no generic per-user window-size registry entry (`UserOdFkeyType` is foundation-owned and an Open Dental number could collide).
8. **"Printed progress notes print a second signature line"** is met by the Signed column, which is part of the printed grid. The Print Day page keeps its single blank "Signature____" line for a hand signature.
9. Audit texts and the permission for the merge log (Edit Note on Completed Procedure for completed notes, otherwise Edit Procedure) are this fork's wording.

## Deviations from the spec

- Merge on a signature-only save (INFERRED 5).
- Second key with its own signer (INFERRED 1).
- The `SELECT * FROM procnote` readers in `Procedures.cs` were not changed (M2-01's note listed them as M2-NOTES work); the Group Note window loads the second signature itself and `Procedures.Update` carries it, which covers every save path without touching M2-HIST's file more.

## Files touched

- New: `OpenDentBusiness/Data Interface/ProcNoteMerges.cs`, `OpenDentBusiness/Data Interface/GroupNoteSignatures.cs`, `OpenDentBusiness/Misc/LocalWindowSizes.cs`, `OpenDental/Forms/FormProcEdit.NoteMerge.cs`, `WpfControlsOD/Frms/FrmProcNoteMerge.xaml`, `WpfControlsOD/Frms/FrmProcNoteMerge.xaml.cs`, `tests/M2NOTESTests/*`, this note.
- Changed (owned): `OpenDental/Forms/FormProcGroup.cs`, `OpenDental/Forms/FormProcGroup.designer.cs`, `OpenDental/Forms/FormProcEdit.cs` (two hook lines), `OpenDentBusiness/Data Interface/ProcNotes.cs`.
- Changed (outside the owned list, small and local):
  - `OpenDentBusiness/Data Interface/Procedures.cs` (owned by M2-HIST in this wave): two inserted lines in `Update` (the condition and `SetSecondSignature`).
  - `OpenDentBusiness/Db Multi Table/ChartModules.cs` (no M2 owner): three inserted lines in `GetProgNotes`.
  - Project files: `OpenDentBusiness/OpenDentBusiness.csproj` (3 compile entries), `OpenDental/OpenDental.csproj` (1), `WpfControlsOD/WpfControlsOD.csproj` (1 compile, 1 page), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project).
- Not changed although owned: `OpenDental/Main Modules/ControlChart.cs`, `OpenDentBusiness/Data Interface/ProcGroupItems.cs`, both `SignatureBoxWrapper` files (a second instance of the existing control was enough).
- No foundation-owned registry, table class, CRUD, enum, preference, permission or migration file was changed.

## Tests

`tests/M2NOTESTests` (MSTest v1, x86, .NET 4.8), 39 tests:

- `ProcNoteMergeRulesTests`, `SecondSignatureRulesTests` (no database): merge rules, pre-fill, results, re-sign, alerts, log; second key (24.3 key unchanged; second signature valid after the first box's user changes, invalid after a note, date, procedure or signer change), edit and sign rules, different signer, Signed column, `SetSecondSignature` cases.
- `LocalWindowSizesTests`: scratch file in the temp folder; per window and user, bad lines, clamping.
- `NotesDbTests` (scratch database `m2notes_<time>_<pid>_<random>`, built from the 24.3 seed and the fork migrations, dropped afterwards): newest note; second signature saved and valid; cleared when the text changes; kept when only the first signature changes by a caller that never loaded it; cleared on purpose; `GetIsProcNoteSigned`; progress notes Signed column and audit mode; merge detection with two windows, Keep Theirs writes no row, Save Merged writes one; the older automatic merge preference.
- `WiringTests`: the windows' sources and designer (sizes, anchors, hooks, project entries), since the GUI is not launched.

Related existing assemblies run: `GroupNoteTests` (A-GN's group note rules and wiring, which read the same window) and `SignatureTests` (A-SIG's change-user wiring).

## Interplay with packages built at the same time

- **M2-EST** (C1, takes `FormProcEdit.cs` before this package): this package adds only two hook lines and a partial file; merge after M2-EST's edits. If M2-EST also adds code at the top of `SaveAndClose`, keep `NoteMerge_TryResolve` first, before anything is written.
- **M2-HIST** (owns `Procedures.cs` in C2): keep the two inserted lines in `Procedures.Update` (`IsSecondSignatureChanged` in the condition, `SetSecondSignature` before `ProcNotes.Insert`).
- **M2-CARRIER** (C3) and **M2-SHELL** (C4) take over `ControlChart.cs`/`FormProcEdit.cs` from this package; nothing in `ControlChart.cs` changed here.
- **M2-01**: the columns and the `Procedure` fields are used as M2-01 added them. `GetManyProc` (M2-01's reader) returns a procedure twice when two of its notes share the newest second; that is the existing join, not changed here.

## Owner questions

1. Second signature key (INFERRED 1): keep each signature bound to its own signer, or bind the second signature to exactly the first signature's key as the spec words it (then a change of the first box's user would clear the second signature)?
2. Editing a note signed by two (INFERRED 2): should either signer be allowed to edit (clearing the other's signature, with an alert), or only a user with Group Note Edit Signed, as built?
3. Merge on a signature-only save (INFERRED 5): keep, or ask only when the text changed, as the spec says?
4. Window size: per user on each workstation (built), or per user everywhere (would need a `UserOdFkeyType` value from the registry owner)?

## Untested

- The windows themselves (Group Note layout and resizing, the merge window, Change User, Topaz pads): the GUI is not launched in this run; the wiring tests read the sources.
- A second signature written by Open Dental 26.1 (its key is not public).
