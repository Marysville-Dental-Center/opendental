// Scratch databases on the local MariaDB for the UDS+ report tests. Synthetic rows only: nothing here reads or copies an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M2UDSTests {
	///<summary>Creates a scratch database with the reference definitions (OdSchema-26.1.70.sql, embedded in OpenDentBusiness) of the tables a test
	///needs, connects the business layer to it and drops it afterwards. Names are "m2uds_" plus a per-run id (time, process id, random number), so
	///parallel runs never collide and nothing else is ever dropped. Tests are inconclusive when no server answers at 127.0.0.1:3306 as root with a
	///blank password, or when M2UDS_TEST_DB=skip.</summary>
	public static class UdsTestDb {
		private const string SERVER_CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private const string PREFIX="m2uds_";
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		public static string ScratchName(string purpose) {
			return PREFIX+_runId+"_"+purpose;
		}

		///<summary>Null when the server answers; otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("M2UDS_TEST_DB")=="skip") {
				return "M2UDS_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(SERVER_CONNECTION)) {
					connection.Open();
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB server for scratch databases: "+ex.GetBaseException().Message;
			}
		}

		///<summary>Drops and creates the database with the reference definitions of the given tables, then connects the business layer to it.</summary>
		public static void Create(string dbName,params string[] arrayTableNames) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Dictionary<string,string> dictCreate=GetReferenceCreateStatements();
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+dbName+"`");
				Exec(connection,"CREATE DATABASE `"+dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				Exec(connection,"USE `"+dbName+"`");
				foreach(string tableName in arrayTableNames) {
					Exec(connection,dictCreate[tableName]);
				}
			}
			Connect(dbName);
		}

		///<summary>Points the business layer at the scratch database. Each test class calls it before every test, because the connection is global.
		///</summary>
		public static void Connect(string dbName) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			new DataConnection().SetDb("127.0.0.1",dbName,"root","","root","",DatabaseType.MySql,true);
		}

		public static void Drop(string dbName) {
			if(dbName==null || !dbName.StartsWith(PREFIX)) {
				throw new ArgumentException("Not a UDS+ test database: "+dbName);
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					Exec(connection,"DROP DATABASE IF EXISTS `"+dbName+"`");
				}
			}
			catch(Exception) {
				//Harmless if left behind; the name is unique to this run.
			}
		}

		///<summary>Runs synthetic-data statements with a blank session sql_mode, so rows only need the columns a test cares about.</summary>
		public static void Seed(string dbName,params string[] arrayCommands) {
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+dbName+"`");
				Exec(connection,"SET SESSION sql_mode=''");
				foreach(string command in arrayCommands) {
					Exec(connection,command);
				}
			}
		}

		///<summary>Every CREATE TABLE statement of the reference schema the program expects, by table name.</summary>
		public static Dictionary<string,string> GetReferenceCreateStatements() {
			string ddl=SchemaVerifier.ReadReferenceDdl();
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(Match match in Regex.Matches(ddl,@"CREATE TABLE `(\w+)` \(.*?\) ENGINE=[^;]*;",RegexOptions.Singleline)) {
				dict[match.Groups[1].Value]=match.Value;
			}
			return dict;
		}

		///<summary>The repository file at the given path, found by walking up from the test assembly to the folder with LICENSE and NOTICE.</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string dir=Path.GetDirectoryName(typeof(UdsTestDb).Assembly.Location);
			while(dir!=null && !(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE")))) {
				dir=Path.GetDirectoryName(dir);
			}
			if(dir==null) {
				throw new FileNotFoundException("Could not find the repository root above the test assembly.");
			}
			return Path.Combine(new[] { dir }.Concat(arrayParts).ToArray());
		}

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection(SERVER_CONNECTION);
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
