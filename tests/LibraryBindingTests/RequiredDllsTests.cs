// The support assemblies are the versions of the library updates (lib\net462 of the NuGet packages the owner approved), the .NET Framework's
// own System.ValueTuple is used instead of a copy, and fo-dicom 4 and the packages only fo-dicom 5 needed stay out (the owner dropped fo-dicom
// for the in-house DICOM reader on 2026-11-02). Every package of the projects' lock files maps to the files the build copies from it (since
// S2-1 of the .NET 10 move), and since S2-4 those files come from the packages only: "Required dlls" holds just the files that have no package,
// and each package file the release ships is the file of the package at the version the lock files name. A later package that replaces one of
// these files changes its line here on purpose.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace LibraryBindingTests {
	[TestClass]
	public class RequiredDllsTests {
		///<summary>File, assembly version, file version and NuGet package version of each support assembly (the package's id is the file's name
		///without .dll). The tier 1 library updates use them all. The libraries themselves are checked by tests/LibraryUpdateTests
		///(LibraryFilesTests).</summary>
		private static readonly string[][] _arraySupportAssemblies=new[] {
			//S2-2 of the .NET 10 move: the three Microsoft packages that were on 8.0.x, and Microsoft.Bcl.AsyncInterfaces with them, at 10.0.15.
			new[] { "Microsoft.Bcl.AsyncInterfaces.dll","10.0.0.15","10.0.1526.53008","10.0.15" },
			new[] { "Microsoft.Bcl.Cryptography.dll","10.0.0.10","10.0.1026.32716","10.0.10" },
			new[] { "Microsoft.Extensions.DependencyInjection.Abstractions.dll","10.0.0.15","10.0.1526.53008","10.0.15" },
			new[] { "Microsoft.Extensions.Logging.Abstractions.dll","10.0.0.15","10.0.1526.53008","10.0.15" },
			new[] { "System.Buffers.dll","4.0.5.0","4.600.125.16908","4.6.1" },
			new[] { "System.Diagnostics.DiagnosticSource.dll","10.0.0.15","10.0.1526.53008","10.0.15" },
			new[] { "System.Formats.Asn1.dll","10.0.0.13","10.0.1326.50504","10.0.13" },
			new[] { "System.IO.Pipelines.dll","10.0.0.14","10.0.1426.52326","10.0.14" },
			new[] { "System.Memory.dll","4.0.5.0","4.600.325.20307","4.6.3" },
			new[] { "System.Numerics.Vectors.dll","4.1.6.0","4.600.125.16908","4.6.1" },
			new[] { "System.Runtime.CompilerServices.Unsafe.dll","6.0.3.0","6.100.225.20307","6.1.2" },
			new[] { "System.Threading.Tasks.Extensions.dll","4.2.4.0","4.600.325.20307","4.6.3" },
		};

		///<summary>What "Required dlls" holds after stage 2 of the .NET 10 move: the files that have no NuGet package (docs/specs/net10-plan.md, "Left
		///in Required dlls after stage 2"). The Direct Project's three files, the interop of Schick (CDR) and QuickBooks Desktop, SharpDX and Tao,
		///and Windows.winmd.</summary>
		private static readonly string[] _arrayRequiredDllsLeft=new[] {
			"Health.Direct.Agent.dll","Health.Direct.Common.dll","Health.Direct.ResolverPlugins.dll","Interop.CDRDATALib.dll","Interop.CDRDicom.dll",
			"Interop.QBFC10.dll","SharpDX.Direct3D9.dll","SharpDX.Mathematics.dll","SharpDX.dll","Tao.OpenGl.dll","Tao.Platform.Windows.dll","Windows.winmd",
		};

		///<summary>The files S2-4 deleted from "Required dlls" that come from no package: System.Security.Cryptography.Pkcs 8.0.1, which nothing
		///referenced and no release shipped, and the x86 Visual C++ runtime vcruntime140.dll, which nothing used (the setup installs
		///vc_redist.x86.exe). The 30 files the packages supply are PackageFiles' (RequiredDlls_HoldOnlyTheFilesThatHaveNoPackage).</summary>
		private static readonly string[] _arrayDeletedWithoutPackage=new[] { "System.Security.Cryptography.Pkcs.dll","vcruntime140.dll" };

		///<summary>fo-dicom 4 (Dicom.Core.dll and its native codecs Dicom.Native.dll), which the in-house DICOM reader replaced, and the packages
		///only fo-dicom 5 needed (and fo-dicom 5 itself). No library that stays uses them: MimeKit's net48 build does not reference
		///System.Text.Encoding.CodePages, and Microsoft.Extensions.Logging.Abstractions 10.0.15 needs only DependencyInjection.Abstractions,
		///DiagnosticSource, System.Buffers and System.Memory, all of them library files.</summary>
		private static readonly string[] _arrayFoDicomOnly=new[] {
			"Dicom.Core.dll","Dicom.Native.dll",
			"CommunityToolkit.HighPerformance.dll","Microsoft.Bcl.HashCode.dll","Microsoft.Extensions.Configuration.Abstractions.dll",
			"Microsoft.Extensions.Configuration.Binder.dll","Microsoft.Extensions.DependencyInjection.dll","Microsoft.Extensions.Logging.dll",
			"Microsoft.Extensions.Options.dll","Microsoft.Extensions.Options.ConfigurationExtensions.dll","Microsoft.Extensions.Primitives.dll",
			"System.Text.Encoding.CodePages.dll","System.Threading.Channels.dll","fo-dicom.core.dll","fo-dicom.Imaging.Desktop.dll",
		};

		[TestMethod]
		public void SupportAssemblies_AreTheVersionsOfTheLibraryUpdates() {
			List<string> listProblems=new List<string>();
			foreach(string[] arrayRow in _arraySupportAssemblies) {
				string packageId=Path.GetFileNameWithoutExtension(arrayRow[0]);
				string versionLocked;
				if(!Libraries.LockedVersions.TryGetValue(packageId,out versionLocked) || versionLocked!=arrayRow[3]) {
					listProblems.Add("The lock files name "+packageId+" "+(versionLocked??"nowhere")+", not "+arrayRow[3]+".");
					continue;
				}
				string path;
				if(!Libraries.Files.TryGetValue(arrayRow[0],out path) || !File.Exists(path)) {
					listProblems.Add(arrayRow[0]+" is missing (NuGet package "+packageId+" "+arrayRow[3]+").");
					continue;
				}
				Assert.AreEqual(packageId,Libraries.PackageOf(arrayRow[0]),true,arrayRow[0]+" comes from its package, not from Required dlls.");
				AssemblyFacts assemblyFacts=MetadataReader.Read(path);
				string fileVersion=FileVersionInfo.GetVersionInfo(path).FileVersion;
				if(!assemblyFacts.IsManaged || assemblyFacts.Version.ToString()!=arrayRow[1] || fileVersion!=arrayRow[2]) {
					listProblems.Add(arrayRow[0]+" is "+assemblyFacts.Version+" (file "+fileVersion+"), not "+arrayRow[1]+" (file "+arrayRow[2]+", NuGet package "
						+arrayRow[3]+").");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void SystemValueTuple_ComesFromTheFramework() {
			//.NET Framework 4.7 and later have System.ValueTuple in the global assembly cache and bind every 4.0.x reference to it, so a copy in
			//"Required dlls" (4.0.3.0 used to sit there, referenced by nothing) is never used and must not come back.
			Assert.IsFalse(File.Exists(Path.Combine(RepoPaths.RequiredDlls,"System.ValueTuple.dll")),"Required dlls\\System.ValueTuple.dll is back.");
			Assert.IsFalse(Libraries.Files.ContainsKey("System.ValueTuple.dll"),"No package brings a System.ValueTuple.dll for .NET Framework 4.8.");
			List<AssemblyReference> listReferences=Libraries.Assemblies.Values.SelectMany(x => x.ListReferences)
				.Where(x => x.Name=="System.ValueTuple").ToList();
			foreach(Version version in listReferences.Select(x => x.Version).Distinct()) {
				AssemblyName assemblyName=new AssemblyName("System.ValueTuple, Version="+version+", Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
				Assembly assembly=Assembly.Load(assemblyName);//the framework's own assembly, not a file of this repository
				Assert.IsTrue(assembly.GlobalAssemblyCache,"System.ValueTuple "+version+" should bind to the framework's copy, not "+assembly.Location);
			}
		}

		///<summary>Since S2-4 of the .NET 10 move "Required dlls" holds only the files that have no NuGet package: none of a package the lock files
		///name (PackageFiles), none of the two files that left without one (System.Security.Cryptography.Pkcs, vcruntime140.dll), nothing else.
		///A file of a package that comes back would be a second source of it (the release takes it from the package).</summary>
		[TestMethod]
		public void RequiredDlls_HoldOnlyTheFilesThatHaveNoPackage() {
			CollectionAssert.AreEquivalent(_arrayRequiredDllsLeft.Select(x => x.ToLowerInvariant()).ToArray(),
				RequiredDlls.Files.Keys.Select(x => x.ToLowerInvariant()).ToArray(),"Required dlls: "+string.Join(", ",RequiredDlls.Files.Keys.OrderBy(x => x)));
			List<string> listPackageFiles=PackageFiles.MappedIds.SelectMany(x => PackageFiles.GetFileNames(x,"PackageFiles")).ToList();
			Assert.AreEqual(30,listPackageFiles.Count,"The files of S2-1, S2-2 and S2-3 that S2-4 took out of Required dlls: "+string.Join(", ",listPackageFiles));
			foreach(string fileName in listPackageFiles.Concat(_arrayDeletedWithoutPackage)) {
				Assert.IsFalse(File.Exists(Path.Combine(RepoPaths.RequiredDlls,fileName)),"Required dlls\\"+fileName+" is back.");
			}
			//The Pkcs package is in no lock file either, so no project gets the file another way.
			Assert.IsFalse(Libraries.LockedVersions.ContainsKey("System.Security.Cryptography.Pkcs"));
		}

		///<summary>Since S2-1 of the .NET 10 move the projects get the files of the packages from the offline folder, and the binding checks count
		///the files a project's lock file brings (PackageFiles). Every package of every lock file of the solution filter is mapped there or brings
		///none, and every mapped package is in a lock file. Each mapped asset is a file of the package at the version the lock files name, in the
		///offline folder, and exactly the runtime and native assets the restore picked for the projects that get it are mapped (their
		///project.assets.json, NuGetFiles.ReadCopiedAssets: lib\net4x of the nearest framework, and runtimes\win-x86\native for an exe's win-x86
		///target), so a file of the same name in another folder of the package (another framework's lib folder) is never the one checked, and a
		///file a new version adds is not left out. A package that brings none has no file of a library file's name in any of its folders. A package
		///that a later package adds fails here until it is mapped, instead of dropping out (S2-2 mapped MySqlConnector, S2-3 Microsoft.Web.WebView2).
		///The checks that need no assets file run first and fail as they would after a build; without the restore's assets files (no build yet) the
		///rest is inconclusive, a failure with OPMS_REQUIRE_BUILD_OUTPUT=1 (READER-ENV, from S2-1's review).</summary>
		[TestMethod]
		public void PackagesOfTheLockFiles_MapToTheFilesTheBuildCopies() {
			string folderOffline=NuGetFiles.GetOfflineFolder(RepoPaths.Root);
			if(folderOffline==null) {
				BuildOutput.NotBuilt("The offline package folder (OPMS_NUGET_OFFLINE, or od-tools\\nuget-packages in the common git folder) is not there; "
					+"the build restores from it (README, Building).");
				return;
			}
			Dictionary<string,LockedPackage> dictPackages=new Dictionary<string,LockedPackage>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string,string> dictWhere=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			//"id/version" to the files the build copies from it, over every project that gets it.
			Dictionary<string,HashSet<string>> dictCopied=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			List<string> listNotRestored=new List<string>();
			foreach(string pathProject in SolutionProjects.ProjectPaths) {
				string pathLock=Path.Combine(Path.GetDirectoryName(pathProject),"packages.lock.json");
				if(!File.Exists(pathLock)) {
					continue;
				}
				foreach(LockedPackage lockedPackage in NuGetFiles.ReadLockFile(pathLock)) {
					string key=lockedPackage.Id+"/"+lockedPackage.Version;
					if(!dictPackages.ContainsKey(key)) {
						dictPackages[key]=lockedPackage;
						dictWhere[key]=RepoPaths.Relative(pathLock);
					}
				}
				//Where the restore wrote it (MSBuildProjectExtensionsPath, obj\ for every project of the repository), as the reader evaluates it.
				string pathAssets=ProjectFile.Load(pathProject).GetProperty("ProjectAssetsFile");
				if(!File.Exists(pathAssets)) {
					listNotRestored.Add(RepoPaths.Relative(pathAssets));
					continue;
				}
				foreach(KeyValuePair<string,HashSet<string>> pairAssets in NuGetFiles.ReadCopiedAssets(pathAssets)) {
					HashSet<string> hashSetCopied;
					if(!dictCopied.TryGetValue(pairAssets.Key,out hashSetCopied)) {
						hashSetCopied=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
						dictCopied[pairAssets.Key]=hashSetCopied;
					}
					hashSetCopied.UnionWith(pairAssets.Value);
				}
			}
			Assert.IsTrue(dictPackages.Count>0,"No lock file of the solution filter names a package.");
			//First what needs no assets file: each package is in the offline folder and mapped (or brings none, and then holds no file of a library
			//file's name), each mapped asset is a file of it, and each mapped package is in a lock file.
			List<string> listProblems=new List<string>();
			HashSet<string> hashSetLibraryNames=new HashSet<string>(RequiredDlls.Files.Keys,StringComparer.OrdinalIgnoreCase);
			hashSetLibraryNames.UnionWith(PackageFiles.MappedIds.SelectMany(x => PackageFiles.GetFileNames(x,"PackageFiles")));
			Dictionary<string,string[]> dictMapped=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
			foreach(KeyValuePair<string,LockedPackage> pair in dictPackages.OrderBy(x => x.Key,StringComparer.OrdinalIgnoreCase)) {
				LockedPackage lockedPackage=pair.Value;
				string folderPackage=NuGetFiles.GetPackageFolder(folderOffline,lockedPackage.Id,lockedPackage.Version);
				if(!Directory.Exists(folderPackage)) {
					listProblems.Add(pair.Key+" ("+dictWhere[pair.Key]+") is not in the offline folder.");
					continue;
				}
				if(PackageFiles.BringsNoLibraries(lockedPackage.Id)) {
					List<string> listNamed=Directory.GetFiles(folderPackage,"*",SearchOption.AllDirectories).Select(x => Path.GetFileName(x))
						.Where(x => hashSetLibraryNames.Contains(x)).Distinct().ToList();
					if(listNamed.Count>0) {
						listProblems.Add(pair.Key+" is mapped to no library file, but holds "+string.Join(", ",listNamed)+".");
					}
					continue;
				}
				string[] arrayAssets;
				try {
					arrayAssets=PackageFiles.GetAssets(lockedPackage.Id,dictWhere[pair.Key]);
				}
				catch(InvalidOperationException ex) {
					listProblems.Add(ex.Message);
					continue;
				}
				dictMapped[pair.Key]=arrayAssets;
				foreach(string asset in arrayAssets) {
					string pathAsset=Path.Combine(folderPackage,asset.Replace('/','\\'));
					if(!File.Exists(pathAsset)) {
						listProblems.Add(pair.Key+" has no "+asset+".");
					}
					else if(!pathAsset.Equals(Libraries.Files[Path.GetFileName(pathAsset)],StringComparison.OrdinalIgnoreCase)) {
						listProblems.Add(Path.GetFileName(pathAsset)+" is "+Libraries.Files[Path.GetFileName(pathAsset)]+" for the binding checks, not "+asset+" of "+pair.Key+".");
					}
				}
			}
			foreach(string packageId in PackageFiles.MappedIds.Where(x => !dictPackages.Values.Any(y => y.Id.Equals(x,StringComparison.OrdinalIgnoreCase)))) {
				listProblems.Add("PackageFiles maps "+packageId+", which no lock file names: remove the mapping.");
			}
			//Then what the restore's assets files say: inconclusive without them (no build yet), unless a check above failed already.
			if(listNotRestored.Count>0) {
				Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
				BuildOutput.NotBuilt("The restore's assets files of "+listNotRestored.Count+" projects with a lock file ("+listNotRestored[0]+", ...): the files the "
					+"build copies from each package are read from them; the "+dictPackages.Count+" packages are in the offline folder and mapped.");
				return;
			}
			int countFiles=0;
			HashSet<string> hashSetFolders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(KeyValuePair<string,string[]> pair in dictMapped.OrderBy(x => x.Key,StringComparer.OrdinalIgnoreCase)) {
				HashSet<string> hashSetCopied;
				if(!dictCopied.TryGetValue(pair.Key,out hashSetCopied)) {
					listProblems.Add(pair.Key+" ("+dictWhere[pair.Key]+") is in no project's assets file: build again, so the restore writes them.");
					continue;
				}
				foreach(string asset in pair.Value.Where(x => !hashSetCopied.Contains(x))) {
					listProblems.Add(pair.Key+": the build copies no "+asset+" (its runtime and native assets: "+string.Join(", ",hashSetCopied.OrderBy(x => x))+").");
				}
				foreach(string asset in hashSetCopied.Where(x => !pair.Value.Contains(x,StringComparer.OrdinalIgnoreCase)).OrderBy(x => x)) {
					listProblems.Add(pair.Key+": the build copies "+asset+", which PackageFiles does not map.");
				}
				countFiles+=pair.Value.Length;
				hashSetFolders.UnionWith(pair.Value.Select(x => x.Substring(0,x.LastIndexOf('/'))));
			}
			Console.WriteLine(dictPackages.Count+" packages in the lock files; "+countFiles+" mapped files, each an asset the build copies, from "
				+string.Join(", ",hashSetFolders.OrderBy(x => x))+".");
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>Each third-party file the release ships that a NuGet package supplies (the 30 that "Required dlls" held until S2-4) is the file of
		///the package at the version the lock files name: OpenPmsDental's lock file names that version, the program's restore copies that very asset
		///of it (tools\Build-Release.ps1 compares each staged file with it: Test-PackageFiles), and the built program folder holds it byte for byte.
		///The other third-party files of the allow-list are the ones "Required dlls" keeps (the Direct Project, SharpDX, Tao) or are built here.</summary>
		[TestMethod]
		public void ShippedPackageFiles_AreTheFilesOfTheLockFilesVersions() {
			List<string> listThird=BuildRelease.ListFiles().Where(x => x.Key=="third").Select(x => x.Value).ToList();
			List<string> listFromPackages=listThird.Where(x => Libraries.PackageOf(x)!=null).ToList();
			Assert.AreEqual(30,listFromPackages.Count,string.Join(", ",listFromPackages));
			CollectionAssert.AreEquivalent(new[] { "Health.Direct.Agent.dll","Health.Direct.Common.dll","Health.Direct.ResolverPlugins.dll","SharpDX.dll",
				"SharpDX.Direct3D9.dll","SharpDX.Mathematics.dll","Tao.OpenGl.dll","Tao.Platform.Windows.dll" },
				listThird.Where(x => RequiredDlls.Files.ContainsKey(x)).ToArray(),"The shipped files of Required dlls.");
			string folderProgram=RepoPaths.Combine("OpenPmsDental");
			List<LockedPackage> listLocked=NuGetFiles.ReadLockFile(Path.Combine(folderProgram,"packages.lock.json"));
			List<string> listProblems=new List<string>();
			foreach(string fileName in listFromPackages) {
				string packageId=Libraries.PackageOf(fileName);
				LockedPackage lockedPackage=listLocked.FirstOrDefault(x => x.Id.Equals(packageId,StringComparison.OrdinalIgnoreCase));
				if(lockedPackage==null || lockedPackage.Version!=Libraries.LockedVersions[packageId]) {
					listProblems.Add(fileName+": OpenPmsDental's lock file names "+packageId+" "+(lockedPackage==null ? "nowhere" : lockedPackage.Version)+".");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			Dictionary<string,PackageFile> dictCopied=NuGetFiles.ReadCopiedFiles(RepoPaths.Root,folderProgram);
			if(dictCopied==null) {
				BuildOutput.NotBuilt("OpenPmsDental is not restored yet (obj\\project.assets.json): build OpenDental.Local.slnf first.");
				return;
			}
			string folderBuilt=Path.Combine(folderProgram,"bin","Debug");
			bool isBuilt=File.Exists(Path.Combine(folderBuilt,"OpenPmsDental.exe"));
			foreach(string fileName in listFromPackages) {
				PackageFile packageFile;
				string pathLibrary=Libraries.Files[fileName];
				if(!dictCopied.TryGetValue(fileName,out packageFile)) {
					listProblems.Add(fileName+": the program's restore copies it from no package.");
					continue;
				}
				string pathExpected=Path.Combine(Libraries.GetLockedPackageFolder(packageFile.Id),packageFile.Asset.Replace('/','\\'));
				if(!packageFile.Id.Equals(Libraries.PackageOf(fileName),StringComparison.OrdinalIgnoreCase) || packageFile.Version!=Libraries.LockedVersions[packageFile.Id]
					|| !pathExpected.Equals(pathLibrary,StringComparison.OrdinalIgnoreCase))
				{
					listProblems.Add(fileName+": the program's restore copies "+packageFile+", the binding checks use "+pathLibrary+".");
				}
				if(isBuilt && NuGetFiles.Sha256(Path.Combine(folderBuilt,fileName))!=NuGetFiles.Sha256(pathLibrary)) {
					listProblems.Add(fileName+": OpenPmsDental\\bin\\Debug holds another file than "+packageFile+".");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			if(!isBuilt) {
				BuildOutput.NotBuilt(@"OpenPmsDental\bin\Debug is not built, so its copies are not compared. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
		}

		[TestMethod]
		public void PackageFiles_APackageItDoesNotMap_FailsLoudly() {
			//A package a later package adds and does not map: every check that reads the projects throws instead of leaving its files out.
			AssertThrows<InvalidOperationException>(() => PackageFiles.GetAssets("Fixture.Unmapped","x\\packages.lock.json"),"names the package Fixture.Unmapped");
			CollectionAssert.AreEqual(new[] { "Renci.SshNet.dll" },PackageFiles.GetFileNames("ssh.net","x"),"NuGet's ids ignore case.");
			//S2-3's Microsoft.Web.WebView2 brings files whose names are not its id: its three assemblies and the native loader of the win-x86 target
			//(PackagesOfTheLockFiles_MapToTheFilesTheBuildCopies checks each against the assets the build copies from it).
			CollectionAssert.AreEqual(new[] { "lib/net45/Microsoft.Web.WebView2.Core.dll","lib/net45/Microsoft.Web.WebView2.WinForms.dll",
				"lib/net45/Microsoft.Web.WebView2.Wpf.dll","runtimes/win-x86/native/WebView2Loader.dll" },PackageFiles.GetAssets("Microsoft.Web.WebView2","x"));
			CollectionAssert.AreEqual(new string[0],PackageFiles.GetAssets("System.ValueTuple","x"));
			//A mapping to no file, to a file of another folder, or to a file "Required dlls" holds too fails the same way.
			AssertThrows<InvalidOperationException>(() => PackageFiles.CheckMapping("Fixture.Package",new string[0],"x"),"to no file");
			AssertThrows<InvalidOperationException>(() => PackageFiles.CheckMapping("Fixture.Package",new[] { "lib/net45/Fixture.dll","ref/net45/Fixture.dll" },"x"),
				"ref/net45/Fixture.dll, which is no file of a lib or runtimes/win-x86/native folder");
			AssertThrows<InvalidOperationException>(() => PackageFiles.CheckMapping("Fixture.Package",new[] { "lib/net45/Health.Direct.Common.dll" },"x"),
				"Health.Direct.Common.dll, which \"Required dlls\" holds too");
			//A package entry of a lock file without its resolved version is not skipped either.
			string folder=Path.Combine(Path.GetTempPath(),"LibraryBindingTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				string pathLock=Path.Combine(folder,"packages.lock.json");
				File.WriteAllText(pathLock,"{ \"version\": 2, \"dependencies\": { \".NETFramework,Version=v4.8\": {\r\n"
					+"  \"Newtonsoft.Json\": { \"type\": \"Direct\", \"requested\": \"[13.0.4, )\", \"resolved\": \"13.0.4\", \"contentHash\": \"x\" },\r\n"
					+"  \"System.ValueTuple\": { \"type\": \"Transitive\", \"resolved\": \"4.6.2\", \"contentHash\": \"x\" },\r\n"
					+"  \"CodeBase\": { \"type\": \"Project\" } },\r\n"
					+"  \".NETFramework,Version=v4.8/win-x86\": { \"Newtonsoft.Json\": { \"type\": \"Direct\", \"requested\": \"[13.0.4, )\", \"resolved\": \"13.0.4\" } } } }");
				CollectionAssert.AreEqual(new[] { "Newtonsoft.Json/13.0.4","System.ValueTuple/4.6.2" },
					NuGetFiles.ReadLockFile(pathLock).Select(x => x.Id+"/"+x.Version).ToArray(),"Each package once; a project is not a package.");
				File.WriteAllText(pathLock,"{ \"dependencies\": { \".NETFramework,Version=v4.8\": { \"Newtonsoft.Json\": { \"type\": \"Direct\", \"contentHash\": \"x\" } } } }");
				AssertThrows<InvalidOperationException>(() => NuGetFiles.ReadLockFile(pathLock),"no resolved version");
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void PackageFiles_ReadCopiedAssets_TheRuntimeAndNativeAssetsOnly() {
			//The form the restore writes project.assets.json in: each target's packages with their compile, runtime, runtimeTargets and native
			//assets, the projects referenced, and the package folder's whole list of files under "libraries". The build copies a package's runtime
			//assets of the project's target and the native ones of an exe's win-x86 target, nothing else: not the compile assets (reference
			//assemblies when a package has them), not runtimeTargets (for .NET's runtime folders), not another framework's lib folder.
			string folder=Path.Combine(Path.GetTempPath(),"LibraryBindingTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				string pathAssets=Path.Combine(folder,"project.assets.json");
				File.WriteAllText(pathAssets,"{\r\n  \"version\": 3,\r\n  \"targets\": {\r\n"
					+"    \".NETFramework,Version=v4.8\": {\r\n"
					+"      \"Fixture.Package/1.2.3\": { \"type\": \"package\", \"dependencies\": { \"System.ValueTuple\": \"4.6.2\" },\r\n"
					+"        \"compile\": { \"ref/net462/Fixture.Package.dll\": { \"related\": \".xml\" } },\r\n"
					+"        \"runtime\": { \"lib/net462/Fixture.Package.dll\": { \"related\": \".xml\" } },\r\n"
					+"        \"runtimeTargets\": { \"runtimes/win/lib/net9.0/Fixture.Package.dll\": { \"assetType\": \"runtime\", \"rid\": \"win\" } } },\r\n"
					+"      \"System.ValueTuple/4.6.2\": { \"type\": \"package\", \"compile\": { \"lib/net471/_._\": {} }, \"runtime\": { \"lib/net471/_._\": {} } },\r\n"
					+"      \"CodeBase/1.0.0\": { \"type\": \"project\", \"framework\": \".NETFramework,Version=v4.8\", \"compile\": { \"bin/placeholder/CodeBase.dll\": {} },\r\n"
					+"        \"runtime\": { \"bin/placeholder/CodeBase.dll\": {} } } },\r\n"
					+"    \".NETFramework,Version=v4.8/win-x86\": {\r\n"
					+"      \"Fixture.Package/1.2.3\": { \"type\": \"package\", \"runtime\": { \"lib/net462/Fixture.Package.dll\": {} },\r\n"
					+"        \"native\": { \"runtimes/win-x86/native/Fixture.Native.dll\": {} } } } },\r\n"
					+"  \"libraries\": { \"Fixture.Package/1.2.3\": { \"type\": \"package\", \"path\": \"fixture.package/1.2.3\",\r\n"
					+"    \"files\": [ \"lib/net462/Fixture.Package.dll\", \"lib/netstandard2.0/Fixture.Package.dll\" ] } } }");
				Dictionary<string,HashSet<string>> dictAssets=NuGetFiles.ReadCopiedAssets(pathAssets);
				CollectionAssert.AreEquivalent(new[] { "Fixture.Package/1.2.3","System.ValueTuple/4.6.2" },dictAssets.Keys.ToArray(),"Packages only, not a project.");
				CollectionAssert.AreEquivalent(new[] { "lib/net462/Fixture.Package.dll","runtimes/win-x86/native/Fixture.Native.dll" },
					dictAssets["fixture.package/1.2.3"].ToArray(),"Both targets; NuGet's ids ignore case.");
				CollectionAssert.AreEquivalent(new[] { "lib/net471/_._" },dictAssets["System.ValueTuple/4.6.2"].ToArray());
				//A file without targets is not read as one without packages.
				File.WriteAllText(pathAssets,"{ \"version\": 3, \"targets\": { }, \"libraries\": { } }");
				AssertThrows<InvalidOperationException>(() => NuGetFiles.ReadCopiedAssets(pathAssets),"has no targets");
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		private static void AssertThrows<T>(Action action,string expectedText) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				StringAssert.Contains(ex.Message,expectedText);
				return;
			}
			Assert.Fail("No "+typeof(T).Name+" for "+expectedText);
		}

		[TestMethod]
		public void FoDicomAndItsDependencies_AreNotInRequiredDlls() {
			List<string> listFound=_arrayFoDicomOnly.Where(x => Libraries.Files.ContainsKey(x)).ToList();
			Assert.AreEqual(0,listFound.Count,"fo-dicom was dropped for the in-house DICOM reader, so these are not needed: "+string.Join(", ",listFound));
			//Nothing that stays references a DICOM library.
			List<string> listReferencing=Libraries.Assemblies.Values.Where(x => x.ListReferences.Any(y => y.Name.StartsWith("Dicom.",StringComparison.OrdinalIgnoreCase)
				|| y.Name.StartsWith("fo-dicom",StringComparison.OrdinalIgnoreCase))).Select(x => x.FileName).ToList();
			Assert.AreEqual(0,listReferencing.Count,"These reference fo-dicom: "+string.Join(", ",listReferencing));
		}
	}
}
