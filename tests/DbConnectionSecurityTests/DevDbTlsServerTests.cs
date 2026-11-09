// O-12 (docs/port-notes/DB-TLS.md): the local test server with TLS, on 127.0.0.1:3306 only. These tests run once tools/Enable-LocalDbTls.ps1
// has turned on TLS and written the CA (DevDbTls.GetCaPath); before that they are inconclusive, unless OPMS_DB_REQUIRE_TLS=1 (the gates),
// which makes a missing CA a failure. They prove the sessions are encrypted (Ssl_cipher) and the server is verified. The only database they
// create is a dbtls_ scratch database, dropped afterwards; synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class DevDbTlsServerTests {
		private const string SERVER="127.0.0.1";
		private const string ROOT="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=10;Pooling=false";
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>The local test server's CA. Inconclusive without one, a failure when TLS is required (OPMS_DB_REQUIRE_TLS=1).</summary>
		private static string RequireCa() {
			string caPath=DevDbTls.GetCaPath();
			if(caPath!="") {
				return caPath;
			}
			if(DevDbTls.IsTlsRequired()) {
				Assert.Fail(DevDbTls.GetMissingMessage());
			}
			Assert.Inconclusive("No CA of the local test server yet: the owner turns on TLS with "+DevDbTls.ENABLE_SCRIPT+" (docs/port-notes/DB-TLS.md).");
			return null;
		}

		///<summary>Name and value rows of a SHOW or SELECT, as a dictionary (names lower case).</summary>
		private static Dictionary<string,string> Query(MySqlConnection mySqlConnection,string command) {
			Dictionary<string,string> dictValues=new Dictionary<string,string>();
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection))
			using(MySqlDataReader mySqlDataReader=mySqlCommand.ExecuteReader()) {
				while(mySqlDataReader.Read()) {
					dictValues[mySqlDataReader.GetString(0).ToLowerInvariant()]=Convert.ToString(mySqlDataReader.GetValue(1),CultureInfo.InvariantCulture);
				}
			}
			return dictValues;
		}

		private static Dictionary<string,string> GetSessionTls(MySqlConnection mySqlConnection) {
			return Query(mySqlConnection,"SHOW SESSION STATUS WHERE Variable_name IN ('Ssl_cipher','Ssl_version','Ssl_server_not_after')");
		}

		private static void AssertEncrypted(Dictionary<string,string> dictTls,string what) {
			Assert.AreNotEqual("",dictTls["ssl_cipher"],what+": the session is encrypted (Ssl_cipher).");
			CollectionAssert.Contains(new[] { "TLSv1.2","TLSv1.3" },dictTls["ssl_version"],what);
		}

		[TestMethod]
		public void TestHelpersConnection_DevDbTlsApply_IsEncrypted_TheServerHasTls12And13Only() {
			RequireCa();
			string connectionString=DevDbTls.Apply(ROOT);
			Assert.AreEqual(MySqlSslMode.VerifyCA,new MySqlConnectionStringBuilder(connectionString).SslMode);
			using(MySqlConnection mySqlConnection=new MySqlConnection(connectionString)) {
				mySqlConnection.Open();
				AssertEncrypted(GetSessionTls(mySqlConnection),"DevDbTls.Apply");
				Dictionary<string,string> dictServer=Query(mySqlConnection,"SELECT 'have_ssl',@@have_ssl UNION ALL SELECT 'tls_version',@@tls_version "
					+"UNION ALL SELECT 'port',@@port UNION ALL SELECT 'bind_address',@@bind_address");
				Assert.AreEqual("YES",dictServer["have_ssl"]);
				Assert.AreEqual("TLSv1.2,TLSv1.3",dictServer["tls_version"],"No TLS 1.0 or 1.1.");
				Assert.AreEqual("3306",dictServer["port"]);
				Assert.AreEqual("127.0.0.1",dictServer["bind_address"]);
			}
		}

		[TestMethod]
		public void ProgramsLoopbackStringWithTheCa_DataConnection_IsEncrypted() {
			string sslCa=RequireCa();
			Assert.AreEqual(sslCa,DevDbTls.GetSslCa(SERVER));
			DataConnection dataConnection=new DataConnection(SERVER,"","root","",DatabaseType.MySql,DevDbTls.GetSslCa(SERVER));
			System.Data.DataTable table=dataConnection.GetTable("SHOW SESSION STATUS LIKE 'Ssl_cipher'");
			Assert.AreNotEqual("",table.Rows[0][1].ToString(),"DataConnection with the helpers' SslCa (SslMode=VerifyCA) is encrypted.");
		}

		[TestMethod]
		public void ScratchDatabase_CreateFillReadDrop_OverTls() {
			RequireCa();
			string database="dbtls_"+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
			using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnection.Open();
				try {
					foreach(string command in new[] { "CREATE DATABASE `"+database+"` CHARACTER SET utf8mb3",
						"CREATE TABLE `"+database+"`.sample (SampleNum bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,Txt varchar(50)) ENGINE=MyISAM",
						"INSERT INTO `"+database+"`.sample (Txt) VALUES ('synthetic one'),('synthetic two')" })
					{
						using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
							mySqlCommand.ExecuteNonQuery();
						}
					}
					using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT COUNT(*) FROM `"+database+"`.sample",mySqlConnection)) {
						Assert.AreEqual(2L,Convert.ToInt64(mySqlCommand.ExecuteScalar()));
					}
					AssertEncrypted(GetSessionTls(mySqlConnection),"The scratch database's session");
				}
				finally {
					using(MySqlCommand mySqlCommand=new MySqlCommand("DROP DATABASE IF EXISTS `"+database+"`",mySqlConnection)) {
						mySqlCommand.ExecuteNonQuery();
					}
				}
			}
		}

		[TestMethod]
		public void VerifyFull_TheServerCertificateNamesLocalhostAnd127001() {
			string sslCa=RequireCa();
			//S2-2's certificate-login test connects with VerifyFull: the host name must be in the server certificate.
			foreach(string host in new[] { "127.0.0.1","localhost" }) {
				MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(ROOT) { Server=host,SslMode=MySqlSslMode.VerifyFull,SslCa=sslCa };
				using(MySqlConnection mySqlConnection=new MySqlConnection(builder.ConnectionString)) {
					mySqlConnection.Open();
					AssertEncrypted(GetSessionTls(mySqlConnection),"VerifyFull to "+host);
				}
			}
		}

		[TestMethod]
		public void AnotherCa_IsRefused_SoTheServerIsReallyVerified() {
			RequireCa();
			string caOther=TestHelpers.WriteCaPem(_folder,"other-ca.pem");
			foreach(MySqlSslMode sslMode in new[] { MySqlSslMode.VerifyCA,MySqlSslMode.VerifyFull }) {
				MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(ROOT) { SslMode=sslMode,SslCa=caOther };
				using(MySqlConnection mySqlConnection=new MySqlConnection(builder.ConnectionString)) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					Assert.IsTrue(ex is MySqlException || ex is System.Security.Authentication.AuthenticationException,sslMode+": "+ex.GetType().Name);
				}
			}
		}

		[TestMethod]
		public void ServerCertificate_IsValidForAnotherMonth() {
			RequireCa();
			using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnection.Open();
				string notAfter=GetSessionTls(mySqlConnection)["ssl_server_not_after"];
				//MariaDB writes it as OpenSSL does, for example "Nov  8 22:38:18 2031 GMT".
				string[] arrayParts=notAfter.Split(new[] { ' ' },StringSplitOptions.RemoveEmptyEntries);
				if(!DateTime.TryParseExact(string.Join(" ",arrayParts.Take(4)),"MMM d HH:mm:ss yyyy",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateNotAfter))
				{
					Assert.Inconclusive("Ssl_server_not_after in a form this test does not read: '"+notAfter+"'.");
				}
				Assert.IsTrue(dateNotAfter>DateTime.UtcNow.AddMonths(1),"The server certificate expires "+notAfter+": run "+DevDbTls.ENABLE_SCRIPT
					+" -Revert, then the script again, for new certificates.");
			}
		}

		[TestMethod]
		public void ClientCertificateForS22_ChainsToTheServersCa() {
			string sslCa=RequireCa();
			string pathClient=Path.Combine(Path.GetDirectoryName(DevDbTls.GetDefaultCaPath()),"client-cert.pem");
			if(!File.Exists(pathClient)) {
				Assert.Inconclusive("No client certificate (the script ran with -NoClientCertificate, or the CA comes from OPMS_DB_TLS_CA).");
			}
			X509Certificate2 x509Certificate2Ca=new X509Certificate2(sslCa);
			X509Certificate2 x509Certificate2Client=new X509Certificate2(pathClient);
			using(X509Chain x509Chain=new X509Chain()) {
				x509Chain.ChainPolicy.RevocationMode=X509RevocationMode.NoCheck;
				x509Chain.ChainPolicy.VerificationFlags=X509VerificationFlags.AllowUnknownCertificateAuthority;
				x509Chain.ChainPolicy.ExtraStore.Add(x509Certificate2Ca);
				Assert.IsTrue(x509Chain.Build(x509Certificate2Client));
				Assert.AreEqual(x509Certificate2Ca.Thumbprint,x509Chain.ChainElements[x509Chain.ChainElements.Count-1].Certificate.Thumbprint);
			}
			Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(pathClient),"client-key.pem")));
		}

		[TestMethod]
		public void CommandLineClient_WithDevDbTlsArguments_IsEncrypted() {
			RequireCa();
			string client=FindClient();
			if(client==null) {
				Assert.Inconclusive("No mariadb.exe (set MARIADB_BIN to the MariaDB bin folder).");
			}
			string arguments="--no-defaults --protocol=TCP --host=127.0.0.1 --port=3306 --user=root --connect-timeout=10 --batch --raw --skip-column-names "
				+string.Concat(DevDbTls.GetClientArguments(SERVER,3306).Select(x => "\""+x+"\" "))
				+"-e \"SHOW SESSION STATUS LIKE 'Ssl_cipher'\"";
			ProcessStartInfo processStartInfo=new ProcessStartInfo(client,arguments) {
				UseShellExecute=false,
				CreateNoWindow=true,
				RedirectStandardOutput=true,
				RedirectStandardError=true,
			};
			processStartInfo.EnvironmentVariables.Remove("MYSQL_PWD");
			using(Process process=Process.Start(processStartInfo)) {
				System.Threading.Tasks.Task<string> taskError=process.StandardError.ReadToEndAsync();
				string output=process.StandardOutput.ReadToEnd();
				Assert.IsTrue(process.WaitForExit(60000));
				Assert.AreEqual(0,process.ExitCode,taskError.Result);
				string[] arrayParts=output.Trim().Split('\t');
				Assert.AreEqual(2,arrayParts.Length,output);
				Assert.AreNotEqual("",arrayParts[1].Trim(),"mariadb.exe with --ssl-ca and --ssl-verify-server-cert is encrypted.");
			}
		}

		///<summary>mariadb.exe from MARIADB_BIN, else the newest MariaDB under Program Files.</summary>
		private static string FindClient() {
			List<string> listFolders=new List<string>();
			string bin=Environment.GetEnvironmentVariable("MARIADB_BIN");
			if(!string.IsNullOrWhiteSpace(bin)) {
				listFolders.Add(bin.Trim().Trim('"'));
			}
			string programFiles=Environment.GetEnvironmentVariable("ProgramW6432")??Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			if(Directory.Exists(programFiles)) {
				listFolders.AddRange(Directory.GetDirectories(programFiles,"MariaDB*").OrderByDescending(x => x).Select(x => Path.Combine(x,"bin")));
			}
			return listFolders.Select(x => Path.Combine(x,"mariadb.exe")).FirstOrDefault(x => File.Exists(x));
		}

		[TestMethod]
		public void WhenTheServerRequiresTls_AConnectionWithoutTlsIsRefused() {
			RequireCa();
			string required;
			using(MySqlConnection mySqlConnection=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnection.Open();
				using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT @@require_secure_transport",mySqlConnection)) {
					required=Convert.ToString(mySqlCommand.ExecuteScalar(),CultureInfo.InvariantCulture);
				}
			}
			if(required!="1") {
				Assert.Inconclusive("require_secure_transport is OFF: the lead's -RequireSecureTransport step has not run yet.");
			}
			using(MySqlConnection mySqlConnection=new MySqlConnection(ROOT+";SslMode=None")) {
				TestHelpers.AssertThrows<MySqlException>(() => mySqlConnection.Open());
			}
			//The program's loopback string without a CA (SslMode=none) is refused the same way; with the CA it connects.
			using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,"","root","")))
			{
				TestHelpers.AssertThrows<MySqlException>(() => mySqlConnection.Open());
			}
		}
	}
}
