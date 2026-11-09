// The Insurance Verification List and its batch verification when a patient plan and its insurance plan have the same key (INSVERIFY,
// docs/port-notes/INSVERIFY.md). The list's query unions the patient plans' eligibility rows (insverify.VerifyType PatientEnrollment, FKey a
// PatPlanNum) and the plans' benefits rows (InsuranceBenefit, FKey a PlanNum) and groups them; grouped by PatPlanNum and FKey only, the two rows
// of a patient plan whose PlanNum equals its PatPlanNum fell into one group and one of them was lost. Each test sets the next keys of insplan and
// patplan itself, so no test depends on the keys earlier tests used. Synthetic rows only, on the scratch database of InsvDb.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Eclaims;
using UnitTestsCore;

namespace INSVERIFYTests {
	[TestClass]
	public class VerifyGridKeyTests {
		///<summary>A 271 that reports active family coverage and the group number XYZ123 (the shape of the legacy unit tests' simple response, with
		///synthetic names). Nothing is sent: x270Controller uses it instead of a clearinghouse's answer.</summary>
		private const string FAKE_271="ISA*00*          *00*          *30*330989922      *29*AA0989922      *030606*0936*U*00401*000013966*0*T*:~"
			+"GS*HB*330989922*AA0989922*20030606*0936*13966*X*004010X092~ST*271*0001~BHT*0022*11*ASX012145WEB*20030606*0936~HL*1**20*1~"
			+"NM1*PR*2*SYNTHETICPAYER*****PI*12345~HL*2*1*21*1~NM1*1P*1*PROVLAST*PROVFIRST****SV*5558006~HL*3*2*22*0~TRN*2*100*1330989922~"
			+"NM1*IL*1*SYNTHETIC*INSV*B***MI*123456789~REF*6P*XYZ123*GROUPNAME~REF*18*2484568*TEST PLAN NAME~N3*100 EXAMPLE ST*~"
			+"N4*SAMPLE CITY*ZZ*99999~DMG*D8*19800101*M~DTP*307*RD8*19910712-19920525~EB*1*FAM*30~SE*17*0001~GE*1*13966~IEA*1*000013966~";
		private static int _numNext=0;

		[TestInitialize]
		public void TestInitialize() {
			InsvDb.Begin();
		}

		[TestCleanup]
		public void TestCleanup() {
			x270Controller.FakeResponseOverride271="";
		}

		#region Verification list
		[TestMethod]
		public void GridList_PlanNumEqualsPatPlanNum_ListsTheEligibilityAndTheBenefitsRow() {
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Assert.AreEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum,"the setup gives the plan and the patient plan the same key");
			//Both year checks off (the default): the eligibility row and the benefits row of one patient plan are combined into one grid row.
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
		}

		[TestMethod]
		public void GridList_PlanNumEqualsPatPlanNum_SeparateRowsWhenOnlyOneYearCheckIsOn() {
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Assert.AreEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum);
			//One of the two year checks on: the list shows the patient's eligibility and the plan's benefits as two rows.
			Prefs.UpdateBool(PrefName.InsVerifyFutureDateBenefitYear,true);
			Prefs.UpdateBool(PrefName.InsVerifyFutureDatePatEnrollmentYear,false);
			Cache.Refresh(InvalidType.Prefs);
			try {
				List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
				Assert.AreEqual(2,listInsVerifyGridObjects.Count,"an eligibility row and a benefits row");
				InsVerifyGridObject insVerifyGridObjectPat=listInsVerifyGridObjects.SingleOrDefault(x => x.IsOnlyPatRow());
				InsVerifyGridObject insVerifyGridObjectPlan=listInsVerifyGridObjects.SingleOrDefault(x => x.IsOnlyInsRow());
				Assert.IsNotNull(insVerifyGridObjectPat,"the eligibility row is listed");
				Assert.IsNotNull(insVerifyGridObjectPlan,"the benefits row is listed");
				AssertEligibilityRow(insVerifyGridObjectPat.InsVerifyPat,coverage);
				AssertBenefitsRow(insVerifyGridObjectPlan.InsVerifyPlan,coverage);
			}
			finally {
				Prefs.UpdateBool(PrefName.InsVerifyFutureDateBenefitYear,false);
				Cache.Refresh(InvalidType.Prefs);
			}
		}

		[TestMethod]
		public void GridList_DifferentKeys_ListsTheEligibilityAndTheBenefitsRow() {
			//The usual case, before and after the fix: the keys differ, nothing to merge.
			long key=GetKeyAboveBoth();
			Coverage coverage=InsertCoveredPatient(new long[] { key,key+50 });
			Assert.AreNotEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count);
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
		}

		[TestMethod]
		public void Query_PlanNumEqualsPatPlanNum_OneRowPerPatientPlanVerifyTypeAndKey() {
			//The query still collapses what belongs together: two appointments in the range and a second benefits row of the plan (an entry the
			//Database Maintenance check InsVerifyRedundantEntries would move to the history) still give one row per patient plan, verify type and
			//key. Only the eligibility row and the benefits row no longer collapse into each other.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Assert.AreEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum);
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(3).AddHours(14),coverage.OperatoryNum,coverage.ProvNum);
			InsVerifies.InsertForPlanNum(coverage.InsPlan.PlanNum);
			DataTable table=RunGridQuery(coverage.Carrier.CarrierNum);
			List<string> listKeys=table.Select().Where(x => PIn.Long(x["PatPlanNum"].ToString())==coverage.PatPlan.PatPlanNum)
				.Select(x => ((VerifyTypes)PIn.Int(x["VerifyType"].ToString())).ToString()+":"+PIn.Long(x["FKey"].ToString()))
				.OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(new List<string> {
					VerifyTypes.InsuranceBenefit.ToString()+":"+coverage.InsPlan.PlanNum,
					VerifyTypes.PatientEnrollment.ToString()+":"+coverage.PatPlan.PatPlanNum
				},listKeys,"rows of the patient plan: "+string.Join(", ",listKeys));
		}
		#endregion Verification list

		#region Batch verification
		[TestMethod]
		public void BatchVerify_PlanNumEqualsPatPlanNum_VerifiesTheEligibilityRow() {
			//The batch verification (InsVerifies.TryBatchPatInsVerify, which the scheduled processes run) asks the clearinghouse about the patient
			//rows of the list. With the eligibility row lost it skipped the patient as a benefits-only row.
			long provNum=ProviderT.CreateProvider("SYN"+NextNum(),"Synthetic","Provider",ssn:"123456789",isUsingTIN:true,nationalProvID:"0123456789");
			Prefs.UpdateLong(PrefName.PracticeDefaultProv,provNum);
			Prefs.UpdateString(PrefName.PracticePhone,"5035550100");
			Prefs.UpdateString(PrefName.PracticeAddress,"100 Example Street");
			Prefs.UpdateString(PrefName.PracticeCity,"Sample City");
			Prefs.UpdateString(PrefName.PracticeST,"ZZ");
			Prefs.UpdateString(PrefName.PracticeZip,"99999");
			foreach(PrefName prefName in new[] { PrefName.InsBatchVerifyCreateAdjustments,PrefName.InsBatchVerifyCheckAnnualMax,
				PrefName.InsBatchVerifyCheckDeductible,PrefName.InsBatchVerifyChangeInsHist,PrefName.InsBatchVerifyChangeEffectiveDates })
			{
				Prefs.UpdateBool(prefName,false);
			}
			Clearinghouse clearinghouse=ClearinghouseT.CreateClearinghouse("Synthetic eligibility "+NextNum(),commBridge:EclaimsCommBridge.ClaimConnect,
				eFormat:ElectronicClaimFormat.x837D_5010_dental,isa05:"ZZ",isa07:"30",isa08:"SYNTHETIC",isa15:"T");
			Prefs.UpdateLong(PrefName.ClearinghouseDefaultEligibility,clearinghouse.ClearinghouseNum);
			Cache.Refresh(InvalidType.Prefs);
			Coverage coverage=InsertCoveredPatient(KeysEqual(),provNum,isTrustedForEligibility:true);
			Assert.AreEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum);
			x270Controller.FakeResponseOverride271=FAKE_271;
			List<InsVerify> listInsVerifies=InsVerifies.TryBatchPatInsVerify().FindAll(x => x.PatNum==coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifies.Count,"the patient's eligibility row is verified");
			Assert.AreEqual(VerifyTypes.PatientEnrollment,listInsVerifies[0].VerifyType);
			Assert.AreEqual(coverage.PatPlan.PatPlanNum,listInsVerifies[0].FKey);
			Assert.AreEqual(BatchInsVerifyState.Success,listInsVerifies[0].BatchVerifyState,"note: "+listInsVerifies[0].Note);
			Assert.AreEqual(DateTime.Today,InsVerifies.GetOneByFKey(coverage.PatPlan.PatPlanNum,VerifyTypes.PatientEnrollment).DateLastVerified);
			Assert.AreEqual(DateTime.MinValue,InsVerifies.GetOneByFKey(coverage.InsPlan.PlanNum,VerifyTypes.InsuranceBenefit).DateLastVerified,
				"the batch verifies eligibility only; the plan's benefits row is untouched");
		}
		#endregion Batch verification

		#region Helpers
		private class Coverage {
			public Patient Patient;
			public Carrier Carrier;
			public InsPlan InsPlan;
			public InsSub InsSub;
			public PatPlan PatPlan;
			public long ProvNum;
			public long OperatoryNum;
		}

		private static int NextNum() {
			return ++_numNext;
		}

		///<summary>A key above every key insplan and patplan hold or have handed out, so a test that uses it never meets an earlier test's rows.
		///</summary>
		private static long GetKeyAboveBoth() {
			long key=0;
			foreach(string command in new[] {
				"SELECT COALESCE(MAX(PlanNum),0)+1 FROM insplan",
				"SELECT COALESCE(MAX(PatPlanNum),0)+1 FROM patplan",
				"SELECT COALESCE(AUTO_INCREMENT,1) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='insplan'",
				"SELECT COALESCE(AUTO_INCREMENT,1) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='patplan'" })
			{
				key=Math.Max(key,PIn.Long(DataCore.GetScalar(command)));
			}
			return key+100;
		}

		///<summary>The next PlanNum and the next PatPlanNum, equal, as they are in a database whose two counters happen to meet.</summary>
		private static long[] KeysEqual() {
			long key=GetKeyAboveBoth();
			return new long[] { key,key };
		}

		///<summary>A patient with an appointment in two days, the subscriber of a plan of a carrier of its own, with nothing verified yet. The plan
		///gets arrayKeys[0] and the patient plan arrayKeys[1]: the two tables' next keys are set first, as a database's counters would stand.</summary>
		private static Coverage InsertCoveredPatient(long[] arrayKeys,long provNum=0,bool isTrustedForEligibility=false) {
			Coverage coverage=new Coverage();
			int num=NextNum();
			coverage.ProvNum=(provNum!=0 ? provNum : ProviderT.CreateProvider("SYN"+num,"Synthetic","Provider"));
			coverage.Patient=PatientT.CreatePatient(priProvNum:coverage.ProvNum,lName:"Synthetic",fName:"Insv"+num,birthDate:new DateTime(1980,1,1));
			if(isTrustedForEligibility) {
				coverage.Carrier=CarrierT.CreateCarrier("Syn Insv Carrier "+num,"100 Example Street","Sample City","ZZ","99999","12345",
					TrustedEtransTypes.RealTimeEligibility);
			}
			else {
				coverage.Carrier=CarrierT.CreateCarrier("Syn Insv Carrier "+num);
			}
			DataCore.NonQ("ALTER TABLE insplan AUTO_INCREMENT="+POut.Long(arrayKeys[0]));
			DataCore.NonQ("ALTER TABLE patplan AUTO_INCREMENT="+POut.Long(arrayKeys[1]));
			coverage.InsPlan=InsPlanT.CreateInsPlan(coverage.Carrier.CarrierNum,groupNum:"XYZ123");
			coverage.InsSub=InsSubT.CreateInsSub(coverage.Patient.PatNum,coverage.InsPlan.PlanNum,"SYN"+num.ToString("000000"));
			coverage.PatPlan=PatPlanT.CreatePatPlan(1,coverage.Patient.PatNum,coverage.InsSub.InsSubNum);
			Assert.AreEqual(arrayKeys[0],coverage.InsPlan.PlanNum,"the plan got the key the test set");
			Assert.AreEqual(arrayKeys[1],coverage.PatPlan.PatPlanNum,"the patient plan got the key the test set");
			coverage.OperatoryNum=OperatoryT.CreateOperatory("S"+num,"Synthetic "+num).OperatoryNum;
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(2).AddHours(9),coverage.OperatoryNum,coverage.ProvNum);
			return coverage;
		}

		///<summary>The list's rows of the patient, as the window asks for them with every filter on "All": appointments from today through 7 days,
		///verifications older than 30 days, the standard and the Medicaid list.</summary>
		private static List<InsVerifyGridObject> GetGridObjectsForPatient(long patNum) {
			DateTime dateLastVerified=DateTime.Today.AddDays(-30);
			List<InsVerifyGridObject> listInsVerifyGridObjects=InsVerifies.GetVerifyGridList(DateTime.Today,DateTime.Today.AddDays(7),dateLastVerified,
				dateLastVerified,new List<long> { -1 },new List<long> { 0 },0,-1,new List<long>(),false,false,
				DateTime.Today,DateTime.Today.AddDays(7),dateLastVerified,dateLastVerified,InsVerifyListType.Both);
			return listInsVerifyGridObjects.FindAll(x => x.GetPatNum()==patNum);
		}

		///<summary>The rows the list's query returns for the carrier, before they are turned into grid rows (InsVerifies.GetVerifyGridListQuery, a
		///private method): appointments from today through 7 days, verifications older than 30 days, plans without a filing code.</summary>
		private static DataTable RunGridQuery(long carrierNum) {
			MethodInfo methodInfo=typeof(InsVerifies).GetMethod("GetVerifyGridListQuery",BindingFlags.NonPublic|BindingFlags.Static);
			Assert.IsNotNull(methodInfo,"InsVerifies.GetVerifyGridListQuery");
			DateTime dateLastVerified=DateTime.Today.AddDays(-30);
			string command=(string)methodInfo.Invoke(null,new object[] { DateTime.Today,DateTime.Today.AddDays(7),dateLastVerified,dateLastVerified,
				new List<long> { -1 },new List<long> { 0 },0L,-1L,new List<long> { carrierNum },false,false,new List<long> { 0 } });
			return DataCore.GetTable(command);
		}

		private static void AssertEligibilityRow(InsVerify insVerify,Coverage coverage) {
			Assert.IsNotNull(insVerify,"the patient's eligibility row (patient enrollment) is listed");
			Assert.AreEqual(VerifyTypes.PatientEnrollment,insVerify.VerifyType);
			Assert.AreEqual(coverage.PatPlan.PatPlanNum,insVerify.FKey);
			Assert.AreEqual(coverage.PatPlan.PatPlanNum,insVerify.PatPlanNum);
			Assert.AreEqual(coverage.InsPlan.PlanNum,insVerify.PlanNum);
			Assert.AreEqual(coverage.Patient.PatNum,insVerify.PatNum);
		}

		private static void AssertBenefitsRow(InsVerify insVerify,Coverage coverage) {
			Assert.IsNotNull(insVerify,"the plan's benefits row (insurance benefit) is listed");
			Assert.AreEqual(VerifyTypes.InsuranceBenefit,insVerify.VerifyType);
			Assert.AreEqual(coverage.InsPlan.PlanNum,insVerify.FKey);
			Assert.AreEqual(coverage.PatPlan.PatPlanNum,insVerify.PatPlanNum);
			Assert.AreEqual(coverage.InsPlan.PlanNum,insVerify.PlanNum);
			Assert.AreEqual(coverage.Patient.PatNum,insVerify.PatNum);
		}
		#endregion Helpers
	}
}
