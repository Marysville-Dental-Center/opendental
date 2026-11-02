> **Release readiness review, 2026-11-01: a snapshot.** A read-only analysis of `main` at 1db2c6d86, made before the organization cleanup, the rename and the v0.0 release. It uses the names and paths of that time (OpenDental, OpenDentBusiness and so on), and "you" is the owner it was written for. It lists the binaries, licences and installer needs for v0.0. Its blockers were fixed by packages BIN-A, BIN-B, REL-FILES, RDL-SRC and the release work (docs/release-notes/v0.0.md, tools/Build-Release.ps1, installer/OpenPmsDental.iss); v0.0 was published on 2026-11-01.

# v0.0 release readiness for Open PMS - Dental (read-only review of main 1db2c6d86)

Per your request, the office switch-over is out of scope. Adoption comes up below only where it touches the installer and the update mechanism. Nothing in the repository was changed.

## Verdict

**v0.0 cannot ship yet.** These are the blockers:

1. **Open Dental proprietary and commercial binaries are still compile-time or runtime dependencies on main.**
   - `ImagingDeviceManager.dll` is substituted from `Required dlls`.
   - `xImageDeviceManager.dll` and `VBbridges.dll` are referenced.
   - `Sparks3D.dll` and `SigPlusNET.dll` are referenced.
   - EZTwain, VideoCamLib and SuniUSB are loaded at runtime.
   - cap/cap-4 removes only the TWAIN and video set.
2. **The build output already contains files that may not be redistributed**: `stdole.dll`, the Office interop assemblies, the Adobe ActiveX interop, `Windows.winmd` and `RS232.dll`.
3. **Two files a Release build needs are not copied to the output.**
   - Without `Hunspellx86.dll`, every `ODtextBox` throws in Release.
   - Without `WebView2Loader.dll`, every WebView2 feature fails.
4. **Some libraries have licences that conflict with GPL v2** (MS-PL and Apache-2.0). This needs your decision.
5. **The in-app licence list names Open Dental's commercial purchase terms as "the" licence**, and no third-party notices file exists.
6. **No Release build has ever been produced or tested.** There is no `*/bin/Release` folder anywhere in the checkout. Debug builds never publish or pull (`docs/release-process.md:130`).
7. **There is no installer or staging script, and no installer tool is installed.**

The details and the evidence follow.

## 1. Binaries

### 1.0 What the repository holds

- **129 tracked binaries, 55.7 MB** (from `git ls-files` filtered on dll/exe/zip/ocx/ax/msi):
  - `Required dlls/`: 101 at the top level, plus `.NET Core/` (3), `.NET Standard/` (10) and `Sparks3D/` (2).
  - `RequiredServerDlls/` (1), `MobileWeb/` (3), `SlowQueryLogTool/` (2), `SparksToothChart/RequiredDlls` (4), `SparksToothChart/TestForm/RequiredDlls` (2).
  - `OpenDentBusiness/Resources/rxnorm.zip` (1).
- **Not in the 129 but also binary or licensed:** `Required dlls/Windows.winmd` and `Required dlls/Sparks3D/readme.txt`.
- **No NuGet package reaches the app.** There is no `PackageReference` anywhere. `packages.config` exists only in `UnitTests/` (Mono.Cecil), `RdlEngine/` and `SlowQueryLogTool/`.
- **Already gone from main:** Bridges.dll, CDT.dll, ODCrypt.dll (now in-repo at `ODCrypt/xODCrypt.csproj`), OpenDentalHelp.dll, VirtualWeb.dll, ODApi.dll, DpsPos.dll and log4net.dll. None is tracked.
- **Baseline audit:** `docs/third-party-binaries-audit.md` (2026-10-18). This section re-checks every file against main as it is today.

### 1.1 Open Dental proprietary, or closed binaries Open Dental shipped. None may ship.

| File | Origin and licence | How main uses it | In the OpenDental build output? |
|---|---|---|---|
| `Required dlls/ImagingDeviceManager.dll` (65 KB) | Open Dental. Closed EZTwain wrapper. | Substituted for the missing `..\..\ImagingDeviceManager\xImagingDeviceManager.csproj` (`OpenDental/OpenDental.csproj:9490-9493`; `Directory.Build.targets:20-33` says "today only ImagingDeviceManager"). | Yes (`OpenDental/bin/Debug/ImagingDeviceManager.dll`). cap/cap-4 adds an in-repo `ImagingDeviceManager/` and deletes the DLL. |
| `xImageDeviceManager.dll` (10.4 MB) | Open Dental. Closed Suni capture. | HintPath `OpenDental.csproj:274-277`. `ControlImagesOld.cs:26,123,181` always creates a `SuniDeviceControl`. `FormImages.designer.cs:27` always hosts `ControlImagesOld`, and FormImages is opened from claims (`FormClaimEdit.cs:2202`, `FormClaimPayBatch.cs:545`). So this is a hard runtime dependency. | Yes. **Still referenced on cap/cap-4** (`OpenDental.csproj:274`). |
| `SuniUSB.dll` | Suni Imaging commercial driver, shipped by Open Dental. | No project references it. Presumably loaded by xImageDeviceManager (audit line 349). | No. Still present on cap-4. |
| `VBbridges.dll` | Open Dental. Closed VB.NET helper. | HintPaths `CodeBase/xCodeBase.csproj:139-142`, `OpenDental.csproj:268-271`, `WpfControlsOD.csproj:105-108`. Seven call sites: `CodeBase/Utilities/DrCephArgs.cs:44`, `FormRxManage.cs:154,157`, `ControlChart.cs:8431,8435`, `WpfControlsOD/Bridges/Patterson.cs:35`, `Schick.cs:38`. | Yes. |
| `VideoCamLib.dll` | Open Dental. Closed native Media Foundation wrapper. | Seven `DllImport`s in `OpenDental/UI/VideoCamera/VideoCamLib.cs`. | No, so video capture fails at runtime. cap-4 deletes the code and the DLL. |
| `Sparks3D/Sparks3D.dll`, `Sparks3DXEngine.dll`, `readme.txt` | Open Dental custom licence: no SaaS use, no reverse engineering. | HintPaths `OpenDentBusiness.csproj:208-210` and `WpfControlsOD.csproj:83-86` (copy-local), `OpenDental.csproj:223-226` (`Private` False), `UnitTests.csproj:170`. Code: `ToothChartHelper.cs`, `Sparks3DInterface.cs`. The runtime gate is the file's existence (`ControlChart.cs:4541`). | Not in `OpenDental/bin/Debug`, but copied into `OpenDentBusiness/bin/Debug` and `ServiceManager/bin/Debug`. `ForkReleases.cs:74-77` and `UpdateFileCopier/FormMain.cs:38` would publish and copy any `Sparks3D\` folder that is present. |
| `EZT4Jpeg.dll`, `EZT4Pdf.dll`, `Eztwain4.dll` | Atalasoft EZTwain Pro. Commercial, per-developer licence. | No project references them. Loaded by `ImagingDeviceManager.dll`. | No. cap-4 deletes all three (`git diff main...cap/cap-4`). |
| `ODR.dll` | Open Dental code, but GPL, with source in `ODR/xODR.csproj`. | Named as `<CodeModule>ODR.dll` in `OpenDental/Resources/Unsched Treat Plans.rdl:4` and `ReportAccounting*.txt:753/895`. No project references it. | No. Ship it only if built from source; otherwise omit it. |
| `PasswordVaultWrapper.dll` (prebuilt) | Stale copy of the in-repo GPL project. | Unused: the project reference `OpenDentBusiness.csproj:2178` is used instead. | Built from source. Delete the prebuilt copy. |

### 1.2 Commercial binaries and interop generated from proprietary type libraries

| File | Licence | How main uses it | Output | May it ship? |
|---|---|---|---|---|
| `SigPlusNET.dll` (Topaz) | Proprietary. No published redistribution terms. | `xCodeBase.csproj:114-117`, `OpenDentBusiness.csproj:205-207`, `OpenDental.csproj:219-222`. Isolated behind `OpenDentBusiness/UI/TopazWrapper.cs`. | Copied | No |
| `Interop.QBFC10.dll` (Intuit) | Proprietary SDK | Embedded interop types (`OpenDental.csproj:140-144`, `WpfControlsOD.csproj:53-56`) | Not copied | Not shipped. Compile-time only, repo only. |
| `Interop.CDRDATALib.dll`, `Interop.CDRDicom.dll` (Schick) | Proprietary type library | Embedded (`OpenDental.csproj:128-135`, `WpfControlsOD.csproj:45-52`) | Not copied | Repo only |
| `Interop.Acrobat.dll`, `Interop.AFORMAUTLib.dll` (Adobe) | Proprietary type library | Embedded and unused (`OpenDental.csproj:118-127`) | Not copied | Delete |
| `AxInterop.AcroPDFLib.dll`, `Interop.AcroPDFLib.dll` (Adobe) | Proprietary type library | `OpenDental.csproj:103-106`, not embedded, unused | **Copied** | No |
| `Interop.Word.dll`, `Interop.Microsoft.Office.Core.dll`, `Interop.VBIDE.dll`, `stdole.dll` | Interop we generated from Office's type library; `stdole.dll` is redistributable only through Microsoft's Office PIA installer (audit line 367) | `OpenDental.csproj:136-139,145-148`, `OpenDentBusiness.csproj:156-158`. Word letter merge. | **Copied** | No |
| `RS232.dll` | Unknown author and licence | `OpenDentBusiness.csproj:201-203`; `Eclaims/TerminalConnector.cs` (BCBS Georgia dial-up) | Copied | No |

### 1.3 Microsoft files

| File | Status | May it ship? |
|---|---|---|
| `Windows.winmd` | Copied because of the default CopyLocal (`PasswordVaultWrapper/PasswordVaultWrapper.csproj:61-63`; present in `OpenDental/bin/Debug`). | No |
| `installutil.exe` (two copies) | Copied into `ServiceManager/bin/Debug` (`ServiceManager.csproj:83-87`). | No |
| `vcruntime140.dll` | Not referenced. | No: use `vc_redist` instead |
| `MSVCR71.dll`, `Microsoft.DirectX*.dll` (3), `System.Net.Http.dll`, `System.ValueTuple.dll`, `Microsoft.ML.*` (11, `SlowQueryLogTool` only) | Unused. | Delete |
| `Microsoft.Web.WebView2.Core`, `.WinForms`, `.Wpf` | WebView2 SDK licence (BSD-style, redistributable). Copied. | Yes |
| `WebView2Loader.dll` | Same licence. **Not copied**: only `tests/M2IMGTests` copies it. | **Yes, and it must be added** |

### 1.4 Open source in the output. These may ship, with notices.

| Licence | Files |
|---|---|
| MIT | `MySqlConnector`, `Microsoft.Extensions.Logging.Abstractions`, `System.Buffers`, `System.Diagnostics.DiagnosticSource`, `System.Memory`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`, `System.Threading.Tasks.Extensions` (all via `xDataConnectionBase.csproj:72-104`); `Newtonsoft.Json`; `MimeKit` (its BouncyCastle dependency is missing, audit line 595); `Microsoft.Identity.Client`, `Microsoft.IdentityModel.Abstractions`; `Microsoft.Bcl.AsyncInterfaces`; `Renci.SshNet`; `PdfSharp`, `PdfSharp.Charting`, `MigraDoc.DocumentObjectModel`, `MigraDoc.Rendering`; `SharpDX` (3) |
| Unlicense | `OpenPop` |
| BSD-3 | `Health.Direct.*` (3) |
| MIT-X11 | `Tao.OpenGl`, `Tao.Platform.Windows` |
| BSD-style | `Tamir.SharpSSH` (unmaintained), `DiffieHellman`, `Org.Mentalis.Security` |
| LGPL / MPL / GPL | `NHunspell` |
| LGPL-2.1+ | `RdlEngine`, `RdlViewer`. These are prebuilt 2.9.3 binaries; source is in `RdlEngine/` and `RdlViewer/`, but nobody has checked that the source matches the binaries. |
| Apache-2.0 | `Google.Apis`, `.Auth`, `.Core`, `.Gmail.v1` |

`MailKit.dll` is referenced, but nothing compiled uses its types, so it does not reach `OpenDental/bin`.

### 1.5 Licence conflicts with GPL v2. Your decision.

- **MS-PL:** `Dicom.Core.dll` and `Dicom.Native.dll` (fo-dicom, used by `BitmapDicom.cs`). Also `Ionic.Zip.dll` (DotNetZip): it carries CVE-2024-48510, and the fork's own release channel uses it (`OpenDentBusiness/Misc/ForkRelease/ForkReleases.cs:11,326,452`).
- **NDde Shared Source licence:** MS-PL-like (`OpenDental/Resources/NDde.txt`).
- **Apache-2.0** (Google APIs, Avalara, zxing): incompatible with GPL v2 only, compatible with GPL v3. Open Dental's own source headers say "version 2 ... or any later version" (`OpenDental/Main Modules/FormOpenDental.cs:5-6`). 752 fork files say "version 2 or later". The 39 files that say v2 only are in `tools/` and tests, not in the app. But README, NOTICE and About all say "GPL v2".
- **Not shipped, but in the repo:**
  - `SlowQueryLogTool/Required dlls/PoorMansTSqlFormatterLib35.dll` (AGPL-3.0 according to its project; please verify).
  - `MobileWeb/Microsoft.Web.GeneratedImage.dll` (MS-PL).
  - `SlowQueryLogTool/MachineLearningModel/ThirdPartyQueryModel.zip` (licence unknown).

### 1.6 Dead references that are still copied, unused files, and files the program needs but the build doesn't copy

**Dead but copied:**
- `zxing.dll` and `zxing.presentation.dll` (`OpenDental.csproj:278-285`; no code uses ZXing).
- `MigraDoc.RtfRendering.dll` (`OpenDental.csproj:171-174`).
- `Avalara.AvaTax.RestClient.net45.dll` (`OpenDentBusiness.csproj:115-117`; `AvaTax.IsEnabled` always returns false, `AvaTax.cs:29-31`).

**Unused, not shipped:**
- `freeglut.dll`, `Tao.FreeGlut.dll` (dead references at `xCodeBase.csproj:127-130` and `SparksToothChart.csproj:115-117`).
- `MediaFoundation.dll`, `MediaFoundation.Extension.dll` (cap-4 deletes them).
- `Hunspellx64.dll` (x64 only).
- `.NET Core/` (the AWS SDK) and `.NET Standard/` (only the unbuilt `xDataConnectionBaseStandard`).
- `RdlDesigner.exe`.
- `RequiredServerDlls/` (`installutil.exe` plus five .bat files).
- `MobileWeb/*.dll`.
- `SparksToothChart/RequiredDlls/*` and `SparksToothChart/TestForm/RequiredDlls/*` (only `TestForm.csproj:39-43` uses them).
- `rxnorm.zip` (a `None` item at `OpenDentBusiness.csproj:2544`, never embedded).

**Needed at runtime but not copied by the build:**
- **`Hunspellx86.dll`**: in a Release build, `ODtextBox.cs:207-208` creates Hunspell with no `catch`. Only the Debug branch copies the DLL (`:196-205`).
- **`WebView2Loader.dll`**: see `CodeBase/Controls/ODWebView2.cs:30-66`.
- `ODR.dll`: only for RDL code modules.
- The VC++ runtime and possibly D3DX9 (see section 2).

### 1.7 What the OpenDental build output folder contains

There are no `Content` or `None` copy items in `OpenDental.csproj`, and no packages.

**Built from this repository:**
- `OpenDental.exe` with `.config`, `.pdb`, and in Release `OpenDental.xml` (`OpenDental.csproj:100`).
- `OpenDentBusiness`, `CodeBase`, `DataConnectionBase`, `WpfControlsOD`, `OpenDentalGraph`, `OpenDentalCloud`, `SparksToothChart`, `ODCrypt`, `PasswordVaultWrapper` (dlls, with `.config` files where they have one).
- `ServiceManager.exe` (a project reference, `OpenDental.csproj:9515`).
- `UpdateFileCopier.exe` (copied by the target at `OpenDental.csproj:9577-9579`).
- `Direct2dWrapper.dll` (xcopy post-build step in `Direct2dWrapper/Direct2dWrapper.vcxproj:111-117,163-165`).

**Copied third-party files:** all of 1.4, plus the forbidden ones from 1.1 to 1.3 marked "Copied". The current Debug folder (96 entries) confirms it.

`OpenDental/bin/Debug/ConversionFiles` (tracked) only matters for databases older than 4.4.

## 2. What the installer has to cover

### Files
- Exactly one staging folder made from a **Release x86** build.
- An allow-list of files, and a deny-list check that fails the build. The deny-list:
  - all of 1.1;
  - `stdole.dll`, `Interop.Word.dll`, `Interop.Microsoft.Office.Core.dll`, `Interop.VBIDE.dll`, `AxInterop.AcroPDFLib.dll`, `Interop.AcroPDFLib.dll`;
  - `Windows.winmd`, `installutil.exe`, `vcruntime140.dll`, `MSVCR71.dll`;
  - `RS232.dll`, `SigPlusNET.dll`, `zxing*`, `Avalara*`;
  - any `Sparks3D` folder.
- Add `WebView2Loader.dll`, `Hunspellx86.dll`, and `LICENSE`, `NOTICE` and a third-party notices file at the top level.
- Do not include `FreeDentalConfig.xml`.

### Prerequisites
- **.NET Framework 4.8** (`OpenDental.csproj:25`). Check `Release >= 528040`. Windows 10 1903 and later and Windows 11 already have it.
- **VC++ 2015-2022 x86 redistributable.** `Dicom.Native.dll` imports `MSVCP140` and `VCRUNTIME140` (audit line 286), and so does the Release `Direct2dWrapper.dll`, which has 68 `DllImport`s in `OpenDental/UI/Direct2d/Direct2d.cs`.
- **WebView2 Evergreen runtime, x86.** See `ODWebView2.cs:48-66`.
- **DirectX 9 tooth chart (to be verified).** Without Sparks3D the chart uses SharpDX D3DX (`SparksToothChart/ToothGroup.cs:66`, `ToothChartDirectX.cs:280`), which needs `d3dx9_43.dll` from the legacy DirectX End-User Runtime. The default mode is DirectX (`ComputerPrefs.cs:111`). Either chain that runtime or default to OpenGL or Simple 2D.
- **No MariaDB client pieces.** MySqlConnector is fully managed, and no code runs `mysqldump` or `mysql.exe`.
- **TWAIN.** The data source manager comes from the scanner drivers (cap-4 `NativeTwainDsm.cs:9-32`).
- Download prerequisite payloads at build time with pinned hashes. Do not commit them.

### Configuration
- `FreeDentalConfig.xml` lives in the program folder (`CentralConnections.cs:485,334`). If it is missing, the first run must be elevated (`CentralConnections.cs:491-500`).
- The installer should write it from a wizard page: server, database, user, and `NoShowOnStartup=False`, with no password.
  - The password format is the `osd1$` obfuscation (`CodeBase/Crypto/README.md:9`). The user enters it in Choose Database on first start.
  - Grant Users modify rights on that one file only, never on the folder. The folder is searched for `TWAINDSM.dll` (cap-4 `NativeTwainDsm.cs:26`).
- A release never publishes the config (`ForkReleases.cs:277-286`).

### Shortcuts
- Start Menu entry "Open PMS - Dental" pointing at `OpenDental.exe` (the exe name until the rename). Desktop shortcut optional.

### Per-machine install
- `C:\Program Files (x86)\Open PMS - Dental`, the path the runbook already uses (`docs/adoption-runbook.md:668`). Install with admin rights.
- Later updates elevate through `UpdateFileCopier`, which requires administrator (`UpdateFileCopier/app.manifest:18`).
- Per-user installs and ClickOnce don't work with this update path. The stale ClickOnce properties are at `OpenDental.csproj:24,28,9463-9487`.

### Uninstall
- Put the uninstaller in a subfolder. A release publishes every top-level file plus the `Sparks3D` and `OpenDentalReplicationService` folders (`ForkReleases.cs:74-77,290+`), so `unins000.*` at the top level would be published to every workstation.
- Delete the whole program folder, including files the self-update added.
- Never touch the database or the A to Z folder (`DocPath`).
- Logs are written to `{app}\Logger` by default (`CodeBase/Utilities/Logger.cs:184`). In Program Files, standard users can't write there.

### Server
The office installs MariaDB 10.11 itself (`README.md:65-68`; the runbook's step 0.0.1 confirms the server's platform and its MariaDB 10.11 version). It needs:
- `sql_mode` blank (the program sets it with `SET GLOBAL`, which requires privileges: `MiscData.cs:557`);
- `default-storage-engine=MyISAM`;
- `max_allowed_packet` of at least 40 MB (`DataConnectionBase/DataConnection.cs:782`);
- a dedicated account per office (runbook step 0.5);
- backups.

Nothing is installed on the server by us.

### How self-update and the installer interact (`docs/release-process.md:95-136`)
- Only Release builds publish or pull.
- The update server (`WebServiceServerName`) starts the installed build as administrator and publishes it. The release is a zip of the program folder, stored as DocMiscType 100 and 101 rows.
- A workstation installed from the same staging folder has the same build id, so nothing happens.
- An older install pulls the published build.
- **An installer newer than the published build on a workstation that is not the update server is refused with exit code 174.** So:
  - upgrade the update server first;
  - other PCs then pull automatically;
  - the installer is only for new PCs and the update server.
- The Programs and Features version stays at the installer's version after a pull (cosmetic).
- After cap-4 merges, add `ImagingDeviceManager.dll` and `ImagingCapture.dll` to `ForkReleases.ListBuildIdFiles` (`ForkReleases.cs:80-92`). The build stamp inputs at `OpenDental.csproj:9565` (`@(ReferencePath)`) mitigate this today.

### First database
- **New office: there is no production path.**
  - `tools/New-DevDatabase.ps1:8-18` builds DevDbTool from source (it needs MSBuild).
  - It seeds `UnitTests/Resources/dump.txt`, a 21.1.9 demo database.
  - That seed contains **578 procedure code rows with ADA CDT descriptors** (table data from `:25253`; for example the D0120 row at `:25274`). This conflicts with ground rule 4.
  - The tool converts the seed to 24.3.63, adopts it, and migrates it to 26.1.70.
  - Admin has a blank password (`README.md:88`).
  - It refuses remote servers unless given `--allow-remote-server` (`tools/DevDbTool/Options.cs:154-156`).
- **Adopting an Open Dental database:**
  - Only 24.3.63 and 25.3.48 are allowed (`release-process.md:66`).
  - Either through the in-app `FormAdoptDatabase`, or headless with `AdoptionValidation adopt` (`release-process.md:68`).
  - Not part of the workstation installer. Switch-over deferred.

## 3. Versioning

- **OpenDental.exe:**
  - `AssemblyVersion("24.3.63.0")` (`OpenDental/Properties/AssemblyInfo.cs:37`). This is the compatibility version, `ForkVersions.CompatVersion` (`ForkVersions.cs:32`).
  - `AssemblyInformationalVersion("0.0")` (`:41`).
  - `AssemblyFileVersion` is commented out (`:43`).
  - Result: Win32 FileVersion 24.3.63.0 and ProductVersion 0.0, which a test asserts (`tests/ReleaseFlowTests/VersionSourceTests.cs:16-28`).
  - About shows the informational version (`FormAbout.cs:30,82-97`). Release manifests record the release version.
- **The 0.0 must also appear in:**
  - the installer: AppVersion 0.0, VersionInfoVersion 0.0.0.0, the Programs and Features DisplayVersion, the setup file name;
  - the git tag `v0.0`;
  - a shared assembly-info file for every shipped assembly, adding `AssemblyInformationalVersion("0.0")` and `AssemblyProduct("Open PMS - Dental")`.
- **Today the metadata is inconsistent:**
  - `AssemblyProduct("OpenDental")` and `Copyright © 2006` (`AssemblyInfo.cs:12-13`).
  - CentralManager says Company "Microsoft" (`CentralManager/Properties/AssemblyInfo.cs:11-13`).
  - UpdateFileCopier's FileVersion is 13.1.0.0 (`UpdateFileCopier/Properties/AssemblyInfo.cs:35-36`).
  - ServiceManager is 15.4.1.0.
- **Keep every AssemblyVersion and the 24.3.63.0 FileVersion as they are.**
- **Code that now sees "0.0" from `Application.ProductVersion` needs review:**
  - `ClaimConnect.cs:50,272,796` (the version sent to DentalXChange);
  - `FormUpdate.cs:26-27` (looks up UpdateHistory under "0.0");
  - `FormUpdateSetup.cs:102` (Recopy);
  - `DemandForce.cs:57`;
  - `CentralConnections.cs:243`.
- **OpenDentHL7 can't run on an adopted database.** `ServiceHL7.cs:104-108` compares its ProductVersion (24.3.63.0) with `ProgramVersion`, which is the sentinel 99.0.0.0 after adoption (`release-process.md:40`).
- **The final rename comes before the installer freeze.** Names are hard-coded at `UpdateFileCopier/FormMain.cs:83,428` and `ForkReleases.cs:81-91`.

## 4. Attribution and licence files

| Item | State |
|---|---|
| README | The attribution is correct (`README.md:3`). These statements are stale: `:51` ("Prebuilt DLLs ... ship in Required dlls") and `:130`. There are no install instructions for an office. |
| LICENSE | GPL v2 text (`LICENSE:1-2`). |
| NOTICE | Good, but `NOTICE:35-36` says the third-party licences are shown under Help > About > View Licenses, which is not accurate (see the next row). |
| About | The attribution is exact (`FormAbout.cs:27-28,76-77`). |
| In-app licence list (`FormLicense.cs:35-61`; the same list in `FormRegistrationKey.cs:45-68`, reachable from `FormUpdateSetup.cs:91`) | The default entry "OpenDental" is `opendentallicense.txt`: Open Dental's paid registration-key terms (line 3), which contradict the GPL. It also lists the removed API's EULA, the ADA CDT EULA, Sparks3D, a Visual Studio 2015 pre-release licence for stdole, and "Drifty" (the wrong licence for DotNetZip). Missing: the GPL v2 itself (`Resources/GPL.txt` exists but isn't in the resx), MySqlConnector, MimeKit, Google APIs, fo-dicom, DotNetZip, RdlEngine, NHunspell and the Hunspell dictionary, Direct Project, MSAL, WebView2 and the MIT System.* libraries. |
| Third-party notices file | None exists. |
| Installer | Needs a GPL page, the attribution page, `NOTICE` and the third-party notices in the program folder, and a source link for GPL v2 section 3 (the GitHub tag). |
| Public repository | This repository is a public GitHub fork of `OpenDental/opendental`, and it still carries the binaries in 1.1 and 1.2 at the tip and in its history. |

## 5. Installer tooling on this PC, and my recommendation

**Not installed:** Inno Setup, NSIS, WiX v3 to v6, the Visual Studio IDE (so no `.vdproj` installer projects; only Build Tools 2022 17.14 is installed), Advanced Installer.

**Available:**
- `C:\WINDOWS\system32\iexpress.exe`, `makecab.exe`, `msiexec.exe`;
- 7-Zip 26.05, with `7z.sfx` and `7zCon.sfx`;
- .NET SDK 9.0.319 (could host WiX v4 or later, after a download);
- MSBuild from Build Tools;
- `signtool.exe` from Windows SDK 10.0.26100 (x86, x64 and arm64);
- the ClickOnce bootstrapper packages (manifests only, no payloads);
- winget.

**Licence terms of the candidate tools:**
- Inno Setup has asked commercial users to buy a licence since 6.5.0; its FAQ says this is not strictly required.
- WiX v6 and later require the Open Source Maintenance Fee from revenue-generating organisations (above $10k under OSMF v1.1).

**Recommendation: Inno Setup 6.** You already leaned towards it. It needs your OK to download it and a decision on the commercial licence. If you'd rather add no new tool, the fallback is a PowerShell install script wrapped with IExpress or the 7-Zip SFX, with a hand-made Programs and Features entry and uninstaller. Not recommended: WiX (the fee, more effort), ClickOnce and MSBuild publish (per-user, clashes with UpdateFileCopier), and `.vdproj` (needs the full IDE).

**Outline:**

1. `tools/Build-Release.ps1`:
   - Build `OpenDental.Local.slnf` Release x86.
   - Stage the files.
   - Add the loader and Hunspell DLLs.
   - Run the deny-list and allow-list checks.
   - Generate the third-party notices.
   - Write SHA-256 hashes.
2. `installer/OpenPmsDental.iss`:
   - App name "Open PMS - Dental", version 0.0, install to `{commonpf32}\Open PMS - Dental`, admin rights, uninstaller files in `{app}\uninstall`.
   - LICENSE as the licence page, the attribution as the information page.
   - Prerequisite checks with silent `vc_redist.x86` and WebView2 installs; the .NET 4.8 check.
   - The configuration page writes `FreeDentalConfig.xml` only if it doesn't exist, and sets the users-modify permission on that file.
   - Start Menu shortcut, optional desktop shortcut.
   - Uninstall deletes the whole program folder.
   - Optional signing.
   - Silent switches for mass install.
3. Tag `v0.0` and make a GitHub Release with the installer, the hashes and the source archive, after your OK.

Sources: [jrsoftware.org/isorder.php](https://jrsoftware.org/isorder.php), [jrsoftware.org/files/is6.5-whatsnew.htm](https://jrsoftware.org/files/is6.5-whatsnew.htm), [docs.firegiant.com/wix/osmf](https://docs.firegiant.com/wix/osmf/), [github.com/wixtoolset/wix/releases](https://github.com/wixtoolset/wix/releases)