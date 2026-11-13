// The NuGet packages a project's packages.lock.json names and the files the build copies from them, for the tests that check the files the
// programs get from packages (stage 2 of the .NET 10 move, docs/specs/net10-plan.md). Since S2-4 "Required dlls" no longer holds those files,
// so a check that compared a built or staged file with its copy in "Required dlls" compares it with the package's own file instead: the package
// at the version the lock file names, in the offline package folder the build restores from (nuget.config's %OPMS_NUGET_OFFLINE%) or, when that
// folder does not hold it, in the global packages folder the restore used (project.assets.json, packageFolders). tools\Build-Release.ps1 makes
// the same comparison for the staged release (Test-PackageFiles). Linked into the test projects that need it, as ProjectFile.cs is; its own
// tests are in tests\ProjectFileTests (NuGetFilesTests). Reads files only: no restore, no build, nothing is started.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ProjectFileTests {
	///<summary>A package a packages.lock.json names (type Direct, Transitive or CentralTransitive), with the version it resolved to and its content
	///hash (NuGet's hash of the package, which the restore checks).</summary>
	internal class LockedPackage {
		public string Id;
		public string Version;
		public string ContentHash;
	}

	///<summary>A file the build copies from a package into a project's output.</summary>
	internal class PackageFile {
		public string Id;
		///<summary>The version the project's lock file names.</summary>
		public string Version;
		///<summary>Its path in the package, with '/' (lib/net462/System.Memory.dll, runtimes/win-x86/native/WebView2Loader.dll).</summary>
		public string Asset;
		///<summary>The file in the package's folder: the offline folder's, else the restore's global packages folder's; null when neither holds the
		///package.</summary>
		public string Path;

		public string FileName {
			get { return Asset.Substring(Asset.LastIndexOf('/')+1); }
		}

		public override string ToString() {
			return Asset+" of "+Id+" "+Version;
		}
	}

	internal static class NuGetFiles {
		///<summary>The offline package folder the build restores from (nuget.config's %OPMS_NUGET_OFFLINE%): the variable's folder when it is set,
		///else od-tools\nuget-packages in the repository's common git folder, where tools\Initialize-BuildEnvironment.ps1 points it for the lane;
		///null when that folder is not there. Its packages are laid out as NuGet lays out its global folder: id\version\, in lower case.</summary>
		public static string GetOfflineFolder(string repoRoot) {
			string folder=Environment.GetEnvironmentVariable("OPMS_NUGET_OFFLINE");
			if(string.IsNullOrWhiteSpace(folder)) {
				string folderGit=GetCommonGitFolder(repoRoot);
				folder=folderGit==null ? null : System.IO.Path.Combine(folderGit,"od-tools","nuget-packages");
			}
			return folder!=null && Directory.Exists(folder) ? folder : null;
		}

		///<summary>The folder of the package's version under a package folder laid out as NuGet's global folder (id\version\, in lower case).</summary>
		public static string GetPackageFolder(string folderPackages,string packageId,string version) {
			return System.IO.Path.Combine(folderPackages,packageId.ToLowerInvariant(),version.ToLowerInvariant());
		}

		///<summary>The package's folder: the offline folder's when it holds the package, else the first global packages folder of the restore's
		///assets file (when given and there) that holds it; null when none does.</summary>
		public static string FindPackageFolder(string repoRoot,string packageId,string version,string pathAssets) {
			string folderOffline=GetOfflineFolder(repoRoot);
			if(folderOffline!=null && Directory.Exists(GetPackageFolder(folderOffline,packageId,version))) {
				return GetPackageFolder(folderOffline,packageId,version);
			}
			if(pathAssets==null || !File.Exists(pathAssets)) {
				return null;
			}
			foreach(string folderPackages in ReadPackageFolders(pathAssets)) {
				if(Directory.Exists(GetPackageFolder(folderPackages,packageId,version))) {
					return GetPackageFolder(folderPackages,packageId,version);
				}
			}
			return null;
		}

		///<summary>git rev-parse --git-common-dir, without git: .git itself when it is a folder; for a worktree, whose .git is a file naming its
		///own git folder, the folder its commondir file names.</summary>
		private static string GetCommonGitFolder(string repoRoot) {
			string pathGit=System.IO.Path.Combine(repoRoot,".git");
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
			string folderGit=System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot,match.Groups[1].Value.Replace('/','\\')));
			string pathCommonDir=System.IO.Path.Combine(folderGit,"commondir");
			if(!File.Exists(pathCommonDir)) {
				return folderGit;
			}
			return System.IO.Path.GetFullPath(System.IO.Path.Combine(folderGit,File.ReadAllText(pathCommonDir).Trim().Replace('/','\\')));
		}

		///<summary>The packages of a packages.lock.json, each id and version once (an exe's lock file lists some again for win-x86). Throws for a
		///package entry without a resolved version, so none is skipped. ContentHash is "" where the entry has none.</summary>
		public static List<LockedPackage> ReadLockFile(string pathLock) {
			string text=File.ReadAllText(pathLock);
			List<LockedPackage> listPackages=new List<LockedPackage>();
			foreach(Match match in Regex.Matches(text,@"""([^""]+)""\s*:\s*\{\s*""type""\s*:\s*""(?:Direct|Transitive|CentralTransitive)""")) {
				Match matchResolved=new Regex(@"\G(?:\s*,\s*""requested""\s*:\s*""[^""]*"")?\s*,\s*""resolved""\s*:\s*""([^""]+)""(?:\s*,\s*""contentHash""\s*:\s*""([^""]*)"")?")
					.Match(text,match.Index+match.Length);
				if(!matchResolved.Success) {
					throw new InvalidOperationException(pathLock+": the package "+match.Groups[1].Value+" has no resolved version where it was expected.");
				}
				string packageId=match.Groups[1].Value;
				string version=matchResolved.Groups[1].Value;
				if(!listPackages.Any(x => x.Id.Equals(packageId,StringComparison.OrdinalIgnoreCase) && x.Version.Equals(version,StringComparison.OrdinalIgnoreCase))) {
					listPackages.Add(new LockedPackage { Id=packageId,Version=version,ContentHash=matchResolved.Groups[2].Value });
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
			XElement xElementRoot=ReadJson(pathAssets);
			XElement xElementTargets=GetJsonMember(xElementRoot,"targets");
			if(xElementTargets==null || !xElementTargets.Elements().Any()) {
				throw new InvalidOperationException(pathAssets+" has no targets.");
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

		///<summary>The global packages folders the restore used (project.assets.json, packageFolders), in its order.</summary>
		public static List<string> ReadPackageFolders(string pathAssets) {
			XElement xElementFolders=GetJsonMember(ReadJson(pathAssets),"packageFolders");
			if(xElementFolders==null) {
				return new List<string>();
			}
			return xElementFolders.Elements().Select(x => GetJsonName(x)).ToList();
		}

		///<summary>The files the build copies from packages into the project's output, by file name (ignoring case): the runtime and native assets
		///its restore picked (ReadCopiedAssets of obj\project.assets.json; MSBuildProjectExtensionsPath is obj\ for every project of the repository),
		///each of the package at the version the project's packages.lock.json names, with the file in the package's folder (FindPackageFolder).
		///NuGet's "_._" (no file for this framework) is not a file. Throws for a package of the restore that the lock file does not name or names
		///at another version, and for two packages that bring a file of the same name. Null when the project is not restored yet (no assets file);
		///empty when it has no lock file.</summary>
		public static Dictionary<string,PackageFile> ReadCopiedFiles(string repoRoot,string folderProject) {
			Dictionary<string,PackageFile> dictFiles=new Dictionary<string,PackageFile>(StringComparer.OrdinalIgnoreCase);
			string pathLock=System.IO.Path.Combine(folderProject,"packages.lock.json");
			string pathAssets=System.IO.Path.Combine(folderProject,"obj","project.assets.json");
			if(!File.Exists(pathLock)) {
				return dictFiles;
			}
			if(!File.Exists(pathAssets)) {
				return null;
			}
			List<LockedPackage> listLocked=ReadLockFile(pathLock);
			foreach(KeyValuePair<string,HashSet<string>> pair in ReadCopiedAssets(pathAssets).OrderBy(x => x.Key,StringComparer.OrdinalIgnoreCase)) {
				string[] arrayParts=pair.Key.Split('/');
				LockedPackage lockedPackage=listLocked.FirstOrDefault(x => x.Id.Equals(arrayParts[0],StringComparison.OrdinalIgnoreCase));
				if(lockedPackage==null || !lockedPackage.Version.Equals(arrayParts[1],StringComparison.OrdinalIgnoreCase)) {
					throw new InvalidOperationException(pathAssets+" has the package "+pair.Key+", but "+pathLock+" names "
						+(lockedPackage==null ? "no such package" : "version "+lockedPackage.Version)+": build again, so the restore writes it anew.");
				}
				string folderPackage=FindPackageFolder(repoRoot,lockedPackage.Id,lockedPackage.Version,pathAssets);
				foreach(string asset in pair.Value.OrderBy(x => x,StringComparer.OrdinalIgnoreCase)) {
					PackageFile packageFile=new PackageFile { Id=lockedPackage.Id,Version=lockedPackage.Version,Asset=asset };
					if(packageFile.FileName=="_._") {
						continue;
					}
					packageFile.Path=folderPackage==null ? null : System.IO.Path.Combine(folderPackage,asset.Replace('/','\\'));
					PackageFile packageFileOther;
					if(dictFiles.TryGetValue(packageFile.FileName,out packageFileOther)) {
						if(packageFileOther.Id!=packageFile.Id || packageFileOther.Asset!=packageFile.Asset) {
							throw new InvalidOperationException(pathAssets+": two package files are named "+packageFile.FileName+": "+packageFileOther+" and "+packageFile+".");
						}
						continue;
					}
					dictFiles.Add(packageFile.FileName,packageFile);
				}
			}
			return dictFiles;
		}

		///<summary>SHA-256 of the file in lower-case hex.</summary>
		public static string Sha256(string path) {
			using(SHA256 sha256=SHA256.Create()) {
				return string.Concat(sha256.ComputeHash(File.ReadAllBytes(path)).Select(x => x.ToString("x2")));
			}
		}

		private static XElement ReadJson(string path) {
			using(XmlDictionaryReader xmlDictionaryReader=JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(path),XmlDictionaryReaderQuotas.Max)) {
				return XElement.Load(xmlDictionaryReader);
			}
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
}
