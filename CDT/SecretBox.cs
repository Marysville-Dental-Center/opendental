// CDT: open source replacement for the prebuilt CDT.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;
using System.Text;

namespace CDT {
	///<summary>Reversible obfuscation of stored secrets (passwords and tokens in the database and in local config files).
	///Format: "osd1$" + base64(IV[16] || AES-256-CBC-PKCS7 ciphertext || HMAC-SHA256 tag[32]). The tag covers the ASCII prefix, the IV and the
	///ciphertext (encrypt-then-MAC), so corrupted or foreign values are rejected instead of decrypting to garbage.
	///The key is fixed and derived from a public constant. That is deliberate; see CDT/README.md for the threat model.</summary>
	internal static class SecretBox {
		///<summary>Version prefix of every value this class produces. A future format with different keys must use a new prefix (e.g. osd2$).</summary>
		internal const string Prefix="osd1$";
		///<summary>Public key material for osd1. Changing it makes every stored osd1 value undecryptable.</summary>
		internal const string KeyMaterial="CDT osd1 secret obfuscation key. Public by design; see CDT/README.md.";
		private const int IvLength=16;
		private const int TagLength=32;
		private const int AesBlockLength=16;
		private static readonly byte[] _arrayPrefix=Encoding.ASCII.GetBytes(Prefix);
		private static readonly byte[] _arrayKeyEncrypt;
		private static readonly byte[] _arrayKeyAuthenticate;

		static SecretBox() {
			byte[] arrayMaster=HmacSha256.Sha256(Encoding.UTF8.GetBytes(KeyMaterial));
			_arrayKeyEncrypt=HmacSha256.Compute(arrayMaster,Encoding.ASCII.GetBytes("osd1 encryption"));
			_arrayKeyAuthenticate=HmacSha256.Compute(arrayMaster,Encoding.ASCII.GetBytes("osd1 authentication"));
		}

		///<summary>True if text carries the osd1 prefix. Says nothing about whether it will decrypt.</summary>
		internal static bool HasPrefix(string text) {
			return text!=null && text.Trim().StartsWith(Prefix,StringComparison.Ordinal);
		}

		///<summary>Encrypts plainText (UTF-8) with a random IV.</summary>
		internal static string Seal(string plainText) {
			byte[] arrayIv=new byte[IvLength];
			using(RandomNumberGenerator rng=new RNGCryptoServiceProvider()) {
				rng.GetBytes(arrayIv);
			}
			return Seal(plainText,arrayIv);
		}

		///<summary>Encrypts plainText with the given IV. Only tests should pass their own IV.</summary>
		internal static string Seal(string plainText,byte[] arrayIv) {
			if(plainText==null) {
				throw new ArgumentNullException(nameof(plainText));
			}
			if(arrayIv==null || arrayIv.Length!=IvLength) {
				throw new ArgumentException("IV must be "+IvLength+" bytes.",nameof(arrayIv));
			}
			byte[] arrayPlain=Encoding.UTF8.GetBytes(plainText);
			byte[] arrayCipher;
			using(Aes aes=CreateAes()) {
				aes.IV=arrayIv;
				using(ICryptoTransform encryptor=aes.CreateEncryptor()) {
					arrayCipher=encryptor.TransformFinalBlock(arrayPlain,0,arrayPlain.Length);
				}
			}
			byte[] arrayTag=ComputeTag(arrayIv,arrayCipher,arrayCipher.Length);
			byte[] arrayPayload=new byte[IvLength+arrayCipher.Length+TagLength];
			Buffer.BlockCopy(arrayIv,0,arrayPayload,0,IvLength);
			Buffer.BlockCopy(arrayCipher,0,arrayPayload,IvLength,arrayCipher.Length);
			Buffer.BlockCopy(arrayTag,0,arrayPayload,IvLength+arrayCipher.Length,TagLength);
			return Prefix+Convert.ToBase64String(arrayPayload);
		}

		///<summary>Decrypts a value produced by Seal. Returns false, with plainText set to "", if sealedText isn't an intact osd1 value.</summary>
		internal static bool TryOpen(string sealedText,out string plainText) {
			plainText="";
			if(!HasPrefix(sealedText)) {
				return false;
			}
			byte[] arrayPayload;
			try {
				arrayPayload=Convert.FromBase64String(sealedText.Trim().Substring(Prefix.Length));
			}
			catch(FormatException) {
				return false;
			}
			int lengthCipher=arrayPayload.Length-IvLength-TagLength;
			if(lengthCipher<AesBlockLength || lengthCipher%AesBlockLength!=0) {
				return false;
			}
			byte[] arrayIv=new byte[IvLength];
			byte[] arrayCipher=new byte[lengthCipher];
			byte[] arrayTag=new byte[TagLength];
			Buffer.BlockCopy(arrayPayload,0,arrayIv,0,IvLength);
			Buffer.BlockCopy(arrayPayload,IvLength,arrayCipher,0,lengthCipher);
			Buffer.BlockCopy(arrayPayload,IvLength+lengthCipher,arrayTag,0,TagLength);
			if(!HmacSha256.ConstantEquals(arrayTag,ComputeTag(arrayIv,arrayCipher,lengthCipher))) {
				return false;
			}
			try {
				using(Aes aes=CreateAes()) {
					aes.IV=arrayIv;
					using(ICryptoTransform decryptor=aes.CreateDecryptor()) {
						byte[] arrayPlain=decryptor.TransformFinalBlock(arrayCipher,0,arrayCipher.Length);
						plainText=new UTF8Encoding(encoderShouldEmitUTF8Identifier:false,throwOnInvalidBytes:true).GetString(arrayPlain);
					}
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is ArgumentException) {
				plainText="";
				return false;
			}
			return true;
		}

		private static Aes CreateAes() {
			Aes aes=new AesCryptoServiceProvider();
			aes.KeySize=256;
			aes.Key=_arrayKeyEncrypt;
			aes.Mode=CipherMode.CBC;
			aes.Padding=PaddingMode.PKCS7;
			return aes;
		}

		private static byte[] ComputeTag(byte[] arrayIv,byte[] arrayCipher,int lengthCipher) {
			byte[] arrayAuthenticated=new byte[_arrayPrefix.Length+IvLength+lengthCipher];
			Buffer.BlockCopy(_arrayPrefix,0,arrayAuthenticated,0,_arrayPrefix.Length);
			Buffer.BlockCopy(arrayIv,0,arrayAuthenticated,_arrayPrefix.Length,IvLength);
			Buffer.BlockCopy(arrayCipher,0,arrayAuthenticated,_arrayPrefix.Length+IvLength,lengthCipher);
			return HmacSha256.Compute(_arrayKeyAuthenticate,arrayAuthenticated);
		}
	}
}
