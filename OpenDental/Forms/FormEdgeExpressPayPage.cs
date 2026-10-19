// Vendor-direct integrations: the EdgeExpress hosted payment page window (docs/specs/vendor-direct/edgeexpress.md 3.3 and 3.4).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;
using CodeBase.Controls;
using CodeBase.VendorDirect;
using Microsoft.Web.WebView2.Core;
using OpenDentBusiness;
using Task=System.Threading.Tasks.Task;

namespace OpenDental {
	///<summary>Shows the card processor's hosted pay page so the card number is typed into the vendor's page and never touches this program. It is not
	///a general browser: InPrivate (no cookies, history or form data kept), autofill and password saving off, no dev tools, no new windows or
	///downloads, and only the pay page's own hosts may load at the top level. It closes as soon as the page navigates to the return URL, cancelling
	///that navigation before any request goes out; nothing the return carries is read. It also closes when the user presses Close, when the session
	///lifetime runs out, or when the loopback listener (Loopback mode) catches the return. Either way, the caller then asks the card processor for
	///the result. In system-browser mode (no WebView2 runtime) the page opens in the default browser and this window only waits.</summary>
	public partial class FormEdgeExpressPayPage:FormODBase {
		private readonly string _payPageUrl;
		private readonly string _returnUrl;
		private readonly bool _isTestGateway;
		private readonly TimeSpan _timeSpanLifetime;
		private readonly bool _isSystemBrowser;
		private readonly Task _taskLoopbackReturn;
		private bool _isEnding;

		///<summary>How the window ended. Read it after ShowDialog.</summary>
		public EdgeExpressPayPageEnd PayPageEnd { get; private set; }=EdgeExpressPayPageEnd.Closed;

		///<summary>Required by the designer. Use the other constructor.</summary>
		public FormEdgeExpressPayPage() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		///<summary>payPageUrl is PAYPAGEURL from the session. returnUrl is the address the session was created with (the clinic's, the built-in one,
		///or the loopback listener's). taskLoopbackReturn, when given, completes successfully when the loopback listener receives the return.
		///isSystemBrowser opens the page in the default browser and shows only a wait message with "I'm finished" and Close.</summary>
		public FormEdgeExpressPayPage(string payPageUrl,string returnUrl,bool isTestGateway,TimeSpan timeSpanLifetime,Task taskLoopbackReturn=null,
			bool isSystemBrowser=false) : this()
		{
			_payPageUrl=payPageUrl;
			_returnUrl=returnUrl;
			_isTestGateway=isTestGateway;
			_timeSpanLifetime=timeSpanLifetime;
			_taskLoopbackReturn=taskLoopbackReturn;
			_isSystemBrowser=isSystemBrowser;
		}

		private async void FormEdgeExpressPayPage_Load(object sender,EventArgs e) {
			Text=Lan.g(this,"Card Entry");
			if(_isTestGateway) {
				Text+=" - "+Lan.g(this,"TEST GATEWAY, no real charges");
			}
			WatchLoopbackReturn();
			StartLifetimeTimer();
			if(_isSystemBrowser) {
				webViewMain.Visible=false;
				labelBrowserWait.Visible=true;
				butFinished.Visible=true;
				try {
					SystemBrowser.Open(_payPageUrl);
				}
				catch(Exception ex) {
					FriendlyException.Show(Lan.g(this,"Could not open the card entry page in the browser."),ex);
					EndWith(EdgeExpressPayPageEnd.Failed);
				}
				return;
			}
			try {
				//InPrivate: nothing persists in the shared WebView2 cache folder after the window closes.
				await webViewMain.Init(x => x.IsInPrivateModeEnabled=true);
			}
			catch(Exception ex) {
				ex.DoNothing();
				EndWith(EdgeExpressPayPageEnd.Failed);
				return;
			}
			if(_isEnding || IsDisposed) {
				return;
			}
			CoreWebView2 coreWebView2=webViewMain.CoreWebView2;
			coreWebView2.Settings.IsGeneralAutofillEnabled=false;//Never offer a keyed card number to the next user.
			coreWebView2.Settings.IsPasswordAutosaveEnabled=false;
			coreWebView2.Settings.AreDevToolsEnabled=false;
			coreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=false;
			coreWebView2.Settings.AreDefaultContextMenusEnabled=false;
			coreWebView2.Settings.IsStatusBarEnabled=false;
			coreWebView2.NavigationStarting+=CoreWebView2_NavigationStarting;
			coreWebView2.FrameNavigationStarting+=CoreWebView2_FrameNavigationStarting;
			coreWebView2.NewWindowRequested+=CoreWebView2_NewWindowRequested;
			coreWebView2.DownloadStarting+=CoreWebView2_DownloadStarting;
			coreWebView2.PermissionRequested+=CoreWebView2_PermissionRequested;
			coreWebView2.Navigate(_payPageUrl);
		}

		///<summary>Closes the window when the loopback listener receives the return (a backstop in the embedded window; the only signal in the
		///system browser). A listener that times out or is cancelled changes nothing.</summary>
		private void WatchLoopbackReturn() {
			if(_taskLoopbackReturn==null) {
				return;
			}
			_taskLoopbackReturn.ContinueWith(x => {
				if(x.Status!=TaskStatus.RanToCompletion) {
					_=x.Exception;//Observe it.
					return;
				}
				try {
					BeginInvoke((Action)(() => EndWith(EdgeExpressPayPageEnd.Returned)));
				}
				catch(InvalidOperationException) {
					//The window is already gone.
				}
			},TaskScheduler.Default);
		}

		private void StartLifetimeTimer() {
			if(_timeSpanLifetime<=TimeSpan.Zero) {
				return;
			}
			timerLifetime.Interval=(int)Math.Min(int.MaxValue,_timeSpanLifetime.TotalMilliseconds);
			timerLifetime.Start();
		}

		private void CoreWebView2_NavigationStarting(object sender,CoreWebView2NavigationStartingEventArgs e) {
			HandleNavigation(e.Uri,isFrame:false,() => e.Cancel=true);
		}

		private void CoreWebView2_FrameNavigationStarting(object sender,CoreWebView2NavigationStartingEventArgs e) {
			HandleNavigation(e.Uri,isFrame:true,() => e.Cancel=true);
		}

		private void HandleNavigation(string uri,bool isFrame,Action actionCancel) {
			EdgeExpressPayPageNavigation payPageNavigation=EdgeExpress.CNP.ClassifyPayPageNavigation(uri,_payPageUrl,_returnUrl,isFrame);
			if(payPageNavigation==EdgeExpressPayPageNavigation.Allow) {
				return;
			}
			//Cancelled before any request goes out, so whatever the return would have carried never leaves the browser.
			actionCancel();
			if(payPageNavigation==EdgeExpressPayPageNavigation.Return) {
				BeginInvoke((Action)(() => EndWith(EdgeExpressPayPageEnd.Returned)));
				return;
			}
			if(isFrame) {
				return;
			}
			BeginInvoke((Action)(() => OfferSystemBrowser(uri)));
		}

		///<summary>A link to another site on the pay page: open it in the default browser only if the user confirms, and never an Open Dental host or a
		///non-https address.</summary>
		private void OfferSystemBrowser(string uri) {
			Uri uriTarget;
			if(!Uri.TryCreate(uri??"",UriKind.Absolute,out uriTarget) || uriTarget.Scheme!=Uri.UriSchemeHttps || VendorUrlPolicy.Check(uriTarget)!=VendorUrlProblem.None) {
				return;
			}
			string message=Lan.g(this,"The card entry page tried to open another site:")+"\r\n"+uriTarget.Host+"\r\n\r\n"
				+Lan.g(this,"Open it in your web browser?");
			if(MessageBox.Show(this,message,"",MessageBoxButtons.YesNo)!=DialogResult.Yes) {
				return;
			}
			ODException.SwallowAnyException(() => SystemBrowser.Open(uriTarget.AbsoluteUri));
		}

		private void CoreWebView2_NewWindowRequested(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
			//Never a second window. Handled with no NewWindow set means nothing opens.
			e.Handled=true;
			string uri=e.Uri;
			BeginInvoke((Action)(() => OfferSystemBrowser(uri)));
		}

		private void CoreWebView2_DownloadStarting(object sender,CoreWebView2DownloadStartingEventArgs e) {
			e.Cancel=true;
		}

		private void CoreWebView2_PermissionRequested(object sender,CoreWebView2PermissionRequestedEventArgs e) {
			e.State=CoreWebView2PermissionState.Deny;
		}

		private void timerLifetime_Tick(object sender,EventArgs e) {
			timerLifetime.Stop();
			EndWith(EdgeExpressPayPageEnd.TimedOut);
		}

		private void butFinished_Click(object sender,EventArgs e) {
			EndWith(EdgeExpressPayPageEnd.Returned);
		}

		private void butClose_Click(object sender,EventArgs e) {
			EndWith(EdgeExpressPayPageEnd.Closed);
		}

		///<summary>Records how the window ended and closes it. Only the first call counts.</summary>
		private void EndWith(EdgeExpressPayPageEnd payPageEnd) {
			if(_isEnding || IsDisposed) {
				return;
			}
			_isEnding=true;
			PayPageEnd=payPageEnd;
			timerLifetime.Stop();
			DialogResult=(payPageEnd==EdgeExpressPayPageEnd.Returned) ? DialogResult.OK : DialogResult.Cancel;
			Close();
		}

		private void FormEdgeExpressPayPage_FormClosing(object sender,FormClosingEventArgs e) {
			timerLifetime.Stop();
			if(!_isEnding) {
				_isEnding=true;//Closed with the window's X: same as Close.
				PayPageEnd=EdgeExpressPayPageEnd.Closed;
			}
		}
	}
}
