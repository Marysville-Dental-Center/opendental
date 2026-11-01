# ORG-CLEANUP: organization cleanup before the rename

Branch `pre/org-cleanup-1`, from `main` at d9c177ca6. Source: the organization review (sections 4 and 5a, actions ORG-02 to ORG-13).

No compiled code path, schema, migration, preference or database name changed. The work only removes things that nothing builds, ships or reads, and points the build at the repository only. No namespace, assembly, project or executable was renamed.

## What changed

### 1. The solution builds by itself

- `OpenDental.sln` lost its 5 entries for projects outside the repository that don't exist on disk: `xEHR`, `OpenDentalWebCore`, `OpenDentalSocket`, `OpenDentBusiness.ODXam` and `OpenDentBusiness.WebApis`. Their configuration lines went with them.
- The Xamarin `ODXam` solution configuration is gone. So are its lines for every project, and the two `ODXam` PropertyGroups in each of `xCodeBase`, `xDataConnectionBase`, `OpenDentBusiness`, `OpenDentalCloud`, `OpenDentalGraph` and `UnitTestsCore`. Each is one small hunk per csproj.
- OpenDental's build-order dependency on `xDatabaseIntegrityCheck` is gone. Its dependencies on OpenDentBusiness, CodeBase, Direct2dWrapper and SparksToothChart stay.
- The solution and `OpenDental.Local.slnf` now list the same 109 projects (108, plus DevDbTool from item 8). The full build below built `OpenDental.sln` directly.

### 2. `Directory.Build.targets`

- `ODSubstituteMissingProjectReferences` is deleted. A script over the 109 projects found no missing ProjectReference among their 370; ImagingDeviceManager, the last such project, came in-repo with the capture track. Without the target, a missing project fails the build instead of quietly using an old binary.
- The MSTest v1 search-path target stays. The header describes the file as it is now.

### 3. Paths outside the repository

- `OpenDental.csproj`: the one HintPath line on `<Reference Include="System">` that pointed into `Program Files (x86)\Reference Assemblies\...\v4.7.2\System.dll` is removed. `System` now resolves from the 4.8 targeting pack.
- `Direct2dWrapper.vcxproj`: both Debug post-build steps only copy the DLL into `..\OpenDental\bin\Debug\` now. The copies into `..\UnitTests\bin\Debug\` and into the two `..\..\..\Unversioned\Icons\...` folders are gone. The Release steps already copied only into OpenDental's output.
- `D:\Unversioned\Icons\{IconOrganizer,Direct2DTest}\bin\Debug` on this PC was left in place: deleting it is the owner's call.

### 4. Dead projects and folders deleted

No built project references any of these. That was checked for every ProjectReference, HintPath and linked item in the remaining projects.

| Deleted | Why |
|---|---|
| `CodeBaseStandard/`, `DataConnectionBase/xDataConnectionBaseStandard.csproj` | .NET Standard builds for the out-of-repo web apps |
| `OpenDentBusiness.Tests/` | Visual Studio 2005 project; references the missing OpenDentServer |
| `OpenDentalWpf/` | Old WPF dashboard; nothing references it |
| `QueryExecutor/` | Visual Studio 2005; references a missing MySql.Data.dll |
| `MobileWeb/` | eServices mobile web; references two missing projects |
| `TestCanada/` | Test harness for the Canadian networks, which were cut (7.2 MB of dumps) |
| `TestToothChart/`, `SparksToothChart/TestForm/`, `SparksToothChart/RequiredDlls/` | Tooth chart test windows, plus private copies of the Tao and freeglut DLLs |
| `SparksToothChart/SparksToothChart.sln` | Visual Studio 2005 solution whose only other project was TestForm (INFERRED, below) |
| `SlowQueryLogTool/` | Open Dental support tool; its ML.NET packages are not in the repo |
| `RdlViewer/RdlReader/` | Standalone RDL reader. Its entry is also gone from the legacy `RdlViewer/RdlViewer.sln` |
| `DataClassesOld/`, `Macros/` | No project at all |

### 5. Root leftovers

- Deleted: `packaging/` (2007 Debian/Mono packaging, exported from Open Dental's Subversion server), `Makefile`, `BUILDING.Linux`, `mkfolders.cmd`, `UpgradeLog.XML`, `UpgradeLog.htm`, `UpgradeLog2.htm`, and `RequiredServerDlls/` (batch files and installutil.exe for the retired OpenDentServer service).
- Also deleted: the root `FreeDentalConfig.xml` and `OpenDentServerConfig.xml`. They were upstream samples for port 9390. No code reads them; every reader takes the file from the program's own folder. The fork's sample is `tools/FreeDentalConfig.dev.xml`.
- Untracked with `git rm --cached`: the 16 `*.csproj.user`, `*.vcxproj.user` and `*.suo` files that were still tracked. `.gitignore` already lists `*.user` and `*.suo`. When this branch merges, Git removes these files from other checkouts.

### 6. "Required dlls": 32 unused files deleted (files only)

- `.NET Core/` (3) and `.NET Standard/` (10)
- `Microsoft.ML.*` (11) and `Microsoft.DirectX*` (3)
- `MSVCR71.dll`, `PasswordVaultWrapper.dll` (a stale copy; the project builds it), `RdlDesigner.exe`, `System.Net.Http.dll` and `freeglut.dll`

No remaining project names any of them in a HintPath, Content or None item, and no wildcard covers the folder. Nothing loads them by name, and none of them was in `OpenDental/bin/Debug`.

No `<Reference>` item was changed (package BIN-B owns those). 79 tracked files remain.

### 7. Source files no project compiles: 64 deleted

A script checked every project file in the repository: all item types, Include and Update, linked items, wildcards and SDK-style folders. It found these `.cs` and `.resx` files in no project:

- **OpenDental (52):** the Anesthesia windows and `ContrAnesthesia(Data)`, `FormApptEditOld`, `FormClaimInstEdit`, `FormLanguageDefs`, `FormPatList2014` and its two relatives, `FormScannerSetup`, `Tables Old Grids/TableClaimPay`, `UI/PrintPreview`, and the stray `UserControlTreatPlanInsHistory.resx`.
- **Elsewhere (6):**
  - `CodeBase/Utilities/ResponseListHandler.cs`
  - `OpenDentalGraph/Cache/DashboardCacheHQMessages.cs`
  - `UnitTests/UnitTests/ProcedureButtonTests.cs`
  - `UnitTestsCore/TableTypes/ProcButtonQuickT.cs`
  - RdlDesign's `Copy of FiltersCtl` (.cs and its .resx)
- **OpenDentBusiness (6, INFERRED, below):**
  - stale copies of `Crud/EServiceBillingCrud.cs`, `Crud/EServiceCodeLinkCrud.cs` and `Crud/SmsBillingCrud.cs` (the compiled ones are under `InternalTools`)
  - `Data Interface/Jobs.cs`
  - `TableTypes/Signal.cs`
  - `Serializing/CareCreditWebResponse.cs` (namespace `OpenDentalWebCore`)

`OpenDentBusiness/Resources/rxnorm.zip` (1.8 MB) and its `<None>` item are deleted too. Nothing read it.

Not deleted: the 8 stale `RdlEngine/Definition/*.cs` files. The compiled copies are under `RdlEngine/Runtime`. The whole RdlEngine project is unbuilt and waits for the owner's RDL decision.

### 8. Tooling

- **`tools/run-tests.ps1`:** a committed copy of the runner in `.git/od-tools`. It behaves the same: modes, `-Include`, `-Out`, `-WaitOnly`, `-List` and the same machine-wide lock name, so it shares the lock with the old copy. Two changes:
  - `-Root` defaults to the repository that holds the script, and it stops with a message if the slnf is missing.
  - `vstest.console.exe` is found in the Build Tools 2022 folder, or through vswhere for other Visual Studio 2022 editions.
  - Checked: `-List` from a folder outside the repository found the 83 test assemblies.
- **`tools/DevDbTool`:** added to `OpenDental.Local.slnf`, and to `OpenDental.sln` (Debug|x86 and Release|x86, like the other tools), so every solution build compiles it. `New-DevDatabase.ps1` still builds it on its own. The comment in `DevDbTool.csproj` that said it was deliberately left out of the solution was updated.

### 9. Seed

`UnitTests/Resources/dump.txt` was not touched (package INIT-SQL).

### 10. Docs

`docs/decoupling-plan.md`:
- Ground rule 1 now records that the substitution is gone, and that the filter stays only because the runner and the host check read it.
- Phase 2 got a dated status line.
- `README.md` and `NOTICE` were not edited. The corrections REL-FILES should make are listed below.

## Decisions (INFERRED)

1. **DevDbTool is in `OpenDental.sln` too.** A solution filter can only list projects of its solution, so adding DevDbTool to the slnf needs a solution entry.
2. **`SparksToothChart/SparksToothChart.sln` is deleted** with TestForm. It was a Visual Studio 2005 solution whose other project, SparksToothChart, is built from `OpenDental.sln`. For `RdlViewer/RdlViewer.sln`, only the RdlReader entry was dropped, because the RDL projects wait for the owner.
3. **`Direct2dWrapper.vcxproj.user` was untracked as well.** It matches `*.user` in `.gitignore`, like the `*.csproj.user` files.
4. **Six uncompiled OpenDentBusiness files were deleted, beyond the review's list of 57.** The review's script skipped OpenDentBusiness because its csproj has wildcard items. These six are the same kind of file: in no project, and never touched since upstream 24.3.1. The count is 58 from the review (with "Copy of FiltersCtl.resx", its .cs file's companion) plus these 6, which makes 64.
5. **`FormClaimInstEdit.cs` was deleted, although a test read it.**
   - The A-IMG patch (2773306ef) had kept this never-compiled window in step "if it is ever brought back".
   - `tests/ImagingTests/WiringTests.cs` (`DeleteClaimElsewhere_PreAuthEobsGoFirst`) keeps its Send Claims checks and drops the ones on the deleted file. This is outside this package's files, so it is listed in outsideOwned.
6. **The System reference keeps an empty element.** Only the HintPath line was removed, which leaves a one-line hunk next to the references BIN-B may edit.
7. **The solution filter is kept.** The test runner and `Test-NoHqHosts.ps1` read it; it now matches the solution.

## For the integrator: merging `OpenDental.sln`

The local sln merge driver (`.git/od-tools/sln_merge_driver.py`) only removes project blocks. In configuration sections it adds back every line the other side has. A simulated merge of this branch with a branch that added one project (both directions, exit 0) behaved like this:
- The 5 dead entries stayed gone.
- The removed build-order dependency stayed removed.
- DevDbTool stayed.
- The `ODXam` configuration lines came back: 464 or 492 of them, depending on the direction.

After merging any branch that also changed `OpenDental.sln`, strip them again from Git Bash. Line endings are kept, and after this package no other line in the solution contains `ODXam`:

```bash
sed -i '/ODXam/d' OpenDental.sln
```

## README corrections for REL-FILES

- **Build section:**
  - Line 41: either `OpenDental.sln` or `OpenDental.Local.slnf` builds now; they list the same projects.
  - Lines 49-57 ("Why upstream doesn't build out of the box"):
    - No project is missing any more, and nothing is substituted from `Required dlls/`.
    - `Directory.Build.targets` only adds the MSTest v1 search path.
    - The filter no longer loads "the 21 projects": it loads all 109, the same as the solution.
  - Line 19 still says to merge newer upstream builds, which contradicts `docs/decoupling-plan.md` ground rule 1.
- **Running section:**
  - Line 78: DevDbTool is now in the solution and the filter, and the script still builds it on its own.
  - Line 113: drop the sentence about `FreeDentalConfig.xml` and `OpenDentServerConfig.xml` in the repository root; both files are deleted. The sample is `tools/FreeDentalConfig.dev.xml` (line 110).
- **Repository layout:**
  - Line 129: `tools/` is no longer "not in the solution". AdoptionValidation, SyntheticData and DevDbTool are in it, and the folder also holds `run-tests.ps1` (the test runner) and `Test-NoHqHosts.ps1`.
  - Line 130: `RequiredServerDlls/` is deleted.
  - Line 131: `MobileWeb/` and `TestCanada/` are deleted. The unbuilt projects left are `RdlDesign/`, `RdlEngine/`, `RdlViewer/`, `ODR/`, `DocumentationBuilder/` and `PluginExample/`.
  - Add a line for `tests/` (the fork's MSTest assemblies, run with `tools/run-tests.ps1`), and one for `ImagingCapture/` and `ImagingDeviceManager/`.

## Left as is (not this package)

- The `DOT_NET_STANDARD` `#if` blocks in CodeBase are dead now that CodeBaseStandard is gone. Removing them is a code change.
- `tests/M2AUDITTests/BirthdateSavePathTests.cs` still lists `TestCanada`, `DataClassesOld` and `OpenDentBusiness.Tests` as excluded folders. That is harmless.
- `docs/third-party-binaries-audit.md` and other docs still name some of the deleted files and folders. They are owned elsewhere.

## Owner questions

1. Delete `D:\Unversioned` on this PC? Earlier Debug builds of Direct2dWrapper created it, and nothing writes there any more.
2. Should the fork's merge driver drop configuration lines that one side removed (here `ODXam`)? Until then, the strip step above is needed after merges that touch `OpenDental.sln`.
3. Keep `OpenDental.Local.slnf`, or point the runner and the host check at `OpenDental.sln` and delete the filter?

## Tests

- **Full build:** `OpenDental.sln` by itself, Debug|x86, through the build lane, from a clean worktree: 0 errors. This covers all 109 projects, the same set as the filter, DevDbTool included.
- **Core:** `run-tests -Mode Core`: 79 assemblies, 5,333 tests, all passed. ImagingTests (43) passed with the adjusted wiring test.
- **Host check:** `tools/Test-NoHqHosts.ps1`, which now also scans `tools/DevDbTool`: passed (6,805 files in 107 projects, 26 allowed references).
- **Runner:** the committed `tools/run-tests.ps1 -List`, run from outside the repository, found the 83 test assemblies.
