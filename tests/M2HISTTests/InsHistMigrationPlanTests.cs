// The rule that moves the ten InsHist*Codes preferences into code groups (InsHistCodeGroupsMigration.Plan), without a database. Synthetic data
// only (made-up Z codes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations.FeatureSteps;

namespace M2HISTTests {
	[TestClass]
	public class InsHistMigrationPlanTests {
		private static readonly List<string> _listProcCodes=new List<string> { "Z0120","Z0150","Z0210","Z0272","Z0274","Z1110","Z1120","Z4341","Z4342",
			"Z4355","Z4910" };

		///<summary>Code groups like the ones Open Dental 24.x created from the old benefit preferences: one per fixed category, plus others.</summary>
		private static List<CodeGroup> GetGroups24() {
			return new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=1,GroupName="BW",ProcCodes="Z0272,Z0274",ItemOrder=0,CodeGroupFixed=EnumCodeGroupFixed.BW },
				new CodeGroup { CodeGroupNum=2,GroupName="Exam",ProcCodes="Z0120",ItemOrder=1,CodeGroupFixed=EnumCodeGroupFixed.Exam },
				new CodeGroup { CodeGroupNum=3,GroupName="Pano/FMX",ProcCodes="Z0210",ItemOrder=2,CodeGroupFixed=EnumCodeGroupFixed.PanoFMX },
				new CodeGroup { CodeGroupNum=4,GroupName="Prophy",ProcCodes="Z1110,Z1120",ItemOrder=3,CodeGroupFixed=EnumCodeGroupFixed.Prophy },
				new CodeGroup { CodeGroupNum=5,GroupName="SRP",ProcCodes="Z4341-Z4342",ItemOrder=4,CodeGroupFixed=EnumCodeGroupFixed.SRP },
				new CodeGroup { CodeGroupNum=6,GroupName="Crowns",ProcCodes="Z2740",ItemOrder=5 },
				new CodeGroup { CodeGroupNum=7,GroupName="Perio Maint",ProcCodes="Z4910",ItemOrder=6,CodeGroupFixed=EnumCodeGroupFixed.Perio },
			};
		}

		private static Dictionary<string,string> Prefs(params (PrefName PrefName,string Value)[] arrayPrefs) {
			return arrayPrefs.ToDictionary(x => x.PrefName.ToString(),x => x.Value);
		}

		[TestMethod]
		public void ExistingFixedGroups_GetShowInHistoryAndTheFirstCode() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistBWCodes,"Z0272"),(PrefName.InsHistExamCodes,"Z0150,Z0120"),
				(PrefName.InsHistProphyCodes,""));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,GetGroups24(),_listProcCodes,out List<string> listNotes);
			CollectionAssert.AreEqual(new long[] { 1,2 },listChanged.Select(x => x.CodeGroupNum).ToArray(),"Prophy has no value: no history row.");
			CodeGroup codeGroupBW=listChanged[0];
			Assert.IsFalse(codeGroupBW.IsNew);
			Assert.IsTrue(codeGroupBW.ShowInHistory);
			Assert.AreEqual("Z0272",codeGroupBW.HistProcCode);
			Assert.AreEqual("Z0272,Z0274",codeGroupBW.ProcCodes,"Nothing to add, the text is kept as it was.");
			Assert.IsFalse(codeGroupBW.IsPerioFourQuads);
			CodeGroup codeGroupExam=listChanged[1];
			Assert.AreEqual("Z0150",codeGroupExam.HistProcCode,"The first code written in the preference.");
			Assert.AreEqual("Z0120,Z0150",codeGroupExam.ProcCodes,"Every code of the preference is in the group.");
			Assert.AreEqual(0,listNotes.Count);
		}

		[TestMethod]
		public void MissingFixedGroup_IsCreatedAfterTheOthers() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistDebridementCodes,"Z4355"),(PrefName.InsHistPanoCodes,"Z0210"));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,GetGroups24(),_listProcCodes,out List<string> listNotes);
			Assert.AreEqual(2,listChanged.Count);
			Assert.AreEqual(3,listChanged[0].CodeGroupNum,"Pano comes first, in the order of the 24.3 window.");
			CodeGroup codeGroupNew=listChanged[1];
			Assert.IsTrue(codeGroupNew.IsNew);
			Assert.AreEqual("Full Mouth Debridement",codeGroupNew.GroupName);
			Assert.AreEqual(EnumCodeGroupFixed.FMDebride,codeGroupNew.CodeGroupFixed);
			Assert.AreEqual("Z4355",codeGroupNew.ProcCodes);
			Assert.AreEqual("Z4355",codeGroupNew.HistProcCode);
			Assert.AreEqual(7,codeGroupNew.ItemOrder,"After every existing group.");
			Assert.IsTrue(codeGroupNew.ShowInHistory);
			Assert.IsFalse(codeGroupNew.IsHidden || codeGroupNew.ShowInFrequency || codeGroupNew.ShowInAgeLimit || codeGroupNew.ShowInOther,
				"A created group is in no benefit grid.");
			Assert.AreEqual(1,listNotes.Count);
			StringAssert.Contains(listNotes[0],"created code group 'Full Mouth Debridement'");
		}

		[TestMethod]
		public void PerioQuadrants_OneFourQuadGroup_HistCodeFromUR() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistPerioULCodes,"Z4342"),(PrefName.InsHistPerioURCodes,"Z4341"),
				(PrefName.InsHistPerioLRCodes,"Z4341"),(PrefName.InsHistPerioLLCodes,""));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,GetGroups24(),_listProcCodes,out List<string> listNotes);
			Assert.AreEqual(1,listChanged.Count);
			CodeGroup codeGroupSRP=listChanged[0];
			Assert.AreEqual(5,codeGroupSRP.CodeGroupNum);
			Assert.IsTrue(codeGroupSRP.IsPerioFourQuads);
			Assert.AreEqual("Z4341",codeGroupSRP.HistProcCode,"UR first.");
			Assert.AreEqual("Z4341-Z4342",codeGroupSRP.ProcCodes,"Both codes are already inside the group's range.");
			Assert.AreEqual(1,listNotes.Count,"The quadrants had different codes.");
			StringAssert.Contains(listNotes[0],"Z4341, Z4342");
		}

		[TestMethod]
		public void PerioQuadrants_WithoutUR_TakeTheFirstQuadrantThatHasAValue() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistPerioLLCodes,"Z4342"),(PrefName.InsHistPerioLRCodes,"Z4341"));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,new List<CodeGroup>(),_listProcCodes,out List<string> _);
			Assert.AreEqual(1,listChanged.Count);
			Assert.AreEqual("Perio Scaling",listChanged[0].GroupName);
			Assert.AreEqual(EnumCodeGroupFixed.SRP,listChanged[0].CodeGroupFixed);
			Assert.AreEqual("Z4341",listChanged[0].HistProcCode,"Order UR, UL, LR, LL: LR before LL.");
			Assert.AreEqual("Z4341,Z4342",listChanged[0].ProcCodes);
			Assert.AreEqual(0,listChanged[0].ItemOrder);
		}

		[TestMethod]
		public void HistProcCode_SkipsCodesNotInTheList_ExpandsRanges_KeepsTheTextWhenNoneExists() {
			List<string> listSorted=_listProcCodes.OrderBy(x => x).ToList();
			Dictionary<string,string> dictCodes=listSorted.ToDictionary(x => x,x => x,StringComparer.OrdinalIgnoreCase);
			Assert.AreEqual("Z0120",InsHistCodeGroupsMigration.GetFirstCode("Z0001,z0120",listSorted,dictCodes),"Written as the code list has it.");
			Assert.AreEqual("Z4341",InsHistCodeGroupsMigration.GetFirstCode("Z4300-Z4349",listSorted,dictCodes));
			Assert.AreEqual("Z0001",InsHistCodeGroupsMigration.GetFirstCode("Z0001,Z0002",listSorted,dictCodes));
			Assert.AreEqual("Z0500",InsHistCodeGroupsMigration.GetFirstCode("Z0500-Z0599",listSorted,dictCodes));
			Assert.AreEqual("",InsHistCodeGroupsMigration.GetFirstCode(" , ",listSorted,dictCodes));
		}

		[TestMethod]
		public void MergeProcCodes_AppendsOnlyWhatIsMissing() {
			Assert.AreEqual("Z0120,Z0150",InsHistCodeGroupsMigration.MergeProcCodes("Z0120",new List<string> { "Z0150,z0120" }));
			Assert.AreEqual("Z4341-Z4342",InsHistCodeGroupsMigration.MergeProcCodes("Z4341-Z4342",new List<string> { "Z4342" }));
			Assert.AreEqual("Z4341,Z4300-Z4349",InsHistCodeGroupsMigration.MergeProcCodes("Z4341,",new List<string> { "Z4300-Z4349" }));
			Assert.AreEqual("Z1110,Z1120",InsHistCodeGroupsMigration.MergeProcCodes("",new List<string> { "Z1110","Z1120,Z1110" }));
			Assert.AreEqual("Z0120, ",InsHistCodeGroupsMigration.MergeProcCodes("Z0120, ",new List<string> { "" }),"Unchanged text is not tidied.");
		}

		[TestMethod]
		public void RetiredFixedGroup_IsUsedWhenNoGroupInUseHasTheCategory() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=8,GroupName="Old prophy",ProcCodes="Z1110",ItemOrder=9,CodeGroupFixed=EnumCodeGroupFixed.Prophy,IsHidden=true },
			};
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(Prefs((PrefName.InsHistProphyCodes,"Z1120")),listCodeGroups,_listProcCodes,
				out List<string> _);
			Assert.AreEqual(8,listChanged.Single().CodeGroupNum);
			Assert.AreEqual("Z1110,Z1120",listChanged.Single().ProcCodes);
			Assert.IsTrue(listChanged.Single().IsHidden,"Retired stays retired; its history row shows anyway.");
		}

		[TestMethod]
		public void NoPreferenceValues_ChangeNothing() {
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(new Dictionary<string,string>(),GetGroups24(),_listProcCodes,out List<string> listNotes);
			Assert.AreEqual(0,listChanged.Count);
			Assert.AreEqual(0,listNotes.Count);
			Assert.AreEqual(10,InsHistCodeGroupsMigration.ListPrefNames.Distinct().Count());
		}
	}
}
