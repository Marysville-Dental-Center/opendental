// Appointment estimates when an insurance plan is saved (Open Dental 26.2 Appointments item 7, port P262-APPT) on the scratch database: which
// patients of a plan get their estimates computed (InsPlans.GetPatNumsWithScheduledApptsForPlan, ComputeEstimatesForApptsOnPlan), seen from the
// estimate rows their treatment-planned procedures get. Synthetic data only (a made-up Z code, made-up carriers and patients).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262ApptTests {
	[TestClass]
	public class PlanEstimatesDbTests {
		private const string PROC_CODE="Z9262";
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			ApptDb.Begin();
		}

		private static Patient NewPatient(string name) {
			_counter++;
			return PatientT.CreatePatient("P262Est"+_counter,lName:"Synthetic",fName:name);
		}

		///<summary>Covers the patient by the plan with an inssub of their own (another subscriber of the same plan).</summary>
		private static void AddToPlan(Patient patient,long planNum) {
			InsSub insSub=InsSubT.CreateInsSub(patient.PatNum,planNum,"P262-"+patient.PatNum);
			PatPlanT.CreatePatPlan(1,patient.PatNum,insSub.InsSubNum);
		}

		///<summary>A treatment-planned procedure, on a new appointment of the given status and date when one is given; without estimate rows.</summary>
		private static Procedure NewTpProc(Patient patient,DateTime? dateAppt,ApptStatus apptStatus=ApptStatus.Scheduled) {
			long aptNum=0;
			if(dateAppt!=null) {
				aptNum=AppointmentT.CreateAppointment(patient.PatNum,dateAppt.Value.Date.AddHours(10),0,patient.PriProv,aptStatus:apptStatus).AptNum;
			}
			Procedure procedure=ProcedureT.CreateProcedure(patient,PROC_CODE,ProcStat.TP,"",120,aptNum:aptNum);
			DataCore.NonQ("DELETE FROM claimproc WHERE ProcNum="+POut.Long(procedure.ProcNum));
			return procedure;
		}

		private static bool HasEstimate(Procedure procedure,long planNum) {
			return ClaimProcs.RefreshForProc(procedure.ProcNum).Any(x => x.PlanNum==planNum && x.Status==ClaimProcStatus.Estimate);
		}

		[TestMethod]
		public void SavingAPlan_ComputesThePatientsWithScheduledAppointmentsFromToday() {
			Patient patientA=NewPatient("Subscriber");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patientA,"P262 Carrier One");
			long planNum=insuranceInfo.PriInsPlan.PlanNum;
			Patient patientB=NewPatient("Tomorrow");
			Patient patientC=NewPatient("NoAppointment");
			Patient patientD=NewPatient("Yesterday");
			Patient patientG=NewPatient("Unscheduled");
			Patient patientToday=NewPatient("Today");
			foreach(Patient patient in new[] { patientB,patientC,patientD,patientG,patientToday }) {
				AddToPlan(patient,planNum);
			}
			Patient patientE=NewPatient("OtherPlan");
			long planNumOther=InsuranceT.AddInsurance(patientE,"P262 Carrier Two").PriInsPlan.PlanNum;
			DateTime dateTomorrow=DateTime.Today.AddDays(1);
			Procedure procA=NewTpProc(patientA,dateTomorrow);
			Procedure procB=NewTpProc(patientB,dateTomorrow);
			Procedure procC=NewTpProc(patientC,null);
			Procedure procD=NewTpProc(patientD,DateTime.Today.AddDays(-1));
			Procedure procG=NewTpProc(patientG,dateTomorrow,ApptStatus.UnschedList);
			Procedure procToday=NewTpProc(patientToday,DateTime.Today);
			Procedure procE=NewTpProc(patientE,dateTomorrow);
			List<long> listExpected=new[] { patientA.PatNum,patientB.PatNum,patientToday.PatNum }.OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(listExpected,InsPlans.GetPatNumsWithScheduledApptsForPlan(planNum,DateTime.Today),
				"Covered by the plan, with a Scheduled appointment today or later.");
			//The plan window computed the subscriber's patients already (ComputeEstimatesForSubscriber), so it passes them as done.
			CollectionAssert.AreEqual(new List<long> { patientA.PatNum },InsPlans.GetPatNumsForSubscriber(patientA.PatNum));
			List<long> listComputed=InsPlans.ComputeEstimatesForApptsOnPlan(planNum,DateTime.Today,InsPlans.GetPatNumsForSubscriber(patientA.PatNum));
			CollectionAssert.AreEqual(new[] { patientB.PatNum,patientToday.PatNum }.OrderBy(x => x).ToList(),listComputed);
			Assert.IsTrue(HasEstimate(procB,planNum),"Another subscriber of the plan with an appointment tomorrow.");
			Assert.IsTrue(HasEstimate(procToday,planNum),"An appointment today.");
			Assert.IsFalse(HasEstimate(procA,planNum),"Skipped: already computed by the caller.");
			Assert.IsFalse(HasEstimate(procC,planNum),"No appointment.");
			Assert.IsFalse(HasEstimate(procD,planNum),"Only an appointment in the past.");
			Assert.IsFalse(HasEstimate(procG,planNum),"An appointment on the Unscheduled List.");
			Assert.IsFalse(HasEstimate(procE,planNumOther),"Another plan.");
			//Without patients done (the plan saved from Lists, Insurance Plans): every such patient.
			listComputed=InsPlans.ComputeEstimatesForApptsOnPlan(planNum,DateTime.Today);
			CollectionAssert.AreEqual(listExpected,listComputed);
			Assert.IsTrue(HasEstimate(procA,planNum));
			Assert.IsFalse(HasEstimate(procC,planNum));
		}

		[TestMethod]
		public void NoPlan_ComputesNothing() {
			Assert.AreEqual(0,InsPlans.ComputeEstimatesForApptsOnPlan(0,DateTime.Today).Count);
		}
	}
}
