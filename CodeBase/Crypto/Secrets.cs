// Secret obfuscation and SecurityHash tamper hashes, built on .NET's own crypto providers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace CodeBase {
	///<summary>Obfuscated storage of passwords and tokens in the database and in local config files: AES-256-CBC plus HMAC-SHA256, deterministic,
	///"osd1$" prefix, fixed public key. This is obfuscation at rest, not strong security. See CodeBase/Crypto/README.md for the format and the
	///threat model. Lives in CodeBase because DataConnectionBase needs it to read connection passwords before any database connection exists.</summary>
	public static class Secrets {
		///<summary>Prefix of Open Dental's own "v2" ciphertexts, which can only be decrypted by Open Dental's binaries. Always rejected.</summary>
		private const string PrefixOpenDentalV2="odv2e$";

		///<summary>Prefix of every value this class produces.</summary>
		public static string Prefix {
			get {
				return SecretBox.Prefix;
			}
		}

		///<summary>Encrypts plainText for storage in the database or a local config file. Returns true and sets encryptedText on success.
		///An empty or null plainText gives "" (blank stays blank). On failure returns false and sets encryptedText to null.</summary>
		public static bool Encrypt(string plainText,out string encryptedText) {
			if(string.IsNullOrEmpty(plainText)) {
				encryptedText="";
				return true;
			}
			try {
				encryptedText=SecretBox.Seal(plainText);
				return true;
			}
			catch(Exception) {//Callers check the return value.
				encryptedText=null;
				return false;
			}
		}

		///<summary>Decrypts a value produced by Encrypt (or any of the *Sftp/*Clearinghouse variants, which share one format).
		///An empty or null encryptedText gives "" and returns true. Returns false, with plainText set to "", for anything that isn't an intact
		///osd1$ value: plain text, Open Dental's own ciphertexts (unprefixed base64 or odv2e$), or corrupted data. Secrets that Open Dental
		///stored encrypted have to be re-entered after migrating a database.</summary>
		public static bool Decrypt(string encryptedText,out string plainText) {
			plainText="";
			if(string.IsNullOrEmpty(encryptedText)) {
				return true;
			}
			if(encryptedText.StartsWith(PrefixOpenDentalV2,StringComparison.Ordinal)) {
				return false;
			}
			try {
				return SecretBox.TryOpen(encryptedText,out plainText);
			}
			catch(Exception) {//Callers check the return value.
				plainText="";
				return false;
			}
		}

		///<summary>Returns the encrypted form of plainText, or plainText unchanged if it is null or empty or encryption fails.</summary>
		public static string TryEncrypt(string plainText) {
			if(string.IsNullOrEmpty(plainText)) {
				return plainText;
			}
			if(Encrypt(plainText,out string encryptedText)) {
				return encryptedText;
			}
			return plainText;
		}

		///<summary>Returns the decrypted form of text if it is a value produced by Encrypt. Otherwise returns text unchanged, so values that
		///were stored as plain text keep working.</summary>
		public static string TryDecrypt(string text) {
			if(string.IsNullOrEmpty(text) || !SecretBox.HasPrefix(text)) {
				return text;
			}
			if(Decrypt(text,out string plainText)) {
				return plainText;
			}
			return text;
		}

		///<summary>Encrypts an SFTP password. Same format as Encrypt. Returns "" if plainText is empty or encryption fails.</summary>
		public static string EncryptSftp(string plainText) {
			if(Encrypt(plainText,out string encryptedText)) {
				return encryptedText;
			}
			return "";
		}

		///<summary>Decrypts an SFTP password stored by EncryptSftp. Same behavior as Decrypt.</summary>
		public static bool DecryptSftp(string encryptedText,out string plainText) {
			return Decrypt(encryptedText,out plainText);
		}

		///<summary>Conceals a clearinghouse password for storage in the clearinghouse table. Same format and behavior as Encrypt.</summary>
		public static bool ConcealClearinghouse(string password,out string concealedPassword) {
			return Encrypt(password,out concealedPassword);
		}

		///<summary>Reveals a clearinghouse password stored by ConcealClearinghouse. Same behavior as Decrypt: returns false and sets
		///password to "" if the stored value can't be revealed, so the user is asked to re-enter it.</summary>
		public static bool RevealClearinghouse(string concealedPassword,out string password) {
			return Decrypt(concealedPassword,out password);
		}

		///<summary>True if text looks like a value produced by Encrypt (has the osd1$ prefix). Doesn't check that it decrypts.</summary>
		public static bool IsEncrypted(string text) {
			return SecretBox.HasPrefix(text);
		}
	}
}
