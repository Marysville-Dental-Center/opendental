> **Organization review, 2026-11-01: a snapshot.** A read-only analysis of `main` at 1db2c6d86, made before the organization cleanup, the rename and the v0.0 release. It uses the names and paths of that time (OpenDental, OpenDentBusiness and so on), and "you" is the owner it was written for. It inventories every project, its dependencies, dead projects and leftovers, and proposes a target structure. What it proposed for before v0.0 was done by package ORG-CLEANUP (docs/port-notes/ORG-CLEANUP.md) and the binary packages BIN-A and BIN-B; the restructuring it leaves for after v0.0 (section 5d: test consolidation, layering, NuGet and SDK-style projects) is still open.

ORGANIZATION REVIEW: projects, dependencies, leftovers and target structure

Baseline: `main` at 1db2c6d86 (clean apart from the untracked `aqtinstall.log`). I read the capture work from `cap/cap-4` at c7dd54cb8, whose merge base with main is ee44421eb. Nothing in the repository was changed and no database was touched.

Per your note ("Skip office switch over for now"), the adoption and switch-over tools (`tools/AdoptionValidation`, `docs/adoption-runbook.md`) are listed in the inventory only. Nothing below proposes switch-over work.

Abbreviations used in the tables: DCB = xDataConnectionBase, ODB = OpenDentBusiness, CB = xCodeBase. A `*` after a name means that project is not built by the solution filter.

---

## 1. Key numbers

**Solution and filter**
- `OpenDental.sln` has 110 project entries.
- `OpenDental.Local.slnf` has 104. The 6 entries it leaves out are exactly the 6 whose project files sit outside the repository and do not exist on disk (checked by script: sln entries minus slnf entries = missing files):
  - `..\EHR\xEHR.csproj` (sln:37)
  - `..\OpenDentalWebApps\OpenDentalWebCore` (sln:41)
  - `..\OpenDentalWebApps\OpenDentalSocket` (sln:51)
  - `..\OpenDentalWebApps\OpenDentBusiness.ODXam` (sln:55)
  - `..\ImagingDeviceManager\xImagingDeviceManager.csproj` (sln:61)
  - `..\OpenDentalWebApps\OpenDentBusiness.WebApis` (sln:65)
- None of `..\EHR`, `..\OpenDentalWebApps` and `..\ImagingDeviceManager` exists next to the checkout.

**Projects outside the solution**
- There are 133 tracked .csproj/.vcxproj files. 17 are in no solution entry:
  - `CodeBaseStandard`, `DataConnectionBase/xDataConnectionBaseStandard`, `DocumentationBuilder`, `MobileWeb`, `ODR`, `OpenDentBusiness.Tests`, `OpenDentalWpf`, `PluginExample`, `QueryExecutor`
  - `RdlDesign/ReportDesigner`, `RdlEngine`, `RdlViewer`, `RdlViewer/RdlReader`
  - `SlowQueryLogTool`, `SparksToothChart/TestForm`, `TestCanada`, `TestToothChart`, `tools/DevDbTool`
- Eight separate legacy .sln files exist: `DocumentationBuilder`, `PluginExample`, `RdlDesign`, `RdlEngine`, `RdlViewer`, `SlowQueryLogTool`, `SparksToothChart`, `TestCanada`.

**Test assemblies**
- The 81 test assemblies are 78 `tests/*` projects plus `NoHqTests`, `tools/AdoptionValidation/AdoptionValidation.Tests` and `tools/SyntheticData/SyntheticData.Tests`.
- The runner `.git/od-tools/run-tests.ps1` (lines 66-68) runs every `*Tests.csproj` in the slnf except the legacy `UnitTests`. It is not committed.
- There are 4,263 `[TestMethod]` attributes in those folders. The legacy `UnitTests` project holds 1,952 more, which never run.

**Build output and binaries**
- What ships is `OpenDental\bin\<cfg>`, zipped by `ForkReleases.Publish` (`docs/release-process.md:104`). It holds only three executables: `OpenDental.exe`, `ServiceManager.exe` and `UpdateFileCopier.exe`. Checked by listing `OpenDental/bin/Debug`: 96 files, no HL7, CentralManager, MySQLManager or DatabaseIntegrityCheck executable.
- `Required dlls/` holds 118 files (52 MB) and `RequiredServerDlls/` 6.

---

## 2. Inventory

### 2a. Non-test entries of OpenDental.sln (29)

All are old-style csproj targeting .NET Framework 4.8 unless noted. All are built by the slnf except rows 25-29.

| # | Project (sln name) → assembly | Folder | Type | References (projects) | Referenced by | Ships? | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | OpenDental → OpenDental.exe | OpenDental/ | WinExe | xODCrypt, CB, DCB, ODB, OpenDentalCloud, OpenDentalGraph, ServiceManager (exe), SparksToothChart, UpdateFileCopier (exe), WpfControlsOD, `..\..\ImagingDeviceManager` (missing; OpenDental.csproj:9490). Build-order-only dependencies on xDirect2dWrapper and xDatabaseIntegrityCheck (sln:5-13). | xCentralManager, UnitTests, 17 tests/* (plus PluginExample*, TestCanada*) | yes | keep |
| 2 | OpenDentBusiness | OpenDentBusiness/ | Library | xODCrypt, CB, DCB, OpenDentalCloud, PasswordVaultWrapper | 18 non-test projects, 76 of 78 tests/* | yes | keep |
| 3 | xCodeBase → CodeBase.dll | CodeBase/ | Library | none | almost everything | yes | keep |
| 4 | xDataConnectionBase → DataConnectionBase.dll | DataConnectionBase/ | Library | CB | 15 projects, 79 test projects | yes | keep |
| 5 | xODCrypt → ODCrypt.dll | ODCrypt/ | Library | none | ODB, OpenDental, UnitTests, CryptoTests, DevDbTool* | yes | keep |
| 6 | PasswordVaultWrapper | PasswordVaultWrapper/ | Library | CB | ODB, xCentralManager | yes | keep |
| 7 | OpenDentalCloud | OpenDentalCloud/ | Library | CB | ODB, OpenDental, OpenDentHL7 | yes | keep; now only `Storage/Sftp.cs` and `Storage/RemovedStorage.cs` plus task states |
| 8 | OpenDentalGraph | OpenDentalGraph/ | Library | CB, DCB, ODB | OpenDental | yes | keep |
| 9 | SparksToothChart | SparksToothChart/ | Library | CB, DCB, ODB | OpenDental, WpfControlsOD, UnitTests (plus TestForm*, TestToothChart*) | yes | keep |
| 10 | WpfControlsOD | WpfControlsOD/ | Library (built Any CPU) | CB, DCB, ODB, SparksToothChart | OpenDental, xCentralManager, UnitTests, BPERIOTests, BPRINTTests, BSHELLTests, M2MISCTests | yes | keep |
| 11 | ServiceManager | ServiceManager/ | WinExe, used in-process | CB, DCB, ODB | OpenDental (`FormOpenDental.cs:54`, `:5732` `new FormMain()`) | yes | keep |
| 12 | UpdateFileCopier | UpdateFileCopier/ | WinExe | none; compiles 17 linked CodeBase files plus `OpenDentBusiness/Misc/ForkRelease/ReleaseManifest.cs` | OpenDental (and copy target OpenDental.csproj:9577-9578) | yes | keep (linking sources is deliberate: it replaces those DLLs while running) |
| 13 | xDirect2dWrapper | Direct2dWrapper/ | native C++ DLL (Win32) | none; P/Invoked by `OpenDental/UI/Direct2d/Direct2d.cs:186` | sln build order only; post-build xcopy | yes (via xcopy) | keep; fix the post-build step (F1) |
| 14 | OpenDentHL7 | OpenDentHL7/ | WinExe (Windows service) | CB, DCB, OpenDentalCloud, ODB | none | **no** | owner decision |
| 15 | OpenDentalServer | OpenDentalServer/ | ASP.NET ASMX middle tier | CB, ODB | none | **no** (IIS) | owner decision (`owner-questions.md:315`, B-SERVER 5) |
| 16 | xCentralManager → CentralManager.exe | CentralManager/ | WinExe | CB, DCB, OpenDental (exe), ODB, PasswordVaultWrapper, WpfControlsOD | UnitTests | **no** | owner decision |
| 17 | xDatabaseIntegrityCheck | DatabaseIntegrityCheck/ | WinExe | CB | nothing (only OpenDental's sln build-order entry) | **no** | owner decision |
| 18 | xMySQLManager | MySQLManager/ | WinExe | CB, DCB | none | **no** | owner decision |
| 19 | xCrudGenerator → CrudGenerator.exe | xCrudGenerator/ | WinExe (developer tool) | CB, DCB, ODB | UnitTests | no | keep; belongs in tools/ |
| 20 | UnitTests → "Unit Tests.exe" | UnitTests/ | WinExe test harness, MSTest v1, NuGet Mono.Cecil | xODCrypt, xCentralManager, CB, DCB, OpenDental, ODB, SparksToothChart, UnitTestsCore, WpfControlsOD, xCrudGenerator | xDocumentationBuilder* | no; tests never run | owner decision; holds the seed `Resources/dump.txt` |
| 21 | UnitTestsCore | UnitTestsCore/ | Library (150 `*T` test-data factories) | CB, DCB, ODB | UnitTests and 24 tests/* | no | keep, becomes TestSupport |
| 22 | SyntheticData | tools/SyntheticData/ | Exe | none (MySqlConnector, Newtonsoft only) | AdoptionValidation and 2 test projects | no | keep |
| 23 | AdoptionValidation | tools/AdoptionValidation/ | Exe | SyntheticData, CB, DCB, ODB | AdoptionValidation.Tests | no | keep (switch-over tool; out of scope) |
| 24 | xImagingDeviceManager | `..\ImagingDeviceManager` (missing) | — | — | OpenDental (swapped for `Required dlls/ImagingDeviceManager.dll` by `Directory.Build.targets:20-33`) | the prebuilt Open Dental DLL ships | `cap/cap-4` brings it in-repo |
| 25-29 | xEHR, OpenDentalWebCore, OpenDentalSocket, OpenDentBusiness.ODXam, OpenDentBusiness.WebApis | outside the repo, missing | — | — | nothing | — | delete the entries |

### 2b. Projects outside the solution (17, none built)

| Project | Type | References | Referenced by | Status / evidence |
|---|---|---|---|---|
| CodeBaseStandard/xCodeBaseStandard.csproj | SDK-style, netstandard2.0 | links 18 CodeBase files (`_README.txt`) | xDataConnectionBaseStandard* | dead (served the OpenDentalWebApps) |
| DataConnectionBase/xDataConnectionBaseStandard.csproj | netstandard2.0 | CodeBaseStandard; 3 HintPaths to files that don't exist (Ubiety.Dns.Core, System.Text.Encoding.CodePages, Unsafe) | none | dead; it is the only user of `Required dlls/.NET Standard/*` |
| DocumentationBuilder/ | WinExe 4.8, own .sln | CB, DCB, ODB, UnitTests (exe) | none | owner decision (`port-backlog.md:92` "only if we keep publishing schema docs") |
| MobileWeb/ | ASP.NET 4.5 | ODB, `..\WebForms` and `..\WebHostSynch` (both missing); ships its own AjaxMin.dll, AjaxMinTask.dll, Microsoft.Web.GeneratedImage.dll | none | dead (eServices mobile web) |
| ODR/xODR.csproj | Library 4.7.2 | CB, DCB, ODB; HintPath to the missing `Required dlls\MySql.Data.dll` | none | owner decision. RDL report definitions name ODR as a `<CodeModule>`, but the build never copies `ODR.dll` (`third-party-binaries-audit.md:331`; it is absent from OpenDental/bin/Debug) |
| OpenDentBusiness.Tests/ | Visual Studio 2005 era (ToolsVersion 3.5, ProductVersion 8.0.50727) | ODB and `..\OpenDentServer` (missing) | none | dead |
| OpenDentalWpf/ | Library 4.5 (old WPF dashboard) | ODB | none | dead |
| PluginExample/ | Library 4.7.2, own .sln | CB, DCB, OpenDental, ODB | none | owner decision (plugin sample) |
| QueryExecutor/ | WinExe, Visual Studio 2005 | MySql.Data (missing) | none | dead |
| RdlDesign/ReportDesigner.csproj | WinExe 4.5 (fyiReporting 2.9.3, LGPL) | RdlEngine, RdlViewer | none | owner decision; the prebuilt `RdlDesigner.exe` is unused |
| RdlEngine/ | Library 4.5 | packages.config MySql.Data 6.9.9; missing HintPath | RdlViewer*, ReportDesigner* | owner decision; OpenDental uses the prebuilt `Required dlls/RdlEngine.dll` |
| RdlViewer/ | Library 4.5 | RdlEngine | RdlReader*, ReportDesigner* | owner decision; the prebuilt `RdlViewer.dll` is used |
| RdlViewer/RdlReader/ | WinExe | RdlViewer (HintPath into `RdlEngine\bin\Debug`) | none | dead |
| SlowQueryLogTool/ | WinExe 4.7.2, own .sln | NuGet ML.NET (all 17 `packages\` HintPaths missing); its own `Required dlls/PoorMansTSqlFormatterLib35.dll`, plus a 620 KB ML model zip and 867 KB of training data | none | dead (Open Dental support tool: "Non-Open Dental Queries", FormSlowQueryLog.cs:227) |
| SparksToothChart/TestForm/ | WinExe | SparksToothChart; its own `TestForm/RequiredDlls` | none | dead |
| TestCanada/ | WinExe 4.5, own .sln, 7.2 MB of Canadian test dumps | OpenDental, ODB | none | dead (Canadian networks were cut) |
| TestToothChart/ | WinExe 4.5 | CB, ODB, SparksToothChart; Microsoft.DirectX.Direct3D | none | dead |
| tools/DevDbTool/ | Exe 4.8 | xODCrypt, CB, DCB, ODB | built by `tools/New-DevDatabase.ps1` | live; not in the slnf by design (README:78) |

### 2c. Test projects (81, all in the slnf, all MSTest v1 Library .NET 4.8, nothing references them)

**The 78 `tests/*` projects, by prefix**
- 23 named by feature or by M1: Accounting, Appt, Attribution, ChartLetter, Crypto, EForm, Fee, ForkMigration, GroupNote, Imaging, Insurance, Medication, PayPlan, QueryMonitor, Registry, ReleaseFlow, Report, RowHash, Sheet, Signature, Statement, TaskCategory, VendorDirect.
- 20 B* projects: BACCT, BAPPT, BAUDIT, BCLAIM, BDBM, BEF, BERA, BIMG, BINSH, BMED, BPAY, BPERIO, BPRINT, BPROC, BSERVER, BSHELL, BTASK, BTIME, BTP, BWIKI.
- 28 M2* projects: M201, M202, ADJ, APPT, AUDIT, CAN, CARRIER, CLAIMFORM, CLAIMRCV, DUN, EF, EMP, ERA, EST, HIST, IMG, INSPLAN, INSV, MED, MISC, NOTES, PAYW, RECUR, SDV, SHEETS, SHELL, TERM, UDS.
- 7 V* projects: VBRIDGE1, VBRIDGE2, VDXC2, VEDS, VGP, VPEARL1, VPEARL2.

**What they reference**
- Base set: CB, DCB and ODB (76 projects). The exceptions are AttributionTests (CB, OpenDental) and CryptoTests (CB, xODCrypt).
- 17 also reference OpenDental.exe; 24 reference UnitTestsCore; 4 reference WpfControlsOD.
- 34 compile the linked `..\ForkMigrationTests\TestDb.cs`.

**The other three**
- `NoHqTests/` sits at the repository root, not under `tests/`.
- `tools/AdoptionValidation/AdoptionValidation.Tests` and `tools/SyntheticData/SyntheticData.Tests` sit next to their tools.

**Size**
- `[TestMethod]` counts range from 8 (StatementTests) to 873 (VendorDirectTests); the median is about 40.

### 2d. What cap/cap-4 adds

- In-repo projects:
  - `ImagingDeviceManager/xImagingDeviceManager.csproj` (Library; PdfSharp only).
  - `ImagingCapture/xImagingCapture.csproj` (references CB, ImagingDeviceManager, ODB, WpfControlsOD).
- Tests: `tests/ImagingCaptureTests`, `tests/ImagingDeviceManagerTests`. That makes 83 test assemblies.
- OpenDental.csproj now references both projects in-repo.
- It deletes `ImagingDeviceManager.dll`, `Eztwain4.dll`, `EZT4Jpeg.dll`, `EZT4Pdf.dll`, `VideoCamLib.dll`, `MediaFoundation.dll` and `MediaFoundation.Extension.dll`.
- It keeps `xImageDeviceManager.dll` (`cap/cap-4:OpenDental/OpenDental.csproj:274-276`).

---

## 3. Dependency graph (main, slnf projects)

ProjectReference edges by layer; "(exe)" marks a reference to an executable.

```
L0  xCodeBase            (no project refs; refs WinForms, WebView2.WinForms, Tao.*, SigPlusNET, VBbridges)
    xODCrypt             (none)
    UpdateFileCopier     (none; source-links 17 CodeBase files + ReleaseManifest.cs)
    SyntheticData        (none)
    xDirect2dWrapper     (native; P/Invoke from OpenDental)
L1  xDataConnectionBase  -> CodeBase
    OpenDentalCloud      -> CodeBase
    PasswordVaultWrapper -> CodeBase
    xDatabaseIntegrityCheck -> CodeBase
    xMySQLManager        -> CodeBase, DCB
L2  OpenDentBusiness     -> ODCrypt, CodeBase, DCB, OpenDentalCloud, PasswordVaultWrapper
L3  SparksToothChart, OpenDentalGraph, ServiceManager(exe), xCrudGenerator(exe), UnitTestsCore -> CodeBase, DCB, ODB
    OpenDentHL7(exe)     -> CodeBase, DCB, OpenDentalCloud, ODB
    OpenDentalServer     -> CodeBase, ODB
    AdoptionValidation   -> SyntheticData, CodeBase, DCB, ODB
L4  WpfControlsOD        -> CodeBase, DCB, ODB, SparksToothChart
L5  OpenDental(exe)      -> ODCrypt, CodeBase, DCB, ODB, OpenDentalCloud, OpenDentalGraph, SparksToothChart, WpfControlsOD,
                            ServiceManager(exe), UpdateFileCopier(exe), [ImagingDeviceManager.dll substituted]
                            (+ sln build-order: Direct2dWrapper, DatabaseIntegrityCheck)
L6  xCentralManager(exe) -> CodeBase, DCB, ODB, PasswordVaultWrapper, WpfControlsOD, OpenDental(exe)
L7  UnitTests(exe)       -> ODCrypt, CodeBase, DCB, ODB, SparksToothChart, WpfControlsOD, UnitTestsCore,
                            OpenDental(exe), xCentralManager(exe), xCrudGenerator(exe)
Tests tests/* -> {CodeBase, DCB, ODB} [+OpenDental(exe) x17] [+UnitTestsCore x24] [+WpfControlsOD x4]; NoHqTests -> CodeBase, DCB, ODB
```

**Edges that point upward at runtime** (not visible as ProjectReferences):
- ODB raises UI navigation events that FormOpenDental handles (`OpenDentBusiness/UI/GlobalFormOpenDental.cs:7`, whose own comment says these "really don't belong in the business layer").
- `WpfControlsOD/Bridges/DexisIntegrator.cs:115` looks up `"OpenDental.FormOpenDental"` by name string.
- Plugin DLLs are loaded at runtime: `OpenDentBusiness/Plugins/Plugins.cs:113-115` and `WpfControlsOD/Bridges/PluginLoader.cs:72`.
- There are no real ProjectReference cycles.

---

## 4. Findings

### F1. Out-of-repo references (build health)

1. **OpenDental.sln does not build by itself.** It holds 6 entries for missing projects (section 1), and they have Debug|x86 Build.0 mappings. That is why the README sends everyone to the slnf. Five of them serve removed features (EHR, the web apps, Xamarin). The sixth is fixed by cap-4.
2. **Leftover Xamarin build configuration.** `OpenDental.sln` carries an "ODXam" solution configuration (sln:239-240; "ODXam" appears 443 times). Six csproj files have ODXam PropertyGroups too (for example `OpenDentBusiness.csproj:86-105`, and likewise CodeBase, DataConnectionBase, OpenDentalCloud, OpenDentalGraph, UnitTestsCore).
3. **`Directory.Build.targets:20-33`** swaps missing ProjectReferences for DLLs in `Required dlls`. The only missing reference left in a built project is `OpenDental.csproj:9490` → `..\..\ImagingDeviceManager` (checked by script over every built project). After cap-4 the target has nothing to do. The decoupling plan already calls it temporary (`decoupling-plan.md:17`).
4. **`OpenDental.csproj:228` has an out-of-repo HintPath:** `..\..\..\..\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\System.dll`.
   - It points at a 4.7.2 copy although the project targets 4.8.
   - From this checkout's depth it doesn't resolve. In a shallower checkout it could pick up the wrong System.dll.
5. **`Direct2dWrapper.vcxproj` writes outside the repository.**
   - Its Debug post-build xcopies the DLL to `..\..\..\Unversioned\Icons\IconOrganizer\bin\Debug\` and `...\Direct2DTest\bin\Debug\` (lines 116-117 and 140-141).
   - It also copies into `..\UnitTests\bin\Debug\` (114, 138).
   - On this PC that created `D:\Unversioned\Icons\{IconOrganizer,Direct2DTest}\bin\Debug\Direct2dWrapper.dll` (verified).
6. **Vestigial build-order dependency.** The sln makes OpenDental depend on xDatabaseIntegrityCheck (sln:5-13). No OpenDental code or target uses it.
7. **Dead HintPaths and references in unbuilt projects** (the missing-file scan):
   - ODR, QueryExecutor and RdlEngine point at `Required dlls\MySql.Data.dll`.
   - xDataConnectionBaseStandard has 3 missing HintPaths; RdlReader points into `RdlEngine\bin\Debug`; SlowQueryLogTool has 17 missing `packages\` paths.
   - MobileWeb references WebForms and WebHostSynch; OpenDentBusiness.Tests references OpenDentServer.
8. **Fork tooling that lives outside the repository.**
   - `run-tests.ps1` and `sln_merge_driver.py` are in `.git/od-tools`, and `odbuild.ps1` is in the session notes. None is committed.
   - A fresh clone cannot run the suite the docs quote ("81 assemblies, 4,243 tests", `docs/port-notes/M2-SHELL.md:151`).
   - `tools/Test-NoHqHosts.ps1:220` and the runner both read `OpenDental.Local.slnf`, so the filter must stay, or the scripts must switch to the sln.

### F2. Dead projects (not built, not shipped, not testing anything live)

**Projects**
- CodeBaseStandard and xDataConnectionBaseStandard.
- OpenDentBusiness.Tests, OpenDentalWpf, QueryExecutor and MobileWeb (eServices).
- TestCanada (Canadian networks, cut), TestToothChart, SparksToothChart/TestForm.
- SlowQueryLogTool (an Open Dental support tool) and RdlViewer/RdlReader.
- The evidence for each is in table 2b.

**Folders with no project**
- `DataClassesOld/`: 12 .cs files.
- `Macros/`: Visual Studio 2005 macro project files.

**Unused per-project binary folders**
- `SparksToothChart/RequiredDlls/`: 4 DLLs, 2.7 MB. SparksToothChart.csproj:116-124 uses `..\Required dlls\` instead; TestForm uses its own `TestForm/RequiredDlls` (TestForm.csproj:39, 43).

**Knock-on cleanups**
- Deleting CodeBaseStandard makes the `DOT_NET_STANDARD` `#if` blocks in 10 CodeBase files dead.
- `tests/M2AUDITTests/BirthdateSavePathTests.cs:16-17` lists some of these folders as exclusions. That is harmless after deletion.

### F3. Built and maintained, but not shipped and with no consumer

These cost build time and the C++/web workloads (README:28-29), and they keep compiling against ODB.

**Built but never delivered**
- OpenDentHL7.exe, CentralManager.exe, MySQLManager.exe, DatabaseIntegrityCheck.exe and OpenDentalServer are all built, but the release zip holds only the OpenDental output folder.
  - OpenDental.csproj never mentions HL7, CentralManager, MySQLManager or OpenDentalServer (grep count 0).
- So the fork can build the HL7 service but has no way to deliver it to the office. Its service name `OpenDentHL7` is set in `OpenDentHL7/ProjectInstaller.cs:19`.

**The legacy UnitTests project**
- It is built in the slnf but excluded by the runner.
- 1,952 test methods never run. Port notes confirm new tests went to `tests/*` instead (M2-EST.md:69, M2-INSV.md:50, M2-RECUR.md:96, V-PEARL1.md:51).
- It also holds the seed `UnitTests/Resources/dump.txt` (2.4 MB). 24 files read it: DevDbTool, `New-DevDatabase.ps1` and 18 test helpers.

**Closed-source sibling programs that code still expects** (no project anywhere)
- `OpenDentalService.exe`: `OpenDental/Logic/PrefL.cs:462-500`, which has no callers but is pinned by `NoHqTests/EServicesRemovedTests.cs:124`.
- `OpenDentalReplicationService`: `ForkReleases.cs:76` (a sub-folder the release zip includes) and `ServiceManager/FormServiceManage.cs:103`.
- `OpenDentalAPIService`: `FormServiceManage.cs:104`.

### F4. Duplicated or overlapping pieces

**Prebuilt copies of in-repo sources**
- `Required dlls/PasswordVaultWrapper.dll` is a stale copy; ODB uses the project.
- `ODR.dll` duplicates `ODR/`; nothing references it.
- `RdlEngine.dll` and `RdlViewer.dll` (used by OpenDental HintPaths) and `RdlDesigner.exe` (unused) duplicate `RdlEngine/`, `RdlViewer/` and `RdlDesign/`, all at version 2.9.3.
- `SparksToothChart/RequiredDlls` and `TestForm/RequiredDlls` duplicate the Tao and freeglut DLLs in `Required dlls/`.

**Test helpers that build scratch databases**
- About 33 separate implementations, each with a different hash: ScratchDb, TestDb, TestSupport, ServerTestDb, WikiScratchDb, UdsTestDb, ReportTestDb, ... (for example tests/ApptTests/ScratchDb.cs, tests/BACCTTests/TestDb.cs, tests/M201Tests/ScratchDb.cs).
- In addition, 34 projects compile one linked copy of `tests/ForkMigrationTests/TestDb.cs`.
- Beyond the tests, the legacy `UnitTests/DatabaseTools.cs` and `UnitTests/Misc/TestBase.cs`, `tools/DevDbTool/DevDatabase.cs`, `tools/SyntheticData/Infrastructure/SchemaBuilder.cs` and `tools/AdoptionValidation/DatabaseCopier.cs` build databases too.
- 66 distinct environment-variable switches (`*_TEST_DB`, `*_KEEP_DB`, ...) control them.

**Legacy and new tests split by package instead of by domain**
- UnitTests vs UnitTestsCore vs tests/* overlap by domain. For example, appointments are tested in ApptTests, BAPPTTests and M2APPTTests. Imaging in ImagingTests, BIMGTests, M2IMGTests and VPEARL1/2Tests (plus cap's two). Claims and ERA in BCLAIM, M2CLAIMFORM, M2CLAIMRCV, BERA and M2ERA.

**Code**
- There are two ProgramL classes: `OpenDental/Logic/ProgramL.cs:20` and `WpfControlsOD/Logic/ProgramL.cs:13`.
- Superseded WinForms windows remain, for example `FormFamilyMemberSelect` and `FormGuardianEdit`. Nothing constructs them; `FrmFamilyMemberSelect` and `FrmGuardianEdit` replaced them.
- UpdateFileCopier and CodeBaseStandard source-link CodeBase files. That is justified for UpdateFileCopier (it replaces CodeBase.dll while running).

### F5. Layering problems

**UI code in the business assembly**
- OpenDentBusiness.csproj references System.Windows.Forms, PresentationFramework, System.Xaml, WindowsBase, Interop.Word, Sparks3D, SigPlusNET and RS232.
- 75 ODB files use WinForms; 39 call MessageBox.Show.
- `OpenDentBusiness/UI/` holds 24 WinForms control files (GridOD, SignatureBox, TopazWrapper, ...). 9 of them use namespace `OpenDental.UI`.
- The WPF XAML binds to them: `clr-namespace:OpenDental.UI;assembly=OpenDentBusiness` appears in 5 .xaml files.
- So the IIS middle tier (OpenDentalServer) and the services load WinForms, WPF and Office interop.

**UI code in the bottom layer**
- CodeBase, which README:124 calls "shared utilities with no database dependency", references System.Windows.Forms, PresentationCore, WebView2.WinForms, Tao.OpenGl, SigPlusNET and VBbridges.
- `CodeBase/Controls/` holds WinForms controls (MessageBox, ODWebView2, OpenGLWinFormsControl, PrintPanel); 21 CodeBase files use WinForms.

**WpfControlsOD is part WPF, part WinForms bridges**
- `WpfControlsOD/Bridges` has 109 files: 88 use WinForms, none uses WPF, and they use namespace `OpenDental.Bridges`.
- Namespace `OpenDental` spans three assemblies: OpenDental.exe (1,730 files), WpfControlsOD (175 files `OpenDental`, 105 `OpenDental.Bridges`, 4 `OpenDental.UI`) and ODB (9 files).
- `CodeBase/Utilities/Environment_.cs:4` declares `namespace OpenDentalWebCore`.

**Vendor-integration code is spread over 7 folders in 4 assemblies**
- `CodeBase/Utilities/VendorDirect` (9 files).
- `OpenDentBusiness/Bridges` (49), `/WebBridges` (48) and `/VendorDirect` (28).
- `WpfControlsOD/Bridges` (109).
- `OpenDental/Bridges` (18) and `OpenDental/Logic/VendorDirect`.

**References to executables**
- OpenDental → ServiceManager (it opens `FormMain` in-process) and → UpdateFileCopier.
- CentralManager → OpenDental.exe.
- UnitTests → OpenDental, CentralManager and CrudGenerator.
- DocumentationBuilder → UnitTests.
- PluginExample and TestCanada → OpenDental.exe.

**The OpenDental / WpfControlsOD split itself**
- The split is upstream's WinForms-to-WPF migration path: 823 WinForms `Form*` windows against 154 WPF `Frm*` windows, and only 3 names overlap.
- WpfControlsOD cannot see OpenDental. That is why the GlobalFormOpenDental event hub exists and why ProgramL is duplicated.
- The split itself is fine. Its contents (bridges, WinForms) are the problem.

### F6. Leftover folders and files

**Root**
- `packaging/` (35 files) is the 2007 Debian/Mono packaging. It exports from Open Dental's SVN at 70.90.133.65 (`packaging/scripts/export-opendental:3,7`).
- `Makefile` runs `mdtool build --buildfile:OpenDental5_6.sln`. `BUILDING.Linux` is the Mono build note dated Sep 28, 2007. `mkfolders.cmd` creates C:\opendentaldata.
- `UpgradeLog.XML`, `UpgradeLog.htm` and `UpgradeLog2.htm` are Visual Studio upgrade reports.
- `FreeDentalConfig.xml` and `OpenDentServerConfig.xml` are ancient samples: port 9390 belongs to the retired OpenDentServer, and they hold a dummy password. No code reads them, but README:113 mentions them. The fork's own sample is `tools/FreeDentalConfig.dev.xml`.

**RequiredServerDlls/**
- 6 files: batch files and installutil.exe for the retired OpenDentServer service (audit lines 245-250).

**Tracked editor files**
- 22 `*.csproj.user` and `*.suo` files are still tracked, although `.gitignore` lists both patterns. Examples: CodeBase/xCodeBase.csproj.user and RdlDesign/RdlDesign.suo.

**Uncompiled source files** (zero build effect; most were never in the csproj)
- 52 files in OpenDental: the Anesthesia forms, `FormApptEditOld`, `FormClaimInstEdit`, `FormLanguageDefs`, `FormPatList2014` and its 2 relatives, `FormScannerSetup`, `Tables Old Grids/TableClaimPay`, `UI/PrintPreview`, `ContrAnesthesia(Data)` and `UserControlTreatPlanInsHistory`.
- One each elsewhere: `CodeBase/Utilities/ResponseListHandler.cs`, `OpenDentalGraph/Cache/DashboardCacheHQMessages.cs`, `UnitTests/UnitTests/ProcedureButtonTests.cs`, `UnitTestsCore/TableTypes/ProcButtonQuickT.cs`, and RdlDesign's "Copy of FiltersCtl".

**Other**
- `OpenDentBusiness/Resources/rxnorm.zip` (1.8 MB) is only a `<None>` item (OpenDentBusiness.csproj:2544). Nothing reads it.
- `OpenDental/bin/Debug/ConversionFiles/` (12 tracked files) is still read by the legacy chain for pre-4.4 databases (`ConvertDatabases1.cs:29-30, 119, 302, 767, 1017`). No csproj item copies it, so a Release output lacks it.

**Phase-3 code that is still in place**
- 156 `FormEhr*` files in OpenDental/Forms.
- `OpenDentBusiness/InternalTools` (59 HQ table-type and Crud files). These are kept deliberately: `NoHqTests/HqToolsRemovedTests.cs:14,55,103-124`.
- `OpenDentBusiness/WebTypes`, `OpenDentBusiness/Web References` (92 files) and `HL7old`.
- Developer test windows inside the shipped WpfControlsOD.dll: FrmTestCombos, FrmTestFilters, FrmTestSplitters, Frm_BasicTemplate.
- These are code-level, not structural.

### F7. Required dlls

**Unused: no built project references them and nothing loads them** (rechecked against the audit's "delete outright" list, `third-party-binaries-audit.md:203-250`)
- `.NET Core/` (3) and `.NET Standard/` (10; only the dead xDataConnectionBaseStandard uses them).
- `Microsoft.ML.*` (11) and `Microsoft.DirectX*` (3; only TestToothChart).
- `MSVCR71.dll`, `PasswordVaultWrapper.dll`, `RdlDesigner.exe`, `System.Net.Http.dll`, `freeglut.dll`.
- `MediaFoundation*` (2; cap-4 deletes them).
- Seven more go once a dead `<Reference>` is removed (audit:252-264): AxInterop.AcroPDFLib, Interop.Acrobat, Interop.AcroPDFLib, Interop.AFORMAUTLib, MigraDoc.RtfRendering, Tao.FreeGlut, zxing.presentation.

**Open Dental proprietary files still in the build, against the v0.0 rule**
- `xImageDeviceManager.dll` (10 MB) and `SuniUSB.dll`:
  - Compile-time reference: OpenDental.csproj:274-276.
  - Used by ControlImagesOld.cs:26, 123 and 181, which FormImages and FormOpenDental.cs:561 always create.
  - Still referenced on cap/cap-4. The held branch `decouple/twain` (45a8b8206) deletes them.
- `VBbridges.dll`: HintPaths in OpenDental, WpfControlsOD and CodeBase; 5 static calls (audit:139-142).
- `Sparks3D/` (Sparks3D.dll, Sparks3DXEngine.dll): HintPaths in ODB, OpenDental, UnitTests and WpfControlsOD; the release zip includes a `Sparks3D` folder (ForkReleases.cs:75). The fallback `SparksToothChart` already exists (audit:134-137).
- `ImagingDeviceManager.dll`, the EZTwain trio and `VideoCamLib.dll`: removed by cap-4.
- No backlog package or branch owns VBbridges or Sparks3D: neither name appears in port-backlog.md, the decoupling plan or owner-questions.md, and no branch is named for them.
- The other license items (SigPlusNET, COM interop, `stdole.dll` and `Windows.winmd` copied into OpenDental/bin/Debug, installutil, RS232) are covered by the audit.

**Binaries outside the audit's scope** (it covered only `Required dlls/` and `RequiredServerDlls/`)
- MobileWeb's 3 DLLs.
- `SlowQueryLogTool/Required dlls/PoorMansTSqlFormatterLib35.dll` and its ML model zip.
- The SparksToothChart and TestForm `RequiredDlls` copies.
- `rxnorm.zip`.

### F8. tools/

**Live**
- `AdoptionValidation` (+Tests), `SyntheticData` (+Tests) and `DevDbTool`.
- Scripts: `New-DevDatabase.ps1`, `New-OdReferenceDatabase.ps1`, `Test-NoHqHosts.ps1`, and the `FreeDentalConfig.dev.xml` sample.

**Gaps**
- `xCrudGenerator` is a developer tool at the repository root.
- `DevDbTool` is built only by its script, so nothing notices when it breaks.
- The test runner, build lane and merge driver are not in tools/ (F1.8).
- README:129 still says tools/ is "not in the solution", but AdoptionValidation and SyntheticData are in it.

### F9. The 81 test assemblies: keep the layout for v0.0, consolidate after

**Costs today**
- 78 near-identical csproj files of about 80 lines (for example `tests/FeeTests/FeeTests.csproj`).
- Every project copies its full dependency closure: `tests/*/bin` totals 5.7 GB, 75-118 MB each.
- About 33 duplicated scratch-database helpers and 66 environment switches.
- Package-code names (M2CLAIMRCVTests) mean nothing to new contributors.
- MSTest v1 depends on a Visual Studio install (`Directory.Build.targets:36-46`).

**Benefits today**
- Each assembly runs in its own vstest process (`run-tests.ps1`: one `Start-Process vstest` per DLL, `$MaxParallel`). That isolates ODB's process-wide static caches.
- Targeted runs are cheap (`-Include`).
- Parallel package work did not collide.

**Verdict**
- The layout works (4,243 tests passing) and changing it now only adds risk. Leave it as it is through v0.0.
- After v0.0, consolidate by domain into about 14 assemblies plus one `TestSupport` library, keeping the database, UI and pure-logic tests apart (section 5d).

### F10. Rename hazards for the final step

**Executable and process names**
- `UpdateFileCopier/FormMain.cs:83` (`KillProcess("OpenDental")`), `:88` (ServiceManager) and `:428` (restarts `OpenDental.exe`).
- `ForkReleases.cs:80-92`: the build-id file list, which also names UpdateFileCopier.exe.
- After a rename, a workstation that pulls the new release keeps its old `OpenDental.exe` and shortcut. That stale build would keep pulling. The renaming release needs a transition: a stub exe, or UpdateFileCopier removing the old file and repointing the shortcut.

**Windows service names**
- `OpenDentHL7` (ProjectInstaller.cs:19) and `OpenDentalService` (PrefL.cs:473, 488). Renaming them orphans services that are already installed.

**Names held in strings**
- `DexisIntegrator.cs:115` (`"OpenDental.FormOpenDental"`).
- `Remoting/DataTransferObject.cs:52` (`"OpenDentBusiness."+name`), and `Meth.cs:368` and `Security.cs:433` (`typeof(T).FullName`). The middle-tier client and server must change in lockstep.
- The 5 XAML files with `assembly=OpenDentBusiness`.
- `Plugins.cs:115` (`assName+".Plugin"`). Plugins compiled against the OpenDental and OpenDentBusiness assembly names, PluginExample included, need an AssemblyResolve shim.

**Type names that may be stored in the database**
- `TsiTransLog.TransJson` is written and read with `TypeNameHandling.Auto` (TsiTransLog.cs:94, 106). Check stored rows for `$type` before the rename.
- The `ImageStore` preference holds `'OpenDental.Imaging.FileStore'` (ConvertDatabases1.cs:6557). Nothing reads it any more, so it is safe.

**Folder and file names**
- `%ProgramData%\OpenDental\Logs` (Logger.cs:177).
- `%LOCALAPPDATA%\OpenDental\...` (LocalWindowSizes.cs:21, QueryFilters.cs:294) and Sftp.cs:61.
- Config files `FreeDentalConfig.xml`, `OpenDentalServerConfig.xml` and `OpenDentalServiceConfig.xml` (ForkReleases.cs:278).
- Recommendation: keep these names. They are local data, not branding.

**Tests and scripts that read repository paths**
- 56 files: NoHqTests (7), VendorDirectTests (16), and many tests/* source checks such as `BPRINTTests/WiringTests.cs:201`.
- `Test-NoHqHosts.ps1`, `run-tests.ps1` and the sln merge driver.

**Safe**
- Translations are keyed by the class's short name (`OpenDental/Logic/Lan.cs:26` uses `GetType().Name`).
- The schema snapshots use LogicalName (OpenDentBusiness.csproj:2185, 2188).
- Database table, column and preference names stay untouched.

**Also worth a look**
- 751 string literals start with "OpenDental" (mostly translation contexts and UI text).
- cap-4's facade class `EZTwain` (`ImagingDeviceManager/EZTwain.cs:27`) carries a vendor product name.

### F11. Stale documents

- README:44-53 (actual line 53) says the filter loads "the 21 projects"; it now has 104. README:51 lists 12 missing projects; 6 remain.
- README:19 tells you to merge upstream builds, which contradicts `decoupling-plan.md:17`.
- README:128-131 describes the tests and tools as outside the solution.
- `decoupling-plan.md:17` and `:93` describe the substitution target and the solution filter as temporary.

---

## 5. Proposed structure

### 5a. Low-risk cleanup worth doing before v0.0

Do this after cap/cap-4 merges into main. The sln, slnf, OpenDental.csproj and Required dlls all conflict with it. After the merge, check that the xImagingDeviceManager Project line points at `ImagingDeviceManager\`: compare main sln:61 with the cap-4 diff.

1. **Solution.**
   - Delete the 5 missing out-of-repo entries.
   - Delete the ODXam configuration from the sln and the 6 csproj files.
   - Delete the vestigial OpenDental→DatabaseIntegrityCheck dependency.
   - Result: OpenDental.sln builds by itself. Keep `OpenDental.Local.slnf`, because the scripts read it.
2. **Build targets.** Remove `ODSubstituteMissingProjectReferences` (`Directory.Build.targets:20-33`); keep the MSTest search-path target.
3. **Out-of-repo paths.**
   - Remove the HintPath at OpenDental.csproj:228.
   - Remove the post-build xcopies to `..\..\..\Unversioned` and `..\UnitTests\bin` (Direct2dWrapper.vcxproj:114-117, 138-141).
   - `D:\Unversioned` on this PC is outside the repository, so deleting it is your call.
4. **Dead projects and folders (F2).**
   - Delete CodeBaseStandard, xDataConnectionBaseStandard.csproj, OpenDentBusiness.Tests, OpenDentalWpf, QueryExecutor and MobileWeb.
   - Delete TestCanada, TestToothChart, SparksToothChart/TestForm and SparksToothChart/RequiredDlls.
   - Delete SlowQueryLogTool, DataClassesOld and Macros.
5. **Root leftovers.**
   - Delete packaging/, Makefile, BUILDING.Linux, mkfolders.cmd and the three UpgradeLog files.
   - Delete the root FreeDentalConfig.xml and OpenDentServerConfig.xml, and fix README:113.
   - Delete RequiredServerDlls/.
   - Untrack the 22 `*.user`/`*.suo` files.
6. **Unused Required dlls (F7).**
   - Delete the 32 unused files listed in F7: the 34 there minus the two MediaFoundation files, which cap-4 deletes.
   - Then remove the 7 dead References and delete those files.
7. **Uncompiled files and unused data.** Delete the 57 uncompiled source files (F6) and `rxnorm.zip` with its `<None>` item.
8. **Tooling.**
   - Commit `run-tests.ps1` into `tools/`.
   - Add `tools/DevDbTool` to the slnf so the build catches its breakage. It is not a `*Tests` project, so the runner ignores it.
9. **Docs.** Update the README and the decoupling plan (F11).

None of these changes compiled code paths or the database. Check with one full slnf build, the Core test run and `Test-NoHqHosts.ps1`.

### 5b. Required before v0.0 by your release rules, but not "cleanup" and not low-risk

- **Merge cap/cap-4.** It removes ImagingDeviceManager.dll, EZTwain, VideoCamLib and MediaFoundation.
- **xImageDeviceManager.dll and SuniUSB.dll.** Decide whether to drop direct Suni capture (port `decouple/twain` 45a8b8206) or re-implement it.
- **VBbridges.dll.** Re-implement its 5 static methods in C#.
- **Sparks3D.** Remove it and make SparksToothChart the default. Also remove `"Sparks3D"` from ForkReleases.cs:75.
- **The remaining license items in the audit:** SigPlusNET, the COM interop files, stdole, Windows.winmd, installutil and RS232.
- **The rename itself** (5e), with the F10 checklist built into the rename package.

### 5c. Decisions for you

1. **Which secondary programs ship in v0.0?** OpenDentHL7, the OpenDentalServer middle tier (owner question B-SERVER 5), CentralManager, MySQLManager and DatabaseIntegrityCheck. Anything shipped needs a copy step into the release folder; anything not shipped should go or move under `tools/`.
2. **The closed-source sibling programs the code still expects:** OpenDentalService, OpenDentalReplicationService and OpenDentalAPIService (F3).
3. **RDL reporting.**
   - Build `ODR`, `RdlEngine` and `RdlViewer` from the in-repo source (switch them to MySqlConnector, copy `ODR.dll` into the output) and delete the prebuilt binaries? Or keep the binaries and delete the source?
   - Keep or delete `RdlDesign/`?
   - Custom RDL reports that use the ODR code module fail today, because `ODR.dll` is never copied.
4. **The legacy UnitTests.** Retire them (1,952 tests that don't run), or revive the useful ones into `tests/`? Either way, move the seed `dump.txt` (24 readers) to a neutral place such as `db/seed/`.
5. **PluginExample and plugin compatibility** after the rename.
6. **DocumentationBuilder:** keep it?
7. **ConversionFiles:** ship them with Release builds, or raise the oldest database version the frozen chain will convert?
8. **The rename's identifier form.** I propose `OpenPmsDental`. Also decide whether folders move (to `src/`) at the rename or later.
9. **Phase-3 leftovers** (EHR forms, InternalTools types, WebTypes): in or out of v0.0.

### 5d. Restructuring better left until after v0.0

1. **Tests.**
   - Create `tests/TestSupport`: UnitTestsCore plus one ScratchDb with one switch (for example `OPMS_TEST_DB` / `OPMS_KEEP_DB`) and one repository-root locator.
   - Add `tests/Directory.Build.props` for the shared boilerplate.
   - Move to MSTest v2 from NuGet.
   - Consolidate about 81 assemblies into about 14 by domain:
     - Scheduling (Appt, BAPPT, M2APPT)
     - Claims and ERA (BCLAIM, M2CLAIMFORM, M2CLAIMRCV, BERA, M2ERA, VEDS, VDXC2)
     - Insurance (Insurance, BINSH, M2INSPLAN, M2INSV, M2CARRIER, M2HIST, M2EST)
     - Payments and billing (BPAY, PayPlan, M2PAYW, M2RECUR, VGP, Statement, M2DUN, M2ADJ)
     - Accounting (Accounting, BACCT)
     - Clinical (BPROC, BPERIO, BTP, BMED, Medication, M2MED, M2NOTES, GroupNote, ChartLetter, M2SDV, M2CAN, Fee)
     - Forms and sheets (EForm, BEF, M2EF, Sheet, M2SHEETS, Signature)
     - Imaging (Imaging, BIMG, M2IMG, VPEARL1/2, cap's two)
     - Security and audit (BAUDIT, M2AUDIT, RowHash, Crypto)
     - Shell and UI (BSHELL, M2SHELL, M2MISC, BPRINT, Attribution)
     - Administration (BTASK, TaskCategory, BTIME, M2EMP, BWIKI, QueryMonitor, Report, M2UDS, BDBM, BSERVER, M2TERM)
     - Platform (ForkMigration, M201, M202, Registry, ReleaseFlow, NoHq)
     - Vendor direct (VendorDirect, VBRIDGE1/2)
     - The two tool test projects stay next to their tools.
   - Keep the assemblies that reference OpenDental.exe apart from pure business tests. Watch the static state when tests share a process.
2. **Layering.**
   - Move WinForms and WPF out of OpenDentBusiness and CodeBase (OpenDentBusiness/UI, CodeBase/Controls, GlobalFormOpenDental) into a shared UI library.
   - Move the vendor SDK references (VBbridges, SigPlusNET, Tao, WebView2) out of CodeBase.
   - Gather vendor-integration business code into one assembly, and move the launchers in `WpfControlsOD/Bridges` and the duplicate ProgramL into it or into the UI library.
   - Remove `namespace OpenDental` from ODB and WpfControlsOD.
3. **Packaging.** Replace the committed open-source binaries with NuGet packages (77 files, per the audit). Convert the projects to SDK-style csproj: OpenDental.csproj is 9,594 lines of explicit includes, which is how the 52 uncompiled files went unnoticed.
4. **Small cleanups.** Fold OpenDentalCloud (now only SFTP storage) into a `Storage` assembly. Move the HQ-only table types into a clearly named legacy folder (they must stay; see NoHqTests).

### 5e. Target layout and name mapping (a proposal for the rename step)

Database tables, columns and preferences stay as they are. Config file names and data folder names stay too, unless you decide otherwise.

| Today (folder / assembly) | Proposed project = assembly | Notes |
|---|---|---|
| OpenDental / OpenDental.exe | OpenPmsDental (OpenPmsDental.exe) | needs the exe-transition plan (F10) |
| OpenDentBusiness | OpenPmsDental.Business | namespace OpenDentBusiness.* → OpenPmsDental.Business.*; 9 `OpenDental.UI` files move to UI |
| CodeBase (xCodeBase) | OpenPmsDental.Core | drop the `x` prefix: it only fed the substitution regex |
| DataConnectionBase | OpenPmsDental.Data | |
| ODCrypt | OpenPmsDental.Crypto (or fold into Core) | |
| PasswordVaultWrapper | OpenPmsDental.CredentialVault | |
| OpenDentalCloud | OpenPmsDental.Storage | holds only SFTP |
| WpfControlsOD | OpenPmsDental.Wpf | namespaces WpfControls / OpenDental / OpenDental.Bridges become one |
| OpenDentalGraph | OpenPmsDental.Dashboards | |
| SparksToothChart | OpenPmsDental.ToothChart | keep the original credit in NOTICE |
| Direct2dWrapper | OpenPmsDental.Direct2d (native) | DllImport name at Direct2d.cs:186 |
| ImagingDeviceManager / ImagingCapture (cap) | OpenPmsDental.Imaging.Devices / .Capture | consider renaming the `EZTwain` facade class |
| UpdateFileCopier | OpenPmsDental.Updater.exe | |
| ServiceManager | OpenPmsDental.ServiceManager.exe | |
| OpenDentHL7, OpenDentalServer, CentralManager | OpenPmsDental.HL7Service, .MiddleTier, .CentralManager | only if kept (5c.1); service-name migration |
| xCrudGenerator, DevDbTool, SyntheticData, AdoptionValidation | tools/* | |
| UnitTestsCore + scratch-database helpers | tests/TestSupport | |

**Folders:** `src/` (shipped code), `tests/`, `tools/`, `lib/` (vendor binaries left after the NuGet move), `db/seed/`, `docs/`.

The folder move is free to do at the rename, since the rename already moves every path. But it adds churn to the 56 path-reading test files and to the docs, so whether to do it then or later is a decision for you (5c.8).