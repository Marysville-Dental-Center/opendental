// V-PEARL2: the per-image lines (uploads, results, failed results, polling progress), with the bridge's own mock client underneath.
// No database: the inner clients below never insert a pearlrequest row.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Pearl;
using Result=OpenPmsDentalBusiness.Pearl.Result;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlApiClientLoggingTests {
		private const string REQUEST_ID="6f1c0b5e-1111-4d2a-9c3b-000000000002";

		///<summary>An inner client for uploads that returns a fixed request ID, or throws, without touching the database.</summary>
		private sealed class UploadOnlyClient:PearlApiClientMock, IPearlApiClient {
			public string RequestIdToReturn=REQUEST_ID;
			public Exception ExceptionToThrow;
			public HttpClient HttpClientToCall;

			string IPearlApiClient.SendOneImageToPearl(long docNum,Bitmap bitmap,Patient patient) {
				if(HttpClientToCall!=null) {
					//The calls a real upload makes, through the same APIRequest path and a logged client.
					new APIRequest().SendRequest<ImageResponse>("/api/v1/image",HttpMethod.Post,null,"{}",HttpContentType.Json,clientOverride:HttpClientToCall);
				}
				if(ExceptionToThrow!=null) {
					throw ExceptionToThrow;
				}
				return RequestIdToReturn;
			}
		}

		private static PearlApiClientLogging Wrap(Func<string,string,ImageRequestIdResponse> funcResult) {
			PearlApiClientLogging pearlApiClientLogging=new PearlApiClientLogging(new UploadOnlyClientWithResults(funcResult));
			pearlApiClientLogging.FuncGetDocNum=requestId => 0;//No pearlrequest rows here.
			return pearlApiClientLogging;
		}

		///<summary>Uploads like UploadOnlyClient and answers checks for results from a function, like the bridge's mock.</summary>
		private sealed class UploadOnlyClientWithResults:PearlApiClientMock, IPearlApiClient {
			public UploadOnlyClientWithResults(Func<string,string,ImageRequestIdResponse> funcResult) {
				FuncGetImageByRequestIdOutput=funcResult;
			}

			string IPearlApiClient.SendOneImageToPearl(long docNum,Bitmap bitmap,Patient patient) {
				return REQUEST_ID;
			}
		}

		private static ImageRequestIdResponse Respond(Result result) {
			return new ImageRequestIdResponse { result=result };
		}

		[TestMethod]
		public void Upload_IsOneInformationLine() {
			using(LogCapture logCapture=new LogCapture("1"))
			using(Bitmap bitmap=new Bitmap(40,30)) {
				PearlApiClientLogging pearlApiClientLogging=new PearlApiClientLogging(new UploadOnlyClient());
				string requestId=pearlApiClientLogging.SendOneImageToPearl(42,bitmap,new Patient());
				Assert.AreEqual(REQUEST_ID,requestId,"Returns what the wrapped client returns.");
				string line=logCapture.Lines.Single();
				StringAssert.StartsWith(line,"INFO\tImage uploaded (");
				StringAssert.EndsWith(line,"\tdocument 42, request "+REQUEST_ID);
				Assert.IsNull(PearlLog.ContextCur);
			}
		}

		[TestMethod]
		public void Upload_AtLevel0_WritesNothing() {
			using(LogCapture logCapture=new LogCapture("0")) {
				new PearlApiClientLogging(new UploadOnlyClient()).SendOneImageToPearl(42,null,new Patient());
				Assert.AreEqual(0,logCapture.Lines.Count,logCapture.AllText);
			}
		}

		[TestMethod]
		public void Upload_AtLevel2_LogsStartAndEachApiCall() {
			ScriptedHttpHandler scriptedHttpHandler=new ScriptedHttpHandler();
			using(LogCapture logCapture=new LogCapture("2"))
			using(HttpClient httpClient=scriptedHttpHandler.CreateLoggedClient(false))
			using(Bitmap bitmap=new Bitmap(40,30)) {
				UploadOnlyClient uploadOnlyClient=new UploadOnlyClient { HttpClientToCall=httpClient };
				new PearlApiClientLogging(uploadOnlyClient).SendOneImageToPearl(42,bitmap,new Patient());
				Assert.AreEqual(3,logCapture.Lines.Count,logCapture.AllText);
				Assert.AreEqual("VERBOSE\tUpload starting (40 x 30 pixels)\tdocument 42",logCapture.Lines[0]);
				StringAssert.StartsWith(logCapture.Lines[1],"VERBOSE\tAPI call: POST /api/v1/image -> 200 OK (");
				StringAssert.EndsWith(logCapture.Lines[1],"\tdocument 42","The API call is tagged with the document before the request ID exists.");
				StringAssert.StartsWith(logCapture.Lines[2],"INFO\tImage uploaded (");
			}
		}

		[TestMethod]
		public void UploadFailure_IsRethrown_AndVerboseOnly() {
			ApplicationException applicationException=new ApplicationException("Could not upload image successfully.");
			using(LogCapture logCapture=new LogCapture("1")) {
				PearlApiClientLogging pearlApiClientLogging=new PearlApiClientLogging(new UploadOnlyClient { ExceptionToThrow=applicationException });
				try {
					pearlApiClientLogging.SendOneImageToPearl(42,null,new Patient());
					Assert.Fail("Expected the upload to throw.");
				}
				catch(ApplicationException ex) {
					Assert.AreSame(applicationException,ex,"The same exception reaches the bridge, which logs it as an error.");
				}
				Assert.AreEqual(0,logCapture.Lines.Count,"The worker logs the failure; this client adds detail only at level 2.");
				logCapture.LogLevel="2";
				try {
					pearlApiClientLogging.SendOneImageToPearl(42,null,new Patient());
				}
				catch(ApplicationException) {
				}
				StringAssert.Contains(logCapture.AllText,"Upload failed after ");
				StringAssert.Contains(logCapture.AllText,"Could not upload image successfully.");
				Assert.AreEqual(0,pearlApiClientLogging.CountTracked);
			}
		}

		[TestMethod]
		public void Upload_WithoutRequestId_IsNotTracked() {
			using(LogCapture logCapture=new LogCapture("2")) {
				PearlApiClientLogging pearlApiClientLogging=new PearlApiClientLogging(new UploadOnlyClient { RequestIdToReturn=null });
				Assert.IsNull(pearlApiClientLogging.SendOneImageToPearl(42,null,new Patient()));
				StringAssert.Contains(logCapture.AllText,"Upload returned no request ID");
				Assert.AreEqual(0,pearlApiClientLogging.CountTracked);
			}
		}

		[TestMethod]
		public void CompletedResult_IsOneInformationLine_WithCountsAndProgress() {
			int countCalls=0;
			PearlApiClientLogging pearlApiClientLogging=Wrap((org,requestId) => {
				countCalls++;
				if(countCalls<3) {
					return Respond(null);//Still processing (Pearl answered 202).
				}
				return Respond(new Result {
					is_completed=true,
					annotations=new Annotation[] { new Annotation(),new Annotation() },
					toothParts=new ToothPart[] { new ToothPart() },
				});
			});
			DateTime dateTimeNow=new DateTime(2026,10,25,9,0,0);
			pearlApiClientLogging.FuncNow=() => dateTimeNow;
			using(LogCapture logCapture=new LogCapture("1")) {
				Assert.AreEqual(REQUEST_ID,pearlApiClientLogging.SendOneImageToPearl(42,null,new Patient()));
				logCapture.Clear();
				Assert.IsNull(pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID));
				dateTimeNow=dateTimeNow.AddSeconds(2);
				Assert.IsNull(pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID));
				dateTimeNow=dateTimeNow.AddSeconds(2);
				Result result=pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
				Assert.IsTrue(result.is_completed,"Returns what the wrapped client returns.");
				Assert.AreEqual("INFO\tResults received: 2 annotations, 1 tooth parts (check 3, 4 s since the first check)\tdocument 42, request "+REQUEST_ID,
					logCapture.Lines.Single());
				Assert.AreEqual(0,pearlApiClientLogging.CountTracked,"Forgotten once the result is final.");
			}
		}

		[TestMethod]
		public void PollingProgress_IsVerbose() {
			PearlApiClientLogging pearlApiClientLogging=Wrap((org,requestId) => Respond(new Result()));
			DateTime dateTimeNow=new DateTime(2026,10,25,9,0,0);
			pearlApiClientLogging.FuncNow=() => dateTimeNow;
			using(LogCapture logCapture=new LogCapture("2")) {
				pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
				dateTimeNow=dateTimeNow.AddSeconds(75);
				pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
				CollectionAssert.AreEqual(new[] {
						"VERBOSE\tResults not ready, Pearl is still processing (check 1, 0 s since the first check)\trequest "+REQUEST_ID,
						"VERBOSE\tResults not ready, Pearl is still processing (check 2, 75 s since the first check)\trequest "+REQUEST_ID,
					},logCapture.Lines,logCapture.AllText);
				Assert.AreEqual(1,pearlApiClientLogging.CountTracked);
				logCapture.Clear();
				logCapture.LogLevel="1";
				pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
				Assert.AreEqual(0,logCapture.Lines.Count,"Progress is verbose only.");
			}
		}

		[TestMethod]
		public void RejectedOrDeletedResult_IsAnError_AtLevel0() {
			foreach(Result resultFailed in new[] {
				new Result { is_rejected=true,message_type="synthetic_reason" },
				new Result { is_deleted=true },
				new Result { is_rejected=true,is_deleted=true,is_completed=true },
			})
			{
				PearlApiClientLogging pearlApiClientLogging=Wrap((org,requestId) => Respond(resultFailed));
				using(LogCapture logCapture=new LogCapture("0")) {
					pearlApiClientLogging.SendOneImageToPearl(9,null,new Patient());
					Result result=pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
					Assert.AreSame(resultFailed,result);
					string line=logCapture.Lines.Single();
					StringAssert.StartsWith(line,"ERROR\tPearl ");
					StringAssert.Contains(line,"the image; no results will come (check 1,");
					StringAssert.EndsWith(line,"\tdocument 9, request "+REQUEST_ID);
					if(resultFailed.message_type!=null) {
						StringAssert.Contains(line,"message type synthetic_reason");
					}
					if(resultFailed.is_rejected && resultFailed.is_deleted) {
						StringAssert.Contains(line,"Pearl rejected and deleted the image");
					}
					Assert.AreEqual(0,pearlApiClientLogging.CountTracked);
				}
			}
		}

		[TestMethod]
		public void ResultCheckFailure_IsRethrown_AndCountingCarriesOn() {
			PearlApiClientLogging pearlApiClientLogging=Wrap(null);//The bridge's mock throws when it has no result function.
			using(LogCapture logCapture=new LogCapture("2")) {
				pearlApiClientLogging.SendOneImageToPearl(5,null,new Patient());
				logCapture.Clear();
				for(int i=0;i<2;i++) {
					bool threw=false;
					try {
						pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
					}
					catch(Exception) {
						threw=true;
					}
					Assert.IsTrue(threw,"The same exception reaches the bridge.");
				}
				Assert.AreEqual(2,logCapture.Lines.Count,logCapture.AllText);
				Assert.AreEqual("VERBOSE\tResults check 1 failed: GetImageByRequestId threw an exception.\tdocument 5, request "+REQUEST_ID,logCapture.Lines[0]);
				StringAssert.StartsWith(logCapture.Lines[1],"VERBOSE\tResults check 2 failed:","If the bridge polls again, the count goes on.");
				Assert.AreEqual(1,pearlApiClientLogging.CountTracked,"Kept until a final result or the hour runs out.");
			}
		}

		[TestMethod]
		public void RequestNotUploadedHere_GetsItsDocumentFromItsRowOnce() {
			int countLookups=0;
			PearlApiClientLogging pearlApiClientLogging=Wrap((org,requestId) => Respond(new Result()));
			pearlApiClientLogging.FuncGetDocNum=requestId => {
				countLookups++;
				return requestId==REQUEST_ID ? 77 : 0;
			};
			using(LogCapture logCapture=new LogCapture("2")) {
				pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
				pearlApiClientLogging.GetOneImageFromPearl(REQUEST_ID);
				Assert.AreEqual(1,countLookups,"One lookup per request, not per check.");
				Assert.IsTrue(logCapture.Lines.All(x => x.EndsWith("\tdocument 77, request "+REQUEST_ID)),logCapture.AllText);
				pearlApiClientLogging.FuncGetDocNum=requestId => throw new InvalidOperationException("no database");
				pearlApiClientLogging.GetOneImageFromPearl("request-other");
				StringAssert.EndsWith(logCapture.Lines.Last(),"\trequest request-other","A failed lookup leaves the document out.");
			}
		}

		[TestMethod]
		public void OldRequests_AreForgotten() {
			PearlApiClientLogging pearlApiClientLogging=Wrap((org,requestId) => Respond(new Result()));
			DateTime dateTimeNow=new DateTime(2026,10,25,9,0,0);
			pearlApiClientLogging.FuncNow=() => dateTimeNow;
			using(new LogCapture("0")) {
				pearlApiClientLogging.GetOneImageFromPearl("request-a");
				pearlApiClientLogging.GetOneImageFromPearl("request-b");
				Assert.AreEqual(2,pearlApiClientLogging.CountTracked);
				dateTimeNow=dateTimeNow.AddHours(2);
				pearlApiClientLogging.GetOneImageFromPearl("request-c");
				Assert.AreEqual(1,pearlApiClientLogging.CountTracked,"Polling stops after 10 minutes; requests an hour old are dropped.");
			}
		}

		[TestMethod]
		public void OtherMembers_PassThrough() {
			PearlApiClientMock pearlApiClientMock=new PearlApiClientMock { SucceedGetAuthToken=false };
			PearlApiClientLogging pearlApiClientLogging=new PearlApiClientLogging(pearlApiClientMock);
			Assert.AreSame(pearlApiClientMock,pearlApiClientLogging.Inner);
			Assert.IsNotNull(pearlApiClientLogging.GetAwsPresignedUrlInfo(new ImageRequest()));
			Assert.IsTrue(pearlApiClientLogging.UploadToAwsPresignedUrl(null,new ImageResponse()));
			Assert.AreEqual("",pearlApiClientLogging.BitmapToImageTempCopy(null,"x.jpg"));
			Assert.AreEqual("org-1",pearlApiClientLogging.GetImageByRequestId("org-1","r").result.organization_id);
			try {
				pearlApiClientLogging.GetAuthToken("id","secret");
				Assert.Fail("Expected the mock's exception.");
			}
			catch(Exception ex) {
				Assert.AreEqual("GetAuthToken threw an exception.",ex.Message);
			}
			Assert.AreEqual("",pearlApiClientLogging.GetOneImageFromPearl("").request_id,"Blank request IDs go straight through.");
			Assert.AreEqual(0,pearlApiClientLogging.CountTracked);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException))]
		public void Constructor_NeedsAnInnerClient() {
			new PearlApiClientLogging(null);
		}
	}
}
