// In-process fake POP3 and SMTP servers on 127.0.0.1 for the email tests: plain text or TLS with a self-signed certificate made for each run.
// They speak just enough of RFC 1939 (POP3, with CAPA, UIDL and TOP) and RFC 5321 (SMTP, with AUTH LOGIN and PLAIN, STARTTLS or TLS from the
// first byte) for the program's
// clients, record every command line, and never reach the network. Licensed under the GNU General Public License, version 2 or later. See LICENSE
// in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace LibraryUpdateTests {
	///<summary>A self-signed server certificate for "localhost" and 127.0.0.1, made once per test run, with a private key SChannel can use.</summary>
	public static class TestCertificates {
		private static readonly Lazy<X509Certificate2> _lazyServer=new Lazy<X509Certificate2>(Create);

		public static X509Certificate2 Server {
			get { return _lazyServer.Value; }
		}

		///<summary>True for the test certificate only: what the program's test seams use in place of a CA's signature.</summary>
		public static bool IsTestCertificate(object sender,X509Certificate certificate,X509Chain chain,SslPolicyErrors sslPolicyErrors) {
			return certificate!=null && certificate.GetCertHashString()==Server.GetCertHashString();
		}

		private static X509Certificate2 Create() {
			using(RSA rsa=RSA.Create(2048)) {
				CertificateRequest certificateRequest=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				SubjectAlternativeNameBuilder subjectAlternativeNameBuilder=new SubjectAlternativeNameBuilder();
				subjectAlternativeNameBuilder.AddDnsName("localhost");
				subjectAlternativeNameBuilder.AddIpAddress(IPAddress.Loopback);
				certificateRequest.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
				certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
					new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },false));//server authentication
				using(X509Certificate2 x509Certificate2=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(2))) {
					//An ephemeral key can't be used by SChannel on .NET Framework; a PFX round trip puts it in a key container for this process.
					return new X509Certificate2(x509Certificate2.Export(X509ContentType.Pfx,""),"",X509KeyStorageFlags.Exportable);
				}
			}
		}
	}

	///<summary>The base of the fake servers: one listener on a free port of 127.0.0.1, one connection at a time on a background thread.</summary>
	public abstract class FakeServerBase:IDisposable {
		private readonly TcpListener _tcpListener;
		private readonly Thread _thread;
		private volatile bool _isStopping;
		///<summary>Every line the clients sent, in order (passwords too: they are synthetic).</summary>
		public List<string> ListCommands=new List<string>();
		public int Port { get; }
		///<summary>An error inside the server thread, for the test to report.</summary>
		public Exception ServerError;

		///<summary>port 0 takes a free port.</summary>
		protected FakeServerBase(int port=0) {
			_tcpListener=new TcpListener(IPAddress.Loopback,port);
			_tcpListener.Start();
			Port=((IPEndPoint)_tcpListener.LocalEndpoint).Port;
			_thread=new Thread(AcceptLoop);
			_thread.IsBackground=true;
			_thread.Start();
		}

		private void AcceptLoop() {
			while(!_isStopping) {
				TcpClient tcpClient;
				try {
					tcpClient=_tcpListener.AcceptTcpClient();
				}
				catch(Exception) {
					return;//Stopped.
				}
				using(tcpClient) {
					try {
						tcpClient.ReceiveTimeout=20000;
						tcpClient.SendTimeout=20000;
						Serve(new Connection(tcpClient.GetStream()));
					}
					catch(IOException) {
						//The client went away.
					}
					catch(AuthenticationException) {
						//The client refused the certificate.
					}
					catch(Exception ex) {
						ServerError=ex;
					}
				}
			}
		}

		protected abstract void Serve(Connection connection);

		protected void Record(string line) {
			lock(ListCommands) {
				ListCommands.Add(line);
			}
		}

		public List<string> GetCommands() {
			lock(ListCommands) {
				return new List<string>(ListCommands);
			}
		}

		public void Dispose() {
			_isStopping=true;
			_tcpListener.Stop();
			_thread.Join(5000);
		}

		///<summary>A line-based connection that can switch to TLS.</summary>
		protected class Connection {
			public Stream Stream;

			public Connection(Stream stream) {
				Stream=stream;
			}

			public void StartTls() {
				SslStream sslStream=new SslStream(Stream,false);
				sslStream.AuthenticateAsServer(TestCertificates.Server,false,SslProtocols.Tls12,false);
				Stream=sslStream;
			}

			public void Write(string text) {
				WriteBytes(Encoding.ASCII.GetBytes(text));
			}

			public void WriteBytes(byte[] arrayBytes) {
				Stream.Write(arrayBytes,0,arrayBytes.Length);
				Stream.Flush();
			}

			///<summary>One line without its CRLF, as Latin-1 (byte for byte); null when the client closed the connection.</summary>
			public string ReadLine() {
				MemoryStream memoryStream=new MemoryStream();
				while(true) {
					int b=Stream.ReadByte();
					if(b<0) {
						return memoryStream.Length==0 ? null : Encoding.GetEncoding(28591).GetString(memoryStream.ToArray());
					}
					if(b=='\n') {
						byte[] arrayLine=memoryStream.ToArray();
						int length=arrayLine.Length;
						if(length>0 && arrayLine[length-1]=='\r') {
							length--;
						}
						return Encoding.GetEncoding(28591).GetString(arrayLine,0,length);
					}
					memoryStream.WriteByte((byte)b);
				}
			}
		}

		///<summary>The message as a multi-line response sends it: every line that starts with a dot gets another (RFC 1939 3, RFC 5321 4.5.2), the
		///message ends with CRLF, then the terminating dot.</summary>
		public static byte[] DotStuff(byte[] arrayMessage) {
			MemoryStream memoryStream=new MemoryStream();
			bool isLineStart=true;
			foreach(byte b in arrayMessage) {
				if(isLineStart && b=='.') {
					memoryStream.WriteByte((byte)'.');
				}
				memoryStream.WriteByte(b);
				isLineStart=(b=='\n');
			}
			if(!isLineStart) {
				memoryStream.WriteByte((byte)'\r');
				memoryStream.WriteByte((byte)'\n');
			}
			memoryStream.Write(new byte[] { (byte)'.',(byte)'\r',(byte)'\n' },0,3);
			return memoryStream.ToArray();
		}
	}

	///<summary>A POP3 mailbox for one user: the messages with their UIDs, never deleted (a DELE is recorded and answered, but nothing is removed).
	///</summary>
	public class FakePop3Server:FakeServerBase {
		private readonly bool _isTls;
		private readonly string _userName;
		private readonly string _password;
		///<summary>UID and raw bytes of each message, message 1 first.</summary>
		public List<KeyValuePair<string,byte[]>> ListMessages=new List<KeyValuePair<string,byte[]>>();
		///<summary>Offer SASL (PLAIN, CRAM-MD5) and APOP (a timestamp in the greeting), which this server then refuses: a client that takes them
		///fails. The program signs in with USER and PASS only, as OpenPop did.</summary>
		public bool IsSaslAndApopOffered;

		public FakePop3Server(string userName,string password,bool isTls) {
			_userName=userName;
			_password=password;
			_isTls=isTls;
		}

		public void AddMessage(string uid,byte[] arrayMessage) {
			lock(ListMessages) {
				ListMessages.Add(new KeyValuePair<string,byte[]>(uid,arrayMessage));
			}
		}

		protected override void Serve(Connection connection) {
			if(_isTls) {
				connection.StartTls();
			}
			connection.Write(IsSaslAndApopOffered ? "+OK Fake POP3 server ready <1896.697170952@pop.example.test>\r\n" : "+OK Fake POP3 server ready\r\n");
			string userGiven=null;
			bool isSignedIn=false;
			List<KeyValuePair<string,byte[]>> listMessages;
			lock(ListMessages) {
				listMessages=new List<KeyValuePair<string,byte[]>>(ListMessages);
			}
			while(true) {
				string line=connection.ReadLine();
				if(line==null) {
					return;
				}
				Record(line);
				string[] arrayParts=line.Split(' ');
				string command=arrayParts[0].ToUpperInvariant();
				if(command=="CAPA") {
					connection.Write("+OK Capability list follows\r\nUSER\r\nUIDL\r\nTOP\r\n"+(IsSaslAndApopOffered ? "SASL PLAIN CRAM-MD5\r\n" : "")+".\r\n");
				}
				else if(command=="USER") {
					userGiven=line.Substring(5);
					connection.Write("+OK\r\n");
				}
				else if(command=="PASS") {
					if(userGiven==_userName && line.Substring(5)==_password) {
						isSignedIn=true;
						connection.Write("+OK Signed in\r\n");
					}
					else {
						connection.Write("-ERR [AUTH] Invalid user name or password\r\n");
					}
				}
				else if(command=="QUIT") {
					connection.Write("+OK Bye\r\n");
					return;
				}
				else if(command=="NOOP") {
					connection.Write("+OK\r\n");
				}
				else if(!isSignedIn) {
					connection.Write("-ERR Not signed in\r\n");
				}
				else if(command=="STAT") {
					int size=0;
					listMessages.ForEach(x => size+=x.Value.Length);
					connection.Write("+OK "+listMessages.Count+" "+size+"\r\n");
				}
				else if(command=="LIST" && arrayParts.Length==1) {
					StringBuilder stringBuilder=new StringBuilder("+OK\r\n");
					for(int i=0;i<listMessages.Count;i++) {
						stringBuilder.Append((i+1)+" "+listMessages[i].Value.Length+"\r\n");
					}
					connection.Write(stringBuilder.Append(".\r\n").ToString());
				}
				else if(command=="UIDL" && arrayParts.Length==1) {
					StringBuilder stringBuilder=new StringBuilder("+OK\r\n");
					for(int i=0;i<listMessages.Count;i++) {
						stringBuilder.Append((i+1)+" "+listMessages[i].Key+"\r\n");
					}
					connection.Write(stringBuilder.Append(".\r\n").ToString());
				}
				else if((command=="RETR" || command=="TOP" || command=="DELE") && arrayParts.Length>=2 && int.TryParse(arrayParts[1],out int number)
					&& number>=1 && number<=listMessages.Count)
				{
					byte[] arrayMessage=listMessages[number-1].Value;
					if(command=="DELE") {
						connection.Write("+OK Marked\r\n");
						continue;
					}
					if(command=="TOP") {
						arrayMessage=GetHeaderPart(arrayMessage);
					}
					connection.Write("+OK "+arrayMessage.Length+" octets\r\n");
					connection.WriteBytes(DotStuff(arrayMessage));
				}
				else {
					connection.Write("-ERR Unknown command\r\n");
				}
			}
		}

		///<summary>The header and the blank line after it (TOP n 0).</summary>
		private static byte[] GetHeaderPart(byte[] arrayMessage) {
			for(int i=0;i+3<arrayMessage.Length;i++) {
				if(arrayMessage[i]=='\r' && arrayMessage[i+1]=='\n' && arrayMessage[i+2]=='\r' && arrayMessage[i+3]=='\n') {
					byte[] arrayHeader=new byte[i+4];
					Array.Copy(arrayMessage,arrayHeader,i+4);
					return arrayHeader;
				}
			}
			return arrayMessage;
		}
	}

	///<summary>An SMTP server that accepts every message from a signed-in client and keeps it.</summary>
	public class FakeSmtpServer:FakeServerBase {
		private readonly string _userName;
		private readonly string _password;
		private readonly bool _isStartTlsOffered;
		private readonly bool _isImplicitTls;

		public class Received {
			public string MailFrom;
			public List<string> ListRcptTo=new List<string>();
			///<summary>The DATA as the client sent it, without the dot-stuffing and the terminating dot.</summary>
			public byte[] Data;
			public bool WasTls;
		}

		public List<Received> ListReceived=new List<Received>();

		///<summary>isImplicitTls: TLS from the first byte (SMTPS, as on port 465); STARTTLS is then not offered. port 0 takes a free port.</summary>
		public FakeSmtpServer(string userName,string password,bool isStartTlsOffered,bool isImplicitTls=false,int port=0) : base(port) {
			_userName=userName;
			_password=password;
			_isStartTlsOffered=isStartTlsOffered && !isImplicitTls;
			_isImplicitTls=isImplicitTls;
		}

		private static string Base64(string text) {
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
		}

		private static string FromBase64(string text) {
			try {
				return Encoding.UTF8.GetString(Convert.FromBase64String(text.Trim()));
			}
			catch(FormatException) {
				return "";
			}
		}

		protected override void Serve(Connection connection) {
			bool isTls=false;
			if(_isImplicitTls) {
				connection.StartTls();
				isTls=true;
			}
			connection.Write("220 smtp.example.test Fake ESMTP\r\n");
			bool isSignedIn=false;
			Received received=null;
			while(true) {
				string line=connection.ReadLine();
				if(line==null) {
					return;
				}
				Record(line);
				string upper=line.ToUpperInvariant();
				if(upper.StartsWith("EHLO") || upper.StartsWith("HELO")) {
					StringBuilder stringBuilder=new StringBuilder("250-smtp.example.test\r\n");
					if(_isStartTlsOffered && !isTls) {
						stringBuilder.Append("250-STARTTLS\r\n");
					}
					stringBuilder.Append("250-AUTH LOGIN PLAIN\r\n250-8BITMIME\r\n250 SIZE 10485760\r\n");
					connection.Write(stringBuilder.ToString());
				}
				else if(upper=="STARTTLS" && _isStartTlsOffered && !isTls) {
					connection.Write("220 Ready to start TLS\r\n");
					connection.StartTls();
					isTls=true;
				}
				else if(upper.StartsWith("AUTH LOGIN")) {
					string user;
					if(line.Length>"AUTH LOGIN ".Length) {
						user=FromBase64(line.Substring("AUTH LOGIN ".Length));
					}
					else {
						connection.Write("334 "+Base64("Username:")+"\r\n");
						user=FromBase64(connection.ReadLine()??"");
					}
					connection.Write("334 "+Base64("Password:")+"\r\n");
					string pass=FromBase64(connection.ReadLine()??"");
					Record("(AUTH LOGIN "+user+")");
					isSignedIn=(user==_userName && pass==_password);
					connection.Write(isSignedIn ? "235 2.7.0 Authentication successful\r\n" : "535 5.7.8 Authentication failed\r\n");
				}
				else if(upper.StartsWith("AUTH PLAIN")) {
					string response=(line.Length>"AUTH PLAIN ".Length) ? line.Substring("AUTH PLAIN ".Length) : null;
					if(response==null) {
						connection.Write("334 \r\n");
						response=connection.ReadLine()??"";
					}
					string[] arrayParts=FromBase64(response).Split('\0');
					isSignedIn=(arrayParts.Length==3 && arrayParts[1]==_userName && arrayParts[2]==_password);
					connection.Write(isSignedIn ? "235 2.7.0 Authentication successful\r\n" : "535 5.7.8 Authentication failed\r\n");
				}
				else if(upper.StartsWith("MAIL FROM:")) {
					if(!isSignedIn) {
						connection.Write("530 5.7.0 Authentication required\r\n");
						continue;
					}
					received=new Received() { MailFrom=line.Substring(10).Trim(),WasTls=isTls };
					connection.Write("250 2.1.0 OK\r\n");
				}
				else if(upper.StartsWith("RCPT TO:") && received!=null) {
					received.ListRcptTo.Add(line.Substring(8).Trim());
					connection.Write("250 2.1.5 OK\r\n");
				}
				else if(upper=="DATA" && received!=null) {
					connection.Write("354 Start mail input; end with <CRLF>.<CRLF>\r\n");
					MemoryStream memoryStream=new MemoryStream();
					Encoding latin1=Encoding.GetEncoding(28591);
					while(true) {
						string dataLine=connection.ReadLine();
						if(dataLine==null || dataLine==".") {
							break;
						}
						if(dataLine.StartsWith(".")) {
							dataLine=dataLine.Substring(1);
						}
						byte[] arrayLine=latin1.GetBytes(dataLine+"\r\n");
						memoryStream.Write(arrayLine,0,arrayLine.Length);
					}
					received.Data=memoryStream.ToArray();
					lock(ListReceived) {
						ListReceived.Add(received);
					}
					received=null;
					connection.Write("250 2.0.0 Queued\r\n");
				}
				else if(upper=="RSET") {
					received=null;
					connection.Write("250 OK\r\n");
				}
				else if(upper=="NOOP") {
					connection.Write("250 OK\r\n");
				}
				else if(upper=="QUIT") {
					connection.Write("221 2.0.0 Bye\r\n");
					return;
				}
				else {
					connection.Write("502 5.5.2 Command not implemented\r\n");
				}
			}
		}
	}
}
