// v26_1-setup-automation-clinic-mismatch: the automation condition "procedure clinic differs from patient clinic" (AutoCondField 14), which
// works only with the ProcedureComplete trigger.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2MISCTests {
	[TestClass]
	public class AutomationClinicMismatchTests {
		private const string CODE_A="Z9001";//synthetic codes, not CDT
		private const string CODE_B="Z9002";
		private const string MESSAGE="Synthetic clinic mismatch warning";

		#region Rules, no database
		[TestMethod]
		public void EnumValue_Is14() {
			Assert.AreEqual(14,(int)AutoCondField.PatientProcedureClinicMismatch);
		}

		[TestMethod]
		public void Mismatch_ProcedureClinicDiffersFromPatientClinic() {
			Assert.IsTrue(AutomationClinicMismatches.IsMismatch(new long[] { 2 },1,hasClinicsEnabled:true));
			Assert.IsFalse(AutomationClinicMismatches.IsMismatch(new long[] { 1 },1,hasClinicsEnabled:true));
			Assert.IsTrue(AutomationClinicMismatches.IsMismatch(new long[] { 1,2 },1,hasClinicsEnabled:true),"Any one completed procedure is enough.");
			Assert.IsTrue(AutomationClinicMismatches.IsMismatch(new long[] { 3 },0,hasClinicsEnabled:true),"A patient with no clinic mismatches any clinic.");
			Assert.IsTrue(AutomationClinicMismatches.IsMismatch(new long[] { 0 },3,hasClinicsEnabled:true));
			Assert.IsFalse(AutomationClinicMismatches.IsMismatch(new long[] { 0 },0,hasClinicsEnabled:true));
			Assert.IsFalse(AutomationClinicMismatches.IsMismatch(new long[0],1,hasClinicsEnabled:true));
			Assert.IsFalse(AutomationClinicMismatches.IsMismatch(null,1,hasClinicsEnabled:true));
			Assert.IsFalse(AutomationClinicMismatches.IsMismatch(new long[] { 2 },1,hasClinicsEnabled:false),"Always false with clinics off.");
		}

		[TestMethod]
		public void OnlyTheProcedureCompleteTrigger() {
			foreach(EnumAutomationTrigger automationTrigger in Enum.GetValues(typeof(EnumAutomationTrigger))) {
				Assert.AreEqual(automationTrigger==EnumAutomationTrigger.ProcedureComplete,AutomationClinicMismatches.IsTriggerAllowed(automationTrigger),
					automationTrigger.ToString());
			}
		}

		[TestMethod]
		public void SaveRefused_WhileTheConditionIsOnAnotherTrigger() {
			List<AutomationCondition> listWith=new List<AutomationCondition> {
				new AutomationCondition { CompareField=AutoCondField.Age,Comparison=AutoCondComparison.GreaterThan,CompareString="10" },
				new AutomationCondition { CompareField=AutoCondField.PatientProcedureClinicMismatch,Comparison=AutoCondComparison.None,CompareString="" },
			};
			List<AutomationCondition> listWithout=listWith.Take(1).ToList();
			Assert.AreEqual("",AutomationClinicMismatches.GetSaveError(EnumAutomationTrigger.ProcedureComplete,listWith));
			string error=AutomationClinicMismatches.GetSaveError(EnumAutomationTrigger.ApptCreate,listWith);
			StringAssert.Contains(error,"ProcedureComplete");
			Assert.AreEqual("",AutomationClinicMismatches.GetSaveError(EnumAutomationTrigger.ApptCreate,listWithout));
			Assert.AreEqual("",AutomationClinicMismatches.GetSaveError(EnumAutomationTrigger.ApptCreate,null));
		}

		[TestMethod]
		public void AutomationCodes_AreTrimmedAndBlankSkipped() {
			CollectionAssert.AreEqual(new List<string> { CODE_A,CODE_B },
				AutomationClinicMismatches.GetAutomationCodes(new Automation { ProcCodes=CODE_A+", "+CODE_B+",," }));
			Assert.AreEqual(0,AutomationClinicMismatches.GetAutomationCodes(new Automation { ProcCodes="" }).Count);
			Assert.AreEqual(0,AutomationClinicMismatches.GetAutomationCodes(null).Count);
		}

		[TestMethod]
		public void ProceduresPassedByTheCaller_OnlyTheCompleteOnes() {
			TestDb.ResetOrSkip();//Procedure's constructor reads a preference
			List<Procedure> listProcedures=new List<Procedure> {
				new Procedure { ProcNum=1,ProcStatus=ProcStat.C,ClinicNum=2 },
				new Procedure { ProcNum=2,ProcStatus=ProcStat.TP,ClinicNum=3 },
			};
			List<Procedure> listResult=AutomationClinicMismatches.GetProceduresForTrigger(listProcedures,patNum:1,listProcCodes:null);
			Assert.AreEqual(1,listResult.Count);
			Assert.AreEqual(1,listResult[0].ProcNum);
			Assert.AreEqual(1,AutomationClinicMismatches.GetProceduresForTrigger(listProcedures[0],1,null).Count);
			Assert.AreEqual(0,AutomationClinicMismatches.GetProceduresForTrigger(null,1,null).Count,"No procedures and no codes: none.");
		}
		#endregion Rules, no database

		#region Database: Automations.Trigger
		private long _clinicNum1;
		private long _clinicNum2;

		private void SetUpDb(bool hasClinics=true) {
			TestDb.ResetOrSkip();
			TestDb.SetPref("EasyNoClinics",hasClinics ? "0" : "1");
			OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode=CODE_A,Descript="Synthetic A",AbbrDesc="SynA",ProcTime="/X/",
				TreatArea=TreatmentArea.Mouth });
			OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode=CODE_B,Descript="Synthetic B",AbbrDesc="SynB",ProcTime="/X/",
				TreatArea=TreatmentArea.Mouth });
			_clinicNum1=Clinics.Insert(new Clinic { Abbr="SYN1",Description="Synthetic clinic 1" });
			_clinicNum2=Clinics.Insert(new Clinic { Abbr="SYN2",Description="Synthetic clinic 2" });
			TestDb.RefreshCaches();
		}

		private long InsertPatient(long clinicNum) {
			return OpenPmsDentalBusiness.Crud.PatientCrud.Insert(new Patient { LName="Synthetic",FName="Patient"+Guid.NewGuid().ToString("N").Substring(0,6),
				PatStatus=PatientStatus.Patient,ClinicNum=clinicNum,Birthdate=new DateTime(1980,1,1) });
		}

		private Procedure InsertCompleted(long patNum,string procCode,long clinicNum,DateTime dateComplete) {
			Procedure procedure=new Procedure { PatNum=patNum,CodeNum=ProcedureCodes.GetCodeNum(procCode),ProcStatus=ProcStat.C,ProcDate=dateComplete,
				DateComplete=dateComplete,DateTP=dateComplete,ClinicNum=clinicNum,ToothNum="",Surf="",ToothRange="" };
			procedure.ProcNum=OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(procedure);
			return procedure;
		}

		///<summary>A pop-up automation on the trigger for CODE_A with the clinic mismatch condition.</summary>
		private static void InsertAutomation(EnumAutomationTrigger automationTrigger=EnumAutomationTrigger.ProcedureComplete,string procCodes=CODE_A) {
			Automation automation=new Automation { Description="Synthetic clinic check",Autotrigger=automationTrigger,ProcCodes=procCodes,
				AutoAction=AutomationAction.PopUp,MessageContent=MESSAGE };
			long automationNum=Automations.Insert(automation);
			AutomationConditions.Insert(new AutomationCondition { AutomationNum=automationNum,CompareField=AutoCondField.PatientProcedureClinicMismatch,
				Comparison=AutoCondComparison.None,CompareString="" });
			Automations.RefreshCache();
			AutomationConditions.RefreshCache();
		}

		///<summary>Fires the trigger the way OpenPmsDental.AutomationL does and returns the pop-up messages shown.</summary>
		private static List<string> Fire(EnumAutomationTrigger automationTrigger,List<string> listProcCodes,long patNum,object triggerObj=null) {
			List<string> listMessages=new List<string>();
			Automations.Trigger<object>(automationTrigger,listProcCodes,patNum,new Dictionary<long,Dictionary<long,DateTime>>(),
				(msg) => listMessages.Add(msg),(msg,caption) => true,(commlog) => { },(sheet) => { },(listProcs) => (Image)null,0,triggerObj);
			return listMessages;
		}

		[TestMethod]
		public void Trigger_WarnsWhenTheCompletedProcedureIsAtAnotherClinic() {
			SetUpDb();
			InsertAutomation();
			long patNum=InsertPatient(_clinicNum1);
			InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today);
			CollectionAssert.AreEqual(new List<string> { MESSAGE },Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A },patNum));
		}

		[TestMethod]
		public void Trigger_SameClinic_NoWarning() {
			SetUpDb();
			InsertAutomation();
			long patNum=InsertPatient(_clinicNum1);
			InsertCompleted(patNum,CODE_A,_clinicNum1,DateTime.Today);
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A },patNum).Count);
		}

		[TestMethod]
		public void Trigger_PatientWithoutClinic_Warns() {
			SetUpDb();
			InsertAutomation();
			long patNum=InsertPatient(0);
			InsertCompleted(patNum,CODE_A,_clinicNum1,DateTime.Today);
			Assert.AreEqual(1,Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A },patNum).Count);
		}

		[TestMethod]
		public void Trigger_ClinicsOff_NeverWarns() {
			SetUpDb(hasClinics:false);
			InsertAutomation();
			long patNum=InsertPatient(_clinicNum1);
			InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today);
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A },patNum).Count);
		}

		[TestMethod]
		public void Trigger_OnlyProceduresSetCompleteToday_AndWithTheAutomationsCodes() {
			SetUpDb();
			InsertAutomation();
			long patNum=InsertPatient(_clinicNum1);
			InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today.AddDays(-1));//completed earlier, at the other clinic
			InsertCompleted(patNum,CODE_B,_clinicNum2,DateTime.Today);//completed now, at the other clinic, but not a code of the automation
			InsertCompleted(patNum,CODE_A,_clinicNum1,DateTime.Today);//completed now, at the patient's clinic
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A,CODE_B },patNum).Count);
		}

		[TestMethod]
		public void Trigger_ProceduresPassedByTheCaller_AreTheOnesChecked() {
			SetUpDb();
			InsertAutomation();
			long patNum=InsertPatient(_clinicNum1);
			Procedure procedureDbMismatch=InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today);
			//The caller completed only this procedure, at the patient's clinic: the other completed procedure of today is not looked at.
			Procedure procedurePassed=new Procedure { PatNum=patNum,CodeNum=ProcedureCodes.GetCodeNum(CODE_A),ProcStatus=ProcStat.C,ClinicNum=_clinicNum1 };
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A },patNum,new List<Procedure> { procedurePassed }).Count);
			Assert.AreEqual(1,Fire(EnumAutomationTrigger.ProcedureComplete,new List<string> { CODE_A },patNum,new List<Procedure> { procedureDbMismatch }).Count);
		}

		[TestMethod]
		public void Trigger_ConditionOnAnotherTrigger_IsNeverTrue() {
			//Saving is refused in the window, but a row could come from elsewhere: the condition is false, so nothing fires.
			SetUpDb();
			InsertAutomation(EnumAutomationTrigger.PatientOpen,procCodes:"");
			long patNum=InsertPatient(_clinicNum1);
			InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today);
			Assert.AreEqual(0,Fire(EnumAutomationTrigger.PatientOpen,null,patNum).Count);
		}

		[TestMethod]
		public void CompletedToday_Query() {
			SetUpDb();
			long patNum=InsertPatient(_clinicNum1);
			InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today);
			InsertCompleted(patNum,CODE_A,_clinicNum2,DateTime.Today.AddDays(-2));
			InsertCompleted(InsertPatient(_clinicNum1),CODE_A,_clinicNum2,DateTime.Today);//another patient
			List<Procedure> listProcedures=AutomationClinicMismatches.GetCompletedToday(patNum,new List<long> { ProcedureCodes.GetCodeNum(CODE_A) },
				DateTime.Today);
			Assert.AreEqual(1,listProcedures.Count);
			Assert.AreEqual(0,AutomationClinicMismatches.GetCompletedToday(patNum,new List<long>(),DateTime.Today).Count);
		}
		#endregion Database: Automations.Trigger
	}
}
