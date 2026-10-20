// Test helper for every vendor-direct package: osd1$ values for fixtures, made with the real CodeBase.Secrets.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests {
	public static class SecretsTestHelper {
		///<summary>The osd1$ form of plainText, as a setup window would store it. Fails the test if encryption fails.</summary>
		public static string Protect(string plainText) {
			string stored;
			Assert.IsTrue(OpenDentBusiness.VendorDirect.SecretValue.Protect(plainText,out stored),"Encryption failed.");
			return stored;
		}

		///<summary>Asserts that stored is an osd1$ value that decrypts to expectedPlainText.</summary>
		public static void AssertRoundTrip(string expectedPlainText,string stored) {
			Assert.IsTrue(Secrets.IsEncrypted(stored),"Expected an osd1$ value.");
			string plainText;
			Assert.IsTrue(Secrets.Decrypt(stored,out plainText),"The stored value did not decrypt.");
			Assert.AreEqual(expectedPlainText,plainText);
		}
	}
}
