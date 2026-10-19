<#
.SYNOPSIS
	Fails if live code still references Open Dental Software, Inc. hosts.

.DESCRIPTION
	Scans the C# (.cs), XAML (.xaml) and resource (.resx) files of every project in OpenDental.Local.slnf, except the unit test projects,
	for these hosts: opendental.com, open-dent.com, opendentalsoft.com, patientviewer.com and opendental.cloud (including subdomains such as
	www., api. or emailhosting.).

	Host names that only live in database preferences (for example EmailHostingEndpoint, whose default is set in ConvertDatabases7.cs) cannot
	be found this way. The code that reads them must check WebServiceMainHQProxy.IsHqAvailable instead.

	Not scanned:
	- comments (//, /* */ and /// XML doc comments in C#; <!-- --> in XAML and .resx), which also covers the GPL license headers
	- generated "Web References" folders
	- ConvertDatabases*.cs (historical database upgrade scripts)
	- bin and obj folders

	Every remaining reference must match an entry in $AllowList below, each with the reason it is deliberate.
	Exit code 0 means no unexpected references, 1 means at least one was found, 2 means the script could not run.

.PARAMETER Root
	Repository root. Defaults to the parent of the folder that contains this script.

.PARAMETER ShowAllowed
	Also lists the references that matched the allow-list, with their reasons.

.EXAMPLE
	powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoHqHosts.ps1

.EXAMPLE
	powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoHqHosts.ps1 -ShowAllowed
#>
[CmdletBinding()]
param(
	[string]$Root='',
	[switch]$ShowAllowed
)

Set-StrictMode -Version 2.0
$ErrorActionPreference='Stop'

#Files scanned: C# code, XAML, and .resx resources (which can hold HTML or text shown to users).
$ScanExtensions=@('.cs','.xaml','.resx')

#Hosts owned by Open Dental Software, Inc. Matches the bare domain and any subdomain, in any letter case.
$HostPattern='(?i)(?<![a-z0-9-])(?:[a-z0-9-]+\.)*(?:opendental\.com|open-dent\.com|opendentalsoft\.com|patientviewer\.com|opendental\.cloud)(?![a-z0-9-])'

#Deliberate remaining references. Path is a wildcard matched against the repository relative path (backslashes). Match, when present, is a
#regular expression that the line must also match. Keep entries narrow and say why each one is allowed.
$AllowList=@(
	#--- Manual link in the Practice by Numbers bridge. Open Dental's online help was removed; the vendor-neutrality work removed the
	#    DentalIntel bridge and kept this line, which opens the portal page for an office that has enabled the program link.
	@{ Path='WpfControlsOD\Bridges\PracticeByNumbers.cs'; Match='(?i)opendental\.com/manual/'; Reason='Practice by Numbers portal page on the Open Dental manual site (still to be pointed at the vendor).' }

	#--- Not network access.
	@{ Path='OpenDentalServer\ServiceMain.asmx.cs'; Match='WebService\(Namespace='; Reason='XML namespace identifier of the middle tier web service. Never contacted.' }
	@{ Path='OpenDental\Main Modules\ControlChart.cs'; Match='UpdateServerAddress\)\.ToLower\(\)\.Contains\('; Reason='String comparison on an old preference value. Nothing is contacted.' }
	@{ Path='OpenDentBusiness\WebServiceTests.cs'; Match='@opendental\.com'; Reason='Sample e-mail address used only by unit tests.' }

	#--- Unreachable UI and code that later phases delete.
	@{ Path='OpenDental\InternalTools\*'; Reason='Open Dental HQ staff tools. Unreachable because PrefC.IsODHQ is always false; deleted in a later phase.' }
	@{ Path='OpenDentBusiness\InternalTools\*'; Reason='Open Dental HQ staff tools. Unreachable because PrefC.IsODHQ is always false; deleted in a later phase.' }
	@{ Path='OpenDental\Forms\FormEServices*'; Reason='eServices setup windows. The eServices menu is gone and they close with "not available"; deleted in a later phase.' }
	@{ Path='OpenDental\Forms\FormWebFormSetup*'; Reason='Hosted Web Forms setup (example text). Its menu items are gone; deleted in a later phase.' }
	@{ Path='OpenDentBusiness\Data Interface\EmailHostingTemplates.cs'; Reason='Default template of the hosted mass e-mail eService, which is not available; deleted in a later phase.' }
	@{ Path='WpfControlsOD\Frms\FrmTestAllControls.xaml*'; Reason='Developer UI test window, only opened from the UnitTests project.' }
	@{ Path='WpfControlsOD\Frms\FrmTestSplitters.xaml*'; Reason='Developer UI test window, only opened from the UnitTests project.' }

	#--- Vendor integrations whose Open Dental hosted part is replaced in the direct vendor integrations wave.
	@{ Path='OpenDentBusiness\Properties\Resources.resx'; Reason='GoogleAuthCodeResponseHtml, the page shown in the browser after Google sign-in. It loads Open Dental styling; replaced with the direct Google integration.' }
)

#Comment stripping in C#, so that "//" inside a string literal (like a URL) is not mistaken for a comment.
$StripperSource=@'
using System.Text;
public static class HqHostScanText {
	///<summary>Returns the C# source with comments replaced by spaces. Line breaks and string literals are kept, so line numbers still match.</summary>
	public static string StripCSharpComments(string s) {
		StringBuilder sb=new StringBuilder(s.Length);
		int i=0;
		int n=s.Length;
		while(i<n) {
			char c=s[i];
			char next=(i+1<n) ? s[i+1] : '\0';
			if(c=='/' && next=='/') {
				while(i<n && s[i]!='\n') {
					sb.Append(s[i]=='\r' ? '\r' : ' ');
					i++;
				}
				continue;
			}
			if(c=='/' && next=='*') {
				while(i<n && !(s[i]=='*' && i+1<n && s[i+1]=='/')) {
					sb.Append((s[i]=='\n' || s[i]=='\r') ? s[i] : ' ');
					i++;
				}
				if(i<n) {
					sb.Append("  ");
					i+=2;
				}
				continue;
			}
			bool isVerbatim=(c=='@' && next=='"')
				|| (c=='$' && next=='@' && i+2<n && s[i+2]=='"')
				|| (c=='@' && next=='$' && i+2<n && s[i+2]=='"');
			if(isVerbatim) {
				while(s[i]!='"') {
					sb.Append(s[i]);
					i++;
				}
				sb.Append('"');
				i++;
				while(i<n) {
					if(s[i]=='"') {
						if(i+1<n && s[i+1]=='"') {
							sb.Append("\"\"");
							i+=2;
							continue;
						}
						sb.Append('"');
						i++;
						break;
					}
					sb.Append(s[i]);
					i++;
				}
				continue;
			}
			if(c=='"' || (c=='$' && next=='"')) {
				if(c=='$') {
					sb.Append('$');
					i++;
				}
				sb.Append('"');
				i++;
				while(i<n) {
					if(s[i]=='\\') {
						sb.Append(s[i]);
						if(i+1<n) {
							sb.Append(s[i+1]);
						}
						i+=2;
						continue;
					}
					if(s[i]=='"') {
						sb.Append('"');
						i++;
						break;
					}
					if(s[i]=='\n') {
						break;//Unterminated. Let the outer loop handle the line break.
					}
					sb.Append(s[i]);
					i++;
				}
				continue;
			}
			if(c=='\'') {
				sb.Append(c);
				i++;
				int start=i;
				while(i<n && i-start<12) {
					if(s[i]=='\\') {
						sb.Append(s[i]);
						if(i+1<n) {
							sb.Append(s[i+1]);
						}
						i+=2;
						continue;
					}
					if(s[i]=='\'') {
						sb.Append('\'');
						i++;
						break;
					}
					if(s[i]=='\n') {
						break;
					}
					sb.Append(s[i]);
					i++;
				}
				continue;
			}
			sb.Append(c);
			i++;
		}
		return sb.ToString();
	}

	///<summary>Returns the XAML with &lt;!-- --&gt; comments replaced by spaces, keeping line breaks.</summary>
	public static string StripXmlComments(string s) {
		StringBuilder sb=new StringBuilder(s.Length);
		int i=0;
		int n=s.Length;
		while(i<n) {
			if(string.CompareOrdinal(s,i,"<!--",0,4)==0) {
				int end=s.IndexOf("-->",i+4);
				end=(end<0) ? n : end+3;
				for(;i<end;i++) {
					sb.Append((s[i]=='\n' || s[i]=='\r') ? s[i] : ' ');
				}
				continue;
			}
			sb.Append(s[i]);
			i++;
		}
		return sb.ToString();
	}
}
'@

try {
	if(-not ('HqHostScanText' -as [type])) {
		Add-Type -TypeDefinition $StripperSource -Language CSharp
	}
	if([string]::IsNullOrEmpty($Root)) {
		$Root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
	}
	$Root=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
	$slnfPath=Join-Path $Root 'OpenDental.Local.slnf'
	if(-not (Test-Path -LiteralPath $slnfPath)) {
		throw "Solution filter not found: $slnfPath"
	}
	$slnf=Get-Content -LiteralPath $slnfPath -Raw | ConvertFrom-Json
	$projectDirs=@($slnf.solution.projects |
		ForEach-Object { Split-Path -Parent ($_ -replace '/','\') } |
		Where-Object { $_ -and ($_ -notmatch '^(UnitTests|UnitTestsCore)$') } |
		Sort-Object -Unique)
}
catch {
	Write-Host "Test-NoHqHosts: could not start: $($_.Exception.Message)"
	exit 2
}

$listViolations=New-Object System.Collections.Generic.List[object]
$listAllowed=New-Object System.Collections.Generic.List[object]
$countFiles=0
foreach($projectDir in $projectDirs) {
	$dirFull=Join-Path $Root $projectDir
	if(-not (Test-Path -LiteralPath $dirFull)) {
		continue
	}
	#Filter on Extension rather than -Include, which can also match 8.3 short names.
	$files=Get-ChildItem -LiteralPath $dirFull -Recurse -File -ErrorAction SilentlyContinue |
		Where-Object {
			$ScanExtensions -contains $_.Extension.ToLowerInvariant() -and
			$_.FullName -notmatch '\\(bin|obj)\\' -and
			$_.FullName -notmatch '\\Web References\\' -and
			$_.Name -notlike 'ConvertDatabases*.cs'
		}
	foreach($file in $files) {
		$countFiles++
		$text=[System.IO.File]::ReadAllText($file.FullName)
		if($text -notmatch $HostPattern) {
			continue #Quick filter. Most files never mention these hosts.
		}
		if($file.Extension -ne '.cs') {
			$code=[HqHostScanText]::StripXmlComments($text)
		}
		else {
			$code=[HqHostScanText]::StripCSharpComments($text)
		}
		$relPath=$file.FullName.Substring($Root.Length+1)
		$linesCode=$code -split "`n"
		$linesText=$text -split "`n"
		for($i=0;$i -lt $linesCode.Length;$i++) {
			$lineCode=$linesCode[$i]
			if($lineCode -notmatch $HostPattern) {
				continue
			}
			$finding=[pscustomobject]@{
				Path=$relPath
				Line=$i+1
				Text=$linesText[$i].Trim()
				Reason=$null
			}
			foreach($allow in $AllowList) {
				if($relPath -notlike $allow.Path) {
					continue
				}
				if($allow.ContainsKey('Match') -and $lineCode -notmatch $allow.Match) {
					continue
				}
				$finding.Reason=$allow.Reason
				break
			}
			if($finding.Reason) {
				$listAllowed.Add($finding)
			}
			else {
				$listViolations.Add($finding)
			}
		}
	}
}

function Format-Finding($finding) {
	$text=$finding.Text
	if($text.Length -gt 160) {
		$text=$text.Substring(0,160)+'...'
	}
	return "$($finding.Path):$($finding.Line): $text"
}

if($ShowAllowed -and $listAllowed.Count -gt 0) {
	Write-Host "Allowed references ($($listAllowed.Count)):"
	foreach($group in ($listAllowed | Group-Object Reason)) {
		Write-Host "  $($group.Name)"
		foreach($finding in $group.Group) {
			Write-Host "    $(Format-Finding $finding)"
		}
	}
	Write-Host ''
}
if($listViolations.Count -gt 0) {
	Write-Host "Test-NoHqHosts: FAILED. $($listViolations.Count) reference(s) to Open Dental hosts in live code:"
	foreach($finding in $listViolations) {
		Write-Host "  $(Format-Finding $finding)"
	}
	Write-Host 'Remove the reference, or add a narrow entry with a reason to the allow-list in tools\Test-NoHqHosts.ps1.'
	exit 1
}
Write-Host "Test-NoHqHosts: passed. Scanned $countFiles files in $($projectDirs.Count) projects; $($listAllowed.Count) allowed reference(s)."
exit 0
