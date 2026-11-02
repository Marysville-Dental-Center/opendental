// The rules every WebView2 of the program follows (CodeBase.Controls.WebView2Policy): the profile folder, what a link in a page the program
// shows may open, the context menu those pages keep, and the cookies of the eCW panel. Pure functions; no browser is started.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		public void PageFolder_IsTheProgramsTempFolder() {
			string folder=WebView2Policy.GetPageFolder();
			Assert.AreEqual(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName),folder,"Emptied when the program closes.");
			Assert.IsTrue(Directory.Exists(folder));
		}
	}
}
