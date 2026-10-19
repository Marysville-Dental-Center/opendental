// Vendor-direct integrations: shows the EdgeExpress hosted pay page and gets its result (docs/specs/vendor-direct/edgeexpress.md 3.1 to 3.4).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;
using CodeBase.Controls;
using CodeBase.VendorDirect;
using OpenDental.UI;
using OpenDentBusiness;
using Task=System.Threading.Tasks.Task;

namespace OpenDental {
	///<summary>How the pay-page step ended. Whatever it is, the result always comes from asking the card processor (QUERYPAYMENT) afterwards.</summary>
	public enum EdgeExpressPayPageEnd {
		///<summary>The page sent the browser to the return URL (or the user pressed "I'm finished" in system-browser mode).</summary>
		Returned,
		///<summary>The user closed the window.</summary>
		Closed,
		///<summary>The session lifetime ran out.</summary>
		TimedOut,
		///<summary>The page could not be shown.</summary>
		Failed,
	}

	///<summary>One pay-page session's return settings, chosen before the session is created because the return URL goes into the session request.
	///In Loopback mode it owns the listener; dispose it when the pay-page step is over.</summary>
	public sealed class EdgeExpressPayPageSession:IDisposable {
		///<summary>The clinic whose credentials and return settings the session uses.</summary>
		public long ClinicNum { get; internal set; }
		///<summary>Loopback only if the listener started; otherwise Embedded.</summary>
		public EdgeExpressReturnMode ReturnMode { get; internal set; }
		///<summary>Pass this to GetUrlForPaymentPage or GetUrlForCreditCardAlias as returnUrl.</summary>
		public string ReturnUrl { get; internal set; }
		internal LoopbackListener LoopbackListener_;
		internal Task TaskLoopbackReturn;
		internal CancellationTokenSource CancellationTokenSource_;

		public void Dispose() {
			ODException.SwallowAnyException(() => CancellationTokenSource_?.Cancel());
			ODException.SwallowAnyException(() => LoopbackListener_?.Dispose());
			ODException.SwallowAnyException(() => CancellationTokenSource_?.Dispose());
		}
	}

	///<summary>The EdgeExpress card-not-present pay page. The callers (FormPayment and FormCreditCardManage) use RunPayPage, or the three steps:
	///BeginPayPage before creating the session, ShowPayPage, then GetResult.</summary>
	public static class EdgeExpressL {
		///<summary>Step 1, before the session is created: picks the clinic's return mode. In Loopback mode it starts the loopback listener now,
		///because its address goes into the session request; if the listener can't bind, it logs a warning and falls back to Embedded mode.</summary>
		public static EdgeExpressPayPageSession BeginPayPage(long clinicNum) {
			EdgeExpressPayPageSession payPageSession=new EdgeExpressPayPageSession();
			payPageSession.ClinicNum=clinicNum;
			payPageSession.ReturnMode=EdgeExpressReturnMode.Embedded;
			payPageSession.ReturnUrl=EdgeExpress.CNP.GetReturnUrl(clinicNum);
			if(EdgeExpress.CNP.GetReturnMode(clinicNum)!=EdgeExpressReturnMode.Loopback) {
				return payPageSession;
			}
			LoopbackListener loopbackListener=EdgeExpress.CNP.StartReturnListener();
			if(loopbackListener==null) {
				Logger.WriteLine("Loopback return mode unavailable (could not bind); using the embedded return.","EdgeExpress");
				return payPageSession;
			}
			payPageSession.ReturnMode=EdgeExpressReturnMode.Loopback;
			payPageSession.ReturnUrl=loopbackListener.RedirectUri;
			payPageSession.LoopbackListener_=loopbackListener;
			payPageSession.CancellationTokenSource_=new CancellationTokenSource();
			//Session lifetime plus one minute; the session disposes it sooner.
			payPageSession.TaskLoopbackReturn=loopbackListener.WaitForRedirectAsync(
				EdgeExpress.CNP.PayPageSessionLifetime.Add(TimeSpan.FromMinutes(1)),payPageSession.CancellationTokenSource_.Token);
			return payPageSession;
		}

		///<summary>Step 2: shows the pay page of a session (xWebResponse from GetUrlForPaymentPage or GetUrlForCreditCardAlias). payPageSession is
		///the one from BeginPayPage that the session was created with; null means Embedded mode with the clinic's return URL. Uses the WebView2 window
		///when the runtime is installed. Without it: Loopback mode opens the system browser and waits for the listener; Embedded mode falls back to
		///the IE control (FormWebBrowser), which closes on the same return URL.</summary>
		public static EdgeExpressPayPageEnd ShowPayPage(XWebResponse xWebResponse,long clinicNum,IWin32Window owner,
			EdgeExpressPayPageSession payPageSession=null)
		{
			if(payPageSession==null) {
				payPageSession=new EdgeExpressPayPageSession();
				payPageSession.ClinicNum=clinicNum;
				payPageSession.ReturnMode=EdgeExpressReturnMode.Embedded;
				payPageSession.ReturnUrl=EdgeExpress.CNP.GetReturnUrl(clinicNum);
			}
			//The pay page comes from the card processor. Show it only if it is https and not an Open Dental host.
			VendorUrlProblem vendorUrlProblem=VendorUrlPolicy.Check(xWebResponse?.HpfUrl);
			if(vendorUrlProblem!=VendorUrlProblem.None) {
				MessageBox.Show(owner,Lans.g("EdgeExpress","The card processor sent an address that can't be opened:")+" "
					+Lans.g("EdgeExpress",VendorUrlPolicy.Describe(vendorUrlProblem)));
				return EdgeExpressPayPageEnd.Failed;
			}
			TimeSpan timeSpanLifetime=GetTimeLeft(xWebResponse);
			bool isTestGateway=EdgeExpress.CNP.IsTestGateway();
			if(ODWebView2.IsRuntimeAvailable()) {
				EdgeExpressPayPageEnd payPageEnd;
				using(FormEdgeExpressPayPage formEdgeExpressPayPage=new FormEdgeExpressPayPage(xWebResponse.HpfUrl,payPageSession.ReturnUrl,isTestGateway,
					timeSpanLifetime,payPageSession.TaskLoopbackReturn))
				{
					formEdgeExpressPayPage.ShowDialog(owner);
					payPageEnd=formEdgeExpressPayPage.PayPageEnd;
				}
				if(payPageEnd!=EdgeExpressPayPageEnd.Failed) {
					return payPageEnd;
				}
				//WebView2 is installed but couldn't start. Fall through to the same fallbacks as when it is missing.
			}
			if(payPageSession.ReturnMode==EdgeExpressReturnMode.Loopback) {
				using FormEdgeExpressPayPage formEdgeExpressPayPage=new FormEdgeExpressPayPage(xWebResponse.HpfUrl,payPageSession.ReturnUrl,isTestGateway,
					timeSpanLifetime,payPageSession.TaskLoopbackReturn,isSystemBrowser:true);
				formEdgeExpressPayPage.ShowDialog(owner);
				return formEdgeExpressPayPage.PayPageEnd;
			}
			using FormWebBrowser formWebBrowser=new FormWebBrowser(xWebResponse.HpfUrl);
			formWebBrowser.UrlCloseOn=payPageSession.ReturnUrl;
			if(formWebBrowser.ShowDialog(owner)==DialogResult.OK) {
				return EdgeExpressPayPageEnd.Returned;
			}
			return EdgeExpressPayPageEnd.Closed;
		}

		///<summary>Step 3: asks the card processor for the result, with up to EdgeExpress.CNP.RepollCount more tries while it is still pending, under
		///a progress window ("Checking with the card processor..."). The query uses the session's clinic. Returns the updated row; a result that is
		///still EdgeExpressPending is left for reconciliation (EdgeExpressReconcileJob). EdgeExpress.CNP.GetUserMessage gives the text to show.</summary>
		public static XWebResponse GetResult(XWebResponse xWebResponse,Payment paymentExisting=null,bool doCreatePayment=true) {
			XWebResponse xWebResponseProcessed=null;
			ProgressWin progressWin=new ProgressWin();
			progressWin.ActionMain=() => {
				xWebResponseProcessed=EdgeExpress.CNP.ProcessTransactionWithRepoll(xWebResponse,paymentExisting,doCreatePayment);
			};
			progressWin.ShowCancelButton=false;
			progressWin.StartingMessage=Lans.g("EdgeExpress","Checking with the card processor...");
			progressWin.ShowDialog();
			return xWebResponseProcessed??xWebResponse;
		}

		///<summary>The whole pay-page step: picks the return mode for the clinic, creates the session with funcCreateSession (given the return URL to
		///pass on, it calls GetUrlForPaymentPage or GetUrlForCreditCardAlias with that clinic and returns the row), shows the page, and gets the
		///result. clinicNum must be the clinic the session is created with (EdgeExpress.CNP.ResolveClinicNum). Exceptions from creating the session
		///(no connection, credentials) are thrown to the caller; no row is written then.</summary>
		public static XWebResponse RunPayPage(IWin32Window owner,long clinicNum,Func<string,XWebResponse> funcCreateSession,Payment paymentExisting=null,
			bool doCreatePayment=true)
		{
			using EdgeExpressPayPageSession payPageSession=BeginPayPage(clinicNum);
			XWebResponse xWebResponse=funcCreateSession(payPageSession.ReturnUrl);
			ShowPayPage(xWebResponse,clinicNum,owner,payPageSession);
			return GetResult(xWebResponse,paymentExisting,doCreatePayment);
		}

		///<summary>How long the pay page stays open: until the session's HpfExpiration, but at least 30 seconds and at most the session lifetime.</summary>
		private static TimeSpan GetTimeLeft(XWebResponse xWebResponse) {
			TimeSpan timeSpanLeft=EdgeExpress.CNP.PayPageSessionLifetime;
			if(xWebResponse.HpfExpiration.Year>1880) {
				timeSpanLeft=xWebResponse.HpfExpiration-DateTime.Now;
			}
			if(timeSpanLeft>EdgeExpress.CNP.PayPageSessionLifetime) {
				timeSpanLeft=EdgeExpress.CNP.PayPageSessionLifetime;
			}
			if(timeSpanLeft<TimeSpan.FromSeconds(30)) {
				timeSpanLeft=TimeSpan.FromSeconds(30);
			}
			return timeSpanLeft;
		}
	}
}
