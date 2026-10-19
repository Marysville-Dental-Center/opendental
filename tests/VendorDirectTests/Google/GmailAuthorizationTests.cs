// Tests for the Gmail authorization flow (GMAIL-LIB): the loopback redirect on 127.0.0.1, state, code exchange and the account check. A fake
// "browser" calls the listener on this computer; token calls go to FakeHttpHandler. No other network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using Task=System.Threading.Tasks.Task;

namespace VendorDirectTests.Gmail {
	[TestClass]
	public class GmailAuthorizationTests {
		private const string MAILBOX="front+desk@example.com";

		///<summary>Plays the browser: after Google's consent page, it is redirected to redirect_uri with the given query. Returns the page shown.</summary>
		private static async Task<string> RedirectAsync(string authorizationUrl,Func<Dictionary<string,string>,string> funcQuery) {
			Dictionary<string,string> dict=OAuthRedirectParser.ParseQuery(new Uri(authorizationUrl).Query);
			Uri uriRedirect=new Uri(dict["redirect_uri"]);
			string target=uriRedirect.AbsolutePath+"?"+funcQuery(dict);
			using(TcpClient tcpClient=new TcpClient()) {
				await tcpClient.ConnectAsync("127.0.0.1",uriRedirect.Port);
				NetworkStream stream=tcpClient.GetStream();
				byte[] bytes=Encoding.ASCII.GetBytes("GET "+target+" HTTP/1.1\r\nHost: 127.0.0.1:"+uriRedirect.Port+"\r\nConnection: close\r\n\r\n");
				await stream.WriteAsync(bytes,0,bytes.Length);
				using(StreamReader reader=new StreamReader(stream,Encoding.UTF8)) {
					return await reader.ReadToEndAsync();
				}
			}
		}

		private static Google.AuthorizationRequest MakeRequest(FakeHttpHandler fake,Func<Dictionary<string,string>,string> funcQuery,List<string> listUrls,
			List<string> listPages=null)
		{
			Google.AuthorizationRequest authorizationRequest=new Google.AuthorizationRequest();
			authorizationRequest.ClientCredentials=GmailTestData.Credentials();
			authorizationRequest.HttpClient=GmailTestData.Client(fake);
			authorizationRequest.Timeout=TimeSpan.FromSeconds(20);
			authorizationRequest.FuncShouldRevoke=x => true;
			authorizationRequest.ActionOpenBrowser=url => {
				listUrls.Add(url);
				if(funcQuery!=null) {
					Task.Run(async () => {
						string page=await RedirectAsync(url,funcQuery);
						listPages?.Add(page);
					});
				}
			};
			return authorizationRequest;
		}

		private static string CodeAndState(Dictionary<string,string> dict) {
			return "code=4%2F0Abc&scope=email&state="+Uri.EscapeDataString(dict["state"]);
		}

		[TestMethod]
		public void Authorize_FullFlowReturnsTokensForTheRightAccount() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-1","rt-1",idToken:GmailTestData.IdTokenFor("Front+Desk@Example.com")));
				List<string> listUrls=new List<string>();
				List<string> listPages=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,CodeAndState,listUrls,listPages)) {
					GoogleToken googleToken=await authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(true));
					Assert.AreEqual("at-1",googleToken.AccessToken);
					Assert.AreEqual("rt-1",googleToken.RefreshToken,"Plain from the async flow; the caller encrypts it.");
					Assert.AreEqual("",authorizationRequest.RedirectUri,"The listener is closed when the flow ends.");
				}
				Dictionary<string,string> dictUrl=OAuthRedirectParser.ParseQuery(new Uri(listUrls.Single()).Query);
				StringAssert.StartsWith(dictUrl["redirect_uri"],"http://127.0.0.1:");
				StringAssert.EndsWith(dictUrl["redirect_uri"],"/oauth2");
				Assert.AreEqual(MAILBOX,dictUrl["login_hint"]);
				StringAssert.Contains(dictUrl["scope"],GoogleOAuthSettings.ScopeGmailReadOnly);
				Dictionary<string,string> dictForm=GmailTestData.Form(fake.Requests.Single().Body);
				Assert.AreEqual("4/0Abc",dictForm["code"]);
				Assert.AreEqual(dictUrl["redirect_uri"],dictForm["redirect_uri"],"The same redirect URI goes to the token endpoint.");
				Assert.AreEqual(dictUrl["code_challenge"],Pkce.ComputeChallenge(dictForm["code_verifier"]),"The verifier matches the challenge.");
				for(int i=0;i<50 && listPages.Count==0;i++) {
					await Task.Delay(20);
				}
				string page=listPages.Single();
				StringAssert.Contains(page,OAuthResponsePage.MessageSuccess);
				Assert.IsFalse(page.ToLowerInvariant().Contains("opendental"),"The neutral local page loads nothing from opendental.com.");
				Assert.IsFalse(page.Contains("src=") || page.Contains("href="),"No external assets.");
			});
		}

		private static void AssertPortReleased(int port) {
			TcpListener tcpListener=new TcpListener(IPAddress.Loopback,port);
			tcpListener.Start();//Throws if the listener still holds the port.
			tcpListener.Stop();
		}

		[TestMethod]
		public void Authorize_WrongAccountIsRefusedAndRevoked() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-b","rt-b",idToken:GmailTestData.IdTokenFor("other@example.com")));
				fake.Enqueue(HttpStatusCode.OK,"{}");
				List<string> listUrls=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,CodeAndState,listUrls)) {
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.WrongAccount,ex.Reason);
					Assert.AreEqual("You authorized other@example.com, but this address is "+MAILBOX+". Sign in to Google as "+MAILBOX+" and try again.",ex.Message);
				}
				Assert.AreEqual(2,fake.Requests.Count);
				Assert.AreEqual("https://oauth2.googleapis.com/revoke",fake.Requests[1].RequestUri.AbsoluteUri);
				Assert.AreEqual("rt-b",GmailTestData.Form(fake.Requests[1].Body)["token"]);
			});
		}

		[TestMethod]
		public void Authorize_WrongAccountKeepsAnotherAddressesGrant() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-b","rt-b",idToken:GmailTestData.IdTokenFor("billing@example.com")));
				List<string> listUrls=new List<string>();
				List<string> listAsked=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,CodeAndState,listUrls)) {
					authorizationRequest.FuncShouldRevoke=x => {
						listAsked.Add(x);
						return false;//billing@ is already a configured Google address: revoking would sign it out too.
					};
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.WrongAccount,ex.Reason);
				}
				CollectionAssert.AreEqual(new[] { "billing@example.com" },listAsked);
				Assert.AreEqual(1,fake.Requests.Count,"No revoke.");
			});
		}

		[TestMethod]
		public void Authorize_StateMismatchIsRefusedBeforeAnyTokenCall() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				List<string> listUrls=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,x => "code=abc&state=forged",listUrls)) {
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.StateMismatch,ex.Reason);
				}
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Authorize_DeclinedConsentIsAccessDenied() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				List<string> listUrls=new List<string>();
				List<string> listPages=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,
					x => "error=access_denied&state="+Uri.EscapeDataString(x["state"]),listUrls,listPages))
				{
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.AccessDenied,ex.Reason);
				}
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Authorize_MissingSendScopeIsRefused() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at","rt",scope:"openid https://www.googleapis.com/auth/userinfo.email",
					idToken:GmailTestData.IdTokenFor(MAILBOX)));
				List<string> listUrls=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,CodeAndState,listUrls)) {
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.ScopeNotGranted,ex.Reason);
				}
			});
		}

		[TestMethod]
		public void Authorize_ReadScopeNotGrantedIsVisibleToTheCaller() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at","rt",idToken:GmailTestData.IdTokenFor(MAILBOX)));
				List<string> listUrls=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,CodeAndState,listUrls)) {
					GoogleToken googleToken=await authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(true));
					Assert.IsFalse(GoogleOAuthSettings.HasScope(googleToken.Scope,GoogleOAuthSettings.ScopeGmailReadOnly),
						"The user unticked read access; the caller warns and unticks Download incoming emails.");
				}
			});
		}

		[TestMethod]
		public void Authorize_CancelReleasesThePort() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				List<string> listUrls=new List<string>();
				using(CancellationTokenSource cancellationTokenSource=new CancellationTokenSource())
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,null,listUrls)) {
					authorizationRequest.ActionOpenBrowser=url => {
						listUrls.Add(url);
						cancellationTokenSource.CancelAfter(100);//The user closes the browser tab, then clicks Cancel.
					};
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false),cancellationTokenSource.Token));
					Assert.AreEqual(GoogleAuthReason.Cancelled,ex.Reason);
					Assert.AreEqual("Authorization was cancelled.",ex.Message);
				}
				AssertPortReleased(new Uri(OAuthRedirectParser.ParseQuery(new Uri(listUrls[0]).Query)["redirect_uri"]).Port);
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Authorize_TimeoutEndsTheWait() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				List<string> listUrls=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,null,listUrls)) {
					authorizationRequest.Timeout=TimeSpan.FromMilliseconds(300);
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.TimedOut,ex.Reason);
				}
			});
		}

		[TestMethod]
		public void Authorize_NotConfiguredOpensNoBrowser() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				List<string> listUrls=new List<string>();
				using(Google.AuthorizationRequest authorizationRequest=MakeRequest(fake,CodeAndState,listUrls)) {
					authorizationRequest.ClientCredentials=new GoogleClientCredentials(" ","");
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
						authorizationRequest.MakeAccessTokenRequestAsync(MAILBOX,GoogleOAuthSettings.GetScopes(false)));
					Assert.AreEqual(GoogleAuthReason.NotConfigured,ex.Reason);
					Assert.AreEqual(GoogleOAuthSettings.MessageNotConfiguredAuthorize,ex.Message);
				}
				Assert.AreEqual(0,listUrls.Count);
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}
	}
}
