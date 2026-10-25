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
		public static readonly string[] ArrayCheckIds={ "G1","G2","A1","A2","A3","A4","M1","M2","H1","V1","V2","V3","V4","V5","V6","V7","V8","V9","V10","R1",
			"R2","I1","I2","I3","I4" };

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
				foreach(string prefix in new[] { "Generate","Record baseline","Adopt (","Fork migration","Schema verifier","Rehash","Record post-adoption",
					"Data-layer round trip","Second pass" })
				{
					Assert.IsTrue(listNames.Any(x => x.StartsWith(prefix)),"Run "+run.RunNumber+" has no step "+prefix);
				}
				if(DatabaseCopier.FindClientTools()!=null) {
					Assert.IsTrue(listNames.Contains("Back up (mariadb-dump)") && listNames.Contains("Restore the copy"),"A real backup and restore is timed.");
				}
				Assert.IsTrue(run.ListSteps.All(x => x.Seconds>=0) && run.TotalSeconds>0);
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
			Assert.IsTrue(listSteps.IndexOf("Preflight")<indexAdopt && indexAdopt<listSteps.IndexOf("Fork migration (all steps again)")
				&& listSteps.IndexOf("Schema verifier")<indexRehash && indexRehash<indexReport && indexReport<indexRoundTrip,string.Join(", ",listSteps));
			foreach(string expected in new[] { "It can be adopted.","Dropped index patient.portal_active_email_uniq.","Dropped column patient._portal_active_email.",
				"DataBaseVersion and ProgramVersion set to 99.0.0.0.","Adoption finished.","0 changes.","The schema matches Open Dental 25.3.48 exactly","Rehash finished." })
			{
				Assert.IsTrue(run.ListAdoptionLog.Any(x => x.Contains(expected)),expected+"\r\n"+string.Join("\r\n",run.ListAdoptionLog));
			}
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
			(string dump,string client)? tools=DatabaseCopier.FindClientTools();
			if(tools==null) {
				Assert.Inconclusive("mariadb-dump and mariadb are not installed.");
			}
			string folder=Path.Combine(_outDir,"adopt-refusals");
			Directory.CreateDirectory(folder);
			string pathBackup=Path.Combine(folder,"source.sql");
			DatabaseCopier.Backup(_server,Source(1),pathBackup,tools.Value.dump);
			string copy=Rehearsal.DbPrefix+Tag+"_refusals";
			DatabaseCopier.Restore(_server,pathBackup,copy,tools.Value.client);
			List<string> listLog=new List<string>();
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

		///<summary>The dry run of runbook step 0.6: Invoke-RunbookDryRun.ps1 runs steps 3.1 to 6 and the go-live check with the runbook's own commands
		///(mariadb-dump, AdoptionValidation restore, SyntheticData validate and compare, AdoptionValidation adopt and startup-check) on a copy of the
		///rehearsal's source, out of process, as the office runs them. Then step 4 is run again, as the runbook says to after an interruption.</summary>
		[TestMethod]
		public void RunbookDryRun_PassesWithTheRunbookCommands() {
			SkipIfNeeded();
			if(DatabaseCopier.FindClientTools()==null) {
				Assert.Inconclusive("mariadb-dump and mariadb are not installed.");
			}
			string tools=AppDomain.CurrentDomain.BaseDirectory;
			string script=Path.Combine(tools,"Invoke-RunbookDryRun.ps1");
			for(DirectoryInfo dir=new DirectoryInfo(tools);!File.Exists(script) && dir!=null;dir=dir.Parent) {
				script=Path.Combine(dir.FullName,"tools","AdoptionValidation","Invoke-RunbookDryRun.ps1");
			}
			Assert.IsTrue(File.Exists(script),"Invoke-RunbookDryRun.ps1 not found");
			string copy=Rehearsal.DbPrefix+Tag+"_dryrun";
			string folder=Path.Combine(_outDir,"dry-run");
			ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Original "+Source(1)
				+" -Copy "+copy+" -Folder \""+folder+"\" -AsOf 2026-09-30 -Tools \""+tools.TrimEnd('\\')+"\"") {
				UseShellExecute=false,
				CreateNoWindow=true,
				RedirectStandardOutput=true,
				RedirectStandardError=true,
			};
			string output;
			int exitCode;
			using(Process process=Process.Start(processStartInfo)) {
				System.Threading.Tasks.Task<string> taskErr=process.StandardError.ReadToEndAsync();
				output=process.StandardOutput.ReadToEnd();
				Assert.IsTrue(process.WaitForExit(600000),"The dry run did not finish in 10 minutes.");
				output+=taskErr.Result;
				exitCode=process.ExitCode;
			}
			Assert.AreEqual(0,exitCode,output);
			StringAssert.Contains(output,"Dry run: PASS");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"compare.txt")),"Unexpected differences: 0");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"compare-original.txt")),"Unexpected differences: 0");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"adoption-log.txt")),"Adopt: PASS");
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"startup-check.txt")),"Every Open Dental release client refuses this database.");
			Assert.IsTrue(File.Exists(Path.Combine(folder,"dry-run-timings.txt")));
			Assert.IsFalse(Directory.GetFiles(folder,"*.sql").Any(),"The backup file is deleted at the end.");
			using(Db db=Db.Open(_server)) {
				ForkMigrationsState(db,copy,out string version,out long markers);
				Assert.AreEqual("99.0.0.0",version);
				Assert.AreEqual(1,markers);
			}
			//Step 4 again, in process: it finishes or repeats the adoption, and changes nothing but its own security log entry.
			List<string> listLog=new List<string>();
			ValidationReport before;
			using(Db db=Db.Open(_server)) {
				before=Validator.Run(db,copy,null,null);
			}
			Assert.AreEqual(Program.ExitPass,Program.Run(new[] { "adopt","--database",copy,"--original",Source(1),"--confirm",copy },listLog.Add),
				string.Join("\r\n",listLog));
			Assert.IsTrue(listLog.Any(x => x.Contains("already has an adoption record")) && listLog.Any(x => x.Contains("Adoption is skipped")));
			using(Db db=Db.Open(_server)) {
				List<Difference> listDiffs=ReportComparer.Compare(before,Validator.Run(db,copy,null,null),null);
				Assert.IsTrue(listDiffs.All(x => x.Table=="securitylog" || x.Table=="securityloghash"),ReportComparer.Format(listDiffs));
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
