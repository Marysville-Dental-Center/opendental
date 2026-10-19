using System;
using System.Threading;
using System.Windows.Forms;
using CodeBase.VendorDirect;
using OpenDentBusiness;
using OpenDentBusiness.Qbo;

namespace OpenDental {
	///<summary>Receives the answer from Intuit's consent page. With a loopback or relay redirect it waits for the browser and closes by itself; the
	///paste box is always there as a fallback. With a production HTTPS redirect the user pastes the address the browser ended on. The old separate
	///code and realm ID boxes still work, but then the state can't be checked.</summary>
	public partial class FormQuickBooksOnlineAuthorization:FormODBase {
		///<summary>What came back. Set when DialogResult is OK.</summary>
		public QboAuthorizationResult AuthorizationResult;
		///<summary>Already started by the caller, or null for the paste flow. Disposed when this window closes.</summary>
		private readonly LoopbackListener _loopbackListener;
		private readonly CancellationTokenSource _cancellationTokenSource=new CancellationTokenSource();

		public FormQuickBooksOnlineAuthorization(LoopbackListener loopbackListener) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_loopbackListener=loopbackListener;
		}

		private void FormQuickBooksOnlineAuthorization_Load(object sender,EventArgs e) {
			if(_loopbackListener==null) {
				labelWaiting.Text=Lan.g(this,"After approving, copy the full address from your browser's address bar and paste it here.");
				return;
			}
			labelWaiting.Text=Lan.g(this,"Waiting for the browser to finish. This window closes by itself. "
				+"If it doesn't, copy the full address from your browser's address bar and paste it here.");
			WaitForBrowser();
		}

		private async void WaitForBrowser() {
			try {
				OAuthRedirectResult oAuthRedirectResult=await _loopbackListener.WaitForRedirectAsync(null,_cancellationTokenSource.Token);
				if(IsDisposed) {
					return;
				}
				AuthorizationResult=QboAuthorizationResult.FromRedirect(oAuthRedirectResult);
				DialogResult=DialogResult.OK;
			}
			catch(OperationCanceledException) {
				//The window closed.
			}
			catch(TimeoutException) {
				if(!IsDisposed) {
					labelWaiting.Text=Lan.g(this,"Stopped waiting for the browser. Paste the address from your browser's address bar, or close this window "
						+"and click Connect again.");
				}
			}
			catch(Exception ex) {
				if(!IsDisposed) {
					labelWaiting.Text=Lan.g(this,"Could not receive the answer from the browser")+" ("+ex.GetType().Name+"). "
						+Lan.g(this,"Paste the address from your browser's address bar instead.");
				}
			}
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(textAddress.Text.Trim()!="") {
				QboAuthorizationResult result=QboRedirectParser.Parse(textAddress.Text);
				if(result==null) {
					MsgBox.Show(this,"The address could not be read. Copy the whole address from the browser's address bar, starting with https://.");
					return;
				}
				AuthorizationResult=result;
				DialogResult=DialogResult.OK;
				return;
			}
			if(textAuthCode.Text.Trim()!="" || textRealmId.Text.Trim()!="") {
				if(textAuthCode.Text.Trim()=="" || textRealmId.Text.Trim()=="") {
					MsgBox.Show(this,"Enter both the Authorization Code and the Realm ID, or paste the full address instead.");
					return;
				}
				AuthorizationResult=QboAuthorizationResult.FromSeparateValues(textAuthCode.Text,textRealmId.Text);
				DialogResult=DialogResult.OK;
				return;
			}
			MsgBox.Show(this,"Paste the address from your browser's address bar.");
		}

		private void FormQuickBooksOnlineAuthorization_FormClosing(object sender,FormClosingEventArgs e) {
			_cancellationTokenSource.Cancel();
			_loopbackListener?.Dispose();
		}
	}
}
