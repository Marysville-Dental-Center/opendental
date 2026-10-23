// A scratch database on the local MariaDB for the A-APPT database tests: a fresh 24.3.63 database built like tools/New-DevDatabase.ps1, then
// upgraded to the Open Dental 25.3.48 schema by the fork migration runner. Synthetic rows only; nothing here reads an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ApptTests {
	///<summary>Every database name starts with "appttest_" so the tests can never drop anything else.</summary>
	public static class ScratchDb {
		private const string SERVER="127.0.0.1";
		private const string USER="root";
		private const string PASSWORD="";
		private const string PREFIX="appttest_";

		///<summary>Unique to this run, so runs from other worktrees at the same time never share a database.</summary>
		public static string NewName() {
			return PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		}

		///<summary>Null when the server answers with a blank global sql_mode (the convert scripts need it, as OpenDental.exe sets at startup);
		///otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("APPT_TEST_DB")=="skip") {
				return "APPT_TEST_DB=skip";
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

		///<summary>Builds the database and leaves the business layer connected to it with a made-up current user.</summary>
		public static void Create(string name) {
			Drop(name);
			Exec("CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			Connect(name);
			DataCore.NonQ(File.ReadAllText(FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt")),new UTF8Encoding(false)));
			Security.CurUser=new Userod { UserNum=1,UserName="appttest" };
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
