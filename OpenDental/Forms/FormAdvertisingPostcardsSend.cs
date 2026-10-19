using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDental.UI;
using OpenDentBusiness;
using OpenDentBusiness.PostcardMania;

namespace OpenDental {
	///<summary>Tools > Advertising > Postcards: mail a postcard to selected patients through the office's own PostcardMania account. Pick a clinic,
	///patients and a design; check addresses and get an estimate; then place the order after an explicit confirmation (it is a purchase).
	///Only name and mailing address leave the building. The vendor is only contacted when this window opens or a button is pressed.</summary>
	public partial class FormAdvertisingPostcardsSend:FormODBase {
		private long _clinicNum;
		private PostcardManiaAccount _account;
		private List<PatientInfo> _listPatientInfos=new List<PatientInfo>();
		private PostcardRecipientList _postcardRecipientList=new PostcardRecipientList();
		private List<PcmDesign> _listDesigns=new List<PcmDesign>();
		private List<PcmOrder> _listOrders=new List<PcmOrder>();
		///<summary>The estimate shown, and what it was for. Placing an order needs an estimate for exactly the current mailing.</summary>
		private PcmPricingResponse _pcmPricingResponse;
		private string _estimateSignature;
		///<summary>This mailing's order reference, made once and kept until the mailing is placed (or the window closes), so a retry can't place it
		///twice.</summary>
		private string _extRefNbrBase;
		///<summary>True once an order was attempted with _extRefNbrBase; the next attempt looks each order up first.</summary>
		private bool _isExtRefNbrAttempted;
		///<summary>True when the last attempt ended without knowing whether the order was placed.</summary>
		private bool _isOutcomeUnknown;
		///<summary>Bumped on each design change, so a slow proof download for an earlier design is ignored.</summary>
		private int _proofRequestId;

		public FormAdvertisingPostcardsSend() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormAdvertisingPostcardsSend_Load(object sender,EventArgs e) {
			menuMain.Add(Lan.g(this,"Setup"),menuSetup_Click);
			textMailDate.Text=DateTime.Today.ToShortDateString();
			FillClinics();
			FillGridOrders();
			LoadAccount();
		}

		private void FillClinics() {
			comboClinic.Items.Clear();
			if(!PrefC.HasClinicsEnabled) {
				labelClinic.Visible=false;
				comboClinic.Visible=false;
				_clinicNum=0;
				return;
			}
			List<Clinic> listClinics=Clinics.GetForUserod(Security.CurUser);
			if(!Security.CurUser.ClinicIsRestricted) {
				comboClinic.Items.Add(Lan.g(this,"Practice (no clinic)"),new Clinic { ClinicNum=0 });
			}
			for(int i=0;i<listClinics.Count;i++) {
				comboClinic.Items.Add(listClinics[i].Abbr,listClinics[i]);
			}
			int index=comboClinic.Items.GetAll<Clinic>().FindIndex(x => x.ClinicNum==Clinics.ClinicNum);
			comboClinic.SelectedIndex=Math.Max(0,index);
			_clinicNum=comboClinic.GetSelected<Clinic>()?.ClinicNum??0;
		}

		///<summary>Reads the clinic's account (its own or the default) and fetches its designs and recent orders.</summary>
		private void LoadAccount() {
			_account=AdvertisingPostcards.GetAccount(_clinicNum);
			labelReturnAddressValue.Text=AdvertisingPostcards.DescribeReturnAddress(AdvertisingPostcards.BuildReturnAddress(_clinicNum));
			if(labelReturnAddressValue.Text=="") {
				labelReturnAddressValue.Text=Lan.g(this,"None. Enter the practice or clinic address so cards carry a return address.");
			}
			_listDesigns=new List<PcmDesign>();
			_listOrders=new List<PcmOrder>();
			if(_account.IsConfigured) {
				RefreshFromVendor(isDesigns:true,isOrders:true);
			}
			FillDesigns(0);
			FillGridOrders();
			ClearEstimate();
			UpdateUi();
		}

		private void UpdateUi() {
			bool isConfigured=_account!=null && _account.IsConfigured;
			bool isBaaOnFile=PostcardManiaProgram.IsBaaOnFile();
			labelSandbox.Visible=isConfigured && _account.IsSandbox;
			labelBaaBlocked.Visible=!isBaaOnFile;
			if(!isConfigured) {
				labelSandbox.Visible=true;
				labelSandbox.Text=Lan.g(this,"No API key and secret are set up for this clinic or the default. Use Setup.");
			}
			else {
				labelSandbox.Text=Lan.g(this,"Sandbox: nothing will be printed or mailed.");
			}
			PcmDesign design=GetSelectedDesign();
			bool hasRecipients=_postcardRecipientList.ListRecipients.Count>0;
			butRefreshDesigns.Enabled=isConfigured;
			butNewDesign.Enabled=isConfigured;
			butEditDesign.Enabled=isConfigured && design!=null && !design.IsUpload;
			butCheckAddresses.Enabled=isConfigured && isBaaOnFile && hasRecipients;
			butEstimate.Enabled=isConfigured && isBaaOnFile && hasRecipients && design!=null;
			butSend.Enabled=isConfigured && isBaaOnFile && hasRecipients && design!=null;
			butRefreshOrders.Enabled=isConfigured;
			PcmOrder order=gridOrders.SelectedTag<PcmOrder>();
			butCancelOrder.Enabled=isConfigured && order!=null && order.IsCancelable;
			textRecipients.Text=_postcardRecipientList.ListRecipients.Count.ToString();
		}

		///<summary>Fetches designs and/or recent orders in one progress window. Shows a message and returns false on failure.</summary>
		private bool RefreshFromVendor(bool isDesigns,bool isOrders) {
			PostcardManiaAccount account=_account;
			long clinicNum=_clinicNum;
			bool isAllOrders=checkAllOrders.Checked;
			List<PcmDesign> listDesigns=null;
			List<PcmOrder> listOrders=null;
			bool isOk=RunVendorCall(Lan.g(this,"Contacting the postcard service")+"...",() => {
				if(isDesigns) {
					listDesigns=AdvertisingPostcards.GetDesigns(account);
				}
				if(isOrders) {
					listOrders=AdvertisingPostcards.GetRecentOrders(account,isAllOrders ? (long?)null : clinicNum,1,100);
				}
			});
			if(listDesigns!=null) {
				_listDesigns=listDesigns;
			}
			if(listOrders!=null) {
				_listOrders=listOrders;
			}
			return isOk;
		}

		///<summary>Runs a vendor call in a progress window. Returns false (after telling the user) if it failed or was cancelled.</summary>
		private bool RunVendorCall(string startingMessage,Action action) {
			try {
				ProgressWin progressWin=new ProgressWin();
				progressWin.ActionMain=action;
				progressWin.StartingMessage=startingMessage;
				progressWin.ShowDialog();
				return !progressWin.IsCancelled;
			}
			catch(PostcardManiaException ex) {
				MsgBox.Show(this,ex.Message);
				return false;
			}
			catch(ODException ex) {
				MsgBox.Show(this,ex.Message);
				return false;
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"The postcard service call failed."),ex);
				return false;
			}
		}

		#region Recipients
		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			long clinicNum=comboClinic.GetSelected<Clinic>()?.ClinicNum??0;
			if(clinicNum==_clinicNum) {
				return;
			}
			_clinicNum=clinicNum;
			_extRefNbrBase=null;
			_isExtRefNbrAttempted=false;
			_isOutcomeUnknown=false;
			LoadAccount();
		}

		private void butSelectPatients_Click(object sender,EventArgs e) {
			using FormAdvertisingPatientList formAdvertisingPatientList=new FormAdvertisingPatientList(AdvertisingType.Postcards);
			formAdvertisingPatientList.ListPatientInfos=_listPatientInfos;
			formAdvertisingPatientList.ShowDialog();
			if(formAdvertisingPatientList.DialogResult!=DialogResult.OK) {
				return;
			}
			_listPatientInfos=formAdvertisingPatientList.ListPatientInfos;
			RebuildRecipients();
		}

		private void checkHousehold_Click(object sender,EventArgs e) {
			RebuildRecipients();
		}

		private void RebuildRecipients() {
			Dictionary<long,Patient> dictGuarantors=null;
			if(checkHousehold.Checked && _listPatientInfos.Count>0) {
				dictGuarantors=AdvertisingPostcards.GetGuarantorsByPatNum(_listPatientInfos.Select(x => x.PatNum).Distinct().ToList());
			}
			_postcardRecipientList=AdvertisingPostcards.BuildRecipients(_listPatientInfos,PostcardManiaProgram.GetUsePreferredName(),
				PostcardManiaProgram.GetSendPatientReference(),checkHousehold.Checked,dictGuarantors);
			FillRecipientNotes();
			ClearEstimate();
			UpdateUi();
		}

		private void FillRecipientNotes() {
			List<string> listNotes=new List<string>();
			if(_postcardRecipientList.CountNonUs>0) {
				listNotes.Add(_postcardRecipientList.CountNonUs+" "+Lan.g(this,"outside the US skipped"));
			}
			if(_postcardRecipientList.CountIncompleteAddress>0) {
				listNotes.Add(_postcardRecipientList.CountIncompleteAddress+" "+Lan.g(this,"with an incomplete address skipped"));
			}
			if(_postcardRecipientList.CountHouseholdMerged>0) {
				listNotes.Add(_postcardRecipientList.CountHouseholdMerged+" "+Lan.g(this,"share a household card"));
			}
			labelRecipientNotes.Text=string.Join("; ",listNotes);
		}
		#endregion Recipients

		#region Designs
		private PcmDesign GetSelectedDesign() {
			return comboDesign.GetSelected<PcmDesign>();
		}

		///<summary>Fills the design combo and selects designIDSelect if present (0 keeps the current selection when possible).</summary>
		private void FillDesigns(long designIDSelect) {
			long designIDKeep=designIDSelect;
			if(designIDKeep==0) {
				designIDKeep=GetSelectedDesign()?.DesignID??0;
			}
			comboDesign.Items.Clear();
			comboDesign.Items.AddList(_listDesigns,x => x.ToString());
			int index=_listDesigns.FindIndex(x => x.DesignID==designIDKeep);
			if(index>=0) {
				comboDesign.SelectedIndex=index;
			}
			else if(_listDesigns.Count>0) {
				comboDesign.SelectedIndex=0;
			}
			OnDesignChanged();
		}

		private void comboDesign_SelectionChangeCommitted(object sender,EventArgs e) {
			OnDesignChanged();
			ClearEstimate();
		}

		private void OnDesignChanged() {
			PcmDesign design=GetSelectedDesign();
			string mailClassKeep=comboMailClass.GetSelected<string>()??PostcardManiaProgram.GetDefaultMailClass();
			comboMailClass.Items.Clear();
			List<string> listMailClasses=new List<string>();
			if(design!=null && design.MailClasses!=null && design.MailClasses.Count>0) {
				listMailClasses=design.MailClasses.Where(x => x==PcmMailClass.FirstClass || x==PcmMailClass.Standard).Distinct().ToList();
			}
			if(listMailClasses.Count==0) {
				listMailClasses=new List<string> { PcmMailClass.FirstClass,PcmMailClass.Standard };
			}
			for(int i=0;i<listMailClasses.Count;i++) {
				comboMailClass.Items.Add(listMailClasses[i]==PcmMailClass.FirstClass ? Lan.g(this,"First Class") : Lan.g(this,"Standard"),listMailClasses[i]);
			}
			int index=listMailClasses.IndexOf(mailClassKeep);
			comboMailClass.SelectedIndex=Math.Max(0,index);
			labelDesignWarning.Text="";
			if(design!=null) {
				List<string> listCustomFields=design.GetMandatoryCustomFields();
				if(listCustomFields.Count>0) {
					labelDesignWarning.Text=Lan.g(this,"This design has required fields this window doesn't fill")+": "+string.Join(", ",listCustomFields);
				}
				else if(design.IsUpload) {
					labelDesignWarning.Text=Lan.g(this,"Uploaded designs can't be edited here.");
				}
			}
			LoadProof(design);
			UpdateUi();
		}

		///<summary>Downloads the proof image on a background thread (no credentials are sent) and shows it if the same design is still selected.</summary>
		private void LoadProof(PcmDesign design) {
			int proofRequestId=++_proofRequestId;
			Image imageOld=pictureProof.Image;
			pictureProof.Image=null;
			imageOld?.Dispose();
			string url=design?.ProofFront;
			if(string.IsNullOrWhiteSpace(url)) {
				return;
			}
			System.Threading.Tasks.Task.Run(() => {
				byte[] arrayBytes=null;
				try {
					arrayBytes=AdvertisingPostcards.GetProofImage(url);
				}
				catch(Exception ex) {
					ex.DoNothing();//The preview is optional.
				}
				if(arrayBytes==null || IsDisposed) {
					return;
				}
				try {
					BeginInvoke((Action)(() => ShowProof(proofRequestId,arrayBytes)));
				}
				catch(Exception ex) {
					ex.DoNothing();//The window closed meanwhile.
				}
			});
		}

		private void ShowProof(int proofRequestId,byte[] arrayBytes) {
			if(IsDisposed || proofRequestId!=_proofRequestId) {
				return;
			}
			try {
				using MemoryStream memoryStream=new MemoryStream(arrayBytes);
				using Image image=Image.FromStream(memoryStream);
				Image imageOld=pictureProof.Image;
				pictureProof.Image=new Bitmap(image);
				imageOld?.Dispose();
			}
			catch(Exception ex) {
				ex.DoNothing();//Not an image this window can show.
			}
		}

		private void butRefreshDesigns_Click(object sender,EventArgs e) {
			if(RefreshFromVendor(isDesigns:true,isOrders:false)) {
				FillDesigns(0);
			}
		}

		private void butEditDesign_Click(object sender,EventArgs e) {
			PcmDesign design=GetSelectedDesign();
			if(design==null || design.IsUpload) {
				return;
			}
			PostcardManiaAccount account=_account;
			PcmDesignEditorResponse pcmDesignEditorResponse=null;
			if(!RunVendorCall(Lan.g(this,"Opening the design editor")+"...",
				() => pcmDesignEditorResponse=AdvertisingPostcards.GetDesignEditorUrl(account,design.DesignID,duplicate:false)))
			{
				return;
			}
			ShowEditor(pcmDesignEditorResponse);
		}

		private void butNewDesign_Click(object sender,EventArgs e) {
			InputBoxParam inputBoxParamName=new InputBoxParam();
			inputBoxParamName.InputBoxType_=InputBoxType.TextBox;
			inputBoxParamName.LabelText=Lan.g(this,"Design name");
			inputBoxParamName.MaxLengthText=100;
			InputBoxParam inputBoxParamSize=new InputBoxParam();
			inputBoxParamSize.InputBoxType_=InputBoxType.ComboSelect;
			inputBoxParamSize.LabelText=Lan.g(this,"Size");
			List<Tuple<string,string>> listSizes=new List<Tuple<string,string>> {
				Tuple.Create("46","4.25 x 6 (First Class only)"),
				Tuple.Create("57","5 x 7"),
				Tuple.Create("58","5.5 x 8.5"),
				Tuple.Create("68","6 x 8.5"),
				Tuple.Create("69","6 x 9"),
				Tuple.Create("611","6 x 11"),
			};
			inputBoxParamSize.ListSelections=listSizes.Select(x => x.Item2).ToList();
			inputBoxParamSize.SelectedIndex=0;
			InputBox inputBox=new InputBox(inputBoxParamName,inputBoxParamSize);
			inputBox.Text=Lan.g(this,"New Design");
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return;
			}
			string name=(inputBox.StringResult??"").Trim();
			if(name=="") {
				MsgBox.Show(this,"Enter a name for the design.");
				return;
			}
			string size=listSizes[Math.Max(0,inputBox.SelectedIndex)].Item1;
			PostcardManiaAccount account=_account;
			PcmDesignEditorResponse pcmDesignEditorResponse=null;
			if(!RunVendorCall(Lan.g(this,"Creating the design")+"...",
				() => pcmDesignEditorResponse=AdvertisingPostcards.CreateDesign(account,name,size)))
			{
				return;
			}
			ShowEditor(pcmDesignEditorResponse);
		}

		///<summary>Shows the vendor's embedded editor. Its URL carries a short-lived token, so it opens only in this program's own browser window
		///(single use), never the system browser, and is never logged. Afterwards the design list is refreshed and the edited design selected.</summary>
		private void ShowEditor(PcmDesignEditorResponse pcmDesignEditorResponse) {
			if(pcmDesignEditorResponse==null || string.IsNullOrWhiteSpace(pcmDesignEditorResponse.Url)) {
				MsgBox.Show(this,"The postcard service did not return an editor address.");
				return;
			}
			if(HqHosts.IsBlocked(pcmDesignEditorResponse.Url) || VendorUrlPolicy.Check(pcmDesignEditorResponse.Url)!=VendorUrlProblem.None) {
				MsgBox.Show(this,"The editor address the postcard service returned is not allowed.");
				return;
			}
			using(FormWebView formWebView=new FormWebView()) {
				formWebView.UrlBrowseTo=pcmDesignEditorResponse.Url;
				formWebView.IsUrlSingleUse=true;
				formWebView.Title=Lan.g(this,"Postcard Design");
				formWebView.ShowDialog();
			}
			if(RefreshFromVendor(isDesigns:true,isOrders:false)) {
				FillDesigns(pcmDesignEditorResponse.DesignID);
			}
			ClearEstimate();
		}
		#endregion Designs

		#region Estimate and order
		private void comboMailClass_SelectionChangeCommitted(object sender,EventArgs e) {
			ClearEstimate();
		}

		private void textMailDate_TextChanged(object sender,EventArgs e) {
			ClearEstimate();
		}

		private void ClearEstimate() {
			_pcmPricingResponse=null;
			_estimateSignature=null;
			labelEstimate.Text="";
		}

		///<summary>Validates the form and builds the orders for the current mailing. Returns null (after telling the user) if something is missing.</summary>
		private List<PcmPostcardOrderRequest> BuildOrders() {
			PcmDesign design=GetSelectedDesign();
			if(design==null) {
				MsgBox.Show(this,"Pick a design.");
				return null;
			}
			if(_postcardRecipientList.ListRecipients.Count==0) {
				MsgBox.Show(this,"Select at least one patient with a complete US mailing address.");
				return null;
			}
			if(!textMailDate.IsValid() || textMailDate.Text=="") {
				MsgBox.Show(this,"Enter a valid mail date.");
				return null;
			}
			DateTime dateMail=PIn.Date(textMailDate.Text);
			if(dateMail<DateTime.Today) {
				MsgBox.Show(this,"The mail date can't be in the past.");
				return null;
			}
			PcmPostcardOrderRequest requestTemplate=new PcmPostcardOrderRequest();
			requestTemplate.DesignID=design.DesignID;
			requestTemplate.MailClass=comboMailClass.GetSelected<string>()??PcmMailClass.FirstClass;
			requestTemplate.MailDate=dateMail;
			PcmReturnAddress pcmReturnAddress=AdvertisingPostcards.BuildReturnAddress(_clinicNum);
			if(!string.IsNullOrWhiteSpace(pcmReturnAddress.Address)) {
				requestTemplate.ReturnAddress=pcmReturnAddress;
			}
			if(_extRefNbrBase==null) {
				_extRefNbrBase=AdvertisingPostcards.NewExtRefNbr(_clinicNum);
			}
			return AdvertisingPostcards.SplitIntoOrders(requestTemplate,_postcardRecipientList.ListRecipients,_extRefNbrBase);
		}

		///<summary>Identifies what an estimate was for, so a changed mailing needs a new one.</summary>
		private string GetSignature(List<PcmPostcardOrderRequest> listOrders) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(_clinicNum).Append('|').Append(_account?.GetCacheKey()).Append('|');
			for(int i=0;i<listOrders.Count;i++) {
				stringBuilder.Append(PcmJson.Serialize(listOrders[i])).Append('|');
			}
			return TokenCache.Fingerprint(stringBuilder.ToString());
		}

		private void butCheckAddresses_Click(object sender,EventArgs e) {
			if(!PostcardManiaProgram.IsBaaOnFile()) {
				return;
			}
			PostcardManiaAccount account=_account;
			List<PcmRecipient> listRecipients=_postcardRecipientList.ListRecipients.ToList();
			PostcardVerifyResult postcardVerifyResult=null;
			if(!RunVendorCall(Lan.g(this,"Checking addresses")+"...",() => postcardVerifyResult=AdvertisingPostcards.VerifyRecipients(account,listRecipients))) {
				return;
			}
			if(postcardVerifyResult.ListInvalidIndexes.Count==0) {
				MessageBox.Show(this,Lan.g(this,"Valid addresses")+": "+postcardVerifyResult.CountValid+"\r\n"
					+Lan.g(this,"Invalid addresses")+": "+postcardVerifyResult.CountInvalid);
				return;
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lan.g(this,"Valid addresses")+": "+postcardVerifyResult.CountValid);
			stringBuilder.AppendLine(Lan.g(this,"Invalid addresses")+": "+postcardVerifyResult.CountInvalid);
			stringBuilder.AppendLine();
			List<long> listPatNumsInvalid=postcardVerifyResult.ListInvalidIndexes.Select(x => _postcardRecipientList.ListPatNums[x]).ToList();
			List<Patient> listPatients=Patients.GetLimForPats(listPatNumsInvalid);
			for(int i=0;i<listPatients.Count && i<20;i++) {
				stringBuilder.AppendLine(listPatients[i].GetNameLF());
			}
			if(listPatients.Count>20) {
				stringBuilder.AppendLine("...");
			}
			stringBuilder.AppendLine();
			stringBuilder.Append(Lan.g(this,"Remove the invalid addresses from this mailing?"));
			if(MessageBox.Show(this,stringBuilder.ToString(),Lan.g(this,"Check Addresses"),MessageBoxButtons.YesNo)!=DialogResult.Yes) {
				return;
			}
			_postcardRecipientList.RemoveAtIndexes(postcardVerifyResult.ListInvalidIndexes);
			ClearEstimate();
			UpdateUi();
		}

		private void butEstimate_Click(object sender,EventArgs e) {
			Estimate();
		}

		///<summary>Gets the vendor's estimate for the current mailing and shows it. Returns false if it failed.</summary>
		private bool Estimate() {
			if(!PostcardManiaProgram.IsBaaOnFile()) {
				return false;
			}
			List<PcmPostcardOrderRequest> listOrders=BuildOrders();
			if(listOrders==null) {
				return false;
			}
			PostcardManiaAccount account=_account;
			PcmPricingResponse pcmPricingResponse=null;
			if(!RunVendorCall(Lan.g(this,"Getting an estimate")+"...",() => pcmPricingResponse=AdvertisingPostcards.Estimate(account,listOrders))) {
				return false;
			}
			_pcmPricingResponse=pcmPricingResponse;
			_estimateSignature=GetSignature(listOrders);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lan.g(this,"Estimated total")+": "+(pcmPricingResponse.EstimatedTotalPrice??0).ToString("c"));
			foreach(PcmPricingLineItem lineItem in pcmPricingResponse.LineItems.Take(3)) {
				stringBuilder.AppendLine(lineItem.Name+": "+lineItem.Quantity+" x "+(lineItem.Price??0).ToString("c")+" = "+(lineItem.LineTotal??0).ToString("c"));
			}
			labelEstimate.Text=stringBuilder.ToString().TrimEnd();
			return true;
		}

		private void butSend_Click(object sender,EventArgs e) {
			if(!PostcardManiaProgram.IsBaaOnFile() || !Security.IsAuthorized(EnumPermType.Advertising)) {
				return;
			}
			List<PcmPostcardOrderRequest> listOrders=BuildOrders();
			if(listOrders==null) {
				return;
			}
			if(_pcmPricingResponse==null || _estimateSignature!=GetSignature(listOrders)) {
				if(!Estimate()) {
					return;
				}
				listOrders=BuildOrders();
				if(listOrders==null) {
					return;
				}
			}
			PcmDesign design=GetSelectedDesign();
			int countRecipients=listOrders.Sum(x => x.Recipients.Count);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lan.g(this,"This places a paid order with the vendor."));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(Lan.g(this,"Recipients")+": "+countRecipients+(listOrders.Count>1 ? " ("+listOrders.Count+" "+Lan.g(this,"orders")+")" : ""));
			stringBuilder.AppendLine(Lan.g(this,"Design")+": "+design);
			stringBuilder.AppendLine(Lan.g(this,"Mail class")+": "+comboMailClass.GetStringSelectedItems()+", "+Lan.g(this,"mail date")+": "
				+PIn.Date(textMailDate.Text).ToShortDateString());
			stringBuilder.AppendLine(Lan.g(this,"Estimated total")+": "+(_pcmPricingResponse.EstimatedTotalPrice??0).ToString("c"));
			stringBuilder.AppendLine(Lan.g(this,"Environment")+": "+(_account.IsSandbox ? Lan.g(this,"Sandbox (nothing is printed or mailed)") : Lan.g(this,"Production")));
			if(_isExtRefNbrAttempted) {
				stringBuilder.AppendLine();
				stringBuilder.AppendLine(Lan.g(this,"This mailing was sent before. Orders that already reached the vendor are found and not placed again."));
			}
			stringBuilder.AppendLine();
			stringBuilder.Append(Lan.g(this,"Place the order?"));
			if(MessageBox.Show(this,stringBuilder.ToString(),Lan.g(this,"Place Order"),MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return;
			}
			PostcardManiaAccount account=_account;
			bool isRetry=_isExtRefNbrAttempted;
			_isExtRefNbrAttempted=true;
			PostcardOrderOutcome postcardOrderOutcome=null;
			try {
				ProgressWin progressWin=new ProgressWin();
				progressWin.ActionMain=() => postcardOrderOutcome=AdvertisingPostcards.PlaceOrder(account,listOrders,isRetry);
				progressWin.StartingMessage=Lan.g(this,"Placing the order")+"...";
				progressWin.MessageCancel=Lan.g(this,"The order may already have reached the vendor. If you stop now, check Recent orders before sending again. Stop anyway?");
				progressWin.ShowDialog();
				if(progressWin.IsCancelled) {
					_isOutcomeUnknown=true;
					SecurityLogs.MakeLogEntry(EnumPermType.Advertising,0,"Postcards order stopped by the user, outcome unknown. Reference "+_extRefNbrBase+".");
					FillGridOrdersFromVendor();
					return;
				}
			}
			catch(Exception ex) {
				_isOutcomeUnknown=true;
				SecurityLogs.MakeLogEntry(EnumPermType.Advertising,0,"Postcards order failed, outcome unknown. Reference "+_extRefNbrBase+".");
				FriendlyException.Show(Lan.g(this,"Placing the order failed. Check Recent orders before sending again."),ex);
				return;
			}
			MakeOrderLogEntry(listOrders,postcardOrderOutcome);
			ShowOutcome(postcardOrderOutcome,countRecipients);
			FillGridOrdersFromVendor();
		}

		///<summary>Audit entry for an order attempt: clinic, design, counts, estimate, order IDs and environment. Never patient names.</summary>
		private void MakeOrderLogEntry(List<PcmPostcardOrderRequest> listOrders,PostcardOrderOutcome postcardOrderOutcome) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("Postcards order "+(postcardOrderOutcome.IsComplete ? "placed" : (postcardOrderOutcome.IsOutcomeUnknown ? "outcome unknown" : "failed")));
			stringBuilder.Append(". Clinic: "+(_clinicNum==0 ? "none" : Clinics.GetAbbr(_clinicNum)));
			stringBuilder.Append(", design "+listOrders[0].DesignID);
			stringBuilder.Append(", recipients "+listOrders.Sum(x => x.Recipients.Count));
			stringBuilder.Append(", estimate "+(_pcmPricingResponse?.EstimatedTotalPrice??0).ToString("c"));
			stringBuilder.Append(", reference "+_extRefNbrBase);
			List<string> listOrderIDs=postcardOrderOutcome.ListResults.Where(x => x.OrderID!=0).Select(x => x.OrderID.ToString()).ToList();
			if(listOrderIDs.Count>0) {
				stringBuilder.Append(", order IDs "+string.Join(",",listOrderIDs));
			}
			stringBuilder.Append(", "+(_account.IsSandbox ? "sandbox" : "production")+".");
			SecurityLogs.MakeLogEntry(EnumPermType.Advertising,0,stringBuilder.ToString());
		}

		private void ShowOutcome(PostcardOrderOutcome postcardOrderOutcome,int countRecipients) {
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=0;i<postcardOrderOutcome.ListResults.Count;i++) {
				PostcardOrderResult result=postcardOrderOutcome.ListResults[i];
				stringBuilder.Append(result.WasAlreadyPlaced ? Lan.g(this,"Already placed") : Lan.g(this,"Placed"));
				stringBuilder.Append(": "+Lan.g(this,"order")+" "+(result.OrderID==0 ? "?" : result.OrderID.ToString()));
				if(result.BatchID.HasValue) {
					stringBuilder.Append(", "+Lan.g(this,"batch")+" "+result.BatchID.Value);
				}
				stringBuilder.AppendLine(", "+result.CountRecipients+" "+Lan.g(this,"recipients"));
			}
			if(postcardOrderOutcome.IsComplete) {
				_isOutcomeUnknown=false;
				//The next mailing from this window gets a new reference.
				_extRefNbrBase=null;
				_isExtRefNbrAttempted=false;
				stringBuilder.AppendLine();
				stringBuilder.Append(Lan.g(this,"Payment problems, if any, are resolved in the vendor portal."));
				MessageBox.Show(this,stringBuilder.ToString(),Lan.g(this,"Order Placed"));
				ClearEstimate();
				return;
			}
			_isOutcomeUnknown=postcardOrderOutcome.IsOutcomeUnknown;
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(postcardOrderOutcome.Error.Message);
			if(postcardOrderOutcome.IsOutcomeUnknown) {
				stringBuilder.AppendLine();
				stringBuilder.Append(Lan.g(this,"It is not known whether the order reached the vendor. Place order again to retry: it looks the order up first and never places it twice."));
			}
			MessageBox.Show(this,stringBuilder.ToString(),Lan.g(this,"Order Not Complete"));
		}
		#endregion Estimate and order

		#region Recent orders
		private void FillGridOrders() {
			gridOrders.BeginUpdate();
			gridOrders.Columns.Clear();
			gridOrders.Columns.Add(new GridColumn(Lan.g(gridOrders.TranslationName,"Order"),70));
			gridOrders.Columns.Add(new GridColumn(Lan.g(gridOrders.TranslationName,"Date"),90));
			gridOrders.Columns.Add(new GridColumn(Lan.g(gridOrders.TranslationName,"Status"),100));
			gridOrders.Columns.Add(new GridColumn(Lan.g(gridOrders.TranslationName,"Design"),70));
			gridOrders.Columns.Add(new GridColumn(Lan.g(gridOrders.TranslationName,"Batch"),70));
			gridOrders.Columns.Add(new GridColumn(Lan.g(gridOrders.TranslationName,"Reference"),100){ IsWidthDynamic=true });
			gridOrders.ListGridRows.Clear();
			for(int i=0;i<_listOrders.Count;i++) {
				PcmOrder order=_listOrders[i];
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(order.OrderID.ToString());
				gridRow.Cells.Add(order.OrderDate??"");
				gridRow.Cells.Add(order.Status??"");
				gridRow.Cells.Add(order.DesignID?.ToString()??"");
				gridRow.Cells.Add(order.BatchID?.ToString()??"");
				gridRow.Cells.Add(order.ExtRefNbr??"");
				gridRow.Tag=order;
				gridOrders.ListGridRows.Add(gridRow);
			}
			gridOrders.EndUpdate();
			UpdateUi();
		}

		private void FillGridOrdersFromVendor() {
			if(_account==null || !_account.IsConfigured) {
				return;
			}
			RefreshFromVendor(isDesigns:false,isOrders:true);
			FillGridOrders();
		}

		private void butRefreshOrders_Click(object sender,EventArgs e) {
			FillGridOrdersFromVendor();
		}

		private void checkAllOrders_Click(object sender,EventArgs e) {
			FillGridOrdersFromVendor();
		}

		private void gridOrders_CellClick(object sender,ODGridClickEventArgs e) {
			UpdateUi();
		}

		private void butCancelOrder_Click(object sender,EventArgs e) {
			PcmOrder order=gridOrders.SelectedTag<PcmOrder>();
			if(order==null || !order.IsCancelable) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Cancel this order? Pending orders can be canceled until 11:30 PM Eastern on the day they were placed.")) {
				return;
			}
			PostcardManiaAccount account=_account;
			if(!RunVendorCall(Lan.g(this,"Canceling the order")+"...",() => AdvertisingPostcards.CancelOrder(account,order.OrderID))) {
				FillGridOrdersFromVendor();
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Advertising,0,"Postcards order "+order.OrderID+" canceled ("+(_account.IsSandbox ? "sandbox" : "production")+").");
			FillGridOrdersFromVendor();
		}

		private void butOpenPortal_Click(object sender,EventArgs e) {
			//The portal opens without any token: the user signs in with the office's own vendor login.
			try {
				SystemBrowser.Open(PostcardManiaProgram.PortalUrl);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Could not open the browser."),ex);
			}
		}
		#endregion Recent orders

		private void menuSetup_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			using FormAdvertisingPostcardsSetup formAdvertisingPostcardsSetup=new FormAdvertisingPostcardsSetup();
			formAdvertisingPostcardsSetup.ShowDialog();
			if(formAdvertisingPostcardsSetup.DialogResult!=DialogResult.OK) {
				return;
			}
			if(!PostcardManiaProgram.IsEnabled()) {
				Close();
				return;
			}
			RebuildRecipients();
			LoadAccount();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}

		private void FormAdvertisingPostcardsSend_FormClosing(object sender,FormClosingEventArgs e) {
			if(!_isOutcomeUnknown) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"It is not known whether the last order reached the vendor. Check Recent orders before mailing it again. Close anyway?")) {
				e.Cancel=true;
			}
		}
	}
}
