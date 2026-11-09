// Shared helpers for the library binding tests: repository paths, assembly metadata read with Mono.Cecil (no file is loaded or locked), the
// binding redirects of a config file, the .NET Framework's global assembly cache, and the projects of OpenDental.Local.slnf.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using ProjectFileTests;

namespace LibraryBindingTests {
	internal static class RepoPaths {
		private static string _root;

		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE (not found by the solution's name,
		///which a rename may change; package REN-PROOF).</summary>
		public static string Root {
			get {
				if(_root!=null) {
					return _root;
				}
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
						_root=directoryInfo.FullName.TrimEnd('\\');
						return _root;
					}
					directoryInfo=directoryInfo.Parent;
				}
				throw new InvalidOperationException("LICENSE and NOTICE not found above "+AppDomain.CurrentDomain.BaseDirectory);
			}
		}

		public static string RequiredDlls {
			get { return Path.Combine(Root,"Required dlls"); }
		}

		public static string Combine(params string[] arrayParts) {
			return Path.Combine(Root,Path.Combine(arrayParts));
		}

		///<summary>The path relative to the repository root, for messages.</summary>
		public static string Relative(string path) {
			string full=Path.GetFullPath(path);
			if(full.StartsWith(Root+"\\",StringComparison.OrdinalIgnoreCase)) {
				return full.Substring(Root.Length+1);
			}
			return full;
		}

		public static string ReadText(params string[] arrayParts) {
			return File.ReadAllText(Combine(arrayParts));
		}
	}

	///<summary>A reference from one assembly to another, as its metadata names it.</summary>
	internal class AssemblyReference {
		public string Name;
		public Version Version;
		///<summary>The public key token in lower-case hex; "" for an assembly without a strong name.</summary>
		public string Token;
		///<summary>A Windows Runtime metadata reference (Windows.winmd), which Windows itself resolves at run time.</summary>
		public bool IsWindowsRuntime;
	}

	///<summary>What a file's metadata says about it. IsManaged is false for a native file, which has no assembly identity.</summary>
	internal class AssemblyFacts {
		public string Path;
		public string FileName;
		public bool IsManaged;
		public string Name;
		public Version Version;
		public string Token;
		public List<AssemblyReference> ListReferences=new List<AssemblyReference>();
	}

	internal static class MetadataReader {
		///<summary>Keyed by path, length and last write time, so the many identical copies in the output folders are read once per run.</summary>
		private static readonly Dictionary<string,AssemblyFacts> _dictCache=new Dictionary<string,AssemblyFacts>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string,string> _dictHashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

		private static string Key(string path) {
			FileInfo fileInfo=new FileInfo(path);
			return fileInfo.FullName+"|"+fileInfo.Length+"|"+fileInfo.LastWriteTimeUtc.Ticks;
		}

		public static string FormatToken(byte[] arrayToken) {
			if(arrayToken==null || arrayToken.Length==0) {
				return "";
			}
			return string.Concat(arrayToken.Select(x => x.ToString("x2")));
		}

		///<summary>The file's assembly identity and references, read from a copy of its bytes so the file stays unlocked. Nothing is loaded.</summary>
		public static AssemblyFacts Read(string path) {
			string key=Key(path);
			if(_dictCache.TryGetValue(key,out AssemblyFacts assemblyFactsCached)) {
				return assemblyFactsCached;
			}
			AssemblyFacts assemblyFacts=new AssemblyFacts();
			assemblyFacts.Path=path;
			assemblyFacts.FileName=System.IO.Path.GetFileName(path);
			try {
				using(MemoryStream memoryStream=new MemoryStream(File.ReadAllBytes(path)))
				using(ModuleDefinition moduleDefinition=ModuleDefinition.ReadModule(memoryStream)) {
					if(moduleDefinition.Assembly!=null) {
						assemblyFacts.IsManaged=true;
						assemblyFacts.Name=moduleDefinition.Assembly.Name.Name;
						assemblyFacts.Version=moduleDefinition.Assembly.Name.Version;
						assemblyFacts.Token=FormatToken(moduleDefinition.Assembly.Name.PublicKeyToken);
						foreach(AssemblyNameReference assemblyNameReference in moduleDefinition.AssemblyReferences) {
							assemblyFacts.ListReferences.Add(new AssemblyReference {
								Name=assemblyNameReference.Name,
								Version=assemblyNameReference.Version,
								Token=FormatToken(assemblyNameReference.PublicKeyToken),
								IsWindowsRuntime=assemblyNameReference.IsWindowsRuntime,
							});
						}
					}
				}
			}
			catch(BadImageFormatException) {
				//A native file: no metadata.
			}
			_dictCache[key]=assemblyFacts;
			return assemblyFacts;
		}

		///<summary>The managed .dll and .exe files directly in the folder (not its sub folders), by assembly name, ignoring case.</summary>
		public static Dictionary<string,AssemblyFacts> ReadFolder(string folder) {
			Dictionary<string,AssemblyFacts> dictAssemblies=new Dictionary<string,AssemblyFacts>(StringComparer.OrdinalIgnoreCase);
			foreach(string path in Directory.GetFiles(folder).Where(x => IsBinary(x)).OrderBy(x => x,StringComparer.OrdinalIgnoreCase)) {
				AssemblyFacts assemblyFacts=Read(path);
				if(assemblyFacts.IsManaged && !dictAssemblies.ContainsKey(assemblyFacts.Name)) {
					dictAssemblies.Add(assemblyFacts.Name,assemblyFacts);
				}
			}
			return dictAssemblies;
		}

		public static bool IsBinary(string path) {
			string extension=System.IO.Path.GetExtension(path);
			return extension.Equals(".dll",StringComparison.OrdinalIgnoreCase) || extension.Equals(".exe",StringComparison.OrdinalIgnoreCase);
		}

		public static string Sha256(string path) {
			string key=Key(path);
			if(_dictHashes.TryGetValue(key,out string hash)) {
				return hash;
			}
			using(SHA256 sha256=SHA256.Create()) {
				hash=string.Concat(sha256.ComputeHash(File.ReadAllBytes(path)).Select(x => x.ToString("x2")));
			}
			_dictHashes[key]=hash;
			return hash;
		}
	}

	///<summary>The files of "Required dlls": every file by name, and the managed ones by assembly name.</summary>
	internal static class RequiredDlls {
		private static Dictionary<string,string> _dictFiles;
		private static Dictionary<string,AssemblyFacts> _dictAssemblies;

		///<summary>File name to full path, ignoring case.</summary>
		public static Dictionary<string,string> Files {
			get {
				if(_dictFiles==null) {
					_dictFiles=Directory.GetFiles(RepoPaths.RequiredDlls).ToDictionary(x => Path.GetFileName(x),x => x,StringComparer.OrdinalIgnoreCase);
				}
				return _dictFiles;
			}
		}

		///<summary>Assembly name to its facts, ignoring case.</summary>
		public static Dictionary<string,AssemblyFacts> Assemblies {
			get {
				if(_dictAssemblies==null) {
					_dictAssemblies=MetadataReader.ReadFolder(RepoPaths.RequiredDlls);
				}
				return _dictAssemblies;
			}
		}

		///<summary>The managed file of "Required dlls" with this file name, or null.</summary>
		public static AssemblyFacts ByFileName(string fileName) {
			return Assemblies.Values.FirstOrDefault(x => x.FileName.Equals(fileName,StringComparison.OrdinalIgnoreCase));
		}
	}

	internal class BindingRedirect {
		public string Name;
		public string Token;
		public Version OldLow;
		public Version OldHigh;
		public Version NewVersion;

		public bool Covers(Version version) {
			return OldLow<=version && version<=OldHigh;
		}

		public override string ToString() {
			return Name+" ("+Token+") "+OldLow+"-"+OldHigh+" -> "+NewVersion;
		}
	}

	internal static class ConfigFiles {
		private static readonly XNamespace _xNamespaceBinding="urn:schemas-microsoft-com:asm.v1";

		///<summary>Every bindingRedirect of the config's runtime section, with the identity of its dependentAssembly.</summary>
		public static List<BindingRedirect> ReadRedirects(string path) {
			List<BindingRedirect> listRedirects=new List<BindingRedirect>();
			XDocument xDocument=XDocument.Load(path);
			foreach(XElement xElementDependent in xDocument.Descendants(_xNamespaceBinding+"dependentAssembly")) {
				XElement xElementIdentity=xElementDependent.Element(_xNamespaceBinding+"assemblyIdentity");
				foreach(XElement xElementRedirect in xElementDependent.Elements(_xNamespaceBinding+"bindingRedirect")) {
					string[] arrayOld=((string)xElementRedirect.Attribute("oldVersion")??"").Split('-');
					listRedirects.Add(new BindingRedirect {
						Name=(string)xElementIdentity?.Attribute("name")??"",
						Token=((string)xElementIdentity?.Attribute("publicKeyToken")??"").ToLowerInvariant(),
						OldLow=Version.Parse(arrayOld[0].Trim()),
						OldHigh=Version.Parse(arrayOld[arrayOld.Length-1].Trim()),
						NewVersion=Version.Parse(((string)xElementRedirect.Attribute("newVersion")).Trim()),
					});
				}
			}
			return listRedirects;
		}

		///<summary>The redirect element for a message: what to add to a config.</summary>
		public static string FormatRedirect(AssemblyFacts assemblyFacts) {
			return "<dependentAssembly><assemblyIdentity name=\""+assemblyFacts.Name+"\" publicKeyToken=\""+assemblyFacts.Token+"\" culture=\"neutral\" />"
				+"<bindingRedirect oldVersion=\"0.0.0.0-"+assemblyFacts.Version+"\" newVersion=\""+assemblyFacts.Version+"\" /></dependentAssembly>";
		}
	}

	///<summary>The .NET Framework's global assembly caches (the 4.0 one and the 2.0 one), looked up by folder name. Nothing is loaded.</summary>
	internal static class Gac {
		private static readonly string[] _arrayRoots=new[] {
			@"Microsoft.NET\assembly\GAC_MSIL",@"Microsoft.NET\assembly\GAC_32",@"Microsoft.NET\assembly\GAC_64",
			@"assembly\GAC_MSIL",@"assembly\GAC_32",@"assembly\GAC_64",@"assembly\GAC",
		};

		private static IEnumerable<string> AssemblyFolders(string name) {
			string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			return _arrayRoots.Select(x => Path.Combine(windows,x,name)).Where(x => Directory.Exists(x));
		}

		public static bool Has(string name) {
			return AssemblyFolders(name).Any();
		}

		///<summary>True when the cache holds this exact version and token ("v4.0_4.0.0.0__b77a5c561934e089" or "2.0.0.0__b77a5c561934e089").</summary>
		public static bool HasVersion(string name,Version version,string token) {
			foreach(string folder in AssemblyFolders(name)) {
				foreach(string folderVersion in Directory.GetDirectories(folder).Select(x => Path.GetFileName(x))) {
					Match match=Regex.Match(folderVersion,@"^(?:v[\d.]+_)?(?<version>\d+\.\d+\.\d+\.\d+)_[^_]*_(?<token>[0-9a-f]+)$",RegexOptions.IgnoreCase);
					if(match.Success && Version.Parse(match.Groups["version"].Value)==version
						&& match.Groups["token"].Value.Equals(token,StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	///<summary>A C# project of OpenDental.Local.slnf, as far as these tests need it.</summary>
	internal class ProjectInfo {
		public string Path;
		public string Folder;
		public string AssemblyName;
		public bool IsExe;
		public bool IsWeb;
		///<summary>Ends with "Tests", the rule Directory.Build.targets uses for the shared test config.</summary>
		public bool IsTest;
		public bool AutoGeneratesRedirects;
		///<summary>File names in "Required dlls" this project references (embedded interop types excluded) or copies to its output: by a HintPath,
		///or as the file of a package of its lock file (since S2-1 of the .NET 10 move, the packages whose files are the ones of "Required dlls").
		///</summary>
		public List<string> ListRequiredDlls=new List<string>();
		///<summary>Primary references into "Required dlls" that are copied to the output whether the code uses them or not, and the files of the
		///packages of its lock file, which NuGet copies to the output of a .NET Framework project used or not.</summary>
		public List<string> ListPrimaryReferencesCopied=new List<string>();
		///<summary>Files of "Required dlls" that None or Content items copy to the output (and to the output of every project that references this one).</summary>
		public List<string> ListCopyItems=new List<string>();
		public List<string> ListProjectReferences=new List<string>();
		///<summary>app.config, App.config or Web.config next to the project, or null.</summary>
		public string ConfigPath;

		///<summary>The folder its Debug build writes the primary output to, or null when it is not built.</summary>
		public string OutputFolder {
			get {
				foreach(string relative in new[] { @"bin\Debug",@"bin\x86\Debug","bin" }) {
					string folder=System.IO.Path.Combine(Folder,relative);
					if(File.Exists(System.IO.Path.Combine(folder,PrimaryOutputName))) {
						return folder;
					}
				}
				return null;
			}
		}

		public string PrimaryOutputName {
			get { return AssemblyName+(IsExe ? ".exe" : ".dll"); }
		}
	}

	internal static class SolutionProjects {
		private static List<ProjectInfo> _listProjects;

		///<summary>The C# projects of OpenDental.Local.slnf.</summary>
		public static List<ProjectInfo> All {
			get {
				if(_listProjects!=null) {
					return _listProjects;
				}
				string slnf=RepoPaths.ReadText("OpenDental.Local.slnf");
				_listProjects=Regex.Matches(slnf,@"""([^""]+\.csproj)""").Cast<Match>()
					.Select(x => Read(RepoPaths.Combine(x.Groups[1].Value.Replace(@"\\",@"\"))))
					.ToList();
				return _listProjects;
			}
		}

		///<summary>The project's facts from its evaluated properties and items (Debug|x86, with Directory.Build.props and the other files it imports),
		///so they hold for the classic project files and the SDK-style ones of the .NET 10 move alike: no 2003 MSBuild namespace, metadata as
		///child elements or attributes, AutoGenerateBindingRedirects from tests\Directory.Build.props.</summary>
		private static ProjectInfo Read(string path) {
			ProjectFile projectFile=ProjectFile.Load(path);
			ProjectInfo projectInfo=new ProjectInfo();
			projectInfo.Path=projectFile.FullPath;
			projectInfo.Folder=projectFile.Folder;
			projectInfo.AssemblyName=projectFile.GetProperty("AssemblyName").Trim();
			if(projectInfo.AssemblyName=="") {
				projectInfo.AssemblyName=System.IO.Path.GetFileNameWithoutExtension(path);//MSBuild's default, the project's name
			}
			string outputType=projectFile.GetProperty("OutputType").Trim();
			projectInfo.IsExe=outputType.Equals("Exe",StringComparison.OrdinalIgnoreCase) || outputType.Equals("WinExe",StringComparison.OrdinalIgnoreCase);
			projectInfo.IsWeb=projectFile.GetProperty("ProjectTypeGuids").IndexOf("349c5851-65df-11da-9384-00065b846f21",StringComparison.OrdinalIgnoreCase)>=0;
			projectInfo.IsTest=System.IO.Path.GetFileNameWithoutExtension(path).EndsWith("Tests");
			projectInfo.AutoGeneratesRedirects=projectFile.GetProperty("AutoGenerateBindingRedirects").Trim().Equals("true",StringComparison.OrdinalIgnoreCase)
				&& projectFile.GetProperty("GenerateBindingRedirectsOutputType").Trim().Equals("true",StringComparison.OrdinalIgnoreCase);
			foreach(ProjectItem projectItemReference in projectFile.GetItems("Reference")) {
				string fileName=GetRequiredDllsFileName(projectItemReference.GetMetadata("HintPath"));
				if(fileName==null || projectItemReference.GetMetadata("EmbedInteropTypes").Trim().Equals("true",StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				projectInfo.ListRequiredDlls.Add(fileName);
				if(!projectItemReference.GetMetadata("Private").Trim().Equals("false",StringComparison.OrdinalIgnoreCase)) {
					projectInfo.ListPrimaryReferencesCopied.Add(fileName);
				}
			}
			foreach(string fileName in GetRequiredDllsOfPackages(projectInfo.Folder)) {
				projectInfo.ListRequiredDlls.Add(fileName);
				projectInfo.ListPrimaryReferencesCopied.Add(fileName);
			}
			foreach(ProjectItem projectItem in projectFile.GetItems("None").Concat(projectFile.GetItems("Content"))) {
				string fileName=GetRequiredDllsFileName(projectItem.Include);
				if(fileName!=null && projectItem.HasMetadata("CopyToOutputDirectory")) {
					projectInfo.ListRequiredDlls.Add(fileName);
					projectInfo.ListCopyItems.Add(fileName);
				}
			}
			foreach(ProjectItem projectItemReference in projectFile.GetItems("ProjectReference")) {
				if(projectItemReference.Include.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase)) {
					projectInfo.ListProjectReferences.Add(projectItemReference.FullPath);
				}
			}
			projectInfo.ConfigPath=Directory.GetFiles(projectInfo.Folder,"*.config")
				.FirstOrDefault(x => new[] { "app.config","web.config" }.Contains(System.IO.Path.GetFileName(x).ToLowerInvariant()));
			return projectInfo;
		}

		///<summary>The packages of the project's packages.lock.json (every package the restore gives it for .NET Framework 4.8, its own, those of the
		///projects it references and their dependencies) whose assembly is a file of "Required dlls": the package's id plus .dll, or Renci.SshNet.dll
		///for SSH.NET. Packages without such a file (Mono.Cecil, System.ValueTuple) are left out.</summary>
		private static List<string> GetRequiredDllsOfPackages(string folderProject) {
			List<string> listFiles=new List<string>();
			string pathLock=System.IO.Path.Combine(folderProject,"packages.lock.json");
			if(!File.Exists(pathLock)) {
				return listFiles;
			}
			foreach(Match match in Regex.Matches(File.ReadAllText(pathLock),@"""([^""]+)""\s*:\s*\{\s*""type""\s*:\s*""(Direct|Transitive|CentralTransitive)""")) {
				string packageId=match.Groups[1].Value;
				string fileName=(packageId.Equals("SSH.NET",StringComparison.OrdinalIgnoreCase) ? "Renci.SshNet" : packageId)+".dll";
				if(RequiredDlls.Files.ContainsKey(fileName) && !listFiles.Contains(fileName,StringComparer.OrdinalIgnoreCase)) {
					listFiles.Add(fileName);
				}
			}
			return listFiles;
		}

		///<summary>The file name when the path points into "Required dlls" (..\Required dlls\X.dll, ..\..\Required dlls\X.dll), otherwise null.</summary>
		private static string GetRequiredDllsFileName(string path) {
			if(string.IsNullOrWhiteSpace(path)) {
				return null;
			}
			Match match=Regex.Match(path.Trim(),@"(?:^|\\)Required dlls\\([^\\]+)$",RegexOptions.IgnoreCase);
			return match.Success ? match.Groups[1].Value : null;
		}

		public static ProjectInfo ByName(string projectFileName) {
			return All.Single(x => System.IO.Path.GetFileName(x.Path).Equals(projectFileName,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The project and the projects it references, all the way down.</summary>
		public static List<ProjectInfo> Closure(ProjectInfo projectInfo) {
			List<ProjectInfo> listProjects=new List<ProjectInfo>();
			HashSet<string> hashSetSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Stack<ProjectInfo> stackProjects=new Stack<ProjectInfo>();
			stackProjects.Push(projectInfo);
			while(stackProjects.Count>0) {
				ProjectInfo projectInfoCur=stackProjects.Pop();
				if(!hashSetSeen.Add(projectInfoCur.Path)) {
					continue;
				}
				listProjects.Add(projectInfoCur);
				foreach(string pathReference in projectInfoCur.ListProjectReferences) {
					ProjectInfo projectInfoReference=All.FirstOrDefault(x => x.Path.Equals(pathReference,StringComparison.OrdinalIgnoreCase));
					if(projectInfoReference!=null) {
						stackProjects.Push(projectInfoReference);
					}
				}
			}
			return listProjects;
		}

		///<summary>The "Required dlls" files the project and the projects it references (all the way down) reference or copy.</summary>
		public static HashSet<string> RequiredDllsInClosure(ProjectInfo projectInfo) {
			HashSet<string> hashSetFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(ProjectInfo projectInfoCur in Closure(projectInfo)) {
				hashSetFiles.UnionWith(projectInfoCur.ListRequiredDlls);
			}
			return hashSetFiles;
		}

		///<summary>True when the project, directly or through the projects it references, uses a managed assembly of "Required dlls".</summary>
		public static bool LoadsRequiredAssemblies(ProjectInfo projectInfo) {
			return RequiredDllsInClosure(projectInfo).Any(x => RequiredDlls.ByFileName(x)!=null);
		}
	}

	internal static class BindingRules {
		///<summary>Each reference from one of the assemblies to another of them (strong-named, the same key) at a version other than the one there must
		///be covered by a redirect of the config to that version. Returns one line per reference that is not.</summary>
		public static List<string> FindUnresolvedReferences(Dictionary<string,AssemblyFacts> dictAssemblies,List<BindingRedirect> listRedirects,string label) {
			List<string> listProblems=new List<string>();
			foreach(AssemblyFacts assemblyFacts in dictAssemblies.Values.OrderBy(x => x.FileName,StringComparer.OrdinalIgnoreCase)) {
				foreach(AssemblyReference assemblyReference in assemblyFacts.ListReferences) {
					if(!dictAssemblies.TryGetValue(assemblyReference.Name,out AssemblyFacts assemblyFactsTarget)) {
						continue;
					}
					//The loader ignores the version of an assembly without a strong name, and another key is another assembly.
					if(assemblyReference.Token=="" || assemblyReference.Token!=assemblyFactsTarget.Token) {
						continue;
					}
					if(assemblyReference.Version==assemblyFactsTarget.Version) {
						continue;
					}
					bool isRedirected=listRedirects.Any(x => x.Name.Equals(assemblyReference.Name,StringComparison.OrdinalIgnoreCase)
						&& x.Token==assemblyFactsTarget.Token && x.NewVersion==assemblyFactsTarget.Version && x.Covers(assemblyReference.Version));
					if(!isRedirected) {
						listProblems.Add(label+": "+assemblyFacts.FileName+" references "+assemblyReference.Name+" "+assemblyReference.Version+", but "
							+assemblyFactsTarget.FileName+" is "+assemblyFactsTarget.Version+". Add "+ConfigFiles.FormatRedirect(assemblyFactsTarget));
					}
				}
			}
			return listProblems;
		}

		///<summary>Every redirect must name the version that is there: in the folder of assemblies, else in "Required dlls", else in the .NET Framework.
		///Also no assembly may have two redirect entries. Returns one line per problem.</summary>
		public static List<string> FindStaleRedirects(Dictionary<string,AssemblyFacts> dictAssemblies,List<BindingRedirect> listRedirects,string label) {
			List<string> listProblems=new List<string>();
			foreach(IGrouping<string,BindingRedirect> grouping in listRedirects.GroupBy(x => x.Name.ToLowerInvariant()+"|"+x.Token).Where(x => x.Count()>1)) {
				listProblems.Add(label+": "+grouping.Count()+" redirects for "+grouping.First().Name+"; keep one.");
			}
			foreach(BindingRedirect bindingRedirect in listRedirects) {
				if(!dictAssemblies.TryGetValue(bindingRedirect.Name,out AssemblyFacts assemblyFacts)) {
					RequiredDlls.Assemblies.TryGetValue(bindingRedirect.Name,out assemblyFacts);
				}
				if(assemblyFacts!=null) {
					if(bindingRedirect.Token!=assemblyFacts.Token) {
						listProblems.Add(label+": the redirect "+bindingRedirect+" has another key than "+assemblyFacts.FileName+" ("+assemblyFacts.Token+").");
					}
					else if(bindingRedirect.NewVersion!=assemblyFacts.Version) {
						listProblems.Add(label+": the redirect "+bindingRedirect+" names a version that is not there; "+assemblyFacts.FileName+" is "
							+assemblyFacts.Version+".");
					}
					continue;
				}
				if(!Gac.HasVersion(bindingRedirect.Name,bindingRedirect.NewVersion,bindingRedirect.Token)) {
					listProblems.Add(label+": the redirect "+bindingRedirect+" names an assembly version that is neither in Required dlls nor in the .NET "
						+"Framework.");
				}
			}
			return listProblems;
		}
	}

	internal static class BuildRelease {
		private static List<KeyValuePair<string,string>> _listFiles;

		///<summary>The allow-list as tools\Build-Release.ps1 -ListFiles prints it: kind ("own", "third", "doc") and path. Nothing is built.</summary>
		public static List<KeyValuePair<string,string>> ListFiles() {
			if(_listFiles!=null) {
				return _listFiles;
			}
			ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe");
			processStartInfo.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+RepoPaths.Combine("tools","Build-Release.ps1")+"\" -ListFiles";
			processStartInfo.UseShellExecute=false;
			processStartInfo.RedirectStandardOutput=true;
			processStartInfo.RedirectStandardError=true;
			processStartInfo.CreateNoWindow=true;
			using(Process process=Process.Start(processStartInfo)) {
				Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				Task<string> taskErr=process.StandardError.ReadToEndAsync();
				if(!process.WaitForExit(120000)) {
					process.Kill();
					Assert.Fail("Build-Release.ps1 -ListFiles did not finish in 2 minutes.");
				}
				process.WaitForExit();
				Assert.AreEqual(0,process.ExitCode,taskOut.Result+taskErr.Result);
				_listFiles=new List<KeyValuePair<string,string>>();
				foreach(string line in taskOut.Result.Split(new[] { "\r\n","\n" },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())) {
					string[] arrayParts=line.Split('|');
					Assert.AreEqual(2,arrayParts.Length,"Unexpected line: "+line);
					_listFiles.Add(new KeyValuePair<string,string>(arrayParts[0],arrayParts[1]));
				}
			}
			return _listFiles;
		}
	}
}
