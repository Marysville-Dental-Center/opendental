// The PDFsharp and MigraDoc 6.5.0 assemblies are built here from empira's source (third-party\build, the git submodule third-party\PDFsharp):
// none of them, and no PdfSharp.License.dll, is in "Required dlls"; every project references them as projects; the built files have empira's
// names, versions and public key token, mark this build and reference no PdfSharp.License; the release ships them from their project outputs
// and denies PdfSharp.License.dll; the 1.2 files stay gone; every program selects the licence before its first PDF.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PdfLibraryTests {
	[TestClass]
	public class LibraryFilesTests {
		///<summary>The assemblies of PDFsharp and MigraDoc 6.5.0 the program loads, as the projects of third-party\build name them: PdfSharp-gdi
		///and the two assemblies it references, PdfSharp.Charting-gdi (which MigraDoc.Rendering-gdi references) and MigraDoc.</summary>
		public static readonly string[] ArrayAssemblies=new[] { "PdfSharp-gdi","PdfSharp.Charting-gdi","PdfSharp.Shared","PdfSharp.System",
			"MigraDoc.DocumentObjectModel","MigraDoc.Rendering-gdi" };
		///<summary>The PDFsharp and MigraDoc 1.2 files that 0.0 shipped and 6.5 replaces under other names.</summary>
		private static readonly string[] _arrayOldFiles=new[] { "PdfSharp.dll","PdfSharp.Charting.dll","MigraDoc.Rendering.dll" };
		///<summary>Package files nothing the program loads references: not in the repository.</summary>
		private static readonly string[] _arrayUnused=new[] { "PdfSharp.BarCodes-gdi.dll","PdfSharp.Cryptography.dll","PdfSharp.Quality-gdi.dll",
			"PdfSharp.Snippets-gdi.dll","PdfSharp.WPFonts.dll","MigraDoc.RtfRendering-gdi.dll","MigraDoc.RtfRendering.dll" };

		private static string RequiredDlls {
			get { return Path.GetDirectoryName(TestDb.FindRepoFile(Path.Combine("Required dlls","MySqlConnector.dll"))); }
		}

		private static string RepoRoot {
			get { return Path.GetDirectoryName(RequiredDlls); }
		}

		[TestMethod]
		public void RequiredDlls_HoldNoPdfSharpOrMigraDocFile_AndNoLicenceDll() {
			List<string> listFound=ArrayAssemblies.Select(x => x+".dll").Concat(new[] { "PdfSharp.License.dll" }).Concat(_arrayOldFiles).Concat(_arrayUnused)
				.Where(x => File.Exists(Path.Combine(RequiredDlls,x))).ToList();
			Assert.AreEqual(0,listFound.Count,"Built from source, so not in Required dlls: "+string.Join(", ",listFound));
			Assert.AreEqual(0,Directory.GetFiles(RequiredDlls,"PdfSharp*").Length+Directory.GetFiles(RequiredDlls,"MigraDoc*").Length,
				"No PDFsharp or MigraDoc file of any name.");
		}

		[TestMethod]
		public void BuiltAssemblies_HaveEmpirasIdentities_MarkThisBuild_AndNeedNoLicenceDll() {
			List<string> listProblems=new List<string>();
			foreach(string name in ArrayAssemblies) {
				//The file next to this test, the one the tests load: the output of third-party\build\<name>.
				Assembly assembly=Assembly.Load(new AssemblyName(name+", Version=6.5.0.0, Culture=neutral, PublicKeyToken=f94615aa0424f9eb"));
				AssemblyName assemblyName=assembly.GetName();
				string publicKeyToken=string.Concat(assemblyName.GetPublicKeyToken().Select(x => x.ToString("x2")));
				if(assemblyName.Version.ToString()!="6.5.0.0" || publicKeyToken!=SourceBuild.PDFSHARP_PUBLIC_KEY_TOKEN) {
					listProblems.Add(name+" is "+assemblyName.Version+" with key "+publicKeyToken);
				}
				if(!Path.GetDirectoryName(assembly.Location).TrimEnd('\\').Equals(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) {
					listProblems.Add(name+" was loaded from "+assembly.Location);
				}
				FileVersionInfo fileVersionInfo=FileVersionInfo.GetVersionInfo(assembly.Location);
				if(fileVersionInfo.FileVersion!="6.5.0.0") {
					listProblems.Add(name+" file version "+fileVersionInfo.FileVersion);
				}
				string informational=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
				if(informational!=SourceBuild.PDFSHARP_INFORMATIONAL_VERSION || fileVersionInfo.ProductVersion!=informational) {
					listProblems.Add(name+" informational version "+informational+" (product version "+fileVersionInfo.ProductVersion+")");
				}
				Dictionary<string,string> dictMetadata=assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(x => x.Key,x => x.Value);
				if(dictMetadata.GetValueOrDefault("SourceCommit")!=SourceBuild.PDFSHARP_COMMIT || dictMetadata.GetValueOrDefault("SourceTag")!=SourceBuild.PDFSHARP_TAG
					|| dictMetadata.GetValueOrDefault("SourceRepository")!="https://github.com/empira/PDFsharp"
					|| dictMetadata.GetValueOrDefault("SourceBuild")!=@"Open PMS - Dental, third-party\build\"+name)
				{
					listProblems.Add(name+" metadata: "+string.Join("; ",dictMetadata.Select(x => x.Key+"="+x.Value)));
				}
				if(assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company!="empira Software") {
					listProblems.Add(name+" company "+assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company);
				}
				if(assembly.GetReferencedAssemblies().Any(x => x.Name=="PdfSharp.License")) {
					listProblems.Add(name+" references PdfSharp.License.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//The licence check is part of PdfSharp-gdi itself.
			Assert.AreEqual(typeof(PdfSharp.Pdf.PdfDocument).Assembly,typeof(PdfSharp.PdfSharpLicense).Assembly);
			Assert.AreEqual("PdfSharp-gdi",typeof(PdfSharp.License.PdfSharpLicenseException).Assembly.GetName().Name);
			Assert.IsFalse(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PdfSharp.License.dll")),"No PdfSharp.License.dll next to the tests.");
			string programFolder=Path.Combine(RepoRoot,"OpenPmsDental","bin","Debug");
			if(File.Exists(Path.Combine(programFolder,"OpenPmsDental.exe"))) {
				Assert.IsFalse(File.Exists(Path.Combine(programFolder,"PdfSharp.License.dll")),"No PdfSharp.License.dll next to the program.");
				foreach(string name in ArrayAssemblies) {
					Assert.AreEqual(SourceBuild.PDFSHARP_INFORMATIONAL_VERSION,FileVersionInfo.GetVersionInfo(Path.Combine(programFolder,name+".dll")).ProductVersion,
						"The program folder holds the file built from source: "+name);
				}
			}
		}

		[TestMethod]
		public void Projects_ReferenceTheSourceBuild_NotDlls() {
			List<string> listProblems=new List<string>();
			List<string> listUsers=new List<string>();
			foreach(string path in Directory.GetFiles(RepoRoot,"*.csproj",SearchOption.AllDirectories)) {
				string relative=path.Substring(RepoRoot.Length+1);
				if(relative.Contains(@"\bin\") || relative.Contains(@"\obj\") || relative.StartsWith(@".claude\") || relative.StartsWith(@".git\")
					|| relative.StartsWith(@"third-party\PDFsharp\") || relative.StartsWith(@"third-party\WeCantSpell.Hunspell\"))
				{
					continue;//the upstream projects of the submodules are not built
				}
				XDocument xDocument=XDocument.Load(path);
				foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName=="Reference")) {
					string include=((string)xElement.Attribute("Include")??"").Split(',')[0].Trim();
					if(include.StartsWith("PdfSharp",StringComparison.OrdinalIgnoreCase) || include.StartsWith("MigraDoc",StringComparison.OrdinalIgnoreCase)) {
						listProblems.Add(relative+": <Reference Include=\""+include+"\">");
					}
					string hintPath=(string)xElement.Elements().FirstOrDefault(x => x.Name.LocalName=="HintPath")??"";
					string fileName=Path.GetFileName(hintPath);
					if(fileName.StartsWith("PdfSharp",StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("MigraDoc",StringComparison.OrdinalIgnoreCase)) {
						listProblems.Add(relative+": "+hintPath);
					}
				}
				foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName=="ProjectReference")) {
					string include=(string)xElement.Attribute("Include")??"";
					if(Regex.IsMatch(include,@"(^|\\)third-party\\build\\(PdfSharp|MigraDoc)")) {
						if(!relative.StartsWith(@"third-party\build\")) {
							listUsers.Add(relative);
						}
						string target=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),include));
						if(!File.Exists(target)) {
							listProblems.Add(relative+": "+include+" does not exist.");
						}
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//The projects that referenced the NuGet files in "Required dlls" before.
			foreach(string user in new[] { @"OpenPmsDental\OpenPmsDental.csproj",@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj",
				@"WpfControlsOD\WpfControlsOD.csproj",@"OpenPmsDentalGraph\OpenPmsDentalGraph.csproj",@"ImagingDeviceManager\xImagingDeviceManager.csproj",
				@"tests\PdfLibraryTests\PdfLibraryTests.csproj",@"tests\M2SHEETSTests\M2SHEETSTests.csproj",@"tests\BPRINTTests\BPRINTTests.csproj" })
			{
				CollectionAssert.Contains(listUsers,user);
			}
		}

		[TestMethod]
		public void Release_ShipsTheBuiltFiles_DeniesTheLicenceDll_AndUpgradesRemoveThe12Files() {
			string script=File.ReadAllText(Path.Combine(RepoRoot,"tools","Build-Release.ps1"));
			StringAssert.Contains(script,"foreach($name in @('PdfSharp-gdi','PdfSharp.Charting-gdi','PdfSharp.Shared','PdfSharp.System','MigraDoc.DocumentObjectModel','MigraDoc.Rendering-gdi',");
			StringAssert.Contains(script,@"$ThirdPartyFiles[$name+'.dll']='third-party\build\'+$name+'\bin\Release'");
			Assert.IsFalse(Regex.IsMatch(script,@"'PdfSharp\.License\.dll'(?!,"")"),"PdfSharp.License.dll is only on the deny-list.");
			StringAssert.Contains(script,"@('PdfSharp.License.dll',\"empira's closed licence check of PDFsharp 6.5");
			//The allow-list comes before the deny-list, which names the 1.2 files as files of an earlier release that a release refuses
			//(ForkReleases.ListRetiredFiles).
			int indexDenyList=script.IndexOf("$DenyList=@(");
			Assert.IsTrue(indexDenyList>0,"Build-Release.ps1 has a deny-list.");
			string scriptAllowed=script.Substring(0,indexDenyList);
			string scriptDenied=script.Substring(indexDenyList);
			foreach(string fileName in _arrayOldFiles) {
				Assert.IsFalse(scriptAllowed.Contains("'"+fileName+"'"),"Build-Release.ps1 still ships "+fileName);
				StringAssert.Contains(scriptDenied,"@('"+fileName+"',","Build-Release.ps1 refuses "+fileName);
			}
			List<string> listThird=SourceBuild.ListReleaseFiles().Where(x => x.Key=="third").Select(x => x.Value).ToList();
			foreach(string name in ArrayAssemblies) {
				CollectionAssert.Contains(listThird,name+".dll");
			}
			CollectionAssert.DoesNotContain(listThird,"PdfSharp.License.dll");
			StringAssert.Contains(script,"'LICENSE-AGPL-3.0.txt'='LICENSE-AGPL-3.0.txt'");
			StringAssert.Contains(script,"'LICENSE-GPL-3.0.txt'='LICENSE-GPL-3.0.txt'");
			string iss=File.ReadAllText(Path.Combine(RepoRoot,"installer","OpenPmsDental.iss"));
			Match match=Regex.Match(iss,@"(?ms)^\[InstallDelete\]\s*$(.*?)(?=^\[|\z)");
			Assert.IsTrue(match.Success,"The installer has an [InstallDelete] section.");
			foreach(string fileName in _arrayOldFiles) {
				StringAssert.Contains(match.Groups[1].Value,"Name: \"{app}\\"+fileName+"\"","An upgrade removes "+fileName);
			}
			string notices=File.ReadAllText(Path.Combine(RepoRoot,"THIRD-PARTY-NOTICES.txt"));
			foreach(string name in ArrayAssemblies) {
				StringAssert.Contains(notices,name+".dll");
			}
			StringAssert.Contains(notices,"GNU Affero General Public License, version 3 (LICENSE-AGPL-3.0.txt)");
			string notice=File.ReadAllText(Path.Combine(RepoRoot,"NOTICE"));
			StringAssert.Contains(notice,"LICENSE-AGPL-3.0.txt");
			StringAssert.Contains(notice,"LICENSE-GPL-3.0.txt");
		}

		[TestMethod]
		public void Programs_SelectTheLicence_BeforeTheirFirstPdf() {
			string programEntry=File.ReadAllText(Path.Combine(RepoRoot,"OpenPmsDental","Main Modules","ProgramEntry.cs"));
			Assert.IsTrue(Regex.IsMatch(programEntry,@"ODInitialize\.Initialize\(\);\s*(//[^\n]*\n\s*)?PdfSharpSetup\.Init\(\);"),
				"OpenPmsDental selects the licence right after ODInitialize.");
			string dtoProcessor=File.ReadAllText(Path.Combine(RepoRoot,"OpenPmsDentalBusiness","Remoting","DtoProcessor.cs"));
			StringAssert.Contains(dtoProcessor,"PdfSharpSetup.Init();","The Middle Tier selects it when it initializes.");
			//The business methods that make PDFs by themselves select it too, for programs other than OpenPmsDental.
			foreach(string[] arrayFileAndMethod in new[] {
				new[] { @"OpenPmsDentalBusiness\SheetFramework\SheetDrawingJob.cs","public PdfDocument CreatePdf(" },
				new[] { @"OpenPmsDentalBusiness\Logic\BillingL.cs","public static bool SendStatements(" },
				new[] { @"OpenPmsDentalBusiness\Data Interface\PerioExams.cs","public static PdfDocument CreatePerioPDF(" } })
			{
				string text=File.ReadAllText(Path.Combine(RepoRoot,arrayFileAndMethod[0]));
				int index=text.IndexOf(arrayFileAndMethod[1]);
				Assert.IsTrue(index>=0,arrayFileAndMethod[1]);
				string start=text.Substring(index,Math.Min(400,text.Length-index));
				StringAssert.Contains(start,"PdfSharpSetup.Init();",arrayFileAndMethod[0]);
			}
		}
	}

	internal static class StringExtensions {
		public static bool IsOneOf(this string value,params string[] arrayValues) {
			return arrayValues.Contains(value,StringComparer.OrdinalIgnoreCase);
		}

		public static string GetValueOrDefault(this Dictionary<string,string> dict,string key) {
			return dict.TryGetValue(key,out string value) ? value : null;
		}
	}
}
