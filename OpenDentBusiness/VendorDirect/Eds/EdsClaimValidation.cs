// EDS claim validation (Open Dental 26.1, v26_1-manage-eds-claim-validation): validates a claim that goes to the EDS clearinghouse when it is
// saved or sent. The EDS-specific local checks (EdsClaimChecks) run first; then, when the office has turned on the EDS attachment service and
// entered its EDS login, the claim goes to EDS's attachment validation (EDS.ValidateClaim) with the office's own credentials. Every problem ends
// up in one list per claim. Errors block sending, warnings and an unreachable EDS let the user send anyway.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness.Eclaims;

namespace OpenDentBusiness.VendorDirect.Eds {
	public static class EdsClaimValidation {
		///<summary>Prefix of every EDS problem added to a queue item's Missing Info or Warnings.</summary>
		public const string MISSING_DATA_PREFIX="EDS: ";
		///<summary>Partner-gated: EDS's own enhanced checks. Null until EDS documents them (see IEdsEnhancedValidator).</summary>
		public static IEdsEnhancedValidator EnhancedValidator;
		///<summary>The last validation of each claim on this workstation, shown on the claim's EDS tab and in the claim attachment window.</summary>
		private static readonly ConcurrentDictionary<long,EdsValidationResult> _dictLastResults=new ConcurrentDictionary<long,EdsValidationResult>();
		///<summary>Per claim, the procedure codes that EDS's latest answer on this workstation said need a narrative. Read by the local narrative check
		///(EdsClaimChecks, rule 2); replaced by each answer from EDS.</summary>
		private static readonly ConcurrentDictionary<long,List<string>> _dictProcCodesNarrativeRequired=new ConcurrentDictionary<long,List<string>>();

		#region Gates
		///<summary>True when clearinghouseClin sends claims to EDS. Only such claims get the EDS checks.</summary>
		public static bool IsEds(Clearinghouse clearinghouseClin) {
			return clearinghouseClin!=null && clearinghouseClin.CommBridge==EclaimsCommBridge.EDS;
		}

		///<summary>True when the claim may be sent to EDS for validation: an EDS clearinghouse whose EDS attachment service the office turned on
		///("Allow attachment send", off by default) and whose EDS login ID and password are filled in. Otherwise only the local checks run.</summary>
		public static bool IsRemoteValidationOn(Clearinghouse clearinghouseClin) {
			return IsEds(clearinghouseClin)
				&& clearinghouseClin.IsAttachmentSendAllowed
				&& !string.IsNullOrWhiteSpace(clearinghouseClin.LoginID)
				&& !string.IsNullOrWhiteSpace(clearinghouseClin.Password);
		}

		///<summary>Why EDS is not asked about the claim, or "" when it may be.</summary>
		public static string GetRemoteOffReason(Clearinghouse clearinghouseClin) {
			if(!IsEds(clearinghouseClin)) {
				return Lans.g("EDS","The clearinghouse is not EDS.");
			}
			if(!clearinghouseClin.IsAttachmentSendAllowed) {
				return Lans.g("EDS","Only local checks ran: online validation with EDS uses the EDS attachment service, which is off for this clearinghouse.");
			}
			if(string.IsNullOrWhiteSpace(clearinghouseClin.LoginID) || string.IsNullOrWhiteSpace(clearinghouseClin.Password)) {
				return Lans.g("EDS","Only local checks ran: the EDS clearinghouse has no login ID or password.");
			}
			return "";
		}

		///<summary>True for the claim types validated: primary (P), secondary (S), other (tertiary) and preauthorizations. Capitation claims are
		///never sent.</summary>
		public static bool ShouldValidate(Claim claim) {
			if(claim==null) {
				return false;
			}
			return claim.ClaimType=="P" || claim.ClaimType=="S" || claim.ClaimType=="Other" || claim.ClaimType=="PreAuth";
		}
		#endregion Gates

		#region Missing data (Eclaims.GetMissingData)
		///<summary>Called at the end of Eclaims.GetMissingData for a claim that goes to EDS: adds the local EDS errors to the queue item's Missing Info
		///(so the claim cannot be sent until they are fixed) and the local EDS warnings to its Warnings. Never throws and never calls EDS.</summary>
		public static void AddLocalChecksToQueueItem(Clearinghouse clearinghouseClin,ClaimSendQueueItem queueItem) {
			if(!IsEds(clearinghouseClin) || queueItem==null) {
				return;
			}
			List<EdsValidationIssue> listIssues;
			try {
				Claim claim=Claims.GetClaim(queueItem.ClaimNum);
				if(!ShouldValidate(claim)) {
					return;
				}
				listIssues=EdsClaimChecks.GetLocalIssues(EdsClaimSnapshot.FromDb(claim,clearinghouseClin));
			}
			catch(Exception ex) {
				ex.DoNothing();//The format validators report a claim whose data cannot be read.
				return;
			}
			queueItem.MissingData=AppendPart(queueItem.MissingData,FormatErrorsForMissingData(listIssues));
			queueItem.Warnings=AppendPart(queueItem.Warnings,FormatWarningsForQueue(listIssues));
		}

		///<summary>The text AddLocalChecksToQueueItem adds to Missing Info for these problems: each error as "EDS: message", comma separated.</summary>
		public static string FormatErrorsForMissingData(List<EdsValidationIssue> listIssues) {
			return string.Join(", ",(listIssues??new List<EdsValidationIssue>()).Where(x => x.Severity==EdsIssueSeverity.Error && !x.IsResolved)
				.Select(x => MISSING_DATA_PREFIX+FormatIssueShort(x)));
		}

		///<summary>The text AddLocalChecksToQueueItem adds to Warnings.</summary>
		public static string FormatWarningsForQueue(List<EdsValidationIssue> listIssues) {
			return string.Join(", ",(listIssues??new List<EdsValidationIssue>()).Where(x => x.Severity==EdsIssueSeverity.Warning && !x.IsResolved)
				.Select(x => MISSING_DATA_PREFIX+FormatIssueShort(x)));
		}

		///<summary>missingData without the EDS part that AddLocalChecksToQueueItem appended for listIssuesLocal: what the format validators found.
		///</summary>
		public static string GetMissingDataWithoutEds(string missingData,List<EdsValidationIssue> listIssuesLocal) {
			string value=missingData??"";
			string part=FormatErrorsForMissingData(listIssuesLocal);
			if(part=="") {
				return value;
			}
			if(value==part) {
				return "";
			}
			if(value.EndsWith(", "+part)) {
				return value.Substring(0,value.Length-part.Length-2);
			}
			return value;
		}

		private static string FormatIssueShort(EdsValidationIssue issue) {
			if(string.IsNullOrWhiteSpace(issue.Location)) {
				return issue.Message;
			}
			return issue.Location+": "+issue.Message;
		}

		private static string AppendPart(string text,string part) {
			if(string.IsNullOrEmpty(part)) {
				return text??"";
			}
			if(string.IsNullOrEmpty(text)) {
				return part;
			}
			return text+", "+part;
		}
		#endregion Missing data (Eclaims.GetMissingData)

		#region Validation
		///<summary>Validates one claim that goes to clearinghouseClin. Never throws. queueItem is the claim's queue item after Eclaims.GetMissingData;
		///what the format validators found becomes one error row, so every problem shows in one list. EDS is asked only when there are no errors,
		///isRemoteAllowed is true and IsRemoteValidationOn. The result is kept as the claim's last validation (GetLastResult).</summary>
		public static EdsValidationResult Validate(Claim claim,Clearinghouse clearinghouseClin,ClaimSendQueueItem queueItem,TimeSpan timeout,
			bool isRemoteAllowed)
		{
			EdsValidationResult result=ValidateLocal(claim,clearinghouseClin,queueItem);
			if(!isRemoteAllowed) {
				if(!result.IsSkipped && !result.HasErrors) {
					result.RemoteNote=Lans.g("EDS","Only local checks ran.");
					SetLastResult(result);
				}
				return result;
			}
			if(CanAskEds(result,clearinghouseClin)) {
				ValidateRemote(claim,clearinghouseClin,result,timeout);
			}
			return result;
		}

		///<summary>The first step of Validate: the local checks, and what the format validators put in queueItem's Missing Info as one error row.
		///Never calls EDS and never throws. Sets RemoteNote when EDS will not be asked. The result is kept as the claim's last validation.</summary>
		public static EdsValidationResult ValidateLocal(Claim claim,Clearinghouse clearinghouseClin,ClaimSendQueueItem queueItem) {
			if(claim==null) {
				return MakeNotSendableResult(null,queueItem?.ClaimNum??0,Lans.g("EDS","The claim has been deleted."));
			}
			EdsValidationResult result=MakeResult(claim);
			if(!IsEds(clearinghouseClin)) {
				result.SkippedReason=Lans.g("EDS","The claim does not go to EDS.");
				return result;
			}
			if(!ShouldValidate(claim)) {
				result.SkippedReason=Lans.g("EDS","EDS validation does not apply to this claim type.");
				return result;
			}
			List<EdsValidationIssue> listIssuesLocal=new List<EdsValidationIssue>();
			try {
				listIssuesLocal=EdsClaimChecks.GetLocalIssues(EdsClaimSnapshot.FromDb(claim,clearinghouseClin));
			}
			catch(Exception ex) {
				listIssuesLocal.Add(EdsClaimChecks.MakeLocal(EdsIssueSeverity.Error,EdsClaimChecks.CHECK_OTHER_MISSING_DATA,Lans.g("EDS","Claim"),
					Lans.g("EDS","The claim's data could not be read:")+" "+ex.Message,Lans.g("EDS","Recreate the claim.")));
			}
			string missingDataOther=GetMissingDataWithoutEds(queueItem?.MissingData,listIssuesLocal).Trim();
			List<EdsValidationIssue> listIssues=new List<EdsValidationIssue>();
			if(missingDataOther!="") {
				listIssues.Add(EdsClaimChecks.MakeLocal(EdsIssueSeverity.Error,EdsClaimChecks.CHECK_OTHER_MISSING_DATA,Lans.g("EDS","Claim data"),
					Lans.g("EDS","Missing or invalid:")+" "+missingDataOther,Lans.g("EDS","Fix the claim data in Edit Claim, the patient, the plan or the provider.")));
			}
			listIssues.AddRange(listIssuesLocal);
			result.ListIssues=EdsClaimChecks.Order(listIssues);
			if(result.HasErrors) {
				result.RemoteNote=Lans.g("EDS","EDS was not asked: fix the errors first.");
			}
			else if(!IsRemoteValidationOn(clearinghouseClin)) {
				result.RemoteNote=GetRemoteOffReason(clearinghouseClin);
			}
			result.DateTimeValidated=DateTime_.Now;
			SetLastResult(result);
			return result;
		}

		///<summary>True when the second step of Validate (asking EDS) applies: the claim was validated, the local checks found no errors, and online
		///validation is on for the clearinghouse.</summary>
		public static bool CanAskEds(EdsValidationResult result,Clearinghouse clearinghouseClin) {
			return result!=null && !result.IsSkipped && !result.HasErrors && IsRemoteValidationOn(clearinghouseClin);
		}

		///<summary>The second step of Validate: asks EDS about the claim, with the office's EDS login, and adds what EDS said to result, or sets
		///result.CallFailedMessage. Never throws. The result is kept as the claim's last validation.</summary>
		public static void ValidateRemote(Claim claim,Clearinghouse clearinghouseClin,EdsValidationResult result,TimeSpan timeout) {
			List<EdsValidationIssue> listIssuesRemote=new List<EdsValidationIssue>();
			result.CallFailedMessage="";
			result.IsRemoteChecked=false;
			try {
				EdsClaimSnapshot snapshot=EdsClaimSnapshot.FromDb(claim,clearinghouseClin);
				EDS.ListPayerResponses listPayerResponses=EDS.ValidateClaim(claim,timeout);
				if(listPayerResponses==null || listPayerResponses.Payers==null) {
					throw new ODException(Lans.g("EDS","EDS returned an empty answer."));
				}
				listIssuesRemote.AddRange(EdsClaimChecks.GetIssuesFromAttachmentResponse(listPayerResponses,snapshot));
				SetProcCodesNarrativeRequiredByEds(claim.ClaimNum,EdsClaimChecks.GetProcCodesNarrativeRequired(listPayerResponses));
				IEdsEnhancedValidator edsEnhancedValidator=EnhancedValidator;
				if(edsEnhancedValidator!=null) {
					List<EdsValidationIssue> listIssuesEnhanced=edsEnhancedValidator.Validate(claim,clearinghouseClin,timeout);
					if(listIssuesEnhanced!=null) {
						listIssuesRemote.AddRange(listIssuesEnhanced.Where(x => x!=null));
					}
				}
				result.IsRemoteChecked=true;
				result.RemoteNote="";
				//An earlier answer is replaced, and so is the local narrative check, which repeats EDS's earlier answer.
				List<EdsValidationIssue> listIssues=result.ListIssues
					.Where(x => x.Source==EdsIssueSource.Local && x.CheckId!=EdsClaimChecks.CHECK_NARRATIVE_REQUIRED).ToList();
				listIssues.AddRange(listIssuesRemote);
				result.ListIssues=EdsClaimChecks.Order(listIssues);
			}
			catch(Exception ex) {
				result.CallFailedMessage=GetCallFailedMessage(ex);
			}
			result.DateTimeValidated=DateTime_.Now;
			SetLastResult(result);
		}

		///<summary>A message for a failed EDS call that never contains the login.</summary>
		public static string GetCallFailedMessage(Exception ex) {
			Exception exception=ex;
			if(exception is AggregateException aggregateException && aggregateException.InnerException!=null) {
				exception=aggregateException.InnerException;
			}
			string message=exception?.Message;
			if(exception is Newtonsoft.Json.JsonException) {
				message=Lans.g("EDS","EDS's answer could not be read.");
			}
			if(string.IsNullOrWhiteSpace(message)) {
				message=Lans.g("EDS","The validation call failed.");
			}
			return Lans.g("EDS","EDS could not be reached:")+" "+message.Trim();
		}

		///<summary>Validates a claim again, by ClaimNum, reading its queue item and clearinghouse afresh: hqClearinghouseNumOverride when not 0 (the
		///Send Claims window's clearinghouse list), otherwise the claim's own. Used by the validation window's Re-validate. Never throws.
		///A deleted claim gets a result that cannot be sent (IsNotSendable). isWaitingToSendRequired is true when re-validating for the Send Claims
		///window, whose batch sends only claims waiting to send (W or P): a claim sent, received or held since it was selected (for example from
		///Edit Claim opened in the validation window) also gets a result that cannot be sent. Edit Claim's own Send passes false, since it may
		///resend a sent claim on purpose.</summary>
		public static EdsValidationResult ValidateClaimNum(long claimNum,long hqClearinghouseNumOverride,TimeSpan timeout,bool isWaitingToSendRequired=false) {
			Claim claim;
			try {
				claim=Claims.GetClaim(claimNum);
			}
			catch(Exception ex) {
				EdsValidationResult resultFailed=new EdsValidationResult();
				resultFailed.ClaimNum=claimNum;
				resultFailed.CallFailedMessage=ex.Message;
				return resultFailed;
			}
			if(claim==null) {
				return MakeNotSendableResult(null,claimNum,Lans.g("EDS","The claim has been deleted."));
			}
			if(isWaitingToSendRequired && !IsWaitingToSend(claim)) {
				return MakeNotSendableResult(claim,claimNum,Lans.g("EDS","The claim is no longer waiting to send. Its status is now:")+" "
					+GetClaimStatusText(claim.ClaimStatus)+".");
			}
			Clearinghouse clearinghouseClin;
			ClaimSendQueueItem queueItem;
			try {
				queueItem=Claims.GetQueueList(claimNum,0,0).FirstOrDefault();
				long hqClearinghouseNum=hqClearinghouseNumOverride!=0 ? hqClearinghouseNumOverride : (queueItem?.ClearinghouseNum??0);
				Clearinghouse clearinghouseHq=Clearinghouses.GetClearinghouse(hqClearinghouseNum);
				clearinghouseClin=clearinghouseHq==null ? null : Clearinghouses.OverrideFields(clearinghouseHq,claim.ClinicNum);
				if(queueItem!=null && clearinghouseClin!=null) {
					queueItem=Eclaims.Eclaims.GetMissingData(clearinghouseClin,queueItem);
				}
			}
			catch(Exception ex) {
				EdsValidationResult resultFailed=MakeResult(claim);
				resultFailed.ListIssues.Add(EdsClaimChecks.MakeLocal(EdsIssueSeverity.Error,EdsClaimChecks.CHECK_OTHER_MISSING_DATA,Lans.g("EDS","Claim"),
					Lans.g("EDS","The claim could not be validated:")+" "+ex.Message,Lans.g("EDS","Recreate the claim.")));
				return resultFailed;
			}
			return Validate(claim,clearinghouseClin,queueItem,timeout,true);
		}

		///<summary>A result for a claim that must not be sent (see EdsValidationResult.NotSendableReason): one error row that says why, so the
		///validation window lists it, and GetSendDecision is Block. claim is null for a deleted claim. Not kept as the claim's last validation.</summary>
		public static EdsValidationResult MakeNotSendableResult(Claim claim,long claimNum,string reason) {
			EdsValidationResult result=MakeResult(claim);
			result.ClaimNum=claimNum;
			result.NotSendableReason=reason;
			result.ListIssues.Add(EdsClaimChecks.MakeLocal(EdsIssueSeverity.Error,EdsClaimChecks.CHECK_CLAIM_NOT_SENDABLE,Lans.g("EDS","Claim"),reason,
				Lans.g("EDS","Nothing is sent for this claim from here. Refresh the Send Claims window.")));
			result.DateTimeValidated=DateTime_.Now;
			return result;
		}

		///<summary>True when the claim is in the Send Claims queue: it exists and is waiting to send (W) or probably sent (P), the statuses the
		///Send Claims window lists.</summary>
		public static bool IsWaitingToSend(Claim claim) {
			return claim!=null && (claim.ClaimStatus=="W" || claim.ClaimStatus=="P");
		}

		///<summary>The claim status letter as its description, like "Sent - Verified".</summary>
		private static string GetClaimStatusText(string claimStatus) {
			foreach(ClaimStatus status in Enum.GetValues(typeof(ClaimStatus))) {
				if(status.GetDescription(useShortVersionIfAvailable:true)==claimStatus) {
					return Lans.g("enumClaimStatus",status.GetDescription());
				}
			}
			return claimStatus??"";
		}

		///<summary>Of listClaimNums, the claims that were deleted or are no longer waiting to send (IsWaitingToSend), read from the database now.
		///</summary>
		public static List<long> GetClaimNumsNotWaitingToSend(List<long> listClaimNums) {
			if(listClaimNums.IsNullOrEmpty()) {
				return new List<long>();
			}
			List<long> listClaimNumsDistinct=listClaimNums.Distinct().ToList();
			List<long> listClaimNumsWaiting=Claims.GetClaimsFromClaimNums(listClaimNumsDistinct).FindAll(x => IsWaitingToSend(x)).Select(x => x.ClaimNum).ToList();
			return listClaimNumsDistinct.FindAll(x => !listClaimNumsWaiting.Contains(x));
		}

		///<summary>The header fields of a result, read from the database. Never throws.</summary>
		public static EdsValidationResult MakeResult(Claim claim) {
			EdsValidationResult result=new EdsValidationResult();
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
		#endregion Validation

		#region Decisions
		public static EdsSendDecision GetSendDecision(EdsValidationResult result) {
			if(result!=null && result.IsNotSendable) {
				return EdsSendDecision.Block;//Deleted, or no longer waiting to send: never sent, not even with Send Anyway.
			}
			if(result==null || result.IsSkipped) {
				return EdsSendDecision.Send;
			}
			if(result.HasErrors) {
				return EdsSendDecision.Block;
			}
			if(result.IsCallFailed) {
				return EdsSendDecision.ConfirmUnreachable;
			}
			if(result.HasWarnings) {
				return EdsSendDecision.ConfirmWarnings;
			}
			return EdsSendDecision.Send;
		}

		///<summary>The claims of a (batch) send that go out: every claim without problems, and the claims with only warnings or an unreachable EDS when
		///the user chose Send Anyway. Claims with errors stay in the queue.</summary>
		public static List<long> GetClaimNumsToSend(List<EdsValidationResult> listResults,bool isSendAnywayConfirmed) {
			List<long> listClaimNums=new List<long>();
			for(int i=0;i<listResults.Count;i++) {
				EdsSendDecision decision=GetSendDecision(listResults[i]);
				if(decision==EdsSendDecision.Send
					|| (isSendAnywayConfirmed && (decision==EdsSendDecision.ConfirmWarnings || decision==EdsSendDecision.ConfirmUnreachable)))
				{
					listClaimNums.Add(listResults[i].ClaimNum);
				}
			}
			return listClaimNums;
		}

		///<summary>The claims of a batch send from the Send Claims window that go out once the validation window has closed: GetClaimNumsToSend, less
		///every claim that was deleted or is no longer waiting to send (read from the database now). While the window was open, the user could
		///send, delete or hold a claim in Edit Claim, and another workstation could too; such a claim is never sent again from the batch.</summary>
		public static List<long> GetClaimNumsToSendForBatch(List<EdsValidationResult> listResults,bool isSendAnywayConfirmed) {
			List<long> listClaimNums=GetClaimNumsToSend(listResults,isSendAnywayConfirmed);
			List<long> listClaimNumsNotWaiting=GetClaimNumsNotWaitingToSend(listClaimNums);
			return listClaimNums.FindAll(x => !listClaimNumsNotWaiting.Contains(x));
		}

		///<summary>The list the window shows after a re-validation: every problem in listIssuesLatest (unresolved), then every problem from
		///listIssuesPrevious that the latest validation no longer found, marked resolved.</summary>
		public static List<EdsValidationIssue> MergeRevalidation(List<EdsValidationIssue> listIssuesPrevious,List<EdsValidationIssue> listIssuesLatest) {
			List<EdsValidationIssue> listIssues=new List<EdsValidationIssue>();
			List<EdsValidationIssue> listLatest=listIssuesLatest??new List<EdsValidationIssue>();
			for(int i=0;i<listLatest.Count;i++) {
				EdsValidationIssue issue=listLatest[i].Copy();
				issue.IsResolved=false;
				listIssues.Add(issue);
			}
			List<EdsValidationIssue> listPrevious=listIssuesPrevious??new List<EdsValidationIssue>();
			for(int i=0;i<listPrevious.Count;i++) {
				if(listPrevious[i].IsResolved || listIssues.Any(x => x.Key==listPrevious[i].Key)) {
					continue;
				}
				EdsValidationIssue issue=listPrevious[i].Copy();
				issue.IsResolved=true;
				listIssues.Add(issue);
			}
			return listIssues;
		}

		///<summary>Re-validation of a result shown in the window: the latest result, with problems that went away kept as resolved. When EDS could not
		///be reached this time, EDS's earlier problems are kept as they were, since nothing new is known about them.</summary>
		public static EdsValidationResult MergeRevalidation(EdsValidationResult resultPrevious,EdsValidationResult resultLatest) {
			if(resultPrevious==null || resultLatest==null) {
				return resultLatest??resultPrevious;
			}
			if(resultLatest.IsSkipped) {
				return resultLatest;
			}
			if(resultLatest.IsNotSendable) {
				//The earlier problems no longer matter, and must not show as resolved. Keep the claim's header so the batch window still names it.
				if(string.IsNullOrEmpty(resultLatest.PatientName)) {
					resultLatest.PatNum=resultPrevious.PatNum;
					resultLatest.PatientName=resultPrevious.PatientName;
					resultLatest.DateService=resultPrevious.DateService;
					resultLatest.CarrierName=resultPrevious.CarrierName;
				}
				return resultLatest;
			}
			List<EdsValidationIssue> listIssuesPrevious=resultPrevious.ListIssues;
			if(resultLatest.IsCallFailed || (!resultLatest.IsRemoteChecked && resultPrevious.IsRemoteChecked)) {
				//EDS was not asked this time: carry EDS's earlier problems over unchanged, and compare only the local ones.
				List<EdsValidationIssue> listIssuesEdsPrevious=listIssuesPrevious.Where(x => x.Source==EdsIssueSource.Eds && !x.IsResolved).Select(x => x.Copy()).ToList();
				resultLatest.ListIssues=MergeRevalidation(listIssuesPrevious.Where(x => x.Source==EdsIssueSource.Local).ToList(),resultLatest.ListIssues);
				resultLatest.ListIssues.AddRange(listIssuesEdsPrevious.Where(x => !resultLatest.ListIssues.Any(y => y.Key==x.Key)));
				resultLatest.ListIssues=resultLatest.ListIssues.OrderBy(x => x.IsResolved ? 1 : 0).ThenBy(x => x.Severity==EdsIssueSeverity.Error ? 0 : 1).ToList();
				return resultLatest;
			}
			resultLatest.ListIssues=MergeRevalidation(listIssuesPrevious,resultLatest.ListIssues);
			return resultLatest;
		}
		#endregion Decisions

		#region Display
		///<summary>One line for the Send Claims grid or the claim's EDS tab. Blank for a skipped claim.</summary>
		public static string GetSummary(EdsValidationResult result) {
			if(result==null || result.IsSkipped) {
				return "";
			}
			int countErrors=result.ListIssues.Count(x => !x.IsResolved && x.Severity==EdsIssueSeverity.Error);
			int countWarnings=result.ListIssues.Count(x => !x.IsResolved && x.Severity==EdsIssueSeverity.Warning);
			List<string> listParts=new List<string>();
			if(countErrors>0) {
				listParts.Add(Lans.g("EDS","EDS errors:")+" "+countErrors);
			}
			if(countWarnings>0) {
				listParts.Add(Lans.g("EDS","EDS warnings:")+" "+countWarnings);
			}
			if(result.IsCallFailed) {
				listParts.Add(result.CallFailedMessage);
			}
			if(listParts.Count==0) {
				return result.IsRemoteChecked ? Lans.g("EDS","EDS validation passed.") : Lans.g("EDS","The local EDS checks passed.");
			}
			return string.Join("; ",listParts);
		}

		///<summary>The claim's last validation on this workstation, as text for the claim's validation status display: when, the summary, and each
		///open problem. "" when the claim has not been validated since the program started.</summary>
		public static string GetLastValidationText(long claimNum) {
			EdsValidationResult result=GetLastResult(claimNum);
			if(result==null) {
				return "";
			}
			List<string> listLines=new List<string>();
			listLines.Add(Lans.g("EDS","Last EDS validation:")+" "+result.DateTimeValidated.ToString("g")+" - "+GetSummary(result));
			if(!string.IsNullOrWhiteSpace(result.RemoteNote)) {
				listLines.Add(result.RemoteNote);
			}
			foreach(EdsValidationIssue issue in result.ListIssues.Where(x => !x.IsResolved)) {
				listLines.Add((issue.Severity==EdsIssueSeverity.Error ? Lans.g("EDS","Error") : Lans.g("EDS","Warning"))+": "+FormatIssueShort(issue));
			}
			return string.Join("\r\n",listLines);
		}

		///<summary>The claim's last validation on this workstation, or null.</summary>
		public static EdsValidationResult GetLastResult(long claimNum) {
			EdsValidationResult result;
			if(_dictLastResults.TryGetValue(claimNum,out result)) {
				return result.Copy();
			}
			return null;
		}

		///<summary>Keeps result as its claim's last validation. Skipped and not-sendable results are not kept.</summary>
		public static void SetLastResult(EdsValidationResult result) {
			if(result==null || result.ClaimNum==0 || result.IsSkipped || result.IsNotSendable) {
				return;
			}
			_dictLastResults[result.ClaimNum]=result.Copy();
		}

		///<summary>The procedure codes that EDS's latest answer for the claim, on this workstation, said need a narrative. Empty when EDS has not
		///answered for the claim since the program started. A copy.</summary>
		public static List<string> GetProcCodesNarrativeRequiredByEds(long claimNum) {
			List<string> listProcCodes;
			if(_dictProcCodesNarrativeRequired.TryGetValue(claimNum,out listProcCodes)) {
				return new List<string>(listProcCodes);
			}
			return new List<string>();
		}

		///<summary>Keeps what EDS's latest answer for the claim said about narratives, replacing the earlier answer.</summary>
		public static void SetProcCodesNarrativeRequiredByEds(long claimNum,List<string> listProcCodes) {
			if(claimNum==0) {
				return;
			}
			_dictProcCodesNarrativeRequired[claimNum]=new List<string>(listProcCodes??new List<string>());
		}

		///<summary>Tests only.</summary>
		public static void ClearLastResults() {
			_dictLastResults.Clear();
			_dictProcCodesNarrativeRequired.Clear();
		}
		#endregion Display
	}
}
