// Tests that the vendor-direct scaffolding (README section 3) stays in place: the program names, the project wildcards, the business-layer stubs
// that later packages fill in, and the registry and job host. Files that later packages own are tested by those packages.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using ProjectFileTests;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class FoundationLayoutTests {
		///<summary>The repository root: the first folder above this assembly that holds LICENSE and NOTICE.</summary>
		public static string GetRepositoryRoot() {
			string path=Path.GetDirectoryName(typeof(FoundationLayoutTests).Assembly.Location);
			while(path!=null) {
				if(File.Exists(Path.Combine(path,"LICENSE")) && File.Exists(Path.Combine(path,"NOTICE"))) {
					return path;
				}
				path=Path.GetDirectoryName(path);
			}
			throw new InvalidOperationException("Repository root not found.");
		}

		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(GetRepositoryRoot(),relativePath));
		}

		[TestMethod]
		public void ProgramName_VendorDirectNamesAreAppendedLast() {
			string[] arrayNames=Enum.GetNames(typeof(ProgramName));
			//Later program links are appended after these four (V-BRIDGE1), so check the block that follows ZImage.
			CollectionAssert.AreEqual(new[] { "Microsoft365Email","GmailApi","USPSAddresses","PostcardMania" },
				arrayNames.Skip(Array.IndexOf(arrayNames,"ZImage")+1).Take(4).ToArray());
			Assert.AreEqual((int)ProgramName.ZImage+1,(int)ProgramName.Microsoft365Email,"Existing values keep their numbers.");
			Assert.IsTrue((int)ProgramName.MeetInTheCloud>(int)ProgramName.PostcardMania,"Global Payments V2 is appended after the F0 names.");
		}

		[TestMethod]
		public void OAuthApplicationNames_KeepTheirOrdinals() {
			Assert.AreEqual(0,(int)OAuthApplicationNames.Dropbox);
			Assert.AreEqual(2,(int)OAuthApplicationNames.QuickBooksOnline);
			Assert.AreEqual(9,(int)OAuthApplicationNames.MedispanClientID);
			Assert.AreEqual(10,Enum.GetNames(typeof(OAuthApplicationNames)).Length);
		}

		[TestMethod]
		public void BusinessProject_HasTheVendorFolderWildcards() {
			//Each vendor folder is compiled whole: by its wildcard in the classic project file, by the default glob in an SDK-style one. The
			//evaluated items, so the check holds for both. A wildcard compiles each file, so a file added to the folder needs no line of its own
			//in the project file (a classic file that listed the files one by one would compile them all and still fail the second check).
			ProjectFile projectFile=ProjectFile.Load(Path.Combine(GetRepositoryRoot(),@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj"));
			foreach(string folder in new[] { @"Bridges\QuickBooksOnline",@"Bridges\CareCredit",@"Bridges\PostcardMania",@"WebBridges\Microsoft365",
				@"WebBridges\Usps",@"WebBridges\Erx\DoseSpot","VendorDirect" })
			{
				System.Collections.Generic.List<string> listNotCompiled=projectFile.GetFilesNotCompiled(folder);
				Assert.AreEqual(0,listNotCompiled.Count,folder+": not compiled: "+string.Join(", ",listNotCompiled));
				System.Collections.Generic.List<string> listNotByWildcard=projectFile.GetFilesNotCompiledByWildcard(folder);
				Assert.AreEqual(0,listNotByWildcard.Count,folder+": not compiled by a wildcard: "+string.Join(", ",listNotByWildcard));
			}
			//MSAL: the package of Directory.Packages.props, whose file is the one Required dlls held (S2-1 of the .NET 10 move).
			Assert.AreEqual("4.92.0",projectFile.GetPackageVersion("Microsoft.Identity.Client"));
		}

		[TestMethod]
		public void Stubs_ExistAndAreCompiled() {
			string root=GetRepositoryRoot();
			ProjectFile projectFileBusiness=ProjectFile.Load(Path.Combine(root,@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj"));
			foreach(string stub in new[] { @"WebBridges\Google\GoogleTokenCache.cs",@"WebBridges\Google\GoogleOAuthSettings.cs",
				@"WebBridges\Erx\NewCrop\NewCropConfig.cs",@"WebBridges\PDMP\PdmpHttp.cs",@"WebBridges\PDMP\PdmpCertificate.cs",
				@"WebBridges\PaySimpleAchStatusSync.cs" })
			{
				Assert.IsTrue(File.Exists(Path.Combine(root,"OpenPmsDentalBusiness",stub)),stub);
				projectFileBusiness.AssertCompiles(stub);
			}
		}

		[TestMethod]
		public void ProgramProject_HasTheRegistryAndJobHost() {
			//Only the foundation's own files. The forms and helpers that later packages fill in, rename or delete are theirs to test.
			string root=GetRepositoryRoot();
			ProjectFile projectFileProgram=ProjectFile.Load(Path.Combine(root,@"OpenPmsDental\OpenPmsDental.csproj"));
			foreach(string file in new[] { @"Logic\VendorDirect\IProgramLinkSetup.cs",@"Logic\VendorDirect\ProgramLinkSetupRegistry.cs",
				@"Logic\VendorDirect\VendorJobHost.cs" })
			{
				Assert.IsTrue(File.Exists(Path.Combine(root,"OpenPmsDental",file)),file);
				projectFileProgram.AssertCompiles(file);
			}
		}

		[TestMethod]
		public void HostCheck_AllowsOnlyTheBlockedHostListFiles() {
			string script=ReadRepoFile(@"tools\Test-NoHqHosts.ps1");
			StringAssert.Contains(script,@"CodeBase\Utilities\VendorDirect\HqHosts.cs");
			StringAssert.Contains(script,@"tests\VendorDirectTests\Foundation\HqHostsTests.cs");
			Assert.IsFalse(script.Contains(@"tests\VendorDirectTests\*"),"Allow single files, not the whole test project.");
		}

		[TestMethod]
		public void BusinessHelpers_HaveTheirMiddleTierEntryPoints() {
			//Middle Tier forwards these by method name, so they must exist exactly once (no overloads).
			foreach(string name in new[] { "InsertIfMissing","GetOneFromDb","UpdateValueIfUnchanged","GetValOrDefault" }) {
				Assert.AreEqual(1,typeof(ProgramProperties).GetMethods(BindingFlags.Public|BindingFlags.Static).Count(x => x.Name==name),name);
			}
			Assert.AreEqual(1,typeof(Programs).GetMethods(BindingFlags.Public|BindingFlags.Static).Count(x => x.Name=="InsertIfMissing"));
		}
	}
}
