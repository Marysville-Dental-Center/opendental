// The legacy ReportsTests (UnitTests/UnitTests/ReportsTests.cs, Reports_GetTable_MySqlUserLow), copied here by package S0-T2 of the .NET 10
// plan; the original stays in UnitTests. Through the Middle Tier, reports and user queries (Reports.GetTable, Meth.GetTableLow and
// DataCore.GetTableLow on the server) run as the low-privilege MySQL user, so a query can SELECT and SHOW but cannot change the database. The
// other tests of this assembly give their mock server root as the low user, so only this one shows that the low user is used and is limited.
// The test keeps its name and its arrange, act and assert; only the harness changed: the s0t2_ copy of ScratchDb's database (ScratchDbCopy.cs)
// instead of the legacy unittest database and TestBase, and, per test instead of per class, a second mock server whose low user is a
// SELECT-only account made with CREATE USER and GRANT on the local MariaDB (127.0.0.1), as tests/DbGrantsTests/GrantRun.cs makes its account.
// The account's name is unique to the run and starts with s0t2_ (the legacy class used the fixed name unittestuserlow, with SELECT on every
// database); it may SELECT in the copy only, has a random password, and is dropped in the clean-up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;
using System.Threading;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.WebServices;

namespace MiddleTierMockTests {
	[TestClass]
	public class ReportsTests {
		private static readonly string USER_LOW_NAME=ScratchDbCopy.Name("low");
		private static readonly string USER_LOW_PASS=Guid.NewGuid().ToString("N");

		[TestInitialize]
		public void SetupTest() {
			ScratchDbCopy.BeginTest();
			//Create a new user with this run's name as the database user name.
			//Only give the SELECT permission to simulate a user of lower status in life.
			ScratchDbCopy.CreateSelectOnlyAccount(USER_LOW_NAME,USER_LOW_PASS);
			//Pass in new connection settings so that this plebeian is set as our "UserLow" when invoking Reports.GetTable().
			//The role stays ClientDirect, as in the legacy class: the test's RunMiddleTierMock makes it a Middle Tier client through this mock.
			OpenDentalServerProxy.MockOpenDentalServerCur=ScratchDbCopy.NewMockServer(userLow:USER_LOW_NAME,passLow:USER_LOW_PASS);
		}

		[TestCleanup]
		public void TearDownTest() {
			//Put the Middle Tier mock server back to the way it was before the test, then drop the user that SetupTest() created.
			ScratchDb.UseDirectConnection();
			if(ScratchDb.SkipReason==null) {
				ScratchDbCopy.DropAccount(USER_LOW_NAME);
			}
			ScratchDbCopy.EndTest();
		}

		[ClassCleanup]
		public static void TearDownClass() {
			if(ScratchDb.SkipReason==null) {
				ODException.SwallowAnyException(() => ScratchDbCopy.DropAccount(USER_LOW_NAME));
			}
			ScratchDbCopy.DropCopy();
		}

		[TestMethod]
		public void Reports_GetTable_MySqlUserLow() {
			Exception ex=null;
			//Spawn a new thread so that we don't manipulate any global DataConnection settings.
			ODThread thread=new ODThread(o => {
				//Prepare some simple queries to verify how both user and user low react.
				string tempTableName="tmpreportsunittesttable";
				string tempTableDropQuery=$"DROP TABLE IF EXISTS {tempTableName}";
				string tempTableCreateQuery=$"CREATE TABLE {tempTableName} (Col1 VARCHAR(1))";
				string tempTableShowQuery=$"SHOW TABLES LIKE '{tempTableName}'";
				//Make sure that we can create and drop tables as the normal user but cannot do the same thing via Reports.GetTable().
				//First, make sure that the regular user works as intended.
				DataCore.NonQ(tempTableDropQuery);
				DataCore.NonQ(tempTableCreateQuery);
				Assert.AreEqual(1,DataCore.GetTable(tempTableShowQuery).Rows.Count,"'root' user was not able to create a new table.");
				DataCore.NonQ(tempTableDropQuery);
				//Next, make sure that user low cannot create the new table.  Required to use the Middle Tier otherwise user low is ignored.
				DataAction.RunMiddleTierMock(() => {
					//User low should be able to run SELECT and SHOW commands.
					if(Reports.GetTable(tempTableShowQuery).Rows.Count!=0) {//Should have been dropped via root user above.
						throw new ApplicationException("Temporary table was not dropped correctly.");
					}
					//Reports.GetTable() should throw an exception due to the lack of the CREATE permission.  Swallow it.
					ODException.SwallowAnyException(() => Reports.GetTable(tempTableCreateQuery));
					//User low should not have been able to create the table.
					if(Reports.GetTable(tempTableShowQuery).Rows.Count!=0) {
						throw new ApplicationException("User low was able to create a table.");
					}
				});
			});
			thread.AddExceptionHandler(e => { ex=e; });//This will cause the unit test to fail.
			thread.Name="thread"+MethodBase.GetCurrentMethod().Name;
			thread.Start();
			thread.Join(Timeout.Infinite);
			Assert.IsNull(ex,ex?.Message);
		}

	}
}
