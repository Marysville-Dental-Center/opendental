// fo-dicom (MS-PL) is gone (owner decision 2026-11-02, option C; docs/imaging/dicom-reader.md): not in the repository, not named by a project,
// not used by source, not in a build output, removed from installed folders on upgrade, refused by the release check, and not in the notices.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DicomReaderTests {
	[TestClass]
	public class FoDicomRemovedTests {
		private static readonly string[] _arrayFiles={ "Dicom.Core.dll","Dicom.Native.dll" };

		private static string RepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))
					&& Directory.Exists(Path.Combine(directoryInfo.FullName,"Required dlls")))
				{
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("Repository root not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		private static IEnumerable<string> GetFiles(string root,params string[] arrayExtensions) {
			return Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
				.Where(x => arrayExtensions.Any(y => x.EndsWith(y,StringComparison.OrdinalIgnoreCase)))
				.Where(x => !Regex.IsMatch(x,@"\\(bin|obj|packages|\.git|\.vs|\.claude)\\",RegexOptions.IgnoreCase));//.claude holds other worktrees
		}

		[TestMethod]
		public void FoDicom_IsNotInTheRepository_NorNamedByAProject() {
			string root=RepoRoot();
			List<string> listFound=Directory.EnumerateFiles(Path.Combine(root,"Required dlls"))
				.Where(x => Regex.IsMatch(Path.GetFileName(x),@"^(Dicom\..*|fo-dicom.*)\.dll$",RegexOptions.IgnoreCase)).ToList();
			Assert.AreEqual(0,listFound.Count,"Back in Required dlls: "+string.Join(", ",listFound));
			List<string> listViolations=new List<string>();
			foreach(string path in GetFiles(root,".csproj",".vbproj",".vcxproj",".targets",".props",".config")) {
				XDocument xDocument;
				try {
					xDocument=XDocument.Load(path);
				}
				catch(Exception) {
					continue;//not XML
				}
				foreach(XElement xElement in xDocument.Descendants()) {
					string value=xElement.Name.LocalName=="HintPath" ? xElement.Value : ((string)xElement.Attribute("Include") ?? (string)xElement.Attribute("name") ?? "");
					if(Regex.IsMatch(value,@"(^|[\\/""])(Dicom\.(Core|Native)|fo-dicom)",RegexOptions.IgnoreCase)) {
						listViolations.Add(path.Substring(root.Length)+": <"+xElement.Name.LocalName+"> "+value);
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"fo-dicom is named again: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void FoDicom_IsNotUsedBySource() {
			string root=RepoRoot();
			Regex regexUsing=new Regex(@"^\s*using\s+(static\s+)?(Dicom|FellowOakDicom)(\.\w+)*\s*;",RegexOptions.Multiline);
			List<string> listViolations=new List<string>();
			foreach(string path in GetFiles(root,".cs")) {
				foreach(Match match in regexUsing.Matches(File.ReadAllText(path))) {
					listViolations.Add(path.Substring(root.Length)+": "+match.Value.Trim());
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Source uses fo-dicom: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void FoDicom_IsNotInABuildOutput() {
			string root=RepoRoot();
			List<string> listFolders=new List<string> { AppDomain.CurrentDomain.BaseDirectory,Path.Combine(root,"OpenPmsDentalBusiness","bin","Debug") };
			string folderProgram=Path.Combine(root,"OpenPmsDental","bin","Debug");
			if(File.Exists(Path.Combine(folderProgram,"OpenPmsDental.exe"))) {
				listFolders.Add(folderProgram);
			}
			List<string> listFound=listFolders.Where(Directory.Exists).SelectMany(x => _arrayFiles.Select(y => Path.Combine(x,y))).Where(File.Exists).ToList();
			Assert.AreEqual(0,listFound.Count,"In a build output folder: "+string.Join(", ",listFound));
		}

		[TestMethod]
		public void Release_RefusesFoDicom_TheInstallerRemovesIt_AndTheNoticesDoNotListIt() {
			string root=RepoRoot();
			string script=File.ReadAllText(Path.Combine(root,"tools","Build-Release.ps1"));
			foreach(string name in _arrayFiles) {
				Assert.IsFalse(script.Contains("'"+name+"'"),"tools\\Build-Release.ps1 still ships "+name);
			}
			StringAssert.Contains(script,"@('Dicom.*.dll',");
			StringAssert.Contains(script,"@('fo-dicom*',");
			string iss=File.ReadAllText(Path.Combine(root,"installer","OpenPmsDental.iss"));
			Match matchDelete=Regex.Match(iss,@"(?ms)^\[InstallDelete\]\s*$(.*?)(?=^\[|\z)");
			Assert.IsTrue(matchDelete.Success,"The installer has an [InstallDelete] section.");
			foreach(string name in _arrayFiles) {
				StringAssert.Contains(matchDelete.Groups[1].Value,"Type: files; Name: \"{app}\\"+name+"\"","An upgrade removes "+name+".");
			}
			string notices=File.ReadAllText(Path.Combine(root,"THIRD-PARTY-NOTICES.txt"));
			Assert.IsFalse(notices.Contains("fo-dicom"));
			Assert.IsFalse(notices.Contains("Dicom.Core") || notices.Contains("Dicom.Native"));
			Assert.IsFalse(notices.Contains("--- License: Microsoft Public License"),"No shipped component is under the MS-PL any more.");
		}
	}
}
