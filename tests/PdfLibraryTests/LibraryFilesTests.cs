// The PDFsharp and MigraDoc 6.5.0 files in "Required dlls" (the restored NuGet packages' .NET Framework builds), the 1.2 files gone from the
// repository and every project, the release shipping the new files with their licence, the installer removing the old ones on upgrade, and every
// program selecting the licence before its first PDF.
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
		///<summary>File, assembly version and file version of each file of PDFsharp-GDI and PDFsharp-MigraDoc-GDI 6.5.0 (lib/net462) the program
		///loads: PdfSharp-gdi and the three assemblies it references, PdfSharp.Charting-gdi (which MigraDoc.Rendering-gdi references) and MigraDoc.
		///</summary>
		private static readonly string[][] _arrayFiles=new[] {
			new[] { "PdfSharp-gdi.dll","6.5.0.0","6.5.0.7955" },
			new[] { "PdfSharp.Charting-gdi.dll","6.5.0.0","6.5.0.7955" },
			new[] { "PdfSharp.Shared.dll","6.5.0.0","6.5.0.7955" },
			new[] { "PdfSharp.System.dll","6.5.0.0","6.5.0.7955" },
			new[] { "PdfSharp.License.dll","7.0.0.0","7.0.0.7955" },
			new[] { "MigraDoc.DocumentObjectModel.dll","6.5.0.0","6.5.0.7955" },
			new[] { "MigraDoc.Rendering-gdi.dll","6.5.0.0","6.5.0.7955" },
		};
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
		public void RequiredDlls_HoldThe65Files_AndNotThe12Ones() {
			List<string> listProblems=new List<string>();
			foreach(string[] arrayRow in _arrayFiles) {
				string path=Path.Combine(RequiredDlls,arrayRow[0]);
				if(!File.Exists(path)) {
					listProblems.Add(arrayRow[0]+" is missing.");
					continue;
				}
				string version=AssemblyName.GetAssemblyName(path).Version.ToString();
				string fileVersion=FileVersionInfo.GetVersionInfo(path).FileVersion;
				if(version!=arrayRow[1] || fileVersion!=arrayRow[2]) {
					listProblems.Add(arrayRow[0]+" is "+version+" (file "+fileVersion+"), not "+arrayRow[1]+" (file "+arrayRow[2]+").");
				}
				string publicKeyToken=string.Concat(AssemblyName.GetAssemblyName(path).GetPublicKeyToken().Select(x => x.ToString("x2")));
				if(publicKeyToken!="f94615aa0424f9eb") {
					listProblems.Add(arrayRow[0]+" is not signed by empira (public key token "+publicKeyToken+").");
				}
			}
			foreach(string fileName in _arrayOldFiles.Concat(_arrayUnused)) {
				if(File.Exists(Path.Combine(RequiredDlls,fileName))) {
					listProblems.Add(fileName+" should not be in Required dlls.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Projects_ReferenceOnlyThe65Assemblies() {
			List<string> listProblems=new List<string>();
			foreach(string path in Directory.GetFiles(RepoRoot,"*.csproj",SearchOption.AllDirectories)) {
				string relative=path.Substring(RepoRoot.Length);
				if(relative.Contains(@"\bin\") || relative.Contains(@"\obj\") || relative.Contains(@"\.claude\") || relative.Contains(@"\.git\")) {
					continue;
				}
				XDocument xDocument=XDocument.Load(path);
				foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName=="Reference")) {
					string include=((string)xElement.Attribute("Include")??"").Split(',')[0].Trim();
					if(include.IsOneOf("PdfSharp","PdfSharp.Charting","MigraDoc.Rendering","MigraDoc.RtfRendering")) {
						listProblems.Add(path.Substring(RepoRoot.Length)+": <Reference Include=\""+include+"\">");
					}
					string hintPath=(string)xElement.Elements().FirstOrDefault(x => x.Name.LocalName=="HintPath")??"";
					if(_arrayOldFiles.Any(x => hintPath.EndsWith("\\"+x,StringComparison.OrdinalIgnoreCase))) {
						listProblems.Add(path.Substring(RepoRoot.Length)+": "+hintPath);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Release_ShipsThe65Files_AndTheLicenceTexts_AndUpgradesRemoveThe12Files() {
			string script=File.ReadAllText(Path.Combine(RepoRoot,"tools","Build-Release.ps1"));
			foreach(string[] arrayRow in _arrayFiles) {
				StringAssert.Contains(script,"'"+arrayRow[0]+"'","Build-Release.ps1 ships "+arrayRow[0]);
			}
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
			StringAssert.Contains(script,"'LICENSE-AGPL-3.0.txt'='LICENSE-AGPL-3.0.txt'");
			StringAssert.Contains(script,"'LICENSE-GPL-3.0.txt'='LICENSE-GPL-3.0.txt'");
			string iss=File.ReadAllText(Path.Combine(RepoRoot,"installer","OpenPmsDental.iss"));
			Match match=Regex.Match(iss,@"(?ms)^\[InstallDelete\]\s*$(.*?)(?=^\[|\z)");
			Assert.IsTrue(match.Success,"The installer has an [InstallDelete] section.");
			foreach(string fileName in _arrayOldFiles) {
				StringAssert.Contains(match.Groups[1].Value,"Name: \"{app}\\"+fileName+"\"","An upgrade removes "+fileName);
			}
			string notices=File.ReadAllText(Path.Combine(RepoRoot,"THIRD-PARTY-NOTICES.txt"));
			foreach(string[] arrayRow in _arrayFiles) {
				StringAssert.Contains(notices,arrayRow[0]);
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
	}
}
