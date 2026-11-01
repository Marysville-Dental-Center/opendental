// Checks for package REL-FILES (docs/port-notes/REL-FILES.md): the shared assembly info of every shipped assembly, the version numbers it must
// not change, the native files the program needs next to it, and the code that used to read the release version as a version number.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
			new ShippedProject("OpenDental","OpenDental.csproj","24.3.63.0",null),
			new ShippedProject("OpenDentalCloud","OpenDentalCloud.csproj","16.4.0.0","1.0.0.0"),
			new ShippedProject("OpenDentalGraph","OpenDentalGraph.csproj","16.1.1.0","16.1.1.0"),
			new ShippedProject("OpenDentalServer","OpenDentalServer.csproj","24.3.63.0",null),
			new ShippedProject("OpenDentBusiness","OpenDentBusiness.csproj","24.3.63.0",null),
			new ShippedProject("OpenDentHL7","OpenDentHL7.csproj","24.3.63.0",null),
			new ShippedProject("PasswordVaultWrapper","PasswordVaultWrapper.csproj","1.0.0.0","1.0.0.0"),
			new ShippedProject("ServiceManager","ServiceManager.csproj","15.4.1.0","15.4.1.0"),
			new ShippedProject("SparksToothChart","SparksToothChart.csproj","4.7.1.0",null),
			new ShippedProject("UpdateFileCopier","UpdateFileCopier.csproj","24.3.63.0","13.1.0.0"),
			new ShippedProject("WpfControlsOD","WpfControlsOD.csproj","1.0.0.0","1.0.0.0"),
		};

		///<summary>The program's own assemblies in OpenDental's output folder, which carry the shared assembly info.</summary>
		private static readonly string[] _arrayBuiltAssemblies=new string[] {
			"OpenDental.exe","OpenDentBusiness.dll","CodeBase.dll","DataConnectionBase.dll","WpfControlsOD.dll","OpenDentalGraph.dll",
			"OpenDentalCloud.dll","SparksToothChart.dll","ODCrypt.dll","PasswordVaultWrapper.dll","ImagingCapture.dll","ImagingDeviceManager.dll",
			"ServiceManager.exe","UpdateFileCopier.exe","ODR.dll",
		};

		///<summary>The native x86 files the program loads at run time that no reference copies (Required dlls).</summary>
		private static readonly string[] _arrayRuntimeDlls=new string[] { "Hunspellx86.dll","WebView2Loader.dll" };

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
				string csproj=TestPaths.ReadSource(shippedProject.Folder,shippedProject.ProjectFile);
				StringAssert.Contains(csproj,"<Compile Include=\"..\\SharedAssemblyInfo.cs\">",shippedProject.Folder);
				StringAssert.Contains(csproj,"<Link>Properties\\SharedAssemblyInfo.cs</Link>",shippedProject.Folder);
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
			string folder=Path.Combine(TestPaths.RepoRoot,"OpenDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenDental.exe"))) {
				Assert.Inconclusive("OpenDental is not built.");
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
			Assert.AreEqual("24.3.63.0",FileVersionInfo.GetVersionInfo(Path.Combine(folder,"OpenDental.exe")).FileVersion);
			Assert.AreEqual("24.3.63.0",FileVersionInfo.GetVersionInfo(Path.Combine(folder,"OpenDentBusiness.dll")).FileVersion);
		}

		[TestMethod]
		public void OutputFolder_HasTheNativeRuntimeDlls() {
			string folder=Path.Combine(TestPaths.RepoRoot,"OpenDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenDental.exe"))) {
				Assert.Inconclusive("OpenDental is not built.");
			}
			List<string> listFileNames=Directory.GetFiles(folder).Select(x => Path.GetFileName(x)).ToList();
			foreach(string fileName in _arrayRuntimeDlls) {
				Assert.IsTrue(listFileNames.Contains(fileName,StringComparer.OrdinalIgnoreCase),"The output folder does not list "+fileName+".");
				string pathOutput=Path.Combine(folder,fileName);
				string pathRequired=Path.Combine(TestPaths.RepoRoot,"Required dlls",fileName);
				CollectionAssert.AreEqual(File.ReadAllBytes(pathRequired),File.ReadAllBytes(pathOutput),fileName+" is the copy from Required dlls.");
				Assert.AreEqual(0x014C,GetPeMachine(pathOutput),fileName+" is x86, like OpenDental.exe.");
			}
		}

		[TestMethod]
		public void OpenDentalCsproj_CopiesTheNativeRuntimeDllsInEveryConfiguration() {
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.Load(Path.Combine(TestPaths.RepoRoot,"OpenDental","OpenDental.csproj"));
			XmlNamespaceManager xmlNamespaceManager=new XmlNamespaceManager(xmlDocument.NameTable);
			xmlNamespaceManager.AddNamespace("b","http://schemas.microsoft.com/developer/msbuild/2003");
			foreach(string fileName in _arrayRuntimeDlls) {
				XmlElement xmlElement=(XmlElement)xmlDocument.SelectSingleNode("//b:ItemGroup/b:None[@Include='..\\Required dlls\\"+fileName+"']",
					xmlNamespaceManager);
				Assert.IsNotNull(xmlElement,"No copy item for "+fileName);
				Assert.AreEqual(fileName,xmlElement.SelectSingleNode("b:Link",xmlNamespaceManager)?.InnerText,"Copied to the top of the output.");
				Assert.AreEqual("PreserveNewest",xmlElement.SelectSingleNode("b:CopyToOutputDirectory",xmlNamespaceManager)?.InnerText);
				//No condition on the item or its group, so Release builds copy it too.
				Assert.IsFalse(xmlElement.HasAttribute("Condition"),fileName);
				Assert.IsFalse(((XmlElement)xmlElement.ParentNode).HasAttribute("Condition"),fileName);
			}
		}

		[TestMethod]
		public void ReleaseVersion_IsNeverSentOrComparedAsAVersionNumber() {
			//Each of these read Application.ProductVersion, which is the release version ("0.0") in every shipped program since the shared
			//assembly info. Where a version number is sent or compared, they now use the compatibility version.
			string[][] arrayPaths=new[] {
				new[] { "OpenDentBusiness","Eclaims","ClaimConnect.cs" },
				new[] { "OpenDentBusiness","Data Interface","CentralConnections.cs" },
				new[] { "WpfControlsOD","Bridges","DemandForce.cs" },
				new[] { "OpenDental","Forms","FormUpdateSetup.cs" },
				new[] { "OpenDentHL7","ServiceHL7.cs" },
				new[] { "OpenDental","Forms","FormUpdate.cs" },
			};
			foreach(string[] arrayPath in arrayPaths) {
				string text=StripComments(TestPaths.ReadSource(arrayPath));
				Assert.IsFalse(text.Contains("Application.ProductVersion"),string.Join("\\",arrayPath));
			}
			Assert.AreEqual(3,Regex.Matches(StripComments(TestPaths.ReadSource("OpenDentBusiness","Eclaims","ClaimConnect.cs")),
				@"ForkVersions\.CompatVersion").Count,"DentalXChange: the uploader version and both credential versions.");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("OpenDentBusiness","Data Interface","CentralConnections.cs")),
				"Security.LogInWeb(username,password,\"\",ForkVersions.CompatVersion.ToString(4),useEcwAlgorithm)");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("WpfControlsOD","Bridges","DemandForce.cs")),
				"string versionCur=ForkVersions.CompatVersion.ToString();");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("OpenDental","Forms","FormUpdateSetup.cs")),
				"Version versionCurrent=ForkVersions.CompatVersion;");
			StringAssert.Contains(StripComments(TestPaths.ReadSource("OpenDentHL7","ServiceHL7.cs")),
				"string versionProgram=ForkVersions.CompatVersion.ToString(4);");
			//The Update window shows the release version, as About does.
			StringAssert.Contains(TestPaths.ReadSource("OpenDental","Forms","FormUpdate.cs"),"AboutBoxInfo.GetReleaseVersion(assemblyProgram)");
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
