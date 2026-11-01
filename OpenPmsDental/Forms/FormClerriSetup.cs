// V-BRIDGE2 (Open Dental 25.4, v25_4-family-clerri): the setup window for the Clerri program link (the office's own Client Key, per clinic).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.VendorDirect;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental {
	///<summary>Setup > Program Links > Clerri. The fields of the public setup page: Clinic (only with clinics on), Enabled, Show Toolbar Button (not
	///changeable while enabled), the read-only Internal Name and Description, and the Client Key (osd1$, masked, high security; never shown again).
	///Plus the dashboard address the office gets from Clerri, because Clerri's address is not public (partner-gated). No eConnector status line:
	///the eConnector is removed. Vendor-neutral: no links, logos or sign-up text.</summary>
	public partial class FormClerriSetup:FormODBase {
		private Program _program;
		private bool _canEditSecret;
		///<summary>The clinic whose Client Key the key box shows. 0 is the default.</summary>
		private long _clinicNumShown;
		///<summary>Keys typed for a clinic and not saved yet, by ClinicNum.</summary>
		private Dictionary<long,string> _dictKeysTyped=new Dictionary<long,string>();
		///<summary>Clinics whose saved key is to be cleared on save.</summary>
		private HashSet<long> _hashSetKeysCleared=new HashSet<long>();

		public FormClerriSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormClerriSetup_Load(object sender,EventArgs e) {
			if(ClerriProgram.EnsureProgram()) {
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
			}
			ToolButItems.RefreshCache();
			_program=Programs.GetCur(ProgramName.Clerri);
			if(_program==null) {
				MsgBox.Show(this,"The Clerri program link could not be created.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			textProgName.Text=_program.ProgName;
			textProgDesc.Text=_program.ProgDesc;
			checkEnabled.Checked=_program.Enabled;
			checkShowToolbarButton.Checked=ClerriProgram.IsToolbarButtonShown(_program.ProgramNum);
			SetToolbarButtonEditable();
			textDashboardAddress.Text=ProgramProperties.GetValOrDefault(_program.ProgramNum,ClerriProgram.PropDashboardAddress,0);
			_canEditSecret=ProgramProperties.CanEditProperties(ProgramProperties.GetForProgram(_program.ProgramNum));
			textClientKey.ReadOnly=!_canEditSecret;
			butClearKey.Enabled=_canEditSecret;
			labelClinic.Visible=PrefC.HasClinicsEnabled;
			comboClinic.Visible=PrefC.HasClinicsEnabled;
			if(PrefC.HasClinicsEnabled) {
				//Fill the picker now; it fills itself (and selects the current clinic) in its own OnLoad, which has not run yet.
				comboClinic.SetUser(Security.CurUser);
			}
			_clinicNumShown=GetClinicNumSelected();
			FillKeyStatus();
		}

		private void FormClerriSetup_Shown(object sender,EventArgs e) {
			if(_program==null || !PrefC.HasClinicsEnabled) {
				return;
			}
			//The picker's own OnLoad may have selected another clinic after Load. Show the key of the clinic the picker shows.
			StashTypedKey();
			_clinicNumShown=GetClinicNumSelected();
			FillKeyStatus();
		}

		///<summary>The clinic the picker shows, or 0 (the default) when clinics are off.</summary>
		private long GetClinicNumSelected() {
			if(!PrefC.HasClinicsEnabled) {
				return 0;
			}
			return Math.Max(0,comboClinic.ClinicNumSelected);
		}

		private void SetToolbarButtonEditable() {
			checkShowToolbarButton.Enabled=ClerriProgram.CanChangeToolbarButton(_program.Enabled,checkEnabled.Checked);
		}

		private void checkEnabled_CheckedChanged(object sender,EventArgs e) {
			if(_program==null) {
				return;
			}
			SetToolbarButtonEditable();
		}

		///<summary>Keeps what was typed for the clinic shown so far, so switching clinics loses nothing.</summary>
		private void StashTypedKey() {
			string typed=textClientKey.Text.Trim();
			if(typed=="") {
				return;
			}
			_dictKeysTyped[_clinicNumShown]=typed;
			_hashSetKeysCleared.Remove(_clinicNumShown);
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			StashTypedKey();
			_clinicNumShown=GetClinicNumSelected();
			FillKeyStatus();
		}

		private void FillKeyStatus() {
			string typed;
			textClientKey.Text=_dictKeysTyped.TryGetValue(_clinicNumShown,out typed) ? typed : "";
			if(!_canEditSecret) {
				labelKeyStatus.Text=Lan.g(this,"You need the permission to manage high security program properties to change the Client Key.");
				return;
			}
			if(_hashSetKeysCleared.Contains(_clinicNumShown)) {
				labelKeyStatus.Text=Lan.g(this,"The key will be cleared when you save.");
				return;
			}
			SecretReadResult secretReadResult=ClerriProgram.ReadClientKeyExact(_program.ProgramNum,_clinicNumShown);
			if(secretReadResult.HasValue) {
				labelKeyStatus.Text=Lan.g(this,"A key is saved. Leave blank to keep it.");
			}
			else if(secretReadResult.NeedsReentry) {
				labelKeyStatus.Text=Lan.g(this,"The saved key can't be read. Enter it again.");
			}
			else if(_clinicNumShown!=0) {
				labelKeyStatus.Text=Lan.g(this,"Not set. This clinic uses the default key.");
			}
			else {
				labelKeyStatus.Text=Lan.g(this,"Not set.");
			}
		}

		private void butClearKey_Click(object sender,EventArgs e) {
			_dictKeysTyped.Remove(_clinicNumShown);
			textClientKey.Text="";
			_hashSetKeysCleared.Add(_clinicNumShown);
			FillKeyStatus();
		}

		///<summary>True if, after saving, some clinic or the default would have a Client Key.</summary>
		private bool WillHaveAnyKey() {
			if(_dictKeysTyped.Count>0) {
				return true;
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetWhere(x => x.ProgramNum==_program.ProgramNum
				&& x.PropertyDesc==ClerriProgram.PropClientKey);
			return listProgramProperties.Any(x => !_hashSetKeysCleared.Contains(x.ClinicNum)
				&& SecretValue.Read(x.PropertyValue,LegacyPolicy.AcceptPlaintext).HasValue);
		}

		private void butSave_Click(object sender,EventArgs e) {
			StashTypedKey();
			string dashboardAddress=textDashboardAddress.Text.Trim();
			if(dashboardAddress!="" || checkEnabled.Checked) {
				string error=ClerriProgram.ValidateDashboardAddress(dashboardAddress);
				if(error!=null) {
					MsgBox.Show(this,error);
					return;
				}
			}
			if(checkEnabled.Checked && ClerriProgram.ContainsTag(dashboardAddress,ClerriProgram.TagClientKey) && !WillHaveAnyKey()) {
				MsgBox.Show(this,"Enter the Client Key from Clerri before enabling.");
				return;
			}
			if((_dictKeysTyped.Count>0 || _hashSetKeysCleared.Count>0) && !_canEditSecret) {
				MsgBox.Show(this,"You need the permission to manage high security program properties to change the Client Key.");
				return;
			}
			//Keys to write: typed ones, cleared ones (blank), and plain-text ones (typed into the generic property editor or carried over), which are
			//encrypted now.
			Dictionary<long,string> dictKeysToWrite=new Dictionary<long,string>(_dictKeysTyped);
			foreach(long clinicNum in _hashSetKeysCleared) {
				dictKeysToWrite[clinicNum]="";
			}
			if(_canEditSecret) {
				List<ProgramProperty> listProgramPropertiesKey=ProgramProperties.GetWhere(x => x.ProgramNum==_program.ProgramNum
					&& x.PropertyDesc==ClerriProgram.PropClientKey);
				for(int i=0;i<listProgramPropertiesKey.Count;i++) {
					SecretReadResult secretReadResult=SecretValue.Read(listProgramPropertiesKey[i].PropertyValue,LegacyPolicy.AcceptPlaintext);
					if(secretReadResult.NeedsReEncrypt && !dictKeysToWrite.ContainsKey(listProgramPropertiesKey[i].ClinicNum)) {
						dictKeysToWrite[listProgramPropertiesKey[i].ClinicNum]=secretReadResult.PlainText;
					}
				}
			}
			foreach(KeyValuePair<long,string> keyValuePair in dictKeysToWrite) {
				if(!VendorCredentials.WriteSecret(_program.ProgramNum,ClerriProgram.PropClientKey,keyValuePair.Key,keyValuePair.Value)) {
					MsgBox.Show(this,"The Client Key could not be encrypted, so it was not saved.");
					return;
				}
			}
			if(_dictKeysTyped.Count>0 || _hashSetKeysCleared.Count>0) {
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,
					_program.ProgDesc+"'s "+ClerriProgram.PropClientKey+" was altered.",_program.ProgramNum,DateTime.Now);
			}
			VendorCredentials.WriteSetting(_program.ProgramNum,ClerriProgram.PropDashboardAddress,0,dashboardAddress);
			//Show Toolbar Button is saved whenever the link was saved as disabled, even if Enabled was checked afterwards (which locks the box but
			//keeps the value shown), and before Enabled is saved. So what the window shows is what is saved.
			ClerriProgram.SaveToolbarButtonAndEnabled(_program,checkShowToolbarButton.Checked,checkEnabled.Checked);
			DataValid.SetInvalid(InvalidType.Programs,InvalidType.ToolButsAndMounts);
			DialogResult=DialogResult.OK;
		}
	}

	///<summary>Registers the Clerri setup with Setup > Program Links (found by ProgramLinkSetupRegistry).</summary>
	public class ClerriProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.Clerri;
			}
		}

		public bool EnsureProgram() {
			return ClerriProgram.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using FormClerriSetup formClerriSetup=new FormClerriSetup();
			return formClerriSetup.ShowDialog(owner);
		}
	}
}
