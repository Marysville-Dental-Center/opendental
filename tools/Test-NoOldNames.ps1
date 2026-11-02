<#
.SYNOPSIS
	Lists the old internal code names (OpenDental, OpenDentBusiness, OpenDentalGraph, OpenDentalCloud, OpenDentalServer, OpenDentHL7) that are
	left outside a reviewed allow-list. Report mode by default; -Strict fails while any remain.

.DESCRIPTION
	Scans the text files of the repository (code, XAML, resources, project and solution files, scripts, configuration and Markdown) for the
	old names as whole words, in any context: code, strings, comments and docs. A name inside a longer identifier is not a match, so
	FormOpenDental, OpenDentalVendor, OpenDentalServerConfig.xml or OpenDentalService are never reported. Letter case must match, so database
	names such as opendental_dev are not reported either.

	Since the rename (docs\port-notes\RENAME.md) every old name left is in the allow-list, so -Strict passes; run it with -Strict after a
	merge or on a branch made before the rename, and rename what it lists or add a narrow entry with its reason.

	Not scanned: bin, obj, packages, .git, .vs, .claude, TestResults and node_modules folders, and binary files (only the extensions in
	$ScanExtensions and the files in $ScanFileNames are read).

	The allow-list ($AllowList below) holds what must keep the old names after the rename, each entry with its reason:
	- the frozen ConvertDatabases*.cs upgrade scripts and the history in docs (port notes, specs, audits and reports);
	- attribution and licence text, and GPL headers;
	- preference values and enum members stored in the database (the database never changes names);
	- values sent to vendors and other systems, and the Middle Tier's wire contract (SOAP namespace, IIS path, Credential Manager prefix,
	  web reference name and proxy class);
	- the OpenDental.sln and OpenDental.Local.slnf file names, which stay;
	- translation keys and plugin hook names;
	- text and checks about Open Dental itself (its company, its own Windows service, its developers' checkout folders, its leftover files
	  on a workstation, its host names, its licence list entries and generated preference tables);
	- the names of builds before the rename that are still looked up (the %LOCALAPPDATA% folder, the HL7 service name) and the test that
	  keeps the old literals out of the sites routed through CodeBase\Utilities\ProgramNames.cs.
	An entry has a Path, a wildcard matched against the repository relative path (backslashes), and at most one of Match and Line, regular
	expressions. With neither it allows the whole file. Match allows only the old names that lie inside a match of the expression, so another
	old name on the same line is still reported. Line allows every old name on a line that the expression matches. Keep entries narrow.

	Exit code 0: report mode, or -Strict with nothing outside the allow-list. 1: -Strict and names remain. 2: the script could not run.

.PARAMETER Root
	Repository root. Defaults to the parent of the folder that contains this script.

.PARAMETER Strict
	Fail (exit 1) when any old name is outside the allow-list. Passes since the rename.

.PARAMETER List
	Print every line with an old name outside the allow-list (path:line: text), not only the summary.

.PARAMETER ShowAllowed
	Also list the allowed lines, grouped by reason.

.PARAMETER OutFile
	Write every line with an old name outside the allow-list to this file (path:line: text), one per line.

.EXAMPLE
	powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoOldNames.ps1

.EXAMPLE
	powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NoOldNames.ps1 -Strict -List
#>
[CmdletBinding()]
param(
	[string]$Root='',
	[switch]$Strict,
	[switch]$List,
	[switch]$ShowAllowed,
	[string]$OutFile=''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference='Stop'

#The old names, as whole words, matching case.
$OldNames=@('OpenDental','OpenDentBusiness','OpenDentalGraph','OpenDentalCloud','OpenDentalServer','OpenDentHL7')
$TokenPattern='(?<![A-Za-z0-9_])(?:'+($OldNames -join '|')+')(?![A-Za-z0-9_])'

#Text files that are read. Everything else is skipped as binary.
$ScanExtensions=@('.cs','.xaml','.resx','.csproj','.vcxproj','.vcxitems','.filters','.sln','.slnf','.config','.settings','.targets','.props',
	'.ps1','.psm1','.psd1','.cmd','.bat','.sh','.md','.txt','.xml','.xsd','.json','.asmx','.asax','.aspx','.htm','.html','.rdl','.tt',
	'.manifest','.iss','.sql','.js','.css','.yml','.yaml','.py','.editorconfig','.gitattributes','.gitignore')
$ScanFileNames=@('LICENSE','NOTICE','Makefile')
$SkipFolders=@('bin','obj','packages','.git','.vs','.claude','TestResults','node_modules')

#What keeps the old names after the rename. Path: wildcard on the relative path. Match (optional): the old names inside its matches are allowed.
#Line (optional): every old name on a line it matches is allowed. Paths below a renamed project folder start with *\ so they survive the move.
$AllowList=@(
	#--- History and frozen code: whole files.
	@{ Path='*ConvertDatabases*.cs'; Reason='Frozen database upgrade scripts (Open Dental history); their string literals are data.' }
	@{ Path='docs\port-notes\*'; Reason='Port notes: the record of what was done, with the paths of the time. A mapping note covers the rename.' }
	@{ Path='docs\specs\*'; Reason='Port specs: the record of the 24.4-26.1 port, with the paths of the time.' }
	@{ Path='docs\reviews\*'; Reason='Dated review snapshots (organization, rename, release readiness), with the names and paths of the time.' }
	@{ Path='docs\third-party-binaries-audit.md'; Reason='Audit record of the binaries as they were.' }
	@{ Path='docs\m1-validation-report.md'; Reason='Validation report of milestone M1, as run.' }
	@{ Path='tests\ReportTests\RdlSourceBuildTests.cs'; Match='(?:ProjectReference|HintPath): OpenDental\b'; Reason='Checks rows of the binaries audit, which keeps the names of its time.' }
	@{ Path='UnitTests\Resources\dump.txt'; Match='OpenDental\.Imaging\.FileStore'; Reason="Open Dental's seed database dump: the ImageStore preference value." }
	@{ Path='tools\Test-NoOldNames.ps1'; Reason='This scanner names the old names.' }
	@{ Path='tools\Rename-ToOpenPmsDental.ps1'; Reason='The rename script names the old names it maps.' }
	@{ Path='tools\Rename-TokenPass.py'; Reason="The rename's token pass names the old names it maps and the ones it keeps." }

	#--- Attribution, licences and GPL headers: whole lines.
	@{ Path='*'; Line='(?i)GNU General Public License|Copyright \(C\) \d{4}|Jordan Sparks'; Reason='Licence and copyright notices.' }
	@{ Path='*'; Line='(?i)is a fork of Open Dental|not affiliated with or endorsed by'; Reason='The attribution to Open Dental.' }
	@{ Path='*\Forms\FormLicense.cs'; Match='Items\.Add\("OpenDental",'; Reason='The licence list entry for Open Dental.' }
	@{ Path='*\Forms\FormRegistrationKey.cs'; Match='Items\.Add\("OpenDental",'; Reason='The licence list entry for Open Dental.' }

	#--- Values stored in the database: preference values and enum member names (EnumAsString columns, program.ProgName).
	@{ Path='*\TableTypes\ImageDraw.cs'; Match='^\s*OpenDental,'; Reason='EnumImageAnnotVendor.OpenDental: an enum member stored in the database.' }
	@{ Path='*\TableTypes\Pref.cs'; Match='^\s*OpenDental,'; Reason='AccountingSoftware.OpenDental: an enum member stored as a preference value.' }
	@{ Path='*'; Match='\b(EnumImageAnnotVendor|AccountingSoftware)\.OpenDental\b'; Reason='Enum members stored in the database are never renamed.' }
	@{ Path='*'; Match='\b0=OpenDental\b'; Reason='AccountingSoftware.OpenDental (0): the enum member stored as a preference value.' }
	@{ Path='DocumentationBuilder\OpenDentalDocumentation.xml'; Match='<EnumValue name="OpenDental">'; Reason='EnumImageAnnotVendor.OpenDental: an enum member stored in the database.' }
	@{ Path='tests\RegistryTests\Data\OdEnums25_3.txt'; Match='\|OpenDental\|'; Reason="EnumImageAnnotVendor.OpenDental in Open Dental 25.3's enum reference: an enum member stored in the database." }
	@{ Path='*'; Match='OpenDental\.Imaging\.(FileStore|SqlStore)'; Reason='The legacy ImageStore preference value.' }

	#--- Values sent to vendors and other systems (vendor-direct decisions keep them).
	@{ Path='WpfControlsOD\Bridges\Sirona.cs'; Match='line\.Append\("OpenDental"\)'; Reason='The sender name Sirona expects.' }
	@{ Path='WpfControlsOD\Bridges\Vipersoft.cs'; Match='"OpenDental\|"'; Reason='The program name Vipersoft expects.' }
	@{ Path='*\Bridges\EHG_statements.cs'; Match='UploaderName=OpenDental'; Reason='The uploader name EHG expects.' }
	@{ Path='db\init\new-database.sql'; Match='--source \\"OpenDental\\"'; Reason='The --source value Shining3D expects (program.CommandLine).' }
	@{ Path='*\X12\X837_4010.cs'; Match='Sometimes OpenDental\.'; Reason='The X12 GS02 sender code, a value sent to clearinghouses.' }
	@{ Path='*\Data Interface\EmailMessages.cs'; Match='CreateAcks\("OpenDental "'; Reason='The reporting agent of Direct message acknowledgements, a value sent to other systems.' }
	@{ Path='tests\VendorDirectTests\DentalXChange\DxcPartnerIdentityTests.cs'; Match='Client=\\"OpenDental\\"|UploaderName=OpenDental'; Reason='Checks that the removed vendor values stay removed.' }
	@{ Path='tests\VendorDirectTests\Erx\ErxSourceTests.cs'; Match='\\"OpenDental\(Lexi\)\?\\"'; Reason='Checks that the removed eRx vendor values stay removed.' }

	#--- The Middle Tier's wire contract and deployment path (decision D4).
	@{ Path='*'; Match='open-dent\.com/OpenDentalServer'; Reason='The SOAP namespace and action of the Middle Tier web service.' }
	@{ Path='*'; Match='/OpenDentalServer/ServiceMain\.asmx'; Reason="The Middle Tier's IIS path in its default URL." }
	@{ Path='*'; Match='(?:localhost|>|\s|^)/OpenDentalServer\b'; Reason="The Middle Tier's IIS virtual path (/OpenDentalServer)." }
	@{ Path='*'; Match='"?OpenDental Middle Tier\b:?"?'; Reason='The Windows Credential Manager prefix of saved Middle Tier logins.' }
	@{ Path='*'; Match='(?<=\.|Web References\\)OpenDentalServer\b'; Reason='The Middle Tier web reference, named after the web service.' }
	@{ Path='*'; Match='\bOpenDentalServer\.ServiceMain\b'; Reason='The web reference proxy class (OpenPmsDentalBusiness.OpenDentalServer.ServiceMain).' }

	#--- The solution and solution filter keep their file names (the shared build lane, run-tests, Test-NoHqHosts and the merge driver use them).
	@{ Path='*'; Match='\bOpenDental\.(?:Local\.slnf|sln)\b'; Reason='The OpenDental.sln and OpenDental.Local.slnf file names stay.' }

	#--- Translation keys and plugin hook names.
	@{ Path='*'; Match='Lans?\.g\("OpenDental"'; Reason='A translation key (translations are keyed by this text).' }
	@{ Path='*\UI\Appt\ControlApptPanel.cs'; Match='"OpenDentBusiness\.UI\.ApptSingleDrawing\.\w+"'; Reason='A plugin hook name.' }

	#--- Open Dental itself.
	@{ Path='*'; Match="(?i)\b[a-z0-9.-]*opendental\.(com|cloud)\b"; Reason="Open Dental's host names (the blocked-host list and its tests)." }
	@{ Path='docs\adoption-runbook.md'; Line="'C:\\Program Files\*\\Open Dental\*'"; Reason="Finds Open Dental's own files in its install folders." }
	@{ Path='docs\adoption-runbook.md'; Match='An `OpenDental\.exe` is always Open Dental|no `OpenDental\.exe` or `FreeDentalConfig\.xml` left'; Reason="Open Dental's own exe on a workstation." }
	@{ Path='docs\adoption-runbook.md'; Match='`OpenDentalServer`(?= (?:web application )?in IIS)'; Reason="Open Dental's Middle Tier web application in IIS, stopped before the switch." }
	@{ Path='*'; Match='\bOpenDental, Inc\.'; Reason='Open Dental Software, Inc., the company.' }
	@{ Path='*'; Match='\bOpenDental Service\b'; Reason="Open Dental's own Windows service (OpenDentalService), which this program does not ship." }
	@{ Path='*'; Match='Versioned\\+OpenDental\b'; Reason="Open Dental developers' checkout folder in developer-only default paths." }
	@{ Path='DocumentationBuilder\PreferenceTable\*'; Reason="Open Dental's generated preference tables of versions 23.3 to 24.2 (history)." }
	@{ Path='tests\AttributionTests\ReleaseIdentityTests.cs'; Match='"OpenDental(?: API End User)?"'; Reason="Entries of Open Dental's licence list that are gone." }

	#--- Names of builds before the rename, kept so that what they left on a computer is still found.
	@{ Path='CodeBase\Utilities\ProgramNames.cs'; Match='LegacyLocalAppDataFolderName="OpenDental"'; Reason='The folder builds before the rename used, still scanned for unsaved work.' }
	@{ Path='CodeBase\Utilities\ProgramNames.cs'; Match='named OpenDentHL7'; Reason='The HL7 service name of builds before the rename, still found by LegacyServicePrefix.' }
	@{ Path='*HL7\Program.cs'; Match='\(OpenDentHL7\)'; Reason='The HL7 service name of builds before the rename, still found by LegacyServicePrefix.' }
	@{ Path='tests\ReleaseFlowTests\ProgramNamesTests.cs'; Match='"OpenDental",ProgramNames\.LegacyLocalAppDataFolderName'; Reason='The test that the pre-rename folder never changes.' }
	@{ Path='tests\ReleaseFlowTests\ProgramNamesTests.cs'; Line='^\s*\[@"[^"]+"\]=new\[\]'; Reason='The pre-rename literals that must stay out of the sites routed through ProgramNames.' }
	@{ Path='tests\ReleaseFlowTests\InstallerTests.cs'; Match='"OpenDental\.exe","OpenDentBusiness\.dll"'; Reason="The release's deny-list test: a release never carries a file of the pre-rename names." }
)

#The file walk and the line scan, in C#: a PowerShell loop over every old name in the repository takes minutes.
$ScannerSource=@'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
public class OldNameLine {
	public int Line;
	public string Text;
	///<summary>The old names on the line that no rule allows. Empty when every one is allowed.</summary>
	public List<string> Names=new List<string>();
	///<summary>The reason of the rule that allowed the last allowed name, or null.</summary>
	public string AllowedReason;
	public int AllowedCount;
}
public static class OldNameScan {
	///<summary>The files below root to read: their extension (lower case) or file name is listed, and no folder on the way is skipped.</summary>
	public static List<string> ListFiles(string root,string[] arraySkipFolders,string[] arrayExtensions,string[] arrayFileNames) {
		HashSet<string> hashSetSkip=new HashSet<string>(arraySkipFolders,StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSetExtensions=new HashSet<string>(arrayExtensions,StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSetNames=new HashSet<string>(arrayFileNames,StringComparer.OrdinalIgnoreCase);
		List<string> listFiles=new List<string>();
		Stack<string> stackDirs=new Stack<string>();
		stackDirs.Push(root);
		while(stackDirs.Count>0) {
			string dir=stackDirs.Pop();
			foreach(string sub in Directory.GetDirectories(dir)) {
				if(!hashSetSkip.Contains(Path.GetFileName(sub))) {
					stackDirs.Push(sub);
				}
			}
			foreach(string file in Directory.GetFiles(dir)) {
				if(hashSetExtensions.Contains(Path.GetExtension(file)) || hashSetNames.Contains(Path.GetFileName(file))) {
					listFiles.Add(file);
				}
			}
		}
		listFiles.Sort(StringComparer.OrdinalIgnoreCase);
		return listFiles;
	}

	///<summary>Every line of text with an old name. An old name is allowed when it lies inside a match of one of the rules on that line, or
	///when a line rule (arrayIsLine) matches the line.</summary>
	public static List<OldNameLine> Scan(string text,Regex regexToken,Regex[] arrayRules,bool[] arrayIsLine,string[] arrayReasons) {
		List<OldNameLine> listLines=new List<OldNameLine>();
		string[] arrayLines=text.Split('\n');
		for(int i=0;i<arrayLines.Length;i++) {
			string line=arrayLines[i].TrimEnd('\r');
			MatchCollection matchesToken=regexToken.Matches(line);
			if(matchesToken.Count==0) {
				continue;
			}
			OldNameLine oldNameLine=new OldNameLine();
			oldNameLine.Line=i+1;
			oldNameLine.Text=line.Trim();
			string reasonLine=null;
			for(int r=0;r<arrayRules.Length && reasonLine==null;r++) {
				if(arrayIsLine[r] && arrayRules[r].IsMatch(line)) {
					reasonLine=arrayReasons[r];
				}
			}
			if(reasonLine!=null) {
				oldNameLine.AllowedReason=reasonLine;
				oldNameLine.AllowedCount=matchesToken.Count;
				listLines.Add(oldNameLine);
				continue;
			}
			List<Match>[] arrayRuleMatches=new List<Match>[arrayRules.Length];
			foreach(Match matchToken in matchesToken) {
				string reason=null;
				for(int r=0;r<arrayRules.Length && reason==null;r++) {
					if(arrayIsLine[r]) {
						continue;
					}
					if(arrayRuleMatches[r]==null) {
						arrayRuleMatches[r]=new List<Match>();
						foreach(Match matchRule in arrayRules[r].Matches(line)) {
							arrayRuleMatches[r].Add(matchRule);
						}
					}
					foreach(Match matchRule in arrayRuleMatches[r]) {
						if(matchRule.Index<=matchToken.Index && matchRule.Index+matchRule.Length>=matchToken.Index+matchToken.Length) {
							reason=arrayReasons[r];
							break;
						}
					}
				}
				if(reason==null) {
					oldNameLine.Names.Add(matchToken.Value);
				}
				else {
					oldNameLine.AllowedReason=reason;
					oldNameLine.AllowedCount++;
				}
			}
			listLines.Add(oldNameLine);
		}
		return listLines;
	}
}
'@

try {
	if(-not ('OldNameScan' -as [type])) {
		Add-Type -TypeDefinition $ScannerSource -Language CSharp
	}
	if([string]::IsNullOrEmpty($Root)) {
		$Root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
	}
	$Root=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
	if(-not ((Test-Path -LiteralPath (Join-Path $Root 'LICENSE')) -and (Test-Path -LiteralPath (Join-Path $Root 'NOTICE')))) {
		throw "Not the repository root (no LICENSE and NOTICE): $Root"
	}
	$regexToken=New-Object System.Text.RegularExpressions.Regex($TokenPattern)
	$allowCompiled=@($AllowList | ForEach-Object {
		$match=$null
		$isLine=$false
		if($_.ContainsKey('Match')) {
			$match=New-Object System.Text.RegularExpressions.Regex($_.Match)
		}
		elseif($_.ContainsKey('Line')) {
			$match=New-Object System.Text.RegularExpressions.Regex($_.Line)
			$isLine=$true
		}
		[pscustomobject]@{ Path=$_.Path; Match=$match; IsLine=$isLine; Reason=$_.Reason }
	})
	$files=[OldNameScan]::ListFiles($Root,[string[]]$SkipFolders,[string[]]$ScanExtensions,[string[]]$ScanFileNames)
}
catch {
	Write-Host "Test-NoOldNames: could not start: $($_.Exception.Message)"
	exit 2
}

$listFindings=New-Object System.Collections.Generic.List[object]
$listAllowed=New-Object System.Collections.Generic.List[object]
$countAllowedTokens=0
$dictByName=@{}
$dictByFolder=@{}
$countTokens=0
$hashSetFilesWithFindings=New-Object System.Collections.Generic.HashSet[string]
foreach($path in $files) {
	$text=[System.IO.File]::ReadAllText($path)
	if(-not $regexToken.IsMatch($text)) {
		continue
	}
	$relPath=$path.Substring($Root.Length+1)
	$ruleFile=$null
	$listRules=New-Object System.Collections.Generic.List[object]
	foreach($rule in $allowCompiled) {
		if($relPath -notlike $rule.Path) {
			continue
		}
		if($rule.Match -eq $null) {
			$ruleFile=$rule
			break
		}
		$listRules.Add($rule)
	}
	if($ruleFile) {
		$countAllowedTokens+=$regexToken.Matches($text).Count
		if($ShowAllowed) {
			$listAllowed.Add([pscustomobject]@{ Path=$relPath; Line=0; Text='(whole file)'; Reason=$ruleFile.Reason })
		}
		continue
	}
	[System.Text.RegularExpressions.Regex[]]$arrayRules=@(foreach($rule in $listRules) { $rule.Match })
	[string[]]$arrayReasons=@(foreach($rule in $listRules) { $rule.Reason })
	[bool[]]$arrayIsLine=@(foreach($rule in $listRules) { $rule.IsLine })
	$folder=$(if($relPath.Contains('\')) { $relPath.Substring(0,$relPath.IndexOf('\')) } else { '(root)' })
	foreach($oldNameLine in [OldNameScan]::Scan($text,$regexToken,$arrayRules,$arrayIsLine,$arrayReasons)) {
		$countAllowedTokens+=$oldNameLine.AllowedCount
		if($oldNameLine.Names.Count -eq 0) {
			if($ShowAllowed) {
				$listAllowed.Add([pscustomobject]@{ Path=$relPath; Line=$oldNameLine.Line; Text=$oldNameLine.Text; Reason=$oldNameLine.AllowedReason })
			}
			continue
		}
		$listFindings.Add([pscustomobject]@{ Path=$relPath; Line=$oldNameLine.Line; Text=$oldNameLine.Text })
		[void]$hashSetFilesWithFindings.Add($relPath)
		foreach($name in $oldNameLine.Names) {
			$countTokens++
			$dictByName[$name]=1+$(if($dictByName.ContainsKey($name)) { $dictByName[$name] } else { 0 })
		}
		$dictByFolder[$folder]=1+$(if($dictByFolder.ContainsKey($folder)) { $dictByFolder[$folder] } else { 0 })
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
	Write-Host "Allowed ($($listAllowed.Count) lines or whole files):"
	foreach($group in ($listAllowed | Group-Object Reason)) {
		Write-Host "  $($group.Name) ($($group.Count))"
		foreach($finding in ($group.Group | Select-Object -First 25)) {
			Write-Host "    $(Format-Finding $finding)"
		}
		if($group.Count -gt 25) {
			Write-Host "    ... and $($group.Count-25) more"
		}
	}
	Write-Host ''
}
if($OutFile) {
	$listFindings | ForEach-Object { Format-Finding $_ } | Set-Content -LiteralPath $OutFile -Encoding UTF8
}
if($List) {
	foreach($finding in $listFindings) {
		Write-Host (Format-Finding $finding)
	}
}
$countFilesWithFindings=$hashSetFilesWithFindings.Count
$mode=$(if($Strict) { 'strict mode' } else { 'report mode' })
Write-Host ("Test-NoOldNames ($mode): scanned $($files.Count) files. $countTokens old name(s) on $($listFindings.Count) line(s) in "+
	"$countFilesWithFindings file(s) are outside the allow-list; $countAllowedTokens are allowed.")
if($listFindings.Count -gt 0) {
	$byName=($OldNames | Where-Object { $dictByName.ContainsKey($_) } | ForEach-Object { "$_ $($dictByName[$_])" }) -join ', '
	Write-Host "  By name: $byName"
	$byFolder=($dictByFolder.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 15 | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', '
	Write-Host "  Lines by top folder (first 15): $byFolder"
	if(-not $List) {
		Write-Host '  Run with -List (or -OutFile <path>) to see every line, -ShowAllowed for the allowed ones.'
	}
	if($Strict) {
		Write-Host 'Test-NoOldNames: FAILED. Rename what is left, or add a narrow entry with its reason to the allow-list in tools\Test-NoOldNames.ps1.'
		exit 1
	}
	Write-Host '  Report mode: not a failure. The rename run turns on -Strict.'
}
elseif($Strict) {
	Write-Host 'Test-NoOldNames: passed.'
}
exit 0
