// CDT: open source replacement for the prebuilt CDT.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace CDT {
	///<summary>Drop-in replacement for the proprietary CDT.dll. It keeps the original assembly name, namespace and the member signatures this
	///repository calls, written from those call sites and public algorithm specifications only. See CDT/README.md for the design and threat model.
	///Covers:
	///1. Secrets: obfuscated storage of passwords and tokens (AES-256-CBC + HMAC-SHA256, "osd1$" prefix, fixed public key).
	///2. SecurityHash: keyed hashes for tamper detection ("osh1$" prefix, fixed public key).
	///3. ADA content: CDT code text and ADA claim-form artwork are proprietary and are not included. The getters return empty values.
	///4. Registration keys: no license gates; key generation returns "" and validation always succeeds.</summary>
	public static class Class1 {
		///<summary>Prefix of Open Dental's own "v2" ciphertexts, which can only be decrypted by Open Dental's binaries. Always rejected.</summary>
		private const string PrefixOpenDentalV2="odv2e$";
		private static readonly object _lockSaltedHash=new object();
		private static SaltedHash _saltedHash;

		#region Secrets
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
		#endregion Secrets

		#region SecurityHash
		///<summary>Prefix of every hash CreateSaltedHash returns. A non-empty SecurityHash without it was written by Open Dental and can't be
		///recomputed here.</summary>
		public static string SaltedHashPrefix {
			get {
				return SaltedHash.Prefix;
			}
		}

		///<summary>Returns the keyed hash stored in SecurityHash columns. isPatientTable selects a separate key for the patient table.
		///Thread safe. Never returns null or empty.</summary>
		public static string CreateSaltedHash(string unhashedText,bool isPatientTable=false) {
			lock(_lockSaltedHash) {
				if(_saltedHash==null) {
					_saltedHash=new SaltedHash();
				}
				return _saltedHash.Compute(unhashedText,isPatientTable);
			}
		}

		///<summary>True if hash was produced by this version of CreateSaltedHash (has the current prefix).</summary>
		public static bool IsCurrentSaltedHash(string hash) {
			return SaltedHash.IsCurrentFormat(hash);
		}

		///<summary>Releases the cached hash providers after a bulk rehash. The next CreateSaltedHash call recreates them.</summary>
		public static void DisposeHashAlgorithm() {
			lock(_lockSaltedHash) {
				_saltedHash?.Dispose();
				_saltedHash=null;
			}
		}
		#endregion SecurityHash

		#region ADA content
		//The American Dental Association's CDT code text and claim-form artwork are copyrighted and licensed by the ADA, so they are not shipped.
		//Procedure codes come from a user import. Claim forms print on pre-printed paper, with no background image.

		///<summary>ADA procedure codes and descriptions. Always "": not distributed with this program.</summary>
		public static string GetADAcodes() {
			return "";
		}

		///<summary>Default settings for ADA procedure codes. Always "".</summary>
		public static string GetProcedureCodeSettings() {
			return "";
		}

		///<summary>Default treatment areas for ADA procedure codes. Always "".</summary>
		public static string GetProcedureCodeTreatAreas() {
			return "";
		}

		///<summary>JSON list of mandibular procedure codes. Always "", which callers treat as an empty list.</summary>
		public static string GetMandibularCodes() {
			return "";
		}

		///<summary>JSON list of maxillary procedure codes. Always "", which callers treat as an empty list.</summary>
		public static string GetMaxillaryCodes() {
			return "";
		}

		///<summary>Background for the ADA 2006 claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetADA2006() {
			return CreateBlankImage();
		}

		///<summary>Background for the ADA 2012 claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetADA2012() {
			return CreateBlankImage();
		}

		///<summary>Background for the ADA 2012 (J430D) claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetADA2012_J430D() {
			return CreateBlankImage();
		}

		///<summary>Background for the ADA 2018 (J432) claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetADA2018_J432() {
			return CreateBlankImage();
		}

		///<summary>Background for the ADA 2019 (J430) claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetADA2019_J430() {
			return CreateBlankImage();
		}

		///<summary>Background for the ADA 2024 (J430) claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetADA2024_J430() {
			return CreateBlankImage();
		}

		///<summary>Background for the DC-217 claim form. Blank: print on pre-printed forms.</summary>
		public static Image GetDC217() {
			return CreateBlankImage();
		}

		///<summary>A new 1x1 fully transparent image each call, so callers may dispose it.</summary>
		private static Image CreateBlankImage() {
			Bitmap bitmap=new Bitmap(1,1,PixelFormat.Format32bppArgb);
			bitmap.SetPixel(0,0,Color.Transparent);
			bitmap.SetResolution(100,100);
			return bitmap;
		}
		#endregion ADA content

		#region Registration keys
		///<summary>Registration keys are not used. Always returns "".</summary>
		public static string GenerateRandKey() {
			return "";
		}

		///<summary>Registration keys are not used. Always returns true.</summary>
		public static bool ValidateKey(string key) {
			return true;
		}
		#endregion Registration keys
	}
}
