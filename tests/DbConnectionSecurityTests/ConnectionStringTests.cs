// The connection strings BuildSimpleConnectionString makes for loopback and network servers, and how the rule reads them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class ConnectionStringTests {
		private const string THUMBPRINT="0123456789ABCDEF0123456789ABCDEF01234567";
		private string _folder;
		private string _thumbprintOld;
		private string _officeCaPathOld;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_thumbprintOld=DataConnection.ClientCertificateThumbprint;
			_officeCaPathOld=DataConnection.OfficeCaPath;
			DataConnection.ClientCertificateThumbprint="";
			DataConnection.OfficeCaPath="";
		}

		[TestCleanup]
		public void CleanUp() {
			DataConnection.ClientCertificateThumbprint=_thumbprintOld;
			DataConnection.OfficeCaPath=_officeCaPathOld;
			TestHelpers.DeleteFolder(_folder);
		}

		private static MySqlConnectionStringBuilder Build(string server,string password,string sslCa,string thumbprint=null) {
			return new MySqlConnectionStringBuilder(DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,server,"opendental","opmsd",password,
				sslCa,thumbprint));
		}

		[TestMethod]
		public void Loopback_ConnectsAsBefore() {
			MySqlConnectionStringBuilder builder=Build("localhost","","");
			Assert.AreEqual(MySqlSslMode.None,builder.SslMode);
			Assert.AreEqual("localhost",builder.Server);
			Assert.AreEqual(3306u,builder.Port);
			Assert.AreEqual(MySqlCertificateStoreLocation.None,builder.CertificateStoreLocation);
			Assert.AreEqual("",builder.CertificateThumbprint??"");
			MySqlConnectionStringBuilder builderCa=Build("127.0.0.1:13306","",@"C:\certs\office-ca.pem");
			Assert.AreEqual(MySqlSslMode.VerifyCA,builderCa.SslMode,"A CA on this computer: verified chain, as before.");
			Assert.AreEqual(13306u,builderCa.Port);
			StringAssert.Contains(DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"localhost","opendental","root",""),
				"Server=localhost;Port=3306;Database=opendental;User ID=root;Password=;CharSet=utf8","The format other tools read is unchanged.");
		}

		[TestMethod]
		public void Network_VerifyFull_TheOfficeCa_AndTheClientCertificateFromLocalMachine() {
			string ca=TestHelpers.WriteCaPem(_folder);
			MySqlConnectionStringBuilder builder=Build(TestHelpers.NETWORK_HOST+":3307","",ca,THUMBPRINT);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode);
			Assert.AreEqual(ca,builder.SslCa);
			Assert.AreEqual(MySqlCertificateStoreLocation.LocalMachine,builder.CertificateStoreLocation);
			Assert.AreEqual(THUMBPRINT,builder.CertificateThumbprint);
			Assert.AreEqual(3307u,builder.Port);
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(builder.ConnectionString).Count);
		}

		[TestMethod]
		public void Network_ThePasswordAlone_IsTheFallback() {
			string ca=TestHelpers.WriteCaPem(_folder);
			MySqlConnectionStringBuilder builder=Build(TestHelpers.NETWORK_ADDRESS,TestHelpers.SECRET,ca);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode);
			Assert.AreEqual(MySqlCertificateStoreLocation.None,builder.CertificateStoreLocation);
			Assert.AreEqual(TestHelpers.SECRET,builder.Password,"A password with ; and = survives the string.");
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(builder.ConnectionString).Count);
		}

		[TestMethod]
		public void Network_WithoutTheCaOrACredential_IsRefusedByTheRule() {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,"opendental","opmsd","","");
			Assert.AreEqual(MySqlSslMode.VerifyFull,new MySqlConnectionStringBuilder(connectionString).SslMode,"Never SslMode=none over the network.");
			List<string> listMissing=DbConnectionSecurity.GetMissing(connectionString);
			Assert.AreEqual(2,listMissing.Count,string.Join(" | ",listMissing));
			StringAssert.Contains(listMissing[0],"SslCa");
			StringAssert.Contains(listMissing[1],"CertificateThumbprint");
		}

		[TestMethod]
		public void Network_TheComputersCertificate_IsUsedWhenNoneIsGiven_AndAnEmptyOneTurnsItOff() {
			string ca=TestHelpers.WriteCaPem(_folder);
			DataConnection.ClientCertificateThumbprint=THUMBPRINT;
			Assert.AreEqual(THUMBPRINT,Build(TestHelpers.NETWORK_HOST,"",ca).CertificateThumbprint,"Every connection of the program's own account.");
			Assert.AreEqual("",Build(TestHelpers.NETWORK_HOST,"x",ca,"").CertificateThumbprint??"");
			Assert.AreEqual("",Build("localhost","","").CertificateThumbprint??"","Never sent to this computer's server.");
		}

		[TestMethod]
		public void Ipv6Servers_AreParsed() {
			MySqlConnectionStringBuilder builder=Build("[::1]:3307","","");
			Assert.AreEqual("::1",builder.Server);
			Assert.AreEqual(3307u,builder.Port);
			Assert.AreEqual(MySqlSslMode.None,builder.SslMode);
			MySqlConnectionStringBuilder builderBare=Build("::1","","");
			Assert.AreEqual("::1",builderBare.Server);
			Assert.AreEqual(3306u,builderBare.Port);
			MySqlConnectionStringBuilder builderNetwork=Build("[2001:db8::1]:3307","","");
			Assert.AreEqual("2001:db8::1",builderNetwork.Server);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builderNetwork.SslMode);
		}

		[TestMethod]
		public void PortsAndBrackets_BecomeWhatMySqlConnectorTakes_AndTheDecisionFollowsTheWrittenServer() {
			//The program's notation for this computer: written without ports or brackets, so MySqlConnector also takes it for this computer.
			foreach(string[] arrayCase in new[] {
				new[] { "localhost:3306,127.0.0.1:3306","localhost,127.0.0.1","3306" },
				new[] { "[127.0.0.1]","127.0.0.1","3306" },
				new[] { "[127.0.0.1],[::1]:3307","127.0.0.1,::1","3307" },
				new[] { "localhost, 127.0.0.1:13306","localhost,127.0.0.1","13306" },
			})
			{
				MySqlConnectionStringBuilder builder=Build(arrayCase[0],"","");
				Assert.AreEqual(arrayCase[1],builder.Server,arrayCase[0]);
				Assert.AreEqual(uint.Parse(arrayCase[2]),builder.Port,arrayCase[0]);
				Assert.AreEqual(MySqlSslMode.None,builder.SslMode,arrayCase[0]);
				Assert.IsTrue(DbConnectionSecurity.IsLoopbackConnectorServer(builder.Server),arrayCase[0]);
				Assert.AreEqual(0,DbConnectionSecurity.GetMissing(builder.ConnectionString).Count,arrayCase[0]);
			}
			MySqlConnectionStringBuilder builderNetwork=Build("localhost:3306,"+TestHelpers.NETWORK_HOST+":3306","x",TestHelpers.WriteCaPem(_folder));
			Assert.AreEqual("localhost,"+TestHelpers.NETWORK_HOST,builderNetwork.Server);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builderNetwork.SslMode,"One host over the network: encrypted.");
			DbConnectionRefusedException ex=TestHelpers.AssertThrows<DbConnectionRefusedException>(() =>
				Build("a.example.test:3307,b.example.test:3308","",""));
			StringAssert.Contains(ex.Message,"different ports");
		}

		[TestMethod]
		public void Network_ThisComputersOfficeCa_WinsOverTheCaTheCallerNames() {
			//The program's own account (BuildSimpleConnectionString). The read-only and reporting servers of their own keep their preference's CA:
			//PreferenceServerTests.
			string ca=TestHelpers.WriteCaPem(_folder);
			string caOther=TestHelpers.WriteCaPem(_folder,"other-ca.pem");
			//No office CA on this computer yet and none named: refused.
			string connectionStringNone=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,"opendental","opmsd","x","");
			CollectionAssert.AreEqual(new[] { "the office CA certificate file (SslCa)" },DbConnectionSecurity.GetMissing(connectionStringNone));
			DataConnection.OfficeCaPath=ca;
			Assert.AreEqual(ca,Build(TestHelpers.NETWORK_HOST,"x",caOther).SslCa,"The caller's CA is never used over the network.");
			Assert.AreEqual(ca,Build(TestHelpers.NETWORK_HOST,"x","").SslCa,"Also when the caller names none.");
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(Build(TestHelpers.NETWORK_HOST,"x",caOther).ConnectionString).Count);
			//A server on this computer keeps the CA of its own settings, as before.
			MySqlConnectionStringBuilder builderLocal=Build("localhost","",caOther);
			Assert.AreEqual(caOther,builderLocal.SslCa);
			Assert.AreEqual(MySqlSslMode.VerifyCA,builderLocal.SslMode);
		}

		[TestMethod]
		public void Values_WithSemicolonsQuotesOrSpaces_AreQuoted() {
			string ca=TestHelpers.WriteCaPem(_folder,"office ca;copy.pem");
			MySqlConnectionStringBuilder builder=Build(TestHelpers.NETWORK_HOST," p\"w;x ",ca);
			Assert.AreEqual(" p\"w;x ",builder.Password);
			Assert.AreEqual(ca,builder.SslCa);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode,"A ; in a value never ends it early.");
		}

		[TestMethod]
		public void AServerNameWithAnInjectedOption_StillEndsUpVerified() {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST+";SslMode=None","opendental",
				"opmsd","x","");
			Assert.AreEqual(MySqlSslMode.VerifyFull,new MySqlConnectionStringBuilder(connectionString).SslMode);
			string connectionStringLocal=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"localhost;Server="+TestHelpers.NETWORK_HOST,
				"opendental","opmsd","x","");
			Assert.AreEqual(MySqlSslMode.VerifyFull,new MySqlConnectionStringBuilder(connectionStringLocal).SslMode,"Not taken for this computer.");
			Assert.AreNotEqual(0,DbConnectionSecurity.GetMissing(connectionStringLocal).Count);
		}
	}
}
