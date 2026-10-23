// DentalXChange XConnect claim validation window (Open Dental 25.2, v25_2-account-xconnect-claim-validation).
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
using OpenDentBusiness.VendorDirect.DentalXChange;

namespace OpenDental {
	///<summary>Lists the problems DentalXChange's XConnect validation returned for one claim, or for a batch grouped by claim. Unresolved rows are
	///yellow; problems a re-validation no longer returns turn white and are marked Resolved. Send Anyway shows only on a send path and only when a
	///claim has warnings and no errors; claims with errors stay unsent.</summary>
	public partial class FormXConnectValidation:FormODBase {
		///<summary>Where the window was opened from, which decides what Edit Claim and Send Anyway do.</summary>
		public enum EnumOpenedFrom {
			///<summary>After Edit Claim saved the claim. Non-modal; Edit Claim opens the claim.</summary>
			Save,
			///<summary>From Edit Claim's Send. Modal; Edit Claim closes this window and returns to the claim.</summary>
			ClaimEditSend,
			///<summary>From the Send Claims window. Modal; Edit Claim opens the claim.</summary>
			ClaimsSend,
		}

		private List<XConnectValidationResult> _listResults;
		private readonly EnumOpenedFrom _openedFrom;
		///<summary>True when the user chose Send Anyway (warning-only claims may be sent).</summary>
		public bool IsSendAnywayClicked;
		///<summary>True when, opened from Edit Claim's Send, the user chose Edit Claim.</summary>
		public bool IsEditClaimClicked;

		public FormXConnectValidation(List<XConnectValidationResult> listResults,EnumOpenedFrom openedFrom) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_listResults=listResults??new List<XConnectValidationResult>();
			_openedFrom=openedFrom;
		}

		///<summary>The results as they stand, after any re-validation.</summary>
		public List<XConnectValidationResult> ListResults {
			get {
				return _listResults;
			}
		}

		private void FormXConnectValidation_Load(object sender,EventArgs e) {
			FillHeader();
			FillGrid();
		}

		private void FillHeader() {
			if(_listResults.Count==1) {
				XConnectValidationResult result=_listResults[0];
				labelHeader.Text=Lan.g(this,"Patient:")+" "+result.PatientName+"    "
					+Lan.g(this,"Claim date:")+" "+(result.DateService.Year>1880 ? result.DateService.ToShortDateString() : "")+"    "
					+Lan.g(this,"Carrier:")+" "+result.CarrierName;
			}
			else {
				labelHeader.Text=Lan.g(this,"Claims validated:")+" "+_listResults.Count+".  "
					+Lan.g(this,"Claims with errors stay in the queue unsent.");
			}
			butSendAnyway.Visible=_openedFrom!=EnumOpenedFrom.Save
				&& _listResults.Any(x => XConnectValidation.GetSendDecision(x)==XConnectSendDecision.ConfirmWarnings);
		}

		private void FillGrid() {
			bool isBatch=_listResults.Count>1;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			if(isBatch) {
				gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Claim"),170));
			}
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Severity"),60));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Location"),110));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Message"),isBatch ? 220 : 300));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"How to fix"),isBatch ? 210 : 290));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Status"),70));
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listResults.Count;i++) {
				XConnectValidationResult result=_listResults[i];
				string claimText=result.PatientName+" "+(result.DateService.Year>1880 ? result.DateService.ToShortDateString() : "");
				if(result.IsCallFailed) {
					GridRow gridRow=new GridRow();
					if(isBatch) {
						gridRow.Cells.Add(claimText);
					}
					gridRow.Cells.Add(Lan.g(this,"Error"));
					gridRow.Cells.Add("XConnect");
					gridRow.Cells.Add(result.CallFailedMessage);
					gridRow.Cells.Add(Lan.g(this,"Re-validate when DentalXChange answers again."));
					gridRow.Cells.Add("");
					gridRow.ColorBackG=Color.LightYellow;
					gridRow.Tag=result;
					gridMain.ListGridRows.Add(gridRow);
				}
				for(int j=0;j<result.ListIssues.Count;j++) {
					XConnectValidationIssue issue=result.ListIssues[j];
					GridRow gridRow=new GridRow();
					if(isBatch) {
						gridRow.Cells.Add(claimText);
					}
					gridRow.Cells.Add(issue.Severity==XConnectIssueSeverity.Error ? Lan.g(this,"Error") : Lan.g(this,"Warning"));
					gridRow.Cells.Add(issue.Location);
					gridRow.Cells.Add(issue.Message);
					gridRow.Cells.Add(issue.HowToFix);
					gridRow.Cells.Add(issue.IsResolved ? Lan.g(this,"Resolved") : "");
					gridRow.ColorBackG=issue.IsResolved ? Color.White : Color.LightYellow;
					gridRow.Tag=result;
					gridMain.ListGridRows.Add(gridRow);
				}
				if(!result.IsCallFailed && result.ListIssues.Count==0 && isBatch) {
					GridRow gridRow=new GridRow();
					gridRow.Cells.Add(claimText);
					gridRow.Cells.Add("");
					gridRow.Cells.Add("");
					gridRow.Cells.Add(result.IsSkipped ? result.SkippedReason : Lan.g(this,"No problems found."));
					gridRow.Cells.Add("");
					gridRow.Cells.Add("");
					gridRow.Tag=result;
					gridMain.ListGridRows.Add(gridRow);
				}
			}
			gridMain.EndUpdate();
		}

		///<summary>The claim of the selected row, or the only claim.</summary>
		private XConnectValidationResult GetSelectedResult() {
			XConnectValidationResult result=gridMain.SelectedTag<XConnectValidationResult>();
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
			XConnectValidationResult result=GetSelectedResult();
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
			Revalidate(new List<XConnectValidationResult> { result });
		}

		private void butRevalidate_Click(object sender,EventArgs e) {
			Revalidate(_listResults.ToList());
		}

		///<summary>Validates the given claims again and merges each result into the list, so problems that went away show as resolved.</summary>
		private void Revalidate(List<XConnectValidationResult> listResultsToRevalidate) {
			Dictionary<long,XConnectValidationResult> dictResultsLatest=new Dictionary<long,XConnectValidationResult>();
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Communicating with DentalXChange...");
			progressWin.ActionMain=() => {
				for(int i=0;i<listResultsToRevalidate.Count;i++) {
					dictResultsLatest[listResultsToRevalidate[i].ClaimNum]=ClaimL.ValidateXConnect(listResultsToRevalidate[i].ClaimNum,XConnect.TimeoutValidateOnSend);
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
				XConnectValidationResult resultLatest;
				if(!dictResultsLatest.TryGetValue(_listResults[i].ClaimNum,out resultLatest) || resultLatest==null) {
					continue;
				}
				_listResults[i]=XConnectValidation.MergeRevalidation(_listResults[i],resultLatest);
			}
			FillHeader();
			FillGrid();
		}

		private void butSendAnyway_Click(object sender,EventArgs e) {
			if(_listResults.Any(x => XConnectValidation.GetSendDecision(x)==XConnectSendDecision.Block
				|| XConnectValidation.GetSendDecision(x)==XConnectSendDecision.CallFailed))
			{
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Claims with errors stay in the queue. Send the claims that have only warnings?")) {
					return;
				}
			}
			IsSendAnywayClicked=true;
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
