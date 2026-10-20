// Tests for the in-memory token cache, the token endpoint client and the refresh-token strategies. Fake handlers only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class TokenTests {
		private static readonly DateTime _dateTimeStart=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);

		private static OAuthAccessToken MakeToken(string value,DateTime issuedUtc,TimeSpan lifetime) {
			OAuthAccessToken token=new OAuthAccessToken();
			token.Value=value;
			token.IssuedUtc=issuedUtc;
			token.ExpiresUtc=issuedUtc.Add(lifetime);
			return token;
		}

		[TestMethod]
		public void Cache_ReusesTokenUntilMarginThenRefreshes() {
			AssertEx.Run(async () => {
				DateTime nowUtc=_dateTimeStart;
				TokenCache tokenCache=new TokenCache(TimeSpan.FromMinutes(5),() => nowUtc);
				int countFetches=0;
				Func<CancellationToken,Task<OAuthAccessToken>> fetch=ct => {
					countFetches++;
					return Task.FromResult(MakeToken("t"+countFetches,nowUtc,TimeSpan.FromHours(1)));
				};
				Assert.AreEqual("t1",(await tokenCache.GetAsync(fetch)).Value);
				nowUtc=nowUtc.AddMinutes(54);
				Assert.AreEqual("t1",(await tokenCache.GetAsync(fetch)).Value,"Six minutes left: still good.");
				nowUtc=nowUtc.AddMinutes(2);
				Assert.AreEqual("t2",(await tokenCache.GetAsync(fetch)).Value,"Four minutes left: inside the margin.");
				Assert.AreEqual(2,countFetches);
			});
		}

		[TestMethod]
		public void Cache_ShortLivedTokensUseHalfTheirLifetime() {
			AssertEx.Run(async () => {
				DateTime nowUtc=_dateTimeStart;
				TokenCache tokenCache=new TokenCache(TimeSpan.FromMinutes(5),() => nowUtc);
				int countFetches=0;
				Func<CancellationToken,Task<OAuthAccessToken>> fetch=ct => {
					countFetches++;
					return Task.FromResult(MakeToken("t"+countFetches,nowUtc,TimeSpan.FromMinutes(4)));
				};
				await tokenCache.GetAsync(fetch);
				nowUtc=nowUtc.AddMinutes(1);
				await tokenCache.GetAsync(fetch);
				Assert.AreEqual(1,countFetches,"A 4-minute token is not refreshed on every call.");
				nowUtc=nowUtc.AddMinutes(1.5);
				await tokenCache.GetAsync(fetch);
				Assert.AreEqual(2,countFetches);
				//PostcardMania: 15-minute tokens with a 60-second margin.
				TokenCache tokenCachePcm=new TokenCache(TimeSpan.FromSeconds(60),() => nowUtc);
				int countPcm=0;
				Func<CancellationToken,Task<OAuthAccessToken>> fetchPcm=ct => {
					countPcm++;
					return Task.FromResult(MakeToken("p",nowUtc,TimeSpan.FromMinutes(15)));
				};
				await tokenCachePcm.GetAsync(fetchPcm);
				nowUtc=nowUtc.AddMinutes(13.5);
				await tokenCachePcm.GetAsync(fetchPcm);
				Assert.AreEqual(1,countPcm);
				nowUtc=nowUtc.AddMinutes(0.6);
				await tokenCachePcm.GetAsync(fetchPcm);
				Assert.AreEqual(2,countPcm);
			});
		}

		[TestMethod]
		public void Cache_RefreshIsSingleFlight() {
			AssertEx.Run(async () => {
				TokenCache tokenCache=new TokenCache();
				int countFetches=0;
				TaskCompletionSource<bool> tcsRelease=new TaskCompletionSource<bool>();
				Func<CancellationToken,Task<OAuthAccessToken>> fetch=async ct => {
					Interlocked.Increment(ref countFetches);
					await tcsRelease.Task;
					return MakeToken("only",DateTime.UtcNow,TimeSpan.FromHours(1));
				};
				List<Task<OAuthAccessToken>> listTasks=Enumerable.Range(0,20).Select(x => Task.Run(() => tokenCache.GetAsync(fetch))).ToList();
				await Task.Delay(100);
				tcsRelease.SetResult(true);
				OAuthAccessToken[] arrayTokens=await Task.WhenAll(listTasks);
				Assert.AreEqual(1,countFetches,"A burst of callers makes one refresh.");
				Assert.IsTrue(arrayTokens.All(x => x.Value=="only"));
			});
		}

		[TestMethod]
		public void Cache_RejectionIsStickyAndNetworkErrorsAreNot() {
			AssertEx.Run(async () => {
				TokenCache tokenCache=new TokenCache();
				int countFetches=0;
				await AssertEx.ThrowsAsync<HttpRequestException>(() => tokenCache.GetAsync(ct => {
					countFetches++;
					throw new HttpRequestException("offline");
				}));
				Assert.IsFalse(tokenCache.NeedsReauthorization,"A network error never asks for a new sign-in.");
				await AssertEx.ThrowsAsync<TokenRejectedException>(() => tokenCache.GetAsync(ct => {
					countFetches++;
					throw new TokenRejectedException("invalid_grant","Refused.",400);
				}));
				Assert.IsTrue(tokenCache.NeedsReauthorization);
				await AssertEx.ThrowsAsync<ReauthorizationRequiredException>(() => tokenCache.GetAsync(ct => {
					countFetches++;
					return Task.FromResult(MakeToken("x",DateTime.UtcNow,TimeSpan.FromHours(1)));
				}));
				Assert.AreEqual(2,countFetches,"After a rejection, calls fail fast without contacting the vendor.");
				Assert.IsTrue(tokenCache.ShouldShowReauthorizationMessage());
				Assert.IsFalse(tokenCache.ShouldShowReauthorizationMessage(),"One message per session.");
				tokenCache.ClearReauthorization();
				Assert.AreEqual("x",(await tokenCache.GetAsync(ct => Task.FromResult(MakeToken("x",DateTime.UtcNow,TimeSpan.FromHours(1))))).Value);
			});
		}

		[TestMethod]
		public void Cache_SendWithAuthRetry_RefreshesOnceOn401() {
			AssertEx.Run(async () => {
				TokenCache tokenCache=new TokenCache();
				int countFetches=0;
				Func<CancellationToken,Task<OAuthAccessToken>> fetch=ct => {
					countFetches++;
					return Task.FromResult(MakeToken("token"+countFetches,DateTime.UtcNow,TimeSpan.FromHours(1)));
				};
				FakeHttpHandler fake=new FakeHttpHandler().Enqueue(HttpStatusCode.Unauthorized).Enqueue(HttpStatusCode.OK,"ok");
				HttpClient httpClient=new HttpClient(fake);
				HttpResponseMessage response=await tokenCache.SendWithAuthRetryAsync(httpClient,
					() => new HttpRequestMessage(HttpMethod.Get,"https://api.vendor.example/me"),fetch);
				Assert.AreEqual(HttpStatusCode.OK,response.StatusCode);
				Assert.AreEqual("Bearer token1",fake.Requests[0].GetHeader("Authorization"));
				Assert.AreEqual("Bearer token2",fake.Requests[1].GetHeader("Authorization"));
				//A second 401 goes back to the caller; no third try.
				fake.Enqueue(HttpStatusCode.Unauthorized).Enqueue(HttpStatusCode.Unauthorized).Enqueue(HttpStatusCode.OK);
				response=await tokenCache.SendWithAuthRetryAsync(httpClient,() => new HttpRequestMessage(HttpMethod.Get,"https://api.vendor.example/me"),fetch);
				Assert.AreEqual(HttpStatusCode.Unauthorized,response.StatusCode);
				Assert.AreEqual(4,fake.Requests.Count);
				Assert.AreEqual(3,countFetches);
			});
		}

		[TestMethod]
		public void Cache_ForSharesPerVendorAndFingerprint() {
			TokenCache.ClearAll();
			string fingerprint1=TokenCache.Fingerprint("client","Production","osd1$abc");
			string fingerprint2=TokenCache.Fingerprint("client","Sandbox","osd1$abc");
			Assert.AreNotEqual(fingerprint1,fingerprint2);
			Assert.AreNotEqual(TokenCache.Fingerprint("ab","c"),TokenCache.Fingerprint("a","bc"));
			Assert.AreEqual(64,fingerprint1.Length);
			Assert.AreSame(TokenCache.For("V",fingerprint1),TokenCache.For("V",fingerprint1));
			Assert.AreNotSame(TokenCache.For("V",fingerprint1),TokenCache.For("V",fingerprint2));
		}

		[TestMethod]
		public void TokenClient_ParsesSuccess() {
			AssertEx.Run(async () => {
				FakeHttpHandler fake=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,
					"{\"access_token\":\"at\",\"token_type\":\"bearer\",\"expires_in\":3600,\"refresh_token\":\"rt2\",\"x_refresh_token_expires_in\":8726400}");
				HttpClient httpClient=new HttpClient(fake);
				OAuthAccessToken token=await OAuthTokenClient.RequestAsync(httpClient,new Uri("https://auth.vendor.example/token"),
					OAuthTokenClient.GetRefreshForm("rt1"),"client id","s3cret:x",funcUtcNow:() => _dateTimeStart);
				Assert.AreEqual("at",token.Value);
				Assert.AreEqual("Bearer",token.TokenType);
				Assert.AreEqual("rt2",token.RefreshToken);
				Assert.AreEqual(_dateTimeStart.AddHours(1),token.ExpiresUtc);
				RecordedRequest recorded=fake.Requests.Single();
				Assert.AreEqual(HttpMethod.Post,recorded.Method);
				Assert.AreEqual("application/x-www-form-urlencoded",recorded.ContentType);
				Assert.AreEqual("grant_type=refresh_token&refresh_token=rt1",recorded.Body);
				string basic=Convert.ToBase64String(Encoding.UTF8.GetBytes("client%20id:s3cret%3Ax"));
				Assert.AreEqual("Basic "+basic,recorded.GetHeader("Authorization"));
			});
		}

		[TestMethod]
		public void TokenClient_400And401AreRejections_OthersAreTransient() {
			TokenRejectedException ex=AssertEx.Throws<TokenRejectedException>(() =>
				OAuthTokenClient.ParseResponse(400,"{\"error\":\"invalid_grant\",\"error_description\":\"Token expired\"}",_dateTimeStart));
			Assert.AreEqual("invalid_grant",ex.Error);
			Assert.AreEqual(400,ex.StatusCode);
			StringAssert.Contains(ex.Message,"Token expired");
			ex=AssertEx.Throws<TokenRejectedException>(() => OAuthTokenClient.ParseResponse(401,"<html>nope</html>",_dateTimeStart));
			Assert.AreEqual("",ex.Error);
			Assert.IsFalse(ex.Message.Contains("<html>"),"The body is never echoed.");
			AssertEx.Throws<HttpRequestException>(() => OAuthTokenClient.ParseResponse(503,"",_dateTimeStart));
			AssertEx.Throws<HttpRequestException>(() => OAuthTokenClient.ParseResponse(200,"{\"token_type\":\"bearer\"}",_dateTimeStart));
			OAuthAccessToken token=OAuthTokenClient.ParseResponse(200,"{\"access_token\":\"a\",\"expires_in\":\"28799\"}",_dateTimeStart);
			Assert.AreEqual(_dateTimeStart.AddSeconds(28799),token.ExpiresUtc,"USPS sends expires_in as a string.");
		}

		[TestMethod]
		public void TokenClient_Forms() {
			Dictionary<string,string> dict=OAuthTokenClient.GetAuthorizationCodeForm("c","http://127.0.0.1:1/cb","id","verifier");
			Assert.AreEqual("authorization_code",dict["grant_type"]);
			Assert.AreEqual("verifier",dict["code_verifier"]);
			Assert.IsFalse(dict.ContainsKey("client_secret"));
			dict=OAuthTokenClient.GetClientCredentialsForm("addresses");
			Assert.AreEqual("client_credentials",dict["grant_type"]);
			Assert.AreEqual("addresses",dict["scope"]);
		}

		private sealed class FakeStore : IRefreshTokenStore {
			public string Value;
			public int CountWrites;

			public string ReadCurrent() {
				return Value??"";
			}

			public bool TryReplace(string expectedCurrent,string newValue) {
				if(Value!=expectedCurrent) {
					return false;
				}
				Value=newValue;
				CountWrites++;
				return true;
			}
		}

		[TestMethod]
		public void Rotating_WritesNewRefreshTokenWithCompareAndSwap() {
			AssertEx.Run(async () => {
				FakeStore store=new FakeStore { Value="rt1" };
				OAuthAccessToken token=await RefreshTokenStrategy.RefreshRotatingAsync(store,(rt,ct) => {
					Assert.AreEqual("rt1",rt);
					OAuthAccessToken t=MakeToken("at",DateTime.UtcNow,TimeSpan.FromHours(1));
					t.RefreshToken="rt2";
					return Task.FromResult(t);
				});
				Assert.AreEqual("at",token.Value);
				Assert.AreEqual("rt2",store.Value);
				Assert.AreEqual(1,store.CountWrites);
			});
		}

		[TestMethod]
		public void Rotating_RereadsOnceWhenAnotherWorkstationRotated() {
			AssertEx.Run(async () => {
				FakeStore store=new FakeStore { Value="rt-old" };
				List<string> listRedeemed=new List<string>();
				OAuthAccessToken token=await RefreshTokenStrategy.RefreshRotatingAsync(store,(rt,ct) => {
					listRedeemed.Add(rt);
					if(rt=="rt-old") {
						store.Value="rt-new";//Another workstation rotated it meanwhile.
						throw new TokenRejectedException("invalid_grant","Refused.",400);
					}
					OAuthAccessToken t=MakeToken("at",DateTime.UtcNow,TimeSpan.FromHours(1));
					t.RefreshToken="rt-newer";
					return Task.FromResult(t);
				});
				CollectionAssert.AreEqual(new[] { "rt-old","rt-new" },listRedeemed);
				Assert.AreEqual("rt-newer",store.Value);
				//Rejected twice with the same value: the rejection stands.
				FakeStore storeStuck=new FakeStore { Value="dead" };
				await AssertEx.ThrowsAsync<TokenRejectedException>(() => RefreshTokenStrategy.RefreshRotatingAsync(storeStuck,
					(rt,ct) => throw new TokenRejectedException("invalid_grant","Refused.",400)));
				//Not signed in at all.
				await AssertEx.ThrowsAsync<TokenRejectedException>(() => RefreshTokenStrategy.RefreshRotatingAsync(new FakeStore(),
					(rt,ct) => Task.FromResult(MakeToken("x",DateTime.UtcNow,TimeSpan.FromHours(1)))));
			});
		}

		[TestMethod]
		public void Rotating_LostRaceKeepsTheOtherWorkstationsToken() {
			AssertEx.Run(async () => {
				FakeStore store=new FakeStore { Value="rt1" };
				OAuthAccessToken token=await RefreshTokenStrategy.RefreshRotatingAsync(store,(rt,ct) => {
					store.Value="rt-theirs";//Another workstation wrote first.
					OAuthAccessToken t=MakeToken("at",DateTime.UtcNow,TimeSpan.FromHours(1));
					t.RefreshToken="rt-ours";
					return Task.FromResult(t);
				});
				Assert.AreEqual("at",token.Value);
				Assert.AreEqual("rt-theirs",store.Value);
				Assert.AreEqual(0,store.CountWrites);
			});
		}

		[TestMethod]
		public void NonRotating_NeverWrites() {
			AssertEx.Run(async () => {
				FakeStore store=new FakeStore { Value="rt" };
				OAuthAccessToken token=await RefreshTokenStrategy.RefreshNonRotatingAsync(store,(rt,ct) => {
					OAuthAccessToken t=MakeToken("at",DateTime.UtcNow,TimeSpan.FromHours(1));
					t.RefreshToken="rt-other";
					return Task.FromResult(t);
				});
				Assert.AreEqual("rt",store.Value);
				Assert.AreEqual(0,store.CountWrites);
			});
		}

		[TestMethod]
		public void WriteThrottle_OncePerIntervalUnlessIdentityChanges() {
			DateTime nowUtc=_dateTimeStart;
			WriteThrottle writeThrottle=new WriteThrottle(TimeSpan.FromDays(1),() => nowUtc);
			Assert.IsTrue(writeThrottle.ShouldWrite("mailbox1","account-a"));
			Assert.IsFalse(writeThrottle.ShouldWrite("mailbox1","account-a"));
			Assert.IsTrue(writeThrottle.ShouldWrite("mailbox1","account-b"),"Another account writes at once.");
			Assert.IsTrue(writeThrottle.ShouldWrite("mailbox2","account-a"));
			nowUtc=nowUtc.AddDays(1);
			Assert.IsTrue(writeThrottle.ShouldWrite("mailbox1","account-b"));
		}
	}
}
