// EDS claim validation window (Open Dental 26.1, v26_1-manage-eds-claim-validation).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.Eds;

namespace OpenDental {
	///<summary>Lists every problem found for one claim that goes to EDS, or for a batch grouped by claim: the local checks and what EDS returned.
	///Unresolved rows are yellow; problems a re-validation no longer finds turn white and are marked Resolved. On a send path, Send Anyway shows when
	///a claim has only warnings or EDS could not be reached; claims with errors stay unsent.</summary>
	public partial class FormEdsValidation:FormODBase {
		///<summary>Where the window was opened from, which decides what Edit Claim and Send do.</summary>
		public enum EnumOpenedFrom {
			///<summary>After Edit Claim saved a new claim. Non-modal; nothing is sent from here.</summary>
			Save,
			///<summary>From Edit Claim's Send. Modal; Edit Claim closes this window and returns to the claim.</summary>
			ClaimEditSend,
			///<summary>From the Send Claims window. Modal; Edit Claim opens the claim.</summary>
			ClaimsSend,
		}

		private List<EdsValidationResult> _listResults;
		private readonly EnumOpenedFrom _openedFrom;
		private readonly long _hqClearinghouseNumOverride;
		///<summary>True when the user chose Send Anyway (or Send, once nothing is left to confirm).</summary>
		public bool IsSendClicked;
		///<summary>True when, opened from Edit Claim's Send, the user chose Edit Claim.</summary>
		public bool IsEditClaimClicked;

		///<summary>hqClearinghouseNumOverride: the clearinghouse the Send Claims window sends through, or 0 for each claim's own.</summary>
		public FormEdsValidation(List<EdsValidationResult> listResults,EnumOpenedFrom openedFrom,long hqClearinghouseNumOverride) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_listResults=listResults??new List<EdsValidationResult>();
			_openedFrom=openedFrom;
			_hqClearinghouseNumOverride=hqClearinghouseNumOverride;
		}

		///<summary>The results as they stand, after any re-validation.</summary>
		public List<EdsValidationResult> ListResults {
			get {
				return _listResults;
			}
		}

		private void FormEdsValidation_Load(object sender,EventArgs e) {
			FillHeader();
			FillGrid();
		}

		private void FillHeader() {
			if(_listResults.Count==1) {
				EdsValidationResult result=_listResults[0];
				labelHeader.Text=Lan.g(this,"Patient:")+" "+result.PatientName+"    "
					+Lan.g(this,"Claim date:")+" "+(result.DateService.Year>1880 ? result.DateService.ToShortDateString() : "")+"    "
					+Lan.g(this,"Carrier:")+" "+result.CarrierName;
				if(!string.IsNullOrWhiteSpace(result.RemoteNote)) {
					labelHeader.Text+="\r\n"+result.RemoteNote;
				}
			}
			else {
				labelHeader.Text=Lan.g(this,"Claims validated:")+" "+_listResults.Count+".  "
					+Lan.g(this,"Claims with errors stay in the queue unsent. Close sends only the claims without problems.");
			}
			if(_openedFrom==EnumOpenedFrom.Save) {
				labelHeader.Text+="\r\n"+Lan.g(this,"The claim was saved. It is validated again when it is sent.");
			}
			bool canConfirm=_listResults.Any(x => EdsClaimValidation.GetSendDecision(x)==EdsSendDecision.ConfirmWarnings
				|| EdsClaimValidation.GetSendDecision(x)==EdsSendDecision.ConfirmUnreachable);
			bool isAllClear=_listResults.All(x => EdsClaimValidation.GetSendDecision(x)==EdsSendDecision.Send);
			butSend.Visible=_openedFrom!=EnumOpenedFrom.Save && (canConfirm || isAllClear);
			butSend.Text=isAllClear ? Lan.g(this,"&Send") : Lan.g(this,"&Send Anyway");
		}

		private void FillGrid() {
			bool isBatch=_listResults.Count>1;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			if(isBatch) {
				gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Claim"),150));
			}
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Severity"),60));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Source"),50));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Location"),130));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Message"),isBatch ? 230 : 300));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"How to fix"),isBatch ? 180 : 250));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Status"),60));
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listResults.Count;i++) {
				EdsValidationResult result=_listResults[i];
				string claimText=result.PatientName+" "+(result.DateService.Year>1880 ? result.DateService.ToShortDateString() : "");
				if(result.IsCallFailed) {
					AddRow(isBatch,claimText,Lan.g(this,"Warning"),"EDS",Lan.g(this,"Claim"),result.CallFailedMessage,
						_openedFrom==EnumOpenedFrom.Save ? Lan.g(this,"The claim is validated again when it is sent.")
							: Lan.g(this,"Re-validate when EDS answers again, or send anyway."),
						"",Color.LightYellow,result);
				}
				for(int j=0;j<result.ListIssues.Count;j++) {
					EdsValidationIssue issue=result.ListIssues[j];
					AddRow(isBatch,claimText,issue.Severity==EdsIssueSeverity.Error ? Lan.g(this,"Error") : Lan.g(this,"Warning"),
						issue.Source==EdsIssueSource.Eds ? "EDS" : Lan.g(this,"Local"),issue.Location,issue.Message,issue.HowToFix,
						issue.IsResolved ? Lan.g(this,"Resolved") : "",issue.IsResolved ? Color.White : Color.LightYellow,result);
				}
				if(!result.IsCallFailed && result.ListIssues.Count==0) {
					AddRow(isBatch,claimText,"","","",result.IsSkipped ? result.SkippedReason : Lan.g(this,"No problems found."),"","",Color.White,result);
				}
			}
			gridMain.EndUpdate();
		}

		private void AddRow(bool isBatch,string claimText,string severity,string source,string location,string message,string howToFix,string status,
			Color colorBack,EdsValidationResult result)
		{
			GridRow gridRow=new GridRow();
			if(isBatch) {
				gridRow.Cells.Add(claimText);
			}
			gridRow.Cells.Add(severity);
			gridRow.Cells.Add(source);
			gridRow.Cells.Add(location);
			gridRow.Cells.Add(message);
			gridRow.Cells.Add(howToFix);
			gridRow.Cells.Add(status);
			gridRow.ColorBackG=colorBack;
			gridRow.Tag=result;
			gridMain.ListGridRows.Add(gridRow);
		}

		///<summary>The claim of the selected row, or the only claim.</summary>
		private EdsValidationResult GetSelectedResult() {
			EdsValidationResult result=gridMain.SelectedTag<EdsValidationResult>();
			if(result==null && _listResults.Count==1) {
				result=_listResults[0];
			}
			return result;
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			EditClaim();
		}

		private void butEditClaim_Click(object sender,EventArgs e) {
			EditClaim();
		}

		private void EditClaim() {
			EdsValidationResult result=GetSelectedResult();
			if(result==null) {
				MsgBox.Show(this,"Select a claim first.");
				return;
			}
			if(_openedFrom==EnumOpenedFrom.ClaimEditSend) {
				IsEditClaimClicked=true;
				DialogResult=DialogResult.Cancel;
				return;
			}
			Claim claim=Claims.GetClaim(result.ClaimNum);
			if(claim==null) {
				MsgBox.Show(this,"The claim has been deleted.");
				return;
			}
			using FormClaimEdit formClaimEdit=new FormClaimEdit(claim,Patients.GetPat(claim.PatNum),Patients.GetFamily(claim.PatNum));
			formClaimEdit.ShowDialog();
			Revalidate(new List<EdsValidationResult> { result });
		}

		private void butRevalidate_Click(object sender,EventArgs e) {
			Revalidate(_listResults.ToList());
		}

		///<summary>Validates the given claims again and merges each result into the list, so problems that went away show as resolved.</summary>
		private void Revalidate(List<EdsValidationResult> listResultsToRevalidate) {
			Dictionary<long,EdsValidationResult> dictResultsLatest=new Dictionary<long,EdsValidationResult>();
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Validating with EDS...");
			progressWin.ActionMain=() => {
				for(int i=0;i<listResultsToRevalidate.Count;i++) {
					dictResultsLatest[listResultsToRevalidate[i].ClaimNum]=EdsClaimValidation.ValidateClaimNum(listResultsToRevalidate[i].ClaimNum,
						_hqClearinghouseNumOverride,EDS.TimeoutValidateOnSend);
				}
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Re-validation failed."),ex);
				return;
			}
			for(int i=0;i<_listResults.Count;i++) {
				EdsValidationResult resultLatest;
				if(!dictResultsLatest.TryGetValue(_listResults[i].ClaimNum,out resultLatest) || resultLatest==null) {
					continue;
				}
				_listResults[i]=EdsClaimValidation.MergeRevalidation(_listResults[i],resultLatest);
			}
			FillHeader();
			FillGrid();
		}

		private void butSend_Click(object sender,EventArgs e) {
			if(_listResults.Any(x => EdsClaimValidation.GetSendDecision(x)==EdsSendDecision.Block)) {
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Claims with errors stay in the queue. Send the claims that have only warnings?")) {
					return;
				}
			}
			IsSendClicked=true;
			DialogResult=DialogResult.OK;
		}

		private void butClose_Click(object sender,EventArgs e) {
			if(Modal) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			Close();
		}
	}
}
