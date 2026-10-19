// Tests for CodeBase's Secrets and TamperHash and the ODCrypt library.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ODCrypt;

namespace CryptoTests {
	[TestClass]
	public class ODCryptUtilTests {
		[TestMethod]
		public void ConstantEquals_Cases() {
			Assert.IsTrue(CryptUtil.ConstantEquals("abc","abc"));
			Assert.IsTrue(CryptUtil.ConstantEquals("",""));
			Assert.IsTrue(CryptUtil.ConstantEquals(null,null));
			Assert.IsFalse(CryptUtil.ConstantEquals("abc","abd"));
			Assert.IsFalse(CryptUtil.ConstantEquals("abc","ab"));
			Assert.IsFalse(CryptUtil.ConstantEquals("abc",null));
			Assert.IsFalse(CryptUtil.ConstantEquals(null,""));
		}

		[TestMethod]
		public void GenerateSalt_IsBase64OfRequestedLength() {
			Assert.AreEqual("",CryptUtil.GenerateSalt(0));
			Assert.AreEqual(64,Convert.FromBase64String(CryptUtil.GenerateSalt(64)).Length);
			Assert.AreEqual(16,Convert.FromBase64String(CryptUtil.GenerateSalt(16)).Length);
			Assert.AreNotEqual(CryptUtil.GenerateSalt(64),CryptUtil.GenerateSalt(64));
		}

		[TestMethod]
		public void RandomString_IsBase64OfBitsOver8() {
			Assert.AreEqual(16,Convert.FromBase64String(CryptUtil.RandomString(128)).Length);
			Assert.AreEqual(32,Convert.FromBase64String(CryptUtil.RandomString(256)).Length);
			Assert.AreNotEqual(CryptUtil.RandomString(128),CryptUtil.RandomString(128));
		}

		///<summary>UserWebs.PassGen indexes arrays with Random&lt;int&gt;()%length, so values must never be negative.</summary>
		[TestMethod]
		public void RandomInt_IsNonNegativeAndVaries() {
			HashSet<int> hashSetValues=new HashSet<int>();
			for(int i=0;i<2000;i++) {
				int value=CryptUtil.Random<int>();
				Assert.IsTrue(value>=0);
				hashSetValues.Add(value);
			}
			Assert.IsTrue(hashSetValues.Count>1900);
			Assert.IsTrue(CryptUtil.Random<long>()>=0);
			Assert.IsTrue(CryptUtil.Random<short>()>=0);
			Assert.IsTrue(CryptUtil.Random<sbyte>()>=0);
		}

		[TestMethod]
		public void RandomDouble_IsInUnitInterval() {
			for(int i=0;i<1000;i++) {
				double value=CryptUtil.Random<double>();
				Assert.IsTrue(value>=0 && value<1);
			}
		}

		[TestMethod]
		public void RandomUnsupportedType_Throws() {
			try {
				CryptUtil.Random<DateTime>();
				Assert.Fail("Expected NotSupportedException.");
			}
			catch(NotSupportedException) {
			}
		}

		[TestMethod]
		public void CompressString_RoundTrips() {
			string text="[{\"ComputerName\":\"FRONT-DESK\",\"Note\":\"Zoë €5\"}]"+new string('x',5000);
			string error="";
			Assert.IsTrue(Encryption.CompressString(text,out string compressed,ref error));
			Assert.IsTrue(compressed.Length<text.Length);
			Assert.IsTrue(Encryption.DecompressString(compressed,out string decompressed,ref error));
			Assert.AreEqual(text,decompressed);
		}

		///<summary>EConnectorStatistics relies on DecompressString returning false for plain JSON so it can fall back to the raw text.</summary>
		[TestMethod]
		public void DecompressString_RejectsUncompressedText() {
			string error="";
			Assert.IsFalse(Encryption.DecompressString("[{\"a\":1}]",out string text,ref error));
			Assert.AreEqual("",text);
			Assert.IsFalse(Encryption.DecompressString("QUJDRA==",out text,ref error));//valid base64, not gzip
			Assert.IsFalse(Encryption.DecompressString("",out text,ref error));
			Assert.IsFalse(Encryption.DecompressString(null,out text,ref error));
		}

		[TestMethod]
		public void EncryptString_IsNotSupported() {
			string error="";
			try {
				Encryption.EncryptString("secret",true,out string encrypted,ref error);
				Assert.Fail("Expected NotSupportedException.");
			}
			catch(NotSupportedException) {
			}
		}
	}
}
