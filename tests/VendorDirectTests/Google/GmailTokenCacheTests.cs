// Tests for the per-address Gmail access token cache (GMAIL-LIB). No network: token calls go to FakeHttpHandler.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using Task=System.Threading.Tasks.Task;

namespace VendorDirectTests.Gmail {
	[TestClass]
	public class GmailTokenCacheTests {
		private const string MAILBOX="front@example.com";

		[TestInitialize]
		public void TestInitialize() {
			GoogleTokenCache.ClearAll();
		}

		private static Task<string> Get(long emailAddressNum,string stored,HttpClient httpClient,GoogleClientCredentials googleClientCredentials=null) {
			return GoogleTokenCache.GetAccessTokenAsync(emailAddressNum,MAILBOX,stored,googleClientCredentials??GmailTestData.Credentials(),httpClient,
				CancellationToken.None);
		}

		[TestMethod]
		public void Cache_RefreshesOnceThenServesFromMemory() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-1"));
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-1");
				Assert.AreEqual("at-1",await Get(1,stored,httpClient));
				Assert.AreEqual("at-1",await Get(1,stored,httpClient));
				Assert.AreEqual(1,fake.Requests.Count);
				Assert.AreEqual("rt-1",GmailTestData.Form(fake.Requests[0].Body)["refresh_token"],"The decrypted token is sent; the osd1$ value never is.");
			});
		}

		[TestMethod]
		public void Cache_ParallelCallersShareOneRefresh() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(async (request,ct) => {
					await Task.Delay(200,ct);
					return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,GmailTestData.TokenJson("at-shared"));
				});
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-2");
				string[] arrayTokens=await Task.WhenAll(Enumerable.Range(0,10).Select(x => Get(2,stored,httpClient)));
				Assert.IsTrue(arrayTokens.All(x => x=="at-shared"));
				Assert.AreEqual(1,fake.Requests.Count,"A statement batch makes one refresh.");
			});
		}

		[TestMethod]
		public void Cache_InvalidateForcesOneNewRefresh() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-old"));
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-new"));
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-3");
				Assert.AreEqual("at-old",await Get(3,stored,httpClient));
				GoogleTokenCache.Invalidate(3);//What a 401 from Gmail does.
				Assert.AreEqual("at-new",await Get(3,stored,httpClient));
				Assert.AreEqual("at-new",await Get(3,stored,httpClient));
				Assert.AreEqual(2,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Cache_InvalidGrantMarksTheAddressAndStopsCalling() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.BadRequest,"{\"error\":\"invalid_grant\",\"error_description\":\"Token has been expired or revoked.\"}");
				fake.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,GmailTestData.TokenJson("never"))));
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-4");
				GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(4,stored,httpClient));
				Assert.AreEqual(GoogleAuthReason.NeedsReauthorization,ex.Reason);
				Assert.IsTrue(ex.NeedsAuthorization);
				StringAssert.Contains(ex.Message,MAILBOX+" needs to be authorized");
				for(int i=0;i<5;i++) {
					GoogleAuthException exAgain=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(4,stored,httpClient));
					Assert.AreEqual(GoogleAuthReason.NeedsReauthorization,exAgain.Reason);
				}
				Assert.AreEqual(1,fake.Requests.Count,"Later sends from that address fail fast, without a network call.");
			});
		}

		[TestMethod]
		public void Cache_InvalidClientPointsToProgramLinks() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"invalid_client\"}");
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-5");
				GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(5,stored,httpClient));
				Assert.AreEqual(GoogleAuthReason.InvalidClient,ex.Reason);
				StringAssert.Contains(ex.Message,"Program Links");
				GoogleAuthException exAgain=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(5,stored,httpClient));
				Assert.AreEqual(GoogleAuthReason.InvalidClient,exAgain.Reason,"The reason is kept for the fail-fast calls.");
				Assert.AreEqual(1,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Cache_NetworkErrorDoesNotMarkTheAddress() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.EnqueueNetworkFailure();
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-back"));
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-6");
				GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(6,stored,httpClient));
				Assert.AreEqual(GoogleAuthReason.Unreachable,ex.Reason);
				Assert.IsFalse(ex.NeedsAuthorization);
				Assert.AreEqual("at-back",await Get(6,stored,httpClient),"Reconnected: works without re-authorizing.");
			});
		}

		[TestMethod]
		public void Cache_ServerErrorDoesNotMarkTheAddress() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.ServiceUnavailable,"");
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-up"));
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-7");
				GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(7,stored,httpClient));
				Assert.AreEqual(GoogleAuthReason.Unreachable,ex.Reason);
				Assert.AreEqual("at-up",await Get(7,stored,httpClient));
			});
		}

		[TestMethod]
		public void Cache_LegacyBlankOrUnreadableTokenThrowsWithoutNetwork() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				HttpClient httpClient=GmailTestData.Client(fake);
				foreach(string stored in new[] { "","1//plain-token-from-open-dental","osd1$not-really-ciphertext","odv2e$abc" }) {
					GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(8,stored,httpClient));
					Assert.AreEqual(GoogleAuthReason.NotAuthorized,ex.Reason,stored);
				}
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Cache_NotConfiguredThrowsWithoutNetwork() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				HttpClient httpClient=GmailTestData.Client(fake);
				GoogleAuthException ex=await AssertEx.ThrowsAsync<GoogleAuthException>(() =>
					Get(9,SecretsTestHelper.Protect("rt"),httpClient,new GoogleClientCredentials("","")));
				Assert.AreEqual(GoogleAuthReason.NotConfigured,ex.Reason);
				StringAssert.Contains(ex.Message,"Program Links > Gmail API");
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}

		[TestMethod]
		public void Cache_ReauthorizedTokenGetsAFreshEntry() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.BadRequest,"{\"error\":\"invalid_grant\"}");
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-after"));
				HttpClient httpClient=GmailTestData.Client(fake);
				await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(10,SecretsTestHelper.Protect("rt-revoked"),httpClient));
				Assert.AreEqual("at-after",await Get(10,SecretsTestHelper.Protect("rt-new"),httpClient),"A new refresh token is a new entry.");
			});
		}

		[TestMethod]
		public void Cache_ChangedClientGetsAFreshEntry() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				fake.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"invalid_client\"}");
				fake.Enqueue(HttpStatusCode.OK,GmailTestData.TokenJson("at-fixed"));
				HttpClient httpClient=GmailTestData.Client(fake);
				string stored=SecretsTestHelper.Protect("rt-11");
				await AssertEx.ThrowsAsync<GoogleAuthException>(() => Get(11,stored,httpClient,new GoogleClientCredentials(GmailTestData.ClientId,"wrong")));
				Assert.AreEqual("at-fixed",await Get(11,stored,httpClient,new GoogleClientCredentials(GmailTestData.ClientId,"right")));
			});
		}

		[TestMethod]
		public void Cache_PrimeAvoidsTheFirstRefresh() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler();
				HttpClient httpClient=GmailTestData.Client(fake);
				GoogleClientCredentials googleClientCredentials=GmailTestData.Credentials();
				string stored=SecretsTestHelper.Protect("rt-12");
				GoogleToken googleToken=new GoogleToken("at-primed","");
				googleToken.ExpiresInSeconds=3599;
				googleToken.Scope=GoogleOAuthSettings.ScopeGmailSend;
				GoogleTokenCache.Prime(12,stored,googleToken,googleClientCredentials.Fingerprint);
				Assert.AreEqual("at-primed",await Get(12,stored,httpClient,googleClientCredentials));
				Assert.AreEqual(0,fake.Requests.Count);
			});
		}
	}
}
