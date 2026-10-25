// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using SyntheticData.Generation;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	public static class Program {
		public const int ExitPass=0;
		public const int ExitError=1;
		public const int ExitUsage=2;
		public const int ExitNetworkAttempted=3;
		public const int ExitValidationFailed=6;

		public const string Usage=@"AdoptionValidation: the M1-Z adoption rehearsal on synthetic data, and the headless steps of the office runbook
(docs/adoption-runbook.md).

Usage:
  AdoptionValidation rehearse [--size small|medium|large] [--seed N] [--as-of yyyy-MM-dd] [--runs 2] [--fill-remaining 2]
                              [--families N] [--years N] [--copy dump|sql] [--out DIR] [--keep] [--skip-roundtrip] [--tag TAG]
                              [--schema-sql PATH | --schema-from DB] [--allowed FILE]
  AdoptionValidation restore --dump FILE --database NEW_DB
  AdoptionValidation adopt --database COPY --original ORIGINAL --confirm COPY [--log FILE]
  AdoptionValidation credentials --database DB
  AdoptionValidation startup-check --database DB
  AdoptionValidation roundtrip --database synth_...

Commands:
  rehearse       Generate a synthetic Open Dental 25.3.48 database (default: medium), record its baseline, back it up and restore a
                 copy, adopt the copy (the same code as the adopt command), and validate: row counts, checksums, business totals,
                 the 24.4-25.3 columns through OpenDentBusiness, the dropped office column, the removed update files, the version
                 sentinels and an Open Dental-style startup check. Then everything a second time on the adopted copy. Repeats the
                 whole run --runs times (default 2) and checks the runs agree. Every step is timed. Writes rehearsal-result.json,
                 rehearsal-result.md and the reports to --out. Scratch databases are named synth_m1z_<tag>_r<n>_src and _adopted
                 and are dropped afterwards unless --keep.
  restore        Restores a backup file as a NEW database (runbook steps 0.6 and 3.2). Refuses a database name that exists, and a
                 file that names a database (USE, CREATE/DROP/ALTER DATABASE, a table name qualified with a database): a
                 --databases or --all-databases dump would also write to the database it names. Loads with mariadb --one-database.
  adopt          Adopts a restored copy without starting the program (runbook steps 4 and 5): Adoption.Preflight and Adoption.Run,
                 the fork migration with every step again, the schema verifier and SecurityHash.RehashNow, exactly as the rehearsal
                 does. Before it changes anything it checks that --confirm repeats --database, that --original is a different,
                 unadopted Open Dental database, that nobody else is connected to either, and that a copy not adopted yet has the
                 original's tables and row counts. Run it again to finish an interrupted adoption; on a finished one it changes
                 nothing but its security log entry. --log appends everything it prints to FILE.
  credentials    Lists the stored passwords of a database that the fork cannot read (to re-enter after adoption). Read-only;
                 names and rows only, never values.
  startup-check  Shows what an Open Dental client of each version would do at startup on the database (the model of its
                 version checks). Read-only. Exit code 0 when every Open Dental release client refuses the database, 6 when one
                 would use it.
  roundtrip      Runs only the data-layer round trip on an adopted scratch database (it writes; synth* databases only).

Connection: --server (localhost, 127.0.0.1 or ::1 only), --port 3306, --user root; the password, if any, from the
SYNTH_DB_PASSWORD environment variable. Exit codes: 0 pass, 1 error, 2 usage or refused, 3 network access attempted,
6 a validation failed.";

		public static int Main(string[] args) {
			NetworkGuard.Install();
			int exitCode;
			try {
				exitCode=Run(args,Console.WriteLine);
			}
			catch(ArgumentException ex) {
				Console.Error.WriteLine(ex.Message);
				Console.Error.WriteLine();
				Console.Error.WriteLine(Usage);
				exitCode=ExitUsage;
			}
			catch(RefusedException ex) {
				Console.Error.WriteLine("Refused: "+ex.Message);
				exitCode=ExitUsage;
			}
			catch(Exception ex) {
				Console.Error.WriteLine("Failed: "+ex);
				exitCode=ExitError;
			}
			if(NetworkGuard.ListBlocked.Count>0) {
				Console.Error.WriteLine("Outbound network requests were attempted and blocked: "+string.Join(", ",NetworkGuard.ListBlocked));
				exitCode=ExitNetworkAttempted;
			}
			return exitCode;
		}

		public static int Run(string[] args,Action<string> log) {
			if(args.Length==0 || args.Any(x => x=="--help" || x=="-h" || x=="/?")) {
				log(Usage);
				return ExitPass;
			}
			string command=args[0].ToLowerInvariant();
			RehearsalOptions options=new RehearsalOptions();
			string database=null;
			string original=null;
			string confirm=null;
			string pathLog=null;
			string pathDump=null;
			for(int i=1;i<args.Length;i++) {
				string arg=args[i];
				string Next() {
					if(i+1>=args.Length) {
						throw new ArgumentException(arg+" needs a value.");
					}
					return args[++i];
				}
				switch(arg.ToLowerInvariant()) {
					case "--server": options.Server.Server=Next(); break;
					case "--port": options.Server.Port=ParseInt(arg,Next()); break;
					case "--user": options.Server.User=Next(); break;
					case "--database": database=Next(); break;
					case "--original": original=Next(); break;
					case "--confirm": confirm=Next(); break;
					case "--log": pathLog=Next(); break;
					case "--dump": pathDump=Next(); break;
					case "--size": options.Size=GenerationSettings.ParseSize(Next()); break;
					case "--seed": options.Seed=long.Parse(Next(),CultureInfo.InvariantCulture); break;
					case "--as-of": options.AsOf=DateTime.ParseExact(Next(),"yyyy-MM-dd",CultureInfo.InvariantCulture); break;
					case "--runs": options.Runs=Math.Max(1,ParseInt(arg,Next())); break;
					case "--fill-remaining": options.FillRemaining=ParseInt(arg,Next()); break;
					case "--families": options.Families=ParseInt(arg,Next()); break;
					case "--years": options.Years=ParseInt(arg,Next()); break;
					case "--copy":
						string copy=Next().ToLowerInvariant();
						if(copy!="dump" && copy!="sql") {
							throw new ArgumentException("--copy is dump or sql.");
						}
						options.Copy=copy=="dump" ? CopyMethod.Dump : CopyMethod.Sql;
						break;
					case "--out": options.OutDir=Next(); break;
					case "--keep": options.Keep=true; break;
					case "--skip-roundtrip": options.SkipRoundTrip=true; break;
					case "--tag":
						options.Tag=Next();
						if(!LocalOnly.IsWritableDatabaseName(Rehearsal.DbPrefix+options.Tag+"_r9_adopted")) {
							throw new ArgumentException("--tag may only hold lower-case letters, digits and underscores.");
						}
						break;
					case "--schema-sql": options.SchemaSql=Next(); options.SchemaFrom=null; break;
					case "--schema-from": options.SchemaFrom=Next(); options.SchemaSql=null; break;
					case "--allowed": options.AllowedPath=Next(); break;
					default:
						throw new ArgumentException("Unknown argument: "+arg);
				}
			}
			switch(command) {
				case "rehearse":
					if(options.Copy==CopyMethod.Dump && DatabaseCopier.FindClientTools()==null) {
						throw new ArgumentException("--copy dump needs mariadb-dump and mariadb (set MARIADB_BIN to their folder).");
					}
					options.OutDir=options.OutDir??Path.Combine(Environment.CurrentDirectory,"adoption-rehearsal-"
						+DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture));
					RehearsalResult result=new Rehearsal(options,log).Run();
					log("");
					foreach(RunResult run in result.ListRuns) {
						log("Run "+run.RunNumber+": "+(run.Passed ? "PASS" : "FAIL")+", "+run.ListChecks.Count(x => x.Passed)+" of "+run.ListChecks.Count+" checks passed, "
							+run.TotalSeconds.ToString("0.0")+" s"+(run.Error==null ? "" : ", error: "+run.Error));
					}
					foreach(CheckResult check in result.ListCrossRunChecks) {
						log(check.ToString());
					}
					log("Rehearsal: "+(result.Passed ? "PASS" : "FAIL"));
					return result.Passed ? ExitPass : ExitValidationFailed;
				case "restore":
					RequireDatabase(database);
					if(string.IsNullOrEmpty(pathDump)) {
						throw new ArgumentException("--dump is required: the backup file to restore.");
					}
					return RunRestore(options.Server,pathDump,database,log);
				case "adopt":
					RequireDatabase(database);
					if(string.IsNullOrEmpty(original)) {
						throw new ArgumentException("--original is required: the Open Dental database the copy was restored from.");
					}
					LocalOnly.EnsureSafeIdentifier(original);
					if(confirm!=database) {
						throw new ArgumentException("--confirm must repeat the database to adopt exactly: --confirm "+database);
					}
					return RunAdopt(options.Server,database,original,pathLog,log);
				case "credentials":
					RequireDatabase(database);
					using(Db db=Db.Open(options.Server)) {
						List<CredentialInventory.Item> listItems=CredentialInventory.Run(db,database);
						log(listItems.Count+" stored password(s) in "+database+" that the fork cannot read; re-enter them after adoption:");
						foreach(CredentialInventory.Item item in listItems) {
							log("  "+item);
						}
					}
					return ExitPass;
				case "roundtrip":
					RequireDatabase(database);
					LocalOnly.EnsureWritableDatabaseName(database);
					BusinessLayer.Connect(options.Server,database);
					using(Db db=Db.Open(options.Server)) {
						RoundTripSummary summary=DataLayerRoundTrip.Run(db,database,log);
						log("Round trip: "+(summary.Passed ? "PASS" : "FAIL"));
						return summary.Passed ? ExitPass : ExitValidationFailed;
					}
				case "startup-check":
					RequireDatabase(database);
					BusinessLayer.Connect(options.Server,database);
					OdDatabaseState state=OdDatabaseState.Read();
					log("DataBaseVersion "+state.DataBaseVersion+", ProgramVersion "+state.ProgramVersion+", "+state.CountUpdateFileRows+" Open Dental update-file rows.");
					int countReleaseUsing=0;
					foreach((string name,OdClient client,bool isClassicView) scenario in OdStartupCheck.StandardScenarios()) {
						OdDatabaseState stateScenario=OdDatabaseState.Read();
						stateScenario.IsClassicUpdateView|=scenario.isClassicView;
						OdStartupResult startupResult=OdStartupCheck.Evaluate(scenario.client,stateScenario);
						if(!scenario.client.IsDebugBuild && !startupResult.IsRefused) {
							countReleaseUsing++;
						}
						log(scenario.name+": "+startupResult.Describe());
						foreach(string trace in startupResult.ListTrace) {
							log("    "+trace);
						}
					}
					log(countReleaseUsing==0 ? "Every Open Dental release client refuses this database."
						: countReleaseUsing+" Open Dental release client scenario(s) would use this database.");
					return countReleaseUsing==0 ? ExitPass : ExitValidationFailed;
				default:
					throw new ArgumentException("Unknown command: "+command);
			}
		}

		///<summary>Runbook steps 0.6 and 3.2: restores a backup file as a new database (DatabaseCopier.Restore checks the file first).</summary>
		private static int RunRestore(ServerSettings server,string pathDump,string database,Action<string> log) {
			(string dump,string client)? tools=DatabaseCopier.FindClientTools();
			if(tools==null) {
				throw new ArgumentException("restore needs the mariadb client program (set MARIADB_BIN to the MariaDB bin folder).");
			}
			if(!File.Exists(pathDump)) {
				throw new RefusedException("The backup file "+pathDump+" does not exist.");
			}
			log("Checking "+pathDump+" ("+new FileInfo(pathDump).Length.ToString("N0")+" bytes) and restoring it as the new database "+database+".");
			Stopwatch stopwatch=Stopwatch.StartNew();
			DatabaseCopier.Restore(server,pathDump,database,tools.Value.client);
			using(Db db=Db.Open(server)) {
				long countTables=db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database));
				log("Restored "+countTables+" tables into "+database+" in "+stopwatch.Elapsed.TotalSeconds.ToString("0.0",CultureInfo.InvariantCulture)+" s.");
			}
			return ExitPass;
		}

		///<summary>Runbook steps 4 and 5: the headless adoption of a restored copy (HeadlessAdoption, the code the rehearsal adopts with).</summary>
		private static int RunAdopt(ServerSettings server,string database,string original,string pathLog,Action<string> logConsole) {
			void Log(string text) {
				logConsole(text);
				if(pathLog!=null) {
					File.AppendAllText(pathLog,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)+"  "+text+Environment.NewLine,
						new System.Text.UTF8Encoding(false));
				}
			}
			Log("AdoptionValidation adopt: "+database+", restored copy of "+original+", on "+server.Describe()+".");
			try {
				using(Db db=Db.Open(server)) {
					HeadlessAdoption.CheckCopy(db,database,original,Log);
				}
			}
			catch(RefusedException ex) {
				Log("Refused: "+ex.Message);
				Log("Nothing was changed.");
				return ExitUsage;
			}
			BusinessLayer.Connect(server,database);
			HeadlessAdoption.Result result;
			try {
				result=HeadlessAdoption.Run(database,Log,(name,action) => {
					Log("- "+name);
					Stopwatch stopwatch=Stopwatch.StartNew();
					action();
					Log("  ("+stopwatch.Elapsed.TotalSeconds.ToString("0.0",CultureInfo.InvariantCulture)+" s)");
				});
			}
			catch(Exception ex) {
				Exception exInner=ex is System.Reflection.TargetInvocationException && ex.InnerException!=null ? ex.InnerException : ex;
				Log("Adoption stopped: "+exInner.Message);
				Log("Every step can be run again: run the same command again to finish. If it stops again, go to Rollback (runbook).");
				return ExitError;
			}
			if(result.Passed) {
				Log("Adopt: PASS. "+database+" is adopted, matches Open Dental "+OpenDentBusiness.ForkMigrations.SchemaVerifier.ReferenceVersion
					+" and is rehashed. Validate it now (runbook step 6), before anyone starts the program.");
				return ExitPass;
			}
			foreach(string failure in result.ListFailures) {
				Log("FAIL: "+failure);
			}
			Log("Adopt: FAIL. Go to Rollback (runbook); keep "+database+" and this log for the analysis.");
			return ExitValidationFailed;
		}

		private static void RequireDatabase(string database) {
			if(string.IsNullOrEmpty(database)) {
				throw new ArgumentException("--database is required.");
			}
			LocalOnly.EnsureSafeIdentifier(database);
		}

		private static int ParseInt(string name,string value) {
			if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int result) || result<0) {
				throw new ArgumentException(name+" needs a whole number, not '"+value+"'.");
			}
			return result;
		}
	}
}
