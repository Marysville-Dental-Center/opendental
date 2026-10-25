// Procedure Code Tools against a scratch database: Reset Code Groups (benefits moved, unmatched groups kept hidden, nothing orphaned) and the D
// Codes and CDA Codes imports (codes added, descriptions updated, codes moved to Obsolete, nothing deleted). Synthetic codes and descriptions
// only: no ADA or CDA code text.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace BPROCTests {
	[TestClass]
	public class ProcToolsDbTests {
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			ProcDb.Begin();
		}

		#region Helpers
		private static CodeGroup InsertGroup(string groupName,EnumCodeGroupFixed codeGroupFixed=EnumCodeGroupFixed.None) {
			CodeGroup codeGroup=new CodeGroup();
			codeGroup.GroupName=groupName;
			codeGroup.ProcCodes="Z0001";
			codeGroup.ItemOrder=500+(++_counter);
			codeGroup.CodeGroupFixed=codeGroupFixed;
			codeGroup.ShowInFrequency=true;
			CodeGroups.Insert(codeGroup);
			return codeGroup;
		}

		///<summary>Benefits on a patient plan override (PatPlanNum only), which writes no insurance edit log.</summary>
		private static Benefit InsertBenefit(long codeGroupNum) {
			Benefit benefit=new Benefit();
			benefit.PatPlanNum=900000+(++_counter);
			benefit.BenefitType=InsBenefitType.Limitations;
			benefit.CodeGroupNum=codeGroupNum;
			benefit.Quantity=1;
			benefit.QuantityQualifier=BenefitQuantity.NumberOfServices;
			benefit.TimePeriod=BenefitTimePeriod.CalendarYear;
			benefit.MonetaryAmt=-1;
			benefit.Percent=-1;
			Benefits.Insert(benefit);
			return benefit;
		}

		private static long GetBenefitCodeGroupNum(long benefitNum) {
			return PIn.Long(DataCore.GetScalar("SELECT CodeGroupNum FROM benefit WHERE BenefitNum="+POut.Long(benefitNum)));
		}

		private static List<CodeGroup> GetGroupsFromDb() {
			return CodeGroups.GetManyFromDb();
		}

		private static int CountLogs(EnumPermType permType,string textLike) {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+POut.Int((int)permType)
				+" AND LogText LIKE '%"+POut.String(textLike)+"%'"));
		}

		///<summary>Unused codes "D9xxx" for synthetic tests, never codes already in the database.</summary>
		private static List<string> GetUnusedDCodes(int count) {
			HashSet<string> hashSetUsed=new HashSet<string>(DataCore.GetTable("SELECT ProcCode FROM procedurecode").Rows.Cast<DataRow>()
				.Select(x => x["ProcCode"].ToString()));
			List<string> listCodes=new List<string>();
			for(int number=9990;number>=9000 && listCodes.Count<count;number--) {
				string code="D"+number;
				if(!hashSetUsed.Contains(code)) {
					listCodes.Add(code);
				}
			}
			return listCodes;
		}

		private static long InsertCategory(string itemName) {
			Def def=new Def();
			def.Category=DefCat.ProcCodeCats;
			def.ItemName=itemName;
			def.ItemOrder=Defs.GetDefsForCategory(DefCat.ProcCodeCats).Count;
			Defs.Insert(def);
			Defs.RefreshCache();
			return def.DefNum;
		}
		#endregion Helpers

		#region Reset Code Groups
		///<summary>Each reset test starts from no code groups and no code group benefits, so the tests do not depend on each other's order. Scratch
		///database only.</summary>
		private static void ClearCodeGroups() {
			DataCore.NonQ("DELETE FROM benefit WHERE CodeGroupNum!=0");
			DataCore.NonQ("DELETE FROM codegroup");
			CodeGroups.RefreshCache();
		}

		[TestMethod]
		public void Reset_RepointsBenefits_KeepsUnmatchedHidden_DeletesTheRest() {
			ClearCodeGroups();
			CodeGroup codeGroupBw=InsertGroup("Synthetic bitewings "+_counter,EnumCodeGroupFixed.BW);
			CodeGroup codeGroupCrown=InsertGroup("CROWN");
			CodeGroup codeGroupNight=InsertGroup("Synthetic night guards "+_counter);
			CodeGroup codeGroupUnused=InsertGroup("Synthetic unused "+_counter);
			List<Benefit> listBenefitsBw=new List<Benefit> { InsertBenefit(codeGroupBw.CodeGroupNum),InsertBenefit(codeGroupBw.CodeGroupNum) };
			Benefit benefitCrown=InsertBenefit(codeGroupCrown.CodeGroupNum);
			List<Benefit> listBenefitsNight=new List<Benefit> { InsertBenefit(codeGroupNight.CodeGroupNum),InsertBenefit(codeGroupNight.CodeGroupNum) };
			int countBenefits=PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM benefit"));
			//Not accepted: nothing changes.
			CodeGroupResetResult codeGroupResetResultRefused=CodeGroupResets.Reset(EnumDentalCodeSystem.CDT,doKeepUnmatched:false);
			Assert.IsFalse(codeGroupResetResultRefused.IsDone);
			List<CodeGroup> listCodeGroups=GetGroupsFromDb();
			Assert.IsTrue(listCodeGroups.Any(x => x.CodeGroupNum==codeGroupBw.CodeGroupNum));
			Assert.IsTrue(listCodeGroups.Any(x => x.CodeGroupNum==codeGroupUnused.CodeGroupNum));
			Assert.AreEqual(codeGroupBw.CodeGroupNum,GetBenefitCodeGroupNum(listBenefitsBw[0].BenefitNum));
			//Accepted.
			int countLogsBefore=CountLogs(EnumPermType.ProcCodeEdit,"Code groups reset");
			CodeGroupResetResult codeGroupResetResult=CodeGroupResets.Reset(EnumDentalCodeSystem.CDT,doKeepUnmatched:true);
			Assert.IsTrue(codeGroupResetResult.IsDone);
			Assert.AreEqual(13,codeGroupResetResult.CountCreated);
			Assert.AreEqual(1,codeGroupResetResult.CountKeptRetired);
			Assert.AreEqual(3,codeGroupResetResult.CountBenefitsRepointed);
			listCodeGroups=GetGroupsFromDb();
			Assert.AreEqual(14,listCodeGroups.Count,"13 defaults and the kept group.");
			Assert.IsFalse(listCodeGroups.Any(x => x.CodeGroupNum==codeGroupBw.CodeGroupNum));
			Assert.IsFalse(listCodeGroups.Any(x => x.CodeGroupNum==codeGroupCrown.CodeGroupNum));
			Assert.IsFalse(listCodeGroups.Any(x => x.CodeGroupNum==codeGroupUnused.CodeGroupNum));
			CodeGroup codeGroupNightAfter=listCodeGroups.Single(x => x.CodeGroupNum==codeGroupNight.CodeGroupNum);
			Assert.IsTrue(codeGroupNightAfter.IsHidden,"Kept as a hidden (retired) group.");
			Assert.AreEqual(13,codeGroupNightAfter.ItemOrder);
			CodeGroup codeGroupBwNew=listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.BW);
			Assert.AreEqual("BW",codeGroupBwNew.GroupName);
			Assert.AreEqual("D0272,D0274",codeGroupBwNew.ProcCodes);
			Assert.IsTrue(listBenefitsBw.All(x => GetBenefitCodeGroupNum(x.BenefitNum)==codeGroupBwNew.CodeGroupNum));
			Assert.AreEqual(listCodeGroups.Single(x => x.GroupName=="Crown").CodeGroupNum,GetBenefitCodeGroupNum(benefitCrown.BenefitNum));
			Assert.IsTrue(listBenefitsNight.All(x => GetBenefitCodeGroupNum(x.BenefitNum)==codeGroupNight.CodeGroupNum));
			//Nothing deleted or orphaned.
			Assert.AreEqual(countBenefits,PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM benefit")));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM benefit WHERE CodeGroupNum!=0 "
				+"AND CodeGroupNum NOT IN(SELECT CodeGroupNum FROM codegroup)"));
			Assert.AreEqual(countLogsBefore+1,CountLogs(EnumPermType.ProcCodeEdit,"Code groups reset to the default CDT"));
			//The cache follows after a refresh, and the grids see the defaults.
			CodeGroups.RefreshCache();
			Assert.AreEqual(13,CodeGroups.GetForFrequencyGrid().Count);
			CollectionAssert.AreEquivalent(new[] { "Fluoride","Sealant" },CodeGroups.GetForAgeLimitGrid().Select(x => x.GroupName).ToArray());
		}

		[TestMethod]
		public void Reset_Twice_IsStable() {
			ClearCodeGroups();
			CodeGroup codeGroupCustom=InsertGroup("Synthetic custom twice "+_counter);
			Benefit benefit=InsertBenefit(codeGroupCustom.CodeGroupNum);
			CodeGroupResets.Reset(EnumDentalCodeSystem.CDA,doKeepUnmatched:true);
			CodeGroupResetResult codeGroupResetResult=CodeGroupResets.Reset(EnumDentalCodeSystem.CDA,doKeepUnmatched:true);
			Assert.IsTrue(codeGroupResetResult.IsDone);
			List<CodeGroup> listCodeGroups=GetGroupsFromDb();
			Assert.AreEqual(13,listCodeGroups.Count(x => !x.IsHidden));
			Assert.AreEqual(1,listCodeGroups.Count(x => x.CodeGroupFixed==EnumCodeGroupFixed.Sealant));
			Assert.AreEqual("13401",listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Sealant).ProcCodes);
			Assert.AreEqual(codeGroupCustom.CodeGroupNum,GetBenefitCodeGroupNum(benefit.BenefitNum));
			Assert.IsTrue(listCodeGroups.Single(x => x.CodeGroupNum==codeGroupCustom.CodeGroupNum).IsHidden);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM benefit WHERE CodeGroupNum!=0 "
				+"AND CodeGroupNum NOT IN(SELECT CodeGroupNum FROM codegroup)"));
		}
		#endregion Reset Code Groups

		#region Code imports
		[TestMethod]
		public void CdtImport_AddsUpdatesAndMovesToObsolete_DeletesNothing() {
			List<string> listCodes=GetUnusedDCodes(4);
			Assert.AreEqual(4,listCodes.Count);
			long defNumCat=InsertCategory("Synthetic BPROC cat "+(++_counter));
			ProcedureCode procedureCodeOld=ProcedureCodeT.CreateProcCode(listCodes[0],descript:"Synthetic old text",procCat:defNumCat);
			ProcedureCode procedureCodeGone=ProcedureCodeT.CreateProcCode(listCodes[1],descript:"Synthetic removed",abbrDesc:"SynRm",procCat:defNumCat);
			ProcedureCodes.RefreshCache();
			int countCodesBefore=PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM procedurecode"));
			string categoryNew="Synthetic BPROC new cat "+(++_counter);
			string fileText="ProcCode\tDescript\tAbbrDesc\tCategory\tTreatArea\tDeleted\r\n"
				+listCodes[0]+"\tSynthetic new text\tSynNew\t\t\t\r\n"
				+listCodes[1]+"\tSynthetic removed\t\t\t\t1\r\n"
				+listCodes[2]+"\tSynthetic added\tSynAdd\t"+categoryNew+"\tTooth\t\r\n"
				+listCodes[3]+"\tSynthetic added no category\t\t\t\t\r\n"
				+"99990\tSynthetic not a D code\t\t\t\t\r\n";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(fileText,out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			ProcCodeImportPlan procCodeImportPlan=ProcCodeImports.CreatePlanFromDb(listRows,EnumDentalCodeSystem.CDT);
			string categoryExpectedNoCat=ProcCodeImports.GetCategoryForNewCode(listCodes[3],ProcedureCodes.GetAllCodes(),
				Defs.GetDefsForCategory(DefCat.ProcCodeCats));
			ProcCodeImportResult procCodeImportResult=ProcCodeImports.Apply(procCodeImportPlan);
			Assert.AreEqual(2,procCodeImportResult.CountAdded);
			Assert.AreEqual(1,procCodeImportResult.CountDescriptionsUpdated);
			Assert.AreEqual(1,procCodeImportResult.CountAbbrsFilled);
			Assert.AreEqual(1,procCodeImportResult.CountMovedToObsolete);
			Defs.RefreshCache();
			ProcedureCodes.RefreshCache();
			Assert.AreEqual(countCodesBefore+2,PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM procedurecode")),"Nothing is deleted.");
			ProcedureCode procedureCodeOldAfter=ProcedureCodes.GetProcCodeFromDb(procedureCodeOld.CodeNum);
			Assert.AreEqual("Synthetic new text",procedureCodeOldAfter.Descript);
			Assert.AreEqual("SynNew",procedureCodeOldAfter.AbbrDesc);
			Assert.AreEqual(defNumCat,procedureCodeOldAfter.ProcCat);
			ProcedureCode procedureCodeGoneAfter=ProcedureCodes.GetProcCodeFromDb(procedureCodeGone.CodeNum);
			Def defObsolete=Defs.GetDef(DefCat.ProcCodeCats,procedureCodeGoneAfter.ProcCat);
			Assert.AreEqual(ProcCodeImports.CATEGORY_OBSOLETE,defObsolete.ItemName);
			Assert.IsTrue(defObsolete.IsHidden);
			Assert.AreEqual("SynRm",procedureCodeGoneAfter.AbbrDesc);
			ProcedureCode procedureCodeAdded=ProcedureCodes.GetProcCode(listCodes[2]);
			Assert.AreEqual("Synthetic added",procedureCodeAdded.Descript);
			Assert.AreEqual(TreatmentArea.Tooth,procedureCodeAdded.TreatArea);
			Assert.AreEqual(categoryNew,Defs.GetName(DefCat.ProcCodeCats,procedureCodeAdded.ProcCat),"Categories are created by name.");
			Assert.AreEqual(0,procedureCodeAdded.ProvNumDefault);
			Assert.AreEqual(categoryExpectedNoCat,Defs.GetName(DefCat.ProcCodeCats,ProcedureCodes.GetProcCode(listCodes[3]).ProcCat));
			Assert.IsFalse(ProcedureCodes.GetContainsKey("99990"));
			Assert.IsTrue(CountLogs(EnumPermType.ProcCodeEdit,"added from the CDT code import")>=2);
			Assert.IsTrue(CountLogs(EnumPermType.ProcCodeEdit,"CDT code import from a user-supplied file")>=1);
			//The same file again has nothing left to do.
			ProcCodeImportPlan procCodeImportPlanAgain=ProcCodeImports.CreatePlanFromDb(listRows,EnumDentalCodeSystem.CDT);
			Assert.IsFalse(procCodeImportPlanAgain.HasWork(),ProcCodeImports.GetPlanSummary(procCodeImportPlanAgain,null));
		}

		[TestMethod]
		public void CdaImport_AddsMissing_LeavesExistingAlone() {
			string codeExisting="9998"+(++_counter%10);
			string codeNew="9997"+(_counter%10);
			ProcedureCode procedureCodeExisting=ProcedureCodeT.CreateProcCode(codeExisting,descript:"Synthetic Canadian kept",abbrDesc:"");
			ProcedureCodes.RefreshCache();
			Assert.IsFalse(ProcedureCodes.GetContainsKey(codeNew));
			string fileText=codeExisting+"\tSynthetic Canadian changed\t2\t0\t0\t0\t0\t\t/X/\tSynChg\t1\n"
				+codeNew+"\tSynthetic Canadian added\t2\t0\t0\t1\t0\tSynthetic BPROC Canada\t/XX/\tSynAdd\t1.5\n";
			List<ProcCodeImportRow> listRows=ProcCodeImports.Parse(fileText,out List<string> listErrors);
			Assert.AreEqual(0,listErrors.Count,string.Join("; ",listErrors));
			ProcCodeImportResult procCodeImportResult=ProcCodeImports.Apply(ProcCodeImports.CreatePlanFromDb(listRows,EnumDentalCodeSystem.CDA));
			Assert.AreEqual(1,procCodeImportResult.CountAdded);
			Assert.AreEqual(0,procCodeImportResult.CountDescriptionsUpdated);
			ProcedureCodes.RefreshCache();
			Defs.RefreshCache();
			ProcedureCode procedureCodeExistingAfter=ProcedureCodes.GetProcCodeFromDb(procedureCodeExisting.CodeNum);
			Assert.AreEqual("Synthetic Canadian kept",procedureCodeExistingAfter.Descript);
			Assert.AreEqual("",procedureCodeExistingAfter.AbbrDesc??"");
			ProcedureCode procedureCodeNew=ProcedureCodes.GetProcCode(codeNew);
			Assert.AreEqual("Synthetic Canadian added",procedureCodeNew.Descript);
			Assert.IsTrue(procedureCodeNew.IsHygiene);
			Assert.AreEqual(1.5,procedureCodeNew.CanadaTimeUnits);
			Assert.AreEqual("/XX/",procedureCodeNew.ProcTime);
			Assert.AreEqual("Synthetic BPROC Canada",Defs.GetName(DefCat.ProcCodeCats,procedureCodeNew.ProcCat));
			Assert.IsTrue(CountLogs(EnumPermType.ProcCodeEdit,"added from the CDA code import")>=1);
		}
		#endregion Code imports
	}
}
