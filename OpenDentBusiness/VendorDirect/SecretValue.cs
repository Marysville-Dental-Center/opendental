// Vendor-direct integrations: how secrets are stored in programproperty (and emailaddress) rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;

namespace OpenDentBusiness.VendorDirect {
	///<summary>What to do with a stored value that has no osd1$ prefix.</summary>
	public enum LegacyPolicy {
		///<summary>Keys the office typed, which Open Dental stored as plain text: the PaySimple key, PayConnect Secret, EdgeExpress Auth Key, DoseSpot
		///clinic key, NewCrop reseller password. Used as-is, and encrypted on the next save.</summary>
		AcceptPlaintext,
		///<summary>Tokens or ciphertexts issued under Open Dental: QuickBooks Online tokens, the Gmail refresh token, the Microsoft 365 cache,
		///PDMP certificates and passwords, the CareCredit token. Treated as "needs re-authorization" and never sent.</summary>
		RejectPlaintext,
	}

	///<summary>The outcome of reading a stored secret.</summary>
	public enum SecretReadStatus {
		///<summary>An osd1$ value that decrypted.</summary>
		Ok,
		///<summary>Nothing stored.</summary>
		Blank,
		///<summary>An osd1$ value that didn't decrypt, or an Open Dental ciphertext (odv2e$). Counts as "not configured"; the setup field shows blank.</summary>
		Unreadable,
		///<summary>A value without the prefix. Usable only under LegacyPolicy.AcceptPlaintext (PlainText is then set).</summary>
		Legacy,
	}

	///<summary>A secret read from the database.</summary>
	public sealed class SecretReadResult {
		public SecretReadStatus Status { get; }
		///<summary>The plain value when Status is Ok, or Legacy under AcceptPlaintext. "" otherwise.</summary>
		public string PlainText { get; }

		public SecretReadResult(SecretReadStatus status,string plainText) {
			Status=status;
			PlainText=plainText??"";
		}

		///<summary>True if PlainText can be sent to the vendor.</summary>
		public bool HasValue {
			get {
				return PlainText!="";
			}
		}

		///<summary>True if the stored value should be rewritten as osd1$ on the next save (an accepted plain-text value).</summary>
		public bool NeedsReEncrypt {
			get {
				return Status==SecretReadStatus.Legacy && PlainText!="";
			}
		}

		///<summary>True if the user has to sign in again or re-enter the secret: unreadable, or plain text under RejectPlaintext.</summary>
		public bool NeedsReentry {
			get {
				return Status==SecretReadStatus.Unreadable || (Status==SecretReadStatus.Legacy && PlainText=="");
			}
		}
	}

	///<summary>The only way vendor-direct code stores and reads secrets. Wraps CodeBase.Secrets (osd1$). Unlike Secrets.TryEncrypt, Protect never
	///falls back to plain text; unlike Secrets.TryDecrypt, Read never hands back a corrupted ciphertext as if it were the password.
	///osd1$ is obfuscation, not secrecy (CodeBase/Crypto/README.md): the real controls are database access, high-security property flags,
	///least-privilege and revocable vendor credentials, and rotation when a copy leaks.</summary>
	public static class SecretValue {
		private const string PREFIX_OPEN_DENTAL_V2="odv2e$";

		///<summary>Encrypts plainText for storage. Returns false, with stored set to null, if encryption failed: the caller must refuse to save.
		///Blank stays blank (""). Secret rows are written with IsMasked=1 and IsHighSecurity=1 (VendorCredentials does this).</summary>
		public static bool Protect(string plainText,out string stored) {
			Meth.NoCheckMiddleTierRole();
			string encrypted;
			if(!Secrets.Encrypt(plainText,out encrypted) || encrypted==null) {
				stored=null;
				return false;
			}
			stored=encrypted;
			return true;
		}

		///<summary>Reads a stored value under the integration's legacy policy. Never throws.</summary>
		public static SecretReadResult Read(string stored,LegacyPolicy legacyPolicy) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(stored)) {
				return new SecretReadResult(SecretReadStatus.Blank,"");
			}
			if(stored.StartsWith(PREFIX_OPEN_DENTAL_V2,StringComparison.Ordinal)) {
				return new SecretReadResult(SecretReadStatus.Unreadable,"");
			}
			if(Secrets.IsEncrypted(stored)) {
				string plainText;
				if(Secrets.Decrypt(stored,out plainText)) {
					return new SecretReadResult(SecretReadStatus.Ok,plainText);
				}
				return new SecretReadResult(SecretReadStatus.Unreadable,"");
			}
			if(legacyPolicy==LegacyPolicy.AcceptPlaintext) {
				return new SecretReadResult(SecretReadStatus.Legacy,stored);
			}
			return new SecretReadResult(SecretReadStatus.Legacy,"");
		}

		///<summary>The plain value if Read gives one, otherwise "". For readers that only need "the key, or nothing".</summary>
		public static string ReadOrBlank(string stored,LegacyPolicy legacyPolicy) {
			return Read(stored,legacyPolicy).PlainText;
		}
	}
}
