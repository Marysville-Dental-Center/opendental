// One scratch database for the B-DBM tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the current schema
// (Open Dental 26.1.70) by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.FileIO;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace BDBMTests {
	[TestClass]
	public class BDbmDb {
		public static readonly string DbName=TestDb.ScratchName("bdbm");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(DbName);
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security);
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(SkipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			TestDb.Connect(DbName);
			Security.CurUser=Security.CurUser??new Userod();
		}

		///<summary>A made-up patient for one test.</summary>
		public static Patient CreatePatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Bdbm",fName:"Synthetic");
		}

		///<summary>Writes a text value exactly as given (through a query parameter), so a test can store characters that the CRUD layer would clean.
		///</summary>
		public static void SetText(string tableName,string columnName,string keyName,long priKey,string value) {
			DataCore.NonQ("UPDATE "+tableName+" SET "+columnName+"=?paramValue WHERE "+keyName+"="+priKey,
				new OdSqlParameter("paramValue",OdDbType.Text,value));
		}

		///<summary>Reads one text value as stored.</summary>
		public static string GetText(string tableName,string columnName,string keyName,long priKey) {
			DataTable table=DataCore.GetTable("SELECT "+columnName+" FROM "+tableName+" WHERE "+keyName+"="+priKey);
			Assert.AreEqual(1,table.Rows.Count,tableName+" row "+priKey+" not found.");
			return table.Rows[0][0].ToString();
		}

		///<summary>Switches image storage: InDatabase keeps files in document.RawBase64; LocalAtoZ uses atoZPath as this computer's A to Z folder.
		///</summary>
		public static void SetStorage(DataStorageType dataStorageType,string atoZPath=null) {
			Prefs.UpdateInt(PrefName.AtoZfolderUsed,(int)dataStorageType);
			Cache.Refresh(InvalidType.Prefs);
			FileAtoZ.LocalAtoZpath=atoZPath;
		}

		///<summary>The first image category.</summary>
		public static long GetImageCategory() {
			return Defs.GetDefsForCategory(DefCat.ImageCats,true).First().DefNum;
		}

		///<summary>Security log rows of one permission for the patient (0 for any patient), oldest first.</summary>
		public static List<SecurityLog> GetLogs(EnumPermType perm,long patNum) {
			string command="SELECT * FROM securitylog WHERE PermType="+(int)perm;
			if(patNum!=0) {
				command+=" AND PatNum="+patNum;
			}
			command+=" ORDER BY SecurityLogNum";
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany(command);
		}
	}
}
