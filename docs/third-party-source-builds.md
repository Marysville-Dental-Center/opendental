# Third-party libraries built from source

PDFsharp/MigraDoc 6.5.0 and WeCantSpell.Hunspell 7.0.1 are built from their upstream source as part of the solution. The repository does not copy that source: each library is a **git submodule** pinned to its release commit, and this project's own small project files, outside the submodules, compile it. An upgrade moves a pin. The owner decided this on 2026-11-02: "Better if you reference the GitHub project instead of copying. This is to make future upgrades easy."

Why from source:

- **PDFsharp/MigraDoc 6.5** are under the GNU AGPL 3.0. Their NuGet build calls `PdfSharpLicense.CheckLicense` (in the `PdfDocument`, `PdfReader` and `PdfWriter` constructors), and that wrapper passes every call to `PdfSharpLicenseImpl` in `PdfSharp.License.dll`, a library empira publishes only prebuilt. Its source is not public, so the Corresponding Source of a program that ships it could not be given (GPL 3.0 and AGPL 3.0, sections 1 and 6). Building from source with one file replaced (below) removes that library.
- **WeCantSpell.Hunspell** is used under its LGPL 2.1-or-later option, which asks whoever ships the binary to offer its source. Building it from the pinned source, and publishing that source with every release, does that.

## The submodules

| Path | Upstream (`.gitmodules`) | Tag | Pinned commit | What the build uses |
|---|---|---|---|---|
| `third-party/PDFsharp` | https://github.com/empira/PDFsharp.git | `v6.5.0` | `96d5ef81d304d314228712e28e5d7ac3d5e29f86` | `src/foundation/src`: PdfSharp.Shared, PdfSharp.System, PdfSharp + PdfSharp-gdi, PdfSharp.Charting, MigraDoc.DocumentObjectModel, MigraDoc.Rendering + MigraDoc.Rendering-gdi; `src/StrongnameKey.snk` |
| `third-party/WeCantSpell.Hunspell` | https://github.com/aarondandy/WeCantSpell.Hunspell.git | `7.0.1` | `ab5709d95b2d23541984d22baa0ab2d1e783582f` | the library folder `WeCantSpell.Hunspell/` only (not its tests, benchmarks, samples or its own submodules `hunspell-origin` and `samples`) |

Nothing inside a submodule is ever changed. `tests/PdfLibraryTests` and `tests/LibSpellTests` check that `.gitmodules` names these GitHub addresses and that the pins are the tags' commits.

**Getting them.** A clone from GitHub: `git clone --recurse-submodules <address>`, or `git submodule update --init` after a plain clone. Without them the build stops with "The upstream source is missing" (`third-party/build/SourceBuild.targets`).

**Offline, on the development PC.** The developer PC keeps local mirrors, bare clones of the two upstream repositories in the main checkout's git folder: `.git/od-tools/mirrors/PDFsharp.git` and `.git/od-tools/mirrors/WeCantSpell.Hunspell.git` (not part of the repository). The repository's own git configuration (`.git/config`, which every worktree shares) points the submodules there:

```
submodule.third-party/PDFsharp.url = file:///<main checkout>/.git/od-tools/mirrors/PDFsharp.git
submodule.third-party/WeCantSpell.Hunspell.url = file:///<main checkout>/.git/od-tools/mirrors/WeCantSpell.Hunspell.git
```

while `.gitmodules` keeps the GitHub addresses. Any checkout or worktree then fetches them with no internet:

```
git -c protocol.file.allow=always submodule update --init
```

(`protocol.file.allow=always` because git refuses `file://` transports for submodules by default.) Never run `git submodule sync`: it copies the GitHub addresses from `.gitmodules` over the mirror addresses. A worktree that holds submodules can only be removed with `git worktree remove --force`.

## How the build works

`third-party/build/` holds one classic project file per assembly, in `OpenDental.sln` and `OpenDental.Local.slnf`, built by the normal build (the build lane builds them with everything else; `PdfSharp-gdi`, `WeCantSpell.Hunspell` and so on are also solution targets of their own):

| Project | Assembly | Compiles (from the submodule) |
|---|---|---|
| `PdfSharp.Shared` | PdfSharp.Shared.dll | every `.cs` of `shared/src/PdfSharp.Shared` |
| `PdfSharp.System` | PdfSharp.System.dll | every `.cs` of `shared/src/PdfSharp.System` |
| `PdfSharp-gdi` | PdfSharp-gdi.dll | every `.cs` of `PDFsharp/src/PdfSharp` except what upstream's `PdfSharp-gdi.csproj` leaves out (`Forms/**`, `Windows/**`, `Internal/Png/BigGustave/**`, `Fonts/CoreBuildFontResolver.cs`, `Fonts/WindowsPlatformFontResolver.cs`) and **except `root/PdfSharpLicense.cs`**; every `.cs` and `.resx` of `PDFsharp/src/PdfSharp-gdi`; `PdfSharp/Resources/**` (the sRGB profile); `PdfSharp.System/System/CompilerServices.cs` and `CodeAnalysis.cs` |
| `PdfSharp.Charting-gdi` | PdfSharp.Charting-gdi.dll | every `.cs` of `PDFsharp/src/PdfSharp.Charting` |
| `MigraDoc.DocumentObjectModel` | MigraDoc.DocumentObjectModel.dll | every `.cs` of `MigraDoc/src/MigraDoc.DocumentObjectModel`; `PdfSharp.System/System/CompilerServices.cs` |
| `MigraDoc.Rendering-gdi` | MigraDoc.Rendering-gdi.dll | every `.cs` of `MigraDoc/src/MigraDoc.Rendering` except `Rendering.Windows/**`, `Rendering.Forms/DocumentPreviewWpf.cs` and `Rendering.Printing/MigraDocPrintDocument.cs`; every `.cs` and `.resx` of `MigraDoc/src/MigraDoc.Rendering-gdi`; `PdfSharp.System/System/CompilerServices.cs` |
| `WeCantSpell.Hunspell` | WeCantSpell.Hunspell.dll | every `.cs` of `WeCantSpell.Hunspell/` |

The item lists use **wildcards with explicit excludes**, so a file upstream adds is compiled unless it is excluded; the excludes are exactly those of the upstream `-gdi` project files. After each build `obj\...\SourceBuildItems.txt` lists what was compiled and embedded, and `tests/PdfLibraryTests` (`SourceBuildTests`) and `tests/LibSpellTests` compare it with what the upstream project files compile (default globs, `Compile Remove`, linked files), so a difference after an upgrade fails a test.

What the build adds, because the upstream project files are SDK style and are not used (no NuGet restore, no internet, no GitVersion, SourceLink or package analyzers):

- **Settings** (`SourceBuild.props`, `PDFsharp.props`): .NET Framework 4.8, x86 like the program, nullable on, the upstream compiler symbols. The -gdi assemblies and MigraDoc.DocumentObjectModel compile with the symbols the .NET SDK sets for upstream's `net462` build, PdfSharp.Shared, PdfSharp.System and WeCantSpell.Hunspell with those of its `netstandard2.0` build (with WeCantSpell's `NO_...` list), so the same code paths are taken; PDFsharp also gets `USE_LONG_SIZE;TEST_CODE_xxx` from upstream's `src/Directory.Build.targets`. Debug and Release compile the same code (no `DEBUG` symbol, which would change PDFsharp's Producer string): upstream's Release paths, so a Debug build of the program behaves like the release. C#: `latest` for PDFsharp; `preview` for WeCantSpell.Hunspell, whose `LangVersion` 14.0 (the `field` keyword, null-conditional assignment) this compiler offers as preview.
- **Assembly attributes** (`SourceBuild.targets`, written to `obj\...\SourceBuildInfo.g.cs`): title, product, company, copyright, the **fixed versions** of the props file, an **informational version that marks this build** (`6.5.0+96d5ef81d304.open-pms-dental-source-build`, `7.0.1+ab5709d95b2d.open-pms-dental-source-build`), `AssemblyMetadata` `SourceRepository`, `SourceTag`, `SourceCommit` and `SourceBuild`, and the `InternalsVisibleTo` attributes, read from the upstream project file at each build. PDFsharp also gets the `GitVersionInformation` class GitVersion would generate; its `InformationalVersion` stays `6.5.0`, as in empira's files, because it goes into every PDF's Producer ("PDFsharp 6.5.0-gdi").
- **Identities.** The PDFsharp assemblies are signed with upstream's `src/StrongnameKey.snk`, so their names, version 6.5.0.0 and public key token `f94615aa0424f9eb` are empira's and nothing that referenced the NuGet files changes. WeCantSpell.Hunspell is not strong-named, as upstream's is not; its assembly version is 7.0.0.0, upstream's (MinVer gives the major version), file version 7.0.1.0.
- **Global usings** (`PDFsharp/GlobalUsings.cs`, `GlobalUsings.WindowsForms.cs`): what upstream's `ImplicitUsings` generates (and, for PdfSharp-gdi and PdfSharp.Charting-gdi, its two Windows Forms namespaces).
- **Logging source generator output** (`PdfSharp-gdi/LoggerMessages.cs`, `MigraDoc.DocumentObjectModel/LoggerMessages.cs`): the bodies of upstream's `[LoggerMessage]` methods, which the analyzer of the Microsoft.Extensions.Logging.Abstractions NuGet package writes for upstream. A new `[LoggerMessage]` method upstream fails the build (CS8795) until it is added there. The program sets no logger factory, so nothing is logged.
- **Polyfills:** none of our own. WeCantSpell.Hunspell's netstandard2.0 build brings its own (`HashCode.cs`, the `NO_...` paths), and PDFsharp links its `PdfSharp.System/System/*.cs`; the support assemblies come from `Required dlls` (Microsoft.Extensions.Logging.Abstractions, System.Memory, System.Buffers, System.Runtime.CompilerServices.Unsafe).

The projects that used `Required dlls` HintPaths now have ProjectReferences to these projects, and the replaced DLLs left `Required dlls`: PdfSharp-gdi, PdfSharp.Charting-gdi, PdfSharp.Shared, PdfSharp.System, MigraDoc.DocumentObjectModel, MigraDoc.Rendering-gdi, WeCantSpell.Hunspell and `PdfSharp.License.dll`. `tools/Build-Release.ps1` ships them from `third-party\build\<project>\bin\Release` and denies `PdfSharp.License.dll`.

empira's NuGet files were built from a commit that is not public (their informational version is `6.5.0+9c879a4019d8...`); this build is from the public tag. When this was set up, the public API of each assembly was compared with empira's 6.5.0 NuGet file: the same types and members, apart from `PdfSharp.License.PdfSharpLicenseException`, which is now in PdfSharp-gdi.dll.

## The one replaced PDFsharp file

`src/foundation/src/PDFsharp/src/PdfSharp/root/PdfSharpLicense.cs` is not compiled. `third-party/build/PdfSharp-gdi/PdfSharpLicense.cs` is compiled in its place. It is a **modified version of empira's AGPL wrapper**: it keeps empira's copyright and licence notice and carries a dated modification notice (AGPL section 5a; NOTICE and THIRD-PARTY-NOTICES.txt say so). It has the same public API (`PdfSharpLicense.LoadLicense`, `SetAgplLicense`, and the internal `ResetLicense`, `HasValidLicense`, `CheckLicense`) and holds `PdfSharpLicenseImpl` itself, behaving exactly like the prebuilt library on the AGPL path, as observed from outside on 2026-11-02:

- No key, no network connection, no message; nothing is read from a file, the registry or the environment.
- Before `SetAgplLicense()`, `CheckLicense()` throws `PdfSharp.License.PdfSharpLicenseException` ("The loaded license is not valid."), so `PdfDocument`, `PdfReader` and `PdfWriter` refuse to work, as before. After it, they work (`PdfSharpSetup.Init` calls it in every program).
- Every PDF saved names the licence as before, through PDFsharp's `PdfDocument.PrepareForSaveCallback`: "(AGPL license)" after each mention of the library in the Producer ("PDFsharp 6.5.0-gdi (AGPL license)", also inside "(Original: ...)") and in a Creator PDFsharp or MigraDoc set ("PDFsharp 6.5.0 (www.pdfsharp.com) (AGPL license)", "empira MigraDoc 6.5.0 (www.pdfsharp.com) (AGPL license)"); a Creator the application set is left alone.
- `LoadLicense` refuses every key and leaves the licence as it was: nothing that needs empira's commercial licence is ever enabled.

`tests/PdfLibraryTests` checks all of this.

## The source archive

Every release publishes its Corresponding Source next to the setup: `tools/Build-Release.ps1` writes `OpenPmsDental-<version>-source.zip`, the repository at the release commit with every submodule at its pinned commit (`git archive`) and `SOURCE-COMMITS.txt`, so the source of everything shipped stays available even if an upstream repository goes away ([release-process.md](release-process.md)). The script refuses to build when a submodule is not checked out at its pin.

## Upgrading

1. **Get the new tag.** Online, in the submodule: `git -C third-party/PDFsharp fetch --tags https://github.com/empira/PDFsharp.git`. Offline, first fetch it into the mirror on a computer that has internet, for example `git --git-dir <main checkout>/.git/od-tools/mirrors/PDFsharp.git fetch https://github.com/empira/PDFsharp.git 'refs/tags/*:refs/tags/*'`, then `git -C third-party/PDFsharp fetch --tags`. Read the release notes and the licence (`LICENSE`) of the new version.
2. **Move the pin.** `git -C third-party/PDFsharp checkout --detach <tag>`, then `git add third-party/PDFsharp` in the repository. (WeCantSpell.Hunspell the same way, `third-party/WeCantSpell.Hunspell`.)
3. **Update the recorded version** in `third-party/build/PDFsharp.props` (`SourceBuildTag`, `SourceBuildCommit`, `SourceBuildCommitDate`, `SourceBuildVersion`, the assembly, file and informational versions, `SourceBuildGitInformationalVersion`, `SourceBuildGitBranchName`, the copyright year), or the same properties in `third-party/build/WeCantSpell.Hunspell/WeCantSpell.Hunspell.csproj`, and the commits and versions in the tests (`SourceBuildTests`, `SpellLibraryFilesTests`), THIRD-PARTY-NOTICES.txt, NOTICE and this file.
4. **Compare the upstream project files** with the old tag: `git -C third-party/PDFsharp diff v6.5.0 <tag> -- "*.csproj" "src/Directory.Build.*"`. A new excluded file (`Compile Remove`, a file the -gdi project no longer links), new symbols (`DefineConstants`), a new package reference (its DLL must come into `Required dlls`) or a new linked file goes into `third-party/build`. The `InternalsVisibleTo` lists follow by themselves.
5. **Compare the replaced file** with the new upstream wrapper: `git -C third-party/PDFsharp diff v6.5.0 <tag> -- src/foundation/src/PDFsharp/src/PdfSharp/root/PdfSharpLicense.cs`, and the callers of `PdfSharpLicense` and of `PdfDocument.PrepareForSaveCallback`. Carry every API change into `third-party/build/PdfSharp-gdi/PdfSharpLicense.cs`, keep empira's copyright, and update its modification notice (date, upstream version and commit). Before the upgrade, record how empira's prebuilt file behaves on the AGPL path (the Producer and Creator strings, the exception), and keep the stand-in and its tests equal to it.
6. **Build** through the build lane (the whole solution filter). A new `[LoggerMessage]` method shows up as CS8795: add its body to the `LoggerMessages.cs` of that project.
7. **Run the tests**: PdfLibraryTests and LibSpellTests (pins, project files against the upstream ones, assembly identities and build marker, the replaced file, PDF output, spell checking, the source archive), then LibraryBindingTests, AttributionTests, ReleaseFlowTests, M2SHEETSTests, BPRINTTests and the rest.
8. Update the licence texts if upstream changed them (`OpenPmsDental/Resources/PdfSharp.txt`, `MigraDoc.txt`, `WeCantSpellHunspell.txt`) and the owner questions if the licence changed.
