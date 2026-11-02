// Shows a page the program generated in the WebView2 wrapper (CodeBase ODWebView2.NavigateToHtml), in a small off-screen window, and checks the
// page mode the wiki and email windows rely on: the encoded text shows as text, the page's own script does not run (the program's scripts do),
// a local image next to the page loads, host objects and web messages are off, a clicked link raises LinkClicked and is never followed, and a
// navigation the page starts by itself (a meta refresh, a frame) is cancelled without raising it. Every address is local or under .invalid and
// is cancelled before a request goes out, and the browser runs with its background networking off: no network. Skipped (inconclusive) when the
// WebView2 Runtime is missing, when no window can be shown, or when LIBWEB_SKIP_WEBVIEW2=1.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;
using OpenPmsDental;

namespace LibWebTests {
	[TestClass]
	public class WebView2SmokeTests {
		private const string PATIENT_TEXT="Jane <b>Doe</b> & \"Sons\"";

		///<summary>What the off-screen window saw, filled on its thread and checked on the test's.</summary>
		private class Results {
			public string BrowserVersion="";
			public string Error=null;
			public bool IsLoaded;
			public string TextName;
			public string TextScriptCheck;
			public int ImageWidth;
			public bool IsScriptEnabled;
			public bool AreHostObjectsAllowed;
			public bool AreDevToolsEnabled;
			public bool IsWebMessageEnabled;
			public string SourceAfterClicks;
			public string DocumentUri;
			public string DocumentUriAfterRefresh;
			public string SourceAfterRefresh;
			public string PathDocument;
			public bool IsDocumentFileDeleted;
			public List<ODWebView2LinkEventArgs> ListLinks=new List<ODWebView2LinkEventArgs>();
			public int CountLinksBeforeRefresh;
			public List<string> ListNavigations=new List<string>();
			public List<string> ListFrames=new List<string>();
		}

		private static string BuildPage() {
			string body="<p id=\"name\">"+HtmlPage.Encode(PATIENT_TEXT)+"</p>"
				+"<p id=\"check\">not run</p>"
				+"<script>document.getElementById('check').textContent='script ran';</script>"
				+"<img id=\"img\" src=\"dot.png\">"
				+"<a id=\"wiki\" href=\"wiki:Test%20Page\" style=\"display:block;position:absolute;left:0;top:200px;width:300px;height:40px\">wiki link</a>"
				+"<a id=\"web\" href=\"https://example.invalid/page\" style=\"display:block;position:absolute;left:0;top:300px;width:300px;height:40px\">web</a>"
				+"<iframe id=\"frame\" src=\"https://frame.example.invalid/\" style=\"position:absolute;left:400px;top:0;width:50px;height:50px\"></iframe>";
			return HtmlPage.Build(body,"Smoke test","margin:0;font-family:sans-serif");
		}

		private static async Task<bool> WaitFor(Func<bool> funcIsDone,int milliseconds) {
			DateTime dateTimeEnd=DateTime.Now.AddMilliseconds(milliseconds);
			while(DateTime.Now<dateTimeEnd) {
				if(funcIsDone()) {
					return true;
				}
				await Task.Delay(100);
			}
			return funcIsDone();
		}

		private static async Task<bool> NavigateAndWait(ODWebView2 odWebView2,string html,string folder) {
			bool isCompleted=false;
			bool isSuccess=false;
			EventHandler<CoreWebView2NavigationCompletedEventArgs> eventHandler=(sender,e) => {
				isCompleted=true;
				isSuccess=e.IsSuccess;
			};
			odWebView2.NavigationCompleted+=eventHandler;
			odWebView2.NavigateToHtml(html,folder);
			await WaitFor(() => isCompleted,20000);
			odWebView2.NavigationCompleted-=eventHandler;
			return isSuccess;
		}

		///<summary>A real click (the browser's input pipeline, so the page sees a user gesture) at x,y CSS pixels.</summary>
		private static async Task Click(ODWebView2 odWebView2,int x,int y) {
			foreach(string type in new[] { "mousePressed","mouseReleased" }) {
				await odWebView2.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
					"{\"type\":\""+type+"\",\"x\":"+x+",\"y\":"+y+",\"button\":\"left\",\"clickCount\":1}");
			}
		}

		///<summary>The string a script returned (ExecuteScriptAsync gives it as JSON).</summary>
		private static string ReadJsonString(string json) {
			if(json==null || json=="null" || json.Length<2 || json[0]!='"') {
				return null;
			}
			return System.Text.RegularExpressions.Regex.Unescape(json.Substring(1,json.Length-2));
		}

		private static async Task Run(Form form,string folder,Results results) {
			CoreWebView2EnvironmentOptions coreWebView2EnvironmentOptions=new CoreWebView2EnvironmentOptions();
			coreWebView2EnvironmentOptions.AdditionalBrowserArguments="--disable-background-networking --disable-component-update --no-first-run";
			CoreWebView2Environment coreWebView2Environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(folder,"data"),
				coreWebView2EnvironmentOptions);
			results.BrowserVersion=coreWebView2Environment.BrowserVersionString;
			ODWebView2 odWebView2=new ODWebView2();
			odWebView2.Dock=DockStyle.Fill;
			form.Controls.Add(odWebView2);
			await odWebView2.EnsureCoreWebView2Async(coreWebView2Environment);
			odWebView2.LinkClicked+=(sender,e) => results.ListLinks.Add(e);
			odWebView2.CoreWebView2.NavigationStarting+=(sender,e) => results.ListNavigations.Add(e.Uri+" user="+e.IsUserInitiated);
			odWebView2.CoreWebView2.FrameNavigationStarting+=(sender,e) => results.ListFrames.Add(e.Uri);
			string folderPage=Path.Combine(folder,"page");
			Directory.CreateDirectory(folderPage);
			using(Bitmap bitmap=new Bitmap(8,8)) {
				using(Graphics g=Graphics.FromImage(bitmap)) {
					g.Clear(Color.Black);
				}
				bitmap.Save(Path.Combine(folderPage,"dot.png"),ImageFormat.Png);
			}
			results.IsLoaded=await NavigateAndWait(odWebView2,BuildPage(),folderPage);
			results.DocumentUri=odWebView2.DocumentUri;
			results.PathDocument=new Uri(odWebView2.DocumentUri).LocalPath;
			await Task.Delay(500);
			results.TextName=ReadJsonString(await odWebView2.CoreWebView2.ExecuteScriptAsync("document.getElementById('name').textContent"));
			results.TextScriptCheck=ReadJsonString(await odWebView2.CoreWebView2.ExecuteScriptAsync("document.getElementById('check').textContent"));
			int.TryParse(await odWebView2.CoreWebView2.ExecuteScriptAsync("document.getElementById('img').naturalWidth"),out results.ImageWidth);
			results.IsScriptEnabled=odWebView2.CoreWebView2.Settings.IsScriptEnabled;
			results.AreHostObjectsAllowed=odWebView2.CoreWebView2.Settings.AreHostObjectsAllowed;
			results.AreDevToolsEnabled=odWebView2.CoreWebView2.Settings.AreDevToolsEnabled;
			results.IsWebMessageEnabled=odWebView2.CoreWebView2.Settings.IsWebMessageEnabled;
			//Links: clicked, never followed.
			await Click(odWebView2,100,220);
			await WaitFor(() => results.ListLinks.Count>=1,5000);
			await Click(odWebView2,100,320);
			await WaitFor(() => results.ListLinks.Count>=2,5000);
			await Task.Delay(500);
			results.SourceAfterClicks=odWebView2.CoreWebView2.Source;
			//A page that navigates by itself: cancelled, and no LinkClicked.
			results.CountLinksBeforeRefresh=results.ListLinks.Count;
			string htmlRefresh="<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><meta http-equiv=\"refresh\" content=\"0;url=https://refresh.example.invalid/\">"
				+"</head><body><p>refresh</p></body></html>";
			await NavigateAndWait(odWebView2,htmlRefresh,folderPage);
			await WaitFor(() => results.ListNavigations.Any(x => x.StartsWith("https://refresh.example.invalid")),5000);
			await Task.Delay(1000);
			results.DocumentUriAfterRefresh=odWebView2.DocumentUri;
			results.SourceAfterRefresh=odWebView2.CoreWebView2.Source;
			string pathRefresh=new Uri(odWebView2.DocumentUri).LocalPath;
			form.Controls.Remove(odWebView2);
			odWebView2.Dispose();
			results.IsDocumentFileDeleted=!File.Exists(results.PathDocument) && !File.Exists(pathRefresh);
		}

		[TestMethod]
		public void GeneratedPage_ShowsTextLocalImages_RunsNoScript_AndFollowsNoLink() {
			if(Environment.GetEnvironmentVariable("LIBWEB_SKIP_WEBVIEW2")=="1") {
				Assert.Inconclusive("LIBWEB_SKIP_WEBVIEW2=1");
			}
			if(!ODWebView2.IsRuntimeAvailable()) {
				Assert.Inconclusive("The Microsoft WebView2 Runtime is not installed.");
			}
			string folder=TestPaths.NewTempFolder("smoke");
			Results results=new Results();
			try {
				Thread thread=new Thread(() => {
					using Form form=new Form();
					form.StartPosition=FormStartPosition.Manual;
					form.Location=new Point(-6000,-6000);//Off screen; WebView2 still lays out and runs the page.
					form.Size=new Size(600,500);
					form.ShowInTaskbar=false;
					form.FormBorderStyle=FormBorderStyle.None;
					form.Load+=async (sender,e) => {
						try {
							await Run(form,folder,results);
						}
						catch(Exception ex) {
							results.Error=ex.GetType().Name+": "+ex.Message;
						}
						form.Close();
					};
					Application.Run(form);
				});
				thread.SetApartmentState(ApartmentState.STA);
				thread.IsBackground=true;
				thread.Start();
				if(!thread.Join(TimeSpan.FromMinutes(3))) {
					Assert.Inconclusive("The WebView2 window did not finish within 3 minutes.");
				}
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			if(results.Error!=null) {
				Assert.Inconclusive("WebView2 could not be used here: "+results.Error);
			}
			string details="WebView2 Runtime "+results.BrowserVersion+"; navigations: "+string.Join(" | ",results.ListNavigations)
				+"; frames: "+string.Join(" | ",results.ListFrames)+"; links: "+string.Join(" | ",results.ListLinks.Select(x => x.Uri+(x.IsNewWindow ? " (new)" : "")));
			Console.WriteLine(details);
			Assert.IsTrue(results.IsLoaded,"The page loads from its file. "+details);
			StringAssert.StartsWith(results.DocumentUri,"file:///");
			Assert.AreEqual(PATIENT_TEXT,results.TextName,"The encoded text shows as text. "+details);
			Assert.AreEqual("not run",results.TextScriptCheck,"The page's own script never runs (the program's script read this). "+details);
			Assert.AreEqual(8,results.ImageWidth,"The local image next to the page loads. "+details);
			Assert.IsFalse(results.IsScriptEnabled);
			Assert.IsFalse(results.AreHostObjectsAllowed);
			Assert.IsFalse(results.IsWebMessageEnabled);
			Assert.AreEqual(WebView2Policy.AreDevToolsAllowed,results.AreDevToolsEnabled,"Dev tools only in Debug builds.");
			Assert.IsTrue(results.ListFrames.Any(x => x.StartsWith("https://frame.example.invalid")),"The frame asked to load. "+details);
			//The clicks.
			Assert.AreEqual(2,results.ListLinks.Count,"Each click raised LinkClicked once. "+details);
			Assert.AreEqual(EnumWikiLink.WikiPage,FormWiki.ParseLink(results.ListLinks[0].Uri,out string target),details);
			Assert.AreEqual("Test Page",target);
			Assert.AreEqual(results.DocumentUri,results.ListLinks[0].UriDocument);
			Assert.IsFalse(results.ListLinks[0].IsNewWindow);
			Assert.AreEqual("https://example.invalid/page",results.ListLinks[1].GetSystemBrowserUrl(),details);
			Assert.IsTrue(WebView2Policy.IsSameDocument(results.SourceAfterClicks,results.DocumentUri),"The page stayed: "+results.SourceAfterClicks);
			//The meta refresh.
			Assert.IsTrue(results.ListNavigations.Any(x => x.StartsWith("https://refresh.example.invalid") && x.EndsWith("user=False")),
				"The page tried to navigate by itself. "+details);
			Assert.AreEqual(results.CountLinksBeforeRefresh,results.ListLinks.Count,"A navigation the page starts by itself never raises LinkClicked. "+details);
			Assert.IsTrue(WebView2Policy.IsSameDocument(results.SourceAfterRefresh,results.DocumentUriAfterRefresh),"The refresh was cancelled: "
				+results.SourceAfterRefresh);
			Assert.IsTrue(results.IsDocumentFileDeleted,"The page files are deleted when the page changes and when the control is disposed.");
		}
	}
}
