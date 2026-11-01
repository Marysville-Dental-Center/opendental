// Scratch databases on the local MariaDB for the M2-MED tests, after the pattern of tests/ForkMigrationTests/TestDb.cs. Synthetic data only:
// nothing here reads or copies an office database, and nothing connects to any server but 127.0.0.1:3306.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2MEDTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "m2med_" plus this run's id, so a test never drops anything
	///else and runs from other worktrees on the same server never collide.</summary>
	public static class ScratchDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="m2med_";
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		///<summary>This run's scratch database for the given purpose, like m2med_261025101500_1234_5678_merge.</summary>
		public static string Name(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the local server answers; otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("M2MED_TEST_DB")=="skip") {
				return "M2MED_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					return null;
				}
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		public static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Default Command Timeout=600");
			connection.Open();
			return connection;
		}

		///<summary>Creates the database with every table of the Open Dental 26.1.70 reference schema embedded in OpenDentBusiness (no rows), adds the
		///preference rows the code under test reads, and connects the business layer to it.</summary>
		public static void CreateFrom26_1Reference(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				Exec(connection,"USE `"+name+"`");
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES('RandomPrimaryKeys','0'),('ProblemsIndicateNone','0'),"
					+"('AllergiesIndicateNone','0')");
			}
			Connect(name);
			Prefs.RefreshCache();
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Only for names starting with m2med_.</summary>
		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Drops the database unless M2MED_KEEP_DB=1. Never throws: the name is unique to this run, so a leftover harms nothing.</summary>
		public static void DropQuietly(string name) {
			if(Environment.GetEnvironmentVariable("M2MED_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(name);
			}
			catch(Exception) {
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
