// The support assemblies in "Required dlls" are the versions of the library updates (lib\net462 of the NuGet packages the owner approved), the
// .NET Framework's own System.ValueTuple is used instead of a copy, and fo-dicom 4 and the packages only fo-dicom 5 needed stay out (the owner
// dropped fo-dicom for the in-house DICOM reader on 2026-11-02), and every package of the projects' lock files maps to its files there, byte for
// byte the package's own (since S2-1 of the .NET 10 move). A later package that replaces one of these files changes its line here on purpose.
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
		///<summary>File, assembly version, file version and NuGet package version of each support assembly. The tier 1 library updates use all but
		///System.Security.Cryptography.Pkcs, which waits in the folder and ships once a shipped library needs it (ReleaseListTests). The libraries
		///themselves are checked by tests/LibraryUpdateTests (LibraryFilesTests).</summary>
		private static readonly string[][] _arraySupportAssemblies=new[] {
			new[] { "Microsoft.Bcl.AsyncInterfaces.dll","10.0.0.14","10.0.1426.52326","10.0.14" },
			new[] { "Microsoft.Bcl.Cryptography.dll","10.0.0.10","10.0.1026.32716","10.0.10" },
			new[] { "Microsoft.Extensions.DependencyInjection.Abstractions.dll","8.0.0.2","8.0.1024.46610","8.0.2" },
			new[] { "Microsoft.Extensions.Logging.Abstractions.dll","8.0.0.3","8.0.1325.6609","8.0.3" },
			new[] { "System.Buffers.dll","4.0.5.0","4.600.125.16908","4.6.1" },
			new[] { "System.Diagnostics.DiagnosticSource.dll","8.0.0.1","8.0.424.16909","8.0.1" },
			new[] { "System.Formats.Asn1.dll","10.0.0.13","10.0.1326.50504","10.0.13" },
			new[] { "System.IO.Pipelines.dll","10.0.0.14","10.0.1426.52326","10.0.14" },
			new[] { "System.Memory.dll","4.0.5.0","4.600.325.20307","4.6.3" },
			new[] { "System.Numerics.Vectors.dll","4.1.6.0","4.600.125.16908","4.6.1" },
			new[] { "System.Runtime.CompilerServices.Unsafe.dll","6.0.3.0","6.100.225.20307","6.1.2" },
			new[] { "System.Security.Cryptography.Pkcs.dll","8.0.0.1","8.0.1024.46610","8.0.1" },
			new[] { "System.Threading.Tasks.Extensions.dll","4.2.4.0","4.600.325.20307","4.6.3" },
		};

		///<summary>fo-dicom 4 (Dicom.Core.dll and its native codecs Dicom.Native.dll), which the in-house DICOM reader replaced, and the packages
		///only fo-dicom 5 needed (and fo-dicom 5 itself). No library that stays uses them: MimeKit's net48 build does not reference
		///System.Text.Encoding.CodePages, and Microsoft.Extensions.Logging.Abstractions 8.0.3 needs only DependencyInjection.Abstractions.</summary>
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
				string path=Path.Combine(RepoPaths.RequiredDlls,arrayRow[0]);
				if(!File.Exists(path)) {
					listProblems.Add(arrayRow[0]+" is missing (NuGet package "+arrayRow[3]+").");
					continue;
				}
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
			List<AssemblyReference> listReferences=RequiredDlls.Assemblies.Values.SelectMany(x => x.ListReferences)
				.Where(x => x.Name=="System.ValueTuple").ToList();
			foreach(Version version in listReferences.Select(x => x.Version).Distinct()) {
				AssemblyName assemblyName=new AssemblyName("System.ValueTuple, Version="+version+", Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
				Assembly assembly=Assembly.Load(assemblyName);//the framework's own assembly, not a file of this repository
				Assert.IsTrue(assembly.GlobalAssemblyCache,"System.ValueTuple "+version+" should bind to the framework's copy, not "+assembly.Location);
			}
		}

		[TestMethod]
		public void PackagesOfTheLockFiles_MapToTheirFilesOfRequiredDlls_ByteForByte() {
			//Since S2-1 of the .NET 10 move the projects get the files of the identical packages from the offline folder, and the binding checks
			//count the files of "Required dlls" a project's lock file brings (TestSupport's PackageFiles). Every package of every lock file of the
			//solution filter is mapped there or brings none; every file mapped is in "Required dlls" and byte for byte a file of that package at
			//the locked version in the offline folder; a package that brings none has no file of a name in "Required dlls". A package that a later
			//package adds (S2-2's MySqlConnector, S2-3's Microsoft.Web.WebView2) fails here until it is mapped, instead of dropping out.
			string folderOffline=OfflinePackages.Folder;
			if(folderOffline==null) {
				BuildOutput.NotBuilt("The offline package folder (OPMS_NUGET_OFFLINE, or od-tools\\nuget-packages in the common git folder) is not there; "
					+"the build restores from it (README, Building).");
				return;
			}
			Dictionary<string,LockedPackage> dictPackages=new Dictionary<string,LockedPackage>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string,string> dictWhere=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string pathProject in SolutionProjects.ProjectPaths) {
				string pathLock=Path.Combine(Path.GetDirectoryName(pathProject),"packages.lock.json");
				if(!File.Exists(pathLock)) {
					continue;
				}
				foreach(LockedPackage lockedPackage in PackageFiles.ReadLockFile(pathLock)) {
					string key=lockedPackage.Id+"/"+lockedPackage.Version;
					if(!dictPackages.ContainsKey(key)) {
						dictPackages[key]=lockedPackage;
						dictWhere[key]=RepoPaths.Relative(pathLock);
					}
				}
			}
			Assert.IsTrue(dictPackages.Count>0,"No lock file of the solution filter names a package.");
			List<string> listProblems=new List<string>();
			int countFiles=0;
			foreach(KeyValuePair<string,LockedPackage> pair in dictPackages.OrderBy(x => x.Key,StringComparer.OrdinalIgnoreCase)) {
				LockedPackage lockedPackage=pair.Value;
				string folderPackage=OfflinePackages.GetPackageFolder(lockedPackage.Id,lockedPackage.Version);
				if(!Directory.Exists(folderPackage)) {
					listProblems.Add(pair.Key+" ("+dictWhere[pair.Key]+") is not in the offline folder.");
					continue;
				}
				string[] arrayPackageFiles=Directory.GetFiles(folderPackage,"*",SearchOption.AllDirectories);
				if(PackageFiles.BringsNoRequiredDlls(lockedPackage.Id)) {
					List<string> listNamed=arrayPackageFiles.Select(x => Path.GetFileName(x)).Where(x => RequiredDlls.Files.ContainsKey(x)).Distinct().ToList();
					if(listNamed.Count>0) {
						listProblems.Add(pair.Key+" is mapped to no file of Required dlls, but holds "+string.Join(", ",listNamed)+".");
					}
					continue;
				}
				string[] arrayFiles;
				try {
					arrayFiles=PackageFiles.GetRequiredDlls(lockedPackage.Id,dictWhere[pair.Key]);
				}
				catch(InvalidOperationException ex) {
					listProblems.Add(ex.Message);
					continue;
				}
				foreach(string fileName in arrayFiles) {
					countFiles++;
					string hash=MetadataReader.Sha256(RequiredDlls.Files[fileName]);
					if(!arrayPackageFiles.Any(x => Path.GetFileName(x).Equals(fileName,StringComparison.OrdinalIgnoreCase) && MetadataReader.Sha256(x)==hash)) {
						listProblems.Add(pair.Key+": no file of the package is byte for byte Required dlls\\"+fileName+".");
					}
				}
			}
			Console.WriteLine(dictPackages.Count+" packages in the lock files; "+countFiles+" files of Required dlls checked against the offline folder.");
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void PackageFiles_APackageItDoesNotMap_FailsLoudly() {
			//S2-3's Microsoft.Web.WebView2 brings files whose names are not its id (Microsoft.Web.WebView2.Core.dll, .WinForms.dll, .Wpf.dll,
			//WebView2Loader.dll): until it is mapped, every check that reads the projects throws instead of leaving its files out.
			AssertThrows<InvalidOperationException>(() => PackageFiles.GetRequiredDlls("Microsoft.Web.WebView2","x\\packages.lock.json"),
				"names the package Microsoft.Web.WebView2");
			CollectionAssert.AreEqual(new[] { "Renci.SshNet.dll" },PackageFiles.GetRequiredDlls("ssh.net","x"),"NuGet's ids ignore case.");
			CollectionAssert.AreEqual(new string[0],PackageFiles.GetRequiredDlls("System.ValueTuple","x"));
			//A mapping to no file, or to one "Required dlls" does not hold, fails the same way.
			AssertThrows<InvalidOperationException>(() => PackageFiles.CheckMapping("Fixture.Package",new string[0],"x"),"to no file");
			AssertThrows<InvalidOperationException>(() => PackageFiles.CheckMapping("Fixture.Package",new[] { "Newtonsoft.Json.dll","Fixture.NotThere.dll" },"x"),
				"Fixture.NotThere.dll, which \"Required dlls\" does not hold");
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
					PackageFiles.ReadLockFile(pathLock).Select(x => x.Id+"/"+x.Version).ToArray(),"Each package once; a project is not a package.");
				File.WriteAllText(pathLock,"{ \"dependencies\": { \".NETFramework,Version=v4.8\": { \"Newtonsoft.Json\": { \"type\": \"Direct\", \"contentHash\": \"x\" } } } }");
				AssertThrows<InvalidOperationException>(() => PackageFiles.ReadLockFile(pathLock),"no resolved version");
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
			List<string> listFound=_arrayFoDicomOnly.Where(x => RequiredDlls.Files.ContainsKey(x)).ToList();
			Assert.AreEqual(0,listFound.Count,"fo-dicom was dropped for the in-house DICOM reader, so these are not needed: "+string.Join(", ",listFound));
			//Nothing that stays references a DICOM library.
			List<string> listReferencing=RequiredDlls.Assemblies.Values.Where(x => x.ListReferences.Any(y => y.Name.StartsWith("Dicom.",StringComparison.OrdinalIgnoreCase)
				|| y.Name.StartsWith("fo-dicom",StringComparison.OrdinalIgnoreCase))).Select(x => x.FileName).ToList();
			Assert.AreEqual(0,listReferencing.Count,"These reference fo-dicom: "+string.Join(", ",listReferencing));
		}
	}
}
