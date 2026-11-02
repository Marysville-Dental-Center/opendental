// When the M2-HIST step (InsHistCodeGroupsStep) runs and when it leaves a database alone, on a scratch database with the Open Dental 26.1.70
// schema (the reference embedded in OpenPmsDentalBusiness; the step runs after M2-01's 26.1 schema step) and synthetic rows only (made-up Z codes).
// The database name is unique to the run and it is dropped afterwards.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace M2HISTTests {
	[TestClass]
	public class InsHistMigrationDbTests {
		private static readonly string _dbName=TestDb.ScratchName("m2hist_mig");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.Recreate(_dbName);
			using(MySqlConnection connection=TestDb.OpenServer()) {
				TestDb.Exec(connection,"USE `"+_dbName+"`");
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					TestDb.Exec(connection,table.ToDdl());
				}
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(_dbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(_dbName);
			foreach(string table in new[] { "preference","codegroup","procedurecode" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			foreach(string procCode in new[] { "Z0120","Z0272","Z0274","Z1110","Z4341","Z4342" }) {
				DataCore.NonQ("INSERT INTO procedurecode (ProcCode,Descript,AbbrDesc) VALUES ('"+procCode+"','Synthetic','Syn')");
			}
		}

		private static void AddPref(string prefName,string valueString) {
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(prefName)+"','"+POut.String(valueString)+"')");
		}

		private static void AddGroup(string name,string procCodes,int itemOrder,EnumCodeGroupFixed codeGroupFixed,bool showInHistory=false,
			string histProcCode="")
		{
			DataCore.NonQ("INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit,ShowInFrequency,ShowInOther,"
				+"ShowInHistory,HistProcCode,IsPerioFourQuads) VALUES ('"+name+"','"+procCodes+"',"+itemOrder+","+(int)codeGroupFixed+",0,0,1,0,"
				+(showInHistory ? 1 : 0)+",'"+histProcCode+"',0)");
		}

		///<summary>An office with history codes in four of the ten preferences, and groups like Open Dental 24.x created them (no Exam group).</summary>
		private static void FillSyntheticOffice(string dataBaseVersion) {
			AddPref("DataBaseVersion",dataBaseVersion);
			AddPref("RandomPrimaryKeys","0");
			AddPref(nameof(PrefName.InsHistBWCodes),"Z0272");
			AddPref(nameof(PrefName.InsHistExamCodes),"Z0120");
			AddPref(nameof(PrefName.InsHistPerioURCodes),"Z4341");
			AddPref(nameof(PrefName.InsHistPerioULCodes),"Z4341");
			AddPref(nameof(PrefName.InsHistProphyCodes),"");
			AddGroup("BW","Z0272,Z0274",0,EnumCodeGroupFixed.BW);
			AddGroup("Prophy","Z1110",1,EnumCodeGroupFixed.Prophy);
			AddGroup("SRP","Z4341-Z4342",2,EnumCodeGroupFixed.SRP);
		}

		private static ForkMigrationResult RunStep(bool isRunAll=false) {
			return ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=isRunAll,
				ListSteps=new List<ForkMigrationStep> { new InsHistCodeGroupsStep() } });
		}

		private static string SnapshotCodeGroups() {
			return string.Join(";",DataCore.GetTable("SELECT * FROM codegroup ORDER BY CodeGroupNum").Select().Select(x => string.Join("|",x.ItemArray)));
		}

		private static string SnapshotInsHistPrefs() {
			return string.Join(";",DataCore.GetTable("SELECT PrefName,ValueString FROM preference WHERE PrefName LIKE 'InsHist%' ORDER BY PrefName")
				.Select().Select(x => x[0]+"="+x[1]));
		}

		[TestMethod]
		public void From25_3_MigratesOnce_PreferencesKept() {
			FillSyntheticOffice("25.3.48.0");
			string prefsBefore=SnapshotInsHistPrefs();
			ForkMigrationResult result=RunStep();
			Assert.AreEqual(1,result.ListStepsRun.Count);
			List<CodeGroup> listCodeGroups=OpenPmsDentalBusiness.Crud.CodeGroupCrud.SelectMany("SELECT * FROM codegroup ORDER BY ItemOrder");
			CodeGroup codeGroupBW=listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.BW);
			Assert.IsTrue(codeGroupBW.ShowInHistory);
			Assert.AreEqual("Z0272",codeGroupBW.HistProcCode);
			Assert.IsFalse(listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Prophy).ShowInHistory,"The Prophy preference is blank.");
			CodeGroup codeGroupSRP=listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.SRP);
			Assert.IsTrue(codeGroupSRP.ShowInHistory && codeGroupSRP.IsPerioFourQuads);
			Assert.AreEqual("Z4341",codeGroupSRP.HistProcCode);
			CodeGroup codeGroupExam=listCodeGroups.Single(x => x.CodeGroupFixed==EnumCodeGroupFixed.Exam);
			Assert.AreEqual("Exam",codeGroupExam.GroupName,"Created for the Exam preference.");
			Assert.AreEqual(3,codeGroupExam.ItemOrder);
			Assert.IsTrue(codeGroupExam.ShowInHistory);
			Assert.AreEqual("Z0120",codeGroupExam.ProcCodes);
			Assert.AreEqual("Z0120",codeGroupExam.HistProcCode);
			Assert.IsFalse(codeGroupExam.IsHidden || codeGroupExam.ShowInFrequency || codeGroupExam.ShowInAgeLimit || codeGroupExam.ShowInOther);
			Assert.AreEqual(prefsBefore,SnapshotInsHistPrefs(),"The preferences are kept as they were.");
			Assert.IsTrue(result.ListChanges.Count>0);
			//Again: the runner skips the applied level, and a RunAll rerun finds nothing to do.
			string snapshot=SnapshotCodeGroups();
			Assert.AreEqual(0,RunStep().ListStepsRun.Count);
			ForkMigrationResult resultAll=RunStep(isRunAll:true);
			Assert.AreEqual(1,resultAll.ListStepsRun.Count);
			Assert.AreEqual(0,resultAll.ListChanges.Count,string.Join("\r\n",resultAll.ListChanges));
			Assert.AreEqual(snapshot,SnapshotCodeGroups());
		}

		///<summary>The plan that migration-preview prints and adopt's row check holds the adopted copy to (InsHistCodeGroupsMigration.CreatePlan, read
		///before the step runs) is exactly what the step writes and logs: the planned groups get the planned ProcCodes and history columns and keep every
		///other column, the planned new group is the one added row, every other row is unchanged, and the log is the plan's lines.</summary>
		[TestMethod]
		public void Plan_IsWhatRunDoes() {
			FillSyntheticOffice("25.3.48.0");
			AddPref(nameof(PrefName.InsHistPanoCodes),"Z0330");//Neither a group nor a procedure code: a group is created with the code as written.
			AddPref(nameof(PrefName.InsHistDebridementCodes),"Z4355");
			AddGroup("Debridement (office)","Z4355",3,EnumCodeGroupFixed.None);//Not the fixed group: the category gets its own.
			DataCore.NonQ("UPDATE preference SET ValueString='Z0272,Z0279' WHERE PrefName='"+nameof(PrefName.InsHistBWCodes)+"'");//Z0279 is added to BW.
			List<CodeGroup> listBefore=OpenPmsDentalBusiness.Crud.CodeGroupCrud.SelectMany("SELECT * FROM codegroup ORDER BY CodeGroupNum");
			InsHistCodeGroupsMigration.MigrationPlan plan=InsHistCodeGroupsMigration.CreatePlan(true,false,InsHistCodeGroupsMigration.ReadPrefs(),
				InsHistCodeGroupsMigration.ReadCodeGroups(),InsHistCodeGroupsMigration.ReadProcCodes());
			Assert.IsTrue(plan.IsRun);
			Assert.AreEqual(2,plan.ListUpdated.Count,"BW and SRP.");
			CollectionAssert.AreEqual(new[] { "Pano/FMX","Exam","Full Mouth Debridement" },plan.ListAdded.Select(x => x.GroupName).ToArray());
			ForkMigrationResult result=RunStep();
			string prefix=new InsHistCodeGroupsStep().Level+": ";
			CollectionAssert.AreEqual(plan.GetLogLines(),result.ListChanges.Where(x => x.StartsWith(prefix)).Select(x => x.Substring(prefix.Length)).ToList(),
				string.Join("\r\n",result.ListChanges));
			StringAssert.Contains(plan.GetLogLines()[0],"Code group 'BW' (CodeGroupNum ");
			StringAssert.Contains(plan.GetLogLines()[0],") shown in Insurance History, History Proc Code Z0272, codes added: Z0279");
			List<CodeGroup> listAfter=OpenPmsDentalBusiness.Crud.CodeGroupCrud.SelectMany("SELECT * FROM codegroup ORDER BY CodeGroupNum");
			foreach(CodeGroup codeGroupBefore in listBefore) {
				CodeGroup codeGroupAfter=listAfter.Single(x => x.CodeGroupNum==codeGroupBefore.CodeGroupNum);
				CodeGroup codeGroupPlanned=plan.ListUpdated.FirstOrDefault(x => x.CodeGroupNum==codeGroupBefore.CodeGroupNum)??codeGroupBefore;
				Assert.AreEqual(codeGroupPlanned.ProcCodes,codeGroupAfter.ProcCodes,codeGroupBefore.GroupName);
				Assert.AreEqual(codeGroupPlanned.ShowInHistory,codeGroupAfter.ShowInHistory,codeGroupBefore.GroupName);
				Assert.AreEqual(codeGroupPlanned.HistProcCode,codeGroupAfter.HistProcCode,codeGroupBefore.GroupName);
				Assert.AreEqual(codeGroupPlanned.IsPerioFourQuads,codeGroupAfter.IsPerioFourQuads,codeGroupBefore.GroupName);
				Assert.AreEqual(codeGroupBefore.GroupName,codeGroupAfter.GroupName);
				Assert.AreEqual(codeGroupBefore.ItemOrder,codeGroupAfter.ItemOrder);
				Assert.AreEqual(codeGroupBefore.CodeGroupFixed,codeGroupAfter.CodeGroupFixed);
				Assert.AreEqual(codeGroupBefore.IsHidden,codeGroupAfter.IsHidden);
				Assert.AreEqual(codeGroupBefore.ShowInFrequency,codeGroupAfter.ShowInFrequency);
			}
			List<CodeGroup> listAdded=listAfter.Where(x => !listBefore.Any(y => y.CodeGroupNum==x.CodeGroupNum)).OrderBy(x => x.CodeGroupNum).ToList();
			Assert.AreEqual(plan.ListAdded.Count,listAdded.Count);
			for(int i=0;i<listAdded.Count;i++) {
				CodeGroup codeGroupPlanned=plan.ListAdded[i];
				Assert.AreEqual(codeGroupPlanned.GroupName,listAdded[i].GroupName);
				Assert.AreEqual(codeGroupPlanned.ProcCodes,listAdded[i].ProcCodes);
				Assert.AreEqual(codeGroupPlanned.ItemOrder,listAdded[i].ItemOrder);
				Assert.AreEqual(codeGroupPlanned.CodeGroupFixed,listAdded[i].CodeGroupFixed);
				Assert.AreEqual(codeGroupPlanned.HistProcCode,listAdded[i].HistProcCode);
				Assert.IsTrue(listAdded[i].ShowInHistory && !listAdded[i].IsHidden && !listAdded[i].ShowInFrequency && !listAdded[i].ShowInAgeLimit && !listAdded[i].ShowInOther);
			}
			Assert.AreEqual("Z0330",plan.ListAdded[0].HistProcCode,"Not in the procedure code list: the code as written, so Code Group Edit asks for a valid one.");
			//After the step the plan says why it changes nothing.
			InsHistCodeGroupsMigration.MigrationPlan planAfter=InsHistCodeGroupsMigration.CreatePlan(true,true,InsHistCodeGroupsMigration.ReadPrefs(),
				InsHistCodeGroupsMigration.ReadCodeGroups(),InsHistCodeGroupsMigration.ReadProcCodes());
			Assert.AreEqual("it already ran on this database",planAfter.GetReasonUnchanged());
			Assert.AreEqual(0,planAfter.ListCodeGroupsChanged.Count);
		}

		[TestMethod]
		public void From26_1_NothingChanges() {
			FillSyntheticOffice("26.1.70.0");
			string snapshot=SnapshotCodeGroups();
			ForkMigrationResult result=RunStep();
			Assert.AreEqual(1,result.ListStepsRun.Count);
			Assert.AreEqual(0,result.ListChanges.Count,string.Join("\r\n",result.ListChanges));
			Assert.AreEqual(snapshot,SnapshotCodeGroups(),"Open Dental 26.1 already converted its history rows.");
		}

		[TestMethod]
		public void HistoryRowsAlreadySetUp_NothingChanges() {
			FillSyntheticOffice("25.3.48.0");
			AddGroup("Fluoride","Z1206",3,EnumCodeGroupFixed.Fluoride,showInHistory:true,histProcCode:"Z1206");
			string snapshot=SnapshotCodeGroups();
			ForkMigrationResult result=RunStep();
			Assert.AreEqual(0,result.ListChanges.Count,string.Join("\r\n",result.ListChanges));
			Assert.AreEqual(snapshot,SnapshotCodeGroups());
		}

		[TestMethod]
		public void NoHistoryPreferences_NothingChanges() {
			AddPref("DataBaseVersion","24.3.63.0");
			AddGroup("BW","Z0272",0,EnumCodeGroupFixed.BW);
			string snapshot=SnapshotCodeGroups();
			ForkMigrationResult result=RunStep();
			Assert.AreEqual(0,result.ListChanges.Count,string.Join("\r\n",result.ListChanges));
			Assert.AreEqual(snapshot,SnapshotCodeGroups());
		}
	}
}
