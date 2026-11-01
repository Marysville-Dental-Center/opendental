// The M2-HIST migration as the fork migration runner runs it on a fresh 24.3.63 database: the ten InsHist*Codes preferences of the seed become
// Insurance History rows from code groups, the preferences are kept, and running the steps again changes nothing.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.FeatureSteps;

namespace M2HISTTests {
	[TestClass]
	public class FreshDatabaseMigrationTests {
		[TestInitialize]
		public void TestInitialize() {
			M2HistDb.Begin();
		}

		private static CodeGroup GetFixedGroup(EnumCodeGroupFixed codeGroupFixed) {
			return M2HistDb.ListCodeGroupsAfter.Where(x => x.CodeGroupFixed==codeGroupFixed).OrderBy(x => x.IsHidden).ThenBy(x => x.ItemOrder).First();
		}

		[TestMethod]
		public void Fresh243_PreferencesAreSetInTheSeed() {
			//The rest of this class relies on the seed having history codes, as an office database does.
			Assert.AreEqual(10,M2HistDb.DictPrefsBefore.Count);
			Assert.IsTrue(M2HistDb.DictPrefsBefore.Values.All(x => x.Trim()!=""),string.Join("; ",M2HistDb.DictPrefsBefore));
		}

		[TestMethod]
		public void Fresh243_EachPreferenceBecameItsFixedGroupsHistoryRow() {
			Assert.IsTrue(M2HistDb.ListChangesStep.Count>0,"The step ran and changed code groups.");
			foreach(PrefName prefName in InsHistCodeGroupsMigration.ListPrefNames) {
				CodeGroup codeGroup=GetFixedGroup(InsHistCategories.GetCodeGroupFixed(prefName));
				string value=M2HistDb.DictPrefsBefore[prefName.ToString()];
				Assert.IsTrue(codeGroup.ShowInHistory,prefName.ToString());
				Assert.IsTrue(ProcCodeRanges.Parse(codeGroup.ProcCodes).Contains(value.Split(',')[0].Trim()),
					prefName+": the group holds the preference's codes ("+codeGroup.ProcCodes+").");
				Assert.AreEqual(codeGroup.CodeGroupFixed==EnumCodeGroupFixed.SRP,codeGroup.IsPerioFourQuads,prefName.ToString());
			}
			Assert.AreEqual(M2HistDb.DictPrefsBefore[PrefName.InsHistBWCodes.ToString()].Split(',')[0].Trim(),GetFixedGroup(EnumCodeGroupFixed.BW).HistProcCode);
			Assert.AreEqual(M2HistDb.DictPrefsBefore[PrefName.InsHistPerioURCodes.ToString()].Split(',')[0].Trim(),
				GetFixedGroup(EnumCodeGroupFixed.SRP).HistProcCode,"The perio group takes its code from the UR preference.");
			//Only the seven history groups changed; the others are as they were.
			List<CodeGroup> listOthers=M2HistDb.ListCodeGroupsAfter.FindAll(x => !x.ShowInHistory);
			Assert.IsTrue(listOthers.Count>0);
			Assert.IsTrue(listOthers.All(x => M2HistDb.DictProcCodesBefore.ContainsKey(x.CodeGroupNum)
				&& M2HistDb.DictProcCodesBefore[x.CodeGroupNum]==x.ProcCodes && x.HistProcCode=="" && !x.IsPerioFourQuads));
			Assert.AreEqual(7,M2HistDb.ListCodeGroupsAfter.Count(x => x.ShowInHistory));
		}

		[TestMethod]
		public void Fresh243_TheTenRowsOf24_3StillShow() {
			List<InsHistCategory> listCategories=M2HistDb.ListCategoriesAfter;
			Assert.AreEqual(10,listCategories.Count,string.Join(", ",listCategories.Select(x => x.Description)));
			CollectionAssert.AreEquivalent(new[] { "Perio Scaling UL","Perio Scaling UR","Perio Scaling LL","Perio Scaling LR" },
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
