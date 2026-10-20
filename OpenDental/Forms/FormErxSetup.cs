using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;
using System.Linq;
using OpenDental.UI;

namespace OpenDental {
	///<summary>This form is used to make changes for the eRx program link: the option (DoseSpot, NewCrop, or both), and each vendor's own settings,
	///which the office gets from its agreement with the vendor. Keys and passwords are encrypted (osd1$) before they are saved, in masked,
	///high-security rows. Nothing here contacts a vendor except the Test Connection buttons.</summary>
	public partial class FormErxSetup:FormODBase {

		private Program _program;
		private ErxOption _erxOption;
		///<summary>Every eRx row, as stored: secrets are osd1$ values (or the previous program's plain text until the next save).</summary>
		private List<ProgramProperty> _listProgramProperties=new List<ProgramProperty>();
		///<summary>True if this user may change keys and passwords (ManageHighSecurityProgProperties).</summary>
		private bool _canEditSecrets;
		///<summary>The subscription key as loaded, to tell whether the user changed it.</summary>
		private string _subscriptionKeyLoaded="";
		///<summary>The NewCrop password as loaded, to tell whether the user changed it.</summary>
		private string _newCropPasswordLoaded="";
		///<summary>NPI to "enabled for NewCrop", as loaded and as edited.</summary>
		private Dictionary<string,bool> _dictNewCropEnabledLoaded=new Dictionary<string,bool>();
		private Dictionary<string,bool> _dictNewCropEnabled=new Dictionary<string,bool>();

		private ProgramProperty ErxOptionPP {
			get {
				ProgramProperty programProperty=_listProgramProperties.FirstOrDefault(x => x.PropertyDesc==Erx.PropertyDescs.ErxOption);
				if(programProperty==null) {
					throw new Exception("The database is missing an eRx option program property.");
				}
				return programProperty;
			}
		}

		public FormErxSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormErxSetup_Load(object sender,EventArgs e) {
			try {
				_program=Programs.GetCur(ProgramName.eRx);
				if(_program==null) {
					throw new Exception("The eRx bridge is missing from the database.");
				}
				//Rows added in this version (environments, the subscription key, NewCrop's settings) are inserted here if missing, before the
				//list is loaded: ProgramProperties.Sync on save deletes rows the list doesn't have.
				DoseSpotConfig.EnsureProperties();
				NewCropConfig.InsertMissingProperties();
				ProgramProperties.RefreshCache();
				_listProgramProperties=ProgramProperties.GetForProgram(_program.ProgramNum);
				_canEditSecrets=Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties,true);
				checkEnabled.Checked=_program.Enabled;
				_erxOption=PIn.Enum<ErxOption>(ErxOptionPP.PropertyValue);
				if(_erxOption==ErxOption.NewCrop) {
					radioNewCrop.Checked=true;
				}
				else if(_erxOption==ErxOption.DoseSpot) {
					radioDoseSpot.Checked=true;
				}
				else if(_erxOption==ErxOption.DoseSpotWithNewCrop) {
					radioDoseSpotLegacy.Checked=true;
				}
				LoadDoseSpot();
				LoadNewCrop();
				if(!_canEditSecrets) {
					textSubscriptionKey.ReadOnly=true;
					textPassword.ReadOnly=true;
					checkShowSubscriptionKey.Enabled=false;
					checkShowPassword.Enabled=false;
				}
				SetRadioButtonChecked(_erxOption);
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"Error loading the eRx program: ")+ex.Message);
				DialogResult=DialogResult.Cancel;
				return;
			}
		}

		#region DoseSpot
		private void LoadDoseSpot() {
			comboDoseSpotEnvironment.Items.Clear();
			comboDoseSpotEnvironment.Items.Add(Lan.g(this,"Production"));
			comboDoseSpotEnvironment.Items.Add(Lan.g(this,"Staging"));
			DoseSpotEnvironment doseSpotEnvironment=DoseSpotConfig.ParseEnvironment(GetListValue(Erx.PropertyDescs.Environment));
			comboDoseSpotEnvironment.SelectedIndex=(doseSpotEnvironment==DoseSpotEnvironment.Staging) ? 1 : 0;
			SecretReadResult secretReadResult=SecretValue.Read(GetListValue(Erx.PropertyDescs.SubscriptionKey),LegacyPolicy.AcceptPlaintext);
			_subscriptionKeyLoaded=secretReadResult.PlainText;
			textSubscriptionKey.Text=_subscriptionKeyLoaded;
			if(secretReadResult.Status==SecretReadStatus.Unreadable) {
				labelSubscriptionKey.Text=Lan.g(this,"Subscription Key (re-enter)");
			}
			List<ProgramProperty> listProgramPropertiesClinicIDs=_listProgramProperties.FindAll(x => x.PropertyDesc==Erx.PropertyDescs.ClinicID);
			List<ProgramProperty> listProgramPropertiesClinicKeys=_listProgramProperties.FindAll(x => x.PropertyDesc==Erx.PropertyDescs.ClinicKey);
			//Always make sure clinicnum 0 (HQ) exists, regardless of if clinics are enabled
			List<long> listClinicNums=new List<long> { 0 };
			if(PrefC.HasClinicsEnabled) {
				listClinicNums.AddRange(Clinics.GetAllForUserod(Security.CurUser).Select(x => x.ClinicNum));
			}
			else {
				checkShowHiddenClinics.Visible=false;
			}
			for(int i=0;i<listClinicNums.Count;i++) {
				if(!listProgramPropertiesClinicIDs.Exists(x => x.ClinicNum==listClinicNums[i])) {//Only add a program property if it doesn't already exist.
					_listProgramProperties.Add(MakeProperty(Erx.PropertyDescs.ClinicID,listClinicNums[i],false));
				}
				if(!listProgramPropertiesClinicKeys.Exists(x => x.ClinicNum==listClinicNums[i])) {
					_listProgramProperties.Add(MakeProperty(Erx.PropertyDescs.ClinicKey,listClinicNums[i],true));
				}
			}
			FillGridDoseSpot();
		}

		private ProgramProperty MakeProperty(string desc,long clinicNum,bool isSecret) {
			ProgramProperty programProperty=new ProgramProperty();
			programProperty.ProgramNum=_program.ProgramNum;
			programProperty.ClinicNum=clinicNum;
			programProperty.PropertyDesc=desc;
			programProperty.PropertyValue="";
			programProperty.ComputerName="";
			programProperty.IsMasked=isSecret;
			programProperty.IsHighSecurity=isSecret;
			return programProperty;
		}

		private void FillGridDoseSpot() {
			gridProperties.BeginUpdate();
			gridProperties.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g(this,"Clinic"),160);
			gridProperties.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Clinic ID"),160);
			gridProperties.Columns.Add(col);
			col=new GridColumn(Lan.g(this,"Clinic Key"),160);
			gridProperties.Columns.Add(col);
			gridProperties.ListGridRows.Clear();
			List<ProgramProperty> listProgramPropertiesHQClinicIDs=GetPropertyForClinic(0,Erx.PropertyDescs.ClinicID);
			List<ProgramProperty> listProgramPropertiesHQClinicKeys=GetPropertyForClinic(0,Erx.PropertyDescs.ClinicKey);
			//Should only be one, but if there are more we need to display them so they can be deleted.
			for(int i = 0;i<listProgramPropertiesHQClinicIDs.Count();i++) {
				DoseSpotGridRowModel doseSpotGridRowModel=new DoseSpotGridRowModel();
				doseSpotGridRowModel.ClinicCur=new Clinic();
				doseSpotGridRowModel.ClinicCur.ClinicNum=0;
				doseSpotGridRowModel.ClinicCur.Abbr=Lan.g(this,"Headquarters");
				if(i>=listProgramPropertiesHQClinicKeys.Count) {
					//The ClinicID and ClinicKey program properties should always be made as pairs.
					continue;
				}
				doseSpotGridRowModel.ProgramPropertyClinicID=listProgramPropertiesHQClinicIDs[i];
				doseSpotGridRowModel.ProgramPropertyClinicKey=listProgramPropertiesHQClinicKeys[i];
				gridProperties.ListGridRows.Add(CreateDoseSpotGridRow(doseSpotGridRowModel));
			}
			if(PrefC.HasClinicsEnabled) {
				foreach(Clinic clinicCur in Clinics.GetAllForUserod(Security.CurUser)) {
					if(!checkShowHiddenClinics.Checked && clinicCur.IsHidden) {
						continue;
					}
					List<ProgramProperty> listProgramPropertiesClinicIDs=GetPropertyForClinic(clinicCur.ClinicNum,Erx.PropertyDescs.ClinicID);
					List<ProgramProperty> listProgramPropertiesClinicKeys=GetPropertyForClinic(clinicCur.ClinicNum,Erx.PropertyDescs.ClinicKey);
					//Each clinic should only have one set of properties but display all of them in case there was an error during setup.
					for(int i = 0;i<listProgramPropertiesClinicIDs.Count();i++) {
						if(i>=listProgramPropertiesClinicKeys.Count) {
							continue;
						}
						DoseSpotGridRowModel doseSpotGridRowModel=new DoseSpotGridRowModel();
						doseSpotGridRowModel.ClinicCur=clinicCur.Copy();
						doseSpotGridRowModel.ProgramPropertyClinicID=listProgramPropertiesClinicIDs[i];
						doseSpotGridRowModel.ProgramPropertyClinicKey=listProgramPropertiesClinicKeys[i];
						gridProperties.ListGridRows.Add(CreateDoseSpotGridRow(doseSpotGridRowModel));
					}
				}
			}
			gridProperties.EndUpdate();
		}

		private GridRow CreateDoseSpotGridRow(DoseSpotGridRowModel doseSpotGridRowModel) {
			GridRow row=new GridRow();
			row.Cells.Add(doseSpotGridRowModel.ClinicCur.Abbr);
			row.Cells.Add(doseSpotGridRowModel.ProgramPropertyClinicID==null ? "" : doseSpotGridRowModel.ProgramPropertyClinicID.PropertyValue);
			row.Cells.Add(DescribeSecret(doseSpotGridRowModel.ProgramPropertyClinicKey?.PropertyValue));
			row.Tag=doseSpotGridRowModel;
			return row;
		}

		///<summary>What the grid shows for a stored key: never the key itself.</summary>
		private string DescribeSecret(string stored) {
			SecretReadResult secretReadResult=DoseSpotConfig.ReadClinicKey(stored);
			if(secretReadResult.Status==SecretReadStatus.Unreadable) {
				return Lan.g(this,"(unreadable, re-enter)");
			}
			return secretReadResult.HasValue ? "••••••••" : "";
		}

		///<summary>This method should only ever return one item but in the event that an error has occurred setting up the DoseSpot account we need to know if there are multiple sets of credentials in the db.</summary>
		private List<ProgramProperty> GetPropertyForClinic(long clinicNum,string propDesc) {
			return _listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==propDesc);
		}

		private void checkShowHiddenClinics_CheckedChanged(object sender,EventArgs e) {
			FillGridDoseSpot();
		}

		private void checkShowSubscriptionKey_CheckedChanged(object sender,EventArgs e) {
			textSubscriptionKey.UseSystemPasswordChar=!checkShowSubscriptionKey.Checked;
		}

		private void gridProperties_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(!_canEditSecrets) {
				MsgBox.Show(this,"Changing the DoseSpot clinic keys needs the high-security program property permission.");
				return;
			}
			DoseSpotGridRowModel doseSpotGridRowModel=(DoseSpotGridRowModel)gridProperties.ListGridRows[e.Row].Tag;
			string clinicKeyPlain=DoseSpotConfig.ReadClinicKey(doseSpotGridRowModel.ProgramPropertyClinicKey.PropertyValue).PlainText;
			using FormDoseSpotPropertyEdit formDoseSpotPropertyEdit=new FormDoseSpotPropertyEdit(doseSpotGridRowModel.ClinicCur,
				doseSpotGridRowModel.ProgramPropertyClinicID.PropertyValue,clinicKeyPlain,_listProgramProperties);
			formDoseSpotPropertyEdit.ShowDialog();
			if(formDoseSpotPropertyEdit.DialogResult==DialogResult.OK) {
				string clinicKeyStored;
				if(!SecretValue.Protect(formDoseSpotPropertyEdit.ClinicKeyVal,out clinicKeyStored)) {
					MsgBox.Show(this,"The clinic key could not be encrypted, so it was not changed.");
				}
				else {
					int clinicIdPos=_listProgramProperties.FindIndex(x => x.ClinicNum==doseSpotGridRowModel.ProgramPropertyClinicID.ClinicNum && x.PropertyDesc==doseSpotGridRowModel.ProgramPropertyClinicID.PropertyDesc);
					_listProgramProperties[clinicIdPos].PropertyValue=formDoseSpotPropertyEdit.ClinicIdVal;
					int clinicKeyPos=_listProgramProperties.FindIndex(x => x.ClinicNum==doseSpotGridRowModel.ProgramPropertyClinicKey.ClinicNum && x.PropertyDesc==doseSpotGridRowModel.ProgramPropertyClinicKey.PropertyDesc);
					_listProgramProperties[clinicKeyPos].PropertyValue=clinicKeyStored;
					_listProgramProperties[clinicKeyPos].IsMasked=true;
					_listProgramProperties[clinicKeyPos].IsHighSecurity=true;
				}
			}
			FillGridDoseSpot();//Always fill grid because clinics could have been edited in FormDoseSpotPropertyEdit.
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(gridProperties.SelectedGridRows.Count()!=1) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Are you sure you want to delete this row?")) {
				return;
			}
			DoseSpotGridRowModel doseSpotGridRowModel=gridProperties.SelectedTag<DoseSpotGridRowModel>();
			//_listProgramProperties is synced against the cache on OK click, removing these from the list will cause them to be deleted.
			_listProgramProperties.Remove(doseSpotGridRowModel.ProgramPropertyClinicID);
			_listProgramProperties.Remove(doseSpotGridRowModel.ProgramPropertyClinicKey);
			FillGridDoseSpot();
		}

		///<summary>Gets a DoseSpot token for the selected clinic row (or Headquarters) as the current user, with the values on screen. Proves the
		///subscription key, clinic ID and key and the user's DoseSpot user ID work together. Sends no patient data.</summary>
		private void butDoseSpotTest_Click(object sender,EventArgs e) {
			DoseSpotGridRowModel doseSpotGridRowModel=null;
			if(gridProperties.SelectedGridRows.Count==1) {
				doseSpotGridRowModel=gridProperties.SelectedTag<DoseSpotGridRowModel>();
			}
			else if(gridProperties.ListGridRows.Count>0) {
				doseSpotGridRowModel=(DoseSpotGridRowModel)gridProperties.ListGridRows[0].Tag;
			}
			if(doseSpotGridRowModel==null) {
				MsgBox.Show(this,"Select a clinic first.");
				return;
			}
			long clinicNum=doseSpotGridRowModel.ClinicCur.ClinicNum;
			UserOdPref userOdPref=DoseSpot.GetDoseSpotUserIdFromPref(Security.CurUser.UserNum,clinicNum);
			DoseSpotCredentials doseSpotCredentials=new DoseSpotCredentials();
			doseSpotCredentials.ApiBaseUrl=DoseSpotConfig.GetApiBaseUrl(comboDoseSpotEnvironment.SelectedIndex==1 ? DoseSpotEnvironment.Staging : DoseSpotEnvironment.Production);
			doseSpotCredentials.SubscriptionKey=textSubscriptionKey.Text.Trim();
			doseSpotCredentials.ClinicId=(doseSpotGridRowModel.ProgramPropertyClinicID?.PropertyValue??"").Trim();
			doseSpotCredentials.ClinicKey=DoseSpotConfig.ReadClinicKey(doseSpotGridRowModel.ProgramPropertyClinicKey?.PropertyValue).PlainText.Trim();
			doseSpotCredentials.UserId=(userOdPref?.ValueString??"").Trim();
			Cursor=Cursors.WaitCursor;
			try {
				DoseSpotRESTV2.CheckCredentials(doseSpotCredentials,isInteractive:true);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				MessageBox.Show(Lan.g(this,"DoseSpot test failed:")+"\r\n"+ex.Message);
				return;
			}
			Cursor=Cursors.Default;
			MessageBox.Show(Lan.g(this,"DoseSpot accepted the subscription key, the clinic ID and key for")+" "+doseSpotGridRowModel.ClinicCur.Abbr+" "
				+Lan.g(this,"and your DoseSpot user ID."));
		}
		#endregion DoseSpot

		#region NewCrop
		private void LoadNewCrop() {
			NewCropConfig newCropConfig=NewCropConfig.Load();
			comboNewCropEnvironment.Items.Clear();
			comboNewCropEnvironment.Items.Add(Lan.g(this,"Production"));
			comboNewCropEnvironment.Items.Add(Lan.g(this,"Preproduction"));
			comboNewCropEnvironment.SelectedIndex=(newCropConfig.Environment==NewCropEnvironment.Preproduction) ? 1 : 0;
			textPartnerName.Text=newCropConfig.PartnerName;
			textAccountName.Text=newCropConfig.AccountName;
			_newCropPasswordLoaded=newCropConfig.Password;
			textPassword.Text=newCropConfig.Password;
			if(newCropConfig.PasswordStatus==SecretReadStatus.Unreadable) {
				labelPassword.Text=Lan.g(this,"Password (re-enter)");
			}
			textAccountId.Text=newCropConfig.AccountId;
			textSiteId.Text=newCropConfig.SiteId;
			textProductName.Text=newCropConfig.ProductName;
			textNewCropAccountID.Text=PrefC.GetString(PrefName.NewCropAccountId);//Issued under the previous vendor's agreement; shown for reference only.
			_dictNewCropEnabledLoaded.Clear();
			foreach(Provider provider in GetNewCropProviders()) {
				string npi=NewCropConfig.NormalizeNpi(provider.NationalProvID);
				if(!_dictNewCropEnabledLoaded.ContainsKey(npi)) {
					_dictNewCropEnabledLoaded[npi]=NewCropConfig.IsProviderEnabled(npi);
				}
			}
			_dictNewCropEnabled=new Dictionary<string,bool>(_dictNewCropEnabledLoaded);
			FillGridNewCropProviders();
		}

		///<summary>Visible providers that are people with a 10-digit NPI: the ones NewCrop can know as prescribers.</summary>
		private List<Provider> GetNewCropProviders() {
			return Providers.GetDeepCopy(true).FindAll(x => !x.IsNotPerson && NewCropConfig.NormalizeNpi(x.NationalProvID).Length==10);
		}

		private void FillGridNewCropProviders() {
			gridNewCropProviders.BeginUpdate();
			gridNewCropProviders.Columns.Clear();
			gridNewCropProviders.Columns.Add(new GridColumn(Lan.g(this,"Provider"),110));
			gridNewCropProviders.Columns.Add(new GridColumn(Lan.g(this,"NPI"),80));
			gridNewCropProviders.Columns.Add(new GridColumn(Lan.g(this,"Enabled"),50,HorizontalAlignment.Center));
			gridNewCropProviders.ListGridRows.Clear();
			foreach(Provider provider in GetNewCropProviders()) {
				string npi=NewCropConfig.NormalizeNpi(provider.NationalProvID);
				GridRow row=new GridRow();
				row.Cells.Add(provider.Abbr);
				row.Cells.Add(npi);
				row.Cells.Add(_dictNewCropEnabled.TryGetValue(npi,out bool isEnabled) && isEnabled ? "X" : "");
				row.Tag=npi;
				gridNewCropProviders.ListGridRows.Add(row);
			}
			gridNewCropProviders.EndUpdate();
		}

		private void gridNewCropProviders_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin)) {
				return;
			}
			string npi=(string)gridNewCropProviders.ListGridRows[e.Row].Tag;
			_dictNewCropEnabled.TryGetValue(npi,out bool isEnabled);
			_dictNewCropEnabled[npi]=!isEnabled;
			FillGridNewCropProviders();
		}

		private void checkShowPassword_CheckedChanged(object sender,EventArgs e) {
			textPassword.UseSystemPasswordChar=!checkShowPassword.Checked;
		}

		private void butGenerateAccountId_Click(object sender,EventArgs e) {
			if(textAccountId.Text.Trim()!=""
				&& !MsgBox.Show(this,MsgBoxButtons.YesNo,"Replace the account ID with a new one? Use a generated ID only if Ensora or the reseller accepts it."))
			{
				return;
			}
			textAccountId.Text=NewCropConfig.GenerateAccountId();
		}

		private void butClearAccountId_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.SecurityAdmin)) {
				return;
			}
			textAccountId.Text="";
		}

		///<summary>The NewCrop settings as they are on screen.</summary>
		private NewCropConfig GetNewCropConfigFromScreen() {
			NewCropConfig newCropConfig=new NewCropConfig();
			newCropConfig.Environment=(comboNewCropEnvironment.SelectedIndex==1) ? NewCropEnvironment.Preproduction : NewCropEnvironment.Production;
			newCropConfig.PartnerName=textPartnerName.Text.Trim();
			newCropConfig.AccountName=textAccountName.Text.Trim();
			newCropConfig.Password=textPassword.Text;
			newCropConfig.PasswordStatus=(textPassword.Text=="") ? SecretReadStatus.Blank : SecretReadStatus.Ok;
			newCropConfig.AccountId=textAccountId.Text.Trim();
			newCropConfig.SiteId=textSiteId.Text.Trim();
			newCropConfig.ProductName=textProductName.Text.Trim();
			return newCropConfig;
		}

		private void butNewCropTest_Click(object sender,EventArgs e) {
			Cursor=Cursors.WaitCursor;
			bool isOk=GetNewCropConfigFromScreen().TestConnection(out string message);
			Cursor=Cursors.Default;
			MessageBox.Show((isOk ? Lan.g(this,"NewCrop accepted the settings.") : Lan.g(this,"NewCrop test failed."))+"\r\n"+message);
		}
		#endregion NewCrop

		private void SetRadioButtonChecked(ErxOption erxOption) {
			_erxOption=erxOption;
			//Both tabs stay so a combined setup can be finished; the tab for the chosen option is brought forward.
			if(erxOption==ErxOption.NewCrop) {
				tabControlVendors.SelectedIndex=1;
			}
			else {
				tabControlVendors.SelectedIndex=0;
			}
		}

		private void radioNewCrop_Click(object sender,EventArgs e) {
			SetRadioButtonChecked(ErxOption.NewCrop);
		}

		private void radioDoseSpot_Click(object sender,EventArgs e) {
			MsgBox.Show(this,"This turns on the DoseSpot program link only. A DoseSpot account, and its values entered on the DoseSpot tab, are also required.");
			SetRadioButtonChecked(ErxOption.DoseSpot);
		}

		private void radioDoseSpotLegacy_Click(object sender,EventArgs e) {
			MsgBox.Show(this,"This turns on both program links. An account with each eRx vendor, and its values entered on its tab, are also required.");
			SetRadioButtonChecked(ErxOption.DoseSpotWithNewCrop);
		}

		///<summary>The stored value of the practice-wide (ClinicNum 0) row with this description in the list, or "".</summary>
		private string GetListValue(string desc) {
			return _listProgramProperties.FirstOrDefault(x => x.ClinicNum==0 && x.PropertyDesc==desc)?.PropertyValue??"";
		}

		///<summary>Sets the practice-wide (ClinicNum 0) row with this description in the list, adding the row if needed.</summary>
		private void SetListValue(string desc,string value,bool isSecret=false) {
			ProgramProperty programProperty=_listProgramProperties.FirstOrDefault(x => x.ClinicNum==0 && x.PropertyDesc==desc);
			if(programProperty==null) {
				programProperty=MakeProperty(desc,0,isSecret);
				_listProgramProperties.Add(programProperty);
			}
			programProperty.PropertyValue=value??"";
			if(isSecret) {
				programProperty.IsMasked=true;
				programProperty.IsHighSecurity=true;
			}
		}

		///<summary>Encrypts plainText into the practice-wide row desc. Returns false, after telling the user, if encryption failed.</summary>
		private bool SetListSecret(string desc,string plainText,string displayName) {
			string stored;
			if(!SecretValue.Protect(plainText,out stored)) {
				MessageBox.Show(displayName+" "+Lan.g(this,"could not be encrypted, so nothing was saved."));
				return false;
			}
			SetListValue(desc,stored,true);
			return true;
		}

		private void butSave_Click(object sender,EventArgs e) {
			//Secrets first: if any can't be encrypted, nothing is saved.
			if(_canEditSecrets) {
				if(textSubscriptionKey.Text.Trim()!=_subscriptionKeyLoaded
					&& !SetListSecret(Erx.PropertyDescs.SubscriptionKey,textSubscriptionKey.Text.Trim(),Lan.g(this,"The DoseSpot subscription key")))
				{
					return;
				}
				if(textPassword.Text!=_newCropPasswordLoaded
					&& !SetListSecret(NewCropConfig.PropPassword,textPassword.Text,Lan.g(this,"The NewCrop password")))
				{
					return;
				}
				//Clinic keys the previous program stored as plain text are encrypted now.
				List<ProgramProperty> listProgramPropertiesKeys=_listProgramProperties.FindAll(x => x.PropertyDesc==Erx.PropertyDescs.ClinicKey);
				for(int i=0;i<listProgramPropertiesKeys.Count;i++) {
					SecretReadResult secretReadResult=DoseSpotConfig.ReadClinicKey(listProgramPropertiesKeys[i].PropertyValue);
					if(!secretReadResult.NeedsReEncrypt) {
						continue;
					}
					string stored;
					if(!SecretValue.Protect(secretReadResult.PlainText,out stored)) {
						MsgBox.Show(this,"A DoseSpot clinic key could not be encrypted, so nothing was saved.");
						return;
					}
					listProgramPropertiesKeys[i].PropertyValue=stored;
					listProgramPropertiesKeys[i].IsMasked=true;
					listProgramPropertiesKeys[i].IsHighSecurity=true;
				}
			}
			SetListValue(Erx.PropertyDescs.Environment,comboDoseSpotEnvironment.SelectedIndex==1 ? DoseSpotEnvironment.Staging.ToString() : DoseSpotEnvironment.Production.ToString());
			//Every DoseSpot call uses API v2 now. The row is kept, set to 2, for databases that still read it.
			ProgramProperty programPropertyApiVersion=_listProgramProperties.FirstOrDefault(x => x.ClinicNum==0 && x.PropertyDesc==DoseSpotREST.PropertyDescs.DoseSpotApiVersion);
			if(programPropertyApiVersion!=null) {
				programPropertyApiVersion.PropertyValue="2";
			}
			NewCropConfig newCropConfig=GetNewCropConfigFromScreen();
			SetListValue(NewCropConfig.PropEnvironment,newCropConfig.Environment.ToString());
			SetListValue(NewCropConfig.PropPartnerName,newCropConfig.PartnerName);
			SetListValue(NewCropConfig.PropAccountName,newCropConfig.AccountName);
			SetListValue(NewCropConfig.PropAccountId,newCropConfig.AccountId);
			SetListValue(NewCropConfig.PropSiteId,newCropConfig.SiteId=="" ? NewCropConfig.DefaultSiteId : newCropConfig.SiteId);
			SetListValue(NewCropConfig.PropProductName,newCropConfig.ProductName=="" ? CodeBase.VendorDirect.ProductIdentity.ProductName : newCropConfig.ProductName);
			ErxOptionPP.PropertyValue=POut.Int((int)_erxOption);
			_program.Enabled=checkEnabled.Checked;
			Programs.Update(_program);
			ProgramProperties.Sync(_listProgramProperties,_program.ProgramNum);
			BlankPlainTextClinicErxKeys();
			foreach(KeyValuePair<string,bool> pair in _dictNewCropEnabled) {
				if(_dictNewCropEnabledLoaded.TryGetValue(pair.Key,out bool isEnabledLoaded) && isEnabledLoaded==pair.Value) {
					continue;
				}
				NewCropConfig.SetProviderEnabled(pair.Key,pair.Value);
			}
			DoseSpotApiClient.Default.ResetAll();//Changed settings are tried again at once, not after a restart.
			DataValid.SetInvalid(InvalidType.Programs);
			DialogResult=DialogResult.OK;
		}

		///<summary>clinicerx.ClinicKey holds plain-text DoseSpot keys copied there by the previous vendor's servers. Nothing reads them any more, so they are
		///blanked rather than left in the clear. A data update, not a schema change.</summary>
		private void BlankPlainTextClinicErxKeys() {
			bool isChanged=false;
			List<ClinicErx> listClinicErxs=ClinicErxs.GetWhere(x => !string.IsNullOrEmpty(x.ClinicKey));
			for(int i=0;i<listClinicErxs.Count;i++) {
				ClinicErx clinicErxOld=listClinicErxs[i].Copy();
				listClinicErxs[i].ClinicKey="";
				isChanged|=ClinicErxs.Update(listClinicErxs[i],clinicErxOld);
			}
			if(isChanged) {
				DataValid.SetInvalid(InvalidType.ClinicErxs);
			}
		}

		private class DoseSpotGridRowModel {
			public Clinic ClinicCur;
			public ProgramProperty ProgramPropertyClinicID;
			public ProgramProperty ProgramPropertyClinicKey;

			public DoseSpotGridRowModel() {
			}
		}

	}
}
