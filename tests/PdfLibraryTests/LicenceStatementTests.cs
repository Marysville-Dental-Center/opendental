// What the licence statements say. PdfSharp.License.dll no longer blocks a release: PDFsharp is built from empira's source with an open licence
// check (third-party\build), so tools\Build-Release.ps1 has no release blocker and no -PdfSharpLicenceResolved, denies the file, and the owner
// questions record the decision (route B, 2026-11-02). Also what the statements say about the AGPL's network clause, and empira's own reading
// of its licence, which stays a dispute risk for counsel. Nothing is built; the script runs only its list checks.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PdfLibraryTests {
	[TestClass]
	public class LicenceStatementTests {
		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(SourceBuild.RepoRoot,Path.Combine(arrayParts)));
		}

		///<summary>The text with every run of white space (line breaks of wrapped text too) as one space.</summary>
		private static string Unwrap(string text) {
			return Regex.Replace(text,@"\s+"," ");
		}

		[TestMethod]
		public void BuildRelease_HasNoReleaseBlocker_AndDeniesPdfSharpLicenseDll() {
			string script=ReadRepoFile("tools","Build-Release.ps1");
			foreach(string gone in new[] { "PdfSharpLicenceResolved","ReleaseBlockers","Get-ReleaseBlockers","Release blocked" }) {
				Assert.IsFalse(script.Contains(gone),"Build-Release.ps1 still has "+gone);
			}
			string folder=Path.Combine(Path.GetTempPath(),"PdfLibraryTests_gate_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				//An empty folder fails the list checks (every allowed file is missing), and nothing else: no blocker, no licence file.
				SourceBuild.ProcessResult processResult=SourceBuild.BuildRelease("-CheckFolder","\""+folder+"\"");
				Assert.AreEqual(1,processResult.ExitCode,processResult.Output);
				Assert.IsFalse(processResult.Lines.Any(x => x.StartsWith("Release blocked")),processResult.Output);
				Assert.IsFalse(processResult.Output.Contains("PdfSharp.License.dll"),processResult.Output);
				CollectionAssert.Contains(processResult.Lines,"Missing: PdfSharp-gdi.dll (every PDF the program makes or reads goes through it; it holds the AGPL "
					+"licence check that PdfSharpSetup.Init satisfies)");
				Match match=Regex.Match(processResult.Output,@"The check failed, (\d+) problem\(s\):");
				Assert.IsTrue(match.Success,processResult.Output);
				Assert.AreEqual(processResult.Lines.Count(x => x.StartsWith("Missing: ")),int.Parse(match.Groups[1].Value),"Only the missing files.");
				//empira's prebuilt file in a staging folder is refused.
				File.WriteAllText(Path.Combine(folder,"PdfSharp.License.dll"),"synthetic");
				processResult=SourceBuild.BuildRelease("-CheckFolder","\""+folder+"\"");
				Assert.AreEqual(1,processResult.ExitCode,processResult.Output);
				Assert.IsTrue(processResult.Lines.Any(x => x.StartsWith("Denied file: PdfSharp.License.dll (empira's closed licence check of PDFsharp 6.5")),
					processResult.Output);
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void OwnerQuestions_RecordRouteB_TheResolvedQuestions_AndTheDisputeRisk() {
			string ownerQuestions=ReadRepoFile("docs","owner-questions.md");
			Match match=Regex.Match(ownerQuestions,@"(?m)^- \(LIB-PDF; \*\*resolved on 2026-11-02 by building from source\*\*\) \*\*`PdfSharp\.License\.dll` had no published source\.\*\*(.*)$");
			Assert.IsTrue(match.Success,"The question of PdfSharp.License.dll, resolved.");
			string question=match.Groups[1].Value;
			StringAssert.Contains(question,"You chose route B on 2026-11-02");
			StringAssert.Contains(question,"`third-party/build/PdfSharp-gdi/PdfSharpLicense.cs`");
			StringAssert.Contains(question,"the release blocker and `-PdfSharpLicenceResolved` are gone");
			StringAssert.Contains(question,"`OpenPmsDental-<version>-source.zip`");
			Assert.IsFalse(question.Contains("**Today:** it blocks the next release."));
			//The spell checker: built from source, and no MPL text.
			StringAssert.Contains(ownerQuestions,"(this release; library updates; **decided on 2026-11-02: built from source**) **The source of the spell checker.**");
			StringAssert.Contains(ownerQuestions,"No MPL 1.1 text ships: **decided by you on 2026-11-02**, no longer a default.");
			//empira's reading of its licence stays on record as a risk for counsel.
			StringAssert.Contains(ownerQuestions,"\"Requires your project to be licensed with AGPL, too\"");
			StringAssert.Contains(ownerQuestions,"**This stays open as a dispute risk for counsel** after route B");
		}

		[TestMethod]
		public void ReleaseProcess_HasNoBlocker_AndAttachesTheSourceArchive() {
			string releaseProcess=ReadRepoFile("docs","release-process.md");
			Assert.IsFalse(releaseProcess.Contains("**Release blocker:**"));
			Assert.IsFalse(releaseProcess.Contains("-PdfSharpLicenceResolved"));
			StringAssert.Contains(releaseProcess,"**Publishing:** attach the setup, the source archive `OpenPmsDental-<version>-source.zip`, `SHA256SUMS.txt` and "
				+"the release notes to the GitHub release of the tag.");
			StringAssert.Contains(releaseProcess,"`PdfSharp.License.dll` is on the deny-list");
		}

		[TestMethod]
		public void LicenceStatements_ApplyTheNetworkClauseToTheWholeProgram_AndNameEmpirasReading() {
			//GPL 3.0 section 13: the AGPL's network requirements "will apply to the combination as such". The owner question quotes it.
			string gpl3=Unwrap(ReadRepoFile("LICENSE-GPL-3.0.txt"));
			const string quote="concerning interaction through a network will apply to the combination as such";
			StringAssert.Contains(gpl3,quote);
			string ownerQuestions=ReadRepoFile("docs","owner-questions.md");
			Match match=Regex.Match(ownerQuestions,@"(?m)^- \(LIB-PDF, your decision of 2026-11-02 to use PDFsharp/MigraDoc 6\.5[^)]*\) \*\*What the AGPL changes\.\*\*(.*)$");
			Assert.IsTrue(match.Success,"The owner question of the AGPL.");
			string question=match.Groups[1].Value;
			StringAssert.Contains(question,"\""+quote+"\"");
			StringAssert.Contains(question,"the Corresponding Source of the whole modified program");
			StringAssert.Contains(question,"is not settled");
			StringAssert.Contains(question,"`DtoProcessor` now calls `PdfSharpSetup.Init`");
			//empira's own reading (https://pdfsharp.com/Pricing), and what its commercial licence covers.
			StringAssert.Contains(question,"\"Requires your project to be licensed with AGPL, too\"");
			StringAssert.Contains(question,"\"Use in closed-source projects prohibited\"");
			StringAssert.Contains(question,"EUR 750 excluding VAT");
			StringAssert.Contains(question,"6.5.x and 7.0.x");
			string notice=Unwrap(ReadRepoFile("NOTICE"));
			StringAssert.Contains(notice,"apply to the combination as such, that is, to the program as a whole and not only to PDFsharp and MigraDoc");
			StringAssert.Contains(notice,"must offer those users the Corresponding Source of the whole modified program");
			StringAssert.Contains(notice,"is not settled");
			StringAssert.Contains(ReadRepoFile("README.md"),"apply to the combination as a whole");
		}
	}
}
