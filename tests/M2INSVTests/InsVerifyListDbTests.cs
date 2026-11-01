// The Insurance Verification List of Open Dental 25.4 (M2-INSV) on a scratch database: a fresh 24.3.63 database (as tools/New-DevDatabase.ps1
// builds it) brought to the 26.1.70 schema by the fork migration runner, so insverify.DateLastAssigned is a datetime. Synthetic rows only. Skipped
// (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2INSVTests {
	[TestClass]
	public class InsVerifyListDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("m2insv");
		private static string _skipReason;
		private static Userod _userod;
		private static int _numNext=0;

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
			long userGroupNum=DataCore.NonQ("INSERT INTO usergroup (Description) VALUES ('m2insv')",true);
			long userNum=DataCore.NonQ("INSERT INTO userod (UserName,Password,UserGroupNum) VALUES ('m2insv','',"+POut.Long(userGroupNum)+")",true);
			DataCore.NonQ("INSERT INTO usergroupattach (UserNum,UserGroupNum) VALUES ("+POut.Long(userNum)+","+POut.Long(userGroupNum)+")");
			Cache.Refresh(InvalidType.Security);
			_userod=Userods.GetUser(userNum);
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
			Security.CurUser=_userod;
		}

		#region Appointment day range (v25_4-appts-insverify-daterange)
		[TestMethod]
		public void DayRange_ListsOnlyAppointmentsFromMinThroughMaxDays() {
			long carrierNum=InsertCarrier("Syn Range Carrier");
			Dictionary<int,long> dictPatNumsByDays=new Dictionary<int,long>();
			foreach(int days in new int[] { 0,3,6,7,10,14,15,20 }) {
				//The appointment on the last day of a range is late in the evening: the range covers whole days.
				TimeSpan timeOfDay=(days==14 ? new TimeSpan(23,30,0) : new TimeSpan(10,0,0));
				dictPatNumsByDays[days]=InsertCoveredPatientWithAppt(carrierNum,DateTime.Today.AddDays(days).Add(timeOfDay));
			}
			CollectionAssert.AreEquivalent(new[] { 7,10,14 },DaysListed(dictPatNumsByDays,7,14,null));
			CollectionAssert.AreEquivalent(new[] { 0,3,6,7,10,14 },DaysListed(dictPatNumsByDays,0,14,null),"min 0 is the 24.3 list");
			CollectionAssert.AreEquivalent(new[] { 15,20 },DaysListed(dictPatNumsByDays,15,20,null));
			CollectionAssert.AreEquivalent(new[] { 7 },DaysListed(dictPatNumsByDays,7,7,null),"min equal to max: one day");
		}

		[TestMethod]
		public void DayRange_MedicaidPairIsIndependentOfTheStandardPair() {
			//A filing code listed in the Medicaid preference puts the plan on the Medicaid list, which has its own Min and Max boxes.
			InsFilingCode insFilingCode=new InsFilingCode();
			insFilingCode.Descript="Synthetic Medicaid";
			long insFilingCodeNum=InsFilingCodes.Insert(insFilingCode);
			Cache.Refresh(InvalidType.InsFilingCodes);
			string medicaidCodesOld=PrefC.GetString(PrefName.InsVerifyMedicaidFilingCodes);
			Prefs.UpdateString(PrefName.InsVerifyMedicaidFilingCodes,POut.Long(insFilingCodeNum));
			Cache.Refresh(InvalidType.Prefs);
			try {
				long carrierNum=InsertCarrier("Syn Medicaid Carrier");
				long patNumStandard=InsertCoveredPatientWithAppt(carrierNum,DateTime.Today.AddDays(2).AddHours(9));
				long patNumMedicaid=InsertCoveredPatientWithAppt(carrierNum,DateTime.Today.AddDays(2).AddHours(9));
				DataCore.NonQ("UPDATE insplan SET FilingCode="+POut.Long(insFilingCodeNum)+" WHERE PlanNum IN (SELECT PlanNum FROM inssub "
					+"WHERE Subscriber="+POut.Long(patNumMedicaid)+")");
				List<long> listPatNumsOwn=new List<long> { patNumStandard,patNumMedicaid };
				//Standard 0 to 7 days, Medicaid 5 to 10 days: only the standard patient's appointment in 2 days is listed.
				CollectionAssert.AreEquivalent(new[] { patNumStandard },PatNumsListed(listPatNumsOwn,0,7,5,10,null));
				//Standard 5 to 10, Medicaid 0 to 7: only the Medicaid patient.
				CollectionAssert.AreEquivalent(new[] { patNumMedicaid },PatNumsListed(listPatNumsOwn,5,10,0,7,null));
			}
			finally {
				Prefs.UpdateString(PrefName.InsVerifyMedicaidFilingCodes,medicaidCodesOld);
				Cache.Refresh(InvalidType.Prefs);
			}
		}
		#endregion Appointment day range

		#region Carrier filter (v25_4-appts-insverify-carriers)
		[TestMethod]
		public void Carriers_FilterOnOneOrMoreCarriers() {
			long carrierNumA=InsertCarrier("Syn Carrier A");
			long carrierNumB=InsertCarrier("Syn Carrier B");
			long carrierNumC=InsertCarrier("Syn Carrier C");
			long patNumA=InsertCoveredPatientWithAppt(carrierNumA,DateTime.Today.AddDays(2).AddHours(9));
			long patNumB=InsertCoveredPatientWithAppt(carrierNumB,DateTime.Today.AddDays(2).AddHours(9));
			long patNumC=InsertCoveredPatientWithAppt(carrierNumC,DateTime.Today.AddDays(2).AddHours(9));
			List<long> listPatNumsOwn=new List<long> { patNumA,patNumB,patNumC };
			CollectionAssert.AreEquivalent(new[] { patNumA },PatNumsListed(listPatNumsOwn,0,7,new List<long> { carrierNumA }));
			CollectionAssert.AreEquivalent(new[] { patNumA,patNumC },PatNumsListed(listPatNumsOwn,0,7,new List<long> { carrierNumA,carrierNumC }));
			CollectionAssert.AreEquivalent(listPatNumsOwn,PatNumsListed(listPatNumsOwn,0,7,new List<long>()),"no carrier picked: all carriers");
			CollectionAssert.AreEquivalent(listPatNumsOwn,PatNumsListed(listPatNumsOwn,0,7,null),"null: all carriers");
		}

		[TestMethod]
		public void Carriers_AndDayRangeCombine() {
			long carrierNumA=InsertCarrier("Syn Both A");
			long carrierNumB=InsertCarrier("Syn Both B");
			long patNumANear=InsertCoveredPatientWithAppt(carrierNumA,DateTime.Today.AddDays(1).AddHours(9));
			long patNumAFar=InsertCoveredPatientWithAppt(carrierNumA,DateTime.Today.AddDays(9).AddHours(9));
			long patNumBFar=InsertCoveredPatientWithAppt(carrierNumB,DateTime.Today.AddDays(9).AddHours(9));
			List<long> listPatNumsOwn=new List<long> { patNumANear,patNumAFar,patNumBFar };
			CollectionAssert.AreEquivalent(new[] { patNumAFar },PatNumsListed(listPatNumsOwn,5,10,new List<long> { carrierNumA }));
		}

		[TestMethod]
		public void Carriers_CombinedOrDeletedCarrierDropsOutOfTheFilter() {
			long carrierNumKeep=InsertCarrier("Syn Combine Keep");
			long carrierNumGone=InsertCarrier("Syn Combine Gone");
			long patNumGone=InsertCoveredPatientWithAppt(carrierNumGone,DateTime.Today.AddDays(1).AddHours(9));
			Carriers.RefreshCache();
			Carriers.Combine(new List<long> { carrierNumKeep,carrierNumGone },carrierNumKeep);
			Carriers.RefreshCache();
			CollectionAssert.AreEqual(new List<long> { carrierNumKeep },InsVerifies.GetCarrierNumsStillPresent(new List<long> { carrierNumGone,carrierNumKeep }));
			Assert.AreEqual(0,InsVerifies.GetCarrierNumsStillPresent(new List<long> { carrierNumGone }).Count,"only a combined carrier left: all carriers");
			Assert.AreEqual(0,InsVerifies.GetCarrierNumsStillPresent(null).Count);
			//The plan moved to the kept carrier, so the patient is listed under it.
			CollectionAssert.AreEquivalent(new[] { patNumGone },PatNumsListed(new List<long> { patNumGone },0,7,new List<long> { carrierNumKeep }));
		}
		#endregion Carrier filter

		#region Assignment time
		[TestMethod]
		public void DateLastAssigned_KeepsTheTimeOfDayInInsVerifyAndHistory() {
			Assert.AreEqual("datetime",DataCore.GetScalar("SELECT DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() "
				+"AND TABLE_NAME='insverify' AND COLUMN_NAME='DateLastAssigned'"));
			long carrierNum=InsertCarrier("Syn Time Carrier");
			long planNum=InsertPlan(carrierNum);
			InsVerify insVerify=InsVerifies.GetOneByFKey(planNum,VerifyTypes.InsuranceBenefit);
			DateTime dateTimeAssigned=DateTime.Today.AddHours(14).AddMinutes(37).AddSeconds(12);
			insVerify.UserNum=_userod.UserNum;
			insVerify.DateLastAssigned=dateTimeAssigned;
			InsVerifies.Update(insVerify);
			InsVerify insVerifyDb=InsVerifies.GetOne(insVerify.InsVerifyNum);
			Assert.AreEqual(dateTimeAssigned,insVerifyDb.DateLastAssigned);
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerifyDb),"the hash covers the date part, so the time does not break it");
			InsVerifyHists.InsertFromInsVerify(insVerifyDb);
			InsVerifyHist insVerifyHist=InsVerifyHists.GetForFKeyByType(planNum,VerifyTypes.InsuranceBenefit).Single();
			Assert.AreEqual(dateTimeAssigned,insVerifyHist.DateLastAssigned,"history keeps the time");
			Assert.IsTrue(InsVerifyHists.IsInsVerifyHistHashValid(insVerifyHist));
			Assert.AreEqual(DateTime.MinValue,InsVerifies.GetOne(insVerify.InsVerifyNum).DateLastAssigned,"verifying still resets the assignment");
		}

		[TestMethod]
		public void Cleanup_ClearsAssignmentsMoreThan30WholeDaysOld() {
			long carrierNum=InsertCarrier("Syn Cleanup Carrier");
			DateTime dateCutoff=DateTime.Today.AddDays(-30);
			InsVerify insVerifyOld=AssignPatientRow(InsertCoveredPatient(carrierNum),dateCutoff.AddSeconds(-1));//31 days ago, 23:59:59
			InsVerify insVerifyMidnight=AssignPatientRow(InsertCoveredPatient(carrierNum),dateCutoff);//30 days ago, 00:00
			InsVerify insVerifyMorning=AssignPatientRow(InsertCoveredPatient(carrierNum),dateCutoff.AddHours(8));//30 days ago, 08:00
			long patNumWithAppt=InsertCoveredPatientWithAppt(carrierNum,DateTime.Today.AddDays(3).AddHours(9));
			InsVerify insVerifyOldWithAppt=AssignPatientRow(patNumWithAppt,dateCutoff.AddDays(-10));
			InsVerifies.CleanupInsVerifyRows(DateTime.Today,DateTime.Today.AddDays(7),DateTime.Today.AddDays(7));
			InsVerify insVerifyOldDb=InsVerifies.GetOne(insVerifyOld.InsVerifyNum);
			Assert.AreEqual(0,insVerifyOldDb.UserNum,"more than 30 whole days old: cleared");
			Assert.AreEqual(DateTime.MinValue,insVerifyOldDb.DateLastAssigned);
			Assert.IsTrue(InsVerifies.IsInsVerifyHashValid(insVerifyOldDb));
			Assert.AreEqual(dateCutoff,InsVerifies.GetOne(insVerifyMidnight.InsVerifyNum).DateLastAssigned,"30 days ago: kept");
			Assert.AreEqual(dateCutoff.AddHours(8),InsVerifies.GetOne(insVerifyMorning.InsVerifyNum).DateLastAssigned,"30 days ago, any time: kept");
			Assert.AreEqual(_userod.UserNum,InsVerifies.GetOne(insVerifyOldWithAppt.InsVerifyNum).UserNum,"the patient has an appointment in the list: kept");
		}
		#endregion Assignment time

		#region Helpers
		private static int NextNum() {
			return ++_numNext;
		}

		private static long InsertCarrier(string carrierName) {
			Carrier carrier=new Carrier();
			carrier.CarrierName=carrierName+" "+NextNum();
			Carriers.Insert(carrier);
			return carrier.CarrierNum;
		}

		private static long InsertPlan(long carrierNum) {
			InsPlan insPlan=new InsPlan();
			insPlan.CarrierNum=carrierNum;
			insPlan.GroupName="Synthetic group";
			insPlan.GroupNum="SYN"+NextNum();
			return InsPlans.Insert(insPlan);
		}

		///<summary>A patient with its own plan of the carrier, as subscriber, with nothing verified yet.</summary>
		private static long InsertCoveredPatient(long carrierNum) {
			Patient patient=new Patient();
			patient.LName="Synthetic";
			patient.FName="Insv"+NextNum();
			patient.PatStatus=PatientStatus.Patient;
			Patients.Insert(patient,false);
			InsSub insSub=new InsSub();
			insSub.PlanNum=InsertPlan(carrierNum);
			insSub.Subscriber=patient.PatNum;
			insSub.SubscriberID="SYN"+NextNum();
			InsSubs.Insert(insSub);
			PatPlan patPlan=new PatPlan();
			patPlan.PatNum=patient.PatNum;
			patPlan.InsSubNum=insSub.InsSubNum;
			patPlan.Ordinal=1;
			patPlan.Relationship=Relat.Self;
			PatPlans.Insert(patPlan);
			return patient.PatNum;
		}

		private static long InsertCoveredPatientWithAppt(long carrierNum,DateTime aptDateTime) {
			long patNum=InsertCoveredPatient(carrierNum);
			Appointment appointment=new Appointment();
			appointment.PatNum=patNum;
			appointment.AptDateTime=aptDateTime;
			appointment.AptStatus=ApptStatus.Scheduled;
			appointment.Pattern="//";
			OpenPmsDentalBusiness.Crud.AppointmentCrud.Insert(appointment);
			return patNum;
		}

		///<summary>Assigns the patient's eligibility row (insverify of the patplan) to the test user at the given time, as the list does.</summary>
		private static InsVerify AssignPatientRow(long patNum,DateTime dateTimeAssigned) {
			PatPlan patPlan=PatPlans.Refresh(patNum).Single();
			InsVerify insVerify=InsVerifies.GetOneByFKey(patPlan.PatPlanNum,VerifyTypes.PatientEnrollment);
			insVerify.UserNum=_userod.UserNum;
			insVerify.DateLastAssigned=dateTimeAssigned;
			InsVerifies.Update(insVerify);
			return insVerify;
		}

		///<summary>The patients of listPatNumsOwn that the list shows for the range of days until the appointment and the carriers, as the window
		///asks for them: all clinics, all users and statuses, verifications older than 30 days.</summary>
		private static List<long> PatNumsListed(List<long> listPatNumsOwn,int daysMin,int daysMax,List<long> listCarrierNums) {
			return PatNumsListed(listPatNumsOwn,daysMin,daysMax,daysMin,daysMax,listCarrierNums);
		}

		///<summary>As above, with separate day ranges for the standard and the Medicaid list.</summary>
		private static List<long> PatNumsListed(List<long> listPatNumsOwn,int daysMinStandard,int daysMaxStandard,int daysMinMedicaid,int daysMaxMedicaid,
			List<long> listCarrierNums)
		{
			InsVerifies.GetApptDateRange(DateTime.Today,daysMinStandard,daysMaxStandard,out DateTime dateStartStandard,out DateTime dateEndStandard);
			InsVerifies.GetApptDateRange(DateTime.Today,daysMinMedicaid,daysMaxMedicaid,out DateTime dateStartMedicaid,out DateTime dateEndMedicaid);
			DateTime dateLastVerified=DateTime.Today.AddDays(-30);
			List<InsVerifyGridObject> listInsVerifyGridObjects=InsVerifies.GetVerifyGridList(dateStartStandard,dateEndStandard,dateLastVerified,dateLastVerified,
				new List<long>(),new List<long> { -1 },0,-1,listCarrierNums,false,false,
				dateStartMedicaid,dateEndMedicaid,dateLastVerified,dateLastVerified,InsVerifyListType.Both);
			return listInsVerifyGridObjects.Select(x => x.GetPatNum()).Distinct().Where(x => listPatNumsOwn.Contains(x)).ToList();
		}

		private static List<int> DaysListed(Dictionary<int,long> dictPatNumsByDays,int daysMin,int daysMax,List<long> listCarrierNums) {
			List<long> listPatNums=PatNumsListed(dictPatNumsByDays.Values.ToList(),daysMin,daysMax,listCarrierNums);
			return dictPatNumsByDays.Where(x => listPatNums.Contains(x.Value)).Select(x => x.Key).ToList();
		}
		#endregion Helpers
	}
}
