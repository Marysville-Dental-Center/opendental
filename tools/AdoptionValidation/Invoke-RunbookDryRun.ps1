<#
.SYNOPSIS
Runs the tool commands of docs\adoption-runbook.md, from the FLOAT check of step 0.2 to step 6.1 and the go-live checks of steps 10.1 and 10.2,
exactly as the runbook gives them, with a new copy instead of the office's adopted database: the dry run of runbook step 0.6.

.DESCRIPTION
Plays the day of the switch, in order, on the admin PC, with -Original standing in for the office database "opendental" and -Copy for
"opendental_pms":

  0.2   FLOAT check of -Original (AdoptionValidation float-check; recorded, not a failure), and what adoption's fork migration will change
        in its rows (AdoptionValidation migration-preview: the report rows it renames, recorded)
  1.1   no connection uses -Original (AdoptionValidation connections)
  3.2   copy -Original into the new database -Copy on the server (AdoptionValidation copy)
  3.3   reports of both, compared: 0 differences
  4-5   adopt and rehash -Copy without starting the program, with the row check against -Original (AdoptionValidation adopt); its log
        names exactly the report row renames migration-preview listed
  6.1   post-adoption report compared with the copy's report under allowed-differences.adoption.json: 0 unexpected differences;
        the Open Dental startup check: every release client refuses the copy
  10.1  no connection uses -Original (AdoptionValidation connections)
  10.2  -Original still equals its report of step 3.3

This is everything before the program's first start (step 6.2). Step 3.1, the folder copy of the database that is the rollback point, runs on
the database server itself (bash) and is not part of this script; neither is anything on the workstations.

It stops at the first unexpected result. Every report and log, and the time of each step, go to -Folder (dry-run-timings.txt). The script
never writes to -Original and never drops a database: drop -Copy afterwards (runbook step 0.6). No copy of -Original is made off the
database server: the copy is made by the server. The tools read through the connection only what they work on (adopt's rehash, for
example, reads the keys, dates, amounts and statuses it hashes) and write to -Folder only reports and logs: row counts, checksums, totals,
and names of tables, columns, preferences and computers.

The tools connect to 127.0.0.1 only, on -Port: on the office's admin PC the local end of the SSH forward to the database server (runbook step
0.0), here the developers' local server. -User is the MariaDB account; its password, if any, is read from the SYNTH_DB_PASSWORD environment
variable, never from the command line.

.EXAMPLE
.\Invoke-RunbookDryRun.ps1 -Original opendental -Copy od_prep_pms -Folder C:\Adoption\dry-run -Port 13306 -User adoption_admin
The office dry run of runbook step 0.6, on the admin PC, from the folder that holds the tools, through the SSH forward of step 0.0.

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
	[ValidateRange(1,65535)][int]$Port=3306,
	[string]$User='root'
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
if($User -notmatch '^[A-Za-z0-9_.-]{1,80}$') {
	throw "'$User' is not a valid MariaDB account name."
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

if(Test-Path $Folder) {
	if(Get-ChildItem -Path $Folder -Force | Select-Object -First 1) {
		throw "$Folder is not empty. Use a new folder, so no earlier report is overwritten."
	}
}
else {
	New-Item -ItemType Directory -Path $Folder | Out-Null
}
$Folder=(Resolve-Path $Folder).Path
#Every tool command gets the same connection: this machine, the given port and account.
$connection=@('--server','127.0.0.1','--port',"$Port",'--user',$User)

$timings=New-Object System.Collections.Generic.List[object]
$notes=New-Object System.Collections.Generic.List[string]
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
#Runs a tool with the connection arguments, shows its output and keeps a copy in the folder.
function Invoke-Tool([string]$Exe,[string[]]$Arguments,[string]$OutFile) {
	$all=@($Arguments)+$connection
	& $Exe @all | Tee-Object -Variable lines | Out-Host
	$lines | Out-File -FilePath (Join-Path $Folder $OutFile) -Encoding utf8
}

$failure=$null
try {
	Invoke-Step '0.2 FLOAT values a logical backup would round (float-check)' {
		Invoke-Tool $adoptionValidation @('float-check','--database',$Original) 'float-check.txt'
		if($LASTEXITCODE -eq 6) {
			$notes.Add("float-check found FLOAT values a logical backup (mariadb-dump) would round (float-check.txt); the runbook's copies are exact.")
		}
		elseif($LASTEXITCODE -ne 0) {
			throw "AdoptionValidation float-check returned exit code $LASTEXITCODE; expected 0 or 6 (a count of values)."
		}
	}
	Invoke-Step '0.2 What the fork migration will change (migration-preview)' {
		Invoke-Tool $adoptionValidation @('migration-preview','--database',$Original) 'migration-preview.txt'
		Assert-ExitCode 'AdoptionValidation migration-preview' 'a list of the report rows adoption renames, possibly none'
		$script:renames=@(Get-Content (Join-Path $Folder 'migration-preview.txt') | ForEach-Object { [regex]::Matches($_,'Renamed displayreport \S+ to \S+ \(same description\)') } |
			ForEach-Object { $_.Value })
		$notes.Add("migration-preview: adoption's fork migration renames $($script:renames.Count) displayreport row(s) (migration-preview.txt).")
	}
	Invoke-Step '1.1 No connection uses the original (connections)' {
		Invoke-Tool $adoptionValidation @('connections','--database',$Original) 'connections-before.txt'
		Assert-ExitCode 'AdoptionValidation connections' "nobody connected to $Original"
	}
	Invoke-Step '3.2 Copy the original on the server (AdoptionValidation copy)' {
		Invoke-Tool $adoptionValidation @('copy','--original',$Original,'--database',$Copy) 'copy.txt'
		Assert-ExitCode 'AdoptionValidation copy' 'Copy: PASS'
	}
	Invoke-Step '3.3 Reports of both and their comparison' {
		Invoke-Tool $syntheticData @('validate','--database',$Original,'--as-of',$AsOf,'--out',(Join-Path $Folder 'baseline-original.json')) 'baseline-original.txt'
		Assert-ExitCode 'SyntheticData validate (original)' 'a report'
		Invoke-Tool $syntheticData @('validate','--database',$Copy,'--as-of',$AsOf,'--out',(Join-Path $Folder 'baseline-copy.json')) 'baseline-copy.txt'
		Assert-ExitCode 'SyntheticData validate (copy)' 'a report'
		& $syntheticData compare (Join-Path $Folder 'baseline-original.json') (Join-Path $Folder 'baseline-copy.json') |
			Tee-Object -Variable lines | Out-Host
		$lines | Out-File -FilePath (Join-Path $Folder 'compare-baselines.txt') -Encoding utf8
		Assert-ExitCode 'SyntheticData compare (baselines)' 'Unexpected differences: 0, the copy equals the original'
	}
	Invoke-Step '4-5 Adopt and rehash the copy (AdoptionValidation adopt)' {
		$all=@('adopt','--database',$Copy,'--original',$Original,'--confirm',$Copy,'--log',(Join-Path $Folder 'adoption-log.txt'))+$connection
		& $adoptionValidation @all
		Assert-ExitCode 'AdoptionValidation adopt' 'Adopt: PASS'
		#Step 4 of the runbook: the fork migration renames exactly the report rows the preview listed (adopt's row check holds it to the same plan).
		$logged=@(Get-Content (Join-Path $Folder 'adoption-log.txt') | ForEach-Object { [regex]::Matches($_,'Renamed displayreport \S+ to \S+ \(same description\)') } |
			ForEach-Object { $_.Value })
		$missing=@($script:renames | Where-Object { $logged -notcontains $_ })
		$extra=@($logged | Where-Object { $script:renames -notcontains $_ })
		if($missing.Count -gt 0 -or $extra.Count -gt 0) {
			throw "The fork migration's renames in adoption-log.txt differ from migration-preview.txt: missing $($missing -join '; '), not previewed $($extra -join '; ')."
		}
	}
	Invoke-Step '6.1 Post-adoption report, comparison and startup check' {
		Invoke-Tool $syntheticData @('validate','--database',$Copy,'--as-of',$AsOf,'--out',(Join-Path $Folder 'after-adoption.json')) 'after-adoption.txt'
		Assert-ExitCode 'SyntheticData validate (adopted copy)' 'a report'
		& $syntheticData compare (Join-Path $Folder 'baseline-copy.json') (Join-Path $Folder 'after-adoption.json') --allowed $allowed |
			Tee-Object -Variable lines | Out-Host
		$lines | Out-File -FilePath (Join-Path $Folder 'compare.txt') -Encoding utf8
		Assert-ExitCode 'SyntheticData compare (after adoption)' 'Unexpected differences: 0'
		Invoke-Tool $adoptionValidation @('startup-check','--database',$Copy) 'startup-check.txt'
		Assert-ExitCode 'AdoptionValidation startup-check' 'every Open Dental release client refuses the adopted copy'
	}
	Invoke-Step '10.1 No connection uses the original (connections)' {
		Invoke-Tool $adoptionValidation @('connections','--database',$Original) 'connections-golive.txt'
		Assert-ExitCode 'AdoptionValidation connections' "nobody connected to $Original"
	}
	Invoke-Step '10.2 The original is unchanged' {
		Invoke-Tool $syntheticData @('validate','--database',$Original,'--as-of',$AsOf,'--out',(Join-Path $Folder 'golive-original.json')) 'golive-original.txt'
		Assert-ExitCode 'SyntheticData validate (original again)' 'a report'
		& $syntheticData compare (Join-Path $Folder 'baseline-original.json') (Join-Path $Folder 'golive-original.json') |
			Tee-Object -Variable lines | Out-Host
		$lines | Out-File -FilePath (Join-Path $Folder 'compare-original.txt') -Encoding utf8
		Assert-ExitCode 'SyntheticData compare (original)' 'Unexpected differences: 0, nothing wrote to the original'
	}
}
catch {
	$failure=$_.Exception.Message
}

$total=($timings | Measure-Object -Property Seconds -Sum).Sum
$summary=@()
$summary+="Runbook dry run, the tool commands of steps 0.2, 1.1 and 3.2 to 6.1 (everything before the program's first start) and the go-live checks of steps 10.1 and 10.2: $Original -> $Copy on 127.0.0.1:$Port as $User, as of $AsOf, $(Get-Date -Format 'yyyy-MM-dd HH:mm')."
$summary+='Step 3.1 (the folder copy on the database server) is not part of this script.'
$summary+=($timings | Format-Table -AutoSize | Out-String).TrimEnd()
$summary+=("Total: {0:0.0} s" -f $total)
foreach($note in $notes) {
	$summary+="Note: $note"
}
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
