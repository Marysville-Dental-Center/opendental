// Scratch databases on the local MariaDB for the M2-EF tests. Synthetic data only: nothing here reads or copies an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M2EFTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "m2eftest_" so a test can never drop anything else.</summary>
	public static class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="m2eftest_";
		///<summary>Unique to this test run: start time (UTC), process id and a random number, because several runs share the one local server.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);

		///<summary>This run's scratch database name for a purpose, like m2eftest_261025081500_1234_5678_patfields.</summary>
		public static string ScratchName(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the server answers; otherwise why database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("M2EF_TEST_DB")=="skip") {
				return "M2EF_TEST_DB=skip";
			}
			string reason=null;
			for(int i=0;i<3;i++) {
				try {
					using(MySqlConnection connection=OpenServer()) {
						return null;
					}
				}
				catch(Exception ex) {
					reason="No local MariaDB at "+Server+": "+ex.GetBaseException().Message;
				}
			}
			return reason;
		}

		public static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true");
			connection.Open();
			return connection;
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("DROP DATABASE IF EXISTS `"+name+"`",connection)) {
				command.ExecuteNonQuery();
			}
		}

		///<summary>Drops scratch databases of earlier runs that crashed, once they are older than 12 hours. Never touches a run that may still go.
		///</summary>
		public static void DropStaleScratchDatabases() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'm2eftest\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
					continue;
				}
				try {
					Drop(name);
				}
				catch(Exception) {
					//another run may be dropping it
				}
			}
		}

		///<summary>Builds a fresh database the way tools/New-DevDatabase.ps1 does (the seed dump, the frozen 24.3.63 convert chain), then runs the
		///fork migration runner, so the schema is at the Open Dental 26.1.70 level (M2-01). Leaves the business layer connected to it.</summary>
		public static void CreateFresh(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci",connection)) {
				command.ExecuteNonQuery();
			}
			Connect(name);
			string dumpPath=FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt"));
			DataCore.NonQ(File.ReadAllText(dumpPath,new UTF8Encoding(false)));
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			Cache.Refresh(InvalidType.Prefs);
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs);
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				string candidate=Path.Combine(dir.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				dir=dir.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}
	}

	///<summary>Runs WPF code (FlowDocument, TextRange, RichTextBox) on its own STA thread, as WPF requires.</summary>
	public static class Sta {
		public static void Run(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
			}
		}
	}
}
