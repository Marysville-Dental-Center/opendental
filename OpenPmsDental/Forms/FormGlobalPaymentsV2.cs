// Global Payments V2 (Open Dental 25.4, v25_4-payments-globalpayments-v2): the transaction window opened from the payment window. Runs a sale or a
// refund on a card terminal or a saved card, or voids an earlier Global Payments V2 transaction. Talks to Global Payments directly with the office's own
// API app (OpenPmsDentalBusiness/VendorDirect/GlobalPayments). Vendor-neutral: no logos or links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.GlobalPayments;

namespace OpenPmsDental {
	///<summary>One Global Payments V2 transaction. Closes with OK only after an approved transaction; ResultOut and ReceiptOut then hold it, and
	///DoSaveCard says whether the user asked to keep the card. A declined or failed transaction keeps the window open. Every attempt is written to the
	///security log (amount, last four digits, terminal; never the card token).</summary>
	public partial class FormGlobalPaymentsV2:FormODBase {
		private readonly Patient _patient;
		private readonly long _clinicNum;
		private readonly decimal _amount;
		///<summary>A card saved through Global Payments V2 that the payment window had selected, or null.</summary>
		private readonly CreditCard _creditCardSaved;
		private readonly bool _canUseNewCard;
		///<summary>The Global Payments transaction of the payment being edited, for a void, or "".</summary>
		private readonly string _transactionIdVoid;
		private readonly decimal _amountVoid;
		private GlobalPaymentsV2Config _config;
		private List<PayTerminal> _listPayTerminals=new List<PayTerminal>();
		private CancellationTokenSource _cancellationTokenSource;
		private bool _isRunning;
		///<summary>The approved transaction, when the window closes with OK.</summary>
		public GlobalPaymentsV2Result ResultOut;
		///<summary>The receipt text of ResultOut.</summary>
		public string ReceiptOut="";
		///<summary>True if the user asked to save the card (and was allowed to).</summary>
		public bool DoSaveCard;

		///<summary>creditCardSaved: the payment window's selected card if it is a Global Payments V2 card, else null. canUseNewCard is false when the clinic's
		///"Prevent saving new cards" is set. transactionIdVoid and amountVoid: the transaction of the payment being edited, to offer a void; "" for none.
		///</summary>
		public FormGlobalPaymentsV2(Patient patient,long clinicNum,decimal amount,CreditCard creditCardSaved,bool canUseNewCard,string transactionIdVoid,
			decimal amountVoid)
		{
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_patient=patient;
			_clinicNum=clinicNum;
			_amount=Math.Abs(amount);
			_creditCardSaved=creditCardSaved;
			_canUseNewCard=canUseNewCard;
			_transactionIdVoid=transactionIdVoid??"";
			_amountVoid=Math.Abs(amountVoid);
		}

		private void FormGlobalPaymentsV2_Load(object sender,EventArgs e) {
			_config=GlobalPaymentsV2.GetConfig(_clinicNum);
			if(!GlobalPaymentsV2.IsEnabled() || !_config.IsComplete) {
				MsgBox.Show(this,"Global Payments V2 is not set up for this clinic. Enter the App ID and App Key in Setup > Program Links > Global Payments V2.");
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			if(_config.UseSandbox) {
				Text+=" - "+Lan.g(this,"SANDBOX (test mode, nothing is charged)");
			}
			_listPayTerminals=GlobalPaymentsV2.GetTerminals(_clinicNum);
			comboTerminal.Items.Clear();
			for(int i=0;i<_listPayTerminals.Count;i++) {
				string name=_listPayTerminals[i].Name;
				if(string.IsNullOrWhiteSpace(name)) {
					name=_listPayTerminals[i].TerminalID;
				}
				comboTerminal.Items.Add(name,_listPayTerminals[i]);
			}
			//Preselect the terminal this workstation used last (computerpref.CreditCardTerminalId, shared with PayConnect 2), else the first one.
			string terminalIdLast="";
			try {
				terminalIdLast=ComputerPrefs.GetCreditCardTerminalId();
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			int indexTerminal=PayTerminals.GetIndexToSelect(_listPayTerminals,terminalIdLast);
			if(indexTerminal>=0) {
				comboTerminal.SelectedIndex=indexTerminal;
			}
			bool hasSavedCard=GlobalPaymentsV2.IsGlobalPaymentsV2Card(_creditCardSaved);
			if(hasSavedCard) {
				radioSavedCard.Text=Lan.g(this,"Saved card ending")+" "+GpV2Json.GetLast4(_creditCardSaved.CCNumberMasked);
			}
			radioSavedCard.Enabled=hasSavedCard;
			radioTerminal.Enabled=_canUseNewCard && _listPayTerminals.Count>0;
			if(hasSavedCard) {
				radioSavedCard.Checked=true;
			}
			else {
				radioTerminal.Checked=true;
			}
			if(_transactionIdVoid!="") {
				//A payment that already holds a Global Payments V2 transaction can only be voided here; a new charge goes on a new payment.
				radioVoid.Checked=true;
				radioSale.Enabled=false;
				radioRefund.Enabled=false;
			}
			else {
				radioVoid.Enabled=false;
				if(!radioTerminal.Enabled && !radioSavedCard.Enabled) {
					if(!_canUseNewCard) {
						MsgBox.Show(this,"Cannot add a new credit card.");
					}
					else {
						MsgBox.Show(this,"Add a terminal in Setup > Program Links > Global Payments V2 first.");
					}
					DialogResult=DialogResult.Cancel;
					Close();
					return;
				}
			}
			textAmount.Text=_amount.ToString("F2",CultureInfo.CurrentCulture);
			textTransaction.Text=_transactionIdVoid;
			checkSaveCard.Checked=PrefC.GetBool(PrefName.StoreCCtokens);
			labelStatus.Text="";
			FillControls();
		}

		private GpV2TransType GetTransType() {
			if(radioVoid.Checked) {
				return GpV2TransType.Void;
			}
			if(radioRefund.Checked) {
				return GpV2TransType.Refund;
			}
			return GpV2TransType.Sale;
		}

		///<summary>Shows the controls that apply to the chosen type and card.</summary>
		private void FillControls() {
			GpV2TransType transType=GetTransType();
			bool isVoid=transType==GpV2TransType.Void;
			groupCard.Enabled=!isVoid && !_isRunning;
			comboTerminal.Enabled=radioTerminal.Checked && !isVoid && !_isRunning;
			labelTransaction.Visible=isVoid;
			textTransaction.Visible=isVoid;
			textAmount.ReadOnly=isVoid || _isRunning;
			if(isVoid) {
				textAmount.Text=_amountVoid.ToString("F2",CultureInfo.CurrentCulture);
			}
			//A card can be saved only from a terminal sale, when new cards are allowed and the user may edit cards.
			checkSaveCard.Visible=transType==GpV2TransType.Sale && radioTerminal.Checked && _canUseNewCard && _patient!=null;
			groupTransType.Enabled=!_isRunning;
			butRun.Enabled=!_isRunning;
			butCancel.Text=_isRunning ? Lan.g(this,"Stop waiting") : Lan.g(this,"&Cancel");
		}

		private void radioTransType_CheckedChanged(object sender,EventArgs e) {
			FillControls();
		}

		private void radioCard_CheckedChanged(object sender,EventArgs e) {
			FillControls();
		}

		private async void butRun_Click(object sender,EventArgs e) {
			if(_isRunning) {
				return;
			}
			GpV2TransType transType=GetTransType();
			decimal amount;
			if(transType==GpV2TransType.Void) {
				amount=_amountVoid;
			}
			else if(!decimal.TryParse(textAmount.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out amount) || amount<=0) {
				MsgBox.Show(this,"Enter an amount greater than zero.");
				return;
			}
			amount=Math.Round(amount,2,MidpointRounding.AwayFromZero);
			PayTerminal payTerminal=null;
			bool isTerminal=transType!=GpV2TransType.Void && radioTerminal.Checked;
			if(isTerminal) {
				payTerminal=comboTerminal.GetSelected<PayTerminal>();
				if(payTerminal==null) {
					MsgBox.Show(this,"Choose a terminal.");
					return;
				}
				if(!Security.IsAuthorized(EnumPermType.CreditCardTerminal)) {
					return;
				}
			}
			else if(transType!=GpV2TransType.Void && !GlobalPaymentsV2.IsGlobalPaymentsV2Card(_creditCardSaved)) {
				MsgBox.Show(this,"Choose a card.");
				return;
			}
			if(transType==GpV2TransType.Sale && _patient!=null && !ConfirmNotDuplicate(amount)) {
				return;
			}
			if(transType==GpV2TransType.Void
				&& !MsgBox.Show(this,MsgBoxButtons.YesNo,"Void this Global Payments V2 transaction? The whole amount goes back to the card."))
			{
				return;
			}
			if(isTerminal) {
				RememberTerminal(payTerminal.TerminalID);
			}
			_isRunning=true;
			_cancellationTokenSource=new CancellationTokenSource();
			FillControls();
			labelStatus.Text=Lan.g(this,"Contacting Global Payments...");
			GpV2Transactions gpV2Transactions=new GpV2Transactions(GlobalPaymentsV2.GetClient());
			CancellationToken cancellationToken=_cancellationTokenSource.Token;
			Action<string> actionStatus=x => ShowStatusFromPoolThread(x);
			GlobalPaymentsV2Result result;
			try {
				if(transType==GpV2TransType.Void) {
					result=await System.Threading.Tasks.Task.Run(() => gpV2Transactions.RunVoidAsync(_config,_transactionIdVoid,amount,cancellationToken));
				}
				else if(isTerminal) {
					result=await System.Threading.Tasks.Task.Run(() => gpV2Transactions.RunTerminalAsync(_config,payTerminal,transType,amount,actionStatus,cancellationToken));
				}
				else {
					string reference=GlobalPaymentsV2.NewReference(transType==GpV2TransType.Refund ? "RF" : "SL");
					result=await System.Threading.Tasks.Task.Run(() => gpV2Transactions.RunSavedCardAsync(_config,_creditCardSaved,transType,amount,reference,isRecurring:false,
						cancellationToken));
				}
			}
			catch(Exception ex) {
				//Not a Global Payments answer (those become results): something in this program. The request may have been sent.
				result=new GlobalPaymentsV2Result();
				result.TransType=transType;
				result.Amount=amount;
				result.IsSandbox=_config.UseSandbox;
				result.Outcome=GpV2Outcome.Unknown;
				result.Message=ex.Message;
				result.TerminalName=payTerminal?.Name??"";
				result.TerminalId=payTerminal?.TerminalID??"";
			}
			_isRunning=false;
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource=null;
			if(IsDisposed) {
				return;
			}
			FinishRun(result);
		}

		///<summary>False when the patient already has a Global Payments V2 payment of this amount today and the user doesn't want to charge again. Runs
		///before anything is sent, inside an async handler, so it never throws: when the check itself fails the user decides.</summary>
		private bool ConfirmNotDuplicate(decimal amount) {
			bool hasSamePayment;
			try {
				hasSamePayment=GlobalPaymentsV2.HasSamePaymentOnDate(_patient.PatNum,(double)amount,DateTime.Today);
			}
			catch(Exception ex) {
				return MessageBox.Show(this,Lan.g(this,"Could not check for an earlier Global Payments V2 payment of this amount today:")+" "+ex.Message+"\r\n"
					+Lan.g(this,"Charge anyway?"),GlobalPaymentsV2.DisplayName,MessageBoxButtons.YesNo)==DialogResult.Yes;
			}
			if(!hasSamePayment) {
				return true;
			}
			return MsgBox.Show(this,MsgBoxButtons.YesNo,"This patient already has a Global Payments V2 payment of this amount today. Charge again?");
		}

		private void ShowStatusFromPoolThread(string status) {
			try {
				if(IsDisposed || !IsHandleCreated) {
					return;
				}
				BeginInvoke((Action)(() => {
					if(_isRunning && !IsDisposed) {
						labelStatus.Text=status;
					}
				}));
			}
			catch(Exception ex) {
				ex.DoNothing();//The window closed meanwhile.
			}
		}

		///<summary>Remembers the terminal on this workstation's computerpref row. A failure to save it never stops the payment.</summary>
		private void RememberTerminal(string terminalId) {
			try {
				ComputerPrefs.UpdateCreditCardTerminalId(terminalId);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		private void FinishRun(GlobalPaymentsV2Result result) {
			long patNum=_patient?.PatNum??0;
			try {
				SecurityLogs.MakeLogEntry(EnumPermType.CreditCardTerminal,patNum,GlobalPaymentsV2.BuildLogText(result));
			}
			catch(Exception ex) {
				ex.DoNothing();//Never loses the result: an approved transaction still reaches the payment.
			}
			FillControls();
			if(result.IsApproved) {
				ResultOut=result;
				ReceiptOut=GlobalPaymentsV2.BuildReceipt(result,CreditCardUtils.AddClinicToReceipt(_clinicNum),_patient?.GetNameFLnoPref()??"",DateTime.Now);
				DoSaveCard=checkSaveCard.Visible && checkSaveCard.Checked;
				DialogResult=DialogResult.OK;
				Close();
				return;
			}
			string text=GlobalPaymentsV2.GetOutcomeText(result.Outcome);
			if(!string.IsNullOrWhiteSpace(result.Message)) {
				text+=": "+result.Message;
			}
			labelStatus.Text=text;
			if(result.IsInDoubt) {
				MessageBox.Show(this,text+"\r\n\r\n"+Lan.g(this,"Check the terminal and the Global Payments merchant portal before trying again, so the card isn't "
					+"charged twice.")+(result.DeviceCommandId=="" ? "" : "\r\n"+Lan.g(this,"Terminal command")+": "+result.DeviceCommandId),
					GlobalPaymentsV2.DisplayName,MessageBoxButtons.OK,MessageBoxIcon.Warning);
			}
		}

		private void butCancel_Click(object sender,EventArgs e) {
			if(_isRunning) {
				//Stops waiting; the run queries the terminal once more and then reports.
				_cancellationTokenSource?.Cancel();
				labelStatus.Text=Lan.g(this,"Stopping...");
				return;
			}
			DialogResult=DialogResult.Cancel;
			Close();
		}

		private void FormGlobalPaymentsV2_FormClosing(object sender,FormClosingEventArgs e) {
			if(!_isRunning) {
				return;
			}
			//Never close while a request is out: the result must be shown and logged.
			_cancellationTokenSource?.Cancel();
			labelStatus.Text=Lan.g(this,"Stopping...");
			e.Cancel=true;
		}
	}
}
