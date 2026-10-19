using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;
using OpenDentBusiness.VendorDirect;

namespace OpenDental {
	///<summary>CareCredit setup. Stores the office's own Synchrony credentials (the client secret encrypted, in a high-security row), the endpoint values
	///from Synchrony's implementation guide, the merchant numbers and batch options, and which computer runs the background tasks. The program can
	///only be enabled after Test connection passes with the settings being saved.</summary>
	public partial class FormCareCreditSetup:FormODBase {

		private Program _program;
		///<summary>List of CareCredit program properties for all clinics.
		///Includes properties with ClinicNum=0, the headquarters properties or properties not assigned to a clinic.</summary>
		private List<ProgramProperty> _listProgramProperties;
		///<summary>Used to switch back to clinics if user selected a different clinic and validation did not pass.</summary>
		private long _previouslySelectedClinicNum;
		///<summary>Default days for program property.</summary>
		private const int DEFAULT_DAYS=2;
		///<summary>Shown in the client secret box when a secret is saved. Leaving it unchanged keeps the saved secret.</summary>
		private const string SECRET_MASK="********";
		private bool _hasCareCreditPatFieldDefNum=false;
		public bool DoShowApptViewWindow=false;
		///<summary>Fingerprint of the connection settings at the last passing Test connection (saved, or from this session).</summary>
		private string _verifiedFingerprint="";
		///<summary>The computer chosen to run the background tasks, or "".</summary>
		private string _jobComputerName="";
		///<summary>The connection settings as saved when the window opened. Destination changes are compared against these.</summary>
		private CareCreditConfig _configSaved;
		///<summary>True if the user has ManageHighSecurityProgProperties, which editing the secret and the settings that decide where it goes needs.</summary>
		private bool _hasHighSecurity;

		///<summary>Used for comboboxDaysOut. The number of days a user can set.</summary>
		public List<int> ListDaysOut => new List<int>() { 2,3,4 };

		public FormCareCreditSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormCareCreditSetup_Load(object sender,EventArgs e) {
			_program=Programs.GetCur(ProgramName.CareCredit);
			if(_program==null) {
				throw new Exception("The CareCredit program is missing from the database.");//should never happen
			}
			//Insert any missing practice-wide rows first, so the Sync on save never deletes a row this window doesn't list.
			if(CareCreditSettings.EnsureRows(_program.ProgramNum)) {
				ProgramProperties.RefreshCache();
			}
			checkEnabled.Checked=_program.Enabled;
			if(PrefC.HasClinicsEnabled) {
				if(Security.CurUser.ClinicIsRestricted) {
					if(checkEnabled.Checked) {
						checkEnabled.Enabled=false;
					}
				}
				_previouslySelectedClinicNum=comboClinics.ClinicNumSelected;
				textMerchantNumberPractice.Visible=false;
				labelMerchantNumberPractice.Visible=false;
			}
			else {
				// ClinicNum 0 to indicate 'Headquarters' or practice level program properties
				comboClinics.Visible=false;
				groupClinicSettings.Text="";//This will just show a box around the settings
				labelMerchantNumberClinic.Visible=false;
				textMerchantNumberClinic.Visible=false;
			}
			_listProgramProperties=ProgramProperties.GetForProgram(_program.ProgramNum);
			//We only care that the _progCur was enabled prior to opening the form. Missing program properties would have been added already.
			groupPromotions.Visible=_program.Enabled;
			AddNeededProgramProperties(0);
			if(PrefC.HasClinicsEnabled) {
				foreach(Clinic clinic in Clinics.GetForUserod(Security.CurUser)) {
					AddNeededProgramProperties(clinic.ClinicNum);
				}
			}
			List<PatFieldDef> listPatFieldDefs=PatFieldDefs.GetDeepCopy();
			long patFieldDefNum=PIn.Long(ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditPatField));
			long patFieldDefNumPreApprvAmt=PIn.Long(ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldPreApprovalAmt));
			long patFieldDefNumAvailableCredit=PIn.Long(ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldAvailableCredit));
			comboPatFieldDef.Items.Clear();
			comboPatFieldDef.Items.AddList(listPatFieldDefs.FindAll(x=>x.FieldType==PatFieldType.CareCreditStatus),x=>x.FieldName);
			comboPatFieldDef.SetSelectedKey<PatFieldDef>(patFieldDefNum,x => x.PatFieldDefNum,x => Lan.g(this,"None"));
			comboPatFieldPreApprvAmt.Items.Clear();
			comboPatFieldPreApprvAmt.Items.AddList(listPatFieldDefs.FindAll(x=>x.FieldType==PatFieldType.CareCreditPreApprovalAmt),x=>x.FieldName);
			comboPatFieldPreApprvAmt.SetSelectedKey<PatFieldDef>(patFieldDefNumPreApprvAmt,x => x.PatFieldDefNum,x => Lan.g(this,"None"));
			comboPatFieldAvailableCredit.Items.Clear();
			comboPatFieldAvailableCredit.Items.AddList(listPatFieldDefs.FindAll(x=>x.FieldType==PatFieldType.CareCreditAvailableCredit),x=>x.FieldName);
			comboPatFieldAvailableCredit.SetSelectedKey<PatFieldDef>(patFieldDefNumAvailableCredit,x => x.PatFieldDefNum,x => Lan.g(this,"None"));
			_hasCareCreditPatFieldDefNum=ApptViewItems.GetWhere(x => listPatFieldDefs.Select(y => y.PatFieldDefNum).ToList().Contains(x.PatFieldDefNum)).Count>0;
			//EnumCareCreditBatch is stored in db. Manually order to preserve enum order but improve readability.
			EnumCareCreditBatch enumCareCreditBatch=EnumCareCreditBatch.Off;
			listBoxQSOptions.Items.Add(Lan.g("enumQSBatch",enumCareCreditBatch.GetDescription()),enumCareCreditBatch);
			enumCareCreditBatch=EnumCareCreditBatch.On;
			listBoxQSOptions.Items.Add(Lan.g("enumQSBatch",enumCareCreditBatch.GetDescription()),enumCareCreditBatch);
			enumCareCreditBatch=EnumCareCreditBatch.NoBalance;
			listBoxQSOptions.Items.Add(Lan.g("enumQSBatch",enumCareCreditBatch.GetDescription()),enumCareCreditBatch);
			enumCareCreditBatch=EnumCareCreditBatch.LookupOnly;
			listBoxQSOptions.Items.Add(Lan.g("enumQSBatch",enumCareCreditBatch.GetDescription()),enumCareCreditBatch);
			FillFields(isLoad:true);
			FillConnection();
			FillBackground();
			DisableControlsForMerchantClosed();
		}

		#region Connection and background tasks
		///<summary>The value of a practice-wide (ClinicNum 0) row in the in-memory list, or "".</summary>
		private string GetPracticeListValue(string desc) {
			return _listProgramProperties.FirstOrDefault(x => x.ClinicNum==0 && x.PropertyDesc==desc)?.PropertyValue??"";
		}

		///<summary>Sets a practice-wide row in the in-memory list, adding it if missing. Secret rows are kept masked and high security.</summary>
		private void SetPracticeListValue(string desc,string value,bool isSecret=false) {
			ProgramProperty programProperty=_listProgramProperties.FirstOrDefault(x => x.ClinicNum==0 && x.PropertyDesc==desc);
			if(programProperty==null) {
				programProperty=new ProgramProperty();
				programProperty.ProgramNum=_program.ProgramNum;
				programProperty.PropertyDesc=desc;
				programProperty.ClinicNum=0;
				programProperty.ComputerName="";
				_listProgramProperties.Add(programProperty);
			}
			programProperty.PropertyValue=value??"";
			if(isSecret) {
				programProperty.IsMasked=true;
				programProperty.IsHighSecurity=true;
			}
		}

		private void FillConnection() {
			textClientId.Text=GetPracticeListValue(CareCreditSettings.ClientId);
			SecretReadResult secretReadResult=SecretValue.Read(GetPracticeListValue(CareCreditSettings.ClientSecret),LegacyPolicy.RejectPlaintext);
			textClientSecret.Text=secretReadResult.HasValue ? SECRET_MASK : "";
			//Editing the secret needs the same permission as any other high-security program property. So does every setting that decides where the
			//secret and patient data are sent: otherwise a user without the permission could point the API host elsewhere and Test connection would
			//send the saved secret there. Test connection and Save check it again (CareCredit.GetConnectionChangeProblem).
			_hasHighSecurity=Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties,suppressMessage:true);
			textClientSecret.Enabled=_hasHighSecurity;
			textApiBaseUrl.Enabled=_hasHighSecurity;
			textIhsBaseUrl.Enabled=_hasHighSecurity;
			textTokenPath.Enabled=_hasHighSecurity;
			textPrefillPath.Enabled=_hasHighSecurity;
			textPullbackPath.Enabled=_hasHighSecurity;
			textBatchPath.Enabled=_hasHighSecurity;
			comboClientAuth.Enabled=_hasHighSecurity;
			textScope.Enabled=_hasHighSecurity;
			comboEnvironment.Items.Clear();
			comboEnvironment.Items.AddEnums<CareCreditEnvironment>();
			comboEnvironment.SetSelectedEnum(CareCreditSettings.ParseEnvironment(GetPracticeListValue(CareCreditSettings.Environment)));
			textApiBaseUrl.Text=GetPracticeListValue(CareCreditSettings.ApiBaseUrl);
			textIhsBaseUrl.Text=GetPracticeListValue(CareCreditSettings.IhsBaseUrl);
			string partnerCode=GetPracticeListValue(ProgramProperties.PropertyDescs.CareCredit.CareCreditPartnerCode).Trim();
			if(partnerCode==CareCreditSettings.PARTNER_CODE_REFUSED) {
				partnerCode="";//Open Dental's own code, from the database upgrade. Treated as not set.
			}
			textPartnerCode.Text=partnerCode;
			textTokenPath.Text=GetPracticeListValue(CareCreditSettings.TokenPath);
			textPrefillPath.Text=GetPracticeListValue(CareCreditSettings.PrefillPath);
			textPullbackPath.Text=GetPracticeListValue(CareCreditSettings.PullbackPath);
			textBatchPath.Text=GetPracticeListValue(CareCreditSettings.BatchPath);
			textSessionParam.Text=GetPracticeListValue(CareCreditSettings.SessionParam);
			comboClientAuth.Items.Clear();
			comboClientAuth.Items.AddEnums<CareCreditClientAuth>();
			comboClientAuth.SetSelectedEnum(CareCreditSettings.ParseClientAuth(GetPracticeListValue(CareCreditSettings.ClientAuth)));
			textScope.Text=GetPracticeListValue(CareCreditSettings.TokenScope);
			_verifiedFingerprint=GetPracticeListValue(CareCreditSettings.VerifiedConfig);
			string lastAuthError=CareCreditSettings.GetPracticeValue(CareCreditSettings.LastAuthError);
			CareCreditConfig config=BuildConfigFromForm(out string error);
			_configSaved=BuildSavedDestinationConfig();
			if(lastAuthError!="") {
				labelConnectionStatus.Text=Lan.g(this,"Last error")+": "+lastAuthError;
			}
			else if(config!=null && _verifiedFingerprint!="" && _verifiedFingerprint==config.GetVerificationFingerprint()) {
				labelConnectionStatus.Text=Lan.g(this,"Connection tested with these settings.");
			}
			else {
				labelConnectionStatus.Text=Lan.g(this,"Not tested. Test the connection before enabling CareCredit.");
			}
		}

		///<summary>The saved values of the settings CareCreditConfig.GetDestinationChanges compares, from the rows loaded with the window.</summary>
		private CareCreditConfig BuildSavedDestinationConfig() {
			CareCreditConfig config=new CareCreditConfig();
			config.ApiBaseUrl=GetPracticeListValue(CareCreditSettings.ApiBaseUrl).Trim();
			config.IhsBaseUrl=GetPracticeListValue(CareCreditSettings.IhsBaseUrl).Trim();
			config.TokenPath=GetPracticeListValue(CareCreditSettings.TokenPath).Trim();
			config.PrefillPath=GetPracticeListValue(CareCreditSettings.PrefillPath).Trim();
			config.PullbackPath=GetPracticeListValue(CareCreditSettings.PullbackPath).Trim();
			config.BatchPath=GetPracticeListValue(CareCreditSettings.BatchPath).Trim();
			config.ClientAuth=CareCreditSettings.ParseClientAuth(GetPracticeListValue(CareCreditSettings.ClientAuth));
			config.Scope=GetPracticeListValue(CareCreditSettings.TokenScope).Trim();
			return config;
		}

		private void FillBackground() {
			_jobComputerName=GetPracticeListValue(CareCreditSettings.JobComputerName).Trim();
			SetJobComputerLabel();
			textProcessTime.Text=FormatTime(CareCreditSettings.GetProcessTime());
			textPullbackTime.Text=FormatTime(CareCreditSettings.GetPullbackTime());
		}

		private static string FormatTime(TimeSpan timeOfDay) {
			return timeOfDay.Hours.ToString("00")+":"+timeOfDay.Minutes.ToString("00");
		}

		private void SetJobComputerLabel() {
			if(_jobComputerName=="") {
				labelJobComputer.Text=Lan.g(this,"Run on: not set. No batch QuickScreen; each workstation checks only its own results.");
				butRunHere.Text=Lan.g(this,"Run on this computer");
				return;
			}
			labelJobComputer.Text=Lan.g(this,"Run on")+": "+_jobComputerName;
			if(string.Equals(_jobComputerName,ODEnvironment.MachineName,StringComparison.OrdinalIgnoreCase)) {
				butRunHere.Text=Lan.g(this,"Stop running here");
			}
			else {
				butRunHere.Text=Lan.g(this,"Run on this computer");
			}
		}

		private void butRunHere_Click(object sender,EventArgs e) {
			if(string.Equals(_jobComputerName,ODEnvironment.MachineName,StringComparison.OrdinalIgnoreCase)) {
				_jobComputerName="";
			}
			else {
				_jobComputerName=ODEnvironment.MachineName;
			}
			SetJobComputerLabel();
		}

		///<summary>The connection settings as typed. The client secret box showing the mask means the saved secret. Returns null, with error set, if the
		///typed secret can't be encrypted.</summary>
		private CareCreditConfig BuildConfigFromForm(out string error) {
			error="";
			CareCreditConfig config=new CareCreditConfig();
			config.ClientId=textClientId.Text.Trim();
			if(textClientSecret.Text==SECRET_MASK) {
				config.ClientSecretStored=GetPracticeListValue(CareCreditSettings.ClientSecret);
				SecretReadResult secretReadResult=SecretValue.Read(config.ClientSecretStored,LegacyPolicy.RejectPlaintext);
				config.ClientSecret=secretReadResult.PlainText;
				config.IsClientSecretUnreadable=secretReadResult.NeedsReentry;
			}
			else if(textClientSecret.Text=="") {
				config.ClientSecretStored="";
				config.ClientSecret="";
			}
			else {
				string stored;
				if(!SecretValue.Protect(textClientSecret.Text,out stored)) {
					error=Lan.g(this,"The client secret could not be encrypted, so it was not saved.");
					return null;
				}
				config.ClientSecret=textClientSecret.Text;
				config.ClientSecretStored=stored;
			}
			config.Environment=comboEnvironment.GetSelected<CareCreditEnvironment>();
			config.ApiBaseUrl=textApiBaseUrl.Text.Trim();
			config.IhsBaseUrl=textIhsBaseUrl.Text.Trim();
			config.PartnerCode=textPartnerCode.Text.Trim();
			config.TokenPath=textTokenPath.Text.Trim();
			config.PrefillPath=textPrefillPath.Text.Trim();
			config.PullbackPath=textPullbackPath.Text.Trim();
			config.BatchPath=textBatchPath.Text.Trim();
			config.SessionParam=textSessionParam.Text.Trim();
			config.ClientAuth=comboClientAuth.GetSelected<CareCreditClientAuth>();
			config.Scope=textScope.Text.Trim();
			config.VerifiedFingerprint=_verifiedFingerprint;
			return config;
		}

		private void butTestConnection_Click(object sender,EventArgs e) {
			CareCreditConfig config=BuildConfigFromForm(out string error);
			if(config==null) {
				MsgBox.Show(error);
				return;
			}
			string result;
			Cursor=Cursors.WaitCursor;
			try {
				//Refuses, before anything is sent, a changed host or path from a user who may not change them.
				result=CareCredit.TestConnection(config,_configSaved,_hasHighSecurity);
			}
			finally {
				Cursor=Cursors.Default;
			}
			if(result=="") {
				_verifiedFingerprint=config.GetVerificationFingerprint();
				CareCreditSettings.SetLastAuthError("");
				labelConnectionStatus.Text=Lan.g(this,"Connection test passed. Save to keep these settings.");
				return;
			}
			labelConnectionStatus.Text=Lan.g(this,"Connection test failed")+": "+result;
			MsgBox.Show(Lan.g(this,"Connection test failed")+":\r\n"+result);
		}

		///<summary>Checks the values that are typed (blank is allowed while the program stays disabled). Returns "" or the problem.</summary>
		private string GetTypedValueProblem(CareCreditConfig config) {
			if(config.PartnerCode!="" && CareCreditConfig.GetPartnerCodeProblem(config.PartnerCode)!="") {
				return CareCreditConfig.GetPartnerCodeProblem(config.PartnerCode);
			}
			if(config.ApiBaseUrl!="" && CareCreditConfig.GetUrlProblem(config.ApiBaseUrl)!="") {
				return Lan.g(this,"API base URL")+": "+CareCreditConfig.GetUrlProblem(config.ApiBaseUrl);
			}
			if(config.IhsBaseUrl!="" && CareCreditConfig.GetUrlProblem(config.IhsBaseUrl)!="") {
				return Lan.g(this,"Hosted-page base URL")+": "+CareCreditConfig.GetUrlProblem(config.IhsBaseUrl);
			}
			foreach(Tuple<string,string> tuple in new[] { Tuple.Create("Token path",config.TokenPath),Tuple.Create("Prefill path",config.PrefillPath),
				Tuple.Create("Pullback path",config.PullbackPath),Tuple.Create("Batch path",config.BatchPath) })
			{
				string problem=CareCreditConfig.GetPathProblem(tuple.Item2,isRequired:false);
				if(problem!="") {
					return Lan.g(this,tuple.Item1)+" "+problem;
				}
			}
			return "";
		}

		///<summary>Rows the background jobs and Test connection write are reloaded from the database before the list is synced, so a stale value isn't
		///put back.</summary>
		private void ReloadJobManagedRows() {
			List<string> listDescs=CareCreditSettings.GetJobManagedDescs();
			for(int i=0;i<_listProgramProperties.Count;i++) {
				ProgramProperty programProperty=_listProgramProperties[i];
				if(programProperty.ProgramPropertyNum==0) {
					continue;
				}
				if(!listDescs.Contains(programProperty.PropertyDesc) && !VendorJobLease.IsLeaseProperty(programProperty.PropertyDesc)) {
					continue;
				}
				ProgramProperty programPropertyDb=ProgramProperties.GetOneFromDb(programProperty.ProgramNum,programProperty.PropertyDesc,programProperty.ClinicNum);
				if(programPropertyDb!=null && programPropertyDb.ProgramPropertyNum==programProperty.ProgramPropertyNum) {
					programProperty.PropertyValue=programPropertyDb.PropertyValue;
				}
			}
		}
		#endregion Connection and background tasks

		private void FillFields(bool isLoad=false) {
			if(isLoad) {
				comboPaymentType.Items.AddDefs(Defs.GetDefsForCategory(DefCat.PaymentTypes,true));
				comboBoxDaysOut.Items.AddList(ListDaysOut,x => x.ToString());
			}
			string payTypeDefNum=ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditPaymentType,
				comboClinics.ClinicNumSelected);
			comboPaymentType.SetSelectedDefNum(PIn.Long(payTypeDefNum));
			listBoxQSOptions.SetSelectedEnum(PIn.Enum<EnumCareCreditBatch>(ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled,comboClinics.ClinicNumSelected)));
			groupQSBatch.Visible=true;
			if(listBoxQSOptions.GetSelected<EnumCareCreditBatch>()==EnumCareCreditBatch.Off){
				groupQSBatch.Visible=false;
			}
			SetLabelQSDaysText(listBoxQSOptions.GetSelected<EnumCareCreditBatch>());
			int daysOut=PIn.Int(ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchDays,
				comboClinics.ClinicNumSelected));
			if(daysOut<=0) {
				daysOut=DEFAULT_DAYS;
			}
			comboBoxDaysOut.SetSelectedKey<int>(daysOut,x => x);
			checkMerchantNumByProv.Visible=PrefC.HasClinicsEnabled;
			checkMerchantNumByProv.Checked=false;
			if(PrefC.HasClinicsEnabled && PIn.Bool(ProgramProperties.GetPropValFromList(_listProgramProperties,
				ProgramProperties.PropertyDescs.CareCredit.CareCreditIsMerchantNumberByProv)))
			{
				checkMerchantNumByProv.Checked=true;
			}
			if(PrefC.HasClinicsEnabled) {
				textMerchantNumberClinic.Visible=!checkMerchantNumByProv.Checked;
				labelMerchantNumberClinic.Visible=!checkMerchantNumByProv.Checked;
				textMerchantNumberClinic.Text=PIn.String(ProgramProperties.GetPropValFromList(_listProgramProperties,
					ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber,comboClinics.ClinicNumSelected));
			}
			else {
				textMerchantNumberPractice.Visible=!checkMerchantNumByProv.Checked;
				labelMerchantNumberPractice.Visible=!checkMerchantNumByProv.Checked;
				textMerchantNumberPractice.Text=PIn.String(ProgramProperties.GetPropValFromList(_listProgramProperties,
					ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber));
			}
		}

		///<summary>Determine if the practice or currently selected clinic merchant is closed and if so, disable all controls that will allow the user to change settings that would affect CareCredit actions. A closed merchant number is one with a 'x' prepended.</summary>
		private void DisableControlsForMerchantClosed() {
			bool clinicMerchantClosed=CareCredit.IsMerchantNumClosed(textMerchantNumberClinic.Text);
			bool practiceMerchantClosed=CareCredit.IsMerchantNumClosed(textMerchantNumberPractice.Text);
			groupPromotions.Enabled=!(clinicMerchantClosed || practiceMerchantClosed);
			labelMerchantClosedDescription.Visible=(clinicMerchantClosed || practiceMerchantClosed);
		}

		///<summary>Returns the Merchant Number program property from the in memory list of program properties for ClinicNum 0.</summary>
		private string GetHQMerchantNumber() {
			return PIn.String(ProgramProperties.GetPropValFromList(_listProgramProperties,ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber,0));
		}

		///<summary>Adds any missing program property for the passed in clinic.</summary>
		private void AddNeededProgramProperties(long clinicNum) {
			if(!_listProgramProperties.Any(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber)) {
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=clinicNum,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber,
					ProgramNum=_program.ProgramNum,
					PropertyValue=""//default value to empty string.
				});
			}
			if(!_listProgramProperties.Any(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled)) {
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=clinicNum,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled,
					ProgramNum=_program.ProgramNum,
					PropertyValue=_listProgramProperties.Find(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled && x.ClinicNum==0)?.PropertyValue??"1",//"On"
				});
			}
			if(!_listProgramProperties.Any(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPaymentType)) {
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=clinicNum,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditPaymentType,
					ProgramNum=_program.ProgramNum,
					PropertyValue=_listProgramProperties.Find(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPaymentType && x.ClinicNum==0)?.PropertyValue??"0",
				});
			}
			if(!_listProgramProperties.Any(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchDays)) {
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=clinicNum,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchDays,
					ProgramNum=_program.ProgramNum,
					PropertyValue=_listProgramProperties
						.FirstOrDefault(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchDays && x.ClinicNum==0)?.PropertyValue??DEFAULT_DAYS.ToString(),
				});
			}
			//These program properties are not clinic specific
			if(!_listProgramProperties.Any(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPatField)) {
				PatFieldDef patFieldDef=PatFieldDefs.GetDeepCopy().FindAll(x => x.FieldType==PatFieldType.CareCreditStatus).FirstOrDefault();
				long progPropValue=patFieldDef?.PatFieldDefNum??0;
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=0,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditPatField,
					ProgramNum=_program.ProgramNum,
					PropertyValue=POut.String(progPropValue.ToString()),
				});
			}
			if(!_listProgramProperties.Any(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldPreApprovalAmt)) {
				PatFieldDef patFieldDef=PatFieldDefs.GetDeepCopy().FindAll(x => x.FieldType==PatFieldType.CareCreditPreApprovalAmt).FirstOrDefault();
				long progPropValue=patFieldDef?.PatFieldDefNum??0;
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=0,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldPreApprovalAmt,
					ProgramNum=_program.ProgramNum,
					PropertyValue=POut.String(progPropValue.ToString()),
				});
			}
			if(!_listProgramProperties.Any(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldAvailableCredit)) {
				PatFieldDef patFieldDef=PatFieldDefs.GetDeepCopy().FindAll(x => x.FieldType==PatFieldType.CareCreditAvailableCredit).FirstOrDefault();
				long progPropValue=patFieldDef?.PatFieldDefNum??0;
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=0,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldAvailableCredit,
					ProgramNum=_program.ProgramNum,
					PropertyValue=POut.String(progPropValue.ToString()),
				});
			}
			if(!_listProgramProperties.Any(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditIsMerchantNumberByProv)) {
				_listProgramProperties.Add(new ProgramProperty() {
					ClinicNum=0,
					PropertyDesc=ProgramProperties.PropertyDescs.CareCredit.CareCreditIsMerchantNumberByProv,
					ProgramNum=_program.ProgramNum,
					PropertyValue="0",//Default to false
				});
			}
		}

		///<summary>Sets the Payment Type combobox to the first "CareCredit" payment type in the combo box. A new "CareCredit" payment type will be created and added to the combo box if there were none to select when this method was invoked. A "CareCredit" payment type is guaranteed to be selected when this method returns.</summary>
		private void SetDefaultCareCreditPaymentType() {
			Def defCareCredit=comboPaymentType.Items.GetAll<Def>().FirstOrDefault(x => x.ItemName.ToLower()=="carecredit");
			if(defCareCredit!=null) {
				comboPaymentType.SetSelectedDefNum(defCareCredit.DefNum);
				return;
			}
			List<Def> listDefsPaymentTypes=Defs.GetDefsForCategory(DefCat.PaymentTypes);//Include hidden defs on purpose.
			defCareCredit=listDefsPaymentTypes.FirstOrDefault(x => x.ItemName.ToLower()=="carecredit");
			if(defCareCredit==null) {
				//Create a new CareCredit Def
				defCareCredit=new Def() {
					Category=DefCat.PaymentTypes,
					ItemOrder=listDefsPaymentTypes.Max(x => x.ItemOrder)+1,
					ItemName="CareCredit",
					IsHidden=false,
				};
				DefL.Insert(defCareCredit);
				DataValid.SetInvalid(InvalidType.Defs);
			}
			else {
				if(defCareCredit.IsHidden) {
					defCareCredit.IsHidden=false;
					DefL.Update(defCareCredit);
					DataValid.SetInvalid(InvalidType.Defs);
				}
			}
			comboPaymentType.Items.AddDefs(new List<Def>() { defCareCredit });
			comboPaymentType.SetSelectedDefNum(defCareCredit.DefNum);
		}

		private void SetLabelQSDaysText(EnumCareCreditBatch enumCareCreditBatch){
			string labelText="Number of days out to check for pre-approvals for future appointments.";
			if(enumCareCreditBatch==EnumCareCreditBatch.LookupOnly){
				labelText="Number of days out to check for lookups for future appointments.";
			}
			labelQSDays.Text=labelText;
		}

		///<summary>Updates each clinic's property values with the values on the form. Validation should happen before calling this method.</summary>
		private void UpdatePropertiesInMemory(long clinicNum) {
			string payTypeSelected="0";
			if(comboPaymentType.SelectedIndex>-1) {
				payTypeSelected=comboPaymentType.GetSelectedDefNum().ToString();
			}
			int days=DEFAULT_DAYS;
			if(comboBoxDaysOut.SelectedIndex>-1) {
				int.TryParse(comboBoxDaysOut.GetSelectedKey<int>(x => x).ToString(),out days);
			}
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum
				&& x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled)
				.ForEach(x => x.PropertyValue=POut.String(((int)listBoxQSOptions.GetSelected<EnumCareCreditBatch>()).ToString()));
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPaymentType)
				.ForEach(x => x.PropertyValue=POut.String(payTypeSelected));
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchDays)
				.ForEach(x => x.PropertyValue=POut.String(days.ToString()));
			string merchantNumber=POut.String(textMerchantNumberPractice.Text);
			if(PrefC.HasClinicsEnabled) {
				merchantNumber=POut.String(textMerchantNumberClinic.Text);
			}
			_listProgramProperties.FindAll(x => x.ClinicNum==clinicNum && x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber)
				.ForEach(x => x.PropertyValue=merchantNumber);
			//Update program properties that are not clinic specific
			if(comboPatFieldDef.SelectedIndex>-1) {
				_listProgramProperties.FindAll(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPatField)
				.ForEach(x => x.PropertyValue=POut.String(comboPatFieldDef.GetSelected<PatFieldDef>().PatFieldDefNum.ToString()));
			}
			if(comboPatFieldPreApprvAmt.SelectedIndex>-1) {
				_listProgramProperties.FindAll(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldPreApprovalAmt)
				.ForEach(x => x.PropertyValue=POut.String(comboPatFieldPreApprvAmt.GetSelected<PatFieldDef>().PatFieldDefNum.ToString()));
			}
			if(comboPatFieldAvailableCredit.SelectedIndex>-1) {
				_listProgramProperties.FindAll(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditPatFieldAvailableCredit)
				.ForEach(x => x.PropertyValue=POut.String(comboPatFieldAvailableCredit.GetSelected<PatFieldDef>().PatFieldDefNum.ToString()));
			}
			_listProgramProperties.FindAll(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditIsMerchantNumberByProv)
				.ForEach(x => x.PropertyValue=POut.Bool(checkMerchantNumByProv.Checked));
		}

		private void textMerchantNumberPractice_Leave(object sender,EventArgs e) {
			DisableControlsForMerchantClosed();
		}

		private void comboClinics_SelectionChangeCommitted(object sender,EventArgs e) {
			if(comboClinics.ClinicNumSelected==_previouslySelectedClinicNum) {//didn't change the selected clinic
				return;
			}
			//if CareCredit is enabled and the username and key are set for the current clinic,
			//make the user select a payment type before switching clinics
			if(checkEnabled.Checked && !IsValid(_previouslySelectedClinicNum)) {
				comboClinics.ClinicNumSelected=_previouslySelectedClinicNum;
				return;
			}
			UpdatePropertiesInMemory(_previouslySelectedClinicNum);
			//Update previously selected clinic num since we have updated the values for the clinic.
			_previouslySelectedClinicNum=comboClinics.ClinicNumSelected;
			FillFields();
			DisableControlsForMerchantClosed();
		}

		private void textMerchantNumberClinic_Leave(object sender,EventArgs e) {
			DisableControlsForMerchantClosed();
		}

		private void listQSBatch_SelectionChanged(object sender,EventArgs e) {
			groupQSBatch.Visible=true;
			if(listBoxQSOptions.GetSelected<EnumCareCreditBatch>()==EnumCareCreditBatch.Off){
				groupQSBatch.Visible=false;
			}
			SetLabelQSDaysText(listBoxQSOptions.GetSelected<EnumCareCreditBatch>());
		}

		private void checkMerchantNumByProv_CheckedChanged(object sender,EventArgs e) {
			bool isMerchantClosed=CareCredit.IsMerchantNumClosed(textMerchantNumberClinic.Text) || CareCredit.IsMerchantNumClosed(textMerchantNumberPractice.Text);
			labelMerchantClosedDescription.Visible=(!checkMerchantNumByProv.Checked && isMerchantClosed);
			if(PrefC.HasClinicsEnabled) {
				textMerchantNumberClinic.Visible=!checkMerchantNumByProv.Checked;
				labelMerchantNumberClinic.Visible=!checkMerchantNumByProv.Checked;
			}
			else {
				textMerchantNumberPractice.Visible=!checkMerchantNumByProv.Checked;
				labelMerchantNumberPractice.Visible=!checkMerchantNumByProv.Checked;
			}
		}

		private bool IsValid(long clinicNum) {
			if(comboPaymentType.SelectedIndex<0) {
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"No default payment type set. Set a default automatically?","CareCredit")) {
					return false;//They want to set the default payment type.
				}
				//Set a default CareCredit payment type.
				SetDefaultCareCreditPaymentType();
			}
			if(listBoxQSOptions.GetSelected<EnumCareCreditBatch>()!=EnumCareCreditBatch.Off && comboBoxDaysOut.SelectedIndex<0) {
				MsgBox.Show(this,"Number of days out Batch Quickscreen Setting required.");
				return false;
			}
			if(comboPatFieldDef.SelectedIndex<0) {
				//Program is enabled and a patfielddef is not selected or merchant numbers is not by providers and the.
				MsgBox.Show(this,"Approval Status Patient Field is required.");
				return false;
			}
			if(!checkMerchantNumByProv.Checked && !IsMerchantNumberValid(clinicNum)) {
				//User has entered a merchant number but the value does not meet the requirements for a valid Merchant Number
				MsgBox.Show(this,"Merchant Number must be a valid 16-digit number.");
				return false;
			}
			return true;
		}

		private bool IsMerchantNumberValid(long clinicNum) {
			string merchantNumber=textMerchantNumberPractice.Text;
			if(PrefC.HasClinicsEnabled) {
				merchantNumber=textMerchantNumberClinic.Text;
			}
			if(clinicNum!=0 && CareCredit.IsMerchantNumValid(GetHQMerchantNumber(),allowClosedMerchant:true) && string.IsNullOrEmpty(merchantNumber)) {
				//Not HQ Merchant Number. OK if the non HQ ClinicNum's Merchant Number is empty. No override for the clinic
				return true;
			}
			return CareCredit.IsMerchantNumValid(merchantNumber,allowClosedMerchant:true);
		}

		private void butPromotions_Click(object sender,EventArgs e) {
			if(!checkEnabled.Enabled) {
				MsgBox.Show(this,"CareCredit must be enabled.");
				return;
			}
			if(checkMerchantNumByProv.Checked) {
				if(comboClinics.GetSelectedClinic()==null) {
					MsgBox.Show(this,"Please select a clinic.");
					return;
				}
				List<Provider> listProviderOverrides=Providers.GetAll();
				FrmProviderPick frmProviderPick=new FrmProviderPick(listProviderOverrides);
				frmProviderPick.ProvNumSelected=Security.CurUser.ProvNum;
				frmProviderPick.ShowDialog();
				if(!frmProviderPick.IsDialogOK) {
					return;
				}
				if(frmProviderPick.ProvNumSelected==0) {
					MsgBox.Show(this,"No provider selected. Choose a provider and try again.");
					return;
				}
				CareCreditL.LaunchAdminPage(frmProviderPick.ProvNumSelected,comboClinics.GetSelectedClinic().ClinicNum,isProvOverride:true);
			}
			else {
				string merchantNumber=textMerchantNumberPractice.Text;
				if(PrefC.HasClinicsEnabled) {
					merchantNumber=textMerchantNumberClinic.Text;
				}
				CareCreditL.LaunchAdminPage(merchantNumber);
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(checkEnabled.Checked && !IsValid(comboClinics.ClinicNumSelected)) {
				return;
			}
			CareCreditConfig config=BuildConfigFromForm(out string error);
			if(config==null) {
				MsgBox.Show(error);
				return;
			}
			string problem=CareCredit.GetConnectionChangeProblem(_configSaved,config,_hasHighSecurity);
			if(problem!="") {
				MsgBox.Show(problem);
				return;
			}
			problem=GetTypedValueProblem(config);
			if(problem!="") {
				MsgBox.Show(problem);
				return;
			}
			List<string> listDestinationChanges=CareCreditConfig.GetDestinationChanges(_configSaved,config);
			if(checkEnabled.Checked) {
				List<string> listProblems=config.GetProblems();
				if(listProblems.Count>0) {
					MsgBox.Show(Lan.g(this,"CareCredit can't be enabled until the connection settings are complete")+":\r\n"+string.Join("\r\n",listProblems));
					return;
				}
				if(_verifiedFingerprint=="" || _verifiedFingerprint!=config.GetVerificationFingerprint()) {
					MsgBox.Show(this,"Click Test connection with these settings before enabling CareCredit.");
					return;
				}
			}
			if(!CareCreditSettings.TryParseTimeOfDay(textProcessTime.Text,out TimeSpan timeProcess)
				|| !CareCreditSettings.TryParseTimeOfDay(textPullbackTime.Text,out TimeSpan timePullback))
			{
				MsgBox.Show(this,"Enter the batch times as 24-hour times, such as 07:20.");
				return;
			}
			//Set the program property value before saving.
			UpdatePropertiesInMemory(comboClinics.ClinicNumSelected);
			string secretStoredOld=GetPracticeListValue(CareCreditSettings.ClientSecret);
			SetPracticeListValue(CareCreditSettings.ClientId,config.ClientId);
			SetPracticeListValue(CareCreditSettings.ClientSecret,config.ClientSecretStored,isSecret:true);
			SetPracticeListValue(CareCreditSettings.Environment,config.Environment.ToString());
			SetPracticeListValue(CareCreditSettings.ApiBaseUrl,config.ApiBaseUrl);
			SetPracticeListValue(CareCreditSettings.IhsBaseUrl,config.IhsBaseUrl);
			SetPracticeListValue(ProgramProperties.PropertyDescs.CareCredit.CareCreditPartnerCode,config.PartnerCode);
			SetPracticeListValue(CareCreditSettings.TokenPath,config.TokenPath);
			SetPracticeListValue(CareCreditSettings.PrefillPath,config.PrefillPath);
			SetPracticeListValue(CareCreditSettings.PullbackPath,config.PullbackPath);
			SetPracticeListValue(CareCreditSettings.BatchPath,config.BatchPath);
			SetPracticeListValue(CareCreditSettings.SessionParam,config.SessionParam);
			SetPracticeListValue(CareCreditSettings.ClientAuth,config.ClientAuth.ToString());
			SetPracticeListValue(CareCreditSettings.TokenScope,config.Scope);
			SetPracticeListValue(CareCreditSettings.VerifiedConfig,_verifiedFingerprint);
			SetPracticeListValue(CareCreditSettings.JobComputerName,_jobComputerName);
			ReloadJobManagedRows();
			//The token Open Dental's HQ handed out was issued to Open Dental's app. It is never used; clear it.
			_listProgramProperties.FindAll(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditOAuthToken)
				.ForEach(x => x.PropertyValue="");
			#region Save
			//Only update the program if the IsEnabled flag has changed
			if(_program.Enabled!=checkEnabled.Checked) {
				_program.Enabled=checkEnabled.Checked;
				Programs.Update(_program);
			}
			ProgramProperties.Sync(_listProgramProperties,_program.ProgramNum);
			#endregion
			if(secretStoredOld!=config.ClientSecretStored) {
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,_program.ProgDesc+"'s "+CareCreditSettings.ClientSecret+" was altered.",
					_program.ProgramNum,DateTime.Now);
			}
			if(listDestinationChanges.Count>0) {
				SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,_program.ProgDesc+"'s connection settings were altered. "
					+string.Join("; ",listDestinationChanges),_program.ProgramNum,DateTime.Now);
			}
			bool isPrefChanged=Prefs.UpdateDateT(PrefName.CareCreditBatchProcessDateTime,CareCreditSettings.GetPrefValueForTime(timeProcess));
			isPrefChanged|=Prefs.UpdateDateT(PrefName.CareCreditBatchPullbackDateTime,CareCreditSettings.GetPrefValueForTime(timePullback));
			if(isPrefChanged) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			DataValid.SetInvalid(InvalidType.Programs);
			bool hasCareCreditQSBatchEnabled=_listProgramProperties.Any(x => x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled
				&& PIn.Enum<EnumCareCreditBatch>(x.PropertyValue)!=EnumCareCreditBatch.Off);
			if(checkEnabled.Checked && hasCareCreditQSBatchEnabled && !_hasCareCreditPatFieldDefNum) {
				DoShowApptViewWindow=MsgBox.Show(this,MsgBoxButtons.YesNo,$"Add the {PatFieldType.CareCreditStatus.GetDescription()} to appointment views?");
			}
			DialogResult=DialogResult.OK;
		}

	}
}
