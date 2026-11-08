# Port note: CAP, native intraoral X-ray and camera capture

The capture track was built in a separate imaging session on branches cut from the old `main` ee44421eb, and merged into `main` on `cap/main-1` to `cap/main-3` (2026-11-01). The design is [docs/imaging-capture-design.md](../imaging-capture-design.md); the clinic runbook is [docs/imaging/intraoral-capture-setup.md](../imaging/intraoral-capture-setup.md).

## What landed

- **The merge of `cap/cap-4`** (164 commits, the packages CAP-TW, CAP-0 and CAP-1 to CAP-4, each cut from the one before; reviewed twice in the imaging session):
  - `ImagingDeviceManager/`: the in-repo TWAIN library on the system DSM, built from source. It replaces the prebuilt `ImagingDeviceManager.dll` and the commercial EZTwain library. It also has the source gate, the journal, the quarantine, the capture log and the capture.ini reader (`LocalCaptureConfig`).
  - `ImagingCapture/`: the native UVC camera behind the unchanged FormVideo, the TWAIN capture session (controller, window, keys, sounds), 16-bit DICOM writing, the import into ImageStore, Test capture, unassigned captures and their recovery, the capture settings window and the standard mount templates.
  - Deleted from `Required dlls`: `Eztwain4.dll`, `EZT4Jpeg.dll`, `EZT4Pdf.dll`, `VideoCamLib.dll`, `MediaFoundation.dll`, `MediaFoundation.Extension.dll` and `ImagingDeviceManager.dll`.
  - `tests/ImagingDeviceManagerTests` and `tests/ImagingCaptureTests`, on fakes of the DSM, the data source and the session window.
- **The hook** (design 6.4 and 7.3): three lines in `ControlImages.ToolBarMountAcquire_Click` and the partial `OpenDental/Main Modules/ControlImages.Acquire.cs`, which the capture track owns from now on. It hands Mount/Acquire to `CaptureSessionLauncher` and deletes a mount made for the session that is still empty. Deleting it also deletes the mount's imagedraw rows.
- **Off by default** (until 2026-11-07, see "Capture on by default" below). The session runs only when this computer's capture.ini has `[Session] Enabled=1`. With the default 0, the launcher returns NotStarted before any other check, and the legacy Mount/Acquire loop runs exactly as before. This is also the rollback.
- **Database tests** (`tests/ImagingCaptureTests/Db`). They use one scratch MariaDB per run: a 24.3.63 seed through the fork migration runner, a unique name, dropped at the end. They cover the stores, capture settings, standard templates, unassigned captures, the hook's database path (`CaptureHookMirror`, pinned statement by statement to the partial) and full 4BW sessions.
- **The design document, as a neutral edition.** Commit 8d9a9bf14 (the design as the imaging session wrote it) is not on `cap/main-3`. `cap/main-3` was rebuilt from `cap/main-2` without it: the two later commits (the scale-drawing fix and the database tests) were replayed unchanged onto the hook commit. The neutral edition replaces it (see C2).

## Fixes from the merge review (on `cap/main-3`)

- **C1, the hook after the main window's patient changed.** A task popup's Go To, or a Windows lock that logs off and leaves no patient, can change `ControlImages._patient` while the modal session runs. After the session, the hook used to read `_patient` again:
  - the bound patient's mount or last image was loaded into the other patient's Imaging module;
  - its thumbnail was written into that patient's folder;
  - "Mount kept" and "Mount Deleted" went under the wrong PatNum;
  - with no patient, a NullReferenceException came after the mount was already deleted.

  `TryAcquireWithCaptureSession` now keeps `patientBound` and logs every entry under it. After the session it computes `isSamePatient` (`_patient` and `FormOpenDental.PatNumCur` are still the bound patient). When the patient changed:
  - the empty mount is still deleted (it is the bound patient's), but the module is not cleared of it;
  - `RefreshAfterCaptureSession` only refreshes the module that is loaded now: no SelectTreeNode1, no ThumbnailRefresh, no Adj mode.

  Tests: `CaptureHookDbTests` region "The main window's patient changes while the session window is open". It has three tests: another patient, no patient, and unchanged. `CaptureHookMirror` now reads `_patient` before and after the session. It records the display calls, and pins `_patient.PatNum` to the one comparison.
- **C2, vendor names in the design document.** The neutral edition names roles instead: the clinic's sensor vendor, the vendor's imaging software, the private TWAIN source project ("the DS project"). It drops:
  - the local path;
  - the product, detector and DS names;
  - the FDA record numbers that identify them;
  - the sentences about where the private project's camera knowledge came from.

  Every section number the code cites is unchanged, and the status line is current. The removed text stays in the imaging session's copy of the design (`claude/decouple-crypto-libs-8087ea`, 2a9bb33fa, not pushed) and in the coordination record. `VendorNeutralityTests` now scans `docs/imaging-capture-design.md` and this note.
- **C3, a failed quarantine move deleted the session's mount.** When an image's save failed and its move to unassigned captures failed too, the mount still counted as empty. It was deleted, so the image's only copy, its journal copy, named a mount that no longer existed. Assign then filed such an 8-bit copy unmounted, and could not file a 16-bit copy at all. The controller now counts every quarantine attempt (`CountQuarantineAttempts`, incremented before the move). The launcher keeps the mount, and logs why, when that count is above 0. Test: `CaptureSessionLauncherTests.Completed_NothingSaved_TheQuarantineMoveFailed_CreatedMountKeptForTheJournalCopy`.
- **C4, Assign into a merged or deleted chart.** Assign used to file for the sidecar's PatNum with only a null check, so after a merge the image went into the archived chart. Now:
  - `PlanAssign` refuses a Deleted patient.
  - It refuses a patient merged into another, and names the surviving PatNum and Reassign. The new member `ICaptureRecoveryStore.GetMergedIntoPatNum` follows later merges, through `PatientLinks.GetLinks` and `PatientLinks.WasPatientMerged`. No OpenDentBusiness change was needed.
  - An archived patient who was not merged is still filed, because offices archive inactive patients.
  - `GetPatientLine` shows a status other than Patient ("(PatNum 42, Archived)").
  - A 16-bit capture of a merged patient stays in unassigned captures, as before, because Reassign is never offered for 16-bit.

  Tests: three new `CaptureRecoveryFilerTests`, and `CaptureRecoveryDbTests`, which do a real `Patients.MergeTwoPatients` (refused, then Reassign files it for the surviving patient), a merge chain, a deleted patient and an archived one.
- **C5, no guard against the removed binaries.** `tests/ImagingDeviceManagerTests/RemovedImagingBinariesTests` (design 13) checks three things:
  - none of the seven DLLs is back in `Required dlls`;
  - no Reference, HintPath, Content or None item of any `*.csproj`, `*.targets` or `*.props` names one of them (parsed as XML, so comments are ignored);
  - every ProjectReference to `xImagingDeviceManager.csproj` resolves to the in-repo `ImagingDeviceManager\xImagingDeviceManager.csproj`.
- **C6, the `Directory.Build.targets` comment.** It no longer says that ImagingDeviceManager is missing from the repository and ships prebuilt. No project is substituted today, and the target stays as a safety net. The "EzTwain4.dll not found" message texts in the legacy paths are left to their files' owners.

## Reviews and test results

- **Reviews in the imaging session**, on `cap/cap-4`:
  - Round 1: four read-only lenses (patient safety, session logic and threading, data and UI, settings and recovery). 23 issues: 17 confirmed and fixed, 3 refuted, 3 duplicates.
  - Round 2: four new lenses (windows at runtime, library file handling, test strength, a second safety pass) and a hang investigator. 23 issues, 20 confirmed and fixed, plus round 1's 4 open minor issues and the hang.
  - Each round ended with a regression review, and its findings were fixed.
- **The merge review** on `cap/main-3`: C1 to C6 above.
- **Database tests:** the 55 tests in `tests/ImagingCaptureTests/Db` all pass on the scratch database. None were inconclusive. No `capdb` database was left on the local MariaDB after the run.
- **Final check** (2026-11-01, `cap/main-final`, from `cap/main-3`):
  - A cold full build through the build lane: 0 errors. The only warnings in the capture projects are two CS0649 in the test fake `FakeTwainDsm.cs`, which are older than CAP-4.
  - `run-tests -Mode Full`: 83 assemblies, 5515 tests, 5513 passed, 0 failed. The 2 skipped tests are in M201Tests (the published 26.1 reference documents), and main skips the same 2.
    - ImagingDeviceManagerTests 501/501.
    - ImagingCaptureTests 751/751, with the database tests.
    - ImagingTests 43/43.
  - `tools/Test-NoHqHosts.ps1` passes.
  - The OpenDental output has `ImagingCapture.dll` and `ImagingDeviceManager.dll`, and the second is the one built from `ImagingDeviceManager/` (the same file hash). No EZTwain, VideoCamLib or MediaFoundation DLL is anywhere in the build output.
  - `OpenDental.sln` has 113 projects with no duplicate name, path or GUID, and no duplicate configuration line. `OpenDental.Local.slnf` has 108 entries, with no duplicates. Each entry exists and is in the sln.

## Still not verified

- **No hardware.** No DSM, sensor or camera has been attached. Everything ran against the fake DSM, the twain-contract 1.1 helper fake and a fake capture session. The clinic run (ask W2, the CAP-0 acceptance list) and a camera test on a PC with a camera are the next real checks.
- **No window opened at runtime:**
  - FrmCaptureSession, FrmCaptureQuarantine, FrmCaptureSettings and FrmStandardTemplates;
  - the FormVideo notice and the FormImagingDevices and FormImagingDeviceEdit buttons;
  - so nothing is checked yet for layout, DPI, focus or touch, or for real RegisterHotKey, WM_HOTKEY, SystemSounds or a SynchronizationContext Post from another thread.
- **The hook has never run inside OpenDental.exe.** Its database path runs through `CaptureHookMirror`. Its display calls (SelectTreeNode1, ThumbnailRefresh, FillImageSelector) are recorded, not run. A real ODEvent from FormOpenDental, a task popup's Go To and a Windows-lock logoff during a session are reproduced only by setting `PatNumCur` and `_patient` in the tests.
- **A patient change during a notice.** When the settings are not verified, the launcher shows a notice and returns NotStarted, and the legacy loop runs. A patient change while that message box is open makes the legacy loop run for the patient shown then, as with any message box on the legacy path. This was not changed.
- **Owner decisions.** The capture track's open decisions, with the 16-bit filing route needed before 16-bit is turned on, are in the imaging session's owner-question list. It goes into `docs/owner-questions.md` after this merge.
- **CAP-W1, CAP-W2 and CAP-DR** were built on 2026-11-01 as package TWAIN-W ([TWAIN-W.md](TWAIN-W.md), branch `pre/twain-w-1`). 16-bit capture stays off.

## Capture on by default (owner decisions of 2026-11-07)

After counsel's review of the capture's regulatory position (CAP design 18), with no changes, the owner decided:

- **Every new install starts with the capture session on.** `[Session] Enabled` defaults to `1` (`CaptureSessionConfig.EnabledDefault`; the
  Capture settings catalog shows `1` as the default). Setup writes no capture.ini, so the program's default is what a new install gets. The
  session still runs only once the computer's capture settings were saved in Capture settings (design 9.3: unverified settings mean the safe
  values, and the safe value of Enabled is off): until then Mount/Acquire with a Twain Radiograph device shows the "never saved" notice and runs
  the legacy loop. `[Session] Enabled=0` is still the rollback, without a notice. A missing or empty `Enabled` is now hashed as `1`
  (`GatedSettingsHash`, like `Bits` and `Allow16Bit` since 2026-11-01), so a capture settings save logged for a file without the key (when it
  meant 0) no longer matches: the session stays off there until the file is saved again.
- **16 bits stay the default** (`[Twain] Allow16Bit=1`, `[Device.N] Bits=16`), behind the Test capture gate, unchanged. The runbook no longer
  tells patient computers to set `Allow16Bit=0` until counsel's review.
- **Patient use waits for the phantom comparison** (CAP design 18): on the same line-pair and step-wedge phantom, at least the same line pairs
  and steps visible as in the current imaging program's image of that phantom, and left and right matching the lead markers for every sensor
  size; the dentist signs off against these criteria (runbook section 1, rule 2, and section 12).
- **Twain Radiograph only** (CAP design 23): the sensor is set up only as a Twain Radiograph device, never Twain Multi for patients (runbook
  section 1, rule 3, and section 6).

Tests: `CaptureSessionLauncherTests` (a file without the key runs the session once saved; a new install without capture.ini or a save gets the
notice and the legacy loop; `Enabled=0` is the rollback), `CaptureBitDepthDecisionTests.SessionConfig_Defaults`, `GatedSettingsHashTests`
(a missing `Enabled` hashes as `1`, `Enabled=0` differs).
