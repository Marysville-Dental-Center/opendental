// Tests for the CareCredit facade (the surface this program used from the prebuilt Bridges.dll): no network while disabled or unconfigured,
// prefill row creation for each landing page, failure handling, merchant numbers, session IDs and the pullback helpers. Uses a fake HTTP
// handler and an in-memory response store; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Net;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;

namespace VendorDirectTests.CareCreditTests {
	[TestClass]
	public class CareCreditFacadeTests {
		private const string TOKEN_JSON="{\"access_token\":\"tok-1\",\"token_type\":\"bearer\",\"expires_in\":3600}";
		private static readonly DateTime _dateTimeNow=new DateTime(2026,10,19,9,30,0);
		private FakeHttpHandler _handler;
		private FakeResponseStore _store;
		private CareCreditConfig _config;
		private bool _isEnabled;
		private string _lastAuthError;

		[TestInitialize]
		public void TestInitialize() {
			TokenCache.ClearAll();
			_handler=new FakeHttpHandler();
			_store=new FakeResponseStore();
			_config=CareCreditTestData.CreateConfig();
			_isEnabled=true;
			_lastAuthError=null;
			CareCredit.ApiClient=new CareCreditApiClient(_handler);
			CareCredit.ResponseStore=_store;
			CareCredit.FuncGetConfig=() => _config;
			CareCredit.FuncIsEnabled=() => _isEnabled;
			CareCredit.FuncNow=() => _dateTimeNow;
			CareCredit.ActionSetLastAuthError=x => _lastAuthError=x;
		}

		[TestCleanup]
		public void TestCleanup() {
			CareCredit.ResetDependencies();
		}

		private string SendPrefill(CareCreditLandingPage landingPage,Patient patient,double purchase=0,double refund=0,long payNum=0) {
			return CareCredit.SendPrefillReturnPageUrl(landingPage,patient,"inv",purchase,refund,"memo","","staff1",0,"",CareCreditTestData.MERCHANT,
				EnumCareCreditPrefillServiceType.C,payNum,clinicNum:3);
		}

		[TestMethod]
		public void Disabled_MakesNoCallAndNoRow() {
			_isEnabled=false;
			ODException ex=AssertEx.Throws<ODException>(() => SendPrefill(CareCreditLandingPage.PR,CareCreditTestData.CreateAdult(1),purchase:10));
			StringAssert.Contains(ex.Message,"not configured");
			Assert.AreEqual(0,_handler.Requests.Count);
			Assert.AreEqual(0,_store.ListRows.Count);
			Assert.IsFalse(CareCredit.IsUsable());
		}

		[TestMethod]
		public void Incomplete_MakesNoCall() {
			_config.PartnerCode="1065";
			AssertEx.Throws<ODException>(() => SendPrefill(CareCreditLandingPage.AP,CareCreditTestData.CreateAdult(1)));
			_config=CareCreditTestData.CreateConfig();
			_config.ClientSecret="";
			AssertEx.Throws<ODException>(() => SendPrefill(CareCreditLandingPage.AP,CareCreditTestData.CreateAdult(1)));
			Assert.AreEqual(0,_handler.Requests.Count);
			Assert.AreEqual(0,_store.ListRows.Count);
		}

		[TestMethod]
		public void Prefill_PurchaseCreatesPendingRowWithReferenceAndSession() {
			_handler.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"referenceId\":\"REF-1\",\"url\":\"https://pages.carecredit-sandbox.test/p?sid=S-1\"}");
			string url=SendPrefill(CareCreditLandingPage.PR,CareCreditTestData.CreateAdult(12),purchase:99.5,payNum:44);
			Assert.AreEqual("https://pages.carecredit-sandbox.test/p?sid=S-1",url);
			CareCreditWebResponse row=_store.ListRows.Single();
			Assert.AreEqual(CareCreditWebStatus.Pending,row.ProcessingStatus);
			Assert.AreEqual(_dateTimeNow,row.DateTimePending);
			Assert.AreEqual("REF-1",row.RefNumber);
			Assert.AreEqual("S-1",row.WebToken);
			Assert.AreEqual(CareCreditServiceType.Prefill,row.ServiceType);
			Assert.AreEqual(CareCreditTransType.Purchase,row.TransType);
			Assert.AreEqual(44,row.PayNum);
			Assert.AreEqual(12,row.PatNum);
			Assert.AreEqual(3,row.ClinicNum);
			Assert.AreEqual(99.5,row.Amount,0.001);
			Assert.AreEqual(CareCreditTestData.MERCHANT,row.MerchantNumber);
			Assert.AreEqual("S-1",CareCredit.GetSessionIDFromUrl(url));
			Assert.AreEqual("staff1",CareCredit.GetUserName(row),"The staff user is kept with the row.");
			Assert.AreEqual("",_lastAuthError,"A good call clears the last authorization error.");
		}

		[TestMethod]
		public void Prefill_LandingPagesSetServiceAndTransactionType() {
			foreach(Tuple<CareCreditLandingPage,CareCreditServiceType,CareCreditTransType> tuple in new[] {
				Tuple.Create(CareCreditLandingPage.QS,CareCreditServiceType.IndividualQS,CareCreditTransType.None),
				Tuple.Create(CareCreditLandingPage.RF,CareCreditServiceType.Prefill,CareCreditTransType.Refund),
				Tuple.Create(CareCreditLandingPage.AP,CareCreditServiceType.Prefill,CareCreditTransType.None),
				Tuple.Create(CareCreditLandingPage.LU,CareCreditServiceType.Prefill,CareCreditTransType.None),
				Tuple.Create(CareCreditLandingPage.HM,CareCreditServiceType.Prefill,CareCreditTransType.None) })
			{
				_handler.SetDefault((request,ct) => System.Threading.Tasks.Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("/token")
					? FakeHttpHandler.MakeResponse(HttpStatusCode.OK,TOKEN_JSON)
					: FakeHttpHandler.MakeResponse(HttpStatusCode.OK,"{\"referenceId\":\"R\",\"url\":\"https://pages.carecredit-sandbox.test/x?sid=1\"}")));
				Patient patient=(tuple.Item1==CareCreditLandingPage.HM) ? null : CareCreditTestData.CreateAdult(2);
				SendPrefill(tuple.Item1,patient,refund:(tuple.Item1==CareCreditLandingPage.RF) ? 20 : 0);
				CareCreditWebResponse row=_store.ListRows.Last();
				Assert.AreEqual(tuple.Item2,row.ServiceType,tuple.Item1.ToString());
				Assert.AreEqual(tuple.Item3,row.TransType,tuple.Item1.ToString());
			}
			string bodyApplication=_handler.Requests.Where(x => !x.RequestUri.AbsolutePath.EndsWith("/token")).ElementAt(2).Body;
			StringAssert.Contains(bodyApplication,"dateOfBirth","An application carries the date of birth.");
		}

		[TestMethod]
		public void Prefill_PageOffTheHostedHostIsRefused() {
			_handler.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"referenceId\":\"REF-1\",\"url\":\"https://phish.test/p?sid=S-1\"}");
			AssertEx.Throws<ODException>(() => SendPrefill(CareCreditLandingPage.PR,CareCreditTestData.CreateAdult(1),purchase:5));
			CareCreditWebResponse row=_store.ListRows.Single();
			Assert.AreEqual(CareCreditWebStatus.CreatedError,row.ProcessingStatus);
			Assert.AreEqual("",row.RefNumber);
		}

		[TestMethod]
		public void Prefill_HttpErrorMarksRowAndRethrows() {
			_handler.Enqueue(HttpStatusCode.OK,TOKEN_JSON).Enqueue(HttpStatusCode.BadRequest,"{\"errorCode\":\"INVALID_ZIP\"}");
			CareCredit.CareCreditException ex=AssertEx.Throws<CareCredit.CareCreditException>(
				() => SendPrefill(CareCreditLandingPage.AP,CareCreditTestData.CreateAdult(1)));
			Assert.AreEqual(400,ex.ErrorCode);
			CareCreditWebResponse row=_store.ListRows.Single();
			Assert.AreEqual(CareCreditWebStatus.CreatedError,row.ProcessingStatus);
			Assert.AreEqual(_dateTimeNow,row.DateTimeLastError);
			StringAssert.Contains(row.LastResponseStr,"INVALID_ZIP");
		}

		[TestMethod]
		public void Prefill_RefusedCredentialsAreRecordedForSetup() {
			_handler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"invalid_client\"}");
			CareCredit.CareCreditException ex=AssertEx.Throws<CareCredit.CareCreditException>(
				() => SendPrefill(CareCreditLandingPage.AP,CareCreditTestData.CreateAdult(1)));
			Assert.IsTrue(ex.IsAuthFailure);
			StringAssert.Contains(_lastAuthError,"rejected");
		}

		[TestMethod]
		public void Prefill_ClosedMerchantIsRefusedWithoutACall() {
			AssertEx.Throws<ODException>(() => CareCredit.SendPrefillReturnPageUrl(CareCreditLandingPage.PR,CareCreditTestData.CreateAdult(1),"",5,0,"","",
				"",0,"","x"+CareCreditTestData.MERCHANT,EnumCareCreditPrefillServiceType.C,0,0));
			Assert.AreEqual(0,_handler.Requests.Count);
		}

		[TestMethod]
		public void UpdateWebResponse_AppliesPullback() {
			_handler.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"status\":\"COMPLETED\",\"amount\":\"99.50\",\"merchantFee\":\"6.20\",\"promoCode\":\"P12\",\"ssn\":\"123456789\"}");
			CareCreditWebResponse row=new CareCreditWebResponse();
			row.CareCreditWebResponseNum=_store.Insert(row);
			row.RefNumber="REF-1";
			row.ProcessingStatus=CareCreditWebStatus.Pending;
			row.TransType=CareCreditTransType.Purchase;
			row.ServiceType=CareCreditServiceType.Prefill;
			CareCredit.UpdateWebResponse(row);
			Assert.AreEqual(CareCreditWebStatus.Completed,row.ProcessingStatus);
			Assert.AreEqual(_dateTimeNow,row.DateTimeCompleted);
			Assert.IsFalse(row.LastResponseStr.Contains("123456789"));
			Assert.AreEqual(CareCreditWebStatus.Completed,_store.Get(row.CareCreditWebResponseNum).ProcessingStatus);
			Assert.AreEqual(6.20,CareCredit.GetMerchantFee(row),0.001);
			string note=CareCredit.GetFormattedNote(row);
			StringAssert.Contains(note,"Purchase");
			StringAssert.Contains(note,"REF-1");
			StringAssert.Contains(note,"P12");
		}

		[TestMethod]
		public void UpdateWebResponse_PendingKeepsStatusAndRowsWithoutReferenceAreSkipped() {
			_handler.Enqueue(HttpStatusCode.OK,TOKEN_JSON).Enqueue(HttpStatusCode.OK,"{\"status\":\"PENDING\"}");
			CareCreditWebResponse row=new CareCreditWebResponse();
			row.CareCreditWebResponseNum=_store.Insert(row);
			row.RefNumber="REF-2";
			row.ProcessingStatus=CareCreditWebStatus.Pending;
			row.DateTimePending=new DateTime(2026,10,19,9,0,0);
			CareCredit.UpdateWebResponse(row);
			Assert.AreEqual(CareCreditWebStatus.Pending,row.ProcessingStatus);
			Assert.AreEqual(new DateTime(2026,10,19,9,0,0),row.DateTimePending);
			int countRequests=_handler.Requests.Count;
			CareCreditWebResponse rowNoRef=new CareCreditWebResponse();
			rowNoRef.RefNumber="";
			CareCredit.UpdateWebResponse(rowNoRef);
			Assert.AreEqual(countRequests,_handler.Requests.Count);
		}

		[TestMethod]
		public void TestConnection_ReportsSuccessAndFailure() {
			_handler.Enqueue(HttpStatusCode.OK,TOKEN_JSON);
			Assert.AreEqual("",CareCredit.TestConnection(_config));
			_handler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"invalid_client\"}");
			StringAssert.Contains(CareCredit.TestConnection(_config),"rejected");
			CareCreditConfig configIncomplete=CareCreditTestData.CreateConfig();
			configIncomplete.TokenPath="";
			int countRequests=_handler.Requests.Count;
			StringAssert.Contains(CareCredit.TestConnection(configIncomplete),"Token path");
			Assert.AreEqual(countRequests,_handler.Requests.Count,"Incomplete settings are refused without a call.");
		}

		[TestMethod]
		public void MerchantNumbers_ValidAndClosed() {
			Assert.IsTrue(CareCredit.IsMerchantNumValid(CareCreditTestData.MERCHANT,allowClosedMerchant:false));
			Assert.IsFalse(CareCredit.IsMerchantNumValid("x"+CareCreditTestData.MERCHANT,allowClosedMerchant:false));
			Assert.IsTrue(CareCredit.IsMerchantNumValid("x"+CareCreditTestData.MERCHANT,allowClosedMerchant:true));
			Assert.IsFalse(CareCredit.IsMerchantNumValid("123",allowClosedMerchant:true));
			Assert.IsFalse(CareCredit.IsMerchantNumValid("53481200000000AB",allowClosedMerchant:true));
			Assert.IsTrue(CareCredit.IsMerchantNumClosed("x"+CareCreditTestData.MERCHANT));
			Assert.IsFalse(CareCredit.IsMerchantNumClosed(CareCreditTestData.MERCHANT));
			Assert.IsFalse(CareCredit.IsMerchantNumClosed(""));
			Assert.IsTrue(CareCredit.IsMerchantNumClosed(CareCreditTestData.MERCHANT,new System.Collections.Generic.List<string> { "x"+CareCreditTestData.MERCHANT }));
			Assert.IsFalse(CareCredit.IsMerchantNumClosed(CareCreditTestData.MERCHANT,new System.Collections.Generic.List<string> { CareCreditTestData.MERCHANT }));
		}

		[TestMethod]
		public void SessionId_FromConfiguredOrCommonParameter() {
			Assert.AreEqual("abc",CareCredit.GetSessionIdFromUrl("https://pages.test/x?token=abc","token"));
			Assert.AreEqual("abc",CareCredit.GetSessionIdFromUrl("https://pages.test/x?a=1&sessionId=abc",""));
			Assert.AreEqual("a b",CareCredit.GetSessionIdFromUrl("https://pages.test/x#session_id=a%20b",""));
			Assert.AreEqual("",CareCredit.GetSessionIdFromUrl("https://pages.test/x?a=1",""));
			Assert.AreEqual("",CareCredit.GetSessionIdFromUrl("not a url",""));
		}

		[TestMethod]
		public void Patients_MinorsAndCalifornia() {
			Assert.IsTrue(CareCredit.IsPatAMinor(CareCreditTestData.CreateMinor(2,1)));
			Assert.IsFalse(CareCredit.IsPatAMinor(CareCreditTestData.CreateAdult(1)));
			Patient patientNoBirthdate=CareCreditTestData.CreateAdult(3);
			patientNoBirthdate.Birthdate=DateTime.MinValue;
			Assert.IsFalse(CareCredit.IsPatAMinor(patientNoBirthdate));
			Assert.IsTrue(CareCredit.IsCaliforniaState("CA"));
			Assert.IsTrue(CareCredit.IsCaliforniaState(" california "));
			Assert.IsFalse(CareCredit.IsCaliforniaState("OR"));
			Assert.IsFalse(CareCredit.IsCaliforniaState(""));
		}
	}
}
