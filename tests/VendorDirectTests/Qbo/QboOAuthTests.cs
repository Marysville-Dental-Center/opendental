// Tests for Intuit's OAuth calls: the consent URL, the code exchange, token parsing and revoke. No network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.Qbo;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboOAuthTests {
		private static readonly DateTime _dateTimeNow=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);

		[TestMethod]
		public void ParseTokenResponse_ReadsAccessRefreshAndRefreshExpiry() {
			QboTokenResult result=QboOAuth.ParseTokenResponse(200,QboTestKit.TokenJson("at-1","rt-2",8726400),_dateTimeNow);
			Assert.AreEqual("at-1",result.AccessToken);
			Assert.AreEqual("rt-2",result.RefreshToken);
			Assert.AreEqual(_dateTimeNow.AddHours(1),result.AccessTokenExpiresUtc);
			Assert.AreEqual(_dateTimeNow.AddSeconds(8726400),result.RefreshTokenExpiresUtc);
			Assert.AreEqual("Bearer",result.ToAccessToken().TokenType);
		}

		[TestMethod]
		public void ParseTokenResponse_MissingRefreshExpiryIsNull_AndUnknownFieldsAreIgnored() {
			QboTokenResult result=QboOAuth.ParseTokenResponse(200,QboTestKit.TokenJson("at-1","rt-2",null,"x_refresh_token_max_expires_in"),_dateTimeNow);
			Assert.AreEqual("at-1",result.AccessToken);
			Assert.IsNull(result.RefreshTokenExpiresUtc);
		}

		[TestMethod]
		public void ParseTokenResponse_InvalidGrantAndInvalidClientAreRejections() {
			TokenRejectedException ex=AssertEx.Throws<TokenRejectedException>(
				() => QboOAuth.ParseTokenResponse(400,QboTestKit.OAuthErrorJson("invalid_grant"),_dateTimeNow));
			Assert.AreEqual("invalid_grant",ex.Error);
			ex=AssertEx.Throws<TokenRejectedException>(() => QboOAuth.ParseTokenResponse(401,QboTestKit.OAuthErrorJson("invalid_client"),_dateTimeNow));
			Assert.AreEqual("invalid_client",ex.Error);
			AssertEx.Throws<System.Net.Http.HttpRequestException>(() => QboOAuth.ParseTokenResponse(503,"",_dateTimeNow));
		}

		[TestMethod]
		public void BuildAuthorizeUrl_HasOnlyTheAccountingScopeAndTheState() {
			string url=QboOAuth.BuildAuthorizeUrl("client id","https://office.example/qbo/callback","state-123");
			Uri uri=new Uri(url);
			Assert.AreEqual("https://appcenter.intuit.com/connect/oauth2",uri.GetLeftPart(UriPartial.Path));
			Dictionary<string,string> dict=OAuthRedirectParser.ParseQuery(uri.Query);
			Assert.AreEqual("client id",dict["client_id"]);
			Assert.AreEqual("code",dict["response_type"]);
			Assert.AreEqual("com.intuit.quickbooks.accounting",dict["scope"]);
			Assert.AreEqual("https://office.example/qbo/callback",dict["redirect_uri"]);
			Assert.AreEqual("state-123",dict["state"]);
			Assert.IsFalse(dict.ContainsKey("client_secret"));
		}

		[TestMethod]
		public void ExchangeCode_PostsTheCodeWithBasicClientAuthentication() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-1"));
			QboTokenResult result=null;
			AssertEx.Run(async () => {
				result=await kit.QboOAuth.ExchangeCodeAsync("code-1","http://localhost:8765/callback");
			});
			Assert.AreEqual("rt-1",result.RefreshToken);
			RecordedRequest request=kit.HandlerOAuth.Requests[0];
			Assert.AreEqual("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer",request.RequestUri.AbsoluteUri);
			Assert.AreEqual("Basic "+Convert.ToBase64String(Encoding.UTF8.GetBytes(QboTestKit.ClientId+":"+QboTestKit.ClientSecret)),
				request.GetHeader("Authorization"));
			Assert.AreEqual("application/x-www-form-urlencoded",request.ContentType);
			Dictionary<string,string> dictForm=OAuthRedirectParser.ParseQuery(request.Body);
			Assert.AreEqual("authorization_code",dictForm["grant_type"]);
			Assert.AreEqual("code-1",dictForm["code"]);
			Assert.AreEqual("http://localhost:8765/callback",dictForm["redirect_uri"]);
			Assert.IsFalse(dictForm.ContainsKey("client_secret"),"The secret goes only in the Basic header.");
			Assert.IsFalse(string.Join("\n",kit.ListLogLines).Contains("code-1"),"Codes are never logged.");
		}

		[TestMethod]
		public void Refresh_SendsTheRefreshGrant() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-2","rt-2"));
			AssertEx.Run(async () => {
				await kit.QboOAuth.RefreshAsync("rt-1");
			});
			Dictionary<string,string> dictForm=OAuthRedirectParser.ParseQuery(kit.HandlerOAuth.Requests[0].Body);
			Assert.AreEqual("refresh_token",dictForm["grant_type"]);
			Assert.AreEqual("rt-1",dictForm["refresh_token"]);
		}

		[TestMethod]
		public void Revoke_PostsTheTokenAsJson() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,"");
			AssertEx.Run(async () => {
				await kit.QboOAuth.RevokeAsync("rt-9");
			});
			RecordedRequest request=kit.HandlerOAuth.Requests[0];
			Assert.AreEqual("https://developer.api.intuit.com/v2/oauth2/tokens/revoke",request.RequestUri.AbsoluteUri);
			Assert.AreEqual("rt-9",JObject.Parse(request.Body).Value<string>("token"));
			StringAssert.StartsWith(request.GetHeader("Authorization"),"Basic ");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.BadRequest,"");
			AssertEx.Run(async () => {
				await AssertEx.ThrowsAsync<System.Net.Http.HttpRequestException>(() => kit.QboOAuth.RevokeAsync("rt-9"));
			});
		}
	}
}
