using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DataConnectionBase;

namespace UnitTests.ODInstaller_Tests {
	[TestClass]
	public class ODInstallerTests:TestBase {
		[ClassInitialize]
		public static void SetupClass(TestContext testContext) {
			//Add anything here that you want to run once before the tests in this class run.
		}

		[TestInitialize]
		public void SetupTest() {
			//Add anything here that you want to run before every test in this class.
		}

		[TestCleanup]
		public void TearDownTest() {
			//Add anything here that you want to run after every test in this class.
		}

		[ClassCleanup]
		public static void TearDownClass() {
			//Add anything here that you want to run after all the tests in this class have been run.
		}

		[TestMethod]
		public void ODInstallerTests_DbAdminMysql_CreateAndDestroyAdminUsers() {
			//Two full-permission users on the local server, with names unique to this run (they were the fixed fakeuser1 and fakeuser2) and random
			//passwords; whatever happens, neither is left behind.
			string userName1=GetRunMySqlUserName("utadm1");
			string userName2=GetRunMySqlUserName("utadm2");
			DataConnection conAdmin=new DataConnection();//A brand new admin connection to the unittest### database.
			try {
				Assert.AreEqual(null,DbAdminMysql.ModifyUser(conAdmin,userName1,GetRandomMySqlPassword(),userName1));//Grants, verifies new user.
				Assert.AreEqual(null,DbAdminMysql.ModifyUser(conAdmin,userName2,GetRandomMySqlPassword(),userName1));//Drops user 1 and verifies.
				CollectionAssert.DoesNotContain(DbAdminMysql.GetUsers(conAdmin),userName1);
				try {
					DbAdminMysql.DropUser(conAdmin,userName2);//Drops user 2 and verifies.
				}
				catch(Exception ex) {
					Assert.Fail(ex.Message);
				}
			}
			finally {
				CodeBase.ODException.SwallowAnyException(() => DbAdminMysql.DropUser(conAdmin,userName1));
				CodeBase.ODException.SwallowAnyException(() => DbAdminMysql.DropUser(conAdmin,userName2));
			}
		}

	}
}
