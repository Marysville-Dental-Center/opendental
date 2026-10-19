// Tests for the ERX package: the DoseSpot REST v2 client against a fake HTTP handler. No network call is made and no real credentials are used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness;

namespace VendorDirectTests.Erx {
	[TestClass]
	public class DoseSpotApiClientTests {
		private const string BASE_URL="https://my.staging.dosespot.com/webapi/v2";
		private const string TOKEN_JSON="{\"access_token\":\"tok-1\",\"token_type\":\"bearer\",\"expires_in\":600}";
		private static readonly DateTime _dateTimeStart=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
		private DateTime _nowUtc;
		private ErxPollThrottle _erxPollThrottle;

		[TestInitialize]
		public void TestInitialize() {
			ErxTestData.UseEnUs();
			_nowUtc=_dateTimeStart;
			_erxPollThrottle=new ErxPollThrottle(() => _nowUtc);
		}

		private DoseSpotApiClient MakeClient(FakeHttpHandler fakeHttpHandler) {
			return new DoseSpotApiClient(fakeHttpHandler,() => _nowUtc,x => { },_erxPollThrottle);
		}

		private static DoseSpotCredentials MakeCredentials(string clinicKey="ck+/=1") {
			DoseSpotCredentials doseSpotCredentials=new DoseSpotCredentials();
			doseSpotCredentials.ApiBaseUrl=BASE_URL;
			doseSpotCredentials.SubscriptionKey="sub-key";
			doseSpotCredentials.ClinicId="123";
			doseSpotCredentials.ClinicKey=clinicKey;
			doseSpotCredentials.UserId="456";
			return doseSpotCredentials;
		}

		///<summary>Reads an application/x-www-form-urlencoded body.</summary>
		private static Dictionary<string,string> ParseForm(string body) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			foreach(string pair in (body??"").Split(new[] { '&' },StringSplitOptions.RemoveEmptyEntries)) {
				int index=pair.IndexOf('=');
				string name=(index<0) ? pair : pair.Substring(0,index);
				string value=(index<0) ? "" : pair.Substring(index+1);
				dict[Uri.UnescapeDataString(name.Replace('+',' '))]=Uri.UnescapeDataString(value.Replace('+',' '));
			}
			return dict;
		}

		private static string CountsJson(int refills,int errors,int pending) {
			return "{\"RefillRequestsCount\":"+refills+",\"TransactionErrorsCount\":"+errors+",\"PendingPrescriptionsCount\":"+pending
				+",\"PendingRxChangeCount\":0,\"Result\":{\"ResultCode\":\"OK\",\"ResultDescription\":\"\"}}";
		}

		[TestMethod]
		public void Token_IsAFormEncodedPasswordGrantWithTheSubscriptionKey() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,TOKEN_JSON).Enqueue(HttpStatusCode.OK,CountsJson(1,2,3));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotNotificationCounts counts=null;
			AssertEx.Run(async () => counts=await client.GetNotificationCountsAsync(MakeCredentials(),false));
			Assert.AreEqual(1,counts.RefillRequestsCount);
			Assert.AreEqual(2,counts.TransactionErrorsCount);
			Assert.AreEqual(3,counts.PendingPrescriptionsCount);
			RecordedRequest requestToken=fakeHttpHandler.Requests[0];
			Assert.AreEqual(HttpMethod.Post,requestToken.Method);
			Assert.AreEqual(BASE_URL+"/connect/token",requestToken.RequestUri.AbsoluteUri);
			Assert.AreEqual("application/x-www-form-urlencoded",requestToken.ContentType);
			Assert.AreEqual("sub-key",requestToken.GetHeader("Subscription-Key"));
			Assert.IsNull(requestToken.GetHeader("Authorization"),"The token request sends no Basic header.");
			Dictionary<string,string> form=ParseForm(requestToken.Body);
			Assert.AreEqual("password",form["grant_type"]);
			Assert.AreEqual("123",form["client_id"]);
			Assert.AreEqual("ck+/=1",form["client_secret"],"A key with + / = must survive form encoding.");
			Assert.AreEqual("456",form["username"]);
			Assert.AreEqual("ck+/=1",form["password"]);
			Assert.AreEqual("api",form["scope"]);
			StringAssert.Contains(requestToken.Body,"ck%2B%2F%3D1");
			RecordedRequest requestCounts=fakeHttpHandler.Requests[1];
			Assert.AreEqual(BASE_URL+"/api/notifications/counts",requestCounts.RequestUri.AbsoluteUri);
			Assert.AreEqual("Bearer tok-1",requestCounts.GetHeader("Authorization"));
			Assert.AreEqual("sub-key",requestCounts.GetHeader("Subscription-Key"));
		}

		[TestMethod]
		public void Token_IsCachedUntilExpiresInMinusAMinute() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,CountsJson(0,0,0))
				.Enqueue(HttpStatusCode.OK,CountsJson(0,0,0))
				.Enqueue(HttpStatusCode.OK,"{\"access_token\":\"tok-2\",\"expires_in\":600}")
				.Enqueue(HttpStatusCode.OK,CountsJson(0,0,0));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			AssertEx.Run(() => client.GetNotificationCountsAsync(MakeCredentials(),false));
			_nowUtc=_dateTimeStart.AddSeconds(530);//Inside 600-60.
			AssertEx.Run(() => client.GetNotificationCountsAsync(MakeCredentials(),false));
			_nowUtc=_dateTimeStart.AddSeconds(545);//Past 600-60: a new token.
			AssertEx.Run(() => client.GetNotificationCountsAsync(MakeCredentials(),false));
			List<RecordedRequest> listRequests=fakeHttpHandler.Requests;
			Assert.AreEqual(2,listRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith("/connect/token")));
			Assert.AreEqual("Bearer tok-2",listRequests.Last().GetHeader("Authorization"));
		}

		[TestMethod]
		public void Token_WithoutExpiresIn_LastsTenMinutes() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,"{\"access_token\":\"tok-1\"}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			OAuthAccessToken token=null;
			AssertEx.Run(async () => token=await client.GetAccessTokenAsync(MakeCredentials(),false));
			Assert.AreEqual(_dateTimeStart.Add(DoseSpotApiClient.DefaultTokenLifetime),token.ExpiresUtc);
		}

		[TestMethod]
		public void Call401_GetsExactlyOneNewTokenAndOneRetry() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.Unauthorized,"")
				.Enqueue(HttpStatusCode.OK,"{\"access_token\":\"tok-2\",\"expires_in\":600}")
				.Enqueue(HttpStatusCode.OK,CountsJson(4,0,0));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotNotificationCounts counts=null;
			AssertEx.Run(async () => counts=await client.GetNotificationCountsAsync(MakeCredentials(),false));
			Assert.AreEqual(4,counts.RefillRequestsCount);
			Assert.AreEqual(4,fakeHttpHandler.Requests.Count);
			Assert.AreEqual("Bearer tok-2",fakeHttpHandler.Requests[3].GetHeader("Authorization"));
		}

		[TestMethod]
		public void Call401Twice_IsRejectedWithoutAThirdTry() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.Unauthorized,"")
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.Unauthorized,"");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(MakeCredentials(),false)));
			Assert.AreEqual(DoseSpotErrorKind.Rejected,ex.Kind);
			Assert.AreEqual(4,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void TokenRejected_IsRejectedAndBackgroundCallsFailFastUntilInteractive() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.BadRequest,"{\"error\":\"invalid_client\"}")
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,CountsJson(0,0,0));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(MakeCredentials(),false)));
			Assert.AreEqual(DoseSpotErrorKind.Rejected,ex.Kind);
			Assert.IsFalse(ex.Message.Contains("ck+/=1"),"The message never holds the key.");
			//A background poll after the rejection makes no request.
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(MakeCredentials(),false)));
			Assert.AreEqual(DoseSpotErrorKind.Rejected,ex.Kind);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count);
			//The user clicking eRx tries again.
			AssertEx.Run(() => client.GetNotificationCountsAsync(MakeCredentials(),true));
			Assert.AreEqual(3,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void Call429_PausesPollsForRetryAfter() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.SetDefault((request,ct) => System.Threading.Tasks.Task.FromResult(FakeHttpHandler.MakeResponse((HttpStatusCode)429,"",
					"application/json",x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(90)))));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(MakeCredentials(),false)));
			Assert.AreEqual(DoseSpotErrorKind.RateLimited,ex.Kind);
			Assert.AreEqual(TimeSpan.FromSeconds(90),ex.RetryAfter);
			StringAssert.Contains(ex.Message,"90");
			Assert.IsTrue(_erxPollThrottle.IsPaused(DoseSpotApiClient.VendorName));
			Assert.IsFalse(_erxPollThrottle.TryBegin(DoseSpotApiClient.VendorName,"counts|1",ErxPollThrottle.IntervalNotificationCounts,isForced:true));
			_nowUtc=_dateTimeStart.AddSeconds(91);
			Assert.IsFalse(_erxPollThrottle.IsPaused(DoseSpotApiClient.VendorName));
			//A 90 second Retry-After is longer than VendorHttp waits, so the GET was not retried.
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void AddPatient_IsNeverRetriedAfterAServerError() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.SetDefault((request,ct) => System.Threading.Tasks.Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.ServiceUnavailable,"")));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.AddPatientAsync(MakeCredentials(),new { FirstName="A" },true)));
			Assert.AreEqual(DoseSpotErrorKind.VendorError,ex.Kind);
			Assert.AreEqual(503,ex.HttpStatus);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith("/api/patients")));
		}

		[TestMethod]
		public void AddPatient_PostsJsonAndReturnsTheId() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"Id\":98765,\"Result\":{\"ResultCode\":\"OK\",\"ResultDescription\":\"\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			string id=null;
			AssertEx.Run(async () => id=await client.AddPatientAsync(MakeCredentials(),new { FirstName="Ann",Gender=2 },true));
			Assert.AreEqual("98765",id);
			RecordedRequest request=fakeHttpHandler.Requests[1];
			Assert.AreEqual(HttpMethod.Post,request.Method);
			Assert.AreEqual("application/json",request.ContentType);
			Assert.AreEqual("Ann",JObject.Parse(request.Body).Value<string>("FirstName"));
		}

		[TestMethod]
		public void GetPrescriptions_RetriesAGetOnceAfterAServerError() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.BadGateway,"")
				.Enqueue(HttpStatusCode.OK,"{\"Items\":[],\"PageResult\":{\"CurrentPage\":1,\"HasNext\":false},\"Result\":{\"ResultCode\":\"OK\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			List<DoseSpotPrescription> listPrescriptions=null;
			AssertEx.Run(async () => listPrescriptions=await client.GetPrescriptionsAsync(MakeCredentials(),"55",false));
			Assert.AreEqual(0,listPrescriptions.Count);
			Assert.AreEqual(3,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void GetPrescriptions_ReadsEveryPage() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"Items\":[{\"PrescriptionId\":1,\"DisplayName\":\"Amoxicillin\",\"Status\":4,\"MedicationStatus\":1,\"RxCUI\":\"308182\"}],"
					+"\"PageResult\":{\"CurrentPage\":1,\"HasNext\":true},\"Result\":{\"ResultCode\":\"OK\"}}")
				.Enqueue(HttpStatusCode.OK,"{\"Items\":[{\"PrescriptionId\":2,\"DisplayName\":\"Ibuprofen\",\"Status\":1,\"MedicationStatus\":1,\"NewField\":{\"x\":1}}],"
					+"\"PageResult\":{\"CurrentPage\":2,\"HasNext\":false},\"Result\":{\"ResultCode\":\"OK\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			List<DoseSpotPrescription> listPrescriptions=null;
			AssertEx.Run(async () => listPrescriptions=await client.GetPrescriptionsAsync(MakeCredentials(),"55",false));
			Assert.AreEqual(2,listPrescriptions.Count);
			Assert.AreEqual(DoseSpotREST.PrescriptionStatus.eRxSent,listPrescriptions[0].Status);
			Assert.AreEqual("308182",listPrescriptions[0].RxCUI);
			Assert.AreEqual("Ibuprofen",listPrescriptions[1].DisplayName);
			Assert.AreEqual(BASE_URL+"/api/patients/55/prescriptions",fakeHttpHandler.Requests[1].RequestUri.AbsoluteUri);
			Assert.AreEqual(BASE_URL+"/api/patients/55/prescriptions?pageNumber=2",fakeHttpHandler.Requests[2].RequestUri.AbsoluteUri);
		}

		[TestMethod]
		public void GetSelfReported_MapsTheV2FieldNames() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"Items\":[{\"SelfReportedMedicationId\":7,\"DisplayName\":\"Aspirin\",\"Status\":3,"
					+"\"DiscontinuedDate\":\"2026-01-02T00:00:00\",\"DosageText\":\"daily\",\"Dispense\":\"30\"}],"
					+"\"PageResult\":{\"CurrentPage\":1,\"HasNext\":false},\"Result\":{\"ResultCode\":\"OK\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			List<DoseSpotSelfReported> listSelfReported=null;
			AssertEx.Run(async () => listSelfReported=await client.GetSelfReportedAsync(MakeCredentials(),"55",false));
			Assert.AreEqual(1,listSelfReported.Count);
			Assert.AreEqual(DoseSpotREST.MedicationStatus.Discontinued,listSelfReported[0].MedicationStatus);
			Assert.AreEqual(new DateTime(2026,1,2),listSelfReported[0].DateInactive);
			Assert.AreEqual("daily",listSelfReported[0].Directions);
			Assert.AreEqual("30",listSelfReported[0].Quantity);
		}

		[TestMethod]
		public void ResultError_CarriesDoseSpotsDescription() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"Result\":{\"ResultCode\":\"ERROR\",\"ResultDescription\":\"Patient is invalid.\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.EditPatientAsync(MakeCredentials(),"55",new { },true)));
			Assert.AreEqual(DoseSpotErrorKind.VendorError,ex.Kind);
			StringAssert.Contains(ex.Message,"Patient is invalid.");
		}

		[TestMethod]
		public void ResultNotAuthorized_IsTheNotAuthorizedKind() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"Result\":{\"ResultCode\":\"ERROR\",\"ResultDescription\":\"User is not authorized.\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(MakeCredentials(),false)));
			Assert.AreEqual(DoseSpotErrorKind.NotAuthorized,ex.Kind);
			Assert.AreEqual(CodeBase.ODException.ErrorCodes.DoseSpotNotAuthorized,ex.ErrorCodeAsEnum);
		}

		[TestMethod]
		public void NetworkFailure_IsUnreachable() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.EnqueueNetworkFailure()
				.EnqueueNetworkFailure();
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(MakeCredentials(),false)));
			Assert.AreEqual(DoseSpotErrorKind.Unreachable,ex.Kind);
		}

		[TestMethod]
		public void MissingValues_AreNotConfiguredAndSendNothing() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			DoseSpotCredentials doseSpotCredentials=MakeCredentials();
			doseSpotCredentials.SubscriptionKey="";
			DoseSpotException ex=null;
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(doseSpotCredentials,true)));
			Assert.AreEqual(DoseSpotErrorKind.NotConfigured,ex.Kind);
			StringAssert.Contains(ex.Message,"subscription key");
			doseSpotCredentials=MakeCredentials();
			doseSpotCredentials.UserId=" ";
			AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(doseSpotCredentials,true)));
			Assert.AreEqual(DoseSpotErrorKind.NotConfigured,ex.Kind);
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void OpenDentalHost_IsRefusedWithoutARequest() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().SetDefault((request,ct) =>
				System.Threading.Tasks.Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,TOKEN_JSON)));
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			foreach(string host in HqHosts.BlockedDomains) {
				DoseSpotCredentials doseSpotCredentials=MakeCredentials();
				doseSpotCredentials.ApiBaseUrl="https://erx."+host+"/webapi/v2";
				DoseSpotException ex=null;
				AssertEx.Run(async () => ex=await AssertEx.ThrowsAsync<DoseSpotException>(() => client.GetNotificationCountsAsync(doseSpotCredentials,true)));
				Assert.AreEqual(DoseSpotErrorKind.NotConfigured,ex.Kind,host);
			}
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}

		[TestMethod]
		public void PostClinic_ReturnsTheNewIdAndKey() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TOKEN_JSON)
				.Enqueue(HttpStatusCode.OK,"{\"ClinicId\":2001,\"ClinicKey\":\"NEWKEY+/\",\"Result\":{\"ResultCode\":\"OK\"}}");
			DoseSpotApiClient client=MakeClient(fakeHttpHandler);
			KeyValuePair<string,string> pairIdKey=default(KeyValuePair<string,string>);
			AssertEx.Run(async () => pairIdKey=await client.PostClinicAsync(MakeCredentials(),new { ClinicName="North" }));
			Assert.AreEqual("2001",pairIdKey.Key);
			Assert.AreEqual("NEWKEY+/",pairIdKey.Value);
		}

		[TestMethod]
		public void BuildPatientBody_SendsUnknownForOtherAndRequiresVitalsUnder18() {
			Patient patient=new Patient { PatNum=1,FName=" Ann ",LName="Lee",Birthdate=new DateTime(1980,5,6),Gender=PatientGender.Other,
				Address="1 Main St",City="Salem",State="OR",Zip="97301" };
			JObject jObject=JObject.FromObject(DoseSpotRESTV2.BuildPatientBody(patient,null,"5035551234","Home"));
			Assert.AreEqual("Ann",jObject.Value<string>("FirstName"));
			Assert.AreEqual((int)PatientGender.Unknown+1,jObject.Value<int>("Gender"));
			Assert.IsNull(jObject["Weight"]);
			Patient patientChild=patient.Copy();
			patientChild.Birthdate=DateTime.Today.AddYears(-10);
			AssertEx.Throws<CodeBase.ODException>(() => DoseSpotRESTV2.BuildPatientBody(patientChild,null,"5035551234","Home"));
			Vitalsign vitalsign=new Vitalsign { Height=50,Weight=70 };
			jObject=JObject.FromObject(DoseSpotRESTV2.BuildPatientBody(patientChild,vitalsign,"5035551234","Home"));
			Assert.AreEqual(70,jObject.Value<float>("Weight"));
			Assert.AreEqual(1,jObject.Value<int>("HeightMetric"));
		}
	}
}
