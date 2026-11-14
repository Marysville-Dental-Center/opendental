// Tests of NuGetFiles (S2-4 of the .NET 10 move, docs/specs/net10-plan.md): the files a project's restore copies from its packages, each of the
// package at the version the project's lock file names, found in a package folder; a restore that disagrees with the lock file, and two packages
// that bring a file of one name, throw instead of being read as fewer files. Synthetic files in a scratch folder under the temp folder.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class NuGetFilesTests {
		private const string LOCK_FILE="{\r\n  \"version\": 2,\r\n  \"dependencies\": {\r\n    \".NETFramework,Version=v4.8\": {\r\n"
			+"      \"Fixture.Package\": { \"type\": \"Direct\", \"requested\": \"[1.2.3, )\", \"resolved\": \"1.2.3\", \"contentHash\": \"aGFzaA==\" },\r\n"
			+"      \"System.ValueTuple\": { \"type\": \"Transitive\", \"resolved\": \"4.6.2\", \"contentHash\": \"dnQ=\" },\r\n"
			+"      \"CodeBase\": { \"type\": \"Project\" } },\r\n"
			+"    \".NETFramework,Version=v4.8/win-x86\": { \"Fixture.Package\": { \"type\": \"Direct\", \"requested\": \"[1.2.3, )\", \"resolved\": \"1.2.3\", \"contentHash\": \"aGFzaA==\" } } } }";

		///<summary>project.assets.json in the form the restore writes it, with the package folder given (JSON-escaped).</summary>
		private static string AssetsFile(string folderPackages,string versionFixture,string nativeName) {
			return "{\r\n  \"version\": 3,\r\n  \"targets\": {\r\n"
				+"    \".NETFramework,Version=v4.8\": {\r\n"
				+"      \"Fixture.Package/"+versionFixture+"\": { \"type\": \"package\", \"compile\": { \"ref/net462/Fixture.Package.dll\": {} },\r\n"
				+"        \"runtime\": { \"lib/net462/Fixture.Package.dll\": {} } },\r\n"
				+"      \"System.ValueTuple/4.6.2\": { \"type\": \"package\", \"compile\": { \"lib/net471/_._\": {} }, \"runtime\": { \"lib/net471/_._\": {} } },\r\n"
				+"      \"CodeBase/1.0.0\": { \"type\": \"project\", \"runtime\": { \"bin/placeholder/CodeBase.dll\": {} } } },\r\n"
				+"    \".NETFramework,Version=v4.8/win-x86\": {\r\n"
				+"      \"Fixture.Package/"+versionFixture+"\": { \"type\": \"package\", \"runtime\": { \"lib/net462/Fixture.Package.dll\": {} },\r\n"
				+"        \"native\": { \"runtimes/win-x86/native/"+nativeName+"\": {} } } } },\r\n"
				+"  \"libraries\": { },\r\n"
				+"  \"packageFolders\": { \""+folderPackages.Replace("\\","\\\\")+"\\\\\": {} } }";
		}

		[TestMethod]
		public void ReadCopiedFiles_TheLockedVersionsAssets_InThePackageFolder() {
			string folder=Path.Combine(Path.GetTempPath(),"ProjectFileTests_NuGet_"+Guid.NewGuid().ToString("N"));
			string folderProject=Path.Combine(folder,"Project");
			string folderPackages=Path.Combine(folder,"packages");
			Directory.CreateDirectory(Path.Combine(folderProject,"obj"));
			try {
				//No lock file: no package; a lock file without a restore: not known yet.
				Assert.AreEqual(0,NuGetFiles.ReadCopiedFiles(folder,folderProject).Count);
				File.WriteAllText(Path.Combine(folderProject,"packages.lock.json"),LOCK_FILE);
				Assert.IsNull(NuGetFiles.ReadCopiedFiles(folder,folderProject));
				CollectionAssert.AreEqual(new[] { "Fixture.Package/1.2.3/aGFzaA==","System.ValueTuple/4.6.2/dnQ=" },
					NuGetFiles.ReadLockFile(Path.Combine(folderProject,"packages.lock.json")).Select(x => x.Id+"/"+x.Version+"/"+x.ContentHash).ToArray(),
					"Each package once, with its content hash; a project is not a package.");
				string pathAssets=Path.Combine(folderProject,"obj","project.assets.json");
				File.WriteAllText(pathAssets,AssetsFile(folderPackages,"1.2.3","Fixture.Native.dll"));
				CollectionAssert.AreEqual(new[] { folderPackages+"\\" },NuGetFiles.ReadPackageFolders(pathAssets).ToArray());
				//The package is in no package folder yet: its files are known, their path is not.
				Dictionary<string,PackageFile> dictFiles=NuGetFiles.ReadCopiedFiles(folder,folderProject);
				CollectionAssert.AreEquivalent(new[] { "Fixture.Package.dll","Fixture.Native.dll" },dictFiles.Keys.ToArray(),"Runtime and native assets; no _._.");
				Assert.IsNull(dictFiles["fixture.package.dll"].Path,"File names ignore case; no folder holds the package.");
				//In the restore's package folder (the fixture id is in no offline folder).
				string folderFixture=Path.Combine(folderPackages,"fixture.package","1.2.3");
				Directory.CreateDirectory(folderFixture);
				dictFiles=NuGetFiles.ReadCopiedFiles(folder,folderProject);
				PackageFile packageFile=dictFiles["Fixture.Package.dll"];
				Assert.AreEqual("Fixture.Package",packageFile.Id);
				Assert.AreEqual("1.2.3",packageFile.Version);
				Assert.AreEqual("lib/net462/Fixture.Package.dll",packageFile.Asset);
				Assert.AreEqual(Path.Combine(folderFixture,"lib","net462","Fixture.Package.dll"),packageFile.Path);
				Assert.AreEqual(Path.Combine(folderFixture,"runtimes","win-x86","native","Fixture.Native.dll"),dictFiles["Fixture.Native.dll"].Path);
				//A restore of another version than the lock file's is not read as the locked package.
				File.WriteAllText(pathAssets,AssetsFile(folderPackages,"1.2.4","Fixture.Native.dll"));
				AssertThrows(() => NuGetFiles.ReadCopiedFiles(folder,folderProject),"has the package Fixture.Package/1.2.4");
				//Two files of one name from different assets: which one a folder gets is not decided by a test.
				File.WriteAllText(pathAssets,AssetsFile(folderPackages,"1.2.3","Fixture.Package.dll"));
				AssertThrows(() => NuGetFiles.ReadCopiedFiles(folder,folderProject),"two package files are named Fixture.Package.dll");
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void GetPackageFolder_IsTheLowerCaseIdAndVersion_AsNuGetLaysItOut() {
			Assert.AreEqual(@"C:\packages\microsoft.web.webview2\1.0.2420.47",NuGetFiles.GetPackageFolder(@"C:\packages","Microsoft.Web.WebView2","1.0.2420.47"));
		}

		private static void AssertThrows(Action action,string expectedText) {
			try {
				action();
			}
			catch(InvalidOperationException ex) {
				StringAssert.Contains(ex.Message,expectedText);
				return;
			}
			Assert.Fail("No InvalidOperationException for "+expectedText);
		}
	}
}
