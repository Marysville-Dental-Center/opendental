// Tests for the PDMP package: the HTTP path of both bridges against a fake handler (no network): the two Bamboo steps, LogiCoy, and the error
// mapping. Asserts that no response body and no patient data reach an exception message.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.Pdmp {
	[TestClass]
	public class PdmpHttpTests {
		private const string BODY_MARKER="BODY-MARKER-7f3a";

		[TestInitialize]
		public void TestInitialize() {
			PdmpTestData.UseEnUs();
		}

		///<summary>A PatientResponse with NarxCare scores and a ViewableReport URL.</summary>
		public static BambooScript.PatientResponseType MakeReportResponse() {
			return new BambooScript.PatientResponseType {
				Item=new BambooScript.ReportType {
					NarxScores=new[] {
						new BambooScript.NarxScoreType { ScoreType="Narcotics",ScoreValue="310" },
						new BambooScript.NarxScoreType { ScoreType="Sedatives",ScoreValue="120" },
					},
					ReportRequestURLs=new BambooScript.ReportTypeReportRequestURLs {
						ViewableReport=new BambooScript.ReportURLType { Value=PdmpTestData.ViewableReportUrl },
					},
				},
			};
		}

		private static PDMPBamboo MakeBamboo(FakeHttpHandler fakeHttpHandler) {
			return new PDMPBamboo(PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl),fakeHttpHandler);
		}

		private static void AssertNoLeak(Exception ex) {
			Assert.IsFalse(ex.Message.Contains(BODY_MARKER),"Response body in the message: "+ex.Message);
			Assert.IsFalse(ex.Message.Contains(PdmpTestData.PatientLastName),"Patient name in the message: "+ex.Message);
			Assert.IsFalse(ex.Message.Contains(PdmpTestData.Password),"Password in the message.");
			Assert.IsNull(ex.InnerException,"ProgressWin would rethrow an inner exception in place of the PdmpException.");
		}

		[TestMethod]
		public void Bamboo_TwoSteps_DigestHeadersAndReportLink() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(MakeReportResponse()),"application/xml");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(new BambooScript.ReportResponseType {
				Items=new object[] { PdmpTestData.ReportLink } }),"application/xml");
			PDMPBamboo pdmpBamboo=MakeBamboo(fakeHttpHandler);
			pdmpBamboo.QueryPatient();
			Assert.IsTrue(pdmpBamboo.HasReport);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count,"Step 1 only: the report is requested after OK.");
			RecordedRequest recordedPatient=fakeHttpHandler.Requests[0];
			Assert.AreEqual(HttpMethod.Post,recordedPatient.Method);
			Assert.AreEqual(PdmpTestData.GatewayUrl,recordedPatient.RequestUri.AbsoluteUri);
			Assert.AreEqual("application/xml",recordedPatient.ContentType);
			StringAssert.Contains(recordedPatient.GetHeader("Accept"),"application/xml");
			Assert.AreEqual(PdmpTestData.Username,recordedPatient.GetHeader("X-Auth-Username"));
			string nonce=recordedPatient.GetHeader("X-Auth-Nonce");
			long epoch=long.Parse(recordedPatient.GetHeader("X-Auth-Timestamp"));
			Assert.IsFalse(nonce.Contains(":"));
			Assert.IsTrue(Math.Abs(epoch-BambooAuth.GetEpoch(DateTime.Now))<120,"Seconds since 1970, now.");
			Assert.AreEqual(new BambooAuth(epoch,nonce).GetPasswordDigest(PdmpTestData.Password),recordedPatient.GetHeader("X-Auth-PasswordDigest"));
			StringAssert.Contains(recordedPatient.Body,"PatientRequest");
			string link=pdmpBamboo.GetReportLink();
			Assert.AreEqual(PdmpTestData.ReportLink,link);
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count);
			RecordedRequest recordedReport=fakeHttpHandler.Requests[1];
			Assert.AreEqual(PdmpTestData.ViewableReportUrl,recordedReport.RequestUri.AbsoluteUri);
			StringAssert.Contains(recordedReport.Body,"ReportRequest");
			Assert.AreNotEqual(nonce,recordedReport.GetHeader("X-Auth-Nonce"),"A fresh nonce per request.");
		}

		[TestMethod]
		public void Logicoy_PostsWithBasicAuthAndReturnsTheReportUrl() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PDMPScript.PrescriptionSummaryResponseType response=new PDMPScript.PrescriptionSummaryResponseType {
				Report=new PDMPScript.ReportType {
					ReportRequestURLs=new PDMPScript.ReportTypeReportRequestURLs {
						ViewableReport=new PDMPScript.ReportURLType { Value=PdmpTestData.ReportLink },
					},
				},
			};
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(response),"application/xml");
			PDMPLogicoy pdmpLogicoy=new PDMPLogicoy(PdmpTestData.MakeProperty(ProgramName.PDMP.ToString(),"CA",PdmpTestData.LogicoyUrl),fakeHttpHandler);
			Assert.AreEqual(PdmpTestData.ReportLink,pdmpLogicoy.GetURL());
			RecordedRequest recorded=fakeHttpHandler.Requests.Single();
			Assert.AreEqual(PdmpTestData.LogicoyUrl,recorded.RequestUri.AbsoluteUri);
			StringAssert.StartsWith(recorded.GetHeader("Authorization"),"Basic ");
			Assert.IsFalse(recorded.Body.Contains("999001234"),"No SSN for California.");
			Assert.IsFalse(recorded.Body.IndexOf("Open Dental",StringComparison.OrdinalIgnoreCase)>=0);
		}

		[TestMethod]
		public void Logicoy_VendorErrorShowsItsMessage() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PDMPScript.PrescriptionSummaryResponseType response=new PDMPScript.PrescriptionSummaryResponseType {
				Error=new PDMPScript.PrescriptionSummaryResponseTypeError { Message="Invalid facility" },
			};
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(response),"application/xml");
			PDMPLogicoy pdmpLogicoy=new PDMPLogicoy(PdmpTestData.MakeProperty(ProgramName.PDMP.ToString(),"CA",PdmpTestData.LogicoyUrl),fakeHttpHandler);
			PdmpException ex=AssertEx.Throws<PdmpException>(() => pdmpLogicoy.GetURL());
			Assert.AreEqual(PdmpErrorKind.VendorError,ex.Kind);
			StringAssert.Contains(ex.Message,"Invalid facility");
		}

		[TestMethod]
		public void Errors_401_IsUnauthorizedWithTheClockHint() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.Unauthorized,BODY_MARKER,"text/plain");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.Unauthorized,ex.Kind);
			Assert.AreEqual(401,ex.HttpStatus);
			StringAssert.Contains(ex.Message,PdmpHttp.MessageUnauthorized);
			StringAssert.Contains(ex.Message,PdmpHttp.MessageClock);
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_403_IsCertificateRejected() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.Forbidden,BODY_MARKER,"text/html");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.CertificateRejected,ex.Kind);
			Assert.AreEqual(PdmpHttp.MessageCertificateRejected,ex.Message);
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_503_IsServiceUnavailable() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.ServiceUnavailable,BODY_MARKER,"text/plain");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.ServiceUnavailable,ex.Kind);
			Assert.AreEqual(PdmpHttp.MessageUnavailable,ex.Message);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count,"POSTs are never retried.");
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_DisallowedIsABusinessOutcome() {
			BambooScript.PatientResponseType patientResponse=new BambooScript.PatientResponseType {
				Response=new[] {
					new BambooScript.ResponseType { Item=new BambooScript.ResponseTypeDisallowed { Message="No unique patient found." } },
				},
			};
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(patientResponse),"application/xml");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.Disallowed,ex.Kind);
			StringAssert.Contains(ex.Message,"No unique patient found.");
			StringAssert.Contains(ex.Message,PdmpHttp.MessageSearchPortal);
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_VendorErrorShowsMessageAndDetails() {
			BambooScript.PatientResponseType patientResponse=new BambooScript.PatientResponseType {
				Item=new BambooScript.PatientResponseTypeError { Message="Bad request",Details=new[] { "Birthdate missing" } },
			};
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.BadRequest,PdmpHttp.Serialize(patientResponse),"application/xml");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.VendorError,ex.Kind,"A 4xx carrying the vendor's error document shows that error.");
			StringAssert.Contains(ex.Message,"Bad request");
			StringAssert.Contains(ex.Message,"Birthdate missing");
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_MalformedXml() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,"<html><body>"+BODY_MARKER+"</body></html>","text/html");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.MalformedResponse,ex.Kind);
			Assert.AreEqual(PdmpHttp.MessageMalformed,ex.Message);
			AssertNoLeak(ex);
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"not xml "+BODY_MARKER,"text/plain");
			ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.MalformedResponse,ex.Kind);
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_OtherHttpStatusWithoutXml() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.NotFound,BODY_MARKER,"text/plain");
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.HttpError,ex.Kind);
			Assert.AreEqual(404,ex.HttpStatus);
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_NetworkFailureIsUnreachable() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().EnqueueNetworkFailure();
			PdmpException ex=AssertEx.Throws<PdmpException>(() => MakeBamboo(fakeHttpHandler).QueryPatient());
			Assert.AreEqual(PdmpErrorKind.Unreachable,ex.Kind);
			Assert.AreEqual(PdmpHttp.MessageUnreachable,ex.Message);
			Assert.AreEqual(nameof(HttpRequestException),ex.CauseType);
			AssertNoLeak(ex);
		}

		[TestMethod]
		public void Errors_TlsFailureIsCertificateRejected() {
			WebException webException=new WebException("The request was aborted: Could not create SSL/TLS secure channel.",
				WebExceptionStatus.SecureChannelFailure);
			PdmpException ex=PdmpHttp.MapSendException(new HttpRequestException("An error occurred while sending the request.",webException));
			Assert.AreEqual(PdmpErrorKind.CertificateRejected,ex.Kind);
			WebException webExceptionDns=new WebException("The remote name could not be resolved",WebExceptionStatus.NameResolutionFailure);
			Assert.AreEqual(PdmpErrorKind.Unreachable,PdmpHttp.MapSendException(new HttpRequestException("x",webExceptionDns)).Kind);
		}

		[TestMethod]
		public void Errors_ReportStepDisallowed() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(MakeReportResponse()),"application/xml");
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,PdmpHttp.Serialize(new BambooScript.ReportResponseType {
				Items=new object[] { new BambooScript.ReportResponseTypeDisallowed { Message="Report no longer available." } } }),"application/xml");
			PDMPBamboo pdmpBamboo=MakeBamboo(fakeHttpHandler);
			pdmpBamboo.QueryPatient();
			PdmpException ex=AssertEx.Throws<PdmpException>(() => pdmpBamboo.GetReportLink());
			Assert.AreEqual(PdmpErrorKind.Disallowed,ex.Kind);
			StringAssert.Contains(ex.Message,"Report no longer available.");
		}

		[TestMethod]
		public void CheckReachable_SendsABodylessGetToTheOrigin() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.MethodNotAllowed,"","text/plain");
			Assert.AreEqual("",PdmpHttp.CheckReachable(PdmpTestData.GatewayUrl,null,fakeHttpHandler),"Any HTTP status counts as reachable.");
			RecordedRequest recorded=fakeHttpHandler.Requests.Single();
			Assert.AreEqual(HttpMethod.Get,recorded.Method);
			Assert.AreEqual("https://gateway.example.test/",recorded.RequestUri.AbsoluteUri);
			Assert.AreEqual("",recorded.Body);
			fakeHttpHandler.EnqueueNetworkFailure();
			Assert.AreEqual(PdmpHttp.MessageUnreachable,PdmpHttp.CheckReachable(PdmpTestData.GatewayUrl,null,fakeHttpHandler));
			Assert.AreNotEqual("",PdmpHttp.CheckReachable("http://gateway.example.test/",null,fakeHttpHandler),"Plain http is refused before sending.");
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count);
		}
	}
}
