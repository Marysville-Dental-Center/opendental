// Remembered SSH host keys (docs/specs/ssh-host-keys.md, section 5): Setup, SSH Host Keys.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>The SSH host key the program stores for each SSH server (SFTP image storage, TSI, the Denti-Cal and Mercury clearinghouses, the HL7
	///SFTP mode). A server whose key a connection refused shows the stored and the new key; Accept Changed Key replaces the stored key with the
	///refused one, Remove forgets the stored key so the next connection stores the key it gets. Needs SecurityAdmin, to open (the callers check) and
	///for both changes (SshHostKeys checks it again). Every change is in the audit trail under SSH Host Keys.</summary>
	public partial class FormSshHostKeys:FormODBase {
		private List<SshHostKey> _listSshHostKeys=new List<SshHostKey>();

		public FormSshHostKeys() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSshHostKeys_Load(object sender,EventArgs e) {
			FillGrid();
		}

		private void FillGrid() {
			long sshHostKeyNumSelected=gridMain.SelectedTag<SshHostKey>()?.SshHostKeyNum??0;
			_listSshHostKeys=SshHostKeys.GetAll();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Server"),180));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Key Type"),130));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Fingerprint (SHA-256)"),330));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Stored"),200));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Changed Key Refused"),200){ IsWidthDynamic=true });
			gridMain.ListGridRows.Clear();
			int indexSelect=-1;
			for(int i=0;i<_listSshHostKeys.Count;i++) {
				SshHostKey sshHostKey=_listSshHostKeys[i];
				GridRow row=new GridRow();
				row.Cells.Add(SshHostKeys.GetServerText(sshHostKey));
				row.Cells.Add(sshHostKey.KeyType);
				row.Cells.Add(sshHostKey.FingerprintSha256);
				row.Cells.Add(GetStoredText(sshHostKey));
				string refused="";
				if(sshHostKey.HasRefusedKey()) {
					refused=sshHostKey.DateTRefused.ToString()+": "+SshHostKeys.GetRefusedKeyText(sshHostKey);
					row.ColorBackG=Color.FromArgb(255,215,215);//A connection was refused: the administrator should look at it.
				}
				row.Cells.Add(refused);
				row.Tag=sshHostKey;
				gridMain.ListGridRows.Add(row);
				if(sshHostKey.SshHostKeyNum==sshHostKeyNumSelected) {
					indexSelect=i;
				}
			}
			gridMain.EndUpdate();
			if(indexSelect>=0) {
				gridMain.SetSelected(indexSelect,true);
			}
			FillSelected();
		}

		///<summary>"2026-11-02 10:15 by Admin, first use" or "..., accepted".</summary>
		private string GetStoredText(SshHostKey sshHostKey) {
			string user=sshHostKey.UserNum==0 ? Lan.g(this,"a service") : Userods.GetName(sshHostKey.UserNum);
			string how=sshHostKey.IsAccepted ? Lan.g(this,"accepted") : Lan.g(this,"first use");
			return sshHostKey.DateTStored.ToString()+" "+Lan.g(this,"by")+" "+user+", "+how;
		}

		///<summary>The selected server's stored and refused keys in full, and which buttons apply.</summary>
		private void FillSelected() {
			SshHostKey sshHostKey=gridMain.SelectedTag<SshHostKey>();
			if(sshHostKey==null) {
				textServer.Text="";
				textStoredKey.Text="";
				textRefusedKey.Text="";
				butAccept.Enabled=false;
				butRemove.Enabled=false;
				return;
			}
			textServer.Text=SshHostKeys.GetServerText(sshHostKey);
			textStoredKey.Text=SshHostKeys.GetStoredKeyText(sshHostKey);
			textRefusedKey.Text=sshHostKey.HasRefusedKey() ? SshHostKeys.GetRefusedKeyText(sshHostKey) : Lan.g(this,"(none refused)");
			butAccept.Enabled=sshHostKey.HasRefusedKey();
			butRemove.Enabled=true;
		}

		private void gridMain_CellClick(object sender,ODGridClickEventArgs e) {
			FillSelected();
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void butAccept_Click(object sender,EventArgs e) {
			SshHostKey sshHostKey=gridMain.SelectedTag<SshHostKey>();
			if(sshHostKey==null || !sshHostKey.HasRefusedKey()) {
				MsgBox.Show(this,"Select a server whose changed key was refused.");
				return;
			}
			string message=Lan.g(this,"Accept the new SSH host key of")+" "+SshHostKeys.GetServerText(sshHostKey)+"?\r\n\r\n"
				+Lan.g(this,"Stored key (old)")+": "+SshHostKeys.GetStoredKeyText(sshHostKey)+"\r\n"
				+Lan.g(this,"Refused key (new)")+": "+SshHostKeys.GetRefusedKeyText(sshHostKey)+"\r\n\r\n"
				+Lan.g(this,"Accept it only if the new fingerprint is the one the server's administrator gives you. Otherwise someone else may be "
				+"answering for the server.");
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,message)) {
				return;
			}
			try {
				SshHostKeys.AcceptRefusedKey(sshHostKey.SshHostKeyNum,sshHostKey.RefusedKeyType,sshHostKey.RefusedFingerprintSha256);
			}
			catch(ApplicationException ex) {
				MessageBox.Show(ex.Message);
			}
			FillGrid();
		}

		private void butRemove_Click(object sender,EventArgs e) {
			SshHostKey sshHostKey=gridMain.SelectedTag<SshHostKey>();
			if(sshHostKey==null) {
				MsgBox.Show(this,"Select a server first.");
				return;
			}
			string message=Lan.g(this,"Remove the stored SSH host key of")+" "+SshHostKeys.GetServerText(sshHostKey)+"?\r\n\r\n"
				+SshHostKeys.GetStoredKeyText(sshHostKey)+"\r\n\r\n"
				+Lan.g(this,"The next connection to the server stores whatever key it presents then, without asking.");
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,message)) {
				return;
			}
			try {
				SshHostKeys.Delete(sshHostKey.SshHostKeyNum);
			}
			catch(ApplicationException ex) {
				MessageBox.Show(ex.Message);
			}
			FillGrid();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
