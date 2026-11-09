// A scratch database on the local MariaDB for the P262-LINKS database tests. Synthetic rows only; nothing here reads an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262LinksTests {
	///<summary>One scratch database for the test assembly, named test_p262links_ plus the time, the process id and a random number, so runs from other
	///worktrees at the same time never share or drop each other's database. Every table of the current reference schema (SchemaVerifier.GetReference(),
	///the one the generated CRUD matches). Dropped in AssemblyCleanup. Tests that need it call RequireDb(), which marks them inconclusive when the server
	///does not answer or P262LINKS_TEST_DB=skip.</summary>
	[TestClass]
	public class ScratchDb {
		private const string PREFIX="test_p262links_";
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=5";
		///<summary>Emptied before each database test.</summary>
		private static readonly string[] _arrayTables={ "program","programproperty","toolbutitem" };
		private static string _skipReason;
		public static string DbName { get; private set; }

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			if(Environment.GetEnvironmentVariable("P262LINKS_TEST_DB")=="skip") {
				_skipReason="P262LINKS_TEST_DB=skip";
				return;
			}
			string dbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
			try {
				using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION))) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			DbName=dbName;
			using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION+";Database="+DbName))) {
				connection.Open();
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('EasyHideDentalSchools','1')");
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,DbName,"root","","","",DatabaseType.MySql,true,DevDbTls.GetSslCa(SERVER));
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
			Prefs.RefreshCache();
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(DbName==null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION))) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+DbName+"`");
				}
			}
			catch(Exception) {
				//The name is unique to this run, so a database left behind never disturbs another run.
			}
		}

		///<summary>Marks the calling test inconclusive when there is no scratch database. Otherwise empties the program link tables and reloads their
		///caches.</summary>
		public static void RequireDb() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			for(int i=0;i<_arrayTables.Length;i++) {
				DataCore.NonQ("DELETE FROM "+_arrayTables[i]);
			}
			RefreshCaches();
		}

		public static void RefreshCaches() {
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			ToolButItems.RefreshCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
