// Tests for the open source CDT and ODCrypt libraries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CryptoTests {
	///<summary>Known-answer tests for ODCrypt.MD5 and ODCrypt.Sha3. Expected values are the published RFC 1321 / FIPS 202 vectors, plus values
	///computed with Python's hashlib for padding boundaries.</summary>
	[TestClass]
	public class ODCryptHashTests {
		private static string Sha3Hex(string ascii) {
			return TestUtil.ToHex(ODCrypt.Sha3.Hash(Encoding.ASCII.GetBytes(ascii)));
		}

		private static string Md5Hex(string ascii) {
			return TestUtil.ToHex(ODCrypt.MD5.Hash(Encoding.ASCII.GetBytes(ascii)));
		}

		[TestMethod]
		public void Sha3_512_Empty() {
			Assert.AreEqual("a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a6"
				+"15b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26",Sha3Hex(""));
		}

		[TestMethod]
		public void Sha3_512_Abc() {
			Assert.AreEqual("b751850b1a57168a5693cd924b6b096e08f621827444f70d884f5d0240d2712e"
				+"10e116e9192af3c91a7ec57647e3934057340b4cf408d5a56592f8274eec53f0",Sha3Hex("abc"));
		}

		[TestMethod]
		public void Sha3_512_448Bits() {
			Assert.AreEqual("04a371e84ecfb5b8b77cb48610fca8182dd457ce6f326a0fd3d7ec2f1e91636d"
				+"ee691fbe0c985302ba1b0d8dc78c086346b533b49c030d99a27daf1139d6e75e",
				Sha3Hex("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"));
		}

		[TestMethod]
		public void Sha3_512_896Bits() {
			Assert.AreEqual("afebb2ef542e6579c50cad06d2e578f9f8dd6881d7dc824d26360feebf18a4fa"
				+"73e3261122948efcfd492e74e82e2189ed0fb440d187f382270cb455f21dd185",
				Sha3Hex("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu"));
		}

		///<summary>Lengths around the 72-byte SHA3-512 rate, where the 0x06 and 0x80 padding bytes collide or spill into a new block.</summary>
		[TestMethod]
		public void Sha3_512_RateBoundaries() {
			Assert.AreEqual("070faf98d2a8fddf8ed886408744dc06456096c2e045f26f3c7b010530e6bbb3"
				+"db535a54d636856f4e0e1e982461cb9a7e8e57ff8895cff1619af9f0e486e28c",Sha3Hex(new string('a',71)));
			Assert.AreEqual("a8ae722a78e10cbbc413886c02eb5b369a03f6560084aff566bd597bb7ad8c1c"
				+"cd86e81296852359bf2faddb5153c0a7445722987875e74287adac21adebe952",Sha3Hex(new string('a',72)));
			Assert.AreEqual("23e6a8815f8201dbbf6a5463be8dcadb1acea9df5f8998954e59ac9565cf6d29"
				+"b17aa27a5e8b0fc06343db6122d6e544d27583ddc78504d08203217e7e65b6bd",Sha3Hex(new string('a',73)));
			Assert.AreEqual("446cd4d7ba19510dcc776b21045bc68d424b5b840e14685e149bb238b5f473c0"
				+"356b69e04f0f5785eefce20ff09e678b080d8aac64568c5edf001cd32b2ed7a8",Sha3Hex(new string('a',144)));
		}

		[TestMethod]
		public void Sha3_512_OneMillionA() {
			Assert.AreEqual("3c3a876da14034ab60627c077bb98f7e120a2a5370212dffb3385a18d4f38859"
				+"ed311d0a9d5141ce9cc5c66ee689b266a8aa18ace8282a0e0db596c90b0a7b87",Sha3Hex(new string('a',1000000)));
		}

		///<summary>Same construction as OpenDentBusiness.Authentication.HashPasswordSHA512: base64(SHA3-512(UTF-16LE(salt+password))).</summary>
		[TestMethod]
		public void Sha3_512_UserPasswordHashFormat() {
			string hash=Convert.ToBase64String(ODCrypt.Sha3.Hash(Encoding.Unicode.GetBytes("salt"+"password")));
			Assert.AreEqual("ExseJjQ1eeTqLOEKZe37PzcO7p2m380CRMToNhApescJmNkDbC4alY8+KMxMyk/P5LSlPEY6VvwuT7kKoKla7w==",hash);
		}

		[TestMethod]
		public void Sha3_512_NullThrows() {
			try {
				ODCrypt.Sha3.Hash(null);
				Assert.Fail("Expected ArgumentNullException.");
			}
			catch(ArgumentNullException) {
			}
		}

		[TestMethod]
		public void Md5_Rfc1321Vectors() {
			Assert.AreEqual("d41d8cd98f00b204e9800998ecf8427e",Md5Hex(""));
			Assert.AreEqual("0cc175b9c0f1b6a831c399e269772661",Md5Hex("a"));
			Assert.AreEqual("900150983cd24fb0d6963f7d28e17f72",Md5Hex("abc"));
			Assert.AreEqual("f96b697d7cb7938d525a2f31aaf161d0",Md5Hex("message digest"));
			Assert.AreEqual("c3fcd3d76192e4007dfb496cca67e13b",Md5Hex("abcdefghijklmnopqrstuvwxyz"));
			Assert.AreEqual("d174ab98d277d9f5a5611c2c9f419d9f",Md5Hex("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"));
			Assert.AreEqual("57edf4a22be3c955ac49da2e2107b67a",
				Md5Hex("12345678901234567890123456789012345678901234567890123456789012345678901234567890"));
		}

		///<summary>Same construction as OpenDentBusiness.Authentication.HashPasswordMD5 (non-eCW): base64(MD5(UTF-16LE(password))).</summary>
		[TestMethod]
		public void Md5_UserPasswordHashFormat() {
			Assert.AreEqual("sIHb6F4ew//D1OfQInQAzQ==",Convert.ToBase64String(ODCrypt.MD5.Hash(Encoding.Unicode.GetBytes("password"))));
		}

		///<summary>Compares against the framework's MD5 for every length across several block boundaries.</summary>
		[TestMethod]
		public void Md5_MatchesFrameworkForManyLengths() {
			using(System.Security.Cryptography.MD5 md5Framework=System.Security.Cryptography.MD5.Create()) {
				for(int length=0;length<=300;length++) {
					byte[] data=TestUtil.PseudoRandomBytes(length,length);
					CollectionAssert.AreEqual(md5Framework.ComputeHash(data),ODCrypt.MD5.Hash(data),"Length "+length);
				}
			}
		}
	}
}
