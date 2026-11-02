using CodeBase;
using CodeBase.Controls;
using CodeBase.VendorDirect;
using OpenPmsDental.UI;
using System;
using System.Windows.Forms;
using System.Text;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Diagnostics;

namespace OpenPmsDental {
	///<summary>A wrapper form to display a web page or an HTML document within a window of the program (WebView2).
	///With UrlBrowseTo it shows a vendor's page: only http and https addresses load, and a page that asks for a new window gets the default
	///browser instead (never a second WebView2 window). With HtmlContent it shows that HTML as a page of its own (ODWebView2.NavigateToHtml):
	///scripts off, and links open in the default browser.</summary>
	public partial class FormWebView:FormODBase {
		///<summary>Set to the value passed into the constructor if one was passed in.  Navigates the browser control to this url on load.</summary>
		public string UrlBrowseTo="";
		///<summary>Set to the post data to be sent to the browser.</summary>
		public string PostData=null;
		///<summary>Any additional headers will be sent to the browser. Will only be sent if postData is set as well.</summary>
		public string AdditionalHeaders="";
		///<summary>Not used: a page that asks for a new window always gets the default browser.</summary>
		public bool CanWrapNewWindow;
		///<summary>Default to false. Determines if url is single use or not. When this is set to true, then the refresh button is not added. See LayoutToolBar()
		///Bamboo's API returns a single use URL for PHI security reasons. Hitting refresh would cause an unauthorized message to show from their server which would force offices to close and relaunch the bridge.
		///</summary>
		public bool IsUrlSingleUse;
		/// <summary>This will be a static form title that overrides the dynamic website title.</summary>
		public string Title="";
		///<summary>HTML to show when UrlBrowseTo is blank, for example a clearinghouse's benefits page.</summary>
		public string HtmlContent;

		///<summary>Used when opening a new browser window via a link or to display html content.
		///If html content and url specified, url will be used and given content ignored. postData and additionalHeaders will only be used if url is
		///passed in.</summary>
		public FormWebView(){
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private async void FormWebView_Load(object sender,EventArgs e) {
			Cursor=Cursors.WaitCursor;//Is set back to default cursor after the document loads inside the browser.
			Application.DoEvents();//To show cursor change.
			Text=Lan.g(this,"Loading")+"...";
			LayoutToolBars();
			try {
				await webViewMain.Init();
			}
			catch(Exception ex) {
				ex.DoNothing();//Init told the user what is missing.
				Cursor=Cursors.Default;
				Close();
				return;
			}
			if(IsDisposed) {
				return;
			}
			webViewMain.CoreWebView2.DocumentTitleChanged+=WebViewMain_DocumentTitleChanged;
			if(UrlBrowseTo=="") {
				Text=Title;//Since we are not browsing, we need to clear the title stored within the Text.
				if(!string.IsNullOrEmpty(HtmlContent)) {
					webViewMain.LinkClicked+=WebViewMain_LinkClicked;
					webViewMain.AreBrowserKeysEnabled=true;//Ctrl+F finds text in the response, as in the old browser window.
					try {
						//Html from outside the office (a clearinghouse's response): no file but its own, nothing from the web.
						webViewMain.NavigateToHtml(HtmlContent);
					}
					catch(Exception ex) {
						FriendlyException.Show("Error loading window.",ex);
					}
				}
				Cursor=Cursors.Default;
				return;
			}
			//Use the window as a simple web browswer when a URL is passed in.
			//This will also change the Text to the title of the URL page browsed to.
			webViewMain.CoreWebView2.NavigationStarting+=WebViewMain_NavigationStarting;
			webViewMain.CoreWebView2.NewWindowRequested+=WebViewMain_NewWindowRequested;
			try {
				if(PostData==null) {
					webViewMain.CoreWebView2.Navigate(UrlBrowseTo);
				}
				else {
					byte[] byteArrayPostData=Encoding.ASCII.GetBytes(PostData);
					using MemoryStream memoryStream = new MemoryStream(byteArrayPostData);
					CoreWebView2WebResourceRequest coreWebView2WebResourceRequest=webViewMain.CoreWebView2.Environment.CreateWebResourceRequest(UrlBrowseTo,
						"POST",memoryStream,AdditionalHeaders);
					webViewMain.CoreWebView2.NavigateWithWebResourceRequest(coreWebView2WebResourceRequest);
				}
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				FriendlyException.Show("Error loading window.",ex);
				return;
			}
			Cursor=Cursors.Default;
		}

		///<summary>A vendor's page stays on the web: only http and https addresses load at the top level (no file: or other programs' protocols).</summary>
		private void WebViewMain_NavigationStarting(object sender,CoreWebView2NavigationStartingEventArgs e) {
			Uri uri;
			if(!Uri.TryCreate(e.Uri??"",UriKind.Absolute,out uri) || (uri.Scheme!=Uri.UriSchemeHttps && uri.Scheme!=Uri.UriSchemeHttp)) {
				e.Cancel=true;
			}
		}

		///<summary>Never a second WebView2 window: the address opens in the default browser when the vendor rules allow it (https, not an Open
		///Dental host).</summary>
		private void WebViewMain_NewWindowRequested(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
			e.Handled=true;
			string uri=e.Uri;
			if(!e.IsUserInitiated) {
				return;
			}
			BeginInvoke((Action)(() => ODException.SwallowAnyException(() => SystemBrowser.Open(uri))));
		}

		///<summary>A link in HtmlContent: http, https and mailto links open in the default browser; anything else does nothing.</summary>
		private void WebViewMain_LinkClicked(object sender,ODWebView2LinkEventArgs e) {
			WebView2Policy.OpenInSystemBrowser(e.GetSystemBrowserUrl());
		}

		///<summary></summary>
		public void LayoutToolBars() {
			ToolBarMain.Buttons.Clear();
			ContextMenu contextMenuSettings = new ContextMenu();
			MenuItem menuItemClearCache = new MenuItem();
			menuItemClearCache.Name="ClearCache";
			menuItemClearCache.Text="Clear Browser Cache";
			menuItemClearCache.Click+=new System.EventHandler(this.menuItemClearCache_Click);
			contextMenuSettings.MenuItems.Add(menuItemClearCache);
			ODToolBarButton toolBarButtonSettings = new ODToolBarButton(Lan.g(this,"Settings"),-1,"","Settings");
			toolBarButtonSettings.Style=ODToolBarButtonStyle.DropDownButton;
			toolBarButtonSettings.DropDownMenu=contextMenuSettings;
			ToolBarMain.Buttons.Add(toolBarButtonSettings);
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Back"),0,"","Back"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Forward"),1,"","Forward"));
			if(!IsUrlSingleUse) {
				ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Refresh"),-1,"","Refresh"));
			}
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Print"),-1,"","Print"));
			ToolBarMain.Invalidate();
		}

		private void WebViewMain_DocumentTitleChanged(object sender,object e) {
			SetTitle();
		}

		///<summary>Sets the text of the form to the browser's DocumentTitle. A page of HtmlContent keeps Title (WebView2 names a page without a
		///title after its file).</summary>
		protected virtual void SetTitle() {
			if(!string.IsNullOrWhiteSpace(Title)) {
				Text=Title;
				return;
			}
			if(UrlBrowseTo=="") {
				return;
			}
			Text=webViewMain.CoreWebView2.DocumentTitle;
		}

		private void ToolBarMain_ButtonClick(object sender,ODToolBarButtonClickEventArgs e) {
			if(webViewMain.CoreWebView2==null) {
				return;
			}
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
					webViewMain.Reload();
					break;
				case "Print":
					webViewMain.ShowPrintDialog();
					break;
			}
			Cursor=Cursors.Default;
		}

		private void menuItemClearCache_Click(object sender,EventArgs e) {
			webViewMain.ClearCache();
		}

	}
}
