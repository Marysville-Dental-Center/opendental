<#
.SYNOPSIS
Builds an Open PMS - Dental release: a Release x86 build, one checked staging folder, SHA256SUMS.txt and the Inno Setup installer.

.DESCRIPTION
1. Builds OpenDental.Local.slnf as Release x86, through the shared build lane when -BuildLane is given.
2. Checks OpenPmsDental\bin\Release: every file must be on the allow-list below or be one the release never ships (debug symbols,
   XML documentation). A file on the deny-list, an unknown file or a sub folder fails the script.
3. Stages the allowed files into one folder (<OutDir>\staging), with LICENSE, LICENSE-LGPL-2.1.txt, NOTICE, THIRD-PARTY-NOTICES.txt,
   db\init\new-database.sql and db\README.md, and checks the staging folder again: allow-list, deny-list, every allowed file present,
   every third-party file named in THIRD-PARTY-NOTICES.txt, every file built from this repository equal to its project's own output, the
   native files x86 and free of the debug C runtime, and OpenPmsDental.exe's versions (file version 24.3.63.0, the database compatibility version; product version the
   release version of SharedAssemblyInfo.cs).
4. Prints the build id (ForkReleases.ComputeBuildId over ForkReleases.ListBuildIdFiles) and writes <OutDir>\SHA256SUMS.txt.
5. Compiles installer\OpenPmsDental.iss with Inno Setup 6 into <OutDir>, and adds the setup's SHA-256 to SHA256SUMS.txt.
6. With -CopyTo, copies the setup, SHA256SUMS.txt and the release notes (docs\release-notes\v<version>.md) there.

The staging folder is exactly what the installer puts in the program folder. The uninstaller goes to the program folder's "uninstall"
sub folder, never the top level: a release published from the update server holds every top-level file of the program folder.

Nothing is downloaded and nothing connects to a database.

Exit codes: 0 done; 1 a check failed; 2 the build failed; 3 the installer did not compile; 4 a tool or file is missing.

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
	[switch]$ListFiles
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
	'OpenPmsDentalCloud.dll'='OpenPmsDentalCloud\bin\Release'
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
}

#Third-party files. Each must be named in THIRD-PARTY-NOTICES.txt. Value: where its bytes come from, a byte copy of the repository's
#"Required dlls" file or the project output it is built from.
$ThirdPartyFiles=[ordered]@{}
foreach($name in @('BouncyCastle.Cryptography.dll','Dicom.Core.dll','Dicom.Native.dll','Google.Apis.dll','Google.Apis.Auth.dll',
	'Google.Apis.Core.dll','Google.Apis.Gmail.v1.dll','Health.Direct.Agent.dll','Health.Direct.Common.dll','Health.Direct.ResolverPlugins.dll',
	'Hunspellx86.dll','MailKit.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.Bcl.Cryptography.dll',
	'Microsoft.Extensions.DependencyInjection.Abstractions.dll','Microsoft.Extensions.Logging.Abstractions.dll','Microsoft.Identity.Client.dll',
	'Microsoft.IdentityModel.Abstractions.dll','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll',
	'Microsoft.Web.WebView2.Wpf.dll','MigraDoc.DocumentObjectModel.dll','MigraDoc.Rendering.dll','MimeKit.dll','MySqlConnector.dll','NDde.dll',
	'NHunspell.dll','Newtonsoft.Json.dll','PdfSharp.dll','PdfSharp.Charting.dll','Renci.SshNet.dll','SharpDX.dll','SharpDX.Direct3D9.dll',
	'SharpDX.Mathematics.dll','System.Buffers.dll','System.Diagnostics.DiagnosticSource.dll','System.Formats.Asn1.dll','System.IO.Pipelines.dll',
	'System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Text.Encodings.Web.dll',
	'System.Text.Json.dll','System.Threading.Tasks.Extensions.dll','Tao.OpenGl.dll','Tao.Platform.Windows.dll','WebView2Loader.dll'))
{
	$ThirdPartyFiles[$name]='Required dlls'
}
$ThirdPartyFiles['RdlEngine.dll']='RdlEngine\bin\Release'
$ThirdPartyFiles['RdlViewer.dll']='RdlViewer\bin\Release'

#Files from the repository: the path in the program folder, and the repository-relative source. LICENSE-LGPL-2.1.txt is the licence
#text the LGPL 2.1 asks for with every copy of the libraries the release ships under it (RdlEngine.dll, RdlViewer.dll, NHunspell.dll,
#Hunspellx86.dll and the en_US word list in WpfControlsOD.dll; THIRD-PARTY-NOTICES.txt).
$DocumentFiles=[ordered]@{
	'LICENSE'='LICENSE'
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
	'Hunspellx86.dll'='NHunspell loads it for the spell check of every ODtextBox; a Release build throws without it'
	'ODR.dll'='RDL report definitions load it by file name (<CodeModule>ODR.dll</CodeModule>)'
	'LICENSE-LGPL-2.1.txt'='the LGPL 2.1 (sections 1 and 4) asks for its text with every copy of RdlEngine.dll, RdlViewer.dll, NHunspell.dll and Hunspellx86.dll'
}

#Native files: they must be x86 and must not need the debug C runtime, which may not be redistributed.
$NativeFiles=@('Direct2dWrapper.dll','Hunspellx86.dll','WebView2Loader.dll','Dicom.Native.dll')
$DebugRuntimeNames=@('VCRUNTIME140D.dll','MSVCP140D.dll','ucrtbased.dll','VCRUNTIME140_1D.dll')

#Files of OpenPmsDental's Release output that are never shipped.
$NotShippedPatterns=@('*.pdb','OpenPmsDental.xml','OpenPmsDentalBusiness.xml','CodeBase.xml')

#ForkReleases.ListBuildIdFiles: the build id is the SHA-256 over these files' lower-case names and SHA-256 values.
$BuildIdFiles=@('OpenPmsDental.exe','OpenPmsDentalBusiness.dll','CodeBase.dll','DataConnectionBase.dll','WpfControlsOD.dll',
	'OpenPmsDentalGraph.dll','OpenPmsDentalCloud.dll','SparksToothChart.dll','ODCrypt.dll','PasswordVaultWrapper.dll','ImagingDeviceManager.dll',
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
	@('SigPlusNET.dll','Topaz: no published redistribution terms; loaded from the Topaz software when installed (BIN-B)'),
	@('zxing*','a dead reference (removed in BIN-B)'),
	@('Avalara*','the AvaTax SDK, a dead reference (removed in BIN-B)'),
	@('Ionic.*','DotNetZip: MS-PL, abandoned, CVE-2024-48510 (replaced in BIN-B)'),
	@('DotNetZip*','DotNetZip: MS-PL, abandoned, CVE-2024-48510 (replaced in BIN-B)'),
	@('Tamir.SharpSSH.dll','SharpSSH, unmaintained since 2013 (replaced by SSH.NET in the tier 1 library updates)'),
	@('Org.Mentalis.Security.dll',"SharpSSH's cryptography library (removed with SharpSSH)"),
	@('DiffieHellman.dll',"SharpSSH's key exchange library (removed with SharpSSH)"),
	@('OpenPop.dll',"OpenPop.NET, unmaintained (replaced by MailKit's POP3 client in the tier 1 library updates)"),
	@('BouncyCastle.Crypto.dll',"MimeKit 2's Bouncy Castle 1.8; MimeKit 4 uses BouncyCastle.Cryptography.dll"),
	@('Google.Apis*.PlatformServices.dll','the Google APIs 1.44 platform shims; the library has none since 1.50'),
	@('TWAINDSM.dll',"the TWAIN data source manager comes with the scanner's driver; one in the program folder would be loaded first"),
	@('FreeDentalConfig.xml','the connection settings of each computer; the installer writes it, a release never carries it'),
	@('OpenDentalServerConfig.xml','the Middle Tier settings of each server'),
	@('ForkRelease.manifest','written by the release channel on each workstation'),
	@('*.pdb','debug symbols'),
	@('unins*.exe','the uninstaller lives in the uninstall sub folder; a release publishes every top-level file'),
	@('unins*.dat','the uninstaller lives in the uninstall sub folder; a release publishes every top-level file'),
	@('unins*.msg','the uninstaller lives in the uninstall sub folder; a release publishes every top-level file'),
	@('OpenDent*.exe','a file of the pre-rename names; the program is OpenPmsDental.exe'),
	@('OpenDent*.dll','a file of the pre-rename names; the program uses OpenPmsDentalBusiness.dll')
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
	#The information page: the attribution and the source address, then NOTICE.
	$template=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'installer\setup-info.txt'))
	$notice=[System.IO.File]::ReadAllText((Join-Path $RepoRoot 'NOTICE'))
	$info=$template.Replace('@VERSION@',$releaseVersion).Replace('@NOTICE@',$notice.TrimEnd())
	$info=($info -replace "`r`n","`n") -replace "`n","`r`n"
	$infoPath=Join-Path $OutDir 'setup-info.txt'
	[System.IO.File]::WriteAllText($infoPath,$info,(New-Object System.Text.UTF8Encoding($true)))
	if(Test-Path -LiteralPath $setupPath){ Remove-Item -LiteralPath $setupPath -Force }
	& $Iscc "/DStagingDir=$staging" "/DInfoFile=$infoPath" "/O$OutDir" '/Q' (Join-Path $RepoRoot 'installer\OpenPmsDental.iss')
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
Write-Host "Checksums: $sumsPath"
#endregion Installer

#region Copy
if($CopyTo){
	$notes=Join-Path $RepoRoot "docs\release-notes\v$releaseVersion.md"
	foreach($path in @($sumsPath,$notes)){
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
	Copy-Item -LiteralPath $sumsPath -Destination (Join-Path $CopyTo 'SHA256SUMS.txt') -Force
	Copy-Item -LiteralPath $notes -Destination (Join-Path $CopyTo "release-notes-v$releaseVersion.md") -Force
	Write-Host "Copied the setup, SHA256SUMS.txt and the release notes to $CopyTo"
}
#endregion Copy
exit 0
