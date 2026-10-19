// Vendor-direct integrations: the Gmail API program link's settings (the office's own Google OAuth client) and the per-address refresh token format.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>The office's own Google Cloud "Desktop app" OAuth client, kept in the "GmailApi" program link (docs/specs/vendor-direct/google.md,
	///section 3.3). The program row and its two property rows are inserted at runtime with Enabled=0; there is no schema change. The client ID is a
	///public identifier and is stored as typed. The client secret is stored as osd1$ in a masked, high-security row. Both rows use ClinicNum 0: one
	///client covers every clinic. Each email address keeps its own refresh token, encrypted, in emailaddress.RefreshToken.</summary>
	public static class GoogleOAuthSettings {
		///<summary>Short vendor name for logs and HTTP clients.</summary>
		public const string VendorName="Google";
		///<summary>The program row's description, shown in Setup > Program Links.</summary>
		public const string ProgDesc="Gmail API";
		///<summary>PropertyDesc of the client ID row (plain, ClinicNum 0).</summary>
		public const string PropertyClientId="Client ID";
		///<summary>PropertyDesc of the client secret row (osd1$, masked, high security, ClinicNum 0).</summary>
		public const string PropertyClientSecret="Client Secret";
		///<summary>OpenID Connect: asks for an id_token, used to check which account was authorized.</summary>
		public const string ScopeOpenId="openid";
		///<summary>Puts the email and email_verified claims in the id_token.</summary>
		public const string ScopeEmail="email";
		///<summary>Send mail as the mailbox. Google classes it as sensitive.</summary>
		public const string ScopeGmailSend="https://www.googleapis.com/auth/gmail.send";
		///<summary>Read mail, for "Download incoming emails" only. Google classes it as restricted.</summary>
		public const string ScopeGmailReadOnly="https://www.googleapis.com/auth/gmail.readonly";
		///<summary>Shown when Authorize is clicked before the office has entered its OAuth client.</summary>
		public const string MessageNotConfiguredAuthorize="Enter the OAuth client ID in Setup > Program Links > Gmail API first.";
		///<summary>Shown when a Google address sends before the office has entered its OAuth client.</summary>
		public const string MessageNotConfiguredSend="Gmail sending is not configured. An administrator can set it up in Setup > Program Links > Gmail API.";

		///<summary>Inserts the GmailApi program row (Enabled=0, no path, no toolbar button) and its Client ID and Client Secret rows if they are missing.
		///Returns true if it inserted anything; it then signals InvalidType.Programs (the UI caller also runs DataValid.SetInvalid). Never calls Google.
		///Safe on two workstations at once: Programs.InsertIfMissing and ProgramProperties.InsertIfMissing remove a racing duplicate.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.GmailApi,ProgDesc);
			Program program=Programs.GetCur(ProgramName.GmailApi);
			if(program==null) {
				Programs.RefreshCache();//Inserted just now, here or on another workstation.
				program=Programs.GetCur(ProgramName.GmailApi);
			}
			if(program==null) {
				return isInserted;
			}
			if(!HasPropertyRow(program.ProgramNum,PropertyClientId)) {
				VendorCredentials.EnsureSetting(program.ProgramNum,PropertyClientId);
				isInserted=true;
			}
			ProgramProperty programPropertySecret=ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum && x.PropertyDesc==PropertyClientSecret
				&& x.ClinicNum==0).FirstOrDefault();
			if(programPropertySecret==null || !programPropertySecret.IsMasked || !programPropertySecret.IsHighSecurity) {
				VendorCredentials.EnsureSecret(program.ProgramNum,PropertyClientSecret);//Also raises the flags on an existing row.
				isInserted=true;
			}
			if(isInserted) {
				Signalods.SetInvalid(InvalidType.Programs);
			}
			return isInserted;
		}

		private static bool HasPropertyRow(long programNum,string desc) {
			return ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==desc && x.ClinicNum==0).Count>0;
		}

		///<summary>The GmailApi program's key, or 0 if the row doesn't exist yet.</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			return Programs.GetProgramNum(ProgramName.GmailApi);
		}

		///<summary>True if the GmailApi program link is enabled.</summary>
		public static bool IsEnabled {
			get {
				return Programs.IsEnabled(ProgramName.GmailApi);
			}
		}

		///<summary>The office's OAuth client ID, trimmed, or "".</summary>
		public static string GetClientId() {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return "";
			}
			return ProgramProperties.GetValOrDefault(programNum,PropertyClientId,0).Trim();
		}

		///<summary>Reads the stored client secret. A secret typed into the old plain-text row (no osd1$ prefix) is accepted and encrypted on the next
		///save; an unreadable value counts as missing.</summary>
		public static SecretReadResult ReadClientSecret() {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return new SecretReadResult(SecretReadStatus.Blank,"");
			}
			return VendorCredentials.ReadSecret(programNum,PropertyClientSecret,0,LegacyPolicy.AcceptPlaintext);
		}

		///<summary>The decrypted client secret, or "" if none or unreadable. Sent only to Google's token endpoint.</summary>
		public static string GetClientSecret() {
			Meth.NoCheckMiddleTierRole();
			return ReadClientSecret().PlainText;
		}

		///<summary>Stores the client ID as typed (trimmed). Does not refresh the cache; the caller signals InvalidType.Programs.</summary>
		public static void SetClientId(string clientId) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				throw new ODException("The Gmail API program link is missing. Open Setup > Program Links once to add it.");
			}
			VendorCredentials.WriteSetting(programNum,PropertyClientId,0,(clientId??"").Trim());
		}

		///<summary>Encrypts and stores the client secret (masked, high security). Returns false, writing nothing, if encryption failed: the caller must
		///refuse to save. Blank clears it. Does not refresh the cache; the caller signals InvalidType.Programs.</summary>
		public static bool SetClientSecret(string clientSecret) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				throw new ODException("The Gmail API program link is missing. Open Setup > Program Links once to add it.");
			}
			return VendorCredentials.WriteSecret(programNum,PropertyClientSecret,0,(clientSecret??"").Trim());
		}

		///<summary>True once the office has entered a client ID. Without one, nothing calls Google.</summary>
		public static bool IsConfigured {
			get {
				return GetClientId()!="";
			}
		}

		///<summary>A fingerprint of the OAuth client (the client ID and the stored, encrypted secret), so a changed client gives fresh cached tokens.</summary>
		public static string GetClientFingerprint() {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return TokenCache.Fingerprint("");
			}
			return VendorCredentials.GetFingerprint(programNum,0,PropertyClientId,PropertyClientSecret);
		}

		///<summary>The client ID and decrypted secret as one value, for the token calls.</summary>
		public static GoogleClientCredentials GetClientCredentials() {
			Meth.NoCheckMiddleTierRole();
			return new GoogleClientCredentials(GetClientId(),GetClientSecret(),GetClientFingerprint());
		}

		///<summary>The scopes to request: openid, email and gmail.send always; gmail.readonly only when the address downloads mail. Google doesn't
		///support incremental authorization for installed apps, so the whole set is requested every time.</summary>
		public static List<string> GetScopes(bool isDownloadingMail) {
			List<string> listScopes=new List<string> { ScopeOpenId,ScopeEmail,ScopeGmailSend };
			if(isDownloadingMail) {
				listScopes.Add(ScopeGmailReadOnly);
			}
			return listScopes;
		}

		///<summary>True if the space-separated grantedScopes (from a token response) include scope. Google may return the email scope as its long
		///form, "https://www.googleapis.com/auth/userinfo.email".</summary>
		public static bool HasScope(string grantedScopes,string scope) {
			if(string.IsNullOrWhiteSpace(grantedScopes) || string.IsNullOrEmpty(scope)) {
				return false;
			}
			string[] arrayScopes=grantedScopes.Split(new[] { ' ' },StringSplitOptions.RemoveEmptyEntries);
			if(arrayScopes.Contains(scope,StringComparer.Ordinal)) {
				return true;
			}
			if(scope==ScopeEmail) {
				return arrayScopes.Contains("https://www.googleapis.com/auth/userinfo.email",StringComparer.Ordinal);
			}
			return false;
		}

		///<summary>True for a refresh token that this program can't use: a non-blank value without the osd1$ prefix. Those are plain-text tokens that
		///Open Dental's own OAuth client issued, and only Open Dental's client secret can refresh them. The address has to be authorized again.</summary>
		public static bool IsLegacyToken(string storedRefreshToken) {
			if(string.IsNullOrEmpty(storedRefreshToken)) {
				return false;
			}
			return !Secrets.IsEncrypted(storedRefreshToken);
		}

		///<summary>Encrypts a refresh token for emailaddress.RefreshToken. Returns false, with stored null, if encryption failed; the caller must
		///refuse to save (never fall back to plain text).</summary>
		public static bool ProtectRefreshToken(string refreshToken,out string stored) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(refreshToken)) {
				stored=null;
				return false;
			}
			return SecretValue.Protect(refreshToken,out stored);
		}

		///<summary>Reads emailaddress.RefreshToken. A plain-text (legacy) value is rejected: it was issued to Open Dental's client.</summary>
		public static SecretReadResult ReadRefreshToken(string storedRefreshToken) {
			Meth.NoCheckMiddleTierRole();
			return SecretValue.Read(storedRefreshToken,LegacyPolicy.RejectPlaintext);
		}
	}

	///<summary>The office's OAuth client as the token calls need it. Fingerprint identifies the stored configuration (never the plain secret).</summary>
	public sealed class GoogleClientCredentials {
		public string ClientId { get; }
		///<summary>The plain client secret, or "" if none is stored. Google issues one for Desktop clients; it is sent whenever configured.</summary>
		public string ClientSecret { get; }
		public string Fingerprint { get; }

		public GoogleClientCredentials(string clientId,string clientSecret,string fingerprint=null) {
			ClientId=(clientId??"").Trim();
			ClientSecret=clientSecret??"";
			Fingerprint=fingerprint??TokenCache.Fingerprint(ClientId,ClientSecret);
		}

		public bool IsConfigured {
			get {
				return ClientId!="";
			}
		}
	}
}
