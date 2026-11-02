using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

//This class wraps webview2 in order for the use to be easier, hides all of the setup (WebView2Policy has the rules it follows).
//This can throw exceptions if WebView2 runtime is not installed, needs to be wrapped in a try-catch for this reason.
namespace CodeBase.Controls {
	public partial class ODWebView2:WebView2 {
		/// <summary>Prevents WebView2 from navigating. Determines what value _doBlockNavigation is set to after navigating.</summary>
		public bool DoBlockNavigation=false;
		/// <summary>Prevents navigation. Altered by OdWebView2Navigate and CoreWebView2_NavigationCompleted to allow navigation via OdWebView2Navigate.</summary>
		private bool _doBlockNavigation=false;
		///<summary>Null until SetScriptEnabled is called, which leaves WebView2's own default (scripts allowed).</summary>
		private bool? _isScriptEnabled=null;
		///<summary>The file of the page that NavigateToHtml shows, or null when the control shows anything else. While it is set the control stays on
		///that page: every other navigation and every new window is cancelled, and a link the user clicks raises LinkClicked instead.</summary>
		private string _pathDocument=null;
		///<summary>The file: address of _pathDocument.</summary>
		private string _uriDocument=null;
		///<summary>The html that NavigateToHtml received before the control was initialized, shown as soon as it is. Only the last one counts.</summary>
		private string _htmlPending=null;
		///<summary>The folder that goes with _htmlPending.</summary>
		private string _folderPending=null;
		///<summary>True once NavigateToHtml has started the initialization itself, so it starts only once.</summary>
		private bool _isInitStarted=false;

		///<summary>Raised when the user clicks a link (or opens one in a new window) in a page shown with NavigateToHtml. The navigation is always
		///cancelled; the window decides what the link does, and a window without a handler ignores links. Never raised for a navigation the page
		///starts by itself (a meta refresh, for example), so a received email cannot open anything.</summary>
		public event EventHandler<ODWebView2LinkEventArgs> LinkClicked;
		///<summary>Raised when StatusText changes, for example when the mouse moves over a link.</summary>
		public event EventHandler StatusTextChanged;

		///<summary>False removes the right-click menu from the pages shown with NavigateToHtml. When true, those pages keep only Copy, Select all,
		///the link and image copy commands and "Open link in new window" (WebView2Policy).</summary>
		[DefaultValue(true)]
		[Category("OD")]
		[Description("False removes the right-click menu from the pages shown with NavigateToHtml.")]
		public bool IsContextMenuEnabled { get; set; }=true;

		///<summary>The text WebView2 would show in its status bar: the address of the link under the mouse. Empty before initialization.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string StatusText {
			get {
				return CoreWebView2?.StatusBarText??"";
			}
		}

		///<summary>The file: address of the page NavigateToHtml shows, or null.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string DocumentUri {
			get {
				return _uriDocument;
			}
		}

		public ODWebView2() {
			InitializeComponent();
		}

		///<summary>True if the Microsoft WebView2 Runtime is installed on this computer. Shows nothing and never throws, so a caller can choose a
		///fallback before calling Init (which tells the user how to get the runtime and then throws).</summary>
		public static bool IsRuntimeAvailable() {
			try {
				return !CoreWebView2Environment.GetAvailableBrowserVersionString().IsNullOrEmpty();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>The WebView2 environment of the program: its user data folder is %LOCALAPPDATA%\OpenPMS-Dental\WebView2 for the current Windows
		///user (WebView2Policy.GetUserDataFolder), never the installation folder.</summary>
		public static Task<CoreWebView2Environment> CreateEnvironmentAsync() {
			return CoreWebView2Environment.CreateAsync(null,WebView2Policy.GetUserDataFolder());
		}

		public Task Init() {
			return Init(null);
		}

		///<summary>Like Init(), but first lets the caller set the controller options, which are created from this control's environment. For example,
		///x => x.IsInPrivateModeEnabled=true gives an InPrivate profile, so no cookies, history or form data persist in the shared user data folder.</summary>
		public async Task Init(Action<CoreWebView2ControllerOptions> actionSetControllerOptions) {
			bool doShowError=false;
			try {
				if(CoreWebView2Environment.GetAvailableBrowserVersionString().IsNullOrEmpty()) {
					doShowError=true;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
				doShowError=true;
			}
			if(doShowError) {
				//The program never downloads or installs the runtime itself: an administrator installs it (Windows 11 includes it).
				string warning="The Microsoft Edge WebView2 Runtime is not installed on this computer, so this window cannot show its page.\r\n"
					+"Windows 11 includes it. On other computers an administrator can install it from Microsoft: "+WebView2Policy.RuntimeDownloadUrl;
				if(ODBuild.IsDebug()) {
					warning+="\r\nIf you are in debug, make sure you are running in x86 config instead of Any CPU. Also, make sure RequiredDlls\\WebView2Loader.dll has been copied to your bin folder.";
				}
				MessageBox.Show(warning,"Error");
				throw new ODException();
			}
			CoreWebView2Environment environment=await CreateEnvironmentAsync();
			if(actionSetControllerOptions==null) {
				await this.EnsureCoreWebView2Async(environment);
				return;
			}
			CoreWebView2ControllerOptions coreWebView2ControllerOptions=environment.CreateCoreWebView2ControllerOptions();
			actionSetControllerOptions(coreWebView2ControllerOptions);
			await this.EnsureCoreWebView2Async(environment,coreWebView2ControllerOptions);
		}

		/// <summary>
		/// Runs after webView2 has initialized, before navigating, disable accelerator key combos for the browser.
		/// Disables the following browser features:
		/// Find: Ctrl+f and F3,
		/// Print: Ctrl+p,
		/// Refresh: Ctrl+r and F5,
		/// Zoom: Ctrl+Plus/Minus,
		/// Dev Tools: Ctrl+Shift+C and F12,
		/// Special browser keys
		/// Turns off host objects, and dev tools outside Debug builds (WebView2Policy.ApplySecureSettings).
		/// </summary>
		public void CoreWebView2_InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e) {
			if(!e.IsSuccess || this.CoreWebView2==null) {
				return;
			}
			this.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=false;
			WebView2Policy.ApplySecureSettings(this.CoreWebView2.Settings);
			this.CoreWebView2.NewWindowRequested+=CoreWebView2_NewWindowRequested;
			this.CoreWebView2.FrameNavigationStarting+=CoreWebView2_FrameNavigationStarting;
			this.CoreWebView2.LaunchingExternalUriScheme+=CoreWebView2_LaunchingExternalUriScheme;
			this.CoreWebView2.DownloadStarting+=CoreWebView2_DownloadStarting;
			this.CoreWebView2.PermissionRequested+=CoreWebView2_PermissionRequested;
			this.CoreWebView2.ContextMenuRequested+=CoreWebView2_ContextMenuRequested;
			this.CoreWebView2.StatusBarTextChanged+=CoreWebView2_StatusBarTextChanged;
			if(_isScriptEnabled!=null) {
				this.CoreWebView2.Settings.IsScriptEnabled=_isScriptEnabled.Value;
			}
			ShowPendingDocument();
		}

		///<summary>Sets whether scripts may run in what this control shows, from its next navigation on. This includes the JavaScript inside PDFs:
		///the built-in PDF viewer follows CoreWebView2Settings.IsScriptEnabled (checked with WebView2 Runtime 155, see tests/M2IMGTests), while the
		///viewer itself keeps working. Can be called before Init; the value is then applied when initialization completes.</summary>
		public void SetScriptEnabled(bool isScriptEnabled) {
			_isScriptEnabled=isScriptEnabled;
			if(this.CoreWebView2!=null) {
				this.CoreWebView2.Settings.IsScriptEnabled=isScriptEnabled;
			}
		}

		///<summary>On attempting to navigate, if DoBlockNavigation is true, cancel the event. A page shown with NavigateToHtml stays: anything else is
		///cancelled, and a link the user clicked raises LinkClicked.</summary>
		public void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e) {
			if(_uriDocument!=null) {
				if(WebView2Policy.IsSameDocument(e.Uri,_uriDocument)) {
					return;
				}
				e.Cancel=true;
				if(e.IsUserInitiated) {
					RaiseLinkClicked(e.Uri,isNewWindow:false);
				}
				return;
			}
			if(_doBlockNavigation) {
				e.Cancel=true;
			}
		}

		public void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
			_doBlockNavigation=DoBlockNavigation;
		}

		public void CoreWebView2_NewWindowRequested(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
			if(_uriDocument!=null) {
				//Never a second browser window from a page the program shows. Handled without NewWindow set means nothing opens.
				e.Handled=true;
				if(e.IsUserInitiated) {
					RaiseLinkClicked(e.Uri,isNewWindow:true);
				}
				return;
			}
			if(_doBlockNavigation) {
				//Stop the default Microsoft Edge add-on from launching a new window when a user tries to open hyperlink
				e.Handled=true;
			}
		}

		///<summary>A frame inside a page the program shows never loads anything but about:blank (an email cannot pull a remote page into itself).</summary>
		private void CoreWebView2_FrameNavigationStarting(object sender,CoreWebView2NavigationStartingEventArgs e) {
			if(_uriDocument!=null && !WebView2Policy.IsAllowedFrameUri(e.Uri)) {
				e.Cancel=true;
			}
		}

		///<summary>A page the program shows never starts another program through its protocol (a mailto link goes through LinkClicked).</summary>
		private void CoreWebView2_LaunchingExternalUriScheme(object sender,CoreWebView2LaunchingExternalUriSchemeEventArgs e) {
			if(_uriDocument==null) {
				return;
			}
			e.Cancel=true;
			if(e.IsUserInitiated) {
				RaiseLinkClicked(e.Uri,isNewWindow:false);
			}
		}

		private void CoreWebView2_DownloadStarting(object sender,CoreWebView2DownloadStartingEventArgs e) {
			if(_uriDocument!=null) {
				e.Cancel=true;
			}
		}

		private void CoreWebView2_PermissionRequested(object sender,CoreWebView2PermissionRequestedEventArgs e) {
			if(_uriDocument!=null) {
				e.State=CoreWebView2PermissionState.Deny;
			}
		}

		private void CoreWebView2_ContextMenuRequested(object sender,CoreWebView2ContextMenuRequestedEventArgs e) {
			if(_uriDocument==null) {
				return;
			}
			WebView2Policy.KeepContextMenuItems(e.MenuItems);
			if(e.MenuItems.Count==0) {
				e.Handled=true;//Nothing left to show.
			}
		}

		private void CoreWebView2_StatusBarTextChanged(object sender,object e) {
			StatusTextChanged?.Invoke(this,EventArgs.Empty);
		}

		///<summary>Raises LinkClicked after the browser event returns, so a handler may show a message box or open another window.</summary>
		private void RaiseLinkClicked(string uri,bool isNewWindow) {
			if(LinkClicked==null || IsDisposed) {
				return;
			}
			ODWebView2LinkEventArgs odWebView2LinkEventArgs=new ODWebView2LinkEventArgs(uri,_uriDocument,isNewWindow);
			BeginInvoke((Action)(() => {
				if(!IsDisposed) {
					LinkClicked?.Invoke(this,odWebView2LinkEventArgs);
				}
			}));
		}

		///<summary>This method will always be allowed to navigate.</summary>
		public async void ODWebView2Navigate(string path) {
			ClearDocument();
			_doBlockNavigation=false;
			CoreWebView2.Navigate(path);
		}

		///<summary>Shows html that the program generated or received (a wiki page, an email, a report): writes it to a new file and shows that file.
		///A file page can show the local images such html refers to (file paths), which a page from a string cannot. The file goes into folder, or
		///into the program's temp folder when folder is null (give a folder that already holds the images a relative path names). While the page is
		///shown, scripts are off and the control stays on it: links raise LinkClicked. Initializes the control first when needed (the user is told
		///if the WebView2 Runtime is missing); the page then shows when initialization completes. Never throws for a missing runtime.</summary>
		public void NavigateToHtml(string html,string folder=null) {
			if(IsDisposed) {
				return;
			}
			if(CoreWebView2==null) {
				_htmlPending=html??"";
				_folderPending=folder;
				InitForDocument();
				return;
			}
			ShowDocument(html??"",folder);
		}

		///<summary>Starts the initialization for NavigateToHtml, once. The pending page shows when initialization completes
		///(CoreWebView2_InitializationCompleted), whoever started it.</summary>
		private async void InitForDocument() {
			if(_isInitStarted) {
				return;
			}
			_isInitStarted=true;
			try {
				await Init();
			}
			catch(Exception ex) {
				ex.DoNothing();//Init already told the user what is missing, or another caller is initializing the control.
				return;
			}
			ShowPendingDocument();
		}

		private void ShowPendingDocument() {
			if(IsDisposed || CoreWebView2==null || _htmlPending==null) {
				return;
			}
			string html=_htmlPending;
			string folder=_folderPending;
			_htmlPending=null;
			_folderPending=null;
			ShowDocument(html,folder);
		}

		private void ShowDocument(string html,string folder) {
			string pathOld=_pathDocument;
			if(string.IsNullOrEmpty(folder)) {
				folder=WebView2Policy.GetPageFolder();
			}
			string path=ODFileUtils.CreateRandomFile(folder,".html");
			//UTF-8 with a byte order mark, so the page reads as UTF-8 whatever its meta tags say.
			File.WriteAllText(path,html,new UTF8Encoding(true));
			_pathDocument=path;
			_uriDocument=new Uri(path).AbsoluteUri;
			_doBlockNavigation=false;
			WebView2Policy.ApplyDocumentSettings(CoreWebView2.Settings,IsContextMenuEnabled);
			CoreWebView2.Navigate(_uriDocument);
			DeleteFile(pathOld);
		}

		///<summary>Leaves the page of NavigateToHtml (its file is deleted) before the control shows something else.</summary>
		private void ClearDocument() {
			string pathOld=_pathDocument;
			_pathDocument=null;
			_uriDocument=null;
			_htmlPending=null;
			_folderPending=null;
			DeleteFile(pathOld);
		}

		private static void DeleteFile(string path) {
			if(string.IsNullOrEmpty(path)) {
				return;
			}
			ODException.SwallowAnyException(() => File.Delete(path));
		}

		///<summary>Sets a session cookie for url's host in the browser's cookie store, as InternetSetCookie did for Internet Explorer's control.
		///cookieData is "name=value" (spaces around either are ignored, attributes after a ';' are dropped). Returns false, and sets nothing, before
		///initialization or when url or cookieData cannot be used.</summary>
		public bool SetCookie(string url,string cookieData) {
			if(CoreWebView2==null) {
				return false;
			}
			Uri uri;
			if(!Uri.TryCreate(url??"",UriKind.Absolute,out uri) || uri.Host=="") {
				return false;
			}
			string name;
			string value;
			if(!WebView2Policy.TryParseCookie(cookieData,out name,out value)) {
				return false;
			}
			CoreWebView2Cookie coreWebView2Cookie=CoreWebView2.CookieManager.CreateCookie(name,value,uri.Host,"/");
			CoreWebView2.CookieManager.AddOrUpdateCookie(coreWebView2Cookie);
			return true;
		}

		///<summary>Shows Windows' print dialog for what the control shows (WebView2's print API). Does nothing before initialization.</summary>
		public void ShowPrintDialog() {
			if(CoreWebView2==null) {
				return;
			}
			CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
		}

		///<summary>The scroll position of the page from its top, in CSS pixels. 0 before initialization or when it cannot be read. Works while the
		///page's own scripts are off: scripts the program runs always run.</summary>
		public async Task<int> GetScrollTopAsync() {
			if(CoreWebView2==null) {
				return 0;
			}
			try {
				string result=await CoreWebView2.ExecuteScriptAsync("Math.round(document.documentElement.scrollTop||document.body.scrollTop||0)");
				int scrollTop;
				if(int.TryParse(result,out scrollTop)) {
					return scrollTop;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return 0;
		}

		///<summary>Scrolls the page to scrollTop CSS pixels from its top. Call it after NavigationCompleted.</summary>
		public void SetScrollTop(int scrollTop) {
			if(CoreWebView2==null) {
				return;
			}
			_=CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollTop="+scrollTop+";document.body.scrollTop="+scrollTop+";");
		}

		///<summary>Sets the CSS zoom of the page's body, in percent (the old browser control's body style "zoom"). Call it after NavigationCompleted.</summary>
		public void SetBodyZoom(int percent) {
			if(CoreWebView2==null) {
				return;
			}
			_=CoreWebView2.ExecuteScriptAsync("if(document.body){document.body.style.zoom='"+percent+"%';}");
		}

		public void ClearCache(bool doDisplayError=true) {
			//Fire and forget.
			_=ClearCache();
			if(doDisplayError) {
				MessageBox.Show("Browser cache cleared. You may need to close or refresh this window. If you are still experiencing problems, run the program as an Admin and try again.");
			}
		}

		///<summary>Makes a call to dev tools to clear WebView2 browser cache. Use a fire and forget pattern as you shouldn't care about the result of this method.</summary>
		private async Task ClearCache() {
			await this.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.clearBrowserCache","{}");
		}
	}
}
