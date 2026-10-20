// Row-integrity hash formulas for insverify, insverifyhist and inssub, and the rule for keeping a hash on save. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using SecurityHash=OpenDentBusiness.Misc.SecurityHash;

namespace RowHashTests {
	[TestClass]
	public class HashFieldsTests {
		private static InsVerify NewInsVerify(long userNum,long defNum,DateTime dateLastAssigned) {
			InsVerify insVerify=new InsVerify();
			insVerify.UserNum=userNum;
			insVerify.DefNum=defNum;
			insVerify.DateLastAssigned=dateLastAssigned;
			return insVerify;
		}

		private static InsSub NewInsSub() {
			InsSub insSub=new InsSub();
			insSub.PlanNum=11;
			insSub.Subscriber=22;
			insSub.SubscriberID="XYZ123";
			insSub.DateTerm=new DateTime(2026,12,31);
			return insSub;
		}

		[TestMethod]
		public void InsVerify_HashIsCurrentFormatAndDependsOnEachHashedField() {
			InsVerify insVerify=NewInsVerify(3,4,new DateTime(2026,10,1));
			string hash=InsVerifies.HashFields(insVerify);
			Assert.IsTrue(TamperHash.IsCurrentFormat(hash));
			Assert.AreNotEqual(hash,InsVerifies.HashFields(NewInsVerify(5,4,new DateTime(2026,10,1))),"UserNum");
			Assert.AreNotEqual(hash,InsVerifies.HashFields(NewInsVerify(3,5,new DateTime(2026,10,1))),"DefNum");
			Assert.AreNotEqual(hash,InsVerifies.HashFields(NewInsVerify(3,4,new DateTime(2026,10,2))),"DateLastAssigned");
		}

		[TestMethod]
		public void InsVerify_HashIgnoresUnhashedFieldsAndTimeOfDay() {
			InsVerify insVerify=NewInsVerify(3,4,new DateTime(2026,10,1));
			string hash=InsVerifies.HashFields(insVerify);
			insVerify.Note="changed";
			insVerify.DateLastVerified=new DateTime(2026,9,1);
			insVerify.FKey=77;
			insVerify.DateLastAssigned=new DateTime(2026,10,1,15,42,7);
			Assert.AreEqual(hash,InsVerifies.HashFields(insVerify),"The 24.3 CRUD stores the date only; the 25.3 column keeps the time.");
		}

		[TestMethod]
		public void InsVerify_FieldsAreSeparated() {
			Assert.AreNotEqual(InsVerifies.HashFields(NewInsVerify(1,23,DateTime.MinValue)),InsVerifies.HashFields(NewInsVerify(12,3,DateTime.MinValue)));
		}

		[TestMethod]
		public void InsVerifyHist_CopyHasTheSourceHashAndItStillVerifies() {
			InsVerify insVerify=NewInsVerify(3,4,new DateTime(2026,10,1,9,30,0));
			insVerify.SecurityHash=InsVerifies.HashFields(insVerify);
			if(Security.CurUser==null) {
				Security.CurUser=new Userod();//The history constructor records the current user as the verifier.
			}
			InsVerifyHist insVerifyHist=new InsVerifyHist(insVerify);
			Assert.AreEqual(insVerify.SecurityHash,insVerifyHist.SecurityHash);
			Assert.AreEqual(insVerify.SecurityHash,InsVerifyHists.HashFields(insVerifyHist));
		}

		[TestMethod]
		public void InsSub_HashDependsOnHashedFieldsOnly() {
			string hash=InsSubs.HashFields(NewInsSub());
			Assert.IsTrue(TamperHash.IsCurrentFormat(hash));
			InsSub insSub=NewInsSub();
			insSub.PlanNum=12;
			Assert.AreNotEqual(hash,InsSubs.HashFields(insSub),"PlanNum");
			insSub=NewInsSub();
			insSub.Subscriber=23;
			Assert.AreNotEqual(hash,InsSubs.HashFields(insSub),"Subscriber");
			insSub=NewInsSub();
			insSub.SubscriberID="XYZ124";
			Assert.AreNotEqual(hash,InsSubs.HashFields(insSub),"SubscriberID");
			insSub=NewInsSub();
			insSub.DateTerm=new DateTime(2027,1,1);
			Assert.AreNotEqual(hash,InsSubs.HashFields(insSub),"DateTerm");
			insSub=NewInsSub();
			insSub.BenefitNotes="notes";
			insSub.SubscNote="note";
			insSub.DateEffective=new DateTime(2020,1,1);
			insSub.AssignBen=!insSub.AssignBen;
			insSub.ReleaseInfo=!insSub.ReleaseInfo;
			insSub.DateTerm=new DateTime(2026,12,31,23,59,0);
			Assert.AreEqual(hash,InsSubs.HashFields(insSub),"Unhashed fields and the time of DateTerm");
		}

		[TestMethod]
		public void DoRehashOnSave_KeepsOnlyAHashThatShowsAnOutsideChange() {
			string hashComputed=TamperHash.Compute("stored values");
			string hashOther=TamperHash.Compute("other values");
			Assert.IsTrue(SecurityHash.DoRehashOnSave(hashComputed,hashComputed),"valid");
			Assert.IsTrue(SecurityHash.DoRehashOnSave("",hashComputed),"never hashed");
			Assert.IsTrue(SecurityHash.DoRehashOnSave(null,hashComputed),"never hashed (null)");
			Assert.IsTrue(SecurityHash.DoRehashOnSave("od-legacy-hash",hashComputed),"written by Open Dental");
			Assert.IsFalse(SecurityHash.DoRehashOnSave(hashOther,hashComputed),"changed outside the program");
		}

		[TestMethod]
		public void HashPatNum_MatchesThePatientHash() {
			Assert.AreEqual(SecurityHash.HashPatNum(4242),TamperHash.Compute("4242",isPatientTable:true));
			Assert.AreNotEqual(SecurityHash.HashPatNum(4242),TamperHash.Compute("4242"),"patient key differs from the row key");
		}
	}
}
