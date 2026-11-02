// The host key check (SshHostKeyCheck) with a store in memory and a simulated connection that raises SSH.NET's HostKeyReceived event as SSH.NET
// does: in each key exchange, before the password is sent. Real SSH.NET connections are in FakeServerTests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalStorage;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;

namespace SshHostKeyTests {
	[TestClass]
	public class HostKeyCheckTests {
		private static SshHostKeyFingerprint Fingerprint(HostKeyEventArgs hostKeyEventArgs) {
			return SshHostKeyFingerprint.FromHostKeyEvent(hostKeyEventArgs);
		}

		[TestMethod]
		public void FirstUse_IsTrusted_AndTheKeyIsStoredAfterTheConnectionSignedIn() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,keyA);
			connection.Check.Connect();
			Assert.IsTrue(connection.WasPasswordSent);
			Assert.AreEqual(1,store.CountStores);
			Assert.IsTrue(Fingerprint(keyA).Matches(store.DictKeys["sftp.example.test:22"]));
			Assert.AreEqual("ssh-ed25519",store.DictKeys["sftp.example.test:22"].KeyType);
			Assert.AreEqual(0,store.ListRefusals.Count);
			Assert.AreEqual(0,connection.CountDisconnects);
		}

		[TestMethod]
		public void SameKey_Connects_AndStoresNothingMore() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			store.DictKeys["sftp.example.test:22"]=Fingerprint(keyA);
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,keyA);
			connection.Check.Connect();
			connection.Check.Connect();
			Assert.AreEqual(2,connection.CountConnects);
			Assert.AreEqual(2,store.CountReads,"Every connect reads the stored key again (no cache).");
			Assert.AreEqual(0,store.CountStores);
			Assert.AreEqual(0,store.ListRefusals.Count);
		}

		[TestMethod]
		public void ChangedFingerprint_IsRefusedBeforeThePassword_RecordedAndExplained() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			HostKeyEventArgs keyB=TestKeys.NewEd25519Event();
			store.DictKeys["sftp.example.test:2222"]=Fingerprint(keyA);
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",2222,store,keyB);
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => connection.Check.Connect());
			Assert.IsFalse(connection.WasPasswordSent,"The key exchange ends before the password.");
			Assert.IsInstanceOfType(ex,typeof(SshConnectionException),"Callers that report SSH connection errors report it the same way.");
			StringAssert.Contains(ex.Message,"sftp.example.test:2222");
			StringAssert.Contains(ex.Message,"its fingerprint changed");
			StringAssert.Contains(ex.Message,"Stored key: ssh-ed25519 SHA256:"+keyA.FingerPrintSHA256);
			StringAssert.Contains(ex.Message,"The server now presents: ssh-ed25519 SHA256:"+keyB.FingerPrintSHA256);
			StringAssert.Contains(ex.Message,"refused before the user name and password were sent");
			StringAssert.Contains(ex.Message,"Setup, SSH Host Keys");
			Assert.AreEqual("sftp.example.test",ex.Host);
			Assert.AreEqual(2222,ex.Port);
			Assert.IsTrue(Fingerprint(keyA).Matches(ex.FingerprintStored));
			Assert.IsTrue(Fingerprint(keyB).Matches(ex.FingerprintPresented));
			Assert.IsTrue(Fingerprint(keyA).Matches(store.DictKeys["sftp.example.test:2222"]),"The stored key stays.");
			Assert.AreEqual(1,store.ListRefusals.Count);
			StringAssert.Contains(store.ListRefusals[0],keyB.FingerPrintSHA256);
			Assert.AreEqual(0,store.CountStores);
			//Still refused the next time.
			TestAssert.Throws<SshHostKeyRefusedException>(() => connection.Check.Connect());
			Assert.AreEqual(2,store.ListRefusals.Count,"Each refusal is handed to the store, which decides what to log.");
		}

		[TestMethod]
		public void ChangedKeyType_IsRefusedLikeAChangedKey() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyEd25519=TestKeys.NewEd25519Event();
			HostKeyEventArgs keyEcdsa=TestKeys.GetHostKeyEvents(TestKeys.MakeEcdsaPem())[0];
			Assert.AreEqual("ecdsa-sha2-nistp256",SshHostKeyCheck.GetKeyType(keyEcdsa.HostKey));
			new SimulatedConnection("sftp.example.test",22,store,keyEd25519).Check.Connect();
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,keyEcdsa);
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => connection.Check.Connect());
			StringAssert.Contains(ex.Message,"its key type changed");
			StringAssert.Contains(ex.Message,"ssh-ed25519 SHA256:"+keyEd25519.FingerPrintSHA256);
			StringAssert.Contains(ex.Message,"ecdsa-sha2-nistp256 SHA256:"+keyEcdsa.FingerPrintSHA256);
			Assert.IsFalse(connection.WasPasswordSent);
			//The type alone is enough: the stored fingerprint under another type is refused.
			SshHostKeyCheck check=new SshHostKeyCheck("sftp.example.test",22,store,() => { },() => { });
			check.Connect();
			Assert.IsFalse(check.CanTrust(new SshHostKeyFingerprint("ecdsa-sha2-nistp256",keyEd25519.FingerPrintSHA256)));
			Assert.IsTrue(check.CanTrust(Fingerprint(keyEd25519)));
		}

		[TestMethod]
		public void OneRsaKeyUnderEverySignatureAlgorithm_IsOneKey() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			List<HostKeyEventArgs> listRsa=TestKeys.GetHostKeyEvents(TestKeys.MakeRsaPem());
			Assert.IsTrue(listRsa.Count>=2,"Algorithms: "+string.Join(", ",listRsa.Select(x => x.HostKeyName)));
			new SimulatedConnection("sftp.example.test",22,store,listRsa[0]).Check.Connect();
			Assert.AreEqual("ssh-rsa",store.DictKeys["sftp.example.test:22"].KeyType);
			foreach(HostKeyEventArgs hostKeyEventArgs in listRsa) {
				new SimulatedConnection("sftp.example.test",22,store,hostKeyEventArgs).Check.Connect();
			}
			Assert.AreEqual(1,store.CountStores);
			Assert.AreEqual(0,store.ListRefusals.Count);
		}

		[TestMethod]
		public void FailedSignIn_StoresNothing() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,keyA);
			connection.IsPasswordAccepted=false;
			TestAssert.Throws<SshAuthenticationException>(() => connection.Check.Connect(),"Not a host key refusal: SSH.NET's own error.");
			Assert.AreEqual(0,store.DictKeys.Count,"Only a successful connection stores a key.");
			Assert.AreEqual(0,store.CountStores);
			Assert.AreEqual(0,store.ListRefusals.Count);
			//The next connection is still a first use.
			HostKeyEventArgs keyB=TestKeys.NewEd25519Event();
			new SimulatedConnection("sftp.example.test",22,store,keyB).Check.Connect();
			Assert.IsTrue(Fingerprint(keyB).Matches(store.DictKeys["sftp.example.test:22"]));
		}

		[TestMethod]
		public void StoreThatCannotBeRead_RefusesBeforeConnecting() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			store.DoFailReading=true;
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,TestKeys.NewEd25519Event());
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => connection.Check.Connect());
			Assert.AreEqual(0,connection.CountConnects,"Nothing is sent to the server.");
			StringAssert.Contains(ex.Message,"sftp.example.test:22");
			StringAssert.Contains(ex.Message,"could not be read");
			StringAssert.Contains(ex.Message,"Synthetic read failure.");
		}

		[TestMethod]
		public void StoreThatCannotBeWritten_DisconnectsAndRefuses() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			store.DoFailStoring=true;
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,TestKeys.NewEd25519Event());
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => connection.Check.Connect());
			Assert.AreEqual(1,connection.CountDisconnects,"No connection runs on a key the program has not remembered.");
			StringAssert.Contains(ex.Message,"could not be stored");
		}

		[TestMethod]
		public void AnotherConnectionStoredADifferentFirstKey_DisconnectsAndRefuses() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			HostKeyEventArgs keyOther=TestKeys.NewEd25519Event();
			store.FingerprintStoredByOther=Fingerprint(keyOther);
			SimulatedConnection connection=new SimulatedConnection("sftp.example.test",22,store,keyA);
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => connection.Check.Connect());
			Assert.AreEqual(1,connection.CountDisconnects);
			StringAssert.Contains(ex.Message,"Another connection stored the SSH host key of sftp.example.test:22");
			StringAssert.Contains(ex.Message,keyOther.FingerPrintSHA256);
			StringAssert.Contains(ex.Message,keyA.FingerPrintSHA256);
			Assert.AreEqual(1,store.ListRefusals.Count);
			Assert.IsTrue(Fingerprint(keyOther).Matches(store.DictKeys["sftp.example.test:22"]));
			//The same key from both connections is no conflict.
			MemoryHostKeyStore storeSame=new MemoryHostKeyStore();
			storeSame.FingerprintStoredByOther=Fingerprint(keyA);
			new SimulatedConnection("sftp.example.test",22,storeSame,keyA).Check.Connect();
			Assert.AreEqual(0,storeSame.ListRefusals.Count);
		}

		[TestMethod]
		public void Rekey_MustPresentTheSameKey() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			HostKeyEventArgs keyB=TestKeys.NewEd25519Event();
			//First use: a re-key with another key is refused, and nothing is stored.
			TestAssert.Throws<SshHostKeyRefusedException>(() => new SimulatedConnection("sftp.example.test",22,store,keyA,keyB).Check.Connect());
			Assert.AreEqual(0,store.DictKeys.Count);
			//First use with a re-key of the same key: stored.
			new SimulatedConnection("sftp.example.test",22,store,keyA,keyA).Check.Connect();
			Assert.IsTrue(Fingerprint(keyA).Matches(store.DictKeys["sftp.example.test:22"]));
			//Stored key: a re-key with another key is refused.
			TestAssert.Throws<SshHostKeyRefusedException>(() => new SimulatedConnection("sftp.example.test",22,store,keyA,keyB).Check.Connect());
		}

		[TestMethod]
		public void Servers_AreHostAndPort_HostWithoutCase() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			HostKeyEventArgs keyB=TestKeys.NewEd25519Event();
			new SimulatedConnection("sftp.example.test",22,store,keyA).Check.Connect();
			SimulatedConnection connectionUpper=new SimulatedConnection(" SFTP.Example.Test ",22,store,keyA);
			Assert.AreEqual("sftp.example.test",connectionUpper.Check.Host);
			connectionUpper.Check.Connect();
			new SimulatedConnection("sftp.example.test",2222,store,keyB).Check.Connect();
			new SimulatedConnection("sftp2.example.test",22,store,keyB).Check.Connect();
			Assert.AreEqual(3,store.DictKeys.Count,"Another port or another host is another server.");
			Assert.AreEqual(0,store.ListRefusals.Count);
		}

		[TestMethod]
		public void KeyWithoutFingerprint_IsRefused_AndNoConnectionRunsWithoutAKey() {
			MemoryHostKeyStore store=new MemoryHostKeyStore();
			//A check that has not read the store yet trusts nothing.
			SshHostKeyCheck checkUnread=new SshHostKeyCheck("sftp.example.test",22,store,() => { },() => { });
			Assert.IsFalse(checkUnread.CanTrust(SshHostKeyFingerprint.FromHostKeyEvent(TestKeys.NewEd25519Event())));
			//A first use whose key has no fingerprint.
			SshHostKeyCheck check=null;
			check=new SshHostKeyCheck("sftp.example.test",22,store,
				() => {
					if(!check.CanTrust(new SshHostKeyFingerprint("ssh-ed25519",""))) {
						throw new SshConnectionException("Key exchange negotiation failed.",DisconnectReason.KeyExchangeFailed);
					}
				},
				() => { });
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => check.Connect());
			StringAssert.Contains(ex.Message,"a key without a fingerprint");
			Assert.IsFalse(check.CanTrust(null));
			//A connection that signed in without any key exchange (SSH.NET never does this) is closed, not stored.
			int countDisconnects=0;
			SshHostKeyCheck checkNoKey=new SshHostKeyCheck("sftp.example.test",22,store,() => { },() => countDisconnects++);
			TestAssert.Throws<SshHostKeyRefusedException>(() => checkNoKey.Connect());
			Assert.AreEqual(1,countDisconnects);
			Assert.AreEqual(0,store.DictKeys.Count);
		}

		[TestMethod]
		public void Attach_NeedsAStore_AndEveryConnectGoesThroughTheCheck() {
			using(SftpClient sftpClient=new SftpClient("sftp.example.test",22,"user","synthetic-password")) {
				TestAssert.Throws<ArgumentNullException>(() => SshHostKeyCheck.Attach(sftpClient,null));
				Assert.IsFalse(SshHostKeyCheck.IsAttached(sftpClient));
				InvalidOperationException ex=TestAssert.Throws<InvalidOperationException>(() => SshHostKeyCheck.Connect(sftpClient),
					"A client without a check never connects.");
				StringAssert.Contains(ex.Message,"no host key check");
				SshHostKeyCheck.Attach(sftpClient,new MemoryHostKeyStore());
				Assert.IsTrue(SshHostKeyCheck.IsAttached(sftpClient));
				TestAssert.Throws<ArgumentException>(() => SshHostKeyCheck.Attach(sftpClient,new MemoryHostKeyStore()),"One check per client.");
			}
		}

		[TestMethod]
		public void Refusal_IsFoundInsideTheErrorsThatWrapIt() {
			SshHostKeyRefusedException exRefused=new SshHostKeyRefusedException("Refused.","sftp.example.test",22,null,null,null);
			Exception exWrapped=new Exception("Connecting to sftp.example.test has failed with user: user\r\nRefused.",exRefused);
			Assert.AreSame(exRefused,SshHostKeyCheck.FindRefusal(exWrapped));
			Assert.AreEqual("Refused.",SshHostKeyCheck.GetRefusalMessage(exWrapped));
			Assert.IsNull(SshHostKeyCheck.FindRefusal(new Exception("Other.",new SshConnectionException("Key exchange negotiation failed."))));
			Assert.IsNull(SshHostKeyCheck.GetRefusalMessage(null));
		}
	}
}
