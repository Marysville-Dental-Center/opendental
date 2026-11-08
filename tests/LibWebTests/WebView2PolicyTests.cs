// The rules every WebView2 of the program follows (CodeBase.Controls.WebView2Policy): the profile folder (and that nothing creates an environment
// without it), what a link in a page the program shows may open, what such a page may load, the context menu those pages keep, the cookies of
// the eCW panel, and what each window allows its pages. Pure functions and source checks; no browser is started.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibWebTests {
	[TestClass]
	public class WebView2PolicyTests {
		private const string DOCUMENT=@"file:///C:/Users/u/AppData/Local/Temp/openpms-dental/ABC12320261102.html";

		[TestMethod]
		public void UserDataFolder_IsTheProgramsLocalAppDataFolder_NeverTheInstallFolder() {
			Assert.AreEqual(@"C:\Users\u\AppData\Local\OpenPMS-Dental\WebView2",WebView2Policy.GetUserDataFolder(@"C:\Users\u\AppData\Local"));
			Assert.AreEqual(Path.Combine(ProgramNames.LocalAppDataFolderName,"WebView2"),
				WebView2Policy.GetUserDataFolder(@"C:\x").Substring(@"C:\x\".Length),"From ProgramNames, so a rename changes it in one place.");
			string folder=WebView2Policy.GetUserDataFolder();
			StringAssert.StartsWith(folder,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
			string folderInstall=AppDomain.CurrentDomain.BaseDirectory;
			Assert.IsFalse(folder.StartsWith(folderInstall,StringComparison.OrdinalIgnoreCase));
			Assert.IsFalse(folder.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),StringComparison.OrdinalIgnoreCase));
			//WebView2 puts a profile next to the exe when it gets no folder: never.
			foreach(string blank in new[] { null,"","  " }) {
				try {
					WebView2Policy.GetUserDataFolder(blank);
					Assert.Fail("No folder for a blank local application data folder.");
				}
				catch(ArgumentException) {
				}
			}
		}

		[TestMethod]
		public void UserDataFolder_IsWhatBothWrappersUse() {
			string winForms=TestPaths.ReadSource("CodeBase","Controls","ODWebView2.cs");
			StringAssert.Contains(winForms,"CoreWebView2Environment.CreateAsync(null,WebView2Policy.GetUserDataFolder())");
			Assert.IsFalse(winForms.Contains("WindowsFormsWebView2"),"The old shared folder name is gone.");
			string wpf=TestPaths.ReadSource("WpfControlsOD","UI","Controls","WebView2.xaml.cs");
			StringAssert.Contains(wpf,"string userDataFolder=WebView2Policy.GetUserDataFolder();");
			Assert.IsFalse(wpf.Contains("GetTempFolderPath"),"Not the temp folder, which the program empties when it closes.");
			StringAssert.Contains(wpf,"WebView2Policy.ApplySecureSettings(webView2.CoreWebView2.Settings);");
		}

		[TestMethod]
		public void EveryEnvironment_UsesTheProgramsUserDataFolder() {
			//An environment created without a user data folder, or a control initialized without an environment, puts the profile next to the
			//exe: in the installation folder, which users cannot write to. Only the two wrappers create an environment, from GetUserDataFolder.
			List<string> listFiles=TestPaths.GetShippedFiles(".cs");
			Assert.IsTrue(listFiles.Any(x => TestPaths.Relative(x).Equals(@"OpenPmsDental\User Controls\ControlImageDisplay.cs",
				StringComparison.OrdinalIgnoreCase)),"The scan covers the program.");
			Regex regexEnsureWithoutEnvironment=new Regex(@"\bEnsureCoreWebView2Async\s*\(\s*(null\s*)?[,)]");
			List<string> listViolations=new List<string>();
			int countAllowed=0;
			foreach(string path in listFiles) {
				string relative=TestPaths.Relative(path);
				string[] arrayLines=File.ReadAllLines(path);
				for(int i=0;i<arrayLines.Length;i++) {
					string line=arrayLines[i];
					if(line.TrimStart().StartsWith("//")) {
						continue;
					}
					if(line.Contains("CoreWebView2Environment.CreateAsync(")) {
						bool isWinFormsWrapper=relative.Equals(@"CodeBase\Controls\ODWebView2.cs",StringComparison.OrdinalIgnoreCase)
							&& line.Contains("CoreWebView2Environment.CreateAsync(null,WebView2Policy.GetUserDataFolder())");
						bool isWpfWrapper=relative.Equals(@"WpfControlsOD\UI\Controls\WebView2.xaml.cs",StringComparison.OrdinalIgnoreCase)
							&& line.Contains("CoreWebView2Environment.CreateAsync(null,userDataFolder,null)");
						if(isWinFormsWrapper || isWpfWrapper) {
							countAllowed++;
						}
						else {
							listViolations.Add(relative+":"+(i+1)+" "+line.Trim());
						}
					}
					if(regexEnsureWithoutEnvironment.IsMatch(line) || line.Contains("new CoreWebView2CreationProperties")) {
						listViolations.Add(relative+":"+(i+1)+" "+line.Trim());
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Only ODWebView2.CreateEnvironmentAsync and WpfControls.UI.WebView2 create an environment:\r\n"
				+string.Join("\r\n",listViolations));
			Assert.AreEqual(2,countAllowed,"Both wrappers were found.");
			//The Images module prints with the environment of its own control (S0-F: its print browser, _odWebView2Print).
			string source=TestPaths.ReadSource("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			StringAssert.Contains(source,"_odWebView2Print.CoreWebView2.Environment.CreatePrintSettings()");
		}

		[TestMethod]
		public void DevTools_OnlyInDebugBuilds() {
			Assert.AreEqual(ODBuild.IsDebug(),WebView2Policy.AreDevToolsAllowed);
			string source=TestPaths.ReadSource("CodeBase","Controls","WebView2Policy.cs");
			StringAssert.Contains(source,"coreWebView2Settings.AreHostObjectsAllowed=false;");
			StringAssert.Contains(source,"coreWebView2Settings.AreDevToolsEnabled=AreDevToolsAllowed;");
			StringAssert.Contains(source,"return ODBuild.IsDebug();");
		}

		[TestMethod]
		public void NoHostObject_IsEverAdded() {
			List<string> listViolations=new List<string>();
			foreach(string path in TestPaths.GetShippedFiles(".cs")) {
				string text=File.ReadAllText(path);
				if(text.Contains("AddHostObjectToScript") || text.Contains("AreHostObjectsAllowed=true") || text.Contains("AreHostObjectsAllowed = true")) {
					listViolations.Add(TestPaths.Relative(path));
				}
			}
			Assert.AreEqual(0,listViolations.Count,"No page may reach the program's objects: "+string.Join(", ",listViolations));
		}

		[TestMethod]
		public void IsSameDocument_IgnoresTheFragmentAndCase() {
			Assert.IsTrue(WebView2Policy.IsSameDocument(DOCUMENT,DOCUMENT));
			Assert.IsTrue(WebView2Policy.IsSameDocument(DOCUMENT+"#section2",DOCUMENT));
			Assert.IsTrue(WebView2Policy.IsSameDocument(DOCUMENT.ToUpperInvariant(),DOCUMENT));
			Assert.IsFalse(WebView2Policy.IsSameDocument(@"file:///C:/Users/u/AppData/Local/Temp/openpms-dental/OTHER.html",DOCUMENT));
			Assert.IsFalse(WebView2Policy.IsSameDocument("about:blank",DOCUMENT));
			Assert.IsFalse(WebView2Policy.IsSameDocument(DOCUMENT,null));
			Assert.IsFalse(WebView2Policy.IsSameDocument(null,DOCUMENT));
		}

		[TestMethod]
		public void IsSystemBrowserUrl_OnlyHttpHttpsAndMailto() {
			Assert.IsTrue(WebView2Policy.IsSystemBrowserUrl("https://www.example.com/page?x=1"));
			Assert.IsTrue(WebView2Policy.IsSystemBrowserUrl("http://www.example.com/"));
			Assert.IsTrue(WebView2Policy.IsSystemBrowserUrl("mailto:someone@example.com"));
			foreach(string url in new[] { null,"","file:///C:/Windows/System32/calc.exe",@"C:\Windows\notepad.exe","javascript:alert(1)",
				"ms-settings:display","wiki:Home","about:blank","data:text/html,<p>x</p>","www.example.com","\\\\server\\share\\file.exe" })
			{
				Assert.IsFalse(WebView2Policy.IsSystemBrowserUrl(url),url??"null");
			}
			Assert.IsFalse(WebView2Policy.OpenInSystemBrowser("file:///C:/Windows/notepad.exe"),"Nothing but a web or mail address reaches the shell.");
			Assert.IsFalse(WebView2Policy.OpenInSystemBrowser(null));
		}

		[TestMethod]
		public void GetSystemBrowserUrl_WebAndMailLinks_AndRelativeLinksAsWebAddresses() {
			Assert.AreEqual("https://www.example.com/a",WebView2Policy.GetSystemBrowserUrl("https://www.example.com/a",DOCUMENT));
			Assert.AreEqual("mailto:office@example.com",WebView2Policy.GetSystemBrowserUrl("mailto:office@example.com",DOCUMENT));
			//<a href="www.example.com"> resolves into the page's folder; the old browser control opened it as http://www.example.com.
			Assert.AreEqual("http://www.example.com/",
				WebView2Policy.GetSystemBrowserUrl("file:///C:/Users/u/AppData/Local/Temp/openpms-dental/www.example.com",DOCUMENT));
			Assert.AreEqual("http://www.example.com/path/page",
				WebView2Policy.GetSystemBrowserUrl("file:///C:/Users/u/AppData/Local/Temp/openpms-dental/www.example.com/path/page",DOCUMENT));
			//Never a file, the page itself, a file elsewhere, another program's protocol or a name without a dot.
			foreach(string uri in new[] { DOCUMENT,DOCUMENT+"#top","file:///C:/Windows/notepad.exe","file:///C:/Users/u/AppData/Local/Temp/openpms-dental/readme",
				"file://server/share/x.exe","javascript:alert(1)","ms-settings:","wiki:Home",null,"" })
			{
				Assert.AreEqual("",WebView2Policy.GetSystemBrowserUrl(uri,DOCUMENT),uri??"null");
			}
			Assert.AreEqual("",WebView2Policy.GetSystemBrowserUrl("file:///C:/Users/u/AppData/Local/Temp/openpms-dental/www.example.com",null));
		}

		[TestMethod]
		public void GetSystemBrowserUrl_AFileThatIsThere_IsNeverAWebAddress() {
			//The page file of an earlier page of the same window sits in the same folder, and its name has a dot.
			string folder=TestPaths.NewTempFolder("links");
			try {
				string pathDocument=Path.Combine(folder,"PAGE20261102.html");
				string pathOther=Path.Combine(folder,"ABC12320261102.html");
				File.WriteAllText(pathDocument,"<p>page</p>");
				File.WriteAllText(pathOther,"<p>other page</p>");
				string uriDocument=new Uri(pathDocument).AbsoluteUri;
				Assert.AreEqual("",WebView2Policy.GetSystemBrowserUrl(new Uri(pathOther).AbsoluteUri,uriDocument));
				string pathWeb=Path.Combine(folder,"www.example.com");
				Assert.AreEqual("http://www.example.com/",WebView2Policy.GetSystemBrowserUrl(new Uri(pathWeb).AbsoluteUri,uriDocument),
					"A name that is not there stays a web address.");
				Directory.CreateDirectory(pathWeb);
				Assert.AreEqual("",WebView2Policy.GetSystemBrowserUrl(new Uri(pathWeb).AbsoluteUri,uriDocument),"A folder that is there is no web address.");
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
		}

		[TestMethod]
		public void IsPageResourceAllowed_OwnFolderAndAllowedFolders_RemoteOnlyWhenAllowed() {
			List<string> listNone=new List<string>();
			//The page's own folder (and below), the page itself, and what holds nothing from outside.
			foreach(string uri in new[] { DOCUMENT,DOCUMENT+"#top","file:///C:/Users/u/AppData/Local/Temp/openpms-dental/dot.png",
				"file:///C:/Users/u/AppData/Local/Temp/openpms-dental/RANDOM1/image001.png","data:image/png;base64,iVBORw0KGgo=","about:blank",
				"blob:file:///00000000-0000-0000-0000-000000000000" })
			{
				Assert.IsTrue(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listNone,false),uri);
			}
			//Files anywhere else: the parent folder, a folder that only starts like it, the system, another computer's share.
			foreach(string uri in new[] { "file:///C:/Users/u/AppData/Local/Temp/other.png","file:///C:/Users/u/AppData/Local/Temp/openpms-dental-x/a.png",
				"file:///C:/Users/u/AppData/Local/Temp/openpms-dental/../other.png","file:///C:/Windows/win.ini","file://unc.invalid/s/x.png",
				"file:///C:/Users/u/AppData/Local/Temp/openpms-dental/..%2F..%2Fother.png","file:///C:/Users/u/AppData/Local/Temp/openpms-dental/..%5Cother.png",
				"file:///C:/Users/u/AppData/Local/Temp/openpms-dental/dot.png:stream","file:///D:/OpenDentImages/Wiki/a.png" })
			{
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listNone,true),uri);
			}
			//The web: only when the window allows it.
			foreach(string uri in new[] { "http://img.invalid/x.png","https://img.invalid/style.css" }) {
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listNone,false),uri);
				Assert.IsTrue(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listNone,true),uri);
			}
			//Other schemes never, nor nothing.
			foreach(string uri in new[] { "ftp://img.invalid/x.png","javascript:alert(1)","ms-settings:display","wiki:Home","ws://img.invalid/",null,""," " }) {
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listNone,true),uri??"null");
			}
			//An allowed folder, on a drive or a share: its files and those below it, never a neighbour or another share.
			List<string> listAllowed=new List<string> { @"D:\OpenDentImages\Wiki",@"\\server.invalid\OpenDentImages\EmailImages\" };
			foreach(string uri in new[] { "file:///D:/OpenDentImages/Wiki/a.png","file:///d:/opendentimages/wiki/sub/b.png",
				"file://server.invalid/OpenDentImages/EmailImages/logo.png","file:///C:/Users/u/AppData/Local/Temp/openpms-dental/dot.png" })
			{
				Assert.IsTrue(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listAllowed,false),uri);
			}
			foreach(string uri in new[] { "file:///D:/OpenDentImages/WikiOld/a.png","file:///D:/OpenDentImages/a.png",
				"file://server.invalid/OpenDentImages/Other/a.png","file://unc.invalid/OpenDentImages/EmailImages/logo.png","file:///C:/Windows/win.ini" })
			{
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(uri,DOCUMENT,listAllowed,false),uri);
			}
			//A folder without a drive or server (a cloud path, a relative one) allows nothing.
			List<string> listRelative=new List<string> { "/AtoZ/Wiki","Wiki",@"\Wiki","",null };
			Assert.IsFalse(WebView2Policy.IsPageResourceAllowed("file:///C:/AtoZ/Wiki/a.png",DOCUMENT,listRelative,false));
			Assert.IsFalse(WebView2Policy.IsPageResourceAllowed("file:///C:/Wiki/a.png",DOCUMENT,listRelative,false));
			//Without a page file nothing local loads.
			Assert.IsFalse(WebView2Policy.IsPageResourceAllowed("file:///C:/x/a.png","https://example.invalid/",new List<string>{ @"C:\x" },false));
			Assert.IsTrue(WebView2Policy.IsRemoteUri("https://img.invalid/x.png"));
			Assert.IsFalse(WebView2Policy.IsRemoteUri("file://img.invalid/x.png"));
		}

		[TestMethod]
		public void GetLocalFilePath_LocalAndSharePaths_NeverAHiddenSeparator() {
			Assert.AreEqual(@"C:\a\b c.png",WebView2Policy.GetLocalFilePath("file:///C:/a/b%20c.png"));
			Assert.AreEqual(@"\\server.invalid\share\x.png",WebView2Policy.GetLocalFilePath("file://server.invalid/share/x.png"));
			foreach(string uri in new[] { "https://img.invalid/x.png","file:///C:/a/..%5Cb.png","file:///C:/a/..%2Fb.png","file:///C:/a/b.png:ads",
				"file://server.invalid/C:/x.png","about:blank",null,"" })
			{
				Assert.IsNull(WebView2Policy.GetLocalFilePath(uri),uri??"null");
			}
		}

		[TestMethod]
		public void DocumentPages_NoSwipe_NoLinkForTheirOwnFilesOrHistory_AndAFilterForEveryRequest() {
			string policy=TestPaths.ReadSource("CodeBase","Controls","WebView2Policy.cs");
			StringAssert.Contains(policy,"coreWebView2Settings.IsSwipeNavigationEnabled=false");
			string source=TestPaths.ReadSource("CodeBase","Controls","ODWebView2.cs");
			StringAssert.Contains(source,"e.NavigationKind==CoreWebView2NavigationKind.NewDocument","Back, Forward and Reload are never links.");
			StringAssert.Contains(source,"if(IsPageUri(e.Uri)) {","An earlier page file of the control is never a link.");
			StringAssert.Contains(source,"CoreWebView2.AddWebResourceRequestedFilter(\"*\",CoreWebView2WebResourceContext.All);");
			StringAssert.Contains(source,"CreateWebResourceResponse(null,403,\"Blocked\",\"\")");
			StringAssert.Contains(source,"this.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=AreBrowserKeysEnabled;");
		}

		[TestMethod]
		public void Windows_LoadWhatTheirPagesNeed() {
			//Html from outside the office never loads anything from the web by itself.
			foreach(string[] arrayPath in new[] { new[] { "OpenPmsDental","Forms","FormEhrSummaryCcdEdit.cs" },new[] { "OpenPmsDental","Forms","FormWebView.cs" },
				new[] { "OpenPmsDental","User Controls","UserControlEmailTemplate.cs" },new[] { "OpenPmsDental","Forms","FormWebBrowserPrefs.cs" } })
			{
				Assert.IsFalse(TestPaths.ReadSource(arrayPath).Contains("isRemoteContentAllowed:true"),string.Join("\\",arrayPath));
			}
			//A received email loads web images only after Show Images, which also appears when the email asked for some.
			string email=TestPaths.ReadSource("OpenPmsDental","User Controls","EmailPreviewControl.cs");
			Assert.IsTrue(Regex.IsMatch(email,@"if\(IsReceived\(\)\)\s*\{\s*webViewEmail\.NavigateToHtml\(HtmlText\);"),"A received email: the defaults.");
			StringAssert.Contains(email,"webViewEmail.NavigateToHtml(html,isRemoteContentAllowed:true,listFoldersAllowed:listFoldersAllowed,dictFiles:dictFiles);");
			StringAssert.Contains(email,"webViewEmail.RemoteContentBlocked+=webViewEmail_RemoteContentBlocked;");
			//The wiki windows load the wiki's images.
			foreach(string name in new[] { "FormWiki.cs","FormWikiAllPages.cs","FormWikiDrafts.cs","FormWikiEdit.cs","FormWikiHistory.cs",
				"FormWikiIncomingLinks.cs","FormWikiSearch.cs" })
			{
				StringAssert.Contains(TestPaths.ReadSource("OpenPmsDental","Forms",name),"listFoldersAllowed:MarkupEdit.GetImageFolders(isEmail:false)",name);
			}
			//Find in page (Ctrl+F) where the Internet Explorer control kept its shortcuts.
			foreach(string[] arrayPath in new[] { new[] { "OpenPmsDental","Forms","FormWiki.designer.cs" },
				new[] { "OpenPmsDental","Forms","FormWikiIncomingLinks.designer.cs" },new[] { "OpenPmsDental","Forms","FormEhrSummaryCcdEdit.Designer.cs" },
				new[] { "OpenPmsDental","Forms","FormEhrEduBrowser.Designer.cs" },new[] { "OpenPmsDental","Forms","FormEmailTemplateEdit.designer.cs" },
				new[] { "OpenPmsDental","User Controls","UserControlEmailTemplate.Designer.cs" },new[] { "OpenPmsDental","Main Modules","ControlChart.designer.cs" } })
			{
				StringAssert.Contains(TestPaths.ReadSource(arrayPath),".AreBrowserKeysEnabled = true;",string.Join("\\",arrayPath));
			}
		}

		[TestMethod]
		public void GetSchemeTarget_UndoesEscapes() {
			Assert.AreEqual("Main Page",WebView2Policy.GetSchemeTarget("wiki:Main%20Page","wiki"));
			Assert.AreEqual("Main Page",WebView2Policy.GetSchemeTarget("WIKI:Main Page","wiki"));
			Assert.AreEqual("",WebView2Policy.GetSchemeTarget("wiki:","wiki"));
			Assert.IsNull(WebView2Policy.GetSchemeTarget("wikifile:C:/x.txt","wiki"),"Another scheme.");
			Assert.IsNull(WebView2Policy.GetSchemeTarget("https://example.com","wiki"));
			Assert.IsNull(WebView2Policy.GetSchemeTarget(null,"wiki"));
		}

		[TestMethod]
		public void IsAllowedFrameUri_OnlyBlankFrames() {
			Assert.IsTrue(WebView2Policy.IsAllowedFrameUri("about:blank"));
			Assert.IsTrue(WebView2Policy.IsAllowedFrameUri("about:srcdoc"));
			Assert.IsFalse(WebView2Policy.IsAllowedFrameUri("https://tracker.example.invalid/"));
			Assert.IsFalse(WebView2Policy.IsAllowedFrameUri(DOCUMENT));
			Assert.IsFalse(WebView2Policy.IsAllowedFrameUri(null));
		}

		[TestMethod]
		public void ContextMenu_KeepsCopyAndLinkCommands_AndTidySeparators() {
			List<string> listNames=new List<string> { "back","forward","reload","","saveAs","print","","copy","selectAll","","openLinkInNewWindow",
				"copyLinkLocation","saveLinkAs","","inspectElement","" };
			List<bool> listIsSeparator=listNames.Select(x => x=="").ToList();
			List<bool> listKept=WebView2Policy.GetContextMenuItemsKept(listNames,listIsSeparator);
			List<string> listResult=listNames.Where((x,i) => listKept[i]).Select(x => x=="" ? "|" : x).ToList();
			Assert.AreEqual("copy,selectAll,|,openLinkInNewWindow,copyLinkLocation",string.Join(",",listResult));
			//Nothing kept: no separators either.
			List<string> listNone=new List<string> { "","print","","saveAs","" };
			Assert.IsFalse(WebView2Policy.GetContextMenuItemsKept(listNone,listNone.Select(x => x=="").ToList()).Any(x => x));
			foreach(string name in new[] { "saveAs","print","inspectElement","share","webCapture","back","reload","createQrCode","other",null }) {
				Assert.IsFalse(WebView2Policy.IsContextMenuItemKept(name),name??"null");
			}
		}

		[TestMethod]
		public void TryParseCookie_ReadsInternetSetCookiesFormat() {
			Assert.IsTrue(WebView2Policy.TryParseCookie("JSESSIONID = A1B2C3",out string name,out string value));
			Assert.AreEqual("JSESSIONID",name);
			Assert.AreEqual("A1B2C3",value);
			Assert.IsTrue(WebView2Policy.TryParseCookie("LBSESSIONID=xyz==; path=/",out name,out value));
			Assert.AreEqual("LBSESSIONID",name);
			Assert.AreEqual("xyz==",value,"The value keeps its own '=', and the attributes are dropped.");
			Assert.IsTrue(WebView2Policy.TryParseCookie("EMPTY=",out name,out value));
			Assert.AreEqual("",value);
			foreach(string data in new[] { null,"","   ","novalue","=x","bad name=1","a;b=1","tab\tname=1" }) {
				Assert.IsFalse(WebView2Policy.TryParseCookie(data,out name,out value),data??"null");
				Assert.AreEqual("",name);
			}
		}

		[TestMethod]
		public void LinkEventArgs_GiveTheSystemBrowserUrl() {
			ODWebView2LinkEventArgs odWebView2LinkEventArgs=new ODWebView2LinkEventArgs("https://www.example.com/",DOCUMENT,isNewWindow:true);
			Assert.IsTrue(odWebView2LinkEventArgs.IsNewWindow);
			Assert.AreEqual("https://www.example.com/",odWebView2LinkEventArgs.GetSystemBrowserUrl());
			Assert.AreEqual("",new ODWebView2LinkEventArgs("file:///C:/x.exe",DOCUMENT,false).GetSystemBrowserUrl());
		}

		[TestMethod]
		public void InMemoryPagesFolder_IsUnderTheProgramsTempFolder_AndNeverCreated() {
			string folder=WebView2PageStore.GetRootFolder();
			Assert.AreEqual(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName,WebView2PageStore.RootFolderName),folder);
			WebView2Page webView2Page=WebView2PageStore.AddPage("<p>page</p>");
			try {
				Assert.IsFalse(Directory.Exists(folder),"Adding a page writes nothing.");
				Assert.IsFalse(File.Exists(webView2Page.Path));
			}
			finally {
				WebView2PageStore.RemovePage(webView2Page);
			}
		}
	}
}
