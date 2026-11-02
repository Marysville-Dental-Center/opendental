// The rules every embedded browser (WebView2) of the program follows: where its profile lives, the settings that keep pages away from the program,
// and the link handling of the pages the program shows itself (ODWebView2.NavigateToHtml). The WinForms wrapper (ODWebView2) and the WPF one
// (WpfControls.UI.WebView2) both use it, so this class needs only Microsoft.Web.WebView2.Core.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Web.WebView2.Core;

namespace CodeBase.Controls {
	///<summary>The setup every WebView2 of the program shares. There are no host objects (nothing of the program is reachable from a page's
	///script), dev tools only in Debug builds, and one user data folder per Windows user under the program's %LOCALAPPDATA% folder, never the
	///installation folder (which users cannot write to, and which a release replaces).</summary>
	public static class WebView2Policy {
		///<summary>The sub folder of the program's %LOCALAPPDATA% folder (ProgramNames.LocalAppDataFolderName) that holds the WebView2 user data:
		///the profile with its cache and cookies.</summary>
		public const string UserDataFolderName="WebView2";
		///<summary>Microsoft's installer of the Evergreen WebView2 Runtime (the bootstrapper). Windows 11 includes the runtime.</summary>
		public const string RuntimeDownloadUrl="https://go.microsoft.com/fwlink/p/?LinkId=2124703";
		///<summary>The items of the default context menu that a page shown with ODWebView2.NavigateToHtml keeps. Everything else (Save as,
		///Print, Share, Inspect, Back, Reload and the like) is removed: those either leave the page or go around the window's own buttons.</summary>
		private static readonly string[] _arrayContextMenuItemsKept={ "copy","copyLinkLocation","copyImage","copyImageLocation","selectAll",
			"openLinkInNewWindow" };

		///<summary>The WebView2 user data folder of this Windows user: %LOCALAPPDATA%\OpenPMS-Dental\WebView2.</summary>
		public static string GetUserDataFolder() {
			return GetUserDataFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		}

		///<summary>The WebView2 user data folder under folderLocalAppData (%LOCALAPPDATA%). Throws ArgumentException when folderLocalAppData is
		///blank, because WebView2 would otherwise put the profile next to the exe, in the installation folder.</summary>
		public static string GetUserDataFolder(string folderLocalAppData) {
			if(string.IsNullOrWhiteSpace(folderLocalAppData)) {
				throw new ArgumentException("The local application data folder is unknown.",nameof(folderLocalAppData));
			}
			return Path.Combine(folderLocalAppData,ProgramNames.LocalAppDataFolderName,UserDataFolderName);
		}

		///<summary>The folder the pages of NavigateToHtml are written to: the program's temp folder (ProgramNames.TempFolderName), which the
		///program empties when it closes.</summary>
		public static string GetPageFolder() {
			string folder=Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName);
			Directory.CreateDirectory(folder);
			return folder;
		}

		///<summary>True in Debug builds only: a Release build never opens the dev tools.</summary>
		public static bool AreDevToolsAllowed {
			get {
				return ODBuild.IsDebug();
			}
		}

		///<summary>The settings every WebView2 of the program gets once it is initialized: no host objects, dev tools only in Debug builds.</summary>
		public static void ApplySecureSettings(CoreWebView2Settings coreWebView2Settings) {
			coreWebView2Settings.AreHostObjectsAllowed=false;
			coreWebView2Settings.AreDevToolsEnabled=AreDevToolsAllowed;
		}

		///<summary>The settings of a page the program shows itself (wiki pages, email previews, generated reports): no scripts (scripts the
		///program runs with ExecuteScriptAsync still work), no web messages, no script dialogs, autofill or password saving.</summary>
		public static void ApplyDocumentSettings(CoreWebView2Settings coreWebView2Settings,bool isContextMenuEnabled) {
			coreWebView2Settings.IsScriptEnabled=false;
			coreWebView2Settings.IsWebMessageEnabled=false;
			coreWebView2Settings.AreDefaultScriptDialogsEnabled=false;
			coreWebView2Settings.IsGeneralAutofillEnabled=false;
			coreWebView2Settings.IsPasswordAutosaveEnabled=false;
			coreWebView2Settings.AreDefaultContextMenusEnabled=isContextMenuEnabled;
		}

		///<summary>True when uri is the page uriDocument itself, ignoring a #fragment (an in-page bookmark) and letter case.</summary>
		public static bool IsSameDocument(string uri,string uriDocument) {
			if(string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(uriDocument)) {
				return false;
			}
			return string.Equals(RemoveFragment(uri),RemoveFragment(uriDocument),StringComparison.OrdinalIgnoreCase);
		}

		private static string RemoveFragment(string uri) {
			int idx=uri.IndexOf('#');
			if(idx<0) {
				return uri;
			}
			return uri.Substring(0,idx);
		}

		///<summary>True for the only addresses a frame of a generated page may load: about:blank and about:srcdoc. A frame never loads a remote
		///page or a file into a page the program shows.</summary>
		public static bool IsAllowedFrameUri(string uri) {
			return uri=="about:blank" || uri=="about:srcdoc";
		}

		///<summary>True when url may be opened in the default browser or mail program: an absolute http, https or mailto address. Anything else
		///(file:, javascript:, other programs' protocols) is never handed to the shell.</summary>
		public static bool IsSystemBrowserUrl(string url) {
			if(string.IsNullOrWhiteSpace(url)) {
				return false;
			}
			Uri uri;
			if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri)) {
				return false;
			}
			if(uri.Scheme==Uri.UriSchemeMailto) {
				return true;
			}
			return (uri.Scheme==Uri.UriSchemeHttp || uri.Scheme==Uri.UriSchemeHttps) && uri.Host!="";
		}

		///<summary>The address to open in the default browser for a link the user clicked in a page the program shows (uriDocument), or "" when
		///the link must not leave the program. Only http, https and mailto links qualify. A relative link (which the browser resolves into the
		///page's own folder) counts as a web address, as with the old browser control: "www.example.com" opens http://www.example.com/.</summary>
		public static string GetSystemBrowserUrl(string uriLink,string uriDocument) {
			if(string.IsNullOrWhiteSpace(uriLink)) {
				return "";
			}
			if(IsSystemBrowserUrl(uriLink)) {
				return uriLink.Trim();
			}
			Uri uri;
			Uri uriDoc;
			if(!Uri.TryCreate(uriLink,UriKind.Absolute,out uri) || !uri.IsFile
				|| string.IsNullOrEmpty(uriDocument) || !Uri.TryCreate(uriDocument,UriKind.Absolute,out uriDoc) || !uriDoc.IsFile)
			{
				return "";
			}
			string folderDocument=uriDoc.GetLeftPart(UriPartial.Path);
			folderDocument=folderDocument.Substring(0,folderDocument.LastIndexOf('/')+1);
			string link=uri.GetLeftPart(UriPartial.Path);
			if(!link.StartsWith(folderDocument,StringComparison.OrdinalIgnoreCase) || IsSameDocument(uriLink,uriDocument)) {
				return "";
			}
			string relative=Uri.UnescapeDataString(link.Substring(folderDocument.Length));
			Uri uriWeb;
			if(relative=="" || relative.Contains("\\") || !Uri.TryCreate("http://"+relative,UriKind.Absolute,out uriWeb) || !uriWeb.Host.Contains(".")) {
				return "";
			}
			return uriWeb.AbsoluteUri;
		}

		///<summary>The target of a link with one of the program's own schemes, such as "wiki:" or "wikifile:": what follows "scheme:", with %
		///escapes undone. Null when uriLink has another scheme.</summary>
		public static string GetSchemeTarget(string uriLink,string scheme) {
			if(string.IsNullOrEmpty(uriLink) || string.IsNullOrEmpty(scheme) || !uriLink.StartsWith(scheme+":",StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			return Uri.UnescapeDataString(uriLink.Substring(scheme.Length+1));
		}

		///<summary>Splits cookieData of the form "name=value" (InternetSetCookie's format; spaces around either are ignored and attributes after a
		///';' are dropped). False when there is no '=' or no name, or the name holds a character a cookie name may not have.</summary>
		public static bool TryParseCookie(string cookieData,out string name,out string value) {
			name="";
			value="";
			if(string.IsNullOrWhiteSpace(cookieData)) {
				return false;
			}
			string data=cookieData;
			int idxSemicolon=data.IndexOf(';');
			if(idxSemicolon>=0) {
				data=data.Substring(0,idxSemicolon);
			}
			int idxEquals=data.IndexOf('=');
			if(idxEquals<=0) {
				return false;
			}
			string nameCookie=data.Substring(0,idxEquals).Trim();
			if(nameCookie=="" || nameCookie.Any(x => char.IsWhiteSpace(x) || char.IsControl(x) || "()<>@,;:\\\"/[]?={}".Contains(x))) {
				return false;
			}
			name=nameCookie;
			value=data.Substring(idxEquals+1).Trim();
			return true;
		}

		///<summary>Opens url in the default browser (or mail program for mailto) when IsSystemBrowserUrl allows it. Returns false, and starts
		///nothing, otherwise or when the shell fails.</summary>
		public static bool OpenInSystemBrowser(string url) {
			if(!IsSystemBrowserUrl(url)) {
				return false;
			}
			try {
				ProcessStartInfo processStartInfo=new ProcessStartInfo(url.Trim());
				processStartInfo.UseShellExecute=true;
				using(Process.Start(processStartInfo)) {
				}
				return true;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True when the default context menu item with this name stays on a page shown with NavigateToHtml.</summary>
		public static bool IsContextMenuItemKept(string name) {
			return _arrayContextMenuItemsKept.Contains(name??"");
		}

		///<summary>Which items of a context menu stay: the kept commands, and the separators between two of them (never a separator at either end
		///or next to another one). listNames and listIsSeparator describe the menu items in order; the result has one entry per item.</summary>
		public static List<bool> GetContextMenuItemsKept(IList<string> listNames,IList<bool> listIsSeparator) {
			List<bool> listKept=new List<bool>();
			for(int i=0;i<listNames.Count;i++) {
				listKept.Add(!listIsSeparator[i] && IsContextMenuItemKept(listNames[i]));
			}
			//A separator stays when a kept command comes before it (since the last kept separator) and another one after it.
			bool hasCommandBefore=false;
			for(int i=0;i<listNames.Count;i++) {
				if(!listIsSeparator[i]) {
					if(listKept[i]) {
						hasCommandBefore=true;
					}
					continue;
				}
				if(!hasCommandBefore) {
					continue;
				}
				bool hasCommandAfter=false;
				for(int j=i+1;j<listNames.Count;j++) {
					if(listIsSeparator[j]) {
						break;
					}
					if(listKept[j]) {
						hasCommandAfter=true;
						break;
					}
				}
				if(hasCommandAfter) {
					listKept[i]=true;
					hasCommandBefore=false;
				}
			}
			return listKept;
		}

		///<summary>Removes from the context menu every item that GetContextMenuItemsKept does not keep.</summary>
		public static void KeepContextMenuItems(IList<CoreWebView2ContextMenuItem> listMenuItems) {
			List<string> listNames=listMenuItems.Select(x => x.Name).ToList();
			List<bool> listIsSeparator=listMenuItems.Select(x => x.Kind==CoreWebView2ContextMenuItemKind.Separator).ToList();
			List<bool> listKept=GetContextMenuItemsKept(listNames,listIsSeparator);
			for(int i=listMenuItems.Count-1;i>=0;i--) {
				if(!listKept[i]) {
					listMenuItems.RemoveAt(i);
				}
			}
		}
	}

	///<summary>A link the user clicked in a page shown with ODWebView2.NavigateToHtml. The navigation itself is always cancelled.</summary>
	public class ODWebView2LinkEventArgs:EventArgs {
		///<summary>The address of the link, as the browser resolved it (a relative link is a file: address in the page's folder).</summary>
		public string Uri { get; }
		///<summary>The address of the page the link is on.</summary>
		public string UriDocument { get; }
		///<summary>True when the user asked for a new window (Ctrl+click, a middle click, or "Open link in new window").</summary>
		public bool IsNewWindow { get; }

		public ODWebView2LinkEventArgs(string uri,string uriDocument,bool isNewWindow) {
			Uri=uri;
			UriDocument=uriDocument;
			IsNewWindow=isNewWindow;
		}

		///<summary>The address to open in the default browser, or "" when the link must stay in the program. See WebView2Policy.GetSystemBrowserUrl.</summary>
		public string GetSystemBrowserUrl() {
			return WebView2Policy.GetSystemBrowserUrl(Uri,UriDocument);
		}
	}
}
