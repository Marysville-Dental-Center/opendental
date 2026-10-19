// Tests for SecretValue, the only way vendor-direct code stores and reads secrets.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class SecretValueTests {
		[TestMethod]
		public void Protect_RoundTripsAndIsDeterministic() {
			string stored=SecretsTestHelper.Protect("client-secret-123");
			SecretsTestHelper.AssertRoundTrip("client-secret-123",stored);
			Assert.AreEqual(stored,SecretsTestHelper.Protect("client-secret-123"),"Compare-and-swap relies on osd1$ being deterministic.");
			string blank;
			Assert.IsTrue(SecretValue.Protect("",out blank));
			Assert.AreEqual("",blank);
		}

		[TestMethod]
		public void Read_Ok() {
			SecretReadResult result=SecretValue.Read(SecretsTestHelper.Protect("k"),LegacyPolicy.RejectPlaintext);
			Assert.AreEqual(SecretReadStatus.Ok,result.Status);
			Assert.AreEqual("k",result.PlainText);
			Assert.IsTrue(result.HasValue);
			Assert.IsFalse(result.NeedsReEncrypt);
			Assert.IsFalse(result.NeedsReentry);
		}

		[TestMethod]
		public void Read_Blank() {
			Assert.AreEqual(SecretReadStatus.Blank,SecretValue.Read("",LegacyPolicy.AcceptPlaintext).Status);
			Assert.AreEqual(SecretReadStatus.Blank,SecretValue.Read(null,LegacyPolicy.RejectPlaintext).Status);
			Assert.IsFalse(SecretValue.Read(null,LegacyPolicy.RejectPlaintext).NeedsReentry);
		}

		[TestMethod]
		public void Read_CorruptedCiphertextIsUnreadableNeverSent() {
			string stored=SecretsTestHelper.Protect("password");
			string corrupted=stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA");
			SecretReadResult result=SecretValue.Read(corrupted,LegacyPolicy.AcceptPlaintext);
			Assert.AreEqual(SecretReadStatus.Unreadable,result.Status);
			Assert.AreEqual("",result.PlainText,"Unlike Secrets.TryDecrypt, the ciphertext is never handed back as the password.");
			Assert.IsTrue(result.NeedsReentry);
			Assert.AreEqual(corrupted,Secrets.TryDecrypt(corrupted),"(The behaviour SecretValue avoids.)");
		}

		[TestMethod]
		public void Read_OpenDentalCiphertextIsUnreadable() {
			SecretReadResult result=SecretValue.Read("odv2e$AbCdEf==",LegacyPolicy.AcceptPlaintext);
			Assert.AreEqual(SecretReadStatus.Unreadable,result.Status);
			Assert.AreEqual("",result.PlainText);
		}

		[TestMethod]
		public void Read_PlainTextFollowsLegacyPolicy() {
			SecretReadResult resultAccept=SecretValue.Read("typed-key",LegacyPolicy.AcceptPlaintext);
			Assert.AreEqual(SecretReadStatus.Legacy,resultAccept.Status);
			Assert.AreEqual("typed-key",resultAccept.PlainText);
			Assert.IsTrue(resultAccept.NeedsReEncrypt);
			SecretReadResult resultReject=SecretValue.Read("old-refresh-token",LegacyPolicy.RejectPlaintext);
			Assert.AreEqual(SecretReadStatus.Legacy,resultReject.Status);
			Assert.AreEqual("",resultReject.PlainText,"A token issued under Open Dental is never sent.");
			Assert.IsTrue(resultReject.NeedsReentry);
			Assert.AreEqual("",SecretValue.ReadOrBlank("old-refresh-token",LegacyPolicy.RejectPlaintext));
		}
	}
}
