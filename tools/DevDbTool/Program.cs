// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using CodeBase;
using OpenDentBusiness;

namespace DevDbTool {
	public static class Program {
		public const int ExitOk=0;
		public const int ExitError=1;
		public const int ExitRefused=2;
		public const int ExitNetworkAttempted=3;
		public const int ExitVerifyFailed=4;

		public static int Main(string[] args) {
			Options options;
			try {
				options=Options.Parse(args);
			}
			catch(ArgumentException ex) {
				Console.Error.WriteLine(ex.Message);
				Console.Error.WriteLine();
				Console.Error.WriteLine(Options.Usage);
				return ExitRefused;
			}
			if(options.ShowHelp) {
				Console.WriteLine(Options.Usage);
				return ExitOk;
			}
			Stopwatch stopwatch=Stopwatch.StartNew();
			int exitCode;
			try {
				ODInitialize.Initialize();
				NetworkGuard.Install();
				ODEvent.Fired+=ODEvent_Fired;
				exitCode=options.VerifyOnly ? RunVerify(options) : RunCreate(options);
			}
			catch(RefusedException ex) {
				Console.Error.WriteLine("Refused: "+ex.Message);
				exitCode=ExitRefused;
			}
			catch(Exception ex) {
				Console.Error.WriteLine("FAILED: "+ex.Message);
				Console.Error.WriteLine(ex.ToString());
				exitCode=ExitError;
			}
			List<string> listBlocked=NetworkGuard.ListBlocked;
			if(listBlocked.Count>0) {
				Console.Error.WriteLine();
				Console.Error.WriteLine("WARNING: "+listBlocked.Count+" outbound network request(s) were attempted and blocked:");
				foreach(string destination in listBlocked) {
					Console.Error.WriteLine("  "+destination);
				}
				Console.Error.WriteLine("Building a dev database must not contact any server. Report this; do not work around it.");
				if(exitCode==ExitOk) {
					exitCode=ExitNetworkAttempted;
				}
			}
			Info("Done in "+stopwatch.Elapsed.ToString(@"m\:ss")+" (exit code "+exitCode+")");
			return exitCode;
		}

		private static int RunCreate(Options options) {
			string dumpPath=options.DumpPath!="" ? options.DumpPath : DevDatabase.FindDefaultDump();
			if(dumpPath==null) {
				throw new RefusedException("Could not find UnitTests\\Resources\\dump.txt above "+AppDomain.CurrentDomain.BaseDirectory
					+". Pass --dump.");
			}
			DevDatabase devDatabase=new DevDatabase(options);
			devDatabase.ConnectToServer();
			devDatabase.CheckServer();
			if(devDatabase.DatabaseExists()) {
				if(!options.Force) {
					throw new RefusedException("Database '"+options.Database+"' already exists. Use --force (-Force) to drop and recreate it, "
						+"or --verify-only (-VerifyOnly) to check it.");
				}
				devDatabase.Drop();
			}
			devDatabase.Create();
			devDatabase.ConnectToDatabase(skipValidation:true);
			devDatabase.LoadDump(dumpPath);
			devDatabase.ConnectToDatabase(skipValidation:false);
			devDatabase.Upgrade();
			devDatabase.StampProgramVersion();//Before the fork migrations: adoption needs ProgramVersion to agree with DataBaseVersion.
			if(!options.SkipForkMigrations) {
				devDatabase.RunForkMigrations();
			}
			devDatabase.ApplyDefaults();
			if(options.AtoZPath!="") {
				devDatabase.SetAtoZFolder(options.AtoZPath);
			}
			if(options.WithSampleData) {
				SampleData.Insert();
			}
			return Verify(options);
		}

		private static int RunVerify(Options options) {
			DevDatabase devDatabase=new DevDatabase(options);
			devDatabase.ConnectToServer();
			if(!devDatabase.DatabaseExists()) {
				throw new RefusedException("Database '"+options.Database+"' does not exist.");
			}
			devDatabase.ConnectToDatabase(skipValidation:true);
			return Verify(options);
		}

		private static int Verify(Options options) {
			Info("Verifying "+options.Database);
			SchemaCheck check=SchemaCheck.Run(options.Database,!options.SkipForkMigrations);
			foreach(string note in check.ListNotes) {
				Info("  "+note);
			}
			Info("  Checked "+check.CountTables+" tables and "+check.CountColumns+" columns ("+check.CountHqOnlySkipped
				+" HQ-only table types skipped).");
			if(!check.IsOk) {
				foreach(string problem in check.ListProblems) {
					Console.Error.WriteLine("  PROBLEM: "+problem);
				}
				Console.Error.WriteLine("Verification FAILED with "+check.ListProblems.Count+" problem(s).");
				return ExitVerifyFailed;
			}
			if(options.SkipForkMigrations) {
				Info("Verification passed: DataBaseVersion "+DevDatabase.GetPrefRaw(PrefName.DataBaseVersion)+" matches program version "
					+DevDatabase.VersionProgram+".");
			}
			else {
				Info("Verification passed: adopted database at fork schema level "+DevDatabase.GetPrefRaw(ForkVersions.PREF_FORK_SCHEMA_LEVEL)
					+", schema of Open Dental "+DevDatabase.GetPrefRaw(ForkVersions.PREF_OD_SCHEMA_COMPAT)+".");
			}
			return ExitOk;
		}

		private static void ODEvent_Fired(ODEventArgs e) {
			if(e.EventType==ODEventType.ConvertDatabases && e.Tag is string message) {
				Info("  "+message);
			}
		}

		public static void Info(string message) {
			Console.WriteLine(message);
		}

		public static void Warn(string message) {
			Console.Error.WriteLine("WARNING: "+message);
		}
	}
}
