// The connection rule: loopback hosts, network hosts, and what a connection over the network needs (DbConnectionSecurity).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class RuleTests {
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		#region Loopback
		[TestMethod]
		public void IsLoopbackHost_LocalhostInAnyCase_AndEveryLoopbackAddress() {
			foreach(string host in new[] { "localhost","LOCALHOST","LocalHost"," localhost ","127.0.0.1","127.0.0.2","127.1.2.3","127.255.255.254",
				"::1","[::1]","0:0:0:0:0:0:0:1","::ffff:127.0.0.1","[::ffff:127.0.0.1]" })
			{
				Assert.IsTrue(DbConnectionSecurity.IsLoopbackHost(host),host);
			}
		}

		[TestMethod]
		public void IsLoopbackHost_EveryOtherNameOrAddress_IsTheNetwork() {
			foreach(string host in new[] { "localhost.","localhost.example.test","127.0.0.1.example.test",TestHelpers.NETWORK_HOST,
				TestHelpers.NETWORK_ADDRESS,"0.0.0.0","::","fe80::1","::ffff:192.0.2.10","2001:db8::1","[2001:db8::1]","","  ",null,"[]","[localhost" })
			{
				Assert.IsFalse(DbConnectionSecurity.IsLoopbackHost(host),host??"null");
			}
		}

		[TestMethod]
		public void IsLoopbackHost_NeverAsksDns_ThisComputersOwnNameIsTheNetwork() {
			//The computer's own name resolves to this computer, but deciding by DNS would let a changed DNS answer turn encryption off.
			Assert.IsFalse(DbConnectionSecurity.IsLoopbackHost(Environment.MachineName));
			Assert.IsFalse(DbConnectionSecurity.IsLoopbackHost(System.Net.Dns.GetHostName()));
		}

		[TestMethod]
		public void IsLoopbackServer_HandlesPorts_Brackets_Lists_AndAnEmptyServer() {
			foreach(string server in new[] { "localhost:3306","127.0.0.1:13306","[::1]:3307","[::1]","::1","localhost,127.0.0.1","" }) {
				Assert.IsTrue(DbConnectionSecurity.IsLoopbackServer(server),server);
			}
			foreach(string server in new[] { "localhost,"+TestHelpers.NETWORK_HOST,TestHelpers.NETWORK_ADDRESS+":3306","[2001:db8::1]:3306",
				TestHelpers.NETWORK_HOST+":3306" })
			{
				Assert.IsFalse(DbConnectionSecurity.IsLoopbackServer(server),server);
			}
		}

		[TestMethod]
		public void SplitServer_ReadsHostAndPort() {
			AssertSplit("localhost","localhost","");
			AssertSplit("db.example.test:3307","db.example.test","3307");
			AssertSplit("[::1]:3307","::1","3307");
			AssertSplit("[2001:db8::1]","2001:db8::1","");
			AssertSplit("2001:db8::1","2001:db8::1","");//a bare IPv6 address keeps its colons
			AssertSplit("::1","::1","");
			AssertSplit(" 127.0.0.1:13306 ","127.0.0.1","13306");
			AssertSplit("a,b:3307","a,b:3307","");//a list is left whole
		}

		private static void AssertSplit(string server,string hostExpected,string portExpected) {
			DbConnectionSecurity.SplitServer(server,out string host,out string port);
			Assert.AreEqual(hostExpected,host,server);
			Assert.AreEqual(portExpected,port,server);
		}
		#endregion Loopback

		#region What the network needs
		[TestMethod]
		public void GetMissing_Loopback_NeedsNothing() {
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server=localhost;SslMode=None;User ID=root;Password=").Count);
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server=::1;SslMode=Disabled").Count);
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server=127.0.0.1;Protocol=Pipe").Count,"Other protocols on this computer.");
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing("").Count,"A connection object made before a database is chosen.");
		}

		[TestMethod]
		public void GetMissing_Network_VerifyFull_SslCa_AndACertificateOrAPassword() {
			string ca=TestHelpers.WriteCaPem(_folder);
			string server="Server="+TestHelpers.NETWORK_HOST+";";
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;SslCa="+ca+";Password=x").Count,"CA and password");
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;SslCa="+ca
				+";CertificateStoreLocation=LocalMachine;CertificateThumbprint=0123456789ABCDEF0123456789ABCDEF01234567").Count,"CA and certificate");
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;SslCa="+ca+";Password=x"
				+";CertificateStoreLocation=LocalMachine;CertificateThumbprint=0123456789ABCDEF0123456789ABCDEF01234567").Count,"both");
			foreach(string sslMode in new[] { "None","Preferred","Required","VerifyCA" }) {
				List<string> listMissing=DbConnectionSecurity.GetMissing(server+"SslMode="+sslMode+";SslCa="+ca+";Password=x");
				Assert.AreEqual(1,listMissing.Count,sslMode);
				StringAssert.Contains(listMissing[0],"SslMode=VerifyFull",sslMode);
			}
			List<string> listNoCa=DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;Password=x");
			CollectionAssert.AreEqual(new[] { "the office CA certificate file (SslCa)" },listNoCa);
			string caMissing=Path.Combine(_folder,"missing-ca.pem");
			List<string> listCaMissing=DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;SslCa="+caMissing+";Password=x");
			CollectionAssert.AreEqual(new[] { "the office CA certificate file "+caMissing+" (the file does not exist)" },listCaMissing);
			List<string> listNoCredential=DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;SslCa="+ca);
			CollectionAssert.AreEqual(new[] { "a client certificate (CertificateThumbprint) or a database password stored on this computer" },
				listNoCredential);
			List<string> listCurrentUser=DbConnectionSecurity.GetMissing(server+"SslMode=VerifyFull;SslCa="+ca
				+";CertificateStoreLocation=CurrentUser;CertificateThumbprint=0123456789ABCDEF0123456789ABCDEF01234567");
			CollectionAssert.AreEqual(new[] { "the client certificate in the computer's certificate store (CertificateStoreLocation=LocalMachine)" },
				listCurrentUser);
		}

		[TestMethod]
		public void GetMissing_Network_ListsEverythingMissing_InOrder() {
			List<string> listMissing=DbConnectionSecurity.GetMissing("Server="+TestHelpers.NETWORK_ADDRESS+";SslMode=None");
			Assert.AreEqual(3,listMissing.Count,string.Join(" | ",listMissing));
			StringAssert.StartsWith(listMissing[0],"server verification (SslMode=VerifyFull, now None)");
			Assert.AreEqual("the office CA certificate file (SslCa)",listMissing[1]);
			StringAssert.StartsWith(listMissing[2],"a client certificate");
		}

		[TestMethod]
		public void GetMissing_Network_RefusesServerRedirection_OtherProtocols_AndListsWithANetworkHost() {
			string ca=TestHelpers.WriteCaPem(_folder);
			string good=";SslMode=VerifyFull;SslCa="+ca+";Password=x";
			StringAssert.Contains(DbConnectionSecurity.GetMissing("Server="+TestHelpers.NETWORK_HOST+good+";ServerRedirectionMode=Preferred").Single(),
				"ServerRedirectionMode=Disabled");
			StringAssert.Contains(DbConnectionSecurity.GetMissing("Server="+TestHelpers.NETWORK_HOST+good+";Protocol=Pipe").Single(),"TCP");
			Assert.AreEqual(3,DbConnectionSecurity.GetMissing("Server=localhost,"+TestHelpers.NETWORK_HOST+";SslMode=None").Count,
				"One host over the network is enough to need everything.");
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server=localhost,"+TestHelpers.NETWORK_HOST+good).Count);
		}

		[TestMethod]
		public void GetMissing_TheLastValueOfARepeatedKeyCounts_AsMySqlConnectorReadsIt() {
			string ca=TestHelpers.WriteCaPem(_folder);
			//A server name with an injected option cannot switch encryption off: the check reads the string as the connector does.
			Assert.AreEqual(1,DbConnectionSecurity.GetMissing("Server=localhost;Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+ca).Count);
			Assert.AreEqual(1,DbConnectionSecurity.GetMissing("Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+ca+";Password=x"
				+";SslMode=None").Count);
		}
		#endregion What the network needs

		#region Refusal
		[TestMethod]
		public void CheckConnectionString_Refuses_WithAMessageThatNamesTheHost_TheMissingItems_AndTheTool_ButNoSecret() {
			DbConnectionRefusedException ex=AssertRefused(() => DbConnectionSecurity.CheckConnectionString("Server="+TestHelpers.NETWORK_HOST
				+";Port=3306;User ID=opmsd;Password=\""+TestHelpers.SECRET.Replace("\"","\"\"")+"\";SslMode=None"));
			Assert.IsInstanceOfType(ex,typeof(ApplicationException),"Shown to the user as it is.");
			StringAssert.Contains(ex.Message,"database server "+TestHelpers.NETWORK_HOST+":");
			StringAssert.Contains(ex.Message,"must be encrypted and the server verified");
			StringAssert.Contains(ex.Message,"server verification (SslMode=VerifyFull, now None); the office CA certificate file (SslCa)");
			StringAssert.Contains(ex.Message,"Run "+ProgramNames.DbSettingsExeFileName+" in the program folder as an administrator");
			Assert.IsFalse(ex.Message.Contains(TestHelpers.SECRET),"No password in the message.");
			Assert.AreEqual(TestHelpers.NETWORK_HOST,ex.Server);
			Assert.AreEqual(2,ex.ListMissing.Count);
		}

		[TestMethod]
		public void NewMySqlConnection_RefusesBeforeAnyConnection_AndPassesLoopback() {
			//A connection attempt to the documentation range would time out or fail with a MySqlException; the refusal comes first.
			DateTime dateTimeStart=DateTime.Now;
			AssertRefused(() => DbConnectionSecurity.NewMySqlConnection("Server="+TestHelpers.NETWORK_ADDRESS+";SslMode=Required;Password=x"));
			Assert.IsTrue((DateTime.Now-dateTimeStart).TotalSeconds<5,"Refused at once, without trying the network.");
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection("Server=127.0.0.1;Port=3306;SslMode=None")) {
				Assert.AreEqual(System.Data.ConnectionState.Closed,mySqlConnection.State,"Created, not opened.");
			}
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection("")) {
				Assert.IsNotNull(mySqlConnection);
			}
		}

		[TestMethod]
		public void CheckConnectionString_ARefusedStringIsRefusedEveryTime() {
			string connectionString="Server="+TestHelpers.NETWORK_HOST+";SslMode=None;Password=x";
			AssertRefused(() => DbConnectionSecurity.CheckConnectionString(connectionString));
			AssertRefused(() => DbConnectionSecurity.CheckConnectionString(connectionString));
		}

		[TestMethod]
		public void CheckReportConnection_OnlyMySqlConnector_AndOnlyByTheRule() {
			DbConnectionSecurity.CheckReportConnection("MySQL.NET","MySqlConnector.MySqlConnection","Server=localhost;SslMode=None");
			AssertRefused(() => DbConnectionSecurity.CheckReportConnection("MySQL.NET","MySqlConnector.MySqlConnection",
				"Server="+TestHelpers.NETWORK_HOST+";SslMode=None"));
			foreach(string className in new[] { "System.Data.SqlClient.SqlConnection","System.Data.Odbc.OdbcConnection","System.Data.OleDb.OleDbConnection" }) {
				DbConnectionRefusedException ex=AssertRefused(() => DbConnectionSecurity.CheckReportConnection("sql",className,"Server=localhost"));
				StringAssert.Contains(ex.Message,"only through MySqlConnector");
			}
		}

		[TestMethod]
		public void RdlEngine_CallsTheHook_BeforeItCreatesAConnection() {
			List<string> listCalls=new List<string>();
			Action<string,string,string> actionOld=fyiReporting.RDL.RdlEngineConfig.CheckConnection;
			try {
				fyiReporting.RDL.RdlEngineConfig.CheckConnection=(provider,className,connectionString) => {
					listCalls.Add(className);
					DbConnectionSecurity.CheckReportConnection(provider,className,connectionString);
				};
				AssertRefused(() => fyiReporting.RDL.RdlEngineConfig.GetConnection("odbc","Driver=x"));
				AssertRefused(() => fyiReporting.RDL.RdlEngineConfig.GetConnection("MySQL.NET","Server="+TestHelpers.NETWORK_HOST+";SslMode=None"));
				CollectionAssert.AreEqual(new[] { "System.Data.Odbc.OdbcConnection","MySqlConnector.MySqlConnection" },listCalls);
			}
			finally {
				fyiReporting.RDL.RdlEngineConfig.CheckConnection=actionOld;
			}
		}

		internal static DbConnectionRefusedException AssertRefused(Action action) {
			try {
				action();
			}
			catch(DbConnectionRefusedException ex) {
				return ex;
			}
			Assert.Fail("Not refused.");
			return null;
		}
		#endregion Refusal
	}
}
