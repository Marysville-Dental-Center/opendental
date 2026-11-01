// V-GP: one transaction from start to finish against a fake Global Payments: the terminal wait (polling, cancel, timeout, network blips), saved cards
// and voids. A fake clock makes the waits instant.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.GlobalPayments;

namespace VGPTests {
	[TestClass]
	public class GpV2TransactionsTests {
		private DateTime _dateTimeNow;
		private GpFakeHttp _gpFakeHttp;
		private GpV2Transactions _gpV2Transactions;
		private List<string> _listStatuses;
		///<summary>Called on each poll delay; a test can cancel from it.</summary>
		private Action<int> _actionOnDelay;
		private int _countDelays;

		[TestInitialize]
		public void TestInitialize() {
			_dateTimeNow=new DateTime(2026,10,25,15,0,0,DateTimeKind.Utc);
			_gpFakeHttp=new GpFakeHttp();
			_listStatuses=new List<string>();
			_countDelays=0;
			_actionOnDelay=null;
			GpV2Client gpV2Client=new GpV2Client(_gpFakeHttp,() => _dateTimeNow,x => { });
			_gpV2Transactions=new GpV2Transactions(gpV2Client,() => _dateTimeNow,(timeSpan,ct) => {
				ct.ThrowIfCancellationRequested();
				_dateTimeNow=_dateTimeNow.Add(timeSpan);
				_countDelays++;
				_actionOnDelay?.Invoke(_countDelays);
				return System.Threading.Tasks.Task.CompletedTask;
			});
		}

		private static GlobalPaymentsV2Config MakeConfig() {
			GlobalPaymentsV2Config config=new GlobalPaymentsV2Config();
			config.AppId="app";
			config.AppKey="key";
			config.AccountName="acct";
			config.UseSandbox=true;
			config.Fingerprint=Guid.NewGuid().ToString("N");
			return config;
		}

		private static PayTerminal MakeTerminal() {
			return new PayTerminal { Name="Front Desk",TerminalID="T-100",CCIntegration=PayTerminals.CCIntegrationMeetInTheCloud };
		}

		private GlobalPaymentsV2Result RunTerminal(GpV2TransType transType,decimal amount,CancellationToken cancellationToken) {
			return VendorHttp.RunSync(() => _gpV2Transactions.RunTerminalAsync(MakeConfig(),MakeTerminal(),transType,amount,
				x => _listStatuses.Add(x),cancellationToken));
		}

		[TestMethod]
		public void Terminal_WaitsForTheCardThenApproves() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_1"));
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_1\",\"status\":\"INITIATED\"}");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_1\",\"status\":\"PENDING\"}");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceCompleted("DVC_1","TRN_1","2500"));
			GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,25m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual("TRN_1",result.TransactionId);
			Assert.AreEqual("DVC_1",result.DeviceCommandId);
			Assert.AreEqual("Front Desk",result.TerminalName);
			Assert.AreEqual("T-100",result.TerminalId);
			Assert.AreEqual(25m,result.Amount);
			Assert.IsTrue(result.IsSandbox);
			Assert.AreEqual(4,_gpFakeHttp.ApiRequests.Count,"One start and three result queries.");
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count(x => (string)x.GetJson()["action_type"]=="AUTHORIZE"),"The sale is sent once.");
			Assert.IsTrue(_listStatuses.Any(x => x.Contains("Waiting for the card")));
		}

		[TestMethod]
		public void Terminal_Declined() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_2"));
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceCompleted("DVC_2","TRN_2","2500","51"));
			GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,25m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.Declined,result.Outcome);
			Assert.IsFalse(result.IsInDoubt);
		}

		[TestMethod]
		public void Terminal_ANetworkBlipWhileWaitingDoesNotLoseTheResult() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_3"));
			_gpFakeHttp.EnqueueNetworkFailure();
			_gpFakeHttp.Enqueue(HttpStatusCode.ServiceUnavailable,"");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceCompleted("DVC_3","TRN_3","100"));
			GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,1m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count(x => (string)x.GetJson()["action_type"]=="AUTHORIZE"));
		}

		[TestMethod]
		public void Terminal_StoppingWaiting_ChecksOnceMore_ThenCancelled() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_4"));
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_4\",\"status\":\"INITIATED\"}");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_4\",\"status\":\"INITIATED\"}");
			using(CancellationTokenSource cancellationTokenSource=new CancellationTokenSource()) {
				_actionOnDelay=x => {
					if(x==1) {
						cancellationTokenSource.Cancel();
					}
				};
				GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,10m,cancellationTokenSource.Token);
				Assert.AreEqual(GpV2Outcome.Cancelled,result.Outcome);
				Assert.IsTrue(result.IsInDoubt,"The terminal may still finish it.");
				StringAssert.Contains(result.Message,"check the terminal");
				Assert.AreEqual("DVC_4",result.DeviceCommandId);
			}
		}

		[TestMethod]
		public void Terminal_StoppingWaiting_ButTheCardWasAlreadyCharged_IsApproved() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_5"));
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_5\",\"status\":\"INITIATED\"}");
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceCompleted("DVC_5","TRN_5","1000"));
			using(CancellationTokenSource cancellationTokenSource=new CancellationTokenSource()) {
				//Stop waiting during the first delay: the query after it still says INITIATED, then the last look finds the finished sale.
				_actionOnDelay=x => {
					if(x==1) {
						cancellationTokenSource.Cancel();
					}
				};
				GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,10m,cancellationTokenSource.Token);
				Assert.AreEqual(GpV2Outcome.Approved,result.Outcome,"The last look found the finished sale, so it is recorded.");
				Assert.AreEqual(1,_countDelays);
				Assert.AreEqual("TRN_5",result.TransactionId);
			}
		}

		[TestMethod]
		public void Terminal_NoAnswerInTime_TimesOut() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.DeviceInitiated("DVC_6"));
			for(int i=0;i<200;i++) {
				_gpFakeHttp.Enqueue(HttpStatusCode.OK,"{\"id\":\"DVC_6\",\"status\":\"INITIATED\"}");
			}
			GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,10m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.TimedOut,result.Outcome);
			Assert.IsTrue(result.IsInDoubt);
			int countQueries=_gpFakeHttp.ApiRequests.Count-1;
			Assert.AreEqual((int)(GpV2Transactions.TerminalWaitDefault.TotalSeconds/GpV2Transactions.PollIntervalDefault.TotalSeconds),countQueries);
		}

		[TestMethod]
		public void Terminal_StartWithoutAnAnswer_IsUnknownAndNotResent() {
			_gpFakeHttp.EnqueueNetworkFailure();
			GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Sale,10m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.Unknown,result.Outcome);
			Assert.AreEqual(1,_gpFakeHttp.ApiRequests.Count);
		}

		[TestMethod]
		public void Terminal_RejectedStart_IsFailed() {
			_gpFakeHttp.Enqueue(HttpStatusCode.BadRequest,GpSamples.Error("INVALID_REQUEST_DATA","Unknown device_reference"));
			GlobalPaymentsV2Result result=RunTerminal(GpV2TransType.Refund,10m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.Failed,result.Outcome);
			StringAssert.Contains(result.Message,"Unknown device_reference");
			Assert.IsFalse(result.IsInDoubt);
		}

		[TestMethod]
		public void Terminal_InputChecks_SendNothing() {
			GlobalPaymentsV2Result result=VendorHttp.RunSync(() => _gpV2Transactions.RunTerminalAsync(MakeConfig(),new PayTerminal(),GpV2TransType.Sale,10m,
				null,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Failed,result.Outcome);
			result=RunTerminal(GpV2TransType.Sale,0m,CancellationToken.None);
			Assert.AreEqual(GpV2Outcome.Failed,result.Outcome);
			Assert.AreEqual(0,_gpFakeHttp.Requests.Count);
		}

		[TestMethod]
		public void SavedCard_ApprovedAndOffline() {
			CreditCard creditCard=new CreditCard { XChargeToken="PMT_saved",CCNumberMasked="XXXXXXXXXXXX1111",CCSource=CreditCardSource.MeetInTheCloudTerminal };
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_7","CAPTURED","1500","00","PMT_saved"));
			GlobalPaymentsV2Result result=VendorHttp.RunSync(() => _gpV2Transactions.RunSavedCardAsync(MakeConfig(),creditCard,GpV2TransType.Sale,15m,
				"SL-00000007",false,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual("",result.PaymentMethodId,"The saved card's own token is never handed back to be saved again.");
			_gpFakeHttp.Enqueue(HttpStatusCode.GatewayTimeout,"");
			result=VendorHttp.RunSync(() => _gpV2Transactions.RunSavedCardAsync(MakeConfig(),creditCard,GpV2TransType.Sale,15m,"SL-00000008",false,
				CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Unknown,result.Outcome);
			Assert.AreEqual("1111",result.CardLast4,"The saved card's last four digits when Global Payments sent none.");
		}

		[TestMethod]
		public void Void_ReversesTheTransaction() {
			_gpFakeHttp.Enqueue(HttpStatusCode.OK,GpSamples.Transaction("TRN_8","REVERSED","4000"));
			GlobalPaymentsV2Result result=VendorHttp.RunSync(() => _gpV2Transactions.RunVoidAsync(MakeConfig(),"TRN_8",40m,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Approved,result.Outcome);
			Assert.AreEqual(40m,result.Amount);
			_gpFakeHttp.Enqueue(HttpStatusCode.BadRequest,GpSamples.Error("INVALID_TRANSACTION_ACTION","Transaction already settled"));
			result=VendorHttp.RunSync(() => _gpV2Transactions.RunVoidAsync(MakeConfig(),"TRN_9",40m,CancellationToken.None));
			Assert.AreEqual(GpV2Outcome.Failed,result.Outcome);
			StringAssert.Contains(result.Message,"already settled");
			Assert.AreEqual("TRN_9",result.TransactionId);
		}
	}
}
