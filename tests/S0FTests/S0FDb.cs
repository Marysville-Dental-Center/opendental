// One scratch database for the S0-F tests on the local MariaDB (127.0.0.1:3306, root, blank password): the committed db/init/new-database.sql
// with every fork migration of this build run on it. Its name starts with "s0f_" plus this run's id, so a test can never drop anything else and
// runs from other worktrees never collide. Built the first time a test asks for it (the tests without a database never wait for it) and dropped
// when the assembly is done. Synthetic rows only. Skipped (inconclusive) when the server does not answer, or when S0F_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace S0FTests {
	[TestClass]
	public class S0FDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="s0f_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		public static readonly string DbName=PREFIX+_runId+"_main";
		private static readonly object _lock=new object();
		///<summary>Null before the first Begin. Then null when the database is ready, otherwise why the database tests are skipped.</summary>
		private static string _skipReason;
		private static bool _isTried;
		private static bool _isBuilt;
		private static int _counter;

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuilt || Environment.GetEnvironmentVariable("S0F_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		///<summary>Call from [TestInitialize]: builds the database the first time, skips the test when there is none, otherwise connects to it as
		///user 1.</summary>
		public static void Begin() {
			SetCulture();
			lock(_lock) {
				if(!_isTried) {
					_isTried=true;
					Build();
				}
			}
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			Connect(DbName);
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Admin" };
		}

		private static void Build() {
			_skipReason=GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				DropStale();
				CreateFromInitFile(DbName);
				_isBuilt=true;
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security);
				Userods.RefreshCache();
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		///<summary>Lans.g returns the English text without touching the database only in en-US.</summary>
		public static void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}

		public static int Next() {
			return Interlocked.Increment(ref _counter);
		}

		public static Patient NewPatient() {
			return PatientT.CreatePatient(Next().ToString(),lName:"S0fThumb",fName:"Synthetic");
		}

		///<summary>Switches image storage: LocalAtoZ uses atoZPath as this computer's A-to-Z folder.</summary>
		public static void SetStorage(DataStorageType dataStorageType,string atoZPath=null) {
			Prefs.UpdateInt(PrefName.AtoZfolderUsed,(int)dataStorageType);
			Cache.Refresh(InvalidType.Prefs);
			OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath=atoZPath;
		}

		#region Scratch database
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("S0F_TEST_DB")=="skip") {
				return "S0F_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					string sqlMode=Scalar(connection,"SELECT @@GLOBAL.sql_mode");
					if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
						return "Global sql_mode is '"+sqlMode+"'; the business layer expects a blank sql_mode.";
					}
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		public static MySqlConnection OpenServer(string database="") {
			MySqlConnection connection=new MySqlConnection(DevDbTls.Apply("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+(database=="" ? "" : ";Database="+database)+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600"));
			connection.Open();
			return connection;
		}

		///<summary>Drops leftovers of earlier runs (a crash, or S0F_KEEP_DB=1) once they are older than 12 hours.</summary>
		private static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 's0f\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader())
			{
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
					continue;
				}
				try {
					Drop(name);
				}
				catch(Exception) {
					//Another run may be dropping it at the same time.
				}
			}
		}

		///<summary>An empty utf8mb3 database with the committed new-database file loaded into it, statement by statement. Leaves the business layer
		///connected to it.</summary>
		private static void CreateFromInitFile(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			string text=File.ReadAllText(FindRepoFile(Path.Combine("db","init","new-database.sql")),new UTF8Encoding(false)).Replace("\r\n","\n");
			using(MySqlConnection connection=OpenServer(name)) {
				//Every statement of the generated file ends with a semicolon at the end of a line, and no value holds a raw line break.
				foreach(string statement in text.Split(new[] { ";\n" },StringSplitOptions.None)) {
					if(statement.Trim()=="") {
						continue;
					}
					Exec(connection,statement);
				}
			}
			Connect(name);
			Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="Admin" };
			if(!Prefs.DictIsNull()) {
				Prefs.ClearCache();
			}
			Cache.Refresh(InvalidType.Prefs);
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true,DevDbTls.GetSslCa(Server));
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static string Scalar(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : Convert.ToString(value,CultureInfo.InvariantCulture);
			}
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
		#endregion Scratch database
	}
}
