// Tests for the PostcardMania (PCM Integrations) DirectMail API v3 integration: placing orders safely (409, timeouts, retries), the BAA gate,
// estimates, address checks and recent orders. No network: a fake HTTP handler or the in-memory mock.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.PostcardMania;

namespace VendorDirectTests.PostcardMania {
	[TestClass]
	public class PostcardManiaOrderTests {
		private Func<bool> _funcIsBaaOnFileOld;

		[TestInitialize]
		public void TestInitialize() {
			_funcIsBaaOnFileOld=AdvertisingPostcards.FuncIsBaaOnFile;
			AdvertisingPostcards.FuncIsBaaOnFile=() => true;
		}

		[TestCleanup]
		public void TestCleanup() {
			AdvertisingPostcards.FuncIsBaaOnFile=_funcIsBaaOnFileOld;
			PostcardManiaApiClient.Inst=null;
		}

		private static PostcardManiaFixture UseFixture() {
			PostcardManiaFixture fixture=new PostcardManiaFixture();
			PostcardManiaApiClient.Inst=fixture.Client;
			return fixture;
		}

		private static string OrderListJson(string extRefNbr,long orderID) {
			return "{\"results\":[{\"orderID\":"+orderID+",\"batchID\":7,\"status\":\"Pending\",\"extRefNbr\":\""+extRefNbr+"\"}]}";
		}

		[TestMethod]
		public void PlaceOrder_TimeoutLooksTheOrderUpAndNeverPostsAgain() {
			PostcardManiaFixture fixture=UseFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((request,ct) => throw new TaskCanceledException("Simulated timeout."));
			fixture.Handler.Enqueue(HttpStatusCode.OK,OrderListJson("ops-0-a",5001));
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(PostcardManiaFixture.MakeAccount(),
				new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder("ops-0-a") },isRetry:false);
			Assert.IsTrue(outcome.IsComplete,"The vendor had the order: it counts as placed.");
			Assert.AreEqual(5001,outcome.ListResults.Single().OrderID);
			Assert.IsFalse(outcome.ListResults.Single().WasAlreadyPlaced);
			Assert.AreEqual(1,fixture.RequestsTo("POST order/postcard").Count);
			List<RecordedRequest> listLookups=fixture.RequestsTo("GET order");
			Assert.AreEqual(1,listLookups.Count);
			StringAssert.Contains(listLookups[0].RequestUri.Query,"extRefNbr=ops-0-a");
		}

		[TestMethod]
		public void PlaceOrder_TimeoutWithNoOrderFoundIsUnknownAndStops() {
			PostcardManiaFixture fixture=UseFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((request,ct) => throw new TaskCanceledException("Simulated timeout."));
			//The vendor ignores the filter and lists someone else's order: it must not count as ours.
			fixture.Handler.Enqueue(HttpStatusCode.OK,OrderListJson("ops-0-other",4000));
			List<PcmPostcardOrderRequest> listOrders=new List<PcmPostcardOrderRequest> {
				PostcardManiaFixture.MakeOrder("ops-0-b-1"),
				PostcardManiaFixture.MakeOrder("ops-0-b-2"),
			};
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(PostcardManiaFixture.MakeAccount(),listOrders,isRetry:false);
			Assert.IsFalse(outcome.IsComplete);
			Assert.IsTrue(outcome.IsOutcomeUnknown);
			Assert.AreEqual(PostcardManiaErrorKind.Timeout,outcome.Error.Kind);
			Assert.AreEqual(0,outcome.ListResults.Count);
			Assert.AreEqual(1,fixture.RequestsTo("POST order/postcard").Count,"Never a second POST, and the second order isn't tried.");
		}

		[TestMethod]
		public void PlaceOrder_ConflictMeansAlreadyPlaced() {
			PostcardManiaFixture fixture=UseFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue(HttpStatusCode.Conflict,"{\"error\":{\"code\":409,\"message\":\"One or more errors occurred\",\"data\":[{\"message\":\"Duplicate extRefNbr\"}]}}");
			fixture.Handler.Enqueue(HttpStatusCode.OK,OrderListJson("ops-0-c",5002));
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(PostcardManiaFixture.MakeAccount(),
				new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder("ops-0-c") },isRetry:false);
			Assert.IsTrue(outcome.IsComplete);
			Assert.IsTrue(outcome.ListResults.Single().WasAlreadyPlaced);
			Assert.AreEqual(5002,outcome.ListResults.Single().OrderID);
			Assert.AreEqual(1,fixture.RequestsTo("POST order/postcard").Count);
		}

		[TestMethod]
		public void PlaceOrder_RetryLooksUpFirstAndPlacesOnlyWhatIsMissing() {
			PostcardManiaFixture fixture=UseFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue(HttpStatusCode.OK,OrderListJson("ops-0-d-1",6001));//First order exists.
			fixture.Handler.Enqueue(HttpStatusCode.OK,"{\"results\":[]}");//Second doesn't.
			fixture.Handler.Enqueue(HttpStatusCode.Created,"{\"orderID\":6002,\"batchID\":8,\"extRefNbr\":\"ops-0-d-2\"}");
			List<PcmPostcardOrderRequest> listOrders=new List<PcmPostcardOrderRequest> {
				PostcardManiaFixture.MakeOrder("ops-0-d-1"),
				PostcardManiaFixture.MakeOrder("ops-0-d-2",3),
			};
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(PostcardManiaFixture.MakeAccount(),listOrders,isRetry:true);
			Assert.IsTrue(outcome.IsComplete);
			Assert.AreEqual(2,outcome.ListResults.Count);
			Assert.IsTrue(outcome.ListResults[0].WasAlreadyPlaced);
			Assert.AreEqual(6002,outcome.ListResults[1].OrderID);
			Assert.AreEqual(3,outcome.ListResults[1].CountRecipients);
			List<RecordedRequest> listPosts=fixture.RequestsTo("POST order/postcard");
			Assert.AreEqual(1,listPosts.Count);
			StringAssert.Contains(listPosts[0].Body,"\"extRefNbr\":\"ops-0-d-2\"");
		}

		[TestMethod]
		public void PlaceOrder_ValidationErrorStopsWithTheVendorsReason() {
			PostcardManiaFixture fixture=UseFixture();
			fixture.EnqueueLogin();
			fixture.Handler.Enqueue((HttpStatusCode)422,"{\"error\":{\"code\":422,\"data\":[{\"message\":\"Recipient failed verification\"}]}}");
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(PostcardManiaFixture.MakeAccount(),
				new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder("ops-0-e") },isRetry:false);
			Assert.IsFalse(outcome.IsComplete);
			Assert.IsFalse(outcome.IsOutcomeUnknown,"A 422 means nothing was placed.");
			CollectionAssert.AreEqual(new[] { "Recipient failed verification" },outcome.Error.Messages);
			Assert.AreEqual(0,fixture.RequestsTo("GET order").Count);
		}

		[TestMethod]
		public void BaaNotOnFile_BlocksEverythingThatSendsRecipients() {
			PostcardManiaFixture fixture=UseFixture();
			AdvertisingPostcards.FuncIsBaaOnFile=() => false;
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			List<PcmPostcardOrderRequest> listOrders=new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder("ops-0-f") };
			AssertEx.Throws<ODException>(() => { AdvertisingPostcards.PlaceOrder(account,listOrders,false); });
			AssertEx.Throws<ODException>(() => { AdvertisingPostcards.Estimate(account,listOrders); });
			AssertEx.Throws<ODException>(() => { AdvertisingPostcards.VerifyRecipients(account,PostcardManiaFixture.MakeRecipients(2)); });
			Assert.AreEqual(0,fixture.Handler.Requests.Count,"Nothing reaches the vendor.");
		}

		[TestMethod]
		public void Estimate_SumsTheOrders() {
			PostcardManiaApiClientMock mock=new PostcardManiaApiClientMock { PricePerCard=0.5m };
			PostcardManiaApiClient.Inst=mock;
			List<PcmPostcardOrderRequest> listOrders=AdvertisingPostcards.SplitIntoOrders(PostcardManiaFixture.MakeOrder("x",0),
				PostcardManiaFixture.MakeRecipients(50001),"ops-0-g");
			PcmPricingResponse response=AdvertisingPostcards.Estimate(PostcardManiaFixture.MakeAccount(),listOrders);
			Assert.AreEqual(25000.5m,response.EstimatedTotalPrice);
			Assert.AreEqual(2,response.LineItems.Count);
		}

		[TestMethod]
		public void VerifyRecipients_ChunksAndReportsInvalidIndexes() {
			PostcardManiaApiClientMock mock=new PostcardManiaApiClientMock();
			mock.FuncIsAddressInvalid=x => x.Address=="3 Main St" || x.Address=="1500 Main St";
			PostcardManiaApiClient.Inst=mock;
			PostcardVerifyResult result=AdvertisingPostcards.VerifyRecipients(PostcardManiaFixture.MakeAccount(),PostcardManiaFixture.MakeRecipients(2000));
			CollectionAssert.AreEqual(new[] { 3,1500 },result.ListInvalidIndexes);
			Assert.AreEqual(1998,result.CountValid);
			Assert.AreEqual(2,result.CountInvalid);
		}

		[TestMethod]
		public void Mock_PlaceThenCancelAndRecentOrdersForTheClinic() {
			PostcardManiaApiClientMock mock=new PostcardManiaApiClientMock();
			PostcardManiaApiClient.Inst=mock;
			PostcardManiaAccount account=PostcardManiaFixture.MakeAccount();
			string extRefNbr=AdvertisingPostcards.NewExtRefNbr(4);
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(account,new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder(extRefNbr) },false);
			Assert.IsTrue(outcome.IsComplete);
			mock.ListOrders.Add(new PcmOrder { OrderID=1,Status="Processed",ExtRefNbr="someone-else" });
			List<PcmOrder> listOrders=AdvertisingPostcards.GetRecentOrders(account,4,1,50);
			Assert.AreEqual(1,listOrders.Count,"Only this program's orders for the clinic.");
			Assert.AreEqual(2,AdvertisingPostcards.GetRecentOrders(account,null,1,50).Count);
			AdvertisingPostcards.CancelOrder(account,listOrders[0].OrderID);
			Assert.AreEqual("Canceled",mock.ListOrders.Single(x => x.ExtRefNbr==extRefNbr).Status);
			PostcardManiaException ex=PostcardManiaApiClientTests.AssertThrows(() => AdvertisingPostcards.CancelOrder(account,1));
			Assert.AreEqual(PostcardManiaErrorKind.Validation,ex.Kind,"A processed order can't be canceled.");
			//Placing the same mailing again is answered from the vendor's 409, never a second order.
			outcome=AdvertisingPostcards.PlaceOrder(account,new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder(extRefNbr) },false);
			Assert.IsTrue(outcome.ListResults.Single().WasAlreadyPlaced);
			Assert.AreEqual(1,mock.ListOrders.Count(x => x.ExtRefNbr==extRefNbr));
		}

		[TestMethod]
		public void Mock_TimeoutAfterTheVendorStoredTheOrderIsFound() {
			PostcardManiaApiClientMock mock=new PostcardManiaApiClientMock();
			mock.FuncPlaceFailure=x => new PostcardManiaException(PostcardManiaErrorKind.Timeout,"Simulated timeout.");
			mock.IsPlacedBeforeFailure=true;
			PostcardManiaApiClient.Inst=mock;
			PostcardOrderOutcome outcome=AdvertisingPostcards.PlaceOrder(PostcardManiaFixture.MakeAccount(),
				new List<PcmPostcardOrderRequest> { PostcardManiaFixture.MakeOrder("ops-0-h") },false);
			Assert.IsTrue(outcome.IsComplete);
			Assert.AreEqual(1,mock.ListPlaceRequests.Count);
			CollectionAssert.AreEqual(new[] { "ops-0-h" },mock.ListOrderLookups);
		}

		[TestMethod]
		public void GetDesigns_ReadsEveryPage() {
			PostcardManiaApiClientMock mock=new PostcardManiaApiClientMock();
			for(int i=0;i<250;i++) {
				mock.ListDesigns.Add(new PcmDesign { DesignID=i+1,FriendlyName="D"+i });
			}
			PostcardManiaApiClient.Inst=mock;
			Assert.AreEqual(250,AdvertisingPostcards.GetDesigns(PostcardManiaFixture.MakeAccount()).Count);
		}
	}
}
