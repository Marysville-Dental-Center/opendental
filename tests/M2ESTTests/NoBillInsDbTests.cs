// The per-procedure Do Not Bill to Ins decision (procedurelog.NoBillIns, Open Dental 25.4) on a scratch database: estimates inherit it when they
// are created or rebuilt, it is audited, a claimproc set to bill clears it, and the 24.3/25.3 back-fill. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;
using UnitTestsCore;

namespace M2ESTTests {
	[TestClass]
	public class NoBillInsDbTests {
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			EstDb.Begin();
		}

		private static InsuranceInfo NewPatientWithInsurance(string procCode,out Patient patient,bool isCodeNoBillIns=false) {
			patient=PatientT.CreatePatient("NoBill"+(++_counter),lName:"M2est",fName:"Synthetic");
			InsuranceInfo insuranceInfo=InsuranceT.AddInsurance(patient,"M2-EST carrier");
			ProcedureCode procedureCode=ProcedureCodeT.CreateProcCode(procCode,noBillIns:isCodeNoBillIns);
			BenefitT.CreatePercentForProc(insuranceInfo.PriInsPlan.PlanNum,procedureCode.CodeNum,100);
			return insuranceInfo;
		}

		private static List<SecurityLog> GetLogs(long patNum) {
			return SecurityLogs.GetMany(new SQLWhere[] { SQLWhere.Create(nameof(SecurityLog.PatNum),ComparisonOperator.Equals,patNum) })
				.Where(x => x.LogText.Contains("Do Not Bill to Ins")).ToList();
		}

		[TestMethod]
		public void NewEstimates_InheritTheProcedureFlag() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7601",out Patient patient);
			Procedure procFlagged=ProcedureT.CreateProcedure(patient,"Z7601",ProcStat.TP,"",100,doInsert:false);
			procFlagged.NoBillIns=true;
			Procedures.Insert(procFlagged);
			Procedure procNormal=ProcedureT.CreateProcedure(patient,"Z7601",ProcStat.TP,"",100);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			ClaimProc claimProcFlagged=listClaimProcs.Single(x => x.ProcNum==procFlagged.ProcNum);
			Assert.IsTrue(claimProcFlagged.NoBillIns,"The code and plan would bill; the procedure's decision wins.");
			Assert.AreEqual(0,ClaimProcs.GetInsEstTotal(claimProcFlagged));
			Assert.IsFalse(listClaimProcs.Single(x => x.ProcNum==procNormal.ProcNum).NoBillIns);
			Assert.IsTrue(Procedures.GetNoBillInsForNewEstimate(procFlagged,insuranceInfo.PriInsPlan));
			Assert.IsFalse(Procedures.GetNoBillInsForNewEstimate(procNormal,insuranceInfo.PriInsPlan));
		}

		[TestMethod]
		public void RebuiltEstimates_KeepTheDecision() {
			//The 24.3 problem: estimates rebuilt (for example after an insurance plan change) lost the box. Now they take it from the procedure.
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7602",out Patient patient);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7602",ProcStat.TP,"",100);
			ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Procedures.SetNoBillInsFromUser(procedure,true,false);
			ClaimProcs.DeleteMany(ClaimProcs.RefreshForProc(procedure.ProcNum));
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.IsTrue(listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum).NoBillIns);
			//ClaimProcs.CreateEst, used for claims, Add Estimate and preauthorizations, inherits it too.
			ClaimProc claimProc=new ClaimProc();
			ClaimProcs.CreateEst(claimProc,Procedures.GetOneProc(procedure.ProcNum,false),insuranceInfo.PriInsPlan,insuranceInfo.PriInsSub,
				isInsertNeeded:false);
			Assert.IsTrue(claimProc.NoBillIns);
		}

		[TestMethod]
		public void CodeDefaultStillApplies_WithoutTheFlag() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7603",out Patient patient,isCodeNoBillIns:true);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7603",ProcStat.TP,"",100);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			Assert.IsTrue(listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum).NoBillIns);
			Assert.IsFalse(Procedures.GetOneProc(procedure.ProcNum,false).NoBillIns,"The procedure flag is only the user's decision.");
		}

		[TestMethod]
		public void UserDecision_IsSavedAndAudited() {
			NewPatientWithInsurance("Z7604",out Patient patient);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7604",ProcStat.TP,"",100,procDate:new DateTime(2027,3,15));
			Procedures.SetNoBillInsFromUser(procedure,true,false);
			Assert.IsTrue(procedure.NoBillIns);
			Assert.IsTrue(Procedures.GetOneProc(procedure.ProcNum,false).NoBillIns);
			List<SecurityLog> listLogs=GetLogs(patient.PatNum);
			Assert.AreEqual(1,listLogs.Count);
			Assert.AreEqual(EnumPermType.ProcEdit,listLogs[0].PermType);
			Assert.AreEqual(procedure.ProcNum,listLogs[0].FKey);
			Assert.AreEqual("Z7604 3/15/2027: Do Not Bill to Ins changed from unchecked to checked",listLogs[0].LogText);
			//The box did not change: no entry. From mixed to unchecked: an entry, and the flag is cleared.
			Procedures.SetNoBillInsFromUser(procedure,true,true);
			Assert.AreEqual(1,GetLogs(patient.PatNum).Count);
			Procedures.SetNoBillInsFromUser(procedure,false,null);
			Assert.IsFalse(Procedures.GetOneProc(procedure.ProcNum,false).NoBillIns);
			Assert.AreEqual(2,GetLogs(patient.PatNum).Count);
			StringAssert.Contains(GetLogs(patient.PatNum).OrderBy(x => x.SecurityLogNum).Last().LogText,"changed from mixed to unchecked");
		}

		[TestMethod]
		public void UserDecision_OnACompletedProcedure_IsLoggedUnderCompletedEdit() {
			NewPatientWithInsurance("Z7605",out Patient patient);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7605",ProcStat.C,"",100);
			Procedures.SetNoBillInsFromUser(procedure,true,false);
			Assert.AreEqual(EnumPermType.ProcCompleteEditMisc,GetLogs(patient.PatNum).Single().PermType);
		}

		[TestMethod]
		public void ClaimProcSetToBill_ClearsTheProcedureFlag() {
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7606",out Patient patient);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7606",ProcStat.TP,"",100);
			Procedures.SetNoBillInsFromUser(procedure,true,false);
			List<ClaimProc> listClaimProcs=ProcedureT.ComputeEstimates(patient,insuranceInfo);
			ClaimProc claimProcOld=listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum);
			Assert.IsTrue(claimProcOld.NoBillIns);
			ClaimProc claimProcNew=claimProcOld.Copy();
			claimProcNew.NoBillIns=false;
			Assert.IsFalse(Procedures.ClearNoBillInsForBilledClaimProc(procedure,claimProcNew,claimProcNew),"Nothing changed.");
			Assert.IsTrue(Procedures.ClearNoBillInsForBilledClaimProc(procedure,claimProcOld,claimProcNew));
			Assert.IsFalse(procedure.NoBillIns);
			Assert.IsFalse(Procedures.GetOneProc(procedure.ProcNum,false).NoBillIns);
			SecurityLog securityLog=GetLogs(patient.PatNum).OrderBy(x => x.SecurityLogNum).Last();
			StringAssert.Contains(securityLog.LogText,"changed from checked to unchecked "+Procedures.NO_BILL_INS_CLEARED_SUFFIX);
			Assert.IsFalse(Procedures.ClearNoBillInsForBilledClaimProc(procedure,claimProcOld,claimProcNew),"Already cleared.");
			Assert.IsFalse(Procedures.ClearNoBillInsForBilledClaimProc(null,claimProcOld,claimProcNew));
		}

		[TestMethod]
		public void TreatmentPlanGrid_FlaggedProcedure_HasNoAllowedFee() {
			//The plan's estimates are created as the grid loads and inherit the decision; the Allowed column shows -1 (not billed).
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7607",out Patient patient);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z7607",ProcStat.TP,"",100,doInsert:false);
			procedure.NoBillIns=true;
			Procedures.Insert(procedure);
			Prefs.UpdateBool(PrefName.InsChecksFrequency,true);
			TPModuleData tpModuleData=TreatmentPlanModules.GetModuleData(patient.PatNum,false);
			TreatPlan treatPlan=tpModuleData.ListTreatPlans.First(x => x.TPStatus==TreatPlanStatus.Active);
			LoadActiveTPData loadActiveTPData=TreatmentPlanModules.GetLoadActiveTpData(tpModuleData.Pat,treatPlan.TreatPlanNum,tpModuleData.BenefitList,
				tpModuleData.PatPlanList,tpModuleData.InsPlanList,DateTime.Today,tpModuleData.SubList,true,false,tpModuleData.ListSubstLinks);
			List<ClaimProc> listClaimProcs=loadActiveTPData.ClaimProcList;
			List<TpRow> listTpRows=TreatmentPlanModules.GetActiveTpPlanTpRows(true,false,false,false,treatPlan,tpModuleData.Pat,DateTime.Today,loadActiveTPData,
				tpModuleData.InsPlanList,tpModuleData.BenefitList,tpModuleData.PatPlanList,tpModuleData.ListSubstLinks,tpModuleData.SubList,
				tpModuleData.DiscountPlanSub,tpModuleData.DiscountPlan,tpModuleData.ListProcedures,ref listClaimProcs,loadActiveTPData.HistList);
			Assert.AreEqual(-1,(double)listTpRows.Single(x => x.RowType==TpRowType.TpRow).FeeAllowed,"-1 means Do Not Bill Insurance.");
			Assert.IsTrue(listClaimProcs.Single(x => x.ProcNum==procedure.ProcNum).NoBillIns);
		}

		#region Back-fill of 24.3/25.3 decisions
		private static void SetClaimProcNoBillIns(long procNum,bool isNoBillIns) {
			DataCore.NonQ("UPDATE claimproc SET NoBillIns="+POut.Bool(isNoBillIns)+" WHERE ProcNum="+POut.Long(procNum));
		}

		[TestMethod]
		public void Backfill_FlagsProceduresWhoseEstimatesTheUserMarked() {
			//Start as a database straight from 24.3/25.3: no procedure flagged yet (other tests here flag some).
			DataCore.NonQ("UPDATE procedurelog SET NoBillIns=0");
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7611",out Patient patient);
			ProcedureCode procedureCodeDefault=ProcedureCodeT.CreateProcCode("Z7612",noBillIns:true);
			BenefitT.CreatePercentForProc(insuranceInfo.PriInsPlan.PlanNum,procedureCodeDefault.CodeNum,100);
			ProcedureCode procedureCodeOverride=ProcedureCodeT.CreateProcCode("Z7613");
			InsPlanPreferences.Insert(new InsPlanPreference { PlanNum=insuranceInfo.PriInsPlan.PlanNum,FKey=procedureCodeOverride.CodeNum,
				FKeyType=InsPlanPrefFKeyType.ProcCodeNoBillIns,ValueString=((int)NoBillInsOverride.DoNotUsuallyBillToIns).ToString() });
			Procedure procMarked=ProcedureT.CreateProcedure(patient,"Z7611",ProcStat.TP,"",100);
			Procedure procMarkedComplete=ProcedureT.CreateProcedure(patient,"Z7611",ProcStat.C,"",100);
			Procedure procNotMarked=ProcedureT.CreateProcedure(patient,"Z7611",ProcStat.TP,"",100);
			Procedure procCodeDefault=ProcedureT.CreateProcedure(patient,"Z7612",ProcStat.TP,"",100);
			Procedure procPlanOverride=ProcedureT.CreateProcedure(patient,"Z7613",ProcStat.TP,"",100);
			ProcedureT.ComputeEstimates(patient,insuranceInfo);//treatment-planned procedures only: the completed one gets its estimate by hand
			ClaimProc claimProcComplete=new ClaimProc();
			ClaimProcs.CreateEst(claimProcComplete,procMarkedComplete,insuranceInfo.PriInsPlan,insuranceInfo.PriInsSub);
			SetClaimProcNoBillIns(procMarked.ProcNum,true);
			SetClaimProcNoBillIns(procMarkedComplete.ProcNum,true);
			Assert.IsTrue(ClaimProcs.RefreshForProc(procCodeDefault.ProcNum).Single().NoBillIns);
			Assert.IsTrue(ClaimProcs.RefreshForProc(procPlanOverride.ProcNum).Single().NoBillIns);
			long countFlagged=NoBillInsBackfillStep.Run();
			Assert.IsTrue(countFlagged>=2,"These two, and possibly procedures of other tests in this database.");
			Assert.IsTrue(Procedures.GetOneProc(procMarked.ProcNum,false).NoBillIns);
			Assert.IsTrue(Procedures.GetOneProc(procMarkedComplete.ProcNum,false).NoBillIns,"Complete and not billed.");
			Assert.IsFalse(Procedures.GetOneProc(procNotMarked.ProcNum,false).NoBillIns);
			Assert.IsFalse(Procedures.GetOneProc(procCodeDefault.ProcNum,false).NoBillIns,"The code's default, not a decision.");
			Assert.IsFalse(Procedures.GetOneProc(procPlanOverride.ProcNum,false).NoBillIns,"The plan's default, not a decision.");
			Assert.AreEqual(0,NoBillInsBackfillStep.Run(),"Once any procedure is flagged, the back-fill never runs again.");
		}

		[TestMethod]
		public void BackfillStep_RunsOnlyForDatabasesFromBefore25_4() {
			DataCore.NonQ("UPDATE procedurelog SET NoBillIns=0");
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7621",out Patient patient);
			Procedure procMarked=ProcedureT.CreateProcedure(patient,"Z7621",ProcStat.TP,"",100);
			ProcedureT.ComputeEstimates(patient,insuranceInfo);
			SetClaimProcNoBillIns(procMarked.ProcNum,true);
			string dataBaseVersion=DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'");
			try {
				//This scratch database comes from 24.3 (DataBaseVersion 24.3.63, not adopted).
				ForkMigrationContext context=new ForkMigrationContext(EstDb.DbName);
				new StepUnderTest().Apply(context);
				Assert.IsTrue(Procedures.GetOneProc(procMarked.ProcNum,false).NoBillIns);
				Assert.IsTrue(context.ListChanges.Any(x => x.Contains("UPDATE procedurelog SET NoBillIns=1")));
				//A database that is already 25.4 or later is never back-filled.
				DataCore.NonQ("UPDATE procedurelog SET NoBillIns=0");
				DataCore.NonQ("UPDATE preference SET ValueString='25.4.10.0' WHERE PrefName='DataBaseVersion'");
				context=new ForkMigrationContext(EstDb.DbName);
				new StepUnderTest().Apply(context);
				Assert.IsFalse(Procedures.GetOneProc(procMarked.ProcNum,false).NoBillIns);
				Assert.AreEqual(0,context.ListChanges.Count);
			}
			finally {
				DataCore.NonQ("UPDATE preference SET ValueString='"+POut.String(dataBaseVersion)+"' WHERE PrefName='DataBaseVersion'");
			}
		}

		private static string GetDateTStamp(long procNum) {
			return DataCore.GetScalar("SELECT DATE_FORMAT(DateTStamp,'%Y-%m-%d %H:%i:%s') FROM procedurelog WHERE ProcNum="+POut.Long(procNum));
		}

		///<summary>Combined review (2026-11-01): a back-fill is not an edit of the row (ForkMigrations README, rule 4), so the UPDATE keeps
		///procedurelog's ON UPDATE timestamp DateTStamp; the adoption rehearsal fails on any audit timestamp that moves. Both ways the step runs:
		///Run() on its own and Apply through the migration context.</summary>
		[TestMethod]
		public void Backfill_KeepsTheProcedureTimestamp() {
			DataCore.NonQ("UPDATE procedurelog SET NoBillIns=0");
			InsuranceInfo insuranceInfo=NewPatientWithInsurance("Z7631",out Patient patient);
			Procedure procMarked=ProcedureT.CreateProcedure(patient,"Z7631",ProcStat.TP,"",100);
			ProcedureT.ComputeEstimates(patient,insuranceInfo);
			SetClaimProcNoBillIns(procMarked.ProcNum,true);
			const string dateTStamp="2026-01-02 03:04:05";
			DataCore.NonQ("UPDATE procedurelog SET DateTStamp='"+dateTStamp+"' WHERE ProcNum="+POut.Long(procMarked.ProcNum));
			Assert.AreEqual(dateTStamp,GetDateTStamp(procMarked.ProcNum));
			Assert.IsTrue(NoBillInsBackfillStep.Run()>=1);
			Assert.IsTrue(Procedures.GetOneProc(procMarked.ProcNum,false).NoBillIns);
			Assert.AreEqual(dateTStamp,GetDateTStamp(procMarked.ProcNum),"The back-fill keeps DateTStamp.");
			//Through the migration context (the step's Apply).
			DataCore.NonQ("UPDATE procedurelog SET NoBillIns=0");
			DataCore.NonQ("UPDATE procedurelog SET DateTStamp='"+dateTStamp+"' WHERE ProcNum="+POut.Long(procMarked.ProcNum));
			ForkMigrationContext context=new ForkMigrationContext(EstDb.DbName);
			new StepUnderTest().Apply(context);
			Assert.IsTrue(Procedures.GetOneProc(procMarked.ProcNum,false).NoBillIns);
			Assert.AreEqual(dateTStamp,GetDateTStamp(procMarked.ProcNum),"Apply keeps DateTStamp too.");
			Assert.IsTrue(context.ListChanges.Any(x => x.Contains("UPDATE procedurelog SET NoBillIns=1,DateTStamp=DateTStamp WHERE ProcNum IN(")));
		}

		///<summary>The back-fill step is abstract until its level is settled (see the port note), so the test runs it through a subclass. The
		///subclass has no [ForkMigration] attribute and is not in OpenPmsDentalBusiness, so the runner never sees it.</summary>
		private class StepUnderTest:NoBillInsBackfillStep {
		}
		#endregion Back-fill of 24.3/25.3 decisions
	}
}
