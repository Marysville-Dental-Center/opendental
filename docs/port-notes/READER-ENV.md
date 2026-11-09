# Port note: READER-ENV (the project-file reader reads every name as MSBuild reads it, environment variables included)

Follow-up package of S2-1 ([S2-1.md](S2-1.md), sections 9 and 10) for the project-file reader `tests/ProjectFileTests/ProjectFile.cs`, the
stage gates' evaluator of the .NET 10 move ([docs/specs/net10-plan.md](../specs/net10-plan.md), section 3). Branch `net10/readerenv-1` from
`net10/s21r3-1` (`a8dd529b7`, S2-1 finished, not yet on `main`). No package, version, lock file or project file changed; nothing was
downloaded; no program was started.

## 1. What changed

| File | Change |
|---|---|
| `tests/ProjectFileTests/ProjectFile.cs` | every name the reader's own code reads where MSBuild's or the SDK's files read it is read as MSBuild reads it (section 2, the inventory in section 3); the class summary wrapped again and its paragraph on environment variables |
| `tests/ProjectFileTests/EnvironmentVariablesTests.cs` (new) | 15 tests, one case per converted read (section 4) |
| `tests/ProjectFileTests/ImportsTests.cs` | `WithEnvironmentVariable` internal (the new tests use it); `TheSdksPackageReferenceNames_AsEnvironmentVariablesOrNotKnown_Throw`: the case of S2-1's review (section 6) |
| `tests/LibraryBindingTests/RequiredDllsTests.cs` | the checks that need no `project.assets.json` run before the inconclusive exit (section 6) |
| `docs/port-notes/S2-1.md` | section 10: which `run-tests.ps1` sets `OPMS_REQUIRE_BUILD_OUTPUT`; the open point on S1-T's reads points here |

Commits: `d2ad8d380` (the reader), `3b432605f` (reads whose old form was already safe stay as they were), `b7323d074` (the tests),
`cb977956a` (what the mutation check found in the tests), `05fa914a5` (S2-1's review points), and this note.

## 2. The rule, and what the reader does now

MSBuild starts every evaluation with the environment variables of its process as properties (`Utilities.GetEnvironmentProperties`: every
variable whose name is a valid property name and not reserved), then the toolset's properties over them, then the global properties over
both. A file's assignment replaces a variable; a global property cannot be replaced. So wherever MSBuild's files, the SDK's or the project's
read `$(Name)` and no file set the name before that point, an environment variable of the name is the value, unless the name is reserved
(`MSBuildProjectDirectory`, `MSBuildProjectFile`, `MSBuildThisFile*`, `MSBuildToolsVersion` and the rest), global (`Configuration`,
`Platform`, and the solution's `Solution*` for a project `OpenDental.sln` lists) or the toolset's (`MSBuildExtensionsPath` and the other
names of `_arrayNotModelledProperties`). `VisualStudioVersion` is the toolset's too, but `Toolset.GenerateSubToolsetVersion` takes it from a
variable of the name, and the evaluator sets it only when it is not set yet, so a variable reaches it.

The reader does not model environment variables (a test process has its own). S1-T made an expression (`GetValueForExpression`) and
`GetProperty` refuse a variable no file set; S2-1 made the central-package and restore-import code read its names that way
(`GetValueIfKnown`, `GetKnownValue`, `IsKnownTrue`, `IsKnownFalse`). The rest of the reader's own code, its modelling of MSBuild's other
files (S1-T), still read such a name as `''` (or compared `SetIfEmpty`'s marker as a value), and so could disagree with MSBuild without a
word. Now:

- **`GetValue`** gives the marker of a value not known for an environment variable no file set, as an expression does: a value the reader
  builds from it (a path, a list, a moniker) is not known, and a test of it for `''` is false, as MSBuild's is. A name a file, a global
  property or the reader's model of an unconditional assignment set is unchanged.
- **A comparison** of the reader's own code reads its name with **`IsValueIfKnown`** (null when the value is not known: an environment
  variable no file set, or a value with an expression the reader cannot evaluate), combined with C#'s `&`, `|` and `!` on `bool?`, which
  are three-valued as MSBuild's `and`, `or` and `not` are on a value not known: an operand MSBuild does not read (after a false operand of an
  `and`, a true one of an `or`) decides nothing either way. What MSBuild's files compute from a condition not known is not known
  (**`NotKnown`**: a marker naming the property, the file and the name it is not known from; `GetProperty` throws for it, and so does every
  condition, include or import that uses it).
- **A condition that decides an import or an item** throws at `Load` (`GetKnownValue`, `IsKnownTrue`, `IsKnownFalse`): items and imports
  are not markers. So does **an import of a file a property names** (the `Custom.Before`/`Custom.After` files of Directory.Build, of
  `Microsoft.Common` and of the language targets, `AlternateCommonProps`, `LanguageTargets`, the target framework inference hooks, the
  publish profile, ...: **`ThrowIfImportHook`**) when a file or an environment variable sets it: MSBuild imports what it names, which the
  reader does not read.
- **A name MSBuild's files read and the reader took as `''`** is read: `MSBuildIsRestoring`, `OutDirWasSpecified`,
  `TargetingClr2Framework`, `_IsExecutable`, `_PlatformToAppendToOutputPath`, `_UnsupportedTargetFrameworkError`, `Language` and
  `_EnablePackageReferencesInVCProjects`, `DisableDefaultItemsInProjectFolder`, `MicrosoftNETBuildExtensionsTargets`,
  `IsRestoreTargetsFileLoaded`, `NuGetRestoreTargets`, the import hooks above; and the presets `ProjectDir`, `ProjectPath`,
  `ProjectFileName`, `ProjectName` and `VisualStudioVersion` give way to a variable (`SetIfEmpty`).

Three fixes that are not about variables came with it, each found while reading MSBuild's lines: `CustomAfterDirectoryBuild{Props,Targets}`
is read after the import, where Directory.Build.props/.targets may set it (before, a file that set it was missed); `ProjectPath` is
`$(ProjectDir)$(ProjectFileName)` as line 348 has it; and `_EnableWindowsDesktopGlobbing` is the WindowsDesktop targets' only when
`Microsoft.NET.Sdk.targets` imported them (otherwise a file's or a variable's, which `Microsoft.NET.Sdk.WindowsDesktop.props` reads).
S2-1's review point 4 (the AND of NuGet.targets) is section 6.

No result changes for the repository's projects: no such variable is set where the build lane, the evaluator and the tests run (the
machine's and the user's variables are `PATH`, `TEMP`, `TMP`, `OS`, `PROCESSOR_*`, `USERNAME` and the like; the lane and
`Initialize-BuildEnvironment.ps1` add only the `DOTNET_*`, `NUGET_CERT_REVOCATION_MODE`, `OPMS_*` names S2-1 listed), and the evaluator's
counts are S2-1's (section 7).

## 3. The inventory

Every property read of `ProjectFile.cs`. Files are those of the Build Tools 2026 MSBuild 18 (`Microsoft.Common.props`, `Microsoft.Common.targets`,
`Microsoft.Common.CurrentVersion.targets` "CurrentVersion", `Microsoft.CSharp.CurrentVersion.targets`, `Microsoft.NETFramework.CurrentVersion.props`,
`Microsoft.NET.props`, `Microsoft.Common.targets\ImportAfter\Microsoft.NET.Build.Extensions.targets`) and of SDK 10.0.114 (`Sdk.props`,
`Sdk.targets`, `Microsoft.NET.Sdk.props` and `.targets`, `Microsoft.NET.Sdk.BeforeCommon.targets` "BeforeCommon",
`Microsoft.NET.TargetFrameworkInference.targets` "Inference", `Microsoft.NET.DefaultOutputPaths.targets` "OutputPaths",
`Microsoft.PackageDependencyResolution.targets`, `Microsoft.NET.Sdk.DefaultItems.props` and `.targets`, `Microsoft.NET.Sdk.CSharp.props`
and `.targets`, `Microsoft.NET.Sdk.Analyzers.targets`, `Microsoft.NET.Sdk.WindowsDesktop.props` and `.targets`); the line is the file's.
"Before" is what the reader did with an environment variable of the name that no file set; "now" what it does; "test" the case of
`EnvironmentVariablesTests` (section 4) and the mutation unit (section 5) that undoes the read.

### 3.1 Converted reads

**Evaluate, the presets** (the reader sets them at the start, so a project's own body sees them)

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `ProjectDir` | CurrentVersion 345, only when `''` | the folder, over the variable | `SetIfEmpty`: not known | P1; M01 |
| `ProjectFileName` | CurrentVersion 256 | the file name | not known | P2; M02 |
| `ProjectPath` | CurrentVersion 348, `$(ProjectDir)$(ProjectFileName)` | the full path, over a variable of any of the three | `SetIfEmpty` of `$(ProjectDir)$(ProjectFileName)`: not known with either | P1-P3; M03, M06 |
| `ProjectName` | CurrentVersion 246 | the project's name | not known | P4; M04 |
| `VisualStudioVersion` | the toolset's sub-toolset version (a variable of the name wins); CurrentVersion 34 | 18.0 | not known | P5; M05 |

**`ImportDirectoryBuildFile`** (Directory.Build.props: `Microsoft.Common.props` lines 19-36; Directory.Build.targets: `Microsoft.Common.targets`
lines 45-62; one code for both)

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `ImportDirectoryBuildProps`, `ImportDirectoryBuildTargets` | 19, 26, 34 / 45, 52, 60 | `SetIfEmpty`'s marker compared as not 'true': the file left out | `IsKnownTrue`: throws | D1; M07 |
| `DirectoryBuildPropsPath`, `DirectoryBuildTargetsPath` | 26, 34 / 52, 60, only with the switch on | `''`: the file above imported where MSBuild imports the variable's | `GetKnownValue`: throws; not read with the switch off | D2; M08 |
| `_DirectoryBuildPropsFile`, `_DirectoryBuildTargetsFile` | 27-29 / 53-55 | the marker as a file name: `ArgumentException` | `GetKnownValue`: throws | D3; M09 |
| `_DirectoryBuildPropsBasePath`, `_DirectoryBuildTargetsBasePath` | 28-29 / 54-55 | the marker as a folder: `ArgumentException` | throws | D4; M10 |
| `CustomBeforeDirectoryBuildProps`, `...Targets` | 32 / 58 | `''`: MSBuild imported the variable's file | `ThrowIfImportHook`: throws | D5; M11 |
| `CustomAfterDirectoryBuildProps`, `...Targets` | 36 / 62, after the import | `''`; and read before the import, so a Directory.Build file that set it was missed | throws; read after the import | D6, D7; M12, M13 |

**`ImportMicrosoftCommonProps`, `ImportNuGetProps`, `ImportMicrosoftCommonTargets`, `ApplyBuildExtensionsTargets`**

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `MSBuildIsRestoring` | Common.props 68 (the restore's props off), read only when `ImportProjectExtensionProps` is `''` | not read | `GetKnownValue`: throws; a file's 'true' turns the restore's files off, as MSBuild does | I11; M14 |
| `MSBuildIsRestoring` | Common.targets 37, the same for the targets | not read | throws | I12; M15 |
| `CustomBeforeMicrosoftCommonProps`, `CustomAfterMicrosoftCommonProps` | Common.props 88-92, 113 | not read: MSBuild imported the variable's file | `ThrowIfImportHook` | I1, I2; M16, M17 |
| `MSBuildUseVisualStudioDirectoryLayout` | Common.props 122-127: Visual Studio's NuGet.props or `$(MSBuildToolsPath)`'s (the Build Tools' Bin folder has none) | not read | throws | I3; M18 |
| `NuGetPropsFile` | Common.props 123-127 | not read | throws | I4; M19 |
| `IsCrossTargetingBuild` | Microsoft.CSharp.targets 20 (and .VisualBasic.targets): the cross-targeting targets | not read | 'true' or not known: throws | I5; M20 |
| `CustomBeforeMicrosoftCSharpTargets`, `CustomAfterMicrosoftCSharpTargets` (and Visual Basic's) | CSharp.CurrentVersion 30-34, 392 | not read | throws | I6, I7; M21, M22 |
| `MicrosoftCommonPropsHasBeenImported` | CurrentVersion 18, for a project without its own Common.props import | `IsPropertyTrue`: Common.props read where MSBuild skips it | `IsKnownTrue`: throws | I10; M25 |
| `CustomBeforeMicrosoftCommonTargets`, `CustomAfterMicrosoftCommonTargets` | CurrentVersion 38-43, 7140 | not read | throws | I8, I9; M23, M24 |
| `ImportByWildcardAfterMicrosoftCommonTargets` | CurrentVersion 22, 7142 | the marker as not 'true': `_TargetFrameworkVersionWithoutV` left `''` for a classic project | not known | B1; M26 |
| `MicrosoftNETBuildExtensionsTargets` | ImportAfter file 14, 17 | not read | a variable: not known; a file's: not modelled (not known) | B2; M27 |

**`ApplyCommonTargetsProperties`** (CurrentVersion) **and `ApplyNuGetTargetsProperties`**

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `TargetRuntime` | 61-71 (reachable for a project without language targets) | the marker: no .NET Framework defaults | the defaults not known | C1; M28 |
| `TargetFrameworkIdentifier`, `TargetFrameworkVersion` | 80-81 and 90-92, when TargetRuntime is not Managed | `''`: no moniker, no framework defaults | `GetValue`: not known | C2, C3; M29-M32 |
| `TargetFrameworkMoniker` | 91-92 | computed over the variable | stays the variable | C4; M33 |
| `TargetFrameworkProfile` | 91 | `''` | the moniker not known | C5; M34 |
| `_EnableDefaultWindowsPlatform` | 104-105 (the SDK sets it only for .NET 5+) | `''`: Windows and 7.0 | `IsValueIfKnown`: not known | C6, T5, E5, V1; M35 |
| `TargetPlatformMoniker` | 114 | computed over the variable | stays the variable | C7; M36 |
| `TargetPlatformIdentifier`, `TargetPlatformVersion` | 114, with the defaults off | `''`: no moniker | the moniker not known | C8; M37-M40 |
| `TargetType` | 182-183 | `''` | OutputType not known | C9; M41, M42 |
| `DebugSymbols`, `DebugType` | 186 | `''`: DebugSymbols true | as MSBuild: the variable stands, or no default | C10; M43, M44 |
| `ConfigurationName` | 186 | `SetIfEmpty`'s marker as not Debug | not known | C11; M45 |
| `TargetExt` | 230-235, 262 | `''`: computed, and TargetFileName from `''` | stays the variable; TargetFileName not known | C12, C13; M46, M47 |
| `OutputType` | 230-235, 253, 260-261, 377 (a variable for an SDK-style project: `SetIfEmpty`) | the marker matched none: TargetExt `''`, TargetName and TargetFileName the assembly's, no WinMD name, no GenerateBindingRedirectsOutputType | not known | C14-C17; M48-M51 |
| `RootNamespace` | 253 | `''` | TargetName not known | C18; M52, M53 |
| `TargetName` | 253 | `''`: RootNamespace over the variable | stays the variable | C19; M54 |
| `OutDirWasSpecified` | 240, 250 | the reader's own flag; the name not read | read as MSBuild reads it | C20, C21; M55, M56 |
| `WindowsAppContainer` | 250 | `''` | OutDir not known | C22; M57 |
| `GenerateProjectSpecificOutputFolder` | 250, both reads | `''` | OutDir not known | C23, C24; M58, M59 |
| `AutoGenerateBindingRedirects` | 379 | true over the variable | stays the variable | C25; M60 |
| `TargetFrameworkIdentifier`, `TargetFrameworkVersion` | 379 | the marker: neither .NETFramework nor a version, `''` | not known | C26, C27; M61, M62 |
| `PublishDir` | 514-515 | the output path's `app.publish\` | stays the variable | C28; M63 |
| `DisableOutOfProcTaskHost` | 569 | `''`: the group's values | the group left out, as MSBuild does | C29; M64 |
| `PlatformTargetAsMSBuildArchitecture` | 574-575 | ExplicitlySet false | true, as MSBuild | C30; M65 |
| `SignAssembly` | 594 | `''`: DelaySign false, then `''` | DelaySign not known when it is 'true' | C31; M66 |
| `DelaySign` | 595 | `''` | stays the variable | C32 (C31); M67 |
| `IsRestoreTargetsFileLoaded` | 7138: NuGet.targets imported unless 'true' | not read | `_CentralPackageVersionsEnabled` not known | N1; M68 |
| `NuGetRestoreTargets` | 7134-7138 | not read | not known | N2; M69 |
| `ManagePackageVersionsCentrally`, `CentralPackageVersionsFileImported` | NuGet.targets 65 (S2-1) | S2-1: the first not known made the switch not known | a false operand decides the AND | N3; M70 |

**`ApplyFrameworkProps`** (`Microsoft.NETFramework.CurrentVersion.props`, `Microsoft.NET.props`)

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `TargetingClr2Framework` | 76 (set only for 2.0-3.5), 86, 98; NET.props 35 | `''`: Prefer32Bit true, 6.00, HighEntropyVA true | not known | F1; M71 |
| `OutputType` | 86 (the project's; the common default comes later) and 96 | `''` counted as none | not known | F2, F3; M72, M73 |
| `PreferNativeArm64` | 86 | `''` | Prefer32Bit not known | F4; M74 |
| `PlatformTarget` | 96 | `''` | SubsystemVersion not known | F5; M75 |
| `SubsystemVersion` | 96 | 6.02 over the variable | stays the variable | F6; M76 |

**`ImportSdkProps`, `ImportSdkTargets`, `ApplySdkBeforeCommonProperties`, `ApplySdkLanguageProperties`**

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `AlternateCommonProps` | Sdk.props 48-49 | not read | `ThrowIfImportHook` | S1; M77 |
| `DebugSymbols` | Microsoft.NET.Sdk.props 96 (over the project's DebugType) | the marker as not 'false': portable | DebugType not known | K1; M92 |
| `WarningsAsErrors`, `DefineConstants` | CSharp.props 18, 21-22 | `''` | not known | K2, K3; M93, M94 |
| `AfterMicrosoftNetSdkProps` | Microsoft.NET.Sdk.props 182 | not read | throws | S2; M78 |
| `BeforeMicrosoftNETSdkTargets`, `IsCrossTargetingBuild`, `LanguageTargets` | Sdk.targets 13, 16-23, 25-38 | not read | throws | S3-S5; M79-M81 |
| `OutputType` | BeforeCommon 24 | the marker as not exe | `_IsExecutable` not known | E1; M113 |
| `_IsExecutable`, `HasRuntimeOutput` | BeforeCommon 27-28 | `''` | stays the variable | E2, E3, G3; M114, M115 |
| `WebPublishProfileFile`, `PublishProfileFullPath`, `PublishProfileName`, `PublishProfile` | BeforeCommon 47-48, Microsoft.NET.Sdk.ImportPublishProfile.targets | not read | throws unless PublishProfileImported is 'true' | S10-S13; M86-M89 |
| `UsingMicrosoftNETSdkRazor` | BeforeCommon 73 | not read | throws | S7; M83 |
| `BeforeTargetFrameworkInferenceTargets`, `AfterTargetFrameworkInferenceTargets` | Inference 47, 278 | not read | throws | S8, S9; M84, M85 |
| `TargetFramework`, `TargetFrameworks` | Inference 52, Sdk.targets 16 | `''`: the common targets' .NETFramework v4.0 | not known; TargetFrameworks throws | T10, T11; M104, M105 |
| `TargetFrameworkIdentifier`, `TargetFrameworkVersion` | Inference 52 | `''`: inferred again over the variables | kept, not known | T1; M95, M96 |
| `TargetFrameworkVersion` | Inference 59 | `''` | `_TargetFrameworkVersionWithoutV` not known | T5, V1, SPR; M97 |
| `TargetFrameworkIdentifier`, `TargetFrameworkVersion` | Inference 93-95 | not modelled | `_UnsupportedTargetFrameworkError` | T4; M98-M100 |
| `_TargetFrameworkVersionWithoutV`, `TargetFrameworkIdentifier` | Inference 215, 281 | the marker as not a version, not .NETCoreApp | not known | T5, V1, V2; M101, M102 |
| `Language`, `_EnablePackageReferencesInVCProjects` | Inference 215 (Language is the project's here) | not read | `_EnableDefaultWindowsPlatform` not known | E5, E6; M117, M118 |
| `ProduceReferenceAssembly` | Inference 281 | computed over the variable | stays the variable | E8; M120 |
| `TargetFrameworkIdentifier` | Inference 281 | the marker | not known | T12; M103 |
| `ProduceOnlyReferenceAssembly` | Inference 281 | `''` | not known | E7; M119 |
| `UseArtifactsOutput`, `UseArtifactsIntermediateOutput` | OutputPaths 48-110 | the marker as not 'true' | `IsKnownTrue`: throws | S14, S15; M90, M91 |
| `AppendPlatformToOutputPath` | OutputPaths 26-28 | `SetIfEmpty`'s marker as not 'true': no platform folder | not known | E9; M121, M122 |
| `PlatformName` | OutputPaths 26-27 | the marker as not AnyCPU: true | not known | E10; M123 |
| `_PlatformToAppendToOutputPath` | OutputPaths 28, 104, 113 | the reader's own `''` | stays the variable | E11; M124 |
| `DefaultItemExcludes` | OutputPaths 125-127 | `''` | not known (the default items throw) | X6; M159 |
| `AppendTargetFrameworkToOutputPath`, `_UnsupportedTargetFrameworkError` | OutputPaths 141, 146 | the marker; not read | not known | E13, E4, T4; M125, M116 |
| `RuntimeIdentifier` | RuntimeIdentifierInference 368 | `''` | the output paths not known | E14; M126 |
| `HasRuntimeOutput` | BeforeCommon 102-104 | `IsPropertyTrue` | not known | E15; M127 |
| `GenerateDocumentationFile`, `DocumentationFile` | BeforeCommon 301-313 | `''` | as MSBuild: the variable stands, DocumentationFile not known | E16-E18; M128-M131 |
| `CopyLocalLockFileAssemblies` | PackageDependencyResolution 31-42 | computed over the variable | stays the variable | G1; M132 |
| `_TargetFrameworkVersionWithoutV`, `HasRuntimeOutput`, `EnableDynamicLoading` | PackageDependencyResolution 37-39 | the marker as not below 3.0; `IsPropertyTrue` | not known | V3, G3, G2; M110, M134, M133 |
| `DisableDefaultItemsInProjectFolder` | DefaultItems.targets 18-22 | not read | EnableDefaultItems not known (Load throws) | X5; M158 |
| `DefaultExcludesInProjectFolder`, `DefaultItemExcludesInProjectFolder` | DefaultItems.targets 51 | `''` | not known (Load throws) | X8, X7; M161, M160 |
| `UseWPF`, `UseWindowsForms` | Microsoft.NET.Sdk.targets 1468-1469 | `''` | `IsKnownTrue`: throws | W1, W2; M143, M144 |
| `TargetFrameworkIdentifier`, `_TargetFrameworkVersionWithoutV`, `TargetPlatformIdentifier` | 1470-1473 | the marker compared: no WindowsDesktop targets | `GetKnownValue`: throws | W3, V5, W4; M145, M111, M146 |
| `ImportWindowsDesktopTargets` | 1468, 1475 | computed over the variable | stays the variable: throws | W5; M147, M148 |
| `AfterMicrosoftNETSdkTargets` | 1476, 1510 | not read | throws | S6; M82 |
| `EnableUnsafeBinaryFormatterSerialization`, `TargetFrameworkIdentifier`, `_TargetFrameworkVersionWithoutV` | Microsoft.NET.Sdk.targets 189 | `''`; the marker | not known | G4, T6, V4; M135, M106, M107 |
| `UseWindowsForms`, `UseWPF` | 195-196 | `''` | `_ProjectTypeRequiresBinaryFormatter` not known | G5; M136, M137 |
| `_ProjectTypeRequiresBinaryFormatter` | 200 (set only in that group) | `''` | not known | G6 (G5); M138 |
| `DisableImplicitConfigurationDefines` | CSharp.targets 16-31 | `''` | ImplicitConfigurationDefine and DefineConstants not known | G7; M139 |
| `_BinaryFormatterObsoleteAsError` | CSharp.targets 45 (set only for .NET 7+) | `''` | WarningsAsErrors not known | G8, T6, V4; M140 |
| `AnalysisLevel`, `WarningLevel`, `TargetFrameworkIdentifier` | Analyzers.targets 61-65 | `''`, computed over the variable, the marker | not known; the variable stands | G9, G10, T7; M141, M142, M108 |

**The items** (`AddSdkDefaultItems`, `ApplySdkTargetsItems`, `IsWindowsDesktopGlobbing`, `ApplyItemDefinitions`, `ApplySdkLinkMetadata`;
items and metadata are not markers, so a switch not known throws at `Load`)

| Name | MSBuild | Before | Now | Test |
|---|---|---|---|---|
| `EnableDefaultItems` | DefaultItems.props 35, WindowsDesktop.targets 56 | the marker as not 'true': no items | `IsKnownTrue`: throws | X1; M154 |
| `EnableDefaultCompileItems`, `...EmbeddedResourceItems`, `...NoneItems` | DefaultItems.props 36-45 | no items | throws | X2-X4; M155-M157 |
| `_EnableWindowsDesktopGlobbing` | WindowsDesktop.props 3, 17 (only WindowsDesktop.targets 56 sets it) | computed for a project without those targets too | the targets' when imported; otherwise `IsKnownTrue` | W6; M149 |
| `UseWPF`, `_TargetFrameworkVersionWithoutV` | WindowsDesktop.targets 51, 56, 73 | `''`; the marker as not a version | throws | W7, V6; M150, M112 |
| `EnableDefaultPageItems` | WindowsDesktop.props 25, 39; .targets 80, 100 | the marker as not 'false' | `IsKnownFalse`: throws (the four reads one unit: the first throws) | W8; M151 |
| `EnableDefaultApplicationDefinition` | WindowsDesktop.props 19, 39; .targets 100 | the same | throws | W9; M152 |
| `DefaultXamlRuntime` | WindowsDesktop.props 6, 12 | the marker's text as XamlRuntime metadata | `GetKnownValue`: throws | W10; M153 |
| `SetLinkMetadataAutomatically` | DefaultItems.targets 82 | `''`: the Link set | `IsKnownFalse`: throws | X9; M162 |

**`ThrowIfImplicitPackageReferences`** (S2-1's read, test added): `_TargetFrameworkVersionWithoutV`, a version not known for .NET Core
throws (SPR; M163; section 6).

**Size:** 159 reads converted, each undone in turn by its mutation unit (section 5), and three structural fixes (M06, M13, M70).

### 3.2 Reads left as they were

| Reads | Why |
|---|---|
| Reserved names: `MSBuildProjectFile`, `MSBuildProjectName`, `MSBuildProjectExtension`, `MSBuildProjectDirectory`, `MSBuildProjectFullPath`, `MSBuildThisFile*`, `MSBuildToolsVersion`, `MSBuildProjectDirectoryNoRoot` (`IsRestoreFile`, `ImportProjectExtensions`, the `AssemblyName`, `RootNamespace`, `ProjectName`, `ProjectExt` defaults) | no variable reaches them |
| Global properties: `Configuration`, `Platform` (every `IsValue` of `ImportSdkProps`, the output paths, the configuration's define), and the solution's `Solution*` for a listed project | a global property replaces a variable; for an unlisted project `SetIfEmpty("*Undefined*")` already gives a variable's marker |
| Names MSBuild's files set unconditionally before the read, as the reader does: `Language`, `DefaultLanguageSourceExtension`, `TargetRuntime` (C# and Visual Basic), `MicrosoftCommonPropsHasBeenImported`, `UsingMicrosoftNETSdk`, `UsingNETSdkDefaults`, `DisableHandlePackageFileConflicts`, `RestoreProjectStyle`, `SupportsRoslynComponentVersioning`, `_TargetFrameworkVersionWithoutV` of an SDK-style project (Inference 59), `TargetDir` (CurrentVersion 336, whenever OutDir is not `''`), `ProjectAssetsFile` (normalized unconditionally), `_InitialBaseIntermediateOutputPath`, the appends to `DefaultItemExcludes`, `DefaultExcludesInProjectFolder`, `WarningsAsErrors` and `DefineConstants` after their first read | the file's value replaces the variable |
| Names `SetIfEmpty` sets just before the read, so a variable is already the marker there, which flows into every value made from it and makes `GetProperty` throw: `BaseOutputPath`, `OutputPath`, `BaseIntermediateOutputPath`, `IntermediateOutputPath`, `MSBuildProjectExtensionsPath`, `OutDir`, `AssemblyName`, `TargetName` (line 254), `TargetFileName`, `TargetPath`, `ProjectExt`, `AppDesignerFolder`, `Utf8Output`, `PlatformName` in the classic output paths (CurrentVersion 169-176: `IsValue` with the marker gives the marker's folder), `TargetFrameworkAsMSBuildRuntime`, `PlatformTargetAsMSBuildArchitecture`, `ProduceReferenceAssembly` (common default), the SDK's defaults (`Configurations` ... `CheckForOverflowUnderflow`, `NoWarn`, `PlatformTarget`, `DebugType`, the `EnableDefault*` switches, `IsPublishable`, `AppendTargetFrameworkToOutputPath`), the `ImportByWildcard*` switches, and the names S2-1 read through `SetIfEmpty` and `ThrowIfUnresolved` (`ImportProjectExtensionProps`, `ImportProjectExtensionTargets`, `ImportDirectoryPackagesProps`, `_DirectoryPackagesPropsFile`, `_DirectoryPackagesPropsBasePath`) | unchanged: a variable was not known before already |
| Reads whose old form already refused, left as they were (commit `3b432605f`): `TargetPlatformIdentifier` in Inference 66-67 (a marker is not 'Windows'; the version is then 7.0 as MSBuild has it for a framework without a platform, or not modelled for .NET 5+), `AppendRuntimeIdentifierToOutputPath` (RuntimeIdentifierInference 358, 368: a variable is not 'false', so the paths are not known either way), `TargetFrameworkIdentifier` in BeforeCommon 102 (CurrentVersion 379 reads it again and makes AutoGenerateBindingRedirects not known), `DelaySign` in line 594's branch (line 595 decides it), `OutputType` in the TargetExt loop (the not-known branch before it decides), `TargetType` against Container (the marker is OutputType's value), `TargetFrameworkVersion` in TargetFrameworkAsMSBuildRuntime (its `IsUnresolved` branch), `PlatformTarget` in PlatformTargetAsMSBuildArchitecture (`IsPropertyKnown` makes it not modelled) | the old read already gave a value not known, so no fixture can tell it from a converted one |
| Reads a variable does not reach, because the reader's own model of MSBuild stored the name before (so `GetValue`'s change cannot matter there): `TargetFrameworkIdentifier` in the CopyLocalLockFileAssemblies guard (the common targets' `SetIfEmpty` ran first; mutation unit M109 survived as an equivalent mutant), `TargetFrameworkVersion` in `ApplyBuildExtensionsTargets`, `TargetPlatformIdentifier`/`Version` in Inference 65 (both variables: `SetIfEmpty`'s marker), `EnableDefaultItems` in DefaultItems.targets 18 (the next `SetIfEmpty`), `OutDir` in line 240 (the next `SetIfEmpty`), the identifier and version after a not-known check returned (`ApplyFrameworkProps`, CopyLocalLockFileAssemblies, WarningLevel) | not reachable |
| S2-1's converted reads (S2-1.md, section 10): `ImportDirectoryPackagesProps`, `DirectoryPackagesPropsPath`, `_DirectoryPackagesPropsFile`, `_DirectoryPackagesPropsBasePath`, `ManagePackageVersionsCentrally`, `CentralPackageVersionsFileImported`, `_CentralPackageVersionsEnabled`, `CentralPackageVersionOverrideEnabled`, `RestoreEnableGlobalPackageReference`, `DisableImplicitFrameworkReferences`, `TargetFrameworkIdentifier`, `_TargetFrameworkVersionWithoutV`, `RuntimeFrameworkVersion`, `ImportProjectExtensionProps`, `ImportProjectExtensionTargets`, `NuGetPackageFolders` | already read as MSBuild reads them; the AND of NuGet.targets three-valued now (section 6) |

## 4. Tests

`EnvironmentVariablesTests` (new), one test per MSBuild file or method, each case labelled as in section 3 (the label is in every assertion
message). Each case sets the variable for the test process with `ImportsTests.WithEnvironmentVariable` (given back in a `finally`; MSTest v1
runs an assembly's tests one after another in one host, S2-1 section 10) and checks that the reader does not answer as if it were `''`:
`GetProperty` throws for what is computed from it, naming the variable, or `Load` throws for an import or an item; where it matters, a file
that sets the name decides it, and the value without the variable is MSBuild's.

| Test | Cases |
|---|---|
| `TheCommonTargetsPresets_AsEnvironmentVariables_AreNotKnown` | P1-P5 |
| `DirectoryBuildFileNames_AsEnvironmentVariables_ThrowWhereMicrosoftCommonReadsThem` | D1-D7, each for Props and Targets |
| `TheImportsOfTheCommonFiles_AsEnvironmentVariables_Throw` | I1-I12 |
| `TheBuildExtensionsTargets_AsEnvironmentVariables_MakeTheVersionWithoutVNotKnown` | B1, B2 |
| `TheCommonTargetsProperties_FromEnvironmentVariables_AreNotKnown` | C1-C32 |
| `NuGetTargets_FromEnvironmentVariables_LeavesTheCentralSwitchNotKnown_UnlessAFalseOperandDecidesIt` | N1-N3 |
| `TheFrameworkPropsDefaults_FromEnvironmentVariables_AreNotKnown` | F1-F6 |
| `TheImportsOfTheSdksFiles_AsEnvironmentVariables_Throw` | S1-S15 |
| `TheSdksPropsDefaults_FromEnvironmentVariables_AreNotKnown` | K1-K3 |
| `TheTargetFramework_FromEnvironmentVariables_IsNotInferredAgain` | T1-T12 |
| `TheVersionOfANetCoreProject_NotKnown_MakesWhatDependsOnItNotKnown` | V1-V6 |
| `TheSdksBeforeCommonProperties_FromEnvironmentVariables_AreNotKnown` | E1-E18 |
| `TheSdksTargetsProperties_FromEnvironmentVariables_AreNotKnown` | G1-G10 |
| `TheWindowsDesktopNames_AsEnvironmentVariables_Throw` | W1-W10 |
| `TheDefaultItemSwitches_AsEnvironmentVariables_Throw` | X1-X9 |
| `ImportsTests.TheSdksPackageReferenceNames_AsEnvironmentVariablesOrNotKnown_Throw` | SPR (section 6) |

All of them fail on the reader of `net10/s21r3-1`: its `ProjectFile.cs` built with the tests of `b7323d074` failed all 16 (run
`.git\od-tools\test-results\rdenv-oldreader.txt`); section 5 undoes each read on its own.

## 5. The mutation check

As S2-1's round 3 did, each converted read was undone in turn, and the run recorded which tests failed
(`.git\od-tools\test-results\rdenv-mut-<unit>.txt`, and one line per unit with the failing cases in `rdenv-mutation-summary.txt`). For the
159 reads the undoing was done by a temporary instrumented build of `tests\ProjectFileTests` (never committed): each helper of the reader
(`GetValue`, `GetUnescaped`, `GetValueIfKnown`, `IsValueIfKnown`, `GetKnownValue`, `IsKnownTrue`, `IsKnownFalse`, `ThrowIfImportHook`,
`SetIfEmpty`, the `MSBuildIsRestoring` read) took the read's old form (a variable no file set read as `''`, the value compared as before,
a preset set over the variable) when the variable `OPMS_READERENV_MUTATION` named the source line of its caller and the name (the code's
line numbers unchanged; a statement over several lines is keyed by its first line, and two reads of one name in one statement by the value
compared). One build, then one Targeted run of ProjectFileTests (with NoHqTests) per unit. The three structural fixes were undone by hand,
one build each (M06: `ProjectPath` as the full path again; M13: the CustomAfter read before the import; M70: S2-1's form of the AND).

**Result: every unit killed, by exactly the tests of its reads.** 151 of the 162 units failed exactly one test, at the case of section 3
(the test's other cases passed up to it). The 11 others failed every test that has a case of the read:

| Unit | Tests that failed (cases) | Why more than one |
|---|---|---|
| M35 `_EnableDefaultWindowsPlatform` (CurrentVersion 104-105) | CTP (C6), TFW (T5), SBC (E5), VER (V1) | four cases make it not known |
| M95, M96 (Inference 52) | TFW (T1), WDT (W3) | W3 needs the inference skipped too |
| M97 (Inference 59) | TFW (T5), VER (V1), SPR | the version of three cases |
| M101 (Inference 215, 281) | TFW (T5), VER (V1) | both cases have a version not known |
| M115 `HasRuntimeOutput` (BeforeCommon 27) | SBC (E3), STP (G3) | |
| M116 `_UnsupportedTargetFrameworkError` | SBC (E4), TFW (T4) | |
| M106, M108 (Microsoft.NET.Sdk.targets 189, Analyzers.targets 63-65) | TFW (T6, T7), `SdkComputedTests.Sdk_TheConfigurationsDefine_AndTheTargetFrameworksDefaults` | the same reads now make WarningsAsErrors and WarningLevel of a framework the reader does not know (`uap10.0`) not known, which the old code did with its own `IsUnresolved` branch; the mutant reads that marker as a value too |
| M107 (Microsoft.NET.Sdk.targets 189, the version) | TFW (T6), VER (V4), the same `SdkComputedTests` test | the same |
| M140 `_BinaryFormatterObsoleteAsError` (CSharp.targets 45) | STP (G4), TFW (T6), VER (V4), the same `SdkComputedTests` test | the same |

M67 (`DelaySign`, line 595) and M138 (`_ProjectTypeRequiresBinaryFormatter`, line 200) fail at C31 and G5, the case before their own: the
same read also handles the value the line before made not known. Four findings of the first pass, fixed before the record above: units
M31 and M32 survived, because case C2 for TargetFrameworkVersion kept the C# targets (which set TargetRuntime Managed over the project's
Native, so the common targets' `SetIfEmpty` stored the marker first); the case imports `Microsoft.Common.targets` alone now. M57-M59 at first
named the read's own line, which a statement over several lines does not report; they are keyed by the statement's first line now. M121-M124
were killed only by the message of `Load`'s exception (the output paths are in the default items' excludes, so `Load` throws with or
without the read); cases E9-E11 check AppendPlatformToOutputPath and OutputPath themselves with the default items off now. And M109, the
TargetFrameworkIdentifier of the CopyLocalLockFileAssemblies guard, survived as an equivalent mutant: the common targets' `SetIfEmpty`
stored the name before, so a variable never reaches that read; it is listed in section 3.2, not as a converted read. The summary file
keeps every run, the first pass's included; the last line of a unit is its result.

## 6. S2-1's review points

- **(a) `RequiredDllsTests.PackagesOfTheLockFiles_MapToTheirFilesOfRequiredDlls_ByteForByte`** returned Inconclusive as soon as a
  `project.assets.json` was missing, before the checks that need none. Now each package of the lock files is checked first for being in the
  offline folder, mapped (`PackageFiles.GetRequiredDlls`, which throws for a package it does not map or a file `Required dlls` does not
  hold) or bringing none, and for the names of a package that brings none; those fail as they would after a build. Only the comparison
  with the files the build copies (read from the assets files) is inconclusive without them. With a build, the test passes as before (15 of
  15 in LibraryBindingTests).
- **(b) `GetKnownValue("_TargetFrameworkVersionWithoutV")`** in `ThrowIfImplicitPackageReferences` has its test:
  `TheSdksPackageReferenceNames_AsEnvironmentVariablesOrNotKnown_Throw` loads a .NET Core project without a TargetFramework (so nothing is
  inferred) with `ImportByWildcardAfterMicrosoftCommonTargets` false and its TargetFrameworkVersion an environment variable: the version is
  not known, and the PackageReference queries throw, with the switch false and true. A variable of the name `_TargetFrameworkVersionWithoutV`
  itself does not reach the read: `Microsoft.NET.TargetFrameworkInference.targets` line 59 sets it for every SDK-style project (the file is
  imported unconditionally, BeforeCommon line 54, whatever its comment says), over the variable, as the test shows (3.1, and one
  PackageReference). Mutation unit M163 undoes the read: the test fails.
- **(c) S2-1.md section 10** said `run-tests.ps1` sets only `OPMS_REQUIRE_BUILD_OUTPUT`: that is the lane's copy in the common git folder
  (`.git\od-tools\run-tests.ps1`, which the Targeted runs use, with `-RequireBuildOutput`); the repository's own `tools\run-tests.ps1` sets
  none. Corrected there.
- **Point 4.** `_CentralPackageVersionsEnabled` was not known whenever `ManagePackageVersionsCentrally` was not, even with
  `CentralPackageVersionsFileImported` known to be `''`; NuGet.targets' AND is false then whichever the first operand is. The switch is now
  the three-valued AND of the two (`ApplyNuGetTargetsProperties`): a false operand decides it, as MSBuild evaluates it (N3, M70). The over-wide
  lines of the class summary are wrapped again.

## 7. The proof

In this worktree, cleaned with `git clean -fdXq` at `05fa914a5` (the code of this package; this note changes no built file):

- **One cold full lane build** of `OpenDental.Local.slnf` (Debug x86): 0 errors, 353 warnings as before; `git status` clean after it. The
  build log's compiler command lines name 31 outputs that compile `tests\ProjectFileTests\ProjectFile.cs` besides ProjectFileTests itself,
  32 in all, as at S2-1's round 3.
- **`tools\Test-ProjectFileEvaluator.ps1`**: exit 0, "The evaluator agrees with MSBuild": 153 projects, 306 evaluations, 40,485 comparisons
  (31,798 Debug equal, 3,638 Debug not modelled, 5,049 Release equal), no mismatch, silent empty, core-not-modelled, evaluator or MSBuild
  error: the same counts as S2-1's round 3.
- **The project baseline.** `tools\Get-ProjectBaseline.ps1 -Against` a copy of S2-1's round-3 record: one difference, the item
  `ProjectFileTests|Compile|EnvironmentVariablesTests.cs`, this package's new test file; resources, configs and file names without a
  difference; sizes and SHA-256 of 121 files differ (the test assemblies that compile `ProjectFile.cs`, the program's copies with the build
  stamp, Direct2dWrapper, the classic Middle Tier), not gated. Against that new record, ProjectFileTests with `OPMS_CHECK_PROJECT_BASELINE=1`:
  `items-check.txt` 153 projects (152 SDK-style), 13,540 recorded items, 13,540 evaluated, "No differences" (against S2-1's record itself the
  same check names the one new item).
- **Targeted runs** (NoHqTests in each): ProjectFileTests, LibraryBindingTests, AttributionTests with the baseline variables
  (`.git\od-tools\test-results\rdenv-targeted.txt`), then the 29 other assemblies that compile `ProjectFile.cs`
  (`rdenv-targeted-2.txt`). All passed:

| Assembly | Total | Passed | Not run |
|---|---|---|---|
| ProjectFileTests | 126 | 126 | (111 before; 15 new) |
| LibraryBindingTests | 15 | 15 | |
| AttributionTests | 50 | 50 | |
| NoHqTests | 58 | 58 | |
| VendorDirectTests | 922 | 922 | |
| ReleaseFlowTests | 120 | 119 | 1: `BuildIdFiles_AreAllInTheReleaseOutput` is inconclusive without a Release build, as before |
| SshHostKeyTests | 46 | 46 | |
| the 26 others that link `ProjectFile.cs` (PdfLibraryTests and LibSpellTests among them) | 1,313 | 1,313 | |

ForkMigrationTests and InitSqlTests were left out, and `db/init/new-database.sql` was not written again (the coordinator's rule).

## 8. For the lead

- **What the reader still does not model** (unchanged, each reads as `''` or throws as before): the files of MSBuild's and Visual Studio's
  `ImportBefore`/`ImportAfter` folders other than Microsoft.NET.Build.Extensions.targets, and the user's own folders of that kind
  (`%LOCALAPPDATA%\Microsoft\MSBuild`), which MSBuild imports for every project when they exist; the workload resolver
  (`MSBuildEnableWorkloadResolver`); the WinFX targets of a .NET Framework project without WPF; the SDK's AnalysisLevel default. They decide
  names the reader lists as not modelled, or none it computes for the repository's projects.
- **Seen while reading BeforeCommon, not about variables:** line 115 gives a .NET Core or .NET Standard project `TargetExt` .dll even when it
  is an exe; the reader's CurrentVersion lines 230-235 give .exe. No project of the repository is .NET Core yet, so the evaluator cannot see
  it; the first .NET 10 project of stage 3 will (it compares TargetExt). Worth modelling before then.
- **The rule for a later change to the reader:** a read of the reader's own code goes through `GetValue` (a value) or `IsValueIfKnown` (a
  comparison) unless the name is reserved, global or set just before; an import or an item decision through `GetKnownValue`/`IsKnownTrue`;
  an import of a file a property names through `ThrowIfImportHook`. `EnvironmentVariablesTests` is the place for its case.
