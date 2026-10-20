// Row-integrity hashes on a scratch database: a fresh 24.3.63 database (as tools/New-DevDatabase.ps1 builds it) upgraded to the Open Dental 25.3.48
// schema by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using SecurityHash=OpenDentBusiness.Misc.SecurityHash;

namespace RowHashTests {
	[TestClass]
	public class RowHashDatabaseTests {
		private static readonly string DB_NAME=TestDb.ScratchName("rowhash");
		private static string _skipReason;
		private static Userod _userodAdmin;
		private static Userod _userodPlain;
		///<summary>Unique numbers for the synthetic rows of each test, so tests don't see each other's rows.</summary>
		private static long _numNext=900000;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(DB_NAME);
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs);
			_userodAdmin=CreateUser("rowhash_admin",isSecurityAdmin:true);
			_userodPlain=CreateUser("rowhash_plain",isSecurityAdmin:false);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
			Security.CurUser=_userodAdmin;
		}

		#region Helpers
		///<summary>A user in its own group; the group has Security Admin when isSecurityAdmin. Made with SQL, so the test does not depend on the
		///security forms.</summary>
		private static Userod CreateUser(string userName,bool isSecurityAdmin) {
			long userGroupNum=DataCore.NonQ("INSERT INTO usergroup (Description) VALUES ('"+POut.String(userName)+"')",true);
			if(isSecurityAdmin) {
				DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES ('0001-01-01',0,"
					+POut.Long(userGroupNum)+","+POut.Int((int)EnumPermType.SecurityAdmin)+",0)");
			}
			long userNum=DataCore.NonQ("INSERT INTO userod (UserName,Password,UserGroupNum) VALUES ('"+POut.String(userName)+"','',"
				+POut.Long(userGroupNum)+")",true);
			DataCore.NonQ("INSERT INTO usergroupattach (UserNum,UserGroupNum) VALUES ("+POut.Long(userNum)+","+POut.Long(userGroupNum)+")");
			Cache.Refresh(InvalidType.Security);
			return Userods.GetUser(userNum);
		}

		private static long NextNum() {
			return ++_numNext;
		}

		///<summary>An edit made directly in the database, as a query tool would make it.</summary>
		private static void EditOutsideProgram(string command) {
			DataCore.NonQ(command);
		}

		private static InsSub InsertInsSub(long planNum,string subscriberID) {
			InsSub insSub=new InsSub();
			insSub.PlanNum=planNum;
			insSub.Subscriber=NextNum();
			insSub.SubscriberID=subscriberID;
			insSub.DateEffective=new DateTime(2025,1,1);
			InsSubs.Insert(insSub);
			return InsSubs.GetOne(insSub.InsSubNum);
		}
		#endregion Helpers

		#region insverify
		[TestMethod]
		public void InsVerify_InsertWritesAValidHash() {
			long insVerifyNum=InsVerifies.InsertForPlanNum(NextNum());
			InsVerify insVerify=InsVerifies.GetOne(insVerifyNum);
			Assert.IsTrue(TamperHash.IsCurrentFormat(insVerify.SecurityHash));
			Assert.AreEqual(InsVerifies.HashFields(insVerify),insVerify.SecurityHash);
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerify));
			insVerifyNum=InsVerifies.InsertForPatPlanNum(NextNum());
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(InsVerifies.GetOne(insVerifyNum)));
		}

		[TestMethod]
		public void InsVerify_UpdateOfAHashedFieldRehashes() {
			InsVerify insVerify=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(NextNum()));
			string hashBlank=insVerify.SecurityHash;
			insVerify.UserNum=_userodAdmin.UserNum;
			insVerify.DefNum=NextNum();
			insVerify.DateLastAssigned=DateTime.Now;
			InsVerifies.Update(insVerify);
			InsVerify insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.AreNotEqual(hashBlank,insVerifyDb.SecurityHash);
			Assert.AreEqual(InsVerifies.HashFields(insVerifyDb),insVerifyDb.SecurityHash);
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerifyDb));
		}

		[TestMethod]
		public void InsVerify_EditOutsideTheProgramIsDetectedAndNotHiddenByAnUnrelatedSave() {
			InsVerify insVerify=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(NextNum()));
			insVerify.UserNum=_userodAdmin.UserNum;
			insVerify.DateLastAssigned=DateTime.Today;
			InsVerifies.Update(insVerify);
			EditOutsideProgram("UPDATE insverify SET UserNum=UserNum+1000 WHERE InsVerifyNum="+POut.Long(insVerify.InsVerifyNum));
			InsVerify insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.IsFalse(InsVerifies.IsInsVerifyHashValid(insVerifyDb),"edit outside the program");
			//A save that changes no hashed field keeps the old hash, so the warning stays.
			insVerifyDb.Note="Synthetic note";
			InsVerifies.Update(insVerifyDb);
			insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.AreEqual("Synthetic note",insVerifyDb.Note);
			Assert.IsFalse(InsVerifies.IsInsVerifyHashValid(insVerifyDb),"unrelated save");
			//Nor does a save of a hashed field: a user's edit never hides the outside change.
			long defNumStatus=NextNum();
			insVerifyDb.DefNum=defNumStatus;
			InsVerifies.Update(insVerifyDb);
			insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.AreEqual(defNumStatus,insVerifyDb.DefNum);
			Assert.IsFalse(InsVerifies.IsInsVerifyHashValid(insVerifyDb),"program sets a status");
			//The administrator's rehash accepts the row as it stands.
			SecurityHash.RehashNow();
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(InsVerifies.GetOne(insVerify.InsVerifyNum)),"after the rehash tool");
		}

		[TestMethod]
		public void InsVerify_ClearAssignmentsRehashesButKeepsATamperedRowFlagged() {
			InsVerify insVerifyGood=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(NextNum()));
			InsVerify insVerifyTampered=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(NextNum()));
			foreach(InsVerify insVerify in new[] { insVerifyGood,insVerifyTampered }) {
				insVerify.UserNum=_userodAdmin.UserNum;
				insVerify.DefNum=NextNum();
				insVerify.DateLastAssigned=DateTime.Today;
				InsVerifies.Update(insVerify);
			}
			EditOutsideProgram("UPDATE insverify SET DefNum=DefNum+1 WHERE InsVerifyNum="+POut.Long(insVerifyTampered.InsVerifyNum));
			InsVerifies.ClearAssignments(new List<long> { insVerifyGood.InsVerifyNum,insVerifyTampered.InsVerifyNum });
			InsVerify insVerifyGoodDb=InsVerifies.GetOne(insVerifyGood.InsVerifyNum);
			Assert.AreEqual(0,insVerifyGoodDb.UserNum);
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerifyGoodDb));
			InsVerify insVerifyTamperedDb=InsVerifies.GetOne(insVerifyTampered.InsVerifyNum);
			Assert.AreEqual(0,insVerifyTamperedDb.DefNum);
			Assert.IsFalse(InsVerifies.IsInsVerifyHashValid(insVerifyTamperedDb));
		}

		[TestMethod]
		public void InsVerify_RowsOutsideTheHashingWindowAreNotChecked() {
			InsVerify insVerify=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(NextNum()));
			EditOutsideProgram("UPDATE insverify SET DefNum=12345,SecDateTEdit="+POut.DateT(SecurityHash.GetHashingDate().AddDays(-3))
				+" WHERE InsVerifyNum="+POut.Long(insVerify.InsVerifyNum));
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(InsVerifies.GetOne(insVerify.InsVerifyNum)));
		}

		[TestMethod]
		public void InsVerify_UpsertBlanksTheRowAndRehashes() {
			long planNum=NextNum();
			InsVerify insVerify=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(planNum));
			insVerify.UserNum=_userodAdmin.UserNum;
			insVerify.DefNum=NextNum();
			insVerify.DateLastAssigned=DateTime.Today;
			InsVerifies.Update(insVerify);
			InsVerifies.Upsert(planNum,VerifyTypes.InsuranceBenefit);
			InsVerify insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.AreEqual(0,insVerifyDb.UserNum);
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerifyDb));
		}

		[TestMethod]
		public void InsVerifyHist_CopyKeepsTheValidHashAndTheSourceIsRehashed() {
			long planNum=NextNum();
			InsVerify insVerify=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(planNum));
			insVerify.UserNum=_userodAdmin.UserNum;
			insVerify.DefNum=NextNum();
			insVerify.DateLastAssigned=DateTime.Today;
			insVerify.DateLastVerified=DateTime.Today;
			InsVerifies.Update(insVerify);
			string hashAssigned=InsVerifies.GetOne(insVerify.InsVerifyNum).SecurityHash;
			InsVerifyHists.InsertFromInsVerify(InsVerifies.GetOne(insVerify.InsVerifyNum));
			InsVerifyHist insVerifyHist=InsVerifyHists.GetForFKeyByType(planNum,VerifyTypes.InsuranceBenefit).Single();
			Assert.AreEqual(hashAssigned,insVerifyHist.SecurityHash,"copied unchanged");
			Assert.IsTrue(InsVerifyHists.IsInsVerifyHistHashValid(insVerifyHist));
			InsVerify insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.AreEqual(0,insVerifyDb.UserNum,"source row is blanked");
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerifyDb),"source row rehashed");
			EditOutsideProgram("UPDATE insverifyhist SET UserNum=UserNum+1 WHERE InsVerifyHistNum="+POut.Long(insVerifyHist.InsVerifyHistNum));
			Assert.IsFalse(InsVerifyHists.IsInsVerifyHistHashValid(InsVerifyHists.GetOne(insVerifyHist.InsVerifyHistNum)));
		}
		#endregion insverify

		#region inssub
		[TestMethod]
		public void InsSub_InsertAndUpdateWriteValidHashes() {
			InsSub insSub=InsertInsSub(NextNum(),"SYN"+NextNum());
			Assert.IsTrue(TamperHash.IsCurrentFormat(insSub.SecurityHash));
			Assert.IsTrue(InsSubs.IsInsSubHashValid(insSub));
			insSub.SubscriberID="SYN"+NextNum();
			insSub.DateTerm=new DateTime(2027,6,30);
			InsSubs.Update(insSub);
			InsSub insSubDb=InsSubs.GetOne(insSub.InsSubNum);
			Assert.AreEqual(InsSubs.HashFields(insSubDb),insSubDb.SecurityHash);
			Assert.IsTrue(InsSubs.IsInsSubHashValid(insSubDb));
		}

		[TestMethod]
		public void InsSub_EditOutsideTheProgramIsDetectedAndKeptOnLaterSaves() {
			InsSub insSub=InsertInsSub(NextNum(),"SYN"+NextNum());
			EditOutsideProgram("UPDATE inssub SET DateTerm='2020-01-31' WHERE InsSubNum="+POut.Long(insSub.InsSubNum));
			InsSub insSubDb=InsSubs.GetOne(insSub.InsSubNum);
			Assert.IsFalse(InsSubs.IsInsSubHashValid(insSubDb),"edit outside the program");
			insSubDb.BenefitNotes="Synthetic benefit notes";
			InsSubs.Update(insSubDb);
			Assert.IsFalse(InsSubs.IsInsSubHashValid(InsSubs.GetOne(insSub.InsSubNum)),"unrelated save");
			insSubDb.SubscriberID="SYN"+NextNum();
			InsSubs.Update(insSubDb);
			Assert.IsFalse(InsSubs.IsInsSubHashValid(InsSubs.GetOne(insSub.InsSubNum)),"save of a hashed field");
		}

		[TestMethod]
		public void InsSub_ChangePlanNumRehashesMovedRowsButKeepsATamperedOne() {
			long planNumFrom=NextNum();
			long planNumTo=NextNum();
			InsSub insSubGood=InsertInsSub(planNumFrom,"SYN"+NextNum());
			InsSub insSubTampered=InsertInsSub(planNumFrom,"SYN"+NextNum());
			EditOutsideProgram("UPDATE inssub SET SubscriberID='TAMPERED' WHERE InsSubNum="+POut.Long(insSubTampered.InsSubNum));
			InsSubs.ChangePlanNum(planNumFrom,planNumTo);
			InsSub insSubGoodDb=InsSubs.GetOne(insSubGood.InsSubNum);
			Assert.AreEqual(planNumTo,insSubGoodDb.PlanNum);
			Assert.IsTrue(InsSubs.IsInsSubHashValid(insSubGoodDb),"moved row rehashed");
			InsSub insSubTamperedDb=InsSubs.GetOne(insSubTampered.InsSubNum);
			Assert.AreEqual(planNumTo,insSubTamperedDb.PlanNum);
			Assert.IsFalse(InsSubs.IsInsSubHashValid(insSubTamperedDb),"tampered row keeps its warning");
		}

		[TestMethod]
		public void InsSub_RowsWithOpenDentalHashesAreAcceptedAndConvertedOnSave() {
			InsSub insSub=InsertInsSub(NextNum(),"SYN"+NextNum());
			EditOutsideProgram("UPDATE inssub SET SecurityHash='Zm9yZWlnbi1oYXNo' WHERE InsSubNum="+POut.Long(insSub.InsSubNum));
			InsSub insSubDb=InsSubs.GetOne(insSub.InsSubNum);
			Assert.IsTrue(InsSubs.IsInsSubHashValid(insSubDb),"legacy hash is not reported");
			insSubDb.SubscNote="Synthetic";
			InsSubs.Update(insSubDb);
			insSubDb=InsSubs.GetOne(insSub.InsSubNum);
			Assert.IsTrue(TamperHash.IsCurrentFormat(insSubDb.SecurityHash),"converted on save");
			Assert.IsTrue(InsSubs.IsInsSubHashValid(insSubDb));
		}
		#endregion inssub

		#region Hashing pass
		[TestMethod]
		public void RehashNow_HashesRecentInsVerifyInsVerifyHistAndInsSubRows() {
			InsVerify insVerify=InsVerifies.GetOne(InsVerifies.InsertForPlanNum(NextNum()));
			long insVerifyHistNum=InsVerifyHists.Insert(new InsVerifyHist(insVerify));
			InsSub insSub=InsertInsSub(NextNum(),"SYN"+NextNum());
			EditOutsideProgram("UPDATE insverify SET SecurityHash='' WHERE InsVerifyNum="+POut.Long(insVerify.InsVerifyNum));
			EditOutsideProgram("UPDATE insverifyhist SET SecurityHash='' WHERE InsVerifyHistNum="+POut.Long(insVerifyHistNum));
			EditOutsideProgram("UPDATE inssub SET SecurityHash='' WHERE InsSubNum="+POut.Long(insSub.InsSubNum));
			Assert.IsFalse(InsVerifies.IsInsVerifyHashValid(InsVerifies.GetOne(insVerify.InsVerifyNum)),"blank hash on a recent row");
			SecurityHash.RehashNow();
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(InsVerifies.GetOne(insVerify.InsVerifyNum)));
			Assert.IsTrue(InsVerifyHists.IsInsVerifyHistHashValid(InsVerifyHists.GetOne(insVerifyHistNum)));
			Assert.IsTrue(InsSubs.IsInsSubHashValid(InsSubs.GetOne(insSub.InsSubNum)));
			Assert.IsTrue(TamperHash.IsCurrentFormat(InsSubs.GetOne(insSub.InsSubNum).SecurityHash));
		}
		#endregion Hashing pass

		#region patient
		[TestMethod]
		public void Patient_InsertHashesAndAnEditOutsideTheProgramIsDetected() {
			Patient patient=new Patient();
			patient.LName="Synthetic";
			patient.FName="Rowhash";
			Patients.Insert(patient,false);
			Patient patientDb=Patients.GetPat(patient.PatNum);
			Assert.IsTrue(Patients.IsPatientHashValid(patientDb));
			EditOutsideProgram("UPDATE patient SET SecurityHash='"+POut.String(SecurityHash.HashPatNum(patient.PatNum+1))+"' WHERE PatNum="
				+POut.Long(patient.PatNum));
			Assert.IsFalse(Patients.IsPatientHashValid(Patients.GetPat(patient.PatNum)));
		}

		[TestMethod]
		public void Dbm_SecurityHashRehashPatientTable_CoversTheWholeTable() {
			//More rows than one batch: blank, Open Dental (legacy), changed outside the program, and correct hashes.
			int countRows=SecurityHash.PATIENT_BATCH_SIZE*2+137;
			List<string> listValues=new List<string>();
			for(int i=0;i<countRows;i++) {
				string hash;
				switch(i%4) {
					case 0: hash=""; break;
					case 1: hash="T3BlbkRlbnRhbExlZ2FjeQ"+i; break;
					case 2: hash=SecurityHash.HashPatNum(-i); break;
					default: hash="placeholder"; break;
				}
				listValues.Add("('Synthetic','Bulk"+i+"','"+POut.String(hash)+"','2024-05-05 05:05:05')");
			}
			DataCore.NonQ("INSERT INTO patient (LName,FName,SecurityHash,DateTStamp) VALUES "+string.Join(",",listValues));
			//Give the 'placeholder' rows a correct hash.
			DataTable table=DataCore.GetTable("SELECT PatNum FROM patient WHERE SecurityHash='placeholder'");
			foreach(DataRow row in table.Rows) {
				long patNum=PIn.Long(row["PatNum"].ToString());
				DataCore.NonQ("UPDATE patient SET SecurityHash='"+POut.String(SecurityHash.HashPatNum(patNum))+"',DateTStamp=DateTStamp WHERE PatNum="
					+POut.Long(patNum));
			}
			long countPatients=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM patient"));
			long countExpected=CountNotMatchingIndependently();
			Assert.IsTrue(countExpected>=countRows*3/4,"blank, legacy and changed rows");
			string logCheck=DatabaseMaintenances.SecurityHashRehashPatientTable(false,DbmMode.Check);
			StringAssert.Contains(logCheck,": "+countExpected);
			long countLegacy;
			long countBlank;
			Assert.AreEqual(countExpected,SecurityHash.CountPatientHashesNotMatching(out countLegacy,out countBlank));
			Assert.IsTrue(countLegacy>=countRows/4 && countBlank>=countRows/4);
			//Without Security Admin, Fix changes nothing.
			Security.CurUser=_userodPlain;
			string logRefused=DatabaseMaintenances.SecurityHashRehashPatientTable(false,DbmMode.Fix);
			StringAssert.Contains(logRefused,"Security Admin");
			Assert.AreEqual(countExpected,CountNotMatchingIndependently());
			//With Security Admin, every row is rehashed, in batches, with one security log entry; DateTStamp is kept.
			Security.CurUser=_userodAdmin;
			string commandLogCount="SELECT COUNT(*) FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.SecurityAdmin)
				+" AND LogText LIKE '%SecurityHashRehashPatientTable%'";
			long countLogsBefore=PIn.Long(DataCore.GetScalar(commandLogCount));
			string logFix=DatabaseMaintenances.SecurityHashRehashPatientTable(false,DbmMode.Fix);
			StringAssert.Contains(logFix,": "+countExpected);
			Assert.AreEqual(0,CountNotMatchingIndependently());
			Assert.AreEqual(countPatients,PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM patient")));
			Assert.AreEqual(countLogsBefore+1,PIn.Long(DataCore.GetScalar(commandLogCount)));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM patient WHERE FName LIKE 'Bulk%' AND DateTStamp!='2024-05-05 05:05:05'"),
				"DateTStamp kept");
			Assert.AreEqual("",DatabaseMaintenances.SecurityHashRehashPatientTable(false,DbmMode.Check),"nothing left to report");
		}

		///<summary>Counts patient rows whose hash differs from HMAC-SHA256 of the PatNum under the patient key, without the code under test.</summary>
		private static long CountNotMatchingIndependently() {
			DataTable table=DataCore.GetTable("SELECT PatNum,SecurityHash FROM patient");
			long count=0;
			foreach(DataRow row in table.Rows) {
				if(row["SecurityHash"].ToString()!=TamperHash.Compute(row["PatNum"].ToString(),isPatientTable:true)) {
					count++;
				}
			}
			return count;
		}
		#endregion patient
	}
}
