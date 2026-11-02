// The read-only and reporting servers of their own (owner decision of 2026-11-02): as before the connection rule, and nothing of the program's
// own account goes to them. No connection is made: network hosts are refused or only built, never opened.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class PreferenceServerTests {
		private const string THUMBPRINT="0123456789ABCDEF0123456789ABCDEF01234567";
		///<summary>A read-only or reporting server of its own, from the documentation names: never contacted.</summary>
		private const string PREFERENCE_HOST="replica.example.test";
		private string _folder;
		private string _thumbprintOld;
		private string _officeCaPathOld;
		private string _officeCa;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_thumbprintOld=DataConnection.ClientCertificateThumbprint;
			_officeCaPathOld=DataConnection.OfficeCaPath;
			//This computer has an office CA and a client certificate, as a workstation set up by the tool has.
			_officeCa=TestHelpers.WriteCaPem(_folder);
			DataConnection.ClientCertificateThumbprint=THUMBPRINT;
			DataConnection.OfficeCaPath=_officeCa;
		}

		[TestCleanup]
		public void CleanUp() {
			DataConnection.ClientCertificateThumbprint=_thumbprintOld;
			DataConnection.OfficeCaPath=_officeCaPathOld;
			ConnectionStoreBase.ClearConnectionDictionary();
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>Runs action on a thread of its own, so the thread context it sets never stays on a test thread. Rethrows what it threw.</summary>
		internal static void RunOnThread(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.Start();
			thread.Join();
			if(exception!=null) {
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
			}
		}

		#region The connection string
		[TestMethod]
		public void Network_AsBefore_TheOldSslMode_ItsOwnCa_AndNoClientCertificate() {
			string caPreference=TestHelpers.WriteCaPem(_folder,"preference-ca.pem");
			MySqlConnectionStringBuilder builderNone=new MySqlConnectionStringBuilder(DataConnection.BuildPreferenceServerConnectionString(
				PREFERENCE_HOST+":3307","opendental","replica_user",TestHelpers.SECRET,""));
			Assert.AreEqual(MySqlSslMode.None,builderNone.SslMode,"No CA in the preference: SslMode=none, as on main.");
			Assert.AreEqual(PREFERENCE_HOST,builderNone.Server);
			Assert.AreEqual(3307u,builderNone.Port);
			Assert.AreEqual("replica_user",builderNone.UserID);
			Assert.AreEqual(TestHelpers.SECRET,builderNone.Password,"The preference's own password.");
			Assert.AreEqual("",builderNone.SslCa??"","Never this computer's office CA.");
			Assert.AreEqual("",builderNone.CertificateThumbprint??"","Never this computer's client certificate.");
			Assert.AreEqual(MySqlCertificateStoreLocation.None,builderNone.CertificateStoreLocation);
			MySqlConnectionStringBuilder builderCa=new MySqlConnectionStringBuilder(DataConnection.BuildPreferenceServerConnectionString(
				PREFERENCE_HOST,"opendental","replica_user","",caPreference));
			Assert.AreEqual(MySqlSslMode.VerifyCA,builderCa.SslMode,"A CA in the preference: VerifyCA, as on main.");
			Assert.AreEqual(caPreference,builderCa.SslCa,"The preference's CA, not the office CA.");
			Assert.AreEqual("",builderCa.CertificateThumbprint??"");
			//The same format as main's BuildSimpleConnectionString for these servers.
			StringAssert.StartsWith(DataConnection.BuildPreferenceServerConnectionString(PREFERENCE_HOST,"opendental","ro","",""),
				"Server="+PREFERENCE_HOST+";Port=3306;Database=opendental;User ID=ro;Password=;CharSet=utf8");
			StringAssert.EndsWith(DataConnection.BuildPreferenceServerConnectionString(PREFERENCE_HOST,"opendental","ro","",""),";SslMode=none");
		}

		[TestMethod]
		public void Network_TheBuiltString_PassesWithoutTheRule() {
			string connectionString=DataConnection.BuildPreferenceServerConnectionString(PREFERENCE_HOST,"opendental","ro","","");
			Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionString));
			DbConnectionSecurity.CheckConnectionString(connectionString);
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionString)) {
				Assert.AreEqual(connectionString,mySqlConnection.ConnectionString,"Created, not opened: nothing is sent anywhere.");
			}
			DbConnectionSecurity.CheckReportConnection("MySQL.NET",typeof(MySqlConnection).FullName,connectionString);
		}

		[TestMethod]
		public void TheMainConnection_NeverSkipsTheRule_ThroughTheExemption() {
			//The read-only server is the main server's host with the main user and password: still two different connections.
			string connectionStringPreference=DataConnection.BuildPreferenceServerConnectionString(TestHelpers.NETWORK_HOST,"opendental","opmsd",
				TestHelpers.SECRET,"");
			Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionStringPreference));
			//The main connection is built by BuildSimpleConnectionString: encrypted and checked by the rule, whatever was built for the preference.
			DataConnection.OfficeCaPath="";
			string connectionStringMain=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,"opendental","opmsd",
				TestHelpers.SECRET,"","");
			Assert.AreEqual(MySqlSslMode.VerifyFull,new MySqlConnectionStringBuilder(connectionStringMain).SslMode);
			Assert.IsFalse(DbConnectionSecurity.IsPreferenceServerConnection(connectionStringMain));
			DbConnectionRefusedException ex=RuleTests.AssertRefused(() => DbConnectionSecurity.NewMySqlConnection(connectionStringMain));
			StringAssert.Contains(ex.Message,"the office CA certificate file (SslCa)");
			//A string that only looks like the preference's: written by hand, changed, or with options added. Each is checked by the rule.
			foreach(string connectionString in new[] {
				"Server="+TestHelpers.NETWORK_HOST+";Port=3306;Database=opendental;User ID=opmsd;Password=x;SslMode=none",
				connectionStringPreference.Replace("SslMode=none","SslMode=None"),
				connectionStringPreference+";pooling=false",
				connectionStringPreference+";CertificateStoreLocation=LocalMachine;CertificateThumbprint="+THUMBPRINT,
				" "+connectionStringPreference,
			})
			{
				Assert.IsFalse(DbConnectionSecurity.IsPreferenceServerConnection(connectionString),connectionString);
				RuleTests.AssertRefused(() => DbConnectionSecurity.CheckConnectionString(connectionString));
			}
			//A connection string element in FreeDentalConfig.xml that is exactly the preference's is still refused for the main connection.
			StringAssert.Contains(DbConnectionSecurity.GetConnectionStringProblem(connectionStringPreference),"over the network");
			Assert.IsFalse(DbConnectionSecurity.IsPreferenceServerConnection(""));
			Assert.IsFalse(DbConnectionSecurity.IsPreferenceServerConnection(null));
		}

		[TestMethod]
		public void AnEmptyServer_IsTheMainConnection_AndIsRefusedHere() {
			TestHelpers.AssertThrows<ArgumentException>(() => DataConnection.BuildPreferenceServerConnectionString("","opendental","ro","",""));
			TestHelpers.AssertThrows<ArgumentException>(() => DataConnection.BuildPreferenceServerConnectionString(null,"opendental","ro","",""));
			TestHelpers.AssertThrows<ArgumentException>(() => DataConnection.CreateForPreferenceServer("","mysql","ro","",""));
		}

		[TestMethod]
		public void PreferenceValues_AreQuoted_SoNoneAddsACertificateOrAnotherServer() {
			string server=PREFERENCE_HOST+";CertificateStoreLocation=LocalMachine;CertificateThumbprint="+THUMBPRINT;
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(DataConnection.BuildPreferenceServerConnectionString(server,
				"opendental","ro;Server=other.example.test"," p\"w;x ",""));
			Assert.AreEqual("",builder.CertificateThumbprint??"");
			Assert.AreEqual(MySqlCertificateStoreLocation.None,builder.CertificateStoreLocation);
			Assert.AreEqual(server,builder.Server,"The whole value is one (unresolvable) host name.");
			Assert.AreEqual("ro;Server=other.example.test",builder.UserID);
			Assert.AreEqual(" p\"w;x ",builder.Password);
		}

		[TestMethod]
		public void WithoutPooling_KeepsThePreferenceServer_AndTheRuleForEveryOtherString() {
			string connectionString=DataConnection.BuildPreferenceServerConnectionString(PREFERENCE_HOST,"opendental","ro","","");
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnectionWithoutPooling(connectionString)) {
				MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(mySqlConnection.ConnectionString);
				Assert.IsFalse(builder.Pooling);
				Assert.AreEqual(PREFERENCE_HOST,builder.Server);
				Assert.AreEqual(MySqlSslMode.None,builder.SslMode);
			}
			RuleTests.AssertRefused(() => DbConnectionSecurity.NewMySqlConnectionWithoutPooling("Server="+PREFERENCE_HOST+";SslMode=None;Password=x"));
		}
		#endregion The connection string

		#region The thread context
		[TestMethod]
		public void ThreadContext_EverythingBuiltAgainForThatServer_GoesTheOldWay_AndTheMainConnectionStaysAsItIs() {
			string caPreference=TestHelpers.WriteCaPem(_folder,"preference-ca.pem");
			string connectionStringMainBefore=null;
			RunOnThread(() => connectionStringMainBefore=DataConnection.GetConnectionString());
			int commandTimeoutOld=DataConnection.CommandTimeout;
			RunOnThread(() => {
				new DataConnection(DatabaseType.MySql).SetDbTPreferenceServer(PREFERENCE_HOST,"opendental","ro","",caPreference,skipTestConnection:true);
				string connectionStringThread=DataConnection.GetCurrentConnectionString();
				Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionStringThread));
				Assert.AreEqual(MySqlSslMode.VerifyCA,new MySqlConnectionStringBuilder(connectionStringThread).SslMode);
				Assert.AreEqual("",DataConnection.GetMysqlPass(),"An empty password stays empty: never the main connection's.");
				//Built again from the thread's context: the lower user, another database on that server.
				foreach(string connectionString in new[] { DataConnection.GetLowConnectionString(),
					DataConnection.GetReportConnectionString(PREFERENCE_HOST,"opendental_other","ro","") })
				{
					MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
					Assert.AreEqual(PREFERENCE_HOST,builder.Server,connectionString);
					Assert.AreEqual("",builder.CertificateThumbprint??"","This computer's client certificate never goes to that server.");
					Assert.AreEqual(caPreference,builder.SslCa,"Its own CA.");
					Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionString));
				}
				//Any other server on this thread is the program's own account: the rule, the office CA and the client certificate.
				MySqlConnectionStringBuilder builderOther=new MySqlConnectionStringBuilder(DataConnection.GetReportConnectionString(TestHelpers.NETWORK_HOST,
					"opendental","opmsd",TestHelpers.SECRET));
				Assert.AreEqual(MySqlSslMode.VerifyFull,builderOther.SslMode);
				Assert.AreEqual(_officeCa,builderOther.SslCa);
				Assert.AreEqual(THUMBPRINT,builderOther.CertificateThumbprint);
				//A new command timeout changes this thread's connection only, still the old way.
				DataConnection.CommandTimeout=4321;
				string connectionStringTimeout=DataConnection.GetCurrentConnectionString();
				StringAssert.Contains(connectionStringTimeout,"Default Command Timeout=4321");
				Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionStringTimeout));
				DataConnection.CommandTimeout=commandTimeoutOld;
			});
			string connectionStringMainAfter=null;
			RunOnThread(() => connectionStringMainAfter=DataConnection.GetConnectionString());
			Assert.AreEqual(connectionStringMainBefore,connectionStringMainAfter,"The main connection is never set to the preference's server.");
		}

		[TestMethod]
		public void ThreadContext_AChildThread_KeepsThePreferenceServer() {
			string connectionStringParent=null;
			string connectionStringChild=null;
			RunOnThread(() => {
				using(DataConnection dataConnection=new DataConnection(DatabaseType.MySql)) {
					dataConnection.SetDbTPreferenceServer(PREFERENCE_HOST,"opendental","ro","","",skipTestConnection:true);
				}
				connectionStringParent=DataConnection.GetCurrentConnectionString();
				ODThread odThread=new ODThread(o => connectionStringChild=DataConnection.GetCurrentConnectionString());
				Exception exceptionChild=null;
				odThread.AddExceptionHandler(ex => exceptionChild=ex);
				odThread.Start(true);
				odThread.Join(Timeout.Infinite);
				if(exceptionChild!=null) {
					throw exceptionChild;
				}
			});
			Assert.AreEqual(connectionStringParent,connectionStringChild);
		}

		[TestMethod]
		public void ThreadContext_TheOrdinarySetDbT_IsCheckedByTheRule_EvenForThatServer() {
			//No office CA and no client certificate here, so the rule refuses before anything is attempted.
			DataConnection.OfficeCaPath="";
			DataConnection.ClientCertificateThumbprint="";
			RunOnThread(() => {
				DataConnection dataConnection=new DataConnection(DatabaseType.MySql);
				dataConnection.SetDbTPreferenceServer(PREFERENCE_HOST,"opendental","ro","","",skipTestConnection:true);
				//Only SetDbTPreferenceServer (ConnectionStore's marked connections) builds the old way; SetDbT is the program's own account.
				RuleTests.AssertRefused(() => dataConnection.SetDbT(PREFERENCE_HOST,"opendental","ro","","","",DatabaseType.MySql,true));
				RuleTests.AssertRefused(() => dataConnection.SetDbT(TestHelpers.NETWORK_HOST,"opendental","opmsd","","","",DatabaseType.MySql,true));
				Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(DataConnection.GetCurrentConnectionString()),
					"A refused SetDbT leaves the thread's context as it was.");
			});
		}

		[TestMethod]
		public void SetDb_RefusesThePreferenceServersString_AndTheMainConnectionStaysAsItIs() {
			//The string a lost read-only connection reports (StartConnectionErrorRetry: GetCurrentConnectionString on that thread).
			string connectionStringPreference=null;
			RunOnThread(() => {
				new DataConnection(DatabaseType.MySql).SetDbTPreferenceServer(PREFERENCE_HOST,"opendental","ro","","",skipTestConnection:true);
				connectionStringPreference=DataConnection.GetCurrentConnectionString();
			});
			Assert.IsTrue(DbConnectionSecurity.IsPreferenceServerConnection(connectionStringPreference));
			string connectionStringMainBefore=null;
			RunOnThread(() => connectionStringMainBefore=DataConnection.GetConnectionString());
			Assert.AreNotEqual(connectionStringPreference,connectionStringMainBefore);
			//Refused before any connection (the host is never contacted; a connection attempt would end in a MySqlException), as the main
			//connection or as its lower user's string, on any thread.
			RunOnThread(() => {
				DataConnection dataConnection=new DataConnection(DatabaseType.MySql);
				DbConnectionRefusedException ex=RuleTests.AssertRefused(() => dataConnection.SetDb(connectionStringPreference,"",DatabaseType.MySql));
				StringAssert.Contains(ex.Message,"thread of its own only");
				Assert.AreEqual(PREFERENCE_HOST,ex.Server);
				RuleTests.AssertRefused(() => dataConnection.SetDb(connectionStringPreference,"",DatabaseType.MySql,true));
				string connectionStringLoopback=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"localhost","opendental","root","");
				RuleTests.AssertRefused(() => dataConnection.SetDb(connectionStringLoopback,connectionStringPreference,DatabaseType.MySql,true));
				Assert.AreEqual(connectionStringMainBefore,DataConnection.GetConnectionString(),"This thread has no context of its own: the main one.");
			});
			string connectionStringMainAfter=null;
			RunOnThread(() => connectionStringMainAfter=DataConnection.GetConnectionString());
			Assert.AreEqual(connectionStringMainBefore,connectionStringMainAfter,"The main connection is never set to the preference's server.");
		}

		[TestMethod]
		public void ConnectionLostWindow_OnlyTestsTheLostConnection() {
			//A window, so a source check: the lost connection can be any thread's, also a read-only or reporting server's (section 5.1 of the spec).
			string source=TestHelpers.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDentalThreads.cs");
			int indexStart=source.IndexOf("private void BeginDataConnectionLostThread(",StringComparison.Ordinal);
			Assert.IsTrue(indexStart>=0);
			int indexEnd=source.IndexOf("#endregion",indexStart,StringComparison.Ordinal);
			Assert.IsTrue(indexEnd>indexStart);
			string method=source.Substring(indexStart,indexEnd-indexStart);
			StringAssert.Contains(method,".TestConnectionString(e.ConnectionString,");
			foreach(string call in new[] { ".SetDb(",".SetDbT(" }) {
				Assert.IsFalse(method.Contains(call),"The window sets no connection: "+call);
			}
		}
		#endregion The thread context

		#region ConnectionStore
		[TestMethod]
		public void ConnectionStore_OnlyAConnectionMadeFromThePreferences_IsAPreferenceServer() {
			DataConnection.OfficeCaPath="";
			//An entry under the read-only name that ConnectionStore did not make from the preferences (a ConnectionStore.xml entry, an override):
			//the rule, which refuses it before connecting.
			ConnectionStoreBase.OverrideConnection(ConnectionNames.DentalOfficeReadOnlyServer,new CentralConnectionBase() { ServerName=PREFERENCE_HOST,
				DatabaseName="opendental",MySqlUser="ro",MySqlPassword="" });
			RunOnThread(() => RuleTests.AssertRefused(() => ConnectionStoreBase.SetDbT(ConnectionNames.DentalOfficeReadOnlyServer,
				new DataConnection(DatabaseType.MySql))));
			//A marked connection under any other name: the rule too.
			CentralConnectionBase centralConnectionBaseMarked=new CentralConnectionBase() { ServerName=PREFERENCE_HOST,DatabaseName="opendental",
				MySqlUser="ro",MySqlPassword="" };
			centralConnectionBaseMarked.MarkPreferenceServer();
			ConnectionStoreBase.OverrideConnection(ConnectionNames.DentalOffice,centralConnectionBaseMarked);
			RunOnThread(() => RuleTests.AssertRefused(() => ConnectionStoreBase.SetDbT(ConnectionNames.DentalOffice,new DataConnection(DatabaseType.MySql))));
			//Never the main connection.
			ConnectionStoreBase.OverrideConnection(ConnectionNames.DentalOfficeReadOnlyServer,centralConnectionBaseMarked);
			RunOnThread(() => TestHelpers.AssertThrows<ApplicationException>(() => ConnectionStoreBase.SetDb(ConnectionNames.DentalOfficeReadOnlyServer)));
		}

		[TestMethod]
		public void ConnectionStoreXml_CanNeverClaimToBeAPreferenceServer() {
			string xml="<ListCentralConnections><CentralConnection><ServerName>"+PREFERENCE_HOST+"</ServerName><DatabaseName>opendental</DatabaseName>"
				+"<MySqlUser>ro</MySqlUser><Note>DentalOfficeReadOnlyServer</Note><IsPreferenceServer>true</IsPreferenceServer></CentralConnection>"
				+"</ListCentralConnections>";
			ConnectionStoreBase.ListCentralConnections listCentralConnections;
			using(XmlReader xmlReader=XmlReader.Create(new StringReader(xml))) {
				listCentralConnections=(ConnectionStoreBase.ListCentralConnections)new XmlSerializer(typeof(ConnectionStoreBase.ListCentralConnections))
					.Deserialize(xmlReader);
			}
			Assert.AreEqual(1,listCentralConnections.Items.Count);
			Assert.AreEqual(PREFERENCE_HOST,listCentralConnections.Items[0].ServerName);
			Assert.IsFalse(listCentralConnections.Items[0].IsPreferenceServer);
			using(StringWriter stringWriter=new StringWriter()) {
				CentralConnectionBase centralConnectionBase=new CentralConnectionBase() { ServerName=PREFERENCE_HOST };
				centralConnectionBase.MarkPreferenceServer();
				new XmlSerializer(typeof(CentralConnectionBase)).Serialize(stringWriter,centralConnectionBase);
				Assert.IsFalse(stringWriter.ToString().Contains("IsPreferenceServer"),"Never written to a file.");
			}
		}
		#endregion ConnectionStore
	}
}
