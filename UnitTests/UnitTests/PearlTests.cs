using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Pearl;
using UnitTestsCore;

namespace UnitTests {
	[TestClass]
	public class PearlTests:TestBase {
		///<summary>This method will execute only once, just before any tests in this class run.</summary>
		[ClassInitialize]
		public static void SetupClass(TestContext testContext) {
		}

		///<summary>This method will execute just before each test in this class.</summary>
		[TestInitialize]
		public void SetupTest() {
			PearlApiClient.Inst=new PearlApiClientMock();
		}

		///<summary>This method will execute after each test in this class.</summary>
		[TestCleanup]
		public void TearDownTest() {
		}

		///<summary>This method will execute only once, just after all tests in this class have run.
		///However, this method is not guaranteed to execute before starting another TestMethod from another TestClass.</summary>
		[ClassCleanup]
		public static void TearDownClass() {
		}

		///<summary>Sends 1 image and asserts various result paths returned when api auth/upload fails. Also tests when nothing fails.</summary>
		[TestMethod]
		public void Pearl_SendOneImageToPearl_HappyPath() {
			void assert(PearlApiClientMock pearlApiClientMock,bool doSucceed) {
				PearlApiClient.Inst=pearlApiClientMock;
				Patient pat=PatientT.CreatePatient(fName: "Jerry");
				Document document=new Document();
				long docNum=Documents.Insert(document,pat);
				OpenPmsDentalBusiness.Bridges.Pearl pearl=new OpenPmsDentalBusiness.Bridges.Pearl();
				pearl.Bitmap_=new Bitmap(40,40);
				pearl.Document_=document;
				pearl.Patient_=pat;
				Exception exceptionThread=null;
				ODThread oDThread=new ODThread(pearl.SendOnThreadWorker);
				oDThread.AddExceptionHandler((e) => exceptionThread=e);
				oDThread.Start();
				if(!oDThread.Join(timeoutMS: 1_000)) {
					Assert.Fail("Pearl.SendOnThread failed to complete in time");
				}
				PearlRequest pearlRequest=PearlRequests.GetOneByDocNum(docNum);
				//Request will be not null if we succeed, null if we fail.
				Assert.IsTrue(doSucceed ? pearlRequest!=null : pearlRequest==null);
				//A failed send is reported to the thread, which raises the Pearl alert in the program: Pearl.SendOnThreadWorker rethrows the upload
				//error ("There was an error uploading an image", as Open Dental 24.3 does; V-PEARL1 also marks a row that was already inserted
				//ErrorUploading). This test still expected every error to be swallowed. A send that works throws nothing.
				if(doSucceed) {
					Assert.IsNull(exceptionThread,exceptionThread?.ToString());
				}
				else {
					Assert.IsNotNull(exceptionThread);
					StringAssert.Contains(exceptionThread.Message,"There was an error uploading an image");
				}
			}
			//Mock each error type. They should all respond the same.
			assert(new PearlApiClientMock() { SucceedGetAuthToken=false },false);
			assert(new PearlApiClientMock() { SucceedGetAwsPresignedUrlInfo=false },false);
			assert(new PearlApiClientMock() { SucceedUploadToAwsPresignedURL=false },false);
			//SucceedGetImageByRequestId gets it own test.
			//No error means outputs are valid.
			assert(new PearlApiClientMock(),true);
		}

		///<summary>Sends 1 image and asserts various result paths returned from GetOneImageFromPearl.</summary>
		[TestMethod]
		public void Pearl_PollSingleRequest() {
			void assert(Func<string,string,ImageRequestIdResponse> funcGetImageByRequestIdOutput,EnumPearlStatus pearlStatus,bool isExceptionExpected=false) {
				PearlApiClient.Inst=new PearlApiClientMock() { FuncGetImageByRequestIdOutput=funcGetImageByRequestIdOutput };
				Patient pat=PatientT.CreatePatient(fName: "Jerry");
				Document document=new Document();
				long docNum=Documents.Insert(document,pat);
				OpenPmsDentalBusiness.Bridges.Pearl pearl=new OpenPmsDentalBusiness.Bridges.Pearl();
				pearl.Bitmap_=new Bitmap(40,40);
				pearl.Document_=document;
				pearl.Patient_=pat;
				bool threwException=false;
				ODThread oDThread=new ODThread(pearl.SendOnThreadWorker);
				oDThread.AddExceptionHandler((e) => threwException=true);
				oDThread.Start();
				//If we did not mock in any result that means we are still polling and the short timeout is expected.
				if(!oDThread.Join(timeoutMS: 500) && pearlStatus!=EnumPearlStatus.Polling) {
					Assert.Fail("Pearl.SendOnThread failed to complete in time");
				}
				
				PearlRequest pearlRequest=PearlRequests.GetOneByDocNum(docNum);
				//Request will be not null if we succeed, null if we fail.
				Assert.IsTrue(pearlRequest!=null);
				Assert.AreEqual(pearlStatus,pearlRequest.RequestStatus);
				//Pearl should handle all exceptions, no matter if a good or bad pearlRequest. No exceptions should bubble up to thread.
				//V-PEARL1: except a failed poll, which ends polling as ErrorProcessing and is reported to the thread (an alert in the program).
				Assert.AreEqual(isExceptionExpected,threwException);
			}
			//Mock each status.
			OpenPmsDentalBusiness.Pearl.ToothPart toothPart=new OpenPmsDentalBusiness.Pearl.ToothPart() {
				condition_id=OpenPmsDentalBusiness.Pearl.EnumCondition.Enamel,
				polygon=new Polygon[] { new Polygon() { x=0.1f,y=0.1f },new Polygon() { x=0.5f,y=0.1f },new Polygon() { x=0.5f,y=0.5f } },
			};
			assert((a,b) => new ImageRequestIdResponse() { result=new OpenPmsDentalBusiness.Pearl.Result() { is_completed=true,annotations=new Annotation[0],toothParts=new[] { toothPart } } },EnumPearlStatus.Received );
			//V-PEARL1 (24.4): complete, but nothing found.
			assert((a,b) => new ImageRequestIdResponse() { result=new OpenPmsDentalBusiness.Pearl.Result() { is_completed=true } },EnumPearlStatus.NoResults);
			assert((a,b) => new ImageRequestIdResponse() { result=new OpenPmsDentalBusiness.Pearl.Result() { is_rejected=true } },EnumPearlStatus.Error);
			assert((a,b) => new ImageRequestIdResponse() { result=new OpenPmsDentalBusiness.Pearl.Result() { is_deleted=true } },EnumPearlStatus.Error);
			//API returned a result but is not completed and includes no other state. We should still be polling.
			assert((a,b) => new ImageRequestIdResponse() { result=new OpenPmsDentalBusiness.Pearl.Result() {  } },EnumPearlStatus.Polling);
			//API threw an exception. V-PEARL1 (24.4): polling stops as ErrorProcessing, which can be polled again.
			assert((a,b) => null,EnumPearlStatus.ErrorProcessing,isExceptionExpected:true);

		}

		//Deprecated. No longer sending multiple images on a single thread.
		///<summary>Sends multi images and asserts various result paths.</summary>
		//[TestMethod]
		//public void Pearl_SendMultiImagesToPearl() {
		//	void assert(bool doSucceed) {
		//		Patient pat=PatientT.CreatePatient(fName: "Jerry");
		//		//Set up document / mountItem #1
		//		MountItem mountItem=new MountItem { MountNum=1 };
		//		mountItem.MountItemNum=MountItems.Insert(mountItem);
		//		Document document1=new Document();
		//		document1.MountItemNum=mountItem.MountItemNum;
		//		document1.DocNum=Documents.Insert(document1,pat);
		//		//Set up document / mountItem #2
		//		MountItem mountItem2=new MountItem() { MountNum=1 };
		//		mountItem2.MountItemNum=MountItems.Insert(mountItem2);
		//		Document document2=new Document();
		//		document2.MountItemNum=mountItem2.MountItemNum;
		//		document2.DocNum=Documents.Insert(document2,pat);
		//		//Create successful PearlRequest for document #2
		//		if(!doSucceed) { //When we simulate api failure, mock in doc2 result. This will cause us to use API (and fail) doc1, but skip api for doc2 (success).
		//			PearlRequests.Insert(new PearlRequest() { DocNum=document2.DocNum,RequestStatus=EnumPearlStatus.Received });
		//		}
		//		//Throw an exception in UploadToAwsPresignedUrl() if simulating failure.
		//		((PearlApiClientMock)PearlApiClient.Inst).SucceedUploadToAwsPresignedURL=doSucceed;
		//		List<Bitmap> listBitmaps=new List<Bitmap>();
		//		listBitmaps.Add(new Bitmap(40,40));
		//		listBitmaps.Add(new Bitmap(40,40));
		//		OpenPmsDentalBusiness.Bridges.Pearl pearl=new OpenPmsDentalBusiness.Bridges.Pearl();
		//		pearl.ListBitmaps=listBitmaps;
		//		pearl.DocNum=0;
		//		pearl.MountNum=1;
		//		pearl.ListMountItems=new List<MountItem> { mountItem,mountItem2 };
		//		pearl.Patient_=pat;
		//		bool threwException=false;
		//		ODThread oDThread=new ODThread(pearl.SendOnThreadWorker);
		//		oDThread.AddExceptionHandler((e) => threwException=true);
		//		oDThread.Start();
		//		if(!oDThread.Join(timeoutMS: 1_000)) {
		//			Assert.Fail("Pearl.SendOnThread failed to complete in time");
		//		}
		//		//Document 1 should only be created if api failed. Document 2 will always exist since we mocked it in above.
		//		PearlRequest pearlRequest1=PearlRequests.GetOneByDocNum(document1.DocNum);
		//		PearlRequest pearlRequest2=PearlRequests.GetOneByDocNum(document2.DocNum);
		//		//Request 1 will be not null if we succeed, null if we fail.
		//		Assert.IsTrue(doSucceed ? pearlRequest1!=null : pearlRequest1==null);
		//		//Request 2 will always exist since we mocked it in above.
		//		Assert.IsNotNull(pearlRequest2);
		//		//Status was mocked in above, it should match fresh instance from db.
		//		Assert.IsTrue(pearlRequest2.RequestStatus==EnumPearlStatus.Received);
		//		//Pearl should handle all exceptions, no matter if a good or bad pearlRequest. No exceptions should bubble up to thread.
		//		Assert.IsFalse(threwException);
		//	}
		//	assert(true);
		//	assert(false);
		//}
	}
}
