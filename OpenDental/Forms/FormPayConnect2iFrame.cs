using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using OpenDentBusiness;
using static OpenDentBusiness.PayConnect2;

namespace OpenDental {
	///<summary>Use this form when there is no token on file for the credit card being used.</summary>
	public partial class FormPayConnect2iFrame:FormODBase {
		/// <summary>Controls if we request a tokenizer or payment type iFrame from PayConnect</summary>
		private bool _isAddingCard;
		private PayConnect2Response _response=new PayConnect2Response();
		private long _clinicNum;
		private int _amountInCents;
		///<summary>The hosted card-entry session's order ID.</summary>
		private string _embedOrderId="";

		public FormPayConnect2iFrame(long clinicNum,int amountInCents=0,bool isAddingCard=false) {
			_isAddingCard=isAddingCard;
			_clinicNum=clinicNum;
			_amountInCents=amountInCents;
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private async void FormPayConnect2iFrame_Load(object sender,EventArgs e) {
			if(PayConnect2.GetUseSandbox()) {
				Text+=" - "+Lan.g(this,"SANDBOX (test mode, nothing is charged)");
			}
			string url="";
			try {
				url=GetiFrameUrl();
			}
			catch(ODException ex) {
				FriendlyException.Show("Error loading window.",ex);
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			webViewMain.Visible=true;
			webBrowserMain.Visible=false;
			try {
				await webViewMain.Init();
				webViewMain.CoreWebView2.WebMessageReceived+=GetTransactionResult;
				webViewMain.CoreWebView2.NavigationStarting+=WebViewMain_NavigationStarting;
				//Relays only messages whose origin is a DentalXChange host, with that origin, so GetTransactionResult can check it again.
				await webViewMain.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PayConnect2.GetHostedFormRelayScript());
			}
			catch(Exception ex){
				FriendlyException.Show("Error initializing window.",ex);
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			webViewMain.CoreWebView2.Navigate(url);
			
		}

		///<summary>Throws exceptions.</summary>
		private string GetiFrameUrl() {
			EmbedSessionRequest embedSessionRequest=new EmbedSessionRequest();
			embedSessionRequest.Swiper=true;
			if(_isAddingCard) { 
				embedSessionRequest.Type=PayConnect2.IframeType.Tokenizer;
			}
			else {
				embedSessionRequest.Type=PayConnect2.IframeType.Payment;
				if(_amountInCents>0) {
					//Amount is optional, setting it just prevents it from being changed in the PayConnect iFrame.
					embedSessionRequest.Amount=_amountInCents;
				}
			}
			PayConnect2Response payConnect2Response=PayConnect2.PostEmbedSession(embedSessionRequest,_clinicNum);
			if(payConnect2Response.ResponseType==PayConnect2.ResponseType.EmbedSession) {
				string url=payConnect2Response.EmbedSessionResponse.Url;
				if(!PayConnect2.IsAllowedHostedFormUrl(url)) {
					throw new ODException("PayConnect returned a payment form address that is not a DentalXChange address, so it was not opened.");
				}
				_embedOrderId=payConnect2Response.EmbedSessionResponse.OrderId??"";
				return url;
			}
			string message=payConnect2Response.ErrorResponse?.Message;
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

		///<summary>Gets a new session and loads it, after the old one expired.</summary>
		private void ReloadSession() {
			string url;
			try {
				url=GetiFrameUrl();
			}
			catch(ODException ex) {
				FriendlyException.Show("Error loading window.",ex);
				return;
			}
			_response=new PayConnect2Response();
			webViewMain.CoreWebView2.Navigate(url);
		}

		///<summary>Handles a message relayed from the hosted card-entry page. Ignored unless both the top-level page and the message's origin are
		///DentalXChange hosts.</summary>
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
				//When adding a card the transaction status and reference ID fields will return null from PayConnect, therefore we cannot run GetStatus.
				if(_isAddingCard) {
					//PayConnect sometimes sends back data formatted like track 2 of a magstripe, e.g. ;1234123412341234=0305101193010877?.
					//If the account number in it is a card number rather than a token, it is never stored.
					if(PayConnect2.IsTrackTwoData(response.Response?.CardToken)) {
						MagstripCardParser magstripCardParser=new MagstripCardParser(response.Response.CardToken,EnumMagstripCardParseTrack.TrackTwo);
						if(PayConnect2.LooksLikeCardNumber(magstripCardParser.AccountNumber)) {
							PayConnect2.LogHostedFormError(_clinicNum,0,_isAddingCard,"The card reader returned the card number instead of a token; it was not saved.");
							_response=new PayConnect2Response();
							MsgBox.Show(this,"The card reader returned the card number instead of a token, so the card was not saved. Key the card in instead.");
							return;
						}
						response.Response.CardToken=magstripCardParser.AccountNumber;
					}
					_response.iFrameResponse=response;
					_response.ResponseType=ResponseType.IFrame;
				}
				else {
					//Immediately call the GetStatus endpoint for easier processing. If that fails, try the session's order ID.
					_response=GetTransactionStatus(_clinicNum,response.Response?.ReferenceId);
					if(_response.ResponseType==ResponseType.Error && !string.IsNullOrEmpty(_embedOrderId)) {
						PayConnect2Response responseByOrderId=GetTransactionStatus(_clinicNum,"",orderId:_embedOrderId);
						if(responseByOrderId.ResponseType==ResponseType.GetStatus) {
							_response=responseByOrderId;
						}
					}
				}
				return;
			}
			if(hostedFormMessage.Status=="ERROR") {
				string message=response.Response?.Message;
				if(string.IsNullOrEmpty(message)) {
					message=Lan.g(this,"The payment form reported an error.");
				}
				PayConnect2.LogHostedFormError(_clinicNum,0,_isAddingCard,message);
				_response=new PayConnect2Response();
				_response.ResponseType=ResponseType.Error;
				_response.ErrorResponse=new ErrorResponse();
				_response.ErrorResponse.ErrorType=ErrorType.Response;
				_response.ErrorResponse.Error=message;
				_response.ErrorResponse.Message=message;
				return;
			}
			if(hostedFormMessage.Status=="SESSION_EXPIRED") {
				if(MsgBox.Show(this,MsgBoxButtons.YesNo,"The payment form expired. Load a new one?")) {
					ReloadSession();
				}
			}
		}

		public PayConnect2Response GetResponse() {
			return _response;
		}

		private void GetTransactionResultCloud(object sender, EventArgs e) {
			string result="";
		}

		private void webBrowserMain_DocumentCompleted(object sender,WebBrowserDocumentCompletedEventArgs e) {
			webBrowserMain.Document.AttachEventHandler("message",GetTransactionResultCloud);
		}

	}
}