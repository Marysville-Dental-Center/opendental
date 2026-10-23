<#
.SYNOPSIS
Creates a local development database for this codebase. No installer or trial version is needed.

.DESCRIPTION
Builds tools\DevDbTool, which also builds OpenDentBusiness, CodeBase and DataConnectionBase when they are out of
date, and runs it to:

  1. create the database on a local MariaDB/MySQL server, dropping it first with -Force;
  2. load the seed dump UnitTests\Resources\dump.txt, a version 21.1.9 database;
  3. upgrade it to this build's version with the convert scripts in OpenDentBusiness (frozen at 24.3.63) and record ProgramVersion;
  4. adopt it (OpenDentBusiness\Misc\ForkMigrations\Adoption.cs, as the adoption tool does), which writes the fork version markers and runs
     the fork migrations that bring the schema to Open Dental 25.3.48, so the startup version guard opens it;
  5. apply the default auto codes, procedure buttons and appointment quick-add procedures;
  6. create an A to Z images folder, and optionally add fictional sample data;
  7. verify the version preferences or fork markers, that every table and column the code expects exists, and that the schema matches
     Open Dental 25.3.48 apart from the documented exceptions.

The tool talks only to the database server. Outbound HTTP is blocked, and any attempt is reported with exit code 3.
The default user Admin has a blank password.

-Force refuses to drop a database holding more than 200 patients, and the tool refuses any server other than
localhost/127.0.0.1 unless you pass -AllowRemoteServer.

.PARAMETER Name
Database to create. Default: opendental_dev.

.PARAMETER Server
Database server host. Default: 127.0.0.1.

.PARAMETER Port
Database server port. Default: 3306.

.PARAMETER User
MySQL user. Default: root.

.PARAMETER Password
MySQL password. Default: blank. Passed to the tool through the DEVDB_PASSWORD environment variable, not the command line.

.PARAMETER Force
Drop and recreate the database if it already exists.

.PARAMETER WithSampleData
Add a few fictional providers, operatories, patients, schedules and appointments.

.PARAMETER AtoZPath
A to Z images folder to create and store in the DocPath preference.
Default: %USERPROFILE%\OpenDentImagesDev\<Name>.

.PARAMETER NoAtoZ
Leave DocPath as the seed dump has it (C:\OpenDentImages\) and create no folder.

.PARAMETER WriteConfig
Write OpenDental\bin\<Configuration>\FreeDentalConfig.xml from tools\FreeDentalConfig.dev.xml so that OpenDental.exe
opens this database. An existing file is kept as FreeDentalConfig.xml.bak.

.PARAMETER SkipForkMigrations
Stop after the 24.3.63 convert scripts: a plain, unadopted 24.3.63 database without the fork migrations, for testing the migrations
(OpenDentBusiness\Misc\ForkMigrations) and adoption themselves. OpenDental.exe asks for adoption before it opens such a database.
With -VerifyOnly, check such a database without expecting the migrated schema.

.PARAMETER VerifyOnly
Only check an existing database. Nothing is changed.

.PARAMETER SkipBuild
Run the already-built tools\DevDbTool\bin\<Configuration>\DevDbTool.exe without building.

.PARAMETER MSBuildPath
Full path to MSBuild.exe. Default: found with vswhere.

.PARAMETER Configuration
Build configuration for the tool and for -WriteConfig. Default: Debug.

.PARAMETER AllowRemoteServer
Allow a server other than localhost/127.0.0.1.

.EXAMPLE
.\tools\New-DevDatabase.ps1
Creates opendental_dev on 127.0.0.1:3306.

.EXAMPLE
.\tools\New-DevDatabase.ps1 -Force -WithSampleData -WriteConfig
Recreates opendental_dev with sample data and points OpenDental\bin\Debug\OpenDental.exe at it.

.EXAMPLE
.\tools\New-DevDatabase.ps1 -Name mydev -VerifyOnly
Checks an existing database without changing it.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
	[ValidatePattern('^[A-Za-z0-9_]{1,64}$')]
	[string]$Name = 'opendental_dev',
	[ValidateNotNullOrEmpty()]
	[string]$Server = '127.0.0.1',
	[ValidateRange(1, 65535)]
	[int]$Port = 3306,
	[ValidateNotNullOrEmpty()]
	[string]$User = 'root',
	[string]$Password = '',
	[switch]$Force,
	[switch]$WithSampleData,
	[string]$AtoZPath = '',
	[switch]$NoAtoZ,
	[switch]$WriteConfig,
	[switch]$VerifyOnly,
	[switch]$SkipForkMigrations,
	[switch]$SkipBuild,
	[string]$MSBuildPath = '',
	[ValidateSet('Debug', 'Release')]
	[string]$Configuration = 'Debug',
	[switch]$AllowRemoteServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $PSScriptRoot 'DevDbTool\DevDbTool.csproj'
$toolExe = Join-Path $PSScriptRoot "DevDbTool\bin\$Configuration\DevDbTool.exe"
$configTemplate = Join-Path $PSScriptRoot 'FreeDentalConfig.dev.xml'

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
	throw 'MSBuild for Visual Studio 2022 was not found. Install the Build Tools as described in README.md, or pass -MSBuildPath.'
}

function Write-DevConfig {
	$binDir = Join-Path $repoRoot "OpenDental\bin\$Configuration"
	if (-not (Test-Path -LiteralPath $binDir)) {
		New-Item -ItemType Directory -Path $binDir | Out-Null
	}
	$target = Join-Path $binDir 'FreeDentalConfig.xml'
	# Keep the template's whitespace as is. Re-indenting on save would put a line break inside the empty <Password></Password>,
	# and OpenDental.exe would read that whitespace as the password.
	$xml = New-Object System.Xml.XmlDocument
	$xml.PreserveWhitespace = $true
	$xml.Load($configTemplate)
	$computerName = $Server
	if ($Port -ne 3306) {
		$computerName = "${Server}:$Port"
	}
	$xml.SelectSingleNode('/ConnectionSettings/DatabaseConnection/ComputerName').InnerText = $computerName
	$xml.SelectSingleNode('/ConnectionSettings/DatabaseConnection/Database').InnerText = $Name
	$xml.SelectSingleNode('/ConnectionSettings/DatabaseConnection/User').InnerText = $User
	# Plain text is what OpenDental.exe falls back to when it cannot encrypt; fine for a dev server.
	$xml.SelectSingleNode('/ConnectionSettings/DatabaseConnection/Password').InnerText = $Password
	if (Test-Path -LiteralPath $target) {
		Copy-Item -LiteralPath $target -Destination "$target.bak" -Force
		Write-Host "Kept the previous config as $target.bak"
	}
	$xml.Save($target)
	Write-Host "Wrote $target"
}

if (-not $SkipBuild) {
	$msbuild = Find-MSBuild
	Write-Host "Building DevDbTool ($Configuration|x86) with $msbuild"
	& $msbuild $toolProject -nologo "-p:Configuration=$Configuration" '-p:Platform=x86' '-nodeReuse:false' '-v:minimal' '-clp:Summary;ErrorsOnly'
	if ($LASTEXITCODE -ne 0) {
		throw "Building DevDbTool failed (MSBuild exit code $LASTEXITCODE)."
	}
}
if (-not (Test-Path -LiteralPath $toolExe)) {
	throw "$toolExe does not exist. Run without -SkipBuild."
}

$toolArgs = @('--server', $Server, '--port', "$Port", '--user', $User, '--database', $Name)
if ($VerifyOnly) {
	$toolArgs += '--verify-only'
}
else {
	if ($Force) {
		$toolArgs += '--force'
	}
	if ($WithSampleData) {
		$toolArgs += '--with-sample-data'
	}
	if (-not $NoAtoZ) {
		if (-not $AtoZPath) {
			$AtoZPath = Join-Path $env:USERPROFILE "OpenDentImagesDev\$Name"
		}
		$toolArgs += @('--atoz', $AtoZPath)
	}
}
if ($AllowRemoteServer) {
	$toolArgs += '--allow-remote-server'
}
if ($SkipForkMigrations) {
	$toolArgs += '--no-fork-migrations'
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

if ($exitCode -ne 0 -and $exitCode -ne 3) {
	Write-Error "DevDbTool failed with exit code $exitCode." -ErrorAction Continue
	exit $exitCode
}
if ($WriteConfig) {
	Write-DevConfig
}
if (-not $VerifyOnly) {
	Write-Host ''
	Write-Host "Dev database '$Name' is ready on ${Server}:$Port. Log in as Admin with a blank password."
	if (-not $WriteConfig) {
		Write-Host 'To point OpenDental.exe at it, rerun with -WriteConfig (add -VerifyOnly to skip rebuilding the database), or copy'
		Write-Host "tools\FreeDentalConfig.dev.xml to OpenDental\bin\$Configuration\FreeDentalConfig.xml and set <Database> to $Name."
	}
}
if ($exitCode -eq 3) {
	Write-Warning 'The tool blocked outbound network requests (listed above). The database was built, but please report this.'
}
exit $exitCode
