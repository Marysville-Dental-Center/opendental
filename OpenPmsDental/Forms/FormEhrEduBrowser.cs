using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormEhrEduBrowser:FormODBase {
		public string ResourceURL;
		public bool DidPrint;

		public FormEhrEduBrowser(string resourceURL) {
			ResourceURL=resourceURL;
			InitializeComponent();
			InitializeLayoutManager();
		}

		///<summary>Shows the education resource's web page. Only http and https pages load; a page that asks for a new window gets the default browser.</summary>
		private async void FormEduBrowser_Load(object sender,EventArgs e) {
			Uri uri;
			if(!Uri.TryCreate(ResourceURL??"",UriKind.Absolute,out uri) || !IsWebUri(uri.AbsoluteUri)) {
				MessageBox.Show("The specified URL is in an incorrect format.  Did you include the http:// ?");
				DialogResult=DialogResult.Cancel;
				return;
			}
			Cursor=Cursors.WaitCursor;
			try {
				await webViewMain.Init();
			}
			catch(Exception ex) {
				ex.DoNothing();//Init told the user what is missing.
				Cursor=Cursors.Default;
				DialogResult=DialogResult.Cancel;
				return;
			}
			webViewMain.CoreWebView2.NavigationStarting+=webViewMain_NavigationStarting;
			webViewMain.CoreWebView2.NewWindowRequested+=webViewMain_NewWindowRequested;
			webViewMain.CoreWebView2.Navigate(uri.AbsoluteUri);
			Cursor=Cursors.Default;
		}

		private static bool IsWebUri(string uri) {
			Uri uriParsed;
			return Uri.TryCreate(uri??"",UriKind.Absolute,out uriParsed) && (uriParsed.Scheme==Uri.UriSchemeHttp || uriParsed.Scheme==Uri.UriSchemeHttps);
		}

		///<summary>The window shows web pages only: a file: address or another program's protocol never loads.</summary>
		private void webViewMain_NavigationStarting(object sender,Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e) {
			if(!IsWebUri(e.Uri)) {
				e.Cancel=true;
			}
		}

		///<summary>Never a second WebView2 window: the address opens in the default browser.</summary>
		private void webViewMain_NewWindowRequested(object sender,Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e) {
			e.Handled=true;
			if(!e.IsUserInitiated) {
				return;
			}
			string uri=e.Uri;
			BeginInvoke((Action)(() => CodeBase.Controls.WebView2Policy.OpenInSystemBrowser(uri)));
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(webViewMain.CoreWebView2==null) {
				return;
			}
			//Windows' print dialog, which also allows user to choose printer (WebView2's print API)
			webViewMain.ShowPrintDialog();
			DidPrint = true;
		}

	}
}