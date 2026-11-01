# BIN-A: Open Dental proprietary binaries, part A (Suni, VBbridges, Sparks3D)

Package BIN-A of the v0.0 release work, run before the rename. Branch `pre/bin-a-1`, from `main` d9c177ca6. Release rule: v0.0 ships no Open Dental proprietary binary. This package removes three of them: the Suni capture driver, the VB helper `VBbridges.dll` and the Sparks3D tooth chart. Sources: the call sites in this repository and the vendors' public pages. No Open Dental binary was decompiled, disassembled or inspected beyond its file name. No documentation text was copied.

## What changed

### 1. Suni direct capture: removed (owner design decision 2, default (a))

This ports the held `decouple/twain` commit 4de709278 to today's tree. Its DLL deletion, commit 45a8b8206, is done here too.

- `OpenDental/Main Modules/ControlImagesOld.cs`: the direct capture through `xImageDeviceManager.SuniDeviceControl` is gone. This covers:
  - the field
  - the Capture toolbar button and its handler
  - the mount it built for a capture
  - the capture callbacks and `GetNextUnusedMountItem`
  - the `KillXRayThread` calls in module unselect, dispose and the image thread
- Before this change, `FormImages` always created a `ControlImagesOld`, which always created the Suni control, so `xImageDeviceManager.dll` was loaded whenever FormImages opened (claims, claim payment batches). That hard runtime dependency is gone. The old Images module itself stays.
- `OpenDental/Forms/FormImagingSetup.*`: the Suni group is removed (exposure, port, type, binned; shown only when a "Suni" program link was enabled). Only the removed driver read those values. The `computerpref` columns (`SensorType`, `SensorPort`, `SensorExposure`, `SensorBinned`) stay in the database and in `ComputerPref`.
- These files are deleted: `Required dlls/xImageDeviceManager.dll` and `Required dlls/SuniUSB.dll`.
- Their references are removed: `OpenDental.csproj` and `UnitTests.csproj` (xImageDeviceManager).
- Suni users switch to Suni's own TWAIN driver, used through the capture track.
- `docs/imaging-capture-owner-questions.md`, CAP design 2: the **Today:** line now reads "removed on 2026-11-01 as the design's default".

### 2. VBbridges.dll: removed, and its actions say "not available in this version"

VBbridges had five static methods and seven call sites. For each one, I checked whether its behaviour could be established from the call site and the vendor's public bridge documentation. None could:

| Method | Call sites | What the call site shows | Public vendor interface | Now |
|---|---|---|---|---|
| `DrCephNew.Launch` (30 fields) | `CodeBase/Utilities/DrCephArgs.cs` (from `WpfControlsOD/Bridges/DrCeph.cs`) | Patient, responsible party and record fields go to a running Dr. Ceph | None found. FYI Technologies publishes no automation or command-line interface; the only public bridge page describes the setup, not the protocol. | The Dr. Ceph bridge says it is not available, before asking for the extra fields. `DrCephUtils.Launch` throws `BridgeNotAvailableException`. |
| `Ecw.LoadRxForm`, `Ecw.GetAppServer` | `FormRxManage.cs` (2), `ControlChart.cs` (2) | In eClinicalWorks tight or full mode, the Rx button opens eCW's own prescription window. The app server then refreshes the eCW panel. | None. eClinicalWorks' integration documents are for partners only. | The Rx button in eCW tight or full mode says the eCW prescription window is not available. The panel refresh that needed the app server is gone. |
| `Patterson.Launch` (18 fields) | `WpfControlsOD/Bridges/Patterson.cs` | Patient, provider, program path and an `.ini` path go to Patterson Imaging | None. No public description of the `.ini` file's sections or keys was found. | The Patterson bridge says it is not available. |
| `Schick3.Launch` | `WpfControlsOD/Bridges/Schick.cs` | Patient ID, last name and first name go to Schick CDR version 4 | None for version 4 | Version 4 says it is not available and points to version 5. **Version 5 (CDR DICOM) is unchanged**: it uses Schick's own `CDRDicom` automation interface, through the interop that is already in the repository. |

- The messages are in `CodeBase/Utilities/BridgeNotAvailable.cs`.
- These bridges' program links and properties are unchanged. `FormDrCeph` and `DrCephArgs` are kept for when an interface is known. Patterson's field limits (`Tidy`, `LTidy`) are kept for the same reason.
- `Required dlls/VBbridges.dll` is deleted. Its three HintPaths are removed (`xCodeBase.csproj`, `OpenDental.csproj`, `WpfControlsOD.csproj`).

### 3. Sparks3D: removed, and SparksToothChart is the only chart

**References and files removed:**
- References in `OpenDentBusiness.csproj`, `OpenDental.csproj`, `WpfControlsOD.csproj` and `UnitTests.csproj`.
- `WpfControlsOD/UI/ToothChart/Sparks3DInterface.cs`.
- `OpenDental/Resources/Sparks3D.txt`: Sparks3D's licence text, with its resource and its entries in the License and Registration Key lists.
- `Required dlls/Sparks3D/`: `Sparks3D.dll`, `Sparks3DXEngine.dll` and `readme.txt`.

**`ToothChartRelay`:** it relays to `SparksToothChart.ToothChartWrapper` only.
- `IsSparks3DPresent`, `GetToothChart()`, the relay events and `TextMovedEventArgs` are gone.
- Methods for things only Sparks3D drew are kept as no-ops, so callers need no branches: ortho wires and elastics, free text, brackets, retained roots and space maintainers.
- In wrapper mode these methods were already no-ops.

**Callers:**
- The Sparks3D branches are removed from all of these:
  - `ControlChart` (start-up gate, perio button, sheet layout, the text-moved handler)
  - `ControlTreat`
  - `SheetPrinting.GetToothChartHelper`
  - `FormPerio`, `FormPerioGraphical`, `FormSheetDefEdit`, `FormToothChartBig`
  - `FormOpenDental` (a commented-out block)
- `UnitTests/UI Testing/Form4kTests.cs` drew a Sparks3D chart from a sandbox button; that code is gone.

**Settings:**
- The Graphics window loses its three DirectX 11 groups, its "This window is obsolete!" label and its DirectX 11 explanation.
- The DirectX 9, OpenGL and Simple 2D group is now called "Tooth Chart Technology" and moves to the left.
- `DirectX11ToothChartUseIfAvail` (preference) and `computerpref.GraphicsUseDirectX11` are no longer read or written. Both names stay in the database.

**Business layer (`OpenDentBusiness/SheetFramework/ToothChartHelper.cs`):**
- It drew its chart image with Sparks3D, which it can no longer use.
- SparksToothChart is a WinForms project that references the business layer, so the business layer cannot reference it back.
- Its callers are:
  - the treatment plan PDFs of the API and mobile paths (`TreatPlans.CreateSheetFromTreatmentPlan` and the `Documents` methods built on it)
  - `PerioExams.CreatePerioPDF`
  - `Procedures.CompleteProcedureAutomation` (for the mobile apps)
- OpenDental.exe calls none of these. It draws its own images through `SheetPrinting.GetToothChartHelper`.
- They now get a plain image of the same size, in the chart's colours, with "Tooth chart image not available" written on it. A sheet that expects an image still prints. **INFERRED.**

**Release channel:** `"Sparks3D"` is removed from `ForkReleases.ListSpecialDirs` and from UpdateFileCopier's `_listSpecialDirs` (one line each). `docs/release-process.md` is updated to match.

### 4. DirectX chart fallback (`SparksToothChart/ToothChartWrapper.cs`)

The DirectX 9 chart needs `d3dx9_43.dll` for its fonts and mesh optimisation (`SharpDX.Direct3D9.Font`, `D3DX.OptimizeFaces`). That file comes from Microsoft's legacy DirectX End-User Runtime, which a clean Windows does not have.

- **The `DrawMode` setter** now calls `StartFirstWorkingMode`:
  - DirectX is not tried when `d3dx9_43.dll` cannot be loaded (checked once per process with `LoadLibrary`).
  - A mode that cannot start falls back to the next one: DirectX, then OpenGL, then Simple 2D.
  - A half-made DirectX control is released, so a later attempt starts clean.
  - There is no dialog. The first fallback of the process is written to the `ToothChart` log, once.
  - Simple 2D is the last resort. As before, its failure is not caught.
  - Before this change, a DirectX failure went straight to Simple 2D. It also left a non-null DirectX control behind, which made a later switch back to DirectX draw a blank chart.
- **`FormGraphics.TestDirectXFormat`** treats any mode other than DirectX as a failure. It used to test only for Simple 2D, which the fallback to OpenGL would have broken.
- **`FormGraphics.GetPreferredDirectXFormat`** returns "invalid" at once when the runtime is missing, instead of trying every format, which can take minutes.
- **Tooth chart default:**
  - New computers still default to DirectX (`ComputerPrefs.GetDefaultComputerPref`).
  - At start-up, a computer with no working DirectX format is set to the next mode, OpenGL (it was Simple 2D). The wrapper itself falls back further to Simple 2D if OpenGL cannot start.
  - So DirectX is used where the runtime is installed, and OpenGL or Simple 2D on a clean Windows.
- **The Perio Chart's Graphical button** says that the DirectX End-User Runtime is missing, instead of opening a window that would close with an error. The graphical perio chart draws only in DirectX.

## Decisions (INFERRED)

1. **Every VBbridges method is "not available".** No vendor's public pages describe the interface: not Dr. Ceph, eClinicalWorks, Patterson or Schick CDR 4. I did not guess a protocol (for example an ini layout or a COM ProgID), because a guess could send a patient to the wrong record. I searched the web for public vendor documentation and found only Open Dental's own setup pages, which describe setup but not the protocol.
2. **The Dr. Ceph bridge shows its message before starting Dr. Ceph or asking for the extra fields.** It no longer starts the program alone, because staff could take a program opened with no patient for one opened with this patient.
3. **The eClinicalWorks Rx button shows the message instead of the Rx window.** The eCW panel is no longer refreshed after it. The Rx button outside eCW tight and full mode is unchanged.
4. **The business layer's tooth chart image is a plain "not available" image** rather than null or an error, so the API and mobile PDF paths still produce a document. OpenDental.exe is unaffected.
5. **With no working DirectX format, start-up sets OpenGL** (it was Simple 2D), following the requested fallback order.
6. **The DirectX 11 preference and computer setting stay in the database, unread.** Names never change. A later cleanup migration could delete them; I did not add one.
7. **Text on the tooth chart is still not drawn.** That covers adding and moving free text in the Chart module's Draw tab, and ortho hardware. SparksToothChart never drew these (it was already so without Sparks3D). The controls stay; see owner question 3.

## Removed, and why

| Removed | Why |
|---|---|
| `Required dlls/xImageDeviceManager.dll`, `SuniUSB.dll` | Open Dental's closed Suni capture driver (and Suni's USB driver it shipped). Design decision 2 (a). |
| Suni capture in ControlImagesOld, the Suni settings in Scanning Setup | Only that driver used them |
| `Required dlls/VBbridges.dll`, three HintPaths | Open Dental's closed VB helper; may not be redistributed |
| `Required dlls/Sparks3D/` (3 files), 4 references, `Sparks3DInterface.cs`, Sparks3D licence text and list entries | Open Dental's custom licence (no SaaS, no reverse engineering); may not be redistributed |
| DirectX 11 settings in the Graphics window | They chose between Sparks3D and SparksToothChart |
| `"Sparks3D"` special folder in ForkReleases and UpdateFileCopier | Nothing is published or copied from it any more |

## Owner questions

1. **Dr. Ceph, eClinicalWorks Rx, Patterson Imaging, Schick CDR 4:** do you need any of these bridges? If so, the vendor has to provide its interface, which we would implement from its documentation. Otherwise, should the four bridges be hidden or removed altogether, instead of showing "not available"?
2. **Business-layer tooth chart image** (API and mobile treatment plan and perio PDFs): is the "not available" image acceptable for v0.0, which ships neither of those programs? Or should the image be drawn some other way, for example a 2D renderer inside the business layer?
3. **Free text and ortho hardware on the tooth chart:** only Sparks3D drew them. Should the Chart module hide the Draw tab's text controls (Add, Move Text) and the ortho hardware drawing, or keep them as they are (saved but not drawn)?
4. **DirectX 9:** should the installer install Microsoft's DirectX End-User Runtime (`d3dx9_43.dll`, redistributable only through Microsoft's own installer), or should OpenGL be the default for new computers? The graphical perio chart needs DirectX.
5. **The DirectX 11 preference and computer setting:** delete them in a future fork migration, or keep them?

## Files outside this package's list

These are kept minimal:
- `OpenDentBusiness/Misc/ForkRelease/ForkReleases.cs` and `UpdateFileCopier/FormMain.cs`: one line each, as the task names. The rest belongs to REN-PROOF.
- `tests/ReleaseFlowTests/ReleaseChannelDbTests.cs`: its special-folder round trip used a `Sparks3D` folder; it now uses `OpenDentalReplicationService`, the remaining special folder (three lines).
- `docs/release-process.md`: one sentence about the special folders.
- `OpenDental/Forms/FormLicense.cs`, `FormRegistrationKey.cs`: one line each (the Sparks3D licence entry).
- `OpenDental.sln`, `OpenDental.Local.slnf`: the new test project.

## Tests

- New project `tests/BINATests` (23 tests):
  - `ProprietaryBinariesRemovedTests` checks that none of the removed binaries is:
    - present anywhere in the repository
    - named by a Reference, HintPath or file item in any project file
    - used as a type in any source file
    - referenced by the built libraries or by `OpenDental.exe` and `WpfControlsOD.dll`
    - in a Sparks3D release folder
  - It also checks that the Suni UI, the Sparks3D licence entries and the DirectX 11 settings are gone.
  - `ToothChartFallbackTests` checks:
    - the fallback order, using `StartFirstWorkingMode` with fake starters, so no graphics device is needed
    - that a missing runtime skips DirectX, and that each failure is logged
    - that a Simple 2D failure still surfaces
    - that the runtime check finds an installed `d3dx9_43.dll`
    - that FormGraphics and start-up follow the new rule
    - the size of the business layer's image
  - `BridgeNotAvailableTests` checks the four messages, that `DrCephUtils.Launch` throws, that each former call site shows its message, and that Schick version 5 is kept.
- Results on 2026-11-01:
  - The full build (`OpenDental.Local.slnf`, Debug|x86) passes with 0 errors and no new warnings.
  - `run-tests -Mode Core -Include ReleaseFlowTests` ran 80 assemblies and 5,356 tests: all passed, BINATests' 23 included.
  - `tools/Test-NoHqHosts.ps1` passed.

Not verified:
- No tooth chart, Graphics window, bridge or old Images module was run in the GUI (the rules forbid launching OpenDental.exe).
- This PC has `d3dx9_43.dll` installed, so the real "runtime missing" path was not exercised on a clean Windows. It is covered only by the fake-starter tests.
