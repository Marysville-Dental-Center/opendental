// SFTP with SSH.NET 2026 in place of SharpSSH: remote path handling (the A to Z folders, the Denti-Cal server field), the host key check every
// SSH connection gets (SshHostKeys: a server's key may not change while the program runs), and a connection to something that is not an SSH
// server failing within its timeout. Keys are generated for each run with BouncyCastle; no SSH server, network or key file is needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.Eclaims;
using OpenPmsDentalCloud;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;

namespace LibraryUpdateTests {
	[TestClass]
	public class SftpTests {
		[TestInitialize]
		public void TestInitialize() {
			SshHostKeys.ForgetAll();
		}

		[TestCleanup]
		public void TestCleanup() {
			SshHostKeys.ForgetAll();
		}

		///<summary>A PKCS#8 PEM private key, new for each call.</summary>
		private static string MakeEd25519Pem() {
			Ed25519KeyPairGenerator ed25519KeyPairGenerator=new Ed25519KeyPairGenerator();
			ed25519KeyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
			return ToPem(ed25519KeyPairGenerator.GenerateKeyPair().Private);
		}

		private static string MakeRsaPem() {
			RsaKeyPairGenerator rsaKeyPairGenerator=new RsaKeyPairGenerator();
			rsaKeyPairGenerator.Init(new KeyGenerationParameters(new SecureRandom(),1024));
			return ToPem(rsaKeyPairGenerator.GenerateKeyPair().Private);
		}

		private static string ToPem(AsymmetricKeyParameter asymmetricKeyParameter) {
			byte[] arrayDer=PrivateKeyInfoFactory.CreatePrivateKeyInfo(asymmetricKeyParameter).GetEncoded();
			return "-----BEGIN PRIVATE KEY-----\n"+Convert.ToBase64String(arrayDer,Base64FormattingOptions.InsertLineBreaks)+"\n-----END PRIVATE KEY-----\n";
		}

		///<summary>What SSH.NET hands the HostKeyReceived event for each host key algorithm of the key.</summary>
		private static List<HostKeyEventArgs> GetHostKeyEvents(string pem) {
			using(MemoryStream memoryStream=new MemoryStream(Encoding.ASCII.GetBytes(pem)))
			using(PrivateKeyFile privateKeyFile=new PrivateKeyFile(memoryStream))
			{
				return privateKeyFile.HostKeyAlgorithms.Cast<KeyHostAlgorithm>().Select(x => new HostKeyEventArgs(x)).ToList();
			}
		}

		private static bool CanTrust(string host,int port,HostKeyEventArgs hostKeyEventArgs) {
			return SshHostKeys.CanTrust(host,port,SshHostKeys.GetKeyType(hostKeyEventArgs.HostKey),hostKeyEventArgs.FingerPrintSHA256);
		}

		[TestMethod]
		public void DirectoryPrefixes_WalkTheRemotePathFromTheTop() {
			CollectionAssert.AreEqual(new[] { "/OpenDentImages","/OpenDentImages/A","/OpenDentImages/A/Abbott123" },
				SftpExtension.GetDirectoryPrefixes("/OpenDentImages/A/Abbott123"));
			CollectionAssert.AreEqual(new[] { "images","images/B" },SftpExtension.GetDirectoryPrefixes("images/B"));
			CollectionAssert.AreEqual(new[] { "/a","/a/b" },SftpExtension.GetDirectoryPrefixes("/a//b/"),"Doubled and trailing slashes are skipped.");
			CollectionAssert.AreEqual(new string[0],SftpExtension.GetDirectoryPrefixes(""));
			CollectionAssert.AreEqual(new string[0],SftpExtension.GetDirectoryPrefixes("/"));
			CollectionAssert.AreEqual(new[] { "/Folder With Spaces","/Folder With Spaces/é" },SftpExtension.GetDirectoryPrefixes("/Folder With Spaces/é"));
		}

		[TestMethod]
		public void DentiCalServer_ComesFromTheClientProgramField() {
			DentiCal.GetRemoteHostAndPort("",out string host,out int port);
			Assert.AreEqual("sftp.mft.oxisaas.com",host);
			Assert.AreEqual(2222,port);
			DentiCal.GetRemoteHostAndPort(null,out host,out port);
			Assert.AreEqual("sftp.mft.oxisaas.com",host);
			DentiCal.GetRemoteHostAndPort("sftp.example.test",out host,out port);
			Assert.AreEqual("sftp.example.test",host);
			Assert.AreEqual(2222,port);
			DentiCal.GetRemoteHostAndPort("192.0.2.10:22",out host,out port);
			Assert.AreEqual("192.0.2.10",host);
			Assert.AreEqual(22,port);
			DentiCal.GetRemoteHostAndPort("sftp.example.test:notaport",out host,out port);
			Assert.AreEqual(2222,port,"An invalid port falls back to Denti-Cal's.");
		}

		[TestMethod]
		public void HostKey_IsTrustedFirst_ThenOnlyTheSameKey() {
			List<HostKeyEventArgs> listKeyA=GetHostKeyEvents(MakeEd25519Pem());
			List<HostKeyEventArgs> listKeyB=GetHostKeyEvents(MakeEd25519Pem());
			Assert.AreEqual("ssh-ed25519",SshHostKeys.GetKeyType(listKeyA[0].HostKey));
			Assert.AreNotEqual(listKeyA[0].FingerPrintSHA256,listKeyB[0].FingerPrintSHA256);
			Assert.IsTrue(CanTrust("sftp.example.test",22,listKeyA[0]),"The first key of a server is trusted, as before.");
			Assert.IsTrue(CanTrust("sftp.example.test",22,listKeyA[0]),"The same key again.");
			Assert.IsTrue(CanTrust("SFTP.Example.Test ",22,listKeyA[0]),"Host names compare without case.");
			Assert.IsNull(SshHostKeys.GetRefusalMessage("sftp.example.test",22));
			Assert.IsFalse(CanTrust("sftp.example.test",22,listKeyB[0]),"Another key of the same type from the same server is refused.");
			string message=SshHostKeys.GetRefusalMessage("sftp.example.test",22);
			StringAssert.Contains(message,"SHA256:"+listKeyA[0].FingerPrintSHA256);
			StringAssert.Contains(message,"SHA256:"+listKeyB[0].FingerPrintSHA256);
			Assert.IsFalse(CanTrust("sftp.example.test",22,listKeyB[0]),"Still refused.");
			Assert.IsTrue(CanTrust("sftp.example.test",2222,listKeyB[0]),"Another port is another server.");
			Assert.IsTrue(CanTrust("sftp2.example.test",22,listKeyB[0]),"Another host is another server.");
			Assert.IsTrue(CanTrust("sftp.example.test",22,listKeyA[0]),"The remembered key is still good.");
			Assert.IsNull(SshHostKeys.GetRefusalMessage("sftp.example.test",22),"A good key clears the refusal.");
			Assert.IsFalse(SshHostKeys.CanTrust("sftp3.example.test",22,"ssh-ed25519",""),"No fingerprint, no trust.");
		}

		[TestMethod]
		public void HostKey_OneRsaKeyUnderEverySignatureAlgorithm_IsOneKey() {
			//SSH.NET offers an RSA key as rsa-sha2-512, rsa-sha2-256 and ssh-rsa; whichever the server picks, it is the same key.
			List<HostKeyEventArgs> listRsa=GetHostKeyEvents(MakeRsaPem());
			Assert.IsTrue(listRsa.Count>=2,"Algorithms: "+string.Join(", ",listRsa.Select(x => x.HostKeyName)));
			Assert.IsTrue(listRsa.All(x => SshHostKeys.GetKeyType(x.HostKey)=="ssh-rsa"));
			Assert.AreEqual(1,listRsa.Select(x => x.FingerPrintSHA256).Distinct().Count());
			foreach(HostKeyEventArgs hostKeyEventArgs in listRsa) {
				Assert.IsTrue(CanTrust("sftp.example.test",22,hostKeyEventArgs),hostKeyEventArgs.HostKeyName);
			}
			//A server that also has an Ed25519 key may present it next time: a key of another type is remembered on its own.
			List<HostKeyEventArgs> listEd25519=GetHostKeyEvents(MakeEd25519Pem());
			Assert.IsTrue(CanTrust("sftp.example.test",22,listEd25519[0]));
			Assert.IsFalse(CanTrust("sftp.example.test",22,GetHostKeyEvents(MakeRsaPem())[0]),"A new RSA key is still refused.");
		}

		[TestMethod]
		public void KeyType_IsReadFromTheKeyBlob() {
			Assert.AreEqual("",SshHostKeys.GetKeyType(null));
			Assert.AreEqual("",SshHostKeys.GetKeyType(new byte[] { 0,0,0 }));
			Assert.AreEqual("",SshHostKeys.GetKeyType(new byte[] { 0,0,0,20,(byte)'s' }),"A length past the end.");
			Assert.AreEqual("",SshHostKeys.GetKeyType(new byte[] { 0xff,0xff,0xff,0xff,(byte)'s' }));
			Assert.AreEqual("ssh-rsa",SshHostKeys.GetKeyType(new byte[] { 0,0,0,7,(byte)'s',(byte)'s',(byte)'h',(byte)'-',(byte)'r',(byte)'s',(byte)'a',0,0 }));
		}

		[TestMethod]
		public void Refusal_IsExplainedOnlyForThatServer() {
			List<HostKeyEventArgs> listKeyA=GetHostKeyEvents(MakeEd25519Pem());
			List<HostKeyEventArgs> listKeyB=GetHostKeyEvents(MakeEd25519Pem());
			Assert.IsTrue(CanTrust("sftp.example.test",22,listKeyA[0]));
			Assert.IsFalse(CanTrust("sftp.example.test",22,listKeyB[0]));
			SshConnectionException exNegotiation=new SshConnectionException("Key exchange negotiation failed.");
			Exception exExplained=SshHostKeys.Explain(exNegotiation,"sftp.example.test",22);
			Assert.AreNotSame(exNegotiation,exExplained);
			StringAssert.Contains(exExplained.Message,"changed while the program was running");
			Assert.AreSame(exNegotiation,exExplained.InnerException);
			Assert.AreSame(exNegotiation,SshHostKeys.Explain(exNegotiation,"other.example.test",22));
			Exception exOther=new SocketException();
			Assert.AreSame(exOther,SshHostKeys.Explain(exOther,"sftp.example.test",22),"Only SSH connection failures.");
		}

		[TestMethod]
		public void Connect_ToSomethingThatIsNotAnSshServer_FailsWithinItsTimeout() {
			TcpListener tcpListener=new TcpListener(IPAddress.Loopback,0);
			tcpListener.Start();
			int port=((IPEndPoint)tcpListener.LocalEndpoint).Port;
			Thread thread=new Thread(() => {
				try {
					using(TcpClient tcpClient=tcpListener.AcceptTcpClient())
					using(NetworkStream networkStream=tcpClient.GetStream())
					{
						//A web server's answer: no SSH identification string ever comes.
						byte[] arrayBytes=Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n");
						networkStream.Write(arrayBytes,0,arrayBytes.Length);
						Thread.Sleep(5000);
					}
				}
				catch(Exception) {
				}
			});
			thread.IsBackground=true;
			thread.Start();
			Stopwatch stopwatch=Stopwatch.StartNew();
			try {
				using(SftpSession sftpSession=new SftpSession("127.0.0.1",port,"user","synthetic-password",TimeSpan.FromSeconds(2))) {
					sftpSession.Connect();
				}
				Assert.Fail("Connect should fail.");
			}
			catch(AssertFailedException) {
				throw;
			}
			catch(Exception ex) {
				Assert.IsTrue(ex is SshException || ex is SocketException,ex.GetType().Name+": "+ex.Message);
			}
			finally {
				tcpListener.Stop();
			}
			Assert.IsTrue(stopwatch.Elapsed<TimeSpan.FromSeconds(10),"Took "+stopwatch.Elapsed);
			Assert.IsNull(SshHostKeys.GetRefusalMessage("127.0.0.1",port),"No host key was seen, so none was refused.");
		}
	}
}
