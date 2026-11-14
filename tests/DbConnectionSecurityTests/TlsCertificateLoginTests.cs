// The database TLS check of S2-2 (docs/specs/net10-plan.md, the S2-2 row; open points O-8 and O-12; docs/port-notes/S2-2.md, section 6): the
// program's connection over a non-loopback address of this computer to the local test server, as a user created REQUIRE X509, with
// SslMode=VerifyFull, the test CA and a client certificate from the computer's store (LocalMachine\My, where the connection settings tool puts
// it, OpenPmsDentalDbSettings/ClientCertificateStore.cs): once with a certificate whose private key is in a CAPI provider and once with one in a
// CNG provider. Inconclusive unless the run names the server and both certificates (the variables below); a variable that is set wrongly fails.
// The test creates the REQUIRE X509 user and an s22_ scratch database with one synthetic row through the local test server's own loopback
// connection (127.0.0.1:3306, root, DevDbTls), and drops both afterwards.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class TlsCertificateLoginTests {
		///<summary>A non-loopback address (or name) of this computer at which the local test server listens and which its certificate names, in
		///the program's notation ("host" or "host:3306").</summary>
		public const string VARIABLE_SERVER="OPMS_DB_TLS_TEST_SERVER";
		///<summary>The thumbprint of a client certificate in LocalMachine\My whose private key is in a CAPI (CryptoAPI) provider.</summary>
		public const string VARIABLE_CAPI="OPMS_DB_TLS_TEST_CAPI_THUMBPRINT";
		///<summary>The thumbprint of a client certificate in LocalMachine\My whose private key is in a CNG (Key Storage Provider) provider.</summary>
		public const string VARIABLE_CNG="OPMS_DB_TLS_TEST_CNG_THUMBPRINT";
		///<summary>Optional: the CA (PEM) that signed the server's certificate. Without it, the local test server's CA (DevDbTls.GetCaPath).</summary>
		public const string VARIABLE_CA="OPMS_DB_TLS_TEST_CA";
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

		private static string GetVariable(string name) {
			return (Environment.GetEnvironmentVariable(name)??"").Trim();
		}

		///<summary>The test's set-up from the variables: inconclusive when none of the three is set, a failure when only some are, or when a value
		///is not what it must be (a loopback server, another port than the local test server's, no CA, a certificate that is not there).</summary>
		private static void ReadSetup(out string serverTest,out string sslCa,out string thumbprintCapi,out string thumbprintCng) {
			serverTest=GetVariable(VARIABLE_SERVER);
			thumbprintCapi=GetVariable(VARIABLE_CAPI).Replace(" ","").ToUpperInvariant();
			thumbprintCng=GetVariable(VARIABLE_CNG).Replace(" ","").ToUpperInvariant();
			if(serverTest=="" && thumbprintCapi=="" && thumbprintCng=="") {
				Assert.Inconclusive("The database TLS check needs "+VARIABLE_SERVER+", "+VARIABLE_CAPI+" and "+VARIABLE_CNG+" (docs/port-notes/S2-2.md, "
					+"section 6).");
			}
			Assert.IsTrue(serverTest!="" && thumbprintCapi!="" && thumbprintCng!="","Set all three of "+VARIABLE_SERVER+", "+VARIABLE_CAPI+" and "
				+VARIABLE_CNG+", or none.");
			Assert.IsTrue(DbConnectionSecurity.SplitServerList(serverTest,out string host,out string port) && !host.Contains(","),
				VARIABLE_SERVER+" names one server: "+serverTest);
			Assert.IsFalse(DbConnectionSecurity.IsLoopbackConnectorServer(host),VARIABLE_SERVER+" must be a non-loopback address of this computer, "
				+"not "+serverTest+": the rule exempts loopback connections from TLS.");
			Assert.IsTrue(port=="" || port=="3306",VARIABLE_SERVER+": the local test server listens on port 3306, the one the test creates its user on.");
			sslCa=GetVariable(VARIABLE_CA);
			if(sslCa=="") {
				sslCa=DevDbTls.GetCaPath();
			}
			Assert.IsTrue(sslCa!="" && File.Exists(sslCa),"No CA: set "+VARIABLE_CA+" or run "+DevDbTls.ENABLE_SCRIPT+" (docs/port-notes/DB-TLS.md).");
		}

		///<summary>The certificate in LocalMachine\My with its private key, and whether that key is in a CNG provider (false: CAPI).</summary>
		private static bool IsCngKey(string thumbprint,string variable) {
			using(X509Store x509Store=new X509Store(StoreName.My,StoreLocation.LocalMachine)) {
				x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				X509Certificate2Collection x509Certificate2Collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false);
				Assert.AreEqual(1,x509Certificate2Collection.Count,variable+": no certificate "+thumbprint+" in LocalMachine\\My.");
				X509Certificate2 x509Certificate2=x509Certificate2Collection[0];
				Assert.IsTrue(x509Certificate2.HasPrivateKey,variable+": the certificate has no private key.");
				AsymmetricAlgorithm asymmetricAlgorithm=(AsymmetricAlgorithm)x509Certificate2.GetRSAPrivateKey()??x509Certificate2.GetECDsaPrivateKey();
				Assert.IsNotNull(asymmetricAlgorithm,variable+": this account cannot read the private key (an RSA or ECDSA key).");
				if(asymmetricAlgorithm is RSACryptoServiceProvider) {
					return false;
				}
				Assert.IsTrue(asymmetricAlgorithm is RSACng || asymmetricAlgorithm is ECDsaCng,variable+": a key of "+asymmetricAlgorithm.GetType().Name);
				return true;
			}
		}

		private static void Run(MySqlConnection mySqlConnection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static Dictionary<string,string> GetSessionValues(MySqlConnection mySqlConnection) {
			Dictionary<string,string> dictValues=new Dictionary<string,string>();
			using(MySqlCommand mySqlCommand=new MySqlCommand("SHOW SESSION STATUS WHERE Variable_name IN ('Ssl_cipher','Ssl_version')",mySqlConnection))
			using(MySqlDataReader mySqlDataReader=mySqlCommand.ExecuteReader()) {
				while(mySqlDataReader.Read()) {
					dictValues[mySqlDataReader.GetString(0).ToLowerInvariant()]=mySqlDataReader.GetString(1);
				}
			}
			using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT CURRENT_USER()",mySqlConnection)) {
				dictValues["current_user"]=Convert.ToString(mySqlCommand.ExecuteScalar(),CultureInfo.InvariantCulture);
			}
			return dictValues;
		}

		[TestMethod]
		public void NonLoopbackServer_VerifyFull_X509User_WithACapiKeyAndWithACngKey() {
			ReadSetup(out string serverTest,out string sslCa,out string thumbprintCapi,out string thumbprintCng);
			Assert.IsFalse(IsCngKey(thumbprintCapi,VARIABLE_CAPI),VARIABLE_CAPI+" names a certificate whose key is in a CNG provider.");
			Assert.IsTrue(IsCngKey(thumbprintCng,VARIABLE_CNG),VARIABLE_CNG+" names a certificate whose key is in a CAPI provider.");
			string suffix=DateTime.UtcNow.ToString("MMddHHmmss",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N").Substring(0,6);
			string database="s22_tls_"+suffix;
			string user="s22_x509_"+suffix;
			using(MySqlConnection mySqlConnectionAdmin=new MySqlConnection(DevDbTls.Apply(ROOT))) {
				mySqlConnectionAdmin.Open();
				try {
					Run(mySqlConnectionAdmin,"CREATE DATABASE `"+database+"` CHARACTER SET utf8mb3");
					Run(mySqlConnectionAdmin,"CREATE TABLE `"+database+"`.sample (SampleNum bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,Txt varchar(50)) ENGINE=MyISAM");
					Run(mySqlConnectionAdmin,"INSERT INTO `"+database+"`.sample (Txt) VALUES ('synthetic tls row')");
					//No password: the certificate is the only credential, so a login proves the certificate was sent and accepted.
					Run(mySqlConnectionAdmin,"CREATE USER '"+user+"'@'%' REQUIRE X509");
					Run(mySqlConnectionAdmin,"GRANT SELECT ON `"+database+"`.* TO '"+user+"'@'%'");
					foreach(string thumbprint in new[] { thumbprintCapi,thumbprintCng }) {
						string label=(thumbprint==thumbprintCapi) ? "CAPI" : "CNG";
						//The program's own string for a server over the network, with this certificate: VerifyFull, the CA, LocalMachine.
						string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,serverTest,database,user,"",sslCa:sslCa,
							certificateThumbprint:thumbprint);
						MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
						Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode,label);
						Assert.AreEqual(sslCa,builder.SslCa,label+": this computer has an office CA (DataConnection.OfficeCaPath), which the string takes "
							+"instead of the test CA.");
						Assert.AreEqual(MySqlCertificateStoreLocation.LocalMachine,builder.CertificateStoreLocation,label);
						using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionString)) {
							mySqlConnection.Open();
							Dictionary<string,string> dictValues=GetSessionValues(mySqlConnection);
							Assert.AreNotEqual("",dictValues["ssl_cipher"],label+": the session is encrypted.");
							CollectionAssert.Contains(new[] { "TLSv1.2","TLSv1.3" },dictValues["ssl_version"],label);
							StringAssert.StartsWith(dictValues["current_user"],user+"@",label+": logged in as the REQUIRE X509 user.");
							using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT Txt FROM sample",mySqlConnection)) {
								Assert.AreEqual("synthetic tls row",Convert.ToString(mySqlCommand.ExecuteScalar(),CultureInfo.InvariantCulture),
									label+": the same server the test created the database on.");
							}
							MySqlConnection.ClearPool(mySqlConnection);
						}
					}
					//Without a certificate the REQUIRE X509 user is refused (the rule refuses such a string first, so it goes to MySqlConnector
					//directly), and with another CA the server is refused: the verification over this address is real.
					MySqlConnectionStringBuilder builderNoCertificate=new MySqlConnectionStringBuilder(DataConnection.BuildSimpleConnectionString(
						DatabaseType.MySql,serverTest,database,user,"",sslCa:sslCa,certificateThumbprint:"")) { Pooling=false };
					using(MySqlConnection mySqlConnection=new MySqlConnection(builderNoCertificate.ConnectionString)) {
						MySqlException ex=TestHelpers.AssertThrows<MySqlException>(() => mySqlConnection.Open());
						Assert.AreEqual(MySqlErrorCode.AccessDenied,ex.ErrorCode,ex.Message);
					}
					MySqlConnectionStringBuilder builderOtherCa=new MySqlConnectionStringBuilder(DataConnection.BuildSimpleConnectionString(
						DatabaseType.MySql,serverTest,database,user,"",sslCa:TestHelpers.WriteCaPem(_folder,"other-ca.pem"),certificateThumbprint:thumbprintCng))
						{ Pooling=false };
					using(MySqlConnection mySqlConnection=new MySqlConnection(builderOtherCa.ConnectionString)) {
						TestHelpers.AssertThrows<MySqlException>(() => mySqlConnection.Open());
					}
				}
				finally {
					Run(mySqlConnectionAdmin,"DROP USER IF EXISTS '"+user+"'@'%'");
					Run(mySqlConnectionAdmin,"DROP DATABASE IF EXISTS `"+database+"`");
				}
			}
		}

		[TestMethod]
		public void WithoutTheVariables_TheCheckIsInconclusive_NeverAPass() {
			//The gate passes the variables (docs/port-notes/S2-2.md, section 6); a run without them must not count as the check.
			if(GetVariable(VARIABLE_SERVER)!="" || GetVariable(VARIABLE_CAPI)!="" || GetVariable(VARIABLE_CNG)!="") {
				Assert.Inconclusive("The variables are set: the check itself runs.");
			}
			UnitTestAssertException exceptionCaught=null;
			try {
				ReadSetup(out _,out _,out _,out _);
			}
			catch(UnitTestAssertException ex) {
				exceptionCaught=ex;
			}
			Assert.IsInstanceOfType(exceptionCaught,typeof(AssertInconclusiveException));
		}
	}
}
