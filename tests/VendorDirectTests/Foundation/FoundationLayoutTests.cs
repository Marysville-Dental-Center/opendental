// Tests that the vendor-direct scaffolding (README section 3) stays in place: the program names, the project wildcards, the business-layer stubs
// that later packages fill in, and the registry and job host. Files that later packages own are tested by those packages.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class FoundationLayoutTests {
		///<summary>The repository root: the first folder above this assembly that holds OpenDental.sln.</summary>
		public static string GetRepositoryRoot() {
			string path=Path.GetDirectoryName(typeof(FoundationLayoutTests).Assembly.Location);
			while(path!=null) {
				if(File.Exists(Path.Combine(path,"OpenDental.sln"))) {
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
			CollectionAssert.AreEqual(new[] { "Microsoft365Email","GmailApi","USPSAddresses","PostcardMania" },
				arrayNames.Skip(arrayNames.Length-4).ToArray());
			Assert.AreEqual((int)ProgramName.ZImage+1,(int)ProgramName.Microsoft365Email,"Existing values keep their numbers.");
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
			string csproj=ReadRepoFile(@"OpenDentBusiness\OpenDentBusiness.csproj");
			foreach(string folder in new[] { @"Bridges\QuickBooksOnline",@"Bridges\CareCredit",@"Bridges\PostcardMania",@"WebBridges\Microsoft365",
				@"WebBridges\Usps",@"WebBridges\Erx\DoseSpot","VendorDirect" })
			{
				StringAssert.Contains(csproj,"<Compile Include=\""+folder+@"\**\*.cs"" />",folder);
			}
			StringAssert.Contains(csproj,@"..\Required dlls\Microsoft.Identity.Client.dll");
		}

		[TestMethod]
		public void Stubs_ExistAndAreCompiled() {
			string root=GetRepositoryRoot();
			string csprojBusiness=ReadRepoFile(@"OpenDentBusiness\OpenDentBusiness.csproj");
			foreach(string stub in new[] { @"WebBridges\Google\GoogleTokenCache.cs",@"WebBridges\Google\GoogleOAuthSettings.cs",
				@"WebBridges\Erx\NewCrop\NewCropConfig.cs",@"WebBridges\PDMP\PdmpHttp.cs",@"WebBridges\PDMP\PdmpCertificate.cs",
				@"WebBridges\PaySimpleAchStatusSync.cs" })
			{
				Assert.IsTrue(File.Exists(Path.Combine(root,"OpenDentBusiness",stub)),stub);
				StringAssert.Contains(csprojBusiness,"<Compile Include=\""+stub+"\" />",stub);
			}
		}

		[TestMethod]
		public void ProgramProject_HasTheRegistryAndJobHost() {
			//Only the foundation's own files. The forms and helpers that later packages fill in, rename or delete are theirs to test.
			string root=GetRepositoryRoot();
			string csprojProgram=ReadRepoFile(@"OpenDental\OpenDental.csproj");
			foreach(string file in new[] { @"Logic\VendorDirect\IProgramLinkSetup.cs",@"Logic\VendorDirect\ProgramLinkSetupRegistry.cs",
				@"Logic\VendorDirect\VendorJobHost.cs" })
			{
				Assert.IsTrue(File.Exists(Path.Combine(root,"OpenDental",file)),file);
				StringAssert.Contains(csprojProgram,"<Compile Include=\""+file+"\" />",file);
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
