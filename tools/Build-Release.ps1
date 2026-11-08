<#
.SYNOPSIS
Builds an Open PMS - Dental release: a Release x86 build, one checked staging folder, SHA256SUMS.txt and the Inno Setup installer.

.DESCRIPTION
1. Builds OpenDental.Local.slnf as Release x86, through the shared build lane when -BuildLane is given. PDFsharp/MigraDoc and
   WeCantSpell.Hunspell build from their source in the git submodules under third-party\ (docs\third-party-source-builds.md).
2. Checks OpenPmsDental\bin\Release: every file must be on the allow-list below or be one the release never ships (debug symbols,
   XML documentation). A file on the deny-list, an unknown file or a sub folder fails the script.
3. Stages the allowed files into one folder (<OutDir>\staging), with LICENSE, LICENSE-GPL-3.0.txt, LICENSE-AGPL-3.0.txt,
   LICENSE-LGPL-2.1.txt, NOTICE, THIRD-PARTY-NOTICES.txt, db\init\new-database.sql and db\README.md, and checks the staging folder again: allow-list, deny-list, every allowed file present,
   every third-party file named in THIRD-PARTY-NOTICES.txt, every file built from this repository equal to its project's own output, the
   native files x86 and free of the debug C runtime, and OpenPmsDental.exe's versions (file version 24.3.63.0, the database compatibility version; product version the
   release version of SharedAssemblyInfo.cs).
4. Prints the build id (ForkReleases.ComputeBuildId over ForkReleases.ListBuildIdFiles) and writes <OutDir>\SHA256SUMS.txt.
5. Writes the setup's information page (installer\setup-info.txt with NOTICE) and License Agreement page (installer\setup-license.txt:
   how the installed program is licensed, then LICENSE-GPL-3.0.txt) into <OutDir>, compiles installer\OpenPmsDental.iss with Inno Setup 6
   into <OutDir>, and adds the setup's SHA-256 to SHA256SUMS.txt.
6. Writes the source archive, <OutDir>\OpenPmsDental-<version>-source.zip: the repository at the release commit (git archive HEAD) with
   every submodule at the commit the release commit pins (PDFsharp/MigraDoc and WeCantSpell.Hunspell), and SOURCE-COMMITS.txt naming them,
   so the Corresponding Source of everything the setup ships is published with the release even if an upstream repository goes away. Its
   SHA-256 goes into SHA256SUMS.txt. A submodule that is not checked out at its pinned commit fails the script: the shipped files were
   built from the checkout.
7. With -CopyTo, copies the setup, the source archive, SHA256SUMS.txt and the release notes (docs\release-notes\v<version>.md) there.

The staging folder is exactly what the installer puts in the program folder. The uninstaller goes to the program folder's "uninstall"
sub folder, never the top level: a release published from the update server holds every top-level file of the program folder.

Nothing is downloaded and nothing connects to a database.

Exit codes: 0 done; 1 a check failed; 2 the build failed; 3 the installer did not compile; 4 a tool or file is missing (git, a submodule).

.PARAMETER BuildLane
Path of the shared build lane script (odbuild.ps1) of this PC. The build then runs through it, so only one build runs at a time.
Without it, MSBuild is found with vswhere.

.PARAMETER LaneWaitMinutes
How long the build lane waits for another build to finish. Default 15.

.PARAMETER SkipBuild
Use the Release output that is already there. It must come from a Release build of the current source.

.PARAMETER MSBuildPath
Full path of MSBuild.exe when not building through -BuildLane. Default: found with vswhere.

.PARAMETER OutDir
Where the staging folder, SHA256SUMS.txt and the setup go. Default: installer\bin in this repository (ignored by git).

.PARAMETER SkipInstaller
Stage and check only; do not compile the installer.

.PARAMETER Iscc
Full path of Inno Setup 6's ISCC.exe. Default: Inno Setup 6 under Program Files (x86) or %LOCALAPPDATA%\Programs.

.PARAMETER CopyTo
A folder (created if missing) to copy the setup, SHA256SUMS.txt and the release notes to.

.PARAMETER CheckFolder
Only run the list checks (allow-list, deny-list, required files, third-party notices) on this folder, as if it were the staging folder,
and print its build id. Nothing is built or written. Used by tests\ReleaseFlowTests\InstallerTests.cs.

.PARAMETER ListFiles
Only print the allow-list, one "own|name", "third|name" or "doc|path" line per file.

.PARAMETER WriteSetupTexts
Only write the setup's two pages, setup-info.txt and setup-license.txt, into this folder (created if missing), as a release writes them
into <OutDir>. Nothing is built or checked. Used by tests\ReleaseFlowTests\InstallerTests.cs.

.PARAMETER WriteSourceArchive
Only write the source archive (step 6) into this folder (created if missing) and print its path. Nothing is built or checked. Used by
tests\PdfLibraryTests\SourceArchiveTests.cs.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Build-Release.ps1 -BuildLane C:\path\to\odbuild.ps1 -CopyTo D:\releases\v0.0
#>
[CmdletBinding(PositionalBinding=$false)]
param(
	[string]$BuildLane='',
	[int]$LaneWaitMinutes=15,
	[switch]$SkipBuild,
	[string]$MSBuildPath='',
	[string]$OutDir='',
	[switch]$SkipInstaller,
	[string]$Iscc='',
	[string]$CopyTo='',
	[string]$CheckFolder='',
	[switch]$ListFiles,
	[string]$WriteSetupTexts='',
	[string]$WriteSourceArchive=''
)
Set-StrictMode -Version 2
$ErrorActionPreference='Stop'
$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path

#region The lists
#The database compatibility version. Every AssemblyVersion and OpenPmsDental.exe's file version stay on it (ForkVersions.CompatVersion).
$CompatVersion='24.3.63.0'

#Files built from this repository, at the top of the program folder. Value: the project output (repository-relative) the staged file must
#equal, so that a prebuilt file of the same name can never ship in its place (Open Dental's closed ImagingDeviceManager.dll had this
#name; ODR.dll ships only when built from source; Direct2dWrapper.dll must be the Release build). '' = OpenPmsDental's own output or a
#generated file.
$OwnFiles=[ordered]@{
	'OpenPmsDental.exe'=''
	'OpenPmsDental.exe.config'=''
	'OpenPmsDental.XmlSerializers.dll'=''
	'OpenPmsDentalBusiness.dll'='OpenPmsDentalBusiness\bin\Release'
	'OpenPmsDentalBusiness.dll.config'=''
	'OpenPmsDentalBusiness.XmlSerializers.dll'='OpenPmsDentalBusiness\bin\Release'
	'CodeBase.dll'='CodeBase\bin\Release'
	'DataConnectionBase.dll'='DataConnectionBase\bin\Release'
	'WpfControlsOD.dll'='WpfControlsOD\bin\Release'
	'WpfControlsOD.dll.config'=''
	'OpenPmsDentalGraph.dll'='OpenPmsDentalGraph\bin\Release'
	'OpenPmsDentalGraph.dll.config'=''
	'OpenPmsDentalStorage.dll'='OpenPmsDentalStorage\bin\Release'
	'SparksToothChart.dll'='SparksToothChart\bin\Release'
	'SparksToothChart.dll.config'=''
	'ODCrypt.dll'='ODCrypt\bin\x86\Release'
	'PasswordVaultWrapper.dll'='PasswordVaultWrapper\bin\x86\Release'
	'ImagingDeviceManager.dll'='ImagingDeviceManager\bin\Release'
	'ImagingCapture.dll'='ImagingCapture\bin\Release'
	'ODR.dll'='ODR\bin\Release'
	'Direct2dWrapper.dll'='Direct2dWrapper\bin\Release'
	'ServiceManager.exe'='ServiceManager\bin\Release'
	'ServiceManager.exe.config'=''
	'UpdateFileCopier.exe'='UpdateFileCopier\bin\Release'
	#The connection settings tool (docs/specs/db-connection-security.md); the installer runs it for the connection page.
	'OpenPmsDentalDbSettings.exe'='OpenPmsDentalDbSettings\bin\Release'
	'OpenPmsDentalDbSettings.exe.config'=''
}

#Third-party files. Each must be named in THIRD-PARTY-NOTICES.txt. Value: where its bytes come from, a byte copy of the repository's
#"Required dlls" file or the project output it is built from.
$ThirdPartyFiles=[ordered]@{}
foreach($name in @('BouncyCastle.Cryptography.dll','Google.Apis.dll','Google.Apis.Auth.dll',
	'Google.Apis.Core.dll','Google.Apis.Gmail.v1.dll','Health.Direct.Agent.dll','Health.Direct.Common.dll','Health.Direct.ResolverPlugins.dll',
	'MailKit.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.Bcl.Cryptography.dll',
	'Microsoft.Extensions.DependencyInjection.Abstractions.dll','Microsoft.Extensions.Logging.Abstractions.dll','Microsoft.Identity.Client.dll',
	'Microsoft.IdentityModel.Abstractions.dll','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll',
	'Microsoft.Web.WebView2.Wpf.dll','MimeKit.dll','MySqlConnector.dll',
	'Newtonsoft.Json.dll','Renci.SshNet.dll','SharpDX.dll','SharpDX.Direct3D9.dll',
	'SharpDX.Mathematics.dll','System.Buffers.dll','System.Diagnostics.DiagnosticSource.dll','System.Formats.Asn1.dll','System.IO.Pipelines.dll',
	'System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Text.Encodings.Web.dll',
	'System.Text.Json.dll','System.Threading.Tasks.Extensions.dll','Tao.OpenGl.dll','Tao.Platform.Windows.dll',
	'WebView2Loader.dll'))
{
	$ThirdPartyFiles[$name]='Required dlls'
}
$ThirdPartyFiles['RdlEngine.dll']='RdlEngine\bin\Release'
$ThirdPartyFiles['RdlViewer.dll']='RdlViewer\bin\Release'
#Built here from the upstream source in the git submodules under third-party\ (docs\third-party-source-builds.md): PDFsharp and MigraDoc
#6.5.0 (third-party\PDFsharp, with this project's PdfSharpLicense.cs in place of empira's wrapper around its closed PdfSharp.License.dll,
#which no release ships) and WeCantSpell.Hunspell 7.0.1 (third-party\WeCantSpell.Hunspell).
foreach($name in @('PdfSharp-gdi','PdfSharp.Charting-gdi','PdfSharp.Shared','PdfSharp.System','MigraDoc.DocumentObjectModel','MigraDoc.Rendering-gdi',
	'WeCantSpell.Hunspell'))
{
	$ThirdPartyFiles[$name+'.dll']='third-party\build\'+$name+'\bin\Release'
}

#Files from the repository: the path in the program folder, and the repository-relative source. LICENSE-LGPL-2.1.txt is the licence
#text the LGPL 2.1 asks for with every copy of the libraries the release ships under it (RdlEngine.dll, RdlViewer.dll,
#WeCantSpell.Hunspell.dll and the en_US word list in WpfControlsOD.dll; THIRD-PARTY-NOTICES.txt). PDFsharp and MigraDoc 6.5 are under the
#GNU AGPL version 3, so the program as distributed is conveyed under the GNU GPL version 3 (its own code is "version 2 or later"): both
#texts ship, LICENSE-AGPL-3.0.txt and LICENSE-GPL-3.0.txt (NOTICE).
$DocumentFiles=[ordered]@{
	'LICENSE'='LICENSE'
	'LICENSE-GPL-3.0.txt'='LICENSE-GPL-3.0.txt'
	'LICENSE-AGPL-3.0.txt'='LICENSE-AGPL-3.0.txt'
	'LICENSE-LGPL-2.1.txt'='LICENSE-LGPL-2.1.txt'
	'NOTICE'='NOTICE'
	'THIRD-PARTY-NOTICES.txt'='THIRD-PARTY-NOTICES.txt'
	'db\init\new-database.sql'='db\init\new-database.sql'
	'db\README.md'='db\README.md'
}
$StagedFolders=@('db','db\init')

#Files the program cannot run or ship without, and why. They are on the allow-list too; these get a reason when missing.
$RequiredReasons=[ordered]@{
	'WebView2Loader.dll'='Microsoft.Web.WebView2.Core.dll loads it for every WebView2 window (CodeBase/Controls/ODWebView2.cs)'
	'WeCantSpell.Hunspell.dll'='the spell check of every ODtextBox and TextRich (WpfControlsOD/Logic/SpellCheckL.cs) uses it'
	'ODR.dll'='RDL report definitions load it by file name (<CodeModule>ODR.dll</CodeModule>)'
	'LICENSE-LGPL-2.1.txt'='the LGPL 2.1 (sections 1 and 4) asks for its text with every copy of RdlEngine.dll, RdlViewer.dll and WeCantSpell.Hunspell.dll'
	'LICENSE-AGPL-3.0.txt'='the GNU AGPL 3.0 (section 4) asks for its text with every copy of PDFsharp and MigraDoc'
	'LICENSE-GPL-3.0.txt'='the program with PDFsharp and MigraDoc is conveyed under the GNU GPL 3.0, whose section 4 asks for its text with every copy'
	'PdfSharp-gdi.dll'='every PDF the program makes or reads goes through it; it holds the AGPL licence check that PdfSharpSetup.Init satisfies'
}

#The source archive's name and top folder ({0}: the release version). It holds the repository at the release commit and every submodule
#(third-party\PDFsharp, third-party\WeCantSpell.Hunspell) at the commit the release commit pins (git ls-tree HEAD, .gitmodules).
$SourceArchiveName='OpenPmsDental-{0}-source'

#Native files: they must be x86 and must not need the debug C runtime, which may not be redistributed.
$NativeFiles=@('Direct2dWrapper.dll','WebView2Loader.dll')
$DebugRuntimeNames=@('VCRUNTIME140D.dll','MSVCP140D.dll','ucrtbased.dll','VCRUNTIME140_1D.dll')

#Files of OpenPmsDental's Release output that are never shipped.
$NotShippedPatterns=@('*.pdb','OpenPmsDental.xml','OpenPmsDentalBusiness.xml','CodeBase.xml')

#ForkReleases.ListBuildIdFiles: the build id is the SHA-256 over these files' lower-case names and SHA-256 values.
$BuildIdFiles=@('OpenPmsDental.exe','OpenPmsDentalBusiness.dll','CodeBase.dll','DataConnectionBase.dll','WpfControlsOD.dll',
	'OpenPmsDentalGraph.dll','OpenPmsDentalStorage.dll','SparksToothChart.dll','ODCrypt.dll','PasswordVaultWrapper.dll','ImagingDeviceManager.dll',
	'ImagingCapture.dll','UpdateFileCopier.exe')

#Never shipped, at any depth (matched against file and folder names, ignoring case), with the reason (report-release.md section 2;
#docs/port-notes/BIN-A.md, BIN-B.md).
$DenyList=@(
	@('xImageDeviceManager.dll',"Open Dental's closed Suni capture library (removed in BIN-A)"),
	@('SuniUSB.dll',"Suni's commercial USB driver that Open Dental shipped (removed in BIN-A)"),
	@('VBbridges.dll',"Open Dental's closed VB.NET bridge helper (removed in BIN-A)"),
	@('VideoCamLib.dll',"Open Dental's closed video capture wrapper (removed in the capture work)"),
	@('Sparks3D*',"Open Dental's Sparks3D tooth chart, under Open Dental's own licence (removed in BIN-A)"),
	@('EZT4*.dll','Atalasoft EZTwain Pro, a commercial per-developer licence'),
	@('Eztwain*','Atalasoft EZTwain Pro, a commercial per-developer licence'),
	@('Bridges.dll',"Open Dental's closed bridge library (removed in the decoupling)"),
	@('CDT.dll',"Open Dental's closed CDT library (removed in the decoupling)"),
	@('OpenDentalHelp.dll',"Open Dental's closed help library (removed in the decoupling)"),
	@('VirtualWeb.dll',"Open Dental's closed browser-hosting library (removed in the decoupling)"),
	@('ODApi.dll',"Open Dental's closed API library (removed in the decoupling)"),
	@('DpsPos.dll',"Open Dental's closed payment terminal library (removed in the decoupling)"),
	@('stdole.dll',"redistributable only through Microsoft's Office PIA installer"),
	@('Interop.*.dll','COM interop generated from a proprietary type library (Office, Adobe, Intuit, Schick)'),
	@('AxInterop.*.dll','ActiveX interop generated from a proprietary type library (Adobe)'),
	@('*.winmd','Windows SDK metadata, part of Windows'),
	@('installutil.exe','part of the .NET Framework'),
	@('vcruntime*.dll','the Visual C++ runtime comes from vc_redist.x86.exe'),
	@('msvcp*.dll','the Visual C++ runtime comes from vc_redist.x86.exe'),
	@('msvcr*.dll','an old Visual C++ runtime, not redistributed with the program'),
	@('ucrtbase*.dll','the Universal C runtime is part of Windows'),
	@('Microsoft.DirectX*.dll',"DirectX, redistributable only through Microsoft's installer"),
	@('d3dx*.dll',"DirectX, redistributable only through Microsoft's installer"),
	@('RS232.dll','unknown author and licence (replaced in BIN-B)'),
	@('SigPlusNET.dll','Topaz: no published redistribution terms; Topaz signature pads were removed in stage 0 of the .NET 10 move (S0-2a)'),
	@('zxing*','a dead reference (removed in BIN-B)'),
	@('Avalara*','the AvaTax SDK, a dead reference (removed in BIN-B)'),
	@('Ionic.*','DotNetZip: MS-PL, abandoned, CVE-2024-48510 (replaced in BIN-B)'),
	@('Dicom.*.dll','fo-dicom 4 (MS-PL): replaced by the in-house DICOM reader (docs/imaging/dicom-reader.md)'),
	@('NDde.dll','NDde, under its own shared source licence, which does not fit the GPL: removed with the DentX, Dexis Integrator and Vipersoft program links (owner decision of 2026-11-07, REL-FILES 2)'),
	@('fo-dicom*','fo-dicom (MS-PL): replaced by the in-house DICOM reader (docs/imaging/dicom-reader.md)'),
	@('DotNetZip*','DotNetZip: MS-PL, abandoned, CVE-2024-48510 (replaced in BIN-B)'),
	@('Tamir.SharpSSH.dll','SharpSSH, unmaintained since 2013 (replaced by SSH.NET in the tier 1 library updates)'),
	@('Org.Mentalis.Security.dll',"SharpSSH's cryptography library (removed with SharpSSH)"),
	@('DiffieHellman.dll',"SharpSSH's key exchange library (removed with SharpSSH)"),
	@('OpenPop.dll',"OpenPop.NET, unmaintained (replaced by MailKit's POP3 client in the tier 1 library updates)"),
	@('BouncyCastle.Crypto.dll',"MimeKit 2's Bouncy Castle 1.8; MimeKit 4 uses BouncyCastle.Cryptography.dll"),
	@('Google.Apis*.PlatformServices.dll','the Google APIs 1.44 platform shims; the library has none since 1.50'),
	@('NHunspell.dll','NHunspell 0.9.6, a wrapper of the native Hunspell 1.2 (replaced by the managed WeCantSpell.Hunspell in the library updates)'),
	@('Hunspellx*.dll',"NHunspell's native Hunspell 1.2 (removed with NHunspell; WeCantSpell.Hunspell needs no native file)"),
	@('PdfSharp.License.dll',"empira's closed licence check of PDFsharp 6.5, whose source is not published; the PdfSharp-gdi.dll built here holds the AGPL licence check itself (third-party\build\PdfSharp-gdi\PdfSharpLicense.cs)"),
	@('TWAINDSM.dll',"the TWAIN data source manager comes with the scanner's driver; one in the program folder would be loaded first"),
	@('FreeDentalConfig.xml','the connection settings of each computer; the installer writes it, a release never carries it'),
	@('OpenDentalServerConfig.xml','the Middle Tier settings of each server'),
	@('ForkRelease.manifest','written by the release channel on each workstation'),
	@('*.pdb','debug symbols'),
	@('unins*.exe','the uninstaller lives in the uninstall sub folder; a release publishes every top-level file'),
	@('unins*.dat','the uninstaller lives in the uninstall sub folder; a release publishes every top-level file'),
	@('unins*.msg','the uninstaller lives in the uninstall sub folder; a release publishes every top-level file'),
	@('OpenDent*.exe','a file of the pre-rename names; the program is OpenPmsDental.exe'),
	@('OpenDent*.dll','a file of the pre-rename names; the program uses OpenPmsDentalBusiness.dll'),
	#ForkReleases.ListRetiredFiles: files of an earlier release that this one no longer ships (ReleaseFlowTests checks the two lists agree).
	@('OpenPmsDentalCloud.dll','the storage library of v0.0, renamed OpenPmsDentalStorage.dll on 2026-11-02; a leftover of an older build'),
	@('PdfSharp.dll','PDFsharp 1.2 of v0.0; PDFsharp 6.5 ships as PdfSharp-gdi.dll'),
	@('PdfSharp.Charting.dll','PDFsharp 1.2 of v0.0; PDFsharp 6.5 ships as PdfSharp.Charting-gdi.dll'),
	@('MigraDoc.Rendering.dll','MigraDoc 1.2 of v0.0; MigraDoc 6.5 ships as MigraDoc.Rendering-gdi.dll')
)
#endregion The lists

#region Helpers
function Get-DenyReason([string]$name){
	foreach($entry in $DenyList){
		if($name -like $entry[0]){
			return $entry[1]
		}
	}
	return $null
}

function Get-Sha256([string]$path){
	return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

#The same computation as ForkReleases.ComputeBuildId: the build id files that exist, ordered ignoring case, as "name|sha256\n", hashed.
function Get-BuildId([string]$folder){
	$sorted=New-Object 'System.Collections.Generic.List[string]'
	foreach($name in $BuildIdFiles){ $sorted.Add($name) }
	$sorted.Sort([StringComparer]::OrdinalIgnoreCase)
	$text=New-Object System.Text.StringBuilder
	foreach($name in $sorted){
		$path=Join-Path $folder $name
		if(Test-Path -LiteralPath $path -PathType Leaf){
			[void]$text.Append($name.ToLowerInvariant()+'|'+(Get-Sha256 $path)+"`n")
		}
	}
	$sha=[System.Security.Cryptography.SHA256]::Create()
	try{
		$bytes=$sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text.ToString()))
	}
	finally{ $sha.Dispose() }
	return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
}

#The text of every "Files:" field of THIRD-PARTY-NOTICES.txt, with its continuation lines (the format LicenseList.ParseNotices reads).
function Get-NoticedFilesText([string]$noticesPath){
	$builder=New-Object System.Text.StringBuilder
	$inFiles=$false
	foreach($line in [System.IO.File]::ReadAllLines($noticesPath)){
		if($line -match '^--- '){ $inFiles=$false; continue }
		if($line -match '^([A-Za-z]+):\s*(.*)$'){
			$inFiles=($Matches[1] -eq 'Files')
			if($inFiles){ [void]$builder.Append(' '+$Matches[2]) }
			continue
		}
		if($inFiles -and $line -match '^\s+\S'){
			[void]$builder.Append(' '+$line.Trim())
			continue
		}
		$inFiles=$false
	}
	return $builder.ToString()
}

#The PE machine field of the file (0x14C is x86), or -1 when it is not a PE file.
function Get-PeMachine([string]$path){
	$bytes=[System.IO.File]::ReadAllBytes($path)
	if($bytes.Length -lt 64 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A){ return -1 }
	$offset=[BitConverter]::ToInt32($bytes,0x3C)
	if($offset -lt 0 -or $offset+6 -gt $bytes.Length){ return -1 }
	if($bytes[$offset] -ne 0x50 -or $bytes[$offset+1] -ne 0x45 -or $bytes[$offset+2] -ne 0 -or $bytes[$offset+3] -ne 0){ return -1 }
	return [int][BitConverter]::ToUInt16($bytes,$offset+4)
}

function Read-ReleaseVersion {
	$text=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'SharedAssemblyInfo.cs'))
	$match=[regex]::Match($text,'(?m)^\s*\[assembly:\s*AssemblyInformationalVersion\("([^"]+)"\)\]')
	if(-not $match.Success){ throw 'SharedAssemblyInfo.cs has no AssemblyInformationalVersion.' }
	return $match.Groups[1].Value
}

function Read-InstallerVersion {
	$text=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'installer\OpenPmsDental.iss'))
	$match=[regex]::Match($text,'(?m)^#define AppVersion "([^"]+)"')
	if(-not $match.Success){ throw 'installer\OpenPmsDental.iss has no #define AppVersion.' }
	return $match.Groups[1].Value
}

function Get-AllowedPaths {
	$list=New-Object 'System.Collections.Generic.List[string]'
	foreach($key in $OwnFiles.Keys){ $list.Add($key) }
	foreach($key in $ThirdPartyFiles.Keys){ $list.Add($key) }
	foreach($key in $DocumentFiles.Keys){ $list.Add($key) }
	return $list
}

#The setup's two pages, written into the folder: setup-info.txt (the information page: installer\setup-info.txt with NOTICE) and
#setup-license.txt (the License Agreement page: installer\setup-license.txt, which says how the installed program is licensed, with
#LICENSE-GPL-3.0.txt, the license the program is conveyed under as installed). UTF-8 with a byte order mark and CRLF line ends, as Inno
#Setup reads them. Returns the two paths.
function Write-SetupTexts([string]$folder,[string]$version){
	New-Item -ItemType Directory -Force -Path $folder | Out-Null
	$utf8Bom=New-Object System.Text.UTF8Encoding($true)
	$notice=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'NOTICE'))
	$info=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'installer\setup-info.txt'))
	$info=$info.Replace('@VERSION@',$version).Replace('@NOTICE@',$notice.TrimEnd())
	$info=($info -replace "`r`n","`n") -replace "`n","`r`n"
	$infoPath=Join-Path $folder 'setup-info.txt'
	[System.IO.File]::WriteAllText($infoPath,$info,$utf8Bom)
	$gpl3=[System.IO.File]::ReadAllText((Join-Path $RepoRoot $DocumentFiles['LICENSE-GPL-3.0.txt']))
	$license=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'installer\setup-license.txt'))
	$license=$license.Replace('@VERSION@',$version).Replace('@GPL3@',$gpl3.Trim([char[]]"`r`n"))
	$license=($license -replace "`r`n","`n") -replace "`n","`r`n"
	$licensePath=Join-Path $folder 'setup-license.txt'
	[System.IO.File]::WriteAllText($licensePath,$license,$utf8Bom)
	return @($infoPath,$licensePath)
}

#The checks of a staging folder. -Full adds the checks that need the real build: built-from-source copies, native files, versions.
#Returns the problems found; none means the folder may ship.
function Test-StagingFolder([string]$folder,[switch]$Full){
	$problems=New-Object 'System.Collections.Generic.List[string]'
	$folder=(Resolve-Path -LiteralPath $folder).Path.TrimEnd('\')
	$allowed=Get-AllowedPaths
	$allowedSet=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
	foreach($path in $allowed){ [void]$allowedSet.Add($path) }
	$folderSet=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
	foreach($path in $StagedFolders){ [void]$folderSet.Add($path) }
	#The lists themselves: nothing allowed may be denied.
	foreach($path in $allowed){
		$reason=Get-DenyReason (Split-Path $path -Leaf)
		if($reason){ $problems.Add("The allow-list names a denied file: $path ($reason)") }
	}
	foreach($item in Get-ChildItem -LiteralPath $folder -Recurse -Force){
		$relative=$item.FullName.Substring($folder.Length).TrimStart('\')
		$reason=Get-DenyReason $item.Name
		if($item.PSIsContainer){
			if($reason){ $problems.Add("Denied folder: $relative ($reason)") }
			elseif(-not $folderSet.Contains($relative)){ $problems.Add("Unexpected folder: $relative") }
			continue
		}
		if($reason){ $problems.Add("Denied file: $relative ($reason)") }
		elseif(-not $allowedSet.Contains($relative)){ $problems.Add("Not on the allow-list: $relative") }
	}
	foreach($path in $allowed){
		if(-not (Test-Path -LiteralPath (Join-Path $folder $path) -PathType Leaf)){
			if($RequiredReasons.Contains($path)){ $problems.Add("Missing: $path ($($RequiredReasons[$path]))") }
			else{ $problems.Add("Missing: $path") }
		}
	}
	#Every third-party file is named in the notices file that ships with it.
	$noticesPath=Join-Path $folder 'THIRD-PARTY-NOTICES.txt'
	if(Test-Path -LiteralPath $noticesPath -PathType Leaf){
		$noticed=Get-NoticedFilesText $noticesPath
		foreach($name in $ThirdPartyFiles.Keys){
			if(-not [regex]::IsMatch($noticed,'(?<![\w.])'+[regex]::Escape($name)+'(?![\w.])',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)){
				$problems.Add("Not named in THIRD-PARTY-NOTICES.txt: $name")
			}
		}
	}
	if(-not $Full){
		return ,$problems
	}
	#Built from this repository, or byte copies of the repository's own files (repository-relative source paths).
	$sources=[ordered]@{}
	foreach($key in $OwnFiles.Keys){ if($OwnFiles[$key]){ $sources[$key]=$OwnFiles[$key]+'\'+$key } }
	foreach($key in $ThirdPartyFiles.Keys){ $sources[$key]=$ThirdPartyFiles[$key]+'\'+$key }
	foreach($key in $DocumentFiles.Keys){ $sources[$key]=$DocumentFiles[$key] }
	foreach($name in $sources.Keys){
		$staged=Join-Path $folder $name
		$source=Join-Path $RepoRoot $sources[$name]
		if(-not (Test-Path -LiteralPath $staged -PathType Leaf)){ continue }
		if(-not (Test-Path -LiteralPath $source -PathType Leaf)){
			$problems.Add("No source to compare with: $name (expected $($sources[$name]))")
		}
		elseif((Get-Sha256 $staged) -ne (Get-Sha256 $source)){
			$problems.Add("Not the file of this repository: $name differs from $($sources[$name])")
		}
	}
	foreach($name in $NativeFiles){
		$path=Join-Path $folder $name
		if(-not (Test-Path -LiteralPath $path -PathType Leaf)){ continue }
		$machine=Get-PeMachine $path
		if($machine -ne 0x14C){ $problems.Add(("Not an x86 file: {0} (PE machine 0x{1:X})" -f $name,$machine)) }
		$text=[System.Text.Encoding]::GetEncoding(28591).GetString([System.IO.File]::ReadAllBytes($path))
		foreach($debugName in $DebugRuntimeNames){
			if($text.IndexOf($debugName,[StringComparison]::OrdinalIgnoreCase) -ge 0){
				$problems.Add("Needs the debug C runtime ($debugName), which may not be redistributed: $name (a Debug build?)")
			}
		}
	}
	$releaseVersion=Read-ReleaseVersion
	foreach($name in @('OpenPmsDental.exe','OpenPmsDentalBusiness.dll')){
		$path=Join-Path $folder $name
		if(-not (Test-Path -LiteralPath $path -PathType Leaf)){ continue }
		$info=[System.Diagnostics.FileVersionInfo]::GetVersionInfo($path)
		if($info.FileVersion -ne $CompatVersion){ $problems.Add("$name file version is $($info.FileVersion), not the compatibility version $CompatVersion") }
		if($info.ProductVersion -ne $releaseVersion){ $problems.Add("$name product version is $($info.ProductVersion), not the release version $releaseVersion") }
		if($info.ProductName -ne 'Open PMS - Dental'){ $problems.Add("$name product name is $($info.ProductName)") }
	}
	return ,$problems
}

function Write-Problems([System.Collections.Generic.List[string]]$problems,[string]$what){
	Write-Host "$what failed, $($problems.Count) problem(s):"
	foreach($problem in $problems){ Write-Host "  $problem" }
}

function Write-Sums([string]$sumsPath,[string[]]$lines){
	$utf8=New-Object System.Text.UTF8Encoding($false)
	[System.IO.File]::WriteAllText($sumsPath,(($lines -join "`n")+"`n"),$utf8)
}

function Find-MSBuild {
	$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
	if(-not (Test-Path $vswhere)){ return $null }
	$found=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
	return $found
}

#git from the PATH, or $null.
function Find-Git {
	$command=Get-Command 'git.exe' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
	if($command){ return $command.Source }
	return $null
}

#Runs git in the folder and returns its output lines. Throws with git's own message when git fails.
function Invoke-Git([string]$folder,[string[]]$arguments){
	$saved=$ErrorActionPreference
	$ErrorActionPreference='Continue'
	try{
		$output=@(& $script:GitPath -C $folder @arguments 2>&1 | ForEach-Object { "$_" })
		$exitCode=$LASTEXITCODE
	}
	finally{ $ErrorActionPreference=$saved }
	if($exitCode -ne 0){ throw "git $($arguments -join ' ') failed in ${folder}: $($output -join ' ')" }
	return $output
}

#The submodules the release commit pins: their path, the pinned commit (the gitlinks of git ls-tree HEAD) and their address in the
#release commit's .gitmodules.
function Get-PinnedSubmodules {
	$list=New-Object 'System.Collections.Generic.List[object]'
	foreach($line in Invoke-Git $RepoRoot @('ls-tree','-r','HEAD')){
		if($line -match '^160000 commit ([0-9a-f]{40})\t(.+)$'){
			$commit=$Matches[1]
			$path=$Matches[2]
			$url=@(Invoke-Git $RepoRoot @('config','--blob','HEAD:.gitmodules','--get',"submodule.$path.url"))[0]
			$list.Add([pscustomobject]@{ Path=$path; Commit=$commit; Url=$url })
		}
	}
	return ,$list
}

#Whether every submodule is checked out at the commit the release commit pins: the release's files are built from the checkout and the
#source archive holds the pinned commits, so they must be the same. Returns one line per problem; a problem whose line starts with
#"Missing:" means the submodule is not checked out at all.
function Test-SubmoduleCheckouts {
	$problems=New-Object 'System.Collections.Generic.List[string]'
	$submodules=Get-PinnedSubmodules
	foreach($submodule in $submodules){
		$folder=Join-Path $RepoRoot $submodule.Path
		if(-not (Test-Path -LiteralPath (Join-Path $folder '.git'))){
			$problems.Add("Missing: the submodule $($submodule.Path) is not checked out. Run git -c protocol.file.allow=always submodule update --init (docs\third-party-source-builds.md).")
			continue
		}
		$checkedOut=@(Invoke-Git $folder @('rev-parse','HEAD'))[0].Trim()
		if($checkedOut -ne $submodule.Commit){
			$problems.Add("The submodule $($submodule.Path) is checked out at $checkedOut, but the release commit pins $($submodule.Commit). Run git submodule update, or commit the new pin.")
		}
	}
	return ,$problems
}

#Writes <folder>\OpenPmsDental-<version>-source.zip and returns its path: the repository at the release commit (git archive HEAD) and every
#submodule at the commit HEAD pins, under one top folder, with SOURCE-COMMITS.txt naming the commits. git archive writes each part; the parts
#are joined into one zip (an entry two parts share, the folder of a submodule, is written once).
function Write-SourceArchive([string]$folder,[string]$version){
	Add-Type -AssemblyName System.IO.Compression
	Add-Type -AssemblyName System.IO.Compression.FileSystem
	New-Item -ItemType Directory -Force -Path $folder | Out-Null
	$folder=(Resolve-Path -LiteralPath $folder).Path
	$name=$SourceArchiveName -f $version
	$archivePath=Join-Path $folder ($name+'.zip')
	$commit=@(Invoke-Git $RepoRoot @('rev-parse','HEAD'))[0].Trim()
	$submodules=Get-PinnedSubmodules
	if(@(Invoke-Git $RepoRoot @('status','--porcelain','--untracked-files=no')).Count -gt 0){
		Write-Host "Warning: the working tree has changes that are not committed. The source archive holds the commit $commit."
	}
	$temp=Join-Path ([System.IO.Path]::GetTempPath()) ('OpenPmsDental-source-'+[guid]::NewGuid().ToString('N'))
	New-Item -ItemType Directory -Path $temp | Out-Null
	try{
		$parts=New-Object 'System.Collections.Generic.List[string]'
		$part=Join-Path $temp 'repository.zip'
		[void](Invoke-Git $RepoRoot @('archive','--format=zip',"--prefix=$name/",'-o',$part,'HEAD'))
		$parts.Add($part)
		$index=0
		foreach($submodule in $submodules){
			$index++
			$part=Join-Path $temp "submodule$index.zip"
			[void](Invoke-Git (Join-Path $RepoRoot $submodule.Path) @('archive','--format=zip',"--prefix=$name/$($submodule.Path)/",'-o',$part,$submodule.Commit))
			$parts.Add($part)
		}
		if(Test-Path -LiteralPath $archivePath){ Remove-Item -LiteralPath $archivePath -Force }
		$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
		$zipTarget=[System.IO.Compression.ZipFile]::Open($archivePath,[System.IO.Compression.ZipArchiveMode]::Create)
		try{
			foreach($part in $parts){
				$zipSource=[System.IO.Compression.ZipFile]::OpenRead($part)
				try{
					foreach($entry in $zipSource.Entries){
						if(-not $seen.Add($entry.FullName)){ continue }
						$entryTarget=$zipTarget.CreateEntry($entry.FullName,[System.IO.Compression.CompressionLevel]::Optimal)
						$entryTarget.LastWriteTime=$entry.LastWriteTime
						if($entry.FullName.EndsWith('/')){ continue }
						$streamIn=$entry.Open()
						$streamOut=$entryTarget.Open()
						try{ $streamIn.CopyTo($streamOut) }
						finally{
							$streamOut.Dispose()
							$streamIn.Dispose()
						}
					}
				}
				finally{ $zipSource.Dispose() }
			}
			$lines=New-Object 'System.Collections.Generic.List[string]'
			$lines.Add("Open PMS - Dental $version`: the source of this release")
			$lines.Add('')
			$lines.Add("Repository: commit $commit")
			$lines.Add('')
			$lines.Add('Submodules, each at the commit the release commit pins, built from this source by third-party/build:')
			foreach($submodule in $submodules){ $lines.Add("$($submodule.Path): commit $($submodule.Commit), $($submodule.Url)") }
			$lines.Add('')
			$lines.Add('Build and upgrade steps: README.md and docs/third-party-source-builds.md.')
			$entryCommits=$zipTarget.CreateEntry("$name/SOURCE-COMMITS.txt",[System.IO.Compression.CompressionLevel]::Optimal)
			$writer=New-Object System.IO.StreamWriter($entryCommits.Open(),(New-Object System.Text.UTF8Encoding($false)))
			try{ $writer.Write(($lines -join "`r`n")+"`r`n") }
			finally{ $writer.Dispose() }
		}
		finally{ $zipTarget.Dispose() }
	}
	finally{
		Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
	}
	return $archivePath
}

#Checks git and the submodules for the source archive, and exits when it cannot be written: 4 when git or a submodule is missing, 1 when a
#submodule is not at its pinned commit.
function Assert-SourceArchivePossible {
	$script:GitPath=Find-Git
	if(-not $script:GitPath){
		Write-Host 'git.exe not found on the PATH. The source archive needs it.'
		exit 4
	}
	$problems=Test-SubmoduleCheckouts
	if($problems.Count -gt 0){
		Write-Problems $problems 'The submodule check'
		if(@($problems | Where-Object { $_.StartsWith('Missing:') }).Count -gt 0){ exit 4 }
		exit 1
	}
}

function Find-Iscc {
	foreach($candidate in @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
		(Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')))
	{
		if(Test-Path -LiteralPath $candidate -PathType Leaf){ return $candidate }
	}
	return $null
}
#endregion Helpers

if($ListFiles){
	foreach($key in $OwnFiles.Keys){ Write-Output "own|$key" }
	foreach($key in $ThirdPartyFiles.Keys){ Write-Output "third|$key" }
	foreach($key in $DocumentFiles.Keys){ Write-Output "doc|$key" }
	exit 0
}

if($WriteSetupTexts){
	foreach($path in Write-SetupTexts $WriteSetupTexts (Read-ReleaseVersion)){ Write-Host "Wrote $path" }
	exit 0
}

if($WriteSourceArchive){
	Assert-SourceArchivePossible
	Write-Host "Wrote $(Write-SourceArchive $WriteSourceArchive (Read-ReleaseVersion))"
	exit 0
}

if($CheckFolder){
	if(-not (Test-Path -LiteralPath $CheckFolder -PathType Container)){
		Write-Host "No such folder: $CheckFolder"
		exit 4
	}
	$problems=Test-StagingFolder $CheckFolder
	Write-Host "Build id: $(Get-BuildId (Resolve-Path -LiteralPath $CheckFolder).Path)"
	if($problems.Count -gt 0){
		Write-Problems $problems 'The check'
		exit 1
	}
	Write-Host 'The check passed.'
	exit 0
}

#region Versions
$releaseVersion=Read-ReleaseVersion
$installerVersion=Read-InstallerVersion
if($installerVersion -ne $releaseVersion){
	Write-Host "installer\OpenPmsDental.iss says version $installerVersion, SharedAssemblyInfo.cs says $releaseVersion. Make them the same."
	exit 1
}
Write-Host "Release version $releaseVersion (compatibility version $CompatVersion)"
#endregion Versions

#region Submodules
#Before the build: PDFsharp/MigraDoc and WeCantSpell.Hunspell build from the submodules, and the source archive holds their pinned commits.
Assert-SourceArchivePossible
#endregion Submodules

#region Build
if(-not $SkipBuild){
	if($BuildLane){
		if(-not (Test-Path -LiteralPath $BuildLane -PathType Leaf)){
			Write-Host "No build lane script at $BuildLane"
			exit 4
		}
		& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $BuildLane -Root $RepoRoot -Configuration Release -Platform x86 -WaitMinutes $LaneWaitMinutes
		$exitBuild=$LASTEXITCODE
	}
	else{
		if(-not $MSBuildPath){ $MSBuildPath=Find-MSBuild }
		if(-not $MSBuildPath -or -not (Test-Path -LiteralPath $MSBuildPath -PathType Leaf)){
			Write-Host 'MSBuild.exe not found. Pass -MSBuildPath or -BuildLane.'
			exit 4
		}
		& $MSBuildPath (Join-Path $RepoRoot 'OpenDental.Local.slnf') /p:Configuration=Release /p:Platform=x86 /m /nodeReuse:false /v:minimal /nologo
		$exitBuild=$LASTEXITCODE
	}
	if($exitBuild -ne 0){
		Write-Host "The Release build failed (exit $exitBuild)."
		exit 2
	}
}
#endregion Build

#region Release output
$releaseOutput=Join-Path $RepoRoot 'OpenPmsDental\bin\Release'
if(-not (Test-Path -LiteralPath $releaseOutput -PathType Container)){
	Write-Host "No Release output at $releaseOutput. Build Release x86 first."
	exit 4
}
$problems=New-Object 'System.Collections.Generic.List[string]'
$allowedSet=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach($key in $OwnFiles.Keys){ [void]$allowedSet.Add($key) }
foreach($key in $ThirdPartyFiles.Keys){ [void]$allowedSet.Add($key) }
foreach($item in Get-ChildItem -LiteralPath $releaseOutput -Force){
	if($item.PSIsContainer){
		$reason=Get-DenyReason $item.Name
		if($reason){ $problems.Add("Denied folder in the Release output: $($item.Name) ($reason)") }
		else{ $problems.Add("Unexpected folder in the Release output: $($item.Name)") }
		continue
	}
	$notShipped=$false
	foreach($pattern in $NotShippedPatterns){ if($item.Name -like $pattern){ $notShipped=$true } }
	if($notShipped){ continue }
	$reason=Get-DenyReason $item.Name
	if($reason){ $problems.Add("Denied file in the Release output: $($item.Name) ($reason)") }
	elseif(-not $allowedSet.Contains($item.Name)){
		$problems.Add("Unexpected file in the Release output: $($item.Name) (add it to the allow-list of tools\Build-Release.ps1, with its notice if it is third-party, or remove it from the build)")
	}
}
foreach($name in $allowedSet){
	if(-not (Test-Path -LiteralPath (Join-Path $releaseOutput $name) -PathType Leaf)){
		$why=if($RequiredReasons.Contains($name)){ " ($($RequiredReasons[$name]))" }else{ '' }
		$problems.Add("Missing from the Release output: $name$why")
	}
}
if($problems.Count -gt 0){
	Write-Problems $problems 'The Release output check'
	exit 1
}
#endregion Release output

#region Stage
if(-not $OutDir){ $OutDir=Join-Path $RepoRoot 'installer\bin' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir=(Resolve-Path -LiteralPath $OutDir).Path.TrimEnd('\')
$staging=Join-Path $OutDir 'staging'
if(Test-Path -LiteralPath $staging){
	Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging | Out-Null
foreach($name in $OwnFiles.Keys){ Copy-Item -LiteralPath (Join-Path $releaseOutput $name) -Destination (Join-Path $staging $name) }
foreach($name in $ThirdPartyFiles.Keys){ Copy-Item -LiteralPath (Join-Path $releaseOutput $name) -Destination (Join-Path $staging $name) }
foreach($path in $DocumentFiles.Keys){
	$target=Join-Path $staging $path
	New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
	Copy-Item -LiteralPath (Join-Path $RepoRoot $DocumentFiles[$path]) -Destination $target
}
$problems=Test-StagingFolder $staging -Full
if($problems.Count -gt 0){
	Write-Problems $problems 'The staging check'
	exit 1
}
$buildId=Get-BuildId $staging
$stagedFiles=@(Get-ChildItem -LiteralPath $staging -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($staging.Length+1) } |
	Sort-Object -Property @{ Expression={ $_.ToLowerInvariant() } })
Write-Host "Staged $($stagedFiles.Count) files in $staging"
Write-Host "Build id: $buildId"
$sumLines=New-Object 'System.Collections.Generic.List[string]'
foreach($relative in $stagedFiles){
	$sumLines.Add((Get-Sha256 (Join-Path $staging $relative))+'  staging/'+$relative.Replace('\','/'))
}
$sumsPath=Join-Path $OutDir 'SHA256SUMS.txt'
Write-Sums $sumsPath $sumLines.ToArray()
#endregion Stage

#region Installer
$setupName="OpenPmsDental-$releaseVersion-Setup.exe"
$setupPath=Join-Path $OutDir $setupName
if(-not $SkipInstaller){
	if(-not $Iscc){ $Iscc=Find-Iscc }
	if(-not $Iscc -or -not (Test-Path -LiteralPath $Iscc -PathType Leaf)){
		Write-Host 'Inno Setup 6 (ISCC.exe) not found. Pass -Iscc, or -SkipInstaller to stage only.'
		exit 4
	}
	#The information page (the attribution and the source address, then NOTICE) and the License Agreement page (how the installed program is
	#licensed, then the GNU GPL 3.0 it is conveyed under). Both stay out of the staging folder, which is exactly what is installed.
	$setupTexts=Write-SetupTexts $OutDir $releaseVersion
	$infoPath=$setupTexts[0]
	$licensePath=$setupTexts[1]
	if(Test-Path -LiteralPath $setupPath){ Remove-Item -LiteralPath $setupPath -Force }
	& $Iscc "/DStagingDir=$staging" "/DInfoFile=$infoPath" "/DLicenseTextFile=$licensePath" "/O$OutDir" '/Q' (Join-Path $RepoRoot 'installer\OpenPmsDental.iss')
	if($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $setupPath -PathType Leaf)){
		Write-Host "The installer did not compile (ISCC exit $LASTEXITCODE)."
		exit 3
	}
	$setupSha=Get-Sha256 $setupPath
	$sumLines.Insert(0,$setupSha+'  '+$setupName)
	Write-Sums $sumsPath $sumLines.ToArray()
	Write-Host "Setup: $setupPath"
	Write-Host "Setup SHA-256: $setupSha"
}
#endregion Installer

#region Source archive
#The Corresponding Source of the release: the repository and the submodules at the release commit (docs\release-process.md attaches it to
#the GitHub release). Its SHA-256 follows the setup's in SHA256SUMS.txt.
$sourceArchivePath=Write-SourceArchive $OutDir $releaseVersion
$sourceArchiveName=Split-Path $sourceArchivePath -Leaf
$sourceArchiveSha=Get-Sha256 $sourceArchivePath
$sumLines.Insert($(if($SkipInstaller){ 0 }else{ 1 }),$sourceArchiveSha+'  '+$sourceArchiveName)
Write-Sums $sumsPath $sumLines.ToArray()
Write-Host "Source archive: $sourceArchivePath"
Write-Host "Source archive SHA-256: $sourceArchiveSha"
Write-Host "Checksums: $sumsPath"
#endregion Source archive

#region Copy
if($CopyTo){
	$notes=Join-Path $RepoRoot "docs\release-notes\v$releaseVersion.md"
	foreach($path in @($sumsPath,$notes,$sourceArchivePath)){
		if(-not (Test-Path -LiteralPath $path -PathType Leaf)){
			Write-Host "Missing: $path"
			exit 4
		}
	}
	if($SkipInstaller){
		Write-Host '-CopyTo needs the installer; it was skipped.'
		exit 4
	}
	New-Item -ItemType Directory -Force -Path $CopyTo | Out-Null
	Copy-Item -LiteralPath $setupPath -Destination (Join-Path $CopyTo $setupName) -Force
	Copy-Item -LiteralPath $sourceArchivePath -Destination (Join-Path $CopyTo $sourceArchiveName) -Force
	Copy-Item -LiteralPath $sumsPath -Destination (Join-Path $CopyTo 'SHA256SUMS.txt') -Force
	Copy-Item -LiteralPath $notes -Destination (Join-Path $CopyTo "release-notes-v$releaseVersion.md") -Force
	Write-Host "Copied the setup, the source archive, SHA256SUMS.txt and the release notes to $CopyTo"
}
#endregion Copy
exit 0
