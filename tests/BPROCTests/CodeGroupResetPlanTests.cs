// Reset Code Groups without a database: the shared default list (CodeGroupDefaults) and how a reset plan treats each existing group.
// Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPROCTests {
	[TestClass]
	public class CodeGroupResetPlanTests {
		[TestInitialize]
		public void TestInitialize() {
			System.Threading.Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo("en-US");
		}

		private static CodeGroup Group(long codeGroupNum,string groupName,EnumCodeGroupFixed codeGroupFixed=EnumCodeGroupFixed.None,int itemOrder=0) {
			CodeGroup codeGroup=new CodeGroup();
			codeGroup.CodeGroupNum=codeGroupNum;
			codeGroup.GroupName=groupName;
			codeGroup.CodeGroupFixed=codeGroupFixed;
			codeGroup.ItemOrder=itemOrder;
			codeGroup.ProcCodes="Z0001";
			return codeGroup;
		}

		[TestMethod]
		public void Defaults_Cdt_AreTheConversionList() {
			List<CodeGroup> listCodeGroups=CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDT);
			CollectionAssert.AreEqual(new[] { "BW","Exam","Pano/FMX","Cancer Screening","Prophy","Fluoride","Sealant","Crown","SRP","Full Debridement",
				"Perio Maint","Dentures","Implants" },listCodeGroups.Select(x => x.GroupName).ToArray());
			CollectionAssert.AreEqual(Enumerable.Range(0,13).ToArray(),listCodeGroups.Select(x => x.ItemOrder).ToArray());
			//Every fixed type appears exactly once, so sheets and insurance history find one group per type.
			foreach(EnumCodeGroupFixed codeGroupFixed in Enum.GetValues(typeof(EnumCodeGroupFixed))) {
				if(codeGroupFixed==EnumCodeGroupFixed.None) {
					continue;
				}
				Assert.AreEqual(1,listCodeGroups.Count(x => x.CodeGroupFixed==codeGroupFixed),codeGroupFixed.ToString());
			}
			Assert.AreEqual("D0272,D0274",listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.BW).ProcCodes);
			Assert.AreEqual("D4910",listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Perio).ProcCodes);
			Assert.IsTrue(listCodeGroups.All(x => x.ShowInFrequency && !x.IsHidden && !x.ShowInOther && x.CodeGroupNum==0));
			CollectionAssert.AreEquivalent(new[] { "Fluoride","Sealant" },listCodeGroups.Where(x => x.ShowInAgeLimit).Select(x => x.GroupName).ToArray());
		}

		[TestMethod]
		public void Defaults_HoldCodeNumbersOnly() {
			//Code numbers are shipped; code descriptions never are.
			Regex regexCode=new Regex(@"^[A-Z]?\d{4,5}$");
			foreach(EnumDentalCodeSystem codeSystem in new[] { EnumDentalCodeSystem.CDT,EnumDentalCodeSystem.CDA }) {
				foreach(CodeGroup codeGroup in CodeGroupDefaults.GetDefaults(codeSystem)) {
					foreach(string entry in ProcCodeRanges.Parse(codeGroup.ProcCodes).ListEntries) {
						Assert.IsTrue(regexCode.IsMatch(entry),codeSystem+" "+codeGroup.GroupName+": "+entry);
					}
				}
			}
		}

		[TestMethod]
		public void Defaults_Cda_SameGroupsWithCanadianCodes() {
			List<CodeGroup> listCdt=CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDT);
			List<CodeGroup> listCda=CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDA);
			CollectionAssert.AreEqual(listCdt.Select(x => x.GroupName).ToArray(),listCda.Select(x => x.GroupName).ToArray());
			CollectionAssert.AreEqual(listCdt.Select(x => x.CodeGroupFixed).ToArray(),listCda.Select(x => x.CodeGroupFixed).ToArray());
			Assert.AreEqual("12111",listCda.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Fluoride).ProcCodes);
			Assert.AreEqual("13401",listCda.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Sealant).ProcCodes);
			Assert.IsFalse(listCda.Any(x => x.ProcCodes.Contains("D")),"No D codes in the Canadian list.");
		}

		[TestMethod]
		public void GetCodeSystemForCulture_CanadaIsCda() {
			Assert.AreEqual(EnumDentalCodeSystem.CDA,CodeGroupDefaults.GetCodeSystemForCulture("en-CA"));
			Assert.AreEqual(EnumDentalCodeSystem.CDA,CodeGroupDefaults.GetCodeSystemForCulture("fr-CA"));
			Assert.AreEqual(EnumDentalCodeSystem.CDT,CodeGroupDefaults.GetCodeSystemForCulture("en-US"));
			Assert.AreEqual(EnumDentalCodeSystem.CDT,CodeGroupDefaults.GetCodeSystemForCulture(null));
		}

		[TestMethod]
		public void FindDefaultMatch_FixedTypeThenName() {
			List<CodeGroup> listDefaults=CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDT);
			int indexBw=listDefaults.FindIndex(x => x.CodeGroupFixed==EnumCodeGroupFixed.BW);
			int indexCrown=listDefaults.FindIndex(x => x.GroupName=="Crown");
			Assert.AreEqual(indexBw,CodeGroupResets.FindDefaultMatch(Group(1,"My bitewings",EnumCodeGroupFixed.BW),listDefaults),"Fixed type wins over the name.");
			Assert.AreEqual(indexCrown,CodeGroupResets.FindDefaultMatch(Group(2,"  crown "),listDefaults),"Name, trimmed, without regard to case.");
			Assert.AreEqual(-1,CodeGroupResets.FindDefaultMatch(Group(3,"Night guards"),listDefaults));
			Assert.AreEqual(-1,CodeGroupResets.FindDefaultMatch(Group(4,""),listDefaults));
			Assert.AreEqual(-1,CodeGroupResets.FindDefaultMatch(null,listDefaults));
		}

		[TestMethod]
		public void CreatePlan_ActionPerGroup() {
			List<CodeGroup> listExisting=new List<CodeGroup> {
				Group(11,"Bitewings",EnumCodeGroupFixed.BW,0),//used, fixed: moved to the new BW group
				Group(12,"Crown",itemOrder:1),//used, name match: moved
				Group(13,"Night guards",itemOrder:2),//used, no match: kept hidden
				Group(14,"Unused custom",itemOrder:3),//unused: deleted
				Group(15,"Exam",EnumCodeGroupFixed.Exam,4),//unused default: deleted (replaced)
			};
			Dictionary<long,int> dictBenefitCounts=new Dictionary<long,int> { { 11,5 },{ 12,2 },{ 13,3 } };
			CodeGroupResetPlan codeGroupResetPlan=CodeGroupResets.CreatePlan(EnumDentalCodeSystem.CDT,listExisting,dictBenefitCounts);
			Assert.AreEqual(13,codeGroupResetPlan.ListCodeGroupsNew.Count);
			Dictionary<long,CodeGroupResetItem> dictItems=codeGroupResetPlan.ListItems.ToDictionary(x => x.CodeGroupOld.CodeGroupNum);
			Assert.AreEqual(EnumCodeGroupResetAction.Repoint,dictItems[11].Action);
			Assert.AreEqual(EnumCodeGroupFixed.BW,codeGroupResetPlan.ListCodeGroupsNew[dictItems[11].IndexDefault].CodeGroupFixed);
			Assert.AreEqual(EnumCodeGroupResetAction.Repoint,dictItems[12].Action);
			Assert.AreEqual("Crown",codeGroupResetPlan.ListCodeGroupsNew[dictItems[12].IndexDefault].GroupName);
			Assert.AreEqual(EnumCodeGroupResetAction.KeepRetired,dictItems[13].Action);
			Assert.AreEqual(EnumCodeGroupResetAction.Delete,dictItems[14].Action);
			Assert.AreEqual(EnumCodeGroupResetAction.Delete,dictItems[15].Action);
			Assert.AreEqual(1,codeGroupResetPlan.GetUnmatched().Count);
			Assert.AreEqual(7,codeGroupResetPlan.GetCountBenefitsRepointed());
			Assert.AreEqual(4,codeGroupResetPlan.GetCountDeleted());
			string summary=CodeGroupResets.GetPlanSummary(codeGroupResetPlan);
			StringAssert.Contains(summary,"Night guards (3");
		}

		[TestMethod]
		public void CreatePlan_Cda_EmptyDefaultKeepsTheCurrentCodes() {
			CodeGroup codeGroupExamFixed=Group(31,"Exam",EnumCodeGroupFixed.Exam,0);
			codeGroupExamFixed.ProcCodes="99001";
			CodeGroup codeGroupExamName=Group(32," exam ",itemOrder:1);
			codeGroupExamName.ProcCodes="99002";
			CodeGroup codeGroupBw=Group(33,"Bitewings",EnumCodeGroupFixed.BW,2);//unused: its codes are kept too
			codeGroupBw.ProcCodes="99003,99004";
			CodeGroup codeGroupSrp=Group(34,"SRP",EnumCodeGroupFixed.SRP,3);//no codes: nothing to keep
			codeGroupSrp.ProcCodes="";
			CodeGroup codeGroupFluoride=Group(35,"Fluoride",EnumCodeGroupFixed.Fluoride,4);//the default has CDA codes: they win
			codeGroupFluoride.ProcCodes="99005";
			CodeGroup codeGroupProphyA=Group(36,"Prophy",EnumCodeGroupFixed.Prophy,5);
			codeGroupProphyA.ProcCodes="99006";
			CodeGroup codeGroupProphyB=Group(37,"prophy",itemOrder:6);
			codeGroupProphyB.ProcCodes="99007";
			List<CodeGroup> listExisting=new List<CodeGroup> { codeGroupExamFixed,codeGroupExamName,codeGroupBw,codeGroupSrp,codeGroupFluoride,
				codeGroupProphyA,codeGroupProphyB };
			Dictionary<long,int> dictBenefitCounts=new Dictionary<long,int> { { 31,1 },{ 32,3 },{ 34,2 },{ 35,1 },{ 36,2 },{ 37,2 } };
			CodeGroupResetPlan codeGroupResetPlan=CodeGroupResets.CreatePlan(EnumDentalCodeSystem.CDA,listExisting,dictBenefitCounts);
			List<CodeGroup> listNew=codeGroupResetPlan.ListCodeGroupsNew;
			Assert.AreEqual("99002",listNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Exam).ProcCodes,"Most benefits wins.");
			Assert.AreEqual("99003,99004",listNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.BW).ProcCodes,"Unused groups keep their codes too.");
			Assert.AreEqual("",listNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.SRP).ProcCodes);
			Assert.AreEqual("12111",listNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Fluoride).ProcCodes,"Default codes are not replaced.");
			Assert.AreEqual("99006",listNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Prophy).ProcCodes,"A tie goes to the first in ItemOrder.");
			CollectionAssert.AreEquivalent(new long[] { 32,33,36 },codeGroupResetPlan.GetCodesKept().Select(x => x.CodeGroupOld.CodeGroupNum).ToArray());
			Assert.AreEqual(EnumCodeGroupResetAction.Repoint,codeGroupResetPlan.ListItems.Single(x => x.CodeGroupOld.CodeGroupNum==31).Action);
			//The defaults themselves are never changed.
			Assert.AreEqual("",CodeGroupDefaults.GetDefaults(EnumDentalCodeSystem.CDA).Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Exam).ProcCodes);
			string summary=CodeGroupResets.GetPlanSummary(codeGroupResetPlan);
			StringAssert.Contains(summary,"no default codes for CDA: your current codes are kept");
			StringAssert.Contains(summary,"Exam: codes of  exam  (3 benefits)");
			StringAssert.Contains(summary,"BW: codes of Bitewings (0 benefits)");
			//Groups that stay empty are named.
			StringAssert.Contains(summary,"and will be created empty");
			Assert.IsTrue(codeGroupResetPlan.GetDefaultsWithoutCodes().Any(x => x.CodeGroupFixed==EnumCodeGroupFixed.SRP));
			Assert.IsFalse(codeGroupResetPlan.GetDefaultsWithoutCodes().Any(x => x.CodeGroupFixed==EnumCodeGroupFixed.Exam));
		}

		[TestMethod]
		public void CreatePlan_Cdt_DefaultCodesAlwaysWin() {
			CodeGroup codeGroupExam=Group(41,"Exam",EnumCodeGroupFixed.Exam,0);
			codeGroupExam.ProcCodes="D9001";
			CodeGroupResetPlan codeGroupResetPlan=CodeGroupResets.CreatePlan(EnumDentalCodeSystem.CDT,new List<CodeGroup> { codeGroupExam },
				new Dictionary<long,int> { { 41,4 } });
			Assert.AreEqual("D0120,D0150",codeGroupResetPlan.ListCodeGroupsNew.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Exam).ProcCodes);
			Assert.AreEqual(0,codeGroupResetPlan.GetCodesKept().Count);
			Assert.AreEqual(0,codeGroupResetPlan.GetDefaultsWithoutCodes().Count);
			string summary=CodeGroupResets.GetPlanSummary(codeGroupResetPlan);
			Assert.IsFalse(summary.Contains("no default codes"),summary);
		}

		[TestMethod]
		public void CreatePlan_NoGroupsInUse_NothingUnmatched() {
			List<CodeGroup> listExisting=new List<CodeGroup> { Group(21,"Night guards"),Group(22,"BW",EnumCodeGroupFixed.BW) };
			CodeGroupResetPlan codeGroupResetPlan=CodeGroupResets.CreatePlan(EnumDentalCodeSystem.CDA,listExisting,new Dictionary<long,int>());
			Assert.AreEqual(0,codeGroupResetPlan.GetUnmatched().Count);
			Assert.IsTrue(codeGroupResetPlan.ListItems.All(x => x.Action==EnumCodeGroupResetAction.Delete));
			Assert.AreEqual(EnumDentalCodeSystem.CDA,codeGroupResetPlan.CodeSystem);
		}

		[TestMethod]
		public void GetLogText_NamesCodeSystemAndCounts() {
			CodeGroupResetResult codeGroupResetResult=new CodeGroupResetResult {
				CodeSystem=EnumDentalCodeSystem.CDT,IsDone=true,CountCreated=13,CountDeleted=4,CountKeptRetired=1,CountBenefitsRepointed=7
			};
			string logText=CodeGroupResets.GetLogText(codeGroupResetResult);
			StringAssert.Contains(logText,"CDT");
			StringAssert.Contains(logText,"13 created");
			StringAssert.Contains(logText,"4 deleted");
			StringAssert.Contains(logText,"1 kept as hidden");
			StringAssert.Contains(logText,"7 benefits");
			Assert.IsFalse(logText.Contains("kept their current codes"),"Only named when codes were kept.");
			codeGroupResetResult.CountProcCodesKept=2;
			StringAssert.Contains(CodeGroupResets.GetLogText(codeGroupResetResult),"2 groups with no default codes kept their current codes.");
		}
	}
}
