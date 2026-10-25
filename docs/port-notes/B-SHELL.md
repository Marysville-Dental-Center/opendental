# B-SHELL: main window and cross-module integration

Backlog package B-SHELL (wave B3 in [../specs/port-backlog.md](../specs/port-backlog.md)), built on branch `port/b-shell` from `m1/integration-a2-patch` in the single-wave run. Fork migration level range: 2027002301 to 2027002399 (one level used). Tests: `tests/BSHELLTests`.

Sources used: the public 25.1, 25.2 and 25.3 release-note pages named in the feature specs, and the specs themselves. No Open Dental binary, install or customer-only page was used. Behaviour the public pages do not describe is marked INFERRED below.

## Feature status

| Feature | Status | Summary |
|---|---|---|
| `v25_1-setup-programlink-selectedprocnums` | built | `[SelectedProcNums]` in program-link command lines, paths and output files |
| `v25_2-ui-blur-module-switch` | built | Preferences, Main Window, Misc checkbox; blurred cover on module bar clicks |
| `v25_3-tools-file-monitor` | built | Help, File Monitor: in-process feed of image folder reads and writes |
| `v25_3-chart-orthochart-nonmodal` | built (one optional item not built) | Ortho Chart opens non-modal, one window per patient |
| `v25_2-chart-medical-rows-bold` | built | Med Urgent and Medical Summary labels are bold |
| `v25_2-bridges-dxc-credit-score-removed` | built | Bridge deleted; program row disabled and its buttons removed by a fork migration step |
| Backlog note: main window uses the A-SIG domain-login helper | built | Startup domain login reads `DomainLogin.IsEnabled()` and `DomainLogin.BuildDomainUser` |

### v25_1-setup-programlink-selectedprocnums (built)

- `WpfControlsOD/Logic/ProgramL.cs`: new overload `Execute(programNum,patient,listSelectedProcNums)`; the old two-argument `Execute` calls it with `null`. `[SelectedProcNums]` is replaced in the command line, the path and the output-file template before the patient-only block, so the literal tag never reaches the other program, with or without a patient. Program links that have their own bridge class (`SendData`) don't use the generic replacement, as before.
- `OpenDentBusiness/Misc/MainWindow/ProgramLinkSelectedProcs.cs` (new): formatting (comma separated, no spaces, grid order, zeros and repeats dropped) and the per-module selection rules:
  - Chart: selected progress-note rows with a ProcNum, group notes (`~GRP~`) skipped.
  - Treatment Plan: `Procedure` tags (active/inactive plans) and `ProcTP.ProcNumOrig` (saved plans); subtotal rows skipped.
  - Account: selected ledger rows with a ProcNum.
- Hooks (one call each): `ControlChart`, `ControlTreat`, `ControlAccount` toolbar program-button handlers. Appointments, Family, Imaging and the main toolbar keep the two-argument call, so the field becomes an empty string there.
- Replacement picker: new `MessageReplaceType.ProgramLink` (`WpfControlsOD/Frms/FrmMessageReplacements.xaml.cs`); the `[SelectedProcNums]` row is listed only when the caller supports that type, which only the program-link output file window does (`FormProgramLinkOutputFile .cs`, one line). Email and other templates never replace the field, so they don't list it.
- INFERRED (the spec says the rules are our own design; the public program link pages don't mention the field): everything above.

### v25_2-ui-blur-module-switch (built)

- Setting: Preferences, Main Window, Misc, "Blur when switching between modules to prevent accidental clicking" (`UserControlMainWindowMisc`).
- Storage: the fork's own preference row `ForkModuleSwitchBlur` (bool, off), read and written by name through `PrefC.GetRaw`/`Prefs.UpdateRaw` (`OpenDentBusiness/Misc/MainWindow/ModuleSwitchBlur.cs`). Open Dental's preference name is not public. The spec only proposes `ModuleSwitchBlur`, and `tests/RegistryTests/PrefNameTests.cs` explicitly keeps that name out of `PrefName` until it is confirmed, so no `PrefName` entry, no `PrefInfos.xml` entry and no seed row were added. A missing row means off; `ModuleSwitchBlur.Save` inserts the row the first time the setting is turned on. No migration step adds it, so an adopted Open Dental database is not changed.
- Behaviour (`OpenDental/Main Modules/FormOpenDentalBShell.cs`, hooked by one line at the end of `moduleBar_ButtonClicked`): after the new module is selected and filled, a borderless owned window covers it for 400 ms with a blurred screen snapshot of itself (shrink by 8 and stretch back). The cover never activates (`ShowWithoutActivation`, `WS_EX_NOACTIVATE`) and answers `WM_MOUSEACTIVATE` with `MA_NOACTIVATEANDEAT`, so clicks on it are discarded and the keyboard stays with the module. If the snapshot fails (for example a remote desktop session without a drawing surface), a plain translucent white cover is used. Only module bar clicks with a button blur; module changes made by code (Go To, refreshes, the load simulation) don't.
- INFERRED: the 400 ms, the blur method, the fallback cover and the "only user clicks" rule (all from the spec's "our design").

### v25_3-tools-file-monitor (built)

- Capture (`OpenDentBusiness/Misc/MainWindow/FileMonitor.cs`): a static, in-process event. `FileAtoZ` and `ImageStore` each got one line at the top of the hooked method, `using FileMonitorScope fileMonitorScope=FileMonitor.Begin(...)`. While nobody captures, `Begin` reads one static bool and returns null. The scope records the start time, the duration, the size (read from the file system afterwards for reads, writes, copies and moves on local storage only) and the result: an exception that leaves the operation marks the row with its message (taken from a first-chance exception handler that is subscribed only while capturing).
- Hooked: `FileAtoZ.CreateDirectory`, `CreateDirectoryRelative`, `ReadAllText`, `ReadAllBytes`, `WriteAllText`, `WriteAllBytes`, `GetFilesInDirectory`, `Exists`, `Copy`, `Move`, `Delete`, `DirectoryExists`, `GetImage`, `GetBitmap128` (the `...Relative` variants go through these), and `ImageStore.GetPatientFolder` (the folder existence check), `OpenImage`, `GetBytes`, the four `SaveDocument` overloads and `DeleteDocuments`.
- Window (`OpenDental/Forms/FormFileMonitor.cs`, new): Help, File Monitor, non-modal and single instance. Grid columns Time (ms), Action, Path (relative to the A to Z root), Bytes, Duration (ms), Result; Start/Stop (off when the window opens) and Clear; newest 5,000 rows. When images are stored in the database it says so and disables Start. It notes that cloud storage and a local path override list fewer actions. Nothing is written to disk.
- Hardening (the spec's optional item, taken): opening the window needs the Setup permission and writes a Setup security log entry, because paths name patient folders.
- Not covered: the UI-layer `OpenDental/Misc/FileAtoZ.cs` methods that touch local files directly (`ReadAllText`, `WriteAllText`, `Copy` with a progress window, `OpenFile`) and `ImageStore` methods outside the list above (EOB and amendment attachments, chart letters). They can get the same one-line hook later. The `SaveDocument` rows' duration includes the audit log entry the method writes.
- No Export button: the public excerpt doesn't mention one, and the spec says the feed must not reach the disk unless exported.
- INFERRED: the column set, 5,000 rows, the Setup permission, which methods count as which action.

### v25_3-chart-orthochart-nonmodal (built; inactivity release not built)

- `FormOrthoChart.ShowNonModal(owner,patient,tabIndex,actionSaved)`: opens the chart with `Show(owner)` (owner is the main window). One window per patient: a second open brings the existing window to the front and switches it to the requested tab. The title is "<first tab name> - <patient name>". The window keeps its own patient when the main window selects another one.
- Callers: `ControlChart` (toolbar button, ortho tab menu, ortho grid double-click) and `ControlAppt` (appointment right-click menu). The Chart module refreshes after a save only when it is visible and shows that patient (`ControlChart.OrthoChartSaved`); otherwise the next module switch refreshes it, as before.
- Save closes the non-modal window (setting `DialogResult` alone doesn't close a window opened with `Show`).
- Closing rules (`OpenDentBusiness/Misc/MainWindow/OrthoChartWindowRules.cs`):
  - The edit lock (`patientnote.UserNumOrthoLocked`) is released whenever the window closes, for any reason, but only by the window that holds it.
  - Fix: before, a window closed by the program (not by the user) saved and unlocked even when it did not hold the lock. With a window able to sit in the background showing someone else's chart read-only, that could overwrite or release another user's lock; it now returns without saving or unlocking.
  - [X] still prompts for unsaved edits, as before.
- Log off, user switch or program close started by the user (`CloseOpenForms(isForceClose:false)`, one line in `FormOpenDental.cs`): every ortho chart window that holds the lock and has unsaved edits asks Save / Don't Save / Cancel; Cancel stops the log off. Forced and automatic closes save without asking, as before.
- Patient merged, archived or deleted while the window is open: checked on the existing 4-second timer (also for read-only windows). The window closes with a message, saves nothing (after a merge the rows belong to another patient) and releases its lock.
- Signature boxes, Take Control and the A-SIG change-user code are unchanged.
- Not built: releasing the lock after a period of inactivity. The spec marks it "consider" and VERIFY; 25.3.48's behaviour is not public.
- INFERRED: the merge check (a merge sets the patient merged from to Archived, so a change to Archived or Deleted, or a missing row, closes the window), the log off prompt, and saving on forced closes.

### v25_2-chart-medical-rows-bold (built)

`ControlChart.FillPtInfo`: the label cells of the Med Urgent and Medical Summary rows are bold, like Medications, Allergies and Problems. Values and colours are unchanged (Med Urgent text stays red and bold). Two lines.

### v25_2-bridges-dxc-credit-score-removed (built)

- Deleted `WpfControlsOD/Bridges/DentalXChange.cs` (it held only this bridge, which posted the ClaimConnect login and password to a DentalXChange registration page), its `WpfControlsOD.csproj` entry and the `ProgramL` dispatch branch.
- `ProgramL.Execute` refuses any discontinued link before any dispatch, with a message, so an old button can't reach the generic launcher either (`OpenDentBusiness/Misc/MainWindow/ProgramLinksDiscontinued.cs`).
- `ProgramName.DXCPatientCreditScore` stays, because migrated rows refer to it by name.
- Program Links shows the row as "(discontinued)" (one line in `FormProgramLinks.cs`). Program Link Edit keeps it disabled with no toolbars and saves it disabled.
- Fork migration step `DxcCreditScoreRemovedStep`, level 2027002301 (`OpenDentBusiness/Misc/ForkMigrations/BShellSteps.cs`): deletes the row's `toolbutitem` rows and sets `program.Enabled=0`. It runs on every database because what Open Dental's own 25.2 conversion did to the row is not public (INFERRED). Each statement runs only when it has rows to change, so a database Open Dental already cleaned up (row disabled, no buttons) is not changed, and a second run changes nothing. The row's description is not changed; the label is display-only.

### Main window: domain login through the A-SIG helper (built)

`FormOpenDental` startup domain login now uses `DomainLogin.IsEnabled()` (domain login on and a non-blank path, the same rule as before) and `DomainLogin.BuildDomainUser(guid,Environment.UserName)` (the same "{guid}\{user}" key). Behaviour is unchanged; the rules now live in one place with the change-user prompts.

## Files touched

New:
- `OpenDentBusiness/Misc/MainWindow/`: `FileMonitor.cs`, `ProgramLinkSelectedProcs.cs`, `ModuleSwitchBlur.cs`, `OrthoChartWindowRules.cs`, `ProgramLinksDiscontinued.cs`, compiled by one wildcard line in `OpenDentBusiness.csproj`
- `OpenDentBusiness/Misc/ForkMigrations/BShellSteps.cs`
- `OpenDental/Forms/FormFileMonitor.cs`, `FormFileMonitor.designer.cs`
- `OpenDental/Main Modules/FormOpenDentalBShell.cs`: partial `FormOpenDental` with the blur and the File Monitor menu handler, plus `ModuleSwitchBlurOverlay`
- `tests/BSHELLTests/` (added to `OpenDental.sln` and `OpenDental.Local.slnf`)

Changed, owned by B-SHELL, each edit local:
- `ControlChart.cs`: selection argument, 3 ortho chart openings, `OrthoChartSaved`, 2 bold lines
- `ControlTreat.cs`, `ControlAccount.cs`: selection argument
- `ControlAppt.cs`: ortho chart opening
- `FormOpenDental.cs`: blur hook, ortho chart prompt in `CloseOpenForms`, 2 domain-login lines
- `FormOpenDentalMenus.cs`: File Monitor menu item
- `FormOrthoChart.cs`
- `FormProgramLinkEdit.cs`
- `UserControlMainWindowMisc.cs` and `.Designer.cs`
- `ProgramL.cs`
- `FrmMessageReplacements.xaml.cs`
- `OpenDentBusiness/Misc/FileAtoZ.cs`
- `OpenDentBusiness/Imaging/ImageStore.cs`
- Deleted: `WpfControlsOD/Bridges/DentalXChange.cs`

Outside B-SHELL's list (see "Outside-owned edits"): `ForkMigrationRunner.cs`, `FormProgramLinks.cs`, `FormProgramLinkOutputFile .cs`, the three `.csproj` files, `OpenDental.sln`, `OpenDental.Local.slnf`.

`PatientNotes.cs` (owned) needed no change.

## Outside-owned edits

1. `OpenDentBusiness/Misc/ForkMigrations/ForkMigrationRunner.cs` (M1-01's runner). The assigned range 2027002301-2027002399 is not a date (month 00), and `ValidateStep` rejected anything but `yyyyMMddNN` dates, so the runner would have refused every step and startup. Added `IsReservedPackageLevel`: a level `yyyy00PPNN` from 2027 on is accepted. Dated levels are validated as before. Every package given a `202700PPNN` range needs this; if another package relaxed the check differently, keep one version at merge.
2. `OpenDental/Forms/FormProgramLinks.cs` (later V-PAYSUITE's): one line, the "(discontinued)" label in the list.
3. `OpenDental/Forms/FormProgramLinkOutputFile .cs`: one line, lists `[SelectedProcNums]` in the picker.
4. `OpenDentBusiness.csproj`, `OpenDental.csproj` and `WpfControlsOD.csproj` (new or deleted files), `OpenDental.sln` and `OpenDental.Local.slnf` (test project).

No `PrefName`, `PrefInfos.xml`, permission, enum-file, TableTypes, Crud or seed-row change.

## Interplay with packages built in parallel

- B-DBM edits `FileAtoZ.cs` and `ImageStore.cs`. The File Monitor hooks are single `using` lines at the top of methods (and one inside `GetPatientFolder` and `DeleteDocuments`); a new B-DBM method that touches the image folder can add the same line.
- V-BRIDGE1 (and later V-PEARL2 and V-BRIDGE2) edit `ProgramL.cs` and `FormProgramLinkEdit.cs`. New dispatch branches go anywhere after the discontinued-link check. New bridges get `[SelectedProcNums]` only through the generic launcher.
- B-APPT edits `ControlAppt.cs`, B-TP and B-CLAIM edit `ControlTreat.cs`, B-ACCT and B-CLAIM edit `ControlAccount.cs`, B-INSH edits `ControlFamily.cs`, and B-IMG and V-PEARL1 edit `ControlImages.cs`. B-SHELL changed one call in each of the first three and nothing in the last two.
- A-SIG (merged): the domain-login helper and the ortho chart change-user code are kept; `SignatureTests` still pass.
- M1-02 and M2-02 (registries): if Open Dental's real name for the blur preference is ever confirmed, register it and add a step that copies `ForkModuleSwitchBlur` into it (or the other way round), then drop the fork row.
- M2-SHELL (C4) takes over `FormOpenDental*.cs` and the module controls. The B-SHELL additions to the main window are in `FormOpenDentalBShell.cs`.

## Tests

`tests/BSHELLTests` (MSTest v1, x86, .NET 4.8), 51 tests:
- Unit tests for the `[SelectedProcNums]` rules, the replacement picker (`FrmMessageReplacements.GetReplacementFieldList`), the blur rules and bitmap blur, the cover window (built but never shown), the File Monitor scope, feed and relative paths (including failures and other threads), the ortho chart close rules and the patient-gone rule, and the discontinued-link rules.
- Source wiring tests for the windows that can't be opened in a test run: module calls, main window hooks, ortho chart opening and closing, bold labels, the Program Link windows, the domain-login switch.
- Database tests on a scratch database with a unique per-run name (`bshelltest_<time>_<pid>_<random>`, tables from the 25.3.48 reference schema, dropped afterwards): the DXC step (disable, buttons, idempotent, already cleaned up), the blur preference read and save, `GetPatStatusNoCache`, and real `FileAtoZ`/`ImageStore` calls producing File Monitor rows.

Related existing assemblies run for this package: SignatureTests (FormOrthoChart, domain login), GroupNoteTests (ControlChart), ImagingTests and ChartLetterTests (ImageStore), ApptTests (ControlAppt), VendorDirectTests (ProgramL, bridges, FormOpenDental), ReleaseFlowTests (FormOpenDental), ForkMigrationTests (new step and runner change), RegistryTests (preference names). All pass.

Not tested automatically (needs the GUI, which is not launched in this run): the blur on screen, the File Monitor window, the ortho chart window across module switches and log off, the prompts.

## Owner questions

1. Blur preference name: keep the fork's own `ForkModuleSwitchBlur` row until Open Dental's name is confirmed? If the office uses the setting in 25.3, its value doesn't carry over.
2. File Monitor: keep requiring the Setup permission (Open Dental leaves it open to all)?
3. Ortho chart: release the edit lock after a period of inactivity? Open Dental's 25.3.48 behaviour is not public.
4. Ortho chart: a forced or automatic log off saves open ortho charts without asking, as 24.3 did on automatic shutdown. Is that wanted, or should unsaved edits be discarded?
5. DXC step: it disables the row and removes its buttons even on an adopted Open Dental 25.3.48 database, in case Open Dental's 25.2 conversion left them. Acceptable?
6. Reserved migration ranges (`yyyy00PPNN`): `ForkMigrationRunner.ValidateStep` now accepts them. Is that the intended scheme for all packages?
