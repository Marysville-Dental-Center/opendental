// Read-only copy of the wiki that any browser can open while the program is closed or the database server is down
// (v25_2-wiki-readonly-offline, package B-WIKI). Plain HTML files in a local or network folder the office picks. Nothing is hosted or uploaded.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;
using Newtonsoft.Json;

namespace OpenPmsDentalBusiness {
	///<summary>Writes a static HTML copy of the wiki: one file per page in pages\, rendered by the same wiki-to-HTML renderer the Wiki window uses,
	///with internal links pointing at the other files; an index.html that lists every page alphabetically and has a small title and content search
	///in plain JavaScript (no libraries, works from file://); and the images and wiki-folder files that pages reference, copied into _files\.
	///Archived pages, drafts and pages whose title starts with an underscore (_Master) are left out. Every page carries a "Read-only copy exported"
	///banner. Settings live in two preference rows that are not in the PrefName enum (the same pattern as DxcPartnerIdentity):
	///ForkWikiReadOnlyExportPath (blank means off) and ForkWikiReadOnlyAutoExport (blank or missing means on).</summary>
	public class WikiStaticExport {
		public const string PREF_EXPORT_PATH="ForkWikiReadOnlyExportPath";
		public const string PREF_AUTO_EXPORT="ForkWikiReadOnlyAutoExport";
		///<summary>The suggested folder inside the A-to-Z folder.</summary>
		public const string DEFAULT_FOLDER_NAME="WikiReadOnly";
		public const string INDEX_FILE_NAME="index.html";
		public const string SEARCH_DATA_FILE_NAME="search-data.js";
		public const string PAGES_FOLDER_NAME="pages";
		public const string FILES_FOLDER_NAME="_files";
		///<summary>Inside _files: the names of the files the export copied, so a full export removes only its own stale files.</summary>
		public const string FILES_MANIFEST_NAME="_manifest.txt";
		private const string LOG_SUBDIRECTORY="WikiReadOnly";
		private const string IMAGE_TOKEN_PREFIX="ODROIMAGETOKEN";
		private const string IMAGE_TOKEN_SUFFIX="QQ";
		///<summary>Same pattern as the renderer's image markup.</summary>
		private static readonly Regex _regexImage=new Regex(@"\[\[(img:).+?\]\]");
		private static readonly Regex _regexImageToken=new Regex(IMAGE_TOKEN_PREFIX+@"(\d+)"+IMAGE_TOKEN_SUFFIX);
		private static readonly Regex _regexAnchorTag=new Regex(@"<a\b[^>]*>",RegexOptions.IgnoreCase);
		private static readonly Regex _regexHref=new Regex("\\bhref=\"([^\"]*)\"",RegexOptions.IgnoreCase);
		private static readonly Regex _regexPageFileName=new Regex(@"^\d+\.html$",RegexOptions.IgnoreCase);
		///<summary>One export at a time: saves made in quick succession queue up behind each other.</summary>
		private static readonly object _lockExport=new object();
		///<summary>Guards _wikiExportResultLast only. Never held while an export runs, so reading LastResult never waits for an export.</summary>
		private static readonly object _lockResult=new object();
		private static WikiExportResult _wikiExportResultLast;

		///<summary>The result of the most recent finished export on this workstation, or null. Returns at once, even while an export is running:
		///Wiki Setup reads it on the UI thread.</summary>
		public static WikiExportResult LastResult {
			get {
				lock(_lockResult) {
					return _wikiExportResultLast;
				}
			}
		}

		#region Settings
		///<summary>The folder of the read-only copy, trimmed. Blank means the read-only copy is off.</summary>
		public static string GetExportPath() {
			Meth.NoCheckMiddleTierRole();
			return (GetPrefOrNull(PREF_EXPORT_PATH)??"").Trim();
		}

		///<summary>Whether each saved page is exported right away. On unless the office turned it off.</summary>
		public static bool GetAutoExport() {
			Meth.NoCheckMiddleTierRole();
			string value=GetPrefOrNull(PREF_AUTO_EXPORT);
			if(string.IsNullOrWhiteSpace(value)) {
				return true;
			}
			return value.Trim()!="0";
		}

		///<summary>True when a folder is set.</summary>
		public static bool IsEnabled() {
			return GetExportPath()!="";
		}

		///<summary>True when a folder is set and pages are exported after each save.</summary>
		public static bool IsAutoExportOn() {
			return IsEnabled() && GetAutoExport();
		}

		///<summary>Stores both settings (the path trimmed; blank turns the copy off), inserting the rows if needed, and updates this workstation's
		///cache. Returns true if anything changed; the caller then signals InvalidType.Prefs.</summary>
		public static bool SaveSettings(string exportPath,bool isAutoExport) {
			Meth.NoCheckMiddleTierRole();
			exportPath=(exportPath??"").Trim();
			string autoExport=(isAutoExport ? "1" : "0");
			if(GetPrefOrNull(PREF_EXPORT_PATH)==exportPath && GetPrefOrNull(PREF_AUTO_EXPORT)==autoExport) {
				return false;
			}
			UpsertSettings(exportPath,autoExport);
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_EXPORT_PATH,ValueString=exportPath });
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_AUTO_EXPORT,ValueString=autoExport });
			return true;
		}

		///<summary>Writes both preference rows straight to the table, inserting missing ones.</summary>
		public static void UpsertSettings(string exportPath,string autoExport) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),exportPath,autoExport);
				return;
			}
			PrefC.UpsertRawNoCache(PREF_EXPORT_PATH,exportPath??"");
			PrefC.UpsertRawNoCache(PREF_AUTO_EXPORT,autoExport??"1");
		}

		///<summary>The raw value from the preference cache, or null when the row does not exist. Never throws. Uses B-SERVER's shared helper for
		///fork-owned preference rows.</summary>
		private static string GetPrefOrNull(string prefName) {
			return PrefC.GetRawOrDefault(prefName,null);
		}

		///<summary>The suggested folder, A-to-Z\WikiReadOnly, or blank when the office keeps images in the database or the A-to-Z folder is not
		///known.</summary>
		public static string GetDefaultExportPath() {
			try {
				if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
					return "";
				}
				string atoZPath=ImageStore.GetPreferredAtoZpath();
				if(string.IsNullOrWhiteSpace(atoZPath)) {
					return "";
				}
				return ODFileUtils.CombinePaths(atoZPath,DEFAULT_FOLDER_NAME);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>Blank when the folder can hold the read-only copy, otherwise why not. The folder must be a full local or network path, and must not
		///be the A-to-Z folder itself or the wiki image folder (or inside it), because the export writes and removes its own files there.</summary>
		public static string ValidateExportPath(string exportPath,string wikiFolder,string atoZFolder) {
			exportPath=(exportPath??"").Trim();
			if(exportPath=="") {
				return Lans.g("WikiStaticExport","Enter a folder for the read-only copy.");
			}
			string fullPath;
			try {
				if(!Path.IsPathRooted(exportPath)) {
					return Lans.g("WikiStaticExport","Enter a full path, for example C:\\WikiReadOnly or \\\\computer\\share\\WikiReadOnly.");
				}
				fullPath=NormalizeFolder(exportPath);
			}
			catch(Exception ex) {
				return Lans.g("WikiStaticExport","The folder path is not valid:")+" "+ex.Message;
			}
			if(!string.IsNullOrWhiteSpace(wikiFolder) && IsSameOrInside(fullPath,wikiFolder)) {
				return Lans.g("WikiStaticExport","The read-only copy cannot go in the wiki image folder. Choose another folder.");
			}
			if(!string.IsNullOrWhiteSpace(atoZFolder) && string.Equals(fullPath,NormalizeFolder(atoZFolder),StringComparison.OrdinalIgnoreCase)) {
				return Lans.g("WikiStaticExport","The read-only copy cannot go in the A to Z folder itself. Choose a subfolder, such as")+" "
					+ODFileUtils.CombinePaths(atoZFolder,DEFAULT_FOLDER_NAME);
			}
			return "";
		}

		///<summary>The wiki image folder, or null when it is not available (images stored in the database, or the folder cannot be reached).</summary>
		public static string GetWikiFolderOrNull() {
			try {
				if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
					return null;
				}
				return WikiPages.GetWikiPath();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>The A-to-Z folder when one is used locally, otherwise null.</summary>
		public static string GetAtoZFolderOrNull() {
			try {
				if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
					return null;
				}
				return ImageStore.GetPreferredAtoZpath();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}
		#endregion Settings

		#region Export entry points
		///<summary>Called after a wiki page is saved, renamed, archived or restored, or after the master page changes. Does nothing unless the
		///read-only copy is on with export after each save. Exports in a background thread; errors (for example an unreachable folder) are logged and
		///never interrupt the save. isFullExport re-exports every page, for changes that affect other pages too.</summary>
		public static void ExportInBackground(List<string> listPageTitles,bool isFullExport) {
			Meth.NoCheckMiddleTierRole();
			if(!IsAutoExportOn()) {
				return;
			}
			string folder=GetExportPath();
			List<string> listTitles=(listPageTitles??new List<string>()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
			if(!isFullExport && listTitles.Count==0) {
				return;
			}
			ODThread odThread=new ODThread(o => {
				TryExport(folder,listTitles,isFullExport,DateTime.Now);
			});
			odThread.Name="WikiReadOnlyExport";
			odThread.AddExceptionHandler(ex => Logger.WriteException(ex,LOG_SUBDIRECTORY));
			odThread.Start();
		}

		///<summary>Runs one export and never throws: failures are logged and returned in the result. Exports are serialized on this workstation.</summary>
		public static WikiExportResult TryExport(string folder,List<string> listPageTitles,bool isFullExport,DateTime dateTimeExport) {
			Meth.NoCheckMiddleTierRole();
			lock(_lockExport) {
				WikiExportResult wikiExportResult;
				try {
					if(isFullExport) {
						wikiExportResult=ExportAll(folder,dateTimeExport);
					}
					else {
						wikiExportResult=ExportPages(folder,listPageTitles,dateTimeExport);
					}
				}
				catch(Exception ex) {
					wikiExportResult=new WikiExportResult();
					wikiExportResult.Folder=folder;
					wikiExportResult.DateTimeExport=dateTimeExport;
					wikiExportResult.ErrorMessage=ex.Message;
					Logger.WriteLine("Read-only wiki export to '"+folder+"' failed: "+MiscUtils.GetExceptionText(ex),LOG_SUBDIRECTORY);
				}
				//The last step, under its own lock: LastResult must never wait for the export lock.
				lock(_lockResult) {
					_wikiExportResultLast=wikiExportResult;
				}
				return wikiExportResult;
			}
		}

		///<summary>Exports every page to the folder, rebuilds index.html and the search data, copies referenced files, and removes page files and
		///copied files that are no longer needed (only files this export wrote: pages\NNN.html and the files named in the _files manifest).
		///Throws if the folder cannot be created or written.</summary>
		public static WikiExportResult ExportAll(string folder,DateTime dateTimeExport) {
			Meth.NoCheckMiddleTierRole();
			WikiExportContext context=CreateContext(folder,dateTimeExport);
			List<WikiPage> listWikiPages=WikiPages.GetForStaticExport(includeContent:true);
			FillTitles(context,listWikiPages);
			WikiExportResult wikiExportResult=context.Result;
			for(int i=0;i<listWikiPages.Count;i++) {
				WritePageFile(context,listWikiPages[i]);
			}
			CopyReferencedFiles(context);
			WriteIndexAndSearchData(context,listWikiPages);
			//Remove stale page files: pages archived or deleted since the last export.
			HashSet<string> hashSetPageFiles=new HashSet<string>(listWikiPages.Select(x => GetPageFileName(x.WikiPageNum)),StringComparer.OrdinalIgnoreCase);
			foreach(string filePath in Directory.GetFiles(context.PagesFolder,"*.html")) {
				string fileName=Path.GetFileName(filePath);
				if(!_regexPageFileName.IsMatch(fileName) || hashSetPageFiles.Contains(fileName)) {
					continue;
				}
				File.Delete(filePath);
				wikiExportResult.CountPageFilesRemoved++;
			}
			//Remove copied files that no page references any more, then rewrite the manifest.
			List<string> listManifestOld=ReadManifest(context.FilesFolder);
			for(int i=0;i<listManifestOld.Count;i++) {
				if(context.DictFilesToCopy.ContainsKey(listManifestOld[i])) {
					continue;
				}
				string filePath=Path.Combine(context.FilesFolder,listManifestOld[i]);
				if(File.Exists(filePath)) {
					File.Delete(filePath);
				}
			}
			WriteManifest(context.FilesFolder,context.DictFilesToCopy.Keys.ToList());
			wikiExportResult.IsFullExport=true;
			return wikiExportResult;
		}

		///<summary>Exports the named pages, removes the files of named pages that are archived or gone, rebuilds index.html and the search data,
		///and copies the files the exported pages reference. Other pages are left as they are. Throws if the folder cannot be written.</summary>
		public static WikiExportResult ExportPages(string folder,List<string> listPageTitles,DateTime dateTimeExport) {
			Meth.NoCheckMiddleTierRole();
			WikiExportContext context=CreateContext(folder,dateTimeExport);
			List<WikiPage> listWikiPagesAll=WikiPages.GetForStaticExport(includeContent:false);
			FillTitles(context,listWikiPagesAll);
			WikiExportResult wikiExportResult=context.Result;
			foreach(string pageTitle in (listPageTitles??new List<string>()).Distinct()) {
				WikiPage wikiPage=WikiPages.GetByTitle(pageTitle);
				if(wikiPage!=null && !wikiPage.PageTitle.StartsWith("_")) {
					WritePageFile(context,wikiPage);
					continue;
				}
				//Archived, renamed away or never existed: remove its file if there is one.
				WikiPage wikiPageArchived=WikiPages.GetByTitle(pageTitle,isDeleted:true);
				if(wikiPageArchived==null) {
					continue;
				}
				string filePath=Path.Combine(context.PagesFolder,GetPageFileName(wikiPageArchived.WikiPageNum));
				if(File.Exists(filePath)) {
					File.Delete(filePath);
					wikiExportResult.CountPageFilesRemoved++;
				}
			}
			CopyReferencedFiles(context);
			WriteIndexAndSearchData(context,listWikiPagesAll);
			List<string> listManifest=ReadManifest(context.FilesFolder);
			listManifest.AddRange(context.DictFilesToCopy.Keys);
			WriteManifest(context.FilesFolder,listManifest);
			return wikiExportResult;
		}
		#endregion Export entry points

		#region Export steps
		private static WikiExportContext CreateContext(string folder,DateTime dateTimeExport) {
			folder=(folder??"").Trim();
			if(folder=="") {
				throw new ODException(Lans.g("WikiStaticExport","No folder is set for the read-only copy of the wiki."));
			}
			WikiExportContext context=new WikiExportContext();
			context.Folder=folder;
			context.PagesFolder=Path.Combine(folder,PAGES_FOLDER_NAME);
			context.FilesFolder=Path.Combine(folder,FILES_FOLDER_NAME);
			context.DateTimeExport=dateTimeExport;
			context.WikiFolder=GetWikiFolderOrNull();
			context.Result=new WikiExportResult();
			context.Result.Folder=folder;
			context.Result.DateTimeExport=dateTimeExport;
			Directory.CreateDirectory(context.PagesFolder);
			Directory.CreateDirectory(context.FilesFolder);
			return context;
		}

		private static void FillTitles(WikiExportContext context,List<WikiPage> listWikiPages) {
			context.DictTitleToPageNum=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
			for(int i=0;i<listWikiPages.Count;i++) {
				if(!context.DictTitleToPageNum.ContainsKey(listWikiPages[i].PageTitle)) {
					context.DictTitleToPageNum[listWikiPages[i].PageTitle]=listWikiPages[i].WikiPageNum;
				}
			}
		}

		private static void WritePageFile(WikiExportContext context,WikiPage wikiPage) {
			string html=BuildPageHtml(context,wikiPage);
			WriteTextFile(Path.Combine(context.PagesFolder,GetPageFileName(wikiPage.WikiPageNum)),html);
			context.Result.CountPagesWritten++;
		}

		///<summary>The full HTML document of one page in the read-only copy. A page the renderer refuses (broken markup) becomes a page that says so
		///and shows its plain text, so the content is still readable.</summary>
		public static string BuildPageHtml(WikiExportContext context,WikiPage wikiPage) {
			Dictionary<int,string> dictImageNames;
			string markup=ReplaceImagesWithTokens(wikiPage.PageContent??"",out dictImageNames);
			string html;
			try {
				html=MarkupEdit.TranslateToXhtml(markup,false);
			}
			catch(Exception ex) {
				context.Result.ListPagesFailed.Add(wikiPage.PageTitle);
				return BuildBrokenPageHtml(wikiPage,ex.Message,context.DateTimeExport);
			}
			html=ReplaceImageTokens(html,dictImageNames,context);
			html=RewriteAnchors(html,context);
			return InsertHeadAndBanner(html,wikiPage.PageTitle,GetBannerHtml(context.DateTimeExport,"../"+INDEX_FILE_NAME));
		}

		private static string BuildBrokenPageHtml(WikiPage wikiPage,string errorMessage,DateTime dateTimeExport) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("<!DOCTYPE html>\n<html><head></head><body>\n");
			stringBuilder.Append("<h1>"+Html(wikiPage.PageTitle)+"</h1>\n");
			stringBuilder.Append("<p>"+Html(Lans.g("WikiStaticExport","This page could not be shown as formatted text in the read-only copy:")+" "+errorMessage)
				+"</p>\n");
			stringBuilder.Append("<pre style=\"white-space:pre-wrap\">"+Html(wikiPage.PageContentPlainText??"")+"</pre>\n");
			stringBuilder.Append("</body></html>");
			return InsertHeadAndBanner(stringBuilder.ToString(),wikiPage.PageTitle,GetBannerHtml(dateTimeExport,"../"+INDEX_FILE_NAME));
		}

		///<summary>Replaces every [[img:name]] with a plain token the renderer passes through as text, so the renderer does not need to open the image
		///(a missing image would make it refuse the whole page) and the export can point the image at its copy in _files.</summary>
		public static string ReplaceImagesWithTokens(string markup,out Dictionary<int,string> dictImageNames) {
			Dictionary<int,string> dict=new Dictionary<int,string>();
			string result=_regexImage.Replace(markup,match => {
				string imageName=match.Value.Substring(match.Value.IndexOf(":")+1).TrimEnd(']');
				int index=dict.Count;
				dict[index]=imageName;
				return IMAGE_TOKEN_PREFIX+index+IMAGE_TOKEN_SUFFIX;
			});
			dictImageNames=dict;
			return result;
		}

		///<summary>Turns the image tokens back into img tags that point at the copies in _files. An image that cannot be found becomes a short note.
		///</summary>
		public static string ReplaceImageTokens(string html,Dictionary<int,string> dictImageNames,WikiExportContext context) {
			return _regexImageToken.Replace(html,match => {
				int index=int.Parse(match.Groups[1].Value);
				string imageName;
				if(!dictImageNames.TryGetValue(index,out imageName)) {
					return match.Value;
				}
				string sourcePath=null;
				if(!string.IsNullOrEmpty(context.WikiFolder)) {
					sourcePath=ODFileUtils.CombinePaths(context.WikiFolder,imageName);
				}
				//Only images inside the wiki folder are copied: a name such as ..\..\x.png never pulls a file from elsewhere into the copy.
				if(sourcePath==null || !IsInsideFolderSafe(sourcePath,context.WikiFolder) || !File.Exists(sourcePath)) {
					context.Result.ListFilesMissing.Add(imageName);
					return "<span class=\"PageNotExists\">["+Html(Lans.g("WikiStaticExport","image not available:")+" "+imageName)+"]</span>";
				}
				string copyName=AddFileToCopy(context,sourcePath);
				return "<img src=\"../"+FILES_FOLDER_NAME+"/"+copyName+"\" alt=\""+Html(imageName)+"\" style=\"max-width:100%\"></img>";
			});
		}

		///<summary>Rewrites the links the renderer wrote for the program into links that work in a browser: wiki: links to the page files (or a
		///marked dead link when the page is not in the copy), wikifile: links to the copy in _files when the file is in the wiki folder and to the
		///original file otherwise, folder: links to the folder. Cloud links are disabled. Other links (web, bookmarks) are kept.</summary>
		public static string RewriteAnchors(string html,WikiExportContext context) {
			return _regexAnchorTag.Replace(html,matchTag => {
				string tag=matchTag.Value;
				Match matchHref=_regexHref.Match(tag);
				if(!matchHref.Success) {
					return tag;
				}
				string href=WebUtility.HtmlDecode(matchHref.Groups[1].Value);
				string hrefNew;
				string titleAttribute="";
				bool isDead=false;
				if(href.StartsWith("wiki:")) {
					//The browser control turns runs of spaces into non-breaking spaces; FormWiki.NavigateToWiki undoes it the same way.
					string pageTitle=href.Substring(5).Replace('\u00A0',' ');
					long wikiPageNum;
					if(context.DictTitleToPageNum!=null && context.DictTitleToPageNum.TryGetValue(pageTitle,out wikiPageNum)) {
						hrefNew=GetPageFileName(wikiPageNum);
					}
					else {
						hrefNew="#";
						isDead=true;
						titleAttribute=Lans.g("WikiStaticExport","This page is not in the read-only copy.");
					}
				}
				else if(href.StartsWith("wikifilecloud:") || href.StartsWith("foldercloud:")) {
					hrefNew="#";
					isDead=true;
					titleAttribute=Lans.g("WikiStaticExport","Cloud storage links do not work in the read-only copy.");
				}
				else if(href.StartsWith("wikifile:")) {
					string filePath=href.Substring(9).Replace("/","\\");
					hrefNew=GetFileHref(context,filePath);
				}
				else if(href.StartsWith("folder:")) {
					hrefNew=ToFileUri(href.Substring(7).Replace("/","\\"));
				}
				else {
					return tag;
				}
				string tagNew=tag.Substring(0,matchHref.Index)+"href=\""+Html(hrefNew)+"\""+tag.Substring(matchHref.Index+matchHref.Length);
				if(isDead) {
					if(!Regex.IsMatch(tagNew,"\\bclass=",RegexOptions.IgnoreCase)) {
						tagNew=tagNew.Insert(2," class=\"PageNotExists\"");
					}
					tagNew=tagNew.Insert(2," title=\""+Html(titleAttribute)+"\"");
				}
				return tagNew;
			});
		}

		///<summary>The link for a [[file:]] target: the copy in _files when the file is in the wiki folder and exists, otherwise a file:// link to the
		///original location (which works only while that location can be reached).</summary>
		private static string GetFileHref(WikiExportContext context,string filePath) {
			try {
				if(!string.IsNullOrEmpty(context.WikiFolder) && Path.IsPathRooted(filePath) && IsSameOrInside(filePath,context.WikiFolder)
					&& File.Exists(filePath))
				{
					return "../"+FILES_FOLDER_NAME+"/"+AddFileToCopy(context,filePath);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return ToFileUri(filePath);
		}

		///<summary>A file:// URI for a local or UNC path, or the path itself when it cannot be turned into one.</summary>
		public static string ToFileUri(string path) {
			try {
				if(Path.IsPathRooted(path)) {
					return new Uri(path).AbsoluteUri;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return path;
		}

		///<summary>Registers a file to copy into _files and returns its name there: the first 8 hex digits of a hash of the full source path, then
		///the file name with anything but letters, digits, dot, dash and underscore replaced, so different sources never collide and the name is safe
		///in a URL.</summary>
		private static string AddFileToCopy(WikiExportContext context,string sourcePath) {
			string copyName=GetCopyName(sourcePath);
			if(!context.DictFilesToCopy.ContainsKey(copyName)) {
				context.DictFilesToCopy[copyName]=sourcePath;
			}
			return copyName;
		}

		public static string GetCopyName(string sourcePath) {
			string fullPath=sourcePath;
			try {
				fullPath=Path.GetFullPath(sourcePath);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			string hash;
			using(SHA1 sha1=SHA1.Create()) {
				byte[] arrayHash=sha1.ComputeHash(Encoding.UTF8.GetBytes(fullPath.ToLowerInvariant()));
				hash=string.Concat(arrayHash.Take(4).Select(x => x.ToString("x2")));
			}
			string fileName=Regex.Replace(Path.GetFileName(sourcePath)??"","[^A-Za-z0-9._-]","_");
			if(fileName=="" || fileName.Trim('.')=="") {
				fileName="file";
			}
			return hash+"_"+fileName;
		}

		private static void CopyReferencedFiles(WikiExportContext context) {
			foreach(KeyValuePair<string,string> pair in context.DictFilesToCopy) {
				string targetPath=Path.Combine(context.FilesFolder,pair.Key);
				try {
					FileInfo fileInfoSource=new FileInfo(pair.Value);
					if(!fileInfoSource.Exists) {
						context.Result.ListFilesMissing.Add(pair.Value);
						continue;
					}
					FileInfo fileInfoTarget=new FileInfo(targetPath);
					if(fileInfoTarget.Exists && fileInfoTarget.Length==fileInfoSource.Length && fileInfoTarget.LastWriteTimeUtc>=fileInfoSource.LastWriteTimeUtc) {
						continue;//Already copied and unchanged.
					}
					File.Copy(pair.Value,targetPath,true);
					context.Result.CountFilesCopied++;
				}
				catch(Exception ex) {
					context.Result.ListFilesMissing.Add(pair.Value);
					Logger.WriteLine("Read-only wiki export could not copy '"+pair.Value+"': "+ex.Message,LOG_SUBDIRECTORY);
				}
			}
		}

		private static void WriteIndexAndSearchData(WikiExportContext context,List<WikiPage> listWikiPages) {
			List<WikiPage> listSorted=listWikiPages.OrderBy(x => x.PageTitle,StringComparer.OrdinalIgnoreCase).ThenBy(x => x.WikiPageNum).ToList();
			WriteTextFile(Path.Combine(context.Folder,SEARCH_DATA_FILE_NAME),BuildSearchDataJs(listSorted));
			WriteTextFile(Path.Combine(context.Folder,INDEX_FILE_NAME),BuildIndexHtml(listSorted,context.DateTimeExport));
		}

		///<summary>The search data loaded by index.html with a script tag (fetching a file is blocked for file:// pages): every page's number, title
		///and lower-case plain text.</summary>
		public static string BuildSearchDataJs(List<WikiPage> listWikiPages) {
			List<Dictionary<string,string>> listEntries=new List<Dictionary<string,string>>();
			for(int i=0;i<listWikiPages.Count;i++) {
				listEntries.Add(new Dictionary<string,string> {
					{ "n",listWikiPages[i].WikiPageNum.ToString() },
					{ "t",listWikiPages[i].PageTitle },
					{ "c",(listWikiPages[i].PageContentPlainText??"").ToLowerInvariant() },
				});
			}
			JsonSerializerSettings jsonSerializerSettings=new JsonSerializerSettings();
			jsonSerializerSettings.StringEscapeHandling=StringEscapeHandling.EscapeHtml;
			string json=JsonConvert.SerializeObject(listEntries,jsonSerializerSettings);
			//Line and paragraph separators are not allowed inside string literals in older JavaScript engines.
			json=json.Replace("\u2028","\\u2028").Replace("\u2029","\\u2029");
			return "window.ODRO_PAGES="+json+";\n";
		}

		///<summary>The index page: banner, the alphabetical list of pages (usable without JavaScript) and the search box.</summary>
		public static string BuildIndexHtml(List<WikiPage> listWikiPagesSorted,DateTime dateTimeExport) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n");
			stringBuilder.Append("<title>"+Html(Lans.g("WikiStaticExport","Wiki (read-only copy)"))+"</title>\n");
			stringBuilder.Append("<style>\n"
				+"body{font-family:arial,sans-serif;font-size:10pt;margin:8px;}\n"
				+"h1{font-size:18pt;color:#005677;margin:8px 0;}\n"
				+"a{color:rgb(68,81,199);text-decoration:none;}\n"
				+"a:hover{text-decoration:underline;}\n"
				+"#odroSearch{width:320px;max-width:90%;padding:3px;font-size:10pt;}\n"
				+"#odroList{columns:3 220px;padding-left:18px;}\n"
				+"#odroList li{margin:2px 0;}\n"
				+"</style>\n</head>\n<body>\n");
			stringBuilder.Append(GetBannerHtml(dateTimeExport,""));
			stringBuilder.Append("<h1>"+Html(Lans.g("WikiStaticExport","Wiki"))+"</h1>\n");
			WikiPage wikiPageHome=listWikiPagesSorted.FirstOrDefault(x => string.Equals(x.PageTitle,"Home",StringComparison.OrdinalIgnoreCase));
			if(wikiPageHome!=null) {
				stringBuilder.Append("<p><a href=\""+PAGES_FOLDER_NAME+"/"+GetPageFileName(wikiPageHome.WikiPageNum)+"\">"
					+Html(wikiPageHome.PageTitle)+"</a></p>\n");
			}
			stringBuilder.Append("<p><input type=\"search\" id=\"odroSearch\" placeholder=\""+Html(Lans.g("WikiStaticExport","Search titles and content"))
				+"\" autofocus> <label><input type=\"checkbox\" id=\"odroContent\" checked> "+Html(Lans.g("WikiStaticExport","Include page content"))
				+"</label></p>\n");
			stringBuilder.Append("<p id=\"odroCount\">"+listWikiPagesSorted.Count+" "+Html(Lans.g("WikiStaticExport","pages"))+"</p>\n");
			stringBuilder.Append("<ul id=\"odroList\">\n");
			for(int i=0;i<listWikiPagesSorted.Count;i++) {
				stringBuilder.Append("<li data-num=\""+listWikiPagesSorted[i].WikiPageNum+"\"><a href=\""+PAGES_FOLDER_NAME+"/"
					+GetPageFileName(listWikiPagesSorted[i].WikiPageNum)+"\">"+Html(listWikiPagesSorted[i].PageTitle)+"</a></li>\n");
			}
			stringBuilder.Append("</ul>\n");
			stringBuilder.Append("<script src=\""+SEARCH_DATA_FILE_NAME+"\"></script>\n");
			stringBuilder.Append("<script>\n"+GetSearchScript(Lans.g("WikiStaticExport","of"),Lans.g("WikiStaticExport","pages match"),
				Lans.g("WikiStaticExport","pages"))+"</script>\n");
			stringBuilder.Append("</body>\n</html>\n");
			return stringBuilder.ToString();
		}

		///<summary>Plain JavaScript (ES5, no libraries) that hides the list items that do not match every word typed, in the title or, when the box
		///is checked, in the page text.</summary>
		private static string GetSearchScript(string textOf,string textPagesMatch,string textPages) {
			return "(function(){\n"
				+"var input=document.getElementById('odroSearch'),check=document.getElementById('odroContent'),count=document.getElementById('odroCount');\n"
				+"var items=document.getElementById('odroList').getElementsByTagName('li');\n"
				+"var data=window.ODRO_PAGES||[],byNum={},i;\n"
				+"for(i=0;i<data.length;i++){byNum[data[i].n]=data[i];}\n"
				+"function run(){\n"
				+" var words=input.value.toLowerCase().split(/\\s+/),tokens=[],shown=0,j;\n"
				+" for(j=0;j<words.length;j++){if(words[j].length>0){tokens.push(words[j]);}}\n"
				+" for(i=0;i<items.length;i++){\n"
				+"  var page=byNum[items[i].getAttribute('data-num')];\n"
				+"  var text=(page?page.t:(items[i].textContent||'')).toLowerCase();\n"
				+"  if(check.checked&&page){text+='\\n'+page.c;}\n"
				+"  var isMatch=true;\n"
				+"  for(j=0;j<tokens.length;j++){if(text.indexOf(tokens[j])<0){isMatch=false;break;}}\n"
				+"  items[i].style.display=isMatch?'':'none';\n"
				+"  if(isMatch){shown++;}\n"
				+" }\n"
				+" count.textContent=tokens.length?(shown+' "+JsString(textOf)+" '+items.length+' "+JsString(textPagesMatch)+"'):(items.length+' "
					+JsString(textPages)+"');\n"
				+"}\n"
				+"input.addEventListener('input',run);\n"
				+"check.addEventListener('change',run);\n"
				+"run();\n"
				+"})();\n";
		}

		///<summary>The banner at the top of every page of the copy. indexHref is blank on the index page itself.</summary>
		public static string GetBannerHtml(DateTime dateTimeExport,string indexHref) {
			string text=Lans.g("WikiStaticExport","Read-only copy exported")+" "+dateTimeExport.ToShortDateString()+" "+dateTimeExport.ToShortTimeString()+".";
			string html="<div class=\"odro-banner\" style=\"background-color:#fff4ce;border:1px solid #c9a227;padding:4px 8px;margin:0 0 6px 0;"
				+"font-family:arial,sans-serif;font-size:9pt;color:#000000;\">"+Html(text);
			if(!string.IsNullOrEmpty(indexHref)) {
				html+=" <a href=\""+Html(indexHref)+"\">"+Html(Lans.g("WikiStaticExport","All pages and search"))+"</a>";
			}
			return html+"</div>\n";
		}

		///<summary>Adds a meta charset and a title to the head (creating a head when the master page has none) and puts the banner at the top of the
		///body.</summary>
		public static string InsertHeadAndBanner(string html,string pageTitle,string bannerHtml) {
			string headContent="<title>"+Html(pageTitle)+"</title>\n";
			Match matchHead=Regex.Match(html,@"<head\b[^>]*>",RegexOptions.IgnoreCase);
			if(matchHead.Success) {
				int indexHeadEnd=html.IndexOf("</head>",matchHead.Index,StringComparison.OrdinalIgnoreCase);
				string head=(indexHeadEnd>=0 ? html.Substring(matchHead.Index,indexHeadEnd-matchHead.Index) : "");
				if(head.IndexOf("charset",StringComparison.OrdinalIgnoreCase)<0) {
					headContent="<meta charset=\"utf-8\">\n"+headContent;
				}
				if(Regex.IsMatch(head,@"<title\b",RegexOptions.IgnoreCase)) {
					headContent=headContent.Replace("<title>"+Html(pageTitle)+"</title>\n","");
				}
				html=html.Insert(matchHead.Index+matchHead.Length,"\n"+headContent);
			}
			else {
				Match matchHtml=Regex.Match(html,@"<html\b[^>]*>",RegexOptions.IgnoreCase);
				string head="<head>\n<meta charset=\"utf-8\">\n"+headContent+"</head>\n";
				if(matchHtml.Success) {
					html=html.Insert(matchHtml.Index+matchHtml.Length,"\n"+head);
				}
				else {
					html=head+html;
				}
			}
			Match matchBody=Regex.Match(html,@"<body\b[^>]*>",RegexOptions.IgnoreCase);
			if(matchBody.Success) {
				return html.Insert(matchBody.Index+matchBody.Length,"\n"+bannerHtml);
			}
			return bannerHtml+html;
		}
		#endregion Export steps

		#region Helpers
		///<summary>The file of a page inside pages\: its WikiPageNum, which stays the same when the page is renamed.</summary>
		public static string GetPageFileName(long wikiPageNum) {
			return wikiPageNum+".html";
		}

		///<summary>The full path of index.html in the folder.</summary>
		public static string GetIndexPath(string folder) {
			return Path.Combine(folder,INDEX_FILE_NAME);
		}

		///<summary>Writes a UTF-8 file (with byte order mark, so browsers read it as UTF-8 whatever the master page says) through a temporary file, so
		///a reader never sees half a page.</summary>
		private static void WriteTextFile(string path,string content) {
			string pathTemp=path+".tmp";
			File.WriteAllText(pathTemp,content,new UTF8Encoding(true));
			if(File.Exists(path)) {
				File.Delete(path);
			}
			File.Move(pathTemp,path);
		}

		private static List<string> ReadManifest(string filesFolder) {
			string path=Path.Combine(filesFolder,FILES_MANIFEST_NAME);
			if(!File.Exists(path)) {
				return new List<string>();
			}
			//Only plain names written by this class; anything with a path in it is ignored so a damaged manifest can never point outside _files.
			return File.ReadAllLines(path).Select(x => x.Trim())
				.Where(x => x!="" && x.IndexOfAny(new char[] { '\\','/',':' })<0 && x!=FILES_MANIFEST_NAME && !x.StartsWith("."))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		private static void WriteManifest(string filesFolder,List<string> listCopyNames) {
			List<string> listLines=listCopyNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList();
			WriteTextFile(Path.Combine(filesFolder,FILES_MANIFEST_NAME),string.Join("\r\n",listLines)+"\r\n");
		}

		private static string NormalizeFolder(string folder) {
			return Path.GetFullPath(folder).TrimEnd('\\','/');
		}

		///<summary>True when path is the folder or inside it. Case-insensitive, as Windows paths are.</summary>
		public static bool IsSameOrInside(string path,string folder) {
			string pathFull=NormalizeFolder(path);
			string folderFull=NormalizeFolder(folder);
			return string.Equals(pathFull,folderFull,StringComparison.OrdinalIgnoreCase)
				|| pathFull.StartsWith(folderFull+"\\",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>IsSameOrInside that returns false instead of throwing for a path that is not valid.</summary>
		private static bool IsInsideFolderSafe(string path,string folder) {
			try {
				return IsSameOrInside(path,folder);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		private static string Html(string text) {
			return WebUtility.HtmlEncode(text??"");
		}

		///<summary>Escapes text for a single-quoted JavaScript string.</summary>
		private static string JsString(string text) {
			return (text??"").Replace("\\","\\\\").Replace("'","\\'").Replace("<","\\x3c").Replace("\r","").Replace("\n"," ");
		}
		#endregion Helpers
	}

	///<summary>State of one export run.</summary>
	public class WikiExportContext {
		public string Folder;
		public string PagesFolder;
		public string FilesFolder;
		///<summary>The wiki image folder, or null when it is not available.</summary>
		public string WikiFolder;
		public DateTime DateTimeExport;
		///<summary>Title of every page in the copy (case-insensitive, like page titles in the database) to its WikiPageNum.</summary>
		public Dictionary<string,long> DictTitleToPageNum=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
		///<summary>Name in _files to source path.</summary>
		public Dictionary<string,string> DictFilesToCopy=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		public WikiExportResult Result=new WikiExportResult();
	}

	///<summary>What an export did. ErrorMessage is blank when it ran to the end.</summary>
	[Serializable]
	public class WikiExportResult {
		public string Folder="";
		public DateTime DateTimeExport;
		public bool IsFullExport;
		public int CountPagesWritten;
		public int CountPageFilesRemoved;
		public int CountFilesCopied;
		///<summary>Pages the renderer refused; they were written as plain text.</summary>
		public List<string> ListPagesFailed=new List<string>();
		///<summary>Images and files that could not be found or copied.</summary>
		public List<string> ListFilesMissing=new List<string>();
		public string ErrorMessage="";

		public bool IsSuccess {
			get { return string.IsNullOrEmpty(ErrorMessage); }
		}

		///<summary>A short summary for the user.</summary>
		public string GetSummary() {
			if(!IsSuccess) {
				return Lans.g("WikiStaticExport","The read-only copy could not be written to")+" "+Folder+": "+ErrorMessage;
			}
			string summary=Lans.g("WikiStaticExport","Read-only copy written to")+" "+Folder+" ("+DateTimeExport.ToShortDateString()+" "
				+DateTimeExport.ToShortTimeString()+"). "+Lans.g("WikiStaticExport","Pages written:")+" "+CountPagesWritten+".";
			if(ListPagesFailed.Count>0) {
				summary+=" "+Lans.g("WikiStaticExport","Pages shown as plain text because of a markup error:")+" "+string.Join(", ",ListPagesFailed.Take(10))
					+(ListPagesFailed.Count>10 ? ", ..." : "")+".";
			}
			if(ListFilesMissing.Count>0) {
				summary+=" "+Lans.g("WikiStaticExport","Images or files not found:")+" "+ListFilesMissing.Distinct().Count()+".";
			}
			return summary;
		}
	}
}
