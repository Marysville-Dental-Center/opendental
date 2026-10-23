using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;

namespace SignatureTests {
	///<summary>One scratch MariaDB database for the whole test assembly, on 127.0.0.1:3306 as root with a blank password. The tables are created from
	///the Open Dental 25.3.48 reference schema in the repository, so they match the office's columns exactly. Holds synthetic rows only.
	///Tests that need it call RequireDb(), which marks them inconclusive when the server is unreachable or SIGNATURE_TEST_DB=skip.</summary>
	[TestClass]
	public class ScratchDb {
		public const string DB_NAME="asig_signature_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private static readonly string[] _arrayTables={ "userod","patientnote","sheetfield" };
		private static string _skipReason;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			if(Environment.GetEnvironmentVariable("SIGNATURE_TEST_DB")=="skip") {
				_skipReason="SIGNATURE_TEST_DB=skip";
				return;
			}
			try {
				string schema=File.ReadAllText(TestPaths.GetRepoFile("OpenDentBusiness","Misc","ForkMigrations","Reference","OdSchema-25.3.48.sql"));
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+DB_NAME);
					for(int i=0;i<_arrayTables.Length;i++) {
						Exec(connection,GetCreateTable(schema,_arrayTables[i]));
					}
					//The CRUD layer reads RandomPrimaryKeys on every insert. Kept for the whole run; RequireDb does not empty it.
					Exec(connection,GetCreateTable(schema,"preference"));
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; the next run drops it.
			}
		}

		///<summary>Marks the calling test inconclusive when there is no scratch database. Otherwise empties every table.</summary>
		public static void RequireDb() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			for(int i=0;i<_arrayTables.Length;i++) {
				DataCore.NonQ("DELETE FROM "+_arrayTables[i]);
			}
		}

		///<summary>The CREATE TABLE statement for one table, cut from the reference schema dump.</summary>
		public static string GetCreateTable(string schema,string table) {
			Match match=Regex.Match(schema,@"CREATE TABLE `"+table+@"` \(.*?\n\) ENGINE=[^;]*;",RegexOptions.Singleline);
			if(!match.Success) {
				throw new InvalidOperationException("Table "+table+" is not in the reference schema.");
			}
			return match.Value;
		}

		///<summary>Inserts a synthetic user and returns its UserNum.</summary>
		public static long InsertUser(string userName,long provNum=0,string domainUser="",bool isHidden=false,int failedAttempts=0,DateTime dateTFail=default) {
			Userod userod=new Userod();
			userod.UserName=userName;
			userod.ProvNum=provNum;
			userod.DomainUser=domainUser;
			userod.IsHidden=isHidden;
			userod.FailedAttempts=(byte)failedAttempts;
			userod.DateTFail=(dateTFail==default ? DateTime.MinValue : dateTFail);
			userod.MobileWebPin="";
			userod.EClipboardClinicalPin="";
			userod.BadgeId="";
			userod.Password="";
			return OpenDentBusiness.Crud.UserodCrud.Insert(userod);
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
