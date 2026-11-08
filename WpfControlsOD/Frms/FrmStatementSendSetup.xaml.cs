// ODOPTS: Open Dental 25.1's Statement Send Setup window, for Open Dental's StatementAutoSendEmail and StatementAutoSendSMS rows (the meaning of
// their values is INFERRED, docs/port-notes/ODOPTS.md). Opened from Billing Defaults.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Input;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace OpenPmsDental {
	///<summary>Whether statements are sent by e-mail and by text when billing creates them: Never, Ask or Always (StatementAutoSendMode). Texts
	///need a texting provider, which this program does not have; the window says so.</summary>
	public partial class FrmStatementSendSetup:FrmODBase {
		///<summary>The modes in the order of the combo boxes.</summary>
		private static readonly List<StatementAutoSendMode> _listModes=new List<StatementAutoSendMode> {
			StatementAutoSendMode.Never,StatementAutoSendMode.Ask,StatementAutoSendMode.Always };

		public FrmStatementSendSetup() {
			InitializeComponent();
			Load+=FrmStatementSendSetup_Load;
			PreviewKeyDown+=FrmStatementSendSetup_PreviewKeyDown;
		}

		private void FrmStatementSendSetup_Load(object sender,EventArgs e) {
			Lang.F(this);
			for(int i=0;i<_listModes.Count;i++) {
				comboEmail.Items.Add(StatementAutoSend.GetDescription(_listModes[i]));
				comboSms.Items.Add(StatementAutoSend.GetDescription(_listModes[i]));
			}
			comboEmail.SelectedIndex=_listModes.IndexOf(StatementAutoSend.GetEmailMode());
			comboSms.SelectedIndex=_listModes.IndexOf(StatementAutoSend.GetSmsMode());
			labelTextNeedsProvider.Visible=StatementAutoSend.TextSender==null;
		}

		private void FrmStatementSendSetup_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			StatementAutoSendMode modeEmailOld=StatementAutoSend.GetEmailMode();
			StatementAutoSendMode modeSmsOld=StatementAutoSend.GetSmsMode();
			StatementAutoSendMode modeEmail=_listModes[Math.Max(0,comboEmail.SelectedIndex)];
			StatementAutoSendMode modeSms=_listModes[Math.Max(0,comboSms.SelectedIndex)];
			if(StatementAutoSend.SetModes(modeEmail,modeSms)) {
				OdOptions.LogChange(EnumPermType.Setup,"Statement Send Setup: by e-mail",StatementAutoSend.PREF_EMAIL,
					StatementAutoSend.GetDescription(modeEmailOld),StatementAutoSend.GetDescription(modeEmail));
				OdOptions.LogChange(EnumPermType.Setup,"Statement Send Setup: by text",StatementAutoSend.PREF_SMS,
					StatementAutoSend.GetDescription(modeSmsOld),StatementAutoSend.GetDescription(modeSms));
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			IsDialogOK=true;
		}
	}
}
