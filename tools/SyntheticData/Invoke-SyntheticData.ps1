<#
.SYNOPSIS
Generates synthetic Open Dental 25.3.48 data on the local MariaDB, and records or compares validation reports.

.DESCRIPTION
Builds tools\SyntheticData (unless -SkipBuild) and runs SyntheticData.exe. See tools\SyntheticData\README.md.

The tool only connects to localhost, 127.0.0.1 or ::1, only writes databases whose names start with "synth", and blocks
outbound HTTP. The password, if any, is read from the SYNTH_DB_PASSWORD environment variable.

.PARAMETER Command
Generate, Schema, Validate, Coverage or Compare.

.EXAMPLE
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Generate -Size medium -Force -Out before.json
Creates synth_253 from fox-db-schema.sql, fills it with the medium preset and writes the "before" report.

.EXAMPLE
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Validate -Database synth_253_adopted -Out after.json
.\tools\SyntheticData\Invoke-SyntheticData.ps1 Compare -Before before.json -After after.json -Allowed tools\SyntheticData\allowed-differences.adoption.json
Records the database after adoption and lists every difference that the adoption is not expected to make.
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory=$true,Position=0)]
	[ValidateSet('Generate','Schema','Validate','Coverage','Compare')]
	[string]$Command,
	[string]$Database='synth_253',
	[ValidateSet('small','medium','large')]
	[string]$Size='small',
	[long]$Seed=0,
	[string]$AsOf,
	[int]$Families=0,
	[int]$Years=0,
	[string]$ConfigFrom,
	[int]$FillRemaining=0,
	[switch]$Force,
	[string]$Out,
	[string]$Before,
	[string]$After,
	[string]$Allowed,
	[switch]$Strict,
	[string]$SchemaSql,
	[string]$SchemaFrom,
	[string]$Server='127.0.0.1',
	[int]$Port=3306,
	[string]$User='root',
	[switch]$SkipBuild,
	[string]$MSBuildPath,
	[ValidateSet('Debug','Release')]
	[string]$Configuration='Debug'
)
$ErrorActionPreference='Stop'
$projectDir=$PSScriptRoot
$exe=Join-Path $projectDir "bin\$Configuration\SyntheticData.exe"
if(-not $SkipBuild) {
	if(-not $MSBuildPath) {
		$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
		if(Test-Path $vswhere) {
			$MSBuildPath=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
		}
	}
	if(-not $MSBuildPath -or -not (Test-Path $MSBuildPath)) {
		throw 'MSBuild.exe was not found. Pass -MSBuildPath or -SkipBuild.'
	}
	& $MSBuildPath (Join-Path $projectDir 'SyntheticData.csproj') "-p:Configuration=$Configuration" '-p:Platform=x86' '-nodeReuse:false' '-v:minimal'
	if($LASTEXITCODE -ne 0) {
		throw "Build failed (exit code $LASTEXITCODE)."
	}
}
$arguments=@($Command.ToLowerInvariant())
if($Command -eq 'Compare') {
	if(-not $Before -or -not $After) {
		throw 'Compare needs -Before and -After.'
	}
	$arguments+=@($Before,$After)
	if($Allowed) { $arguments+=@('--allowed',$Allowed) }
}
else {
	$arguments+=@('--server',$Server,'--port',$Port,'--user',$User,'--database',$Database)
	if($Command -eq 'Generate') {
		$arguments+=@('--size',$Size)
		if($Seed -ne 0) { $arguments+=@('--seed',$Seed) }
		if($Families -gt 0) { $arguments+=@('--families',$Families) }
		if($Years -gt 0) { $arguments+=@('--years',$Years) }
		if($ConfigFrom) { $arguments+=@('--config-from',$ConfigFrom) }
		if($FillRemaining -gt 0) { $arguments+=@('--fill-remaining',$FillRemaining) }
	}
	if($Command -eq 'Generate' -or $Command -eq 'Schema') {
		if($Force) { $arguments+='--force' }
		if($SchemaSql) { $arguments+=@('--schema-sql',$SchemaSql) }
		if($SchemaFrom) { $arguments+=@('--schema-from',$SchemaFrom) }
	}
	if($AsOf -and ($Command -eq 'Generate' -or $Command -eq 'Validate')) { $arguments+=@('--as-of',$AsOf) }
	if($Out) { $arguments+=@('--out',$Out) }
	if($Strict) { $arguments+='--strict' }
}
& $exe @arguments
exit $LASTEXITCODE
