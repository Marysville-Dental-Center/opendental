// M2-ERA, v26_1-account-era-claimproc-link: how the ERA windows use claimproc.Etrans835AttachNum (Open Dental 26.1), the ERA claim a received
// claimproc was entered from. Hx835_Claim.IsProcessed asks here first, so the "Recd" column of the ERA window and of the ERAs Automatically
// Processed report, the ERA status, and automatic processing all see an ERA claim as processed only when its own payment was entered, even when
// the same claim is in several ERAs (split payments, reversals, corrections) or twice in one ERA. Claimprocs without a link (entered before the
// upgrade, or by hand) keep the matching that 24.3 used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	public static class EraClaimProcLinks {
		///<summary>The etrans835attach rows that stand for this ERA claim: this 835 (EtransNum) and this CLP loop, plus the CLP loops of the same claim
		///that the 835 split off (Hx835_Claim.IsSplitClaim), which are entered together with it. Includes the attach rows of claims split from the
		///claim in the ERA payment window (same 835 and CLP loop, another ClaimNum).</summary>
		public static List<long> GetAttachNumsForEraClaim(Hx835_Claim hx835_Claim,List<Etrans835Attach> listEtrans835Attaches) {
			if(hx835_Claim?.Era?.EtransSource==null || listEtrans835Attaches==null) {
				return new List<long>();
			}
			long etransNum=hx835_Claim.Era.EtransSource.EtransNum;
			List<int> listClpSegmentIndexes=new List<int>() { hx835_Claim.ClpSegmentIndex };
			if(hx835_Claim.IsSplitClaim) {
				listClpSegmentIndexes.AddRange(hx835_Claim.GetOtherNotDetachedSplitClaims().Select(x => x.ClpSegmentIndex));
			}
			return listEtrans835Attaches
				.Where(x => x.EtransNum==etransNum && x.Etrans835AttachNum!=0 && listClpSegmentIndexes.Contains(x.ClpSegmentIndex))
				.Select(x => x.Etrans835AttachNum)
				.Distinct()
				.ToList();
		}

		///<summary>True when the ERA claim's own payment was entered: a claimproc of its claim (or of a claim split from it in the ERA payment window) is
		///linked to the ERA claim and still received, and no procedure claimproc of its claim is waiting as Not Received. The second condition keeps
		///a claim with a procedure that was un-received after posting (to enter it again) from showing as done.</summary>
		public static bool IsEnteredFromEraClaim(Hx835_Claim hx835_Claim,List<Hx835_ShortClaimProc> listClaimProcs,List<Etrans835Attach> listEtrans835Attaches) {
			if(hx835_Claim==null || hx835_Claim.ClaimNum==0 || listClaimProcs==null) {
				return false;
			}
			List<long> listAttachNums=GetAttachNumsForEraClaim(hx835_Claim,listEtrans835Attaches);
			if(listAttachNums.Count==0) {
				return false;
			}
			List<long> listClaimNums=listEtrans835Attaches.Where(x => listAttachNums.Contains(x.Etrans835AttachNum)).Select(x => x.ClaimNum).ToList();
			listClaimNums.Add(hx835_Claim.ClaimNum);
			bool isLinked=listClaimProcs.Any(x => listClaimNums.Contains(x.ClaimNum)
				&& listAttachNums.Contains(x.Etrans835AttachNum)
				&& ClaimProcs.CanHaveEtrans835AttachNum(x.Status));
			if(!isLinked) {
				return false;
			}
			bool isAnyProcNotReceived=listClaimProcs.Any(x => x.ClaimNum==hx835_Claim.ClaimNum
				&& x.ProcNum!=0
				&& x.Status==ClaimProcStatus.NotReceived);
			return !isAnyProcNotReceived;
		}

		///<summary>The claimprocs of the list, without those entered from a different ERA claim: a link to an existing etrans835attach row (in the list
		///of attaches) that is not one of this ERA claim's rows. What is left (claimprocs without a link, or with a link to a row that no longer exists)
		///goes through the matching that 24.3 used. Returns a new list.</summary>
		public static List<Hx835_ShortClaimProc> RemoveEnteredFromOtherEraClaims(Hx835_Claim hx835_Claim,List<Hx835_ShortClaimProc> listClaimProcs,
			List<Etrans835Attach> listEtrans835Attaches)
		{
			if(listClaimProcs==null) {
				return new List<Hx835_ShortClaimProc>();
			}
			if(hx835_Claim==null || listEtrans835Attaches==null) {
				return new List<Hx835_ShortClaimProc>(listClaimProcs);
			}
			List<long> listAttachNumsThis=GetAttachNumsForEraClaim(hx835_Claim,listEtrans835Attaches);
			HashSet<long> hashSetAttachNumsKnown=new HashSet<long>(listEtrans835Attaches.Select(x => x.Etrans835AttachNum).Where(x => x!=0));
			return listClaimProcs.FindAll(x => !IsEnteredFromOtherEraClaim(x,listAttachNumsThis,hashSetAttachNumsKnown));
		}

		private static bool IsEnteredFromOtherEraClaim(Hx835_ShortClaimProc claimProc,List<long> listAttachNumsThis,HashSet<long> hashSetAttachNumsKnown) {
			if(claimProc.Etrans835AttachNum==0 || listAttachNumsThis.Contains(claimProc.Etrans835AttachNum)) {
				return false;
			}
			return hashSetAttachNumsKnown.Contains(claimProc.Etrans835AttachNum);
		}

		///<summary>The claimprocs of the claim that were entered from this ERA claim and are still received. Used to warn before the same ERA claim is
		///posted twice.</summary>
		public static List<ClaimProc> GetEnteredFromEraClaim(Hx835_Claim hx835_Claim,List<ClaimProc> listClaimProcsForClaim,
			List<Etrans835Attach> listEtrans835Attaches)
		{
			if(listClaimProcsForClaim==null) {
				return new List<ClaimProc>();
			}
			List<long> listAttachNums=GetAttachNumsForEraClaim(hx835_Claim,listEtrans835Attaches);
			return listClaimProcsForClaim.FindAll(x => x.Etrans835AttachNum!=0
				&& listAttachNums.Contains(x.Etrans835AttachNum)
				&& ClaimProcs.CanHaveEtrans835AttachNum(x.Status));
		}

		///<summary>The other ERAs (EtransNums) whose payment has the same check or EFT trace number (TRN02) and payer as this ERA, and from whose
		///claims the claim's received claimprocs were entered: the same payment imported twice. A real supplemental payment, reversal or correction
		///comes with a payment of its own, so it is not listed. Claims twice in one ERA are not listed either (same EtransNum). Runs a query only when
		///such links exist.</summary>
		public static List<long> GetEtransNumsWithSamePayment(Hx835_Claim hx835_Claim,List<Hx835_ShortClaimProc> listClaimProcs,
			List<Etrans835Attach> listEtrans835Attaches)
		{
			if(hx835_Claim?.Era?.EtransSource==null || hx835_Claim.ClaimNum==0 || listClaimProcs==null || listEtrans835Attaches==null) {
				return new List<long>();
			}
			X835 x835=hx835_Claim.Era;
			string transRefNum=(x835.TransRefNum??"").Trim();
			if(transRefNum=="") {
				return new List<long>();
			}
			long etransNum=x835.EtransSource.EtransNum;
			List<long> listAttachNumsLinked=listClaimProcs
				.Where(x => x.ClaimNum==hx835_Claim.ClaimNum && x.Etrans835AttachNum!=0 && ClaimProcs.CanHaveEtrans835AttachNum(x.Status))
				.Select(x => x.Etrans835AttachNum)
				.ToList();
			List<long> listEtransNumsOther=listEtrans835Attaches
				.Where(x => listAttachNumsLinked.Contains(x.Etrans835AttachNum) && x.EtransNum!=etransNum)
				.Select(x => x.EtransNum)
				.Distinct()
				.ToList();
			if(listEtransNumsOther.Count==0) {
				return new List<long>();
			}
			List<Etrans835> listEtrans835s=Etrans835s.GetByEtransNums(listEtransNumsOther.ToArray());
			return listEtrans835s
				.Where(x => IsSamePayment(x,transRefNum,x835.PayerName))
				.Select(x => x.EtransNum)
				.Distinct()
				.ToList();
		}

		///<summary>True when the stored ERA has the given check or EFT trace number and payer name (both compared without case and outer spaces).
		///</summary>
		public static bool IsSamePayment(Etrans835 etrans835,string transRefNum,string payerName) {
			if(etrans835==null || string.IsNullOrWhiteSpace(transRefNum)) {
				return false;
			}
			if(!string.Equals((etrans835.TransRefNum??"").Trim(),transRefNum.Trim(),StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			return string.Equals((etrans835.PayerName??"").Trim(),(payerName??"").Trim(),StringComparison.OrdinalIgnoreCase);
		}
	}
}
