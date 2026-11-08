# Project baseline (stage 1 of the .NET 10 move)

The record `tools/Get-ProjectBaseline.ps1` took of the classic projects before any of them moved to the SDK project format
([docs/specs/net10-plan.md](../../docs/specs/net10-plan.md), S1-0 step 4): the tree at the end of package S1-0 (every project still
classic), after a full Debug|x86 build of `OpenDental.Local.slnf` and before any test run. The script's help says what each file
holds.

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

How this record was taken and checked:

- The build was the build lane's, which still runs the Build Tools 2022 MSBuild 17.14 until the lead switches it to the Build Tools
  2026; the script evaluated the items with the Build Tools 2026 MSBuild 18.11. The S1-0 package had built the same tree with
  MSBuild 18.11 and recorded it (before and after the shared props files, `global.json` and `nuget.config` came in, with a full
  rebuild in between; those two records matched). This record equals that one in every gated file, once the absolute worktree path
  the first version of the script left in it is taken out, except two `Logger` files a test run had left; only sizes and SHA-256
  values differ (3,144 files: `.dll`, `.exe`, `.pdb`).
- A second record of the same tree, taken through another path to it, matches this one, sizes and SHA-256 values included.
