// A fake SSH server on 127.0.0.1 that goes as far as the key exchange (RFC 4253 and RFC 8731: version exchange, KEXINIT, curve25519-sha256 with
// an Ed25519 host key that signs the exchange hash) and records every message the client sends. It never encrypts and never authenticates: a
// client that trusts the host key sends SSH_MSG_NEWKEYS, and the server then closes the connection. So a test can tell, with a real SSH.NET
// client, whether the client accepted the server's key, and that a client that refused it never got as far as authentication.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace SshHostKeyTests {
	///<summary>Accepts connections one after the other until disposed. Each connection's client messages are recorded in ListConnections.</summary>
	public class FakeSshServer:IDisposable {
		public const byte MSG_DISCONNECT=1;
		public const byte MSG_SERVICE_REQUEST=5;
		public const byte MSG_KEXINIT=20;
		public const byte MSG_NEWKEYS=21;
		public const byte MSG_KEX_ECDH_INIT=30;
		public const byte MSG_KEX_ECDH_REPLY=31;
		public const byte MSG_USERAUTH_REQUEST=50;
		private const string SERVER_VERSION="SSH-2.0-FakeSshServer_1.0";
		private readonly TcpListener _tcpListener;
		private readonly Thread _thread;
		private readonly Ed25519PrivateKeyParameters _ed25519PrivateKeyParameters;
		private readonly SecureRandom _secureRandom=new SecureRandom();
		private readonly object _lock=new object();
		private readonly List<FakeSshConnection> _listConnections=new List<FakeSshConnection>();
		private volatile bool _isDisposed;
		public readonly int Port;
		///<summary>The host key blob: string "ssh-ed25519", string the 32-byte public key.</summary>
		public readonly byte[] HostKeyBlob;

		public FakeSshServer() {
			_ed25519PrivateKeyParameters=new Ed25519PrivateKeyParameters(_secureRandom);
			SshWriter sshWriter=new SshWriter();
			sshWriter.WriteString("ssh-ed25519");
			sshWriter.WriteString(_ed25519PrivateKeyParameters.GeneratePublicKey().GetEncoded());
			HostKeyBlob=sshWriter.ToArray();
			_tcpListener=new TcpListener(IPAddress.Loopback,0);
			_tcpListener.Start();
			Port=((IPEndPoint)_tcpListener.LocalEndpoint).Port;
			_thread=new Thread(Run);
			_thread.IsBackground=true;
			_thread.Start();
		}

		///<summary>The SHA-256 fingerprint of the host key as SSH.NET and OpenSSH print it: base64 without padding.</summary>
		public string FingerprintSha256 {
			get {
				using(SHA256 sha256=SHA256.Create()) {
					return Convert.ToBase64String(sha256.ComputeHash(HostKeyBlob)).TrimEnd('=');
				}
			}
		}

		///<summary>The connections handled so far, finished or not.</summary>
		public List<FakeSshConnection> ListConnections {
			get {
				lock(_lock) {
					return _listConnections.ToList();
				}
			}
		}

		///<summary>Waits until count connections have ended (the client closed, or the server closed after NEWKEYS). Returns them.</summary>
		public List<FakeSshConnection> WaitForConnections(int count) {
			DateTime dateTimeEnd=DateTime.Now.AddSeconds(20);
			while(DateTime.Now<dateTimeEnd) {
				List<FakeSshConnection> listConnections=ListConnections;
				if(listConnections.Count>=count && listConnections.Take(count).All(x => x.IsEnded)) {
					return listConnections;
				}
				Thread.Sleep(20);
			}
			throw new TimeoutException("The fake SSH server did not see "+count+" connection(s) end.");
		}

		private void Run() {
			while(!_isDisposed) {
				TcpClient tcpClient;
				try {
					tcpClient=_tcpListener.AcceptTcpClient();
				}
				catch(Exception) {
					return;//Stopped.
				}
				FakeSshConnection fakeSshConnection=new FakeSshConnection();
				lock(_lock) {
					_listConnections.Add(fakeSshConnection);
				}
				try {
					using(tcpClient)
					using(NetworkStream networkStream=tcpClient.GetStream())
					{
						networkStream.ReadTimeout=15000;
						Serve(networkStream,fakeSshConnection);
					}
				}
				catch(Exception ex) {
					fakeSshConnection.Error=ex;
				}
				finally {
					fakeSshConnection.IsEnded=true;
				}
			}
		}

		private void Serve(NetworkStream networkStream,FakeSshConnection fakeSshConnection) {
			byte[] arrayVersion=Encoding.ASCII.GetBytes(SERVER_VERSION+"\r\n");
			networkStream.Write(arrayVersion,0,arrayVersion.Length);
			byte[] arrayKexInitServer=MakeKexInit();
			WritePacket(networkStream,arrayKexInitServer);
			string clientVersion=ReadLine(networkStream);
			fakeSshConnection.ClientVersion=clientVersion;
			byte[] arrayKexInitClient=null;
			while(true) {
				byte[] arrayPayload=ReadPacket(networkStream);
				if(arrayPayload==null) {
					return;//The client closed the connection.
				}
				fakeSshConnection.AddMessage(arrayPayload[0]);
				switch(arrayPayload[0]) {
					case MSG_KEXINIT:
						arrayKexInitClient=arrayPayload;
						break;
					case MSG_KEX_ECDH_INIT:
						SshReader sshReader=new SshReader(arrayPayload,1);
						byte[] arrayClientPublic=sshReader.ReadString();
						WritePacket(networkStream,MakeEcdhReply(clientVersion,arrayKexInitClient,arrayKexInitServer,arrayClientPublic));
						break;
					case MSG_NEWKEYS:
						//The client trusted the host key. This server has no encryption, so it ends here.
						return;
					case MSG_DISCONNECT:
						return;
				}
			}
		}

		///<summary>KEXINIT offering only curve25519-sha256, an Ed25519 host key, aes128-ctr, hmac-sha2-256 and no compression.</summary>
		private byte[] MakeKexInit() {
			SshWriter sshWriter=new SshWriter();
			sshWriter.WriteByte(MSG_KEXINIT);
			byte[] arrayCookie=new byte[16];
			_secureRandom.NextBytes(arrayCookie);
			sshWriter.WriteBytes(arrayCookie);
			sshWriter.WriteString("curve25519-sha256");
			sshWriter.WriteString("ssh-ed25519");
			sshWriter.WriteString("aes128-ctr");
			sshWriter.WriteString("aes128-ctr");
			sshWriter.WriteString("hmac-sha2-256");
			sshWriter.WriteString("hmac-sha2-256");
			sshWriter.WriteString("none");
			sshWriter.WriteString("none");
			sshWriter.WriteString("");
			sshWriter.WriteString("");
			sshWriter.WriteByte(0);//first_kex_packet_follows
			sshWriter.WriteUInt32(0);
			return sshWriter.ToArray();
		}

		///<summary>SSH_MSG_KEX_ECDH_REPLY (RFC 5656, RFC 8731): the host key, the server's ephemeral X25519 key and the host key's signature of the
		///exchange hash H=SHA256(V_C,V_S,I_C,I_S,K_S,Q_C,Q_S,K).</summary>
		private byte[] MakeEcdhReply(string clientVersion,byte[] arrayKexInitClient,byte[] arrayKexInitServer,byte[] arrayClientPublic) {
			X25519KeyPairGenerator x25519KeyPairGenerator=new X25519KeyPairGenerator();
			x25519KeyPairGenerator.Init(new X25519KeyGenerationParameters(_secureRandom));
			AsymmetricCipherKeyPair asymmetricCipherKeyPair=x25519KeyPairGenerator.GenerateKeyPair();
			byte[] arrayServerPublic=((X25519PublicKeyParameters)asymmetricCipherKeyPair.Public).GetEncoded();
			X25519Agreement x25519Agreement=new X25519Agreement();
			x25519Agreement.Init(asymmetricCipherKeyPair.Private);
			byte[] arraySharedSecret=new byte[x25519Agreement.AgreementSize];
			x25519Agreement.CalculateAgreement(new X25519PublicKeyParameters(arrayClientPublic,0),arraySharedSecret,0);
			SshWriter sshWriterHash=new SshWriter();
			sshWriterHash.WriteString(clientVersion);
			sshWriterHash.WriteString(SERVER_VERSION);
			sshWriterHash.WriteString(arrayKexInitClient);
			sshWriterHash.WriteString(arrayKexInitServer);
			sshWriterHash.WriteString(HostKeyBlob);
			sshWriterHash.WriteString(arrayClientPublic);
			sshWriterHash.WriteString(arrayServerPublic);
			sshWriterHash.WriteMpint(arraySharedSecret);
			byte[] arrayExchangeHash;
			using(SHA256 sha256=SHA256.Create()) {
				arrayExchangeHash=sha256.ComputeHash(sshWriterHash.ToArray());
			}
			Ed25519Signer ed25519Signer=new Ed25519Signer();
			ed25519Signer.Init(true,_ed25519PrivateKeyParameters);
			ed25519Signer.BlockUpdate(arrayExchangeHash,0,arrayExchangeHash.Length);
			byte[] arraySignature=ed25519Signer.GenerateSignature();
			SshWriter sshWriterSignature=new SshWriter();
			sshWriterSignature.WriteString("ssh-ed25519");
			sshWriterSignature.WriteString(arraySignature);
			SshWriter sshWriter=new SshWriter();
			sshWriter.WriteByte(MSG_KEX_ECDH_REPLY);
			sshWriter.WriteString(HostKeyBlob);
			sshWriter.WriteString(arrayServerPublic);
			sshWriter.WriteString(sshWriterSignature.ToArray());
			return sshWriter.ToArray();
		}

		///<summary>An unencrypted binary packet (RFC 4253 6): length, padding length, payload, at least 4 bytes of padding, a multiple of 8.</summary>
		private void WritePacket(Stream stream,byte[] arrayPayload) {
			int lengthPadding=8-((5+arrayPayload.Length)%8);
			if(lengthPadding<4) {
				lengthPadding+=8;
			}
			byte[] arrayPadding=new byte[lengthPadding];
			_secureRandom.NextBytes(arrayPadding);
			SshWriter sshWriter=new SshWriter();
			sshWriter.WriteUInt32((uint)(1+arrayPayload.Length+lengthPadding));
			sshWriter.WriteByte((byte)lengthPadding);
			sshWriter.WriteBytes(arrayPayload);
			sshWriter.WriteBytes(arrayPadding);
			byte[] arrayPacket=sshWriter.ToArray();
			stream.Write(arrayPacket,0,arrayPacket.Length);
			stream.Flush();
		}

		///<summary>The payload of the next unencrypted packet, or null when the client closed the connection.</summary>
		private static byte[] ReadPacket(Stream stream) {
			byte[] arrayLength=ReadExactly(stream,4);
			if(arrayLength==null) {
				return null;
			}
			int length=(arrayLength[0]<<24)|(arrayLength[1]<<16)|(arrayLength[2]<<8)|arrayLength[3];
			if(length<5 || length>256*1024) {
				throw new InvalidDataException("Packet length "+length+".");
			}
			byte[] arrayPacket=ReadExactly(stream,length);
			if(arrayPacket==null) {
				return null;
			}
			int lengthPadding=arrayPacket[0];
			byte[] arrayPayload=new byte[length-1-lengthPadding];
			Array.Copy(arrayPacket,1,arrayPayload,0,arrayPayload.Length);
			return arrayPayload;
		}

		private static byte[] ReadExactly(Stream stream,int count) {
			byte[] arrayBytes=new byte[count];
			int offset=0;
			while(offset<count) {
				int read;
				try {
					read=stream.Read(arrayBytes,offset,count-offset);
				}
				catch(IOException) {
					return null;//Reset by the client.
				}
				if(read==0) {
					return null;
				}
				offset+=read;
			}
			return arrayBytes;
		}

		///<summary>The client's identification line, without CR LF.</summary>
		private static string ReadLine(Stream stream) {
			List<byte> listBytes=new List<byte>();
			while(listBytes.Count<255) {
				int value=stream.ReadByte();
				if(value<0) {
					throw new EndOfStreamException("No identification string.");
				}
				if(value=='\n') {
					break;
				}
				listBytes.Add((byte)value);
			}
			return Encoding.ASCII.GetString(listBytes.ToArray()).TrimEnd('\r');
		}

		public void Dispose() {
			_isDisposed=true;
			_tcpListener.Stop();
			_thread.Join(5000);
		}
	}

	///<summary>One client connection of the fake server.</summary>
	public class FakeSshConnection {
		private readonly object _lock=new object();
		private readonly List<byte> _listMessages=new List<byte>();
		public string ClientVersion;
		public Exception Error;
		public volatile bool IsEnded;

		public void AddMessage(byte messageNumber) {
			lock(_lock) {
				_listMessages.Add(messageNumber);
			}
		}

		///<summary>The message numbers the client sent, in order.</summary>
		public List<byte> ListMessages {
			get {
				lock(_lock) {
					return _listMessages.ToList();
				}
			}
		}
	}

	///<summary>SSH wire encoding (RFC 4251 5): byte, uint32, string, mpint.</summary>
	public class SshWriter {
		private readonly MemoryStream _memoryStream=new MemoryStream();

		public void WriteByte(byte value) {
			_memoryStream.WriteByte(value);
		}

		public void WriteBytes(byte[] arrayBytes) {
			_memoryStream.Write(arrayBytes,0,arrayBytes.Length);
		}

		public void WriteUInt32(uint value) {
			WriteBytes(new byte[] { (byte)(value>>24),(byte)(value>>16),(byte)(value>>8),(byte)value });
		}

		public void WriteString(byte[] arrayBytes) {
			WriteUInt32((uint)arrayBytes.Length);
			WriteBytes(arrayBytes);
		}

		public void WriteString(string text) {
			WriteString(Encoding.ASCII.GetBytes(text));
		}

		///<summary>An unsigned big-endian number as an mpint: no leading zero bytes, and one zero byte in front when the high bit is set.</summary>
		public void WriteMpint(byte[] arrayUnsignedBigEndian) {
			int start=0;
			while(start<arrayUnsignedBigEndian.Length && arrayUnsignedBigEndian[start]==0) {
				start++;
			}
			byte[] arrayMagnitude=arrayUnsignedBigEndian.Skip(start).ToArray();
			if(arrayMagnitude.Length>0 && (arrayMagnitude[0]&0x80)!=0) {
				arrayMagnitude=new byte[] { 0 }.Concat(arrayMagnitude).ToArray();
			}
			WriteString(arrayMagnitude);
		}

		public byte[] ToArray() {
			return _memoryStream.ToArray();
		}
	}

	///<summary>Reads SSH wire strings from a payload.</summary>
	public class SshReader {
		private readonly byte[] _arrayBytes;
		private int _offset;

		public SshReader(byte[] arrayBytes,int offset) {
			_arrayBytes=arrayBytes;
			_offset=offset;
		}

		public byte[] ReadString() {
			int length=(_arrayBytes[_offset]<<24)|(_arrayBytes[_offset+1]<<16)|(_arrayBytes[_offset+2]<<8)|_arrayBytes[_offset+3];
			_offset+=4;
			byte[] arrayValue=new byte[length];
			Array.Copy(_arrayBytes,_offset,arrayValue,0,length);
			_offset+=length;
			return arrayValue;
		}
	}
}
