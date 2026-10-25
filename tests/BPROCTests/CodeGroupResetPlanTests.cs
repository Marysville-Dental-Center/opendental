// Reset Code Groups without a database: the shared default list (CodeGroupDefaults) and how a reset plan treats each existing group.
// Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

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
		}
	}
}
