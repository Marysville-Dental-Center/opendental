<#
.SYNOPSIS
Finds the build tools and sets the environment a build of this repository needs, in the current PowerShell process only.

.DESCRIPTION
Dot-source this file, then call its functions:

  . (Join-Path $repoRoot 'tools\Initialize-BuildEnvironment.ps1')
  $environment = Initialize-BuildEnvironment -RepoRoot $repoRoot
  $msbuild = Get-BuildToolPath -Tool MSBuild

Get-BuildToolPath finds MSBuild.exe or vstest.console.exe of Visual Studio 2026 or its Build Tools (version 18, which
installs under "Microsoft Visual Studio\18"), with vswhere and a version range, never the newest instance whatever its
version. -Toolset 2022 finds those of Visual Studio 2022 (version 17) instead, the fallback until the stage 1 gate of the
.NET 10 move (docs/specs/net10-plan.md, section 2.4). "-products *" is needed, or vswhere does not list Build Tools.

Initialize-BuildEnvironment sets, for this process and the programs it starts:
- The .NET SDK that global.json pins. The MSBuild SDK resolver takes the first dotnet.exe on PATH. When that one does
  not have the pinned SDK and the per-user dotnet (%LOCALAPPDATA%\Microsoft\dotnet) or the machine-wide one does,
  DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR names that folder and the folder goes first in PATH. A
  DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR that is already set is kept.
- OPMS_NUGET_OFFLINE, the folder nuget.config restores from, when it is not set already: od-tools\nuget-packages in
  the repository's git folder (the common git folder, the same for every worktree), if that folder exists.
- DOTNET_NOLOGO, DOTNET_CLI_TELEMETRY_OPTOUT, DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE and
  NUGET_CERT_REVOCATION_MODE=offline: no banner, no telemetry, no check that would go online.
It returns what it found: SdkVersion, SdkFolder (empty when the pinned SDK is not installed) and OfflineFolder.
Nothing is downloaded or installed, and nothing outside this process changes.
#>

function Get-BuildToolPath {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory=$true)][ValidateSet('MSBuild','VsTest')][string]$Tool,
		[ValidateSet('2026','2022')][string]$Toolset='2026'
	)
	#A tool's message on stderr must not end the caller's script (ErrorActionPreference Stop); the result is checked instead.
	$ErrorActionPreference='Continue'
	$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
	if(-not (Test-Path -LiteralPath $vswhere)){ return $null }
	$range=if($Toolset -eq '2026'){ '[18.0,19.0)' }else{ '[17.0,18.0)' }
	if($Tool -eq 'MSBuild'){
		$found=& $vswhere -latest -products * -version $range -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
	}
	else{
		$found=& $vswhere -latest -products * -version $range -find 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe' | Select-Object -First 1
	}
	if($found -and (Test-Path -LiteralPath $found -PathType Leaf)){ return $found }
	return $null
}

function Initialize-BuildEnvironment {
	[CmdletBinding()]
	param(
		[Parameter(Mandatory=$true)][string]$RepoRoot
	)
	$ErrorActionPreference='Continue'
	$env:DOTNET_NOLOGO='1'
	$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
	$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1'
	$env:NUGET_CERT_REVOCATION_MODE='offline'
	#The pinned SDK.
	$sdkVersion=''
	$sdkFolder=''
	$globalJson=Join-Path $RepoRoot 'global.json'
	if(Test-Path -LiteralPath $globalJson -PathType Leaf){
		#Property by property, so it also runs under Set-StrictMode (tools\Build-Release.ps1).
		$pin=Get-Content -LiteralPath $globalJson -Raw | ConvertFrom-Json
		$sdk=$pin.PSObject.Properties['sdk']
		if($sdk -and $sdk.Value -and $sdk.Value.PSObject.Properties['version']){ $sdkVersion=[string]$sdk.Value.version }
	}
	if($sdkVersion){
		if($env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR){
			$sdkFolder=$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
		}
		else{
			$onPath=Get-Command 'dotnet.exe' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
			$folderOnPath=if($onPath){ Split-Path -Parent $onPath.Source }else{ '' }
			$candidates=@($folderOnPath,(Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'),(Join-Path $env:ProgramFiles 'dotnet')) | Where-Object { $_ }
			foreach($folder in $candidates){
				if(Test-Path -LiteralPath (Join-Path $folder "sdk\$sdkVersion\dotnet.dll") -PathType Leaf){
					$sdkFolder=$folder
					break
				}
			}
			if($sdkFolder -and $sdkFolder -ne $folderOnPath){
				$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$sdkFolder
				$env:PATH=$sdkFolder+';'+$env:PATH
			}
		}
	}
	#The offline NuGet folder.
	if(-not $env:OPMS_NUGET_OFFLINE){
		$git=Get-Command 'git.exe' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
		if($git){
			Push-Location -LiteralPath $RepoRoot
			try{
				#All of git's output, not the first line of a pipeline: stopping the pipeline early ends git with exit code -1.
				$gitOutput=@(& $git.Source rev-parse --path-format=absolute --git-common-dir 2>$null)
				if($LASTEXITCODE -eq 0 -and $gitOutput.Count -gt 0 -and $gitOutput[0]){
					$offline=Join-Path (([string]$gitOutput[0]).Trim() -replace '/','\') 'od-tools\nuget-packages'
					if(Test-Path -LiteralPath $offline -PathType Container){ $env:OPMS_NUGET_OFFLINE=$offline }
				}
			}
			finally{ Pop-Location }
		}
	}
	return [pscustomobject]@{ SdkVersion=$sdkVersion; SdkFolder=$sdkFolder; OfflineFolder=[string]$env:OPMS_NUGET_OFFLINE }
}
