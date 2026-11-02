// The DPAPI helper (CodeBase.MachineSecret): machine scope, fixed entropy, the dpapi1$ prefix.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;
using System.Text;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class MachineSecretTests {
		[TestMethod]
		public void Protect_RoundTrips_AndNeverHoldsThePlainText() {
			foreach(string plain in new[] { TestHelpers.SECRET,"a","pässwörd ünïcode 密码"," leading and trailing ",new string('x',2000) }) {
				string protectedText=MachineSecret.Protect(plain);
				StringAssert.StartsWith(protectedText,MachineSecret.Prefix);
				Assert.IsTrue(MachineSecret.IsProtected(protectedText));
				if(plain.Length>=8) {//a short text can turn up in any Base64 by chance
					Assert.IsFalse(protectedText.Contains(plain),"The plain text is not in the stored value.");
				}
				Assert.IsTrue(MachineSecret.TryUnprotect(protectedText,out string roundTrip));
				Assert.AreEqual(plain,roundTrip);
			}
		}

		[TestMethod]
		public void Protect_IsDpapiMachineScope_WithTheFixedEntropy() {
			string protectedText=MachineSecret.Protect(TestHelpers.SECRET);
			byte[] arrayBlob=Convert.FromBase64String(protectedText.Substring(MachineSecret.Prefix.Length));
			//Without the entropy DPAPI refuses it: another program's blob is never taken for ours, nor ours for its.
			TestHelpers.AssertThrows<CryptographicException>(() => ProtectedData.Unprotect(arrayBlob,null,DataProtectionScope.LocalMachine));
			byte[] arrayOther=ProtectedData.Protect(Encoding.UTF8.GetBytes(TestHelpers.SECRET),null,DataProtectionScope.LocalMachine);
			Assert.IsFalse(MachineSecret.TryUnprotect(MachineSecret.Prefix+Convert.ToBase64String(arrayOther),out string plain));
			Assert.AreEqual("",plain);
		}

		[TestMethod]
		public void Protect_TwiceGivesTwoValues_BothReadable() {
			string first=MachineSecret.Protect(TestHelpers.SECRET);
			string second=MachineSecret.Protect(TestHelpers.SECRET);
			Assert.AreNotEqual(first,second,"DPAPI salts every blob.");
			Assert.IsTrue(MachineSecret.TryUnprotect(second,out string plain));
			Assert.AreEqual(TestHelpers.SECRET,plain);
		}

		[TestMethod]
		public void BlankStaysBlank_AndAnythingElseIsNotReadable() {
			Assert.AreEqual("",MachineSecret.Protect(""));
			Assert.AreEqual("",MachineSecret.Protect(null));
			Assert.IsTrue(MachineSecret.TryUnprotect("",out string empty));
			Assert.AreEqual("",empty);
			foreach(string text in new[] { "plain text","osd1$AAAA",MachineSecret.Prefix+"not base64!",MachineSecret.Prefix+"AAAAAAAA",
				Secrets.TryEncrypt(TestHelpers.SECRET) })
			{
				Assert.IsFalse(MachineSecret.TryUnprotect(text,out string plain),text);
				Assert.AreEqual("",plain);
			}
			Assert.IsFalse(MachineSecret.IsProtected("osd1$AAAA"));
		}
	}
}
