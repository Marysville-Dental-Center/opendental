// Adding procedures to the Active treatment plan against a scratch database: status, priority, treatment plan attaches, fees and estimates as
// Chart entry makes them, the Active-plan-only rule, all-or-nothing validation, quick buttons, typed codes and the audit entry. Synthetic data
// only (made-up Z codes and names).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace BTPTests {
	[TestClass]
	public class AddToActivePlanDbTests {
		private const string CODE_MOUTH="Z7101";
		private const string CODE_TOOTH="Z7102";
		private const string CODE_SURF="Z7103";
		private const string CODE_QUAD="Z7104";
		private const string CODE_RANGE="Z7105";

		[TestInitialize]
		public void TestInitialize() {
			TpDb.Begin();
			ProcedureCodeT.CreateProcCode(CODE_MOUTH,treatmentArea:TreatmentArea.Mouth,descript:"B-TP synthetic mouth code",abbrDesc:"BtpM");
			ProcedureCodeT.CreateProcCode(CODE_TOOTH,treatmentArea:TreatmentArea.Tooth,descript:"B-TP synthetic tooth code",abbrDesc:"BtpT");
			ProcedureCodeT.CreateProcCode(CODE_SURF,treatmentArea:TreatmentArea.Surf,descript:"B-TP synthetic surface code",abbrDesc:"BtpS");
			ProcedureCodeT.CreateProcCode(CODE_QUAD,treatmentArea:TreatmentArea.Quad,descript:"B-TP synthetic quadrant code",abbrDesc:"BtpQ");
			ProcedureCodeT.CreateProcCode(CODE_RANGE,treatmentArea:TreatmentArea.ToothRange,descript:"B-TP synthetic range code",abbrDesc:"BtpR");
		}

		private static long CodeNum(string procCode) {
			return ProcedureCodes.GetCodeNum(procCode);
		}

		private static Patient NewPatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Btp",fName:"Synthetic");
		}

		private static TreatPlan NewPlan(Patient patient,TreatPlanStatus treatPlanStatus,string heading) {
			TreatPlan treatPlan=new TreatPlan {
				PatNum=patient.PatNum,
				TPStatus=treatPlanStatus,
				Heading=heading,
				Note="",
				DateTP=DateTime.Today,
				TPType=TreatPlanType.Insurance
			};
			TreatPlans.Insert(treatPlan);
			return treatPlan;
		}

		private static long PriorityDefNum() {
			Def def=Defs.GetDefsForCategory(DefCat.TxPriorities,true).FirstOrDefault();
			if(def==null) {
				def=DefT.CreateDefinition(DefCat.TxPriorities,"B-TP priority");
			}
			return def.DefNum;
		}

		private static ProcAddEntry Entry(string procCode,string toothEntry="",string surfEntry="") {
			return new ProcAddEntry { CodeNum=CodeNum(procCode),ToothEntry=toothEntry,SurfEntry=surfEntry };
		}

		private static void AssertThrowsOD(Action action,string expectedPart) {
			try {
				action();
			}
			catch(ODException ex) {
				StringAssert.Contains(ex.Message,expectedPart);
				return;
			}
			Assert.Fail("Expected an ODException containing '"+expectedPart+"'.");
		}

		[TestMethod]
		public void AddsTreatmentPlannedProcsToTheActivePlanWithThePriority() {
			Patient patient=NewPatient("Add");
			TreatPlan treatPlanActive=NewPlan(patient,TreatPlanStatus.Active,"Active Treatment Plan");
			long priorityNum=PriorityDefNum();
			List<ProcAddEntry> listEntries=new List<ProcAddEntry> { Entry(CODE_MOUTH),Entry(CODE_TOOTH,"3,5"),Entry(CODE_SURF,"30","dmo") };
			ProcAddResult procAddResult=ProcAddHelper.AddToActivePlan(patient.PatNum,treatPlanActive.TreatPlanNum,listEntries,priorityNum,DateTime.Today);
			Assert.IsFalse(procAddResult.IsTreatPlanCreated);
			Assert.AreEqual(treatPlanActive.TreatPlanNum,procAddResult.TreatPlanActive.TreatPlanNum);
			Assert.AreEqual(4,procAddResult.ListProcedures.Count,"The tooth code typed for two teeth makes two procedures.");
			List<Procedure> listProcedures=Procedures.Refresh(patient.PatNum);
			Assert.AreEqual(4,listProcedures.Count);
			Assert.IsTrue(listProcedures.All(x => x.ProcStatus==ProcStat.TP));
			Assert.IsTrue(listProcedures.All(x => x.DateTP==DateTime.Today && x.ProcDate==DateTime.Today));
			Assert.IsTrue(listProcedures.All(x => x.Priority==priorityNum));
			Assert.IsTrue(listProcedures.All(x => x.ProvNum==patient.PriProv),"The provider is chosen as Chart entry chooses it.");
			CollectionAssert.AreEquivalent(new[] { "","3","5","30" },listProcedures.Select(x => x.ToothNum).ToArray());
			Assert.AreEqual("MOD",listProcedures.Single(x => x.CodeNum==CodeNum(CODE_SURF)).Surf);
			List<TreatPlanAttach> listTreatPlanAttaches=TreatPlanAttaches.GetAllForTreatPlan(treatPlanActive.TreatPlanNum);
			CollectionAssert.AreEquivalent(listProcedures.Select(x => x.ProcNum).ToArray(),listTreatPlanAttaches.Select(x => x.ProcNum).ToArray());
			Assert.IsTrue(listTreatPlanAttaches.All(x => x.Priority==priorityNum));
			//The module audits the plans when it refreshes: nothing moves.
			TreatPlans.AuditPlans(patient.PatNum,TreatPlanType.Insurance);
			Assert.IsTrue(Procedures.Refresh(patient.PatNum).All(x => x.ProcStatus==ProcStat.TP && x.Priority==priorityNum));
			Assert.AreEqual(4,TreatPlanAttaches.GetAllForTreatPlan(treatPlanActive.TreatPlanNum).Count);
			List<TreatPlan> listTreatPlans=TreatPlans.GetAllForPat(patient.PatNum);
			Assert.AreEqual(1,listTreatPlans.Count,"No Unassigned plan and no second Active plan.");
		}

		[TestMethod]
		public void NoPriority_IsZero() {
			Patient patient=NewPatient("NoPri");
			TreatPlan treatPlanActive=NewPlan(patient,TreatPlanStatus.Active,"Active Treatment Plan");
			ProcAddHelper.AddToActivePlan(patient.PatNum,treatPlanActive.TreatPlanNum,new List<ProcAddEntry> { Entry(CODE_TOOTH,"8") },0,DateTime.Today);
			Assert.AreEqual(0,Procedures.Refresh(patient.PatNum).Single().Priority);
			Assert.AreEqual(0,TreatPlanAttaches.GetAllForTreatPlan(treatPlanActive.TreatPlanNum).Single().Priority);
		}

		[TestMethod]
		public void PatientWithNoPlan_GetsAnActivePlan() {
			Patient patient=NewPatient("NoPlan");
			ProcAddResult procAddResult=ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry(CODE_MOUTH) },0,DateTime.Today);
			Assert.IsTrue(procAddResult.IsTreatPlanCreated);
			TreatPlan treatPlanActive=TreatPlans.GetActiveForPat(patient.PatNum);
			Assert.IsNotNull(treatPlanActive);
			Assert.AreEqual(treatPlanActive.TreatPlanNum,procAddResult.TreatPlanActive.TreatPlanNum);
			Assert.AreEqual(1,TreatPlans.GetAllForPat(patient.PatNum).Count);
			Assert.AreEqual(procAddResult.ListProcedures.Single().ProcNum,TreatPlanAttaches.GetAllForTreatPlan(treatPlanActive.TreatPlanNum).Single().ProcNum);
		}

		[TestMethod]
		public void OnlySavedPlans_GetsAnActivePlan_AndNumberZeroUsesAnExistingActivePlan() {
			Patient patient=NewPatient("Saved");
			TreatPlan treatPlanSaved=NewPlan(patient,TreatPlanStatus.Saved,"Saved plan");
			ProcAddResult procAddResult=ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry(CODE_TOOTH,"14") },0,DateTime.Today);
			Assert.IsTrue(procAddResult.IsTreatPlanCreated);
			Assert.AreNotEqual(treatPlanSaved.TreatPlanNum,procAddResult.TreatPlanActive.TreatPlanNum);
			Assert.AreEqual(TreatPlanStatus.Saved,TreatPlans.GetOne(treatPlanSaved.TreatPlanNum).TPStatus,"The saved plan is left alone.");
			Assert.AreEqual(0,TreatPlanAttaches.GetAllForTreatPlan(treatPlanSaved.TreatPlanNum).Count);
			//Another workstation created the Active plan meanwhile: number 0 adds to it instead of making a second one.
			ProcAddResult procAddResultSecond=ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry(CODE_MOUTH) },0,DateTime.Today);
			Assert.IsFalse(procAddResultSecond.IsTreatPlanCreated);
			Assert.AreEqual(procAddResult.TreatPlanActive.TreatPlanNum,procAddResultSecond.TreatPlanActive.TreatPlanNum);
			Assert.AreEqual(2,TreatPlanAttaches.GetAllForTreatPlan(procAddResult.TreatPlanActive.TreatPlanNum).Count);
			Assert.AreEqual(1,TreatPlans.GetAllForPat(patient.PatNum).Count(x => x.TPStatus==TreatPlanStatus.Active));
		}

		[TestMethod]
		public void InactiveOrSavedPlan_IsRefusedAndNothingIsWritten() {
			Patient patient=NewPatient("Inact");
			NewPlan(patient,TreatPlanStatus.Active,"Active Treatment Plan");
			TreatPlan treatPlanInactive=NewPlan(patient,TreatPlanStatus.Inactive,"Inactive Treatment Plan");
			TreatPlan treatPlanSaved=NewPlan(patient,TreatPlanStatus.Saved,"Saved plan");
			List<ProcAddEntry> listEntries=new List<ProcAddEntry> { Entry(CODE_MOUTH) };
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,treatPlanInactive.TreatPlanNum,listEntries,0,DateTime.Today),"Active");
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,treatPlanSaved.TreatPlanNum,listEntries,0,DateTime.Today),"Active");
			Assert.AreEqual(0,Procedures.Refresh(patient.PatNum).Count);
		}

		[TestMethod]
		public void AnotherPatientsPlan_IsRefused() {
			Patient patient=NewPatient("Mine");
			Patient patientOther=NewPatient("Other");
			TreatPlan treatPlanOther=NewPlan(patientOther,TreatPlanStatus.Active,"Active Treatment Plan");
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,treatPlanOther.TreatPlanNum,new List<ProcAddEntry> { Entry(CODE_MOUTH) },0,
				DateTime.Today),"another patient");
			Assert.AreEqual(0,Procedures.Refresh(patient.PatNum).Count);
			Assert.AreEqual(0,Procedures.Refresh(patientOther.PatNum).Count);
		}

		[TestMethod]
		public void PlanMadeInactiveMeanwhile_IsRefused() {
			Patient patient=NewPatient("Stale");
			TreatPlan treatPlanOld=NewPlan(patient,TreatPlanStatus.Active,"Active Treatment Plan");
			//Another workstation makes a different plan Active while the Add Procedures window is open.
			TreatPlan treatPlanNew=NewPlan(patient,TreatPlanStatus.Active,"New plan");
			TreatPlans.SetOtherActiveTPsToInactive(treatPlanNew);
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,treatPlanOld.TreatPlanNum,new List<ProcAddEntry> { Entry(CODE_MOUTH) },0,
				DateTime.Today),"Active");
			Assert.AreEqual(0,Procedures.Refresh(patient.PatNum).Count);
		}

		[TestMethod]
		public void OneInvalidRow_NothingIsWritten() {
			Patient patient=NewPatient("Invalid");
			List<ProcAddEntry> listEntries=new List<ProcAddEntry> { Entry(CODE_MOUTH),Entry(CODE_SURF,"","MO") };
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,0,listEntries,0,DateTime.Today),CODE_SURF+": Enter the tooth");
			Assert.AreEqual(0,Procedures.Refresh(patient.PatNum).Count);
			Assert.AreEqual(0,TreatPlans.GetAllForPat(patient.PatNum).Count,"No plan is created either.");
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry>(),0,DateTime.Today),"No procedures");
		}

		[TestMethod]
		public void HiddenCategoryAndGroupNoteCodes_AreRefused() {
			Def defHidden=DefT.CreateDefinition(DefCat.ProcCodeCats,"B-TP hidden category",isHidden:true);
			ProcedureCodeT.CreateProcCode("Z7190",procCat:defHidden.DefNum,treatmentArea:TreatmentArea.Mouth,descript:"B-TP synthetic hidden code");
			ProcedureCode procedureCodeHidden=ProcedureCodes.GetProcCode("Z7190");
			StringAssert.Contains(ProcAddHelper.GetCodeError(procedureCodeHidden),"hidden");
			Patient patient=NewPatient("Hidden");
			AssertThrowsOD(() => ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry("Z7190") },0,DateTime.Today),"hidden");
			Assert.AreEqual(0,Procedures.Refresh(patient.PatNum).Count);
			StringAssert.Contains(ProcAddHelper.GetCodeError(new ProcedureCode { CodeNum=1,ProcCode=ProcedureCodes.GroupProcCode }),"Group notes");
			StringAssert.Contains(ProcAddHelper.GetCodeError(null),"not found");
			StringAssert.Contains(ProcAddHelper.GetEntryError(new ProcAddEntry { CodeNum=0 }),"not found");
			Assert.AreEqual("",ProcAddHelper.GetEntryError(Entry(CODE_SURF,"30","MOD")));
		}

		[TestMethod]
		public void FeeAndEstimate_AsChartEntry() {
			Patient patient=NewPatient("Est");
			long feeSchedNum=FeeSchedT.CreateFeeSched(FeeScheduleType.Normal,"BtpEst");
			FeeT.CreateFee(feeSchedNum,CodeNum(CODE_TOOTH),200);
			Patient patientOld=patient.Copy();
			patient.FeeSched=feeSchedNum;
			Patients.Update(patient,patientOld);
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"B-TP carrier");
			BenefitT.CreatePercentForProc(insuranceInfo.PriInsPlan.PlanNum,CodeNum(CODE_TOOTH),80);
			ProcAddResult procAddResult=ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry(CODE_TOOTH,"19") },0,DateTime.Today);
			Procedure procedure=Procedures.GetOneProc(procAddResult.ListProcedures.Single().ProcNum,false);
			Assert.AreEqual(200,procedure.ProcFee,"The fee comes from the patient's fee schedule.");
			List<ClaimProc> listClaimProcs=ClaimProcs.Refresh(patient.PatNum).FindAll(x => x.ProcNum==procedure.ProcNum);
			ClaimProc claimProcEstimate=listClaimProcs.Single(x => x.Status==ClaimProcStatus.Estimate);
			Assert.AreEqual(160,claimProcEstimate.InsEstTotal,"The estimate is computed when the procedure is created.");
			Assert.AreEqual(1,procAddResult.GetClaimProcs(procedure.ProcNum).Count,"The result carries the new procedure's estimates.");
			Assert.AreEqual(0,procAddResult.GetClaimProcs(-1).Count);
		}

		[TestMethod]
		public void QuadAndToothRangeCodes() {
			Patient patient=NewPatient("Areas");
			ProcAddResult procAddResult=ProcAddHelper.AddToActivePlan(patient.PatNum,0,
				new List<ProcAddEntry> { Entry(CODE_QUAD,"3,14"),Entry(CODE_QUAD,"","ll"),Entry(CODE_RANGE,"6-11") },0,DateTime.Today);
			List<Procedure> listProcedures=Procedures.Refresh(patient.PatNum);
			Assert.AreEqual(4,listProcedures.Count);
			CollectionAssert.AreEquivalent(new[] { "UR","UL","LL" },
				listProcedures.Where(x => x.CodeNum==CodeNum(CODE_QUAD)).Select(x => x.Surf).ToArray());
			Assert.IsTrue(listProcedures.Where(x => x.CodeNum==CodeNum(CODE_QUAD)).All(x => x.ToothNum==""),"Quadrant procedures keep no tooth.");
			Assert.AreEqual("6,7,8,9,10,11",listProcedures.Single(x => x.CodeNum==CodeNum(CODE_RANGE)).ToothRange);
			Assert.AreEqual(4,procAddResult.ListProcedures.Count);
		}

		[TestMethod]
		public void AuditEntryAndFirstVisit() {
			Patient patient=NewPatient("Audit");
			Assert.IsTrue(patient.DateFirstVisit.Year<1880);
			ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry(CODE_SURF,"30","MOD") },0,DateTime.Today);
			string count=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PatNum="+POut.Long(patient.PatNum)
				+" AND PermType="+POut.Int((int)EnumPermType.ProcEdit)+" AND LogText LIKE '"+CODE_SURF+", tooth 30, MOD, %treatment planned from the Treatment Plan module.'");
			Assert.AreEqual("1",count,"One ProcEdit entry per new procedure.");
			Assert.AreEqual(DateTime.Today,Patients.GetPat(patient.PatNum).DateFirstVisit,"As Chart entry, the first visit date is set.");
		}

		[TestMethod]
		public void NoAutoCodes_RecommendsTheSameCode() {
			Patient patient=NewPatient("Auto");
			ProcAddResult procAddResult=ProcAddHelper.AddToActivePlan(patient.PatNum,0,new List<ProcAddEntry> { Entry(CODE_SURF,"30","MO") },0,DateTime.Today);
			Procedure procedure=procAddResult.ListProcedures.Single();
			Assert.AreEqual(procedure.CodeNum,ProcAddHelper.GetRecommendedCodeNum(procedure,procAddResult.PatientData.Patient,
				procAddResult.GetClaimProcs(procedure.ProcNum)));
		}

		[TestMethod]
		public void QuickButtons_UsableOnesInChartOrder() {
			List<ProcButtonQuick> listProcButtonQuicks=new List<ProcButtonQuick> {
				new ProcButtonQuick { Description="Second row",CodeValue=CODE_TOOTH,YPos=1,ItemOrder=0 },
				new ProcButtonQuick { Description="First row right",CodeValue=CODE_SURF,Surf="mo",YPos=0,ItemOrder=2 },
				new ProcButtonQuick { Description="First row left",CodeValue=CODE_MOUTH,YPos=0,ItemOrder=1 },
				new ProcButtonQuick { Description="A label",CodeValue=CODE_MOUTH,YPos=0,ItemOrder=0,IsLabel=true },
				new ProcButtonQuick { Description="Missing code",CodeValue="Z7199NOPE",YPos=0,ItemOrder=3 },
			};
			List<ProcButtonQuick> listUsable=ProcAddHelper.GetQuickButtons(listProcButtonQuicks);
			CollectionAssert.AreEqual(new[] { "First row left","First row right","Second row" },listUsable.Select(x => x.Description).ToArray());
			ProcAddEntry procAddEntrySurf=ProcAddHelper.GetEntryForQuickButton(listUsable[1]);
			Assert.AreEqual(CodeNum(CODE_SURF),procAddEntrySurf.CodeNum);
			Assert.AreEqual("MO",procAddEntrySurf.SurfEntry,"A surface code takes the button's surfaces.");
			ProcAddEntry procAddEntryTooth=ProcAddHelper.GetEntryForQuickButton(new ProcButtonQuick { CodeValue=CODE_TOOTH,Surf="MO" });
			Assert.AreEqual("",procAddEntryTooth.SurfEntry,"A tooth code ignores the button's surfaces, as Chart entry does.");
			Assert.AreEqual(0,ProcAddHelper.GetQuickButtons(null).Count);
		}

		[TestMethod]
		public void TypedCodes() {
			ProcedureCodeT.CreateProcCode("9871",treatmentArea:TreatmentArea.Mouth,descript:"B-TP synthetic four digit code");
			Assert.AreEqual(CodeNum(CODE_MOUTH),ProcAddHelper.FindTypedCode(" "+CODE_MOUTH+" ",isUnitedStates:true));
			Assert.AreEqual(CodeNum("9871"),ProcAddHelper.FindTypedCode("9871",isUnitedStates:true),"A visible four digit code is used as typed.");
			Assert.AreEqual(0,ProcAddHelper.FindTypedCode("9872",isUnitedStates:true),"Not found, and not found with a D in front either.");
			Assert.AreEqual(0,ProcAddHelper.FindTypedCode("Z7199NOPE",isUnitedStates:true));
			Assert.AreEqual(0,ProcAddHelper.FindTypedCode("",isUnitedStates:true));
			Assert.AreEqual(0,ProcAddHelper.FindTypedCode(null,isUnitedStates:false));
		}
	}
}
