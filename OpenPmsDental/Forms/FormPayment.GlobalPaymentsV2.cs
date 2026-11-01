// Global Payments V2 (Open Dental 25.4, package V-GP): the payment window's Global Payments V2 button. A partial of FormPayment, called through three
// one-line hooks in FormPayment.cs (CheckUIState, DisablePaymentControls, FormPayment_FormClosing).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using CodeBase;
using CodeBase.VendorDirect;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.GlobalPayments;

namespace OpenPmsDental {
	public partial class FormPayment {
		///<summary>Created the first time it is needed, in the place of the Global Payments V1 (EdgeExpress) button: only one of X-Charge, V1 and V2 can be
		///enabled, so they never show together.</summary>
		private OpenPmsDental.UI.Button _butGlobalPaymentsV2;

		///<summary>Hook in CheckUIState: shows the button only when Global Payments V2 is enabled and set up for the payment's clinic, and the payment has a
		///payment type (ground rule 7: nothing shows before the office enables it).</summary>
		private void FillGlobalPaymentsV2Button() {
			bool isVisible=!checkPayTypeNone.Checked && GlobalPaymentsV2.ShowButton(_payment.ClinicNum,_listDefsPaymentType);
			if(!isVisible) {
				if(_butGlobalPaymentsV2!=null) {
					_butGlobalPaymentsV2.Visible=false;
				}
				return;
			}
			if(_butGlobalPaymentsV2==null) {
				_butGlobalPaymentsV2=new OpenPmsDental.UI.Button();
				_butGlobalPaymentsV2.Name="butGlobalPaymentsV2";
				//Location and size are already scaled: they are the V1 button's.
				_butGlobalPaymentsV2.Location=panelEdgeExpress.Location;
				_butGlobalPaymentsV2.Size=panelEdgeExpress.Size;
				_butGlobalPaymentsV2.Text=Lan.g(this,"Global Payments V2");
				_butGlobalPaymentsV2.Click+=butGlobalPaymentsV2_Click;
				LayoutManager.Add(_butGlobalPaymentsV2,this);
			}
			_butGlobalPaymentsV2.Visible=true;
			_butGlobalPaymentsV2.BringToFront();
		}

		///<summary>Hook in DisablePaymentControls.</summary>
		private void DisableGlobalPaymentsV2Button() {
			if(_butGlobalPaymentsV2!=null) {
				_butGlobalPaymentsV2.Enabled=false;
			}
		}

		private void butGlobalPaymentsV2_Click(object sender,EventArgs e) {
			if(!GlobalPaymentsV2.IsConfigured(_payment.ClinicNum)) {
				MsgBox.Show(this,"Global Payments V2 is not set up for this clinic. Enter the App ID and App Key in Setup > Program Links > Global Payments V2.");
				return;
			}
			GlobalPaymentsV2Config config=GlobalPaymentsV2.GetConfig(_payment.ClinicNum);
			CreditCard creditCardSelected=comboCreditCards.GetSelected<CreditCard>();
			CreditCard creditCardV2=GlobalPaymentsV2.IsGlobalPaymentsV2Card(creditCardSelected) ? creditCardSelected : null;
			//An existing Global Payments V2 payment can be voided from here.
			string transactionIdVoid="";
			if(!IsNew && _payment.PaymentSource==CreditCardSource.MeetInTheCloudTerminal && _payment.PayAmt>0) {
				transactionIdVoid=string.IsNullOrWhiteSpace(_payment.ExternalId) ? GlobalPaymentsV2.GetTransactionIdFromNote(_payment.PayNote) : _payment.ExternalId;
			}
			//"Prevent saving new cards": no transactions on new cards; saved cards and voids still work.
			bool canUseNewCard=!config.PreventSavingNewCards;
			if(creditCardV2==null && !canUseNewCard && transactionIdVoid=="") {
				MsgBox.Show(this,"Cannot add a new credit card.");
				return;
			}
			if(transactionIdVoid=="" && !ShowOverridePrompt()) {
				return;
			}
			if(transactionIdVoid=="" && _listPaySplits.Count>0 && PIn.Double(textAmount.Text)!=PIn.Double(textSplitTotal.Text)) {
				MsgBox.Show(this,"Split totals must equal payment amount before running a credit card transaction.");
				return;
			}
			decimal amount=Math.Abs(PIn.Decimal(textAmount.Text));
			using FormGlobalPaymentsV2 formGlobalPaymentsV2=new FormGlobalPaymentsV2(_patient,_payment.ClinicNum,amount,creditCardV2,canUseNewCard,
				transactionIdVoid,(decimal)_payment.PayAmt);
			if(formGlobalPaymentsV2.ShowDialog()!=DialogResult.OK || formGlobalPaymentsV2.ResultOut==null || !formGlobalPaymentsV2.ResultOut.IsApproved) {
				return;
			}
			ApplyGlobalPaymentsV2Result(formGlobalPaymentsV2.ResultOut,formGlobalPaymentsV2.ReceiptOut,formGlobalPaymentsV2.DoSaveCard,config);
		}

		///<summary>Puts an approved transaction on this payment: amount (negative for a refund), note, receipt, source, the transaction ID (ExternalId, used
		///to void it). A void instead adds the negative payment and closes the window. Cancelling the window after a sale or refund voids it
		///(VoidGlobalPaymentsV2OnClosing), as for the other card processors.</summary>
		private void ApplyGlobalPaymentsV2Result(GlobalPaymentsV2Result result,string receipt,bool doSaveCard,GlobalPaymentsV2Config config) {
			string note=GlobalPaymentsV2.BuildPayNote(result);
			if(result.TransType==GpV2TransType.Void) {
				if(_listPaySplits.Count==0) {
					AddOneSplit();
					Reinitialize();
				}
				_payment.IsSplit=_listPaySplits.Count>1;
				Payment paymentVoid=Payments.InsertVoidPayment(_payment,_listPaySplits,receipt,note,CreditCardSource.MeetInTheCloudTerminal);
				SecurityLogs.MakeLogEntry(EnumPermType.PaymentCreate,paymentVoid.PatNum,Patients.GetLim(paymentVoid.PatNum).GetNameLF()+", "
					+paymentVoid.PayAmt.ToString("c")+", "+GlobalPaymentsV2.DisplayName+" "+Lan.g(this,"void"));
				if(config.PrintReceipt && receipt!="") {
					PrintReceipt(receipt,Lan.g(this,"Global Payments V2 receipt printed"));
				}
				string strErrorMsg=Ledgers.ComputeAgingForPaysplitsAllocatedToDiffPats(_patient.PatNum,_listPaySplits);
				if(!string.IsNullOrEmpty(strErrorMsg)) {
					MessageBox.Show(strErrorMsg);
				}
				MsgBox.Show(this,"Void successful.");
				DialogResult=DialogResult.OK;//Close now, so the user can't cancel this window and void again.
				return;
			}
			decimal amountSigned=(result.TransType==GpV2TransType.Refund) ? -result.Amount : result.Amount;
			textAmount.Text=amountSigned.ToString("F2");
			textNote.Text+=((textNote.Text=="") ? "" : Environment.NewLine)+note;
			textNote.SelectionStart=textNote.TextLength;
			textNote.ScrollToCaret();
			_payment.PayNote=textNote.Text;
			_payment.PaymentSource=CreditCardSource.MeetInTheCloudTerminal;
			_payment.ExternalId=result.TransactionId;
			_payment.Receipt=receipt;
			_payment.IsCcCompleted=true;
			_payment.ProcessStatus=ProcessStat.OfficeProcessed;
			_wasCreditCardSuccessful=true;//Voided if the user cancels this window.
			_isCCDeclined=false;
			if(config.PaymentTypeDefNum!=0 && !PrefC.GetBool(PrefName.PaymentsPromptForPayType)) {
				int indexPayType=Defs.GetOrder(DefCat.PaymentTypes,config.PaymentTypeDefNum);
				if(indexPayType>=0) {
					listPayType.SelectedIndex=indexPayType;
				}
				SetComboDepositAccounts();
			}
			if(!string.IsNullOrEmpty(receipt)) {
				butPrintReceipt.Visible=true;
				if(PrefC.GetBool(PrefName.AllowEmailCCReceipt)) {
					butEmailReceipt.Visible=true;
				}
				if(config.PrintReceipt) {
					PrintReceipt(receipt,Lan.g(this,"Global Payments V2 receipt printed"));
				}
			}
			if(doSaveCard && result.TransType==GpV2TransType.Sale) {
				SaveGlobalPaymentsV2Card(result);
			}
			DisablePaymentControls();
		}

		///<summary>Saves the card of an approved sale when Global Payments returned a reusable card reference and the user may edit cards.</summary>
		private void SaveGlobalPaymentsV2Card(GlobalPaymentsV2Result result) {
			if(string.IsNullOrWhiteSpace(result.PaymentMethodId)) {
				MsgBox.Show(this,"Global Payments didn't return a card reference, so the card couldn't be saved.");
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.CreditCardEdit)) {
				return;
			}
			CreditCard creditCard=GlobalPaymentsV2.SaveCard(_patient.PatNum,_payment.ClinicNum,result);
			if(creditCard==null) {
				return;
			}
			List<CreditCard> listCreditCards=CreditCards.Refresh(_patient.PatNum);
			AddCreditCardsToCombo(listCreditCards,x => x.CreditCardNum==creditCard.CreditCardNum);
		}

		///<summary>Hook in FormPayment_FormClosing, after the payment was saved, when the user cancels a payment that holds a completed card transaction:
		///voids it if it is a Global Payments V2 transaction and adds the void payment. Returns true if it handled the payment (whatever the outcome), false
		///if the payment isn't a Global Payments V2 one.</summary>
		private bool VoidGlobalPaymentsV2OnClosing() {
			if(_payment.PaymentSource!=CreditCardSource.MeetInTheCloudTerminal) {
				return false;
			}
			string transactionId=string.IsNullOrWhiteSpace(_payment.ExternalId) ? GlobalPaymentsV2.GetTransactionIdFromNote(_payment.PayNote) : _payment.ExternalId;
			if(transactionId=="") {
				return false;
			}
			GlobalPaymentsV2Config config=GlobalPaymentsV2.GetConfig(_payment.ClinicNum);
			GlobalPaymentsV2Result result=null;
			UI.ProgressWin progressWin=new UI.ProgressWin();
			progressWin.StartingMessage=Lan.g(this,"Voiding the Global Payments V2 transaction...");
			progressWin.ShowCancelButton=false;
			progressWin.ActionMain=() => {
				GpV2Transactions gpV2Transactions=new GpV2Transactions(GlobalPaymentsV2.GetClient());
				result=VendorHttp.RunSync(() => gpV2Transactions.RunVoidAsync(config,transactionId,(decimal)Math.Abs(_payment.PayAmt),CancellationToken.None));
			};
			try {
				progressWin.ShowDialog();
			}
			catch(Exception ex) {
				result=null;
				ex.DoNothing();
			}
			if(result==null) {
				result=new GlobalPaymentsV2Result();
				result.TransType=GpV2TransType.Void;
				result.TransactionId=transactionId;
				result.Amount=(decimal)Math.Abs(_payment.PayAmt);
				result.Outcome=GpV2Outcome.Unknown;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.CreditCardTerminal,_payment.PatNum,GlobalPaymentsV2.BuildLogText(result));
			if(!result.IsApproved) {
				MessageBox.Show(Lan.g(this,"The Global Payments V2 transaction could not be voided, so the payment was kept.")+" "+result.Message+"\r\n"
					+Lan.g(this,"Void or refund it from this payment, or in the Global Payments merchant portal."));
				return true;
			}
			string receipt=GlobalPaymentsV2.BuildReceipt(result,CreditCardUtils.AddClinicToReceipt(_payment.ClinicNum),_patient.GetNameFLnoPref(),DateTime.Now);
			Payment paymentVoid=Payments.InsertVoidPayment(_payment,_listPaySplits,receipt,GlobalPaymentsV2.BuildPayNote(result),
				CreditCardSource.MeetInTheCloudTerminal);
			SecurityLogs.MakeLogEntry(EnumPermType.PaymentCreate,paymentVoid.PatNum,Patients.GetLim(paymentVoid.PatNum).GetNameLF()+", "
				+paymentVoid.PayAmt.ToString("c"));
			if(config.PrintReceipt) {
				PrintReceipt(receipt,Lan.g(this,"Global Payments V2 receipt printed"));
			}
			return true;
		}
	}
}
