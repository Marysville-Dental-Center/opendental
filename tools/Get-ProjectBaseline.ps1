<#
.SYNOPSIS
Records what every C# project of OpenDental.Local.slnf compiles, embeds and puts in its output folder, and compares two
such records. Stage 1 of the .NET 10 move (docs/specs/net10-plan.md, S1-0 step 4) takes the record on the classic
projects; every package that converts projects to SDK-style compares against it.

.DESCRIPTION
Run it after a Debug|x86 build of OpenDental.Local.slnf. It writes five text files into -Out, one line per entry, sorted.
No absolute path is written, so a record does not depend on the folder it was taken in: a path inside the repository is written
relative to it, and a path inside the Program Files, Windows, local app data, temp or user profile folder starts with a placeholder
such as <ProgramFiles(x86)>\. The script stops with an error, before writing the file, if a line would still hold the repository's
path or any other drive path.

  items.txt      The evaluated Compile, EmbeddedResource, Page, Resource, None and Content items of each C# project
                 (MSBuild -getItem, with the configuration and platform the solution gives the project for Debug|x86):
                 project|item type|include|metadata. The include is the item's path as the project gives it: relative to
                 the project folder, or relative to the repository for an item the project names by a full path (the linked
                 sources of the third-party wrappers, through a folder property); the metadata are the item's own (Link,
                 LogicalName, DependentUpon, CopyToOutputDirectory, SubType, ...), not the well-known ones MSBuild adds to
                 every item (FullPath, ModifiedTime, ...).
  resources.txt  The manifest resource names of each project's built assembly: project|resource name.
  files.txt      Every file under OpenPmsDental\bin\Debug and under the output folder of every test project:
                 folder|file|size|SHA-256. The Logger folder in them is left out: the programs and the tests write it at
                 run time (NoHqTests writes two files whose names hold the date), the build does not.
  configs.txt    Every *.exe.config and *.dll.config in those folders: folder|file|SHA-256 of its text.
  config-texts.txt  The text of each of those configs, once per distinct text (most test folders hold the same few):
                 SHA-256|line number|line.

With -Against, it then compares -Out with an earlier record and prints every difference. Differences in items.txt,
resources.txt, configs.txt, config-texts.txt and in the list of file names of files.txt fail the comparison (exit code 1). A different
size or SHA-256 of a file with the same name is listed but does not fail it: most classic projects do not compile
deterministically, so every build changes their assemblies.

With -CompareOnly, nothing is evaluated or read from the build: -Out and -Against are two existing records.

MSBuild is the Build Tools 2026 MSBuild 18 (found as tools\Initialize-BuildEnvironment.ps1 finds it) unless -MSBuildPath
names another. Evaluation only: nothing is built, restored or downloaded.

.PARAMETER Out
The folder the record is written into (created if missing), or the newer record with -CompareOnly.

.PARAMETER Against
An earlier record to compare with.

.PARAMETER CompareOnly
Compare -Out with -Against without writing a new record.

.PARAMETER Projects
Only these projects (file names without .csproj, e.g. xCodeBase,OpenPmsDental). Default: every C# project of the slnf.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Get-ProjectBaseline.ps1 -Out $env:TEMP\baseline-now -Against tools\project-baseline
#>
[CmdletBinding(PositionalBinding=$false)]
param(
	[Parameter(Mandatory=$true)][string]$Out,
	[string]$Against='',
	[switch]$CompareOnly,
	[string[]]$Projects=@(),
	[string]$MSBuildPath='',
	[string]$Root=''
)
$ErrorActionPreference='Stop'
try{ [System.Diagnostics.Process]::GetCurrentProcess().PriorityClass='BelowNormal' }catch{}
if(-not $Root){ $Root=Split-Path -Parent $PSScriptRoot }
$Root=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
$Projects=@($Projects | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

#Paths in the record are relative to the repository; a path under one of the other folders below gets that folder's placeholder, so no
#computer's folders end up in it. Each pair is (folder with its trailing separator, what replaces it).
$script:replacements=New-Object 'System.Collections.Generic.List[object]'
function Add-Replacement([string]$path,[string]$token){
	if(-not $path){ return }
	$folder=$path.TrimEnd('\','/')
	#The concatenation needs its own parentheses: the comma binds tighter than +, and without them the pair is one string.
	$script:replacements.Add(@(($folder+'\'),$token))
	#The same folder written with forward slashes.
	$script:replacements.Add(@((($folder -replace '\\','/')+'/'),($token -replace '\\','/')))
}
Add-Replacement $Root ''
Add-Replacement ${env:ProgramFiles(x86)} '<ProgramFiles(x86)>\'
Add-Replacement $env:ProgramFiles '<ProgramFiles>\'
Add-Replacement $env:LOCALAPPDATA '<LocalAppData>\'
Add-Replacement $env:USERPROFILE '<UserProfile>\'
Add-Replacement $env:TEMP '<Temp>\'
Add-Replacement $env:windir '<Windows>\'
#The longest folder first: the temp folder lies inside the local app data folder, that one inside the user profile, and the repository
#can lie inside any of them.
$script:replacements.Sort([Comparison[object]]{ param($a,$b) $b[0].Length.CompareTo($a[0].Length) })
foreach($pair in $script:replacements){
	if($pair.Count -ne 2 -or -not $pair[0]){ throw "A path replacement is not a pair: [$($pair -join '][')]" }
}
$script:rootForward=$Root -replace '\\','/'
function Get-Relative([string]$text){
	if(-not $text){ return $text }
	#The repository's folder itself, without its trailing separator (a folder property, say). Checked first: when the repository
	#lies inside the temp folder, say, the temp folder's pair would otherwise turn it into <Temp>\...
	if($text -ieq $Root -or $text -ieq $script:rootForward){ return '.' }
	foreach($pair in $script:replacements){
		$index=$text.IndexOf($pair[0],[StringComparison]::OrdinalIgnoreCase)
		while($index -ge 0){
			$text=$text.Substring(0,$index)+$pair[1]+$text.Substring($index+$pair[0].Length)
			$index=$text.IndexOf($pair[0],$index+$pair[1].Length,[StringComparison]::OrdinalIgnoreCase)
		}
	}
	return $text
}
#The forms the repository's folder can still have in a line when it is not followed by a separator (inside a list, say): as it is,
#with forward slashes, and with a placeholder when it lies inside one of the other folders.
$script:rootForms=New-Object 'System.Collections.Generic.List[string]'
foreach($form in @($Root,$script:rootForward,(Get-Relative ($Root+'|')).TrimEnd('|'),(Get-Relative ($script:rootForward+'|')).TrimEnd('|'))){
	if(-not $script:rootForms.Contains($form)){ $script:rootForms.Add($form) }
}

function Write-Record([string]$name,[System.Collections.Generic.List[string]]$lines){
	#No line may still hold a path of this computer: the repository's own (in any of its forms, so also a UNC path) or any other drive
	#path. A record with one would differ in every other folder it is compared from.
	foreach($line in $lines){
		$hasRoot=$false
		foreach($form in $script:rootForms){
			if($line.IndexOf($form,[StringComparison]::OrdinalIgnoreCase) -ge 0){ $hasRoot=$true }
		}
		if($hasRoot -or $line -match '(?<![A-Za-z0-9])[A-Za-z]:[\\/]'){
			throw "$name would hold an absolute path, so nothing more is written: $line"
		}
	}
	$sorted=$lines.ToArray()
	[Array]::Sort($sorted,[StringComparer]::Ordinal)
	#A line that occurs more than once (a duplicate item, say) is numbered, so a comparison sees how often it occurs.
	$previous=$null
	$count=0
	for($i=0;$i -lt $sorted.Length;$i++){
		if($sorted[$i] -ceq $previous){
			$count++
			$sorted[$i]=$sorted[$i]+'|#'+$count
			continue
		}
		$previous=$sorted[$i]
		$count=1
	}
	$utf8=New-Object System.Text.UTF8Encoding($false)
	$text=if($sorted.Length -gt 0){ ($sorted -join "`n")+"`n" }else{ '' }
	[System.IO.File]::WriteAllText((Join-Path $Out $name),$text,$utf8)
	Write-Host ("{0}: {1} lines" -f $name,$sorted.Length)
}

#region Record
function Get-SolutionProjects {
	$slnf=Get-Content -LiteralPath (Join-Path $Root 'OpenDental.Local.slnf') -Raw | ConvertFrom-Json
	$slnText=Get-Content -LiteralPath (Join-Path $Root 'OpenDental.sln') -Raw
	$list=@()
	foreach($relative in $slnf.solution.projects){
		if($relative -notmatch '\.csproj$'){ continue }
		$name=[System.IO.Path]::GetFileNameWithoutExtension($relative)
		if($Projects.Count -gt 0 -and $Projects -notcontains $name){ continue }
		#The project's configuration and platform for the solution's Debug|x86.
		$configuration='Debug'
		$platform='x86'
		$match=[regex]::Match($slnText,'Project\("\{[^}]+\}"\) = "[^"]+", "'+[regex]::Escape($relative)+'", "(\{[^}]+\})"',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
		if($match.Success){
			$guid=$match.Groups[1].Value
			$cfg=[regex]::Match($slnText,[regex]::Escape($guid)+'\.Debug\|x86\.ActiveCfg = ([^|\r\n]+)\|([^\r\n]+)',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
			if($cfg.Success){
				$configuration=$cfg.Groups[1].Value.Trim()
				$platform=$cfg.Groups[2].Value.Trim() -replace '^Any CPU$','AnyCPU'
			}
		}
		$list+=[pscustomobject]@{ Name=$name; Relative=$relative; Path=(Join-Path $Root $relative); Configuration=$configuration; Platform=$platform }
	}
	return $list
}

#The well-known metadata MSBuild gives every item; the record keeps only an item's own metadata.
$wellKnown=@('Identity','FullPath','RootDir','Filename','Extension','RelativeDir','Directory','RecursiveDir','ModifiedTime','CreatedTime',
	'AccessedTime','DefiningProjectFullPath','DefiningProjectDirectory','DefiningProjectName','DefiningProjectExtension')
Add-Type -AssemblyName System.Web.Extensions
$serializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength=[int]::MaxValue

function Get-ProjectEvaluation($project){
	$arguments=@($project.Path,'-nologo','-getItem:Compile','-getItem:EmbeddedResource','-getItem:Page','-getItem:Resource','-getItem:None','-getItem:Content',
		'-getProperty:TargetPath,AssemblyName,OutputType',"-p:Configuration=$($project.Configuration)","-p:Platform=$($project.Platform)",
		"-p:SolutionDir=$Root\","-p:SolutionPath=$Root\OpenDental.sln",'-p:SolutionName=OpenDental','-p:SolutionExt=.sln')
	$output=& $script:msbuild @arguments 2>&1 | ForEach-Object { "$_" }
	if($LASTEXITCODE -ne 0){ throw "MSBuild could not evaluate $($project.Relative): $(($output | Select-Object -Last 5) -join ' ')" }
	return $serializer.DeserializeObject(($output -join "`n"))
}

function Get-ManifestResourceNames([string]$assemblyPath){
	#Reflection-only, in this process: every project's assembly has its own name, so no two loads collide.
	$assembly=[System.Reflection.Assembly]::ReflectionOnlyLoadFrom($assemblyPath)
	return $assembly.GetManifestResourceNames()
}

if(-not $CompareOnly){
	. (Join-Path $PSScriptRoot 'Initialize-BuildEnvironment.ps1')
	$environment=Initialize-BuildEnvironment -RepoRoot $Root
	$script:msbuild=if($MSBuildPath){ $MSBuildPath }else{ Get-BuildToolPath -Tool MSBuild }
	if(-not $script:msbuild -or -not (Test-Path -LiteralPath $script:msbuild)){ throw 'MSBuild 18 was not found. Pass -MSBuildPath.' }
	New-Item -ItemType Directory -Force -Path $Out | Out-Null
	$items=New-Object 'System.Collections.Generic.List[string]'
	$resources=New-Object 'System.Collections.Generic.List[string]'
	$files=New-Object 'System.Collections.Generic.List[string]'
	$configs=New-Object 'System.Collections.Generic.List[string]'
	$configTexts=New-Object 'System.Collections.Generic.List[string]'
	$configTextHashes=New-Object 'System.Collections.Generic.HashSet[string]'
	$outputFolders=New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::OrdinalIgnoreCase)
	[void]$outputFolders.Add((Join-Path $Root 'OpenPmsDental\bin\Debug'))
	$list=Get-SolutionProjects
	Write-Host "MSBuild: $script:msbuild"
	Write-Host "$($list.Count) C# projects"
	foreach($project in $list){
		$evaluation=Get-ProjectEvaluation $project
		#Resource: the WPF resources a UseWPF project compiles into its .g.resources (S1-4's review), recorded since S1-T3.
		foreach($itemType in @('Compile','EmbeddedResource','Page','Resource','None','Content')){
			$itemList=$evaluation['Items'][$itemType]
			if(-not $itemList){ continue }
			foreach($item in $itemList){
				$metadata=@($item.Keys | Where-Object { $wellKnown -notcontains $_ } | Sort-Object | ForEach-Object { $_+'='+(Get-Relative ([string]$item[$_])) })
				$items.Add(('{0}|{1}|{2}|{3}' -f $project.Name,$itemType,(Get-Relative ([string]$item['Identity'])),($metadata -join ';')))
			}
		}
		$targetPath=[string]$evaluation['Properties']['TargetPath']
		if($targetPath -and (Test-Path -LiteralPath $targetPath)){
			try{
				foreach($resource in (Get-ManifestResourceNames $targetPath)){ $resources.Add(('{0}|{1}' -f $project.Name,$resource)) }
			}
			catch{ $resources.Add(('{0}|<unreadable: {1}>' -f $project.Name,$_.Exception.GetType().Name)) }
		}
		else{
			$resources.Add(('{0}|<not built: {1}>' -f $project.Name,(Get-Relative $targetPath)))
		}
		if($project.Name -match 'Tests$' -and $targetPath){ [void]$outputFolders.Add((Split-Path -Parent $targetPath)) }
	}
	$sha=[System.Security.Cryptography.SHA256]::Create()
	foreach($folder in $outputFolders){
		if(-not (Test-Path -LiteralPath $folder -PathType Container)){
			$files.Add(('{0}|<missing>|0|' -f (Get-Relative $folder)))
			continue
		}
		$folderRelative=Get-Relative $folder
		foreach($file in Get-ChildItem -LiteralPath $folder -Recurse -File -Force){
			$name=$file.FullName.Substring($folder.Length).TrimStart('\')
			#The log files a program or a test writes at run time (CodeBase's Logger: <its folder>\Logger\<log>\<date>\), not the build.
			if($name -like 'Logger\*'){ continue }
			$stream=[System.IO.File]::OpenRead($file.FullName)
			try{ $hash=-join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) }finally{ $stream.Dispose() }
			$files.Add(('{0}|{1}|{2}|{3}' -f $folderRelative,$name,$file.Length,$hash))
			if($name -match '\.(exe|dll)\.config$'){
				#The text with relative paths and one kind of line end, hashed; each distinct text is written once.
				$lines=@([System.IO.File]::ReadAllLines($file.FullName) | ForEach-Object { Get-Relative $_.TrimEnd() })
				$textHash=-join ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))) | ForEach-Object { $_.ToString('x2') })
				$configs.Add(('{0}|{1}|{2}' -f $folderRelative,$name,$textHash))
				if($configTextHashes.Add($textHash)){
					for($i=0;$i -lt $lines.Count;$i++){ $configTexts.Add(('{0}|{1:D5}|{2}' -f $textHash,($i+1),$lines[$i])) }
				}
			}
		}
	}
	Write-Record 'items.txt' $items
	Write-Record 'resources.txt' $resources
	Write-Record 'files.txt' $files
	Write-Record 'configs.txt' $configs
	Write-Record 'config-texts.txt' $configTexts
}
#endregion Record

#region Compare
if(-not $Against){ exit 0 }
$exitCode=0
function Read-Lines([string]$folder,[string]$name){
	$path=Join-Path $folder $name
	if(-not (Test-Path -LiteralPath $path)){ throw "No $name in $folder" }
	#The comma keeps an empty file (no resources among the -Projects, say) an empty array; without it the caller would get $null.
	return ,[string[]]@([System.IO.File]::ReadAllLines($path) | Where-Object { $_ })
}
function Compare-Sets([string]$what,[string[]]$older,[string[]]$newer,[bool]$isGate){
	$olderSet=[System.Collections.Generic.HashSet[string]]::new([string[]]$older,[StringComparer]::OrdinalIgnoreCase)
	$newerSet=[System.Collections.Generic.HashSet[string]]::new([string[]]$newer,[StringComparer]::OrdinalIgnoreCase)
	$gone=@($older | Where-Object { -not $newerSet.Contains($_) })
	$added=@($newer | Where-Object { -not $olderSet.Contains($_) })
	if($gone.Count -eq 0 -and $added.Count -eq 0){
		Write-Host "${what}: no differences"
		return 0
	}
	Write-Host ("{0}: {1} only in {2}, {3} only in {4}{5}" -f $what,$gone.Count,$Against,$added.Count,$Out,$(if($isGate){''}else{' (not a failure)'}))
	$gone | Select-Object -First 200 | ForEach-Object { Write-Host "  - $_" }
	$added | Select-Object -First 200 | ForEach-Object { Write-Host "  + $_" }
	if($isGate){ return 1 }
	return 0
}
foreach($name in @('items.txt','resources.txt','configs.txt','config-texts.txt')){
	if((Compare-Sets $name (Read-Lines $Against $name) (Read-Lines $Out $name) $true) -ne 0){ $exitCode=1 }
}
$olderFiles=Read-Lines $Against 'files.txt'
$newerFiles=Read-Lines $Out 'files.txt'
$namesOlder=@($olderFiles | ForEach-Object { ($_ -split '\|')[0..1] -join '|' })
$namesNewer=@($newerFiles | ForEach-Object { ($_ -split '\|')[0..1] -join '|' })
if((Compare-Sets 'files.txt (file names)' $namesOlder $namesNewer $true) -ne 0){ $exitCode=1 }
$namesBoth=[System.Collections.Generic.HashSet[string]]::new([string[]]$namesNewer,[StringComparer]::OrdinalIgnoreCase)
$namesBoth.IntersectWith([string[]]$namesOlder)
$olderBoth=@($olderFiles | Where-Object { $namesBoth.Contains((($_ -split '\|')[0..1] -join '|')) })
$newerBoth=@($newerFiles | Where-Object { $namesBoth.Contains((($_ -split '\|')[0..1] -join '|')) })
#A set, not Compare-Object, which refuses an empty list.
$olderBothSet=[System.Collections.Generic.HashSet[string]]::new([string[]]$olderBoth,[StringComparer]::OrdinalIgnoreCase)
$changed=@($newerBoth | Where-Object { -not $olderBothSet.Contains($_) } | ForEach-Object { ($_ -split '\|')[0..1] -join '|' })
if($changed.Count -eq 0){ Write-Host 'files.txt (sizes and SHA-256): no differences' }
else{
	Write-Host ("files.txt (sizes and SHA-256): {0} files differ (not a failure)" -f $changed.Count)
	$changed | Group-Object { [System.IO.Path]::GetExtension(($_ -split '\|')[1]).ToLowerInvariant() } | Sort-Object Name |
		ForEach-Object { Write-Host ("  {0}: {1}" -f $(if($_.Name){ $_.Name }else{ '(none)' }),$_.Count) }
}
if($exitCode -eq 0){ Write-Host 'The records match.' }else{ Write-Host 'The records differ.' }
exit $exitCode
#endregion Compare
