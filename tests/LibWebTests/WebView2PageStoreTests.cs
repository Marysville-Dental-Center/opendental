// The in-memory pages of ODWebView2.NavigateToHtml (CodeBase.Controls.WebView2PageStore), without the browser: a page and the files of its own
// folder are found by the file: address the browser asks for, and nothing else is; a replaced page is gone; nothing is written to disk; the
// shared folder of the wiki's cloud images replaces an image shown again and keeps at most SharedBytesMax; a link to an in-memory file is never
// a web address; what a page may load is the same as for a page on disk; and the one-time clean-up deletes only the page files an older version
// left in the temp folder.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBase;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibWebTests {
	[TestClass]
	public class WebView2PageStoreTests {
		private static readonly byte[] _byteArrayImage={ 0x89,0x50,0x4E,0x47,1,2,3 };

		///<summary>The address the browser gives for a file of folder, as it escapes it (a space is %20).</summary>
		private static string GetUri(string folder,string name) {
			return new Uri(Path.Combine(folder,name)).AbsoluteUri;
		}

		[TestMethod]
		public void AddPage_ThePageAndItsFiles_AreFoundByTheirAddress_AndNothingIsOnDisk() {
			string html="<p>Jane Doe \u00e9\u00e8 \u4e2d</p><img src=\"my image.png\">";
			Dictionary<string,byte[]> dictFiles=new Dictionary<string,byte[]> { { "my image.png",_byteArrayImage },{ "LOGO.GIF",new byte[] { 0x47,0x49 } } };
			WebView2Page webView2Page=WebView2PageStore.AddPage(html,dictFiles);
			try {
				StringAssert.StartsWith(webView2Page.Folder,WebView2PageStore.GetRootFolder()+"\\");
				Assert.AreEqual(Path.Combine(webView2Page.Folder,WebView2PageStore.PageFileName),webView2Page.Path);
				Assert.AreEqual(new Uri(webView2Page.Path).AbsoluteUri,webView2Page.Uri);
				Assert.IsTrue(WebView2PageStore.IsInRoot(webView2Page.Uri));
				//The page: UTF-8 with a byte order mark, so it reads as UTF-8 whatever its meta tags say.
				Assert.IsTrue(WebView2PageStore.TryGetFile(webView2Page.Uri,out byte[] content,out string contentType));
				Assert.AreEqual("text/html; charset=utf-8",contentType);
				CollectionAssert.AreEqual(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(html)).ToArray(),content);
				Assert.IsTrue(WebView2PageStore.TryGetFile(webView2Page.Uri+"#top",out content,out _),"An in-page bookmark is the page.");
				//Its files: by the escaped address the browser asks for, in any letter case.
				string uriImage=GetUri(webView2Page.Folder,"my image.png");
				StringAssert.Contains(uriImage,"my%20image.png");
				Assert.IsTrue(WebView2PageStore.TryGetFile(uriImage,out content,out contentType));
				CollectionAssert.AreEqual(_byteArrayImage,content);
				Assert.AreEqual("image/png",contentType);
				Assert.IsTrue(WebView2PageStore.TryGetFile(GetUri(webView2Page.Folder,"logo.gif"),out content,out contentType));
				Assert.AreEqual("image/gif",contentType);
				CollectionAssert.AreEquivalent(new[] { WebView2PageStore.PageFileName,"my image.png","LOGO.GIF" },webView2Page.GetFileNames());
				//Nothing was written.
				Assert.IsFalse(Directory.Exists(WebView2PageStore.GetRootFolder()),"The folder of the in-memory pages is never created.");
				Assert.IsFalse(File.Exists(webView2Page.Path));
				Assert.IsTrue(WebView2PageStore.Exists(Path.Combine(webView2Page.Folder,"my image.png")));
			}
			finally {
				WebView2PageStore.RemovePage(webView2Page);
			}
			Assert.IsFalse(WebView2PageStore.TryGetFile(webView2Page.Uri,out _,out _),"A page that was removed is gone.");
			Assert.IsFalse(WebView2PageStore.TryGetFile(GetUri(webView2Page.Folder,"my image.png"),out _,out _),"And its files.");
			Assert.IsFalse(WebView2PageStore.Exists(Path.Combine(webView2Page.Folder,"my image.png")));
		}

		[TestMethod]
		public void TryGetFile_OnlyFilesOfAPageShownNow_NeverAnythingElse() {
			WebView2Page webView2Page=WebView2PageStore.AddPage("<p>a</p>",new Dictionary<string,byte[]> { { "a.png",_byteArrayImage } });
			WebView2Page webView2PageOther=WebView2PageStore.AddPage("<p>b</p>");
			try {
				string root=WebView2PageStore.GetRootFolder();
				foreach(string uri in new[] {
					GetUri(webView2Page.Folder,"b.png"),//A name the page does not have.
					new Uri(webView2Page.Folder).AbsoluteUri,//Its folder.
					new Uri(root).AbsoluteUri,
					GetUri(root,"page.html"),
					GetUri(Path.Combine(root,Guid.NewGuid().ToString("N")),"page.html"),//No such page.
					GetUri(webView2PageOther.Folder,"a.png"),//Another page does not have it.
					webView2Page.Uri.Replace("/page.html","/..%2Fpage.html"),
					webView2Page.Uri.Replace("/page.html","/..%5C"+Path.GetFileName(webView2Page.Folder)+"/page.html"),
					webView2Page.Uri+":stream",
					new Uri(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName,"page.html")).AbsoluteUri,//Outside the root.
					"file://unc.invalid/s/page.html","https://img.invalid/a.png","data:image/png;base64,iVBORw0KGgo=",null,"" })
				{
					Assert.IsFalse(WebView2PageStore.TryGetFile(uri,out _,out _),uri??"null");
				}
				Assert.IsTrue(WebView2PageStore.TryGetFile(webView2PageOther.Uri,out _,out _));
				//Under the root, whatever the name: answered from memory or refused, never read from the disk.
				Assert.IsTrue(WebView2PageStore.IsInRoot(GetUri(webView2Page.Folder,"b.png")));
				Assert.IsTrue(WebView2PageStore.IsInRoot(new Uri(root).AbsoluteUri));
				Assert.IsFalse(WebView2PageStore.IsInRoot(new Uri(root+"-x\\a.png").AbsoluteUri),"A folder that only starts like the root.");
				Assert.IsFalse(WebView2PageStore.IsInRoot("https://img.invalid/a.png"));
			}
			finally {
				WebView2PageStore.RemovePage(webView2Page);
				WebView2PageStore.RemovePage(webView2PageOther);
			}
		}

		[TestMethod]
		public void AddPage_LeavesOutNamesWithAFolder_AndNeverReplacesThePage() {
			Dictionary<string,byte[]> dictFiles=new Dictionary<string,byte[]> {
				{ "..\\up.png",_byteArrayImage },{ "sub/a.png",_byteArrayImage },{ "c:x.png",_byteArrayImage },{ "",_byteArrayImage },
				{ "null.png",null },{ "PAGE.HTML",Encoding.UTF8.GetBytes("<p>not the page</p>") },{ "ok.jpg",_byteArrayImage } };
			WebView2Page webView2Page=WebView2PageStore.AddPage("<p>the page</p>",dictFiles);
			try {
				CollectionAssert.AreEquivalent(new[] { WebView2PageStore.PageFileName,"ok.jpg" },webView2Page.GetFileNames());
				Assert.IsTrue(WebView2PageStore.TryGetFile(webView2Page.Uri,out byte[] content,out _));
				StringAssert.Contains(Encoding.UTF8.GetString(content),"the page");
				Assert.IsTrue(WebView2PageStore.TryGetFile(GetUri(webView2Page.Folder,"ok.jpg"),out _,out string contentType));
				Assert.AreEqual("image/jpeg",contentType);
			}
			finally {
				WebView2PageStore.RemovePage(webView2Page);
			}
			WebView2PageStore.RemovePage(null);//Nothing to do.
		}

		[TestMethod]
		public void Response_IsNeverCached_AndNamesItsType() {
			string headers=WebView2PageStore.GetResponseHeaders("image/png");
			StringAssert.Contains(headers,"Content-Type: image/png");
			StringAssert.Contains(headers,"Cache-Control: no-store");
			StringAssert.Contains(headers,"Referrer-Policy: no-referrer");
			Assert.AreEqual("application/octet-stream",WebView2PageStore.GetContentType("a.exe"));
			Assert.AreEqual("application/octet-stream",WebView2PageStore.GetContentType(null));
			Assert.AreEqual("image/bmp",WebView2PageStore.GetContentType("A.BMP"));
			Assert.AreEqual("image/tiff",WebView2PageStore.GetContentType("a.tif"));
		}

		[TestMethod]
		public void SharedFolder_ReplacesAnImageShownAgain_AndKeepsTheNewestUpToItsLimit() {
			WebView2PageStore.ClearShared();
			try {
				string pathFirst=WebView2PageStore.AddSharedFile(@"\\cloud\AtoZ\Wiki\a.png",_byteArrayImage,".png");
				StringAssert.StartsWith(pathFirst,WebView2PageStore.GetSharedFolder()+"\\");
				Assert.IsTrue(pathFirst.EndsWith(".png"));
				Assert.IsFalse(File.Exists(pathFirst));
				string pathAgain=WebView2PageStore.AddSharedFile(@"\\cloud\atoz\wiki\A.PNG",new byte[] { 9,9 },"png");
				Assert.AreEqual(pathFirst,pathAgain,"The same image again: the same address, replaced.");
				Assert.AreEqual(2,WebView2PageStore.BytesShared);
				Assert.IsTrue(WebView2PageStore.TryGetFile(new Uri(pathFirst).AbsoluteUri,out byte[] content,out string contentType));
				CollectionAssert.AreEqual(new byte[] { 9,9 },content);
				Assert.AreEqual("image/png",contentType);
				//Over the limit, the oldest go first.
				int size=(int)(WebView2PageStore.SharedBytesMax/3)+1;
				List<string> listPaths=new List<string>();
				for(int i=0;i<4;i++) {
					listPaths.Add(WebView2PageStore.AddSharedFile(@"\\cloud\AtoZ\Wiki\big"+i+".jpg",new byte[size],".jpg"));
				}
				Assert.IsTrue(WebView2PageStore.BytesShared<=WebView2PageStore.SharedBytesMax);
				Assert.IsFalse(WebView2PageStore.Exists(pathFirst),"The oldest went.");
				Assert.IsFalse(WebView2PageStore.Exists(listPaths[0]));
				Assert.IsTrue(WebView2PageStore.Exists(listPaths[2]));
				Assert.IsTrue(WebView2PageStore.Exists(listPaths[3]),"The newest stays.");
				Assert.IsFalse(Directory.Exists(WebView2PageStore.GetRootFolder()));
				//An odd extension gives a name without one.
				StringAssert.DoesNotMatch(Path.GetFileName(WebView2PageStore.AddSharedFile("x",_byteArrayImage,"a/b")),new System.Text.RegularExpressions.Regex("[/\\\\]"));
			}
			finally {
				WebView2PageStore.ClearShared();
			}
		}

		[TestMethod]
		public void Links_ToAnInMemoryFile_AreNeverWebAddresses() {
			WebView2Page webView2Page=WebView2PageStore.AddPage("<a href=\"image001.png\">",
				new Dictionary<string,byte[]> { { "image001.png",_byteArrayImage } });
			try {
				Assert.AreEqual("",WebView2Policy.GetSystemBrowserUrl(GetUri(webView2Page.Folder,"image001.png"),webView2Page.Uri),
					"An image of the page is a file, not http://image001.png/.");
				Assert.AreEqual("http://www.example.com/",WebView2Policy.GetSystemBrowserUrl(GetUri(webView2Page.Folder,"www.example.com"),webView2Page.Uri),
					"A relative link the page has no file for stays a web address, as with the old browser control.");
				Assert.AreEqual("",WebView2Policy.GetSystemBrowserUrl(webView2Page.Uri,webView2Page.Uri));
				Assert.AreEqual("https://www.example.com/a",WebView2Policy.GetSystemBrowserUrl("https://www.example.com/a",webView2Page.Uri));
			}
			finally {
				WebView2PageStore.RemovePage(webView2Page);
			}
		}

		[TestMethod]
		public void WhatAnInMemoryPageMayLoad_IsWhatAPageOnDiskMayLoad() {
			WebView2Page webView2Page=WebView2PageStore.AddPage("<p>a</p>",new Dictionary<string,byte[]> { { "a.png",_byteArrayImage } });
			WebView2Page webView2PageOther=WebView2PageStore.AddPage("<p>b</p>",new Dictionary<string,byte[]> { { "b.png",_byteArrayImage } });
			try {
				List<string> listNone=new List<string>();
				Assert.IsTrue(WebView2Policy.IsPageResourceAllowed(webView2Page.Uri,webView2Page.Uri,listNone,false));
				Assert.IsTrue(WebView2Policy.IsPageResourceAllowed(GetUri(webView2Page.Folder,"a.png"),webView2Page.Uri,listNone,false));
				//Another page's files, the shared folder, the rest of the temp folder: only when allowed.
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(GetUri(webView2PageOther.Folder,"b.png"),webView2Page.Uri,listNone,false));
				string uriShared=new Uri(WebView2PageStore.AddSharedFile("k",_byteArrayImage,".png")).AbsoluteUri;
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(uriShared,webView2Page.Uri,listNone,false),"A received email never gets the wiki's images.");
				Assert.IsTrue(WebView2Policy.IsPageResourceAllowed(uriShared,webView2Page.Uri,new List<string> { WebView2PageStore.GetSharedFolder() },false),
					"A wiki page gets them (MarkupEdit.GetImageFolders).");
				Assert.IsFalse(WebView2Policy.IsPageResourceAllowed(new Uri(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName,"x.png")).AbsoluteUri,
					webView2Page.Uri,listNone,false),"The program's temp folder is no longer a page's own folder.");
			}
			finally {
				WebView2PageStore.RemovePage(webView2Page);
				WebView2PageStore.RemovePage(webView2PageOther);
				WebView2PageStore.ClearShared();
			}
		}

		[TestMethod]
		public void DeleteOldPageFiles_OnlyThePagesAnOlderVersionLeft() {
			string folder=TestPaths.NewTempFolder("oldpages");
			try {
				string pageOld=Path.Combine(folder,"ABC12320261102093000.html");
				string folderShowImages=Path.Combine(folder,"XYZ98720261102093100");
				string folderOther=Path.Combine(folder,"QWE45620261102093200");//A random folder of something else: no page in it.
				Directory.CreateDirectory(folderShowImages);
				Directory.CreateDirectory(folderOther);
				File.WriteAllText(pageOld,"<p>old</p>");
				File.WriteAllText(Path.Combine(folderShowImages,"DEF45620261102093100.html"),"<p>email</p>");
				File.WriteAllBytes(Path.Combine(folderShowImages,"image001.png"),_byteArrayImage);
				string[] arrayKept={ Path.Combine(folder,"report.html"),Path.Combine(folder,"ABC12320261102093000.pdf"),
					Path.Combine(folder,"abc12320261102093000x.html"),Path.Combine(folderOther,"image001.png"),Path.Combine(folder,"ABC1232026110209300.html") };
				foreach(string path in arrayKept) {
					File.WriteAllText(path,"kept");
				}
				//Every file above was written two hours ago; a page an older version running now has just written stays.
				DateTime dateTimeOld=DateTime.Now.AddHours(-2);
				foreach(string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories)) {
					File.SetLastWriteTime(path,dateTimeOld);
				}
				string pageRecent=Path.Combine(folder,"GHI78920261102120000.html");
				File.WriteAllText(pageRecent,"<p>shown now</p>");
				Assert.AreEqual(3,WebView2PageStore.DeleteOldPageFiles(folder,DateTime.Now.AddHours(-1)));
				Assert.IsFalse(File.Exists(pageOld));
				Assert.IsFalse(Directory.Exists(folderShowImages),"An email's Show Images folder goes with its page.");
				foreach(string path in arrayKept.Concat(new[] { pageRecent })) {
					Assert.IsTrue(File.Exists(path),path);
				}
				Assert.AreEqual(0,WebView2PageStore.DeleteOldPageFiles(folder,DateTime.Now.AddHours(-1)));
				Assert.AreEqual(0,WebView2PageStore.DeleteOldPageFiles(Path.Combine(folder,"missing"),DateTime.Now));
				Assert.AreEqual(0,WebView2PageStore.DeleteOldPageFiles(null,DateTime.Now));
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
		}

		[TestMethod]
		public void NoPageFileIsWritten_AnyMore() {
			//ODWebView2 shows pages from memory only; the email preview's Show Images keeps the images in memory too.
			string control=TestPaths.ReadSource("CodeBase","Controls","ODWebView2.cs");
			foreach(string text in new[] { "File.Write","CreateRandomFile","GetPageFolder","File.Delete","FileStream" }) {
				Assert.IsFalse(control.Contains(text),"ODWebView2.cs: "+text);
			}
			StringAssert.Contains(control,"WebView2PageStore.AddPage(pagePending.Html,pagePending.DictFiles)");
			StringAssert.Contains(control,"WebView2PageStore.RemovePage(pageOld)");
			string email=TestPaths.ReadSource("OpenPmsDental","User Controls","EmailPreviewControl.cs");
			foreach(string text in new[] { "CreateRandomFolder","SaveMimeImageToFile","htmlFolderPath","GetTempFolderPath" }) {
				Assert.IsFalse(email.Contains(text),"EmailPreviewControl.cs: "+text);
			}
			StringAssert.Contains(email,"EmailMessages.GetMimeImageBytes(entity,attachment?.ActualFileName)");
			string markup=TestPaths.ReadSource("OpenPmsDentalBusiness","Db Multi Table","MarkupEdit.cs");
			StringAssert.Contains(markup,"CodeBase.Controls.WebView2PageStore.AddSharedFile(fullPath,byteArray,Path.GetExtension(fullPath))",
				"The wiki's images from cloud storage stay in memory.");
			Assert.IsFalse(TestPaths.ReadSource("OpenPmsDentalBusiness","Data Interface","EmailMessages.cs").Contains("SaveMimeImageToFile"));
		}
	}
}
