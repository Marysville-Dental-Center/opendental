<#
.SYNOPSIS
Loads an Open Dental reference schema into a local, empty database: od_261_schema (Open Dental 26.1.70) by default.

.DESCRIPTION
Creates the database on the local MariaDB/MySQL server and runs the reference DDL that the schema verifier embeds:

  26.1.70  OpenPmsDentalBusiness\Misc\ForkMigrations\Reference\OdSchema-26.1.70.sql  ->  od_261_schema  (427 tables)
  25.3.48  OpenPmsDentalBusiness\Misc\ForkMigrations\Reference\OdSchema-25.3.48.sql  ->  od_253_schema  (422 tables)

The 26.1.70 reference is built from Open Dental's published database documents only; the NULL/NOT NULL, defaults and keys that the documents
do not publish are marked INFERRED in the file. The database has tables and no rows. Tests and tools that compare a database with the
reference use it when it is there (for example ForkMigrationTests and M201Tests).

Safety: only 127.0.0.1 or localhost, only a database named od_<digits>_schema, and an existing database is dropped only with -Force and only
when none of its tables holds a row.

.PARAMETER Version
Which reference to load: 26.1.70 (default) or 25.3.48.

.PARAMETER Name
Database name. Default: od_261_schema for 26.1.70, od_253_schema for 25.3.48. Must look like od_<digits>_schema.

.PARAMETER Server
127.0.0.1 (default) or localhost. No other server is accepted.

.PARAMETER Port
Default 3306.

.PARAMETER User
Default root. The password, if any, is read from the DEVDB_PASSWORD environment variable (blank by default) and handed to the client in
MYSQL_PWD, never on the command line.

.PARAMETER Client
Full path to mariadb.exe or mysql.exe. Default: found on PATH or under C:\Program Files\MariaDB*\bin and C:\Program Files\MySQL\*\bin.

.PARAMETER Force
Drop and recreate the database if it exists (only when it holds no rows).

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\New-OdReferenceDatabase.ps1 -Force
Recreates od_261_schema from the 26.1.70 reference.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\New-OdReferenceDatabase.ps1 -Version 25.3.48
Creates od_253_schema from the 25.3.48 reference if it does not exist yet.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
	[ValidateSet('26.1.70', '25.3.48')]
	[string]$Version = '26.1.70',
	[string]$Name = '',
	[ValidateSet('127.0.0.1', 'localhost')]
	[string]$Server = '127.0.0.1',
	[ValidateRange(1, 65535)]
	[int]$Port = 3306,
	[ValidateNotNullOrEmpty()]
	[string]$User = 'root',
	[string]$Client = '',
	[switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$referenceDir = Join-Path $repoRoot 'OpenPmsDentalBusiness\Misc\ForkMigrations\Reference'
$references = @{
	'26.1.70' = @{ File = 'OdSchema-26.1.70.sql'; Name = 'od_261_schema'; Tables = 427 }
	'25.3.48' = @{ File = 'OdSchema-25.3.48.sql'; Name = 'od_253_schema'; Tables = 422 }
}
$reference = $references[$Version]
$sqlFile = Join-Path $referenceDir $reference.File
if (-not $Name) {
	$Name = $reference.Name
}
if ($Name -notmatch '^od_[0-9]+_schema$') {
	throw "Refusing database name '$Name': reference databases are named od_<digits>_schema, so this script can never drop anything else."
}
if (-not (Test-Path -LiteralPath $sqlFile)) {
	throw "Reference schema not found: $sqlFile"
}

function Find-Client {
	if ($Client) {
		if (-not (Test-Path -LiteralPath $Client)) {
			throw "Client not found at '$Client'."
		}
		return $Client
	}
	foreach ($exe in @('mariadb.exe', 'mysql.exe')) {
		$command = Get-Command $exe -ErrorAction SilentlyContinue | Select-Object -First 1
		if ($command) {
			return $command.Source
		}
	}
	$candidates = @()
	foreach ($pattern in @('C:\Program Files\MariaDB*\bin\mariadb.exe', 'C:\Program Files\MariaDB*\bin\mysql.exe', 'C:\Program Files\MySQL\*\bin\mysql.exe')) {
		$candidates += @(Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
	}
	if ($candidates.Count -gt 0) {
		return $candidates[0].FullName
	}
	throw 'No mariadb.exe or mysql.exe found. Install the MariaDB client or pass -Client.'
}

$clientExe = Find-Client
$baseArgs = @("--host=$Server", "--port=$Port", "--user=$User", '--batch', '--skip-column-names')
$previousPwd = $env:MYSQL_PWD
$env:MYSQL_PWD = $env:DEVDB_PASSWORD

function Invoke-Sql([string]$sql, [string]$database = '') {
	$arguments = $baseArgs
	if ($database) {
		$arguments += "--database=$database"
	}
	$output = & $clientExe @arguments "--execute=$sql"
	if ($LASTEXITCODE -ne 0) {
		throw "The client failed (exit code $LASTEXITCODE) on: $sql"
	}
	return $output
}

try {
	$exists = Invoke-Sql "SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='$Name'"
	if ("$exists".Trim() -ne '0') {
		if (-not $Force) {
			Write-Host "$Name already exists. Pass -Force to recreate it."
			exit 0
		}
		$rows = Invoke-Sql "SELECT COALESCE(SUM(TABLE_ROWS),0) FROM information_schema.TABLES WHERE TABLE_SCHEMA='$Name'"
		if ("$rows".Trim() -ne '0') {
			throw "Refusing to drop $Name`: its tables hold rows, so it is not an empty reference database."
		}
		Invoke-Sql "DROP DATABASE ``$Name``" | Out-Null
	}
	Invoke-Sql "CREATE DATABASE ``$Name`` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci" | Out-Null
	$sourcePath = $sqlFile -replace '\\', '/'
	Invoke-Sql "source $sourcePath" $Name | Out-Null
	$count = "$(Invoke-Sql "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='$Name'")".Trim()
	if ($count -ne "$($reference.Tables)") {
		throw "$Name has $count tables after loading $($reference.File); expected $($reference.Tables)."
	}
	Write-Host "Loaded Open Dental $Version reference schema into $Name on ${Server}:$Port ($count tables, no rows)."
}
finally {
	$env:MYSQL_PWD = $previousPwd
}
