// No temporary file while a wiki page or an email is shown (owner answer of 2026-11-02 to LIB-WEB question 2): a wiki page with an image from
// the wiki folder, and a received email after Show Images with its image in the page's own folder, show in the WebView2 wrapper from memory.
// The program's temp folder and the temp folder itself are compared before and after: no new file holds the pages' text or the email's image,
// and no page file appears. The test's own WebView2 profile is searched as well, so the browser keeps no copy of either page on disk. Runs only
// with the WebView2 Runtime (Inconclusive otherwise, as the other smoke tests); every address is local.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;

namespace LibWebTests {
	public partial class WebView2SmokeTests {
		///<summary>What the in-memory test's window saw.</summary>
		private class InMemoryResults {
			public string BrowserVersion="";
			public bool IsWikiLoaded;
			public string TextWiki;
			public int WidthWikiImage;
			public bool IsWikiInMemory;
			public bool IsEmailLoaded;
			public string TextEmail;
			public int WidthEmailImage;
			public bool IsEmailInMemory;
			public string UriEmail;
			public List<Request> ListRequests=new List<Request>();
			///<summary>The temporary files that held a page's text or the email's image while the pages were shown (an older version deleted its
			///page files when the page changed, so only a look while a page is shown finds them).</summary>
			public List<string> ListTempCopiesWhileShown=new List<string>();
		}

		///<summary>Records every file created in a folder while the test runs, also one deleted again before the test looks.</summary>
		private class FolderWatch:IDisposable {
			private FileSystemWatcher _fileSystemWatcher;
			private List<string> _listCreated=new List<string>();

			public FolderWatch(string folder,bool isRecursive) {
				_fileSystemWatcher=new FileSystemWatcher(folder);
				_fileSystemWatcher.IncludeSubdirectories=isRecursive;
				_fileSystemWatcher.NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName;
				_fileSystemWatcher.InternalBufferSize=64*1024;
				_fileSystemWatcher.Created+=(sender,e) => Add(e.FullPath);
				_fileSystemWatcher.Renamed+=(sender,e) => Add(e.FullPath);
				_fileSystemWatcher.EnableRaisingEvents=true;
			}

			private void Add(string path) {
				lock(_listCreated) {
					_listCreated.Add(path);
				}
			}

			public List<string> GetCreated() {
				lock(_listCreated) {
					return _listCreated.ToList();
				}
			}

			public void Dispose() {
				_fileSystemWatcher.Dispose();
			}
		}

		///<summary>The size and time of each file under folder (and its sub folders), by path. Empty when folder is missing; a file that goes away
		///while it is listed is left out.</summary>
		private static Dictionary<string,string> ListFiles(string folder,bool isRecursive) {
			Dictionary<string,string> dictFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			if(!Directory.Exists(folder)) {
				return dictFiles;
			}
			IEnumerable<string> listPaths;
			try {
				listPaths=Directory.EnumerateFiles(folder,"*",isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
			}
			catch(Exception) {
				//Another process changed the folder while it was listed: the top folder alone.
				listPaths=Directory.GetFiles(folder);
			}
			foreach(string path in listPaths) {
				try {
					FileInfo fileInfo=new FileInfo(path);
					dictFiles[path]=fileInfo.Length+"|"+fileInfo.LastWriteTimeUtc.Ticks;
				}
				catch(Exception) {
				}
			}
			return dictFiles;
		}

		///<summary>The files of dictAfter that are not in dictBefore, or changed.</summary>
		private static List<string> GetNewFiles(Dictionary<string,string> dictBefore,Dictionary<string,string> dictAfter) {
			return dictAfter.Where(x => !dictBefore.TryGetValue(x.Key,out string stamp) || stamp!=x.Value).Select(x => x.Key).ToList();
		}

		///<summary>The files among listPaths that hold one of listNeedles. A file that cannot be read (in use) is left out; files over 64 MB too.</summary>
		private static List<string> FindFilesHolding(IEnumerable<string> listPaths,List<byte[]> listNeedles) {
			List<string> listFound=new List<string>();
			foreach(string path in listPaths) {
				byte[] byteArray;
				try {
					FileInfo fileInfo=new FileInfo(path);
					if(!fileInfo.Exists || fileInfo.Length>64*1024*1024) {
						continue;
					}
					using FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
					using MemoryStream memoryStream=new MemoryStream();
					fileStream.CopyTo(memoryStream);
					byteArray=memoryStream.ToArray();
				}
				catch(Exception) {
					continue;
				}
				if(listNeedles.Any(x => IndexOf(byteArray,x)>=0)) {
					listFound.Add(path);
				}
			}
			return listFound;
		}

		private static int IndexOf(byte[] byteArray,byte[] byteArrayNeedle) {
			if(byteArrayNeedle.Length==0) {
				return -1;
			}
			byte first=byteArrayNeedle[0];
			for(int i=0;i<=byteArray.Length-byteArrayNeedle.Length;i++) {
				if(byteArray[i]!=first) {
					continue;
				}
				int j=1;
				while(j<byteArrayNeedle.Length && byteArray[i+j]==byteArrayNeedle[j]) {
					j++;
				}
				if(j==byteArrayNeedle.Length) {
					return i;
				}
			}
			return -1;
		}

		///<summary>An image no other test writes: 9x7, in a color of its own.</summary>
		private static byte[] GetEmailImageBytes() {
			using Bitmap bitmap=new Bitmap(9,7);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.FromArgb(255,(byte)DateTime.Now.Millisecond,77,(byte)(DateTime.Now.Second*4)));
			}
			using MemoryStream memoryStream=new MemoryStream();
			bitmap.Save(memoryStream,ImageFormat.Png);
			return memoryStream.ToArray();
		}

		private static async Task<string> ReadText(ODWebView2 odWebView2,string id) {
			return ReadJsonString(await odWebView2.CoreWebView2.ExecuteScriptAsync("document.getElementById('"+id+"').textContent"));
		}

		private static async Task RunInMemory(Form form,OffScreenRun offScreenRun,string folder,string markerWiki,string markerEmail,byte[] byteArrayEmailImage,
			Func<List<string>> funcFindTempCopies,InMemoryResults results)
		{
			CoreWebView2Environment coreWebView2Environment=await CreateEnvironmentAsync(folder);
			results.BrowserVersion=coreWebView2Environment.BrowserVersionString;
			await StartRuntime(form,coreWebView2Environment,offScreenRun);
			ODWebView2 odWebView2=new ODWebView2();
			odWebView2.Dock=DockStyle.Fill;
			form.Controls.Add(odWebView2);
			await odWebView2.EnsureCoreWebView2Async(coreWebView2Environment);
			odWebView2.CoreWebView2.WebResourceRequested+=(sender,e) => {
				Request request=new Request();
				request.Uri=e.Request.Uri;
				request.StatusCode=e.Response?.StatusCode??0;
				request.IsBlocked=request.StatusCode==403;
				results.ListRequests.Add(request);
				if(e.Response==null && !request.Uri.StartsWith("file:///")) {
					e.Response=coreWebView2Environment.CreateWebResourceResponse(null,404,"Not found","");//Never the network.
				}
			};
			//A wiki page: its image from the wiki folder of the A to Z folder, by the file: address MarkupEdit.TranslateToXhtml gives it, the
			//folders and the web as FormWiki allows them.
			string folderWiki=Path.Combine(folder,"AtoZ","Wiki");
			Directory.CreateDirectory(folderWiki);
			string pathWikiImage=Path.Combine(folderWiki,"wiki image.png");
			File.WriteAllBytes(pathWikiImage,GetPngBytes());
			string bodyWiki="<p id=\"text\">"+HtmlPage.Encode(markerWiki)+"</p><img id=\"img\" src=\"file:///"+pathWikiImage.Replace("\\","/")+"\">";
			results.IsWikiLoaded=await NavigateAndWait(odWebView2,HtmlPage.Build(bodyWiki,"Wiki",""),null,isRemoteContentAllowed:true,
				listFoldersAllowed:new List<string> { folderWiki,WebView2PageStore.GetSharedFolder() });
			results.WidthWikiImage=(await ReadImageWidths(odWebView2,new[] { "img" }))["img"];
			results.TextWiki=await ReadText(odWebView2,"text");
			results.IsWikiInMemory=IsShownFromMemory(odWebView2);
			results.ListTempCopiesWhileShown.AddRange(funcFindTempCopies());
			//A received email after Show Images: its image in the page's own folder by the name that replaced its cid: address
			//(EmailPreviewControl.ParseAttachedImages), the web allowed, no other folder.
			string bodyEmail="<p id=\"text\">"+HtmlPage.Encode(markerEmail)+"</p><img id=\"img\" src=\"image001.png\">";
			results.IsEmailLoaded=await NavigateAndWait(odWebView2,HtmlPage.Build(bodyEmail,"Email",""),
				new Dictionary<string,byte[]> { { "image001.png",byteArrayEmailImage } },isRemoteContentAllowed:true);
			results.WidthEmailImage=(await ReadImageWidths(odWebView2,new[] { "img" }))["img"];
			results.TextEmail=await ReadText(odWebView2,"text");
			results.IsEmailInMemory=IsShownFromMemory(odWebView2);
			results.UriEmail=odWebView2.DocumentUri;
			results.ListTempCopiesWhileShown.AddRange(funcFindTempCopies());
			await Task.Delay(500);
			form.Controls.Remove(odWebView2);
			odWebView2.Dispose();
		}

		[TestMethod]
		public void WikiPageAndEmail_AreShownFromMemory_AndWriteNoTemporaryFile() {
			SkipWithoutRuntime();
			string folder=TestPaths.NewTempFolder("inmemory");
			string folderProgramTemp=Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName);
			string markerWiki="Wiki page text "+Guid.NewGuid().ToString("N");
			string markerEmail="Patient email text "+Guid.NewGuid().ToString("N");
			byte[] byteArrayEmailImage=GetEmailImageBytes();
			List<byte[]> listNeedles=new List<byte[]> { Encoding.UTF8.GetBytes(markerWiki),Encoding.UTF8.GetBytes(markerEmail),
				Encoding.Unicode.GetBytes(markerWiki),Encoding.Unicode.GetBytes(markerEmail),byteArrayEmailImage };
			Directory.CreateDirectory(folderProgramTemp);//The program makes it whenever it needs it; the watch needs it now.
			Dictionary<string,string> dictProgramTempBefore=ListFiles(folderProgramTemp,isRecursive:true);
			Dictionary<string,string> dictTempBefore=ListFiles(Path.GetTempPath(),isRecursive:false);
			//New files, now: in the program's temp folder (where the pages were written) and in the temp folder itself.
			Func<List<string>> funcFindTempCopies=() => FindFilesHolding(GetNewFiles(dictProgramTempBefore,ListFiles(folderProgramTemp,isRecursive:true))
				.Concat(GetNewFiles(dictTempBefore,ListFiles(Path.GetTempPath(),isRecursive:false))),listNeedles);
			InMemoryResults results=new InMemoryResults();
			OffScreenRun offScreenRun;
			List<string> listNewProgramTemp;
			List<string> listCreatedProgramTemp;
			List<string> listTempCopiesAfter;
			List<string> listProfileHolding;
			try {
				using FolderWatch folderWatch=new FolderWatch(folderProgramTemp,isRecursive:true);
				offScreenRun=RunOffScreen((form,run) => RunInMemory(form,run,folder,markerWiki,markerEmail,byteArrayEmailImage,funcFindTempCopies,results));
				listCreatedProgramTemp=folderWatch.GetCreated();
				listNewProgramTemp=GetNewFiles(dictProgramTempBefore,ListFiles(folderProgramTemp,isRecursive:true));
				listTempCopiesAfter=funcFindTempCopies();
				listProfileHolding=FindFilesHolding(ListFiles(Path.Combine(folder,"data"),isRecursive:true).Keys,listNeedles);
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			offScreenRun.AssertNoError();
			string details="WebView2 Runtime "+results.BrowserVersion+"; requests: "
				+string.Join(" | ",results.ListRequests.Select(x => x.Uri+" "+x.StatusCode))
				+"; created in the program's temp folder (other test runs may write there): "+string.Join(" | ",listCreatedProgramTemp);
			Console.WriteLine(details);
			//Both pages show, with their images.
			Assert.IsTrue(results.IsWikiLoaded,details);
			Assert.AreEqual(markerWiki,results.TextWiki,details);
			Assert.AreEqual(8,results.WidthWikiImage,"The wiki image loads from the wiki folder. "+details);
			Assert.IsTrue(results.IsWikiInMemory,"The wiki page is in memory, not on disk. "+details);
			Assert.IsTrue(results.IsEmailLoaded,details);
			Assert.AreEqual(markerEmail,results.TextEmail,details);
			Assert.AreEqual(9,results.WidthEmailImage,"The email's image loads from memory. "+details);
			Assert.IsTrue(results.IsEmailInMemory,"The email is in memory, not on disk. "+details);
			Assert.IsTrue(results.ListRequests.Any(x => x.Uri==results.UriEmail && x.StatusCode==200),"The control answered the email page itself. "+details);
			//No temporary file: nothing new holds either page's text or the email's image, while the pages were shown or after, and no page file
			//or folder of the in-memory pages was created, not even for a moment.
			Assert.AreEqual(0,results.ListTempCopiesWhileShown.Count,"A temporary file held a page or the email's image while it was shown: "
				+string.Join(" | ",results.ListTempCopiesWhileShown));
			Assert.AreEqual(0,listTempCopiesAfter.Count,"A new temporary file holds a page or the email's image: "+string.Join(" | ",listTempCopiesAfter));
			List<string> listPageFiles=listCreatedProgramTemp.Concat(listNewProgramTemp).Where(x => x.EndsWith(".html",StringComparison.OrdinalIgnoreCase)
				|| x.EndsWith(".htm",StringComparison.OrdinalIgnoreCase) || WebView2PageStore.IsPathInRoot(x)).Distinct().ToList();
			Assert.AreEqual(0,listPageFiles.Count,"A page file was written: "+string.Join(" | ",listPageFiles));
			Assert.IsFalse(Directory.Exists(WebView2PageStore.GetRootFolder()),"The folder of the in-memory pages is never created.");
			//Nor does the browser keep a copy in its profile (its cache, for example).
			Assert.AreEqual(0,listProfileHolding.Count,"The WebView2 profile holds a page or the email's image: "+string.Join(" | ",listProfileHolding));
		}
	}
}
