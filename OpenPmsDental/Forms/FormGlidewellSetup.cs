// Open Dental 26.2 (P262-GLIDEWELL): Setup > Program Links > Glidewell lab cases. Turns the program link on or off and lists the office's
// Glidewell accounts (glidewellacct). Vendor-neutral: it says what is sent and where, and nothing more.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDental.VendorDirect;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace OpenPmsDental {
	///<summary>The Glidewell lab cases program link: the Enabled box and the office's Glidewell accounts. Accounts are saved as soon as their own
	///window is saved; Save here only stores Enabled.</summary>
	public partial class FormGlidewellSetup:FormODBase {
		private Program _program;
		private List<GlidewellAcct> _listGlidewellAccts=new List<GlidewellAcct>();

		public FormGlidewellSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormGlidewellSetup_Load(object sender,EventArgs e) {
			if(GlidewellProgram.EnsureProgram()) {
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
			}
			_program=Programs.GetCur(ProgramName.Glidewell);
			if(_program==null) {
				MsgBox.Show(this,"The Glidewell lab cases program link could not be created.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			checkEnabled.Checked=_program.Enabled;
			checkBaaOnFile.Checked=GlidewellProgram.IsBaaOnFile();
			labelBaaConfirmed.Text=checkBaaOnFile.Checked ? GetBaaConfirmedText() : "";
			IGlidewellClient glidewellClient=GlidewellClients.Current;
			if(glidewellClient.IsAvailable) {
				labelTransport.Text="";
			}
			else {
				labelTransport.Text=glidewellClient.UnavailableReason;
			}
			FillGrid();
		}

		private void FillGrid() {
			_listGlidewellAccts=GlidewellAccts.GetAll();
			List<Provider> listProviders=Providers.GetDeepCopy();
			gridAccounts.BeginUpdate();
			gridAccounts.Columns.Clear();
			gridAccounts.Columns.Add(new GridColumn(Lan.g("TableGlidewellAccts","Account Number"),120));
			gridAccounts.Columns.Add(new GridColumn(Lan.g("TableGlidewellAccts","API Key"),90));
			gridAccounts.Columns.Add(new GridColumn(Lan.g("TableGlidewellAccts","Default for Providers"),200));
			gridAccounts.ListGridRows.Clear();
			for(int i=0;i<_listGlidewellAccts.Count;i++) {
				GlidewellAcct glidewellAcct=_listGlidewellAccts[i];
				string providers=string.Join(", ",listProviders
					.Where(x => x.GlidewellAcctNum==glidewellAcct.GlidewellAcctNum)
					.Select(x => x.Abbr));
				GridRow row=new GridRow();
				row.Cells.Add(glidewellAcct.AccountNumber);
				row.Cells.Add(GlidewellAccts.GetApiKeyStatusText(glidewellAcct));
				row.Cells.Add(providers);
				row.Tag=glidewellAcct;
				gridAccounts.ListGridRows.Add(row);
			}
			gridAccounts.EndUpdate();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			using FormGlidewellAcctEdit formGlidewellAcctEdit=new FormGlidewellAcctEdit(new GlidewellAcct());
			if(formGlidewellAcctEdit.ShowDialog(this)==DialogResult.OK) {
				FillGrid();
			}
		}

		private void gridAccounts_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridAccounts.ListGridRows.Count) {
				return;
			}
			GlidewellAcct glidewellAcct=(GlidewellAcct)gridAccounts.ListGridRows[e.Row].Tag;
			using FormGlidewellAcctEdit formGlidewellAcctEdit=new FormGlidewellAcctEdit(glidewellAcct.Copy());
			if(formGlidewellAcctEdit.ShowDialog(this)==DialogResult.OK) {
				FillGrid();
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(checkEnabled.Checked && !_program.Enabled && !_listGlidewellAccts.Any(x => GlidewellAccts.IsComplete(x))) {
				MsgBox.Show(this,"Add a Glidewell account with its account number and API key before enabling.");
				return;
			}
			if(_program.Enabled!=checkEnabled.Checked) {
				Program programOld=_program.Copy();
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program,programOld);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,_program.ProgDesc+" "+(_program.Enabled ? "enabled." : "disabled."));
			}
			//Owner decision of 2026-11-07: no patient data goes to Glidewell until this is recorded (the postcard service's rule).
			if(checkBaaOnFile.Checked!=GlidewellProgram.IsBaaOnFile()) {
				GlidewellProgram.SaveSetting(GlidewellProgram.PropertyDescs.BaaOnFile,checkBaaOnFile.Checked ? "1" : "0");
				string confirmed=checkBaaOnFile.Checked ? (Security.CurUser.UserName+", "+DateTime.Now.ToString()) : "";
				GlidewellProgram.SaveSetting(GlidewellProgram.PropertyDescs.BaaConfirmed,confirmed);
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Glidewell lab cases: business associate agreement "
					+(checkBaaOnFile.Checked ? "recorded as on file." : "no longer recorded as on file."));
			}
			DataValid.SetInvalid(InvalidType.Programs);
			DialogResult=DialogResult.OK;
		}

		private string GetBaaConfirmedText() {
			string confirmed=GlidewellProgram.GetSetting(GlidewellProgram.PropertyDescs.BaaConfirmed);
			if(confirmed=="") {
				return "";
			}
			return Lan.g(this,"Recorded by")+" "+confirmed;
		}
	}

	///<summary>Registers the Glidewell lab cases setup with Setup > Program Links (found by ProgramLinkSetupRegistry).</summary>
	public class GlidewellProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.Glidewell;
			}
		}

		public bool EnsureProgram() {
			return GlidewellProgram.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using FormGlidewellSetup formGlidewellSetup=new FormGlidewellSetup();
			return formGlidewellSetup.ShowDialog(owner);
		}
	}
}
