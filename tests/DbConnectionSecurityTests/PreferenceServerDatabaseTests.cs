// The read-only and reporting servers of their own through ConnectionStore and the database preferences, on the local MariaDB at
// 127.0.0.1:3306 only (the rule exempts it). Scratch databases test_dbsec_*, dropped afterwards; synthetic preference rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class PreferenceServerDatabaseTests {
		private const string SERVER="127.0.0.1";
		private static string _dbMain;
		private static string _dbReplica;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			string id=Guid.NewGuid().ToString("N").Substring(0,12);
			_dbMain="test_dbsec_"+id+"_main";
			_dbReplica="test_dbsec_"+id+"_ro";
			Match match=Regex.Match(SchemaVerifier.ReadReferenceDdl(),@"CREATE TABLE `preference` \(.*?\) ENGINE=[^;]*;",RegexOptions.Singleline);
			Assert.IsTrue(match.Success,"The reference schema has the preference table.");
			Exec("CREATE DATABASE `"+_dbMain+"` CHARACTER SET utf8mb3",
				"CREATE DATABASE `"+_dbReplica+"` CHARACTER SET utf8mb3",
				"USE `"+_dbMain+"`",match.Value,
				"USE `"+_dbReplica+"`",match.Value);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			ConnectionStoreBase.ClearConnectionDictionary();
			Exec("DROP DATABASE IF EXISTS `"+_dbMain+"`","DROP DATABASE IF EXISTS `"+_dbReplica+"`");
		}

		///<summary>Runs the commands in one connection to the local server as root (blank password), the scratch databases' only user here.</summary>
		private static void Exec(params string[] arrayCommands) {
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,"","root","")))
			{
				mySqlConnection.Open();
				foreach(string command in arrayCommands) {
					using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
						mySqlCommand.ExecuteNonQuery();
					}
				}
			}
		}

		///<summary>The read-only server preferences (server, database, user, password, CA) and empty reporting server preferences, in
		///_dbMain.</summary>
		private static void SeedPreferences(string readOnlyServer,string readOnlyDatabase,string readOnlyUser,string readOnlyPassword,
			string readOnlySslCa="")
		{
			Assert.IsTrue(Secrets.Encrypt(readOnlyPassword,out string passHash));
			Exec("USE `"+_dbMain+"`","SET SESSION sql_mode=''","DELETE FROM preference",
				"INSERT INTO preference (PrefName,ValueString) VALUES ('ReadOnlyServerCompName','"+readOnlyServer+"'),('ReadOnlyServerURI',''),"
					+"('ReadOnlyServerDbName','"+readOnlyDatabase+"'),('ReadOnlyServerMySqlUser','"+readOnlyUser+"'),"
					+"('ReadOnlyServerMySqlPassHash','"+passHash+"'),('ReadOnlyServerSslCa','"+readOnlySslCa.Replace("\\","\\\\")+"'),"
					+"('ReportingServerCompName',''),('ReportingServerURI',''),('ReportingServerDbName',''),('ReportingServerMySqlUser',''),"
					+"('ReportingServerMySqlPassHash',''),('ReportingServerSslCa','')",
				"USE `"+_dbReplica+"`","DELETE FROM preference","INSERT INTO preference (PrefName,ValueString) VALUES ('DbSecProbe','replica')");
			Prefs.RefreshCache();
			ConnectionStoreBase.ClearConnectionDictionary();
		}

		[TestMethod]
		public void ConnectionStore_APreferenceWithAServer_IsItsOwnConnection_AnEmptyOneIsTheMainConnection() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			new DataConnection().SetDb(SERVER,_dbMain,"root","","","",DatabaseType.MySql,true);
			System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ConnectionStore).TypeHandle);
			//A read-only server of its own: its own server, database, user and password (never the main connection's, which is empty here).
			const string CA_PREFERENCE=@"C:\Certificates\replica-ca.pem";
			SeedPreferences(SERVER,_dbReplica,"replica_user",TestHelpers.SECRET,CA_PREFERENCE);
			CentralConnectionBase centralConnectionReadOnly=ConnectionStoreBase.GetDentalOfficeReadOnlyServerFromPrefC();
			Assert.IsTrue(centralConnectionReadOnly.IsPreferenceServer);
			Assert.AreEqual(SERVER,centralConnectionReadOnly.ServerName);
			Assert.AreEqual(_dbReplica,centralConnectionReadOnly.DatabaseName);
			Assert.AreEqual("replica_user",centralConnectionReadOnly.MySqlUser);
			Assert.AreEqual(TestHelpers.SECRET,centralConnectionReadOnly.MySqlPassword);
			Assert.AreEqual(CA_PREFERENCE,centralConnectionReadOnly.SslCa,"Its own CA preference.");
			SeedPreferences(SERVER,_dbReplica,"replica_user","");
			Assert.AreEqual("",ConnectionStoreBase.GetDentalOfficeReadOnlyServerFromPrefC().SslCa,"No CA preference: none (SslMode=none).");
			//No reporting server in the preferences: the main connection, not marked, under the rule.
			CentralConnectionBase centralConnectionReport=ConnectionStoreBase.GetDentalOfficeReportServerFromPrefC();
			Assert.IsFalse(centralConnectionReport.IsPreferenceServer);
			Assert.AreEqual(SERVER,centralConnectionReport.ServerName);
			Assert.AreEqual(_dbMain,centralConnectionReport.DatabaseName);
			Assert.AreEqual("root",centralConnectionReport.MySqlUser);
			Assert.AreEqual("",centralConnectionReport.MySqlPassword);
		}

		[TestMethod]
		public void ReadOnlyServerOfItsOwn_ReadsThere_TheOldWay_AndTheMainConnectionStaysAsItIs() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			new DataConnection().SetDb(SERVER,_dbMain,"root","","","",DatabaseType.MySql,true);
			SeedPreferences(SERVER,_dbReplica,"root","");
			string connectionString=null;
			string databaseUnpooled=null;
			string probe=DataAction.GetString(() => {
				connectionString=DataConnection.GetCurrentConnectionString();
				//DataConnectionCancelable's own connection for a user query that can be cancelled.
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnectionWithoutPooling(connectionString)) {
					mySqlConnection.Open();
					using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT DATABASE()",mySqlConnection)) {
						databaseUnpooled=Convert.ToString(mySqlCommand.ExecuteScalar());
					}
				}
				return DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DbSecProbe'");
			},ConnectionNames.DentalOfficeReadOnlyServer);
			Assert.AreEqual("replica",probe,"The read went to the read-only server's database.");
			Assert.AreEqual(_dbReplica,databaseUnpooled);
			Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionString));
			Assert.AreEqual(MySqlSslMode.None,new MySqlConnectionStringBuilder(connectionString).SslMode);
			Assert.AreEqual(_dbMain,DataConnection.GetDatabaseName(),"The main connection is unchanged.");
			Assert.IsFalse(DbConnectionSecurity.IsPreferenceServerConnection(DataConnection.GetConnectionString()));
		}
	}
}
