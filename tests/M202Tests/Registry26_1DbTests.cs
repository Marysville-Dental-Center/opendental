// Integration tests for the M2-02 seed rows (Seed26_1, Seed26_1Step), the Employee Titles definition rules and the Chart Patient Information
// Gender field, against a scratch MariaDB database on this computer. The database gets the Open Dental 26.1.70 schema from the reference snapshot
// embedded in OpenDentBusiness (Seed26_1 runs after the 25.4 and 26.1 schema steps of package M2-01), and synthetic rows only. Its name is unique to the run and it is dropped afterwards. The tests are skipped
// (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when M202_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace M202Tests {
	[TestClass]
	public class Registry26_1DbTests {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		///<summary>Unique to this run (time, process id, random), because several worktrees and test runners share the one local server.</summary>
		private static readonly string _dbName="m202_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("M202_TEST_DB")=="skip") {
				_skipReason="M202_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			using(MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+_dbName)) {
				connection.Open();
				//The default reference is 26.1.70 (package M2-01), so employee.Title, which the Employee Titles rules read, is part of it.
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,_dbName,"root","","","",DatabaseType.MySql,true);
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; its name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			foreach(string table in new[] { "preference","usergroup","grouppermission","securitylog","definition","employee" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		#region Synthetic data
		private static void AddPref(string prefName,string valueString) {
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(prefName)+"','"+POut.String(valueString)+"')");
		}

		private static string GetPref(string prefName) {
			DataTable table=DataCore.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			return table.Rows.Count==0 ? null : table.Rows[0][0].ToString();
		}

		private static void AddPerm(long userGroupNum,int permType,int newerDays=0,string newerDate="0001-01-01",long fKey=0) {
			DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('"+newerDate+"',"+newerDays+","
				+userGroupNum+","+permType+","+fKey+")");
		}

		private static List<string> GetPerms(int permType) {
			return DataCore.GetTable("SELECT UserGroupNum,NewerDays,NewerDate,FKey FROM grouppermission WHERE PermType="+permType+" ORDER BY UserGroupNum")
				.Select().Select(x => x[0]+"/"+x[1]+"/"+PIn.Date(x[2].ToString()).ToString("yyyy-MM-dd")+"/"+x[3]).ToList();
		}

		///<summary>Three user groups: 1 front desk holds Blockouts with a 30-day limit and the 25.3 PhoneExtension number (from Set All in Open Dental),
		///2 hygiene holds Blockouts, 3 read only holds nothing.</summary>
		private static void FillSyntheticOffice(string dataBaseVersion,string adoptedFrom=null) {
			AddPref("DataBaseVersion",dataBaseVersion);
			if(adoptedFrom!=null) {
				AddPref(ForkVersions.PREF_FORK_ADOPTED_FROM,adoptedFrom);
			}
			DataCore.NonQ("INSERT INTO usergroup (UserGroupNum,Description,UserGroupNumCEMT) VALUES(1,'Front desk',0),(2,'Hygiene',0),(3,'Read only',0)");
			AddPerm(1,(int)EnumPermType.Blockouts,newerDays:30);
			AddPerm(1,272);//PhoneExtension as Open Dental 25.3 numbered it.
			AddPerm(2,(int)EnumPermType.Blockouts);
			AddPerm(2,(int)EnumPermType.BlockoutsFlagged);
		}

		private static ForkMigrationResult RunStep(bool isRunAll=false) {
			ForkMigrationOptions options=new ForkMigrationOptions();
			options.ListSteps=new List<ForkMigrationStep> { new Seed26_1Step() };
			options.RunAll=isRunAll;
			return ForkMigrationRunner.Run(options);
		}

		private static string Snapshot() {
			return string.Join(";",DataCore.GetTable("SELECT PrefName,ValueString FROM preference WHERE PrefName NOT LIKE 'ForkMigration%' "
				+"AND PrefName!='ForkSchemaLevel' ORDER BY PrefNum").Select().Select(x => x[0]+"="+x[1]))
				+"|"+string.Join(";",DataCore.GetTable("SELECT GroupPermNum,UserGroupNum,PermType,FKey,NewerDays FROM grouppermission ORDER BY GroupPermNum")
				.Select().Select(x => x[0]+"/"+x[1]+"/"+x[2]+"/"+x[3]+"/"+x[4]));
		}
		#endregion Synthetic data

		[TestMethod]
		public void From25_3_SplitsBlockoutsAndMovesPhoneExtension() {
			FillSyntheticOffice("25.3.48.0");
			ForkMigrationResult result=RunStep();
			CollectionAssert.AreEqual(new List<string> { "1/0/0001-01-01/0" },GetPerms((int)EnumPermType.PhoneExtension),"272 from 25.3 is now 274.");
			CollectionAssert.AreEqual(new List<string> { "1/30/0001-01-01/0","2/0/0001-01-01/0" },GetPerms((int)EnumPermType.BlockoutAdd),
				"BlockoutAdd goes to the Blockouts holders, with the same limits, and the old 272 row did not survive under the new meaning.");
			CollectionAssert.AreEqual(new List<string> { "1/30/0001-01-01/0","2/0/0001-01-01/0" },GetPerms((int)EnumPermType.BlockoutDelete));
			CollectionAssert.AreEqual(new List<string> { "1/30/0001-01-01/0","2/0/0001-01-01/0" },GetPerms((int)EnumPermType.Blockouts),"Blockouts stays.");
			Assert.AreEqual(0,GetPerms((int)EnumPermType.PatientSelected).Count,"Audit-only permissions are not granted.");
			Assert.IsTrue(result.ListChanges.Any(x => x.Contains("Moved 1 Open Dental 25.3 PhoneExtension")),string.Join("\r\n",result.ListChanges));
			foreach((PrefName prefName,string valueString) in Seed26_1.ListPrefDefaults) {
				Assert.AreEqual(valueString,GetPref(prefName.ToString()),prefName.ToString());
			}
		}

		[TestMethod]
		public void SecondRun_ChangesNothing() {
			FillSyntheticOffice("25.3.48.0");
			RunStep();
			string snapshotBefore=Snapshot();
			ForkMigrationResult resultSkip=RunStep();
			Assert.AreEqual(0,resultSkip.ListStepsRun.Count,"The level is recorded.");
			ForkMigrationResult resultAll=RunStep(isRunAll:true);
			Assert.AreEqual(1,resultAll.ListStepsRun.Count);
			Assert.AreEqual(0,resultAll.ListChanges.Count,string.Join("\r\n",resultAll.ListChanges));
			Assert.AreEqual(snapshotBefore,Snapshot(),"272 now means BlockoutAdd and must not be renumbered again.");
		}

		[TestMethod]
		public void From24_3_SplitsBlockouts() {
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('DataBaseVersion','24.3.63.0')");
			DataCore.NonQ("INSERT INTO usergroup (UserGroupNum,Description,UserGroupNumCEMT) VALUES(1,'Front desk',0)");
			AddPerm(1,(int)EnumPermType.Blockouts);
			RunStep();
			CollectionAssert.AreEqual(new List<string> { "1/0/0001-01-01/0" },GetPerms((int)EnumPermType.BlockoutAdd));
			CollectionAssert.AreEqual(new List<string> { "1/0/0001-01-01/0" },GetPerms((int)EnumPermType.BlockoutDelete));
			Assert.AreEqual(0,GetPerms((int)EnumPermType.PhoneExtension).Count);
		}

		[TestMethod]
		public void AdoptedFrom25_3_IsJudgedByTheAdoptionRecord() {
			FillSyntheticOffice(ForkVersions.SentinelVersion.ToString(),"source=25.3.48.0;started=x");
			Assert.IsTrue(Seed26_1Step.IsFromBefore25_4(new ForkMigrationContext(_dbName)));
			RunStep();
			Assert.AreEqual(2,GetPerms((int)EnumPermType.BlockoutAdd).Count);
			Assert.AreEqual(1,GetPerms((int)EnumPermType.PhoneExtension).Count);
		}

		[TestMethod]
		public void From26_1OrUnknown_GetsPreferencesOnly() {
			//A database Open Dental already converted to 26.1 (adopted later) has its own split and its own 272 rows: they are BlockoutAdd.
			FillSyntheticOffice(ForkVersions.SentinelVersion.ToString(),"source=26.1.70.0;started=x");
			ForkMigrationContext context=new ForkMigrationContext(_dbName);
			Assert.IsFalse(Seed26_1Step.IsFromBefore25_4(context));
			RunStep();
			CollectionAssert.AreEqual(new List<string> { "1/0/0001-01-01/0" },GetPerms(272),"Left as Open Dental wrote it.");
			Assert.AreEqual(0,GetPerms((int)EnumPermType.BlockoutDelete).Count);
			Assert.AreEqual("1",GetPref(nameof(PrefName.RepeatingChargesAllowBackdating)),"Preference rows go to every database.");
			//No adoption record and the sentinel version: the origin is unknown, so nothing is converted.
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+ForkVersions.PREF_FORK_ADOPTED_FROM+"'");
			Assert.IsFalse(Seed26_1Step.IsFromBefore25_4(context));
			Assert.IsFalse(Seed26_1.IsVersionBefore25_4(""));
			Assert.IsFalse(Seed26_1.IsVersionBefore25_4("not a version"));
			Assert.IsTrue(Seed26_1.IsVersionBefore25_4("25.3.48.0"));
			Assert.IsFalse(Seed26_1.IsVersionBefore25_4("25.4.0.0"));
		}

		[TestMethod]
		public void Preferences_ExistingValuesAreKept() {
			AddPref(nameof(PrefName.ImagingAllowPdfJavascript),"1");
			List<string> listChanges=Seed26_1.InsertMissingPrefs();
			Assert.AreEqual("1",GetPref(nameof(PrefName.ImagingAllowPdfJavascript)),"The office's choice stays.");
			Assert.AreEqual(Seed26_1.ListPrefDefaults.Count-1,listChanges.Count);
			Assert.AreEqual(0,Seed26_1.InsertMissingPrefs().Count);
			Assert.AreEqual(Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT,GetPref(nameof(PrefName.RecurringChargesFailEmailBody)));
		}

		[TestMethod]
		public void Grants_NeverOverwriteAnAdministratorsChoice() {
			DataCore.NonQ("INSERT INTO usergroup (UserGroupNum,Description,UserGroupNumCEMT) VALUES(1,'Front desk',0),(2,'Hygiene',0)");
			AddPerm(1,(int)EnumPermType.Blockouts);
			AddPerm(2,(int)EnumPermType.Blockouts);
			AddPerm(2,(int)EnumPermType.BlockoutDelete);//Already set up for one group.
			Assert.AreEqual(0,Seed26_1.GrantIfNeverConfigured(EnumPermType.BlockoutDelete,EnumPermType.Blockouts));
			CollectionAssert.AreEqual(new List<string> { "2/0/0001-01-01/0" },GetPerms((int)EnumPermType.BlockoutDelete));
			Assert.AreEqual(2,Seed26_1.GrantIfNeverConfigured(EnumPermType.BlockoutAdd,EnumPermType.Blockouts));
			try {
				Seed26_1.GrantIfNeverConfigured(EnumPermType.LogScraperHQ,EnumPermType.Blockouts);
				Assert.Fail("Reserved permissions are never granted.");
			}
			catch(ArgumentException) {
			}
		}

		[TestMethod]
		public void EmployeeTitles_InUseRulesReadEmployeeTitle() {
			DataCore.NonQ("INSERT INTO definition (DefNum,Category,ItemOrder,ItemName,ItemValue,ItemColor,IsHidden) VALUES "
				+"(501,"+(int)DefCats26_1.EmployeeTitles+",0,'Hygienist','',0,0),(502,"+(int)DefCats26_1.EmployeeTitles+",1,'Assistant','',0,0)");
			DataCore.NonQ("INSERT INTO employee (LName,FName,PhoneExt,PayrollID,WirelessPhone,EmailWork,EmailPersonal,IsFurloughed,IsWorkingHome,ReportsTo,Title) "
				+"VALUES ('Synthetic','Emp',0,'','','','',0,0,0,501)");
			Def defUsed=new Def { DefNum=501,Category=DefCats26_1.EmployeeTitles,ItemOrder=0,ItemName="Hygienist" };
			Def defFree=new Def { DefNum=502,Category=DefCats26_1.EmployeeTitles,ItemOrder=1,ItemName="Assistant" };
			Assert.IsTrue(Defs.IsDefinitionInUse(defUsed));
			Assert.IsFalse(Defs.IsDefinitionInUse(defFree));
			try {
				Defs.Delete(defUsed);
				Assert.Fail("A title an employee uses cannot be deleted.");
			}
			catch(ApplicationException) {
			}
			Defs.Delete(defFree);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM definition WHERE Category="+(int)DefCats26_1.EmployeeTitles));
		}

		[TestMethod]
		public void ChartPatientInformation_OffersGender() {
			AddPref(nameof(PrefName.EasyHideHospitals),"1");
			AddPref(nameof(PrefName.ShowFeatureEhr),"0");
			Prefs.RefreshCache();
			List<DisplayField> listAvailable=DisplayFields.GetAllAvailableList(DisplayFieldCategory.ChartPatientInformation);
			Assert.AreEqual(1,listAvailable.Count(x => x.InternalName==DisplayFields26_1.ChartPatientInfoGender));
			Assert.AreEqual(DisplayFieldCategory.ChartPatientInformation,listAvailable.Single(x => x.InternalName=="Gender").Category);
		}
	}
}
