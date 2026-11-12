// What the program does with a database server whose certificate is outside the office CA, since DB-SEC 3 was decided as (c)
// (docs/specs/db-connection-security.md section 8, docs/port-notes/S2-2.md section 3.6). MySqlConnector 2.6.2 with SslCa logs in first and
// decides on the certificate after the server's OK packet, and accepts a server that proves the account's stored hash with any certificate
// (ServerSession.ValidateFingerprint). So the program never hands SslCa to the driver for a server over the network: the driver gets
// SslMode=Required and the program's own check (OfficeCaServerVerifier) as the connection's certificate callback, which fails the TLS handshake
// before the login (HandshakeResponse41) is sent. These tests connect through the program's own factories (DbConnectionSecurity.NewMySqlConnection
// and NewMySqlConnectionWithoutPooling, with the program's own strings) to a fake server on a loopback port of the test, reached by the name
// "localhost." (with its trailing dot: a name on the network for the connection rule, which Windows resolves to this computer), and show:
// 1. A password login (VerifyFull, and a read-only server's VerifyCA) to a server with its own self-signed certificate is refused in the TLS
//    handshake: the fake server never receives a login, so no challenge response reaches it. (With the driver's own check it received the
//    account's challenge response, which a dictionary attack can test offline.)
// 2. A fake server that holds the account's stored hash is refused the same way: it never gets the chance to prove it. (With the driver's own
//    check it was accepted with any certificate.)
// 3. A login with the client certificate alone is refused the same way.
// 4. A server with a certificate of the office CA that names another host is refused under VerifyFull, and accepted under VerifyCA, which
//    checks the chain only (S1).
// 5. A server with a certificate of the office CA that names the host is accepted, and the fake server completes a real login (it reads the
//    user and the challenge response, answers OK, and receives the driver's SET NAMES and COM_QUIT).
// If a later driver or change of the program sends the login before the certificate is decided, 1 to 3 fail here: update the port note and the
// spec's DB-SEC 3 with them. Nothing here connects anywhere but the fake server on 127.0.0.1; every certificate is made in memory.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class UnverifiedServerTests {
		private const string USER="opmsd";
		///<summary>A name for this computer that the connection rule reads as the network (it is not "localhost" exactly), so the program's
		///string for a server over the network is built and checked for it, and Windows' resolver answers it with loopback addresses only.
		///</summary>
		private const string HOST_DOT="localhost.";
		private string _folder;
		private TestCertificateAuthority _caOffice;
		private string _pathOfficeCa;
		private string _officeCaPathOld;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_caOffice=TestCertificateAuthority.CreateRoot("CN=Synthetic Office CA "+Guid.NewGuid().ToString("N"));
			_pathOfficeCa=_caOffice.WritePem(_folder,"office-ca.pem");
			_officeCaPathOld=DataConnection.OfficeCaPath;
			DataConnection.OfficeCaPath=_pathOfficeCa;
		}

		[TestCleanup]
		public void CleanUp() {
			DataConnection.OfficeCaPath=_officeCaPathOld;
			_caOffice.Dispose();
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>Inconclusive unless "localhost." resolves to loopback addresses only, so a test can never connect anywhere but this computer.
		///</summary>
		private static void AssertHostDotIsThisComputer() {
			Assert.IsFalse(DbConnectionSecurity.IsLoopbackConnectorServer(HOST_DOT),"The rule reads "+HOST_DOT+" as a name on the network.");
			IPAddress[] arrayIpAddresses;
			try {
				arrayIpAddresses=Dns.GetHostAddresses(HOST_DOT);
			}
			catch(SocketException ex) {
				Assert.Inconclusive(HOST_DOT+" does not resolve here ("+ex.Message+"); the tests need it to reach their fake server.");
				return;
			}
			if(arrayIpAddresses.Length==0 || arrayIpAddresses.Any(x => !IPAddress.IsLoopback(x))) {
				Assert.Inconclusive(HOST_DOT+" resolves to "+string.Join(", ",arrayIpAddresses.Select(x => x.ToString()))+", not to this computer only.");
			}
		}

		///<summary>The program's string for its own account on a server over the network (BuildSimpleConnectionString: VerifyFull, the office CA
		///of DataConnection.OfficeCaPath, no client certificate), for the fake server, outside the pool so each test's fake server sees its own
		///connection end.</summary>
		private static string GetMainConnectionString(FakeServer fakeServer,string password) {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,HOST_DOT+":"+fakeServer.Port,"s22v_unverified",USER,
				password,certificateThumbprint:"");
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode,"The program's string for a server over the network.");
			builder.Pooling=false;
			builder.ConnectionTimeout=10;
			return builder.ConnectionString;
		}

		///<summary>A read-only or reporting server's string with its CA preference (BuildPreferenceServerConnectionString: VerifyCA), as the
		///program builds it; NewMySqlConnectionWithoutPooling connects it outside the pool.</summary>
		private string GetPreferenceConnectionString(FakeServer fakeServer,string password) {
			string connectionString=DataConnection.BuildPreferenceServerConnectionString(HOST_DOT+":"+fakeServer.Port,"s22v_unverified",USER,password,
				_pathOfficeCa);
			Assert.AreEqual(MySqlSslMode.VerifyCA,new MySqlConnectionStringBuilder(connectionString).SslMode);
			return connectionString;
		}

		///<summary>The refusal of a server in the TLS handshake: MySqlException 1042 ("SSL Authentication Error", which IsConnectionLost treats as
		///before) with the AuthenticationException of SslStream for the rejected certificate, and the program's check (the connection's callback)
		///says why. No secret in it.</summary>
		private static void AssertRefusedInTheHandshake(Exception ex,string label,MySqlConnection mySqlConnection,string partOfRefusal) {
			MySqlException mySqlException=null;
			AuthenticationException authenticationException=null;
			for(Exception exceptionInner=ex;exceptionInner!=null;exceptionInner=exceptionInner.InnerException) {
				mySqlException=mySqlException??(exceptionInner as MySqlException);
				authenticationException=authenticationException??(exceptionInner as AuthenticationException);
			}
			Assert.IsNotNull(mySqlException,label+": "+ex);
			Assert.AreEqual(1042,mySqlException.Number,label+": "+ex);
			Assert.IsNotNull(authenticationException,label+": "+ex);
			Assert.IsTrue(authenticationException.Message.IndexOf("remote certificate",StringComparison.OrdinalIgnoreCase)>=0,
				label+": "+authenticationException.Message);
			Assert.IsFalse(ex.ToString().Contains(TestHelpers.SECRET),label+": the password in a message.");
			Assert.IsNotNull(mySqlConnection.RemoteCertificateValidationCallback,label+": the program's check is the connection's callback.");
			OfficeCaServerVerifier verifier=(OfficeCaServerVerifier)mySqlConnection.RemoteCertificateValidationCallback.Target;
			StringAssert.Contains(verifier.LastRefusal,partOfRefusal,label);
		}

		///<summary>What the fake server saw of a refused connection: the client asked for TLS, then ended the connection without sending anything
		///over it, so nothing of the login arrived. The server's side of the TLS handshake may complete first: SslStream on .NET Framework calls
		///the certificate callback when its side of the handshake is done, and on a refusal closes the connection instead of returning it to the
		///driver.</summary>
		private static void AssertNothingOfTheLoginArrived(FakeServer fakeServer,string label) {
			fakeServer.Wait();
			Assert.IsTrue(fakeServer.IsTlsRequested,label+": the client asked for TLS.");
			Assert.IsNull(fakeServer.UserReceived,label+": no login reached the server.");
			Assert.IsNull(fakeServer.ArrayAuthResponse,label+": no challenge response reached the server.");
			if(fakeServer.IsTlsEstablished) {
				CollectionAssert.AreEqual(new[] { FakeServer.CLOSED },fakeServer.ListCommandsAfterOk,label+": the client closed the TLS session without "
					+"a login.");
			}
			else {
				Assert.IsNotNull(fakeServer.TlsFailure,label+": the TLS handshake ended.");
			}
		}

		private static X509Certificate2 IssueServerCertificate(TestCertificateAuthority ca,string dnsName) {
			return ca.Issue("CN="+dnsName,new[] { dnsName },null,withPrivateKey:true);
		}

		[TestMethod]
		public void PasswordLogin_ServerOutsideTheOfficeCa_IsRefusedInTheHandshake_BeforeAnyLogin() {
			AssertHostDotIsThisComputer();
			//VerifyFull: the program's own account (password mode, Q2's fallback, the backup to another server's typed account).
			using(FakeServer fakeServer=new FakeServer(certificate:null,storedHash:null)) {
				string connectionStringFake=GetMainConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionStringFake)) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedInTheHandshake(ex,"VerifyFull",mySqlConnection,"does not chain to the office CA");
				}
				AssertNothingOfTheLoginArrived(fakeServer,"VerifyFull");
			}
			//VerifyCA: a read-only or reporting server with its CA preference.
			using(FakeServer fakeServer=new FakeServer(certificate:null,storedHash:null)) {
				string connectionStringFake=GetPreferenceConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnectionWithoutPooling(connectionStringFake)) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedInTheHandshake(ex,"VerifyCA",mySqlConnection,"does not chain to the office CA");
				}
				AssertNothingOfTheLoginArrived(fakeServer,"VerifyCA");
			}
		}

		[TestMethod]
		public void PasswordLogin_ServerThatHasTheStoredHash_IsRefused_BeforeItCanProveIt() {
			AssertHostDotIsThisComputer();
			//MariaDB 11.4's proof (ValidateFingerprint) is in the OK packet after the login; the handshake fails before, so it is never read.
			using(FakeServer fakeServer=new FakeServer(certificate:null,storedHash:GetStoredHash(TestHelpers.SECRET))) {
				string connectionStringFake=GetMainConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionStringFake)) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedInTheHandshake(ex,"Stored hash",mySqlConnection,"does not chain to the office CA");
				}
				AssertNothingOfTheLoginArrived(fakeServer,"Stored hash");
			}
			//The same with a certificate of the office CA for another host: the proof cannot stand in for the host name either.
			using(FakeServer fakeServer=new FakeServer(IssueServerCertificate(_caOffice,"other.example.test"),GetStoredHash(TestHelpers.SECRET))) {
				string connectionStringFake=GetMainConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionStringFake)) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedInTheHandshake(ex,"Stored hash, another host",mySqlConnection,"does not name the host localhost");
				}
				AssertNothingOfTheLoginArrived(fakeServer,"Stored hash, another host");
			}
		}

		[TestMethod]
		public void CertificateOnlyLogin_ServerOutsideTheOfficeCa_IsRefusedInTheHandshake() {
			AssertHostDotIsThisComputer();
			//The program's certificate mode: no password, the client certificate as the credential. The program's string names it by thumbprint in
			//LocalMachine\My, which a test may not use, so the connection takes the same driver string and check that NewMySqlConnection gives it
			//(GetDriverConnectionString) and its client certificate, made in memory, from ProvideClientCertificatesCallback.
			using(X509Certificate2 x509Certificate2Client=_caOffice.Issue("CN=synthetic-workstations",null,null,isServerAuth:false,isClientAuth:true,
				withPrivateKey:true))
			using(FakeServer fakeServer=new FakeServer(certificate:null,storedHash:null))
			{
				OfficeCaServerVerifier verifier;
				string driverStringFake=DbConnectionSecurity.GetDriverConnectionString(GetMainConnectionString(fakeServer,""),out verifier);
				Assert.IsNotNull(verifier);
				Assert.AreEqual("",new MySqlConnectionStringBuilder(driverStringFake).Password);
				bool isClientCertificateAsked=false;
				using(MySqlConnection mySqlConnection=new MySqlConnection(driverStringFake)) {
					mySqlConnection.RemoteCertificateValidationCallback=verifier.ValidateServerCertificate;
					mySqlConnection.ProvideClientCertificatesCallback=x509CertificateCollection => {
						isClientCertificateAsked=true;
						x509CertificateCollection.Add(x509Certificate2Client);
						return new ValueTask();
					};
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedInTheHandshake(ex,"Client certificate only",mySqlConnection,"does not chain to the office CA");
				}
				Assert.IsTrue(isClientCertificateAsked,"The driver took the client certificate for the handshake.");
				StringAssert.Contains(verifier.LastRefusal,"does not chain to the office CA");
				AssertNothingOfTheLoginArrived(fakeServer,"Client certificate only");
			}
		}

		[TestMethod]
		public void ServerOfTheOfficeCaForAnotherHost_IsRefusedUnderVerifyFull_AcceptedUnderVerifyCA() {
			AssertHostDotIsThisComputer();
			using(FakeServer fakeServer=new FakeServer(IssueServerCertificate(_caOffice,"other.example.test"),storedHash:null)) {
				string connectionStringFake=GetMainConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionStringFake)) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedInTheHandshake(ex,"Another host, VerifyFull",mySqlConnection,"does not name the host localhost");
				}
				AssertNothingOfTheLoginArrived(fakeServer,"Another host, VerifyFull");
			}
			//A read-only server's VerifyCA checks the chain, not the name (S1): the login runs.
			using(FakeServer fakeServer=new FakeServer(IssueServerCertificate(_caOffice,"other.example.test"),storedHash:null)) {
				string connectionStringFake=GetPreferenceConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnectionWithoutPooling(connectionStringFake)) {
					mySqlConnection.Open();
					Assert.AreEqual(System.Data.ConnectionState.Open,mySqlConnection.State);
				}
				AssertLoginRan(fakeServer,"Another host, VerifyCA");
			}
		}

		[TestMethod]
		public void ServerOfTheOfficeCaThatNamesTheHost_IsAccepted_AndTheLoginRuns() {
			AssertHostDotIsThisComputer();
			using(FakeServer fakeServer=new FakeServer(IssueServerCertificate(_caOffice,"localhost"),storedHash:null)) {
				string connectionStringFake=GetMainConnectionString(fakeServer,TestHelpers.SECRET);
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionStringFake)) {
					Assert.AreEqual(MySqlSslMode.Required,new MySqlConnectionStringBuilder(mySqlConnection.ConnectionString).SslMode,
						"The driver's string: the program decides on the certificate.");
					mySqlConnection.Open();
					Assert.AreEqual(System.Data.ConnectionState.Open,mySqlConnection.State);
					OfficeCaServerVerifier verifier=(OfficeCaServerVerifier)mySqlConnection.RemoteCertificateValidationCallback.Target;
					Assert.AreEqual("",verifier.LastRefusal);
				}
				AssertLoginRan(fakeServer,"Names the host");
			}
		}

		///<summary>The fake server saw a whole login over TLS: the user, a 20-byte challenge response that checks against the password (and not
		///against a wrong guess), and after its OK packet the driver's SET NAMES and COM_QUIT.</summary>
		private static void AssertLoginRan(FakeServer fakeServer,string label) {
			fakeServer.Wait();
			Assert.IsTrue(fakeServer.IsTlsEstablished,label+": TLS.");
			Assert.AreEqual(USER,fakeServer.UserReceived,label);
			Assert.AreEqual(20,fakeServer.ArrayAuthResponse.Length,label);
			CollectionAssert.AreEqual(GetNativePasswordResponse(TestHelpers.SECRET,fakeServer.ArrayScramble),fakeServer.ArrayAuthResponse,label);
			CollectionAssert.AreNotEqual(GetNativePasswordResponse("Synthetic-Wrong-Guess",fakeServer.ArrayScramble),fakeServer.ArrayAuthResponse,label);
			string commands=string.Join(" | ",fakeServer.ListCommandsAfterOk);
			Assert.IsTrue(fakeServer.ListCommandsAfterOk.Count>=2,label+": "+commands);
			StringAssert.StartsWith(fakeServer.ListCommandsAfterOk[0],"SET NAMES",label+": "+commands);
			Assert.AreEqual(FakeServer.QUIT,fakeServer.ListCommandsAfterOk[fakeServer.ListCommandsAfterOk.Count-1],label+": "+commands);
		}

		///<summary>The value mysql.user keeps for a mysql_native_password account, as PASSWORD() gives it: "*" and the hex of SHA1(SHA1(password)).
		///</summary>
		private static string GetStoredHash(string password) {
			using(SHA1 sha1=SHA1.Create()) {
				byte[] arrayHash=sha1.ComputeHash(sha1.ComputeHash(Encoding.UTF8.GetBytes(password)));
				return "*"+BitConverter.ToString(arrayHash).Replace("-","");
			}
		}

		///<summary>The 20 bytes of a stored hash ("*" and 40 hex digits).</summary>
		private static byte[] GetStoredHashBytes(string storedHash) {
			byte[] arrayHash=new byte[20];
			for(int i=0;i<20;i++) {
				arrayHash[i]=Convert.ToByte(storedHash.Substring(1+2*i,2),16);
			}
			return arrayHash;
		}

		///<summary>The mysql_native_password challenge response: SHA1(password) XOR SHA1(scramble + SHA1(SHA1(password))).</summary>
		private static byte[] GetNativePasswordResponse(string password,byte[] arrayScramble) {
			using(SHA1 sha1=SHA1.Create()) {
				byte[] arrayHash1=sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
				byte[] arrayHash2=sha1.ComputeHash(arrayHash1);
				byte[] arrayHash3=sha1.ComputeHash(arrayScramble.Concat(arrayHash2).ToArray());
				for(int i=0;i<arrayHash1.Length;i++) {
					arrayHash1[i]^=arrayHash3[i];
				}
				return arrayHash1;
			}
		}

		///<summary>A fake MySQL server for one connection on a free port of 127.0.0.1. It sends the initial handshake (MariaDB 10.11's version
		///string, mysql_native_password, TLS offered), takes the client's TLS request and the TLS handshake with its certificate (a self-signed one
		///of its own when none is given: outside the office CA), records the login (user and challenge response), and answers with an OK packet
		///whatever the login was. With a stored hash it adds MariaDB 11.4's proof to the OK packet's information: 0x01 and the hex of
		///SHA256(SHA1(SHA1(password)) + scramble + SHA256(certificate)), the proof MySqlConnector's ValidateFingerprint checks. Then it answers
		///each query with OK and records what the client sent until COM_QUIT or the end of the connection. A TLS handshake the client refuses is
		///recorded (TlsFailure), not an error of the fake server.</summary>
		private sealed class FakeServer:IDisposable {
			//CLIENT_LONG_PASSWORD, FOUND_ROWS, LONG_FLAG, CONNECT_WITH_DB, PROTOCOL_41, SSL, TRANSACTIONS, SECURE_CONNECTION, MULTI_STATEMENTS,
			//MULTI_RESULTS, PLUGIN_AUTH. No SESSION_TRACK, so the OK packet's information is the rest of the packet.
			private const uint CAPABILITIES=0x1|0x2|0x4|0x8|0x200|0x800|0x2000|0x8000|0x10000|0x20000|0x80000;
			private readonly TcpListener _tcpListener;
			private readonly Thread _thread;
			private readonly X509Certificate2 _x509Certificate2;
			private readonly string _storedHash;
			public readonly byte[] ArrayScramble=new byte[20];
			public readonly int Port;
			public volatile bool IsTlsRequested;
			public volatile bool IsTlsEstablished;
			public string TlsFailure;
			public string UserReceived;
			public byte[] ArrayAuthResponse;
			public const string CLOSED="(closed)";
			public const string QUIT="(COM_QUIT)";
			///<summary>What the client sent after the login's OK packet: each query's text (answered with OK), then QUIT, or CLOSED when the client
			///closed or reset the connection instead.</summary>
			public readonly List<string> ListCommandsAfterOk=new List<string>();
			public Exception ExceptionServer;

			///<summary>certificate: the server certificate with its private key (the fake server owns it and resets it at the end); null for a
			///self-signed one of its own.</summary>
			public FakeServer(X509Certificate2 certificate,string storedHash) {
				_storedHash=storedHash;
				//Printable bytes, as a server's scramble is.
				using(RandomNumberGenerator randomNumberGenerator=RandomNumberGenerator.Create()) {
					randomNumberGenerator.GetBytes(ArrayScramble);
				}
				for(int i=0;i<ArrayScramble.Length;i++) {
					ArrayScramble[i]=(byte)(0x21+ArrayScramble[i]%0x5e);
				}
				_x509Certificate2=certificate??CreateSelfSignedCertificate();
				_tcpListener=new TcpListener(IPAddress.Loopback,0);
				_tcpListener.Start();
				Port=((IPEndPoint)_tcpListener.LocalEndpoint).Port;
				_thread=new Thread(Serve) { IsBackground=true,Name="UnverifiedServerTests.FakeServer" };
				_thread.Start();
			}

			///<summary>A self-signed server certificate for localhost, db.example.test and 127.0.0.1, with a private key SChannel can use (a PFX
			///round trip).</summary>
			private static X509Certificate2 CreateSelfSignedCertificate() {
				using(RSA rsa=RSA.Create(2048)) {
					CertificateRequest certificateRequest=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
					SubjectAlternativeNameBuilder subjectAlternativeNameBuilder=new SubjectAlternativeNameBuilder();
					subjectAlternativeNameBuilder.AddDnsName("localhost");
					subjectAlternativeNameBuilder.AddDnsName(TestHelpers.NETWORK_HOST);
					subjectAlternativeNameBuilder.AddIpAddress(IPAddress.Loopback);
					certificateRequest.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
					certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
						new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },false));//server authentication
					using(X509Certificate2 x509Certificate2=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
						DateTimeOffset.UtcNow.AddDays(2)))
					{
						return new X509Certificate2(x509Certificate2.Export(X509ContentType.Pfx,""),"",X509KeyStorageFlags.Exportable);
					}
				}
			}

			///<summary>Waits for the connection's end and fails the test on the fake server's own error.</summary>
			public void Wait() {
				Assert.IsTrue(_thread.Join(20000),"The fake server did not finish.");
				Assert.IsNull(ExceptionServer,"The fake server: "+ExceptionServer);
			}

			public void Dispose() {
				_tcpListener.Stop();
				_thread.Join(5000);
				_x509Certificate2.Reset();//Removes the key container of the PFX round trip.
			}

			private void Serve() {
				try {
					using(TcpClient tcpClient=_tcpListener.AcceptTcpClient()) {
						tcpClient.ReceiveTimeout=15000;
						tcpClient.SendTimeout=15000;
						Stream stream=tcpClient.GetStream();
						WritePacket(stream,0,GetInitialHandshake());
						byte[] arraySslRequest=ReadPacket(stream,out byte sequence);
						Assert.IsNotNull(arraySslRequest,"No TLS request.");
						Assert.AreEqual(32,arraySslRequest.Length,"The TLS request.");
						Assert.AreEqual(0x800u,BitConverter.ToUInt32(arraySslRequest,0)&0x800,"The client asks for TLS.");
						IsTlsRequested=true;
						SslStream sslStream=new SslStream(stream,false);
						try {
							sslStream.AuthenticateAsServer(_x509Certificate2,false,SslProtocols.Tls12,false);
						}
						catch(Exception ex) when(ex is AuthenticationException || ex is IOException || ex is Win32Exception) {
							//The client refused the server's certificate and ended the handshake: nothing more comes.
							TlsFailure=ex.GetType().Name+": "+ex.Message;
							return;
						}
						IsTlsEstablished=true;
						stream=sslStream;
						byte[] arrayLogin;
						try {
							arrayLogin=ReadPacket(stream,out sequence);
						}
						catch(IOException) {
							arrayLogin=null;
						}
						if(arrayLogin==null) {
							ListCommandsAfterOk.Add(CLOSED);//The client closed the connection without a login.
							return;
						}
						ReadLogin(arrayLogin);
						WritePacket(stream,(byte)(sequence+1),GetOkPacket(isLogin:true));
						while(true) {
							byte[] arrayCommand;
							try {
								arrayCommand=ReadPacket(stream,out sequence);
							}
							catch(IOException) {
								arrayCommand=null;//The client reset the connection.
							}
							if(arrayCommand==null || arrayCommand.Length==0) {
								ListCommandsAfterOk.Add(CLOSED);
								break;
							}
							if(arrayCommand[0]==1) {
								ListCommandsAfterOk.Add(QUIT);
								break;
							}
							Assert.AreEqual(3,arrayCommand[0],"Only queries and COM_QUIT are expected.");
							ListCommandsAfterOk.Add(Encoding.UTF8.GetString(arrayCommand,1,arrayCommand.Length-1));
							WritePacket(stream,(byte)(sequence+1),GetOkPacket(isLogin:false));
						}
					}
				}
				catch(SocketException) when(!IsTlsRequested) {
					//Dispose stopped the listener before a client came: the test failed earlier and says why.
				}
				catch(ObjectDisposedException) when(!IsTlsRequested) {
				}
				catch(Exception ex) {
					ExceptionServer=ex;
				}
			}

			private byte[] GetInitialHandshake() {
				MemoryStream memoryStream=new MemoryStream();
				memoryStream.WriteByte(10);//protocol version
				WriteNullTerminated(memoryStream,"5.5.5-10.11.99-MariaDB-synthetic");
				memoryStream.Write(BitConverter.GetBytes(7),0,4);//connection id
				memoryStream.Write(ArrayScramble,0,8);
				memoryStream.WriteByte(0);
				memoryStream.Write(BitConverter.GetBytes((ushort)(CAPABILITIES&0xffff)),0,2);
				memoryStream.WriteByte(45);//utf8mb4_general_ci
				memoryStream.Write(BitConverter.GetBytes((ushort)2),0,2);//SERVER_STATUS_AUTOCOMMIT
				memoryStream.Write(BitConverter.GetBytes((ushort)(CAPABILITIES>>16)),0,2);
				memoryStream.WriteByte(21);//the scramble's length with its terminating zero
				memoryStream.Write(new byte[10],0,10);
				memoryStream.Write(ArrayScramble,8,12);
				memoryStream.WriteByte(0);
				WriteNullTerminated(memoryStream,"mysql_native_password");
				return memoryStream.ToArray();
			}

			///<summary>HandshakeResponse41 after the TLS request: capabilities, maximum packet size, character set and 23 zero bytes, then the user
			///(zero-terminated) and the challenge response (one length byte with SECURE_CONNECTION; a length-encoded integer under 251 is the same
			///byte).</summary>
			private void ReadLogin(byte[] arrayLogin) {
				int offset=32;
				int end=Array.IndexOf(arrayLogin,(byte)0,offset);
				Assert.IsTrue(end>=offset,"The login has no user.");
				UserReceived=Encoding.UTF8.GetString(arrayLogin,offset,end-offset);
				offset=end+1;
				int length=arrayLogin[offset];
				Assert.IsTrue(length<251 && offset+1+length<=arrayLogin.Length,"The login's challenge response.");
				byte[] arrayAuthResponse=new byte[length];
				Array.Copy(arrayLogin,offset+1,arrayAuthResponse,0,length);
				ArrayAuthResponse=arrayAuthResponse;
			}

			private byte[] GetOkPacket(bool isLogin) {
				MemoryStream memoryStream=new MemoryStream();
				memoryStream.Write(new byte[] { 0x00,0x00,0x00,0x02,0x00,0x00,0x00 },0,7);//OK, rows, insert id, status, warnings
				if(isLogin && _storedHash!=null) {
					byte[] arrayPasswordHash=GetStoredHashBytes(_storedHash);
					byte[] arrayProof;
					using(SHA256 sha256=SHA256.Create()) {
						byte[] arrayCertificateHash=sha256.ComputeHash(_x509Certificate2.RawData);
						arrayProof=sha256.ComputeHash(arrayPasswordHash.Concat(ArrayScramble).Concat(arrayCertificateHash).ToArray());
					}
					byte[] arrayInfo=Encoding.ASCII.GetBytes("\u0001"+BitConverter.ToString(arrayProof).Replace("-","").ToLowerInvariant());
					memoryStream.WriteByte((byte)arrayInfo.Length);//65, as a length-encoded string
					memoryStream.Write(arrayInfo,0,arrayInfo.Length);
				}
				return memoryStream.ToArray();
			}

			private static void WriteNullTerminated(MemoryStream memoryStream,string text) {
				byte[] arrayBytes=Encoding.ASCII.GetBytes(text);
				memoryStream.Write(arrayBytes,0,arrayBytes.Length);
				memoryStream.WriteByte(0);
			}

			private static void WritePacket(Stream stream,byte sequence,byte[] arrayPayload) {
				byte[] arrayPacket=new byte[4+arrayPayload.Length];
				arrayPacket[0]=(byte)(arrayPayload.Length&0xff);
				arrayPacket[1]=(byte)((arrayPayload.Length>>8)&0xff);
				arrayPacket[2]=(byte)((arrayPayload.Length>>16)&0xff);
				arrayPacket[3]=sequence;
				Array.Copy(arrayPayload,0,arrayPacket,4,arrayPayload.Length);
				stream.Write(arrayPacket,0,arrayPacket.Length);
				stream.Flush();
			}

			///<summary>One packet's payload; null when the client closed the connection before it.</summary>
			private static byte[] ReadPacket(Stream stream,out byte sequence) {
				sequence=0;
				byte[] arrayHeader=new byte[4];
				if(!ReadExactly(stream,arrayHeader)) {
					return null;
				}
				sequence=arrayHeader[3];
				byte[] arrayPayload=new byte[arrayHeader[0]|(arrayHeader[1]<<8)|(arrayHeader[2]<<16)];
				if(!ReadExactly(stream,arrayPayload)) {
					return null;
				}
				return arrayPayload;
			}

			private static bool ReadExactly(Stream stream,byte[] arrayBuffer) {
				int offset=0;
				while(offset<arrayBuffer.Length) {
					int count=stream.Read(arrayBuffer,offset,arrayBuffer.Length-offset);
					if(count==0) {
						return false;
					}
					offset+=count;
				}
				return true;
			}
		}
	}
}
