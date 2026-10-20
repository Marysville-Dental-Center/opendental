// Checks on the built program and on the source: version numbers, the frozen legacy chain, no Debug bypass, no Open Dental update files, no
// downgrade prompt.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class VersionSourceTests {
		[TestMethod]
		public void OpenDentalExe_ReleaseVersionIs_0_0_AndCompatVersionStaysOn_24_3_63() {
			string pathExe=Path.Combine(TestPaths.RepoRoot,"OpenDental","bin","Debug","OpenDental.exe");
			if(!File.Exists(pathExe)) {
				Assert.Inconclusive("OpenDental is not built.");
			}
			FileVersionInfo fileVersionInfo=FileVersionInfo.GetVersionInfo(pathExe);
			Assert.AreEqual("0.0",fileVersionInfo.ProductVersion,"AssemblyInformationalVersion: the release version shown in About.");
			Assert.AreEqual("24.3.63.0",fileVersionInfo.FileVersion,"The compatibility version used by the database checks.");
			string assemblyInfo=TestPaths.ReadSource("OpenDental","Properties","AssemblyInfo.cs");
			StringAssert.Contains(assemblyInfo,"[assembly: AssemblyVersion(\"24.3.63.0\")]");
			StringAssert.Contains(assemblyInfo,"[assembly: AssemblyInformationalVersion(\"0.0\")]");
		}

		[TestMethod]
		public void LegacyChain_IsFrozenAt_24_3_63() {
			Assert.AreEqual(ForkVersions.CompatVersion,ConvertDatabases.FrozenVersion);
			Assert.AreEqual(ConvertDatabases.FrozenVersion,ConvertDatabases.LatestVersion,"The last ToX_Y_Z method is 24.3.63.");
			Version versionFromOld=ConvertDatabases.FromVersion;
			try {
				ConvertDatabases.FromVersion=new Version(25,3,48,0);
				try {
					ConvertDatabases.InvokeConvertMethods();
					Assert.Fail("The legacy chain must refuse a database above 24.3.63.");
				}
				catch(ApplicationException ex) {
					StringAssert.Contains(ex.Message,"legacy conversion chain");
				}
			}
			finally {
				ConvertDatabases.FromVersion=versionFromOld;
			}
		}

		[TestMethod]
		public void ForkDocumentMiscTypes_AreFarFromOpenDentals_AndFitTinyint() {
			Assert.AreEqual(0,(int)DocumentMiscType.UpdateFiles);
			Assert.AreEqual(1,(int)DocumentMiscType.UpdateFilesSegment);
			Assert.AreEqual(2,(int)DocumentMiscType.ShareScreenExeSegment);
			Assert.AreEqual(100,(int)DocumentMiscType.ForkReleaseManifest);
			Assert.AreEqual(101,(int)DocumentMiscType.ForkReleaseSegment);
			foreach(DocumentMiscType documentMiscType in Enum.GetValues(typeof(DocumentMiscType))) {
				Assert.IsTrue((int)documentMiscType<=127,"documentmisc.DocMiscType is tinyint(4).");
			}
		}

		[TestMethod]
		public void GuardCode_HasNoDebugBypass() {
			foreach(string fileName in new[] { "ForkVersionGuard.cs","ForkVersions.cs","Adoption.cs","ForkMigrationHandOff.cs" }) {
				string text=StripComments(TestPaths.ReadSource("OpenDentBusiness","Misc","ForkMigrations",fileName));
				Assert.IsFalse(text.Contains("IsDebug"),fileName);
				Assert.IsFalse(text.Contains("#if"),fileName);
			}
			string textPrefL=TestPaths.ReadSource("OpenDental","Logic","PrefL.cs");
			string guardMethod=GetMethodBody(textPrefL,"public static bool CheckForkVersionGuard(");
			Assert.IsFalse(Regex.IsMatch(guardMethod,@"if\(\s*ODBuild\.IsDebug\(\)\s*\)\s*\{?\s*return true"),"The guard never returns early in Debug builds.");
			string textMain=TestPaths.ReadSource("OpenDental","Main Modules","FormOpenDental.cs");
			List<string> listGuardCalls=textMain.Split('\n').Where(x => x.Contains("PrefL.CheckForkVersionGuard(")).ToList();
			Assert.AreEqual(2,listGuardCalls.Count,"Before and after conversion.");
			Assert.IsTrue(listGuardCalls.All(x => !x.Contains("IsDebug")));
			string lineSafe=textMain.Split('\n').Single(x => x.Contains("!IsDbConnectionSafe(out errorMsg)"));
			Assert.IsFalse(lineSafe.Contains("IsDebug"),"The re-activation check runs in Debug builds too.");
			int indexGuard=textMain.IndexOf("PrefL.CheckForkVersionGuard(");
			int indexConvert=textMain.IndexOf("PrefL.ConvertDB(");
			Assert.IsTrue(indexGuard>0 && indexGuard<indexConvert,"The guard runs before any conversion.");
		}

		[TestMethod]
		public void OpenDentalUpdateFileRows_AreOnlyEverDeleted() {
			List<string> listHits=new List<string>();
			foreach(string folder in new[] { "OpenDental","OpenDentBusiness","UpdateFileCopier","WpfControlsOD","CodeBase" }) {
				foreach(string path in GetSourceFiles(Path.Combine(TestPaths.RepoRoot,folder))) {
					string text=StripComments(File.ReadAllText(path));
					if(Regex.IsMatch(text,@"DocumentMiscType\.UpdateFiles(Segment)?\b")) {
						listHits.Add(path.Substring(TestPaths.RepoRoot.Length+1));
					}
				}
			}
			CollectionAssert.AreEqual(new[] { Path.Combine("OpenDentBusiness","Data Interface","DocumentMiscs.cs") },listHits.ToArray(),
				"Only DocumentMiscs may name Open Dental's update file types.");
			string textDocumentMiscs=StripComments(TestPaths.ReadSource("OpenDentBusiness","Data Interface","DocumentMiscs.cs"));
			string bodyDelete=GetMethodBody(textDocumentMiscs,"public static long DeleteOpenDentalUpdateFiles(");
			int countAll=Regex.Matches(textDocumentMiscs,@"DocumentMiscType\.UpdateFiles(Segment)?\b").Count;
			int countDelete=Regex.Matches(bodyDelete,@"DocumentMiscType\.UpdateFiles(Segment)?\b").Count;
			Assert.AreEqual(countAll,countDelete,"Open Dental's update file types appear only in the method that deletes them.");
			StringAssert.Contains(bodyDelete,"DELETE FROM documentmisc");
		}

		[TestMethod]
		public void NoDowngradePrompt_AndNoOpenDentalSetupDownload() {
			foreach(string[] arrayPath in new[] {
				new[] { "OpenDental","Logic","PrefL.cs" },
				new[] { "OpenDental","Misc","ClassConvertDatabase.cs" },
				new[] { "OpenDental","Main Modules","FormOpenDental.cs" },
				new[] { "UpdateFileCopier","FormMain.cs" } })
			{
				string text=StripComments(TestPaths.ReadSource(arrayPath));
				foreach(Match match in Regex.Matches(text,"\"(?:[^\"\\\\]|\\\\.)*\"")) {
					Assert.IsFalse(match.Value.ToLowerInvariant().Contains("downgrade"),string.Join("\\",arrayPath)+": "+match.Value);
				}
			}
			string textPrefL=StripComments(TestPaths.ReadSource("OpenDental","Logic","PrefL.cs"));
			Assert.IsFalse(textPrefL.Contains("Setup.exe"),"PrefL must not run or download Open Dental's setup program.");
			Assert.IsFalse(textPrefL.Contains("LaunchCorrespondingVersion"));
			Assert.IsFalse(textPrefL.Contains("CheckProgramVersionClassic"));
			Assert.IsFalse(textPrefL.Contains("Application.ProductVersion"),"The release version is never compared with database versions.");
			StringAssert.Contains(textPrefL,"TryInstallOpenDentalService");
		}

		[TestMethod]
		public void UpdateFileCopier_ChecksTheReleaseManifest() {
			string textCsproj=TestPaths.ReadSource("UpdateFileCopier","UpdateFileCopier.csproj");
			StringAssert.Contains(textCsproj,@"..\OpenDentBusiness\Misc\ForkRelease\ReleaseManifest.cs");
			string text=StripComments(TestPaths.ReadSource("UpdateFileCopier","FormMain.cs"));
			StringAssert.Contains(text,"VerifyRelease(_sourceDirectory)");
			StringAssert.Contains(text,"VerifyRelease(TempPathSource)");
			int indexVerify=text.IndexOf("VerifyRelease(_sourceDirectory)");
			int indexMove=text.IndexOf("MoveFilesToDestDir(listSourceFiles)");
			Assert.IsTrue(indexVerify<indexMove,"Files are checked before anything is copied into the installation.");
		}

		private static string GetMethodBody(string text,string signature) {
			int indexStart=text.IndexOf(signature);
			Assert.IsTrue(indexStart>=0,signature);
			int indexBrace=text.IndexOf('{',indexStart);
			int depth=0;
			for(int i=indexBrace;i<text.Length;i++) {
				if(text[i]=='{') {
					depth++;
				}
				else if(text[i]=='}') {
					depth--;
					if(depth==0) {
						return text.Substring(indexBrace,i-indexBrace+1);
					}
				}
			}
			return text.Substring(indexBrace);
		}

		///<summary>Removes // and /* */ comments (not inside string literals) so prose in comments does not count.</summary>
		private static string StripComments(string text) {
			return Regex.Replace(text,@"""(?:[^""\\]|\\.)*""|//[^\n]*|/\*.*?\*/",m => m.Value.StartsWith("/") ? "" : m.Value,RegexOptions.Singleline);
		}

		private static IEnumerable<string> GetSourceFiles(string folder) {
			string sep=Path.DirectorySeparatorChar.ToString();
			return Directory.GetFiles(folder,"*.cs",SearchOption.AllDirectories).Where(x => !x.Contains(sep+"bin"+sep) && !x.Contains(sep+"obj"+sep));
		}
	}
}
