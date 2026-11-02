using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;
using System.Text.RegularExpressions;
using CodeBase;
using CodeBase.Controls;
using DataConnectionBase;

namespace OpenPmsDental {
	///<summary>What a link in a wiki page points to (FormWiki.ParseLink).</summary>
	public enum EnumWikiLink {
		///<summary>Nothing the wiki opens.</summary>
		None,
		///<summary>wiki:Page Title</summary>
		WikiPage,
		///<summary>wikifile:C:\folder\file.txt</summary>
		File,
		///<summary>folder:C:\folder</summary>
		Folder,
		///<summary>wikifilecloud:path in the A to Z folder</summary>
		FileCloud,
		///<summary>foldercloud:path in the A to Z folder</summary>
		FolderCloud,
		///<summary>An http, https or mailto address, for the default browser.</summary>
		Web,
	}

	public partial class FormWiki:FormODBase {
		public WikiPage WikiPageCur;
		private List<string> _listHistoryNav;
		///<summary>Number of pages back that you are browsing. Current page == 0, Oldest page == _listHistoryNav.Length. </summary>
		private int _idxHistoryNavBack;
		private FormWikiSearch _formWikiSearch;

		public FormWiki() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormWiki_Load(object sender,EventArgs e) {
			Rectangle rectangleScreen=System.Windows.Forms.Screen.GetWorkingArea(this);
			Width=LayoutManager.Scale(960);
			Height=rectangleScreen.Height+(int)LayoutManager.ScaleMS(8);//for the transparent 8 pixels at bottom
			Left=rectangleScreen.Left+((rectangleScreen.Width-Width)/2);
			Top=rectangleScreen.Top;
			LayoutToolBar();
			_listHistoryNav=new List<string>();
			_idxHistoryNavBack=0;//This is the pointer that keeps track of our position in historyNav.  0 means this is the newest page in history, a positive number is the number of pages before the newest page.
			if(Plugins.HookMethod(this,"FormWiki.FormWiki_Load_beforeHomePageLoad")) {
				goto HookSkipHomePageLoad;
			}
			LoadWikiPageHome();
			HookSkipHomePageLoad: { }
			Plugins.HookAddCode(this,"FormWiki.FormWiki_Load_end");
		}

		///<summary>Loads the user's home page or the wiki page with the title of "Home" if a custom home page has not been set before.</summary>
		private void LoadWikiPageHome() {
			_idxHistoryNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history or add to page history
			List<UserOdPref> listUserOdPrefs=UserOdPrefs.GetByUserAndFkeyType(Security.CurUser.UserNum,UserOdFkeyType.WikiHomePage);
			if(listUserOdPrefs.Count > 0) {
				LoadWikiPage(listUserOdPrefs[0].ValueString);
				return;
			}
			LoadWikiPage("Home");
		}

		/// <summary>Because FormWikiEdit is no longer modal, this is necessary to be able to tell FormWiki to refresh when saving an edited page.</summary>
		public void RefreshPage(string pageTitle) {
			_idxHistoryNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history or add to page history
			LoadWikiPage(pageTitle);
		}

		///<summary>Shows the address of the link under the mouse.</summary>
		private void webViewWiki_StatusTextChanged(object sender,EventArgs e) {
			labelStatus.Text=webViewWiki.StatusText;
		}

		public void LoadWikiPagePublic(string pageTitle) {
			Application.DoEvents();//allow initialization
			navPage(pageTitle);
		}

		///<summary>Before calling this, make sure to increment/decrement the historyNavBack index to keep track of the position in history.  If loading a new page, decrement historyNavBack before calling this function.  </summary>
		private void LoadWikiPage(string pageTitle) {
			//This is called from 11 different places, any time the program needs to refresh a page from the db.
			//It's also called from the browser_Navigating event when a "wiki:" link is clicked.
			WikiPage wikiPage=WikiPages.GetByTitle(pageTitle);
			if(wikiPage==null) {
				string errorMsg="";
				if(!WikiPages.IsWikiPageTitleValid(pageTitle,out errorMsg)) {
					MsgBox.Show(this,"That page does not exist and cannot be made because the page title contains invalid characters.");
					return;
				}
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"That page does not exist. Would you like to create it?")) {
					return;
				}
				Action<string> actionWikiSaved=new Action<string>((pageTitleNew) => {
					//Insert the pageTitleNew where the pageTitle exists in the existing WikiPageCur 
					//(guaranteed to be unique because all INVALID WIKIPAGE LINK's have an ID at the end)
					string pageContent=WikiPages.GetWikiPageContentWithWikiPageTitles(WikiPageCur.PageContent);
					WikiPageCur.PageContent=pageContent.Replace(pageTitle,pageTitleNew);
					WikiPageCur.PageContent=WikiPages.ConvertTitlesToPageNums(WikiPageCur.PageContent);
					if(!WikiPageCur.IsDraft) {
						WikiPageCur.PageContentPlainText=MarkupEdit.ConvertToPlainText(WikiPageCur.PageContent);
					}
					WikiPages.Update(WikiPageCur);
					WikiStaticExport.ExportInBackground(new List<string>() { WikiPageCur.PageTitle },isFullExport:false);
				});
				AddWikiPage(actionWikiSaved);
				return;
			}
			WikiPageCur=wikiPage;
			try {
				float scale=LayoutManager.ScaleMyFont();//Include any Zoom adjustment to increase the font size of the Wiki
				webViewWiki.NavigateToHtml(MarkupEdit.TranslateToXhtml(WikiPageCur.PageContent,false,scale:scale));
			}
			catch(Exception ex) {
				webViewWiki.NavigateToHtml("");
				MessageBox.Show(this,Lan.g(this,"This page is broken and cannot be viewed. Click 'Edit' to fix this page.  Error message:")+" "+ex.Message);
			}
			Text="Wiki - "+WikiPageCur.PageTitle;
			#region historyMaint
			//External links open in the default browser (webViewWiki_LinkClicked), so only wiki pages enter the history.
			int indexInHistory=_listHistoryNav.Count-(1+_idxHistoryNavBack);//historyNavBack number of pages before the last page in history.  This is the index of the page we are loading.
			if(_listHistoryNav.Count==0) {//empty history
				_idxHistoryNavBack=0;
				_listHistoryNav.Add("wiki:"+pageTitle);
			}
			else if(_idxHistoryNavBack<0) {//historyNavBack could be negative here.  This means before the action that caused this load, we were not navigating through history, simply set back to 0 and add to historyNav[] if necessary.
				_idxHistoryNavBack=0;
				if(_listHistoryNav[_listHistoryNav.Count-1]!="wiki:"+pageTitle) {
					_listHistoryNav.Add("wiki:"+pageTitle);
				}
			}
			else if(_idxHistoryNavBack>=0 && _listHistoryNav[indexInHistory]!="wiki:"+pageTitle) {//branching from page in history
				_listHistoryNav.RemoveRange(indexInHistory,_idxHistoryNavBack+1);//remove "forward" history. branching off in a new direction
				_idxHistoryNavBack=0;
				_listHistoryNav.Add("wiki:"+pageTitle);
			}
			#endregion
		}

		private void LayoutToolBar() {
			ToolBarMain.Buttons.Clear();
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Back"),0,"","Back"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Fwd"),1,"","Forward"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Setup"),2,Lan.g(this,"Setup master page and styles."),"Setup"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(ODToolBarButtonStyle.Separator));
			ODToolBarButton odToolBarButtonHome=new ODToolBarButton(Lan.g(this,"Home"),3,"","Home");
			odToolBarButtonHome.Style=ODToolBarButtonStyle.DropDownButton;
			odToolBarButtonHome.DropDownMenu=menuHomeDropDown; 
			ToolBarMain.Buttons.Add(odToolBarButtonHome);
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Edit"),4,"","Edit"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Print"),5,"","Print"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Rename"),6,"","Rename"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Archive"),7,"","Archive"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Hist"),8,"","History"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Drafts"),14,"","Drafts"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"In-Links"),9,"","Inc Links"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(ODToolBarButtonStyle.Separator));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Add"),10,"","Add"));
			if(DataConnection.DBtype==DatabaseType.MySql) {//not supported in oracle.
				ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Lists"),13,"","Lists"));
			}
			//ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"All Pages"),11,"","All Pages"));
			ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Search"),12,"","Search"));
			if(WikiStaticExport.IsEnabled()) {//v25_2-wiki-readonly-offline: only when the office keeps a read-only copy.
				ToolBarMain.Buttons.Add(new ODToolBarButton(Lan.g(this,"Read-Only Copy"),-1,
					Lan.g(this,"Open the read-only copy of the wiki in a web browser. It also opens while this program is closed."),"ReadOnlyCopy"));
			}
		}

		private void ToolBarMain_ButtonClick(object sender,OpenPmsDental.UI.ODToolBarButtonClickEventArgs e) {
			switch(e.Button.Tag.ToString()) {
				case "Back":
					Back_Click();
					break;
				case "Forward":
					Forward_Click();
					break;
				case "Setup":
					Setup_Click();
					break;
				case "Home":
					Home_Click();
					break;
				case "Edit":
					if(IsWikiPageLocked()) {
						return;
					}
					Edit_Click();
					break;
				case "Print":
					Print_Click();
					break;
				case "Rename":
					if(IsWikiPageLocked()) {
						return;
					}
					Rename_Click();
					break;
				case "Archive":
					if(IsWikiPageLocked()) {
						return;
					}
					Archive_Click();
					break;
				case "History":
					History_Click();
					break;
				case "Drafts":
					//Viewing and comparing drafts needs only wiki access; FormWikiDrafts blocks editing a locked page's drafts without Wiki Admin.
					Drafts_Click();
					break;
				case "Inc Links":
					Inc_Link_Click();
					break;
				case "Add":
					Add_Click();
					break;
				case "Lists":
					Lists_Click();
					break;
				case "Search":
					Search_Click();
					break;
				case "ReadOnlyCopy":
					ReadOnlyCopy_Click();
					break;
			}
		}

		///<summary>Opens index.html of the read-only copy in the default browser (v25_2-wiki-readonly-offline).</summary>
		private void ReadOnlyCopy_Click() {
			string folder=WikiStaticExport.GetExportPath();
			string pathIndex=WikiStaticExport.GetIndexPath(folder);
			if(!File.Exists(pathIndex)) {
				MessageBox.Show(this,Lan.g(this,"The read-only copy has not been written yet, or its folder cannot be reached:")+"\r\n"+folder+"\r\n"
					+Lan.g(this,"Use Setup, Export Now to write it."));
				return;
			}
			try {
				System.Diagnostics.Process.Start(pathIndex);
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
			}
		}

		///<summary>Returns a boolean whether the current wiki page is locked or not.  
		///Will show a warning message to the user if it is locked and they do not have permission to access locked pages.
		///Always returns true if WikiPageCur is null.
		///Returns false if the user currently logged in has the WikiAdmin permission.</summary>
		private bool IsWikiPageLocked() {
			if(WikiPageCur==null) {
				return true;
			}
			if(Security.IsAuthorized(EnumPermType.WikiAdmin,true) || !WikiPageCur.IsLocked) {
				return false;
			}
			MsgBox.Show(this,"This wiki page is locked and cannot be edited without the Wiki Admin security permission.");
			return true;
		}

		private void menuItemHomePageSave_Click(object sender,EventArgs e) {
			if(WikiPageCur==null) {
				MsgBox.Show(this,"Invalid wiki page selected.");
				return;
			}
			List<UserOdPref> listUserOdPrefs=UserOdPrefs.GetByUserAndFkeyType(Security.CurUser.UserNum,UserOdFkeyType.WikiHomePage);//There should only be one.
			if(listUserOdPrefs.Count > 0) {
				//User is updating their current home page to a new one.
				UserOdPref userOdPrefOld=listUserOdPrefs[0].Clone();
				listUserOdPrefs[0].ValueString=WikiPageCur.PageTitle;
				if(UserOdPrefs.Update(listUserOdPrefs[0],userOdPrefOld)) {
					//Only need to signal cache refresh on change.
					DataValid.SetInvalid(InvalidType.UserOdPrefs);
				}
			}
			else {
				//User is saving a custom home page for the first time.
				UserOdPref userOdPref=new UserOdPref();
				userOdPref.UserNum=Security.CurUser.UserNum;
				userOdPref.ValueString=WikiPageCur.PageTitle;
				userOdPref.FkeyType=UserOdFkeyType.WikiHomePage;
				UserOdPrefs.Insert(userOdPref);
				DataValid.SetInvalid(InvalidType.UserOdPrefs);
			}
			MsgBox.Show(this,"Home page saved.");
		}

		private void Back_Click() {
			if(_idxHistoryNavBack<_listHistoryNav.Count-1) {
				_idxHistoryNavBack++;
			}
			NavToHistory();
			//if(historyNav.Count<2) {//should always be 1 or greater
			//  MsgBox.Show(this,"No more history");
			//  return;
			//}
			//string pageName=historyNav[historyNav.Count-2];//-1 is the last/current page.
			//if(pageName.StartsWith("wiki:")) {
			//  pageName=pageName.Substring(5);
			//  WikiPage wpage=WikiPages.GetByTitle(pageName);
			//  if(wpage==null) {
			//    MessageBox.Show("'"+historyNav[historyNav.Count-2]+"' page does not exist.");//very rare
			//    return;
			//  }
			//  historyNav.RemoveAt(historyNav.Count-1);//remove the current page from history
			//  LoadWikiPage(pageName);//because it's a duplicate, it won't add it again to the list.
			//}
			//else if(pageName.StartsWith("http://")) {//www
			//  //historyNav.RemoveAt(historyNav.Count-1);//remove the current page from history
			//  //no need to set the text because the Navigating event will fire and take care of that.
			//  webViewWiki.Navigate(pageName);//adds new page to history
			//}
			//else {
			//  //?
			//}
		}

		private void Forward_Click() {
			if(_idxHistoryNavBack>0) {
				_idxHistoryNavBack--;
			}
			NavToHistory();
		}

		///<summary>Loads page from history based on historyCurIndex.</summary> 
		private void NavToHistory() {
			if(_idxHistoryNavBack<0 || _idxHistoryNavBack>_listHistoryNav.Count-1) {
				//This should never happen.
				MsgBox.Show(this,"Invalid history index.");
				return;
			}
			string pageName=_listHistoryNav[_listHistoryNav.Count-(1+_idxHistoryNavBack)];//-1 is the last/current page.
			if(pageName.StartsWith("wiki:")) {
				pageName=pageName.Substring(5);
				WikiPage wikiPage=WikiPages.GetByTitle(pageName);
				if(wikiPage==null) {
					MessageBox.Show("'"+_listHistoryNav[_listHistoryNav.Count-(1+_idxHistoryNavBack)]+"' page does not exist.");//very rare
					return;
				}
				//historyNavBack--;//no need to decrement since this is only called from Back_Click and Forward_Click and the appropriate adjustment to this index happens there
				LoadWikiPage(pageName);//because it's a duplicate, it won't add it again to the list.
				return;
			}
			if(pageName.StartsWith("http://")) {//www, which the wiki window never shows itself
				WebView2Policy.OpenInSystemBrowser(pageName);
			}
		}

		private void Setup_Click() {
			FrmWikiSetup frmWikiSetup=new FrmWikiSetup();
			frmWikiSetup.ShowDialog();
			if(!frmWikiSetup.IsDialogOK) {
				return;
			}
			LayoutToolBar();//The Read-Only Copy button follows the setting.
			ToolBarMain.Invalidate();
			if(WikiPageCur==null) {//if browsing the WWW
				return;
			}
			_idxHistoryNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history or add to page history
			LoadWikiPage(WikiPageCur.PageTitle);
		}

		private void Home_Click() {
			LoadWikiPageHome();
		}

		private void Edit_Click() {
			if(Plugins.HookMethod(this,"FormWiki.Edit_Click")) {
				return;
			}
			if(WikiPageCur==null) {
				return;
			}
			if(WikiPages.GetDraftsByTitle(WikiPageCur.PageTitle).Count > 0 && MsgBox.Show(this,MsgBoxButtons.YesNo,
				"This page has one or more drafts associated with it.  Would you like to open a draft instead of the current Wiki page?"))
			{
				using FormWikiDrafts formWikiDrafts=new FormWikiDrafts();
				formWikiDrafts.FormWiki_=this;
				formWikiDrafts.ShowDialog();
				return;
			}
			FormWikiEdit formWikiEdit=new FormWikiEdit();
			formWikiEdit.WikiPageCur=WikiPageCur.Copy();
			formWikiEdit.FormWiki_=this;
			formWikiEdit.Show();
			//Maximized non-modal forms pop up on the wrong screen, as documented in FormODBase.OnLoad, so:
			Rectangle rectangleScreen=System.Windows.Forms.Screen.FromControl(this).WorkingArea;
			//Make it half size for when user un-maximizes.
			formWikiEdit.Bounds=new Rectangle(rectangleScreen.Left+rectangleScreen.Width/4,rectangleScreen.Top+rectangleScreen.Height/4,
				rectangleScreen.Width/2,rectangleScreen.Height/2);
			formWikiEdit.WindowState=FormWindowState.Maximized;
		}

		private void Print_Click() {
			if(WikiPageCur==null) {
				return;
			}
			webViewWiki.ShowPrintDialog();
		}

		private void Rename_Click() {
			if(WikiPageCur==null) {
				return;
			}
			FrmWikiRename frmWikiRename=new FrmWikiRename();
			frmWikiRename.PageTitle=WikiPageCur.PageTitle;
			frmWikiRename.ShowDialog();
			if(!frmWikiRename.IsDialogOK) {
				return;
			}
			WikiPages.Rename(WikiPageCur,frmWikiRename.PageTitle);
			WikiStaticExport.ExportInBackground(null,isFullExport:true);//Other pages show the new title in their links.
			_listHistoryNav[_listHistoryNav.Count-(1+_idxHistoryNavBack)]="wiki:"+frmWikiRename.PageTitle;//keep history updated, do not decrement historyNavBack, stay at the same index in history
			//historyNavBack--;//no need to decrement history counter since we are loading the same page, just with a different name, historyNav was edited above with new name
			LoadWikiPage(frmWikiRename.PageTitle);
		}

		private void Archive_Click() {
			if(WikiPageCur==null) {
				return;
			}
			if(WikiPageCur.PageTitle=="Home") {
				MsgBox.Show(this,"Cannot archive homepage."); 
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Archive this wiki page?  It will still be available from the Search window if needed.")) {
				return;
			}
			WikiPages.Archive(WikiPageCur.PageTitle,Security.CurUser.UserNum);
			WikiStaticExport.ExportInBackground(null,isFullExport:true);//Removes the page and marks the links to it.
			//historyNavBack--;//do not decrement, load will consider this a branch and put "wiki:Home" in place of the deleted page and remove "forward" history.
			LoadWikiPage("Home");
		}

		private void History_Click() {
			if(WikiPageCur==null) {
				return;
			}
			using FormWikiHistory formWikiHistory = new FormWikiHistory();
			formWikiHistory.PageTitle=WikiPageCur.PageTitle;
			formWikiHistory.IsLocked=WikiPageCur.IsLocked;
			formWikiHistory.ShowDialog();
			//historyNavBack--;//no need to decrement since we are loading the same page, possibly a different version, but the same PageTitle
			LoadWikiPage(formWikiHistory.PageTitle);
			//if(FormWH.DialogResult!=DialogResult.OK) {
			//	return;
			//}
			//Nothing to do here.
		}

		private void Drafts_Click() {
			if(WikiPageCur==null) {
				return;
			}
			if(WikiPages.GetDraftsByTitle(WikiPageCur.PageTitle).Count==0) {
				MsgBox.Show(this,"There are no drafts for this Wiki Page.");
				return;
			}
			using FormWikiDrafts formWikiDrafts=new FormWikiDrafts();
			formWikiDrafts.FormWiki_=this;
			formWikiDrafts.ShowDialog();
		}

		private void Inc_Link_Click() {
			if(WikiPageCur==null) {
				return;
			}
			using FormWikiIncomingLinks formWikiIncomingLinks = new FormWikiIncomingLinks();
			formWikiIncomingLinks.PageTitle=WikiPageCur.PageTitle;
			formWikiIncomingLinks.ShowDialog();
			if(formWikiIncomingLinks.DialogResult!=DialogResult.OK) {
				return;
			}
			_idxHistoryNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history or add to page history
			LoadWikiPage(formWikiIncomingLinks.WikiPageJumpTo.PageTitle);
		}

		private void Add_Click() {
			AddWikiPage();
		}

		public void AddWikiPage(Action<string> actionNewPageSaved=null) {
			FrmWikiRename frmWikiRename=new FrmWikiRename();
			frmWikiRename.ShowDialog();
			if(!frmWikiRename.IsDialogOK) {
				return;
			}
			FormWikiEdit formWikiEdit=new FormWikiEdit(actionNewPageSaved);
			formWikiEdit.WikiPageCur=new WikiPage();
			formWikiEdit.WikiPageCur.IsNew=true;
			formWikiEdit.WikiPageCur.PageTitle=frmWikiRename.PageTitle;
			formWikiEdit.WikiPageCur.PageContent="[["+WikiPageCur.PageTitle+"]]\r\n"//link back
				+"<h1>"+frmWikiRename.PageTitle+"</h1>\r\n";//page title
			formWikiEdit.FormWiki_=this;
			formWikiEdit.Show();
		}

		/*No longer used
		private void All_Pages_Click() {
			using FormWikiAllPages FormWAP=new FormWikiAllPages();
			FormWAP.ShowDialog();
			if(FormWAP.DialogResult!=DialogResult.OK) {
			  return;
			}
			historyNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history or add to page history
			LoadWikiPage(FormWAP.SelectedWikiPage.PageTitle);
		}*/

		private void Lists_Click() {
			using FormWikiLists formWikiLists=new FormWikiLists();
			formWikiLists.ShowDialog();
		}

		private void Search_Click() {
			//Reselect existing window if available, if not create a new instance
			if(_formWikiSearch==null || _formWikiSearch.IsDisposed) {
				_formWikiSearch=new FormWikiSearch();
				_formWikiSearch.NavToPageDeligate_=navPage;
			}
			_formWikiSearch.Show();
			if(_formWikiSearch.WindowState==FormWindowState.Minimized) {//only applicable if re-using an existing instance
				_formWikiSearch.WindowState=FormWindowState.Normal;
			}
			_formWikiSearch.BringToFront();
		}

		private void navPage(string pageTitle) {
			if(String.IsNullOrEmpty(pageTitle)) {
				return;
			}
			if(this==null || this.IsDisposed) {//when used as a deligate.
				return;
			}
			_idxHistoryNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history
			LoadWikiPage(pageTitle);
		}

		///<summary>Splits a link the user clicked in a wiki page, as the browser reports it, into what it points to (the return value) and the target:
		///the page title of "wiki:Page Title" (no-break spaces become spaces), the path of "wikifile:" and "folder:" (with backslashes), the A to Z
		///path of "wikifilecloud:" and "foldercloud:" (the program's own schemes, which MarkupEdit writes), or an http, https or mailto address (Web).
		///% escapes are undone. Anything else is None, and the click does nothing: the wiki never shows another page, a local file or a remote site
		///itself. In-page bookmarks never get here (the browser scrolls to them).</summary>
		public static EnumWikiLink ParseLink(string uri,out string target) {
			target="";
			if(string.IsNullOrEmpty(uri)) {
				return EnumWikiLink.None;
			}
			string title=WebView2Policy.GetSchemeTarget(uri,"wiki");
			if(title!=null) {
				//It is invalid to have more than one space in a row in URLs, so MarkupEdit's links can hold no-break spaces. Page titles have spaces.
				target=Regex.Replace(title,@" "," ");
				return EnumWikiLink.WikiPage;
			}
			if(TryGetLinkTarget(uri,"wikifile:",out target)) {
				target=target.Replace("/","\\");
				return EnumWikiLink.File;
			}
			if(TryGetLinkTarget(uri,"folder:",out target)) {
				target=target.Replace("/","\\");
				return EnumWikiLink.Folder;
			}
			if(TryGetLinkTarget(uri,"wikifilecloud:",out target)) {
				return EnumWikiLink.FileCloud;
			}
			if(TryGetLinkTarget(uri,"foldercloud:",out target)) {
				return EnumWikiLink.FolderCloud;
			}
			if(WebView2Policy.IsSystemBrowserUrl(uri)) {
				target=uri;
				return EnumWikiLink.Web;
			}
			return EnumWikiLink.None;
		}

		///<summary>The text after the last scheme ("wikifile:", for example) in uri, with % escapes undone. False when uri holds no such scheme.</summary>
		private static bool TryGetLinkTarget(string uri,string scheme,out string target) {
			target="";
			int idx=uri.LastIndexOf(scheme,StringComparison.OrdinalIgnoreCase);
			if(idx<0) {
				return false;
			}
			target=Uri.UnescapeDataString(uri.Substring(idx+scheme.Length));
			return true;
		}

		///<summary>A link the user clicked in the wiki page (the browser never follows it): a wiki page loads here, or in a new wiki window for
		///Ctrl+click and "Open link in new window"; files and folders open with Windows; web addresses open in the default browser.</summary>
		private void webViewWiki_LinkClicked(object sender,ODWebView2LinkEventArgs e) {
			string target;
			switch(ParseLink(e.Uri,out target)) {
				case EnumWikiLink.WikiPage:
					NavigateToWiki("wiki:"+target,isNewWindow:e.IsNewWindow);
					return;
				case EnumWikiLink.File:
					if(!File.Exists(target)) {
						MessageBox.Show(Lan.g(this,"File does not exist: ")+target);
						return;
					}
					ODException.SwallowAnyException(() => System.Diagnostics.Process.Start(target));
					return;
				case EnumWikiLink.Folder:
					if(!Directory.Exists(target)) {
						MessageBox.Show(Lan.g(this,"Folder does not exist: ")+target);
						return;
					}
					ODException.SwallowAnyException(() => System.Diagnostics.Process.Start(target));
					return;
				case EnumWikiLink.FileCloud:
					if(!FileAtoZ.Exists(target)) {
						MessageBox.Show(Lan.g(this,"File does not exist: ")+target);
						return;
					}
					ODException.SwallowAnyException(() => FileAtoZ.StartProcess(target));
					return;
				case EnumWikiLink.FolderCloud:
					if(!FileAtoZ.DirectoryExists(target)) {
						MessageBox.Show(Lan.g(this,"Folder does not exist: ")+target);
						return;
					}
					ODException.SwallowAnyException(() => FileAtoZ.OpenDirectory(target));
					return;
				case EnumWikiLink.Web://navigating outside of wiki by clicking a link
					WebView2Policy.OpenInSystemBrowser(target);
					return;
			}
		}

		private bool HasExistingWikiPage(WikiPage wikiPage,out WikiPage wikiPageExisting) {
			wikiPageExisting=null;
			if(wikiPage==null) {
				return false;//This shouldn't happen.
			}
			wikiPageExisting=WikiPages.GetByTitle(wikiPage.PageTitle);
			if(wikiPageExisting==null) {
				return false;
			}
			return true;
		}

		private void NavigateToWiki(string wikiAddress,bool isNewWindow) {
			string pageTitle=Regex.Replace(wikiAddress,@"\u00A0"," ").Substring(5);
			WikiPage wikiPageDeleted=WikiPages.GetByTitle(pageTitle,isDeleted:true);//Should most likely be null.
			WikiPage wikiPageExisting;
			if(wikiPageDeleted!=null && HasExistingWikiPage(wikiPageDeleted,out wikiPageExisting)) {
				//Now replace any references to wikiPageDeleted with the non deleted wp(wpExisting).
				WikiPages.UpdateWikiPageReferences(WikiPageCur.WikiPageNum,wikiPageDeleted.WikiPageNum,wikiPageExisting.WikiPageNum);
				WikiStaticExport.ExportInBackground(new List<string>() { WikiPageCur.PageTitle },isFullExport:false);
				//Continue to load the page.
			}
			else if(wikiPageDeleted!=null) {
				if(MessageBox.Show(Lan.g(this,"WikiPage '")+pageTitle+Lan.g(this,"' is currently archived. Would you like to restore it?"),
						"",MessageBoxButtons.OKCancel)!=DialogResult.OK) 
				{
					return;
				}
				//User wants to restore the WikiPage.
				WikiPages.WikiPageRestore(wikiPageDeleted,Security.CurUser.UserNum);
				WikiStaticExport.ExportInBackground(null,isFullExport:true);//Links to the restored page work again.
			}
			//Open wiki in a new window.
			bool isCtrlDown=(Control.ModifierKeys & Keys.Control)==Keys.Control;
			if (isNewWindow || isCtrlDown) {
				WikiPages.NavPageDelegate(pageTitle);
			}
			else {
				_idxHistoryNavBack--;//We have to decrement historyNavBack to tell whether or not we need to branch our page history or add to page history
				LoadWikiPage(pageTitle);
			}
		}

		private void FormWiki_ResizeEnd(object sender,EventArgs e) {
			Rectangle rectangleScreen=System.Windows.Forms.Screen.GetWorkingArea(this);
			if(Height>rectangleScreen.Height+(int)LayoutManager.ScaleMS(8)){
				//8 is for the transparent pixels at bottom
				Height=rectangleScreen.Height+(int)LayoutManager.ScaleMS(8);
				Top=rectangleScreen.Top;
			}
		}

		private void FormWiki_FormClosing(object sender,FormClosingEventArgs e) {
			if(_formWikiSearch==null || _formWikiSearch.IsDisposed) {//Close any delinquent search window that may be open.
				return;
			}
			_formWikiSearch.Close();
			_formWikiSearch.Dispose();
		}
	}
}