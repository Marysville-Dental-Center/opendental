<#
.SYNOPSIS
Regenerates db\init\new-database.sql, the SQL file that initializes a new database.

.DESCRIPTION
Builds tools\DevDbTool (through the build lane when -BuildLane is given) and runs it with --export-init-sql. The tool:

  1. builds a scratch database on the local MariaDB the same way tools\New-DevDatabase.ps1 does: the seed dump
     UnitTests\Resources\dump.txt, the frozen convert scripts to 24.3.63, adoption and the fork migrations, the default auto codes,
     procedure buttons and quick-add procedures; no sample data and no A to Z folder;
  2. verifies it like New-DevDatabase.ps1 -VerifyOnly;
  3. removes what belongs to the PC or the run that built it: computer and computerpref rows, the security log, cache signals, run
     timestamps, and the seed dump's fictional practice, providers, schedules and zip codes; writes the adoption record of a new database
     (ForkAdoptedFrom "new=1;..."); sets MainWindowTitle and SoftwareName to the product name "Open PMS - Dental" (the seed dump holds
     Open Dental's; a new database is not an adopted copy, so the adoption row check does not apply); makes Admin set a password at the
     first logon;
  4. writes every table (SHOW CREATE TABLE) and its rows in primary key order, in C#, without MariaDB client tools. The same source always
     gives the same bytes;
  5. drops the scratch database.

Run it after every new fork migration step and commit the regenerated file. The test
tests\ForkMigrationTests\InitSqlRegenerationTests.cs fails while the committed file differs from what the generator writes, and
tests\InitSqlTests checks the committed file itself.

The tool refuses any server other than localhost/127.0.0.1 unless you pass -AllowRemoteServer. Outbound HTTP is blocked (exit code 3).

.PARAMETER Server
Database server host for the scratch database. Default: 127.0.0.1.

.PARAMETER Port
Database server port. Default: 3306.

.PARAMETER User
MySQL user. Default: root.

.PARAMETER Password
MySQL password. Default: blank. Passed to the tool through the DEVDB_PASSWORD environment variable, not the command line.

.PARAMETER OutFile
The file to write. Default: db\init\new-database.sql in this repository.

.PARAMETER BuildLane
Path of the shared build lane script (odbuild.ps1) of this PC. When given, DevDbTool is built through it, so that only one build runs at
a time. Without it, MSBuild is found with vswhere, as tools\New-DevDatabase.ps1 does.

.PARAMETER SkipBuild
Run the already-built tools\DevDbTool\bin\Debug\DevDbTool.exe without building.

.PARAMETER MSBuildPath
Full path to MSBuild.exe when not building through -BuildLane. Default: found with vswhere.

.PARAMETER KeepScratchDatabase
Keep the scratch database (named initsql_<run id>) to look at it. Drop it yourself afterwards.

.PARAMETER AllowRemoteServer
Allow a server other than localhost/127.0.0.1.

.EXAMPLE
.\tools\New-InitSql.ps1
Rebuilds DevDbTool with MSBuild and rewrites db\init\new-database.sql.

.EXAMPLE
.\tools\New-InitSql.ps1 -BuildLane C:\path\to\odbuild.ps1
The same, building through the shared build lane.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
	[ValidateNotNullOrEmpty()]
	[string]$Server = '127.0.0.1',
	[ValidateRange(1, 65535)]
	[int]$Port = 3306,
	[ValidateNotNullOrEmpty()]
	[string]$User = 'root',
	[string]$Password = '',
	[string]$OutFile = '',
	[string]$BuildLane = '',
	[switch]$SkipBuild,
	[string]$MSBuildPath = '',
	[switch]$KeepScratchDatabase,
	[switch]$AllowRemoteServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $PSScriptRoot 'DevDbTool\DevDbTool.csproj'
$toolExe = Join-Path $PSScriptRoot 'DevDbTool\bin\Debug\DevDbTool.exe'
if (-not $OutFile) {
	$OutFile = Join-Path $repoRoot 'db\init\new-database.sql'
}

function Find-MSBuild {
	if ($MSBuildPath) {
		if (-not (Test-Path -LiteralPath $MSBuildPath)) {
			throw "MSBuild not found at '$MSBuildPath'."
		}
		return $MSBuildPath
	}
	$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
	if (Test-Path -LiteralPath $vswhere) {
		$found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
			Select-Object -First 1
		if ($found) {
			return $found
		}
	}
	throw 'MSBuild for Visual Studio 2022 was not found. Install the Build Tools as described in README.md, pass -MSBuildPath, or pass -BuildLane.'
}

if (-not $SkipBuild) {
	if ($BuildLane) {
		if (-not (Test-Path -LiteralPath $BuildLane)) {
			throw "Build lane script not found at '$BuildLane'."
		}
		Write-Host "Building DevDbTool (Debug|x86) through the build lane $BuildLane"
		# Exit code 98 means the lane was busy for too long: try again.
		do {
			& powershell -NoProfile -ExecutionPolicy Bypass -File $BuildLane -Root $repoRoot -WaitMinutes 10 -Solution 'tools/DevDbTool/DevDbTool.csproj'
			$buildExit = $LASTEXITCODE
		} while ($buildExit -eq 98)
		if ($buildExit -ne 0) {
			throw "Building DevDbTool failed (build lane exit code $buildExit)."
		}
	}
	else {
		$msbuild = Find-MSBuild
		Write-Host "Building DevDbTool (Debug|x86) with $msbuild"
		& $msbuild $toolProject -nologo '-p:Configuration=Debug' '-p:Platform=x86' '-nodeReuse:false' '-v:minimal' '-clp:Summary;ErrorsOnly'
		if ($LASTEXITCODE -ne 0) {
			throw "Building DevDbTool failed (MSBuild exit code $LASTEXITCODE)."
		}
	}
}
if (-not (Test-Path -LiteralPath $toolExe)) {
	throw "$toolExe does not exist. Run without -SkipBuild."
}

$toolArgs = @('--server', $Server, '--port', "$Port", '--user', $User, '--export-init-sql', $OutFile)
if ($KeepScratchDatabase) {
	$toolArgs += '--keep-scratch'
}
if ($AllowRemoteServer) {
	$toolArgs += '--allow-remote-server'
}

$previousPassword = $env:DEVDB_PASSWORD
try {
	$env:DEVDB_PASSWORD = $Password
	& $toolExe @toolArgs
	$exitCode = $LASTEXITCODE
}
finally {
	$env:DEVDB_PASSWORD = $previousPassword
}
if ($exitCode -ne 0) {
	Write-Error "DevDbTool failed with exit code $exitCode. $OutFile was not changed unless the tool said it wrote it." -ErrorAction Continue
	exit $exitCode
}
Write-Host ''
Write-Host "Wrote $OutFile. Commit it with the fork migration step it belongs to."
exit 0
