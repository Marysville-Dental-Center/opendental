# Project baseline (stage 1 of the .NET 10 move)

The record `tools/Get-ProjectBaseline.ps1` took of the classic projects before any of them moved to the SDK project format
([docs/specs/net10-plan.md](../../docs/specs/net10-plan.md), S1-0 step 4): a Debug|x86 build of `OpenDental.Local.slnf` with
the Build Tools 2026 MSBuild 18.11, at the end of package S1-0. The script's help says what each file holds.

A package that converts projects compares a new record with this one:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Get-ProjectBaseline.ps1 -Out $env:TEMP\baseline-now -Against tools\project-baseline
```

The stage 1 gate allows no difference in `items.txt`, `resources.txt`, `configs.txt`, `config-texts.txt` and the file names
of `files.txt`, except the ones a package removes on purpose and names in its report. Sizes and SHA-256 values of built files
differ with every build (most classic projects do not compile deterministically) and are only listed.

The record was taken twice in S1-0, before and after the shared props files (`Directory.Build.props`,
`tests/Directory.Build.props`), `global.json` and `nuget.config` came in, with a full rebuild in between: the two records
match.
