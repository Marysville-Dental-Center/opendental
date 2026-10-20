// Tests for the PaySimple ACH status check that replaces Open Dental HQ's webhook relay: the status mapping, the note text, and the poller against
// an in-memory payment store and a fake HttpMessageHandler. No database and no network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.PaySimple {
	[TestClass]
	public class PaySimpleAchStatusSyncTests {
		private static readonly DateTime _today=new DateTime(2026,10,19);
		private FakeHttpHandler _fakeHttpHandler;
		private FakeAchStore _fakeAchStore;
		private List<TimeSpan> _listPauses;
		private Dictionary<long,OpenDentBusiness.PaySimple.PaySimpleCredentials> _dictCredentials;

		[TestInitialize]
		public void TestInitialize() {
			_fakeHttpHandler=new FakeHttpHandler();
			OpenDentBusiness.PaySimple.SetTestHttpHandler(_fakeHttpHandler,TimeSpan.FromMilliseconds(1));
			_fakeAchStore=new FakeAchStore();
			_listPauses=new List<TimeSpan>();
			_dictCredentials=new Dictionary<long,OpenDentBusiness.PaySimple.PaySimpleCredentials>();
			_dictCredentials[0]=new OpenDentBusiness.PaySimple.PaySimpleCredentials("APIUserTest01","fake-key-a","Production");
		}

		[TestCleanup]
		public void TestCleanup() {
			OpenDentBusiness.PaySimple.SetTestHttpHandler(null);
		}

		private PaySimpleAchStatusSync CreateSync(bool isEnabled=true,bool isCheckOn=true) {
			return new PaySimpleAchStatusSync(_fakeAchStore,
				x => _dictCredentials.TryGetValue(x,out OpenDentBusiness.PaySimple.PaySimpleCredentials credentials) ? credentials
					: new OpenDentBusiness.PaySimple.PaySimpleCredentials(),
				() => isEnabled,() => isCheckOn,() => _today,x => _listPauses.Add(x),x => { });
		}

		private Payment AddPayment(long payNum,PaymentStatus paymentStatus,string externalId,long clinicNum=0,int daysAgo=3) {
			Payment payment=new Payment();
			payment.PayNum=payNum;
			payment.PatNum=1000+payNum;
			payment.ClinicNum=clinicNum;
			payment.PayAmt=125.50;
			payment.PayDate=_today.AddDays(-daysAgo);
			payment.PayNote="Original note";
			payment.ExternalId=externalId;
			payment.PaymentStatus=paymentStatus;
			_fakeAchStore.DictPayments[payNum]=payment;
			return payment;
		}

		private void EnqueuePayment(long id,string status,string failureCode=null,string failureDescription=null,string actualSettledDate=null,
			string returnDate=null)
		{
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,PaySimpleClientTests.Envelope(PaySimpleClientTests.PaymentJson(id,status,"",failureCode,
				failureDescription,actualSettledDate,returnDate)));
		}

		[TestMethod]
		public void MapStatus_EveryDocumentedStatus() {
			Assert.AreEqual(PaySimpleAchOutcome.Unchanged,PaySimpleAchStatusSync.MapStatus("Pending"));
			Assert.AreEqual(PaySimpleAchOutcome.Unchanged,PaySimpleAchStatusSync.MapStatus("Posted"));
			Assert.AreEqual(PaySimpleAchOutcome.Settled,PaySimpleAchStatusSync.MapStatus("Settled"));
			Assert.AreEqual(PaySimpleAchOutcome.Failed,PaySimpleAchStatusSync.MapStatus("Returned"));
			Assert.AreEqual(PaySimpleAchOutcome.Failed,PaySimpleAchStatusSync.MapStatus("ReverseNSF"));
			Assert.AreEqual(PaySimpleAchOutcome.Failed,PaySimpleAchStatusSync.MapStatus("ChargeBack"));
			Assert.AreEqual(PaySimpleAchOutcome.Failed,PaySimpleAchStatusSync.MapStatus("Failed"));
			Assert.AreEqual(PaySimpleAchOutcome.Voided,PaySimpleAchStatusSync.MapStatus("Voided"));
			Assert.AreEqual(PaySimpleAchOutcome.Refunded,PaySimpleAchStatusSync.MapStatus("Reversed"));
			Assert.AreEqual(PaySimpleAchOutcome.Refunded,PaySimpleAchStatusSync.MapStatus("ReversePosted"));
			Assert.AreEqual(PaySimpleAchOutcome.Refunded,PaySimpleAchStatusSync.MapStatus("RefundSettled"));
			Assert.AreEqual(PaySimpleAchOutcome.Unrecognized,PaySimpleAchStatusSync.MapStatus("Authorized"));
			Assert.AreEqual(PaySimpleAchOutcome.Unrecognized,PaySimpleAchStatusSync.MapStatus("RequiresAction"));
			Assert.AreEqual(PaySimpleAchOutcome.Unrecognized,PaySimpleAchStatusSync.MapStatus("SomethingNew"));
			Assert.AreEqual(PaySimpleAchOutcome.Unrecognized,PaySimpleAchStatusSync.MapStatus(""));
			Assert.AreEqual(PaySimpleAchOutcome.Unrecognized,PaySimpleAchStatusSync.MapStatus(null));
			Assert.AreEqual(PaySimpleAchOutcome.Settled,PaySimpleAchStatusSync.MapStatus(" settled "),"Case and spaces don't matter.");
		}

		[TestMethod]
		public void GetNewPaymentStatus_FollowsTheSpecTable() {
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,PaySimpleAchStatusSync.GetNewPaymentStatus(PaySimpleAchOutcome.Settled));
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,PaySimpleAchStatusSync.GetNewPaymentStatus(PaySimpleAchOutcome.Refunded));
			Assert.AreEqual(PaymentStatus.PaySimpleAchFailed,PaySimpleAchStatusSync.GetNewPaymentStatus(PaySimpleAchOutcome.Failed));
			Assert.AreEqual(PaymentStatus.PaySimpleAchFailed,PaySimpleAchStatusSync.GetNewPaymentStatus(PaySimpleAchOutcome.Voided));
			Assert.IsNull(PaySimpleAchStatusSync.GetNewPaymentStatus(PaySimpleAchOutcome.Unchanged));
			Assert.IsNull(PaySimpleAchStatusSync.GetNewPaymentStatus(PaySimpleAchOutcome.Unrecognized));
		}

		[TestMethod]
		public void Run_PostedBecomesSettled_NoteAndLog_AmountUnchanged_Idempotent() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501");
			EnqueuePayment(501,"Settled",actualSettledDate:"2026-10-17T06:00:00Z");
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			Payment payment=_fakeAchStore.DictPayments[1];
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,payment.PaymentStatus);
			Assert.AreEqual("Original note\r\nPaySimple ACH settled 2026-10-17",payment.PayNote);
			Assert.AreEqual(125.50,payment.PayAmt);
			Assert.AreEqual(1,result.CountChecked);
			Assert.AreEqual(1,result.CountSettled);
			Assert.AreEqual(0,result.CountFailed);
			Assert.AreEqual(1,_fakeAchStore.ListSecurityLogs.Count);
			Assert.AreEqual(0,_fakeAchStore.ListAlerts.Count);
			Assert.AreEqual("/v4/payment/501",_fakeHttpHandler.Requests[0].RequestUri.AbsolutePath);
			//A settled payment is only swept for late returns: run again with an empty return list, and nothing changes.
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,PaySimpleClientTests.Envelope("[]","{\"TotalItems\":0,\"Page\":1,\"ItemsPerPage\":200}"));
			PaySimpleAchSyncResult result2=CreateSync().Execute(isManual:false);
			Assert.AreEqual(0,result2.CountSettled+result2.CountFailed);
			Assert.AreEqual("Original note\r\nPaySimple ACH settled 2026-10-17",_fakeAchStore.DictPayments[1].PayNote);
			Assert.AreEqual(1,_fakeAchStore.CountUpdates);
		}

		[TestMethod]
		public void Run_PostedStaysPosted_NothingWritten() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501");
			EnqueuePayment(501,"Posted");
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			Assert.AreEqual(PaymentStatus.PaySimpleAchPosted,_fakeAchStore.DictPayments[1].PaymentStatus);
			Assert.AreEqual(1,result.CountChecked);
			Assert.AreEqual(0,_fakeAchStore.CountUpdates);
		}

		[TestMethod]
		public void Run_ReturnedBecomesFailed_WithReasonNoteAndAlert() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501",clinicNum:0);
			EnqueuePayment(501,"Returned","R01","Insufficient Funds",returnDate:"2026-10-18T06:00:00Z");
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			Payment payment=_fakeAchStore.DictPayments[1];
			Assert.AreEqual(PaymentStatus.PaySimpleAchFailed,payment.PaymentStatus);
			Assert.AreEqual("Original note\r\nPaySimple ACH Returned: Insufficient Funds (R01) 2026-10-18",payment.PayNote);
			Assert.AreEqual(125.50,payment.PayAmt);
			Assert.AreEqual(1,result.CountFailed);
			Assert.AreEqual(1,_fakeAchStore.ListAlerts.Count);
			Assert.AreEqual(0,_fakeAchStore.ListAlerts[0].ClinicNum);
		}

		[TestMethod]
		public void Run_VoidedAndRefunded() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501");
			AddPayment(2,PaymentStatus.PaySimpleAchPosted,"502");
			EnqueuePayment(501,"Voided");
			EnqueuePayment(502,"Reversed");
			CreateSync().Execute(isManual:false);
			Assert.AreEqual(PaymentStatus.PaySimpleAchFailed,_fakeAchStore.DictPayments[1].PaymentStatus);
			StringAssert.EndsWith(_fakeAchStore.DictPayments[1].PayNote,"PaySimple ACH voided at PaySimple 2026-10-19");
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,_fakeAchStore.DictPayments[2].PaymentStatus);
			StringAssert.EndsWith(_fakeAchStore.DictPayments[2].PayNote,"PaySimple ACH refunded at PaySimple 2026-10-19");
			Assert.AreEqual(1,_listPauses.Count,"One pause between the two lookups.");
			Assert.AreEqual(TimeSpan.FromSeconds(1),_listPauses[0]);
		}

		[TestMethod]
		public void Run_UnrecognizedStatusLeavesThePaymentAlone() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501");
			EnqueuePayment(501,"Authorized");
			CreateSync().Execute(isManual:false);
			Assert.AreEqual(PaymentStatus.PaySimpleAchPosted,_fakeAchStore.DictPayments[1].PaymentStatus);
			Assert.AreEqual(0,_fakeAchStore.CountUpdates);
		}

		[TestMethod]
		public void Run_SettledPaymentReturnedLate_IsFoundByTheSweep() {
			AddPayment(1,PaymentStatus.PaySimpleAchSettled,"601",daysAgo:40);
			AddPayment(2,PaymentStatus.PaySimpleAchSettled,"602",daysAgo:40);
			//The list returns one of ours and one that isn't (another product's payment).
			_fakeHttpHandler.Enqueue(HttpStatusCode.OK,PaySimpleClientTests.Envelope("["+PaySimpleClientTests.PaymentJson(999,"Returned")+","
				+PaySimpleClientTests.PaymentJson(602,"ReverseNSF")+"]","{\"TotalItems\":2,\"Page\":1,\"ItemsPerPage\":200}"));
			EnqueuePayment(602,"ReverseNSF","R01","Insufficient Funds",returnDate:"2026-10-15T06:00:00Z");
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual("/v4/payment",listRequests[0].RequestUri.AbsolutePath);
			StringAssert.Contains(Uri.UnescapeDataString(listRequests[0].RequestUri.Query),"startdate=2026-08-05");
			Assert.AreEqual("/v4/payment/602",listRequests[1].RequestUri.AbsolutePath);
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,_fakeAchStore.DictPayments[1].PaymentStatus);
			Assert.AreEqual(PaymentStatus.PaySimpleAchFailed,_fakeAchStore.DictPayments[2].PaymentStatus);
			StringAssert.EndsWith(_fakeAchStore.DictPayments[2].PayNote,"PaySimple ACH ReverseNSF: Insufficient Funds (R01) 2026-10-15");
			Assert.AreEqual(1,result.CountFailed);
			Assert.AreEqual(125.50,_fakeAchStore.DictPayments[2].PayAmt);
		}

		[TestMethod]
		public void Run_ChangedByAUserMeanwhile_IsLeftAlone() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501");
			EnqueuePayment(501,"Settled");
			_fakeAchStore.ActionBeforeReread=payNum => _fakeAchStore.DictPayments[payNum].PaymentStatus=PaymentStatus.None;
			CreateSync().Execute(isManual:false);
			Assert.AreEqual(PaymentStatus.None,_fakeAchStore.DictPayments[1].PaymentStatus);
			Assert.AreEqual(0,_fakeAchStore.CountUpdates);
		}

		[TestMethod]
		public void Run_GroupsClinicsByCredentials_AndSkipsClinicsWithoutThem() {
			_dictCredentials[1]=_dictCredentials[0];
			_dictCredentials[2]=new OpenDentBusiness.PaySimple.PaySimpleCredentials("APIUserTest02","fake-key-b","Sandbox");
			AddPayment(1,PaymentStatus.PaySimpleAchSettled,"701",clinicNum:0);
			AddPayment(2,PaymentStatus.PaySimpleAchSettled,"702",clinicNum:1);
			AddPayment(3,PaymentStatus.PaySimpleAchSettled,"703",clinicNum:2);
			AddPayment(4,PaymentStatus.PaySimpleAchPosted,"704",clinicNum:3);//No credentials for clinic 3.
			_fakeHttpHandler.SetDefault((request,ct) => System.Threading.Tasks.Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,
				PaySimpleClientTests.Envelope("[]","{\"TotalItems\":0,\"Page\":1,\"ItemsPerPage\":200}"))));
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			List<RecordedRequest> listRequests=_fakeHttpHandler.Requests;
			Assert.AreEqual(2,listRequests.Count,"One sweep per merchant account.");
			Assert.AreEqual(1,listRequests.Count(x => x.RequestUri.Host=="api.paysimple.com"));
			Assert.AreEqual(1,listRequests.Count(x => x.RequestUri.Host=="sandbox-api.paysimple.com"));
			Assert.AreEqual(1,result.CountSkippedNoCredentials);
		}

		[TestMethod]
		public void Run_CredentialsRejected_OneAlertPerClinicPerDay() {
			_dictCredentials[5]=new OpenDentBusiness.PaySimple.PaySimpleCredentials("APIUserTest05","fake-key-rejected","Production");
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"801",clinicNum:5);
			AddPayment(2,PaymentStatus.PaySimpleAchPosted,"802",clinicNum:5);
			_fakeHttpHandler.SetDefault((request,ct) => System.Threading.Tasks.Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.Unauthorized,"")));
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			Assert.AreEqual(1,_fakeHttpHandler.Requests.Count,"The rest of that account's payments are skipped.");
			Assert.AreEqual(1,_fakeAchStore.ListAlerts.Count);
			Assert.AreEqual(5,_fakeAchStore.ListAlerts[0].ClinicNum);
			Assert.AreEqual(1,result.ListErrors.Count);
			CreateSync().Execute(isManual:false);
			Assert.AreEqual(1,_fakeAchStore.ListAlerts.Count,"No second alert the same day.");
			Assert.AreEqual(PaymentStatus.PaySimpleAchPosted,_fakeAchStore.DictPayments[1].PaymentStatus);
		}

		[TestMethod]
		public void Run_NetworkFailure_EndsTheRunAndChangesNothing() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"901");
			AddPayment(2,PaymentStatus.PaySimpleAchPosted,"902");
			_fakeHttpHandler.SetDefault((request,ct) => throw new HttpRequestException("Simulated network failure."));
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			Assert.AreEqual(4,_fakeHttpHandler.Requests.Count,"The first lookup and its three retries; then the run stops.");
			Assert.AreEqual(0,_fakeAchStore.CountUpdates);
			Assert.AreEqual(1,result.ListErrors.Count);
			Assert.AreEqual(PaymentStatus.PaySimpleAchPosted,_fakeAchStore.DictPayments[2].PaymentStatus);
		}

		[TestMethod]
		public void Run_UnknownPaymentId_IsSkippedAndTheRunContinues() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"901");
			AddPayment(2,PaymentStatus.PaySimpleAchPosted,"902");
			_fakeHttpHandler.Enqueue(HttpStatusCode.BadRequest,"{\"Meta\":{\"Errors\":{\"ErrorCode\":\"NotFound\",\"ErrorMessages\":[{\"Field\":\"Id\","
				+"\"Message\":\"Payment 901 not found\"}]},\"HttpStatus\":\"BadRequest\",\"HttpStatusCode\":400,\"PagingDetails\":null},\"Response\":null}");
			EnqueuePayment(902,"Settled");
			PaySimpleAchSyncResult result=CreateSync().Execute(isManual:false);
			Assert.AreEqual(PaymentStatus.PaySimpleAchPosted,_fakeAchStore.DictPayments[1].PaymentStatus);
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,_fakeAchStore.DictPayments[2].PaymentStatus);
			Assert.AreEqual(1,result.ListErrors.Count);
		}

		[TestMethod]
		public void Run_Gates_DisabledOrTurnedOff() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501");
			Assert.IsTrue(CreateSync(isEnabled:false).Execute(isManual:true).WasSkipped);
			Assert.IsTrue(CreateSync(isCheckOn:false).Execute(isManual:false).WasSkipped);
			Assert.AreEqual(0,_fakeHttpHandler.Requests.Count);
			//A manual run works even with the hourly check off.
			EnqueuePayment(501,"Settled");
			Assert.IsFalse(CreateSync(isCheckOn:false).Execute(isManual:true).WasSkipped);
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,_fakeAchStore.DictPayments[1].PaymentStatus);
		}

		[TestMethod]
		public void CheckPayment_OneSettledPaymentOlderThanTheWindow() {
			AddPayment(1,PaymentStatus.PaySimpleAchPosted,"501",daysAgo:200);
			EnqueuePayment(501,"Settled");
			PaySimpleAchSyncResult result=CreateSync().CheckPayment(1);
			Assert.AreEqual(PaymentStatus.PaySimpleAchSettled,_fakeAchStore.DictPayments[1].PaymentStatus);
			Assert.AreEqual(1,result.CountSettled);
			//Not a PaySimple ACH payment.
			AddPayment(2,PaymentStatus.None,"");
			Assert.IsTrue(CreateSync().CheckPayment(2).WasSkipped);
		}

		[TestMethod]
		public void Job_RunsHourlyForPaySimple() {
			PaySimpleAchStatusJob paySimpleAchStatusJob=new PaySimpleAchStatusJob();
			Assert.AreEqual("PaySimpleAchStatus",paySimpleAchStatusJob.Name);
			Assert.AreEqual(ProgramName.PaySimple,paySimpleAchStatusJob.ProgramName);
			Assert.AreEqual(TimeSpan.FromHours(1),paySimpleAchStatusJob.Interval);
			Assert.IsTrue(VendorJobScheduler.DiscoverJobs().Any(x => x is PaySimpleAchStatusJob),"The job host finds the job.");
		}

		[TestMethod]
		public void AppendNoteLine_KeepsTheExistingNote() {
			Assert.AreEqual("line",PaySimpleAchStatusSync.AppendNoteLine("","line"));
			Assert.AreEqual("line",PaySimpleAchStatusSync.AppendNoteLine(null,"line"));
			Assert.AreEqual("a\r\nline",PaySimpleAchStatusSync.AppendNoteLine("a\r\n","line"));
			Assert.AreEqual("a",PaySimpleAchStatusSync.AppendNoteLine("a",""));
		}

		///<summary>Payments in memory. Records every write.</summary>
		private class FakeAchStore:IPaySimpleAchStore {
			public Dictionary<long,Payment> DictPayments=new Dictionary<long,Payment>();
			public List<string> ListSecurityLogs=new List<string>();
			public List<(long ClinicNum,string Description,string ItemValue)> ListAlerts=new List<(long,string,string)>();
			public int CountUpdates;
			public Action<long> ActionBeforeReread;

			public List<Payment> GetPaymentsToTrack(DateTime dateFrom) {
				return DictPayments.Values
					.Where(x => x.ExternalId!="" && (x.PaymentStatus==PaymentStatus.PaySimpleAchPosted
						|| (x.PaymentStatus==PaymentStatus.PaySimpleAchSettled && x.PayDate>=dateFrom)))
					.Select(x => x.Clone())
					.ToList();
			}

			public Payment GetPayment(long payNum) {
				ActionBeforeReread?.Invoke(payNum);
				return DictPayments.TryGetValue(payNum,out Payment payment) ? payment.Clone() : null;
			}

			public void UpdatePayment(Payment paymentNew,Payment paymentOld) {
				Assert.AreEqual(paymentOld.PayAmt,paymentNew.PayAmt,"The amount is never changed.");
				Assert.AreEqual(paymentOld.DepositNum,paymentNew.DepositNum,"The deposit is never changed.");
				DictPayments[paymentNew.PayNum]=paymentNew.Clone();
				CountUpdates++;
			}

			public void MakeSecurityLog(Payment payment,string logText) {
				ListSecurityLogs.Add(logText);
			}

			public void AddAlert(long clinicNum,string description,string itemValue) {
				ListAlerts.Add((clinicNum,description,itemValue));
			}
		}
	}
}
