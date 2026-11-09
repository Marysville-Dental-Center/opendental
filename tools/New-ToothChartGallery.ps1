<#
.SYNOPSIS
Makes an owner gallery of the WPF tooth chart (docs/specs/wpf-tooth-chart-plan.md, section 8) and zips it.

.DESCRIPTION
Runs the WriteGallery test of tests\ToothChartImageTests with OPMS_CHART_GALLERY set to the gallery folder. The test
draws every chart scene with the WPF chart, marks each picture equal or not to this branch's baseline (the file in
Baselines\wpf-net48 that the branch merges, by its pixel SHA-256), compares it with the approved WPF picture, measures
the render times for owner question O-19 in a fresh second process, copies TC-0's DirectX, OpenGL and Simple 2D
pictures of every scene next to them, and writes index.html (one row per scene and size, filterable by group) and
timings.txt. This script then zips the folder into <folder>.zip, with forward-slash entry names, so the zip opens on
macOS and phones as well as on Windows.

The approved WPF pictures: gallery G0 has none (TC-1's are the first baselines). From G1 on they are those of
-ApprovedRef (default main), never this branch's own Baselines\wpf-net48, so a scene the branch re-recorded shows its
difference: the script extracts tests/ToothChartImageTests/Baselines/wpf-net48 of that commit with git archive into a
temporary folder (deleted afterwards) and passes it as OPMS_CHART_GALLERY_APPROVED. -ApprovedFolder names a folder of
approved baselines instead.

Build the test assembly first (through the build lane: odbuild.ps1 -Projects ToothChartImageTests). The gallery holds
synthetic scenes only: no patient data and no database. vstest.console.exe is that of Visual Studio Build Tools 2026
(tools\Initialize-BuildEnvironment.ps1, Get-BuildToolPath -Tool VsTest), or -VsTestPath.

.PARAMETER Root
The repository or worktree root. Default: the folder above this script.

.PARAMETER Out
The gallery folder: a new or empty folder, or an earlier gallery (which is emptied). The test refuses any other
folder. Default: %TEMP%\ToothChartGallery-<Name>.

.PARAMETER Name
The gallery's name: G0 (TC-1), G1 (TC-3), G2 (TC-5, TC-6), G3 (TC-7), G4 (TC-8), G5 (3b). Default G0.

.PARAMETER ApprovedRef
From G1 on: the commit whose Baselines\wpf-net48 holds the approved WPF pictures. Default main.

.PARAMETER ApprovedFolder
From G1 on: a folder of approved WPF baselines (<size>\<scene>.png) to use instead of -ApprovedRef.

.PARAMETER NoTestRun
Only zip a gallery folder that a test run has already written (for example a run of the whole assembly with
OPMS_CHART_GALLERY set).

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\New-ToothChartGallery.ps1 -Name G0
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\New-ToothChartGallery.ps1 -Name G1 -ApprovedRef main
#>
param(
	[string]$Root = '',
	[string]$Out = '',
	[string]$Name = 'G0',
	[string]$ApprovedRef = 'main',
	[string]$ApprovedFolder = '',
	[string]$VsTestPath = '',
	[switch]$NoTestRun
)
$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$Root = (Resolve-Path $Root).Path
if (-not $Out) { $Out = Join-Path $env:TEMP ('ToothChartGallery-' + $Name) }
$Out = [System.IO.Path]::GetFullPath($Out)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Zips every file under $Folder into $Zip, each entry named by its path relative to $Folder with forward slashes (the zip
# format's separator). Compress-Archive of Windows PowerShell 5.1 writes backslashes, which macOS and phone unzippers
# take as part of a flat file name, so index.html's relative links would break there.
function New-GalleryZip {
	param([string]$Folder, [string]$Zip)
	$folderFull = [System.IO.Path]::GetFullPath($Folder).TrimEnd('\') + '\'
	$stream = [System.IO.File]::Open($Zip, [System.IO.FileMode]::CreateNew)
	try {
		$archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
		try {
			foreach ($file in (Get-ChildItem -LiteralPath $Folder -Recurse -File | Sort-Object FullName)) {
				$entryName = $file.FullName.Substring($folderFull.Length).Replace('\', '/')
				[void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal)
			}
		}
		finally { $archive.Dispose() }
	}
	finally { $stream.Dispose() }
}

# The approved baselines of $Ref: tests/ToothChartImageTests/Baselines/wpf-net48 from git archive, extracted under $Folder.
# Returns the extracted wpf-net48 folder.
function Get-ApprovedBaselines {
	param([string]$Ref, [string]$Folder)
	$pathInRepo = 'tests/ToothChartImageTests/Baselines/wpf-net48'
	$zipApproved = Join-Path $Folder 'approved.zip'
	& git -C $Root archive --format=zip -o $zipApproved $Ref $pathInRepo
	if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $zipApproved -PathType Leaf)) {
		throw "git archive of $pathInRepo at $Ref failed (exit $LASTEXITCODE). Pass -ApprovedFolder, or an -ApprovedRef that has the WPF baselines."
	}
	$folderExtract = Join-Path $Folder 'extracted'
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipApproved, $folderExtract)
	return (Join-Path $folderExtract ($pathInRepo.Replace('/', '\')))
}

if (-not $NoTestRun) {
	$dll = Join-Path $Root 'tests\ToothChartImageTests\bin\Debug\ToothChartImageTests.dll'
	if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
		Write-Output "Not built: $dll. Build ToothChartImageTests through the build lane first."
		exit 1
	}
	$vstest = $VsTestPath
	if (-not $vstest) {
		. (Join-Path $Root 'tools\Initialize-BuildEnvironment.ps1')
		$vstest = Get-BuildToolPath -Tool VsTest
	}
	if (-not $vstest -or -not (Test-Path -LiteralPath $vstest -PathType Leaf)) {
		Write-Output 'vstest.console.exe of Build Tools 2026 not found. Pass -VsTestPath.'
		exit 1
	}
	$commit = ''
	try { $commit = (& git -C $Root rev-parse --short HEAD 2>$null) } catch { $commit = '' }
	$folderTemp = ''
	try {
		if ($Name -ne 'G0') {
			if ($ApprovedFolder) {
				$env:OPMS_CHART_GALLERY_APPROVED = [System.IO.Path]::GetFullPath($ApprovedFolder)
				$env:OPMS_CHART_GALLERY_APPROVED_FROM = 'the folder ' + $env:OPMS_CHART_GALLERY_APPROVED
			}
			else {
				$folderTemp = Join-Path $env:TEMP ('ToothChartGalleryApproved-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
				New-Item -ItemType Directory -Force $folderTemp | Out-Null
				$approvedCommit = ''
				try { $approvedCommit = (& git -C $Root rev-parse --short $ApprovedRef 2>$null) } catch { $approvedCommit = '' }
				$env:OPMS_CHART_GALLERY_APPROVED = Get-ApprovedBaselines -Ref $ApprovedRef -Folder $folderTemp
				$env:OPMS_CHART_GALLERY_APPROVED_FROM = "Baselines/wpf-net48 of $ApprovedRef" + $(if ($approvedCommit) { " at $approvedCommit" } else { '' })
			}
			Write-Output "Approved WPF pictures: $($env:OPMS_CHART_GALLERY_APPROVED_FROM)"
		}
		$env:OPMS_CHART_GALLERY = $Out
		$env:OPMS_CHART_GALLERY_NAME = $Name
		$env:OPMS_CHART_GALLERY_COMMIT = $commit
		Write-Output "Writing gallery $Name to $Out ..."
		& $vstest $dll '/Platform:x86' '/Tests:WriteGallery' '/logger:console;verbosity=normal'
		$exitCode = $LASTEXITCODE
	}
	finally {
		Remove-Item Env:OPMS_CHART_GALLERY, Env:OPMS_CHART_GALLERY_NAME, Env:OPMS_CHART_GALLERY_COMMIT, Env:OPMS_CHART_GALLERY_APPROVED, Env:OPMS_CHART_GALLERY_APPROVED_FROM -ErrorAction SilentlyContinue
		if ($folderTemp -and (Test-Path -LiteralPath $folderTemp)) { Remove-Item -LiteralPath $folderTemp -Recurse -Force }
	}
	if ($exitCode -ne 0) {
		Write-Output "The gallery test failed (exit $exitCode)."
		exit $exitCode
	}
}

$index = Join-Path $Out 'index.html'
if (-not (Test-Path -LiteralPath $index -PathType Leaf)) {
	Write-Output "No gallery in $Out (index.html is missing)."
	exit 1
}
$zip = $Out.TrimEnd('\') + '.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
New-GalleryZip -Folder $Out -Zip $zip
$countPictures = @(Get-ChildItem -LiteralPath (Join-Path $Out 'wpf') -Recurse -Filter '*.png').Count
$sizeMb = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
Write-Output "Gallery $Name`: $index ($countPictures WPF pictures); zipped to $zip ($sizeMb MB)."
exit 0
