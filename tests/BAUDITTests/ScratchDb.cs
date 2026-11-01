// A scratch database on the local MariaDB for the B-AUDIT database tests: a fresh 24.3.63 database built like tools/New-DevDatabase.ps1, then
// upgraded by the fork migration runner (the same pattern as tests/ForkMigrationTests/TestDb.cs). Synthetic rows only; nothing here reads an
// office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace BAUDITTests {
	///<summary>Every database name starts with "baudittest_" so the tests can never drop anything else.</summary>
	public static class ScratchDb {
		private const string SERVER="127.0.0.1";
		private const string USER="root";
		private const string PASSWORD="";
		private const string PREFIX="baudittest_";
		///<summary>Scratch databases left behind by a run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);

		///<summary>Unique to this run (UTC start time, process id and a random number), so runs from other worktrees at the same time never share a
		///database.</summary>
		public static string NewName() {
			return PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		}

		///<summary>Null when the server answers with a blank global sql_mode (the convert scripts need it, as OpenDental.exe sets at startup);
		///otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("BAUDIT_TEST_DB")=="skip") {
				return "BAUDIT_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					using(MySqlCommand command=new MySqlCommand("SELECT @@GLOBAL.sql_mode",connection)) {
						string sqlMode=command.ExecuteScalar()?.ToString()??"";
						if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
							return "Global sql_mode is '"+sqlMode+"'; the convert scripts need a blank sql_mode.";
						}
					}
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+SERVER+":3306 as "+USER+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+SERVER+";Port=3306;User ID="+USER+";Password="+PASSWORD+";Connection Timeout=15");
			connection.Open();
			return connection;
		}

		private static void Exec(string command) {
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>Drops this fixture's scratch databases that earlier runs left behind (a crash, or BAUDIT_TEST_KEEP_DB=1), once they are older than
		///_timeSpanStale, so a run that may still be going is never touched.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'baudittest\\_%'",
				connection))
			using(MySqlDataReader reader=command.ExecuteReader())
			{
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				DateTime dateStarted;
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted>_timeSpanStale) {
					Drop(name);
				}
			}
		}

		///<summary>Builds the database and leaves the business layer connected to it with a made-up current user.</summary>
		public static void Create(string name) {
			Drop(name);
			Exec("CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			Connect(name);
			DataCore.NonQ(File.ReadAllText(FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt")),new UTF8Encoding(false)));
			Security.CurUser=new Userod { UserNum=1,UserName="baudittest" };
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs);
		}

		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,name,USER,PASSWORD,"","",DatabaseType.MySql,true);
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			Exec("DROP DATABASE IF EXISTS `"+name+"`");
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
}
