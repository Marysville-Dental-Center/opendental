// V-PEARL2: one line per HTTP call (endpoint, HTTP status, time taken), offline: the handler under test sits on a scripted fake.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Pearl;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlLoggingHandlerTests {
		private const string ACCESS_TOKEN="synthetic-access-token-0042";
		private const string REQUEST_ID="6f1c0b5e-1111-4d2a-9c3b-000000000001";

		[TestMethod]
		public void SuccessfulCall_IsVerbose_WithEndpointStatusAndTime() {
			using(LogCapture logCapture=new LogCapture("2"))
			using(HttpClient httpClient=new ScriptedHttpHandler().CreateLoggedClient(false)) {
				HttpResponseMessage response=httpClient.GetAsync("/api/v1/image/org-17/byRequestId/"+REQUEST_ID).GetAwaiter().GetResult();
				Assert.AreEqual(HttpStatusCode.OK,response.StatusCode,"The response passes through unchanged.");
				string line=logCapture.Lines.Single();
				StringAssert.Matches(line,new Regex(@"^VERBOSE\tAPI call: GET /api/v1/image/org-17/byRequestId/"+REQUEST_ID+@" -> 200 OK \(\d+ ms\)$"));
				Assert.IsFalse(line.Contains("pearl-api.example.test"),"No host in the line.");
			}
		}

		[TestMethod]
		public void SuccessfulCall_IsNotLogged_BelowVerbose() {
			foreach(string level in new[] { "0","1" }) {
				using(LogCapture logCapture=new LogCapture(level))
				using(HttpClient httpClient=new ScriptedHttpHandler().CreateLoggedClient(false)) {
					httpClient.GetAsync("/api/v1/image").GetAwaiter().GetResult();
					Assert.AreEqual(0,logCapture.Lines.Count,"Level "+level);
				}
			}
		}

		[TestMethod]
		public void FailedStatus_IsAnError_AtLevel0() {
			ScriptedHttpHandler scriptedHttpHandler=new ScriptedHttpHandler { StatusCode=HttpStatusCode.Unauthorized,Body="{\"message\":\"bad\"}" };
			using(LogCapture logCapture=new LogCapture("0"))
			using(HttpClient httpClient=scriptedHttpHandler.CreateLoggedClient(false)) {
				HttpResponseMessage response=httpClient.PostAsync("/api/v1/auth/token",new StringContent("{}")).GetAwaiter().GetResult();
				Assert.AreEqual(HttpStatusCode.Unauthorized,response.StatusCode);
				StringAssert.Matches(logCapture.Lines.Single(),new Regex(@"^ERROR\tAPI call: POST /api/v1/auth/token -> 401 Unauthorized \(\d+ ms\)$"));
			}
		}

		[TestMethod]
		public void TransportFailure_IsAnError_AndStillThrows() {
			ScriptedHttpHandler scriptedHttpHandler=new ScriptedHttpHandler { ExceptionToThrow=new HttpRequestException("No such host is known") };
			using(LogCapture logCapture=new LogCapture("0"))
			using(HttpClient httpClient=scriptedHttpHandler.CreateLoggedClient(false)) {
				bool threw=false;
				try {
					httpClient.GetAsync("/api/v1/image").GetAwaiter().GetResult();
				}
				catch(HttpRequestException) {
					threw=true;
				}
				Assert.IsTrue(threw,"The bridge sees the same exception as before.");
				StringAssert.Matches(logCapture.Lines.Single(),
					new Regex(@"^ERROR\tAPI call failed: GET /api/v1/image, HttpRequestException: No such host is known \(\d+ ms\)$"));
			}
		}

		[TestMethod]
		public void Timeout_IsLoggedAsTimedOut() {
			ScriptedHttpHandler scriptedHttpHandler=new ScriptedHttpHandler { ExceptionToThrow=new TaskCanceledException() };
			using(LogCapture logCapture=new LogCapture("0"))
			using(HttpClient httpClient=scriptedHttpHandler.CreateLoggedClient(false)) {
				try {
					httpClient.GetAsync("/api/v1/image").GetAwaiter().GetResult();
					Assert.Fail("Expected the cancellation to reach the caller.");
				}
				catch(TaskCanceledException) {
				}
				StringAssert.Contains(logCapture.Lines.Single(),"API call failed: GET /api/v1/image, timed out");
			}
		}

		[TestMethod]
		public void PresignedUpload_NeverLogsTheAddress() {
			ScriptedHttpHandler scriptedHttpHandler=new ScriptedHttpHandler { StatusCode=HttpStatusCode.NoContent,Body="" };
			string urlPresigned="https://pearl-uploads-synthetic.s3.amazonaws.com/incoming/img.jpg?X-Amz-Signature=0123456789abcdef";
			using(LogCapture logCapture=new LogCapture("2"))
			using(HttpClient httpClient=scriptedHttpHandler.CreateLoggedClient(true,baseAddress:null)) {
				MultipartFormDataContent formData=new MultipartFormDataContent();
				formData.Add(new StringContent("synthetic-policy-value"),"policy");
				formData.Add(new ByteArrayContent(new byte[] { 0xFF,0xD8,0xFF,0xE0 }),"file","img.jpg");
				httpClient.PostAsync(urlPresigned,formData).GetAwaiter().GetResult();
				string line=logCapture.Lines.Single();
				StringAssert.Matches(line,new Regex(@"^VERBOSE\tAPI call: POST presigned image upload -> 204 No Content \(\d+ ms\)$"));
				Assert.AreEqual(urlPresigned,scriptedHttpHandler.ListUris.Single().AbsoluteUri,"The upload itself is unchanged.");
			}
		}

		[TestMethod]
		public void HeadersAndBodies_AreNeverRead() {
			using(LogCapture logCapture=new LogCapture("2"))
			using(HttpClient httpClient=new ScriptedHttpHandler { Body="{\"access_token\":\""+ACCESS_TOKEN+"\"}" }.CreateLoggedClient(false)) {
				httpClient.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",ACCESS_TOKEN);
				string body="{\"client_id\":\"office-17\",\"client_secret\":\"synthetic-secret-99\",\"patient_name\":\"Synthetic, Pat\"}";
				httpClient.PostAsync("/api/v1/auth/token",new StringContent(body,Encoding.UTF8,"application/json")).GetAwaiter().GetResult();
				string text=logCapture.AllText;
				Assert.AreEqual(1,logCapture.Lines.Count);
				foreach(string notLogged in new[] { ACCESS_TOKEN,"synthetic-secret-99","Synthetic, Pat","office-17" }) {
					Assert.IsFalse(text.Contains(notLogged),notLogged+" in: "+text);
				}
			}
		}

		[TestMethod]
		public void DescribeEndpoint_DropsQueryAndHost() {
			PearlLoggingHandler pearlLoggingHandler=new PearlLoggingHandler(false);
			Assert.AreEqual("GET /api/v1/image",pearlLoggingHandler.DescribeEndpoint(new HttpRequestMessage(HttpMethod.Get,"https://pearl-api.example.test/api/v1/image?x=1")));
			Assert.AreEqual("GET /api/v1/image",pearlLoggingHandler.DescribeEndpoint(new HttpRequestMessage(HttpMethod.Get,new Uri("/api/v1/image?x=1#f",UriKind.Relative))));
			Assert.AreEqual("GET (no address)",pearlLoggingHandler.DescribeEndpoint(new HttpRequestMessage(HttpMethod.Get,(Uri)null)));
			Assert.IsFalse(pearlLoggingHandler.IsPresignedUpload);
			Assert.AreEqual("PUT presigned image upload",new PearlLoggingHandler(true).DescribeEndpoint(
				new HttpRequestMessage(HttpMethod.Put,"https://bucket.s3.amazonaws.com/a?X-Amz-Signature=1")));
		}

		[TestMethod]
		public void Scope_ReachesTheHandler_ThroughApiRequestsTask() {
			//APIRequest.SendRequest runs the call on Task.Run, as the bridge's calls do; the document and request still tag the line.
			ScriptedHttpHandler scriptedHttpHandler=new ScriptedHttpHandler { Body="{\"result\":null}" };
			using(LogCapture logCapture=new LogCapture("2"))
			using(HttpClient httpClient=scriptedHttpHandler.CreateLoggedClient(false)) {
				using(PearlLog.BeginScope(42,REQUEST_ID)) {
					new APIRequest().SendRequest<ImageRequestIdResponse>("/api/v1/image/org-17/byRequestId/"+REQUEST_ID,HttpMethod.Get,null,null,
						HttpContentType.Json,clientOverride:httpClient);
				}
				Assert.AreEqual(42,scriptedHttpHandler.ListContexts.Single().DocNum);
				StringAssert.EndsWith(logCapture.Lines.Single(),"\tdocument 42, request "+REQUEST_ID);
			}
		}
	}
}
