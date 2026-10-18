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
		///<summary>Synthetic IV that Seal derives for KnownPlaintext, computed independently with Python's hmac module.</summary>
		private const string KnownSyntheticIvHex="d015cdaa706054544121f497d8fe333e";

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

		///<summary>Setup forms (PayConnect, Transworld) and Prefs/ProgramProperties updates compare stored ciphertexts to tell whether a secret
		///changed or whether a clinic shares HQ's credentials, so equal plaintexts must give equal ciphertexts.</summary>
		[TestMethod]
		public void Encrypt_IsDeterministic() {
			Assert.IsTrue(CDT.Class1.Encrypt("same input",out string first));
			Assert.IsTrue(CDT.Class1.Encrypt("same input",out string second));
			Assert.AreEqual(first,second);
			Assert.AreEqual(first,CDT.Class1.TryEncrypt("same input"));
			Assert.AreEqual(first,CDT.Class1.EncryptSftp("same input"));
			Assert.IsTrue(CDT.Class1.ConcealClearinghouse("same input",out string concealed));
			Assert.AreEqual(first,concealed);
			Assert.IsTrue(CDT.Class1.Decrypt(first,out string plain));
			Assert.AreEqual("same input",plain);
			foreach(string text in _arrayPlaintexts) {
				Assert.AreEqual(CDT.Class1.TryEncrypt(text),CDT.Class1.TryEncrypt(text),text);
			}
		}

		[TestMethod]
		public void Encrypt_DifferentInputs_GiveDifferentCiphertexts() {
			string[] arrayInputs=new string[_arrayPlaintexts.Length+4];
			_arrayPlaintexts.CopyTo(arrayInputs,0);
			//Near misses: case, trailing space, one extra character, and a value equal to another one's padded length.
			arrayInputs[_arrayPlaintexts.Length]="Password";
			arrayInputs[_arrayPlaintexts.Length+1]="password ";
			arrayInputs[_arrayPlaintexts.Length+2]="passwords";
			arrayInputs[_arrayPlaintexts.Length+3]="exactly16bytes!!\u0010";
			for(int i=0;i<arrayInputs.Length;i++) {
				string cipherI=CDT.Class1.TryEncrypt(arrayInputs[i]);
				for(int j=i+1;j<arrayInputs.Length;j++) {
					Assert.AreNotEqual(cipherI,CDT.Class1.TryEncrypt(arrayInputs[j]),arrayInputs[i]+" vs "+arrayInputs[j]);
				}
			}
			//Different plaintexts also get different IVs, so CBC never reuses an IV for different data.
			Assert.AreNotEqual(TestUtil.ToHex(CDT.SecretBox.SyntheticIv("password")),TestUtil.ToHex(CDT.SecretBox.SyntheticIv("Password")));
		}

		///<summary>Pins the synthetic IV, first 16 bytes of HMAC-SHA256(HMAC-SHA256(master, "osd1 iv"), "osd1$" || UTF-8 plaintext), with values
		///computed independently with Python's hmac module. If the derivation changes, every unchanged secret looks altered on its next save.</summary>
		[TestMethod]
		public void SyntheticIv_MatchesKnownAnswers() {
			Assert.AreEqual(KnownSyntheticIvHex,TestUtil.ToHex(CDT.SecretBox.SyntheticIv(KnownPlaintext)));
			Assert.AreEqual("cf3aadeaa7b9de509274cb8f6fa3f816",TestUtil.ToHex(CDT.SecretBox.SyntheticIv("same input")));
			string sealedText=CDT.SecretBox.Seal(KnownPlaintext);
			Assert.AreEqual(CDT.SecretBox.Seal(KnownPlaintext,TestUtil.FromHex(KnownSyntheticIvHex)),sealedText);
			byte[] arrayPayload=Convert.FromBase64String(sealedText.Substring(5));
			byte[] arrayIv=new byte[16];
			Buffer.BlockCopy(arrayPayload,0,arrayIv,0,16);
			Assert.AreEqual(KnownSyntheticIvHex,TestUtil.ToHex(arrayIv));
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
