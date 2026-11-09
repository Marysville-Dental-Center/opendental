# TLS to the local test database server for the tool scripts (O-12, docs/port-notes/DB-TLS.md), by the rules of
# DataConnectionBase.DevDbTls (DataConnectionBase\DevDbTls.cs): the CA certificate that tools\Enable-LocalDbTls.ps1 writes to
# %LOCALAPPDATA%\OpenPmsDental\DevDbTls\ca.pem, or the file OPMS_DB_TLS_CA names, verifies the server on localhost, 127.0.0.1 or ::1,
# port 3306. Without the CA the scripts connect as before, unless OPMS_DB_REQUIRE_TLS=1. Any other server or port is left as it was.
# Dot-source it: . (Join-Path $PSScriptRoot 'DevDbTls.ps1')

function Test-DevDbTlsRequired {
	$value = "$env:OPMS_DB_REQUIRE_TLS".Trim().ToLowerInvariant()
	if ($value -in @('', '0', 'false', 'no')) {
		return $false
	}
	if ($value -in @('1', 'true', 'yes')) {
		return $true
	}
	throw "OPMS_DB_REQUIRE_TLS is '$env:OPMS_DB_REQUIRE_TLS'; use 1 to require TLS to the local test database, or 0."
}

function Test-DevDbTlsServer([string]$Server, [int]$Port) {
	return ($Server.Trim() -in @('localhost', '127.0.0.1', '::1', '[::1]')) -and $Port -eq 3306
}

# The CA file for the local test server, or '' (no CA, or another server). Throws when TLS is required and there is no CA.
function Get-DevDbTlsCa([string]$Server, [int]$Port) {
	if (-not (Test-DevDbTlsServer $Server $Port)) {
		return ''
	}
	if ("$env:OPMS_DB_TLS_CA".Trim() -ne '') {
		$path = "$env:OPMS_DB_TLS_CA".Trim().Trim('"')
		if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
			throw "OPMS_DB_TLS_CA names the CA certificate file $path, which does not exist."
		}
		return (Resolve-Path -LiteralPath $path).ProviderPath
	}
	$default = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OpenPmsDental\DevDbTls\ca.pem'
	if (Test-Path -LiteralPath $default -PathType Leaf) {
		return $default
	}
	if (Test-DevDbTlsRequired) {
		throw "The local test database must be reached with TLS (OPMS_DB_REQUIRE_TLS=1), but its CA certificate is not at $default and OPMS_DB_TLS_CA names no other file. The owner turns on TLS once with tools\Enable-LocalDbTls.ps1 (docs/port-notes/DB-TLS.md)."
	}
	return ''
}

# The TLS options for mariadb.exe and mariadb-dump.exe: --ssl-ca and --ssl-verify-server-cert for the local test server with its CA, else none.
function Get-DevDbTlsClientArguments([string]$Server, [int]$Port) {
	$ca = Get-DevDbTlsCa $Server $Port
	if ($ca -eq '') {
		return @()
	}
	return @("--ssl-ca=$ca", '--ssl-verify-server-cert')
}
