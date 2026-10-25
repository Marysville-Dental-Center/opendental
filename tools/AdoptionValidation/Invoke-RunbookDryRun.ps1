<#
.SYNOPSIS
Runs steps 3.1 to 6 of docs\adoption-runbook.md, with the runbook's own commands, against a copy instead of the office database: the dry
run of runbook step 0.6.

.DESCRIPTION
Plays the day of the switch, in order, with -Original standing in for the office database "opendental" and -Copy for "opendental_pms":

  3.1  back up -Original with mariadb-dump (the runbook's options)
  3.2  restore the backup as the new database -Copy (AdoptionValidation restore)
  3.3  baseline reports of both, compared: 0 differences
  4-5  adopt and rehash -Copy without starting the program (AdoptionValidation adopt)
  6    post-adoption report compared with the copy's baseline under allowed-differences.adoption.json: 0 unexpected differences;
       the Open Dental startup check: every release client refuses the copy
  10   the go-live check: no connection uses -Original, and -Original still equals its baseline

It stops at the first unexpected result. Every report and log, and the time of each step, go to -Folder (dry-run-timings.txt). The backup
file holds the data of -Original; it is deleted at the end unless -KeepBackup. The script never writes to -Original and never drops a
database: drop -Copy afterwards (runbook step 0.6).

The tools connect to this machine only. The MariaDB root password, if any, is read from the SYNTH_DB_PASSWORD environment variable and
passed to the MariaDB client programs through MYSQL_PWD, never on the command line.

.EXAMPLE
.\Invoke-RunbookDryRun.ps1 -Original od_prep -Copy od_prep_pms -Folder D:\Adoption\dry-run
The office dry run of runbook step 0.6, from the folder that holds the tools.

.EXAMPLE
.\tools\AdoptionValidation\Invoke-RunbookDryRun.ps1 -Original synth_m1z_dry_r1_src -Copy synth_m1z_dry_pms -Folder C:\Temp\dry-run -AsOf 2026-09-30
The developers' dry run on a synthetic database kept by "AdoptionValidation rehearse --runs 1 --keep --tag dry".
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory=$true)][string]$Original,
	[Parameter(Mandatory=$true)][string]$Copy,
	[Parameter(Mandatory=$true)][string]$Folder,
	[string]$AsOf=(Get-Date -Format 'yyyy-MM-dd'),
	[string]$Tools,
	[string]$MariaDbBin,
	[switch]$KeepBackup
)
$ErrorActionPreference='Stop'

foreach($name in @($Original,$Copy)) {
	if($name -notmatch '^[A-Za-z0-9_-]{1,64}$') {
		throw "'$name' is not a valid database name."
	}
}
if($Original -eq $Copy) {
	throw '-Original and -Copy must be different databases.'
}
if($AsOf -notmatch '^\d{4}-\d{2}-\d{2}$') {
	throw '-AsOf is a date written yyyy-MM-dd.'
}

#The tools: next to this script (a copied bin folder), or the build output of the source tree.
if(-not $Tools) {
	foreach($candidate in @($PSScriptRoot,(Join-Path $PSScriptRoot 'bin\Release'),(Join-Path $PSScriptRoot 'bin\Debug'))) {
		if(Test-Path (Join-Path $candidate 'AdoptionValidation.exe')) {
			$Tools=$candidate
			break
		}
	}
}
if(-not $Tools) {
	throw 'AdoptionValidation.exe was not found next to this script or in bin\Release or bin\Debug. Pass -Tools.'
}
$Tools=(Resolve-Path $Tools).Path
$adoptionValidation=Join-Path $Tools 'AdoptionValidation.exe'
$syntheticData=Join-Path $Tools 'SyntheticData.exe'
$allowed=Join-Path $Tools 'allowed-differences.adoption.json'
foreach($path in @($adoptionValidation,$syntheticData,$allowed)) {
	if(-not (Test-Path $path)) {
		throw "$path was not found. -Tools must be the folder with AdoptionValidation.exe, SyntheticData.exe and allowed-differences.adoption.json."
	}
}

#The MariaDB client programs.
if(-not $MariaDbBin) {
	$MariaDbBin=$env:MARIADB_BIN
}
if(-not $MariaDbBin) {
	$command=Get-Command 'mariadb-dump.exe' -ErrorAction SilentlyContinue
	if($command) {
		$MariaDbBin=Split-Path $command.Source
	}
}
if(-not $MariaDbBin) {
	#ProgramW6432 is the 64-bit Program Files folder, also from a 32-bit PowerShell.
	$roots=@($env:ProgramW6432,$env:ProgramFiles) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
	$found=Get-ChildItem -Path $roots -Directory -Filter 'MariaDB*' -ErrorAction SilentlyContinue | Sort-Object Name -Descending |
		Where-Object { Test-Path (Join-Path $_.FullName 'bin\mariadb-dump.exe') } | Select-Object -First 1
	if($found) {
		$MariaDbBin=Join-Path $found.FullName 'bin'
	}
}
if(-not $MariaDbBin -or -not (Test-Path (Join-Path $MariaDbBin 'mariadb-dump.exe')) -or -not (Test-Path (Join-Path $MariaDbBin 'mariadb.exe'))) {
	throw 'mariadb-dump.exe and mariadb.exe were not found. Pass -MariaDbBin (the MariaDB bin folder) or set MARIADB_BIN.'
}
$mariadbDump=Join-Path $MariaDbBin 'mariadb-dump.exe'
$mariadb=Join-Path $MariaDbBin 'mariadb.exe'
$env:MARIADB_BIN=$MariaDbBin

if(Test-Path $Folder) {
	if(Get-ChildItem -Path $Folder -Force | Select-Object -First 1) {
		throw "$Folder is not empty. Use a new folder, so no earlier report or backup is overwritten."
	}
}
else {
	New-Item -ItemType Directory -Path $Folder | Out-Null
}
$Folder=(Resolve-Path $Folder).Path
$backup=Join-Path $Folder "$Original-pre-adoption.sql"
$connection=@('--host=127.0.0.1','--port=3306','--user=root','--protocol=TCP')
$passwordSaved=$env:MYSQL_PWD
if($env:SYNTH_DB_PASSWORD) {
	$env:MYSQL_PWD=$env:SYNTH_DB_PASSWORD
}

$timings=New-Object System.Collections.Generic.List[object]
function Invoke-Step([string]$Name,[scriptblock]$Block) {
	Write-Host "== $Name"
	$stopwatch=[System.Diagnostics.Stopwatch]::StartNew()
	& $Block
	$stopwatch.Stop()
	$timings.Add([pscustomobject]@{ Step=$Name; Seconds=[math]::Round($stopwatch.Elapsed.TotalSeconds,1) })
}
function Assert-ExitCode([string]$What,[string]$Expected) {
	if($LASTEXITCODE -ne 0) {
		throw "$What returned exit code $LASTEXITCODE; expected 0 ($Expected)."
	}
}
#Runs a tool, shows its output and keeps a copy in the folder.
function Invoke-Tool([string]$Exe,[string[]]$Arguments,[string]$OutFile) {
	& $Exe @Arguments | Tee-Object -Variable lines | Out-Host
	$lines | Out-File -FilePath (Join-Path $Folder $OutFile) -Encoding utf8
}

$failure=$null
try {
	Invoke-Step '3.1 Back up the original (mariadb-dump)' {
		& $mariadbDump @connection --lock-tables --routines --triggers --events --hex-blob --default-character-set=utf8mb4 "--result-file=$backup" $Original
		Assert-ExitCode 'mariadb-dump' 'a complete backup'
		Write-Host ("   {0:N0} bytes" -f (Get-Item $backup).Length)
	}
	Invoke-Step '3.2 Restore the copy (AdoptionValidation restore)' {
		Invoke-Tool $adoptionValidation @('restore','--dump',$backup,'--database',$Copy) 'restore.txt'
		Assert-ExitCode 'AdoptionValidation restore' 'the copy restored'
	}
	Invoke-Step '3.3 Baseline reports and comparison' {
		Invoke-Tool $syntheticData @('validate','--database',$Original,'--as-of',$AsOf,'--out',(Join-Path $Folder 'baseline-original.json')) 'baseline-original.txt'
		Assert-ExitCode 'SyntheticData validate (original)' 'a report'
		Invoke-Tool $syntheticData @('validate','--database',$Copy,'--as-of',$AsOf,'--out',(Join-Path $Folder 'baseline-copy.json')) 'baseline-copy.txt'
		Assert-ExitCode 'SyntheticData validate (copy)' 'a report'
		Invoke-Tool $syntheticData @('compare',(Join-Path $Folder 'baseline-original.json'),(Join-Path $Folder 'baseline-copy.json')) 'compare-baselines.txt'
		Assert-ExitCode 'SyntheticData compare (baselines)' 'Unexpected differences: 0, the copy equals the original'
	}
	Invoke-Step '4-5 Adopt and rehash the copy (AdoptionValidation adopt)' {
		& $adoptionValidation adopt --database $Copy --original $Original --confirm $Copy --log (Join-Path $Folder 'adoption-log.txt')
		Assert-ExitCode 'AdoptionValidation adopt' 'Adopt: PASS'
	}
	Invoke-Step '6 Post-adoption report, comparison and startup check' {
		Invoke-Tool $syntheticData @('validate','--database',$Copy,'--as-of',$AsOf,'--out',(Join-Path $Folder 'after-adoption.json')) 'after-adoption.txt'
		Assert-ExitCode 'SyntheticData validate (adopted copy)' 'a report'
		Invoke-Tool $syntheticData @('compare',(Join-Path $Folder 'baseline-copy.json'),(Join-Path $Folder 'after-adoption.json'),'--allowed',$allowed) 'compare.txt'
		Assert-ExitCode 'SyntheticData compare (after adoption)' 'Unexpected differences: 0'
		Invoke-Tool $adoptionValidation @('startup-check','--database',$Copy) 'startup-check.txt'
		Assert-ExitCode 'AdoptionValidation startup-check' 'every Open Dental release client refuses the adopted copy'
	}
	Invoke-Step '10 Go-live check: the original is unused and unchanged' {
		$connections=& $mariadb @connection --batch --skip-column-names -e "SELECT ID,USER,HOST,COMMAND FROM information_schema.PROCESSLIST WHERE DB='$Original' AND ID<>CONNECTION_ID()"
		Assert-ExitCode 'mariadb (process list)' 'the process list'
		if($connections) {
			throw "Connections use $Original`: $($connections -join '; ')"
		}
		Write-Host "   No connection uses $Original."
		Invoke-Tool $syntheticData @('validate','--database',$Original,'--as-of',$AsOf,'--out',(Join-Path $Folder 'golive-original.json')) 'golive-original.txt'
		Assert-ExitCode 'SyntheticData validate (original again)' 'a report'
		Invoke-Tool $syntheticData @('compare',(Join-Path $Folder 'baseline-original.json'),(Join-Path $Folder 'golive-original.json')) 'compare-original.txt'
		Assert-ExitCode 'SyntheticData compare (original)' 'Unexpected differences: 0, nothing wrote to the original'
	}
}
catch {
	$failure=$_.Exception.Message
}
finally {
	$env:MYSQL_PWD=$passwordSaved
	if(-not $KeepBackup -and (Test-Path $backup)) {
		Remove-Item -LiteralPath $backup
		Write-Host "Deleted the backup file $backup (it holds the data of $Original)."
	}
}

$total=($timings | Measure-Object -Property Seconds -Sum).Sum
$summary=@()
$summary+="Runbook dry run, steps 3.1 to 6 and the go-live check: $Original -> $Copy, as of $AsOf, $(Get-Date -Format 'yyyy-MM-dd HH:mm')."
$summary+=($timings | Format-Table -AutoSize | Out-String).TrimEnd()
$summary+=("Total: {0:0.0} s" -f $total)
if($failure) {
	$summary+="Dry run: FAIL. $failure"
}
else {
	$summary+='Dry run: PASS'
}
$summary | Out-File -FilePath (Join-Path $Folder 'dry-run-timings.txt') -Encoding utf8
$summary | Out-Host
if($failure) {
	exit 1
}
exit 0
