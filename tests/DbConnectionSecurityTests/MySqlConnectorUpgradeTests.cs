// S2-2 of the .NET 10 move (docs/port-notes/S2-2.md): what changed for this program between MySqlConnector 2.3.7 and 2.6.2, checked on the
// local MariaDB at 127.0.0.1:3306 (root), through DevDbTls as every helper connects. A killed session leaves the connection Broken (2.3.7
// left it Closed) and Open then throws until it is closed; the program's DataConnection closes it first and reconnects; a session killed
// in the middle of a query fails that query only. The sessions killed are the tests' own (their CONNECTION_ID()); no database is created.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class MySqlConnectorUpgradeTests {
		private const string SERVER="127.0.0.1";
		private const string ROOT="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=10;Pooling=false";

		///<summary>Ends the server session with this id from a connection of its own (KILL CONNECTION).</summary>
		private static void KillSession(long id) {
			using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnection.Open();
				using(MySqlCommand mySqlCommand=new MySqlCommand("KILL CONNECTION "+id.ToString(CultureInfo.InvariantCulture),mySqlConnection)) {
					mySqlCommand.ExecuteNonQuery();
				}
			}
		}

		private static long GetSessionId(MySqlConnection mySqlConnection) {
			using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT CONNECTION_ID()",mySqlConnection)) {
				return Convert.ToInt64(mySqlCommand.ExecuteScalar(),CultureInfo.InvariantCulture);
			}
		}

		///<summary>Runs SELECT 1 until it throws: the server ends a killed session at once, but the client may still read from it once.</summary>
		private static MySqlException RunUntilItFails(MySqlConnection mySqlConnection) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			while(stopwatch.Elapsed<TimeSpan.FromSeconds(10)) {
				try {
					using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT 1",mySqlConnection)) {
						mySqlCommand.ExecuteScalar();
					}
				}
				catch(MySqlException ex) {
					return ex;
				}
				Thread.Sleep(100);
			}
			Assert.Fail("A query on a killed session did not fail within 10 seconds.");
			return null;
		}

		[TestMethod]
		public void TheDriver_IsMySqlConnector262() {
			Assembly assembly=typeof(MySqlConnection).Assembly;
			Assert.AreEqual("2.0.0.0",assembly.GetName().Version.ToString(),"MySqlConnector keeps its assembly version, so no redirect names it.");
			Assert.AreEqual("2.6.2.0",FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion);
		}

		[TestMethod]
		public void KilledSession_LeavesTheConnectionBroken_OpenThrowsUntilItIsClosed() {
			using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnection.Open();
				KillSession(GetSessionId(mySqlConnection));
				RunUntilItFails(mySqlConnection);
				//2.3.7 left it Closed; 2.4 and later say Broken (https://mysqlconnector.net/overview/version-history/).
				Assert.AreEqual(ConnectionState.Broken,mySqlConnection.State);
				InvalidOperationException ex=TestHelpers.AssertThrows<InvalidOperationException>(() => mySqlConnection.Open());
				StringAssert.Contains(ex.Message,"Broken");
				mySqlConnection.Close();
				Assert.AreEqual(ConnectionState.Closed,mySqlConnection.State);
				mySqlConnection.Open();
				using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT 1",mySqlConnection)) {
					Assert.AreEqual(1L,Convert.ToInt64(mySqlCommand.ExecuteScalar(),CultureInfo.InvariantCulture));
				}
			}
		}

		[TestMethod]
		public void ResetConnectionOnAKilledSession_ThrowsMySqlException() {
			//2.4 and later throw MySqlException here (2.3.7 let the SocketException through). No code of the program calls it; this pins the change.
			using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnection.Open();
				KillSession(GetSessionId(mySqlConnection));
				Thread.Sleep(300);
				TestHelpers.AssertThrows<MySqlException>(() => mySqlConnection.ResetConnectionAsync().GetAwaiter().GetResult());
				Assert.AreEqual(ConnectionState.Broken,mySqlConnection.State);
			}
		}

		[TestMethod]
		public void DataConnection_ItsConnectionBroken_ReconnectsOnTheNextQuery() {
			//The program's own class with the helpers' CA, as DevDbTlsServerTests connects. Its connection is made Broken from outside (a session
			//killed while it is open), the state an exception that did not come through RunMySqlAction leaves it in.
			using(DataConnection dataConnection=new DataConnection(SERVER,"","root","",DatabaseType.MySql,DevDbTls.GetSslCa(SERVER))) {
				FieldInfo fieldInfo=typeof(DataConnection).GetField("_con",BindingFlags.Instance|BindingFlags.NonPublic);
				Assert.IsNotNull(fieldInfo,"DataConnection._con");
				MySqlConnection mySqlConnection=(MySqlConnection)fieldInfo.GetValue(dataConnection);
				mySqlConnection.Open();
				KillSession(GetSessionId(mySqlConnection));
				RunUntilItFails(mySqlConnection);
				Assert.AreEqual(ConnectionState.Broken,mySqlConnection.State);
				//DataConnection.OpenCon closes a Broken connection before it opens it.
				Assert.AreEqual("1",dataConnection.GetScalar("SELECT 1",doRetryConn:false));
				Assert.AreEqual("2",dataConnection.GetCount("SELECT 2",doRetryConn:false));
				Assert.AreEqual(1,dataConnection.GetTable("SELECT 3",doAutoRetry:false).Rows.Count);
			}
		}

		[TestMethod]
		public void DataConnection_SessionKilledDuringAQuery_ThatQueryFails_TheNextOneRuns() {
			string marker="s22_"+Guid.NewGuid().ToString("N").Substring(0,12);
			using(DataConnection dataConnection=new DataConnection(SERVER,"","root","",DatabaseType.MySql,DevDbTls.GetSslCa(SERVER))) {
				//Not a SELECT, so RunDbAction does not run it again ("fatal error"/"transport connection" retries are for SELECT and securitylog).
				Task<string> task=Task.Run(() => dataConnection.GetScalar("DO SLEEP(20) /*"+marker+"*/",doRetryConn:false));
				long id=0;
				Stopwatch stopwatch=Stopwatch.StartNew();
				using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
					mySqlConnection.Open();
					while(id==0 && stopwatch.Elapsed<TimeSpan.FromSeconds(10)) {
						using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT ID FROM information_schema.PROCESSLIST WHERE INFO LIKE '%"+marker
							+"%' AND INFO NOT LIKE '%PROCESSLIST%'",mySqlConnection))
						{
							id=Convert.ToInt64(mySqlCommand.ExecuteScalar()??0L,CultureInfo.InvariantCulture);
						}
						if(id==0) {
							Thread.Sleep(100);
						}
					}
				}
				Assert.AreNotEqual(0L,id,"The query did not show up in the process list.");
				KillSession(id);
				AggregateException ex=TestHelpers.AssertThrows<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(30)));
				Assert.IsInstanceOfType(ex.InnerException,typeof(MySqlException),ex.InnerException.ToString());
				Assert.IsTrue(stopwatch.Elapsed<TimeSpan.FromSeconds(19),"The killed query ended at once, not after its 20 seconds.");
				Assert.AreEqual("1",dataConnection.GetScalar("SELECT 1",doRetryConn:false));
			}
		}

		[TestMethod]
		public void ProgramsConnectionOptions_AreReadAsBefore() {
			//The fixed options of BuildConnectionStringStart and the TLS options of the connection rule: none was renamed or removed in 2.4 to 2.6.
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,
				"db.example.test:3306","opendental","opmsd","x","C:\\ca\\office-ca.pem","0123456789ABCDEF0123456789ABCDEF01234567"));
			Assert.AreEqual("db.example.test",builder.Server);
			Assert.AreEqual(3306u,builder.Port);
			Assert.AreEqual("utf8",builder.CharacterSet);
			Assert.IsFalse(builder.TreatTinyAsBoolean);
			Assert.IsTrue(builder.AllowUserVariables);
			Assert.IsTrue(builder.ConvertZeroDateTime);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode);
			Assert.AreEqual(MySqlCertificateStoreLocation.LocalMachine,builder.CertificateStoreLocation);
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",builder.CertificateThumbprint);
			Assert.IsFalse(builder.SkipCertificateRevocationCheck,"2.6's new option stays off: VerifyFull checks revocation as 2.3.7 did.");
			//The program never sets the logging provider, which 2.4 made obsolete; nothing else in the code names it.
			Assert.IsNotNull(typeof(MySqlConnector.Logging.MySqlConnectorLogManager).GetProperty("Provider").GetCustomAttribute<ObsoleteAttribute>());
		}
	}
}
