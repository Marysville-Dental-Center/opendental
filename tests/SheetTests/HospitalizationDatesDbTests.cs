// Box 18 hospitalization dates from the data the claim printout reads: the patient's admit date (loaded with the family) and the EHR discharge
// date. Synthetic data only (backlog package A-SHEET).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace SheetTests {
	[TestClass]
	public class HospitalizationDatesDbTests {
		[TestInitialize]
		public void TestInitialize() {
			SheetDb.Begin();
		}

		[TestMethod]
		public void MedicalClaim_ReadsAdmitAndDischargeDates() {
			string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
			Patient patientInserted=PatientT.CreatePatient(suffix,lName:"Asheet",fName:"Hospital",admitDate:new DateTime(2025,8,10));
			EhrPatientT.CreateEhrPatient(patientInserted,dischargeDate:new DateTime(2025,8,14,11,0,0));
			//As the claim printout loads them.
			Patient patient=Patients.GetFamily(patientInserted.PatNum).GetPatient(patientInserted.PatNum);
			EhrPatient ehrPatient=EhrPatients.GetOne(patient.PatNum);
			Assert.IsNotNull(ehrPatient);
			EhrPatients.GetHospitalizationDates(new Claim { PatNum=patient.PatNum,MedType=EnumClaimMedType.Medical },patient,ehrPatient,
				out DateTime dateFrom,out DateTime dateTo);
			Assert.AreEqual(new DateTime(2025,8,10),dateFrom);
			Assert.AreEqual(new DateTime(2025,8,14),dateTo);
			Assert.AreEqual("08    10    25",dateFrom.ToString("MM    dd    yy"));
		}

		[TestMethod]
		public void NoEhrRecord_PrintsFromOnly() {
			string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
			Patient patientInserted=PatientT.CreatePatient(suffix,lName:"Asheet",fName:"NoEhr",admitDate:new DateTime(2025,9,1));
			Patient patient=Patients.GetFamily(patientInserted.PatNum).GetPatient(patientInserted.PatNum);
			Assert.IsNull(EhrPatients.GetOne(patient.PatNum),"Reading the dates never creates an EHR record.");
			EhrPatients.GetHospitalizationDates(new Claim { MedType=EnumClaimMedType.Medical },patient,EhrPatients.GetOne(patient.PatNum),
				out DateTime dateFrom,out DateTime dateTo);
			Assert.AreEqual(new DateTime(2025,9,1),dateFrom);
			Assert.AreEqual(DateTime.MinValue,dateTo);
		}
	}
}
