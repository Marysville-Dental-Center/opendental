<#
.SYNOPSIS
Makes an owner gallery of the WPF tooth chart (docs/specs/wpf-tooth-chart-plan.md, section 8) and zips it.

.DESCRIPTION
Runs the WriteGallery test of tests\ToothChartImageTests with OPMS_CHART_GALLERY set to the gallery folder. The test
draws every chart scene with the WPF chart, compares each picture with the approved WPF baseline (Baselines\wpf-net48;
gallery G0 has none yet), measures the render times for owner question O-19 in a fresh second process, copies TC-0's
DirectX, OpenGL and Simple 2D pictures of every scene next to them, and writes index.html (one row per scene and size,
filterable by group) and timings.txt. This script then zips the folder into <folder>.zip.

Build the test assembly first (through the build lane: odbuild.ps1 -Projects ToothChartImageTests). The gallery holds
synthetic scenes only: no patient data and no database. vstest.console.exe is that of Visual Studio Build Tools 2026
(found with vswhere and the version range [18.0,19.0)), or -VsTestPath.

.PARAMETER Root
The repository or worktree root. Default: the folder above this script.

.PARAMETER Out
The gallery folder. It is emptied first. Default: %TEMP%\ToothChartGallery-<Name>.

.PARAMETER Name
The gallery's name: G0 (TC-1), G1 (TC-3), G2 (TC-5, TC-6), G3 (TC-7), G4 (TC-8), G5 (3b). Default G0.

.PARAMETER NoTestRun
Only zip a gallery folder that a test run has already written (for example a run of the whole assembly with
OPMS_CHART_GALLERY set).

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\New-ToothChartGallery.ps1 -Name G0
#>
param(
	[string]$Root = '',
	[string]$Out = '',
	[string]$Name = 'G0',
	[string]$VsTestPath = '',
	[switch]$NoTestRun
)
$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$Root = (Resolve-Path $Root).Path
if (-not $Out) { $Out = Join-Path $env:TEMP ('ToothChartGallery-' + $Name) }
$Out = [System.IO.Path]::GetFullPath($Out)

if (-not $NoTestRun) {
	$dll = Join-Path $Root 'tests\ToothChartImageTests\bin\Debug\ToothChartImageTests.dll'
	if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
		Write-Output "Not built: $dll. Build ToothChartImageTests through the build lane first."
		exit 1
	}
	$vstest = $VsTestPath
	if (-not $vstest) {
		$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
		$vstest = & $vswhere -latest -products * -version '[18.0,19.0)' -find 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe' | Select-Object -First 1
	}
	if (-not $vstest -or -not (Test-Path -LiteralPath $vstest -PathType Leaf)) {
		Write-Output 'vstest.console.exe of Build Tools 2026 not found. Pass -VsTestPath.'
		exit 1
	}
	$commit = ''
	try { $commit = (& git -C $Root rev-parse --short HEAD 2>$null) } catch { $commit = '' }
	$env:OPMS_CHART_GALLERY = $Out
	$env:OPMS_CHART_GALLERY_NAME = $Name
	$env:OPMS_CHART_GALLERY_COMMIT = $commit
	Write-Output "Writing gallery $Name to $Out ..."
	& $vstest $dll '/Platform:x86' '/Tests:WriteGallery' '/logger:console;verbosity=normal'
	$exitCode = $LASTEXITCODE
	Remove-Item Env:OPMS_CHART_GALLERY, Env:OPMS_CHART_GALLERY_NAME, Env:OPMS_CHART_GALLERY_COMMIT -ErrorAction SilentlyContinue
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
Compress-Archive -Path (Join-Path $Out '*') -DestinationPath $zip -CompressionLevel Optimal
$countPictures = @(Get-ChildItem -LiteralPath (Join-Path $Out 'wpf') -Recurse -Filter '*.png').Count
$sizeMb = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
Write-Output "Gallery $Name`: $index ($countPictures WPF pictures); zipped to $zip ($sizeMb MB)."
exit 0
