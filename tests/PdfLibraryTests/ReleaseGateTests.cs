// PdfSharp.License.dll blocks the next release: empira publishes it only prebuilt, so its Corresponding Source cannot be supplied under the
// GNU GPL 3.0 and AGPL 3.0 (sections 1 and 6). tools\Build-Release.ps1 refuses a release while the file is on its allow-list, unless the owner's
// resolution is recorded with -PdfSharpLicenceResolved; the owner question and the release process say so. Also what the licence statements say
// about the AGPL's network clause, and empira's own reading of its licence. Nothing is built; the script runs only its list checks.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PdfLibraryTests {
	[TestClass]
	public class ReleaseGateTests {
		///<summary>The line Build-Release.ps1 prints for the blocker, up to the reason.</summary>
		private const string BLOCKED_LINE="Release blocked: PdfSharp.License.dll (";

		private static string RepoRoot {
			get { return Path.GetDirectoryName(Path.GetDirectoryName(TestDb.FindRepoFile(Path.Combine("tools","Build-Release.ps1")))); }
		}

		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(RepoRoot,Path.Combine(arrayParts)));
		}

		///<summary>The text with every run of white space (line breaks of wrapped text too) as one space.</summary>
		private static string Unwrap(string text) {
			return Regex.Replace(text,@"\s+"," ");
		}

		///<summary>Runs tools\Build-Release.ps1 with the arguments and returns its exit code and its output lines, trimmed.</summary>
		private static int RunBuildRelease(out List<string> listLines,params string[] arrayArgs) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe");
			processStartInfo.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(RepoRoot,"tools","Build-Release.ps1")+"\" "
				+string.Join(" ",arrayArgs);
			processStartInfo.UseShellExecute=false;
			processStartInfo.RedirectStandardOutput=true;
			processStartInfo.RedirectStandardError=true;
			processStartInfo.CreateNoWindow=true;
			using(Process process=Process.Start(processStartInfo)) {
				Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				Task<string> taskErr=process.StandardError.ReadToEndAsync();
				if(!process.WaitForExit(120000)) {
					process.Kill();
					Assert.Fail("Build-Release.ps1 did not finish in 2 minutes.");
				}
				process.WaitForExit();
				listLines=(taskOut.Result+taskErr.Result).Split(new[] { "\r\n","\n" },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
				return process.ExitCode;
			}
		}

		[TestMethod]
		public void CheckFolder_RefusesTheRelease_UntilTheOwnersResolutionIsGiven() {
			//An empty folder fails the list checks anyway (every allowed file is missing); only the blocker line differs between the two runs.
			string folder=Path.Combine(Path.GetTempPath(),"PdfLibraryTests_gate_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				List<string> listLines;
				int exitCode=RunBuildRelease(out listLines,"-CheckFolder","\""+folder+"\"");
				string output=string.Join("\r\n",listLines);
				Assert.AreEqual(1,exitCode,output);
				Assert.AreEqual(1,listLines.Count(x => x.StartsWith(BLOCKED_LINE)),output);
				string blockedLine=listLines.First(x => x.StartsWith(BLOCKED_LINE));
				StringAssert.Contains(blockedLine,"-PdfSharpLicenceResolved");
				StringAssert.Contains(blockedLine,"docs/owner-questions.md");
				StringAssert.Contains(blockedLine,"6.2.4 (MIT)");
				CollectionAssert.Contains(listLines,"Missing: PdfSharp.License.dll (PdfSharp-gdi.dll checks the licence through it before it makes or reads any PDF)");
				int exitCodeResolved=RunBuildRelease(out listLines,"-CheckFolder","\""+folder+"\"","-PdfSharpLicenceResolved");
				output=string.Join("\r\n",listLines);
				Assert.AreEqual(1,exitCodeResolved,output);
				Assert.IsFalse(listLines.Any(x => x.StartsWith("Release blocked:")),output);
				Assert.IsFalse(listLines.Any(x => x==""),"No empty problem line.\r\n"+output);
				Match match=Regex.Match(output,@"The check failed, (\d+) problem\(s\):");
				Assert.IsTrue(match.Success,output);
				Assert.AreEqual(listLines.Count(x => x.StartsWith("Missing: ")),int.Parse(match.Groups[1].Value),"Only the missing files.\r\n"+output);
			}
			finally {
				Directory.Delete(folder,true);
			}
		}

		[TestMethod]
		public void BuildRelease_ChecksTheBlockers_BeforeItBuilds_AndAgainInTheStagingCheck() {
			string script=ReadRepoFile("tools","Build-Release.ps1");
			StringAssert.Contains(script,"[switch]$PdfSharpLicenceResolved");
			StringAssert.Contains(script,",@('PdfSharp.License.dll','PdfSharpLicenceResolved',");
			int indexBlockers=script.IndexOf("#region Release blockers");
			int indexBuild=script.IndexOf("#region Build");
			Assert.IsTrue(indexBlockers>0 && indexBuild>indexBlockers,"The release blockers are checked before the build.");
			string regionBlockers=script.Substring(indexBlockers,script.IndexOf("#endregion Release blockers")-indexBlockers);
			StringAssert.Contains(regionBlockers,"$blockers=Get-ReleaseBlockers");
			StringAssert.Contains(regionBlockers,"exit 1");
			int indexStagingCheck=script.IndexOf("function Test-StagingFolder(");
			string stagingCheck=script.Substring(indexStagingCheck,script.IndexOf("function Write-Problems(")-indexStagingCheck);
			StringAssert.Contains(stagingCheck,"$blockers=Get-ReleaseBlockers");
			//The file is still shipped when the owner resolves the question; the blocker does not take it off the allow-list.
			StringAssert.Contains(script,"'PdfSharp.License.dll','PdfSharp.Shared.dll'");
		}

		[TestMethod]
		public void OwnerQuestion_AndReleaseProcess_SayTheFileBlocksTheNextRelease() {
			string ownerQuestions=ReadRepoFile("docs","owner-questions.md");
			Match match=Regex.Match(ownerQuestions,@"(?m)^- \(LIB-PDF\) \*\*`PdfSharp\.License\.dll` has no published source; it blocks the next release\.\*\*(.*)$");
			Assert.IsTrue(match.Success,"The owner question of PdfSharp.License.dll.");
			string question=match.Groups[1].Value;
			StringAssert.Contains(question,"**Today:** it blocks the next release.");
			StringAssert.Contains(question,"`-PdfSharpLicenceResolved`");
			StringAssert.Contains(question,"6.2.4 (MIT)");
			Assert.IsFalse(question.Contains("**Today:** it ships"),"The file no longer ships by default.");
			string releaseProcess=ReadRepoFile("docs","release-process.md");
			StringAssert.Contains(releaseProcess,"**Release blocker:** while `PdfSharp.License.dll` is on the allow-list");
			StringAssert.Contains(releaseProcess,"`-PdfSharpLicenceResolved`");
		}

		[TestMethod]
		public void LicenceStatements_ApplyTheNetworkClauseToTheWholeProgram_AndNameEmpirasReading() {
			//GPL 3.0 section 13: the AGPL's network requirements "will apply to the combination as such". The owner question quotes it.
			string gpl3=Unwrap(ReadRepoFile("LICENSE-GPL-3.0.txt"));
			const string quote="concerning interaction through a network will apply to the combination as such";
			StringAssert.Contains(gpl3,quote);
			string ownerQuestions=ReadRepoFile("docs","owner-questions.md");
			Match match=Regex.Match(ownerQuestions,@"(?m)^- \(LIB-PDF, your decision of 2026-11-02 to use PDFsharp/MigraDoc 6\.5\) \*\*What the AGPL changes\.\*\*(.*)$");
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
