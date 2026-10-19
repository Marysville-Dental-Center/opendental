// Vendor-direct Microsoft 365 email: the exceptions the integration throws, and how Microsoft's error codes map to neutral messages.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;

namespace OpenDentBusiness.Microsoft365 {
	///<summary>What went wrong, in the terms the program acts on. See docs/specs/vendor-direct/microsoft-365.md, section 3.5.</summary>
	public enum Microsoft365ErrorKind {
		///<summary>Anything not listed below.</summary>
		General,
		///<summary>The program link is off, or the client ID or tenant is blank. No network call was made.</summary>
		NotConfigured,
		///<summary>The mailbox has to sign in again: not signed in, a cache from Open Dental's registration, a revoked or expired refresh token.</summary>
		NeedsSignIn,
		///<summary>Graph refused for lack of a permission (scope). Signing in again asks for it.</summary>
		MissingPermission,
		///<summary>The user cancelled the sign-in, or it timed out.</summary>
		Cancelled,
		///<summary>A different account signed in than the one the address names.</summary>
		WrongAccount,
		///<summary>The app registration was not found in the tenant (wrong client ID or tenant).</summary>
		AppRegistration,
		///<summary>The redirect URI is not registered on the app.</summary>
		RedirectUri,
		///<summary>Nobody granted consent for the app's permissions.</summary>
		Consent,
		///<summary>Conditional Access or user assignment blocked the sign-in.</summary>
		AccessBlocked,
		///<summary>The account has no Exchange Online mailbox.</summary>
		NoMailbox,
		///<summary>The message is larger than Microsoft 365 accepts.</summary>
		TooLarge,
		///<summary>Microsoft 365 throttled the call (429).</summary>
		Throttled,
		///<summary>DNS, connection, TLS or timeout failure, or the circuit breaker is open.</summary>
		Unreachable,
		///<summary>A send timed out or failed on the server after it may already have been queued. It is never retried automatically.</summary>
		SendOutcomeUnknown,
	}

	///<summary>A failed Microsoft 365 call. Message is neutral and safe to show; it never contains a token, a cache, a subject or a body.
	///For one release InnerException is never null and its message starts with the Graph or identity error code, so code that still checks
	///ex.InnerException.Message.Contains("InvalidAuthenticationToken") keeps working until EMAIL-UI moves it to IsAuthError.</summary>
	public class Microsoft365Exception : Exception {
		///<summary>The HTTP status from Graph or the token endpoint, or 0.</summary>
		public int HttpStatus { get; }
		///<summary>Graph's error.code ("ErrorAccessDenied"), or the identity platform's error ("invalid_grant"), or "".</summary>
		public string GraphErrorCode { get; }
		///<summary>Graph's request-id header, for support calls, or "".</summary>
		public string RequestId { get; }
		public Microsoft365ErrorKind Kind { get; }

		public Microsoft365Exception(Microsoft365ErrorKind kind,string message,int httpStatus=0,string graphErrorCode="",string requestId="",
			Exception innerException=null)
			: base(message,innerException??new Exception(BuildInnerMessage(graphErrorCode,httpStatus,kind)))
		{
			Kind=kind;
			HttpStatus=httpStatus;
			GraphErrorCode=graphErrorCode??"";
			RequestId=requestId??"";
		}

		///<summary>True if signing in again is what fixes it (Microsoft365AuthException).</summary>
		public virtual bool IsAuthError {
			get {
				return false;
			}
		}

		private static string BuildInnerMessage(string graphErrorCode,int httpStatus,Microsoft365ErrorKind kind) {
			string code=string.IsNullOrEmpty(graphErrorCode) ? kind.ToString() : graphErrorCode;
			return code+(httpStatus==0 ? "" : " (HTTP "+httpStatus+")");
		}
	}

	///<summary>The mailbox has to sign in to Microsoft 365 again (or grant a missing permission). Background callers never prompt; they report it.</summary>
	public class Microsoft365AuthException : Microsoft365Exception {
		public Microsoft365AuthException(Microsoft365ErrorKind kind,string message,int httpStatus=0,string graphErrorCode="",string requestId="",
			Exception innerException=null)
			: base(kind,message,httpStatus,graphErrorCode,requestId,innerException)
		{
		}

		public override bool IsAuthError {
			get {
				return true;
			}
		}
	}

	///<summary>Maps identity-platform and Graph errors to Microsoft365ErrorKind and a neutral message. Pure functions: no network, no database.</summary>
	public static class Microsoft365Errors {
		public const string MessageNotConfigured="Microsoft 365 email is not set up. An administrator can configure it in Setup > Program Links.";
		public const string MessageNotConfiguredSetup="Enter the Application (client) ID and Directory (tenant) ID in Setup > Program Links > "
			+"Microsoft 365 email first.";
		public const string MessageTurnedOff="Microsoft 365 email is turned off. An administrator can enable it in Setup > Program Links.";
		public const string MessageNeedsSignIn="This address needs to sign in to Microsoft 365 again. Please sign out and back in to continue.";
		public const string MessageMissingPermission="Microsoft 365 refused access. Sign in again so the program can ask for the permission it needs.";
		public const string MessageAppRegistration="The app registration was not found in this tenant. Check the Application (client) ID and "
			+"Directory (tenant) ID.";
		public const string MessageConsent="An administrator must grant consent for this app (Microsoft Entra admin center > App registrations > "
			+"API permissions).";
		public const string MessageAccessBlocked="Microsoft 365 refused this sign-in. The office's IT administrator has to allow it.";
		public const string MessageNoMailbox="This account has no Exchange Online mailbox.";
		public const string MessageTooLarge="The message is larger than Microsoft 365 accepts for this mailbox.";
		public const string MessageThrottled="Microsoft 365 is busy; try again.";
		public const string MessageUnreachable="Microsoft 365 could not be reached.";
		public const string MessageSendOutcomeUnknown="Microsoft 365 did not confirm that the message was sent. Check the mailbox's Sent Items "
			+"before sending it again.";
		public const string MessageCancelled="Sign-in was cancelled.";

		///<summary>"Add http://localhost as a Mobile and desktop applications redirect URI in the app registration."</summary>
		public static string GetMessageRedirectUri(string redirectUri) {
			string uri=string.IsNullOrWhiteSpace(redirectUri) ? Microsoft365Program.DefaultRedirectUri : redirectUri.Trim();
			return "Add "+uri+" as a Mobile and desktop applications redirect URI in the app registration.";
		}

		///<summary>The AADSTS number in an identity-platform message ("AADSTS700016: ..." gives "700016"), or "".</summary>
		public static string GetAadstsCode(string description) {
			if(string.IsNullOrEmpty(description)) {
				return "";
			}
			Match match=Regex.Match(description,@"AADSTS(\d+)");
			return match.Success ? match.Groups[1].Value : "";
		}

		///<summary>Classifies an identity-platform (MSAL) error. errorCode is MSAL's ErrorCode ("invalid_grant", "authentication_canceled", ...);
		///description is its message, which carries the AADSTS number.</summary>
		public static Microsoft365ErrorKind ClassifyIdentityError(string errorCode,string description) {
			errorCode=errorCode??"";
			string aadsts=GetAadstsCode(description);
			switch(aadsts) {
				case "700016":
				case "90002":
					return Microsoft365ErrorKind.AppRegistration;
				case "50011":
					return Microsoft365ErrorKind.RedirectUri;
				case "65001":
					return Microsoft365ErrorKind.Consent;
				case "53003":
				case "50105":
					return Microsoft365ErrorKind.AccessBlocked;
				case "50173":
				case "700082":
				case "700084":
				case "50076":
				case "50079":
					return Microsoft365ErrorKind.NeedsSignIn;
			}
			switch(errorCode) {
				case "authentication_canceled":
				case "authentication_ui_failed":
					return Microsoft365ErrorKind.Cancelled;
				case "consent_required":
					return Microsoft365ErrorKind.Consent;
				case "invalid_grant":
				case "interaction_required":
				case "login_required":
				case "no_tokens_found":
				case "no_account_for_login_hint":
				case "multiple_accounts_for_login_hint":
				case "user_null":
				case "token_cache_null":
				case "bad_token":
					return Microsoft365ErrorKind.NeedsSignIn;
				case "unauthorized_client":
				case "invalid_client":
				case "invalid_instance":
				case "invalid_authority":
					return Microsoft365ErrorKind.AppRegistration;
				case "request_timeout":
				case "service_not_available":
				case "network_not_available":
				case "temporarily_unavailable":
					return Microsoft365ErrorKind.Unreachable;
			}
			return Microsoft365ErrorKind.General;
		}

		///<summary>Classifies a Graph error response.</summary>
		public static Microsoft365ErrorKind ClassifyGraphError(int httpStatus,string graphErrorCode) {
			graphErrorCode=graphErrorCode??"";
			if(httpStatus==401 || graphErrorCode=="InvalidAuthenticationToken") {
				return Microsoft365ErrorKind.NeedsSignIn;
			}
			if(graphErrorCode=="MailboxNotEnabledForRESTAPI" || graphErrorCode=="MailboxNotSupportedForRESTAPI"
				|| (httpStatus==404 && graphErrorCode=="ResourceNotFound"))
			{
				return Microsoft365ErrorKind.NoMailbox;
			}
			if(httpStatus==413 || graphErrorCode=="RequestBodyTooLarge" || graphErrorCode=="ErrorMessageSizeExceeded") {
				return Microsoft365ErrorKind.TooLarge;
			}
			if(httpStatus==403 || graphErrorCode=="ErrorAccessDenied" || graphErrorCode=="Authorization_RequestDenied") {
				return Microsoft365ErrorKind.MissingPermission;
			}
			if(httpStatus==429) {
				return Microsoft365ErrorKind.Throttled;
			}
			return Microsoft365ErrorKind.General;
		}

		///<summary>The neutral message for a kind. Kinds whose message depends on details (General, AccessBlocked, RedirectUri) use details.</summary>
		public static string GetMessage(Microsoft365ErrorKind kind,string details="",string redirectUri="") {
			switch(kind) {
				case Microsoft365ErrorKind.NotConfigured:
					return MessageNotConfigured;
				case Microsoft365ErrorKind.NeedsSignIn:
					return MessageNeedsSignIn;
				case Microsoft365ErrorKind.MissingPermission:
					return MessageMissingPermission;
				case Microsoft365ErrorKind.Cancelled:
					return MessageCancelled;
				case Microsoft365ErrorKind.WrongAccount:
					return "A different Microsoft account signed in than the one this address uses.";
				case Microsoft365ErrorKind.AppRegistration:
					return MessageAppRegistration;
				case Microsoft365ErrorKind.RedirectUri:
					return GetMessageRedirectUri(redirectUri);
				case Microsoft365ErrorKind.Consent:
					return MessageConsent;
				case Microsoft365ErrorKind.AccessBlocked:
					return MessageAccessBlocked+(string.IsNullOrWhiteSpace(details) ? "" : " "+SanitizeDetails(details));
				case Microsoft365ErrorKind.NoMailbox:
					return MessageNoMailbox;
				case Microsoft365ErrorKind.TooLarge:
					return MessageTooLarge;
				case Microsoft365ErrorKind.Throttled:
					return MessageThrottled;
				case Microsoft365ErrorKind.Unreachable:
					return MessageUnreachable;
				case Microsoft365ErrorKind.SendOutcomeUnknown:
					return MessageSendOutcomeUnknown;
			}
			return "Microsoft 365 returned an error."+(string.IsNullOrWhiteSpace(details) ? "" : " "+SanitizeDetails(details));
		}

		///<summary>An exception for an identity-platform error. NeedsSignIn, MissingPermission and Consent give Microsoft365AuthException.</summary>
		public static Microsoft365Exception FromIdentityError(string errorCode,string description,int httpStatus=0,string redirectUri="",
			Exception innerException=null)
		{
			Microsoft365ErrorKind kind=ClassifyIdentityError(errorCode,description);
			string message=GetMessage(kind,description,redirectUri);
			string code=string.IsNullOrEmpty(errorCode) ? "" : errorCode;
			string aadsts=GetAadstsCode(description);
			if(aadsts!="") {
				code=(code=="" ? "" : code+" ")+"AADSTS"+aadsts;
			}
			if(kind==Microsoft365ErrorKind.NeedsSignIn || kind==Microsoft365ErrorKind.Consent) {
				return new Microsoft365AuthException(kind,message,httpStatus,code,"",innerException);
			}
			return new Microsoft365Exception(kind,message,httpStatus,code,"",innerException);
		}

		///<summary>An exception for a Graph error response.</summary>
		public static Microsoft365Exception FromGraphError(int httpStatus,string graphErrorCode,string graphMessage,string requestId) {
			Microsoft365ErrorKind kind=ClassifyGraphError(httpStatus,graphErrorCode);
			string details=(kind==Microsoft365ErrorKind.General) ? "HTTP "+httpStatus+(string.IsNullOrEmpty(graphErrorCode) ? "" : ", "+graphErrorCode) : "";
			string message=GetMessage(kind,details);
			if(kind==Microsoft365ErrorKind.NeedsSignIn || kind==Microsoft365ErrorKind.MissingPermission) {
				return new Microsoft365AuthException(kind,message,httpStatus,graphErrorCode,requestId);
			}
			return new Microsoft365Exception(kind,message,httpStatus,graphErrorCode,requestId);
		}

		///<summary>Keeps Microsoft's error description short and free of anything that looks like a token: drops long base64-like runs.</summary>
		public static string SanitizeDetails(string details) {
			if(string.IsNullOrWhiteSpace(details)) {
				return "";
			}
			string sanitized=Regex.Replace(details,@"[A-Za-z0-9_\-\.]{60,}","[...]");
			sanitized=sanitized.Replace("\r"," ").Replace("\n"," ").Trim();
			if(sanitized.Length>300) {
				sanitized=sanitized.Substring(0,300)+"...";
			}
			return sanitized;
		}
	}
}
