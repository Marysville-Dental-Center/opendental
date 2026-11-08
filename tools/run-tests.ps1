<#
.SYNOPSIS
Runs the fork's MSTest assemblies (tests/*, NoHqTests, the legacy UnitTests and tools/*/*Tests) with vstest, a few at a time, and prints one summary.

.DESCRIPTION
The test assemblies are the *Tests / *.Tests projects listed in OpenDental.Local.slnf, the legacy UnitTests project among them
(it builds 'Unit Tests.exe'; -Include and -List name it UnitTests, and it makes and drops its own database). Build them first
(Debug|x86, see README.md).

Targeted mode runs NoHqTests plus the assemblies named in -Include. Core mode runs every assembly except the slow migration and
adoption ones (add them back with -Include). Full mode runs everything.
Results go to -Out (default: %TEMP%\odtests\<repository folder name>.txt). The last line is "DONE ok=True|False".
If the call is cut off by a tool timeout, the run keeps going; call again with -WaitOnly and the same -Root/-Out
to wait (up to 9 minutes per call) and print the summary.

-Root defaults to the repository that holds this script (the parent of tools\), so it can be run from any folder.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Targeted -Include BPAYTests,PayPlanTests
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Core
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Mode Full
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -WaitOnly
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -List
#>
param(
	[string]$Root = '',
	# Targeted: NoHqTests + -Include. Core: every assembly except the slow migration/adoption ones (add them with -Include).
	# Full: everything. Core and Full take a machine-wide lock, so only one such run uses the shared MariaDB at a time.
	[ValidateSet('Full', 'Core', 'Targeted')][string]$Mode = 'Targeted',
	[string[]]$Include = @(),
	[string]$Out = '',
	[int]$MaxParallel = 3,
	[int]$TimeoutMinutesPerAssembly = 20,
	[switch]$WaitOnly,
	[switch]$List
)
$ErrorActionPreference = 'Stop'
# Below-normal priority: vstest and its testhost processes inherit it, so test runs never starve the desktop.
try { (Get-Process -Id $PID).PriorityClass = 'BelowNormal' } catch {}
$heavy = @('AdoptionValidation.Tests', 'SyntheticData.Tests', 'ForkMigrationTests', 'M201Tests')
# The repository is the parent of the tools folder that holds this script, unless -Root says otherwise.
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$Root = (Resolve-Path $Root).Path
if (-not (Test-Path (Join-Path $Root 'OpenDental.Local.slnf'))) { throw "OpenDental.Local.slnf not found in $Root. Pass the repository root with -Root." }

function Find-VsTest {
	$buildTools = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
	if (Test-Path $buildTools) { return $buildTools }
	# Any other Visual Studio 2022 edition: ask vswhere.
	$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
	if (Test-Path $vswhere) {
		$found = & $vswhere -latest -products * -find 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe' | Select-Object -First 1
		if ($found -and (Test-Path $found)) { return $found }
	}
	throw 'vstest.console.exe was not found. Install the Build Tools with the testing tools as described in README.md.'
}

if (-not $Out) {
	$dir = Join-Path $env:TEMP 'odtests'
	New-Item -ItemType Directory -Force $dir | Out-Null
	$Out = Join-Path $dir ((Split-Path $Root -Leaf) + '.txt')
}
$logDir = $Out + '.logs'

function Show-Summary {
	if (-not (Test-Path $Out)) { Write-Output "No results yet at $Out"; return }
	Get-Content $Out
	$failed = Get-Content $Out | Where-Object { $_ -match '^(\S+) exit=(?!0\b)' } | ForEach-Object { ($_ -split ' ')[0] }
	foreach ($name in $failed) {
		$log = Join-Path $logDir "$name.log"
		if (Test-Path $log) {
			Write-Output "---- ${name}: failing tests and errors (first 40 lines)"
			Get-Content $log | Where-Object { $_ -match '^\s*Failed\s|^\s*Error Message:|^\s{2,}(Assert\.|System\.\w+Exception|Test method .* threw)|timed out' } | Select-Object -First 40
		}
	}
}

if ($WaitOnly) {
	$deadline = (Get-Date).AddMinutes(9)
	while ((Get-Date) -lt $deadline) {
		if ((Test-Path $Out) -and (Select-String -Path $Out -Pattern '^DONE ' -Quiet)) { break }
		Start-Sleep -Seconds 10
	}
	if (-not ((Test-Path $Out) -and (Select-String -Path $Out -Pattern '^DONE ' -Quiet))) { Write-Output 'STILL RUNNING: call again with -WaitOnly.' }
	Show-Summary
	exit 0
}

# Discover test projects: every *Tests / *.Tests project listed in OpenDental.Local.slnf, the legacy UnitTests project included (package S0-5).
$slnf = Get-Content (Join-Path $Root 'OpenDental.Local.slnf') -Raw | ConvertFrom-Json
$projects = $slnf.solution.projects | Where-Object { $_ -match 'Tests\.csproj$' } | ForEach-Object { Get-Item (Join-Path $Root $_) -ErrorAction SilentlyContinue } | Where-Object { $_ }
$assemblies = @()
foreach ($p in $projects) {
	[xml]$x = Get-Content $p.FullName
	$assemblyName = ($x.Project.PropertyGroup | ForEach-Object { $_.AssemblyName } | Where-Object { $_ } | Select-Object -First 1)
	if (-not $assemblyName) { $assemblyName = $p.BaseName }
	# An assembly name with a space ('Unit Tests') is listed and included by its project name (UnitTests).
	$name = if ($assemblyName -match '\s') { $p.BaseName } else { $assemblyName }
	# A test container is a DLL, or an EXE (the legacy UnitTests project is a WinExe); vstest runs either.
	$dll = foreach ($folder in @('bin\Debug', 'bin\x86\Debug')) { foreach ($extension in @('.dll', '.exe')) { Join-Path $p.DirectoryName "$folder\$assemblyName$extension" } }
	$dll = $dll | Where-Object { Test-Path $_ } | Select-Object -First 1
	$assemblies += [pscustomobject]@{ Name = $name; Dll = $dll }
}
$assemblies = $assemblies | Sort-Object Name -Unique
if ($List) { $assemblies | ForEach-Object { '{0} {1}' -f $_.Name, ($(if ($_.Dll) { 'built' } else { 'NOT BUILT' })) }; exit 0 }

$vstest = Find-VsTest
$extra = @($Include | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() -replace '\.dll$', '' } | Where-Object { $_ })
if ($Mode -eq 'Targeted') {
	$want = @('NoHqTests') + $extra
	$unknown = $want | Where-Object { $n = $_; -not ($assemblies | Where-Object { $_.Name -eq $n }) }
	if ($unknown) { Write-Output "Unknown test assemblies (use -List): $($unknown -join ', ')" }
	$assemblies = $assemblies | Where-Object { $want -contains $_.Name }
} elseif ($Mode -eq 'Core') {
	$assemblies = $assemblies | Where-Object { ($heavy -notcontains $_.Name) -or ($extra -contains $_.Name) }
}

# Core and Full runs hit the one local MariaDB hard: take turns machine-wide (the lock frees itself if its holder dies).
$lock = $null
if ($Mode -ne 'Targeted') {
	$lock = New-Object System.Threading.Mutex($false, 'Local\OpenPMS-Dental-Tests')
	Write-Output "Waiting for the machine-wide test lock (another Core/Full run may be using the database)..."
	try { [void]$lock.WaitOne() } catch [System.Threading.AbandonedMutexException] { }
}

New-Item -ItemType Directory -Force $logDir | Out-Null
Set-Content -Path $Out -Value "RUN mode=$Mode root=$Root started=$(Get-Date -Format s) assemblies=$($assemblies.Count)" -Encoding utf8
$queue = New-Object System.Collections.Queue
$assemblies | ForEach-Object { $queue.Enqueue($_) }
$running = @()
$allOk = $true
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
	while ($running.Count -lt $MaxParallel -and $queue.Count -gt 0) {
		$a = $queue.Dequeue()
		$log = Join-Path $logDir "$($a.Name).log"
		if (-not $a.Dll) {
			Add-Content $Out "$($a.Name) exit=NOTBUILT total=0 passed=0 failed=0 seconds=0" -Encoding utf8
			$allOk = $false
			continue
		}
		$proc = Start-Process -FilePath $vstest -ArgumentList @("`"$($a.Dll)`"", '/Platform:x86') -WorkingDirectory (Split-Path $a.Dll) -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError ($log + '.err')
		$null = $proc.Handle  # cache the handle so ExitCode is available after the process exits
		$running += [pscustomobject]@{ A = $a; P = $proc; Log = $log; Start = Get-Date }
	}
	Start-Sleep -Milliseconds 500
	$still = @()
	foreach ($r in $running) {
		$elapsed = ((Get-Date) - $r.Start).TotalMinutes
		if (-not $r.P.HasExited -and $elapsed -gt $TimeoutMinutesPerAssembly) {
			& taskkill.exe /T /F /PID $r.P.Id 2>&1 | Out-Null
			Add-Content $Out "$($r.A.Name) exit=TIMEOUT total=? passed=? failed=? seconds=$([int]($elapsed*60))" -Encoding utf8
			$allOk = $false
			continue
		}
		if (-not $r.P.HasExited) { $still += $r; continue }
		$r.P.WaitForExit()
		$text = (Get-Content $r.Log -Raw -ErrorAction SilentlyContinue) + ''
		$total = if ($text -match 'Total tests:\s*(\d+)') { $Matches[1] } elseif ($text -match 'Total:\s*(\d+)') { $Matches[1] } else { '?' }
		$passed = if ($text -match '\bPassed:\s*(\d+)') { $Matches[1] } else { '?' }
		$failedN = if ($text -match '\bFailed:\s*(\d+)') { $Matches[1] } else { '0' }
		$code = $r.P.ExitCode
		if ($code -ne 0) { $allOk = $false }
		Add-Content $Out "$($r.A.Name) exit=$code total=$total passed=$passed failed=$failedN seconds=$([int](((Get-Date) - $r.Start).TotalSeconds))" -Encoding utf8
	}
	$running = $still
}
if ($lock) { try { $lock.ReleaseMutex() } catch {} }
Add-Content $Out "DONE ok=$allOk finished=$(Get-Date -Format s)" -Encoding utf8
Show-Summary
if ($allOk) { exit 0 } else { exit 1 }
