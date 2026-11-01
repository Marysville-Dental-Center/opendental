// Shared helpers for the M2-SHELL tests: repository paths and a scratch MariaDB database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2SHELLTests {
	///<summary>Locates the repository and its source files (the WinForms code is checked as source, because the tests do not load OpenDental.exe).</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
		}

		///<summary>A file under the repository root, for example GetRepoFile("OpenDental","Forms","FormChartView.cs").</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(GetRepoFile(arrayParts));
		}
	}

	///<summary>A scratch database on the local MariaDB (127.0.0.1:3306, root, blank password) with the Open Dental 26.1.70 schema from the reference
	///snapshot embedded in OpenDentBusiness, and synthetic rows only. Its name starts with "m2shell_" and is unique to the run (time, process id,
	///random), because several worktrees and test runners share the one local server. Tests are inconclusive when no server answers or when
	///M2SHELL_TEST_DB=skip.</summary>
	public static class ScratchDb {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		private static readonly object _lock=new object();
		private static string _dbName;
		private static string _skipReason;
		private static bool _isCreated;

		///<summary>Creates the database once per test run and connects the business layer to it. Returns null when it is ready, otherwise why the
		///database tests are skipped.</summary>
		public static string EnsureCreated() {
			lock(_lock) {
				if(_isCreated || _skipReason!=null) {
					Connect();
					return _skipReason;
				}
				if(Environment.GetEnvironmentVariable("M2SHELL_TEST_DB")=="skip") {
					_skipReason="M2SHELL_TEST_DB=skip";
					return _skipReason;
				}
				_dbName="m2shell_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
				try {
					using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
						connection.Open();
						Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					}
				}
				catch(Exception ex) {
					_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
					return _skipReason;
				}
				//The server answers, so from here on a failure is a real failure, not a reason to skip.
				using(MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+_dbName)) {
					connection.Open();
					foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
						Exec(connection,table.ToDdl());
					}
				}
				_isCreated=true;
				Connect();
				return null;
			}
		}

		private static void Connect() {
			if(!_isCreated) {
				return;
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,_dbName,"root","","","",DatabaseType.MySql,true);
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		///<summary>Empties the tables a test uses and puts back the preferences the business layer reads, at their defaults.</summary>
		public static void Reset(params string[] arrayTables) {
			foreach(string table in arrayTables) {
				DataCore.NonQ("DELETE FROM `"+table+"`");
			}
			DataCore.NonQ("DELETE FROM preference");
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('RandomPrimaryKeys','0'),('DataBaseVersion','25.3.48.0')");
			Prefs.RefreshCache();
		}

		public static void Drop() {
			lock(_lock) {
				if(!_isCreated) {
					return;
				}
				try {
					using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
						connection.Open();
						Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
					}
				}
				catch(Exception) {
					//Leaving the scratch database behind is harmless; its name is unique to this run.
				}
				_isCreated=false;
			}
		}

		public static long GetLong(string command) {
			return Convert.ToInt64(DataCore.GetScalar(command));
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}

	///<summary>Drops the scratch database after every test class in the assembly has run.</summary>
	[Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
	public class AssemblyHooks {
		[Microsoft.VisualStudio.TestTools.UnitTesting.AssemblyCleanup]
		public static void Cleanup() {
			ScratchDb.Drop();
		}
	}
}
