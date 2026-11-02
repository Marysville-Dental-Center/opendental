// Tests that Dropbox A to Z storage stays removed (docs/specs/vendor-direct/dropbox.md, owner decision 2026-10-18): no Dropbox client, SDK,
// setup window or host is left, the schema-compatibility values stay, and a database that still names Dropbox fails safely without a network call.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Dropbox {
	[TestClass]
	public class DropboxRemovedTests {
		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		private static Assembly GetCloudAssembly() {
			string name=Path.GetFileNameWithoutExtension(CodeBase.ProgramNames.CloudDllFileName);
			AssemblyName assemblyName=typeof(CloudStorage).Assembly.GetReferencedAssemblies().First(x => x.Name==name);
			return Assembly.Load(assemblyName);
		}

		[TestMethod]
		public void CloudProject_HasNoDropboxClientOrSdk() {
			Assembly assemblyCloud=GetCloudAssembly();
			//The namespace of the SFTP client, which stays: taken from the assembly, so the absence check cannot pass only because it was renamed.
			string nsCloud=assemblyCloud.GetTypes().Single(x => x.Name=="Sftp" && !x.IsNested).Namespace;
			Assert.IsNull(assemblyCloud.GetType(nsCloud+".Dropbox"),"The Dropbox client should have been removed.");
			Assert.IsFalse(assemblyCloud.GetReferencedAssemblies().Any(x => x.Name.StartsWith("Dropbox",StringComparison.OrdinalIgnoreCase)),
				"OpenPmsDentalStorage should not reference the Dropbox SDK.");
			Assert.IsFalse(typeof(CloudStorage).Assembly.GetReferencedAssemblies().Any(x => x.Name.StartsWith("Dropbox",StringComparison.OrdinalIgnoreCase)));
			//The project stays for SFTP A to Z storage.
			Assert.IsNotNull(assemblyCloud.GetType(nsCloud+".Sftp"));
			string csproj=ReadRepoFile(@"OpenPmsDentalStorage\OpenPmsDentalStorage.csproj");
			Assert.IsFalse(csproj.Contains("Dropbox"),"OpenPmsDentalStorage.csproj should not mention Dropbox.");
			Assert.IsFalse(File.Exists(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),@"OpenPmsDentalStorage\Storage\Dropbox.cs")));
		}

		[TestMethod]
		public void BusinessDropboxClass_HasNoTokenOrAuthorizationMembers() {
			List<string> listMembers=typeof(OpenPmsDentalBusiness.Dropbox).GetMembers(BindingFlags.Public|BindingFlags.Static).Select(x => x.Name).ToList();
			foreach(string name in new[] { "AccessToken","GetDropboxAuthorizationUrl","GetDropboxAccessToken" }) {
				Assert.IsFalse(listMembers.Contains(name),"Dropbox."+name+" should have been removed.");
			}
		}

		[TestMethod]
		public void SchemaCompatibilityValues_AreKept() {
			Assert.AreEqual(2,(int)DataStorageType.DropboxAtoZ,"Stored AtoZfolderUsed values keep their meaning.");
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"Dropbox"),"The Dropbox program row stays.");
			Assert.AreEqual(0,(int)OAuthApplicationNames.Dropbox);
			Assert.AreEqual("Dropbox AtoZ Path",OpenPmsDentalBusiness.Dropbox.PropertyDescs.AtoZPath);
			Assert.AreEqual("Dropbox API Token",OpenPmsDentalBusiness.Dropbox.PropertyDescs.AccessToken);
		}

		[TestMethod]
		public void SetupWindows_HaveNoDropboxOption() {
			string root=FoundationLayoutTests.GetRepositoryRoot();
			Assert.IsFalse(File.Exists(Path.Combine(root,@"OpenPmsDental\Forms\FormDropboxAuthorize.cs")));
			Assert.IsFalse(ReadRepoFile(@"OpenPmsDental\OpenPmsDental.csproj").Contains("FormDropboxAuthorize"));
			foreach(string file in new[] { @"OpenPmsDental\Forms\FormPath.cs",@"OpenPmsDental\Forms\FormPath.designer.cs" }) {
				string text=ReadRepoFile(file);
				foreach(string removed in new[] { "radioDropboxStorage","FormDropboxAuthorize","butAuthorize","textAccessToken","OpenPmsDentalStorage.Dropbox" }) {
					Assert.IsFalse(text.Contains(removed),file+" should not contain "+removed);
				}
			}
			foreach(string file in new[] { @"OpenPmsDental\Forms\FormLicense.cs",@"OpenPmsDental\Forms\FormRegistrationKey.cs" }) {
				Assert.IsFalse(ReadRepoFile(file).Contains("Dropbox"),file+" should not list a Dropbox licence.");
			}
			string startupCheck=ReadRepoFile(@"OpenPmsDental\Logic\VendorDirect\DropboxStartupCheck.cs");
			Assert.IsFalse(startupCheck.Contains("OpenPmsDentalStorage"),"The startup check must not contact Dropbox.");
			StringAssert.Contains(startupCheck,"Dropbox.StorageRemovedMessage");
		}

		[TestMethod]
		public void Source_NamesNoDropboxHost() {
			string root=FoundationLayoutTests.GetRepositoryRoot();
			List<string> listHits=new List<string>();
			foreach(string project in new[] { "OpenPmsDental","OpenPmsDentalBusiness","OpenPmsDentalStorage","CodeBase","WpfControlsOD","OpenPmsDentalHL7" }) {
				string folder=Path.Combine(root,project);
				if(!Directory.Exists(folder)) {
					continue;
				}
				foreach(string file in Directory.EnumerateFiles(folder,"*.cs",SearchOption.AllDirectories)) {
					string relative=file.Substring(root.Length);
					if(relative.Contains(@"\bin\") || relative.Contains(@"\obj\")) {
						continue;
					}
					string text=File.ReadAllText(file);
					if(text.IndexOf("dropboxapi.com",StringComparison.OrdinalIgnoreCase)>=0
						|| text.IndexOf("dropbox.com/oauth2",StringComparison.OrdinalIgnoreCase)>=0
						|| text.IndexOf("using Dropbox.Api",StringComparison.Ordinal)>=0)
					{
						listHits.Add(relative);
					}
				}
			}
			Assert.AreEqual(0,listHits.Count,"Dropbox hosts or SDK still named in: "+string.Join(", ",listHits));
		}

		[TestMethod]
		public void CloudStorage_WithDropboxSelected_FailsWithoutNetworkAndKeepsTheNoThrowContract() {
			bool wasCacheNull=Prefs.DictIsNull();
			DataTable tableSaved=wasCacheNull ? null : Prefs.GetTableFromCache(false);
			try {
				Prefs.FillCacheFromTable(OpenPmsDentalBusiness.Crud.PrefCrud.ListToTable(new List<Pref> {
					new Pref { PrefNum=1,PrefName=PrefName.AtoZfolderUsed.ToString(),ValueString=((int)DataStorageType.DropboxAtoZ).ToString() }
				}));
				Assert.IsTrue(OpenPmsDentalBusiness.Dropbox.IsStorageSelected);
				//Callers still take the cloud branch, so they never mistake the old Dropbox paths for local folders.
				Assert.IsTrue(CloudStorage.IsCloudStorage);
				Assert.AreEqual('/',CloudStorage.DirectorySeparatorChar);
				Assert.IsFalse(CloudStorage.FileExists("/AtoZ/A"));
				Assert.IsFalse(CloudStorage.Move("/AtoZ/A/from.jpg","/AtoZ/A/to.jpg"));
				Assert.AreEqual(0,CloudStorage.ListFolderContents("/AtoZ/A").Count);
				byte[] byteArray=CloudStorage.Download("/AtoZ/A","file.jpg");
				Assert.IsTrue(byteArray==null || byteArray.All(x => x==0),"Nothing is downloaded.");
				CloudStorage.Upload("/AtoZ/A","file.jpg",new byte[] { 1,2,3 });//No exception unless asked for.
				CloudStorage.Delete("/AtoZ/A/file.jpg");
				CloudStorage.Copy("/AtoZ/A/from.jpg","/AtoZ/A/to.jpg");
				Exception exception=null;
				try {
					CloudStorage.Upload("/AtoZ/A","file.jpg",new byte[] { 1,2,3 },hasExceptions:true);
				}
				catch(Exception ex) {
					exception=ex;
				}
				Assert.IsNotNull(exception,"hasExceptions:true reports the failure.");
				Assert.AreEqual("OpenPmsDentalStorage.StorageRemovedException",exception.GetType().FullName);
				Assert.AreEqual(OpenPmsDentalBusiness.Dropbox.StorageRemovedMessage,exception.Message);
				exception=null;
				try {
					CloudStorage.Move("/AtoZ/A/from.jpg","/AtoZ/A/to.jpg",hasExceptions:true);
				}
				catch(Exception ex) {
					exception=ex;
				}
				Assert.IsNotNull(exception);
			}
			finally {
				if(wasCacheNull) {
					Prefs.ClearCache();
				}
				else {
					Prefs.FillCacheFromTable(tableSaved);
				}
			}
		}
	}
}
