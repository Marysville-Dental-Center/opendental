// Shows pages the program generated in the WebView2 wrapper (CodeBase ODWebView2.NavigateToHtml), in a small off-screen window, and checks the
// page mode the wiki and email windows rely on: the encoded text shows as text, the page's own script does not run (the program's scripts do),
// a local image next to the page loads, host objects and web messages are off, a clicked link raises LinkClicked and is never followed, and a
// navigation the page starts by itself (a meta refresh, a frame), the earlier of two pages shown back to back and Back are cancelled without
// raising it. A second test checks what a page may load: files from its own folder and the allowed folders only (never the system's or another
// computer's share), and http images only when the window allows them. Every address is local or under .invalid and is cancelled or answered
// before a request goes out, and the browser runs with its background networking off: no network. Inconclusive only when the WebView2 Runtime
// is missing or does not start here (in a plain WebView2 control, none of the program's code; also when no window can be shown), or when
// LIBWEB_SKIP_WEBVIEW2=1. Once it started, any error fails the test: an exception of the control, one in an event handler on the window's
// thread (caught there, never WinForms' error dialog), a message the control shows, or the 3 minutes running out.
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
		///<summary>The web address of the image the second test answers itself (never the network).</summary>
		private const string URL_REMOTE_IMAGE="http://img.invalid/x.png";
		///<summary>An image on another computer's share: Windows would send the user's credentials there if it were opened.</summary>
		private const string URL_SHARE_IMAGE="file://unc.invalid/s/x.png";

		///<summary>What the off-screen window saw, filled on its thread and checked on the test's.</summary>
		private class Results {
			public string BrowserVersion="";
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
			public int CountLinksBeforeQuickPages;
			public int CountLinksAfterQuickPages;
			public string UriFirstQuickPage;
			public string DocumentUriAfterQuickPages;
			public string SourceAfterQuickPages;
			public bool CanGoBack;
			public string SourceAfterBack;
		}

		///<summary>A request the page made, as the second test's own WebResourceRequested handler saw it after the control's.</summary>
		private class Request {
			public string Uri;
			public bool IsBlocked;
		}

		///<summary>What the second test's window saw.</summary>
		private class ResourceResults {
			public string BrowserVersion="";
			public string UriOutsideImage;
			public Dictionary<string,int> DictWidthsUntrusted=new Dictionary<string,int>();
			public Dictionary<string,int> DictWidthsShare=new Dictionary<string,int>();
			public Dictionary<string,int> DictWidthsTrusted=new Dictionary<string,int>();
			public List<Request> ListRequestsUntrusted=new List<Request>();
			public List<Request> ListRequestsShare=new List<Request>();
			public List<Request> ListRequestsTrusted=new List<Request>();
			public bool IsShareChecked;
			public int CountRemoteBlocked;
			public int CountRemoteBlockedAfterUntrusted;
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

		private static async Task<bool> NavigateAndWait(ODWebView2 odWebView2,string html,string folder,bool isRemoteContentAllowed=false,
			List<string> listFoldersAllowed=null)
		{
			bool isCompleted=false;
			bool isSuccess=false;
			EventHandler<CoreWebView2NavigationCompletedEventArgs> eventHandler=(sender,e) => {
				isCompleted=true;
				isSuccess=e.IsSuccess;
			};
			odWebView2.NavigationCompleted+=eventHandler;
			odWebView2.NavigateToHtml(html,folder,isRemoteContentAllowed,listFoldersAllowed);
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

		///<summary>An 8x8 black PNG.</summary>
		private static byte[] GetPngBytes() {
			using Bitmap bitmap=new Bitmap(8,8);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.Black);
			}
			using MemoryStream memoryStream=new MemoryStream();
			bitmap.Save(memoryStream,ImageFormat.Png);
			return memoryStream.ToArray();
		}

		///<summary>The WebView2 environment of a test: its own user data folder under folder, background networking off.</summary>
		private static Task<CoreWebView2Environment> CreateEnvironmentAsync(string folder) {
			CoreWebView2EnvironmentOptions coreWebView2EnvironmentOptions=new CoreWebView2EnvironmentOptions();
			coreWebView2EnvironmentOptions.AdditionalBrowserArguments="--disable-background-networking --disable-component-update --no-first-run";
			return CoreWebView2Environment.CreateAsync(null,Path.Combine(folder,"data"),coreWebView2EnvironmentOptions);
		}

		///<summary>Inconclusive when the test cannot use WebView2 here.</summary>
		private static void SkipWithoutRuntime() {
			if(Environment.GetEnvironmentVariable("LIBWEB_SKIP_WEBVIEW2")=="1") {
				Assert.Inconclusive("LIBWEB_SKIP_WEBVIEW2=1");
			}
			if(!ODWebView2.IsRuntimeAvailable()) {
				Assert.Inconclusive("The Microsoft WebView2 Runtime is not installed.");
			}
		}

		///<summary>How an off-screen run ended: the first error (an exception of the test's code or of the control, or the time running out), and
		///whether the WebView2 Runtime had started by then.</summary>
		private class OffScreenRun {
			private object _lock=new object();
			private string _error=null;
			private bool _isRuntimeStarted=false;

			public string Error {
				get {
					lock(_lock) {
						return _error;
					}
				}
			}

			///<summary>True once a plain WebView2 control started in the test's environment (StartRuntime). Before that an error means that the
			///runtime cannot be used here; after it, an error is a failure of the code under test.</summary>
			public bool IsRuntimeStarted {
				get {
					lock(_lock) {
						return _isRuntimeStarted;
					}
				}
			}

			public void SetRuntimeStarted() {
				lock(_lock) {
					_isRuntimeStarted=true;
				}
			}

			///<summary>Keeps the first error only.</summary>
			public void SetError(string error) {
				lock(_lock) {
					if(_error==null) {
						_error=error;
					}
				}
			}

			///<summary>Inconclusive when the runtime never started here; a failure of the test when it did.</summary>
			public void AssertNoError() {
				string error=Error;
				if(error==null) {
					return;
				}
				if(!IsRuntimeStarted) {
					Assert.Inconclusive("WebView2 could not be used here: "+error);
				}
				Assert.Fail("Failed after the WebView2 Runtime started: "+error);
			}
		}

		///<summary>Proves that the runtime starts here, with the test's environment, in a plain WebView2 control (none of the program's code; it
		///shows nothing and stays until the window closes, so the browser process keeps running), and tells offScreenRun so. Everything after it,
		///ODWebView2's own initialization included, is the code under test.</summary>
		private static async Task StartRuntime(Form form,CoreWebView2Environment coreWebView2Environment,OffScreenRun offScreenRun) {
			Microsoft.Web.WebView2.WinForms.WebView2 webView2=new Microsoft.Web.WebView2.WinForms.WebView2();
			webView2.Size=new Size(10,10);
			form.Controls.Add(webView2);
			await webView2.EnsureCoreWebView2Async(coreWebView2Environment);
			offScreenRun.SetRuntimeStarted();
		}

		///<summary>True when the control's own code threw ex (ODWebView2 or WebView2Policy, their lambdas and async methods included), not a
		///method it called: the control throws nothing on purpose while it shows pages, and what it calls and expects to fail (an event an older
		///runtime lacks, a file already deleted) throws from WebView2's or .NET's code.</summary>
		private static bool IsThrownByTheControl(Exception ex) {
			Type type=ex.TargetSite?.DeclaringType;
			while(type!=null) {
				if(type==typeof(ODWebView2) || type==typeof(WebView2Policy)) {
					return true;
				}
				type=type.DeclaringType;
			}
			return false;
		}

		///<summary>Runs funcRun in a small off-screen window on a thread of its own. An exception in an event handler on that thread (a posted
		///callback of the control, or a continuation of funcRun) goes to Application.ThreadException, never to WinForms' error dialog, which would
		///block the thread: it is recorded and closes the window. An exception the control's own code throws on that thread is recorded too, also
		///when nothing reports it: WebView2 drops an exception thrown in a handler of its events (seen with Runtime 156: a throw in the control's
		///NavigationCompleted handler reached neither ThreadException nor the test). The window may take 3 minutes. Check the result with
		///OffScreenRun.AssertNoError.</summary>
		private static OffScreenRun RunOffScreen(Func<Form,OffScreenRun,Task> funcRun) {
			OffScreenRun offScreenRun=new OffScreenRun();
			Thread thread=null;
			EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> eventHandlerFirstChance=(sender,e) => {
				try {
					if(Thread.CurrentThread==thread && IsThrownByTheControl(e.Exception)) {
						offScreenRun.SetError("The control threw on the window's thread: "+e.Exception.GetType().Name+": "+e.Exception.Message
							+" in "+e.Exception.TargetSite.DeclaringType.FullName+"."+e.Exception.TargetSite.Name);
					}
				}
				catch(Exception) {
					//Never throw from a first-chance handler.
				}
			};
			thread=new Thread(() => {
				//Before the thread's first window. Thread scope: the test host may have windows on other threads.
				Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException,true);
				Form formRun=null;
				Application.ThreadException+=(sender,e) => {
					offScreenRun.SetError("Unhandled on the window's thread: "+e.Exception);
					formRun?.Close();
				};
				using Form form=new Form();
				formRun=form;
				form.StartPosition=FormStartPosition.Manual;
				form.Location=new Point(-6000,-6000);//Off screen; WebView2 still lays out and runs the page.
				form.Size=new Size(600,500);
				form.ShowInTaskbar=false;
				form.FormBorderStyle=FormBorderStyle.None;
				form.Load+=async (sender,e) => {
					try {
						await funcRun(form,offScreenRun);
					}
					catch(Exception ex) {
						offScreenRun.SetError(ex.ToString());
					}
					form.Close();
				};
				Application.Run(form);
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground=true;
			//A message from the control (ODWebView2.ReportStartFailure) would block the window's thread: recorded as an error instead.
			Action<string> actionShowMessageOld=ODWebView2.ActionShowMessage;
			ODWebView2.ActionShowMessage=x => offScreenRun.SetError("The control showed a message: "+x);
			AppDomain.CurrentDomain.FirstChanceException+=eventHandlerFirstChance;
			thread.Start();
			bool isFinished=thread.Join(TimeSpan.FromMinutes(3));
			AppDomain.CurrentDomain.FirstChanceException-=eventHandlerFirstChance;
			if(!isFinished) {
				//The window's thread still runs: it keeps the recording seam, so it never shows a message box in the test host.
				offScreenRun.SetError("The WebView2 window did not finish within 3 minutes.");
				return offScreenRun;
			}
			ODWebView2.ActionShowMessage=actionShowMessageOld;
			return offScreenRun;
		}

		private static async Task Run(Form form,OffScreenRun offScreenRun,string folder,Results results) {
			CoreWebView2Environment coreWebView2Environment=await CreateEnvironmentAsync(folder);
			results.BrowserVersion=coreWebView2Environment.BrowserVersionString;
			await StartRuntime(form,coreWebView2Environment,offScreenRun);
			ODWebView2 odWebView2=new ODWebView2();
			odWebView2.Dock=DockStyle.Fill;
			form.Controls.Add(odWebView2);
			await odWebView2.EnsureCoreWebView2Async(coreWebView2Environment);
			odWebView2.LinkClicked+=(sender,e) => results.ListLinks.Add(e);
			odWebView2.CoreWebView2.NavigationStarting+=(sender,e) => {
				string kind;
				try {
					kind=e.NavigationKind.ToString();
				}
				catch(Exception) {
					kind="unknown";
				}
				results.ListNavigations.Add(e.Uri+" user="+e.IsUserInitiated+" kind="+kind);
			};
			odWebView2.CoreWebView2.FrameNavigationStarting+=(sender,e) => results.ListFrames.Add(e.Uri);
			string folderPage=Path.Combine(folder,"page");
			Directory.CreateDirectory(folderPage);
			File.WriteAllBytes(Path.Combine(folderPage,"dot.png"),GetPngBytes());
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
			//Two pages back to back without waiting (as when the user arrows through the inbox): the first page's own navigation starts after the
			//second took its place. Then Back, to a page that was replaced. Neither is a link the user clicked.
			results.CountLinksBeforeQuickPages=results.ListLinks.Count;
			bool isSecondShown=false;
			EventHandler<CoreWebView2NavigationCompletedEventArgs> eventHandler=(sender,e) => {
				if(e.IsSuccess && WebView2Policy.IsSameDocument(odWebView2.CoreWebView2.Source,odWebView2.DocumentUri)) {
					isSecondShown=true;
				}
			};
			odWebView2.NavigationCompleted+=eventHandler;
			odWebView2.NavigateToHtml(HtmlPage.Build("<p>first</p>","First",""),folderPage);
			results.UriFirstQuickPage=odWebView2.DocumentUri;
			odWebView2.NavigateToHtml(HtmlPage.Build("<p>second</p>","Second",""),folderPage);
			await WaitFor(() => isSecondShown,20000);
			odWebView2.NavigationCompleted-=eventHandler;
			await Task.Delay(1000);
			results.DocumentUriAfterQuickPages=odWebView2.DocumentUri;
			results.SourceAfterQuickPages=odWebView2.CoreWebView2.Source;
			results.CanGoBack=odWebView2.CoreWebView2.CanGoBack;
			int countNavigations=results.ListNavigations.Count;
			odWebView2.CoreWebView2.GoBack();
			await WaitFor(() => results.ListNavigations.Count>countNavigations,5000);
			await Task.Delay(1000);
			results.SourceAfterBack=odWebView2.CoreWebView2.Source;
			results.CountLinksAfterQuickPages=results.ListLinks.Count;
			string pathSecond=new Uri(odWebView2.DocumentUri).LocalPath;
			form.Controls.Remove(odWebView2);
			odWebView2.Dispose();
			results.IsDocumentFileDeleted=!File.Exists(results.PathDocument) && !File.Exists(pathRefresh) && !File.Exists(pathSecond);
		}

		[TestMethod]
		public void GeneratedPage_ShowsTextLocalImages_RunsNoScript_AndFollowsNoLink() {
			SkipWithoutRuntime();
			string folder=TestPaths.NewTempFolder("smoke");
			Results results=new Results();
			OffScreenRun offScreenRun;
			try {
				offScreenRun=RunOffScreen((form,run) => Run(form,run,folder,results));
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			offScreenRun.AssertNoError();
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
			Assert.AreEqual(2,results.CountLinksBeforeRefresh,"Each click raised LinkClicked once. "+details);
			Assert.AreEqual(EnumWikiLink.WikiPage,FormWiki.ParseLink(results.ListLinks[0].Uri,out string target),details);
			Assert.AreEqual("Test Page",target);
			Assert.AreEqual(results.DocumentUri,results.ListLinks[0].UriDocument);
			Assert.IsFalse(results.ListLinks[0].IsNewWindow);
			Assert.AreEqual("https://example.invalid/page",results.ListLinks[1].GetSystemBrowserUrl(),details);
			Assert.IsTrue(WebView2Policy.IsSameDocument(results.SourceAfterClicks,results.DocumentUri),"The page stayed: "+results.SourceAfterClicks);
			//The meta refresh.
			Assert.IsTrue(results.ListNavigations.Any(x => x.StartsWith("https://refresh.example.invalid") && x.Contains(" user=False")),
				"The page tried to navigate by itself. "+details);
			Assert.IsTrue(WebView2Policy.IsSameDocument(results.SourceAfterRefresh,results.DocumentUriAfterRefresh),"The refresh was cancelled: "
				+results.SourceAfterRefresh);
			//Two pages back to back, then Back.
			Assert.AreEqual(results.CountLinksBeforeRefresh,results.CountLinksBeforeQuickPages,
				"A navigation the page starts by itself never raises LinkClicked. "+details);
			Assert.IsTrue(WebView2Policy.IsSameDocument(results.SourceAfterQuickPages,results.DocumentUriAfterQuickPages),
				"The second of two pages shown back to back is the one shown: "+results.SourceAfterQuickPages+" "+details);
			//WebView2 starts the first page's navigation, counted as user initiated, after the second page took its place (checked with Runtime 156).
			Assert.IsTrue(results.ListNavigations.Any(x => x.StartsWith(results.UriFirstQuickPage+" ")),
				"The first page's own navigation started after the second page took its place. "+details);
			Assert.IsTrue(results.CanGoBack,"There is a page to go back to. "+details);
			Assert.IsTrue(results.ListNavigations.Last().Contains(" kind=BackOrForward") || results.ListNavigations.Last().Contains(" kind=unknown"),
				"Back started a navigation. "+details);
			Assert.IsTrue(WebView2Policy.IsSameDocument(results.SourceAfterBack,results.DocumentUriAfterQuickPages),"Back was cancelled: "
				+results.SourceAfterBack+" "+details);
			Assert.AreEqual(results.CountLinksBeforeQuickPages,results.CountLinksAfterQuickPages,
				"Neither the first of two pages shown back to back ("+results.UriFirstQuickPage+") nor Back raises LinkClicked. "+details);
			Assert.IsTrue(results.IsDocumentFileDeleted,"The page files are deleted when the page changes and when the control is disposed.");
		}

		///<summary>The page of the second test: one image per case, by id.</summary>
		private static string BuildResourcePage(string uriAllowedImage,string uriOutsideImage) {
			string body="<img id=\"own\" src=\"dot.png\">"
				+"<img id=\"allowed\" src=\""+uriAllowedImage+"\">"
				+"<img id=\"outside\" src=\""+uriOutsideImage+"\">"
				+"<img id=\"parent\" src=\"../outside/outside.png\">"
				+"<img id=\"system\" src=\"file:///C:/Windows/win.ini\">"
				+"<img id=\"remote\" src=\""+URL_REMOTE_IMAGE+"\">";
			return HtmlPage.Build(body,"Resources","margin:0");
		}

		private static readonly string[] _arrayResourceImageIds={ "own","allowed","outside","parent","system","remote" };

		///<summary>The naturalWidth of each image with one of the ids once every image finished (loaded or failed): 0 for an image that did not load.</summary>
		private static async Task<Dictionary<string,int>> ReadImageWidths(ODWebView2 odWebView2,IEnumerable<string> listIds) {
			DateTime dateTimeEnd=DateTime.Now.AddSeconds(15);
			while(DateTime.Now<dateTimeEnd) {
				string result=await odWebView2.CoreWebView2.ExecuteScriptAsync("Array.prototype.every.call(document.images,function(x){return x.complete;})");
				if(result=="true") {
					break;
				}
				await Task.Delay(100);
			}
			Dictionary<string,int> dictWidths=new Dictionary<string,int>();
			foreach(string id in listIds) {
				int width;
				int.TryParse(await odWebView2.CoreWebView2.ExecuteScriptAsync("document.getElementById('"+id+"').naturalWidth"),out width);
				dictWidths[id]=width;
			}
			return dictWidths;
		}

		private static async Task RunResources(Form form,OffScreenRun offScreenRun,string folder,ResourceResults results) {
			CoreWebView2Environment coreWebView2Environment=await CreateEnvironmentAsync(folder);
			results.BrowserVersion=coreWebView2Environment.BrowserVersionString;
			await StartRuntime(form,coreWebView2Environment,offScreenRun);
			ODWebView2 odWebView2=new ODWebView2();
			odWebView2.Dock=DockStyle.Fill;
			form.Controls.Add(odWebView2);
			await odWebView2.EnsureCoreWebView2Async(coreWebView2Environment);
			odWebView2.RemoteContentBlocked+=(sender,e) => results.CountRemoteBlocked++;
			byte[] byteArrayPng=GetPngBytes();
			List<Request> listRequests=results.ListRequestsUntrusted;
			//Runs after the control's own handler (subscribed when the control was initialized), so e.Response tells whether the control blocked
			//the request. Whatever the control lets through that is not a local file is answered here: the test never reaches the network.
			odWebView2.CoreWebView2.WebResourceRequested+=(sender,e) => {
				string uri=e.Request.Uri;
				Request request=new Request();
				request.Uri=uri;
				request.IsBlocked=e.Response!=null;
				listRequests.Add(request);
				if(request.IsBlocked || uri.StartsWith("file:///")) {
					return;
				}
				if(uri==URL_REMOTE_IMAGE) {
					e.Response=coreWebView2Environment.CreateWebResourceResponse(new MemoryStream(byteArrayPng),200,"OK","Content-Type: image/png");
					return;
				}
				e.Response=coreWebView2Environment.CreateWebResourceResponse(null,404,"Not found","");
			};
			string folderPage=Path.Combine(folder,"page");
			string folderAllowed=Path.Combine(folder,"allowed");
			string folderOutside=Path.Combine(folder,"outside");
			Directory.CreateDirectory(folderPage);
			Directory.CreateDirectory(folderAllowed);
			Directory.CreateDirectory(folderOutside);
			File.WriteAllBytes(Path.Combine(folderPage,"dot.png"),byteArrayPng);
			File.WriteAllBytes(Path.Combine(folderAllowed,"allowed.png"),byteArrayPng);
			File.WriteAllBytes(Path.Combine(folderOutside,"outside.png"),byteArrayPng);
			string uriAllowedImage=new Uri(Path.Combine(folderAllowed,"allowed.png")).AbsoluteUri;
			results.UriOutsideImage=new Uri(Path.Combine(folderOutside,"outside.png")).AbsoluteUri;
			string html=BuildResourcePage(uriAllowedImage,results.UriOutsideImage);
			//Html from outside the office (a received email): its own folder only, nothing from the web.
			await NavigateAndWait(odWebView2,html,folderPage);
			results.DictWidthsUntrusted=await ReadImageWidths(odWebView2,_arrayResourceImageIds);
			await Task.Delay(500);
			results.CountRemoteBlockedAfterUntrusted=results.CountRemoteBlocked;
			//Another computer's share, only once a local file outside the page's folder was seen to be blocked: then a file: request cannot get past
			//the control, and nothing is ever sent to unc.invalid.
			if(results.ListRequestsUntrusted.Any(x => x.Uri==results.UriOutsideImage && x.IsBlocked)) {
				results.IsShareChecked=true;
				listRequests=results.ListRequestsShare;
				string htmlShare=HtmlPage.Build("<img id=\"own\" src=\"dot.png\"><img id=\"share\" src=\""+URL_SHARE_IMAGE+"\">","Share","margin:0");
				await NavigateAndWait(odWebView2,htmlShare,folderPage);
				results.DictWidthsShare=await ReadImageWidths(odWebView2,new[] { "own","share" });
			}
			//The office's own page (a wiki page, a sent email): the allowed folder and the web too.
			listRequests=results.ListRequestsTrusted;
			await NavigateAndWait(odWebView2,html,folderPage,isRemoteContentAllowed:true,listFoldersAllowed:new List<string> { folderAllowed });
			results.DictWidthsTrusted=await ReadImageWidths(odWebView2,_arrayResourceImageIds);
			await Task.Delay(500);
			form.Controls.Remove(odWebView2);
			odWebView2.Dispose();
		}

		[TestMethod]
		public void GeneratedPage_LoadsOnlyItsFoldersFiles_AndTheWebOnlyWhenAllowed() {
			SkipWithoutRuntime();
			string folder=TestPaths.NewTempFolder("resources");
			ResourceResults results=new ResourceResults();
			OffScreenRun offScreenRun;
			try {
				offScreenRun=RunOffScreen((form,run) => RunResources(form,run,folder,results));
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			offScreenRun.AssertNoError();
			Func<List<Request>,string> funcRequests=list => string.Join(" | ",list.Select(x => x.Uri+(x.IsBlocked ? " blocked" : "")));
			string details="WebView2 Runtime "+results.BrowserVersion
				+"; untrusted: "+string.Join(",",results.DictWidthsUntrusted.Select(x => x.Key+"="+x.Value))+" requests: "+funcRequests(results.ListRequestsUntrusted)
				+"; share: "+string.Join(",",results.DictWidthsShare.Select(x => x.Key+"="+x.Value))+" requests: "+funcRequests(results.ListRequestsShare)
				+"; trusted: "+string.Join(",",results.DictWidthsTrusted.Select(x => x.Key+"="+x.Value))+" requests: "+funcRequests(results.ListRequestsTrusted);
			Console.WriteLine(details);
			//Html from outside the office.
			Assert.AreEqual(8,results.DictWidthsUntrusted["own"],"The image in the page's folder loads. "+details);
			foreach(string id in new[] { "allowed","outside","parent","system","remote" }) {
				Assert.AreEqual(0,results.DictWidthsUntrusted[id],id+" does not load. "+details);
			}
			foreach(string uri in new[] { results.UriOutsideImage,"file:///C:/Windows/win.ini",URL_REMOTE_IMAGE }) {
				Assert.IsTrue(results.ListRequestsUntrusted.Any(x => x.Uri==uri && x.IsBlocked),uri+" was refused by the control. "+details);
			}
			Assert.AreEqual(1,results.CountRemoteBlockedAfterUntrusted,"RemoteContentBlocked once for the page. "+details);
			//Another computer's share.
			Assert.IsTrue(results.IsShareChecked,"A local file outside the page's folder reached the control's filter. "+details);
			Assert.AreEqual(8,results.DictWidthsShare["own"],details);
			Assert.AreEqual(0,results.DictWidthsShare["share"],details);
			Assert.IsTrue(results.ListRequestsShare.Any(x => x.Uri==URL_SHARE_IMAGE && x.IsBlocked),
				"The image on another computer's share was refused by the control before Windows opened it. "+details);
			Assert.IsFalse(results.ListRequestsShare.Concat(results.ListRequestsUntrusted).Concat(results.ListRequestsTrusted)
				.Any(x => x.Uri.StartsWith("file://unc.invalid") && !x.IsBlocked),details);
			//The office's own page.
			foreach(string id in new[] { "own","allowed","remote" }) {
				Assert.AreEqual(8,results.DictWidthsTrusted[id],id+" loads. "+details);
			}
			foreach(string id in new[] { "outside","parent","system" }) {
				Assert.AreEqual(0,results.DictWidthsTrusted[id],id+" does not load. "+details);
			}
			Assert.IsTrue(results.ListRequestsTrusted.Any(x => x.Uri==URL_REMOTE_IMAGE && !x.IsBlocked),"The web image passed the control. "+details);
			Assert.AreEqual(1,results.CountRemoteBlocked,"Nothing more was blocked from the web. "+details);
		}
	}
}
