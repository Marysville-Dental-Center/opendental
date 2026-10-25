// Procedure Note Merge (Open Dental 25.4) rules: when the merge window is needed, what it starts with, what each button saves, who gets the
// "signature cleared" alert, and what is logged. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2NOTESTests {
	[TestClass]
	public class ProcNoteMergeRulesTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestPrefs.EnsureFilled();
		}

		private static ProcNote Note(long procNoteNum,DateTime dateTimeEntry,string note="",long userNum=0,string signature="",long userNum2=0,string signature2="") {
			return new ProcNote { ProcNoteNum=procNoteNum,ProcNum=10,EntryDateTime=dateTimeEntry,Note=note,UserNum=userNum,Signature=signature,
				UserNum2=userNum2,Signature2=signature2 };
		}

		[TestMethod]
		public void NewerNote_DetectedByNumberOrTime() {
			DateTime date=new DateTime(2026,10,1,9,0,0);
			ProcNote procNoteAtLoad=Note(5,date);
			Assert.IsFalse(ProcNoteMerges.IsNewerNoteSaved(procNoteAtLoad,Note(5,date)),"Same row.");
			Assert.IsTrue(ProcNoteMerges.IsNewerNoteSaved(procNoteAtLoad,Note(6,date)),"Saved in the same second: a new ProcNoteNum.");
			Assert.IsTrue(ProcNoteMerges.IsNewerNoteSaved(procNoteAtLoad,Note(5,date.AddSeconds(1))));
			Assert.IsTrue(ProcNoteMerges.IsNewerNoteSaved(null,Note(1,date)),"The procedure had no note when the window opened.");
			Assert.IsFalse(ProcNoteMerges.IsNewerNoteSaved(null,null));
			Assert.IsFalse(ProcNoteMerges.IsNewerNoteSaved(procNoteAtLoad,null));
		}

		[TestMethod]
		public void MergeNeeded_OnlyWhenNewerNote_AndThisSaveWritesANote() {
			DateTime date=new DateTime(2026,10,1,9,0,0);
			ProcNote procNoteAtLoad=Note(5,date);
			ProcNote procNoteNewer=Note(6,date.AddMinutes(2));
			Assert.IsTrue(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteNewer,isNoteRowWritten:true));
			Assert.IsFalse(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteNewer,isNoteRowWritten:false),"Other edits only: no note row is written.");
			Assert.IsFalse(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteAtLoad,isNoteRowWritten:true),"Nobody else saved.");
		}

		[TestMethod]
		public void NoteTextChanged_IgnoresLineBreakStyle() {
			Assert.IsFalse(ProcNoteMerges.IsNoteTextChanged("A\r\nB","A\nB"));
			Assert.IsFalse(ProcNoteMerges.IsNoteTextChanged(null,""));
			Assert.IsTrue(ProcNoteMerges.IsNoteTextChanged("A\r\nB","A\r\nB."));
		}

		[TestMethod]
		public void AddedText_IsWhatFollowsTheLoadedNote() {
			Assert.AreEqual("Pt tolerated well.",ProcNoteMerges.GetAddedText("Exam done.","Exam done.\r\nPt tolerated well."));
			Assert.AreEqual("Pt tolerated well.",ProcNoteMerges.GetAddedText("Exam done.\r\n","Exam done.\nPt tolerated well."),"Line break styles differ.");
			Assert.AreEqual("Exam redone.\r\nAll good.",ProcNoteMerges.GetAddedText("Exam done.","Exam redone.\nAll good."),"Loaded text changed: the whole note.");
			Assert.AreEqual("New note.",ProcNoteMerges.GetAddedText("","New note."));
			Assert.AreEqual("",ProcNoteMerges.GetAddedText("Same.","Same."));
		}

		[TestMethod]
		public void MergedDefault_TheirsSeparatorThenMine() {
			string merged=ProcNoteMerges.GetMergedDefault("Exam done.\r\nX-rays taken.","Exam done.","Exam done.\r\nPt tolerated well.");
			Assert.AreEqual("Exam done.\r\nX-rays taken.\r\n"+ProcNoteMerges.Separator+"\r\nPt tolerated well.",merged);
			Assert.AreEqual("Exam done.\r\nX-rays taken.",ProcNoteMerges.GetMergedDefault("Exam done.\nX-rays taken.","Exam done.","Exam done."),
				"Nothing added: just their note.");
			Assert.AreEqual("Mine.",ProcNoteMerges.GetMergedDefault("","","Mine."),"Their note is empty.");
			Assert.AreEqual(54,ProcNoteMerges.Separator.Length,"The same line as the older automatic merge and the Chart's audit mode.");
		}

		[TestMethod]
		public void ResultText_PerChoice() {
			Assert.AreEqual("mine",ProcNoteMerges.GetResultText(ProcNoteMergeChoice.KeepMine,"theirs","mine","merged"));
			Assert.AreEqual("theirs",ProcNoteMerges.GetResultText(ProcNoteMergeChoice.KeepTheirs,"theirs","mine","merged"));
			Assert.AreEqual("merged",ProcNoteMerges.GetResultText(ProcNoteMergeChoice.SaveMerged,"theirs","mine","merged"));
			Assert.IsNull(ProcNoteMerges.GetResultText(ProcNoteMergeChoice.Cancel,"theirs","mine","merged"));
		}

		[TestMethod]
		public void Resign_WhenSavedTextDiffersFromSignedText() {
			Assert.IsTrue(ProcNoteMerges.IsResignNeeded(true,"Mine.","Theirs.\r\n---\r\nMine."));
			Assert.IsFalse(ProcNoteMerges.IsResignNeeded(true,"Mine.","Mine."),"Keep Mine keeps the signature.");
			Assert.IsFalse(ProcNoteMerges.IsResignNeeded(true,"Mine.\r\n","Mine.\n"),"Only line breaks differ.");
			Assert.IsFalse(ProcNoteMerges.IsResignNeeded(false,"Mine.","Other."),"Nothing was signed here.");
		}

		[TestMethod]
		public void TheirSigners_AlertedWhenTheirNoteIsReplaced() {
			ProcNote procNoteTheirs=Note(6,DateTime.Now,"Theirs.",userNum:21,signature:"sig",userNum2:22,signature2:"sig2");
			List<long> listActing=new List<long> { 30 };
			CollectionAssert.AreEqual(new List<long> { 21,22 },ProcNoteMerges.GetTheirSignersToAlert(ProcNoteMergeChoice.KeepMine,procNoteTheirs,listActing));
			CollectionAssert.AreEqual(new List<long> { 21,22 },ProcNoteMerges.GetTheirSignersToAlert(ProcNoteMergeChoice.SaveMerged,procNoteTheirs,listActing));
			Assert.AreEqual(0,ProcNoteMerges.GetTheirSignersToAlert(ProcNoteMergeChoice.KeepTheirs,procNoteTheirs,listActing).Count,"Their note is kept.");
			Assert.AreEqual(0,ProcNoteMerges.GetTheirSignersToAlert(ProcNoteMergeChoice.Cancel,procNoteTheirs,listActing).Count);
			CollectionAssert.AreEqual(new List<long> { 22 },ProcNoteMerges.GetTheirSignersToAlert(ProcNoteMergeChoice.KeepMine,procNoteTheirs,new List<long> { 21 }),
				"The acting user is not alerted.");
			ProcNote procNoteUnsigned=Note(7,DateTime.Now,"Theirs.",userNum:21);
			Assert.AreEqual(0,ProcNoteMerges.GetTheirSignersToAlert(ProcNoteMergeChoice.KeepMine,procNoteUnsigned,listActing).Count,"Nothing signed, nothing cleared.");
		}

		[TestMethod]
		public void Log_PermissionAndText() {
			Assert.AreEqual(EnumPermType.ProcCompleteNote,ProcNoteMerges.GetLogPermission(true));
			Assert.AreEqual(EnumPermType.ProcEdit,ProcNoteMerges.GetLogPermission(false));
			Assert.IsTrue(ProcNoteMerges.IsCompleteStatus(ProcStat.C));
			Assert.IsTrue(ProcNoteMerges.IsCompleteStatus(ProcStat.EC));
			Assert.IsTrue(ProcNoteMerges.IsCompleteStatus(ProcStat.EO));
			Assert.IsFalse(ProcNoteMerges.IsCompleteStatus(ProcStat.TP));
			Assert.AreEqual("T1234 10/1/2026: Procedure note merged with changes by hyg1.",
				ProcNoteMerges.GetLogText(ProcNoteMergeChoice.SaveMerged,"T1234 10/1/2026","hyg1"));
			StringAssert.Contains(ProcNoteMerges.GetLogText(ProcNoteMergeChoice.KeepMine,"","hyg1"),"saved over changes by hyg1");
			StringAssert.Contains(ProcNoteMerges.GetLogText(ProcNoteMergeChoice.KeepTheirs,"","hyg1"),"kept the note saved by hyg1");
			Assert.AreEqual("",ProcNoteMerges.GetLogText(ProcNoteMergeChoice.Cancel,"x","hyg1"));
		}
	}
}
