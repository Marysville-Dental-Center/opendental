// The group note's second signature (Open Dental 26.1): its key, who may edit and sign, the different-signer rule, the alert and log decisions,
// the progress notes' Signed column, and how Procedures.Update decides the second signature of a new note row. The signature tests use the
// business layer's signature box without showing it. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental.UI;
using OpenDentBusiness;

namespace M2NOTESTests {
	[TestClass]
	public class SecondSignatureRulesTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestPrefs.EnsureFilled();
		}

		private static Procedure Group(string note="Hygiene visit.\r\nPerio charted.",long userNum=7,long userNum2=8) {
			return new Procedure { ProcNum=500,ProcDate=new DateTime(2026,9,14),DateEntryC=new DateTime(2026,9,15),UserNum=userNum,UserNum2=userNum2,Note=note };
		}

		private static List<ProcGroupItem> Items(params long[] arrayProcGroupItemNums) {
			List<ProcGroupItem> listProcGroupItems=new List<ProcGroupItem>();
			for(int i=0;i<arrayProcGroupItemNums.Length;i++) {
				listProcGroupItems.Add(new ProcGroupItem { ProcGroupItemNum=arrayProcGroupItemNums[i],GroupNum=500,ProcNum=100+i });
			}
			return listProcGroupItems;
		}

		///<summary>Signs like the signature box wrapper in its default mode (procedures and group notes).</summary>
		private static string Sign(string keyData,float offset=0) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetPointList(new List<PointF> { new PointF(10+offset,10),new PointF(20,15+offset),new PointF(35,12),new PointF(50,30) });
				return signatureBox.GetSigString();
			}
		}

		///<summary>Validates like the signature box wrapper's FillSignature in its default mode.</summary>
		private static bool IsValid(string keyData,string signature) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetSigString(signature);
				return signatureBox.NumberOfTabletPoints()>0;
			}
		}

		[TestMethod]
		public void Key2_SameFieldsWithTheSecondSigner() {
			List<ProcGroupItem> listProcGroupItems=Items(31,32);
			Procedure procedureGroup=Group();
			Assert.AreEqual("9/14/20269/15/20268Hygiene visit.\nPerio charted.3132",GroupNoteSignatures.GetSignatureKey2(procedureGroup,listProcGroupItems));
			Assert.AreEqual("9/14/20269/15/20267Hygiene visit.\nPerio charted.3132",GroupNotes.GetSignatureKey(procedureGroup,listProcGroupItems),
				"The first key is unchanged from 24.3.");
			Assert.AreEqual(7,procedureGroup.UserNum,"The procedure is not changed.");
		}

		[TestMethod]
		public void SecondSignature_InvalidatedByNoteDateOrProcedures_NotByTheFirstBoxUser() {
			List<ProcGroupItem> listProcGroupItems=Items(31,32);
			string signature2=Sign(GroupNoteSignatures.GetSignatureKey2(Group(),listProcGroupItems));
			Assert.IsTrue(IsValid(GroupNoteSignatures.GetSignatureKey2(Group(),listProcGroupItems),signature2));
			Assert.IsTrue(IsValid(GroupNoteSignatures.GetSignatureKey2(Group(userNum:9),listProcGroupItems),signature2),
				"The first box signed by someone else: the second signature still fits.");
			Assert.IsFalse(IsValid(GroupNoteSignatures.GetSignatureKey2(Group("Hygiene visit.\r\nPerio charted. Edited."),listProcGroupItems),signature2),
				"Editing the note invalidates the second signature too.");
			Procedure procedureMoved=Group();
			procedureMoved.ProcDate=new DateTime(2026,9,16);
			Assert.IsFalse(IsValid(GroupNoteSignatures.GetSignatureKey2(procedureMoved,listProcGroupItems),signature2),"Date changed.");
			Assert.IsFalse(IsValid(GroupNoteSignatures.GetSignatureKey2(Group(),Items(31)),signature2),"Attached procedures changed.");
			Assert.IsFalse(IsValid(GroupNoteSignatures.GetSignatureKey2(Group(userNum2:9),listProcGroupItems),signature2),
				"Bound to its own signer: another user number does not fit.");
			Assert.IsFalse(IsValid(GroupNotes.GetSignatureKey(Group(),listProcGroupItems),signature2),"Not the first box's key.");
		}

		[TestMethod]
		public void EditNoteText_EachSignatureProtectsTheNote() {
			//Unsigned: anyone with the note's permission.
			Assert.IsTrue(GroupNoteSignatures.CanEditNoteText(false,5,7,true,0,true));
			//24.3 rule for the first box.
			Assert.IsTrue(GroupNoteSignatures.CanEditNoteText(false,7,7,false,0,true),"The signer.");
			Assert.IsFalse(GroupNoteSignatures.CanEditNoteText(false,5,7,false,0,true),"Someone else.");
			Assert.IsTrue(GroupNoteSignatures.CanEditNoteText(true,5,7,false,0,true),"Group Note Edit Signed.");
			//Applied to the second box too.
			Assert.IsFalse(GroupNoteSignatures.CanEditNoteText(false,5,7,true,8,false),"Second box signed by someone else.");
			Assert.IsTrue(GroupNoteSignatures.CanEditNoteText(false,8,7,true,8,false),"The second signer, first box blank.");
			Assert.IsFalse(GroupNoteSignatures.CanEditNoteText(false,7,7,false,8,false),"Signed by two: the first signer alone may not clear the second.");
			Assert.IsTrue(GroupNoteSignatures.CanEditNoteText(true,7,7,false,8,false),"With Group Note Edit Signed.");
		}

		[TestMethod]
		public void SignBox2_BlankOrOwnOrPermission() {
			Assert.IsTrue(GroupNoteSignatures.CanSignBox2(false,5,0,true),"Blank: a second user can co-sign.");
			Assert.IsTrue(GroupNoteSignatures.CanSignBox2(false,8,8,false),"Their own.");
			Assert.IsFalse(GroupNoteSignatures.CanSignBox2(false,5,8,false),"Someone else's.");
			Assert.IsTrue(GroupNoteSignatures.CanSignBox2(true,5,8,false),"Group Note Edit Signed.");
		}

		[TestMethod]
		public void SecondSigner_MustBeAnotherUser() {
			Assert.IsNotNull(GroupNoteSignatures.GetSignersError(7,false,7,false));
			Assert.IsNull(GroupNoteSignatures.GetSignersError(7,false,8,false));
			Assert.IsNull(GroupNoteSignatures.GetSignersError(7,true,7,false),"The first box is not signed: its user is only the author.");
			Assert.IsNull(GroupNoteSignatures.GetSignersError(7,false,7,true));
		}

		[TestMethod]
		public void Alert_SecondSignerClearedBySomeoneElse() {
			Procedure procedureOld=Group();
			procedureOld.Signature2="sig2";
			List<long> listActing=new List<long> { 7 };
			Assert.AreEqual(8,GroupNoteSignatures.GetSecondSignerToAlert(procedureOld,true,listActing));
			Assert.AreEqual(0,GroupNoteSignatures.GetSecondSignerToAlert(procedureOld,false,listActing),"The box was not touched.");
			Assert.AreEqual(0,GroupNoteSignatures.GetSecondSignerToAlert(procedureOld,true,new List<long> { 8 }),"They cleared it themselves.");
			procedureOld.Signature2="";
			Assert.AreEqual(0,GroupNoteSignatures.GetSecondSignerToAlert(procedureOld,true,listActing),"Nothing was signed.");
			StringAssert.EndsWith(GroupNoteSignatures.GetSignatureClearedAlertText(procedureOld,"~GRP~","DOC1",isSecondSignature:true),"(Signature 2)");
			StringAssert.StartsWith(GroupNoteSignatures.GetSignatureClearedAlertText(procedureOld,"~GRP~","DOC1",isSecondSignature:false),
				"Group Note Changed for PatNum: ");
		}

		[TestMethod]
		public void Log_SecondSignatureSignedOrCleared() {
			Func<long,string> funcName=x => "user"+x;
			Procedure procedureOld=Group();
			procedureOld.Signature2="";
			procedureOld.UserNum2=0;
			Procedure procedureSaved=procedureOld.Copy();
			Assert.AreEqual("",GroupNoteSignatures.GetSecondSignatureLogPart(procedureOld,procedureSaved,funcName),"Unchanged.");
			procedureSaved.Signature2="sig2";
			procedureSaved.UserNum2=8;
			Assert.AreEqual("Second signature by user8",GroupNoteSignatures.GetSecondSignatureLogPart(procedureOld,procedureSaved,funcName));
			Assert.AreEqual("Second signature by user8 cleared",GroupNoteSignatures.GetSecondSignatureLogPart(procedureSaved,procedureOld,funcName));
			Assert.AreEqual("Group note 9/14/2026. Second signature by user8.",
				GroupNoteSignatures.AppendSecondSignatureLog("","Second signature by user8",new DateTime(2026,9,14)));
			Assert.AreEqual("Group note 9/14/2026. Date changed. Second signature by user8.",
				GroupNoteSignatures.AppendSecondSignatureLog("Group note 9/14/2026. Date changed.","Second signature by user8",new DateTime(2026,9,14)));
			Assert.AreEqual("Group note 9/14/2026. Date changed.",
				GroupNoteSignatures.AppendSecondSignatureLog("Group note 9/14/2026. Date changed.","",new DateTime(2026,9,14)));
		}

		[TestMethod]
		public void SignedColumn_ListsBothSigners() {
			Assert.AreEqual("",GroupNoteSignatures.GetSignedText(false,false,"hyg1","doc1"));
			Assert.AreEqual("Signed",GroupNoteSignatures.GetSignedText(true,false,"hyg1",""),"As before when only the first box is signed.");
			Assert.AreEqual("Signed: hyg1, doc1",GroupNoteSignatures.GetSignedText(true,true,"hyg1","doc1"));
			Assert.AreEqual("Signed: doc1",GroupNoteSignatures.GetSignedText(false,true,"hyg1","doc1"),"Only the second box.");
		}

		[TestMethod]
		public void SecondSignatureChanged_ComparesAllThreeFields() {
			Procedure procedureOld=Group();
			procedureOld.Signature2="sig2";
			Procedure procedure=procedureOld.Copy();
			Assert.IsFalse(ProcNotes.IsSecondSignatureChanged(procedure,procedureOld));
			procedure.SigIsTopaz2=true;
			Assert.IsTrue(ProcNotes.IsSecondSignatureChanged(procedure,procedureOld));
			procedure=procedureOld.Copy();
			procedure.UserNum2=9;
			Assert.IsTrue(ProcNotes.IsSecondSignatureChanged(procedure,procedureOld));
			procedure=procedureOld.Copy();
			procedure.Signature2="";
			Assert.IsTrue(ProcNotes.IsSecondSignatureChanged(procedure,procedureOld),"Cleared.");
			procedureOld.Signature2=null;
			procedure.Signature2="";
			Assert.IsFalse(ProcNotes.IsSecondSignatureChanged(procedure,procedureOld),"Null and empty are the same.");
		}

		[TestMethod]
		public void NewNoteRow_SecondSignatureAsChangedByTheCaller_OrClearedWithTheText() {
			Procedure procedureOld=Group();
			procedureOld.Signature2="old sig2";
			//Signed again in the window: as set.
			Procedure procedure=procedureOld.Copy();
			procedure.Note="Edited.";
			procedure.UserNum2=9;
			procedure.Signature2="new sig2";
			procedure.SigIsTopaz2=true;
			ProcNote procNote=new ProcNote();
			ProcNotes.SetSecondSignature(procNote,procedure,procedureOld);
			Assert.AreEqual(9,procNote.UserNum2);
			Assert.AreEqual("new sig2",procNote.Signature2);
			Assert.IsTrue(procNote.SigIsTopaz2);
			//The text changed and the second box was not signed again: cleared, it was made over the old text.
			procedure=procedureOld.Copy();
			procedure.Note="Edited.";
			procNote=new ProcNote { UserNum2=1,Signature2="x" };
			ProcNotes.SetSecondSignature(procNote,procedure,procedureOld);
			Assert.AreEqual(0,procNote.UserNum2);
			Assert.AreEqual("",procNote.Signature2);
			Assert.IsFalse(procNote.SigIsTopaz2);
		}

		[TestMethod]
		public void CopySecondSignature_IntoTheProcedure() {
			Procedure procedure=new Procedure();
			ProcNotes.CopySecondSignatureTo(new ProcNote { UserNum2=8,Signature2="sig2",SigIsTopaz2=true },procedure);
			Assert.AreEqual(8,procedure.UserNum2);
			Assert.AreEqual("sig2",procedure.Signature2);
			Assert.IsTrue(procedure.SigIsTopaz2);
			ProcNotes.CopySecondSignatureTo(null,procedure);
			Assert.AreEqual(8,procedure.UserNum2,"Null changes nothing.");
		}
	}
}
