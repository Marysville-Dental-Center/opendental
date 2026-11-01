// B-CLAIM, v25_2-account-medical-eob-prompt: when a medical or institutional claim is received, offer to attach its EOB to the patient's primary
// dental claims that are still Unsent or Hold Until Pri Received. Runs before the existing "prompt for primary claims" question, so the attachment
// goes out with the dental claim. Off by default; turned on in Preferences > Account > Claim Receive (ClaimAttaches.IsMedicalEobPromptEnabled).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	public class MedicalEobL {
		///<summary>Asks, once per primary dental claim, whether to attach the received medical claim's EOB. Yes attaches the EOB files of the
		///medical claim's payment when there are any (the user can choose a file instead), otherwise asks for a file to import. Errors are shown and
		///skipped: the prompt never blocks receiving or sending. Does nothing unless the preference is on and claimMedical is a medical or
		///institutional claim with such dental claims. Never called from ERA auto-processing, which has no user.</summary>
		public static void PromptToAttachMedicalEob(Claim claimMedical,List<ClaimProc> listClaimProcsMedical) {
			try {
				if(!ClaimAttaches.IsMedicalEobPromptEnabled() || !ClaimAttaches.IsMedicalClaim(claimMedical)) {
					return;
				}
				List<Claim> listClaimsDental=ClaimAttaches.GetDentalClaimsForMedicalEob(claimMedical,listClaimProcsMedical);
				if(listClaimsDental.Count==0) {
					return;
				}
				List<EobAttach> listEobAttaches=ClaimAttaches.GetMedicalEobAttaches(claimMedical.ClaimNum);
				for(int i=0;i<listClaimsDental.Count;i++) {
					if(!PromptForOneClaim(listClaimsDental[i],listEobAttaches)) {
						return;
					}
				}
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g("MedicalEobL","The medical EOB could not be attached."),ex);
			}
		}

		///<summary>The question and the attaching for one dental claim. False stops asking about the remaining claims (no A to Z folder).</summary>
		private static bool PromptForOneClaim(Claim claimDental,List<EobAttach> listEobAttaches) {
			string carrierName="";
			InsPlan insPlan=InsPlans.RefreshOne(claimDental.PlanNum);
			if(insPlan!=null) {
				carrierName=Carriers.GetName(insPlan.CarrierNum);
			}
			string question=Lan.g("MedicalEobL","Attach the medical EOB to the primary dental claim?")+"\r\n"
				+Lan.g("MedicalEobL","Date of service:")+" "+claimDental.DateService.ToShortDateString()
				+(string.IsNullOrWhiteSpace(carrierName) ? "" : "\r\n"+Lan.g("MedicalEobL","Carrier:")+" "+carrierName);
			if(!MsgBox.Show(MsgBoxButtons.YesNo,question,Lan.g("MedicalEobL","Medical EOB"))) {
				return true;
			}
			if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
				MsgBox.Show(Lan.g("MedicalEobL","Claim attachments need the A to Z images folder. Attach the EOB from the claim instead."));
				return false;
			}
			if(listEobAttaches.Count>0) {
				string questionFiles=Lan.g("MedicalEobL","The medical claim's payment has this many EOB files:")+" "+listEobAttaches.Count+"\r\n"
					+Lan.g("MedicalEobL","Attach them? Choose No to pick a file instead.");
				if(MsgBox.Show(MsgBoxButtons.YesNo,questionFiles,Lan.g("MedicalEobL","Medical EOB"))) {
					for(int i=0;i<listEobAttaches.Count;i++) {
						TryAttach(() => ClaimAttaches.AttachMedicalEob(claimDental.ClaimNum,listEobAttaches[i]));
					}
					return true;
				}
			}
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Multiselect=false;
			openFileDialog.Title=Lan.g("MedicalEobL","Select the medical EOB");
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				return true;
			}
			string filePath=openFileDialog.FileName;
			TryAttach(() => ClaimAttaches.AttachFileToClaim(claimDental.ClaimNum,filePath,
				ClaimAttaches.MEDICAL_EOB_DISPLAY_PREFIX+Path.GetFileName(filePath)));
			return true;
		}

		private static void TryAttach(Func<ClaimAttach> funcAttach) {
			try {
				funcAttach();
			}
			catch(ODException ex) {
				MsgBox.Show(ex.Message);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g("MedicalEobL","The medical EOB could not be attached."),ex);
			}
		}
	}
}
