// Tests for the Gmail OAuth library (GMAIL-LIB): authorize URL, token calls against a fake HTTP handler, id_token, error mapping and helpers.
// No network: every HTTP call goes to FakeHttpHandler.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using Task=System.Threading.Tasks.Task;

namespace VendorDirectTests.Gmail {
	///<summary>Shared fixtures for the Gmail tests.</summary>
	public static class GmailTestData {
		public const string ClientId="123-abc.apps.googleusercontent.com";
		public const string ClientSecret="test-client-secret";

		public static GoogleClientCredentials Credentials() {
			return new GoogleClientCredentials(ClientId,ClientSecret);
		}

		///<summary>A Google OAuth client over a fake handler, with the production policy (Google hosts only, 15 s).</summary>
		public static HttpClient Client(FakeHttpHandler fakeHttpHandler) {
			return VendorHttp.CreateClient(Google.GetHttpOptions(),fakeHttpHandler);
		}

		///<summary>An unsigned JWT with the given payload, as Google's token endpoint would return it (signature not checked).</summary>
		public static string IdToken(string payloadJson) {
			return Base64Url.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"))+"."
				+Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson))+".c2lnbmF0dXJl";
		}

		public static string IdTokenFor(string email) {
			return IdToken("{\"iss\":\"https://accounts.google.com\",\"email\":\""+email+"\",\"email_verified\":true}");
		}

		public static string TokenJson(string accessToken,string refreshToken=null,string scope=null,string idToken=null,int expiresIn=3599) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("{\"access_token\":\"").Append(accessToken).Append("\",\"expires_in\":").Append(expiresIn).Append(",\"token_type\":\"Bearer\"");
			if(refreshToken!=null) {
				stringBuilder.Append(",\"refresh_token\":\"").Append(refreshToken).Append("\"");
			}
			stringBuilder.Append(",\"scope\":\"").Append(scope??("openid "+GoogleOAuthSettings.ScopeGmailSend+" https://www.googleapis.com/auth/userinfo.email")).Append("\"");
			if(idToken!=null) {
				stringBuilder.Append(",\"id_token\":\"").Append(idToken).Append("\"");
			}
			stringBuilder.Append("}");
			return stringBuilder.ToString();
		}

		///<summary>Parses an application/x-www-form-urlencoded body.</summary>
		public static Dictionary<string,string> Form(string body) {
			return OAuthRedirectParser.ParseQuery(body);
		}
	}

	[TestClass]
	public class GmailOAuthTests {
		[TestMethod]
		public void Pkce_Rfc7636AppendixBVector() {
			Assert.AreEqual("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",Pkce.ComputeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
			Pkce pkce=Pkce.Create();
			Assert.IsTrue(Pkce.IsValidVerifier(pkce.Verifier));
			Assert.AreEqual(43,pkce.Verifier.Length);
		}

		[TestMethod]
		public void AuthorizationUrl_EscapesEveryValueAndAsksForOfflineS256() {
			string url=Google.BuildAuthorizationUrl(GmailTestData.ClientId,"http://127.0.0.1:50123/oauth2",GoogleOAuthSettings.GetScopes(false),"st+ate/=",
				"challenge","front.desk+billing@example.com");
			StringAssert.StartsWith(url,"https://accounts.google.com/o/oauth2/v2/auth?");
			Dictionary<string,string> dict=OAuthRedirectParser.ParseQuery(new Uri(url).Query);
			Assert.AreEqual(GmailTestData.ClientId,dict["client_id"]);
			Assert.AreEqual("code",dict["response_type"]);
			Assert.AreEqual("http://127.0.0.1:50123/oauth2",dict["redirect_uri"]);
			Assert.AreEqual("openid email "+GoogleOAuthSettings.ScopeGmailSend,dict["scope"]);
			Assert.AreEqual("S256",dict["code_challenge_method"]);
			Assert.AreEqual("challenge",dict["code_challenge"]);
			Assert.AreEqual("st+ate/=",dict["state"]);
			Assert.AreEqual("offline",dict["access_type"]);
			Assert.AreEqual("consent",dict["prompt"]);
			Assert.AreEqual("front.desk+billing@example.com",dict["login_hint"],"A '+' in the address must survive (escaped, not a space).");
			StringAssert.Contains(url,"login_hint=front.desk%2Bbilling%40example.com");
			StringAssert.Contains(url,"redirect_uri=http%3A%2F%2F127.0.0.1%3A50123%2Foauth2");
			Assert.IsFalse(url.ToLowerInvariant().Contains("opendental"));
		}

		[TestMethod]
		public void Scopes_ReadOnlyOnlyWhenDownloading() {
			CollectionAssert.AreEqual(new[] { "openid","email",GoogleOAuthSettings.ScopeGmailSend },GoogleOAuthSettings.GetScopes(false));
			CollectionAssert.AreEqual(new[] { "openid","email",GoogleOAuthSettings.ScopeGmailSend,GoogleOAuthSettings.ScopeGmailReadOnly },
				GoogleOAuthSettings.GetScopes(true));
			string granted="openid https://www.googleapis.com/auth/userinfo.email "+GoogleOAuthSettings.ScopeGmailSend;
			Assert.IsTrue(GoogleOAuthSettings.HasScope(granted,GoogleOAuthSettings.ScopeGmailSend));
			Assert.IsTrue(GoogleOAuthSettings.HasScope(granted,GoogleOAuthSettings.ScopeEmail),"Google returns email as its long form.");
			Assert.IsFalse(GoogleOAuthSettings.HasScope(granted,GoogleOAuthSettings.ScopeGmailReadOnly));
			Assert.IsFalse(GoogleOAuthSettings.HasScope("",GoogleOAuthSettings.ScopeGmailSend));
		}

		[TestMethod]
		public void ExchangeCode_PostsToGoogleWithVerifierAndSecret() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-1","rt-1",idToken:GmailTestData.IdTokenFor("Office@Example.com")));
				GoogleToken googleToken=await Google.ExchangeCodeAsync(GmailTestData.ClientId,GmailTestData.ClientSecret,"code-1","verifier-1",
					"http://127.0.0.1:5555/oauth2",httpClient:GmailTestData.Client(fake));
				Assert.AreEqual("at-1",googleToken.AccessToken);
				Assert.AreEqual("rt-1",googleToken.RefreshToken);
				Assert.AreEqual(3599,googleToken.ExpiresInSeconds);
				Assert.AreEqual("Office@Example.com",googleToken.Email);
				StringAssert.Contains(googleToken.Scope,GoogleOAuthSettings.ScopeGmailSend);
				RecordedRequest request=fake.Requests.Single();
				Assert.AreEqual(HttpMethod.Post,request.Method);
				Assert.AreEqual("https://oauth2.googleapis.com/token",request.RequestUri.AbsoluteUri);
				Dictionary<string,string> dictForm=GmailTestData.Form(request.Body);
				Assert.AreEqual("authorization_code",dictForm["grant_type"]);
				Assert.AreEqual("code-1",dictForm["code"]);
				Assert.AreEqual("verifier-1",dictForm["code_verifier"]);
				Assert.AreEqual("http://127.0.0.1:5555/oauth2",dictForm["redirect_uri"]);
				Assert.AreEqual(GmailTestData.ClientId,dictForm["client_id"]);
				Assert.AreEqual(GmailTestData.ClientSecret,dictForm["client_secret"]);
				Assert.IsNull(request.GetHeader("Authorization"));
			});
		}

		[TestMethod]
		public void ExchangeCode_LeavesOutABlankSecret() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at","rt",idToken:GmailTestData.IdTokenFor("a@example.com")));
				await Google.ExchangeCodeAsync(GmailTestData.ClientId,"","c","v","http://127.0.0.1:1/oauth2",httpClient:GmailTestData.Client(fake));
				Assert.IsFalse(GmailTestData.Form(fake.Requests.Single().Body).ContainsKey("client_secret"));
			});
		}

		[TestMethod]
		public void TokenErrors_MapToReasons() {
			AssertEx.Run(async () => {
				Dictionary<string,GoogleAuthReason> dictCases=new Dictionary<string,GoogleAuthReason> {
					{ "{\"error\":\"invalid_grant\",\"error_description\":\"Bad Request\"}",GoogleAuthReason.NeedsReauthorization },
					{ "{\"error\":\"invalid_grant\",\"error_subtype\":\"invalid_rapt\"}",GoogleAuthReason.NeedsReauthorization },
					{ "{\"error\":\"invalid_client\",\"error_description\":\"Unauthorized\"}",GoogleAuthReason.InvalidClient },
					{ "{\"error\":\"unauthorized_client\"}",GoogleAuthReason.InvalidClient },
					{ "{\"error\":\"admin_policy_enforced\"}",GoogleAuthReason.AdminPolicy },
					{ "{\"error\":\"invalid_request\"}",GoogleAuthReason.Other },
				};
				foreach(KeyValuePair<string,GoogleAuthReason> pair in dictCases) {
					FakeHttpHandler fake=new FakeHttpHandler();
					fake.Enqueue(HttpStatusCode.BadRequest,pair.Key);
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						Google.ExchangeCodeAsync(GmailTestData.ClientId,GmailTestData.ClientSecret,"c","v","http://127.0.0.1:1/oauth2",httpClient:GmailTestData.Client(fake)));
					Assert.AreEqual(pair.Value,ex.Reason,pair.Key);
					Assert.IsFalse(ex.Message.Contains("Bad Request") || ex.Message.Contains("{"),"No response body in the message.");
				}
				FakeHttpHandler fakeRapt=new FakeHttpHandler();
				fakeRapt.Enqueue(HttpStatusCode.BadRequest,"{\"error\":\"invalid_grant\",\"error_subtype\":\"invalid_rapt\"}");
				GoogleTokenRejectedException exRapt=await AssertEx.ThrowsAsync<GoogleTokenRejectedException>(() =>
					Google.RefreshAsync(GmailTestData.ClientId,GmailTestData.ClientSecret,"rt",httpClient:GmailTestData.Client(fakeRapt)));
				Assert.AreEqual("invalid_grant",exRapt.Error);
				Assert.AreEqual("invalid_rapt",exRapt.ErrorSubtype);
				Assert.IsInstanceOfType(exRapt,typeof(TokenRejectedException),"TokenCache must see a rejection.");
			});
		}

		[TestMethod]
		public void ExchangeCode_NetworkFailureIsUnreachable() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.EnqueueNetworkFailure();
				GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
					Google.ExchangeCodeAsync(GmailTestData.ClientId,GmailTestData.ClientSecret,"c","v","http://127.0.0.1:1/oauth2",httpClient:GmailTestData.Client(fake)));
				Assert.AreEqual(GoogleAuthReason.Unreachable,ex.Reason);
				Assert.AreEqual("Gmail could not be reached.",ex.Message);
			});
		}

		[TestMethod]
		public void Refresh_SendsRefreshGrant() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-2"));
				OAuthAccessToken token=await Google.RefreshAsync(GmailTestData.ClientId,GmailTestData.ClientSecret,"rt-9",httpClient:GmailTestData.Client(fake));
				Assert.AreEqual("at-2",token.Value);
				Dictionary<string,string> dictForm=GmailTestData.Form(fake.Requests.Single().Body);
				Assert.AreEqual("refresh_token",dictForm["grant_type"]);
				Assert.AreEqual("rt-9",dictForm["refresh_token"]);
				Assert.AreEqual(GmailTestData.ClientSecret,dictForm["client_secret"]);
			});
		}

		[TestMethod]
		public void Revoke_IsBestEffort() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,"{}");
				fake.Enqueue(HttpStatusCode.BadRequest,"{\"error\":\"invalid_token\"}");
				fake.EnqueueNetworkFailure();
				HttpClient httpClient=GmailTestData.Client(fake);
				Assert.IsTrue(await Google.RevokeAsync("rt-1",httpClient:httpClient));
				Assert.IsFalse(await Google.RevokeAsync("rt-1",httpClient:httpClient),"Already revoked: ignored.");
				Assert.IsFalse(await Google.RevokeAsync("rt-1",httpClient:httpClient),"Offline: ignored.");
				Assert.IsFalse(await Google.RevokeAsync("",httpClient:httpClient));
				Assert.AreEqual(3,fake.Requests.Count,"A blank token is never sent.");
				Assert.AreEqual("https://oauth2.googleapis.com/revoke",fake.Requests[0].RequestUri.AbsoluteUri);
				Assert.AreEqual("rt-1",GmailTestData.Form(fake.Requests[0].Body)["token"]);
			});
		}

		[TestMethod]
		public void RevokeStored_NeverSendsALegacyToken() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,"{}")));
				HttpClient httpClient=GmailTestData.Client(fake);
				Assert.IsFalse(await Google.RevokeStoredAsync("1//plain-open-dental-token",httpClient));
				Assert.AreEqual(0,fake.Requests.Count);
				Assert.IsTrue(await Google.RevokeStoredAsync(SecretsTestHelper.Protect("rt-ours"),httpClient));
				Assert.AreEqual("rt-ours",GmailTestData.Form(fake.Requests.Single().Body)["token"]);
			});
		}

		[TestMethod]
		public void Client_RefusesNonGoogleHosts() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				HttpClient httpClient=GmailTestData.Client(fake);
				await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("https://www.opendental.com/style.css"));
				await AssertEx.ThrowsAsync<VendorHttpException>(() => httpClient.GetAsync("https://example.com/"));
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void IdToken_ReadsVerifiedEmail() {
			Assert.AreEqual("a@example.com",Google.ParseIdTokenEmail(GmailTestData.IdTokenFor("a@example.com")));
			Assert.AreEqual("",Google.ParseIdTokenEmail(GmailTestData.IdToken("{\"email\":\"a@example.com\",\"email_verified\":false}")));
			Assert.AreEqual("a@example.com",Google.ParseIdTokenEmail(GmailTestData.IdToken("{\"email\":\"a@example.com\",\"email_verified\":\"true\"}")));
			Assert.AreEqual("",Google.ParseIdTokenEmail(GmailTestData.IdToken("{\"sub\":\"1\"}")));
			Assert.AreEqual("",Google.ParseIdTokenEmail(null));
			Assert.AreEqual("",Google.ParseIdTokenEmail("not-a-jwt"));
			Assert.AreEqual("",Google.ParseIdTokenEmail("a.b!c.d"));
			//Payload lengths that need one and two '=' of padding, and none.
			for(int i=0;i<3;i++) {
				string email=new string('x',i+1)+"@example.com";
				Assert.AreEqual(email,Google.ParseIdTokenEmail(GmailTestData.IdTokenFor(email)),email);
			}
			GoogleIdToken googleIdToken=Google.ParseIdToken(GmailTestData.IdToken("{\"email\":\"a@clinic.example\",\"email_verified\":true,\"hd\":\"clinic.example\"}"));
			Assert.AreEqual("clinic.example",googleIdToken.HostedDomain);
		}

		[TestMethod]
		public void Mailbox_ComparisonIgnoresCase() {
			Assert.IsTrue(Google.IsSameMailbox("Office@Example.com"," office@example.COM "));
			Assert.IsFalse(Google.IsSameMailbox("office@example.com","office2@example.com"));
			Assert.IsFalse(Google.IsSameMailbox("",""));
		}

		[TestMethod]
		public void ShouldRevoke_SkipsWhenAnotherGoogleAddressIsTheSameMailbox() {
			List<EmailAddress> listEmailAddresses=new List<EmailAddress> {
				new EmailAddress { EmailAddressNum=1,EmailUsername="front@example.com",AuthenticationType=OAuthType.Google },
				new EmailAddress { EmailAddressNum=2,EmailUsername="billing@example.com",AuthenticationType=OAuthType.None },
			};
			Assert.IsFalse(Google.ShouldRevoke("FRONT@example.com",listEmailAddresses,0),"Revoking would sign out address 1.");
			Assert.IsTrue(Google.ShouldRevoke("front@example.com",listEmailAddresses,1),"Address 1 itself is excluded.");
			Assert.IsTrue(Google.ShouldRevoke("billing@example.com",listEmailAddresses,0),"A password address doesn't hold a grant.");
			Assert.IsTrue(Google.ShouldRevoke("stranger@example.com",listEmailAddresses,0));
			Assert.IsFalse(Google.ShouldRevoke("",listEmailAddresses,0));
		}

		[TestMethod]
		public void RefreshToken_EncryptsAndRejectsLegacy() {
			string stored;
			Assert.IsTrue(GoogleOAuthSettings.ProtectRefreshToken("1//refresh",out stored));
			SecretsTestHelper.AssertRoundTrip("1//refresh",stored);
			Assert.AreEqual("1//refresh",GoogleOAuthSettings.ReadRefreshToken(stored).PlainText);
			Assert.IsFalse(GoogleOAuthSettings.IsLegacyToken(stored));
			Assert.IsTrue(GoogleOAuthSettings.IsLegacyToken("1//open-dental-plain"));
			Assert.IsFalse(GoogleOAuthSettings.IsLegacyToken(""));
			Assert.AreEqual("",GoogleOAuthSettings.ReadRefreshToken("1//open-dental-plain").PlainText,"Never used.");
			Assert.IsTrue(GoogleOAuthSettings.ReadRefreshToken("1//open-dental-plain").NeedsReentry);
			Assert.IsFalse(GoogleOAuthSettings.ProtectRefreshToken("",out stored),"Nothing to save.");
			Assert.IsNull(stored);
		}

		[TestMethod]
		public void GmailErrors_MapToTheirRows() {
			Assert.AreEqual(GmailErrorKind.Unauthorized,GmailApiErrors.Classify(401,"authError","Invalid Credentials",false));
			Assert.AreEqual(GmailErrorKind.InsufficientScope,GmailApiErrors.Classify(403,"insufficientPermissions","",false));
			Assert.AreEqual(GmailErrorKind.InsufficientScope,GmailApiErrors.Classify(403,"","Request had insufficient authentication scopes.",false));
			Assert.AreEqual(GmailErrorKind.DomainPolicy,GmailApiErrors.Classify(403,"domainPolicy","",true));
			Assert.AreEqual(GmailErrorKind.DailyQuota,GmailApiErrors.Classify(403,"dailyLimitExceeded","",true));
			Assert.AreEqual(GmailErrorKind.RateLimited,GmailApiErrors.Classify(403,"userRateLimitExceeded","",true));
			Assert.AreEqual(GmailErrorKind.RateLimited,GmailApiErrors.Classify(403,"rateLimitExceeded","",false));
			Assert.AreEqual(GmailErrorKind.SendingLimit,GmailApiErrors.Classify(429,"rateLimitExceeded","User-rate limit exceeded.  Retry after 2026-10-19T18:00:00Z",true));
			Assert.AreEqual(GmailErrorKind.RateLimited,GmailApiErrors.Classify(429,"rateLimitExceeded","Too many concurrent requests for user",true));
			Assert.AreEqual(GmailErrorKind.RateLimited,GmailApiErrors.Classify(429,"","Retry after 2026-10-19T18:00:00Z",false),"Only a send is a sending limit.");
			Assert.AreEqual(GmailErrorKind.ServerError,GmailApiErrors.Classify(503,"backendError","",true));
			Assert.AreEqual(GmailErrorKind.Other,GmailApiErrors.Classify(400,"invalidArgument","",true));
			Assert.IsTrue(GmailApiErrors.IsBackoffRetryable(GmailErrorKind.RateLimited));
			Assert.IsTrue(GmailApiErrors.IsBackoffRetryable(GmailErrorKind.ServerError));
			Assert.IsFalse(GmailApiErrors.IsBackoffRetryable(GmailErrorKind.SendingLimit),"Never retry a sending limit in a loop.");
			Assert.IsFalse(GmailApiErrors.IsBackoffRetryable(GmailErrorKind.DailyQuota));
			StringAssert.Contains(GmailApiErrors.GetMessage(GmailErrorKind.SendingLimit,"front@example.com"),"front@example.com");
			StringAssert.Contains(GmailApiErrors.GetMessage(GmailErrorKind.InsufficientScope,""),"Download incoming emails");
			StringAssert.Contains(GmailApiErrors.GetMessage(GmailErrorKind.DailyQuota,""),"quota");
		}

		[TestMethod]
		public void Backoff_DoublesWithJitterAndCaps() {
			Random random=new Random(1);
			for(int attempt=0;attempt<4;attempt++) {
				TimeSpan delay=GmailApiErrors.GetBackoffDelay(attempt,random);
				double secondsBase=Math.Pow(2,attempt);
				Assert.IsTrue(delay.TotalSeconds>=secondsBase && delay.TotalSeconds<secondsBase+1,"Attempt "+attempt+": "+delay);
			}
			Assert.AreEqual(TimeSpan.FromSeconds(32),GmailApiErrors.GetBackoffDelay(10,random));
		}

		[TestMethod]
		public void Inbox_UidMatchIsExact() {
			List<string> listStored=new List<string> { "a1b2c3","GmailIdd2e4f6","G00ff" };
			List<string> listListed=new List<string> { "a1b2c3","d2e4f6","G00ff","1b2c3","2e4f6","00ff","9999","9999" };
			CollectionAssert.AreEqual(new[] { "1b2c3","2e4f6","00ff","9999" },GmailInbox.GetNewMessageIds(listListed,listStored),
				"Leading a, d and G are real hex or ID characters; only the exact GmailId prefix is legacy.");
			Assert.AreEqual("d2e4f6",GmailInbox.NormalizeStoredUid("GmailIdd2e4f6"));
			Assert.AreEqual("addd",GmailInbox.NormalizeStoredUid("addd"));
		}

		[TestMethod]
		public void Inbox_DefaultQueryWhenBlank() {
			Assert.AreEqual("in:inbox newer_than:30d",GmailInbox.GetQuery(""));
			Assert.AreEqual("in:inbox newer_than:30d",GmailInbox.GetQuery(null));
			Assert.AreEqual("in:inbox newer_than:30d is:unread",GmailInbox.GetQuery("is:unread"));
			Assert.AreEqual("from:lab@example.com is:unread",GmailInbox.GetQuery("from:lab@example.com is:unread"));
		}

		[TestMethod]
		public void Inbox_DecodesUnpaddedBase64Url() {
			for(int length=1;length<=6;length++) {
				byte[] bytes=Enumerable.Range(0,length).Select(x => (byte)(250+x)).ToArray();//Bytes that give '-' and '_' in base64url.
				string raw=Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
				CollectionAssert.AreEqual(bytes,GmailInbox.DecodeRaw(raw),"Length "+length);
				CollectionAssert.AreEqual(bytes,GmailInbox.DecodeRaw(Convert.ToBase64String(bytes).Replace('+','-').Replace('/','_')),"Padded, length "+length);
			}
		}

		[TestMethod]
		public void GoogleToken_KeepsItsSerializableShape() {
			GoogleToken googleToken=new GoogleToken();
			Assert.AreEqual("",googleToken.AccessToken);
			Assert.AreEqual("",googleToken.ErrorMessage);
			Assert.AreEqual("",googleToken.Scope);
			Assert.AreEqual("",googleToken.Email);
			Assert.IsTrue(typeof(GoogleToken).IsSerializable);
		}

		[TestMethod]
		public void AuthException_IsAnODExceptionWithAUserMessage() {
			GoogleAuthException ex=GoogleAuthException.Unreachable(new HttpRequestException("boom"));
			Assert.IsInstanceOfType(ex,typeof(ODException));
			StringAssert.Contains(GoogleAuthException.GetNeedsAuthorizationMessage("front@example.com"),"front@example.com needs to be authorized");
			StringAssert.Contains(GoogleAuthException.GetRejectionMessage(GoogleAuthReason.InvalidClient,"invalid_client",""),"Program Links");
		}
	}
}
