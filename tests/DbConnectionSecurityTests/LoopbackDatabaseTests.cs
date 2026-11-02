// The one place these tests connect: the local MariaDB at 127.0.0.1:3306, which the rule exempts. Scratch databases test_dbsec_*.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class LoopbackDatabaseTests {
		private const string SERVER="127.0.0.1:3306";
		private string _dbName;
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_dbName="test_dbsec_"+Guid.NewGuid().ToString("N").Substring(0,12);
			_folder=TestHelpers.NewTempFolder();
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,"","root","")))
			{
				mySqlConnection.Open();
				using(MySqlCommand mySqlCommand=new MySqlCommand("CREATE DATABASE `"+_dbName+"`",mySqlConnection)) {
					mySqlCommand.ExecuteNonQuery();
				}
			}
		}

		[TestCleanup]
		public void CleanUp() {
			try {
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(
					DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,"","root","")))
				{
					mySqlConnection.Open();
					using(MySqlCommand mySqlCommand=new MySqlCommand("DROP DATABASE IF EXISTS `"+_dbName+"`",mySqlConnection)) {
						mySqlCommand.ExecuteNonQuery();
					}
				}
			}
			finally {
				TestHelpers.DeleteFolder(_folder);
			}
		}

		[TestMethod]
		public void DataConnection_Loopback_ConnectsWithoutTls_AsBefore() {
			DataConnection dataConnection=new DataConnection(SERVER,_dbName,"root","",DatabaseType.MySql);
			Assert.AreEqual("1",dataConnection.GetScalar("SELECT 1"));
			Assert.AreEqual(_dbName,dataConnection.GetScalar("SELECT DATABASE()"));
		}

		[TestMethod]
		public void Tool_TestConnection_Loopback_ReportsTheServer_AndADatabaseWithoutTheProgram() {
			DbSettingsResult dbSettingsResult=DbSettingsRunner.TestConnection(SERVER,_dbName,"root","","","");
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			StringAssert.Contains(dbSettingsResult.Message,"Connected to "+SERVER);
			StringAssert.Contains(dbSettingsResult.Message,"has no program version");
			Assert.IsFalse(dbSettingsResult.Message.Contains("encrypted"),"This computer's server is not reached over the network.");
		}

		[TestMethod]
		public void Tool_SaveThenTest_Loopback() {
			string config=Path.Combine(_folder,"FreeDentalConfig.xml");
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=SERVER,Database=_dbName,User="root",
				Auth=DbSettingsAuth.Password,DbPassword="" },config,Path.Combine(_folder,"ca-copy.pem"));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			DbSettingsResult dbSettingsResultTest=DbSettingsRunner.TestSaved(config);
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResultTest.ExitCode,dbSettingsResultTest.Message);
		}

		[TestMethod]
		public void Tool_TestConnection_Network_IsRefusedBeforeConnecting() {
			DbSettingsResult dbSettingsResult=DbSettingsRunner.TestConnection(TestHelpers.NETWORK_ADDRESS,_dbName,"opmsd",TestHelpers.SECRET,"","");
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			StringAssert.Contains(dbSettingsResult.Message,"the office CA certificate file (SslCa)");
			Assert.IsFalse(dbSettingsResult.Message.Contains(TestHelpers.SECRET));
		}
	}
}
