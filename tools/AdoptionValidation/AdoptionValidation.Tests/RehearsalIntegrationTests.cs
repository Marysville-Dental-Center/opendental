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
		public static readonly string[] ArrayCheckIds={ "G1","G2","A1","A2","A3","A4","M1","M2","H1","V1","V2","V3","V4","V5","V6","V7","V8","V9","V10","V11",
			"R1","R2","I1","I2","I3","I4" };

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
				"DataBaseVersion and ProgramVersion set to 99.0.0.0.","Adoption finished.","0 changes.","The schema matches Open Dental 25.3.48 exactly","Rehash finished.",
				"Row check against "+Source(1),"preference: 5 added (","documentmisc: 4 Open Dental update-file row(s) deleted, none left; 0 other row(s) deleted",
				"securitylog: 2 entries added (1 adoption, 1 rehash)","updatehistory: identical row by row." })
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
			foreach(string step in new[] { "== 0.2 ","== 1.1 ","== 3.2 ","== 3.3 ","== 4-5 ","== 6.1 ","== 10.1 ","== 10.2 " }) {
				StringAssert.Contains(output,step);
			}
			StringAssert.Contains(File.ReadAllText(Path.Combine(folder,"float-check.txt")),"no value that a logical backup (mariadb-dump) would round");
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
				AdoptionRowCheck.Result result=AdoptionRowCheck.Run(db,Source(2),tampered);
				string failures=string.Join("\r\n",result.ListFailures);
				Assert.IsFalse(result.Passed);
				StringAssert.Contains(failures,"Preference PracticeTitle");
				StringAssert.Contains(failures,"Existing securitylog rows were removed or changed");
				StringAssert.Contains(failures,"documentmisc row(s) that are not Open Dental update files were deleted");
				StringAssert.Contains(failures,"updatehistory changed");
				//The report comparison under allowed-differences.adoption.json lets the first three through; updatehistory has no rule any more.
				AllowedDifferences allowed=AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..",
					"allowed-differences.adoption.json"));
				List<Difference> listDiffs=ReportComparer.Compare(before,Validator.Run(db,tampered,null,null),allowed);
				Assert.IsTrue(listDiffs.Where(x => x.Table=="preference" || x.Table=="securitylog" || x.Table=="documentmisc").All(x => x.IsAllowed),
					ReportComparer.Format(listDiffs));
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
