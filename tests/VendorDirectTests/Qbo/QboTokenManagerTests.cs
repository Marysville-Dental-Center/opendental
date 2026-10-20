// Tests for the QuickBooks Online token manager: the rotating refresh token with compare-and-swap, the expiry write, and "needs reconnect".
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness.Qbo;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboTokenManagerTests {
		private static OAuthAccessToken GetToken(QboTestKit kit) {
			return Task.Run(() => kit.TokenManager.GetAccessTokenAsync()).GetAwaiter().GetResult();
		}

		[TestMethod]
		public void Refresh_StoresTheRotatedTokenWithCompareAndSwap_AndSignals() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-2"));
			Assert.AreEqual("at-1",GetToken(kit).Value);
			Assert.AreEqual("rt-2",kit.Store.Current);
			CollectionAssert.AreEqual(new List<string> { "rt-2" },kit.Store.ListReplacements);
			Assert.AreEqual(1,kit.Store.CountExpiryWrites);
			Assert.AreEqual(1,kit.Store.CountSignals);
			//The access token is cached: no second call to Intuit.
			Assert.AreEqual("at-1",GetToken(kit).Value);
			Assert.AreEqual(1,kit.HandlerOAuth.Requests.Count);
		}

		[TestMethod]
		public void Refresh_UnchangedTokenAndExpiry_WritesNothing() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.Store.RefreshTokenExpiresUtc=DateTime.UtcNow.AddSeconds(8726400).AddHours(-2);
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-1"));
			GetToken(kit);
			Assert.AreEqual(0,kit.Store.ListReplacements.Count);
			Assert.AreEqual(0,kit.Store.CountExpiryWrites,"An expiry that moved less than a day isn't rewritten.");
			Assert.AreEqual(0,kit.Store.CountSignals);
		}

		[TestMethod]
		public void Refresh_ReadsTheDatabaseEachTime() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-1"));
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-2","rt-5"));
			GetToken(kit);
			kit.Store.Current="rt-4";//Another workstation rotated it.
			kit.TokenManager.Invalidate();
			Assert.AreEqual("at-2",GetToken(kit).Value);
			StringAssert.Contains(kit.HandlerOAuth.Requests[1].Body,"refresh_token=rt-4");
			Assert.AreEqual("rt-5",kit.Store.Current);
		}

		[TestMethod]
		public void Refresh_LostCompareAndSwap_KeepsTheOtherWorkstationsValue() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.Store.ValueStoredByOtherWorkstation="rt-other";
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-2"));
			Assert.AreEqual("at-1",GetToken(kit).Value,"Our access token still works for its hour.");
			Assert.AreEqual("rt-other",kit.Store.Current,"A newer value stored elsewhere is never overwritten.");
			Assert.AreEqual(0,kit.Store.ListReplacements.Count);
		}

		[TestMethod]
		public void InvalidGrant_ReReadsOnceAndRetriesWithTheRotatedValue() {
			QboTestKit kit=new QboTestKit("rt-old");
			kit.HandlerOAuth.Enqueue((request,ct) => {
				kit.Store.Current="rt-new";//Another workstation rotated it while this one was refreshing.
				return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.BadRequest,QboTestKit.OAuthErrorJson("invalid_grant")));
			});
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-newer"));
			Assert.AreEqual("at-1",GetToken(kit).Value);
			StringAssert.Contains(kit.HandlerOAuth.Requests[1].Body,"refresh_token=rt-new");
			Assert.AreEqual("rt-newer",kit.Store.Current);
			Assert.IsFalse(kit.TokenManager.NeedsReconnect);
		}

		[TestMethod]
		public void InvalidGrant_NeedsReconnect_AndLaterCallsFailWithoutTheNetwork() {
			QboTestKit kit=new QboTestKit("rt-revoked");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.BadRequest,QboTestKit.OAuthErrorJson("invalid_grant"));
			QboAuthException ex=AssertEx.Throws<QboAuthException>(() => GetToken(kit));
			Assert.AreEqual(QboAuthException.MessageReconnect,ex.Message);
			Assert.IsTrue(ex.NeedsReconnect);
			Assert.IsTrue(kit.TokenManager.NeedsReconnect);
			int countRequests=kit.HandlerOAuth.Requests.Count;
			AssertEx.Throws<QboAuthException>(() => GetToken(kit));
			Assert.AreEqual(countRequests,kit.HandlerOAuth.Requests.Count,"No network call after a definite rejection.");
			//Someone reconnected (from any workstation): the stored token changed, so this process tries again.
			kit.Store.Current="rt-reconnected";
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-9","rt-reconnected"));
			Assert.AreEqual("at-9",GetToken(kit).Value);
			Assert.IsFalse(kit.TokenManager.NeedsReconnect);
		}

		[TestMethod]
		public void InvalidClient_SaysToCheckTheKeys() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.Unauthorized,QboTestKit.OAuthErrorJson("invalid_client"));
			QboAuthException ex=AssertEx.Throws<QboAuthException>(() => GetToken(kit));
			Assert.AreEqual(QboAuthException.MessageInvalidClient,ex.Message);
			Assert.IsFalse(ex.NeedsReconnect);
		}

		[TestMethod]
		public void NetworkFailure_DoesNotMarkTheConnectionRefused() {
			QboTestKit kit=new QboTestKit("rt-1");
			kit.HandlerOAuth.EnqueueNetworkFailure();
			AssertEx.Throws<System.Net.Http.HttpRequestException>(() => GetToken(kit));
			Assert.IsFalse(kit.TokenManager.NeedsReconnect);
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-1"));
			Assert.AreEqual("at-1",GetToken(kit).Value);
		}

		[TestMethod]
		public void NoStoredToken_NeedsReconnect() {
			QboTestKit kit=new QboTestKit("");
			AssertEx.Throws<QboAuthException>(() => GetToken(kit));
			Assert.AreEqual(0,kit.HandlerOAuth.Requests.Count);
		}

		[TestMethod]
		public void Prime_UsesTheNewTokenAndClearsReconnect() {
			QboTestKit kit=new QboTestKit("rt-revoked");
			kit.HandlerOAuth.Enqueue(HttpStatusCode.BadRequest,QboTestKit.OAuthErrorJson("invalid_grant"));
			AssertEx.Throws<QboAuthException>(() => GetToken(kit));
			QboTokenResult tokenResult=QboOAuth.ParseTokenResponse(200,QboTestKit.TokenJson("at-new","rt-new"),DateTime.UtcNow);
			kit.TokenManager.Prime(tokenResult);
			Assert.IsFalse(kit.TokenManager.NeedsReconnect);
			Assert.AreEqual("at-new",GetToken(kit).Value);
			Assert.AreEqual(1,kit.HandlerOAuth.Requests.Count);
		}
	}
}
