// Group note signatures (package A-GN, with A-SIG's signature boxes): the key a group note's signature is bound to is the same as in 24.3, so
// signatures already in the office database stay valid, and every change that 25.1 makes possible (date, attached procedures) invalidates a
// signature made before the change. Uses the business layer's signature box without showing it. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDental.UI;

namespace GroupNoteTests {
	[TestClass]
	public class SignatureKeyTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestPrefs.EnsureFilled();
		}

		private static Procedure Group(string note="Patient tolerated well.\r\nNext visit 2 weeks.") {
			return new Procedure { ProcNum=500,ProcDate=new DateTime(2026,9,14),DateEntryC=new DateTime(2026,9,15),UserNum=7,Note=note };
		}

		private static List<ProcGroupItem> Items(params long[] arrayProcGroupItemNums) {
			List<ProcGroupItem> listProcGroupItems=new List<ProcGroupItem>();
			for(int i=0;i<arrayProcGroupItemNums.Length;i++) {
				listProcGroupItems.Add(new ProcGroupItem { ProcGroupItemNum=arrayProcGroupItemNums[i],GroupNum=500,ProcNum=100+i });
			}
			return listProcGroupItems;
		}

		///<summary>The key exactly as FormProcGroup.GetSignatureKey built it in 24.3.63, kept here as the reference.</summary>
		private static string Key243(Procedure procedureGroup,List<ProcGroupItem> listProcGroupItems) {
			string keyData=procedureGroup.ProcDate.ToShortDateString();
			keyData+=procedureGroup.DateEntryC.ToShortDateString();
			keyData+=procedureGroup.UserNum.ToString();
			keyData+=procedureGroup.Note;
			for(int i=0;i<listProcGroupItems.Count;i++) {
				keyData+=listProcGroupItems[i].ProcGroupItemNum.ToString();
			}
			keyData=keyData.Replace("\r\n","\n");
			return keyData;
		}

		///<summary>Signs like the signature box wrapper in its default mode (procedures and group notes).</summary>
		private static string Sign(string keyData) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetPointList(new List<PointF> { new PointF(10,10),new PointF(20,15),new PointF(35,12),new PointF(50,30) });
				return signatureBox.GetSigString();
			}
		}

		///<summary>Validates like the signature box wrapper's FillSignature in its default mode: the signature shows only with the right key.</summary>
		private static bool IsValid(string keyData,string signature) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetSigString(signature);
				return signatureBox.NumberOfTabletPoints()>0;
			}
		}

		[TestMethod]
		public void Key_SameAs243() {
			foreach(List<ProcGroupItem> listProcGroupItems in new[] { Items(),Items(31),Items(31,32,40) }) {
				Assert.AreEqual(Key243(Group(),listProcGroupItems),GroupNotes.GetSignatureKey(Group(),listProcGroupItems));
			}
			Assert.AreEqual("9/14/20269/15/20267Patient tolerated well.\nNext visit 2 weeks.3132",GroupNotes.GetSignatureKey(Group(),Items(31,32)));
		}

		[TestMethod]
		public void Signature_MadeWith243Key_StillValidates() {
			List<ProcGroupItem> listProcGroupItems=Items(31,32);
			string signature=Sign(Key243(Group(),listProcGroupItems));
			Assert.IsTrue(IsValid(GroupNotes.GetSignatureKey(Group(),listProcGroupItems),signature));
		}

		[TestMethod]
		public void Signature_GroupNoteWithNothingAttached_Validates() {
			string signature=Sign(GroupNotes.GetSignatureKey(Group(),new List<ProcGroupItem>()));
			Assert.IsTrue(IsValid(GroupNotes.GetSignatureKey(Group(),null),signature));
		}

		[TestMethod]
		public void Signature_InvalidAfterAddingOrRemovingProcedures() {
			string signature=Sign(GroupNotes.GetSignatureKey(Group(),Items(31,32)));
			Assert.IsFalse(IsValid(GroupNotes.GetSignatureKey(Group(),Items(31,32,33)),signature),"Added");
			Assert.IsFalse(IsValid(GroupNotes.GetSignatureKey(Group(),Items(31)),signature),"Removed");
			Assert.IsFalse(IsValid(GroupNotes.GetSignatureKey(Group(),Items()),signature),"All removed");
		}

		[TestMethod]
		public void Signature_InvalidAfterChangingTheDate() {
			string signature=Sign(GroupNotes.GetSignatureKey(Group(),Items(31)));
			Procedure procedureGroup=Group();
			procedureGroup.ProcDate=procedureGroup.ProcDate.AddDays(1);
			Assert.IsFalse(IsValid(GroupNotes.GetSignatureKey(procedureGroup,Items(31)),signature));
		}

		[TestMethod]
		public void Signature_ProviderIsNotPartOfTheKey() {
			//Changing the provider leaves the signature valid, as in 24.3 (the provider was never signed).
			string signature=Sign(GroupNotes.GetSignatureKey(Group(),Items(31)));
			Procedure procedureGroup=Group();
			procedureGroup.ProvNum=99;
			Assert.IsTrue(IsValid(GroupNotes.GetSignatureKey(procedureGroup,Items(31)),signature));
		}

		[TestMethod]
		public void Signature_NewlinesInTheNoteDontMatter() {
			string signature=Sign(GroupNotes.GetSignatureKey(Group("a\r\nb"),Items(31)));
			Assert.IsTrue(IsValid(GroupNotes.GetSignatureKey(Group("a\nb"),Items(31)),signature));
		}
	}
}
