# Port note: M2-APPT, blockout permissions and appointment preferences

Branch `port/m2-appt`, built from `port/foundation` (M2-01 and M2-02 merged) in the single-wave run. Backlog section: [port-backlog.md, M2-APPT](../specs/port-backlog.md#m2-appt-blockout-permissions-and-appointment-preferences). Feature specs: [features-25.4.md](../specs/features-25.4.md#v25_4-appts-blockout-permissions-blockouts-permission-split-into-add-edit-and-delete) and [features-26.1.md](../specs/features-26.1.md#v26_1-setup-make-recall-search-pref-preference-make-recall-automatically-searches-for-openings).

Sources: the feature specs, the 25.4 and 26.1 release notes they cite (the 25.4 page only says that the blockout permission was split into Add, Edit and Delete), and M2-02's registry work, which took the numbers and summaries from Open Dental's published 25.4 and 26.1 database documents. No Open Dental binary or install was used, and no documentation text was copied.

No schema change and no fork migration step: the level range 2027003301 to 2027003399 is unused. The permission values, their wording, the security tree entries, the upgrade grants, both preference rows and their hover help all come from M2-02 (see [M2-02.md](M2-02.md)); this package adds none of them.

## Feature IDs

| Feature ID | Status | Notes |
|---|---|---|
| `v25_4-appts-blockout-permissions` | built | Every blockout check and audit entry now uses the permission for its own action. The Security tree entries and the upgrade grants come from M2-02. The rule that dragging a blockout counts as Edit has nothing to attach to (see below). |
| `v26_1-setup-make-recall-search-pref` | built | Preferences > Appointments > General checkbox; both Make Recall paths skip the opening search when it is off. |
| `v26_1-setup-default-prov-unassigned-pref` | built | Combo box moved from Edit Day to Preferences > Appointments > General, together with its footnote; same `ScheduleProvUnassigned` preference, and the code that reads it is unchanged. |

## v25_4-appts-blockout-permissions

The new class `BlockoutPerms` (`OpenDentBusiness/Data Interface/BlockoutPerms.cs`) holds the rules, so the UI only asks it:

| Blockout action (`BlockoutAction`) | Type without NS/DC flag | Type with NS or DC flag |
|---|---|---|
| Create, Paste, Copy | Blockout Add (272) | BlockoutsFlagged (266) |
| Edit | Blockout Edit (13, C# name `Blockouts`) | BlockoutsFlagged (266) |
| Delete, Cut, Clear | Blockout Delete (273) | BlockoutsFlagged (266) |

- `GetPermType(action, isFlagged / blockoutTypeDefNum / schedule)` gives the permission. A schedule that is not a regular blockout (a Web Sched ASAP blockout left in adopted data) uses the unflagged column.
- `ForCurrentUser()` / `ForUser(userod)` read the four permissions without messages. `CanDo`, `CanDoAny` (bulk actions: the unflagged permission or BlockoutsFlagged, after which the action touches only the kinds the user may manage, as in 25.1), `CanChangeType`, `FilterTypes(list, action)`, `CanOpenCutCopyPasteTool()`, `CanReplaceOnPaste()`, `GetPermTypesToDelete(list)`.

Where it is used:

- **Appointments module right-click menu** (`ControlAppt.ContrApptPanel_ApptMainAreaRightClicked`): Add Blockout by Add, Edit by Edit, Copy by Add, Cut by Delete, Paste by Add (for the clipboard blockout's kind), Delete by Delete, the three Clear items by Delete, Blockout Cut-Copy-Paste by Add, Delete or BlockoutsFlagged, and Delete Web Sched ASAP Blockout by Delete. Flagged blockouts use BlockoutsFlagged for each item. DC-flagged blockouts still cannot be cut or copied.
- **Menu handlers** check the same permission again and show the usual "not authorized" message: `menuBlockAdd_Click` (type list for Create), `menuBlockEdit_Click` (type list and check for Edit), `menuBlockCopy_Click`, `menuBlockCut_Click`, `menuBlockPaste_Click`, `menuBlockDelete_Click`, the Clear handlers (`GetBlockoutClearPerms` now reads Blockout Delete), `menuBlockCutCopyPaste_Click`, `DeleteWebSchedAsapBlockout_Click`.
- **Edit Blockout window** (`FormScheduleBlockEdit`): when no type list is passed in, a new blockout lists the types the user may add and an existing one the types the user may edit. Delete needs Delete for the blockout's own kind. Save needs Add (new) or Edit (existing) for the new type, and Edit for the original type, so changing between flagged and unflagged needs BlockoutsFlagged and Blockout Edit.
- **Blockout Cut-Copy-Paste window** (`FormBlockoutCutCopyPaste`): Copy Day/Week, Paste and Repeat need Add or BlockoutsFlagged, Clear Day needs Delete or BlockoutsFlagged, and Replace Existing is unchecked and disabled when the user may not replace (pasting unflagged blockouts with Replace Existing needs Delete as well). The buttons are disabled, and the handlers check again.
- **Audit** (`Schedules.BlockoutLogHelper`, `Schedules.CopyBlockouts`): each entry is written under the action's permission. A clear without a blockout now defaults to Blockout Delete. A paste of days is logged under Blockout Add (and BlockoutsFlagged for flagged blockouts, one entry per kind as in 25.1). With Replace Existing, an extra entry under Blockout Delete, ending in "(existing blockouts replaced)", records that unflagged blockouts were deleted.

Behavior after the upgrade: M2-02's `Seed26_1` grants Blockout Add and Blockout Delete to every user group that holds Blockouts (13) on a database from before 25.4, with the same date settings. Nothing changes until an administrator narrows the permissions.

## v26_1-setup-make-recall-search-pref

- `ControlAppt.butMakeRecall_Click` and the `OtherResult.PinboardAndSearch` case in `ControlAppt.ProcessOtherDlg` read `AppointmentMakeRecallSearchesOpenings`. When it is off, the appointment is still pinned, the search date box is still set to the recall due date, and the view still refreshes. Only `ShowSearch()` and `DoSearch(isForMakeRecall:true)` are skipped. The weekly view behaves as before (no search either way).
- Preferences > Appointments > General: the checkbox `checkAppointmentMakeRecallSearchesOpenings`, "Make Recall automatically searches for openings", in a new group "Recall and Schedules". It is filled and saved in `UserControlApptGeneral`. The preference row (default '1') and its hover text come from M2-02.

## v26_1-setup-default-prov-unassigned-pref

- Edit Day (`FormScheduleDayEdit`): the "Default Prov for Unassigned*" combo, its label and the footnote under the grid (`label2`, and its resx text) are removed. The window no longer reads or writes the preference. The provider list now fills its tab.
- Preferences > Appointments > General: `labelScheduleProvUnassigned` "Default provider for unassigned operatories" and `comboScheduleProvUnassigned` (non-hidden providers, as in the old combo). The preference is saved only when a provider is selected, as Edit Day did, so a hidden provider set earlier stays until another one is chosen. Changing it needs Setup (the Preferences window) instead of Schedules.
- The code that reads `ScheduleProvUnassigned` is unchanged (`Schedules.GetProvSchedsForOp`, `ControlAppt` operatory provider lookup).

## INFERRED behaviors

- **Right-click Cut needs Blockout Delete only.** The spec's table, its menu list and the 25.4 schema text all put cut under Delete. The spec's line "cut needs Delete and Add" sits under the Cut-Copy-Paste tool, which has no cut button. Pasting the cut blockout then needs Add.
- **Replacing overlapping blockouts needs Delete for each one's kind** also when a right-click paste or the Edit Blockout window replaces them (preference `ReplaceExistingBlockout`). The spec says this only for the tool's Replace Existing option. Without it, Blockout Add alone could delete blockouts by pasting over them.
- **The Add Blockout menu item is enabled by Blockout Add or BlockoutsFlagged.** Before, it was always enabled and the handler checked the permission.
- **Blockout Edit alone does not open the Cut-Copy-Paste window**, because the window offers nothing that Edit covers.
- **The extra "(existing blockouts replaced)" audit entry** under Blockout Delete, written when the tool pastes with Replace Existing.
- **Dragging blockouts:** our 24.3 Appointments module cannot move or resize a blockout by dragging (`ControlApptPanel` only draws them), so the spec's rule has no code to hook into. `BlockoutPerms` documents that a move or resize counts as Edit, for whoever adds dragging later.
- **"Date jump" with the Make Recall preference off** is read as the search date (`dateSearch`), which is still set to the recall due date. The 24.3 code moves the calendar only to the first opening the search finds, so with no search the calendar does not move.
- **The Family Recall toolbar button** (`butFamRecall_Click`) does not follow the preference: it calls `DoSearch()` without `isForMakeRecall`, and the spec names only Make Recall and the Pinboard-and-search result. "Recall for Family" in the Other Appointments window does follow it, because it returns that result. See the questions.
- **UI text** written for this fork: the group name "Recall and Schedules" and the label text; the checkbox text matches the spec.

## Deviations from the spec

- `FormApptsOther` is on the owned list, but needed no change: it already returns `PinboardAndSearch`, and `ControlAppt` decides about the search.
- The Security tree entries, the permission wording and the upgrade grants are not in this package; M2-02 built them (`GroupPermission.cs`, `UserControlSecurityTree.cs`, `Seed26_1`).
- The C# name of 13 stays `Blockouts` (M2-02's decision); the code calls it Blockout Edit in comments.

## Files touched

- New: `OpenDentBusiness/Data Interface/BlockoutPerms.cs`; `tests/M2APPTTests/` (project, `BlockoutPermsTests.cs`, `M2ApptDbTests.cs`, `SourceWiringTests.cs`, `Shared/ScratchDb.cs`, `Shared/RepoFiles.cs`, `Properties/AssemblyInfo.cs`); this note.
- Owned: `OpenDental/Main Modules/ControlAppt.cs`, `OpenDental/Forms/FormBlockoutCutCopyPaste.cs`, `OpenDental/Forms/FormScheduleBlockEdit.cs`, `OpenDental/Forms/FormScheduleDayEdit.cs` (with its designer and resx), `OpenDentBusiness/Data Interface/Schedules.cs`, `OpenDental/User Controls/Preferences/UserControlApptGeneral.cs` (with its designer).
- Outside the owned list: `OpenDentBusiness/OpenDentBusiness.csproj` (one `Compile` line for `BlockoutPerms.cs`), `OpenDental.sln` and `OpenDental.Local.slnf` (the test project), and `tests/ApptTests/ApptDbTests.cs` (A-APPT's tests: three audit expectations now use 272 and 273 instead of 13).

Edits in shared files are local. `Schedules.cs` keeps its 25.1 helpers (`GetBlockoutPermType`, `CanManageBlockoutType`, `FilterBlockoutTypesForUser`); they now mean "the edit permission" for unflagged blockouts, and `BlockoutPerms` builds on them.

## Interplay with packages built at the same time

- **M2-02** (merged in the base): supplies `EnumPermType.BlockoutAdd` (272), `BlockoutDelete` (273), the wording of 13, the tree entries, `Seed26_1`'s grants, the preference `AppointmentMakeRecallSearchesOpenings` and both PrefInfos entries. The control names here match its PrefInfos entries (`checkAppointmentMakeRecallSearchesOpenings`, `labelScheduleProvUnassigned`).
- **M2-SHELL** (wave C4) takes over `ControlAppt.cs` and `Schedules.cs`. It should keep the `BlockoutPerms` calls, and any new blockout action (for example drag to move) should call `BlockoutPerms.GetPermType(BlockoutAction.Edit, ...)`.
- **ApptTests** (A-APPT/B-APPT): `BlockoutPermissionTests` still holds (the 25.1 helpers are unchanged); `ApptDbTests` was updated for the new audit numbers.
- No other C1 package owns these files. Packages that add their own test projects to `OpenDental.sln` and `OpenDental.Local.slnf` will conflict on those lines at integration; keep every project.

## Tests

New project `tests/M2APPTTests` (MSTest v1, x86, .NET 4.8; in `OpenDental.sln` and `OpenDental.Local.slnf`):

- `BlockoutPermsTests` (no database): the spec's action table for every `BlockoutAction`; flagged types under BlockoutsFlagged; the 16-combination Add/Edit/Delete/Flagged matrix for every action and kind; single permissions; the Cut-Copy-Paste window and Replace Existing rules; changing type; type lists; null user.
- `M2ApptDbTests` (scratch MariaDB database `m2appttest_<time>_<pid>_<random>` on 127.0.0.1, dropped afterwards; synthetic user groups and group permissions): permissions read through `Security`; the matrix with real groups; the audit permission of each action, flagged blockouts, clears and Web Sched ASAP deletes; pasting days with and without Replace Existing, and for a flagged-only user; the seeded preference default; `ScheduleProvUnassigned` still decides the hours of an operatory with no provider.
- `SourceWiringTests`: the menu, handler and window wiring in `ControlAppt`, `FormScheduleBlockEdit` and `FormBlockoutCutCopyPaste`; both Make Recall paths read the preference before the search; the Preferences controls match their PrefInfos entries and save both preferences; Edit Day no longer shows or saves the default provider.

Results on this branch: M2APPTTests 37/37, ApptTests 76/76, M202Tests 39/39, NoHqTests 49/49; build 0 errors, 376 warnings; `tools/Test-NoHqHosts.ps1` passed.

Not tested: the windows themselves (OpenDental.exe is not launched in this run; the source checks stand in for that), the Preferences window round trip, and Middle Tier.

## Questions for the owner

1. Right-click Cut: Blockout Delete only (this build), or Delete and Add?
2. Should the Family Recall toolbar button also follow "Make Recall automatically searches for openings"? Today the Other Appointments window's "Recall for Family" follows it, and the toolbar button doesn't.
3. Replacing overlapping blockouts from a right-click paste or the Edit Blockout window now needs Blockout Delete (or BlockoutsFlagged) for the replaced blockouts. Keep this?
4. Keep the extra audit entry under Blockout Delete when the Cut-Copy-Paste window pastes with Replace Existing?
