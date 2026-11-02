// Related claims of the next insurance (package M2-CLAIMRCV in docs/specs/port-backlog.md, Open Dental 26.1): receiving a claim of any ordinal
// offers to handle the unsent claims of the next insurance on the same procedures, where 24.3 only looked for secondary claims after a primary.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	public partial class Claims {
		///<summary>Returns the claims of the next insurance for the claims of the claimprocs passed in (usually the claimprocs of one received claim).
		///For each claim in the list: the claims of the same patient that hold at least one of its procedures, are not preauthorizations, belong to a
		///later insurance (patplan ordinal) and are the next insurance that has not received them yet, with status Unsent or Hold Until Prior Received
		///(and Hold for In Process when includeHoldForInProcess is true), and with at least one not-received claimproc on those procedures.
		///Open Dental 26.1 generalized the secondary-after-primary lookup to any ordinal (see SelectNextOrdinalClaims).</summary>
		public static List<Claim> GetNextOrdinalClaimsNotReceived(List<ClaimProc> listClaimProcsReceived,bool includeHoldForInProcess=false) {
			Meth.NoCheckMiddleTierRole();
			if(listClaimProcsReceived.IsNullOrEmpty()) {
				return new List<Claim>();
			}
			List<long> listProcNums=listClaimProcsReceived.FindAll(x => x.ProcNum>0).Select(x => x.ProcNum).Distinct().ToList();
			List<long> listClaimNumsReceived=listClaimProcsReceived.FindAll(x => x.ClaimNum>0).Select(x => x.ClaimNum).Distinct().ToList();
			if(listProcNums.Count==0 || listClaimNumsReceived.Count==0) {
				return new List<Claim>();
			}
			List<ClaimProc> listClaimProcsForProcs=ClaimProcs.GetForProcs(listProcNums).FindAll(x => x.ClaimNum!=0);
			List<long> listClaimNums=listClaimProcsForProcs.Select(x => x.ClaimNum).Union(listClaimNumsReceived).Distinct().ToList();
			List<Claim> listClaims=GetClaimsFromClaimNums(listClaimNums);
			List<long> listPatNums=listClaims.Select(x => x.PatNum).Distinct().ToList();
			List<PatPlan> listPatPlans=PatPlans.GetPatPlansForPats(listPatNums);
			return SelectNextOrdinalClaims(listClaimProcsReceived,listClaimProcsForProcs,listClaims,listPatPlans,includeHoldForInProcess);
		}

		///<summary>The selection behind GetNextOrdinalClaimsNotReceived, on lists already loaded: the claimprocs of the received claim(s), every
		///claimproc attached to a claim for their procedures, those claims and the received ones, and the patients' patplans. Rules for each received
		///claim R (not a preauthorization):
		///1. Order. Each claim has a place in the patient's insurance order (GetClaimPlace): its patplan ordinal, or, when its plan was dropped after
		///the claim was made, the ordinal of its claim type (primary 1, secondary 2) just before the plan that holds that ordinal today, because
		///dropping a plan (PatPlans.Delete) moves every later plan up one ordinal. A claim of unknown place (another type whose plan was dropped)
		///is never related, and a received claim of unknown place offers nothing.
		///2. Candidates: other claims of R's patient, not preauthorizations, with a claimproc (not a preauth claimproc) on one of R's procedures,
		///placed after R. Exception kept from 24.3, which offered every unsent secondary claim after a primary claim: when R is a primary claim
		///(type P), a secondary claim (type S) comes directly after R, ahead of any other later insurance, when today's order does not place it after
		///R (plans reordered after their claims were made) or when both plans were dropped (their order is known only from the claim types).
		///3. The next insurance is the first candidate place whose claim is not received yet. A later insurance that already received the procedures
		///is passed over (claims received out of order); a later insurance with no claim for them (for example a medical plan between two dental
		///plans) is never a candidate.
		///4. Returned: the candidates at that place with status U or H (or I when includeHoldForInProcess) that have a not-received claimproc on
		///one of R's procedures. A claim sent or waiting at that place returns nothing, so a later insurance never jumps the queue.</summary>
		public static List<Claim> SelectNextOrdinalClaims(List<ClaimProc> listClaimProcsReceived,List<ClaimProc> listClaimProcsForProcs,
			List<Claim> listClaims,List<PatPlan> listPatPlans,bool includeHoldForInProcess=false)
		{
			Meth.NoCheckMiddleTierRole();
			List<Claim> listClaimsRet=new List<Claim>();
			if(listClaimProcsReceived.IsNullOrEmpty() || listClaimProcsForProcs==null || listClaims==null) {
				return listClaimsRet;
			}
			listPatPlans=listPatPlans??new List<PatPlan>();
			List<string> listStatuses=GetNextOrdinalStatuses(includeHoldForInProcess);
			List<long> listClaimNumsReceived=listClaimProcsReceived.FindAll(x => x.ClaimNum>0).Select(x => x.ClaimNum).Distinct().ToList();
			for(int i=0;i<listClaimNumsReceived.Count;i++) {
				Claim claimReceived=listClaims.Find(x => x.ClaimNum==listClaimNumsReceived[i]);
				if(claimReceived==null || claimReceived.ClaimType=="PreAuth") {
					continue;
				}
				List<PatPlan> listPatPlansForPat=listPatPlans.FindAll(x => x.PatNum==claimReceived.PatNum);
				int placeReceived=GetClaimPlace(claimReceived,listPatPlansForPat);
				if(placeReceived<1) {
					continue;//Insurance unknown, so there is no "next" insurance.
				}
				bool isReceivedPlanDropped=GetClaimPatPlan(claimReceived,listPatPlansForPat)==null;
				List<long> listProcNums=listClaimProcsReceived.FindAll(x => x.ClaimNum==claimReceived.ClaimNum && x.ProcNum>0)
					.Select(x => x.ProcNum).Distinct().ToList();
				List<ClaimProc> listClaimProcsOthers=listClaimProcsForProcs.FindAll(x => x.ClaimNum!=0
					&& x.ClaimNum!=claimReceived.ClaimNum
					&& x.Status!=ClaimProcStatus.Preauth
					&& listProcNums.Contains(x.ProcNum));
				List<long> listClaimNumsOthers=listClaimProcsOthers.Select(x => x.ClaimNum).Distinct().ToList();
				List<Claim> listClaimsCandidates=new List<Claim>();
				List<int> listPlacesCandidates=new List<int>();
				for(int c=0;c<listClaimNumsOthers.Count;c++) {
					Claim claim=listClaims.Find(x => x.ClaimNum==listClaimNumsOthers[c]);
					if(claim==null || claim.ClaimType=="PreAuth" || claim.PatNum!=claimReceived.PatNum) {
						continue;
					}
					int place=GetClaimPlace(claim,listPatPlansForPat);
					bool isBothPlansDropped=isReceivedPlanDropped && GetClaimPatPlan(claim,listPatPlansForPat)==null;
					if(IsSecondaryAfterPrimary(claimReceived,claim) && (place<=placeReceived || isBothPlansDropped)) {
						//24.3 order: directly after the primary claim. Places are even, so this odd place is shared with no other insurance's claim.
						place=placeReceived+1;
					}
					else if(place<=placeReceived) {
						continue;//Prior insurance, the same insurance, or unknown.
					}
					listClaimsCandidates.Add(claim);
					listPlacesCandidates.Add(place);
				}
				int placeNext=int.MaxValue;
				for(int c=0;c<listClaimsCandidates.Count;c++) {
					if(listClaimsCandidates[c].ClaimStatus!="R" && listPlacesCandidates[c]<placeNext) {
						placeNext=listPlacesCandidates[c];
					}
				}
				if(placeNext==int.MaxValue) {
					continue;//Every later insurance has received these procedures already, or there is none.
				}
				for(int c=0;c<listClaimsCandidates.Count;c++) {
					Claim claim=listClaimsCandidates[c];
					if(listPlacesCandidates[c]!=placeNext || !listStatuses.Contains(claim.ClaimStatus)) {
						continue;
					}
					if(!listClaimProcsOthers.Any(x => x.ClaimNum==claim.ClaimNum && x.Status==ClaimProcStatus.NotReceived)) {
						continue;
					}
					if(listClaimsRet.Any(x => x.ClaimNum==claim.ClaimNum)) {
						continue;
					}
					listClaimsRet.Add(claim);
				}
			}
			return listClaimsRet;
		}

		///<summary>True when the received claim is a primary claim (type P) and the candidate a secondary claim (type S): the pair 24.3 linked
		///whatever the ordinals (it offered every unsent secondary claim on the procedures of a received primary claim).</summary>
		public static bool IsSecondaryAfterPrimary(Claim claimReceived,Claim claimCandidate) {
			Meth.NoCheckMiddleTierRole();
			if(claimReceived==null || claimCandidate==null) {
				return false;
			}
			return claimReceived.ClaimType=="P" && claimCandidate.ClaimType=="S";
		}

		///<summary>The claim's place in the patient's insurance order, for SelectNextOrdinalClaims. With its patplan: 4 times the patplan ordinal.
		///When that patplan is gone (the plan was dropped after the claim was made): 4 times the claim type's ordinal (GetClaimOrdinal: primary 1,
		///secondary 2) minus 2, which is just before the plan that holds that ordinal today, since dropping a plan (PatPlans.Delete) moved every
		///later plan up one ordinal. 0 when the ordinal is unknown. Places are even, so SelectNextOrdinalClaims can put a claim between two of
		///them.</summary>
		public static int GetClaimPlace(Claim claim,List<PatPlan> listPatPlansForPat) {
			Meth.NoCheckMiddleTierRole();
			int ordinal=GetClaimOrdinal(claim,listPatPlansForPat);
			if(ordinal<1) {
				return 0;
			}
			if(GetClaimPatPlan(claim,listPatPlansForPat)==null) {
				return ordinal*4-2;
			}
			return ordinal*4;
		}

		///<summary>The claim's insurance ordinal: the patient's patplan for the claim's InsSubNum (1 for the first insurance). When that patplan is
		///gone (the plan was dropped after the claim was made), primary claims count as 1 and secondary claims as 2; anything else returns 0
		///(unknown). Dropping a plan moves the later plans up one ordinal, so this can equal a later claim's ordinal; GetClaimPlace orders such
		///claims.</summary>
		public static int GetClaimOrdinal(Claim claim,List<PatPlan> listPatPlansForPat) {
			Meth.NoCheckMiddleTierRole();
			if(claim==null) {
				return 0;
			}
			PatPlan patPlan=GetClaimPatPlan(claim,listPatPlansForPat);
			if(patPlan!=null) {
				return patPlan.Ordinal;
			}
			if(claim.ClaimType=="P") {
				return 1;
			}
			if(claim.ClaimType=="S") {
				return 2;
			}
			return 0;
		}

		///<summary>The patient's patplan (with an ordinal) for the claim's subscriber, or null when the plan was dropped after the claim was made.</summary>
		private static PatPlan GetClaimPatPlan(Claim claim,List<PatPlan> listPatPlansForPat) {
			if(claim==null || listPatPlansForPat==null) {
				return null;
			}
			return listPatPlansForPat.Find(x => x.InsSubNum==claim.InsSubNum && x.PatNum==claim.PatNum && x.Ordinal>0);
		}

		///<summary>The claim statuses of a related claim of the next insurance: Unsent and Hold Until Prior Received, plus Hold for In Process when
		///asked. Claims on hold for an in-process multi-visit group are only recalculated; the prompt never changes, sends or prints them, because
		///their procedures are not complete.</summary>
		public static List<string> GetNextOrdinalStatuses(bool includeHoldForInProcess) {
			Meth.NoCheckMiddleTierRole();
			List<string> listStatuses=new List<string> {
				ClaimStatus.Unsent.GetDescription(useShortVersionIfAvailable:true),
				ClaimStatus.HoldUntilPriReceived.GetDescription(useShortVersionIfAvailable:true),
			};
			if(includeHoldForInProcess) {
				listStatuses.Add(ClaimStatus.HoldForInProcess.GetDescription(useShortVersionIfAvailable:true));
			}
			return listStatuses;
		}
	}

	///<summary>Wording and decisions for the related-claims prompt shown after a claim is received (Open Dental 26.1, package M2-CLAIMRCV). The
	///prompt itself is ClaimL.PromptForPrimaryOrSecondaryClaim in the OpenPmsDental project; this class holds the parts that need no window, so they
	///can be tested.</summary>
	public static class ClaimReceiveRelated {
		///<summary>Open Dental 26.1 shows claim status H as "Hold Until Prior Received" (the 26.1 database document describes H as hold until prior
		///received). The stored value stays "H".</summary>
		public const string HOLD_UNTIL_PRIOR_RECEIVED="Hold Until Prior Received";

		///<summary>The text for a claim status in status drop-downs and grids: the enum description, except H, which reads "Hold Until Prior
		///Received".</summary>
		public static string GetClaimStatusText(ClaimStatus claimStatus) {
			if(claimStatus==ClaimStatus.HoldUntilPriReceived) {
				return HOLD_UNTIL_PRIOR_RECEIVED;
			}
			return claimStatus.GetDescription();
		}

		///<summary>The prompt's message. isNextInsurance false is the medical-to-primary prompt, which keeps its 24.3 wording.</summary>
		public static string GetMessage(bool isNextInsurance) {
			if(isNextInsurance) {
				return "There is at least one unsent claim for the next insurance on the received procedures.";
			}
			return "There is at least one unsent primary claim for the received procedures.";
		}

		///<summary>The prompt window's title.</summary>
		public static string GetTitle(bool isNextInsurance) {
			if(isNextInsurance) {
				return "Outstanding claims for the next insurance";
			}
			return "Outstanding primary claims";
		}

		///<summary>The text of the "send now" choice.</summary>
		public static string GetSendText(bool isNextInsurance) {
			if(isNextInsurance) {
				return "Send claim(s) now";
			}
			return "Send primary claim(s) now";
		}

		///<summary>The text of the "print now" choice.</summary>
		public static string GetPrintText(bool isNextInsurance) {
			if(isNextInsurance) {
				return "Print claim(s) now";
			}
			return "Print primary claim(s) now";
		}

		///<summary>The preference that removes "Do nothing" from the prompt. ClaimPrimaryRecievedForceSecondaryStatus now applies to the next
		///insurance after any received claim; ClaimMedReceivedForcePrimaryStatus to the medical-to-primary prompt.</summary>
		public static PrefName GetForceStatusPrefName(bool isNextInsurance) {
			if(isNextInsurance) {
				return PrefName.ClaimPrimaryRecievedForceSecondaryStatus;
			}
			return PrefName.ClaimMedReceivedForcePrimaryStatus;
		}

		///<summary>True when the estimates of the next insurance's claims are recalculated after this claim is received in Edit Claim: the
		///preference ClaimPrimaryReceivedRecalcSecondary is on, the claim is not a preauthorization (any ordinal since Open Dental 26.1, where 24.3
		///only recalculated after a primary claim), and it was not received with no payment (that path recalculates by itself).</summary>
		public static bool IsRecalcNextInsuranceNeeded(Claim claimReceived,bool isReceivedAsNoPay,bool isRecalcPrefOn) {
			if(claimReceived==null || !isRecalcPrefOn || isReceivedAsNoPay) {
				return false;
			}
			return claimReceived.ClaimType!="PreAuth";
		}
	}
}
