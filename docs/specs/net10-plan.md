# Open PMS - Dental: stage plan for the move from .NET Framework 4.8 to .NET 10

**What this is.** The committed stage plan for moving every project of Open PMS - Dental from .NET Framework 4.8 to .NET 10. Package
briefs are cut from it: each package names the files it owns, its work, its tests and its place in the merge order.

**Date.** 2026-11-08. **Status.** Decided with the owner; stage 0 next. Revised on 2026-11-08 by the owner's standing rule and its
correction (section 11): nothing is cut except what the owner deliberately removed. Revised again on 2026-11-08 by the owner's decision
to go directly to 64-bit (decision 19): stage 3b switches to net10.0-windows x64, and stage 5 is folded into 3a and 3b. Revised a third
time on 2026-11-08 by the owner's choice of self-contained deployment (decision 20): the program and the 32-bit helper carry their own
.NET 10 runtime, and the setup installs none.

**Basis.** A read-only survey of every project (project files, APIs, user interface, libraries, tooling, Middle Tier, runtime behaviour)
and an independent critique of that survey. Nothing was built, run, restored or downloaded for it. File and line references are from
`main` at `e2251ea96` (the survey) and were re-checked at `8018a815f` for the stage 0 additions. Section 11 lists the owner's decisions of
2026-11-08, the standing rule first; section 12 the open points (O-1, O-2, O-6, O-7 and O-9 were answered and O-4 resolved on 2026-11-08;
O-10 is new; O-14 to O-16 come with decision 19, and O-15 was answered the same day, decision 20; O-17 came with the S2-1 review on
2026-11-09), and the appendix where each finding
of the critique is handled. The facts behind decision 19 were checked read-only at `c9463cad1` and on the build PC on 2026-11-08, and
those behind decision 20 on the build PC the same day (section 9.1).

**Ground rules (owner decisions of 2026-11-07, not reopened).** Everything moves to .NET 10, the Middle Tier included (ASP.NET Core on
Windows; Linux deferred); no dual-targeting; self-contained (changed on 2026-11-08 from "framework-dependent; the setup installs the
.NET 10 Desktop Runtime": every shipped exe carries its own .NET 10 runtime, and the setup installs none, decision 20); full 64-bit
(changed on 2026-11-08 from "x86 until stage 5": the switch goes straight to x64, with no 32-bit .NET 10 build, decision 19); the tests
leave MSTest v1 (for MSTest 4, decision 3); NuGet from a local offline folder with lock files, replacing `Required dlls` where a package
exists; SOAP clients of cut services are dropped and the rest replaced; BinaryFormatter is scanned for and replaced; AppDomain tests are
rewritten; COM interop stays; the office does not use QuickBooks Desktop (QBFC), Schick CDR or Topaz (a fact only: since the owner
correction of 2026-11-08 all three are kept and ported, through a 32-bit helper program or a 64-bit replacement, S3a-13 and S3a-14).

---

## 1. The plan in one page

| Order | Stage | Target | What it achieves | Size (rough) |
|---|---|---|---|---|
| 1 | **0. Clean-up and seams** (new) | net48, classic projects | Ports the Middle Tier mock, e-mail and ODThread tests out of the legacy UnitTests project and makes the rest of it a running suite; moves implicit-TLS sending from CDO to MailKit and received mail to MimeKit; removes dead code and the unused ADODB, CDO and System.Web references; rebuilds the EOB and amendment window on the current Imaging code and removes the old 2020 Imaging module; adds the Middle Tier host seam. The removals of S0-2a and of Direct are withdrawn and reverted (section 11). Every later diff gets smaller. | 8 packages (S0-2a withdrawn), M; size under revision |
| 2 | **1. SDK-style project files** | net48 | The build lane and test runners move to Visual Studio Build Tools 2026 (MSBuild 18) first; then every C# project left after stage 0 becomes SDK-style, built with the pinned .NET SDK 10; the output keeps the same layout and files. | 8 packages, L, 9-13 dev-days |
| 3 | **2. NuGet replaces Required dlls** | net48 | 26 identical DLLs become PackageReferences (no download); MySqlConnector 2.6.2, the WebView2 package and the 10.0 versions of three shipped Microsoft packages come in; the unused `System.Security.Cryptography.Pkcs` leaves; lock files everywhere. | 4 packages, M, 3-5 dev-days |
| 4 | **4. MSTest 4** (runs before 3a) | net48 | Every test project leaves MSTest v1, which cannot run on .NET. | 2 packages, M, 2-4 dev-days |
| 5 | **3a. Prepare the code on net48** | net48 | Every code change that also works on .NET Framework: menus, Thread.Abort, shell launching and elevation, ANSI encodings, number formats, PasswordVault, SOAP clients, services, test rewrites, golden outputs, a setup without per-user mode; and the 64-bit preparation (decision 19): pointer-size-safe native code and registry views with x64 test runs, the 32-bit helper program for QuickBooks Desktop, Schick CDR, Topaz and Guru, the tooth chart engines on x64, an x64 build of `Direct2dWrapper`. Ends with the **last .NET Framework release ("bridge release")**. | 19 packages, XL, 30-47 dev-days |
| 6 | **3b. Switch to net10.0-windows x64** | net10.0-windows x64 | One integration branch retargets everything straight to 64-bit (no 32-bit .NET 10 build), adds the new Middle Tier, release and setup changes (the 64-bit install folder, the self-contained x64 runtime, published as its own part of a release, the 64-bit TWAINDSM); merges to `main` once. The office then installs the x64 setup once on every workstation. | 8 packages, XL, 19-31 dev-days |
| 7 | **5. x64** (folded into 3a and 3b on 2026-11-08, decision 19) | - | No separate stage: the 64-bit work that runs on .NET Framework moved into 3a (S3a-12 to S3a-16), the rest into 3b, and the 64-bit prerequisites became entry conditions of 3b. | none |

Each stage ends with a Full test run and a Release dry run (section 3). The effort figures are rough developer-day ranges for orientation
only; the Middle Tier figure (5-6.5 days) comes from the Middle Tier survey, the rest are estimates from the counts in the surveys.

### 1.1 Changes to the agreed stages, and why

1. **The MSTest stage (stage 4) runs before the retarget (stage 3), and targets MSTest 4.** MSTest v1
   (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`) is a .NET Framework assembly found in the Visual Studio install; it cannot run
   on .NET. 112 `.csproj` files reference it today. Moving the tests first, while still on net48, means the retarget is checked by an
   unchanged test framework. MSTest 3.x is a deprecated line on nuget.org; the current 4.5.x line still supports .NET Framework 4.6.2+
   (decisions 2 and 3).
2. **New stage 0 (clean-up and seams).** The surveys found dead or unused items that would otherwise be converted, retargeted and tested
   for nothing: two COM references (ADODB unused, CDO replaceable by the already-shipped MailKit; they also tie the build to
   MSBuild.exe, MSB4803); unused `System.Web.*`/`System.ServiceModel.*` usings that become compile errors; 16 orphaned BinaryFormatter
   `.resx` entries; 8 stale RdlEngine files that SDK globbing would compile as duplicates; and the System.Web calls inside the shared
   libraries (12 uses in 5 files, S0-4) that block the business library from building for .NET. All of it works on today's build. The
   first draft also removed the legacy UnitTests project, RdlDesign, DocumentationBuilder, CentralManager, Direct messaging with its
   Health.Direct DLLs (CLR 2.0 binaries), the QBFC, Schick and Topaz bridges and the integrations the owner first cut. The owner's
   standing rule and its correction (2026-11-08, section 11) keep all of them. The legacy UnitTests project (1,944 active tests that no
   runner runs) becomes a running suite in stage 0 (S0-5); the ports of the kept integrations are to be planned (open point O-10).
3. **Stage 3 is split into 3a (on net48) and 3b (the switch).** Most fixes compile and behave the same on .NET Framework
   (ContextMenuStrip, cooperative cancellation, `UseShellExecute=true`, explicit ANSI code pages, the Credential Manager, hand-written SOAP
   calls, AppDomain-free tests). Landing them on net48 lets the existing Full suite and the running program prove them before the runtime
   changes, keeps the switch diff small, and gives one release (the bridge release) that ships them to the office on the known runtime.
   Golden outputs and crypto known answers must also be recorded from a net48 build, which is only possible before the switch.
4. **The build moves to Visual Studio Build Tools 2026 and the .NET SDK 10 in S1-0, before the first SDK-style project** (decision 1).
   The 9.0.3xx SDK band goes out of support on 2026-11-10
   (https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs,
   https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
   Under the MSBuild 17.14 of Build Tools 2022, SDK 10 downloads a compiler package for every SDK-style project and warns NETSDK1233;
   every SDK band after 10.0.1xx needs MSBuild 18; the vstest 17.14 of Build Tools 2022 lacks a fix the x86 .NET test host needs (no
   longer a reason since decision 19: no x86 .NET test host runs); and Visual Studio 2022 17.14 leaves mainstream support on 2027-01-13.
   Moving the lane and the test runners in S1-0, on the unchanged classic solution, keeps the toolchain change apart from the
   project-format change and avoids a second toolchain switch at 3b (section 2.4).
5. **Stage 3b goes straight to x64; stage 5 is folded into 3a and 3b** (owner, 2026-11-08: "no go directly to 64.", decision 19). The
   plan kept x86 at the switch and left 64-bit to a later stage 5. The lead offered a 32-bit .NET 10 build as an internal checkpoint,
   then the flip to 64-bit, with one office release; the owner chose to go directly. So there is no 32-bit .NET 10 build of the program,
   and the office gets one rollout: the bridge release (x86, end of 3a), then the .NET 10 x64 setup once on every workstation. The
   64-bit work that can be done and tested on .NET Framework moves into 3a as five packages (S3a-12 to S3a-16): pointer-size-safe native
   code and registry views, the 32-bit helper program for the kept 32-bit-only vendor components (two packages), the tooth chart engines
   on x64, and an x64 build of `Direct2dWrapper`. The rest is part of the switch (3b), and the 64-bit prerequisites, the TWAIN ones above
   all, become entry conditions of 3b.

---

## 2. Rules that apply to every stage

### 2.1 Keeping `main` buildable at every commit

- **One writer per package, one worktree per package**, cut from the current `main` (or, in stage 3b, from the integration branch head).
  Writers iterate with single-project builds through the build lane script (`odbuild.ps1`, kept outside the repository; project names as
  slnf targets), finish with one full `OpenDental.Local.slnf` build and the package's Targeted tests, and report complete, partial or
  blocked with the build log.
- **No two packages edit the same file at the same time.** Each package below names the files and projects it owns. Before launching
  packages in parallel, the lead greps each package's search patterns, computes the intersection of their file lists, and gives every
  shared file to exactly one package; the other package's edits to that file wait until the owner's merge. Known overlaps are resolved per
  stage below.
- **Project files and shared props have one owner per stage.** `Directory.Build.props`, `Directory.Build.targets`,
  `tests/Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `OpenDental.sln`/`.slnf` edits, the build lane
  script and the local tools folder (`.git/od-tools`, not versioned: test runner, workflow scripts, sln merge driver) are changed only by
  the stage's foundation package or by the lead.
- **Merge order is fixed in each stage**; after every merge the lane runs an incremental full slnf build on `main` (about 30 s) and the
  Targeted tests of the merged package. A red build on `main` is reverted, not fixed forward.
- **Locked runs are workflow steps, not agent steps.** Core and Full runs take the machine-wide test lock; the workflow script runs them
  one at a time after merges. Parallel package agents run only Targeted tests (always including NoHqTests). Full runs once per stage gate.
- **Stage 1 converts leaf-first.** Classic and SDK-style projects build together under MSBuild.exe and reference each other normally, so
  every wave can merge on its own.
- **Stage 3b is the only stage that cannot be merged piece by piece.** A net48 project cannot reference a net10.0 project (NuGet NU1201,
  https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1201), so the retarget happens bottom-up on one integration
  branch (`net10/switch`). `main` stays net48 and shippable the whole time; the branch takes regular merges from `main` (never the other
  way) and merges into `main` once, after its gate. If the switch must be undone after that merge, the merge commit is reverted and the
  offices stay on the bridge release.
- **No database schema change in stage 3b**, so going back to the net48 bridge release never needs a database change. Stage 0 changes no
  schema either: the preference of the removed old Imaging module stays in the code, and its row stays in the database (S0-6).
- **The sln merge driver** (`sln_merge_driver.py` in the local tools folder, three-way) stays registered; after any merge that touches
  `.sln`, check the changed Project lines.

### 2.2 How the .NET Framework build and the .NET 10 build coexist

They do not coexist as builds of one tree. There is no dual-targeting at any point:

- Stages 0, 1, 2, 4 and 3a: there is one build, net48. Stage 1 changes the project format only; the output folders and files stay as they
  are (flat `bin\Debug\` / `bin\Release\`). Stage 3a adds x64 test builds of a few test projects in separate folders (section 2.3).
- Stage 3b: `main` is net48; the `net10/switch` branch is net10.0-windows x64. Release builds of the branch run only against test
  databases and are never published to an office update channel (Debug builds never publish or pull; the branch's Release builds are not
  installed in the office).
- After the 3b merge, `main` is net10.0-windows x64 only, apart from the 32-bit helper program (net10.0-windows x86, S3a-13, O-15). The
  bridge release (end of 3a) is the last .NET Framework release; it is kept as the rollback installer.

### 2.3 Output layout (decided once, in stage 1)

Repo-wide `Directory.Build.props` for SDK-style projects: `AppendTargetFrameworkToOutputPath=false`,
`AppendRuntimeIdentifierToOutputPath=false`, `OutputPath=bin\$(Configuration)\`, `GenerateAssemblyInfo=false` (the existing
`AssemblyInfo.cs` + `SharedAssemblyInfo.cs` stay the only source, keeping AssemblyVersion 24.3.63.0), `ImplicitUsings=disable`,
`PlatformTarget=x86`, `SatelliteResourceLanguages=en`, `RestorePackagesWithLockFile=true`, `Deterministic=true`
(https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props), and `AutoGenerateBindingRedirects=false` unless a project sets it
(see S1-0; dropped on 2026-11-08, since it changed no config, S1-5 row). Directory.Build.props is imported by classic and SDK projects alike
(https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory), so SDK-only settings are guarded so the classic Middle Tier
project and `Direct2dWrapper.vcxproj` are unaffected; check every property with `MSBuild -getProperty`. Flat paths keep about 45 hard-coded
`bin\Debug`/`bin\Release` references valid (lane, test runner, Build-Release, the Direct2dWrapper post-build `xcopy`, the OpenPmsDental copy
targets, tests). This is safe only because there is no dual-targeting. The one exception before 3b: the x64 test builds of S3a-12 go to
`bin\x64\$(Configuration)\` with their own intermediate folder, so they never overwrite the x86 output. From stage 3b `PlatformTarget`
is x64 (decision 19), and the exes and test projects also set `RuntimeIdentifier=win-x64`, so Windows-specific package assets land flat
too (S3b-1). The exes are self-contained (`SelfContained=true`, decision 20): the x64 runtime files land flat beside them, one runtime
for all of them; the test projects stay framework-dependent (`SelfContained=false`). Only the 32-bit helper program stays x86 (S3a-13):
it is self-contained for win-x86 and is copied with its own runtime into the one sub folder of the output, `Helper32\` (O-15).

`tests/Directory.Build.props` (new, imports the parent with `GetPathOfFileAbove`, same source) holds everything the test projects share:
framework, platform, test framework references, MySqlConnector. Stages 2, 4 and 3b then change one shared file instead of every test
project file.

As built in S1-0: every setting of both files sits in a group with `Condition="'$(UsingMicrosoftNETSdk)' == 'true'"` (Microsoft.NET.Sdk sets
it before it imports `Directory.Build.props`), and MSBuild 18 `-getProperty` gives the same values before and after for the classic
projects, the classic Middle Tier project and `Direct2dWrapper.vcxproj`. The flat output folder is set as `AppendPlatformToOutputPath=false`
(with the framework and runtime ones), not as a literal `OutputPath`: the SDK then computes `OutputPath=bin\$(Configuration)\` after the
project's own properties, when `$(Configuration)` is known, also for a project built on its own without a configuration (a literal
`OutputPath` in `Directory.Build.props` is evaluated before the SDK's default configuration and gives only `bin\` there).
Until 2026-11-08 `AutoGenerateBindingRedirects` was `false` unless the project set it (S1-5 row). `tests/Directory.Build.props` sets `net48`,
`AutoGenerateBindingRedirects=true` and the MSTest v1 reference by name, which all 108 test projects have; MySqlConnector stays in the
projects (100 of the 108 reference it) until stage 2 moves it to a package.

Added from S1-1's findings, before S1-1 merges, in the same guarded group (`2ec7b359f`): `GenerateSupportedRuntime=false`, because otherwise
the SDK writes a `supportedRuntime` element into each .NET Framework exe's `app.config` and saves the file again (another XML declaration
and layout, the same content), and the gate's comparison of config contents fails; and `EnableSourceLink=false`, so the PDBs record no
repository URL or commit, and the build does not read the `.git` folder. S1-1 drops its per-project `GenerateSupportedRuntime`.

### 2.4 The toolchain, the SDK pin and how to bump it

- **Build Tools 2026 from S1-0.** From S1-0 the build lane, both test runners and every tool script that builds use the MSBuild.exe
  (18.x) and vstest.console.exe of Visual Studio Build Tools 2026. They find them with
  `vswhere -latest -products * -version [18.0,19.0) -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`
  (and `-find Common7\IDE\Extensions\TestPlatform\vstest.console.exe` for vstest) or take them as a parameter, never with an unranged
  `vswhere -latest`. Without `-products *`, vswhere does not list Build Tools. Build Tools 2026 18.11.3 (MSBuild 18.11.1, vstest 18.11)
  are installed in `Microsoft Visual Studio\18\BuildTools` (Visual Studio 2026 installs under `18`, not `2026`) on the build PC
  beside Build Tools 2022 17.14.44 (2026-11-08). Until S1-0 the lane stays on Build Tools 2022. Build Tools 2022 stay installed until the
  stage 1 gate has passed on Build Tools 2026, so the lane can go back by parameter.
- **Why S1-0 and not 3b.** Under MSBuild 17.14, an SDK 10 build of any SDK-style project, net48 included, sets
  `RoslynCompilerType=FrameworkPackage` and adds a `PackageDownload` of `Microsoft.Net.Sdk.Compilers.Toolset` at the exact SDK version
  (unless `DOTNET_HOST_PATH` is set), and warns NETSDK1233 (target `_WarnWhenUsingNET10AndVSPriorTo18`, for MSBuild below 17.15). Every
  SDK band after 10.0.1xx needs MSBuild 18. vstest 17.14 lacks the x86 test host `DOTNET_ROOT` fix of vstest 18.9 (no longer needed:
  the tests run x64 from 3b, decision 19). Visual Studio 2022 17.14 leaves mainstream support on 2027-01-13
  (https://learn.microsoft.com/en-us/visualstudio/productinfo/vs-servicing).
- **Components the 2026 instance needs** (the S1-0 offline check confirms them): .NET desktop build tools; web development build tools
  (the classic Middle Tier project until 3b); Desktop development with C++ with the MSVC v143 toolset (`Direct2dWrapper.vcxproj` uses
  v143, which is supported to 2032); .NET Framework 4.8 SDK and targeting pack; Testing tools core; NuGet build tools; Windows 11 SDK
  10.0.26100. Component ids: `Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools`, `Microsoft.VisualStudio.Workload.WebBuildTools`,
  `Microsoft.VisualStudio.Workload.VCTools` with `Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64` (MSVC v143),
  `Microsoft.Net.Component.4.8.SDK`, `Microsoft.Net.Component.4.8.TargetingPack`, `Microsoft.VisualStudio.Component.TestTools.BuildTools`,
  `Microsoft.VisualStudio.Component.NuGet.BuildTools`, `Microsoft.VisualStudio.Component.Windows11SDK.26100`. On 2026-11-08 the instance
  has all of them. The owner added MSVC v143 (MSVC 14.44, toolset files under `MSBuild\Microsoft\VC\v170`) beside the default MSVC 14.51
  (v145). A Win32 v143 DLL built with MSBuild 18.11 links with 14.44 (open point O-4, resolved).
- `dotnet build` cannot build `Direct2dWrapper.vcxproj` (MSB4278), so MSBuild.exe stays the build engine at every stage.
- **Licence and lifecycle.** Build Tools 2026 are used under a Visual Studio Community (or higher) licence; Community covers an
  open-source project under an OSI-approved licence, as this GPL project is (https://visualstudio.microsoft.com/vs/community/). Visual
  Studio 2026 is supported on 64-bit Windows 11 and follows the Modern Lifecycle: it is supported only when kept current. Its updates are
  installed between packages, never during a stage gate or a release build, and each one that changes the SDK is followed by the bump
  steps below.
- **The SDK pin.** From S1-0, `global.json` at the repository root pins the .NET SDK with `"allowPrerelease": false`. Two 10.0 bands are
  installed on the build PC (2026-11-08). S1-0 chooses one after its offline check and records the choice and the reason in this section
  and in the commit (open point O-5):
  - **(a) The per-user 10.0.1xx band** (10.0.114, installed per user): `"version": "10.0.114"`, `"rollForward": "disable"`. Supported to
    November 2028; minimum MSBuild 17.14; it changes only through the bump procedure. NuGet advises `disable` with lock files, because
    SDK-implicit package references change with the SDK
    (https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies). MSBuild.exe finds
    the SDK through its SDK resolver, which locates `dotnet.exe` from `PATH` unless `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` names the folder
    (https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables). The machine-wide dotnet has 9.0.320 and 10.0.403 but
    not 10.0.114, so every build entry point (the lane, the tool scripts, the README command line, Visual Studio) needs, in its own process:
    `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` set to the per-user dotnet folder and that folder first in `PATH`. Its packs have the win-x64
    apphost (`Microsoft.NETCore.App.Host.win-x64` 10.0.14) that 3b needs (decision 19), but not the win-x86 one that the self-contained
    helper needs (O-15): with this band, `Microsoft.NETCore.App.Host.win-x86` comes from NuGet (batch D).
  - **(b) The band Build Tools 2026 install machine-wide** (10.0.403 on 2026-11-08): `"version": "10.0.403"` with
    `"rollForward": "latestFeature"` (any later 10.0 band or patch, never 11.0), or with `"disable"` and a documented bump after each
    Visual Studio update. No per-user SDK and no resolver variables; it is the SDK that ships with the lane's MSBuild; its packs include
    the win-x64 apphost and the win-x86 one, which the self-contained helper uses (O-15). Visual Studio updates move it to a later band
    about every quarter and remove the old one, so the choice must also say what happens when an update brings a later band or the next
    major version's SDK.
  - Every option sets `DOTNET_NOLOGO=1` and `DOTNET_CLI_TELEMETRY_OPTOUT=1` in the lane's own process.
  - **Decided in S1-0 (2026-11-08): band (a), `"version": "10.0.114"`, `"rollForward": "disable"`** (O-5). The offline check (S1-0
    step 1) ran in a scratch folder whose `nuget.config` had one empty local source, with an empty `NUGET_PACKAGES`:
    - Both bands pass. Under the Build Tools 2026 MSBuild 18.11.1, an SDK-style net48 class library, an x86 exe that references it, an
      SDK-style net48 test project that references MSTest v1 by name (resolved by `ODAddMSTestV1SearchPath` from
      `18\BuildTools\Common7\IDE\Extensions\TestPlatform`) and a net10.0 class library build with `/restore`: no package is restored or
      requested (no `Microsoft.Net.Sdk.Compilers.Toolset`, no `downloadDependencies` in `project.assets.json`, `NUGET_PACKAGES` stays
      empty), no NETSDK1233, no warning; a locked restore passes; vstest 18.11 runs the probe's tests with `/Platform:x86`. The SDK
      resolver sets `DOTNET_HOST_PATH` to the dotnet that holds the pinned SDK, so `RoslynCompilerType` is `Core` and the SDK-style
      projects compile with the SDK's own compiler (`sdk\<version>\Roslyn\bincore\csc.exe`: Roslyn 5.0.0 in 10.0.114, 5.9.0 in
      10.0.403), while the classic projects keep MSBuild's (Roslyn 5.11.0 in MSBuild 18.11, 4.14.0 in 17.14).
      `_IsDisjointMSBuildVersion` is false for both (10.0.403 counts MSBuild below 18.9 as disjoint, 10.0.114 below 18.0).
    - Without the resolver variables, 10.0.114 fails at once: "A compatible .NET SDK was not found. Requested SDK version: 10.0.114"
      (MSB4236), after the `errorMessage` of `global.json`, which the .NET 10 host prints through MSBuild's resolver too.
    - Under the Build Tools 2022 MSBuild 17.14 (a lane build of the probe), 10.0.114 with the resolver variables builds the same net48
      projects with the SDK's compiler and no compiler package: the resolver sets `DOTNET_HOST_PATH` there as well, so the download
      named under "Why S1-0" does not occur through MSBuild.exe with this band. 10.0.403 cannot be loaded by it at all ("Version
      10.0.403 of the .NET SDK requires at least version 18.0.0 of MSBuild").
    - **Why (a).** (1) It is the only band with which the lane's Build Tools 2022 fallback can still build SDK-style projects until the
      stage 1 gate. (2) Visual Studio updates do not touch it. 10.0.403 is the Build Tools' own component
      (`Microsoft.NetCore.Component.SDK`, package `Microsoft.NetCore.Toolset.10.0.403`), which an update replaces; 4xx is the last 10.0
      band, so the update that brings the .NET 11 SDK would leave no 10.0 SDK for the pin, and every update before it would change the
      SDK-style projects' compiler in the middle of a stage. (3) The SDK, and with it that compiler and the bundled runtime of the monthly
      runtime release, changes only by a bump, in its own commit, with its lock-file check; the monthly 10.0.1xx patch is covered by the
      owner's standing approval. (4) Supported to November 2028. The costs: every build entry point sets the resolver variables (the
      repository's `tools/Initialize-BuildEnvironment.ps1` does it for the tool scripts and, called from it, for the lane; a builder
      without the per-user SDK installs 10.0.114, per user or machine-wide, README); and the win-x86 apphost of the helper comes from
      batch D (O-15).
    - With `RestoreLockedMode=true`, a project that has no `packages.lock.json` yet restores and writes one: NuGet checks locked mode
      only against an existing lock file. A package that converts a project commits the lock file its first lane build writes; `git
      status` after the lane build shows any that was missed.
- **Bump procedure** (band (a): each monthly 10.0.1xx patch, or when a security fix needs it; band (b): after each Visual Studio update
  that changes the machine-wide SDK):
  1. Band (a): the owner approves the download (a non-NuGet download) and the new 10.0.1NN SDK is installed per user next to the current
     one. Band (b): the Visual Studio update is installed between packages. From stage 3b, with either band: the runtime packs of batch D
     at the runtime version the new SDK bundles are approved with it and placed in the offline folder (decision 20).
  2. With `disable`, the lead changes only `sdk.version` in `global.json`, in its own commit on `main`; never during a stage gate or a
     release build. With `latestFeature` the file does not change, but steps 3 and 4 still run before any other build.
  3. The lane runs a locked restore of the whole slnf. If NuGet reports a lock-file mismatch, run one unlocked restore, review the
     lock-file diff (only SDK-implicit packages may change) and commit it with the pin.
  4. Full slnf build and a Core run on `main`; the next stage gate runs Full as usual.
  5. Open worktrees rebase onto the new pin (with `rollForward` `disable`, a worktree on an SDK that is no longer installed fails at once
     with "SDK not found"). With band (a), remove the old SDK after all of them have.
- **Monthly runtime release (from stage 3b, decision 20).** Every shipped exe carries its own .NET 10 runtime, so Microsoft Update does
  not patch it: its security fixes reach the office only with this program's releases. Each month, after the .NET 10 patch release
  (Patch Tuesday), the lead runs the bump procedure above for the SDK patch that bundles that runtime, with the runtime packs at that
  patch; then a Release build, a Core run and a Release dry run; then a release is published. Its runtime part is new, so every
  workstation pulls the runtime once (S3b-7). A release-flow check refuses to stage a release whose runtime is older than the newest
  10.0.x patch of the runtime packs in the offline folder (S3b-7). A month with a new runtime and no other change still gets a release.
  The owner gave a standing approval for these monthly downloads on 2026-11-08 ("Approve monthly"): each month's .NET 10 SDK patch
  of the pinned band and the runtime packs at its runtime version, from Microsoft (nuget.org and Microsoft's .NET download site), are
  fetched without asking again; anything else still needs its own approval.

---

## 3. What every stage gate checks

- Full lane build of `OpenDental.Local.slnf` (Debug x86; Debug x64 from stage 3b), zero errors; the stage's warning rules (below) hold.
- Full test run (the test runner `run-tests.ps1 -Mode Full` from the local tools folder, locked, alone), compared per assembly with the
  previous gate: no new failures, no fewer passed tests except tests retired on purpose (listed in the package report), Inconclusive count
  not higher. From stage 1 on the lane sets `OPMS_REQUIRE_BUILD_OUTPUT=1` so the 23 "not built" Inconclusive checks in 16 files fail
  instead of silently skipping.
- Release dry run on a **Release** build (x86; x64 from stage 3b): `tools\Build-Release.ps1 -ListFiles` and `-CheckFolder`, and
  `tools\Test-Installer.ps1` (elevated from stage 3a on, see S3a-11); the staged file list is diffed against the previous gate and every
  difference is explained (from stage 3b in two parts, the program part and the runtime part, S3b-7). A Debug build cannot show
  Release-only steps (for example SGen, which MSBuild turns off for the Debug configuration only). From the stage 1 gate on, the
  Release dry run also builds RdlDesign in Release (it is in the slnf since S1-1); check it builds and that `RdlDesigner.exe` stays
  out of the release list. The Release dry run comes **before** the Full run with `-RequireBuildOutput` (lead, 2026-11-08), so
  ReleaseManifestTests' check of the Release output has a Release build to check instead of failing as "not built".
- The project-file reader agrees with MSBuild (from stage 1 on, package S1-T2): after the full lane build and before any test run, the
  gate records a baseline with `tools\Get-ProjectBaseline.ps1 -Out <folder>` and runs ProjectFileTests'
  `ProjectBaselineTests.EveryProjectOfTheSolutionFilter_EvaluatesTheItemsMSBuildRecorded` with `OPMS_CHECK_PROJECT_BASELINE=1` and
  `OPMS_PROJECT_BASELINE_DIR=<folder>`: zero differences between the Compile, EmbeddedResource, Page, Resource (since S1-T3), None and
  Content items (with their metadata) that `tests/ProjectFileTests/ProjectFile.cs` evaluates and the ones MSBuild recorded, for every
  project of the slnf, classic and SDK-style alike; it writes what it compared and every difference to `items-check.txt` in that folder.
  Without the variable the test is Inconclusive. S1-3, S1-4, S1-5 and S1-6 run it the same way in their proof.
- The reader's properties agree with MSBuild's (from stage 1 on, package S1-T3): after the full lane build every stage gate runs
  `tools\Test-ProjectFileEvaluator.ps1 -Out <folder>`, which compares GetProperty with MSBuild -getProperty for every C# project of the
  slnf; it must exit 0 (zero mismatches, zero silent empties, zero errors; a name the reader throws for as not modelled is allowed).
- Hash comparisons (from stage 2 on, the lead, 2026-11-09; S2-1's proof): the SHA-256 diffs of output folders and of the staged files
  list these as the files that change on every build, and explain every other difference: `OpenPmsDental.exe` and `.pdb` and their
  copies (the build stamp `ForkBuildUtc`, and `OpenPmsDental.ContrTable.resources`, which comes out in two forms, open point O-17, so the
  Release exe's size changes too), `Direct2dWrapper.dll` and `.pdb` (native, linked again), `OpenPmsDentalServer.dll` and `.pdb` (the
  classic Middle Tier project, not deterministic), and in a Release build `OpenPmsDental.XmlSerializers.dll` and
  `OpenPmsDentalBusiness.XmlSerializers.dll` (SGen numbers its generated types and methods anew). For these the comparison is of
  Mono.Cecil fingerprints (types, members, IL, references, resources) instead of hashes, with the ContrTable resource's two forms accepted
  until O-17 is closed and SGen's numbers normalised. New files in output folders follow stage 2's rule ("Identical output in stage 2").
- The stage's launch checks (section 6).
- From the merge of S3a-12 and S3a-16 until the switch: the x64 test runs of the pointer-size tests (S3a-12, S3a-15) pass, and
  `Direct2dWrapper` builds for x64 (S3a-16).

---

## 4. Stages

### Stage 0 - Clean-up and seams (net48, classic projects)

**Goal.** Remove the dead code the move would otherwise have to carry, make the legacy UnitTests project a running suite, and cut the
System.Web ties inside the shared libraries, without changing any behaviour the office uses. Under the standing rule (section 11) stage
0 removes only dead code and the old 2020 Imaging module; S0-2a and the Direct removal of S0-3 are withdrawn. The EOB and amendment
window changes its look and a few behaviours on purpose (listed in S0-6): it is rebuilt on the current Imaging code before the old 2020
Imaging module goes (S0-6, decision 17).

**Entry conditions.** `main` green on a Full run. The owner decisions of 2026-11-08 (section 11) settle stage 0: the standing rule
answers open point O-9 (the UnitTests developer windows stay), and the owner correction withdraws S0-2a and the Direct removal of S0-3.
Open point O-6 is answered (2026-11-08): the DCI eligibility path is kept and ported in S3a-6, so stage 0 does not touch it. Before
S0-5 starts, the lead fills in its lists from the survey of the legacy suite.

**Packages.**

| Package | Owns (files / projects) | Work | Targeted tests |
|---|---|---|---|
| **S0-T Tests out of the legacy UnitTests project** (first, alone) | New `tests/MiddleTierMockTests/` (project file; `MiddleTierRoundTripTests.cs` ported from `UnitTests/MiddleTier/MiddleTierTests.cs`, 88 tests; `ParamCheckTests.cs` from `UnitTests/MiddleTier/ParamCheckTests.cs`, 5 tests; a scratch-database helper copied from an existing test assembly); new `tests/LibraryUpdateTests/EmailMessagesTests.cs` (26 of the 27 tests in `UnitTests/UnitTests/EmailTests.cs`: decoding, inline-encoded text, reply/forward, image tags, statements, the 9 `ProcessRawEmailMessageIn_*` cases, `EmailUtils_HasValidChars`); new `tests/LibWebTests/EmailPreviewTemplateTests.cs` (the 27th, `EmailPreviewControl_ReplaceTemplateFields_*`, which needs the program); new `tests/MiddleTierMockTests/ODThreadTests.cs` from `UnitTests/UnitTests/ODThreadTests.cs` (the 7 active tests, the two `SetDb` tests on the scratch database; two more are commented out and are not ported); `tests/LibraryUpdateTests/LibraryUpdateTests.csproj` (UnitTestsCore ProjectReference) and `tests/LibWebTests/LibWebTests.csproj` (UnitTestsCore ProjectReference and MySqlConnector Reference), plus a scratch-database helper in `tests/LibWebTests`; the sln and slnf entries (lead); both test runners find `*Tests` projects in the slnf | Port to the conventions of `tests/`: MSTest v1 for now (stage 4 moves everything), a scratch database built from the seed dump, `UnitTestsCore` helpers allowed, no UnitTests `TestBase`. The mock server is set per test as `UnitTests/Misc/TestBase.cs:88-91` does (`RemotingClient.MiddleTierRole=ClientMT`, `OpenDentalServerProxy.MockOpenDentalServerCur=new OpenDentalServerMockIIS(...)`). The mock round trip runs `DtoProcessor` and `XmlSerializer` (`Remoting/DtoObject.cs:84`), so this suite catches the .NET 10 `XmlSerializer` change for `[Obsolete(IsError=true)]` members: add three round trips, a `Meth.*` call with a `PrefName` argument (`Prefs.GetBoolNoCache`, `Data Interface/Prefs.cs:197`), a `ClinicPref` insert, update and get (`ClinicPrefs.cs:72-97`; `ClinicPref.PrefName` is a public field, `TableTypes/ClinicPref.cs:17`), and an `Adjustment` (its obsolete `TaxTransID`, `TableTypes/Adjustment.cs:47`, starts to travel on .NET 10). Run the old classes once through vstest (they are built but not in the test runner) and the new ones on the same build: same tests, same outcomes. | MiddleTierMockTests (new), LibraryUpdateTests, LibWebTests; the old `UnitTests` MiddleTier, EmailTests and ODThreadTests classes once |
| **S0-1 Stray usings and dead code** (after S0-T; parallel with S0-3, S0-5) | Only `.cs`/`.resx` content, no project files: `OpenPmsDentalBusiness/Eclaims/ClaimConnect.cs`: line 8 (`using System.Web.Services`) and the commented-out `MyMath` block 1221-1249 only; lines 7 (`HttpUtility` at 77-79, 277) and 9 (`SoapException` at 304 and 817; S3a-6 replaces it) stay; `OpenPmsDentalBusiness/Eclaims/DentiCal.cs`, `EDS.cs`, `EdsMedical.cs`, `MercuryDE.cs`, `NHS.cs`: their `System.Web`, `System.Web.Services` and `System.Web.Services.Protocols` usings (none is used); `OpenPmsDental/Forms/FormClaimOverpay.cs:7,9`; `FormQueryMonitor.cs:3,11`; `DataConnectionBase/MySqlHelper.cs:8`; `OpenPmsDentalBusiness/Db/ConnectionStore.cs:5`; `Eclaims/CanadianOutput.cs:12,16`; unused `Microsoft.VisualBasic` usings (`FormODBase.cs:11`, `FormFrame.cs:11`, `UserControlSetupWizFeeSched.cs:15`, `FormImagingSetup.cs:10`); ADODB usings `Misc/ConvertDatabases7.cs:13`, `ConvertDatabases8.cs:15`; CAS attributes (`CodeBase/Utilities/ODException.cs:76`, `WpfControlsOD/Bridges/CADI.cs:116,124`, `CodeBase/Controls/OpenGLWinFormsControl.cs:150`) and their `System.Security.Permissions` usings; the Health.Direct usings that serve no Direct code: `OpenPmsDental/UI/Appt/ToggleDayWeek.cs:1`, `UI/CheckBox.cs:2`, `UI/ListBox.cs:1`, `Db Multi Table/OrthoCaseProcedureLinker.cs:5`, `SheetFramework/SheetUtil.cs:10`, `Data Interface/RequiredFieldConditions.cs:1`; the 16 orphaned BinaryFormatter `.resx` entries (`Main Modules/ControlAppt.resx` 6 DataTables, `Forms/FormClaimsSend.resx` 2, `Forms/FormTsiHistory.resx` 6, `Forms Reports/FormRpUnfinalizedInsPay.resx` 2, plus any `GetObject` line that reads them); the 8 stale `RdlEngine/Definition/{DataSet,DataSets,DataSource,DataSources,Pages,Report,StyleInfo,XmlUtil}.cs` (not in the project today); `OpenPmsDentalServer/OpenDentalServer.Publish.xml` | Delete. Nothing compiled changes except removed attributes. A call that bound to Direct's `IsNullOrEmpty()` extension uses CodeBase's (`CodeBase/Utilities/ODPrimitiveExtensions.cs:23`, for `List<T>`) or an explicit null-or-empty check. | NoHqTests, ReportTests, BINBTests, VendorDirectTests, ApptTests, BAPPTTests, M2CANTests; open the Appointments module once (ControlAppt resources) |
| **S0-5 Legacy UnitTests made a running suite** (after S0-T; parallel with S0-1, S0-3; starts when the lead has filled in its lists) | `UnitTests/**`; the include rule of `tools/run-tests.ps1` (6, 88, 90) and, by the lead, that of the test runner in the local tools folder; the docs that call UnitTests unrun (`README.md:189`) | **Goal.** The legacy UnitTests project (1,944 active tests that no test runner runs today) becomes a running suite: both test runners run it, and the Core and Full runs and every stage gate count it, as they count the projects under `tests/` (standing rule, section 11). **Work, in this order.** (1) Remove the four classes that S0-T ported and that live on in `tests/`: `UnitTests/MiddleTier/MiddleTierTests.cs`, `UnitTests/MiddleTier/ParamCheckTests.cs`, `UnitTests/UnitTests/EmailTests.cs` and `UnitTests/UnitTests/ODThreadTests.cs`, with their project items. (2) Remove only the tests of deliberately removed features (section 11: the old 2020 Imaging module, the earlier fork removals tied to Open Dental HQ services and eServices or to closed parts with no published interface, dead code); the report names each one with its removal. (3) Fix every other test that fails today. When the product code is wrong and not the test, the lead assigns that fix. (4) Add UnitTests to both test runners; then a Full run by the workflow. **Lists from the survey.** The exact lists of tests to fix and to remove, and how the suite's own database set-up (`UnitTests/Misc/TestBase.cs`) runs under the test runner, come from the survey of the legacy suite (running on 2026-11-08); the lead fills them in here before S0-5 starts. **What stays.** The seed dump `UnitTests/Resources/dump.txt` stays where it is. That is simplest: 22 test helpers, DevDbTool and three tool scripts read it there, so the seed move of the first draft and its list of readers are dropped. The `UI Testing` developer windows stay (open point O-9, answered by the standing rule). The project's references stay, `Health.Direct.Common` and `xCentralManager` included, because Direct and CentralManager are kept; so does the xCrudGenerator option that writes `SchemaCrudTest.cs` for it. UnitTests then moves with the other test projects: SDK-style in S1-6, MSTest 4 in S4-1, .NET 10 in S3b-5. | UnitTests (alone first, then in the workflow's Full run); MiddleTierMockTests, LibraryUpdateTests and LibWebTests (the four ported classes); NoHqTests |
| **S0-4 Middle Tier host seam** (after S0-T; starts when a writer is free) | `DataConnectionBase/xDataConnectionBase.csproj` (one new Compile item), new `DataConnectionBase/MiddleTierHost.cs`; `OpenPmsDentalBusiness/Remoting/DtoProcessor.cs`, `Data Interface/Userods.cs`, `Plugins/Plugins.cs`; `WpfControlsOD/Bridges/PluginLoader.cs`; `DataConnectionBase/ConnectionStoreBase.cs`; `OpenPmsDentalServer/ServiceMain.asmx.cs` (also its stray `System.Web` using at line 6) | A static host context (content root, per-request application name and client IP, thread-static) replaces all 12 `HostingEnvironment`/`HttpContext.Current` uses in 5 files; the ASMX code-behind fills it, so net48 behaviour is unchanged. `ProcessDto` gains `(contentRoot, applicationName, clientIp)` and keeps the existing `ProcessDto(dtoString)` form that `OpenDentalServerMockIIS.cs:33` calls. It lives in DataConnectionBase (not the business library) because `ConnectionStoreBase` needs it too and cannot see the business library. `FixPackageAssembly` stays until 3b. | MiddleTierMockTests (the ported suite is the safety net for this change), BSERVERTests, QueryMonitorTests, DbConnectionSecurityTests, NoHqTests |
| **S0-2a Cut integrations; serializer assemblies retired. Withdrawn on 2026-11-08** (merged as `bdbf42fd2`; the lead reverts that merge) | nothing from now on; the revert is the lead's | The owner correction of 2026-11-08 (section 11) keeps everything S0-2a removed: QuickBooks Desktop (QBFC), Schick CDR, Topaz signature pads, CallFire SMS texting, Divvy postcards, the BCBS Georgia dial-up terminal and RdlEngine's SQL Server, ODBC and OLE DB data providers. The lead reverts its merge on `main`. The revert also restores both XmlSerializers files: SGen runs again for Release builds (`GenerateSerializationAssemblies` back to `Auto`), and both files are back in `$OwnFiles` of `tools/Build-Release.ps1` and out of its deny list, `ForkReleases.ListRetiredFiles` and the setup's `[InstallDelete]`. The revert removes `SerializerAssembliesRetiredTests`; a ReleaseFlowTests test that both files ship takes its place. With `Auto`, SGen runs for a project only while it has `WebReferenceUrl` items (critique W1). So every later package that removes such an item proves with a Release dry run that both files are still staged (S1-3, S3a-6), and stage 3b builds them with `Microsoft.XmlSerializer.Generator` (S3b-3, S3b-4, batch D). The port of each kept item is planned separately (open point O-10). | for the revert: a lane build of the slnf, the Targeted tests S0-2a listed, a Full run by the workflow, and a Release dry run whose staged list equals that of the gate before S0-2a, both XmlSerializers files included |
| **S0-3 E-mail on MailKit and MimeKit; CDO replaced; Direct removed (step 3 withdrawn on 2026-11-08)** (after S0-T; the longest package) | `OpenPmsDentalBusiness/Email/SendEmails.cs`; `Data Interface/EmailMessages.cs`; `Data Interface/DatabaseMaintenances.cs` (only `CleanUpRawEmails`, 11709-11788); `OpenPmsDental/Forms/FormEmailInbox.cs`, `FormEmailMessageEdit.cs`, `FormEmailDigitalSignature.cs`/`.designer.cs`, `FormEmailAddressEdit.cs`/`.designer.cs` (the Register certificate button), `FormEmailCertRegister.cs` (its two calls of the moved certificate-name helper; the form stays, see S0-2b); `OpenPmsDental/User Controls/EmailPreviewControl.cs`; `WpfControlsOD/Frms/FrmReferralEdit.xaml`/`.xaml.cs` (the Direct trust check box); tests `tests/LibraryUpdateTests/*` (except the project file), `tests/LibWebTests/WebView2PageStoreTests.cs:255`, `tests/LibWebTests/EmailPreviewTemplateTests.cs`, `tests/VendorDirectTests/Email/*`, `tests/BDBMTests/WiringTests.cs:70-76` (pinned again to the MimeKit code) | In this commit order. **(0) Record.** A corpus of raw messages (the 9 ported receive cases, the `MimeTests` corpus, a signed message, an S/MIME-encrypted one, a `windows-1252` one, one whose sender has consecutive dots in the local part) is parsed with today's Direct-based code; the resulting `EmailMessage` fields are committed as expected values. **(1) CDO to MailKit.** Implicit-TLS SMTP (port 465, `SendEmails.cs:53-108`) uses MailKit `SmtpClient` with `SecureSocketOptions.SslOnConnect` (MailKit is already shipped) and carries over what CDO set: the inline images' `Content-ID` (same `HttpUtility.UrlEncode(fileName)` value, 86-92) and each attachment's `Content-ID` and `Content-Disposition: attachment; filename=<DisplayedFilename>` (97-106). The custom-header pass-through (63-71, "Needed for Direct Acks") has no caller left once Direct goes; step 3 removes the `nameValueCollectionHeaders` parameter from `WireEmailUnsecure` and `BasicEmailMessageToMailMessage` (172-173). **(2) Received mail on MimeKit.** `ProcessRawEmailMessageIn` and its helpers (`RawEmailToIncomingMessage` 1622-1737, `GetMimePartsForMimeTypes` and the `GetMimeImage*` helpers 1875-2034, `DissolveAttachmentsFromIncomingMessage` 2175, `GetAttachmentMimeParts` 2279, `GetMimeLeafNodes` and `ConvertMessageToEmailMessage` 2616-2821, `ProcessMimeTextPart`, `GetMimeEncoding` and the `IsMimeEntity*` checks 2928-3088) parse with `MimeKit.MimeMessage` instead of Health.Direct's `Message`/`MimeEntity`, giving the same fields (addresses, subject, date, text or HTML body, attachments with their names, inline images with their `Content-ID`); MimeKit's tolerant parser replaces the boundary-scrubbing workaround; `RawEmailIn` stays stored unchanged. Signed messages show their content and keep `smime.p7s` as an attachment, as today. S/MIME-encrypted messages (only Direct's agent decrypted them) are stored with a clear "encrypted message, cannot be shown" body. `EmailPreviewControl` and `CleanUpRawEmails` use the MimeKit types. Received-mail parsing no longer builds `System.Net.Mail.MailAddress`, so the .NET 10 rule against consecutive dots cannot reject a received sender; the corpus case keeps the raw address text. **(3) Direct removed** (merged, then withdrawn by the owner correction of 2026-11-08: Direct comes back on top of the MimeKit code of steps 1, 2 and 4 in a new package of the lead, which also decides how received encrypted mail and trust show again). Sending encrypted (`SendEmailDirect`, `SendAckDirect`, `SendOldestUnsentAck`, `SendDirectUnsecure`, the `useDirect` argument of `SendEmail`), certificate discovery and trust (`GetDirectAgentForEmailAddress`, `GetReceiverUntrustedCount`, `IsSenderTrusted`, `TryAddTrustDirect`, `TryAddTrustForSignature`, `RefreshCertStoreExternal` (its calls at `OpenPmsDental/Forms/FormEmailInbox.cs:731-736` and `OpenPmsDental/User Controls/EmailPreviewControl.cs:619`), `FindPublicCertForAddress`, `DnsQueryForCert`, the LDAP resolver, `EmailNameResolver`/`EmailPublicResolver`/`EmailPrivateResolver` 3990-4185; the static certificate-name helpers of `EmailNameResolver`, `GetCertSubjectName` and the others at 4114-4185, move to a second, static class at the end of `Data Interface/EmailMessages.cs`, which S0-3 owns, because `FormEmailDigitalSignature` and `EmailPreviewControl:635` keep using them; S0-3 updates those callers and adds no file to a classic project, since `OpenPmsDentalBusiness.csproj` lists its `Email` and `Data Interface` files one by one and S0-2a owns it during this stage), and the UI that calls them: "send encrypted" in `FormEmailMessageEdit`; the inbox's acknowledgement resend (`FormEmailInbox.cs:217`) and trust state in its signature column (350, 497; the column still shows that a message is signed); the trust status and Trust button of `FormEmailDigitalSignature` (it keeps showing the certificate); the referral's trust check box; the Register certificate button (its form already fails with `HqNotAvailableException`; since the owner correction the Direct package decides on it, not S0-2b). Historic rows keep their `EmailSentOrReceived` values and still display. **(4) Signed sending** (kept; O-1, answered 2026-11-08): `SendEmailUnsecureWithSig` signs with MimeKit's S/MIME classes (`MultipartSigned` with a `CmsSigner` on the certificate that `GetCertFromPrivateStore` finds in the `NHINDPrivate` Windows store, opened with `X509Store` directly) and sends through MailKit for every port, which also lifts today's "signed messages cannot be sent over implicit SSL" restriction. Today signed sending always uses `System.Net.Mail` with the stored password, even for Gmail and Microsoft 365 (OAuth) addresses, whose plain mail goes through their APIs. After the port, a signed MIME message from such an address either goes through the same API path as unsigned mail, or signing stays refused for them with a clear message; the test covers one OAuth address. `CreateCertificateStoresIfNeeded` stays, reduced to what signing needs. After this package no code uses `CDO`. The Health.Direct references and DLLs stay, because Direct comes back (S0-2b). | the corpus comparison (every difference explained in the report), the ported e-mail tests, a test that the implicit-TLS path builds a MailKit message with the inline `Content-ID`, attachment `Content-ID` and disposition file name, a signed message that MimeKit verifies; LibraryUpdateTests (with `FakeMailServers.cs`: POP3 receive, SMTP and implicit-TLS send), VendorDirectTests (Gmail and Microsoft 365 import), LibWebTests, BDBMTests (includes `EmailTextDbTests.RawEmails_ToolProcessesAMessageWithUnicodeWhiteSpace`), NoHqTests |
| **S0-6 EOB and amendment window on the new Imaging code; old 2020 module removed** (after S0-T; starts when a writer is free; its removal commit waits for the revert of S0-2a's merge; merges before S0-2b) | `OpenPmsDental/Forms/FormImages.cs`, `FormImages.designer.cs`, `FormImages.resx` (rebuilt; the class name, its `FormODBase` base and its public fields `ClaimPaymentNum`, `ClaimNumPreAuth` and `EhrAmendmentCur` stay, so the five call sites `FormClaimEdit.cs:2202,2214`, `FormClaimPayBatch.cs:545`, `FormClaimPayPreAuth.cs:68` and `FormEhrAmendmentEdit.cs:65` do not change); `OpenPmsDentalBusiness/Imaging/ImageStore.cs`: `ExportEobAttach` and `ExportAmdAttach` (1028-1060) and a new second static class `EobAmendmentFiles` at the end of the file (the window's logic without UI; as in S0-3, no file is added to a classic project; it also gives the local path of an EOB's or an amendment's file for each storage mode: the stored file with an A-to-Z folder, a temp copy downloaded from the cloud, a temp copy written from `RawBase64` with images in the database; the PDF viewer and the double-click open use that path); `OpenPmsDental/Main Modules/ControlImagesOld.cs`, `ControlImagesOld.designer.cs`, `ControlImagesOld.resx` (delete) and their items at `OpenPmsDental.csproj:1902-1904, 5498-5500, 8542-8545`; `OpenPmsDental/Main Modules/FormOpenDental.cs` (the start-up choice at 595-613, 908-910, `ImagesModuleUsesOld2020()` at 1223-1233 and its 7 calls at 1730, 2199, 3820, 3985, 4014, 4044, 4084, the comment at 6781), `FormOpenDental.Designer.cs:225`, `FormOpenDentalBShell.cs:69`; `OpenPmsDental/UI/BordersDpi/LayoutManagerForms.cs:1222-1225`; `OpenPmsDental/User Controls/Preferences/UserControlImagingGeneral.cs` (24, 87-92, 103, 129-132) and `.Designer.cs` (30, 61, 86-95, 326, and the rows below the check box); `OpenPmsDental/Forms/FormPreferences.cs:504-508` (the forced restart after a module swap); `OpenPmsDental/Resources/PrefInfos.xml:2131-2139`; `OpenPmsDentalBusiness/TableTypes/Pref.cs:1224` (the summary of `ImagesModuleUsesOld2020`); tests: new `tests/ImagingTests/EobAmendmentFilesDbTests.cs`, `tests/ImagingTests/WiringTests.cs` (41, 81-87, 100-110, 114-115), `tests/M2IMGTests/PdfJavascriptPrefTests.cs:85-91`, `tests/BINATests/ProprietaryBinariesRemovedTests.cs:101-110`; `docs/owner-questions.md` (B-IMG 1, settled by the removal); `OpenPmsDental/Main Modules/ControlImages.cs`, only the comment lines 4093-4097, in the removal commit, after the revert of S0-2a's merge. Not edited otherwise: `ControlImages.cs` (its Topaz lines are back with the revert) and `ControlImageDisplay.cs`; S0-6 follows their code. | In this commit order. **(1) The window, rebuilt** from the current Imaging module's parts: its WPF toolbar (`WpfControls.UI.ToolBar`) and `ZoomSlider`; PDFs in `ODWebView2`, with the PDF JavaScript preference applied before each navigation as in `ControlImageDisplay.LoadPdf`; image printing through `PrinterL.TryPrintOrDebugClassicPreview` as in `ControlImages.ToolBarPrint_Click`, with today's half-inch margins and origin at the margins as its arguments (`margins:new Margins(50,50,50,50)`, `printoutOrigin:PrintoutOrigin.AtMargin`) and the print log entry as its audit arguments (`auditDescription`, `auditPatNum`; one entry per print); scanning with the module's `EZTwain` calls and this computer's scan settings as in `ControlImages.ToolBarScan_Click` and `ToolBarScanMulti_Click`; paste through `ODClipboard`. Not reused, because they work only on a patient's documents and mounts: `ImageSelector` (image categories, thumbnails in the patient folder), `ControlImageDisplay` (documents, mounts, drawings, windowing; crop and rotation saved on the document row), the floating image windows, `FrmDocInfo`, signatures, Mount/Acquire with the capture session, Video, Import Automatically, Move to Patient, Forms and program links. **Required behaviour, as the old window has it today for the three uses.** On the left, a list of file names: the EOBs of the claim payment or of the preauthorization, oldest first (`EobAttaches.RefreshForOwner`), or the amendment's one file (none while its `FileName` is empty). The first is selected when the window opens. An image shows fitted, with zoom (the module's zoom slider replaces Zoom In, Zoom Out and Zoom 100) and drag to pan; a PDF shows in the PDF viewer, which pages through it; other types show nothing. Double-clicking an EOB other than JPG, JPEG or GIF opens it in its Windows program through `ODFileUtils.ProcessStart` (the stored file, or a downloaded temp copy for cloud storage; with images in the database, the message to export it first). Commands: Print, Delete, Scan Document, Scan Multi-Page Document, Import, Export, Copy, Paste and Close; right-click on the list gives Print and Delete. No Info, Sign, radiograph or photo scan, crop, rotate, flip or windowing, and no file drop onto the list (today a drop imports nothing: on an entry the handler throws, on empty space it returns). A scanned page is saved as JPG at the scan quality setting and also put on the clipboard; a multi-page scan is saved as PDF from a temp file, which is then deleted. Import takes several files for EOBs and one for an amendment; paste takes a bitmap or one image file from the clipboard. A new EOB is a new `eobattach` row of its payment or preauthorization (`ImageStore.ImportEobAttach` with both keys), and it is selected. For an amendment that has a file, scan and import ask "This will delete your old file. Proceed?" and replace it (`ImportAmdAttach`, then `CleanAmdAttach` of the old name). Delete asks "Delete EOB?" or "Delete amendment?", then `DeleteEobAttach` (file and row) or `DeleteAmdAttach` (the file; the row stays with an empty file name). Export saves a copy under the stored name. Copy puts the shown image on the clipboard. Print prints the shown image fitted inside half-inch margins, and an EOB print logs "EOB printed"; a PDF prints from the viewer's own print command. Cloud uploads and downloads show progress. Close returns OK, and each caller refreshes its scanned state as today. The window adds no permission check (there is none today); a preauthorization EOB keeps its ImageCreate and ImageDelete log entries (`ImageStore`). Same tables (`eobattach`, `ehramendment`), folders (`EOBs` and `Amendments` in the A-to-Z folder or the cloud, `RawBase64` with images in the database) and file names; no schema change. The window has no WinForms `ContextMenu` or `MenuItem`, no `Thread.Abort`, no `Process.Start` and no plugin hooks (decision 16), so stage 3a has nothing to convert in it. **Differences, on purpose** (listed in the report): the look; Print uses the program's printer settings and its Print Options window (Setup > Printers, Default situation), as the Images module does, instead of the Windows print dialog, and with the prompt off it prints straight away; paste onto an amendment that has a file asks and removes the old file, as scan and import do (today it leaves the old file); Copy logs the file's own name (today it reads a document with the EOB's key); printing an amendment logs with the amendment's patient (today it shows an error after printing, because the window has no patient); with images in the database, a PDF EOB or amendment shows (today the viewer reads an empty document instead of the file and shows nothing) and export writes the stored bytes (today `ExportEobAttach` and `ExportAmdAttach` read an empty stream there and fail). **Same commit:** `PreAuthWindows_ScanAndViewEob` (`WiringTests.cs:114-115`) checks the rebuilt window (it opens the preauthorization's EOBs from `ClaimNumPreAuth`) instead of `contrImagesMain`, so ImagingTests passes between commits (1) and (2). **(2) The old module removed**, after the revert of S0-2a's merge (rebased on it): delete `ControlImagesOld` and its three project items. `FormOpenDental` always creates `ControlImages` and loses `controlImagesOld`, `ImagesModuleUsesOld2020()` and every branch on it. The Setup > Imaging check box "Use old Imaging module interface, pre 2020" goes, with its handler, `DoSwapImagingModule`, the restart in `FormPreferences` and its `PrefInfos.xml` entry; the check boxes below it move up. `PrefName.ImagesModuleUsesOld2020` stays (`ConvertDatabases6.cs:3152` and `ConvertDatabases7.cs:2760,3113` name it), its row stays in the database, and the program no longer reads it; `ImagesModuleTreeIsCollapsed`, read only by the old module, stays unread too. The old module's plugin hooks go with it. The comments that still say the old module serves EOBs, amendments or the module choice go: `FormOpenDental.cs:6781` and `ControlImages.cs:4093-4097`. Its remaining source checks go or move to the rebuilt window: `WiringTests.cs` 41, 81-87 and 100-110, `PdfJavascriptPrefTests.cs:85-91` and `ProprietaryBinariesRemovedTests.cs:102-105` (the Scanning Setup check at 106-108 stays). | new `EobAmendmentFilesDbTests` (ImagingTests) on a scratch database, with an A-to-Z folder and with images in the database: the EOB list of a claim payment and of a preauthorization; adding from a file and from a bitmap (row, file or `RawBase64`, both owner keys, the preauthorization's ImageCreate entry); deleting (file and row gone, the ImageDelete entry); replacing an amendment's file (new name, old file gone) and deleting it (row kept, file name empty); export; the local path for the viewer in each storage mode; the commands of each mode; source checks that the five call sites are unchanged, that the window has no WinForms menu, `Thread.Abort` or `Process.Start`, that it applies the PDF preference before each navigation, and that no program code outside the convert scripts and `Pref.cs` names `ControlImagesOld` or `ImagesModuleUsesOld2020`; ImagingTests, BIMGTests, M2IMGTests, BINATests, BCLAIMTests, BSHELLTests, M202Tests, M2TERMTests (PrefInfos), P262LinksTests (`Pref.cs`), NoHqTests; the stage 0 launch check of the window (section 6) before the removal commit |
| **S0-2b Unused references removed** (last; small) | `OpenPmsDentalBusiness.csproj` (the `COMReference` items ADODB and CDO at 2667-2684), `WpfControlsOD/WpfControlsOD.csproj` (`System.Web`, `System.Web.Services`), `DataConnectionBase/xDataConnectionBase.csproj` (`System.Web`) | Done: only references that no code uses any more, so nothing a user does changes. **ADODB** `COMReference`: no code has used it since S0-1 removed its usings (dead code). **CDO** `COMReference`: dead code as well, not a feature cut. Its only use was implicit-TLS sending on port 465, which S0-3 moved to MailKit, and the Direct code restored by the lead's Direct package (S0-3D) sends through MailKit too; port 465 keeps working on MailKit (`tests/LibraryUpdateTests/SmtpTests.cs`, `SignedSendTests.cs`). Both were embedded interop types (`EmbedInteropTypes`), so no interop DLL shipped, and the release file lists, `THIRD-PARTY-NOTICES.txt` and `docs/third-party-binaries-audit.md` do not change. **System.Web** (WpfControlsOD, DataConnectionBase) and **System.Web.Services** (WpfControlsOD): no `.cs`, `.xaml` or `.resx` file of either project uses them since S0-4 (`DataConnectionBase/MiddleTierHost.cs` and `WpfControlsOD/Bridges/PluginLoader.cs` name System.Web only in comments); the business library keeps its `System.Web` reference, which its code uses. Proof of non-use: a search of the repository outside `.claude/` (submodules included) finds no ADODB or CDO type or namespace use (`DirectRestoredTests.cs` only checks that the e-mail sources have no `using CDO` and no `CDO.`), and the full build passes without the references. Kept, by the owner correction of 2026-11-08 (section 11): the three Health.Direct references and DLLs, `FormEmailCertRegister` and the certificate-store call in `FormOpenDental.cs`, because Direct is kept; `SchemaCrudTest.cs` with the xCrudGenerator option that writes it for the kept UnitTests project. No project file has a `COMReference` or `COMFileReference` any more, so the `ResolveComReferences` target (conditioned on those items), whose task raises MSB4803 under `dotnet build`, no longer runs; `dotnet build` was not run for this package, and it still cannot build `Direct2dWrapper.vcxproj` (MSB4278), so MSBuild.exe stays the build engine at every stage (section 2.4). | NoHqTests, LibraryBindingTests, ReleaseFlowTests, LibraryUpdateTests (port 465 on MailKit), MiddleTierMockTests, QueryMonitorTests, DbConnectionSecurityTests; ForkMigrationTests (the convert scripts that had the ADODB usings) in the lead's Full run; Release dry run (no change) |

**Order and merge order.** S0-T first and alone. Then S0-1, S0-5 and S0-3 in parallel (at most three writers); S0-4 and S0-6 start
when a writer is free (each needs only S0-T merged). S0-2a merged and is withdrawn: the lead reverts its merge, and a new package of the
lead brings Direct back on top of S0-3's e-mail code (owner correction, 2026-11-08). Merge order: S0-T, S0-1, S0-4, S0-3, the revert
of S0-2a, S0-6, S0-2b. S0-4 may merge at any point after S0-T; S0-6's first commits may merge before S0-3 (they share no file), and its
removal commit comes after the revert. S0-5 may merge at any point after S0-T, once its lists are done. The Direct package merges after
S0-3 and the revert, where the lead plans it. S0-2b starts after S0-1 and the revert of S0-2a.

Why this order: the ported tests (S0-T) must exist before the Middle Tier seam (S0-4) and the e-mail rewrite (S0-3) change the code they
test, and before S0-5 removes the four ported classes from UnitTests. S0-5 owns only UnitTests and the runners' include rules, so it
gates no other package. UnitTests keeps its `Health.Direct.Common` reference, because Direct is kept. S0-2b removes the references
that S0-1 (the ADODB usings), S0-3 and the Direct package (CDO) and S0-4 (System.Web in WpfControlsOD and DataConnectionBase) left
unused; it edits `OpenPmsDentalBusiness.csproj` only after the revert has given that file back. The revert and S0-6's removal commit
both edit `ControlImagesOld.cs` (the revert restores its Topaz lines, the removal deletes the file), `FormOpenDental.cs` and
`OpenPmsDental.csproj`, so the removal commit is rebased on the revert. The old module goes in stage 0, so that stage 3a never converts
its menus, its Thread.Abort or its shell starts.

**Known overlaps, resolved.** `OpenPmsDentalServer/ServiceMain.asmx.cs` belongs to S0-4 (S0-1 leaves its line 6).
`Eclaims/CanadianOutput.cs` and `ClaimConnect.cs` belong to S0-1. `UnitTests/**` and the include rule of `tools/run-tests.ps1` belong
to S0-5 alone. `tests/LibWebTests`: S0-3 owns `WebView2PageStoreTests.cs` and the S0-T file. `DatabaseMaintenances.cs` belongs to S0-3
in this stage. The revert of S0-2a (lead) touches the files S0-2a owned. After it, `OpenPmsDental.csproj`,
`OpenPmsDental/Main Modules/FormOpenDental.cs`, `Main Modules/ControlImagesOld.cs` and `Main Modules/ControlImages.cs` pass to S0-6 (the
three `ControlImagesOld` items, the old-module branches, the file's deletion and the comment lines 4093-4097, in the removal commit),
and `OpenPmsDentalBusiness.csproj` passes to S0-2b. The Direct package (lead) owns the e-mail files it restores, after S0-3's merge.
S0-6's first commits (the rebuilt window, its business class and its tests) touch no file of another package. S0-6 adds no file to a
classic project (its business class goes at the end of `Imaging/ImageStore.cs`) and does not edit `OpenPmsDentalBusiness.csproj`, so it
shares no file with S0-3. `Main Modules/ControlAppt.cs`: S0-1 owns only `ControlAppt.resx` and the Designer `GetObject` lines that read
its orphaned entries. Step 4 of S0-3 may need a `BouncyCastle.Cryptography` reference in `OpenPmsDentalBusiness.csproj` (HintPath
`Required dlls`; the DLL already ships with MailKit and MimeKit), because `MultipartSigned.Create` has a same-arity overload that takes a
BouncyCastle `PgpSecretKey`. If it does, S0-3 adds that one Reference item in its worktree, merges after S0-2a and rebases onto it.

**Full-test gate.** Section 3. Expected: only tests of the old 2020 Imaging module and of removed dead code retired, and listed; the
legacy UnitTests suite runs, with S0-5's fixes and removals listed; the ported suites pass; the e-mail corpus differences reviewed; the
Release dry run stages the same files as the gate before stage 0 (the revert of S0-2a restores both XmlSerializers files; the
Health.Direct files stay).

**Tooling changes.** Add `tests/MiddleTierMockTests` to the slnf (both test runners find `*Tests` projects there). S0-5 adds UnitTests
to `tools/run-tests.ps1` (its include rule, 6, 88, 90), and the lead adds it to the test runner in the local tools folder; nothing is
removed from the lane script or the local tools folder. `Directory.Build.targets:12-15` (comment) and `tools/Test-NoHqHosts.ps1:62-63`
stay as they are, because UnitTests and its developer windows are kept (O-9). The workflow scripts' step that copies
`packages/Mono.Cecil.0.10.3` into new worktrees stays until S1-6. Build-Release and the setup: no change after the revert of S0-2a. The lane
and the test runners stay on Build Tools 2022 through stage 0. Build Tools 2026 are already installed. Given no lane or path,
`tools/Build-Release.ps1`, `tools/New-InitSql.ps1`, `tools/New-DevDatabase.ps1`, `tools/AdoptionValidation/Invoke-AdoptionRehearsal.ps1`
and `tools/SyntheticData/Invoke-SyntheticData.ps1` find MSBuild with `vswhere -latest -products *`, which now picks the MSBuild 18 of
Build Tools 2026. The owner added MSVC v143 to that instance (2026-11-08), so such a build works, including `Direct2dWrapper.vcxproj` in
Build-Release's slnf build. But it uses another compiler than the lane's MSBuild 17.14, runs outside the lane's lock, and writes to the
same output folders that the lane and the test runs use. So until S1-0 moves everything, `Build-Release.ps1` and `New-InitSql.ps1` run
with `-BuildLane`, so that release builds use the lane's toolchain. The other three have no `-BuildLane`: they run with `-SkipBuild`
after a lane build, or with `-MSBuildPath` set to the Build Tools 2022 MSBuild.exe.

**Downloads.** None. MimeKit's S/MIME support is part of the shipped MimeKit.

**Risks and mitigations.**
- Removing a feature someone uses: under the standing rule stage 0 removes only the old 2020 Imaging module (after its window is rebuilt,
  S0-6) and dead code; S0-2a and the Direct removal are withdrawn and reverted (section 11). The Practice-Web DCI eligibility path stays
  (O-6, decision 12).
- The received-mail rewrite changes what users see for odd messages: the corpus is recorded with the Direct-based parser before any change,
  and every difference is reviewed; the raw message stays stored, so a message can be re-parsed later.
- CDO to MailKit changes the wire behaviour of one SMTP path: test against the fake SMTP server with implicit TLS and once against a local
  test SMTP server before merging.
- The seam changes a Middle Tier code path that no shipped office runs: the ported mock suite (S0-T) and BSERVERTests.
- The legacy suite fails in many places when it first runs: the suite runner has never run it, and it needs its own database set-up.
  S0-5 starts from the survey's lists, fixes tests rather than removing them, and removes only tests of deliberately removed features
  (section 11). The 127 tests that S0-3, S0-4 and S3a-1 rely on (Middle Tier round trips, e-mail parsing, ODThread) already run in
  `tests/` (S0-T). From S0-5's merge the Full runs count UnitTests, so the stage 1 baseline and every later gate include it. Some of its
  tests cover code that later packages change (SInTests for S3a-3, ODFileUtilsTests for S3a-2); those packages run UnitTests with their
  Targeted tests.
- The rebuilt EOB and amendment window misses something the old one does: S0-6 lists the old window's behaviour for its three uses
  command by command, tests the file handling on a scratch database in both storage modes, and runs the launch check from all four entry
  points before its last commit deletes the old module. Rows, folders and file names stay as they are, so a workstation that is not yet
  updated and an updated one show the same EOBs. A database that still has the old-module option on gets the current Imaging module; the
  row stays, unread.

**Size.** 8 packages (S0-2a withdrawn); medium. The first estimate was about 12-17 dev-days (S0-3 4-6 of them, S0-6 about 2-3). It is
under revision: S0-5 grows by the fixes the survey lists, and the lead plans the revert of S0-2a and the Direct package.

---

### Stage 1 - SDK-style project files (net48)

**Goal.** Every C# project in the solution except the Middle Tier, with RdlDesign and DocumentationBuilder, which join the solution here
(S1-1, S1-5), becomes `<Project Sdk="Microsoft.NET.Sdk">` targeting `net48`, built
with the Build Tools 2026 MSBuild and the pinned .NET SDK 10, with the same compiled files, the same embedded resource names, and the same
output folders, files and config file contents.

**Entry conditions.**
- Stage 0 merged and green.
- The toolchain on the build PC (done on 2026-11-08): Visual Studio Build Tools 2026 18.11.3 beside Build Tools 2022 17.14.44; the .NET
  SDK 10.0.403 machine-wide (installed with Build Tools 2026) and 10.0.114 per user.
- **MSVC v143 in the Build Tools 2026 instance** (done on 2026-11-08; open point O-4 resolved). `Direct2dWrapper.vcxproj` sets
  `PlatformToolset` v143 (`Direct2dWrapper.vcxproj:33,39,45,52`). The owner added the component
  `Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64` (MSVC 14.44) to the 2026 instance, and a Win32 v143 DLL built with MSBuild
  18.11 links with 14.44. The project, its output and the setup's VC++ check stay as they are.
- The offline folder (in the local tools folder) holds `Mono.Cecil 0.10.3`, copied from the ignored `packages/Mono.Cecil.0.10.3` folder
  that `nuget restore` fills (no download).

**Packages.**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S1-0 Toolchain and foundation** (first, alone; steps in order) | `Directory.Build.props` (new), `tests/Directory.Build.props` (new), `Directory.Build.targets`, `global.json` (new), `nuget.config` (new), new `tools/Get-ProjectBaseline.ps1`; the MSBuild and vstest look-ups in `tools/Build-Release.ps1` (`Find-MSBuild`, plus the restore switch), `tools/New-InitSql.ps1`, `tools/New-DevDatabase.ps1`, `tools/AdoptionValidation/Invoke-AdoptionRehearsal.ps1`, `tools/SyntheticData/Invoke-SyntheticData.ps1` and `tools/run-tests.ps1` (`Find-VsTest`); `README.md` (Building: prerequisites, install command, command-line build); the build lane script, the test runner in the local tools folder and the workflow scripts in the local tools folder that embed lane steps (lead) | **Step 1, offline check** (in a scratch folder outside the repository, nothing merged; its `nuget.config` has only an empty local source, so any download fails visibly): (a) the Build Tools 2026 MSBuild with each installed SDK band (10.0.114 per user, 10.0.403 machine-wide) builds an SDK-style net48 class library and exe with no `Microsoft.Net.Sdk.Compilers.Toolset` download and no NETSDK1233; (b) the 2026 vstest.console runs an MSTest v1 DLL (NoHqTests, `/Platform:x86`) with the same counts as vstest 17.14, and MSBuild 18 resolves the MSTest v1 assembly through `ODAddMSTestV1SearchPath` in `Directory.Build.targets` (`$(VsInstallRoot)`; the 2026 instance has it under `Common7\IDE\Extensions\TestPlatform`, checked 2026-11-08); (c) the 2026 instance has the components of section 2.4 (`vswhere -products * -version [18.0,19.0) -requires` with their component ids), MSVC v143 included (`Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64`, added 2026-11-08); (d) the 2026 MSBuild builds a scratch copy of `Direct2dWrapper` (Release and Debug, Win32, without its post-build copy) with `PlatformToolset` v143, and the DLL links with linker 14.44. A failed part stops S1-0 and goes to the owner. **Step 2, toolchain switch** (lead): the lane and both test runners use the Build Tools 2026 MSBuild.exe and vstest.console.exe, found with the section 2.4 vswhere query and overridable by parameter; the five tool scripts find MSBuild the same way, never with an unranged `-latest`, and their "Visual Studio 2022" messages change; README's Building section names Visual Studio 2026 or Build Tools 2026 with the components of section 2.4 and the SDK; its command line uses the `Microsoft Visual Studio\18\BuildTools` folder (Visual Studio 2026 installs under `18`, not `2026`), and its install command adds the v143 component id. Then, on the unchanged classic solution, a lane build, a Full run (workflow) and a Release dry run, compared with the last Build Tools 2022 runs: the same results and the same staged file list. **Step 3, SDK pin:** choose the band from the step 1 results (section 2.4), write `global.json`, record the choice and the reason; the lane sets the SDK resolver variables only for the per-user band. **Step 4, baseline** with `tools/Get-ProjectBaseline.ps1` on the Build Tools 2026 toolchain, so compiler differences between MSBuild 17.14 and 18 do not mix with the conversion diff: per project `MSBuild -getItem:Compile -getItem:EmbeddedResource -getItem:Page -getItem:None -getItem:Content` (MSBuild 17.8+, https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties), every assembly's `GetManifestResourceNames()`, the file list plus SHA-256 of `OpenPmsDental\bin\Debug` and each test `bin\Debug`, and the content of every `*.exe.config` and `*.dll.config` there. Every record, and every re-record after a merge, is taken in a clean worktree after a full lane build and before any test run, because test runs leave files in the output folders that are not build output (`FreeDentalConfig.xml`, `EmailAttachments` and others): the record of `08f7f8ca6`, taken in the main checkout, listed 50 of them. The lead re-recorded it that way after TC-0 had merged (`c79ea6a38`, which adds `tests/ToothChartImageTests`), in `bffa11c7e`. **Step 5, props and restore:** props as in section 2.3, guarded for SDK projects. **Binding redirects:** the SDK switches on `AutoGenerateBindingRedirects` for .NET Framework exes that leave it empty (`Microsoft.NET.Sdk.BeforeCommon.targets`, https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.BeforeCommon.targets); OpenPmsDental, ServiceManager, OpenPmsDentalDbSettings, UpdateFileCopier, OpenPmsDentalHL7 and DatabaseIntegrityCheck leave it empty today, so the props set it to `false` when a project does not set it (dropped on 2026-11-08: without it no config changed, S1-5 row; MySQLManager, xCrudGenerator and the test projects keep their own value); `tests/LibraryBindingTests/BindingRedirectTests.cs` reads `OpenPmsDental.exe.config`, and the baseline diff of config contents is the check. `nuget.config`: `<clear/>`, one local folder source, empty `auditSources`, no fallback folders (https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file). Lane: `/restore -p:RestoreLockedMode=true`, `NUGET_CERT_REVOCATION_MODE=offline` in-process, `OPMS_REQUIRE_BUILD_OUTPUT=1`. SDK-style projects fail without restore (NETSDK1004, https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1004). The lead updates every workflow script in the local tools folder that embeds a lane step (19 of the 20 on 2026-11-08; found by grep for `Mono.Cecil`, `odbuild`, `run-tests` and `vstest`) to the new lane: the Visual Studio 2022 paths and the test runner usage. Their Mono.Cecil copy step stays as it is until S1-6 (the lane's locked restore does not restore the two `packages.config` projects, UnitTests and MiddleTierMockTests). | The step 1 results in the package report; the step 2 Full run and Release dry run; a lane build of the unchanged classic solution with `/restore` (must be a no-op) and a second baseline run equal to the first; baseline output committed as test data |
| **S1-T Tests that read project files** (parallel with S1-1, S1-2, but merges before both: two tests that S1-1's conversion breaks read literal `Compile` lines in files S1-T owns, ReportTests' `RdlSourceBuildTests.Sources_UseMySqlConnector_NotMySqlData` and VendorDirectTests' `DdeBridgesTests.Bridges_SendThroughTheInHouseDdeClient`; it also merges before S1-3, S1-5, S1-6) | the 27 test files with 86 literal `<Compile Include>`/`<Page Include>` assertions that the survey found (e.g. `tests/BERATests/WiringTests.cs:100-104`, `BPRINTTests/WiringTests.cs:113-224`, BTP, BDBM, CLAIMDCM, M2EF, M2NOTES, P262*, VBRIDGE1/2, VEDS, VGP, SshHostKeyTests, ReleaseFlowTests, ReportTests, PdfLibraryTests, `NoHqTests/CdtRetiredTests.cs`), plus `tests/VendorDirectTests/DdeBridges/DdeBridgesTests.cs`, newer than the survey, the two parsers using the 2003 namespace (`tests/ReleaseFlowTests/ReleaseFilesTests.cs`, `tests/LibraryBindingTests/TestSupport.cs:312`), `tests/ReleaseFlowTests/InstallerTests.cs:325-336`; the 16 files with "not built" Inconclusive checks | Replace literal-line checks with a shared helper that answers "is this file compiled by this project" for both formats (explicit item, or on disk under the folder and not removed); parse project XML namespace-agnostically; honour `OPMS_REQUIRE_BUILD_OUTPUT`. | the 27 files' assemblies and VendorDirectTests (DdeBridgesTests), against the still-classic projects |
| **S1-1 Leaf libraries and tools** | `CodeBase`, `ODCrypt`, `RdlEngine`, `RdlViewer`, `RdlDesign/ReportDesigner.csproj` (kept, decision 17; new in `OpenDental.sln` and the slnf, lead; it targeted .NET Framework 4.5, which Build Tools 2026 cannot build (MSB3644), and had not been built since its prebuilt `RdlDesigner.exe` left `Required dlls`, so it is first moved to 4.8, with the 4.8 sku in its `app.config`, and made to build as a classic project against today's RdlEngine and RdlViewer, then converted to `net48`), `UpdateFileCopier` (keep the 20 linked CodeBase files and the linked resx explicit), `DataConnectionBase`, `OpenPmsDentalStorage`, `PasswordVaultWrapper`, `OpenPmsDentalDbSettings`, `DatabaseIntegrityCheck`, `MySQLManager`, `ImagingDeviceManager`, `ODR`, `tools/SyntheticData` and `tools/AdoptionValidation` (`DefaultItemExcludes` for their nested test projects), `tools/DevDbTool`, `xCrudGenerator` (delete the csproj-editing code `CrudGenHelper.cs:694-697` and the `Microsoft.Build` reference: globbing makes it pointless); the `ODCrypt`/`PasswordVaultWrapper` source paths in `tools/Build-Release.ps1` (their output becomes flat `bin\Release`) | Convert. Keep only items with real metadata (`Link`, `LogicalName`, `CopyToOutputDirectory`, `DependentUpon`, generator items). Drop ClickOnce/bootstrapper properties, `ResGenToolArchitecture`, `LinuxRelease` configurations, empty build events, `System.Deployment`/`System.EnterpriseServices`/`System.Web.Extensions` references. Check the manifest name of UpdateFileCopier's linked `MsgBoxCopyPaste.resx` (checked by S1-1: it stays `UpdateFileCopier.MsgBoxCopyPaste.resources`, section 8 item 1). Keep the 2003 namespace on each converted `Project` element; the one sweep after the first wave removes it (Project files across packages). | CryptoTests, DbConnectionSecurityTests, ReportTests, VendorDirectTests (DdeBridgesTests, as S1-T rewrote it), AttributionTests (the RdlDesign branding rows), SshHostKeyTests, LibraryUpdateTests, ImagingDeviceManagerTests, SyntheticData.Tests, AdoptionValidation.Tests (build only; its run is a slow-tier locked run), ForkMigrationTests (DevDbTool), NoHqTests |
| **S1-2 Third-party source-build wrappers** | `third-party/build/*` (7 projects), `SourceBuild.props`, `SourceBuild.targets`, `PDFsharp.props` | Convert with `GenerateAssemblyInfo=false`, `DisableImplicitFrameworkDefines=true` (keeps today's hand-written netstandard2.0/net462 symbol sets), `SourceBuild.targets` imported after the SDK targets so `$(IntermediateOutputPath)` exists; `SourceBuildItems.txt` must not change. `DisableImplicitFrameworkDefines` makes S1-2 the one exception to the implicit symbols that stage 1 accepts (Full-test gate). The wrappers' references change, so the lock files of the converted projects that reference them (on the S1-1 branch: ImagingDeviceManager, ODR, DevDbTool, AdoptionValidation and xCrudGenerator) are re-created by whichever of S1-1 and S1-2 merges second (Project files across packages); at the first-wave integration (2026-11-08) the locked restore accepted every lock file without a refresh. | PdfLibraryTests, LibSpellTests, ImagingDeviceManagerTests |
| **S1-3 Business library** (after S1-T) | `OpenPmsDentalBusiness/OpenPmsDentalBusiness.csproj` | Convert: about 1,926 explicit Compile items and 11 folder globs become the default glob; keep the 3 `LogicalName` schema files, Settings, the remaining `Web References` `Reference.cs` files as normal source. Keep the `WebReferences`/`WebReferenceUrl` items: with `GenerateSerializationAssemblies` at `Auto` again (the revert of S0-2a), a Release build runs SGen only while a project has them, and both XmlSerializers files ship (critique W1; the Release dry run checks it). Drop the 3 unused WPF `Resource` gifs or diff the `.g.resources`. Until this conversion merged (`44f55ca54`), a new Crud or Data Interface file that CrudGenerator wrote needed a `Compile` item added by hand to the classic project: S1-1 deleted `CrudGenHelper.AddToOpenDentBusiness`, which added them, with its Microsoft.Build 4.0 reference; the default glob now compiles them. The solution's Release|Any CPU still maps to the project's Release|Any CPU, which the SDK-style project builds as x86 with SGen and the doc file; nothing builds that configuration (the lane and `tools/Build-Release.ps1` pass x86), as with S1-1's projects. The project's references change, so S1-3 re-creates the lock files of the converted projects on `main` that reference it, and S1-4 or TC-1, if it merges after S1-3, re-creates its own; if the 2003-namespace sweep has not run when S1-3 starts, S1-3 does it (both rules: Project files across packages). | Core subset run by the package: BSERVERTests, MiddleTierMockTests, BCLAIMTests, VendorDirectTests, ForkMigrationTests (schema LogicalName), NoHqTests, ReleaseFlowTests |
| **S1-4 UI libraries and services** (SparksToothChart moved to the chart track's TC-1) | `OpenPmsDentalGraph`, `WpfControlsOD` (remove 198 explicit `Page` items; `UseWPF`; make it x86 like the rest), `ImagingCapture` (remove its `**\*.xaml` and `**\*.cs` items), `OpenPmsDentalHL7`, `ServiceManager` | Convert. `UseWPF` with `net48` in Microsoft.NET.Sdk must be verified on the first project. Keep explicit framework `<Reference>` items for net48. | BPERIOTests, BPRINTTests, BSHELLTests, M2MISCTests, ODOPTSTests, ImagingCaptureTests, DicomReaderTests, CLAIMDCMTests, LibSpellTests |
| **S1-5 The program and the consumers of the exe** (after S1-T; best after S1-1..S1-4) | `OpenPmsDental/OpenPmsDental.csproj`, `UnitTestsCore`, `NoHqTests`, `CentralManager/xCentralManager.csproj` (kept, decision 17), `DocumentationBuilder/xDocumentationBuilder.csproj` (kept; new in `OpenDental.sln` and the slnf, lead) | Use explicit `Sdk.props`/`Sdk.targets` imports placed before the custom block so `ForkBuildStampFile` sees `$(IntermediateOutputPath)`. Keep `ForkWriteBuildStamp`, `CopyUpdateFileCopier`, the branding `LogicalName` resources, the WebView2Loader copy. The `Direct2dWrapper` post-build `xcopy` keeps working because the program's output stays `bin\Debug`. `FormReportComplex.resx` keeps its name (see section 8). DocumentationBuilder's HintPath to `OpenPmsDental\bin\Debug\OpenPmsDental.exe` becomes a ProjectReference, as CentralManager already has, so on .NET 10 it gets `OpenPmsDental.dll`; its ProjectReference to UnitTests stays. Binding redirects (lead, 2026-11-08): MSBuild's common targets give the classic net48 projects `AutoGenerateBindingRedirects=true`, and a cold build without the `false` that `Directory.Build.props` set for SDK-style projects changed no `.config` in any output folder (421 files byte for byte, and the baseline's `config-texts.txt`: no exe gets a suggested redirect today). So that override is gone, and the program (here), ServiceManager and the HL7 exe (S1-4) convert with the default `true` they build with today; the config-content diff still gates them. NoHqTests (and UnitTestsCore) drop the Compile items the default glob gives them but keep the explicit linked Compile items from outside their folder: `tests/ProjectFileTests/ProjectFile.cs` and `BuildOutput.cs` (and `TestDb.cs` where a project links it), which the glob does not reach (lead, 2026-11-08). The proof includes the `OPMS_CHECK_PROJECT_BASELINE=1` test of ProjectFileTests against a record taken after the conversion (section 3). | Full Core run by the workflow after merge (this is the largest project); BACCTTests, ApptTests, M2SHEETSTests, AttributionTests, ReleaseFlowTests, LibraryBindingTests (`.exe.config` content), InventoryTests and MaintenanceTests (their CentralManager source checks) |
| **S1-6 The test projects** (last) | `tests/*/*.csproj` (111: the 108 after stage 0, TC-0's `ToothChartImageTests`, S0-F's `S0FTests` and EHR-0's `EhrTests`), `tools/*/…Tests.csproj` (2), `UnitTests/UnitTests.csproj` (kept, S0-5; it is outside `tests/`, so it does not get `tests/Directory.Build.props` by itself: it imports that file without importing the root props twice, or carries the same settings, decided on its conversion), `UnitTests/packages.config` and `tests/MiddleTierMockTests/packages.config` (delete) | Scripted conversion to a 10-20 line project on top of `tests/Directory.Build.props`: remove the `**\*.cs` item (NETSDK1022 otherwise), keep the 36 `TestDb.cs` links, the explicit linked Compile items of `tests/ProjectFileTests/ProjectFile.cs` (30 test projects under `tests/`; NoHqTests, the 31st project that links it, converts in S1-5) and `BuildOutput.cs` (6 under `tests/`, and NoHqTests), which the default glob does not reach, and the copied data files (lead, 2026-11-08); add `GenerateBindingRedirectsOutputType=true` to `tests/Directory.Build.props`, beside its `AutoGenerateBindingRedirects`, because `Microsoft.Common.CurrentVersion.targets` sets it only for an exe or winexe and the test projects are libraries that set it in their own files today (LibraryBindingTests' `TestProjects_GenerateTheirRedirects_FromTheSharedTestConfig` checks both); the proof includes the `OPMS_CHECK_PROJECT_BASELINE=1` test of ProjectFileTests against a record taken after the conversion (section 3); the Mono.Cecil HintPaths in LibraryBindingTests, LibWebTests and UnitTests become a PackageReference (0.10.3 from the offline folder). MSTest v1 stays referenced by name through `Directory.Build.targets`' search path; if that path is not honoured in SDK-style projects, add a HintPath in `tests/Directory.Build.props` (verify on the first test project). | the workflow's Full run is the gate |

**Order and parallelism.** S1-0 alone. Then S1-T, S1-1, S1-2 in parallel (disjoint). S1-T merges first, before S1-1 and S1-2, because two
tests that S1-1's conversion breaks read literal `Compile` lines in files S1-T owns:
`RdlSourceBuildTests.Sources_UseMySqlConnector_NotMySqlData` in ReportTests and `DdeBridgesTests.Bridges_SendThroughTheInHouseDdeClient` in
VendorDirectTests. Then S1-3 and S1-4 in parallel. Then S1-5. Then S1-6. At most three writers at a time (the lane serialises builds).
PluginExample stays a classic project outside the slnf until S3b-4 rebuilds it for .NET 10. The lead adds RdlDesign (S1-1) and
DocumentationBuilder (S1-5) to `OpenDental.sln` and the slnf and updates the README line that calls them not built (196); from then on every
gate builds them. Neither ships.

**Project files across packages** (S1-1's findings, 2026-11-08):
- **The 2003 namespace.** Converted projects keep the 2003 MSBuild namespace (`xmlns`) on their `Project` element until S1-T merges, because
  today's project-file readers (`tests/LibraryBindingTests/TestSupport.cs`, `tests/ReleaseFlowTests/ReleaseFilesTests.cs`) select elements
  by it. S1-1 and S1-2 keep it even though they merge after S1-T. Once S1-T, S1-1 and S1-2 have merged, one sweep removes it from every
  converted project: the lead, or S1-3 if it comes first. Done by the lead after the first wave (2026-11-08): the 25 SDK-style projects
  and `third-party/build/SourceBuild.props`, `SourceBuild.targets` and `PDFsharp.props` have a `<Project>` element without the
  namespace, with the same evaluated items, compiler command lines and assemblies as before; projects converted from now on are
  written without it.
- **Lock files.** A converted project's `packages.lock.json` records its ProjectReference graph. When the references of a project it
  references change (S1-2's PdfSharp wrappers, S1-3's business library), the lane's locked restore fails with NU1004 until the lock files of
  the referencing projects are re-created: delete them and build through the lane, whose locked restore writes a missing lock file
  (section 2.4). The package that changes the referenced project refreshes them; at integration, when the two changes come from different
  packages, the second of them to merge does it.

`OpenPmsDentalServer` stays a classic Web Application project: Microsoft.NET.Sdk has no System.Web project type (community SDKs only,
https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb); it is replaced in 3b. `Direct2dWrapper.vcxproj` is unchanged (v143, built by
MSBuild 18 from S1-0).

**Full-test gate.** Section 3, plus: zero differences in the evaluated Compile/EmbeddedResource/Page items and the manifest resource names
against the baseline (except the deliberately removed ones); an identical output file list for `OpenPmsDental\bin\Debug`; identical
contents of every `*.exe.config` and `*.dll.config`. Expected and accepted: lock files of exes carry a `win-x86` section, because the SDK
gives every .NET Framework exe a default runtime identifier from its platform target
(https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.RuntimeIdentifierInference.targets).

Also accepted in stage 1 (S1-1, 2026-11-08): the SDK's defaults that the classic projects did not have, each proven harmless by the
package's assembly fingerprints before and after the conversion (Mono.Cecil: types, members, attributes, assembly references, resources).
They are the implicit .NET Framework references, the implicit symbols (`NETFRAMEWORK`, `NET48`, `NETxx_OR_GREATER`, and `RELEASE` in Release
builds; no source tests them) and transitive ProjectReferences. For the implicit symbols the exception is S1-2, which sets
`DisableImplicitFrameworkDefines=true` (its row). Every stage 1 package gives the same fingerprint proof.

Also accepted (S1-3, 2026-11-08): the default glob hands the compiler the source files in another order than the classic item list.
That changes compiler-generated names (anonymous types, display classes, lambdas, state machines), the metadata order of the
members of a partial type spread over several files, and the order in which such a type's field initializers run. S1-3 proved
it harmless for the business library: compiled in the classic order, the SDK-style project gave identical assemblies; no
initializer of a multi-file partial type depends on a field of another file; and the code that reads members by reflection sorts
them first. Each later package (S1-4, S1-5, S1-6, TC-1) gives the same proof for its own multi-file partial types, the program's
forms and their designer files among them, and compares fingerprints with compiler-generated names normalised.

Also accepted (S1-4, the lead, 2026-11-08): the SDK's own WPF build for `UseWPF` projects. It compiles the pages with
PresentationBuildTasks 10.0.14, the markup compiler stage 3b uses, instead of the .NET Framework's. The BAML resources keep their
names, counts and parsed node streams, and the generated partial classes, `InitializeComponent` and pack URIs are the same; the
BAML bytes differ (Boolean values stored as text, one more assembly record for the implicit Ribbon reference), the
`GeneratedCode("PresentationBuildTasks")` attributes say 10.0.14.0, and `.g.resources` grows a little. Forcing the old compiler
(`ImportFrameworkWinFXTargets=true` and no implicit WPF references) would give identical bytes now and have to be undone in 3b, so
it is not done. `EmbedUntrackedSources=false` and `ChecksumAlgorithm=SHA256` in `Directory.Build.props` keep the PDBs as the
classic build wrote them. The stage 1 gate's window checks open the WPF windows of WpfControlsOD and the capture windows to confirm
the pages load and look the same.

Also accepted (S1-5, the lead, 2026-11-08): the transitive project references put ODR, OpenPmsDentalDbSettings and (through
UnitTests) CrudGenerator files into the bin folders of CentralManager and DocumentationBuilder, and DocumentationBuilder's
Release folder now takes the Release program; neither tool ships, and the program's own output and every test folder are
unchanged. As with S1-1 and S1-3, the solution's Any CPU configurations of the program, CentralManager and UnitTestsCore now
build x86 (the program's Release|Any CPU with SGen and its doc file); nothing builds them. Visual Studio's form designer may
write `<Compile Update=...><SubType>Form</SubType>` entries back into a converted project file when a form is opened: that is
churn, not an edit to keep. `DocumentationBuilder/xDocumentationBuilder.sln` still names the dropped LinuxRelease
configurations; nothing uses it, and it stays.

Also accepted (S1-6, the lead, 2026-11-09): for the test projects, `tests/Directory.Build.props` turns two SDK defaults off, so
every test output folder holds exactly the files it held: `DisableTransitiveProjectReferences=true` (with the SDK's transitive
references the 27 test projects that reference the program would also get ODR and OpenPmsDentalDbSettings, 135 files in all) and
`EnableDefaultNoneItems=false` (only the listed None items; a test project that adds an `app.config` lists it, or
Directory.Build.targets skips its `tests\BindingRedirects.config` fallback). Mono.Cecil 0.10.3 as a package brings its Mdb, Pdb
and Rocks assemblies into the output folders of LibraryBindingTests, LibWebTests and MiddleTierMockTests (9 files, tests only).
The offline folder is not versioned: a fresh clone or another build PC puts Mono.Cecil 0.10.3 (SHA-256 in
`docs/port-notes/S1-6.md`) into its own `OPMS_NUGET_OFFLINE` folder, as README says.

**Tooling changes (switch at S1-0).** Lane: the Build Tools 2026 MSBuild and vstest (found with a version range, overridable by
parameter), `/restore` and locked mode, the SDK pin (and the resolver variables for the per-user band), `OPMS_REQUIRE_BUILD_OUTPUT`. Test
runners: vstest 18 from Build Tools 2026; paths otherwise unchanged (flat output). Build-Release, New-InitSql, New-DevDatabase and the two
tool scripts: MSBuild found with a version range instead of `-latest`; Build-Release also gains the restore switch and, in S1-1, the
ODCrypt/PasswordVaultWrapper paths. Setup: unchanged. README: the Building section. Workflow scripts in the local tools folder: updated
by the lead in S1-0; after S1-6 merges, the lead removes their Mono.Cecil copy step. Build Tools 2022 stay installed until this stage's
gate has passed.

**Downloads.** None. Build Tools 2026 and both SDKs are installed (2026-11-08). The owner added the MSVC v143 component with the Visual
Studio Installer on 2026-11-08 (not an offline-folder item). SDK-style `net48` adds
`Microsoft.NETFramework.ReferenceAssemblies` only when no targeting pack is installed, see
https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.FrameworkReferenceResolution.targets;
the Build Tools 2026 instance has the 4.8 targeting pack. The S1-0 offline check proves that no package download is needed.

**Risks and mitigations.**
- Silent item changes (globbing, duplicate items, resource names): the baseline diff is a gate, not a spot check.
- Silent config changes (automatic binding redirects): the config-content diff (S1-0's `AutoGenerateBindingRedirects=false` default was
  dropped on 2026-11-08 after a build without it changed no config, S1-5 row).
- Evaluation order of `$(IntermediateOutputPath)` (OpenPmsDental build stamp, SourceBuild.targets): explicit SDK imports; check that no
  generated `.cs` lands in a project folder (`git status` clean after a build).
- Implicit framework symbols changing third-party code paths: `DisableImplicitFrameworkDefines` in the wrappers; `SourceBuildItems.txt`
  guard tests.
- Platform mix-ups (WpfControlsOD and CryptoTests mapped to AnyCPU in the sln): `PlatformTarget=x86` centrally, sln mapping corrected by
  the lead.
- Restore needing the network: `nuget.config` clears all sources; a missing package is a build error, never a silent download.
- An SDK arriving by other means (a Visual Studio update brings a new machine-wide SDK): the `global.json` pin makes the build fail
  clearly or roll forward only as S1-0 decided; the bump procedure of section 2.4 follows every such update.
- The toolchain switch hiding a behaviour change (the MSBuild 18 compiler on the classic projects): S1-0 compares a Full run and a Release
  dry run of the unchanged solution with the last Build Tools 2022 runs before any conversion, and takes the baseline after the switch;
  Build Tools 2022 stay installed, so the lane can go back by parameter.
- A Visual Studio update during the stage (Modern Lifecycle): updates are installed only between packages, never during a gate.

**Size.** 8 packages; large; about 9-13 dev-days (OpenPmsDental, the business library and WpfControlsOD are the hard ones; S1-0 now
includes the toolchain switch).

---

### Stage 2 - NuGet replaces Required dlls (net48)

**Goal.** Every file in `Required dlls` that has a NuGet package comes from the offline folder through PackageReference with lock files;
what is left there has no package. No Microsoft package stays on a line whose support ends on 2026-11-10.

**Entry conditions.** Stage 1 merged and green. Download batch A approved and placed in the offline folder (see Downloads).

**Packages.**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S2-1 Central versions and the 26 identical packages** (first) | `Directory.Packages.props` (new), every `.csproj` with a `Required dlls` HintPath to one of the 26 files, `tests/Directory.Build.props` | Central Package Management (https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management); PackageReference for BouncyCastle.Cryptography 2.7.0, Google.Apis/Auth/Core 1.78.0, Google.Apis.Gmail.v1 1.77.0.4295, MailKit 4.18.2, MimeKit 4.18.2, Microsoft.Identity.Client 4.92.0, Newtonsoft.Json 13.0.4, SSH.NET 2026.0.0 and their transitive polyfills (all byte-identical to the current files, SHA-256 checked); MySqlConnector stays 2.3.7 here (identical bits are not in the feed, so it keeps its HintPath until S2-2); test projects get libraries transitively through ProjectReference; lock files committed. The project-file evaluator of the tests (`tests/ProjectFileTests/ProjectFile.cs`) does not read `Directory.Packages.props` yet (NuGet.props imports it): S2-1 models that import, its `PackageVersion` items and `ManagePackageVersionsCentrally`, or makes the reader refuse it, and adds those names to `tools/Test-ProjectFileEvaluator.ps1`, before it lands. **Note (S2-1 done, 2026-11-09; `docs/port-notes/S2-1.md`, sections 2, 4, 9 and 10):** 25 package versions (the 26th file, `System.Security.Cryptography.Pkcs`, has no user and no `PackageVersion`) and Mono.Cecil's in `Directory.Packages.props`, with transitive pinning; `System.ValueTuple` 4.6.2 restored as a dependency of System.Formats.Asn1 and System.Text.Json (its facade reaches 128 compilations and changes no output); the Google APIs `PrivateAssets="compile"` in the business layer; the reader models `Directory.Packages.props` and the restore's package imports. The lead's decisions (the owner leaves technical choices to the lead): (a) the System.Text.Json and Microsoft.Extensions.Logging.Abstractions source generators stay off repo-wide through stage 2 (`DisableSystemTextJsonSourceGenerator`, `DisableMicrosoftExtensionsLoggingSourceGenerator`): with them on, PdfSharp-gdi and MigraDoc.DocumentObjectModel fail with CS0757, as their `LoggerMessages.cs` is what the logging generator writes; S3b-1 decides whether the System.Text.Json switch goes. (b) The identical-output proof accepts new files under the rule below the table ("Identical output in stage 2"); S2-1's 28 (27 in 11 library folders, one in `tests\ImagingDeviceManagerTests`) are accepted, listed in the port note. (c) `tools/Get-ProjectBaseline.ps1` keeps recording the `PackageVersion` items per project (26 lines in each SDK-style project): the opt-in check compares per project, and the repetition is accepted. (d) The two forms of `OpenPmsDental.ContrTable.resources` found by the review are older than S2-1: open point O-17. | Diff of the output folders by SHA-256 before and after: identical, apart from new files under the rule "Identical output in stage 2" below; Core subset (DataConnection and e-mail tests) |
| **S2-2 MySqlConnector 2.6.2 and the Microsoft 10.0 packages** (after S2-1) | `Directory.Packages.props`, `DataConnectionBase` and any code that uses changed APIs; every project file with a MySqlConnector HintPath (112 at the survey, fewer after stage 0; they collapse into the transitive reference); the `app.config`/`Web.config` files whose manual binding redirects name the three Microsoft assemblies below, and `tests/BindingRedirects.config` | Upgrade MySqlConnector from 2.3.7 (advisory GHSA-473q-m89c-ghf8, https://github.com/advisories/GHSA-473q-m89c-ghf8). Review the 2.4-2.6 changes: `State=Broken` after network errors, `ResetConnectionAsync` throws `MySqlException`, stricter argument checks, `MySqlConnectorLogManager.Provider` obsolete (https://mysqlconnector.net/overview/version-history/). Move the three shipped Microsoft packages still on 8.0.x to the current 10.0 patch, which still targets net462: `Microsoft.Extensions.DependencyInjection.Abstractions` (8.0.2), `Microsoft.Extensions.Logging.Abstractions` (8.0.3), `System.Diagnostics.DiagnosticSource` (8.0.1). .NET 8 support ends on 2026-11-10 (https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core); the three ship today (`Build-Release.ps1:136-146`), and the bridge release would otherwise ship them months later. `System.Security.Cryptography.Pkcs` 8.0.1 is not shipped and nothing references it (`ReleaseListTests.cs:104-105`). It leaves `Required dlls` in stage 2 (S2-4) instead of being upgraded, and batch A drops it. The binding redirects follow the new assembly versions. **Database TLS check:** add a slow-tier test in `tests/DbConnectionSecurityTests` (skipped unless the lane passes the test server address and the two certificate thumbprints; the environment is open point O-8) that connects over a non-loopback address of the build PC to a local MySQL/MariaDB user created `REQUIRE X509`, with `SslMode=VerifyFull` and the office CA PEM, once with a client certificate imported through a CAPI key provider and once through a CNG key provider (`OpenPmsDentalDbSettings/ClientCertificateStore.cs:30-46,186-204`). Today's tests open only loopback connections, which the connection rule exempts from TLS. | Core run by the workflow (database-heavy); ForkMigrationTests, M201Tests (slow tier, locked); LibraryBindingTests (redirects); the TLS test |
| **S2-3 WebView2 package** (after S2-2, which edits the same project files) | `CodeBase`, `OpenPmsDental`, `WpfControlsOD`, `tests/LibWebTests`, `tests/M2IMGTests` project files; `Required dlls/WebView2Loader.dll` and the three WebView2 DLLs | PackageReference `Microsoft.Web.WebView2` 1.0.2420.47 (same bits); the package's build targets supply `WebView2Loader.dll` per RID, so the `None` item at `OpenPmsDental.csproj:9598` goes; check the loader lands flat in `bin\Debug` for x86 (https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution). | LibWebTests, M2IMGTests; open an eForm and the e-mail preview |
| **S2-4 Release tooling for packages** (after S2-3) | `tools/Build-Release.ps1` (ThirdPartyFiles 135-149, NativeFiles 194), `THIRD-PARTY-NOTICES.txt`, `Required dlls/vcruntime140.dll` (delete; unused, the setup installs vc_redist), `Required dlls/System.Security.Cryptography.Pkcs.dll` (delete; not shipped, nothing references it), tests that pin `Required dlls` (`tests/LibraryBindingTests/RequiredDllsTests.cs`, `ReleaseListTests.cs`, `tests/BINBTests/RemovedBinariesTests.cs`, `tests/LibraryUpdateTests/LibraryFilesTests.cs`, ReleaseFlowTests) | Compare staged third-party files with the package files named by the lock files instead of `Required dlls`; update the notices (MySqlConnector 2.6.2 and the three shipped 10.0 packages; remove the Pkcs sentence) and the Pkcs pins in `RequiredDllsTests.cs:31` and `ReleaseListTests.cs:104-105`. | ReleaseFlowTests, LibraryBindingTests, BINBTests, LibraryUpdateTests; `Build-Release.ps1 -ListFiles` diff |

**Identical output in stage 2 (the lead, 2026-11-09).** The proof of each stage 2 package and of the stage 2 gate (the SHA-256 diff of
every output folder before and after, and the Release dry run's staged list) accepts a file that is new in an output folder only when
it is byte for byte a file of `Required dlls` or the file of the same name in the package of the same id and version (the lock file's)
in the offline folder, and `tools/Build-Release.ps1` stages none of them. NuGet copies a package's whole closure into the output of a
.NET Framework project, where reference resolution copied only what it could bind, so such files appear in library and test folders.
Any other new file, and any file gone, is a difference the package removes; a changed file that is not one of the files that change on
every build (section 3) is explained by a source the package changed (the test assemblies that compile a changed test helper, say) or
removed. S2-1's 28 new files are accepted under this rule (`docs/port-notes/S2-1.md`, section 4).

**Left in `Required dlls` after stage 2:** SharpDX x3 and Tao x2 (to be retired by the planned WPF 3D tooth chart, `docs/roadmap.md`;
until then they run on x64 too, S3a-15), `Windows.winmd` (removed in 3a). Also the Health.Direct DLLs, the interop DLLs of QuickBooks
Desktop and Schick, and `NDde.dll` if it comes back: they are kept, and their port is to be planned (open point O-10). The QuickBooks
Desktop and Schick interop DLLs move to the 32-bit helper program (S3a-13, S3a-14), unless the vendor check finds a 64-bit way (O-14).

**Full-test gate.** Section 3, plus a release dry run with the new third-party source check, and the database TLS test run once against
the test server (CAPI and CNG; environment per O-8).

**Tooling changes.** Lane: locked restore is now load-bearing; a package that changes dependencies runs one unlocked restore in its
worktree and commits the new lock files. Build-Release: S2-4. Setup: none.

**Downloads (batch A).** `MySqlConnector` 2.6.2; `Microsoft.Web.WebView2` 1.0.2420.47 (fetching 1.0.4314.35 here instead is possible; the
plan keeps the same bits first and upgrades in 3b); `Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Logging.Abstractions`, `System.Diagnostics.DiagnosticSource` at the current 10.0 patch (10.0.14 at the time of
writing), with the net462 closure their lock files list. Not `System.Security.Cryptography.Pkcs`: it leaves `Required dlls` (S2-2).

**Risks and mitigations.**
- MySqlConnector behaviour changes on connection errors: the Core run and the reconnect/"connection lost" paths are exercised by the
  existing database tests; add one test that a killed connection reports `Broken` and the program's reconnect path recovers.
- The 10.0 polyfills on net48 change assembly versions: the manual binding redirects and `BindingRedirectTests` move with them.
- The TLS path differs per key provider and TLS version: the new test covers both providers; it runs again at the 3b gate. It runs
  on a temporary local server on `127.0.0.1:3307` (open point O-8, answered).
- The classic Middle Tier project may no longer get transitive package DLLs copied (NuGet has limited PackageReference support for ASP.NET
  Framework apps, https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files). It is not shipped and is
  replaced in 3b; it must still build. Do not deploy it from stages 2-3a.
- NuGet audit is silent with an offline-only source (https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages): the lead runs
  an online audit of the lock files elsewhere whenever the feed changes.

**Size.** 4 packages; medium; about 3-5 dev-days.

---

### Stage 4 - MSTest 4 (on net48, runs after stage 2 and before stage 3a)

**Goal.** All test projects, the legacy UnitTests project included, and `UnitTestsCore` run on MSTest 4 with the same results as on
MSTest v1.

**Entry conditions.** Stage 2 merged and green; per-assembly test counts and outcomes of the last v1 Full run recorded. Download batch B
approved.

**Packages.**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S4-1 Framework swap** | `tests/Directory.Build.props`, `Directory.Packages.props`, `UnitTestsCore/UnitTestsCore.csproj`, `UnitTests/UnitTests.csproj` (unless S1-6 made it import the shared test props), `Directory.Build.targets` (remove `ODAddMSTestV1SearchPath`; keep the binding-redirect parts while on net48), every test `.cs` that fails to compile | `MSTest.TestFramework` + `MSTest.TestAdapter` 4.5.x and `Microsoft.NET.Test.Sdk` in the shared test props; `UnitTestsCore` gets `MSTest.TestFramework` only. Apply Microsoft's v1-to-v3 guide (https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-migration-from-v1-to-v3) and then the v3-to-v4 guide (https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-migration-v3-v4); check the v4 list of removed APIs against the code before starting. Known now: `Assert.AreEqual` on differently typed values needs explicit types; the two `[Timeout]` tests (`tests/BPERIOTests/ContrPerioTests.cs:262,378`); the one `[ExpectedException]` (`tests/VPEARL2Tests/PearlApiClientLoggingTests.cs`) and the 20 `Assert.ThrowsException` uses in 20 files, which v4 replaces with `Assert.ThrowsExactly`; `ClassCleanup` (163 uses in 81 files): decide its timing explicitly and check that scratch databases are still dropped. | every assembly whose sources changed, then the workflow's Full run |
| **S4-2 Runners** | the build lane script (`-Test`), the test runner in the local tools folder, `tools/run-tests.ps1`; the workflow scripts in the local tools folder that call vstest or the runner (lead; found by the same grep as in S1-0) | vstest.console finds the MSTest adapter next to each test DLL; keep `/Platform:x86`; check on the first assembly that vstest.console 18.11 (Build Tools 2026, the runners' vstest since S1-0) runs the MSTest 4 adapter; keep both summary formats in the parser; compare discovered test counts per assembly with the v1 record; update the workflow scripts' test steps. | test runner `-Mode Targeted` on 5 assemblies; Full by the workflow |

**Order.** S4-1 then S4-2 (S4-2 is small; it can be prepared in parallel and merged second).

**Full-test gate.** Section 3, plus identical per-assembly discovered-test counts and outcomes compared to the v1 record (differences
explained one by one).

**Tooling changes.** Lane, both test runner copies and the workflow scripts (S4-2). Build-Release and setup: none.

**Downloads (batch B).** `MSTest.TestFramework` and `MSTest.TestAdapter` 4.5.x (the latest 4.5 patch at download time; 4.5.2 of
2026-10-27 was current when this plan was made and supports net462, https://www.nuget.org/packages/MSTest.TestFramework/);
`Microsoft.NET.Test.Sdk` (current stable at download); and the closure the lock files list (`MSTest.Analyzers`,
`Microsoft.Testing.Platform` and `Microsoft.Testing.Extensions.*` as applicable, `Microsoft.TestPlatform.TestHost`,
`Microsoft.CodeCoverage`). The lock files' `contentHash` values are the download manifest. The exact versions are recorded in
`Directory.Packages.props`.

**Risks and mitigations.**
- Tests that silently stop running (discovery differences): the per-assembly count comparison is part of the gate.
- APIs removed in v4: the compile errors list them; each replacement keeps the test's meaning (an exact exception type stays exact).
- Scratch-database lifetime changes with `ClassCleanup`: run the Full suite twice in a row; leftover `scratch_*` databases are a failure.

**Size.** 2 packages; medium; about 2-4 dev-days.

---

### Stage 3a - Prepare the code on net48

**Goal.** Make every change that the .NET 10 move needs and that also works on .NET Framework, prove it on net48, record the net48 golden
outputs, and ship it all as the last .NET Framework release (the bridge release). This includes the 64-bit preparation (wave 3,
decision 19), proven on the x86 build and in x64 test runs on .NET Framework.

**Entry conditions.** Stage 4 merged and green.

**Packages, wave 1 (parallel, disjoint).**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S3a-1 No more Thread.Abort** | `WpfControlsOD/UI/Progress/FrmProgressAuto.xaml.cs` (Abort at 125, 142 on the success path, 242, 248), `ProgressWin.cs`, `CodeBase/Utilities/ODThread.cs` (Join on timeout, 280), Join callers `FormOpenDental.cs:6840`, `ControlChart.cs:830`; `tests/MiddleTierMockTests/ODThreadTests.cs` (from S0-T; the two abort tests are rewritten for cooperative cancellation) | Cooperative cancellation: no abort on success; Cancel sets a flag the action checks, or closes the window and lets the background thread finish without database writes; `Join(timeout)` returns false instead of aborting. Thread.Abort always throws on .NET 5+ (https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0006). Dead `catch(ThreadAbortException)` blocks are left (harmless) and removed after 3b. The old Images module's abort (`ControlImagesOld.cs:3710`) and its two shell opens (308, 336) went with the module in S0-6. | new ProgressWin success/exception/cancel tests (WPF STA test), the ODThread tests; BPERIOTests, M2MISCTests |
| **S3a-2 Shell launching and elevation** (the helper merges first, as a small first commit) | `CodeBase/Utilities/ODFileUtils.cs` (Process.Start at 172, 190, 196, 307, 309 without `UseShellExecute`; `ProcessStart(Process)` at 162-164), every live `Process.Start(string…)` and `Process.Start(ProcessStartInfo)` site in shipped code except `PrefL.cs` (S3a-10). The list includes these: the lines in menu files, which wave 2 edits only after wave 1 merges (`ControlImages.cs:826,1304,1323`, `PopupHelper.cs:164,177`, `PopupHelper2.cs:162,175`, `FormClaimEdit.cs:1237,1242`, `FormSheetFillEdit.cs:258` and `FormRpTreatmentFinder.cs:204`); the executable starts, routed through `ODFileUtils.ProcessStart` (`Tesia.cs:55`, `Clearinghouses.cs:653,689`, `FrmAtoZFoldersCreate.xaml.cs:60`, `FrmDocInfo.xaml.cs:151`, `Dolphin.cs:30,70,73,78`, `Shining3D.cs:23`, `PracticeBooster.cs:22`, `Owandy.cs:47`, `One2.cs:55`, `MeditLink.cs:32`, `Guru.cs:219`, `Benco.cs:22`, `AiDental.cs:24`, `AaTemplate.cs:26,67` and `OpenPmsDentalBusiness/Shared/ODCloudClient/Sirona.cs:25`); the `net view` starts `UserControlServerConnections.cs:108`, `CentralConnections.cs:118` and `FormReportSetup.cs:128` (UseShellExecute=true keeps the hidden window); the X-Charge style `new ProcessStartInfo(path)` sites (`FormPayment.cs:3450,4036`, `FormCreditCardEdit.cs:594,775`, `FormCreditCardManage.cs:334`, `FormXchargeTokenTool.cs:114`, `RecurringCharges.cs:700`); the `new Process()` sites `FormClaimAttachSnipDXC.cs:241,257`, `FormClaimAttachment.cs:362,378` and `FormImageSelectClaimAttach.cs:425,438` (both whole files, including their `OutOfMemoryException` catches at 499 and 515), `FormCreditCardEdit.cs:614,798`, `FormCreditCardManage.cs:357`, `FormPayment.cs:3480,4149`, `FormXchargeTokenTool.cs:131`, `RecurringCharges.cs:748`, `Eclaims/WebMD.cs:42`, `UpdateFileCopier/FormMain.cs:429`, `WpfControlsOD/Bridges/Dolphin.cs:64`, `Office.cs:37`, `Progeny.cs:38,49`, `WpfControlsOD/Frms/FrmPatientSelect.xaml.cs:832`; once EHR-3 has merged, the starter's redirected `new Process()` in `OpenPmsDentalBusiness/Data Interface/EhrSummaryCcds.cs` (`EhrStylesheetStarter`, `docs/specs/ehr-rebuild-plan.md`), which sets `UseShellExecute=false` explicitly and is checked, not edited; the starts in the three kept unshipped programs (`RdlDesign/DialogAbout.cs:253`, `RdlDesign/DialogListOfStrings.cs:171`, `RdlDesign/RdlDesigner.cs:2425,2438,2494,3462`, `DocumentationBuilder/Form1.cs:409`, `CentralManager/Db/CentralConnectionHelper.cs:51`); new source-scan test file in NoHqTests | Every start that does not redirect output uses `UseShellExecute=true`, the .NET Framework default (https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute); new `ODFileUtils.ShellOpen(pathOrUrl)`. **Elevation:** with `UseShellExecute=false` (the .NET default) a start goes through CreateProcess, which refuses a program whose manifest asks for elevation (`ERROR_ELEVATION_REQUIRED`); only ShellExecute asks for consent (https://learn.microsoft.com/windows/security/application-security/application-control/user-account-control/architecture). So every `new Process()` start (22 live sites in 16 shipped files, one more in `EhrSummaryCcds.cs` once EHR-3 has merged, already explicit, one more in xCrudGenerator `CrudGenHelper.cs:683`, plus one commented out in `SheetPrinting.cs:479`; the PrefL and ServicesHelper ones belong to S3a-10 and S3a-7) and every `ODFileUtils.ProcessStart(Process)` call sets `UseShellExecute` explicitly: true unless it redirects standard streams. osk.exe (`FrmPatientSelect`) is a uiAccess binary and needs true. The `OutOfMemoryException` catches gain `catch(ExternalException)` as in S3a-9. Source-scan test: no `Process.Start(string…)` outside `ODFileUtils`, and every `new Process()`/`new ProcessStartInfo(` block sets `UseShellExecute` explicitly. The scan covers the shipped projects and RdlDesign, DocumentationBuilder and CentralManager, whose link and document opens would fail on .NET. It leaves out `tests/`, `tools/`, `UnitTests`, `UnitTestsCore` and `xCrudGenerator`, whose starts redirect output or never ship. The scan commit merges last in wave 1, after S3a-10. | the helper unit test (`UseShellExecute==true` for every overload), the source-scan test, VendorDirectTests, BIMGTests, BPAYTests |
| **S3a-3 Encodings and numbers** | `ProcCodeImports.cs:133`, `ProcCodesOther.cs:241`, `CodeBase/Utilities/ODZip.cs:43`, `EForms/EFormRichPaste.cs:82`, `FormPatienteBill.cs:100,811`, `Data Interface/EmailMessages.cs` (the charset sites left after S0-3; the whole file is owned here in 3a), `DataConnectionBase/SOut.cs`, `SIn.cs`, the money-formatting helpers, `X12/X837_5010.cs`, `X837_4010.cs`, `Eclaims/Ramq.cs`, `Data Interface/TreatPlans.cs:258-273`, `PayPlans.cs:910`; tests `tests/BPROCTests/ProcCodeImportTests.cs:120`, `tests/M2CANTests/OtherCodesTests.cs:237`, `tests/BINBTests/ODZipTests.cs:63` | Replace `Encoding.Default` (UTF-8 on .NET, https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding.default) with the explicit ANSI page `Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage)`; give machine-read numbers (X12, claim files, signature keys, exports) explicit formats with `InvariantCulture` and "G15" where text must match today; normalise negative zero in money formatting; `SIn.Double/Float` treat infinity as invalid (.NET parses overflow to infinity, https://learn.microsoft.com/en-us/dotnet/core/compatibility/3.0#floating-point-formatting-and-parsing-behavior-changed). All no-ops on net48. Code-page provider registration waits for 3b (section 8). EHR-5 (`docs/specs/ehr-rebuild-plan.md`) stores the QRDA I zip entries as UTF-8 (`QualityMeasures.cs:7860-7861`), so once it has merged `ODZip.GetDefaultEncodingBytes` has one caller fewer: `Eclaims/Ramq.cs` is then the only one in shipped code. | the three fixed tests (bytes built with 1252, no guard), BCLAIMTests, M2CLAIMFORMTests, BTPTests, PayPlanTests, StatementTests |
| **S3a-4 Golden outputs and known answers** | new `tests/GoldenOutputTests` project (+ sln/slnf entry, lead merges), fixture files | From one fixture database built by the existing scratch-database helpers: an 837 batch, statement PDF text, treatment-plan and pay-plan signature keys, the main CSV exports, `SOut.Float` SQL text for computed values, crypto known answers (`MiscUtils.Decrypt`, `CentralConnections.Decrypt`, a stored signature, RDL data-source decryption), a `wikilisthist.ListContent` written by net48, and the output of a fixture RDL report whose expression calls a VB `<Code>` function (for S3b-2, decision 14). Recorded with the net48 build and committed; the same tests compare byte for byte in 3b. | the new assembly |
| **S3a-5 PasswordVault to Credential Manager** | `PasswordVaultWrapper/*` (project file drops `Windows.winmd`), `Required dlls/Windows.winmd`, `tests/BINBTests` vault tests | Credential Manager P/Invoke (`CredWriteW`/`CredReadW`/`CredDeleteW`/`CredEnumerateW`), same public interface; on first use a one-time migration copies existing PasswordVault entries over (WinRT still works on net48), so nobody re-enters a password. .NET 5+ cannot consume `.winmd` (https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/5.0/built-in-support-for-winrt-removed). No TFM change, no download. | BINBTests; the migration test (an entry written through PasswordVault is readable through Credential Manager after first use, and the migration runs once) |
| **S3a-6 SOAP clients without System.Web.Services** | `OpenPmsDentalBusiness.csproj` (owner in 3a), `Web References/Dentalxchange2016/*`, `DentalxchangePartnerService/*`, `com.dentalxchange.webservices/*` (DCI), `NewCropWebService/*`, `EmdeonITS/*`, new `WebServices/SoapCaller.cs`, `Eclaims/ClaimConnect.cs` (304, 817 `SoapException`), `ElectIDs.cs`, `Eclaims/EmdeonMedical.cs` (`PutFileExt` at 44, `GetFile` at 103), `WebBridges/Erx/NewCrop/NewCrop.cs`, `NewCropConfig.cs`, `OpenPmsDentalBusiness/Properties/Settings*`, its `app.config`; `OpenPmsDental/Forms/FormInsPlan.cs` (only the `EligibilityCheckDentalXchange` region, 2011-2420); the unused ITS URL setting of the program (`OpenPmsDental/Properties/Settings.Designer.cs:38-41`, `Settings.settings`, its `app.config` entry); tests `tests/VDXC2Tests/FakeDentalXChange.cs`, `DxcPdfAttachmentTests.cs`, `tests/VendorDirectTests/DentalXChange/DxcPartnerIdentityTests.cs`, `Erx/NewCropAndThrottleTests.cs`, `tests/CLAIMDCMTests/WiringTests.cs:134`, new ITS tests, new `tests/VendorDirectTests/DentalXChange/DciEligibilityTests.cs` with a recorded response file | Keep the generated `[XmlType]` data classes, delete the `SoapHttpClientProtocol` classes, and send the 14 used document/literal operations (DentalXChange DWS 4, DEA 6, NewCrop 2, Change Healthcare ITS 2) through one `HttpClient` + `XmlSerializer` SOAP caller that reproduces each method's `SoapDocumentMethod` wrapper. ITS is kept and must work on .NET 10 although the office does not use it today (decision 12). **DCI eligibility** (the Practice-Web path, kept: decision 12, open point O-6) has one used operation, `lookupEligibility` (`FormInsPlan.cs:2055`). Its proxy is rpc/encoded (`SoapRpcMethod`), so the same caller writes that body by hand: the `lookupEligibility` element in the namespace `http://www.dentalxchange.com/webservice`, with the parts `in0` (`Credentials`) and `in1` (`Request`) typed as the old proxy types them; it reads `content` from `lookupEligibilityReturn`. Its three `[SoapType]` data classes stay, the `WebServiceService` proxy goes, the `FormInsPlan` region calls the new caller, and the URL setting `OpenDentBusiness_com_dentalxchange_webservices_WebServiceService` stays. Before deleting the old proxies, a test captures the envelope each old proxy sends (through the existing `WebRequest.RegisterPrefix` fake) and asserts the new caller's envelope is equivalent, for every operation including ITS's two and DCI's one. The VDXC2 fake becomes an `HttpMessageHandler`; a new fake ITS endpoint checks the request the caller builds (login, message type, file name, zipped payload) and parses an `ItsReturn`. The new DCI test checks the rpc/encoded request body the caller builds (element, namespace, parts and their types) and parses a recorded `lookupEligibility` response into the `content` that `ProcessEligibilityResponseDentalXchange` reads. Removing these `WebReferenceUrl` items must keep `OpenPmsDentalBusiness.XmlSerializers.dll` (critique W1): the Middle Tier's web reference stays until 3b, so SGen still runs at `Auto`, and the Release dry run checks that both XmlSerializers files are still staged. This route differs from regenerating with `dotnet-svcutil`; see section 8. The Middle Tier's own proxy stays until 3b. | VDXC2Tests, VendorDirectTests, CLAIMDCMTests, BCLAIMTests, the envelope-equivalence tests, the ITS request test, the DCI request and response tests |
| **S3a-7 Windows services without InstallUtil** | `CodeBase/Utilities/ServicesHelper.cs` (InstallUtil at 18-25, 68, 114; its `new Process()` at 30), `OpenPmsDentalHL7/ProjectInstaller.cs` (delete), `OpenPmsDentalHL7.csproj` (`System.Configuration.Install` reference), `ServiceManager/FormServiceManage.cs:115,154`; tests `tests/ReleaseFlowTests/ProgramNamesTests.cs` (183-196, and S3a-8's exe-loading fix at 191), `tests/BINBTests/RemovedBinariesTests.cs:139-143` | Install and remove services with `sc.exe` (or `CreateService`/`DeleteService` P/Invoke); `System.Configuration.Install` does not exist on .NET (https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable) and InstallUtil cannot load a .NET 10 assembly. The `ServicesHelper.Install`/`Uninstall` signatures stay, so the caller `OpenPmsDental/Logic/PrefL.cs:488` needs no edit (if it must change, the edit goes into S3a-10's brief). Elevation: ServiceManager runs elevated by its manifest; the program's call keeps today's "try running as admin" message. | ReleaseFlowTests, BINBTests; manual: install and remove the HL7 service from ServiceManager on a test PC |
| **S3a-8 Tests that use .NET Framework-only APIs** | AppDomain probes (`NoHqTests/RemovedAssembliesTests.cs`, `tests/DicomReaderTests/DicomPixelReaderChainTests.cs`, `tests/LibSpellTests/TestSupport.cs` + `SpellCheckProbe.cs`, `tests/VendorDirectTests/BridgesRemoval/BridgesDllRemovedTests.cs`, `tests/PdfLibraryTests/IsolatedPdfRun.cs`), ReflectionOnly loads (`NoHqTests/RenameGuardTests.cs`, `tests/BINATests/RepoFiles.cs`, `tests/BINBTests/CompiledReferencesTests.cs`, `tests/LibraryUpdateTests/LibraryFilesTests.cs`, `VendorDirectTests/PayConnect/PayConnect1RemovedTests.cs`, `tests/VendorDirectTests/DdeBridges/DdeBridgesTests.cs`, which S0-R made of `DdeBridgesRemovedTests.cs`), `tests/LibraryBindingTests/RequiredDllsTests.cs:75` (GAC), `tests/AttributionTests/ReleaseIdentityTests.cs:96`, `AttributionTests.cs:52`, `tests/ImagingCaptureTests/InteropLayoutTests.cs:114,137`, the exe-loading tests `NoHqTests/CdtRetiredTests.cs:108` and `tests/ReleaseFlowTests/ReleaseManifestTests.cs:219-224` | Read assembly references with Mono.Cecil (already used by the tests; no new package) instead of `ReflectionOnlyLoad`; run the probes in-process or in a child process instead of a new AppDomain (AppDomain creation throws on .NET, https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable); `AssemblyBuilder.DefineDynamicAssembly`; delete the GAC assertion; assert the COM method order from a list; load "the program's managed assembly" through a helper that takes `OpenPmsDental.dll` when present and the `.exe` otherwise (S3a-7 applies the same helper in `ProgramNamesTests.cs:191`). The binding-redirect tests stay until 3b (still meaningful on net48). | each touched assembly |
| **S3a-9 Small API fixes** | `OpenPmsDentalDbSettings/OfficeCaFile.cs`, `DbSettingsRunner.cs`, `ClientCertificateStore.cs`, `tests/DbConnectionSecurityTests/ToolTests.cs`, `OwnerAnswersTests.cs`; `SheetFramework/SheetPrintingJob.cs:498`, `Imaging/DicomImageReader.cs:204`, `SheetFramework/SheetDrawingJob.cs:857`, `tests/ImagingDeviceManagerTests/CaptureSessionFilesTests.cs:121`; network guards `tools/DevDbTool/NetworkGuard.cs`, `tools/SyntheticData/Infrastructure/LocalOnly.cs`; `OpenPmsDentalBusiness/TableTypes/Pref.cs`, new `NoHqTests/ObsoletePrefNamesTests.cs`; `OpenPmsDentalBusiness/Email/SendEmails.cs` (142-150, 259-268), `CodeBase/Utilities/EmailUtils.cs:16-18`, `Data Interface/EmailAddresses.cs:221`, new `tests/LibraryUpdateTests/AddressRulesTests.cs` | (i) Static `File/Directory.Get/SetAccessControl` to the `FileInfo`/`DirectoryInfo` instance form, which compiles on both runtimes (https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions). (ii) The `OutOfMemoryException` catches gain `catch(ExternalException)` (https://learn.microsoft.com/en-us/dotnet/core/compatibility/windows-forms/10.0/system-drawing-outofmemory-externalexception). (iii) The network guards also set `HttpClient.DefaultProxy`. (iv) **XmlSerializer and obsolete members:** on .NET 10 `XmlSerializer` serializes `[Obsolete]` members and throws while building a serializer for a member marked `IsError=true` (https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/10/xmlserializer-obsolete-properties); enum constants count. `Pref.cs` has 25 `[Obsolete("…",true)]` members of `enum PrefName` (for example lines 93, 241, 728), and `PrefName` travels in 15 `Meth.*` calls in 4 files (`Prefs.cs` 12, `PrefC.cs:273`, `LanguagePats.cs:181`, and `ClinicPrefs.cs:267` with a `List<PrefName>`) and in `ClinicPref.PrefName`: every such Middle Tier call would throw. Change the 25 to `IsError=false`; on net48 nothing changes (`XmlSerializer` ignores obsolete members there). The new tests replace the lost compile error: a source scan that no code outside `Pref.cs` references any of the 25 as `PrefName.<name>` (strings in the convert scripts and comments are allowed); and a reflection test that no member of `PrefName` carries an `ObsoleteAttribute` with `IsError==true`, so a newly ported upstream preference marked that way fails on net48 and not only at 3b. The alternative, the AppContext switch that restores "ignore obsolete members", would have to be set in every process that serializes (program, server, tools, tests). (v) **Addresses with consecutive dots:** .NET 10's `MailAddress` rejects them (https://learn.microsoft.com/en-us/dotnet/core/compatibility/10, Networking); the sending and validation sites listed catch the `FormatException` and tell the user the address is not valid instead of failing; the new test pins today's result for `a..b@example.com` and is updated to the .NET 10 result in 3b. | DbConnectionSecurityTests, ImagingDeviceManagerTests, DevDbTool/SyntheticData guard tests, NoHqTests, LibraryUpdateTests, MiddleTierMockTests |
| **S3a-10 Release-channel hardening (bridge features)** | `OpenPmsDentalBusiness/Misc/ForkRelease/ForkReleases.cs`, `ReleaseManifest.cs`, `OpenPmsDental/Logic/PrefL.cs` (the whole file in 3a), `tests/ReleaseFlowTests` except `ProgramNamesTests.cs` (S3a-7), `ReleaseManifestTests.cs` (S3a-8) and `InstallerTests.cs` (S3a-11) | (i) An unreadable or foreign manifest means "refuse and tell the user", not "nothing is published" (today `GetPublishedManifest` returns null at 405-411 and `Decide(null,…)` publishes for the update server, 237-242). (ii) Accept a new optional manifest key naming the runtime a release carries (for example `Runtime=Microsoft.NETCore.App 10.0.14 win-x64`): the .NET 10 release is self-contained and brings its runtime as a separate part of the release (decision 20, S3b-7). The keys of that runtime part (its version, architecture, archive SHA-256 and file list) are defined here too, so the bridge release reads a .NET 10 manifest instead of calling it unreadable. A release whose key names another runtime than this install's (.NET Framework) or another architecture than this install's process (the bridge release is x86, the .NET 10 release x64, decision 19) is refused with "run the new setup": it needs the new setup and the other install folder. (iii) Pull copies every file UpdateFileCopier needs, not only the exe (prepares for 3b). (iv) **Elevated copier:** `UpdateFileCopier/app.manifest` asks for `requireAdministrator`, and `PrefL.cs:592-596` starts it with the default `UseShellExecute`; on .NET that is false, CreateProcess refuses the start, and the program would end with `FriendlyException` and `Environment.Exit(305)` (597-601), so every self-update between two .NET 10 builds would fail on a standard workstation. Set `UseShellExecute=true` (the net48 default, so nothing changes today); UAC then asks for consent as it does now. (v) **Apphost resources:** a new test reads `RT_MANIFEST` (requested execution level), `RT_VERSION` (file and product version) and `RT_GROUP_ICON` (present, same icon bytes as the net48 record) from every staged `.exe`; record today's values from the net48 Release build (`requireAdministrator` for OpenPmsDentalDbSettings, ServiceManager and UpdateFileCopier; FileVersion 24.3.63.0 on `OpenPmsDental.exe`, `ReleaseFilesTests.cs:104`), so 3b proves the apphosts carry them. | ReleaseFlowTests (new cases for both refusals, the elevated start and the resource test); launch check: self-update from a standard user |
| **S3a-11 Setup without the per-user mode** | `installer/OpenPmsDental.iss`, `tools/Test-Installer.ps1`, `tests/ReleaseFlowTests/InstallerTests.cs`; `docs/release-process.md` (the administrative-install and installer-test paragraphs, 166 and 170) and `README.md` (the `Test-Installer.ps1` description in the tools table, 190); S3b-7 later adds the 64-bit install and the self-contained runtime to both | The setup always runs as administrator (decision 7): remove `PrivilegesRequiredOverridesAllowed=commandline` (line 83; `PrivilegesRequired=admin` at 82 stays), the `/CURRENTUSER` documentation (33), and the branches only a per-user install reached (`IsAdminInstallMode` in `SetUpConnection` 669-676 and 692-693, the security page condition at 801). `{autodesktop}` then always means the common desktop. Reason: a per-user install could never run the connection settings tool, and the owner does not need the mode (decision 7). The first reason given, that the .NET 10 Desktop Runtime installs machine-wide and needs administrator rights, went with decision 20 (the setup installs no runtime); the 64-bit install into `Program Files`, the Visual C++ runtime and the 64-bit TWAINDSM (3b) need administrator rights too. `Test-Installer.ps1` today installs per user without rights (`/CURRENTUSER`, line 67) and checks the per-user uninstall entry under HKCU (40, 138-155). It becomes an elevated test: it refuses to start unelevated; it still installs into new `%TEMP%` folders with `/DIR=`; it reads the uninstall entry under `HKLM\SOFTWARE\WOW6432Node\...` (the 32-bit install; S3b-7 moves it to the 64-bit view); scenarios A and B use a loopback database server, so the connection settings tool is not needed; a new scenario gives a network server and checks that the silent setup runs the tool `/silent` and ends with exit code 10 when the tool fails (no office CA); and it checks what the uninstall leaves (the Event Log source key per its `[Registry]` flags). Who runs it, and where, is open point O-3. The two documents describe an elevated test install and drop the per-user sentences. | ReleaseFlowTests (InstallerTests: no `PrivilegesRequiredOverridesAllowed`, no `/CURRENTUSER`); one elevated `Test-Installer.ps1` run |

**Packages, wave 2 (after wave 1 merges; WinForms menus and remaining UI).**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S3a-M1 Shared menu code** (first) | `OpenPmsDentalBusiness/UI/GridOD.cs` (1934-2038), `GridOld.cs` (1466-1551), `PopupHelper.cs` and `WpfControlsOD/UI/Controls/Supplemental/PopupHelper2.cs` (the menu code; their shell lines, `PopupHelper.cs:164,177` and `PopupHelper2.cs:162,175`, are S3a-2's and merge in wave 1), `OpenPmsDental/Logic/Lan.cs` (33-154), `UI/ODToolBarButton.cs`, `UI/ToolBarOD.cs`, `Data Interface/SheetFields.cs:287-289`, `ODR/ComboBoxMulti.cs` | `ContextMenu`/`MenuItem`/`Menu` become `ContextMenuStrip`/`ToolStripMenuItem`/`ToolStripDropDown` (`.MenuItems` to `.Items`, `Popup` to `Opening`); `DropDownMenu` moves to the half-built `ContextMenuStripDropDown`; `Lan.C` translates ToolStrip items. These types are binary-compatibility shims on .NET 10 that "can't be instantiated" (WFDEV006, https://learn.microsoft.com/en-us/dotnet/desktop/winforms/wfdev-diagnostics/wfdev006). | GridOD tests, BACCTTests, M2SHELLTests; screenshots of grid right-click menus and toolbar drop-downs |
| **S3a-M2 Program modules** (after M1; parallel with M3) | `FormOpenDental.Designer.cs`/`.cs`, `ControlAccount.*`, `ControlChart.*`, `ControlAppt.*`, `ControlFamily.*`, `ControlTreat.*`, `ControlImages.*`, `ControlImageDisplay.*` (also its clipboard fix: put `NodeTypeAndKey` on the clipboard as a JSON string under a custom format, which needs no BinaryFormatter, https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/winforms-wpf-ole-guidance), `UserControlTasks.*`, `EmailPreviewControl.*`, `ODtextBox.cs`, `UserControlSecurityUserGroup.*` | Same conversion. | ApptTests, BAPPTTests, BACCTTests, BIMGTests (+ its wiring test at `tests/BIMGTests/WiringTests.cs:115`), BTASKTests; in-process clipboard round-trip test |
| **S3a-M3 Remaining forms, grids, main menus** (after M1; parallel with M2) | the remaining files of the 102 source files that use WinForms `ContextMenu`/`MenuItem`/`Menu` after stage 0 (104 at the survey: OpenPmsDental 96, OpenPmsDentalBusiness 4, ODR 1, RdlDesign 2 and CentralManager 1; S0-6 deletes `ControlImagesOld.cs` with its designer file), plus `UnitTests/UI Testing/FormTestAllControls.cs` and its designer file, which the survey did not count (Forms, Forms Reports, ReportingForRdl, `FormMedical.cs`, `FormQueryMonitor.cs`; RdlDesign's `DesignCtl.cs` and `RdlDesigner.cs` construct these types 209 times), `Forms Reports/FormQuery.designer.cs`/`.cs` (DataGrid to DataGridView or GridOD); the WinForms `DataGrid` in 10 RdlDesign files (`DataSetsCtl.cs`, `DataSetRowsCtl.cs`, `DialogValidValues.cs`, `DrillParametersDialog.cs`, `GroupingCtl.cs`, `ModulesClassesCtl.cs`, `QueryParametersCtl.cs`, `SortingCtl.cs`, `SubreportCtl.cs` and its custom column style `DGCBColumn.cs`) to DataGridView; the source-text tests `tests/P262BillingTests/WiringSourceTests.cs:48-69`, `tests/M2SDVTests/ServiceDateViewUiSourceTests.cs:35`, `tests/BACCTTests/WindowTextTests.cs:58`; new NoHqTests source-scan test that fails on any WinForms `ContextMenu`/`MenuItem`/`MainMenu`/`DataGrid`/`Control.ContextMenu` use | Same conversion. Spot check: 272 fully qualified constructions of these types in 50 files at the survey (some are in CentralManager and UnitTests, which are kept, and some were in `ControlImagesOld.designer.cs`, deleted in S0-6). | P262BillingTests, M2SDVTests, BACCTTests, M2HISTTests, the source-scan test; the menus and grid dialogs of RdlDesign and CentralManager launched once (section 6) |

**Packages, wave 3 (64-bit preparation, decision 19; after wave 1 merges, in parallel with wave 2).**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S3a-12 Pointer-size-safe native code and registry views** (first in wave 3) | TWAIN: `ImagingDeviceManager/TwainProtocol/NativeMethods.cs`, `NativeTwainDsm.cs`, `ProxyWindow.cs`, `TwainStructs.cs`, `TwainSessionStop.cs` (the `MSG` reads, 229-242), `TwainSessionCaptureTransfer.cs` (the `TW_INFO` reads, 576-621); DirectShow: `ImagingCapture/Uvc/Interop/DsStructs.cs`, `Uvc/DirectShow/DirectShowFrameSource.cs` (678-679); window longs: `OpenPmsDental/UI/BordersDpi/FormODBase.cs:599-610`, `WpfControlsOD/Frame/FormFrame.cs:445-455`, `OpenPmsDental/Forms/FormPopupFade.cs:42-46`; `SendMessage` declarations `CodeBase/Utilities/MiscUtils.cs:26-27`, `WindowsApiWrapper.cs:721-730`, `WpfControlsOD/Bridges/Owandy.cs:26-27`; the `Direct2dWrapper` entry points in `OpenPmsDental/UI/Direct2d/Direct2d.cs` (68); vendor registry keys `WpfControlsOD/Bridges/Trojan.cs:31`, `OpenPmsDental/Forms/FormInsPlan.cs:1373`; system folders `OpenPmsDental/Forms/FormClaimAttachment.cs` (350, 373-384), `FormClaimAttachSnipDXC.cs` (229, 252-263), `FormImageSelectClaimAttach.cs` (413, 437-445), `WpfControlsOD/Frms/FrmPatientSelect.xaml.cs:826-845` (all four were S3a-2's in wave 1); any other `DllImport` the review finds wrong (listed in the report); tests `tests/ImagingDeviceManagerTests/TwainProtocolTests.cs` (15-52), `tests/ImagingCaptureTests/InteropLayoutTests.cs` (17-59), a new NoHqTests source-scan file; the lane's x64 test build and the test runners' `/Platform:x64` option (lead) | **Review** every P/Invoke declaration in shipped code and every structure passed to native code for what breaks in a 64-bit process: `int` or `uint` where Windows has a pointer, a handle, `WPARAM`, `LPARAM`, `LONG_PTR` or `SIZE_T`; `ToInt32()` on such a value (it throws `OverflowException` in a 64-bit process when the value does not fit); hand-coded sizes and offsets. Listed as checked: `SetSystemTime` in `OpenPmsDentalBusiness/Misc/WindowsTime.cs` (EHR-0) and `SetErrorMode` in `OpenPmsDental/Main Modules/EhrRenderChild.cs` (EHR-3), from `docs/specs/ehr-rebuild-plan.md`. **TWAIN, above all.** Check each structure against the TWAIN 2.x header: two-byte packing on Windows in both bitnesses; `TW_HANDLE`, `TW_MEMREF` and `TW_UINTPTR` are 8 bytes in a 64-bit process, `TW_UINT32` stays 4. `TW_INFO.Item` is a `TW_UINTPTR`, so the rule in `TwExtImageInfo.IsItemHandle` for data that fits in the field is checked for an 8-byte field. The hand-coded `MSG` offsets (`NativeMethods.cs:18-22`, the 28-byte 32-bit layout) become the offsets of a declared `MSG` structure. `SetWindowLong` with `ToInt32()` on a window handle (`NativeMethods.cs:90-91`, `ProxyWindow.cs:41`) becomes `SetWindowLongPtr` in a 64-bit process. `NativeTwainDsm.Load` stops refusing a 64-bit process (`NativeTwainDsm.cs:39-42`); a 64-bit process loads only `TWAINDSM.dll` from the system folder, never `twain_32.dll`, which is 32-bit only (24-33). The DirectShow structures of ImagingCapture (natural packing, `IntPtr` fields) are checked the same way. **Window longs:** a pointer-sized value goes through `SetWindowLongPtr`/`GetWindowLongPtr` in a 64-bit process and `SetWindowLong`/`GetWindowLong` in a 32-bit one, as the Windows headers define them; style bits may stay on the 32-bit entry points. **Registry views:** a vendor key that a 32-bit vendor installer wrote, and that a 32-bit process reads today through WOW64 redirection (`Software\TROJAN BENEFIT SERVICE`), is read with `RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32)`, as `LocalDiagnostics.cs:289` already does, or from both views when the vendor may write either; no code builds a `WOW6432Node` path. `SYSTEM\CurrentControlSet\Services` (ServicesHelper, HL7, ServiceManager) and the `HKEY_CLASSES_ROOT` lookups are listed as checked. RdlDesign's ODBC data source list (`DesignerUtility.cs:86`) shows the DSNs of its own bitness, the ones it can use (noted, no change). **System folders:** `sysnative` exists only for a 32-bit process; the Snipping Tool starts choose by `Environment.Is64BitProcess` (`FormImageSelectClaimAttach.cs:440` has today's test reversed); `Wow64DisableWow64FsRedirection` runs only in a 32-bit process on 64-bit Windows. **Tests in both bitnesses:** expected sizes and offsets come from a table with an x86 and an x64 column (TWAIN 2.x header, Windows SDK), chosen by `IntPtr.Size`; today both test files assert `IntPtr.Size==4`. The lane builds `ImagingDeviceManagerTests` and `ImagingCaptureTests` a second time for x64 on net48 (`-p:PlatformTarget=x64`, output `bin\x64\$(Configuration)\`, its own intermediate folder), and the test runners run them with `/Platform:x64` on the 64-bit .NET Framework 4.8; the TWAIN protocol tests run against `FakeTwainDsm` in both bitnesses. Every change is a no-op for today's x86 build. | TwainProtocolTests and InteropLayoutTests in x86 and x64; ImagingDeviceManagerTests and ImagingCaptureTests in both bitnesses; the source scan (no `IntPtr.Size==4` gate, no `ToInt32()` on a handle or pointer passed to native code, no 32-bit window-long entry point for a pointer, every `Registry.LocalMachine` read of a vendor key names its view); BSHELLTests, M2IMGTests, BPRINTTests; launch check: a TWAIN scan on the x86 build (section 6) |
| **S3a-13 The 32-bit helper program and QuickBooks Desktop** (after wave 1; starts with its vendor check) | new project `OpenPmsDentalHelper32/` (x86 WinExe with no main window, `OpenPmsDentalHelper32.exe`; sln and slnf entries by the lead), with the protocol in one source file, `OpenPmsDentalHelper32/Helper32Protocol.cs`, linked into CodeBase (one `Compile` item with `Link` in `CodeBase/xCodeBase.csproj`); new `CodeBase/Utilities/Helper32Client.cs`; `WpfControlsOD/Bridges/QuickBooks.cs`; the `Interop.QBFC10` references of `OpenPmsDental.csproj:114-118` and `WpfControlsOD.csproj:53-56` (they move to the helper project); `tools/Build-Release.ps1` (`$OwnFiles`: the helper and its config file; its PE check expects 0x14C for the helper, also after 3b); `THIRD-PARTY-NOTICES.txt` if the QBFC entry changes; new `tests/Helper32Tests` (sln and slnf, lead); a new ReleaseFlowTests file | **Vendor check first** (documentation only, no download): whether Intuit ships a 64-bit QBFC that a 64-bit program can use; the result goes to open point O-14. Until the owner chooses otherwise, QuickBooks Desktop gets a helper module. **The helper.** One small 32-bit program for every kept 32-bit-only component (this package: the host and QuickBooks Desktop; S3a-14: Schick CDR, Topaz, Guru). The program starts it from its own folder on first use, one helper per program process, for the same user; they talk over a named pipe that only the current user may open, one request and one reply at a time. The helper exits when the program's process ends. A call has a timeout and then fails with a clear message; a helper that crashes is reported once and started again on the next call; a missing helper gives a clear message instead of a crash. The helper references no other project of this repository (the protocol file is linked), so it can have another runtime than the program after the switch: its own self-contained x86 .NET 10 runtime in its own sub folder (O-15, decision 20). In 3a it is built like everything else (net48, x86), and the bridge release already uses it, so the helper path is proven on the known runtime. **QuickBooks Desktop.** The helper's QuickBooks module runs the QBFC session and the requests `QuickBooks.cs` builds today (`OpenConnection`, `DoRequests`, `CloseConnection` and the request methods), with the same arguments and results. The public methods of `QuickBooks` stay, so `FormQuickBooksSetup`, `FormQBAccountSelect`, `FormDepositEdit` and `FormAccountPick` do not change. After this package, no project but the helper references `Interop.QBFC10`. | `tests/Helper32Tests`: the protocol with a fake module (start, call, reply, error, a crash during a call, timeout, a missing helper, no helper process left after the program's process ends, a second program process gets its own helper); the QuickBooks module against a fake of the QBFC session interfaces that records each call, pinned to the calls today's code makes; ReleaseFlowTests (the helper ships and is x86); VendorDirectTests (QuickBooks Online is untouched); launch check where a test PC has QuickBooks Desktop (none today, O-14) |
| **S3a-14 Schick CDR, Topaz and Guru through the helper** (after S3a-13; its `ControlImages.cs` lines after S3a-M2) | `WpfControlsOD/Bridges/Schick.cs`, `WpfControlsOD/Bridges/Guru.cs`, `OpenPmsDentalBusiness/UI/TopazWrapper.cs`, `SignatureBox.cs`, `SignatureBoxWrapper.cs`, `OpenPmsDental/Forms/FormTPsign.cs`, the Topaz lines of `OpenPmsDental/Main Modules/ControlImages.cs`, and any other caller of `TopazWrapper` (the lead's grep); the `Interop.CDRDATALib` and `Interop.CDRDicom` references of `OpenPmsDental.csproj:106-113` and `WpfControlsOD.csproj:45-52`; the helper's Schick, Topaz and Guru modules in `OpenPmsDentalHelper32/`; tests `tests/BINBTests/TopazWrapperTests.cs`, `UnitTests/UnitTests/SignatureBoxWrapperTests.cs`, new files in `tests/Helper32Tests` | **Vendor check first** (documentation only, no download; results to O-14). Schick: `CDRDicom.Application` starts the CDR program when it is not running (`Schick.cs:50-53`), which suggests an out-of-process automation server; if it is one and its interfaces are registered for 64-bit callers, the 64-bit program calls it directly and needs no helper module. Topaz: whether Topaz ships a 64-bit `SigPlusNET.dll`. Guru: whether a 64-bit `MedVisorInterface.dll` exists. Until O-14 is answered, each gets its helper module. **Schick:** the helper module runs `ShowExam` (load the patient, or create the exam) with today's arguments. **Guru:** the helper calls `MVStart` and `MVSendPatient` with the structure that `ToCPlusPlusStructure` builds today (`Guru.cs:15-25, 49`), now built in the helper. **Topaz:** `SigPlusNET` is a WinForms control that lives inside the program's windows today (`SignatureBoxWrapper`, `FormTPsign`, `ControlImages`); in the helper it lives in the helper's own small signing window, which the program shows over its own window. The helper makes the same calls on the control as `TopazWrapper` does today (key string, encryption mode, signature string), so stored signatures and their checks do not change, and it returns the signature data and a picture of it; showing a stored Topaz signature asks the helper for that picture. The signing window looks different (release notes); the signature data does not change. `TopazWrapperTests` stops compiling a fake control with CodeDom, which throws on .NET (open point O-10): a test project builds the fake `SigPlusNET.dll`. **Other 32-bit vendor code that the program could load in process**, found by the review of S3a-12 and listed as checked: `vqdde32.dll` (VisiQuick) is declared only in `OpenPmsDental/Misc/VisiQuick.cs`, which is commented out in full and has no program link, so nothing loads it; `eidlib.dll` (the Belgian eID card, `OpenPmsDental/SmartCards/Belgium/BelgianIdentityCard.cs:62-74`) is compiled but has no caller (only a commented-out `using` in `FormOpenDental.cs:49`). Neither needs a helper. COM servers that run in their own process (Word through `Word.Application`, CADI through `Mediadent.Application`, `CADI.cs:31,58`) work across bitness and need no helper, if the vendor registered them for 64-bit callers (launch check where available). After this package no project but the helper references the Schick interop DLLs or loads `SigPlusNET.dll` or `MedVisorInterface.dll` (unless O-14 chose a 64-bit way). | `tests/Helper32Tests`: each module against a fake of its vendor interface (the Schick calls in order, the Guru structure byte for byte against today's, the Topaz key, encryption and signature calls and the returned picture); TopazWrapperTests without CodeDom; SignatureBoxWrapperTests (UnitTests); the S3a-12 source scan extended to the vendor references; BINATests (`BridgeNotAvailableTests`), BIMGTests, BTPTests; launch checks where a test PC has the vendor software (section 6) |
| **S3a-15 The tooth chart's DirectX and OpenGL engines on x64** (after S3a-12; small) | a new test file in `tests/BINATests` (next to `ToothChartFallbackTests.cs`) and the x64 test build of BINATests (the lead's lane switch from S3a-12); `SparksToothChart/ToothChartWrapper.cs` and `CodeBase/Controls/OpenGLWinFormsControl.cs` only if the x64 run finds a fault | **Decision, from the repository: the DirectX and OpenGL engines stay on x64, with today's fallback as the safety net; the chart is not moved to 2D on purpose.** Evidence (2026-11-08): `SharpDX.dll`, `SharpDX.Direct3D9.dll`, `SharpDX.Mathematics.dll`, `Tao.OpenGl.dll` and `Tao.Platform.Windows.dll` are IL-only with no 32-bit-required flag in their CLI headers, so they load in a 64-bit process; the chart passes window and device handles as `IntPtr` (`ToothChartDirectX.cs:1479,1506`, `OpenGLWinFormsControl.cs:72,150`); the Windows DLLs they call (`d3d9.dll`, `opengl32.dll`, `glu32.dll`, `gdi32.dll`) exist in both bitnesses; `d3dx9_43.dll` comes from Microsoft's DirectX End-User Runtime and must be there in the process's own bitness, which `ToothChartWrapper.IsDirectXRuntimeAvailable()` already checks by loading it (`ToothChartWrapper.cs:406-425`); and a mode that cannot start already falls back, DirectX to OpenGL to Simple 2D, without an error dialog and logged once (`StartFirstWorkingMode`, `ToothChartWrapper.cs:432-457`). So no fallback code is added. **Tests:** a static test that the five DLLs stay IL-only and not 32-bit-required; in the x64 test build, an STA test that starts each mode on a hidden form through `StartFirstWorkingMode` and records which mode came up (a mode the build PC cannot start is reported, not failed, so the fallback runs too). The real check is the 3b launch check on an x64 workstation (section 6). Until the WPF chart (`docs/roadmap.md`) replaces both engines, a PC where one fails uses the fallback, and the release notes say so. | ToothChartFallbackTests and the new tests in both bitnesses; BINATests |
| **S3a-16 `Direct2dWrapper` x64 build** (wave 3; small; the lead owns the sln and lane parts) | `Direct2dWrapper/Direct2dWrapper.vcxproj` (the x64 post-build steps, 134 and 180), `OpenDental.sln` (the project's mappings, 607-614; lead), the build lane script (lead), a new ReleaseFlowTests file | The project already has x64 Debug and Release configurations (`Direct2dWrapper.vcxproj:8-18`) with their own output folder `bin\x64\$(Configuration)\` (85, 96). But their post-build `xcopy` copies the x64 DLL into the x86 program's folder (`..\OpenPmsDental\bin\Debug\` and `bin\Release\`, 134, 180), and the sln maps `Any CPU` to x64 (`OpenDental.sln:607-608, 611-612`), so a solution build for Any CPU puts a 64-bit DLL beside the 32-bit program. **In 3a:** `Any CPU` maps to Win32, as `x86` does; the x64 configurations copy nothing (the DLL stays in `Direct2dWrapper\bin\x64\$(Configuration)\`); the lane builds the x64 Debug and Release configurations of the project at every gate; the x64 build treats the compiler's pointer-truncation warnings (C4311, C4312) as errors. The MSVC v143 component `Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64` includes the x64 compiler; the first step checks that it builds. **In 3b (S3b-4):** the x64 configurations copy into the program's output folder, and the Win32 configurations go (no build uses them after the switch). | the new test: the PE machine of `Direct2dWrapper\bin\x64\Release\Direct2dWrapper.dll` is 0x8664 and that of the program folder's copy 0x14C; the export table of the x64 DLL equals that of the Win32 DLL and holds every entry point that `Direct2d.cs` imports; NoHqTests |

**Order.** Wave 1 packages in parallel (at most three writers at once; the lead computes file intersections first). Wave 2: M1, then M2
and M3 in parallel. Wave 3 after wave 1, in parallel with wave 2, with at most three writers at once across both waves: S3a-12, S3a-13
and S3a-16 first; S3a-15 after S3a-12 (it uses the lane's x64 test build); S3a-14 after S3a-13 (the helper), and its `ControlImages.cs`
lines after S3a-M2. Then the gate. Then the bridge release.

**Known overlaps, resolved.** `PrefL.cs` belongs to S3a-10 (it carries S3a-2's elevation fix; S3a-7 needs no edit there).
`FormClaimAttachment.cs` and `FormImageSelectClaimAttach.cs` belong to S3a-2, including the `OutOfMemoryException` fixes listed in S3a-9's
work. `tests/ReleaseFlowTests`: `ProgramNamesTests.cs` to S3a-7 (with S3a-8's line 191), `ReleaseManifestTests.cs` to S3a-8,
`InstallerTests.cs` to S3a-11, the rest to S3a-10 (its new tests go in new files). NoHqTests: each package adds its own new file.
S3a-2's source-scan test merges last in wave 1, after S3a-10. The menu files that S3a-2 edits for shell starts
(`ControlImages.cs`, `PopupHelper.cs`, `PopupHelper2.cs`, `FormClaimEdit.cs`, `FormSheetFillEdit.cs`, `FormRpTreatmentFinder.cs`) pass to
the wave 2 packages after wave 1 merges. Wave 3: `FormClaimAttachment.cs`, `FormClaimAttachSnipDXC.cs`, `FormImageSelectClaimAttach.cs`
and `FrmPatientSelect.xaml.cs` pass from S3a-2 to S3a-12 after wave 1 merges; a file that a wave 2 package also edits (for example
`FormODBase.cs` or `FormPopupFade.cs`, if M3 converts their menus) waits for that package's merge. `OpenPmsDental.csproj` and
`WpfControlsOD.csproj`: S3a-13 removes the QBFC references, then S3a-14 the Schick ones. `tools/Build-Release.ps1` belongs to S3a-13 in
3a (the helper's files). `ControlImages.cs` passes from S3a-M2 to S3a-14 (its Topaz lines only). In `tests/ReleaseFlowTests`, S3a-13 and
S3a-16 add new files only. The lane's x64 test build and `OpenDental.sln` are the lead's.

**Full-test gate.** Section 3, plus: the golden outputs and known answers recorded and committed; the source-scan tests pass; the
launch checks for 3a (section 6) done with screenshots, including a self-update from a standard user; the x64 test runs (S3a-12, S3a-15)
and the x64 build of `Direct2dWrapper` (S3a-16) pass; the helper's tests pass, and its links are launch-checked where a test PC has the
vendor software (S3a-13, S3a-14); the elevated installer test; then the bridge release is built, installed on a test PC over the
previous release by setup and by self-update, and published to the office.

**Tooling changes.** `Test-Installer.ps1` runs elevated (S3a-11); the lead adjusts the release workflow script in the local tools folder
that runs it. The new manifest key (S3a-10) is defined here and written by 3b's release. Lane and runners (lead, wave 3): an x64 test
build of chosen test projects into `bin\x64\$(Configuration)\` and their run with `/Platform:x64` (S3a-12, S3a-15); the x64 build of
`Direct2dWrapper` at every gate (S3a-16). Build-Release: the helper's files (S3a-13).

**Downloads.** None (Mono.Cecil is already in use; the source-scan tests replace an analyzer package). The vendor checks of S3a-13 and
S3a-14 read vendor documentation only; no vendor file is downloaded.

**Risks and mitigations.**
- Menus look different (native Win32 menus to the ToolStrip renderer: fonts, heights, check marks, `&` accelerators): screenshot pass on
  net48 (section 6); this is the point to catch it, not in 3b.
- Cancel in progress windows now stops at the next check instead of immediately: review each long action reachable from a ProgressWin
  for a check point; the cancel test proves no database write after "cancelled".
- Hand-written SOAP envelopes could differ from the generated proxies: envelope-equivalence tests run against the old proxies before they
  are deleted, plus one call against the DentalXChange test endpoint (prelive) if credentials allow. ITS has no test account in the
  office, so it is proven by envelope equivalence and the fake endpoint only; the release notes say so.
- Explicit `UseShellExecute=true` changes nothing on net48 (it is the default there); the elevation launch check proves the copier still
  asks for consent and finishes.
- Too much product change in one release: the bridge release ships 3a on the known runtime, so the office meets these changes one at a
  time, before the runtime changes.
- Pointer-size faults that show only in a 64-bit process: the x64 test runs on net48 (S3a-12, S3a-15) catch layout and signature errors
  before the switch; the TWAIN path is proven on x64 against the fake DSM here, and against the real 64-bit TWAINDSM and the clinic's
  sensor before the office release (3b entry conditions).
- The helper adds a process boundary to QuickBooks Desktop, Schick, Topaz and Guru: the protocol tests cover a crash, a timeout and a
  missing helper; the bridge release already runs the helper on the known runtime; no test PC has the vendor software today (O-14).

**Size.** 19 packages (11 + 3 menu packages + 5 for 64-bit); extra large; about 30-47 dev-days, of which the menus are about 6-10 and the
64-bit preparation about 9-15.

---

### Stage 3b - Switch to net10.0-windows x64

**Goal.** One integration branch moves every project to `net10.0-windows` (x64, self-contained, decision 20; straight to 64-bit, with no
32-bit .NET 10 build, decision 19), replaces the Middle Tier with an ASP.NET Core host, updates release and setup for the .NET 10 file
layout, the runtime that ships inside the program (published as its own part of a release) and the 64-bit install folder, and merges to
`main` once. Only the 32-bit helper program stays x86, self-contained in its own sub folder (S3a-13, O-15).

**Entry conditions.** 3a merged; the bridge release installed on every workstation and on the update server. The lane and the test runners
have been on Build Tools 2026 since S1-0 (decision 1), and the x64 .NET 10 runtimes (10.0.14 on 2026-11-08, kept current by Visual Studio
updates; the x86 ones are installed too) are on the build PC; the test runs use them, while the shipped exes carry their own
(decision 20). Download batch D approved and in place, with the runtime packs. Batch C is not needed: the office runs no Middle Tier
server (open point O-7, answered 2026-11-08), and the Middle Tier server is self-contained too, so the gate's Middle Tier check on the
build PC needs no runtime install. Branch `net10/switch` cut from `main`.

**64-bit entry conditions (decision 19).** The first two hold before the branch's work starts, and the third is asked when it starts;
the 3b gate and the merge also wait for the last two.
- The wave 3 packages of 3a (S3a-12 to S3a-16) merged, with their x64 test runs green.
- Open point O-15 (the helper's runtime after the switch) answered (2026-11-08: self-contained, decision 20); O-14 answered for each
  vendor where S3a-13 and S3a-14 found a choice.
- The owner's approval of the 64-bit TWAINDSM download (O-16), asked when 3b starts; it is not covered by the batch approval. This
  program's setup ships the 64-bit `TWAINDSM.dll` (S3b-7): TWAIN DSM 2.5.1 from the TWAIN Working Group's GitHub release (both DLLs
  signed by the TWAIN Working Group).
- State on 2026-11-08: the TWAIN driver project's two 64-bit data sources (version 1.2) are built and pass that project's fake-mode
  suite in both bitnesses; they are not yet packaged, and not yet tested through the 64-bit TWAINDSM. They install to the standard
  folder `C:\Windows\twain_64\<name>\`, beside the unchanged 32-bit sources; the 32-bit package must be installed too, because the
  64-bit source reads the `twain_32` INI file and starts the 32-bit helper.
- **Before the office release:** the 64-bit data sources are packaged and tested through the 64-bit TWAINDSM on the clinic's real
  sensor, from the x64 program. Whether two programs may use the sensor at once is still an open owner decision in the driver project.
- **Before the office release:** each workstation's TWAIN devices are listed, with a 64-bit data source for each, or that workstation
  stays on the bridge release for now (section 10 step 3).

**Packages (all on `net10/switch`).**

| Package | Owns | Work | Targeted tests |
|---|---|---|---|
| **S3b-0 Spike** (first; throwaway branch, not merged) | nothing on the integration branch | Prove the unknowns on the real toolchain: (1) The lane's MSBuild 18 with the SDK band pinned in S1-0 builds `net10.0-windows` without NETSDK1233 and without a `Microsoft.Net.Sdk.Compilers.Toolset` download. (2) The win-x64 apphost (decision 19): both SDK bands have `Microsoft.NETCore.App.Host.win-x64` 10.0.14 in their `packs` folder (the machine-wide 10.0.403 and the per-user 10.0.114, checked 2026-11-08); the win-x86 apphost of the self-contained helper (O-15) is in the machine-wide band's packs only (the per-user band then takes it from batch D). Confirm that the build uses the local pack and that the locked restore succeeds without an apphost package (ResolveAppHosts restores the pack as a package only when it is missing, https://source.dot.net/Microsoft.NET.Build.Tasks/ResolveAppHosts.cs.html). (3) The 10.0 WindowsDesktop reference pack still has System.Resources.Extensions, System.CodeDom, System.DirectoryServices, WindowsFormsIntegration. (4) A minimal `Microsoft.NET.Sdk.Web` x64 project referencing the business library, self-contained for win-x64 (decision 20), gets a `runtimeconfig.json` that includes both `Microsoft.AspNetCore.App` and `Microsoft.WindowsDesktop.App`, carries the three runtimes in its folder and starts as a Windows service. (5) The WebView2 1.0.4314.35 package layout for `net10.0-windows`. (6) DentalXChange test endpoint responses parse under `HttpClient` without `useUnsafeHeaderParsing`. (7) With `RuntimeIdentifier=win-x64` and `SelfContained=true` (decision 20), a locked restore takes the runtime packs from the offline folder (batch D) and asks for nothing else; the Windows assets of System.Management, System.Speech and System.DirectoryServices.AccountManagement/Protocols (`runtimes/win/lib/net10.0` in the packages) land flat in `bin\Release`, not as a PlatformNotSupported stub; the runtime files land flat beside the exes, without the runtime packs' language folders (`SatelliteResourceLanguages=en`); and the program, ServiceManager and the DbSettings tool, built into one folder, share one runtime: every `.runtimeconfig.json` names the same included frameworks and version, and the started program's `coreclr.dll` loads from its own folder, not from `%ProgramFiles%\dotnet`. A test project that references the self-contained program stays framework-dependent and builds (the SDK skips the self-contained match check, NETSDK1151, for VSTest test projects). (8) The `PrefName`, `ClinicPref` and `Adjustment` round trips of MiddleTierMockTests pass on net10.0. (9) The x64 test host: vstest 18.11 with `/Platform:x64` runs a `net10.0-windows` x64 MSTest 4 assembly on the machine-wide x64 runtime (`Program Files\dotnet`), and the x64 apphost's PE machine is 0x8664 (the large-address-aware question of section 9.2 does not arise for x64). (10) `Microsoft.CodeAnalysis.VisualBasic` compiles and runs a VB `<Code>` block in an x64 `net10.0-windows` process, and `Microsoft.XmlSerializer.Generator` builds an `XmlSerializers.dll` in the lane's Release build. (11) After the owner approves its download (O-16): the TWAIN code of S3a-12 in an x64 `net10.0-windows` process loads the 64-bit TWAINDSM 2.5.1 from the system folder and opens the TWAIN driver project's 64-bit data sources in their fake mode. (12) Which registry view the x64 Visual C++ redistributable writes its `VC\Runtimes\x64` key to, for the setup's check (S3b-7); and that the x86 helper program (S3a-13), self-contained for win-x86 in its sub folder `Helper32\` (O-15), starts from the x64 program and answers over its pipe. Each vendor piece the helper loads is proven to load in that .NET 10 x86 process: the QBFC and Schick CDR COM objects, Topaz `SigPlusNET.dll` (a .NET Framework WinForms control, so in compatibility mode) and Guru's native `MedVisorInterface.dll`. The build PC has none of the vendor software, so the spike proves what it can there (the interop types, the P/Invoke declarations, the helper modules against their fakes) and the rest on a test PC with the vendor software where one exists (O-14); a piece that does not load, or cannot be proven, is reported to the owner, with no fallback to .NET Framework. (13) The sizes of the runtime part (S3b-7): the x64 runtime files and the helper's x86 ones, on disk and zipped, for the office estimate of section 10. | spike notes only |
| **S3b-1 Toolchain and shared settings** | `global.json` (stays on the band S1-0 chose; bump per section 2.4 if one is due), `Directory.Build.props`, `Directory.Build.targets` (remove `ODDropValueTupleRedirect` and the test `AppConfig` default), `tests/Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, the build lane script, both test runner copies; the x64 solution platforms of `OpenDental.sln` and the slnf (lead) | `TargetFramework` `net10.0-windows` for SDK projects; `PlatformTarget x64` (decision 19; only the 32-bit helper keeps x86, O-15); for exes and test projects `RuntimeIdentifier=win-x64`. Every exe, shipped or not, is self-contained (`SelfContained=true`, decision 20): one setting for all of them, because an exe that is not self-contained cannot reference one that is (NETSDK1151; CentralManager and DocumentationBuilder reference the program). The test projects stay framework-dependent (`SelfContained=false`) and run on the build PC's machine-wide x64 runtime; the SDK skips that check for VSTest test projects (`Microsoft.NET.Sdk.targets:1313-1322` in SDK 10.0.403). `SatelliteResourceLanguages=en` (section 2.3) also keeps the 13 language folders of the WindowsDesktop runtime pack out of the output (the SDK's `ResolveRuntimePackAssets` filters by it, `Microsoft.NET.Sdk.FrameworkReferenceResolution.targets:544-552`), so the output stays flat. No trimming: it is not supported for WinForms and WPF. Without a RuntimeIdentifier, the Windows implementations of packages such as System.Management land under `bin\Release\runtimes\win\lib\net10.0\` with a PlatformNotSupported stub at the top (https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks#architecture-specific-folders, https://github.com/dotnet/runtime/blob/main/src/libraries/System.Management/src/System.Management.csproj); `Build-Release.ps1:717-722` refuses sub folders, ForkReleases publishes top-level files only and UpdateFileCopier copies non-recursively, so WMI, domain login and voice perio would throw after a self-update. With the RID the SDK copies them flat; `AppendRuntimeIdentifierToOutputPath=false` keeps the folder flat. Remove `Prefer32Bit` and `AutoGenerateBindingRedirects` (no meaning on .NET); pin `LangVersion` to today's effective values (7.3/8.0, never `latest`), raised later on purpose (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version); `WarningsAsErrors` WFDEV006; WFO1000 as a warning for the switch (https://learn.microsoft.com/en-us/dotnet/desktop/winforms/compiler-messages/wfo1000); `RuntimeHostConfigurationOption System.Globalization.UseNls=true` for the exes and tests (decision 4, https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu). Lane: already on the Build Tools 2026 MSBuild and vstest since S1-0; it builds with `/p:Platform=x64`, and the test runners pass `/Platform:x64` instead of the `/Platform:x86` of S4-2; the x64 test host finds the machine-wide x64 runtime under `Program Files\dotnet` (the build PC has the x64 8.0, 9.0 and 10.0 runtimes there; `DOTNET_ROOT_X64` only for a per-user install, https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables). The sln and slnf get x64 solution platforms (lead), and the x86 and Any CPU ones go once nothing builds them. `Directory.Packages.props` turns the source generators of System.Text.Json and Microsoft.Extensions.Logging.Abstractions off (S2-1, the lead's decision (a) of 2026-11-09, for CS0757 in PdfSharp-gdi and MigraDoc.DocumentObjectModel): decide here whether `DisableSystemTextJsonSourceGenerator` goes once the .NET 10 build uses the framework's own System.Text.Json generator. | the layer builds below |
| **S3b-2 Layer A: base libraries** | `CodeBase`, `ODCrypt`, `DataConnectionBase`, `OpenPmsDentalStorage`, `PasswordVaultWrapper`, `RdlEngine` (with `Definition/Code.cs`), `RdlViewer`, `RdlDesign` (it references only RdlEngine and RdlViewer; not shipped), a new ReportTests test file, the 7 wrappers (+ `SourceBuild.props`, `PDFsharp.props`: delete the hand-written symbol lists, let the SDK define upstream's net10 symbols; WeCantSpell with upstream's net10.0 constants), `UpdateFileCopier`, `OpenPmsDentalDbSettings`, `ImagingDeviceManager`, `ODR`, tools | Retarget; plain `net10.0-windows` for non-UI libraries, `UseWindowsForms`/`UseWPF` per the project survey; PackageReferences: `System.Management`, `System.ServiceProcess.ServiceController`, `System.Security.Cryptography.ProtectedData` (Storage has no WinForms); the 10.0 `Microsoft.Extensions.*.Abstractions` come from stage 2; remove polyfill references (pruned on net10, https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#prunepackagereference) and `Sftp.TypeAsyncInterfacesDependency`; `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` first in `ODInitialize.Initialize()` (https://learn.microsoft.com/en-us/dotnet/api/system.text.codepagesencodingprovider); the secured-`FileStream` constructor in DbSettings to `FileSystemAclExtensions.Create`; remove `ServicePointManager.SecurityProtocol` (`ODInitialize.cs:27`); `DtoProcessor`'s `FixPackageAssembly` goes in S3b-6. `ImagingDeviceManager`: TWAIN runs in the x64 process through the 64-bit TWAINDSM only (the code of S3a-12; S3b-7 installs the DSM). **RDL `<Code>` blocks keep working (decision 14).** `RdlEngine/Definition/Code.cs:59-129` compiles a report's `<Code>` block with `VBCodeProvider.CompileAssemblyFromSource`, which throws on .NET. The compile moves to Roslyn: `Microsoft.CodeAnalysis.VisualBasic` (batch D) compiles the same generated source against the runtime's reference assemblies, and the engine loads the emitted assembly; a compile error keeps today's message, with the line relative to the `<Code>` element. RdlDesign's syntax check (`CodeCtl.cs:232-273`, the same `VBCodeProvider` call) uses the same compile. The new ReportTests test compiles and runs a report's `<Code>` function: the report output equals the S3a-4 record, and a `<Code>` block with an error logs that error. RdlDesign's SQL Server, ODBC and OLE DB code (`DesignerUtility.cs`, `DialogDatabase.cs`, `DialogDataSourceRef.cs`, `DialogDataSources.cs`) moves with the port of the RDL providers (open point O-10). | builds of layer A; CryptoTests, DbConnectionSecurityTests, PdfLibraryTests, LibSpellTests once layer tests retarget (S3b-5 runs them) |
| **S3b-3 Layer B: business library and services** (after S3b-2) | `OpenPmsDentalBusiness`, `OpenPmsDentalGraph`, `SparksToothChart` (SharpDX/Tao stay file references and run on x64, S3a-15; drop `Microsoft.VisualC`), `OpenPmsDentalHL7`, `ServiceManager` | Retarget; packages `System.DirectoryServices.AccountManagement` (with `System.DirectoryServices.Protocols` as its dependency), `System.Speech` for voice perio charting (decision 13), `WinForms.DataVisualization` 1.10.2 (MIT, same namespace) for the dashboard and graph charts (decision 10); `OpenPmsDentalBusiness.XmlSerializers.dll` built by `Microsoft.XmlSerializer.Generator` (batch D) instead of SGen, which does not run for .NET (https://learn.microsoft.com/en-us/dotnet/core/additional-tools/xml-serializer-generator); `LocalDiagnostics` reports `RuntimeInformation.FrameworkDescription`; drop the OSVersion WMI workaround. | builds of layer B |
| **S3b-4 Layer C: UI and executables** (after S3b-3) | `WpfControlsOD` (incl. `Bridges/PluginLoader.cs`), `ImagingCapture`, `OpenPmsDental` (+ `Main Modules/ProgramEntry.cs`, `app.config`), `PluginExample`, `xCrudGenerator`, `DatabaseIntegrityCheck`, `MySQLManager`, `CentralManager` and `DocumentationBuilder` (kept, not shipped; decision 17); `Direct2dWrapper/Direct2dWrapper.vcxproj` (its configurations and copy steps only; it stays on v143); `OpenPmsDentalHelper32` (self-contained for win-x86 into `Helper32\`, O-15) and `CodeBase/Utilities/Helper32Client.cs` (the helper's path only); PluginExample's sln and `OpenDental.Local.slnf` entries (lead), so the gates build it; not shipped (not in Build-Release's lists) | Retarget. In every exe's `Main`, before anything creates a window: `Application.SetDefaultFont(new Font("Microsoft Sans Serif", 8.25f))` first (72 plain forms rely on the default, which is Segoe UI 9 on .NET; the call throws `InvalidOperationException` once a window exists, so it goes before ProgramEntry's splash screen and its NoDpi handling, https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setdefaultfont), then `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)` (the app.config DPI section is .NET Framework only, https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms). In `ProgramEntry.Main` the `/ehr-render` branch of EHR-3 (`docs/specs/ehr-rebuild-plan.md`) stays the first statement, before `SetDefaultFont`, `SetHighDpiMode` and the `ODInitialize.Initialize()` call, which registers the code pages (S3b-2): it runs the render child, which creates no window, ends in `Environment.Exit` and reads only UTF-8, so it needs no code page. `SetDefaultFont` applies the Windows text scale factor, so with "Make text bigger" above 100% those 72 forms show slightly larger text than on net48 (accepted, decision 9; release notes). The code-page registration in every `Main` that does not call `ODInitialize` (HL7, ServiceManager, DbSettings, UpdateFileCopier, DatabaseIntegrityCheck, MySQLManager, xCrudGenerator, RdlDesign, CentralManager, DocumentationBuilder). Delete the ignored app.config sections: binding redirects, `startup` (its `useLegacyV2RuntimeActivationPolicy` at `app.config:112` has no .NET equivalent and nothing needs it: every managed DLL left in `Required dlls` is IL-only), DPI, `system.net`, `system.serviceModel`. `OpenPmsDental.XmlSerializers.dll` is built by `Microsoft.XmlSerializer.Generator` (batch D), as the business library's is in S3b-3: both files are kept (standing rule), and SGen does not run for .NET. UpdateFileCopier is self-contained like the other exes and uses the program folder's runtime; `CopyUpdateFileCopier` copies its files (`.exe`, `.dll`, `.runtimeconfig.json`, `.deps.json`) next to the program. It is not a single file (https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview): a self-contained single file would carry a whole runtime (about 66 MB zipped) in every release. It cannot run from the program folder, whose files it replaces, the runtime's included, so `OpenFileCopier` (S3b-7) copies it with the x64 runtime files into a new temp folder and starts it there. Run from there it would read `UpdateFileCopier.dll.config`, which `OpenFileCopier` never copies, so defaults apply: confirm that none of its `Settings.settings` values or app.config entries matters and remove them. The build stamp lands in `OpenPmsDental.dll`; WebView2 1.0.4314.35, with the x64 `WebView2Loader.dll` from the package flat in the output. `Direct2dWrapper`: its x64 configurations copy the DLL into the program's output folder, and its Win32 configurations go, since no build uses them after the switch (S3a-16 prepared both). The 32-bit helper keeps `PlatformTarget` x86 and is self-contained for win-x86 (`RuntimeIdentifier=win-x86`, `SelfContained=true`; O-15, decision 20): a copy step puts it with its own x86 runtime (NETCore and WindowsDesktop) into the sub folder `Helper32\` of the program's output, and `Helper32Client` starts it from there. PluginExample becomes an SDK-style `net10.0-windows` project that the lane builds (not shipped) (decision 16); `PluginLoader` reports a plugin built for .NET Framework (its `TargetFrameworkAttribute`, read with `System.Reflection.Metadata` without loading it) with a clear message instead of a load failure. | Core run by the workflow after S3b-5; the plugin test of section 7 (a .NET Framework plugin DLL gets the clear message and is not loaded; the rebuilt PluginExample loads) |
| **S3b-5 Tests** (after S3b-4) | `tests/Directory.Build.props`, the 24 test projects that reference the exe and the 8 that reference WpfControlsOD (`UseWindowsForms`/`UseWPF`), `UnitTests/UnitTests.csproj` (kept, S0-5; with its `UI Testing` windows), `tests/LibraryBindingTests` (retire binding-redirect and GAC tests), `tests/BindingRedirects.config` (delete), `tests/VendorDirectTests/BridgesRemoval/BridgesDllRemovedTests.cs:165-190`, `tests/LibSpellTests/SpellLibraryFilesTests.cs:85`, PdfLibraryTests/LibSpellTests upstream-TFM guards, Mono.Cecil 0.11.6, a test `[AssemblyInitialize]` that registers the code-page provider, `tests/LibraryUpdateTests/SmtpTests.cs:108-121`, `tests/LibraryUpdateTests/AddressRulesTests.cs` (the .NET 10 result), `tests/MiddleTierMockTests`, `tests/Helper32Tests` | Retarget and fix; add the behaviour tests of section 7. The pointer-size tests of S3a-12 keep their x86 and x64 columns, and their x64 runs become the normal runs; the helper tests run the x86 helper against the x64 program. | Full by the workflow (gate) |
| **S3b-6 Middle Tier** (after S3b-3; parallel with S3b-4) | `OpenPmsDentalServer/*` (replaced: new `Microsoft.NET.Sdk.Web` project, `Program.cs`, `appsettings.json`, IIS `web.config`; ASMX, old `Web.config`, old csproj deleted), `OpenPmsDentalBusiness/WebServices/OpenDentalServerHttp.cs` (new), `OpenDentalServerProxy.cs`, `OpenDentalServerReal.cs` (delete), `Web References/OpenDentalServer/*` (delete), `Remoting/RemotingClient.cs` (353-372), `Remoting/Meth.cs` (484-493), `Remoting/DtoProcessor.cs` (`FixPackageAssembly`), new `tests/MiddleTierHostTests`, `tests/LibraryBindingTests/BindingRedirectTests.cs:33`, `tests/ReleaseFlowTests/ReleaseFilesTests.cs:31`, `tools/Test-NoOldNames.ps1:127-132` | As the Middle Tier survey designed it: one POST endpoint at `{PathBase}/ServiceMain.asmx` and `{PathBase}/` (default `/OpenDentalServer`), raw UTF-8 body, synchronous `ProcessDto` on one thread with the `ServerMT` role set per request (thread-static state), 1 GiB body limit in Kestrel and IIS, no execution timeout, HTTPS only, Windows service by default (https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service), IIS in-process with a 64-bit application pool (the IIS default) as an option (https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/); the server is x64 like the program (decision 19) and self-contained for win-x64 (decision 20): it is published into its own folder on its host with the NETCore, WindowsDesktop and ASP.NET Core runtimes of the program's release, so the host installs nothing for .NET; the IIS option still needs the ASP.NET Core Module, which the .NET 10 Hosting Bundle installs without its runtimes (`OPT_NO_RUNTIME=1 OPT_NO_SHAREDFX=1`, https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/hosting-bundle); client `OpenDentalServerHttp` on a cached `HttpClient` (1 h timeout, same proxy and User-Agent, `SendAsync(...).GetAwaiter().GetResult()` because TestServer rejects synchronous `Send`); `HttpRequestError.ConnectionError` keeps the connection-lost retry (https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequesterror). No SOAP compatibility: the server has never shipped to an office and the version check forces client = server, so client and server switch together. `Adjustment.TaxTransID` now travels (harmless: both ends are the same build). The host must run a Windows Server version on Microsoft's .NET 10 OS list (decision 8). | the mock suite (MiddleTierMockTests, with the `PrefName`, `ClinicPref` and `Adjustment` round trips) and the new host round-trip tests (login good/bad, DtoGetTable/Object/Void/String with XML-special text, IsMiddleTierAvailable, version mismatch, 413, connection refused to retry, 50 MB payload, two concurrent users on two databases), which can also run against a server URL, for the 3b gate's check of the Windows service on the build PC; BSERVERTests |
| **S3b-7 Release, setup, self-update** (parallel with S3b-4 after S3b-3; the release-channel package of 3b, it owns the runtime part, decision 20) | `THIRD-PARTY-NOTICES.txt` (Roslyn; the 64-bit TWAINDSM; the .NET runtime, MIT, named by its runtime packs); `tools/Build-Release.ps1` (OwnFiles 104-131, with both XmlSerializers files; the source folder of `Direct2dWrapper.dll`, 124, now `Direct2dWrapper\bin\x64\Release`; ThirdPartyFiles, with the Roslyn DLLs; NativeFiles with PE 0x8664 for every apphost and native DLL, 194 and 437-441, and 0x14C only for the helper; a third allow list, the runtime files, read from the runtime packs' own file lists (`data/RuntimeList.xml`, the list the SDK's `ResolveRuntimePackAssets` reads) instead of typed in, with the PE checks over them (0x8664 for the x64 runtime files, 0x14C for those in `Helper32\`; an IL-only file is checked as IL-only); `Helper32\` as the one sub folder it accepts; the 64-bit `TWAINDSM.dll` as a setup file that the program folder still refuses, 250; BuildIdFiles 201-203, version check 450-457), `installer/OpenPmsDental.iss` (Framework 4.8 gate 158, 187-194, 727-738; DbSettings run 580-605, 860-871; `[InstallDelete]` 116-139; prerequisites 243-290 and 783-831; `MinVersion` 103; the 64-bit install: `[Setup]` 59-83 with `AppId` 59, `DefaultDirName={commonpf32}` 77 and `UsePreviousAppDir` 78, `[Icons]` 148-149, the Visual C++ check 159-165 and 196-201), `tools/Test-Installer.ps1`, `OpenPmsDentalBusiness/Misc/ForkRelease/ForkReleases.cs` (`ListBuildIdFiles` adds `OpenPmsDental.dll`; `ListRetiredFiles` adds the `*.exe.config` files shipped before and the pruned in-box DLLs; `ListSpecialDirs` adds `Helper32`; the runtime part) and `ReleaseManifest.cs` (the runtime part's keys of S3a-10), `OpenPmsDentalBusiness/Data Interface/DocumentMiscs.cs` (the runtime part's rows), `OpenPmsDentalBusiness/TableTypes/DocumentMisc.cs` and `docs/port-notes/FORK-ENUMS.md` (two fork-only `DocumentMiscType` values), `UpdateFileCopier/FormMain.cs` (`Helper32` as a special folder; the runtime check before the manifest is written), `OpenPmsDental/Logic/PrefL.cs` (OpenFileCopier copies the copier with the x64 runtime files into a new temp folder; the runtime part on pull; runtime check before pull), `tests/ReleaseFlowTests/*`, README, `docs/release-process.md`, `docs/roadmap.md` (the .NET Framework 4.8 to .NET 10 section: record the switch as done) | The release ships `X.exe` (apphost) + `X.dll` + `X.runtimeconfig.json` + `X.deps.json` for OpenPmsDental, ServiceManager, OpenPmsDentalDbSettings and UpdateFileCopier (and the HL7 service when it ships), all self-contained on the one x64 runtime beside them (decision 20); the helper with its x86 runtime in `Helper32\`; both XmlSerializers files, now from the generator (S3b-3, S3b-4); and the Roslyn compiler DLLs that RdlEngine loads (`Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.VisualBasic.dll`, MIT; decision 14), named in the notices. The manifest carries the runtime key defined in S3a-10 (for example `Runtime=Microsoft.NETCore.App 10.0.14 win-x64`) and names the runtime part it needs, and `PrefL` refuses a release for another architecture than its own. **Runtime (decisions 6, 19 and 20):** the setup installs no .NET runtime. It installs the program's own runtime files with the program (the x64 runtime beside the exes, the helper's x86 runtime in `Helper32\`), also in a silent install, so the .NET check, the download, its signature check and its install step go. `/PREREQS=` (`ask`, `download`, `skip`) now covers only the Visual C++ runtime, which stays: `Direct2dWrapper.dll` imports `VCRUNTIME140.dll`, while the .NET runtime needs no redistributable (the WindowsDesktop runtime carries its own `vcruntime140_cor3.dll`, checked on 10.0.14). The DbSettings tool, self-contained, runs from the program folder as soon as the files are in place, with `skip` too. `Test-Installer.ps1` starts the installed DbSettings tool, reads its loaded modules (`coreclr.dll` must come from the install folder, not from `%ProgramFiles%\dotnet`; this holds on the build PC, which has machine-wide runtimes, and on a clean VM without .NET the tool simply starts) and closes it, and compares the installed files with Build-Release's staged list (the program part, the runtime part, `Helper32\`). **The runtime part of a release (decision 20).** A self-contained release carries the x64 runtime (`Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`, about 150 MB on disk and 66 MB zipped for 10.0.14) and the helper's x86 runtime (about 100 MB and 40 MB zipped with WinForms only, about 145 MB and 61 MB if it also takes WPF; section 9.1). Published with every release, that would add about 145-170 MB of base64 rows at every publish and make every workstation pull it each time. So the release channel stores a release in two parts. **The program part** is every other file, published as today (`ForkReleaseManifest` and `ForkReleaseSegment` rows, `DocumentMiscs.ReplaceForkRelease`). **The runtime part** is the runtime files of both architectures, keyed by the runtime version and the SHA-256 over their names, sizes and SHA-256 values; it has two new fork-only `DocumentMiscType` values (102 manifest, 103 segment, recorded in `docs/port-notes/FORK-ENUMS.md`; the same table and column, so no schema change) and its own `DocumentMiscs.ReplaceForkRuntime`, which deletes the old rows and writes the segments, then its manifest last, as `ReplaceForkRelease` does. The program part's manifest names the runtime key it needs. `ForkReleases.Publish` writes the runtime part only when the install folder's runtime key differs from the published one, then the program part, its manifest last. On a pull, a workstation compares the published runtime key with the one in its own folder's manifest: the same key downloads the program part only; a new key downloads both, and both are checked in full (product, archive SHA-256, every file's size and SHA-256, the runtime key over the runtime files) before anything is copied. UpdateFileCopier copies both parts in one run. `OpenFileCopier` starts it from a new temp folder with one x64 runtime: the new release's copier with the new runtime part when the key changes, otherwise with the program folder's runtime files, which are then the same version. A workstation never mixes runtime versions: the copier refuses a download whose runtime files do not all belong to its one key, and after copying it checks every runtime file in the program folder against the runtime part before it writes the release manifest; if one does not match, it writes no manifest and says to run the setup, which carries the full runtime. Before it copies, it stops every process that runs from the program folder (the program, ServiceManager, the DbSettings tool, the helper, the HL7 service), because they all hold the shared runtime files. **Release-flow check:** Build-Release refuses to stage when the staged `.deps.json` files name more than one runtime version, when the helper's runtime is another version, or when that version is older than the newest 10.0.x `Microsoft.NETCore.App.Runtime.win-x64` in the offline folder (the monthly runtime release, section 2.4). The setup always carries the full runtime: the first install and each runtime change bring the whole runtime to a workstation. **Windows versions (decision 8):** Windows 11 is the baseline. Below it (Windows build lower than 22000) the setup warns and lets the user go on. The warning says that .NET 10 supports Windows 10 only in some editions, with the link https://learn.microsoft.com/en-us/dotnet/core/install/windows. `MinVersion` (10.0.18362, `OpenPmsDental.iss:103`) stays the hard floor. This setup does not install a Middle Tier server, which is self-contained too (S3b-6, section 10 step 4). **64-bit install (decision 19):** `ArchitecturesAllowed=x64compatible` and `ArchitecturesInstallIn64BitMode=x64compatible` (https://jrsoftware.org/ishelp/index.php?topic=64bitinstallation); `DefaultDirName={commonpf64}\Open PMS - Dental` (today `{commonpf32}`); the Visual C++ check moves to the x64 redistributable (`vc_redist.x64.exe`, its `VC\Runtimes\x64` key read in the view S3b-0 (12) found; today the x86 file and key in the 32-bit view, `OpenPmsDental.iss:159-165, 196-201`), with the same signature check; the 64-bit TWAINDSM 2.5.1 (O-16) goes into the 64-bit system folder (`{sys}` in 64-bit mode) when it is missing or older, as a shared file the uninstall leaves in place, never into the program folder (the program would load one there first); Build-Release checks its TWAIN Working Group signature and its SHA-256 against the approved download. **The move from the 32-bit install** (section 10 step 3): in 64-bit mode the setup looks for a previous install only in the 64-bit view, so `UsePreviousAppDir` cannot find the bridge release's install. The setup finds it by its uninstall entry in the 32-bit view (same `AppId`), copies `FreeDentalConfig.xml` and the flag files `NoDpi.txt` and `NoCustomBorders.txt` (read from the program folder, `ProgramEntry.cs:73,77`) into the new folder, then runs the old uninstaller silently: it removes the old program files, the Start menu and desktop shortcuts (`{autoprograms}`, `{autodesktop}`) and the old uninstall entry, and it may also delete `FreeDentalConfig.xml`, which that setup installed without `uninsneveruninstall`, hence the copy first. The setup then deletes what is left of the program's own files in the old folder (the files self-updates added, by the release's file list) and leaves the folder only if other files remain, logged and named on the last page. A Windows service registered from the old folder (the HL7 service) is reported, to be registered again from ServiceManager. `Test-Installer.ps1` reads the uninstall entry in the 64-bit view (today `WOW6432Node`, 43) and gains a scenario that installs over a 32-bit bridge install. | ReleaseFlowTests (the apphost resource test of S3a-10 now on apphosts; the layout test of section 7; the PE machine checks, the runtime part included; the setup's 64-bit and move tests of section 7; the runtime part tests of section 7: a release with an unchanged runtime publishes no runtime rows and its pull downloads no runtime segment, a release with a new runtime replaces the runtime part and its pull downloads both parts, a workstation never ends with runtime files of two versions, the release-flow check refuses an older or mixed runtime); `Build-Release.ps1 -ListFiles/-CheckFolder`; the elevated `Test-Installer.ps1` (the program's runtime loads from the install folder; the installed file list); manual install on a clean VM without any .NET, and an upgrade over the bridge release that moves the install from `Program Files (x86)` to `Program Files` |

**Order on the branch.** S3b-0 (spike) and S3b-1 first; then S3b-2, S3b-3 strictly in sequence (each layer must be net10 before the
layer above can build); then S3b-4 with S3b-6 and S3b-7 in parallel (disjoint files: the Middle Tier package owns the Remoting and
WebServices files, the release package owns ForkReleases/DocumentMiscs/PrefL/UpdateFileCopier's `FormMain.cs`/setup/Build-Release;
`OpenPmsDentalBusiness.csproj` belongs to S3b-3 and needs no edit afterwards because the SDK globs files); then S3b-5. Each commit on
the branch builds every project already retargeted (single-project lane builds); the full slnf builds again from S3b-5 on. `main` keeps
receiving fixes; the lead merges `main` into the branch after each of them.

**Full-test gate.** Section 3 on the branch, plus: zero WFDEV006; golden outputs and known answers equal to the net48 records (any
difference is explained and accepted by the owner or fixed); the behaviour tests of section 7 pass; the database TLS test (CAPI and CNG,
non-loopback; environment per O-8) passes; the screenshot pass of section 6 is complete with before/after pairs; the self-update between
two .NET 10 builds works from a standard user; the release is built, installed on a clean VM without any .NET and over the bridge
release; the new Middle Tier is checked for real on the build PC: started as a Windows service on a loopback address with a test
certificate, with the host round-trip tests (S3b-6) and a client session run against it. The office runs no Middle Tier server
(B-SERVER 5 in `docs/owner-questions.md`, open point O-7), so there is no office hosting check. The 64-bit checks (decision 19): PE
machine 0x8664 for every staged exe and native DLL, the x64 runtime part's included, and 0x14C for the helper's folder; TWAIN capture
from the x64 program with the clinic's real sensor through the 64-bit TWAINDSM and the driver project's 64-bit data sources (entry
condition); each helper link from the x64 program where a test PC has the vendor software; the setup's move from a 32-bit bridge
install on a test PC (section 10 step 3). Then the branch merges into `main` in one merge commit.

**Tooling at the switch (S3b-1/S3b-7).** The lane and the test runners have used the Build Tools 2026 MSBuild and vstest since S1-0;
S3b-1 keeps the same SDK pin and moves the builds and the test runs to x64 (`/p:Platform=x64`, `/Platform:x64`, the machine-wide x64
runtime). Test runner: same flat paths, adapters unchanged. Build-Release and setup: S3b-7. `Direct2dWrapper.vcxproj` keeps v143 and
builds x64 only (S3b-4). MSBuild.exe stays the engine (the vcxproj needs the C++
targets, https://learn.microsoft.com/en-us/visualstudio/msbuild/errors/msb4278); `dotnet build` becomes possible for everything else.

**Downloads.** Batch D (NuGet), section 5, with the runtime packs the self-contained exes need (decision 20). Batch C (non-NuGet) is not
needed: the Middle Tier server is self-contained, and only its IIS option, for a future office server, needs the Hosting Bundle (O-7).
The 64-bit TWAINDSM 2.5.1 is not a plan batch: its download is asked when 3b starts (O-16).

**Risks and mitigations.**
- Toolchain support: Build Tools 2026 target net10.0 officially
  (https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)
  and have built `main` since S1-0; the spike proves the net10.0 build, the apphost pack and the restore.
- Compatibility-mode binaries (SharpDX, Tao) might fail at run time: the tooth chart in DirectX, OpenGL and 2D modes on x64; a mode
  that cannot start falls back, down to 2D (S3a-15).
- TWAIN on x64: the TWAIN code was made pointer-size-safe and tested in both bitnesses in 3a (S3a-12); the spike opens the 64-bit
  TWAINDSM with the driver project's sources in fake mode; the office release waits for the test on the clinic's real sensor.
- A 32-bit vendor component that no helper covers: the review of S3a-12 lists every native and COM component the program loads; the
  source scan keeps the vendor references in the helper.
- Mixed workstations during the switch: the bridge release refuses the x64 release and says to run the new setup; no schema change, so
  both builds work on the same database (section 10).
- Silent runtime differences: NLS mode (decision 4), golden outputs, section 7 tests, screenshot pass.
- RID-specific assets landing in sub folders: RID set for exes and tests; Build-Release refuses folders; the layout test calls WMI from
  the staged folder.
- The runtime ships inside the program (decision 20), so Microsoft Update no longer patches it: the monthly runtime release (section
  2.4) and the release-flow check that refuses an older runtime (S3b-7).
- Larger releases: the runtime part is published and pulled only when the runtime changes (S3b-7); the first install and each runtime
  change carry the full runtime (section 10).
- A vendor piece of the self-contained helper that does not load on .NET 10: the spike proves each where it can (S3b-0 (12)); one that
  does not load is reported to the owner, with no fallback to .NET Framework (O-15).
- The branch drifts from `main`: keep 3b short; merge `main` into it after every fix; no feature work on `main` during 3b except fixes.
- The Middle Tier's thread-static per-request state: synchronous handler, role set per request, concurrent-user test.
- Long-lived branch merges and the sln: the merge driver; the lead checks changed Project lines.

**Size.** 8 packages; extra large; about 19-31 dev-days (Middle Tier 5-6.5 of them; the 64-bit setup and the move from the 32-bit
install about 2-3; the runtime part of the release channel and the self-contained layout, decision 20, about 2-3).

---

### Stage 5 - x64: folded into 3a and 3b (owner, 2026-11-08, decision 19)

There is no separate x64 stage any more: the switch goes straight to x64, and the office gets one rollout. Where each item of the former
stage 5 is now:

- 64-bit TWAIN: the TWAIN code made pointer-size-safe and tested in both bitnesses (S3a-12); the 64-bit TWAINDSM shipped by this
  program's setup (S3b-7; its download asked when 3b starts, O-16); the TWAIN driver project's 64-bit data sources and their test on the
  clinic's real sensor (3b entry conditions, with the driver project's state on 2026-11-08).
- The tooth chart's SharpDX and Tao engines: kept on x64, with today's fallback down to 2D (S3a-15); the WPF 3D chart still replaces
  them later (`docs/roadmap.md`), but it is no longer a prerequisite of 64-bit.
- `Direct2dWrapper` x64 with its own copy destination and a corrected sln `Any CPU` mapping: S3a-16, then S3b-4. `WebView2Loader.dll`
  x64 from the package: S3b-4.
- 32-bit vendor DLLs and COM pieces: QuickBooks Desktop (QBFC), Schick CDR, Topaz and Guru (`MedVisorInterface.dll`) get the 32-bit
  helper program or a 64-bit replacement (S3a-13, S3a-14, O-14, O-15); VisiQuick (`vqdde32.dll`) and the Belgian eID card
  (`eidlib.dll`) were checked: nothing loads them (S3a-14).
- The registry view change (no `WOW6432Node` assumptions) and other code that assumes 32-bit: S3a-12.
- Setup `{commonpf64}` with `ArchitecturesInstallIn64BitMode`, the self-contained x64 runtime (no runtime install, decision 20) and
  vc_redist x64, the PE check 0x8664: S3b-7.
  `/Platform:x64` in the lane and runners (`DOTNET_ROOT_X64` only for a per-user runtime): S3a-12 for the x64 test runs, then S3b-1.
  The Middle Tier as x64: S3b-6.

---

## 5. Downloads by stage (exact ids; nothing fetched by this plan)

| Batch | Stage | Items |
|---|---|---|
| none | 0, 1, 3a | Mono.Cecil 0.10.3 is copied from the ignored `packages/Mono.Cecil.0.10.3` folder into the offline folder (local file, no download). Already installed by the owner (2026-11-08): Visual Studio Build Tools 2026 18.11.3 with the machine-wide .NET SDK 10.0.403 and the x86 and x64 .NET 10.0.14 runtimes (NETCore, WindowsDesktop, AspNetCore); the .NET SDK 10.0.114 per user. Added by the owner on 2026-11-08 with the Visual Studio Installer (not a download batch): the MSVC v143 component of the Build Tools 2026 instance (O-4 resolved). Later SDK bumps are approved one by one (section 2.4). |
| A | 2 | `MySqlConnector` 2.6.2; `Microsoft.Web.WebView2` 1.0.2420.47; `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `System.Diagnostics.DiagnosticSource` at the current 10.0 patch (10.0.14 at the time of writing), with the net462 closure the lock files list |
| B | 4 | `MSTest.TestFramework` 4.5.x; `MSTest.TestAdapter` 4.5.x (same patch, chosen at download); `Microsoft.NET.Test.Sdk` (current stable at download); the closure the lock files list (`MSTest.Analyzers`, `Microsoft.Testing.Platform`, `Microsoft.Testing.Extensions.*`, `Microsoft.TestPlatform.TestHost`, `Microsoft.CodeCoverage` as applicable) |
| C (non-NuGet) | 3b | Nothing for the build PC: Build Tools 2026 are there (batch "none"), and the new Middle Tier is self-contained (decision 20), so the 3b gate runs it on its own runtime. Nothing for the office today: it runs no Middle Tier server (O-7). A future office Middle Tier server is self-contained too and needs no runtime on its host; only its IIS option needs the .NET 10 Hosting Bundle, installed for its ASP.NET Core Module only (`OPT_NO_RUNTIME=1 OPT_NO_SHAREDFX=1`). The setup downloads no runtime: the program carries its own (decisions 6, 19 and 20). |
| D (NuGet) | 3b | No x64 apphost package: both SDK bands have `Microsoft.NETCore.App.Host.win-x64` 10.0.14 in their packs folder (checked 2026-11-08); only if the S3b-0 spike shows the pinned SDK asking for another version: `Microsoft.NETCore.App.Host.win-x64` at that version. The x86 apphost of the self-contained helper (O-15), only with the per-user band (a), whose packs lack it: `Microsoft.NETCore.App.Host.win-x86` 10.0.14. **Runtime packs** (decision 20; part of batch D, approved with it on 2026-11-08), at the runtime version of the pinned SDK (10.0.14 for both installed bands, their `BundledNETCoreAppPackageVersion`): `Microsoft.NETCore.App.Runtime.win-x64`, `Microsoft.WindowsDesktop.App.Runtime.win-x64`, `Microsoft.AspNetCore.App.Runtime.win-x64` (the Middle Tier server), and for the helper `Microsoft.NETCore.App.Runtime.win-x86` and `Microsoft.WindowsDesktop.App.Runtime.win-x86`; each later monthly patch of the same ids comes with the SDK bump (section 2.4). Always: `System.Management` 10.0.14; `System.CodeDom` 10.0.14; `System.Security.Cryptography.ProtectedData` 10.0.14; `System.ServiceProcess.ServiceController` 10.0.14; `System.Diagnostics.EventLog` 10.0.14; `System.DirectoryServices.AccountManagement` 10.0.14; `System.DirectoryServices.Protocols` 10.0.14 (its dependency); `System.Speech` 10.0.14 (voice perio, decision 13); `WinForms.DataVisualization` 1.10.2 (decision 10); `Microsoft.Web.WebView2` 1.0.4314.35; `Mono.Cecil` 0.11.6; `Microsoft.Extensions.Hosting.WindowsServices` 10.0.14 and `Microsoft.AspNetCore.Mvc.Testing` 10.0.14 with the closures their lock files list; `Microsoft.CodeAnalysis.VisualBasic` with `Microsoft.CodeAnalysis.Common`, its dependency, at the same version (the current stable release, chosen at download; RDL `<Code>` blocks, decision 14); `Microsoft.XmlSerializer.Generator` (version chosen at download; the two XmlSerializers files, S3b-3, S3b-4); optional `Google.Apis.Gmail.v1` 1.78.0.4295. The `Microsoft.Extensions.*.Abstractions` are already in the feed from batch A. `System.Security.Cryptography.Pkcs` is not (it leaves in stage 2); if a net10.0 package lists it, the S3b-0 spike shows it and it joins this batch. All "10.0.14" versions mean the current 10.0 patch at download; the runtime packs follow the pinned SDK's runtime version. |
| TWAINDSM (non-NuGet, not a batch) | 3b | TWAIN DSM 2.5.1, the 64-bit `TWAINDSM.dll`, from the TWAIN Working Group's GitHub release (signed by the TWAIN Working Group), shipped by this program's setup (S3b-7). Not covered by the owner's approval of batches A to D: asked when 3b starts, with file name, source and size (O-16). |

The survey's conditional items: the Roslyn compiler for RDL `<Code>` is now in batch D (decision 14). The others belong to features the
owner first cut and then kept (owner correction, 2026-11-08): the dial-up terminal's `System.IO.Ports` packages, the RDL providers'
`System.Data.Odbc`/`System.Data.OleDb`/`Microsoft.Data.SqlClient` (RdlDesign's data source dialogs use them too), the WCF client
packages for Divvy. They join a batch when the lead plans those ports (open point O-10). The kept SOAP clients, Change Healthcare ITS
and the DCI eligibility path included, need no package.

---

## 6. What must be checked by launching the program (screenshots)

Same database, same monitor, before and after; one PC at 100%, one at 150% (4K), one pair of monitors with different scaling; also with
`NoDpi.txt`, with `NoCustomBorders.txt`, and with Windows "Make text bigger" above 100%.

- **Stage 0:** e-mail through the fake or a local test mail server: receive a plain, an HTML message with an inline image, one with
  attachments and a signed one, and view each in the inbox and the preview; send over port 465 (implicit TLS) with an inline image and an
  attachment, and check what arrives (image shown, attachment name); send a signed message. The rebuilt EOB and amendment window
  (S0-6) from all four entry points: Scan EOB / View EOB in Insurance Payment (EOB), View EOB in Edit Claim (a payment's EOBs and a
  preauthorization's), Scan EOB / View EOB in Enter Estimates for PreAuth, and Scan / View in Edit Amendment. In each, scan a page and a
  multi-page document, import, paste, show an image and a PDF, zoom and pan, print, export, copy, delete and close, then check the
  caller's scanned state; once with an A-to-Z folder and once with images in the database. Then, after S0-6's removal commit, the
  Images module and the Imaging preferences page without the old-module check box; the Appointments module
  (ControlAppt resources). The checks of S0-2a's removals are withdrawn with it.
- **Stage 1:** start the program from `bin\Debug`, open each module once; open the Appointments module, a report through
  FormReportComplex, an RDL report; start RdlDesign (open and preview a report), CentralManager and DocumentationBuilder once; the
  screenshots must be identical to the classic build.
- **Stage 2:** an eForm and the e-mail preview (WebView2 from the package); sign in to the database (MySqlConnector 2.6.2), once over a
  non-loopback address with TLS and a client certificate (environment per O-8).
- **Stage 3a (net48):** right-click menus on OD grids (Account, Appointments, Chart progress notes, claims, reports) and every toolbar
  drop-down arrow (Account Payment/Statement, Images): font, height, width, check marks, separators, `&` accelerators; the User Query
  window grid (column widths, formatting, copy); RdlDesign's menus and its grid dialogs, CentralManager's menus; a progress window that
  finishes, one that fails and one that is cancelled; opening a PDF
  document, an image, a wiki link, a link in a commlog note, the A to Z folder, a claim attachment, the display settings from the zoom
  window, the on-screen keyboard from the patient search; copy and paste of an image document between two patients (in one program and
  between two programs); installing the HL7 service from ServiceManager; a self-update from a standard (non-administrator) user: the UAC
  prompt for UpdateFileCopier appears and the update completes; the setup started without administrator rights asks for elevation.
  Wave 3 (64-bit preparation, still x86): a TWAIN scan with the sensor and with a document scanner after S3a-12 (its changes must be
  no-ops on x86); each link through the 32-bit helper (QuickBooks Desktop, Schick CDR, a Topaz pad, Guru) where a test PC has the vendor
  software, including signing with the helper's Topaz window and showing a stored Topaz signature; the tooth chart in its three modes
  and the drawing through `Direct2dWrapper`, unchanged. The EHR launch checks of `docs/specs/ehr-rebuild-plan.md` section 11, on a test
  database, for every EHR package merged by then: turning EHR on in Show Features and in the Setup Wizard, the EHR window, provider keys and
  permissions, the time check, value sets, CCD and CCR display and exports, Measure Calc, Quality Meas. with the QRDA export, the release
  dry run's staged file list, and Direct sending.
- **Stage 3b (net10):** the main window and every module (toolbars, grids, `MenuOD` main menu, custom borders); the forms that used the
  default font (splash screen, clinic picker, `ToolTipOD`, progress windows, `MsgBoxCopyPaste`, capture notice/status windows, ODR
  parameter input, dashboard edit and print dialogs, RDL viewer dialogs, `FormSnap`, the DbSettings, ServiceManager and UpdateFileCopier
  windows), also with "Make text bigger" above 100% (slightly larger text accepted, decision 9); DPI changes when dragging a window between
  monitors; dashboards and income/production graphs and their printing (`WinForms.DataVisualization`); print previews and real prints to a
  PDF printer (sheets, statements, claim forms, RDL reports: margins, fonts, page counts); WPF Frm screens hosted in FormFrame with their
  combo, calendar and tooltip pop-ups and keyboard focus; the Images module's hosted strips and the EOB viewer; the tooth chart in WPF/2D,
  DirectX and OpenGL modes; voice perio charting; WebView2 panes at each DPI; ImageLists created in code (icon edges with 32-bit depth);
  TreeView check boxes, DateTimePicker, MonthCalendar, RichTextBox text; e-mail receive and send; a clearinghouse send to a sandbox; a Word
  letter merge; an RDL report with a VB `<Code>` function; RdlDesign opens, edits and previews a report and checks a `<Code>` block;
  CentralManager and DocumentationBuilder start and do their main task; PluginExample loads and a .NET Framework plugin DLL gets the
  clear message; the self-update end to end between two .NET 10
  builds from a standard user (publish on a test update server, pull on a second test PC, UpdateFileCopier runs elevated from its temp
  folder with its runtime, program restarts), with an unchanged runtime (only the program part pulled), and the first release with a new
  runtime patch the same way before the office gets it (both parts pulled, decision 20); the setup on a clean VM without any .NET
  (nothing installed for .NET; the program, ServiceManager and the DbSettings tool start) and over the bridge release.
  On x64 (decision 19): Help > About and the diagnostics show a 64-bit process; TWAIN capture with the clinic's real sensor through the
  64-bit TWAINDSM and the driver project's 64-bit data sources, and a document scan with a scanner's 64-bit driver; each helper link from
  the x64 program where a test PC has the vendor software; the tooth chart in DirectX and OpenGL modes on x64, and a mode that cannot
  start falling back; the Snipping Tool start of the claim attachment windows and the on-screen keyboard (the system-folder fixes of
  S3a-12); the setup's move from a 32-bit bridge install on a test PC: the new folder in `Program Files`, the old folder removed or its
  leftovers named, the Start menu and desktop shortcuts, `FreeDentalConfig.xml` and the flag files carried over, the HL7 service
  reported; and a mixed pair of workstations, where the one still on the bridge release refuses the x64 release with "run the new
  setup".

---

## 7. Tests to add for behaviour differences

| Difference | Test | Stage |
|---|---|---|
| `UseShellExecute` default false on .NET | Unit test that every `ODFileUtils` start overload and `ShellOpen` build a `ProcessStartInfo` with `UseShellExecute==true`; NoHqTests source scan of the shipped projects: no `Process.Start(string…)` outside `ODFileUtils`. | 3a |
| Elevation (CreateProcess refuses a `requireAdministrator` program) | Source scan of the shipped projects: every `new Process()`/`new ProcessStartInfo(` block sets `UseShellExecute` explicitly; a test that the UpdateFileCopier start in `PrefL` uses `UseShellExecute==true`; launch check: self-update from a standard user. | 3a, 3b |
| Thread.Abort | ProgressWin success/exception/cancel test on an STA thread; ODThread `Join(timeout)` returns false, never throws. | 3a |
| Legacy WinForms menus | NoHqTests source scan for `ContextMenu`/`MenuItem`/`MainMenu`/`DataGrid`/`Control.ContextMenu`; WFDEV006 as an error. | 3a, 3b |
| Default font | Source test that each exe's `Main` calls `SetDefaultFont` first and `SetHighDpiMode` before anything creates a window (before ProgramEntry's splash and NoDpi handling), except that in `ProgramEntry.Main` it allows EHR-3's `/ehr-render` branch before `SetDefaultFont`, since that branch ends the process (`Environment.Exit`) before any window exists; runtime test, in a child process that the test starts and in which no window exists yet (the extracted start-up font/DPI method, or the exe with a test switch), that a plain `Form` created after the program's start-up routine has the family Microsoft Sans Serif and the size 8.25 pt times the Windows text scale factor (`TextScaleFactor` under `HKCU\Software\Microsoft\Accessibility`, 100 when absent), so it also passes on a PC with "Make text bigger". `SetDefaultFont` throws once any window exists in the process, so a shared test host cannot run it. | 3b |
| Number formatting | Pins: `(-0.0).ToString("F2")`, `Math.Round(-0.004,2).ToString("F2")`, `(0.1+0.2).ToString()`, `SIn.Double("1e400")` invalid; the golden-output comparison (837, statements, signature keys, exports, SQL text). | 3a (record), 3b (compare) |
| Encodings | Fresh-process test: right after `ODInitialize.Initialize()`, `Encoding.GetEncoding(1252)` and `(850)` work before any PDF code ran (PDFsharp registers the provider itself, so failures would otherwise look intermittent); round trips for capture.ini (`LocalCaptureConfigTests`), Canadian 850, an e-mail with a `windows-1252` charset; the three ANSI tests use 1252 explicitly. | 3a, 3b |
| ICU vs NLS | Startup assert that NLS is in use (decision 4); patient-name sort test (`O'Brien`, `OBrien`, `Smith-Jones`, `Smith Jones`, `Smithjones`) against the net48 order; CRLF, NUL and soft-hyphen searches in wiki markup and user queries. | 3a (record), 3b |
| `XmlSerializer` and obsolete members | Mock round trips with a `PrefName` argument, a `ClinicPref` and an `Adjustment` (S0-T), on net48 and net10; source scan: no `PrefName.<name>` reference to the 25 obsolete members outside `Pref.cs`; reflection test: no `PrefName` member carries an `ObsoleteAttribute` with `IsError==true`. | 0, 3a, 3b |
| Received mail | The corpus recorded with the Direct-based parser and compared with the MimeKit parser; a sender with consecutive dots keeps its raw text. | 0 |
| EOB and amendment window rebuilt (S0-6) | `EobAmendmentFilesDbTests` on a scratch database, with an A-to-Z folder and with images in the database: the EOB lists of a claim payment and of a preauthorization, adding, deleting and exporting EOBs, replacing and deleting an amendment's file, with the same rows, files and log entries as the old window, except the differences S0-6 lists (paste onto an amendment that has a file, export and PDFs with images in the database, the Copy and amendment print log entries); the launch check from all four entry points. | 0 |
| Addresses with consecutive dots (sending, validation) | `AddressRulesTests`: today's result for `a..b@example.com` pinned on net48; on .NET 10 the address is refused with a clear message, not a crash. | 3a, 3b |
| Config file name | Release-layout test: next to `OpenPmsDental.exe` are `OpenPmsDental.dll`, `.runtimeconfig.json`, `.deps.json` (and `.dll.config` only if kept); no `*.exe.config` is staged; the setup deletes old `*.exe.config`. | 3b |
| Package assets for a runtime identifier | No folder under `bin\Release` except `Helper32\` (Build-Release check); a test that calls WMI (`System.Management`) from the staged folder. | 3b |
| Apphost resources | `RT_MANIFEST` (execution level), `RT_VERSION` (file and product version) and `RT_GROUP_ICON` (present, same icon bytes) of every staged `.exe` equal the net48 record. | 3a (record), 3b |
| Update files | Test that the staged UpdateFileCopier, copied with the x64 runtime files into an empty temp folder as `OpenFileCopier` does, runs there (x64) and replaces the program folder's runtime files; build id includes `OpenPmsDental.dll`; retired-files list equals `[InstallDelete]`; the bridge release refuses an unreadable manifest and a release for another runtime or another architecture (decision 19), and reads the runtime part's keys of a .NET 10 manifest (S3a-10). | 3a, 3b |
| Self-contained runtime part (decision 20) | ReleaseFlowTests: a publish whose runtime key equals the published one writes no runtime rows, and a pull of it downloads no runtime segment; a publish with a new runtime key replaces the runtime part, and a pull downloads and checks both parts before anything is copied; a pull whose runtime files do not all match the one key is refused and nothing is copied; after a copy whose runtime files do not all match, UpdateFileCopier writes no release manifest, so a workstation never claims runtime files of two versions; every staged `.runtimeconfig.json` (the helper's too) includes frameworks of one runtime version; Build-Release refuses mixed runtime versions and a runtime older than the newest 10.0.x runtime pack in the offline folder. | 3b |
| Database TLS | Non-loopback connection to a `REQUIRE X509` user with `VerifyFull`, with a CAPI and a CNG client key (environment per O-8). | 2, 3b |
| Setup | Elevated `Test-Installer.ps1`: no per-user install; network server runs the tool `/silent`; on .NET 10, the setup installs no .NET runtime, and the installed tool runs on the install folder's own runtime (`coreclr.dll` loads from there), with `/PREREQS=skip` too; on .NET 10, the install in `Program Files` with its uninstall entry in the 64-bit view, and the install over a 32-bit bridge install (`FreeDentalConfig.xml` and the flag files carried over, the old install and its uninstall entry gone, the 64-bit TWAINDSM installed when missing). | 3a, 3b |
| Clipboard | In-process round trip of the image-node copy through the JSON string format. | 3a |
| Middle Tier | The mock suite (S0-T) on both runtimes; the host round-trip suite in `tests/MiddleTierHostTests` (S3b-6). | 0, 3b |
| Plugins (decision 16) | A DLL whose `TargetFrameworkAttribute` names .NETFramework gets the clear message and is not loaded; the rebuilt PluginExample loads. | 3b |
| Crypto | Known answers recorded on net48 (S3a-4) equal on net10. | 3a, 3b |
| RDL `<Code>` blocks (CodeDom compiling throws on .NET; decision 14) | A fixture report whose expression calls a VB `<Code>` function gives the output recorded on net48 (S3a-4), compiled with Roslyn (S3b-2); a `<Code>` block with an error logs that error with its line; RdlDesign's syntax check reports it too. | 3a (record), 3b |
| XmlSerializers files (kept; critique W1) | The Release dry run stages `OpenPmsDental.XmlSerializers.dll` and `OpenPmsDentalBusiness.XmlSerializers.dll` after every package that removes a `WebReferenceUrl` item, and on .NET 10 from `Microsoft.XmlSerializer.Generator`; a ReleaseFlowTests test checks that both ship. | 0, 1, 3a, 3b |
| Type names | `wikilisthist.ListContent` written by net48 reads on net10; Middle Tier round trip for every parameter type the remoting layer uses. | 3a, 3b |
| Pointer size (decision 19) | TWAIN and DirectShow structure sizes and field offsets against a table with an x86 and an x64 column, run in both bitnesses (the x64 test builds of S3a-12 on net48, then on net10); the `MSG` offsets from a declared structure; the TWAIN protocol against `FakeTwainDsm` in both bitnesses; source scan: no `IntPtr.Size==4` gate, no `ToInt32()` on a handle or pointer passed to native code, no 32-bit window-long entry point for a pointer. | 3a, 3b |
| Registry views and system folders (decision 19) | Source scan: every `Registry.LocalMachine` read of a vendor key names its `RegistryView`, and no code builds a `WOW6432Node` path; the `sysnative` sites choose by `Environment.Is64BitProcess`. | 3a |
| 32-bit helper (S3a-13, S3a-14) | Protocol tests (start, call, reply, error, a crash during a call, timeout, a missing helper, no helper left after the program ends, one helper per program process); each vendor module against a fake of its vendor interface; source scan: no project but the helper references the 32-bit vendor pieces; on .NET 10 the x86 helper answers the x64 program. | 3a, 3b |
| Tooth chart engines on x64 (S3a-15) | The CLI headers of the SharpDX and Tao DLLs: IL-only, not 32-bit-required; in an x64 process each mode starts through `StartFirstWorkingMode`, or falls back and says which mode came up. | 3a, 3b |
| Native files (decision 19) | PE machine 0x8664 for every staged exe and native DLL (`Direct2dWrapper.dll`, `WebView2Loader.dll`) and for the x64 runtime part, 0x14C for the helper and its runtime in `Helper32\`; the x64 `Direct2dWrapper.dll` exports what its Win32 build exports and every entry point `Direct2d.cs` imports. | 3a (`Direct2dWrapper`), 3b |

---

## 8. Survey disagreements and corrections, and how they were resolved

1. **FormReportComplex.resx manifest name.** One survey: changes under the SDK convention; another: unchanged. Evidence:
   `OpenPmsDental.csproj:12` RootNamespace `OpenPmsDental`; `FormReportComplex.cs:15,17` namespace `OpenPmsDental.ReportingComplex`, class
   `FormReportComplex`. The folder-based and class-based names are the same, so nothing changes. UpdateFileCopier's linked
   `MsgBoxCopyPaste.resx` was checked by S1-1: it keeps the manifest name `UpdateFileCopier.MsgBoxCopyPaste.resources`, because SDK-style
   net48 projects turn off the `DependentUpon` naming convention. UpdateFileCopier never shows that window.
2. **Is the apphost pack in the SDK?** The question was asked for win-x86; since decision 19 the build needs win-x64. Both SDK bands
   have it: the machine-wide folder `%ProgramFiles%\dotnet\packs\Microsoft.NETCore.App.Host.win-x64` (8.0.33, 9.0.22 and 10.0.14, beside
   the win-x86 pack with the same versions; the 10.0 packs came with SDK 10.0.403) and the per-user SDK 10.0.114
   (`Microsoft.NETCore.App.Host.win-x64` 10.0.14). So the win-x64 pack is local with either band. The self-contained x86 helper (O-15)
   also needs the win-x86 pack: the machine-wide band has it, the per-user band does not, so with that band batch D adds
   `Microsoft.NETCore.App.Host.win-x86`. The spike checks the restore either way.
3. **ICU or NLS.** One survey: NLS at the switch; another: ICU plus analyzers, NLS as fallback. Decided: NLS for the first .NET 10
   release (decision 4): 1,573 culture-sensitive `IndexOf/StartsWith/EndsWith` calls with literals (plus 495 with variables) cannot be
   reviewed before the switch, and NLS keeps today's behaviour; ICU becomes its own later release after the CA1307/CA1309/CA1310 clean-up.
4. **Number of ProgressWin sites.** The surveys counted about 180-190 with wider patterns. Grep `new ProgressWin\(` gives 127 in 54 files,
   including `ProgressWin.cs` itself (4) and the legacy UnitTests project (9). The conclusion (every progress window fails at its end) is
   the same.
5. **QBFC, Schick, Topaz.** Two surveys: remove; one: removal optional for .NET 10. Resolved first as remove (S0-2a): the office uses
   none of them, QBFC's interop DLL is a CLR 1.1 binary that would stay in `Required dlls`, and `TopazWrapperTests` compiles code with
   CodeDom, which throws on .NET. Changed by the owner correction of 2026-11-08: all three are kept and ported; these facts go into
   the planning of their ports (open point O-10).
6. **SOAP clients: dotnet-svcutil or hand-written.** The Middle Tier survey recommends regenerating DWS/DEA/NewCrop with `dotnet-svcutil`
   on the WCF client packages. The plan hand-writes one `HttpClient` + `XmlSerializer` caller over the existing data classes in 3a, for ITS
   too, because on net48 the old proxies still exist, so a test can prove the new envelopes equal the old ones before the old code goes; it
   also avoids four WCF packages and a tool download.
7. **Where the Middle Tier host context lives.** Survey: business library, plus a settable path in DataConnectionBase. The plan puts one
   host context in DataConnectionBase, which both ConnectionStoreBase and the business library can see.
8. **Code-page provider registration on net48.** On .NET Framework all code pages are built in, and `CodePagesEncodingProvider` would need
   a package there, so registration lands in 3b (one line in `ODInitialize` and each `Main`).
9. **x86 runtime for the tests.** One survey proposes a per-user x86 install with `DOTNET_ROOT_X86`. Since decision 19 the tests run
   x64: the build PC has the machine-wide x64 runtimes 8.0.33, 9.0.22 and 10.0.14 under `%ProgramFiles%\dotnet\shared` (10.0.14 came
   with Build Tools 2026), so no further install is needed; an environment variable is needed only for a per-user install. The x86
   runtimes under `%ProgramFiles(x86)%\dotnet\shared` are installed too and are not used.
10. **Toolchain.** The surveys planned SDK 10.0.1xx under the MSBuild 17.14 of Build Tools 2022 from stage 1, and Build Tools 2026 only
    for 3b. Changed (decision 1): the lane and the test runners move to Build Tools 2026 in S1-0, because SDK 10 under MSBuild 17.14
    downloads a compiler package for every SDK-style project, every later SDK band needs MSBuild 18, and vstest 17.14 lacks the x86 test
    host fix (no longer needed since decision 19: the tests run x64). The SDK band is chosen in S1-0 (section 2.4).
11. **Middle Tier SOAP compatibility.** Not needed: the server has never shipped to an office, and the version check already forces client,
    server and database to the same build.
12. **When to delete the orphaned resx entries.** Stage 0: it needs no project file change.
13. **Where the legacy UnitTests coverage goes.** The draft said the new host round-trip tests (3b) replace the UnitTests mock tests, but
    the project was to go in stage 0. Resolved: S0-T ports the mock suite, the e-mail tests and the ODThread tests to `tests/` first, so
    the coverage runs on both runtimes before S0-3 and S0-4 change the code. Since the standing rule (2026-11-08) the project stays too,
    as a running suite without the four ported classes (S0-5).
14. **"WebReferenceUrl items are design-time only."** Wrong for Release builds (SGen). Resolved first in S0-2a by dropping the serializer
    assemblies. Since the standing rule and the revert of S0-2a both files ship again: every package that removes a `WebReferenceUrl`
    item proves with a Release dry run that both are still built, and 3b builds them with `Microsoft.XmlSerializer.Generator`.
15. **Runtime identifier at the switch.** The draft said "no RuntimeIdentifier"; the tooling survey said "RID set". Resolved: RID set for
    exes and tests (S3b-1), because of the `runtimes\win\...` assets. Since decision 20 the exes are also self-contained; the tests are not.

---

## 9. Spot checks relied on, and what was checked and found clear

### 9.1 Spot checks (blockers relied on)

At the survey commit:

- `FrmProgressAuto.xaml.cs` calls `_threadWorker.Abort()` at 125, 142 (success path), 242, 248.
- `ODFileUtils.cs` starts processes at 172, 190, 196, 307, 309 with the default `UseShellExecute`; only line 207 sets it (false).
- 112 `.csproj` files reference `Microsoft.VisualStudio.QualityTools.UnitTestFramework`.
- 272 fully qualified `new System.Windows.Forms.ContextMenu/MenuItem/MainMenu/DataGrid(` in 50 files.
- `HostingEnvironment`/`HttpContext.Current`: 12 occurrences in 5 files (ConnectionStoreBase, Plugins, DtoProcessor, PluginLoader, Userods).
- `PrefL.cs:562` copies only `UpdateFileCopier.exe`.
- `installer/OpenPmsDental.iss:731-734` refuses without .NET Framework 4.8.
- `ForkReleases.GetPublishedManifest` returns null on a parse error (398-411); `Decide(null,…)` publishes unless the role is `OtherComputer`
  (237-242).
- `PasswordVaultWrapper.csproj:64` references `Required dlls\Windows.winmd`.
- `Canadian.cs:1638,1683,1962` use code page 850; `OpenPmsDentalHL7` uses `ServiceBase` and `[RunInstaller]`.
- The build lane script uses the Visual Studio 2022 Build Tools paths and never passes `/restore`.
- `OpenPmsDentalBusiness.csproj:2667,2676` COM references ADODB and CDO; `OpenPmsDentalServer.csproj:112` imports
  `Microsoft.WebApplication.targets`; `OpenPmsDental.csproj:186` and `OpenPmsDentalGraph.csproj:72` reference
  `System.Windows.Forms.DataVisualization`.
- `DpiAwareness=PerMonitorV2` appears only in `OpenPmsDental/app.config:116` (and in `UnitTests/app.config:65`).

Added for this version, at `8018a815f` and on the build PC on 2026-11-08:

- The build PC: Visual Studio Build Tools 2026 18.11.3 (MSBuild 18.11.1, vstest 18.11) beside Build Tools 2022 17.14.44 (MSBuild 17.14,
  vstest 17.14). The 2026 instance has the .NET desktop, C++, web, Universal and MSBuild build-tools workloads, the .NET Framework 4.8
  SDK and the 4.8 and 4.7.2 targeting packs, Testing tools core, NuGet build tools, the Windows 10 SDK and the Windows 11 SDK 10.0.26100,
  and the .NET 8, 9 and 10 runtime components. Its MSVC toolsets are 14.51 (v145, the default) and 14.44 (v143, component
  `Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64`, added by the owner on 2026-11-08; a Win32 v143 DLL built with MSBuild 18.11
  links with 14.44); Build Tools 2022 have 14.44 (v143). Both instances have
  `Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll` under `Common7\IDE\Extensions\TestPlatform`. Machine-wide SDKs 9.0.320 and
  10.0.403, whose packs include `Microsoft.NETCore.App.Host.win-x64` and `Microsoft.NETCore.App.Host.win-x86` (8.0.33, 9.0.22, 10.0.14
  each); machine-wide x64 and x86 runtimes 8.0.33, 9.0.22 and 10.0.14; per-user SDK 10.0.114, whose packs are
  `Microsoft.NETCore.App.Host.win-x64` (10.0.14) and the NETCore, WindowsDesktop and AspNetCore reference packs.
- `tools/Build-Release.ps1`, `tools/New-InitSql.ps1`, `tools/New-DevDatabase.ps1`, `tools/AdoptionValidation/Invoke-AdoptionRehearsal.ps1`
  and `tools/SyntheticData/Invoke-SyntheticData.ps1` find MSBuild with `vswhere -latest -products *` when given no lane or path, so they
  now find MSBuild 18. `tools/run-tests.ps1` tries the Build Tools 2022 vstest path first, then `vswhere -latest -products *`. Plain
  `vswhere -latest` (without `-products *`) finds no Build Tools instance. `Directory.Build.targets` finds
  MSTest v1 through `$(VsInstallRoot)`. `README.md` (Building) names Visual Studio 2022 or its Build Tools. `Direct2dWrapper.vcxproj` sets
  v143 in all four configurations; the setup's VC++ check accepts any major version 14 (`OpenPmsDental.iss:196-201`).
- Health.Direct: 88 matching lines in `Data Interface/EmailMessages.cs` (4,185 lines); `DatabaseMaintenances.cs:11749-11757`;
  `EmailPreviewControl.cs:13,29`; stray usings in 7 more files; references in `OpenPmsDental.csproj:103`,
  `OpenPmsDentalBusiness.csproj:103-110`, `UnitTests.csproj:98`, `tests/LibraryUpdateTests/LibraryUpdateTests.csproj:53-57`; the three DLLs
  were shipped in release 0.0.
- Signed sending uses the Direct library (`SendEmailUnsecureWithSig`, `EmailMessages.cs:1123-1144`); the CDO path's custom headers have
  one source, the Direct path (`EmailMessages.cs:616-634`); `FormEmailDigitalSignature`'s trust text names only encrypted e-mail and
  Direct.
- `FormEmailCertRegister` no longer calls `CustomerUpdatesProxy` (O-13, done by DIRECT-CERT): it imports the Direct provider's
  certificate file through `OpenPmsDentalBusiness/Email/DirectCertificateImport.cs` and sends nothing anywhere. Before, it called
  `CustomerUpdatesProxy.GetWebServiceInstance()`, which throws `HqNotAvailableException` in this build. `CustomerUpdatesProxy`, with
  `ICustomerUpdates` and `CustomerUpdatesNoHq`, was then deleted (DIRECT-CERT-FOLLOWUP).
- `Forms/FormImages.cs` hosts `ControlImagesOld` for EOBs and amendments (5 call sites in 4 files; they set only `ClaimPaymentNum`,
  `ClaimNumPreAuth` or `EhrAmendmentCur` and call `ShowDialog`); the module choice `ImagesModuleUsesOld2020` is set to 0 by
  `ConvertDatabases7.cs:2760,3113`. Every image read, import, export and delete of those three uses is an `ImageStore` method
  (`OpenImagesEob`/`OpenImagesAmd`, `ImportEobAttach`/`ImportAmdAttach`, `ExportEobAttach`/`ExportAmdAttach`,
  `DeleteEobAttach`/`DeleteAmdAttach`, `CleanAmdAttach`); the two export methods are called only by `ControlImagesOld`. Showing a PDF
  (`ControlImagesOld.cs:2919-2957`) and opening a file in its Windows program (the double-click, 262-338) read the stored file in the
  control itself: the stored path with an A-to-Z folder, a temp file from `FileAtoZ.Download` or `CloudStorage.Download` with the cloud,
  and with images in the database the `RawBase64` of the shown document, which is empty for an EOB or an amendment (the double-click shows
  the message to export instead). `ImageStore` has no EOB or amendment method for either. The new module's `ImageSelector` and
  `ControlImageDisplay` work only on a patient's documents and mounts; its document scan uses the same `EZTwain` calls as the old
  module's.
- `UnitTests/Resources/dump.txt` (2.4 MB) is read by 22 test helpers, DevDbTool and three tool scripts; `UnitTests/packages.config` is the
  only `packages.config` (Mono.Cecil 0.10.3; `packages/` is git-ignored). UnitTests holds 88 + 5 Middle Tier mock tests, 27 e-mail tests
  and 9 ODThread tests (7 active).
- `RdlDesigner.exe` is no longer in `Required dlls` (deleted in ORG-CLEANUP); RdlDesign, DocumentationBuilder and PluginExample were not
  in the sln at the survey, so nothing built them; RdlDesign is in the sln and the slnf since S1-1 (still not shipped, decision 17),
  DocumentationBuilder joins them in S1-5 and PluginExample in S3b-4. CentralManager (`CentralManager/xCentralManager.csproj`) and
  UnitTests are in the sln and the slnf.
- The kept unshipped programs (decision 17), checked on 2026-11-08. **RdlDesign** (`RdlDesign/ReportDesigner.csproj`, WinExe
  `RdlDesigner`, .NET Framework 4.5): ProjectReferences to RdlEngine and RdlViewer only, plus framework references (WinForms, Drawing,
  Data, Xml). For .NET 10 it needs: its WinForms `MainMenu`/`ContextMenu`/`MenuItem` code in `DesignCtl.cs` and `RdlDesigner.cs` (209
  constructions) and the WinForms `DataGrid` in 10 files, with a custom column style (`DGCBColumn.cs`), converted (S3a-M3); its
  `VBCodeProvider` syntax check (`CodeCtl.cs:232-273`) moved to Roslyn (S3b-2); `System.Data.SqlClient`, `Odbc` and `OleDb` code in 4
  files (with the RDL providers, O-10); `Process.Start` of links (S3a-2). **DocumentationBuilder** (WinExe, .NET Framework 4.8; its own
  `xDocumentationBuilder.sln`): ProjectReferences to CodeBase, DataConnectionBase, the business library and UnitTests (it documents the
  `UnitTests.Documentation` test numbers), and a HintPath to `OpenPmsDental\bin\Debug\OpenPmsDental.exe` (S1-5 makes it a project
  reference); an unused `System.Data.SqlClient` using (`Form1.cs:24`); `XslCompiledTransform` with default settings and no script blocks
  in its `.xsl` files; `Process.Start` of the written XML file (`Form1.cs:409`, S3a-2). **CentralManager** (WinExe, .NET Framework 4.8):
  ProjectReferences to the program, the business library, WpfControlsOD, PasswordVaultWrapper, CodeBase and DataConnectionBase;
  `System.DirectoryServices` (in the WindowsDesktop pack, S3b-0 (3)) and `System.Deployment` (dropped in stage 1); legacy WinForms menus
  in `FormCentralManager` (S3a-M3); `Process.Start` of the program (`Db/CentralConnectionHelper.cs:51`, S3a-2). UnitTests references
  CentralManager (`UnitTests.csproj:549`) and tests it (`UnitTests/UnitTests/CentralManagerTests.cs`). None of the three uses COM,
  BinaryFormatter in code, a new AppDomain or Thread.Abort.
- `RdlEngine/Definition/Code.cs:59-129` compiles a report's `<Code>` block with `VBCodeProvider.CompileAssemblyFromSource` into a file and
  loads it with `Assembly.LoadFrom`.
- 22 live `new Process()` sites in 16 shipped files, one in xCrudGenerator, plus one commented out.
- Setup: `PrivilegesRequired=admin` with `PrivilegesRequiredOverridesAllowed=commandline` (82-83); `Test-Installer.ps1:67` runs
  `/CURRENTUSER /PREREQS=skip`; the vc_redist download is checked with `Get-AuthenticodeSignature` for a Microsoft Corporation signer
  (243-290).
- `WebReferenceUrl` items: `OpenPmsDental.csproj:9478` (1), `OpenPmsDentalBusiness.csproj:2501-2556` (6: Middle Tier, ITS, DEA, NewCrop,
  DWS, DCI); release 0.0 shipped both `XmlSerializers` assemblies and `OpenPmsDental.exe.config`, `ServiceManager.exe.config`. S0-2a
  retired both assemblies (`GenerateSerializationAssemblies=Off` in both project files, both files out of `$OwnFiles` and into the deny
  list of `tools/Build-Release.ps1`, `ForkReleases.ListRetiredFiles`, the setup's `[InstallDelete]`, `SerializerAssembliesRetiredTests`);
  the revert of its merge undoes all of that.
- `Eclaims/EmdeonMedical.cs` uses two ITS operations (`PutFileExt` at 44, `GetFile` at 103).

Added for decision 19 (64-bit), read-only at `c9463cad1` and on the build PC on 2026-11-08:

- CLI headers of `Required dlls`: `SharpDX.dll`, `SharpDX.Direct3D9.dll`, `SharpDX.Mathematics.dll`, `Tao.OpenGl.dll`,
  `Tao.Platform.Windows.dll`, `Interop.CDRDATALib.dll`, `Interop.CDRDicom.dll`, `Interop.QBFC10.dll` and the three Health.Direct DLLs
  are IL-only with no 32-bit-required flag (PE machine 0x14C, as for any-CPU assemblies), so they load in a 64-bit process;
  `WebView2Loader.dll` and `vcruntime140.dll` are native x86. The QBFC and Schick interop types are embedded (`EmbedInteropTypes`,
  `OpenPmsDental.csproj:106-118`, `WpfControlsOD.csproj:45-56`).
- TWAIN: `NativeTwainDsm.cs:39-42` refuses a 64-bit process; its candidate paths (24-33) end with `twain_32.dll`; `NativeMethods.cs:18-22`
  hard-codes the 32-bit `MSG` offsets; `NativeMethods.cs:90-91` and `ProxyWindow.cs:41` call `SetWindowLong` with `ToInt32()` of a
  window handle; `TwainStructs.cs:7` lays the structures out "as on 32-bit Windows"; `TwainProtocolTests.cs:15` and
  `InteropLayoutTests.cs:22` assert `IntPtr.Size==4` and pin 32-bit sizes.
- In-process vendor native code: `MedVisorInterface.dll` (Guru, `WpfControlsOD/Bridges/Guru.cs:15,21`, called from `ProgramL.cs:213`);
  `SigPlusNET.dll` (Topaz, loaded at run time, `TopazWrapper.cs:105`); the QBFC and Schick COM objects; `vqdde32.dll` only in
  `OpenPmsDental/Misc/VisiQuick.cs`, which is commented out in full; `eidlib.dll` in `SmartCards/Belgium/BelgianIdentityCard.cs:62-74`,
  which has no caller. Every other `DllImport` names a Windows DLL or `Direct2dWrapper` (68 entry points in `UI/Direct2d/Direct2d.cs`).
  COM servers started in their own process: `Word.Application` (`LetterMerges.cs:98`, `ChartLetterL.cs:599`) and CADI's
  `Mediadent.Application` (`CADI.cs:31`). The in-house DDE client (`CodeBase/Utilities/ODDdeClient.cs`) declares its handles as `IntPtr`.
- Registry and system folders: `Trojan.cs:31` and `FormInsPlan.cs:1373` read a vendor key from HKLM without a view;
  `LocalDiagnostics.cs:289` reads with `RegistryView.Registry32`; no code builds a `WOW6432Node` path (`tools/Test-Installer.ps1:43`
  reads the 32-bit install's uninstall entry there); `sysnative` in `FormClaimAttachment.cs:350,383`, `FormClaimAttachSnipDXC.cs:229,262`
  and `FormImageSelectClaimAttach.cs:413,441` (the last with its bitness test reversed); `FrmPatientSelect.xaml.cs:829,842` turns off
  WOW64 file redirection whenever Windows is 64-bit.
- `Direct2dWrapper.vcxproj` has x64 configurations (8-18) with the output folder `bin\x64\$(Configuration)\` (85, 96), whose post-build
  steps copy into the x86 program folder (134, 180); `OpenDental.sln:607-614` maps `Any CPU` to x64 and `x86` to Win32; the sln has no
  x64 solution platform. `tools/Build-Release.ps1` takes the DLL from `Direct2dWrapper\bin\Release` (124), checks PE 0x14C for its
  native files (194, 437-441) and refuses a `TWAINDSM.dll` in the program folder (250).
- Setup: `DefaultDirName={commonpf32}` (`OpenPmsDental.iss:77`), `UsePreviousAppDir=yes` (78), one `AppId` (59), the x86 vc_redist and
  its key in the 32-bit view (159-165, 196-201), the shortcuts `{autoprograms}` and `{autodesktop}` (148-149), and `FreeDentalConfig.xml`
  in the program folder (43, 145; installed without `uninsneveruninstall`, so the uninstaller may remove it). `NoDpi.txt` and
  `NoCustomBorders.txt` are read from the program folder (`ProgramEntry.cs:73,77`).
- The tooth chart: the fallback DirectX, OpenGL, Simple 2D (`ToothChartWrapper.cs:403-457`); window and device handles as `IntPtr`
  (`ToothChartDirectX.cs:1479,1506`, `OpenGLWinFormsControl.cs:72,150`).

Added for decision 20 (self-contained), read-only on the build PC on 2026-11-08:

- Runtime sizes (10.0.14, the files at the top of each runtime folder, without the 13 language folders of the WindowsDesktop runtime):
  x64 `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App` together about 154 MB, about 66 MB zipped; the x86 pair about 144 MB,
  about 61 MB zipped, of which about 43 MB are WPF files that a WinForms-only helper does not take; x64 `Microsoft.AspNetCore.App`
  about 29 MB, about 13 MB zipped. Today's x86 `OpenPmsDental\bin\Release` is about 118 MB, about 33 MB zipped.
- Both installed SDK bands (10.0.403 machine-wide, 10.0.114 per user) bundle runtime 10.0.14 (`BundledNETCoreAppPackageVersion` in
  `Microsoft.NETCoreSdk.BundledVersions.props`). Neither has a runtime pack in its `packs` folder (only apphost and reference packs), so
  the runtime packs come from NuGet (batch D). The per-user band has no win-x86 apphost pack.
- `Direct2dWrapper.dll` (x86 Release build) imports `VCRUNTIME140.dll`; no file of the x64 10.0.14 `Microsoft.NETCore.App` imports
  it, and `Microsoft.WindowsDesktop.App` ships its own `vcruntime140_cor3.dll`. So the setup keeps the Visual C++ runtime only for
  `Direct2dWrapper.dll`.
- SDK 10.0.403: `ResolveRuntimePackAssets` takes `SatelliteResourceLanguages`
  (`Microsoft.NET.Sdk.FrameworkReferenceResolution.targets:544-552`); a VSTest test project skips the check that a referenced exe
  matches its self-contained setting (`Microsoft.NET.Sdk.targets:1313-1322`); a non-self-contained exe that references a
  self-contained exe fails with NETSDK1151.
- The release channel stores a release as base64 rows of about 1 MB (`ForkReleases.SEGMENT_BYTES`, 786,432 bytes per row);
  `DocumentMiscs.ReplaceForkRelease` deletes the old manifest and segments and writes the new segments, then the manifest last.
  UpdateFileCopier copies the files and writes the release manifest last, with no rollback; it and `ForkReleases` publish and copy only
  top-level files and the special folders (`ListSpecialDirs`, not recursive).

### 9.2 Checked and clear (no action needed; recorded so nobody re-checks)

| Topic | Result |
|---|---|
| COM registration | No `RegisterForComInterop=true`, `ComRegisterFunction` or `EnableComHosting` (only `false` in the Rdl* project files). COM clients only (late-bound Word, DirectShow `[ComImport]`), covered by the UI and API work. |
| Mixed-mode / CLR 1.1 assemblies | The 41 managed `Required dlls` are IL-only (CLI 2.5); `Interop.QBFC10.dll` has a CLI 2.0 header (CLR 1.x; kept by the owner correction, its port to be planned, O-10); 2 are native x86 (`vcruntime140`, `WebView2Loader`). At 3b `WebView2Loader.dll` comes as x64 from the package (S3b-4); `vcruntime140.dll` is deleted in S2-4. |
| LARGEADDRESSAWARE | Today's x86 `OpenPmsDental.exe` is not large-address-aware (2 GB limit); the x86 apphost (the 8.0 and 9.0 packs, checked at the survey) and x86 `dotnet.exe` are not either. Since decision 19 the .NET 10 build is x64, with the 64-bit address space, so the question does not arise (owner, 2026-11-07: no interim large-address-aware flag). |
| T4, licenses.licx, .testsettings/.runsettings, culture-specific .resx, embedded fonts (.ttf/.otf) | None in the tree. |
| XSLT script blocks | `RdlEngine/Runtime/XmlUtil.cs:106` uses `XslCompiledTransform` with default settings (scripts already off on net48); the `.xsl` files are only in DocumentationBuilder (kept), which loads them with default settings and has no script blocks. |
| Delegate `BeginInvoke`/`EndInvoke` (PlatformNotSupported on .NET) | Only `Eclaims/ClaimConnect.cs:1246`, inside the commented `MyMath` block that S0-1 deletes. |
| `Marshal.GetActiveObject`, `WindowsIdentity.Impersonate`, `MutexSecurity`/`PipeSecurity`, System.Messaging/Remoting/EnterpriseServices in code, `ApplicationDeployment` | None. |
| `DllImportSearchPath`, `NativeLibrary`, `SetDllDirectory` (.NET 10 interop changes) | None; LoadLibrary by full path only (TWAIN, msftedit). |
| Windows services and scheduled tasks | One service (`OpenPmsDentalHL7/ServiceHL7.cs`), handled in S3a-7; no schtasks/TaskScheduler. |
| InternalsVisibleTo and strong names | IVT names test assemblies only; the PDFsharp wrappers are signed (`third-party/build/PDFsharp.props:28-29`), harmless on .NET; ODR's `AssemblyKeyFile("")` is empty. |
| WinForms .NET 10 change list (StatusStrip renderer, WPF empty grid definitions, DynamicResource, MenuItem ambiguity) | 0 StatusStrip, 0 empty definitions, 3 valid DynamicResource. |
| CodeDom in shipped code | Only RdlEngine `Code.cs` (decision 14: Roslyn in S3b-2) and a commented block in `FormReportComplex.cs:1149-1185`. Outside shipped code: RdlDesign's `CodeCtl.cs` (S3b-2) and `tests/BINBTests/TopazWrapperTests.cs` (back with the revert of S0-2a; O-10). |
| `Application.ExecutablePath` | Returns the apphost `.exe` on .NET (`StartupPath` is `AppContext.BaseDirectory`, https://source.dot.net/System.Windows.Forms/System/Windows/Forms/Application.cs.html); shipped uses of `Assembly.GetEntryAssembly().Location` take the directory only. No change needed; the test rewrites for `.exe` loading are in S3a-8. |
| Config file name on .NET | `OpenPmsDental.dll.config` (built from the entry assembly's module name, https://source.dot.net/System.Configuration.ConfigurationManager/System/Configuration/ClientConfigPaths.cs.html); UpdateFileCopier, run from its temp folder, reads `UpdateFileCopier.dll.config` there. |
| resx resources serialized with BinaryFormatter | Load on .NET 9+ without BinaryFormatter (https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/winforms-applications); the 16 orphaned entries are clean-up, not a blocker. |

---

## 10. The update path for an office (from the .NET Framework build to the .NET 10 build)

1. **Bridge release first (end of 3a).** The last .NET Framework release, x86, published through the normal self-update and installed on
   every workstation. It carries all 3a changes, the one-time PasswordVault to Credential Manager migration (S3a-5), the 32-bit helper
   program for QuickBooks Desktop, Schick CDR, Topaz and Guru (S3a-13, S3a-14), and the two release-channel guards (S3a-10): an
   unreadable manifest is refused instead of treated as "nothing published", and a release that names another runtime or another
   architecture than this install (the .NET 10 release carries its own x64 runtime, decisions 19 and 20) is refused with "run the new
   setup".
   Its setup installs for all users only (S3a-11). Confirm every workstation, and above all the update server, runs it (Help > About on
   each) before going on; a pre-bridge update server would read the .NET 10 manifest as unreadable and offer to publish its old release
   over it.
2. **Optional check of office data.** Nothing in the code writes BinaryFormatter data, but a database upgraded from old Open Dental could
   hold some. The owner (agents never touch office data) may run a read-only scan of text and blob columns for the BinaryFormatter
   header `AAEAAAD/////` and `PublicKeyToken=`; expected result zero rows.
3. **Switch by setup on every workstation** (decision 5), as for the rename. The office's workstations run Windows 11, the baseline
   (decision 8). **TWAIN devices first** (decision 19): the .NET 10 program is 64-bit, so it uses the 64-bit TWAINDSM and 64-bit TWAIN
   data sources. Before a workstation switches, list its TWAIN devices: the clinic's sensor needs the TWAIN driver project's 64-bit data
   sources (version 1.2, tested on that sensor before the office release, 3b entry conditions); a document scanner or a second device
   needs its vendor's 64-bit driver. A workstation with a device that has only a 32-bit driver stays on the bridge release until a 64-bit
   driver or the optional 32-bit TWAIN bridge helper (`docs/roadmap.md`) exists. **The setup.** The .NET 10 setup is 64-bit: it installs
   into `Program Files\Open PMS - Dental` instead of `Program Files (x86)\Open PMS - Dental`. It installs no .NET runtime, because the
   program and the helper carry their own (decision 20). It installs the x64 Visual C++ runtime and the 64-bit TWAINDSM when they are
   missing (administrator rights; the self-update elevates for the file copy but never installs prerequisites), and then installs the
   program. **Moving the install** (S3b-7): on a workstation with the bridge release, the setup copies `FreeDentalConfig.xml` and the flag
   files (`NoDpi.txt`, `NoCustomBorders.txt`) into the new folder, removes the old install with its own uninstaller (old files, Start menu
   and desktop shortcuts, uninstall entry), deletes what is left of the program's own files in the old folder, and leaves that folder
   only if other files remain (named on the setup's last page). The new shortcuts point to the new folder; a shortcut a user pinned to
   the taskbar points to the old path and must be pinned again. A Windows service registered from the old folder (the HL7 service) is
   reported and registered again from ServiceManager. **The update channel.** Start with the update server; its first start publishes
   the .NET 10 release in its two parts, the runtime part and the program part, and its manifest names the x64 runtime (S3b-7). **Size
   for an office** (estimate from 10.0.14 on the build PC, section 9.1): each workstation's program folder grows from about 120 MB to
   about 370-420 MB (the program's own files, about 150 MB of x64 runtime, about 100-145 MB of x86 runtime for the helper); the database
   holds the program part (about 45 MB of rows) and the runtime part (about 145-170 MB of rows, replaced only when the runtime changes),
   and its backups grow by the same; a normal update pulls about 35 MB to each workstation; the first install (the setup) and each
   runtime change carry the full runtime, about 110-130 MB more zipped. **A mixed set of workstations** during the switch works on the same
   database (stage 3b changes no schema): a workstation not yet set up stays on the bridge release, which refuses to pull the x64 release
   and says to run the setup; a switched workstation never pulls an x86 release. Each workstation switches when its turn comes, in any
   order after the update server.
4. **Middle Tier.** Not needed for this office today: it runs no Middle Tier server (open point O-7, answered 2026-11-08). An office
   that later runs one installs the server per S3b-6. Client and server switch together: the Middle Tier has never shipped to an
   office, and the version check refuses mixed versions. The host runs a Windows Server version on Microsoft's .NET 10 list. The server
   is self-contained (decision 20): it carries the .NET 10 runtime of its release, so nothing is installed for .NET. Install it as a
   Windows service at the same URL path, with a certificate; the IIS option (a 64-bit application pool) also needs the ASP.NET Core
   Module, from the .NET 10 Hosting Bundle installed without its runtimes. Its runtime is updated with each release, like the program's.
5. **After the switch:** runtime patches no longer arrive through Microsoft Update for this program: the runtime ships inside it
   (decision 20). Each month's .NET 10 patch comes as a release of this program (the monthly runtime release, section 2.4), whose new
   runtime part every workstation pulls once; the other releases carry the program part only. Later releases use the normal
   self-update, with UpdateFileCopier started from a temp folder with its runtime.
6. **Rollback:** on each workstation, copy `FreeDentalConfig.xml` out of the 64-bit program folder, uninstall the .NET 10 program, run
   the bridge release's setup (it installs into `Program Files (x86)` again) and put the file back, or enter the connection settings
   again. No database change is involved because stage 3b changes no schema.

**Release-notes items collected by this plan.**
- Stage 0 release: removed the "Use old Imaging module interface, pre 2020" option with the old Imaging module. The window behind Scan
  EOB / View EOB (insurance payments, preauthorizations) and Scan on EHR amendments is rebuilt on the current Imaging module: it looks
  like the Images module and keeps the same commands and the same stored files. Received e-mail is read with MimeKit; signed sending now
  also works over implicit SSL (port 465). Direct secure messaging stays (owner correction, 2026-11-08); how it shows received
  encrypted mail comes with the Direct package. The integrations S0-2a had removed are back after the revert.
- Bridge release: setup for all users only (administrator rights); passwords saved by the program move to the Windows Credential Manager
  once, without re-entry. DentalXChange, NewCrop and Change Healthcare ITS calls use a new SOAP caller. ITS was checked against the
  requests of the old code and a fake endpoint in the tests, not against the live service. QuickBooks Desktop, Schick CDR, Topaz and
  Guru run through a small 32-bit helper program; a Topaz signature is signed in the helper's own small window.
- .NET 10 release: the program is 64-bit (decision 19); it moves from `Program Files (x86)` to `Program Files`, and a shortcut pinned to
  the taskbar must be pinned again. It carries its own .NET 10 runtime, so nothing is installed for .NET, its program folder is larger
  (about 400 MB), and .NET security fixes come with this program's monthly releases (decision 20). It needs 64-bit TWAIN drivers (the
  setup installs the 64-bit TWAIN data source manager); Windows 11 is the baseline; on older Windows the setup
  warns. With Windows "Make text bigger" above 100%, 72 older windows show slightly larger text; plugins built for .NET Framework no
  longer load and must be rebuilt for .NET 10 (the office uses none); RDL reports with VB `<Code>` blocks keep working (compiled with
  Roslyn);
  Change Healthcare ITS claims work on .NET 10 (the bridge release's SOAP caller; still not tested against the live service).

---

## 11. Owner decisions (2026-11-08)

The owner answered the plan's questions "as recommended", with the corrections below. Each decision is applied in the stage text above.

- **Standing rule (owner, 2026-11-08).** "Overall decisions wrt .net 10: don't cut anything including test. Cut test only if it's
  testing something that we deliberately deleted." The owner confirmed the reading ("Yes"): what the owner deliberately removed stays
  removed, and everything else is kept and moved to .NET 10. The owner corrected the list of deliberate removals the same day: "Don't
  make the assumption that if the office doesn't use, we'd cut it. 1-8 we want to keep it and have them ported. DentX, Dexis
  Integrator and Vipersoft links, and NDde.dll we should keep if we can."
  - **Kept; port to be planned (owner, 2026-11-08).** The eight items: (1) QuickBooks Desktop (QBFC; QuickBooks Online stays too), (2)
    Schick CDR, (3) Topaz signature pads, (4) CallFire SMS texting, (5) Divvy postcards, (6) the BCBS Georgia dial-up terminal, (7)
    RdlEngine's SQL Server, ODBC and OLE DB data providers, (8) Direct secure messaging (HISP). Also, if feasible, the DentX, Dexis
    Integrator and Vipersoft program links and `NDde.dll`, removed earlier (REL-FILES, 2026-11-07). S0-2a is withdrawn and its merge
    reverted; Direct comes back on top of S0-3's MimeKit e-mail code in a new package of the lead. The lead plans each port in stages
    3a or 3b (open point O-10; stage 5 is folded in, decision 19). That the office does not use QuickBooks Desktop, Schick, Topaz or
    Direct stays recorded as a fact; it no longer justifies a removal.
  - **Deliberately removed (stays removed; their tests go).** The old 2020 Imaging module (`ControlImagesOld` and its "Use old Imaging
    module interface, pre 2020" option), after its EOB and amendment window was rebuilt (S0-6); the earlier fork removals tied to Open
    Dental HQ services and eServices, and the closed-source parts with no published interface (Sparks3D, Suni, the "not available"
    bridges); dead code and unused resources (S0-1, and the unused ADODB, CDO and System.Web references in S0-2b).
  - **Kept by the rule, changing this plan.** The legacy UnitTests project becomes a running suite (S0-5, decision 17); RdlDesign,
    DocumentationBuilder and CentralManager are moved to .NET 10 (decision 17); RDL reports with VB `<Code>` blocks keep working
    (decision 14); both XmlSerializers files keep shipping (the revert of S0-2a; 3b builds them with `Microsoft.XmlSerializer.Generator`).

1. **Toolchain.** Visual Studio Build Tools 2026 18.11.3 are installed on the build PC (2026-11-08) beside Build Tools 2022 17.14.44,
   with the machine-wide .NET SDK 10.0.403 and the x64 and x86 .NET 10.0.14 runtimes (3b builds and tests x64 only, decision 19); the
   .NET SDK 10.0.114 is installed per user. The
   build lane and the test runners move to the Build Tools 2026 MSBuild and vstest in S1-0, before the first SDK-style project, not at 3b.
   Build Tools 2022 stay installed until the stage 1 gate has passed on Build Tools 2026 (Visual Studio 2022 17.14 leaves mainstream
   support on 2027-01-13). S1-0 pins the SDK in `global.json`, choosing between the per-user 10.0.1xx band and the machine-wide band that
   Visual Studio installs, and records why (section 2.4, open point O-5). Build Tools 2026 are used under the Visual Studio Community
   licence, which covers this GPL project, and are kept current (Modern Lifecycle). The owner added the MSVC v143 component to the Build
   Tools 2026 instance on 2026-11-08 and verified it (O-4 resolved): `Direct2dWrapper.vcxproj` keeps v143, there is no v145 retarget, and
   the setup's VC++ check does not change.
2. **Stage order.** 0, 1, 2, MSTest (stage 4, before the switch), 3a ending in the bridge release, 3b straight to x64 (stage 5 folded
   into 3a and 3b, decision 19).
3. **MSTest 4** (the current 4.5.x line, which supports net462) instead of MSTest 3, a deprecated line. Exact patch chosen at download.
4. **Globalization.** NLS (`System.Globalization.UseNls=true`) for the first .NET 10 release; ICU later as its own release after the
   CA1307/CA1309/CA1310 clean-up.
5. **Office switch.** The new setup runs on every workstation, after the .NET Framework bridge release with its release-channel guards.
6. **Runtime in the setup: none.** Changed by decision 20 on 2026-11-08; was: the setup downloads the x64 .NET 10 Desktop Runtime
   (decision 19; it was x86) at install time with a signature check, as it does for vc_redist. The program and the helper carry their
   own runtime, so the setup installs their files only; it still installs the x64 Visual C++ runtime when missing, and `/PREREQS=skip`
   stays for managed installs, for that runtime only.
7. **No per-user setup mode** (S3a-11). Confirmed after the standing rule (owner, 2026-11-08): "Don't need 'Install for me only'". How
   the installer test then runs is open point O-3.
8. **Windows versions.** Windows 11 is the baseline; the office's workstations run Windows 11. The setup warns (does not refuse) below
   it. Microsoft's .NET 10 list (https://learn.microsoft.com/en-us/dotnet/core/install/windows) supports Windows 10 only in some editions.
   A Middle Tier host runs a Windows Server version on that list.
9. **Larger text accepted.** With Windows "Make text bigger" above 100%, the 72 older windows show slightly larger text on .NET 10;
   noted for the release notes.
10. **Charts.** The community port `WinForms.DataVisualization` 1.10.2 (MIT), checked by screenshots.
11. **Kept; port to be planned (owner, 2026-11-08).** CallFire SMS, Divvy postcards, the BCBS Georgia dial-up terminal, RdlEngine's SQL
    Server, ODBC and OLE DB data providers, and with them QuickBooks Desktop, Schick CDR and Topaz. Changed by the owner correction of
    2026-11-08; was: cut (S0-2a), and QBFC, Schick and Topaz removed because the office does not use them. The revert of S0-2a's merge
    brings them back (open point O-10). QuickBooks Desktop, Schick CDR and Topaz, with Guru, stay 32-bit in the helper program or get a
    64-bit replacement (S3a-13, S3a-14, O-14; decision 19).
12. **Kept: Change Healthcare (Emdeon) ITS medical claims.** The office does not use it, but it must keep working on .NET 10: its
    generated proxy is replaced by the hand-written SOAP caller, with tests of the requests it builds (S3a-6). **Kept too: the
    Practice-Web DentalXChange eligibility path** (owner, 2026-11-08: "don't cut"; open point O-6). The DCI rpc/encoded proxy behind
    `CustomizedForPracticeWeb` is kept and made to work on .NET 10: S3a-6 replaces it with the same hand-written caller, with a test of
    the request it builds and of parsing a recorded response. Stage 0 does not touch it.
13. **Kept: voice perio charting** (`System.Speech`, batch D).
14. **RDL reports with VB `<Code>` blocks keep working on .NET 10.** Changed by the standing rule on 2026-11-08; was: they fail with a
    clear message. S3b-2 moves the runtime compile from CodeDom to Roslyn (`Microsoft.CodeAnalysis.VisualBasic`, batch D, version chosen
    at download), with a test that compiles and runs a report's `<Code>` function against an output recorded on net48 (S3a-4).
15. **Direct secure messaging kept; port to be planned (owner, 2026-11-08).** Changed by the owner correction of 2026-11-08; was:
    removed in stage 0 (S0-3 step 3, the references and Health.Direct DLLs in S0-2b) because the office does not use it. Received-mail
    parsing stays on MimeKit (S0-3); Direct comes back on top of it in a new package of the lead (open point O-10).
16. **Plugins.** Keep the plugin loader, rebuild PluginExample for .NET 10 (S3b-4); .NET Framework plugin DLLs become unsupported
    (release notes). The office uses no plugins.
17. **Unshipped projects kept; the old 2020 Imaging module removed.** The legacy UnitTests project, RdlDesign, DocumentationBuilder and
    CentralManager are kept and moved to .NET 10 with the other projects. Changed by the standing rule on 2026-11-08; was: UnitTests,
    RdlDesign and DocumentationBuilder deleted and CentralManager retired (S0-5). UnitTests becomes a running suite in stage 0 (S0-5),
    without the four classes S0-T moved into `tests/`; its `UI Testing` developer windows stay (open point O-9). RdlDesign and
    DocumentationBuilder join the solution in stage 1 (S1-1, S1-5); none of the four ships. The old 2020 Imaging module is retired
    completely after its EOB and amendment window is rebuilt on the new Imaging code (S0-6; open point O-2).
18. **Middle Tier.** The office runs no Middle Tier server (owner, 2026-11-08: "no, but port middle tier to .net 10"; open point O-7).
    The Middle Tier is still moved to .NET 10 (S3b-6). The 3b gate checks it on the build PC; batch C and section 10 step 4 are only for
    an office that later runs a Middle Tier server, which is self-contained too (decision 20).
19. **Directly to 64-bit (owner, 2026-11-08).** The lead offered "32-bit .NET 10 as an internal checkpoint, then flip to 64-bit, one
    office release"; the owner answered "no go directly to 64." So there is no 32-bit .NET 10 build of the program: stage 3b retargets
    everything straight to `net10.0-windows` x64 (`PlatformTarget` x64, `RuntimeIdentifier` win-x64; `SelfContained` false, changed to
    true by decision 20), and stage 5 as a separate later stage goes away. The office gets one rollout: the .NET Framework 4.8 bridge
    release (x86, end of 3a), then the .NET 10 x64 setup once on every workstation (section 10). The 64-bit work that can be done and
    tested on .NET Framework moves into 3a (S3a-12 to S3a-16), and the 64-bit prerequisites become entry conditions of 3b. Changed with
    it: the ground rule "x86 until stage 5", the stage order (decision 2) and the runtime the setup downloads (decision 6; since decision
    20, none). The 32-bit-only vendor components stay 32-bit in a helper program unless a 64-bit replacement is chosen (O-14); the
    helper's runtime after the switch was open point O-15, answered by decision 20; the TWAINDSM download is asked when 3b starts (O-16).
20. **Self-contained: the program and the 32-bit helper (owner, 2026-11-08).** Asked open point O-15 (the helper's runtime after the
    switch), the owner chose "(c) self-contained" and added "self-contained for 64bit as well". So the helper (`OpenPmsDentalHelper32`,
    S3a-13, S3a-14) moves to `net10.0-windows` x86, published self-contained for win-x86 with its own x86 .NET 10 runtime in its own sub
    folder of the program folder (`Helper32\`), and no x86 runtime is installed on the workstations. Each vendor piece it loads (QBFC,
    Schick CDR, Topaz `SigPlusNET`, Guru `MedVisorInterface`) must be proven to load on .NET 10 in the S3b-0 spike; one that does not is
    reported to the owner, with no silent fallback to .NET Framework. The 64-bit program and every other shipped .NET 10 exe (the
    database settings tool, ServiceManager, UpdateFileCopier, the HL7 service, and the Middle Tier server for an office that runs one)
    are published self-contained for win-x64; the exes in the program folder share one x64 runtime there, of one version, and the Middle
    Tier server carries the same version in its own folder on its host. This replaces the owner decision of 2026-11-07 "Deployment:
    framework-dependent; the installer installs the .NET 10 Desktop Runtime" (the ground rules, decision 6). Consequences: the setup
    downloads and installs no .NET runtime (the Visual C++ runtime stays, for `Direct2dWrapper.dll`); the "install for me only" mode stays
    dropped (decision 7); runtime security fixes no longer come from Microsoft Update for this program, only with its releases, so a
    runtime release follows each monthly .NET 10 patch (section 2.4); a release is published in two parts, the runtime part only when the
    runtime changes, so normal updates stay small (S3b-7); the runtime packs come from the offline folder (batch D); a Middle Tier host
    needs no ASP.NET Core runtime (batch C, section 10 step 4); the first install and each runtime change carry the full runtime, and a
    workstation's program folder grows to about 400 MB (section 10 step 3).

Also recorded for the update path: the Middle Tier client and server switch together (it has never shipped to an office); saved
passwords get a one-time migration from PasswordVault to the Credential Manager (S3a-5). Also answered on 2026-11-08: signed e-mail
sending is kept and moves to MimeKit (open point O-1, S0-3 step 4), the old module's EOB and amendment window is rebuilt (open
point O-2, S0-6, decision 17), the Practice-Web DCI eligibility path is kept (open point O-6, S3a-6, decision 12), and the office runs
no Middle Tier server while the Middle Tier is still ported (open point O-7, S3b-6, decision 18), and the helper and the program are
self-contained (open point O-15, decision 20).

---

## 12. Open points

- **O-1. Signed e-mail sending. Answered 2026-11-08: kept (S0-3 step 4).** An S/MIME signature on ordinary e-mail. It is not Direct
  messaging, but it is built on the Direct library and uses the `NHINDPrivate` certificate store that Direct created. It moves to MimeKit
  in S0-3 step 4 and then also works over implicit SSL. Decrypting received S/MIME-encrypted mail is not planned (MimeKit could, with the
  same certificate, if ever wanted).
- **O-2. ControlImagesOld after the module choice is gone. Answered 2026-11-08: rebuilt (S0-6).** The window it draws for claim-payment
  EOBs, preauthorization EOBs and EHR amendments (`FormImages`) is rebuilt on the current Imaging code in stage 0, with the same stored
  files. The old module, its option and its code are then removed, so stage 3a has none of it to convert.
- **O-3. How the installer test runs without per-user mode.** (a) The lead runs `tools/Test-Installer.ps1` from an elevated PowerShell at
  each Release dry run on the build PC: a UAC prompt each time, and machine-wide side effects limited to a `%TEMP%` folder, an uninstall
  entry and the Event Log source key, which the script removes or checks. (b) A disposable Windows VM or Windows Sandbox where the script
  runs elevated; Windows Sandbox is an optional Windows feature, and turning it on changes the build PC's system settings, which only the
  owner may do. Recommendation: (a) for the gates from 3a on; (b) later if wanted. Needed before S3a-11 merges.
- **O-4. Resolved on 2026-11-08: MSVC v143 in the Build Tools 2026 instance.** The owner added
  `Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64`; a Win32 v143 DLL built with MSBuild 18.11 links with 14.44.
  `Direct2dWrapper.vcxproj` stays on v143 (supported to 2032). No retarget to v145, no change to the setup's VC++ check.
- **O-5. The SDK band in `global.json`. Decided in S1-0 (2026-11-08): (a), 10.0.114 per user with `rollForward` `disable`; the
  evidence and the reasons are in section 2.4.** (Decided by the lead in S1-0 from the offline check, recorded in section 2.4 and the
  commit; the owner is told the result.) (a) The per-user 10.0.1xx band with `disable`: it changes only through a bump and is supported to November
  2028, but every build entry point needs the resolver variables; its win-x64 apphost pack, the one 3b needs (decision 19), is local,
  and the win-x86 one of the self-contained helper (O-15) comes from batch D.
  (b) The machine-wide band Visual Studio installs, with `latestFeature` or `disable` plus a bump after each Visual Studio update: no
  per-user set-up and both apphost packs are local, but Visual Studio updates move the SDK about every quarter. A band whose check shows a
  `Microsoft.Net.Sdk.Compilers.Toolset` download is not chosen. Neither band needs an SDK download now.
- **O-6. The Practice-Web DentalXChange eligibility path. Answered 2026-11-08: kept, ported in S3a-6 (owner: "don't cut").** The DCI
  rpc/encoded proxy and its region `OpenPmsDental/Forms/FormInsPlan.cs:2011-2420` are reached only through the preference
  `CustomizedForPracticeWeb` (`FormInsPlan.cs:1876-1880`), which no window sets (default 0, off). Without it, the eligibility button uses
  the default eligibility clearinghouse. The path is kept and made to work on .NET 10: S3a-6 ports it with the other SOAP clients, with
  a test of the rpc/encoded request it builds and of parsing a recorded response (decision 12). Nothing changes in stage 0 (S0-2a left
  it untouched and has merged), and `docs/specs/vendor-direct/dentalxchange.md` stays as it is.
- **O-7. Does the office run a Middle Tier server? Answered 2026-11-08: no Middle Tier server in the office; the Middle Tier is still
  ported (S3b-6).** (B-SERVER 5 in `docs/owner-questions.md`; owner: "no, but port middle tier to .net 10".) The 3b gate still checks the
  new Middle Tier for real on the build PC: a Windows service on a loopback address with a test certificate, with the host round-trip
  tests and a client session run against it. The server is self-contained (decision 20), so the check needs no runtime install, batch C
  is only for the IIS option of a future office server, and section 10 step 4 is not needed for this office today (decision 18).
- **O-8. Database TLS test environment. Answered 2026-11-08: local testing with encryption, on a temporary second server (option (a)).**
  The owner: "database encryption: do local testing with encryption", then "A". The TLS test of S2-2 (again at the 3b gate) runs
  entirely on the build PC:
  - the test run starts a temporary MariaDB server from the installed MariaDB 10.11 binaries, on `127.0.0.1:3307` with its own data
    folder under the test's temp folder and TLS from a test CA, and stops and deletes it afterwards; the server on `127.0.0.1:3306` is
    not touched (the one exception to the rule that only `127.0.0.1:3306` is used, for this test only);
  - the test creates the CA, a server certificate for `127.0.0.1` and `localhost`, and a CAPI and a CNG client PFX itself, imports the
    client certificates into the current user's store (no administrator rights; `DbConnectionSecurity` gets a test-only store
    location) and removes them afterwards;
  - a test-only switch makes the loopback connection follow the network rule (TLS required, `VerifyFull`, the client certificate),
    since the program exempts loopback connections from it;
  - a `REQUIRE X509` account on the temporary server takes the certificate logins.
  The stage 2 and 3b gates need this test to pass (it is skipped only when the MariaDB binaries are missing, and the gate then fails).
- **O-9. The developer windows in the legacy UnitTests project. Answered 2026-11-08 by the standing rule: kept.** Its `UI Testing`
  folder holds 23 developer windows. One of them, `FormWpfConverter`, is the only front end of the `WpfConverter` class
  (`OpenPmsDental/UIManagement/WpfConverter.cs`, compiled into the program) and the only launcher of the WPF test windows
  `FrmTestAllControls` and `FrmTestSplitters` (`WpfControlsOD/Frms`). UnitTests stays (S0-5, decision 17), and so do these windows,
  where they are. The three stay reachable, the comments that say "Run UnitTests FormWpfConverter" stay true, and
  `tools/Test-NoHqHosts.ps1:62-63` and `tests/LibWebTests/IeBrowserRemovedTests.cs:55` need no change. The options weighed, a new
  developer tool under `tools/` or deleting the three, are not needed. The windows move with UnitTests (S1-6, S3a-M3, S3b-5).
- **O-10. The kept integrations: their port is to be planned (owner, 2026-11-08).** The owner correction keeps these and has them
  ported; the lead plans each port in stage 3a or 3b (stage 5 is folded in, decision 19) and adds its packages to this plan:
  - QuickBooks Desktop (QBFC): `Interop.QBFC10.dll` is a CLR 1.x interop DLL (section 9.2), and its COM piece is 32-bit: the helper
    program or a 64-bit QBFC (S3a-13, O-14).
  - Schick CDR: COM interop (`Interop.CDRDATALib.dll`, `Interop.CDRDicom.dll`), 32-bit: the helper program, or a direct call if CDR
    runs as its own process and is registered for 64-bit callers (S3a-14, O-14).
  - Topaz signature pads: `SigPlusNET.dll` is not redistributed (no published terms); `tests/BINBTests/TopazWrapperTests.cs` compiles
    code with CodeDom, which throws on .NET (S3a-14 rewrites it without CodeDom); 32-bit: the helper program or a 64-bit
    `SigPlusNET.dll` (S3a-14, O-14).
  - CallFire SMS texting: a SOAP client (`System.Web.Services`), which .NET 10 does not have; its `WebReferenceUrl` keeps
    `OpenPmsDental.XmlSerializers.dll` built until then (critique W1).
  - Divvy postcards: a WCF client (`System.ServiceModel`), which needs the WCF client packages.
  - The BCBS Georgia dial-up terminal: `System.IO.Ports` (a package on .NET 10).
  - RdlEngine's SQL Server, ODBC and OLE DB data providers: `Microsoft.Data.SqlClient`, `System.Data.Odbc` and `System.Data.OleDb`
    (packages); RdlDesign's data source dialogs use the same providers.
  - Direct secure messaging (HISP): the Health.Direct DLLs are CLR 2.0 binaries; Direct comes back on top of S0-3's MimeKit code in a
    new package of the lead, after the revert of S0-2a.
  - If feasible: the DentX, Dexis Integrator and Vipersoft program links and `NDde.dll`, removed earlier (REL-FILES, 2026-11-07,
    `docs/port-notes/REL-FILES.md`). Their feasibility includes NDde's licence question (REL-FILES 2 in `docs/owner-questions.md`).

- **O-11. A runner for the scheduled jobs and the HIE export. Answered 2026-11-08: build it, on one workstation only.** Setup >
  Scheduled Processes (recall sync, batch insurance verification, statements) and the HIE export (`HieQueues.ProcessQueues`) are set
  up in the program, but only Open Dental's closed background service ran them, so nothing runs them today. The owner: "Yes build it in
  a way that these workflow only run on a single workstation in the practice, so no race conditions." A package after stage 0 adds a
  runner inside the program: setup names the one workstation that runs these jobs; only that workstation's program runs them, and a
  database lock (one row or `GET_LOCK`) makes a second runner wait instead of running a job twice, also if two computers are set up by
  mistake. The owner also asked for "option for background test to be turned off": a setting turns the runner off for the whole
  practice (no workstation runs these jobs), and each job can be switched off on its own. The runner is off by default, also after an
  upgrade: a database adopted from Open Dental may hold scheduled processes that Open Dental's service ran, so the office turns the
  runner on itself after looking at them. The 55 legacy tests of these jobs stay (S0-5) and become the runner's tests.
- **O-12. The local test database runs encrypted. Answered 2026-11-08: yes.** The owner: "Once db switches you can get rid of the old
  encrypted db" (the current test database on `127.0.0.1:3306`), and "yes" to the plan. A package before the stage 2 gate: the owner
  runs a script once as administrator that turns on TLS (a test CA and a server certificate for `127.0.0.1` and `localhost`) in the
  local MariaDB service and restarts it; the program's development set-up, the test helpers and the scripts then connect encrypted;
  the TLS and certificate-login test of S2-2 runs against this server, so the temporary server of O-8 is no longer needed; the server
  then refuses unencrypted connections (`require_secure_transport`; other sessions are told first); and the old test database
  (`opendental_dev`) is dropped and rebuilt with `tools/New-DevDatabase.ps1` over the encrypted connection (confirmed with the owner
  right before the drop).
- **O-13. Direct certificates without Open Dental. Answered 2026-11-08: a local certificate import. Done by the DIRECT-CERT package
  (`docs/port-notes/DIRECT-CERT.md`, branch `dev/directcert-2`).** The e-mail address window's
  Register certificate button opens `FormEmailCertRegister`, which registered the address's Direct certificate with Open Dental's own
  servers and fails since they were removed (S0-3D restored it as it was). The owner: "Yes, load Direct provider's certificate from a
  file". A package after stage 0 replaces it: the button imports the certificate the office's Direct provider (HISP) issued, from a
  `.pfx` or `.p12` file with its password, into the Direct private store (`NHINDPrivate`, the key kept non-exportable), and can also
  import the provider's trust anchor into `NHINDAnchors`. It checks that the certificate names the address (or its domain) and has a
  private key, shows the subject and dates, and replaces an older certificate for the same address only after asking. Tests use
  generated certificates in current-user test stores, as S0-3D's do. As built, "older" means of the same purpose (signing or
  encryption, from the Key Usage and Extended Key Usage) and expiring before the file's certificate, so a provider's separate signing and
  encryption certificates for one address both stay; the button is labelled Certificate. The program's own search then signs with the
  signing one and encrypts and decrypts with the encryption one (the lead's decision of 2026-11-09, third fix round). What the .NET 10
  switch needs of this code (`X509CertificateLoader` for SYSLIB0057, `FileSystemAclExtensions`, the CAPI key check and the serialized
  copy of `Undo`, `CngKey` disposal to verify) is in the port note. Left to the owner:
  `ICustomerUpdates` and `CustomerUpdatesProxy`, and the read access of `BUILTIN\Users` to the imported private keys. **Answered by the
  owner on 2026-11-09 (DIRECT-CERT-FOLLOWUP, `docs/port-notes/DIRECT-CERT-FOLLOWUP.md`):** the empty `ICustomerUpdates`,
  `CustomerUpdatesNoHq` and `CustomerUpdatesProxy` are deleted, with the tests that pinned them changed to check they are gone;
  `BUILTIN\Users` keep read access to the Direct private key, as built; a replaced certificate's private key is deleted only if the
  window imported it, as built; and the connection settings tool (`ClientCertificateStore`) got the same CAPI key fix.
- **O-14. 32-bit vendor components: a helper module or a 64-bit replacement, per vendor (decision 19).** QuickBooks Desktop (QBFC),
  Schick CDR, Topaz signature pads and Guru load 32-bit vendor code in the program's process today. Each gets a module in the 32-bit
  helper program (S3a-13, S3a-14), unless the vendor has a 64-bit way: a 64-bit QBFC from Intuit, a Schick CDR server that runs as its
  own process and is registered for 64-bit callers, a 64-bit `SigPlusNET.dll` from Topaz, a 64-bit `MedVisorInterface.dll` from Guru.
  S3a-13 and S3a-14 first check each vendor's documentation (no download) and report what they find; the owner then chooses per vendor
  where a choice exists. A 64-bit replacement may need newer vendor software on the workstations and a download of that vendor's files,
  which the owner approves separately. Recommendation: the helper module, unless a vendor's 64-bit way needs nothing new on the
  workstations. Until this is answered, each component gets its helper module, so nothing waits. No test PC has any of the four today;
  whether the owner wants a vendor's trial or test set-up for a launch check is part of this question.
- **O-15. The helper's runtime after the switch (decision 19). Answered 2026-11-08: (c) self-contained (decision 20).** The helper is
  32-bit because the vendor code it loads is; the program itself has no 32-bit .NET 10 build. In 3a the helper is built like everything
  else (net48, x86). For 3b the options were: (a) it stays on .NET
  Framework 4.8 x86, which Windows 10 and 11 include and service, so nothing more is installed on the workstations, and the vendor
  pieces it loads were made for .NET Framework (whether they load on .NET 10 is not checked). It would be the one project left on .NET
  Framework, an exception to the ground rule of 2026-11-07 ("no project left on .NET Framework") that only the owner can make. (b) It
  moves to `net10.0-windows` x86, framework-dependent: the setup also installs the x86 .NET 10 Desktop Runtime, and each vendor piece
  must be proven to load on .NET 10. (c) As (b), but self-contained: no second runtime to install, several tens of MB more in each
  release, and runtime fixes only with this program's releases. The recommendation was (a). The owner chose (c) and made the 64-bit
  program self-contained too ("self-contained for 64bit as well"): the helper moves to `net10.0-windows` x86, self-contained in its own
  sub folder `Helper32\` (S3b-4), and each vendor piece must be proven to load on .NET 10 in the S3b-0 spike; one that does not is
  reported to the owner, with no fallback to .NET Framework. The extra size reaches a workstation only with a runtime change, because
  the runtime is published as its own part of a release (S3b-7).
- **O-16. The download of the 64-bit TWAIN data source manager (decision 19).** The .NET 10 program is 64-bit and needs the 64-bit
  `TWAINDSM.dll`, which this program's setup ships (owner, 2026-11-07): TWAIN DSM 2.5.1 from the TWAIN Working Group's GitHub release
  (both DLLs signed by the TWAIN Working Group). It is not a NuGet package and not covered by the approval of the download batches
  (2026-11-08). The lead asks for it when 3b starts, with the file name, the source and the size; nothing is downloaded before. Its
  licence goes into `THIRD-PARTY-NOTICES.txt` (S3b-7).
- **O-17. The ContrTable resource comes out in two forms (found by the S2-1 review, 2026-11-09; not caused by S2-1).**
  `OpenPmsDental.ContrTable.resources`, the resources of `ContrTable` embedded in `OpenPmsDental.exe`, is 5,606 or 7,424 bytes from build
  to build: the resource generator in the build node writes the form's `$this.Language` CultureInfo in one of two serialized forms. Both
  occur on `main` and on `net10/s21-1`, in Debug and in Release builds (`docs/port-notes/S2-1.md`, section 4), so the Release exe's size
  and SHA-256 change between builds of the same source even apart from the build stamp. It gets a follow-up of its own (find what
  decides the form and make it one; then the exe differs between builds by the build stamp only). Until then the gates' hash
  comparisons list it with the files that change on every build (section 3).

---

## 13. Sources

- SDK, MSBuild and Visual Studio versions: https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs ; NETSDK1233:
  https://github.com/dotnet/sdk/issues/51678
- Visual Studio lifecycle and servicing: https://learn.microsoft.com/en-us/visualstudio/productinfo/vs-servicing ; Community licence for
  open-source projects: https://visualstudio.microsoft.com/vs/community/
- .NET support policy and dates: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- SDK project overview and properties: https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview ;
  https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props ;
  https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop
- SDK targets: https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.BeforeCommon.targets ;
  https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.RuntimeIdentifierInference.targets ;
  https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.FrameworkReferenceResolution.targets
  https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.GenerateAssemblyInfo.targets ;
  https://source.dot.net/Microsoft.NET.Build.Tasks/ResolveAppHosts.cs.html
- MSBuild common targets (SGen): https://github.com/dotnet/msbuild/blob/main/src/Tasks/Microsoft.Common.CurrentVersion.targets ;
  XmlSerializer generator for .NET: https://learn.microsoft.com/en-us/dotnet/core/additional-tools/xml-serializer-generator
- Directory.Build.props: https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory
- Evaluated items: https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties
- NETSDK1004: https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1004 ; NU1201:
  https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1201
- NuGet PackageReference, lock files, pruning:
  https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files ;
  Central Package Management: https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management ;
  nuget.config: https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file ; audit:
  https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages ;
  RID-specific assets:
  https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks#architecture-specific-folders ;
  System.Management targets: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Management/src/System.Management.csproj
- Environment variables (SDK resolver, DOTNET_ROOT): https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables
- ResolveComReference (MSB4803): https://learn.microsoft.com/en-us/visualstudio/msbuild/resolvecomreference-task ; MSB4278:
  https://learn.microsoft.com/en-us/visualstudio/msbuild/errors/msb4278
- System.Web SDK (community): https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb
- MSTest migration: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-migration-from-v1-to-v3 ;
  https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-migration-v3-v4 ; package:
  https://www.nuget.org/packages/MSTest.TestFramework/
- Technologies unavailable on .NET: https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable ;
  compatibility mode: https://learn.microsoft.com/en-us/dotnet/core/porting/third-party-deps#net-framework-compatibility-mode
- Breaking changes in .NET 10 (incl. MailAddress): https://learn.microsoft.com/en-us/dotnet/core/compatibility/10 ;
  XmlSerializer and obsolete members:
  https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/10/xmlserializer-obsolete-properties ;
  https://source.dot.net/System.Private.Xml/System/Xml/Serialization/XmlAttributes.cs.html ;
  https://source.dot.net/System.Private.Xml/System/Xml/Serialization/XmlReflectionImporter.cs.html
- Thread.Abort: https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0006
- UseShellExecute: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute ; UAC and CreateProcess:
  https://learn.microsoft.com/windows/security/application-security/application-control/user-account-control/architecture
- Encodings: https://learn.microsoft.com/en-us/dotnet/api/system.text.codepagesencodingprovider ;
  https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding.default
- Floating-point formatting:
  https://learn.microsoft.com/en-us/dotnet/core/compatibility/3.0#floating-point-formatting-and-parsing-behavior-changed
- ICU and NLS: https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
- WinForms: WFDEV006 https://learn.microsoft.com/en-us/dotnet/desktop/winforms/wfdev-diagnostics/wfdev006 ; high DPI
  https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms ; SetDefaultFont
  https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setdefaultfont ; WFO1000
  https://learn.microsoft.com/en-us/dotnet/desktop/winforms/compiler-messages/wfo1000 ; OutOfMemory to ExternalException
  https://learn.microsoft.com/en-us/dotnet/core/compatibility/windows-forms/10.0/system-drawing-outofmemory-externalexception ; clipboard
  https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/winforms-wpf-ole-guidance ; resx
  https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/winforms-applications ; Application
  https://source.dot.net/System.Windows.Forms/System/Windows/Forms/Application.cs.html
- Configuration file paths: https://source.dot.net/System.Configuration.ConfigurationManager/System/Configuration/ClientConfigPaths.cs.html
- WinRT on .NET 5+: https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/5.0/built-in-support-for-winrt-removed
- File ACLs: https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions
- Single-file apps: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Self-contained deployment (decision 20): https://learn.microsoft.com/en-us/dotnet/core/deploying/ ; trimming is not supported for
  WinForms and WPF: https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities ; .NET servicing on Patch Tuesday:
  https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core ; Hosting Bundle options:
  https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/hosting-bundle
- Installing and detecting .NET on Windows (incl. supported Windows versions):
  https://learn.microsoft.com/en-us/dotnet/core/install/windows ;
  https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions?pivots=os-windows
- ASP.NET Core hosting: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service ;
  https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/ ; HttpRequestError
  https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequesterror
- Language version: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version
- MySqlConnector advisory and history: https://github.com/advisories/GHSA-473q-m89c-ghf8 ;
  https://mysqlconnector.net/overview/version-history/ ;
  https://www.nuget.org/packages/MySqlConnector/
- WebView2 distribution: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
- 64-bit (decision 19): WOW64 registry redirection https://learn.microsoft.com/en-us/windows/win32/winprog64/registry-redirector ; file
  system redirection (`sysnative`) https://learn.microsoft.com/en-us/windows/win32/winprog64/file-system-redirector ;
  `SetWindowLongPtr` https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw ; Inno Setup 64-bit
  installs https://jrsoftware.org/ishelp/index.php?topic=64bitinstallation ; TWAIN DSM releases https://github.com/twain/twain-dsm/releases
- The survey and critique notes are not part of the repository; every claim this plan relies on is restated here with its file and line
  or its source.

---

## Appendix. Where each finding of the critique is handled

| Finding | In short | Handled in |
|---|---|---|
| W1 | Release builds run SGen for `WebReferenceUrl` items; removing one silently drops an `XmlSerializers.dll` that Build-Release requires | S0-2a (both dropped and retired), then the revert of its merge (both kept, standing rule); S1-3 and S3a-6 (keep them built, proven by the Release dry run); S3b-3, S3b-4 and batch D (`Microsoft.XmlSerializer.Generator`); section 7 |
| W2 | SDK-style .NET Framework exes get automatic binding redirects | S1-0, stage 1 gate (config contents) |
| W3 | The `requireAdministrator` UpdateFileCopier started through CreateProcess fails on .NET; 21 more `new Process()` sites in shipped code | S3a-10 (iv), S3a-2, section 7, launch checks |
| W4 | `SetDefaultFont` applies the text scale factor; the planned test was fragile | S3b-4, section 7, decision 9 |
| W5 | The `Application.ExecutablePath` item was unnecessary | dropped from S3b-4; section 9.2 |
| W6 | A build without a runtime identifier puts Windows assets under `runtimes\win\...` | S3b-1, S3b-0 (7), section 7 |
| W7 | Wording: sgen, the ProgressWin count, "the self-update runs as the user" | S3b-4, section 8.4, section 10 |
| G1 | `XmlSerializer` on .NET 10 throws for `[Obsolete(IsError=true)]` members (`PrefName`) | S0-T, S3a-9 (iv), S3b-0 (8), S3b-6 |
| G2 | SDK 9.0.3xx leaves support on 2026-11-10 | decision 1, section 2.4, S1-0 (Build Tools 2026 lane and the SDK 10 pin), O-5 |
| G3 | `MailAddress` rejects consecutive dots on .NET 10 | S0-3 (received mail), S3a-9 (v), S3b-5 |
| G4 | Database TLS with client certificates never tested over a non-loopback address | S2-2, stage 2 and 3b gates, O-8 (test environment) |
| G5 | The setup's per-user mode cannot install the runtime | decision 7, S3a-11, O-3; since decision 20 the setup installs no runtime, and the mode stays dropped (decision 7) |
| G6 | The CDO path also set headers, `Content-ID` and attachment dispositions | S0-3 (1) and (3) |
| G7 | MSTest 3.x is deprecated | decision 3, stage 4 |
| G8 | Four Microsoft packages on 8.0.x | S2-2 (three shipped ones upgraded; the unused Pkcs leaves), S2-4, batch A |
| G9 | Apphost manifest and version resources | S3a-10 (v), S3b-7 |
| G10 | Workflow scripts outside the repository embed old lane steps | S1-0, S4-2 |
| G11 | UpdateFileCopier settings, `useLegacyV2RuntimeActivationPolicy`, `SetDefaultFont` placement | S3b-4 |
| G12 | Checked and clear | section 9.2 |
