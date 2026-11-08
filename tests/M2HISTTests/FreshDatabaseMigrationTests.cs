// The M2-HIST migration as the fork migration runner runs it on a fresh 24.3.63 database: the ten InsHist*Codes preferences of the seed become
// Insurance History rows from code groups used only for Insurance History (owner decision of 2026-11-07, switch-over question 4), every code group
// the seed had is unchanged, the preferences are kept, and running the steps again changes nothing.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace M2HISTTests {
	[TestClass]
	public class FreshDatabaseMigrationTests {
		///<summary>The 24.3 row label of each preference, which names the history group the step adds for it (plus " History").</summary>
		private static readonly Dictionary<PrefName,string> _dictLabels=new Dictionary<PrefName,string> {
			{ PrefName.InsHistBWCodes,"Bitewing" },{ PrefName.InsHistPanoCodes,"Pano/FMX" },{ PrefName.InsHistExamCodes,"Exam" },
			{ PrefName.InsHistProphyCodes,"Prophy" },{ PrefName.InsHistPerioURCodes,"Perio Scaling" },{ PrefName.InsHistPerioULCodes,"Perio Scaling" },
			{ PrefName.InsHistPerioLRCodes,"Perio Scaling" },{ PrefName.InsHistPerioLLCodes,"Perio Scaling" },{ PrefName.InsHistPerioMaintCodes,"Perio Maintenance" },
			{ PrefName.InsHistDebridementCodes,"Full Mouth Debridement" },
		};

		[TestInitialize]
		public void TestInitialize() {
			M2HistDb.Begin();
		}

		private static List<CodeGroup> GetAddedGroups() {
			return M2HistDb.ListCodeGroupsAfter.FindAll(x => !M2HistDb.DictRowsBefore.ContainsKey(x.CodeGroupNum));
		}

		[TestMethod]
		public void Fresh243_PreferencesAreSetInTheSeed() {
			//The rest of this class relies on the seed having history codes, as an office database does.
			Assert.AreEqual(10,M2HistDb.DictPrefsBefore.Count);
			Assert.IsTrue(M2HistDb.DictPrefsBefore.Values.All(x => x.Trim()!=""),string.Join("; ",M2HistDb.DictPrefsBefore));
		}

		[TestMethod]
		public void Fresh243_EachPreferenceBecameAHistoryOnlyGroup() {
			Assert.IsTrue(M2HistDb.ListChangesStep.Count>0,"The step ran and added code groups.");
			List<CodeGroup> listAdded=GetAddedGroups();
			Assert.AreEqual(7,listAdded.Count,string.Join(", ",listAdded.Select(x => x.GroupName)));
			foreach(PrefName prefName in InsHistCodeGroupsMigration.ListPrefNames) {
				CodeGroup codeGroup=listAdded.Single(x => x.GroupName==_dictLabels[prefName]+InsHistCodeGroupsMigration.GROUP_NAME_SUFFIX);
				string value=M2HistDb.DictPrefsBefore[prefName.ToString()];
				Assert.IsTrue(codeGroup.ShowInHistory,prefName.ToString());
				Assert.AreEqual(EnumCodeGroupFixed.None,codeGroup.CodeGroupFixed,prefName+": never a category's group.");
				Assert.IsFalse(codeGroup.ShowInFrequency || codeGroup.ShowInAgeLimit || codeGroup.ShowInOther || codeGroup.IsHidden,
					prefName+": used only for Insurance History.");
				Assert.IsTrue(ProcCodeRanges.Parse(codeGroup.ProcCodes).Contains(value.Split(',')[0].Trim()),
					prefName+": the group holds the preference's codes ("+codeGroup.ProcCodes+").");
				Assert.AreEqual(_dictLabels[prefName]=="Perio Scaling",codeGroup.IsPerioFourQuads,prefName.ToString());
			}
			int itemOrderMaxBefore=M2HistDb.DictRowsBefore.Values.Max(x => int.Parse(x["ItemOrder"]));
			Assert.IsTrue(listAdded.All(x => x.ItemOrder>itemOrderMaxBefore),"After every group the seed had.");
			Assert.AreEqual(M2HistDb.DictPrefsBefore[PrefName.InsHistBWCodes.ToString()].Split(',')[0].Trim(),
				listAdded.Single(x => x.GroupName=="Bitewing History").HistProcCode);
			Assert.AreEqual(M2HistDb.DictPrefsBefore[PrefName.InsHistPerioURCodes.ToString()].Split(',')[0].Trim(),
				listAdded.Single(x => x.GroupName=="Perio Scaling History").HistProcCode,"The perio group takes its code from the UR preference.");
			Assert.AreEqual(7,M2HistDb.ListCodeGroupsAfter.Count(x => x.ShowInHistory),"Only the added groups show in Insurance History.");
			StringAssert.Contains(M2HistDb.ListChangesStep.Last(),InsHistCodeGroupsMigration.NOTE_EXISTING_UNCHANGED);
		}

		[TestMethod]
		public void Fresh243_EveryCodeGroupOfTheSeedIsUnchanged() {
			//Owner decision of 2026-11-07: the frequency and age limit code groups stay exactly as they were.
			Assert.IsTrue(M2HistDb.DictRowsBefore.Count>0);
			List<string> listProblems=new List<string>();
			foreach(KeyValuePair<long,Dictionary<string,string>> pairBefore in M2HistDb.DictRowsBefore) {
				CodeGroup codeGroupAfter=M2HistDb.ListCodeGroupsAfter.SingleOrDefault(x => x.CodeGroupNum==pairBefore.Key);
				if(codeGroupAfter==null) {
					listProblems.Add(pairBefore.Key+" is gone");
					continue;
				}
				DataTable tableAfter=DataCore.GetTable("SELECT * FROM codegroup WHERE CodeGroupNum="+POut.Long(pairBefore.Key));
				foreach(KeyValuePair<string,string> pairColumn in pairBefore.Value) {
					string valueAfter=tableAfter.Rows[0][pairColumn.Key].ToString();
					if(valueAfter!=pairColumn.Value) {
						listProblems.Add(pairBefore.Key+"."+pairColumn.Key+": '"+pairColumn.Value+"' became '"+valueAfter+"'");
					}
				}
				if(codeGroupAfter.ShowInHistory || codeGroupAfter.HistProcCode!="" || codeGroupAfter.IsPerioFourQuads) {
					listProblems.Add(pairBefore.Key+": the 26.1 history columns are not their defaults");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
		}

		[TestMethod]
		public void Fresh243_TheTenRowsOf24_3StillShow() {
			List<InsHistCategory> listCategories=M2HistDb.ListCategoriesAfter;
			Assert.AreEqual(10,listCategories.Count,string.Join(", ",listCategories.Select(x => x.Description)));
			CollectionAssert.AreEquivalent(new[] { "Perio Scaling History UL","Perio Scaling History UR","Perio Scaling History LL","Perio Scaling History LR" },
				listCategories.Where(x => x.IsQuadrant).Select(x => x.Description).ToList());
			Assert.IsTrue(listCategories.All(x => x.HistProcCode!=""),"Every row has a History Proc Code.");
		}

		[TestMethod]
		public void Fresh243_PreferencesAreKept() {
			CollectionAssert.AreEquivalent(M2HistDb.DictPrefsBefore.ToList(),M2HistDb.DictPrefsAfter.ToList());
		}

		[TestMethod]
		public void RunningTheStepAgain_ChangesNothing() {
			Dictionary<string,string> dictBefore=TestDb.ChecksumAllTables(M2HistDb.DbName,"preference");
			ForkMigrationResult result=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true,
				ListSteps=new List<ForkMigrationStep> { new InsHistCodeGroupsStep() } });
			Assert.AreEqual(1,result.ListStepsRun.Count);
			Assert.AreEqual(0,result.ListChanges.Count,string.Join("\r\n",result.ListChanges));
			CollectionAssert.AreEquivalent(dictBefore.ToList(),TestDb.ChecksumAllTables(M2HistDb.DbName,"preference").ToList());
		}
	}
}
