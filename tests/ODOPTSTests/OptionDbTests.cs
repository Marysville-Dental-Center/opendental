// ODOPTS on this run's scratch database (ScratchDb): each option read and written under Open Dental's own row with its default when the row is
// missing, and each behaviour on made-up rows: the Perio Chart's Att Ging count, the secondary claim fee, the eForm import of unmatched entries, the
// Insurance History perio scaling code, the statements a billing run created, and the domain login switch. No office value is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace ODOPTSTests {
	[TestClass]
	public class OptionDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ScratchDb.Begin();
		}

		private static string NewSuffix() {
			return Guid.NewGuid().ToString("N").Substring(0,8);
		}

		#region Each option under Open Dental's row
		[TestMethod]
		public void MissingRows_ReadAsTheDefaults() {
			foreach(string name in OdOptions.ArrayPrefNames) {
				Assert.IsNull(ScratchDb.GetPrefRaw(name),name+": a database from 24.3 has no such row.");
			}
			Assert.IsNull(PerioMeasures.GetRedAttGing());
			Assert.IsTrue(DomainLogin.IsForSigBoxesSettingOn());
			Assert.IsFalse(ClaimFeeForSecondary.IsEnabled());
			Assert.IsTrue(EFormImport.IsAllergiesAsOther());
			Assert.IsTrue(EFormImport.IsProblemsAsOther());
			Assert.AreEqual("",InsHistCategories.GetPerioScalingCodeSetting());
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetEmailMode());
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetSmsMode());
			Assert.IsFalse(StatementAutoSend.IsAnyOn());
			Assert.IsNull(ScratchDb.GetPrefRaw(StatementAutoSend.PREF_SETUP_SAVED));
			Assert.IsFalse(StatementAutoSend.IsSetupSavedHere());
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetEmailModeToUse());
		}

		[TestMethod]
		public void SavingTheDefault_WritesNoRow() {
			Assert.IsFalse(PerioMeasures.SetRedAttGing(null));
			Assert.IsFalse(DomainLogin.SetForSigBoxesSetting(true));
			Assert.IsFalse(ClaimFeeForSecondary.SetEnabled(false));
			Assert.IsFalse(EFormImport.SetAllergiesAsOther(true));
			Assert.IsFalse(EFormImport.SetProblemsAsOther(true));
			Assert.IsFalse(InsHistCategories.SetPerioScalingCodeSetting(" "));
			Assert.IsFalse(StatementAutoSend.SetModes(StatementAutoSendMode.Never,StatementAutoSendMode.Never));
			foreach(string name in OdOptions.ArrayPrefNames) {
				Assert.IsNull(ScratchDb.GetPrefRaw(name),name);
			}
		}

		[TestMethod]
		public void Saving_WritesOpenDentalsRows_ReadBackAfterAReload() {
			Assert.IsTrue(PerioMeasures.SetRedAttGing(1));
			Assert.IsTrue(DomainLogin.SetForSigBoxesSetting(false));
			Assert.IsTrue(ClaimFeeForSecondary.SetEnabled(true));
			Assert.IsTrue(EFormImport.SetAllergiesAsOther(false));
			Assert.IsTrue(EFormImport.SetProblemsAsOther(false));
			Assert.IsTrue(InsHistCategories.SetPerioScalingCodeSetting(" Z4398 "));
			Assert.IsTrue(StatementAutoSend.SetModes(StatementAutoSendMode.Ask,StatementAutoSendMode.Always));
			Assert.AreEqual("1",ScratchDb.GetPrefRaw("PerioRedAttGing"));
			Assert.AreEqual("0",ScratchDb.GetPrefRaw("DomainLoginForSigBoxes"));
			Assert.AreEqual("1",ScratchDb.GetPrefRaw("ClaimUpdateFeeForSecondary"));
			Assert.AreEqual("0",ScratchDb.GetPrefRaw("EFormImportAllergiesAsOther"));
			Assert.AreEqual("0",ScratchDb.GetPrefRaw("EFormImportProblemsAsOther"));
			Assert.AreEqual("Z4398",ScratchDb.GetPrefRaw("InsHistPerioScalingCode"));
			Assert.AreEqual("1",ScratchDb.GetPrefRaw("StatementAutoSendEmail"));
			Assert.AreEqual("2",ScratchDb.GetPrefRaw("StatementAutoSendSMS"));
			Prefs.RefreshCache();//as another workstation reads them
			Assert.AreEqual(1,PerioMeasures.GetRedAttGing());
			Assert.IsFalse(DomainLogin.IsForSigBoxesSettingOn());
			Assert.IsTrue(ClaimFeeForSecondary.IsEnabled());
			Assert.IsFalse(EFormImport.IsAllergiesAsOther());
			Assert.IsFalse(EFormImport.IsProblemsAsOther());
			Assert.AreEqual("Z4398",InsHistCategories.GetPerioScalingCodeSetting());
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetEmailMode());
			Assert.AreEqual(StatementAutoSendMode.Always,StatementAutoSend.GetSmsMode());
			//Back to the defaults: rows that exist are written, not deleted.
			Assert.IsTrue(PerioMeasures.SetRedAttGing(null));
			Assert.AreEqual("",ScratchDb.GetPrefRaw("PerioRedAttGing"),"No threshold is stored as blank.");
			Assert.IsNull(PerioMeasures.GetRedAttGing());
			Assert.IsTrue(DomainLogin.SetForSigBoxesSetting(true));
			Assert.AreEqual("1",ScratchDb.GetPrefRaw("DomainLoginForSigBoxes"));
			Assert.IsTrue(StatementAutoSend.SetModes(StatementAutoSendMode.Never,StatementAutoSendMode.Always));
			Assert.AreEqual("0",ScratchDb.GetPrefRaw("StatementAutoSendEmail"));
			Assert.IsFalse(StatementAutoSend.SetModes(StatementAutoSendMode.Never,StatementAutoSendMode.Always),"Unchanged: nothing written.");
		}

		[TestMethod]
		public void OpenDentalsRows_AreReadAsTheyAre() {
			//Rows as Open Dental may have left them, made up: an unknown statement value never sends, a blank Att Ging row means no threshold.
			ScratchDb.SetPrefRaw("StatementAutoSendEmail","7");
			ScratchDb.SetPrefRaw("PerioRedAttGing","");
			ScratchDb.SetPrefRaw("EFormImportAllergiesAsOther","");
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetEmailMode());
			Assert.IsNull(PerioMeasures.GetRedAttGing());
			Assert.IsTrue(EFormImport.IsAllergiesAsOther(),"Blank reads as the default.");
			//Saving what the row already means leaves it as Open Dental wrote it.
			Assert.IsFalse(StatementAutoSend.SetModes(StatementAutoSendMode.Never,StatementAutoSendMode.Never));
			Assert.AreEqual("7",ScratchDb.GetPrefRaw("StatementAutoSendEmail"));
		}

		[TestMethod]
		public void StatementAutoSendEmail_AlwaysFromOpenDental_AsksUntilSavedHere() {
			//A made-up row as Open Dental's window may have left it: there it most likely meant Message-to-Pay links, not e-mailing the statement.
			ScratchDb.SetPrefRaw("StatementAutoSendEmail","2");
			Assert.AreEqual(StatementAutoSendMode.Always,StatementAutoSend.GetEmailMode(),"The window shows what is stored.");
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetEmailModeToUse(),"Billing asks.");
			Assert.IsTrue(StatementAutoSend.IsEmailAlwaysNotSavedHere());
			Assert.IsTrue(StatementAutoSend.IsAnyOn(),"Billing Options still notes the statements it creates.");
			//Saving the modes alone does not count: only the window's save records it (SetSetupSavedHere).
			Assert.IsFalse(StatementAutoSend.SetModes(StatementAutoSendMode.Always,StatementAutoSendMode.Never));
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetEmailModeToUse());
			Assert.IsTrue(StatementAutoSend.SetSetupSavedHere());
			Assert.AreEqual("1",ScratchDb.GetPrefRaw(StatementAutoSend.PREF_SETUP_SAVED));
			Assert.AreEqual("2",ScratchDb.GetPrefRaw("StatementAutoSendEmail"),"Open Dental's row is left as it was.");
			Assert.IsFalse(StatementAutoSend.SetSetupSavedHere(),"Recorded once.");
			Prefs.RefreshCache();//as another workstation reads it
			Assert.AreEqual(StatementAutoSendMode.Always,StatementAutoSend.GetEmailModeToUse());
			Assert.IsFalse(StatementAutoSend.IsEmailAlwaysNotSavedHere());
			//Saved here, Ask and Never are used as stored.
			StatementAutoSend.SetModes(StatementAutoSendMode.Ask,StatementAutoSendMode.Never);
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetEmailModeToUse());
		}
		#endregion Each option under Open Dental's row

		#region PerioRedAttGing: the Perio Chart
		private static ContrPerio LoadChart(long patNum) {
			FormOpenDental.PatNumCur=patNum;//ContrPerio reads the patient's procedures for the implant marker.
			ContrPerio contrPerio=new ContrPerio();
			contrPerio.LayoutManager.Is96dpi=true;
			contrPerio.Size=new Size(608,685);//default as in FormPerio.designer.cs
			contrPerio.ListPerioExams=PerioExams.Refresh(patNum);
			contrPerio.ListPerioMeasures=PerioMeasures.GetForPatient(patNum);
			contrPerio.IdxExamSelected=contrPerio.ListPerioExams.Count-1;
			contrPerio.SetShowCurrentExamOnly(true);
			contrPerio.LoadData();
			return contrPerio;
		}

		private static void InsertMeasure(long perioExamNum,PerioSequenceType perioSequenceType,int intTooth,int mb=-1,int b=-1,int db=-1,int ml=-1,
			int l=-1,int dl=-1)
		{
			OpenPmsDentalBusiness.Crud.PerioMeasureCrud.Insert(new PerioMeasure { PerioExamNum=perioExamNum,SequenceType=perioSequenceType,IntTooth=intTooth,
				ToothValue=-1,MBvalue=mb,Bvalue=b,DBvalue=db,MLvalue=ml,Lvalue=l,DLvalue=dl });
		}

		[TestMethod]
		public void PerioChart_CountsTheTeethAtOrBelowTheThreshold() {
			Patient patient=PatientT.CreatePatient(NewSuffix(),lName:"Odopts",fName:"Perio");
			long perioExamNum=PerioExams.Insert(new PerioExam { PatNum=patient.PatNum,ExamDate=DateTime.Today,DateTMeasureEdit=new DateTime(2026,1,5,8,30,0) });
			//Attached gingiva: tooth 3 has 2, 0 and -2; tooth 30 has 4, 5 and 6; tooth 19 has 3, 2 and 1.
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,3,mb:5,b:4,db:3);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,mb:3,b:4,db:5);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,30,ml:6,l:7,dl:8);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,30,ml:2,l:2,dl:2);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,19,mb:4,b:4,db:4);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,19,mb:1,b:2,db:3);
			ContrPerio contrPerio=LoadChart(patient.PatNum);
			Assert.AreEqual(0,contrPerio.CountTeeth(PerioSequenceType.AttGing).Count,"No threshold: no tooth, as in 24.4.");
			PerioMeasures.SetRedAttGing(0);
			CollectionAssert.AreEquivalent(new[] { Tooth.Display("3") },contrPerio.CountTeeth(PerioSequenceType.AttGing));
			PerioMeasures.SetRedAttGing(2);
			CollectionAssert.AreEquivalent(new[] { Tooth.Display("3"),Tooth.Display("19") },contrPerio.CountTeeth(PerioSequenceType.AttGing));
			PerioMeasures.SetRedAttGing(4);
			Assert.AreEqual(3,contrPerio.CountTeeth(PerioSequenceType.AttGing).Count);
			//The MGJ row keeps its own threshold.
			Assert.AreEqual(PrefC.GetInt(PrefName.PerioRedMGJ)>=3 ? 1 : 0,contrPerio.CountTeeth(PerioSequenceType.MGJ).Count(x => x==Tooth.Display("3")));
		}
		#endregion PerioRedAttGing: the Perio Chart

		#region ClaimUpdateFeeForSecondary
		private class SecondaryScenario {
			public Patient Patient_;
			public InsPlan PlanPrimary;
			public InsPlan PlanSecondary;
			public long InsSubNumPrimary;
			public long InsSubNumSecondary;
			public Procedure Procedure_;
			public Claim ClaimPrimary;
			public Claim ClaimSecondary;
		}

		private static Claim CreateClaim(Patient patient,InsPlan insPlan,long insSubNum,string claimType,string claimStatus,DateTime dateService) {
			Claim claim=new Claim();
			claim.PatNum=patient.PatNum;
			claim.PlanNum=insPlan.PlanNum;
			claim.InsSubNum=insSubNum;
			claim.ClaimType=claimType;
			claim.MedType=EnumClaimMedType.Dental;
			claim.ClaimStatus=claimStatus;
			claim.DateService=dateService;
			claim.ProvTreat=patient.PriProv;
			claim.ProvBill=patient.PriProv;
			claim.AttachedFlags="Mail";
			Claims.Insert(claim);
			return Claims.GetClaim(claim.ClaimNum);
		}

		private static void CreateClaimProc(Procedure procedure,InsPlan insPlan,long insSubNum,long claimNum) {
			ClaimProc claimProc=new ClaimProc();
			claimProc.PatNum=procedure.PatNum;
			claimProc.ProcNum=procedure.ProcNum;
			claimProc.PlanNum=insPlan.PlanNum;
			claimProc.InsSubNum=insSubNum;
			claimProc.ClaimNum=claimNum;
			claimProc.Status=ClaimProcStatus.NotReceived;
			claimProc.ProvNum=procedure.ProvNum;
			claimProc.ProcDate=procedure.ProcDate;
			claimProc.DateCP=procedure.ProcDate;
			claimProc.FeeBilled=procedure.ProcFee;
			claimProc.CodeSent=ProcedureCodes.GetStringProcCode(procedure.CodeNum);
			claimProc.AllowedOverride=-1;
			claimProc.CopayOverride=-1;
			claimProc.DedEstOverride=-1;
			claimProc.PercentOverride=-1;
			claimProc.PaidOtherInsOverride=-1;
			claimProc.InsEstTotalOverride=-1;
			claimProc.WriteOffEstOverride=-1;
			claimProc.PaidOtherIns=-1;
			claimProc.CopayAmt=-1;
			claimProc.DedEst=-1;
			claimProc.WriteOffEst=-1;
			ClaimProcs.Insert(claimProc);
		}

		///<summary>A patient with a primary and a secondary dental plan and one completed procedure (made-up code, fee 100) on a sent primary claim and
		///on a secondary claim with the given status.</summary>
		private static SecondaryScenario CreateSecondaryScenario(string claimStatusSecondary) {
			SecondaryScenario scenario=new SecondaryScenario();
			string suffix=NewSuffix();
			scenario.Patient_=PatientT.CreatePatient(suffix,lName:"Odopts",fName:"Secondary");
			scenario.PlanPrimary=InsPlanT.CreateInsPlan(CarrierT.CreateCarrier("Synthetic Primary Carrier "+suffix).CarrierNum);
			scenario.PlanSecondary=InsPlanT.CreateInsPlan(CarrierT.CreateCarrier("Synthetic Secondary Carrier "+suffix).CarrierNum);
			scenario.InsSubNumPrimary=InsSubT.CreateInsSub(scenario.Patient_.PatNum,scenario.PlanPrimary.PlanNum).InsSubNum;
			scenario.InsSubNumSecondary=InsSubT.CreateInsSub(scenario.Patient_.PatNum,scenario.PlanSecondary.PlanNum).InsSubNum;
			PatPlanT.CreatePatPlan(1,scenario.Patient_.PatNum,scenario.InsSubNumPrimary);
			PatPlanT.CreatePatPlan(2,scenario.Patient_.PatNum,scenario.InsSubNumSecondary);
			DateTime dateProc=DateTime.Today.AddDays(-5);
			scenario.Procedure_=ProcedureT.CreateProcedure(scenario.Patient_,"Z4397",ProcStat.C,"3",100,dateProc);
			scenario.ClaimPrimary=CreateClaim(scenario.Patient_,scenario.PlanPrimary,scenario.InsSubNumPrimary,"P","S",dateProc);
			CreateClaimProc(scenario.Procedure_,scenario.PlanPrimary,scenario.InsSubNumPrimary,scenario.ClaimPrimary.ClaimNum);
			scenario.ClaimSecondary=CreateClaim(scenario.Patient_,scenario.PlanSecondary,scenario.InsSubNumSecondary,"S",claimStatusSecondary,dateProc);
			CreateClaimProc(scenario.Procedure_,scenario.PlanSecondary,scenario.InsSubNumSecondary,scenario.ClaimSecondary.ClaimNum);
			return scenario;
		}

		///<summary>Saves a new fee on the procedure, as the procedure window does, and returns the old fee.</summary>
		private static double ChangeFee(Procedure procedure,double procFeeNew) {
			Procedure procedureOld=procedure.Copy();
			procedure.ProcFee=procFeeNew;
			Procedures.Update(procedure,procedureOld);
			return procedureOld.ProcFee;
		}

		private static double GetFeeBilled(long claimNum,long procNum) {
			return PIn.Double(DataCore.GetScalar("SELECT FeeBilled FROM claimproc WHERE ClaimNum="+POut.Long(claimNum)+" AND ProcNum="+POut.Long(procNum)));
		}

		[TestMethod]
		public void ClaimUpdateFeeForSecondary_On_TheUnsentSecondaryClaimTakesTheNewFee() {
			ClaimFeeForSecondary.SetEnabled(true);
			SecondaryScenario scenario=CreateSecondaryScenario("H");
			double procFeeOld=ChangeFee(scenario.Procedure_,150);
			List<Claim> listClaimsUpdated=ClaimFeeForSecondary.UpdateForProcFeeChange(scenario.Procedure_,procFeeOld);
			CollectionAssert.AreEqual(new[] { scenario.ClaimSecondary.ClaimNum },listClaimsUpdated.Select(x => x.ClaimNum).ToArray());
			double procFeeTotal=Procedures.GetOneProc(scenario.Procedure_.ProcNum,false).ProcFeeTotal;
			Assert.AreEqual(150,procFeeTotal,0.001,"The made-up procedure has one unit.");
			Assert.AreEqual(procFeeTotal,GetFeeBilled(scenario.ClaimSecondary.ClaimNum,scenario.Procedure_.ProcNum),0.001);
			Assert.AreEqual(procFeeTotal,Claims.GetClaim(scenario.ClaimSecondary.ClaimNum).ClaimFee,0.001,"The claim total follows.");
			Assert.AreEqual(100,GetFeeBilled(scenario.ClaimPrimary.ClaimNum,scenario.Procedure_.ProcNum),0.001,"The sent primary claim keeps its fee.");
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.ClaimEdit+" AND FKey="
				+scenario.ClaimSecondary.ClaimNum+" AND LogText LIKE '%ClaimUpdateFeeForSecondary%'"));
			//The same fee again: nothing to do.
			Assert.AreEqual(0,ClaimFeeForSecondary.UpdateForProcFeeChange(scenario.Procedure_,150).Count);
		}

		[TestMethod]
		public void ClaimUpdateFeeForSecondary_Off_NothingChanges() {
			SecondaryScenario scenario=CreateSecondaryScenario("H");
			double procFeeOld=ChangeFee(scenario.Procedure_,150);
			Assert.AreEqual(0,ClaimFeeForSecondary.UpdateForProcFeeChange(scenario.Procedure_,procFeeOld).Count,"No row: off, as in 24.3.");
			Assert.AreEqual(100,GetFeeBilled(scenario.ClaimSecondary.ClaimNum,scenario.Procedure_.ProcNum),0.001);
		}

		[TestMethod]
		public void ClaimUpdateFeeForSecondary_ASentSecondaryClaimKeepsItsFee() {
			ClaimFeeForSecondary.SetEnabled(true);
			SecondaryScenario scenario=CreateSecondaryScenario("S");
			double procFeeOld=ChangeFee(scenario.Procedure_,150);
			Assert.AreEqual(0,ClaimFeeForSecondary.UpdateForProcFeeChange(scenario.Procedure_,procFeeOld).Count);
			Assert.AreEqual(100,GetFeeBilled(scenario.ClaimSecondary.ClaimNum,scenario.Procedure_.ProcNum),0.001);
		}
		#endregion ClaimUpdateFeeForSecondary

		#region EFormImportAllergiesAsOther and EFormImportProblemsAsOther
		private static EFormField Field(string dbLink,string value) {
			EFormField eFormField=new EFormField();
			eFormField.FieldType=EnumEFormFieldType.TextField;
			eFormField.DbLink=dbLink;
			eFormField.ValueString=value;
			eFormField.ValueLabel=dbLink;
			eFormField.AutoImport=true;
			return eFormField;
		}

		///<summary>Auto imports an eForm whose typed allergy and problem match no list entry (made-up names), and returns the patient.</summary>
		private static Patient ImportUnmatched(out string allergyTyped,out string problemTyped) {
			return ImportUnmatched(out allergyTyped,out problemTyped,out _);
		}

		private static Patient ImportUnmatched(out string allergyTyped,out string problemTyped,out List<string> listNotImported) {
			string suffix=NewSuffix();
			Patient patient=PatientT.CreatePatient(suffix,lName:"Odopts",fName:"EForm");
			allergyTyped="Synthetic allergen "+suffix;
			problemTyped="Synthetic condition "+suffix;
			EForm eForm=new EForm();
			eForm.PatNum=patient.PatNum;
			eForm.Description="Synthetic history";
			eForm.ListEFormFields=new List<EFormField> { Field("allergiesOther",allergyTyped),Field("problemsOther",problemTyped) };
			for(int i=0;i<eForm.ListEFormFields.Count;i++) {
				eForm.ListEFormFields[i].PatNum=patient.PatNum;
				eForm.ListEFormFields[i].ItemOrder=i;
			}
			EFormImport.ImportAutoFields(eForm,out listNotImported);
			return patient;
		}

		///<summary>The LogText of the patient's Audit Trail entries of one permission, oldest first.</summary>
		private static List<string> GetLogTexts(long patNum,EnumPermType permType) {
			return DataCore.GetTable("SELECT LogText FROM securitylog WHERE PatNum="+POut.Long(patNum)+" AND PermType="+POut.Int((int)permType)
				+" ORDER BY SecurityLogNum").Select().Select(x => x["LogText"].ToString()).ToList();
		}

		[TestMethod]
		public void EFormImport_Off_EachSkippedEntryIsInTheAuditTrail_ApartFromTheCount() {
			EFormImport.SetAllergiesAsOther(false);
			EFormImport.SetProblemsAsOther(false);
			Patient patient=ImportUnmatched(out string allergyTyped,out string problemTyped,out List<string> listNotImported);
			CollectionAssert.AreEqual(new[] { EFormImport.GetAllergyNotImportedText(allergyTyped),EFormImport.GetProblemNotImportedText(problemTyped) },
				listNotImported,"Returned for the user who saved the form.");
			Assert.AreEqual("Allergy not imported (matches no allergy in the list; EFormImportAllergiesAsOther off): "+allergyTyped,listNotImported[0]);
			Assert.AreEqual("Problem not imported (matches no problem in the list; EFormImportProblemsAsOther off): "+problemTyped,listNotImported[1]);
			List<string> listLogTexts=GetLogTexts(patient.PatNum,EnumPermType.EFormImport);
			Assert.AreEqual(1,listLogTexts.Count,"The auto import's entry is made although nothing was imported.");
			string logText=listLogTexts[0].Replace("\r\n","\n");
			StringAssert.StartsWith(logText,"Auto import from eForm #0 (Synthetic history): 0 change(s).\n","Not counted as changes.");
			StringAssert.Contains(logText,"\nAllergy not imported (matches no allergy in the list; EFormImportAllergiesAsOther off): "+allergyTyped);
			StringAssert.Contains(logText,"\nProblem not imported (matches no problem in the list; EFormImportProblemsAsOther off): "+problemTyped);
			Assert.AreEqual(0,GetLogTexts(patient.PatNum,EnumPermType.PatAllergyListEdit).Count,"Nothing was added to the allergy list.");
			Assert.AreEqual(0,GetLogTexts(patient.PatNum,EnumPermType.PatProblemListEdit).Count);
			//With changes, the skipped entry follows the count, which counts only the changes.
			EFormImport.SetProblemsAsOther(true);
			patient=ImportUnmatched(out allergyTyped,out problemTyped,out listNotImported);
			Assert.AreEqual(1,listNotImported.Count);
			logText=GetLogTexts(patient.PatNum,EnumPermType.EFormImport).Single().Replace("\r\n","\n");
			Assert.AreEqual("Auto import from eForm #0 (Synthetic history): 1 change(s).\n"+EFormImport.GetAllergyNotImportedText(allergyTyped),logText);
		}

		[TestMethod]
		public void EFormImport_On_NothingIsListedAsNotImported() {
			Patient patient=ImportUnmatched(out _,out _,out List<string> listNotImported);
			Assert.AreEqual(0,listNotImported.Count);
			string logText=GetLogTexts(patient.PatNum,EnumPermType.EFormImport).Single();
			Assert.AreEqual("Auto import from eForm #0 (Synthetic history): 2 change(s).",logText);
		}

		[TestMethod]
		public void EFormImport_On_UnmatchedEntriesBecomeOther() {
			Patient patient=ImportUnmatched(out string allergyTyped,out string problemTyped);
			List<Allergy> listAllergies=Allergies.GetAll(patient.PatNum,showInactive:false);
			Assert.AreEqual(1,listAllergies.Count);
			Assert.AreEqual(allergyTyped,listAllergies[0].Reaction,"The patient's text is kept.");
			Assert.AreEqual("Other",AllergyDefs.GetOne(listAllergies[0].AllergyDefNum).Description);
			List<Disease> listDiseases=Diseases.Refresh(patient.PatNum,showActiveOnly:true);
			Assert.AreEqual(1,listDiseases.Count);
			Assert.AreEqual(problemTyped,listDiseases[0].PatNote);
			Assert.AreEqual("Other",DiseaseDefs.GetName(listDiseases[0].DiseaseDefNum));
		}

		[TestMethod]
		public void EFormImport_Off_UnmatchedEntriesAreNotImported() {
			EFormImport.SetAllergiesAsOther(false);
			Patient patient=ImportUnmatched(out _,out _);
			Assert.AreEqual(0,Allergies.GetAll(patient.PatNum,showInactive:false).Count,"Allergies off: not imported.");
			Assert.AreEqual(1,Diseases.Refresh(patient.PatNum,showActiveOnly:true).Count,"Problems still on: imported as Other.");
			EFormImport.SetProblemsAsOther(false);
			patient=ImportUnmatched(out _,out _);
			Assert.AreEqual(0,Diseases.Refresh(patient.PatNum,showActiveOnly:true).Count,"Problems off: not imported.");
			//A typed allergy that matches the list is still imported.
			string suffix=NewSuffix();
			AllergyDef allergyDef=new AllergyDef { Description="Synthetic listed "+suffix };
			AllergyDefs.Insert(allergyDef);
			Patient patientListed=PatientT.CreatePatient(suffix,lName:"Odopts",fName:"Listed");
			EForm eForm=new EForm { PatNum=patientListed.PatNum,Description="Synthetic history",
				ListEFormFields=new List<EFormField> { Field("allergiesOther",allergyDef.Description) } };
			eForm.ListEFormFields[0].PatNum=patientListed.PatNum;
			EFormImport.ImportAutoFields(eForm);
			Assert.AreEqual(allergyDef.AllergyDefNum,Allergies.GetAll(patientListed.PatNum,showInactive:false).Single().AllergyDefNum);
		}
		#endregion EFormImportAllergiesAsOther and EFormImportProblemsAsOther

		#region InsHistPerioScalingCode
		///<summary>A row of a perio scaling code group (InsHistCategories.IsPerioScalingGroup): since the owner's decision of 2026-11-07 the seed's perio
		///quadrant preferences become a "Perio Scaling History" group with no fixed type and four quadrant rows (M2-HIST).</summary>
		private static bool IsPerioScalingRow(InsHistCategory insHistCategory) {
			return InsHistCategories.IsPerioScalingGroup(CodeGroups.GetFirstOrDefault(x => x.CodeGroupNum==insHistCategory.CodeGroupNum));
		}

		[TestMethod]
		public void InsHistPerioScalingCode_ThePerioScalingRowsCreateProceduresWithIt() {
			List<InsHistCategory> listRowsSrp=InsHistCategories.GetAll().FindAll(x => IsPerioScalingRow(x));
			Assert.IsTrue(listRowsSrp.Count>0,"The seed's perio quadrant preferences became a perio scaling code group (M2-HIST).");
			Assert.IsTrue(listRowsSrp.All(x => x.IsQuadrant),"Its four quadrant rows.");
			string histProcCodeOwn=listRowsSrp[0].HistProcCode;
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode("Z4396",treatmentArea:TreatmentArea.Quad);
			ProcedureCodes.RefreshCache();
			InsHistCategories.SetPerioScalingCodeSetting("z4396");
			Assert.IsTrue(InsHistCategories.GetAll().FindAll(x => IsPerioScalingRow(x)).All(x => x.HistProcCode=="Z4396"),
				"As the procedure code list spells it.");
			Assert.IsTrue(InsHistCategories.GetAll().FindAll(x => !IsPerioScalingRow(x)).All(x => x.HistProcCode!="Z4396"));
			//A date entered for a quadrant row makes an Existing Other procedure with that code.
			Patient patient=PatientT.CreatePatient(NewSuffix(),lName:"Odopts",fName:"History");
			InsPlan insPlan=InsPlanT.CreateInsPlan(CarrierT.CreateCarrier("Synthetic History Carrier "+NewSuffix()).CarrierNum);
			long insSubNum=InsSubT.CreateInsSub(patient.PatNum,insPlan.PlanNum).InsSubNum;
			InsHistCategory rowQuadrant=InsHistCategories.GetAll().First(x => IsPerioScalingRow(x));
			Assert.AreEqual(InsHistSaveResult.Inserted,InsHistCategories.InsertOrUpdateProcedure(patient,rowQuadrant,DateTime.Today.AddMonths(-2),
				insPlan.PlanNum,insSubNum,null,null,doSynchRecalls:false));
			Procedure procedure=Procedures.Refresh(patient.PatNum).Single();
			Assert.AreEqual(procedureCode.CodeNum,procedure.CodeNum);
			Assert.AreEqual(ProcStat.EO,procedure.ProcStatus);
			Assert.IsNotNull(InsHistCategories.GetMostRecentProc(Procedures.Refresh(patient.PatNum),rowQuadrant),"Read back as the row's history.");
			//The CodeNum of a code is read too; a value that names no code falls back to each group's own code.
			InsHistCategories.SetPerioScalingCodeSetting(POut.Long(procedureCode.CodeNum));
			Assert.AreEqual("Z4396",InsHistCategories.GetAll().First(x => IsPerioScalingRow(x)).HistProcCode);
			InsHistCategories.SetPerioScalingCodeSetting("Z0000");
			Assert.AreEqual(histProcCodeOwn,InsHistCategories.GetAll().First(x => IsPerioScalingRow(x)).HistProcCode);
		}
		#endregion InsHistPerioScalingCode

		#region StatementAutoSendEmail: the statements a billing run created
		[TestMethod]
		public void StatementAutoSend_TheCreatedStatementsAreTheNewUnsentOnes() {
			Patient patient=PatientT.CreatePatient(NewSuffix(),lName:"Odopts",fName:"Billing");
			List<long> listUnsentBefore=Statements.GetUnsentStatements();
			List<long> listEmailBefore=Statements.GetUnsentStatements(StatementMode.Email);
			List<Statement> listStatements=new List<Statement>();
			foreach(StatementMode mode in new[] { StatementMode.Email,StatementMode.Mail,StatementMode.Email }) {
				listStatements.Add(new Statement { PatNum=patient.PatNum,Mode_=mode,DateSent=DateTime.Today,DateRangeFrom=DateTime.Today.AddMonths(-1),
					DateRangeTo=DateTime.Today,Note="",NoteBold="",EmailSubject="",EmailBody="",SmsSendStatus=AutoCommStatus.DoNotSend,ShortGUID="",
					StatementURL="",StatementShortURL="" });
			}
			Statements.InsertMany(listStatements);//as billing does: the objects do not get their keys
			List<long> listCreated=StatementAutoSend.GetCreatedStatementNums(listUnsentBefore,Statements.GetUnsentStatements());
			List<long> listEmailCreated=StatementAutoSend.GetCreatedStatementNums(listEmailBefore,Statements.GetUnsentStatements(StatementMode.Email));
			Assert.AreEqual(3,listCreated.Count);
			Assert.AreEqual(2,listEmailCreated.Count);
			Assert.IsTrue(listEmailCreated.All(x => listCreated.Contains(x)));
			Assert.IsTrue(listEmailCreated.All(x => Statements.GetStatement(x).Mode_==StatementMode.Email));
		}
		#endregion StatementAutoSendEmail: the statements a billing run created

		#region DomainLoginForSigBoxes
		[TestMethod]
		public void DomainLoginForSigBoxes_FromThePreferences() {
			string enabledOld=ScratchDb.GetPrefRaw("DomainLoginEnabled");
			string pathOld=ScratchDb.GetPrefRaw("DomainLoginPath");
			try {
				ScratchDb.SetPrefRaw("DomainLoginEnabled","1");
				ScratchDb.SetPrefRaw("DomainLoginPath","LDAP://DC=example,DC=test");
				Assert.IsTrue(DomainLogin.IsOfferedInChangeUser(isSigBox:true),"No row: on.");
				DomainLogin.SetForSigBoxesSetting(false);
				Assert.IsFalse(DomainLogin.IsOfferedInChangeUser(isSigBox:true));
				Assert.IsTrue(DomainLogin.IsOfferedInChangeUser(isSigBox:false));
				ScratchDb.SetPrefRaw("DomainLoginEnabled","0");
				DomainLogin.SetForSigBoxesSetting(true);
				Assert.IsFalse(DomainLogin.IsOfferedInChangeUser(isSigBox:true),"Domain login off: never.");
			}
			finally {
				ScratchDb.SetPrefRaw("DomainLoginEnabled",enabledOld??"0");
				ScratchDb.SetPrefRaw("DomainLoginPath",pathOld??"");
			}
		}
		#endregion DomainLoginForSigBoxes
	}
}
