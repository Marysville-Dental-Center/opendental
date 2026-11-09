<#
.SYNOPSIS
Converts the classic test projects to SDK-style net48 project files on top of tests\Directory.Build.props (package S1-6 of stage 1 of the
.NET 10 move, docs/specs/net10-plan.md; the port note is docs/port-notes/S1-6.md).

.DESCRIPTION
For each classic test project (tests\*\*.csproj, tools\*\*.Tests\*.csproj and UnitTests\UnitTests.csproj by default) the script writes a
new project file in its place: <Project Sdk="Microsoft.NET.Sdk">, with only what differs from the SDK's defaults and from what
tests\Directory.Build.props sets for every test project (net48, x86, LangVersion 8.0, DebugType, the binding redirects, MSTest v1, no
None items from the globs, no transitive project references). It keeps:

  - the comments of the project file (the description, and the comments in front of every item or property it keeps);
  - OutputType, RootNamespace and AssemblyName when they differ from the defaults, LangVersion when it is not 8.0, ApplicationManifest,
    Platforms AnyCPU;x86 for a project the solution also builds as AnyCPU, and DisableImplicitConfigurationDefines for a project whose
    Debug configuration defined no DEBUG symbol;
  - every Reference with a HintPath or other metadata, and every framework Reference the SDK does not add itself; the Mono.Cecil
    HintPath references become one PackageReference to Mono.Cecil 0.10.3 (from the offline package folder, nuget.config);
  - every ProjectReference (without the Project and Name metadata, which only classic projects use);
  - the Compile items from outside the project's folder (the linked TestDb.cs, ProjectFile.cs and BuildOutput.cs), and an Update item
    for every Compile or EmbeddedResource item in the folder that carries metadata other than SubType (DependentUpon, AutoGen, ...);
  - every None and Content item except packages.config, which is deleted with it.

It drops the **\*.cs item and every other Compile and EmbeddedResource item that the SDK's default globs give (NETSDK1022 otherwise),
SubType metadata, ProjectGuid and the other properties only Visual Studio's classic project system reads, the ClickOnce and bootstrapper
settings, the LinuxRelease and AnyCPU configurations, and the settings that equal the SDK's or the shared files' values. Anything it does
not know stops it with an error before it writes the project: a property with another value than expected, an item type, item metadata,
a .cs or .resx file under the folder that the classic project does not compile or embed (the glob would add it).

A project outside tests\ (UnitTests, the two tools tests) names tests\Directory.Build.props as its Directory.Build.props
(DirectoryBuildPropsPath, set before an explicit Sdk.props import): that file imports the repository's own, so each is imported once.

Run once by S1-6. It stays for the rest of stage 1, in case a package brings a classic test project file in a merge (convert that file as
it is on its branch, then compare: docs/port-notes/S1-6.md); it can be deleted after the stage 1 gate.

.PARAMETER Projects
Project files to convert (paths relative to -Root or absolute, comma-separated). Default: every classic test project named above.

.PARAMETER Check
Write nothing: print each project's new text instead.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Convert-TestProjectsToSdk.ps1
#>
[CmdletBinding(PositionalBinding=$false)]
param(
	[string[]]$Projects=@(),
	[switch]$Check,
	[string]$Root=''
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
if(-not $Root){ $Root=Split-Path -Parent $PSScriptRoot }
$Root=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
$Projects=@($Projects | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if($Projects.Count -eq 0){
	$Projects=@(Get-ChildItem -Path (Join-Path $Root 'tests') -Directory | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File } | ForEach-Object { $_.FullName })
	$Projects+=@(Get-ChildItem -Path (Join-Path $Root 'tools') -Directory | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Filter '*.Tests' } |
		ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File } | ForEach-Object { $_.FullName })
	$Projects+=@(Join-Path $Root 'UnitTests\UnitTests.csproj')
}
$msbuildNamespace='http://schemas.microsoft.com/developer/msbuild/2003'

#The framework assemblies Microsoft.NET.Sdk references by itself for .NET Framework 4.x (Microsoft.NET.Sdk.BeforeCommon.targets,
#_SDKImplicitReference): a plain Reference to one of them adds nothing.
$sdkImplicitReferences=@('System','System.Data','System.Drawing','System.Xml','System.Core','System.Runtime.Serialization','System.Xml.Linq',
	'System.Numerics','System.IO.Compression.FileSystem')
#Properties only Visual Studio's classic project system, ClickOnce or the classic targets read, or that equal what the SDK, the repository's
#Directory.Build.props or tests\Directory.Build.props set: dropped, whatever their value.
$droppedProperties=@('Configuration','Platform','ProjectGuid','ProductVersion','SchemaVersion','FileUpgradeFlags','OldToolsVersion','UpgradeBackupLocation',
	'TargetFrameworkProfile','ProjectTypeGuids','IsCodedUITest','TestProjectType','PublishUrl','Install','InstallFrom','UpdateEnabled','UpdateMode',
	'UpdateInterval','UpdateIntervalUnits','UpdatePeriodically','UpdateRequired','MapFileExtensions','ApplicationRevision','ApplicationVersion',
	'IsWebBootstrapper','UseApplicationTrust','BootstrapperEnabled','ResGenToolArchitecture')
#Properties dropped only with this value (the SDK's or a shared file's); another value stops the script.
$expectedProperties=@{
	'TargetFrameworkVersion'='v4.8'; 'AppDesignerFolder'='Properties'; 'FileAlignment'='512'; 'AutoGenerateBindingRedirects'='true';
	'GenerateBindingRedirectsOutputType'='true'; 'PlatformTarget'='x86'; 'ErrorReport'='prompt'; 'WarningLevel'='4'; 'Prefer32Bit'='false'
}
$expectedDebug=@{ 'DebugSymbols'='true'; 'DebugType'='full'; 'Optimize'='false'; 'OutputPath'='bin\Debug\' }
$expectedRelease=@{ 'DebugType'='pdbonly'; 'Optimize'='true'; 'OutputPath'='bin\Release\' }
#Comment texts that the conversion makes untrue, and what replaces them (the rest of the comment stays as it is).
$commentEdits=@(
	@('Compile\s+items\s+are\s+listed\s+one\s+by\s+one\s+until\s+S1-6\s+converts\s+this\s+file\.','Every .cs file under this folder is compiled.'),
	@('The NuGet package UnitTests restores \(UnitTests\\packages\.config\)\.','The NuGet package, from the offline package folder (nuget.config).'),
	@('The NuGet package this project''s packages\.config\s+names, as UnitTests\\packages\.config does, so a restore fills packages\\Mono\.Cecil\.0\.10\.3 for this project on its own; the build lane\s+copies the folder into a new worktree\.',
		"The NuGet package, from the offline package`n         folder (nuget.config).")
)
#The comment Visual Studio's templates put at the end of a classic project.
$templateComment='To modify your build process, add your task inside one of the targets below and uncomment it.'
#The comment of the Mono.Cecil package when the classic project had none for its references.
$cecilComment=" Mono.Cecil 0.10.3 (Mono.Cecil, Mono.Cecil.Mdb, Mono.Cecil.Pdb, Mono.Cecil.Rocks): the NuGet package, from the offline package`n         folder (nuget.config). "
#The comment in front of Platforms (a project the solution also builds as Any CPU).
$platformsComment=" The solution also builds this project as Any CPU; it compiles for x86 all the same (PlatformTarget, the repository's`n         Directory.Build.props). "
#The order of the properties in the new file.
$propertyOrder=@('OutputType','RootNamespace','AssemblyName','LangVersion','ApplicationManifest','Platforms')
#A description for a project whose classic file had none (by project file name).
$descriptions=@{
	'UnitTests'=@(
		'',
		'    UnitTests: the legacy test program (Unit Tests.exe, a WinExe with its own test window, FormUnitTests), kept from the code base this',
		'    project started from (S0-5 of docs/specs/net10-plan.md); the test runner runs it with vstest like the test libraries. The SDK''s default',
		'    globs compile every .cs file and embed every .resx file under this folder (bin\ and obj\ excepted), so only the items with their own',
		'    metadata are listed: the designer files and the .resx files of the forms (DependentUpon), the generated resource and settings files,',
		'    and the None and Content items of the classic project (tests\Directory.Build.props turns the None glob off, as for every test project).',
		'  ')
	'CryptoTests'=@(
		'',
		'    CryptoTests: unit tests for CodeBase.Secrets, CodeBase.TamperHash and ODCrypt. No database needed. Every .cs file under this folder is',
		'    compiled.',
		'  ')
}

function Get-Escaped([string]$text){
	return $text.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;').Replace('"','&quot;')
}

function Get-CommentText([System.Xml.XmlNode]$comment){
	$text=$comment.Value
	foreach($edit in $commentEdits){ $text=[regex]::Replace($text,$edit[0],$edit[1]) }
	return $text
}

#The comments directly in front of a node (whitespace between them does not count).
function Get-LeadingComments([System.Xml.XmlNode]$node){
	$list=New-Object 'System.Collections.Generic.List[System.Xml.XmlNode]'
	$previous=$node.PreviousSibling
	while($previous -ne $null -and ($previous.NodeType -eq 'Comment' -or $previous.NodeType -eq 'Whitespace')){
		if($previous.NodeType -eq 'Comment'){ $list.Insert(0,$previous) }
		$previous=$previous.PreviousSibling
	}
	return ,$list
}

function Get-Kind([string]$condition){
	$c=($condition -replace '\s','')
	switch -regex ($c){
		'^$' { return 'All' }
		"^'\$\(Configuration\)\|\$\(Platform\)'=='Debug\|x86'$" { return 'Debug' }
		"^'\$\(Configuration\)\|\$\(Platform\)'=='Release\|x86'$" { return 'Release' }
		"^'\$\(Configuration\)'=='Debug'$" { return 'Debug' }
		"^'\$\(Configuration\)'!='Debug'$" { return 'Release' }
		#Configurations nothing builds any more, or that the SDK's flat output folder replaces.
		"^'\$\(Configuration\)\|\$\(Platform\)'=='(Debug|Release|LinuxRelease)\|AnyCPU'$" { return 'AnyCPU' }
		"^'\$\(Configuration\)\|\$\(Platform\)'=='LinuxRelease\|x86'$" { return 'Dropped' }
		"^'\$\(Platform\)'(==|!=)'AnyCPU'$" { return 'AnyCPU' }
	}
	throw "Unknown PropertyGroup condition: $condition"
}

function Convert-Project([string]$path){
	$path=(Resolve-Path -LiteralPath $path).Path
	$folder=Split-Path -Parent $path
	$name=[System.IO.Path]::GetFileNameWithoutExtension($path)
	$relativePath=$path.Substring($Root.Length+1)
	$document=New-Object System.Xml.XmlDocument
	$document.PreserveWhitespace=$true
	$document.Load($path)
	$project=$document.DocumentElement
	if($project.HasAttribute('Sdk') -or $project.NamespaceURI -ne $msbuildNamespace){
		Write-Host "$relativePath is not a classic project file; skipped."
		return
	}
	$notes=New-Object 'System.Collections.Generic.List[string]'
	$outsideTests=-not $relativePath.StartsWith('tests\',[StringComparison]::OrdinalIgnoreCase)

	#region Properties
	$values=@{ 'All'=@{}; 'Debug'=@{}; 'Release'=@{} }
	$propertyComments=@{}
	$hasAnyCpu=$false
	foreach($group in @($project.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.LocalName -eq 'PropertyGroup' })){
		$kind=Get-Kind $group.GetAttribute('Condition')
		if($kind -eq 'AnyCPU'){ $hasAnyCpu=$true }
		foreach($property in @($group.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })){
			$propertyName=$property.LocalName
			$value=$property.InnerText.Trim()
			if($kind -eq 'AnyCPU' -or $kind -eq 'Dropped'){ continue }
			if($propertyName -eq 'Platform' -and $value -eq 'AnyCPU'){ $hasAnyCpu=$true }
			$comments=Get-LeadingComments $property
			if($comments.Count -gt 0){ $propertyComments[$propertyName]=$comments }
			$values[$kind][$propertyName]=$value
		}
	}
	$emit=New-Object 'System.Collections.Generic.List[object]'
	$all=$values['All']
	foreach($propertyName in @($all.Keys)){
		$value=$all[$propertyName]
		if($droppedProperties -contains $propertyName){ continue }
		if($expectedProperties.ContainsKey($propertyName)){
			if($value -ne $expectedProperties[$propertyName]){ throw "${relativePath}: $propertyName is '$value', not '$($expectedProperties[$propertyName])'." }
			continue
		}
		switch($propertyName){
			'OutputType' { if($value -ne 'Library'){ $emit.Add(@($propertyName,$value)) } }
			'RootNamespace' { if($value -ne $name){ $emit.Add(@($propertyName,$value)) } }
			'AssemblyName' { if($value -ne $name){ $emit.Add(@($propertyName,$value)) } }
			'LangVersion' { }
			'ApplicationManifest' { $emit.Add(@($propertyName,$value)) }
			default { throw "${relativePath}: the property $propertyName ('$value') is not known to this script." }
		}
	}
	#LangVersion: the project's, or that of its x86 configurations (UnitTests sets it there only); 8.0 is tests\Directory.Build.props'.
	$langVersion=if($all.ContainsKey('LangVersion')){ $all['LangVersion'] }elseif($values['Debug'].ContainsKey('LangVersion')){ $values['Debug']['LangVersion'] }else{ '' }
	foreach($kind in @('Debug','Release')){
		if($values[$kind].ContainsKey('LangVersion') -and $langVersion -and $values[$kind]['LangVersion'] -ne $langVersion){ throw "${relativePath}: LangVersion differs between configurations." }
	}
	if(-not $langVersion){ throw "${relativePath}: no LangVersion; the default of a classic project is the compiler's latest, which the SDK does not give net48." }
	if($langVersion -ne '8.0'){ $emit.Insert(0,@('LangVersion',$langVersion)) }
	$defines=@{}
	foreach($kind in @('Debug','Release')){
		$expected=if($kind -eq 'Debug'){ $expectedDebug }else{ $expectedRelease }
		foreach($propertyName in @($values[$kind].Keys)){
			$value=$values[$kind][$propertyName]
			if($propertyName -eq 'DefineConstants'){ $defines[$kind]=$value; continue }
			if($propertyName -eq 'LangVersion'){ continue }
			if($expected.ContainsKey($propertyName)){
				if($value -ne $expected[$propertyName]){ throw "${relativePath}: $kind $propertyName is '$value', not '$($expected[$propertyName])'." }
				continue
			}
			if($expectedProperties.ContainsKey($propertyName)){
				if($value -ne $expectedProperties[$propertyName]){ throw "${relativePath}: $kind $propertyName is '$value', not '$($expectedProperties[$propertyName])'." }
				continue
			}
			throw "${relativePath}: the $kind property $propertyName ('$value') is not known to this script."
		}
		foreach($propertyName in @($expected.Keys)){
			#A classic project without the property gets the classic default, which equals the SDK's for these (Optimize off, symbols for Debug).
			if(-not $values[$kind].ContainsKey($propertyName) -and $propertyName -eq 'DebugType'){ throw "${relativePath}: no $kind DebugType." }
		}
	}
	$debugDefines=if($defines.ContainsKey('Debug')){ $defines['Debug'] }else{ '' }
	$releaseDefines=if($defines.ContainsKey('Release')){ $defines['Release'] }else{ '' }
	$disableConfigurationDefines=$false
	if($releaseDefines -ne 'TRACE'){ throw "${relativePath}: Release DefineConstants '$releaseDefines'." }
	if($debugDefines -eq 'TRACE'){ $disableConfigurationDefines=$true }
	elseif($debugDefines -ne 'DEBUG;TRACE'){ throw "${relativePath}: Debug DefineConstants '$debugDefines'." }
	if($hasAnyCpu){ $emit.Add(@('Platforms','AnyCPU;x86')) }
	#endregion Properties

	#region Items
	$itemText=New-Object 'System.Collections.Generic.List[string]'
	$compiled=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
	$embedded=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
	$hasGlob=$false
	$usesCecil=$false
	foreach($group in @($project.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.LocalName -eq 'ItemGroup' })){
		if($group.GetAttribute('Condition')){ throw "${relativePath}: an ItemGroup with a condition." }
		$lines=New-Object 'System.Collections.Generic.List[string]'
		foreach($item in @($group.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })){
			$type=$item.LocalName
			$include=$item.GetAttribute('Include')
			foreach($attribute in @($item.Attributes)){
				if($attribute.Name -ne 'Include' -and -not ($type -eq 'Compile' -and $attribute.Name -eq 'Exclude')){ throw "${relativePath}: $type $include has the attribute $($attribute.Name)." }
			}
			$metadata=New-Object 'System.Collections.Generic.List[object]'
			foreach($child in @($item.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })){ $metadata.Add(@($child.LocalName,$child.InnerText.Trim())) }
			$comments=Get-LeadingComments $item
			$keep=$null
			switch($type){
				'Reference' {
					$simpleName=($include -split ',')[0].Trim()
					$known=@($metadata | ForEach-Object { $_[0] } | Where-Object { @('HintPath','SpecificVersion','Private') -notcontains $_ })
					if($known.Count -gt 0){ throw "${relativePath}: Reference $include has the metadata $($known -join ', ')." }
					if($simpleName -eq 'Microsoft.VisualStudio.QualityTools.UnitTestFramework'){ break }
					if($simpleName -like 'Mono.Cecil*'){
						if(-not $usesCecil){
							$usesCecil=$true
							$keep=@('PackageReference','Mono.Cecil',@(,@('Version','0.10.3')),'attribute')
							if($comments.Count -eq 0){
								$comments=New-Object 'System.Collections.Generic.List[System.Xml.XmlNode]'
								$comments.Add($document.CreateComment($cecilComment))
							}
						}
						elseif($comments.Count -gt 0){ throw "${relativePath}: a comment in front of a second Mono.Cecil reference." }
						break
					}
					if($metadata.Count -eq 0 -and $sdkImplicitReferences -contains $include){ break }
					$keep=@('Reference',$include,$metadata,'element')
				}
				'ProjectReference' {
					$known=@($metadata | ForEach-Object { $_[0] } | Where-Object { @('Project','Name') -notcontains $_ })
					if($known.Count -gt 0){ throw "${relativePath}: ProjectReference $include has the metadata $($known -join ', ')." }
					$keep=@('ProjectReference',$include,@(),'element')
				}
				'Compile' {
					if($include -eq '**\*.cs'){
						if($item.GetAttribute('Exclude') -ne 'bin\**;obj\**'){ throw "${relativePath}: the **\*.cs item excludes '$($item.GetAttribute('Exclude'))'." }
						if($metadata.Count -gt 0){ throw "${relativePath}: the **\*.cs item has metadata." }
						$hasGlob=$true
						break
					}
					if($item.HasAttribute('Exclude')){ throw "${relativePath}: Compile $include has an Exclude." }
					$own=@($metadata | Where-Object { $_[0] -ne 'SubType' })
					if($include.StartsWith('..\')){
						$keep=@('Compile',$include,$own,'element')
						break
					}
					if(-not (Test-Path -LiteralPath (Join-Path $folder $include) -PathType Leaf)){ throw "${relativePath}: Compile $include is not there." }
					[void]$compiled.Add($include)
					if($own.Count -gt 0){ $keep=@('Compile',$include,$own,'update') }
				}
				'EmbeddedResource' {
					$own=@($metadata | Where-Object { $_[0] -ne 'SubType' })
					if($include.StartsWith('..\') -or -not $include.EndsWith('.resx',[StringComparison]::OrdinalIgnoreCase)){ throw "${relativePath}: EmbeddedResource $include is not a .resx file of the folder." }
					if(-not (Test-Path -LiteralPath (Join-Path $folder $include) -PathType Leaf)){ throw "${relativePath}: EmbeddedResource $include is not there." }
					[void]$embedded.Add($include)
					if($own.Count -gt 0){ $keep=@('EmbeddedResource',$include,$own,'update') }
				}
				{ $_ -eq 'None' -or $_ -eq 'Content' } {
					if($include -eq 'packages.config'){ break }
					$own=@($metadata | Where-Object { $_[0] -ne 'SubType' })
					$keep=@($type,$include,$own,'element')
				}
				{ $_ -eq 'BootstrapperPackage' -or $_ -eq 'Service' } { break }
				default { throw "${relativePath}: the item type $type is not known to this script." }
			}
			if($null -eq $keep){
				foreach($comment in $comments){ $notes.Add("dropped with $type ${include}: <!--$($comment.Value)-->") }
				continue
			}
			foreach($comment in $comments){ $lines.Add('    <!--'+(Get-CommentText $comment)+'-->') }
			$itemType=$keep[0]
			$attributeName=if($keep[3] -eq 'update'){ 'Update' }else{ 'Include' }
			if($keep[3] -eq 'attribute'){
				$text='    <'+$itemType+' Include="'+(Get-Escaped $keep[1])+'"'
				foreach($pair in $keep[2]){ $text+=' '+$pair[0]+'="'+(Get-Escaped $pair[1])+'"' }
				$lines.Add($text+' />')
			}
			elseif($keep[2].Count -eq 0){
				$lines.Add('    <'+$itemType+' '+$attributeName+'="'+(Get-Escaped $keep[1])+'" />')
			}
			else{
				$lines.Add('    <'+$itemType+' '+$attributeName+'="'+(Get-Escaped $keep[1])+'">')
				foreach($pair in $keep[2]){ $lines.Add('      <'+$pair[0]+'>'+(Get-Escaped $pair[1])+'</'+$pair[0]+'>') }
				$lines.Add('    </'+$itemType+'>')
			}
		}
		if($lines.Count -gt 0){
			$itemText.Add('  <ItemGroup>')
			$itemText.AddRange($lines)
			$itemText.Add('  </ItemGroup>')
		}
	}
	#The SDK's globs compile every .cs file and embed every .resx file under the folder (bin, obj and hidden folders excepted): a project
	#that listed its files must have listed all of them, or the new project would compile more.
	$onDisk=@(Get-ChildItem -LiteralPath $folder -Recurse -File -Force | Where-Object {
		$relative=$_.FullName.Substring($folder.Length+1)
		-not ($relative -match '^(bin|obj)\\' -or $relative -match '(^|\\)\.[^\\]*\\')
	} | ForEach-Object { $_.FullName.Substring($folder.Length+1) })
	if(-not $hasGlob){
		$missing=@($onDisk | Where-Object { $_ -like '*.cs' -and -not $compiled.Contains($_) })
		if($missing.Count -gt 0){ throw "${relativePath}: the default glob would also compile $($missing -join ', ')." }
	}
	$missingResx=@($onDisk | Where-Object { $_ -like '*.resx' -and -not $embedded.Contains($_) })
	if($missingResx.Count -gt 0){ throw "${relativePath}: the default glob would also embed $($missingResx -join ', ')." }
	#endregion Items

	#region Text
	$text=New-Object 'System.Collections.Generic.List[string]'
	$text.Add($(if($outsideTests){ '<Project>' }else{ '<Project Sdk="Microsoft.NET.Sdk">' }))
	$topComments=@($project.ChildNodes | Where-Object { $_.NodeType -eq 'Comment' -and -not $_.Value.Contains($templateComment) })
	foreach($comment in $topComments){ $text.Add('  <!--'+(Get-CommentText $comment)+'-->') }
	if($topComments.Count -eq 0 -and $descriptions.ContainsKey($name)){ $text.Add('  <!--'+($descriptions[$name] -join "`n")+'-->') }
	if($outsideTests){
		$depth=($relativePath.Split('\').Count-1)
		$up=('..\' * $depth)
		$text.Add('  <PropertyGroup>')
		$text.Add('    <!--')
		$text.Add('      The project is outside tests\, so MSBuild would import the repository''s Directory.Build.props for it, not tests\Directory.Build.props,')
		$text.Add('      which holds what every test project shares. It names that file instead, before Sdk.props, which imports it; tests\Directory.Build.props')
		$text.Add('      imports the repository''s own first, so each of the two is imported once. Sdk.props and Sdk.targets are therefore imported explicitly.')
		$text.Add('    -->')
		$text.Add('    <DirectoryBuildPropsPath>$(MSBuildThisFileDirectory)'+$up+'tests\Directory.Build.props</DirectoryBuildPropsPath>')
		$text.Add('  </PropertyGroup>')
		$text.Add('  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />')
	}
	if($emit.Count -gt 0 -or $disableConfigurationDefines){
		$text.Add('  <PropertyGroup>')
		foreach($pair in @($emit | Sort-Object { [array]::IndexOf($propertyOrder,$_[0]) })){
			if($propertyComments.ContainsKey($pair[0])){
				foreach($comment in $propertyComments[$pair[0]]){ $text.Add('    <!--'+(Get-CommentText $comment)+'-->') }
			}
			if($pair[0] -eq 'Platforms'){
				$text.Add('    <!--'+$platformsComment+'-->')
				#The classic project's reason for x86, now the repository's default.
				if($propertyComments.ContainsKey('PlatformTarget')){
					foreach($comment in $propertyComments['PlatformTarget']){ $text.Add('    <!--'+(Get-CommentText $comment)+'-->') }
					$propertyComments.Remove('PlatformTarget')
				}
			}
			$text.Add('    <'+$pair[0]+'>'+(Get-Escaped $pair[1])+'</'+$pair[0]+'>')
		}
		if($disableConfigurationDefines){
			$text.Add('    <!-- As in the classic project, no DEBUG symbol in the Debug configuration (and no RELEASE in Release): only TRACE and the SDK''s')
			$text.Add('         framework symbols. -->')
			$text.Add('    <DisableImplicitConfigurationDefines>true</DisableImplicitConfigurationDefines>')
		}
		$text.Add('  </PropertyGroup>')
	}
	foreach($propertyName in @($propertyComments.Keys)){
		if(-not ($emit | Where-Object { $_[0] -eq $propertyName })){
			foreach($comment in $propertyComments[$propertyName]){ $notes.Add("dropped with the property ${propertyName}: <!--$($comment.Value)-->") }
		}
	}
	$text.AddRange($itemText)
	if($outsideTests){ $text.Add('  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />') }
	$text.Add('</Project>')
	#endregion Text

	#One kind of line end: the comments keep the classic file's own (CRLF in the working tree).
	$content=(($text -join "`n")+"`n").Replace("`r`n","`n")
	if($content -match '[^\x09\x0A\x0D\x20-\x7E]'){ Write-Host "${relativePath}: the new file holds non-ASCII text (from a comment); it is written as UTF-8." }
	if($Check){
		Write-Host "==== $relativePath"
		Write-Host $content
	}
	else{
		[System.IO.File]::WriteAllText($path,$content,(New-Object System.Text.UTF8Encoding($false)))
		$packagesConfig=Join-Path $folder 'packages.config'
		if(Test-Path -LiteralPath $packagesConfig){ Remove-Item -LiteralPath $packagesConfig }
		Write-Host ("{0}: {1} lines" -f $relativePath,$text.Count)
	}
	foreach($note in $notes){ Write-Host "  note: $note" }
}

foreach($projectPath in $Projects){
	if(-not [System.IO.Path]::IsPathRooted($projectPath)){ $projectPath=Join-Path $Root $projectPath }
	Convert-Project $projectPath
}
