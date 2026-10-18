// Tests for the open source CDT and ODCrypt libraries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CryptoTests {
	///<summary>Secret obfuscation in CDT.Class1 (Encrypt/Decrypt and their variants).</summary>
	[TestClass]
	public class CdtSecretTests {
		///<summary>Produced by SecretBox.Seal("correct horse battery staple") with IV 00 01 .. 0f. Its MAC was checked independently with Python's
		///hmac module. If this stops decrypting, every osd1$ value already stored in databases and config files would stop decrypting too.</summary>
		private const string KnownCiphertext="osd1$AAECAwQFBgcICQoLDA0OD+XLPQt/psHvhUOgHfBgyB+3gL/iNZui4su70t9kGZa/Hnd/Dh2S4abCA4K8NjKL0UgNr1NOaQ7UxRpegshU5rY=";
		private const string KnownPlaintext="correct horse battery staple";
		///<summary>osd1 AES key, HMAC-SHA256(SHA-256(key material), "osd1 encryption"), computed independently with Python.</summary>
		private const string KnownEncryptionKeyHex="5ea6a3d63826b93d44d6827a34f186f1b5db7ba0a32d57b37688580e2c810b07";

		private static readonly string[] _arrayPlaintexts=new string[] {
			"a",
			"password",
			"correct horse battery staple",
			"p@$$w0rd with spaces, quotes \" ' and <xml> & stuff",
			"Zoë €5 日本語 😀",
			"exactly16bytes!!",
			new string('x',5000),
		};

		[TestMethod]
		public void EncryptDecrypt_RoundTrips() {
			foreach(string plain in _arrayPlaintexts) {
				Assert.IsTrue(CDT.Class1.Encrypt(plain,out string encrypted));
				Assert.IsTrue(encrypted.StartsWith("osd1$"));
				Assert.AreNotEqual(plain,encrypted);
				Assert.IsTrue(CDT.Class1.Decrypt(encrypted,out string decrypted));
				Assert.AreEqual(plain,decrypted);
			}
		}

		[TestMethod]
		public void Encrypt_UsesRandomIv() {
			CDT.Class1.Encrypt("same input",out string first);
			CDT.Class1.Encrypt("same input",out string second);
			Assert.AreNotEqual(first,second);
			Assert.IsTrue(CDT.Class1.Decrypt(first,out string firstPlain));
			Assert.IsTrue(CDT.Class1.Decrypt(second,out string secondPlain));
			Assert.AreEqual("same input",firstPlain);
			Assert.AreEqual("same input",secondPlain);
		}

		[TestMethod]
		public void EmptyAndNull_StayBlank() {
			Assert.IsTrue(CDT.Class1.Encrypt("",out string encrypted));
			Assert.AreEqual("",encrypted);
			Assert.IsTrue(CDT.Class1.Encrypt(null,out encrypted));
			Assert.AreEqual("",encrypted);
			Assert.IsTrue(CDT.Class1.Decrypt("",out string decrypted));
			Assert.AreEqual("",decrypted);
			Assert.IsTrue(CDT.Class1.Decrypt(null,out decrypted));
			Assert.AreEqual("",decrypted);
		}

		[TestMethod]
		public void KnownCiphertext_Decrypts() {
			Assert.IsTrue(CDT.Class1.Decrypt(KnownCiphertext,out string plain));
			Assert.AreEqual(KnownPlaintext,plain);
			//Values pasted into XML config files may pick up surrounding whitespace.
			Assert.IsTrue(CDT.Class1.Decrypt("  "+KnownCiphertext+"\r\n",out plain));
			Assert.AreEqual(KnownPlaintext,plain);
		}

		[TestMethod]
		public void Seal_WithFixedIv_IsStable() {
			byte[] arrayIv=new byte[16];
			for(int i=0;i<16;i++) {
				arrayIv[i]=(byte)i;
			}
			Assert.AreEqual(KnownCiphertext,CDT.SecretBox.Seal(KnownPlaintext,arrayIv));
		}

		///<summary>Decrypts the known value with the framework's AesManaged and an independently derived key, to pin the documented format:
		///"osd1$" + base64(IV || AES-256-CBC-PKCS7(UTF-8 plaintext) || tag).</summary>
		[TestMethod]
		public void KnownCiphertext_MatchesDocumentedFormat() {
			byte[] arrayPayload=Convert.FromBase64String(KnownCiphertext.Substring(5));
			byte[] arrayIv=new byte[16];
			byte[] arrayCipher=new byte[arrayPayload.Length-16-32];
			Buffer.BlockCopy(arrayPayload,0,arrayIv,0,16);
			Buffer.BlockCopy(arrayPayload,16,arrayCipher,0,arrayCipher.Length);
			using(AesManaged aes=new AesManaged()) {
				aes.Key=TestUtil.FromHex(KnownEncryptionKeyHex);
				aes.IV=arrayIv;
				aes.Mode=CipherMode.CBC;
				aes.Padding=PaddingMode.PKCS7;
				using(ICryptoTransform decryptor=aes.CreateDecryptor()) {
					byte[] arrayPlain=decryptor.TransformFinalBlock(arrayCipher,0,arrayCipher.Length);
					Assert.AreEqual(KnownPlaintext,Encoding.UTF8.GetString(arrayPlain));
				}
			}
		}

		[TestMethod]
		public void Decrypt_RejectsNonCiphertext() {
			string[] arrayNotOurs=new string[] {
				"plain password",
				"cGFzc3dvcmQ=",//base64, like Open Dental's unprefixed values
				"odv2e$fakeciphertext",
				"osd1$",
				"osd1$not base64!",
				"osd1$AAECAwQFBgcICQoLDA0ODw==",//too short
			};
			foreach(string text in arrayNotOurs) {
				Assert.IsFalse(CDT.Class1.Decrypt(text,out string plain),text);
				Assert.AreEqual("",plain,text);
			}
		}

		[TestMethod]
		public void Decrypt_RejectsTamperedCiphertext() {
			byte[] arrayPayload=Convert.FromBase64String(KnownCiphertext.Substring(5));
			for(int i=0;i<arrayPayload.Length;i+=7) {
				byte[] arrayTampered=(byte[])arrayPayload.Clone();
				arrayTampered[i]^=0x01;
				string tampered="osd1$"+Convert.ToBase64String(arrayTampered);
				Assert.IsFalse(CDT.Class1.Decrypt(tampered,out string plain),"Byte "+i);
				Assert.AreEqual("",plain);
			}
		}

		[TestMethod]
		public void TryDecrypt_PassesThroughNonCiphertext() {
			Assert.AreEqual("plain password",CDT.Class1.TryDecrypt("plain password"));
			Assert.AreEqual("cGFzc3dvcmQ=",CDT.Class1.TryDecrypt("cGFzc3dvcmQ="));
			Assert.AreEqual("odv2e$fakeciphertext",CDT.Class1.TryDecrypt("odv2e$fakeciphertext"));
			Assert.AreEqual("<WhiteList><Item>x</Item></WhiteList>",CDT.Class1.TryDecrypt("<WhiteList><Item>x</Item></WhiteList>"));
			Assert.AreEqual("",CDT.Class1.TryDecrypt(""));
			Assert.IsNull(CDT.Class1.TryDecrypt(null));
			//Damaged osd1$ values come back unchanged rather than as garbage.
			Assert.AreEqual("osd1$garbage",CDT.Class1.TryDecrypt("osd1$garbage"));
		}

		[TestMethod]
		public void TryDecrypt_DecryptsCiphertext() {
			Assert.AreEqual(KnownPlaintext,CDT.Class1.TryDecrypt(KnownCiphertext));
			foreach(string plain in _arrayPlaintexts) {
				Assert.AreEqual(plain,CDT.Class1.TryDecrypt(CDT.Class1.TryEncrypt(plain)));
			}
		}

		[TestMethod]
		public void TryEncrypt_BlankStaysBlank() {
			Assert.AreEqual("",CDT.Class1.TryEncrypt(""));
			Assert.IsNull(CDT.Class1.TryEncrypt(null));
			Assert.IsTrue(CDT.Class1.TryEncrypt("x").StartsWith("osd1$"));
		}

		[TestMethod]
		public void SftpAndClearinghouseVariants_ShareFormat() {
			string sftp=CDT.Class1.EncryptSftp("sftp pass");
			Assert.IsTrue(CDT.Class1.DecryptSftp(sftp,out string plain));
			Assert.AreEqual("sftp pass",plain);
			Assert.IsTrue(CDT.Class1.Decrypt(sftp,out plain));
			Assert.AreEqual("sftp pass",plain);
			Assert.AreEqual("",CDT.Class1.EncryptSftp(""));
			Assert.IsTrue(CDT.Class1.ConcealClearinghouse("clearinghouse pass",out string concealed));
			Assert.IsTrue(CDT.Class1.RevealClearinghouse(concealed,out plain));
			Assert.AreEqual("clearinghouse pass",plain);
			Assert.IsFalse(CDT.Class1.RevealClearinghouse("legacy concealed value",out plain));
			Assert.AreEqual("",plain);
			Assert.IsTrue(CDT.Class1.IsEncrypted(concealed));
			Assert.IsFalse(CDT.Class1.IsEncrypted("legacy concealed value"));
		}
	}
}
