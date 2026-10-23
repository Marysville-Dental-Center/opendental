// One scratch database for the A-PAY tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open Dental
// 25.3.48 schema by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace PayPlanTests {
	[TestClass]
	public class PayPlanDb {
		public static readonly string DbName=TestDb.ScratchName("apay");
		///<summary>Null when the database is ready; otherwise why the tests are skipped.</summary>
		public static string SkipReason;
		///<summary>The fork migration result, for the tests that check what the migration did.</summary>
		public static ForkMigrationResult MigrationResult;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(DbName);
			MigrationResult=ForkMigrationRunner.Run();
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
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Sheets,InvalidType.Security);
		}

		///<summary>A made-up patient for one test.</summary>
		public static Patient CreatePatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Apay",fName:"Synthetic");
		}

		///<summary>Logs in a new user whose only group has exactly these permissions. Returns the user.</summary>
		public static Userod LogInWith(params EnumPermType[] arrayPermTypes) {
			long userGroupNum=UserGroupT.CreateUserGroup("A-PAY test group "+Guid.NewGuid().ToString("N").Substring(0,8));
			for(int i=0;i<arrayPermTypes.Length;i++) {
				GroupPermissions.Insert(new GroupPermission {
					UserGroupNum=userGroupNum,
					PermType=arrayPermTypes[i],
					NewerDate=DateTime.MinValue,
					NewerDays=0,
				});
			}
			GroupPermissions.RefreshCache();
			Userod userod=UserodT.CreateUser(userGroupNumbers:new List<long> { userGroupNum });
			Security.CurUser=userod;
			return userod;
		}
	}
}
