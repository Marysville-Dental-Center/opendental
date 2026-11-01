// Trophy (Enhanced) Command line (Open Dental 25.4, v25_4-bridges-trophy-enhanced-tags) with sample command lines. Nothing is launched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace VBRIDGE2Tests {
	[TestClass]
	public class TrophyEnhancedCommandLineTests {
		private const string PAT_FOLDER=@"\\SERVER\tw\O.rvg\O0000012";

		///<summary>A stand-in for Patients.ReplacePatient covering a few tags, so these tests need no database. The real one is tested in
		///ProgramLinksDbTests.</summary>
		private static string FakeReplacePatient(string text,Patient patient) {
			return text.Replace("[PatNum]",patient.PatNum.ToString()).Replace("[ChartNumber]",patient.ChartNumber)
				.Replace("[LName]",patient.LName).Replace("[FName]",patient.FName);
		}

		[TestMethod]
		public void BlankCommandLine_SendsThe24_3Arguments() {
			Patient patient=SyntheticPatients.Create();
			foreach(string commandLine in new[] { null,""," ","\t" }) {
				string arguments=TrophyEnhancedCommandLine.Build(commandLine,patient,PAT_FOLDER,FakeReplacePatient);
				//No space after -P or -N; quotes removed from the names; "last, first".
				Assert.AreEqual(@"-P\\SERVER\tw\O.rvg\O0000012 -NOTesta, Ann Annie",arguments,"Command line: '"+commandLine+"'");
			}
		}

		[TestMethod]
		public void BlankCommandLine_DoesNotCallTheReplacer() {
			string arguments=TrophyEnhancedCommandLine.Build("",SyntheticPatients.Create(),"F:\\tw\\A.rvg\\A1",
				(text,patient) => throw new AssertFailedException("The tags are not replaced for a blank Command line."));
			Assert.AreEqual("-PF:\\tw\\A.rvg\\A1 -NOTesta, Ann Annie",arguments);
		}

		[TestMethod]
		public void CommandLine_ReplacesPatientTagsAndTrophyFolder() {
			Patient patient=SyntheticPatients.Create();
			Assert.AreEqual("-D1042",TrophyEnhancedCommandLine.Build("-D[PatNum]",patient,"",FakeReplacePatient));
			Assert.AreEqual(@"-P""\\SERVER\tw\O.rvg\O0000012"" -ICH-77",
				TrophyEnhancedCommandLine.Build(@"-P""[TrophyFolder]"" -I[ChartNumber]",patient,PAT_FOLDER,FakeReplacePatient));
			Assert.AreEqual(@"-P\\SERVER\tw\O.rvg\O0000012 -NO'Testa",
				TrophyEnhancedCommandLine.Build("-P[TrophyFolder] -N[LName]",patient,PAT_FOLDER,FakeReplacePatient),
				"The Command line sends what the office wrote; only the 24.3 arguments strip quotes.");
		}

		[TestMethod]
		public void TrophyFolderTag_AnyCaseAndEveryOccurrence() {
			string arguments=TrophyEnhancedCommandLine.Build("[trophyfolder] [TROPHYFOLDER] [TrophyFolder]",SyntheticPatients.Create(),"X:\\p",
				FakeReplacePatient);
			Assert.AreEqual("X:\\p X:\\p X:\\p",arguments);
		}

		[TestMethod]
		public void TrophyFolderTag_DollarSignsAreLiteral() {
			string arguments=TrophyEnhancedCommandLine.Build("-P[TrophyFolder]",SyntheticPatients.Create(),@"\\srv\share$\$1\G.rvg",FakeReplacePatient);
			Assert.AreEqual(@"-P\\srv\share$\$1\G.rvg",arguments);
		}

		[TestMethod]
		public void TrophyFolderTag_BlankWhenFoldersAreSkipped() {
			Assert.AreEqual("-D1042 -P",TrophyEnhancedCommandLine.Build(" -D[PatNum] -P[TrophyFolder] ",SyntheticPatients.Create(),"",FakeReplacePatient));
			Assert.AreEqual("-D1042",TrophyEnhancedCommandLine.Build("-D[PatNum][TrophyFolder]",SyntheticPatients.Create(),null,FakeReplacePatient));
		}

		[TestMethod]
		public void SkipsFolders_OnlyWithACommandLineAndABlankStoragePath() {
			Assert.IsTrue(TrophyEnhancedCommandLine.SkipsFolders("-D[PatNum]",""));
			Assert.IsTrue(TrophyEnhancedCommandLine.SkipsFolders("-D[PatNum]","  "));
			Assert.IsFalse(TrophyEnhancedCommandLine.SkipsFolders("-D[PatNum]",@"F:\tw"),"A Storage Path keeps the folder steps.");
			Assert.IsFalse(TrophyEnhancedCommandLine.SkipsFolders("",""),"A blank Command line keeps the 24.3 storage path check.");
			Assert.IsFalse(TrophyEnhancedCommandLine.SkipsFolders(null,null));
		}

		[TestMethod]
		public void UsesCommandLine_IgnoresWhiteSpace() {
			Assert.IsFalse(TrophyEnhancedCommandLine.UsesCommandLine(null));
			Assert.IsFalse(TrophyEnhancedCommandLine.UsesCommandLine(" \r\n"));
			Assert.IsTrue(TrophyEnhancedCommandLine.UsesCommandLine("-D[PatNum]"));
		}

		[TestMethod]
		public void Tidy_RemovesQuotes() {
			Assert.AreEqual("OBrien Jr",TrophyEnhancedCommandLine.Tidy("O'Brien \"Jr\""));
			Assert.AreEqual("",TrophyEnhancedCommandLine.Tidy(null));
		}

		[TestMethod]
		public void Build_NeedsAPatient() {
			Expect.Throws<ArgumentNullException>(() => TrophyEnhancedCommandLine.Build("-D[PatNum]",null,"",FakeReplacePatient));
		}

		[TestMethod]
		public void CommandLineHint_NamesTheTags() {
			StringAssert.Contains(TrophyEnhancedCommandLine.CommandLineHint,TrophyEnhancedCommandLine.TagTrophyFolder);
			StringAssert.Contains(TrophyEnhancedCommandLine.CommandLineHint,"[PatNum]");
			StringAssert.Contains(TrophyEnhancedCommandLine.CommandLineHint,"Storage Path");
		}
	}
}
