using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using CodeBase;

namespace OpenPmsDental {
	public partial class UserControlEmailTemplate:UserControl {

		public UserControlEmailTemplate() {
			InitializeComponent();
			Font=LayoutManagerForms.FontInitial;
			webViewEmail.LinkClicked+=webViewEmail_LinkClicked;
		}

		public void RefreshView(string plainText,string htmlText,EmailType emailType) {
			textboxPlainText.Text=plainText;
			if(string.IsNullOrEmpty(htmlText)) {
				labelNoHtml.Visible=true;
				labelHtml.Visible=false;
				webViewEmail.Visible=false;
				tableLayoutPanel1.ColumnStyles[1].Width=0;
				
			}
			else {
				labelNoHtml.Visible=false;
				labelHtml.Visible=true;
				tableLayoutPanel1.ColumnStyles[1].Width=50;//50%
				webViewEmail.Visible=true;
				string xhtml=htmlText;
				if(emailType==EmailType.Html) {
					//This might not work for images, we should consider blocking them or warning them about sending if we detect images
					try {
						xhtml=MarkupEdit.TranslateToXhtml(htmlText,true,false,true);
					}
					catch(Exception ex) {
						ex.DoNothing();
					}
				}
				webViewEmail.NavigateToHtml(xhtml);
			}
		}

		///<summary>A link clicked in the preview opens in the default browser or mail program (http, https and mailto only), never in the preview.</summary>
		private void webViewEmail_LinkClicked(object sender,CodeBase.Controls.ODWebView2LinkEventArgs e) {
			CodeBase.Controls.WebView2Policy.OpenInSystemBrowser(e.GetSystemBrowserUrl());
		}


	}
}
