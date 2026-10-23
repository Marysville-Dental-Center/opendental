// DentalXChange XConnect claim validation (Open Dental 25.2, v25_2-account-xconnect-claim-validation): turning XConnect's response into a list of
// problems, deciding whether a claim may be sent, and tracking which problems a re-validation resolved. The network call is XConnect.ValidateClaim.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness.Eclaims;

namespace OpenDentBusiness.VendorDirect.DentalXChange {
	public enum XConnectIssueSeverity {
		Error,
		Warning,
	}

	///<summary>What a claim's validation means for sending it.</summary>
	public enum XConnectSendDecision {
		///<summary>No problems, or the claim is not validated (XConnect off, preauthorization, ...).</summary>
		Send,
		///<summary>Warnings only: the user may send after confirming.</summary>
		ConfirmWarnings,
		///<summary>At least one error: the claim stays in the queue unsent.</summary>
		Block,
		///<summary>The call failed or timed out: the claim stays unsent, as in 24.3, because sending unvalidated would defeat the point.</summary>
		CallFailed,
	}

	///<summary>One problem DentalXChange returned for a claim.</summary>
	public class XConnectValidationIssue {
		public XConnectIssueSeverity Severity;
		///<summary>The loop, claim part or procedure line the API named.</summary>
		public string Location="";
		public string Message="";
		///<summary>Our own hint about where in the program to fix it.</summary>
		public string HowToFix="";
		///<summary>True when the latest re-validation no longer returned this problem.</summary>
		public bool IsResolved;

		///<summary>Identifies the same problem across validations.</summary>
		public string Key {
			get {
				return (Severity+"|"+(Location??"").Trim()+"|"+(Message??"").Trim()).ToLowerInvariant();
			}
		}

		public XConnectValidationIssue Copy() {
			return (XConnectValidationIssue)MemberwiseClone();
		}
	}

	///<summary>The validation of one claim.</summary>
	public class XConnectValidationResult {
		public long ClaimNum;
		public long PatNum;
		public string PatientName="";
		public DateTime DateService;
		public string CarrierName="";
		public List<XConnectValidationIssue> ListIssues=new List<XConnectValidationIssue>();
		///<summary>Set when the call failed or timed out.</summary>
		public string CallFailedMessage="";
		///<summary>Set when the claim was not validated at all (XConnect off, preauthorization, ...). Such a claim is sent as before.</summary>
		public string SkippedReason="";

		public bool IsCallFailed {
			get {
				return !string.IsNullOrEmpty(CallFailedMessage);
			}
		}

		public bool IsSkipped {
			get {
				return !string.IsNullOrEmpty(SkippedReason);
			}
		}

		public bool HasErrors {
			get {
				return ListIssues.Any(x => !x.IsResolved && x.Severity==XConnectIssueSeverity.Error);
			}
		}

		public bool HasWarnings {
			get {
				return ListIssues.Any(x => !x.IsResolved && x.Severity==XConnectIssueSeverity.Warning);
			}
		}

		///<summary>True when the validation window has something to show: unresolved problems or a failed call.</summary>
		public bool NeedsAttention {
			get {
				return IsCallFailed || HasErrors || HasWarnings;
			}
		}
	}

	public static class XConnectValidation {
		///<summary>True for the claim types XConnect validates: primary (P), secondary (S) and tertiary (Other). Preauthorizations and capitation
		///claims are skipped.</summary>
		public static bool ShouldValidate(Claim claim) {
			if(claim==null) {
				return false;
			}
			return claim.ClaimType=="P" || claim.ClaimType=="S" || claim.ClaimType=="Other";
		}

		///<summary>Validates the claim with XConnect when it is on for clearinghouseClin. Never throws: a failed or timed-out call is reported in
		///CallFailedMessage. Every call is logged as etrans rows by XConnect.ValidateClaim.</summary>
		public static XConnectValidationResult Validate(Claim claim,Clearinghouse clearinghouseClin,TimeSpan timeout) {
			XConnectValidationResult result=MakeResult(claim);
			if(!XConnect.IsEnabled(clearinghouseClin)) {
				result.SkippedReason=Lans.g("XConnect","XConnect is not enabled for this clearinghouse.");
				return result;
			}
			if(!ShouldValidate(claim)) {
				result.SkippedReason=Lans.g("XConnect","XConnect validates only primary, secondary and tertiary claims.");
				return result;
			}
			try {
				XConnectWebResponse xConnectWebResponse=XConnect.ValidateClaim(claim,timeout);
				result.ListIssues=GetIssues(xConnectWebResponse,claim.AttachedFlags);
			}
			catch(Exception ex) {
				result.CallFailedMessage=string.IsNullOrWhiteSpace(ex.Message) ? Lans.g("XConnect","The validation call failed.") : ex.Message;
			}
			return result;
		}

		///<summary>The header fields of a result, read from the database. Never throws.</summary>
		public static XConnectValidationResult MakeResult(Claim claim) {
			XConnectValidationResult result=new XConnectValidationResult();
			if(claim==null) {
				return result;
			}
			result.ClaimNum=claim.ClaimNum;
			result.PatNum=claim.PatNum;
			result.DateService=claim.DateService;
			try {
				Patient patient=Patients.GetLim(claim.PatNum);
				if(patient!=null) {
					result.PatientName=patient.GetNameLF();
				}
				InsPlan insPlan=InsPlans.GetPlan(claim.PlanNum,null);
				if(insPlan!=null) {
					result.CarrierName=Carriers.GetName(insPlan.CarrierNum);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return result;
		}

		///<summary>The problems in an XConnect validation response, errors first. Claim errors and procedure-line errors are errors; the claim status
		///message (missing information) is an error; procedure-line status messages and the response's warnings are warnings. A procedure-line
		///message about attachments is left out once the claim's DentalXChange attachment was sent (claim.AttachedFlags has Submitted). When the
		///status code is not 0 and nothing else explains it, the status itself is an error.</summary>
		public static List<XConnectValidationIssue> GetIssues(XConnectWebResponse xConnectWebResponse,string attachedFlags) {
			List<XConnectValidationIssue> listIssues=new List<XConnectValidationIssue>();
			if(xConnectWebResponse==null) {
				listIssues.Add(MakeIssue(XConnectIssueSeverity.Error,Lans.g("XConnect","Claim"),Lans.g("XConnect","DentalXChange returned no response."),""));
				return listIssues;
			}
			bool isAttachmentSubmitted=ClaimAttaches.IsDxcAttachmentSubmitted(attachedFlags);
			XConnectClaimStatus xConnectClaimStatus=xConnectWebResponse.response;
			if(xConnectClaimStatus!=null) {
				XConnectDXCStatus xConnectDXCStatus=xConnectClaimStatus.dxcStatus;
				if(xConnectDXCStatus!=null && xConnectDXCStatus.claimErrors!=null) {
					for(int i=0;i<xConnectDXCStatus.claimErrors.Length;i++) {
						XConnectClaimError xConnectClaimError=xConnectDXCStatus.claimErrors[i];
						if(xConnectClaimError==null) {
							continue;
						}
						string location=JoinNonBlank(" ",xConnectClaimError.claimPart,xConnectClaimError.entityId);
						string message=xConnectClaimError.description;
						if(string.IsNullOrWhiteSpace(message)) {
							message=Lans.g("XConnect","Error code")+" "+xConnectClaimError.code;
						}
						else if(!string.IsNullOrWhiteSpace(xConnectClaimError.code)) {
							message+=" ("+xConnectClaimError.code+")";
						}
						listIssues.Add(MakeIssue(XConnectIssueSeverity.Error,location,message,
							JoinNonBlank(" ",xConnectClaimError.claimPart,xConnectClaimError.category,xConnectClaimError.description)));
					}
				}
				if(xConnectDXCStatus!=null && xConnectDXCStatus.claimItemErrors!=null) {
					for(int i=0;i<xConnectDXCStatus.claimItemErrors.Length;i++) {
						XConnectClaimItemError xConnectClaimItemError=xConnectDXCStatus.claimItemErrors[i];
						if(xConnectClaimItemError==null) {
							continue;
						}
						listIssues.Add(MakeIssue(XConnectIssueSeverity.Error,GetProcedureLineLocation(xConnectClaimItemError.claimItem),
							Lans.g("XConnect","Error code")+" "+xConnectClaimItemError.code,"procedure"));
					}
				}
				if(!string.IsNullOrWhiteSpace(xConnectClaimStatus.claimStatus?.message)) {
					listIssues.Add(MakeIssue(XConnectIssueSeverity.Error,Lans.g("XConnect","Claim"),xConnectClaimStatus.claimStatus.message.Trim(),
						xConnectClaimStatus.claimStatus.message));
				}
				if(xConnectClaimStatus.claimItems!=null) {
					for(int i=0;i<xConnectClaimStatus.claimItems.Length;i++) {
						string message=xConnectClaimStatus.claimItems[i]?.itemStatus?.message;
						if(string.IsNullOrWhiteSpace(message)) {
							continue;
						}
						if(isAttachmentSubmitted && message.ToLowerInvariant().Contains("attachment")) {
							continue;//The attachment was sent already.
						}
						listIssues.Add(MakeIssue(XConnectIssueSeverity.Warning,GetProcedureLineLocation(i+1),message.Trim(),message));
					}
				}
				if(xConnectClaimStatus.warnings!=null) {
					for(int i=0;i<xConnectClaimStatus.warnings.Length;i++) {
						if(string.IsNullOrWhiteSpace(xConnectClaimStatus.warnings[i])) {
							continue;
						}
						listIssues.Add(MakeIssue(XConnectIssueSeverity.Warning,Lans.g("XConnect","Claim"),xConnectClaimStatus.warnings[i].Trim(),
							xConnectClaimStatus.warnings[i]));
					}
				}
			}
			if(xConnectWebResponse.status!=null && xConnectWebResponse.status.code!=0 && !listIssues.Any(x => x.Severity==XConnectIssueSeverity.Error)) {
				string message=JoinNonBlank(" ",xConnectWebResponse.messages==null ? "" : string.Join(" ",xConnectWebResponse.messages.Where(x => !string.IsNullOrWhiteSpace(x))));
				if(message=="") {
					message=xConnectWebResponse.status.description;
				}
				if(string.IsNullOrWhiteSpace(message)) {
					message=Lans.g("XConnect","DentalXChange returned status code")+" "+xConnectWebResponse.status.code+".";
				}
				listIssues.Add(MakeIssue(XConnectIssueSeverity.Error,Lans.g("XConnect","Claim"),message.Trim(),message));
			}
			//Errors first, then warnings, otherwise in the order returned. Duplicates collapse.
			List<XConnectValidationIssue> listIssuesDistinct=new List<XConnectValidationIssue>();
			foreach(XConnectValidationIssue issue in listIssues.OrderBy(x => x.Severity==XConnectIssueSeverity.Error ? 0 : 1)) {
				if(listIssuesDistinct.Any(x => x.Key==issue.Key)) {
					continue;
				}
				listIssuesDistinct.Add(issue);
			}
			return listIssuesDistinct;
		}

		///<summary>The list the window shows after a re-validation: every problem in listIssuesLatest (unresolved), then every problem from
		///listIssuesPrevious that the latest validation no longer returned, marked resolved.</summary>
		public static List<XConnectValidationIssue> MergeRevalidation(List<XConnectValidationIssue> listIssuesPrevious,List<XConnectValidationIssue> listIssuesLatest) {
			List<XConnectValidationIssue> listIssues=new List<XConnectValidationIssue>();
			List<XConnectValidationIssue> listLatest=listIssuesLatest??new List<XConnectValidationIssue>();
			for(int i=0;i<listLatest.Count;i++) {
				XConnectValidationIssue issue=listLatest[i].Copy();
				issue.IsResolved=false;
				listIssues.Add(issue);
			}
			List<XConnectValidationIssue> listPrevious=listIssuesPrevious??new List<XConnectValidationIssue>();
			for(int i=0;i<listPrevious.Count;i++) {
				if(listIssues.Any(x => x.Key==listPrevious[i].Key)) {
					continue;
				}
				XConnectValidationIssue issue=listPrevious[i].Copy();
				issue.IsResolved=true;
				listIssues.Add(issue);
			}
			return listIssues;
		}

		///<summary>Re-validation of a result shown in the window: the latest result, with problems that went away kept as resolved.</summary>
		public static XConnectValidationResult MergeRevalidation(XConnectValidationResult resultPrevious,XConnectValidationResult resultLatest) {
			if(resultPrevious==null) {
				return resultLatest;
			}
			if(resultLatest.IsCallFailed || resultLatest.IsSkipped) {
				//Nothing new is known about the old problems: keep them as they were.
				resultLatest.ListIssues=resultPrevious.ListIssues.Select(x => x.Copy()).ToList();
				return resultLatest;
			}
			resultLatest.ListIssues=MergeRevalidation(resultPrevious.ListIssues,resultLatest.ListIssues);
			return resultLatest;
		}

		public static XConnectSendDecision GetSendDecision(XConnectValidationResult result) {
			if(result==null || result.IsSkipped) {
				return XConnectSendDecision.Send;
			}
			if(result.IsCallFailed) {
				return XConnectSendDecision.CallFailed;
			}
			if(result.HasErrors) {
				return XConnectSendDecision.Block;
			}
			if(result.HasWarnings) {
				return XConnectSendDecision.ConfirmWarnings;
			}
			return XConnectSendDecision.Send;
		}

		///<summary>The claims of a (batch) send that go out: every claim without problems, and the warning-only claims when the user chose Send
		///Anyway. Claims with errors or a failed call stay in the queue.</summary>
		public static List<long> GetClaimNumsToSend(List<XConnectValidationResult> listResults,bool isSendAnywayConfirmed) {
			List<long> listClaimNums=new List<long>();
			for(int i=0;i<listResults.Count;i++) {
				XConnectSendDecision decision=GetSendDecision(listResults[i]);
				if(decision==XConnectSendDecision.Send || (decision==XConnectSendDecision.ConfirmWarnings && isSendAnywayConfirmed)) {
					listClaimNums.Add(listResults[i].ClaimNum);
				}
			}
			return listClaimNums;
		}

		///<summary>One line for the Send Claims grid's Missing Info or Warnings column.</summary>
		public static string GetSummary(XConnectValidationResult result) {
			if(result==null || result.IsSkipped) {
				return "";
			}
			if(result.IsCallFailed) {
				return Lans.g("XConnect","XConnect validation call failed:")+" "+result.CallFailedMessage;
			}
			int countErrors=result.ListIssues.Count(x => !x.IsResolved && x.Severity==XConnectIssueSeverity.Error);
			int countWarnings=result.ListIssues.Count(x => !x.IsResolved && x.Severity==XConnectIssueSeverity.Warning);
			if(countErrors>0) {
				return Lans.g("XConnect","XConnect errors:")+" "+countErrors;
			}
			if(countWarnings>0) {
				return Lans.g("XConnect","XConnect warnings:")+" "+countWarnings;
			}
			return "";
		}

		///<summary>Our own hint for where a problem is fixed, chosen from words in the API's claim part, category and message.</summary>
		public static string GetHowToFix(string text) {
			string lower=(text??"").ToLowerInvariant();
			if(lower.Contains("attach")) {
				return Lans.g("XConnect","Add the attachment with the claim's right-click attachment options in the Account module.");
			}
			if(lower.Contains("subscriber") || lower.Contains("patient") || lower.Contains("member") || lower.Contains("birth")) {
				return Lans.g("XConnect","Check the patient and subscriber: name, birthdate, subscriber ID and relationship (Family module).");
			}
			if(lower.Contains("provider") || lower.Contains("npi") || lower.Contains("taxonomy") || lower.Contains("billing") || lower.Contains("rendering")
				|| lower.Contains("license") || lower.Contains("tax id"))
			{
				return Lans.g("XConnect","Check the billing and treating providers: NPI, taxonomy, license and tax ID (Setup > Providers).");
			}
			if(lower.Contains("payer") || lower.Contains("carrier") || lower.Contains("insur") || lower.Contains("plan") || lower.Contains("group")) {
				return Lans.g("XConnect","Check the insurance plan and carrier: payer ID, address and group number.");
			}
			if(lower.Contains("diagnos") || lower.Contains("icd")) {
				return Lans.g("XConnect","Check the diagnosis codes on the claim's procedures.");
			}
			if(lower.Contains("address") || lower.Contains("zip") || lower.Contains("state") || lower.Contains("city")) {
				return Lans.g("XConnect","Check the addresses: practice or clinic, patient and carrier.");
			}
			if(lower.Contains("procedure") || lower.Contains("tooth") || lower.Contains("surface") || lower.Contains("line") || lower.Contains("code")
				|| lower.Contains("quadrant") || lower.Contains("area"))
			{
				return Lans.g("XConnect","Check the procedure on the claim: code, tooth, surface or area, and date.");
			}
			return Lans.g("XConnect","Correct the claim in Edit Claim, then re-validate.");
		}

		private static XConnectValidationIssue MakeIssue(XConnectIssueSeverity severity,string location,string message,string textForHint) {
			XConnectValidationIssue issue=new XConnectValidationIssue();
			issue.Severity=severity;
			issue.Location=location??"";
			issue.Message=message??"";
			issue.HowToFix=GetHowToFix(JoinNonBlank(" ",location,textForHint));
			return issue;
		}

		private static string GetProcedureLineLocation(int lineNumber) {
			return Lans.g("XConnect","Procedure line")+" "+lineNumber;
		}

		private static string JoinNonBlank(string separator,params string[] arrayParts) {
			return string.Join(separator,arrayParts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
		}
	}
}
