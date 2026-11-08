// The rule that moves the ten InsHist*Codes preferences into code groups (InsHistCodeGroupsMigration.Plan), without a database. Synthetic data
// only (made-up Z codes). Owner decision of 2026-11-07 (switch-over question 4): the existing code groups, the frequency and age limit groups
// among them, are never changed; each history category gets a new code group used only for Insurance History.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace M2HISTTests {
	[TestClass]
	public class InsHistMigrationPlanTests {
		private static readonly List<string> _listProcCodes=new List<string> { "Z0120","Z0150","Z0210","Z0272","Z0274","Z1110","Z1120","Z4341","Z4342",
			"Z4355","Z4910" };

		///<summary>Code groups like the ones Open Dental 24.x created from the old benefit preferences: one per fixed category, plus others.</summary>
		private static List<CodeGroup> GetGroups24() {
			return new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=1,GroupName="BW",ProcCodes="Z0272,Z0274",ItemOrder=0,CodeGroupFixed=EnumCodeGroupFixed.BW,ShowInFrequency=true },
				new CodeGroup { CodeGroupNum=2,GroupName="Exam",ProcCodes="Z0120",ItemOrder=1,CodeGroupFixed=EnumCodeGroupFixed.Exam,ShowInFrequency=true },
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

		///<summary>The fields of a code group the step must never change.</summary>
		private static string Fields(CodeGroup codeGroup) {
			return string.Join("|",codeGroup.CodeGroupNum,codeGroup.GroupName,codeGroup.ProcCodes,codeGroup.ItemOrder,codeGroup.CodeGroupFixed,
				codeGroup.IsHidden,codeGroup.ShowInAgeLimit,codeGroup.ShowInFrequency,codeGroup.ShowInOther,codeGroup.ShowInHistory,codeGroup.HistProcCode,
				codeGroup.IsPerioFourQuads);
		}

		[TestMethod]
		public void EachCategory_GetsANewHistoryOnlyGroup_TheExistingGroupsStayAsTheyAre() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistBWCodes,"Z0272"),(PrefName.InsHistExamCodes,"Z0150,Z0120"),
				(PrefName.InsHistProphyCodes,""));
			List<CodeGroup> listCodeGroups=GetGroups24();
			List<string> listFieldsBefore=listCodeGroups.Select(Fields).ToList();
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,listCodeGroups,_listProcCodes,out List<string> listNotes);
			CollectionAssert.AreEqual(listFieldsBefore,listCodeGroups.Select(Fields).ToList(),"The groups passed in are not touched.");
			Assert.AreEqual(2,listChanged.Count,"Prophy has no value: no history row.");
			Assert.IsTrue(listChanged.All(x => x.IsNew),"Only added groups: no existing group, the frequency groups among them, is changed.");
			CodeGroup codeGroupBW=listChanged[0];
			Assert.AreEqual("Bitewing History",codeGroupBW.GroupName);
			Assert.AreEqual(7,codeGroupBW.ItemOrder,"After every existing group.");
			Assert.IsTrue(codeGroupBW.ShowInHistory);
			Assert.AreEqual("Z0272",codeGroupBW.HistProcCode);
			Assert.AreEqual("Z0272",codeGroupBW.ProcCodes,"Only the codes of the preference.");
			Assert.AreEqual(EnumCodeGroupFixed.None,codeGroupBW.CodeGroupFixed,"Never a category's group: not in frequency limits, sheets or resets.");
			Assert.IsFalse(codeGroupBW.IsHidden || codeGroupBW.ShowInFrequency || codeGroupBW.ShowInAgeLimit || codeGroupBW.ShowInOther,
				"Used only for Insurance History: in no benefit grid.");
			Assert.IsFalse(codeGroupBW.IsPerioFourQuads);
			CodeGroup codeGroupExam=listChanged[1];
			Assert.AreEqual("Exam History",codeGroupExam.GroupName);
			Assert.AreEqual(8,codeGroupExam.ItemOrder);
			Assert.AreEqual("Z0150",codeGroupExam.HistProcCode,"The first code written in the preference.");
			Assert.AreEqual("Z0150,Z0120",codeGroupExam.ProcCodes,"Every code of the preference, in the order written.");
			Assert.AreEqual(EnumCodeGroupFixed.None,codeGroupExam.CodeGroupFixed);
			Assert.AreEqual(InsHistCodeGroupsMigration.NOTE_EXISTING_UNCHANGED,listNotes.Single());
		}

		[TestMethod]
		public void EveryCategory_InTheOrderOfThe24_3Window() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistBWCodes,"Z0272"),(PrefName.InsHistPanoCodes,"Z0210"),(PrefName.InsHistExamCodes,"Z0120"),
				(PrefName.InsHistProphyCodes,"Z1110"),(PrefName.InsHistPerioURCodes,"Z4341"),(PrefName.InsHistPerioMaintCodes,"Z4910"),
				(PrefName.InsHistDebridementCodes,"Z4355"));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,GetGroups24(),_listProcCodes,out List<string> _);
			CollectionAssert.AreEqual(new[] { "Bitewing History","Pano/FMX History","Exam History","Prophy History","Perio Scaling History",
				"Perio Maintenance History","Full Mouth Debridement History" },listChanged.Select(x => x.GroupName).ToArray());
			CollectionAssert.AreEqual(Enumerable.Range(7,7).ToArray(),listChanged.Select(x => x.ItemOrder).ToArray());
			Assert.IsTrue(listChanged.All(x => x.IsNew && x.ShowInHistory && x.CodeGroupFixed==EnumCodeGroupFixed.None));
		}

		[TestMethod]
		public void AGroupNameInUse_GetsANumber() {
			List<CodeGroup> listCodeGroups=GetGroups24();
			listCodeGroups.Add(new CodeGroup { CodeGroupNum=8,GroupName="exam history",ProcCodes="Z0120",ItemOrder=7 });
			listCodeGroups.Add(new CodeGroup { CodeGroupNum=9,GroupName="Exam History 2",ProcCodes="Z0120",ItemOrder=8 });
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(Prefs((PrefName.InsHistExamCodes,"Z0120")),listCodeGroups,_listProcCodes,
				out List<string> _);
			Assert.AreEqual("Exam History 3",listChanged.Single().GroupName);
			Assert.AreEqual(9,listChanged.Single().ItemOrder);
		}

		[TestMethod]
		public void PerioQuadrants_OneFourQuadGroup_HistCodeFromUR() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistPerioULCodes,"Z4342"),(PrefName.InsHistPerioURCodes,"Z4341"),
				(PrefName.InsHistPerioLRCodes,"Z4341"),(PrefName.InsHistPerioLLCodes,""));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,GetGroups24(),_listProcCodes,out List<string> listNotes);
			Assert.AreEqual(1,listChanged.Count);
			CodeGroup codeGroupSRP=listChanged[0];
			Assert.IsTrue(codeGroupSRP.IsNew,"The existing SRP group is not changed.");
			Assert.AreEqual("Perio Scaling History",codeGroupSRP.GroupName);
			Assert.IsTrue(codeGroupSRP.IsPerioFourQuads);
			Assert.AreEqual(EnumCodeGroupFixed.None,codeGroupSRP.CodeGroupFixed);
			Assert.AreEqual("Z4341",codeGroupSRP.HistProcCode,"UR first.");
			Assert.AreEqual("Z4341,Z4342",codeGroupSRP.ProcCodes,"Every code of the four preferences, once.");
			Assert.AreEqual(2,listNotes.Count,"The quadrants had different codes; then the closing note.");
			StringAssert.Contains(listNotes[0],"Z4341, Z4342");
			Assert.AreEqual(InsHistCodeGroupsMigration.NOTE_EXISTING_UNCHANGED,listNotes[1]);
		}

		[TestMethod]
		public void PerioQuadrants_WithoutUR_TakeTheFirstQuadrantThatHasAValue() {
			Dictionary<string,string> dictPrefs=Prefs((PrefName.InsHistPerioLLCodes,"Z4342"),(PrefName.InsHistPerioLRCodes,"Z4341"));
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,new List<CodeGroup>(),_listProcCodes,out List<string> _);
			Assert.AreEqual(1,listChanged.Count);
			Assert.AreEqual("Perio Scaling History",listChanged[0].GroupName);
			Assert.AreEqual(EnumCodeGroupFixed.None,listChanged[0].CodeGroupFixed);
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
		public void RetiredGroups_AreLeftAlone_TheHistoryGroupIsNew() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=8,GroupName="Old prophy",ProcCodes="Z1110",ItemOrder=9,CodeGroupFixed=EnumCodeGroupFixed.Prophy,IsHidden=true },
			};
			List<CodeGroup> listChanged=InsHistCodeGroupsMigration.Plan(Prefs((PrefName.InsHistProphyCodes,"Z1120")),listCodeGroups,_listProcCodes,
				out List<string> _);
			CodeGroup codeGroup=listChanged.Single();
			Assert.IsTrue(codeGroup.IsNew);
			Assert.AreEqual("Prophy History",codeGroup.GroupName);
			Assert.AreEqual("Z1120",codeGroup.ProcCodes);
			Assert.AreEqual(10,codeGroup.ItemOrder);
			Assert.IsFalse(codeGroup.IsHidden);
			Assert.AreEqual("Z1110",listCodeGroups[0].ProcCodes,"The retired group keeps its codes.");
		}

		[TestMethod]
		public void LogLines_SayEachAddedGroup_AndThatTheOthersAreUnchanged() {
			InsHistCodeGroupsMigration.MigrationPlan plan=InsHistCodeGroupsMigration.CreatePlan(true,false,
				Prefs((PrefName.InsHistExamCodes,"Z0120,Z0150"),(PrefName.InsHistPerioURCodes,"Z4341")),GetGroups24(),_listProcCodes);
			Assert.IsTrue(plan.IsRun);
			Assert.AreEqual(0,plan.ListUpdated.Count,"Nothing is updated.");
			Assert.AreEqual(2,plan.ListAdded.Count);
			List<string> listLines=plan.GetLogLines();
			Assert.AreEqual(3,listLines.Count);
			Assert.AreEqual("Added code group 'Exam History' (ItemOrder 7), used only for Insurance History (not in frequency or age limits), "
				+"History Proc Code Z0120, codes Z0120,Z0150",listLines[0]);
			Assert.AreEqual("Added code group 'Perio Scaling History' (ItemOrder 8), used only for Insurance History with four quadrant rows "
				+"(not in frequency or age limits), History Proc Code Z4341, codes Z4341",listLines[1]);
			Assert.AreEqual(InsHistCodeGroupsMigration.NOTE_EXISTING_UNCHANGED,listLines[2]);
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
