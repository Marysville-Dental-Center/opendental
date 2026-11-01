// One scratch database for the B-PAY tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open Dental 25.3.48
// schema by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace BPAYTests {
	[TestClass]
	public class BPayDb {
		public static readonly string DbName=TestDb.ScratchName("bpay");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static int _counter;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			UseEnglishCulture();
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

		///<summary>Amounts in audit text are formatted with the current culture, and Lans.g only skips the translation cache for en-US.</summary>
		public static void UseEnglishCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			UseEnglishCulture();
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			TestDb.Connect(DbName);
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security,InvalidType.ProcCodes);
		}

		///<summary>A made-up patient. Pass a guarantor to put the patient in that family.</summary>
		public static Patient CreatePatient(string suffix,long guarantor=0) {
			return PatientT.CreatePatient(suffix,lName:"Bpay",fName:"Synthetic"+suffix,guarantor:guarantor);
		}

		///<summary>A procedure on a made-up, non-CDT procedure code.</summary>
		public static Procedure CreateProcedure(Patient patient,ProcStat procStat,double fee) {
			string procCode="BP"+(Interlocked.Increment(ref _counter)%10000).ToString("0000");
			ProcedureCodeT.CreateProcCode(procCode);
			Cache.Refresh(InvalidType.ProcCodes);
			return ProcedureT.CreateProcedure(patient,procCode,procStat,"",fee,DateTime.Today);
		}

		///<summary>Logs in a new user whose only group has exactly these permissions. Returns the user.</summary>
		public static Userod LogInWith(params EnumPermType[] arrayPermTypes) {
			long userGroupNum=UserGroupT.CreateUserGroup("B-PAY test group "+Guid.NewGuid().ToString("N").Substring(0,8));
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

		///<summary>The PayPlanEdit audit entries for the plan, oldest first.</summary>
		public static List<string> GetPayPlanEditLogTexts(long payPlanNum) {
			List<string> listTexts=new List<string>();
			System.Data.DataTable table=DataCore.GetTable("SELECT LogText FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PayPlanEdit)
				+" AND FKey="+POut.Long(payPlanNum)+" ORDER BY SecurityLogNum");
			for(int i=0;i<table.Rows.Count;i++) {
				listTexts.Add(table.Rows[i]["LogText"].ToString());
			}
			return listTexts;
		}
	}
}
