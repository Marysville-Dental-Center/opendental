// Open Dental 26.2 (P262-GLIDEWELL): one Glidewell account: its account number, its API key (stored encrypted, never shown again) and the
// providers who send to it by default (provider.GlidewellAcctNum).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect;

namespace OpenPmsDental {
	///<summary>Adds or edits one Glidewell account. The API key is changed only when typed (blank keeps it) and only by a user with permission to
	///manage high security program properties, like every other integration secret. Saved at once; Delete refuses an account lab cases were sent
	///with.</summary>
	public partial class FormGlidewellAcctEdit:FormODBase {
		private readonly GlidewellAcct _glidewellAcct;
		private SecretReadResult _secretReadResult;
		private bool _canEditApiKey;
		private List<Provider> _listProviders;

		public FormGlidewellAcctEdit(GlidewellAcct glidewellAcct) {
			_glidewellAcct=glidewellAcct;
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormGlidewellAcctEdit_Load(object sender,EventArgs e) {
			textAccountNumber.Text=_glidewellAcct.AccountNumber;
			_secretReadResult=GlidewellAccts.ReadApiKey(_glidewellAcct);
			_canEditApiKey=Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties,true);
			textApiKey.ReadOnly=!_canEditApiKey;
			FillApiKeyStatus();
			if(_glidewellAcct.DateTimeExpires.Year>1880) {
				textLastSignIn.Text=_glidewellAcct.DateTimeExpires.ToString();
			}
			List<long> listProvNumsDefault=GlidewellAccts.GetProvNumsForAcct(_glidewellAcct.GlidewellAcctNum);
			//Hidden providers are listed only when this account is already their default.
			_listProviders=Providers.GetWhere(x => !x.IsHidden || listProvNumsDefault.Contains(x.ProvNum));
			for(int i=0;i<_listProviders.Count;i++) {
				string text=_listProviders[i].Abbr+" - "+_listProviders[i].GetLongDesc();
				listProviders.Items.Add(text,_listProviders[i]);
				if(listProvNumsDefault.Contains(_listProviders[i].ProvNum)) {
					listProviders.SetSelected(i,true);
				}
			}
			butDelete.Visible=(_glidewellAcct.GlidewellAcctNum!=0);
		}

		private void FillApiKeyStatus() {
			if(!_canEditApiKey) {
				labelApiKeyStatus.Text=Lan.g(this,"You need the permission to manage high security program properties to change the API key.");
			}
			else if(_secretReadResult.HasValue) {
				labelApiKeyStatus.Text=Lan.g(this,"An API key is saved. Leave blank to keep it.");
			}
			else if(_secretReadResult.NeedsReentry) {
				labelApiKeyStatus.Text=Lan.g(this,"The saved API key can't be read here. Enter it again.");
			}
			else {
				labelApiKeyStatus.Text=Lan.g(this,"Not set.");
			}
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete this Glidewell account?")) {
				return;
			}
			try {
				GlidewellAccts.Delete(_glidewellAcct.GlidewellAcctNum);
			}
			catch(ApplicationException ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Glidewell account "+_glidewellAcct.AccountNumber+" deleted.");
			DataValid.SetInvalid(InvalidType.Providers);
			DialogResult=DialogResult.OK;
		}

		private void butSave_Click(object sender,EventArgs e) {
			string accountNumber=textAccountNumber.Text.Trim();
			if(accountNumber=="") {
				MsgBox.Show(this,"Enter the account number.");
				return;
			}
			if(GlidewellAccts.GetAll().Any(x => x.GlidewellAcctNum!=_glidewellAcct.GlidewellAcctNum
				&& string.Equals(x.AccountNumber.Trim(),accountNumber,StringComparison.OrdinalIgnoreCase)))
			{
				MsgBox.Show(this,"Another Glidewell account has this account number.");
				return;
			}
			string apiKeyEntered=textApiKey.Text.Trim();
			if(apiKeyEntered!="" && !_canEditApiKey) {
				MsgBox.Show(this,"You need the permission to manage high security program properties to change the API key.");
				return;
			}
			//The key box takes more than API_KEY_MAX_LENGTH characters on purpose: a text box cuts a pasted text at its MaxLength without a word, so a
			//longer key has to reach SaveAccount and be refused here rather than be shortened and saved.
			GlidewellApiKeyResult glidewellApiKeyResult=GlidewellAccts.SaveAccount(_glidewellAcct,accountNumber,apiKeyEntered);
			if(glidewellApiKeyResult==GlidewellApiKeyResult.TooLong) {
				MsgBox.Show(this,"The API key is too long to store (at most 127 characters).");
				return;
			}
			if(glidewellApiKeyResult!=GlidewellApiKeyResult.Ok) {
				MsgBox.Show(this,"The API key could not be encrypted, so nothing was saved.");
				return;
			}
			List<long> listProvNums=listProviders.GetListSelected<Provider>().Select(x => x.ProvNum).ToList();
			GlidewellAccts.SetProvidersForAcct(_glidewellAcct.GlidewellAcctNum,listProvNums);
			DataValid.SetInvalid(InvalidType.Providers);
			DialogResult=DialogResult.OK;
		}
	}
}
