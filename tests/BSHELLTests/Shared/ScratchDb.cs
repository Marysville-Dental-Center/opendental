// A scratch database on the local MariaDB for the B-SHELL database tests. Synthetic rows only; nothing here reads an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace BSHELLTests {
	///<summary>One scratch database for the test assembly, with a name unique to this run (time, process id, random number), so runs from other
	///worktrees at the same time never share or drop each other's database. The tables come from the current reference schema
	///(SchemaVerifier.GetReference(), the one the generated CRUD matches). Dropped in AssemblyCleanup. Tests that need it call RequireDb(), which marks them inconclusive when the server does not answer or
	///BSHELL_TEST_DB=skip.</summary>
	[TestClass]
	public class ScratchDb {
		private const string PREFIX="bshelltest_";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=5";
		///<summary>Emptied before each database test. preference keeps RandomPrimaryKeys and AtoZfolderUsed, rewritten by RequireDb.</summary>
		private static readonly string[] _arrayTables={ "program","toolbutitem","patient" };
		private static string _skipReason;
		public static string DbName { get; private set; }

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			if(Environment.GetEnvironmentVariable("BSHELL_TEST_DB")=="skip") {
				_skipReason="BSHELL_TEST_DB=skip";
				return;
			}
			DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
			try {
				SchemaSnapshot schema=SchemaVerifier.GetReference();
				using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION))) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+DbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE `"+DbName+"`");
					for(int i=0;i<_arrayTables.Length;i++) {
						Exec(connection,GetCreateTable(schema,_arrayTables[i]));
					}
					Exec(connection,GetCreateTable(schema,"preference"));
				}
				RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
				new DataConnection().SetDb("127.0.0.1",DbName,"root","","root","",DatabaseType.MySql,true,DevDbTls.GetSslCa("127.0.0.1"));
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
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

		///<summary>Marks the calling test inconclusive when there is no scratch database. Otherwise empties the tables, writes the preference rows the
		///business layer reads (local A to Z storage) and reloads the preference cache.</summary>
		public static void RequireDb() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			for(int i=0;i<_arrayTables.Length;i++) {
				DataCore.NonQ("DELETE FROM "+_arrayTables[i]);
			}
			DataCore.NonQ("DELETE FROM preference");
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('AtoZfolderUsed','"
				+(int)DataStorageType.LocalAtoZ+"')");
			Cache.Refresh(InvalidType.Prefs);
		}

		///<summary>The CREATE TABLE statement for one table of the reference schema.</summary>
		public static string GetCreateTable(SchemaSnapshot schema,string table) {
			SchemaTable schemaTable=schema.GetTable(table);
			if(schemaTable==null) {
				throw new InvalidOperationException("Table "+table+" is not in the reference schema.");
			}
			return schemaTable.ToDdl();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
