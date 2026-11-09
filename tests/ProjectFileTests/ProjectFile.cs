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
	///What it models: the properties of the project file and of the files it imports from the repository (Directory.Build.props above the
	///project, through Microsoft.Common.props, which the project imports itself or the common targets import for it; Directory.Build.targets;
	///relative imports such as third-party\build\SourceBuild.props; the project's .user file; the project extension files of NuGet's restore,
	///obj\Project.csproj.*.props and .*.targets, and the build files of packages they import, ReadPackageImport), with their conditions; the
	///properties that MSBuild's and the SDK's own files compute for the projects of the repository, each at the point of the evaluation where
	///MSBuild computes it (the output and intermediate paths, the target framework, AssemblyName, OutputType, TargetPath, Prefer32Bit,
	///PublishDir, the import switches and paths and the like, and for an SDK-style C# project the SDK's defaults and the compiler defaults of
	///Microsoft.NET.Sdk.props, Microsoft.NET.Sdk.CSharp.props and .targets and Microsoft.NET.Sdk.Analyzers.targets: DefineConstants with TRACE
	///and the configuration's define, NoWarn 1701;1702, DebugType, Optimize, WarningLevel and the like: region "MSBuild's and the SDK's own
	///files"), as MSBuild -getProperty gives them, MSBuild's %XX escapes unescaped where MSBuild unescapes them; the items of those files in
	///MSBuild's order, with Include (a file, a list, a wildcard, an item list), Exclude, Remove and Update, and their metadata from child
	///elements and attributes alike; for an SDK-style project the .NET SDK's default items before the project's own (Compile **\*.cs,
	///EmbeddedResource **\*.resx and None **\* without bin, obj, hidden folders and the DefaultItemExcludes; with UseWPF, Page **\*.xaml and
	///App.xaml as ApplicationDefinition, with the WindowsDesktop SDK's Page metadata), switched off by EnableDefaultItems,
	///EnableDefaultCompileItems and the other switches, and the Link metadata the SDK gives an item outside the project folder
	///(SetLinkMetadataAutomatically), exactly as Microsoft.NET.Sdk.DefaultItems.props/.targets and Microsoft.NET.Sdk.WindowsDesktop.props/.targets
	///of the pinned SDK 10.0.114 define them.
	///What it does not model: the rest of the files of MSBuild, Visual Studio's components, the .NET Framework and the SDK (those the common
	///targets import from MSBuild's ImportBefore and ImportAfter folders, Microsoft.WinFx.targets, the web application targets), targets (items
	///created while building), ItemDefinitionGroup defaults other than the WindowsDesktop SDK's, Choose, and environment variables, which MSBuild
	///reads as properties. Anything it cannot evaluate throws, so a check never passes on a guess: Choose, a missing import, an import of another
	///file of MSBuild, Visual Studio or the SDK, an unknown property function in an Include or a condition, an environment variable no file set,
	///and a property that MSBuild, its toolset, Visual Studio's components or the SDK compute and this reader does not (the machine paths such
	///as $(MSBuildExtensionsPath), NuGet's $(NuGetTargetMoniker), the SDK's $(RuntimeIdentifier), a web application's $(AppConfig): the
	///not-modelled lists _arrayNotModelledProperties, _arrayNotModelledToolsetProperties, _arrayNotModelledSdkProperties,
	///_arrayWebApplicationProperties and, for an SDK-style project in another language than C#, _arraySdkLanguageProperties, each from the point
	///of the evaluation where MSBuild's files set them) used in a Condition, an Include or an Import, directly or through another property, or
	///asked of GetProperty; and an SDK other than Microsoft.NET.Sdk. A property on none of those lists that MSBuild's own files set and this
	///reader does not compute still reads as '' (a full MSBuild -pp listing has several hundred such names per project, none of them used by
	///the repository's project files or tests): GetProperty is exact for the names tools\Test-ProjectFileEvaluator.ps1 checks, which compares
	///it with MSBuild -getProperty on every project of the solution filter for a long list of names, and every stage gate runs it. A test that
	///needs another computed name adds it to that script's lists first.
	///Environment variables (package READER-ENV, docs\port-notes\READER-ENV.md): MSBuild starts every evaluation with them as properties, so
	///wherever MSBuild's files, the SDK's or the project's read a name that no file set and that is not reserved, global or the toolset's, an
	///environment variable of the name is its value. This reader's own code reads every name as MSBuild reads it there (GetValue, IsValueIfKnown,
	///GetKnownValue): what it computes from one not known is not known (NotKnown: GetProperty throws for it), a condition that decides an import
	///or an item throws, and an import of a file a property names (the Custom.Before and Custom.After files, AlternateCommonProps,
	///LanguageTargets and the other hooks of those files: ThrowIfImportHook) throws when a file or an environment variable sets it.
	///NuGet's central package versions (stage 2's S2-1): the Directory.Packages.props that NuGet.props imports at the end of
	///Microsoft.Common.props (ImportNuGetProps), with its PackageVersion items and ManagePackageVersionsCentrally, and NuGet.targets'
	///_CentralPackageVersionsEnabled; GetPackageVersion gives the version a PackageReference restores with, as NuGet decides it. Not modelled:
	///the restore itself (which packages a project gets through its references and dependencies, transitive pinning: the lock files say that),
	///GlobalPackageReference items, and the PackageReference items and metadata the SDK gives itself (the implicit PackageReference of a .NET
	///Standard 1.x/2.0 or .NET Core 1.x/2.x project, the metadata of Microsoft.NET.Sdk.DefaultItems.Shared.targets:
	///ThrowIfImplicitPackageReferences), which throw.</summary>
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
		///<summary>The characters MSBuild's EscapingUtilities.Escape writes as %XX.</summary>
		private static readonly char[] _arrayEscapedChars=new[] { '%','*','?','@','$','(',')',';','\'' };
		///<summary>Properties that MSBuild, its toolset (MSBuild.exe.config) or the solution build give every project and this reader does not
		///compute: machine paths and versions, and the solution build's global properties other than the ones _dictGlobalProperties holds. One of
		///them in a Condition, an Include or an Import throws
		///(except the imports of MSBuild's and Visual Studio's own files, which are not read), as does GetProperty for it, unless a file this reader
		///read set it.</summary>
		private static readonly string[] _arrayNotModelledProperties=new[] { "MSBuildExtensionsPath","MSBuildExtensionsPath32","MSBuildExtensionsPath64",
			"MSBuildToolsPath","MSBuildToolsPath32","MSBuildToolsPath64","MSBuildBinPath","MSBuildSDKsPath","MSBuildToolsRoot","MSBuildFrameworkToolsPath",
			"MSBuildFrameworkToolsPath32","MSBuildFrameworkToolsPath64","MSBuildFrameworkToolsRoot","MSBuildProgramFiles32","MSBuildUserExtensionsPath",
			"MSBuildVersion","MSBuildAssemblyVersion","MSBuildFileVersion","MSBuildSemanticVersion","MSBuildRuntimeType","MSBuildRuntimeVersion",
			"MSBuildNodeCount","MSBuildStartupDirectory","MSBuildInteractive","MSBuildIsRestoring","MSBuildOverrideTasksPath","VsInstallRoot",
			"RoslynTargetsPath","FrameworkSDKRoot","SDK35ToolsPath","SDK40ToolsPath","WindowsSDK80Path","WindowsKitsRoot","VCTargetsPath",
			"TargetFrameworkRootPath","FrameworkPathOverride","TargetFrameworkDirectory","TargetPlatformSdkPath","TargetPlatformDisplayName",
			"TargetPlatformSdkMetadataLocation",
			"NETCoreSdkVersion","NETCoreSdkRuntimeIdentifier","NETCoreRoot","DOTNET_HOST_PATH",
			"CurrentSolutionConfigurationContents","BuildingSolutionFile","BuildingInsideVisualStudio",
			//Microsoft.Common.CurrentVersion.targets (from $(ReferencePath), the registry and $(OutDir)) and Microsoft.NET.Sdk.props compute these.
			"AssemblySearchPaths","DesignTimeAssemblySearchPaths",
			//Microsoft.Common.props: where NuGet.props is (Visual Studio's folder when MSBuild runs from it); ImportNuGetProps reads what it does.
			"NuGetPropsFile","MSBuildUseVisualStudioDirectoryLayout" };
		///<summary>Properties that files MSBuild's common targets import for every project, classic and SDK-style alike, set and this reader does
		///not read: the files of Visual Studio's components in MSBuild's ImportAfter folder (Microsoft.NuGet.targets: NuGetTargetMoniker and
		///RuntimeIdentifiers), the .NET Framework's Microsoft.WinFx.targets (Version 1.0.0.0), and for an SDK-style project the SDK's own
		///(Microsoft.NET.DefaultAssemblyInfo.targets and Microsoft.PackageDependencyResolution.targets). MSBuild 18 gives every C# project of the
		///repository a value for each (S1-T2's check).</summary>
		private static readonly string[] _arrayNotModelledToolsetProperties=new[] { "Version","RuntimeIdentifiers","NuGetTargetMoniker",
			//Microsoft.Common.CurrentVersion.targets: where NuGet.targets is (_CentralPackageVersionsEnabled is the one of its names modelled).
			"NuGetRestoreTargets" };
		///<summary>Properties the .NET SDK computes for an SDK-style project and this reader does not (for a classic project MSBuild leaves them
		///'', as this reader does): the inferred runtime identifier, the package and assembly info defaults, the restore's output, the publish and
		///package folders, the analysis level and the analyzers' switch of Microsoft.NET.Sdk.Analyzers.targets.</summary>
		private static readonly string[] _arrayNotModelledSdkProperties=new[] { "RuntimeIdentifier","VersionPrefix","Authors","Company","AssemblyTitle",
			"Product","ProjectDepsFilePath","ProjectRuntimeConfigFilePath","PublishDir","PackageOutputPath","AnalysisLevel",
			"EffectiveAnalysisLevel","EnableNETAnalyzers" };
		///<summary>Properties Microsoft.WebApplication.targets and the Microsoft.Web.Publishing.targets it imports set for a web application
		///project (the Middle Tier's OpenPmsDentalServer.csproj imports them through $(VSToolsPath)), which this reader does not read: AppConfig is
		///Web.config when the project has one. Not modelled for a project that imports them.</summary>
		private static readonly string[] _arrayWebApplicationProperties=new[] { "AppConfig","ProjectConfigFileName" };
		///<summary>The compiler defaults that Microsoft.NET.Sdk.CSharp.props and .targets and Microsoft.NET.Sdk.Analyzers.targets compute for an
		///SDK-style C# project (ImportSdkProps, ApplySdkLanguageProperties). The SDK's Visual Basic and F# files compute them otherwise, which this
		///reader does not model, so for an SDK-style project in another language they are not modelled.</summary>
		private static readonly string[] _arraySdkLanguageProperties=new[] { "NoWarn","WarningsAsErrors","DefineConstants","ImplicitConfigurationDefine",
			"WarningLevel","AppDesignerFolder" };
		///<summary>The item types whose items Microsoft.NET.Sdk.DefaultItems.targets gives Link metadata (SetLinkMetadataAutomatically).</summary>
		private static readonly string[] _arrayLinkItemTypes=new[] { "Compile","AdditionalFiles","None","Content","EmbeddedResource","Page","Resource" };
		///<summary>The names that are not modelled at this point of the evaluation: those of _arrayNotModelledProperties from the start, those of
		///_arrayNotModelledToolsetProperties from the common targets on, for an SDK-style project those of _arrayNotModelledSdkProperties from
		///Sdk.targets on, those of _arrayWebApplicationProperties after an import of the web application targets, and any the evaluation could not
		///compute (TargetPlatformVersion of a .NET 5+ target without a platform version, say).</summary>
		private readonly HashSet<string> _hashSetNotModelled=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		///<summary>OpenDental.sln by path: its last write time and its project mappings, read once per test run.</summary>
		private static readonly Dictionary<string,SolutionFile> _dictSolutions=new Dictionary<string,SolutionFile>(StringComparer.OrdinalIgnoreCase);
		///<summary>The global properties of the evaluation, which no file can change (MSBuild ignores an assignment to one): Configuration and
		///Platform, and for a project OpenDental.sln lists the ones the solution build gives every project it builds (SolutionProjectGenerator):
		///SolutionDir (the solution's folder with a trailing back slash), SolutionPath, SolutionName, SolutionExt and SolutionFileName.</summary>
		private readonly Dictionary<string,string> _dictGlobalProperties=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		///<summary>True when Microsoft.NET.Sdk.targets imported Microsoft.NET.Sdk.WindowsDesktop.targets (ImportWindowsDesktopTargets), whose
		///_EnableWindowsDesktopGlobbing and Page removals this reader models (IsWindowsDesktopGlobbing, ApplySdkTargetsItems).</summary>
		private bool _isWindowsDesktopTargetsImported;

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
			FileInfo fileInfo=new FileInfo(pathSln);
			lock(_dictSolutions) {
				SolutionFile solutionFile;
				if(!_dictSolutions.TryGetValue(pathSln,out solutionFile) || solutionFile.DateTimeWriteUtc!=fileInfo.LastWriteTimeUtc
					|| solutionFile.Length!=fileInfo.Length)
				{
					solutionFile=new SolutionFile(pathSln,fileInfo.LastWriteTimeUtc,fileInfo.Length);
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
			projectFile._dictGlobalProperties["Configuration"]=configuration;
			projectFile._dictGlobalProperties["Platform"]=platform;
			//As the solution build evaluates the projects OpenDental.sln lists; a project it does not list is built on its own, without them.
			string pathSln=FindFileAbove("OpenDental.sln",projectFile.Folder);
			if(pathSln!=null && GetSolutionFile(pathSln).Lists(fullPath)) {
				projectFile._dictGlobalProperties["SolutionDir"]=Path.GetDirectoryName(pathSln)+@"\";
				projectFile._dictGlobalProperties["SolutionPath"]=pathSln;
				projectFile._dictGlobalProperties["SolutionName"]=Path.GetFileNameWithoutExtension(pathSln);
				projectFile._dictGlobalProperties["SolutionExt"]=Path.GetExtension(pathSln);
				projectFile._dictGlobalProperties["SolutionFileName"]=Path.GetFileName(pathSln);
			}
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
			//Every value is kept as MSBuild keeps it, escaped (Escape, Unescape): the global properties and the reserved ones hold the escaped
			//value.
			foreach(KeyValuePair<string,string> pair in _dictGlobalProperties) {
				_dictProperties[pair.Key]=Escape(pair.Value);
			}
			_dictProperties["MSBuildProjectFullPath"]=Escape(FullPath);
			_dictProperties["MSBuildProjectDirectory"]=Escape(Folder);
			_dictProperties["MSBuildProjectFile"]=Escape(Path.GetFileName(FullPath));
			_dictProperties["MSBuildProjectName"]=Escape(Path.GetFileNameWithoutExtension(FullPath));
			_dictProperties["MSBuildProjectExtension"]=Escape(Path.GetExtension(FullPath));
			//Microsoft.Common.CurrentVersion.targets defines these for every project, each only when it is '' (lines 246, 256, 345, 348); build
			//events and copy targets use them. An environment variable of the name is MSBuild's value instead: not known (SetIfEmpty).
			SetIfEmpty("ProjectDir",Escape(Folder+@"\"));
			SetIfEmpty("ProjectFileName",Escape(Path.GetFileName(FullPath)));
			SetIfEmpty("ProjectPath",GetValue("ProjectDir")+GetValue("ProjectFileName"));
			SetIfEmpty("ProjectName",Escape(Path.GetFileNameWithoutExtension(FullPath)));
			_dictProperties["MSBuildProjectDirectoryNoRoot"]=Escape(Folder.Substring(Path.GetPathRoot(Folder).Length));
			//MSBuild 18's toolset: "Current" for every MSBuild since 16 (reserved), and the sub-toolset version, which MSBuild takes from an
			//environment variable VisualStudioVersion when there is one (Toolset.GenerateSubToolsetVersion; the property is set only when it is
			//not set yet): not known then.
			_dictProperties["MSBuildToolsVersion"]="Current";
			SetIfEmpty("VisualStudioVersion",VISUAL_STUDIO_VERSION);
			//The machine's own: from the start. The toolset's and the SDK's are added where MSBuild's files set them (ApplyCommonTargetsProperties,
			//ApplySdkBeforeCommonProperties): before that point a name nothing set is '', as MSBuild has it there.
			_hashSetNotModelled.UnionWith(_arrayNotModelledProperties);
			if(IsSdkStyle) {
				_dictProperties["UsingMicrosoftNETSdk"]="true";
				if(!IsCSharpProject()) {
					_hashSetNotModelled.UnionWith(_arraySdkLanguageProperties);
				}
			}
			_listFilesRead.Add(FullPath);
			XElement xElementRoot=xDocument.Root;
			bool hasImplicitSdk=xElementRoot.Attribute("Sdk")!=null || xElementRoot.Elements().Any(x => x.Name.LocalName=="Sdk");
			CheckSdkName((string)xElementRoot.Attribute("Sdk"),FullPath);
			foreach(XElement xElementSdk in xElementRoot.Elements().Where(x => x.Name.LocalName=="Sdk")) {
				CheckSdkName((string)xElementSdk.Attribute("Name")??"",FullPath);
			}
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
			//NuGet.targets turns every GlobalPackageReference into a PackageReference and a PackageVersion (its ItemGroup with the condition
			//'$(ManagePackageVersionsCentrally)' == 'true' And '$(RestoreEnableGlobalPackageReference)' != 'false'), which this reader does not
			//model. Without such items the group adds nothing, so the two are read only when there are some, each as MSBuild reads it: one not known
			//here (an environment variable no file set) throws (IsKnownTrue).
			if(_listItems.Any(x => x.ItemType.Equals("GlobalPackageReference",StringComparison.OrdinalIgnoreCase))
				&& IsKnownTrue("ManagePackageVersionsCentrally") && !IsKnownFalse("RestoreEnableGlobalPackageReference"))
			{
				throw new NotSupportedException(Describe()+": GlobalPackageReference items are not modelled by ProjectFile.");
			}
		}

		///<summary>Pass 1 of MSBuild's evaluation for one file: properties in order (imports read where they stand), item groups queued for pass 2.
		///</summary>
		private void ReadProjectElement(XElement xElementProject,FileContext fileContext) {
			foreach(XElement xElement in xElementProject.Elements()) {
				switch(xElement.Name.LocalName) {
					case "PropertyGroup":
						if(!IsConditionTrueInFile(xElement,fileContext)) {
							break;
						}
						foreach(XElement xElementProperty in xElement.Elements()) {
							//A global property (Configuration, Platform and the solution's, which the solution build gives) a file cannot change.
							if(_dictGlobalProperties.ContainsKey(xElementProperty.Name.LocalName)) {
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
						if(IsConditionTrueInFile(xElement,fileContext)) {
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

		///<summary>Throws NotSupportedException unless every SDK that sdkNames names (a ;-list, each optionally with a /version) is
		///Microsoft.NET.Sdk, the only SDK this evaluator models. Another SDK (Microsoft.NET.Sdk.Web for the Middle Tier on .NET 10,
		///Microsoft.NET.Sdk.Razor, ...) has other defaults and globs, so it is refused instead of read as Microsoft.NET.Sdk. Null or empty: no SDK.
		///</summary>
		private static void CheckSdkName(string sdkNames,string path) {
			if(string.IsNullOrWhiteSpace(sdkNames)) {
				return;
			}
			foreach(string sdk in sdkNames.Split(';').Select(x => x.Trim()).Where(x => x!="")) {
				string name=sdk.Split('/')[0].Trim();
				if(!name.Equals("Microsoft.NET.Sdk",StringComparison.OrdinalIgnoreCase)) {
					throw new NotSupportedException(path+": the SDK "+name+" is not modelled by ProjectFile (only Microsoft.NET.Sdk is).");
				}
			}
		}

		private void ReadImport(XElement xElementImport,FileContext fileContext) {
			string project=(string)xElementImport.Attribute("Project")??"";
			if(xElementImport.Attribute("Sdk")!=null) {
				if(!IsConditionTrueInFile(xElementImport,fileContext)) {
					return;
				}
				CheckSdkName((string)xElementImport.Attribute("Sdk"),fileContext.Path);
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
			//A package's build file, which the restore's own files import (since S2-1 brought packages with build files).
			if(project.StartsWith("$(NuGetPackageRoot)",StringComparison.OrdinalIgnoreCase) && IsRestoreFile(fileContext.Path)) {
				ReadPackageImport(xElementImport,fileContext,project);
				return;
			}
			//Any other file of MSBuild, Visual Studio or the SDK is not read. Unless its condition is false, what it sets is not known: the web
			//application targets of the Middle Tier project make their names not modelled, any other such file throws.
			if(Regex.IsMatch(project,@"^\$\((MSBuildExtensionsPath|MSBuildExtensionsPath32|MSBuildExtensionsPath64|MSBuildToolsPath|MSBuildBinPath|VSToolsPath|MSBuildSDKsPath|NuGetPackageRoot)\)",
				RegexOptions.IgnoreCase))
			{
				bool? isImported=null;
				try {
					isImported=IsConditionTrueInFile(xElementImport,fileContext);
				}
				catch(NotSupportedException) {
					//A condition on the machine's own paths ('$(VSToolsPath)' != ''): whether MSBuild imports the file is not known here.
				}
				if(isImported==false) {
					return;
				}
				if(fileName.Equals("Microsoft.WebApplication.targets",StringComparison.OrdinalIgnoreCase)) {
					_hashSetNotModelled.UnionWith(_arrayWebApplicationProperties);
					return;
				}
				throw new NotSupportedException(fileContext.Path+" imports "+project+", a file of MSBuild, Visual Studio or the .NET SDK that ProjectFile does "
					+"not read, so the properties and items it sets are not known.");
			}
			if(!IsConditionTrueInFile(xElementImport,fileContext)) {
				return;
			}
			string expanded=Expand(project,fileContext);
			ThrowIfUnresolved(expanded,fileContext.Path+": Import "+project);
			//A '*' or '?' as written is a wildcard; an escaped one (%2a, %3f) is part of the file's name.
			if(expanded.IndexOfAny(new[] { '*','?' })>=0) {
				throw new NotSupportedException(fileContext.Path+": a wildcard Import is not modelled by ProjectFile: "+project);
			}
			string path=Path.GetFullPath(Path.Combine(fileContext.Directory,Unescape(expanded)));
			if(!File.Exists(path)) {
				throw new FileNotFoundException(fileContext.Path+" imports "+project+", which is not there ("+path+"). MSBuild fails the same way (MSB4019).",path);
			}
			ReadImportedFile(path);
		}

		///<summary>True for a file NuGet's restore wrote for this project, which ImportProjectExtensions reads:
		///$(MSBuildProjectExtensionsPath)$(MSBuildProjectFile).nuget.g.props or .nuget.g.targets.</summary>
		private bool IsRestoreFile(string path) {
			string folder=Unescape(GetValue("MSBuildProjectExtensionsPath"));
			if(folder=="" || IsUnresolved(folder)) {
				return false;
			}
			string prefix=Path.Combine(folder,GetUnescaped("MSBuildProjectFile"));
			return path.Equals(prefix+".nuget.g.props",StringComparison.OrdinalIgnoreCase) || path.Equals(prefix+".nuget.g.targets",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>An import of a package's build file (build\ or buildTransitive\ of a package of the restore: System.ValueTuple's
		///System.ValueTuple.targets, the generator switches of System.Text.Json and Microsoft.Extensions.Logging.Abstractions) as the restore's
		///obj\Project.csproj.nuget.g.props and .targets list them: Project="$(NuGetPackageRoot)id\version\folder\tfm\file" with
		///Condition="Exists('...the same...')". The restore writes $(NuGetPackageRoot) as its global packages folder, through $(UserProfile) when the
		///folder lies in the user profile (an environment variable, which this reader does not model), and the same folder first in
		///$(NuGetPackageFolders), as a literal path (NuGet's BuildAssetsUtils): the file is read from that folder when it is there, as MSBuild reads
		///it. $(NuGetPackageRoot) itself stays not known (GetProperty throws for it). Any other form of the import throws.</summary>
		private void ReadPackageImport(XElement xElementImport,FileContext fileContext,string project) {
			const string ROOT="$(NuGetPackageRoot)";
			string relative=project.Substring(ROOT.Length);
			string condition=((string)xElementImport.Attribute("Condition")??"").Trim();
			if(condition!="Exists('"+project+"')" || relative.Contains("$(") || relative.Contains("@(") || relative.Contains("%(")) {
				throw new NotSupportedException(fileContext.Path+": an import of a package file in another form than the restore writes is not modelled by "
					+"ProjectFile: "+project);
			}
			//MSBuild reads an environment variable of the name when no file set it (the restore's file sets it only when it is ''); not modelled.
			ThrowIfEnvironmentVariable("NuGetPackageFolders");
			string folders=GetValue("NuGetPackageFolders");
			string folder=Unescape(folders.Split(';').Select(x => x.Trim()).FirstOrDefault(x => x!="")??"");
			if(folder=="" || IsUnresolved(folders) || !Path.IsPathRooted(folder)) {
				throw new NotSupportedException(fileContext.Path+" imports "+project+", but the restore's $(NuGetPackageFolders) names no package folder.");
			}
			string path=Path.GetFullPath(Path.Combine(folder,Unescape(relative)));
			if(File.Exists(path)) {
				ReadImportedFile(path);
			}
		}

		private void ReadImportedFile(string path) {
			if(_listFilesRead.Contains(path,StringComparer.OrdinalIgnoreCase)) {
				return;//MSBuild skips a second import of the same file (warning MSB4011).
			}
			_listFilesRead.Add(path);
			ReadProjectElement(XDocument.Load(path).Root,new FileContext(path));
		}

		///<summary>Directory.Build.props (Microsoft.Common.props, lines 19-36) or Directory.Build.targets (Microsoft.Common.targets, lines 45-62), as
		///those files find it: the switch ImportDirectoryBuildProps or ImportDirectoryBuildTargets true unless set, then, unless the project names
		///the file itself (DirectoryBuildPropsPath, DirectoryBuildTargetsPath), the first one in the project's folder or a folder above it
		///(_DirectoryBuildPropsFile, the folder _DirectoryBuildPropsBasePath, the full path), imported when the switch is true and the file is
		///there. The Custom{Before,After}DirectoryBuild{Props,Targets} imports before and after it are not modelled: one of them set throws, the
		///After one also when the file just imported set it. Each name is read as MSBuild reads it, and only where it reads it (with the switch
		///off the path is never expanded): one that is not known (an environment variable no file set, GetKnownValue) throws, as what MSBuild
		///imports is then not known.</summary>
		private void ImportDirectoryBuildFile(string kind) {
			string switchName="ImportDirectoryBuild"+kind;
			string pathName="DirectoryBuild"+kind+"Path";
			string fileNameName="_DirectoryBuild"+kind+"File";
			string basePathName="_DirectoryBuild"+kind+"BasePath";
			string fileMSBuild="Microsoft.Common."+(kind=="Props" ? "props" : "targets");
			SetIfEmpty(switchName,"true");
			bool isImported=IsKnownTrue(switchName);
			if(isImported && GetKnownValue(pathName)=="") {
				SetIfEmpty(fileNameName,"Directory.Build."+kind.ToLowerInvariant());
				string fileName=GetKnownValue(fileNameName);
				string pathFound=FindFileAbove(fileName,Folder);
				SetIfEmpty(basePathName,pathFound==null ? "" : Escape(Path.GetDirectoryName(pathFound)));
				string basePath=GetKnownValue(basePathName);
				if(basePath!="" && fileName!="") {
					_dictProperties[pathName]=Escape(Path.Combine(basePath,fileName));
				}
			}
			ThrowIfImportHook("CustomBeforeDirectoryBuild"+kind,fileMSBuild);
			string path=isImported ? GetKnownValue(pathName) : "";
			if(path!="") {
				if(!Path.IsPathRooted(path)) {
					//MSBuild resolves it from the folder of Microsoft.Common.props or .targets, which this reader does not read.
					throw new NotSupportedException(Describe()+": a relative $("+pathName+") is not modelled by ProjectFile: "+path);
				}
				if(File.Exists(path)) {
					ReadImportedFile(Path.GetFullPath(path));
				}
			}
			ThrowIfImportHook("CustomAfterDirectoryBuild"+kind,fileMSBuild);
		}

		///<summary>The project extension imports of Microsoft.Common.props and Microsoft.Common.targets, which NuGet's restore writes
		///($(MSBuildProjectExtensionsPath)$(MSBuildProjectFile).*.props and .*.targets, as obj\Project.csproj.nuget.g.props): when the switch is true
		///and the folder is there, every such file in it, in the order of their names (MSBuild sorts the list), as files of the project.</summary>
		private void ImportProjectExtensions(string extension) {
			string folder=GetValue("MSBuildProjectExtensionsPath");
			ThrowIfUnresolved(folder,Describe()+": $(MSBuildProjectExtensionsPath)");
			folder=Unescape(folder);
			if(folder=="" || !Directory.Exists(folder)) {
				return;
			}
			string prefix=GetUnescaped("MSBuildProjectFile")+".";
			foreach(string path in Directory.GetFiles(folder,prefix+"*"+extension)
				.Where(x => Path.GetFileName(x).StartsWith(prefix,StringComparison.OrdinalIgnoreCase) && x.EndsWith(extension,StringComparison.OrdinalIgnoreCase))
				.OrderBy(x => x,StringComparer.OrdinalIgnoreCase))
			{
				ReadImportedFile(path);
			}
		}
		#endregion Loading

		#region MSBuild's and the SDK's own files
		//What the files of MSBuild 18 (Build Tools 2026) and of the pinned .NET SDK 10.0.114 do for the properties a project's conditions, items
		//and the tests read, at the point of the evaluation where those files do it, in their order. Each method names the file it follows; the
		//properties it does not compute are in the not-modelled lists (_arrayNotModelledProperties, _arrayNotModelledToolsetProperties,
		//_arrayNotModelledSdkProperties, _arraySdkLanguageProperties, _arrayWebApplicationProperties), and throw when used.

		///<summary>Microsoft.Common.props, which a classic project imports first, Sdk.props imports for an SDK-style project, and
		///Microsoft.Common.CurrentVersion.targets imports for a project that did neither: Directory.Build.props (ImportDirectoryBuildFile), then
		///BaseIntermediateOutputPath (obj\ unless set, with a trailing back slash) and MSBuildProjectExtensionsPath, the folder of the restore's files
		///(BaseIntermediateOutputPath unless set, made a full path from the project folder), the project extension props of that folder
		///(ImportProjectExtensionProps true unless set: an evaluation, not a restore), then MicrosoftCommonPropsHasBeenImported, and last
		///NuGet.props with Directory.Packages.props (ImportNuGetProps).</summary>
		private void ImportMicrosoftCommonProps() {
			ImportDirectoryBuildFile("Props");
			SetIfEmpty("BaseIntermediateOutputPath",@"obj\");
			AppendTrailingSlash("BaseIntermediateOutputPath");
			_dictProperties["_InitialBaseIntermediateOutputPath"]=GetValue("BaseIntermediateOutputPath");
			SetIfEmpty("MSBuildProjectExtensionsPath",GetValue("BaseIntermediateOutputPath"));
			string extensionsPath=GetValue("MSBuildProjectExtensionsPath");
			if(!IsUnresolved(extensionsPath) && !Path.IsPathRooted(Unescape(extensionsPath))) {
				_dictProperties["MSBuildProjectExtensionsPath"]=Escape(Path.Combine(Folder,Unescape(extensionsPath)));
			}
			AppendTrailingSlash("MSBuildProjectExtensionsPath");
			//Line 68: false during NuGet's restore (the global property MSBuildIsRestoring, which an evaluation does not have), read only when the
			//switch is ''. An environment variable of that name is read there: not known, so whether the restore's files are imported is not.
			SetIfEmptyUnlessRestoring("ImportProjectExtensionProps");
			SetIfEmpty("ImportProjectExtensionProps","true");
			//The import of the restore's files depends on it: one not known here (an environment variable no file set) throws, not ''.
			ThrowIfUnresolved(GetValue("ImportProjectExtensionProps"),Describe()+": $(ImportProjectExtensionProps)");
			if(IsPropertyTrue("ImportProjectExtensionProps")) {
				_dictProperties["_InitialMSBuildProjectExtensionsPath"]=GetValue("MSBuildProjectExtensionsPath");
				ImportProjectExtensions(".props");
			}
			//Line 92: Custom.Before.Microsoft.Common.props, a file of MSBuild's folder unless a file or an environment variable names another.
			ThrowIfImportHook("CustomBeforeMicrosoftCommonProps","Microsoft.Common.props");
			//Microsoft.Common.CurrentVersion.targets imports Microsoft.Common.props unless this is true.
			_dictProperties["MicrosoftCommonPropsHasBeenImported"]="true";
			ThrowIfImportHook("CustomAfterMicrosoftCommonProps","Microsoft.Common.props");
			ImportNuGetProps();
		}

		///<summary>Microsoft.Common.props line 68 and Microsoft.Common.targets line 37: the switch of the restore's project extension imports is
		///false when it is '' and $(MSBuildIsRestoring) is 'true' (NuGet's restore sets that global property; an evaluation has none). An
		///environment variable MSBuildIsRestoring is read there: it throws (GetKnownValue), as the import then depends on it.</summary>
		private void SetIfEmptyUnlessRestoring(string switchName) {
			if(GetValue(switchName)!="") {
				return;
			}
			if(GetKnownValue("MSBuildIsRestoring").Equals("true",StringComparison.OrdinalIgnoreCase)) {
				_dictProperties[switchName]="false";
			}
		}

		///<summary>NuGet.props, which Microsoft.Common.props imports at its end (for the Build Tools 2026 MSBuild, Visual Studio's copy in
		///Common7\IDE\CommonExtensions\Microsoft\NuGet), as Directory.Build.props is found (ImportDirectoryBuildFile): the switch
		///ImportDirectoryPackagesProps true unless set; unless the project names the file itself (DirectoryPackagesPropsPath), the first
		///Directory.Packages.props in the project's folder or a folder above it (_DirectoryPackagesPropsFile, the folder
		///_DirectoryPackagesPropsBasePath, the full path NormalizePath gives); the import when the switch is true and the file is there, and then
		///CentralPackageVersionsFileImported true. The file's properties (ManagePackageVersionsCentrally and the others) and items (PackageVersion)
		///are read as those of any imported file. So it comes after Directory.Build.props and the restore's project extension props, and before the
		///project's own properties; a classic project imports it too. Every condition of NuGet.props tests the switch first, and MSBuild stops at
		///the first false operand of an 'and', so with the switch off $(DirectoryPackagesPropsPath) is never expanded: only the switch must be
		///known then (it throws when it is not), and the path only when the switch is on. Each of the five is read as MSBuild reads it: what a file
		///set, else an environment variable of the name, which this reader does not model, so one that no file set throws (through SetIfEmpty for
		///the switch, _DirectoryPackagesPropsFile and _DirectoryPackagesPropsBasePath, GetKnownValue for DirectoryPackagesPropsPath) instead of
		///reading as ''. When no folder has the file (NuGet.props' GetDirectoryNameOfFileAbove gives ''), _DirectoryPackagesPropsBasePath is '',
		///DirectoryPackagesPropsPath stays '' and nothing is imported, so CentralPackageVersionsFileImported keeps what it had ('' unless a file
		///set it; an environment variable of the name throws where it is read, ApplyCommonTargetsProperties).</summary>
		private void ImportNuGetProps() {
			//Microsoft.Common.props lines 122-127: which NuGet.props it imports (Visual Studio's copy for the Build Tools MSBuild, whose own folder
			//has none). A file or an environment variable that names another, or the switch between the two, is not modelled.
			ThrowIfImportHook("MSBuildUseVisualStudioDirectoryLayout","Microsoft.Common.props");
			ThrowIfImportHook("NuGetPropsFile","Microsoft.Common.props");
			SetIfEmpty("ImportDirectoryPackagesProps","true");
			ThrowIfUnresolved(GetValue("ImportDirectoryPackagesProps"),Describe()+": $(ImportDirectoryPackagesProps)");
			if(!IsPropertyTrue("ImportDirectoryPackagesProps")) {
				return;
			}
			if(GetKnownValue("DirectoryPackagesPropsPath")=="") {
				SetIfEmpty("_DirectoryPackagesPropsFile","Directory.Packages.props");
				ThrowIfUnresolved(GetValue("_DirectoryPackagesPropsFile"),Describe()+": $(_DirectoryPackagesPropsFile)");
				string pathFound=FindFileAbove(GetUnescaped("_DirectoryPackagesPropsFile"),Folder);
				SetIfEmpty("_DirectoryPackagesPropsBasePath",pathFound==null ? "" : Escape(Path.GetDirectoryName(pathFound)));
				ThrowIfUnresolved(GetValue("_DirectoryPackagesPropsBasePath"),Describe()+": $(_DirectoryPackagesPropsBasePath)");
				if(GetValue("_DirectoryPackagesPropsBasePath")!="" && GetValue("_DirectoryPackagesPropsFile")!="") {
					_dictProperties["DirectoryPackagesPropsPath"]=Escape(Path.GetFullPath(Path.Combine(GetUnescaped("_DirectoryPackagesPropsBasePath"),
						GetUnescaped("_DirectoryPackagesPropsFile"))));
				}
			}
			string path=GetKnownValue("DirectoryPackagesPropsPath");
			if(path=="") {
				return;
			}
			if(!Path.IsPathRooted(path)) {
				//MSBuild resolves it from the folder of NuGet.props, which this reader does not read.
				throw new NotSupportedException(Describe()+": a relative $(DirectoryPackagesPropsPath) is not modelled by ProjectFile: "+path);
			}
			if(!File.Exists(path)) {
				return;
			}
			ReadImportedFile(Path.GetFullPath(path));
			_dictProperties["CentralPackageVersionsFileImported"]="true";
		}

		///<summary>Microsoft.CSharp.targets (or the Visual Basic or common one) of a classic project, and the language targets that Sdk.targets
		///imports, in their order: the language (Microsoft.CSharp.CurrentVersion.targets, with AppDesignerFolder for C#); Microsoft.Common.props,
		///which Microsoft.Common.CurrentVersion.targets imports first when MicrosoftCommonPropsHasBeenImported is not true (a classic project without
		///its own import, as OpenPmsDental.csproj: its Directory.Build.props is read here, after the project's own properties); the project's .user
		///file when there is one; the properties of Microsoft.Common.CurrentVersion.targets (ApplyCommonTargetsProperties); from the files its
		///ImportAfter folder imports for every project, the _TargetFrameworkVersionWithoutV of Microsoft.NET.Build.Extensions.targets (the others,
		///of Visual Studio's components and the .NET Framework, are in _arrayNotModelledToolsetProperties); then, from Microsoft.Common.targets,
		///the project extension targets of the restore and Directory.Build.targets; and for C# and Visual Basic, Utf8Output, which the language's
		///CurrentVersion.targets sets after Microsoft.Common.targets. The imports MSBuild's files make of a file a property names (the Custom.Before
		///and Custom.After files of the language and common targets, NuGet.targets, the ImportAfter file Microsoft.NET.Build.Extensions.targets)
		///and the switches between them (IsCrossTargetingBuild, MicrosoftCommonPropsHasBeenImported, ImportByWildcardAfterMicrosoftCommonTargets)
		///are read as MSBuild reads them: one that a file or an environment variable sets to another file throws, one that is not known throws or
		///makes what depends on it not known.</summary>
		private void ImportMicrosoftCommonTargets(string fileName) {
			bool isLanguage=false;
			string languageName=null;
			if(fileName.Equals("Microsoft.VisualBasic.targets",StringComparison.OrdinalIgnoreCase)) {
				languageName="VisualBasic";
			}
			else if(fileName.Equals("Microsoft.CSharp.targets",StringComparison.OrdinalIgnoreCase)) {
				languageName="CSharp";
			}
			if(languageName!=null) {
				//Microsoft.CSharp.targets (and .VisualBasic.targets) line 20: the cross-targeting targets instead, when it is 'true'.
				if(IsValueIfKnown("IsCrossTargetingBuild","true")!=false) {
					ThrowIfImportHook("IsCrossTargetingBuild","Microsoft."+languageName+".targets");
				}
				//Microsoft.CSharp.CurrentVersion.targets line 34 (Microsoft.VisualBasic.CurrentVersion.targets line 34).
				ThrowIfImportHook("CustomBeforeMicrosoft"+languageName+"Targets","Microsoft."+languageName+".CurrentVersion.targets");
			}
			if(languageName=="VisualBasic") {
				_dictProperties["Language"]="VB";
				_dictProperties["DefaultLanguageSourceExtension"]=".vb";
				_dictProperties["TargetRuntime"]="Managed";
				isLanguage=true;
			}
			else if(languageName=="CSharp") {
				//Microsoft.CSharp.CurrentVersion.targets lines 37-39, 46.
				_dictProperties["Language"]="C#";
				_dictProperties["DefaultLanguageSourceExtension"]=".cs";
				_dictProperties["TargetRuntime"]="Managed";
				SetIfEmpty("AppDesignerFolder","Properties");
				isLanguage=true;
			}
			//Microsoft.Common.CurrentVersion.targets line 18: Microsoft.Common.props unless it is 'true'; one not known (an environment variable no
			//file set) throws, as whether Directory.Build.props is read depends on it.
			if(!IsKnownTrue("MicrosoftCommonPropsHasBeenImported")) {
				ImportMicrosoftCommonProps();
			}
			foreach(string name in new[] { "ImportByWildcardBeforeMicrosoftCommonTargets","ImportByWildcardAfterMicrosoftCommonTargets",
				"ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets","ImportUserLocationsByWildcardAfterMicrosoftCommonTargets" })
			{
				SetIfEmpty(name,"true");
			}
			//Microsoft.Common.CurrentVersion.targets imports $(MSBuildProjectFullPath).user before its own properties. An empty one fails MSBuild's
			//evaluation, and this reader's (XDocument.Load throws).
			if(File.Exists(FullPath+".user")) {
				ReadImportedFile(FullPath+".user");
			}
			//Line 43: Custom.Before.Microsoft.Common.targets.
			ThrowIfImportHook("CustomBeforeMicrosoftCommonTargets","Microsoft.Common.CurrentVersion.targets");
			ApplyCommonTargetsProperties();
			//Line 7140: Custom.After.Microsoft.Common.targets.
			ThrowIfImportHook("CustomAfterMicrosoftCommonTargets","Microsoft.Common.CurrentVersion.targets");
			ApplyBuildExtensionsTargets();
			//Microsoft.Common.targets, after Microsoft.Common.CurrentVersion.targets: line 37 (MSBuildIsRestoring), line 39, line 42.
			SetIfEmptyUnlessRestoring("ImportProjectExtensionTargets");
			SetIfEmpty("ImportProjectExtensionTargets","true");
			ThrowIfUnresolved(GetValue("ImportProjectExtensionTargets"),Describe()+": $(ImportProjectExtensionTargets)");
			if(IsPropertyTrue("ImportProjectExtensionTargets")) {
				ImportProjectExtensions(".targets");
			}
			ImportDirectoryBuildFile("Targets");
			if(isLanguage) {
				SetIfEmpty("Utf8Output","true");
				//Microsoft.CSharp.CurrentVersion.targets line 392 (Microsoft.VisualBasic.CurrentVersion.targets line 393).
				ThrowIfImportHook("CustomAfterMicrosoft"+languageName+"Targets","Microsoft."+languageName+".CurrentVersion.targets");
			}
		}

		///<summary>The file of Microsoft.Common.CurrentVersion.targets' ImportAfter folder (line 7142, when ImportByWildcardAfterMicrosoftCommonTargets
		///is 'true') that this reader models: Microsoft.Common.targets\ImportAfter\Microsoft.NET.Build.Extensions.targets imports
		///$(MicrosoftNETBuildExtensionsTargets) (MSBuild's Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets unless a
		///file or an environment variable names another, lines 14 and 17), which sets _TargetFrameworkVersionWithoutV from TargetFrameworkVersion
		///(line 15) for every project. When the switch or that path is not known, or the path names another file, _TargetFrameworkVersionWithoutV
		///is not known; the other files of the folder (Visual Studio's components) set names this reader does not model
		///(_arrayNotModelledToolsetProperties).</summary>
		private void ApplyBuildExtensionsTargets() {
			const string FILE="Microsoft.NET.Build.Extensions.targets";
			bool? isImportedAfter=IsValueIfKnown("ImportByWildcardAfterMicrosoftCommonTargets","true");
			if(isImportedAfter==false) {
				return;
			}
			if(isImportedAfter==null) {
				_dictProperties["_TargetFrameworkVersionWithoutV"]=NotKnown("_TargetFrameworkVersionWithoutV",FILE,"ImportByWildcardAfterMicrosoftCommonTargets");
				return;
			}
			if(GetValueIfKnown("MicrosoftNETBuildExtensionsTargets")!="") {
				_dictProperties["_TargetFrameworkVersionWithoutV"]=GetValueIfKnown("MicrosoftNETBuildExtensionsTargets")==null
					? NotKnown("_TargetFrameworkVersionWithoutV",FILE,"MicrosoftNETBuildExtensionsTargets")
					: Unresolved("the _TargetFrameworkVersionWithoutV of the file $(MicrosoftNETBuildExtensionsTargets) names");
				return;
			}
			//That file's line 15. TargetFrameworkVersion is stored before this point unless TargetRuntime is not Managed (the common targets' lines 61-71 then
			//set no default): an environment variable no file set is then MSBuild's value here, which GetValue gives as not known.
			_dictProperties["_TargetFrameworkVersionWithoutV"]=Escape(GetUnescaped("TargetFrameworkVersion").TrimStart('v','V'));
		}

		///<summary>The properties of Microsoft.Common.CurrentVersion.targets (MSBuild 18) that the repository's files and tests read, in its
		///order: the target framework defaults (.NETFramework, v4.0); Microsoft.NETFramework.props for a .NET Framework target or Microsoft.NET.props
		///for .NET and .NET Standard (ApplyFrameworkProps: Prefer32Bit, SubsystemVersion, HighEntropyVA); the target framework moniker; the Windows
		///platform defaults (Windows, 7.0) unless a .NET 5+ target turned them off; the platform and configuration names; BaseOutputPath (bin\),
		///OutputPath (bin\configuration\ for AnyCPU, else bin\platform\configuration\, when the project sets none), BaseIntermediateOutputPath and
		///IntermediateOutputPath the same way, each with a trailing back slash; OutputType (exe); DebugSymbols (true for the Debug configuration
		///when neither it nor DebugType is set); ProduceReferenceAssembly (false unless set); the output file's extension, folder (OutDir, TargetDir
		///as a full path), name and path; ProjectExt; GenerateBindingRedirectsOutputType for an exe; AutoGenerateBindingRedirects for a .NET
		///Framework 4.7.2 or later project that leaves it empty; PublishDir (with a trailing back slash, or the output path's app.publish\ for a
		///classic project: the SDK's own is not modelled); PlatformTargetAsMSBuildArchitecture (MSBuild.exe runs on the .NET Framework);
		///DelaySign, false for an unsigned assembly and '' unless 'true'; and from NuGet.targets, which it imports at its end,
		///_CentralPackageVersionsEnabled, true when ManagePackageVersionsCentrally is true and Directory.Packages.props was imported (not known
		///when MSBuild would read either from an environment variable).</summary>
		private void ApplyCommonTargetsProperties() {
			const string FILE="Microsoft.Common.CurrentVersion.targets";
			_hashSetNotModelled.UnionWith(_arrayNotModelledToolsetProperties);
			//Lines 61-71: the .NET Framework defaults of a managed project (the language targets set TargetRuntime Managed for C# and Visual Basic;
			//one not known makes the defaults not known).
			SetIfEmpty("TargetRuntime","Managed");
			bool? isManaged=IsValueIfKnown("TargetRuntime","Managed");
			if(isManaged==null) {
				SetIfEmpty("TargetFrameworkIdentifier",NotKnown("TargetFrameworkIdentifier",FILE,"TargetRuntime"));
				SetIfEmpty("TargetFrameworkVersion",NotKnown("TargetFrameworkVersion",FILE,"TargetRuntime"));
			}
			else if(isManaged.Value) {
				SetIfEmpty("TargetFrameworkIdentifier",".NETFramework");
				SetIfEmpty("TargetFrameworkVersion","v4.0");
			}
			ApplyFrameworkProps();
			//Lines 90-92 (GetValue: a value not known, an environment variable no file set among them, makes the moniker not known).
			string frameworkIdentifier=GetValue("TargetFrameworkIdentifier");
			string frameworkVersion=GetValue("TargetFrameworkVersion");
			if(frameworkIdentifier!="" && frameworkVersion!="" && GetValue("TargetFrameworkMoniker")=="") {
				string profile=GetValue("TargetFrameworkProfile");
				_dictProperties["TargetFrameworkMoniker"]=frameworkIdentifier+",Version="+frameworkVersion+(string.IsNullOrWhiteSpace(profile) ? "" : ",Profile="+profile);
			}
			//Lines 104-105: the Windows platform defaults, each when it is '' and _EnableDefaultWindowsPlatform is not 'false' (the SDK sets it
			//false for .NET 5 and later; otherwise an environment variable of the name stands).
			foreach(string[] arrayDefault in new[] { new[] { "TargetPlatformIdentifier","Windows" },new[] { "TargetPlatformVersion","7.0" } }) {
				if(GetValue(arrayDefault[0])!="") {
					continue;
				}
				bool? isDefaultOff=IsValueIfKnown("_EnableDefaultWindowsPlatform","false");
				if(isDefaultOff==null) {
					_dictProperties[arrayDefault[0]]=NotKnown(arrayDefault[0],FILE,"_EnableDefaultWindowsPlatform");
				}
				else if(!isDefaultOff.Value) {
					_dictProperties[arrayDefault[0]]=arrayDefault[1];
				}
			}
			//Line 114.
			if(GetValue("TargetPlatformMoniker")=="" && GetValue("TargetPlatformIdentifier")!="" && GetValue("TargetPlatformVersion")!="") {
				_dictProperties["TargetPlatformMoniker"]=GetValue("TargetPlatformIdentifier")+",Version="+GetValue("TargetPlatformVersion");
			}
			//Lines 163-177. A PlatformName not known (an environment variable no file set: SetIfEmpty) is in the default OutputPath and
			//IntermediateOutputPath it gives, which are not known then; a known one decides the folder.
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
			//Lines 182-184: the legacy TargetType, then the default. One not known (GetValue: an environment variable no file set) is
			//OutputType's value, which is not known then.
			if(GetValue("TargetType")!="") {
				_dictProperties["OutputType"]=IsValue("TargetType","Container") || IsValue("TargetType","DocumentContainer") ? "library" : GetValue("TargetType");
			}
			SetIfEmpty("OutputType","exe");
			//Line 186.
			if(GetValue("DebugSymbols")=="" && GetValue("DebugType")=="") {
				bool? isDebug=IsValueIfKnown("ConfigurationName","Debug");
				if(isDebug==null) {
					_dictProperties["DebugSymbols"]=NotKnown("DebugSymbols",FILE,"ConfigurationName");
				}
				else if(isDebug.Value) {
					_dictProperties["DebugSymbols"]="true";
				}
			}
			SetIfEmpty("ProduceReferenceAssembly","false");
			//Lines 230-235.
			if(GetValue("TargetExt")=="") {
				if(GetValueIfKnown("OutputType")==null) {
					_dictProperties["TargetExt"]=NotKnown("TargetExt",FILE,"OutputType");
				}
				foreach(string[] arrayExtension in new[] { new[] { "exe",".exe" },new[] { "winexe",".exe" },new[] { "appcontainerexe",".exe" },
					new[] { "library",".dll" },new[] { "module",".netmodule" },new[] { "winmdobj",".winmdobj" } })
				{
					if(IsValue("OutputType",arrayExtension[0])) {
						_dictProperties["TargetExt"]=arrayExtension[1];
					}
				}
			}
			//Line 240: OutDirWasSpecified, when OutDir was set before this point and it is '' (GetValue: an environment variable of OutDir that
			//no file set is set there, as MSBuild reads it; the SetIfEmpty below cannot tell).
			if(GetValue("OutDir")!="" && GetValue("OutDirWasSpecified")=="") {
				_dictProperties["OutDirWasSpecified"]="true";
			}
			SetIfEmpty("OutDir",GetValue("OutputPath"));
			if(GetValue("OutDir")!="") {
				AppendTrailingSlash("OutDir");
			}
			SetIfEmpty("ProjectName",GetValue("MSBuildProjectName"));
			//Line 250: the project's own folder in an OutDir that was set, for an app container or a project-specific output folder.
			if(GetValue("OutDir")!="") {
				bool? isProjectSpecific=IsValueIfKnown("OutDirWasSpecified","true")
					& ((IsValueIfKnown("WindowsAppContainer","true") & !IsValueIfKnown("GenerateProjectSpecificOutputFolder","false"))
					| IsValueIfKnown("GenerateProjectSpecificOutputFolder","true"));
				if(isProjectSpecific==null) {
					_dictProperties["OutDir"]=NotKnown("OutDir",FILE,FirstNotKnown("OutDirWasSpecified","WindowsAppContainer","GenerateProjectSpecificOutputFolder"));
				}
				else if(isProjectSpecific.Value) {
					_dictProperties["OutDir"]=GetValue("OutDir")+GetValue("ProjectName")+@"\";
				}
			}
			SetIfEmpty("AssemblyName",GetValue("MSBuildProjectName"));
			//Line 253.
			if(GetValue("TargetName")=="") {
				bool? isWinmdTargetName=IsValueIfKnown("OutputType","winmdobj") & GetValue("RootNamespace")!="";
				if(isWinmdTargetName==null) {
					_dictProperties["TargetName"]=NotKnown("TargetName",FILE,"OutputType");
				}
				else if(isWinmdTargetName.Value) {
					_dictProperties["TargetName"]=GetValue("RootNamespace");
				}
			}
			SetIfEmpty("TargetName",GetValue("AssemblyName"));
			SetIfEmpty("ProjectExt",GetValue("MSBuildProjectExtension"));
			//Lines 260-262.
			bool? isWinmd=IsValueIfKnown("OutputType","winmdobj");
			if(isWinmd==null) {
				SetIfEmpty("WinMDExpOutputWindowsMetadataFilename",NotKnown("WinMDExpOutputWindowsMetadataFilename",FILE,"OutputType"));
				SetIfEmpty("TargetFileName",NotKnown("TargetFileName",FILE,"OutputType"));
			}
			else if(isWinmd.Value) {
				SetIfEmpty("WinMDExpOutputWindowsMetadataFilename",GetValue("TargetName")+".winmd");
				SetIfEmpty("TargetFileName",GetValue("WinMDExpOutputWindowsMetadataFilename"));
			}
			SetIfEmpty("TargetFileName",GetValue("TargetName")+GetValue("TargetExt"));
			//Line 336: set whenever OutDir is not '', over an environment variable of the name.
			string outDir=GetValue("OutDir");
			if(outDir!="") {
				_dictProperties["TargetDir"]=IsUnresolved(outDir) ? outDir : Escape(Path.GetFullPath(Path.Combine(Folder,Unescape(outDir))));
			}
			SetIfEmpty("TargetPath",GetValue("TargetDir")+GetValue("TargetFileName"));
			SetIfEmpty("ProjectDir",Escape(EnsureTrailingSlash(Folder)));
			//Lines 363-372: a project built on its own, without the solution's global properties (or one that set none of them), has these.
			foreach(string name in new[] { "DevEnvDir","SolutionName","SolutionFileName","SolutionPath","SolutionDir","SolutionExt" }) {
				SetIfEmpty(name,"*Undefined*");
			}
			//Line 377.
			bool? isExe=IsValueIfKnown("OutputType","exe","winexe");
			if(isExe==null) {
				_dictProperties["GenerateBindingRedirectsOutputType"]=NotKnown("GenerateBindingRedirectsOutputType",FILE,"OutputType");
			}
			else if(isExe.Value) {
				_dictProperties["GenerateBindingRedirectsOutputType"]="true";
			}
			//Line 379.
			if(GetValue("AutoGenerateBindingRedirects")=="") {
				bool? isFramework=IsValueIfKnown("TargetFrameworkIdentifier",".NETFramework");
				string versionText=isFramework==true ? GetValueIfKnown("TargetFrameworkVersion") : "";
				Version versionFramework;
				if(isFramework==null || versionText==null) {
					_dictProperties["AutoGenerateBindingRedirects"]=NotKnown("AutoGenerateBindingRedirects",FILE,
						isFramework==null ? "TargetFrameworkIdentifier" : "TargetFrameworkVersion");
				}
				else if(isFramework.Value && Version.TryParse(versionText.TrimStart('v','V'),out versionFramework) && versionFramework>=new Version(4,7,2)) {
					_dictProperties["AutoGenerateBindingRedirects"]="true";
				}
			}
			if(GetValue("PublishDir")!="") {
				AppendTrailingSlash("PublishDir");
			}
			else if(!IsSdkStyle) {
				//An SDK-style project's comes from Microsoft.NET.Sdk.BeforeCommon.targets and its runtime identifier: not modelled.
				_dictProperties["PublishDir"]=IsUnresolved(GetValue("OutputPath")) ? GetValue("OutputPath")
					: Escape(EnsureTrailingSlash(GetUnescaped("OutputPath")))+@"app.publish\";
			}
			//MSBuild.exe runs on the .NET Framework (MSBuildRuntimeType Full); MSBuildExtensionsPath64 is set on a 64-bit Windows.
			if(GetValue("DisableOutOfProcTaskHost")=="") {
				if(new[] { "v2.0","v3.0","v3.5" }.Any(x => IsValue("TargetFrameworkVersion",x))) {
					_dictProperties["TargetFrameworkAsMSBuildRuntime"]="CLR2";
				}
				if(new[] { "v4.0","v4.5" }.Any(x => IsValue("TargetFrameworkVersion",x))) {
					_dictProperties["TargetFrameworkAsMSBuildRuntime"]="CLR4";
				}
				if(IsUnresolved(GetValue("TargetFrameworkVersion"))) {
					_dictProperties["TargetFrameworkAsMSBuildRuntime"]=GetValue("TargetFrameworkVersion");
				}
				SetIfEmpty("TargetFrameworkAsMSBuildRuntime","CurrentRuntime");
				_dictProperties["PlatformTargetAsMSBuildArchitectureExplicitlySet"]=GetValue("PlatformTargetAsMSBuildArchitecture")=="" ? "false" : "true";
				if(!IsPropertyKnown("PlatformTarget")) {
					_hashSetNotModelled.Add("PlatformTargetAsMSBuildArchitecture");
				}
				else {
					if(IsValue("PlatformTarget","x86") || (IsValue("PlatformTarget","x64") && Environment.Is64BitOperatingSystem)) {
						_dictProperties["PlatformTargetAsMSBuildArchitecture"]=GetValue("PlatformTarget");
					}
					if(IsValue("PlatformTarget","arm")) {
						_dictProperties["PlatformTargetAsMSBuildArchitecture"]="x86";
					}
					if(IsValue("PlatformTarget","ia64") && Environment.Is64BitOperatingSystem) {
						_dictProperties["PlatformTargetAsMSBuildArchitecture"]="x64";
					}
					SetIfEmpty("PlatformTargetAsMSBuildArchitecture","CurrentArchitecture");
				}
			}
			//Lines 594-595: delay signing only for a signed assembly, and only when it is 'true': anything else is '' (the SDK's DelaySign false
			//included). A SignAssembly not known decides nothing for a DelaySign known not to be 'true' (it is '' either way).
			bool? isSigned=IsValueIfKnown("SignAssembly","true");
			if(isSigned==false) {
				_dictProperties["DelaySign"]="false";
			}
			else if(isSigned==null && IsPropertyTrue("DelaySign")) {
				_dictProperties["DelaySign"]=NotKnown("DelaySign",FILE,"SignAssembly");
			}
			if(IsValueIfKnown("DelaySign","true")==false) {
				_dictProperties["DelaySign"]="";
			}
			ApplyNuGetTargetsProperties();
		}

		///<summary>NuGet.targets (Visual Studio's copy, as NuGet.props), which Microsoft.Common.CurrentVersion.targets imports at its end (lines
		///7131-7138) unless IsRestoreTargetsFileLoaded is 'true': its _CentralPackageVersionsEnabled (line 65; the SDK's copy, line 63), true when
		///'$(ManagePackageVersionsCentrally)' == 'true' AND '$(CentralPackageVersionsFileImported)' == 'true', else what it was. An operand MSBuild
		///reads that is not known here (an environment variable no file set, GetValueIfKnown) leaves the switch not known, unless it is true
		///already, so GetProperty and GetPackageVersion throw for it; MSBuild reads the second operand only when the first is true, and a false
		///operand makes the AND false whichever the other is, so one known to be false decides it (three-valued, IsValueIfKnown). A NuGet.targets
		///that a file or an environment variable names (NuGetRestoreTargets), or an IsRestoreTargetsFileLoaded not known, leaves the switch not
		///known too.</summary>
		private void ApplyNuGetTargetsProperties() {
			const string FILE="NuGet.targets";
			bool? isImported=!IsValueIfKnown("IsRestoreTargetsFileLoaded","true");
			string restoreTargets=GetValueIfKnown("NuGetRestoreTargets");
			bool? isEnabled;
			string fromName;
			if(isImported==false) {
				return;
			}
			if(isImported==null || restoreTargets!="") {
				isEnabled=null;
				fromName=isImported==null ? "IsRestoreTargetsFileLoaded" : "NuGetRestoreTargets";
			}
			else {
				isEnabled=IsValueIfKnown("ManagePackageVersionsCentrally","true") & IsValueIfKnown("CentralPackageVersionsFileImported","true");
				fromName=FirstNotKnown("ManagePackageVersionsCentrally","CentralPackageVersionsFileImported");
			}
			if(isEnabled==true) {
				_dictProperties["_CentralPackageVersionsEnabled"]="true";
			}
			else if(isEnabled==null && IsValueIfKnown("_CentralPackageVersionsEnabled","true")!=true) {
				_dictProperties["_CentralPackageVersionsEnabled"]=fromName=="NuGetRestoreTargets" && GetValueIfKnown("NuGetRestoreTargets")!=null
					? Unresolved("the _CentralPackageVersionsEnabled of the NuGet.targets $(NuGetRestoreTargets) names")
					: NotKnown("_CentralPackageVersionsEnabled",FILE,fromName);
			}
		}

		///<summary>The first of the names whose value is not known (GetValueIfKnown), for a message; the first name when all are known.</summary>
		private string FirstNotKnown(params string[] arrayNames) {
			return arrayNames.FirstOrDefault(x => GetValueIfKnown(x)==null)??arrayNames[0];
		}

		///<summary>Microsoft.NETFramework.props (Microsoft.NETFramework.CurrentVersion.props), which Microsoft.Common.CurrentVersion.targets imports
		///for a .NET Framework target right after its defaults, and Microsoft.NET.props, which that file imports at its end and the common targets
		///import for .NET and .NET Standard: TargetingClr2Framework for 2.0, 3.0 and 3.5; Prefer32Bit true for an exe (or a project without an
		///OutputType yet) on 4.5 and later unless PreferNativeArm64, else false; SubsystemVersion 6.02 for ARM, an app container exe or a WinMD,
		///6.00 for .NET Framework 4.5 and later; HighEntropyVA true for those and for .NET and .NET Standard, else false. Each only when the
		///project leaves it empty. A name these files read that is not known (TargetingClr2Framework, which they set only for 2.0 to 3.5,
		///OutputType, PreferNativeArm64, PlatformTarget: an environment variable no file set) makes the default that depends on it not known.
		///</summary>
		private void ApplyFrameworkProps() {
			const string FILE="Microsoft.NETFramework.CurrentVersion.props";
			bool isFramework=IsValue("TargetFrameworkIdentifier",".NETFramework") || IsValue("TargetFrameworkIdentifier","Silverlight");
			bool isNet=IsValue("TargetFrameworkIdentifier",".NETStandard") || IsValue("TargetFrameworkIdentifier",".NETCoreApp");
			//Microsoft.Common.CurrentVersion.targets lines 80-81: which file is imported is not known (GetValue: an environment variable no file
			//set among them).
			if(IsUnresolved(GetValue("TargetFrameworkIdentifier")) || IsUnresolved(GetValue("TargetFrameworkVersion"))) {
				foreach(string name in new[] { "Prefer32Bit","SubsystemVersion","HighEntropyVA" }) {
					if(GetValue(name)=="") {
						_dictProperties[name]=NotKnown(name,FILE,FirstNotKnown("TargetFrameworkIdentifier","TargetFrameworkVersion"));
					}
				}
				return;
			}
			if(!isFramework && !isNet) {
				return;
			}
			bool? isFramework45=false;
			if(isFramework) {
				//Line 76; otherwise an environment variable of the name stands where lines 86 and 98 and Microsoft.NET.props line 35 read it.
				if(new[] { "v2.0","v3.0","v3.5" }.Any(x => IsValue("TargetFrameworkVersion",x))) {
					_dictProperties["TargetingClr2Framework"]="true";
				}
				isFramework45=IsValue("TargetFrameworkIdentifier",".NETFramework") & !IsValueIfKnown("TargetingClr2Framework","true") & !IsValue("TargetFrameworkVersion","v4.0");
				//Lines 86-91: OutputType is the project's here (the common targets' default comes later), so '' counts.
				bool? isPrefer32Bit=isFramework45 & IsValueIfKnown("OutputType","exe","winexe","appcontainerexe","") & !IsValueIfKnown("PreferNativeArm64","true");
				if(isPrefer32Bit==null) {
					SetIfEmpty("Prefer32Bit",NotKnown("Prefer32Bit",FILE,FirstNotKnown("TargetingClr2Framework","OutputType","PreferNativeArm64")));
				}
				else if(isPrefer32Bit.Value) {
					SetIfEmpty("Prefer32Bit","true");
				}
				SetIfEmpty("Prefer32Bit","false");
				//Lines 96-98.
				if(GetValue("SubsystemVersion")=="") {
					bool? isSubsystem602=IsValueIfKnown("PlatformTarget","ARM") | IsValueIfKnown("OutputType","appcontainerexe","winmdobj");
					if(isSubsystem602==null) {
						_dictProperties["SubsystemVersion"]=NotKnown("SubsystemVersion",FILE,FirstNotKnown("PlatformTarget","OutputType"));
					}
					else if(isSubsystem602.Value) {
						_dictProperties["SubsystemVersion"]="6.02";
					}
				}
				if(isFramework45==null) {
					SetIfEmpty("SubsystemVersion",NotKnown("SubsystemVersion",FILE,"TargetingClr2Framework"));
				}
				else if(isFramework45.Value) {
					SetIfEmpty("SubsystemVersion","6.00");
				}
			}
			//Microsoft.NET.props lines 35-40.
			bool? isHighEntropyVA=isFramework45 | isNet;
			if(isHighEntropyVA==null) {
				SetIfEmpty("HighEntropyVA",NotKnown("HighEntropyVA","Microsoft.NET.props","TargetingClr2Framework"));
			}
			else if(isHighEntropyVA.Value) {
				SetIfEmpty("HighEntropyVA","true");
			}
			SetIfEmpty("HighEntropyVA","false");
		}

		///<summary>Sdk.props of Microsoft.NET.Sdk: UsingMicrosoftNETSdk, Microsoft.Common.props (Directory.Build.props), the defaults of
		///Microsoft.NET.Sdk.props that the project's own properties may read or override (DisableHandlePackageFileConflicts; Configurations and
		///Platforms; PackRelease; OutputType Library, FileAlignment, ErrorReport, AssemblyName and RootNamespace from the project's name,
		///Deterministic; DebugSymbols and Optimize for Debug, Optimize for Release; PlatformTarget from an x86, x64, ARM or arm64 platform;
		///DebugType; RestoreProjectStyle PackageReference; the switches that default to false), Microsoft.NET.Sdk.DefaultItems.props and
		///Microsoft.NET.Sdk.WindowsDesktop.props, whose default items come before the project's own, and for a C# project
		///Microsoft.NET.Sdk.CSharp.props: NoWarn 1701;1702 unless set, NU1605 added to WarningsAsErrors, and TRACE added to DefineConstants (after
		///a ';' when it is not empty). All before the project's own properties, which may change them.</summary>
		private void ImportSdkProps() {
			_dictProperties["UsingMicrosoftNETSdk"]="true";
			_dictProperties["UsingNETSdkDefaults"]="true";
			//Sdk.props lines 48-49: another file instead of Microsoft.Common.props when a file or an environment variable names one.
			ThrowIfImportHook("AlternateCommonProps","Sdk.props");
			ImportMicrosoftCommonProps();
			//Microsoft.NET.Sdk.props, in its order.
			_dictProperties["DisableHandlePackageFileConflicts"]="true";
			SetIfEmpty("Configurations","Debug;Release");
			SetIfEmpty("Platforms","AnyCPU");
			SetIfEmpty("PackRelease","true");
			SetIfEmpty("OutputType","Library");
			SetIfEmpty("FileAlignment","512");
			SetIfEmpty("ErrorReport","prompt");
			SetIfEmpty("AssemblyName",GetValue("MSBuildProjectName"));
			SetIfEmpty("RootNamespace",Escape(GetUnescaped("MSBuildProjectName").Replace(" ","_")));
			SetIfEmpty("Deterministic","true");
			if(IsValue("Configuration","Debug")) {
				SetIfEmpty("DebugSymbols","true");
				SetIfEmpty("Optimize","false");
			}
			if(IsValue("Configuration","Release")) {
				SetIfEmpty("Optimize","true");
			}
			foreach(string platformTarget in new[] { "x64","x86","ARM","arm64" }) {
				if(IsValue("Platform",platformTarget)) {
					SetIfEmpty("PlatformTarget",platformTarget);
				}
			}
			//Lines 96-97: DebugType before DebugSymbols' own default: a Release project that sets neither gets portable and DebugSymbols false.
			//DebugSymbols not known (an environment variable no file set; for Debug, SetIfEmpty above) makes DebugType not known: line 96 sets it
			//over the project's own.
			bool? isNoDebugSymbols=IsValueIfKnown("DebugSymbols","false");
			if(isNoDebugSymbols==null) {
				_dictProperties["DebugType"]=NotKnown("DebugType","Microsoft.NET.Sdk.props","DebugSymbols");
			}
			else if(isNoDebugSymbols.Value) {
				_dictProperties["DebugType"]="None";
			}
			SetIfEmpty("DebugType","portable");
			_dictProperties["RestoreProjectStyle"]="PackageReference";
			foreach(string name in new[] { "AllowUnsafeBlocks","TreatWarningsAsErrors","Prefer32Bit","PreferNativeArm64","SignAssembly","DelaySign",
				"GeneratePackageOnBuild","PackageRequireLicenseAcceptance","DebugSymbols","CheckForOverflowUnderflow" })
			{
				SetIfEmpty(name,"false");
			}
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_DEFAULT_ITEMS });
			//Microsoft.NET.Sdk.CSharp.props, which Microsoft.NET.Sdk.props imports after Microsoft.NET.Sdk.DefaultItems.props.
			//Lines 15-22 (GetValue: WarningsAsErrors or DefineConstants an environment variable no file set makes it not known).
			if(IsCSharpProject()) {
				SetIfEmpty("NoWarn","1701;1702");
				_dictProperties["WarningsAsErrors"]=GetValue("WarningsAsErrors")+";NU1605";
				string defineConstants=GetValue("DefineConstants");
				_dictProperties["DefineConstants"]=(defineConstants=="" ? "" : defineConstants+";")+"TRACE";
			}
			//Microsoft.NET.Sdk.props line 182: a file of the project's choice after the SDK's props.
			ThrowIfImportHook("AfterMicrosoftNetSdkProps","Microsoft.NET.Sdk.props");
		}

		///<summary>True for a .csproj, the project files whose SDK language files (Microsoft.NET.Sdk.CSharp.props and .targets) this reader
		///models.</summary>
		private bool IsCSharpProject() {
			return Path.GetExtension(FullPath).Equals(".csproj",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Sdk.targets of Microsoft.NET.Sdk, in its order: Microsoft.NET.Sdk.BeforeCommon.targets (ApplySdkBeforeCommonProperties); the
		///language targets with Microsoft.Common.targets and Directory.Build.targets; Microsoft.NET.Sdk.targets with the assets file,
		///SupportsRoslynComponentVersioning (true) and CopyLocalLockFileAssemblies (Microsoft.PackageDependencyResolution.targets: false for .NET
		///Standard and for .NET before 3.0 or without runtime output, else true), the default item switches and excludes of Microsoft.NET.Sdk.DefaultItems.targets and its automatic Link
		///metadata (an item step, after every item before this point), IsPublishable (Microsoft.NET.Publish.targets), for a C# project the compiler
		///defaults of Microsoft.NET.Sdk.targets, Microsoft.NET.Sdk.CSharp.targets and Microsoft.NET.Sdk.Analyzers.targets
		///(ApplySdkLanguageProperties); then, for a WPF or Windows Forms project, Microsoft.NET.Sdk.WindowsDesktop.targets with its defaults and
		///Page removals.</summary>
		private void ImportSdkTargets() {
			//Sdk.targets line 13: a file of the project's choice first; lines 16-23: the cross-targeting targets instead of the rest when
			//IsCrossTargetingBuild is 'true' (line 16 sets it for TargetFrameworks without a TargetFramework, which
			//ApplySdkBeforeCommonProperties refuses; otherwise a file or an environment variable may set it).
			ThrowIfImportHook("BeforeMicrosoftNETSdkTargets","Sdk.targets");
			if(!(GetValue("TargetFrameworks")!="" && GetValue("TargetFramework")=="") && IsValueIfKnown("IsCrossTargetingBuild","true")!=false) {
				ThrowIfImportHook("IsCrossTargetingBuild","Sdk.targets");
			}
			ApplySdkBeforeCommonProperties();
			//Sdk.targets lines 25-38: the language targets, unless a file or an environment variable names others.
			ThrowIfImportHook("LanguageTargets","Sdk.targets");
			ImportMicrosoftCommonTargets(Path.GetExtension(FullPath).Equals(".vbproj",StringComparison.OrdinalIgnoreCase) ? "Microsoft.VisualBasic.targets"
				: "Microsoft.CSharp.targets");
			//Microsoft.PackageDependencyResolution.targets: the restore's assets file (obj\project.assets.json unless the restore's props named
			//it), as a full path.
			SetIfEmpty("ProjectAssetsFile",GetValue("MSBuildProjectExtensionsPath")+"/project.assets.json");
			if(!IsUnresolved(GetValue("ProjectAssetsFile"))) {
				_dictProperties["ProjectAssetsFile"]=Escape(Path.GetFullPath(Path.Combine(Folder,Unescape(GetValue("ProjectAssetsFile")))));
			}
			//The flag that packages with analyzers for several Roslyn versions read (System.Text.Json's and Microsoft.Extensions.Logging.Abstractions'
			//build files): always true.
			_dictProperties["SupportsRoslynComponentVersioning"]="true";
			//Microsoft.PackageDependencyResolution.targets lines 31-42. A name these lines read that is not known (the target framework,
			//HasRuntimeOutput, EnableDynamicLoading: an environment variable no file set, or a value not known) makes it not known.
			if(GetValue("CopyLocalLockFileAssemblies")=="") {
				if(IsUnresolved(GetValue("TargetFrameworkIdentifier"))) {
					_dictProperties["CopyLocalLockFileAssemblies"]=GetValue("TargetFrameworkIdentifier");
				}
				else {
					string versionText=GetValueIfKnown("_TargetFrameworkVersionWithoutV");
					Version versionNet;
					bool? isBelow3=versionText==null ? (bool?)null : Version.TryParse(versionText,out versionNet) && versionNet<new Version(3,0);
					bool? isNotCopied=IsValue("TargetFrameworkIdentifier",".NETStandard") | (IsValue("TargetFrameworkIdentifier",".NETCoreApp")
						& (isBelow3 | (!IsValueIfKnown("HasRuntimeOutput","true") & !IsValueIfKnown("EnableDynamicLoading","true"))));
					if(isNotCopied==null) {
						_dictProperties["CopyLocalLockFileAssemblies"]=NotKnown("CopyLocalLockFileAssemblies","Microsoft.PackageDependencyResolution.targets",
							FirstNotKnown("_TargetFrameworkVersionWithoutV","HasRuntimeOutput","EnableDynamicLoading"));
					}
					else {
						_dictProperties["CopyLocalLockFileAssemblies"]=isNotCopied.Value ? "false" : "true";
					}
				}
			}
			//Microsoft.NET.Sdk.DefaultItems.targets lines 18-27: no default items when DisableDefaultItemsInProjectFolder is 'true' and the
			//project's folder holds a solution or a C# project (the project itself, for a .csproj); a switch not known makes them not known.
			//EnableDefaultItems is read before the SetIfEmpty below stores it: an environment variable no file set is not '' there (GetValue).
			bool? isDisabledInFolder=GetValue("EnableDefaultItems")=="" ? IsValueIfKnown("DisableDefaultItemsInProjectFolder","true") : false;
			if(isDisabledInFolder!=false) {
				isDisabledInFolder&=new[] { "*.sln","*.slnx","*.csproj" }.Any(x => Directory.GetFiles(Folder,x).Length!=0);
				if(isDisabledInFolder==null) {
					_dictProperties["EnableDefaultItems"]=NotKnown("EnableDefaultItems","Microsoft.NET.Sdk.DefaultItems.targets","DisableDefaultItemsInProjectFolder");
				}
				else if(isDisabledInFolder.Value) {
					_dictProperties["EnableDefaultItems"]="false";
				}
			}
			SetIfEmpty("EnableDefaultItems","true");
			SetIfEmpty("EnableDefaultCompileItems","true");
			SetIfEmpty("EnableDefaultEmbeddedResourceItems","true");
			SetIfEmpty("EnableDefaultNoneItems","true");
			_dictProperties["DefaultItemExcludes"]=GetValue("DefaultItemExcludes")+";"+GetValue("BaseOutputPath")+"/**;"
				+GetValue("BaseIntermediateOutputPath")+"/**;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";
			_dictProperties["DefaultExcludesInProjectFolder"]=GetValue("DefaultExcludesInProjectFolder")+";"
				+GetValue("DefaultItemExcludesInProjectFolder")+";**/.*/**";
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_LINK_METADATA });
			SetIfEmpty("IsPublishable","true");
			if(IsCSharpProject()) {
				ApplySdkLanguageProperties();
			}
			//Microsoft.NET.Sdk.targets lines 1468-1473: the WindowsDesktop targets for a WPF or Windows Forms project of .NET Framework, or of .NET
			//Core 3.0 or later for Windows. An import, so a name these lines read that is not known (an environment variable no file set, a value
			//not known) throws (GetKnownValue), where MSBuild reads it.
			if(GetValue("ImportWindowsDesktopTargets")=="" && (IsKnownTrue("UseWPF") || IsKnownTrue("UseWindowsForms"))) {
				string frameworkIdentifier=GetKnownValue("TargetFrameworkIdentifier");
				Version versionFramework;
				if(frameworkIdentifier.Equals(".NETCoreApp",StringComparison.OrdinalIgnoreCase)
					&& Version.TryParse(GetKnownValue("_TargetFrameworkVersionWithoutV"),out versionFramework) && versionFramework>=new Version(3,0)
					&& GetKnownValue("TargetPlatformIdentifier").Equals("Windows",StringComparison.OrdinalIgnoreCase))
				{
					_dictProperties["ImportWindowsDesktopTargets"]="true";
				}
				if(frameworkIdentifier.Equals(".NETFramework",StringComparison.OrdinalIgnoreCase)) {
					_dictProperties["ImportWindowsDesktopTargets"]="true";
				}
			}
			_isWindowsDesktopTargetsImported=IsKnownTrue("ImportWindowsDesktopTargets");
			//Lines 1476 and 1510: a file of the project's choice after the SDK's targets (and before the WindowsDesktop targets, which line 1476
			//adds to the same list).
			ThrowIfImportHook("AfterMicrosoftNETSdkTargets","Microsoft.NET.Sdk.targets");
			//Microsoft.NET.Sdk.WindowsDesktop.targets lines 5-7.
			if(_isWindowsDesktopTargetsImported) {
				SetIfEmpty("EnableDefaultPageItems","true");
				SetIfEmpty("EnableDefaultApplicationDefinition","true");
				SetIfEmpty("DefaultXamlRuntime","Wpf");
			}
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_TARGETS_ITEMS });
		}

		///<summary>The part of Microsoft.NET.Sdk.BeforeCommon.targets (SDK 10.0.114) that comes before the common targets: whether the project is
		///an exe; the platform name; the target framework's identifier, version and platform (Microsoft.NET.TargetFrameworkInference.targets, with
		///ProduceReferenceAssembly true for .NET 5 and later, .NET 7 and later for F#); the output and intermediate paths
		///(Microsoft.NET.DefaultOutputPaths.targets: bin\ and obj\, then the platform folder unless the platform is AnyCPU or
		///AppendPlatformToOutputPath is false, the configuration, both added to DefaultItemExcludes, then the target framework unless
		///AppendTargetFrameworkToOutputPath is false; a runtime identifier folder is not modelled); the SDK's AutoGenerateBindingRedirects for a .NET
		///Framework exe; GenerateDocumentationFile (true when the project names a DocumentationFile) and the DocumentationFile it gives
		///($(IntermediateOutputPath)$(AssemblyName).xml, $(AssemblyName).xml for Visual Basic, '' when no documentation is generated).</summary>
		private void ApplySdkBeforeCommonProperties() {
			const string FILE_INFERENCE="Microsoft.NET.TargetFrameworkInference.targets";
			const string FILE_OUTPUT="Microsoft.NET.DefaultOutputPaths.targets";
			_hashSetNotModelled.UnionWith(_arrayNotModelledToolsetProperties);
			_hashSetNotModelled.UnionWith(_arrayNotModelledSdkProperties);
			//Microsoft.NET.Sdk.BeforeCommon.targets line 24: _IsExecutable only for an exe (otherwise an environment variable of the name stands
			//where line 28 reads it for HasRuntimeOutput).
			bool? isExecutable=IsValueIfKnown("OutputType","Exe","WinExe");
			if(isExecutable==null) {
				_dictProperties["_IsExecutable"]=NotKnown("_IsExecutable","Microsoft.NET.Sdk.BeforeCommon.targets","OutputType");
			}
			else if(isExecutable.Value) {
				_dictProperties["_IsExecutable"]="true";
			}
			if(GetValue("HasRuntimeOutput")=="") {
				_dictProperties["HasRuntimeOutput"]=GetValue("_IsExecutable");
			}
			SetIfEmpty("PlatformName",GetValue("Platform"));
			//Lines 47-48: the publish profile a file or an environment variable names (Microsoft.NET.Sdk.ImportPublishProfile.targets), not
			//modelled; line 73: the Razor SDK's targets.
			ThrowIfPublishProfile();
			if(IsValueIfKnown("UsingMicrosoftNETSdkRazor","true")!=false) {
				ThrowIfImportHook("UsingMicrosoftNETSdkRazor","Microsoft.NET.Sdk.BeforeCommon.targets");
			}
			//Microsoft.NET.TargetFrameworkInference.targets, imported for every SDK-style project (line 54 of BeforeCommon): line 47, a file of the
			//project's choice first; lines 52-56, the identifier and version inferred unless both are set (an environment variable of the name
			//among them: GetValue); line 59, _TargetFrameworkVersionWithoutV; lines 65-84, the platform.
			ThrowIfImportHook("BeforeTargetFrameworkInferenceTargets",FILE_INFERENCE);
			string targetFramework=GetUnescaped("TargetFramework");
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
						_dictProperties[name]=Unresolved("the "+name+" of TargetFramework "+TextOf(targetFramework));
					}
				}
				else {
					if(GetValue("TargetFrameworkIdentifier")=="" || GetValue("TargetFrameworkVersion")=="") {
						_dictProperties["TargetFrameworkIdentifier"]=frameworkIdentifier;
						_dictProperties["TargetFrameworkVersion"]="v"+frameworkVersion;
					}
					if(GetValue("TargetPlatformIdentifier")=="" || GetValue("TargetPlatformVersion")=="") {
						//An identifier not known (an environment variable no file set: SetIfEmpty) is not 'Windows' here; the version is then the
						//common targets' 7.0 (a framework without a platform, as MSBuild's lines 67-81 leave it '') or not modelled (.NET 5 and
						//later, below), so nothing is given a value that depends on it.
						SetIfEmpty("TargetPlatformIdentifier",platformIdentifier);
						if(IsValue("TargetPlatformIdentifier","Windows")) {
							_dictProperties["TargetPlatformIdentifier"]="Windows";
							if(platformVersion!="") {
								SetIfEmpty("TargetPlatformVersion",platformVersion);
							}
						}
					}
				}
			}
			_dictProperties["_TargetFrameworkVersionWithoutV"]=Escape(GetUnescaped("TargetFrameworkVersion").TrimStart('v','V'));
			//Lines 93-95: _UnsupportedTargetFrameworkError when the framework is not known to the SDK (so an environment variable of the name stands
			//otherwise, where Microsoft.NET.DefaultOutputPaths.targets lines 141 and 146 read it).
			bool? isUnsupported=GetValue("TargetFrameworkIdentifier")=="" | IsValueIfKnown("TargetFrameworkIdentifier","Unsupported") | GetValue("TargetFrameworkVersion")=="";
			if(isUnsupported==null) {
				_dictProperties["_UnsupportedTargetFrameworkError"]=NotKnown("_UnsupportedTargetFrameworkError",FILE_INFERENCE,"TargetFrameworkIdentifier");
			}
			else if(isUnsupported.Value) {
				_dictProperties["_UnsupportedTargetFrameworkError"]="true";
			}
			//Line 215: _EnableDefaultWindowsPlatform false for .NET 5 and later, unless the project is C++ (Language, which the language targets set
			//later on, so here only a file or an environment variable).
			Version versionFramework;
			string versionText=GetValueIfKnown("_TargetFrameworkVersionWithoutV");
			bool? isNet5=IsValueIfKnown("TargetFrameworkIdentifier",".NETCoreApp")
				& (versionText==null ? (bool?)null : Version.TryParse(versionText,out versionFramework) && versionFramework>=new Version(5,0));
			bool? isDefaultWindowsPlatformOff=isNet5 & (!IsValueIfKnown("Language","C++") | IsValueIfKnown("_EnablePackageReferencesInVCProjects","true"));
			if(isDefaultWindowsPlatformOff==null) {
				_dictProperties["_EnableDefaultWindowsPlatform"]=NotKnown("_EnableDefaultWindowsPlatform",FILE_INFERENCE,
					FirstNotKnown("TargetFrameworkIdentifier","_TargetFrameworkVersionWithoutV","Language","_EnablePackageReferencesInVCProjects"));
			}
			else if(isDefaultWindowsPlatformOff.Value) {
				_dictProperties["_EnableDefaultWindowsPlatform"]="false";
				if(GetValue("TargetPlatformIdentifier")!="" && GetValue("TargetPlatformVersion")=="") {
					//The SDK's platform files give it a default later on; not modelled.
					_hashSetNotModelled.Add("TargetPlatformVersion");
					_hashSetNotModelled.Add("TargetPlatformMoniker");
				}
			}
			//Line 278: a file of the project's choice; lines 281-282: ProduceReferenceAssembly for .NET 5 and later (F#: 7), unless
			//ProduceOnlyReferenceAssembly.
			ThrowIfImportHook("AfterTargetFrameworkInferenceTargets",FILE_INFERENCE);
			if(GetValue("ProduceReferenceAssembly")=="") {
				bool isFSharp=Path.GetExtension(FullPath).Equals(".fsproj",StringComparison.OrdinalIgnoreCase);
				if(IsUnresolved(GetValue("TargetFrameworkIdentifier"))) {
					_dictProperties["ProduceReferenceAssembly"]=GetValue("TargetFrameworkIdentifier");
				}
				else {
					bool? isProduced=IsValueIfKnown("TargetFrameworkIdentifier",".NETCoreApp")
						& (versionText==null ? (bool?)null : Version.TryParse(versionText,out versionFramework) && versionFramework>=new Version(isFSharp ? 7 : 5,0))
						& !IsValueIfKnown("ProduceOnlyReferenceAssembly","true");
					if(isProduced==null) {
						_dictProperties["ProduceReferenceAssembly"]=NotKnown("ProduceReferenceAssembly",FILE_INFERENCE,
							FirstNotKnown("_TargetFrameworkVersionWithoutV","ProduceOnlyReferenceAssembly"));
					}
					else if(isProduced.Value) {
						_dictProperties["ProduceReferenceAssembly"]="true";
					}
				}
			}
			//Microsoft.NET.DefaultOutputPaths.targets: the artifacts layout (UseArtifactsOutput, lines 48-101; UseArtifactsIntermediateOutput, line
			//110) is not modelled: 'true' or not known throws.
			if(IsKnownTrue("UseArtifactsOutput") || IsKnownTrue("UseArtifactsIntermediateOutput")) {
				throw new NotSupportedException(FullPath+": the SDK's artifacts output layout (UseArtifactsOutput) is not modelled by ProjectFile.");
			}
			//Lines 25-28.
			SetIfEmpty("AppendTargetFrameworkToOutputPath","true");
			if(GetValue("AppendPlatformToOutputPath")=="") {
				bool? isAnyCpu=IsValueIfKnown("PlatformName","AnyCPU");
				_dictProperties["AppendPlatformToOutputPath"]=isAnyCpu==null ? NotKnown("AppendPlatformToOutputPath",FILE_OUTPUT,"PlatformName")
					: isAnyCpu.Value ? "false" : "true";
			}
			bool? isPlatformAppended=IsValueIfKnown("AppendPlatformToOutputPath","true");
			if(isPlatformAppended==null) {
				_dictProperties["_PlatformToAppendToOutputPath"]=NotKnown("_PlatformToAppendToOutputPath",FILE_OUTPUT,"AppendPlatformToOutputPath");
			}
			else if(isPlatformAppended.Value) {
				_dictProperties["_PlatformToAppendToOutputPath"]=GetValue("PlatformName")+@"\";
			}
			//Lines 101-114 (GetValue: an _PlatformToAppendToOutputPath of an environment variable stands when the platform is not appended).
			string platformFolder=GetValue("_PlatformToAppendToOutputPath");
			SetIfEmpty("BaseOutputPath",@"bin\");
			AppendTrailingSlash("BaseOutputPath");
			SetIfEmpty("OutputPath",GetValue("BaseOutputPath")+platformFolder+GetValue("Configuration")+@"\");
			AppendTrailingSlash("OutputPath");
			SetIfEmpty("BaseIntermediateOutputPath",@"obj\");
			AppendTrailingSlash("BaseIntermediateOutputPath");
			SetIfEmpty("IntermediateOutputPath",GetValue("BaseIntermediateOutputPath")+platformFolder+GetValue("Configuration")+@"\");
			AppendTrailingSlash("IntermediateOutputPath");
			//Lines 125-127.
			_dictProperties["DefaultItemExcludes"]=GetValue("DefaultItemExcludes")+";"+GetValue("OutputPath")+"/**;"+GetValue("IntermediateOutputPath")+"/**";
			//Lines 140-147: the target framework's folder, unless the SDK does not know the framework.
			bool? isFrameworkAppended=targetFramework!=""
				& (IsValueIfKnown("AppendTargetFrameworkToOutputPath","true") & !IsValueIfKnown("_UnsupportedTargetFrameworkError","true"));
			if(isFrameworkAppended!=false) {
				foreach(string name in new[] { "OutputPath","IntermediateOutputPath" }) {
					if(!isFrameworkKnown) {
						_dictProperties[name]=Unresolved("the "+name+" of TargetFramework "+TextOf(targetFramework));
					}
					else if(isFrameworkAppended==null) {
						_dictProperties[name]=NotKnown(name,FILE_OUTPUT,FirstNotKnown("AppendTargetFrameworkToOutputPath","_UnsupportedTargetFrameworkError"));
					}
					else if(isFrameworkAppended.Value) {
						_dictProperties[name]=GetValue(name)+Escape(targetFramework.ToLowerInvariant())+@"\";
					}
				}
			}
			//Microsoft.NET.RuntimeIdentifierInference.targets sets the platform target from the runtime identifier it infers when the project has
			//none (Directory.Build.props gives every SDK-style project x86); not modelled. It also adds a runtime identifier the project names
			//itself to the output paths unless AppendRuntimeIdentifierToOutputPath is false (Directory.Build.props): not modelled either (lines 358,
			//368; a switch not known is not 'false' here, so the paths are not known either way).
			if(GetValue("PlatformTarget")=="") {
				_hashSetNotModelled.Add("PlatformTarget");
			}
			if(GetValue("RuntimeIdentifier")!="" && !IsPropertyFalse("AppendRuntimeIdentifierToOutputPath")) {
				foreach(string name in new[] { "OutputPath","IntermediateOutputPath" }) {
					_dictProperties[name]=Unresolved("the "+name+" of RuntimeIdentifier "+TextOf(GetUnescaped("RuntimeIdentifier")));
				}
			}
			//Microsoft.NET.Sdk.BeforeCommon.targets lines 102-104 (a TargetFrameworkIdentifier not known is not .NETFramework here, and the common
			//targets' line 379 then makes AutoGenerateBindingRedirects not known; a HasRuntimeOutput not known does it here).
			bool? isBindingRedirected=IsValue("TargetFrameworkIdentifier",".NETFramework") & IsValueIfKnown("HasRuntimeOutput","true");
			if(isBindingRedirected==null) {
				SetIfEmpty("AutoGenerateBindingRedirects",NotKnown("AutoGenerateBindingRedirects","Microsoft.NET.Sdk.BeforeCommon.targets","HasRuntimeOutput"));
			}
			else if(isBindingRedirected.Value) {
				SetIfEmpty("AutoGenerateBindingRedirects","true");
			}
			//Lines 301-313: the XML documentation file.
			if(GetValue("GenerateDocumentationFile")=="") {
				_dictProperties["GenerateDocumentationFile"]=GetValue("DocumentationFile")=="" ? "false" : "true";
			}
			bool? isDocumented=IsValueIfKnown("GenerateDocumentationFile","true");
			if(isDocumented==null) {
				_dictProperties["DocumentationFile"]=NotKnown("DocumentationFile","Microsoft.NET.Sdk.BeforeCommon.targets","GenerateDocumentationFile");
			}
			else if(isDocumented.Value && GetValue("DocumentationFile")=="") {
				_dictProperties["DocumentationFile"]=(Path.GetExtension(FullPath).Equals(".vbproj",StringComparison.OrdinalIgnoreCase) ? "" : GetValue("IntermediateOutputPath"))
					+GetValue("AssemblyName")+".xml";
			}
			else if(!isDocumented.Value) {
				_dictProperties["DocumentationFile"]="";
			}
		}

		///<summary>Microsoft.NET.Sdk.BeforeCommon.targets lines 47-48 import Microsoft.NET.Sdk.ImportPublishProfile.targets (unless
		///PublishProfileImported is 'true'), which imports the publish profile WebPublishProfileFile names, or PublishProfileFullPath, or the
		///.pubxml of PublishProfileName or PublishProfile in Properties\PublishProfiles, when it is there. This reader does not read publish
		///profiles: when a file or an environment variable sets one of those names, it throws (whether a profile is imported is not known), unless
		///PublishProfileImported is known to be 'true'.</summary>
		private void ThrowIfPublishProfile() {
			string[] arrayNames=new[] { "WebPublishProfileFile","PublishProfileFullPath","PublishProfileName","PublishProfile" };
			if(arrayNames.All(x => GetValueIfKnown(x)=="") || IsValueIfKnown("PublishProfileImported","true")==true) {
				return;
			}
			foreach(string name in arrayNames) {
				ThrowIfImportHook(name,"Microsoft.NET.Sdk.ImportPublishProfile.targets");
			}
		}

		///<summary>The compiler defaults that the SDK (10.0.114) computes after the project's own properties for a C# project, in its order:
		///Microsoft.NET.Sdk.targets turns BinaryFormatter's obsolete warning into an error for .NET 7 and later unless
		///EnableUnsafeBinaryFormatterSerialization is true, and sets that false from .NET 8 on (for Windows Forms and WPF from .NET 9 on);
		///Microsoft.NET.Sdk.CSharp.targets adds the configuration's define to DefineConstants
		///(ImplicitConfigurationDefine: the configuration in capitals with '-', '.' and ' ' made '_', always after a ';') unless
		///DisableImplicitConfigurationDefines is true, and SYSLIB0011 to WarningsAsErrors for that error; Microsoft.NET.Sdk.Analyzers.targets
		///sets WarningLevel when the project has none: 9999 for AnalysisLevel preview, 4 for .NET Framework, the major version for .NET. The
		///symbols the SDK's targets add while building (NETFRAMEWORK, NET48 and the like) are not part of the evaluation, so not here either. For a
		///target framework this reader does not know, what depends on it cannot be evaluated.</summary>
		private void ApplySdkLanguageProperties() {
			const string FILE_SDK="Microsoft.NET.Sdk.targets";
			const string FILE_CSHARP="Microsoft.NET.Sdk.CSharp.targets";
			string versionText=GetValueIfKnown("_TargetFrameworkVersionWithoutV");
			Version versionFramework=null;
			bool? hasFrameworkVersion=versionText==null ? (bool?)null : Version.TryParse(versionText,out versionFramework);
			//Microsoft.NET.Sdk.targets lines 189-201 (three-valued: a name the group reads that is not known, an environment variable no file set
			//or a framework not known, makes what it sets not known).
			bool? isBinaryFormatterGroup=!IsValueIfKnown("EnableUnsafeBinaryFormatterSerialization","true") & IsValueIfKnown("TargetFrameworkIdentifier",".NETCoreApp")
				& (hasFrameworkVersion==null ? (bool?)null : hasFrameworkVersion.Value && versionFramework>=new Version(7,0));
			if(isBinaryFormatterGroup==null) {
				string fromName=FirstNotKnown("EnableUnsafeBinaryFormatterSerialization","TargetFrameworkIdentifier","_TargetFrameworkVersionWithoutV");
				foreach(string name in new[] { "_ProjectTypeRequiresBinaryFormatter","_BinaryFormatterObsoleteAsError","EnableUnsafeBinaryFormatterSerialization" }) {
					_dictProperties[name]=NotKnown(name,FILE_SDK,fromName);
				}
			}
			else if(isBinaryFormatterGroup.Value) {
				//Lines 195-196: Windows Forms and WPF keep BinaryFormatter working at run time through .NET 8.
				bool isThrough8=versionFramework<=new Version(8,0);
				foreach(string name in new[] { "UseWindowsForms","UseWPF" }) {
					bool? isRequired=IsValueIfKnown(name,"true") & isThrough8;
					if(isRequired==true) {
						_dictProperties["_ProjectTypeRequiresBinaryFormatter"]="true";
					}
					else if(isRequired==null && IsValueIfKnown("_ProjectTypeRequiresBinaryFormatter","true")!=true) {
						_dictProperties["_ProjectTypeRequiresBinaryFormatter"]=NotKnown("_ProjectTypeRequiresBinaryFormatter",FILE_SDK,name);
					}
				}
				_dictProperties["_BinaryFormatterObsoleteAsError"]="true";
				//Line 200.
				bool? isUnsafeOff=versionFramework>=new Version(8,0) & !IsValueIfKnown("_ProjectTypeRequiresBinaryFormatter","true");
				if(isUnsafeOff==null) {
					_dictProperties["EnableUnsafeBinaryFormatterSerialization"]=NotKnown("EnableUnsafeBinaryFormatterSerialization",FILE_SDK,"_ProjectTypeRequiresBinaryFormatter");
				}
				else if(isUnsafeOff.Value) {
					_dictProperties["EnableUnsafeBinaryFormatterSerialization"]="false";
				}
			}
			//Microsoft.NET.Sdk.CSharp.targets lines 14-31.
			SetIfEmpty("AppDesignerFolder","Properties");
			bool? isImplicitDefine=!IsValueIfKnown("DisableImplicitConfigurationDefines","true");
			if(isImplicitDefine==null) {
				_dictProperties["ImplicitConfigurationDefine"]=NotKnown("ImplicitConfigurationDefine",FILE_CSHARP,"DisableImplicitConfigurationDefines");
				_dictProperties["DefineConstants"]=NotKnown("DefineConstants",FILE_CSHARP,"DisableImplicitConfigurationDefines");
			}
			else if(isImplicitDefine.Value) {
				string define=Escape(GetUnescaped("Configuration").ToUpperInvariant().Replace('-','_').Replace('.','_').Replace(' ','_'));
				_dictProperties["ImplicitConfigurationDefine"]=define;
				_dictProperties["DefineConstants"]=GetValue("DefineConstants")+";"+define;
			}
			//Line 45 (_BinaryFormatterObsoleteAsError, which the SDK sets only for .NET 7 and later; otherwise an environment variable of the name
			//stands).
			bool? isObsoleteAsError=IsValueIfKnown("_BinaryFormatterObsoleteAsError","true");
			if(isObsoleteAsError==null) {
				_dictProperties["WarningsAsErrors"]=NotKnown("WarningsAsErrors",FILE_CSHARP,"_BinaryFormatterObsoleteAsError");
			}
			else if(isObsoleteAsError.Value) {
				_dictProperties["WarningsAsErrors"]=GetValue("WarningsAsErrors")+";SYSLIB0011";
			}
			//Microsoft.NET.Sdk.Analyzers.targets lines 61-65 (Language is C# here), each when WarningLevel is still ''.
			if(GetValue("WarningLevel")=="") {
				const string FILE_ANALYZERS="Microsoft.NET.Sdk.Analyzers.targets";
				bool? isPreview=IsValueIfKnown("AnalysisLevel","preview");
				bool? isFramework=IsValueIfKnown("TargetFrameworkIdentifier",".NETFramework");
				if(isPreview==null) {
					_dictProperties["WarningLevel"]=NotKnown("WarningLevel",FILE_ANALYZERS,"AnalysisLevel");
				}
				else if(isPreview.Value) {
					_dictProperties["WarningLevel"]="9999";
				}
				else if(isFramework==null) {
					_dictProperties["WarningLevel"]=NotKnown("WarningLevel",FILE_ANALYZERS,"TargetFrameworkIdentifier");
				}
				else if(isFramework.Value) {
					_dictProperties["WarningLevel"]="4";
				}
				else if(IsValue("TargetFrameworkIdentifier",".NETCoreApp")) {
					_dictProperties["WarningLevel"]=versionText==null ? NotKnown("WarningLevel",FILE_ANALYZERS,"_TargetFrameworkVersionWithoutV")
						: Escape(versionText.Split('.')[0]);
				}
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

		///<summary>MSBuild's &lt;X Condition="'$(X)' == ''"&gt;value&lt;/X&gt;. An environment variable of that name that no file set is MSBuild's
		///value there instead, which this reader does not model: the name is then not known.</summary>
		private void SetIfEmpty(string name,string value) {
			string valueSet;
			if(_dictProperties.TryGetValue(name,out valueSet) && valueSet!="") {
				return;
			}
			_dictProperties[name]=IsEnvironmentVariable(name) ? Unresolved(name) : value;
		}

		///<summary>The property's value equals the text, as an MSBuild condition compares them: unescaped and ignoring case. For a name whose
		///value is always known at that point (a global property, a reserved one, one a file of MSBuild set just before); a condition on a name
		///that may be an environment variable or not known reads it with IsValueIfKnown.</summary>
		private bool IsValue(string name,string value) {
			return GetUnescaped(name).Equals(value,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The value as it stands at this point of the evaluation, unescaped, or "" when nothing set it (GetValue: a marker for an
		///environment variable no file set); no check.</summary>
		private string GetUnescaped(string name) {
			return Unescape(GetValue(name));
		}

		private bool IsPropertyTrue(string name) {
			return IsValue(name,"true");
		}

		private bool IsPropertyFalse(string name) {
			return IsValue(name,"false");
		}

		///<summary>The value of a property at this point of the evaluation, unescaped, for a condition of MSBuild's, NuGet's or the SDK's own files
		///that this reader evaluates in code (region "MSBuild's and the SDK's own files", ThrowIfImplicitPackageReferences, GetPackageVersion),
		///as MSBuild reads it there: a global property, which no file can change, else what a file set, else an environment variable of the
		///name, with which MSBuild starts every evaluation. This reader does not model environment variables, so for a name no file set that is
		///an environment variable of this process the value is not known: null, as for a value with an expression the reader cannot evaluate.
		///IsValue would compare the marker as if MSBuild had it.</summary>
		private string GetValueIfKnown(string name) {
			if(IsEnvironmentVariable(name)) {
				return null;
			}
			string value=GetValue(name);
			return IsUnresolved(value) ? null : Unescape(value);
		}

		///<summary>An MSBuild comparison '$(Name)' == 'value' of a condition this reader evaluates in code (ignoring case), as MSBuild reads the
		///name there: null when the value is not known (GetValueIfKnown), so the caller makes what depends on the condition not known (NotKnown),
		///or throws where the evaluation cannot go on without it. Combine them with C#'s &amp;, | and ! on bool?, which are three-valued as MSBuild's
		///and, or and not are on a value not known: an operand MSBuild does not read (after a false operand of an 'and', a true one of an 'or')
		///decides nothing either way.</summary>
		private bool? IsValueIfKnown(string name,string value) {
			string valueKnown=GetValueIfKnown(name);
			if(valueKnown==null) {
				return null;
			}
			return valueKnown.Equals(value,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>IsValueIfKnown for any of the values.</summary>
		private bool? IsValueIfKnown(string name,params string[] arrayValues) {
			string valueKnown=GetValueIfKnown(name);
			if(valueKnown==null) {
				return null;
			}
			return arrayValues.Any(x => valueKnown.Equals(x,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The value of a property MSBuild's or the SDK's files compute from a value this reader does not know: a marker, so GetProperty
		///throws for it, and so does any condition, include or import that uses it. fromName names the value not known (an environment variable
		///no file set, or a value with an expression the reader cannot evaluate, whose expression the message names).</summary>
		private string NotKnown(string name,string file,string fromName) {
			string value=GetValue(fromName);
			return Unresolved("the "+name+" of "+file+", from "+(IsUnresolved(value) ? GetUnresolvedExpression(value) : fromName));
		}

		///<summary>Throws NotSupportedException unless the property is '' and known: a property that names a file MSBuild's or the SDK's files
		///import at that point, or a switch that makes them import another file than the one this reader models (Custom.Before and Custom.After
		///imports, AlternateCommonProps, LanguageTargets, the target framework inference hooks and the like). MSBuild imports what a file or an
		///environment variable of the name gives, which this reader does not read.</summary>
		private void ThrowIfImportHook(string name,string where) {
			string value=GetValueIfKnown(name);
			if(value==null && IsEnvironmentVariable(name)) {
				throw new NotSupportedException(Describe()+": $("+name+") ("+where+") is an environment variable of this process, which MSBuild reads as a "
					+"property; ProjectFile does not model environment variables.");
			}
			ThrowIfUnresolved(GetValue(name),Describe()+": $("+name+") ("+where+")");
			if(value!="") {
				throw new NotSupportedException(Describe()+": $("+name+") ("+where+") names a file or a switch of an import that ProjectFile does not model.");
			}
		}

		///<summary>GetValueIfKnown where the evaluation or the query cannot go on without the value (an import, a refusal, the version the restore
		///takes): throws NotSupportedException when it is not known.</summary>
		private string GetKnownValue(string name) {
			ThrowIfEnvironmentVariable(name);
			string value=GetValue(name);
			ThrowIfUnresolved(value,Describe()+": $("+name+")");
			return Unescape(value);
		}

		///<summary>GetKnownValue is 'true', as an MSBuild condition compares it (ignoring case).</summary>
		private bool IsKnownTrue(string name) {
			return GetKnownValue(name).Equals("true",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>GetKnownValue is 'false', as an MSBuild condition compares it (ignoring case).</summary>
		private bool IsKnownFalse(string name) {
			return GetKnownValue(name).Equals("false",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Microsoft.NET.Sdk.DefaultItems.props and Microsoft.NET.Sdk.WindowsDesktop.props (SDK 10.0.114). Items are not markers, so a
		///switch these files read that is not known (an environment variable no file set, a value not known) throws (IsKnownTrue, IsKnownFalse),
		///where they read it.</summary>
		private void AddSdkDefaultItems() {
			//Microsoft.NET.Sdk.DefaultItems.props line 35.
			if(!IsKnownTrue("EnableDefaultItems")) {
				return;
			}
			string excludes=GetValue("DefaultItemExcludes")+";"+GetValue("DefaultExcludesInProjectFolder");
			string extension=GetValue("DefaultLanguageSourceExtension");
			//Lines 36-37, 42-45.
			if(IsKnownTrue("EnableDefaultCompileItems")) {
				AddItems("Compile","**/*"+extension,excludes,new Dictionary<string,string>(),null);
			}
			if(IsKnownTrue("EnableDefaultEmbeddedResourceItems")) {
				AddItems("EmbeddedResource","**/*.resx",excludes,new Dictionary<string,string>(),null);
			}
			if(IsKnownTrue("EnableDefaultNoneItems")) {
				AddItems("None","**/*",excludes,new Dictionary<string,string>(),null);
				RemoveItems("None","**/*"+extension,null);
				RemoveItems("None","**/*.resx",null);
			}
			//Microsoft.NET.Sdk.WindowsDesktop.props lines 17-39.
			if(!IsWindowsDesktopGlobbing()) {
				return;
			}
			if(!IsKnownFalse("EnableDefaultApplicationDefinition") && File.Exists(Path.Combine(Folder,"App.xaml"))
				&& Path.GetExtension(FullPath).Equals(".csproj",StringComparison.OrdinalIgnoreCase))
			{
				AddItems("ApplicationDefinition","App.xaml",null,new Dictionary<string,string>(),null);
			}
			if(!IsKnownFalse("EnableDefaultPageItems")) {
				AddItems("Page","**/*.xaml",excludes+";@(ApplicationDefinition)",new Dictionary<string,string>(),null);
			}
			if(!IsKnownFalse("EnableDefaultApplicationDefinition") && !IsKnownFalse("EnableDefaultPageItems")) {
				RemoveItems("None","**/*.xaml",null);
			}
		}

		///<summary>The item part of Microsoft.NET.Sdk.WindowsDesktop.targets (SDK 10.0.114, lines 73-101), when Microsoft.NET.Sdk.targets imported
		///it: a Page that is also a Resource, Content, the ApplicationDefinition or an explicit None item is not a Page. Its ItemGroup has line 56's
		///condition of its own (IsWindowsDesktopTargetsGlobbing), not _EnableWindowsDesktopGlobbing: a file or an environment variable that sets
		///the name for a project without UseWPF gets the Page items of the props, and none of these removals.</summary>
		private void ApplySdkTargetsItems() {
			if(!_isWindowsDesktopTargetsImported || !IsWindowsDesktopTargetsGlobbing()) {
				return;
			}
			if(!IsKnownFalse("EnableDefaultPageItems")) {
				RemoveItems("Page","@(Resource);@(Content)",null);
			}
			RemoveItems("Page","@(ApplicationDefinition)",null);
			if(!IsKnownFalse("EnableDefaultApplicationDefinition") && !IsKnownFalse("EnableDefaultPageItems")) {
				RemoveItems("Page","@(None)",null);
			}
		}

		///<summary>_EnableWindowsDesktopGlobbing as Microsoft.NET.Sdk.WindowsDesktop.props, which every SDK-style project imports, reads it (lines 3
		///and 17, in the passes after the properties: the name's last value). Only Microsoft.NET.Sdk.WindowsDesktop.targets sets it, which
		///Microsoft.NET.Sdk.targets imports for a WPF or Windows Forms project (ImportWindowsDesktopTargets), and only when its line 56's
		///condition holds (IsWindowsDesktopTargetsGlobbing); otherwise, the targets imported or not, the name keeps what a file or an environment
		///variable of the name gives: one not known throws.</summary>
		private bool IsWindowsDesktopGlobbing() {
			if(_isWindowsDesktopTargetsImported && IsWindowsDesktopTargetsGlobbing()) {
				return true;
			}
			return IsKnownTrue("_EnableWindowsDesktopGlobbing");
		}

		///<summary>The condition of Microsoft.NET.Sdk.WindowsDesktop.targets line 56 (which sets _EnableWindowsDesktopGlobbing true) and of its
		///ItemGroup at line 73: default items on, UseWPF, and a target framework of 3.0 or later (every .NET Framework 4.x and .NET target this
		///repository has). For a project that imports those targets only.</summary>
		private bool IsWindowsDesktopTargetsGlobbing() {
			Version versionFramework;
			return IsKnownTrue("EnableDefaultItems") && IsKnownTrue("UseWPF")
				&& Version.TryParse(GetKnownValue("_TargetFrameworkVersionWithoutV"),out versionFramework) && versionFramework>=new Version(3,0);
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
			//Metadata are not markers: a DefaultXamlRuntime not known throws.
			foreach(string[] arrayDefinition in new[] { new[] { "Generator","MSBuild:Compile" },new[] { "XamlRuntime",GetKnownValue("DefaultXamlRuntime") },
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
			//Microsoft.NET.Sdk.DefaultItems.targets line 82; one not known throws (metadata are not markers).
			if(IsKnownFalse("SetLinkMetadataAutomatically")) {
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
							projectItem.SetMetadata(pair.Key,Unescape(ExpandWellKnownMetadata(pair.Value,projectItem)));
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

		///<summary>Adds the items an Include names (already expanded, so escaped): files, wildcards, item lists, separated by ';', less those its
		///Exclude names. As MSBuild has it, the list is split and its wildcards are found in the escaped text (an escaped ';' or '*' is part of a
		///name), then each entry is unescaped: the item's Include, its file and its metadata are unescaped, as -getItem prints them.</summary>
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
						//A copy keeps what the wildcard gave the item: MSBuild keeps its RecursiveDir, so the SDK's automatic Link is the same.
						ProjectItem projectItem=new ProjectItem(itemType,projectItemSource.Include,projectItemSource.FullPath,projectItemSource.IsFromWildcard,
							fileContext?.Path);
						projectItem.RecursiveDir=projectItemSource.RecursiveDir;
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
					//A spec with a wildcard is unescaped whole before the files are matched (MSBuild's EngineFileUtilities).
					foreach(string[] arrayFile in ExpandWildcard(Unescape(spec),listPrunedFolders)) {
						ProjectItem projectItem=new ProjectItem(itemType,arrayFile[0],arrayFile[1],true,fileContext?.Path);
						projectItem.RecursiveDir=arrayFile[2];
						listNew.Add(projectItem);
					}
					continue;
				}
				string specUnescaped=Unescape(spec);
				listNew.Add(new ProjectItem(itemType,NormalizeSlashes(specUnescaped),TryGetFullPath(specUnescaped),false,fileContext?.Path));
			}
			foreach(ProjectItem projectItem in listNew) {
				if(listExcludes.Any(x => x(projectItem))) {
					continue;
				}
				ApplyItemDefinitions(projectItem);
				foreach(KeyValuePair<string,string> pair in dictMetadata) {
					projectItem.SetMetadata(pair.Key,Unescape(ExpandWellKnownMetadata(pair.Value,projectItem)));
				}
				_listItems.Add(projectItem);
			}
		}

		///<summary>%(Filename), %(Extension), %(RecursiveDir), %(Identity), %(FullPath) and %(RelativeDir) of the item itself in a metadata value,
		///as in Link="%(RecursiveDir)%(Filename)%(Extension)" of the third-party source builds, escaped like the value they go into. Other metadata
		///references stay as written.</summary>
		private static string ExpandWellKnownMetadata(string value,ProjectItem projectItem) {
			if(value==null || !value.Contains("%(")) {
				return value;
			}
			return Regex.Replace(value,@"%\(\s*(\w+)\s*\)",match => Escape(GetWellKnownMetadata(match,projectItem)));
		}

		///<summary>The value of one %(Name) of the item itself, unescaped; another name stays as written.</summary>
		private static string GetWellKnownMetadata(Match match,ProjectItem projectItem) {
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
					return projectItem.FullPath??"";
				case "relativedir": {
						int index=projectItem.Include.LastIndexOf('\\');
						return index<0 ? "" : projectItem.Include.Substring(0,index+1);
					}
				default:
					return match.Value;
			}
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
				//Wildcards found in the escaped text, then the entry unescaped, as AddItems has it.
				if(spec.IndexOfAny(new[] { '*','?' })>=0) {
					Regex regex=GetWildcardRegex(Unescape(spec));
					listMatchers.Add(x => x.FullPath!=null && regex.IsMatch(x.FullPath));
					continue;
				}
				string fullPath=TryGetFullPath(Unescape(spec));
				string include=NormalizeSlashes(Unescape(spec));
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
				string normalized=NormalizeSlashes(Unescape(spec));
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
		///<summary>The evaluated value of a property, as MSBuild has it at the end of the evaluation (as MSBuild -getProperty gives it, so
		///without what targets add while building): set by the project file or a file it imports from the repository, or computed by this reader
		///where MSBuild's or the SDK's own files compute it (region "MSBuild's and the SDK's own files": for an SDK-style C# project also
		///DefineConstants, NoWarn and the other compiler defaults), or "" when nothing sets it. Throws for a property that MSBuild, its toolset,
		///Visual Studio's components or the SDK compute and this reader does not (the not-modelled lists: _arrayNotModelledProperties,
		///_arrayNotModelledToolsetProperties, _arrayNotModelledSdkProperties, _arrayWebApplicationProperties, and _arraySdkLanguageProperties for
		///an SDK-style project in another language), for an environment variable of this process that no file set (MSBuild reads environment
		///variables as properties; this reader does not model them), and for a value that holds an expression this reader cannot evaluate.
		///</summary>
		public string GetProperty(string name) {
			if(!_dictProperties.ContainsKey(name) && _hashSetNotModelled.Contains(name)) {
				throw new NotSupportedException(Describe()+": $("+name+") is computed by MSBuild, the solution build or the .NET SDK and not modelled by ProjectFile.");
			}
			ThrowIfEnvironmentVariable(name);
			string value=GetValue(name);
			ThrowIfUnresolved(value,Describe()+": $("+name+")");
			//Unescaped, as -getProperty prints it: ApplicationVersion 1.0.0.%2a is 1.0.0.*.
			return Unescape(value);
		}

		///<summary>Throws NotSupportedException when no file this reader read set the name and it is an environment variable of this process
		///(IsEnvironmentVariable): MSBuild would read the variable's value there, which this reader does not model.</summary>
		private void ThrowIfEnvironmentVariable(string name) {
			if(IsEnvironmentVariable(name)) {
				throw new NotSupportedException(Describe()+": $("+name+") is an environment variable of this process, which MSBuild reads as a property; "
					+"ProjectFile does not model environment variables.");
			}
		}

		///<summary>True when the reader knows the property's value (GetProperty does not throw for it).</summary>
		public bool IsPropertyKnown(string name) {
			if(!_dictProperties.ContainsKey(name)) {
				return !_hashSetNotModelled.Contains(name) && !IsEnvironmentVariable(name);
			}
			return !IsUnresolved(GetValue(name));
		}

		///<summary>True when no file this reader read set the name and it is an environment variable of this process: MSBuild gives every
		///environment variable whose name is a valid property name to the evaluation as a property ($(TEMP), $(UserProfile), which NuGet's
		///obj\*.nuget.g.props use), and a file may set it over; this reader does not model environment variables.</summary>
		private bool IsEnvironmentVariable(string name) {
			return !_dictProperties.ContainsKey(name) && Regex.IsMatch(name,@"^[A-Za-z_][\w-]*$") && Environment.GetEnvironmentVariable(name)!=null;
		}

		///<summary>The value as it stands at this point of the evaluation, escaped as MSBuild keeps it, or "" when nothing set it; no check. It is
		///$(Name) as MSBuild's and the SDK's own files read it where this reader computes them in code, so for a name no file set that is an
		///environment variable of this process (IsEnvironmentVariable), which MSBuild reads there and this reader does not model, it is the
		///marker of a value not known, as in an expression (GetValueForExpression): a value made from it, a path or a list, is not known either,
		///and a test of it for '' is false, as MSBuild's is (package READER-ENV). A comparison with a value reads the name with IsValueIfKnown.
		///</summary>
		private string GetValue(string name) {
			string value;
			if(_dictProperties.TryGetValue(name,out value)) {
				return value;
			}
			return IsEnvironmentVariable(name) ? Unresolved(name) : "";
		}

		///<summary>The value of $(Name) in an expression: as GetValue, but a property MSBuild, the solution build or the SDK compute and this reader
		///does not, or an environment variable no file set, becomes a marker, which makes the condition, include or import that uses it, directly
		///or through another property, throw.</summary>
		private string GetValueForExpression(string name) {
			if(!_dictProperties.ContainsKey(name) && (_hashSetNotModelled.Contains(name) || IsEnvironmentVariable(name))) {
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
				//MSBuild hands a property function its arguments unescaped and escapes what it returns.
				string result=EvaluateStaticFunction(matchStatic.Groups[1].Value,matchStatic.Groups[2].Value,listArguments,fileContext);
				return result==null ? Unresolved(expression) : Escape(result);
			}
			//$(Name.Method('argument')), a string method on a property's value
			Match matchMethod=Regex.Match(expression,@"^([A-Za-z_][\w-]*)\.(\w+)\((.*)\)$",RegexOptions.Singleline);
			if(matchMethod.Success) {
				string value=GetPropertyInFile(matchMethod.Groups[1].Value,fileContext);
				List<string> listArguments=SplitArguments(matchMethod.Groups[3].Value).Select(x => ExpandArgument(x,fileContext)).ToList();
				if(IsUnresolved(value) || listArguments.Any(x => IsUnresolved(x))) {
					return Unresolved(expression);
				}
				//The method works on the value unescaped (a %2a is a '*' to EndsWith), and its result is escaped again.
				string result=EvaluateStringMethod(Unescape(value),matchMethod.Groups[2].Value,listArguments);
				return result==null ? Unresolved(expression) : Escape(result);
			}
			return Unresolved(expression);
		}

		private string GetPropertyInFile(string name,FileContext fileContext) {
			switch(name.ToLowerInvariant()) {
				//Escaped, as MSBuild keeps its reserved properties.
				case "msbuildthisfile":
					return Escape(Path.GetFileName(fileContext.Path));
				case "msbuildthisfiledirectory":
					return Escape(fileContext.Directory);
				case "msbuildthisfilefullpath":
					return Escape(fileContext.Path);
				case "msbuildthisfilename":
					return Escape(Path.GetFileNameWithoutExtension(fileContext.Path));
				case "msbuildthisfileextension":
					return Escape(Path.GetExtension(fileContext.Path));
				case "msbuildthisfiledirectorynoroot":
					return Escape(fileContext.Directory.Substring(Path.GetPathRoot(fileContext.Directory).Length));
				default:
					return GetValueForExpression(name);
			}
		}

		private string ExpandArgument(string argument,FileContext fileContext) {
			string trimmed=argument.Trim();
			if(trimmed.Length>=2 && trimmed[0]=='\'' && trimmed[trimmed.Length-1]=='\'') {
				trimmed=trimmed.Substring(1,trimmed.Length-2);
			}
			return Unescape(Expand(trimmed,fileContext));
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
				//A bool result as MSBuild writes it: Boolean.ToString(), "True" or "False".
				case "endswith":
					return listArguments.Count==1 ? value.EndsWith(listArguments[0],StringComparison.Ordinal).ToString() : null;
				case "startswith":
					return listArguments.Count==1 ? value.StartsWith(listArguments[0],StringComparison.Ordinal).ToString() : null;
				case "contains":
					return listArguments.Count==1 ? value.Contains(listArguments[0]).ToString() : null;
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

		///<summary>MSBuild's EscapingUtilities.UnescapeAll: every %XX with two hexadecimal digits becomes its character (%2a a '*', %3b a ';',
		///%28 a '('). MSBuild keeps property values and metadata escaped while it evaluates and unescapes them where it uses them: a condition's
		///operands, an Exists() path, an Import path, a file an item names, a property function's arguments, and what -getProperty and -getItem
		///print.</summary>
		private static string Unescape(string text) {
			if(text==null || text.IndexOf('%')<0) {
				return text;
			}
			StringBuilder stringBuilder=new StringBuilder(text.Length);
			for(int i=0;i<text.Length;i++) {
				if(text[i]=='%' && i+2<text.Length && Uri.IsHexDigit(text[i+1]) && Uri.IsHexDigit(text[i+2])) {
					stringBuilder.Append((char)Convert.ToInt32(text.Substring(i+1,2),16));
					i+=2;
					continue;
				}
				stringBuilder.Append(text[i]);
			}
			return stringBuilder.ToString();
		}

		///<summary>MSBuild's EscapingUtilities.Escape: % * ? @ $ ( ) ; and ' as %XX, as MSBuild stores the paths of its reserved properties and the
		///result of a property function, so a ';' or a '*' in them is no list separator or wildcard where the value is used. A value that holds a
		///marker of something this reader cannot evaluate stays as it is.</summary>
		private static string Escape(string text) {
			if(string.IsNullOrEmpty(text) || IsUnresolved(text) || text.IndexOfAny(_arrayEscapedChars)<0) {
				return text;
			}
			StringBuilder stringBuilder=new StringBuilder(text.Length+8);
			foreach(char c in text) {
				if(Array.IndexOf(_arrayEscapedChars,c)>=0) {
					stringBuilder.Append('%').Append(((int)c).ToString("x2"));
					continue;
				}
				stringBuilder.Append(c);
			}
			return stringBuilder.ToString();
		}

		private static void ThrowIfUnresolved(string value,string where) {
			if(!IsUnresolved(value)) {
				return;
			}
			throw new NotSupportedException(where+": ProjectFile cannot evaluate $("+GetUnresolvedExpression(value)+").");
		}

		///<summary>The expression of the first marker in the value, for a message.</summary>
		private static string GetUnresolvedExpression(string value) {
			int index=value.IndexOf(UNRESOLVED_START,StringComparison.Ordinal);
			int indexEnd=value.IndexOf(UNRESOLVED_END,index,StringComparison.Ordinal);
			return value.Substring(index+UNRESOLVED_START.Length,(indexEnd<0 ? value.Length : indexEnd)-index-UNRESOLVED_START.Length);
		}

		///<summary>The value for a message: each marker as $(expression), so a marker made from a value with a marker reads as text.</summary>
		private static string TextOf(string value) {
			if(!IsUnresolved(value)) {
				return value;
			}
			return value.Replace(UNRESOLVED_START,"$(").Replace(UNRESOLVED_END,")");
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

		///<summary>The condition of a PropertyGroup, an Import or an ImportGroup: Exists('relative') is relative to the folder of the file the
		///condition is in (MSBuild's Evaluator.GetCurrentDirectoryForConditionEvaluation, kept from MSBuild 2.0 for those three elements).
		///</summary>
		private bool IsConditionTrueInFile(XElement xElement,FileContext fileContext) {
			return IsConditionTrue(xElement,fileContext,fileContext.Directory);
		}

		///<summary>The condition of any other element (a property, an ItemGroup, an item, a piece of metadata): Exists('relative') is relative to
		///the project's folder, even in an imported file, as MSBuild has it.</summary>
		private bool IsConditionTrue(XElement xElement,FileContext fileContext) {
			return IsConditionTrue(xElement,fileContext,Folder);
		}

		private bool IsConditionTrue(XElement xElement,FileContext fileContext,string folderForExists) {
			string condition=(string)xElement.Attribute("Condition");
			if(condition==null || condition.Trim()=="") {
				return true;
			}
			return new ConditionParser(condition,x => Expand(x,fileContext),x => Path.Combine(folderForExists,x),fileContext.Path).Parse();
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
				//MSBuild compares, tests and checks for files with the operands unescaped: '$(V)' == '1.0.0.*' and == '1.0.0.%2a' alike.
				return Unescape(value);
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
			ThrowIfImplicitPackageReferences(itemType);
			return _listItems.Where(x => x.ItemType.Equals(itemType,StringComparison.OrdinalIgnoreCase)).ToList();
		}

		///<summary>The PackageReference items and metadata that the SDK's own files give an SDK-style project and this reader does not; no project of
		///the repository has any of them, so every query that can return PackageReference items (GetItems and GetItem for the type, the queries
		///built on them such as GetPackageReference and GetPackageVersion, GetAllItems, GetItemsMentioning) throws for them instead of leaving
		///them out or returning items without their metadata. itemType null: a query of every item type.
		///- Microsoft.NET.Sdk.DefaultItems.props gives a .NET Standard project before 2.1 the PackageReference NETStandard.Library and a .NET Core
		///  project before 3.0 Microsoft.NETCore.App, unless DisableImplicitFrameworkReferences is true.
		///- Microsoft.NET.Sdk.DefaultItems.Shared.targets (imported by Microsoft.NET.Sdk.DefaultItems.targets, so for SDK-style projects only) gives
		///  every PackageReference AllowExplicitVersion true when DisableImplicitFrameworkReferences is true; a PackageReference of
		///  Microsoft.AspNetCore.App or Microsoft.AspNetCore.All without a Version PrivateAssets all and Publish true (with central package
		///  management a PackageReference never has a Version, NU1008, so it always gets them); and one of Microsoft.NETCore.App the Version
		///  $(RuntimeFrameworkVersion) and AllowExplicitVersion true when RuntimeFrameworkVersion is set. Its Update items match the Include ignoring
		///  case, as MSBuild matches item specs.
		///Each property is read as those files read it (GetKnownValue): one that is not known here (an environment variable no file set, a value
		///with an expression the reader cannot evaluate, the framework of a TargetFramework it does not know) throws, as whether the SDK adds the
		///items is then not known.</summary>
		private void ThrowIfImplicitPackageReferences(string itemType) {
			if(!IsSdkStyle || (itemType!=null && !itemType.Equals("PackageReference",StringComparison.OrdinalIgnoreCase))) {
				return;
			}
			if(IsKnownTrue("DisableImplicitFrameworkReferences")) {
				throw new NotSupportedException(Describe()+": the PackageReference metadata the SDK gives with DisableImplicitFrameworkReferences are not modelled "
					+"by ProjectFile.");
			}
			string frameworkIdentifier=GetKnownValue("TargetFrameworkIdentifier");
			bool isNetStandard=frameworkIdentifier.Equals(".NETStandard",StringComparison.OrdinalIgnoreCase);
			bool isNetCoreApp=frameworkIdentifier.Equals(".NETCoreApp",StringComparison.OrdinalIgnoreCase);
			if(isNetStandard || isNetCoreApp) {
				Version versionFramework;
				bool hasVersion=Version.TryParse(GetKnownValue("_TargetFrameworkVersionWithoutV"),out versionFramework);
				if((isNetStandard && (!hasVersion || versionFramework<new Version(2,1))) || (isNetCoreApp && hasVersion && versionFramework<new Version(3,0))) {
					throw new NotSupportedException(Describe()+": the SDK's implicit PackageReference of "+GetUnescaped("TargetFramework")+" is not modelled by ProjectFile.");
				}
			}
			foreach(ProjectItem projectItem in _listItems.Where(x => x.ItemType.Equals("PackageReference",StringComparison.OrdinalIgnoreCase))) {
				if((projectItem.Include.Equals("Microsoft.AspNetCore.App",StringComparison.OrdinalIgnoreCase)
					|| projectItem.Include.Equals("Microsoft.AspNetCore.All",StringComparison.OrdinalIgnoreCase))
					&& projectItem.GetMetadata("Version")=="")
				{
					throw new NotSupportedException(Describe()+": the PrivateAssets and Publish metadata the SDK gives the PackageReference of "+projectItem.Include
						+" without a Version (Microsoft.NET.Sdk.DefaultItems.Shared.targets) are not modelled by ProjectFile.");
				}
				if(projectItem.Include.Equals("Microsoft.NETCore.App",StringComparison.OrdinalIgnoreCase) && GetKnownValue("RuntimeFrameworkVersion")!="") {
					throw new NotSupportedException(Describe()+": the Version and AllowExplicitVersion metadata the SDK gives the PackageReference of "+projectItem.Include
						+" with RuntimeFrameworkVersion set (Microsoft.NET.Sdk.DefaultItems.Shared.targets) are not modelled by ProjectFile.");
				}
			}
		}

		///<summary>Every item, of every type. Throws as GetItems("PackageReference") does for the PackageReference items the SDK gives itself
		///(ThrowIfImplicitPackageReferences).</summary>
		public List<ProjectItem> GetAllItems() {
			ThrowIfImplicitPackageReferences(null);
			return _listItems.ToList();
		}

		///<summary>The first item of the type for the path (relative to the project folder, '\' or '/', or full) or the plain text (a Reference's
		///Include), or null.</summary>
		public ProjectItem GetItem(string itemType,string spec) {
			ThrowIfImplicitPackageReferences(itemType);
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

		///<summary>The project's own PackageReference item for a package id (NuGet's ids ignore case), or null: one the project file or a file it
		///imports names, not one the project gets through the projects it references or a package's dependencies (the restore, and the lock file,
		///say those).</summary>
		public ProjectItem GetPackageReference(string packageId) {
			return GetItems("PackageReference").LastOrDefault(x => x.Include.Equals(packageId,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The version the project's PackageReference of the package restores with, as NuGet reads it
		///(https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management), or null when the project has no PackageReference of
		///it. With central package management (_CentralPackageVersionsEnabled: ManagePackageVersionsCentrally true and Directory.Packages.props
		///imported), the PackageReference's VersionOverride, else the Version of the package's PackageVersion item; a Version on the
		///PackageReference itself (NU1008), a VersionOverride with CentralPackageVersionOverrideEnabled false (NU1013) and no PackageVersion (NU1010)
		///fail the restore, so they throw. Two or more PackageVersion items of one package throw too, which is stricter than NuGet: NuGet.targets'
		///CollectCentralPackageVersions reports them as NU1506, a warning (an error only through TreatWarningsAsErrors or WarningsAsErrors, which the
		///repository does not set for it), keeps one of them and the restore goes on. Which one it keeps is that task's choice, which this reader
		///does not model, and Directory.Packages.props names each package once, so a second item is a mistake for a test to report, not a version
		///for the reader to guess. Without central package management, the PackageReference's own Version. _CentralPackageVersionsEnabled and
		///CentralPackageVersionOverrideEnabled (read only for a VersionOverride) are read as NuGet's restore gets them from MSBuild: one that is not
		///known here (an environment variable no file set, or a switch that NuGet.targets set from one) throws NotSupportedException.</summary>
		public string GetPackageVersion(string packageId) {
			ProjectItem projectItemReference=GetPackageReference(packageId);
			if(projectItemReference==null) {
				return null;
			}
			if(!IsKnownTrue("_CentralPackageVersionsEnabled")) {
				return projectItemReference.GetMetadata("Version");
			}
			if(projectItemReference.GetMetadata("Version")!="") {
				throw new InvalidOperationException(Describe()+": the PackageReference of "+packageId+" names a version, which central package management "
					+"does not allow (NU1008).");
			}
			if(projectItemReference.GetMetadata("VersionOverride")!="") {
				if(IsKnownFalse("CentralPackageVersionOverrideEnabled")) {
					throw new InvalidOperationException(Describe()+": VersionOverride of "+packageId+" with CentralPackageVersionOverrideEnabled false (NU1013).");
				}
				return projectItemReference.GetMetadata("VersionOverride");
			}
			List<ProjectItem> listVersions=GetItems("PackageVersion").Where(x => x.Include.Equals(packageId,StringComparison.OrdinalIgnoreCase)).ToList();
			if(listVersions.Count==0) {
				throw new InvalidOperationException(Describe()+": no PackageVersion item for "+packageId+" (NU1010).");
			}
			if(listVersions.Count>1) {
				throw new InvalidOperationException(Describe()+": "+listVersions.Count+" PackageVersion items for "+packageId+" (NuGet's warning NU1506; "
					+"ProjectFile refuses them, as it does not model which one NuGet keeps).");
			}
			return listVersions[0].GetMetadata("Version");
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
		///gone from the project in either format. No item types means every type. Throws as GetItems("PackageReference") does when the types
		///include PackageReference (ThrowIfImplicitPackageReferences).</summary>
		public List<ProjectItem> GetItemsMentioning(string text,params string[] arrayItemTypes) {
			if(arrayItemTypes.Length==0) {
				ThrowIfImplicitPackageReferences(null);
			}
			foreach(string itemType in arrayItemTypes) {
				ThrowIfImplicitPackageReferences(itemType);
			}
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
			public readonly long Length;
			private readonly Dictionary<string,string> _dictGuids=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			private readonly Dictionary<string,string[]> _dictMappings=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);

			public SolutionFile(string pathSln,DateTime dateTimeWriteUtc,long length) {
				DateTimeWriteUtc=dateTimeWriteUtc;
				Length=length;
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

			///<summary>True when the solution lists the project (by its full path).</summary>
			public bool Lists(string pathProject) {
				return _dictGuids.ContainsKey(pathProject);
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
