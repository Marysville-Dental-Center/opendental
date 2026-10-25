// One scratch database for the M2-RECUR tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork migration
// runner, which also seeds the 25.4 and 26.1 preferences (Seed26_1). Synthetic data only. Skipped (inconclusive) when the local MariaDB does not
// answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace M2RECURTests {
	[TestClass]
	public class RecurDb {
		public static readonly string DbName=TestDb.ScratchName("m2recur");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		///<summary>The fork migration result, for the tests that check the seeded preferences.</summary>
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
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security,InvalidType.Email,InvalidType.ProcCodes);
		}

		///<summary>A made-up patient. guarantor 0 makes the patient their own guarantor.</summary>
		public static Patient CreatePatient(string suffix,string email="",long guarantor=0,string preferredName="") {
			return PatientT.CreatePatient(suffix,lName:"Recur",fName:"Synthetic",email:email,guarantor:guarantor,preferredName:preferredName);
		}

		///<summary>Logs in a new user whose only group has exactly these permissions. Returns the user.</summary>
		public static Userod LogInWith(params EnumPermType[] arrayPermTypes) {
			long userGroupNum=UserGroupT.CreateUserGroup("M2-RECUR test group "+Guid.NewGuid().ToString("N").Substring(0,8));
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

		///<summary>Makes a synthetic office e-mail address the practice default (no server is ever contacted: the tests replace the sender).</summary>
		public static EmailAddress SetOfficeEmailAddress(string senderAddress) {
			EmailAddress emailAddress=new EmailAddress {
				SMTPserver="smtp.example.invalid",
				EmailUsername=senderAddress,
				EmailPassword="",
				ServerPort=587,
				UseSSL=true,
				SenderAddress=senderAddress,
				Pop3ServerIncoming="",
				AccessToken="",
				RefreshToken="",
				QueryString="",
			};
			EmailAddresses.Insert(emailAddress);
			EmailAddresses.RefreshCache();
			Prefs.UpdateLong(PrefName.EmailDefaultAddressNum,emailAddress.EmailAddressNum);
			return emailAddress;
		}

		///<summary>Removes every office e-mail address.</summary>
		public static void ClearOfficeEmailAddresses() {
			DataCore.NonQ("DELETE FROM emailaddress");
			EmailAddresses.RefreshCache();
			Prefs.UpdateLong(PrefName.EmailDefaultAddressNum,0);
		}
	}
}
