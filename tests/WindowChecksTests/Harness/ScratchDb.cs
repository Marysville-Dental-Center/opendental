// The scratch database of a run: the dump of the synthetic test database (OPMS_WINDOW_CHECKS_DUMP, made once from it) restored with the local
// MariaDB's own mysql.exe into a new database winchk_<time>, on 127.0.0.1:3306 only. Every check runs against this copy, and the copy is
// dropped at the end. Only names starting with winchk_ are ever created or dropped. The copy's A to Z folder is moved to a scratch folder in
// the temp folder, so no check writes into the test database's own images folder.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DataConnectionBase;
using MySqlConnector;

namespace WindowChecksTests {
	public static class ScratchDb {
		public const string PREFIX="winchk_";
		public const string Server="127.0.0.1";
		public const int Port=3306;
		private static string _name;
		private static string _atoZFolder;
		private static string _skipReason;
		private static bool _hasTried;
		private static readonly object _lock=new object();

		///<summary>This run's database, made on first use.</summary>
		public static string Name {
			get {
				Ensure();
				return _name;
			}
		}

		///<summary>The copy's A to Z folder (DocPath), a scratch folder in the temp folder with the letter folders.</summary>
		public static string AtoZFolder {
			get {
				Ensure();
				return _atoZFolder;
			}
		}

		///<summary>Null when the copy is ready; otherwise why it could not be made.</summary>
		public static string GetSkipReason() {
			Ensure();
			return _skipReason;
		}

		public static bool IsMade {
			get {
				return _name!=null && _skipReason==null;
			}
		}

		private static void Ensure() {
			lock(_lock) {
				if(_hasTried) {
					return;
				}
				_hasTried=true;
				try {
					Make();
				}
				catch(Exception ex) {
					_skipReason="The scratch copy of the test database could not be made: "+ex.GetBaseException().Message;
					Results.Log(_skipReason);
				}
			}
		}

		private static void Make() {
			string name=PREFIX+DateTime.Now.ToString("MMddHHmmss");
			using(MySqlConnection connection=Open("")) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3");
			}
			_name=name;
			Stopwatch stopwatch=Stopwatch.StartNew();
			List<string> listArgs=new List<string> { "-h",Server,"-P",Port.ToString(),"-u","root","--default-character-set=utf8mb4" };
			listArgs.AddRange(DevDbTls.GetClientArguments(Server,Port));
			listArgs.Add(name);
			ProcessStartInfo processStartInfo=new ProcessStartInfo(Path.Combine(WinChkEnv.MySqlBin,"mysql.exe"),
				string.Join(" ",listArgs.Select(x => x.Contains(" ") ? "\""+x+"\"" : x)))
			{
				UseShellExecute=false,
				RedirectStandardInput=true,
				RedirectStandardError=true,
				RedirectStandardOutput=true,
				CreateNoWindow=true,
			};
			using(Process process=Process.Start(processStartInfo)) {
				System.Threading.Tasks.Task<string> taskError=process.StandardError.ReadToEndAsync();
				System.Threading.Tasks.Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				using(FileStream fileStream=File.OpenRead(WinChkEnv.DumpPath)) {
					fileStream.CopyTo(process.StandardInput.BaseStream);
				}
				process.StandardInput.Close();
				if(!process.WaitForExit(600000)) {
					process.Kill();
					throw new Exception("mysql.exe did not restore the dump within 10 minutes.");
				}
				if(process.ExitCode!=0) {
					throw new Exception("mysql.exe exit "+process.ExitCode+": "+taskError.Result);
				}
			}
			Results.Log("Restored the dump into "+name+" in "+stopwatch.Elapsed.TotalSeconds.ToString("0.0")+" s");
			//Never let the program contact a time server: a ForkTimeSyncMode other than 0 stops the run.
			string timeSync=Scalar("SELECT ValueString FROM preference WHERE PrefName='ForkTimeSyncMode'");
			if(timeSync!="" && timeSync!="0") {
				throw new Exception("ForkTimeSyncMode is "+timeSync+" in the copy: the checks would contact a time server. Stopped.");
			}
			string patient=Scalar("SELECT CONCAT(LName,', ',FName) FROM patient WHERE PatNum=1");
			if(patient!="Gatetest, Stage") {
				throw new Exception("Patient #1 is '"+patient+"', not the synthetic 'Gatetest, Stage'.");
			}
			_atoZFolder=Path.Combine(Path.GetTempPath(),"winchk",name,"AtoZ");
			Directory.CreateDirectory(_atoZFolder);
			for(char letter='A';letter<='Z';letter++) {
				Directory.CreateDirectory(Path.Combine(_atoZFolder,letter.ToString()));
			}
			using(MySqlConnection connection=Open(name)) {
				MySqlCommand command=new MySqlCommand("UPDATE preference SET ValueString=@path WHERE PrefName='DocPath'",connection);
				command.Parameters.AddWithValue("@path",_atoZFolder+"\\");
				command.ExecuteNonQuery();
				//No computer of the copy overrides the A to Z folder.
				Exec(connection,"UPDATE computerpref SET AtoZpath=''");
			}
		}

		public static void Drop() {
			lock(_lock) {
				if(_name==null || !_name.StartsWith(PREFIX)) {
					return;
				}
				using(MySqlConnection connection=Open("")) {
					Exec(connection,"DROP DATABASE IF EXISTS `"+_name+"`");
				}
				Results.Log("Dropped "+_name);
				try {
					Directory.Delete(Path.Combine(Path.GetTempPath(),"winchk",_name),true);
				}
				catch(Exception ex) {
					Results.Log("Scratch A to Z folder not deleted: "+ex.Message);
				}
			}
		}

		///<summary>A connection to the local test server; database "" for none. Only winchk_ databases are ever opened.</summary>
		public static MySqlConnection Open(string database) {
			if(database!="" && !database.StartsWith(PREFIX)) {
				throw new ArgumentException("The window checks only open databases whose name starts with "+PREFIX);
			}
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply("Server="+Server+";Port="+Port+";User ID=root;Password=;Connection Timeout=15;"
				+"Allow User Variables=true"+(database=="" ? "" : ";Database="+database)));
			connection.Open();
			return connection;
		}

		public static void Exec(MySqlConnection connection,string command) {
			using MySqlCommand mySqlCommand=new MySqlCommand(command,connection);
			mySqlCommand.ExecuteNonQuery();
		}

		///<summary>A command against this run's copy.</summary>
		public static void Exec(string command) {
			using MySqlConnection connection=Open(Name);
			Exec(connection,command);
		}

		///<summary>The first value of the first row of a query on this run's copy, or "" when there is none.</summary>
		public static string Scalar(string command) {
			using MySqlConnection connection=Open(_name);
			using MySqlCommand mySqlCommand=new MySqlCommand(command,connection);
			object value=mySqlCommand.ExecuteScalar();
			return value==null || value==DBNull.Value ? "" : value.ToString();
		}

		public static DataTable Table(string command) {
			using MySqlConnection connection=Open(_name);
			using MySqlDataAdapter adapter=new MySqlDataAdapter(command,connection);
			DataTable table=new DataTable();
			adapter.Fill(table);
			return table;
		}

		///<summary>A query's rows as "a|b|c" lines, for notes.</summary>
		public static string Rows(string command) {
			DataTable table=Table(command);
			return string.Join("; ",table.Rows.Cast<DataRow>().Select(x => string.Join("|",x.ItemArray.Select(y => y==DBNull.Value ? "NULL" : y.ToString()))));
		}
	}
}
