// GetFilesNotCompiledByWildcard on the fixtures (package S1-T2 of the .NET 10 plan): the check of the vendor folders that "a file added to the
// folder needs no line of its own in the project file". It holds for a classic folder wildcard (VendorDirect\**\*.cs) and for the SDK's default
// glob, and fails when the classic file lists the folder's files one by one or an SDK-style file names one of them again.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class FolderWildcardTests {
		[TestMethod]
		public void AFolderWildcard_AndTheDefaultGlob_CompileTheWholeFolder() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				foreach(string name in new[] { "Classic","SdkStyle" }) {
					ProjectFile projectFile=fixtureFolder.Load(name);
					CollectionAssert.AreEqual(new string[0],projectFile.GetFilesNotCompiled("VendorDirect"),name);
					CollectionAssert.AreEqual(new string[0],projectFile.GetFilesNotCompiledByWildcard("VendorDirect"),name);
				}
			}
		}

		[TestMethod]
		public void AFolderListedFileByFile_IsCompiledButNotByAWildcard() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("Classic","<Compile Include=\"VendorDirect\\**\\*.cs\" />",
					"<Compile Include=\"VendorDirect\\Lab\\LabClient.cs\" /><Compile Include=\"VendorDirect\\Lab\\Sub\\LabHelper.cs\" />");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				//Every file is still compiled, so the old check passes; the new one names the files that have a line of their own.
				CollectionAssert.AreEqual(new string[0],projectFile.GetFilesNotCompiled("VendorDirect"));
				CollectionAssert.AreEquivalent(new[] { @"VendorDirect\Lab\LabClient.cs",@"VendorDirect\Lab\Sub\LabHelper.cs" },
					projectFile.GetFilesNotCompiledByWildcard("VendorDirect"));
				//The fixture's Forms folder is listed file by file too.
				CollectionAssert.AreEquivalent(new[] { @"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs" },projectFile.GetFilesNotCompiledByWildcard("Forms"));
				//A file the wildcard's Exclude leaves out is not compiled at all: it is named as well.
				CollectionAssert.AreEqual(new[] { @"Old\Skip.cs" },projectFile.GetFilesNotCompiledByWildcard("Old"));
			}
		}

		[TestMethod]
		public void Sdk_AFileNamedAgain_IsNotCompiledByTheGlobAlone() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//An explicit Include of a file the default glob compiles: two items (the SDK stops with NETSDK1022), and a line of its own.
				fixtureFolder.EditProject("SdkStyle","<Compile Remove=\"Removed\\**\" />","<Compile Remove=\"Removed\\**\" /><Compile Include=\"VendorDirect\\Lab\\LabClient.cs\" />");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				CollectionAssert.AreEqual(new[] { @"VendorDirect\Lab\LabClient.cs" },projectFile.GetFilesNotCompiledByWildcard("VendorDirect"));
				//An Update gives metadata without a line of its own for the file: still the glob's item.
				fixtureFolder.EditProject("SdkStyle","<Compile Include=\"VendorDirect\\Lab\\LabClient.cs\" />","<Compile Update=\"VendorDirect\\Lab\\LabClient.cs\" SubType=\"Code\" />");
				CollectionAssert.AreEqual(new string[0],fixtureFolder.Load("SdkStyle").GetFilesNotCompiledByWildcard("VendorDirect"));
				//A removed file is not compiled at all.
				fixtureFolder.EditProject("SdkStyle","<Compile Remove=\"Removed\\**\" />","<Compile Remove=\"Removed\\**;VendorDirect\\Lab\\Sub\\**\" />");
				CollectionAssert.AreEqual(new[] { @"VendorDirect\Lab\Sub\LabHelper.cs" },fixtureFolder.Load("SdkStyle").GetFilesNotCompiledByWildcard("VendorDirect"));
			}
		}
	}
}
