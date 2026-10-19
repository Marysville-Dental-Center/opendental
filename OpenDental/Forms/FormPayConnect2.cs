using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using OpenDental.Bridges;
using OpenDentBusiness;
using OpenDentBusiness.PayConnectService;
using static OpenDentBusiness.PayConnect2;
using PayConnectService=OpenDentBusiness.PayConnectService;

namespace OpenDental {
	public partial class FormPayConnect2:FormODBase {
		private Patient _patient;
		public string ReceiptStr;
		private CreditCard _creditCard;
		///<summary>For iFrame only.</summary>
		private bool _isAddingCard;
		private long _clinicNum;
		private decimal _amount;
		private Program _program;
		private PayConnectResponse _response;
		private PayConnect2Response _payConnect2Response=new PayConnect2Response();
		private bool iframeOpen=false;
		public PayConnectService.transType TransType=PayConnectService.transType.SALE;//Backwards compatability with PayConnect v1
		///<summary>Opening the PayConnect or PaySimple window from FormPayment, and then closing them, can set isCcDeclined to True.
		///This is because FormPayment didn't know if a transaction was attempted or not, and was assuming it was.
		///This can cause the payment amount to be reset to $0. So, this bool indicates if we have actually attempted a transaction.</summary>
		public bool WasPaymentAttempted=false;
		///<summary>The hosted card-entry session's order ID, kept so the result can still be found if the status call after SUCCESS fails.</summary>
		private string _embedOrderId="";
		///<summary>True once the hosted page has posted SUCCESS.</summary>
		private bool _didHostedFormSucceed;
		///<summary>True while every call goes to the vendor's sandbox. Shown in the title and on the receipt so test rows can't pass for real ones.</summary>
		private bool _isSandbox;

		public FormPayConnect2(long clinicNum,Patient patient,CreditCard creditCard,decimal amount,bool isAddingCard=false) {
			_clinicNum=clinicNum;
			_creditCard=creditCard;
			_patient=patient;
			_isAddingCard=isAddingCard;
			_amount=amount;
			_response=new PayConnectResponse();
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormPayConnect2_Load(object sender,EventArgs e) {
			_program=Programs.GetCur(ProgramName.PayConnect);
			if(_program==null) {
				MsgBox.Show(this,"PayConnect does not exist in the database.");
				DialogResult=DialogResult.Cancel;
				return;
			}
			comboTerminal.Items.Clear();
			List<PayTerminal> listPayTerminals = PayTerminals.Refresh(_clinicNum);
			for(int i = 0;i<listPayTerminals.Count;i++) {
				string name = listPayTerminals[i].Name;
				if(name.IsNullOrEmpty()) {
					name=listPayTerminals[i].TerminalID;
				}
				comboTerminal.Items.Add(name,listPayTerminals[i]);
				if(i==0) {
					comboTerminal.SelectedIndex=0;
				}
			}
			textAmount.Text=_amount.ToString();
			_isSandbox=PayConnect2.GetUseSandbox();
			if(_isSandbox) {
				Text+=" - "+Lan.g(this,"SANDBOX (test mode, nothing is charged)");
			}
			//Hide it here to override the enable/disable changes between terminal/webservice checked methods.
			checkSaveToken.Visible=!PIn.Bool(ProgramProperties.GetPropVal(_program.ProgramNum,PayConnect.ProgramProperties.PayConnectPreventSavingNewCC,_clinicNum));
			if(_patient==null) {//Prepaid card
				radioAuthorization.Enabled=false;
				radioVoid.Enabled=false;
				radioRefund.Enabled=false;
				checkSaveToken.Visible=false;
				sigBoxWrapper.Enabled=false;
			}
			else {//Other cards
				checkSaveToken.Checked=PrefC.GetBool(PrefName.StoreCCtokens);
			}
			if(!PIn.Bool(ProgramProperties.GetPropVal(_program.ProgramNum,"TerminalProcessingEnabled",_clinicNum))){
				groupProcessMethod.Visible=false;
				//it is hidden but is still "checked" so the process transaction method knows which service to use.
				radioWebService.Checked=true;
			}
			else {
				string procMethod=ProgramProperties.GetPropValForClinicOrDefault(_program.ProgramNum,
					PayConnect.ProgramProperties.DefaultProcessingMethod,_clinicNum);
				if(procMethod=="0") {
					radioWebService.Checked=true;
				}
				else if(procMethod=="1") {
					radioTerminal.Checked=true;
				}
			}
			if(radioWebService.Checked) {
				comboTerminal.Enabled=false;
				if(radioSale.Checked && _creditCard==null) {
					SetUpIframe(GetAmountFieldAsCents());
					label1.Visible=true;
					butRefresh.Enabled=true;
				}
				else {
					label1.Visible=false;
					butRefresh.Enabled=false;
					SetFormSizeToNormal();
				}
			}
			else if(radioTerminal.Checked) {
				comboTerminal.Enabled=true;
			}
		}

		/// <summary>Returns -1 if an error occured. Takes whatever is in textAmount and tries to prepare it for use with PayConnect.</summary>
		private int GetAmountFieldAsCents() {
			if(textAmount.Text.IsNullOrEmpty()){
				return 0;
			}
			double amount=textAmount.Value;
			try {
				return PayConnect2.FormatAmountForApi(amount);
			}
			catch(Exception ex) { 
				ex.DoNothing();
				MsgBox.Show(this,"Transaction amount is formatted incorrectly.");
				return -1;
			}
		}

		/// <summary>Returns true if we contacted and got a response from PayConnect.</summary>
		private bool ProcessTransaction() {
			WasPaymentAttempted=true;
			int amountInCents=GetAmountFieldAsCents();
			string magData="";
			if(radioTerminal.Checked) {
				if(comboTerminal.SelectedItem==null) {
					MsgBox.Show(this,"Please select a terminal from the dropdown.");
					return false;
				}
				string terminalId=((PayTerminal)comboTerminal.SelectedItem).TerminalID;
				decimal amount=(decimal)textAmount.Value;//textAmount will be a value between 0 and 100,000,000
				if(amount>0) {
					CreateTerminalTransactionRequest request=new CreateTerminalTransactionRequest();
					request.Amount=amountInCents;
					request.Terminal=terminalId;
					request.Frequency=TransactionFrequency.OneTime;
					request.Signature=true;//For now always request signature on the terminal, PayConnect will handle if the terminal does not support signatures.
					if(radioSale.Checked) {
						request.TransType=TransactionType.Sale;
						TransType=PayConnectService.transType.SALE;
					}
					else if(radioAuthorization.Checked) {
							request.TransType=TransactionType.AuthorizeOnly;
							TransType=PayConnectService.transType.AUTH;
					}
					//The library sends a correlation ID as invoiceNumber and, if the answer is lost, looks the transaction up by it.
					request.InvoiceNumber=PayConnect2.NewCorrelationId();
					UI.ProgressWin progressOD=new UI.ProgressWin();
					progressOD.ActionMain=()=> {_payConnect2Response=PayConnect2.PostCreateTerminalTransaction(request,_clinicNum);};
					progressOD.ShowCancelButton=false;//This is neccessary as cancelling on our side will not cancel the transaction on the terminal, which could lead to the payment being processed but not show in the patients account. This also matches what we do in FormPayConnect.cs.
					progressOD.StartingMessage=Lan.g(this,"Processing payment on terminal");
					progressOD.StopNotAllowedMessage=Lan.g(this,"Not allowed to stop. Please wait up to 2 minutes.");
					try{
						progressOD.ShowDialog();
					}
					catch(Exception ex){
						SecurityLogs.MakeLogEntry(EnumPermType.CreditCardTerminal,GetPatNumForLog(),"No response received. PayConnect invoice number: "+request.InvoiceNumber);
						MessageBox.Show(Lan.g(this,"A payment was initiated but no response was received. The payment may or may not have processed."
							+" Verify payment with your Credit Card merchant.")+" "+Lan.g(this,"Invoice number")+": "+request.InvoiceNumber,ex.Message);
						return false;
					}
					if(progressOD.IsCancelled){
						return false;
					}
					if(!CheckOutcome(_payConnect2Response,request.InvoiceNumber)) {
						return false;
					}
				}
				else {
						MsgBox.Show(this,"Amount must be greater than 0.00");
						return false;
				}
				//The New Terminal API endpoint only allows for Sale and Auths, keeping the below block of code around incase PayConnect adds functionality later.
				//else if(radioVoid.Checked) {
				//	if(textRefNumber.Text.IsNullOrEmpty()) {
				//		MsgBox.Show(this,"Reference Number is required to void a transaction.");
				//		return false;
				//	}
				//	_response=PayConnect2L.CreateVoid(textRefNumber.Text,apiKey,terminalId,forceDuplicate:checkForceDuplicate.Checked);
				//	TransType=PayConnectService.transType.VOID;
				//}
				//else if(radioRefund.Checked) {
				//	if(textRefNumber.Text.IsNullOrEmpty()) {
				//		MsgBox.Show(this,"Reference Number is required to refund a transaction.");
				//		return false;
				//	}
				//	if(amount>0) {
				//		_response=PayConnect2L.CreateRefund(amount,apiKey,terminalId,textRefNumber.Text,forceDuplicate:checkForceDuplicate.Checked);
				//		TransType=PayConnectService.transType.RETURN;
				//	}
				//	else {
				//		MsgBox.Show(this,"Amount must be greater than 0.00");
				//		return false;
				//	}
				//}
				//We only have the last 4 digits of the card to work with now, no need for MagStripCardParser
				if(_payConnect2Response.TerminalTransactionResponse?.PaymentMethod?.CardPaymentMethod!=null) {
					magData=_payConnect2Response.TerminalTransactionResponse.PaymentMethod.CardPaymentMethod.CardLast4Digits;
				}
			}
			else if(radioWebService.Checked) {
				//A hosted page that reported SUCCESS still goes through here when the status call after it failed, so the order ID lookup below can run.
				if(radioSale.Checked && (_payConnect2Response.ErrorResponse==null || _didHostedFormSucceed)) {
					if(amountInCents < 0) {
						MsgBox.Show(this,"Amount must be greater than 0.00");
						return false;
					}
					if(_creditCard==null || _creditCard.PayConnectToken.IsNullOrEmpty()) {
						GetStatusResponse getStatusResponse=_payConnect2Response.GetStatusResponse;
						if(getStatusResponse==null && _didHostedFormSucceed && !string.IsNullOrEmpty(_embedOrderId)) {
							//The hosted page reported SUCCESS but the status call after it failed: look the transaction up by the session's order ID.
							PayConnect2Response responseByOrderId=PayConnect2.GetTransactionStatus(_clinicNum,"",orderId:_embedOrderId);
							if(responseByOrderId.ResponseType==ResponseType.GetStatus && responseByOrderId.GetStatusResponse!=null) {
								_payConnect2Response=responseByOrderId;
								getStatusResponse=responseByOrderId.GetStatusResponse;
							}
						}
						if(getStatusResponse==null) {
							if(!_didHostedFormSucceed) {
								return false;
							}
							//The card was charged on the hosted page, but neither lookup could read the result. Never silent: staff must check the portal.
							WarnHostedSaleNotFound();
							_response=PayConnect2.ApiResponseToPayConnectResponse(_payConnect2Response);
							return true;
						}
						string pc2GatewayResponse=getStatusResponse.GatewayResponse?.ToString()??"";
						if(pc2GatewayResponse.ToLower().Contains("swipe")){//"entrymode" : "Swipe (non emv)" means card was swiped, not manually entered.
							magData="swiped";//Set magData to something other than blank so receipt shows "Swiped"
						}
					}
					//We have a saved card with a token
					else {
						try {
							_payConnect2Response=PayConnect2.PostCreateTransactionByToken(_patient,_creditCard,amountInCents,_clinicNum);
						}
						catch(Exception ex) {
							ex.DoNothing();
							MsgBox.Show(this,"Invalid data, unable to send sale transaction");
							return false;
						}
						if(!CheckOutcome(_payConnect2Response,_payConnect2Response.CorrelationId)) {
							return false;
						}
					}
					TransType=PayConnectService.transType.SALE;
				}
				else if(radioAuthorization.Checked) {
					if(_creditCard==null || _creditCard.PayConnectToken.IsNullOrEmpty()) { 
						MsgBox.Show(this,"A saved card is required to run an authorization transaction. Return to the previous window and select a saved card.");
						return false;
					}
					if(amountInCents<0) {
						MsgBox.Show(this,"Amount must be greater than 0.00");
						return false;
					}
					try {
						_payConnect2Response=PayConnect2.PostCreateTransactionByToken(_patient,_creditCard,amountInCents,_clinicNum,PayConnect2.TransactionType.AuthorizeOnly);
					}
					catch(Exception ex) {
							ex.DoNothing();
							MsgBox.Show(this,"Invalid data, unable to send auth transaction");
							return false;
					}
					if(!CheckOutcome(_payConnect2Response,_payConnect2Response.CorrelationId)) {
						return false;
					}
					TransType=PayConnectService.transType.AUTH;
				}
				else if(radioRefund.Checked) {
					if(textRefNumber.Text.IsNullOrEmpty()) { 
						MsgBox.Show(this,"Reference Number is required to refund a transaction.");
						return false;
					}
					RefundReferenceIDRequest refundReferenceIDRequest=new RefundReferenceIDRequest();
					refundReferenceIDRequest.ReferenceId=textRefNumber.Text;
					//Amount can be used to do partial refunds. Not including the amount field will refund the entire amount of the original transaction.
					if(amountInCents>0) {
						refundReferenceIDRequest.Amount=amountInCents;
					}
					_payConnect2Response=PayConnect2.PostRefundWithReferenceID(refundReferenceIDRequest,_clinicNum);
					if(!CheckOutcome(_payConnect2Response,"")) {
						return false;
					}
					TransType=PayConnectService.transType.RETURN;
				}
				else if(radioVoid.Checked) {
					if(textRefNumber.Text.IsNullOrEmpty()) { 
						MsgBox.Show(this,"Reference Number is required to void a transaction.");
						return false;
					}
					VoidReferenceIDRequest voidReferenceIDRequest=new VoidReferenceIDRequest();
					voidReferenceIDRequest.ReferenceId=textRefNumber.Text;
					_payConnect2Response=PayConnect2.PutVoidWithReferenceID(voidReferenceIDRequest,_clinicNum);
					if(!CheckOutcome(_payConnect2Response,"")) {
						return false;
					}
					TransType=PayConnectService.transType.VOID;
				}	
			}
			_response=PayConnect2.ApiResponseToPayConnectResponse(_payConnect2Response);
			if(_response.TransType==PayConnectResponse.TransactionType.Unknown) {
				return true;
			}
			bool doShowSignatureLine=true;
			if(TransType==transType.SALE || TransType==transType.AUTH || TransType==transType.RETURN) {
				PayConnect2Response signatureResponse=SendSignature();
				if(signatureResponse!=null && signatureResponse.SignatureResponse!=null && signatureResponse.SignatureResponse.Status=="Processed") {
					doShowSignatureLine=false;
				}
			}
			decimal surchargeAmount=_response.AmountSurcharged;
			ReceiptStr=PayConnect.BuildReceiptString(TransType,_response.RefNumber,_patient.GetNameFLnoPref(),_response.CardNumber,magData,_response.AuthCode,_response.Description,messages:null,_response.Amount,doShowSignatureLine,_clinicNum,_response.CardType,surchargeAmount:surchargeAmount,cardHolder:_response.CardHolder);
			if(_isSandbox) {
				ReceiptStr="SANDBOX TEST - NOT A REAL CHARGE"+Environment.NewLine+ReceiptStr;
			}
			return true;
		}

		///<summary>Handles a call that didn't get a normal answer. NotSent: says nothing was charged and returns false, so the user can try again.
		///SentNoResponse (the library already searched the transaction list for the correlation ID and didn't find it): writes the ID to the security
		///log, warns that the payment may or may not have gone through, and returns true so the payment window records the outcome. Anything else
		///returns true.</summary>
		private bool CheckOutcome(PayConnect2Response payConnect2Response,string correlationId) {
			if(payConnect2Response==null) {
				return true;
			}
			if(payConnect2Response.Outcome==PayConnectRequestOutcome.NotSent) {
				MsgBox.Show(payConnect2Response.ErrorResponse?.Message??Lan.g(this,"Could not reach PayConnect. No charge was made."));
				return false;
			}
			if(payConnect2Response.Outcome==PayConnectRequestOutcome.SentNoResponse) {
				string logText="PayConnect did not answer.";
				if(!string.IsNullOrEmpty(correlationId)) {
					logText+=" The transaction was not found in its transaction list. PayConnect invoice number: "+correlationId;
				}
				SecurityLogs.MakeLogEntry(EnumPermType.CreditCardTerminal,GetPatNumForLog(),logText);
				MsgBox.Show(payConnect2Response.ErrorResponse?.Message??Lan.g(this,"PayConnect did not answer. The payment may or may not have processed."));
			}
			return true;
		}

		///<summary>The hosted card-entry page reported SUCCESS, but the status call after it and the order ID lookup both failed, so no payment
		///details are known. Writes the order ID to the security log and warns that the card may have been charged, as CheckOutcome does for a lost
		///answer.</summary>
		private void WarnHostedSaleNotFound() {
			string logText="The PayConnect payment form reported success, but the transaction's status could not be read.";
			string message=Lan.g(this,"The payment form reported success, but PayConnect's record of the payment could not be read. The card may have been "
				+"charged. Verify the payment in the PayConnect portal before trying again.");
			if(!string.IsNullOrEmpty(_embedOrderId)) {
				logText+=" PayConnect order ID: "+_embedOrderId;
				message+=" "+Lan.g(this,"Order ID")+": "+_embedOrderId;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.CreditCardTerminal,GetPatNumForLog(),logText);
			MsgBox.Show(message);
		}

		private long GetPatNumForLog() {
			if(_patient==null) {
				return 0;
			}
			return _patient.PatNum;
		}

		/// <summary>Does validation before sending. Will not make an API call if a signature has not been entered.</summary>
		private PayConnect2Response SendSignature() {
			if(!sigBoxWrapper.GetSigChanged() || string.IsNullOrEmpty(sigBoxWrapper.GetSignature(""))) {
				return null;
			}
			AddSignatureReferenceIDRequest addSignatureRequest=new AddSignatureReferenceIDRequest();
			addSignatureRequest.ReferenceId=_response.RefNumber;
			using(Bitmap bitmapSigImage=sigBoxWrapper.GetSigImage())
			using(MemoryStream memoryStream=new MemoryStream()) {
				bitmapSigImage.Save(memoryStream,ImageFormat.Jpeg);
				byte[] byteArrayImageBytes=memoryStream.ToArray();
				addSignatureRequest.Signature=Convert.ToBase64String(byteArrayImageBytes);
			}
			return PayConnect2.PutSignatureWithReferenceID(addSignatureRequest,_clinicNum);
		}

		///<summary>Checks all UI state and preconditions needed to save a card.</summary>
		private void SaveCreditCard() {
			if(!checkSaveToken.Checked || !checkSaveToken.Enabled || !checkSaveToken.Visible) { 
				return;
			}
			if(_response.StatusCode!="0") {
				//error occured, let calling form handle it.
				return;
			}
			PayConnect2Response statusResponse=PayConnect2.GetTransactionStatus(_clinicNum,_response.RefNumber);
			if(statusResponse.GetStatusResponse?.PaymentMethod?.CardPaymentMethod==null) {
				return;
			}
			CardPaymentMethod cardPaymentMethod=statusResponse.GetStatusResponse.PaymentMethod.CardPaymentMethod;
			if(_creditCard==null) {//user selected Add new card from the payment window, save it or its token depending on settings
				_creditCard=new CreditCard();
				_creditCard.IsNew=true;
				_creditCard.PatNum=_patient.PatNum;
				List<CreditCard> listCreditCardsItemOrderCount=CreditCards.RefreshAll(_patient.PatNum);
				_creditCard.ItemOrder=listCreditCardsItemOrderCount.Count;
			}
			DateTime expiryDate;
			bool didParse=DateTime.TryParseExact(cardPaymentMethod.Expiry,new string[] {"MMyy","yyyyMM"},CultureInfo.CurrentCulture.DateTimeFormat,DateTimeStyles.None,out expiryDate);
			if(didParse) {
				_creditCard.CCExpiration=expiryDate;
			}
			_creditCard.CCNumberMasked=cardPaymentMethod.CardLast4Digits.PadLeft(16,'X');
			_creditCard.Zip=cardPaymentMethod.ZipCode;
			_creditCard.PayConnectToken="";
			_creditCard.PayConnectTokenExp=expiryDate;//Token expiration is the same as CC expiration.
			_creditCard.PayConnectToken=_response.PaymentToken;
			_creditCard.CCSource=CreditCardSource.PayConnect;
			if(_creditCard.IsNew) {
				_creditCard.ClinicNum=_clinicNum;
				_creditCard.Procedures=PrefC.GetString(PrefName.DefaultCCProcs);
				CreditCards.Insert(_creditCard);
				SecurityLogs.MakeLogEntry(EnumPermType.CreditCardEdit,_patient.PatNum,"Credit Card Added");
			}
			else {
				if(_creditCard.CCSource==CreditCardSource.XServer) {//This card has also been added for XCharge.
					_creditCard.CCSource=CreditCardSource.XServerPayConnect;
				}
				CreditCards.Update(_creditCard);
			}
		}

		///<summary>Only call this method once FormPayConnect2 has closed and the dialog result is OK. Uses the old PayConnectResponse class for backwards compatability.</summary>
		public PayConnectResponse GetPayConnectResponse() {
			return _response;
		}


		///<summary>Originally from FormPayConnect2IFrame_Load().</summary>
		public async void SetUpIframe(int amountInCents,bool update=false) {
			if(iframeOpen) {
				if(!update) {
					Size=new Size(LayoutManager.Scale(959),LayoutManager.Scale(516));
					return;
				}
				if(update) {
					webViewMain.Stop();
					Size=new Size(LayoutManager.Scale(959),LayoutManager.Scale(516));
				}
			}
			string url = "";
			Size=new Size(LayoutManager.Scale(959),LayoutManager.Scale(516));
			try {
				url=GetiFrameUrl(amountInCents);
			}
			catch(ODException ex) {
				FriendlyException.Show("Error loading window.",ex);
				return;
			}
			webViewMain.Visible=true;
			webBrowserMain.Visible=false;
			if(!update) {
				try {
					await webViewMain.Init();
					webViewMain.CoreWebView2.WebMessageReceived+=GetTransactionResult;
					webViewMain.CoreWebView2.NavigationStarting+=WebViewMain_NavigationStarting;
					//Relays only messages whose origin is a DentalXChange host, with that origin, so GetTransactionResult can check it again.
					await webViewMain.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PayConnect2.GetHostedFormRelayScript());
				}
				catch(Exception ex) {
					FriendlyException.Show("Error initializing window.",ex);
					return;
				}
			}
			webViewMain.CoreWebView2.Navigate(url);
			iframeOpen=true;
		}

		///<summary>Originally from FormPayConnect2IFrame_Load().</summary>
		private string GetiFrameUrl(int amountInCents) {
			EmbedSessionRequest embedSessionRequest=new EmbedSessionRequest();
			embedSessionRequest.Swiper=true;
			if(_isAddingCard) { 
				embedSessionRequest.Type=PayConnect2.IframeType.Tokenizer;
			}
			else {
				embedSessionRequest.Type=PayConnect2.IframeType.Payment;
				if(amountInCents>0) {
					//Amount is optional, setting it just prevents it from being changed in the PayConnect iFrame.
					embedSessionRequest.Amount=amountInCents;
				}
			}
			_payConnect2Response=PayConnect2.PostEmbedSession(embedSessionRequest,_clinicNum);
			_didHostedFormSucceed=false;
			if(_payConnect2Response.ResponseType==PayConnect2.ResponseType.EmbedSession) {
				string url=_payConnect2Response.EmbedSessionResponse.Url;
				if(!PayConnect2.IsAllowedHostedFormUrl(url)) {
					throw new ODException("PayConnect returned a payment form address that is not a DentalXChange address, so it was not opened.");
				}
				_embedOrderId=_payConnect2Response.EmbedSessionResponse.OrderId??"";
				return url;
			}
			string message=_payConnect2Response.ErrorResponse?.Message;
			if(string.IsNullOrEmpty(message)) {
				message="Error occurred retrieving payment form URL from PayConnect.";
			}
			throw new ODException(message);
		}

		///<summary>Keeps the card-entry window on DentalXChange: any top-level navigation elsewhere is cancelled.</summary>
		private void WebViewMain_NavigationStarting(object sender,CoreWebView2NavigationStartingEventArgs args) {
			if(!PayConnect2.IsAllowedHostedFormUrl(args.Uri)) {
				args.Cancel=true;
			}
		}

		///<summary>Handles a message relayed from the hosted card-entry page. Ignored unless both the top-level page and the message's origin are
		///DentalXChange hosts. Originally from FormPayConnect2IFrame_Load().</summary>
		private void GetTransactionResult(object sender,CoreWebView2WebMessageReceivedEventArgs args) {
			if(!PayConnect2.IsAllowedHostedFormUrl(args.Source)) {
				return;
			}
			HostedFormMessage hostedFormMessage=PayConnect2.ParseHostedFormMessage(args.WebMessageAsJson);
			if(hostedFormMessage==null || !hostedFormMessage.IsFromAllowedOrigin) {
				return;
			}
			iFrameResponse response=hostedFormMessage.Response;
			if(hostedFormMessage.Status=="SUCCESS") {
				_didHostedFormSucceed=true;
				//When adding a card the transaction status and reference ID fields will return null from PayConnect, therefore we cannot run GetStatus.
				if(_isAddingCard) {
					//PayConnect sometimes sends back data formatted like track 2 of a magstripe, e.g. ;1234123412341234=0305101193010877?.
					//If the account number in it is a card number rather than a token, it is never stored.
					if(PayConnect2.IsTrackTwoData(response.Response?.CardToken)) {
						MagstripCardParser magstripCardParser=new MagstripCardParser(response.Response.CardToken,EnumMagstripCardParseTrack.TrackTwo);
						if(PayConnect2.LooksLikeCardNumber(magstripCardParser.AccountNumber)) {
							SetHostedFormError(Lan.g(this,"The card reader returned the card number instead of a token, so the card was not saved. Key the card in instead."));
							MsgBox.Show(this,"The card reader returned the card number instead of a token, so the card was not saved. Key the card in instead.");
							return;
						}
						response.Response.CardToken=magstripCardParser.AccountNumber;
					}
					_payConnect2Response.iFrameResponse=response;
					_payConnect2Response.ResponseType=ResponseType.IFrame;
				}
				else {
					//Immediately call the GetStatus endpoint for easier processing. If that fails, try the session's order ID.
					_payConnect2Response=GetTransactionStatus(_clinicNum,response.Response?.ReferenceId);
					if(_payConnect2Response.ResponseType==ResponseType.Error && !string.IsNullOrEmpty(_embedOrderId)) {
						PayConnect2Response responseByOrderId=GetTransactionStatus(_clinicNum,"",orderId:_embedOrderId);
						if(responseByOrderId.ResponseType==ResponseType.GetStatus) {
							_payConnect2Response=responseByOrderId;
						}
					}
				}
				butRefresh.Enabled=false;
				return;
			}
			if(hostedFormMessage.Status=="ERROR") {
				SetHostedFormError(response.Response?.Message??Lan.g(this,"The payment form reported an error."));
				return;
			}
			if(hostedFormMessage.Status=="SESSION_EXPIRED") {
				SetHostedFormError(Lan.g(this,"The payment form expired."));
				MsgBox.Show(this,"The payment form expired. Click Refresh to load a new one.");
				butRefresh.Enabled=true;
			}
		}

		private void SetHostedFormError(string message) {
			_payConnect2Response.ResponseType=ResponseType.Error;
			_payConnect2Response.ErrorResponse=new ErrorResponse();
			_payConnect2Response.ErrorResponse.ErrorType=ErrorType.Response;
			_payConnect2Response.ErrorResponse.Error=message;
			_payConnect2Response.ErrorResponse.Message=message;
		}

		///<summary>Size of this form is the original size. If any size changes occur this should be changed as well.</summary>
		private void SetFormSizeToNormal() {
			Size=new Size(LayoutManager.Scale(430),LayoutManager.Scale(516));
		}

		private void radioTerminal_CheckedChanged(object sender,EventArgs e) {
			if(radioTerminal.Checked) {
				if(radioRefund.Checked || radioVoid.Checked) {
					MsgBox.Show(this,"Not allowed to send refunds or voids via Terminal. Please select Web Service to process a refund or void.");
					radioWebService.Checked=true;
					return;
				}
				comboTerminal.Enabled=true;
				radioVoid.Enabled=false;
				radioRefund.Enabled=false;
				label1.Visible=false;
				butRefresh.Enabled=false;
			}
			SetFormSizeToNormal();
		}

		private void radioWebService_CheckedChanged(object sender,EventArgs e) {
			if(radioWebService.Checked) {
				checkSaveToken.Enabled=true;
				comboTerminal.Enabled=false;
				radioVoid.Enabled=true;
				radioRefund.Enabled=true;
				if(_creditCard==null && radioSale.Checked==true) {
					SetUpIframe(GetAmountFieldAsCents());
					butRefresh.Enabled=true;
					label1.Visible=true;
					butRefresh.Enabled=true;
				}
			}
		}

		///<summary>Not able to save a card during a void or refund</summary>
		private void transactionTypeCheckedChanged(object sender,EventArgs e) {
			if(radioSale.Checked && radioWebService.Checked && _creditCard==null) {
				label1.Visible=true;
				butRefresh.Enabled=true;
				SetUpIframe(GetAmountFieldAsCents());
				return;
			}
			else {
				label1.Visible=false;
				butRefresh.Enabled=false;
			}
			SetFormSizeToNormal();
			if(radioVoid.Checked || radioRefund.Checked) {
				checkSaveToken.Visible=false;
				return;
			}
			checkSaveToken.Visible=true;
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textAmount.IsValid()) {
				MsgBox.Show(this,"Please enter a valid amount or clear the amount field.");
				return;
			}
			if(!ProcessTransaction()) {
				return;
			}
			if(TransType==transType.SALE || TransType==transType.AUTH) {
				try {
					SaveCreditCard();
				}
				catch(Exception ex) {
					FriendlyException.Show("Unable to save card to database.",ex);
				}
			}
			if(!ReceiptStr.IsNullOrEmpty()) {
				PayConnectL.PrintReceipt(ReceiptStr,_patient);
			}
			DialogResult=DialogResult.OK;
		}


		private void butRefresh_Click(object sender,EventArgs e) {
			if(radioSale.Checked && radioWebService.Checked && _creditCard==null) {
				SetUpIframe(GetAmountFieldAsCents(),update:true);
			}
		}
	}
}