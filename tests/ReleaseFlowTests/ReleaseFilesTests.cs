// Checks for package REL-FILES (docs/port-notes/REL-FILES.md): the shared assembly info of every shipped assembly, the version numbers it must
// not change, the native files the program needs next to it, and the code that used to read the release version as a version number.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace ReleaseFlowTests {
	[TestClass]
	public class ReleaseFilesTests {
		///<summary>Every shipped project: its folder, project file, and the AssemblyVersion and AssemblyFileVersion (null when the project sets none)
		///it had before the shared assembly info. These stay as they are: 24.3.63.0 is the compatibility version.</summary>
		private static readonly List<ShippedProject> _listShippedProjects=new List<ShippedProject>() {
			new ShippedProject("CentralManager","xCentralManager.csproj","1.0.0.0","1.0.0.0"),
			new ShippedProject("CodeBase","xCodeBase.csproj","24.3.63.0",null),
			new ShippedProject("DatabaseIntegrityCheck","xDatabaseIntegrityCheck.csproj","1.0.0.0","1.0.0.0"),
			new ShippedProject("DataConnectionBase","xDataConnectionBase.csproj","24.3.63.0",null),
			new ShippedProject("ImagingCapture","xImagingCapture.csproj","24.3.63.0",null),
			new ShippedProject("ImagingDeviceManager","xImagingDeviceManager.csproj","24.3.63.0",null),
			new ShippedProject("MySQLManager","xMySQLManager.csproj","24.3.63.0",null),
			new ShippedProject("ODCrypt","xODCrypt.csproj","24.3.63.0","24.3.63.0"),
			//Built from source and shipped since package RDL-SRC; RDL report definitions load it by file name, so its version is never compared.
			new ShippedProject("ODR","xODR.csproj","4.6.1",null),
			new ShippedProject("OpenPmsDental","OpenPmsDental.csproj","24.3.63.0",null),
			new ShippedProject("OpenPmsDentalGraph","OpenPmsDentalGraph.csproj","16.1.1.0","16.1.1.0"),
			new ShippedProject("OpenPmsDentalServer","OpenPmsDentalServer.csproj","24.3.63.0",null),
			new ShippedProject("OpenPmsDentalStorage","OpenPmsDentalStorage.csproj","16.4.0.0","1.0.0.0"),
			new ShippedProject("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj","24.3.63.0",null),
			new ShippedProject("OpenPmsDentalHL7","OpenPmsDentalHL7.csproj","24.3.63.0",null),
			new ShippedProject("PasswordVaultWrapper","PasswordVaultWrapper.csproj","1.0.0.0","1.0.0.0"),
			new ShippedProject("ServiceManager","ServiceManager.csproj","15.4.1.0","15.4.1.0"),
			new ShippedProject("SparksToothChart","SparksToothChart.csproj","4.7.1.0",null),
			new ShippedProject("UpdateFileCopier","UpdateFileCopier.csproj","24.3.63.0","13.1.0.0"),
			new ShippedProject("WpfControlsOD","WpfControlsOD.csproj","1.0.0.0","1.0.0.0"),
		};

		///<summary>The program's own assemblies in OpenPmsDental's output folder, which carry the shared assembly info.</summary>
		private static readonly string[] _arrayBuiltAssemblies=new string[] {
			"OpenPmsDental.exe","OpenPmsDentalBusiness.dll","CodeBase.dll","DataConnectionBase.dll","WpfControlsOD.dll","OpenPmsDentalGraph.dll",
			"OpenPmsDentalStorage.dll","SparksToothChart.dll","ODCrypt.dll","PasswordVaultWrapper.dll","ImagingCapture.dll","ImagingDeviceManager.dll",
			"ServiceManager.exe","UpdateFileCopier.exe","ODR.dll",
		};

		///<summary>The native x86 files the program loads at run time that no reference copies: the WebView2 package's loader (since S2-3 of the
		///.NET 10 move; until then a None item of Required dlls, which keeps the same bytes until S2-4).</summary>
		private static readonly string[] _arrayRuntimeDlls=new string[] { "WebView2Loader.dll" };

		[TestMethod]
		public void SharedAssemblyInfo_NamesTheProductTheReleaseAndBothCopyrights() {
			string text=StripComments(TestPaths.ReadSource("SharedAssemblyInfo.cs"));
			StringAssert.Contains(text,"[assembly: AssemblyProduct(\"Open PMS - Dental\")]");
			StringAssert.Contains(text,"[assembly: AssemblyInformationalVersion(\"0.0\")]");
			Match matchCopyright=Regex.Match(text,@"\[assembly: AssemblyCopyright\((.*?)\)\]",RegexOptions.Singleline);
			Assert.IsTrue(matchCopyright.Success,"AssemblyCopyright");
			string copyright=string.Concat(Regex.Matches(matchCopyright.Groups[1].Value,"\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>().Select(x => x.Groups[1].Value));
			StringAssert.Contains(copyright,"Jordan Sparks, D.M.D., Open Dental Software, Inc. and contributors","Open Dental's copyright is kept.");
			StringAssert.Contains(copyright,"Open PMS - Dental contributors","The fork's copyright is added.");
			StringAssert.Contains(copyright,"GPL v2 or later");
			Match matchCompany=Regex.Match(text,"\\[assembly: AssemblyCompany\\(\"([^\"]*)\"\\)\\]");
			Assert.IsTrue(matchCompany.Success);
			Assert.IsFalse(matchCompany.Groups[1].Value.Contains("Microsoft"));
			//Version numbers never come from the shared file: they are the compatibility line of each project.
			Assert.IsFalse(text.Contains("AssemblyVersion("),"No AssemblyVersion in the shared file.");
			Assert.IsFalse(text.Contains("AssemblyFileVersion("),"No AssemblyFileVersion in the shared file.");
		}

		[TestMethod]
		public void SharedAssemblyInfo_IsLinkedIntoEveryShippedProject_WhichKeepsItsVersions() {
			foreach(ShippedProject shippedProject in _listShippedProjects) {
				//The evaluated items, so the check holds for the classic project files and the SDK-style ones of the .NET 10 move alike.
				ProjectFile projectFile=ProjectFile.Load(Path.Combine(TestPaths.RepoRoot,shippedProject.Folder,shippedProject.ProjectFile));
				Assert.AreEqual(@"Properties\SharedAssemblyInfo.cs",projectFile.AssertItem("Compile",@"..\SharedAssemblyInfo.cs").GetMetadata("Link"),shippedProject.Folder);
				string assemblyInfo=TestPaths.ReadSource(shippedProject.Folder,"Properties","AssemblyInfo.cs");
				foreach(string attribute in new[] { "AssemblyProduct","AssemblyCompany","AssemblyCopyright","AssemblyInformationalVersion" }) {
					Assert.IsFalse(Regex.IsMatch(assemblyInfo,@"(?m)^\s*\[assembly:\s*"+attribute+@"\("),shippedProject.Folder+" still sets "+attribute+".");
				}
				CollectionAssert.AreEqual(new[] { shippedProject.AssemblyVersion },GetAttributeValues(assemblyInfo,"AssemblyVersion"),
					shippedProject.Folder+": AssemblyVersion");
				string[] arrayExpectedFileVersions=(shippedProject.FileVersion==null) ? new string[0] : new[] { shippedProject.FileVersion };
				CollectionAssert.AreEqual(arrayExpectedFileVersions,GetAttributeValues(assemblyInfo,"AssemblyFileVersion"),
					shippedProject.Folder+": AssemblyFileVersion");
			}
		}

		[TestMethod]
		public void BuiltAssemblies_CarryTheProductNameReleaseVersionAndCopyright() {
			string folder=Path.Combine(TestPaths.RepoRoot,"OpenPmsDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenPmsDental.exe"))) {
				BuildOutput.NotBuilt(@"OpenPmsDental\bin\Debug is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			foreach(string fileName in _arrayBuiltAssemblies) {
				string path=Path.Combine(folder,fileName);
				Assert.IsTrue(File.Exists(path),"Missing from the output: "+fileName);
				FileVersionInfo fileVersionInfo=FileVersionInfo.GetVersionInfo(path);
				Assert.AreEqual("Open PMS - Dental",fileVersionInfo.ProductName,fileName);
				Assert.AreEqual("0.0",fileVersionInfo.ProductVersion,fileName);
				StringAssert.Contains(fileVersionInfo.LegalCopyright,"Jordan Sparks",fileName);
				StringAssert.Contains(fileVersionInfo.LegalCopyright,"Open PMS - Dental contributors",fileName);
			}
			Assert.AreEqual("24.3.63.0",FileVersionInfo.GetVersionInfo(Path.Combine(folder,"OpenPmsDental.exe")).FileVersion);
			Assert.AreEqual("24.3.63.0",FileVersionInfo.GetVersionInfo(Path.Combine(folder,"OpenPmsDentalBusiness.dll")).FileVersion);
		}

		///<summary>The native Direct2dWrapper.dll is built from source too, so its version resource names the fork like the shared assembly info
		///does, and keeps its file version.</summary>
		[TestMethod]
		public void Direct2dWrapper_NamesTheForkAndKeepsItsFileVersion() {
			string rc=TestPaths.ReadSource("Direct2dWrapper","xDirect2dWrapper.rc");
			StringAssert.Contains(rc,"VALUE \"CompanyName\", \"Open PMS - Dental contributors\"");
			StringAssert.Contains(rc,"VALUE \"ProductName\", \"Open PMS - Dental\"");
			StringAssert.Contains(rc,"VALUE \"ProductVersion\", \"0.0\"");
			StringAssert.Contains(rc,"VALUE \"FileVersion\", \"24.3.63.0\"");
			StringAssert.Contains(rc," FILEVERSION 24,3,63,0");
			Assert.IsFalse(rc.Contains("\"Open Dental Software\"") || rc.Contains("\"Open Dental\""),"The version resource still names Open Dental as the company or product.");
			string path=Path.Combine(TestPaths.RepoRoot,"OpenPmsDental","bin","Debug","Direct2dWrapper.dll");
			if(!File.Exists(path)) {
				BuildOutput.NotBuilt(@"OpenPmsDental\bin\Debug\Direct2dWrapper.dll is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			FileVersionInfo fileVersionInfo=FileVersionInfo.GetVersionInfo(path);
			Assert.AreEqual("Open PMS - Dental contributors",fileVersionInfo.CompanyName);
			Assert.AreEqual("Open PMS - Dental",fileVersionInfo.ProductName);
			Assert.AreEqual("0.0",fileVersionInfo.ProductVersion);
			Assert.AreEqual("24.3.63.0",fileVersionInfo.FileVersion);
			StringAssert.Contains(fileVersionInfo.LegalCopyright,"Jordan Sparks");
			StringAssert.Contains(fileVersionInfo.LegalCopyright,"Open PMS - Dental contributors");
		}

		[TestMethod]
		public void OutputFolder_HasTheNativeRuntimeDlls() {
			string folder=Path.Combine(TestPaths.RepoRoot,"OpenPmsDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenPmsDental.exe"))) {
				BuildOutput.NotBuilt(@"OpenPmsDental\bin\Debug is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			List<string> listFileNames=Directory.GetFiles(folder).Select(x => Path.GetFileName(x)).ToList();
			foreach(string fileName in _arrayRuntimeDlls) {
				Assert.IsTrue(listFileNames.Contains(fileName,StringComparer.OrdinalIgnoreCase),"The output folder does not list "+fileName+".");
				string pathOutput=Path.Combine(folder,fileName);
				string pathRequired=Path.Combine(TestPaths.RepoRoot,"Required dlls",fileName);
				CollectionAssert.AreEqual(File.ReadAllBytes(pathRequired),File.ReadAllBytes(pathOutput),fileName+" is the copy from Required dlls.");
				Assert.AreEqual(0x014C,GetPeMachine(pathOutput),fileName+" is x86, like OpenPmsDental.exe.");
			}
		}

		[TestMethod]
		public void OpenDentalCsproj_CopiesTheNativeRuntimeDllsInEveryConfiguration() {
			//Since S2-3 of the .NET 10 move WebView2Loader.dll comes from the WebView2 package (Microsoft.Web.WebView2, the version of
			//Directory.Packages.props), not from a None item of "Required dlls": OpenPmsDental names the package with its build files, whose
			//build\Common.targets adds the x86 loader as a Content item for runtimes\win-x86\native\, and Directory.Build.targets gives that item
			//the file name as its Link, without a condition on the configuration, so Debug and Release builds copy it to the top of the output.
			//No item copies a loader anywhere else (a runtimes sub folder, which tools\Build-Release.ps1 refuses in the program's output) or the
			//x64 or arm64 loader. Evaluated, with the package's build files as the restore's obj\OpenPmsDental.csproj.nuget.g.targets imports them.
			string pathProject=Path.Combine(TestPaths.RepoRoot,"OpenPmsDental","OpenPmsDental.csproj");
			foreach(string configuration in new[] { "Debug","Release" }) {
				ProjectFile projectFile=ProjectFile.Load(pathProject,configuration,"x86");
				ProjectItem projectItemPackage=projectFile.GetPackageReference("Microsoft.Web.WebView2");
				Assert.IsNotNull(projectItemPackage,"OpenPmsDental names the WebView2 package itself.");
				Assert.AreEqual("",projectItemPackage.GetMetadata("ExcludeAssets"),"With its build files, which bring the loader.");
				Assert.AreEqual("1.0.2420.47",projectFile.GetPackageVersion("Microsoft.Web.WebView2"));
				if(!File.Exists(projectFile.GetProperty("ProjectAssetsFile"))) {
					BuildOutput.NotBuilt("OpenPmsDental is not restored yet, so its package's build files are not imported. Build OpenDental.Local.slnf first.");
				}
				List<ProjectItem> listCopied=projectFile.GetItems("None").Concat(projectFile.GetItems("Content"))
					.Where(x => x.GetMetadata("CopyToOutputDirectory")!="").ToList();
				foreach(string fileName in _arrayRuntimeDlls) {
					List<ProjectItem> listItems=listCopied.Where(x => Path.GetFileName(x.FullPath??x.Include).Equals(fileName,StringComparison.OrdinalIgnoreCase)).ToList();
					Assert.AreEqual(1,listItems.Count,configuration+": one item copies "+fileName+": "+string.Join(", ",listItems.Select(x => x.ItemType+" "+x.Include)));
					ProjectItem projectItem=listItems[0];
					Assert.AreEqual("Content",projectItem.ItemType,configuration);
					StringAssert.EndsWith(projectItem.FullPath.ToLowerInvariant(),@"\microsoft.web.webview2\1.0.2420.47\runtimes\win-x86\native\"+fileName.ToLowerInvariant(),
						configuration+": the package's x86 loader.");
					Assert.AreEqual(fileName,projectItem.GetMetadata("Link"),configuration+": copied to the top of the output.");
					Assert.AreEqual("PreserveNewest",projectItem.GetMetadata("CopyToOutputDirectory"),configuration);
					//The same bytes as "Required dlls", which keeps the file until S2-4 moves the release tooling to the packages.
					CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(TestPaths.RepoRoot,"Required dlls",fileName)),File.ReadAllBytes(projectItem.FullPath),
						configuration+": "+fileName+" of the package is the file of Required dlls.");
				}
				//The package's loaders for x64 and arm64 (only for an AnyCPU project) and its runtimes folders stay out.
				List<ProjectItem> listElsewhere=listCopied.Where(x => x.GetMetadata("Link").StartsWith("runtimes\\",StringComparison.OrdinalIgnoreCase)
					|| (x.FullPath??"").IndexOf(@"\runtimes\win-x64\",StringComparison.OrdinalIgnoreCase)>=0
					|| (x.FullPath??"").IndexOf(@"\runtimes\win-arm64\",StringComparison.OrdinalIgnoreCase)>=0).ToList();
				Assert.AreEqual(0,listElsewhere.Count,configuration+": "+string.Join(", ",listElsewhere.Select(x => x.Include+" -> "+x.GetMetadata("Link"))));
				Assert.IsFalse(listCopied.Any(x => x.Include.IndexOf("Required dlls",StringComparison.OrdinalIgnoreCase)>=0),
					configuration+": no item copies a file of Required dlls any more.");
			}
		}

		[TestMethod]
		public void ReleaseVersion_IsNeverSentOrComparedAsAVersionNumber() {
			//Each of these read Application.ProductVersion, which is the release version ("0.0") in every shipped program since the shared
			//assembly info. Where a version number is sent or compared, they now use the compatibility version.
			string[][] arrayPaths=new[] {
				new[] { "OpenPmsDentalBusiness","Eclaims","ClaimConnect.cs" },
				new[] { "OpenPmsDentalBusiness","Data Interface","CentralConnections.cs" },
				new[] { "WpfControlsOD","Bridges","DemandForce.cs" },
				new[] { "OpenPmsDental","Forms","FormUpdateSetup.cs" },
				new[] { "OpenPmsDentalHL7","ServiceHL7.cs" },
				new[] { "OpenPmsDental","Forms","FormUpdate.cs" },
			};
			foreach(string[] arrayPath in arrayPaths) {
				string text=StripComments(TestPaths.ReadSource(arrayPath));
				Assert.IsFalse(text.Contains("Application.ProductVersion"),string.Join("\\",arrayPath));
			}
			Assert.AreEqual(3,Regex.Matches(StripComments(TestPaths.ReadSource("OpenPmsDentalBusiness","Eclaims","ClaimConnect.cs")),
				@"ForkVersions\.CompatVersion").Count,"DentalXChange: the uploader version and both credential versions.");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("OpenPmsDentalBusiness","Data Interface","CentralConnections.cs")),
				"Security.LogInWeb(username,password,\"\",ForkVersions.CompatVersion.ToString(4),useEcwAlgorithm)");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("WpfControlsOD","Bridges","DemandForce.cs")),
				"string versionCur=ForkVersions.CompatVersion.ToString();");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("OpenPmsDental","Forms","FormUpdateSetup.cs")),
				"Version versionCurrent=ForkVersions.CompatVersion;");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("OpenPmsDentalHL7","ServiceHL7.cs")),
				"string versionProgram=ForkVersions.CompatVersion.ToString(4);");
			//The Update window shows the release version, as About does.
			StringAssert.Contains(TestPaths.ReadSource("OpenPmsDental","Forms","FormUpdate.cs"),"AboutBoxInfo.GetReleaseVersion(assemblyProgram)");
		}

		///<summary>The values of [assembly: Name("value")] lines that are not commented out.</summary>
		private static string[] GetAttributeValues(string text,string attribute) {
			return Regex.Matches(text,@"(?m)^\s*\[assembly:\s*"+attribute+@"\(""([^""]*)""\)\]").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
		}

		///<summary>The machine field of a PE file's COFF header (0x014C is x86).</summary>
		private static int GetPeMachine(string path) {
			byte[] arrayBytes=File.ReadAllBytes(path);
			int offsetPe=BitConverter.ToInt32(arrayBytes,0x3C);
			Assert.AreEqual("PE\0\0",System.Text.Encoding.ASCII.GetString(arrayBytes,offsetPe,4),path);
			return BitConverter.ToUInt16(arrayBytes,offsetPe+4);
		}

		///<summary>Removes // and /* */ comments (not inside string literals) so prose in comments does not count.</summary>
		private static string StripComments(string text) {
			return Regex.Replace(text,@"""(?:[^""\\]|\\.)*""|//[^\n]*|/\*.*?\*/",m => m.Value.StartsWith("/") ? "" : m.Value,RegexOptions.Singleline);
		}

		private class ShippedProject {
			public string Folder;
			public string ProjectFile;
			public string AssemblyVersion;
			public string FileVersion;

			public ShippedProject(string folder,string projectFile,string assemblyVersion,string fileVersion) {
				Folder=folder;
				ProjectFile=projectFile;
				AssemblyVersion=assemblyVersion;
				FileVersion=fileVersion;
			}
		}
	}
}
