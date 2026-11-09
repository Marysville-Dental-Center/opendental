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
	///<summary>A project file evaluated as MSBuild evaluates its items, for one configuration and platform (Debug|x86 by default, the build lane's).
	///What it models: the properties of the project file and of the files it imports from the repository (Directory.Build.props and
	///Directory.Build.targets above the project, and relative imports such as third-party\build\SourceBuild.props), with their conditions; the
	///items of those files in MSBuild's order, with Include (a file, a list, a wildcard, an item list), Exclude, Remove and Update, and their
	///metadata from child elements and attributes alike; for an SDK-style project the .NET SDK's default items before the project's own (Compile
	///**\*.cs, EmbeddedResource **\*.resx and None **\* without bin, obj, hidden folders and the DefaultItemExcludes; with UseWPF, Page **\*.xaml
	///and App.xaml as ApplicationDefinition), switched off by EnableDefaultItems, EnableDefaultCompileItems and the other switches, exactly as
	///Microsoft.NET.Sdk.DefaultItems.props/.targets and Microsoft.NET.Sdk.WindowsDesktop.props/.targets of the pinned SDK 10.0.114 define them.
	///What it does not model: the files of MSBuild and the SDK themselves (so a property the SDK computes, such as its default AssemblyName, is
	///not known here), targets (items created while building), ItemDefinitionGroup defaults, and Choose. An expression it cannot evaluate where
	///it decides an item (an unknown property function in an Include or a condition) throws, so a check never passes on a guess.</summary>
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
		private const string STEP_SDK_TARGETS_ITEMS="SdkTargetsItems";
		private static readonly string[] _arrayReservedAttributes=new[] { "Include","Exclude","Remove","Update","Condition","KeepMetadata",
			"RemoveMetadata","KeepDuplicates","MatchOnMetadata","MatchOnMetadataOptions","Label" };

		private ProjectFile() {
		}

		#region Loading
		///<summary>The project file evaluated for Debug|x86, the configuration the build lane and the tests build.</summary>
		public static ProjectFile Load(string path) {
			return Load(path,"Debug","x86");
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
			if(IsSdkStyle) {
				_dictProperties["UsingMicrosoftNETSdk"]="true";
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
				ImportDirectoryBuildFile("Directory.Build.props");
				return;
			}
			if(new[] { "Microsoft.CSharp.targets","Microsoft.Common.targets","Microsoft.VisualBasic.targets" }.Contains(fileName,StringComparer.OrdinalIgnoreCase)) {
				ImportDirectoryBuildFile("Directory.Build.targets");
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
			if(GetProperty(switchName).Equals("false",StringComparison.OrdinalIgnoreCase)) {
				return;
			}
			string path=FindFileAbove(fileName,Folder);
			if(path!=null) {
				ReadImportedFile(path);
			}
		}

		///<summary>What Sdk.props of Microsoft.NET.Sdk does for items: Microsoft.Common.props (Directory.Build.props), then
		///Microsoft.NET.Sdk.DefaultItems.props and Microsoft.NET.Sdk.WindowsDesktop.props, whose default items come before the project's own.</summary>
		private void ImportSdkProps() {
			_dictProperties["UsingMicrosoftNETSdk"]="true";
			ImportDirectoryBuildFile("Directory.Build.props");
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_DEFAULT_ITEMS });
		}

		///<summary>What Sdk.targets does for items: the property defaults of Microsoft.NET.Sdk.DefaultItems.targets and
		///Microsoft.NET.Sdk.WindowsDesktop.targets, Microsoft.Common.targets (Directory.Build.targets), and the Page removals of the latter.</summary>
		private void ImportSdkTargets() {
			SetIfEmpty("EnableDefaultItems","true");
			SetIfEmpty("EnableDefaultCompileItems","true");
			SetIfEmpty("EnableDefaultEmbeddedResourceItems","true");
			SetIfEmpty("EnableDefaultNoneItems","true");
			SetIfEmpty("EnableDefaultPageItems","true");
			SetIfEmpty("EnableDefaultApplicationDefinition","true");
			SetIfEmpty("DefaultLanguageSourceExtension",".cs");
			SetIfEmpty("BaseOutputPath",@"bin\");
			SetIfEmpty("BaseIntermediateOutputPath",@"obj\");
			_dictProperties["DefaultItemExcludes"]=GetProperty("DefaultItemExcludes")+";"+GetProperty("BaseOutputPath")+"/**;"
				+GetProperty("BaseIntermediateOutputPath")+"/**;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";
			_dictProperties["DefaultExcludesInProjectFolder"]=GetProperty("DefaultExcludesInProjectFolder")+";"
				+GetProperty("DefaultItemExcludesInProjectFolder")+";**/.*/**";
			ImportDirectoryBuildFile("Directory.Build.targets");
			_listItemSteps.Add(new ItemStep { Builtin=STEP_SDK_TARGETS_ITEMS });
		}

		private void SetIfEmpty(string name,string value) {
			if(GetProperty(name)=="") {
				_dictProperties[name]=value;
			}
		}

		private bool IsPropertyTrue(string name) {
			return GetProperty(name).Equals("true",StringComparison.OrdinalIgnoreCase);
		}

		private bool IsPropertyFalse(string name) {
			return GetProperty(name).Equals("false",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Microsoft.NET.Sdk.DefaultItems.props and Microsoft.NET.Sdk.WindowsDesktop.props (SDK 10.0.114).</summary>
		private void AddSdkDefaultItems() {
			if(!IsPropertyTrue("EnableDefaultItems")) {
				return;
			}
			string excludes=GetProperty("DefaultItemExcludes")+";"+GetProperty("DefaultExcludesInProjectFolder");
			string extension=GetProperty("DefaultLanguageSourceExtension");
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

		///<summary>_EnableWindowsDesktopGlobbing: default items on, UseWPF, and a target framework of 3.0 or later (every .NET Framework 4.x and
		///.NET target this repository has).</summary>
		private bool IsWindowsDesktopGlobbing() {
			return IsPropertyTrue("EnableDefaultItems") && IsPropertyTrue("UseWPF");
		}
		#endregion Loading

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
					foreach(ProjectItem projectItem in _listItems.Where(x => x.ItemType==itemType && listMatchers.Any(y => y(x)))) {
						foreach(KeyValuePair<string,string> pair in dictMetadata) {
							projectItem.SetMetadata(pair.Key,ExpandWellKnownMetadata(pair.Value,projectItem));
						}
					}
				}
			}
		}

		///<summary>Metadata from child elements (with their conditions) and from attributes, the SDK-style short form.</summary>
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
		///<summary>The evaluated value of a property set by the project file or a file it imports from the repository, or "". Properties MSBuild
		///or the SDK compute in their own files are not known, apart from the few this reader sets (the MSBuildProject* ones, ProjectDir, and the
		///SDK's default item switches for an SDK-style project).</summary>
		public string GetProperty(string name) {
			string value;
			return _dictProperties.TryGetValue(name,out value) ? value : "";
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
				default:
					return GetProperty(name);
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
			string folder=Path.GetFullPath(Path.Combine(Folder,relativeFolder));
			List<string> listFiles=Directory.Exists(folder)
				? Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(x => x.EndsWith(".cs",StringComparison.OrdinalIgnoreCase))
					.Where(x => !Regex.IsMatch(x.Substring(folder.Length),@"\\(bin|obj)\\",RegexOptions.IgnoreCase)).ToList()
				: new List<string>();
			if(listFiles.Count==0) {
				throw new InvalidOperationException(Name+": no .cs files under "+relativeFolder);
			}
			return listFiles.Where(x => !Compiles(x)).Select(x => x.Substring(Folder.Length+1)).ToList();
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
