// The Insurance Verification List's choice within one patient plan, verify type and key (INSV2, docs/port-notes/INSVERIFY.md): each row shows the
// latest verification of its key and the earliest appointment in the range, as Open Dental 23.3.54 to 24.2.1 did (JobNum 53236). Before, the row
// was whichever MariaDB read first of the group. The second part of VerifyGridKeyTests: the same scratch database and helpers. Every key gets its
// rows here in the order the test names (the rows the plan and the patient plan got when they were inserted are deleted first). The list asks for
// verifications older than 30 days, so the dates here are 100 and 40 days ago (both due) or 5 days ago (not due, except with a year check for an
// appointment after the plan's renewal). Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace INSVERIFYTests {
	public partial class VerifyGridKeyTests {
		#region Latest verification
		[TestMethod]
		public void GridList_TwoRowsForOneKey_ShowsTheLatestVerification_LatestInsertedFirst() {
			AssertTheLatestVerificationIsShown(isLatestInsertedFirst:true);
		}

		[TestMethod]
		public void GridList_TwoRowsForOneKey_ShowsTheLatestVerification_LatestInsertedSecond() {
			AssertTheLatestVerificationIsShown(isLatestInsertedFirst:false);
		}

		[TestMethod]
		public void GridList_TwoRowsOnTheSameDate_ShowsTheRowInsertedLater() {
			//The tie GetOneByFKey and Database Maintenance's InsVerifyRedundantEntries break the same way: the newer row.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			DateTime date=DateTime.Today.AddDays(-40);
			long insVerifyNumPat=InsertTwoVerifications(VerifyTypes.PatientEnrollment,coverage.PatPlan.PatPlanNum,date,date,isLatestInsertedFirst:false);
			long insVerifyNumPlan=InsertTwoVerifications(VerifyTypes.InsuranceBenefit,coverage.InsPlan.PlanNum,date,date,isLatestInsertedFirst:false);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
			Assert.AreEqual(insVerifyNumPat,listInsVerifyGridObjects[0].InsVerifyPat.InsVerifyNum,"the eligibility row inserted later");
			Assert.AreEqual(insVerifyNumPlan,listInsVerifyGridObjects[0].InsVerifyPlan.InsVerifyNum,"the benefits row inserted later");
		}

		[TestMethod]
		public void GridList_LatestVerificationNotDue_KeyNotListedForAnOlderRow() {
			//The patient's eligibility was verified 5 days ago; an older row of the same key, verified 100 days ago and inserted after it, would be due.
			//The key's latest verification decides, as in Open Dental 23.3.54 to 24.2.1: no eligibility row. The plan's benefits row is still due.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			InsertTwoVerifications(VerifyTypes.PatientEnrollment,coverage.PatPlan.PatPlanNum,DateTime.Today.AddDays(-100),DateTime.Today.AddDays(-5),
				isLatestInsertedFirst:true);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one row for the patient");
			Assert.IsTrue(listInsVerifyGridObjects[0].IsOnlyInsRow(),"only the plan's benefits row is listed");
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
		}

		[TestMethod]
		public void GridList_PlanNumEqualsPatPlanNum_ALaterBenefitsVerificationDoesNotHideTheEligibilityRow() {
			//The latest row is the latest of its own verify type and key: the plan's benefits row (same FKey, the keys are equal) was verified 5 days
			//ago, the patient's eligibility never. The eligibility row is listed on its own.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Assert.AreEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum);
			InsertTwoVerifications(VerifyTypes.InsuranceBenefit,coverage.InsPlan.PlanNum,DateTime.Today.AddDays(-100),DateTime.Today.AddDays(-5),
				isLatestInsertedFirst:false);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one row for the patient");
			Assert.IsTrue(listInsVerifyGridObjects[0].IsOnlyPatRow(),"only the patient's eligibility row is listed");
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
		}
		#endregion Latest verification

		#region Earliest appointment
		[TestMethod]
		public void GridList_SeveralAppointments_ShowsTheEarliestInTheRange() {
			//The patient's first appointment is in two days at 9:00 (InsertCoveredPatient). Created after it, so with higher AptNums: one later in
			//another clinic, the earliest in a third clinic, and two that do not count: a broken one earlier in the range and a completed one yesterday,
			//before the range.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Clinic clinicLater=InsertClinic();
			Clinic clinicEarliest=InsertClinic();
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(5).AddHours(8),coverage.OperatoryNum,coverage.ProvNum,
				clinicNum:clinicLater.ClinicNum);
			Appointment appointmentEarliest=AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(2).AddHours(8.5),
				coverage.OperatoryNum,coverage.ProvNum,clinicNum:clinicEarliest.ClinicNum);
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(1).AddHours(8),coverage.OperatoryNum,coverage.ProvNum,
				aptStatus:ApptStatus.Broken);
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(-1).AddHours(8),coverage.OperatoryNum,coverage.ProvNum,
				aptStatus:ApptStatus.Complete);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
			AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPat,appointmentEarliest,clinicEarliest);
			AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPlan,appointmentEarliest,clinicEarliest);
		}

		[TestMethod]
		public void GridList_TwoAppointmentsAtTheSameTime_ShowsTheLowerAptNum() {
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Clinic clinicFirst=InsertClinic();
			Clinic clinicSecond=InsertClinic();
			Appointment appointmentFirst=AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(1).AddHours(8),
				coverage.OperatoryNum,coverage.ProvNum,clinicNum:clinicFirst.ClinicNum);
			Appointment appointmentSecond=AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(1).AddHours(8),
				coverage.OperatoryNum,coverage.ProvNum,clinicNum:clinicSecond.ClinicNum);
			Assert.IsTrue(appointmentFirst.AptNum<appointmentSecond.AptNum);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
			AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPat,appointmentFirst,clinicFirst);
			AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPlan,appointmentFirst,clinicFirst);
		}

		[TestMethod]
		public void GridList_PlanOfTwoPatientsAtTheSameTime_TheBenefitsRowGoesWithTheLowerAptNum() {
			//A family member on the subscriber's plan (a patient plan on the same subscriber row) with an appointment at the same time, created after
			//the subscriber's. The list shows a plan's benefits row once, with the first of its rows in the query's order: the order is complete
			//(AptDateTime, AptNum, PatPlanNum, VerifyType), so that is the subscriber's row, combined with the subscriber's eligibility.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Patient patientFamily=PatientT.CreatePatient(priProvNum:coverage.ProvNum,lName:"Synthetic",fName:"InsvFamily"+NextNum(),
				birthDate:new DateTime(1985,1,1));
			PatPlan patPlanFamily=PatPlanT.CreatePatPlan(1,patientFamily.PatNum,coverage.InsSub.InsSubNum);
			AppointmentT.CreateAppointment(patientFamily.PatNum,DateTime.Today.AddDays(2).AddHours(9),coverage.OperatoryNum,coverage.ProvNum);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the subscriber");
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
			List<InsVerifyGridObject> listInsVerifyGridObjectsFamily=GetGridObjectsForPatient(patientFamily.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjectsFamily.Count,"one row for the family member");
			Assert.IsTrue(listInsVerifyGridObjectsFamily[0].IsOnlyPatRow(),"the family member's eligibility only");
			Assert.AreEqual(patPlanFamily.PatPlanNum,listInsVerifyGridObjectsFamily[0].InsVerifyPat.FKey);
		}
		#endregion Earliest appointment

		#region Year check
		[TestMethod]
		public void GridList_YearCheck_ShowsTheEarliestAppointmentTheVerificationIsDueFor() {
			//The plan renews on the first of the month after next (GetDateRenew). Both verifications were done 5 days ago, so neither is due by its age
			//(the list asks for older than 30 days). With a year check on, a verification is also due for an appointment on or after the plan's renewal
			//date in that appointment's year when it was done before that date. In the range: an appointment two days before the renewal, which it is
			//not due for, and two after it, created in the opposite order. The row shows the earliest appointment it is due for, the day after the
			//renewal, as Open Dental 24.2.1 listed it (the first row of each key in AptDateTime order).
			DateTime dateRenew=GetDateRenew();
			Coverage coverage=InsertCoveredPatient(KeysEqual(),monthRenew:(byte)dateRenew.Month);
			ReplaceVerification(VerifyTypes.PatientEnrollment,coverage.PatPlan.PatPlanNum,DateTime.Today.AddDays(-5));
			ReplaceVerification(VerifyTypes.InsuranceBenefit,coverage.InsPlan.PlanNum,DateTime.Today.AddDays(-5));
			Clinic clinicAfter=InsertClinic();
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,dateRenew.AddDays(-2).AddHours(9),coverage.OperatoryNum,coverage.ProvNum);
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,dateRenew.AddDays(3).AddHours(9),coverage.OperatoryNum,coverage.ProvNum);
			Appointment appointmentAfter=AppointmentT.CreateAppointment(coverage.Patient.PatNum,dateRenew.AddDays(1).AddHours(9),coverage.OperatoryNum,
				coverage.ProvNum,clinicNum:clinicAfter.ClinicNum);
			Dictionary<PrefName,string> dictPrefsBefore=GetPrefValues(PrefName.InsVerifyFutureDateBenefitYear,PrefName.InsVerifyFutureDatePatEnrollmentYear);
			try {
				SetYearChecks(isBenefitYearOn:false,isPatEnrollmentYearOn:false);
				Assert.AreEqual(0,GetGridObjectsForPatient(coverage.Patient.PatNum,dateRenew.AddDays(-5),dateRenew.AddDays(5)).Count,
					"without the year checks neither verification is due");
				SetYearChecks(isBenefitYearOn:true,isPatEnrollmentYearOn:true);
				List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum,dateRenew.AddDays(-5),
					dateRenew.AddDays(5));
				Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
				AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
				AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
				AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPat,appointmentAfter,clinicAfter);
				AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPlan,appointmentAfter,clinicAfter);
			}
			finally {
				RestorePrefValues(dictPrefsBefore);
			}
			AssertPrefValuesRestored(dictPrefsBefore);
		}

		[TestMethod]
		public void GridList_YearCheck_EachVerificationShowsTheEarliestAppointmentItIsDueFor() {
			//As above, but the eligibility was verified 40 days ago, so it is due by its age for every appointment, and the benefits 5 days ago, due only
			//from the renewal on. Both year checks on combine the two into one row: its eligibility half shows the appointment before the renewal, the
			//earliest in the range, and its benefits half the earliest after the renewal.
			DateTime dateRenew=GetDateRenew();
			Coverage coverage=InsertCoveredPatient(KeysEqual(),monthRenew:(byte)dateRenew.Month);
			ReplaceVerification(VerifyTypes.PatientEnrollment,coverage.PatPlan.PatPlanNum,DateTime.Today.AddDays(-40));
			ReplaceVerification(VerifyTypes.InsuranceBenefit,coverage.InsPlan.PlanNum,DateTime.Today.AddDays(-5));
			Clinic clinicBefore=InsertClinic();
			Clinic clinicAfter=InsertClinic();
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,dateRenew.AddDays(3).AddHours(9),coverage.OperatoryNum,coverage.ProvNum);
			Appointment appointmentAfter=AppointmentT.CreateAppointment(coverage.Patient.PatNum,dateRenew.AddDays(1).AddHours(9),coverage.OperatoryNum,
				coverage.ProvNum,clinicNum:clinicAfter.ClinicNum);
			Appointment appointmentBefore=AppointmentT.CreateAppointment(coverage.Patient.PatNum,dateRenew.AddDays(-2).AddHours(9),
				coverage.OperatoryNum,coverage.ProvNum,clinicNum:clinicBefore.ClinicNum);
			Dictionary<PrefName,string> dictPrefsBefore=GetPrefValues(PrefName.InsVerifyFutureDateBenefitYear,PrefName.InsVerifyFutureDatePatEnrollmentYear);
			try {
				SetYearChecks(isBenefitYearOn:true,isPatEnrollmentYearOn:true);
				List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum,dateRenew.AddDays(-5),
					dateRenew.AddDays(5));
				Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
				AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
				AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
				AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPat,appointmentBefore,clinicBefore);
				AssertAppointment(listInsVerifyGridObjects[0].InsVerifyPlan,appointmentAfter,clinicAfter);
			}
			finally {
				RestorePrefValues(dictPrefsBefore);
			}
			AssertPrefValuesRestored(dictPrefsBefore);
		}
		#endregion Year check

		#region The query
		[TestMethod]
		public void Query_TwoRowsForOneKeyAndSeveralAppointments_OneRowWithTheLatestVerificationAndTheEarliestAppointment() {
			//The query itself (before the rows become grid rows): one row per patient plan, verify type and key, with the latest verification and the
			//earliest appointment, in the order AptDateTime, AptNum, PatPlanNum, VerifyType.
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			long insVerifyNumPat=InsertTwoVerifications(VerifyTypes.PatientEnrollment,coverage.PatPlan.PatPlanNum,DateTime.Today.AddDays(-100),
				DateTime.Today.AddDays(-40),isLatestInsertedFirst:true);
			long insVerifyNumPlan=InsertTwoVerifications(VerifyTypes.InsuranceBenefit,coverage.InsPlan.PlanNum,DateTime.Today.AddDays(-100),
				DateTime.Today.AddDays(-40),isLatestInsertedFirst:false);
			AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(6).AddHours(8),coverage.OperatoryNum,coverage.ProvNum);
			Appointment appointmentEarliest=AppointmentT.CreateAppointment(coverage.Patient.PatNum,DateTime.Today.AddDays(1).AddHours(15),
				coverage.OperatoryNum,coverage.ProvNum);
			DataTable table=RunGridQuery(coverage.Carrier.CarrierNum);
			List<DataRow> listRows=table.Select().Where(x => PIn.Long(x["PatPlanNum"].ToString())==coverage.PatPlan.PatPlanNum).ToList();
			Assert.AreEqual(2,listRows.Count,"a benefits row and an eligibility row");
			Assert.AreEqual(VerifyTypes.InsuranceBenefit,(VerifyTypes)PIn.Int(listRows[0]["VerifyType"].ToString()),"benefits first (VerifyType 1)");
			Assert.AreEqual(VerifyTypes.PatientEnrollment,(VerifyTypes)PIn.Int(listRows[1]["VerifyType"].ToString()));
			Assert.AreEqual(insVerifyNumPlan,PIn.Long(listRows[0]["InsVerifyNum"].ToString()),"the plan's latest benefits row");
			Assert.AreEqual(insVerifyNumPat,PIn.Long(listRows[1]["InsVerifyNum"].ToString()),"the patient plan's latest eligibility row");
			foreach(DataRow row in listRows) {
				Assert.AreEqual(DateTime.Today.AddDays(-40),PIn.Date(row["DateLastVerified"].ToString()));
				Assert.AreEqual(appointmentEarliest.AptNum,PIn.Long(row["AptNum"].ToString()),"the earliest appointment");
				Assert.AreEqual(appointmentEarliest.AptDateTime,PIn.DateT(row["AptDateTime"].ToString()));
			}
		}
		#endregion The query

		#region Helpers
		///<summary>Both keys of a patient whose keys are equal get two due rows, verified 100 and 40 days ago, the latest inserted first or second;
		///the list shows the latest of each.</summary>
		private void AssertTheLatestVerificationIsShown(bool isLatestInsertedFirst) {
			Coverage coverage=InsertCoveredPatient(KeysEqual());
			Assert.AreEqual(coverage.InsPlan.PlanNum,coverage.PatPlan.PatPlanNum);
			long insVerifyNumPat=InsertTwoVerifications(VerifyTypes.PatientEnrollment,coverage.PatPlan.PatPlanNum,DateTime.Today.AddDays(-100),
				DateTime.Today.AddDays(-40),isLatestInsertedFirst);
			long insVerifyNumPlan=InsertTwoVerifications(VerifyTypes.InsuranceBenefit,coverage.InsPlan.PlanNum,DateTime.Today.AddDays(-100),
				DateTime.Today.AddDays(-40),isLatestInsertedFirst);
			List<InsVerifyGridObject> listInsVerifyGridObjects=GetGridObjectsForPatient(coverage.Patient.PatNum);
			Assert.AreEqual(1,listInsVerifyGridObjects.Count,"one combined row for the patient");
			AssertEligibilityRow(listInsVerifyGridObjects[0].InsVerifyPat,coverage);
			AssertBenefitsRow(listInsVerifyGridObjects[0].InsVerifyPlan,coverage);
			Assert.AreEqual(insVerifyNumPat,listInsVerifyGridObjects[0].InsVerifyPat.InsVerifyNum,"the latest eligibility row");
			Assert.AreEqual(insVerifyNumPlan,listInsVerifyGridObjects[0].InsVerifyPlan.InsVerifyNum,"the latest benefits row");
			Assert.AreEqual(DateTime.Today.AddDays(-40),listInsVerifyGridObjects[0].InsVerifyPat.DateLastVerified);
			Assert.AreEqual(DateTime.Today.AddDays(-40),listInsVerifyGridObjects[0].InsVerifyPlan.DateLastVerified);
		}

		///<summary>Replaces the rows of the key with two, verified on dateOlder and dateLatest, inserted in the order given. Returns the InsVerifyNum
		///of the row verified on dateLatest (on equal dates, the one inserted second when isLatestInsertedFirst is false).</summary>
		private static long InsertTwoVerifications(VerifyTypes verifyType,long fKey,DateTime dateOlder,DateTime dateLatest,bool isLatestInsertedFirst) {
			InsVerifies.DeleteByFKey(fKey,verifyType);
			long insVerifyNumLatest;
			if(isLatestInsertedFirst) {
				insVerifyNumLatest=InsertVerification(verifyType,fKey,dateLatest);
				InsertVerification(verifyType,fKey,dateOlder);
			}
			else {
				InsertVerification(verifyType,fKey,dateOlder);
				insVerifyNumLatest=InsertVerification(verifyType,fKey,dateLatest);
			}
			string command="SELECT COUNT(*) FROM insverify WHERE VerifyType="+POut.Int((int)verifyType)+" AND FKey="+POut.Long(fKey);
			Assert.AreEqual(2,PIn.Int(DataCore.GetScalar(command)),"two rows for "+verifyType+":"+fKey);
			return insVerifyNumLatest;
		}

		///<summary>A row of the key as the program inserts one (InsertForPatPlanNum or InsertForPlanNum), then verified on the date (Update rehashes it).
		///</summary>
		private static long InsertVerification(VerifyTypes verifyType,long fKey,DateTime dateLastVerified) {
			long insVerifyNum=(verifyType==VerifyTypes.PatientEnrollment ? InsVerifies.InsertForPatPlanNum(fKey) : InsVerifies.InsertForPlanNum(fKey));
			InsVerify insVerify=InsVerifies.GetOne(insVerifyNum);
			insVerify.DateLastVerified=dateLastVerified;
			InsVerifies.Update(insVerify);
			return insVerifyNum;
		}

		///<summary>Replaces the rows of the key with one, verified on the date. Returns its InsVerifyNum.</summary>
		private static long ReplaceVerification(VerifyTypes verifyType,long fKey,DateTime dateLastVerified) {
			InsVerifies.DeleteByFKey(fKey,verifyType);
			return InsertVerification(verifyType,fKey,dateLastVerified);
		}

		///<summary>The first of the month after next: 32 to 62 days ahead, so a range of five days on either side of it never holds the appointment
		///InsertCoveredPatient makes in two days, and a verification done in the last 40 days was done before it.</summary>
		private static DateTime GetDateRenew() {
			return new DateTime(DateTime.Today.Year,DateTime.Today.Month,1).AddMonths(2);
		}

		///<summary>Sets the two year checks of the list (InsVerifyFutureDateBenefitYear, InsVerifyFutureDatePatEnrollmentYear) and refreshes the
		///cache. The caller reads their values first and puts them back (GetPrefValues, RestorePrefValues).</summary>
		private static void SetYearChecks(bool isBenefitYearOn,bool isPatEnrollmentYearOn) {
			Prefs.UpdateBool(PrefName.InsVerifyFutureDateBenefitYear,isBenefitYearOn);
			Prefs.UpdateBool(PrefName.InsVerifyFutureDatePatEnrollmentYear,isPatEnrollmentYearOn);
			Cache.Refresh(InvalidType.Prefs);
		}

		private static Clinic InsertClinic() {
			int num=NextNum();
			Clinic clinic=new Clinic();
			clinic.Abbr="SynC"+num;
			clinic.Description="Synthetic Clinic "+num;
			Clinics.Insert(clinic);
			return clinic;
		}

		private static void AssertAppointment(InsVerify insVerify,Appointment appointment,Clinic clinic) {
			Assert.IsNotNull(insVerify);
			Assert.AreEqual(appointment.AptNum,insVerify.AptNum,"the appointment of "+insVerify.VerifyType);
			Assert.AreEqual(appointment.AptDateTime,insVerify.AppointmentDateTime);
			Assert.AreEqual(clinic.ClinicNum,insVerify.ClinicNum,"the clinic of that appointment");
			Assert.AreEqual(clinic.Abbr,insVerify.ClinicName);
		}
		#endregion Helpers
	}
}
