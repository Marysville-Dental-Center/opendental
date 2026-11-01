# PRE-INTEGRATION: the seven packages of the run before the rename, merged

Branch `pre/integration`, from `main` (a88489992). The seven verified package branches were merged with `git merge --no-ff`, in this order:

| # | Branch | Package | Note |
|---|---|---|---|
| 1 | `pre/org-cleanup-1` | ORG-CLEANUP | [ORG-CLEANUP.md](ORG-CLEANUP.md) |
| 2 | `pre/init-sql-1` | INIT-SQL | [INIT-SQL.md](INIT-SQL.md) |
| 3 | `pre/bin-a-1` | BIN-A | [BIN-A.md](BIN-A.md) |
| 4 | `pre/bin-b-3` | BIN-B | [BIN-B.md](BIN-B.md) |
| 5 | `pre/twain-w-1` | TWAIN-W | [TWAIN-W.md](TWAIN-W.md) |
| 6 | `pre/rel-files-1` | REL-FILES | [REL-FILES.md](REL-FILES.md) |
| 7 | `pre/ren-proof-2` | REN-PROOF | [REN-PROOF.md](REN-PROOF.md) |

All seven are merged; none was reverted. No feature or test of any package was dropped.

## Conflicts and how they were resolved

- **`OpenDental.sln`, after every merge that changed it** (INIT-SQL, BIN-A, BIN-B, TWAIN-W): the union merge driver added back the `ODXam`
  configuration lines (458 to 462 of them) and the 40 configuration lines of the five dead projects that ORG-CLEANUP removed (`xEHR`,
  `OpenDentalWebCore`, `OpenDentalSocket`, `OpenDentBusiness.ODXam`, `OpenDentBusiness.WebApis`). Both kinds were stripped again in the
  merge commit, CRLF kept: every configuration line whose project GUID has no `Project(...)` entry, and every line naming `ODXam`.
  After the last merge the solution and `OpenDental.Local.slnf` list the same 113 projects. (ORG-CLEANUP's `sed -i '/ODXam/d'` converted the
  file to LF in Git Bash here, so a small script did it instead.)
- **`tools/DevDbTool/DevDbTool.csproj`** (INIT-SQL against ORG-CLEANUP): both put DevDbTool in the solution and rewrote the header comment that
  says so. The comment now says both: the solution build compiles it and builds it for the regeneration test.
- **`OpenDental.csproj`, `OpenDentBusiness.csproj`, `UnitTests.csproj`** (BIN-B against BIN-A): adjacent `<Reference>` items removed on both
  sides (BIN-A: Sparks3D, xImageDeviceManager; BIN-B: SigPlusNET, RS232, zxing, zxing.presentation). Both removals kept.
- **`OpenDental/Forms/FormLicense.cs`, `FormRegistrationKey.cs`** (REL-FILES against BIN-A): BIN-A dropped the Sparks3D line of the old list;
  REL-FILES replaced the list with `LicenseList`, which has no Sparks3D entry either. REL-FILES' files taken as they are.
- **`README.md`** (REL-FILES against INIT-SQL): INIT-SQL's sentence about the solution build building DevDbTool and its `--export-init-sql`
  command is kept in REL-FILES' rewritten section.
- **`OpenDentHL7/ServiceHL7.cs`** (REN-PROOF against REL-FILES): the database version check compares with `ForkVersions.CompatVersion`
  (REL-FILES) and writes its Event Log entry under `ProgramNames.EventLogSourceHL7` (REN-PROOF).

## Integration commits

1. **README after ORG-CLEANUP** (REL-FILES wrote it from `main`, before the deletions): the solution and the filter list the same projects;
   `Directory.Build.targets` substitutes nothing any more; `tools/` also holds `run-tests.ps1`, `New-InitSql.ps1` and `Test-NoOldNames.ps1`;
   `MobileWeb/` and `TestCanada/` are gone from the list of unbuilt projects, `DocumentationBuilder/` and `PluginExample/` are added. Also two
   inert `None` items for files that have not existed since upstream 24.3.1 (`OpenDental.csproj` `Resources\Icon1.ico`,
   `OpenDentBusiness.csproj` `..\.editorconfig`), found by the dangling-reference check.
2. **ReleaseArchiveTests (BIN-B) and the special folder BIN-A removed**: BIN-B's archive tests published a `Sparks3D` special folder, which
   BIN-A removed from `ForkReleases.ListSpecialDirs`. Two tests failed in the Full run. They now use `OpenDentalReplicationService`, the special
   folder that remains, as BIN-A already did in `ReleaseChannelDbTests`. What they check is unchanged.
3. **Repository-root locators** (REN-PROOF's rule across the other packages): `tests/BINATests/RepoFiles.cs`, `tests/BINBTests/TestSupport.cs`
   and `tests/AttributionTests/ReleaseIdentityTests.cs` were written at the same time as REN-PROOF and still found the root by
   `OpenDental.sln`. They now look for `LICENSE` and `NOTICE`, like every other test project.

## Checks

- **Project files** (script over the 120 tracked project files, 113 in the solution): no item or HintPath names a missing file, apart from
  build output (`OpenDentalCloud\bin\Debug`, `OpenDental\bin\Debug`) and the NuGet `packages/` folder, and in the unbuilt `ODR` and `RdlEngine`
  a `MySql.Data.dll` that `main` does not have either.
- **Removed binaries** (xImageDeviceManager, SuniUSB, VBbridges, Sparks3D, SigPlusNET, Interop.Word, Interop.Microsoft.Office.Core,
  Interop.VBIDE, stdole, AxInterop.AcroPDFLib, Interop.AcroPDFLib, Interop.Acrobat, Interop.AFORMAUTLib, zxing, Avalara, RS232, Ionic.Zip,
  a prebuilt ImagingDeviceManager.dll, EZTwain, VideoCamLib, MediaFoundation): no project references any of them, none is in
  `Required dlls/` or anywhere else in the tree. Source names them only in comments.
- **Output** `OpenDental\bin\Debug` after the full build: none of the removed binaries; `Hunspellx86.dll` and `WebView2Loader.dll` present,
  byte copies of `Required dlls`; `ImagingDeviceManager.dll` is the one built from `ImagingDeviceManager/`.
- **Full build** of `OpenDental.Local.slnf` through the build lane: 0 errors.
- **`run-tests -Mode Full`** (`.git/od-tools/test-results/pre_integration.txt`): 87 assemblies, 5,684 tests, 5,680 passed, 2 failed
  (ReleaseFlowTests, the archive tests of integration commit 2), M201Tests 2 inconclusive as on earlier heads. After commits 2 and 3:
  Targeted run of AttributionTests 32/32, BINATests 23/23, BINBTests 40/40, ReleaseFlowTests 89/89, NoHqTests 53/53
  (`pre_integration_fix.txt`).
- **`tools/Test-NoHqHosts.ps1`**: passed (6,839 files in 111 projects, 26 allowed references).
- **`tools/Test-NoOldNames.ps1`** (report mode): 26,983 old names on 26,385 lines in 5,775 files outside the allow-list, 3,379 allowed
  (8,095 files scanned); exit 0.
- **Migration check**: DevDbTool built through the lane, `tools/New-DevDatabase.ps1 -Name preint_<digits> -Force -SkipBuild -NoAtoZ`:
  exit 0, 16 fork steps, fork schema level 2026110104, schema of Open Dental 26.1.70 (14 documented exceptions), 441 MyISAM tables. Dropped.
- **Adoption rehearsal**: AdoptionValidation built through the lane, `Invoke-AdoptionRehearsal.ps1 -Size medium -Runs 1 -Prefix
  synth_preint<digits>_ -SkipBuild`: PASS, 31 of 31 checks. Its databases were dropped by the tool; none is left.

## Owner questions

1. The sln merge driver adds back configuration lines that one side removed (ORG-CLEANUP's owner question 2). Every merge here needed the strip
   step; until the driver drops them, every later merge that touches `OpenDental.sln` will too.

The packages' own owner questions are in their notes.
