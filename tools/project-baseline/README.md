# Project baseline (stage 1 of the .NET 10 move)

The record `tools/Get-ProjectBaseline.ps1` takes of every C# project of `OpenDental.Local.slnf`
([docs/specs/net10-plan.md](../../docs/specs/net10-plan.md), S1-0 step 4). It was first taken of the classic projects at the end of
package S1-0, before any of them moved to the SDK project format, and is re-recorded after each merge that changes projects, always in
a clean worktree after a full Debug|x86 lane build and before any test run (test runs leave files in the output folders that are not
build output). The script's help says what each file holds.

No line holds an absolute path, so a record taken in another worktree or checkout compares with this one: paths inside the repository
are relative to it (the linked sources of the third-party wrappers included), paths inside the Program Files, Windows, local app data,
temp or user profile folders start with a placeholder such as `<ProgramFiles(x86)>\`, and the script stops before writing a file that
would still hold the repository's path or any other drive path. The `Logger` folder of each output folder is left out: the programs
and tests write it at run time, with the date in its file names.

A package that converts projects compares a new record with this one:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Get-ProjectBaseline.ps1 -Out $env:TEMP\baseline-now -Against tools\project-baseline
```

The stage 1 gate allows no difference in `items.txt`, `resources.txt`, `configs.txt`, `config-texts.txt` and the file names
of `files.txt`, except the ones a package removes on purpose and names in its report. Sizes and SHA-256 values of built files
differ with every build (most classic projects do not compile deterministically) and are only listed.

## This record: after the first wave of stage 1 (2026-11-08)

Taken after S1-T, S1-2 and S1-1 had merged and the lead's sweep after them (the 2003 MSBuild namespace removed from the SDK-style
project files; `AutoGenerateBindingRedirects` no longer turned off for SDK-style projects): 25 projects are SDK-style (the 18 of
S1-1, RdlDesign among them, and the 7 wrappers of S1-2), the rest still classic. It was taken in a clean worktree (`git clean -fdXq`)
after a cold full lane build with the Build Tools 2026 MSBuild 18.11, before any test run. Against the previous record (`bffa11c7e`,
every project classic) every difference falls into one of these groups:

| Group | `items.txt` | `resources.txt` | `configs.txt` | `files.txt` names |
|---|---|---|---|---|
| S1-T: the new `tests/ProjectFileTests` project | 8 new items | none (no resources) | 1 new (`ProjectFileTests.dll.config`, the same text as the other test configs, so `config-texts.txt` is unchanged) | 4 new (its `bin\Debug`) |
| S1-T: `ProjectFile.cs` and `BuildOutput.cs` linked into test projects | 38 new `Compile` items in 31 test projects | | | |
| S1-1: `SubType` metadata dropped (only that) | 349 items of 9 projects | | | |
| S1-1: new `None` items from the SDK's default globs | 2 (`RdlEngine/RdlEngineConfig.xml`, `xCrudGenerator/updateCode.bat`) | | | |
| S1-1: `packages.lock.json` as a `None` item | 17 (the S1-1 projects other than RdlDesign, whose lock file is in the next row) | | | |
| S1-1: RdlDesign, new in the slnf | 143 new items (its lock file included) | 56 new | | |
| S1-2: the 7 wrappers | none | none | none | none |
| The sweep: the binding-redirect default | none | none | none | none |

Only sizes and SHA-256 values differ otherwise (3,106 files: `.dll`, `.exe`, `.pdb`): 2,223 are copies of the 25 SDK-style
assemblies and their PDBs, which the conversions changed and whose Mono.Cecil fingerprints the packages compared, and 883 are
assemblies of classic projects, which do not compile deterministically.

## How the first record was taken and checked

- The build was the build lane's, which still ran the Build Tools 2022 MSBuild 17.14 until the lead switched it to the Build Tools
  2026; the script evaluated the items with the Build Tools 2026 MSBuild 18.11. The S1-0 package had built the same tree with
  MSBuild 18.11 and recorded it (before and after the shared props files, `global.json` and `nuget.config` came in, with a full
  rebuild in between; those two records matched). This record equals that one in every gated file, once the absolute worktree path
  the first version of the script left in it is taken out, except two `Logger` files a test run had left; only sizes and SHA-256
  values differ (3,144 files: `.dll`, `.exe`, `.pdb`).
- A second record of the same tree, taken through another path to it, matches this one, sizes and SHA-256 values included.
