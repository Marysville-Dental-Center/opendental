using OpenDental.UI;
using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDental {
	///<summary>Browser window for CareCredit's hosted pages, using the WebView2 control. Modal for a purchase (FormPayment pulls the result back when it
	///closes); non-modal for every other page (CareCreditL.ShowPage), whose results come from the background poller. Only the hosted-page host set in
	///CareCredit Setup is shown here; links to anything else open in the default browser, and Open Dental hosts are never opened.</summary>
	public partial class FormCareCreditWeb:FormODBase {
		///<summary>Set to the value passed into the constructor if one was passed in.  Navigates the browser control to this url on load.</summary>
		private string _urlBrowseTo="";
		///<summary>The patient CareCredit was opened for.</summary>
		private Patient _patient;
		///<summary>Default to false. When true, the Refresh button is not added (see LayoutToolBars), for a hosted-page URL that can only be loaded
		///once; reloading it would show an error from the vendor. Set it once Synchrony's implementation guide confirms CareCredit's session URLs are
		///single use.</summary>
		public bool IsUrlSingleUse;
		///<summary>The session ID of the CareCredit prefill response.</summary>
		public string SessionId;


		///<summary>Used when opening a new browser window via a link.</summary>
		public FormCareCreditWeb(string url,Patient patient) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_urlBrowseTo=url;
			_patient=patient;
			SessionId=CareCredit.GetSessionIDFromUrl(url);
		}

		private async void FormCareCreditWeb_Load(object sender,EventArgs e) {
			if(string.IsNullOrEmpty(_urlBrowseTo) || !CareCredit.IsHostedPageUrl(_urlBrowseTo)) {
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			try {
				await webViewMain.Init();
			}
			catch(Exception ex){
				FriendlyException.Show("Error loading window.",ex);
				Close();
				return;
			}
			webViewMain.CoreWebView2.NavigationStarting+=CoreWebView2_NavigationStarting;
			webViewMain.CoreWebView2.NewWindowRequested+=CoreWebView2_NewWindowRequested;
			Text=Lan.g(this,"Loading")+"...";
			LayoutToolBars();
			webViewMain.CoreWebView2.Navigate(_urlBrowseTo);
		}

		///<summary>Keeps this window on the hosted-page host. Anything else is cancelled here and, if allowed, opened in the default browser.</summary>
		private void CoreWebView2_NavigationStarting(object sender,CoreWebView2NavigationStartingEventArgs e) {
			if(CareCredit.IsHostedPageUrl(e.Uri)) {
				return;
			}
			e.Cancel=true;
			OpenInSystemBrowser(e.Uri);
		}

		///<summary>Pop-ups (a cardholder agreement, for example) open in the default browser, never in a new WebView2 window.</summary>
		private void CoreWebView2_NewWindowRequested(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
			e.Handled=true;
			OpenInSystemBrowser(e.Uri);
		}

		private void OpenInSystemBrowser(string url) {
			if(VendorUrlPolicy.Check(url)!=VendorUrlProblem.None) {
				return;//Not HTTPS, or a host this program never contacts.
			}
			try {
				SystemBrowser.Open(url);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary></summary>
		public void LayoutToolBars() {
			ToolBarMain.Buttons.Clear();
			ContextMenu contextMenuSettings=new ContextMenu();
			MenuItem menuItemClearCache=new MenuItem();
			menuItemClearCache.Name="ClearCache";
			menuItemClearCache.Text="Clear Browser Cache";
			menuItemClearCache.Click+=new System.EventHandler(this.menuItemClearCache_Click);
			contextMenuSettings.MenuItems.Add(menuItemClearCache);
			ODToolBarButton buttonSettings=new ODToolBarButton(Lan.g(this,"Settings"),-1,"","Settings");
			buttonSettings.Style=ODToolBarButtonStyle.DropDownButton;
			buttonSettings.DropDownMenu=contextMenuSettings;
			ToolBarMain.Buttons.Add(buttonSettings);
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Back"),0,"","Back"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Forward"),1,"","Forward"));
			if(!IsUrlSingleUse) {
				ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Refresh"),-1,"","Refresh"));
			}
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Print"),-1,"","Print"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Close"),-1,"","Close"));
			ToolBarMain.Invalidate();
		}

		private void webViewMain_NavigationCompleted(object sender,CoreWebView2NavigationCompletedEventArgs e) {
			SetTitle();
		}

		///<summary>Sets the text of the form to the browser's DocumentTitle.</summary>
		private void SetTitle() {
			Text=Lan.g(this,"CareCredit");
			if(_patient!=null) {//Can only be null when a subwindow is opened by clicking on a link from inside another CareCredit instance.
				Text+=" - "+_patient.GetNameFL();
			}
		}


		private void ToolBarMain_ButtonClick(object sender,ODToolBarButtonClickEventArgs e) {
			switch(e.Button.Tag.ToString()) {
				case "Back":
					if(webViewMain.CanGoBack) {
						Cursor=Cursors.WaitCursor;//Is set back to default cursor after the document loads inside the browser.
						Application.DoEvents();//To show cursor change.
						Text=Lan.g(this,"Loading")+"...";
						webViewMain.GoBack();
					}
					break;
				case "Forward":
					if(webViewMain.CanGoForward) {
						Cursor=Cursors.WaitCursor;//Is set back to default cursor after the document loads inside the browser.
						Application.DoEvents();//To show cursor change.
						Text=Lan.g(this,"Loading")+"...";
						webViewMain.GoForward();
					}
					break;
				case "Refresh":
					webViewMain.Refresh();
					break;
				case "Print":
					if(webViewMain.CoreWebView2!=null) {
						webViewMain.CoreWebView2.ExecuteScriptAsync("window.print();");
					}
					break;
				case "Close":
					DialogResult=DialogResult.Cancel;
					Close();//For when we launch the window in a non-modal manner.
					break;
			}
		}

		private void menuItemClearCache_Click(object sender,EventArgs e) {
			webViewMain.ClearCache();
		}

		protected override void OnClosed(EventArgs e) {
			webViewMain.Dispose();
			base.OnClosed(e);
		}
	}
}
