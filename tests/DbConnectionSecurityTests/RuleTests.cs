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

		[TestMethod]
		public void SplitServerList_EveryEntry_OnePort() {
			AssertSplitList("a.example.test:3307,b.example.test:3307","a.example.test,b.example.test","3307");
			AssertSplitList("a.example.test,b.example.test","a.example.test,b.example.test","");
			AssertSplitList("[::1]:3307,localhost","::1,localhost","3307");
			AssertSplitList("localhost:3306,127.0.0.1:3306","localhost,127.0.0.1","3306");
			AssertSplitList("[127.0.0.1],[::1]","127.0.0.1,::1","");
			AssertSplitList(" a.example.test , ,b.example.test:3307","a.example.test,b.example.test","3307");
			AssertSplitList("","","");
			Assert.IsFalse(DbConnectionSecurity.SplitServerList("a.example.test:3307,b.example.test:3308",out string hosts,out string port),
				"MySqlConnector has one Port for all hosts.");
		}

		private static void AssertSplitList(string server,string hostsExpected,string portExpected) {
			Assert.IsTrue(DbConnectionSecurity.SplitServerList(server,out string hosts,out string port),server);
			Assert.AreEqual(hostsExpected,hosts,server);
			Assert.AreEqual(portExpected,port,server);
		}

		[TestMethod]
		public void IsLoopbackConnectorHost_OnlyWhatMySqlConnectorReachesWithoutAskingTheResolver() {
			//MySqlConnector hands each entry unchanged to Dns.GetHostAddresses, which answers by itself only for what IPAddress.TryParse reads.
			foreach(string entry in new[] { "localhost","LOCALHOST","LocalHost","127.0.0.1","127.0.0.2","127.1","2130706433","::1","[::1]","[::1]:3306",
				"0:0:0:0:0:0:0:1","::ffff:127.0.0.1","[::ffff:127.0.0.1]" })
			{
				Assert.IsTrue(DbConnectionSecurity.IsLoopbackConnectorHost(entry),entry);
			}
			foreach(string entry in new[] { "localhost:3306","127.0.0.1:3306","[127.0.0.1]","[127.0.0.1]:3306"," localhost","localhost "," 127.0.0.1",
				"127.0.0.1 ","localhost.","[localhost]","","  ",null,TestHelpers.NETWORK_HOST,TestHelpers.NETWORK_ADDRESS,"[::ffff:192.0.2.10]","0.0.0.0",
				"::" })
			{
				Assert.IsFalse(DbConnectionSecurity.IsLoopbackConnectorHost(entry),"'"+(entry??"null")+"'");
			}
		}

		[TestMethod]
		public void IsLoopbackConnectorServer_SplitsAtCommasOnly_AsMySqlConnectorDoes() {
			foreach(string server in new[] { "","localhost,127.0.0.1","[::1],::1,127.0.0.5" }) {
				Assert.IsTrue(DbConnectionSecurity.IsLoopbackConnectorServer(server),server);
			}
			foreach(string server in new[] { "localhost:3306,127.0.0.1:3306","[127.0.0.1],[::1]","localhost, 127.0.0.1","localhost,","localhost,,::1",
				"localhost,"+TestHelpers.NETWORK_HOST })
			{
				Assert.IsFalse(DbConnectionSecurity.IsLoopbackConnectorServer(server),server);
			}
			//The program's notation reads several of them as this computer, so the two tests must never be swapped.
			Assert.IsTrue(DbConnectionSecurity.IsLoopbackServer("localhost:3306,127.0.0.1:3306"));
			Assert.IsTrue(DbConnectionSecurity.IsLoopbackServer("[127.0.0.1],[::1]"));
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
		public void CheckConnectionString_PortsOrBracketsInServer_AreTheNetwork_AndRefusedWithoutTls() {
			//Each of these goes to the system's name resolution in MySqlConnector, so a spoofed answer would get an unencrypted connection.
			foreach(string server in new[] { "localhost:3306","127.0.0.1:3306","[127.0.0.1]","[127.0.0.1],[::1]","localhost:3306,127.0.0.1:3306",
				"localhost, 127.0.0.1" })
			{
				string connectionString="Server="+server+";User ID=root;Password=;SslMode=None";
				Assert.AreNotEqual(0,DbConnectionSecurity.GetMissing(connectionString).Count,server);
				DbConnectionRefusedException ex=AssertRefused(() => DbConnectionSecurity.CheckConnectionString(connectionString));
				StringAssert.Contains(ex.Message,"server verification (SslMode=VerifyFull, now None)",server);
			}
			DbConnectionRefusedException exPort=AssertRefused(() => DbConnectionSecurity.CheckConnectionString("Server=localhost:3306;SslMode=None"));
			StringAssert.Contains(exPort.Message,"database server localhost:3306:","The entry is named as MySqlConnector takes it.");
			StringAssert.Contains(exPort.Message,"give the port with Port=");
			//What MySqlConnector does take for this computer.
			foreach(string server in new[] { "localhost","127.0.0.1","[::1]","::1","localhost,127.0.0.1" }) {
				DbConnectionSecurity.CheckConnectionString("Server="+server+";Port=3306;SslMode=None");
			}
		}

		[TestMethod]
		public void GetMissing_OnceThisComputerHasAnOfficeCa_NoOtherCaIsTrusted() {
			string ca=TestHelpers.WriteCaPem(_folder);
			string caOther=TestHelpers.WriteCaPem(_folder,"preference-ca.pem");
			string officeCaPathOld=DataConnection.OfficeCaPath;
			try {
				string connectionStringOther="Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+caOther+";Password=x";
				DataConnection.OfficeCaPath="";
				DbConnectionSecurity.CheckConnectionString(connectionStringOther);//No office CA yet: any CA file that exists.
				DataConnection.OfficeCaPath=ca;
				List<string> listMissing=DbConnectionSecurity.GetMissing(connectionStringOther);
				CollectionAssert.AreEqual(new[] { "this computer's office CA certificate file (SslCa="+ca+", now "+caOther+")" },listMissing);
				AssertRefused(() => DbConnectionSecurity.CheckConnectionString(connectionStringOther));//Not passed from memory: checked again.
				Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+ca+";Password=x").Count);
				Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+ca.ToUpperInvariant()
					+";Password=x").Count,"Windows compares paths without case.");
				Assert.AreEqual(0,DbConnectionSecurity.GetMissing("Server=localhost;SslMode=VerifyCA;SslCa="+caOther).Count,"This computer is exempt.");
			}
			finally {
				DataConnection.OfficeCaPath=officeCaPathOld;
			}
		}

		[TestMethod]
		public void GetConnectionStringProblem_ConnectionStringsAreForThisComputerOnly() {
			foreach(string connectionString in new[] { "","  ","Server=localhost;Database=opendental;User ID=root;Password=x",
				"Server=127.0.0.1;Port=3306;Pwd=x;SslMode=None","Server=[::1],127.0.0.1" })
			{
				Assert.AreEqual("",DbConnectionSecurity.GetConnectionStringProblem(connectionString),connectionString);
			}
			string ca=TestHelpers.WriteCaPem(_folder);
			foreach(string connectionString in new[] { "Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+ca+";Password="+TestHelpers.SECRET
				.Replace(";","").Replace("=",""),"Server="+TestHelpers.NETWORK_ADDRESS+";Pwd=x","Server=localhost:3306;Password=x",
				"Server=127.0.0.1,"+TestHelpers.NETWORK_HOST })
			{
				string problem=DbConnectionSecurity.GetConnectionStringProblem(connectionString);
				StringAssert.Contains(problem,"Connection strings are for a database on this computer only",connectionString);
				StringAssert.Contains(problem,ProgramNames.DbSettingsExeFileName);
				Assert.IsFalse(problem.Contains("Synthetic-Secret") || problem.Contains("Pwd=x"),"No part of the string's password in the message.");
			}
			string problemUnreadable=DbConnectionSecurity.GetConnectionStringProblem("Unknown Option=1;Password="+TestHelpers.SECRET.Replace(";",""));
			StringAssert.Contains(problemUnreadable,"cannot be read");
			Assert.IsFalse(problemUnreadable.Contains("Synthetic-Secret"));
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
				//The SQL Server, ODBC and OLE DB providers were removed (S0-2a): each is refused with a clear error before the hook, never a null
				//connection, whatever the case of its name.
				foreach(string provider in new[] { "sql","SQL","odbc","ODBC","oledb","OLEDB" }) {
					try {
						fyiReporting.RDL.RdlEngineConfig.GetConnection(provider,"Driver=x");
						Assert.Fail("Not refused: "+provider);
					}
					catch(NotSupportedException ex) {
						Assert.AreEqual(fyiReporting.RDL.RdlEngineConfig.GetProviderRemovedMessage(provider),ex.Message);
						StringAssert.Contains(ex.Message,"'"+provider+"' is not available in this version");
					}
				}
				AssertRefused(() => fyiReporting.RDL.RdlEngineConfig.GetConnection("MySQL.NET","Server="+TestHelpers.NETWORK_HOST+";SslMode=None"));
				CollectionAssert.AreEqual(new[] { "MySqlConnector.MySqlConnection" },listCalls,"The hook still sees the one provider reports use.");
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
