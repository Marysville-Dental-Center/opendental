// Shared helpers for the library binding tests: repository paths, assembly metadata read with Mono.Cecil (no file is loaded or locked), the
// binding redirects of a config file, the .NET Framework's global assembly cache, and the projects of OpenDental.Local.slnf.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
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

	///<summary>A package a project's packages.lock.json names (type Direct, Transitive or CentralTransitive), with the version it resolved to.
	///</summary>
	internal class LockedPackage {
		public string Id;
		public string Version;
	}

	///<summary>The files of "Required dlls" that each package of the lock files brings (since S2-1 of the .NET 10 move, the packages whose files
	///are byte for byte those of "Required dlls": docs/port-notes/S2-1.md, section 2), which NuGet copies to the output of a .NET Framework project
	///that gets the package, used or not. Named per package, not made from its id: a package's files need not carry its id (SSH.NET brings
	///Renci.SshNet.dll; Microsoft.Web.WebView2, since S2-3, brings Microsoft.Web.WebView2.Core.dll, .WinForms.dll and .Wpf.dll and the native
	///WebView2Loader.dll). GetRequiredDlls throws for a package in neither list and for a file "Required dlls" does not hold, so a package that a
	///later package adds fails every check that reads the projects until it is mapped here, instead of dropping out of the binding checks.
	///RequiredDllsTests checks every mapping against the package's own files in the offline folder.</summary>
	internal static class PackageFiles {
		///<summary>Package id (NuGet's ids ignore case) to its files of "Required dlls", at least one each.</summary>
		private static readonly Dictionary<string,string[]> _dictRequiredDlls=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase) {
			{ "BouncyCastle.Cryptography",new[] { "BouncyCastle.Cryptography.dll" } },
			{ "Google.Apis",new[] { "Google.Apis.dll" } },
			{ "Google.Apis.Auth",new[] { "Google.Apis.Auth.dll" } },
			{ "Google.Apis.Core",new[] { "Google.Apis.Core.dll" } },
			{ "Google.Apis.Gmail.v1",new[] { "Google.Apis.Gmail.v1.dll" } },
			{ "MailKit",new[] { "MailKit.dll" } },
			{ "MimeKit",new[] { "MimeKit.dll" } },
			{ "Microsoft.Identity.Client",new[] { "Microsoft.Identity.Client.dll" } },
			{ "Microsoft.IdentityModel.Abstractions",new[] { "Microsoft.IdentityModel.Abstractions.dll" } },
			//S2-3: the WebView2 SDK, 1.0.2420.47: its three assemblies (lib\net45) and the native loader (runtimes\win-x86\native, which NuGet
			//copies for an exe's win-x86 target; the package's build files copy the same file, Directory.Build.targets). None carries the id.
			{ "Microsoft.Web.WebView2",new[] { "Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","Microsoft.Web.WebView2.Wpf.dll",
				"WebView2Loader.dll" } },
			//S2-2: the database client, 2.6.2 (lib\net48), whose file "Required dlls" holds until S2-4.
			{ "MySqlConnector",new[] { "MySqlConnector.dll" } },
			{ "Newtonsoft.Json",new[] { "Newtonsoft.Json.dll" } },
			{ "SSH.NET",new[] { "Renci.SshNet.dll" } },
			{ "Microsoft.Bcl.AsyncInterfaces",new[] { "Microsoft.Bcl.AsyncInterfaces.dll" } },
			{ "Microsoft.Bcl.Cryptography",new[] { "Microsoft.Bcl.Cryptography.dll" } },
			{ "Microsoft.Extensions.DependencyInjection.Abstractions",new[] { "Microsoft.Extensions.DependencyInjection.Abstractions.dll" } },
			{ "Microsoft.Extensions.Logging.Abstractions",new[] { "Microsoft.Extensions.Logging.Abstractions.dll" } },
			{ "System.Buffers",new[] { "System.Buffers.dll" } },
			{ "System.Diagnostics.DiagnosticSource",new[] { "System.Diagnostics.DiagnosticSource.dll" } },
			{ "System.Formats.Asn1",new[] { "System.Formats.Asn1.dll" } },
			{ "System.IO.Pipelines",new[] { "System.IO.Pipelines.dll" } },
			{ "System.Memory",new[] { "System.Memory.dll" } },
			{ "System.Numerics.Vectors",new[] { "System.Numerics.Vectors.dll" } },
			{ "System.Runtime.CompilerServices.Unsafe",new[] { "System.Runtime.CompilerServices.Unsafe.dll" } },
			{ "System.Text.Encodings.Web",new[] { "System.Text.Encodings.Web.dll" } },
			{ "System.Text.Json",new[] { "System.Text.Json.dll" } },
			{ "System.Threading.Tasks.Extensions",new[] { "System.Threading.Tasks.Extensions.dll" } },
			//The 26th identical package, in no lock file (nothing references it; S2-4 takes it out of "Required dlls"): mapped as the others,
			//so a project that ever gets it is checked as before.
			{ "System.Security.Cryptography.Pkcs",new[] { "System.Security.Cryptography.Pkcs.dll" } },
		};

		///<summary>Packages of the lock files that bring no file of "Required dlls", each with its reason. An entry here exempts the package from
		///the binding checks: no file of it is counted for any project that gets it, so nothing of it is checked for its references, redirects or
		///output. A new entry therefore needs the lead's agreement, and its reason is written next to it. RequiredDllsTests checks that no file of
		///such a package, in any of its folders, has the name of a file of "Required dlls".</summary>
		private static readonly Dictionary<string,string> _dictNoRequiredDlls=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			{ "Mono.Cecil","the tests' reader of assembly metadata (S1-6), whose files were never in Required dlls" },
			{ "System.ValueTuple","no assembly for .NET Framework 4.7.1 and later (lib\\net471\\_._): the framework's own (SystemValueTuple_ComesFromTheFramework)" },
		};

		///<summary>True for a package of the lock files that brings no file of "Required dlls".</summary>
		public static bool BringsNoRequiredDlls(string packageId) {
			return _dictNoRequiredDlls.ContainsKey(packageId);
		}

		///<summary>The files of "Required dlls" the package brings: none for one of _dictNoRequiredDlls. Throws for a package neither list knows,
		///and for a mapping to a file "Required dlls" does not hold. where: the lock file, for the message.</summary>
		public static string[] GetRequiredDlls(string packageId,string where) {
			if(BringsNoRequiredDlls(packageId)) {
				return new string[0];
			}
			string[] arrayFiles;
			if(!_dictRequiredDlls.TryGetValue(packageId,out arrayFiles)) {
				throw new InvalidOperationException(where+" names the package "+packageId+", which PackageFiles in tests\\LibraryBindingTests\\TestSupport.cs does not "
					+"map: add the files of \"Required dlls\" it brings (or, if it brings none, the reason), so the binding checks cover it.");
			}
			CheckMapping(packageId,arrayFiles,where);
			return arrayFiles;
		}

		///<summary>Throws unless the files are at least one and every one is in "Required dlls".</summary>
		public static void CheckMapping(string packageId,string[] arrayFiles,string where) {
			List<string> listMissing=arrayFiles.Where(x => !RequiredDlls.Files.ContainsKey(x)).ToList();
			if(arrayFiles.Length==0 || listMissing.Count>0) {
				throw new InvalidOperationException("PackageFiles maps the package "+packageId+" ("+where+") to "
					+(arrayFiles.Length==0 ? "no file" : string.Join(", ",listMissing)+", which \"Required dlls\" does not hold")
					+": every mapped package brings at least one of its files.");
			}
		}

		///<summary>The packages of a packages.lock.json, each id and version once (an exe's lock file lists them again for win-x86). Throws for a
		///package entry without a resolved version, so none is skipped.</summary>
		public static List<LockedPackage> ReadLockFile(string pathLock) {
			string text=File.ReadAllText(pathLock);
			List<LockedPackage> listPackages=new List<LockedPackage>();
			foreach(Match match in Regex.Matches(text,@"""([^""]+)""\s*:\s*\{\s*""type""\s*:\s*""(?:Direct|Transitive|CentralTransitive)""")) {
				Match matchResolved=new Regex(@"\G(?:\s*,\s*""requested""\s*:\s*""[^""]*"")?\s*,\s*""resolved""\s*:\s*""([^""]+)""").Match(text,match.Index+match.Length);
				if(!matchResolved.Success) {
					throw new InvalidOperationException(RepoPaths.Relative(pathLock)+": the package "+match.Groups[1].Value+" has no resolved version where it was expected.");
				}
				string packageId=match.Groups[1].Value;
				string version=matchResolved.Groups[1].Value;
				if(!listPackages.Any(x => x.Id.Equals(packageId,StringComparison.OrdinalIgnoreCase) && x.Version.Equals(version,StringComparison.OrdinalIgnoreCase))) {
					listPackages.Add(new LockedPackage { Id=packageId,Version=version });
				}
			}
			return listPackages;
		}

		///<summary>The files the build copies from each package to a project's output, as the restore picked them in the project's
		///project.assets.json: the runtime and native assets of the package in every target of the file, by "id/version" as the file names the
		///package (NuGet's ids ignore case), each a path in the package's folder with '/' (lib/net462/System.Memory.dll). The SDK's
		///ResolvePackageAssets copies a .NET Framework project's runtime assets of its target, the lib folder of the nearest framework the package
		///has (lib\net4x for net48), and for an exe, whose target is win-x86, also its native assets (runtimes\win-x86\native). Read with the
		///.NET Framework's JSON reader (JsonReaderWriterFactory), which gives each JSON member an element of its name, or, for a name that is no XML
		///name (a target, "id/version", an asset's path), an element 'item' with the name in its attribute 'item'.</summary>
		public static Dictionary<string,HashSet<string>> ReadCopiedAssets(string pathAssets) {
			XElement xElementRoot;
			using(XmlDictionaryReader xmlDictionaryReader=JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(pathAssets),XmlDictionaryReaderQuotas.Max)) {
				xElementRoot=XElement.Load(xmlDictionaryReader);
			}
			XElement xElementTargets=GetJsonMember(xElementRoot,"targets");
			if(xElementTargets==null || !xElementTargets.Elements().Any()) {
				throw new InvalidOperationException(RepoPaths.Relative(pathAssets)+" has no targets.");
			}
			Dictionary<string,HashSet<string>> dictAssets=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			foreach(XElement xElementLibrary in xElementTargets.Elements().SelectMany(x => x.Elements())) {
				XElement xElementType=GetJsonMember(xElementLibrary,"type");
				if(xElementType==null || xElementType.Value!="package") {
					continue;//a project the project references
				}
				string key=GetJsonName(xElementLibrary);
				HashSet<string> hashSetAssets;
				if(!dictAssets.TryGetValue(key,out hashSetAssets)) {
					hashSetAssets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					dictAssets[key]=hashSetAssets;
				}
				foreach(string section in new[] { "runtime","native" }) {
					XElement xElementSection=GetJsonMember(xElementLibrary,section);
					if(xElementSection!=null) {
						hashSetAssets.UnionWith(xElementSection.Elements().Select(x => GetJsonName(x)));
					}
				}
			}
			return dictAssets;
		}

		///<summary>The JSON member's name of an element JsonReaderWriterFactory wrote: its 'item' attribute for a name that is no XML name, else
		///the element's name.</summary>
		private static string GetJsonName(XElement xElement) {
			XAttribute xAttribute=xElement.Attribute("item");
			return xAttribute!=null ? xAttribute.Value : xElement.Name.LocalName;
		}

		private static XElement GetJsonMember(XElement xElementObject,string name) {
			return xElementObject.Elements().FirstOrDefault(x => GetJsonName(x)==name);
		}
	}

	///<summary>The offline package folder the build restores from (nuget.config's %OPMS_NUGET_OFFLINE%): the variable's folder when it is set,
	///else od-tools\nuget-packages in the repository's common git folder, where tools\Initialize-BuildEnvironment.ps1 points it for the lane;
	///null when that folder is not there. Its packages are laid out as NuGet lays out its global folder: id\version\, in lower case.</summary>
	internal static class OfflinePackages {
		public static string Folder {
			get {
				string folder=Environment.GetEnvironmentVariable("OPMS_NUGET_OFFLINE");
				if(string.IsNullOrWhiteSpace(folder)) {
					string folderGit=GetCommonGitFolder();
					folder=folderGit==null ? null : Path.Combine(folderGit,"od-tools","nuget-packages");
				}
				return folder!=null && Directory.Exists(folder) ? folder : null;
			}
		}

		///<summary>The folder of the package's version in the offline folder (which must be there).</summary>
		public static string GetPackageFolder(string packageId,string version) {
			return Path.Combine(Folder,packageId.ToLowerInvariant(),version.ToLowerInvariant());
		}

		///<summary>git rev-parse --git-common-dir, without git: .git itself when it is a folder; for a worktree, whose .git is a file naming its
		///own git folder, the folder its commondir file names.</summary>
		private static string GetCommonGitFolder() {
			string pathGit=Path.Combine(RepoPaths.Root,".git");
			if(Directory.Exists(pathGit)) {
				return pathGit;
			}
			if(!File.Exists(pathGit)) {
				return null;
			}
			Match match=Regex.Match(File.ReadAllText(pathGit),@"^gitdir:\s*(.+?)\s*$",RegexOptions.Multiline);
			if(!match.Success) {
				return null;
			}
			string folderGit=Path.GetFullPath(Path.Combine(RepoPaths.Root,match.Groups[1].Value.Replace('/','\\')));
			string pathCommonDir=Path.Combine(folderGit,"commondir");
			if(!File.Exists(pathCommonDir)) {
				return folderGit;
			}
			return Path.GetFullPath(Path.Combine(folderGit,File.ReadAllText(pathCommonDir).Trim().Replace('/','\\')));
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
		///or as the file of a package of its lock file (since S2-1 of the .NET 10 move, the packages whose files are the ones of "Required dlls",
		///as PackageFiles maps them).</summary>
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
				_listProjects=ProjectPaths.Select(x => Read(x)).ToList();
				return _listProjects;
			}
		}

		///<summary>The full paths of the C# project files of OpenDental.Local.slnf, in its order.</summary>
		public static List<string> ProjectPaths {
			get {
				string slnf=RepoPaths.ReadText("OpenDental.Local.slnf");
				return Regex.Matches(slnf,@"""([^""]+\.csproj)""").Cast<Match>().Select(x => RepoPaths.Combine(x.Groups[1].Value.Replace(@"\\",@"\"))).ToList();
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

		///<summary>The files of "Required dlls" that the packages of the project's packages.lock.json bring (every package the restore gives it for
		///.NET Framework 4.8, its own, those of the projects it references and their dependencies), as PackageFiles maps them; it throws for a
		///package it does not know, so none drops out of the checks.</summary>
		private static List<string> GetRequiredDllsOfPackages(string folderProject) {
			List<string> listFiles=new List<string>();
			string pathLock=System.IO.Path.Combine(folderProject,"packages.lock.json");
			if(!File.Exists(pathLock)) {
				return listFiles;
			}
			foreach(LockedPackage lockedPackage in PackageFiles.ReadLockFile(pathLock)) {
				foreach(string fileName in PackageFiles.GetRequiredDlls(lockedPackage.Id,RepoPaths.Relative(pathLock))) {
					if(!listFiles.Contains(fileName,StringComparer.OrdinalIgnoreCase)) {
						listFiles.Add(fileName);
					}
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
