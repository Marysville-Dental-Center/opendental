// Tests for the MSAL session: the encrypted cache round trip, silent refresh, revocation, legacy caches and device-code sign-in, against a fake
// identity platform and Graph (docs/specs/vendor-direct/microsoft-365.md, 3.2-3.5, 3.9 and 6.3). Every token, ID and address here is synthetic;
// never commit a cache captured from a real tenant. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the
// repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Microsoft365 {
	[TestClass]
	public class Microsoft365SessionTests {
		private const string CLIENT_ID="0b6c1f7e-3f0e-4c55-9a51-1d2f5e6a7b8c";
		private const string TENANT_ID="9d8c7b6a-5e4f-4a3b-8c2d-1e0f9a8b7c6d";
		private const string USER_OID="aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
		private const string USERNAME="frontdesk@example-dental.test";
		private const string ENVIRONMENT="login.microsoftonline.com";
		private static readonly string _homeAccountId=USER_OID+"."+TENANT_ID;

		private static Microsoft365Config MakeConfig(Microsoft365SignInMethod signInMethod=Microsoft365SignInMethod.SystemBrowser) {
			Microsoft365Config config=new Microsoft365Config();
			config.ClientId=CLIENT_ID;
			config.TenantId=TENANT_ID;
			config.SignInMethod=signInMethod;
			return config;
		}

		private static long ToUnix(DateTime dateTimeUtc) {
			return (long)(dateTimeUtc-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
		}

		private static string Base64Url(string text) {
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+','-').Replace('/','_');
		}

		///<summary>An unsigned, synthetic ID token with the claims MSAL reads.</summary>
		private static string MakeIdToken(string username=USERNAME,string oid=USER_OID) {
			long now=ToUnix(DateTime.UtcNow);
			JObject jObjectPayload=new JObject {
				["aud"]=CLIENT_ID,
				["iss"]="https://login.microsoftonline.com/"+TENANT_ID+"/v2.0",
				["iat"]=now,
				["nbf"]=now,
				["exp"]=now+3600,
				["name"]="Front Desk",
				["oid"]=oid,
				["preferred_username"]=username,
				["sub"]="synthetic-subject",
				["tid"]=TENANT_ID,
				["ver"]="2.0",
			};
			return Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")+"."+Base64Url(jObjectPayload.ToString(Newtonsoft.Json.Formatting.None))+".";
		}

		private static string MakeClientInfo(string oid=USER_OID) {
			return Base64Url("{\"uid\":\""+oid+"\",\"utid\":\""+TENANT_ID+"\"}");
		}

		///<summary>A synthetic MSAL v3 token cache for one account: an access token valid for accessTokenLifetime, a refresh token and an ID token.</summary>
		private static string MakeSyntheticCache(string accessToken,TimeSpan accessTokenLifetime,string refreshToken="synthetic-refresh-token-1") {
			DateTime dateTimeNow=DateTime.UtcNow;
			string cachedAt=ToUnix(dateTimeNow).ToString();
			string expiresOn=ToUnix(dateTimeNow.Add(accessTokenLifetime)).ToString();
			JObject jObjectCache=new JObject();
			jObjectCache["Account"]=new JObject {
				[_homeAccountId+"-"+ENVIRONMENT+"-"+TENANT_ID]=new JObject {
					["home_account_id"]=_homeAccountId,
					["environment"]=ENVIRONMENT,
					["realm"]=TENANT_ID,
					["local_account_id"]=USER_OID,
					["username"]=USERNAME,
					["authority_type"]="MSSTS",
					["client_info"]=MakeClientInfo(),
				},
			};
			jObjectCache["AccessToken"]=new JObject {
				["at-1"]=new JObject {
					["home_account_id"]=_homeAccountId,
					["environment"]=ENVIRONMENT,
					["client_id"]=CLIENT_ID,
					["secret"]=accessToken,
					["credential_type"]="AccessToken",
					["realm"]=TENANT_ID,
					["target"]="User.Read Mail.Send Mail.Read",
					["cached_at"]=cachedAt,
					["expires_on"]=expiresOn,
					["extended_expires_on"]=expiresOn,
					["token_type"]="Bearer",
				},
			};
			jObjectCache["RefreshToken"]=new JObject {
				["rt-1"]=new JObject {
					["home_account_id"]=_homeAccountId,
					["environment"]=ENVIRONMENT,
					["client_id"]=CLIENT_ID,
					["secret"]=refreshToken,
					["credential_type"]="RefreshToken",
				},
			};
			jObjectCache["IdToken"]=new JObject {
				["id-1"]=new JObject {
					["home_account_id"]=_homeAccountId,
					["environment"]=ENVIRONMENT,
					["client_id"]=CLIENT_ID,
					["secret"]=MakeIdToken(),
					["credential_type"]="IdToken",
					["realm"]=TENANT_ID,
				},
			};
			jObjectCache["AppMetadata"]=new JObject {
				["appmetadata-"+ENVIRONMENT+"-"+CLIENT_ID]=new JObject {
					["environment"]=ENVIRONMENT,
					["client_id"]=CLIENT_ID,
				},
			};
			return jObjectCache.ToString(Newtonsoft.Json.Formatting.None);
		}

		private static string MakeTokenResponse(string accessToken,string refreshToken,string username=USERNAME,string oid=USER_OID) {
			JObject jObject=new JObject {
				["token_type"]="Bearer",
				["scope"]="User.Read Mail.Send Mail.Read",
				["expires_in"]=3600,
				["ext_expires_in"]=3600,
				["access_token"]=accessToken,
				["refresh_token"]=refreshToken,
				["id_token"]=MakeIdToken(username,oid),
				["client_info"]=MakeClientInfo(oid),
			};
			return jObject.ToString(Newtonsoft.Json.Formatting.None);
		}

		private static string MakeDiscoveryResponse() {
			return "{\"tenant_discovery_endpoint\":\"https://login.microsoftonline.com/"+TENANT_ID+"/v2.0/.well-known/openid-configuration\","
				+"\"api-version\":\"1.1\",\"metadata\":[{\"preferred_network\":\"login.microsoftonline.com\",\"preferred_cache\":\"login.windows.net\","
				+"\"aliases\":[\"login.microsoftonline.com\",\"login.windows.net\",\"login.microsoft.com\",\"sts.windows.net\"]}]}";
		}

		///<summary>A fake identity platform and Graph. tokenResponder answers POST .../oauth2/v2.0/token; everything else is routed by path.</summary>
		private sealed class FakeMicrosoft {
			public FakeHttpHandler Handler=new FakeHttpHandler();
			public Func<RecordedRequest,HttpResponseMessage> TokenResponder;
			public string ProfileJson="{\"id\":\""+USER_OID+"\",\"mail\":\""+USERNAME+"\",\"userPrincipalName\":\""+USERNAME+"\",\"displayName\":\"Front Desk\"}";

			public FakeMicrosoft() {
				Handler.SetDefault((request,ct) => Task.FromResult(Route(request)));
			}

			public List<RecordedRequest> TokenRequests {
				get {
					return Handler.Requests.Where(x => x.Method==HttpMethod.Post && x.RequestUri.AbsolutePath.EndsWith("/oauth2/v2.0/token")).ToList();
				}
			}

			private HttpResponseMessage Route(RecordedRequest request) {
				string path=request.RequestUri.AbsolutePath;
				if(path.Contains("/discovery/instance")) {
					return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,MakeDiscoveryResponse());
				}
				if(path.EndsWith("/oauth2/v2.0/devicecode")) {
					return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,"{\"user_code\":\"SYNTH123\",\"device_code\":\"synthetic-device-code\","
						+"\"verification_uri\":\"https://login.microsoftonline.com/common/oauth2/deviceauth\",\"expires_in\":900,\"interval\":1,"
						+"\"message\":\"Enter SYNTH123\"}");
				}
				if(path.EndsWith("/oauth2/v2.0/token")) {
					if(TokenResponder==null) {
						return FakeHttpHandler.MakeResponse(HttpStatusCode.BadRequest,"{\"error\":\"invalid_request\"}");
					}
					return TokenResponder(request);
				}
				if(request.RequestUri.Host=="graph.microsoft.com" && path=="/v1.0/me") {
					return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,ProfileJson);
				}
				return FakeHttpHandler.MakeResponse(HttpStatusCode.NotFound,"");
			}

			public Microsoft365SessionOptions MakeOptions(List<string> listAlerts=null) {
				Microsoft365SessionOptions options=new Microsoft365SessionOptions();
				options.HttpMessageHandler=Handler;
				options.GraphClient=new Microsoft365GraphClient(new Microsoft365GraphOptions {
					HttpMessageHandler=Handler,
					Delay=(timeSpan,ct) => Task.CompletedTask,
				});
				if(listAlerts!=null) {
					options.RaiseReauthenticationAlert=label => {
						lock(listAlerts) {
							listAlerts.Add(label);
						}
					};
				}
				options.WriteThrottle=new WriteThrottle(TimeSpan.FromHours(24));
				return options;
			}
		}

		///<summary>A store that throttles writes, like the emailaddress row.</summary>
		private sealed class ThrottledStore : IMicrosoft365CacheStore {
			public MemoryMicrosoft365CacheStore Inner;
			public int CountClears;

			public ThrottledStore(string storedCache) {
				Inner=new MemoryMicrosoft365CacheStore(storedCache,_homeAccountId);
			}

			public bool ThrottleWrites {
				get {
					return true;
				}
			}

			public Microsoft365StoredSignIn Read() {
				return Inner.Read();
			}

			public void Write(string storedCache,string accountMarker) {
				Inner.Write(storedCache,accountMarker);
			}

			public void Clear() {
				CountClears++;
				Inner.Clear();
			}
		}

		private static string Decrypt(string stored) {
			Assert.IsTrue(Secrets.IsEncrypted(stored),"The cache is stored as osd1$.");
			string plainText;
			Assert.IsTrue(Secrets.Decrypt(stored,out plainText));
			return plainText;
		}

		[TestMethod]
		public void Cache_RoundTripServesTheCachedTokenWithoutTheNetwork() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				string stored=SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-1",TimeSpan.FromHours(1)));
				MemoryMicrosoft365CacheStore store=new MemoryMicrosoft365CacheStore(stored,_homeAccountId);
				Microsoft365Session session=new Microsoft365Session(MakeConfig(),store,"test|1",USERNAME,fakeMicrosoft.MakeOptions());
				string accessToken=await session.GetAccessTokenAsync(Microsoft365Scopes.ListSend);
				Assert.AreEqual("synthetic-access-token-1",accessToken);
				Assert.AreEqual(0,fakeMicrosoft.TokenRequests.Count,"A valid cached token needs no token request.");
				Assert.AreEqual(_homeAccountId,session.AccountMarker);
				Assert.AreEqual(USERNAME,session.AccountUsername);
			});
		}

		[TestMethod]
		public void ForceRefresh_RedeemsTheRefreshTokenAndStoresTheNewCacheEncrypted() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				fakeMicrosoft.TokenResponder=request => FakeHttpHandler.MakeResponse(HttpStatusCode.OK,
					MakeTokenResponse("synthetic-access-token-2","synthetic-refresh-token-2"));
				string stored=SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-1",TimeSpan.FromHours(1)));
				MemoryMicrosoft365CacheStore store=new MemoryMicrosoft365CacheStore(stored,_homeAccountId);
				Microsoft365Session session=new Microsoft365Session(MakeConfig(),store,"test|2",USERNAME,fakeMicrosoft.MakeOptions());
				string accessToken=await session.GetAccessTokenAsync(Microsoft365Scopes.ListSend,forceRefresh:true);
				Assert.AreEqual("synthetic-access-token-2",accessToken);
				RecordedRequest requestToken=fakeMicrosoft.TokenRequests.Single();
				StringAssert.Contains(requestToken.Body,"grant_type=refresh_token");
				StringAssert.Contains(requestToken.Body,"synthetic-refresh-token-1");
				Assert.IsFalse(requestToken.Body.Contains("client_secret"),"A public client has no secret.");
				StringAssert.Contains(requestToken.RequestUri.AbsoluteUri,"https://login.microsoftonline.com/"+TENANT_ID+"/oauth2/v2.0/token");
				Microsoft365StoredSignIn storedSignIn=store.Read();
				Assert.AreNotEqual(stored,storedSignIn.StoredCache);
				Assert.AreEqual(_homeAccountId,storedSignIn.AccountMarker,"emailaddress.AccessToken holds the account marker, not a bearer token.");
				string cacheJson=Decrypt(storedSignIn.StoredCache);
				StringAssert.Contains(cacheJson,"synthetic-refresh-token-2","The rotated refresh token is kept.");
				Assert.AreEqual(storedSignIn.StoredCache,session.CurrentStoredCache);
				//A new session (another process) starts from the stored cache and needs no token request.
				FakeMicrosoft fakeMicrosoft2=new FakeMicrosoft();
				Microsoft365Session session2=new Microsoft365Session(MakeConfig(),new MemoryMicrosoft365CacheStore(storedSignIn.StoredCache,_homeAccountId),
					"test|2b",USERNAME,fakeMicrosoft2.MakeOptions());
				Assert.AreEqual("synthetic-access-token-2",await session2.GetAccessTokenAsync(Microsoft365Scopes.ListSend));
				Assert.AreEqual(0,fakeMicrosoft2.TokenRequests.Count);
			});
		}

		[TestMethod]
		public void ThrottledStore_WritesAtMostOnceADayPerProcess() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				int countRefreshes=0;
				fakeMicrosoft.TokenResponder=request => {
					int count=Interlocked.Increment(ref countRefreshes);
					return FakeHttpHandler.MakeResponse(HttpStatusCode.OK,MakeTokenResponse("synthetic-access-token-r"+count,"synthetic-refresh-token-r"+count));
				};
				ThrottledStore store=new ThrottledStore(SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-1",TimeSpan.FromHours(1))));
				Microsoft365Session session=new Microsoft365Session(MakeConfig(),store,"test|3",USERNAME,fakeMicrosoft.MakeOptions());
				await session.GetAccessTokenAsync(Microsoft365Scopes.ListSend,forceRefresh:true);
				await session.GetAccessTokenAsync(Microsoft365Scopes.ListSend,forceRefresh:true);
				await session.GetAccessTokenAsync(Microsoft365Scopes.ListSend,forceRefresh:true);
				Assert.AreEqual(3,fakeMicrosoft.TokenRequests.Count);
				Assert.AreEqual(1,store.Inner.CountWrites,"Hourly refreshes don't write; the first refresh in a process does.");
				StringAssert.Contains(Decrypt(session.CurrentStoredCache),"synthetic-refresh-token-r3","The newest chain stays in memory.");
				StringAssert.Contains(Decrypt(store.Read().StoredCache),"synthetic-refresh-token-r1");
			});
		}

		[TestMethod]
		public void Revoked_NeedsReauthenticationAlertsOnceAndFailsFast() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				fakeMicrosoft.TokenResponder=request => FakeHttpHandler.MakeResponse(HttpStatusCode.BadRequest,
					"{\"error\":\"invalid_grant\",\"error_description\":\"AADSTS50173: The provided grant has expired due to it being revoked.\","
					+"\"error_codes\":[50173],\"suberror\":\"bad_token\"}");
				List<string> listAlerts=new List<string>();
				string stored=SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-old",TimeSpan.FromMinutes(-10)));
				Microsoft365Session session=new Microsoft365Session(MakeConfig(),new MemoryMicrosoft365CacheStore(stored,_homeAccountId),"test|4|"+Guid.NewGuid(),
					USERNAME,fakeMicrosoft.MakeOptions(listAlerts));
				Microsoft365AuthException ex=await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => session.GetAccessTokenAsync(Microsoft365Scopes.ListSend));
				Assert.AreEqual(Microsoft365ErrorKind.NeedsSignIn,ex.Kind);
				Assert.IsTrue(session.NeedsReauthentication);
				int countRequests=fakeMicrosoft.Handler.Requests.Count;
				await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => session.GetAccessTokenAsync(Microsoft365Scopes.ListSend));
				await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => session.GetAccessTokenAsync(Microsoft365Scopes.ListSend,forceRefresh:true));
				Assert.AreEqual(countRequests,fakeMicrosoft.Handler.Requests.Count,"Later calls fail at once, with no network call.");
				CollectionAssert.AreEqual(new[] { USERNAME },listAlerts,"One alert, not one per send.");
				//A new sign-in stored elsewhere starts the session over.
				fakeMicrosoft.TokenResponder=null;
				string storedNew=SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-new",TimeSpan.FromHours(1)));
				session.NoteStoredValue(storedNew);
				Assert.IsFalse(session.NeedsReauthentication);
			});
		}

		[TestMethod]
		public void LegacyCache_IsClearedNeverDecryptedAndRaisesTheAlert() {
			AssertEx.Run(async () => {
				foreach(string legacyValue in new[] { "bGVnYWN5LW1zYWwtY2FjaGUtZW5jcnlwdGVkLXdpdGgtYS1maXhlZC1rZXk=","odv2e$c29tZXRoaW5n","osd1$not-really-encrypted" }) {
					FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
					List<string> listAlerts=new List<string>();
					ThrottledStore store=new ThrottledStore(legacyValue);
					Microsoft365Session session=new Microsoft365Session(MakeConfig(),store,"test|5|"+Guid.NewGuid(),USERNAME,fakeMicrosoft.MakeOptions(listAlerts));
					Microsoft365AuthException ex=await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => session.GetAccessTokenAsync());
					Assert.AreEqual(Microsoft365ErrorKind.NeedsSignIn,ex.Kind);
					Assert.AreEqual(1,store.CountClears,"The row is blanked on first use: "+legacyValue);
					Assert.AreEqual("",store.Read().StoredCache);
					Assert.AreEqual(1,listAlerts.Count);
					Assert.AreEqual(0,fakeMicrosoft.Handler.Requests.Count,"Nothing is sent to Microsoft.");
				}
			});
		}

		[TestMethod]
		public void NotSignedIn_FailsWithoutAnAlertOrNetwork() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				List<string> listAlerts=new List<string>();
				Microsoft365Session session=new Microsoft365Session(MakeConfig(),new MemoryMicrosoft365CacheStore(),"test|6|"+Guid.NewGuid(),USERNAME,
					fakeMicrosoft.MakeOptions(listAlerts));
				Microsoft365AuthException ex=await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => session.GetAccessTokenAsync());
				Assert.AreEqual(Microsoft365Errors.MessageNeedsSignIn,ex.Message);
				Assert.AreEqual(0,listAlerts.Count);
				Assert.AreEqual(0,fakeMicrosoft.Handler.Requests.Count);
			});
		}

		[TestMethod]
		public void NotConfigured_MakesNoNetworkCall() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				string stored=SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-1",TimeSpan.FromHours(1)));
				Microsoft365Config config=new Microsoft365Config();
				Microsoft365Session session=new Microsoft365Session(config,new MemoryMicrosoft365CacheStore(stored,_homeAccountId),"test|7",USERNAME,
					fakeMicrosoft.MakeOptions());
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(() => session.GetAccessTokenAsync());
				Assert.AreEqual(Microsoft365ErrorKind.NotConfigured,ex.Kind);
				Assert.AreEqual(0,fakeMicrosoft.Handler.Requests.Count);
			});
		}

		[TestMethod]
		public void SessionGraphCall_RefreshesOnceOnA401() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				fakeMicrosoft.TokenResponder=request => FakeHttpHandler.MakeResponse(HttpStatusCode.OK,
					MakeTokenResponse("synthetic-access-token-2","synthetic-refresh-token-2"));
				bool hasRejected=false;
				string stored=SecretsTestHelper.Protect(MakeSyntheticCache("synthetic-access-token-1",TimeSpan.FromHours(1)));
				Microsoft365Session session=new Microsoft365Session(MakeConfig(),new MemoryMicrosoft365CacheStore(stored,_homeAccountId),"test|8",USERNAME,
					fakeMicrosoft.MakeOptions());
				fakeMicrosoft.Handler.SetDefault((request,ct) => {
					if(request.RequestUri.Host=="graph.microsoft.com" && !hasRejected) {
						hasRejected=true;
						return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.Unauthorized,"{\"error\":{\"code\":\"InvalidAuthenticationToken\"}}"));
					}
					if(request.RequestUri.Host=="graph.microsoft.com") {
						return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,fakeMicrosoft.ProfileJson));
					}
					if(request.RequestUri.AbsolutePath.EndsWith("/oauth2/v2.0/token")) {
						return Task.FromResult(fakeMicrosoft.TokenResponder(request));
					}
					if(request.RequestUri.AbsolutePath.Contains("/discovery/instance")) {
						return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,MakeDiscoveryResponse()));
					}
					return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.NotFound,""));
				});
				MicrosoftProfile microsoftProfile=await session.GetProfileAsync();
				Assert.AreEqual(USERNAME,microsoftProfile.Mail);
				List<RecordedRequest> listGraph=fakeMicrosoft.Handler.Requests.Where(x => x.RequestUri.Host=="graph.microsoft.com").ToList();
				Assert.AreEqual(2,listGraph.Count);
				Assert.AreEqual("Bearer synthetic-access-token-1",listGraph[0].GetHeader("Authorization"));
				Assert.AreEqual("Bearer synthetic-access-token-2",listGraph[1].GetHeader("Authorization"),"Forced refresh after the 401.");
				Assert.AreEqual(1,fakeMicrosoft.TokenRequests.Count);
			});
		}

		[TestMethod]
		public void DeviceCodeSignIn_ChecksTheAccountAndReturnsTheEncryptedCache() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				fakeMicrosoft.TokenResponder=request => FakeHttpHandler.MakeResponse(HttpStatusCode.OK,
					MakeTokenResponse("synthetic-access-token-signin","synthetic-refresh-token-signin"));
				Microsoft365SessionOptions options=fakeMicrosoft.MakeOptions();
				List<Microsoft365DeviceCode> listCodes=new List<Microsoft365DeviceCode>();
				options.DeviceCodeHandler=deviceCode => {
					listCodes.Add(deviceCode);
					return Task.CompletedTask;
				};
				Microsoft365SignInResult result=await Microsoft365Session.SignInInteractiveAsync(MakeConfig(Microsoft365SignInMethod.DeviceCode),
					USERNAME.ToUpperInvariant(),Microsoft365Scopes.GetSignInScopes(false),CancellationToken.None,options);
				Assert.AreEqual("SYNTH123",listCodes.Single().UserCode);
				Assert.AreEqual(_homeAccountId,result.AccountMarker);
				Assert.AreEqual(USERNAME,result.Profile.GetAddress());
				string cacheJson=Decrypt(result.StoredCache);
				StringAssert.Contains(cacheJson,"synthetic-refresh-token-signin");
				RecordedRequest requestDeviceCode=fakeMicrosoft.Handler.Requests.First(x => x.RequestUri.AbsolutePath.EndsWith("/devicecode"));
				StringAssert.Contains(Uri.UnescapeDataString(requestDeviceCode.Body),"Mail.Send");
				Assert.IsFalse(Uri.UnescapeDataString(requestDeviceCode.Body).Contains("Mail.Read "),"Mail.Read only when inbox download is on.");
				Assert.IsTrue(fakeMicrosoft.Handler.Requests.Any(x => x.RequestUri.AbsoluteUri.StartsWith("https://graph.microsoft.com/v1.0/me")),
					"The account is checked with GET /me.");
			});
		}

		[TestMethod]
		public void DeviceCodeSignIn_WrongAccountKeepsNothing() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				fakeMicrosoft.TokenResponder=request => FakeHttpHandler.MakeResponse(HttpStatusCode.OK,
					MakeTokenResponse("synthetic-access-token-signin","synthetic-refresh-token-signin"));
				Microsoft365SessionOptions options=fakeMicrosoft.MakeOptions();
				options.DeviceCodeHandler=deviceCode => Task.CompletedTask;
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(() => Microsoft365Session.SignInInteractiveAsync(
					MakeConfig(Microsoft365SignInMethod.DeviceCode),"billing@example-dental.test",Microsoft365Scopes.GetSignInScopes(false),
					CancellationToken.None,options));
				Assert.AreEqual(Microsoft365ErrorKind.WrongAccount,ex.Kind);
			});
		}

		[TestMethod]
		public void DeviceCodeSignIn_AppRegistrationErrorIsNeutral() {
			AssertEx.Run(async () => {
				FakeMicrosoft fakeMicrosoft=new FakeMicrosoft();
				fakeMicrosoft.TokenResponder=request => FakeHttpHandler.MakeResponse(HttpStatusCode.BadRequest,
					"{\"error\":\"unauthorized_client\",\"error_description\":\"AADSTS700016: Application with identifier '"+CLIENT_ID+"' was not found in "
					+"the directory.\",\"error_codes\":[700016]}");
				Microsoft365SessionOptions options=fakeMicrosoft.MakeOptions();
				options.DeviceCodeHandler=deviceCode => Task.CompletedTask;
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(() => Microsoft365Session.SignInInteractiveAsync(
					MakeConfig(Microsoft365SignInMethod.DeviceCode),USERNAME,Microsoft365Scopes.GetSignInScopes(false),CancellationToken.None,options));
				Assert.AreEqual(Microsoft365ErrorKind.AppRegistration,ex.Kind);
				Assert.AreEqual(Microsoft365Errors.MessageAppRegistration,ex.Message);
			});
		}

		[TestMethod]
		public void IdentityCalls_NeverReachAnotherHost() {
			AssertEx.Run(async () => {
				//The identity HttpClient goes through the vendor policy: an Open Dental host or a host outside the Microsoft list is refused.
				Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check(new Uri("https://login.microsoftonline.com/x"),Microsoft365Session.ListIdentityHosts));
				Assert.AreNotEqual(VendorUrlProblem.None,VendorUrlPolicy.Check(new Uri("https://login.example.test/x"),Microsoft365Session.ListIdentityHosts));
				Assert.AreNotEqual(VendorUrlProblem.None,VendorUrlPolicy.Check(new Uri("https://graph.microsoft.com/x"),Microsoft365Session.ListIdentityHosts));
				await Task.CompletedTask;
			});
		}
	}
}
