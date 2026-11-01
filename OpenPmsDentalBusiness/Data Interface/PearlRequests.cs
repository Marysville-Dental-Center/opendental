using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class PearlRequests{
		#region Methods - Get
		/*
		///<summary></summary>
		public static List<PearlRequest> Refresh(long patNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PearlRequest>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM pearlrequest WHERE PatNum = "+POut.Long(patNum);
			return Crud.PearlRequestCrud.SelectMany(command);
		}

		///<summary>Gets one PearlRequest from the db.</summary>
		public static PearlRequest GetOne(long pearlRequestNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<PearlRequest>(MethodBase.GetCurrentMethod(),pearlRequestNum);
			}
			return Crud.PearlRequestCrud.SelectOne(pearlRequestNum);
		}
		*/

		///<summary>Gets the most recently sent PearlRequest from the db that matches the given DocNum.</summary>
		public static PearlRequest GetOneByDocNum(long docNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<PearlRequest>(MethodBase.GetCurrentMethod(),docNum);
			}
			string command="SELECT * FROM pearlrequest WHERE DocNum="+POut.Long(docNum)+" ORDER BY DateTSent DESC";
			return Crud.PearlRequestCrud.SelectOne(command);
		}

		///<summary>Gets the most recently sent PearlRequest from the db that matches the given RequestId.</summary>
		public static PearlRequest GetOneByRequestId(string requestId){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<PearlRequest>(MethodBase.GetCurrentMethod(),requestId);
			}
			string command="SELECT * FROM pearlrequest WHERE RequestId=\'"+POut.String(requestId)+"\' ORDER BY DateTSent DESC";
			return Crud.PearlRequestCrud.SelectOne(command);
		}
		#endregion Methods - Get
		#region Methods - Modify
		///<summary></summary>
		public static long Insert(PearlRequest pearlRequest){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				pearlRequest.PearlRequestNum=Meth.GetLong(MethodBase.GetCurrentMethod(),pearlRequest);
				return pearlRequest.PearlRequestNum;
			}
			return Crud.PearlRequestCrud.Insert(pearlRequest);
		}

		///<summary></summary>
		public static void Update(PearlRequest pearlRequest){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),pearlRequest);
				return;
			}
			Crud.PearlRequestCrud.Update(pearlRequest);
		}

		///<summary></summary>
		public static void UpdateStatusForRequests(List<PearlRequest> listPearlRequests,EnumPearlStatus pearlStatus){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listPearlRequests,pearlStatus);
				return;
			}
			List<long> listPearlRequestNums=listPearlRequests.Select(x => x.PearlRequestNum).ToList();
			string command="UPDATE pearlrequest SET RequestStatus="+POut.Enum<EnumPearlStatus>(pearlStatus)
				+" WHERE PearlRequestNum IN ("+string.Join(",",listPearlRequestNums)+")";
			Db.NonQ(command);
		}

		///<summary>DEPRECATED. Sets RequestStatus to ServicePolling for each request matching a given DocNum.</summary>
		//public static void SwitchDocsToServerPolling(List<long> listDocNums){
		//	if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
		//		Meth.GetVoid(MethodBase.GetCurrentMethod(),listDocNums);
		//		return;
		//	}
		//	string command="UPDATE pearlrequest SET RequestStatus="+POut.Enum<EnumPearlStatus>(EnumPearlStatus.ServicePolling)
		//		+" WHERE DocNum IN ("+string.Join(",",listDocNums.Select(x => POut.Long(x)))+")";
		//	Db.NonQ(command);
		//}

		///<summary></summary>
		public static void Delete(long pearlRequestNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),pearlRequestNum);
				return;
			}
			Crud.PearlRequestCrud.Delete(pearlRequestNum);
		}
		///<summary></summary>
		public static void DeleteByDocNum(long docNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),docNum);
				return;
			}
			string command="DELETE FROM pearlrequest WHERE DocNum="+POut.Long(docNum);
			Db.NonQ(command);
		}
		#endregion Methods - Modify
		#region Methods - Misc
		///<summary>Returns whether the request has been processed by Pearl, either with success or error, so it is not polled any more: Received, Error,
		///and the Open Dental 24.4 statuses ErrorUploading, ErrorProcessing and NoResults. A status this program does not know counts as Error.
		///False for null, Polling, Uploading and TimedOut.</summary>
		public static bool IsRequestHandled(PearlRequest pearlRequest) {
			if(pearlRequest==null) {
				return false;
			}
			return GetKnownStatus(pearlRequest.RequestStatus).In(EnumPearlStatus.Received,EnumPearlStatus.Error,EnumPearlStatus.ErrorUploading,
				EnumPearlStatus.ErrorProcessing,EnumPearlStatus.NoResults);
		}

		#region V-PEARL1: Open Dental 24.4 request statuses
		///<summary>Returns the status as this program handles it. A value this program does not know, for example one written by a newer version of
		///Open Dental, is treated as Error: the image is not sent again.</summary>
		public static EnumPearlStatus GetKnownStatus(EnumPearlStatus pearlStatus) {
			Meth.NoCheckMiddleTierRole();
			if(Enum.IsDefined(typeof(EnumPearlStatus),pearlStatus)) {
				return pearlStatus;
			}
			return EnumPearlStatus.Error;
		}

		///<summary>True if the image has to be uploaded to Pearl: it was never sent (null), or its upload failed (ErrorUploading).</summary>
		public static bool NeedsUpload(PearlRequest pearlRequest) {
			Meth.NoCheckMiddleTierRole();
			if(pearlRequest==null) {
				return true;
			}
			return GetKnownStatus(pearlRequest.RequestStatus)==EnumPearlStatus.ErrorUploading;
		}

		///<summary>True if Pearl already has the image and the same request can be polled again without uploading the image: polling timed out
		///(TimedOut), or Pearl failed while processing it (ErrorProcessing).</summary>
		public static bool CanPollAgain(PearlRequest pearlRequest) {
			Meth.NoCheckMiddleTierRole();
			if(pearlRequest==null) {
				return false;
			}
			return GetKnownStatus(pearlRequest.RequestStatus).In(EnumPearlStatus.TimedOut,EnumPearlStatus.ErrorProcessing);
		}

		///<summary>True if the image of this request (null when the image has none) may be sent to Pearl: never sent, upload failed (uploaded again),
		///timed out or failed while processing (polled again). False while it is uploading or polling, and after it was received, rejected (Error)
		///or came back without results (NoResults).</summary>
		public static bool CanSendToPearl(PearlRequest pearlRequest) {
			Meth.NoCheckMiddleTierRole();
			return NeedsUpload(pearlRequest) || CanPollAgain(pearlRequest);
		}

		///<summary>True if the Pearl panel of the Imaging module shows this request's status as text. Received shows the annotation controls instead.
		///</summary>
		public static bool HasStatusToShow(PearlRequest pearlRequest) {
			Meth.NoCheckMiddleTierRole();
			return pearlRequest!=null && GetKnownStatus(pearlRequest.RequestStatus)!=EnumPearlStatus.Received;
		}

		///<summary>The status text in the Pearl panel of the Imaging module. Each status has its own text; an unknown status shows as Error.</summary>
		public static string GetStatusText(EnumPearlStatus pearlStatus) {
			Meth.NoCheckMiddleTierRole();
			switch(GetKnownStatus(pearlStatus)) {
				case EnumPearlStatus.Polling:
					return Lans.g("PearlRequests","Polling...");
				case EnumPearlStatus.Uploading:
					return Lans.g("PearlRequests","Uploading...");
				case EnumPearlStatus.Received:
					return Lans.g("PearlRequests","Results received.");
				case EnumPearlStatus.TimedOut:
					return Lans.g("PearlRequests","Timed out. Can be sent again.");
				case EnumPearlStatus.ErrorUploading:
					return Lans.g("PearlRequests","Upload failed. Can be sent again.");
				case EnumPearlStatus.ErrorProcessing:
					return Lans.g("PearlRequests","Processing failed. Can be sent again.");
				case EnumPearlStatus.NoResults:
					return Lans.g("PearlRequests","No results for this image.");
				case EnumPearlStatus.Error:
				default:
					return Lans.g("PearlRequests","Pearl rejected this image.");
			}
		}

		///<summary>Why the image of this request is not sent to Pearl again, for the message shown when the user sends it anyway. Blank if it can be sent.
		///</summary>
		public static string GetCannotSendText(PearlRequest pearlRequest) {
			Meth.NoCheckMiddleTierRole();
			if(CanSendToPearl(pearlRequest)) {
				return "";
			}
			switch(GetKnownStatus(pearlRequest.RequestStatus)) {
				case EnumPearlStatus.Polling:
				case EnumPearlStatus.Uploading:
					return Lans.g("PearlRequests","This image is still being processed by Pearl.");
				case EnumPearlStatus.NoResults:
					return Lans.g("PearlRequests","Pearl returned no results for this image. It is not sent again.");
				case EnumPearlStatus.Error:
					return Lans.g("PearlRequests","Pearl rejected this image. It is not sent again.");
				case EnumPearlStatus.Received:
				default:
					return Lans.g("PearlRequests","This image was already sent to Pearl.");
			}
		}

		///<summary>The status for a result that Pearl marked complete: NoResults when it holds no annotations and no tooth parts, Received otherwise.
		///</summary>
		public static EnumPearlStatus GetCompletedStatus(OpenPmsDentalBusiness.Pearl.Result result) {
			Meth.NoCheckMiddleTierRole();
			if(result==null || (result.annotations.IsNullOrEmpty() && result.toothParts.IsNullOrEmpty())) {
				return EnumPearlStatus.NoResults;
			}
			return EnumPearlStatus.Received;
		}

		///<summary>Called by the sending thread before it uploads an image, with the image's latest request (null if none). Returns the RequestId to
		///poll again when the request timed out or failed while processing; its status is set back to Polling and the image is not uploaded again.
		///Returns null when the image is to be uploaded: it was never sent, or its upload failed (that request is deleted; the new upload makes a new
		///one with a new RequestId). Throws if the image was already sent and is not sent again.</summary>
		public static string PrepareToSend(PearlRequest pearlRequest) {
			Meth.NoCheckMiddleTierRole();
			if(CanPollAgain(pearlRequest)) {
				UpdateStatusForRequests(new List<PearlRequest> { pearlRequest },EnumPearlStatus.Polling);
				pearlRequest.RequestStatus=EnumPearlStatus.Polling;
				return pearlRequest.RequestId;
			}
			if(!NeedsUpload(pearlRequest)) {
				throw new Exception("Image was already sent to Pearl");
			}
			if(pearlRequest!=null) {
				Delete(pearlRequest.PearlRequestNum);//The failed upload.
			}
			return null;
		}

		///<summary>Sets the image's requests that are still Uploading to ErrorUploading, after the upload failed, so the image can be sent again.
		///</summary>
		public static void SetUploadFailed(long docNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),docNum);
				return;
			}
			string command="UPDATE pearlrequest SET RequestStatus="+POut.Enum<EnumPearlStatus>(EnumPearlStatus.ErrorUploading)
				+" WHERE DocNum="+POut.Long(docNum)+" AND RequestStatus="+POut.Enum<EnumPearlStatus>(EnumPearlStatus.Uploading);
			Db.NonQ(command);
		}
		#endregion V-PEARL1: Open Dental 24.4 request statuses

		#endregion Methods - Misc

	}
}