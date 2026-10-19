// Vendor-direct integrations: the parameters an OAuth authorization server sends back on its redirect.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace CodeBase.VendorDirect {
	///<summary>What came back on an OAuth redirect: from the loopback listener, or from an address or code the user pasted.
	///The caller always checks State with VerifyState before redeeming Code.</summary>
	public sealed class OAuthRedirectResult {
		///<summary>The authorization code, or null.</summary>
		public string Code { get; }
		///<summary>The state value, or null (a bare pasted code carries none).</summary>
		public string State { get; }
		///<summary>The OAuth error code ("access_denied" and so on), or null.</summary>
		public string Error { get; }
		///<summary>The error_description, or null. Text from the vendor; show it, don't parse it.</summary>
		public string ErrorDescription { get; }
		///<summary>Every parameter, including code and state, by name (case-sensitive). The first value wins when a name repeats.
		///Holds vendor extras such as QuickBooks Online's realmId.</summary>
		public IReadOnlyDictionary<string,string> Parameters { get; }

		public OAuthRedirectResult(IDictionary<string,string> dictParameters) {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.Ordinal);
			if(dictParameters!=null) {
				foreach(KeyValuePair<string,string> pair in dictParameters) {
					dict[pair.Key]=pair.Value;
				}
			}
			Parameters=dict;
			Code=Get("code");
			State=Get("state");
			Error=Get("error");
			ErrorDescription=Get("error_description");
		}

		///<summary>True if a code came back and no error.</summary>
		public bool HasCode {
			get {
				return !string.IsNullOrEmpty(Code) && string.IsNullOrEmpty(Error);
			}
		}

		///<summary>Returns the named parameter, or null.</summary>
		public string Get(string name) {
			string value;
			if(Parameters.TryGetValue(name,out value)) {
				return value;
			}
			return null;
		}

		///<summary>True if State matches the value sent on the authorization request (constant-time comparison).</summary>
		public bool VerifyState(string expectedState) {
			return OAuthState.Matches(expectedState,State);
		}
	}

	///<summary>Parses what the user pastes after signing in on a vendor's page, when the loopback redirect isn't available.</summary>
	public static class OAuthRedirectParser {
		///<summary>Accepts a full redirected address ("https://host/path?code=..&amp;state=.."), a query string with or without its leading '?',
		///a fragment ("#code=.."), or a bare authorization code (anything without '=', '?', '&amp;' or whitespace inside).
		///Returns null for empty input or input it can't read.</summary>
		public static OAuthRedirectResult Parse(string input) {
			if(string.IsNullOrWhiteSpace(input)) {
				return null;
			}
			string text=input.Trim();
			Uri uri;
			if(Uri.TryCreate(text,UriKind.Absolute,out uri) && (uri.Scheme==Uri.UriSchemeHttp || uri.Scheme==Uri.UriSchemeHttps)) {
				string query=uri.Query;
				if(string.IsNullOrEmpty(query) || query=="?") {
					query=uri.Fragment;
				}
				return new OAuthRedirectResult(ParseQuery(query));
			}
			if(text.StartsWith("?") || text.StartsWith("#") || text.Contains("=")) {
				return new OAuthRedirectResult(ParseQuery(text));
			}
			if(text.IndexOfAny(new[] { '&',' ','\t','\r','\n','/' })>=0) {
				return null;
			}
			return new OAuthRedirectResult(new Dictionary<string,string> { { "code",text } });
		}

		///<summary>Parses an application/x-www-form-urlencoded query or fragment. A leading '?' or '#' is skipped. The first value wins when a name
		///repeats.</summary>
		public static Dictionary<string,string> ParseQuery(string query) {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.Ordinal);
			if(string.IsNullOrEmpty(query)) {
				return dict;
			}
			string text=query;
			if(text.StartsWith("?") || text.StartsWith("#")) {
				text=text.Substring(1);
			}
			string[] arrayPairs=text.Split('&');
			for(int i=0;i<arrayPairs.Length;i++) {
				if(arrayPairs[i]=="") {
					continue;
				}
				int indexEquals=arrayPairs[i].IndexOf('=');
				string name;
				string value;
				if(indexEquals<0) {
					name=Decode(arrayPairs[i]);
					value="";
				}
				else {
					name=Decode(arrayPairs[i].Substring(0,indexEquals));
					value=Decode(arrayPairs[i].Substring(indexEquals+1));
				}
				if(name!="" && !dict.ContainsKey(name)) {
					dict[name]=value;
				}
			}
			return dict;
		}

		private static string Decode(string text) {
			return Uri.UnescapeDataString(text.Replace('+',' '));
		}
	}
}
