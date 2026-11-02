// A real SSH.NET client, through the program's own SFTP classes and the host key check, against the fake SSH server on 127.0.0.1: a changed key is
// refused during the key exchange, so the client never sends SSH_MSG_NEWKEYS and never reaches authentication (the service request and the
// user name and password come only after NEWKEYS). The stored key, or none, is accepted (NEWKEYS is sent).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalStorage;
using OpenPmsDentalStorage.Core;
using Renci.SshNet.Common;

namespace SshHostKeyTests {
	[TestClass]
	public class FakeServerTests {
		private static readonly TimeSpan _timeSpanConnect=TimeSpan.FromSeconds(10);

		///<summary>Asserts the connection went no further than the key exchange: KEXINIT and ECDH_INIT, never NEWKEYS, a service request or a user
		///authentication request.</summary>
		private static void AssertRefusedBeforeAuthentication(FakeSshConnection fakeSshConnection) {
			List<byte> listMessages=fakeSshConnection.ListMessages;
			string messages=string.Join(",",listMessages);
			Assert.IsNull(fakeSshConnection.Error,fakeSshConnection.Error?.ToString());
			CollectionAssert.Contains(listMessages,FakeSshServer.MSG_KEXINIT,messages);
			CollectionAssert.Contains(listMessages,FakeSshServer.MSG_KEX_ECDH_INIT,"The client got as far as the host key: "+messages);
			CollectionAssert.DoesNotContain(listMessages,FakeSshServer.MSG_NEWKEYS,"The client must not accept the key: "+messages);
			CollectionAssert.DoesNotContain(listMessages,FakeSshServer.MSG_SERVICE_REQUEST,messages);
			CollectionAssert.DoesNotContain(listMessages,FakeSshServer.MSG_USERAUTH_REQUEST,messages);
		}

		private static void AssertKeyAccepted(FakeSshConnection fakeSshConnection) {
			List<byte> listMessages=fakeSshConnection.ListMessages;
			Assert.IsNull(fakeSshConnection.Error,fakeSshConnection.Error?.ToString());
			CollectionAssert.Contains(listMessages,FakeSshServer.MSG_NEWKEYS,"The client accepted the key: "+string.Join(",",listMessages));
		}

		[TestMethod]
		public void ChangedKey_IsRefusedDuringTheKeyExchange_BeforeAuthentication() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				SshHostKeyFingerprint fingerprintStored=SshHostKeyFingerprint.FromHostKeyEvent(TestKeys.NewEd25519Event());
				store.DictKeys["127.0.0.1:"+fakeSshServer.Port]=fingerprintStored;
				SshHostKeyRefusedException ex;
				using(SftpSession sftpSession=new SftpSession("127.0.0.1",fakeSshServer.Port,"user","synthetic-password",store,_timeSpanConnect)) {
					ex=TestAssert.Throws<SshHostKeyRefusedException>(() => sftpSession.Connect());
				}
				AssertRefusedBeforeAuthentication(fakeSshServer.WaitForConnections(1)[0]);
				StringAssert.Contains(ex.Message,"127.0.0.1:"+fakeSshServer.Port);
				StringAssert.Contains(ex.Message,"Stored key: "+fingerprintStored);
				StringAssert.Contains(ex.Message,"The server now presents: ssh-ed25519 SHA256:"+fakeSshServer.FingerprintSha256);
				Assert.AreEqual(fakeSshServer.FingerprintSha256,ex.FingerprintPresented.Sha256,"The fingerprint SSH.NET computed is the server's.");
				Assert.AreEqual(1,store.ListRefusals.Count);
				Assert.AreEqual(0,store.CountStores);
				Assert.IsTrue(fingerprintStored.Matches(store.DictKeys["127.0.0.1:"+fakeSshServer.Port]),"The stored key stays.");
			}
		}

		[TestMethod]
		public void ChangedKeyType_IsRefusedDuringTheKeyExchange() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				//The stored key has the server's fingerprint under another type: the type alone refuses it.
				store.DictKeys["127.0.0.1:"+fakeSshServer.Port]=new SshHostKeyFingerprint("ecdsa-sha2-nistp256",fakeSshServer.FingerprintSha256);
				using(SftpSession sftpSession=new SftpSession("127.0.0.1",fakeSshServer.Port,"user","synthetic-password",store,_timeSpanConnect)) {
					SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => sftpSession.Connect());
					StringAssert.Contains(ex.Message,"its key type changed");
				}
				AssertRefusedBeforeAuthentication(fakeSshServer.WaitForConnections(1)[0]);
			}
		}

		[TestMethod]
		public void StoredKey_IsAccepted() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				store.DictKeys["127.0.0.1:"+fakeSshServer.Port]=new SshHostKeyFingerprint("ssh-ed25519",fakeSshServer.FingerprintSha256);
				using(SftpSession sftpSession=new SftpSession("127.0.0.1",fakeSshServer.Port,"user","synthetic-password",store,_timeSpanConnect)) {
					//The fake server closes after the key exchange, so the connection fails, but not for its host key.
					Exception ex=TestAssert.Throws<Exception>(() => sftpSession.Connect());
					Assert.IsNotInstanceOfType(ex,typeof(SshHostKeyRefusedException),ex.Message);
				}
				AssertKeyAccepted(fakeSshServer.WaitForConnections(1)[0]);
				Assert.AreEqual(0,store.ListRefusals.Count);
				Assert.AreEqual(0,store.CountStores);
			}
		}

		[TestMethod]
		public void FirstUse_IsAccepted_ButAConnectionThatDoesNotSignInStoresNothing() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				using(SftpSession sftpSession=new SftpSession("127.0.0.1",fakeSshServer.Port,"user","synthetic-password",store,_timeSpanConnect)) {
					Exception ex=TestAssert.Throws<Exception>(() => sftpSession.Connect());
					Assert.IsNotInstanceOfType(ex,typeof(SshHostKeyRefusedException),ex.Message);
				}
				AssertKeyAccepted(fakeSshServer.WaitForConnections(1)[0]);
				Assert.AreEqual(0,store.DictKeys.Count,"Only a connection that signed in stores the key.");
				Assert.AreEqual(0,store.ListRefusals.Count);
			}
		}

		[TestMethod]
		public void SftpStorageAndTsiPaths_RefuseAChangedKeyToo() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				store.DictKeys["127.0.0.1:"+fakeSshServer.Port]=SshHostKeyFingerprint.FromHostKeyEvent(TestKeys.NewEd25519Event());
				//The TSI connection test (port given).
				Assert.IsFalse(Sftp.IsConnectionValid("127.0.0.1","user","synthetic-password",store,fakeSshServer.Port));
				//An upload, as CloudStorage and the TSI uploads run it.
				TaskStateUpload taskStateUpload=new Sftp.Upload("127.0.0.1","user","synthetic-password",store,fakeSshServer.Port) {
					Folder="/AtoZ/A",
					FileName="synthetic.txt",
					ByteArray=new byte[] { 1,2,3 },
					HasExceptions=true,
				};
				Exception exUpload=TestAssert.Throws<Exception>(() => taskStateUpload.Execute());
				Assert.IsNotNull(SshHostKeyCheck.FindRefusal(exUpload),exUpload.ToString());
				StringAssert.Contains(exUpload.Message,"has changed");
				List<FakeSshConnection> listConnections=fakeSshServer.WaitForConnections(2);
				foreach(FakeSshConnection fakeSshConnection in listConnections) {
					AssertRefusedBeforeAuthentication(fakeSshConnection);
				}
				Assert.AreEqual(2,store.ListRefusals.Count);
			}
		}

		[TestMethod]
		public void IsConnectionValid_WithTheFlag_ThrowsTheRefusal_NamingTheServerAndBothKeys() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				SshHostKeyFingerprint fingerprintStored=SshHostKeyFingerprint.FromHostKeyEvent(TestKeys.NewEd25519Event());
				store.DictKeys["127.0.0.1:"+fakeSshServer.Port]=fingerprintStored;
				//The TSI connection test of the A/R Manager, the suspend message and FormArManager (TsiTransLogs.ValidateClinicSftpDetails).
				SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(
					() => Sftp.IsConnectionValid("127.0.0.1","user","synthetic-password",store,fakeSshServer.Port,doThrowHostKeyRefusal:true));
				AssertRefusedBeforeAuthentication(fakeSshServer.WaitForConnections(1)[0]);
				StringAssert.Contains(ex.Message,"127.0.0.1:"+fakeSshServer.Port);
				StringAssert.Contains(ex.Message,"Stored key: "+fingerprintStored);
				StringAssert.Contains(ex.Message,"The server now presents: ssh-ed25519 SHA256:"+fakeSshServer.FingerprintSha256);
				StringAssert.Contains(ex.Message,"Setup, SSH Host Keys");
				Assert.AreEqual(ex.Message,SshHostKeyCheck.GetRefusalMessage(ex),"What callers without SSH.NET read.");
				Assert.AreEqual(1,store.ListRefusals.Count,"The refusal is recorded too.");
				Assert.AreEqual(0,store.CountStores);
			}
		}

		[TestMethod]
		public void IsConnectionValid_WithTheFlag_OtherFailuresStillReturnFalse() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				MemoryHostKeyStore store=new MemoryHostKeyStore();
				//First use: the key is accepted, then the fake server closes after the key exchange, so the connection fails, not for its host key.
				Assert.IsFalse(Sftp.IsConnectionValid("127.0.0.1","user","synthetic-password",store,fakeSshServer.Port,doThrowHostKeyRefusal:true));
				//The stored key: accepted, the same failure afterwards.
				store.DictKeys["127.0.0.1:"+fakeSshServer.Port]=new SshHostKeyFingerprint("ssh-ed25519",fakeSshServer.FingerprintSha256);
				Assert.IsFalse(Sftp.IsConnectionValid("127.0.0.1","user","synthetic-password",store,fakeSshServer.Port,doThrowHostKeyRefusal:true));
				foreach(FakeSshConnection fakeSshConnection in fakeSshServer.WaitForConnections(2)) {
					AssertKeyAccepted(fakeSshConnection);
				}
				Assert.AreEqual(0,store.ListRefusals.Count);
				Assert.AreEqual(0,store.CountStores,"Only a connection that signed in stores the key.");
			}
		}
	}
}
