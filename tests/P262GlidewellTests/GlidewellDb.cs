// One scratch database for the P262-GLIDEWELL database tests: every table of the 26.2.0 reference schema (SchemaVerifier.GetReference), empty,
// plus the rows the business layer reads. Synthetic data only: nothing here reads or copies an office database, nothing connects to any server
// but 127.0.0.1:3306, and Glidewell is never called (the tests use FakeGlidewellClient).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace P262GlidewellTests {
	[TestClass]
	public class GlidewellDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="test_p262glidewell_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number, so runs from other worktrees never collide.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		public static readonly string DbName=PREFIX+_runId+"_main";
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			SkipReason=GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			DropStale();
			Recreate(DbName);
			SchemaSnapshot snapshot=SchemaVerifier.GetReference();
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"USE `"+DbName+"`");
				foreach(SchemaTable table in snapshot.Tables) {
					Exec(connection,table.ToDdl());
				}
				//The CRUD layer reads RandomPrimaryKeys on every insert; the provider cache reads EasyHideDentalSchools.
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('EasyHideDentalSchools','1')");
			}
			Connect();
			Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="Synthetic" };
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			GlidewellClients.Current=null;
			if(SkipReason!=null || Environment.GetEnvironmentVariable("P262_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it and empties the tables the tests
		///write, so every test starts from the same state.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Connect();
			foreach(string table in new[] { "glidewellacct","glidewellmsg","labcaseproduct","labcase","patient","provider","program","programproperty" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			RefreshCaches();
			GlidewellClients.Current=null;
		}

		public static void RefreshCaches() {
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			Providers.RefreshCache();
		}

		public static void Connect() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,DbName,User,Password,"","",DatabaseType.MySql,true);
		}

		#region Synthetic rows
		///<summary>Inserts the program row and turns it on or off.</summary>
		public static void SetProgramEnabled(bool isEnabled) {
			GlidewellProgram.EnsureProgram();
			DataCore.NonQ("UPDATE program SET Enabled="+(isEnabled ? "1" : "0")+" WHERE ProgName='Glidewell'");
			Programs.RefreshCache();
		}

		public static Provider CreateProvider(string abbr,string stateLicense="LIC123") {
			Provider provider=new Provider();
			provider.Abbr=abbr;
			provider.FName="Synthetic";
			provider.LName="Provider"+abbr;
			provider.StateLicense=stateLicense;
			provider.ProvNum=OpenPmsDentalBusiness.Crud.ProviderCrud.Insert(provider);
			Providers.RefreshCache();
			return provider;
		}

		public static Patient CreatePatient(string lName="Glidewelltest",string fName="Synthetic") {
			Patient patient=new Patient();
			patient.LName=lName;
			patient.FName=fName;
			patient.Gender=PatientGender.Female;
			patient.Birthdate=new DateTime(1980,2,3);
			patient.PatStatus=PatientStatus.Patient;
			patient.PatNum=OpenPmsDentalBusiness.Crud.PatientCrud.Insert(patient);
			return patient;
		}

		public static LabCase CreateLabCase(Patient patient,Provider provider,string instructions="Synthetic instructions") {
			LabCase labCase=new LabCase();
			labCase.PatNum=patient.PatNum;
			labCase.ProvNum=provider.ProvNum;
			labCase.LaboratoryNum=1;
			labCase.Instructions=instructions;
			labCase.InvoiceNum="";
			labCase.LabCaseNum=OpenPmsDentalBusiness.Crud.LabCaseCrud.Insert(labCase);
			return labCase;
		}

		///<summary>An account with a made-up API key, stored encrypted. Not the credentials of any real account.</summary>
		public static GlidewellAcct CreateAccount(string accountNumber,string apiKey="synthetic-key-"+"0001") {
			GlidewellAcct glidewellAcct=new GlidewellAcct();
			glidewellAcct.AccountNumber=accountNumber;
			if(apiKey!=null) {
				Assert.IsTrue(GlidewellAccts.SetApiKey(glidewellAcct,apiKey));
			}
			GlidewellAccts.Insert(glidewellAcct);
			return glidewellAcct;
		}

		public static long CountRows(string table,string where="1") {
			return PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM "+table+" WHERE "+where));
		}
		#endregion Synthetic rows

		#region Server
		///<summary>Null when the local server answers with a blank global sql_mode (as OpenPmsDental.exe sets it); otherwise why database tests are
		///skipped.</summary>
		private static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("P262_TEST_DB")=="skip") {
				return "P262_TEST_DB=skip";
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

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600");
			connection.Open();
			return connection;
		}

		///<summary>Drops leftovers of earlier runs (a crash, or P262_KEEP_DB=1) once they are older than 12 hours.</summary>
		private static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_p262glidewell\\_%'",
				connection))
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

		private static void Recreate(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
		}

		private static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
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
				return value==null || value==DBNull.Value ? null : value.ToString();
			}
		}
		#endregion Server
	}
}
