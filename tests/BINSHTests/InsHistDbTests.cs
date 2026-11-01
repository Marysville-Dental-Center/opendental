// Two Insurance History dates per row (Open Dental 25.1) and editing or removing them (25.2) against a scratch database: the procedures and
// claimprocs that saving creates, moves and deletes, completed procedures left alone, the security log, recall due dates and the note for a date
// that is saved but not shown. From Open Dental 26.1 (M2-HIST) the rows are code groups marked Show In History, so each test sets up its own
// groups instead of the InsHist*Codes preferences. Synthetic data only (made-up Z codes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace BINSHTests {
	[TestClass]
	public class InsHistDbTests {
		private const string CODE_PROPHY="Z1110";
		private const string CODE_PROPHY_ALT="Z1120";
		private const string CODE_SRP="Z4341";
		///<summary>Ends the name of every code group these tests add, so each test starts without them.</summary>
		private const string GROUP_SUFFIX=" BINSH";
		///<summary>The Prophy row: Z1110 and Z1120, History Proc Code Z1110.</summary>
		private static InsHistCategory _categoryProphy;
		///<summary>The UR and UL rows of a Perio Four Quadrants group with Z4341.</summary>
		private static InsHistCategory _categorySrpUR;
		private static InsHistCategory _categorySrpUL;

		[TestInitialize]
		public void TestInitialize() {
			BinshDb.Begin();
			ProcedureCodeT.CreateProcCode(CODE_PROPHY);
			ProcedureCodeT.CreateProcCode(CODE_PROPHY_ALT);
			ProcedureCodeT.CreateProcCode(CODE_SRP);
			ProcedureCodes.RefreshCache();
			//Only this test's rows: the fork migration marked the seed's groups Show In History.
			DataCore.NonQ("UPDATE codegroup SET ShowInHistory=0,HistProcCode='',IsPerioFourQuads=0");
			DataCore.NonQ("DELETE FROM codegroup WHERE GroupName LIKE '%"+GROUP_SUFFIX+"'");
			long codeGroupNumProphy=AddHistoryGroup("Prophy",CODE_PROPHY+","+CODE_PROPHY_ALT,CODE_PROPHY);
			long codeGroupNumSrp=AddHistoryGroup("SRP",CODE_SRP,CODE_SRP,isPerioFourQuads:true);
			CodeGroups.RefreshCache();
			_categoryProphy=GetCategory(codeGroupNumProphy,"");
			_categorySrpUR=GetCategory(codeGroupNumSrp,"UR");
			_categorySrpUL=GetCategory(codeGroupNumSrp,"UL");
		}

		///<summary>A code group marked Show In History, named name plus GROUP_SUFFIX. Returns its CodeGroupNum. The caller refreshes the cache.</summary>
		private static long AddHistoryGroup(string name,string procCodes,string histProcCode,bool isPerioFourQuads=false) {
			CodeGroup codeGroup=new CodeGroup { GroupName=name+GROUP_SUFFIX,ProcCodes=procCodes,ItemOrder=900,ShowInHistory=true,HistProcCode=histProcCode,
				IsPerioFourQuads=isPerioFourQuads };
			return CodeGroups.Insert(codeGroup);
		}

		private static InsHistCategory GetCategory(long codeGroupNum,string quadrant) {
			return InsHistCategories.GetAll().Single(x => x.CodeGroupNum==codeGroupNum && x.Quadrant==quadrant);
		}

		///<summary>A new patient with a primary plan. Returns the patient; insSub gets the subscriber row.</summary>
		private static Patient CreatePatientWithPlan(out InsSub insSub) {
			Patient patient=PatientT.CreatePatient("binsh"+Guid.NewGuid().ToString("N").Substring(0,6),priProvNum:ProviderT.GetFirstProvNum());
			Carrier carrier=CarrierT.CreateCarrier("BINSH Carrier "+patient.PatNum);
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum,feeSched:0);
			insSub=InsSubT.CreateInsSub(patient.PatNum,insPlan.PlanNum);
			PatPlanT.CreatePatPlan(1,patient.PatNum,insSub.InsSubNum);
			return patient;
		}

		private static InsHistAction Insert(InsHistCategory insHistCategory,DateTime date) {
			return new InsHistAction { InsHistCategory=insHistCategory,ActionType=InsHistActionType.Insert,DateNew=date };
		}

		private static List<Procedure> GetShown(Patient patient,InsSub insSub,InsHistCategory insHistCategory) {
			List<Procedure> listProcedures=InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> _);
			return InsHistDates.GetMostRecentProcs(listProcedures,insHistCategory);
		}

		///<summary>A completed procedure with a received claimproc on the plan, as a paid claim leaves it.</summary>
		private static Procedure CreateCompletedWithReceivedClaimProc(Patient patient,InsSub insSub,string procCode,DateTime date,string surf="") {
			Procedure procedure=ProcedureT.CreateProcedure(patient,procCode,ProcStat.C,"",60,date,surf:surf);
			ClaimProc claimProc=new ClaimProc();
			claimProc.PatNum=patient.PatNum;
			claimProc.ProcNum=procedure.ProcNum;
			claimProc.PlanNum=insSub.PlanNum;
			claimProc.InsSubNum=insSub.InsSubNum;
			claimProc.Status=ClaimProcStatus.Received;
			claimProc.ProcDate=date;
			claimProc.ProvNum=procedure.ProvNum;
			ClaimProcs.Insert(claimProc);
			return procedure;
		}

		private static List<string> GetLogTexts(long patNum,EnumPermType enumPermType) {
			DataTable table=DataCore.GetTable("SELECT LogText FROM securitylog WHERE PatNum="+POut.Long(patNum)+" AND PermType="+POut.Int((int)enumPermType));
			return table.Select().Select(x => x["LogText"].ToString()).ToList();
		}

		[TestMethod]
		public void Insert_TwoDatesMakeTwoExistingOtherProceduresWithInsHistClaimProcs() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			List<string> listMessages=InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> {
				Insert(_categoryProphy,new DateTime(2025,9,15)),
				Insert(_categoryProphy,new DateTime(2026,3,1)),
			},new List<ClaimProc>());
			Assert.AreEqual(0,listMessages.Count,string.Join("; ",listMessages));
			List<Procedure> listProcedures=InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> listClaimProcs);
			Assert.AreEqual(2,listProcedures.Count);
			Assert.IsTrue(listProcedures.All(x => x.ProcStatus==ProcStat.EO));
			Assert.IsTrue(listProcedures.All(x => ProcedureCodes.GetStringProcCode(x.CodeNum)==CODE_PROPHY),"The row's History Proc Code is used.");
			Assert.AreEqual(2,listClaimProcs.Count);
			Assert.IsTrue(listClaimProcs.All(x => x.Status==ClaimProcStatus.InsHist && x.PlanNum==insSub.PlanNum));
			List<Procedure> listShown=GetShown(patient,insSub,_categoryProphy);
			CollectionAssert.AreEqual(new[] { new DateTime(2026,3,1),new DateTime(2025,9,15) },listShown.Select(x => x.ProcDate).ToArray());
		}

		[TestMethod]
		public void ChangeDate_MovesTheExistingOtherProcedureAndItsClaimProcAndLogs() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> { Insert(_categoryProphy,new DateTime(2025,9,15)) },
				new List<ClaimProc>());
			List<Procedure> listProcedures=InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> listClaimProcs);
			Procedure procedure=listProcedures.Single();
			InsHistAction insHistAction=new InsHistAction { InsHistCategory=_categoryProphy,ActionType=InsHistActionType.ChangeDate,
				Procedure=procedure,DateNew=new DateTime(2025,10,2) };
			List<string> listMessages=InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> { insHistAction },listClaimProcs);
			Assert.AreEqual(0,listMessages.Count);
			Procedure procedureAfter=Procedures.GetOneProc(procedure.ProcNum,false);
			Assert.AreEqual(new DateTime(2025,10,2),procedureAfter.ProcDate);
			Assert.AreEqual(ProcStat.EO,procedureAfter.ProcStatus);
			InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out listClaimProcs);
			Assert.AreEqual(new DateTime(2025,10,2),listClaimProcs.Single().ProcDate,"The InsHist claimproc moves with the procedure.");
			List<string> listLogTexts=GetLogTexts(patient.PatNum,EnumPermType.ProcExistingEdit);
			Assert.AreEqual(1,listLogTexts.Count);
			StringAssert.Contains(listLogTexts[0],"Insurance History, Prophy");
			StringAssert.Contains(listLogTexts[0],"9/15/2025");
			StringAssert.Contains(listLogTexts[0],"10/2/2025");
		}

		[TestMethod]
		public void Remove_DeletesOnlyTheExistingOtherProcedureAndLogs() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			Procedure procedureC=CreateCompletedWithReceivedClaimProc(patient,insSub,CODE_PROPHY_ALT,new DateTime(2026,3,1));
			InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> { Insert(_categoryProphy,new DateTime(2025,9,15)) },
				new List<ClaimProc>());
			List<Procedure> listShown=GetShown(patient,insSub,_categoryProphy);
			Assert.AreEqual(2,listShown.Count);
			Assert.AreEqual(procedureC.ProcNum,listShown[0].ProcNum,"The completed procedure is the most recent date.");
			List<InsHistBox> listInsHistBoxes=new List<InsHistBox> {
				new InsHistBox { ProcedureLoaded=listShown[0] },//Read-only: what the box shows does not matter.
				new InsHistBox { ProcedureLoaded=listShown[1],DateEntered=DateTime.MinValue },//Removed.
			};
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,listInsHistBoxes,out string error);
			Assert.AreEqual("",error);
			InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> listClaimProcs);
			List<string> listMessages=InsHistDates.ApplyActions(patient,insSub,listActions,listClaimProcs);
			Assert.AreEqual(0,listMessages.Count,string.Join("; ",listMessages));
			Assert.AreEqual(ProcStat.D,Procedures.GetOneProc(listShown[1].ProcNum,false).ProcStatus);
			Assert.AreEqual(ProcStat.C,Procedures.GetOneProc(procedureC.ProcNum,false).ProcStatus,"Completed procedures are never touched.");
			listShown=GetShown(patient,insSub,_categoryProphy);
			Assert.AreEqual(1,listShown.Count);
			Assert.AreEqual(procedureC.ProcNum,listShown[0].ProcNum);
			Assert.AreEqual(1,ClaimProcs.GetForProcs(new List<long> { procedureC.ProcNum }).Count,"The received claimproc stays.");
			List<string> listLogTexts=GetLogTexts(patient.PatNum,EnumPermType.ProcDelete);
			Assert.AreEqual(1,listLogTexts.Count);
			StringAssert.Contains(listLogTexts[0],"Insurance History, Prophy");
			StringAssert.Contains(listLogTexts[0],"9/15/2025");
		}

		[TestMethod]
		public void Apply_CompletedProcedureIsNeverChangedOrDeleted() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			Procedure procedureC=CreateCompletedWithReceivedClaimProc(patient,insSub,CODE_PROPHY,new DateTime(2026,2,1));
			InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> listClaimProcs);
			InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> {
				new InsHistAction { InsHistCategory=_categoryProphy,ActionType=InsHistActionType.ChangeDate,Procedure=procedureC,DateNew=new DateTime(2026,2,9) },
				new InsHistAction { InsHistCategory=_categoryProphy,ActionType=InsHistActionType.Remove,Procedure=procedureC },
			},listClaimProcs);
			Procedure procedureAfter=Procedures.GetOneProc(procedureC.ProcNum,false);
			Assert.AreEqual(ProcStat.C,procedureAfter.ProcStatus);
			Assert.AreEqual(new DateTime(2026,2,1),procedureAfter.ProcDate);
		}

		[TestMethod]
		public void PerioQuadrant_NewDateGetsTheQuadrantSurface() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> {
				Insert(_categorySrpUR,new DateTime(2026,1,10)),
				Insert(_categorySrpUR,new DateTime(2025,1,10)),
			},new List<ClaimProc>());
			List<Procedure> listShown=GetShown(patient,insSub,_categorySrpUR);
			Assert.AreEqual(2,listShown.Count);
			Assert.IsTrue(listShown.All(x => x.Surf=="UR"));
			Assert.AreEqual(0,GetShown(patient,insSub,_categorySrpUL).Count);
		}

		[TestMethod]
		public void Insert_CategoryWithoutValidCodeIsReported() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			//Code Group Edit refuses such a code (CodeGroupHistoryOptions.GetError); a code deleted later, or a migrated preference whose first code was
			//never in the list, leaves one.
			long codeGroupNumExam=AddHistoryGroup("Exam","ZNOTACODE","ZNOTACODE");
			CodeGroups.RefreshCache();
			InsHistCategory insHistCategoryExam=GetCategory(codeGroupNumExam,"");
			List<string> listMessages=InsHistDates.ApplyActions(patient,insSub,
				new List<InsHistAction> { Insert(insHistCategoryExam,new DateTime(2026,1,1)) },new List<ClaimProc>());
			Assert.AreEqual(1,listMessages.Count);
			StringAssert.Contains(listMessages[0],"Exam");
			StringAssert.Contains(listMessages[0],"History Proc Code");
			Assert.AreEqual(0,InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> _).Count);
		}

		[TestMethod]
		public void GetNotesForHiddenDates_DateOlderThanTwoShownDates() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			CreateCompletedWithReceivedClaimProc(patient,insSub,CODE_PROPHY,new DateTime(2026,3,1));
			CreateCompletedWithReceivedClaimProc(patient,insSub,CODE_PROPHY,new DateTime(2025,9,1));
			List<InsHistAction> listActions=new List<InsHistAction> { Insert(_categoryProphy,new DateTime(2024,6,1)) };
			InsHistDates.ApplyActions(patient,insSub,listActions,new List<ClaimProc>());
			List<Procedure> listProcedures=InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> _);
			Assert.AreEqual(3,listProcedures.Count,"The date is saved.");
			List<string> listNotes=InsHistDates.GetNotesForHiddenDates(listActions,listProcedures);
			Assert.AreEqual(1,listNotes.Count);
			StringAssert.Contains(listNotes[0],"6/1/2024");
			listActions=new List<InsHistAction> { Insert(_categoryProphy,new DateTime(2026,4,1)) };
			InsHistDates.ApplyActions(patient,insSub,listActions,new List<ClaimProc>());
			listProcedures=InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> _);
			Assert.AreEqual(0,InsHistDates.GetNotesForHiddenDates(listActions,listProcedures).Count,"A newer date is shown.");
		}

		[TestMethod]
		public void GetProcsForInsSub_OnlyThisSubscribersHistory() {
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			Carrier carrierOther=CarrierT.CreateCarrier("BINSH Other "+patient.PatNum);
			InsPlan insPlanOther=InsPlanT.CreateInsPlan(carrierOther.CarrierNum,feeSched:0);
			InsSub insSubOther=InsSubT.CreateInsSub(patient.PatNum,insPlanOther.PlanNum);
			PatPlanT.CreatePatPlan(2,patient.PatNum,insSubOther.InsSubNum);
			InsHistDates.ApplyActions(patient,insSubOther,new List<InsHistAction> { Insert(_categoryProphy,new DateTime(2026,1,1)) },
				new List<ClaimProc>());
			ProcedureT.CreateProcedure(patient,CODE_PROPHY,ProcStat.EO,"",0,new DateTime(2026,2,1));//Charted in the Chart: no InsHist claimproc.
			Assert.AreEqual(0,InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> _).Count);
			Assert.AreEqual(1,InsHistDates.GetProcsForInsSub(patient.PatNum,insSubOther.InsSubNum,out List<ClaimProc> _).Count);
		}

		[TestMethod]
		public void Recall_FollowsTheMostRecentDateAndRecalculatesAfterRemove() {
			RecallType recallType=RecallTypeT.CreateRecallType("BINSH recall "+Guid.NewGuid().ToString("N").Substring(0,6),CODE_PROPHY);
			RecallTriggers.RefreshCache();
			try {
				Patient patient=CreatePatientWithPlan(out InsSub insSub);
				InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> {
					Insert(_categoryProphy,new DateTime(2025,3,1)),
					Insert(_categoryProphy,new DateTime(2025,9,1)),
				},new List<ClaimProc>());
				Recall recall=Recalls.GetList(patient.PatNum).Single(x => x.RecallTypeNum==recallType.RecallTypeNum);
				Assert.AreEqual(new DateTime(2025,9,1),recall.DatePrevious,"Recall follows the most recent date.");
				List<Procedure> listShown=GetShown(patient,insSub,_categoryProphy);
				InsHistDates.GetProcsForInsSub(patient.PatNum,insSub.InsSubNum,out List<ClaimProc> listClaimProcs);
				InsHistDates.ApplyActions(patient,insSub,new List<InsHistAction> {
					new InsHistAction { InsHistCategory=_categoryProphy,ActionType=InsHistActionType.Remove,Procedure=listShown[0] },
				},listClaimProcs);
				recall=Recalls.GetList(patient.PatNum).Single(x => x.RecallTypeNum==recallType.RecallTypeNum);
				Assert.AreEqual(new DateTime(2025,3,1),recall.DatePrevious,"Removing the most recent date recalculates the recall.");
			}
			finally {
				//Without a trigger the recall type is inactive, so the other tests' patients get no recalls from it.
				DataCore.NonQ("DELETE FROM recalltrigger WHERE RecallTypeNum="+POut.Long(recallType.RecallTypeNum));
				RecallTriggers.RefreshCache();
			}
		}
	}
}
