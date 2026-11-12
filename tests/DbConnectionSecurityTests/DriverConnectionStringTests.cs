// What MySqlConnector gets for each kind of program connection string since DB-SEC 3 was decided as (c) (docs/specs/db-connection-security.md
// section 8, docs/port-notes/S2-2.md section 3.6): a server over the network with a CA gets SslMode=Required without SslCa and the program's
// own check as the connection's certificate callback; everything else goes to the driver as before. Also each place that makes such a
// connection: DbConnectionSecurity.NewMySqlConnection and NewMySqlConnectionWithoutPooling, DataConnection (the main connection, the backup to
// another server, the read-only and reporting servers), and the RDL engine's hook. No connection is opened here (UnverifiedServerTests opens
// them against fake servers).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class DriverConnectionStringTests {
		private string _folder;
		private string _pathOfficeCa;
		private string _officeCaPathOld;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_pathOfficeCa=TestHelpers.WriteCaPem(_folder);
			_officeCaPathOld=DataConnection.OfficeCaPath;
			DataConnection.OfficeCaPath=_pathOfficeCa;
		}

		[TestCleanup]
		public void CleanUp() {
			DataConnection.OfficeCaPath=_officeCaPathOld;
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>A database name of this test alone, so its driver strings never meet another test's pool record.</summary>
		private static string NewDatabaseName() {
			return "s22v_driver_"+Guid.NewGuid().ToString("N").Substring(0,12);
		}

		[TestMethod]
		public void ServerOverTheNetwork_DriverGetsRequiredWithoutTheCa_AndTheProgramsCheck() {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,NewDatabaseName(),"opmsd",
				TestHelpers.SECRET,certificateThumbprint:"0123456789ABCDEF0123456789ABCDEF01234567");
			MySqlConnectionStringBuilder builderProgram=new MySqlConnectionStringBuilder(connectionString);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builderProgram.SslMode,"The rule's string is unchanged.");
			Assert.AreEqual(_pathOfficeCa,builderProgram.SslCa);
			DbConnectionSecurity.CheckConnectionString(connectionString);
			OfficeCaServerVerifier verifier;
			string driverString=DbConnectionSecurity.GetDriverConnectionString(connectionString,out verifier);
			MySqlConnectionStringBuilder builderDriver=new MySqlConnectionStringBuilder(driverString);
			Assert.AreEqual(MySqlSslMode.Required,builderDriver.SslMode,"The driver's own check (VerifyFull with SslCa) logs in before it decides.");
			Assert.AreEqual("",builderDriver.SslCa,"With SslCa the driver ignores the connection's callback.");
			Assert.IsFalse(driverString.IndexOf("SslCa",StringComparison.OrdinalIgnoreCase)>=0,driverString);
			Assert.IsFalse(builderDriver.SkipCertificateRevocationCheck);
			//The client certificate, the password and the rest go to the driver as they were.
			Assert.AreEqual(MySqlCertificateStoreLocation.LocalMachine,builderDriver.CertificateStoreLocation);
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",builderDriver.CertificateThumbprint);
			Assert.AreEqual(TestHelpers.SECRET,builderDriver.Password);
			Assert.AreEqual(builderProgram.Server,builderDriver.Server);
			Assert.AreEqual(builderProgram.Database,builderDriver.Database);
			Assert.AreEqual(builderProgram.Port,builderDriver.Port);
			Assert.IsTrue(builderDriver.Pooling);
			Assert.IsNotNull(verifier);
			Assert.AreEqual(_pathOfficeCa,verifier.CaPath);
			Assert.IsTrue(verifier.IsHostNameChecked);
			CollectionAssert.AreEqual(new[] { TestHelpers.NETWORK_HOST },verifier.ListHosts);
			Assert.AreEqual(System.Security.Cryptography.X509Certificates.X509RevocationMode.Online,verifier.RevocationMode);
		}

		[TestMethod]
		public void LoopbackOrWithoutACa_GoesToTheDriverAsBefore() {
			string database=NewDatabaseName();
			foreach(string connectionString in new[] {
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"localhost",database,"root",""),//SslMode=none
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"127.0.0.1",database,"root","",sslCa:_pathOfficeCa),//VerifyCA, loopback
				"Server=::1;Database="+database+";SslMode=VerifyFull;SslCa="+_pathOfficeCa,
				"Server="+TestHelpers.NETWORK_HOST+";Database="+database+";SslMode=None",//a read-only server without a CA preference
				"Server="+TestHelpers.NETWORK_HOST+";Database="+database+";SslMode=Required",
				"Server="+TestHelpers.NETWORK_HOST+";Database="+database+";SslMode=Preferred;SslCa="+_pathOfficeCa,
			})
			{
				OfficeCaServerVerifier verifier;
				Assert.AreEqual(connectionString,DbConnectionSecurity.GetDriverConnectionString(connectionString,out verifier),connectionString);
				Assert.IsNull(verifier,connectionString);
			}
		}

		[TestMethod]
		public void PreferenceServerWithACa_VerifyCA_IsCheckedByTheProgram_WithoutTheHostName() {
			string connectionString=DataConnection.BuildPreferenceServerConnectionString("reports.example.test",NewDatabaseName(),"ro",TestHelpers.SECRET,
				_pathOfficeCa);
			Assert.AreEqual(MySqlSslMode.VerifyCA,new MySqlConnectionStringBuilder(connectionString).SslMode);
			OfficeCaServerVerifier verifier;
			MySqlConnectionStringBuilder builderDriver=new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionString,
				out verifier));
			Assert.AreEqual(MySqlSslMode.Required,builderDriver.SslMode);
			Assert.AreEqual("",builderDriver.SslCa);
			Assert.IsNotNull(verifier);
			Assert.IsFalse(verifier.IsHostNameChecked,"VerifyCA checks the chain, not the host name (S1).");
			Assert.AreEqual(System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,verifier.RevocationMode);
			//The rule lets the preference server's own string through, and the connection gets the check.
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionString)) {
				Assert.IsNotNull(mySqlConnection.RemoteCertificateValidationCallback);
				Assert.AreEqual(connectionString,DbConnectionSecurity.GetConnectionString(mySqlConnection));
			}
		}

		[TestMethod]
		public void SkipCertificateRevocationCheck_NeverReachesTheDriver() {
			//The driver allows it only with VerifyFull and would refuse the Required string; the program's check keeps revocation on.
			string connectionString="Server="+TestHelpers.NETWORK_HOST+";Database="+NewDatabaseName()+";User ID=opmsd;Password=x;SslMode=VerifyFull;SslCa="
				+_pathOfficeCa+";SkipCertificateRevocationCheck=true";
			OfficeCaServerVerifier verifier;
			MySqlConnectionStringBuilder builderDriver=new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionString,
				out verifier));
			Assert.IsFalse(builderDriver.SkipCertificateRevocationCheck);
			Assert.AreEqual(System.Security.Cryptography.X509Certificates.X509RevocationMode.Online,verifier.RevocationMode);
		}

		[TestMethod]
		public void TwoChecksForTheSameDriverString_NeverShareAPool() {
			//The main string (VerifyFull) and a read-only server's string (VerifyCA) for the same server, database and account differ only in what
			//the check is; the driver strings would be the same, and a pooled session was verified by whichever check made it.
			string database=NewDatabaseName();
			string connectionStringFull=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,database,"opmsd",
				TestHelpers.SECRET,certificateThumbprint:"");
			string connectionStringCa=DataConnection.BuildPreferenceServerConnectionString(TestHelpers.NETWORK_HOST,database,"opmsd",TestHelpers.SECRET,
				_pathOfficeCa);
			OfficeCaServerVerifier verifierFull;
			OfficeCaServerVerifier verifierCa;
			MySqlConnectionStringBuilder builderFull=new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionStringFull,
				out verifierFull));
			MySqlConnectionStringBuilder builderCa=new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionStringCa,
				out verifierCa));
			Assert.IsTrue(verifierFull.IsHostNameChecked);
			Assert.IsFalse(verifierCa.IsHostNameChecked);
			Assert.IsTrue(builderFull.Pooling,"The first check keeps the pool.");
			Assert.IsFalse(builderCa.Pooling,"The second check of the same driver string connects outside that pool.");
			builderCa.Remove("Pooling");
			Assert.AreEqual(builderFull.ConnectionString,builderCa.ConnectionString,"Pooling is the only difference.");
			//Asked again, each keeps its own answer.
			OfficeCaServerVerifier verifierAgain;
			Assert.IsTrue(new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionStringFull,out verifierAgain)).Pooling);
			Assert.IsFalse(new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionStringCa,out verifierAgain)).Pooling);
		}

		[TestMethod]
		public void NewMySqlConnection_AndWithoutPooling_GetTheCheck_AndKeepTheProgramsString() {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,NewDatabaseName(),"opmsd",
				TestHelpers.SECRET,certificateThumbprint:"");
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionString)) {
				Assert.IsNotNull(mySqlConnection.RemoteCertificateValidationCallback,"The program's check is the connection's callback.");
				Assert.IsInstanceOfType(mySqlConnection.RemoteCertificateValidationCallback.Target,typeof(OfficeCaServerVerifier));
				Assert.AreEqual(MySqlSslMode.Required,new MySqlConnectionStringBuilder(mySqlConnection.ConnectionString).SslMode);
				Assert.AreEqual(connectionString,DbConnectionSecurity.GetConnectionString(mySqlConnection),"What seeds other connections is the "
					+"program's string, which the rule checks again.");
				//MySqlConnection.Clone keeps the callback (the driver's private constructor copies it).
				using(MySqlConnection mySqlConnectionClone=mySqlConnection.Clone()) {
					Assert.IsNotNull(mySqlConnectionClone.RemoteCertificateValidationCallback);
				}
			}
			//DataConnectionCancelable's connection of its own.
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnectionWithoutPooling(connectionString)) {
				Assert.IsNotNull(mySqlConnection.RemoteCertificateValidationCallback);
				MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(mySqlConnection.ConnectionString);
				Assert.IsFalse(builder.Pooling);
				Assert.AreEqual(MySqlSslMode.Required,builder.SslMode);
				Assert.AreEqual("",builder.SslCa);
			}
			//The driver's string is refused by the rule: a connection made from it again, without the program's check, never reaches the network.
			OfficeCaServerVerifier verifier;
			string driverString=DbConnectionSecurity.GetDriverConnectionString(connectionString,out verifier);
			RuleTests.AssertRefused(() => DbConnectionSecurity.CheckConnectionString(driverString));
			//A loopback connection has no callback, as before.
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection("Server=127.0.0.1;Port=3306;SslMode=None")) {
				Assert.IsNull(mySqlConnection.RemoteCertificateValidationCallback);
			}
		}

		[TestMethod]
		public void DataConnection_TheMainConnectionAndTheBackupToAnotherServer_GetTheCheck() {
			//DataConnection(server,database,user,password,...) is how the main connection, ServiceManager, MySQLManager, the Middle Tier server
			//and MiscData.MakeABackup(serverName,user,pass) (the backup to another server) connect: BuildSimpleConnectionString, then
			//NewMySqlConnection. GetReportConnectionString reads that connection's string, which must stay the program's.
			string database=NewDatabaseName();
			string connectionString=DataConnection.GetReportConnectionString(TestHelpers.NETWORK_HOST,database,"opmsd",TestHelpers.SECRET);
			Assert.AreEqual(DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,database,"opmsd",TestHelpers.SECRET),
				connectionString);
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode);
			Assert.AreEqual(_pathOfficeCa,builder.SslCa);
			OfficeCaServerVerifier verifier;
			Assert.AreEqual(MySqlSslMode.Required,new MySqlConnectionStringBuilder(DbConnectionSecurity.GetDriverConnectionString(connectionString,
				out verifier)).SslMode);
			Assert.IsTrue(verifier.IsHostNameChecked);
		}

		[TestMethod]
		public void RdlEngine_MakesItsMySqlConnectorConnection_ThroughTheProgram() {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,NewDatabaseName(),"opmsd",
				TestHelpers.SECRET,certificateThumbprint:"");
			Action<string,string,string> actionCheckOld=fyiReporting.RDL.RdlEngineConfig.CheckConnection;
			Func<string,string,string,IDbConnection> funcCreateOld=fyiReporting.RDL.RdlEngineConfig.CreateConnection;
			try {
				fyiReporting.RDL.RdlEngineConfig.CheckConnection=DbConnectionSecurity.CheckReportConnection;
				fyiReporting.RDL.RdlEngineConfig.CreateConnection=DbConnectionSecurity.NewReportConnection;
				IDbConnection dbConnection=fyiReporting.RDL.RdlEngineConfig.GetConnection("MySQL.NET",connectionString);
				MySqlConnection mySqlConnection=dbConnection as MySqlConnection;
				Assert.IsNotNull(mySqlConnection,"The report's connection is MySqlConnector's.");
				Assert.IsNotNull(mySqlConnection.RemoteCertificateValidationCallback,"Made by the program, with its check.");
				Assert.AreEqual(MySqlSslMode.Required,new MySqlConnectionStringBuilder(mySqlConnection.ConnectionString).SslMode);
				Assert.AreEqual(connectionString,DbConnectionSecurity.GetConnectionString(mySqlConnection));
				mySqlConnection.Dispose();
				//The rule still decides first: a string it refuses never reaches the hook.
				RuleTests.AssertRefused(() => fyiReporting.RDL.RdlEngineConfig.GetConnection("MySQL.NET","Server="+TestHelpers.NETWORK_HOST+";SslMode=None"));
			}
			finally {
				fyiReporting.RDL.RdlEngineConfig.CheckConnection=actionCheckOld;
				fyiReporting.RDL.RdlEngineConfig.CreateConnection=funcCreateOld;
			}
			Assert.IsNull(DbConnectionSecurity.NewReportConnection("sql","System.Data.SqlClient.SqlConnection","Server=localhost"));
		}

		[TestMethod]
		public void ReportWindows_SetTheConnectionFactory() {
			//The report windows cannot be opened here; their constructors set both hooks.
			foreach(string fileName in new[] { "FormReportForRdl.cs","FormReportCustom.cs" }) {
				string text=TestHelpers.ReadRepoFile("OpenPmsDental","ReportingForRdl",fileName);
				StringAssert.Contains(text,"RdlEngineConfig.CheckConnection=DataConnectionBase.DbConnectionSecurity.CheckReportConnection;",fileName);
				StringAssert.Contains(text,"RdlEngineConfig.CreateConnection=DataConnectionBase.DbConnectionSecurity.NewReportConnection;",fileName);
			}
		}
	}
}
