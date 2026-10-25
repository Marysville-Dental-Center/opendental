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

		public const string Usage=@"AdoptionValidation: the M1-Z adoption rehearsal on synthetic data, and the steps of the office runbook that run without the
program (docs/adoption-runbook.md).

Usage:
  AdoptionValidation rehearse [--size small|medium|large] [--seed N] [--as-of yyyy-MM-dd] [--runs 2] [--fill-remaining 2]
                              [--families N] [--years N] [--copy server|dump] [--out DIR] [--keep] [--skip-roundtrip]
                              [--prefix synth_m1z_] [--tag TAG] [--schema-sql PATH | --schema-from DB] [--allowed FILE]
  AdoptionValidation connections --database DB
  AdoptionValidation float-check --database DB
  AdoptionValidation migration-preview --database DB
  AdoptionValidation copy --original ORIGINAL --database NEW_DB
  AdoptionValidation adopt --database COPY --original ORIGINAL --confirm COPY [--log FILE]
  AdoptionValidation credentials --database DB
  AdoptionValidation startup-check --database DB
  AdoptionValidation restore --dump FILE --database NEW_DB
  AdoptionValidation roundtrip --database synth_...

Commands:
  rehearse       Generate a synthetic Open Dental 25.3.48 database (default: medium), record its baseline, copy it the way the
                 runbook does (default; --copy dump backs it up with mariadb-dump and restores it instead), adopt the copy (the
                 same code as the adopt command), and validate: row counts, checksums, business totals, the rows adoption may
                 change, the 24.4-25.3 columns through OpenDentBusiness, the dropped office column, the removed update files, the
                 version sentinels and an Open Dental-style startup check. Then everything a second time on the adopted copy.
                 Repeats the whole run --runs times (default 2) and checks the runs agree. Every step is timed. Writes
                 rehearsal-result.json, rehearsal-result.md and the reports to --out. Scratch databases are named
                 <prefix><tag>_r<n>_src, _adopted and _snap and are dropped afterwards unless --keep.
  connections    Lists every other connection that has a default database (who is connected, from which computer). Read-only.
                 Exit code 0 when none uses DB, 6 when one does (runbook steps 1.1, 10.1, 11.1 and Rollback).
  float-check    Counts the FLOAT and DOUBLE values of DB that a logical backup (mariadb-dump) would not restore exactly (runbook
                 step 0.2). Read-only; names columns, never values. Exit code 0 when there are none, 6 when there are some.
  migration-preview
                 Lists what adoption's fork migration will change in the rows of DB, an Open Dental 25.3.48 database (runbook step
                 0.2): the displayreport rows of Production by Procedure, Procedures (individual) and Insurance Pending
                 Supplementals that Open Dental wrote under an internal name this program does not know, which step 2026101971
                 renames (InternalName only); adopt's row check later requires exactly these renames. Read-only. Exit code 0.
  copy         Copies ORIGINAL into the NEW database on the same server (runbook step 3.2): CREATE TABLE ... LIKE, the
                 AUTO_INCREMENT counters and INSERT ... SELECT, so the copy is exact (FLOAT values included) and no row leaves the
                 server. Refuses a name that exists, a missing original, the same name twice, any connection to either, and
                 views, triggers, routines or events it would not carry; then checks every table's row count and AUTO_INCREMENT.
  adopt          Adopts the copy without starting the program (runbook steps 4 and 5): Adoption.Preflight and Adoption.Run,
                 the fork migration with every step again, the schema verifier and SecurityHash.RehashNow, exactly as the rehearsal
                 does, then checks row by row against --original what adoption changed in preference, documentmisc, securitylog,
                 securityloghash, displayreport and updatehistory. Before it changes anything it checks that --confirm repeats --database, that
                 --original is a different, unadopted Open Dental database, that nobody else is connected to either, and that a
                 copy not adopted yet has the original's tables and row counts. On a finished adoption it changes nothing but its
                 security log entry. If it stops or fails, do not run it again on that copy: drop the copy and make a new one
                 (runbook step 4). --log appends everything it prints to FILE.
  credentials    Lists the stored passwords of a database that the fork cannot read (to re-enter after adoption). Read-only;
                 names and rows only, never values.
  startup-check  Shows what an Open Dental client of each version would do at startup on the database (the model of its
                 version checks). Read-only. Exit code 0 when every Open Dental release client refuses the database, 6 when one
                 would use it.
  restore        Restores a backup file as a NEW database. Refuses a database name that exists, and a file that names a database
                 (USE, CREATE/DROP/ALTER DATABASE, a table name qualified with a database): a --databases or --all-databases dump
                 would also write to the database it names. Loads with mariadb --one-database. Not used by the runbook.
  roundtrip      Runs only the data-layer round trip on an adopted scratch database (it writes; synth* databases only).

Connection: --server (localhost, 127.0.0.1 or ::1 only), --port 3306, --user root; the password, if any, from the
SYNTH_DB_PASSWORD environment variable. On the office's admin PC the port is the local end of an SSH forward to the
database server (runbook step 0.0). Exit codes: 0 pass, 1 error, 2 usage or refused, 3 network access attempted,
6 a validation failed (or, for connections and float-check, something was found).";

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
						if(copy!="dump" && copy!="server" && copy!="sql") {
							throw new ArgumentException("--copy is server (the runbook's copy, the default) or dump.");
						}
						options.Copy=copy=="dump" ? CopyMethod.Dump : CopyMethod.Server;
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
					case "--prefix":
						options.Prefix=Next();
						if(!options.Prefix.EndsWith("_") || !LocalOnly.IsWritableDatabaseName(options.Prefix+"x")) {
							throw new ArgumentException("--prefix must start with synth, end with an underscore and hold only lower-case letters, digits and "
								+"underscores, for example synth_m1z_.");
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
				case "connections":
					RequireDatabase(database);
					using(Db db=Db.Open(options.Server)) {
						List<string> listAll=HeadlessAdoption.AllOtherConnections(db);
						List<string> listUsing=HeadlessAdoption.OtherConnections(db,new[] { database });
						log("Connections to "+options.Server.Describe()+" other than this one, with a database: "+(listAll.Count==0 ? "none." : listAll.Count+"."));
						foreach(string connection in listAll) {
							log("  "+connection);
						}
						log(listUsing.Count==0 ? "No connection uses "+database+"."
							: listUsing.Count+" connection(s) use "+database+": close the program on the computer each one names (from ...).");
						return listUsing.Count==0 ? ExitPass : ExitValidationFailed;
					}
				case "float-check":
					RequireDatabase(database);
					using(Db db=Db.Open(options.Server)) {
						FloatCheck.Result floatResult=FloatCheck.Run(db,database);
						log(database+": "+floatResult.Describe()+".");
						if(floatResult.Total>0) {
							log("Programs read these values through the same 6-digit text, so they show the same before and after a dump, but CHECKSUM TABLE differs. "
								+"The runbook's copies are exact (steps 3.1 and 3.2); a logical backup (nightly dump) of this database is not, for these values.");
						}
						return floatResult.Total==0 ? ExitPass : ExitValidationFailed;
					}
				case "migration-preview":
					RequireDatabase(database);
					using(Db db=Db.Open(options.Server)) {
						foreach(string line in MigrationPreview.Run(db,database).Lines()) {
							log(line);
						}
					}
					return ExitPass;
				case "copy":
					RequireDatabase(database);
					if(string.IsNullOrEmpty(original)) {
						throw new ArgumentException("--original is required: the database to copy.");
					}
					LocalOnly.EnsureSafeIdentifier(original);
					try {
						using(Db db=Db.Open(options.Server)) {
							DatabaseCopier.CopyOnServer(db,original,database,log);
						}
					}
					catch(ApplicationException ex) {
						log("Copy: FAIL. "+ex.Message);
						return ExitError;
					}
					log("Copy: PASS. Compare the two databases now (runbook step 3.3).");
					return ExitPass;
				case "restore":
					RequireDatabase(database);
					if(string.IsNullOrEmpty(pathDump)) {
						throw new ArgumentException("--dump is required: the backup file to restore.");
					}
					return RunRestore(options.Server,pathDump,database,log);
				case "adopt":
					RequireDatabase(database);
					if(string.IsNullOrEmpty(original)) {
						throw new ArgumentException("--original is required: the Open Dental database the copy was made from.");
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

		///<summary>Restores a backup file as a new database (DatabaseCopier.Restore checks the file first). The runbook no longer uses it.</summary>
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

		///<summary>Runbook steps 4 and 5: the headless adoption of the copy (HeadlessAdoption, the code the rehearsal adopts with).</summary>
		private static int RunAdopt(ServerSettings server,string database,string original,string pathLog,Action<string> logConsole) {
			void Log(string text) {
				logConsole(text);
				if(pathLog!=null) {
					File.AppendAllText(pathLog,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)+"  "+text+Environment.NewLine,
						new System.Text.UTF8Encoding(false));
				}
			}
			Log("AdoptionValidation adopt: "+database+", copy of "+original+", on "+server.Describe()+".");
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
				},server,original);
			}
			catch(Exception ex) {
				Exception exInner=ex is System.Reflection.TargetInvocationException && ex.InnerException!=null ? ex.InnerException : ex;
				Log("Adoption stopped: "+exInner.Message);
				Log("Adopt: STOPPED. Do not run adopt on "+database+" again: keep this log, drop "+database+", make a new copy from "+original
					+" and adopt that (runbook step 4). "+original+" is unchanged.");
				return ExitError;
			}
			if(result.Passed) {
				List<string> listRenames=result.RowCheck?.ListReportRenames??new List<string>();
				Log("Adopt: PASS. "+database+" is adopted, matches Open Dental "+OpenDentBusiness.ForkMigrations.SchemaVerifier.ReferenceVersion
					+", is rehashed, and changed only the rows adoption changes (row check above"
					+(listRenames.Count==0 ? "; the fork migration renamed no report row" : "; the fork migration renamed "+listRenames.Count+" report row(s), as "
						+"migration-preview lists: "+string.Join("; ",listRenames))
					+"). Validate it now (runbook step 6.1), before anyone starts the program.");
				return ExitPass;
			}
			foreach(string failure in result.ListFailures) {
				Log("FAIL: "+failure);
			}
			Log("Adopt: FAIL. Do not use "+database+": keep this log, drop "+database+", make a new copy from "+original+" and adopt that (runbook step 4); "
				+"a second failure means Rollback. "+original+" is unchanged.");
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
