// The items of an MSBuild project file as MSBuild evaluates them, for the tests that check project files (package S1-T of the .NET 10 plan,
// docs/specs/net10-plan.md). A check such as "this file is compiled", "this resource is embedded", "this page is built" or "this reference is
// there" asks this reader instead of looking for a literal line, so it holds for today's classic project files (the 2003 MSBuild namespace,
// every item listed) and for the SDK-style ones stage 1 converts them to (no namespace, the .NET SDK's default globs, explicit items only where
// they carry metadata) alike. Linked into every test project that checks a project file, as tests\ForkMigrationTests\TestDb.cs is; its own
// tests are in tests\ProjectFileTests. Reads files only: no MSBuild, no build, nothing is started.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	///<summary>A project file evaluated as MSBuild evaluates its properties and items, for one configuration and platform: by default the ones
	///OpenDental.sln builds the project with for the build lane's Debug|x86 (Debug|x86, or Debug|AnyCPU for WpfControlsOD and CryptoTests).
	///What it models: the properties of the project file and of the files it imports from the repository (Directory.Build.props and
	///Directory.Build.targets above the project, and relative imports such as third-party\build\SourceBuild.props), with their conditions; the
	///properties that MSBuild's and the SDK's own files compute and that the repository's project files or tests use, each at the point of the
	///evaluation where MSBuild computes it (the output and intermediate paths, the target framework, AssemblyName, OutputType, TargetPath and the
	///like: region "MSBuild's and the SDK's own files"); the items of those files in MSBuild's order, with Include (a file, a list, a wildcard, an
	///item list), Exclude, Remove and Update, and their metadata from child elements and attributes alike; for an SDK-style project the .NET
	///SDK's default items before the project's own (Compile **\*.cs, EmbeddedResource **\*.resx and None **\* without bin, obj, hidden folders and
	///the DefaultItemExcludes; with UseWPF, Page **\*.xaml and App.xaml as ApplicationDefinition, with the WindowsDesktop SDK's Page metadata),
	///switched off by EnableDefaultItems, EnableDefaultCompileItems and the other switches, and the Link metadata the SDK gives an item outside
	///the project folder (SetLinkMetadataAutomatically), exactly as Microsoft.NET.Sdk.DefaultItems.props/.targets and
	///Microsoft.NET.Sdk.WindowsDesktop.props/.targets of the pinned SDK 10.0.114 define them.
	///What it does not model: the rest of the files of MSBuild and the SDK, targets (items created while building), ItemDefinitionGroup
	///defaults other than the WindowsDesktop SDK's, and Choose. Anything it cannot evaluate throws, so a check never passes on a guess: Choose, a
	///missing import, an unknown property function in an Include or a condition, and a property that MSBuild, its toolset, the solution build
	///or the SDK compute and this reader does not (the machine paths such as $(MSBuildExtensionsPath), $(SolutionDir), the SDK's
	///$(RuntimeIdentifier): _arrayNotModelledProperties and _arrayNotModelledSdkProperties) used in a Condition, an Include or an Import, directly
	///or through another property, or asked of GetProperty.</summary>
	internal sealed class ProjectFile {
		///<summary>The project file, full path.</summary>
		public string FullPath { get; private set; }
		///<summary>The folder of the project file, full path without a trailing back slash.</summary>
		public string Folder { get; private set; }
		///<summary>True for an SDK-style project file: an Sdk attribute on Project, an Sdk element, or an Import with an Sdk attribute.</summary>
		public bool IsSdkStyle { get; private set; }
		public string Configuration { get; private set; }
		public string Platform { get; private set; }
		private readonly Dictionary<string,string> _dictProperties=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		private readonly List<ProjectItem> _listItems=new List<ProjectItem>();
		///<summary>The item groups (and the SDK's default item steps) in MSBuild's order, evaluated once every property is known.</summary>
		private readonly List<ItemStep> _listItemSteps=new List<ItemStep>();
		///<summary>The project file and the files it imported, in the order they were read. MSBuild skips a file imported twice; so does this.</summary>
		private readonly List<string> _listFilesRead=new List<string>();
		private const string UNRESOLVED_START="\u0001unresolved:";
		private const string UNRESOLVED_END="\u0002";
		private const string STEP_SDK_DEFAULT_ITEMS="SdkDefaultItems";
		private const string STEP_SDK_LINK_METADATA="SdkLinkMetadata";
		private const string STEP_SDK_TARGETS_ITEMS="SdkTargetsItems";
		///<summary>$(VisualStudioVersion) in every project built by MSBuild 18 (Build Tools 2026, which the build lane and section 2.4 of the
		///.NET 10 plan pin): the sub-toolset version of its Current toolset. OpenPmsDentalServer.csproj tests it in a condition.</summary>
		private const string VISUAL_STUDIO_VERSION="18.0";
		private static readonly string[] _arrayReservedAttributes=new[] { "Include","Exclude","Remove","Update","Condition","KeepMetadata",
			"RemoveMetadata","KeepDuplicates","MatchOnMetadata","MatchOnMetadataOptions","Label" };
		///<summary>Properties that MSBuild, its toolset (MSBuild.exe.config) or the solution build give every project and this reader does not
		///compute: machine paths and versions, and the solution's global properties. One of them in a Condition, an Include or an Import throws
		///(except the imports of MSBuild's and Visual Studio's own files, which are not read), as does GetProperty for it, unless a file this reader
		///read set it.</summary>
		private static readonly string[] _arrayNotModelledProperties=new[] { "MSBuildExtensionsPath","MSBuildExtensionsPath32","MSBuildExtensionsPath64",
			"MSBuildToolsPath","MSBuildToolsPath32","MSBuildToolsPath64","MSBuildBinPath","MSBuildSDKsPath","MSBuildToolsRoot","MSBuildFrameworkToolsPath",
			"MSBuildFrameworkToolsPath32","MSBuildFrameworkToolsPath64","MSBuildFrameworkToolsRoot","MSBuildProgramFiles32","MSBuildUserExtensionsPath",
			"MSBuildVersion","MSBuildAssemblyVersion","MSBuildFileVersion","MSBuildSemanticVersion","MSBuildRuntimeType","MSBuildRuntimeVersion",
			"MSBuildNodeCount","MSBuildStartupDirectory","MSBuildInteractive","MSBuildIsRestoring","MSBuildOverrideTasksPath","VsInstallRoot",
			"RoslynTargetsPath","FrameworkSDKRoot","SDK35ToolsPath","SDK40ToolsPath","WindowsSDK80Path","WindowsKitsRoot","VCTargetsPath",
			"TargetFrameworkRootPath","FrameworkPathOverride","TargetFrameworkDirectory","TargetPlatformSdkPath","TargetPlatformDisplayName",
			"TargetPlatformSdkMetadataLocation","NuGetPackageRoot","NuGetPackageFolders",
			"NETCoreSdkVersion","NETCoreSdkRuntimeIdentifier","NETCoreRoot","DOTNET_HOST_PATH","SolutionDir","SolutionPath","SolutionName","SolutionExt",
			"SolutionFileName","CurrentSolutionConfigurationContents","BuildingSolutionFile","BuildingInsideVisualStudio" };
		///<summary>Properties the .NET SDK computes for an SDK-style project and this reader does not (a classic project has none of them, so they
		///are '' there, as MSBuild has them): the inferred runtime identifier, the generated documentation file, the package and assembly info
		///defaults, the restore's output, the publish and package folders.</summary>
		private static readonly string[] _arrayNotModelledSdkProperties=new[] { "RuntimeIdentifier","RuntimeIdentifiers","DocumentationFile",
			"VersionPrefix","Version","Authors","Company","AssemblyTitle","Product","ProjectAssetsFile","ProjectDepsFilePath","ProjectRuntimeConfigFilePath",
			"PublishDir","PackageOutputPath","NuGetTargetMoniker" };
		///<summary>The item types whose items Microsoft.NET.Sdk.DefaultItems.targets gives Link metadata (SetLinkMetadataAutomatically).</summary>
		private static readonly string[] _arrayLinkItemTypes=new[] { "Compile","AdditionalFiles","None","Content","EmbeddedResource","Page","Resource" };
		///<summary>The names of _arrayNotModelledProperties, and for an SDK-style project those of _arrayNotModelledSdkProperties and any the
		///evaluation could not compute (TargetPlatformVersion of a .NET 5+ target without a platform version, say).</summary>
		private readonly HashSet<string> _hashSetNotModelled=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		///<summary>OpenDental.sln by path: its last write time and its project mappings, read once per test run.</summary>
		private static readonly Dictionary<string,SolutionFile> _dictSolutions=new Dictionary<string,SolutionFile>(StringComparer.OrdinalIgnoreCase);

		private ProjectFile() {
		}

		#region Loading
		///<summary>The project file evaluated for the configuration and platform OpenDental.sln builds it with for the build lane's Debug|x86
		///(GetSolutionConfiguration): Debug|x86 for nearly every project, Debug|AnyCPU for WpfControlsOD and CryptoTests, so the properties of
		///their platform-conditioned groups are read as the build reads them.</summary>
		public static ProjectFile Load(string path) {
			string configuration;
			string platform;
			GetSolutionConfiguration(Path.GetFullPath(path),"Debug","x86",out configuration,out platform);
			return Load(path,configuration,platform);
		}

		///<summary>The configuration and platform that OpenDental.sln (the first one in the project's folder or a folder above it) maps the
		///solution configuration and platform to for the project, as MSBuild builds it from the solution or the solution filter: "Any CPU" becomes
		///AnyCPU, as the solution build passes it. Without an OpenDental.sln above the project, or for a project the solution does not list, the
		///solution's own configuration and platform.</summary>
		public static void GetSolutionConfiguration(string pathProject,string solutionConfiguration,string solutionPlatform,out string configuration,
			out string platform)
		{
			configuration=solutionConfiguration;
			platform=solutionPlatform;
			string pathSln=FindFileAbove("OpenDental.sln",Path.GetDirectoryName(Path.GetFullPath(pathProject)));
			if(pathSln==null) {
				return;
			}
			string[] arrayMapping=GetSolutionFile(pathSln).GetMapping(Path.GetFullPath(pathProject),solutionConfiguration+"|"+solutionPlatform);
			if(arrayMapping!=null) {
				configuration=arrayMapping[0];
				platform=arrayMapping[1];
			}
		}

		private static SolutionFile GetSolutionFile(string pathSln) {
			DateTime dateTimeWrite=File.GetLastWriteTimeUtc(pathSln);
			lock(_dictSolutions) {
				SolutionFile solutionFile;
				if(!_dictSolutions.TryGetValue(pathSln,out solutionFile) || solutionFile.DateTimeWriteUtc!=dateTimeWrite) {
					solutionFile=new SolutionFile(pathSln,dateTimeWrite);
					_dictSolutions[pathSln]=solutionFile;
				}
				return solutionFile;
			}
		}

		public static ProjectFile Load(string path,string configuration,string platform) {
			string fullPath=Path.GetFullPath(path);
			if(!File.Exists(fullPath)) {
				throw new FileNotFoundException("No project file at "+fullPath,fullPath);
			}
			ProjectFile projectFile=new ProjectFile();
			projectFile.FullPath=fullPath;
			projectFile.Folder=Path.GetDirectoryName(fullPath);
			projectFile.Configuration=configuration;
			projectFile.Platform=platform;
			XDocument xDocument=XDocument.Load(fullPath);
			projectFile.IsSdkStyle=IsSdkStyleDocument(xDocument);
			projectFile.Evaluate(xDocument);
			return projectFile;
		}

		///<summary>True when the document is an SDK-style project: Project Sdk="...", an Sdk element, or Import Sdk="..." (explicit Sdk.props and
		///Sdk.targets imports).</summary>
		public static bool IsSdkStyleDocument(XDocument xDocument) {
			XElement xElementRoot=xDocument.Root;
			if(xElementRoot==null) {
				return false;
			}
			return xElementRoot.Attribute("Sdk")!=null
				|| xElementRoot.Elements().Any(x => x.Name.LocalName=="Sdk")
				|| xElementRoot.Descendants().Any(x => x.Name.LocalName=="Import" && x.Attribute("Sdk")!=null);
		}

		///<summary>The full paths of the C# projects a solution filter (.slnf) lists, in its order.</summary>
		public static List<string> GetSolutionFilterProjects(string pathSlnf) {
			string folder=Path.GetDirectoryName(Path.GetFullPath(pathSlnf));
			return Regex.Matches(File.ReadAllText(pathSlnf),@"""([^""]+\.csproj)""").Cast<Match>()
				.Select(x => Path.GetFullPath(Path.Combine(folder,x.Groups[1].Value.Replace(@"\\",@"\").Replace('/','\\'))))
				.ToList();
		}

		private void Evaluate(XDocument xDocument) {
			_dictProperties["Configuration"]=Configuration;
			_dictProperties["Platform"]=Platform;
			_dictProperties["MSBuildProjectFullPath"]=FullPath;
			_dictProperties["MSBuildProjectDirectory"]=Folder;
			_dictProperties["MSBuildProjectFile"]=Path.GetFileName(FullPath);
			_dictProperties["MSBuildProjectName"]=Path.GetFileNameWithoutExtension(FullPath);
			_dictProperties["MSBuildProjectExtension"]=Path.GetExtension(FullPath);
			//Microsoft.Common.CurrentVersion.targets defines these for every project; build events and copy targets use them.
			_dictProperties["ProjectDir"]=Folder+@"\";
			_dictProperties["ProjectPath"]=FullPath;
			_dictProperties["ProjectFileName"]=Path.GetFileName(FullPath);
			_dictProperties["ProjectName"]=Path.GetFileNameWithoutExtension(FullPath);
			_dictProperties["MSBuildProjectDirectoryNoRoot"]=Folder.Substring(Path.GetPathRoot(Folder).Length);
			//MSBuild 18's toolset: "Current" for every MSBuild since 16, and the sub-toolset version.
			_dictProperties["MSBuildToolsVersion"]="Current";
			_dictProperties["VisualStudioVersion"]=VISUAL_STUDIO_VERSION;
			_hashSetNotModelled.UnionWith(_arrayNotModelledProperties);
			if(IsSdkStyle) {
				_dictProperties["UsingMicrosoftNETSdk"]="true";
				_hashSetNotModelled.UnionWith(_arrayNotModelledSdkProperties);
			}
			_listFilesRead.Add(FullPath);
			XElement xElementRoot=xDocument.Root;
			bool hasImplicitSdk=xElementRoot.Attribute("Sdk")!=null || xElementRoot.Elements().Any(x => x.Name.LocalName=="Sdk");
			FileContext fileContext=new FileContext(FullPath);
			if(hasImplicitSdk) {
				ImportSdkProps();
			}
			ReadProjectElement(xElementRoot,fileContext);
			if(hasImplicitSdk) {
				ImportSdkTargets();
			}
			foreach(ItemStep itemStep in _listItemSteps) {
				if(itemStep.Builtin==STEP_SDK_DEFAULT_ITEMS) {
					AddSdkDefaultItems();
				}
				else if(itemStep.Builtin==STEP_SDK_LINK_METADATA) {
					ApplySdkLinkMetadata();
				}
				else if(itemStep.Builtin==STEP_SDK_TARGETS_ITEMS) {
					ApplySdkTargetsItems();
				}
				else {
					EvaluateItemGroup(itemStep.XElementGroup,itemStep.FileContext);
				}
			}
		}

		///<summary>Pass 1 of MSBuild's evaluation for one file: properties in order (imports read where they stand), item groups queued for pass 2.
		///</summary>
		private void ReadProjectElement(XElement xElementProject,FileContext fileContext) {
			foreach(XElement xElement in xElementProject.Elements()) {
				switch(xElement.Name.LocalName) {
					case "PropertyGroup":
						if(!IsConditionTrue(xElement,fileContext)) {
							break;
						}
						foreach(XElement xElementProperty in xElement.Elements()) {
							//Configuration and Platform are global properties (the solution gives them), which a project file cannot change.
							if(new[] { "Configuration","Platform" }.Contains(xElementProperty.Name.LocalName,StringComparer.OrdinalIgnoreCase)) {
								continue;
							}
							if(IsConditionTrue(xElementProperty,fileContext)) {
								_dictProperties[xElementProperty.Name.LocalName]=Expand(xElementProperty.Value.Trim(),fileContext);
							}
						}
						break;
					case "ItemGroup":
						_listItemSteps.Add(new ItemStep { XElementGroup=xElement,FileContext=fileContext });
						break;
					case "Import":
						ReadImport(xElement,fileContext);
						break;
					case "ImportGroup":
						if(IsConditionTrue(xElement,fileContext)) {
							foreach(XElement xElementImport in xElement.Elements().Where(x => x.Name.LocalName=="Import")) {
								ReadImport(xElementImport,fileContext);
							}
						}
						break;
					case "Choose":
						throw new NotSupportedException(fileContext.Path+": Choose is not modelled by ProjectFile.");
					default:
						//Target, UsingTask, ItemDefinitionGroup, ProjectExtensions, Sdk: nothing that adds an item at evaluation.
						break;
				}
			}
		}

		private void ReadImport(XElement xElementImport,FileContext fileContext) {
			string project=(string)xElementImport.Attribute("Project")??"";
			if(xElementImport.Attribute("Sdk")!=null) {
				if(!IsConditionTrue(xElementImport,fileContext)) {
					return;
				}
				if(project.EndsWith("Sdk.props",StringComparison.OrdinalIgnoreCase)) {
					ImportSdkProps();
				}
				else if(project.EndsWith("Sdk.targets",StringComparison.OrdinalIgnoreCase)) {
					ImportSdkTargets();
				}
				return;
			}
			string fileName=Path.GetFileName(project.Replace('/','\\'));
			//The two imports of MSBuild's own files that import the repository's Directory.Build.props and Directory.Build.targets.
			if(fileName.Equals("Microsoft.Common.props",StringComparison.OrdinalIgnoreCase)) {
				ImportMicrosoftCommonProps();
				return;
			}
			if(new[] { "Microsoft.CSharp.targets","Microsoft.Common.targets","Microsoft.VisualBasic.targets" }.Contains(fileName,StringComparer.OrdinalIgnoreCase)) {
				ImportMicrosoftCommonTargets(fileName);
				return;
			}
			//Any other file of MSBuild, Visual Studio or the SDK (the web application targets of the Middle Tier project): not modelled.
			if(Regex.IsMatch(project,@"^\$\((MSBuildExtensionsPath|MSBuildExtensionsPath32|MSBuildExtensionsPath64|MSBuildToolsPath|MSBuildBinPath|VSToolsPath|MSBuildSDKsPath|NuGetPackageRoot)\)",
				RegexOptions.IgnoreCase))
			{
				return;
			}
			if(!IsConditionTrue(xElementImport,fileContext)) {
				return;
			}
			string expanded=Expand(project,fileContext);
			ThrowIfUnresolved(expanded,fileContext.Path+": Import "+project);
			if(expanded.IndexOfAny(new[] { '*','?' })>=0) {
				throw new NotSupportedException(fileContext.Path+": a wildcard Import is not modelled by ProjectFile: "+project);
			}
			string path=Path.GetFullPath(Path.Combine(fileContext.Directory,expanded));
			if(!File.Exists(path)) {
				throw new FileNotFoundException(fileContext.Path+" imports "+project+", which is not there ("+path+"). MSBuild fails the same way (MSB4019).",path);
			}
			ReadImportedFile(path);
		}

		private void ReadImportedFile(string path) {
			if(_listFilesRead.Contains(path,StringComparer.OrdinalIgnoreCase)) {
				return;//MSBuild skips a second import of the same file (warning MSB4011).
			}
			_listFilesRead.Add(path);
			ReadProjectElement(XDocument.Load(path).Root,new FileContext(path));
		}

		///<summary>Directory.Build.props or Directory.Build.targets: the first one in the project's folder or a folder above it, as
		///Microsoft.Common.props and Microsoft.Common.targets find it.</summary>
		private void ImportDirectoryBuildFile(string fileName) {
			string switchName=fileName.EndsWith(".props",StringComparison.OrdinalIgnoreCase) ? "ImportDirectoryBuildProps" : "ImportDirectoryBuildTargets";
			if(IsPropertyFalse(switchName)) {
				return;
			}
			string path=FindFileAbove(fileName,Folder);
			if(path!=null) {
				ReadImportedFile(path);
			}
		}
		#endregion Loading

		#region MSBuild's and the SDK's own files
		//What the files of MSBuild 18 (Build Tools 2026) and of the pinned .NET SDK 10.0.114 do for the properties a project's conditions, items
		//and the tests read, at the point of the evaluation where those files do it, in their order. Each method names the file it follows; the
		//properties it does not compute are in _arrayNotModelledProperties and _arrayNotModelledSdkProperties, and throw when used.

		///<summary>Microsoft.Common.props, which a classic project imports first and Sdk.props imports for an SDK-style project:
		///Directory.Build.props, then BaseIntermediateOutputPath (obj\ unless set, with a trailing back slash) and MSBuildProjectExtensionsPath, the
		///folder of the restore's files (BaseIntermediateOutputPath unless set, made a full path from the project folder).</summary>
		private void ImportMicrosoftCommonProps() {
			ImportDirectoryBuildFile("Directory.Build.props");
			SetIfEmpty("BaseIntermediateOutputPath",@"obj\");
			AppendTrailingSlash("BaseIntermediateOutputPath");
			_dictProperties["_InitialBaseIntermediateOutputPath"]=GetValue("BaseIntermediateOutputPath");
			SetIfEmpty("MSBuildProjectExtensionsPath",GetValue("BaseIntermediateOutputPath"));
			string extensionsPath=GetValue("MSBuildProjectExtensionsPath");
			if(!IsUnresolved(extensionsPath) && !Path.IsPathRooted(extensionsPath)) {
				_dictProperties["MSBuildProjectExtensionsPath"]=Path.Combine(Folder,extensionsPath);
			}
			AppendTrailingSlash("MSBuildProjectExtensionsPath");
		}

		///<summary>Microsoft.CSharp.targets (or the Visual Basic or common one) of a classic project, and the language targets that Sdk.targets
		///imports: the language (Microsoft.CSharp.CurrentVersion.targets), the properties of Microsoft.Common.CurrentVersion.targets, then
		///Directory.Build.targets, which Microsoft.Common.targets imports after them.</summary>
		private void ImportMicrosoftCommonTargets(string fileName) {
			if(fileName.Equals("Microsoft.VisualBasic.targets",StringComparison.OrdinalIgnoreCase)) {
				_dictProperties["Language"]="VB";
				_dictProperties["DefaultLanguageSourceExtension"]=".vb";
				_dictProperties["TargetRuntime"]="Managed";
			}
			else if(fileName.Equals("Microsoft.CSharp.targets",StringComparison.OrdinalIgnoreCase)) {
				_dictProperties["Language"]="C#";
				_dictProperties["DefaultLanguageSourceExtension"]=".cs";
				_dictProperties["TargetRuntime"]="Managed";
			}
			ApplyCommonTargetsProperties();
			ImportDirectoryBuildFile("Directory.Build.targets");
		}

		///<summary>The properties of Microsoft.Common.CurrentVersion.targets (MSBuild 18) that the repository's files and tests read, in its
		///order: the target framework defaults (.NETFramework, v4.0) and moniker; the Windows platform defaults (Windows, 7.0) unless a .NET 5+
		///target turned them off; the platform and configuration names; BaseOutputPath (bin\), OutputPath (bin\configuration\ for AnyCPU, else
		///bin\platform\configuration\, when the project sets none), BaseIntermediateOutputPath and IntermediateOutputPath the same way, each with a
		///trailing back slash; OutputType (exe); the output file's extension, folder (OutDir, TargetDir as a full path), name and path;
		///GenerateBindingRedirectsOutputType for an exe; and AutoGenerateBindingRedirects for a .NET Framework 4.7.2 or later project that leaves
		///it empty.</summary>
		private void ApplyCommonTargetsProperties() {
			SetIfEmpty("TargetRuntime","Managed");
			if(IsValue("TargetRuntime","Managed")) {
				SetIfEmpty("TargetFrameworkIdentifier",".NETFramework");
				SetIfEmpty("TargetFrameworkVersion","v4.0");
			}
			string frameworkIdentifier=GetValue("TargetFrameworkIdentifier");
			string frameworkVersion=GetValue("TargetFrameworkVersion");
			if(frameworkIdentifier!="" && frameworkVersion!="" && GetValue("TargetFrameworkMoniker")=="") {
				string profile=GetValue("TargetFrameworkProfile");
				_dictProperties["TargetFrameworkMoniker"]=frameworkIdentifier+",Version="+frameworkVersion+(string.IsNullOrWhiteSpace(profile) ? "" : ",Profile="+profile);
			}
			if(!IsPropertyFalse("_EnableDefaultWindowsPlatform")) {
				SetIfEmpty("TargetPlatformIdentifier","Windows");
				SetIfEmpty("TargetPlatformVersion","7.0");
			}
			if(GetValue("TargetPlatformMoniker")=="" && GetValue("TargetPlatformIdentifier")!="" && GetValue("TargetPlatformVersion")!="") {
				_dictProperties["TargetPlatformMoniker"]=GetValue("TargetPlatformIdentifier")+",Version="+GetValue("TargetPlatformVersion");
			}
			SetIfEmpty("PlatformName",GetValue("Platform"));
			SetIfEmpty("ConfigurationName",GetValue("Configuration"));
			string platformFolder=IsValue("PlatformName","AnyCPU") ? "" : GetValue("PlatformName")+@"\";
			SetIfEmpty("BaseOutputPath",@"bin\");
			AppendTrailingSlash("BaseOutputPath");
			SetIfEmpty("OutputPath",GetValue("BaseOutputPath")+platformFolder+GetValue("Configuration")+@"\");
			AppendTrailingSlash("OutputPath");
			SetIfEmpty("BaseIntermediateOutputPath",@"obj\");
			AppendTrailingSlash("BaseIntermediateOutputPath");
			SetIfEmpty("IntermediateOutputPath",GetValue("BaseIntermediateOutputPath")+platformFolder+GetValue("Configuration")+@"\");
			AppendTrailingSlash("IntermediateOutputPath");
			//The legacy TargetType, then the default.
			if(GetValue("TargetType")!="") {
				_dictProperties["OutputType"]=IsValue("TargetType","Container") || IsValue("TargetType","DocumentContainer") ? "library" : GetValue("TargetType");
			}
			SetIfEmpty("OutputType","exe");
			if(GetValue("TargetExt")=="") {
				foreach(string[] arrayExtension in new[] { new[] { "exe",".exe" },new[] { "winexe",".exe" },new[] { "appcontainerexe",".exe" },
					new[] { "library",".dll" },new[] { "module",".netmodule" },new[] { "winmdobj",".winmdobj" } })
				{
					if(IsValue("OutputType",arrayExtension[0])) {
						_dictProperties["TargetExt"]=arrayExtension[1];
					}
				}
			}
			bool isOutDirSpecified=GetValue("OutDir")!="";
			SetIfEmpty("OutDir",GetValue("OutputPath"));
			if(GetValue("OutDir")!="") {
				AppendTrailingSlash("OutDir");
			}
			SetIfEmpty("ProjectName",GetValue("MSBuildProjectName"));
			if(GetValue("OutDir")!="" && isOutDirSpecified
				&& ((IsPropertyTrue("WindowsAppContainer") && !IsPropertyFalse("GenerateProjectSpecificOutputFolder")) || IsPropertyTrue("GenerateProjectSpecificOutputFolder")))
			{
				_dictProperties["OutDir"]=GetValue("OutDir")+GetValue("ProjectName")+@"\";
			}
			SetIfEmpty("AssemblyName",GetValue("MSBuildProjectName"));
			if(GetValue("TargetName")=="" && IsValue("OutputType","winmdobj") && GetValue("RootNamespace")!="") {
				_dictProperties["TargetName"]=GetValue("RootNamespace");
			}
			SetIfEmpty("TargetName",GetValue("AssemblyName"));
			if(IsValue("OutputType","winmdobj")) {
				SetIfEmpty("WinMDExpOutputWindowsMetadataFilename",GetValue("TargetName")+".winmd");
				SetIfEmpty("TargetFileName",GetValue("WinMDExpOutputWindowsMetadataFilename"));
			}
			SetIfEmpty("TargetFileName",GetValue("TargetName")+GetValue("TargetExt"));
			string outDir=GetValue("OutDir");
			if(outDir!="") {
				_dictProperties["TargetDir"]=IsUnresolved(outDir) ? outDir : Path.GetFullPath(Path.Combine(Folder,outDir));
			}
			SetIfEmpty("TargetPath",GetValue("TargetDir")+GetValue("TargetFileName"));
			SetIfEmpty("ProjectDir",EnsureTrailingSlash(Folder));
			if(IsValue("OutputType","exe") || IsValue("OutputType","winexe")) {
				_dictProperties["GenerateBindingRedirectsOutputType"]="true";
			}
			Version versionFramework;
			if(GetValue("AutoGenerateBindingRedirects")=="" && IsValue("TargetFrameworkIdentifier",".NETFramework")
				&& Version.TryParse(GetValue("TargetFrameworkVersion").TrimStart('v','V'),out versionFramework) && versionFramework>=new Version(4,7,2))
			{
				_dictProperties["AutoGenerateBindingRedirects"]="true";
			}
		}

		///<summary>Sdk.props of Microsoft.NET.Sdk: UsingMicrosoftNETSdk, Microsoft.Common.props (Directory.Build.props), the defaults of
		///Microsoft.NET.Sdk.props that the project's own properties may read (OutputType Library, AssemblyName and RootNamespace from the project's
		///name, PlatformTarget from an x86, x64, ARM or arm64 platform), and Microsoft.NET.Sdk.DefaultItems.props and
		///Microsoft.NET.Sdk.WindowsDesktop.props, whose default items come before the project's own.</summary>
		private void ImportSdkProps() {
			_dictProperties["UsingMicrosoftNETSdk"]="true";
			_dictProperties["UsingNETSdkDefaults"]="true";
			ImportMicrosoftCommonProps();
			SetIfEmpty("OutputType","Library");
			SetIfEmpty("AssemblyName",GetValue("MSBuildProjectName"));
			SetIfEmpty("RootNamespace",GetValue("MSBuildProjectName").Replace(" ","_"));
			foreach(string platformTarget in new[] { "x64","x86","ARM","arm64" }) {
				if(IsValue("Platform",platformTarget)) {
					SetIfEmpty("PlatformTarget",platformTarget);
				}
			}
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_DEFAULT_ITEMS });
		}

		///<summary>Sdk.targets of Microsoft.NET.Sdk, in its order: Microsoft.NET.Sdk.BeforeCommon.targets (ApplySdkBeforeCommonProperties); the
		///language targets with Microsoft.Common.targets and Directory.Build.targets; Microsoft.NET.Sdk.targets with the default item switches and
		///excludes of Microsoft.NET.Sdk.DefaultItems.targets and its automatic Link metadata (an item step, after every item before this point);
		///then, for a WPF or Windows Forms project, Microsoft.NET.Sdk.WindowsDesktop.targets with its defaults and Page removals.</summary>
		private void ImportSdkTargets() {
			ApplySdkBeforeCommonProperties();
			ImportMicrosoftCommonTargets(Path.GetExtension(FullPath).Equals(".vbproj",StringComparison.OrdinalIgnoreCase) ? "Microsoft.VisualBasic.targets"
				: "Microsoft.CSharp.targets");
			SetIfEmpty("EnableDefaultItems","true");
			SetIfEmpty("EnableDefaultCompileItems","true");
			SetIfEmpty("EnableDefaultEmbeddedResourceItems","true");
			SetIfEmpty("EnableDefaultNoneItems","true");
			_dictProperties["DefaultItemExcludes"]=GetValue("DefaultItemExcludes")+";"+GetValue("BaseOutputPath")+"/**;"
				+GetValue("BaseIntermediateOutputPath")+"/**;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";
			_dictProperties["DefaultExcludesInProjectFolder"]=GetValue("DefaultExcludesInProjectFolder")+";"
				+GetValue("DefaultItemExcludesInProjectFolder")+";**/.*/**";
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_LINK_METADATA });
			Version versionFramework;
			bool isWindowsDesktopFramework=IsValue("TargetFrameworkIdentifier",".NETFramework")
				|| (IsValue("TargetFrameworkIdentifier",".NETCoreApp") && IsValue("TargetPlatformIdentifier","Windows")
				&& Version.TryParse(GetValue("_TargetFrameworkVersionWithoutV"),out versionFramework) && versionFramework>=new Version(3,0));
			if(GetValue("ImportWindowsDesktopTargets")=="" && (IsPropertyTrue("UseWPF") || IsPropertyTrue("UseWindowsForms")) && isWindowsDesktopFramework) {
				_dictProperties["ImportWindowsDesktopTargets"]="true";
			}
			if(IsPropertyTrue("ImportWindowsDesktopTargets")) {
				SetIfEmpty("EnableDefaultPageItems","true");
				SetIfEmpty("EnableDefaultApplicationDefinition","true");
				SetIfEmpty("DefaultXamlRuntime","Wpf");
			}
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_TARGETS_ITEMS });
		}

		///<summary>The part of Microsoft.NET.Sdk.BeforeCommon.targets (SDK 10.0.114) that comes before the common targets: whether the project is
		///an exe; the platform name; the target framework's identifier, version and platform (Microsoft.NET.TargetFrameworkInference.targets); the
		///output and intermediate paths (Microsoft.NET.DefaultOutputPaths.targets: bin\ and obj\, then the platform folder unless the platform is
		///AnyCPU or AppendPlatformToOutputPath is false, the configuration, both added to DefaultItemExcludes, then the target framework unless
		///AppendTargetFrameworkToOutputPath is false); and the SDK's AutoGenerateBindingRedirects for a .NET Framework exe.</summary>
		private void ApplySdkBeforeCommonProperties() {
			bool isExecutable=IsValue("OutputType","Exe") || IsValue("OutputType","WinExe");
			if(isExecutable) {
				_dictProperties["_IsExecutable"]="true";
			}
			if(GetValue("HasRuntimeOutput")=="") {
				_dictProperties["HasRuntimeOutput"]=isExecutable ? "true" : "";
			}
			SetIfEmpty("PlatformName",GetValue("Platform"));
			string targetFramework=GetValue("TargetFramework");
			if(targetFramework=="" && GetValue("TargetFrameworks")!="") {
				throw new NotSupportedException(FullPath+": TargetFrameworks (more than one target framework) is not modelled by ProjectFile.");
			}
			bool isFrameworkKnown=true;
			if(targetFramework!="") {
				string frameworkIdentifier;
				string frameworkVersion;
				string platformIdentifier;
				string platformVersion;
				isFrameworkKnown=TryParseTargetFramework(targetFramework,out frameworkIdentifier,out frameworkVersion,out platformIdentifier,out platformVersion);
				if(!isFrameworkKnown) {
					foreach(string name in new[] { "TargetFrameworkIdentifier","TargetFrameworkVersion","TargetPlatformIdentifier","TargetPlatformVersion" }) {
						_dictProperties[name]=Unresolved("the "+name+" of TargetFramework "+targetFramework);
					}
				}
				else {
					if(GetValue("TargetFrameworkIdentifier")=="" || GetValue("TargetFrameworkVersion")=="") {
						_dictProperties["TargetFrameworkIdentifier"]=frameworkIdentifier;
						_dictProperties["TargetFrameworkVersion"]="v"+frameworkVersion;
					}
					if(GetValue("TargetPlatformIdentifier")=="" || GetValue("TargetPlatformVersion")=="") {
						SetIfEmpty("TargetPlatformIdentifier",platformIdentifier);
						if(IsValue("TargetPlatformIdentifier","Windows")) {
							_dictProperties["TargetPlatformIdentifier"]="Windows";
							SetIfEmpty("TargetPlatformVersion",platformVersion);
						}
					}
				}
			}
			_dictProperties["_TargetFrameworkVersionWithoutV"]=GetValue("TargetFrameworkVersion").TrimStart('v','V');
			Version versionFramework;
			if(IsValue("TargetFrameworkIdentifier",".NETCoreApp") && Version.TryParse(GetValue("_TargetFrameworkVersionWithoutV"),out versionFramework)
				&& versionFramework>=new Version(5,0))
			{
				_dictProperties["_EnableDefaultWindowsPlatform"]="false";
				if(GetValue("TargetPlatformIdentifier")!="" && GetValue("TargetPlatformVersion")=="") {
					//The SDK's platform files give it a default later on; not modelled.
					_hashSetNotModelled.Add("TargetPlatformVersion");
					_hashSetNotModelled.Add("TargetPlatformMoniker");
				}
			}
			//Microsoft.NET.DefaultOutputPaths.targets
			if(IsPropertyTrue("UseArtifactsOutput") || IsPropertyTrue("UseArtifactsIntermediateOutput")) {
				throw new NotSupportedException(FullPath+": the SDK's artifacts output layout (UseArtifactsOutput) is not modelled by ProjectFile.");
			}
			SetIfEmpty("AppendTargetFrameworkToOutputPath","true");
			SetIfEmpty("AppendPlatformToOutputPath",IsValue("PlatformName","AnyCPU") ? "false" : "true");
			string platformFolder="";
			if(IsPropertyTrue("AppendPlatformToOutputPath")) {
				platformFolder=GetValue("PlatformName")+@"\";
				_dictProperties["_PlatformToAppendToOutputPath"]=platformFolder;
			}
			SetIfEmpty("BaseOutputPath",@"bin\");
			AppendTrailingSlash("BaseOutputPath");
			SetIfEmpty("OutputPath",GetValue("BaseOutputPath")+platformFolder+GetValue("Configuration")+@"\");
			AppendTrailingSlash("OutputPath");
			SetIfEmpty("BaseIntermediateOutputPath",@"obj\");
			AppendTrailingSlash("BaseIntermediateOutputPath");
			SetIfEmpty("IntermediateOutputPath",GetValue("BaseIntermediateOutputPath")+platformFolder+GetValue("Configuration")+@"\");
			AppendTrailingSlash("IntermediateOutputPath");
			_dictProperties["DefaultItemExcludes"]=GetValue("DefaultItemExcludes")+";"+GetValue("OutputPath")+"/**;"+GetValue("IntermediateOutputPath")+"/**";
			if(IsPropertyTrue("AppendTargetFrameworkToOutputPath") && targetFramework!="") {
				foreach(string name in new[] { "OutputPath","IntermediateOutputPath" }) {
					_dictProperties[name]=isFrameworkKnown ? GetValue(name)+targetFramework.ToLowerInvariant()+@"\" : Unresolved("the "+name+" of TargetFramework "+targetFramework);
				}
			}
			//Microsoft.NET.RuntimeIdentifierInference.targets sets the platform target from the runtime identifier it infers when the project has
			//none (Directory.Build.props gives every SDK-style project x86); not modelled.
			if(GetValue("PlatformTarget")=="") {
				_hashSetNotModelled.Add("PlatformTarget");
			}
			if(IsValue("TargetFrameworkIdentifier",".NETFramework") && IsPropertyTrue("HasRuntimeOutput")) {
				SetIfEmpty("AutoGenerateBindingRedirects","true");
			}
		}

		///<summary>[MSBuild]::GetTargetFrameworkIdentifier, GetTargetFrameworkVersion(..., 2), GetTargetPlatformIdentifier and
		///GetTargetPlatformVersion for the short names NuGet gives the frameworks this repository targets or moves to: netNN[N] (.NET Framework,
		///one digit per version part: net48 is 4.8, net472 4.7.2), netX.Y from 5.0 on (.NET, with an optional -platform and platform version, as
		///net10.0-windows), netcoreappX.Y and netstandardX.Y. False for any other name.</summary>
		private static bool TryParseTargetFramework(string targetFramework,out string frameworkIdentifier,out string frameworkVersion,
			out string platformIdentifier,out string platformVersion)
		{
			frameworkIdentifier=null;
			frameworkVersion=null;
			platformIdentifier="";
			platformVersion="";
			Match match=Regex.Match(targetFramework.Trim(),@"^(net|netcoreapp|netstandard)([0-9][0-9.]*)(?:-([A-Za-z]+)([0-9][0-9.]*)?)?$",RegexOptions.IgnoreCase);
			if(!match.Success) {
				return false;
			}
			string prefix=match.Groups[1].Value.ToLowerInvariant();
			string digits=match.Groups[2].Value;
			List<int> listParts;
			if(prefix=="net" && !digits.Contains(".")) {
				frameworkIdentifier=".NETFramework";
				listParts=digits.Select(x => x-'0').ToList();
			}
			else {
				if(digits.EndsWith(".") || digits.Contains("..")) {
					return false;
				}
				listParts=digits.Split('.').Select(x => int.Parse(x,System.Globalization.CultureInfo.InvariantCulture)).ToList();
				if(prefix=="netstandard") {
					frameworkIdentifier=".NETStandard";
				}
				else if(prefix=="netcoreapp" || listParts[0]>=5) {
					frameworkIdentifier=".NETCoreApp";
				}
				else {
					frameworkIdentifier=".NETFramework";
				}
			}
			if(match.Groups[3].Success) {
				if(frameworkIdentifier!=".NETCoreApp" || listParts[0]<5) {
					return false;
				}
				platformIdentifier=match.Groups[3].Value;
				platformVersion=match.Groups[4].Value;
			}
			while(listParts.Count>2 && listParts[listParts.Count-1]==0) {
				listParts.RemoveAt(listParts.Count-1);
			}
			while(listParts.Count<2) {
				listParts.Add(0);
			}
			frameworkVersion=string.Join(".",listParts);
			return true;
		}

		///<summary>MSBuild's &lt;X Condition="!HasTrailingSlash('$(X)')"&gt;$(X)\&lt;/X&gt;.</summary>
		private void AppendTrailingSlash(string name) {
			string value=GetValue(name);
			if(!value.EndsWith(@"\") && !value.EndsWith("/")) {
				_dictProperties[name]=value+@"\";
			}
		}

		private void SetIfEmpty(string name,string value) {
			if(GetValue(name)=="") {
				_dictProperties[name]=value;
			}
		}

		///<summary>The property's value equals the text, as an MSBuild condition compares them: ignoring case.</summary>
		private bool IsValue(string name,string value) {
			return GetValue(name).Equals(value,StringComparison.OrdinalIgnoreCase);
		}

		private bool IsPropertyTrue(string name) {
			return IsValue(name,"true");
		}

		private bool IsPropertyFalse(string name) {
			return IsValue(name,"false");
		}

		///<summary>Microsoft.NET.Sdk.DefaultItems.props and Microsoft.NET.Sdk.WindowsDesktop.props (SDK 10.0.114).</summary>
		private void AddSdkDefaultItems() {
			if(!IsPropertyTrue("EnableDefaultItems")) {
				return;
			}
			string excludes=GetValue("DefaultItemExcludes")+";"+GetValue("DefaultExcludesInProjectFolder");
			string extension=GetValue("DefaultLanguageSourceExtension");
			if(IsPropertyTrue("EnableDefaultCompileItems")) {
				AddItems("Compile","**/*"+extension,excludes,new Dictionary<string,string>(),null);
			}
			if(IsPropertyTrue("EnableDefaultEmbeddedResourceItems")) {
				AddItems("EmbeddedResource","**/*.resx",excludes,new Dictionary<string,string>(),null);
			}
			if(IsPropertyTrue("EnableDefaultNoneItems")) {
				AddItems("None","**/*",excludes,new Dictionary<string,string>(),null);
				RemoveItems("None","**/*"+extension,null);
				RemoveItems("None","**/*.resx",null);
			}
			if(!IsWindowsDesktopGlobbing()) {
				return;
			}
			if(!IsPropertyFalse("EnableDefaultApplicationDefinition") && File.Exists(Path.Combine(Folder,"App.xaml"))
				&& Path.GetExtension(FullPath).Equals(".csproj",StringComparison.OrdinalIgnoreCase))
			{
				AddItems("ApplicationDefinition","App.xaml",null,new Dictionary<string,string>(),null);
			}
			if(!IsPropertyFalse("EnableDefaultPageItems")) {
				AddItems("Page","**/*.xaml",excludes+";@(ApplicationDefinition)",new Dictionary<string,string>(),null);
			}
			if(!IsPropertyFalse("EnableDefaultApplicationDefinition") && !IsPropertyFalse("EnableDefaultPageItems")) {
				RemoveItems("None","**/*.xaml",null);
			}
		}

		///<summary>The item part of Microsoft.NET.Sdk.WindowsDesktop.targets (SDK 10.0.114): a Page that is also a Resource, Content, the
		///ApplicationDefinition or an explicit None item is not a Page.</summary>
		private void ApplySdkTargetsItems() {
			if(!IsWindowsDesktopGlobbing()) {
				return;
			}
			if(!IsPropertyFalse("EnableDefaultPageItems")) {
				RemoveItems("Page","@(Resource);@(Content)",null);
			}
			RemoveItems("Page","@(ApplicationDefinition)",null);
			if(!IsPropertyFalse("EnableDefaultApplicationDefinition") && !IsPropertyFalse("EnableDefaultPageItems")) {
				RemoveItems("Page","@(None)",null);
			}
		}

		///<summary>_EnableWindowsDesktopGlobbing of Microsoft.NET.Sdk.WindowsDesktop.targets: default items on, UseWPF, and a target framework of
		///3.0 or later (every .NET Framework 4.x and .NET target this repository has).</summary>
		private bool IsWindowsDesktopGlobbing() {
			Version versionFramework;
			return IsPropertyTrue("EnableDefaultItems") && IsPropertyTrue("UseWPF")
				&& Version.TryParse(GetValue("_TargetFrameworkVersionWithoutV"),out versionFramework) && versionFramework>=new Version(3,0);
		}

		///<summary>The ItemDefinitionGroup of Microsoft.NET.Sdk.WindowsDesktop.props (SDK 10.0.114): with the WPF default globs on, every Page and
		///ApplicationDefinition item starts with Generator=MSBuild:Compile, XamlRuntime=$(DefaultXamlRuntime) and SubType=Designer, which its own
		///metadata then override.</summary>
		private void ApplyItemDefinitions(ProjectItem projectItem) {
			if(!projectItem.ItemType.Equals("Page",StringComparison.OrdinalIgnoreCase)
				&& !projectItem.ItemType.Equals("ApplicationDefinition",StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			if(!IsSdkStyle || !IsWindowsDesktopGlobbing()) {
				return;
			}
			//Before the item's own metadata; an item copied from another item list (Include="@(Page)") keeps the metadata it brought.
			foreach(string[] arrayDefinition in new[] { new[] { "Generator","MSBuild:Compile" },new[] { "XamlRuntime",GetValue("DefaultXamlRuntime") },
				new[] { "SubType","Designer" } })
			{
				if(!projectItem.HasMetadata(arrayDefinition[0])) {
					projectItem.SetMetadata(arrayDefinition[0],arrayDefinition[1]);
				}
			}
		}

		///<summary>The automatic Link metadata of Microsoft.NET.Sdk.DefaultItems.targets (SDK 10.0.114), unless SetLinkMetadataAutomatically is
		///false: an Update of every Compile, AdditionalFiles, None, Content, EmbeddedResource, Page and Resource item there is at that point (so
		///those of Directory.Build.targets too, but none after an explicit Sdk.targets import). LinkBase gets a trailing back slash, and an item
		///whose full path is not inside the project folder, with no Link and not from a shared project (.projitems), gets
		///Link=%(LinkBase)%(RecursiveDir)%(Filename)%(Extension): its file name, under the folders a wildcard matched.</summary>
		private void ApplySdkLinkMetadata() {
			if(IsPropertyFalse("SetLinkMetadataAutomatically")) {
				return;
			}
			string folderWithSlash=EnsureTrailingSlash(Folder);
			foreach(ProjectItem projectItem in _listItems.Where(x => _arrayLinkItemTypes.Contains(x.ItemType,StringComparer.OrdinalIgnoreCase))) {
				string linkBase=projectItem.GetMetadata("LinkBase");
				if(linkBase!="") {
					linkBase=EnsureTrailingSlash(linkBase);
					projectItem.SetMetadata("LinkBase",linkBase);
				}
				if(projectItem.GetMetadata("Link")!=""
					|| (projectItem.DefiningFile!=null && Path.GetExtension(projectItem.DefiningFile).Equals(".projitems",StringComparison.OrdinalIgnoreCase))
					|| (projectItem.FullPath??"").StartsWith(folderWithSlash,StringComparison.Ordinal))
				{
					continue;
				}
				projectItem.SetMetadata("Link",linkBase+projectItem.RecursiveDir+Path.GetFileNameWithoutExtension(projectItem.Include)
					+Path.GetExtension(projectItem.Include));
			}
		}
		#endregion MSBuild's and the SDK's own files

		#region Items
		///<summary>Pass 2 for one ItemGroup outside a target.</summary>
		private void EvaluateItemGroup(XElement xElementGroup,FileContext fileContext) {
			if(!IsConditionTrue(xElementGroup,fileContext)) {
				return;
			}
			foreach(XElement xElementItem in xElementGroup.Elements()) {
				if(!IsConditionTrue(xElementItem,fileContext)) {
					continue;
				}
				string itemType=xElementItem.Name.LocalName;
				string include=(string)xElementItem.Attribute("Include");
				string remove=(string)xElementItem.Attribute("Remove");
				string update=(string)xElementItem.Attribute("Update");
				if(include!=null) {
					AddItems(itemType,Expand(include,fileContext),xElementItem.Attribute("Exclude")==null ? null : Expand((string)xElementItem.Attribute("Exclude"),fileContext),
						ReadMetadata(xElementItem,fileContext),fileContext);
				}
				else if(remove!=null) {
					RemoveItems(itemType,Expand(remove,fileContext),fileContext);
				}
				else if(update!=null) {
					Dictionary<string,string> dictMetadata=ReadMetadata(xElementItem,fileContext);
					List<Func<ProjectItem,bool>> listMatchers=GetMatchers(Expand(update,fileContext),fileContext);
					//The item type ignoring case, as Include and Remove have it: MSBuild's item types are not case-sensitive.
					foreach(ProjectItem projectItem in _listItems.Where(x => x.ItemType.Equals(itemType,StringComparison.OrdinalIgnoreCase) && listMatchers.Any(y => y(x)))) {
						foreach(KeyValuePair<string,string> pair in dictMetadata) {
							projectItem.SetMetadata(pair.Key,ExpandWellKnownMetadata(pair.Value,projectItem));
						}
					}
				}
			}
		}

		///<summary>Metadata from child elements (with their conditions) and from attributes, the SDK-style short form. A value this reader cannot
		///evaluate throws.</summary>
		private Dictionary<string,string> ReadMetadata(XElement xElementItem,FileContext fileContext) {
			Dictionary<string,string> dictMetadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(XAttribute xAttribute in xElementItem.Attributes()) {
				if(xAttribute.IsNamespaceDeclaration || _arrayReservedAttributes.Contains(xAttribute.Name.LocalName,StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				dictMetadata[xAttribute.Name.LocalName]=Expand(xAttribute.Value,fileContext);
			}
			foreach(XElement xElementMetadata in xElementItem.Elements()) {
				if(IsConditionTrue(xElementMetadata,fileContext)) {
					dictMetadata[xElementMetadata.Name.LocalName]=Expand(xElementMetadata.Value.Trim(),fileContext);
				}
			}
			foreach(KeyValuePair<string,string> pair in dictMetadata) {
				ThrowIfUnresolved(pair.Value,fileContext.Path+": "+xElementItem.Name.LocalName+" metadata "+pair.Key);
			}
			return dictMetadata;
		}

		///<summary>Adds the items an Include names (already expanded): files, wildcards, item lists, separated by ';', less those its Exclude
		///names.</summary>
		private void AddItems(string itemType,string include,string exclude,Dictionary<string,string> dictMetadata,FileContext fileContext) {
			string where=fileContext==null ? "the .NET SDK's default "+itemType+" items" : fileContext.Path+": "+itemType+" Include=\""+include+"\"";
			ThrowIfUnresolved(include,where);
			List<Func<ProjectItem,bool>> listExcludes=exclude==null ? new List<Func<ProjectItem,bool>>() : GetMatchers(exclude,fileContext);
			List<Regex> listPrunedFolders=exclude==null ? new List<Regex>() : GetPrunedFolders(exclude);
			List<ProjectItem> listNew=new List<ProjectItem>();
			foreach(string spec in SplitList(include)) {
				Match matchItemList=Regex.Match(spec,@"^@\(\s*([A-Za-z_][\w.-]*)\s*\)$");
				if(matchItemList.Success) {
					foreach(ProjectItem projectItemSource in _listItems.Where(x => x.ItemType.Equals(matchItemList.Groups[1].Value,StringComparison.OrdinalIgnoreCase)).ToList()) {
						ProjectItem projectItem=new ProjectItem(itemType,projectItemSource.Include,projectItemSource.FullPath,false,fileContext?.Path);
						foreach(KeyValuePair<string,string> pair in projectItemSource.Metadata) {
							projectItem.SetMetadata(pair.Key,pair.Value);
						}
						listNew.Add(projectItem);
					}
					continue;
				}
				if(spec.Contains("@(") || spec.Contains("%(")) {
					throw new NotSupportedException(where+": item transforms and metadata references are not modelled by ProjectFile.");
				}
				if(spec.IndexOfAny(new[] { '*','?' })>=0) {
					foreach(string[] arrayFile in ExpandWildcard(spec,listPrunedFolders)) {
						ProjectItem projectItem=new ProjectItem(itemType,arrayFile[0],arrayFile[1],true,fileContext?.Path);
						projectItem.RecursiveDir=arrayFile[2];
						listNew.Add(projectItem);
					}
					continue;
				}
				listNew.Add(new ProjectItem(itemType,NormalizeSlashes(spec),TryGetFullPath(spec),false,fileContext?.Path));
			}
			foreach(ProjectItem projectItem in listNew) {
				if(listExcludes.Any(x => x(projectItem))) {
					continue;
				}
				ApplyItemDefinitions(projectItem);
				foreach(KeyValuePair<string,string> pair in dictMetadata) {
					projectItem.SetMetadata(pair.Key,ExpandWellKnownMetadata(pair.Value,projectItem));
				}
				_listItems.Add(projectItem);
			}
		}

		///<summary>%(Filename), %(Extension), %(RecursiveDir), %(Identity), %(FullPath) and %(RelativeDir) of the item itself in a metadata value,
		///as in Link="%(RecursiveDir)%(Filename)%(Extension)" of the third-party source builds. Other metadata references stay as written.
		///</summary>
		private static string ExpandWellKnownMetadata(string value,ProjectItem projectItem) {
			if(value==null || !value.Contains("%(")) {
				return value;
			}
			return Regex.Replace(value,@"%\(\s*(\w+)\s*\)",match => {
				string fullPath=projectItem.FullPath??"";
				switch(match.Groups[1].Value.ToLowerInvariant()) {
					case "filename":
						return Path.GetFileNameWithoutExtension(projectItem.Include);
					case "extension":
						return Path.GetExtension(projectItem.Include);
					case "recursivedir":
						return projectItem.RecursiveDir;
					case "identity":
						return projectItem.Include;
					case "fullpath":
						return fullPath;
					case "relativedir": {
							int index=projectItem.Include.LastIndexOf('\\');
							return index<0 ? "" : projectItem.Include.Substring(0,index+1);
						}
					default:
						return match.Value;
				}
			});
		}

		private void RemoveItems(string itemType,string remove,FileContext fileContext) {
			ThrowIfUnresolved(remove,(fileContext==null ? "the .NET SDK" : fileContext.Path)+": "+itemType+" Remove=\""+remove+"\"");
			List<Func<ProjectItem,bool>> listMatchers=GetMatchers(remove,fileContext);
			_listItems.RemoveAll(x => x.ItemType.Equals(itemType,StringComparison.OrdinalIgnoreCase) && listMatchers.Any(y => y(x)));
		}

		///<summary>One test per entry of an Exclude, Remove or Update list: an item list matches its items, a wildcard matches by full path, a
		///plain entry matches the same full path or the same Include text (a Reference by its assembly name).</summary>
		private List<Func<ProjectItem,bool>> GetMatchers(string list,FileContext fileContext) {
			ThrowIfUnresolved(list,(fileContext==null ? "the .NET SDK" : fileContext.Path)+": "+list);
			List<Func<ProjectItem,bool>> listMatchers=new List<Func<ProjectItem,bool>>();
			foreach(string spec in SplitList(list)) {
				Match matchItemList=Regex.Match(spec,@"^@\(\s*([A-Za-z_][\w.-]*)\s*\)$");
				if(matchItemList.Success) {
					List<ProjectItem> listReferenced=_listItems.Where(x => x.ItemType.Equals(matchItemList.Groups[1].Value,StringComparison.OrdinalIgnoreCase)).ToList();
					HashSet<string> hashSetFullPaths=new HashSet<string>(listReferenced.Where(x => x.FullPath!=null).Select(x => x.FullPath),StringComparer.OrdinalIgnoreCase);
					HashSet<string> hashSetIncludes=new HashSet<string>(listReferenced.Select(x => x.Include),StringComparer.OrdinalIgnoreCase);
					listMatchers.Add(x => (x.FullPath!=null && hashSetFullPaths.Contains(x.FullPath)) || hashSetIncludes.Contains(x.Include));
					continue;
				}
				if(spec.Contains("@(") || spec.Contains("%(")) {
					throw new NotSupportedException("Item transforms and metadata references are not modelled by ProjectFile: "+spec);
				}
				if(spec.IndexOfAny(new[] { '*','?' })>=0) {
					Regex regex=GetWildcardRegex(spec);
					listMatchers.Add(x => x.FullPath!=null && regex.IsMatch(x.FullPath));
					continue;
				}
				string fullPath=TryGetFullPath(spec);
				string include=NormalizeSlashes(spec);
				listMatchers.Add(x => IsSameItemSpec(x,fullPath,include));
			}
			return listMatchers;
		}

		private static bool IsSameItemSpec(ProjectItem projectItem,string fullPath,string include) {
			if(fullPath!=null && projectItem.FullPath!=null && projectItem.FullPath.Equals(fullPath,StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			return projectItem.Include.Equals(include,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The files a wildcard names: Include as MSBuild gives it (the part before the first wildcard as written, then the path below
		///it) and the full path. MSBuild's rules: '*' and '?' within one folder or file name, '**' any number of folders; never Windows' short names.
		///</summary>
		private List<string[]> ExpandWildcard(string spec,List<Regex> listPrunedFolders) {
			string normalized=NormalizeSlashes(spec);
			string[] arraySegments=normalized.Split('\\');
			int indexFirstWildcard=Array.FindIndex(arraySegments,x => x.IndexOfAny(new[] { '*','?' })>=0);
			string fixedPart=string.Join(@"\",arraySegments.Take(indexFirstWildcard));
			string baseFolder=fixedPart=="" ? Folder : Path.GetFullPath(Path.Combine(Folder,fixedPart+@"\"));
			baseFolder=baseFolder.TrimEnd('\\');
			string prefix=fixedPart=="" ? "" : fixedPart+@"\";
			List<string[]> listFiles=new List<string[]>();
			if(!Directory.Exists(baseFolder)) {
				return listFiles;
			}
			Regex regex=GetWildcardRegex(spec);
			bool isRecursive=arraySegments.Length-indexFirstWildcard>1 || arraySegments.Skip(indexFirstWildcard).Any(x => x.Contains("**"));
			foreach(string path in EnumerateFiles(baseFolder,isRecursive,listPrunedFolders)) {
				if(regex.IsMatch(path)) {
					string relative=path.Substring(baseFolder.Length+1);
					int indexLastSlash=relative.LastIndexOf('\\');
					//%(RecursiveDir): the folders the wildcard matched below the fixed part, with a trailing back slash.
					listFiles.Add(new[] { prefix+relative,path,indexLastSlash<0 ? "" : relative.Substring(0,indexLastSlash+1) });
				}
			}
			return listFiles;
		}

		private static IEnumerable<string> EnumerateFiles(string folder,bool isRecursive,List<Regex> listPrunedFolders) {
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(folder);
			while(stackFolders.Count>0) {
				string folderCur=stackFolders.Pop();
				foreach(string path in Directory.EnumerateFiles(folderCur).OrderBy(x => x,StringComparer.OrdinalIgnoreCase)) {
					yield return path;
				}
				if(!isRecursive) {
					continue;
				}
				foreach(string subfolder in Directory.EnumerateDirectories(folderCur).OrderByDescending(x => x,StringComparer.OrdinalIgnoreCase)) {
					if(!listPrunedFolders.Any(x => x.IsMatch(subfolder))) {
						stackFolders.Push(subfolder);
					}
				}
			}
		}

		///<summary>The folders a list's "folder\**" entries leave out whole (bin, obj, hidden folders), so they are not even read.</summary>
		private List<Regex> GetPrunedFolders(string list) {
			List<Regex> listRegexes=new List<Regex>();
			foreach(string spec in SplitList(list)) {
				string normalized=NormalizeSlashes(spec);
				if(!normalized.EndsWith(@"\**") || normalized.Contains("@(") || normalized.Contains("$(")) {
					continue;
				}
				string folderPattern=normalized.Substring(0,normalized.Length-3);
				if(folderPattern.IndexOfAny(new[] { '*','?' })<0) {
					listRegexes.Add(new Regex("^"+Regex.Escape(TryGetFullPath(folderPattern)??"\u0000")+"$",RegexOptions.IgnoreCase));
				}
				else {
					listRegexes.Add(GetWildcardRegex(folderPattern));
				}
			}
			return listRegexes;
		}

		///<summary>A full-path regex for a wildcard relative to the project folder (or rooted).</summary>
		private Regex GetWildcardRegex(string spec) {
			string normalized=NormalizeSlashes(spec);
			string[] arraySegments=normalized.Split('\\');
			int indexFirstWildcard=Array.FindIndex(arraySegments,x => x.IndexOfAny(new[] { '*','?' })>=0);
			string fixedPart=string.Join(@"\",arraySegments.Take(indexFirstWildcard));
			string baseFolder=(fixedPart=="" ? Folder : Path.GetFullPath(Path.Combine(Folder,fixedPart+@"\"))).TrimEnd('\\');
			StringBuilder stringBuilder=new StringBuilder("^"+Regex.Escape(baseFolder)+@"\\");
			for(int i=indexFirstWildcard;i<arraySegments.Length;i++) {
				bool isLast=i==arraySegments.Length-1;
				string segment=arraySegments[i];
				if(segment=="**") {
					stringBuilder.Append(isLast ? ".+" : @"(?:.*\\)?");
					continue;
				}
				foreach(char c in segment) {
					if(c=='*') {
						stringBuilder.Append(@"[^\\]*");
					}
					else if(c=='?') {
						stringBuilder.Append(@"[^\\]");
					}
					else {
						stringBuilder.Append(Regex.Escape(c.ToString()));
					}
				}
				if(!isLast) {
					stringBuilder.Append(@"\\");
				}
			}
			stringBuilder.Append("$");
			return new Regex(stringBuilder.ToString(),RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
		}

		private static IEnumerable<string> SplitList(string list) {
			return list.Split(';').Select(x => x.Trim()).Where(x => x!="");
		}

		///<summary>Back slashes only, no doubled ones (an SDK exclude such as "bin\/**" becomes "bin\**").</summary>
		private static string NormalizeSlashes(string spec) {
			string normalized=spec.Replace('/','\\');
			while(normalized.Contains(@"\\") && !normalized.StartsWith(@"\\")) {
				normalized=normalized.Replace(@"\\",@"\");
			}
			return normalized;
		}

		///<summary>The full path of an item spec relative to the project folder, or null when it is no path (an assembly name with characters a
		///path cannot hold).</summary>
		private string TryGetFullPath(string spec) {
			try {
				return Path.GetFullPath(Path.Combine(Folder,NormalizeSlashes(spec)));
			}
			catch(Exception) {
				return null;
			}
		}
		#endregion Items

		#region Properties and conditions
		///<summary>The evaluated value of a property, as MSBuild has it at the end of the evaluation: set by the project file or a file it imports
		///from the repository, or computed by this reader where MSBuild's or the SDK's own files compute it (region "MSBuild's and the SDK's own
		///files"), or "" when nothing sets it. Throws for a property that MSBuild, the solution build or the SDK compute and this reader does not
		///(_arrayNotModelledProperties, _arrayNotModelledSdkProperties) and for a value that holds an expression this reader cannot evaluate.
		///</summary>
		public string GetProperty(string name) {
			if(!_dictProperties.ContainsKey(name) && _hashSetNotModelled.Contains(name)) {
				throw new NotSupportedException(Describe()+": $("+name+") is computed by MSBuild, the solution build or the .NET SDK and not modelled by ProjectFile.");
			}
			string value=GetValue(name);
			ThrowIfUnresolved(value,Describe()+": $("+name+")");
			return value;
		}

		///<summary>True when the reader knows the property's value (GetProperty does not throw for it).</summary>
		public bool IsPropertyKnown(string name) {
			if(!_dictProperties.ContainsKey(name)) {
				return !_hashSetNotModelled.Contains(name);
			}
			return !IsUnresolved(GetValue(name));
		}

		///<summary>The value as it stands at this point of the evaluation, or "" when nothing set it; no check.</summary>
		private string GetValue(string name) {
			string value;
			return _dictProperties.TryGetValue(name,out value) ? value : "";
		}

		///<summary>The value of $(Name) in an expression: as GetValue, but a property MSBuild, the solution build or the SDK compute and this reader
		///does not becomes a marker, which makes the condition, include or import that uses it, directly or through another property, throw.
		///</summary>
		private string GetValueForExpression(string name) {
			if(!_dictProperties.ContainsKey(name) && _hashSetNotModelled.Contains(name)) {
				return Unresolved(name);
			}
			return GetValue(name);
		}

		private static bool IsUnresolved(string value) {
			return value!=null && value.Contains(UNRESOLVED_START);
		}

		///<summary>The files read: the project file, then each imported file of the repository in the order MSBuild imports it.</summary>
		public List<string> FilesRead {
			get { return _listFilesRead.ToList(); }
		}

		///<summary>Expands $(Name), the MSBuildThisFile* properties of the file being read, $(Name.EndsWith('x')) and the like, and the few
		///property functions the repository's project files use. Anything else becomes a marker that makes its user throw.</summary>
		private string Expand(string text,FileContext fileContext) {
			if(text==null || !text.Contains("$(")) {
				return text;
			}
			StringBuilder stringBuilder=new StringBuilder();
			int index=0;
			while(index<text.Length) {
				int indexStart=text.IndexOf("$(",index,StringComparison.Ordinal);
				if(indexStart<0) {
					stringBuilder.Append(text.Substring(index));
					break;
				}
				stringBuilder.Append(text.Substring(index,indexStart-index));
				int indexEnd=FindClosingParenthesis(text,indexStart+1);
				if(indexEnd<0) {
					stringBuilder.Append(text.Substring(indexStart));
					break;
				}
				string expression=text.Substring(indexStart+2,indexEnd-indexStart-2).Trim();
				stringBuilder.Append(EvaluatePropertyExpression(expression,fileContext));
				index=indexEnd+1;
			}
			return stringBuilder.ToString();
		}

		private static int FindClosingParenthesis(string text,int indexOpen) {
			int depth=0;
			bool isInQuote=false;
			for(int i=indexOpen;i<text.Length;i++) {
				char c=text[i];
				if(c=='\'') {
					isInQuote=!isInQuote;
				}
				else if(!isInQuote && c=='(') {
					depth++;
				}
				else if(!isInQuote && c==')') {
					depth--;
					if(depth==0) {
						return i;
					}
				}
			}
			return -1;
		}

		private string EvaluatePropertyExpression(string expression,FileContext fileContext) {
			if(Regex.IsMatch(expression,@"^[A-Za-z_][\w-]*$")) {
				return GetPropertyInFile(expression,fileContext);
			}
			//$([MSBuild]::Function(arguments)) and $([System.IO.Path]::Function(arguments))
			Match matchStatic=Regex.Match(expression,@"^\[([\w.]+)\]::(\w+)\((.*)\)$",RegexOptions.Singleline);
			if(matchStatic.Success) {
				List<string> listArguments=SplitArguments(matchStatic.Groups[3].Value).Select(x => ExpandArgument(x,fileContext)).ToList();
				if(listArguments.Any(x => x.Contains(UNRESOLVED_START))) {
					return Unresolved(expression);
				}
				string result=EvaluateStaticFunction(matchStatic.Groups[1].Value,matchStatic.Groups[2].Value,listArguments,fileContext);
				return result??Unresolved(expression);
			}
			//$(Name.Method('argument')), a string method on a property's value
			Match matchMethod=Regex.Match(expression,@"^([A-Za-z_][\w-]*)\.(\w+)\((.*)\)$",RegexOptions.Singleline);
			if(matchMethod.Success) {
				string value=GetPropertyInFile(matchMethod.Groups[1].Value,fileContext);
				List<string> listArguments=SplitArguments(matchMethod.Groups[3].Value).Select(x => ExpandArgument(x,fileContext)).ToList();
				if(IsUnresolved(value) || listArguments.Any(x => IsUnresolved(x))) {
					return Unresolved(expression);
				}
				string result=EvaluateStringMethod(value,matchMethod.Groups[2].Value,listArguments);
				return result??Unresolved(expression);
			}
			return Unresolved(expression);
		}

		private string GetPropertyInFile(string name,FileContext fileContext) {
			switch(name.ToLowerInvariant()) {
				case "msbuildthisfile":
					return Path.GetFileName(fileContext.Path);
				case "msbuildthisfiledirectory":
					return fileContext.Directory;
				case "msbuildthisfilefullpath":
					return fileContext.Path;
				case "msbuildthisfilename":
					return Path.GetFileNameWithoutExtension(fileContext.Path);
				case "msbuildthisfileextension":
					return Path.GetExtension(fileContext.Path);
				case "msbuildthisfiledirectorynoroot":
					return fileContext.Directory.Substring(Path.GetPathRoot(fileContext.Directory).Length);
				default:
					return GetValueForExpression(name);
			}
		}

		private string ExpandArgument(string argument,FileContext fileContext) {
			string trimmed=argument.Trim();
			if(trimmed.Length>=2 && trimmed[0]=='\'' && trimmed[trimmed.Length-1]=='\'') {
				trimmed=trimmed.Substring(1,trimmed.Length-2);
			}
			return Expand(trimmed,fileContext);
		}

		private static List<string> SplitArguments(string text) {
			List<string> listArguments=new List<string>();
			if(text.Trim()=="") {
				return listArguments;
			}
			int depth=0;
			bool isInQuote=false;
			int indexStart=0;
			for(int i=0;i<text.Length;i++) {
				char c=text[i];
				if(c=='\'') {
					isInQuote=!isInQuote;
				}
				else if(!isInQuote && c=='(') {
					depth++;
				}
				else if(!isInQuote && c==')') {
					depth--;
				}
				else if(!isInQuote && depth==0 && c==',') {
					listArguments.Add(text.Substring(indexStart,i-indexStart));
					indexStart=i+1;
				}
			}
			listArguments.Add(text.Substring(indexStart));
			return listArguments;
		}

		private string EvaluateStaticFunction(string typeName,string function,List<string> listArguments,FileContext fileContext) {
			if(typeName.Equals("MSBuild",StringComparison.OrdinalIgnoreCase)) {
				switch(function.ToLowerInvariant()) {
					case "normalizedirectory":
						return EnsureTrailingSlash(Path.GetFullPath(Path.Combine(Folder,CombineAll(listArguments))));
					case "normalizepath":
						return Path.GetFullPath(Path.Combine(Folder,CombineAll(listArguments)));
					case "ensuretrailingslash":
						return listArguments.Count==1 ? EnsureTrailingSlash(listArguments[0]) : null;
					case "getpathoffileabove": {
							if(listArguments.Count<1) {
								return null;
							}
							string startFolder=listArguments.Count>1 && listArguments[1]!="" ? Path.GetFullPath(Path.Combine(Folder,listArguments[1])) : fileContext.Directory;
							return FindFileAbove(listArguments[0],startFolder)??"";
						}
					case "getdirectorynameoffileabove": {
							if(listArguments.Count!=2) {
								return null;
							}
							string path=FindFileAbove(listArguments[1],Path.GetFullPath(Path.Combine(Folder,listArguments[0])));
							return path==null ? "" : Path.GetDirectoryName(path);
						}
					default:
						return null;
				}
			}
			if(typeName.Equals("System.IO.Path",StringComparison.OrdinalIgnoreCase)) {
				switch(function.ToLowerInvariant()) {
					case "combine":
						return CombineAll(listArguments);
					case "getfullpath":
						return listArguments.Count==1 ? Path.GetFullPath(Path.Combine(Folder,listArguments[0])) : null;
					case "getfilename":
						return listArguments.Count==1 ? Path.GetFileName(listArguments[0]) : null;
					case "getdirectoryname":
						return listArguments.Count==1 ? Path.GetDirectoryName(listArguments[0]) : null;
					default:
						return null;
				}
			}
			return null;
		}

		private static string CombineAll(List<string> listArguments) {
			string path="";
			foreach(string argument in listArguments) {
				path=Path.Combine(path,argument.Replace('/','\\'));
			}
			return path;
		}

		private static string EnsureTrailingSlash(string path) {
			return path=="" || path.EndsWith(@"\") || path.EndsWith("/") ? path : path+@"\";
		}

		private static string EvaluateStringMethod(string value,string method,List<string> listArguments) {
			switch(method.ToLowerInvariant()) {
				case "endswith":
					return listArguments.Count==1 ? (value.EndsWith(listArguments[0],StringComparison.Ordinal) ? "true" : "false") : null;
				case "startswith":
					return listArguments.Count==1 ? (value.StartsWith(listArguments[0],StringComparison.Ordinal) ? "true" : "false") : null;
				case "contains":
					return listArguments.Count==1 ? (value.Contains(listArguments[0]) ? "true" : "false") : null;
				case "tolower":
				case "tolowerinvariant":
					return listArguments.Count==0 ? value.ToLowerInvariant() : null;
				case "toupper":
				case "toupperinvariant":
					return listArguments.Count==0 ? value.ToUpperInvariant() : null;
				case "trim":
					return listArguments.Count==0 ? value.Trim() : null;
				case "trimend":
					return listArguments.Count==1 && listArguments[0].Length>0 ? value.TrimEnd(listArguments[0].ToCharArray()) : null;
				case "replace":
					return listArguments.Count==2 && listArguments[0]!="" ? value.Replace(listArguments[0],listArguments[1]) : null;
				default:
					return null;
			}
		}

		private static string Unresolved(string expression) {
			return UNRESOLVED_START+expression+UNRESOLVED_END;
		}

		private static void ThrowIfUnresolved(string value,string where) {
			if(value==null) {
				return;
			}
			int index=value.IndexOf(UNRESOLVED_START,StringComparison.Ordinal);
			if(index<0) {
				return;
			}
			int indexEnd=value.IndexOf(UNRESOLVED_END,index,StringComparison.Ordinal);
			string expression=value.Substring(index+UNRESOLVED_START.Length,(indexEnd<0 ? value.Length : indexEnd)-index-UNRESOLVED_START.Length);
			throw new NotSupportedException(where+": ProjectFile cannot evaluate $("+expression+").");
		}

		private static string FindFileAbove(string fileName,string startFolder) {
			DirectoryInfo directoryInfo=new DirectoryInfo(startFolder.TrimEnd('\\'));
			while(directoryInfo!=null) {
				string path=Path.Combine(directoryInfo.FullName,fileName);
				if(File.Exists(path)) {
					return path;
				}
				directoryInfo=directoryInfo.Parent;
			}
			return null;
		}

		private bool IsConditionTrue(XElement xElement,FileContext fileContext) {
			string condition=(string)xElement.Attribute("Condition");
			if(condition==null || condition.Trim()=="") {
				return true;
			}
			//Exists('relative') is relative to the folder of the file the condition is in, as MSBuild has it for imported files.
			return new ConditionParser(condition,x => Expand(x,fileContext),x => Path.Combine(fileContext.Directory,x),fileContext.Path).Parse();
		}

		///<summary>MSBuild's condition grammar as the repository uses it: 'string' and unquoted operands (properties expanded), == != &lt; &gt;
		///&lt;= &gt;= (numbers), ! and or, parentheses, Exists('path') and HasTrailingSlash('path'), true and false.</summary>
		private sealed class ConditionParser {
			private readonly string _condition;
			private readonly Func<string,string> _funcExpand;
			private readonly Func<string,string> _funcFullPath;
			private readonly string _where;
			private int _index;

			public ConditionParser(string condition,Func<string,string> funcExpand,Func<string,string> funcFullPath,string where) {
				_condition=condition;
				_funcExpand=funcExpand;
				_funcFullPath=funcFullPath;
				_where=where;
			}

			public bool Parse() {
				bool result=ParseOr();
				SkipWhiteSpace();
				if(_index<_condition.Length) {
					throw Error("unexpected text at "+_index);
				}
				return result;
			}

			private bool ParseOr() {
				bool result=ParseAnd();
				while(TryKeyword("or")) {
					bool right=ParseAnd();
					result=result || right;
				}
				return result;
			}

			private bool ParseAnd() {
				bool result=ParseUnary();
				while(TryKeyword("and")) {
					bool right=ParseUnary();
					result=result && right;
				}
				return result;
			}

			private bool ParseUnary() {
				SkipWhiteSpace();
				if(Peek()=='!' && PeekAt(1)!='=') {
					_index++;
					return !ParseUnary();
				}
				if(Peek()=='(') {
					_index++;
					bool result=ParseOr();
					SkipWhiteSpace();
					if(Peek()!=')') {
						throw Error("missing )");
					}
					_index++;
					return result;
				}
				Match matchFunction=new Regex(@"\G(Exists|HasTrailingSlash)\s*\(",RegexOptions.IgnoreCase).Match(_condition,_index);
				if(matchFunction.Success) {
					_index+=matchFunction.Length;
					string argument=ParseOperand();
					SkipWhiteSpace();
					if(Peek()!=')') {
						throw Error("missing ) after "+matchFunction.Groups[1].Value);
					}
					_index++;
					if(matchFunction.Groups[1].Value.Equals("Exists",StringComparison.OrdinalIgnoreCase)) {
						string trimmed=argument.Trim();
						return trimmed!="" && (File.Exists(_funcFullPath(trimmed)) || Directory.Exists(_funcFullPath(trimmed)));
					}
					return argument.EndsWith(@"\") || argument.EndsWith("/");
				}
				string left=ParseOperand();
				SkipWhiteSpace();
				string op=TryOperator();
				if(op==null) {
					if(left.Equals("true",StringComparison.OrdinalIgnoreCase)) {
						return true;
					}
					if(left.Equals("false",StringComparison.OrdinalIgnoreCase)) {
						return false;
					}
					throw Error("'"+left+"' is not a boolean");
				}
				string right=ParseOperand();
				switch(op) {
					case "==":
						return left.Equals(right,StringComparison.OrdinalIgnoreCase);
					case "!=":
						return !left.Equals(right,StringComparison.OrdinalIgnoreCase);
					default:
						double numberLeft;
						double numberRight;
						if(!double.TryParse(left,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out numberLeft)
							|| !double.TryParse(right,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out numberRight))
						{
							throw Error("'"+left+"' "+op+" '"+right+"' compares no numbers");
						}
						switch(op) {
							case "<": return numberLeft<numberRight;
							case ">": return numberLeft>numberRight;
							case "<=": return numberLeft<=numberRight;
							default: return numberLeft>=numberRight;
						}
				}
			}

			///<summary>A quoted string or an unquoted token (a property, a number, true or false), expanded.</summary>
			private string ParseOperand() {
				SkipWhiteSpace();
				string raw;
				if(Peek()=='\'') {
					int indexEnd=_condition.IndexOf('\'',_index+1);
					if(indexEnd<0) {
						throw Error("unterminated string");
					}
					raw=_condition.Substring(_index+1,indexEnd-_index-1);
					_index=indexEnd+1;
				}
				else if(Peek()=='$' && PeekAt(1)=='(') {
					int indexEnd=FindClosingParenthesis(_condition,_index+1);
					if(indexEnd<0) {
						throw Error("unterminated $(");
					}
					raw=_condition.Substring(_index,indexEnd-_index+1);
					_index=indexEnd+1;
				}
				else {
					Match match=new Regex(@"\G[\w.\-]+").Match(_condition,_index);
					if(!match.Success) {
						throw Error("no operand at "+_index);
					}
					raw=match.Value;
					_index+=match.Length;
				}
				if(raw.Contains("@(") || raw.Contains("%(")) {
					throw Error("item lists and metadata are not modelled by ProjectFile in conditions");
				}
				string value=_funcExpand(raw);
				if(value.Contains(UNRESOLVED_START)) {
					ThrowIfUnresolved(value,_where+": Condition=\""+_condition+"\"");
				}
				return value;
			}

			private string TryOperator() {
				foreach(string op in new[] { "==","!=","<=",">=","<",">" }) {
					if(string.CompareOrdinal(_condition,_index,op,0,op.Length)==0) {
						_index+=op.Length;
						return op;
					}
				}
				return null;
			}

			private bool TryKeyword(string keyword) {
				SkipWhiteSpace();
				Match match=new Regex(@"\G"+keyword+@"\b",RegexOptions.IgnoreCase).Match(_condition,_index);
				if(!match.Success) {
					return false;
				}
				_index+=match.Length;
				return true;
			}

			private void SkipWhiteSpace() {
				while(_index<_condition.Length && char.IsWhiteSpace(_condition[_index])) {
					_index++;
				}
			}

			private char Peek() {
				return _index<_condition.Length ? _condition[_index] : '\0';
			}

			private char PeekAt(int offset) {
				return _index+offset<_condition.Length ? _condition[_index+offset] : '\0';
			}

			private Exception Error(string message) {
				return new NotSupportedException(_where+": ProjectFile cannot evaluate Condition=\""+_condition+"\": "+message+".");
			}
		}
		#endregion Properties and conditions

		#region Queries
		///<summary>The file name of the project file.</summary>
		public string Name {
			get { return Path.GetFileName(FullPath); }
		}

		///<summary>Every item of the type, in MSBuild's order. A file both globbed and listed appears twice, as MSBuild has it (the SDK then stops
		///the build with NETSDK1022 for Compile, EmbeddedResource and Page).</summary>
		public List<ProjectItem> GetItems(string itemType) {
			return _listItems.Where(x => x.ItemType.Equals(itemType,StringComparison.OrdinalIgnoreCase)).ToList();
		}

		///<summary>Every item, of every type.</summary>
		public List<ProjectItem> GetAllItems() {
			return _listItems.ToList();
		}

		///<summary>The first item of the type for the path (relative to the project folder, '\' or '/', or full) or the plain text (a Reference's
		///Include), or null.</summary>
		public ProjectItem GetItem(string itemType,string spec) {
			string fullPath=TryGetFullPath(spec);
			string include=NormalizeSlashes(spec);
			return _listItems.FirstOrDefault(x => x.ItemType.Equals(itemType,StringComparison.OrdinalIgnoreCase) && IsSameItemSpec(x,fullPath,include));
		}

		public bool HasItem(string itemType,string spec) {
			return GetItem(itemType,spec)!=null;
		}

		///<summary>True when the project compiles the file (a Compile item, explicit or from a wildcard or the SDK's default glob).</summary>
		public bool Compiles(string path) {
			return HasItem("Compile",path);
		}

		///<summary>The Reference item for an assembly, by its simple name: Include="Name" and Include="Name, Version=..." alike.</summary>
		public ProjectItem GetReference(string assemblyName) {
			return GetItems("Reference").FirstOrDefault(x => x.Include.Split(',')[0].Trim().Equals(assemblyName,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The ProjectReference item for a project file (a path relative to this project's folder, or full), or null.</summary>
		public ProjectItem GetProjectReference(string path) {
			return GetItem("ProjectReference",path);
		}

		///<summary>The .cs files on disk in the folder (relative to the project folder) and below it, outside bin and obj, that the project does not
		///compile. Throws when the folder has none at all, so an empty answer is never vacuous.</summary>
		public List<string> GetFilesNotCompiled(string relativeFolder) {
			return GetSourceFiles(relativeFolder).Where(x => !Compiles(x)).Select(x => x.Substring(Folder.Length+1)).ToList();
		}

		///<summary>The .cs files on disk in the folder and below it, outside bin and obj (as GetFilesNotCompiled finds them), that no wildcard
		///compiles, or that an item naming the file itself compiles as well: the files a classic project file lists one by one, or an SDK-style one
		///names again (NETSDK1022). Empty when a wildcard compiles the whole folder (a classic folder wildcard such as VendorDirect\**\*.cs, or the
		///SDK's default glob), so a file added to the folder needs no line of its own in the project file. Throws when the folder has no .cs files.
		///</summary>
		public List<string> GetFilesNotCompiledByWildcard(string relativeFolder) {
			List<string> listFiles=new List<string>();
			foreach(string path in GetSourceFiles(relativeFolder)) {
				List<ProjectItem> listItems=_listItems.Where(x => x.ItemType.Equals("Compile",StringComparison.OrdinalIgnoreCase)
					&& x.FullPath!=null && x.FullPath.Equals(path,StringComparison.OrdinalIgnoreCase)).ToList();
				if(listItems.Count==0 || listItems.Any(x => !x.IsFromWildcard)) {
					listFiles.Add(path.Substring(Folder.Length+1));
				}
			}
			return listFiles;
		}

		///<summary>The full paths of the .cs files in the folder (relative to the project folder) and below it, outside bin and obj. Throws when
		///there are none, so an empty answer of the callers is never vacuous.</summary>
		private List<string> GetSourceFiles(string relativeFolder) {
			string folder=Path.GetFullPath(Path.Combine(Folder,relativeFolder));
			List<string> listFiles=Directory.Exists(folder)
				? Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(x => x.EndsWith(".cs",StringComparison.OrdinalIgnoreCase))
					.Where(x => !Regex.IsMatch(x.Substring(folder.Length),@"\\(bin|obj)\\",RegexOptions.IgnoreCase)).ToList()
				: new List<string>();
			if(listFiles.Count==0) {
				throw new InvalidOperationException(Name+": no .cs files under "+relativeFolder);
			}
			return listFiles;
		}

		///<summary>The items of any of the types whose Include mentions the text (case-insensitive), for checks that a removed file or library is
		///gone from the project in either format.</summary>
		public List<ProjectItem> GetItemsMentioning(string text,params string[] arrayItemTypes) {
			return _listItems.Where(x => arrayItemTypes.Length==0 || arrayItemTypes.Contains(x.ItemType,StringComparer.OrdinalIgnoreCase))
				.Where(x => x.Include.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)
				.ToList();
		}

		///<summary>Fails the test unless the project has an item of the type for the path; returns the item for metadata checks.</summary>
		public ProjectItem AssertItem(string itemType,string spec) {
			ProjectItem projectItem=GetItem(itemType,spec);
			if(projectItem==null) {
				Assert.Fail(Describe()+" has no "+itemType+" item for "+spec+".");
			}
			return projectItem;
		}

		///<summary>Fails the test unless the project compiles every file (paths relative to the project folder).</summary>
		public void AssertCompiles(params string[] arrayPaths) {
			List<string> listMissing=arrayPaths.Where(x => !Compiles(x)).ToList();
			if(listMissing.Count>0) {
				Assert.Fail(Describe()+" does not compile "+string.Join(", ",listMissing)+".");
			}
		}

		///<summary>Fails the test unless the project embeds every resource file (EmbeddedResource items, paths relative to the project folder).
		///</summary>
		public void AssertEmbeds(params string[] arrayPaths) {
			List<string> listMissing=arrayPaths.Where(x => !HasItem("EmbeddedResource",x)).ToList();
			if(listMissing.Count>0) {
				Assert.Fail(Describe()+" does not embed "+string.Join(", ",listMissing)+".");
			}
		}

		///<summary>Fails the test unless the project builds every XAML file as a WPF page (Page items, paths relative to the project folder).
		///</summary>
		public void AssertPages(params string[] arrayPaths) {
			List<string> listMissing=arrayPaths.Where(x => !HasItem("Page",x)).ToList();
			if(listMissing.Count>0) {
				Assert.Fail(Describe()+" does not build the page "+string.Join(", ",listMissing)+".");
			}
		}

		///<summary>Fails the test if the project compiles any of the files.</summary>
		public void AssertDoesNotCompile(params string[] arrayPaths) {
			List<string> listFound=arrayPaths.Where(x => Compiles(x)).ToList();
			if(listFound.Count>0) {
				Assert.Fail(Describe()+" still compiles "+string.Join(", ",listFound)+".");
			}
		}

		///<summary>The project file and its format, for messages.</summary>
		public string Describe() {
			return Name+" ("+(IsSdkStyle ? "SDK-style" : "classic")+", "+Configuration+"|"+Platform+")";
		}
		#endregion Queries

		private sealed class FileContext {
			public readonly string Path;
			///<summary>With a trailing back slash, as $(MSBuildThisFileDirectory).</summary>
			public readonly string Directory;

			public FileContext(string path) {
				Path=path;
				Directory=System.IO.Path.GetDirectoryName(path)+@"\";
			}
		}

		private sealed class ItemStep {
			public XElement XElementGroup;
			public FileContext FileContext;
			public string Builtin;
		}

		///<summary>The project mappings of a solution file: for each project, by its full path, the configuration and platform that each
		///solution configuration and platform builds it with (the ActiveCfg lines of ProjectConfigurationPlatforms).</summary>
		private sealed class SolutionFile {
			public readonly DateTime DateTimeWriteUtc;
			private readonly Dictionary<string,string> _dictGuids=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			private readonly Dictionary<string,string[]> _dictMappings=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);

			public SolutionFile(string pathSln,DateTime dateTimeWriteUtc) {
				DateTimeWriteUtc=dateTimeWriteUtc;
				string folder=Path.GetDirectoryName(pathSln);
				string text=File.ReadAllText(pathSln);
				foreach(Match match in Regex.Matches(text,@"^Project\(""\{[^}]+\}""\)\s*=\s*""[^""]*""\s*,\s*""([^""]+)""\s*,\s*""(\{[^}]+\})""",RegexOptions.Multiline)) {
					string relative=match.Groups[1].Value.Replace('/','\\');
					if(relative.IndexOfAny(Path.GetInvalidPathChars())>=0) {
						continue;
					}
					_dictGuids[Path.GetFullPath(Path.Combine(folder,relative))]=match.Groups[2].Value;
				}
				foreach(Match match in Regex.Matches(text,@"^\s*(\{[^}]+\})\.([^\r\n=]+?)\.ActiveCfg\s*=\s*([^|\r\n]+)\|([^\r\n]+?)\s*$",RegexOptions.Multiline)) {
					_dictMappings[match.Groups[1].Value+"."+match.Groups[2].Value.Trim()]=new[] { match.Groups[3].Value.Trim(),match.Groups[4].Value.Trim() };
				}
			}

			///<summary>{configuration, platform} for the project and the solution's "Configuration|Platform", or null when the solution has no
			///such mapping. "Any CPU" becomes AnyCPU, the value the solution build gives the project.</summary>
			public string[] GetMapping(string pathProject,string solutionConfigurationPlatform) {
				string guid;
				string[] arrayMapping;
				if(!_dictGuids.TryGetValue(pathProject,out guid) || !_dictMappings.TryGetValue(guid+"."+solutionConfigurationPlatform,out arrayMapping)) {
					return null;
				}
				return new[] { arrayMapping[0],arrayMapping[1].Equals("Any CPU",StringComparison.OrdinalIgnoreCase) ? "AnyCPU" : arrayMapping[1] };
			}
		}
	}

	///<summary>One evaluated item.</summary>
	internal sealed class ProjectItem {
		public string ItemType { get; private set; }
		///<summary>The item's evaluated Include: the path as the project names it (relative to the project folder, '\'), or a wildcard's match
		///with the wildcard's leading folders as written, or plain text such as an assembly name.</summary>
		public string Include { get; private set; }
		///<summary>The full path the Include names relative to the project folder, or null when it cannot be a path.</summary>
		public string FullPath { get; private set; }
		///<summary>True when a wildcard (the project's own or the SDK's default glob) gave the item.</summary>
		public bool IsFromWildcard { get; private set; }
		///<summary>The file the item element is in (the project file or an imported file), or null for the SDK's default items.</summary>
		public string DefiningFile { get; private set; }
		///<summary>%(RecursiveDir) of an item a wildcard gave: the folders below the wildcard's fixed part, with a trailing back slash; else "".
		///</summary>
		public string RecursiveDir { get; internal set; }
		private readonly Dictionary<string,string> _dictMetadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

		public ProjectItem(string itemType,string include,string fullPath,bool isFromWildcard,string definingFile) {
			ItemType=itemType;
			Include=include;
			FullPath=fullPath;
			IsFromWildcard=isFromWildcard;
			DefiningFile=definingFile;
			RecursiveDir="";
		}

		///<summary>The metadata value, or "" when the item has none of that name (as %(Name) gives it).</summary>
		public string GetMetadata(string name) {
			string value;
			return _dictMetadata.TryGetValue(name,out value) ? value : "";
		}

		public bool HasMetadata(string name) {
			return _dictMetadata.ContainsKey(name);
		}

		public IEnumerable<KeyValuePair<string,string>> Metadata {
			get { return _dictMetadata.ToList(); }
		}

		internal void SetMetadata(string name,string value) {
			_dictMetadata[name]=value;
		}

		public override string ToString() {
			return ItemType+" "+Include;
		}
	}

	///<summary>The item elements of any MSBuild file as written (no evaluation), for checks over every project, props and targets file of the
	///repository, such as "no file names a removed library": namespace-agnostic, metadata from child elements and attributes alike (an SDK-style
	///file may write HintPath="..." where a classic one has a HintPath element), items inside targets included.</summary>
	internal static class ProjectXml {
		public static List<ProjectItemElement> ReadItemElements(string path) {
			XDocument xDocument=XDocument.Load(path);
			List<ProjectItemElement> listItems=new List<ProjectItemElement>();
			foreach(XElement xElementGroup in xDocument.Descendants().Where(x => x.Name.LocalName=="ItemGroup")) {
				foreach(XElement xElementItem in xElementGroup.Elements()) {
					listItems.Add(new ProjectItemElement(path,xElementItem));
				}
			}
			return listItems;
		}
	}

	///<summary>An item element as written.</summary>
	internal sealed class ProjectItemElement {
		public string File { get; private set; }
		public string ItemType { get; private set; }
		///<summary>Include, or "" when the element has none.</summary>
		public string Include { get; private set; }
		public string Update { get; private set; }
		public string Remove { get; private set; }
		public XElement XElement { get; private set; }
		private readonly Dictionary<string,string> _dictMetadata=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

		public ProjectItemElement(string file,XElement xElement) {
			File=file;
			XElement=xElement;
			ItemType=xElement.Name.LocalName;
			Include=(string)xElement.Attribute("Include")??"";
			Update=(string)xElement.Attribute("Update")??"";
			Remove=(string)xElement.Attribute("Remove")??"";
			foreach(XAttribute xAttribute in xElement.Attributes()) {
				if(!xAttribute.IsNamespaceDeclaration && !new[] { "Include","Exclude","Remove","Update","Condition" }.Contains(xAttribute.Name.LocalName)) {
					_dictMetadata[xAttribute.Name.LocalName]=xAttribute.Value;
				}
			}
			foreach(XElement xElementMetadata in xElement.Elements()) {
				_dictMetadata[xElementMetadata.Name.LocalName]=xElementMetadata.Value.Trim();
			}
		}

		///<summary>Include, Update and Remove: every value that names a file or an assembly.</summary>
		public IEnumerable<string> Specs {
			get { return new[] { Include,Update,Remove }.Where(x => x!=""); }
		}

		public string GetMetadata(string name) {
			string value;
			return _dictMetadata.TryGetValue(name,out value) ? value : "";
		}

		public override string ToString() {
			return "<"+ItemType+" "+(Include!="" ? "Include" : Update!="" ? "Update" : "Remove")+"=\""+Specs.FirstOrDefault()+"\">";
		}
	}
}
