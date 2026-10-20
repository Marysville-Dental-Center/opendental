// QuickBooks Online (direct): the consent request, the redirect rules, and reading what comes back (loopback, relay page or a pasted address).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase.VendorDirect;

namespace OpenDentBusiness.Qbo {
	///<summary>One connection attempt: the consent URL to open in the system browser, the state to check, and how the redirect comes back. The state
	///lives in memory only, for this attempt.</summary>
	public sealed class QboAuthRequest {
		///<summary>The relay page forwards to this path on this computer.</summary>
		public const string RelayPath="/callback";

		///<summary>Intuit's consent page with client_id, response_type, scope, redirect_uri and state.</summary>
		public string Url { get; }
		///<summary>32 random bytes, base64url.</summary>
		public string State { get; }
		///<summary>Exactly as registered and saved; sent on the consent URL and again on the code exchange.</summary>
		public string RedirectUri { get; }
		public QboEnvironment Environment { get; }
		///<summary>The client ID the consent URL was built with. The exchange refuses to run if the saved one changed since.</summary>
		public string ClientId { get; }
		///<summary>True when the redirect arrives on this computer: a sandbox http://localhost redirect, or the relay page.</summary>
		public bool UsesLoopback { get; }
		///<summary>True when the loopback request comes from the office's relay page rather than straight from Intuit.</summary>
		public bool UsesRelay { get; }
		///<summary>Valid when UsesLoopback.</summary>
		public int LoopbackPort { get; }
		///<summary>Valid when UsesLoopback.</summary>
		public string LoopbackPath { get; }
		///<summary>"localhost", "127.0.0.1" or "[::1]". Valid when UsesLoopback.</summary>
		public string LoopbackHost { get; }

		///<summary>A new attempt with a fresh state (or the given one, for tests). Doesn't validate; QboRedirectRules.Validate does.</summary>
		public static QboAuthRequest Create(string clientId,string redirectUri,QboEnvironment environment,int relayPort,string state=null) {
			string stateUsed=string.IsNullOrEmpty(state) ? OAuthState.New() : state;
			string url=QboOAuth.BuildAuthorizeUrl(clientId,redirectUri,stateUsed);
			return new QboAuthRequest(url,stateUsed,redirectUri,environment,clientId,relayPort);
		}

		private QboAuthRequest(string url,string state,string redirectUri,QboEnvironment environment,string clientId,int relayPort) {
			Url=url;
			State=state;
			RedirectUri=redirectUri;
			Environment=environment;
			ClientId=clientId;
			Uri uriRedirect=new Uri(redirectUri);
			if(QboRedirectRules.IsLoopbackRedirect(redirectUri)) {
				UsesLoopback=true;
				LoopbackPort=uriRedirect.Port;
				LoopbackPath=uriRedirect.AbsolutePath;
				LoopbackHost=QboRedirectRules.GetListenerHost(uriRedirect.Host);
			}
			else if(relayPort>0) {
				UsesLoopback=true;
				UsesRelay=true;
				LoopbackPort=relayPort;
				LoopbackPath=RelayPath;
				LoopbackHost="localhost";
			}
		}

		///<summary>A listener for the loopback or relay redirect, not yet started. Start it before opening the browser. Null when the paste flow is used.</summary>
		public LoopbackListener CreateLoopbackListener() {
			if(!UsesLoopback) {
				return null;
			}
			return new LoopbackListener(LoopbackPort,LoopbackPath,LoopbackHost);
		}
	}

	///<summary>What came back from Intuit's consent page, however it arrived.</summary>
	public sealed class QboAuthorizationResult {
		public string Code { get; }
		///<summary>Null when the values were typed in separately (the old two-box entry), which can't be checked.</summary>
		public string State { get; }
		public string RealmId { get; }
		public string Error { get; }
		public string ErrorDescription { get; }
		///<summary>True when the code and realm ID were typed in separately, so there is no state to check.</summary>
		public bool IsEnteredSeparately { get; }

		public QboAuthorizationResult(string code,string state,string realmId,string error,string errorDescription,bool isEnteredSeparately) {
			Code=(code??"").Trim();
			State=state;
			RealmId=(realmId??"").Trim();
			Error=error??"";
			ErrorDescription=errorDescription??"";
			IsEnteredSeparately=isEnteredSeparately;
		}

		///<summary>From the loopback listener or a parsed address.</summary>
		public static QboAuthorizationResult FromRedirect(OAuthRedirectResult result) {
			if(result==null) {
				return null;
			}
			return new QboAuthorizationResult(result.Code,result.State,result.Get("realmId"),result.Error,result.ErrorDescription,false);
		}

		///<summary>From the separate code and realm ID boxes.</summary>
		public static QboAuthorizationResult FromSeparateValues(string code,string realmId) {
			return new QboAuthorizationResult(code,null,realmId,"","",true);
		}

		///<summary>Checks the result against the request it answers. Throws QboAuthException with a message for the user. The state is compared in
		///constant time, and is skipped only for values typed in separately.</summary>
		public void Validate(QboAuthRequest request) {
			if(Error!="") {
				string message="QuickBooks Online did not approve the connection ("+Error+").";
				if(ErrorDescription!="") {
					message+=" "+ErrorDescription;
				}
				throw new QboAuthException(message,needsReconnect:false,oAuthError:Error);
			}
			if(!IsEnteredSeparately && !OAuthState.Matches(request.State,State)) {
				throw new QboAuthException(QboAuthException.MessageStateMismatch,needsReconnect:false);
			}
			if(Code=="") {
				throw new QboAuthException("The address has no authorization code. Copy the full address from the browser's address bar.",needsReconnect:false);
			}
			if(!QboRedirectRules.IsValidRealmId(RealmId)) {
				throw new QboAuthException("The address has no valid company ID (realmId). Copy the full address from the browser's address bar.",
					needsReconnect:false);
			}
		}
	}

	///<summary>Reads the address (or query string) the user pastes after approving on Intuit's page.</summary>
	public static class QboRedirectParser {
		///<summary>Accepts a full redirected address or a bare query string, URL-encoded or not, and pulls out code, state and realmId (and error).
		///Returns null for blank text or text it can't read.</summary>
		public static QboAuthorizationResult Parse(string text) {
			OAuthRedirectResult result=OAuthRedirectParser.Parse(text);
			if(result==null) {
				return null;
			}
			if(result.Parameters.Count==1 && result.Code!=null && !(text??"").Contains("=")) {
				return null;//A bare code. Without the realm ID it is useless here; the separate boxes cover that case.
			}
			return QboAuthorizationResult.FromRedirect(result);
		}
	}

	///<summary>Intuit's redirect URI rules, checked before anything is opened or saved.</summary>
	public static class QboRedirectRules {
		public const string MessageProductionHttps="Intuit only accepts HTTPS redirect addresses for production keys.";

		///<summary>"" if redirectUri can be used with the environment's keys, otherwise a message for the user.</summary>
		public static string Validate(QboEnvironment environment,string redirectUri) {
			if(string.IsNullOrWhiteSpace(redirectUri)) {
				return "Enter the redirect URI registered for your Intuit app.";
			}
			Uri uri;
			if(!Uri.TryCreate(redirectUri.Trim(),UriKind.Absolute,out uri) || (uri.Scheme!=Uri.UriSchemeHttps && uri.Scheme!=Uri.UriSchemeHttp)
				|| string.IsNullOrEmpty(uri.Host))
			{
				return "The redirect URI must be a full address that starts with https:// (or http://localhost for sandbox keys).";
			}
			if(uri.Fragment!="" || redirectUri.Contains("#")) {
				return "The redirect URI can't contain '#'.";
			}
			if(uri.UserInfo!="") {
				return "The redirect URI can't contain a user name or password.";
			}
			if(HqHosts.IsBlocked(uri)) {
				return VendorUrlPolicy.Describe(VendorUrlProblem.BlockedHost);
			}
			bool isLoopbackHost=VendorUrlPolicy.IsLoopbackHost(uri.Host);
			bool isIpLiteral=(uri.HostNameType==UriHostNameType.IPv4 || uri.HostNameType==UriHostNameType.IPv6);
			if(environment==QboEnvironment.Production) {
				if(uri.Scheme!=Uri.UriSchemeHttps || isLoopbackHost || isIpLiteral || IsLocalhostName(uri.Host)) {
					return MessageProductionHttps;
				}
				return "";
			}
			if(uri.Scheme==Uri.UriSchemeHttp && !isLoopbackHost) {
				return "Use an https:// address, or http://localhost:<port>/<path> with sandbox keys.";
			}
			if(uri.Scheme==Uri.UriSchemeHttp && uri.Query!="") {
				return "A localhost redirect URI can't have a query string.";
			}
			return "";
		}

		///<summary>"" if the relay port is blank or valid for this redirect, otherwise a message.</summary>
		public static string ValidateRelayPort(string relayPortText) {
			if(string.IsNullOrWhiteSpace(relayPortText)) {
				return "";
			}
			if(QboSettings.ParsePort(relayPortText)==0) {
				return "The relay port must be a number from 1 to 65535, or blank.";
			}
			return "";
		}

		///<summary>True for http://localhost, http://127.0.0.1 and http://[::1] redirects, which the listener on this computer catches (sandbox keys only).</summary>
		public static bool IsLoopbackRedirect(string redirectUri) {
			Uri uri;
			if(!Uri.TryCreate((redirectUri??"").Trim(),UriKind.Absolute,out uri)) {
				return false;
			}
			return uri.Scheme==Uri.UriSchemeHttp && VendorUrlPolicy.IsLoopbackHost(uri.Host) && GetListenerHost(uri.Host)!=null;
		}

		///<summary>The LoopbackListener host for a loopback redirect host, or null if the listener can't serve it.</summary>
		internal static string GetListenerHost(string host) {
			string hostLower=(host??"").ToLowerInvariant();
			if(hostLower=="localhost" || hostLower=="127.0.0.1" || hostLower=="[::1]") {
				return hostLower;
			}
			return null;
		}

		private static bool IsLocalhostName(string host) {
			string hostLower=(host??"").ToLowerInvariant().TrimEnd('.');
			return hostLower=="localhost" || hostLower.EndsWith(".localhost",StringComparison.Ordinal);
		}

		///<summary>Realm IDs are numeric company IDs. Letters are allowed too, to be safe, but nothing that could change the API path.</summary>
		public static bool IsValidRealmId(string realmId) {
			return !string.IsNullOrEmpty(realmId) && realmId.Length<=64 && Regex.IsMatch(realmId,"^[A-Za-z0-9]+$");
		}
	}
}
