// Send Claims window: EDS claim validation before a batch send (Open Dental 26.1, v26_1-manage-eds-claim-validation).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormClaimsSend {
		///<summary>Validates the selected claims that go to EDS (Logic/EdsValidationL) and shows one validation window, grouped by claim, for those with
		///problems. Then deselects the claims that stay in the queue: errors, and warnings or an unreachable EDS unless the user chose Send Anyway.
		///Claims that were deleted, or are no longer waiting to send (for example sent or deleted from Edit Claim in the validation window), are
		///deselected and dropped from the grid, never sent again. hqClearinghouseNum is the clearinghouse picked from the list, or 0. Returns false
		///if nothing is left to send or the user cancelled.</summary>
		private bool ApplyEdsResultsToSelection(long hqClearinghouseNum) {
			List<ClaimSendQueueItem> listClaimSendQueueItemsSelected=gridMain.SelectedTags<ClaimSendQueueItem>();
			EdsValidationL.BatchOutcome batchOutcome=EdsValidationL.ValidateBatchForSend(listClaimSendQueueItemsSelected,hqClearinghouseNum);
			if(batchOutcome.IsCancelled) {
				return false;
			}
			if(batchOutcome.ListClaimNumsValidated.Count==0 && batchOutcome.ListClaimNumsNotWaiting.Count==0) {
				return true;
			}
			for(int i=0;i<gridMain.ListGridRows.Count;i++) {
				if(!gridMain.SelectedIndices.Contains(i)) {
					continue;
				}
				ClaimSendQueueItem claimSendQueueItem=(ClaimSendQueueItem)gridMain.ListGridRows[i].Tag;
				if(batchOutcome.ListClaimNumsNotWaiting.Contains(claimSendQueueItem.ClaimNum)
					|| (batchOutcome.ListClaimNumsValidated.Contains(claimSendQueueItem.ClaimNum)
					&& !batchOutcome.ListClaimNumsAllowed.Contains(claimSendQueueItem.ClaimNum)))
				{
					gridMain.SetSelected(i,false);
				}
			}
			if(batchOutcome.ListClaimNumsNotWaiting.Count>0) {
				MsgBox.Show(this,"WARNING: One or more claims were sent, deleted or put on hold since they were selected.  They will not be sent again and will be removed from the grid.");
				FillGrid(true);//Drops them from the grid; the other selected claims stay selected.
			}
			if(gridMain.SelectedTags<ClaimSendQueueItem>().Count==0) {
				MsgBox.Show(this,"No claims to send.");
				return false;
			}
			return true;
		}
	}
}
