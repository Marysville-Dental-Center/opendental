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
		public void NoteTextChanged_IgnoresLineBreakStyle_ForSignatures() {
			//Used where signatures are concerned: their keys ignore line breaks (Procedures.GetSignatureKeyData).
			Assert.IsFalse(ProcNoteMerges.IsNoteTextChanged("A\r\nB","A\nB"));
			Assert.IsFalse(ProcNoteMerges.IsNoteTextChanged(null,""));
			Assert.IsTrue(ProcNoteMerges.IsNoteTextChanged("A\r\nB","A\r\nB."));
		}

		private static Procedure Proc(string note,long userNum=7,string signature="") {
			return new Procedure { ProcNum=10,PatNum=3,Note=note,UserNum=userNum,Signature=signature };
		}

		[TestMethod]
		public void NoteRowNeeded_TextComparedExactly_AsProceduresUpdateDoes() {
			Procedure procedureOld=Proc("Exam done.\r\nX-rays taken.");
			Assert.IsFalse(ProcNotes.IsNoteRowNeeded(procedureOld.Copy(),procedureOld),"Nothing changed.");
			Procedure procedure=procedureOld.Copy();
			procedure.Note="Exam done.\nX-rays taken.";//What the RichTextBox note box returns for the stored text.
			Assert.IsTrue(ProcNotes.IsNoteRowNeeded(procedure,procedureOld),"Only the line breaks differ, and Procedures.Update still writes a row.");
			procedure=procedureOld.Copy();
			procedure.UserNum=8;
			Assert.IsTrue(ProcNotes.IsNoteRowNeeded(procedure,procedureOld));
			procedure=procedureOld.Copy();
			procedure.Signature="sig";
			Assert.IsTrue(ProcNotes.IsNoteRowNeeded(procedure,procedureOld));
			procedure=procedureOld.Copy();
			procedure.UserNum2=9;
			procedure.Signature2="sig2";
			Assert.IsTrue(ProcNotes.IsNoteRowNeeded(procedure,procedureOld),"26.1 second signature.");
		}

		[TestMethod]
		public void NoteRowWritten_PredictedBeforeSaving_EvenForAnOtherEditOnly() {
			//A note stored with \r\n (the CRUD stores every note that way, POut.StringNote), opened in a window whose note box returns \n. Only the
			//Dx, fee or provider changes: Procedures.Update still writes a row with the older text, so the merge window must be asked for.
			Procedure procedureOld=Proc("TP note.\r\nDefault note.");
			Procedure procedure=procedureOld.Copy();
			procedure.Dx=5;
			Assert.IsTrue(ProcNoteMerges.IsNoteRowWritten(procedure,procedureOld,"TP note.\nDefault note.",isSignatureChanged:false));
			Assert.IsFalse(ProcNoteMerges.IsNoteRowWritten(procedure,procedureOld,"TP note.\r\nDefault note.",isSignatureChanged:false),
				"The same text exactly: no row.");
			Assert.IsTrue(ProcNoteMerges.IsNoteRowWritten(procedure,procedureOld,"TP note.\r\nDefault note.",isSignatureChanged:true),"A signature box changed.");
			Assert.AreEqual(5,procedure.Dx,"The window's procedure is not changed.");
			Assert.AreEqual("TP note.\r\nDefault note.",procedure.Note);
			ProcNote procNoteAtLoad=new ProcNote { ProcNoteNum=5,EntryDateTime=new DateTime(2026,10,1,9,0,0) };
			ProcNote procNoteNewer=new ProcNote { ProcNoteNum=6,EntryDateTime=new DateTime(2026,10,1,9,2,0) };
			Assert.IsTrue(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteNewer,
				ProcNoteMerges.IsNoteRowWritten(procedure,procedureOld,"TP note.\nDefault note.",isSignatureChanged:false)));
		}

		[TestMethod]
		public void KeepTheirs_BothCopiesTakeTheirNoteAsShown_SoNoRowIsWritten() {
			Procedure procedureOld=Proc("Mine as loaded.",userNum:7,signature:"sigMine");
			Procedure procedure=procedureOld.Copy();
			procedure.Note="Mine as loaded.\nAdded.";
			procedure.UserNum=7;
			procedure.Dx=4;//another edit, kept
			ProcNote procNoteTheirs=Note(6,DateTime.Now,"Theirs.\r\nSecond line.",userNum:21,signature:"sigTheirs",userNum2:22,signature2:"sig2");
			procNoteTheirs.SigIsTopaz=true;
			string noteAsShown="Theirs.\nSecond line.";//The note box returns \n.
			ProcNoteMerges.ApplyTheirNote(procedure,procedureOld,procNoteTheirs,noteAsShown);
			Assert.AreEqual(noteAsShown,procedure.Note);
			Assert.AreEqual(noteAsShown,procedureOld.Note);
			Assert.AreEqual(21,procedure.UserNum);
			Assert.AreEqual("sigTheirs",procedure.Signature);
			Assert.IsTrue(procedure.SigIsTopaz);
			Assert.AreEqual(22,procedure.UserNum2);
			Assert.AreEqual("sig2",procedureOld.Signature2);
			Assert.AreEqual(4,procedure.Dx,"Other edits stay.");
			Assert.IsFalse(ProcNotes.IsNoteRowNeeded(procedure,procedureOld));
			Assert.IsFalse(ProcNoteMerges.IsNoteRowWritten(procedure,procedureOld,noteAsShown,isSignatureChanged:false),"A second save does not ask again.");
		}

		[TestMethod]
		public void Baseline_TakesTheNewerNote() {
			Procedure procedureOld=Proc("Old.",userNum:7,signature:"sigOld");
			ProcNote procNoteNow=Note(6,DateTime.Now,"Newer.\r\n",userNum:21,signature:null,userNum2:22,signature2:"sig2");
			ProcNoteMerges.TakeAsBaseline(procedureOld,procNoteNow);
			Assert.AreEqual("Newer.\r\n",procedureOld.Note,"Exactly as stored: Keep Mine and Save Merged compare against it.");
			Assert.AreEqual(21,procedureOld.UserNum);
			Assert.AreEqual("",procedureOld.Signature);
			Assert.AreEqual(22,procedureOld.UserNum2);
			Assert.AreEqual("sig2",procedureOld.Signature2);
		}

		[TestMethod]
		public void ProcedureInfoRule_ForTheNewerNote() {
			Assert.IsTrue(ProcNoteMerges.CanEditProcNote(true,false,30,21,isSigBlank:false),"Procedure Note Full.");
			Assert.IsTrue(ProcNoteMerges.CanEditProcNote(false,true,30,30,isSigBlank:false),"Procedure Note User, own signed note.");
			Assert.IsTrue(ProcNoteMerges.CanEditProcNote(false,true,30,21,isSigBlank:true),"Procedure Note User, not signed.");
			Assert.IsFalse(ProcNoteMerges.CanEditProcNote(false,true,30,21,isSigBlank:false),"Signed by another user.");
			Assert.IsFalse(ProcNoteMerges.CanEditProcNote(false,false,30,30,isSigBlank:true),"Neither permission.");
		}

		[TestMethod]
		public void TextChange_OnlyWhenTheNoteWasEditable_AndTheNewerNoteMayBeEdited() {
			Assert.IsTrue(ProcNoteMerges.CanChangeText(canEditTextInWindow:true,canEditTheirs:true));
			Assert.IsFalse(ProcNoteMerges.CanChangeText(canEditTextInWindow:false,canEditTheirs:true),"A co-signer, whose note box is read-only.");
			Assert.IsFalse(ProcNoteMerges.CanChangeText(canEditTextInWindow:true,canEditTheirs:false),"The newer note is signed by another user.");
			foreach(bool canChangeText in new[] { true,false }) {
				Assert.IsTrue(ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.KeepTheirs,canChangeText));
				Assert.IsTrue(ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.Cancel,canChangeText));
				Assert.AreEqual(canChangeText,ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.KeepMine,canChangeText));
				Assert.AreEqual(canChangeText,ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.SaveMerged,canChangeText));
			}
		}

		[TestMethod]
		public void CoSigner_CannotChangeAnotherUsersSignedNote_ThroughTheMergeWindow() {
			//The hygienist (30) co-signs box 2 of a note the dentist (21) signed. Box 1 is signed by someone else, so the note box is read-only.
			long userNumCoSigner=30;
			bool canEditTextInWindow=GroupNoteSignatures.CanEditNoteText(false,userNumCoSigner,21,isSig1Blank:false,0,isSig2Blank:true);
			Assert.IsFalse(canEditTextInWindow);
			Assert.IsTrue(GroupNoteSignatures.CanSignBox2(false,userNumCoSigner,0,isSig2Blank:true),"Co-signing is allowed.");
			//A newer note was saved meanwhile (for example by the co-signer from a second window, signed in box 2 only).
			ProcNote procNoteNewer=Note(7,DateTime.Now,"Dentist's note.",userNum:21,signature:"sigDoc",userNum2:30,signature2:"sigHyg");
			bool canEditTheirs=GroupNoteSignatures.CanEditNoteText(false,userNumCoSigner,procNoteNewer.UserNum,string.IsNullOrEmpty(procNoteNewer.Signature),
				procNoteNewer.UserNum2,string.IsNullOrEmpty(procNoteNewer.Signature2));
			Assert.IsFalse(canEditTheirs,"Box 1 is the dentist's.");
			bool canChangeText=ProcNoteMerges.CanChangeText(canEditTextInWindow,canEditTheirs);
			Assert.IsFalse(ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.SaveMerged,canChangeText),"No free text over the dentist's signature.");
			Assert.IsFalse(ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.KeepMine,canChangeText),"No older text put back over it.");
			Assert.IsTrue(ProcNoteMerges.IsChoiceAllowed(ProcNoteMergeChoice.KeepTheirs,canChangeText));
			//With Group Note Edit Signed, as in 24.3, the user may.
			Assert.IsTrue(ProcNoteMerges.CanChangeText(true,GroupNoteSignatures.CanEditNoteText(true,userNumCoSigner,21,false,30,false)));
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
