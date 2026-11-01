// One scratch database for the V-BRIDGE1 row tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork migration
// runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace VBRIDGE1Tests {
	[TestClass]
	public class BridgeDb {
		public static readonly string DbName=TestDb.ScratchName("vbridge1");
		///<summary>Null when the database is ready; otherwise why the row tests are skipped.</summary>
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
			RefreshCaches();
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
			RefreshCaches();
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs);
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			ToolButItems.RefreshCache();
		}

		///<summary>Deletes every program row with this name, with its property and toolbar rows, so a test starts from a database without it.</summary>
		public static void DeleteProgram(ProgramName programName) {
			foreach(Program program in GetProgramsFromDb(programName)) {
				long programNum=program.ProgramNum;
				DataCore.NonQ("DELETE FROM programproperty WHERE ProgramNum="+POut.Long(programNum));
				DataCore.NonQ("DELETE FROM toolbutitem WHERE ProgramNum="+POut.Long(programNum));
				DataCore.NonQ("DELETE FROM program WHERE ProgramNum="+POut.Long(programNum));
			}
			RefreshCaches();
		}

		///<summary>The program rows with this exact name, straight from the database.</summary>
		public static List<Program> GetProgramsFromDb(ProgramName programName) {
			return OpenPmsDentalBusiness.Crud.ProgramCrud.SelectMany("SELECT * FROM program WHERE ProgName='"+POut.String(programName.ToString())+"' "
				+"ORDER BY ProgramNum");
		}

		public static List<ToolButItem> GetToolButItemsFromDb(long programNum) {
			return OpenPmsDentalBusiness.Crud.ToolButItemCrud.SelectMany("SELECT * FROM toolbutitem WHERE ProgramNum="+POut.Long(programNum)+" "
				+"ORDER BY ToolBar");
		}

		public static List<ProgramProperty> GetPropertiesFromDb(long programNum) {
			return OpenPmsDentalBusiness.Crud.ProgramPropertyCrud.SelectMany("SELECT * FROM programproperty WHERE ProgramNum="+POut.Long(programNum)+" "
				+"ORDER BY ProgramPropertyNum");
		}

		///<summary>Sets one ClinicNum 0 property value and refreshes the cache.</summary>
		public static void SetProperty(long programNum,string desc,string value) {
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='"+POut.String(value)+"' WHERE ProgramNum="+POut.Long(programNum)+" "
				+"AND PropertyDesc='"+POut.String(desc)+"' AND ClinicNum=0");
			ProgramProperties.RefreshCache();
		}
	}
}
