// The pages that ODWebView2.NavigateToHtml shows (wiki pages, emails, generated reports) and the images they use, kept in memory only: nothing
// of them is ever written to disk, so a patient's email never sits in the temp folder, and a crash leaves nothing behind. Each page gets a
// folder of its own under GetRootFolder, a folder that is never created. The browser asks for the page and its images by their file: address,
// and ODWebView2 answers every request under that folder from here (WebResourceRequested), never from the disk: so a page keeps every rule
// of a file page (its own folder, the folders it may load, its links) while it exists only in memory. No WebView2 type is used, so the
// lookup is tested without the browser.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeBase.Controls {
	///<summary>The pages of ODWebView2.NavigateToHtml and their files, in memory. A page is added when a control shows it and removed when the
	///control shows the next one or is disposed. Images downloaded from cloud storage for the wiki go into the shared folder (AddSharedFile),
	///which keeps the most recent ones up to SharedBytesMax. Thread safe.</summary>
	public static class WebView2PageStore {
		///<summary>The sub folder of the program's temp folder that the in-memory pages seem to be in. It is never created.</summary>
		public const string RootFolderName="pages-in-memory";
		///<summary>The sub folder of the root for the images that several pages may use (wiki images downloaded from cloud storage).</summary>
		public const string SharedFolderName="shared";
		///<summary>The file name of every page in its own folder.</summary>
		public const string PageFileName="page.html";
		///<summary>The most the shared folder keeps, in bytes: the images added first go first.</summary>
		public const long SharedBytesMax=64L*1024*1024;
		private static readonly object _lock=new object();
		///<summary>The pages shown now, by the full path of their folder.</summary>
		private static Dictionary<string,WebView2Page> _dictPages=new Dictionary<string,WebView2Page>(StringComparer.OrdinalIgnoreCase);
		///<summary>The files of the shared folder, by file name.</summary>
		private static Dictionary<string,byte[]> _dictShared=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
		///<summary>The names in _dictShared, the oldest first.</summary>
		private static List<string> _listSharedNames=new List<string>();
		private static long _bytesShared=0;
		///<summary>True once DeleteOldPageFiles ran in this program session.</summary>
		private static bool _isOldPageFilesDeleted=false;
		///<summary>The name ODFileUtils.CreateRandomFile and CreateRandomFolder give: six letters or digits and a time stamp (yyyyMMddhhmmss).</summary>
		private static readonly Regex _regexRandomName=new Regex(@"^[A-Z0-9]{6}[0-9]{14}$");

		///<summary>The folder the in-memory pages seem to be in: %TEMP%\openpms-dental\pages-in-memory. It is never created, and nothing in it is
		///ever read from the disk: a request for a file under it is answered from memory, or refused.</summary>
		public static string GetRootFolder() {
			return Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName,RootFolderName);
		}

		///<summary>The folder of the images several pages may use, under the root: the folder a window allows its pages (listFoldersAllowed).</summary>
		public static string GetSharedFolder() {
			return Path.Combine(GetRootFolder(),SharedFolderName);
		}

		///<summary>Adds a page: html (kept as UTF-8 with a byte order mark, so it reads as UTF-8 whatever its meta tags say) and the files it uses,
		///by file name, in a new folder of its own. A name with a folder in it, a name the page itself has, or a file without content is left out.
		///The caller removes the page when it is no longer shown (RemovePage).</summary>
		public static WebView2Page AddPage(string html,IDictionary<string,byte[]> dictFiles=null) {
			string folder=Path.Combine(GetRootFolder(),Guid.NewGuid().ToString("N"));
			Dictionary<string,byte[]> dictPageFiles=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
			if(dictFiles!=null) {
				foreach(KeyValuePair<string,byte[]> keyValuePair in dictFiles) {
					if(!IsFileName(keyValuePair.Key) || keyValuePair.Value==null || string.Equals(keyValuePair.Key,PageFileName,StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					dictPageFiles[keyValuePair.Key]=keyValuePair.Value;
				}
			}
			UTF8Encoding utf8Encoding=new UTF8Encoding(true);
			dictPageFiles[PageFileName]=utf8Encoding.GetPreamble().Concat(utf8Encoding.GetBytes(html??"")).ToArray();
			WebView2Page webView2Page=new WebView2Page(folder,dictPageFiles);
			lock(_lock) {
				_dictPages[folder]=webView2Page;
			}
			return webView2Page;
		}

		///<summary>Removes a page and its files from memory. Null does nothing.</summary>
		public static void RemovePage(WebView2Page webView2Page) {
			if(webView2Page==null) {
				return;
			}
			lock(_lock) {
				_dictPages.Remove(webView2Page.Folder);
			}
		}

		///<summary>Adds or replaces a file of the shared folder and returns its full path. The name comes from key (the image's own path, for
		///example), so the same image shown again replaces itself rather than adding a copy; extension is the file's (".png"). The oldest files go
		///once the folder holds more than SharedBytesMax.</summary>
		public static string AddSharedFile(string key,byte[] content,string extension) {
			string ext=extension??"";
			if(ext!="" && !ext.StartsWith(".")) {
				ext="."+ext;
			}
			if(!IsFileName("x"+ext)) {
				ext="";
			}
			string name;
			using(SHA256 sha256=SHA256.Create()) {
				byte[] byteArrayHash=sha256.ComputeHash(Encoding.UTF8.GetBytes((key??"").ToLowerInvariant()));
				name=string.Concat(byteArrayHash.Take(16).Select(x => x.ToString("x2")))+ext;
			}
			byte[] byteArrayContent=content??new byte[0];
			lock(_lock) {
				if(_dictShared.TryGetValue(name,out byte[] byteArrayOld)) {
					_bytesShared-=byteArrayOld.Length;
					_listSharedNames.Remove(name);
				}
				_dictShared[name]=byteArrayContent;
				_listSharedNames.Add(name);
				_bytesShared+=byteArrayContent.Length;
				while(_bytesShared>SharedBytesMax && _listSharedNames.Count>1) {
					string nameOldest=_listSharedNames[0];
					_listSharedNames.RemoveAt(0);
					_bytesShared-=_dictShared[nameOldest].Length;
					_dictShared.Remove(nameOldest);
				}
			}
			return Path.Combine(GetSharedFolder(),name);
		}

		///<summary>True when uri is a file: address under the root folder (or the root itself): such a request is answered from memory, or refused,
		///and never read from the disk. False for anything else, a path the browser could not mean included (WebView2Policy.GetLocalFilePath).</summary>
		public static bool IsInRoot(string uri) {
			string path=WebView2Policy.GetLocalFilePath(uri);
			if(path==null) {
				return false;
			}
			return IsPathInRoot(path);
		}

		///<summary>True when path (a full path) is the root folder or under it.</summary>
		public static bool IsPathInRoot(string path) {
			if(string.IsNullOrEmpty(path)) {
				return false;
			}
			string root=GetRootFolder().TrimEnd('\\');
			string pathTrimmed=path.TrimEnd('\\');
			return string.Equals(pathTrimmed,root,StringComparison.OrdinalIgnoreCase) || pathTrimmed.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The content of the file the file: address uri names, with its content type, when it is a file of a page shown now or of the
		///shared folder. False for anything else: no such page or file, a folder, or an address outside the root.</summary>
		public static bool TryGetFile(string uri,out byte[] content,out string contentType) {
			content=null;
			contentType="";
			string path=WebView2Policy.GetLocalFilePath(uri);
			if(path==null || !IsPathInRoot(path)) {
				return false;
			}
			return TryGetFileByPath(path,out content,out contentType);
		}

		///<summary>True when path (a full path) is a file of a page shown now or of the shared folder: a link to it is a file, never a web address.</summary>
		public static bool Exists(string path) {
			return TryGetFileByPath(path,out _,out _);
		}

		private static bool TryGetFileByPath(string path,out byte[] content,out string contentType) {
			content=null;
			contentType="";
			if(!IsPathInRoot(path)) {
				return false;
			}
			string folder;
			string name;
			try {
				folder=Path.GetDirectoryName(path.TrimEnd('\\'));
				name=Path.GetFileName(path.TrimEnd('\\'));
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
			if(string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name)) {
				return false;
			}
			lock(_lock) {
				if(string.Equals(folder,GetSharedFolder(),StringComparison.OrdinalIgnoreCase)) {
					if(!_dictShared.TryGetValue(name,out content)) {
						return false;
					}
				}
				else {
					if(!_dictPages.TryGetValue(folder,out WebView2Page webView2Page) || !webView2Page.TryGetFile(name,out content)) {
						return false;
					}
				}
			}
			contentType=GetContentType(name);
			return true;
		}

		///<summary>The content type of a file by its extension: the page's html (UTF-8), the image types, and application/octet-stream for anything
		///else.</summary>
		public static string GetContentType(string fileName) {
			string ext="";
			try {
				ext=Path.GetExtension(fileName??"").ToLowerInvariant();
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			switch(ext) {
				case ".html":
				case ".htm":
					return "text/html; charset=utf-8";
				case ".png":
					return "image/png";
				case ".jpg":
				case ".jpeg":
				case ".jpe":
				case ".jfif":
					return "image/jpeg";
				case ".gif":
					return "image/gif";
				case ".bmp":
					return "image/bmp";
				case ".ico":
					return "image/x-icon";
				case ".tif":
				case ".tiff":
					return "image/tiff";
				case ".webp":
					return "image/webp";
				case ".svg":
					return "image/svg+xml";
				case ".css":
					return "text/css; charset=utf-8";
				default:
					return "application/octet-stream";
			}
		}

		///<summary>The headers of a response for a file of this store: its content type, and never kept in a cache, so the browser's profile keeps no
		///copy of a page either. No cookie, no referrer to the web.</summary>
		public static string GetResponseHeaders(string contentType) {
			return "Content-Type: "+contentType+"\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer";
		}

		///<summary>True for a plain file name: not blank, no folder, nothing Windows would not allow in a name.</summary>
		private static bool IsFileName(string name) {
			if(string.IsNullOrWhiteSpace(name) || name=="." || name==".." || name.IndexOfAny(Path.GetInvalidFileNameChars())>=0) {
				return false;
			}
			return true;
		}

		///<summary>Deletes, once per program session, the page files an older version wrote into the program's temp folder (it wrote every page
		///shown with NavigateToHtml there, and the images of an email's Show Images into a folder with the page): files named like
		///"ABC12320261102093000.html" in that folder, and folders named like "ABC12320261102093000" that hold such a page, with their files. The
		///program empties the folder when it closes, so this only finds what an older version left when it ended without closing. Nothing else is
		///touched, and an error leaves the file. Never throws.</summary>
		public static void DeleteOldPageFiles() {
			lock(_lock) {
				if(_isOldPageFilesDeleted) {
					return;
				}
				_isOldPageFilesDeleted=true;
			}
			ODException.SwallowAnyException(() => DeleteOldPageFiles(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName)));
		}

		///<summary>DeleteOldPageFiles in folder. Returns how many files it deleted. Never throws.</summary>
		public static int DeleteOldPageFiles(string folder) {
			int countDeleted=0;
			if(string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) {
				return 0;
			}
			try {
				foreach(string path in Directory.GetFiles(folder,"*.html",SearchOption.TopDirectoryOnly)) {
					if(IsOldPageFile(path) && TryDelete(path)) {
						countDeleted++;
					}
				}
				foreach(string folderSub in Directory.GetDirectories(folder,"*",SearchOption.TopDirectoryOnly)) {
					if(!_regexRandomName.IsMatch(Path.GetFileName(folderSub))) {
						continue;
					}
					string[] arrayFiles=Directory.GetFiles(folderSub,"*",SearchOption.TopDirectoryOnly);
					if(Directory.GetDirectories(folderSub).Length>0 || !arrayFiles.Any(x => IsOldPageFile(x))) {
						continue;
					}
					foreach(string path in arrayFiles) {
						if(TryDelete(path)) {
							countDeleted++;
						}
					}
					ODException.SwallowAnyException(() => Directory.Delete(folderSub));
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return countDeleted;
		}

		///<summary>True for a page file an older version wrote: "ABC12320261102093000.html".</summary>
		private static bool IsOldPageFile(string path) {
			return string.Equals(Path.GetExtension(path),".html",StringComparison.OrdinalIgnoreCase)
				&& _regexRandomName.IsMatch(Path.GetFileNameWithoutExtension(path));
		}

		private static bool TryDelete(string path) {
			try {
				File.Delete(path);
				return true;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>How many pages are in memory now. For LibWebTests.</summary>
		internal static int CountPages {
			get {
				lock(_lock) {
					return _dictPages.Count;
				}
			}
		}

		///<summary>Empties the shared folder. For LibWebTests.</summary>
		internal static void ClearShared() {
			lock(_lock) {
				_dictShared.Clear();
				_listSharedNames.Clear();
				_bytesShared=0;
			}
		}

		///<summary>How many bytes the shared folder holds now. For LibWebTests.</summary>
		internal static long BytesShared {
			get {
				lock(_lock) {
					return _bytesShared;
				}
			}
		}
	}

	///<summary>A page of WebView2PageStore: its folder (never created), its file: address and its files, the page itself included.</summary>
	public class WebView2Page {
		///<summary>The full path of the page's own folder, under WebView2PageStore.GetRootFolder.</summary>
		public string Folder { get; }
		///<summary>The full path of the page.</summary>
		public string Path { get; }
		///<summary>The file: address of the page, which the browser navigates to.</summary>
		public string Uri { get; }
		///<summary>The page's files by name, the page itself included. Never changed after the page is made.</summary>
		private readonly Dictionary<string,byte[]> _dictFiles;

		internal WebView2Page(string folder,Dictionary<string,byte[]> dictFiles) {
			Folder=folder;
			Path=System.IO.Path.Combine(folder,WebView2PageStore.PageFileName);
			Uri=new Uri(Path).AbsoluteUri;
			_dictFiles=dictFiles;
		}

		///<summary>The names of the page's files, the page included.</summary>
		public List<string> GetFileNames() {
			return _dictFiles.Keys.ToList();
		}

		internal bool TryGetFile(string name,out byte[] content) {
			return _dictFiles.TryGetValue(name,out content);
		}
	}
}
