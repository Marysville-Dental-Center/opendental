// What MySqlConnector 2.6.2 does with a server whose certificate is outside the office CA (docs/port-notes/S2-2.md, section 3.6; owner decision
// Q5 and the open point DB-SEC 3 of docs/specs/db-connection-security.md, section 8). Since 2.4 the driver no longer fails the TLS handshake for
// such a certificate under VerifyCA and VerifyFull: it notes the policy errors, logs in over that TLS session, and refuses the connection only
// after the server's OK packet, unless the login had a password and the server proves in that OK packet that it knows the account's stored hash
// (ServerSession.ValidateFingerprint). These tests pin the three facts that follow, with a fake server on a loopback port of the test that has
// its own self-signed certificate (an attacker in the middle presents whatever certificate it likes, whatever server the office runs):
// 1. A password login is refused, but only after the driver has sent the account's mysql_native_password challenge response, built on the
//    fake server's scramble, which a dictionary attack can test offline. 2.3.7 failed the TLS handshake first and sent nothing.
// 2. A login without a password (the client certificate as the only credential) is refused too, and sends no secret.
// 3. A password login is accepted by a server that holds only the account's stored hash (the mysql.user value, from a copy of the table or a
//    backup of the data directory), whatever its certificate.
// The real-server tests (DevDbTlsServerTests.AnotherCa_IsRefused_SoTheServerIsReallyVerified, TlsCertificateLoginTests) show the refusal of
// another CA; they cannot show 3, because MariaDB 10.11 sends no such proof. If a later driver verifies the server before it logs in, or no
// longer accepts the proof, 1 or 3 fails here: update the port note, the spec's DB-SEC 3 and these tests together.
// Nothing here connects anywhere but the fake server on 127.0.0.1. Licensed under the GNU General Public License, version 2 or later. See LICENSE
// in the repository root.
using System;
using System.Collections.Generic;
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
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class UnverifiedServerTests {
		private const string USER="opmsd";
		private string _folder;
		private string _pathOfficeCa;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_pathOfficeCa=TestHelpers.WriteCaPem(_folder);
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>The program's own string for its database account on a server over the network (VerifyFull with the office CA), with this
		///password, pointed at the fake server. sslMode VerifyCA is the read-only and reporting servers' mode with a CA preference (S1).</summary>
		private string GetConnectionString(FakeServer fakeServer,string password,MySqlSslMode sslMode) {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,TestHelpers.NETWORK_HOST,"s22_unverified",USER,
				password,sslCa:_pathOfficeCa,certificateThumbprint:"");
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
			Assert.AreEqual(MySqlSslMode.VerifyFull,builder.SslMode,"The program's string for a server over the network.");
			//The fake server stands where the office server would be; the CA stays the office CA (this computer may have one of its own).
			builder.Server="127.0.0.1";
			builder.Port=(uint)fakeServer.Port;
			builder.SslCa=_pathOfficeCa;
			builder.SslMode=sslMode;
			builder.Pooling=false;
			builder.ConnectionTimeout=10;
			return builder.ConnectionString;
		}

		///<summary>The connection's refusal: a MySqlException 1042 with an AuthenticationException that names the chain error.</summary>
		private static void AssertRefusedForTheCertificate(Exception ex,string label) {
			MySqlException mySqlException=null;
			AuthenticationException authenticationException=null;
			for(Exception exceptionInner=ex;exceptionInner!=null;exceptionInner=exceptionInner.InnerException) {
				mySqlException=mySqlException??(exceptionInner as MySqlException);
				authenticationException=authenticationException??(exceptionInner as AuthenticationException);
			}
			Assert.IsNotNull(mySqlException,label+": "+ex);
			Assert.AreEqual(1042,mySqlException.Number,label+": "+ex);
			Assert.IsNotNull(authenticationException,label+": "+ex);
			StringAssert.Contains(authenticationException.Message,"RemoteCertificateChainErrors",label);
			Assert.IsFalse(ex.ToString().Contains(TestHelpers.SECRET),label+": the password in a message.");
		}

		[TestMethod]
		public void PasswordLogin_ServerOutsideTheOfficeCa_IsRefused_ButOnlyAfterTheChallengeResponseWasSent() {
			foreach(MySqlSslMode sslMode in new[] { MySqlSslMode.VerifyFull,MySqlSslMode.VerifyCA }) {
				using(FakeServer fakeServer=new FakeServer(storedHash:null)) {
					using(MySqlConnection mySqlConnection=new MySqlConnection(GetConnectionString(fakeServer,TestHelpers.SECRET,sslMode))) {
						Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
						AssertRefusedForTheCertificate(ex,sslMode.ToString());
					}
					fakeServer.Wait();
					Assert.IsTrue(fakeServer.IsTls,sslMode+": the login went over TLS.");
					Assert.AreEqual(USER,fakeServer.UserReceived,sslMode+": the login reached the server whose certificate was refused.");
					//What a server in the middle keeps: the challenge response to its own scramble. Testing a password against it needs nothing
					//else, which is an offline dictionary attack. A wrong guess does not match.
					Assert.AreEqual(20,fakeServer.ArrayAuthResponse.Length,sslMode+": MySqlConnector 2.6.2 sends the challenge response before it refuses "
						+"the certificate (docs/port-notes/S2-2.md, section 3.6). If nothing was sent, the driver verifies first again: update the port note "
						+"and DB-SEC 3.");
					CollectionAssert.AreEqual(GetNativePasswordResponse(TestHelpers.SECRET,fakeServer.ArrayScramble),fakeServer.ArrayAuthResponse,
						sslMode+": the response checks against the password.");
					CollectionAssert.AreNotEqual(GetNativePasswordResponse("Synthetic-Wrong-Guess",fakeServer.ArrayScramble),fakeServer.ArrayAuthResponse,
						sslMode.ToString());
					CollectionAssert.AreEqual(new[] { FakeServer.CLOSED },fakeServer.ListCommandsAfterOk,sslMode+": after the OK packet the client closed "
						+"the connection without a command.");
				}
			}
		}

		[TestMethod]
		public void CertificateOnlyLogin_ServerOutsideTheOfficeCa_IsRefused_AndNoSecretIsSent() {
			//The driver decides on the empty password alone (CertificateErrorNoPassword), so the client certificate, which would have to be in
			//LocalMachine\My for the program's string, is left out here.
			using(FakeServer fakeServer=new FakeServer(storedHash:null)) {
				using(MySqlConnection mySqlConnection=new MySqlConnection(GetConnectionString(fakeServer,"",MySqlSslMode.VerifyFull))) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedForTheCertificate(ex,"No password");
				}
				fakeServer.Wait();
				Assert.AreEqual(USER,fakeServer.UserReceived);
				Assert.AreEqual(0,fakeServer.ArrayAuthResponse.Length,"Without a password the login carries no challenge response.");
				CollectionAssert.AreEqual(new[] { FakeServer.CLOSED },fakeServer.ListCommandsAfterOk);
			}
		}

		[TestMethod]
		public void PasswordLogin_ServerThatHasOnlyTheStoredHash_IsAccepted_WithACertificateOutsideTheOfficeCa() {
			//The open point DB-SEC 3: owner decision Q5 does not hold for this case with MySqlConnector 2.6.2, and there is no option to turn it off.
			//When the decision is made, this test changes with it (a refusal here, if the program stops sending passwords over the network or
			//verifies the server itself).
			using(FakeServer fakeServer=new FakeServer(storedHash:GetStoredHash(TestHelpers.SECRET))) {
				using(MySqlConnection mySqlConnection=new MySqlConnection(GetConnectionString(fakeServer,TestHelpers.SECRET,MySqlSslMode.VerifyFull))) {
					mySqlConnection.Open();
					Assert.AreEqual(System.Data.ConnectionState.Open,mySqlConnection.State,"MySqlConnector 2.6.2 accepts the server once it proves the "
						+"stored hash (docs/port-notes/S2-2.md, section 3.6). If it is refused now, the driver changed: update the port note and DB-SEC 3.");
					mySqlConnection.Close();
				}
				fakeServer.Wait();
				//With the stored hash, the challenge response also gives SHA1(password): what a mysql_native_password login to the real server needs.
				byte[] arrayStoredHash=GetStoredHashBytes(GetStoredHash(TestHelpers.SECRET));
				byte[] arrayMask;
				byte[] arrayPasswordSha1;
				using(SHA1 sha1=SHA1.Create()) {
					arrayMask=sha1.ComputeHash(fakeServer.ArrayScramble.Concat(arrayStoredHash).ToArray());
					arrayPasswordSha1=sha1.ComputeHash(Encoding.UTF8.GetBytes(TestHelpers.SECRET));
				}
				CollectionAssert.AreEqual(arrayPasswordSha1,fakeServer.ArrayAuthResponse.Select((x,i) => (byte)(x^arrayMask[i])).ToArray());
				//The session ran: the driver set its character set (SET NAMES) over the fake server's TLS session and ended with COM_QUIT.
				Assert.IsTrue(fakeServer.ListCommandsAfterOk.Count>=2,string.Join(" | ",fakeServer.ListCommandsAfterOk));
				StringAssert.StartsWith(fakeServer.ListCommandsAfterOk[0],"SET NAMES",string.Join(" | ",fakeServer.ListCommandsAfterOk));
				Assert.AreEqual(FakeServer.QUIT,fakeServer.ListCommandsAfterOk[fakeServer.ListCommandsAfterOk.Count-1]);
			}
		}

		[TestMethod]
		public void PasswordLogin_ServerWithAWrongProof_IsRefused() {
			//The proof is checked: a server with another account's hash is refused as without a proof.
			using(FakeServer fakeServer=new FakeServer(storedHash:GetStoredHash("Synthetic-Other-Password"))) {
				using(MySqlConnection mySqlConnection=new MySqlConnection(GetConnectionString(fakeServer,TestHelpers.SECRET,MySqlSslMode.VerifyFull))) {
					Exception ex=TestHelpers.AssertThrows<Exception>(() => mySqlConnection.Open());
					AssertRefusedForTheCertificate(ex,"Wrong proof");
				}
				fakeServer.Wait();
				CollectionAssert.AreEqual(new[] { FakeServer.CLOSED },fakeServer.ListCommandsAfterOk);
			}
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

		///<summary>A fake MySQL server for one connection on a free port of 127.0.0.1, with a self-signed certificate of its own (outside the office
		///CA). It sends the initial handshake (MariaDB 10.11's version string, mysql_native_password, TLS offered), takes the client's TLS request
		///and the TLS handshake, records the login (user and challenge response), and answers with an OK packet whatever the login was. With a
		///stored hash it adds MariaDB 11.4's proof to the OK packet's information: 0x01 and the hex of SHA256(SHA1(SHA1(password)) + scramble +
		///SHA256(certificate)), the proof MySqlConnector's ValidateFingerprint checks. Then it answers each query with OK and records what the
		///client sent until COM_QUIT or the end of the connection.</summary>
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
			public string UserReceived;
			public byte[] ArrayAuthResponse;
			public bool IsTls;
			public const string CLOSED="(closed)";
			public const string QUIT="(COM_QUIT)";
			///<summary>What the client sent after the login's OK packet: each query's text (answered with OK), then QUIT, or CLOSED when the client
			///closed or reset the connection instead.</summary>
			public readonly List<string> ListCommandsAfterOk=new List<string>();
			public Exception ExceptionServer;

			public FakeServer(string storedHash) {
				_storedHash=storedHash;
				//Printable bytes, as a server's scramble is.
				using(RandomNumberGenerator randomNumberGenerator=RandomNumberGenerator.Create()) {
					randomNumberGenerator.GetBytes(ArrayScramble);
				}
				for(int i=0;i<ArrayScramble.Length;i++) {
					ArrayScramble[i]=(byte)(0x21+ArrayScramble[i]%0x5e);
				}
				_x509Certificate2=CreateCertificate();
				_tcpListener=new TcpListener(IPAddress.Loopback,0);
				_tcpListener.Start();
				Port=((IPEndPoint)_tcpListener.LocalEndpoint).Port;
				_thread=new Thread(Serve) { IsBackground=true,Name="UnverifiedServerTests.FakeServer" };
				_thread.Start();
			}

			///<summary>A self-signed server certificate for db.example.test and 127.0.0.1, with a private key SChannel can use (a PFX round trip).
			///</summary>
			private static X509Certificate2 CreateCertificate() {
				using(RSA rsa=RSA.Create(2048)) {
					CertificateRequest certificateRequest=new CertificateRequest("CN="+TestHelpers.NETWORK_HOST,rsa,HashAlgorithmName.SHA256,
						RSASignaturePadding.Pkcs1);
					SubjectAlternativeNameBuilder subjectAlternativeNameBuilder=new SubjectAlternativeNameBuilder();
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
						SslStream sslStream=new SslStream(stream,false);
						sslStream.AuthenticateAsServer(_x509Certificate2,false,SslProtocols.Tls12,false);
						IsTls=true;
						stream=sslStream;
						byte[] arrayLogin=ReadPacket(stream,out sequence);
						Assert.IsNotNull(arrayLogin,"No login after the TLS handshake.");
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
				catch(SocketException) when(UserReceived==null && !IsTls) {
					//Dispose stopped the listener before a client came: the test failed earlier and says why.
				}
				catch(ObjectDisposedException) when(UserReceived==null && !IsTls) {
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
				ArrayAuthResponse=new byte[length];
				Array.Copy(arrayLogin,offset+1,ArrayAuthResponse,0,length);
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
