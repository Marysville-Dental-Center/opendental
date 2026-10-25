// EDS claim validation (Open Dental 26.1, v26_1-manage-eds-claim-validation): the problems found for one claim and what they mean for sending it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness.VendorDirect.Eds {
	public enum EdsIssueSeverity {
		///<summary>Blocks sending.</summary>
		Error,
		///<summary>The user may send after confirming.</summary>
		Warning,
	}

	///<summary>Where a problem was found.</summary>
	public enum EdsIssueSource {
		///<summary>A check this program ran on the office's own data, before any network call.</summary>
		Local,
		///<summary>Returned by EDS.</summary>
		Eds,
	}

	///<summary>What a claim's validation means for sending it.</summary>
	public enum EdsSendDecision {
		///<summary>No problems, or the claim is not validated (not an EDS claim, ...).</summary>
		Send,
		///<summary>Warnings only: the user may send after confirming.</summary>
		ConfirmWarnings,
		///<summary>At least one error: the claim stays in the queue unsent. Also a claim that can no longer be sent (EdsValidationResult.IsNotSendable).
		///</summary>
		Block,
		///<summary>EDS could not be reached and nothing else is wrong: the user is warned and chooses to send or cancel. Never blocks silently.</summary>
		ConfirmUnreachable,
	}

	///<summary>One problem found for a claim.</summary>
	public class EdsValidationIssue {
		public EdsIssueSeverity Severity;
		public EdsIssueSource Source;
		///<summary>A short name for the check, like "BillProvNpiCheckDigit". Stable across validations; used by tests and to drop duplicates.</summary>
		public string CheckId="";
		///<summary>The part of the claim the problem is about: "Billing provider", "Procedure line 2", "Claim".</summary>
		public string Location="";
		public string Message="";
		///<summary>Where in the program to fix it.</summary>
		public string HowToFix="";
		///<summary>True when the latest re-validation no longer found this problem.</summary>
		public bool IsResolved;

		///<summary>Identifies the same problem across validations.</summary>
		public string Key {
			get {
				return (Severity+"|"+Source+"|"+(CheckId??"")+"|"+(Location??"").Trim()+"|"+(Message??"").Trim()).ToLowerInvariant();
			}
		}

		public EdsValidationIssue Copy() {
			return (EdsValidationIssue)MemberwiseClone();
		}
	}

	///<summary>The validation of one claim.</summary>
	public class EdsValidationResult {
		public long ClaimNum;
		public long PatNum;
		public string PatientName="";
		public DateTime DateService;
		public string CarrierName="";
		public DateTime DateTimeValidated;
		public List<EdsValidationIssue> ListIssues=new List<EdsValidationIssue>();
		///<summary>Set when EDS was asked but could not be reached, timed out, or answered with something that is not a validation response.</summary>
		public string CallFailedMessage="";
		///<summary>Set when the claim was not validated at all (not an EDS claim, a claim type EDS does not validate). Such a claim is sent as before.
		///</summary>
		public string SkippedReason="";
		///<summary>Set when the claim must not be sent: it was deleted, or, re-validated for the Send Claims window, it is no longer waiting to send
		///(sent, received or put on hold, for example from Edit Claim opened in the validation window). Such a result is never sent, whatever else
		///it says, and is not kept as the claim's last validation.</summary>
		public string NotSendableReason="";
		///<summary>Set when the local checks ran but EDS was not asked: the EDS attachment service is off, the credentials are blank, or local errors
		///must be fixed first. Informational only.</summary>
		public string RemoteNote="";
		///<summary>True when EDS answered.</summary>
		public bool IsRemoteChecked;

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

		public bool IsNotSendable {
			get {
				return !string.IsNullOrEmpty(NotSendableReason);
			}
		}

		public bool HasErrors {
			get {
				return ListIssues.Any(x => !x.IsResolved && x.Severity==EdsIssueSeverity.Error);
			}
		}

		public bool HasWarnings {
			get {
				return ListIssues.Any(x => !x.IsResolved && x.Severity==EdsIssueSeverity.Warning);
			}
		}

		///<summary>True when the validation window has something to show: unresolved problems or a failed call.</summary>
		public bool NeedsAttention {
			get {
				return IsCallFailed || HasErrors || HasWarnings;
			}
		}

		public EdsValidationResult Copy() {
			EdsValidationResult result=(EdsValidationResult)MemberwiseClone();
			result.ListIssues=ListIssues.Select(x => x.Copy()).ToList();
			return result;
		}
	}

	///<summary>Optional: EDS's own extended ("enhanced") claim checks. Which checks and which endpoint Open Dental 26.1 calls is not public (26.1
	///open question 4), so nothing implements this yet: it is partner-gated until EDS documents the API to the office. When it is set,
	///EdsClaimValidation.Validate calls it after the documented attachment validation and merges its problems into the same list.</summary>
	public interface IEdsEnhancedValidator {
		///<summary>Throws on a failed call; the caller turns that into a failed-call result.</summary>
		List<EdsValidationIssue> Validate(Claim claim,Clearinghouse clearinghouseClin,TimeSpan timeout);
	}
}
