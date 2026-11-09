// Scratch databases on the local MariaDB for the B-SERVER tests. Synthetic rows only: nothing here reads or copies an office database, and nothing
// connects to any host but 127.0.0.1:3306.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness.ForkMigrations;

namespace BSERVERTests {
	///<summary>Creates scratch databases with the Open Dental 25.3.48 definitions of the tables a test needs (from the reference schema embedded in
	///OpenPmsDentalBusiness), connects the business layer to one of them and drops them afterwards. Names start with "bsrvtest_" plus a per-run id (UTC
	///start time, process id and a random number), so parallel runs from other worktrees never collide and nothing else is ever dropped. Tests are
	///skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when BSERVER_TEST_DB=skip.</summary>
	public static class ServerTestDb {
		public const string SERVER="127.0.0.1";
		private const string SERVER_CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private const string PREFIX="bsrvtest_";
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);

		public static string ScratchName(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Null when the server answers; otherwise why the database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("BSERVER_TEST_DB")=="skip") {
				return "BSERVER_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(SERVER_CONNECTION))) {
					connection.Open();
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB server for scratch databases: "+ex.GetBaseException().Message;
			}
		}

		///<summary>Drops and creates the database with the 25.3.48 definitions of the given tables. Does not connect the business layer.</summary>
		public static void Create(string dbName,params string[] arrayTableNames) {
			CheckName(dbName);
			Dictionary<string,string> dictCreate=GetReferenceCreateStatements();
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+dbName+"`");
				Exec(connection,"CREATE DATABASE `"+dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				Exec(connection,"USE `"+dbName+"`");
				foreach(string tableName in arrayTableNames) {
					Exec(connection,dictCreate[tableName]);
				}
			}
		}

		///<summary>Points the business layer at the scratch database. Each test class calls it before every test, because the connection is global
		///and MSTest may run another class's ClassInitialize in between.</summary>
		public static void Connect(string dbName) {
			CheckName(dbName);
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			new DataConnection().SetDb(SERVER,dbName,"root","","root","",DatabaseType.MySql,true,DevDbTls.GetSslCa(SERVER));
		}

		public static void Drop(string dbName) {
			CheckName(dbName);
			try {
				using(MySqlConnection connection=OpenServer()) {
					Exec(connection,"DROP DATABASE IF EXISTS `"+dbName+"`");
				}
			}
			catch(Exception) {
				//Harmless if left behind; the name is unique to this run.
			}
		}

		///<summary>Runs synthetic-data statements with a blank session sql_mode, so the rows only need the columns a test cares about (the others take
		///their implicit defaults, as Open Dental's own blank sql_mode allows).</summary>
		public static void Seed(string dbName,params string[] arrayCommands) {
			CheckName(dbName);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+dbName+"`");
				Exec(connection,"SET SESSION sql_mode=''");
				foreach(string command in arrayCommands) {
					Exec(connection,command);
				}
			}
		}

		///<summary>One value straight from the database, bypassing the business layer. Null when there is no row.</summary>
		public static string Scalar(string dbName,string command) {
			CheckName(dbName);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+dbName+"`");
				using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
					object value=mySqlCommand.ExecuteScalar();
					if(value==null || value is DBNull) {
						return null;
					}
					return Convert.ToString(value,CultureInfo.InvariantCulture);
				}
			}
		}

		///<summary>Every CREATE TABLE statement of the reference schema, by table name.</summary>
		public static Dictionary<string,string> GetReferenceCreateStatements() {
			string ddl=SchemaVerifier.ReadReferenceDdl();
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(Match match in Regex.Matches(ddl,@"CREATE TABLE `(\w+)` \(.*?\) ENGINE=[^;]*;",RegexOptions.Singleline)) {
				dict[match.Groups[1].Value]=match.Value;
			}
			return dict;
		}

		private static void CheckName(string dbName) {
			if(dbName==null || !dbName.StartsWith(PREFIX)) {
				throw new ArgumentException("Not a B-SERVER test database: "+dbName);
			}
		}

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(SERVER_CONNECTION));
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}

	///<summary>Locates files in the repository, for the tests that check source files.</summary>
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

		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}
	}
}
