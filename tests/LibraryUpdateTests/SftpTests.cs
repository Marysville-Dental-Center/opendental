// SFTP with SSH.NET 2026 in place of SharpSSH: remote path handling (the A to Z folders, the Denti-Cal server field), the host key type read
// from the key blob, and a connection to something that is not an SSH server failing within its timeout. The remembered host keys themselves
// (SshHostKeyCheck and the sshhostkey table) are tested in tests/SshHostKeyTests. Keys are generated for each run with BouncyCastle; no SSH
// server, network or key file is needed.
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
using OpenPmsDentalStorage;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;

namespace LibraryUpdateTests {
	[TestClass]
	public class SftpTests {
		///<summary>A host key store in memory, for connections that never get as far as a host key.</summary>
		private class MemoryHostKeyStore:ISshHostKeyStore {
			public readonly Dictionary<string,SshHostKeyFingerprint> DictKeys=new Dictionary<string,SshHostKeyFingerprint>();
			public int CountRefusals;

			public SshHostKeyFingerprint GetStoredKey(string host,int port) {
				DictKeys.TryGetValue(host+":"+port,out SshHostKeyFingerprint fingerprint);
				return fingerprint;
			}

			public SshHostKeyFingerprint StoreFirstKey(string host,int port,SshHostKeyFingerprint fingerprint) {
				if(!DictKeys.ContainsKey(host+":"+port)) {
					DictKeys[host+":"+port]=fingerprint;
				}
				return DictKeys[host+":"+port];
			}

			public void RecordRefusedKey(string host,int port,SshHostKeyFingerprint fingerprintStored,SshHostKeyFingerprint fingerprintRefused) {
				CountRefusals++;
			}
		}

		private static string MakeRsaPem() {
			RsaKeyPairGenerator rsaKeyPairGenerator=new RsaKeyPairGenerator();
			rsaKeyPairGenerator.Init(new KeyGenerationParameters(new SecureRandom(),1024));
			AsymmetricKeyParameter asymmetricKeyParameter=rsaKeyPairGenerator.GenerateKeyPair().Private;
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
		public void KeyType_IsReadFromTheKeyBlob() {
			Assert.AreEqual("",SshHostKeyCheck.GetKeyType(null));
			Assert.AreEqual("",SshHostKeyCheck.GetKeyType(new byte[] { 0,0,0 }));
			Assert.AreEqual("",SshHostKeyCheck.GetKeyType(new byte[] { 0,0,0,20,(byte)'s' }),"A length past the end.");
			Assert.AreEqual("",SshHostKeyCheck.GetKeyType(new byte[] { 0xff,0xff,0xff,0xff,(byte)'s' }));
			Assert.AreEqual("ssh-rsa",SshHostKeyCheck.GetKeyType(new byte[] { 0,0,0,7,(byte)'s',(byte)'s',(byte)'h',(byte)'-',(byte)'r',(byte)'s',(byte)'a',0,0 }));
		}

		[TestMethod]
		public void HostKey_OneRsaKeyUnderEverySignatureAlgorithm_IsOneKey() {
			//SSH.NET offers an RSA key as rsa-sha2-512, rsa-sha2-256 and ssh-rsa; whichever the server picks, it is the same stored key.
			List<HostKeyEventArgs> listRsa=GetHostKeyEvents(MakeRsaPem());
			Assert.IsTrue(listRsa.Count>=2,"Algorithms: "+string.Join(", ",listRsa.Select(x => x.HostKeyName)));
			SshHostKeyFingerprint fingerprintFirst=SshHostKeyFingerprint.FromHostKeyEvent(listRsa[0]);
			Assert.AreEqual("ssh-rsa",fingerprintFirst.KeyType);
			foreach(HostKeyEventArgs hostKeyEventArgs in listRsa) {
				Assert.IsTrue(fingerprintFirst.Matches(SshHostKeyFingerprint.FromHostKeyEvent(hostKeyEventArgs)),hostKeyEventArgs.HostKeyName);
			}
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
			MemoryHostKeyStore memoryHostKeyStore=new MemoryHostKeyStore();
			Stopwatch stopwatch=Stopwatch.StartNew();
			try {
				using(SftpSession sftpSession=new SftpSession("127.0.0.1",port,"user","synthetic-password",memoryHostKeyStore,TimeSpan.FromSeconds(2))) {
					sftpSession.Connect();
				}
				Assert.Fail("Connect should fail.");
			}
			catch(AssertFailedException) {
				throw;
			}
			catch(Exception ex) {
				Assert.IsTrue(ex is SshException || ex is SocketException,ex.GetType().Name+": "+ex.Message);
				Assert.IsFalse(ex is SshHostKeyRefusedException,"No host key was seen, so none was refused: "+ex.Message);
			}
			finally {
				tcpListener.Stop();
			}
			Assert.IsTrue(stopwatch.Elapsed<TimeSpan.FromSeconds(10),"Took "+stopwatch.Elapsed);
			Assert.AreEqual(0,memoryHostKeyStore.DictKeys.Count,"A connection that failed stores no key.");
			Assert.AreEqual(0,memoryHostKeyStore.CountRefusals);
		}
	}
}
