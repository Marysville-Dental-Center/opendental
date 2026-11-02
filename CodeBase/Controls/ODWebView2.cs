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
		///<summary>The page that NavigateToHtml shows, in memory (WebView2PageStore), or null when the control shows anything else. While it is set
		///the control stays on that page: every other navigation and every new window is cancelled, and a link the user clicks raises LinkClicked
		///instead.</summary>
		private WebView2Page _page=null;
		///<summary>The file: address of _page, which names no file on disk: the control answers it, and the files of the page, from memory.</summary>
		private string _uriDocument=null;
		///<summary>The file: address (without a #fragment) of every page this control showed for NavigateToHtml. A navigation to one of them that is
		///not the current page is cancelled without raising LinkClicked: the earlier of two pages shown in quick succession, whose own navigation
		///starts after the second page took its place, or a page that Back or Forward would show again.</summary>
		private HashSet<string> _hashSetUrisPage=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		///<summary>True when the current page of NavigateToHtml may load http and https images, style sheets and fonts.</summary>
		private bool _isRemoteContentAllowed=false;
		///<summary>The local folders whose files the current page of NavigateToHtml may load, besides the files in its own folder.</summary>
		private List<string> _listFoldersAllowed=new List<string>();
		///<summary>True while the WebResourceRequested filter of NavigateToHtml's pages is in place.</summary>
		private bool _isResourceFilterAdded=false;
		///<summary>True once RemoteContentBlocked was raised for the current page, so it is raised once per page.</summary>
		private bool _isRemoteContentBlockedRaised=false;
		///<summary>The page that NavigateToHtml received before the control was initialized, shown as soon as it is. Only the last one counts.</summary>
		private PagePending _pagePending=null;
		///<summary>True once NavigateToHtml has started the initialization itself, so it starts only once.</summary>
		private bool _isInitStarted=false;
		///<summary>Backs AreBrowserKeysEnabled.</summary>
		private bool _areBrowserKeysEnabled=false;
		///<summary>The start failure this control last reported (Init and the initialization's event both see the same exception), so it is logged once.</summary>
		private Exception _exceptionStartReported=null;
		///<summary>True once this control told the user that the runtime would not start, so Init and the initialization's event, which both report
		///it, count it as told.</summary>
		private bool _isStartFailureTold=false;
		///<summary>True once a WebView2 Runtime that would not start was shown to the user in this program session, so the message shows once: the
		///inbox's two previews and every wiki window would otherwise each show it. Locked by _lockStartFailure.</summary>
		private static bool _isStartFailureShown=false;
		private static object _lockStartFailure=new object();
		///<summary>Whether the WebView2 Runtime is installed, as Init checks it (IsRuntimeAvailable). LibWebTests replaces it.</summary>
		internal static Func<bool> FuncIsRuntimeAvailable=IsRuntimeAvailable;
		///<summary>Creates the environment Init uses (CreateEnvironmentAsync). LibWebTests replaces it with one that fails.</summary>
		internal static Func<Task<CoreWebView2Environment>> FuncCreateEnvironment=CreateEnvironmentAsync;
		///<summary>Shows Init's messages to the user (a message box). LibWebTests replaces it, so no window shows.</summary>
		internal static Action<string> ActionShowMessage=x => MessageBox.Show(x,"Error");

		///<summary>Raised when the user clicks a link (or opens one in a new window) in a page shown with NavigateToHtml. The navigation is always
		///cancelled; the window decides what the link does, and a window without a handler ignores links. Never raised for a navigation the page
		///starts by itself (a meta refresh, for example), so a received email cannot open anything, nor for Back, Forward or Reload.</summary>
		public event EventHandler<ODWebView2LinkEventArgs> LinkClicked;
		///<summary>Raised when StatusText changes, for example when the mouse moves over a link.</summary>
		public event EventHandler StatusTextChanged;
		///<summary>Raised once per page shown with NavigateToHtml when the page asked for an http or https image, style sheet or font that it may
		///not load (NavigateToHtml's isRemoteContentAllowed was false). A window can then offer to show the page with them.</summary>
		public event EventHandler RemoteContentBlocked;

		///<summary>False removes the right-click menu from the pages shown with NavigateToHtml. When true, those pages keep only Copy, Select all,
		///the link and image copy commands and "Open link in new window" (WebView2Policy).</summary>
		[DefaultValue(true)]
		[Category("OD")]
		[Description("False removes the right-click menu from the pages shown with NavigateToHtml.")]
		public bool IsContextMenuEnabled { get; set; }=true;

		///<summary>True keeps the browser's own keys, as the old browser control had them: Ctrl+F and F3 (find in the page), Ctrl+P (print),
		///Ctrl+R and F5 (reload), Ctrl+Plus and Ctrl+Minus (zoom), Alt+Left and Alt+Right (back and forward). False, the default, turns them off.
		///The dev tools stay off in Release builds either way (WebView2Policy.ApplySecureSettings). A page shown with NavigateToHtml stays with
		///either value: a reload shows it again, and back and forward are cancelled.</summary>
		[DefaultValue(false)]
		[Category("OD")]
		[Description("True keeps the browser's own keys, such as Ctrl+F to find text in the page.")]
		public bool AreBrowserKeysEnabled {
			get {
				return _areBrowserKeysEnabled;
			}
			set {
				_areBrowserKeysEnabled=value;
				if(this.CoreWebView2!=null) {
					this.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=value;
				}
			}
		}

		///<summary>The text WebView2 would show in its status bar: the address of the link under the mouse. Empty before initialization.</summary>
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string StatusText {
			get {
				return CoreWebView2?.StatusBarText??"";
			}
		}

		///<summary>The file: address of the page NavigateToHtml shows, or null. The page is in memory: no file is at that address.</summary>
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
		///x => x.IsInPrivateModeEnabled=true gives an InPrivate profile, so no cookies, history or form data persist in the shared user data folder.
		///Throws ODWebView2InitException (an ODException) when the runtime is missing, after telling the user, or when it would not start, after
		///telling the user once per program session (ReportStartFailure); IsUserTold says whether this call told the user. A window that shows its
		///own message when Init fails gives the exception to TryShowInitFailure first, so the user sees one message, never two.</summary>
		public async Task Init(Action<CoreWebView2ControllerOptions> actionSetControllerOptions) {
			if(!FuncIsRuntimeAvailable()) {
				//The program never downloads or installs the runtime itself: an administrator installs it (Windows 11 includes it).
				string warning=GetMissingRuntimeMessage();
				ActionShowMessage(warning);
				throw new ODWebView2InitException(warning,null,isUserTold:true,isClosed:false);
			}
			try {
				CoreWebView2Environment environment=await FuncCreateEnvironment();
				if(actionSetControllerOptions==null) {
					await this.EnsureCoreWebView2Async(environment);
					return;
				}
				CoreWebView2ControllerOptions coreWebView2ControllerOptions=environment.CreateCoreWebView2ControllerOptions();
				actionSetControllerOptions(coreWebView2ControllerOptions);
				await this.EnsureCoreWebView2Async(environment,coreWebView2ControllerOptions);
			}
			catch(Exception ex) {
				//The runtime is there but would not start: damaged, blocked (antivirus, AppLocker), or the profile folder cannot be used.
				bool isClosed=IsDisposed || Disposing || IsCancelledByClose(ex);
				bool isUserTold=ReportStartFailure(ex);
				throw new ODWebView2InitException(ex.Message,ex,isUserTold,isClosed);
			}
		}

		///<summary>The message for a WebView2 Runtime that is not installed: Windows 11 includes it, and where an administrator gets it.</summary>
		internal static string GetMissingRuntimeMessage() {
			string warning="The Microsoft Edge WebView2 Runtime is not installed on this computer, so this window cannot show its page.\r\n"
				+"Windows 11 includes it. On other computers an administrator can install it from Microsoft: "+WebView2Policy.RuntimeDownloadUrl;
			if(ODBuild.IsDebug()) {
				warning+="\r\nIf you are in debug, make sure you are running in x86 config instead of Any CPU. Also, make sure RequiredDlls\\WebView2Loader.dll has been copied to your bin folder.";
			}
			return warning;
		}

		///<summary>Logs a WebView2 Runtime that is installed but would not start, and tells the user once per program session what an administrator
		///can do about it. Nothing is reported for a control that was closed while it started (the window closed), which is no failure. Returns
		///true when this control told the user about it, now or before (Init and the initialization's event both report the same failure).</summary>
		private bool ReportStartFailure(Exception ex) {
			if(ex==null || IsDisposed || Disposing || IsCancelledByClose(ex)) {
				return false;
			}
			if(ReferenceEquals(ex,_exceptionStartReported)) {
				return _isStartFailureTold;
			}
			_exceptionStartReported=ex;
			ODException.SwallowAnyException(() => Logger.WriteException(ex,"WebView2"));
			if(_isStartFailureTold) {
				return true;
			}
			lock(_lockStartFailure) {
				if(_isStartFailureShown) {
					return false;
				}
				_isStartFailureShown=true;
			}
			ActionShowMessage(GetStartFailureMessage(ex));
			_isStartFailureTold=true;
			return true;
		}

		///<summary>For a window that cannot work without its browser and shows its own error when the browser's setup fails (FormCareCreditWeb,
		///FormPayConnect2, FormPayConnect2iFrame, the Images module's PDF print): true when ex is Init's failure (ODWebView2InitException), and the
		///user has then seen exactly one message about it: Init's own, or, when Init held its message back because the runtime's start failure was
		///already shown in this program session, the same message again now (the user opened this window on purpose, and it cannot work). Nothing
		///shows for a window that was closed while its browser started. False for any other exception, which the window shows itself.</summary>
		public static bool TryShowInitFailure(Exception ex) {
			ODWebView2InitException odWebView2InitException=ex as ODWebView2InitException;
			if(odWebView2InitException==null) {
				return false;
			}
			if(odWebView2InitException.IsUserTold || odWebView2InitException.IsClosed) {
				return true;
			}
			ActionShowMessage(GetStartFailureMessage(odWebView2InitException.InnerException??odWebView2InitException,isOncePerSession:false));
			return true;
		}

		///<summary>True for the error WebView2 gives when the control's window goes away while it starts (E_ABORT, or a cancellation).</summary>
		private static bool IsCancelledByClose(Exception ex) {
			return ex is OperationCanceledException || ex is ObjectDisposedException || ex.HResult==unchecked((int)0x80004004);
		}

		///<summary>The message for a WebView2 Runtime that would not start: the error and its HRESULT, and what an administrator can check. With
		///isOncePerSession it ends by saying that it shows once per program session (Init's message); without it (TryShowInitFailure) it does not.</summary>
		internal static string GetStartFailureMessage(Exception ex,bool isOncePerSession=true) {
			string folder;
			try {
				folder=WebView2Policy.GetUserDataFolder();
			}
			catch(Exception exFolder) {
				exFolder.DoNothing();
				folder="%LOCALAPPDATA%\\"+ProgramNames.LocalAppDataFolderName+"\\"+WebView2Policy.UserDataFolderName;
			}
			string message="The Microsoft Edge WebView2 Runtime could not start, so the program cannot show web pages, emails or wiki pages.\r\n"
				+"Error: "+ex.Message+" (HRESULT 0x"+ex.HResult.ToString("X8")+")\r\n\r\n"
				+"An administrator can:\r\n"
				+"- repair the runtime in Windows Settings, Apps and features (Microsoft Edge WebView2 Runtime, Modify, Repair), or reinstall it from "
				+"Microsoft: "+WebView2Policy.RuntimeDownloadUrl+"\r\n"
				+"- check that antivirus software or AppLocker does not block msedgewebview2.exe\r\n"
				+"- check that this Windows user can write to "+folder;
			if(isOncePerSession) {
				message+="\r\n\r\nThis message shows once until the program is started again.";
			}
			return message;
		}

		///<summary>Lets LibWebTests see the once-per-session message again.</summary>
		internal static void ResetStartFailureShown() {
			lock(_lockStartFailure) {
				_isStartFailureShown=false;
			}
		}

		/// <summary>
		/// Runs after webView2 has initialized, before navigating. Unless AreBrowserKeysEnabled is true, disables the accelerator key combos of
		/// the browser, which turns off the following browser features:
		/// Find: Ctrl+f and F3,
		/// Print: Ctrl+p,
		/// Refresh: Ctrl+r and F5,
		/// Zoom: Ctrl+Plus/Minus,
		/// Dev Tools: Ctrl+Shift+C and F12,
		/// Special browser keys
		/// Turns off host objects, and dev tools outside Debug builds (WebView2Policy.ApplySecureSettings).
		/// </summary>
		public void CoreWebView2_InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e) {
			if(!e.IsSuccess) {
				ReportStartFailure(e.InitializationException);
				return;
			}
			if(this.CoreWebView2==null) {
				return;
			}
			this.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=AreBrowserKeysEnabled;
			WebView2Policy.ApplySecureSettings(this.CoreWebView2.Settings);
			this.CoreWebView2.NewWindowRequested+=CoreWebView2_NewWindowRequested;
			this.CoreWebView2.FrameNavigationStarting+=CoreWebView2_FrameNavigationStarting;
			this.CoreWebView2.PermissionRequested+=CoreWebView2_PermissionRequested;
			this.CoreWebView2.WebResourceRequested+=CoreWebView2_WebResourceRequested;
			//Events of newer runtimes: an older runtime that lacks one throws here, and the control still works without it.
			ODException.SwallowAnyException(() => this.CoreWebView2.DownloadStarting+=CoreWebView2_DownloadStarting);
			ODException.SwallowAnyException(() => this.CoreWebView2.ContextMenuRequested+=CoreWebView2_ContextMenuRequested);
			ODException.SwallowAnyException(() => this.CoreWebView2.StatusBarTextChanged+=CoreWebView2_StatusBarTextChanged);
			ODException.SwallowAnyException(() => this.CoreWebView2.LaunchingExternalUriScheme+=CoreWebView2_LaunchingExternalUriScheme);
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
		///cancelled, and a link the user clicked raises LinkClicked. WebView2 counts its own Navigate and GoBack calls, Back and Forward (mouse
		///buttons, keys) and reloads as user initiated too, so a navigation to a page file of this control, or one that is not to a new document,
		///is cancelled without it.</summary>
		public void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e) {
			if(_uriDocument!=null) {
				if(WebView2Policy.IsSameDocument(e.Uri,_uriDocument)) {
					return;
				}
				e.Cancel=true;
				if(IsPageUri(e.Uri)) {
					return;//An earlier page of this control, never a link.
				}
				if(e.IsUserInitiated && IsNewDocument(e)) {
					RaiseLinkClicked(e.Uri,isNewWindow:false);
				}
				return;
			}
			if(_doBlockNavigation) {
				e.Cancel=true;
			}
		}

		///<summary>True when uri is a page this control wrote for NavigateToHtml, the current one included, ignoring a #fragment.</summary>
		private bool IsPageUri(string uri) {
			if(string.IsNullOrEmpty(uri)) {
				return false;
			}
			return _hashSetUrisPage.Contains(WebView2Policy.RemoveFragment(uri));
		}

		///<summary>False for Back, Forward and Reload, which are never a click on a link. True for a new document, and when the runtime is too old to
		///tell (NavigationKind came with SDK 1.0.1901; an older runtime throws for it).</summary>
		private static bool IsNewDocument(CoreWebView2NavigationStartingEventArgs e) {
			try {
				return e.NavigationKind==CoreWebView2NavigationKind.NewDocument;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return true;
			}
		}

		public void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e) {
			_doBlockNavigation=DoBlockNavigation;
		}

		public void CoreWebView2_NewWindowRequested(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
			if(_uriDocument!=null) {
				//Never a second browser window from a page the program shows. Handled without NewWindow set means nothing opens.
				e.Handled=true;
				if(e.IsUserInitiated && !IsPageUri(e.Uri)) {
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

		///<summary>What a page shown with NavigateToHtml may load besides itself (WebView2Policy.IsPageResourceAllowed): files in its own folder and
		///in the folders NavigateToHtml allows, data:, blob: and about: addresses, and http and https only when NavigateToHtml allows them. Anything
		///else gets an empty 403 response, so the request never leaves the computer: an image on another computer's share would otherwise make
		///Windows send the user's credentials there. Until the browser shows the current page, a request can still come from the page before it,
		///so nothing remote loads then. The page itself and the files in its own folder are in memory (WebView2PageStore): every request under the
		///store's folder is answered from memory, with 404 for a file that is not there, and never read from the disk.</summary>
		private void CoreWebView2_WebResourceRequested(object sender,CoreWebView2WebResourceRequestedEventArgs e) {
			if(_uriDocument==null) {
				return;
			}
			string uri="";
			bool isPageShown=false;
			bool isAllowed=false;
			bool isInMemory=false;
			try {
				uri=e.Request.Uri;
				isPageShown=WebView2Policy.IsSameDocument(this.CoreWebView2.Source,_uriDocument);
				isAllowed=WebView2Policy.IsPageResourceAllowed(uri,_uriDocument,_listFoldersAllowed,_isRemoteContentAllowed && isPageShown);
				isInMemory=WebView2PageStore.IsInRoot(uri);
			}
			catch(Exception ex) {
				ex.DoNothing();
				isAllowed=false;
			}
			if(isAllowed && !isInMemory) {
				return;//A file in an allowed folder, the web when allowed, or data:, blob: and about:.
			}
			try {
				if(isAllowed) {
					e.Response=GetMemoryResponse(uri);
					return;
				}
				e.Response=this.CoreWebView2.Environment.CreateWebResourceResponse(null,403,"Blocked","");
			}
			catch(Exception ex) {
				ex.DoNothing();//The control is being disposed: nothing shows the page any more.
				return;
			}
			if(isPageShown && WebView2Policy.IsRemoteUri(uri)) {
				RaiseRemoteContentBlocked();
			}
		}

		///<summary>The response for a file: address under WebView2PageStore's folder: the page or file from memory, or an empty 404 when there is
		///none (a page that was replaced, a name the page does not have).</summary>
		private CoreWebView2WebResourceResponse GetMemoryResponse(string uri) {
			if(!WebView2PageStore.TryGetFile(uri,out byte[] content,out string contentType)) {
				return this.CoreWebView2.Environment.CreateWebResourceResponse(null,404,"Not found",WebView2PageStore.GetResponseHeaders("text/plain"));
			}
			//The browser reads the stream after this handler returns; a MemoryStream over the kept array copies nothing.
			return this.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(content,writable:false),200,"OK",
				WebView2PageStore.GetResponseHeaders(contentType));
		}

		///<summary>Raises RemoteContentBlocked once for the current page, after the browser event returns, and only while that page is still shown.</summary>
		private void RaiseRemoteContentBlocked() {
			if(_isRemoteContentBlockedRaised || RemoteContentBlocked==null || IsDisposed) {
				return;
			}
			_isRemoteContentBlockedRaised=true;
			string uriDocument=_uriDocument;
			BeginInvoke((Action)(() => {
				if(!IsDisposed && _uriDocument==uriDocument) {
					RemoteContentBlocked?.Invoke(this,EventArgs.Empty);
				}
			}));
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
			RemoveResourceFilter();
			_doBlockNavigation=false;
			CoreWebView2.Navigate(path);
		}

		///<summary>Shows html that the program generated or received (a wiki page, an email, a report) from memory: nothing of it is written to disk.
		///The page gets a file: address in a folder of its own that is never created (WebView2PageStore), and the control answers the browser's
		///requests for it, and for the files in its folder, from memory. So the page can show the local images such html refers to (file paths),
		///which a page from a string cannot, and dictFiles gives the files a relative path in the html names (an email's images, by file name).
		///While the page is shown, scripts are off and the control stays on it: links raise LinkClicked. The page loads files only from its own
		///folder (dictFiles) and from listFoldersAllowed (the A to Z folder its images come from, for example), never from another computer's share,
		///and http and https images, style sheets and fonts only when isRemoteContentAllowed is true: leave it false for html from outside the
		///office, such as a received email (RemoteContentBlocked then tells the window that the page wanted some). Initializes the control first
		///when needed (the user is told if the WebView2 Runtime is missing or would not start); the page then shows when initialization completes.
		///Never throws for a missing or failing runtime.</summary>
		public void NavigateToHtml(string html,bool isRemoteContentAllowed=false,List<string> listFoldersAllowed=null,
			IDictionary<string,byte[]> dictFiles=null)
		{
			if(IsDisposed) {
				return;
			}
			PagePending pagePending=new PagePending();
			pagePending.Html=html??"";
			pagePending.DictFiles=dictFiles;
			pagePending.IsRemoteContentAllowed=isRemoteContentAllowed;
			pagePending.ListFoldersAllowed=listFoldersAllowed;
			if(CoreWebView2==null) {
				_pagePending=pagePending;
				InitForDocument();
				return;
			}
			ShowDocument(pagePending);
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
				ex.DoNothing();//Init told the user that the runtime is missing or would not start (once per program session), and logged it.
				return;
			}
			ShowPendingDocument();
		}

		///<summary>Runs from the initialization's event and continuation, where an exception would end the program, so a page that cannot be shown
		///leaves the control blank.</summary>
		private void ShowPendingDocument() {
			if(IsDisposed || CoreWebView2==null || _pagePending==null) {
				return;
			}
			PagePending pagePending=_pagePending;
			_pagePending=null;
			ODException.SwallowAnyException(() => ShowDocument(pagePending));
		}

		private void ShowDocument(PagePending pagePending) {
			//Older versions wrote every page into the temp folder: what one left when it ended without closing goes, once per program session.
			WebView2PageStore.DeleteOldPageFiles();
			WebView2Page pageOld=_page;
			//In memory only. UTF-8 with a byte order mark, so the page reads as UTF-8 whatever its meta tags say.
			_page=WebView2PageStore.AddPage(pagePending.Html,pagePending.DictFiles);
			_uriDocument=_page.Uri;
			_hashSetUrisPage.Add(WebView2Policy.RemoveFragment(_uriDocument));
			_isRemoteContentAllowed=pagePending.IsRemoteContentAllowed;
			_listFoldersAllowed=new List<string>();
			if(pagePending.ListFoldersAllowed!=null) {
				_listFoldersAllowed.AddRange(pagePending.ListFoldersAllowed.FindAll(x => !string.IsNullOrWhiteSpace(x)));
			}
			_isRemoteContentBlockedRaised=false;
			_doBlockNavigation=false;
			WebView2Policy.ApplyDocumentSettings(CoreWebView2.Settings,IsContextMenuEnabled);
			if(!_isResourceFilterAdded) {
				//Every request of every kind, the page's own included, goes through CoreWebView2_WebResourceRequested.
				CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
				_isResourceFilterAdded=true;
			}
			CoreWebView2.Navigate(_uriDocument);
			WebView2PageStore.RemovePage(pageOld);
		}

		///<summary>Leaves the page of NavigateToHtml (it is removed from memory) before the control shows something else or is disposed.</summary>
		private void ClearDocument() {
			WebView2Page pageOld=_page;
			_page=null;
			_uriDocument=null;
			_pagePending=null;
			_isRemoteContentAllowed=false;
			_listFoldersAllowed=new List<string>();
			WebView2PageStore.RemovePage(pageOld);
		}

		///<summary>Removes the WebResourceRequested filter of NavigateToHtml's pages, so other pages load without a detour through the program.</summary>
		private void RemoveResourceFilter() {
			if(!_isResourceFilterAdded || CoreWebView2==null) {
				return;
			}
			CoreWebView2.RemoveWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
			_isResourceFilterAdded=false;
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

		///<summary>A page for NavigateToHtml: its html, the files of its own folder and what it may load besides them.</summary>
		private class PagePending {
			public string Html;
			public IDictionary<string,byte[]> DictFiles;
			public bool IsRemoteContentAllowed;
			public List<string> ListFoldersAllowed;
		}
	}

	///<summary>What ODWebView2.Init throws when the WebView2 Runtime is missing or would not start. An ODException, so the windows' "Init told the
	///user" catches still hold. Give it to ODWebView2.TryShowInitFailure before a window shows its own message.</summary>
	public class ODWebView2InitException:ODException {
		///<summary>True when Init told the user why (a missing runtime always; a runtime that would not start once per program session).</summary>
		public bool IsUserTold { get; }
		///<summary>True when the control was closed while it started (its window closed): no failure, and nothing to tell.</summary>
		public bool IsClosed { get; }

		public ODWebView2InitException(string message,Exception exceptionInner,bool isUserTold,bool isClosed) : base(message,exceptionInner) {
			IsUserTold=isUserTold;
			IsClosed=isClosed;
		}
	}
}
