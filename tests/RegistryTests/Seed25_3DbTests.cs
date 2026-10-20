// Integration tests for the 24.3 upgrade seed (OpenDentBusiness/Misc/ForkMigrations/Seed25_3.cs) against a scratch MariaDB database on this
// computer, filled with synthetic rows only. They are skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank
// password, or when REGISTRY_TEST_DB=skip.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace RegistryTests {
	[TestClass]
	public class Seed25_3DbTests {
		private const string DB_NAME="registry_m1_02_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("REGISTRY_TEST_DB")=="skip") {
				_skipReason="REGISTRY_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+DB_NAME);
					//The same definitions as Open Dental 24.3's tables, cut down to what the seed touches.
					Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
						+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE usergroup (UserGroupNum bigint(20) NOT NULL AUTO_INCREMENT,Description varchar(255) DEFAULT '',"
						+"UserGroupNumCEMT bigint(20) NOT NULL,PRIMARY KEY (UserGroupNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE grouppermission (GroupPermNum bigint(20) NOT NULL AUTO_INCREMENT,NewerDate date NOT NULL DEFAULT '0001-01-01',"
						+"NewerDays int(11) NOT NULL,UserGroupNum bigint(20) NOT NULL,PermType smallint(6) NOT NULL,FKey bigint(20) NOT NULL,"
						+"PRIMARY KEY (GroupPermNum),KEY FKey (FKey),KEY UserGroupNum (UserGroupNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE eform (EFormNum bigint(20) NOT NULL AUTO_INCREMENT,PatNum bigint(20) NOT NULL,Status tinyint(4) NOT NULL,"
						+"PRIMARY KEY (EFormNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
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

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			foreach(string table in new[] { "preference","usergroup","grouppermission","eform" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>Synthetic data: three user groups. Group 1 has ImageEdit and PayPlanEdit, group 2 has ProcExistingEdit, group 3 has nothing.
		///Four eforms with statuses 1, 2, 3 and 4.</summary>
		private static void FillSyntheticOffice(string dataBaseVersion) {
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('DataBaseVersion','"+POut.String(dataBaseVersion)+"')");
			DataCore.NonQ("INSERT INTO usergroup (UserGroupNum,Description,UserGroupNumCEMT) VALUES(1,'Front desk',0),(2,'Hygiene',0),(3,'Read only',0)");
			AddPerm(1,EnumPermType.ImageEdit);
			AddPerm(1,EnumPermType.PayPlanEdit);
			AddPerm(2,EnumPermType.ProcExistingEdit);
			DataCore.NonQ("INSERT INTO eform (PatNum,Status) VALUES(1,1),(2,2),(3,3),(4,4)");
		}

		private static void AddPerm(long userGroupNum,EnumPermType permType) {
			DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('0001-01-01',0,"+userGroupNum+","+(int)permType+",0)");
		}

		private static List<long> GetGroupsWith(EnumPermType permType) {
			return DataCore.GetTable("SELECT UserGroupNum FROM grouppermission WHERE PermType="+(int)permType+" ORDER BY UserGroupNum")
				.Select().Select(x => Convert.ToInt64(x[0])).ToList();
		}

		private static string GetPref(string prefName) {
			DataTable table=DataCore.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+prefName+"'");
			return table.Rows.Count==0 ? null : table.Rows[0][0].ToString();
		}

		private static string Snapshot() {
			return string.Join(";",DataCore.GetTable("SELECT PrefName,ValueString FROM preference ORDER BY PrefNum").Select().Select(x => x[0]+"="+x[1]))
				+"|"+string.Join(";",DataCore.GetTable("SELECT UserGroupNum,PermType,FKey FROM grouppermission ORDER BY GroupPermNum").Select().Select(x => x[0]+"/"+x[1]+"/"+x[2]))
				+"|"+string.Join(";",DataCore.GetTable("SELECT EFormNum,Status FROM eform ORDER BY EFormNum").Select().Select(x => x[0]+"/"+x[1]));
		}

		[TestMethod]
		public void From24_3_SeedsPrefsGrantsAndEFormStatuses() {
			FillSyntheticOffice("24.3.63.0");
			Assert.IsTrue(Seed25_3.IsFrom24_3());
			List<string> listChanges=Seed25_3.Run();
			Assert.IsTrue(listChanges.Count>0);
			Assert.AreEqual("0",GetPref("ApptMirrorSecondary"));
			Assert.AreEqual("0",GetPref("TaskCategoryShow"));
			CollectionAssert.AreEqual(new List<long> { 1 },GetGroupsWith(EnumPermType.ImageSignatureCreate),"From ImageEdit holders.");
			CollectionAssert.AreEqual(new List<long> { 1 },GetGroupsWith(EnumPermType.SignedImageEdit),"From ImageEdit holders.");
			CollectionAssert.AreEqual(new List<long> { 1,2,3 },GetGroupsWith(EnumPermType.BlockoutsFlagged),"Every user group.");
			CollectionAssert.AreEqual(new List<long> { 1 },GetGroupsWith(EnumPermType.PayPlanUnlock),"From PayPlanEdit holders.");
			CollectionAssert.AreEqual(new List<long> { 2 },GetGroupsWith(EnumPermType.ProcTPEditFee),"From ProcExistingEdit holders.");
			foreach(EnumPermType permType in new[] { EnumPermType.SendAlertsFromHQ,EnumPermType.TextAllEmployees,EnumPermType.PhoneExtension,EnumPermType.EFormImport }) {
				Assert.AreEqual(0,GetGroupsWith(permType).Count,permType+" is never granted.");
			}
			Assert.AreEqual("1/1;2/2;3/2;4/2",string.Join(";",DataCore.GetTable("SELECT EFormNum,Status FROM eform ORDER BY EFormNum").Select()
				.Select(x => x[0]+"/"+x[1])),"Statuses 3 and 4 become Filled (2); others are untouched.");
		}

		[TestMethod]
		public void SecondRun_ChangesNothing() {
			FillSyntheticOffice("24.3.63.0");
			Seed25_3.Run();
			string snapshot=Snapshot();
			List<string> listChanges=Seed25_3.Run();
			Assert.AreEqual(0,listChanges.Count,string.Join(", ",listChanges));
			Assert.AreEqual(snapshot,Snapshot());
		}

		[TestMethod]
		public void OpenDental25_3Database_IsLeftAlone() {
			FillSyntheticOffice("25.3.48.0");
			string snapshot=Snapshot();
			Assert.IsFalse(Seed25_3.IsFrom24_3());
			List<string> listChanges=Seed25_3.Run();
			Assert.AreEqual(1,listChanges.Count);
			StringAssert.Contains(listChanges[0],"skipped");
			Assert.AreEqual(snapshot,Snapshot(),"A database Open Dental already converted keeps the office's own rows.");
		}

		[TestMethod]
		public void ExistingPrefValueAndConfiguredPermission_AreNotOverwritten() {
			FillSyntheticOffice("24.3.63.0");
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('TaskCategoryShow','1')");
			AddPerm(3,EnumPermType.BlockoutsFlagged);//An administrator already set this permission up.
			Seed25_3.Run();
			Assert.AreEqual("1",GetPref("TaskCategoryShow"));
			Assert.AreEqual(1,DataCore.GetTable("SELECT * FROM preference WHERE PrefName='TaskCategoryShow'").Rows.Count);
			CollectionAssert.AreEqual(new List<long> { 3 },GetGroupsWith(EnumPermType.BlockoutsFlagged));
		}

		[TestMethod]
		public void UnknownVersion_IsNotSeeded() {
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('DataBaseVersion','')");
			Assert.IsFalse(Seed25_3.IsFrom24_3());
			string snapshot=Snapshot();
			Seed25_3.Run();
			Assert.AreEqual(snapshot,Snapshot());
		}

		[TestMethod]
		public void GrantingAReservedPermission_Throws() {
			FillSyntheticOffice("24.3.63.0");
			try {
				Seed25_3.GrantIfNeverConfigured(EnumPermType.PhoneExtension,EnumPermType.None);
				Assert.Fail("Expected an ArgumentException.");
			}
			catch(ArgumentException) {
				//Expected.
			}
			Assert.AreEqual(0,GetGroupsWith(EnumPermType.PhoneExtension).Count);
		}
	}

	[TestClass]
	public class Seed25_3VersionTests {
		[TestMethod]
		public void IsVersionFrom24_3() {
			Assert.IsTrue(Seed25_3.IsVersionFrom24_3("24.3.63.0"));
			Assert.IsTrue(Seed25_3.IsVersionFrom24_3("24.3.63"));
			Assert.IsTrue(Seed25_3.IsVersionFrom24_3("23.1.10.0"));
			Assert.IsFalse(Seed25_3.IsVersionFrom24_3("24.4.0.0"));
			Assert.IsFalse(Seed25_3.IsVersionFrom24_3("25.3.48.0"));
			Assert.IsFalse(Seed25_3.IsVersionFrom24_3(""));
			Assert.IsFalse(Seed25_3.IsVersionFrom24_3(null));
			Assert.IsFalse(Seed25_3.IsVersionFrom24_3("not a version"));
		}
	}
}
