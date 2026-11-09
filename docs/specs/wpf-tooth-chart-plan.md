# The WPF tooth chart: package plan

**What this is.** The package plan for the WPF 3D tooth chart (`docs/roadmap.md`, "The tooth chart's 3D drawing"). On 2026-11-08 the
owner said "pull ahead WPF work", so the chart runs alongside the .NET 10 stages of `docs/specs/net10-plan.md` instead of after them. It
is built on net48 and moves to net10.0-windows x64 with everything else at 3b. Package briefs are cut from this document the same way as
from the stage plan.

**Date.** 2026-11-08. **Status.** Decided; the owner answered O-17 to O-33 on 2026-11-08 (section 10). The numbers continue the open points of
`docs/specs/net10-plan.md` (section 12, O-1 to O-16).

**Basis.**
- Three read-only surveys of `main` at `c9463cad1`: the chart engines, the callers, and Helix Toolkit 3.x from public sources.
- An adversarial review of the first draft with 30 findings. Each finding was checked again in the repository at `bb446f65a`, where the
  chart files are unchanged since `c9463cad1`. The appendix says how each finding was resolved.
- A second review of the revised plan with 10 findings (3 medium, 7 low), checked the same way and resolved in the appendix's second
  table.
- Microsoft's public WPF documentation: the 3D graphics overview, `EmissiveMaterial` and `VisualTreeHelper.SetRootDpi`.
- Nothing was built, run, restored or downloaded. Line numbers are at `bb446f65a`. Project-file lines move in stage 1, so each brief
  greps them again.

**Scope (owner, 2026-11-07, unchanged).**
- A WPF 3D chart with Helix Toolkit 3.x replaces the DirectX 9 (SharpDX) and OpenGL (Tao) engines. The flat 2D chart stays as an option.
- It reuses the `.obj` models, the selection logic and the procedure-colour logic, and matches today's 3D look.
- It adds free text, orthodontic hardware, Open Dental 26.2's extraction graphic and paint type 18 `TextBelow`.
- It covers the Chart and Treatment Plan modules, the big chart window, the graphical perio chart and sheets/printing.
- A saved DirectX or OpenGL choice moves to the new chart, and it becomes everyone's chart (O-30 asks about saved 2D choices).
- Image tests compare rendered charts. The owner checks a gallery before the chart reaches main.

**Terms.**
- "The lane": the build lane script (`odbuild.ps1`, kept outside the repository).
- "The 3D layer" and "the 2D layer": the two parts of every chart image (section 5.1).
- "The renderer": the chart's own STA render thread (section 5.4).

---

## 1. The decisions in one page

1. **The WPF engine lives in `SparksToothChart`, not in a new project.**
   - Every caller creates `ToothChartWrapper` itself. There are seven sites: `ControlChart.cs:152`, `ControlTreat.cs:1610`,
     `SheetPrinting.cs:325`, `FormGraphics.cs:270`, `FormPerioGraphical.designer.cs:26`, `FormSheetDefEdit.cs:3837` and
     `FormToothChartBig.designer.cs:39`.
   - The wrapper is the engine switch (`ResetControls`, `ToothChartWrapper.cs:333-401`).
   - A project above `SparksToothChart` would need the model (`ToothChartData`, `ToothGraphic`), so the wrapper could not create it
     without a circular reference.
   - A project below it would mean moving the model and its 25 embedded models out of `SparksToothChart`. It would also add references
     to classic projects that stage 1 owns.
   - The old WPF stub is deleted: `ToothChartWpf.cs` and `WPF/*` are unused (grep finds no reference outside their own files), and
     `Program.cs` is commented out.
2. **`SparksToothChart` becomes SDK-style in TC-1, after S1-0 and S1-T, taken from S1-4's list.**
   - New files then need no project-file edits.
   - Packages arrive only through S1-0's restore path (`nuget.config`, locked restore, offline folder). The repository never gets a
     classic project with PackageReference.
   - It waits for S1-0, because there is no SDK-style project before it (net10-plan section 1.1 item 4).
   - It waits for S1-T, because two tests read this project file with checks that only know the old format:
     `tests/ReleaseFlowTests/ReleaseFilesTests.cs:37, 73-76` and `tests/LibraryBindingTests/TestSupport.cs:312`. S1-T makes them
     format-agnostic.
3. **No step before orthodontic hardware waits for a download. Helix is used as a geometry library, not as the viewport.**
   - The parity work uses plain WPF 3D. `SparksToothChart` already references PresentationCore, PresentationFramework, System.Xaml and
     WindowsFormsIntegration (`SparksToothChart.csproj:95-125`).
   - The chart keeps its own `.obj` parser, its own orthographic camera and projection, and the DirectX light constants.
   - Selection uses `ToothChartData`'s millimetre logic, not 3D hit tests.
   - Helix enters in TC-4, when stage 2 has central versions, lock files and package-aware release lists. The chart uses only its
     `MeshBuilder`, for the boxes and tubes of orthodontic hardware. It does not use Helix's viewport, camera, projection, hit testing,
     text or image export.
4. **The caller edits come first (TC-2, during stage 1).**
   - All chart code in `ControlChart.cs`, `ControlTreat.cs`, `FormOpenDental.cs`, `SheetPrinting.cs`, `FormPerio.cs` and the other
     callers moves into chart-owned code (`ToothChartRelay.cs`, `ToothChartWrapper.cs`), with today's behaviour.
   - This happens before 3a, where S3a-1 and S3a-M2 own `ControlChart.*`, `ControlTreat.*` and `FormOpenDental.*`.
   - Later chart packages then edit none of those files. The few other program files they edit (`FormGraphics.*` in TC-8; the
     procedure code windows and the Chart preferences page in TC-5) belong to no stage package.
5. **The engine choice needs no database change, and `DrawingMode` gets no new value.**
   - `DrawingMode` is stored, not in memory only: it is `computerpref.GraphicsSimple`, a `tinyint(1)` column
     (`ConvertDatabases1.cs:4362`; `ComputerPref.cs:18, 92-99`). A new value there would be a fork-only value in a stored enum.
     `docs/port-notes/FORK-ENUMS.md` would put it in the tinyint range below 127, and a later Open Dental port could add its own 3.
   - The stored values stay 0, 1 and 2. From TC-8, 0 (DirectX) and 2 (OpenGL) start the WPF chart, and 1 stays Simple 2D.
   - The wrapper gets its own enum, `ToothChartEngine` (DirectX, Simple2D, OpenGL, Wpf3D), in `SparksToothChart`. It is in memory only,
     never stored and outside the business layer, so FORK-ENUMS.md does not apply and `ForkEnumRangeTests` does not scan it.
6. **How it draws (section 5).**
   - A 3D layer holds only the meshes DirectX draws with depth testing on: the tooth groups, implants and perio teeth, lit as DirectX
     lights them.
   - A 2D layer holds everything DirectX draws with depth testing off, plus the D3DX text it draws over the teeth, in DirectX's order:
     the X, the RCT lines, the build-up/post, the sealant and watch marks, drawings, the centre line, the numbers and every perio
     decoration.
   - One STA render thread makes every image. No WPF object is created on a caller's thread, so the dashboard's MTA worker gets the WPF
     chart.
   - On screen the chart shows the renderer's image. O-19 asks whether a live WPF view is wanted instead.
7. **Packages merge to `main` dark, after their gallery is approved.**
   - Users reach the WPF chart only in TC-8, the engine switch.
   - TC-9a then deletes the old engines' code, and TC-9b their DLLs, licence texts and release entries.
   - S3a-15 follows one rule (section 6, O-26): it runs as written only if TC-8 has not merged when S3a-12 merges.
8. **The track has one writer at a time and counts toward the three-writer cap.** The cap is in the "Order" paragraphs of stages 0, 1
   and 3a in net10-plan.
   - S1-0 stays alone among the stage packages. TC-0's writer may work meanwhile in its own worktree, uses the lane only between
     S1-0's steps and merges right after S1-0 (section 2; net10-plan changes in section 13).
   - Its packages run Targeted tests only, NoHqTests included.
   - Core and Full runs are workflow steps, and every stage gate's Full run includes the chart's tests.

---

## 2. Sequencing against the stages

Each row gives the earliest window for a package. The track's own order (section 6) holds if a window is missed.

| Stage window | Stage packages (net10-plan) | Chart track | Why it fits |
|---|---|---|---|
| Now: stage 0 finished; S1-0 next, alone | S1-0 | **TC-0** is written; it merges after S1-0 | S1-0 stays alone among the stage packages. TC-0's writer works in its own worktree meanwhile and uses the lane only between S1-0's steps: not while step 2 switches the lane and makes its Full run and Release dry run, and not during step 4's baseline. The lead's recorder run (L-1) follows the same rule. TC-0 owns chart `.cs` files and a new classic test project, and edits no existing project file. S1-0 compares the unchanged solution with the Build Tools 2022 runs (S1-0 steps 2 and 4), so nothing merges while it runs. After TC-0's merge the lead re-runs `tools/Get-ProjectBaseline.ps1` for `SparksToothChart` and the new test project. |
| Stage 1, first wave | S1-T, S1-1, S1-2 | TC-0 merges first (merged as `c79ea6a38`) | If TC-0 is not ready when S1-0 merges, it keeps one of the three writer slots and S1-2 (the smallest) waits. EHR-0 (`docs/specs/ehr-rebuild-plan.md`) takes the slot TC-0 frees when it merges, so S1-T and S1-1 never wait for EHR; S1-2 takes the next free one (EHR plan, section 10.1). |
| Stage 1, second wave | S1-3, S1-4 | **TC-1** (after S1-0 and S1-T) | It needs S1-0's props and SDK pin and S1-T's format-agnostic parsers (section 1, item 2). It shares no file with S1-3 or S1-4, and S1-4's list drops `SparksToothChart`. Three writers. |
| Stage 1, later | S1-5, S1-6 | **TC-2** (after TC-1 and S1-T), then **TC-3** | No stage 1 package owns the caller `.cs` files, and TC-2 edits no stage-owned project file. Before S1-6, TC packages add their new test files to the classic `ToothChartImageTests.csproj`, which is the track's own. S1-6 converts `tests/ToothChartImageTests/*.csproj`; no TC package edits that file while S1-6 runs. |
| Stage 2 | S2-1 to S2-4 | TC-3 continues; **TC-4** after S2-4 | TC-4 needs `Directory.Packages.props` (S2-1), the feed with the nine dependencies the repository already ships (S2-1, batch A) and package-aware release lists (S2-4). |
| Stage 4 | S4-1, S4-2 | TC-4 merges before S4-1 starts, or after S4-2 has merged; then **TC-5** and **TC-7** | S4-1 owns `Directory.Packages.props`, the test lock files and every test `.cs` that fails to compile. Chart test edits wait for S4-1's merge. |
| 3a, waves 1 and 2 | S3a-1 to S3a-11, S3a-M1 to M3 | **TC-6**, **TC-8**, then the office check (O-26) | TC-2 moved the chart code of `ControlChart`, `ControlTreat`, `FormOpenDental`, `FormPerio` and `FormPerioGraphical` into relay helpers, so TC-8 edits only chart-owned files and `FormGraphics.*`, which no 3a package owns. |
| 3a, wave 3 | S3a-12 to S3a-16 | **TC-10** in S3a-15's place when TC-8 has merged; **TC-9a** after the office check; **TC-9b** after S3a-14 | The S3a-15 rule (section 6). TC-9b's files belong to S3a-13 and S3a-14 in wave 3 (net10-plan 3a "Known overlaps") and to S3a-8, S3a-10 and S3a-11 in wave 1. |
| 3a gate, bridge release | – | G4 approved; TC-8 and TC-9a merged; TC-9b if S3a-14 merged in time | If TC-8 misses the gate, the bridge release ships without the WPF chart (O-26). |
| After the bridge release, before `net10/switch` is cut | – | TC-9b if it missed the gate; TC-8 and TC-9a if they did | `main` is still net48. The branch is cut after them, so the x64 build never carries SharpDX or Tao. |
| 3b | S3b-0 to S3b-7 | none | S3b-3 retargets `SparksToothChart`. S3b-5 re-records the image baselines on net10 x64 (gallery G5). |

**Status on 2026-11-08.** S1-0 merged at `4871b4959`, and TC-0 after it, on `main` as `c79ea6a38`: 769 legacy pictures (DirectX, OpenGL and
Simple 2D) plus 10 smoothed gallery pictures in `Baselines/legacy-smoothed/`, about 88 MB together, in the unit of O-32 and section 7 (MiB;
92.5 MB in decimal megabytes). The 715 PNGs and about 81 MB of O-32 and section 7 are the first recording, before the 9 new scenes, which
added about 5.7 MB; the smoothed pictures add about 1.3 MB. The recorder and comparison commands (`LegacyRecorder.cs`) run with the
vstest.console.exe of Build Tools 2026, found with the vswhere query of net10-plan section 2.4. The lead re-recorded the project baseline
after the merge, in a clean worktree (`bffa11c7e`). The slot TC-0 freed went to TC-1, not EHR-0, while the owner's answers to E-1 to
E-19 are open (the lead, 2026-11-08). TC-1 writes its engine, gallery and first WPF baselines first, on today's classic
`SparksToothChart.csproj`, and makes its conversion commit (1) last, after S1-T merges.

**TC-1 (2026-11-08).** The owner approved gallery G0 ("Gallery G0 looks good"), shown as a page of 24 scenes beside TC-0's
DirectX pictures with the O-19 times; the full gallery stays on the build PC. TC-1 then merged hidden. Notes from its reviews for
the later packages:
- `ToothChartWrapper.DrawMode` reads DirectX while `Engine` is `Wpf3D`: `Wpf3D` has no stored form, so TC-2's `DrawingMode` source
  checks and TC-8's save rule build on that.
- `GetBitmap` always rebuilds the scene for `Wpf3D` (the 3D layer is reused when it is the same), because `Invalidate` on a wrapper
  without a handle never reaches `OnInvalidated`, so the dirty mark cannot be trusted on the hidden print, sheet and dashboard wrappers;
  a separate on-screen flag keeps the screen current (5.5).
- The watches are ordered by `Hashtable` enumeration, as DirectX orders them. That order is stable on .NET Framework but changes from
  process to process under .NET's randomised string hashing, so TC-10 and S3b-5 give the watches a stable order before they compare
  pictures across processes on .NET 10.
- `SparksToothChart.csproj` removes the default None items (`ChangeLog`, `*.cur`) so its item diff is exactly the accepted set; later
  conversions may do the same.
- The WPF facts `.txt` files gain two lines ("Fresh render", "Screen DPI seen by the test host") at the next deliberate re-record;
  the committed ones predate them, and nothing reads them.
- `ToothChartImageTests` takes about 150-190 s (the 253-scene WPF gate is 80-90 s of software rendering); accepted for Targeted and
  Full runs.

**TC-3 (2026-11-09, branch `chart/tc3-1`).** How the package meets its row and O-19 (b):
- **Live view.** `Wpf3D/ToothChartWpfView.cs`, an `ElementHost` whose WPF content is a `Viewport3D` with the scene's 3D layer and, above
  it, an element that draws the scene's 2D items with the renderer's own `ChartLayer2D`. The wrapper makes it only when it is shown, in
  `Wpf3D`, on an STA thread and not drawn in software; the image host stays, hidden, under it. Every image (`GetBitmap`, prints, sheets,
  the dashboard) is still the software render. A live view that cannot start leaves the image host on screen and logs once
  (`ToothChartFallbackKind.Wpf3DLiveView`). The frozen models come from one `ToothChartRenderer.BuildModel`, with meshes and materials
  cached per thread; a scene with the shown 3D layer redraws only the 2D layer.
- **The "Draw in software" switch** is `ToothChartWrapper.WpfDrawInSoftware`: on, a shown wrapper shows the image host (the software
  render, the same image as `GetBitmap`'s, which no driver can change: the remedy for dotnet/wpf#7076). It switches at once on a shown
  wrapper. The stored form is `ToothChartWrapper.DirectXFormatWpfSoftware` ("WpfSoftware") in `computerpref.DirectXFormat`, read with
  `IsWpfSoftwareFormat`; TC-8 stores it and adds the check box.
- **Mouse.** `ToothChartMouse.cs`, the DirectX handlers' logic with no drawing in it, drives the image host and the live view. The DirectX
  engine keeps its own handlers until TC-9a; a test drives both with the same input. Cursors come from the embedded `.cur` files through
  `ToothChartMouse.GetCursor` (the wrapper's own setter now loads them from its assembly, not its runtime type).
- **Parity probes** (`ParityProbeTests`, section 7) also sample the X's crossing, the build-up silhouette, the implant and a root canal
  line, and a second test checks the ink of every number, sealant "S" and watch "W" within one pixel of DirectX's.
- **Results on the build PC.** All 16,852 probes of the 253 recorded pictures agree with DirectX (mean lightness of WPF minus DirectX
  -0.7 points at the facial centres, -0.5 at the occlusal ones; largest 11), so the light factors of 5.3 stay as TC-1 set them. The
  known differences of 5.1 stay in the allowance list but cover no probe. Of 8,633 numbers and marks, all but 3 are within one pixel of
  DirectX's ink; the 3 are the tall sheet's (360x480) small numbers, whose horizontal scale of about 0.86 puts their stems between two
  pixels (allowed with that reason). The live view, read back in software, agrees with the renderer at all 1,090 probes of 8 scenes.
- **The one change of the drawing.** DirectX draws its overlays unsmoothed, and the 2D layer's smoothed centre line was two half-bright
  rows. `ToothSceneItem2D.CreatePolygons` now snaps an axis-aligned rectangle (the centre line, highlight boxes, a horizontal or vertical
  stroke) to the pixels DirectX fills (the top-left rule); slanted items keep their smoothing. The centre line now fills DirectX's rows
  exactly in all 121 checked pictures, and 46 of the 48 highlight boxes match, 2 by one column. 145 WPF baselines were re-recorded for
  it (every 410x307 picture, 19 at 500x370 and the big window); the other 108 are unchanged.
- **Times** (G1, fresh process, median of 21; a refill and the screen's update, the live view's being the UI thread's work):
  dashboard hidden, DirectX 47 ms and 32 ms at 410x307 and 615x461, the live view 18 and 21 ms, software 382 and 728 ms; a
  selection-only change, DirectX 32 and 48 ms, the live view 11 and 11 ms, software 16 and 30 ms. With the Patient Dashboard visible the
  refill also makes the dashboard's image in software: DirectX 96 and 143 ms, the live view 415 and 764 ms. The owner is asked in G1
  whether that cost needs a later package (the dashboard's image made asynchronously, or with less supersampling). These first live-view
  times were measured in a process that drew WPF in hardware and count the UI thread's work only; fix round 1 replaces them (below).
- **Fix round 1 (branch `chart/tc3-2`): the program draws every WPF window in software.** `OpenPmsDental/Main Modules/ProgramEntry.cs:21`
  sets `RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly` for the whole process, as the first statement of `Main`, before any window
  exists. It came from upstream (added in 24.2.48 and again in 24.3.15, `d4d86a572`, with no comment giving a reason) and is the
  program's only render-mode setting. A window's own `HwndTarget.RenderMode` cannot turn hardware back on: WPF draws in software when
  either setting asks for it. So in the program the live view's `ElementHost` is drawn by WPF's software rasterizer, at 1x and with no
  edge smoothing, not by the graphics card that O-19 (b) assumed, and the "Draw in software" switch moves between two software paths:
  the live view (1x, unsmoothed, drawn by WPF's render thread after the UI thread's work) and the image host (the renderer's 3x
  supersampled picture, the same as `GetBitmap`'s). The tests always drew in software (`TestAssemblySetup`, section 7), so they matched
  the program; the plan and the first G1 times did not. The fix round:
  - Owner question **O-34** (section 10) asks whether the line stays, with the live view as it actually runs.
  - G1's times process sets `SoftwareOnly` first, as the program does, and G1's table adds the live view's frame: the time until WPF has
    drawn and presented it (`MediaContext.CompleteRender`, on a window on the display that the user cannot see), and one full software
    frame at 1x (a `RenderTargetBitmap` of the view's content) as a proxy. A second process measures the live view with hardware
    rendering, for O-34's choice (a).
  - G1 shows the live view's 1x capture (`ToothChartWpfView.GetBitmapForTests`) next to the renderer's picture and DirectX's for six
    scenes, each with a 3x enlargement.
  - The live view keeps its 3D layer in a `BitmapCache` (1x, snapped to the pixels; the captures are identical with and without it) and
    draws the pen stroke in progress on an element of its own, so a selection or a pen move composites the cached 3D image instead of
    rasterizing the 3D layer again.
  - "Drawn by the graphics card" is corrected in `ToothChartWpfView.cs`, `ToothChartEngine.cs`, `ToothChartWrapper.cs`, the tests and this
    plan. A source test pins the line and that no other render-mode setting exists, so a change to it brings this plan and the tests along.

**Why not a separate branch.** The chart must not drift from the stages' rewrites of `ControlChart.cs` and the release files. Dark
merges keep it on `main`, and the gallery approval comes before each merge (O-27).

---

## 3. Shared files and their owner order

One writer per file at a time (net10-plan section 2.1). Before launching a TC package, the lead greps its patterns and intersects its
files with the packages currently running.

| File | Chart package(s) | Other owners (net10-plan; EHR plan where marked) | Order |
|---|---|---|---|
| `SparksToothChart/SparksToothChart.csproj` | TC-1 (conversion), TC-4 (Helix), TC-9a (SharpDX and Tao references) | S1-4 (gives it up), S3b-3 | TC-1 is its stage 1 owner (S1-4's list drops it), then TC-4, then TC-9a, then S3b-3 |
| `SparksToothChart/*.cs`, `SparksToothChart/Wpf3D/*` (new) | all TC | S3a-15 (`ToothChartWrapper.cs`, only if it runs and its x64 run finds a fault) | TC only; an S3a-15 edit merges before TC-9a |
| `WpfControlsOD/UI/ToothChart/ToothChartRelay.cs` | TC-0, TC-2, TC-5, TC-6, TC-7, TC-8, TC-9a | none | TC only. New relay code goes at the end of this file, so the classic `WpfControlsOD.csproj` (S1-4's) is never edited. |
| `OpenPmsDental/Main Modules/ControlChart.cs` | TC-2 | S3a-1 (830), S3a-M2 (whole file, with `menuToothChart` and `Tool_ToothChart_Click` 8436-8439) | TC-2 merges before 3a wave 1. Any later TC edit waits for S3a-M2's merge. TC never edits `ControlChart.designer.cs`. |
| `ControlTreat.cs` | TC-2 | S3a-M2 | same rule |
| `FormOpenDental.cs` | TC-2 (817-835, 1720, 1722-1729, 1753-1765) | S3a-1 (6840), S3a-M2 | before 3a wave 1 (S0-6 has merged) |
| `OpenPmsDental/SheetFramework/SheetPrinting.cs` | TC-2 | S3a-2 names only the commented-out line 479 | TC-2 before 3a wave 1; the lead confirms that S3a-2 does not edit it |
| `OpenPmsDental/UI/BordersDpi/LayoutManagerForms.cs` (1090-1129) | TC-1 | S3a-M3 if its DataGrid scan flags 1130 | TC-1 in stage 1, before 3a |
| `FormGraphics.cs` (254-293 in TC-2; rebuilt in TC-8), `FormGraphics.designer.cs`/`.resx`, `FormPerio.cs` (1114-1123), `FormPerioGraphical.cs` (37-41), `FormToothChartBig.*`, `FormSheetDefEdit.cs`, `FormComputers.cs`, `DashToothChart.cs` | TC-2, TC-8 | none named; S3a-M3 if one has a WinForms menu (grep at `bb446f65a` finds none; `FormPerio` uses `MenuItemOD`) | TC; the lead greps for WinForms menus at launch |
| `OpenPmsDentalBusiness/TableTypes/ComputerPref.cs` | TC-8 (comments only) | none | TC |
| `OpenPmsDentalBusiness/Logic/ToothChartExtractionGraphic.cs` (new), `OpenPmsDental/User Controls/Preferences/UserControlChartGeneral.cs`/`.Designer.cs` | TC-5 | none | TC. The business library is SDK-style since S1-3, so the new file needs no project edit. |
| `ProcedureCodes.cs` (87-97), `FormProcCodeEdit.cs` (367-375), `FormProcCodeNew.cs` (61-67) | TC-5 | S3a-M3 if they have menus | TC |
| `Directory.Packages.props` | TC-4 | S2-1 (creates it), S2-2, S4-1, S3b-1 | after S2-4; before S4-1 starts or after S4-2 |
| `packages.lock.json` of `SparksToothChart` and of every project that gets Helix through it | TC-4 | every package that changes dependencies (S2-x, S4-1, S3b-x) | TC-4 runs alone among them |
| `OpenPmsDental/app.config`, `tests/BindingRedirects.config` (only if TC-4's step 1 needs a redirect) | TC-4 | S2-2, S3b-4 (`app.config`), S3b-5 (deletes `BindingRedirects.config`) | after S2-4 |
| `tools/Build-Release.ps1` | TC-4 (adds Helix), TC-9b (removes 142-145; never-ship patterns) | S1-0, S1-1, S2-4, S3a-13, S3b-7 | TC-4 after S2-4; TC-9b after S3a-14, never while S3b-7 runs |
| `THIRD-PARTY-NOTICES.txt` | TC-4, TC-9b (153-163) | S2-4, S3a-13, S3b-7; EHR-3 (two entries, `docs/specs/ehr-rebuild-plan.md`) | EHR-3 before S2-4, or after TC-4 and before 3a wave 1 (EHR plan, section 10.2); TC-4 after S2-4; TC-9b after S3a-14, never while S3b-7 runs |
| `tests/AttributionTests/ReleaseIdentityTests.cs` (18-41: shipped components and files) | TC-4, TC-9b | S3a-8 (96, wave 1); EHR-3 (18-22, EHR plan) | EHR-3 before TC-4, or after TC-4 and before 3a wave 1; TC-4 in stage 2 or 4; TC-9b after wave 1 |
| `docs/third-party-binaries-audit.md` | TC-4, TC-9b | EHR-0 (133, EHR plan) | EHR-0 in stage 1, before TC-4; then TC-4, then TC-9b |
| `OpenPmsDental/Forms/FormLicense.cs` (152-153), `Properties/Resources.resx` (262-263, 280-281), `Properties/Resources.Designer.cs`, `Resources/SharpDX.txt`, `Resources/Tao_OpenGL.txt` | TC-4 (only for a Helix notice with its own text), TC-9b | none named | TC |
| `Required dlls/SharpDX*.dll`, `Tao*.dll` | TC-9b | kept through stage 2 (S2-4 names what is left) | after TC-9a |
| `OpenPmsDental/OpenPmsDental.csproj` (158-160, 189-195; the `Resources\SharpDX.txt` item, 9003; the `Resources\Tao_OpenGL.txt` item, 9010) | TC-9b | S1-5, S2-1, S2-3, S3a-13, S3a-14, S3b-4 | after S3a-14; before S3b-4 |
| `CodeBase/xCodeBase.csproj` (105-111; 124 if S1-1 keeps it) | TC-9b | S1-1, S2-3, S3a-13 (a linked `Compile` item), S3b-2 | after S3a-13 |
| `CodeBase/Controls/OpenGLWinFormsControl.cs` | TC-9a (TC-9b if `xCodeBase.csproj` still names it after S1-1) | S1-1 (stage 1) | after TC-8 |
| `UnitTests/UnitTests.csproj` (152-180) | TC-9b | S1-6, S4-1, S3b-5 | after S4-1 |
| `ForkReleases.cs` (`ListRetiredFiles`), `installer/OpenPmsDental.iss` (`[InstallDelete]`) | TC-9b | S3a-10, S3a-11, S3b-7 | after S3a-10 and S3a-11 |
| Chart pins in other test projects: `BTPTests/WiringTests.cs:17,66`, `BPRINTTests/WiringTests.cs:107`, `P262ClaimsTests/WindowSourceTests.cs:55`, `M2SHEETSTests/WiringTests.cs:22,81,121`, `SheetDefEditKeyTests.cs:46`, `BSHELLTests`, `BAPPTTests/UiSourceTests.cs:98`, `LibWebTests/WebView2PolicyTests.cs:275` | TC-2, only the pins its moves break | S1-T (its 27 files), S4-1 | after S1-T's merge; no TC test edits while S4-1 runs |
| `tests/BINATests/ToothChartFallbackTests.cs`, `ProprietaryBinariesRemovedTests.cs` (122-128), `tests/BPERIOTests/PerioUiSourceTests.cs` (134-170), `tests/P262SchemaTests/PaintTypeListTests.cs` | TC-1, TC-2, TC-5, TC-8, TC-9a | S1-T if it lists one of them; S3a-15 adds a file next to `ToothChartFallbackTests.cs` | after those merges |
| `tests/ReleaseFlowTests/*`, `tests/LibraryBindingTests/*`, `tests/BINBTests/*` | TC-4, TC-9b | S1-T, S2-4, S3a-7/8/10/11, S3b-6/7 | TC adds new files only: only `ReleaseIdentityTests` pins the five DLL names (grep at `bb446f65a`) |
| `tests/ToothChartImageTests/*` (new; references WpfControlsOD and the libraries below it, never the program) | all TC | S1-6 (converts the project file), S4-1 (MSTest 4), S3b-5 (retarget) | TC, except while those packages run |
| `tests/ToothChartHostTests/*` (new in TC-8; references the program) | TC-8 | S3b-5 (retarget) | TC; it stays x86 until S3b-5 |
| `OpenDental.sln`, `.slnf`, `.gitattributes` (new); this plan; `docs/specs/net10-plan.md`; `docs/roadmap.md` | lead | S3b-7 (roadmap) | lead |

---

## 4. Packages

| Package | Owns | Work | Targeted tests | Size |
|---|---|---|---|---|
| **TC-0 Baseline of today's chart** (its writer starts now; it merges after S1-0; the recorder run is the lead's; merged as `c79ea6a38`, section 2) | **Chart files:** `SparksToothChart/ToothChartData.cs` (6, 103-114, 1113-1122), `ToothGraphic.cs` (11, 19, 75-87 `Copy`, 151-175), `ToothGroup.cs` (5, 19-21, 40-80 with the fan triangulation 48-55, 82-92), `ToothChartDirectX.cs` (receives the moved members), `ToothChartWrapper.cs` (new `IsGraphicsDeviceException`), `ToothChartRelay.cs` (`IsPerioMode` only, 81-85). **New test project** `tests/ToothChartImageTests/`: a classic MSTest v1 project file with explicit items, like the other test projects until S1-6; `Scenes/ChartScenes.cs`, `Support/ChartImageCompare.cs`, `Support/StaRunner.cs` (the pattern of `tests/BPRINTTests/PrintOptionsTests.cs:544`), `LegacyRecorder.cs`, `ModelTests.cs`, `Baselines/legacy/**`. The lead adds the sln and slnf entries. | **Commits, in this order: record first, refactor after.** **(1) Scenes and recorder.** About 120 named, synthetic scenes (section 8). They set state only through the relay's public methods and need no database. Later packages add scenes but never remove one. The recorder is an opt-in test that runs only with `OPMS_RECORD_LEGACY_CHART=1`. It draws every scene in DirectX, OpenGL and Simple 2D through `GetBitmap` (`ToothChartWrapper.cs:783-807`) on a visible form, at 410×307 and at 500×370 (printout colours), plus the perio scenes in DirectX at 649×696. Each PNG gets a text file naming: the mode that actually started (the fallback can change it, `ToothChartWrapper.cs:432-458`), the device or pixel format, the adapter and driver, the Windows build, and the `PixelFormat` of the returned bitmap with whether every alpha is 255. **(2) The recording,** made by the lead (L-1, O-28) and committed as `Baselines/legacy/**`. The images are the gallery's "before" column and the target of the parity probes. They never gate a test pixel by pixel. **(3) Cut the SharpDX tie.** The vertex and index buffers, `facesDirectX`, `PrepareForDirectX`, `CleanupDirectX`, `DrawDirectX` and the `D3DX.OptimizeFaces` call move into `ToothChartDirectX`, as a buffer table keyed by tooth and group. `ToothGraphic.Copy` stops carrying the buffer. The fan triangulation (48-55) becomes a neutral `ToothGroup` method that DirectX and the WPF mesh cache both call. `GetDropletVertices` returns the model's own `Vertex3f`. Afterwards no model file has a `using SharpDX`. The lead then re-records DirectX and perio on the same PC, and every PNG must be byte-identical to commit (2)'s. **(4)** `ToothChartWrapper.IsGraphicsDeviceException(Exception)` returns true for `SharpDXException`, so the callers can stop naming SharpDX (TC-2). **(5)** The relay's `IsPerioMode` setter sets the value it is given; today it always sets true. **(6) Model tests.** Vertex, normal, face and group counts per model (`ImportObj`, `ToothGraphic.cs:457-684`; 53 IDs on 25 files). Teeth 9-24 mirrored in X (`:535-538, 563-585`). `GetToothAtPoint` at every tooth centre and in the primary band (`ToothChartData.cs:398-551`), with today's `ShiftM` truncation pinned rather than fixed (461, 525). `GetAffectedTeeth` across a drag (572-626). The perio geometry helpers against recorded values. | The new assembly (the recorder is skipped unless switched on), BINATests (ToothChartFallbackTests), BPERIOTests, NoHqTests; launch check L-1 | S, 2-3 dd |
| **TC-1 `SparksToothChart` SDK-style; the WPF engine's foundation** (after S1-0 and S1-T; stage 1, second wave; gallery G0 before merge) | `SparksToothChart.csproj` (its stage 1 owner instead of S1-4). **Deleted:** `ToothChartWpf.cs`/`.Designer.cs`/`.resx`, `WPF/Wpf3dControl.xaml`/`.xaml.cs`, `WPF/D3Helper.cs`, `D3Object.cs`, `D3ObjectGroup.cs` (unused: grep finds no reference outside them), `Program.cs` (commented out). **New:** `SparksToothChart/ToothChartEngine.cs`; the folder `SparksToothChart/Wpf3D/` (a new name, so it never meets the deleted `WPF/` folder on a case-insensitive file system) with `ToothScene.cs`, `ToothSceneBuilder.cs`, `ToothMeshCache.cs`, `ToothChartRenderer.cs`, `ChartLayer2D.cs` and `ToothChartImageHost.cs`. **Edited:** `ToothChartWrapper.cs` (the `Engine` property and the `Wpf3D` branches of section 5.5); `OpenPmsDental/UI/BordersDpi/LayoutManagerForms.cs` (1090-1129); `tests/BINATests/ToothChartFallbackTests.cs` (only the pins the wrapper edits break). **New** `tools/New-ToothChartGallery.ps1`, new test files and `Baselines/wpf-net48/`. | **Commits, in order.** **(1) Conversion,** as S1-4 describes for its projects, checked against the S1-0 baseline. The stub and `Program.cs` go first, as deliberate removals. The result is SDK-style net48 with no XAML page left, so no `UseWPF` is needed (S1-4's open `UseWPF` check is avoided). The explicit framework references stay. `System.Deployment` and `Microsoft.VisualC` go: no code uses them, and `System.Deployment` does not exist on .NET 10. `ResGenToolArchitecture` (38) and the x86 PlatformTargets (73, 82, 91) give way to the central props. The models, cursors and the 2D background keep their manifest names. **(2) LayoutManagerForms.** The three engine type checks (1115-1129) become one check for any child of a `ToothChartWrapper`, handled as today. The WPF engine then needs no entry, and TC-9a needs no edit there. **(3) The engine, on plain WPF 3D, with no package** (section 5). <br>• The mesh cache (5.2). <br>• The scene builder: a pure function from `ToothChartData` to an immutable scene. The scene holds the 3D items (mesh key, colour, visibility, transform, winding) and the 2D items in DirectX's order. It holds no WPF objects, so its tests need no thread. <br>• The renderer (5.4), with the camera, light and materials of 5.3 and the 2D layer of 5.1. <br>• The image host: a plain WinForms control that shows the renderer's image (5.4). It raises `SegmentDrawn` and `ToothSelectionsChanged` like the other engines and draws an in-progress pen stroke with GDI+ until mouse up. TC-3 brings the shared mouse class. <br>• The wrapper starts `Wpf3D` and falls back to Simple 2D with one log line. No caller can choose it yet. **(4)** The gallery script and gallery G0: the first WPF images next to the DirectX recordings, with the render times that O-19 needs (L-2). | <br>• The stage 1 baseline diff for commit (1): only the deliberate removals differ. <br>• Mesh tests (5.2), including the four perio row transforms and the maxillary implant flip; position counts equal the `VertexNormals` counts. <br>• Scene-builder tests, including: no 3D material colour has alpha below 255; the 2D items come in DirectX's order. <br>• A transform test: sample vertices go through the WPF chain and through the DirectX chain computed with SharpDX.Mathematics (referenced until TC-9a), and land within 0.001 mm of each other on screen. <br>• A layer test: a marker vertex lands on the same pixel in the 3D layer and in the 2D layer. <br>• Determinism: one scene rendered twice in one process and once in a second process gives identical bytes. <br>• Renderer tests (section 7): a wrapper made on an MTA thread returns a WPF image, `Engine` is still `Wpf3D` and nothing was logged; 500 renders in a loop without a failure; the process exits while the render thread is alive; every text item uses `pixelsPerDip` 1.0 and the root `DpiScale` is 1.0; on a shown wrapper, a refill followed by `GetBitmap` still updates the on-screen image (5.5); a host disposed while its render is in flight gets no call and nothing is thrown. <br>• The first WPF baselines. <br>• BINATests, BINBTests (`CompiledReferencesTests.cs:33` reads this project's output DLL), ReleaseFlowTests, LibraryBindingTests, BPERIOTests and BPRINTTests (S1-4's tests for this step), BSHELLTests (LayoutManagerForms), NoHqTests; launch check L-2. | M-L, 4-6 dd |
| **TC-2 Caller seams and one painter** (after TC-0 and TC-1; after S1-T has merged; before 3a wave 1) | **Relay:** at the end of `ToothChartRelay.cs`, a static `ToothChartPainter`, the interface `IToothChartPaintTarget` and the settings helpers (computer settings, the startup DirectX search, the settings snapshot, the graphical perio gate and mode). **Wrapper:** `ToothChartWrapper.cs` gets `GetPreferredDirectXFormat` and `TestDirectXFormat` from `FormGraphics.cs`, taking a host `Control`, and a `TextMoved` event that no engine raises yet. **Callers:** <br>• `ControlChart.cs`: 27, 28-30 (the dead `#if EHRTEST` block), 906-949 (with 931), 4439-4462, 4519-4531, 6001-6198, 6532-6702, and a new handler for a moved label. <br>• `ControlTreat.cs`: 1604-1679, 2158-2348 and its orthodontic copy. <br>• `SheetPrinting.cs`: 118-307, 310-406. <br>• `FormToothChartBig.cs`: 35-54, about 100-140, 147-332. <br>• `FormSheetDefEdit.cs`: 3837-3856. <br>• `FormPerioGraphical.cs`: 37-41. <br>• `FormPerio.cs`: 1114-1123. <br>• `FormGraphics.cs`: 254-293 (the two helpers leave; the call at 300 goes to the wrapper). <br>• `FormOpenDental.cs`: 817-835, 1720, 1722-1729, 1753-1765. **Tests:** `ToothChartFallbackTests.cs` (the pins at 130-141), `ProprietaryBinariesRemovedTests.cs` (122-128: the DirectX 11 check also reads the relay), the pins in section 3 that the moves break, new files in ToothChartImageTests (and, before S1-6, their items in its classic project file). | **Commit (1): move, with no change in behaviour.** <br>• The four paint switches move unchanged into `ToothChartPainter`, one method per caller: `ControlChart` 6001-6165, `ControlTreat` 2158-2348, `SheetPrinting` 118-279 and `FormToothChartBig` 147-304. So do the four orthodontic copies and the four ToothInitial loops (`ControlChart` 6635-6670, `ControlTreat` 1632-1670, `SheetPrinting` 348-386, `FormToothChartBig` about 100-140). They write to `IToothChartPaintTarget`, which the relay implements. <br>• The eight copies of the computer settings become one helper, `ToothChartRelay.ApplyComputerSettings(...)`, with the same values and the same `ComputerPrefs.Update` calls as today. <br>• The startup DirectX search (817-835) becomes a relay helper. It calls the wrapper's `GetPreferredDirectXFormat` and tells `FormOpenDental` whether to call `controlChart.InitializeOnStartup()`. The chart settings cache (1720, 1722-1729) and its `NeedsRedraw("ChartModule")` test (1753-1765) become a relay snapshot with `Take` and `HasChanged`. The task-list cache (1721, 1730-1737) stays where it is. <br>• `FormPerio`'s gate (1114-1123) becomes `ToothChartRelay.CanOpenGraphicalPerio(out string message)`, and `FormPerioGraphical`'s forced DirectX (37-41) becomes a relay helper. TC-7 and TC-8 then change only the relay. <br>• `catch(SharpDXException)` becomes `catch(Exception ex) when(ToothChartWrapper.IsGraphicsDeviceException(ex))`, and `using SharpDX;` (27) goes. So does the dead `#if EHRTEST` block with `using EHR;` (28-30): no build defines `EHRTEST` (`docs/specs/ehr-rebuild-plan.md`, H8). <br>• A developer switch, compiled only in DEBUG builds: `OPMS_TOOTHCHART_WPF=1` makes the settings helpers and the perio helpers choose `Wpf3D`. TC-8 removes it. <br>• The free-text move: the relay forwards `TextMoved(toothInitialNum, point in 410×307)`. `ControlChart` passes it to a relay helper that clamps the point to 0..410 and 0..307, rounds it to whole numbers and sets the label's new `"x,y;text"` (`ToothInitial.cs:66-83`); `ControlChart` then saves the label and refills. The helper is in the relay so that the round-trip test needs no program code. Whole numbers matter: `SetText` writes decimals in the workstation's format, and `GetTextPoint` accepts only dots and no minus sign, so a comma or a negative value would move the label to 0,0. Today no code moves a label (`docs/port-notes/BIN-A.md`). **Commit (2): one painter.** The four methods merge into one, with options for the differences found (O-25): `ControlTreat`'s own colour code without the TPi exception and the per-provider colours, which also skips status D and unhandled statuses (2179-2213, 2205-2207); the Mouth skip only in `ControlChart` (6010); the dashboard's provider colours (`SheetPrinting` 141); and the white paint of an unhandled status in `ControlChart` and `SheetPrinting`. Each caller's output stays as today. **Commit (3): the dashboard image only when a dashboard can show it.** The refill makes its dashboard image (6679-6686) only when `FormOpenDental.IsDashboardVisible` is true (`FormOpenDental.cs:6234-6238`). A dashboard that opens later builds its own image (`DashToothChart.RefreshData`, 56-73), and the event's only consumer is `DashToothChart.SetData` (29-40). | <br>• Call-log tests: fixture patients on a scratch database, covering every paint type and status, every kind of ToothInitial, orthodontic hardware and the date slider. A recording target logs each call. The logs after commit (1) must equal the logs after commit (2). <br>• Source tests: no program file outside the relay compares or switches on a `DrawingMode` value (`==`, `!=`, `case DrawingMode.`), except `FormGraphics.cs` until TC-8; no program `.cs` names SharpDX, Tao, `ToothChartDirectX`, `ToothChartOpenGL` or `OpenGLWinFormsControl`, except `FormGraphics.cs` until TC-8 and, for good, the licence texts (`FormLicense.cs`, `Properties/Resources.resx`, `Properties/Resources.Designer.cs`, `Resources/*.txt`); the eight settings sites call the one helper; `OPMS_TOOTHCHART_WPF` appears only inside `#if DEBUG`; the dashboard image is guarded. <br>• The moved-label round trip, under en-US and under de-DE. <br>• The affected suites: BSHELLTests, BTPTests, BPRINTTests, M2SHEETSTests, SheetTests, BPERIOTests, BINATests, ChartLetterTests; launch check L-3. | M, 3-4 dd |
| **TC-3 Drawing parity** (after TC-1 and TC-2; O-19 answered after gallery G0; gallery G1 before merge) | `SparksToothChart/Wpf3D/*`, `ToothChartWrapper.cs` (its WPF paths), new `SparksToothChart/ToothChartMouse.cs`, tests; only if O-19 chose the live view, a new `Wpf3D/ToothChartWpfView.cs` | **Everything DirectX draws in the normal chart:** <br>• Missing and hidden teeth; primary letters. <br>• Unerupted teeth (ShiftO < −10) at half RGB with alpha 255. DirectX halves the alpha too (879) but blends nothing (395). <br>• The six movements. <br>• Fillings per surface; crowns, pontics, veneers, build-ups and posts. <br>• Implants from `implant.obj`, flipped for maxillary teeth (`DrawImplant`, 811-832). <br>• RCT lines; the big X as 0.71 mm quads (779-788). <br>• The sealant "S" and the watch "W" with its white outline (863-872, 1162-1204), placed with the chart's own projection of DirectX's points; no Helix. <br>• Mobility; pen drawings in the 410×307 space; the white centre line (1079). <br>• Numbers in every nomenclature with the selection highlight. Their boxes use DirectX's measure rule (`MeasureStringMm`: width ×1.25, height ×0.8), and the engine sets `TcData.Font` as DirectX does (278). Background and text colours. **Look** (5.3): the specular rule of `GetGroupMaterial` (875-898): 0.1 for cementum, 0.4 for near-white colours (R>245, G>245, B>235), 1.0 for every other colour (419-422), power 70 (424). The D3DX fonts' height and average width (279-298) become a WPF font size and a horizontal scale, matched to the recordings within one pixel. **How:** <br>• Lines, the X and every decoration are 2D-layer polygons (5.1). WPF 3D has no line primitive, and Helix's screen-space lines update only on `CompositionTarget.Rendering`, so they would be missing from offscreen images. <br>• Text is 2D-layer text placed with the chart's own layout, never billboard text. <br>• **Mouse:** one shared class, taken from the DirectX engine (1236-1403), drives the pointer, pen, eraser (radius 8, offset 8.49) and colour changer (radius 2). It uses the same `GetToothAtPoint`, the same drag fill-in through `GetAffectedTeeth`, the same `SegmentDrawn` strings (`GetPointMouseScaled`, `ToothChartData.cs:1126-1133`), and raises `ToothSelectionsChanged` on mouse up. <br>• The 2D engine keeps its own code; it toggles only the tooth under the cursor, a difference that stays. <br>• No visible hover, as today. Cursors come from the embedded `.cur` files (`ToothChartWrapper.cs:183-205`). <br>• **Speed:** a refill changes materials, visibility and transforms only; meshes stay frozen and cached; a change that touches only the 2D layer (selection, drawings, text) reuses the last 3D layer image. <br>• **Live view, only if O-19 chose it:** an `ElementHost` subclass with a `Viewport3D` and the 2D layer as a canvas above it, created only when the wrapper is shown on an STA thread (5.4). | <br>• All parity scenes against the WPF baselines. <br>• Parity probes against the DirectX recordings (section 7). <br>• Mouse tests: synthesised input gives the selection, drag fill-in and `SegmentDrawn` strings that the shared `ToothChartData` functions give for the same points. <br>• Refill-plus-render time against DirectX on the build PC, with the dashboard visible and hidden, and the time of a selection-only change (reported in G1). <br>• BINATests; launch check L-4. | L, 5-8 dd (+1-2 dd for the live view) |
| **TC-4 Helix Toolkit 3.1.2** (after S2-4; before S4-1 starts or after S4-2 merges; batch H approved and in the offline folder, O-17; counsel answers of O-18 before merge) | `Directory.Packages.props` (Helix versions); `SparksToothChart.csproj` (`PackageReference HelixToolkit.Wpf`, and the pinning setting of step 2); the lock files in section 3; `tools/Build-Release.ps1` (the shipped Helix files in `ThirdPartyFiles`); `THIRD-PARTY-NOTICES.txt`; `tests/AttributionTests/ReleaseIdentityTests.cs` (the shipped components and files, 18-41); `docs/third-party-binaries-audit.md`; one new file each in `tests/ReleaseFlowTests` and `tests/LibraryBindingTests`. **Only if needed:** `OpenPmsDental/app.config` and `tests/BindingRedirects.config` (a redirect found in step 1); `FormLicense.cs`, `Properties/Resources.resx`, `Properties/Resources.Designer.cs` and a new `Resources/*.txt` (only if the Helix entry needs a vendor text of its own, under the licence-list rule of step 2). | **Step 1, offline check** in a scratch folder, as in S1-0 step 1. Restore and build a net48 copy of `SparksToothChart` with HelixToolkit.Wpf from the offline folder only. List: the resolved packages; the DLLs that land in `bin\Debug`; the assembly references of each Helix DLL; any `analyzers` or `build` asset that reaches the project (the CommunityToolkit.Mvvm and Logging.Abstractions generators must not); the System.Net.Http assembly version that HelixToolkit.dll references, and whether a binding redirect is needed. If a package not in batch H resolves, TC-4 stops for a batch H2 (O-17). **Step 2, add the packages.** <br>• Central versions in `Directory.Packages.props`. <br>• **Pinning, scoped to the chart.** Stage 2 does not turn on `CentralPackageTransitivePinningEnabled`, and turning it on centrally would pin transitive packages in every project. So the nine dependencies the repository already ships resolve to its own versions through a setting in `SparksToothChart.csproj` only: transitive pinning in that project, or direct references to the nine there. The choice is the one with which an unlocked restore of the whole slnf in the worktree gives no NU1603, NU1605, NU1608 or NU1107 in any project, and every changed lock file has the nine at their central versions. <br>• Every new DLL in the staged diff is named, listed and noticed: Helix's MIT text with the BSD notice of `LoopSubdivision.cs`, the MIT notices of the SharpDX- and SlimMath-derived code in HelixToolkit.Maths, and the MIT origin of `MeshSimplification.cs`; CommunityToolkit; System.ComponentModel.Annotations. `ReleaseIdentityTests` lists the new components and files. <br>• **Licence-list rule.** No licence entry is named "BSD" or "Microsoft", and no resource is named "Bsd" or "Microsoft" (`ReleaseIdentityTests.cs:42-44, 173-175`), so the BSD notice of `LoopSubdivision.cs` goes inside the Helix entry. Every vendor-text key in `FormLicense.cs` is a listed component (`ReleaseIdentityTests.cs:145-147`), and a vendor text replaces its component's standard licence text (`LicenseList.GetLicenseText`, `FormLicense.cs:184`): a Helix vendor text therefore carries Helix's MIT text and every notice of that entry. The removed-entry and removed-resource lists of `ReleaseIdentityTests` are not edited. <br>• **Risk W2:** System.Net.Http 4.3.4 carries a real net46 DLL. If step 1 shows it in `bin`, it is kept out with `ExcludeAssets="runtime"` on a central reference, or an equivalent that the step 1 results support. `BindingRedirectTests` and a test that loads the Helix types the chart uses prove it. <br>• The `SRTM.dll` reference inside HelixToolkit.Wpf.dll, which the package does not carry, is allowed in the new reference test: the chart never runs terrain code. **Rules for the chart's Helix use (written into later briefs):** <br>• Use `MeshBuilder` (namespace `HelixToolkit.Geometry`, float, `System.Numerics`) with `ToMesh().ToWndMeshGeometry3D(true)`. <br>• Never use `ConverterExtensions.ToWndMatrix3D`: row 3 reads "M31, M32, M32, M34" in 3.1.2 and on develop3. <br>• Never use `AddExtrudedGeometry` (#2459, fix unreleased). <br>• No `BillboardTextVisual3D`, `TextCreator` or screen-space visuals. <br>• 3.x API only: 2.x samples do not compile against 3.x. | <br>• ReleaseFlowTests (the Helix files ship; they are IL-only and not 32-bit-required; no `System.Net.Http.dll` is staged unless decided otherwise). <br>• LibraryBindingTests, AttributionTests. <br>• ToothChartImageTests unchanged: Helix changes no pixel. <br>• Release dry-run diff; NoHqTests. | S, 1-2 dd |
| **TC-5 Free text, text paint types, the extraction graphic** (after TC-3; O-20, O-21 and O-22 answered; gallery G2 before merge) | `Wpf3D/*`, `ToothChart2D.cs`, `ToothChartWrapper.cs`, `ToothChartRelay.cs` (`AddText` 122, `SetText` 234-239, new `SetTextBelow`, the painter's Text, TextBelow and completed-extraction cases), `ProcedureCodes.cs` (87-97), `FormProcCodeEdit.cs` (367-375), `FormProcCodeNew.cs` (61-67), new `OpenPmsDentalBusiness/Logic/ToothChartExtractionGraphic.cs`, `UserControlChartGeneral.cs`/`.Designer.cs` (the setting's check box), `tests/P262SchemaTests/PaintTypeListTests.cs` (15-42), new test files | **Free text.** `AddText` draws a 2D-layer label at the scaled 410×307 point. With `CursorTool.MoveText` (`ToothChartWrapper.cs:895-896`), a drag moves the label and raises `TextMoved` on mouse up; TC-2 saves it. **Paint type Text (15).** "W" stays the watch mark. Any other text is drawn on the tooth (O-22); today it is dropped (relay 234-239). **TextBelow (18)** is drawn as O-21 decides. `GetPaintTypesForList` lists it, and the PaintText box shows for both Text and TextBelow. `FormProcCodeNew`'s list-index cast stays valid because TextBelow is last; a test pins this. **Extraction graphic (O-20).** The setting is the fork's own preference row `ForkToothChartShowCompletedExtractions` in the new business class, read and written by name (`PrefC.GetRawOrDefault`, `PrefC.UpsertRaw`), as `RecallProphyPerioSwitch` does (`OpenPmsDentalBusiness/Logic/RecallProphyPerioSwitch.cs:33-60`). It is not in `PrefName`, and a missing row reads as off, so there is no `Pref.cs`, `PrefInfos.xml`, convert-script or migration change. A check box on the Chart preferences page saves it with an audit line and a preference signal. The completed-extraction skip, in one place since TC-2, draws the graphic when the setting is on. The slider logic (`ControlChart.cs:6603-6634`) is unchanged, because the procedure filter already drops extractions after the slider date (6578-6601); the call-log tests check this. **2D chart.** Free text, Text and TextBelow are drawn with `DrawString`, and the extraction graphic with the existing X lines (`ToothChart2D.cs:93-112`). Orthodontic hardware stays 3D-only (O-24). | <br>• Scenes and baselines, WPF and 2D. <br>• The free-text add, move and save round trip. <br>• `PaintTypeListTests` now expects TextBelow in the list. <br>• Call-log tests for TextBelow and for the extraction graphic with the setting on and off. <br>• A database test: a missing row reads as off, the first save inserts the row, the name is not in `PrefName` and starts with "Fork" (as `tests/P262MiscTests/RecallSwitchDbTests.cs:70-83`). <br>• P262SchemaTests, RegistryTests, NoHqTests; launch check L-5. | M, 3-4 dd |
| **TC-6 Orthodontic hardware, retained roots, space maintainers** (after TC-4 and TC-5; O-23 and O-24 answered; gallery G2) | `Wpf3D/*`, the relay (106-114, 156, 184, 215, 227), the painter's orthodontic part, tests | <br>• Brackets: `MeshBuilder.AddBox` on each tooth's facial surface, in the 3D layer, placed from the tooth's layout (`GetDefaultOrthographicXpos` and its movements). <br>• Wires between brackets and within a bracket: `AddTube` through the bracket centres along the arch. <br>• Elastics: thin tubes between two brackets. <br>• Colours come from the hardware rows (`DrawOrthoHardware`, now in the painter). The chart uses no WPF hit testing, so the hardware needs no hit-test setting. <br>• Ortho mode per O-24; today it only switches numbering to Palmer (`ControlChart.cs:6544-6549`). <br>• Retained root (the crown groups hidden, the roots in the colour) and space maintainer (a band on the tooth and a loop across the gap; tooth, range or quadrant), both per O-23. | <br>• Scenes and baselines. <br>• Scene-builder tests: brackets and wires follow tooth movements and mirrored teeth, and their windings follow the rule of 5.2. <br>• Call-log tests; launch check L-5. | M, 3-4 dd |
| **TC-7 Graphical perio on WPF** (after TC-3; may run before TC-5 and TC-6; gallery G3) | `Wpf3D/*` (the perio scene), `ToothChartWrapper.cs` (`PerioMode` allowed for `Wpf3D`, 216-227), the relay's perio helpers (the DEBUG switch), tests | The perio scene as DirectX draws it (`DrawScenePerio`, `DrawPerioRow`, `ToothChartDirectX.cs:469-700`; legend 915-944; numbers 1096-1109): <br>• The four rows (layout 469-507), lit with the perio light (ambient 0.6, diffuse 0.4, 403-407). The two lingual rows use the reversed windings (5.2). <br>• In the 2D layer, in DirectX's order for each row: the millimetre lines, mobility, furcation triangles, probing bars with the red limits, bleeding and suppuration droplets (flipped in Y for the mandible, 619), and the GM, CAL and MGJ lines; then the numbers, the centre line and the legend. <br>• The nine perio colours (`SetPerioColors`). <br>`FormPerioGraphical` keeps forcing DirectX through the relay helper until TC-8. The DEBUG switch opens it on `Wpf3D` for L-6. | <br>• Perio scenes at 649×696, plus the print render and the 750×1000 save (`FormPerioGraphical.cs:231-298, 315-339`), against WPF baselines. They are rendered through the wrapper at those methods' sizes and settings; the window itself is a TC-8 host test. <br>• Parity probes against the DirectX recordings. <br>• BPERIOTests, P262PerioTests; launch check L-6. | M-L, 4-6 dd |
| **TC-8 The engine choice moves to the WPF chart; every host** (after TC-3 and TC-5 to TC-7; O-19, O-30 and O-31 answered; gallery G4 before merge) | <br>• **Relay helpers:** stored 0 and 2 start `Wpf3D`, 1 stays Simple 2D (O-30); the software switch if O-19 chose the live view; the startup DirectX search becomes a no-op and the snapshot drops the DirectX and OpenGL fields; the graphical perio gate and mode per O-31; the developer switch goes; no `ComputerPrefs.Update` when an image is made. <br>• `ComputerPref.cs`: comments only. <br>• `FormGraphics.cs`/`.designer.cs`/`.resx`: rebuilt. <br>• `ToothChartWrapper.cs`. <br>• Tests: `ToothChartFallbackTests.cs`, `ProprietaryBinariesRemovedTests.cs` (122-128), `PerioUiSourceTests.cs` (134-170, only the pins its edits break). <br>• New test project `tests/ToothChartHostTests/` for the host tests: SDK-style on `tests/Directory.Build.props`, on MSTest 4 like every test project after S4-1, with one project reference, to the program (the libraries come through it). The lead adds the sln and slnf entries. It stays x86 until S3b-5, so no x64 build of the program is needed before 3b. <br>• Docs: `docs/owner-questions.md` (item 7 at :23 superseded; BIN-A 3 at :255 answered), `docs/release-notes/next.md`. | **Setup > Graphics:** <br>• Two choices: "3D chart" and "Simple 2D chart". <br>• Saving "3D chart" writes 0 only when the stored value was 1. A stored 0 or 2 is kept, so a rollback brings back the old engine. <br>• With the live view (O-19): a "Draw in software" check box and a live preview, so a PC whose driver draws no WPF 3D (dotnet/wpf#7076) shows it at once. <br>• No DirectX format grid and no OpenGL pixel grid. The remote edit from `FormComputers.cs:44-51` saves without testing, as today. **Wrapper:** a DirectX or OpenGL request starts `Wpf3D`, which falls back to Simple 2D with one log line. **Each host checked end to end:** <br>• The Chart module: refill, selection, the Draw tab, Save to Images, and the dashboard event image (6675-6701). <br>• The big chart window (926×858). <br>• The Treatment Plan printouts at 500×370 with printout colours 14 and 15, both the classic document and the sheet. <br>• The referral letter and the automation sheet. <br>• The Patient Dashboard. Its image is made on an MTA `ODThread` worker (`UserControlDashboard.cs:133-152`, `DashToothChart.cs:56-73`). Since TC-1 the wrapper creates no WPF object there, and the image comes from the renderer. Today an exception there is swallowed (150), so the test checks the engine, not only the absence of an exception. <br>• The sheet editor's preview (`FormSheetDefEdit.cs:3833-3867`). <br>• The graphical perio chart: display, print and save, and on a 2D computer per O-31. | <br>• Mapping tests: 0 and 2 give `Wpf3D`, 1 gives Simple 2D; the save rule above; `Wpf3D` has no stored form; with the live view, the software value reads and writes. <br>• Fallback tests rebuilt. <br>• Source tests: nothing selects DirectX or OpenGL; `FormGraphics` names no engine type; the perio gate has no `d3dx9` check. <br>• Host tests (in `tests/ToothChartHostTests`) on a scratch database with synthetic patients: each host's image is made in `Wpf3D`, has the expected size, is opaque, has the pixel format TC-0 recorded for DirectX, is not blank and matches a baseline. The dashboard path runs on an MTA thread; afterwards `Engine` is still `Wpf3D` and nothing was logged. <br>• BINATests, BPERIOTests, M2SHEETSTests, SheetTests, BTPTests, BPRINTTests, ChartLetterTests; launch check L-7. | M, 3-5 dd |
| **TC-9a The old engines' code retired** (after TC-8 and the office check, O-26) | **Deleted:** `ToothChartDirectX.*`, `ToothChartOpenGL.*`; `CodeBase/Controls/OpenGLWinFormsControl.cs` when `xCodeBase.csproj` names no item for it after S1-1 (otherwise TC-9b deletes it with that item). **Wrapper and relay:** the DirectX and OpenGL branches, `D3DX_DLL`, `IsDirectXRuntimeAvailable`, `CanLoadLibrary`, `UseHardware`, `PreferredPixelFormatNumber`, `DeviceFormat`, the two DirectX format helpers from TC-2, and their relay forwards. **Project file:** `SparksToothChart.csproj` (the SharpDX and Tao references). **Tests:** `ToothChartFallbackTests.cs`; S3a-15's test file, if S3a-15 ran; a new "retired" file; the TC-1 transform test (now against recorded numbers) and the SharpDX.Mathematics reference of the ToothChartImageTests project. | <br>• Delete. <br>• `DrawingMode` keeps its three stored values. The dead columns stay unread, as `GraphicsUseDirectX11` does (`ComputerPref.cs:78`). <br>• The program's own SharpDX and Tao references (`OpenPmsDental.csproj`, `xCodeBase.csproj`, `UnitTests.csproj`) stay until TC-9b: no code uses them after TC-2 and TC-8, and the DLLs are still in `Required dlls`. <br>• `UnitTests/UI Testing/FormToothChartTests.cs` needs no edit: it stores `DrawingMode` values, which stay. | <br>• ToothChartImageTests: every baseline unchanged. <br>• BINATests, BINBTests, NoHqTests; launch check L-8. | S, 1-2 dd |
| **TC-9b SharpDX, Tao and the DirectX runtime requirement leave the release** (after TC-9a and S3a-14; before the 3a gate if it fits, otherwise on `main` before `net10/switch` is cut) | **Required dlls:** the five SharpDX and Tao DLLs. **Project files:** `OpenPmsDental.csproj` (158-160, 189-195; the `Resources\SharpDX.txt` item, 9003; the `Resources\Tao_OpenGL.txt` item, 9010), `xCodeBase.csproj` (105-111; 124 if S1-1 kept it), `UnitTests.csproj` (152-180). **Licence texts:** `FormLicense.cs` (152-153), `Properties/Resources.resx` (262-263, 280-281), `Properties/Resources.Designer.cs` (the `SharpDX` and `Tao_OpenGL` properties), `Resources/SharpDX.txt` and `Resources/Tao_OpenGL.txt` (deleted). **Release files:** `tools/Build-Release.ps1`, `THIRD-PARTY-NOTICES.txt` (153-163), `ListRetiredFiles` and `[InstallDelete]`, `docs/third-party-binaries-audit.md`, `docs/release-notes/next.md`. **Tests:** `ReleaseIdentityTests.cs` (20, 33-34), new "retired" files in ReleaseFlowTests and BINBTests. | <br>• In `Build-Release.ps1`, the five names leave `ThirdPartyFiles` (142-145) and join the never-ship patterns. The DirectX patterns (230-231) stay. <br>• The notices lose the SharpDX and Tao components. The SharpDX MIT notice stays inside TC-4's Helix entry, whose HelixToolkit.Maths code derives from SharpDX. <br>• The licence window's `SharpDX` and `Tao Framework` vendor texts go with their components: every vendor-text key must be a listed component (`ReleaseIdentityTests.cs:145-147`). Re-keying the `SharpDX` text to the Helix entry is not an option, because a vendor text replaces that entry's own licence text (`FormLicense.cs:184`). So `FormLicense.cs:152-153`, both resources (`Resources.resx` 262-263 and 280-281, their `Resources.Designer.cs` properties, the two `.txt` files and their project items) go. The removed-entry and removed-resource lists of `ReleaseIdentityTests` are not edited. <br>• The setup changes only `[InstallDelete]`. It has never installed the DirectX runtime: a grep of `installer/` finds no DirectX, d3dx, SharpDX or Tao, and see `docs/owner-questions.md:23`. <br>• Release notes: the DirectX End-User Runtime is no longer needed. | <br>• ReleaseFlowTests (the staged list; the retired list equals `[InstallDelete]`), LibraryBindingTests, BINBTests, LibraryUpdateTests, AttributionTests, PdfLibraryTests (`LicenceTests` reads the vendor texts), BINATests (`LicenceLists_DoNotNameSparks3D` reads the same files). <br>• Release dry run; launch check L-8. | S, 1-2 dd |
| **TC-10 The WPF chart on x64** (3a wave 3, after S3a-12 and TC-8; in S3a-15's place under the rule of section 6) | One new test file; the x64 build and run of ToothChartImageTests (the lead's lane, as for S3a-12); S3a-15's text in net10-plan (lead) | <br>• The x64 test build covers ToothChartImageTests, `SparksToothChart`, WpfControlsOD and the libraries below them. S3a-12 already builds WpfControlsOD for x64 (its ImagingCaptureTests references it). The program is never built for x64 on net48: the host tests in ToothChartHostTests stay x86 until S3b-5. <br>• Image, mesh, scene and renderer tests run on the 64-bit .NET Framework 4.8 against the same baselines and tolerances. A difference beyond tolerance gets its own `Baselines/wpf-net48-x64/`, with the reason. <br>• A static test: the x64 test build's `SparksToothChart.dll` loads in a 64-bit process (PE32+ for AMD64, or IL-only without the 32-bit flag), and the Helix DLLs are IL-only and not 32-bit-required. The normal build stays 32-bit-required (`PlatformTarget=x86`, net10-plan section 2.3). <br>• The largest image (the big chart at 3×) rendered in both bitnesses, as a memory check. <br>**At 3b:** <br>• S3b-3 retargets `SparksToothChart`: `UseWPF` and `UseWindowsForms`; the same Helix packages through their net8.0-windows assets; the System.* 4.3.x packages pruned. The byte[] model resources and the 2D background bitmap take 3b's preserialized-resource path (S3b-0 item 3). <br>• S3b-5 re-records `Baselines/wpf-net10-x64/`, because .NET uses another render DLL (wpfgfx_cor3, not wpfgfx_v0400). <br>• L-2 is repeated on net10 x64, because `SetHighDpiMode` replaces the app.config DPI setting there (S3b-4). <br>• The owner sees gallery G5 before the 3b merge. | ToothChartImageTests in x86 and x64; BINATests | S, 1 dd (+ re-recording in S3b-5) |

---

## 5. How the WPF chart draws

### 5.1 Two layers, chosen from DirectX's depth states

**What the DirectX engine does** (`ToothChartDirectX.cs`):
- Depth testing and writing are on for the scene (392, compare `Less` at 393). Culling is off (391). Blending is off (395).
- Depth is turned off for: the X, the RCT lines and the build-up/post after each tooth's facial view (778, back on at 808), though the
  implant turns it on again first (812); the watches (1163); the drawings (1207); the numbers (1122), with the centre line drawn while
  it is off; each perio row's decorations after its teeth (522); the perio numbers (1097) and legend (916).
- The sealant "S" is D3DX screen-space text (`PrintString`, 1152-1160), drawn in the occlusal view (867, 870) after 808 has turned
  depth testing back on. It lands in front of the teeth, so it belongs in the 2D layer too.
- Everything drawn with depth off is flat colour. It is either D3DX text, or a material with only an emissive colour (800, 960, 975). Such
  a material has zero ambient, diffuse and specular parts, so the lit result is exactly the emissive colour.
- Its screen position does not depend on depth, because the projection is orthographic (`OrthoLH`, 388).
- The order (`DrawScene`, 436-467): for each tooth, the facial view, its X, RCT lines and build-up, its implant, then the occlusal view
  and its "S"; after all teeth, the watches, the drawings, the centre line and the numbers.

**What WPF does:**
- A `Viewport3D` is composed as a two-dimensional element like any other, and 2D and 3D content cannot interpenetrate inside it (WPF 3D
  graphics overview).
- WPF 3D has no setting that turns depth testing or depth writing off for a model: `System.Windows.Media.Media3D` has no such property.
  Back faces are culled unless `BackMaterial` is set (3D overview).
- `EmissiveMaterial` "adds its color to any existing material" (its API page), and a `MaterialGroup`'s children are applied in
  "multiple rendering passes" (3D overview). An emissive-only build-up drawn over enamel would add to the enamel colour, not replace it.

**Choice: two layers in one image.**
- **3D layer:** the tooth groups, the implants and the perio teeth, depth-tested and lit (5.3). Nothing else.
- **2D layer:** every item that DirectX draws with depth off, and the sealant "S", as 2D polygons and text, in DirectX's order, drawn
  over the 3D layer.
  Points are projected with the chart's own orthographic mapping, the same function that sets the camera, so the layers align (TC-1).
- The build-up/post group is a 3D mesh. Its 2D item is the union of its projected triangles: every triangle is given the same winding,
  and all are filled as one geometry with the Nonzero rule. That is what DirectX shows: a flat silhouette, with no depth and no culling.

**Why not the other two ways:**
- A second `Viewport3D` for the overlays would cost a second 3D pass. It would still sort the overlays by depth instead of keeping
  DirectX's order, it would need its own ambient-only light for flat colour, and it would still cull mirrored flat items.
- Depth-offset geometry in the same viewport (moving overlays toward the camera, which an orthographic camera allows) would need a black
  diffuse pass under an emissive pass for flat colour, and depth steps to keep the order.
- The 2D layer gives flat colour, no depth and DirectX's order directly, and 2D polygons have no faces to cull.

**Known difference.** DirectX's overlays write no depth, so a later depth-tested draw can cover them: a neighbouring tooth that overlaps
after a movement, or the same tooth's implant, drawn after its X (812). In the WPF chart the 2D layer is always on top. G1 shows both
cases.

### 5.2 Mirrored geometry

WPF draws only front faces: triangles that are counter-clockwise as seen from the camera. Today's drawing has four kinds of mirror:
- Teeth 9-24 are mirrored in X when they are loaded. Their normals are mirrored, but their winding is not reversed
  (`ToothGraphic.cs:535-538, 563-585`; the comment at 663-664).
- DirectX flips Z for its left-handed space (`Scaling(1,1,-1)`, 443-450 and 478).
- The two lingual perio rows add another Z flip (495, 498), which shows the teeth from behind. Their implants go with them.
- Mandibular droplets are flipped in Y (619).

**The rule.**
- The mesh cache stores each tooth group's triangles with their winding made to agree with the vertex normals, which reverses teeth
  9-24. It also stores a second index list with every triangle reversed.
- The scene builder builds each instance's model transform as DirectX does: user movement and rotation, view rotation, tooth position,
  perio row. It leaves out DirectX's handedness flip, which WPF's right-handed camera replaces.
- When the transform's determinant is negative, the instance uses the reversed list.
- In the normal chart no instance is negative: there are only rotations and translations, and the maxillary implant flip is a rotation
  (815). In the perio chart the two lingual rows are negative, their implants included.
- Droplets, triangles, bars and lines are 2D-layer items, so the Y flip needs nothing.
- `BackMaterial` is not used: it doubles the cost and would light the inside of the teeth.

**The test.** For every transform the chart uses (both views in all four quadrants with sample movements, the four perio rows and the
maxillary implant flip), the triangles WPF keeps are exactly those whose transformed normals face the camera. Edge-on triangles may go
either way.

### 5.3 Camera, light, colours and transparency

- **Camera.** An `OrthographicCamera` 130 mm wide (`ToothChartData.cs:26`), widened as DirectX widens it for wide controls
  (381-388). It sits 400 mm in front of the teeth, the distance of DirectX's shift (443-450), so the specular highlights get the same
  view direction.
- **Light in DirectX.** One directional light with ambient 0.4, diffuse 0.6 and specular 0.8 (400-417). The perio chart uses ambient 0.6
  and diffuse 0.4 (403-407). The direction (0.5, −0.2, 1) is in DirectX's left-handed space; in WPF's right-handed space it is
  (0.5, −0.2, −1).
- **Light in WPF.** A WPF light has one colour for all terms. So the chart uses one directional light of 0.8 (DirectX's specular part)
  and no ambient light. Each material is a group of three:
  - a diffuse brush of the colour × 0.75 (0.6 / 0.8; perio × 0.5);
  - a specular brush of the material's specular value (the colour rule of `GetGroupMaterial`, TC-3), power 70;
  - an emissive brush of the colour × 0.4 (perio × 0.6), which is DirectX's ambient term: ambient light times material colour is a
    constant.
  - Each term then equals DirectX's. WPF's specular formula is not documented, so the parity probes check the result, and TC-3 tunes
    these factors against the recordings if they differ.
  - A group of three costs three passes per model; TC-1's times (O-19) include them.
- **Colours.** Every 3D material colour and every 2D polygon colour gets alpha 255, because DirectX draws them with blending off (395).
  Text keeps its colour's alpha, because D3DX text is blended.
- **Transparency.** WPF does not sort transparent models by depth, so a transparent model shows only what was drawn before it and hides
  what comes after. With alpha 255 everywhere the 3D layer has no transparent material, and the problem never arises. A scene-builder
  test checks every 3D colour of every scene.
- **Background.** Opaque, in the chart's background colour.

### 5.4 Threads, the renderer and the image path

- **One render thread per process.** It is STA, has `IsBackground=true` and its own Dispatcher, and ends with
  `Dispatcher.InvokeShutdown` (never `Thread.Abort`).
- It builds and freezes the meshes once (frozen objects can be used from any thread), keeps the 3D layer's visual tree, and makes every
  image: the on-screen image, `GetBitmap`, prints, sheets and tests.
- **Scenes.** The scene is built on the calling thread from `ToothChartData` and cannot change afterwards. The render thread never reads
  `ToothChartData` and never waits for another thread, so a caller waiting for it cannot deadlock.
- An on-screen result reaches the UI thread only through a non-blocking `BeginInvoke` on the image host. It is skipped when the host is
  disposed or has no handle, and the race where the host goes away during the call is caught and ignored.
- **Exceptions.** Each render job catches its own exceptions and hands them to the failure path below. As a last guard, the render
  thread's `Dispatcher.UnhandledException` handler sets `Handled=true` and logs once, because an unhandled exception on any thread ends
  a .NET Framework process.
- **No WPF object on a caller's thread.** The dashboard makes its wrapper and a hidden form on an MTA `ODThread` worker
  (`UserControlDashboard.cs:133-152`, `DashToothChart.cs:72`, `SheetPrinting.cs:325-339`). An `ElementHost` or another WPF control
  built there would throw, the fallback would silently give the 2D chart, and it would use up the one log line of `LogFallbackOnce`
  (`ToothChartWrapper.cs:492-503`). Because the wrapper builds only the scene there, that worker gets the WPF chart.
- **One image.** The 3D layer is rendered into a `RenderTargetBitmap` at 3×, capped so it stays at or below 8 megapixels, and reduced
  with WIC's Fant scaler. It is drawn with the 2D layer into a second bitmap at the exact pixel size. The pixels are copied out with
  `CopyPixels` into a `System.Drawing.Bitmap` with the pixel format TC-0 recorded for DirectX, every alpha 255.
- **Reuse.** One `RenderTargetBitmap` per pixel size is reused with `Clear()`, and at most three sizes are kept (the least recently used
  is freed). So a test loop never creates bitmaps fast enough to hit dotnet/wpf#3067. A change that touches only the 2D layer reuses
  the last 3D layer image of that wrapper.
- That image is frozen, held by the wrapper (not by the render thread) and released on `Dispose`. So the wrappers that prints and
  sheets make for one image and then throw away (`SheetPrinting.cs:325, 402-404`) leave nothing behind on the render thread.
- **Failure.** A failed render is retried once. Then the Simple 2D image of the same data is returned and logged once. A print never
  comes out without a chart.
- **On screen (recommended in O-19).** The engine control is a plain WinForms control that shows the renderer's image. A burst of changes
  posts one render (5.5). On-screen renders run asynchronously and the newest scene wins, so slider ticks never queue up. The pen stroke
  in progress is drawn with GDI+ over the image until mouse up, as DirectX draws it without redrawing the teeth (`Render(isUsingPen)`,
  378-379). The on-screen image is the same software render as every print, so no graphics driver can change it.
- **Live view (O-19, the alternative).** The `ElementHost` is created only when the wrapper is shown on an STA thread. A wrapper that is
  only used for an image never creates one.
- **The live view in the program is drawn in software (O-34).** `ProgramEntry.cs:21` sets `RenderOptions.ProcessRenderMode` to
  `SoftwareOnly` for the whole process (from upstream), and a window's `HwndTarget.RenderMode` cannot override it. WPF's render thread
  therefore rasterizes the live view in software at 1x, with no edge smoothing, after the UI thread's work, and each frame redraws
  whatever region changed, the 3D layer under it included. TC-3's fix round keeps the 3D layer in a `BitmapCache` and draws the pen
  stroke on an element of its own, so a selection or a pen move composites the cached 3D image; a refill rasterizes it once. The
  dotnet/wpf#7076 driver fault cannot reach a software-drawn view. Only if the owner removes or narrows that line (O-34 (a)) is the live
  view drawn by the graphics card, smoothed by multisampling where the driver allows.

### 5.5 Redraws

- Every wrapper `Set*` method ends in `Invalidate()`, and the relay's `BeginUpdate`/`EndUpdate` is only `SuspendLayout`/`ResumeLayout`
  (relay 126-137). A refill makes hundreds of such calls.
- For `Wpf3D`, `OnInvalidated` (`ToothChartWrapper.cs:319`) only marks the scene dirty. On a wrapper that is shown, the first mark of a
  burst posts one rebuild to the UI thread's message queue, and later marks post nothing. A wrapper that is not shown (the hidden forms
  of prints, sheets and the dashboard) posts nothing. `GetBitmap` rebuilds at once (always, as built in TC-1: see section 2's TC-1
  notes).
- The posted rebuild always renders the on-screen image from the current scene, even when a `GetBitmap` in between has already rebuilt
  the scene and cleared its dirty mark. Only the posted rebuild clears the burst's "posted" mark. So a refill that ends in the
  dashboard's `GetBitmap` (`ControlChart.cs:6673-6681`) still updates the screen.
- The rebuild is posted rather than tied to the wrapper's next paint: the engine control covers the whole wrapper, which may then get no
  paint of its own.
- The `Wpf3D` branches of the wrapper: `TcData` (66-84), `OnInvalidated` (319), `OnResize` (842), the `DrawMode` setter's dispose step
  (51-56), `ResetControls` (333-401), the fallback (403-525) and `GetBitmap` (783-807).

### 5.6 DPI and determinism

- The program turns WPF DPI awareness off (`OpenPmsDental/Properties/AssemblyInfo.cs:33`) and runs WinForms PerMonitorV2
  (`OpenPmsDental/app.config:116`). The test host does not turn WPF DPI awareness off, and offscreen WPF text follows the system DPI.
- So the renderer calls `VisualTreeHelper.SetRootDpi(root, new DpiScale(1,1))` on every root it renders (.NET Framework 4.6.2 and later;
  the root must have no parent) and formats every text with `pixelsPerDip` 1.0. Pixel sizes are explicit, at 96 dpi.
- A test checks the root DPI and every text item. A test cannot change the system DPI, so the lead repeats the determinism test once
  with the desktop at 150% (L-2). The bytes must equal those of the 100% run.
- At 3b, `SetHighDpiMode` replaces the app.config setting (S3b-4), so L-2 is repeated on net10 x64 (TC-10).
- **Render mode.** The program sets `RenderOptions.ProcessRenderMode=SoftwareOnly` first in `Main` (`ProgramEntry.cs:21`), so every WPF
  window in it, the live view included, is drawn in software and does not depend on the graphics card. The test assembly sets the same
  (section 7), so a live-view test sees what the program draws, and G1's times process sets it too. `RenderTargetBitmap` renders in
  software either way, so the renderer's images (`GetBitmap`, prints, sheets, baselines) do not depend on it. Removing or narrowing that
  line (O-34 (a)) changes the live view's look (hardware 3D is smoothed by multisampling where the driver allows, and differs by driver)
  and every other WPF window of the program, so it needs a launch check of its own.

---

## 6. Order and merge order

**Track order:** TC-0, TC-1, TC-2, TC-3, TC-4, TC-5, TC-7, TC-6, TC-8, the office check, TC-10, TC-9a, TC-9b.
- TC-7 depends only on TC-3, and the lead may move it earlier.
- TC-6 needs both TC-4 and TC-5.
- TC-10 needs TC-8 and S3a-12. TC-9a needs TC-8 and the office check. TC-9b needs TC-9a and S3a-14.

**The S3a-15 rule, used everywhere in this plan and in net10-plan:**
- S3a-15 runs as written only if TC-8 has not merged when S3a-12 merges, or if TC-8 is reverted later. Otherwise TC-10 takes its place.
- Reason: after TC-8 no user can reach the DirectX or OpenGL engine, so testing them on x64 would test nothing in use.
- If S3a-15 ran and TC-9a merges later, TC-9a removes S3a-15's test file with the engines.

**Rules:**
- Each package merges only after its gallery answer, where it has a gallery (section 8).
- After every TC merge, the lane runs an incremental full slnf build and the package's Targeted tests.
- During stage 1, the lead re-runs `tools/Get-ProjectBaseline.ps1` for the projects a TC merge touched, as for any fix that lands then.
- TC-1's conversion commit is checked against the baseline the way every stage 1 conversion is.

**Known overlaps, resolved:**
- `SparksToothChart.csproj`: TC-1 owns it in stage 1, and S1-4's list drops it.
- The relay is chart-only.
- The callers' chart code moves in TC-2, before 3a. The menu objects in those files (`menuToothChart`, the designer's `MenuItem`s,
  `Tool_ToothChart_Click`) stay S3a-M2's. TC never edits them.
- TC-2's test-pin edits in S1-T's files wait for S1-T's merge.
- `LayoutManagerForms.cs` is edited once, by TC-1, so that deleting the engines needs no edit there.
- Release lists, notices, licence texts, retired files and the setup follow the order in section 3.

---

## 7. Tests

**What is tested:**
1. **Scene-builder unit tests.** No WPF objects and no thread. For each paint type and each state: which groups get which colour,
   visibility, transform and winding; the 2D items and their order. Unerupted teeth are at half RGB with alpha 255. No 3D colour has
   alpha below 255.
2. **Mesh tests** (TC-1, 5.2).
3. **Image tests.** All images go through the product's own renderer. Under MSTest v1 they use the existing STA-thread pattern. After
   S4-1 they may use `[STATestMethod]` (MSTest 3.6 and later).
4. **Parity probes** against the DirectX recordings.
5. **Call-log tests** of the painter, on a scratch database.
6. **Host tests:** source checks plus one runtime image per host. The runtime checks live in `tests/ToothChartHostTests` (TC-8), the
   only chart test project that references the program; `tests/ToothChartImageTests` never does.
7. **Mouse tests.**
8. **Renderer tests:** an MTA caller, a render loop, process exit with the render thread alive, the DPI settings of 5.6, the
   on-screen refresh after a `GetBitmap` (5.5) and a host disposed during a render (5.4).

**Rendering rules, for determinism:**
- Explicit pixel size; 96 dpi; `Pbgra32`; opaque background; `SetRootDpi` 1.0 and `pixelsPerDip` 1.0 (5.6).
- The 3D layer is drawn at 3× (capped at 8 megapixels) and reduced with WIC's Fant scaler.
- 2D-layer text uses `TextFormattingMode.Display` and grayscale antialiasing, never ClearType. ClearType depends on per-user settings.
- `RenderTargetBitmap` always renders in software (Microsoft's graphics rendering tiers page), so images do not depend on the GPU. Tests
  that host the live view (O-19 (b) only) set `RenderOptions.ProcessRenderMode=SoftwareOnly` in `AssemblyInitialize`, as the program does
  (`ProgramEntry.cs:21`, O-34), so they draw the live view as the program draws it.
- No test waits for `CompositionTarget.Rendering` or for a frame on screen. WPF stops drawing on screen while the session is locked, and
  Full runs are unattended. The tests run in the lead's desktop session, as the existing WPF STA tests do.
- One render thread renders scenes one after another and reuses one bitmap per size (5.4). A `COMException` (dotnet/wpf#3067) is retried
  once, and a second one fails the test.

**Tolerances:**
- Within one build and one machine, TC-1's determinism test must show identical bytes before any tolerance is trusted.
- Against a baseline: a pixel counts as different when a channel differs by more than 8 of 255.
- A test passes when at most 0.1% of the pixels differ and no 16×16 block has more than 24 different pixels. The block rule catches a
  small local error, such as a missing "S".
- A failing test writes the expected, actual and diff images (the different pixels in red over a grey copy).

**Parity probes:**
- The colour is sampled at each tooth's facial and occlusal surface centre, computed from the layout tables, in the WPF image and in the
  DirectX recording.
- For coloured surfaces it passes when the hue is within 12° and the lightness within 20%. For missing and hidden teeth, both images
  must agree on tooth versus background.
- Reviewed exceptions go in an allowance list with a reason. The cases of 5.1's known difference are listed there.
- If DirectX could not be recorded (O-28), the probes run against OpenGL with looser lightness limits. The gallery then judges the look.

**Baseline changes:**
- Baselines change only in a commit that explains why.
- The gallery shows old against new for each changed scene. The owner's approval is needed when the change is visible.
- Expect re-baselining after a Windows font update, for x64 (TC-10) and for .NET 10 (S3b-5).
- TC-0's first recording of today's chart alone was 715 PNGs, about 81 MB (as merged: 769 plus 10 smoothed, section 2), against this
  section's first estimate of about 600 PNGs (20-30 MB) for the whole track; the owner keeps all of it (O-32). The WPF baselines add to it.
  The lead adds a `.gitattributes` with `*.png binary` before the first baseline commit (TC-0 did).
- PNG file bytes also depend on the framework's deflate code, which changes with .NET 10 (zlib-ng). Comparisons across the switch
  (S3b-5, TC-10) compare the pixel SHA-256 that each picture's `.txt` holds, not the file bytes.

**Host tests:**
- **Source checks** (TC-2, then TC-8): no program file outside the relay compares or switches on a `DrawingMode` value; no program `.cs`
  names SharpDX, Tao or the engine types, except `FormGraphics.cs` until TC-8 and the licence texts for good; the eight settings sites
  call the one helper; the developer switch is DEBUG-only.
- **Runtime checks** (`tests/ToothChartHostTests`, x86 until S3b-5). One image per host on a scratch database with synthetic
  patients: the sheet through `GetToothChartHelper`, the TP printout, the dashboard on an MTA thread, the sheet editor preview and the
  perio print. Each is checked for size, opacity, pixel format, not blank, and baseline.

---

## 8. The owner gallery

**What it shows.** Every scene is synthetic: no patient data and no database. Each scene shows, side by side, DirectX, OpenGL and 2D (as
recorded in TC-0), the WPF image, and a diff against the last approved WPF image. Each row describes the scene's data in words.
- **Empty charts:** an empty adult chart in Universal, Palmer, FDI and Haderup numbering; primary dentition; mixed dentition (erupted
  and unerupted permanent teeth, half colour).
- **Tooth states:** missing, hidden and each of the six movements, on teeth in all four quadrants (mirrored teeth included).
- **Paint types:** every type from 1 to 18, in every status colour (TP, C, EC, EO, R, Cn; dark and light).
  - Fillings on every surface combination; crowns, bridges (pontic on missing teeth) and dentures (upper and lower).
  - Sealant, veneer, implant, RCT, post/build-up and the X.
  - The completed-extraction graphic, with the setting off and on.
  - Text with "W" and with other text; TextBelow; retained root; space maintainer.
- **Known difference (5.1):** an X and a build-up next to a moved, overlapping tooth; an X on a tooth with an implant.
- **Colours:** colour-by-provider off and on; the procedure code `GraphicColor` override; the TPi exception.
- **Interaction:** selection of one tooth, several teeth, a dragged range and a tooth in the primary band; pen drawings; erased segments.
- **Free text:** long labels, labels over teeth, labels at the edges, and a moved label.
- **Orthodontics:** brackets, wires, wire in bracket, elastics; ortho mode.
- **Perio:** a full exam at 649×696, the print and the 750×1000 save.
- **Sizes and colour sets:** 410×307 (module and dashboard), 500×370 (printouts and sheets), 926×858 (big window) and a custom sheet
  aspect; module colours (10-13) and printout colours (14 and 15).
- **G4 only:** host captures from launch check L-7, at 100% and 150%, made only on the scratch database with synthetic patients.

**How it is made.** `tools/New-ToothChartGallery.ps1` runs the image tests with `OPMS_CHART_GALLERY=<folder>`. It writes every image and
a static `index.html` (one row per scene, filterable by group), and zips the folder.

**How it reaches the owner.** The lead publishes the index and images as a private page and sends the link, or sends the zip. The
owner's answer ("approved", or a list of scenes to change) is recorded in `docs/owner-questions.md` with the commit and the date. The
package merges only after that answer.

**Galleries:**
- G0: TC-1, the first look, with the render times for O-19.
- G1: TC-3, parity.
- G2: TC-5 and TC-6, the new drawing.
- G3: TC-7, perio.
- G4: TC-8, final, with the hosts.
- G5: 3b, net48 against net10 x64.

---

## 9. Downloads (batch H; nothing is fetched by this plan)

Batch H comes from nuget.org into the offline folder and needs the owner's approval (O-17). It holds 20 new packages for net48:

| Package | Version | Why |
|---|---|---|
| HelixToolkit.Wpf | 3.1.2 (2025-11-25; lib/net48, net6.0-windows7.0, net8.0-windows7.0; 980,836 B) | the chart's helpers |
| HelixToolkit | 3.1.2 (164,113 B) | dependency |
| HelixToolkit.Geometry | 3.1.2 (295,291 B) | `MeshBuilder` |
| HelixToolkit.Maths | 3.1.2 (549,874 B) | dependency |
| CommunityToolkit.Common, CommunityToolkit.Diagnostics, CommunityToolkit.Mvvm | 8.3.2 | dependencies (MIT; the Mvvm source generator is excluded by Helix's `exclude="Build,Analyzers"`) |
| System.ComponentModel.Annotations | 5.0.0 | CommunityToolkit.Mvvm on net48 |
| System.Net.Http | 4.3.4 | HelixToolkit (see TC-4, risk W2) |
| System.Private.Uri | 4.3.2 | HelixToolkit |
| System.Text.RegularExpressions | 4.3.1 | HelixToolkit |
| System.IO.Compression | 4.3.0 | HelixToolkit.Wpf |
| System.Security.Cryptography.X509Certificates, Algorithms, Encoding, Primitives | 4.3.0 | System.Net.Http |
| System.IO, System.Runtime | 4.3.0 | dependencies |
| Microsoft.NETCore.Platforms | 1.1.1 | dependency |
| Microsoft.NETCore.Targets | 1.1.3 | dependency |

**Already in the feed when TC-4 runs** (S2-1, batch A): Microsoft.Bcl.AsyncInterfaces,
Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions (10.0.x),
System.Diagnostics.DiagnosticSource, System.Buffers, System.Memory, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe
and System.Threading.Tasks.Extensions. The sizes of the non-Helix packages are read from the NuGet catalog when the owner approves.

**A package that TC-4's step 1 resolves and this list lacks** stops TC-4 for a batch H2, asked the same way.

**At 3b, nothing new.** The same nupkgs serve net10.0-windows through their net8.0-windows assets. Pruning drops the System.* 4.3.x
packages. The S3b-0 spike confirms this from the lock file.

---

## 10. Owner questions (the numbers continue net10-plan section 12)

**Answered 2026-11-08.** The owner: "1. As recommended 2. Ship dlls we need. Use Helix. It's decided. Rest as recommended." So every
question below is decided as recommended, with one change to O-18: Helix Toolkit is used, and nothing waits for counsel. The setup ships
only the DLLs the staged list needs, each with its notice in `THIRD-PARTY-NOTICES.txt`; the fallback to the chart's own geometry code
is not needed. For O-28: the build PC has the DirectX runtime (`d3dx9_43.dll` in `System32` and `SysWOW64`), so today's DirectX pictures
are recorded here. For O-19 the choice between (a) and (b) follows TC-1's measured times, so it is **(b)**: on the build PC
(i7-10700T, x86 .NET Framework 4.8, software rendering) a warm refill takes 384 ms at 410x307 and 648 ms at 615x461 (217 ms even at
1x without smoothing), against (a)'s limit of 100 ms; a selection-only change takes 13-16 ms, within its 20 ms. TC-3 therefore adds a
live hardware WPF view on screen with a per-computer "Draw in software" switch, and the software render stays for `GetBitmap`, prints,
sheets and the dashboard (TC-1's gallery G0, `timings.txt`). TC-3's fix round found that the program draws every WPF window in software
(`ProgramEntry.cs:21`), so the live view is not drawn by the graphics card in the program: O-34 asks again with the live view as it runs.

- **O-17. Batch H download.** The exact list is in section 9. **Recommended:** approve it. TC-0 to TC-3 do not need it. If TC-4's
  offline check resolves a package that the list lacks, TC-4 stops and the lead asks for a batch H2 the same way.
- **O-18. Counsel items for batch H.**
  - The twelve 4.3.x-era packages use the "Microsoft .NET Library" licence and set `requireLicenseAcceptance`.
  - `LoopSubdivision.cs`, compiled into HelixToolkit.Wpf.dll, carries a BSD notice that redistribution must include.
  - `CanonicalSplineHelper.cs` carries its author's 2009 copyright line and no licence statement. It is in the shipped DLL.
  - HelixToolkit.Maths contains MIT code derived from SharpDX and SlimMath.
  - **Recommended:** ship only the DLLs the staged list needs, add a notice for each, and have counsel answer before TC-4 merges.
  - **If counsel rejects an item,** the owner chooses: the chart's own box and tube code instead of Helix (TC-6 is its only user, and
    batch H is then not needed), or a source build of HelixToolkit.Wpf without the rejected file, through the repository's source-build
    pattern (`third-party/build`).
- **O-19. How the on-screen chart is drawn, and whether a software switch is needed at all.**
  - **(a) Recommended, if TC-1's times allow it:** the on-screen chart shows the renderer's image (5.4).
    - It is the same software render as every print and saved image, so the gallery shows exactly what users see.
    - There is no hardware path, so a driver that draws no WPF 3D (dotnet/wpf#7076, still open) cannot affect it, and no switch is
      needed. Setup > Graphics offers only "3D chart" and "Simple 2D chart".
    - No `ElementHost`, so none of its DPI and keyboard-focus questions arise.
    - The limit the lead proposes: on the build PC, a refill plus render of the Chart module's chart at 150% (615×461) takes at most
      100 ms, and a selection-only change at most 20 ms (L-2, shown in G0).
  - **(b) Otherwise:** a live WPF view on screen (TC-3 adds it), drawn by the graphics card, with a per-computer "Draw in software"
    check box. (The program draws every WPF window in software, `ProgramEntry.cs:21`, so in the program the view is drawn in software
    at 1x: O-34.)
    - Stored as a text value in the existing `ComputerPref.DirectXFormat` column (`ComputerPref.cs:44-47`): empty means hardware,
      `WpfSoftware` means software. No schema change. The DirectX engine that read the column is gone after TC-9a.
    - An older build that reads `WpfSoftware` finds no `;` and keeps its default device format (`ToothChartDirectX.cs:1436-1440`). That
      format's `IsHardware=false` asks for a Reference device, which fails on an ordinary PC, and the old chart then falls back to
      OpenGL and then 2D (`ToothChartWrapper.cs:432-458`).
    - Not recommended: reusing `GraphicsUseHardware`, because new computers get false there and would start in software; or a new
      column, which is a schema change away from Open Dental 26.2.
- **O-20. The 26.2 extraction graphic: name, default and look.**
  - Open Dental's preference name is not known. The repository's 26.2 documents list only `TextBelow=18`
    (`.git/od-tools/od-docs/26-2/OpenDentalDiffDocumentation26-2.xml:227`), and preference rows are not in the schema documents.
    `tests/RegistryTests/PrefNameTests.cs` registers in `PrefName` only names that Open Dental's published documentation states.
  - **Recommended:** the fork's own row `ForkToothChartShowCompletedExtractions`, outside `PrefName`, read and written by name like the
    other fork-owned rows (`docs/port-notes/P262-MISC.md:64-68`). One setting for the whole office, on Setup > Preferences > Chart.
  - When an Open Dental 26.2 database is adopted one day, the office's setting does not carry over unless the name is confirmed, as for
    the other unconfirmed names (`docs/adoption-runbook.md`, step 0.11). If the name becomes known, a fork migration step renames the
    row, as `OdSettingNamesStep` did for B-ERA (`OdSettingNamesStep.cs:85`).
  - Default off, which is today's look.
  - When it is on, a completed extraction (C, EC or EO) draws the big X in that status's colour over the tooth position, while the tooth
    stays hidden as today.
- **O-21. Where TextBelow is drawn.**
  - **Recommended:** inside the image, in a band along the bottom edge under the mandibular teeth and along the top edge over the
    maxillary teeth. It is centred on the tooth, in the paint colour, and cut to fit. This keeps the image's 410×307 aspect, on which
    every sheet field depends (`FormSheetFieldSpecial.cs:58-62, 82-91`).
  - Alternative: a strip added under the chart, which changes the image size everywhere.
- **O-22. Paint type Text with text other than "W".** Today nothing is drawn (relay 234-239). **Recommended:** draw the text on the
  tooth, as Open Dental did. "W" stays the watch mark.
- **O-23. Retained root and space maintainer** (types 16 and 17). They are no-ops today (relay 215, 227). **Recommended:** draw them in
  TC-6. They are cheap with the same geometry tools, and the gallery shows them.
- **O-24. Ortho mode and the 2D chart.** **Recommended:**
  - Keep today's layout with Palmer numbering (`ControlChart.cs:6544-6549`), and draw the hardware on the facial views.
  - The 2D chart draws free text, Text, TextBelow and the extraction graphic, but no orthodontic hardware.
- **O-25. Differences between the four painting copies.**
  - `ControlTreat`'s printout colours lack the TPi exception and the per-provider colours (2179-2213).
  - `ControlTreat` skips procedures with status D or an unhandled status (2205-2207). `ControlChart` and `SheetPrinting` ignore the
    false result of `GetToothColors` and paint such procedures white.
  - Only the Chart module skips Mouth procedures (6010).
  - **Recommended:** keep each caller's output as today, as options of the one painter, and align them later only on request.
- **O-26. When SharpDX and Tao go, and when S3a-15 runs.** **Recommended:**
  - TC-8 (the switch) merges in 3a waves 1 and 2. Right after it, the lead makes the office check (section 11).
  - If the check passes, TC-9a merges. TC-9b merges after S3a-14: before the 3a gate if it fits, otherwise on `main` right after the
    bridge release and before `net10/switch` is cut. The .NET 10 x64 build then never carries SharpDX or Tao, and S3b-3 gets simpler.
    The fallbacks are software rendering and the 2D chart, which both work on any PC.
  - S3a-15 follows the rule of section 6: it runs as written only if TC-8 has not merged when S3a-12 merges, or if TC-8 is reverted.
  - If the office check fails, TC-8 is fixed or reverted, TC-9a waits, and S3a-15 runs.
  - If TC-8 misses the 3a gate, the bridge release ships without the WPF chart. TC-8, TC-9a and TC-9b then land on `main` before
    `net10/switch` is cut, and the office meets the WPF chart with the x64 release. The alternative is to hold the bridge release for
    TC-8.
- **O-27. Dark merges to `main` with a gallery approval per package, or an integration branch.** **Recommended:** dark merges. Users
  reach the chart only through TC-8, and a branch would collide with 3a's rewrite of `ControlChart`.
- **O-28. Where the legacy DirectX pictures are recorded.** DirectX needs `d3dx9_43.dll` (`ToothChartWrapper.cs:406, 421-426`).
  **Recommended:**
  - Use the build PC if it has the runtime.
  - Otherwise use an office workstation that runs the recorder from a test build (no database). TC-0's re-recording after the SharpDX
    cut must run on the same PC, so the lead takes both test builds there in one visit.
  - Do not download the DirectX runtime for the build PC.
- **O-29. The business layer's chart image** (BIN-A 2, `owner-questions.md:254`; `ToothChartHelper.cs:195-212`). **Recommended:** keep
  the placeholder. The paths are unshipped and their entry methods have no callers in the repository. The renderer sits above the
  business library, and moving it below is a later package if those paths ship.
- **O-30. Computers saved as Simple 2D.** Such a computer is often set by Setup > Computers, "Use Simple Graphics"
  (`FormComputers.cs:53-62`, `ComputerPrefs.cs:205-211`), after a DirectX crash. The scope says the new chart "becomes everyone's chart".
  - **Recommended:** keep them on 2D, with no data change, so a rollback is safe. The release notes say how to switch, and the owner can
    switch a computer from Setup > Computers.
  - Alternative: move every stored 1 to the 3D chart once. That is a data change that a rollback to an older build would meet as
    DirectX, on the PCs where DirectX crashed.
- **O-31. Graphical perio on a 2D computer after TC-8.** Today `FormPerio.cs:1114-1123` refuses unless DirectX is chosen and installed.
  - **Recommended:** open it on the WPF chart on every computer. The 2D choice then applies to the tooth chart only. The 2D engine cannot
    draw perio, and the WPF chart renders in software on any PC.
  - Alternative: keep refusing on 2D computers, with a message that names Setup > Graphics.

**Answered by this track:** BIN-A 3 (`owner-questions.md:255`). Free text and orthodontic hardware are drawn, so their controls stay.

**Asked with TC-0's results and answered on 2026-11-08** (the owner chose the recommended answer of each):

- **O-32. The size of the legacy recording.** 715 PNGs, about 81 MB (the repository was 550 MB). Once SharpDX and Tao are removed,
  today's DirectX and OpenGL pictures cannot be recorded again, and the Simple 2D chart that stays is guarded by its own pictures.
  **Answer:** keep all of it.
- **O-33. DirectX edge smoothing.** On the build PC every four-sample draw gave slightly different edge pixels, so the byte-checked
  DirectX pictures are drawn without multisampling; offices on the default format see smoother edges. **Answer:** keep the
  unsmoothed pictures as the baseline, and add about 10 smoothed pictures in `Baselines/legacy-smoothed/` for the gallery only,
  never byte-checked.

**Asked with TC-3's results (gallery G1; the lead puts it to the owner with G1):**

- **O-34. The program draws every WPF window in software, the live view included.**
  - **Facts.** `OpenPmsDental/Main Modules/ProgramEntry.cs:21` sets `RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly` for the
    whole process before any window exists. Upstream added it in 24.2.48 and again in 24.3.15 (`d4d86a572`) with no comment, so its
    reason is not recorded; it is the program's only render-mode setting, and it covers every WPF window and control of the program
    (`WpfControlsOD` and every `ElementHost`), not only the chart. A window's `HwndTarget.RenderMode` cannot turn hardware back on. So the
    live view of O-19 (b) is drawn by WPF's software rasterizer at 1x with no edge smoothing (jagged tooth edges, unlike the renderer's
    3x picture and DirectX's smoothed default; G1's live-view section shows both), and the "Draw in software" switch chooses between two
    software pictures: the live view (fast refills) and the renderer's smoothed picture (the same as every print; a refill takes 0.4 to
    0.7 s). G1's times now include the live view's frame in software.
  - **(a)** A separate, owner-approved package removes or narrows the line. Narrowing means removing the process-wide line and asking for
    software per window (`HwndTarget.RenderMode`) where it is still wanted, since a window can ask for software but not for hardware. Either
    way it changes WPF windows of the whole program, so it needs its own launch check on the office's graphics, and it brings back the
    driver risk (dotnet/wpf#7076) that the switch and Setup > Graphics then cover. The live view would then be drawn by the graphics card as
    O-19 (b) assumed.
  - **(b)** Keep the line, and decide O-19 again with the live view as it actually runs: (b1) keep the live view as the default
    on-screen chart (the fastest refills; 1x edges) with the switch for the smoothed picture; or (b2) show the renderer's smoothed
    picture everywhere (O-19 (a); what is printed is what is shown, but a refill takes 0.4 to 0.7 s), and drop the live view.
  - **Recommended:** (b1). The line is upstream's and covers far more than the chart, so changing it belongs to its own package and launch
    check, if ever. In software the live view is out of reach of driver faults, and with its cached 3D layer a selection or a pen move
    redraws no 3D (G1's times); the switch stays for an office that prefers the smoothed picture.

---

## 11. Launch checks by the lead

- **L-1 (TC-0).** On the build PC's desktop session:
  - Which modes start (the DirectX runtime, the OpenGL driver).
  - The recorder's images are not blank or garbled; OpenGL's read-back from a covered window is the known weak point.
  - Note the adapter and driver.
  - After the SharpDX cut, the re-recording is byte-identical.
- **L-2 (TC-1).** The image host in a test window:
  - At 100% and at 150% (4K), and dragged between monitors with different scaling. The program runs PerMonitorV2
    (`OpenPmsDental/app.config:116`) with WPF DPI awareness off (`OpenPmsDental/Properties/AssemblyInfo.cs:33`). The chart should
    follow the control's pixel size.
  - The times for O-19: a refill plus render of the Chart module's chart at 410×307 and at 615×461, and a selection-only change.
  - The determinism test once with the desktop at 150%: the same bytes as at 100%.
  - The render time and memory of a big chart.
- **L-3 (TC-2).** In DirectX mode, every caller unchanged:
  - Chart module refill, Show Big, and the Treatment Plan print preview (classic and sheet).
  - Referral letter, dashboard widget (with the dashboard open and closed), sheet editor preview, Save to Images.
  - Graphical perio, Setup > Graphics.
  - Start-up on a computer with no DirectX format saved.
- **L-4 (TC-3).** With the DEBUG developer switch:
  - Click and drag selection, pen, eraser and colour changer.
  - Slider ticks and module switches, timed against DirectX, with the dashboard open and closed.
  - With the live view only: focus and keyboard around the ElementHost.
  - With the live view, as the program draws it (in software at 1x, O-34): a click on a tooth and a pen stroke respond at once at 100%
    and 150%; the edges beside the "Draw in software" picture (the switch); and that no other WPF window looks different (the fix round
    changes only the chart's view).
- **L-5 (TC-5, TC-6).**
  - Free text added, edited, moved and deleted, and still there after a restart.
  - Text and TextBelow codes; the extraction setting on and off.
  - The ortho tab's hardware and ortho mode.
- **L-6 (TC-7).** With the DEBUG developer switch, which the relay's perio gate honours since TC-2: graphical perio from the Perio Chart:
  display, setup colours, print, save.
- **L-7 (TC-8).**
  - Every host.
  - A computer with saved DirectX and one with saved OpenGL now show the WPF chart; a 2D computer as O-30 decides.
  - Graphical perio on a 2D computer as O-31 decides.
  - With the live view only: the software switch from Setup > Graphics, and from Setup > Computers for another PC; a PC with Intel Iris
    Xe graphics if one exists (dotnet/wpf#7076).
  - A forced WPF start failure falls back to 2D with one log line.
  - The dashboard image.
  - The G4 captures come only from the scratch database with synthetic patients.
- **The office check (after TC-8, before TC-9a; the input to O-26).** One office workstation, or a PC with the office's graphics type,
  runs the chart's image tests from a test build with no database, as O-28 does for the recording. With the live view, also the
  on-screen chart in hardware.
- **L-8 (TC-9a, TC-9b).**
  - A Release dry run without the five DLLs.
  - A clean Windows VM without the DirectX End-User Runtime: the 3D chart, graphical perio and printing.
  - An install over the previous release removes the five DLLs.
- **3b:** net10-plan section 6's line "the tooth chart in WPF/2D, DirectX and OpenGL modes" becomes "the WPF chart and the 2D chart on
  x64", and L-2 is repeated on net10 x64.

---

## 12. Risks and mitigations

- **The look differs from DirectX.** WPF lights per vertex like D3D9 fixed function, but a WPF light has one colour for all terms.
  Mitigations: the material factors of 5.3, DirectX's own constants, the parity probes, and galleries G0 and G1 before anything reaches
  users.
- **Parts drawn on top of the tooth.** WPF 3D cannot turn depth testing off. Mitigation: the 2D layer (5.1), with the known difference
  shown in G1.
- **Mirrored geometry vanishes under WPF's front-face rule.** Mitigation: the winding rule of 5.2 and its test over every transform the
  chart uses.
- **Transparency.** WPF does not sort transparent models. Mitigation: alpha 255 on every 3D colour, checked in every scene (5.3).
- **Handedness and transform order.** Mitigation: the transform test against SharpDX's math, later against recorded numbers.
- **Images made off the UI thread.** The dashboard renders on an MTA worker. Mitigation: no WPF object on a caller's thread, the
  renderer's own STA thread, and MTA tests in TC-1 and TC-8 (5.4).
- **Hardware WPF 3D draws nothing on some drivers** (dotnet/wpf#7076, open). Mitigation: the on-screen chart shows the software render
  (O-19 (a)); with the live view, the software switch and the live preview. While the program draws every WPF window in software
  (`ProgramEntry.cs:21`, O-34), the live view does not use the driver at all.
- **The live view is drawn in software in the program** (O-34). The plan assumed a hardware view. Mitigations: G1 shows the live view as
  the program draws it and times its frame in software; the cached 3D layer and the pen stroke's own element keep 2D-only changes from
  rasterizing the 3D layer; a source test pins the line, so removing it brings the plan and the tests along.
- **`RenderTargetBitmap` failures in loops** (dotnet/wpf#3067), and an access violation with one d3d9.dll version (#7933). Mitigations:
  one render thread, reused bitmaps, one retry, and a 2D image of the same data as the last resort, logged.
- **Render cost on every refill.** A software render replaces DirectX's hardware draw. Mitigations: frozen cached meshes, material and
  transform updates only, one posted render per burst, the reused 3D layer for 2D-only changes, the dashboard image only when visible,
  and the times in TC-1 (O-19), TC-3 and L-4. Every slider tick rebuilds the chart synchronously today (`ControlChart.cs:3522-3525`).
  The 53-model parse per wrapper (`ToothChartWrapper.cs:539-555`) is the same for every engine and is left as it is.
- **DPI.** Offscreen text follows the system DPI in the test host. Mitigation: `SetRootDpi` and `pixelsPerDip` 1.0, the 150% run in L-2
  and its repeat at 3b (5.6).
- **Memory in the 32-bit bridge release.** A 3× render of the big chart is about 28 MB. Mitigations: the 8-megapixel cap, at most three
  cached sizes, and TC-10's measurement in both bitnesses.
- **Helix's net48 closure.** System.Net.Http 4.3.4 is known to cause binding trouble on .NET Framework. Mitigations: TC-4's offline check,
  the chart-scoped pinning chosen by a whole-slnf restore, the staged-file diff, `BindingRedirectTests` and the reference test.
- **Helix 3.1.2 defects.** Mitigation: banned APIs (TC-4) and 3.x-only documentation in the briefs.
- **Shared files with 3a's menu rewrite.** Mitigation: TC-2 moves the chart code out of `ControlChart`, `ControlTreat`, `FormOpenDental`,
  `FormPerio` and `FormPerioGraphical` before 3a. Later chart packages edit only chart files and files no stage package owns.
- **Brittle baselines** (fonts, Windows updates, the .NET render DLL). Mitigations: tolerances, explained re-baselining with a gallery
  diff, and baselines recorded on the build PC only.
- **The legacy recording is incomplete** (no DirectX runtime; OpenGL read-back). Mitigations: O-28, and the sidecar files show which
  mode really drew.
- **No 3D fallback after TC-9a.** Mitigations: software rendering and 2D, plus O-26's office check before TC-9a.
- **Unknown specification for the extraction graphic and TextBelow.** Mitigation: owner decisions O-20 and O-21, shown in G2 before
  merge.
- **Writer and lane load.** The track uses one of the three writer slots, so the stage packages can run with one writer fewer. Its image
  tests add about 30 s to Full runs.
- **The track misses the bridge release.** Mitigation: the S3a-15 rule (section 6) and O-26's choice for a late TC-8.

---

## 13. Changes to `docs/specs/net10-plan.md` and `docs/roadmap.md` (lead, with this plan)

Places are named by package and section, not by line, because the lines move.
- **S1-0's row and the stage 1 "Order and parallelism" paragraph:** "S1-0 alone" becomes "S1-0 alone among the stage packages". The
  chart track's TC-0 writer may work in its own worktree meanwhile, uses the lane only between S1-0's steps and merges right after S1-0
  (section 2 here).
- **S1-4:** `SparksToothChart` leaves its list (TC-1).
- **S1-6:** "108 after stage 0" becomes 109 (`tests/ToothChartImageTests`, TC-0); 111 since the EHR plan, with S0-F's `S0FTests` and EHR-0's
  `EhrTests` too. `tests/*/*.csproj` counts 108 at `bb446f65a`.
- **The "Order" paragraphs of stages 0, 1 and 3a:** the chart track counts toward the three-writer cap.
- **Stage 2, "Left in `Required dlls`":** SharpDX and Tao are retired by TC-9b, before the bridge release if it fits (O-26).
- **S4-1:** its test `.cs` edits include `tests/ToothChartImageTests`, whose STA runner may then move to `[STATestMethod]`.
- **S3a-15:** runs as written only under the rule of section 6; otherwise TC-10 takes its place.
- **Section 3's x64 line, the 3a "Order" paragraph, the 3a Full-test gate, the 3a tooling changes and risks, and the 3b 64-bit entry
  conditions:** each "S3a-15" becomes "S3a-15, or TC-10 in its place under the rule of the chart plan's section 6". The x64 test runs
  that must pass include TC-10's x64 run of the chart image tests when TC-10 runs. The "S3a-12 to S3a-16" of the section 1 table
  (row 7), section 1.1 item 5 and decision 19 counts TC-10 in S3a-15's place.
- **Section 1 table, row 3a, and section 1.1 item 5:** "the tooth chart engines on x64" becomes "the WPF tooth chart on x64 (TC-10), or
  the old engines (S3a-15)".
- **S3b-3:** `SparksToothChart` is retargeted with Helix through its net8.0-windows assets. SharpDX and Tao are gone, unless O-26
  deferred them.
- **S3b-5:** its lists gain `tests/ToothChartImageTests`, which references WpfControlsOD, and `tests/ToothChartHostTests` (TC-8),
  which references the program: 25 test projects that reference the exe and 9 that reference WpfControlsOD. It re-records
  `Baselines/wpf-net10-x64/` (gallery G5), and the host tests leave x86 there.
- **Stage 3b risks, "Compatibility-mode binaries (SharpDX, Tao)":** applies only if O-26 deferred them.
- **Stage 5 note** and **section 7, row "Tooth chart engines on x64 (S3a-15)":** now the WPF chart on x64 (TC-10). Section 7 gains rows
  for the chart image tests (3a record, 3b compare).
- **Section 5:** adds batch H (section 9 here). Batch D is unchanged.
- **Section 6:** the 3a wave 3 line ("the tooth chart in its three modes") and the two 3b lines ("WPF/2D, DirectX and OpenGL modes";
  "DirectX and OpenGL modes on x64") change as in section 11 here.
- **Section 12:** one line saying that O-17 to O-31 are in this document.
- **`docs/roadmap.md`:** item 2 of the order (13-14) and the tooth chart section's plan paragraph (90-91): "after .NET 10" and "on .NET
  10" become "alongside the .NET 10 stages, on .NET Framework first". The chart reaches the office in the bridge release. The 64-bit
  section's last sentence (130-132) changes with S3a-15.

---

## 14. Size

12 packages; extra large; about 31-47 dev-days with one writer:
- TC-0: 2-3 dd
- TC-1: 4-6 dd
- TC-2: 3-4 dd
- TC-3: 5-8 dd (+1-2 dd for the live view, O-19 (b))
- TC-4: 1-2 dd
- TC-5: 3-4 dd
- TC-6: 3-4 dd
- TC-7: 4-6 dd
- TC-8: 3-5 dd
- TC-9a: 1-2 dd
- TC-9b: 1-2 dd
- TC-10: 1 dd

S3b-5's re-recording is extra.

---

## Appendix. Changes from the review

Each finding was checked again at `bb446f65a` before its fix was applied. "As proposed" means the review's fix was applied. "Changed"
means the fix was wrong or incomplete, and the line says what was done instead.

| Item | How it was resolved | Where |
|---|---|---|
| 1 (high) Overlays on top of the tooth | Changed: a 2D layer drawn from projected points instead of a second `Viewport3D`. DirectX draws every depth-off item in flat colour, in call order, at a screen position independent of depth (391-395, 778-808, 960-975, 1122-1207); the 2D layer reproduces that directly. A second viewport would depth-sort the overlays, need its own light and still cull mirrored flat items; `EmissiveMaterial` adds to what is under it (Microsoft's API page). The build-up becomes a projected silhouette. The cases where DirectX lets a later draw cover an overlay are a recorded, gallery-shown difference. | 1 item 6, 5.1, TC-1, TC-3, 8 |
| 2 (high) All mirrored geometry | Changed: the determinant rule covers every 3D instance, but DirectX's handedness flip is left out of the WPF chain, so only the lingual perio rows (and their implants) are negative. Droplets, triangles and bars are in the 2D layer, so no `BackMaterial` is needed. The mesh test covers the four perio rows and the implant flip. | 5.2, TC-1, TC-7 |
| 3 (high) Unerupted teeth see-through | As proposed (alpha 255 on every 3D and polygon colour), plus: text keeps its alpha as D3DX blends text, and a test checks that no scene has a transparent 3D material, so WPF's unsorted transparency never applies. | 5.3, TC-1, TC-3 |
| 4 (high) Dashboard gets 2D | Changed: no WPF object is ever created on a caller's thread; images come from the renderer, and the on-screen chart is the renderer's image (O-19 (a)). With the live view, the `ElementHost` is created only when shown on an STA thread. MTA tests in TC-1 and TC-8 check `Engine` and the log. | 5.4, TC-1, TC-8, O-19 |
| 5 (high) TC-1 before S1-T | As proposed (after S1-0 and S1-T; ReleaseFlowTests, LibraryBindingTests, BPERIOTests, BPRINTTests added), plus BINBTests, whose `CompiledReferencesTests.cs:33` reads this project's DLL. BPERIOTests and BPRINTTests do not reference the chart (grep); they stay as S1-4's tests. | 1 item 2, 2, TC-1 |
| 6 (high) Files TC-4 and TC-9 miss | Changed: TC-1 makes `LayoutManagerForms` engine-agnostic, so TC-9 needs no edit there. Added `Resources.Designer.cs`, which the review missed. `ReleaseIdentityTests`, `FormLicense`, `Resources.resx` and the two text items as proposed (TC-4 edits the licence window only for a Helix notice with its own text). The ReleaseFlowTests, LibraryBindingTests and BINBTests row keeps "new files only": only `ReleaseIdentityTests` pins the five DLL names (grep). | 3, TC-1, TC-4, TC-9b |
| 7 (high) Where TC-9 fits; the S3a-15 rule | Changed: TC-9 is split as proposed, but the S3a-15 rule depends on TC-8, not TC-9a: after TC-8 no user reaches the old engines, and the rule can be met in wave 3. The office check moves to right after TC-8, with the image tests from a test build. TC-9b may slip to `main` after the bridge release, before the branch cut. A late TC-8 is an owner choice. | 2, 6, TC-9a, TC-9b, TC-10, O-26, 11 |
| 8 (high) TC-2's startup search and ranges | As proposed (helpers into `ToothChartWrapper`; ranges 817-835, 1720, 1722-1729, 1753-1765; TC-2 owns the `ToothChartFallbackTests` pins; narrower source test), plus: `FormPerio`'s gate and `FormPerioGraphical`'s forced mode also move into relay helpers in TC-2, so only `FormGraphics` is exempt until TC-8, and TC-7/TC-8 change only the relay. | TC-2, 3, 7 |
| 9 (medium) Fork enum rule | As proposed (`ToothChartEngine`, `DrawingMode` unchanged). Checked: `DrawingMode` is stored (`computerpref.GraphicsSimple`, `tinyint(1)`, `ConvertDatabases1.cs:4362`), not in memory only; the new enum is in memory only and outside the business layer. | 1 item 5, TC-1 |
| 10 (medium) Extraction preference name | As proposed: a fork-owned row named `Fork...` outside `PrefName`, the fork's rule for unpublished names (`RecallProphyPerioSwitch`, P262-MISC). Checked: the repository's 26.2 documents name no extraction preference. Added the adoption note and the later rename path (`OdSettingNamesStep`). TC-5 drops `Pref.cs`, `PrefInfos.xml` and the convert script. | TC-5, O-20, 3 |
| 11 (medium) Wrapper branches and batching | Changed: the rebuild is posted to the UI thread once per burst, not left to the next paint, because the engine control covers the wrapper. The branches and the read-only scene as proposed. | 5.5, 5.4 |
| 12 (medium) Dashboard image on every refill | Changed: the guard is `FormOpenDental.IsDashboardVisible` (exists, 6234-6238), in TC-2's commit (3), since a dashboard that opens later builds its own image. TC-3 and L-4 time refills with the dashboard open and closed. | TC-2, TC-3, 11 |
| 13 (medium) DPI-independent images | As proposed (`SetRootDpi`, `pixelsPerDip` 1.0, L-2 again at 3b), except: a test cannot change the system DPI, so the 150% byte check is a lead run (L-2). The attribute is at `AssemblyInfo.cs:33`, not 32. | 5.6, 7, 11, TC-10 |
| 14 (medium) Renderer and test robustness | As proposed (background thread, meshes frozen on the render thread, one bitmap per size with `Clear()`, `CopyPixels`, no waits for frames), plus at most three cached sizes and a render thread that never waits for another thread. | 5.4, 7 |
| 15 (medium) TC-4 package settings scope | Changed: pinning stays in `SparksToothChart.csproj`, but the choice between pinning and direct references is made by an unlocked restore of the whole slnf with no NU1603, NU1605, NU1608 or NU1107 anywhere, because per-project settings may not reach the projects that reference the chart. Step 1 checks, the redirect files and the H2 stop as proposed. | TC-4, 3, 9 |
| 16 (medium) TC-0 records before refactoring | As proposed, plus: the cut also moves `DrawDirectX` and the fan triangulation, and an off-site recording takes both test builds in one visit (O-28). | TC-0, O-28, 11 |
| 17 (medium) Scope gaps for the owner | As proposed (O-30, O-31, the save rule, Helix as a geometry library, the counsel fallback), plus: O-19 now asks whether a switch is needed at all, and recommends the renderer's image on screen, which needs none. | 1, TC-8, O-18, O-19, O-30, O-31 |
| 18 (medium) TC-10's static test | As proposed: the x64 test build's DLL must load in a 64-bit process; the normal build stays x86 by section 2.3. | TC-10 |
| 19 (low) Stale references | As proposed: `ToothGroup.cs` lines (5, 19-21, 40-80, 48-55, 82-92); `ToothGraphic.Copy` (75-87) moves in TC-0; net10-plan places by package name; the cap's location; the stub called unused. | TC-0, 1, 13 |
| 20 (low) Parity details | As proposed, plus: the 0.4 specular follows the colour (near-white), not the group (`GetGroupMaterial`, 875-898); the D3DX font width becomes a horizontal scale; the number boxes keep DirectX's measure rule. | TC-3, TC-7, 5.3 |
| 21 (low) Moved labels lost | As proposed (clamp, whole numbers, de-DE test). Not changed: existing labels with decimals are still parsed with the workstation's culture (`ToothInitial.cs:66-78`). | TC-2 |
| 22 (low) O-19's old-build sentence | As proposed: no `;`, the default format, a Reference device that fails, then the fallback. | O-19 |
| 23 (low) Developer switch could ship | As proposed (DEBUG only), plus a source test. | TC-2 |
| 24 (low) No `.gitattributes` | As proposed: the lead adds `*.png binary`. Git already detects the tracked PNGs as binary by content. | 7, 3 |
| 25 (low) Privacy in G4 | As proposed. | 8, 11 |
| 26 (low) Missing plan updates | As proposed: S1-6's count (108 at `bb446f65a`, then 109), S4-1, S3b-5's lists, and the DirectX 11 check that also reads the relay (TC-2). | 13, TC-2 |
| 27 (low) Printed image format | Changed: the DirectX format is not known from the code (a PNG of the back buffer), so TC-0's recorder records each mode's `PixelFormat` and alpha, and TC-8 checks that the WPF image matches and is opaque. | TC-0, TC-8, 5.4 |
| 28 (low) O-25 misses status D | As proposed, and it is a painter option in TC-2. | O-25, TC-2 |
| 29 (low) Writer slots at the end of stage 0 | Changed: stage 0 is finished. TC-0's writer starts now and merges after S1-0, which merges alone; it keeps one first-wave slot if late. | 2 |
| 30 (low) L-6 cannot run in TC-7 | As proposed (the developer switch bypasses the gate): the gate is a relay helper since TC-2, so TC-7 changes only the relay. | TC-2, TC-7, 11 |

**Second review.** Ten findings on the revised plan, each checked again at `bb446f65a` in the same way.

| Item | How it was resolved | Where |
|---|---|---|
| 1 (medium) TC-0 works while S1-0 runs alone | As proposed: net10-plan's "S1-0 alone" (its row and the stage 1 "Order and parallelism" paragraph) becomes "alone among the stage packages", with TC-0's writer in its own worktree, on the lane only between S1-0's steps. Plus: the rule names the steps it keeps clear (step 2's switch, Full run and dry run; step 4's baseline) and covers the lead's recorder run (L-1). | 1 item 8, 2, 13 |
| 2 (medium) net10-plan lines that need S3a-15's x64 runs | As proposed: one section 13 bullet for section 3's x64 line, the 3a "Order" paragraph, the 3a gate, the 3a tooling changes and risks and the 3b entry conditions, and for the "S3a-12 to S3a-16" of the section 1 table, section 1.1 item 5 and decision 19. Checked: these places name S3a-15 or the wave 3 range and were not in section 13. | 13 |
| 3 (medium) TC-10 would build the program for x64 | Changed: the host tests move to a new `tests/ToothChartHostTests` (TC-8), which references the program and stays x86 until S3b-5; `ToothChartImageTests` never references the program. It is created after S1-6 and S4-1, so it is SDK-style and on MSTest 4 from the start, and S1-6's count is unchanged. The proposed first step ("WpfControlsOD builds for x64") is a note instead: S3a-12 already builds WpfControlsOD for x64, because its ImagingCaptureTests references it. To keep the image tests free of program code, TC-2's moved-label clamp is a relay helper and TC-7's perio print and save images are rendered through the wrapper. | 3, TC-2, TC-7, TC-8, TC-10, 7, 13 |
| 4 (low) Stale on-screen chart after a refill | As proposed: the posted rebuild always refreshes the screen; results reach the UI thread only by a non-blocking `BeginInvoke`, skipped for a disposed host; the 3D-layer image is held by the wrapper and released on `Dispose`. Changed: each render job catches its own exceptions and feeds the failure path, and `Dispatcher.UnhandledException` with `Handled=true` is only the last guard, so a `GetBitmap` still gets its retry and its 2D image. TC-1 also tests a host disposed during a render. | 5.4, 5.5, TC-1, 7 |
| 5 (low) Licence-list rules that ReleaseIdentityTests enforces | Changed: the rules go into TC-4 and TC-9b as proposed, but the `SharpDX` vendor text is removed, never re-keyed: a vendor text replaces its component's standard licence text (`FormLicense.cs:184`), so re-keyed it would replace Helix's own MIT text. The SharpDX notice stays inside TC-4's Helix entry. TC-9b now owns the SharpDX resource, its designer property, its `.txt` file and its `Resource` item (`OpenPmsDental.csproj:9003`). The test's lines are 42-44 and 173-175, not 47-50 and 165-169. | TC-4, TC-9b, 3 |
| 6 (low) TC-3 does not wait for O-19 | As proposed. | TC-3 |
| 7 (low) O-30's button text | As proposed: "Use Simple Graphics" (`FormComputers.designer.cs:97`). | O-30 |
| 8 (low) The sealant "S" and depth testing | As proposed: the "S" is screen-space text drawn after 808 turns depth back on (867, 870), and it stays in the 2D layer. Plus: item 6's 3D layer is "the meshes" drawn with depth on, and 5.1's 2D-layer line names the "S". | 1 item 6, 5.1 |
| 9 (low) The camera width's lines | As proposed: `ToothChartData.cs:26` (`SizeOriginalProjection`, 130 × 97.34). | 5.3 |
| 10 (low) TC-2 and the classic test project | As proposed, for every TC package before S1-6, not only TC-2; TC-2's Owns cell says so too. | 2, TC-2 |
