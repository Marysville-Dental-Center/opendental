<#
.SYNOPSIS
Runs the M1-Z adoption rehearsal on synthetic data on the local MariaDB.

.DESCRIPTION
Builds tools\AdoptionValidation (unless -SkipBuild) and runs AdoptionValidation.exe rehearse. See tools\AdoptionValidation\README.md and
docs\adoption-runbook.md.

The tool only connects to localhost, 127.0.0.1 or ::1, only writes databases whose names start with "synth_m1z_" (or -Prefix), and blocks
outbound HTTP. The password, if any, is read from the SYNTH_DB_PASSWORD environment variable.

.EXAMPLE
.\tools\AdoptionValidation\Invoke-AdoptionRehearsal.ps1
Medium preset, two runs, results in .\adoption-rehearsal-<time>.

.EXAMPLE
.\tools\AdoptionValidation\Invoke-AdoptionRehearsal.ps1 -Size small -Runs 1 -Out C:\Temp\rehearsal -Keep
#>
[CmdletBinding()]
param(
	[ValidateSet('small','medium','large')]
	[string]$Size='medium',
	[int]$Runs=2,
	[long]$Seed=0,
	[int]$FillRemaining=-1,
	[ValidateSet('','server','dump')]
	[string]$Copy='',
	[string]$Out,
	[switch]$Keep,
	[switch]$SkipRoundTrip,
	[string]$Prefix,
	[string]$Server='127.0.0.1',
	[int]$Port=3306,
	[string]$User='root',
	[switch]$SkipBuild,
	[string]$MSBuildPath
)
$ErrorActionPreference='Stop'
$projectDir=$PSScriptRoot
$exe=Join-Path $projectDir 'bin\Debug\AdoptionValidation.exe'
if(-not $SkipBuild) {
	if(-not $MSBuildPath) {
		$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
		if(Test-Path $vswhere) {
			$MSBuildPath=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
		}
	}
	if(-not $MSBuildPath -or -not (Test-Path $MSBuildPath)) {
		throw 'MSBuild.exe was not found. Pass -MSBuildPath or build tools\AdoptionValidation\AdoptionValidation.csproj first and use -SkipBuild.'
	}
	& $MSBuildPath (Join-Path $projectDir 'AdoptionValidation.csproj') -restore -p:RestorePackagesConfig=true -p:Configuration=Debug -p:Platform=x86 -v:minimal -nologo
	if($LASTEXITCODE -ne 0) {
		throw "Build failed with exit code $LASTEXITCODE."
	}
}
$arguments=@('rehearse','--size',$Size,'--runs',$Runs,'--server',$Server,'--port',$Port,'--user',$User)
if($Seed -ne 0) { $arguments+=@('--seed',$Seed) }
if($FillRemaining -ge 0) { $arguments+=@('--fill-remaining',$FillRemaining) }
if($Copy) { $arguments+=@('--copy',$Copy) }
if($Out) { $arguments+=@('--out',$Out) }
if($Keep) { $arguments+='--keep' }
if($SkipRoundTrip) { $arguments+='--skip-roundtrip' }
if($Prefix) { $arguments+=@('--prefix',$Prefix) }
& $exe @arguments
exit $LASTEXITCODE
