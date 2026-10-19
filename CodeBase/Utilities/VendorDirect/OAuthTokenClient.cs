// Vendor-direct integrations: requests to an OAuth 2.0 token endpoint (RFC 6749), and authorization URLs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CodeBase.VendorDirect {
	///<summary>Posts form requests to a vendor's token endpoint and reads the standard JSON answer. A 400 or 401 (invalid_grant, invalid_client and so
	///on) throws TokenRejectedException, which TokenCache treats as "sign in again". Anything else that fails throws HttpRequestException (or
	///VendorHttpException), which never marks the configuration as rejected. Error messages carry the OAuth error code and description only, never
	///the request or response body.</summary>
	public static class OAuthTokenClient {
		///<summary>Sends dictForm to tokenEndpoint. With clientId and clientSecret it uses HTTP Basic client authentication (client_secret_basic);
		///vendors that want the secret in the body put it in dictForm instead.</summary>
		public static async Task<OAuthAccessToken> RequestAsync(HttpClient httpClient,Uri tokenEndpoint,IDictionary<string,string> dictForm,
			string basicClientId=null,string basicClientSecret=null,CancellationToken cancellationToken=default(CancellationToken),Func<DateTime> funcUtcNow=null)
		{
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,tokenEndpoint)) {
				request.Content=new FormUrlEncodedContent(dictForm.Where(x => x.Value!=null));
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				if(basicClientId!=null) {
					string credentials=Uri.EscapeDataString(basicClientId)+":"+Uri.EscapeDataString(basicClientSecret??"");
					request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
				}
				VendorHttp.SetEndpointName(request,"token");
				using(HttpResponseMessage response=await httpClient.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					return ParseResponse((int)response.StatusCode,body,(funcUtcNow ?? (() => DateTime.UtcNow))());
				}
			}
		}

		///<summary>Reads a token endpoint answer. Public for tests and for vendors that send the request themselves.</summary>
		public static OAuthAccessToken ParseResponse(int statusCode,string body,DateTime utcNow) {
			JObject jObject=TryParseJson(body);
			if(statusCode==400 || statusCode==401) {
				string error=jObject?.Value<string>("error")??"";
				string description=jObject?.Value<string>("error_description")??"";
				string message="The vendor refused the sign-in"+(error=="" ? "" : " ("+error+")")+"."+(description=="" ? "" : " "+description);
				throw new TokenRejectedException(error,message,statusCode);
			}
			if(statusCode<200 || statusCode>299) {
				throw new HttpRequestException("The token request failed with HTTP "+statusCode+".");
			}
			string accessToken=jObject?.Value<string>("access_token");
			if(string.IsNullOrEmpty(accessToken)) {
				throw new HttpRequestException("The token response had no access token.");
			}
			OAuthAccessToken token=new OAuthAccessToken();
			token.Value=accessToken;
			token.TokenType=NormalizeTokenType(jObject.Value<string>("token_type"));
			token.IssuedUtc=utcNow;
			long expiresIn=ReadSeconds(jObject["expires_in"]);
			token.ExpiresUtc=utcNow.AddSeconds(expiresIn>0 ? expiresIn : 3600);
			token.RefreshToken=jObject.Value<string>("refresh_token");
			token.Scope=jObject.Value<string>("scope");
			token.IdToken=jObject.Value<string>("id_token");
			return token;
		}

		///<summary>The form for redeeming an authorization code. codeVerifier is the PKCE verifier; clientSecret goes in the body only if not null.</summary>
		public static Dictionary<string,string> GetAuthorizationCodeForm(string code,string redirectUri,string clientId,string codeVerifier,
			string clientSecret=null)
		{
			Dictionary<string,string> dict=new Dictionary<string,string>();
			dict["grant_type"]="authorization_code";
			dict["code"]=code;
			dict["redirect_uri"]=redirectUri;
			if(clientId!=null) {
				dict["client_id"]=clientId;
			}
			if(codeVerifier!=null) {
				dict["code_verifier"]=codeVerifier;
			}
			if(clientSecret!=null) {
				dict["client_secret"]=clientSecret;
			}
			return dict;
		}

		///<summary>The form for a refresh-token grant.</summary>
		public static Dictionary<string,string> GetRefreshForm(string refreshToken,string clientId=null,string clientSecret=null,string scope=null) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			dict["grant_type"]="refresh_token";
			dict["refresh_token"]=refreshToken;
			if(clientId!=null) {
				dict["client_id"]=clientId;
			}
			if(clientSecret!=null) {
				dict["client_secret"]=clientSecret;
			}
			if(scope!=null) {
				dict["scope"]=scope;
			}
			return dict;
		}

		///<summary>The form for a client-credentials grant (USPS and other server-to-server vendors).</summary>
		public static Dictionary<string,string> GetClientCredentialsForm(string scope=null,string clientId=null,string clientSecret=null) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			dict["grant_type"]="client_credentials";
			if(scope!=null) {
				dict["scope"]=scope;
			}
			if(clientId!=null) {
				dict["client_id"]=clientId;
			}
			if(clientSecret!=null) {
				dict["client_secret"]=clientSecret;
			}
			return dict;
		}

		///<summary>authorizeEndpoint plus the parameters as a query string (values escaped). Refuses an endpoint VendorUrlPolicy refuses.</summary>
		public static string BuildAuthorizeUrl(Uri authorizeEndpoint,IEnumerable<KeyValuePair<string,string>> listParameters) {
			VendorUrlProblem problem=VendorUrlPolicy.Check(authorizeEndpoint);
			if(problem!=VendorUrlProblem.None) {
				throw new ArgumentException(VendorUrlPolicy.Describe(problem),nameof(authorizeEndpoint));
			}
			string query=string.Join("&",listParameters.Where(x => x.Value!=null)
				.Select(x => Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)));
			string url=authorizeEndpoint.GetLeftPart(UriPartial.Path);
			string queryExisting=authorizeEndpoint.Query.TrimStart('?');
			if(queryExisting!="") {
				query=queryExisting+(query=="" ? "" : "&"+query);
			}
			return (query=="") ? url : url+"?"+query;
		}

		private static JObject TryParseJson(string body) {
			if(string.IsNullOrWhiteSpace(body)) {
				return null;
			}
			try {
				return JObject.Parse(body);
			}
			catch(Exception) {
				return null;//Not JSON. The status code decides.
			}
		}

		private static long ReadSeconds(JToken jToken) {
			if(jToken==null) {
				return 0;
			}
			long seconds;
			if(long.TryParse(jToken.ToString(),out seconds)) {
				return seconds;
			}
			return 0;
		}

		private static string NormalizeTokenType(string tokenType) {
			if(string.IsNullOrEmpty(tokenType) || tokenType.Equals("bearer",StringComparison.OrdinalIgnoreCase)) {
				return "Bearer";
			}
			return tokenType;
		}
	}
}
