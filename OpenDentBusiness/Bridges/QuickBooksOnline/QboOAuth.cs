// QuickBooks Online (direct): Intuit's OAuth 2.0 endpoints (authorization code grant with the office's client secret).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.Qbo {
	///<summary>What Intuit's token endpoint returned. The access token stays in memory; the refresh token is stored encrypted.</summary>
	public sealed class QboTokenResult {
		public string AccessToken="";
		public DateTime AccessTokenIssuedUtc;
		public DateTime AccessTokenExpiresUtc;
		///<summary>Intuit returns one on every exchange and refresh; the value changes about once a day.</summary>
		public string RefreshToken="";
		///<summary>From x_refresh_token_expires_in, or null if Intuit didn't send it.</summary>
		public DateTime? RefreshTokenExpiresUtc;

		///<summary>The form TokenCache keeps.</summary>
		public OAuthAccessToken ToAccessToken() {
			OAuthAccessToken token=new OAuthAccessToken();
			token.Value=AccessToken;
			token.TokenType="Bearer";
			token.IssuedUtc=AccessTokenIssuedUtc;
			token.ExpiresUtc=AccessTokenExpiresUtc;
			token.RefreshToken=RefreshToken;
			return token;
		}
	}

	///<summary>Intuit's OAuth endpoints. The client ID and secret are the office's own (Intuit developer app); they are sent only to Intuit's token and
	///revoke endpoints, as HTTP Basic client authentication. PKCE is not offered by Intuit, so the secret is required. Errors from the token endpoint
	///throw TokenRejectedException (400/401: invalid_grant, invalid_client); anything else throws HttpRequestException.</summary>
	public sealed class QboOAuth {
		public static readonly Uri AuthorizeEndpoint=new Uri("https://appcenter.intuit.com/connect/oauth2");
		public static readonly Uri TokenEndpoint=new Uri("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");
		public static readonly Uri RevokeEndpoint=new Uri("https://developer.api.intuit.com/v2/oauth2/tokens/revoke");
		///<summary>The only scope requested: no OpenID, no payments.</summary>
		public const string Scope="com.intuit.quickbooks.accounting";
		///<summary>The hosts these calls may reach (VendorHttp allow-list).</summary>
		public static readonly string[] ArrayAllowedHosts={ "oauth.platform.intuit.com","developer.api.intuit.com" };

		private readonly HttpClient _httpClient;
		private readonly string _clientId;
		private readonly string _clientSecret;
		private readonly Func<DateTime> _funcUtcNow;

		public QboOAuth(HttpClient httpClient,string clientId,string clientSecret,Func<DateTime> funcUtcNow=null) {
			_httpClient=httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_clientId=clientId??"";
			_clientSecret=clientSecret??"";
			_funcUtcNow=funcUtcNow ?? (() => DateTime.UtcNow);
		}

		///<summary>The consent page URL for the system browser.</summary>
		public static string BuildAuthorizeUrl(string clientId,string redirectUri,string state) {
			List<KeyValuePair<string,string>> listParameters=new List<KeyValuePair<string,string>> {
				new KeyValuePair<string,string>("client_id",clientId),
				new KeyValuePair<string,string>("response_type","code"),
				new KeyValuePair<string,string>("scope",Scope),
				new KeyValuePair<string,string>("redirect_uri",redirectUri),
				new KeyValuePair<string,string>("state",state),
			};
			return OAuthTokenClient.BuildAuthorizeUrl(AuthorizeEndpoint,listParameters);
		}

		///<summary>Redeems an authorization code. redirectUri must be the one on the consent URL.</summary>
		public Task<QboTokenResult> ExchangeCodeAsync(string code,string redirectUri,CancellationToken cancellationToken=default(CancellationToken)) {
			Dictionary<string,string> dictForm=new Dictionary<string,string> {
				{ "grant_type","authorization_code" },
				{ "code",code },
				{ "redirect_uri",redirectUri },
			};
			return RequestTokenAsync(dictForm,"token exchange",cancellationToken);
		}

		///<summary>Redeems a refresh token. Intuit may return a new refresh token; the caller stores it.</summary>
		public Task<QboTokenResult> RefreshAsync(string refreshToken,CancellationToken cancellationToken=default(CancellationToken)) {
			return RequestTokenAsync(OAuthTokenClient.GetRefreshForm(refreshToken),"token refresh",cancellationToken);
		}

		///<summary>Revokes the refresh token, which disconnects the app from the company. Throws HttpRequestException if Intuit doesn't confirm.</summary>
		public async System.Threading.Tasks.Task RevokeAsync(string refreshToken,CancellationToken cancellationToken=default(CancellationToken)) {
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,RevokeEndpoint)) {
				JObject jObject=new JObject();
				jObject["token"]=refreshToken;
				request.Content=new StringContent(jObject.ToString(Newtonsoft.Json.Formatting.None),Encoding.UTF8,"application/json");
				AddHeaders(request);
				VendorHttp.SetEndpointName(request,"revoke");
				using(HttpResponseMessage response=await _httpClient.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					if(!response.IsSuccessStatusCode) {
						throw new HttpRequestException("The revoke request failed with HTTP "+(int)response.StatusCode+".");
					}
				}
			}
		}

		private async Task<QboTokenResult> RequestTokenAsync(Dictionary<string,string> dictForm,string endpointName,CancellationToken cancellationToken) {
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,TokenEndpoint)) {
				request.Content=new FormUrlEncodedContent(dictForm);
				AddHeaders(request);
				VendorHttp.SetEndpointName(request,endpointName);
				using(HttpResponseMessage response=await _httpClient.SendAsync(request,cancellationToken).ConfigureAwait(false)) {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					return ParseTokenResponse((int)response.StatusCode,body,_funcUtcNow());
				}
			}
		}

		private void AddHeaders(HttpRequestMessage request) {
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			string credentials=_clientId+":"+_clientSecret;
			request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
		}

		///<summary>Reads a token endpoint answer: access_token, expires_in, refresh_token and x_refresh_token_expires_in. Unknown fields, such as the
		///expiry field Intuit announced for the five-year cap, are ignored. Public for tests.</summary>
		public static QboTokenResult ParseTokenResponse(int statusCode,string body,DateTime utcNow) {
			OAuthAccessToken token=OAuthTokenClient.ParseResponse(statusCode,body,utcNow);//Throws TokenRejectedException for 400/401.
			QboTokenResult result=new QboTokenResult();
			result.AccessToken=token.Value;
			result.AccessTokenIssuedUtc=utcNow;
			result.AccessTokenExpiresUtc=token.ExpiresUtc;
			result.RefreshToken=token.RefreshToken??"";
			JObject jObject=JObject.Parse(body);
			long seconds;
			JToken jTokenExpires=jObject["x_refresh_token_expires_in"];
			if(jTokenExpires!=null && long.TryParse(jTokenExpires.ToString(),out seconds) && seconds>0) {
				result.RefreshTokenExpiresUtc=utcNow.AddSeconds(seconds);
			}
			return result;
		}
	}
}
