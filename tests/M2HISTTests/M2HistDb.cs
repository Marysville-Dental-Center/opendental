// One scratch database for the M2-HIST database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1, whose seed has the ten
// InsHist*Codes preferences set) upgraded by the fork migration runner, so the M2-HIST step (InsHistCodeGroupsStep) runs as it does for an office.
// The state right before and after the migration is kept for FreshDatabaseMigrationTests. Synthetic data only. Skipped (inconclusive) when the
// local MariaDB does not answer, or when FORKMIGRATION_TEST_DB=skip.
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
using OpenDentBusiness.ForkMigrations;

namespace M2HISTTests {
	[TestClass]
	public class M2HistDb {
		public static readonly string DbName=TestDb.ScratchName("m2hist");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static bool _isBuilt;
		///<summary>The ten InsHist*Codes preference values before the migration, by name.</summary>
		public static Dictionary<string,string> DictPrefsBefore;
		///<summary>The same preferences right after the migration.</summary>
		public static Dictionary<string,string> DictPrefsAfter;
		///<summary>Code groups right after the migration, from the database.</summary>
		public static List<CodeGroup> ListCodeGroupsAfter;
		///<summary>CodeGroupNum and ProcCodes of every code group before the migration.</summary>
		public static Dictionary<long,string> DictProcCodesBefore;
		///<summary>The Insurance History rows right after the migration.</summary>
		public static List<InsHistCategory> ListCategoriesAfter;
		///<summary>What the runner logged for the M2-HIST step.</summary>
		public static List<string> ListChangesStep;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DbName);
				DictPrefsBefore=ReadInsHistPrefs();
				DictProcCodesBefore=DataCore.GetTable("SELECT CodeGroupNum,ProcCodes FROM codegroup").Select()
					.ToDictionary(x => PIn.Long(x["CodeGroupNum"].ToString()),x => x["ProcCodes"].ToString());
				ForkMigrationResult result=ForkMigrationRunner.Run();
				string prefix=new OpenDentBusiness.ForkMigrations.FeatureSteps.InsHistCodeGroupsStep().Level+": ";
				ListChangesStep=result.ListChanges.Where(x => x.StartsWith(prefix)).ToList();
				DictPrefsAfter=ReadInsHistPrefs();
				ListCodeGroupsAfter=OpenDentBusiness.Crud.CodeGroupCrud.SelectMany("SELECT * FROM codegroup ORDER BY ItemOrder,CodeGroupNum");
				Cache.Refresh(InvalidType.AllLocal);
				ListCategoriesAfter=InsHistCategories.GetAll();
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
				_isBuilt=true;
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		private static Dictionary<string,string> ReadInsHistPrefs() {
			string names=string.Join(",",Prefs.GetInsHistPrefNames().Select(x => "'"+x+"'"));
			return DataCore.GetTable("SELECT PrefName,ValueString FROM preference WHERE PrefName IN ("+names+")").Select()
				.ToDictionary(x => x["PrefName"].ToString(),x => x["ValueString"].ToString());
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuilt || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
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
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestDb.Connect(DbName);
		}
	}
}
