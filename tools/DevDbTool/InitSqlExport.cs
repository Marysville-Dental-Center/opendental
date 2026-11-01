// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CodeBase;
using DataConnectionBase;
using OpenDentBusiness;

namespace DevDbTool {
	///<summary>--export-init-sql: writes the SQL file that initializes a new database (db/init/new-database.sql). It builds a scratch database the
	///same way as the normal create command (seed dump, frozen convert chain to 24.3.63, adoption, fork migrations, default auto codes, procedure
	///buttons and quick-add procedures; no sample data and no A to Z folder), checks it like --verify-only, removes everything that belongs to this
	///PC or to this run (InitSqlScrub), writes it with InitSqlWriter and drops the scratch database. The same source always gives the same bytes,
	///and no MariaDB client tool is needed.</summary>
	public static class InitSqlExport {
		///<summary>Scratch databases of this command are named initsql_ plus the run id, unless --database is given.</summary>
		public const string ScratchPrefix="initsql_";
		///<summary>Where the file lives in the repository. Also named in the file's header and in the adoption record of the new database.</summary>
		public const string RepoPath="db/init/new-database.sql";
		///<summary>Culture of the export run. The convert scripts and the default procedure buttons look at the current culture (a Canadian culture
		///gives Canadian defaults), so the run pins one for byte-identical output on any PC.</summary>
		public const string CultureName="en-US";

		///<summary>A scratch database name unique to this run, like initsql_261101101500_1234_5678 (UTC start time, process id, random number).
		///</summary>
		public static string MakeScratchName() {
			return ScratchPrefix+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
				+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		}

		public static int Run(Options options) {
			CultureInfo culture=CultureInfo.GetCultureInfo(CultureName);
			CultureInfo.DefaultThreadCurrentCulture=culture;
			CultureInfo.DefaultThreadCurrentUICulture=culture;
			Thread.CurrentThread.CurrentCulture=culture;
			Thread.CurrentThread.CurrentUICulture=culture;
			string dumpPath=options.DumpPath!="" ? options.DumpPath : DevDatabase.FindDefaultDump();
			if(dumpPath==null) {
				throw new RefusedException("Could not find UnitTests\\Resources\\dump.txt above "+AppDomain.CurrentDomain.BaseDirectory+". Pass --dump.");
			}
			string outPath=Path.GetFullPath(options.ExportInitSqlPath);
			DevDatabase devDatabase=new DevDatabase(options);
			devDatabase.ConnectToServer();
			devDatabase.CheckServer();
			if(devDatabase.DatabaseExists()) {
				throw new RefusedException("Database '"+options.Database+"' already exists. --export-init-sql builds a new scratch database and drops it "
					+"afterwards; give a name that does not exist, or leave out --database.");
			}
			DateTime dateTimeServerStart=ServerNow();
			bool isCreated=false;
			try {
				devDatabase.Create();
				isCreated=true;
				devDatabase.ConnectToDatabase(skipValidation:true);
				devDatabase.LoadDump(dumpPath);
				devDatabase.ConnectToDatabase(skipValidation:false);
				devDatabase.Upgrade();
				devDatabase.StampProgramVersion();
				devDatabase.RunForkMigrations();
				devDatabase.ApplyDefaults();
				int exitCode=Program.Verify(options);
				if(exitCode!=Program.ExitOk) {
					return exitCode;
				}
				Program.Info("Removing what belongs to this PC or this run");
				InitSqlScrub scrub=new InitSqlScrub(options.Database,dateTimeServerStart,ServerNow());
				scrub.Run();
				foreach(string line in scrub.ListLog) {
					Program.Info("  "+line);
				}
				scrub.CheckDatabase();
				Program.Info("Writing "+outPath);
				InitSqlWriter writer=new InitSqlWriter(options.Database);
				string text=writer.Write();
				List<string> listProblems=InitSqlScrub.FindLeftovers(text,GetForbiddenStrings(options,dumpPath),GetRunDates(dateTimeServerStart));
				if(listProblems.Count>0) {
					foreach(string problem in listProblems) {
						Console.Error.WriteLine("  PROBLEM: "+problem);
					}
					throw new ApplicationException("The generated file still holds something of this PC or this run; nothing was written.");
				}
				Directory.CreateDirectory(Path.GetDirectoryName(outPath));
				byte[] arrayBytes=new UTF8Encoding(false).GetBytes(text);
				File.WriteAllBytes(outPath,arrayBytes);
				Program.Info("Wrote "+writer.CountTables+" tables and "+writer.CountRows+" rows ("+arrayBytes.Length+" bytes, SHA-256 "
					+Sha256Hex(arrayBytes)+").");
				return Program.ExitOk;
			}
			finally {
				if(isCreated) {
					if(options.KeepScratch) {
						Program.Info("Kept the scratch database "+options.Database+" (--keep-scratch). Drop it when done.");
					}
					else {
						DropScratch(devDatabase,options.Database);
					}
				}
			}
		}

		private static void DropScratch(DevDatabase devDatabase,string database) {
			try {
				devDatabase.ConnectToServer();
				DataCore.NonQ("DROP DATABASE IF EXISTS `"+database+"`");
				Program.Info("Dropped the scratch database "+database);
			}
			catch(Exception ex) {
				Program.Warn("Could not drop the scratch database "+database+": "+ex.Message);
			}
		}

		private static DateTime ServerNow() {
			return DateTime.ParseExact(DataCore.GetScalar("SELECT DATE_FORMAT(NOW(),'%Y-%m-%d %H:%i:%s')"),"yyyy-MM-dd HH:mm:ss",
				CultureInfo.InvariantCulture);
		}

		///<summary>Names of this PC, its user and this run that must not end up in the file. Names shorter than three characters are left out: they
		///would match ordinary text. So is a name that the seed dump itself contains (a Windows user called "Admin", a PC called "Office"): finding it
		///in the file would prove nothing.</summary>
		private static List<string> GetForbiddenStrings(Options options,string dumpPath) {
			string seed=File.ReadAllText(dumpPath,new UTF8Encoding(false));
			List<string> list=new List<string>();
			void Add(Func<string> funcValue) {
				string value;
				try {
					value=funcValue();
				}
				catch(Exception) {
					return;
				}
				foreach(string part in (value??"").Split(new[] { ", " },StringSplitOptions.RemoveEmptyEntries)) {
					string trimmed=part.Trim();
					if(trimmed.Length<3 || list.Contains(trimmed,StringComparer.OrdinalIgnoreCase)) {
						continue;
					}
					if(seed.IndexOf(trimmed,StringComparison.OrdinalIgnoreCase)>=0) {
						Program.Warn("'"+trimmed+"' is also in the seed dump, so the file is not checked for it.");
						continue;
					}
					list.Add(trimmed);
				}
			}
			Add(() => options.Database);
			Add(() => Environment.MachineName);
			Add(() => Environment.UserName);
			Add(() => Environment.UserDomainName);
			Add(() => ODEnvironment.MachineName);
			Add(() => Security.CurComputerName);
			Add(() => Security.GetComplexComputerName());
			//Paths as the file writes them, with doubled backslashes.
			Add(() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace("\\","\\\\"));
			Add(() => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(dumpPath)))).Replace("\\","\\\\"));
			//The computers of the seed dump's own history, which the scrub removes (computer, computerpref, securitylog).
			list.Add("JORDANS");
			return list;
		}

		///<summary>The dates of this run as the file would write them: a timestamp the scrub missed shows up as one of these.</summary>
		private static List<string> GetRunDates(DateTime dateTimeServerStart) {
			List<DateTime> listDates=new List<DateTime> { dateTimeServerStart.Date,DateTime.Now.Date,DateTime.UtcNow.Date };
			return listDates.Distinct().Where(x => x!=InitSqlScrub.FixedDateTime.Date)
				.Select(x => x.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)).ToList();
		}

		private static string Sha256Hex(byte[] arrayBytes) {
			using(System.Security.Cryptography.SHA256 sha256=System.Security.Cryptography.SHA256.Create()) {
				return string.Concat(sha256.ComputeHash(arrayBytes).Select(x => x.ToString("x2")));
			}
		}
	}
}
