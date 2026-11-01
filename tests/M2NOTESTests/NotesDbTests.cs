// Database tests for package M2-NOTES on a scratch database (see ScratchDb): the second signature is saved with the note row, cleared when the text
// changes, kept when only the first signature changes (even by a caller that never loaded it), counts as signed, and shows in the progress notes;
// the newest note is found for Procedure Note Merge, and Keep Theirs writes no extra row. Synthetic rows only. Skipped (inconclusive) when the local
// MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental.UI;
using OpenDentBusiness;

namespace M2NOTESTests {
	[TestClass]
	public class NotesDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		private static long _codeNumGroup;
		private static long _codeNumA;
		private static long _provNum;
		private static long _userNumHyg;
		private static long _userNumDoc;
		private static long _userNumOther;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				if(!Prefs.DictIsNull()) {
					Prefs.ClearCache();//The tests without a database may have filled it with a few values.
				}
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
				return;
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			//Synthetic code, provider and users, so nothing depends on the seed rows.
			_codeNumA=OpenDentBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="M2N01",Descript="Notes test A",AbbrDesc="M2NA",
				ProcTime="/X/",TreatArea=TreatmentArea.Mouth });
			_provNum=OpenDentBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="M2N",LName="Notes",FName="Test",ItemOrder=90 });
			_userNumHyg=OpenDentBusiness.Crud.UserodCrud.Insert(new Userod { UserName="m2hyg" });
			_userNumDoc=OpenDentBusiness.Crud.UserodCrud.Insert(new Userod { UserName="m2doc" });
			_userNumOther=OpenDentBusiness.Crud.UserodCrud.Insert(new Userod { UserName="m2other" });
			Cache.Refresh(InvalidType.Prefs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.Defs);
			Userods.RefreshCache();
			_codeNumGroup=ProcedureCodes.GetCodeNum(ProcedureCodes.GroupProcCode);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("M2NOTES_TEST_KEEP_DB")=="1") {
				return;
			}
			try {
				ScratchDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless to leave behind; the name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=new Userod { UserNum=_userNumHyg,UserName="m2hyg" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static long NewPatient() {
			return OpenDentBusiness.Crud.PatientCrud.Insert(new Patient { LName="Notes",FName="Test"+Guid.NewGuid().ToString("N").Substring(0,6),
				PatStatus=PatientStatus.Patient,PriProv=_provNum,Birthdate=new DateTime(1980,1,1) });
		}

		private static Procedure NewProc(long patNum,long codeNum,ProcStat procStat,DateTime date) {
			Procedure procedure=new Procedure { PatNum=patNum,CodeNum=codeNum,ProcStatus=procStat,ProcDate=date,DateTP=date,DateEntryC=date,ProvNum=_provNum,
				ToothNum="",Surf="",ToothRange="" };
			procedure.ProcNum=OpenDentBusiness.Crud.ProcedureCrud.Insert(procedure);
			return procedure;
		}

		///<summary>A group note on one completed procedure.</summary>
		private static Procedure NewGroupNote(long patNum,DateTime date) {
			Procedure procA=NewProc(patNum,_codeNumA,ProcStat.C,date);
			Procedure procedureGroup=NewProc(patNum,_codeNumGroup,ProcStat.EC,date);
			ProcGroupItems.Insert(new ProcGroupItem { GroupNum=procedureGroup.ProcNum,ProcNum=procA.ProcNum });
			return procedureGroup;
		}

		private static string Sign(string keyData) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetPointList(new List<PointF> { new PointF(10,10),new PointF(20,15),new PointF(35,12),new PointF(50,30) });
				return signatureBox.GetSigString();
			}
		}

		private static bool IsValid(string keyData,string signature) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetSigString(signature);
				return signatureBox.NumberOfTabletPoints()>0;
			}
		}

		///<summary>Saves the group note as the Group Note window does: note text, the first box signed by the hygienist, the second by the dentist.</summary>
		private static Procedure SaveSignedByTwo(Procedure procedureGroup,string note) {
			Procedure procedureOld=Procedures.GetOneProc(procedureGroup.ProcNum,true);
			Procedure procedure=procedureOld.Copy();
			procedure.Note=note;
			procedure.UserNum=_userNumHyg;
			procedure.UserNum2=_userNumDoc;
			List<ProcGroupItem> listProcGroupItems=ProcGroupItems.GetForGroup(procedure.ProcNum);
			procedure.Signature=Sign(GroupNotes.GetSignatureKey(procedure,listProcGroupItems));
			procedure.Signature2=Sign(GroupNoteSignatures.GetSignatureKey2(procedure,listProcGroupItems));
			Procedures.Update(procedure,procedureOld);
			return procedure;
		}

		private static int CountNotes(long procNum) {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM procnote WHERE ProcNum="+POut.Long(procNum)));
		}

		[TestMethod]
		public void Newest_LatestTimeThenHighestNumber() {
			long patNum=NewPatient();
			Procedure procedure=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,8,1));
			Assert.IsNull(ProcNotes.GetNewestForProc(procedure.ProcNum),"No note yet.");
			long procNoteNumA=ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procedure.ProcNum,Note="A" });
			long procNoteNumB=ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procedure.ProcNum,Note="B" });
			long procNoteNumC=ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procedure.ProcNum,Note="C" });
			long procNoteNumOld=ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procedure.ProcNum,Note="Old" });
			DataCore.NonQ("UPDATE procnote SET EntryDateTime='2026-08-01 10:00:00' WHERE ProcNoteNum="+POut.Long(procNoteNumA));
			DataCore.NonQ("UPDATE procnote SET EntryDateTime='2026-08-01 11:00:00' WHERE ProcNoteNum IN("+POut.Long(procNoteNumB)+","+POut.Long(procNoteNumC)+")");
			DataCore.NonQ("UPDATE procnote SET EntryDateTime='2025-01-01 11:00:00' WHERE ProcNoteNum="+POut.Long(procNoteNumOld));
			ProcNote procNoteNewest=ProcNotes.GetNewestForProc(procedure.ProcNum);
			Assert.AreEqual(procNoteNumC,procNoteNewest.ProcNoteNum,"Saved in the same second as B, with the higher number.");
			Assert.AreEqual("C",procNoteNewest.Note);
			Assert.IsNull(ProcNotes.GetNewestForProc(0));
		}

		[TestMethod]
		public void SecondSignature_SavedWithTheNoteRow_AndValid() {
			long patNum=NewPatient();
			Procedure procedureGroup=NewGroupNote(patNum,new DateTime(2026,8,2));
			SaveSignedByTwo(procedureGroup,"Synthetic group note, signed by two.");
			ProcNote procNote=ProcNotes.GetNewestForProc(procedureGroup.ProcNum);
			Assert.AreEqual(_userNumHyg,procNote.UserNum);
			Assert.AreEqual(_userNumDoc,procNote.UserNum2);
			Assert.IsFalse(string.IsNullOrEmpty(procNote.Signature2));
			Assert.IsFalse(procNote.SigIsTopaz2);
			//Loaded back the way the Group Note window gets it: both signatures validate.
			Procedure procedureLoaded=Procedures.GetManyProc(new List<long> { procedureGroup.ProcNum },true).Single();
			Assert.AreEqual(_userNumDoc,procedureLoaded.UserNum2);
			List<ProcGroupItem> listProcGroupItems=ProcGroupItems.GetForGroup(procedureGroup.ProcNum);
			Assert.IsTrue(IsValid(GroupNotes.GetSignatureKey(procedureLoaded,listProcGroupItems),procedureLoaded.Signature));
			Assert.IsTrue(IsValid(GroupNoteSignatures.GetSignatureKey2(procedureLoaded,listProcGroupItems),procedureLoaded.Signature2));
		}

		[TestMethod]
		public void TextEdited_SecondSignatureCleared() {
			long patNum=NewPatient();
			Procedure procedureGroup=NewGroupNote(patNum,new DateTime(2026,8,3));
			SaveSignedByTwo(procedureGroup,"Synthetic note before the edit.");
			Procedure procedureOld=Procedures.GetManyProc(new List<long> { procedureGroup.ProcNum },true).Single();
			Procedure procedure=procedureOld.Copy();
			procedure.Note+=" Edited.";
			procedure.Signature="";//The first box is cleared by the edit, as in the window. The second box is left as it was loaded.
			Procedures.Update(procedure,procedureOld);
			ProcNote procNote=ProcNotes.GetNewestForProc(procedureGroup.ProcNum);
			Assert.AreEqual("Synthetic note before the edit. Edited.",procNote.Note);
			Assert.AreEqual(0,procNote.UserNum2);
			Assert.IsTrue(string.IsNullOrEmpty(procNote.Signature2),"Made over the old text: cleared with it.");
		}

		[TestMethod]
		public void FirstSignatureOnly_SecondKept_EvenWhenTheCallerNeverLoadedIt() {
			long patNum=NewPatient();
			Procedure procedureGroup=NewGroupNote(patNum,new DateTime(2026,8,4));
			Procedure procedureSaved=SaveSignedByTwo(procedureGroup,"Synthetic note signed by two.");
			//GetOneProc does not select the second signature.
			Procedure procedureOld=Procedures.GetOneProc(procedureGroup.ProcNum,true);
			Assert.AreEqual(0,procedureOld.UserNum2);
			Procedure procedure=procedureOld.Copy();
			procedure.UserNum=_userNumOther;
			procedure.Signature=Sign(GroupNotes.GetSignatureKey(procedure,ProcGroupItems.GetForGroup(procedure.ProcNum)));
			Procedures.Update(procedure,procedureOld);
			ProcNote procNote=ProcNotes.GetNewestForProc(procedureGroup.ProcNum);
			Assert.AreEqual(_userNumOther,procNote.UserNum);
			Assert.AreEqual(_userNumDoc,procNote.UserNum2,"Carried from the newest row.");
			Assert.AreEqual(procedureSaved.Signature2,procNote.Signature2);
			//The window clears a second signature on purpose: the caller set it. Loaded as the Group Note window does (GetManyProc would return the
			//procedure twice here, because both notes were saved in the same second).
			procedureOld=Procedures.GetOneProc(procedureGroup.ProcNum,true);
			ProcNotes.CopySecondSignatureTo(ProcNotes.GetNewestForProc(procedureGroup.ProcNum),procedureOld);
			Assert.AreEqual(_userNumDoc,procedureOld.UserNum2);
			procedure=procedureOld.Copy();
			procedure.UserNum2=0;
			procedure.Signature2="";
			Procedures.Update(procedure,procedureOld);
			Assert.IsTrue(string.IsNullOrEmpty(ProcNotes.GetNewestForProc(procedureGroup.ProcNum).Signature2));
		}

		[TestMethod]
		public void SignedProcNums_SecondSignatureCounts() {
			long patNum=NewPatient();
			Procedure procOnly2=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,8,5));
			Procedure procUnsigned=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,8,5));
			ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procOnly2.ProcNum,Note="Only the second box.",UserNum2=_userNumDoc,Signature2="sig2" });
			ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procUnsigned.ProcNum,Note="Not signed." });
			CollectionAssert.AreEquivalent(new List<long> { procOnly2.ProcNum },
				ProcNotes.GetIsProcNoteSigned(new List<long> { procOnly2.ProcNum,procUnsigned.ProcNum }));
		}

		[TestMethod]
		public void ProgressNotes_SignedColumnAndAuditMode() {
			long patNum=NewPatient();
			Procedure procedureBoth=NewGroupNote(patNum,new DateTime(2026,8,6));
			SaveSignedByTwo(procedureBoth,"Synthetic note signed by two.");
			Procedure procOnly2=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,8,6));
			ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procOnly2.ProcNum,Note="Only the second box.",UserNum=_userNumHyg,UserNum2=_userNumDoc,
				Signature2="sig2" });
			Procedure procOnly1=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,8,6));
			ProcNotes.Insert(new ProcNote { PatNum=patNum,ProcNum=procOnly1.ProcNum,Note="Only the first box.",UserNum=_userNumHyg,Signature="sig1" });
			DataTable table=ChartModules.GetProgNotes(patNum,false,new ChartModuleFilters());
			Dictionary<long,DataRow> dictRows=table.Rows.Cast<DataRow>().Where(x => PIn.Long(x["ProcNum"].ToString())!=0)
				.ToDictionary(x => PIn.Long(x["ProcNum"].ToString()));
			Assert.AreEqual("Signed: m2hyg, m2doc",dictRows[procedureBoth.ProcNum]["signature"].ToString());
			Assert.AreEqual("m2hyg",dictRows[procedureBoth.ProcNum]["user"].ToString());
			Assert.AreEqual("Signed: m2doc",dictRows[procOnly2.ProcNum]["signature"].ToString(),"Signed when either box is signed.");
			Assert.AreEqual("Signed",dictRows[procOnly1.ProcNum]["signature"].ToString(),"Unchanged when only the first box is signed.");
			DataTable tableAudit=ChartModules.GetProgNotes(patNum,true,new ChartModuleFilters());
			DataRow rowAudit=tableAudit.Rows.Cast<DataRow>().First(x => PIn.Long(x["ProcNum"].ToString())==procOnly2.ProcNum);
			StringAssert.Contains(rowAudit["note"].ToString(),"(signature 2: m2doc)");
		}

		[TestMethod]
		public void Merge_NewerNoteFound_KeepTheirsWritesNoExtraRow() {
			long patNum=NewPatient();
			Procedure procedure=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,8,7));
			Procedure procedureOld=Procedures.GetOneProc(procedure.ProcNum,true);
			Procedure procedureFirst=procedureOld.Copy();
			procedureFirst.Note="Exam done.";
			procedureFirst.UserNum=_userNumHyg;
			Procedures.Update(procedureFirst,procedureOld);
			//Window A opens.
			Procedure procedureA=Procedures.GetOneProc(procedure.ProcNum,true);
			ProcNote procNoteAtLoad=ProcNotes.GetNewestForProc(procedure.ProcNum);
			//Window B opens, adds text, saves.
			Procedure procedureBOld=Procedures.GetOneProc(procedure.ProcNum,true);
			Procedure procedureB=procedureBOld.Copy();
			procedureB.Note="Exam done.\r\nX-rays taken.";
			procedureB.UserNum=_userNumDoc;
			Procedures.Update(procedureB,procedureBOld);
			//Window A saves its own addition: a newer note is there.
			ProcNote procNoteNow=ProcNotes.GetNewestForProc(procedure.ProcNum);
			string noteMine="Exam done.\r\nPt tolerated well.";
			Assert.IsTrue(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteNow,ProcNoteMerges.IsNoteTextChanged(procedureA.Note,noteMine)));
			Assert.AreEqual("Exam done.\r\nX-rays taken.\r\n"+ProcNoteMerges.Separator+"\r\nPt tolerated well.",
				ProcNoteMerges.GetMergedDefault(procNoteNow.Note,procedureA.Note,noteMine));
			Assert.IsFalse(ProcNoteMerges.IsMergeNeeded(procNoteNow,ProcNotes.GetNewestForProc(procedure.ProcNum),true),"Nothing newer after the merge.");
			//Keep Theirs, as both windows do it (ProcNoteMerges.ApplyTheirNote, with their note as the note box returns it): no row is written.
			int countBefore=CountNotes(procedure.ProcNum);
			Procedure procedureAOld=procedureA.Copy();
			Procedure procedureASaved=procedureA.Copy();
			procedureASaved.Prognosis=0;//another edit in the window
			ProcNoteMerges.ApplyTheirNote(procedureASaved,procedureAOld,procNoteNow,ProcNoteMerges.Normalize(procNoteNow.Note));
			Procedures.Update(procedureASaved,procedureAOld);
			Assert.AreEqual(countBefore,CountNotes(procedure.ProcNum));
			//Save Merged writes one row with the merged text.
			procedureAOld=procedureA.Copy();
			ProcNoteMerges.TakeAsBaseline(procedureAOld,procNoteNow);
			Procedure procedureMerged=procedureAOld.Copy();
			procedureMerged.Note=ProcNoteMerges.GetMergedDefault(procNoteNow.Note,procedureA.Note,noteMine);
			procedureMerged.UserNum=_userNumHyg;
			Procedures.Update(procedureMerged,procedureAOld);
			Assert.AreEqual(countBefore+1,CountNotes(procedure.ProcNum));
			StringAssert.EndsWith(ProcNotes.GetNewestForProc(procedure.ProcNum).Note,"Pt tolerated well.");
		}

		///<summary>A procedure whose note has line breaks, signed by the dentist. The CRUD stores every note with \r\n (POut.StringNote), and
		///Procedures.SetCompleteInAppt and ProcNotes.SetProcCompleteNoteHelper join notes with \r\n too.</summary>
		private static Procedure NewProcWithCrLfNote(long patNum,DateTime date,string note) {
			Procedure procedure=NewProc(patNum,_codeNumA,ProcStat.C,date);
			Procedure procedureOld=Procedures.GetOneProc(procedure.ProcNum,true);
			Procedure procedureFirst=procedureOld.Copy();
			procedureFirst.Note=note;
			procedureFirst.UserNum=_userNumDoc;
			procedureFirst.Signature=Sign(Procedures.GetSignatureKeyData(procedureFirst));
			Procedures.Update(procedureFirst,procedureOld);
			return procedure;
		}

		///<summary>Another user saves a newer note.</summary>
		private static void SaveNewerNote(long procNum,string note) {
			Procedure procedureBOld=Procedures.GetOneProc(procNum,true);
			Procedure procedureB=procedureBOld.Copy();
			procedureB.Note=note;
			procedureB.UserNum=_userNumOther;
			procedureB.Signature="";
			Procedures.Update(procedureB,procedureBOld);
		}

		[TestMethod]
		public void Merge_CrLfNote_OtherEditOnly_PredictionMatchesProceduresUpdate() {
			long patNum=NewPatient();
			string noteStored="TP note.\r\nDefault note.";
			Procedure procedure=NewProcWithCrLfNote(patNum,new DateTime(2026,8,8),noteStored);
			Assert.AreEqual(noteStored,ProcNotes.GetNewestForProc(procedure.ProcNum).Note,"Stored with \\r\\n.");
			//Window A opens; its note box returns the note with \n.
			Procedure procedureAOld=Procedures.GetOneProc(procedure.ProcNum,true);
			ProcNote procNoteAtLoad=ProcNotes.GetNewestForProc(procedure.ProcNum);
			string noteShown=ProcNoteMerges.Normalize(procedureAOld.Note);
			SaveNewerNote(procedure.ProcNum,"TP note.\r\nDefault note.\r\nPt called back.");
			ProcNote procNoteNow=ProcNotes.GetNewestForProc(procedure.ProcNum);
			//A changes only the Dx and saves: the window asks, because Procedures.Update writes a row.
			Procedure procedureA=procedureAOld.Copy();
			procedureA.Dx=3;
			bool isNoteRowWritten=ProcNoteMerges.IsNoteRowWritten(procedureA,procedureAOld,noteShown,isSignatureChanged:false);
			Assert.IsTrue(isNoteRowWritten);
			Assert.IsTrue(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteNow,isNoteRowWritten),"The merge window is shown.");
			//What the prediction stands for: saved without the merge window, the older text becomes the newest note again.
			int countBefore=CountNotes(procedure.ProcNum);
			procedureA.Note=noteShown;
			Procedures.Update(procedureA,procedureAOld);
			Assert.AreEqual(countBefore+1,CountNotes(procedure.ProcNum),"Procedures.Update wrote the row the prediction said it would.");
			//The CRUD stores every note with \r\n (POut.StringNote), so the row holds the older text exactly as it was stored.
			ProcNote procNoteNewest=ProcNotes.GetNewestForProc(procedure.ProcNum);
			Assert.AreEqual(noteStored,procNoteNewest.Note,"The other user's note is no longer the newest: the overwrite the window prevents.");
			Assert.AreNotEqual(procNoteNow.ProcNoteNum,procNoteNewest.ProcNoteNum);
			//And the other way round: the same text exactly writes no row, and the prediction says so.
			Procedure procedureCOld=Procedures.GetOneProc(procedure.ProcNum,true);
			Procedure procedureC=procedureCOld.Copy();
			procedureC.Dx=4;
			Assert.IsFalse(ProcNoteMerges.IsNoteRowWritten(procedureC,procedureCOld,procedureCOld.Note,isSignatureChanged:false));
			countBefore=CountNotes(procedure.ProcNum);
			Procedures.Update(procedureC,procedureCOld);
			Assert.AreEqual(countBefore,CountNotes(procedure.ProcNum));
		}

		[TestMethod]
		public void Merge_CrLfNote_KeepTheirs_WritesNoRow_AndKeepsTheirSignature() {
			long patNum=NewPatient();
			Procedure procedure=NewProcWithCrLfNote(patNum,new DateTime(2026,8,9),"Exam done.\r\nNo findings.");
			Procedure procedureAOld=Procedures.GetOneProc(procedure.ProcNum,true);
			ProcNote procNoteAtLoad=ProcNotes.GetNewestForProc(procedure.ProcNum);
			//The dentist saves and signs a newer note, also with \r\n.
			Procedure procedureBOld=Procedures.GetOneProc(procedure.ProcNum,true);
			Procedure procedureB=procedureBOld.Copy();
			procedureB.Note="Exam done.\r\nNo findings.\r\nRecall in 6 months.";
			procedureB.UserNum=_userNumDoc;
			procedureB.Signature=Sign(Procedures.GetSignatureKeyData(procedureB));
			Procedures.Update(procedureB,procedureBOld);
			ProcNote procNoteNow=ProcNotes.GetNewestForProc(procedure.ProcNum);
			//A changed only the Dx; the window asks, A keeps theirs.
			Procedure procedureA=procedureAOld.Copy();
			procedureA.Dx=3;
			Assert.IsTrue(ProcNoteMerges.IsMergeNeeded(procNoteAtLoad,procNoteNow,
				ProcNoteMerges.IsNoteRowWritten(procedureA,procedureAOld,ProcNoteMerges.Normalize(procedureAOld.Note),false)));
			ProcNoteMerges.ApplyTheirNote(procedureA,procedureAOld,procNoteNow,ProcNoteMerges.Normalize(procNoteNow.Note));
			Assert.IsFalse(ProcNoteMerges.IsNoteRowWritten(procedureA,procedureAOld,ProcNoteMerges.Normalize(procNoteNow.Note),false),"Saving does not ask again.");
			int countBefore=CountNotes(procedure.ProcNum);
			Procedures.Update(procedureA,procedureAOld);
			Assert.AreEqual(countBefore,CountNotes(procedure.ProcNum),"Keep Theirs writes no row.");
			ProcNote procNoteNewest=ProcNotes.GetNewestForProc(procedure.ProcNum);
			Assert.AreEqual(procNoteNow.ProcNoteNum,procNoteNewest.ProcNoteNum,"Their note stays the newest, as stored.");
			Assert.AreEqual(3,Procedures.GetOneProc(procedure.ProcNum,false).Dx,"The other edit is saved.");
			//Their signature still validates against the text the window shows (signature keys ignore line breaks).
			Procedure procedureShown=procedureA.Copy();
			Assert.IsTrue(IsValid(Procedures.GetSignatureKeyData(procedureShown),procNoteNewest.Signature));
		}

		[TestMethod]
		public void OlderAutomaticMerge_PreferenceTurnsTheWindowOff() {
			try {
				Prefs.UpdateBool(PrefName.ProcNoteConcurrencyMerge,true);
				Cache.Refresh(InvalidType.Prefs);
				Assert.IsTrue(ProcNoteMerges.IsAutoMergeOn());
			}
			finally {
				Prefs.UpdateBool(PrefName.ProcNoteConcurrencyMerge,false);
				Cache.Refresh(InvalidType.Prefs);
			}
			Assert.IsFalse(ProcNoteMerges.IsAutoMergeOn());
		}
	}
}
