// EDS claim validation in the program (Open Dental 26.1, v26_1-manage-eds-claim-validation): when Edit Claim sends or saves a claim, and when the
// Send Claims window sends claims, a claim that goes to EDS gets the EDS checks (OpenDentBusiness/VendorDirect/Eds) and one validation window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.Eds;

namespace OpenDental {
	public static class EdsValidationL {
		///<summary>True when clearinghouseClin sends claims to EDS.</summary>
		public static bool IsEds(Clearinghouse clearinghouseClin) {
			return EdsClaimValidation.IsEds(clearinghouseClin);
		}

		///<summary>Before Edit Claim sends one claim. queueItem is the claim's queue item after Eclaims.GetMissingData. Returns true when the claim may be
		///sent: it does not go to EDS, it has no problems, or the user chose Send Anyway for warnings or an unreachable EDS. Errors, including what
		///the format validators found, show in the validation window and keep the claim unsent.</summary>
		public static bool ValidateBeforeSend(Claim claim,Clearinghouse clearinghouseClin,ClaimSendQueueItem queueItem) {
			if(!EdsClaimValidation.IsEds(clearinghouseClin) || !EdsClaimValidation.ShouldValidate(claim)) {
				return true;
			}
			EdsValidationResult result=EdsClaimValidation.ValidateLocal(claim,clearinghouseClin,queueItem);
			if(EdsClaimValidation.CanAskEds(result,clearinghouseClin)) {
				if(!AskEds(new List<Claim> { claim },new List<Clearinghouse> { clearinghouseClin },new List<EdsValidationResult> { result },
					EDS.TimeoutValidateOnSend,Lans.g("EDS","Validating the claim with EDS...")))
				{
					return false;//Cancelled: nothing is sent.
				}
			}
			if(EdsClaimValidation.GetSendDecision(result)==EdsSendDecision.Send) {
				return true;
			}
			using FormEdsValidation formEdsValidation=new FormEdsValidation(new List<EdsValidationResult> { result },
				FormEdsValidation.EnumOpenedFrom.ClaimEditSend,0);
			formEdsValidation.ShowDialog();
			if(!formEdsValidation.IsSendClicked) {
				return false;
			}
			EdsValidationResult resultLatest=formEdsValidation.ListResults.FirstOrDefault()??result;
			return EdsClaimValidation.GetSendDecision(resultLatest)!=EdsSendDecision.Block;
		}

		///<summary>After Edit Claim saved a new claim that is waiting to be sent and has no missing data. Never blocks saving: the claim is saved
		///already. Waits at most EDS.TimeoutValidateOnSave for EDS. Problems, or an unreachable EDS, open the validation window without blocking.</summary>
		public static void ValidateAfterSave(Claim claim,Clearinghouse clearinghouseClin,ClaimSendQueueItem queueItem) {
			if(!EdsClaimValidation.IsEds(clearinghouseClin) || !EdsClaimValidation.ShouldValidate(claim)) {
				return;
			}
			EdsValidationResult result=EdsClaimValidation.ValidateLocal(claim,clearinghouseClin,queueItem);
			if(EdsClaimValidation.CanAskEds(result,clearinghouseClin)) {
				if(!AskEds(new List<Claim> { claim },new List<Clearinghouse> { clearinghouseClin },new List<EdsValidationResult> { result },
					EDS.TimeoutValidateOnSave,Lans.g("EDS","Validating the claim with EDS...")))
				{
					return;
				}
			}
			if(!result.NeedsAttention) {
				return;
			}
			FormEdsValidation formEdsValidation=new FormEdsValidation(new List<EdsValidationResult> { result },FormEdsValidation.EnumOpenedFrom.Save,0);
			formEdsValidation.Show();//Non-modal: saving never waits on the user.
		}

		///<summary>What a batch validation decided.</summary>
		public class BatchOutcome {
			///<summary>True when the user cancelled validation: nothing is sent.</summary>
			public bool IsCancelled;
			///<summary>The claims that were validated (they go to EDS).</summary>
			public List<long> ListClaimNumsValidated=new List<long>();
			///<summary>Of those, the claims that may be sent.</summary>
			public List<long> ListClaimNumsAllowed=new List<long>();
		}

		///<summary>Before the Send Claims window sends the selected claims. Validates every electronic claim that goes to EDS (the hqClearinghouseNum the
		///user picked, or each claim's own clearinghouse when 0), asks EDS about those without local errors in one progress window, then shows one
		///validation window, grouped by claim, for the claims with problems. Claims with errors stay unsent; claims with warnings or an unreachable
		///EDS go only when the user chose Send Anyway.</summary>
		public static BatchOutcome ValidateBatchForSend(List<ClaimSendQueueItem> listClaimSendQueueItems,long hqClearinghouseNum) {
			BatchOutcome batchOutcome=new BatchOutcome();
			List<Claim> listClaims=new List<Claim>();
			List<Clearinghouse> listClearinghouses=new List<Clearinghouse>();
			List<EdsValidationResult> listResults=new List<EdsValidationResult>();
			for(int i=0;i<listClaimSendQueueItems.Count;i++) {
				ClaimSendQueueItem claimSendQueueItem=listClaimSendQueueItems[i];
				if(!claimSendQueueItem.CanSendElect) {
					continue;
				}
				long hqClearinghouseNumForClaim=hqClearinghouseNum!=0 ? hqClearinghouseNum : claimSendQueueItem.ClearinghouseNum;
				Clearinghouse clearinghouseHq=Clearinghouses.GetClearinghouse(hqClearinghouseNumForClaim);
				if(!EdsClaimValidation.IsEds(clearinghouseHq)) {
					continue;
				}
				Claim claim=Claims.GetClaim(claimSendQueueItem.ClaimNum);
				if(claim==null || !EdsClaimValidation.ShouldValidate(claim)) {
					continue;//A deleted claim is reported when sending.
				}
				Clearinghouse clearinghouseClin=Clearinghouses.OverrideFields(clearinghouseHq,claim.ClinicNum);
				listClaims.Add(claim);
				listClearinghouses.Add(clearinghouseClin);
				listResults.Add(EdsClaimValidation.ValidateLocal(claim,clearinghouseClin,claimSendQueueItem));
				batchOutcome.ListClaimNumsValidated.Add(claim.ClaimNum);
			}
			if(listResults.Count==0) {
				return batchOutcome;
			}
			if(!AskEds(listClaims,listClearinghouses,listResults,EDS.TimeoutValidateOnSend,Lans.g("EDS","Validating the claims with EDS..."))) {
				batchOutcome.IsCancelled=true;
				return batchOutcome;
			}
			List<EdsValidationResult> listResultsWithProblems=listResults.FindAll(x => x.NeedsAttention);
			if(listResultsWithProblems.Count==0) {
				batchOutcome.ListClaimNumsAllowed=EdsClaimValidation.GetClaimNumsToSend(listResults,false);
				return batchOutcome;
			}
			using FormEdsValidation formEdsValidation=new FormEdsValidation(listResultsWithProblems,FormEdsValidation.EnumOpenedFrom.ClaimsSend,hqClearinghouseNum);
			formEdsValidation.ShowDialog();
			List<EdsValidationResult> listResultsFinal=listResults
				.Select(x => formEdsValidation.ListResults.FirstOrDefault(y => y.ClaimNum==x.ClaimNum)??x)
				.ToList();
			batchOutcome.ListClaimNumsAllowed=EdsClaimValidation.GetClaimNumsToSend(listResultsFinal,formEdsValidation.IsSendClicked);
			return batchOutcome;
		}

		///<summary>Asks EDS about each claim whose local checks passed, in one progress window. Returns false when the user cancelled.</summary>
		private static bool AskEds(List<Claim> listClaims,List<Clearinghouse> listClearinghouses,List<EdsValidationResult> listResults,TimeSpan timeout,
			string startingMessage)
		{
			List<int> listIndexes=new List<int>();
			for(int i=0;i<listResults.Count;i++) {
				if(EdsClaimValidation.CanAskEds(listResults[i],listClearinghouses[i])) {
					listIndexes.Add(i);
				}
			}
			if(listIndexes.Count==0) {
				return true;
			}
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=startingMessage;
			progressWin.ActionMain=() => {
				for(int j=0;j<listIndexes.Count;j++) {
					int index=listIndexes[j];
					EdsClaimValidation.ValidateRemote(listClaims[index],listClearinghouses[index],listResults[index],timeout);
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				//ValidateRemote never throws; this is the progress window itself. Treat it like an unreachable EDS so the user still decides.
				for(int j=0;j<listIndexes.Count;j++) {
					EdsValidationResult result=listResults[listIndexes[j]];
					if(!result.IsRemoteChecked && !result.IsCallFailed) {
						result.CallFailedMessage=EdsClaimValidation.GetCallFailedMessage(ex);
					}
				}
			}
			return !progressWin.IsCancelled;
		}

		///<summary>The claim's last EDS validation on this workstation, as text for a validation status box, or "".</summary>
		public static string GetLastValidationText(long claimNum) {
			return EdsClaimValidation.GetLastValidationText(claimNum);
		}

		///<summary>The text for the claim's EDS tab in Edit Claim: the claim's last validation on this workstation. Blank for a claim that does not
		///go to EDS.</summary>
		public static string GetEdsTabText(Claim claim,Clearinghouse clearinghouseClin) {
			if(claim==null || !EdsClaimValidation.IsEds(clearinghouseClin)) {
				return "";
			}
			string text=EdsClaimValidation.GetLastValidationText(claim.ClaimNum);
			if(text=="") {
				return Lans.g("EDS","Not validated with EDS since the program started. The claim is validated when it is sent.");
			}
			return text;
		}
	}
}
