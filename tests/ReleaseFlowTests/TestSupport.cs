// Shared helpers: repository paths and synthetic scratch databases on the local MariaDB.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace ReleaseFlowTests {
	internal static class TestPaths {
		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		public static string RepoRoot {
			get {
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
						return directoryInfo.FullName;
					}
					directoryInfo=directoryInfo.Parent;
				}
				throw new InvalidOperationException("LICENSE and NOTICE not found above "+AppDomain.CurrentDomain.BaseDirectory);
			}
		}

		public static string ReadSource(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(RepoRoot,Path.Combine(arrayParts)));
		}

		///<summary>A new empty folder under the temp folder.</summary>
		public static string NewTempFolder(string name) {
			string folder=Path.Combine(Path.GetTempPath(),"ReleaseFlowTests",name+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
			Directory.CreateDirectory(folder);
			return folder;
		}
	}

	///<summary>Scratch databases with synthetic data. Every database name is a per-run name from RunName (releaseflow_ plus this run's id), dropped
	///and recreated per scenario and dropped when the test class ends.
	///Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when RELEASEFLOW_TEST_DB=skip.</summary>
	internal static class ScratchDb {
		public const string CONNECTION_SERVER="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3;Allow User Variables=true";
		public const string PREFIX="releaseflow_";
		///<summary>Scratch databases of an earlier run that started this long ago are dropped by later runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Unique to this test run: the start time (UTC, yyMMddHHmmss), the process id and a random number. Several runs share the one local
		///MariaDB server (other worktrees and test runners at the same time), so a fixed name like releaseflow_adoption let one run drop or recreate
		///the database another run was still testing ("database exists", "Unknown database").</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		///<summary>The per-run naming scheme of RunName; group 1 is the start time.</summary>
		private static readonly Regex _regexRunName=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
		private static bool _isStaleDropDone;
		private static bool? _isAvailable;
		private static string _skipReason;

		///<summary>This run's scratch database for the purpose, like releaseflow_261025101500_1234_5678_adoption. At most 64 characters, the MariaDB
		///limit.</summary>
		public static string RunName(string purpose) {
			string name=PREFIX+_runId+"_"+purpose;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Drops the scratch databases of earlier runs that were left behind (a crash or a stopped run), once they are older than
		///_timeSpanStale. Never a database of a run that may still be going, and never a name outside the per-run scheme, such as the fixed names older
		///versions of these tests used: an older checkout may still be testing in one.</summary>
		public static void DropStaleRunDatabases() {
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=Open(null))
			using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'releaseflow\\_%'",
				connection))
			using(MySqlDataReader reader=mySqlCommand.ExecuteReader())
			{
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=_regexRunName.Match(name);
				DateTime dateStarted;
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
					continue;
				}
				Drop(name);
			}
		}

		public static void RequireServer() {
			if(_isAvailable==null) {
				if(Environment.GetEnvironmentVariable("RELEASEFLOW_TEST_DB")=="skip") {
					_isAvailable=false;
					_skipReason="RELEASEFLOW_TEST_DB=skip";
				}
				else {
					try {
						using(MySqlConnection connection=new MySqlConnection(CONNECTION_SERVER)) {
							connection.Open();
						}
						_isAvailable=true;
					}
					catch(Exception ex) {
						_isAvailable=false;
						_skipReason="No local MariaDB server: "+ex.GetBaseException().Message;
					}
				}
			}
			if(_isAvailable==false) {
				Assert.Inconclusive(_skipReason);
			}
		}

		///<summary>True if the empty Open Dental 25.3.48 reference schema (od_253_schema) is loaded on the local server.</summary>
		public static bool HasReferenceSchema() {
			return Scalar(null,"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='od_253_schema' AND TABLE_NAME='patient'")!="0";
		}

		///<summary>Drops and creates the database with the preference, documentmisc, updatehistory and patient tables, then points the business layer at
		///it. With is253 the patient table is the 25.3.48 reference definition (with the office-only _portal_active_email column and its unique index);
		///otherwise a 24.3-style patient table without it.</summary>
		public static void Create(string dbName,bool is253) {
			RequireServer();
			RequireRunName(dbName);
			if(!_isStaleDropDone) {
				_isStaleDropDone=true;
				DropStaleRunDatabases();
			}
			Exec(null,"DROP DATABASE IF EXISTS "+dbName);
			Exec(null,"CREATE DATABASE "+dbName+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			Exec(dbName,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,PrefNum bigint(20) NOT NULL AUTO_INCREMENT,"
				+"Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
			Exec(dbName,"CREATE TABLE documentmisc (DocMiscNum bigint(20) NOT NULL AUTO_INCREMENT,DateCreated date NOT NULL DEFAULT '0001-01-01',"
				+"FileName varchar(255) NOT NULL,DocMiscType tinyint(4) NOT NULL,RawBase64 longtext NOT NULL,PRIMARY KEY (DocMiscNum)) "
				+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
			Exec(dbName,"CREATE TABLE updatehistory (UpdateHistoryNum bigint(20) NOT NULL AUTO_INCREMENT,DateTimeUpdated datetime NOT NULL "
				+"DEFAULT '0001-01-01 00:00:00',ProgramVersion varchar(255) NOT NULL,Signature text NOT NULL,PRIMARY KEY (UpdateHistoryNum)) "
				+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
			if(is253 && HasReferenceSchema()) {
				Exec(dbName,"CREATE TABLE patient LIKE od_253_schema.patient");
			}
			else if(is253) {
				//The columns the tests use, plus the office-only generated column and unique index exactly as the office's schema-only dump has them.
				Exec(dbName,"CREATE TABLE patient (PatNum bigint(20) NOT NULL AUTO_INCREMENT,LName varchar(100) NOT NULL DEFAULT '',"
					+"FName varchar(100) NOT NULL DEFAULT '',PatStatus tinyint(4) NOT NULL DEFAULT 0,Email varchar(100) NOT NULL DEFAULT '',"
					+"_portal_active_email varchar(100) GENERATED ALWAYS AS (case when PatStatus = 0 and Email <> '' then Email else NULL end) VIRTUAL,"
					+"PRIMARY KEY (PatNum),UNIQUE KEY portal_active_email_uniq (_portal_active_email),KEY indexLName (LName(10))) "
					+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
			}
			else {
				Exec(dbName,"CREATE TABLE patient (PatNum bigint(20) NOT NULL AUTO_INCREMENT,LName varchar(100) NOT NULL DEFAULT '',"
					+"FName varchar(100) NOT NULL DEFAULT '',PatStatus tinyint(4) NOT NULL DEFAULT 0,Email varchar(100) NOT NULL DEFAULT '',"
					+"PRIMARY KEY (PatNum),KEY indexLName (LName(10))) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
			}
			new DataConnection().SetDb("127.0.0.1",dbName,"root","","root","",DatabaseType.MySql,true);
		}

		///<summary>Throws unless the name is one this run made with RunName, so a test never recreates a database another run may be using.</summary>
		public static void RequireRunName(string dbName) {
			if(dbName==null || !dbName.StartsWith(PREFIX+_runId+"_")) {
				throw new ArgumentException("Scratch databases must be named with ScratchDb.RunName: "+dbName);
			}
		}

		///<summary>True if the name follows the per-run naming scheme of RunName (this run's or an earlier run's).</summary>
		public static bool IsRunName(string dbName) {
			return dbName!=null && _regexRunName.IsMatch(dbName);
		}

		///<summary>Drops a database of the per-run naming scheme (this run's, or a stale one of an earlier run). Ignores any other name, such as the
		///fixed names older versions of these tests used.</summary>
		public static void Drop(string dbName) {
			if(!IsRunName(dbName)) {
				return;
			}
			try {
				Exec(null,"DROP DATABASE IF EXISTS `"+dbName+"`");
			}
			catch(Exception) {
				//Leaving a scratch database behind is harmless; the next run drops it.
			}
		}

		public static void SetPref(string dbName,string prefName,string value) {
			Exec(dbName,"DELETE FROM preference WHERE PrefName='"+MySqlConnector.MySqlHelper.EscapeString(prefName)+"'");
			Exec(dbName,"INSERT INTO preference (PrefName,ValueString,Comments) VALUES ('"+MySqlConnector.MySqlHelper.EscapeString(prefName)+"','"
				+MySqlConnector.MySqlHelper.EscapeString(value)+"','')");
		}

		///<summary>The value of the preference, or null when the row is missing.</summary>
		public static string GetPref(string dbName,string prefName) {
			return Scalar(dbName,"SELECT ValueString FROM preference WHERE PrefName='"+MySqlConnector.MySqlHelper.EscapeString(prefName)+"'");
		}

		public static void Exec(string dbName,string command) {
			using(MySqlConnection connection=Open(dbName))
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>First column of the first row as text, or null when there is no row.</summary>
		public static string Scalar(string dbName,string command) {
			using(MySqlConnection connection=Open(dbName))
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				object value=mySqlCommand.ExecuteScalar();
				return (value==null || value==DBNull.Value) ? null : Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);
			}
		}

		private static MySqlConnection Open(string dbName) {
			MySqlConnection connection=new MySqlConnection(CONNECTION_SERVER+(dbName==null?"":";Database="+dbName));
			connection.Open();
			using(MySqlCommand mySqlCommand=new MySqlCommand("SET SESSION sql_mode=''",connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
			return connection;
		}
	}
}
