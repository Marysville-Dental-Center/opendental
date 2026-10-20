// Vendor-direct integrations: OAuth 2.0 PKCE (RFC 7636), state values and base64url encoding.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;
using System.Text;

namespace CodeBase.VendorDirect {
	///<summary>Base64url encoding without padding (RFC 4648 section 5), as OAuth uses it for PKCE and state values.</summary>
	public static class Base64Url {
		public static string Encode(byte[] bytes) {
			if(bytes==null) {
				throw new ArgumentNullException(nameof(bytes));
			}
			return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
		}

		public static byte[] Decode(string text) {
			if(text==null) {
				throw new ArgumentNullException(nameof(text));
			}
			string base64=text.Replace('-','+').Replace('_','/');
			switch(base64.Length%4) {
				case 2: base64+="=="; break;
				case 3: base64+="="; break;
				case 1: throw new FormatException("Not a valid base64url value.");
			}
			return Convert.FromBase64String(base64);
		}

		///<summary>Returns count random bytes from the operating system's cryptographic generator, base64url encoded.</summary>
		public static string RandomString(int countBytes) {
			byte[] bytes=new byte[countBytes];
			using(RandomNumberGenerator rng=RandomNumberGenerator.Create()) {
				rng.GetBytes(bytes);
			}
			return Encode(bytes);
		}
	}

	///<summary>An RFC 7636 code verifier and its S256 code challenge. Create one per authorization attempt and keep the verifier in memory only.</summary>
	public sealed class Pkce {
		///<summary>The only challenge method this program sends. "plain" is never used.</summary>
		public const string ChallengeMethod="S256";
		private const string VERIFIER_CHARS="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

		///<summary>The code_verifier, sent only to the token endpoint.</summary>
		public string Verifier { get; }
		///<summary>The code_challenge, sent on the authorization URL.</summary>
		public string Challenge { get; }

		private Pkce(string verifier) {
			Verifier=verifier;
			Challenge=ComputeChallenge(verifier);
		}

		///<summary>A new verifier of 43 characters (32 random bytes, base64url), the length RFC 7636 recommends.</summary>
		public static Pkce Create() {
			return new Pkce(Base64Url.RandomString(32));
		}

		///<summary>Wraps an existing verifier, for tests and for vendors that hand one back. Throws if it is not a valid RFC 7636 verifier.</summary>
		public static Pkce FromVerifier(string verifier) {
			if(!IsValidVerifier(verifier)) {
				throw new ArgumentException("A PKCE code verifier must be 43 to 128 unreserved characters.",nameof(verifier));
			}
			return new Pkce(verifier);
		}

		///<summary>BASE64URL(SHA256(ASCII(verifier))).</summary>
		public static string ComputeChallenge(string verifier) {
			if(verifier==null) {
				throw new ArgumentNullException(nameof(verifier));
			}
			using(SHA256 sha=SHA256.Create()) {
				return Base64Url.Encode(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
			}
		}

		///<summary>True if verifier has 43 to 128 characters, all from [A-Z a-z 0-9 - . _ ~].</summary>
		public static bool IsValidVerifier(string verifier) {
			if(verifier==null || verifier.Length<43 || verifier.Length>128) {
				return false;
			}
			for(int i=0;i<verifier.Length;i++) {
				if(VERIFIER_CHARS.IndexOf(verifier[i])<0) {
					return false;
				}
			}
			return true;
		}
	}

	///<summary>The OAuth "state" value that ties a redirect back to the authorization request that started it.</summary>
	public static class OAuthState {
		///<summary>32 random bytes, base64url (43 characters).</summary>
		public static string New() {
			return Base64Url.RandomString(32);
		}

		///<summary>Compares the state that came back with the one that was sent, in constant time. False if either is null or empty.</summary>
		public static bool Matches(string expected,string actual) {
			if(string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual)) {
				return false;
			}
			return FixedTimeEquals(expected,actual);
		}

		///<summary>Constant-time string comparison: the time taken depends only on the lengths, not on where the strings differ.</summary>
		public static bool FixedTimeEquals(string a,string b) {
			if(a==null || b==null) {
				return false;
			}
			byte[] bytesA=Encoding.UTF8.GetBytes(a);
			byte[] bytesB=Encoding.UTF8.GetBytes(b);
			int diff=bytesA.Length ^ bytesB.Length;
			int length=Math.Max(bytesA.Length,bytesB.Length);
			for(int i=0;i<length;i++) {
				byte byteA=(i<bytesA.Length) ? bytesA[i] : (byte)0;
				byte byteB=(i<bytesB.Length) ? bytesB[i] : (byte)0;
				diff|=byteA ^ byteB;
			}
			return diff==0;
		}
	}
}
