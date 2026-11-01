// Insurance History from code groups (Open Dental 26.1) against the scratch database: entering and changing dates, quadrant rows, rows without a
// valid History Proc Code, eligibility (271) imports matched to code groups, and the codes frequency checks take from history. Synthetic data only
// (made-up Z codes); every test sets the history options of the code groups it uses.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2HISTTests {
	[TestClass]
	public class InsHistDbTests {
		private const string CODE_EXAM="Z0120";
		private const string CODE_EXAM_ALT="Z0150";
		private const string CODE_BW="Z0272";
		private const string CODE_BW_ALT="Z0274";
		private const string CODE_BW_HIST="Z0273";
		private const string CODE_BW_STALE="Z0279";
		private const string CODE_SRP="Z4341";
		private const string CODE_FLUORIDE="Z1206";
		private const string GROUP_PREFIX="M2HIST ";

		[TestInitialize]
		public void TestInitialize() {
			M2HistDb.Begin();
			foreach(string procCode in new[] { CODE_EXAM,CODE_EXAM_ALT,CODE_BW,CODE_BW_ALT,CODE_BW_HIST,CODE_BW_STALE,CODE_SRP,CODE_FLUORIDE }) {
				ProcedureCodeT.CreateProcCode(procCode);
			}
			ProcedureCodes.RefreshCache();
			//No history rows until a test sets them.
			DataCore.NonQ("UPDATE codegroup SET ShowInHistory=0,HistProcCode='',IsPerioFourQuads=0");
			DataCore.NonQ("DELETE FROM codegroup WHERE GroupName LIKE '"+GROUP_PREFIX+"%'");
			CodeGroups.RefreshCache();
		}

		///<summary>Sets the history options (and codes) of the code group with this fixed category, the one the frequency checks use.</summary>
		private static CodeGroup SetFixedGroup(EnumCodeGroupFixed codeGroupFixed,string procCodes,string histProcCode,bool isPerioFourQuads=false) {
			CodeGroup codeGroup=CodeGroups.GetOneForCodeGroupFixed(codeGroupFixed);
			Assert.IsNotNull(codeGroup,"The seed has a "+codeGroupFixed+" group.");
			codeGroup.ProcCodes=procCodes;
			codeGroup.ShowInHistory=true;
			codeGroup.HistProcCode=histProcCode;
			codeGroup.IsPerioFourQuads=isPerioFourQuads;
			CodeGroups.Update(codeGroup);
			CodeGroups.RefreshCache();
			return codeGroup;
		}

		///<summary>A new code group of the office's own, shown in history.</summary>
		private static CodeGroup AddGroup(string name,string procCodes,string histProcCode,bool isPerioFourQuads=false) {
			CodeGroup codeGroup=new CodeGroup { GroupName=GROUP_PREFIX+name,ProcCodes=procCodes,ItemOrder=900,ShowInHistory=true,HistProcCode=histProcCode,
				IsPerioFourQuads=isPerioFourQuads };
			CodeGroups.Insert(codeGroup);
			CodeGroups.RefreshCache();
			return codeGroup;
		}

		private static Patient CreatePatientWithPlan(out InsSub insSub) {
			Patient patient=PatientT.CreatePatient("m2hist"+Guid.NewGuid().ToString("N").Substring(0,6),priProvNum:ProviderT.GetFirstProvNum());
			Carrier carrier=CarrierT.CreateCarrier("M2HIST Carrier "+patient.PatNum);
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum,feeSched:0);
			insSub=InsSubT.CreateInsSub(patient.PatNum,insPlan.PlanNum);
			PatPlanT.CreatePatPlan(1,patient.PatNum,insSub.InsSubNum);
			return patient;
		}

		///<summary>The procedures the Insurance History window reads for the plan: EO and C with an InsHist or Received claimproc on the inssub.</summary>
		private static List<Procedure> GetHistoryProcs(Patient patient,InsSub insSub,out List<ClaimProc> listClaimProcs) {
			List<Procedure> listProcedures=Procedures.GetProcsByStatusForPat(patient.PatNum,ProcStat.EO,ProcStat.C);
			List<ClaimProc> listClaimProcsAll=ClaimProcs.GetForProcs(listProcedures.Select(x => x.ProcNum).ToList());
			listClaimProcs=listClaimProcsAll.FindAll(x => x.InsSubNum==insSub.InsSubNum && x.Status.In(ClaimProcStatus.InsHist,ClaimProcStatus.Received));
			List<long> listProcNums=listClaimProcs.Select(x => x.ProcNum).ToList();
			return listProcedures.FindAll(x => listProcNums.Contains(x.ProcNum));
		}

		private static InsHistCategory GetCategory(long codeGroupNum,string quadrant="") {
			return InsHistCategories.GetAll().Single(x => x.CodeGroupNum==codeGroupNum && x.Quadrant==quadrant);
		}

		[TestMethod]
		public void EnterDate_CreatesExistingOtherWithTheHistProcCodeAndAnInsHistClaimProc() {
			CodeGroup codeGroup=SetFixedGroup(EnumCodeGroupFixed.Exam,CODE_EXAM+","+CODE_EXAM_ALT,CODE_EXAM_ALT);
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			InsHistCategory category=GetCategory(codeGroup.CodeGroupNum);
			InsHistSaveResult result=InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2026,3,2),insSub.PlanNum,insSub.InsSubNum,
				null,new List<ClaimProc>());
			Assert.AreEqual(InsHistSaveResult.Inserted,result);
			List<Procedure> listProcedures=GetHistoryProcs(patient,insSub,out List<ClaimProc> listClaimProcs);
			Procedure procedure=listProcedures.Single();
			Assert.AreEqual(ProcStat.EO,procedure.ProcStatus);
			Assert.AreEqual(CODE_EXAM_ALT,ProcedureCodes.GetStringProcCode(procedure.CodeNum),"The group's History Proc Code, not its first code.");
			Assert.AreEqual(new DateTime(2026,3,2),procedure.ProcDate);
			Assert.AreEqual("",procedure.Surf);
			ClaimProc claimProc=listClaimProcs.Single();
			Assert.AreEqual(ClaimProcStatus.InsHist,claimProc.Status);
			Assert.AreEqual(insSub.PlanNum,claimProc.PlanNum);
			Assert.AreEqual(procedure.ProcNum,InsHistCategories.GetMostRecentProc(listProcedures,category).ProcNum,"The date reads back for the row.");
		}

		[TestMethod]
		public void ChangeDate_MovesTheExistingOther_CompletedOnlyTakesANewerDate() {
			CodeGroup codeGroup=SetFixedGroup(EnumCodeGroupFixed.Exam,CODE_EXAM,CODE_EXAM);
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			InsHistCategory category=GetCategory(codeGroup.CodeGroupNum);
			InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2025,9,15),insSub.PlanNum,insSub.InsSubNum,null,new List<ClaimProc>());
			List<Procedure> listProcedures=GetHistoryProcs(patient,insSub,out List<ClaimProc> listClaimProcs);
			Procedure procedureEO=InsHistCategories.GetMostRecentProc(listProcedures,category);
			Assert.AreEqual(InsHistSaveResult.Unchanged,InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2025,9,15),insSub.PlanNum,
				insSub.InsSubNum,procedureEO,listClaimProcs.FindAll(x => x.ProcNum==procedureEO.ProcNum)));
			Assert.AreEqual(InsHistSaveResult.Updated,InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2025,10,2),insSub.PlanNum,
				insSub.InsSubNum,procedureEO,listClaimProcs.FindAll(x => x.ProcNum==procedureEO.ProcNum)));
			listProcedures=GetHistoryProcs(patient,insSub,out listClaimProcs);
			Assert.AreEqual(1,listProcedures.Count,"Moved, not added.");
			Assert.AreEqual(new DateTime(2025,10,2),listProcedures[0].ProcDate);
			Assert.AreEqual(new DateTime(2025,10,2),listClaimProcs.Single().ProcDate);
			//A completed exam on a received claim: an older date changes nothing, a newer one adds an Existing Other procedure.
			Procedure procedureC=ProcedureT.CreateProcedure(patient,CODE_EXAM,ProcStat.C,"",60,new DateTime(2026,1,10));
			ClaimProcs.Insert(new ClaimProc { PatNum=patient.PatNum,ProcNum=procedureC.ProcNum,PlanNum=insSub.PlanNum,InsSubNum=insSub.InsSubNum,
				Status=ClaimProcStatus.Received,ProcDate=procedureC.ProcDate,ProvNum=procedureC.ProvNum });
			listProcedures=GetHistoryProcs(patient,insSub,out listClaimProcs);
			Procedure procedureMostRecent=InsHistCategories.GetMostRecentProc(listProcedures,category);
			Assert.AreEqual(procedureC.ProcNum,procedureMostRecent.ProcNum);
			Assert.AreEqual(InsHistSaveResult.Unchanged,InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2025,12,1),insSub.PlanNum,
				insSub.InsSubNum,procedureMostRecent,listClaimProcs.FindAll(x => x.ProcNum==procedureC.ProcNum)));
			Assert.AreEqual(InsHistSaveResult.Inserted,InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2026,2,20),insSub.PlanNum,
				insSub.InsSubNum,procedureMostRecent,listClaimProcs.FindAll(x => x.ProcNum==procedureC.ProcNum)));
			listProcedures=GetHistoryProcs(patient,insSub,out listClaimProcs);
			Assert.AreEqual(3,listProcedures.Count);
			Assert.AreEqual(ProcStat.C,Procedures.GetOneProc(procedureC.ProcNum,false).ProcStatus,"The completed procedure is left alone.");
			Assert.AreEqual(new DateTime(2026,2,20),InsHistCategories.GetMostRecentProc(listProcedures,category).ProcDate);
		}

		[TestMethod]
		public void QuadrantRows_EachSetsAndReadsItsOwnSurface() {
			CodeGroup codeGroup=SetFixedGroup(EnumCodeGroupFixed.SRP,CODE_SRP,CODE_SRP,isPerioFourQuads:true);
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			List<InsHistCategory> listCategories=InsHistCategories.GetAll().FindAll(x => x.CodeGroupNum==codeGroup.CodeGroupNum);
			CollectionAssert.AreEqual(new[] { "UL","UR","LL","LR" },listCategories.Select(x => x.Quadrant).ToArray());
			InsHistCategory categoryUR=listCategories[1];
			Assert.AreEqual(InsHistSaveResult.Inserted,InsHistCategories.InsertOrUpdateProcedure(patient,categoryUR,new DateTime(2026,4,7),insSub.PlanNum,
				insSub.InsSubNum,null,new List<ClaimProc>()));
			List<Procedure> listProcedures=GetHistoryProcs(patient,insSub,out List<ClaimProc> _);
			Assert.AreEqual("UR",listProcedures.Single().Surf);
			Assert.IsNotNull(InsHistCategories.GetMostRecentProc(listProcedures,categoryUR));
			Assert.IsNull(InsHistCategories.GetMostRecentProc(listProcedures,listCategories[0]),"UL has no history.");
		}

		[TestMethod]
		public void RowWithoutAValidHistProcCode_SavesNothing() {
			CodeGroup codeGroup=AddGroup("Broken",CODE_FLUORIDE,"Z9998");
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			InsHistCategory category=GetCategory(codeGroup.CodeGroupNum);
			Assert.IsFalse(InsHistCategories.HasValidHistProcCode(category));
			Assert.AreEqual(InsHistSaveResult.InvalidCode,InsHistCategories.InsertOrUpdateProcedure(patient,category,new DateTime(2026,1,5),insSub.PlanNum,
				insSub.InsSubNum,null,new List<ClaimProc>()));
			Assert.AreEqual(0,Procedures.GetProcsByStatusForPat(patient.PatNum,ProcStat.EO).Count);
		}

		[TestMethod]
		public void Eligibility_MatchesTheCategoryThenTheCode_SkipsAndLogsTheRest() {
			SetFixedGroup(EnumCodeGroupFixed.BW,CODE_BW+","+CODE_BW_ALT,CODE_BW);
			SetFixedGroup(EnumCodeGroupFixed.SRP,CODE_SRP,CODE_SRP,isPerioFourQuads:true);
			AddGroup("Fluoride",CODE_FLUORIDE,CODE_FLUORIDE);
			Patient patient=CreatePatientWithPlan(out InsSub insSub);
			List<EB271> listEb271s=new List<EB271> {
				EligibilityInsHistTests.MakeEb271("EB*F*IND*4","DTP*304*D8*20250918","MSG*BITEWING X-RAYS"),
				EligibilityInsHistTests.MakeEb271("EB*F*IND*4"+new string('*',10)+"AD:"+CODE_FLUORIDE,"DTP*304*D8*20250611","MSG*FLUORIDE"),
				EligibilityInsHistTests.MakeEb271("EB*F*IND*4","DTP*304*D8*20250301","MSG*SEALANTS"),
				EligibilityInsHistTests.MakeEb271("EB*F*IND*4"+new string('*',10)+"AD:"+CODE_SRP,"DTP*304*D8*20250402"),
				EligibilityInsHistTests.MakeEb271("EB*F*IND*4","DTP*304*D8*20250402","MSG*EXAMS"),
			};
			List<string> listSkipped=new List<string>();
			int count=EB271.SetInsuranceHistoryDates(listEb271s,patient.PatNum,insSub,listSkipped);
			Assert.AreEqual(2,count,string.Join("; ",listSkipped));
			Assert.AreEqual(3,listSkipped.Count,string.Join("; ",listSkipped));
			StringAssert.Contains(listSkipped[0],"SEALANTS");
			StringAssert.Contains(listSkipped[1],"quadrant");
			StringAssert.Contains(listSkipped[2],"EXAMS");
			List<Procedure> listProcedures=GetHistoryProcs(patient,insSub,out List<ClaimProc> listClaimProcs);
			Assert.AreEqual(2,listProcedures.Count);
			Procedure procedureBW=listProcedures.Single(x => ProcedureCodes.GetStringProcCode(x.CodeNum)==CODE_BW);
			Assert.AreEqual(new DateTime(2025,9,18),procedureBW.ProcDate,"September, not January.");
			Procedure procedureFluoride=listProcedures.Single(x => ProcedureCodes.GetStringProcCode(x.CodeNum)==CODE_FLUORIDE);
			Assert.AreEqual(new DateTime(2025,6,11),procedureFluoride.ProcDate);
			Assert.IsTrue(listClaimProcs.All(x => x.Status==ClaimProcStatus.InsHist));
		}

		[TestMethod]
		public void FrequencyCodes_ComeFromTheHistoryGroup_NotTheObsoletePreference() {
			CodeGroup codeGroup=SetFixedGroup(EnumCodeGroupFixed.BW,CODE_BW+","+CODE_BW_ALT,CODE_BW_HIST);
			string prefOld=PrefC.GetString(PrefName.InsHistBWCodes);
			try {
				Prefs.UpdateString(PrefName.InsHistBWCodes,CODE_BW_STALE);
				Patient patient=CreatePatientWithPlan(out InsSub insSub);
				InsPlan insPlan=InsPlans.GetPlan(insSub.PlanNum,null);
				Benefit benefit=BenefitT.CreateFrequencyCodeGroup(insPlan.PlanNum,codeGroup.CodeGroupNum,BenefitQuantity.NumberOfServices,1,
					BenefitTimePeriod.CalendarYear);
				Assert.IsTrue(Benefits.IsBitewingFrequency(benefit));
				List<long> listCodeNums=ProcedureCodes.GetCodeNumsForAllLimitations(new List<Benefit> { benefit },insPlan);
				Assert.IsTrue(listCodeNums.Contains(ProcedureCodeT.GetCodeNum(CODE_BW_HIST)),"The History Proc Code counts for frequencies.");
				Assert.IsTrue(listCodeNums.Contains(ProcedureCodeT.GetCodeNum(CODE_BW_ALT)));
				Assert.IsFalse(listCodeNums.Contains(ProcedureCodeT.GetCodeNum(CODE_BW_STALE)),"The InsHistBWCodes preference is no longer read.");
				CollectionAssert.AreEquivalent(new[] { CODE_BW,CODE_BW_ALT,CODE_BW_HIST },
					InsHistCategories.GetCodeNumsForInsHistPref(PrefName.InsHistBWCodes).Select(x => ProcedureCodes.GetStringProcCode(x)).ToList());
			}
			finally {
				Prefs.UpdateString(PrefName.InsHistBWCodes,prefOld);
			}
		}

		[TestMethod]
		public void CodeGroupCache_SavedOptionsShowUpAsRows() {
			CodeGroup codeGroup=AddGroup("Sealant",CODE_FLUORIDE,CODE_FLUORIDE);
			Assert.IsTrue(InsHistCategories.GetAll().Any(x => x.CodeGroupNum==codeGroup.CodeGroupNum && x.Description==GROUP_PREFIX+"Sealant"));
			codeGroup.ShowInHistory=false;
			CodeGroupHistoryOptions.Apply(codeGroup,false,codeGroup.HistProcCode,false);
			CodeGroups.Update(codeGroup);
			CodeGroups.RefreshCache();
			Assert.IsFalse(InsHistCategories.GetAll().Any(x => x.CodeGroupNum==codeGroup.CodeGroupNum),"Unchecked: the row is gone.");
			Assert.AreEqual("",CodeGroups.GetOneFromDb(codeGroup.CodeGroupNum).HistProcCode);
		}
	}
}
