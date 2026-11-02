using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using CodeBase;

namespace OpenPmsDental {

	public partial class FormWebBrowserPrefs:FormODBase {
		public LayoutManagerForms LayoutManager=new LayoutManagerForms();
		public string HtmlContent;
		///<summary>Set this to the location where the window should start.  If this makes it fall outside the screen, it will be fixed automatically.</summary>
		public Point PointStart=new Point(0,0);
		///<summary>Default size is 591 x 363. If your info is too big and causes scrolling, then increase the size of the window.</summary>
		public Size SizeWindow=new Size(0,0);

		///<summary>The page FormPreferences shows for a preference: the label's text and the preference name, encoded, and the details, which are
		///HTML from the program's own PrefInfos.xml (links to the manual, line breaks) and stay as they are.</summary>
		public static string BuildHtml(string labelText,string detailsHtml,string prefName,int fontSizePx) {
			string body="<p><b>"+HtmlPage.Encode(labelText)+"</b></p>"
				+"<p>"+(detailsHtml??"")+"</p>";
			if(!string.IsNullOrEmpty(prefName)) {
				body+="<p style=\"background-color:rgb(230,230,234)\">Internal PrefName: "+HtmlPage.Encode(prefName)+"</p>";
			}
			return HtmlPage.Build(body,bodyStyle:"font-family:sans-serif; font-size:"+fontSizePx+"px");
		}

		///<summary>Used when opening additional details within FormPreferences. Links in the details open in the default browser.</summary>
		public FormWebBrowserPrefs() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private async void FormWebBrowserPrefs_Load(object sender,EventArgs e) {
			if(SizeWindow!=new Size(0,0)) {
				Size=new Size(LayoutManager.Scale(SizeWindow.Width),LayoutManager.Scale(SizeWindow.Height));
			}
			if(PointStart==new Point(0,0)) {
				CenterToScreen();
			}
			else {
				Rectangle rectangleWorkingArea=System.Windows.Forms.Screen.FromHandle(this.Handle).WorkingArea;
				int x=PointStart.X;
				if(x+Width>rectangleWorkingArea.Right-10){
					x=rectangleWorkingArea.Right-Width-10;
				}
				int y=PointStart.Y;
				if(y+Height>rectangleWorkingArea.Bottom-10){
					y=rectangleWorkingArea.Bottom-Height-10;
				}
				Location=new Point(x,y);
			}
			try {
				await webView.Init();
			}
			catch(Exception ex) {
				ex.DoNothing();
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			if(string.IsNullOrEmpty(HtmlContent)) {
				return;
			}
			webView.Visible=true;
			webView.LinkClicked+=WebView_LinkClicked;
			ODException.SwallowAnyException(() => {
				webView.NavigateToHtml(HtmlContent);
			});
		}

		///<summary>The page stays as it is: a link opens in the default browser (http and https only), never in this window or a new WebView2 one.</summary>
		private void WebView_LinkClicked(object sender,CodeBase.Controls.ODWebView2LinkEventArgs e) {
			string url=e.GetSystemBrowserUrl();
			if(url=="") {
				return;
			}
			if(!CodeBase.Controls.WebView2Policy.OpenInSystemBrowser(url)) {
				MessageBox.Show(Lan.g(this,"Could not find")+" "+url+"\r\n"+Lan.g(this,"Please set up a default web browser."));
			}
		}
	}
}