// Database Maintenance check InsVerifyRedundantEntries (Open Dental 26.2 item 30) against the scratch database. Synthetic data only: the FKeys are
// made-up plan numbers in a range no other row uses.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using Crud=OpenPmsDentalBusiness.Crud;

namespace P262ToolsTests {
	[TestClass]
	public class InsVerifyRedundantDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ToolsDb.Begin();
		}

		private static long NewFKey() {
			return 9000000+ToolsDb.Next();
		}

		private static InsVerify InsertInsVerify(VerifyTypes verifyType,long fKey,DateTime dateLastVerified,string note="",long userNum=0,long defNum=0) {
			InsVerify insVerify=new InsVerify();
			insVerify.VerifyType=verifyType;
			insVerify.FKey=fKey;
			insVerify.DateLastVerified=dateLastVerified;
			insVerify.Note=note;
			insVerify.UserNum=userNum;
			insVerify.DefNum=defNum;
			insVerify.SecurityHash=InsVerifies.HashFields(insVerify);
			Crud.InsVerifyCrud.Insert(insVerify);//DateTimeEntry is set by the database
			return Crud.InsVerifyCrud.SelectOne(insVerify.InsVerifyNum);
		}

		private static List<InsVerify> GetInsVerifies(VerifyTypes verifyType,long fKey) {
			return Crud.InsVerifyCrud.SelectMany("SELECT * FROM insverify WHERE VerifyType="+POut.Int((int)verifyType)+" AND FKey="+POut.Long(fKey));
		}

		private static List<InsVerifyHist> GetHists(VerifyTypes verifyType,long fKey) {
			return Crud.InsVerifyHistCrud.SelectMany("SELECT * FROM insverifyhist WHERE VerifyType="+POut.Int((int)verifyType)+" AND FKey="+POut.Long(fKey)
				+" ORDER BY InsVerifyHistNum");
		}

		[TestMethod]
		public void Fix_KeepsTheLatestVerificationAndMovesTheOtherToHistory() {
			long fKey=NewFKey();
			InsVerify insVerifyOld=InsertInsVerify(VerifyTypes.InsuranceBenefit,fKey,new DateTime(2026,1,5),note:"older verification",userNum:3,defNum:7);
			InsVerify insVerifyNew=InsertInsVerify(VerifyTypes.InsuranceBenefit,fKey,new DateTime(2026,3,1));
			Assert.IsTrue(DatabaseMaintenances.GetInsVerifyRedundantCount()>=1);
			StringAssert.Contains(DatabaseMaintenances.InsVerifyRedundantEntries(false,DbmMode.Check),"Redundant insurance verification entries");
			Assert.AreEqual(2,GetInsVerifies(VerifyTypes.InsuranceBenefit,fKey).Count,"Check changes nothing.");
			string log=DatabaseMaintenances.InsVerifyRedundantEntries(false,DbmMode.Fix);
			StringAssert.Contains(log,"moved to the verification history");
			List<InsVerify> listLeft=GetInsVerifies(VerifyTypes.InsuranceBenefit,fKey);
			Assert.AreEqual(1,listLeft.Count);
			Assert.AreEqual(insVerifyNew.InsVerifyNum,listLeft[0].InsVerifyNum);
			List<InsVerifyHist> listHists=GetHists(VerifyTypes.InsuranceBenefit,fKey);
			Assert.AreEqual(1,listHists.Count);
			InsVerifyHist insVerifyHist=listHists[0];
			Assert.AreEqual(insVerifyNew.InsVerifyNum,insVerifyHist.InsVerifyNum,"The history row points to the row that is kept.");
			Assert.AreEqual(new DateTime(2026,1,5),insVerifyHist.DateLastVerified);
			Assert.AreEqual("older verification",insVerifyHist.Note);
			Assert.AreEqual(3,insVerifyHist.UserNum);
			Assert.AreEqual(7,insVerifyHist.DefNum);
			Assert.AreEqual(insVerifyOld.DateTimeEntry,insVerifyHist.DateTimeEntry);
			Assert.AreEqual(insVerifyOld.SecurityHash,insVerifyHist.SecurityHash,"The hash is copied unchanged and stays valid.");
			Assert.AreEqual(InsVerifyHists.HashFields(insVerifyHist),insVerifyHist.SecurityHash);
			Assert.AreEqual(Security.CurUser.UserNum,insVerifyHist.VerifyUserNum);
			List<DbmLog> listDbmLogs=ToolsDb.GetDbmLogs(nameof(DatabaseMaintenances.InsVerifyRedundantEntries),fKey);
			Assert.AreEqual(1,listDbmLogs.Count);
			Assert.AreEqual(DbmLogFKeyType.InsPlan,listDbmLogs[0].FKeyType);
			Assert.AreEqual(DbmLogActionType.Delete,listDbmLogs[0].ActionType);
			StringAssert.Contains(listDbmLogs[0].LogText,"Moved redundant insverify row "+insVerifyOld.InsVerifyNum);
			StringAssert.Contains(listDbmLogs[0].LogText,"Kept insverify row "+insVerifyNew.InsVerifyNum);
		}

		[TestMethod]
		public void Fix_OnADateTie_KeepsTheNewestRow_TheOneTheProgramReads() {
			long fKey=NewFKey();
			DateTime date=new DateTime(2026,2,10);
			InsertInsVerify(VerifyTypes.PatientEnrollment,fKey,date);
			InsVerify insVerifyNewest=InsertInsVerify(VerifyTypes.PatientEnrollment,fKey,date);
			Assert.AreEqual(insVerifyNewest.InsVerifyNum,InsVerifies.GetOneByFKey(fKey,VerifyTypes.PatientEnrollment).InsVerifyNum);
			DatabaseMaintenances.InsVerifyRedundantEntries(false,DbmMode.Fix);
			List<InsVerify> listLeft=GetInsVerifies(VerifyTypes.PatientEnrollment,fKey);
			Assert.AreEqual(1,listLeft.Count);
			Assert.AreEqual(insVerifyNewest.InsVerifyNum,listLeft[0].InsVerifyNum);
			Assert.AreEqual(DbmLogFKeyType.PatPlan,ToolsDb.GetDbmLogs(nameof(DatabaseMaintenances.InsVerifyRedundantEntries),fKey).Single().FKeyType);
		}

		[TestMethod]
		public void Fix_ThreeRows_MovesTwo_AndASecondRunChangesNothing() {
			long fKey=NewFKey();
			InsertInsVerify(VerifyTypes.PatientEnrollment,fKey,new DateTime(2025,12,1));
			InsVerify insVerifyKept=InsertInsVerify(VerifyTypes.PatientEnrollment,fKey,new DateTime(2026,4,1));
			InsertInsVerify(VerifyTypes.PatientEnrollment,fKey,DateTime.MinValue);//never verified
			DatabaseMaintenances.InsVerifyRedundantEntries(false,DbmMode.Fix);
			Assert.AreEqual(insVerifyKept.InsVerifyNum,GetInsVerifies(VerifyTypes.PatientEnrollment,fKey).Single().InsVerifyNum);
			Assert.AreEqual(2,GetHists(VerifyTypes.PatientEnrollment,fKey).Count);
			Assert.AreEqual(0,DatabaseMaintenances.GetInsVerifyRedundantCount());
			Assert.AreEqual(0,DatabaseMaintenances.MoveInsVerifyRedundantEntries());
			Assert.AreEqual("",DatabaseMaintenances.InsVerifyRedundantEntries(false,DbmMode.Check),"Nothing to report when not verbose.");
			StringAssert.Contains(DatabaseMaintenances.InsVerifyRedundantEntries(true,DbmMode.Check),": 0");
		}

		[TestMethod]
		public void Fix_LeavesSingleRowsOtherPlansAndVerifyTypeNoneAlone() {
			long fKey=NewFKey();
			InsVerify insVerifyBenefit=InsertInsVerify(VerifyTypes.InsuranceBenefit,fKey,new DateTime(2026,1,1));
			InsVerify insVerifyEnrollment=InsertInsVerify(VerifyTypes.PatientEnrollment,fKey,new DateTime(2026,1,1));//same FKey, other type
			InsertInsVerify(VerifyTypes.None,0,DateTime.MinValue);
			InsertInsVerify(VerifyTypes.None,0,DateTime.MinValue);
			long countNoneBefore=Crud.InsVerifyCrud.SelectMany("SELECT * FROM insverify WHERE VerifyType=0").Count;
			DatabaseMaintenances.InsVerifyRedundantEntries(false,DbmMode.Fix);
			Assert.AreEqual(insVerifyBenefit.InsVerifyNum,GetInsVerifies(VerifyTypes.InsuranceBenefit,fKey).Single().InsVerifyNum);
			Assert.AreEqual(insVerifyEnrollment.InsVerifyNum,GetInsVerifies(VerifyTypes.PatientEnrollment,fKey).Single().InsVerifyNum);
			Assert.AreEqual(countNoneBefore,Crud.InsVerifyCrud.SelectMany("SELECT * FROM insverify WHERE VerifyType=0").Count);
			Assert.AreEqual(0,GetHists(VerifyTypes.InsuranceBenefit,fKey).Count);
		}

		[TestMethod]
		public void KeptFirst_OrdersByLastVerifiedThenNewest() {
			List<InsVerify> listInsVerifies=new List<InsVerify> {
				new InsVerify { InsVerifyNum=10,DateLastVerified=new DateTime(2026,1,1) },
				new InsVerify { InsVerifyNum=11,DateLastVerified=new DateTime(2026,2,1) },
				new InsVerify { InsVerifyNum=12,DateLastVerified=new DateTime(2026,2,1) },
				new InsVerify { InsVerifyNum=13,DateLastVerified=DateTime.MinValue },
			};
			CollectionAssert.AreEqual(new long[] { 12,11,10,13 },
				DatabaseMaintenances.GetInsVerifiesKeptFirst(listInsVerifies).Select(x => x.InsVerifyNum).ToArray());
		}
	}
}
