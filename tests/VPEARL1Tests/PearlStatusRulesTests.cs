// 24.4 v24_4-imaging-pearl-request-statuses: which Pearl requests are finished, which images are sent again (uploaded again or polled again),
// the status text of the Imaging module, and how a value this program does not know is read. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Pearl;
using Result=OpenPmsDentalBusiness.Pearl.Result;

namespace VPEARL1Tests {
	[TestClass]
	public class PearlStatusRulesTests {
		///<summary>A value no version of Open Dental published up to 26.1, as a newer version might store it.</summary>
		private const EnumPearlStatus UNKNOWN=(EnumPearlStatus)9;

		private static PearlRequest Request(EnumPearlStatus pearlStatus) {
			return new PearlRequest { RequestId="vpearl1-"+(int)pearlStatus,RequestStatus=pearlStatus };
		}

		private static List<EnumPearlStatus> AllWithUnknown() {
			List<EnumPearlStatus> listPearlStatuses=Enum.GetValues(typeof(EnumPearlStatus)).Cast<EnumPearlStatus>().ToList();
			listPearlStatuses.Add(UNKNOWN);
			listPearlStatuses.Add((EnumPearlStatus)255);
			return listPearlStatuses;
		}

		[TestMethod]
		public void Statuses_HaveThePublishedNumbers() {
			Assert.AreEqual(0,(int)EnumPearlStatus.Polling);
			Assert.AreEqual(1,(int)EnumPearlStatus.Received);
			Assert.AreEqual(2,(int)EnumPearlStatus.Error);
			Assert.AreEqual(3,(int)EnumPearlStatus.TimedOut);
			Assert.AreEqual(4,(int)EnumPearlStatus.Uploading);
			Assert.AreEqual(5,(int)EnumPearlStatus.ErrorUploading);
			Assert.AreEqual(6,(int)EnumPearlStatus.ErrorProcessing);
			Assert.AreEqual(7,(int)EnumPearlStatus.NoResults);
			Assert.AreEqual(8,Enum.GetValues(typeof(EnumPearlStatus)).Length);
		}

		[TestMethod]
		public void UnknownStatus_IsReadAsError() {
			Assert.AreEqual(EnumPearlStatus.Error,PearlRequests.GetKnownStatus(UNKNOWN));
			Assert.AreEqual(EnumPearlStatus.Error,PearlRequests.GetKnownStatus((EnumPearlStatus)255));
			Assert.AreEqual(EnumPearlStatus.Error,PearlRequests.GetKnownStatus((EnumPearlStatus)(-1)));
			foreach(EnumPearlStatus pearlStatus in Enum.GetValues(typeof(EnumPearlStatus))) {
				Assert.AreEqual(pearlStatus,PearlRequests.GetKnownStatus(pearlStatus),"Known values are kept.");
			}
		}

		[TestMethod]
		public void IsRequestHandled_ReceivedAndEveryErrorStopPolling() {
			Assert.IsFalse(PearlRequests.IsRequestHandled(null));
			Assert.IsFalse(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.Polling)));
			Assert.IsFalse(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.Uploading)));
			Assert.IsFalse(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.TimedOut)));
			Assert.IsTrue(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.Received)));
			Assert.IsTrue(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.Error)));
			Assert.IsTrue(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.ErrorUploading)));
			Assert.IsTrue(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.ErrorProcessing)));
			Assert.IsTrue(PearlRequests.IsRequestHandled(Request(EnumPearlStatus.NoResults)));
			Assert.IsTrue(PearlRequests.IsRequestHandled(Request(UNKNOWN)),"Unknown counts as Error.");
		}

		[TestMethod]
		public void SendAgain_OnlyAfterFailedUploadTimeoutOrProcessingFailure() {
			//Upload (again): never sent, or the upload failed.
			Assert.IsTrue(PearlRequests.NeedsUpload(null));
			Assert.IsTrue(PearlRequests.NeedsUpload(Request(EnumPearlStatus.ErrorUploading)));
			//Poll again with the same request ID: Pearl has the image.
			Assert.IsTrue(PearlRequests.CanPollAgain(Request(EnumPearlStatus.TimedOut)));
			Assert.IsTrue(PearlRequests.CanPollAgain(Request(EnumPearlStatus.ErrorProcessing)));
			Assert.IsFalse(PearlRequests.CanPollAgain(null));
			Assert.IsFalse(PearlRequests.CanPollAgain(Request(EnumPearlStatus.ErrorUploading)),"A failed upload is uploaded again, not polled.");
			foreach(EnumPearlStatus pearlStatus in AllWithUnknown()) {
				PearlRequest pearlRequest=Request(pearlStatus);
				bool isSendable=pearlStatus.In(EnumPearlStatus.ErrorUploading,EnumPearlStatus.TimedOut,EnumPearlStatus.ErrorProcessing);
				Assert.AreEqual(isSendable,PearlRequests.CanSendToPearl(pearlRequest),pearlStatus.ToString());
				Assert.IsFalse(PearlRequests.NeedsUpload(pearlRequest) && PearlRequests.CanPollAgain(pearlRequest),"Never both: "+pearlStatus);
				Assert.AreEqual(isSendable,PearlRequests.GetCannotSendText(pearlRequest)=="",pearlStatus.ToString());
			}
			Assert.IsTrue(PearlRequests.CanSendToPearl(null),"An image never sent can be sent.");
			Assert.AreEqual("",PearlRequests.GetCannotSendText(null));
		}

		[TestMethod]
		public void StatusText_TellsTheCasesApart() {
			List<EnumPearlStatus> listPearlStatuses=Enum.GetValues(typeof(EnumPearlStatus)).Cast<EnumPearlStatus>().ToList();
			List<string> listTexts=listPearlStatuses.Select(x => PearlRequests.GetStatusText(x)).ToList();
			Assert.AreEqual(listTexts.Count,listTexts.Distinct().Count(),"Each status has its own text: "+string.Join(" | ",listTexts));
			Assert.IsTrue(listTexts.All(x => !string.IsNullOrWhiteSpace(x)));
			Assert.AreEqual("Polling...",PearlRequests.GetStatusText(EnumPearlStatus.Polling),"Same text as before 24.4.");
			Assert.AreEqual("Uploading...",PearlRequests.GetStatusText(EnumPearlStatus.Uploading),"Same text as before 24.4.");
			Assert.AreEqual(PearlRequests.GetStatusText(EnumPearlStatus.Error),PearlRequests.GetStatusText(UNKNOWN));
			//The statuses that can be sent again say so; the others do not.
			foreach(EnumPearlStatus pearlStatus in listPearlStatuses) {
				bool saysSendAgain=PearlRequests.GetStatusText(pearlStatus).Contains("sent again");
				Assert.AreEqual(PearlRequests.CanSendToPearl(Request(pearlStatus)),saysSendAgain,pearlStatus.ToString());
			}
		}

		[TestMethod]
		public void CannotSendText_SaysWhy() {
			string textProcessing=PearlRequests.GetCannotSendText(Request(EnumPearlStatus.Polling));
			Assert.AreEqual(textProcessing,PearlRequests.GetCannotSendText(Request(EnumPearlStatus.Uploading)));
			string textReceived=PearlRequests.GetCannotSendText(Request(EnumPearlStatus.Received));
			Assert.AreEqual("This image was already sent to Pearl.",textReceived,"Same text as before 24.4.");
			string textNoResults=PearlRequests.GetCannotSendText(Request(EnumPearlStatus.NoResults));
			string textError=PearlRequests.GetCannotSendText(Request(EnumPearlStatus.Error));
			List<string> listTexts=new List<string> { textProcessing,textReceived,textNoResults,textError };
			Assert.AreEqual(4,listTexts.Distinct().Count());
			Assert.AreEqual(textError,PearlRequests.GetCannotSendText(Request(UNKNOWN)));
		}

		[TestMethod]
		public void HasStatusToShow_EverythingButReceived() {
			Assert.IsFalse(PearlRequests.HasStatusToShow(null),"No request: the panel stays hidden.");
			Assert.IsFalse(PearlRequests.HasStatusToShow(Request(EnumPearlStatus.Received)),"Received shows the annotation controls.");
			foreach(EnumPearlStatus pearlStatus in AllWithUnknown().Where(x => x!=EnumPearlStatus.Received)) {
				Assert.IsTrue(PearlRequests.HasStatusToShow(Request(pearlStatus)),pearlStatus.ToString());
			}
		}

		[TestMethod]
		public void CompletedResult_WithoutFindings_IsNoResults() {
			Assert.AreEqual(EnumPearlStatus.NoResults,PearlRequests.GetCompletedStatus(new Result { is_completed=true }));
			Assert.AreEqual(EnumPearlStatus.NoResults,PearlRequests.GetCompletedStatus(
				new Result { is_completed=true,annotations=new Annotation[0],toothParts=new ToothPart[0] }));
			Assert.AreEqual(EnumPearlStatus.NoResults,PearlRequests.GetCompletedStatus(null));
			Assert.AreEqual(EnumPearlStatus.Received,PearlRequests.GetCompletedStatus(FakePearlHttp.Completed("x",hasFindings:true).result));
			Assert.AreEqual(EnumPearlStatus.Received,PearlRequests.GetCompletedStatus(
				new Result { is_completed=true,annotations=new Annotation[] { new Annotation { category_id=EnumCategory.Caries } } }));
		}

		[TestMethod]
		public void PrepareToSend_RefusesWhatWasAlreadySent() {
			//These throw before touching the database.
			foreach(EnumPearlStatus pearlStatus in new[] { EnumPearlStatus.Polling,EnumPearlStatus.Uploading,EnumPearlStatus.Received,
				EnumPearlStatus.Error,EnumPearlStatus.NoResults,UNKNOWN })
			{
				Exception exception=null;
				try {
					PearlRequests.PrepareToSend(Request(pearlStatus));
				}
				catch(Exception ex) {
					exception=ex;
				}
				Assert.IsNotNull(exception,pearlStatus.ToString());
				Assert.AreEqual("Image was already sent to Pearl",exception.Message);
			}
			Assert.IsNull(PearlRequests.PrepareToSend(null),"Never sent: upload.");
		}
	}
}
