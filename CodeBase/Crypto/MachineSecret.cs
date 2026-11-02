// A secret protected for this computer with DPAPI (machine scope), for the database password in FreeDentalConfig.xml.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;
using System.Text;

namespace CodeBase {
	///<summary>Protects a secret for this computer with DPAPI: ProtectedData, DataProtectionScope.LocalMachine, a fixed entropy. Every process
	///on this computer can read it back and no other computer can (owner decision Q2 of 2026-11-02, docs/specs/db-connection-security.md).
	///Format: "dpapi1$" + Base64 of the DPAPI blob of the UTF-8 text. It replaces the osd1$ obfuscation (Secrets) for the database password of
	///FreeDentalConfig.xml: new writes use this class only. Messages of the exceptions this class throws never contain the secret.</summary>
	public static class MachineSecret {
		///<summary>Prefix of every value this class produces.</summary>
		public const string Prefix="dpapi1$";
		///<summary>Fixed entropy, so that a DPAPI blob made by another program on this computer is not taken for one of ours. Changing it makes
		///every stored value unreadable.</summary>
		private static readonly byte[] _arrayEntropy=Encoding.ASCII.GetBytes("Open PMS - Dental database password, machine scope, v1");

		///<summary>True if text carries the prefix. Says nothing about whether it will decrypt on this computer.</summary>
		public static bool IsProtected(string text) {
			return !string.IsNullOrEmpty(text) && text.StartsWith(Prefix,StringComparison.Ordinal);
		}

		///<summary>Protects plainText for this computer. Null or empty gives "" (blank stays blank). Throws CryptographicException (without the
		///secret in its message) when DPAPI fails.</summary>
		public static string Protect(string plainText) {
			if(string.IsNullOrEmpty(plainText)) {
				return "";
			}
			byte[] arrayPlain=Encoding.UTF8.GetBytes(plainText);
			try {
				byte[] arrayProtected=ProtectedData.Protect(arrayPlain,_arrayEntropy,DataProtectionScope.LocalMachine);
				return Prefix+Convert.ToBase64String(arrayProtected);
			}
			catch(CryptographicException ex) {
				throw new CryptographicException("Windows could not protect the secret for this computer (DPAPI, machine scope): 0x"
					+ex.HResult.ToString("X8"));
			}
			finally {
				Array.Clear(arrayPlain,0,arrayPlain.Length);
			}
		}

		///<summary>Returns true and sets plainText when protectedText is "" (gives "") or a value Protect made on this computer. Returns false, with
		///plainText "", for anything else: no prefix, broken Base64, a blob from another computer or another entropy.</summary>
		public static bool TryUnprotect(string protectedText,out string plainText) {
			plainText="";
			if(string.IsNullOrEmpty(protectedText)) {
				return true;
			}
			if(!IsProtected(protectedText)) {
				return false;
			}
			byte[] arrayProtected;
			try {
				arrayProtected=Convert.FromBase64String(protectedText.Substring(Prefix.Length));
			}
			catch(FormatException) {
				return false;
			}
			byte[] arrayPlain=null;
			try {
				arrayPlain=ProtectedData.Unprotect(arrayProtected,_arrayEntropy,DataProtectionScope.LocalMachine);
				plainText=Encoding.UTF8.GetString(arrayPlain);
				return true;
			}
			catch(CryptographicException) {
				return false;
			}
			finally {
				if(arrayPlain!=null) {
					Array.Clear(arrayPlain,0,arrayPlain.Length);
				}
			}
		}
	}
}
