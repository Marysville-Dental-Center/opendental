// Package BIN-B: the binaries we may not redistribute (Topaz, Office and Adobe interop, RS232, the Windows SDK's Windows.winmd, installutil) and
// the dead references (zxing, MigraDoc RTF, Tao.FreeGlut, Avalara, DotNetZip) stay out of the repository, the projects and the build output.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BINBTests {
	[TestClass]
	public class RemovedBinariesTests {
		///<summary>The files BIN-B deleted, relative to the repository root.</summary>
		private static readonly string[] _arrayRemovedFiles={
			@"Required dlls\SigPlusNET.dll",
			@"Required dlls\Interop.Word.dll",
			@"Required dlls\Interop.Microsoft.Office.Core.dll",
			@"Required dlls\Interop.VBIDE.dll",
			@"Required dlls\stdole.dll",
			@"Required dlls\AxInterop.AcroPDFLib.dll",
			@"Required dlls\Interop.AcroPDFLib.dll",
			@"Required dlls\Interop.Acrobat.dll",
			@"Required dlls\Interop.AFORMAUTLib.dll",
			@"Required dlls\zxing.dll",
			@"Required dlls\zxing.presentation.dll",
			@"Required dlls\MigraDoc.RtfRendering.dll",
			@"Required dlls\Tao.FreeGlut.dll",
			@"Required dlls\freeglut.dll",
			@"Required dlls\Avalara.AvaTax.RestClient.net45.dll",
			@"Required dlls\RS232.dll",
			@"Required dlls\Ionic.Zip.dll",
			@"Required dlls\installutil.exe",
			@"SparksToothChart\RequiredDlls\Tao.FreeGlut.dll",
			@"SparksToothChart\RequiredDlls\freeglut.dll",
			@"OpenPmsDental\Forms\FormAvaTax.cs",
		};
		///<summary>The assemblies no project may reference again, as an assembly name or a file name.</summary>
		private static readonly string[] _arrayRemovedAssemblies={ "SigPlusNET","Interop.Word","Interop.Microsoft.Office.Core","Interop.VBIDE","stdole",
			"AxInterop.AcroPDFLib","Interop.AcroPDFLib","Interop.Acrobat","Interop.AFORMAUTLib","zxing","zxing.presentation","MigraDoc.RtfRendering",
			"Tao.FreeGlut","freeglut","Avalara.AvaTax.RestClient.net45","RS232","Ionic.Zip","installutil" };
		///<summary>Files that must never be in a build output folder: the removed assemblies, and Windows.winmd, which is referenced to compile only.
		///</summary>
		private static readonly string[] _arrayNeverInOutput=_arrayRemovedAssemblies.Select(x => x+".dll")
			.Concat(new[] { "installutil.exe","Windows.winmd" }).ToArray();

		[TestMethod]
		public void RemovedFiles_AreNotInTheRepository() {
			string root=TestPaths.RepoRoot;
			Assert.IsTrue(Directory.Exists(Path.Combine(root,"Required dlls")));
			List<string> listFound=_arrayRemovedFiles.Where(x => File.Exists(Path.Combine(root,x))).ToList();
			Assert.AreEqual(0,listFound.Count,"Back in the repository: "+string.Join(", ",listFound));
		}

		[TestMethod]
		public void RemovedAssemblies_AreNotNamedByAProjectItem() {
			string root=TestPaths.RepoRoot;
			List<string> listProjects=TestPaths.GetFiles(root,".csproj",".vbproj",".vcxproj",".targets",".props");
			Assert.IsTrue(listProjects.Any(x => x.EndsWith("OpenPmsDental.csproj",StringComparison.OrdinalIgnoreCase)),"No project files under "+root);
			List<string> listViolations=new List<string>();
			foreach(string path in listProjects) {
				XDocument xDocument;
				try {
					xDocument=XDocument.Load(path);
				}
				catch(Exception ex) {
					Assert.Fail("Not readable as XML: "+path+": "+ex.Message);
					return;
				}
				foreach(XElement xElement in xDocument.Descendants()) {
					string local=xElement.Name.LocalName;
					List<string> listValues=new List<string>();
					if(local=="Reference" || local=="COMReference") {
						string include=((string)xElement.Attribute("Include")??"").Split(',')[0].Trim();
						if(_arrayRemovedAssemblies.Contains(include,StringComparer.OrdinalIgnoreCase)) {
							listViolations.Add(path.Substring(root.Length)+": <"+local+" Include=\""+include+"\">");
						}
					}
					if(local=="HintPath") {
						listValues.Add(xElement.Value);
					}
					if(local=="Content" || local=="None" || local=="Reference") {
						listValues.Add((string)xElement.Attribute("Include")??"");
					}
					foreach(string value in listValues) {
						string fileName=Path.GetFileName(value.Trim().Replace('/','\\'));
						if(_arrayNeverInOutput.Where(x => x!="Windows.winmd").Contains(fileName,StringComparer.OrdinalIgnoreCase)) {
							listViolations.Add(path.Substring(root.Length)+": <"+local+"> names "+value.Trim());
						}
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Removed assemblies are named again: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void RemovedAssemblies_AreNotUsedBySource() {
			string root=TestPaths.RepoRoot;
			Regex regexUsing=new Regex(@"^\s*using\s+(Ionic\.Zip|Topaz|RS232|Avalara(\.\w+)*|Word|Microsoft\.Office\.Core|ZXing(\.\w+)*|Tao\.FreeGlut|AcroPDFLib|AxAcroPDFLib)\s*;",
				RegexOptions.Multiline);
			List<string> listViolations=new List<string>();
			List<string> listSources=TestPaths.GetFiles(root,".cs");
			Assert.IsTrue(listSources.Count>1000,"The source files were not found under "+root);
			foreach(string path in listSources) {
				string text=File.ReadAllText(path);
				foreach(Match match in regexUsing.Matches(text)) {
					listViolations.Add(path.Substring(root.Length)+": "+match.Value.Trim());
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Source uses a removed assembly: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void RemovedAssemblies_AreNotCopiedToTheBuildOutput() {
			//This test's own folder holds the copy-local closure of OpenPmsDentalBusiness, CodeBase and PasswordVaultWrapper.
			List<string> listFolders=new List<string> { AppDomain.CurrentDomain.BaseDirectory };
			string folderOpenDental=Path.Combine(TestPaths.RepoRoot,"OpenPmsDental","bin","Debug");
			if(File.Exists(Path.Combine(folderOpenDental,"OpenPmsDental.exe"))) {
				listFolders.Add(folderOpenDental);
			}
			List<string> listFound=new List<string>();
			foreach(string folder in listFolders) {
				listFound.AddRange(_arrayNeverInOutput.Where(x => File.Exists(Path.Combine(folder,x))).Select(x => Path.Combine(folder,x)));
			}
			Assert.AreEqual(0,listFound.Count,"In a build output folder: "+string.Join(", ",listFound));
		}

		[TestMethod]
		public void WindowsWinmd_IsReferencedToCompileOnly() {
			XDocument xDocument=XDocument.Load(Path.Combine(TestPaths.RepoRoot,"PasswordVaultWrapper","PasswordVaultWrapper.csproj"));
			XElement xElementReference=xDocument.Descendants().Single(x => x.Name.LocalName=="Reference" && (string)x.Attribute("Include")=="Windows");
			StringAssert.EndsWith(xElementReference.Elements().Single(x => x.Name.LocalName=="HintPath").Value,"Windows.winmd");
			Assert.AreEqual("False",xElementReference.Elements().Single(x => x.Name.LocalName=="Private").Value,
				"Windows.winmd is a Windows SDK file: compile against it, never copy it.");
		}

		[TestMethod]
		public void ServiceManager_DoesNotReferenceInstallUtil() {
			string csproj=TestPaths.ReadSource("ServiceManager","ServiceManager.csproj");
			Assert.IsFalse(csproj.IndexOf("installutil",StringComparison.OrdinalIgnoreCase)>=0,"ServiceManager.csproj references installutil again.");
			string source=TestPaths.ReadSource("CodeBase","Utilities","ServicesHelper.cs");
			StringAssert.Contains(source,"RuntimeEnvironment.GetRuntimeDirectory()","Services are installed with the .NET Framework's own InstallUtil.exe.");
		}

		[TestMethod]
		public void PasswordVault_LoadsWithoutTheCopiedWinmd() {
			//Windows resolves Windows.Security.Credentials from its own metadata, so the wrapper works without a Windows.winmd next to it. A resource
			//that cannot exist is looked up: nothing is written to the credential vault, and a missing type would throw instead of returning false.
			Assert.IsFalse(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Windows.winmd")));
			string uri="binbtests-"+Guid.NewGuid().ToString("N")+".invalid";
			Assert.IsFalse(PasswordVaultWrapper.WindowsPasswordVaultWrapper.TryRetrieveUserName(uri,out string userName));
			Assert.AreEqual("",userName);
		}
	}
}
