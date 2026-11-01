// 24.4 v24_4-imaging-pearl-request-statuses against a scratch database: the sending thread of the Pearl bridge (Pearl.SendOnThreadWorker) with
// the real PearlApiClient on the fake HTTP layer. A failed upload becomes ErrorUploading and is uploaded again; a failed poll becomes
// ErrorProcessing and, like a timeout, is polled again under the same request ID without a second upload; a finished result without findings is
// NoResults; rejected, no-result and unknown requests are not sent again. Synthetic data only; nothing reaches the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Pearl;
using Result=OpenDentBusiness.Pearl.Result;
using PearlBridge=OpenDentBusiness.Bridges.Pearl;

namespace VPEARL1Tests {
	[TestClass]
	public class PearlSendDbTests {
		private const string TOKEN="POST /api/v1/auth/token";
		private const string PRESIGN="POST /api/v1/image";
		private const string UPLOAD="POST "+FakePearlHttp.UPLOAD_URL;
		private FakePearlHttp _fakePearlHttp;
		private Bitmap _bitmap;

		[TestInitialize]
		public void TestInitialize() {
			PearlDb.Begin();
			PearlDb.RefreshCaches();
			_fakePearlHttp=new FakePearlHttp();
			APIRequest.Inst=_fakePearlHttp;
			PearlApiClient.Inst=new PearlApiClient();
			Security.CurUser=new Userod { UserNum=1,UserName="vpearl1test" };
			_bitmap=PearlDb.CreateBitmap();
		}

		[TestCleanup]
		public void TestCleanup() {
			APIRequest.Inst=new FakePearlHttp();//Never the real HTTP layer in this test run.
			_bitmap?.Dispose();
			Security.CurUser=new Userod();
			if(_fakePearlHttp==null) {
				return;
			}
			//A failed upload leaves the client's temporary copy of the image behind.
			foreach(string fileName in _fakePearlHttp.GetUploadFileNames()) {
				try {
					string path=Path.Combine(Path.GetTempPath(),"opendental",fileName);
					if(File.Exists(path)) {
						File.Delete(path);
					}
				}
				catch(Exception) {
					//Temp files only.
				}
			}
		}

		///<summary>Runs the sending thread for the image until it ends. Returns the exception it reported (the program shows it as an alert), or null.
		///</summary>
		private static Exception RunSend(PearlBridge pearl) {
			Exception exception=null;
			ODThread oDThread=new ODThread(pearl.SendOnThreadWorker);
			oDThread.AddExceptionHandler((ex) => exception=ex);
			oDThread.Start();
			if(!oDThread.Join(30_000)) {
				oDThread.QuitSync(5_000);
				Assert.Fail("The Pearl sending thread did not finish.");
			}
			return exception;
		}

		private static void AssertNoError(Exception exception) {
			Assert.IsNull(exception,exception?.ToString());
		}

		///<summary>What the toolbar button does for one image: the setup refuses an image that is not sent again (null).</summary>
		private PearlBridge Setup(Patient patient,Document document) {
			return PearlBridge.SetupPearlForSendingSingle(patient,document,_bitmap);
		}

		private static List<PearlRequest> GetRequests(long docNum) {
			return OpenDentBusiness.Crud.PearlRequestCrud.SelectMany("SELECT * FROM pearlrequest WHERE DocNum="+POut.Long(docNum)+" ORDER BY PearlRequestNum");
		}

		private static string PollCall(string requestId) {
			return "GET /api/v1/image/"+PearlDb.ORGANIZATION_ID+"/byRequestId/"+requestId;
		}

		[TestMethod]
		public void NewImage_IsUploadedPolledAndReceived() {
			Patient patient=PearlDb.CreatePatient("new");
			Document document=PearlDb.CreateDocument(patient);
			PearlBridge pearl=Setup(patient,document);
			Assert.IsNotNull(pearl,"An image never sent can be sent.");
			AssertNoError(RunSend(pearl));
			PearlRequest pearlRequest=GetRequests(document.DocNum).Single();
			Assert.AreEqual(EnumPearlStatus.Received,pearlRequest.RequestStatus);
			CollectionAssert.AreEqual(new[] { TOKEN,PRESIGN,UPLOAD,TOKEN,PollCall(pearlRequest.RequestId) },_fakePearlHttp.GetCalls());
			Assert.IsTrue(ImageDraws.RefreshForDoc(document.DocNum).Any(x => x.ImageAnnotVendor==EnumImageAnnotVendor.Pearl),"The findings are drawn.");
			Assert.IsNull(Setup(patient,document),"A received image is not sent again.");
		}

		[TestMethod]
		public void FailedUpload_IsErrorUploading_AndIsUploadedAgainWithANewRequest() {
			Patient patient=PearlDb.CreatePatient("upload");
			Document document=PearlDb.CreateDocument(patient);
			_fakePearlHttp.FailUpload=true;
			Exception exception=RunSend(Setup(patient,document));
			Assert.IsNotNull(exception,"The failure is reported (an alert in the program).");
			StringAssert.Contains(exception.Message,"There was an error uploading an image");
			PearlRequest pearlRequestFailed=GetRequests(document.DocNum).Single();
			Assert.AreEqual(EnumPearlStatus.ErrorUploading,pearlRequestFailed.RequestStatus,"Not left as Uploading, where it could never be sent again.");
			Assert.AreEqual("Upload failed. Can be sent again.",PearlRequests.GetStatusText(pearlRequestFailed.RequestStatus));
			//Send again: a new upload under a new request ID, and the failed request is gone.
			_fakePearlHttp.FailUpload=false;
			int countCallsBefore=_fakePearlHttp.GetCalls().Count;
			PearlBridge pearl=Setup(patient,document);
			Assert.IsNotNull(pearl,"A failed upload can be sent again.");
			AssertNoError(RunSend(pearl));
			PearlRequest pearlRequest=GetRequests(document.DocNum).Single();
			Assert.AreNotEqual(pearlRequestFailed.RequestId,pearlRequest.RequestId);
			Assert.AreEqual(EnumPearlStatus.Received,pearlRequest.RequestStatus);
			List<string> listCallsAgain=_fakePearlHttp.GetCalls().Skip(countCallsBefore).ToList();
			CollectionAssert.AreEqual(new[] { TOKEN,PRESIGN,UPLOAD,TOKEN,PollCall(pearlRequest.RequestId) },listCallsAgain);
		}

		[TestMethod]
		public void FailureBeforeTheUpload_LeavesNoRequest() {
			Patient patient=PearlDb.CreatePatient("auth");
			Document document=PearlDb.CreateDocument(patient);
			_fakePearlHttp.FailAuth=true;
			Exception exception=RunSend(Setup(patient,document));
			Assert.IsNotNull(exception);
			Assert.AreEqual(0,GetRequests(document.DocNum).Count,"Nothing was uploaded, so the image is simply sent again.");
			CollectionAssert.AreEqual(new[] { TOKEN },_fakePearlHttp.GetCalls());
			Assert.IsNotNull(Setup(patient,document));
		}

		[TestMethod]
		public void FailedPoll_IsErrorProcessing_AndIsPolledAgainWithoutUploading() {
			Patient patient=PearlDb.CreatePatient("poll");
			Document document=PearlDb.CreateDocument(patient);
			_fakePearlHttp.FuncPoll=(requestId) => throw new HttpRequestException("Synthetic poll failure.");
			Exception exception=RunSend(Setup(patient,document));
			Assert.IsNotNull(exception);
			StringAssert.Contains(exception.Message,"There was an error retrieving AI results for an image");
			PearlRequest pearlRequestFailed=GetRequests(document.DocNum).Single();
			Assert.AreEqual(EnumPearlStatus.ErrorProcessing,pearlRequestFailed.RequestStatus,"Not left as Polling, where it could never be sent again.");
			//Send again: the same request is polled; the image is not uploaded again.
			_fakePearlHttp.FuncPoll=(requestId) => FakePearlHttp.Completed(requestId,hasFindings:true);
			int countCallsBefore=_fakePearlHttp.GetCalls().Count;
			PearlBridge pearl=Setup(patient,document);
			Assert.IsNotNull(pearl,"A processing failure can be sent again.");
			AssertNoError(RunSend(pearl));
			PearlRequest pearlRequest=GetRequests(document.DocNum).Single();
			Assert.AreEqual(pearlRequestFailed.PearlRequestNum,pearlRequest.PearlRequestNum);
			Assert.AreEqual(pearlRequestFailed.RequestId,pearlRequest.RequestId);
			Assert.AreEqual(EnumPearlStatus.Received,pearlRequest.RequestStatus);
			CollectionAssert.AreEqual(new[] { TOKEN,PollCall(pearlRequest.RequestId) },_fakePearlHttp.GetCalls().Skip(countCallsBefore).ToList());
		}

		[TestMethod]
		public void TimedOut_FromAnotherSession_IsPolledAgainWithAToken() {
			Patient patient=PearlDb.CreatePatient("timedout");
			Document document=PearlDb.CreateDocument(patient);
			PearlDb.InsertRequest(document.DocNum,EnumPearlStatus.TimedOut);
			PearlRequest pearlRequestOld=GetRequests(document.DocNum).Single();//As stored (whole seconds).
			PearlBridge pearl=Setup(patient,document);
			Assert.IsNotNull(pearl,"A timed out request can be sent again.");
			AssertNoError(RunSend(pearl));
			PearlRequest pearlRequest=GetRequests(document.DocNum).Single();
			Assert.AreEqual(pearlRequestOld.RequestId,pearlRequest.RequestId);
			Assert.AreEqual(EnumPearlStatus.Received,pearlRequest.RequestStatus);
			Assert.AreEqual(pearlRequestOld.DateTSent,pearlRequest.DateTSent,"Still the time the image was first sent.");
			//No upload in this session, so the poll gets its own token first (it had none before V-PEARL1).
			CollectionAssert.AreEqual(new[] { TOKEN,PollCall(pearlRequest.RequestId) },_fakePearlHttp.GetCalls());
		}

		[TestMethod]
		public void CompletedWithoutFindings_IsNoResults_AndNotSentAgain() {
			Patient patient=PearlDb.CreatePatient("noresults");
			Document document=PearlDb.CreateDocument(patient);
			_fakePearlHttp.FuncPoll=(requestId) => FakePearlHttp.Completed(requestId,hasFindings:false);
			AssertNoError(RunSend(Setup(patient,document)));
			PearlRequest pearlRequest=GetRequests(document.DocNum).Single();
			Assert.AreEqual(EnumPearlStatus.NoResults,pearlRequest.RequestStatus);
			Assert.AreEqual(0,ImageDraws.RefreshForDoc(document.DocNum).Count);
			Assert.IsNull(Setup(patient,document),"No results: not uploaded again.");
			Assert.AreEqual("No results for this image.",PearlRequests.GetStatusText(pearlRequest.RequestStatus));
		}

		[TestMethod]
		public void Rejected_IsError_AndNotSentAgain() {
			Patient patient=PearlDb.CreatePatient("rejected");
			Document document=PearlDb.CreateDocument(patient);
			_fakePearlHttp.FuncPoll=(requestId) => new ImageRequestIdResponse { result=new Result { request_id=requestId,is_rejected=true } };
			AssertNoError(RunSend(Setup(patient,document)));
			Assert.AreEqual(EnumPearlStatus.Error,GetRequests(document.DocNum).Single().RequestStatus);
			Assert.IsNull(Setup(patient,document),"Rejected: never sent again.");
		}

		[TestMethod]
		public void StillProcessing_KeepsPolling() {
			Patient patient=PearlDb.CreatePatient("processing");
			Document document=PearlDb.CreateDocument(patient);
			//Pearl answers 202 Accepted while it works: no result yet.
			_fakePearlHttp.FuncPoll=(requestId) => new ImageRequestIdResponse();
			ODThread oDThread=new ODThread(Setup(patient,document).SendOnThreadWorker);
			Exception exception=null;
			oDThread.AddExceptionHandler((ex) => exception=ex);
			oDThread.Start();
			Assert.IsFalse(oDThread.Join(3_000),"Still polling.");
			Assert.AreEqual(EnumPearlStatus.Polling,GetRequests(document.DocNum).Single().RequestStatus);
			Assert.IsNull(Setup(patient,document),"Not sent again while it is being polled.");
			oDThread.QuitSync(10_000);
			Assert.IsNull(exception);
		}

		[TestMethod]
		public void OfficeRowsWithEveryStatus_AreReadAndHandled() {
			Patient patient=PearlDb.CreatePatient("office");
			//Rows as Open Dental 24.4 and later write them, plus a value no published version uses, written with plain SQL.
			int[] arrayStatuses=new[] { 0,1,2,3,4,5,6,7,9 };
			foreach(int status in arrayStatuses) {
				Document document=PearlDb.CreateDocument(patient);
				DataCore.NonQ("INSERT INTO pearlrequest (RequestId,DocNum,RequestStatus,DateTSent,DateTChecked) VALUES ('office-"+status+"',"
					+POut.Long(document.DocNum)+","+status+","+POut.DateT(DateTime.Now.AddDays(-1))+","+POut.DateT(DateTime.MinValue)+")");
				PearlRequest pearlRequest=PearlRequests.GetOneByDocNum(document.DocNum);
				Assert.AreEqual(status,(int)pearlRequest.RequestStatus,"Stored values load unchanged.");
				bool isSendable=status.In(3,5,6);
				Assert.AreEqual(isSendable,PearlRequests.CanSendToPearl(pearlRequest),"Status "+status);
				Assert.AreEqual(isSendable,Setup(patient,document)!=null,"Status "+status);
				Assert.AreEqual(status.In(1,2,5,6,7,9),PearlRequests.IsRequestHandled(pearlRequest),"Status "+status);
			}
			Assert.AreEqual(0,_fakePearlHttp.GetCalls().Count,"Reading statuses calls nobody.");
		}

		[TestMethod]
		public void SendingThread_RefusesAnImageAlreadySent() {
			Patient patient=PearlDb.CreatePatient("refuse");
			foreach(EnumPearlStatus pearlStatus in new[] { EnumPearlStatus.Polling,EnumPearlStatus.Uploading,EnumPearlStatus.Received,
				EnumPearlStatus.Error,EnumPearlStatus.NoResults })
			{
				Document document=PearlDb.CreateDocument(patient);
				PearlDb.InsertRequest(document.DocNum,pearlStatus);
				//Built by hand: the setup would already refuse it (another workstation may have sent it in between).
				PearlBridge pearl=new PearlBridge();
				pearl.Patient_=patient;
				pearl.Document_=document;
				pearl.Bitmap_=PearlDb.CreateBitmap();
				Exception exception=RunSend(pearl);
				Assert.IsNotNull(exception,pearlStatus.ToString());
				Assert.AreEqual("Image was already sent to Pearl",exception.Message);
				Assert.AreEqual(pearlStatus,GetRequests(document.DocNum).Single().RequestStatus,"Unchanged: "+pearlStatus);
			}
			Assert.AreEqual(0,_fakePearlHttp.GetCalls().Count);
		}

		[TestMethod]
		public void SetUploadFailed_ChangesOnlyUploadingRowsOfTheImage() {
			Patient patient=PearlDb.CreatePatient("setfailed");
			Document document=PearlDb.CreateDocument(patient);
			Document documentOther=PearlDb.CreateDocument(patient);
			PearlRequest pearlRequestUploading=PearlDb.InsertRequest(document.DocNum,EnumPearlStatus.Uploading);
			PearlRequest pearlRequestOther=PearlDb.InsertRequest(documentOther.DocNum,EnumPearlStatus.Uploading);
			PearlRequests.SetUploadFailed(document.DocNum);
			Assert.AreEqual(EnumPearlStatus.ErrorUploading,PearlRequests.GetOneByRequestId(pearlRequestUploading.RequestId).RequestStatus);
			Assert.AreEqual(EnumPearlStatus.Uploading,PearlRequests.GetOneByRequestId(pearlRequestOther.RequestId).RequestStatus);
			Document documentPolling=PearlDb.CreateDocument(patient);
			PearlRequest pearlRequestPolling=PearlDb.InsertRequest(documentPolling.DocNum,EnumPearlStatus.Polling);
			PearlRequests.SetUploadFailed(documentPolling.DocNum);
			Assert.AreEqual(EnumPearlStatus.Polling,PearlRequests.GetOneByRequestId(pearlRequestPolling.RequestId).RequestStatus);
		}
	}
}
