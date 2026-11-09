<#
.SYNOPSIS
Checks that the project-file evaluator of the tests (tests\ProjectFileTests\ProjectFile.cs) gives every property of every C# project of
OpenDental.Local.slnf the value MSBuild gives it. Stage 1 of the .NET 10 move (docs/specs/net10-plan.md, section 3): every stage gate runs it.

.DESCRIPTION
For each C# project of OpenDental.Local.slnf, with the configuration and platform OpenDental.sln builds it with for Debug|x86 (read from the
solution here, not through the evaluator), the script asks two evaluations for the same property names and compares their values:

  MSBuild    MSBuild 18 -getProperty (evaluation only: nothing is built, restored or written in the repository), with the global properties a
             solution build passes: Configuration, Platform, SolutionDir, SolutionPath, SolutionName, SolutionExt and SolutionFileName.
  Evaluator  ProjectFile.Load(path) and GetProperty(name) of ProjectFileTests.dll, called by reflection. The DLL is x86, so the script runs
             in the 32-bit Windows PowerShell (it starts itself there when it is started in a 64-bit one). It loads a copy of the DLL's folder
             from the temp folder, so the build output stays unlocked.

The names, for every project: the core names (the 33 of S1-T2's check), the extended list below (the names of that check beyond the core ones,
the names the evaluator lists as not modelled, the solution's global properties and more), every name the evaluator holds a value for, every
$(Name) the files it read for the project use, and every name a test asks GetProperty for. It also compares the core names for the solution's
Release|x86 mapping (Release|AnyCPU for the projects the solution builds as AnyCPU), unless -DebugOnly.

Each comparison is one of:
  equal                 the same text (compared exactly, case and slashes included)
  not-modelled          the evaluator throws NotSupportedException: a name it does not compute says so instead of guessing (any MSBuild value)
  mismatch              both have a value and they differ: a failure
  silent-empty          the evaluator gives '' where MSBuild has a value: a failure
  evaluator-error       the evaluator throws anything else, or cannot load the project: a failure
  msbuild-error         MSBuild cannot evaluate the project: a failure
Exit code 0 when there is no failure, 1 when there is at least one, 2 when the script could not run.

It writes three files into -Out: evaluator-check.csv (every comparison: project, configuration, platform, pass, name, kind, evaluator's value,
MSBuild's value), evaluator-findings.csv (the failures only) and evaluator-summary.txt (the counts and the first findings, as printed).

.PARAMETER Out
The folder the CSV files and the summary are written into (created if missing). Default: project-file-evaluator in the temp folder.

.PARAMETER Dll
ProjectFileTests.dll to check. Default: tests\ProjectFileTests\bin\Debug\ProjectFileTests.dll of -Root, which a full lane build writes.

.PARAMETER Build
Build tests\ProjectFileTests (Debug|x86, that project alone) with MSBuild 18 first. Without it the DLL must be there: the stage gates run this
script after their full lane build and do not pass it.

.PARAMETER Projects
Only these projects (file names without .csproj, comma-separated). Default: every C# project of the slnf.

.PARAMETER DebugOnly
Leave out the second pass, the core names for the solution's Release|x86 mapping.

.PARAMETER ThrottleLimit
How many MSBuild evaluations run at the same time. Default 4.

.PARAMETER MSBuildPath
MSBuild.exe to use. Default: the Build Tools 2026 MSBuild 18, found as tools\Initialize-BuildEnvironment.ps1 finds it.

.PARAMETER Root
The repository. Default: the folder above this script's folder.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-ProjectFileEvaluator.ps1 -Out $env:TEMP\evaluator-check
#>
[CmdletBinding(PositionalBinding=$false)]
param(
	[string]$Out='',
	[string]$Dll='',
	[switch]$Build,
	[string[]]$Projects=@(),
	[switch]$DebugOnly,
	[int]$ThrottleLimit=4,
	[string]$MSBuildPath='',
	[string]$Root=''
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0

#region 32-bit
#ProjectFileTests.dll is built for x86: a 64-bit process cannot load it. Start the same script in the 32-bit Windows PowerShell and pass its exit
#code on.
if([Environment]::Is64BitProcess){
	$powershell32=Join-Path $env:SystemRoot 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
	if(-not (Test-Path -LiteralPath $powershell32)){
		Write-Host "The 32-bit Windows PowerShell is not at $powershell32; it is needed to load the x86 ProjectFileTests.dll."
		exit 2
	}
	$argumentList=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath)
	foreach($key in $PSBoundParameters.Keys){
		$value=$PSBoundParameters[$key]
		if($value -is [System.Management.Automation.SwitchParameter]){
			if($value.IsPresent){ $argumentList+="-$key" }
			continue
		}
		if($value -is [array]){ $value=($value -join ',') }
		$argumentList+="-$key"
		$argumentList+=[string]$value
	}
	& $powershell32 @argumentList
	exit $LASTEXITCODE
}
#endregion 32-bit

try{
	if(-not $Root){ $Root=Split-Path -Parent $PSScriptRoot }
	$Root=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
	if(-not $Out){ $Out=Join-Path $env:TEMP 'project-file-evaluator' }
	New-Item -ItemType Directory -Force -Path $Out | Out-Null
	$Out=(Resolve-Path -LiteralPath $Out).Path
	$Projects=@($Projects | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
	try{ [System.Diagnostics.Process]::GetCurrentProcess().PriorityClass='BelowNormal' }catch{}

	. (Join-Path $PSScriptRoot 'Initialize-BuildEnvironment.ps1')
	$environment=Initialize-BuildEnvironment -RepoRoot $Root
	$msbuild=if($MSBuildPath){ $MSBuildPath }else{ Get-BuildToolPath -Tool MSBuild }
	if(-not $msbuild -or -not (Test-Path -LiteralPath $msbuild)){ throw 'MSBuild 18 was not found. Pass -MSBuildPath.' }
	Write-Host "MSBuild: $msbuild"
	Write-Host "SDK: $($environment.SdkVersion)"
}
catch{
	Write-Host "The check could not start: $($_.Exception.Message)"
	exit 2
}

#region Names
#The 33 names of S1-T2's check (the evaluator matched MSBuild for all of them in every project, Debug and Release).
$coreNames=@(
	'AssemblyName','RootNamespace','OutputType','PlatformTarget','TargetFramework','TargetFrameworkIdentifier','TargetFrameworkVersion',
	'TargetFrameworkMoniker','OutputPath','OutDir','IntermediateOutputPath','BaseIntermediateOutputPath','MSBuildProjectExtensionsPath',
	'DefineConstants','NoWarn','LangVersion','AutoGenerateBindingRedirects','GenerateBindingRedirectsOutputType','TargetName','TargetExt',
	'TargetFileName','Deterministic','DebugType','Optimize','WarningLevel','TreatWarningsAsErrors','AllowUnsafeBlocks','SignAssembly','UseWPF',
	'UseWindowsForms','EnableDefaultItems','EnableDefaultCompileItems','GenerateAssemblyInfo'
)
#Beyond them: the extended names of that check, the names the evaluator lists as not modelled, the solution's global properties, the switches
#and paths of the common props and targets, and the SDK's defaults.
$extendedNames=@(
	'TargetPath','TargetDir','ProjectDir','DebugSymbols','Prefer32Bit','WarningsAsErrors','ImplicitConfigurationDefine','AppDesignerFolder',
	'FileAlignment','ErrorReport','PlatformName','ConfigurationName','BaseOutputPath','TargetPlatformIdentifier','TargetPlatformVersion',
	'TargetPlatformMoniker','Language','DefaultLanguageSourceExtension','UsingMicrosoftNETSdk','ImplicitUsings','Nullable',
	'CheckForOverflowUnderflow','DelaySign','AssemblyOriginatorKeyFile','AppConfig','ApplicationManifest','ApplicationIcon','StartupObject',
	'ProjectGuid','Configurations','Platforms','AppendTargetFrameworkToOutputPath','AppendPlatformToOutputPath','EnableDefaultEmbeddedResourceItems',
	'EnableDefaultNoneItems','EnableDefaultPageItems','EnableDefaultApplicationDefinition','ProjectName','VisualStudioVersion','MSBuildToolsVersion',
	'GenerateDocumentationFile','ProduceReferenceAssembly','HasRuntimeOutput','SatelliteResourceLanguages','RestorePackagesWithLockFile',
	'EnableUnsafeBinaryFormatterSerialization','ImportWindowsDesktopTargets','DefaultXamlRuntime','TargetFrameworkProfile',
	'MicrosoftCommonPropsHasBeenImported','ProjectTypeGuids','EnableNETAnalyzers','DisableImplicitConfigurationDefines','ImportDirectoryBuildProps',
	'ImportDirectoryBuildTargets','PreBuildEvent','PostBuildEvent','RunPostBuildEvent','WarningsNotAsErrors','Utf8Output','NoStdLib','NoConfig',
	'GenerateSupportedRuntime','EnableSourceLink','AppendRuntimeIdentifierToOutputPath','ApplicationVersion','ApplicationRevision','Install',
	'UseVSHostingProcess','TargetFrameworkVersionWithoutV','_TargetFrameworkVersionWithoutV','IsPublishable','SelfContained','UseAppHost',
	'GenerateRuntimeConfigurationFiles','CopyLocalLockFileAssemblies','RunAnalyzers','Features','DefineDebug','DefineTrace','OutputFileName',
	'ProjectExt','ProjectFileName','ProjectPath','MSBuildProjectDirectory','DocumentationFile','RuntimeIdentifier','PublishDir','AnalysisLevel',
	'EmbedInteropTypes','RegisterForComInterop','BootstrapperEnabled','IsWebBootstrapper','PublishUrl','UpdateEnabled','MapFileExtensions',
	'PlatformTargetAsMSBuildArchitecture','NuGetPackageRoot','SolutionDir','DebugTypeOrNone','HighEntropyVA','SubsystemVersion','ErrorEndLocation',
	'ProjectAssetsFile','RestoreProjectStyle','DisableHandlePackageFileConflicts','ResolveAssemblyReferencesSilent','GenerateResourceUsePreserializedResources',
	'RuntimeIdentifiers','VersionPrefix','Version','Authors','Company','AssemblyTitle','Product','ProjectDepsFilePath','ProjectRuntimeConfigFilePath',
	'PackageOutputPath','NuGetTargetMoniker','EffectiveAnalysisLevel',
	'SolutionPath','SolutionName','SolutionExt','SolutionFileName','MSBuildProjectFullPath','MSBuildProjectFile','MSBuildProjectName',
	'MSBuildProjectExtension','MSBuildProjectDirectoryNoRoot','TargetRuntime','TargetingClr2Framework','ImportProjectExtensionProps',
	'ImportProjectExtensionTargets','_InitialMSBuildProjectExtensionsPath','_InitialBaseIntermediateOutputPath','DirectoryBuildPropsPath',
	'DirectoryBuildTargetsPath','_DirectoryBuildPropsFile','_DirectoryBuildPropsBasePath','_DirectoryBuildTargetsFile','_DirectoryBuildTargetsBasePath',
	'ProjectConfigFileName','PackRelease','PreferNativeArm64','TargetFrameworkAsMSBuildRuntime',
	'GeneratePackageOnBuild','PackageRequireLicenseAcceptance','UsingNETSdkDefaults','_IsExecutable','DefaultItemExcludes','DefaultExcludesInProjectFolder',
	'SetLinkMetadataAutomatically','RestoreSuccess','RestoreTool','NuGetPackageFolders','NuGetProjectStyle','NuGetToolVersion'
)
#The names a test asks ProjectFile's GetProperty for, from the tests' sources (those of tests\ProjectFileTests ask the fixtures' own names).
$testNames=New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach($file in Get-ChildItem -LiteralPath (Join-Path $Root 'tests') -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj|ProjectFileTests)\\' }){
	$text=[System.IO.File]::ReadAllText($file.FullName)
	foreach($match in [regex]::Matches($text,'(?:\b[Pp]rojectFile\w*|ProjectFile\.Load\([^;]*?\))\.GetProperty\("([A-Za-z_][\w.-]*)"\)')){
		[void]$testNames.Add($match.Groups[1].Value)
	}
}
#endregion Names

#region Projects and the solution's mapping
$slnf=Join-Path $Root 'OpenDental.Local.slnf'
$slnText=[System.IO.File]::ReadAllText((Join-Path $Root 'OpenDental.sln'))
$guidByPath=@{}
foreach($match in [regex]::Matches($slnText,'(?m)^Project\("\{[^}]+\}"\)\s*=\s*"[^"]*"\s*,\s*"([^"]+)"\s*,\s*"(\{[^}]+\})"')){
	$guidByPath[[System.IO.Path]::GetFullPath((Join-Path $Root $match.Groups[1].Value)).ToLowerInvariant()]=$match.Groups[2].Value.ToUpperInvariant()
}
function Get-SolutionMapping([string]$path,[string]$solutionConfiguration){
	$guid=$guidByPath[$path.ToLowerInvariant()]
	if($guid){
		$match=[regex]::Match($slnText,'(?m)^\s*'+[regex]::Escape($guid)+'\.'+[regex]::Escape($solutionConfiguration)+'\|x86\.ActiveCfg\s*=\s*([^|\r\n]+)\|([^\r\n]+?)\s*$',
			[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
		if($match.Success){
			$platform=$match.Groups[2].Value.Trim()
			if($platform -eq 'Any CPU'){ $platform='AnyCPU' }
			return @($match.Groups[1].Value.Trim(),$platform)
		}
	}
	return @($solutionConfiguration,'x86')
}
$slnfJson=[System.IO.File]::ReadAllText($slnf) | ConvertFrom-Json
$projectPaths=@()
$otherProjects=@()
foreach($relative in $slnfJson.solution.projects){
	$path=[System.IO.Path]::GetFullPath((Join-Path $Root ($relative -replace '/','\')))
	if($path -notmatch '\.csproj$'){ $otherProjects+=$relative; continue }
	if($Projects.Count -gt 0 -and $Projects -notcontains [System.IO.Path]::GetFileNameWithoutExtension($path)){ continue }
	$projectPaths+=$path
}
Write-Host ("{0} C# projects; not C# (the evaluator reads C# projects only): {1}" -f $projectPaths.Count,($otherProjects -join ', '))
#endregion Projects and the solution's mapping

#region The evaluator
try{
	if($Build){
		$csproj=Join-Path $Root 'tests\ProjectFileTests\ProjectFileTests.csproj'
		Write-Host "Building $csproj"
		& $msbuild $csproj -nologo -nr:false -v:minimal '-p:Configuration=Debug' '-p:Platform=x86' | Out-Host
		if($LASTEXITCODE -ne 0){ throw "MSBuild could not build $csproj (exit $LASTEXITCODE)." }
	}
	if(-not $Dll){ $Dll=Join-Path $Root 'tests\ProjectFileTests\bin\Debug\ProjectFileTests.dll' }
	if(-not (Test-Path -LiteralPath $Dll -PathType Leaf)){ throw "No $Dll. Build tests\ProjectFileTests through the build lane (or pass -Build)." }
	$Dll=(Resolve-Path -LiteralPath $Dll).Path
	#A copy of the DLL's folder, so the build output is not locked while this process runs. The copy stays loaded until the process ends, so
	#it cannot be deleted at the end; the copies of earlier runs (more than an hour old, so no run still uses them) are deleted here.
	Get-ChildItem -LiteralPath $env:TEMP -Directory -Filter 'Test-ProjectFileEvaluator_*' -ErrorAction SilentlyContinue |
		Where-Object { $_.LastWriteTime -lt (Get-Date).AddHours(-1) } |
		ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
	$copyFolder=Join-Path $env:TEMP ('Test-ProjectFileEvaluator_'+[guid]::NewGuid().ToString('N'))
	New-Item -ItemType Directory -Force -Path $copyFolder | Out-Null
	Get-ChildItem -LiteralPath (Split-Path -Parent $Dll) -File | Where-Object { $_.Extension -in @('.dll','.pdb','.config','.exe') } |
		ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $copyFolder }
	$assembly=[System.Reflection.Assembly]::LoadFrom((Join-Path $copyFolder (Split-Path -Leaf $Dll)))
	$type=$assembly.GetType('ProjectFileTests.ProjectFile',$true)
	$flags=[System.Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
	$methodLoad1=$type.GetMethods($flags) | Where-Object { $_.Name -eq 'Load' -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
	$methodLoad3=$type.GetMethods($flags) | Where-Object { $_.Name -eq 'Load' -and $_.GetParameters().Count -eq 3 } | Select-Object -First 1
	$methodGet=$type.GetMethod('GetProperty',$flags)
	$methodSlnf=$type.GetMethod('GetSolutionFilterProjects',$flags)
	$fieldProperties=$type.GetField('_dictProperties',$flags)
	$propertyConfiguration=$type.GetProperty('Configuration',$flags)
	$propertyPlatform=$type.GetProperty('Platform',$flags)
	$propertyFilesRead=$type.GetProperty('FilesRead',$flags)
	foreach($member in @($methodLoad1,$methodLoad3,$methodGet,$methodSlnf,$fieldProperties,$propertyConfiguration,$propertyPlatform,$propertyFilesRead)){
		if($member -eq $null){ throw "ProjectFileTests.ProjectFile in $Dll lacks a member this script calls." }
	}
}
catch{
	Write-Host "The check could not start: $($_.Exception.Message)"
	exit 2
}
#The evaluator's own list of the slnf's C# projects must be this script's.
$evaluatorProjects=@($methodSlnf.Invoke($null,[object[]]@([string]$slnf)) | ForEach-Object { $_.ToLowerInvariant() })
$listMismatchedProjectLists=@()
foreach($path in $projectPaths){ if($evaluatorProjects -notcontains $path.ToLowerInvariant()){ $listMismatchedProjectLists+=$path } }

function Get-InnerException($errorRecord){
	$exception=$errorRecord.Exception
	while(($exception -is [System.Reflection.TargetInvocationException] -or $exception -is [System.Management.Automation.MethodInvocationException]) -and $exception.InnerException -ne $null){
		$exception=$exception.InnerException
	}
	return $exception
}
function Read-Evaluator($projectFile,[string[]]$names){
	$values=@{}
	foreach($name in $names){
		try{
			$values[$name]=[pscustomobject]@{ Value=[string]$methodGet.Invoke($projectFile,[object[]]@([string]$name)); Exception=$null; Message=$null }
		}
		catch{
			$exception=Get-InnerException $_
			$values[$name]=[pscustomobject]@{ Value=$null; Exception=$exception.GetType().FullName; Message=$exception.Message }
		}
	}
	return $values
}
#endregion The evaluator

#region Evaluate
$passes=New-Object System.Collections.ArrayList
foreach($path in $projectPaths){
	$pass=[pscustomobject]@{ Project=$path; Relative=$path.Substring($Root.Length+1); Pass='Debug'; Configuration=''; Platform=''; Names=@(); Evaluator=$null; LoadError=$null; MSBuild=$null; MSBuildError=$null }
	$mapping=Get-SolutionMapping $path 'Debug'
	$pass.Configuration=$mapping[0]
	$pass.Platform=$mapping[1]
	$names=New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::OrdinalIgnoreCase)
	foreach($name in $coreNames+$extendedNames){ [void]$names.Add($name) }
	foreach($name in $testNames){ [void]$names.Add($name) }
	try{
		$projectFile=$methodLoad1.Invoke($null,[object[]]@([string]$path))
		$evaluatorConfiguration=[string]$propertyConfiguration.GetValue($projectFile)+'|'+[string]$propertyPlatform.GetValue($projectFile)
		if($evaluatorConfiguration -ne $pass.Configuration+'|'+$pass.Platform){
			$pass.LoadError="The evaluator loads it as $evaluatorConfiguration, the solution maps $($pass.Configuration)|$($pass.Platform)."
		}
		foreach($key in $fieldProperties.GetValue($projectFile).Keys){ [void]$names.Add([string]$key) }
		foreach($file in $propertyFilesRead.GetValue($projectFile)){
			foreach($match in [regex]::Matches([System.IO.File]::ReadAllText($file),'\$\(([A-Za-z_][\w-]*)')){ [void]$names.Add($match.Groups[1].Value) }
		}
		$pass.Names=@($names)
		$pass.Evaluator=Read-Evaluator $projectFile $pass.Names
	}
	catch{
		$exception=Get-InnerException $_
		$pass.LoadError="$($exception.GetType().FullName): $($exception.Message)"
		$pass.Names=@($names)
	}
	[void]$passes.Add($pass)
	if(-not $DebugOnly){
		$passRelease=[pscustomobject]@{ Project=$path; Relative=$pass.Relative; Pass='Release'; Configuration=''; Platform=''; Names=$coreNames; Evaluator=$null; LoadError=$null; MSBuild=$null; MSBuildError=$null }
		$mapping=Get-SolutionMapping $path 'Release'
		$passRelease.Configuration=$mapping[0]
		$passRelease.Platform=$mapping[1]
		try{
			$projectFile=$methodLoad3.Invoke($null,[object[]]@([string]$path,[string]$passRelease.Configuration,[string]$passRelease.Platform))
			$passRelease.Evaluator=Read-Evaluator $projectFile $coreNames
		}
		catch{
			$exception=Get-InnerException $_
			$passRelease.LoadError="$($exception.GetType().FullName): $($exception.Message)"
		}
		[void]$passes.Add($passRelease)
	}
}
Write-Host "Evaluator: $($passes.Count) evaluations"

#MSBuild, a few at a time: one response file and one output file per evaluation, in a scratch folder in the temp folder.
$scratch=Join-Path $env:TEMP ('Test-ProjectFileEvaluator_msbuild_'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$globals=@("-p:SolutionDir=$Root\","-p:SolutionPath=$Root\OpenDental.sln",'-p:SolutionName=OpenDental','-p:SolutionExt=.sln','-p:SolutionFileName=OpenDental.sln')
#A line of a response file: quoted only when it holds a space, and then a back slash before the closing quote doubled, so it stays a back slash.
function Get-ResponseArgument([string]$text){
	if($text -notmatch '\s'){ return $text }
	if($text.EndsWith('\')){ $text+='\' }
	return '"'+$text+'"'
}
$running=New-Object System.Collections.ArrayList
$index=0
$started=Get-Date
foreach($pass in $passes){
	$index++
	$pass | Add-Member -NotePropertyName OutputFile -NotePropertyValue (Join-Path $scratch "$index.out.txt")
	$pass | Add-Member -NotePropertyName ErrorFile -NotePropertyValue (Join-Path $scratch "$index.err.txt")
	$responseFile=Join-Path $scratch "$index.rsp"
	#One name more than asked: with a single name MSBuild prints the bare value instead of JSON.
	$lines=@((Get-ResponseArgument $pass.Project),'-nologo','-nr:false',('-getProperty:'+((@($pass.Names)+'MSBuildProjectName') -join ',')),
		(Get-ResponseArgument "-p:Configuration=$($pass.Configuration)"),(Get-ResponseArgument "-p:Platform=$($pass.Platform)"))+($globals | ForEach-Object { Get-ResponseArgument $_ })
	[System.IO.File]::WriteAllLines($responseFile,[string[]]$lines,(New-Object System.Text.UTF8Encoding($false)))
	while($running.Count -ge [Math]::Max(1,$ThrottleLimit)){
		foreach($process in @($running)){ if($process.HasExited){ [void]$running.Remove($process) } }
		if($running.Count -ge [Math]::Max(1,$ThrottleLimit)){ [void]$running[0].WaitForExit(200) }
	}
	$process=Start-Process -FilePath $msbuild -ArgumentList ('"@'+$responseFile+'"') -NoNewWindow -PassThru -RedirectStandardOutput $pass.OutputFile -RedirectStandardError $pass.ErrorFile
	#Without the handle taken now, ExitCode stays empty once the process has ended.
	$null=$process.Handle
	$pass | Add-Member -NotePropertyName Process -NotePropertyValue $process
	[void]$running.Add($process)
}
foreach($process in @($running)){ $process.WaitForExit() }
Write-Host ("MSBuild: {0} evaluations in {1:N0} s" -f $passes.Count,((Get-Date)-$started).TotalSeconds)
foreach($pass in $passes){
	$text=if(Test-Path -LiteralPath $pass.OutputFile){ [System.IO.File]::ReadAllText($pass.OutputFile) }else{ '' }
	$start=$text.IndexOf('{')
	if($pass.Process.ExitCode -ne 0 -or $start -lt 0){
		$pass.MSBuildError="exit $($pass.Process.ExitCode): "+(($text+' '+[System.IO.File]::ReadAllText($pass.ErrorFile)).Trim() -replace '\s+',' ')
		continue
	}
	$pass.MSBuild=($text.Substring($start) | ConvertFrom-Json).Properties
}
Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
#endregion Evaluate

#region Compare
$rows=New-Object System.Collections.ArrayList
foreach($pass in $passes){
	$base=@{ Project=$pass.Relative; Configuration=$pass.Configuration; Platform=$pass.Platform; Pass=$pass.Pass }
	if($pass.MSBuildError){
		[void]$rows.Add([pscustomobject]($base+@{ Name='*'; Kind='msbuild-error'; Evaluator=''; MSBuild=$pass.MSBuildError }))
		continue
	}
	if($pass.LoadError){
		[void]$rows.Add([pscustomobject]($base+@{ Name='*'; Kind='evaluator-error'; Evaluator=$pass.LoadError; MSBuild='' }))
		if($pass.Evaluator -eq $null){ continue }
	}
	foreach($name in $pass.Names){
		$valueMSBuild=''
		$property=$pass.MSBuild.PSObject.Properties[$name]
		if($property -ne $null -and $property.Value -ne $null){ $valueMSBuild=[string]$property.Value }
		$read=$pass.Evaluator[$name]
		if($read.Exception){
			$kind=if($read.Exception -eq 'System.NotSupportedException'){ 'not-modelled' }else{ 'evaluator-error' }
			[void]$rows.Add([pscustomobject]($base+@{ Name=$name; Kind=$kind; Evaluator="$($read.Exception): $($read.Message)"; MSBuild=$valueMSBuild }))
			continue
		}
		$kind='equal'
		if($read.Value -cne $valueMSBuild){
			$kind=if($read.Value -eq ''){ 'silent-empty' }else{ 'mismatch' }
		}
		[void]$rows.Add([pscustomobject]($base+@{ Name=$name; Kind=$kind; Evaluator=$read.Value; MSBuild=$valueMSBuild }))
	}
}
foreach($path in $listMismatchedProjectLists){
	[void]$rows.Add([pscustomobject]@{ Project=$path.Substring($Root.Length+1); Configuration=''; Platform=''; Pass='list'; Name='*'; Kind='evaluator-error';
		Evaluator='GetSolutionFilterProjects does not list it'; MSBuild='' })
}
$failureKinds=@('mismatch','silent-empty','evaluator-error','msbuild-error')
$columns=@('Project','Configuration','Platform','Pass','Name','Kind','Evaluator','MSBuild')
$rows | Select-Object $columns | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $Out 'evaluator-check.csv')
$findings=@($rows | Where-Object { $failureKinds -contains $_.Kind })
$findings | Select-Object $columns | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $Out 'evaluator-findings.csv')
$summary=New-Object System.Collections.Generic.List[string]
$summary.Add(("{0} projects, {1} evaluations, {2} comparisons ({3} names in all)" -f $projectPaths.Count,$passes.Count,$rows.Count,
	(@($rows | ForEach-Object { $_.Name } | Sort-Object -Unique)).Count))
foreach($group in $rows | Group-Object Pass,Kind | Sort-Object Name){ $summary.Add(("{0,7}  {1}" -f $group.Count,$group.Name)) }
foreach($kind in $failureKinds){ $summary.Add(("{0}: {1}" -f $kind,@($findings | Where-Object { $_.Kind -eq $kind }).Count)) }
foreach($group in $findings | Group-Object Kind,Name | Sort-Object Name | Select-Object -First 60){
	$first=$group.Group[0]
	$summary.Add(("  {0} x{1}: {2} evaluator='{3}' msbuild='{4}'" -f $group.Name,$group.Count,$first.Project,$first.Evaluator,$first.MSBuild))
}
$summary.Add($(if($findings.Count -eq 0){ 'The evaluator agrees with MSBuild.' }else{ 'The evaluator differs from MSBuild.' }))
[System.IO.File]::WriteAllLines((Join-Path $Out 'evaluator-summary.txt'),$summary.ToArray(),(New-Object System.Text.UTF8Encoding($false)))
$summary | ForEach-Object { Write-Host $_ }
Write-Host "Written: $(Join-Path $Out 'evaluator-check.csv'), evaluator-findings.csv and evaluator-summary.txt there."
Remove-Item -LiteralPath $copyFolder -Recurse -Force -ErrorAction SilentlyContinue
if($findings.Count -gt 0){ exit 1 }
exit 0
#endregion Compare
