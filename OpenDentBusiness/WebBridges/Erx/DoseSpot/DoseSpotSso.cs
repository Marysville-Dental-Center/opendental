// DoseSpot eRx: single sign-on values computed locally from the clinic key (no network call), replacing the v1 SOAP SingleSignOn type.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenDentBusiness {
	///<summary>The values that open DoseSpot's own prescribing screen for one user at one clinic. Every instance has its own random phrase from a
	///cryptographic RNG; a phrase is never shared between requests or threads (DoseSpot flags reused phrases). The algorithm is the one the in-repo
	///client has always used; confirm it against DoseSpot's partner guide before going live:
	///code = phrase + Base64(SHA-512(phrase + clinicKey)) without a trailing "==";
	///user verification = Base64(SHA-512(userId + first 22 characters of phrase + clinicKey)) without a trailing "==".</summary>
	public sealed class DoseSpotSso {
		public const int PhraseLength=32;
		private const string PHRASE_CHARS="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

		public string ClinicId { get; private set; }
		public string UserId { get; private set; }
		///<summary>Not URL-encoded.</summary>
		public string SingleSignOnCode { get; private set; }
		///<summary>Not URL-encoded.</summary>
		public string SingleSignOnUserIdVerify { get; private set; }

		///<summary>Builds the values for clinicId, clinicKey and userId. phrase is for tests (a known answer); null makes a fresh random one.</summary>
		public static DoseSpotSso Create(string clinicId,string clinicKey,string userId,string phrase=null) {
			if(string.IsNullOrWhiteSpace(clinicKey)) {
				throw new ArgumentException("A clinic key is required.",nameof(clinicKey));
			}
			phrase=phrase??CreatePhrase();
			DoseSpotSso doseSpotSso=new DoseSpotSso();
			doseSpotSso.ClinicId=(clinicId??"").Trim();
			doseSpotSso.UserId=(userId??"").Trim();
			doseSpotSso.SingleSignOnCode=ComputeCode(phrase,clinicKey.Trim());
			doseSpotSso.SingleSignOnUserIdVerify=ComputeUserIdVerify(phrase,doseSpotSso.UserId,clinicKey.Trim());
			return doseSpotSso;
		}

		///<summary>A random phrase of PhraseLength letters and digits from a cryptographic RNG.</summary>
		public static string CreatePhrase() {
			StringBuilder stringBuilder=new StringBuilder(PhraseLength);
			byte[] arrayBytes=new byte[1];
			using(RNGCryptoServiceProvider rng=new RNGCryptoServiceProvider()) {
				while(stringBuilder.Length<PhraseLength) {
					rng.GetBytes(arrayBytes);
					//248 is the largest multiple of 62 below 256: rejecting the rest keeps every character equally likely.
					if(arrayBytes[0]>=248) {
						continue;
					}
					stringBuilder.Append(PHRASE_CHARS[arrayBytes[0]%PHRASE_CHARS.Length]);
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>phrase + Base64(SHA-512(phrase + clinicKey)), trailing "==" removed.</summary>
		public static string ComputeCode(string phrase,string clinicKey) {
			CheckPhrase(phrase);
			return phrase+HashBase64(phrase+clinicKey);
		}

		///<summary>Base64(SHA-512(userId + phrase[0..22) + clinicKey)), trailing "==" removed.</summary>
		public static string ComputeUserIdVerify(string phrase,string userId,string clinicKey) {
			CheckPhrase(phrase);
			return HashBase64(userId+phrase.Substring(0,22)+clinicKey);
		}

		///<summary>The query string for DoseSpot's single sign-on page, starting with '&amp;'. patientId blank and isRefillsErrors false opens DoseSpot's
		///home page; isRefillsErrors opens the refill and transmission-error queue. Every value is URL-encoded once.</summary>
		public string ToQueryString(string onBehalfOfUserId,string patientId,bool isRefillsErrors) {
			StringBuilder stringBuilder=new StringBuilder();
			Append(stringBuilder,"SingleSignOnCode",SingleSignOnCode);
			Append(stringBuilder,"SingleSignOnUserId",UserId);
			Append(stringBuilder,"SingleSignOnUserIdVerify",SingleSignOnUserIdVerify);
			Append(stringBuilder,"SingleSignOnClinicId",ClinicId);
			if(!string.IsNullOrWhiteSpace(onBehalfOfUserId)) {
				Append(stringBuilder,"OnBehalfOfUserId",onBehalfOfUserId.Trim());
			}
			if(isRefillsErrors) {
				Append(stringBuilder,"RefillsErrors","1");
			}
			else if(!string.IsNullOrWhiteSpace(patientId)) {
				Append(stringBuilder,"PatientId",patientId);
			}
			return stringBuilder.ToString();
		}

		///<summary>The query string or URL with the single sign-on code and user verification replaced by ***, for erxlog and any other log. The
		///clinic ID, user ID and patient ID stay: they are identifiers, not secrets.</summary>
		public static string Redact(string queryOrUrl) {
			if(string.IsNullOrEmpty(queryOrUrl)) {
				return queryOrUrl??"";
			}
			return Regex.Replace(queryOrUrl,@"(?i)((?:^|[?&])SingleSignOn(?:Code|UserIdVerify)=)[^&]*","$1***");
		}

		private static void Append(StringBuilder stringBuilder,string name,string value) {
			stringBuilder.Append("&").Append(name).Append("=").Append(Uri.EscapeDataString(value??""));
		}

		private static void CheckPhrase(string phrase) {
			if(phrase==null || phrase.Length<22) {
				throw new ArgumentException("The phrase must be at least 22 characters long.",nameof(phrase));
			}
		}

		private static string HashBase64(string value) {
			byte[] arrayHash;
			using(SHA512 sha512=SHA512.Create()) {
				arrayHash=sha512.ComputeHash(Encoding.UTF8.GetBytes(value));
			}
			string base64=Convert.ToBase64String(arrayHash);
			if(base64.EndsWith("==")) {
				base64=base64.Substring(0,base64.Length-2);
			}
			return base64;
		}
	}
}
