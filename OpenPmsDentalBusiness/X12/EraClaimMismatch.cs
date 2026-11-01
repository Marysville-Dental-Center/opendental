// B-ERA (Open Dental 24.4, already allowed since 24.3.43): an ERA claim can be matched by hand to a claim whose fee or date of service differs.
// The ERA claim select window warns with OK/Cancel instead of refusing; these warnings now name the value the ERA expects
// (docs/specs/features-24.4.md, v24_4-manage-era-mismatch-match).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The fee and date of service checks of FormEtrans835ClaimSelect, and their warning texts.</summary>
	public static class EraClaimMismatch {
		private const string LAN_CLASS="FormEtrans835ClaimSelect";

		///<summary>The ERA claim's total charge (CLP03). A reversal reports it negative, so its absolute value is compared.</summary>
		public static double GetEraClaimFee(Hx835_Claim claimPaid) {
			double fee=(double)claimPaid.ClaimFee;
			if(claimPaid.IsReversal) {
				fee=Math.Abs(fee);
			}
			return fee;
		}

		///<summary>True when the claim's fee differs from the ERA claim's charge.</summary>
		public static bool IsClaimFeeMismatch(Claim claim,Hx835_Claim claimPaid) {
			return !CompareDouble.IsEqual(claim.ClaimFee,GetEraClaimFee(claimPaid));
		}

		///<summary>True when the claim's date of service is outside the ERA claim's service date range. A preauthorization whose ERA reports no
		///date (some payers send 01/01/1900) never mismatches.</summary>
		public static bool IsDateOfServiceMismatch(Claim claim,Hx835_Claim claimPaid) {
			if(claim.ClaimType=="PreAuth" && claimPaid.DateServiceStart.Date.Year<=1900) {
				return false;
			}
			return claim.DateService.Date<claimPaid.DateServiceStart.Date || claim.DateService.Date>claimPaid.DateServiceEnd.Date;
		}

		///<summary>The OK/Cancel question when the claim fee does not match. Shows both fees.</summary>
		public static string GetClaimFeeMismatchMessage(double claimFeeOnClaim,double claimFeeOnEra) {
			return Lans.g(LAN_CLASS,"Claim fee on claim does not match ERA.")+"\r\n"
				+Lans.g(LAN_CLASS,"Claim fee on claim:")+" "+claimFeeOnClaim.ToString("f2")+"\r\n"
				+Lans.g(LAN_CLASS,"Expected from ERA:")+" "+claimFeeOnEra.ToString("f2")+"\r\n"
				+Lans.g(LAN_CLASS,"Continue anyway?");
		}

		///<summary>The OK/Cancel question when the procedures of the selected claim do not add up to a split ERA claim (one the payer reported in
		///several CLP segments).</summary>
		public static string GetSplitClaimFeeMismatchMessage(double claimFeeOnEra) {
			return Lans.g(LAN_CLASS,"The procedures on the selected claim do not match this split ERA claim.")+"\r\n"
				+Lans.g(LAN_CLASS,"Expected from ERA:")+" "+claimFeeOnEra.ToString("f2")+" "+Lans.g(LAN_CLASS,"in matching procedures")+"\r\n"
				+Lans.g(LAN_CLASS,"Continue anyway?");
		}

		///<summary>The OK/Cancel question when the date of service does not match. Shows the claim's date and the ERA's range.</summary>
		public static string GetDateOfServiceMismatchMessage(DateTime dateServiceClaim,DateTime dateServiceEraStart,DateTime dateServiceEraEnd) {
			string expected=dateServiceEraStart.ToShortDateString();
			if(dateServiceEraEnd.Date>dateServiceEraStart.Date) {
				expected+=" "+Lans.g(LAN_CLASS,"to")+" "+dateServiceEraEnd.ToShortDateString();
			}
			return Lans.g(LAN_CLASS,"Date of service on claim does not match service date range on ERA.")+"\r\n"
				+Lans.g(LAN_CLASS,"Date of service on claim:")+" "+dateServiceClaim.ToShortDateString()+"\r\n"
				+Lans.g(LAN_CLASS,"Expected from ERA:")+" "+expected+"\r\n"
				+Lans.g(LAN_CLASS,"Continue anyway?");
		}
	}
}
