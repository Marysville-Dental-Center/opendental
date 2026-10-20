// The explicit adoption tool (backlog Risk R1, package M1-03). Normal startup never adopts a database; an administrator opens this window from
// the startup message for a database on the adoption allowlist and runs it against a restored copy.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeBase;
using DataConnectionBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormAdoptDatabase:FormODBase {
		private AdoptionPreflight _adoptionPreflight;
		private bool _isAdopted;

		public FormAdoptDatabase() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormAdoptDatabase_Load(object sender,EventArgs e) {
			labelExplain.Text=Lan.g(this,"Adoption lets this program use a database that Open Dental created. It is done once, on a restored copy, "
				+"never on the live database. It drops the office-only patient column _portal_active_email and its unique index, deletes Open Dental's "
				+"update files from the database without reading them, blanks the addresses of Open Dental's servers, and sets DataBaseVersion and "
				+"ProgramVersion to")+" "+ForkVersions.SentinelVersion.ToString(4)+" "
				+Lan.g(this,"so Open Dental programs can no longer use the copy. Then this program's version markers take over. "
				+"MyISAM tables have no transactions: take a backup first. Going back means restoring that backup.");
			RefreshPreflight();
		}

		private void RefreshPreflight() {
			try {
				_adoptionPreflight=Adoption.Preflight();
			}
			catch(Exception ex) {
				_adoptionPreflight=new AdoptionPreflight();
				_adoptionPreflight.Message=ex.Message;
			}
			labelDatabase.Text=Lan.g(this,"Database:")+" "+DataConnection.GetDatabaseName()+"   "+Lan.g(this,"Server:")+" "+DataConnection.GetServerName();
			labelStatus.Text=_adoptionPreflight.Message;
			bool canAdopt=_adoptionPreflight.CanAdopt && !_isAdopted;
			textConfirmName.Enabled=canAdopt;
			checkRestoredCopy.Enabled=canAdopt;
			butAdopt.Enabled=canAdopt;
		}

		private void butAdopt_Click(object sender,EventArgs e) {
			if(!checkRestoredCopy.Checked) {
				MsgBox.Show(this,"Confirm that this is a restored copy and that you have a backup.");
				return;
			}
			if(textConfirmName.Text.Trim()!=DataConnection.GetDatabaseName()) {
				MsgBox.Show(this,"Type the database name exactly to confirm.");
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Adopt this database now? Open Dental programs will no longer be able to use it.")) {
				return;
			}
			List<string> listLog=new List<string>();
			try {
				ProgressWin progressWin=new ProgressWin();
				progressWin.StartingMessage=Lan.g(this,"Adopting the database...");
				progressWin.ShowCancelButton=false;
				progressWin.ActionMain=() => Adoption.Run(x => {
					lock(listLog) {
						listLog.Add(x);
					}
					ODEvent.Fire(ODEventType.ProgressBar,x);
				});
				progressWin.ShowDialog();
				_isAdopted=ForkVersions.ReadState().IsForkMarked;
			}
			catch(Exception ex) {
				listLog.Add(Lan.g(this,"Adoption stopped:")+" "+ex.Message);
				listLog.Add(Lan.g(this,"Every step can be run again: fix the cause and click Adopt again, or restore the backup."));
			}
			textLog.Text=string.Join("\r\n",listLog);
			RefreshPreflight();
			if(_isAdopted) {
				MsgBox.Show(this,"The database was adopted.");
			}
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=_isAdopted ? DialogResult.OK : DialogResult.Cancel;
		}

		private void FormAdoptDatabase_FormClosing(object sender,FormClosingEventArgs e) {
			if(_isAdopted) {
				DialogResult=DialogResult.OK;
			}
		}
	}
}
