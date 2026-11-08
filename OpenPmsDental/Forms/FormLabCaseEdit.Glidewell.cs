// Open Dental 26.2 (P262-GLIDEWELL): the Glidewell part of the Edit Lab Case window: the account and scanner, the lab case's products, sending the
// lab case to Glidewell and checking its status. Shown only when the office turned the Glidewell lab cases program link on.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace OpenPmsDental {
	public partial class FormLabCaseEdit {
		///<summary>True when the Glidewell group is in use (the program link is on). Nothing Glidewell is read or saved otherwise.</summary>
		private bool _isGlidewellShown;
		///<summary>The lab case's products as edited; saved with the lab case (SaveToDb).</summary>
		private List<LabCaseProduct> _listLabCaseProducts=new List<LabCaseProduct>();
		private List<GlidewellAcct> _listGlidewellAccts=new List<GlidewellAcct>();
		///<summary>The lab case's row in glidewellmsg, or null when it was not sent.</summary>
		private GlidewellMsg _glidewellMsg;
		///<summary>True once the user picked an account, so a change of provider no longer picks that provider's default.</summary>
		private bool _isGwAccountPicked;

		private void LoadGlidewell(Patient patient) {
			if(!GlidewellProgram.IsEnabled()) {
				groupGlidewell.Visible=false;
				return;
			}
			_isGlidewellShown=true;
			groupGlidewell.Visible=true;
			_listGlidewellAccts=GlidewellAccts.GetAll();
			_glidewellMsg=GlidewellMsgs.GetForLabCase(LabCaseCur.LabCaseNum);
			_listLabCaseProducts=LabCaseProducts.GetForLabCase(LabCaseCur.LabCaseNum);
			comboGwScanner.Items.Clear();
			comboGwScanner.Items.Add(GlidewellLabCases.VENDOR_NAME_ITERO,GlidewellScanner.ITero);
			comboGwScanner.Items.Add(GlidewellLabCases.VENDOR_NAME_MEDIT,GlidewellScanner.Medit);
			comboGwScanner.SelectedIndex=0;
			comboGwAccount.Items.Clear();
			for(int i=0;i<_listGlidewellAccts.Count;i++) {
				comboGwAccount.Items.Add(_listGlidewellAccts[i].AccountNumber,_listGlidewellAccts[i]);
			}
			if(_glidewellMsg==null) {
				SelectGwAccount(GlidewellAccts.GetDefaultAcctNum(LabCaseCur.ProvNum,_listGlidewellAccts));
			}
			else {
				SelectGwAccount(_glidewellMsg.GlidewellAcctNum);
				comboGwScanner.SelectedIndex=(GlidewellLabCases.GetScanner(_glidewellMsg)==GlidewellScanner.Medit) ? 1 : 0;
			}
			comboProv.SelectionChangeCommitted+=comboProv_SelectionChangeCommittedGlidewell;
			FillGwProducts();
			FillGwStatus();
		}

		private void SelectGwAccount(long glidewellAcctNum) {
			comboGwAccount.SelectedIndex=_listGlidewellAccts.FindIndex(x => x.GlidewellAcctNum==glidewellAcctNum);
		}

		private void comboGwAccount_SelectionChangeCommitted(object sender,EventArgs e) {
			_isGwAccountPicked=true;
		}

		private void comboProv_SelectionChangeCommittedGlidewell(object sender,EventArgs e) {
			if(!_isGlidewellShown || _glidewellMsg!=null || _isGwAccountPicked || comboProv.SelectedIndex==-1) {
				return;
			}
			SelectGwAccount(GlidewellAccts.GetDefaultAcctNum(_listProviders[comboProv.SelectedIndex].ProvNum,_listGlidewellAccts));
		}

		private void FillGwProducts() {
			gridGwProducts.BeginUpdate();
			gridGwProducts.Columns.Clear();
			gridGwProducts.Columns.Add(new GridColumn(Lan.g("TableLabCaseProducts","Material"),90));
			gridGwProducts.Columns.Add(new GridColumn(Lan.g("TableLabCaseProducts","Shade"),45));
			gridGwProducts.Columns.Add(new GridColumn(Lan.g("TableLabCaseProducts","Teeth"),60));
			gridGwProducts.Columns.Add(new GridColumn(Lan.g("TableLabCaseProducts","Notes"),100));
			gridGwProducts.ListGridRows.Clear();
			for(int i=0;i<_listLabCaseProducts.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(_listLabCaseProducts[i].Material);
				row.Cells.Add(_listLabCaseProducts[i].Shade);
				row.Cells.Add(_listLabCaseProducts[i].ToothRange);
				row.Cells.Add(_listLabCaseProducts[i].Notes);
				row.Tag=_listLabCaseProducts[i];
				gridGwProducts.ListGridRows.Add(row);
			}
			gridGwProducts.EndUpdate();
		}

		private void FillGwStatus() {
			bool isSent=(_glidewellMsg!=null);
			comboGwAccount.Enabled=!isSent;
			comboGwScanner.Enabled=!isSent;
			butGwDetails.Visible=isSent;
			if(isSent) {
				butGwSend.Text=Lan.g(this,"Check Status");
				labelGwStatus.Text=GlidewellLabCases.GetStatusLine(_glidewellMsg);
				string scanInstruction=GlidewellLabCases.GetScanInstruction(_glidewellMsg);
				if(scanInstruction!="") {
					labelGwStatus.Text+="\r\n"+scanInstruction;
				}
				return;
			}
			butGwSend.Text=Lan.g(this,"Send to Glidewell");
			labelGwStatus.Text=GlidewellLabCases.GetStatusLine(null);
			if(_listGlidewellAccts.Count==0) {
				labelGwStatus.Text+="\r\n"+Lan.g(this,"Add the office's Glidewell account in Setup, Program Links, Glidewell lab cases.");
			}
			else if(!GlidewellClients.Current.IsAvailable) {
				labelGwStatus.Text+="\r\n"+Lan.g(this,"Sending to Glidewell is not available in this version yet (Send to Glidewell says why).");
			}
		}

		///<summary>True when the lab case has a row in glidewellmsg (read from the database, so it is right even when the group is hidden).</summary>
		private bool IsSentToGlidewell() {
			if(_glidewellMsg!=null) {
				return true;
			}
			if(LabCaseCur==null || LabCaseCur.LabCaseNum==0) {
				return false;
			}
			return GlidewellMsgs.GetForLabCase(LabCaseCur.LabCaseNum)!=null;
		}

		///<summary>Saves the products with the lab case. Only while the Glidewell group is in use.</summary>
		private void SaveGlidewellProducts() {
			if(!_isGlidewellShown) {
				return;
			}
			_listLabCaseProducts=LabCaseProducts.Sync(LabCaseCur.LabCaseNum,LabCaseCur.PatNum,_listLabCaseProducts);
			FillGwProducts();
		}

		private void butGwProductAdd_Click(object sender,EventArgs e) {
			LabCaseProduct labCaseProduct=new LabCaseProduct();
			labCaseProduct.LabCaseNum=LabCaseCur.LabCaseNum;
			labCaseProduct.PatNum=LabCaseCur.PatNum;
			using FormLabCaseProductEdit formLabCaseProductEdit=new FormLabCaseProductEdit();
			formLabCaseProductEdit.LabCaseProductCur=labCaseProduct;
			formLabCaseProductEdit.IsNew=true;
			if(formLabCaseProductEdit.ShowDialog(this)!=DialogResult.OK || formLabCaseProductEdit.IsDeleted) {
				return;
			}
			_listLabCaseProducts.Add(labCaseProduct);
			FillGwProducts();
		}

		private void gridGwProducts_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridGwProducts.ListGridRows.Count) {
				return;
			}
			LabCaseProduct labCaseProduct=(LabCaseProduct)gridGwProducts.ListGridRows[e.Row].Tag;
			LabCaseProduct labCaseProductEdit=labCaseProduct.Copy();
			using FormLabCaseProductEdit formLabCaseProductEdit=new FormLabCaseProductEdit();
			formLabCaseProductEdit.LabCaseProductCur=labCaseProductEdit;
			if(formLabCaseProductEdit.ShowDialog(this)!=DialogResult.OK) {
				return;
			}
			int index=_listLabCaseProducts.IndexOf(labCaseProduct);
			if(formLabCaseProductEdit.IsDeleted) {
				_listLabCaseProducts.RemoveAt(index);
			}
			else {
				_listLabCaseProducts[index]=labCaseProductEdit;
			}
			FillGwProducts();
		}

		private void butGwSend_Click(object sender,EventArgs e) {
			if(_glidewellMsg!=null) {
				CheckGlidewellStatus();
				return;
			}
			GlidewellAcct glidewellAcct=comboGwAccount.GetSelected<GlidewellAcct>();
			GlidewellScanner glidewellScanner=comboGwScanner.GetSelected<GlidewellScanner>();
			//What can be told before anything is saved: the account and whether this version can send at all.
			List<string> listProblems=GlidewellLabCases.GetAccountProblems(glidewellAcct);
			if(!GlidewellClients.Current.IsAvailable) {
				listProblems.Add(GlidewellClients.Current.UnavailableReason);
			}
			if(listProblems.Count>0) {
				MessageBox.Show(this,string.Join("\r\n\r\n",listProblems));
				return;
			}
			if(!SaveToDb()) {
				return;
			}
			Patient patient=Patients.GetPat(LabCaseCur.PatNum);
			listProblems=GlidewellLabCases.GetSendProblems(LabCaseCur,patient,glidewellAcct,GlidewellMsgs.GetForLabCase(LabCaseCur.LabCaseNum),
				GlidewellClients.Current);
			if(listProblems.Count>0) {
				MessageBox.Show(this,string.Join("\r\n\r\n",listProblems));
				return;
			}
			string question=Lan.g(this,"Send this lab case to Glidewell with account")+" "+glidewellAcct.AccountNumber+" "
				+Lan.g(this,"for a scan from the")+" "+GlidewellLabCases.GetVendorName(glidewellScanner)+" "+Lan.g(this,"scanner?")+"\r\n\r\n"
				+Lan.g(this,"Sent to Glidewell: the patient's name, patient number, gender and birthdate, the provider's number and license number, and "
				+"the lab case's number, instructions and products.");
			if(MessageBox.Show(this,question,"",MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return;
			}
			GlidewellLabCaseResult result=null;
			long labCaseNum=LabCaseCur.LabCaseNum;
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Sending to Glidewell...");
			progressWin.ShowCancelButton=false;
			progressWin.ActionMain=() => {
				result=GlidewellLabCases.Send(labCaseNum,glidewellAcct.GlidewellAcctNum,glidewellScanner);
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				result=GlidewellLabCaseResult.Refused(ex.Message);
			}
			if(result==null) {
				return;
			}
			if(result.GlidewellMsg!=null) {
				_glidewellMsg=result.GlidewellMsg;
			}
			FillGwStatus();
			MessageBox.Show(this,result.Message);
		}

		private void CheckGlidewellStatus() {
			GlidewellLabCaseResult result=null;
			long labCaseNum=LabCaseCur.LabCaseNum;
			ProgressWin progressWin=new ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Checking the status at Glidewell...");
			progressWin.ShowCancelButton=false;
			progressWin.ActionMain=() => {
				result=GlidewellLabCases.CheckStatus(labCaseNum);
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				result=GlidewellLabCaseResult.Refused(ex.Message);
			}
			if(result==null) {
				return;
			}
			if(result.GlidewellMsg!=null) {
				_glidewellMsg=result.GlidewellMsg;
			}
			FillGwStatus();
			MessageBox.Show(this,result.Message);
		}

		private void butGwDetails_Click(object sender,EventArgs e) {
			if(_glidewellMsg==null) {
				return;
			}
			GlidewellAcct glidewellAcct=_listGlidewellAccts.FirstOrDefault(x => x.GlidewellAcctNum==_glidewellMsg.GlidewellAcctNum);
			using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(GlidewellLabCases.GetDetailsText(_glidewellMsg,glidewellAcct));
			msgBoxCopyPaste.ShowDialog(this);
		}
	}
}
