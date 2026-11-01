// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SyntheticData.Generation;
using SyntheticData.Infrastructure;
using SyntheticData.Validation;
using DisplayReportNames=OpenDentBusiness.DisplayReports.ReportNames;
using SchemaVerifier=OpenDentBusiness.ForkMigrations.SchemaVerifier;
using Seed26_1=OpenDentBusiness.ForkMigrations.Seeds26_1.Seed26_1;

namespace AdoptionValidation.Tests {
	///<summary>Runs the whole M1-Z rehearsal (small preset, two runs) on scratch databases synth_m1z_t&lt;time&gt;_&lt;pid&gt;_* on the local server, keeps
	///them for the checks below and drops them afterwards. Inconclusive without a local server or a 25.3.48 schema source.</summary>
	[TestClass]
	public class RehearsalIntegrationTests {
		///<summary>Unique to this run, so concurrent runs from other worktrees never collide.</summary>
		private static readonly string Tag="t"+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+(Process.GetCurrentProcess().Id%100000).ToString(CultureInfo.InvariantCulture);
		private static readonly ServerSettings _server=new ServerSettings();
		private static string _skipReason;
		private static string _outDir;
		private static RehearsalResult _result;

		///<summary>Every check a run must report, in order.</summary>
		public static readonly string[] ArrayCheckIds={ "G1","G2","A1","A2","A3","A4","M1","M2","M3","M4","M5","M6","H1","V1","V2","V3","V4","V5","V6","V7","V8","V9","V10",
			"V11","R1","R2","I1","I2","I3","I4" };

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(!Db.CanConnect(_server,out string error)) {
				_skipReason="No local MariaDB/MySQL server: "+error;
				return;
			}
			using(Db db=Db.Open(_server)) {
				if(!File.Exists(SchemaBuilder.DefaultSchemaSqlPath) && !db.DatabaseExists(SchemaBuilder.DefaultReferenceDatabase)) {
					_skipReason="Neither the 25.3.48 schema dump nor the od_253_schema database is available.";
					return;
				}
			}
			DropStaleDatabases();
			_outDir=Path.Combine(Path.GetTempPath(),"AdoptionValidationTests",Tag);
			RehearsalOptions options=new RehearsalOptions { Size=DataSize.Small,Runs=2,Tag=Tag,OutDir=_outDir,Keep=true };
			_result=new Rehearsal(options,null).Run();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			using(Db db=Db.Open(_server)) {
				foreach(string database in TaggedDatabases(db).Where(x => x.StartsWith(Rehearsal.DbPrefix+Tag+"_"))) {
					db.Execute("DROP DATABASE IF EXISTS `"+database+"`");
				}
			}
			try {
				Directory.Delete(_outDir,true);
			}
			catch(Exception) {
				//A temp folder left behind is harmless.
			}
		}

		private static List<string> TaggedDatabases(Db db) {
			return db.Rows("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'synth\\_m1z\\_t%'")
				.Select(x => Convert.ToString(x[0])).ToList();
		}

		///<summary>Drops this fixture's databases that runs which crashed left behind, once they are 12 hours old.</summary>
		private static void DropStaleDatabases() {
			using(Db db=Db.Open(_server)) {
				foreach(string database in TaggedDatabases(db)) {
					Match match=Regex.Match(database,@"^synth_m1z_t(\d{12})_\d+_[a-z0-9_]+$");
					if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
						DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted) || DateTime.UtcNow-dateStarted<TimeSpan.FromHours(12))
					{
						continue;
					}
					db.Execute("DROP DATABASE IF EXISTS `"+database+"`");
				}
			}
		}

		private static void SkipIfNeeded() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
		}

		private static string Source(int run) {
			return Rehearsal.DbPrefix+Tag+"_r"+run+"_src";
		}

		private static string Adopted(int run) {
			return Rehearsal.DbPrefix+Tag+"_r"+run+"_adopted";
		}

		[TestMethod]
		public void Rehearsal_PassesTwiceInARow() {
			SkipIfNeeded();
			Assert.AreEqual(2,_result.ListRuns.Count);
			Assert.IsTrue(_result.Passed,_result.ToMarkdown());
			CollectionAssert.AreEqual(new[] { "X1","X2","X3" },_result.ListCrossRunChecks.Select(x => x.Id).ToArray());
		}

		[TestMethod]
		public void EveryRun_ReportsEveryCheckOfTheGate() {
			SkipIfNeeded();
			foreach(RunResult run in _result.ListRuns) {
				Assert.IsNull(run.Error,run.Error);
				CollectionAssert.AreEqual(ArrayCheckIds,run.ListChecks.Select(x => x.Id).ToArray(),"Run "+run.RunNumber);
			}
		}

		[TestMethod]
		public void EveryStep_IsTimed() {
			SkipIfNeeded();
			foreach(RunResult run in _result.ListRuns) {
				List<string> listNames=run.ListSteps.Select(x => x.Name).ToList();
				foreach(string prefix in new[] { "Generate","Record baseline","FLOAT values","Copy on the server (AdoptionValidation copy)","Validate the copy",
					"Adopt (","Fork migration","Schema verifier","Rehash","Row check against the original","Record post-adoption","Data-layer round trip","Second pass" })
				{
					Assert.IsTrue(listNames.Any(x => x.StartsWith(prefix)),"Run "+run.RunNumber+" has no step "+prefix);
				}
				Assert.IsFalse(listNames.Contains("Back up (mariadb-dump)"),"By default the rehearsal copies the way the runbook does, without a dump.");
				Assert.IsTrue(run.ListSteps.All(x => x.Seconds>=0) && run.TotalSeconds>0);
				StringAssert.Contains(run.FloatCheck,"no value that a logical backup (mariadb-dump) would round","The synthetic FLOAT values are short.");
			}
		}

		[TestMethod]
		public void Adoption_DropsTheOfficeColumn_RemovesUpdateFiles_SetsSentinels() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server,Adopted(1))) {
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='patient' "
					+"AND COLUMN_NAME='_portal_active_email'"));
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='patient' "
					+"AND INDEX_NAME='portal_active_email_uniq'"));
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType IN (0,1)"));
				Assert.AreEqual(1,db.ScalarLong("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType=2"));
				Assert.AreEqual("99.0.0.0",Convert.ToString(db.Scalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'")));
				Assert.AreEqual("99.0.0.0",Convert.ToString(db.Scalar("SELECT ValueString FROM preference WHERE PrefName='ProgramVersion'")));
				Assert.AreEqual(4,db.ScalarLong("SELECT COUNT(*) FROM updatehistory"),"Open Dental's version history is kept.");
			}
			using(Db db=Db.Open(_server,Source(1))) {
				Assert.AreEqual(4,db.ScalarLong("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType IN (0,1)"),"The source keeps them: adoption ran on the copy.");
			}
		}

		///<summary>Runbook step 0.2's migration-preview lists, read-only, the report rows adoption's fork migration renames on an Open Dental 25.3.48
		///database: the office fixture's three rows under names this program does not know, and nothing on the adopted copy.</summary>
		[TestMethod]
		public void MigrationPreview_ListsTheReportRowsAdoptionRenames_AndWritesNothing() {
			SkipIfNeeded();
			ValidationReport before;
			using(Db db=Db.Open(_server)) {
				before=Validator.Run(db,Source(2),null,null);
			}
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "migration-preview","--database",Source(2) },listLog.Add),string.Join("\r\n",listLog));
			string output=string.Join("\r\n",listLog);
			foreach((string internalName,string description,int category,string nameFork) report in OfficeFixture.ArrayReportRowsRenamed) {
				StringAssert.Contains(output,"Renamed displayreport "+report.internalName+" to "+report.nameFork+" (same description)");
			}
			StringAssert.Contains(output,"Adoption's fork migration will rename 3 displayreport row(s), changing only their InternalName");
			Assert.IsFalse(output.Contains("ODSyntheticFutureReport") || output.Contains("ODToday"),"Rows of other reports are not listed.\r\n"+output);
			//The Open Dental 25.4 and 26.1 steps: the schema to 26.1.70, the fixture's NULL note, every 25.4 and 26.1 preference, the permission split.
			StringAssert.Contains(output,"bring the schema to Open Dental "+SchemaVerifier.ReferenceVersion+": 5 new table(s), empty (");
			StringAssert.Contains(output,"1 NULL note(s) become empty");
			StringAssert.Contains(output,"inserts "+Seed26_1.ListPrefDefaults.Count+" preference(s) with their defaults: ");
			StringAssert.Contains(output,"moves 1 Open Dental 25.3 PhoneExtension grant(s) from 272 to 274, then grants BlockoutAdd (272) to 2 user group(s) and "
				+"grants BlockoutDelete (273) to 2 user group(s)");
			//The UDS+ report row step (M2-UDS): the 25.4 report's row after the fixture's sealant measure, whose one grant it copies.
			StringAssert.Contains(output,"Step 2026110103 (UDS+ report row) adds the displayreport row "+DisplayReportNames.UdsPlus+" (\"FQHC UDS+ Dental Services\", "
				+"Public Health, ItemOrder ");
			StringAssert.Contains(output,") right after the FQHC Dental Sealant Measure (DisplayReportNum ");
			StringAssert.Contains(output,"it gives the 1 user group(s) that may run the sealant measure (user group ");
			//The Insurance History step (M2-HIST): the fixture's ten InsHist*Codes preferences into six generated code groups and one new group.
			StringAssert.Contains(output,"Step 2026110104 (Insurance History rows from code groups) marks "+OfficeFixture.CountInsHistGroupsMarked+" code group(s) Show In "
				+"History and adds "+OfficeFixture.CountInsHistGroupsAdded+" (this database comes from before Open Dental 25.4;");
			StringAssert.Contains(output,"Code group 'BWs' (CodeGroupNum ");
			StringAssert.Contains(output,") shown in Insurance History with four quadrant rows, History Proc Code Z4341");
			StringAssert.Contains(output,"History Proc Code Z4910, codes added: "+OfficeFixture.INSHIST_CODE_ADDED);
			StringAssert.Contains(output,"Added code group '"+OfficeFixture.INSHIST_GROUP_ADDED+"' (FMDebride, ItemOrder ");
			using(Db db=Db.Open(_server)) {
				List<Difference> listDiffs=ReportComparer.Compare(before,Validator.Run(db,Source(2),null,null),null);
				Assert.AreEqual(0,listDiffs.Count,"migration-preview only reads.\r\n"+ReportComparer.Format(listDiffs));
				MigrationPreview.Result resultAdopted=MigrationPreview.Run(db,Adopted(2));
				Assert.AreEqual(0,resultAdopted.ListRenames.Count,string.Join("\r\n",resultAdopted.Lines()));
				Assert.IsTrue(resultAdopted.ListPlans.All(x => x.RowExisting!=null),"After adoption every report has this program's name.");
				Assert.AreEqual(0,resultAdopted.Plan26_1.ListPrefsInserted.Count,"After adoption every 25.4 and 26.1 preference is there.");
				Assert.IsTrue(resultAdopted.Plan26_1.IsSeedStepApplied && !resultAdopted.Plan26_1.IsPermissionSplitPlanned,"The split never runs twice.");
				Assert.AreEqual(0,resultAdopted.Plan26_1.CountClaimPaymentNotesNull);
				Assert.IsNotNull(resultAdopted.PlanUdsPlus.Rows.RowExisting,"After adoption the UDS+ report has its row; the step adds nothing twice.");
				Assert.IsFalse(resultAdopted.PlanUdsPlus.Rows.IsAdded);
				Assert.IsFalse(resultAdopted.PlanInsHist.IsChanging,"The Insurance History step never runs twice.");
				Assert.AreEqual("it already ran on this database",resultAdopted.PlanInsHist.Migration.GetReasonUnchanged());
			}
		}

		///<summary>On the adopted copy the Insurance History step (M2-HIST) marked the generated code group of each of the fixture's history categories
		///Show In History, with the first code of its preference as History Proc Code (Z4341, from UR, for the SRP group, with four quadrant rows),
		///appended the perio maintenance code its group lacked, and added a Full Mouth Debridement group after every other group, because the fixture
		///made the generated debridement group the office's own. Every other codegroup row and column is unchanged, and the ten preferences are kept.
		///</summary>
		[TestMethod]
		public void Adoption_MovesTheInsHistPreferencesIntoCodeGroups() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				RowComparer.TableDiff diff=RowComparer.Compare(db,Source(1),Adopted(1),"codegroup");
				Assert.AreEqual(0,diff.CountOnlyFirst,diff.Describe());
				Assert.AreEqual(OfficeFixture.CountInsHistGroupsAdded,diff.CountOnlySecond,diff.Describe());
				CollectionAssert.AreEquivalent(new[] { "ShowInHistory","HistProcCode","IsPerioFourQuads" },diff.ListColumnsInOneOnly,"The 26.1 columns.");
				//Only the perio maintenance group's codes changed among the shared columns.
				Assert.AreEqual(1,diff.CountChanged,diff.Describe());
				Assert.IsTrue(diff.ListChanged[0].ListColumns.SequenceEqual(new[] { "ProcCodes" }),diff.Describe());
				Assert.AreEqual("Z4910,"+OfficeFixture.INSHIST_CODE_ADDED,Convert.ToString(db.Scalar("SELECT ProcCodes FROM `"+Adopted(1)+"`.codegroup WHERE CodeGroupNum="
					+diff.ListChanged[0].Key)));
				string marked=Convert.ToString(db.Scalar("SELECT GROUP_CONCAT(CONCAT(GroupName,'=',HistProcCode,IF(IsPerioFourQuads=1,'/4','')) ORDER BY ItemOrder SEPARATOR '; ') "
					+"FROM `"+Adopted(1)+"`.codegroup WHERE ShowInHistory=1"));
				Assert.AreEqual("BWs=Z0272; Pano/FMX=Z0330; Exams=Z0120; Perio Maint=Z4910; Prophy=Z1110; SRP=Z4341/4; "+OfficeFixture.INSHIST_GROUP_ADDED+"=Z4355",marked);
				object[] rowAdded=db.Rows("SELECT GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInFrequency,ShowInAgeLimit,ShowInOther FROM `"+Adopted(1)
					+"`.codegroup WHERE CodeGroupNum="+diff.ListOnlySecond[0]).Single();
				Assert.AreEqual(OfficeFixture.INSHIST_GROUP_ADDED,Convert.ToString(rowAdded[0]));
				Assert.AreEqual("Z4355",Convert.ToString(rowAdded[1]));
				Assert.AreEqual(db.ScalarLong("SELECT MAX(ItemOrder) FROM `"+Source(1)+"`.codegroup")+1,Convert.ToInt64(rowAdded[2]),"After every other group.");
				Assert.AreEqual((int)OpenDentBusiness.EnumCodeGroupFixed.FMDebride,Convert.ToInt32(rowAdded[3]));
				Assert.IsTrue(rowAdded.Skip(4).All(x => Convert.ToInt32(x)==0),"In no benefit grid, not hidden.");
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM `"+Adopted(1)+"`.codegroup WHERE ShowInHistory=0 AND (HistProcCode<>'' OR IsPerioFourQuads<>0)"));
				foreach((string prefName,string value) pref in OfficeFixture.ArrayInsHistPrefs) {
					Assert.AreEqual(pref.value,Convert.ToString(db.Scalar("SELECT ValueString FROM `"+Adopted(1)+"`.preference WHERE PrefName='"+pref.prefName+"'")),
						pref.prefName+" is kept.");
				}
			}
			RunResult run=_result.ListRuns[0];
			Assert.IsTrue(run.ListAllowedDifferences.Any(x => x.StartsWith("RowsChanged codegroup")),string.Join("\r\n",run.ListAllowedDifferences));
			Assert.IsTrue(run.ListAllowedDifferences.Any(x => x.StartsWith("ColumnChanged codegroup.ProcCodes")),string.Join("\r\n",run.ListAllowedDifferences));
			Assert.IsFalse(run.ListUnexpectedDifferences.Any(x => x.Contains("codegroup")),string.Join("\r\n",run.ListUnexpectedDifferences));
			StringAssert.Contains(run.ListChecks.Single(x => x.Id=="M6").Detail,"codegroup: "+OfficeFixture.CountInsHistGroupsMarked+" of the "
				+OfficeFixture.CountInsHistGroupsMarked+" code group(s) migration-preview lists marked Show In History as planned");
		}

		///<summary>Negative control for codegroup: the report comparison allows it as a whole table (the group the Insurance History step adds changes
		///every column checksum); adopt's row check lets through only what the step's plan for the original says.</summary>
		[TestMethod]
		public void RowCheck_CatchesCodeGroupChangesTheWholeTableRuleAllows() {
			SkipIfNeeded();
			string tampered=Rehearsal.DbPrefix+Tag+"_tamperedcg";
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Adopted(1),"--database",tampered },listLog.Add),string.Join("\r\n",listLog));
			using(Db db=Db.Open(_server)) {
				Assert.IsTrue(AdoptionRowCheck.Run(db,Source(1),tampered).Passed,"An untouched adopted copy passes.");
				ValidationReport before=Validator.Run(db,tampered,null,null);
				db.Use(tampered);
				long numBW=db.ScalarLong("SELECT CodeGroupNum FROM codegroup WHERE GroupName='BWs'");
				long numCrowns=db.ScalarLong("SELECT CodeGroupNum FROM codegroup WHERE GroupName='Crowns'");
				long numDentures=db.ScalarLong("SELECT CodeGroupNum FROM codegroup WHERE GroupName='Dentures'");
				long numProphy=db.ScalarLong("SELECT CodeGroupNum FROM codegroup WHERE GroupName='Prophy'");
				//A planned group with another History Proc Code, a planned group renamed, a group the step does not touch marked Show In History, another
				//one with other codes, and a group the step does not add.
				Assert.AreEqual(1,db.Execute("UPDATE codegroup SET HistProcCode='Z0274' WHERE CodeGroupNum="+numBW));
				Assert.AreEqual(1,db.Execute("UPDATE codegroup SET GroupName='Prophy renamed' WHERE CodeGroupNum="+numProphy));
				Assert.AreEqual(1,db.Execute("UPDATE codegroup SET ShowInHistory=1,HistProcCode='Z5110' WHERE CodeGroupNum="+numDentures));
				Assert.AreEqual(1,db.Execute("UPDATE codegroup SET ProcCodes=CONCAT(ProcCodes,',Z2750') WHERE CodeGroupNum="+numCrowns));
				Assert.AreEqual(1,db.Execute("INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit,ShowInFrequency,ShowInOther,"
					+"ShowInHistory,HistProcCode,IsPerioFourQuads) VALUES ('Sealants (synthetic)','Z1351',40,0,0,0,0,0,1,'Z1351',0)"));
				AdoptionRowCheck.Result result=AdoptionRowCheck.Run(db,Source(1),tampered);
				string failures=string.Join("\r\n",result.ListFailures);
				Assert.IsFalse(result.Passed);
				StringAssert.Contains(failures,"codegroup row "+numBW+" ('BWs') holds HistProcCode 'Z0274' (planned: 'Z0272')");
				StringAssert.Contains(failures,"codegroup row "+numProphy+" ('Prophy'): GroupName changed");
				StringAssert.Contains(failures,"codegroup row "+numCrowns+" changed (ProcCodes); the Insurance History step changes only the code groups migration-preview lists");
				StringAssert.Contains(failures,"1 row(s) of codegroup hold a value other than the default (0) in the new column ShowInHistory.");
				StringAssert.Contains(failures,"1 row(s) of codegroup hold a value other than the default ('') in the new column HistProcCode.");
				StringAssert.Contains(failures,"('Sealants (synthetic)') was added; the Insurance History step adds only the code group(s) migration-preview lists");
				Assert.AreEqual(OfficeFixture.CountInsHistGroupsAdded,result.ListCodeGroupNumsAdded.Count,"The step's own group is still found.");
				//The report comparison under allowed-differences.adoption.json lets every one of these through.
				AllowedDifferences allowed=AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..",
					"allowed-differences.adoption.json"));
				List<Difference> listDiffs=ReportComparer.Compare(before,Validator.Run(db,tampered,null,null),allowed);
				Assert.IsTrue(listDiffs.Any(x => x.Table=="codegroup"),ReportComparer.Format(listDiffs));
				Assert.IsTrue(listDiffs.Where(x => x.Table=="codegroup").All(x => x.IsAllowed),ReportComparer.Format(listDiffs));
			}
		}

		///<summary>On the adopted copy the fixture's report rows under unknown names carry this program's names, with the same number, description,
		///category and order. The UDS+ report row step (M2-UDS) added one row, the 25.4 UDS+ report's, right after the fixture's FQHC Dental Sealant
		///Measure, moved the later Public Health rows down one place (ItemOrder only) and gave the sealant measure's group the Reports permission for it;
		///every other report row, and every other column, is unchanged.</summary>
		[TestMethod]
		public void Adoption_RenamesOnlyTheReportRowsUnderUnknownNames_AndAddsTheUdsPlusRow() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				RowComparer.TableDiff diff=RowComparer.Compare(db,Source(1),Adopted(1),"displayreport");
				Assert.AreEqual(0,diff.CountOnlyFirst,diff.Describe());
				Assert.AreEqual(1,diff.CountOnlySecond,"Only the UDS+ row is added. "+diff.Describe());
				List<long> listRenamed=OfficeFixture.ArrayReportRowsRenamed.Select(x => db.ScalarLong("SELECT DisplayReportNum FROM `"+Source(1)+"`.displayreport "
					+"WHERE InternalName='"+x.internalName+"'")).ToList();
				foreach((string internalName,string description,int category,string nameFork) report in OfficeFixture.ArrayReportRowsRenamed) {
					long num=db.ScalarLong("SELECT DisplayReportNum FROM `"+Source(1)+"`.displayreport WHERE InternalName='"+report.internalName+"'");
					Assert.AreEqual(report.nameFork,Convert.ToString(db.Scalar("SELECT InternalName FROM `"+Adopted(1)+"`.displayreport WHERE DisplayReportNum="+num)));
				}
				foreach((string internalName,string description,int category) report in OfficeFixture.ArrayReportRowsKept) {
					Assert.AreEqual(1,db.ScalarLong("SELECT COUNT(*) FROM `"+Adopted(1)+"`.displayreport WHERE InternalName='"+report.internalName+"'"),report.internalName);
				}
				Assert.IsTrue(diff.ListChanged.Where(x => listRenamed.Contains(x.Key)).All(x => x.ListColumns.SequenceEqual(new[] { "InternalName" })),diff.Describe());
				Assert.AreEqual(listRenamed.Count,diff.ListChanged.Count(x => listRenamed.Contains(x.Key)),diff.Describe());
				//The other changed rows are the Public Health rows after the sealant measure, each one place down.
				long itemOrderSealant=db.ScalarLong("SELECT ItemOrder FROM `"+Source(1)+"`.displayreport WHERE InternalName='"+OfficeFixture.ArrayReportRowsPublicHealth[0].internalName+"'");
				List<long> listMoved=db.Rows("SELECT DisplayReportNum FROM `"+Source(1)+"`.displayreport WHERE Category="+OfficeFixture.CategoryPublicHealth+" AND ItemOrder>"
					+itemOrderSealant).Select(x => Convert.ToInt64(x[0])).ToList();
				CollectionAssert.AreEquivalent(listMoved,diff.ListChanged.Where(x => !listRenamed.Contains(x.Key)).Select(x => x.Key).ToList(),diff.Describe());
				Assert.IsTrue(diff.ListChanged.Where(x => !listRenamed.Contains(x.Key)).All(x => x.ListColumns.SequenceEqual(new[] { "ItemOrder" })),diff.Describe());
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM `"+Source(1)+"`.displayreport a INNER JOIN `"+Adopted(1)+"`.displayreport b USING(DisplayReportNum) "
					+"WHERE a.Category="+OfficeFixture.CategoryPublicHealth+" AND a.ItemOrder>"+itemOrderSealant+" AND b.ItemOrder<>a.ItemOrder+1"));
				Assert.IsTrue(listMoved.Contains(db.ScalarLong("SELECT DisplayReportNum FROM `"+Source(1)+"`.displayreport WHERE InternalName='"
					+OfficeFixture.ArrayReportRowsPublicHealth[1].internalName+"'")),"The fixture's later Public Health row moves.");
				object[] rowUds=db.Rows("SELECT DisplayReportNum,ItemOrder,Description,Category,IsHidden FROM `"+Adopted(1)+"`.displayreport WHERE InternalName='"
					+DisplayReportNames.UdsPlus+"'").Single();
				Assert.AreEqual(diff.ListOnlySecond[0],Convert.ToInt64(rowUds[0]));
				Assert.AreEqual(itemOrderSealant+1,Convert.ToInt64(rowUds[1]),"Right after the sealant measure.");
				Assert.AreEqual("FQHC UDS+ Dental Services",Convert.ToString(rowUds[2]));
				Assert.AreEqual(OfficeFixture.CategoryPublicHealth,Convert.ToInt32(rowUds[3]));
				Assert.AreEqual(0,Convert.ToInt32(rowUds[4]));
				long groupClinical=db.ScalarLong("SELECT UserGroupNum FROM `"+Adopted(1)+"`.usergroup WHERE Description='"+OfficeFixture.ArrayGroupsFixture[2]+"' "
					+"ORDER BY UserGroupNum LIMIT 1");
				Assert.AreEqual(groupClinical.ToString(CultureInfo.InvariantCulture),Convert.ToString(db.Scalar("SELECT GROUP_CONCAT(UserGroupNum) FROM `"+Adopted(1)
					+"`.grouppermission WHERE PermType="+OfficeFixture.PermReports+" AND FKey="+rowUds[0])),"The sealant measure's one group may run UDS+.");
			}
			RunResult run=_result.ListRuns[0];
			Assert.IsTrue(run.ListAllowedDifferences.Any(x => x.StartsWith("ColumnChanged displayreport.InternalName")),string.Join("\r\n",run.ListAllowedDifferences));
			Assert.IsTrue(run.ListAllowedDifferences.Any(x => x.StartsWith("RowsChanged displayreport")),string.Join("\r\n",run.ListAllowedDifferences));
			Assert.IsFalse(run.ListUnexpectedDifferences.Any(x => x.Contains("displayreport")),string.Join("\r\n",run.ListUnexpectedDifferences));
			StringAssert.Contains(run.ListChecks.Single(x => x.Id=="M3").Detail,"3 rename(s)");
			StringAssert.Contains(run.ListChecks.Single(x => x.Id=="M5").Detail,"the UDS+ report row added as migration-preview lists");
		}

		///<summary>The Open Dental 25.4 and 26.1 steps on the office fixture's rows: the 25.3 PhoneExtension grant (272) is now 274 under the same key;
		///each Blockouts group got one BlockoutAdd and one BlockoutDelete with its date limit, the duplicated Blockouts row no second one; the NULL claim
		///payment note is empty and the payment's SecDateTEdit did not move; every 25.4 and 26.1 preference holds its default.</summary>
		[TestMethod]
		public void Adoption_ConvertsTheFixtureRowsThe26_1StepsConvert() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				long groupPermNum=db.ScalarLong("SELECT GroupPermNum FROM `"+Source(1)+"`.grouppermission WHERE PermType="+OfficeFixture.PermPhoneExtension25_3);
				Assert.IsTrue(groupPermNum>0,"The fixture's PhoneExtension grant.");
				Assert.AreEqual(274,db.ScalarLong("SELECT PermType FROM `"+Adopted(1)+"`.grouppermission WHERE GroupPermNum="+groupPermNum));
				foreach(int permType in new[] { 272,273 }) {
					Assert.AreEqual("0:1,30:1",Convert.ToString(db.Scalar("SELECT GROUP_CONCAT(CONCAT(NewerDays,':',n) ORDER BY NewerDays) FROM (SELECT g.NewerDays,COUNT(*) n "
						+"FROM `"+Adopted(1)+"`.grouppermission g INNER JOIN `"+Adopted(1)+"`.usergroup u ON u.UserGroupNum=g.UserGroupNum WHERE g.PermType="+permType
						+" AND u.Description IN ('"+OfficeFixture.ArrayGroupsFixture[1]+"','"+OfficeFixture.ArrayGroupsFixture[2]+"') GROUP BY g.UserGroupNum,g.NewerDays) t")),
						"PermType "+permType+": one grant per Blockouts group, with its date limit");
					Assert.AreEqual(2,db.ScalarLong("SELECT COUNT(*) FROM `"+Adopted(1)+"`.grouppermission WHERE PermType="+permType),"PermType "+permType);
				}
				long claimPaymentNum=db.ScalarLong("SELECT ClaimPaymentNum FROM `"+Source(1)+"`.claimpayment WHERE Note IS NULL");
				Assert.IsTrue(claimPaymentNum>0,"The fixture's NULL note.");
				Assert.AreEqual(1,db.ScalarLong("SELECT COUNT(*) FROM `"+Source(1)+"`.claimpayment a INNER JOIN `"+Adopted(1)+"`.claimpayment b USING(ClaimPaymentNum) "
					+"WHERE a.ClaimPaymentNum="+claimPaymentNum+" AND b.Note='' AND b.Note IS NOT NULL AND a.SecDateTEdit=b.SecDateTEdit"),
					"The note is empty and the payment's audit timestamp did not move.");
				foreach((OpenDentBusiness.PrefName prefName,string valueString) pref in Seed26_1.ListPrefDefaults) {
					Assert.AreEqual(pref.valueString,Convert.ToString(db.Scalar("SELECT ValueString FROM `"+Adopted(1)+"`.preference WHERE PrefName='"+pref.prefName+"'")),
						pref.prefName.ToString());
				}
			}
		}

		///<summary>Negative control for the 26.1 rules: the report comparison allows grouppermission as a whole table, the new tables and columns without
		///their values, and claimpayment.Note and DateLastAssigned as whole columns; adopt's row check does not let a change through that the 25.4 and 26.1
		///steps do not make.</summary>
		[TestMethod]
		public void RowCheck_CatchesChangesThe26_1RulesAllow() {
			SkipIfNeeded();
			string tampered=Rehearsal.DbPrefix+Tag+"_tampered261";
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Adopted(1),"--database",tampered },listLog.Add),string.Join("\r\n",listLog));
			using(Db db=Db.Open(_server)) {
				Assert.IsTrue(AdoptionRowCheck.Run(db,Source(1),tampered).Passed,"An untouched adopted copy passes.");
				db.Use(tampered);
				long groupAdmin=db.ScalarLong("SELECT UserGroupNum FROM usergroup WHERE Description='"+OfficeFixture.ArrayGroupsFixture[0]+"'");
				Assert.AreEqual(1,db.Execute("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES ('0001-01-01',0,"+groupAdmin+",272,0)"));
				Assert.AreEqual(1,db.Execute("UPDATE grouppermission SET NewerDays=7 WHERE PermType=273 AND NewerDays=30"));
				Assert.AreEqual(1,db.Execute("UPDATE grouppermission SET NewerDays=5 WHERE PermType=13 ORDER BY GroupPermNum LIMIT 1"));
				Assert.AreEqual(1,db.Execute("UPDATE claimpayment SET Note=CONCAT(Note,' x') WHERE Note<>'' ORDER BY ClaimPaymentNum LIMIT 1"));
				Assert.AreEqual(1,db.Execute("UPDATE insverify SET DateLastAssigned=DATE_ADD(DateLastAssigned,INTERVAL 9 HOUR) ORDER BY InsVerifyNum LIMIT 1"));
				Assert.AreEqual(1,db.Execute("UPDATE carrier SET PreAuthCodes='D0000' ORDER BY CarrierNum LIMIT 1"));
				Assert.AreEqual(1,db.Execute("INSERT INTO deletedobj (DateTEntry,ObjType,ObjSerialized) VALUES ('2026-10-24 20:00:00',0,'')"));
				Assert.AreEqual(1,db.Execute("UPDATE preference SET ValueString='0' WHERE PrefName='RepeatingChargesAllowBackdating'"));
				//A Reports grant for the UDS+ row that the step did not copy (the administrators may not run the sealant measure).
				long displayReportNumUds=db.ScalarLong("SELECT DisplayReportNum FROM displayreport WHERE InternalName='"+DisplayReportNames.UdsPlus+"'");
				Assert.AreEqual(1,db.Execute("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES ('0001-01-01',0,"+groupAdmin+","
					+OfficeFixture.PermReports+","+displayReportNumUds+")"));
				AdoptionRowCheck.Result result=AdoptionRowCheck.Run(db,Source(1),tampered);
				string failures=string.Join("\r\n",result.ListFailures);
				Assert.IsFalse(result.Passed);
				StringAssert.Contains(failures,"(user group "+groupAdmin+", PermType "+OfficeFixture.PermReports+", FKey "+displayReportNumUds
					+", NewerDate 0001-01-01, NewerDays 0) was added");
				Assert.AreEqual(1,result.CountReportPermsCopied,"The step's own grant is still found.");
				StringAssert.Contains(failures,"(user group "+groupAdmin+", PermType 272, FKey 0, NewerDate 0001-01-01, NewerDays 0) was added");
				StringAssert.Contains(failures,"changed (NewerDays); the fork migration moves only the Open Dental 25.3 PhoneExtension grants");
				StringAssert.Contains(failures,"PermType 273, FKey 0, NewerDate 0001-01-01, NewerDays 7) was added");
				StringAssert.Contains(failures,"No grouppermission row was added for the planned grant (user group ");
				StringAssert.Contains(failures,"1 claimpayment note(s) changed; the fork migration only makes a NULL note empty.");
				StringAssert.Contains(failures,"1 insverify.DateLastAssigned value(s) are not the original date at midnight.");
				StringAssert.Contains(failures,"row(s) of carrier hold a value other than the default ('') in the new column PreAuthCodes.");
				StringAssert.Contains(failures,"The new table deletedobj holds 1 row(s)");
				StringAssert.Contains(failures,"Preference RepeatingChargesAllowBackdating (PrefNum ");
				//Against the copy's report from before adoption (runbook step 6.1), the report comparison lets every one of these through.
				AllowedDifferences allowed=AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..",
					"allowed-differences.adoption.json"));
				List<Difference> listDiffs=ReportComparer.Compare(ValidationReport.Load(_result.ListRuns[0].ReportCopyPath),Validator.Run(db,tampered,null,null),allowed);
				string[] arrayTables={ "grouppermission","claimpayment","insverify","carrier","deletedobj","preference" };
				List<Difference> listTampered=listDiffs.Where(x => arrayTables.Contains(x.Table) && (x.Table!="claimpayment" && x.Table!="insverify" && x.Table!="carrier"
					|| x.Column=="Note" || x.Column=="DateLastAssigned" || x.Column=="PreAuthCodes")).ToList();
				Assert.AreEqual(arrayTables.Length,listTampered.Select(x => x.Table).Distinct().Count(),ReportComparer.Format(listDiffs));
				Assert.IsTrue(listTampered.All(x => x.IsAllowed),ReportComparer.Format(listTampered));
			}
		}

		///<summary>Adoption and the rehash write SecurityHash only. The rows' ON UPDATE audit timestamps (DateTStamp, SecDateTEdit) must not move; before
		///the M1-Z fix, SecurityHash's UPDATE statements stamped every rehashed row with the adoption time.</summary>
		[TestMethod]
		public void AdoptionAndRehash_KeepEveryAuditTimestamp() {
			SkipIfNeeded();
			RunResult run=_result.ListRuns[0];
			Assert.IsFalse(run.ListAllowedDifferences.Concat(run.ListUnexpectedDifferences).Any(x => x.Contains(".DateTStamp") || x.Contains(".SecDateTEdit")),
				string.Join("\r\n",run.ListAllowedDifferences));
			using(Db db=Db.Open(_server)) {
				//The hashed tables with an ON UPDATE timestamp in 25.3.48 (payplan has none).
				foreach(string table in new[] { "patient","appointment","claim","claimproc","paysplit","inssub","insverify","insverifyhist" }) {
					string column=table=="patient" || table=="appointment" ? "DateTStamp" : "SecDateTEdit";
					string key=Convert.ToString(db.Scalar("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+Adopted(1)+"' AND TABLE_NAME='"
						+table+"' AND COLUMN_KEY='PRI'"));
					long countHashed=db.ScalarLong("SELECT COUNT(*) FROM `"+Adopted(1)+"`.`"+table+"` b INNER JOIN `"+Source(1)+"`.`"+table+"` a USING(`"+key+"`) "
						+"WHERE NOT (a.SecurityHash<=>b.SecurityHash)");
					long countMoved=db.ScalarLong("SELECT COUNT(*) FROM `"+Adopted(1)+"`.`"+table+"` b INNER JOIN `"+Source(1)+"`.`"+table+"` a USING(`"+key+"`) "
						+"WHERE NOT (a.`"+column+"`<=>b.`"+column+"`)");
					Assert.AreEqual(0,countMoved,table+"."+column+" moved on "+countMoved+" rows ("+countHashed+" rows rehashed)");
					if(table=="patient") {
						Assert.IsTrue(countHashed>0,"Every patient is rehashed.");
					}
				}
			}
		}

		[TestMethod]
		public void RoundTrip_CoversEveryNewTableAndColumn() {
			SkipIfNeeded();
			RoundTripSummary roundTrip=_result.ListRuns[0].RoundTrip;
			Assert.IsNotNull(roundTrip);
			Assert.IsTrue(roundTrip.Passed,string.Join("\r\n",roundTrip.Tables.Where(x => !x.Passed).Select(x => x.Describe())));
			HashSet<string> setTables=new HashSet<string>(roundTrip.Tables.Select(x => x.Table));
			foreach(string item in Coverage.NewTables.Concat(Coverage.NewOrChangedColumns.Where(x => x!="patient._portal_active_email").Select(x => x.Split('.')[0]))) {
				Assert.IsTrue(setTables.Contains(item),item);
			}
			int countColumns=Coverage.NewOrChangedColumns.Count(x => x!="patient._portal_active_email");
			Assert.IsTrue(roundTrip.ColumnCount>=countColumns,roundTrip.ColumnCount+" columns checked");
			List<TableRoundTrip> listNotSaved=roundTrip.Tables.Where(x => !DataLayerRoundTrip.DictDocumentedExceptions.ContainsKey(x.Table) && x.RowsSaved!=x.Rows)
				.ToList();
			Assert.AreEqual(0,listNotSaved.Count,"Every row of every mapped table is saved: "+string.Join(", ",listNotSaved.Select(x => x.Table)));
			Assert.IsTrue(roundTrip.Tables.Where(x => DataLayerRoundTrip.DictDocumentedExceptions.ContainsKey(x.Table)).All(x => x.RowsSaved==0));
			Assert.IsTrue(roundTrip.Tables.Sum(x => x.ColumnsWritten)>=countColumns-5,"New values were written for (almost) every column.");
		}

		[TestMethod]
		public void CredentialInventory_ListsThePasswordsToReenter_WithoutValues() {
			SkipIfNeeded();
			List<string> listItems=_result.ListRuns[0].ListCredentials;
			Assert.IsTrue(listItems.Any(x => x.StartsWith("Program link "+OfficeFixture.PROGRAM_NAME+": Password")),string.Join("\r\n",listItems));
			Assert.IsTrue(listItems.Any(x => x.StartsWith("Clearinghouse Fictional Claims Exchange")),string.Join("\r\n",listItems));
			Assert.IsTrue(listItems.Any(x => x.StartsWith("Preference ReportingServerMySqlPassHash")),string.Join("\r\n",listItems));
			Assert.IsFalse(listItems.Any(x => x.Contains("Username")),"Only secrets are listed.");
			using(Db db=Db.Open(_server)) {
				List<CredentialInventory.Item> listAfter=CredentialInventory.Run(db,Adopted(1));
				Assert.AreEqual(listItems.Count,listAfter.Count,"Adoption does not touch stored passwords; they are re-entered by hand.");
				string secret=Convert.ToString(db.Scalar("SELECT PropertyValue FROM `"+Adopted(1)+"`.programproperty WHERE PropertyDesc='Password'"));
				Assert.IsFalse(listAfter.Any(x => x.ToString().Contains(secret)),"Values are never printed.");
			}
		}

		[TestMethod]
		public void StartupModel_EveryOpenDentalReleaseRefusesTheAdoptedCopy() {
			SkipIfNeeded();
			List<StartupScenarioResult> listAdopted=_result.ListRuns[0].ListStartupScenarios.Where(x => x.Database=="adopted copy").ToList();
			Assert.IsTrue(listAdopted.Count(x => x.IsReleaseClient)>=8);
			Assert.IsTrue(listAdopted.Where(x => x.IsReleaseClient).All(x => x.IsRefused && x.Outcome==OdStartupOutcome.Exits));
			Assert.IsTrue(_result.ListRuns[0].ListStartupScenarios.Any(x => x.Database=="unadopted copy" && x.Outcome!=OdStartupOutcome.Exits),
				"The control case shows the model tells the databases apart.");
		}

		[TestMethod]
		public void ResultFiles_AreWritten() {
			SkipIfNeeded();
			Assert.IsTrue(File.Exists(Path.Combine(_outDir,"rehearsal-result.json")));
			string markdown=File.ReadAllText(Path.Combine(_outDir,"rehearsal-result.md"));
			StringAssert.Contains(markdown,"Overall: **PASS**");
			StringAssert.Contains(markdown,"## Step timings");
			StringAssert.Contains(markdown,"### Adoption log");
			Assert.IsTrue(File.Exists(_result.ListRuns[0].ReportBaselinePath) && File.Exists(_result.ListRuns[0].ReportCopyPath)
				&& File.Exists(_result.ListRuns[0].ReportAdoptedPath));
		}

		///<summary>The rehearsal adopts with HeadlessAdoption, the code "AdoptionValidation adopt" runs in runbook steps 4 and 5, and compares the adopted
		///copy with the restored copy's report, as runbook step 6 does. Its log ends the way the runbook says it does, and the rehash is logged.</summary>
		[TestMethod]
		public void Rehearsal_AdoptsWithTheRunbookSequence() {
			SkipIfNeeded();
			RunResult run=_result.ListRuns[0];
			List<string> listSteps=run.ListSteps.Select(x => x.Name).ToList();
			int indexAdopt=listSteps.FindIndex(x => x.StartsWith("Adopt ("));
			int indexRehash=listSteps.FindIndex(x => x.StartsWith("Rehash"));
			int indexReport=listSteps.FindIndex(x => x.StartsWith("Record post-adoption"));
			int indexRoundTrip=listSteps.FindIndex(x => x.StartsWith("Data-layer round trip"));
			int indexCopy=listSteps.FindIndex(x => x.StartsWith("Copy on the server"));
			int indexRowCheck=listSteps.IndexOf("Row check against the original");
			Assert.IsTrue(indexCopy<listSteps.IndexOf("Preflight") && listSteps.IndexOf("Preflight")<indexAdopt
				&& indexAdopt<listSteps.IndexOf("Fork migration (all steps again)") && listSteps.IndexOf("Schema verifier")<indexRehash && indexRehash<indexRowCheck
				&& indexRowCheck<indexReport && indexReport<indexRoundTrip,string.Join(", ",listSteps));
			foreach(string expected in new[] { "It can be adopted.","Dropped index patient.portal_active_email_uniq.","Dropped column patient._portal_active_email.",
				"DataBaseVersion and ProgramVersion set to 99.0.0.0.","Adoption finished.","0 changes.","The schema matches Open Dental "+SchemaVerifier.ReferenceVersion+" exactly","Rehash finished.",
				"Row check against "+Source(1),"preference: 5 added (","documentmisc: 4 Open Dental update-file row(s) deleted, none left; 0 other row(s) deleted",
				"securitylog: 2 entries added (1 adoption, 1 rehash)","updatehistory: identical row by row.",
				"Fork migration step 2026101971 Report rows for Procedures, Production by Procedure and Insurance Pending Supplementals changed the database:",
				"  Renamed displayreport ODSyntheticProdByProc to ODProductionByProcedure (same description)",
				"  Renamed displayreport ODSyntheticProcsItemized to ODProceduresIndividual (same description)",
				"  Renamed displayreport ODSyntheticInsPendingSupp to ODInsPendingSupplementals (same description)",
				"displayreport: 3 of the 3 rename(s) migration-preview lists, InternalName only (",
				"; the UDS+ report row added as migration-preview lists ("+DisplayReportNames.UdsPlus+", DisplayReportNum ",
				" later Public Health row(s) moved down one place, ItemOrder only; no other row or column changed, no other row added, none removed.",
				//The UDS+ report row step (M2-UDS) on the 25.3.48 source: the 25.4 report's row after the fixture's sealant measure, and its one grant.
				"Fork migration step 2026110103 Report row for the UDS+ report changed the database:","  Added displayreport "+DisplayReportNames.UdsPlus,
				"  Copied 1 report permission(s) from ODDentalSealantMeasure to "+DisplayReportNames.UdsPlus,
				"Reports for the UDS+ report row granted to 1 of the 1 user group(s) that may run the FQHC Dental Sealant Measure, as migration-preview lists",
				//The Insurance History step (M2-HIST) on the fixture's ten InsHist*Codes preferences.
				"Fork migration step 2026110104 Insurance History rows from the InsHist preferences to code groups (26.1) changed the database:",
				"  Code group 'BWs' (CodeGroupNum ","History Proc Code Z4910, codes added: "+OfficeFixture.INSHIST_CODE_ADDED,
				"  Added code group '"+OfficeFixture.INSHIST_GROUP_ADDED+"' (FMDebride, ItemOrder ",
				"codegroup: "+OfficeFixture.CountInsHistGroupsMarked+" of the "+OfficeFixture.CountInsHistGroupsMarked+" code group(s) migration-preview lists marked Show "
					+"In History as planned, ProcCodes and the history columns only (",
				"; "+OfficeFixture.CountInsHistGroupsAdded+" of the "+OfficeFixture.CountInsHistGroupsAdded+" planned code group(s) added with the planned values (CodeGroupNum ",
				//The Open Dental 25.4 and 26.1 steps (M2-01, M2-02) on the 25.3.48 source.
				"Fork migration step 2026102401 Open Dental 25.4 schema changed the database:","Fork migration step 2026102402 Open Dental 26.1 schema changed the database:",
				"  Inserted preference RepeatingChargesAllowBackdating","  Moved 1 Open Dental 25.3 PhoneExtension grant(s) from 272 to 274",
				"  Granted BlockoutAdd to 2 user group(s)","  Granted BlockoutDelete to 2 user group(s)",
				"of the "+Seed26_1.ListPrefDefaults.Count+" 25.4 and 26.1 preference(s) migration-preview lists added with their defaults (",
				"grouppermission: 1 of the 1 Open Dental 25.3 PhoneExtension grant(s) moved from 272 to 274; BlockoutAdd granted to 2 of 2 user group(s), "
					+"BlockoutDelete granted to 2 of 2 user group(s), as migration-preview lists",
				"Changed columns: claimpayment.Note: 1 of the 1 NULL note(s) migration-preview counts now empty, every other note unchanged; "
					+"insverify.DateLastAssigned: the original date at midnight in all ",
				"New tables and columns: 5 new table(s), all empty (deletedobj, ","new column(s) in 21 table(s) hold their default in every row." })
			{
				Assert.IsTrue(run.ListAdoptionLog.Any(x => x.Contains(expected)),expected+"\r\n"+string.Join("\r\n",run.ListAdoptionLog));
			}
			Assert.IsFalse(run.ListAdoptionLog.Any(x => x.Contains("FAIL")),string.Join("\r\n",run.ListAdoptionLog));
			using(Db db=Db.Open(_server,Adopted(1))) {
				Assert.AreEqual(2,db.ScalarLong("SELECT COUNT(*) FROM securitylog WHERE LogText LIKE '"+HeadlessAdoption.REHASH_LOG_TEXT+"%'"),
					"The rehash is logged once by the adoption and once by the second pass.");
			}
		}

		///<summary>Runbook step 3.2's restore never writes to a database that exists, and refuses a --databases dump: loading one into a "scratch"
		///name would replay its DROP TABLE and INSERT statements into the database it names.</summary>
		[TestMethod]
		public void Restore_RefusesAnExistingDatabase_AndADumpThatNamesADatabase() {
			SkipIfNeeded();
			(string dump,string client)? tools=DatabaseCopier.FindClientTools();
			if(tools==null) {
				Assert.Inconclusive("mariadb-dump and mariadb are not installed.");
			}
			string folder=Path.Combine(_outDir,"restore");
			Directory.CreateDirectory(folder);
			string pathMulti=Path.Combine(folder,"databases.sql");
			RunClient(tools.Value.dump,"--databases --no-data --result-file=\""+pathMulti+"\" "+Source(1));
			StringAssert.Contains(File.ReadAllText(pathMulti),"USE `"+Source(1)+"`","A --databases dump names its database.");
			string target=Rehearsal.DbPrefix+Tag+"_restore";
			long tablesBefore;
			using(Db db=Db.Open(_server)) {
				tablesBefore=db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+Source(1)+"'");
			}
			RefusedException ex=AssertThrows<RefusedException>(() => Program.Run(new[] { "restore","--dump",pathMulti,"--database",target },x => { }));
			StringAssert.Contains(ex.Message,"names a database");
			ex=AssertThrows<RefusedException>(() => Program.Run(new[] { "restore","--dump",pathMulti,"--database",Source(2) },x => { }));
			StringAssert.Contains(ex.Message,"already exists");
			using(Db db=Db.Open(_server)) {
				Assert.IsFalse(db.DatabaseExists(target),"Nothing was created.");
				Assert.AreEqual(tablesBefore,db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+Source(1)+"'"));
				Assert.AreEqual(4,db.ScalarLong("SELECT COUNT(*) FROM `"+Source(1)+"`.documentmisc WHERE DocMiscType IN (0,1)"),"The source keeps its rows.");
			}
			//A single-database dump restores.
			string pathSingle=Path.Combine(folder,"single.sql");
			DatabaseCopier.Backup(_server,Source(1),pathSingle,tools.Value.dump);
			Assert.AreEqual(0,DumpCheck.Scan(pathSingle).Count,string.Join("; ",DumpCheck.Scan(pathSingle)));
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "restore","--dump",pathSingle,"--database",target },x => { }));
			using(Db db=Db.Open(_server)) {
				Assert.AreEqual(0,HeadlessAdoption.CompareRowCounts(db,Source(1),target).Count);
			}
		}

		///<summary>"AdoptionValidation adopt" changes nothing unless the copy and the original are what the runbook says they are.</summary>
		[TestMethod]
		public void Adopt_RefusesAndChangesNothing_WhenTheCopyOrTheOriginalIsWrong() {
			SkipIfNeeded();
			string copy=Rehearsal.DbPrefix+Tag+"_refusals";
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Source(1),"--database",copy },listLog.Add),string.Join("\r\n",listLog));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "adopt","--database",copy,"--original",Source(1),"--confirm","opendental" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "adopt","--database",copy,"--confirm",copy },listLog.Add));
			Assert.AreEqual(Program.ExitUsage,Program.Run(new[] { "adopt","--database",copy,"--original",copy,"--confirm",copy },listLog.Add));
			Assert.AreEqual(Program.ExitUsage,Program.Run(new[] { "adopt","--database",copy,"--original",Adopted(1),"--confirm",copy },listLog.Add),
				"An adopted database is not an original.");
			Assert.IsTrue(listLog.Any(x => x.Contains("is not an unadopted Open Dental database")),string.Join("\r\n",listLog));
			Assert.AreEqual(Program.ExitUsage,Program.Run(new[] { "adopt","--database",copy,"--original",Rehearsal.DbPrefix+Tag+"_missing","--confirm",copy },
				listLog.Add),"The original must exist.");
			using(Db dbHeld=Db.Open(_server,Source(1))) {
				Assert.AreEqual(Program.ExitUsage,Program.Run(new[] { "adopt","--database",copy,"--original",Source(1),"--confirm",copy },listLog.Add),
					"Someone is still connected to the original.");
			}
			Assert.IsTrue(listLog.Any(x => x.Contains("Other connections use these databases")),string.Join("\r\n",listLog));
			Assert.AreEqual(Program.ExitUsage,Program.Run(new[] { "adopt","--database",Source(1),"--original",copy,"--confirm",Source(1) },listLog.Add),
				"The names the wrong way round: the original is older than its copy.");
			Assert.IsTrue(listLog.Any(x => x.Contains("the wrong way round")),string.Join("\r\n",listLog));
			using(Db db=Db.Open(_server)) {
				db.Execute("DELETE FROM `"+copy+"`.appointment ORDER BY AptNum DESC LIMIT 1");
			}
			Assert.AreEqual(Program.ExitUsage,Program.Run(new[] { "adopt","--database",copy,"--original",Source(1),"--confirm",copy },listLog.Add),
				"The copy is not an exact copy of the original.");
			Assert.IsTrue(listLog.Any(x => x.Contains("is not an exact copy") && x.Contains("appointment")),string.Join("\r\n",listLog));
			using(Db db=Db.Open(_server)) {
				ForkMigrationsState(db,copy,out string version,out long markers);
				Assert.AreEqual("25.3.48.0",version,"No refusal changed the copy.");
				Assert.AreEqual(0,markers);
				ForkMigrationsState(db,Source(1),out version,out markers);
				Assert.AreEqual("25.3.48.0",version,"No refusal changed the original.");
				Assert.AreEqual(0,markers);
			}
		}

		///<summary>The dry run of runbook step 0.6 in the office's topology: Invoke-RunbookDryRun.ps1 runs the tool commands of steps 0.2, 1.1, 3.2 to
		///6.1 and the go-live checks 10.1 and 10.2 exactly as the runbook gives them (AdoptionValidation float-check, connections, copy, adopt and
		///startup-check, SyntheticData validate and compare), out of process, on a copy of the rehearsal's source, through a relay on another port
		///that stands in for the SSH forward of step 0.0. Then step 4 is run again, through the relay too, on the adopted copy.</summary>
		[TestMethod]
		public void RunbookDryRun_PassesThroughAForwardedPort_WithTheRunbookCommands() {
			SkipIfNeeded();
			string tools=AppDomain.CurrentDomain.BaseDirectory;
			string script=Path.Combine(tools,"Invoke-RunbookDryRun.ps1");
			for(DirectoryInfo dir=new DirectoryInfo(tools);!File.Exists(script) && dir!=null;dir=dir.Parent) {
				script=Path.Combine(dir.FullName,"tools","AdoptionValidation","Invoke-RunbookDryRun.ps1");
			}
			Assert.IsTrue(File.Exists(script),"Invoke-RunbookDryRun.ps1 not found");
			string copy=Rehearsal.DbPrefix+Tag+"_dryrun";
			string folder=Path.Combine(_outDir,"dry-run");
			string output;
			int exitCode;
			using(TcpRelay relay=new TcpRelay(_server.Port)) {
				ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Original "+Source(1)
					+" -Copy "+copy+" -Folder \""+folder+"\" -AsOf 2026-09-30 -Tools \""+tools.TrimEnd('\\')+"\" -Port "+relay.Port+" -User "+_server.User) {
					UseShellExecute=false,
					CreateNoWindow=true,
					RedirectStandardOutput=true,
					RedirectStandardError=true,
				};
				using(Process process=Process.Start(processStartInfo)) {
					System.Threading.Tasks.Task<string> taskErr=process.StandardError.ReadToEndAsync();
					output=process.StandardOutput.ReadToEnd();
					Assert.IsTrue(process.WaitForExit(900000),"The dry run did not finish in 15 minutes.");
					output+=taskErr.Result;
					exitCode=process.ExitCode;
				}
				Assert.AreEqual(0,exitCode,output);
				//Twelve tool runs, each with at least one connection, and adopt's business layer with its own: all of them through the relay.
				Assert.IsTrue(relay.CountAccepted>=13,relay.CountAccepted+" connections through the relay\r\n"+output);
				//Step 4 again, in process and through the relay: it repeats the finished adoption and changes nothing but its own security log entry.
				List<string> listLog=new List<string>();
				ValidationReport before;
				using(Db db=Db.Open(_server)) {
					before=Validator.Run(db,copy,null,null);
				}
				int countBefore=relay.CountAccepted;
				Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "adopt","--database",copy,"--original",Source(1),"--confirm",copy,"--port",
					relay.Port.ToString(CultureInfo.InvariantCulture) },listLog.Add),string.Join("\r\n",listLog));
				Assert.IsTrue(relay.CountAccepted>countBefore,"adopt connected through the relay");
				Assert.IsTrue(listLog.Any(x => x.Contains("already has an adoption record")) && listLog.Any(x => x.Contains("Adoption is skipped")));
				Assert.IsTrue(listLog.Any(x => x.Contains("securitylog: 3 entries added (1 adoption, 2 rehash)")),string.Join("\r\n",listLog));
				using(Db db=Db.Open(_server)) {
					List<Difference> listDiffs=ReportComparer.Compare(before,Validator.Run(db,copy,null,null),null);
					Assert.IsTrue(listDiffs.All(x => x.Table=="securitylog" || x.Table=="securityloghash"),ReportComparer.Format(listDiffs));
				}
			}
			StringAssert.Contains(output,"Dry run: PASS");
			foreach(string step in new[] { "== 0.2 FLOAT","== 0.2 What the fork migration","== 1.1 ","== 3.2 ","== 3.3 ","== 4-5 ","== 6.1 ","== 10.1 ","== 10.2 " }) {
				StringAssert.Contains(output,step);
			}
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"float-check.txt")),"no value that a logical backup (mariadb-dump) would round");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"migration-preview.txt")),"Adoption's fork migration will rename 3 displayreport row(s)");
			StringAssert.Contains(output,"migration-preview: adoption's fork migration renames 3 displayreport row(s)");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"connections-before.txt")),"No connection uses "+Source(1)+".");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"copy.txt")),"Copy: PASS");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"compare-baselines.txt")),"Unexpected differences: 0");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"compare.txt")),"Unexpected differences: 0");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"compare-original.txt")),"Unexpected differences: 0");
			string adoptionLog=File.ReadAllText(Path.Combine(folder,"adoption-log.txt"));
			StringAssert.Contains(adoptionLog,"Adopt: PASS");
			StringAssert.Contains(adoptionLog,"Row check against "+Source(1));
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"startup-check.txt")),"Every Open Dental release client refuses this database.");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"connections-golive.txt")),"No connection uses "+Source(1)+".");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"dry-run-timings.txt")),"Dry run: PASS");
			Assert.IsFalse(Directory.GetFiles(folder,"*.sql").Any(),"No backup file: the copy is made on the server.");
			using(Db db=Db.Open(_server)) {
				ForkMigrationsState(db,copy,out string version,out long markers);
				Assert.AreEqual("99.0.0.0",version);
				Assert.AreEqual(1,markers);
				ForkMigrationsState(db,Source(1),out version,out markers);
				Assert.AreEqual("25.3.48.0",version,"The dry run never writes to the original.");
				Assert.AreEqual(0,markers);
			}
		}

		///<summary>The business layer connects through the port it is given, not 3306: through the SSH forward of runbook step 0.0, every query of
		///adopt and startup-check reaches the database server, not whatever answers on the admin PC's own port 3306.</summary>
		[TestMethod]
		public void BusinessLayer_ConnectsThroughTheGivenPort() {
			SkipIfNeeded();
			using(TcpRelay relay=new TcpRelay(_server.Port)) {
				ServerSettings server=new ServerSettings { Port=relay.Port };
				BusinessLayer.Connect(server,Source(1));
				Assert.AreEqual(Source(1),BusinessLayer.CurrentDatabase());
				StringAssert.Contains(DataConnectionBase.DataConnection.GetCurrentConnectionString(),"Port="+relay.Port.ToString(CultureInfo.InvariantCulture));
				Assert.IsTrue(relay.CountAccepted>=1,"The business layer's connection went through the relay.");
				Assert.AreEqual("25.3.48.0",BusinessLayer.GetPref("DataBaseVersion"));
			}
		}

		///<summary>Runbook step 3.2's copy: on the server, only into a new name, and only when nobody is connected.</summary>
		[TestMethod]
		public void Copy_RefusesAndCreatesNothing_WhenTheNamesAreWrongOrSomeoneIsConnected() {
			SkipIfNeeded();
			string target=Rehearsal.DbPrefix+Tag+"_copyrefused";
			List<string> listLog=new List<string>();
			RefusedException ex=AssertThrows<RefusedException>(() => Program.Run(new[] { "copy","--original",Source(1),"--database",Source(2) },listLog.Add));
			StringAssert.Contains(ex.Message,"already exists");
			ex=AssertThrows<RefusedException>(() => Program.Run(new[] { "copy","--original",Source(1),"--database",Source(1) },listLog.Add));
			StringAssert.Contains(ex.Message,"the same");
			ex=AssertThrows<RefusedException>(() => Program.Run(new[] { "copy","--original",Rehearsal.DbPrefix+Tag+"_missing","--database",target },listLog.Add));
			StringAssert.Contains(ex.Message,"does not exist");
			using(Db dbHeld=Db.Open(_server,Source(1))) {
				ex=AssertThrows<RefusedException>(() => Program.Run(new[] { "copy","--original",Source(1),"--database",target },listLog.Add));
				StringAssert.Contains(ex.Message,"Other connections use these databases");
			}
			using(Db db=Db.Open(_server)) {
				Assert.IsFalse(db.DatabaseExists(target),"Nothing was created.");
				Assert.AreEqual(0,HeadlessAdoption.CompareRowCounts(db,Source(1),Source(2)).Count,"Neither database was written.");
			}
		}

		///<summary>Runbook steps 1.1 and 10.1: the connections command sees other connections, says which computer, and fails when one uses the
		///database.</summary>
		[TestMethod]
		public void Connections_FailsWhileSomeoneUsesTheDatabase() {
			SkipIfNeeded();
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "connections","--database",Source(2) },listLog.Add),string.Join("\r\n",listLog));
			StringAssert.Contains(listLog.Last(),"No connection uses "+Source(2)+".");
			listLog.Clear();
			using(Db dbHeld=Db.Open(_server,Source(2))) {
				Assert.AreEqual(Program.ExitValidationFailed,Program.Run(new[] { "connections","--database",Source(2) },listLog.Add));
			}
			Assert.IsTrue(listLog.Any(x => x.Contains(" to "+Source(2)+" (") && x.Contains(" from ")),string.Join("\r\n",listLog));
			StringAssert.Contains(listLog.Last(),"1 connection(s) use "+Source(2));
		}

		///<summary>Why the runbook copies on the server: mariadb-dump writes a FLOAT with 6 significant digits, so a backup and restore rounds longer
		///values (34.285713 comes back as 34.2857). float-check finds exactly those values; the validator's column checksums now see the change (before
		///they did not, only CHECKSUM TABLE did); and the server-side copy keeps every value and AUTO_INCREMENT exactly.</summary>
		[TestMethod]
		public void Copy_IsExact_WhereADumpRoundsFloatValues() {
			SkipIfNeeded();
			string withFloats=Rehearsal.DbPrefix+Tag+"_floats";
			string copied=Rehearsal.DbPrefix+Tag+"_floats_copy";
			string restored=Rehearsal.DbPrefix+Tag+"_floats_dump";
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Source(1),"--database",withFloats },listLog.Add),string.Join("\r\n",listLog));
			string table;
			string column;
			string key;
			using(Db db=Db.Open(_server)) {
				ValidationReport reportSource=Validator.Run(db,Source(1),null,null);
				ValidationReport reportCopy=Validator.Run(db,withFloats,null,null);
				Assert.AreEqual(0,ReportComparer.Compare(reportSource,reportCopy,null).Count,ReportComparer.Format(ReportComparer.Compare(reportSource,reportCopy,null)));
				AssertSameAutoIncrement(db,Source(1),withFloats);
				//A FLOAT column of a table with at least three rows, as Open Dental's imagedraw.FontSize (information_schema is read with the database
				//named, so the server looks only at this database).
				Dictionary<string,long> dictRows=db.Rows("SELECT TABLE_NAME,TABLE_ROWS FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+withFloats+"'")
					.ToDictionary(x => Convert.ToString(x[0]),x => Convert.ToInt64(x[1]??0L));
				object[] row=db.Rows("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+withFloats+"' AND DATA_TYPE='float' "
					+"ORDER BY TABLE_NAME='imagedraw' DESC,TABLE_NAME,ORDINAL_POSITION").FirstOrDefault(x => dictRows[Convert.ToString(x[0])]>=3);
				Assert.IsNotNull(row,"The synthetic data has a FLOAT column with rows.");
				table=Convert.ToString(row[0]);
				column=Convert.ToString(row[1]);
				key=Convert.ToString(db.Scalar("SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='"+withFloats+"' AND TABLE_NAME='"+table
					+"' AND INDEX_NAME='PRIMARY'"));
				Assert.AreEqual(0,FloatCheck.Run(db,withFloats).Total,"The synthetic FLOAT values are short.");
				List<long> listKeys=db.Rows("SELECT `"+key+"` FROM `"+withFloats+"`.`"+table+"` ORDER BY `"+key+"` LIMIT 3").Select(x => Convert.ToInt64(x[0])).ToList();
				//Two values a dump rounds (a computed font size, 7 digits), one it keeps.
				db.Execute("UPDATE `"+withFloats+"`.`"+table+"` SET `"+column+"`=34.285713 WHERE `"+key+"`="+listKeys[0]);
				db.Execute("UPDATE `"+withFloats+"`.`"+table+"` SET `"+column+"`=0.1234567 WHERE `"+key+"`="+listKeys[1]);
				db.Execute("UPDATE `"+withFloats+"`.`"+table+"` SET `"+column+"`=8.25 WHERE `"+key+"`="+listKeys[2]);
				FloatCheck.Result floatCheck=FloatCheck.Run(db,withFloats);
				Assert.AreEqual(2,floatCheck.Total,floatCheck.Describe());
				Assert.AreEqual(table+"."+column,floatCheck.ListColumns.Single().Table+"."+floatCheck.ListColumns.Single().Column);
			}
			listLog.Clear();
			Assert.AreEqual(Program.ExitValidationFailed,Program.Run(new[] { "float-check","--database",withFloats },listLog.Add),string.Join("\r\n",listLog));
			StringAssert.Contains(string.Join("\r\n",listLog),"2 value(s) in 1 column(s) that a logical backup (mariadb-dump) would round");
			//The server-side copy is exact.
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",withFloats,"--database",copied },listLog.Add),string.Join("\r\n",listLog));
			using(Db db=Db.Open(_server)) {
				ValidationReport reportWithFloats=Validator.Run(db,withFloats,null,null);
				List<Difference> listCopy=ReportComparer.Compare(reportWithFloats,Validator.Run(db,copied,null,null),null);
				Assert.AreEqual(0,listCopy.Count,ReportComparer.Format(listCopy));
				Assert.AreEqual(2,FloatCheck.Run(db,copied).Total,"The copy keeps the long values.");
				AssertSameAutoIncrement(db,withFloats,copied);
				//A dump and restore rounds them, and the comparison now names the column.
				(string dump,string client)? tools=DatabaseCopier.FindClientTools();
				if(tools==null) {
					Assert.Inconclusive("mariadb-dump and mariadb are not installed; the dump half of this test did not run.");
				}
				string pathDump=Path.Combine(_outDir,"floats.sql");
				DatabaseCopier.Backup(_server,withFloats,pathDump,tools.Value.dump);
				DatabaseCopier.Restore(_server,pathDump,restored,tools.Value.client);
				List<Difference> listDump=ReportComparer.Compare(reportWithFloats,Validator.Run(db,restored,null,null),null);
				Assert.IsTrue(listDump.Any(x => x.Kind==DifferenceKind.ColumnChanged && x.Table==table && x.Column==column),ReportComparer.Format(listDump));
				Assert.IsTrue(listDump.All(x => x.Table==table && (x.Column==column || x.Kind==DifferenceKind.ChecksumTableChanged)),ReportComparer.Format(listDump));
				Assert.AreEqual(0,FloatCheck.Run(db,restored).Total,"The restored values are the rounded ones.");
				Assert.AreEqual(1,db.ScalarLong("SELECT COUNT(*) FROM `"+restored+"`.`"+table+"` WHERE `"+column+"`=CAST(34.2857 AS FLOAT)"));
			}
		}

		private static void AssertSameAutoIncrement(Db db,string databaseA,string databaseB) {
			Dictionary<string,string> ReadCounters(string database) {
				return db.Rows("SELECT TABLE_NAME,AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+database+"'")
					.ToDictionary(x => Convert.ToString(x[0]),x => Convert.ToString(x[1]));
			}
			Dictionary<string,string> dictA=ReadCounters(databaseA);
			Dictionary<string,string> dictB=ReadCounters(databaseB);
			List<string> listDifferent=dictA.Where(x => !dictB.TryGetValue(x.Key,out string value) || value!=x.Value).Select(x => x.Key+" "+x.Value).ToList();
			Assert.IsTrue(dictA.Count==dictB.Count && listDifferent.Count==0,"AUTO_INCREMENT of "+databaseA+" and "+databaseB+": "+string.Join(", ",listDifferent));
		}

		///<summary>Negative control for the row check (carry-over C3): the report comparison allows any change to preference, documentmisc, securitylog
		///and securityloghash as whole tables; adopt's row check against the original does not.</summary>
		[TestMethod]
		public void RowCheck_CatchesChangesTheWholeTableRulesAllow() {
			SkipIfNeeded();
			string tampered=Rehearsal.DbPrefix+Tag+"_tampered";
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Adopted(2),"--database",tampered },listLog.Add),string.Join("\r\n",listLog));
			using(Db db=Db.Open(_server)) {
				//The round trip deleted its test rows at the end of 50 tables, so their counters are past the largest key: the copy keeps them too.
				//information_schema is read with the database and table named for each of its tables, so the server looks only at those.
				Assert.AreEqual(1,db.ScalarLong("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+Adopted(2)+"' AND TABLE_NAME='benefit' "
					+"AND COLUMN_NAME='BenefitNum' AND EXTRA LIKE '%auto_increment%'"));
				Assert.IsTrue(db.ScalarLong("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+Adopted(2)+"' AND TABLE_NAME='benefit'")
					>db.ScalarLong("SELECT MAX(BenefitNum) FROM `"+Adopted(2)+"`.benefit")+1,"The round trip left a gap at the end of benefit.");
				AssertSameAutoIncrement(db,Adopted(2),tampered);
				Assert.IsTrue(AdoptionRowCheck.Run(db,Source(2),tampered).Passed,"An untouched adopted copy passes.");
				ValidationReport before=Validator.Run(db,tampered,null,null);
				db.Use(tampered);
				Assert.AreEqual(1,db.Execute("UPDATE preference SET ValueString='Synthetic Practice Renamed' WHERE PrefName='PracticeTitle'"));
				Assert.AreEqual(1,db.Execute("UPDATE securitylog SET LogText=CONCAT(LogText,' edited') ORDER BY SecurityLogNum LIMIT 1"));
				Assert.AreEqual(1,db.Execute("DELETE FROM documentmisc WHERE DocMiscType="+OfficeFixture.DocMiscShareScreenExeSegment));
				Assert.AreEqual(1,db.Execute("INSERT INTO updatehistory (DateTimeUpdated,ProgramVersion,Signature) VALUES ('2026-10-24 20:00:00','99.0.0.0','')"));
				//A report row the fork migration does not rename, renamed: the displayreport.InternalName rule lets any rename through.
				Assert.AreEqual(1,db.Execute("UPDATE displayreport SET InternalName='ODSyntheticFutureRenamed' WHERE InternalName='ODSyntheticFutureReport'"));
				AdoptionRowCheck.Result result=AdoptionRowCheck.Run(db,Source(2),tampered);
				string failures=string.Join("\r\n",result.ListFailures);
				Assert.IsFalse(result.Passed);
				StringAssert.Contains(failures,"Preference PracticeTitle");
				StringAssert.Contains(failures,"Existing securitylog rows were removed or changed");
				StringAssert.Contains(failures,"documentmisc row(s) that are not Open Dental update files were deleted");
				StringAssert.Contains(failures,"updatehistory changed");
				StringAssert.Contains(failures,"the fork migration renames only the rows migration-preview lists");
				Assert.AreEqual(3,result.ListReportRenames.Count,"The planned renames are still found.");
				//The report comparison under allowed-differences.adoption.json lets the first three and the rename through; updatehistory has no rule
				//any more.
				AllowedDifferences allowed=AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..",
					"allowed-differences.adoption.json"));
				List<Difference> listDiffs=ReportComparer.Compare(before,Validator.Run(db,tampered,null,null),allowed);
				Assert.IsTrue(listDiffs.Where(x => x.Table=="preference" || x.Table=="securitylog" || x.Table=="documentmisc" || x.Table=="displayreport")
					.All(x => x.IsAllowed),ReportComparer.Format(listDiffs));
				Assert.IsTrue(listDiffs.Any(x => x.Table=="displayreport" && x.Column=="InternalName"),ReportComparer.Format(listDiffs));
				//Undoing a planned rename is caught too, and so is any other column of displayreport, which the report comparison allows with the whole
				//table (the UDS+ row the fork migration adds changes every column checksum): only the row check holds displayreport to the plan.
				Assert.AreEqual(1,db.Execute("UPDATE displayreport SET InternalName='ODSyntheticProdByProc' WHERE InternalName='"
					+DisplayReportNames.ProductionByProcedure+"'"));
				Assert.AreEqual(1,db.Execute("UPDATE displayreport SET Description='Today, renamed' WHERE InternalName='ODToday'"));
				//The UDS+ row with another value than planned, and a moved Public Health row put back.
				Assert.AreEqual(1,db.Execute("UPDATE displayreport SET IsHidden=1 WHERE InternalName='"+DisplayReportNames.UdsPlus+"'"));
				Assert.AreEqual(1,db.Execute("UPDATE displayreport SET ItemOrder=ItemOrder-1 WHERE InternalName='"+OfficeFixture.ArrayReportRowsPublicHealth[1].internalName+"'"));
				result=AdoptionRowCheck.Run(db,Source(2),tampered);
				failures=string.Join("\r\n",result.ListFailures);
				StringAssert.Contains(failures,"(ODSyntheticProdByProc) was not renamed to "+DisplayReportNames.ProductionByProcedure);
				StringAssert.Contains(failures,"changed (Description)");
				StringAssert.Contains(failures,"was added with IsHidden 1 (planned: 0); the fork migration adds only the UDS+ report row migration-preview lists.");
				StringAssert.Contains(failures,"was not moved down to ItemOrder ");
				Assert.AreEqual(0,result.DisplayReportNumAdded);
				listDiffs=ReportComparer.Compare(before,Validator.Run(db,tampered,null,null),allowed);
				Assert.IsTrue(listDiffs.Any(x => x.Table=="displayreport" && x.Column=="Description" && x.IsAllowed),ReportComparer.Format(listDiffs));
				Assert.IsTrue(listDiffs.Any(x => x.Table=="updatehistory" && !x.IsAllowed),ReportComparer.Format(listDiffs));
			}
		}

		///<summary>Carry-over C1: an adoption interrupted before Adoption.Run writes ForkSchemaLevel (here after it deleted the update files, its step 3)
		///is finished by running adopt again, and ends like an uninterrupted one. One interrupted after it (here in the fork migration, step 7) is not:
		///adopt fails, closed. The runbook does neither: it drops such a copy and makes a new one (step 4), which is shown last.</summary>
		[TestMethod]
		public void InterruptedAdoption_IsFinishedBeforeTheMarkers_FailsClosedAfter_AndANewCopyAdopts() {
			SkipIfNeeded();
			string early=Rehearsal.DbPrefix+Tag+"_cutearly";
			string late=Rehearsal.DbPrefix+Tag+"_cutlate";
			List<string> listLog=new List<string>();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Source(1),"--database",early },listLog.Add),string.Join("\r\n",listLog));
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Source(1),"--database",late },listLog.Add),string.Join("\r\n",listLog));
			//Interrupted after step 3.
			BusinessLayer.Connect(_server,early);
			AssertThrows<ApplicationException>(() => OpenDentBusiness.Adoption.Run(x => {
				if(x.StartsWith("Deleted ")) {
					throw new ApplicationException("interrupted for the test");
				}
			}));
			HeadlessAdoption.Result resumed=HeadlessAdoption.Run(early,null,null,_server,Source(1));
			Assert.IsTrue(resumed.Passed,string.Join("\r\n",resumed.ListFailures));
			Assert.IsTrue(resumed.Preflight.IsResume,"The rerun finishes the adoption that was started.");
			using(Db db=Db.Open(_server)) {
				//Equal to run 1's adopted copy right after adoption, apart from the time of adoption.
				AllowedDifferences allowedTimes=new AllowedDifferences { Rules=Rehearsal.ArraySnapshotTables.Select(x => new AllowedRule { Kind="*",Table=x,
					Reason="time of adoption" }).ToList() };
				List<Difference> listDiffs=ReportComparer.Compare(ValidationReport.Load(_result.ListRuns[0].ReportAdoptedPath),Validator.Run(db,early,null,null),allowedTimes);
				Assert.IsTrue(listDiffs.All(x => x.IsAllowed),ReportComparer.Format(listDiffs));
			}
			//Interrupted in the fork migration, after ForkSchemaLevel was written.
			BusinessLayer.Connect(_server,late);
			OpenDentBusiness.IForkMigrationRunner runnerReal=OpenDentBusiness.ForkMigrationHandOff.Runner;
			AssertThrows<ApplicationException>(() => OpenDentBusiness.Adoption.Run(null,new CutOffRunner(runnerReal)));
			listLog.Clear();
			int exitLate=Program.Run(new[] { "adopt","--database",late,"--original",Source(1),"--confirm",late },listLog.Add);
			Assert.AreNotEqual(Program.ExitPass,exitLate,string.Join("\r\n",listLog));
			Assert.IsTrue(listLog.Any(x => x.StartsWith("Adopt: FAIL") || x.StartsWith("Adopt: STOPPED")),string.Join("\r\n",listLog));
			Assert.IsTrue(listLog.Any(x => x.Contains("make a new copy from "+Source(1))),string.Join("\r\n",listLog));
			//The runbook's step 4 after a failure: drop the copy, copy again, adopt.
			using(Db db=Db.Open(_server)) {
				db.Execute("DROP DATABASE `"+late+"`");
			}
			listLog.Clear();
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "copy","--original",Source(1),"--database",late },listLog.Add),string.Join("\r\n",listLog));
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "adopt","--database",late,"--original",Source(1),"--confirm",late },listLog.Add),
				string.Join("\r\n",listLog));
		}

		///<summary>A fork migration runner that is cut off in its first step, as a killed process would be.</summary>
		private class CutOffRunner : OpenDentBusiness.IForkMigrationRunner {
			private readonly OpenDentBusiness.IForkMigrationRunner _runner;

			public CutOffRunner(OpenDentBusiness.IForkMigrationRunner runner) {
				_runner=runner;
			}

			public string LatestLevel {
				get { return _runner.LatestLevel; }
			}

			public List<string> GetLevels() {
				return _runner.GetLevels();
			}

			public int CompareLevels(string levelA,string levelB) {
				return _runner.CompareLevels(levelA,levelB);
			}

			public void RunPending(Action<string> actionLog) {
				throw new ApplicationException("cut off in the fork migration for the test");
			}
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static T AssertThrows<T>(Func<object> func) where T:Exception {
			try {
				object result=func();
				Assert.Fail("Expected "+typeof(T).Name+"; the call returned "+result+".");
			}
			catch(T ex) {
				return ex;
			}
			return null;
		}

		private static void ForkMigrationsState(Db db,string database,out string dataBaseVersion,out long countForkSchemaLevel) {
			dataBaseVersion=Convert.ToString(db.Scalar("SELECT ValueString FROM `"+database+"`.preference WHERE PrefName='DataBaseVersion'"));
			countForkSchemaLevel=db.ScalarLong("SELECT COUNT(*) FROM `"+database+"`.preference WHERE PrefName='ForkSchemaLevel'");
		}

		private static void RunClient(string fileName,string arguments) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo(fileName,"--host="+_server.Server+" --port="+_server.Port+" --user="+_server.User
				+" --protocol=TCP "+arguments) {
				UseShellExecute=false,
				CreateNoWindow=true,
				RedirectStandardError=true,
			};
			if(!string.IsNullOrEmpty(_server.Password)) {
				processStartInfo.EnvironmentVariables["MYSQL_PWD"]=_server.Password;
			}
			using(Process process=Process.Start(processStartInfo)) {
				string error=process.StandardError.ReadToEnd();
				process.WaitForExit();
				Assert.AreEqual(0,process.ExitCode,error);
			}
		}

		///<summary>Negative control: a change adoption must never make (a second guardian renumbering) is caught by the same comparison.</summary>
		[TestMethod]
		public void NegativeControl_AForbiddenChangeIsCaught() {
			SkipIfNeeded();
			RunResult run=_result.ListRuns[1];
			using(Db db=Db.Open(_server)) {
				db.Use(Adopted(2));
				long changed=db.Execute("UPDATE guardian SET Relationship=23 WHERE Relationship=6");
				Assert.IsTrue(changed>0,"The synthetic data has fathers (6 since 25.1).");
				ValidationReport after=Validator.Run(db,Adopted(2),null,null);
				AllowedDifferences allowed=AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..",
					"allowed-differences.adoption.json"));
				List<Difference> listDiffs=ReportComparer.Compare(ValidationReport.Load(run.ReportBaselinePath),after,allowed);
				Assert.IsTrue(listDiffs.Any(x => !x.IsAllowed && x.Table=="guardian" && x.Column=="Relationship"),ReportComparer.Format(listDiffs));
				Assert.IsTrue(listDiffs.Any(x => !x.IsAllowed && x.Name!=null && x.Name.StartsWith("features.guardians_by_relationship")),
					"The business totals see it too.");
			}
		}
	}
}
