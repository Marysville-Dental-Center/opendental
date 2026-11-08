// Open Dental 26.2 (P262-GLIDEWELL): sending a lab case to Glidewell from the Edit Lab Case window, for a scan from an iTero or Medit scanner,
// and checking its status. The rules here are what Open Dental's 26.2 release notes and database documents state; the rest is INFERRED and listed
// in docs/port-notes/P262-GLIDEWELL.md. The call itself goes through IGlidewellClient (GlidewellClients.Current), which in this build sends nothing.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CodeBase;

namespace OpenPmsDentalBusiness.VendorDirect.Glidewell {
	///<summary>What sending a lab case or checking its status gave.</summary>
	public sealed class GlidewellLabCaseResult {
		public bool IsSuccess { get; }
		///<summary>True when nothing was sent because this build cannot reach Glidewell.</summary>
		public bool IsBlocked { get; }
		///<summary>For the user.</summary>
		public string Message { get; }
		///<summary>The lab case's glidewellmsg row after the call (null when sending failed).</summary>
		public GlidewellMsg GlidewellMsg { get; }

		public GlidewellLabCaseResult(bool isSuccess,bool isBlocked,string message,GlidewellMsg glidewellMsg) {
			IsSuccess=isSuccess;
			IsBlocked=isBlocked;
			Message=message??"";
			GlidewellMsg=glidewellMsg;
		}

		public static GlidewellLabCaseResult Refused(string message,GlidewellMsg glidewellMsg=null) {
			return new GlidewellLabCaseResult(false,false,message,glidewellMsg);
		}
	}

	///<summary>Sends lab cases to Glidewell and checks their status. One glidewellmsg row per lab case, inserted on the first send and only updated
	///afterwards (no history), as Open Dental's 26.2 documents describe. The account a case was sent with is kept in the row; checking status uses
	///that account, never the provider's default (documents: "We never look back to provider.GlidewellAcctNum").</summary>
	public static class GlidewellLabCases {
		public const string VENDOR_NAME_ITERO="iTero";
		public const string VENDOR_NAME_MEDIT="Medit";

		///<summary>The request's VendorName for the scanner.</summary>
		public static string GetVendorName(GlidewellScanner scanner) {
			Meth.NoCheckMiddleTierRole();
			return scanner==GlidewellScanner.Medit ? VENDOR_NAME_MEDIT : VENDOR_NAME_ITERO;
		}

		///<summary>The scanner a stored row was sent for: Medit when only SentMedit is set, otherwise iTero.</summary>
		public static GlidewellScanner GetScanner(GlidewellMsg glidewellMsg) {
			Meth.NoCheckMiddleTierRole();
			if(glidewellMsg!=null && glidewellMsg.SentMedit && !glidewellMsg.SentITero) {
				return GlidewellScanner.Medit;
			}
			return GlidewellScanner.ITero;
		}

		#region Checks
		///<summary>Why the lab case cannot be sent now, one line each; empty when it can. Covers the program link, the one-row-per-lab-case rule, the
		///account and its key (the office's own credentials), the required patient names and the client. Reads nothing from the database.</summary>
		public static List<string> GetSendProblems(LabCase labCase,Patient patient,GlidewellAcct glidewellAcct,GlidewellMsg glidewellMsgExisting,
			IGlidewellClient glidewellClient)
		{
			Meth.NoCheckMiddleTierRole();
			List<string> listProblems=new List<string>();
			if(!GlidewellProgram.IsEnabled()) {
				listProblems.Add(Lans.g("Glidewell","The Glidewell program link is turned off (Setup, Program Links)."));
			}
			if(labCase==null || labCase.LabCaseNum==0) {
				listProblems.Add(Lans.g("Glidewell","Save the lab case first."));
			}
			if(glidewellMsgExisting!=null) {
				listProblems.Add(Lans.g("Glidewell","This lab case was already sent to Glidewell. Use Check Status."));
			}
			listProblems.AddRange(GetAccountProblems(glidewellAcct));
			if(patient==null) {
				listProblems.Add(Lans.g("Glidewell","The lab case has no valid patient."));
			}
			else if(string.IsNullOrWhiteSpace(patient.FName) || string.IsNullOrWhiteSpace(patient.LName)) {
				listProblems.Add(Lans.g("Glidewell","Glidewell requires the patient's first and last name."));
			}
			if(glidewellClient==null || !glidewellClient.IsAvailable) {
				listProblems.Add(glidewellClient?.UnavailableReason??new GlidewellClientBlocked().UnavailableReason);
			}
			return listProblems;
		}

		///<summary>Why the account cannot be used: none chosen, no account number, or no readable API key.</summary>
		public static List<string> GetAccountProblems(GlidewellAcct glidewellAcct) {
			Meth.NoCheckMiddleTierRole();
			List<string> listProblems=new List<string>();
			if(glidewellAcct==null) {
				listProblems.Add(Lans.g("Glidewell","Pick the Glidewell account to send with."));
				return listProblems;
			}
			if(string.IsNullOrWhiteSpace(glidewellAcct.AccountNumber)) {
				listProblems.Add(Lans.g("Glidewell","The Glidewell account has no account number."));
			}
			SecretReadResult secretReadResult=GlidewellAccts.ReadApiKey(glidewellAcct);
			if(secretReadResult.HasValue) {
				return listProblems;
			}
			string accountName=string.IsNullOrWhiteSpace(glidewellAcct.AccountNumber) ? "" : " "+glidewellAcct.AccountNumber.Trim();
			if(secretReadResult.Status==SecretReadStatus.Blank) {
				listProblems.Add(Lans.g("Glidewell","Enter the API key of Glidewell account")+accountName+" "
					+Lans.g("Glidewell","in Setup, Program Links, Glidewell lab cases."));
			}
			else {
				listProblems.Add(Lans.g("Glidewell","The API key of Glidewell account")+accountName+" "
					+Lans.g("Glidewell","cannot be read here. Enter it again in Setup, Program Links, Glidewell lab cases."));
			}
			return listProblems;
		}
		#endregion Checks

		#region Request
		///<summary>The register request for the lab case. Notes are the lab case's instructions followed by one line per product (the request has no
		///product field; INFERRED).</summary>
		public static GlidewellRegisterRequest BuildRegisterRequest(LabCase labCase,Patient patient,Provider provider,GlidewellScanner scanner,
			List<LabCaseProduct> listLabCaseProducts)
		{
			Meth.NoCheckMiddleTierRole();
			GlidewellRegisterRequest request=new GlidewellRegisterRequest();
			request.CreateVendorOrder=true;
			request.VendorName=GetVendorName(scanner);
			request.OrderId=labCase.LabCaseNum;
			request.DoctorId=labCase.ProvNum;
			request.LicenseNumber=(provider?.StateLicense??"").Trim();
			request.PatientFirstName=(patient.FName??"").Trim();
			request.PatientLastName=(patient.LName??"").Trim();
			request.PatientId=patient.PatNum;
			request.PatientGender=GetGenderText(patient.Gender);
			request.PatientDateOfBirth=patient.Birthdate.Year>1880 ? patient.Birthdate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) : null;
			request.Notes=BuildNotes(labCase.Instructions,listLabCaseProducts);
			return request;
		}

		///<summary>"Male", "Female", "Other" or "Unknown" (INFERRED: Glidewell's gender values are not published).</summary>
		public static string GetGenderText(PatientGender patientGender) {
			Meth.NoCheckMiddleTierRole();
			switch(patientGender) {
				case PatientGender.Male:
					return "Male";
				case PatientGender.Female:
					return "Female";
				case PatientGender.Other:
					return "Other";
				default:
					return "Unknown";
			}
		}

		///<summary>The instructions, then "Products:" and one "- " line per product that has anything in it. Line breaks are \n.</summary>
		public static string BuildNotes(string instructions,List<LabCaseProduct> listLabCaseProducts) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			string instructionsTrimmed=(instructions??"").Replace("\r\n","\n").Replace("\r","\n").Trim();
			stringBuilder.Append(instructionsTrimmed);
			List<string> listLines=(listLabCaseProducts??new List<LabCaseProduct>())
				.Select(x => LabCaseProducts.GetSummary(x))
				.Where(x => x!="")
				.ToList();
			if(listLines.Count==0) {
				return stringBuilder.ToString();
			}
			if(stringBuilder.Length>0) {
				stringBuilder.Append("\n");
			}
			stringBuilder.Append("Products:");
			for(int i=0;i<listLines.Count;i++) {
				stringBuilder.Append("\n- "+listLines[i]);
			}
			return stringBuilder.ToString();
		}
		#endregion Request

		#region Send and status
		///<summary>Sends the lab case to Glidewell with the chosen account, for the chosen scanner. Saves nothing unless Glidewell accepts it; then
		///inserts the lab case's glidewellmsg row (request, response, status, VendorRxId, account, scanner). The lab case and its products must be
		///saved first. glidewellClient null uses GlidewellClients.Current.</summary>
		public static GlidewellLabCaseResult Send(long labCaseNum,long glidewellAcctNum,GlidewellScanner scanner,IGlidewellClient glidewellClient=null) {
			Meth.NoCheckMiddleTierRole();
			glidewellClient=glidewellClient??GlidewellClients.Current;
			LabCase labCase=LabCases.GetOne(labCaseNum);
			Patient patient=(labCase==null) ? null : Patients.GetPat(labCase.PatNum);
			GlidewellAcct glidewellAcct=GlidewellAccts.GetOne(glidewellAcctNum);
			GlidewellMsg glidewellMsgExisting=GlidewellMsgs.GetForLabCase(labCaseNum);
			List<string> listProblems=GetSendProblems(labCase,patient,glidewellAcct,glidewellMsgExisting,glidewellClient);
			if(listProblems.Count>0) {
				bool isBlocked=(glidewellClient==null || !glidewellClient.IsAvailable) && listProblems.Count==1;
				return new GlidewellLabCaseResult(false,isBlocked,string.Join("\r\n",listProblems),glidewellMsgExisting);
			}
			Provider provider=Providers.GetProv(labCase.ProvNum);
			List<LabCaseProduct> listLabCaseProducts=LabCaseProducts.GetForLabCase(labCaseNum);
			GlidewellRegisterRequest request=BuildRegisterRequest(labCase,patient,provider,scanner,listLabCaseProducts);
			string requestJson=request.ToJson();
			GlidewellCredentials credentials=new GlidewellCredentials(glidewellAcct.AccountNumber,GlidewellAccts.ReadApiKey(glidewellAcct).PlainText);
			GlidewellCallResult callResult=Call(() => glidewellClient.Register(credentials,requestJson));
			if(callResult.Outcome==GlidewellCallOutcome.Blocked) {
				return new GlidewellLabCaseResult(false,true,callResult.ErrorMessage,null);
			}
			if(!callResult.IsSuccess) {
				return GlidewellLabCaseResult.Refused(Lans.g("Glidewell","Glidewell did not accept the lab case. Nothing was saved.")+"\r\n"
					+callResult.ErrorMessage);
			}
			GlidewellResponse response=GlidewellResponse.Parse(callResult.ResponseJson);
			GlidewellMsg glidewellMsg=new GlidewellMsg();
			glidewellMsg.LabCaseNum=labCase.LabCaseNum;
			glidewellMsg.PatNum=labCase.PatNum;
			glidewellMsg.GlidewellAcctNum=glidewellAcct.GlidewellAcctNum;
			glidewellMsg.RawJsonRequest=requestJson;
			glidewellMsg.RawJsonResponse=callResult.ResponseJson;
			glidewellMsg.VendorRxId=response.VendorRxId;
			glidewellMsg.SentITero=(scanner==GlidewellScanner.ITero);
			glidewellMsg.SentMedit=(scanner==GlidewellScanner.Medit);
			//A registered case awaits its scan (documents: Initialized), unless Glidewell already says otherwise.
			EnumGlidewellOrderStatus orderStatus;
			if(!GlidewellOrderStatusText.TryParse(response.StatusText,out orderStatus)) {
				orderStatus=EnumGlidewellOrderStatus.Initialized;
			}
			glidewellMsg.OrderStatus=orderStatus;
			GlidewellMsgs.Insert(glidewellMsg);
			string message=Lans.g("Glidewell","The lab case was sent to Glidewell.")+" "+GetScanInstruction(glidewellMsg);
			return new GlidewellLabCaseResult(true,false,message,glidewellMsg);
		}

		///<summary>Asks Glidewell for the lab case's current status with the account it was sent with, and updates its row: the most recent response
		///always, the status when Glidewell names one this program knows. Writes nothing when the call fails.</summary>
		public static GlidewellLabCaseResult CheckStatus(long labCaseNum,IGlidewellClient glidewellClient=null) {
			Meth.NoCheckMiddleTierRole();
			glidewellClient=glidewellClient??GlidewellClients.Current;
			GlidewellMsg glidewellMsg=GlidewellMsgs.GetForLabCase(labCaseNum);
			if(glidewellMsg==null) {
				return GlidewellLabCaseResult.Refused(Lans.g("Glidewell","This lab case was not sent to Glidewell."));
			}
			if(!GlidewellProgram.IsEnabled()) {
				return GlidewellLabCaseResult.Refused(Lans.g("Glidewell","The Glidewell program link is turned off (Setup, Program Links)."),glidewellMsg);
			}
			if(glidewellClient==null || !glidewellClient.IsAvailable) {
				return new GlidewellLabCaseResult(false,true,glidewellClient?.UnavailableReason??new GlidewellClientBlocked().UnavailableReason,glidewellMsg);
			}
			GlidewellAcct glidewellAcct=GlidewellAccts.GetOne(glidewellMsg.GlidewellAcctNum);
			if(glidewellAcct==null) {
				return GlidewellLabCaseResult.Refused(Lans.g("Glidewell","The Glidewell account this lab case was sent with no longer exists."),glidewellMsg);
			}
			List<string> listAccountProblems=GetAccountProblems(glidewellAcct);
			if(listAccountProblems.Count>0) {
				return GlidewellLabCaseResult.Refused(string.Join("\r\n",listAccountProblems),glidewellMsg);
			}
			GlidewellCredentials credentials=new GlidewellCredentials(glidewellAcct.AccountNumber,GlidewellAccts.ReadApiKey(glidewellAcct).PlainText);
			GlidewellMsg glidewellMsgForCall=glidewellMsg.Copy();
			GlidewellCallResult callResult=Call(() => glidewellClient.GetStatus(credentials,glidewellMsgForCall));
			if(callResult.Outcome==GlidewellCallOutcome.Blocked) {
				return new GlidewellLabCaseResult(false,true,callResult.ErrorMessage,glidewellMsg);
			}
			if(!callResult.IsSuccess) {
				return GlidewellLabCaseResult.Refused(Lans.g("Glidewell","Glidewell's status could not be read. The stored status is unchanged.")+"\r\n"
					+callResult.ErrorMessage,glidewellMsg);
			}
			GlidewellResponse response=GlidewellResponse.Parse(callResult.ResponseJson);
			glidewellMsg.RawJsonResponse=callResult.ResponseJson;
			string message;
			EnumGlidewellOrderStatus orderStatus;
			if(GlidewellOrderStatusText.TryParse(response.StatusText,out orderStatus)) {
				glidewellMsg.OrderStatus=orderStatus;
				message=Lans.g("Glidewell","Status")+": "+GlidewellOrderStatusText.GetDisplayText(orderStatus);
			}
			else if(response.StatusText=="") {
				message=Lans.g("Glidewell","Glidewell's answer names no status. The stored status is unchanged.");
			}
			else {
				message=Lans.g("Glidewell","Glidewell's status is not one this program knows:")+" "+response.StatusText+". "
					+Lans.g("Glidewell","The stored status is unchanged.");
			}
			GlidewellMsgs.Update(glidewellMsg);
			return new GlidewellLabCaseResult(true,false,message,glidewellMsg);
		}

		///<summary>Runs the client call. Anything it throws becomes a failed call (no connection, a timeout, a bug), never a crash.</summary>
		private static GlidewellCallResult Call(Func<GlidewellCallResult> funcCall) {
			try {
				GlidewellCallResult callResult=funcCall();
				if(callResult==null) {
					return GlidewellCallResult.Failed(Lans.g("Glidewell","No answer."));
				}
				return callResult;
			}
			catch(Exception ex) {
				return GlidewellCallResult.Failed(ex.Message);
			}
		}
		#endregion Send and status

		#region Display
		///<summary>What the user does next for a case that was registered: scan with the scanner it was sent for and pick the case there.</summary>
		public static string GetScanInstruction(GlidewellMsg glidewellMsg) {
			Meth.NoCheckMiddleTierRole();
			if(glidewellMsg==null || glidewellMsg.OrderStatus!=EnumGlidewellOrderStatus.Initialized) {
				return "";
			}
			string scanner=GetVendorName(GetScanner(glidewellMsg));
			return Lans.g("Glidewell","Take the scan on the")+" "+scanner+" "+Lans.g("Glidewell","scanner and pick this case there.");
		}

		///<summary>One short line for the Edit Lab Case window: the status, and the tracking number once shipped.</summary>
		public static string GetStatusLine(GlidewellMsg glidewellMsg) {
			Meth.NoCheckMiddleTierRole();
			if(glidewellMsg==null) {
				return Lans.g("Glidewell","Not sent to Glidewell.");
			}
			string line=GlidewellOrderStatusText.GetDisplayText(glidewellMsg.OrderStatus)
				+" ("+GetVendorName(GetScanner(glidewellMsg))+")";
			GlidewellResponse response=GlidewellResponse.Parse(glidewellMsg.RawJsonResponse);
			if(response.OutboundTrackingNumber!="") {
				line+=", "+(response.OutboundCarrier=="" ? "" : response.OutboundCarrier+" ")+response.OutboundTrackingNumber;
			}
			return line;
		}

		///<summary>Everything stored about the sent case, one item per line, for the Details button. Never the API key.</summary>
		public static string GetDetailsText(GlidewellMsg glidewellMsg,GlidewellAcct glidewellAcct) {
			Meth.NoCheckMiddleTierRole();
			if(glidewellMsg==null) {
				return Lans.g("Glidewell","Not sent to Glidewell.");
			}
			GlidewellResponse response=GlidewellResponse.Parse(glidewellMsg.RawJsonResponse);
			List<string> listLines=new List<string>();
			AddLine(listLines,Lans.g("Glidewell","Status"),GlidewellOrderStatusText.GetDisplayText(glidewellMsg.OrderStatus));
			AddLine(listLines,Lans.g("Glidewell","Scanner"),GetVendorName(GetScanner(glidewellMsg)));
			AddLine(listLines,Lans.g("Glidewell","Account"),glidewellAcct?.AccountNumber??(Lans.g("Glidewell","deleted account")));
			AddLine(listLines,"VendorRxId",glidewellMsg.VendorRxId);
			AddLine(listLines,Lans.g("Glidewell","Scanner order"),response.VendorOrderId);
			AddLine(listLines,Lans.g("Glidewell","Carrier"),response.OutboundCarrier);
			AddLine(listLines,Lans.g("Glidewell","Tracking number"),response.OutboundTrackingNumber);
			AddLine(listLines,Lans.g("Glidewell","Products"),response.Products);
			AddLine(listLines,Lans.g("Glidewell","Shade"),response.Shade);
			AddLine(listLines,Lans.g("Glidewell","Teeth"),response.ToothRange);
			AddLine(listLines,Lans.g("Glidewell","Notes from Glidewell"),response.Notes);
			if(!response.IsJsonObject && !string.IsNullOrWhiteSpace(glidewellMsg.RawJsonResponse)) {
				AddLine(listLines,Lans.g("Glidewell","Last answer"),glidewellMsg.RawJsonResponse);
			}
			return string.Join("\r\n",listLines);
		}

		private static void AddLine(List<string> listLines,string label,string value) {
			if(string.IsNullOrWhiteSpace(value)) {
				return;
			}
			listLines.Add(label+": "+value.Trim());
		}
		#endregion Display
	}
}
