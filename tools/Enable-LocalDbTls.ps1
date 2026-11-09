<#
.SYNOPSIS
Turns on TLS in the local MariaDB service on 127.0.0.1:3306, the development and test database server (O-12).

.DESCRIPTION
Run once, as administrator, on the development PC (docs/port-notes/DB-TLS.md). The script changes nothing before it has shown what it
will change and you have typed YES.

Without a mode switch (enable):
  1. Checks: the session is elevated; the service (-ServiceName, default MariaDB) runs mysqld.exe or mariadbd.exe with
     --defaults-file=<my.ini>; that my.ini says port 3306 (or no port) and bind-address=127.0.0.1; the server that answers on
     127.0.0.1:3306 reports that port, that bind address and that my.ini's datadir, so it is this service; my.ini has no TLS line yet.
  2. Makes, in memory, a test CA (RSA 3072, 10 years) and a server certificate for localhost and 127.0.0.1 (RSA 2048, 5 years,
     serverAuth). Only with -WithClientCertificate also a client certificate for the certificate-login test of S2-2 (RSA 2048, 5 years,
     clientAuth); without it (the default, until the owner has agreed to it) none. The CA's private key is never written anywhere, so
     nothing else can ever be signed with it: a client certificate can only be made in the same run as the CA.
  3. Writes ca.pem, server-cert.pem and server-key.pem to <MariaDB folder>\opms-dev-tls (next to bin and data, not inside data, where
     MariaDB would take the folder for a database), with a protected ACL: SYSTEM and Administrators full control, the service account
     read, nobody else.
  4. Writes the CA certificate (public part only) as ca.pem to -DevFolder (default %LOCALAPPDATA%\OpenPmsDental\DevDbTls), where the dev
     tools and tests look (DataConnectionBase.DevDbTls), with a protected ACL: -DevUser, SYSTEM and Administrators. That folder never
     holds a private key.
  5. With -WithClientCertificate: writes client-cert.pem and client-key.pem to -ClientFolder (default DevDbTlsClient beside -DevFolder,
     %LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient), a folder of its own, never -DevFolder or inside it, with a protected ACL: -DevUser read,
     SYSTEM and Administrators full control. Nothing is written to the repository.
  6. Copies my.ini to my.ini.before-opms-dev-tls and adds, at the end of its [mysqld] section between marker comments, comment lines that
     record -DevFolder (and -ClientFolder) for -Revert and -RequireSecureTransport, then ssl_ca, ssl_cert, ssl_key and
     tls_version=TLSv1.2,TLSv1.3.
  7. Restarts the service and verifies a TLS session (have_ssl=YES, Ssl_cipher not empty) and the server certificate against the CA
     (--ssl-ca with --ssl-verify-server-cert). When the server does not come back with TLS, the backup is restored, the service restarted
     and the new files removed.
  It does NOT set require_secure_transport: unencrypted connections still work until -RequireSecureTransport.

-RequireSecureTransport: the lead's later step, once every session connects with TLS. Checks the CA copy in the dev folder that my.ini
records, adds require_secure_transport=ON inside the script's block, restarts, and verifies that a TLS connection works and an
unencrypted one is refused (else it removes the line again).

-Revert: restores my.ini from the backup (it shows what would be lost when my.ini changed since), restarts, verifies an unencrypted
connection, then removes the opms-dev-tls folder, the files the script wrote to the dev folder and the client folder that my.ini
records (whichever account's they are), and the backup.

-RequireSecureTransport and -Revert never recompute the folders from the account that runs them: they take them from the record in
my.ini and refuse a -DevFolder or -ClientFolder that names another folder. Only when my.ini has no block of this script do they fall back
to -DevFolder or this account's default (the plan then says that a copy in another account's folder is not touched).

-DryRun: with any mode, shows the checks and the plan and changes nothing. It does not need elevation.

-TestCertificatesFolder <folder>: writes only the certificate set of step 2 (the CA certificate, the server certificate and key, and
with -WithClientCertificate the client certificate and key) to an empty folder outside the repository, and nothing else; no elevation,
no service. The tests use it to check the certificates offline (tests/DbConnectionSecurityTests/DevDbTlsScriptTests.cs).

The client is the MariaDB service's own mariadb.exe (or mysql.exe), always on 127.0.0.1:3306 over TCP, as -User (default root); the
password comes from the MYSQL_PWD environment variable when it is set, otherwise none (the dev server's root has a blank password).

Exit codes: 0 done, nothing to do, or a dry run; 1 a check refused or a value is wrong; 2 you did not type YES; 3 the change failed and
was undone; 4 the change failed and could not be undone (the message says what to do).

.PARAMETER RequireSecureTransport
Adds require_secure_transport=ON (the lead's step after every session connects with TLS).

.PARAMETER Revert
Undoes everything the script did: restores the backup of my.ini, restarts, removes the certificate files (in the folders my.ini records).

.PARAMETER DryRun
Shows the checks and the plan of the chosen mode and changes nothing.

.PARAMETER TestCertificatesFolder
Writes a certificate set to this empty folder and does nothing else (for the tests).

.PARAMETER ServiceName
The MariaDB service. Default: MariaDB.

.PARAMETER DevFolder
Where the CA certificate (public part only) goes for the dev tools and tests; never a private key.
Default: %LOCALAPPDATA%\OpenPmsDental\DevDbTls of the account that runs the script. With -RequireSecureTransport and -Revert it must
be the folder my.ini records (leave it out).

.PARAMETER DevUser
The account that runs the dev tools and tests and gets access to -DevFolder (and read access to -ClientFolder). Default: the account that
runs the script.

.PARAMETER User
The database account for the checks. Default: root.

.PARAMETER WithClientCertificate
Also makes the client certificate for S2-2's certificate-login test and writes it with its key to -ClientFolder. Off by default: only
once the owner has agreed to a client certificate. Later it means -Revert and this script again (a new CA), because the CA's key is gone.

.PARAMETER ClientFolder
With -WithClientCertificate: where client-cert.pem and client-key.pem go, a folder of its own (never -DevFolder or inside it).
Default: DevDbTlsClient beside -DevFolder (%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient). With -RequireSecureTransport and -Revert it must
be the folder my.ini records (leave it out).

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1 -DryRun
Shows what the script would change.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1
From a PowerShell started with "Run as administrator": turns on TLS after you type YES.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Enable-LocalDbTls.ps1 -Revert
Undoes it, in the folders the enable run recorded in my.ini.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
	[switch]$RequireSecureTransport,
	[switch]$Revert,
	[switch]$DryRun,
	[string]$TestCertificatesFolder = '',
	[ValidatePattern('^[A-Za-z0-9_.-]{1,64}$')]
	[string]$ServiceName = 'MariaDB',
	[string]$DevFolder = '',
	[string]$DevUser = '',
	[ValidatePattern('^[A-Za-z0-9_.-]{1,64}$')]
	[string]$User = 'root',
	[switch]$WithClientCertificate,
	[string]$ClientFolder = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MarkerBegin = '# BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)'
$MarkerEnd = '# END Open PMS - Dental local test TLS'
# Comment lines inside the block: the folders the enable run wrote to, which -Revert and -RequireSecureTransport read back.
$RecordDevPrefix = '# dev-folder='
$RecordClientPrefix = '# client-folder='
$TlsFolderName = 'opms-dev-tls'
$ClientFolderName = 'DevDbTlsClient'
$BackupSuffix = '.before-opms-dev-tls'
$RepoRoot = Split-Path -Parent $PSScriptRoot

# A check refused: say why and stop with exit code 1. Every check runs before the first change, so nothing needs undoing.
function Stop-Refused([string]$Message) {
	Write-Host "REFUSED: $Message"
	exit 1
}

# A path as PowerShell resolves it (relative to the current location), so .NET file calls and cmdlets agree on it.
function Resolve-FullPath([string]$Path) {
	return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

#region Certificates

function New-SerialNumber {
	$bytes = New-Object byte[] 16
	$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
	try {
		$rng.GetBytes($bytes)
	}
	finally {
		$rng.Dispose()
	}
	$bytes[0] = $bytes[0] -band 0x7F
	if ($bytes[0] -eq 0) {
		$bytes[0] = 1
	}
	return , $bytes
}

function ConvertTo-Pem([byte[]]$Der, [string]$Label) {
	$base64 = [Convert]::ToBase64String($Der)
	$builder = New-Object System.Text.StringBuilder
	[void]$builder.Append("-----BEGIN $Label-----`n")
	for ($i = 0; $i -lt $base64.Length; $i += 64) {
		[void]$builder.Append($base64.Substring($i, [Math]::Min(64, $base64.Length - $i)))
		[void]$builder.Append("`n")
	}
	[void]$builder.Append("-----END $Label-----`n")
	return $builder.ToString()
}

function ConvertFrom-Hex([string]$Hex) {
	$bytes = New-Object byte[] ($Hex.Length / 2)
	for ($i = 0; $i -lt $bytes.Length; $i++) {
		$bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
	}
	return , $bytes
}

function New-Request([string]$Subject, [System.Security.Cryptography.RSA]$Key) {
	return New-Object System.Security.Cryptography.X509Certificates.CertificateRequest($Subject, $Key,
		[System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
}

function Add-LeafExtensions($Request, [string]$EkuOid, [string]$AuthorityKeyId) {
	$Request.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($false, $false, 0, $true)))
	$keyUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment
	$Request.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension($keyUsage, $true)))
	$oids = New-Object System.Security.Cryptography.OidCollection
	[void]$oids.Add((New-Object System.Security.Cryptography.Oid($EkuOid)))
	$Request.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($oids, $false)))
	$Request.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension($Request.PublicKey, $false)))
	# Authority Key Identifier, keyIdentifier only: SEQUENCE { [0] <the CA's subject key identifier> }. .NET Framework has no class for it.
	$keyId = ConvertFrom-Hex $AuthorityKeyId
	$value = New-Object byte[] (4 + $keyId.Length)
	$value[0] = 0x30
	$value[1] = [byte](2 + $keyId.Length)
	$value[2] = 0x80
	$value[3] = [byte]$keyId.Length
	[Array]::Copy($keyId, 0, $value, 4, $keyId.Length)
	$Request.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509Extension('2.5.29.35', $value, $false)))
}

# The certificate set: the CA (its key only in memory), the server certificate for localhost and 127.0.0.1, and optionally the client
# certificate. Returns PEM texts; the keys are disposed before it returns.
function New-CertificateSet([bool]$WithClient) {
	$notBefore = [DateTimeOffset]::UtcNow.AddMinutes(-10)
	$stamp = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
	$caKey = New-Object System.Security.Cryptography.RSACng(3072)
	$serverKey = New-Object System.Security.Cryptography.RSACng(2048)
	$clientKey = $null
	$ca = $null
	try {
		$caRequest = New-Request "CN=Open PMS - Dental local test CA $stamp, O=Open PMS - Dental development" $caKey
		$caRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($true, $true, 0, $true)))
		$caUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::CrlSign
		$caRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension($caUsage, $true)))
		$caKeyId = New-Object System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension($caRequest.PublicKey, $false)
		$caRequest.CertificateExtensions.Add($caKeyId)
		$ca = $caRequest.CreateSelfSigned($notBefore, $notBefore.AddYears(10))

		$serverRequest = New-Request 'CN=localhost, O=Open PMS - Dental local test server' $serverKey
		Add-LeafExtensions $serverRequest '1.3.6.1.5.5.7.3.1' $caKeyId.SubjectKeyIdentifier
		$names = New-Object System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
		$names.AddDnsName('localhost')
		$names.AddIpAddress([System.Net.IPAddress]::Parse('127.0.0.1'))
		$serverRequest.CertificateExtensions.Add($names.Build($false))
		$server = $serverRequest.Create($ca, $notBefore, $notBefore.AddYears(5), (New-SerialNumber))

		$set = [ordered]@{
			CaCert     = ConvertTo-Pem ($ca.RawData) 'CERTIFICATE'
			ServerCert = ConvertTo-Pem ($server.RawData) 'CERTIFICATE'
			ServerKey  = ConvertTo-Pem ($serverKey.Key.Export([System.Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)) 'PRIVATE KEY'
			CaThumbprint     = $ca.Thumbprint
			ServerThumbprint = $server.Thumbprint
			NotAfter         = $server.NotAfter
			ClientCert = $null
			ClientKey  = $null
		}
		if ($WithClient) {
			$clientKey = New-Object System.Security.Cryptography.RSACng(2048)
			$clientRequest = New-Request 'CN=opms-dbtls-test-client, O=Open PMS - Dental local test' $clientKey
			Add-LeafExtensions $clientRequest '1.3.6.1.5.5.7.3.2' $caKeyId.SubjectKeyIdentifier
			$client = $clientRequest.Create($ca, $notBefore, $notBefore.AddYears(5), (New-SerialNumber))
			$set.ClientCert = ConvertTo-Pem ($client.RawData) 'CERTIFICATE'
			$set.ClientKey = ConvertTo-Pem ($clientKey.Key.Export([System.Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)) 'PRIVATE KEY'
		}
		return $set
	}
	finally {
		if ($ca -ne $null) {
			$ca.Reset()
		}
		$caKey.Dispose()
		$serverKey.Dispose()
		if ($clientKey -ne $null) {
			$clientKey.Dispose()
		}
	}
}

#endregion

#region Files and ACLs

function Get-Sid([string]$Account) {
	return (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier])
}

# A protected ACL (nothing inherited): full control for SYSTEM, Administrators and $FullSids, read for $ReadSids; owner Administrators.
function Set-ProtectedAcl([string]$Path, [System.Security.Principal.SecurityIdentifier[]]$FullSids, [System.Security.Principal.SecurityIdentifier[]]$ReadSids) {
	$isFolder = Test-Path -LiteralPath $Path -PathType Container
	if ($isFolder) {
		$acl = New-Object System.Security.AccessControl.DirectorySecurity
		$inherit = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
	}
	else {
		$acl = New-Object System.Security.AccessControl.FileSecurity
		$inherit = [System.Security.AccessControl.InheritanceFlags]::None
	}
	$acl.SetAccessRuleProtection($true, $false)
	$admins = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')
	$system = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-18')
	$acl.SetOwner($admins)
	$full = @($system, $admins) + @($FullSids | Where-Object { $_ -ne $null })
	foreach ($sid in $full) {
		$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($sid, 'FullControl', $inherit, 'None', 'Allow')))
	}
	foreach ($sid in @($ReadSids | Where-Object { $_ -ne $null })) {
		$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($sid, 'ReadAndExecute', $inherit, 'None', 'Allow')))
	}
	Set-Acl -LiteralPath $Path -AclObject $acl
}

function Write-AsciiFile([string]$Path, [string]$Text) {
	[System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.ASCIIEncoding))
}

function Test-InsideRepository([string]$Path) {
	$full = (Resolve-FullPath $Path).TrimEnd('\') + '\'
	$repo = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\') + '\'
	return $full.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)
}

#endregion

#region The service, my.ini and the client

function Get-ServiceFacts {
	$service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$ServiceName'"
	if ($service -eq $null) {
		Stop-Refused "The service '$ServiceName' was not found on this computer. Pass -ServiceName for the local MariaDB service."
	}
	$path = [string]$service.PathName
	$match = [regex]::Match($path, '^\s*"?(?<exe>[^"]+?\.exe)"?(?<args>.*)$')
	if (-not $match.Success) {
		Stop-Refused "The service '$ServiceName' has a command line this script does not read: $path"
	}
	$exe = $match.Groups['exe'].Value
	$exeName = [System.IO.Path]::GetFileName($exe).ToLowerInvariant()
	if ($exeName -ne 'mysqld.exe' -and $exeName -ne 'mariadbd.exe') {
		Stop-Refused "The service '$ServiceName' runs $exe, not a MariaDB server (mysqld.exe or mariadbd.exe)."
	}
	# "--defaults-file=C:\...\my.ini" (the installer's form), --defaults-file="...", or without quotes.
	$defaults = [regex]::Match($match.Groups['args'].Value, '"--defaults-file=(?<f>[^"]+)"|--defaults-file="(?<f>[^"]+)"|--defaults-file=(?<f>[^\s"]+)')
	if (-not $defaults.Success) {
		Stop-Refused "The service '$ServiceName' has no --defaults-file, so this script cannot tell which option file it reads."
	}
	$ini = $defaults.Groups['f'].Value
	if (-not (Test-Path -LiteralPath $ini -PathType Leaf)) {
		Stop-Refused "The option file of the service, $ini, does not exist."
	}
	$bin = Split-Path -Parent $exe
	$client = Join-Path $bin 'mariadb.exe'
	if (-not (Test-Path -LiteralPath $client)) {
		$client = Join-Path $bin 'mysql.exe'
	}
	if (-not (Test-Path -LiteralPath $client)) {
		Stop-Refused "No mariadb.exe or mysql.exe next to $exe."
	}
	return [pscustomobject]@{
		Name      = $service.Name
		State     = [string]$service.State
		Account   = [string]$service.StartName
		Exe       = $exe
		Ini       = $ini
		Bin       = $bin
		Home      = Split-Path -Parent $bin
		Client    = $client
		PluginDir = Join-Path (Split-Path -Parent $bin) 'lib\plugin'
	}
}

# my.ini as lines (each without its line end), its line end and its encoding, so that a write keeps every other byte as it was.
function Read-OptionFile([string]$Path) {
	$bytes = [System.IO.File]::ReadAllBytes($Path)
	$encoding = New-Object System.Text.UTF8Encoding($false)
	$offset = 0
	if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
		$encoding = New-Object System.Text.UTF8Encoding($true)
		$offset = 3
	}
	$text = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
	$crlf = ([regex]::Matches($text, "`r`n")).Count
	$lf = ([regex]::Matches($text, "`n")).Count - $crlf
	$newline = "`n"
	if ($crlf -gt $lf) {
		$newline = "`r`n"
	}
	return [pscustomobject]@{
		Bytes    = $bytes
		Text     = $text
		Encoding = $encoding
		Newline  = $newline
	}
}

# The values of the server's sections ([mysqld], [mariadb], [mariadbd], [server] and their versioned forms), last one wins, keys with
# - and _ alike, lower case.
function Get-ServerOptions([string]$Text) {
	$options = @{}
	$section = ''
	foreach ($rawLine in ($Text -split "`n")) {
		$line = $rawLine.Trim()
		if ($line -eq '' -or $line.StartsWith('#') -or $line.StartsWith(';')) {
			continue
		}
		$header = [regex]::Match($line, '^\[(?<s>[^\]]+)\]')
		if ($header.Success) {
			$section = $header.Groups['s'].Value.Trim().ToLowerInvariant()
			continue
		}
		if ($section -notmatch '^(mysqld|mariadb|mariadbd|server)(-[0-9.]+)?$') {
			continue
		}
		$key = $line
		$value = ''
		$equals = $line.IndexOf('=')
		if ($equals -ge 0) {
			$key = $line.Substring(0, $equals).Trim()
			$value = $line.Substring($equals + 1).Trim().Trim('"').Trim("'")
		}
		$key = $key.ToLowerInvariant().Replace('-', '_')
		$key = $key -replace '^(loose_|enable_)', ''
		$options[$key] = $value
	}
	return $options
}

function Test-HasBlock([string]$Text) {
	return $Text.Contains($MarkerBegin) -and $Text.Contains($MarkerEnd)
}

# my.ini without the script's block (the lines from the begin to the end marker).
function Remove-Block([string]$Text) {
	$pattern = '(?m)^' + [regex]::Escape($MarkerBegin) + '[^\n]*\n(?:.*\n)*?' + [regex]::Escape($MarkerEnd) + '[^\n]*(\n|$)'
	return [regex]::Replace($Text, $pattern, '')
}

# Inserts lines at the end of the [mysqld] section (before the next section header, without the blank lines before it).
function Add-ToMysqldSection([string]$Text, [string[]]$Lines, [string]$Newline) {
	$list = New-Object System.Collections.Generic.List[string]
	foreach ($line in ($Text -split "`n")) {
		$list.Add($line)
	}
	$start = -1
	for ($i = 0; $i -lt $list.Count; $i++) {
		if ($list[$i].Trim().ToLowerInvariant() -match '^\[mysqld\]') {
			$start = $i
			break
		}
	}
	if ($start -lt 0) {
		Stop-Refused 'my.ini has no [mysqld] section.'
	}
	$end = $list.Count
	for ($i = $start + 1; $i -lt $list.Count; $i++) {
		if ($list[$i].Trim().StartsWith('[')) {
			$end = $i
			break
		}
	}
	$insertAt = $end
	while ($insertAt -gt $start + 1 -and $list[$insertAt - 1].Trim() -eq '') {
		$insertAt--
	}
	$lineEnd = ''
	if ($Newline -eq "`r`n") {
		$lineEnd = "`r"
	}
	$insert = @($Lines | ForEach-Object { $_ + $lineEnd })
	# The last line of a file without a final line end gets one, so the block starts on its own line.
	if ($insertAt -eq $list.Count -and $list.Count -gt 0 -and $list[$list.Count - 1] -ne '') {
		$list.Add('')
		$insertAt = $list.Count - 1
	}
	$list.InsertRange($insertAt, [string[]]$insert)
	return ($list -join "`n")
}

function Write-OptionFile($File, [string]$Text) {
	$bytes = $File.Encoding.GetPreamble() + $File.Encoding.GetBytes($Text)
	$temp = "$($script:Facts.Ini).opms-new"
	[System.IO.File]::WriteAllBytes($temp, [byte[]]$bytes)
	Move-Item -LiteralPath $temp -Destination $script:Facts.Ini -Force
}

function Join-Arguments([string[]]$Arguments) {
	$quoted = foreach ($argument in $Arguments) {
		if ($argument -match '[\s"]') {
			'"' + $argument.Replace('"', '\"') + '"'
		}
		else {
			$argument
		}
	}
	return ($quoted -join ' ')
}

# Runs one statement list with the service's client on 127.0.0.1:3306 over TCP. $Tls: 'plain' (no TLS), 'tls' (TLS without
# verification) or the path of a CA file (TLS, the server verified against it, host name included).
function Invoke-DbClient([string]$Sql, [string]$Tls = 'tls') {
	$arguments = @('--no-defaults', "--plugin-dir=$($script:Facts.PluginDir)", '--protocol=TCP', '--host=127.0.0.1', '--port=3306',
		"--user=$User", '--connect-timeout=10', '--batch', '--raw', '--skip-column-names')
	if ($Tls -eq 'plain') {
		$arguments += '--skip-ssl'
	}
	elseif ($Tls -eq 'tls') {
		$arguments += '--ssl'
	}
	else {
		$arguments += @("--ssl-ca=$Tls", '--ssl-verify-server-cert')
	}
	$arguments += @('-e', $Sql)
	$startInfo = New-Object System.Diagnostics.ProcessStartInfo
	$startInfo.FileName = $script:Facts.Client
	$startInfo.Arguments = Join-Arguments $arguments
	$startInfo.UseShellExecute = $false
	$startInfo.RedirectStandardOutput = $true
	$startInfo.RedirectStandardError = $true
	$startInfo.CreateNoWindow = $true
	$process = [System.Diagnostics.Process]::Start($startInfo)
	$stdoutTask = $process.StandardOutput.ReadToEndAsync()
	$stderr = $process.StandardError.ReadToEnd()
	$process.WaitForExit()
	return [pscustomobject]@{
		ExitCode = $process.ExitCode
		Lines    = @($stdoutTask.Result -split "`r?`n" | Where-Object { $_ -ne '' })
		Error    = $stderr.Trim()
	}
}

# Name/value lines of a SELECT/SHOW list into a hashtable.
function Get-ServerState([string]$Tls) {
	$sql = "SELECT 'port',@@port UNION ALL SELECT 'bind_address',@@bind_address UNION ALL SELECT 'datadir',@@datadir " +
		"UNION ALL SELECT 'version',@@version UNION ALL SELECT 'have_ssl',@@have_ssl " +
		"UNION ALL SELECT 'require_secure_transport',@@require_secure_transport UNION ALL SELECT 'tls_version',@@tls_version " +
		"UNION ALL SELECT 'ssl_cert',@@ssl_cert; " +
		"SHOW SESSION STATUS WHERE Variable_name IN ('Ssl_cipher','Ssl_version','Ssl_server_not_after')"
	$result = Invoke-DbClient $sql $Tls
	if ($result.ExitCode -ne 0) {
		return [pscustomobject]@{ Ok = $false; Error = $result.Error; Values = @{} }
	}
	$values = @{}
	foreach ($line in $result.Lines) {
		$parts = $line -split "`t", 2
		if ($parts.Count -eq 2) {
			$values[$parts[0].ToLowerInvariant()] = $parts[1]
		}
		else {
			$values[$parts[0].ToLowerInvariant()] = ''
		}
	}
	foreach ($key in @('ssl_cipher', 'ssl_version', 'ssl_server_not_after')) {
		if (-not $values.ContainsKey($key)) {
			$values[$key] = ''
		}
	}
	return [pscustomobject]@{ Ok = $true; Error = ''; Values = $values }
}

function Get-NormalPath([string]$Path) {
	return $Path.Replace('/', '\').TrimEnd('\').ToLowerInvariant()
}

function Restart-DbService {
	Write-Host "Restarting the service $ServiceName ..."
	Restart-Service -Name $ServiceName -Force
	$deadline = [DateTime]::UtcNow.AddSeconds(90)
	while ([DateTime]::UtcNow -lt $deadline) {
		$client = New-Object System.Net.Sockets.TcpClient
		try {
			$client.Connect('127.0.0.1', 3306)
			return
		}
		catch {
			Start-Sleep -Milliseconds 500
		}
		finally {
			$client.Close()
		}
	}
	throw "The service $ServiceName did not answer on 127.0.0.1:3306 within 90 seconds after the restart."
}

function Confirm-Change([string]$Question) {
	if ($DryRun) {
		Write-Host ''
		Write-Host 'Dry run: nothing was changed.'
		exit 0
	}
	Write-Host ''
	$answer = ''
	try {
		$answer = Read-Host "$Question Type YES to continue"
	}
	catch {
		$answer = ''
	}
	if ($answer -cne 'YES') {
		Write-Host 'Nothing was changed.'
		exit 2
	}
}

function Test-Elevated {
	$principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
	return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# The checks every mode shares: the service, its my.ini, and the server on 127.0.0.1:3306 being that service.
function Assert-LocalServer {
	$script:Facts = Get-ServiceFacts
	Write-Host "Service:        $($script:Facts.Name) ($($script:Facts.State)), account $($script:Facts.Account)"
	Write-Host "Server program: $($script:Facts.Exe)"
	Write-Host "Option file:    $($script:Facts.Ini)"
	if ($script:Facts.State -ne 'Running') {
		Stop-Refused "The service $ServiceName is $($script:Facts.State). Start it first."
	}
	$script:IniFile = Read-OptionFile $script:Facts.Ini
	$options = Get-ServerOptions $script:IniFile.Text
	$port = '3306'
	if ($options.ContainsKey('port')) {
		$port = $options['port']
	}
	if ($port -ne '3306') {
		Stop-Refused "my.ini sets port=$port. This script is only for the local server on 127.0.0.1:3306."
	}
	if (-not $options.ContainsKey('bind_address') -or $options['bind_address'] -ne '127.0.0.1') {
		$bind = '(not set: every address)'
		if ($options.ContainsKey('bind_address')) {
			$bind = $options['bind_address']
		}
		Stop-Refused "my.ini's bind-address is $bind. This script is only for a server that listens on 127.0.0.1 alone."
	}
	$state = Get-ServerState 'tls'
	if (-not $state.Ok) {
		Stop-Refused "No connection to 127.0.0.1:3306 as $User ($($state.Error)). Set MYSQL_PWD when the account has a password."
	}
	$values = $state.Values
	if ($values['port'] -ne '3306' -or $values['bind_address'] -ne '127.0.0.1') {
		Stop-Refused "The server on 127.0.0.1:3306 reports port $($values['port']) and bind address $($values['bind_address'])."
	}
	if ($options.ContainsKey('datadir') -and (Get-NormalPath $options['datadir']) -ne (Get-NormalPath $values['datadir'])) {
		Stop-Refused "The server on 127.0.0.1:3306 uses the data folder $($values['datadir']), but $($script:Facts.Ini) names $($options['datadir']): it is not the service $ServiceName."
	}
	Write-Host "Server:         $($values['version']) on 127.0.0.1:3306, data folder $($values['datadir'])"
	Write-Host "TLS now:        have_ssl=$($values['have_ssl']), require_secure_transport=$($values['require_secure_transport']), tls_version=$($values['tls_version']), this session's cipher '$($values['ssl_cipher'])'"
	return $state
}

#endregion

#region Modes

# The default client folder: DevDbTlsClient beside the dev folder (%LOCALAPPDATA%\OpenPmsDental\DevDbTlsClient for the default).
function Get-DefaultClientFolder([string]$DevPath) {
	$parent = Split-Path -Parent $DevPath
	if ([string]::IsNullOrEmpty($parent)) {
		Stop-Refused "-DevFolder $DevPath has no parent folder for the client folder. Pass -ClientFolder."
	}
	return Join-Path $parent $ClientFolderName
}

# True when the two folders are the same or one is inside the other.
function Test-FolderOverlap([string]$First, [string]$Second) {
	$a = (Get-NormalPath $First) + '\'
	$b = (Get-NormalPath $Second) + '\'
	return $a.StartsWith($b) -or $b.StartsWith($a)
}

# The folders that the block of this script in my.ini records (the comment lines $RecordDevPrefix and $RecordClientPrefix), each $null when
# the block does not record it or there is no block.
function Get-RecordedFolders([string]$Text) {
	$recorded = [pscustomobject]@{ DevFolder = $null; ClientFolder = $null }
	$block = [regex]::Match($Text, '(?ms)^' + [regex]::Escape($MarkerBegin) + '.*?^' + [regex]::Escape($MarkerEnd))
	if (-not $block.Success) {
		return $recorded
	}
	foreach ($rawLine in ($block.Value -split "`n")) {
		$line = $rawLine.Trim()
		if ($line.StartsWith($RecordDevPrefix) -and $line.Length -gt $RecordDevPrefix.Length) {
			$recorded.DevFolder = $line.Substring($RecordDevPrefix.Length).Trim().Replace('/', '\')
		}
		elseif ($line.StartsWith($RecordClientPrefix) -and $line.Length -gt $RecordClientPrefix.Length) {
			$recorded.ClientFolder = $line.Substring($RecordClientPrefix.Length).Trim().Replace('/', '\')
		}
	}
	return $recorded
}

# The dev folder and the client folder ('' for none) of a -Revert or -RequireSecureTransport run. The enable run recorded in my.ini where it
# wrote them, for whichever account (-DevUser, -DevFolder); they are taken from there, never recomputed from the account that runs this, and
# a -DevFolder or -ClientFolder that names another folder is refused. Only without a record (no block, or one without the line) the folder
# is -DevFolder, else $DefaultDev, and the client folder -ClientFolder, else none; IsRecorded then says so.
function Resolve-RecordedFolders($Recorded, [string]$GivenDev, [string]$GivenClient, [string]$DefaultDev) {
	if ($null -eq $Recorded.DevFolder) {
		$dev = $GivenDev
		if ($dev -eq '') {
			$dev = $DefaultDev
		}
		$client = ''
		if ($GivenClient -ne '') {
			$client = Resolve-FullPath $GivenClient
		}
		return [pscustomobject]@{ DevFolder = (Resolve-FullPath $dev); ClientFolder = $client; IsRecorded = $false }
	}
	if ($GivenDev -ne '' -and (Get-NormalPath (Resolve-FullPath $GivenDev)) -ne (Get-NormalPath $Recorded.DevFolder)) {
		Stop-Refused "my.ini records the dev folder $($Recorded.DevFolder), where the enable run wrote ca.pem, but -DevFolder names $GivenDev. Leave -DevFolder out: this mode uses the recorded folder."
	}
	$client = ''
	if ($null -ne $Recorded.ClientFolder) {
		$client = $Recorded.ClientFolder
	}
	if ($GivenClient -ne '' -and (Get-NormalPath (Resolve-FullPath $GivenClient)) -ne (Get-NormalPath $client)) {
		if ($client -eq '') {
			Stop-Refused "my.ini records no client folder (the enable run wrote no client certificate), but -ClientFolder names $GivenClient. Leave -ClientFolder out."
		}
		Stop-Refused "my.ini records the client folder $client, but -ClientFolder names $GivenClient. Leave -ClientFolder out: this mode uses the recorded folder."
	}
	return [pscustomobject]@{ DevFolder = $Recorded.DevFolder; ClientFolder = $client; IsRecorded = $true }
}

# The paths of a run. The enable run takes -DevFolder and -ClientFolder (or their defaults); the other modes take what my.ini records.
function Get-Paths([bool]$ForEnable) {
	$tlsFolder = Join-Path $script:Facts.Home $TlsFolderName
	$defaultDev = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OpenPmsDental\DevDbTls'
	if ($ForEnable) {
		$dev = $DevFolder
		if ($dev -eq '') {
			$dev = $defaultDev
		}
		$dev = Resolve-FullPath $dev
		$client = ''
		if ($WithClientCertificate) {
			$client = $ClientFolder
			if ($client -eq '') {
				$client = Get-DefaultClientFolder $dev
			}
			$client = Resolve-FullPath $client
		}
		$folders = [pscustomobject]@{ DevFolder = $dev; ClientFolder = $client; IsRecorded = $false }
	}
	else {
		$folders = Resolve-RecordedFolders (Get-RecordedFolders $script:IniFile.Text) $DevFolder $ClientFolder $defaultDev
	}
	$clientCert = ''
	$clientKey = ''
	if ($folders.ClientFolder -ne '') {
		$clientCert = Join-Path $folders.ClientFolder 'client-cert.pem'
		$clientKey = Join-Path $folders.ClientFolder 'client-key.pem'
	}
	return [pscustomobject]@{
		TlsFolder    = $tlsFolder
		Ca           = Join-Path $tlsFolder 'ca.pem'
		ServerCert   = Join-Path $tlsFolder 'server-cert.pem'
		ServerKey    = Join-Path $tlsFolder 'server-key.pem'
		DevFolder    = $folders.DevFolder
		DevCa        = Join-Path $folders.DevFolder 'ca.pem'
		ClientFolder = $folders.ClientFolder
		ClientCert   = $clientCert
		ClientKey    = $clientKey
		IsRecorded   = $folders.IsRecorded
		Backup       = $script:Facts.Ini + $BackupSuffix
	}
}

# The block of this script in my.ini: the record of the dev folder (and the client folder) as comment lines, then the TLS options.
function Get-BlockLines($Paths, [bool]$WithRequire) {
	$lines = @(
		$MarkerBegin,
		"$RecordDevPrefix$($Paths.DevFolder.Replace('\', '/'))"
	)
	if ($Paths.ClientFolder -ne '') {
		$lines += "$RecordClientPrefix$($Paths.ClientFolder.Replace('\', '/'))"
	}
	$lines += @(
		"ssl_ca=$($Paths.Ca.Replace('\', '/'))",
		"ssl_cert=$($Paths.ServerCert.Replace('\', '/'))",
		"ssl_key=$($Paths.ServerKey.Replace('\', '/'))",
		'tls_version=TLSv1.2,TLSv1.3'
	)
	if ($WithRequire) {
		$lines += 'require_secure_transport=ON'
	}
	$lines += $MarkerEnd
	return $lines
}

# The files the script writes outside the opms-dev-tls folder, as $Paths names them.
function Get-DevFiles($Paths) {
	return @(@($Paths.DevCa, $Paths.ClientCert, $Paths.ClientKey) | Where-Object { $_ -ne '' })
}

function Remove-NewFiles($Paths) {
	foreach ($path in (Get-DevFiles $Paths)) {
		if (Test-Path -LiteralPath $path) {
			Remove-Item -LiteralPath $path -Force
		}
	}
	foreach ($folder in @(@($Paths.DevFolder, $Paths.ClientFolder) | Where-Object { $_ -ne '' })) {
		if ((Test-Path -LiteralPath $folder) -and @(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) {
			Remove-Item -LiteralPath $folder -Force
		}
	}
	if (Test-Path -LiteralPath $Paths.TlsFolder) {
		Remove-Item -LiteralPath $Paths.TlsFolder -Recurse -Force
	}
}

function Invoke-Enable {
	$state = Assert-LocalServer
	$paths = Get-Paths $true
	$text = $script:IniFile.Text
	if (Test-HasBlock $text) {
		Write-Host ''
		Write-Host "TLS is already on through this script ($($script:Facts.Ini) has its block). Nothing to do."
		Write-Host 'To make new certificates, run -Revert first and then this script again.'
		exit 0
	}
	$options = Get-ServerOptions $text
	foreach ($key in @('ssl', 'ssl_ca', 'ssl_capath', 'ssl_cert', 'ssl_key', 'ssl_cipher', 'tls_version', 'require_secure_transport')) {
		if ($options.ContainsKey($key)) {
			Stop-Refused "my.ini already sets $key. This script only turns on TLS where none is configured; leave it as it is or remove the line first."
		}
	}
	if ($state.Values['have_ssl'] -eq 'YES') {
		Stop-Refused 'The server already has TLS (have_ssl=YES) from somewhere this script does not manage.'
	}
	if (Test-Path -LiteralPath $paths.Backup) {
		Stop-Refused "$($paths.Backup) exists from an earlier run. Run -Revert, or look at it and remove it."
	}
	if (Test-Path -LiteralPath $paths.TlsFolder) {
		Stop-Refused "$($paths.TlsFolder) exists. Run -Revert, or look at it and remove it."
	}
	if (Test-InsideRepository $paths.DevFolder) {
		Stop-Refused "-DevFolder $($paths.DevFolder) is inside the repository; certificates and keys never go there."
	}
	if ($paths.ClientFolder -ne '') {
		if (Test-InsideRepository $paths.ClientFolder) {
			Stop-Refused "-ClientFolder $($paths.ClientFolder) is inside the repository; certificates and keys never go there."
		}
		if (Test-FolderOverlap $paths.ClientFolder $paths.DevFolder) {
			Stop-Refused "-ClientFolder $($paths.ClientFolder) and -DevFolder $($paths.DevFolder) overlap. The client key goes to a folder of its own, never where the dev tools read the CA."
		}
	}
	$serviceSid = $null
	if ($script:Facts.Account -ne '' -and $script:Facts.Account -ne 'LocalSystem') {
		$serviceSid = Get-Sid $script:Facts.Account
	}
	$devUserName = $DevUser
	if ($devUserName -eq '') {
		$devUserName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
	}
	$devSid = Get-Sid $devUserName
	$consoleUser = [string](Get-CimInstance -ClassName Win32_ComputerSystem).UserName
	$block = Get-BlockLines $paths $false
	$newText = Add-ToMysqldSection $text $block $script:IniFile.Newline

	$withClient = $paths.ClientFolder -ne ''

	Write-Host ''
	Write-Host 'Plan:'
	$step = 1
	Write-Host "  $step. Make a test CA (its private key stays in memory and is discarded) and a server certificate for localhost and 127.0.0.1."
	if ($withClient) {
		Write-Host '     With -WithClientCertificate also a client certificate (clientAuth) for the certificate-login test of S2-2.'
	}
	else {
		Write-Host '     No client certificate (-WithClientCertificate is not given): S2-2''s certificate login waits for the owner''s yes. It can only be'
		Write-Host '     made in the same run as the CA, so it then means -Revert and this script again with -WithClientCertificate.'
	}
	$step++
	Write-Host "  $step. Create $($paths.TlsFolder) (owner Administrators; SYSTEM and Administrators full control, $($script:Facts.Account) read) with"
	Write-Host '     ca.pem, server-cert.pem and server-key.pem.'
	$step++
	Write-Host "  $step. Write the CA certificate (public part only) to $($paths.DevCa),"
	Write-Host "     the folder readable by $devUserName, SYSTEM and Administrators only. No private key goes to this folder."
	if ($withClient) {
		$step++
		Write-Host "  $step. Write the client certificate and key to $($paths.ClientCert) and $($paths.ClientKey):"
		Write-Host "     a folder of its own, not the CA's, $devUserName read only, SYSTEM and Administrators full control."
	}
	foreach ($existing in (Get-DevFiles $paths)) {
		if (Test-Path -LiteralPath $existing) {
			Write-Host "     $existing exists and is replaced."
		}
	}
	if ($consoleUser -ne '' -and $consoleUser -ne $devUserName) {
		Write-Host "     NOTE: $consoleUser is signed in, but the files go to the folders of $devUserName. The dev tools and tests of"
		Write-Host "     $consoleUser look in their own %LOCALAPPDATA%: pass -DevUser and -DevFolder for that account, or set OPMS_DB_TLS_CA."
	}
	$step++
	Write-Host "  $step. Copy $($script:Facts.Ini) to $($paths.Backup), then add at the end of its [mysqld] section (the comment lines"
	Write-Host '     record the folders above; -Revert and -RequireSecureTransport read them from there):'
	foreach ($line in $block) {
		Write-Host "       $line"
	}
	$step++
	Write-Host "  $step. Restart the service $ServiceName (connections drop for a few seconds) and check a TLS session and the server"
	Write-Host '     certificate against the CA. If the server does not come back with TLS, the backup is restored and the files removed.'
	Write-Host '  require_secure_transport is not set: unencrypted connections keep working until -RequireSecureTransport.'
	Confirm-Change 'Turn on TLS in the local MariaDB service?'

	$set = New-CertificateSet $withClient
	$changedIni = $false
	try {
		New-Item -ItemType Directory -Path $paths.TlsFolder | Out-Null
		Set-ProtectedAcl $paths.TlsFolder @() @($serviceSid)
		Write-AsciiFile $paths.Ca $set.CaCert
		Write-AsciiFile $paths.ServerCert $set.ServerCert
		Write-AsciiFile $paths.ServerKey $set.ServerKey
		foreach ($file in @($paths.Ca, $paths.ServerCert, $paths.ServerKey)) {
			Set-ProtectedAcl $file @() @($serviceSid)
		}
		if (-not (Test-Path -LiteralPath $paths.DevFolder)) {
			New-Item -ItemType Directory -Path $paths.DevFolder | Out-Null
		}
		Set-ProtectedAcl $paths.DevFolder @($devSid) @()
		Write-AsciiFile $paths.DevCa $set.CaCert
		$written = "$($paths.TlsFolder), $($paths.DevCa)"
		if ($withClient) {
			if (-not (Test-Path -LiteralPath $paths.ClientFolder)) {
				New-Item -ItemType Directory -Path $paths.ClientFolder | Out-Null
			}
			Set-ProtectedAcl $paths.ClientFolder @() @($devSid)
			Write-AsciiFile $paths.ClientCert $set.ClientCert
			Write-AsciiFile $paths.ClientKey $set.ClientKey
			foreach ($file in @($paths.ClientCert, $paths.ClientKey)) {
				Set-ProtectedAcl $file @() @($devSid)
			}
			$written += ", $($paths.ClientFolder)"
		}
		Copy-Item -LiteralPath $script:Facts.Ini -Destination $paths.Backup
		$changedIni = $true
		Write-OptionFile $script:IniFile $newText
		Write-Host "Wrote $written and the TLS lines of $($script:Facts.Ini)."
		Restart-DbService
		$after = Get-ServerState 'tls'
		if (-not $after.Ok -or $after.Values['have_ssl'] -ne 'YES' -or $after.Values['ssl_cipher'] -eq '') {
			$why = $after.Error
			if ($after.Ok) {
				$why = "have_ssl=$($after.Values['have_ssl']), cipher '$($after.Values['ssl_cipher'])'"
			}
			throw "The server did not come back with TLS ($why). The MariaDB error log in the data folder says why."
		}
	}
	catch {
		$failure = $_.Exception.Message
		Write-Host "FAILED: $failure"
		try {
			if ($changedIni) {
				Copy-Item -LiteralPath $paths.Backup -Destination $script:Facts.Ini -Force
				Restart-DbService
				Remove-Item -LiteralPath $paths.Backup -Force
			}
			Remove-NewFiles $paths
			Write-Host 'Undone: my.ini restored from the backup, the service restarted, the new files removed.'
			exit 3
		}
		catch {
			Write-Host "Undoing failed too: $($_.Exception.Message)"
			Write-Host "Restore $($script:Facts.Ini) from $($paths.Backup) by hand, restart the service $ServiceName, and remove $($paths.TlsFolder) and $((Get-DevFiles $paths) -join ', ')."
			exit 4
		}
	}
	Write-Host ''
	Write-Host "TLS is on: $($after.Values['ssl_version']), cipher $($after.Values['ssl_cipher']); the server certificate is valid until $($after.Values['ssl_server_not_after'])."
	$verified = Get-ServerState $paths.DevCa
	if ($verified.Ok -and $verified.Values['ssl_cipher'] -ne '') {
		Write-Host "The server certificate was verified against $($paths.DevCa) (chain and host name 127.0.0.1)."
	}
	else {
		Write-Host "WARNING: TLS works, but the client could not verify the server against $($paths.DevCa): $($verified.Error)"
		Write-Host '         Report this to the lead; the tests (DevDbTlsServerTests) check the verification with the program''s own connector.'
	}
	Write-Host ''
	Write-Host 'Next: the lead runs the TLS tests, tells the other sessions, and later runs this script with -RequireSecureTransport.'
	exit 0
}

function Invoke-RequireSecureTransport {
	$state = Assert-LocalServer
	$text = $script:IniFile.Text
	if (-not (Test-HasBlock $text)) {
		Stop-Refused "my.ini has no block of this script: turn on TLS first (run the script without -RequireSecureTransport)."
	}
	$paths = Get-Paths $false
	if ($state.Values['require_secure_transport'] -eq '1' -and $text.Contains('require_secure_transport=ON')) {
		Write-Host ''
		Write-Host 'require_secure_transport is already ON. Nothing to do.'
		exit 0
	}
	if (-not (Test-Path -LiteralPath $paths.DevCa)) {
		if ($paths.IsRecorded) {
			Stop-Refused "$($paths.DevCa), the CA copy the enable run recorded in my.ini, is missing; the dev tools and tests would have no CA. Run -Revert and turn TLS on again."
		}
		Stop-Refused "$($paths.DevCa) is missing (my.ini records no dev folder); the dev tools and tests would have no CA. Pass -DevFolder, or run -Revert and turn TLS on again."
	}
	$verified = Get-ServerState $paths.DevCa
	if (-not $verified.Ok -or $verified.Values['ssl_cipher'] -eq '') {
		Stop-Refused "A TLS connection verified against $($paths.DevCa) does not work ($($verified.Error)); fix that first."
	}
	$block = Get-BlockLines $paths $true
	$newText = Add-ToMysqldSection (Remove-Block $text) $block $script:IniFile.Newline
	Write-Host ''
	if ($paths.IsRecorded) {
		Write-Host "The CA copy of the dev tools and tests, as my.ini records it: $($paths.DevCa) (verified against the server)."
	}
	else {
		Write-Host "my.ini records no dev folder; the CA copy checked is $($paths.DevCa) (verified against the server)."
	}
	Write-Host 'Plan:'
	Write-Host "  1. Replace the block of this script in $($script:Facts.Ini) with:"
	foreach ($line in $block) {
		Write-Host "       $line"
	}
	Write-Host "  2. Restart the service $ServiceName, then check that a TLS connection works and an unencrypted one is refused."
	Write-Host '     If not, the line is removed again and the service restarted.'
	Write-Host '  From then on every connection without TLS is refused: tell the other sessions first, and make sure their worktrees'
	Write-Host '  have the DevDbTls change (docs/port-notes/DB-TLS.md).'
	Confirm-Change 'Require TLS for every connection to the local MariaDB?'

	try {
		Write-OptionFile $script:IniFile $newText
		Restart-DbService
		$after = Get-ServerState $paths.DevCa
		if (-not $after.Ok -or $after.Values['require_secure_transport'] -ne '1' -or $after.Values['ssl_cipher'] -eq '') {
			throw "After the restart the TLS check failed ($($after.Error); require_secure_transport=$($after.Values['require_secure_transport']))."
		}
		$plain = Get-ServerState 'plain'
		if ($plain.Ok) {
			throw 'An unencrypted connection was still accepted.'
		}
	}
	catch {
		Write-Host "FAILED: $($_.Exception.Message)"
		try {
			Write-OptionFile $script:IniFile $text
			Restart-DbService
			Write-Host 'Undone: the block is back as it was (TLS on, require_secure_transport not set) and the service restarted.'
			exit 3
		}
		catch {
			Write-Host "Undoing failed too: $($_.Exception.Message). Remove the line require_secure_transport=ON from $($script:Facts.Ini) and restart the service."
			exit 4
		}
	}
	Write-Host ''
	Write-Host "require_secure_transport is ON: TLS connections work ($($after.Values['ssl_version'])), an unencrypted one was refused:"
	Write-Host "  $($plain.Error)"
	exit 0
}

function Invoke-Revert {
	[void](Assert-LocalServer)
	$paths = Get-Paths $false
	$text = $script:IniFile.Text
	if (-not (Test-Path -LiteralPath $paths.Backup)) {
		if (-not (Test-HasBlock $text) -and -not (Test-Path -LiteralPath $paths.TlsFolder)) {
			Write-Host ''
			Write-Host 'Nothing to revert: no backup, no block of this script in my.ini, no certificate folder.'
			exit 0
		}
		Stop-Refused "$($paths.Backup) is missing, so the original my.ini cannot be restored. Remove the block between the marker lines from $($script:Facts.Ini) by hand."
	}
	$backup = Read-OptionFile $paths.Backup
	$lost = @()
	if ((Remove-Block $text) -ne $backup.Text) {
		$current = @((Remove-Block $text) -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
		$old = @($backup.Text -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
		$lost = @(Compare-Object -ReferenceObject $old -DifferenceObject $current | Where-Object { $_.SideIndicator -eq '=>' } |
			ForEach-Object { $_.InputObject })
	}
	Write-Host ''
	Write-Host 'Plan:'
	Write-Host "  1. Restore $($script:Facts.Ini) from $($paths.Backup)."
	if ($lost.Count -gt 0) {
		Write-Host '     my.ini was changed since TLS was turned on; restoring the backup also drops these lines:'
		foreach ($line in $lost) {
			Write-Host "       $line"
		}
	}
	Write-Host "  2. Restart the service $ServiceName and check an unencrypted connection."
	Write-Host "  3. Remove $($paths.TlsFolder), $((Get-DevFiles $paths) -join ', ') and the backup."
	if ($paths.IsRecorded) {
		Write-Host '     These are the folders the enable run recorded in my.ini (whichever account they belong to).'
	}
	else {
		Write-Host "     my.ini records no dev folder, so only $($paths.DevCa) is removed (-DevFolder, else this account's %LOCALAPPDATA%)."
		Write-Host '     A ca.pem, client-cert.pem or client-key.pem in another account''s folder is not touched: pass -DevFolder (and'
		Write-Host '     -ClientFolder) for it, or remove it by hand, or the dev tools there keep asking the server for TLS.'
	}
	Confirm-Change 'Turn TLS off again?'
	try {
		Copy-Item -LiteralPath $paths.Backup -Destination $script:Facts.Ini -Force
		Restart-DbService
		$plain = Get-ServerState 'plain'
		if (-not $plain.Ok) {
			throw "An unencrypted connection does not work after the restore: $($plain.Error)"
		}
		Remove-NewFiles $paths
		Remove-Item -LiteralPath $paths.Backup -Force
	}
	catch {
		Write-Host "FAILED: $($_.Exception.Message)"
		Write-Host "The backup $($paths.Backup) is kept. Look at $($script:Facts.Ini) and the MariaDB error log, then restart the service."
		exit 4
	}
	Write-Host ''
	Write-Host 'TLS is off again: my.ini restored, the service restarted, the certificate files removed.'
	exit 0
}

function Invoke-TestCertificates {
	$TestCertificatesFolder = Resolve-FullPath $TestCertificatesFolder
	if (Test-InsideRepository $TestCertificatesFolder) {
		Stop-Refused "$TestCertificatesFolder is inside the repository; certificates and keys never go there."
	}
	if (Test-Path -LiteralPath $TestCertificatesFolder) {
		if (@(Get-ChildItem -LiteralPath $TestCertificatesFolder -Force).Count -gt 0) {
			Stop-Refused "$TestCertificatesFolder is not empty."
		}
	}
	else {
		New-Item -ItemType Directory -Path $TestCertificatesFolder | Out-Null
	}
	$set = New-CertificateSet $WithClientCertificate.IsPresent
	Write-AsciiFile (Join-Path $TestCertificatesFolder 'ca.pem') $set.CaCert
	Write-AsciiFile (Join-Path $TestCertificatesFolder 'server-cert.pem') $set.ServerCert
	Write-AsciiFile (Join-Path $TestCertificatesFolder 'server-key.pem') $set.ServerKey
	if ($WithClientCertificate) {
		Write-AsciiFile (Join-Path $TestCertificatesFolder 'client-cert.pem') $set.ClientCert
		Write-AsciiFile (Join-Path $TestCertificatesFolder 'client-key.pem') $set.ClientKey
	}
	Write-Host "Wrote a test certificate set to $TestCertificatesFolder (CA $($set.CaThumbprint), server $($set.ServerThumbprint))."
	exit 0
}

#endregion

try {
	$modes = @($RequireSecureTransport.IsPresent, $Revert.IsPresent, ($TestCertificatesFolder -ne '')) | Where-Object { $_ }
	if (@($modes).Count -gt 1) {
		Stop-Refused 'Use one of -RequireSecureTransport, -Revert and -TestCertificatesFolder at a time.'
	}
	if ($WithClientCertificate -and ($RequireSecureTransport -or $Revert)) {
		Stop-Refused '-WithClientCertificate goes with the enable run (or -TestCertificatesFolder) only: the client certificate is made with the CA.'
	}
	if ($ClientFolder -ne '' -and ($TestCertificatesFolder -ne '' -or (-not $WithClientCertificate -and -not $RequireSecureTransport -and -not $Revert))) {
		Stop-Refused '-ClientFolder needs -WithClientCertificate (the enable run), or names the recorded client folder for -Revert or -RequireSecureTransport.'
	}
	if ($TestCertificatesFolder -ne '') {
		Invoke-TestCertificates
	}
	if (-not (Test-Elevated)) {
		if (-not $DryRun) {
			Stop-Refused 'This script changes the MariaDB service and must run elevated: start PowerShell with "Run as administrator" (or use -DryRun to see the plan).'
		}
		Write-Host 'Not elevated: a dry run only. The real run must be started as administrator.'
	}
	if ($RequireSecureTransport) {
		Invoke-RequireSecureTransport
	}
	elseif ($Revert) {
		Invoke-Revert
	}
	else {
		Invoke-Enable
	}
}
catch {
	Write-Host "FAILED: $($_.Exception.Message)"
	exit 1
}
