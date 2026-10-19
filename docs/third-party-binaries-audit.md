# Third-party binaries audit

Status: 2026-10-18. Baseline: `main` at `ff2e2a5b4` (Open Dental 24.3.63.0). This is the `Required dlls` license audit from Phase 2 of the [decoupling plan](decoupling-plan.md).

The audit covers every file in `Required dlls/` and `RequiredServerDlls/`: 179 files, 73.4 MB.

- `Required dlls/` holds 157 files at the top level and 16 in three subfolders: `.NET Core/` (3), `.NET Standard/` (10) and `Sparks3D/` (3).
- `RequiredServerDlls/` holds 6 files.

For each file the audit records its version info, its vendor and product, its license, whether anything references or loads it, the feature that uses it, and what to do with it.

## Findings

- **132 files (49 MB) are open source**, mostly MIT and Apache-2.0. Nearly all of them can come from NuGet instead of being committed binaries.
- **51 files (17.5 MB) are unused** and can be deleted. 44 of them can go without touching any project file. See [Unused files](#unused-files-that-can-be-deleted).
- **33 files are Open Dental proprietary, commercial, or of unknown license**, and two more are Microsoft platform files that we may not redistribute. Ten are already being replaced in other pieces: the seven Open Dental assemblies named in the brief and the three commercial EZTwain files. The remaining blockers, in priority order, are:
  - Sparks3D, whose license forbids SaaS use and reverse engineering.
  - VBbridges and xImageDeviceManager (with SuniUSB).
  - The Topaz and DentalXChange SDKs, which are kept vendor integrations that need a way to stop shipping the vendor's binary.
  - COM interop generated from the Schick, QuickBooks Desktop and Microsoft Office type libraries, plus `stdole.dll`.
  - `Windows.winmd` and `installutil.exe`. They are Microsoft platform files with no redistribution grant, and the build copies both into its output today. Setting `<Private>False</Private>` on their two References stops that.
  - VideoCamLib, Open Dental's native video-capture library.
  - `RS232.dll`, whose license is unknown.

  See [Blockers](#binaries-that-block-a-truly-open-source-release).
- **The build copies none of the 12 native DLLs** into the output folder. Open Dental's installer, which is not in this repo, must have deployed them. A local build therefore runs without TWAIN scanning, video capture, the Sparks3D chart and the WebView2 loader. See [Native DLLs](#native-dlls).
- **Several declared dependencies are missing.** Examples: BouncyCastle for MimeKit, the JWT library for Microsoft Graph, and the Microsoft.Extensions packages for Serilog. Moving to NuGet package references makes the dependency set complete. See [Other findings](#other-findings).
- **The in-app third-party license list is incomplete and partly wrong.** For example, it shows the Ionic web framework's license for DotNetZip.

## Summary counts

### By license classification

| Classification | Files | Size |
|---|---:|---:|
| Open source | 132 | 49.2 MB |
| Microsoft redistributable or platform component | 14 | 5.9 MB |
| Commercial or proprietary third party | 13 | 3.2 MB |
| Open Dental proprietary | 13 | 13.9 MB |
| Unknown | 7 | 1.3 MB |
| **Total** | **179** | **73.4 MB** |

Open source files by license: MIT 57, Apache-2.0 43, GPL-2.0 (built from this repo, or Oracle MySql.Data) 8, BSD-3-Clause 4, MS-PL 3, LGPL-2.1-or-later (fyiReporting) 3, MIT-X11 (Tao) 3, Mentalis.org (BSD-style) 2, MPL / LGPL / GPL (Hunspell) 2, LGPL-2.1 or BSD (MF.Net) 2, X-Consortium (freeglut) 1, NDde Shared Source (permissive) 1, LGPL / MPL / GPL (NHunspell) 1, Public domain (OpenPop.NET) 1, BSD (SharpSSH/JSch) 1.

### By recommended action

| Recommended action | Files | Size |
|---|---:|---:|
| Keep as is | 10 | 2.4 MB |
| Replace with a NuGet package | 77 | 30.8 MB |
| Replace with an in-repo open source implementation | 18 | 15.0 MB |
| Remove because unused | 51 | 17.5 MB |
| Remove together with a feature or binary that is being cut or replaced | 10 | 3.5 MB |
| Remove from the repo and install the vendor redistributable instead | 1 | 0.1 MB |
| Remove from the repo; an optional plugin binds to the DLL from the vendor's own installer | 2 | 0.6 MB |
| Already being replaced in another piece of the decoupling work | 10 | 3.5 MB |

### Classification by action

| Classification | Keep | NuGet | In-repo | Unused | Cut | Redist | Plugin | Elsewhere |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Open source | 10 | 72 | 5 | 40 | 5 | - | - | - |
| Microsoft redistributable or platform component | - | 5 | 1 | 7 | - | 1 | - | - |
| Commercial or proprietary third party | - | - | 3 | 4 | 1 | - | 2 | 3 |
| Open Dental proprietary | - | - | 4 | - | 2 | - | - | 7 |
| Unknown | - | - | 5 | - | 2 | - | - | - |

## How this audit was done

- **Version info:** PowerShell `(Get-Item <file>).VersionInfo` for every file. The fields appear verbatim in [Appendix A](#appendix-a-version-info-of-every-file).
- **Project references:**
  - Every `HintPath` in all 40 project files in the repo, whether or not the project is in `OpenDental.Local.slnf`.
  - `ProjectReference`s to projects that are not in the repo, which `Directory.Build.targets` replaces with the prebuilt DLL.
  - No `Content` or `None` item and no build event copies any of these files.
- **Runtime loading by name:**
  - A search of all source, config, resource and report-definition files for each file name.
  - A review of every `DllImport`, `LoadLibrary` and `Assembly.Load*` call. Of the files audited, only `VideoCamLib.dll` is `DllImport`ed by our code; the others are loaded by libraries.
- **Static dependencies:** a detailed MSBuild log of the normal build. Its assembly-resolution step lists which assemblies the compiled `.exe`/`.dll` files really reference, as opposed to the References a project merely declares. Many declared References turn out to be dead.
- **Native or managed, architecture, imported DLL names:** read from each file's PE header as plain data, the same information `dumpbin /dependents` prints. **No file was loaded, executed, decompiled or disassembled.**
- **License facts:**
  - Version-info copyright notices.
  - The license texts already in `OpenDental/Resources/*.txt`.
  - Source headers of the in-repo projects (`RdlEngine/`, `ODR/`, `PasswordVaultWrapper/`).
  - Vendor, GitHub and NuGet pages, linked in each row and listed under [Sources](#sources).
- **Build check:** the standard `OpenDental.Local.slnf` Debug x86 build of this commit finishes with 0 errors and 445 warnings. The detailed log above came from that same build.

**Classification rules**

| Class | Meaning |
|---|---|
| Open source | OSI-style license (MIT, Apache-2.0, BSD, MS-PL, LGPL, GPL, MPL), public domain, or an equivalent permissive license such as NDde's or Mentalis.org's. Includes binaries built from GPL source in this repo, and Microsoft packages published under MIT. |
| Microsoft redistributable or platform component | Microsoft binaries that may be redistributed under Microsoft's terms (VC++ runtime, WebView2 SDK, DirectX, older NuGet builds under the .NET Library license). Also Microsoft files that ship with Windows, .NET or the Windows SDK (InstallUtil, `Windows.winmd`). Those platform files carry no redistribution grant: users already have them, and we must not ship our copies. |
| Commercial or proprietary third party | Vendor binaries under commercial or "All rights reserved" terms, and COM interop assemblies generated from a third party's proprietary type library. |
| Open Dental proprietary | Open Dental binaries whose source is not published. |
| Unknown | No license found that lets us ship the file as it sits in the repo. This includes interop generated from Microsoft type libraries outside Microsoft's official primary interop assemblies. It also includes `stdole.dll`, an official PIA that Microsoft supports redistributing only through its PIA installer. |

**Limits**

- The Open Dental installer is not in the repo, so this audit cannot see which files it deploys or what else it ships.
- Where one prebuilt binary loads another (for example `SuniUSB.dll` behind `xImageDeviceManager.dll`), the link is inferred from code comments and naming, and the row says so.

## Binaries that block a truly open source release

A file blocks the release when either the build or a feature we keep needs it, and it is one of these:

- Open Dental proprietary, or commercial.
- Of unknown license.
- A Microsoft platform file with no redistribution grant that the build copies into its output.

Unused proprietary files are not listed here; they are simply deleted (for example the four Adobe interop files).

### Priority 1: compile-time Open Dental assemblies, already assigned

| Files | Project files that reference them | Status |
|---|---|---|
| `CDT.dll` | 12 (5 substituted ProjectReferences, 7 HintPaths) | Being replaced elsewhere |
| `Bridges.dll` | 4 | Being replaced elsewhere (split; QuickBooks Online, Microsoft 365 and USPS are kept) |
| `ODCrypt.dll` | 3 (HintPaths only) | Being replaced elsewhere |
| `OpenDentalHelp.dll` | 3 | Being replaced elsewhere |
| `VirtualWeb.dll` | 4 | Being replaced elsewhere |
| `ImagingDeviceManager.dll` | 1 | Being replaced elsewhere (NTwain) |
| `ODApi.dll` | 1 | Being replaced elsewhere (in-repo local REST API) |
| `EHR` | 0 (loaded by reflection) | Being removed elsewhere. There is no `EHR.dll` in `Required dlls`. |

### Priority 2: compile-time Open Dental binaries with no owner yet

1. **`Sparks3D/Sparks3D.dll`**, plus `Sparks3DXEngine.dll` and `readme.txt`.
   - Four projects reference it.
   - Its own license bans SaaS use and reverse engineering, and allows thin-client use only for customers on an Open Dental support plan. That license is incompatible with an open source release.
   - Only two files use its types: `WpfControlsOD/UI/ToothChart/Sparks3DInterface.cs` and `OpenDentBusiness/SheetFramework/ToothChartHelper.cs`. The app already falls back to the in-repo `SparksToothChart` when the DLL is missing (`ToothChartRelay.IsSparks3DPresent`). Remove the references and those two code paths, then make the fallback the default. A new Direct3D 11 chart can come later.
2. **`VBbridges.dll`**.
   - Three projects reference it.
   - Our code calls only five static methods: `DrCephNew.Launch`, `Ecw.LoadRxForm`, `Ecw.GetAppServer`, `Patterson.Launch` and `Schick3.Launch`.
   - Re-implement them in C# in-repo. These are vendor bridges, so the features stay.
3. **`xImageDeviceManager.dll`** (10 MB) and **`SuniUSB.dll`**.
   - They provide direct Suni sensor capture. `FormImages` always hosts a `ControlImagesOld`, and its constructor always creates `xImageDeviceManager.SuniDeviceControl`, so the DLL is loaded whenever that window opens, not only when the `ImagesModuleUsesOld2020` pref is set. The held `decouple/twain` branch removes the Suni code path along with these files.
   - Owner decision needed: delete them with the old module (Suni sensors also work through TWAIN), or re-implement Suni capture from Suni's SDK documentation.

### Priority 3: commercial vendor SDKs referenced at compile time (kept integrations)

4. **`SigPlusNET.dll`** (Topaz signature pads). Four projects reference it.
5. **`DpsPos.dll`** (DentalXChange PayConnect terminals). Two projects reference it. At runtime it also loads `log4net.dll` by name.

Neither vendor publishes redistribution terms, so the repo should not carry these files. Move each integration into a small optional assembly that is built from our source but resolves the vendor DLL at runtime from the vendor's own installation. Show "not installed" when the DLL is absent. The core build then needs neither file.

### Priority 4: COM interop generated from proprietary type libraries

6. **`Interop.CDRDATALib.dll`, `Interop.CDRDicom.dll`** (Schick CDR) and **`Interop.QBFC10.dll`** (QuickBooks Desktop).
   - All three are referenced with `EmbedInteropTypes=True`, so only the compiler needs them.
   - Late binding (`dynamic`) or a few hand-written `[ComImport]` interfaces in `WpfControlsOD/Bridges/Schick.cs` and `QuickBooks.cs` remove them.
7. **`Interop.Word.dll`, `Interop.Microsoft.Office.Core.dll`, `Interop.VBIDE.dll`**, plus `stdole.dll`.
   - They support Word letter merge (`FormLetterMerges.cs`, `LetterMerges.cs`).
   - The build copies `stdole.dll` into `OpenDental\bin\Debug`, as a dependency of the copy-local Office interop References. Its license is unknown. Microsoft supports redistributing it only through its Office PIA redistributable installer, so a loose copy must not ship, and no NuGet `stdole` package grants redistribution either.
   - Late binding to `Word.Application` removes all four.

### Priority 5: Microsoft platform files that the build copies into its output

These files come with Windows, the Windows SDK or the .NET Framework, and no Microsoft license lets us redistribute them. Each one is only a one-line project change away from no longer shipping. Checked on this commit: with `<Private>False</Private>` added to both References, the build still finishes with 0 errors, and neither file appears in any output folder. (That test change was not committed.)

8. **`Windows.winmd`**, the Windows SDK union metadata.
   - The `Windows` Reference in `PasswordVaultWrapper/PasswordVaultWrapper.csproj` (lines 61-63) has the default CopyLocal, so the build copies the file into `OpenDental\bin\Debug`.
   - Interim fix: add `<Private>False</Private>` to that Reference. The compiler still reads the file. At runtime Windows supplies its own metadata for system WinRT types, and the SDK's single-file copy exists for compile time only ([source](https://learn.microsoft.com/en-us/uwp/winrt-cref/winmd-files)). So `PasswordVault` should keep working, but test a saved credential once after the change.
   - Final fix: either of these.
     - NuGet `Microsoft.Windows.SDK.Contracts`. It must also keep its `.winmd` files out of the output folder: check `bin` after the switch, and turn off copy-local on those references if they appear.
     - The Credential Manager P/Invoke option (`CredWrite`/`CredRead` in `advapi32.dll`), which needs no `.winmd` at all.
   - Either way, delete `Required dlls/Windows.winmd`.
9. **`installutil.exe`** (`Required dlls/`, .NET Framework 4 build).
   - The `installutil` Reference in `ServiceManager/ServiceManager.csproj` (lines 84-87) has the default CopyLocal, so the build copies the file into `ServiceManager\bin\Debug`.
   - No ServiceManager code uses a type from it, so the Reference does nothing at compile time.
   - Interim fix: add `<Private>False</Private>` to that Reference, or delete the Reference outright.
   - Final fix: point `CodeBase/Utilities/ServicesHelper.cs` at the framework's own `InstallUtil.exe` (the folder `RuntimeEnvironment.GetRuntimeDirectory()` returns), or call `ManagedInstallerClass.InstallHelper`. Then delete both repo copies.

### Priority 6: proprietary binaries needed only at runtime

10. **`VideoCamLib.dll`**, Open Dental's native Media Foundation wrapper. Video capture (`FormVideo`) fails without it. Re-implement in-repo: the code comments say it was derived from the MIT-licensed Tanta samples, which are a clean starting point.
11. **EZTwain** (`Eztwain4.dll`, `EZT4Jpeg.dll`, `EZT4Pdf.dll`). Already being replaced elsewhere (NTwain).

### Priority 7: unknown license

12. **`RS232.dll`**, a VB.NET serial-port class of unknown origin and license.
    - It is used only for the BCBS Georgia dial-up clearinghouse (`TerminalConnector.cs`).
    - Rewrite it on `System.IO.Ports.SerialPort`. A commented-out C# port, `Eclaims/Rs232.cs`, is already in the repo.

### Not blockers by license, but they should leave the repo

- **`vcruntime140.dll`** should come from the VC++ Redistributable, installed as a prerequisite.
- **The WebView2 SDK files** should come from NuGet `Microsoft.Web.WebView2`.
- **`Ionic.Zip.dll`** (DotNetZip) is open source but abandoned, and its last release has a high-severity directory-traversal advisory, CVE-2024-48510. Replace it with `System.IO.Compression`.
- **`Tamir.SharpSSH.dll`** is open source but unmaintained since 2013. Port its four call sites to SSH.NET, which the repo already uses. That also removes `DiffieHellman.dll` and `Org.Mentalis.Security.dll`.

## Unused files that can be deleted

51 files (17.5 MB) do nothing for the application today. Nothing loads them by name and no compiled assembly depends on them, so deleting them cannot break a feature.

### Delete outright (no project in the solution references them): 44 files, 17.3 MB

| File | Size | Why it is unused |
|---|---:|---|
| `.NET Core/AWSSDK.Core.dll` | 964 KB | None. No project references the `.NET Core` folder. |
| `.NET Core/AWSSDK.EC2.dll` | 1,836 KB | None. No project references the `.NET Core` folder. |
| `.NET Core/AWSSDK.S3.dll` | 536 KB | None. No project references the `.NET Core` folder. |
| `.NET Standard/BouncyCastle.Crypto.dll` | 2,128 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/Google.Protobuf.dll` | 292 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/MySql.Data.dll` | 742 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/Newtonsoft.Json.dll` | 677 KB | None. No project references it, not even `xDataConnectionBaseStandard.csproj`. |
| `.NET Standard/Renci.SshNet.dll` | 838 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/SshNet.Security.Cryptography.dll` | 34 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/System.Configuration.ConfigurationManager.dll` | 372 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/System.Security.Cryptography.ProtectedData.dll` | 24 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/System.Security.Permissions.dll` | 92 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `.NET Standard/System.Xml.XPath.XmlDocument.dll` | 116 KB | Referenced only by `xDataConnectionBaseStandard.csproj`, which is not in the solution and cannot build. |
| `freeglut.dll` | 236 KB | Only through Tao.FreeGlut, which nothing calls. Native x86. |
| `IppDotNetSdkForQuickBooksApiV3.dll` | 4 KB | None. An empty package assembly that no project references. |
| `MediaFoundation.dll` | 350 KB | None. Nothing references it (a leftover from the video-capture work). |
| `MediaFoundation.Extension.dll` | 86 KB | None. Nothing references it. |
| `Microsoft.Azure.Documents.Client.dll` | 1,711 KB | None in practice |
| `Microsoft.DirectX.Direct3D.dll` | 462 KB | Only `TestToothChart.csproj`, which is not in the solution. |
| `Microsoft.DirectX.Direct3DX.dll` | 565 KB | None. No project references it. |
| `Microsoft.DirectX.dll` | 218 KB | None. No project references it. |
| `Microsoft.DotNet.PlatformAbstractions.dll` | 23 KB | None. It belongs to Microsoft.Extensions.DependencyModel, which Serilog.Settings.Configuration wants but which is not here. No project references it. |
| `Microsoft.IdentityModel.JsonWebTokens.dll` | 87 KB | None in the current build |
| `Microsoft.ML.Core.dll` | 404 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.CpuMath.dll` | 60 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.Data.dll` | 1,522 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.DataView.dll` | 46 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.dll` | 14 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.FastTree.dll` | 466 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.KMeansClustering.dll` | 54 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.LightGbm.dll` | 86 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.PCA.dll` | 52 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.StandardTrainers.dll` | 316 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `Microsoft.ML.Transforms.dll` | 744 KB | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. |
| `MSVCR71.dll` | 340 KB | No. No binary in the repo imports it and no code names it. |
| `PasswordVaultWrapper.dll` | 6 KB | None. A stale prebuilt copy: `OpenDentBusiness` uses a ProjectReference to the in-repo project. |
| `RdlDesigner.exe` | 1,008 KB | No (named only in comments in `FormReportForRdl.cs` and `FormReportCustom.cs`) |
| `System.Net.Http.dll` | 193 KB | None. No project references it, and the framework's System.Net.Http is used. |
| `RequiredServerDlls/BatchCheckServiceStatus.bat` | 270 B | Runs `net pause OpenDental` to show the service state for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). |
| `RequiredServerDlls/BatchInstallService.bat` | 130 B | Runs `installutil.exe OpenDentServer.exe` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). |
| `RequiredServerDlls/BatchStartService.bat` | 81 B | Runs `net start OpenDental` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). |
| `RequiredServerDlls/BatchStopService.bat` | 79 B | Runs `net stop OpenDental` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). |
| `RequiredServerDlls/BatchUninstallService.bat` | 136 B | Runs `installutil.exe /u OpenDentServer.exe` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). |
| `RequiredServerDlls/installutil.exe` | 28 KB | Installing the retired OpenDentServer service |

### Delete after removing a dead reference from a project file: 7 files, 0.2 MB

A project in the solution names these as a `<Reference>`, but no code uses them and no compiled assembly references them. Remove the `<Reference>` item, then delete the file.

| File | Size | Referenced by |
|---|---:|---|
| `AxInterop.AcroPDFLib.dll` | 9 KB | HintPath: OpenDental |
| `Interop.Acrobat.dll` | 36 KB | HintPath: OpenDental |
| `Interop.AcroPDFLib.dll` | 12 KB | Nothing directly. It is needed only by `AxInterop.AcroPDFLib.dll`; delete them together. |
| `Interop.AFORMAUTLib.dll` | 12 KB | HintPath: OpenDental; Not built: PluginExample |
| `MigraDoc.RtfRendering.dll` | 80 KB | HintPath: OpenDental; Not built: PluginExample |
| `Tao.FreeGlut.dll` | 40 KB | HintPath: SparksToothChart, xCodeBase |
| `zxing.presentation.dll` | 19 KB | HintPath: OpenDental |

Files that become unused only after other work lands (the "Cut" and "Elsewhere" actions) are not in these lists. Examples: the AWS, Avalara and Windows Media Player files, SharpSSH's helpers and the EZTwain DLLs.

## Native DLLs

Twelve files are plain native Windows DLLs and four are mixed-mode C++/CLI assemblies. All are 32-bit (x86), like the application, except `Hunspellx64.dll`. The kind, architecture and imported DLL names come from the PE headers. Nothing was loaded or disassembled.

| File | Kind and architecture | Imports | Who loads it | Copied by the build | Recommendation |
|---|---|---|---|---|---|
| `MSVCR71.dll` | Native, x86 | KERNEL32.dll | No. No binary in the repo imports it and no code names it. | No | Delete (unused). |
| `vcruntime140.dll` | Native, x86 | api-ms-win-crt-runtime-l1-1-0.dll, api-ms-win-crt-heap-l1-1-0.dll, api-ms-win-crt-string-l1-1-0.dll, api-ms-win-crt-stdio-l1-1-0.dll, api-ms-win-crt-convert-l1-1-0.dll, KERNEL32.dll | Yes, as an import of `Dicom.Native.dll`, `Sparks3D/Sparks3DXEngine.dll` and Release builds of `Direct2dWrapper.dll`. | No | Do not keep it in the repo. The installer should chain `vc_redist.x86.exe`, which Microsoft recommends, and which also supplies `MSVCP140.dll`. Dicom.Native.dll needs that file and it is not in `Required dlls`. |
| `Hunspellx86.dll` | Native, x86 | KERNEL32.dll | Yes, loaded by NHunspell.dll. Debug builds of `ODtextBox.cs:202-203` copy it from `Required dlls`. | No | Comes with NuGet `NHunspell`. Not needed at all with `WeCantSpell.Hunspell`. |
| `Hunspellx64.dll` | Native, x64 | KERNEL32.dll | Yes, loaded by NHunspell.dll in 64-bit processes only. The x86 app never loads it. | No | Same as Hunspellx86.dll. |
| `Eztwain4.dll` | Native, x86 | KERNEL32.dll, USER32.dll, GDI32.dll, WINSPOOL.DRV, comdlg32.dll, ADVAPI32.dll, SHELL32.dll, OLEAUT32.dll | Yes, loaded by `ImagingDeviceManager.dll`. The code shows "EzTwain4.dll not found" when it is missing (`ControlImages.cs:2627`). | No | NTwain (MIT) replaces it. Delete it when ImagingDeviceManager goes. |
| `EZT4Jpeg.dll` | Native, x86 | KERNEL32.dll, USER32.dll, GDI32.dll | Yes, loaded by EZTwain. | No | Delete with Eztwain4.dll. |
| `EZT4Pdf.dll` | Native, x86 | KERNEL32.dll, EZT4Jpeg.dll, USER32.dll | Yes, loaded by EZTwain. | No | Delete with Eztwain4.dll. |
| `freeglut.dll` | Native, x86 | KERNEL32.dll, USER32.dll, GDI32.dll, ADVAPI32.dll, WINMM.dll, OPENGL32.dll, GLU32.dll | Only through Tao.FreeGlut, which nothing calls. | No | Delete (unused). |
| `SuniUSB.dll` | Native, x86 | KERNEL32.dll | Presumably loaded by `xImageDeviceManager.dll`. Named only in a comment (`WpfControlsOD/Bridges/Guru.cs:61`). | No | Remove with xImageDeviceManager. Suni users install Suni's own driver or use TWAIN. |
| `VideoCamLib.dll` | Native, x86 | ole32.dll, OLEAUT32.dll, KERNEL32.dll | Yes. `[DllImport("VideoCamLib.dll")]` in `OpenDental/UI/VideoCamera/VideoCamLib.cs`. | No | Re-implement in-repo: a small C++ project built from the MIT Tanta samples, or managed Media Foundation through MF.Net (LGPL-2.1) or DirectShowLib (LGPL-2.1). |
| `WebView2Loader.dll` | Native, x86 | KERNEL32.dll | Yes, loaded by `Microsoft.Web.WebView2.Core.dll`. `CodeBase/Controls/ODWebView2.cs:42` warns when it is missing. | No | Replace with NuGet `Microsoft.Web.WebView2`, which ships the loader under `runtimes/win-x86/native` and copies it. |
| `Sparks3D/Sparks3DXEngine.dll` | Native, x86 | d2d1.dll, DWrite.dll, d3d11.dll, VCRUNTIME140.dll, api-ms-win-crt-heap-l1-1-0.dll, api-ms-win-crt-runtime-l1-1-0.dll, KERNEL32.dll | Yes, loaded by `Sparks3D.dll`. | No | Remove together with Sparks3D.dll. |
| `Dicom.Native.dll` | Mixed-mode C++/CLI, x86 | MSVCP140.dll, KERNEL32.dll, VCRUNTIME140.dll, api-ms-win-crt-runtime-l1-1-0.dll, api-ms-win-crt-heap-l1-1-0.dll, api-ms-win-crt-string-l1-1-0.dll, api-ms-win-crt-stdio-l1-1-0.dll, api-ms-win-crt-math-l1-1-0.dll, mscoree.dll | Yes, by fo-dicom's desktop codec support, not by our code. No compiled assembly references it. Needs MSVCP140.dll and VCRUNTIME140.dll. | Yes (HintPath) | NuGet `fo-dicom.Desktop` (MS-PL) ships it. The VC++ redistributable is a prerequisite. |
| `Microsoft.DirectX.dll` | Mixed-mode C++/CLI, x86 | KERNEL32.dll, mscoree.dll, msvcrt.dll, ADVAPI32.dll | No | No | Delete (unused). |
| `Microsoft.DirectX.Direct3D.dll` | Mixed-mode C++/CLI, x86 | KERNEL32.dll, mscoree.dll, msvcrt.dll, USER32.dll, GDI32.dll, d3d9.dll | No | No | Delete (unused), or delete TestToothChart with it. |
| `Microsoft.DirectX.Direct3DX.dll` | Mixed-mode C++/CLI, x86 | msvcrt.dll, KERNEL32.dll, mscoree.dll, USER32.dll, GDI32.dll, d3dx9_30.dll | No | No | Delete (unused). |

Notes:

- **The build copies none of the twelve native DLLs.** No project has `Content` items or copy steps for them, so they reached users only through Open Dental's installer. A local `OpenDental\bin\Debug` lacks:
  - TWAIN (EZTwain) and video capture (VideoCamLib).
  - The Sparks3D chart. The app falls back to `SparksToothChart`.
  - `WebView2Loader.dll`. `ODWebView2` shows a warning when it is missing.
  - Spell check in the WPF controls. `ODtextBox` copies the Hunspell DLLs from `Required dlls` itself, and only in Debug builds.

  Native code we keep should either come from NuGet packages, whose `runtimes/win-x86/native` files MSBuild copies, or be built from source in the repo, the way `Direct2dWrapper` already is.
- **`MSVCR71.dll` has no consumer.**
  - The Hunspell and Suni DLLs link the C runtime statically: they import only `KERNEL32`.
  - The EZTwain DLLs (apart from importing each other), freeglut and VideoCamLib import only Windows system DLLs.
  - No code names it. It is probably a leftover from an older native component. Delete it.
- **`vcruntime140.dll` alone is not enough.**
  - `Dicom.Native.dll` imports `MSVCP140.dll` and `VCRUNTIME140.dll`. `Sparks3DXEngine.dll` and Release builds of our own `Direct2dWrapper.dll` import `VCRUNTIME140.dll`.
  - `MSVCP140.dll` is not in `Required dlls`, so DICOM's native codecs already depend on the VC++ Redistributable being installed.
  - Microsoft recommends installing the central redistributable package rather than copying runtime DLLs next to the app. Make `vc_redist.x86.exe` an installer prerequisite and delete the copy.
- **Hunspell:** NHunspell picks `Hunspellx86.dll` or `Hunspellx64.dll` at runtime. The x86 application only ever uses the x86 one. A managed port such as `WeCantSpell.Hunspell` removes both native files and the Debug copy step.
- **The mixed-mode DirectX files** (`Microsoft.DirectX*.dll`) are unused. Only `TestToothChart`, which is not built, references one of them.

## Per-file audit

One row per file, grouped by family. "csproj references" covers every project file in the repo. "Not built" marks projects outside `OpenDental.Local.slnf`. "Substituted" means a `ProjectReference` to a project that is not in the repo, which `Directory.Build.targets` swaps for this DLL. The raw version-info fields are in [Appendix A](#appendix-a-version-info-of-every-file).

### Open Dental assemblies already being replaced

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `Bridges.dll` | 116 KB | Open Dental Software, Bridges | Proprietary, source not published | Open Dental | ProjectReference, substituted: OpenDental; HintPath: OpenDentBusiness, UnitTests, WpfControlsOD | No | CareCredit, PostcardMania, QuickBooks Online, Microsoft 365 email, USPS address validation. It pulls in the Intuit, Microsoft Graph, MSAL and Azure.Core assemblies below. | **Replaced elsewhere.** Split into in-repo code. Delete the DLL and the `UnitTests`, `OpenDentBusiness` and `WpfControlsOD` HintPaths when the replacement lands. |
| `CDT.dll` | 339 KB | Open Dental Software, CDT | Proprietary, source not published | Open Dental | ProjectReference, substituted: OpenDentBusiness, OpenDental, OpenDentalServer, WpfControlsOD, xDataConnectionBase; HintPath: OpenDentHL7, ServiceManager, UnitTests, xCentralManager, xDatabaseIntegrityCheck; Not built: xDataConnectionBaseStandard, xODR | No (comments only, e.g. `OpenDental/Forms/FormRegistrationKey.cs:79`) | Secret encryption, `SecurityHash` salted hashes, registration-key validation, claim-form images. | **Replaced elsewhere.** Replaced by an in-repo library. Twelve project files reference it (five through substituted ProjectReferences, seven through HintPaths), so the HintPath ones need updating too. |
| `ImagingDeviceManager.dll` | 64 KB | Open Dental Software, ImagingDeviceManager | Proprietary, source not published | Open Dental | ProjectReference, substituted: OpenDental | No, but it loads `Eztwain4.dll` at runtime | TWAIN scanning and sensor acquisition in the Imaging module (`ControlImages.cs`, `FormImagingSetup.cs`, `FormImagingDeviceEdit.cs`). | **Replaced elsewhere.** Scanning is re-implemented on NTwain or WIA. |
| `ODApi.dll` | 1,240 KB | Open Dental Software, ODApi | Proprietary, source not published | Open Dental | ProjectReference, substituted: OpenDental | No | Local REST API server (port 30222), used from `FormOpenDental.cs`. | **Replaced elsewhere.** The owner decided to keep the local REST API, so it gets an in-repo implementation. |
| `ODCrypt.dll` | 81 KB | Open Dental Software, ODCrypt | Proprietary, source not published | Open Dental | HintPath: OpenDentBusiness, OpenDental, UnitTests | No | MD5 and SHA3-512 password hashes, salts, random values, gzip. | **Replaced elsewhere.** Replaced by an in-repo shim. `UnitTests.csproj` also has a HintPath to it. |
| `OpenDentalHelp.dll` | 142 KB | Open Dental Software, OpenDentalHelp (version info wrongly says "Microsoft") | Proprietary, source not published | Open Dental | ProjectReference, substituted: OpenDental, UnitTests, WpfControlsOD | No | F1 manual URLs, help-key and support-status checks, HQ SOAP client. | **Replaced elsewhere.** Replaced by an in-repo help-URL map. |
| `VirtualWeb.dll` | 6 KB | Open Dental Software, VirtualWeb ("Web") | Proprietary, source not published | Open Dental | ProjectReference, substituted: OpenDental, WpfControlsOD; HintPath: OpenDentBusiness, OpenDentalGraph | No | Thinfinity "ODCloud" browser-hosting shim. | **Replaced elsewhere.** The Thinfinity code paths are deleted. |

### Other Open Dental binaries

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `ODR.dll` | 52 KB | Open Dental Software, ODR (RDL report helpers) | GPL-2.0, the repo license. The source is in this repo at `ODR/xODR.csproj`. | Open source | None | Yes. RDL report definitions name it as a `<CodeModule>` (`OpenDental/Resources/Unsched Treat Plans.rdl`, `ReportAccountingGenLedger.txt`, `ReportAccountingBalanceSheet.txt`), and RdlEngine loads it. No csproj references it, so the build never copies it to `bin`. | Helper functions for RDL reports (accounting balance sheet, general ledger, unscheduled treatment plans). | **In-repo replacement.** Build it from `ODR/` in the solution. Switch its missing `MySql.Data.dll` HintPath to MySqlConnector and have OpenDental copy it to the output. Then delete the binary. |
| `PasswordVaultWrapper.dll` | 6 KB | Open Dental Software, PasswordVaultWrapper | GPL-2.0. The source is in this repo at `PasswordVaultWrapper/`. | Open source | None | No | None. A stale prebuilt copy: `OpenDentBusiness` uses a ProjectReference to the in-repo project. | **Remove: unused.** |
| `Sparks3D/readme.txt` | 2 KB | Open Dental Software, Sparks3D license notice | License notice for the two Sparks3D binaries | Open Dental | None | No (the same text is embedded as `OpenDental/Resources/Sparks3D.txt`) | Shown in the license list (`FormLicense.cs`). | **Remove with a cut feature.** Remove with Sparks3D. Also drop `Resources/Sparks3D.txt` from the license form. |
| `Sparks3D/Sparks3D.dll` | 1,092 KB | Open Dental Software / Jordan Sparks, Sparks3D | Custom binary-only license in `Sparks3D/readme.txt`: no SaaS use, thin-client use only with an Open Dental support plan, no reverse engineering. Not open source. | Open Dental | HintPath: OpenDentBusiness, OpenDental, UnitTests, WpfControlsOD | Yes. Loaded from the `Sparks3D\` subfolder of the app folder. `ControlChart.cs:4908` checks that the file exists and sets `ToothChartRelay.IsSparks3DPresent`. Only `WpfControlsOD/UI/ToothChart/Sparks3DInterface.cs` and `OpenDentBusiness/SheetFramework/ToothChartHelper.cs` use its types. | DirectX 11 3D tooth chart. | **In-repo replacement.** Remove. Fall back to the in-repo `SparksToothChart` renderers (DirectX 9 via SharpDX, OpenGL via Tao), or write a new Direct3D 11 chart in-repo (for example on Vortice.Windows, MIT). Four projects reference it at compile time. |
| `Sparks3D/Sparks3DXEngine.dll` | 62 KB | Open Dental Software, Sparks3D native engine | Same custom license as Sparks3D.dll | Open Dental | None | Yes, loaded by `Sparks3D.dll`. Native x86. Imports d3d11, d2d1, DWrite and VCRUNTIME140. | Rendering engine behind Sparks3D. | **Remove with a cut feature.** Remove together with Sparks3D.dll. |
| `VBbridges.dll` | 16 KB | Open Dental Software, VBbridges 22.1 (VB.NET helper) | Proprietary, source not published | Open Dental | HintPath: OpenDental, WpfControlsOD, xCodeBase | No | Vendor bridges: Dr. Ceph (`CodeBase/Utilities/DrCephArgs.cs`), eClinicalWorks Rx form (`ControlChart.cs`, `FormRxManage.cs`), Patterson (`WpfControlsOD/Bridges/Patterson.cs`), Schick 3 (`WpfControlsOD/Bridges/Schick.cs`). | **In-repo replacement.** Re-implement the four entry points in C# in-repo (late-bound COM or command-line calls to the vendor programs), then delete. These are vendor integrations, so they are kept. |
| `VideoCamLib.dll` | 861 KB | Open Dental Software, VideoCamLib (native C++ Media Foundation wrapper) | Proprietary, source not published. According to the code comments, it was derived from the MIT-licensed Tanta samples. ([source](https://github.com/OfItselfSo/Tanta)) | Open Dental | None | Yes. `[DllImport("VideoCamLib.dll")]` in `OpenDental/UI/VideoCamera/VideoCamLib.cs`. Native x86. Imports only ole32, OLEAUT32 and KERNEL32. | Intraoral and video camera capture (`FormVideo.cs`, `UI/VideoCamera/CameraMethods.cs`). | **In-repo replacement.** Re-implement in-repo: a small C++ project built from the MIT Tanta samples, or managed Media Foundation through MF.Net (LGPL-2.1) or DirectShowLib (LGPL-2.1). |
| `xImageDeviceManager.dll` | 10,186 KB | Open Dental Software, xImageDeviceManager (Suni sensor capture) | Proprietary, source not published | Open Dental | HintPath: OpenDental | No. It presumably loads `SuniUSB.dll` (not verified, since that would mean inspecting the binary). | Direct Suni X-ray sensor capture in `ControlImagesOld.cs`. That control is always hosted by `FormImages` and creates `SuniDeviceControl` in its constructor, regardless of the `ImagesModuleUsesOld2020` pref. | **In-repo replacement.** Owner decision: remove it with `ControlImagesOld` (the current Imaging module uses TWAIN), or re-implement Suni capture in-repo from Suni's own SDK documentation. |

### Commercial and proprietary third-party binaries

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `DpsPos.dll` | 330 KB | EDI Health Group (DentalXChange), PayConnect POS module | Proprietary ("All Rights Reserved") | Commercial | HintPath: OpenDental, WpfControlsOD | No. It needs `DpsPos.dll.config`, which `FormPayConnect.cs:67` writes from an embedded resource. That config in turn loads log4net by name. | PayConnect card-terminal payments (`FormPayConnect.cs`, `FormPayment.cs`, `WpfControlsOD/Bridges/PayConnectTerminal.cs`). | **Remove from repo: optional plugin that binds to the vendor-installed DLL.** Keep the PayConnect terminal integration, but move it into an optional plugin assembly built from our source that loads the `DpsPos.dll` DentalXChange supplies at install time. Alternatively, re-implement terminal processing against DentalXChange's published API. Until then it blocks a binary-free repo. |
| `EZT4Jpeg.dll` | 268 KB | Atalasoft, EZTwain Pro JPEG/TIFF/PDF module | Commercial (part of EZTwain Pro) ([source](https://www.eztwain.com/)) | Commercial | None | Yes, loaded by EZTwain. Native x86. | JPEG and TIFF encoding for EZTwain. | **Replaced elsewhere.** Delete with Eztwain4.dll. |
| `EZT4Pdf.dll` | 388 KB | Atalasoft, EZTwain Pro PDF module | Commercial (part of EZTwain Pro) ([source](https://www.eztwain.com/)) | Commercial | None | Yes, loaded by EZTwain. Native x86. Imports EZT4Jpeg.dll. | PDF output for EZTwain. | **Replaced elsewhere.** Delete with Eztwain4.dll. |
| `Eztwain4.dll` | 964 KB | Atalasoft (Dosadi), EZTwain Pro 4 | Commercial, per-developer license ([source](https://www.eztwain.com/)) | Commercial | None | Yes, loaded by `ImagingDeviceManager.dll`. Native x86. The code shows "EzTwain4.dll not found" when it is missing (`ControlImages.cs:2627`). | TWAIN scanner and sensor acquisition. | **Replaced elsewhere.** NTwain (MIT) replaces it. Delete it when ImagingDeviceManager goes. |
| `SigPlusNET.dll` | 271 KB | Topaz Systems, SigPlusNET 2.1.5 | Proprietary Topaz SDK. Redistribution terms are not published. ([source](https://topazsystems.com/license.htm)) | Commercial | HintPath: OpenDentBusiness, OpenDental, UnitTests, xCodeBase | No (comments only) | Topaz signature pads (`OpenDentBusiness/UI/TopazWrapper.cs`, `SignatureBoxWrapper.cs`, Imaging module, treatment plans). | **Remove from repo: optional plugin that binds to the vendor-installed DLL.** Keep the Topaz integration, but stop shipping the binary: move the Topaz code into an optional plugin assembly that binds at runtime to the `SigPlusNET.dll` installed by Topaz's own SDK installer. Until then it blocks a binary-free repo. |
| `SuniUSB.dll` | 84 KB | Suni Imaging Microsystems, SuniRay sensor driver 2.5 | Proprietary | Commercial | None | Presumably loaded by `xImageDeviceManager.dll`. Native x86. Named only in a comment (`WpfControlsOD/Bridges/Guru.cs:61`). | Direct Suni sensor capture (`ControlImagesOld`, always created by `FormImages`). | **Remove with a cut feature.** Remove with xImageDeviceManager. Suni users install Suni's own driver or use TWAIN. |

### COM interop assemblies

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `AxInterop.AcroPDFLib.dll` | 9 KB | Generated ActiveX wrapper for Adobe Reader | Generated from a proprietary type library | Commercial | HintPath: OpenDental | No. Referenced by `OpenDental.csproj`, but no code uses it and OpenDental.exe has no reference to it. | None | **Remove: unused.** Delete. Remove the Reference from `OpenDental.csproj` first. |
| `AxInterop.WMPLib.dll` | 52 KB | Generated ActiveX wrapper for Windows Media Player | tlbimp output for a Microsoft type library. Redistribution terms unclear. | Unknown | HintPath: OpenDental | No | Same HQ voicemail form | **Remove with a cut feature.** Delete with InternalTools (Phase 3). |
| `Interop.Acrobat.dll` | 36 KB | Generated from the Adobe Acrobat type library | Generated from a proprietary type library | Commercial | HintPath: OpenDental | No. `EmbedInteropTypes=True`, and no code uses it. | None | **Remove: unused.** Delete. Remove the Reference from `OpenDental.csproj` first. |
| `Interop.AcroPDFLib.dll` | 12 KB | Generated from the Adobe Reader ActiveX type library | Generated from a proprietary type library | Commercial | None | No | None (only needed by AxInterop.AcroPDFLib.dll) | **Remove: unused.** Delete with AxInterop.AcroPDFLib.dll. |
| `Interop.AFORMAUTLib.dll` | 12 KB | Generated from the Adobe Acrobat Forms type library | Generated from a proprietary type library | Commercial | HintPath: OpenDental; Not built: PluginExample | No. `EmbedInteropTypes=True`, and no code uses it. | None (also referenced by the unbuilt `PluginExample`) | **Remove: unused.** Delete. Remove the References from `OpenDental.csproj` and `PluginExample.csproj` first. |
| `Interop.CDRDATALib.dll` | 48 KB | Generated from the Schick CDR (Sirona/Dentsply) type library | Generated from a proprietary type library; no redistribution grant | Commercial | HintPath: OpenDental, WpfControlsOD | No. `EmbedInteropTypes=True`, so it is needed at compile time only. | Schick CDR imaging bridge (`WpfControlsOD/Bridges/Schick.cs`). | **In-repo replacement.** Replace with late binding (`dynamic`) or a few hand-written `[ComImport]` interfaces in `Schick.cs`, then delete. |
| `Interop.CDRDicom.dll` | 32 KB | Generated from the Schick CDR DICOM type library | Generated from a proprietary type library; no redistribution grant | Commercial | HintPath: OpenDental, WpfControlsOD | No. `EmbedInteropTypes=True`, compile time only. | Schick CDR imaging bridge (`WpfControlsOD/Bridges/Schick.cs`). | **In-repo replacement.** Same as Interop.CDRDATALib.dll. |
| `Interop.Microsoft.Office.Core.dll` | 192 KB | Generated from the Microsoft Office type library | tlbimp output for a Microsoft type library. Redistribution terms unclear. | Unknown | HintPath: OpenDental | No | Needed by Interop.Word.dll. `using Microsoft.Office.Core` in `FormApptsOther.cs` is unused. | **In-repo replacement.** Delete when Word uses late binding. |
| `Interop.QBFC10.dll` | 854 KB | Intuit QuickBooks Desktop SDK (QBFC 10) interop, signed by Intuit | Part of the proprietary QuickBooks SDK | Commercial | HintPath: OpenDental, WpfControlsOD | No. `EmbedInteropTypes=True`, compile time only. | QuickBooks Desktop deposit export (`WpfControlsOD/Bridges/QuickBooks.cs`). This is QuickBooks Desktop, not QuickBooks Online. | **In-repo replacement.** Replace with late binding to the `QBFC10.QBSessionManager` COM class, which QuickBooks installs on the workstation. Then delete. |
| `Interop.VBIDE.dll` | 56 KB | Generated from the VBA extensibility type library | tlbimp output for a Microsoft type library. Redistribution terms unclear. | Unknown | None | No | Needed by Interop.Word.dll | **In-repo replacement.** Delete when Word uses late binding. |
| `Interop.WMPLib.dll` | 323 KB | Generated from the Windows Media Player type library | tlbimp output for a Microsoft type library. Redistribution terms unclear. | Unknown | None | No | Voicemail player in HQ-only `OpenDental/InternalTools/Phones/FormVoiceMails.cs`. `OpenDental.csproj` also has `COMReference` items that generate the same interop at build time. | **Remove with a cut feature.** Delete with InternalTools (Phase 3). |
| `Interop.Word.dll` | 572 KB | Generated from the Microsoft Word type library (not Microsoft's official PIA) | tlbimp output for a Microsoft type library. Unsigned. Redistribution terms unclear (Microsoft ships official PIAs for this). | Unknown | HintPath: OpenDentBusiness, OpenDental, UnitTests | No | Word letter merge (`OpenDental/Forms/FormLetterMerges.cs`, `FormLetterMergeEdit.cs`, `OpenDentBusiness/Data Interface/LetterMerges.cs`). | **In-repo replacement.** Replace with late binding (`Type.GetTypeFromProgID("Word.Application")` plus `dynamic`) in-repo. That also removes Interop.Microsoft.Office.Core, Interop.VBIDE and stdole. |
| `stdole.dll` | 16 KB | Microsoft OLE Automation primary interop assembly (stdole 7.0.3300.0). Version info says Visual Studio .NET, file version 7.00.9466. | No license found that lets us ship this loose file. Microsoft lists stdole 7.0.3300.0 among the PIAs included with Office 2003, and says the only supported way to redistribute it is Microsoft's Office PIA redistributable installer ([source](https://learn.microsoft.com/en-us/previous-versions/office/developer/office-2003/aa159923(v=office.11))). The NuGet `stdole` packages grant no redistribution right: 17.x is under the Visual Studio Add-ons and Extensions terms, which forbid bundling it with applications for others ([source](https://www.nuget.org/packages/stdole/17.14.40260/License)), and 7.0.3300 links to a Visual Studio 2017 pre-release license ([source](https://www.nuget.org/packages/stdole/7.0.3300)). The text the app shows for it, `OpenDental/Resources/stdole.txt`, is a Visual Studio 2015 pre-release license that allows evaluation use only. | Unknown | None directly. The build copies it into `OpenDental\bin\Debug` (and the other outputs downstream of OpenDentBusiness) as a dependency of the copy-local Interop.Word.dll and Interop.Microsoft.Office.Core.dll References. | No | Needed by Interop.Microsoft.Office.Core.dll (Word letter merge). | **In-repo replacement.** Delete when Word uses late binding. Do not swap in a NuGet `stdole` package. Until the late-binding change lands, a release must not ship this loose copy: users who run Word letter merge get stdole from Office itself or from Microsoft's Office PIA redistributable installer, and only that way. |

### Microsoft runtime, SDK and platform files

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `installutil.exe` | 27 KB | Microsoft .NET Framework 4 InstallUtil | Microsoft .NET Framework component with no redistribution grant. Every machine with .NET Framework already has it. ([source](https://learn.microsoft.com/en-us/dotnet/framework/tools/installutil-exe-installer-tool)) | Microsoft | HintPath: ServiceManager. No ServiceManager code uses it, but the default CopyLocal copies it into `ServiceManager\bin\Debug`. | Yes. `CodeBase/Utilities/ServicesHelper.cs` launches `InstallUtil\installutil.exe` from the app folder as a process. | Installing and removing Windows services: `ServiceManager`, plus the eConnector, API and OpenDentalService installs in `OpenDental/Logic/PrefL.cs`. | **In-repo replacement.** A blocker, because the build ships it. Interim: add `<Private>False</Private>` to the `installutil` Reference in `ServiceManager.csproj`, or delete that Reference. Then call `System.Configuration.Install.ManagedInstallerClass.InstallHelper`, or the framework's own `InstallUtil.exe` under `%WINDIR%\Microsoft.NET\Framework\v4.0.30319`, and delete the file. |
| `Microsoft.DirectX.Direct3D.dll` | 462 KB | Microsoft Managed DirectX 1.1 Direct3D | DirectX SDK redistributable | Microsoft | Not built: TestToothChart | No | Only `TestToothChart.csproj`, which is not in the solution. | **Remove: unused.** Delete (unused), or delete TestToothChart with it. |
| `Microsoft.DirectX.Direct3DX.dll` | 565 KB | Microsoft Managed DirectX 1.1 D3DX | DirectX SDK redistributable | Microsoft | None | No | None. No project references it. | **Remove: unused.** |
| `Microsoft.DirectX.dll` | 218 KB | Microsoft Managed DirectX 1.1 (DirectX SDK, deprecated) | DirectX SDK redistributable | Microsoft | None | No | None. No project references it. | **Remove: unused.** |
| `Microsoft.Web.WebView2.Core.dll` | 550 KB | Microsoft Edge WebView2 SDK 1.0.2420.47 | Microsoft WebView2 SDK license (BSD-3-Clause style) ([source](https://www.nuget.org/packages/Microsoft.Web.WebView2)) | Microsoft | HintPath: OpenDental, WpfControlsOD, xCodeBase | No | Embedded browser (`CodeBase/Controls/ODWebView2.cs`, `WpfControlsOD` WebBrowser). | **NuGet.** NuGet `Microsoft.Web.WebView2`. |
| `Microsoft.Web.WebView2.WinForms.dll` | 37 KB | Microsoft Edge WebView2 SDK 1.0.2420.47 | Microsoft WebView2 SDK license (BSD-3-Clause style) ([source](https://www.nuget.org/packages/Microsoft.Web.WebView2)) | Microsoft | HintPath: OpenDental, xCodeBase | No | WinForms WebView2 control | **NuGet.** NuGet `Microsoft.Web.WebView2`. |
| `Microsoft.Web.WebView2.Wpf.dll` | 45 KB | Microsoft Edge WebView2 SDK 1.0.2420.47 | Microsoft WebView2 SDK license (BSD-3-Clause style) ([source](https://www.nuget.org/packages/Microsoft.Web.WebView2)) | Microsoft | HintPath: WpfControlsOD | No | WPF WebView2 control | **NuGet.** NuGet `Microsoft.Web.WebView2`. |
| `MSVCR71.dll` | 340 KB | Microsoft Visual C++ .NET 2003 C runtime 7.10 | Microsoft redistributable (VS .NET 2003 redist) | Microsoft | None | No. No binary in the repo imports it and no code names it. | None found | **Remove: unused.** |
| `System.Net.Http.dll` | 193 KB | Microsoft System.Net.Http 4.3.x NuGet build | Microsoft .NET Library license (2016-era package) ([source](https://www.nuget.org/packages/System.Net.Http/4.3.4)) | Microsoft | None | No | None. No project references it, and the framework's System.Net.Http is used. | **Remove: unused.** |
| `vcruntime140.dll` | 82 KB | Microsoft Visual C++ 2015-2022 runtime 14.27 (x86) | Microsoft redistributable (Visual Studio redist list) ([source](https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files)) | Microsoft | None | Yes, as an import of `Dicom.Native.dll`, `Sparks3D/Sparks3DXEngine.dll` and Release builds of `Direct2dWrapper.dll`. Not copied by the build. | C runtime for native and mixed-mode DLLs | **Remove: install the redistributable.** Do not keep it in the repo. The installer should chain `vc_redist.x86.exe`, which Microsoft recommends, and which also supplies `MSVCP140.dll`. Dicom.Native.dll needs that file and it is not in `Required dlls`. |
| `WebView2Loader.dll` | 114 KB | Microsoft Edge WebView2 SDK native loader 1.0.2420.47 (x86) | Microsoft WebView2 SDK license (BSD-3-Clause style) ([source](https://www.nuget.org/packages/Microsoft.Web.WebView2)) | Microsoft | None | Yes, loaded by `Microsoft.Web.WebView2.Core.dll`. `CodeBase/Controls/ODWebView2.cs:42` warns when it is missing. Not copied by the build. | Embedded Edge browser (help, eServices pages, web bridges). | **NuGet.** Replace with NuGet `Microsoft.Web.WebView2`, which ships the loader under `runtimes/win-x86/native` and copies it. |
| `Windows.winmd` | 3,267 KB | Microsoft Windows SDK union metadata | Windows SDK reference metadata. Not a runtime redistributable: Windows supplies the system metadata at runtime ([source](https://learn.microsoft.com/en-us/uwp/winrt-cref/winmd-files), [package](https://www.nuget.org/packages/Microsoft.Windows.SDK.Contracts)). | Microsoft | HintPath: PasswordVaultWrapper. The default CopyLocal copies it into `OpenDental\bin\Debug`. | No. A compile-time reference, but the build copies it to `bin` anyway. | `Windows.Security.Credentials.PasswordVault` in `PasswordVaultWrapper/WindowsPasswordVaultWrapper.cs` (stored database and bridge credentials). | **NuGet.** A blocker, because the build ships it. Interim: add `<Private>False</Private>` to the `Windows` Reference in `PasswordVaultWrapper.csproj`. Then replace it with NuGet `Microsoft.Windows.SDK.Contracts` (Windows SDK license), keeping that package's `.winmd` files out of the output folder too. Or use in-repo P/Invoke to the Credential Manager API, which needs no `.winmd`. If you switch APIs, plan a one-time migration of existing vault entries. |

### Open source: database, JSON and .NET support libraries

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `Microsoft.Bcl.AsyncInterfaces.dll` | 22 KB | Microsoft, Microsoft.Bcl.AsyncInterfaces | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | None | No | Dependency of System.Text.Json | **NuGet.** NuGet `Microsoft.Bcl.AsyncInterfaces` (MIT). |
| `Microsoft.DotNet.PlatformAbstractions.dll` | 23 KB | Microsoft, Microsoft.DotNet.PlatformAbstractions 2.0.4 | MIT ([source](https://github.com/dotnet/core-setup)) | Open source | None | No | None. It belongs to Microsoft.Extensions.DependencyModel, which Serilog.Settings.Configuration wants but which is not here. No project references it. | **Remove: unused.** Delete (unused). The NuGet Intuit SDK brings the correct set. |
| `Microsoft.Extensions.Logging.Abstractions.dll` | 65 KB | Microsoft, Microsoft.Extensions.Logging.Abstractions | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of MySqlConnector | **NuGet.** NuGet `Microsoft.Extensions.Logging.Abstractions` (MIT). |
| `MySqlConnector.dll` | 802 KB | Bradley Grainger, MySqlConnector 2.3.7 | MIT ([source](https://github.com/mysql-net/MySqlConnector)) | Open source | HintPath: OpenDentBusiness, OpenDental, OpenDentalGraph, UnitTests, xCentralManager, xDataConnectionBase, xDatabaseIntegrityCheck; Not built: xDocumentationBuilder | No | The MySQL/MariaDB driver for the whole application. | **NuGet.** NuGet `MySqlConnector` (MIT). |
| `Newtonsoft.Json.dll` | 684 KB | James Newton-King, Json.NET 12.0.3 | MIT ([source](https://github.com/JamesNK/Newtonsoft.Json)) | Open source | HintPath: OpenDentBusiness, OpenDental, OpenDentalGraph, UnitTests, UnitTestsCore, WpfControlsOD, xCodeBase | Yes. `ProgramEntry.cs:139` force-loads it by path (`ODInitialize.FixPackageAssembly`) to work around binding conflicts. | JSON everywhere | **NuGet.** NuGet `Newtonsoft.Json` (MIT). Binding redirects replace the force-load hack. |
| `System.Buffers.dll` | 20 KB | Microsoft, System.Buffers | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of MySqlConnector and System.Memory | **NuGet.** NuGet `System.Buffers` (MIT). |
| `System.Diagnostics.DiagnosticSource.dll` | 169 KB | Microsoft, System.Diagnostics.DiagnosticSource | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of MySqlConnector and Azure.Core | **NuGet.** NuGet `System.Diagnostics.DiagnosticSource` (MIT). |
| `System.Memory.dll` | 139 KB | Microsoft, System.Memory | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of MySqlConnector, System.Text.Json and Azure.Core | **NuGet.** NuGet `System.Memory` (MIT). |
| `System.Net.Http.WinHttpHandler.dll` | 136 KB | Microsoft, System.Net.Http.WinHttpHandler | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: OpenDental | No | Dependency of Microsoft.Graph.Core | **NuGet.** NuGet `System.Net.Http.WinHttpHandler` (MIT). |
| `System.Numerics.Vectors.dll` | 113 KB | Microsoft, System.Numerics.Vectors | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of System.Memory | **NuGet.** NuGet `System.Numerics.Vectors` (MIT). |
| `System.Runtime.CompilerServices.Unsafe.dll` | 18 KB | Microsoft, System.Runtime.CompilerServices.Unsafe | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of System.Memory and System.Text.Json | **NuGet.** NuGet `System.Runtime.CompilerServices.Unsafe` (MIT). |
| `System.Text.Encodings.Web.dll` | 75 KB | Microsoft, System.Text.Encodings.Web | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | None | No | Dependency of System.Text.Json | **NuGet.** NuGet `System.Text.Encodings.Web` (MIT). |
| `System.Text.Json.dll` | 502 KB | Microsoft, System.Text.Json | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: OpenDental | No | PayConnect iFrame JSON (`FormPayConnect2iFrame.cs`). Also a dependency of Azure.Core. | **NuGet.** NuGet `System.Text.Json` (MIT). |
| `System.Threading.Tasks.Extensions.dll` | 25 KB | Microsoft, System.Threading.Tasks.Extensions | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | HintPath: xDataConnectionBase | No | Dependency of MySqlConnector and Microsoft.Graph.Core | **NuGet.** NuGet `System.Threading.Tasks.Extensions` (MIT). |
| `System.ValueTuple.dll` | 25 KB | Microsoft, System.ValueTuple | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | None | No | Dependency of System.Text.Json and Microsoft.Graph.Core | **NuGet.** NuGet `System.ValueTuple` (MIT). |

### Open source: email, identity and cloud SDKs

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `Avalara.AvaTax.RestClient.net45.dll` | 758 KB | Avalara, AvaTax REST v2 SDK 20.1 | Apache-2.0 ([source](https://github.com/avadev/AvaTax-REST-V2-DotNet-SDK)) | Open source | HintPath: OpenDentBusiness | No | Sales tax. Only active at Open Dental HQ: `AvaTax.IsEnabled()` requires `PrefC.IsODHQ` (`OpenDentBusiness/WebBridges/AvaTax.cs:30`). | **Remove with a cut feature.** Remove with the HQ-only code (pin `IsODHQ` false, then delete `AvaTax.cs` and `FormAvaTax`). |
| `AWSSDK.Core.dll` | 1,952 KB | Amazon, AWS SDK for .NET 3.7 | Apache-2.0 ([source](https://github.com/aws/aws-sdk-net)) | Open source | HintPath: OpenDental | No | Open Dental Cloud (AWS AppStream) user admin (`OpenDental/Forms/FormCloudUsers.cs`, `FormCloudUserEdit.cs`). This is an Open Dental-hosted service. | **Remove with a cut feature.** Remove with the ODCloud/AppStream code paths (Phase 3). |
| `AWSSDK.IdentityStore.dll` | 120 KB | Amazon, AWS SDK for .NET 3.7 (IdentityStore) | Apache-2.0 ([source](https://github.com/aws/aws-sdk-net)) | Open source | HintPath: OpenDental | No | Same ODCloud user admin | **Remove with a cut feature.** Remove with the ODCloud/AppStream code paths. |
| `Azure.Core.dll` | 323 KB | Microsoft Azure SDK, Azure.Core 1.25 | MIT ([source](https://github.com/Azure/azure-sdk-for-net)) | Open source | None | No | Dependency of Microsoft.Graph and Graph.Core (Microsoft 365 email) | **NuGet.** Transitive from NuGet `Microsoft.Graph`. |
| `Dropbox.Api.dll` | 2,156 KB | Dropbox, Dropbox.Api 4.0 | MIT ([source](https://github.com/dropbox/dropbox-sdk-dotnet)) | Open source | HintPath: OpenDentalCloud | No (comment in `ProgramEntry.cs:137`) | Dropbox image storage (`OpenDentalCloud/Storage/Dropbox.cs`). | **NuGet.** NuGet `Dropbox.Api` (MIT). Our own Dropbox app key is needed. |
| `Google.Apis.Auth.dll` | 116 KB | Google, Google APIs Client Library for .NET 1.44 | Apache-2.0 ([source](https://github.com/googleapis/google-api-dotnet-client)) | Open source | HintPath: OpenDentBusiness | No | Gmail OAuth email (`EmailMessages.cs`, `WebBridges/Google/GoogleApiConnector.cs`). The `using` in `Bridges/Pearl/IPearlApiClient.cs` is unused. | **NuGet.** NuGet `Google.Apis.Gmail.v1` (Apache-2.0), which pulls in the others. |
| `Google.Apis.Auth.PlatformServices.dll` | 5 KB | Google, Google APIs Client Library for .NET 1.44 (platform shim) | Apache-2.0 ([source](https://github.com/googleapis/google-api-dotnet-client)) | Open source | HintPath: OpenDentBusiness | Not by name. No compiled assembly in the build references it, but it ships in the Google.Apis 1.44 net45 package. | Gmail (package companion) | **NuGet.** Let NuGet `Google.Apis` supply it if needed. Do not keep it separately. |
| `Google.Apis.Core.dll` | 71 KB | Google, Google APIs Client Library for .NET 1.44 | Apache-2.0 ([source](https://github.com/googleapis/google-api-dotnet-client)) | Open source | HintPath: OpenDentBusiness | No | Gmail OAuth email (`EmailMessages.cs`, `WebBridges/Google/GoogleApiConnector.cs`). The `using` in `Bridges/Pearl/IPearlApiClient.cs` is unused. | **NuGet.** NuGet `Google.Apis.Gmail.v1` (Apache-2.0), which pulls in the others. |
| `Google.Apis.dll` | 76 KB | Google, Google APIs Client Library for .NET 1.44 | Apache-2.0 ([source](https://github.com/googleapis/google-api-dotnet-client)) | Open source | HintPath: OpenDentBusiness | No | Gmail OAuth email (`EmailMessages.cs`, `WebBridges/Google/GoogleApiConnector.cs`). The `using` in `Bridges/Pearl/IPearlApiClient.cs` is unused. | **NuGet.** NuGet `Google.Apis.Gmail.v1` (Apache-2.0), which pulls in the others. |
| `Google.Apis.Gmail.v1.dll` | 104 KB | Google, Google APIs Client Library for .NET 1.44 | Apache-2.0 ([source](https://github.com/googleapis/google-api-dotnet-client)) | Open source | HintPath: OpenDentBusiness | No | Gmail OAuth email (`EmailMessages.cs`, `WebBridges/Google/GoogleApiConnector.cs`). The `using` in `Bridges/Pearl/IPearlApiClient.cs` is unused. | **NuGet.** NuGet `Google.Apis.Gmail.v1` (Apache-2.0), which pulls in the others. |
| `Google.Apis.PlatformServices.dll` | 4 KB | Google, Google APIs Client Library for .NET 1.44 (platform shim) | Apache-2.0 ([source](https://github.com/googleapis/google-api-dotnet-client)) | Open source | HintPath: OpenDentBusiness | Not by name. No compiled assembly in the build references it, but it ships in the Google.Apis 1.44 net45 package. | Gmail (package companion) | **NuGet.** Let NuGet `Google.Apis` supply it if needed. Do not keep it separately. |
| `Health.Direct.Agent.dll` | 52 KB | Direct Project .NET reference implementation 1.2 | BSD-3-Clause ([source](https://github.com/DirectProject/nhin-d-dotnet/blob/master/csharp/copyright_header.txt)) | Open source | HintPath: OpenDentBusiness | No | Direct secure messaging (EHR email encryption in `EmailMessages.cs`) | **Keep as is.** Keep as is (no maintained NuGet package). Remove with the MU/EHR features if Direct messaging goes. |
| `Health.Direct.Common.dll` | 274 KB | Direct Project .NET reference implementation 1.2 | BSD-3-Clause ([source](https://github.com/DirectProject/nhin-d-dotnet/blob/master/csharp/copyright_header.txt)) | Open source | HintPath: OpenDentBusiness, OpenDental, UnitTests | No | Direct messaging (`EmailMessages.cs`, `EmailPreviewControl.cs`). Many files also have stray `using Health.Direct.Common.Extensions;` lines. | **Keep as is.** Keep as is. Remove the stray `using` lines. |
| `Health.Direct.ResolverPlugins.dll` | 22 KB | Direct Project .NET reference implementation 1.2 | BSD-3-Clause ([source](https://github.com/DirectProject/nhin-d-dotnet/blob/master/csharp/copyright_header.txt)) | Open source | HintPath: OpenDentBusiness | No | Direct certificate resolution | **Keep as is.** Keep as is. |
| `MailKit.dll` | 766 KB | Jeffrey Stedfast / Xamarin, MailKit 2.4.1 | MIT ([source](https://github.com/jstedfast/MailKit)) | Open source | HintPath: OpenDentBusiness | No | SMTP sending (`OpenDentBusiness/Email/SendEmails.cs`, `EmailMessages.cs`) | **NuGet.** NuGet `MailKit` (MIT). |
| `Microsoft.Graph.Core.dll` | 172 KB | Microsoft Graph .NET Core 2.0.13 | MIT ([source](https://github.com/microsoftgraph/msgraph-sdk-dotnet-core)) | Open source | HintPath: OpenDentBusiness | No | Microsoft 365 email (Bridges) | **NuGet.** NuGet `Microsoft.Graph.Core` (MIT). |
| `Microsoft.Graph.dll` | 10,439 KB | Microsoft Graph .NET SDK 4.42 | MIT ([source](https://github.com/microsoftgraph/msgraph-sdk-dotnet)) | Open source | HintPath: OpenDentBusiness | No | Microsoft 365 email, used by Bridges.dll. The `using Microsoft.Graph` lines in `AutoCodes.cs`, `PatientNotes.cs` and `Patients.cs` are unused. | **NuGet.** NuGet `Microsoft.Graph` (MIT), referenced by the in-repo Bridges replacement. |
| `Microsoft.Identity.Client.dll` | 1,476 KB | Microsoft Authentication Library (MSAL.NET) 4.47.1 | MIT ([source](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet)) | Open source | None | No | Microsoft 365 OAuth (Bridges) | **NuGet.** NuGet `Microsoft.Identity.Client` (MIT). It will be configured with our own Azure app registration. |
| `Microsoft.IdentityModel.Abstractions.dll` | 18 KB | Microsoft IdentityModel 6.23.1 | MIT ([source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | Open source | None | No | Dependency of Microsoft.Graph.Core and MSAL (Microsoft 365 email) | **NuGet.** Transitive from NuGet `Microsoft.Graph`. |
| `Microsoft.IdentityModel.JsonWebTokens.dll` | 87 KB | Microsoft IdentityModel 6.23.1 | MIT ([source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | Open source | None | No. Nothing resolves it: its only consumer, System.IdentityModel.Tokens.Jwt, is missing from `Required dlls`. | None in the current build | **Remove: unused.** Delete. NuGet `Microsoft.Graph` brings the correct JWT pair. |
| `Microsoft.IdentityModel.Logging.dll` | 33 KB | Microsoft IdentityModel 6.23.1 | MIT ([source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | Open source | None | No | Dependency of Microsoft.Graph.Core and MSAL (Microsoft 365 email) | **NuGet.** Transitive from NuGet `Microsoft.Graph`. |
| `Microsoft.IdentityModel.Protocols.dll` | 37 KB | Microsoft IdentityModel 6.23.1 | MIT ([source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | Open source | None | No | Dependency of Microsoft.Graph.Core and MSAL (Microsoft 365 email) | **NuGet.** Transitive from NuGet `Microsoft.Graph`. |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect.dll` | 111 KB | Microsoft IdentityModel 6.23.1 | MIT ([source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | Open source | None | No | Dependency of Microsoft.Graph.Core and MSAL (Microsoft 365 email) | **NuGet.** Transitive from NuGet `Microsoft.Graph`. |
| `Microsoft.IdentityModel.Tokens.dll` | 962 KB | Microsoft IdentityModel 6.23.1 | MIT ([source](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | Open source | None | No | Dependency of Microsoft.Graph.Core and MSAL (Microsoft 365 email) | **NuGet.** Transitive from NuGet `Microsoft.Graph`. |
| `MimeKit.dll` | 911 KB | Jeffrey Stedfast / Xamarin, MimeKit 2.4.1 | MIT ([source](https://www.nuget.org/packages/MimeKit/2.4.1)) | Open source | HintPath: OpenDentBusiness | No | MIME messages for email. It needs `BouncyCastle.Crypto` 1.8.5, which is missing from `Required dlls` (see Other findings). | **NuGet.** NuGet `MimeKit` (MIT), which brings BouncyCastle (MIT). |
| `OpenPop.dll` | 68 KB | OpenPop.NET 2.0.5 | Public domain (Unlicense) ([source](https://www.nuget.org/packages/OpenPop.NET)) | Open source | HintPath: OpenDentBusiness | No | POP3 email retrieval (`EmailMessages.cs:960`) | **NuGet.** NuGet `OpenPop.NET` (public domain), or MailKit's POP3 client, which is already here. |
| `System.Memory.Data.dll` | 21 KB | Microsoft, System.Memory.Data 1.0.2 (Azure SDK) | MIT ([source](https://github.com/Azure/azure-sdk-for-net)) | Open source | None | No | Dependency of Azure.Core (Microsoft 365 email) | **NuGet.** NuGet `System.Memory.Data` (MIT), pulled in by `Azure.Core`. |

### Open source: QuickBooks Online SDK and its Serilog dependencies

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `Intuit.Ipp.Core.dll` | 68 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.Data.dll` | 344 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). `using Intuit.Ipp.Data` in `FormSmsTextMessaging.cs` and `FormMapAreaContainerEdit.cs` is unused. | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.DataService.dll` | 70 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.Diagnostics.dll` | 6 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.EntitlementService.dll` | 10 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.Exception.dll` | 20 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.GlobalTaxService.dll` | 15 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.OAuth2PlatformClient.dll` | 80 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.QueryFilter.dll` | 12 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.ReportService.dll` | 32 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.Retry.dll` | 6 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.Security.dll` | 6 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.Utility.dll` | 32 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `Intuit.Ipp.WebHooksService.dll` | 13 KB | Intuit, QuickBooks Online .NET SDK v3 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | HintPath: OpenDental | No | QuickBooks Online, used by Bridges.dll (`FormQuickBooksOnlineSetup.cs`). | **NuGet.** NuGet `IppDotNetSdkForQuickBooksApiV3` (Apache-2.0), referenced by the in-repo Bridges replacement. Kept vendor integration, with our own Intuit app credentials. |
| `IppDotNetSdkForQuickBooksApiV3.dll` | 4 KB | Intuit, QuickBooks Online SDK metapackage stub 14.4 | Apache-2.0 ([source](https://github.com/intuit/QuickBooks-V3-DotNET-SDK)) | Open source | None | No | None. An empty package assembly that no project references. | **Remove: unused.** Delete (unused). The NuGet package replaces it. |
| `Microsoft.Azure.Documents.Client.dll` | 1,711 KB | Microsoft Azure Cosmos DB (DocumentDB) SDK 2.4 | MIT (package deprecated) ([source](https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/2.4.0)) | Open source | None | No. It never resolves: Serilog.Sinks.AzureDocumentDB wants 2.1.0.0 and this file is 2.4.0.0, so the build never copies it. | None in practice | **Remove: unused.** |
| `Serilog.dll` | 124 KB | Serilog contributors, Serilog 2.9 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Core logger. A static dependency of Intuit.Ipp.OAuth2PlatformClient. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Enrichers.Environment.dll` | 6 KB | Serilog contributors, Serilog.Enrichers.Environment 2.1.3 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | NuGet dependency of the Intuit SDK. No compiled assembly references it. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Enrichers.Thread.dll` | 6 KB | Serilog contributors, Serilog.Enrichers.Thread 3.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | NuGet dependency of the Intuit SDK. No compiled assembly references it. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Extensions.Logging.dll` | 16 KB | Serilog contributors, Serilog.Extensions.Logging 3.0.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | NuGet dependency of the Intuit SDK. Its own dependencies (Microsoft.Extensions.Logging and DI abstractions) are missing. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Settings.Configuration.dll` | 46 KB | Serilog contributors, Serilog.Settings.Configuration 3.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | NuGet dependency of the Intuit SDK. Its own dependencies (Microsoft.Extensions.Configuration, DependencyModel) are missing. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.AzureDocumentDB.dll` | 28 KB | Serilog contributors, Serilog.Sinks.AzureDocumentDB 4.0 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | NuGet dependency of the Intuit SDK (net461 only). It needs Microsoft.Azure.Documents.Client 2.1, which does not resolve. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.Console.dll` | 32 KB | Serilog contributors, Serilog.Sinks.Console 3.1.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Static dependency of the Intuit SDK. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.Debug.dll` | 6 KB | Serilog contributors, Serilog.Sinks.Debug 1.0.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Static dependency of the Intuit SDK. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.File.dll` | 28 KB | Serilog contributors, Serilog.Sinks.File 4.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Static dependency of the Intuit SDK. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.Literate.dll` | 5 KB | Serilog contributors, Serilog.Sinks.Literate 3.0 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Static dependency of the Intuit SDK. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.RollingFile.dll` | 18 KB | Serilog contributors, Serilog.Sinks.RollingFile 3.3 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Static dependency of the Intuit SDK. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `Serilog.Sinks.Trace.dll` | 6 KB | Serilog contributors, Serilog.Sinks.Trace 2.1 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | Static dependency of the Intuit SDK. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |
| `SerilogTraceListener.dll` | 9 KB | Serilog contributors, SerilogTraceListener 3.2 | Apache-2.0 ([source](https://github.com/serilog/serilog)) | Open source | HintPath: OpenDental | No | NuGet dependency of the Intuit SDK. No compiled assembly references it. Only `OpenDental.csproj` references it, and no code uses Serilog. | **NuGet.** Let NuGet `IppDotNetSdkForQuickBooksApiV3` bring the Serilog packages it needs, then delete these copies and the 13 `OpenDental.csproj` References. |

### Open source: documents, imaging, UI and devices

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `Dicom.Core.dll` | 1,446 KB | fo-dicom contributors, fo-dicom 4.0.7 | MS-PL ([source](https://www.nuget.org/packages/fo-dicom.Desktop/4.0.7)) | Open source | HintPath: OpenDentBusiness, OpenDental | No | DICOM image import and rendering (`OpenDentBusiness/Imaging/BitmapDicom.cs`) | **NuGet.** NuGet `fo-dicom.Desktop` 4.0.8 (MS-PL). Later fo-dicom 5 (MS-PL), which needs code changes. |
| `Dicom.Native.dll` | 919 KB | fo-dicom contributors, fo-dicom 4.0.7 native codecs | MS-PL ([source](https://www.nuget.org/packages/fo-dicom.Desktop/4.0.7)) | Open source | HintPath: OpenDentBusiness, OpenDental | Yes, by fo-dicom's desktop codec support, not by our code. No compiled assembly references it. Mixed-mode C++/CLI, x86. Needs MSVCP140.dll and VCRUNTIME140.dll. | JPEG and JPEG 2000 DICOM codecs | **NuGet.** NuGet `fo-dicom.Desktop` (MS-PL) ships it. The VC++ redistributable is a prerequisite. |
| `freeglut.dll` | 236 KB | freeglut (native, x86) | X-Consortium (MIT-style) ([source](https://freeglut.sourceforge.net/)) | Open source | None | Only through Tao.FreeGlut, which nothing calls. Native x86. | None | **Remove: unused.** |
| `Hunspellx64.dll` | 578 KB | Hunspell native library 1.2 (NHunspell build, x64) | MPL-1.1 / LGPL / GPL tri-license ([source](https://www.nuget.org/packages/NHunspell)) | Open source | None | Yes, loaded by NHunspell.dll in 64-bit processes only. The x86 app never loads it. Native x64. | Spell check (64-bit hosts) | **NuGet.** Same as Hunspellx86.dll. |
| `Hunspellx86.dll` | 525 KB | Hunspell native library 1.2 (NHunspell build, x86) | MPL-1.1 / LGPL / GPL tri-license ([source](https://www.nuget.org/packages/NHunspell)) | Open source | None | Yes, loaded by NHunspell.dll. Debug builds of `ODtextBox.cs:202-203` copy it from `Required dlls`. Native x86. Imports only KERNEL32 (static CRT). | Spell check | **NuGet.** Comes with NuGet `NHunspell`. Not needed at all with `WeCantSpell.Hunspell`. |
| `Ionic.Zip.dll` | 434 KB | Dino Chiesa, DotNetZip 1.9.1.4 | MS-PL (plus BSD, zlib, Apache-2.0 for bundled parts) ([source](https://github.com/haf/DotNetZip.Semverd/blob/master/LICENSE)) | Open source | HintPath: OpenDentBusiness, OpenDental, UnitTests | No | Zip files: e-claims (WebMD, RAMQ, Emdeon), code-system and RxNorm imports, eRx/CDS setup, update packaging (`PrefL.cs`). | **In-repo replacement.** Replace with `System.IO.Compression` (in .NET Framework 4.5+, MIT source). DotNetZip is deprecated and its last NuGet release has a high-severity directory-traversal advisory (CVE-2024-48510). |
| `log4net.dll` | 264 KB | Apache Software Foundation, log4net 2.0.15 | Apache-2.0 ([source](https://github.com/apache/logging-log4net)) | Open source | HintPath: OpenDental | Yes. `DpsPos.dll.config` declares a `log4net` config section, and .NET loads `log4net` by name when DpsPos reads that config. No compiled assembly references it. | Logging inside the DentalXChange PayConnect module | **NuGet.** NuGet `log4net` (Apache-2.0) as long as DpsPos is used. |
| `MediaFoundation.dll` | 350 KB | MF.Net (MediaFoundation .NET) 3.1 | LGPL-2.1 or BSD (per its version info) ([source](https://sourceforge.net/projects/mfnet/)) | Open source | None | No | None. Nothing references it (a leftover from the video-capture work). | **Remove: unused.** |
| `MediaFoundation.Extension.dll` | 86 KB | MF.Net extension library 1.0 | LGPL-2.1 or BSD (per its version info) ([source](https://sourceforge.net/projects/mfnet/)) | Open source | None | No | None. Nothing references it. | **Remove: unused.** |
| `MigraDoc.DocumentObjectModel.dll` | 244 KB | empira Software, MigraDoc 1.2 | MIT ([source](https://github.com/empira/MigraDoc)) | Open source | HintPath: OpenDental; Not built: PluginExample | No | Printed and PDF documents (`OpenDental` reports, treatment plans, statements) | **Keep as is.** Keep as is. Later NuGet `PDFsharp-MigraDoc-GDI` (MIT). |
| `MigraDoc.Rendering.dll` | 132 KB | empira Software, MigraDoc 1.2 | MIT ([source](https://github.com/empira/MigraDoc)) | Open source | HintPath: OpenDental; Not built: PluginExample | No | MigraDoc rendering | **Keep as is.** Keep as is. Later NuGet `PDFsharp-MigraDoc-GDI` (MIT). |
| `MigraDoc.RtfRendering.dll` | 80 KB | empira Software, MigraDoc 1.2 RTF | MIT ([source](https://github.com/empira/MigraDoc)) | Open source | HintPath: OpenDental; Not built: PluginExample | No. No compiled assembly references it. | None | **Remove: unused.** Delete. Remove the References from `OpenDental.csproj` and `PluginExample.csproj` first. |
| `NDde.dll` | 84 KB | Brian Gideon, NDde 1.03 | NDde Shared Source License (permissive: use, modify and redistribute with notices kept; text in `OpenDental/Resources/NDde.txt`) ([source](https://www.nuget.org/packages/NDde)) | Open source | HintPath: OpenDental, WpfControlsOD | No | DDE bridges to imaging programs (`WpfControlsOD/Bridges/DentX.cs`, `DexisIntegrator.cs`, `Vipersoft.cs`). | **Keep as is.** Keep as is. NuGet `NDde` 2.1 exists but is unmaintained. |
| `NHunspell.dll` | 28 KB | maierhofer software, NHunspell 0.9.6 | LGPL / MPL / GPL tri-license ([source](https://www.nuget.org/packages/NHunspell)) | Open source | HintPath: OpenDental, WpfControlsOD | Yes. It loads `Hunspellx86.dll` or `Hunspellx64.dll` by name at runtime. | Spell check (`OpenDental/User Controls/ODtextBox.cs`, `WpfControlsOD` TextRich, TreeView, WebBrowser) | **NuGet.** NuGet `NHunspell` 1.2 (same licenses), or better `WeCantSpell.Hunspell` (managed port, MPL/LGPL/GPL) to drop the native DLLs. |
| `PdfSharp.Charting.dll` | 92 KB | empira Software, PDFsharp Charting 1.2 | MIT ([source](https://github.com/empira/PDFsharp)) | Open source | HintPath: OpenDental, OpenDentalGraph; Not built: PluginExample | No | Dependency of MigraDoc.Rendering | **Keep as is.** Keep as is. Move together with PDFsharp/MigraDoc. |
| `PdfSharp.dll` | 524 KB | empira Software, PDFsharp 1.2 | MIT ([source](https://github.com/empira/PDFsharp)) | Open source | HintPath: OpenDentBusiness, OpenDental, OpenDentalGraph, UnitTests, WpfControlsOD; Not built: PluginExample | No | PDF generation (statements, sheets, reports, claim attachments) | **Keep as is.** Keep as is (MIT). Later move to NuGet `PDFsharp` 1.50 or 6.x (MIT), which is a behavior change that needs testing. |
| `RdlDesigner.exe` | 1,008 KB | fyiReporting RDL Project 2.9.3, designer | LGPL-2.1-or-later (source in `RdlDesign/`) | Open source | None | No (named only in comments in `FormReportForRdl.cs` and `FormReportCustom.cs`) | None | **Remove: unused.** Delete (unused). The source stays in `RdlDesign/` if anyone wants a designer. |
| `RdlEngine.dll` | 612 KB | fyiReporting RDL Project 2.9.3, engine | LGPL-2.1-or-later (source headers in `RdlEngine/` in this repo) | Open source | HintPath: OpenDental | No | RDL report rendering (`OpenDental/ReportingForRdl/`) | **In-repo replacement.** Build from the in-repo `RdlEngine/` sources (add to the solution and fix its MySql.Data reference), then delete the binary. |
| `RdlViewer.dll` | 56 KB | fyiReporting RDL Project 2.9.3, viewer | LGPL-2.1-or-later (source in `RdlViewer/`) | Open source | HintPath: OpenDental | No | RDL report viewer control | **In-repo replacement.** Build from the in-repo `RdlViewer/` sources, then delete the binary. |
| `RS232.dll` | 80 KB | "VB.Net RS232" serial class 1.18 (author not stated; probably Corrado Cavalli's public sample) | No license found | Unknown | HintPath: OpenDentBusiness | No | Dial-up modem connection for the BCBS Georgia clearinghouse (`OpenDentBusiness/Eclaims/TerminalConnector.cs`, `BCBSGA.cs`). | **In-repo replacement.** Rewrite `TerminalConnector` on `System.IO.Ports.SerialPort`. A commented-out C# port, `Eclaims/Rs232.cs`, is already in the repo. |
| `SharpDX.Direct3D9.dll` | 339 KB | Alexandre Mutel, SharpDX 4.0.1 | MIT (archived) ([source](https://github.com/sharpdx/SharpDX)) | Open source | HintPath: SparksToothChart, UnitTests | No | DirectX 9 tooth chart (`SparksToothChart/ToothChartDirectX.cs`) | **NuGet.** NuGet `SharpDX.Direct3D9` 4.0.1 (MIT). |
| `SharpDX.dll` | 270 KB | Alexandre Mutel, SharpDX 4.0.1 | MIT (project archived in 2019) ([source](https://github.com/sharpdx/SharpDX)) | Open source | HintPath: OpenDental, SparksToothChart | No | DirectX 9 tooth chart (`SparksToothChart`) and `ControlChart.cs` | **NuGet.** NuGet `SharpDX` 4.0.1 (MIT, same version). Long term: Vortice.Windows (MIT). |
| `SharpDX.Mathematics.dll` | 216 KB | Alexandre Mutel, SharpDX 4.0.1 | MIT (archived) ([source](https://github.com/sharpdx/SharpDX)) | Open source | HintPath: SparksToothChart, UnitTests | No | DirectX 9 tooth chart | **NuGet.** NuGet `SharpDX.Mathematics` 4.0.1 (MIT). |
| `Tao.FreeGlut.dll` | 40 KB | Tao Framework, FreeGLUT binding 2.4 | MIT-X11 | Open source | HintPath: SparksToothChart, xCodeBase | No. No compiled assembly references it, and no code uses `Glut`. | None | **Remove: unused.** Delete. Remove the References from `CodeBase/xCodeBase.csproj` and `SparksToothChart.csproj` first. |
| `Tao.OpenGl.dll` | 912 KB | Tao Framework 2.1, OpenGL binding | MIT-X11 (text in `OpenDental/Resources/Tao_OpenGL.txt`) | Open source | HintPath: OpenDental, SparksToothChart, xCodeBase | No | OpenGL tooth chart (`SparksToothChart`, `CodeBase/Controls/OpenGLWinFormsControl.cs`) | **Keep as is.** Keep as is (no maintained NuGet package). Long term: OpenTK or Silk.NET (MIT). |
| `Tao.Platform.Windows.dll` | 84 KB | Tao Framework, Windows platform binding | MIT-X11 | Open source | HintPath: OpenDental, SparksToothChart, UnitTests, xCodeBase | No | OpenGL context creation for the tooth chart | **Keep as is.** Keep as is. |
| `zxing.dll` | 436 KB | ZXing.Net 0.16.5 | Apache-2.0 ([source](https://github.com/micjahn/ZXing.Net)) | Open source | HintPath: OpenDental | No | QR codes for the mobile-app unlock code (`OpenDental/Forms/FormMobileCode.cs`). This is an eServices/mobile feature. | **NuGet.** NuGet `ZXing.Net` (Apache-2.0) while the feature exists. Remove it if the mobile/eClipboard code goes. |
| `zxing.presentation.dll` | 19 KB | ZXing.Net 0.16.5 WPF helpers | Apache-2.0 ([source](https://github.com/micjahn/ZXing.Net)) | Open source | HintPath: OpenDental | No. No compiled assembly references it. | None | **Remove: unused.** Delete. Remove the Reference from `OpenDental.csproj` first. |

### Open source: SSH and SFTP

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `DiffieHellman.dll` | 52 KB | Mentalis.org Diffie-Hellman library (distributed with SharpSSH) | Mentalis.org license (BSD-style). The binary has no version info, so this is inferred from its SharpSSH origin. ([source](https://sourceforge.net/projects/sharpssh/)) | Open source | None | No | Key exchange for SharpSSH | **Remove with a cut feature.** Delete with Tamir.SharpSSH. |
| `Org.Mentalis.Security.dll` | 180 KB | Mentalis.org Security Library 1.0.13 | Mentalis.org license (BSD-style, text in `OpenDental/Resources/Mentalis.txt`) | Open source | HintPath: OpenDental | No | Crypto for SharpSSH | **Remove with a cut feature.** Delete with Tamir.SharpSSH. |
| `Renci.SshNet.dll` | 836 KB | Renci, SSH.NET 2024.1 | MIT ([source](https://github.com/sshnet/SSH.NET)) | Open source | HintPath: OpenDentalCloud | No | SFTP for cloud image storage and TSI collections uploads (`OpenDentalCloud/Storage/Sftp.cs`) | **NuGet.** NuGet `SSH.NET` (MIT). |
| `Tamir.SharpSSH.dll` | 196 KB | Tamir Gal, SharpSSH 1.1.1.13 (a port of JSch) | BSD (JSch BSD text in `OpenDental/Resources/Tamir.txt`) ([source](https://sourceforge.net/projects/sharpssh/)) | Open source | HintPath: OpenDentBusiness, OpenDentHL7, OpenDental | No | SFTP for clearinghouses DentiCal, MercuryDE and EDS Medical (`OpenDentBusiness/Eclaims/*.cs`) and for HL7 (`OpenDentHL7/ServiceHL7.cs`). | **In-repo replacement.** Port these four call sites to SSH.NET, which is already here. That also removes DiffieHellman.dll and Org.Mentalis.Security.dll. SharpSSH was last updated in 2013. |

### Open source: ML.NET (unused)

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `Microsoft.ML.Core.dll` | 404 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.CpuMath.dll` | 60 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.Data.dll` | 1,522 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.DataView.dll` | 46 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.dll` | 14 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.FastTree.dll` | 466 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.KMeansClustering.dll` | 54 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.LightGbm.dll` | 86 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.PCA.dll` | 52 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.StandardTrainers.dll` | 316 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |
| `Microsoft.ML.Transforms.dll` | 744 KB | Microsoft, ML.NET 1.7.1 | MIT ([source](https://github.com/dotnet/machinelearning)) | Open source | None | No | None. Only `SlowQueryLogTool` (not in the solution) uses ML.NET, through its own `packages\` paths, not these copies. | **Remove: unused.** |

### Subfolders `.NET Core/` and `.NET Standard/`

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `.NET Core/AWSSDK.Core.dll` | 964 KB | Amazon, AWS SDK for .NET 3.3 (.NET Core build) | Apache-2.0 ([source](https://github.com/aws/aws-sdk-net)) | Open source | None | No | None. No project references the `.NET Core` folder. | **Remove: unused.** |
| `.NET Core/AWSSDK.EC2.dll` | 1,836 KB | Amazon, AWS SDK for .NET 3.3 (.NET Core build) | Apache-2.0 ([source](https://github.com/aws/aws-sdk-net)) | Open source | None | No | None. No project references the `.NET Core` folder. | **Remove: unused.** |
| `.NET Core/AWSSDK.S3.dll` | 536 KB | Amazon, AWS SDK for .NET 3.3 (.NET Core build) | Apache-2.0 ([source](https://github.com/aws/aws-sdk-net)) | Open source | None | No | None. No project references the `.NET Core` folder. | **Remove: unused.** |
| `.NET Standard/BouncyCastle.Crypto.dll` | 2,128 KB | Legion of the Bouncy Castle, Bouncy Castle for .NET 1.8 (file version 1.8.18099.1) | MIT-style Bouncy Castle License (text in `OpenDental/Resources/BouncyCastle.txt`) ([source](https://www.bouncycastle.org/licence.html)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/Google.Protobuf.dll` | 292 KB | Google, Protocol Buffers 3.6.1 | BSD-3-Clause ([source](https://github.com/protocolbuffers/protobuf)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/MySql.Data.dll` | 742 KB | Oracle, MySQL Connector/NET 8.0.17 | GPL-2.0 with Oracle Universal FOSS Exception ([source](https://www.nuget.org/packages/MySql.Data/8.0.17)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/Newtonsoft.Json.dll` | 677 KB | James Newton-King, Json.NET 12.0.3 (netstandard2.0) | MIT ([source](https://github.com/JamesNK/Newtonsoft.Json)) | Open source | None | No | None. No project references it, not even `xDataConnectionBaseStandard.csproj`. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/Renci.SshNet.dll` | 838 KB | Renci, SSH.NET 2024.1 (netstandard) | MIT ([source](https://github.com/sshnet/SSH.NET)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/SshNet.Security.Cryptography.dll` | 34 KB | Renci, SshNet.Security.Cryptography 1.2 | MIT ([source](https://github.com/sshnet/Cryptography)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/System.Configuration.ConfigurationManager.dll` | 372 KB | Microsoft, System.Configuration.ConfigurationManager 4.4.1 | MIT ([source](https://www.nuget.org/packages/System.Configuration.ConfigurationManager/4.4.1)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/System.Security.Cryptography.ProtectedData.dll` | 24 KB | Microsoft, System.Security.Cryptography.ProtectedData 4.4 | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/System.Security.Permissions.dll` | 92 KB | Microsoft, System.Security.Permissions 4.4.1 | MIT ([source](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)) | Open source | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |
| `.NET Standard/System.Xml.XPath.XmlDocument.dll` | 116 KB | Microsoft, System.Xml.XPath.XmlDocument 4.3 | Microsoft .NET Library license ([source](https://www.nuget.org/packages/System.Xml.XPath.XmlDocument/4.3.0)) | Microsoft | Not built: xDataConnectionBaseStandard | No | None in the build. Only `DataConnectionBase/xDataConnectionBaseStandard.csproj` references it. That project is not in `OpenDental.sln`, and it cannot build because two of its HintPaths (`Ubiety.Dns.Core.dll`, `System.Text.Encoding.CodePages.dll`) point to files that do not exist. | **Remove: unused.** Delete with the dead `xDataConnectionBaseStandard.csproj` (and `CodeBaseStandard/` if nothing else needs it). |

### `RequiredServerDlls/`

| File | Size | Vendor and product | License | Class | csproj references | Loaded at runtime by name | Feature | Recommended action |
|---|---:|---|---|---|---|---|---|---|
| `RequiredServerDlls/BatchCheckServiceStatus.bat` | 270 B | Open Dental Software, service helper script | GPL-2.0 (plain-text script in this repo) | Open source | None | No | Runs `net pause OpenDental` to show the service state for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). | **Remove: unused.** |
| `RequiredServerDlls/BatchInstallService.bat` | 130 B | Open Dental Software, service helper script | GPL-2.0 (plain-text script in this repo) | Open source | None | No | Runs `installutil.exe OpenDentServer.exe` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). | **Remove: unused.** |
| `RequiredServerDlls/BatchStartService.bat` | 81 B | Open Dental Software, service helper script | GPL-2.0 (plain-text script in this repo) | Open source | None | No | Runs `net start OpenDental` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). | **Remove: unused.** |
| `RequiredServerDlls/BatchStopService.bat` | 79 B | Open Dental Software, service helper script | GPL-2.0 (plain-text script in this repo) | Open source | None | No | Runs `net stop OpenDental` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). | **Remove: unused.** |
| `RequiredServerDlls/BatchUninstallService.bat` | 136 B | Open Dental Software, service helper script | GPL-2.0 (plain-text script in this repo) | Open source | None | No | Runs `installutil.exe /u OpenDentServer.exe` for the retired `OpenDentServer` Windows service. That project is not in this repo (only `OpenDentBusiness.Tests` points at the missing `..\OpenDentServer\OpenDentServer.csproj`). | **Remove: unused.** |
| `RequiredServerDlls/installutil.exe` | 28 KB | Microsoft .NET Framework 2.0 InstallUtil | Microsoft .NET Framework component ([source](https://learn.microsoft.com/en-us/dotnet/framework/tools/installutil-exe-installer-tool)) | Microsoft | None | No (only the scripts next to it call it) | Installing the retired OpenDentServer service | **Remove: unused.** |

## NuGet replacement map

The build already restores NuGet packages (`-restore -p:RestorePackagesConfig=true`). Reference the top-level package and let NuGet bring the dependencies, instead of committing each transitive DLL.

| Files in `Required dlls` today | NuGet package | License | Notes |
|---|---|---|---|
| `MySqlConnector.dll` | `MySqlConnector` | MIT | Same version available (2.3.7). |
| `Newtonsoft.Json.dll` | `Newtonsoft.Json` | MIT | Lets `ProgramEntry`'s force-load workaround go. |
| `System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`, `System.Threading.Tasks.Extensions`, `System.ValueTuple`, `System.Diagnostics.DiagnosticSource`, `System.Text.Json`, `System.Text.Encodings.Web`, `System.Net.Http.WinHttpHandler`, `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Extensions.Logging.Abstractions` | Same-named packages | MIT | Mostly transitive. Reference only what our code uses directly (`System.Text.Json`). |
| `Microsoft.Web.WebView2.*.dll`, `WebView2Loader.dll` | `Microsoft.Web.WebView2` | Microsoft BSD-3-Clause-style | Also fixes the missing loader in dev builds. |
| `Windows.winmd` | `Microsoft.Windows.SDK.Contracts` | Windows SDK license | The package's `.winmd` files are compile-time metadata that we may not redistribute, so they must stay out of the output folder: check `bin` after switching, and turn off copy-local on them if they appear. Or replace `PasswordVault` with Credential Manager P/Invoke in-repo, which needs no `.winmd`. |
| `MailKit.dll`, `MimeKit.dll` | `MailKit` (brings `MimeKit` and BouncyCastle) | MIT | Fixes the missing BouncyCastle dependency. |
| `OpenPop.dll` | `OpenPop.NET` | Public domain | Or use MailKit's POP3 client and drop it. |
| `Google.Apis*.dll` (6) | `Google.Apis.Gmail.v1` | Apache-2.0 | Gmail OAuth uses our own Google client ID. |
| `Microsoft.Graph*.dll`, `Microsoft.Identity.Client.dll`, `Microsoft.IdentityModel.*.dll`, `Azure.Core.dll`, `System.Memory.Data.dll` | `Microsoft.Graph`, `Microsoft.Identity.Client` | MIT | Referenced by the in-repo Bridges replacement (Microsoft 365 email) with our own Azure app registration. |
| `Intuit.Ipp.*.dll` (14), `Serilog*.dll` (13) | `IppDotNetSdkForQuickBooksApiV3` | Apache-2.0 | QuickBooks Online. The package brings the Serilog set it needs (Apache-2.0). |
| `Dropbox.Api.dll` | `Dropbox.Api` | MIT | |
| `Dicom.Core.dll`, `Dicom.Native.dll` | `fo-dicom.Desktop` | MS-PL | 4.0.8 is a drop-in. fo-dicom 5 needs code changes. |
| `zxing.dll` | `ZXing.Net` | Apache-2.0 | Only while the mobile-app QR feature exists. |
| `NHunspell.dll`, `Hunspellx86.dll`, `Hunspellx64.dll` | `NHunspell`, or `WeCantSpell.Hunspell` (managed) | LGPL / MPL / GPL | The managed option removes the native DLLs. |
| `SharpDX*.dll` (3) | `SharpDX`, `SharpDX.Direct3D9`, `SharpDX.Mathematics` 4.0.1 | MIT | The project is archived. Vortice.Windows (MIT) is the long-term option. |
| `log4net.dll` | `log4net` | Apache-2.0 | Only while DpsPos is used. |
| `Renci.SshNet.dll` | `SSH.NET` | MIT | Also the target for the SharpSSH call sites. |
| `Eztwain4.dll` and friends | `NTwain` | MIT | Being done elsewhere. |
| `PdfSharp*.dll`, `MigraDoc*.dll` (later) | `PDFsharp-MigraDoc-GDI` 1.50 | MIT | A behavior change from 1.2. Keep the MIT binaries until someone can test printing. |

Not on NuGet in a maintained form, so keep them as is: Tao Framework (MIT-X11), NDde (permissive), and the Direct Project libraries (BSD-3-Clause, buildable from source).

## Other findings

### Missing dependencies

The build's assembly resolution reports these references as unresolved. Nothing in `Required dlls` satisfies them:

| Wanted by | Missing assembly |
|---|---|
| `MimeKit.dll` 2.4.1 (and MailKit, which is built on it) | `BouncyCastle.Crypto` 1.8.5. The only copy, in `.NET Standard/`, is a different build and is not on the search path. |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect.dll` (part of the Microsoft Graph set) | `System.IdentityModel.Tokens.Jwt` |
| The Direct Project libraries (`Health.Direct.*`) | `Health.Direct.Config.Store`, `Health.Direct.Config.Client` |
| `Serilog.Extensions.Logging.dll`, `Serilog.Settings.Configuration.dll` | `Microsoft.Extensions.Logging`, `.DependencyInjection.Abstractions`, `.Configuration.Abstractions`, `.Configuration.Binder`, `.DependencyModel`, `.Primitives` |
| `Serilog.Sinks.AzureDocumentDB.dll` | `Microsoft.Azure.Documents.Client` 2.1 (2.4 is present but does not match) |

The build log names the assembly that is missing but not always which file asks for it, so the "Wanted by" column for the first three rows comes from the packages' published dependency lists (MimeKit, IdentityModel) or from the assembly names (Direct Project). Either the installer ships extra files, or those code paths never run in practice. NuGet package references make the full dependency set explicit.

Some HintPaths in projects outside the solution point to files that do not exist:

- `Required dlls\MySql.Data.dll`, referenced by `xODR`, `QueryExecutor` and `RdlEngine`. The only copy is under `.NET Standard/`.
- `.NET Standard\Ubiety.Dns.Core.dll`, `.NET Standard\System.Text.Encoding.CodePages.dll` and `.NET Standard\System.Runtime.CompilerServices.Unsafe.dll`, referenced by `xDataConnectionBaseStandard`.

`ODR` and `RdlEngine` need MySqlConnector before they can be built from source.

### HintPaths the replacement pieces must also update

`Directory.Build.targets` only swaps `ProjectReference`s. Several projects reference Open Dental DLLs directly by HintPath, so the pieces replacing those DLLs must edit these project files too:

| DLL | Projects with a direct HintPath |
|---|---|
| `CDT.dll` | `xCentralManager`, `xDatabaseIntegrityCheck`, `OpenDentHL7`, `ServiceManager`, `UnitTests` (and the unbuilt `xODR`, `xDataConnectionBaseStandard`) |
| `Bridges.dll` | `OpenDentBusiness`, `UnitTests`, `WpfControlsOD` |
| `ODCrypt.dll` | `OpenDental`, `OpenDentBusiness`, `UnitTests` (it has no ProjectReference at all) |
| `VirtualWeb.dll` | `OpenDentBusiness`, `OpenDentalGraph` |

### Third-party license notices

The in-app license list (`OpenDental/Forms/FormLicense.cs`, texts in `OpenDental/Resources/`) does not match what ships:

- The "Drifty" entry, shown for Ionic, is the MIT license of the Ionic web framework. `Ionic.Zip.dll` is DotNetZip, which is MS-PL.
- The "Stdole" entry shows `Resources/stdole.txt`, a Visual Studio 2015 pre-release license for evaluation use only. It does not license `stdole.dll` for redistribution.
- There are no entries for many libraries whose licenses ask for the license text or notices to travel with the binaries:
  - Apache-2.0: Google APIs, AWS SDK, Intuit SDK, Serilog, log4net, Avalara.
  - MS-PL: fo-dicom, DotNetZip.
  - LGPL: NHunspell, fyiReporting.
  - BSD-3-Clause: Direct Project.
  - MIT: MailKit, MimeKit, MySqlConnector, Microsoft Graph and MSAL.
- It still lists components that are going away: Sparks3D, CDT, the Open Dental API end-user license.

Recommendation: once packages come from NuGet, generate a `THIRD-PARTY-NOTICES.md` at the repo root from the package list, and feed `FormLicense` from it.

### Stray `using` directives

Several files import namespaces from these libraries without obviously needing them. They tie unrelated code to the assemblies. Remove them when the library goes; the compiler will flag any that are real. Examples:

- `using Health.Direct.Common.Extensions` in UI controls such as `OpenDental/UI/CheckBox.cs`.
- `using Intuit.Ipp.Data` in `FormSmsTextMessaging.cs`.
- `using Microsoft.Graph` in `AutoCodes.cs`.
- `using Tamir.SharpSsh.java.lang` in `FormASAP.cs`.
- `using Word` in `ProcedureCodes.cs`.

### Not verified

- Which files Open Dental's installer deploys, and whether it ships the missing dependencies above.
- What the Open Dental binaries themselves load. For example, `SuniUSB.dll` is assumed to sit behind `xImageDeviceManager.dll`. Checking would mean inspecting those binaries, which this project does not do.
- Redistribution terms for Topaz SigPlusNET, DentalXChange DpsPos, Suni and EZTwain. They are not public. The classification rests on the copyright notices in the files and the vendors' pages.
- The origin of `RS232.dll`.

## Appendix A: version info of every file

From PowerShell `(Get-Item <file>).VersionInfo`, as stored in each file. "-" means the field is empty. Text files and scripts have no version info. The garbled "Â©" in `Ionic.Zip.dll`'s copyright is stored that way in the file itself.

| File | Size (bytes) | CompanyName | ProductName | FileVersion | LegalCopyright |
|---|---:|---|---|---|---|
| `.NET Core/AWSSDK.Core.dll` | 987,648 | Amazon.com, Inc | Amazon Web Services SDK for .NET | 3.3.107.6 | Copyright 2009-2015 Amazon.com, Inc. or its affiliates. All Rights Reserved. |
| `.NET Core/AWSSDK.EC2.dll` | 1,879,552 | Amazon.com, Inc | Amazon Web Services SDK for .NET | 3.3.179.1 | Copyright 2009-2020 Amazon.com, Inc. or its affiliates. All Rights Reserved. |
| `.NET Core/AWSSDK.S3.dll` | 549,376 | Amazon.com, Inc | Amazon Web Services SDK for .NET | 3.3.111.7 | Copyright 2009-2020 Amazon.com, Inc. or its affiliates. All Rights Reserved. |
| `.NET Standard/BouncyCastle.Crypto.dll` | 2,179,584 | The Legion of the Bouncy Castle Inc. | Bouncy Castle for .NET | 1.8.18099.1 | Copyright (C) 2000-2018 |
| `.NET Standard/Google.Protobuf.dll` | 299,008 | Google Inc. | Google.Protobuf | 3.6.1.0 | Copyright 2015, Google Inc. |
| `.NET Standard/MySql.Data.dll` | 759,296 | Oracle | MySql.Data.Core | 8.0.17.0 | Copyright © 2016, 2019, Oracle and/or its affiliates. All rights reserved. |
| `.NET Standard/Newtonsoft.Json.dll` | 693,680 | Newtonsoft | Json.NET | 12.0.3.23909 | Copyright © James Newton-King 2008 |
| `.NET Standard/Renci.SshNet.dll` | 858,624 | Renci | SSH.NET | 2024.1.0.0 | Copyright © Renci 2010-2024 |
| `.NET Standard/SshNet.Security.Cryptography.dll` | 34,816 | Renci | SshNet.Security.Cryptography | 1.2.0 | Copyright © Renci 2010-2016 |
| `.NET Standard/System.Configuration.ConfigurationManager.dll` | 380,848 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.25921.02 | © Microsoft Corporation.  All rights reserved. |
| `.NET Standard/System.Security.Cryptography.ProtectedData.dll` | 24,880 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.25519.03 | © Microsoft Corporation.  All rights reserved. |
| `.NET Standard/System.Security.Permissions.dll` | 94,584 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.25921.02 | © Microsoft Corporation.  All rights reserved. |
| `.NET Standard/System.Xml.XPath.XmlDocument.dll` | 118,520 | Microsoft Corporation | Microsoft® .NET Framework | 1.0.24212.01 | © Microsoft Corporation.  All rights reserved. |
| `Avalara.AvaTax.RestClient.net45.dll` | 776,704 | - | Avalara.AvaTax.RestClient | 20.1.0 | Copyright (C) 2004 - 2017 |
| `AWSSDK.Core.dll` | 1,998,960 | Amazon.com, Inc | Amazon Web Services SDK for .NET | 3.7.300.9 | Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved. |
| `AWSSDK.IdentityStore.dll` | 122,992 | Amazon.com, Inc | Amazon Web Services SDK for .NET | 3.7.300.9 | Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved. |
| `AxInterop.AcroPDFLib.dll` | 9,216 | - | - | 1.0.0.0 | - |
| `AxInterop.WMPLib.dll` | 53,760 | - | - | 1.0.0.0 | - |
| `Azure.Core.dll` | 330,640 | Microsoft Corporation | Azure .NET SDK | 1.2500.22.33004 | © Microsoft Corporation. All rights reserved. |
| `Bridges.dll` | 118,784 | - | Bridges | 24.3.63.0 | Copyright ©  2020 |
| `CDT.dll` | 347,136 | - | CDT | 24.3.63.0 | Copyright ©  2007 |
| `Dicom.Core.dll` | 1,481,216 | fo-dicom | fo-dicom | 4.0.7.0 | Copyright © fo-dicom contributors 2012-2020 |
| `Dicom.Native.dll` | 941,056 | - | - | - | - |
| `DiffieHellman.dll` | 53,248 | - | - | 0.0.0.0 | - |
| `DpsPos.dll` | 338,432 | EDI-Health Group, Inc. | DpsPos | 1.0.6191.28914 | Copyright © EDI-Health Group, Inc. 2016. All Rights Reserved. |
| `Dropbox.Api.dll` | 2,207,232 | Dropbox Inc. | Dropbox.Api | 4.0.6348 | Copyright © Dropbox Inc |
| `EZT4Jpeg.dll` | 274,208 | Atalasoft Inc. (www.atalasoft.com) | EZTwain Pro | 1.16.0.0 | ©Atalasoft Inc. All Rights Reserved. |
| `EZT4Pdf.dll` | 397,088 | Atalasoft Inc. (www.atalasoft.com) | EZTwain Pro | 1.61.0.0 | ©Atalasoft Inc. All Rights Reserved. |
| `Eztwain4.dll` | 986,912 | Dosadi (www.dosadi.com) | EZTwain Pro | 4.0.3.0 | ©Atalasoft Inc. All Rights Reserved. |
| `freeglut.dll` | 241,664 | - | - | - | - |
| `Google.Apis.Auth.dll` | 119,296 | Google Inc. | Google.Apis.Auth | 1.44.0.0 | Copyright 2017 Google Inc. |
| `Google.Apis.Auth.PlatformServices.dll` | 5,120 | Google Inc. | Google.Apis.Auth.PlatformServices | 1.44.0.0 | Copyright 2017 Google Inc. |
| `Google.Apis.Core.dll` | 72,704 | Google Inc. | Google.Apis.Core | 1.44.0.0 | Copyright 2017 Google Inc. |
| `Google.Apis.dll` | 77,312 | Google Inc. | Google.Apis | 1.44.0.0 | Copyright 2017 Google Inc. |
| `Google.Apis.Gmail.v1.dll` | 105,984 | Google Inc. | Google.Apis.Gmail.v1 | 1.44.0.1859 | Copyright 2017 Google Inc. |
| `Google.Apis.PlatformServices.dll` | 4,608 | Google Inc. | Google.Apis.PlatformServices | 1.44.0.0 | Copyright 2017 Google Inc. |
| `Health.Direct.Agent.dll` | 53,248 | Direct Project | Direct Project | 1.2.0.8 | Copyright 2010-2013 © Direct Project |
| `Health.Direct.Common.dll` | 280,064 | Direct Project | Direct Project | 1.2.0.8 | Copyright 2010-2013 © Direct Project |
| `Health.Direct.ResolverPlugins.dll` | 22,016 | Direct Project | Direct Project | 1.2.0.8 | Copyright 2010-2013 © Direct Project |
| `Hunspellx64.dll` | 592,384 | - | NHunspell Spell Checker | 0,9,6,0 | Copyright (C) 2009 maierhofer software, Lazlo Nemeth and the contributing authors. |
| `Hunspellx86.dll` | 537,600 | - | NHunspell Spell Checker | 0,9,6,0 | Copyright (C) 2009 maierhofer software, Lazlo Nemeth and the contributing authors. |
| `ImagingDeviceManager.dll` | 65,024 | - | ImagingDeviceManager | 24.3.63.0 | Copyright ©  2020 |
| `installutil.exe` | 27,992 | Microsoft Corporation | Microsoft® .NET Framework | 4.0.30319.1 (RTMRel.030319-0100) | © Microsoft Corporation.  All rights reserved. |
| `Interop.Acrobat.dll` | 36,864 | - | Assembly imported from type library 'Acrobat'. | 1.1.0.0 | - |
| `Interop.AcroPDFLib.dll` | 12,800 | - | Assembly imported from type library 'AcroPDFLib'. | 1.0.0.0 | - |
| `Interop.AFORMAUTLib.dll` | 11,776 | - | Assembly imported from type library 'AFORMAUTLib'. | 1.0.0.0 | - |
| `Interop.CDRDATALib.dll` | 49,152 | - | Assembly imported from type library 'CDRDATALib'. | 1.1.0.0 | - |
| `Interop.CDRDicom.dll` | 32,768 | - | Assembly imported from type library 'CDRDicom'. | 1.1.0.0 | - |
| `Interop.Microsoft.Office.Core.dll` | 196,608 | - | Assembly imported from type library Office | 2.2.0.0 | - |
| `Interop.QBFC10.dll` | 874,352 | - | Assembly imported from type library QBFC10Lib | 11.0.0.29 | - |
| `Interop.VBIDE.dll` | 57,344 | - | Assembly imported from type library VBIDE | 5.3.0.0 | - |
| `Interop.WMPLib.dll` | 330,752 | - | Assembly imported from type library 'WMPLib'. | 1.0.0.0 | - |
| `Interop.Word.dll` | 585,728 | - | Assembly imported from type library Word | 8.2.0.0 | - |
| `Intuit.Ipp.Core.dll` | 69,120 | Intuit | Intuit.Ipp.Core | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.Data.dll` | 352,768 | Intuit.Ipp.Data | Intuit.Ipp.Data | 1.0.0.0 | - |
| `Intuit.Ipp.DataService.dll` | 72,192 | Intuit | Intuit.Ipp.DataService | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.Diagnostics.dll` | 5,632 | Intuit | Intuit.Ipp.Diagnostics | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.EntitlementService.dll` | 9,728 | Intuit | Intuit.Ipp.EntitlementService | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.Exception.dll` | 19,968 | Intuit | Intuit.Ipp.Exception | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.GlobalTaxService.dll` | 15,360 | Intuit | Intuit.Ipp.GlobalTaxService | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.OAuth2PlatformClient.dll` | 81,920 | Intuit | Intuit.Ipp.OAuth2PlatformClient | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.QueryFilter.dll` | 11,776 | Intuit | Intuit.Ipp.QueryFilter | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.ReportService.dll` | 32,768 | Intuit | Intuit.Ipp.ReportService | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.Retry.dll` | 6,144 | Intuit | Intuit.Ipp.Retry | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.Security.dll` | 5,632 | Intuit | Intuit.Ipp.Security | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.Utility.dll` | 33,280 | Intuit | Intuit.Ipp.Utility | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Intuit.Ipp.WebHooksService.dll` | 13,312 | Intuit | Intuit.Ipp.WebHooksService | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `Ionic.Zip.dll` | 444,416 | Dino Chiesa | DotNetZip Library | 1.9.1.4 | Copyright Â© Dino Chiesa 2006 - 2010 |
| `IppDotNetSdkForQuickBooksApiV3.dll` | 4,608 | Intuit | IppDotNetSdkForQuickBooksApiV3 | 14.4.0.0 | Copyright © 2020 Intuit, Inc. |
| `log4net.dll` | 270,336 | The Apache Software Foundation | log4net | 2.0.15.0 | Copyright 2004-2017 The Apache Software Foundation. |
| `MailKit.dll` | 784,896 | Xamarin Inc. | MailKit | 2.4.1.0 | Copyright © 2013-2019 Xamarin Inc. (www.xamarin.com) |
| `MediaFoundation.dll` | 358,912 | - | Debug Version | 3.1.0.0 | GNU Lesser General Public License v2.1 or BSD |
| `MediaFoundation.Extension.dll` | 87,552 | - | Release Version | 1.0.0.0 | GNU Lesser General Public License v2.1 or BSD |
| `Microsoft.Azure.Documents.Client.dll` | 1,751,960 | Microsoft Corporation | Microsoft(R) Azure Cosmos DB | 2.4.0.0 | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Bcl.AsyncInterfaces.dll` | 22,144 | Microsoft Corporation | Microsoft® .NET | 6.0.21.52210 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.DirectX.Direct3D.dll` | 473,600 | Microsoft Corporation | Microsoft® DirectX for Windows® | 9.05.132.0000 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.DirectX.Direct3DX.dll` | 578,560 | Microsoft Corporation | Microsoft® DirectX for Windows® | 9.12.589.0000 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.DirectX.dll` | 223,232 | Microsoft Corporation | Microsoft® DirectX for Windows® | 5.04.00.2904 | Copyright © Microsoft Corporation.  All rights reserved. |
| `Microsoft.DotNet.PlatformAbstractions.dll` | 23,576 | Microsoft Corporation | Microsoft® .NET Framework | 2.0.4 | © Microsoft Corporation.  All rights reserved. |
| `Microsoft.Extensions.Logging.Abstractions.dll` | 66,168 | Microsoft Corporation | Microsoft® .NET | 7.0.723.27404 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.Graph.Core.dll` | 176,016 | Microsoft | Microsoft.Graph.Core | 2.0.13.0 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.Graph.dll` | 10,689,448 | Microsoft | Microsoft.Graph | 4.42.0.0 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.Identity.Client.dll` | 1,511,904 | Microsoft Corporation | Microsoft Authentication Library | 4.47.1.0 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.IdentityModel.Abstractions.dll` | 18,816 | Microsoft Corporation. | Microsoft IdentityModel | 6.23.1.30908 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.IdentityModel.JsonWebTokens.dll` | 89,472 | Microsoft Corporation. | Microsoft IdentityModel | 6.23.1.30908 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.IdentityModel.Logging.dll` | 34,176 | Microsoft Corporation. | Microsoft IdentityModel | 6.23.1.30908 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.IdentityModel.Protocols.dll` | 38,272 | Microsoft Corporation. | Microsoft IdentityModel | 6.23.1.30908 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect.dll` | 114,080 | Microsoft Corporation. | Microsoft IdentityModel | 6.23.1.30908 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.IdentityModel.Tokens.dll` | 985,488 | Microsoft Corporation. | Microsoft IdentityModel | 6.23.1.30908 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.Core.dll` | 413,808 | Microsoft Corporation | Microsoft.ML.Core | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.CpuMath.dll` | 61,552 | Microsoft Corporation | Microsoft.ML.CpuMath | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.Data.dll` | 1,558,656 | Microsoft Corporation | Microsoft.ML.Data | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.DataView.dll` | 47,216 | Microsoft Corporation | Microsoft.ML.DataView | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.dll` | 13,936 | Microsoft Corporation | Microsoft.ML | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.FastTree.dll` | 476,800 | Microsoft Corporation | Microsoft.ML.FastTree | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.KMeansClustering.dll` | 54,896 | Microsoft Corporation | Microsoft.ML.KMeansClustering | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.LightGbm.dll` | 88,192 | Microsoft Corporation | Microsoft.ML.LightGbm | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.PCA.dll` | 52,864 | Microsoft Corporation | Microsoft.ML.PCA | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.StandardTrainers.dll` | 323,696 | Microsoft Corporation | Microsoft.ML.StandardTrainers | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.ML.Transforms.dll` | 761,968 | Microsoft Corporation | Microsoft.ML.Transforms | 1.700.122.15804 | © Microsoft Corporation. All rights reserved. |
| `Microsoft.Web.WebView2.Core.dll` | 563,672 | Microsoft | WebView2 .NET Interop Wrapper | 1.0.2420.47 | Copyright ©2020 |
| `Microsoft.Web.WebView2.WinForms.dll` | 38,360 | Microsoft | WebView2 Windows Forms Control | 1.0.2420.47 | Copyright ©2020 |
| `Microsoft.Web.WebView2.Wpf.dll` | 45,648 | Microsoft | WebView2 WPF Control | 1.0.2420.47 | Copyright ©2020 |
| `MigraDoc.DocumentObjectModel.dll` | 249,856 | empira Software GmbH, Cologne (Germany) | empira MigraDoc® | 1.2.2546.0 | Copyright © 2001-2008 empira Software GmbH. |
| `MigraDoc.Rendering.dll` | 135,168 | empira Software GmbH, Cologne (Germany) | empira MigraDoc® | 1.2.2546.0 | Copyright © 2001-2008 empira Software GmbH. |
| `MigraDoc.RtfRendering.dll` | 81,920 | empira Software GmbH, Cologne (Germany) | empira MigraDoc® | 1.2.2546.0 | Copyright © 2001-2008 empira Software GmbH. |
| `MimeKit.dll` | 932,864 | Xamarin Inc. | MimeKit | 2.4.1.0 | Copyright © 2013-2019 Xamarin Inc. (www.xamarin.com) |
| `MSVCR71.dll` | 348,160 | Microsoft Corporation | Microsoft® Visual Studio .NET | 7.10.3052.4 | © Microsoft Corporation.  All rights reserved. |
| `MySqlConnector.dll` | 821,608 | Bradley Grainger | MySqlConnector | 2.3.7.0 | Copyright 2016–2024 Bradley Grainger |
| `NDde.dll` | 86,016 | Brian Gideon | NDde | 1.03.0171.0 | Copyright (c) 2005 by Brian Gideon (briangideon@yahoo.com) |
| `Newtonsoft.Json.dll` | 700,336 | Newtonsoft | Json.NET | 12.0.3.23909 | Copyright © James Newton-King 2008 |
| `NHunspell.dll` | 29,184 | maierhofer software | NHunspell | 0.9.6.0 | Copyright © maierhofer software 2009 |
| `ODApi.dll` | 1,270,272 | Open Dental Software | ODApi.Properties | 1.0.0.0 | Copyright ©  2022 |
| `ODCrypt.dll` | 82,944 | - | ODCrypt | 24.3.63.0 | Copyright ©  2017 |
| `ODR.dll` | 53,248 | - | - | 4.6.1.0 | - |
| `OpenDentalHelp.dll` | 145,408 | Microsoft | OpenDentalHelp | 24.3.63.0 | Copyright © Microsoft 2015 |
| `OpenPop.dll` | 69,632 | OpenPop | OpenPop | 2.0.5.0 | Public Domain |
| `Org.Mentalis.Security.dll` | 184,320 | The Mentalis.org Team | Mentalis.org Security Library | 1.0.13.715 | Copyright © 2002-2005, The Mentalis.org Team |
| `PasswordVaultWrapper.dll` | 5,632 | - | PasswordVaultWrapper | 1.0.0.0 | Copyright ©  2023 |
| `PdfSharp.Charting.dll` | 94,208 | empira Software GmbH, Cologne (Germany) | PDFsharp | 1.2.1269.0 | Copyright © 2005-2008 empira Software GmbH. |
| `PdfSharp.dll` | 536,576 | empira Software GmbH, Cologne (Germany) | PDFsharp | 1.2.1269.0 | Copyright © 2005-2008 empira Software GmbH. |
| `RdlDesigner.exe` | 1,032,192 | fyiReporting Software, LLC | RDL Project | 2.9.3.0 | Copyright (C) 2004-2006  fyiReporting Software, LLC |
| `RdlEngine.dll` | 626,688 | fyiReporting Software, LLC | RDL Project | 2.9.3.0 | Copyright (C) 2004-2006  fyiReporting Software, LLC |
| `RdlViewer.dll` | 57,344 | fyiReporting Software, LLC | RDL Project | 2.9.3.0 | Copyright (C) 2004-2006  fyiReporting Software, LLC |
| `Renci.SshNet.dll` | 856,064 | Renci | SSH.NET | 2024.1.0.0 | Copyright © Renci 2010-2024 |
| `RS232.dll` | 81,920 | - | - | 1.18.0.0 | - |
| `Serilog.dll` | 126,464 | Serilog Contributors | Serilog | 2.9.0.0 | - |
| `Serilog.Enrichers.Environment.dll` | 6,656 | Serilog Contributors | Serilog.Enrichers.Environment | 2.1.3.0 | - |
| `Serilog.Enrichers.Thread.dll` | 6,144 | Serilog Contributors | Serilog.Enrichers.Thread | 3.1.0.0 | - |
| `Serilog.Extensions.Logging.dll` | 15,872 | Microsoft;Serilog Contributors | Serilog.Extensions.Logging | 3.0.1.0 | - |
| `Serilog.Settings.Configuration.dll` | 47,616 | Serilog Contributors | Serilog.Settings.Configuration | 3.1.0.0 | - |
| `Serilog.Sinks.AzureDocumentDB.dll` | 28,160 | Serilog | Serilog.Sinks.AzureDocumentDB | 4.0.0.0 | Copyright © Serilog Contributors 2013-2017 |
| `Serilog.Sinks.Console.dll` | 32,256 | Serilog Contributors | Serilog.Sinks.Console | 3.1.1.0 | - |
| `Serilog.Sinks.Debug.dll` | 6,656 | Serilog Contributors | Serilog.Sinks.Debug | 1.0.1.0 | - |
| `Serilog.Sinks.File.dll` | 28,160 | Serilog Contributors | Serilog.Sinks.File | 4.1.0.0 | - |
| `Serilog.Sinks.Literate.dll` | 5,120 | Serilog Contributors | Serilog.Sinks.Literate | 3.0.0 | - |
| `Serilog.Sinks.RollingFile.dll` | 18,432 | - | - | 3.3.0.0 | - |
| `Serilog.Sinks.Trace.dll` | 6,144 | - | - | 2.1.0.0 | - |
| `SerilogTraceListener.dll` | 9,216 | - | - | 0.0.0.0 | - |
| `SharpDX.Direct3D9.dll` | 347,136 | Alexandre Mutel | SharpDX.Direct3D9 | 4.0.1 | Copyright (c) 2010-2016 Alexandre Mutel |
| `SharpDX.dll` | 276,992 | Alexandre Mutel | SharpDX | 4.0.1 | Copyright (c) 2010-2016 Alexandre Mutel |
| `SharpDX.Mathematics.dll` | 220,672 | Alexandre Mutel | SharpDX.Mathematics | 4.0.1 | Copyright (c) 2010-2016 Alexandre Mutel |
| `SigPlusNET.dll` | 277,624 | Topaz Systems Inc. | SigPlusNET | 2.1.5.0 | Copyright © Topaz Systems Inc. 2011-2023 |
| `Sparks3D/readme.txt` | 2,102 | - | - | - | - |
| `Sparks3D/Sparks3D.dll` | 1,118,720 | Open Dental Software | Sparks3D | 24.3.63.0 | Copyright ©  Jordan Sparks, 2020 |
| `Sparks3D/Sparks3DXEngine.dll` | 63,488 | Open Dental Software | Open Dental | 22.4.0.0 | Copyright (C) 2020 |
| `stdole.dll` | 16,384 | Microsoft Corporation | Microsoft® Visual Studio .NET | 7.00.9466 | © Microsoft Corporation.  All rights reserved. |
| `SuniUSB.dll` | 86,016 | Suni Imaging Microsystems, Inc. | SuniRay | 2, 5, 0, 14 | Copyright (C) 2004 Suni Imaging Microsystems, Inc. |
| `System.Buffers.dll` | 20,856 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.28619.01 | © Microsoft Corporation. All rights reserved. |
| `System.Diagnostics.DiagnosticSource.dll` | 173,232 | Microsoft Corporation | Microsoft® .NET | 7.0.423.11508 | © Microsoft Corporation. All rights reserved. |
| `System.Memory.Data.dll` | 21,880 | Microsoft Corporation | Azure .NET SDK | 1.0.221.20802 | © Microsoft Corporation. All rights reserved. |
| `System.Memory.dll` | 142,240 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.31308.01 | © Microsoft Corporation. All rights reserved. |
| `System.Net.Http.dll` | 197,680 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.26907.01 | © Microsoft Corporation. All rights reserved. |
| `System.Net.Http.WinHttpHandler.dll` | 139,416 | Microsoft Corporation | Microsoft® .NET | 6.0.422.16404 | © Microsoft Corporation. All rights reserved. |
| `System.Numerics.Vectors.dll` | 115,856 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.26515.06 | © Microsoft Corporation.  All rights reserved. |
| `System.Runtime.CompilerServices.Unsafe.dll` | 18,024 | Microsoft Corporation | Microsoft® .NET | 6.0.21.52210 | © Microsoft Corporation. All rights reserved. |
| `System.Text.Encodings.Web.dll` | 76,904 | Microsoft Corporation | Microsoft® .NET | 6.0.21.52210 | © Microsoft Corporation. All rights reserved. |
| `System.Text.Json.dll` | 514,184 | Microsoft Corporation | Microsoft® .NET | 6.0.922.41905 | © Microsoft Corporation. All rights reserved. |
| `System.Threading.Tasks.Extensions.dll` | 25,984 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.28619.01 | © Microsoft Corporation. All rights reserved. |
| `System.ValueTuple.dll` | 25,232 | Microsoft Corporation | Microsoft® .NET Framework | 4.6.26515.06 | © Microsoft Corporation.  All rights reserved. |
| `Tamir.SharpSSH.dll` | 200,704 | tamirgal.com | SharpSSH | 1.1.1.13 | Tamir Gal (c) 2007 and jcraft.com |
| `Tao.FreeGlut.dll` | 40,960 | Tao Framework -- http://www.taoframework.com | Tao.FreeGlut.dll | 2.4.0.1 | Copyright ©2003-2006 Tao Framework Team.  All rights reserved. |
| `Tao.OpenGl.dll` | 933,888 | Tao Framework -- http://www.taoframework.com | Tao.OpenGl.dll | 2.1.0.4 | Copyright ©2003-2007 Tao Framework Team.  All rights reserved. |
| `Tao.Platform.Windows.dll` | 86,016 | Tao Framework -- http://www.taoframework.com | Tao.Platform.Windows.dll | 1.0.0.4 | Copyright ©2003-2006 Tao Framework Team.  All rights reserved. |
| `VBbridges.dll` | 16,896 | - | VBbridges | 22.1.0.0 | Copyright ©  2022 |
| `vcruntime140.dll` | 83,752 | Microsoft Corporation | Microsoft® Visual Studio® | 14.27.29016.0 built by: vcwrkspc | © Microsoft Corporation. All rights reserved. |
| `VideoCamLib.dll` | 881,664 | - | - | - | - |
| `VirtualWeb.dll` | 5,632 | - | Web | 22.1.0.0 | Copyright ©  2019 |
| `WebView2Loader.dll` | 116,824 | Microsoft Corporation | Microsoft Edge Embedded Browser WebView Loader | 1.0.2420.47 | Copyright Microsoft Corporation. All rights reserved. |
| `Windows.winmd` | 3,345,728 | - | - | - | - |
| `xImageDeviceManager.dll` | 10,430,976 | - | xImageDeviceManager | 1.0.0.0 | Copyright ©  2007 |
| `zxing.dll` | 445,952 | ZXing.Net Development | ZXing.Net | 0.16.5.0 | Copyright © 2012 |
| `zxing.presentation.dll` | 19,456 | ZXing.Net Development | ZXing.Net | 0.16.5.0 | Copyright © 2012 |
| `RequiredServerDlls/BatchCheckServiceStatus.bat` | 270 | - | - | - | - |
| `RequiredServerDlls/BatchInstallService.bat` | 130 | - | - | - | - |
| `RequiredServerDlls/BatchStartService.bat` | 81 | - | - | - | - |
| `RequiredServerDlls/BatchStopService.bat` | 79 | - | - | - | - |
| `RequiredServerDlls/BatchUninstallService.bat` | 136 | - | - | - | - |
| `RequiredServerDlls/installutil.exe` | 28,672 | Microsoft Corporation | Microsoft® .NET Framework | 2.0.50727.1434 (REDBITS.050727-1400) | © Microsoft Corporation.  All rights reserved. |

## Sources

Vendor, project and package pages consulted for license facts on 2026-10-18. License texts already in the repo (`OpenDental/Resources/*.txt`) and in-repo source headers are cited in the tables where they were used.

- <https://freeglut.sourceforge.net/>
- <https://github.com/Azure/azure-sdk-for-net>
- <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet>
- <https://github.com/AzureAD/microsoft-authentication-library-for-dotnet>
- <https://github.com/DirectProject/nhin-d-dotnet/blob/master/csharp/copyright_header.txt>
- <https://github.com/JamesNK/Newtonsoft.Json>
- <https://github.com/OfItselfSo/Tanta>
- <https://github.com/apache/logging-log4net>
- <https://github.com/avadev/AvaTax-REST-V2-DotNet-SDK>
- <https://github.com/aws/aws-sdk-net>
- <https://github.com/dotnet/core-setup>
- <https://github.com/dotnet/machinelearning>
- <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>
- <https://github.com/dropbox/dropbox-sdk-dotnet>
- <https://github.com/empira/MigraDoc>
- <https://github.com/empira/PDFsharp>
- <https://github.com/googleapis/google-api-dotnet-client>
- <https://github.com/haf/DotNetZip.Semverd/blob/master/LICENSE>
- <https://github.com/intuit/QuickBooks-V3-DotNET-SDK>
- <https://github.com/jstedfast/MailKit>
- <https://github.com/micjahn/ZXing.Net>
- <https://github.com/microsoftgraph/msgraph-sdk-dotnet>
- <https://github.com/microsoftgraph/msgraph-sdk-dotnet-core>
- <https://github.com/mysql-net/MySqlConnector>
- <https://github.com/protocolbuffers/protobuf>
- <https://github.com/serilog/serilog>
- <https://github.com/sharpdx/SharpDX>
- <https://github.com/sshnet/Cryptography>
- <https://github.com/sshnet/SSH.NET>
- <https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files>
- <https://learn.microsoft.com/en-us/dotnet/framework/tools/installutil-exe-installer-tool>
- <https://learn.microsoft.com/en-us/previous-versions/office/developer/office-2003/aa159923(v=office.11)>
- <https://learn.microsoft.com/en-us/uwp/winrt-cref/winmd-files>
- <https://sourceforge.net/projects/mfnet/>
- <https://sourceforge.net/projects/sharpssh/>
- <https://topazsystems.com/license.htm>
- <https://visualstudio.microsoft.com/license-terms/mt591984> (the license that NuGet `stdole` 7.0.3300 links to)
- <https://www.bouncycastle.org/licence.html>
- <https://www.eztwain.com/>
- <https://www.nuget.org/packages/DotNetZip>
- <https://www.nuget.org/packages/IppDotNetSdkForQuickBooksApiV3/14.4.0>
- <https://www.nuget.org/packages/Microsoft.Azure.DocumentDB/2.4.0>
- <https://www.nuget.org/packages/Microsoft.Office.Interop.Word>
- <https://www.nuget.org/packages/Microsoft.Web.WebView2>
- <https://www.nuget.org/packages/Microsoft.Windows.SDK.Contracts>
- <https://www.nuget.org/packages/MimeKit/2.4.1>
- <https://www.nuget.org/packages/MySql.Data/8.0.17>
- <https://www.nuget.org/packages/NDde>
- <https://www.nuget.org/packages/NHunspell>
- <https://www.nuget.org/packages/NTwain>
- <https://www.nuget.org/packages/OpenPop.NET>
- <https://www.nuget.org/packages/System.Configuration.ConfigurationManager/4.4.1>
- <https://www.nuget.org/packages/System.Net.Http/4.3.4>
- <https://www.nuget.org/packages/System.Xml.XPath.XmlDocument/4.3.0>
- <https://www.nuget.org/packages/WeCantSpell.Hunspell>
- <https://www.nuget.org/packages/fo-dicom.Desktop/4.0.7>
- <https://www.nuget.org/packages/stdole/17.14.40260/License>
- <https://www.nuget.org/packages/stdole/7.0.3300>
