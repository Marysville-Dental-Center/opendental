using System.Collections.Generic;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Open Dental 25.4 (backlog package M2-PAYW): with the preference "Limit manual income transfers to the amount of patient payments" on,
	///saving an income transfer that takes more from a source than the patient payments it holds is blocked with a message that names the source
	///and the amount available. The rule itself is in IncomeTransferLimits. The splits that the Transfer button makes automatically are not
	///limited as long as the user saves them unchanged.</summary>
	public partial class FormPayment {
		///<summary>Fingerprint of the splits that the automatic Transfer button made last; null when it has not been used.</summary>
		private string _incomeTransferAutoFingerprint=null;

		///<summary>Called right after the Transfer button replaced the splits with an automatic transfer.</summary>
		private void IncomeTransferLimit_RememberAutomaticSplits() {
			_incomeTransferAutoFingerprint=IncomeTransferLimits.GetFingerprint(_listPaySplits);
		}

		///<summary>Called while saving an income transfer. Returns false, after telling the user why, when the transfer breaks the limit.</summary>
		private bool IncomeTransferLimit_IsValid() {
			if(!IncomeTransferLimits.IsEnabled()) {
				return true;
			}
			if(_incomeTransferAutoFingerprint!=null && _incomeTransferAutoFingerprint==IncomeTransferLimits.GetFingerprint(_listPaySplits)) {
				return true;//The automatic transfer, unchanged.
			}
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(_payment.PayNum,_listPaySplits);
			if(listViolations.Count==0) {
				return true;
			}
			using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(IncomeTransferLimits.GetMessage(listViolations));
			msgBoxCopyPaste.ShowDialog();
			return false;
		}
	}
}
