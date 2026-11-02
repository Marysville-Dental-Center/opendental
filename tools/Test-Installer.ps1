<#
.SYNOPSIS
Test-installs the Open PMS - Dental setup on this PC without administrator rights, checks the result and uninstalls it.

.DESCRIPTION
Runs the setup that tools\Build-Release.ps1 made, silently and per user (/CURRENTUSER), into new folders under %TEMP%. It never starts
OpenPmsDental.exe (the setup has no [Run] entry) and touches no database. Each scenario writes the setup and uninstall logs to -OutDir.

A. Fresh install with the configuration page values on the command line (/DBSERVER, /DBNAME, /DBUSER) and /NOICONS:
   - the program folder holds exactly the staging folder (same files, same SHA-256), FreeDentalConfig.xml and the uninstall folder;
   - FreeDentalConfig.xml has the given values, no password and NoShowOnStartup False, and BUILTIN\Users may modify it;
   - no uninstaller file at the top level, no Start menu or desktop shortcut, the uninstall entry says version 0.0;
   - no OpenPmsDental process was started;
   then the silent uninstall: the folder and the uninstall entry are gone.
B. Reinstall over an installation: FreeDentalConfig.xml is kept as it is. Files a self-update added, a Logger folder and an A to Z
   images folder are put into the program folder; the uninstall removes everything except the A to Z folder (which this script then
   deletes, since it made it).
C. A folder that already holds other files is refused, and nothing is written into it.

Exit code 0 when every check passed, 1 otherwise.
#>
[CmdletBinding(PositionalBinding=$false)]
param(
	[string]$Setup='',
	[string]$Staging='',
	[string]$OutDir=''
)
Set-StrictMode -Version 2
$ErrorActionPreference='Stop'
$RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not $Staging){ $Staging=Join-Path $RepoRoot 'installer\bin\staging' }
if(-not $Setup){ $Setup=(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'installer\bin') -Filter 'OpenPmsDental-*-Setup.exe' | Select-Object -First 1).FullName }
if(-not $OutDir){ $OutDir=Join-Path $RepoRoot 'installer\bin\install-test' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$AppId='{D5640A25-F302-4CAD-96DC-84AAA8B22F98}_is1'
$UninstallKey="HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$StartMenuLink=Join-Path ([Environment]::GetFolderPath('Programs')) 'Open PMS - Dental.lnk'
$DesktopLink=Join-Path ([Environment]::GetFolderPath('Desktop')) 'Open PMS - Dental.lnk'
$script:Failures=New-Object 'System.Collections.Generic.List[string]'
$script:Passes=0

function Check([bool]$condition,[string]$what){
	if($condition){ $script:Passes++; Write-Host "  ok    $what" }
	else{ $script:Failures.Add($what); Write-Host "  FAIL  $what" }
}

function Get-ProgramPids {
	return @(Get-Process -Name 'OpenPmsDental' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
}

function New-TestFolder([string]$name){
	$folder=Join-Path $env:TEMP ("OpenPmsDental-installtest-{0}-{1}" -f $name,[guid]::NewGuid().ToString('N').Substring(0,8))
	return (Join-Path $folder 'Open PMS - Dental')
}

function Invoke-Setup([string]$dir,[string]$log,[string[]]$extra){
	$arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/PREREQS=skip',"/DIR=`"$dir`"","/LOG=`"$log`"")+$extra
	$process=Start-Process -FilePath $Setup -ArgumentList $arguments -Wait -PassThru
	return $process.ExitCode
}

function Invoke-Uninstall([string]$dir,[string]$log){
	$uninstaller=Join-Path $dir 'uninstall\unins000.exe'
	$process=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$log`"") -Wait -PassThru
	#The uninstaller runs its second phase from a copy in %TEMP%; wait (up to a minute) until it has removed its own files.
	$deadline=(Get-Date).AddSeconds(60)
	while((Test-Path -LiteralPath $uninstaller) -and (Get-Date) -lt $deadline){ Start-Sleep -Milliseconds 500 }
	return $process.ExitCode
}

function Get-FileMap([string]$folder){
	$map=@{}
	foreach($file in Get-ChildItem -LiteralPath $folder -Recurse -File -Force){
		$map[$file.FullName.Substring($folder.Length+1)]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
	}
	return $map
}

if(-not $Setup -or -not (Test-Path -LiteralPath $Setup -PathType Leaf)){ Write-Host 'No setup found; run tools\Build-Release.ps1 first.'; exit 1 }
if(-not (Test-Path -LiteralPath $Staging -PathType Container)){ Write-Host "No staging folder at $Staging"; exit 1 }
if(Test-Path $UninstallKey){ Write-Host "A per-user installation exists already ($UninstallKey); uninstall it first."; exit 1 }
Write-Host "Setup: $Setup"
Write-Host ("Setup SHA-256: {0}" -f (Get-FileHash -LiteralPath $Setup -Algorithm SHA256).Hash.ToLowerInvariant())
$pidsBefore=Get-ProgramPids
$stagingMap=Get-FileMap (Resolve-Path -LiteralPath $Staging).Path

#region A. Fresh install
Write-Host 'A. Fresh per-user install'
$dirA=New-TestFolder 'a'
$exit=Invoke-Setup $dirA (Join-Path $OutDir 'a-install.log') @('/DBSERVER=dbserver.test:3307','/DBNAME=opms_test','/DBUSER=opms_user')
Check ($exit -eq 0) "setup exit code 0 (was $exit)"
Check (Test-Path -LiteralPath (Join-Path $dirA 'OpenPmsDental.exe')) 'OpenPmsDental.exe installed'
$installedMap=Get-FileMap $dirA
$extra=@($installedMap.Keys | Where-Object { -not $stagingMap.ContainsKey($_) } | Sort-Object)
$missing=@($stagingMap.Keys | Where-Object { -not $installedMap.ContainsKey($_) } | Sort-Object)
$changed=@($stagingMap.Keys | Where-Object { $installedMap.ContainsKey($_) -and $installedMap[$_] -ne $stagingMap[$_] } | Sort-Object)
Check ($missing.Count -eq 0) "every staged file installed (missing: $($missing -join ', '))"
Check ($changed.Count -eq 0) "every installed file equals the staged one (different: $($changed -join ', '))"
$extraExpected=@('FreeDentalConfig.xml','uninstall\unins000.dat','uninstall\unins000.exe')
$extraOther=@($extra | Where-Object { $extraExpected -notcontains $_ })
Check ($extraOther.Count -eq 0) "nothing else installed besides FreeDentalConfig.xml and the uninstall folder (also: $($extraOther -join ', '))"
foreach($name in $extraExpected){ Check ($extra -contains $name) "installed $name" }
Check (@(Get-ChildItem -LiteralPath $dirA -Filter 'unins*' -File).Count -eq 0) 'no uninstaller file at the top level'
$configPath=Join-Path $dirA 'FreeDentalConfig.xml'
if(Test-Path -LiteralPath $configPath){
	[xml]$config=Get-Content -LiteralPath $configPath -Raw
	$connection=$config.ConnectionSettings.DatabaseConnection
	Check ($connection.ComputerName -eq 'dbserver.test:3307') "config ComputerName dbserver.test:3307 (was $($connection.ComputerName))"
	Check ($connection.Database -eq 'opms_test') "config Database opms_test (was $($connection.Database))"
	Check ($connection.User -eq 'opms_user') "config User opms_user (was $($connection.User))"
	Check (($connection.Password -eq '') -and ($connection.MySQLPassHash -eq '')) 'config has no password'
	Check ($connection.NoShowOnStartup -eq 'False') 'config NoShowOnStartup False'
	Check ($config.ConnectionSettings.DatabaseType -eq 'MySql') 'config DatabaseType MySql'
	$usersSid=New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-545')
	$rules=@((Get-Acl -LiteralPath $configPath).Access | Where-Object {
		$_.AccessControlType -eq 'Allow' -and $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Equals($usersSid) -and
		($_.FileSystemRights -band [System.Security.AccessControl.FileSystemRights]::Modify) -eq [System.Security.AccessControl.FileSystemRights]::Modify })
	Check ($rules.Count -gt 0) 'BUILTIN\Users may modify FreeDentalConfig.xml'
}
Check (-not (Test-Path -LiteralPath $StartMenuLink)) 'no Start menu shortcut with /NOICONS'
Check (-not (Test-Path -LiteralPath $DesktopLink)) 'no desktop shortcut (the task is unchecked)'
Check (Test-Path $UninstallKey) 'per-user uninstall entry written'
if(Test-Path $UninstallKey){
	$entry=Get-ItemProperty $UninstallKey
	Check ($entry.DisplayVersion -eq '0.0') "uninstall entry version 0.0 (was $($entry.DisplayVersion))"
	Check ($entry.DisplayName -eq 'Open PMS - Dental') "uninstall entry name Open PMS - Dental (was $($entry.DisplayName))"
}
$logA=Get-Content -LiteralPath (Join-Path $OutDir 'a-install.log') -Raw
Check ($logA -match 'Installation process succeeded') 'the setup log says the installation succeeded'
Check ($logA -match 'Wrote FreeDentalConfig\.xml') 'the setup log records the config file'
$pidsNow=Get-ProgramPids
Check (@($pidsNow | Where-Object { $pidsBefore -notcontains $_ }).Count -eq 0) 'setup started no OpenPmsDental process'
$exit=Invoke-Uninstall $dirA (Join-Path $OutDir 'a-uninstall.log')
Check ($exit -eq 0) "uninstall exit code 0 (was $exit)"
Check (-not (Test-Path -LiteralPath $dirA)) 'the program folder is gone after the uninstall'
Check (-not (Test-Path $UninstallKey)) 'the uninstall entry is gone'
#endregion

#region B. Reinstall, self-update files, A to Z folder
Write-Host 'B. Reinstall over an installation, then uninstall with files the self-update added'
$dirB=New-TestFolder 'b'
$exit=Invoke-Setup $dirB (Join-Path $OutDir 'b-install-1.log') @('/DBSERVER=first.test','/DBNAME=opms_first','/DBUSER=first_user')
Check ($exit -eq 0) "first setup exit code 0 (was $exit)"
$configB=Join-Path $dirB 'FreeDentalConfig.xml'
$saved='<?xml version="1.0" encoding="utf-8"?><ConnectionSettings><DatabaseConnection><ComputerName>saved.test</ComputerName><Database>saved_db</Database><User>saved_user</User><Password></Password><MySQLPassHash>osd1$synthetic</MySQLPassHash><NoShowOnStartup>True</NoShowOnStartup></DatabaseConnection><DatabaseType>MySql</DatabaseType><UseDynamicMode>False</UseDynamicMode></ConnectionSettings>'
[System.IO.File]::WriteAllText($configB,$saved)
$exit=Invoke-Setup $dirB (Join-Path $OutDir 'b-install-2.log') @('/DBSERVER=second.test','/DBNAME=opms_second','/DBUSER=second_user')
Check ($exit -eq 0) "second setup exit code 0 (was $exit)"
Check ([System.IO.File]::ReadAllText($configB) -eq $saved) 'a reinstall keeps FreeDentalConfig.xml as it is'
[System.IO.File]::WriteAllText((Join-Path $dirB 'AddedBySelfUpdate.dll'),'synthetic')
New-Item -ItemType Directory -Path (Join-Path $dirB 'Logger') | Out-Null
[System.IO.File]::WriteAllText((Join-Path $dirB 'Logger\synthetic.log'),'synthetic')
$atoz=Join-Path $dirB 'Images'
New-Item -ItemType Directory -Path (Join-Path $atoz 'A\SyntheticPatient1') | Out-Null
[System.IO.File]::WriteAllText((Join-Path $atoz 'A\SyntheticPatient1\note.txt'),'synthetic')
$exit=Invoke-Uninstall $dirB (Join-Path $OutDir 'b-uninstall.log')
Check ($exit -eq 0) "uninstall exit code 0 (was $exit)"
$left=@(Get-ChildItem -LiteralPath $dirB -Force -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
Check (($left.Count -eq 1) -and ($left[0] -eq 'Images')) "only the A to Z folder is left (left: $($left -join ', '))"
Check (Test-Path -LiteralPath (Join-Path $atoz 'A\SyntheticPatient1\note.txt')) 'the A to Z folder is untouched'
Check (-not (Test-Path $UninstallKey)) 'the uninstall entry is gone'
Remove-Item -LiteralPath (Split-Path $dirB -Parent) -Recurse -Force
#endregion

#region C. A folder with other files
Write-Host 'C. A folder that holds other files'
$dirC=New-TestFolder 'c'
New-Item -ItemType Directory -Path $dirC | Out-Null
[System.IO.File]::WriteAllText((Join-Path $dirC 'other.txt'),'synthetic')
$exit=Invoke-Setup $dirC (Join-Path $OutDir 'c-install.log') @()
Check ($exit -ne 0) "setup refuses the folder (exit code $exit)"
$leftC=@(Get-ChildItem -LiteralPath $dirC -Force | ForEach-Object { $_.Name })
Check (($leftC.Count -eq 1) -and ($leftC[0] -eq 'other.txt')) "nothing written into it (holds: $($leftC -join ', '))"
Check (-not (Test-Path $UninstallKey)) 'no uninstall entry'
Remove-Item -LiteralPath (Split-Path $dirC -Parent) -Recurse -Force
#endregion

foreach($dir in @($dirA)){
	$parent=Split-Path $dir -Parent
	if(Test-Path -LiteralPath $parent){ Remove-Item -LiteralPath $parent -Recurse -Force }
}
$pidsNow=Get-ProgramPids
Check (@($pidsNow | Where-Object { $pidsBefore -notcontains $_ }).Count -eq 0) 'no OpenPmsDental process was started'
Write-Host ("{0} checks passed, {1} failed. Logs: {2}" -f $script:Passes,$script:Failures.Count,$OutDir)
if($script:Failures.Count -gt 0){ exit 1 }
exit 0
