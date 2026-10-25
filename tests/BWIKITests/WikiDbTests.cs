// Database tests for package B-WIKI against a scratch MariaDB database (127.0.0.1:3306, root, blank password) with synthetic wiki pages only.
// Skipped (inconclusive) when no server answers or BWIKI_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenDentBusiness;
using OpenDentBusiness.FileIO;

namespace BWIKITests {
	[TestClass]
	public class WikiAssemblyCleanup {
		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			WikiScratchDb.Drop();
		}
	}

	///<summary>v25_2-wiki-search-limit: WikiPages.GetForSearch with a limit keeps the tier order and returns the first N titles of the unlimited
	///search.</summary>
	[TestClass]
	public class WikiSearchLimitDbTests {
		private static readonly string[] _arrayExpectedAll=new[] {
			//Keyword tier, by title.
			"Backup Server","Generator","Network Closet",
			//Title tier: new titles only (Backup Server is already in).
			"Server Down","Server Room",
			//Content tier, including Index, which only links to Server Down.
			"Index","Phone List","Zebra Notes",
		};

		[TestInitialize]
		public void TestInitialize() {
			string skipReason=WikiScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			WikiScratchDb.ClearWiki();
			WikiScratchDb.InsertPage("Backup Server","<h1>Backup Server</h1>\nThe server backup runs at night.",keyWords:"server, backup");
			WikiScratchDb.InsertPage("Generator","<h1>Generator</h1>\nPower for the rack.",keyWords:"server");
			WikiScratchDb.InsertPage("Network Closet","<h1>Network Closet</h1>\nSwitches.",keyWords:"Server, main");
			long numServerDown=WikiScratchDb.InsertPage("Server Down","<h1>Server Down</h1>\nWhat to do when the server is down.");
			WikiScratchDb.InsertPage("Server Room","<h1>Server Room</h1>\nKey is at the front desk.");
			WikiScratchDb.InsertPage("Index","<h1>Index</h1>\n[["+numServerDown+"]]");
			WikiScratchDb.InsertPage("Phone List","<h1>Phone List</h1>\nCall the server company.");
			WikiScratchDb.InsertPage("Zebra Notes","<h1>Zebra Notes</h1>\nthe server");
			WikiScratchDb.InsertPage("Holidays","<h1>Holidays</h1>\nClosed.");
			//Never in a normal search: archived, draft and underscore pages.
			WikiScratchDb.InsertPage("Old Server","<h1>Old Server</h1>\nserver",keyWords:"server",isDeleted:true);
			WikiScratchDb.InsertPage("Server Down","draft about the server",keyWords:"server",isDraft:true);
			WikiScratchDb.InsertPage("_Hidden server","server",keyWords:"server");
		}

		[TestMethod]
		public void NoLimit_KeepsTheTierOrder() {
			CollectionAssert.AreEqual(_arrayExpectedAll,WikiPages.GetForSearch("server",ignoreContent:false));
			CollectionAssert.AreEqual(_arrayExpectedAll,WikiPages.GetForSearch("server",ignoreContent:false,limit:0));
		}

		[TestMethod]
		public void Limit_ReturnsTheFirstNTitles() {
			for(int limit=1;limit<=_arrayExpectedAll.Length+2;limit++) {
				List<string> listTitles=WikiPages.GetForSearch("server",ignoreContent:false,limit:limit);
				CollectionAssert.AreEqual(_arrayExpectedAll.Take(limit).ToList(),listTitles,"Limit "+limit);
			}
		}

		[TestMethod]
		public void Limit_DuplicatesAcrossTiersDoNotShortenTheResult() {
			//Backup Server matches all three tiers. With 4, the title tier must still add Server Down after skipping it.
			CollectionAssert.AreEqual(new[] { "Backup Server","Generator","Network Closet","Server Down" },
				WikiPages.GetForSearch("server",ignoreContent:false,limit:4));
		}

		[TestMethod]
		public void Limit_LinkTierStillFindsLinkedPages() {
			//The content tier runs only when the title tier was not cut, so it searches for links to every title match.
			List<string> listTitles=WikiPages.GetForSearch("server",ignoreContent:false,limit:6);
			Assert.AreEqual("Index",listTitles.Last());
		}

		[TestMethod]
		public void Limit_WithIgnoreContent() {
			CollectionAssert.AreEqual(_arrayExpectedAll.Take(5).ToList(),WikiPages.GetForSearch("server",ignoreContent:true));
			CollectionAssert.AreEqual(_arrayExpectedAll.Take(4).ToList(),WikiPages.GetForSearch("server",ignoreContent:true,limit:4));
		}

		[TestMethod]
		public void Limit_WithMainPages_MovesMainPagesOfTheFirstN() {
			List<string> listAll=WikiPages.GetForSearch("server",ignoreContent:false,showMainPages:true);
			Assert.AreEqual("Network Closet",listAll[0],"Main pages first.");
			Assert.AreEqual(_arrayExpectedAll.Length,listAll.Count);
			CollectionAssert.AreEqual(new[] { "Backup Server","Generator" },WikiPages.GetForSearch("server",false,showMainPages:true,limit:2));
			CollectionAssert.AreEqual(new[] { "Network Closet","Backup Server","Generator" },WikiPages.GetForSearch("server",false,showMainPages:true,limit:3));
		}

		[TestMethod]
		public void Limit_ArchivedOnlySearch() {
			CollectionAssert.AreEqual(new[] { "Old Server" },WikiPages.GetForSearch("server",false,isDeleted:true,limit:1));
		}
	}

	///<summary>v25_2-wiki-search-limit: the setting is remembered per user in userodpref (type 25, Fkey 1) next to the include-content row.</summary>
	[TestClass]
	public class WikiSearchLimitPrefDbTests {
		[TestInitialize]
		public void TestInitialize() {
			string skipReason=WikiScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			WikiScratchDb.ClearWiki();
		}

		[TestMethod]
		public void SaveForUser_RoundTripsPerUserAndLeavesIncludeContentAlone() {
			Assert.IsTrue(new WikiSearchLimit().IsSameAs(WikiSearchLimit.GetForUser(7)));
			Assert.IsFalse(WikiSearchLimit.SaveForUser(7,new WikiSearchLimit()),"The default writes no row.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM userodpref"));
			//The Open Dental include-content row (Fkey 0).
			UserOdPrefs.Insert(new UserOdPref { UserNum=7,Fkey=0,FkeyType=UserOdFkeyType.WikiSearchIncludeContent,ValueString="1" });
			UserOdPrefs.RefreshCache();
			Assert.IsTrue(WikiSearchLimit.SaveForUser(7,new WikiSearchLimit(true,250)));
			UserOdPrefs.RefreshCache();
			Assert.AreEqual("1,250",DataCore.GetScalar("SELECT ValueString FROM userodpref WHERE UserNum=7 AND Fkey=1 AND FkeyType="
				+(int)UserOdFkeyType.WikiSearchIncludeContent));
			WikiSearchLimit wikiSearchLimit=WikiSearchLimit.GetForUser(7);
			Assert.IsTrue(wikiSearchLimit.IsEnabled);
			Assert.AreEqual(250,wikiSearchLimit.Limit);
			Assert.IsFalse(WikiSearchLimit.GetForUser(8).IsEnabled,"Other users keep the default.");
			Assert.IsTrue(WikiSearchLimit.SaveForUser(7,new WikiSearchLimit(false,250)));
			UserOdPrefs.RefreshCache();//The window signals InvalidType.UserOdPrefs after a change, which refreshes the cache.
			Assert.IsFalse(WikiSearchLimit.SaveForUser(7,new WikiSearchLimit(false,250)),"No change, no write.");
			Assert.IsFalse(WikiSearchLimit.GetForUser(7).IsEnabled);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM userodpref WHERE UserNum=7 AND Fkey=0"),"Include-content row untouched.");
			Assert.AreEqual("2",DataCore.GetScalar("SELECT COUNT(*) FROM userodpref WHERE UserNum=7"));
		}
	}

	///<summary>v25_1-misc-wiki-draft-compare: the published version a draft is compared with.</summary>
	[TestClass]
	public class WikiDraftCompareDbTests {
		[TestInitialize]
		public void TestInitialize() {
			string skipReason=WikiScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			WikiScratchDb.ClearWiki();
			Security.CurUser=Security.CurUser??new Userod { UserNum=1 };
		}

		private static WikiPage SavePage(string pageTitle,string pageContent) {
			WikiPage wikiPage=WikiPages.GetByTitle(pageTitle)??new WikiPage { PageTitle=pageTitle,KeyWords="" };
			wikiPage.PageContent=pageContent;
			wikiPage.UserNum=1;
			WikiPages.InsertAndArchive(wikiPage);
			return WikiPages.GetByTitle(pageTitle);
		}

		[TestMethod]
		public void LivePage_IsTheLeftSide() {
			WikiPage wikiPage=SavePage("Office Hours","<h1>Office Hours</h1>\nMonday to Friday.");
			WikiPage wikiPageDraft=wikiPage.Copy();
			wikiPageDraft.PageContent="<h1>Office Hours</h1>\nMonday to Thursday.";
			WikiPages.InsertAsDraft(wikiPageDraft);
			Assert.AreEqual(1,WikiPages.GetDraftsByTitle("Office Hours").Count);
			bool isFromHistory;
			WikiPageHist wikiPageHist=WikiPages.GetPublishedForDraftCompare("Office Hours",out isFromHistory);
			Assert.IsFalse(isFromHistory);
			Assert.AreEqual("<h1>Office Hours</h1>\nMonday to Friday.",wikiPageHist.PageContent,"The live page, not the draft.");
			Assert.AreEqual("Office Hours",wikiPageHist.PageTitle);
		}

		[TestMethod]
		public void ArchivedPage_UsesItsNewestHistoryVersion() {
			SavePage("Old Policy","<h1>Old Policy</h1>\nVersion one.");
			WikiPage wikiPage=SavePage("Old Policy","<h1>Old Policy</h1>\nVersion two.");
			WikiPage wikiPageDraft=wikiPage.Copy();
			wikiPageDraft.PageContent="<h1>Old Policy</h1>\nVersion three (draft).";
			WikiPages.InsertAsDraft(wikiPageDraft);
			WikiPages.Archive("Old Policy",1);
			Assert.IsNull(WikiPages.GetByTitle("Old Policy"),"Archived.");
			bool isFromHistory;
			WikiPageHist wikiPageHist=WikiPages.GetPublishedForDraftCompare("Old Policy",out isFromHistory);
			Assert.IsTrue(isFromHistory);
			Assert.IsNotNull(wikiPageHist);
			Assert.AreEqual("<h1>Old Policy</h1>\nVersion two.",wikiPageHist.PageContent);
		}

		[TestMethod]
		public void HistoryOrder_NewestWins() {
			//A page deleted outright (older data): only history rows remain.
			DataCore.NonQ("INSERT INTO wikipagehist (UserNum,PageTitle,PageContent,DateTimeSaved,IsDeleted) VALUES "
				+"(1,'Gone','first','2026-01-01 08:00:00',0),(1,'Gone','last','2026-03-01 08:00:00',1),(1,'Gone','middle','2026-02-01 08:00:00',0)");
			bool isFromHistory;
			WikiPageHist wikiPageHist=WikiPages.GetPublishedForDraftCompare("Gone",out isFromHistory);
			Assert.IsTrue(isFromHistory);
			Assert.AreEqual("last",wikiPageHist.PageContent);
			Assert.IsNull(WikiPages.GetPublishedForDraftCompare("Never Existed",out isFromHistory),"Compared with an empty page.");
			Assert.IsTrue(isFromHistory);
		}
	}

	///<summary>v25_2-wiki-readonly-offline: the static export against synthetic pages and a temporary A to Z folder.</summary>
	[TestClass]
	public class WikiStaticExportDbTests {
		private static string _folderRoot;
		private static string _folderAtoZ;
		private static string _folderWiki;
		private static string _folderOutside;
		private string _folderExport;
		private long _numHome;
		private long _numServerDown;
		private long _numOld;
		private long _numBroken;
		private long _numDraft;
		private long _numMaster;
		private long _numHidden;
		private static readonly DateTime _dateTimeExport=new DateTime(2026,10,24,21,15,0);

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_folderRoot=Path.Combine(Path.GetTempPath(),"bwiki_export_"+Guid.NewGuid().ToString("N"));
			_folderAtoZ=Path.Combine(_folderRoot,"OpenDentImages");
			_folderWiki=Path.Combine(_folderAtoZ,"Wiki");
			_folderOutside=Path.Combine(_folderRoot,"Office Docs");
			Directory.CreateDirectory(Path.Combine(_folderWiki,"docs"));
			Directory.CreateDirectory(_folderOutside);
			File.WriteAllBytes(Path.Combine(_folderWiki,"logo.png"),new byte[] { 137,80,78,71,1,2,3,4 });
			File.WriteAllText(Path.Combine(_folderWiki,"docs","emergency.txt"),"Synthetic emergency steps.");
			File.WriteAllText(Path.Combine(_folderOutside,"list.txt"),"Synthetic list.");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			FileAtoZ.LocalAtoZpath=null;
			if(Environment.GetEnvironmentVariable("BWIKI_KEEP_EXPORT")=="1") {
				return;//To look at the exported copies in a browser. Synthetic content only.
			}
			try {
				Directory.Delete(_folderRoot,true);
			}
			catch(Exception) {
				//Synthetic files only.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			string skipReason=WikiScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			FileAtoZ.LocalAtoZpath=_folderAtoZ;
			Security.CurUser=Security.CurUser??new Userod { UserNum=1 };
			WikiScratchDb.ClearWiki();
			DataCore.NonQ("DELETE FROM preference WHERE PrefName LIKE 'ForkWikiReadOnly%'");
			Prefs.RefreshCache();
			_numMaster=WikiScratchDb.InsertPage("_Master",WikiStaticExportRuleTests.MASTER_PAGE);
			WikiPages.ClearCache();
			_numHome=WikiScratchDb.InsertPage("Home","placeholder");
			_numServerDown=WikiScratchDb.InsertPage("Server Down","<h1>Server Down</h1>\nCall the IT company at 555-0100.\n[["+_numHome+"]]");
			_numOld=WikiScratchDb.InsertPage("Old Page","<h1>Old Page</h1>",isDeleted:true);
			_numBroken=WikiScratchDb.InsertPage("Broken Page","<h1>Broken Page</h1>\n[[unclosed");
			_numDraft=WikiScratchDb.InsertPage("Server Down","DRAFTONLY text",isDraft:true);
			_numHidden=WikiScratchDb.InsertPage("_Hidden","hidden");
			WikiScratchDb.SetContent(_numHome,"<h1>Home</h1>\nSee [["+_numServerDown+"]] and [["+_numOld+"]] and [[0]].\n[[img:logo.png]]\n"
				+"[[file:"+Path.Combine(_folderWiki,"docs","emergency.txt")+"]]\n[[file:"+Path.Combine(_folderOutside,"list.txt")+"]]\n"
				+"[[folder:"+_folderOutside+"]]\n[[keywords: main, home]]");
			_folderExport=Path.Combine(_folderRoot,"export_"+Guid.NewGuid().ToString("N").Substring(0,8));
		}

		private string PagePath(long wikiPageNum) {
			return Path.Combine(_folderExport,"pages",wikiPageNum+".html");
		}

		[TestMethod]
		public void ExportAll_WritesEveryPublishedPageAndNothingElse() {
			WikiExportResult wikiExportResult=WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			Assert.AreEqual(3,wikiExportResult.CountPagesWritten);
			Assert.IsTrue(File.Exists(PagePath(_numHome)));
			Assert.IsTrue(File.Exists(PagePath(_numServerDown)));
			Assert.IsTrue(File.Exists(PagePath(_numBroken)));
			foreach(long wikiPageNum in new[] { _numOld,_numDraft,_numMaster,_numHidden }) {
				Assert.IsFalse(File.Exists(PagePath(wikiPageNum)),"Not exported: "+wikiPageNum);
			}
			Assert.AreEqual(3,Directory.GetFiles(Path.Combine(_folderExport,"pages")).Length);
			string home=File.ReadAllText(PagePath(_numHome));
			StringAssert.Contains(home,"<title>Home</title>");
			StringAssert.Contains(home,"Read-only copy exported "+_dateTimeExport.ToShortDateString()+" "+_dateTimeExport.ToShortTimeString());
			StringAssert.Contains(home,"href=\""+_numServerDown+".html\">Server Down</a>");
			Assert.IsTrue(Regex.IsMatch(home,"<a title=\"[^\"]+\" class=\"PageNotExists\" href=\"#\">Old Page</a>"),"Archived target: dead link.");
			StringAssert.Contains(home,"INVALID WIKIPAGE LINK 1");
			string copyLogo=WikiStaticExport.GetCopyName(Path.Combine(_folderWiki,"logo.png"));
			StringAssert.Contains(home,"src=\"../_files/"+copyLogo+"\"");
			CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(_folderWiki,"logo.png")),File.ReadAllBytes(Path.Combine(_folderExport,"_files",copyLogo)));
			string copyEmergency=WikiStaticExport.GetCopyName(Path.Combine(_folderWiki,"docs","emergency.txt"));
			StringAssert.Contains(home,"href=\"../_files/"+copyEmergency+"\"");
			Assert.IsTrue(File.Exists(Path.Combine(_folderExport,"_files",copyEmergency)));
			StringAssert.Contains(home,"href=\""+new Uri(Path.Combine(_folderOutside,"list.txt")).AbsoluteUri+"\"","Outside the wiki folder: linked.");
			Assert.IsFalse(Directory.GetFiles(Path.Combine(_folderExport,"_files")).Any(x => x.EndsWith("list.txt")),"Not copied.");
			StringAssert.Contains(home,"keywords: main, home");
			Assert.IsFalse(home.Contains("DRAFTONLY"));
			//The page that does not render is still there as plain text.
			CollectionAssert.AreEqual(new[] { "Broken Page" },wikiExportResult.ListPagesFailed);
			StringAssert.Contains(File.ReadAllText(PagePath(_numBroken)),"<pre");
			//The index lists the three pages alphabetically.
			string index=File.ReadAllText(Path.Combine(_folderExport,"index.html"));
			int indexBroken=index.IndexOf(">Broken Page</a></li>");
			int indexHome=index.IndexOf(">Home</a></li>");
			int indexServerDown=index.IndexOf(">Server Down</a></li>");
			Assert.IsTrue(indexBroken>0 && indexBroken<indexHome && indexHome<indexServerDown,index);
			Assert.IsFalse(index.Contains("Old Page"));
			Assert.IsFalse(index.Contains("_Hidden"));
			Assert.IsFalse(index.Contains("_Master"));
			//The search data holds the same pages with their plain text.
			string js=File.ReadAllText(Path.Combine(_folderExport,"search-data.js"));
			List<Dictionary<string,string>> listEntries=JsonConvert.DeserializeObject<List<Dictionary<string,string>>>(
				js.Substring("window.ODRO_PAGES=".Length).TrimEnd().TrimEnd(';'));
			CollectionAssert.AreEquivalent(new[] { _numHome.ToString(),_numServerDown.ToString(),_numBroken.ToString() },listEntries.Select(x => x["n"]).ToList());
			StringAssert.Contains(listEntries.First(x => x["n"]==_numServerDown.ToString())["c"],"call the it company");
			Assert.IsTrue(wikiExportResult.IsSuccess);
		}

		[TestMethod]
		public void ExportAll_CopyIsSelfContained() {
			WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			List<string> listHtmlFiles=Directory.GetFiles(Path.Combine(_folderExport,"pages"),"*.html").ToList();
			listHtmlFiles.Add(Path.Combine(_folderExport,"index.html"));
			Regex regexLink=new Regex("\\b(href|src)=\"([^\"]*)\"",RegexOptions.IgnoreCase);
			int countRelative=0;
			foreach(string filePath in listHtmlFiles) {
				string html=File.ReadAllText(filePath);
				Assert.IsFalse(Regex.IsMatch(html,"href=\"(wiki|wikifile|folder|wikifilecloud|foldercloud):"),"Program-only link left in "+filePath);
				Assert.IsFalse(Regex.IsMatch(html,"<(script|img|link)\\b[^>]*(src|href)=\"(https?:)?//",RegexOptions.IgnoreCase),"External resource in "+filePath);
				foreach(Match match in regexLink.Matches(html)) {
					string link=System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);
					if(link.StartsWith("#") || link.StartsWith("file:") || link.StartsWith("http")) {
						continue;
					}
					string pathTarget=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath),Uri.UnescapeDataString(link.Split('#')[0])));
					Assert.IsTrue(File.Exists(pathTarget),"Broken link "+link+" in "+filePath);
					countRelative++;
				}
			}
			Assert.IsTrue(countRelative>=10,"Links between pages, to the index, the files and the search data: "+countRelative);
		}

		[TestMethod]
		public void ExportPages_AfterSave_UpdatesThatPageAndTheIndexOnly() {
			WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			byte[] arrayServerDownBefore=File.ReadAllBytes(PagePath(_numServerDown));
			WikiPage wikiPageHome=WikiPages.GetByTitle("Home");
			wikiPageHome.PageContent="<h1>Home</h1>\nUpdated text for the front desk.\n[["+_numServerDown+"]]";
			WikiPages.InsertAndArchive(wikiPageHome);
			WikiPage wikiPageNew=new WikiPage { PageTitle="Fire Drill",KeyWords="",UserNum=1,PageContent="<h1>Fire Drill</h1>\nMeet in the parking lot." };
			WikiPages.InsertAndArchive(wikiPageNew);
			WikiExportResult wikiExportResult=WikiStaticExport.ExportPages(_folderExport,new List<string> { "Home","Fire Drill" },_dateTimeExport.AddHours(1));
			Assert.AreEqual(2,wikiExportResult.CountPagesWritten);
			StringAssert.Contains(File.ReadAllText(PagePath(_numHome)),"Updated text for the front desk.");
			Assert.IsTrue(File.Exists(PagePath(wikiPageNew.WikiPageNum)));
			CollectionAssert.AreEqual(arrayServerDownBefore,File.ReadAllBytes(PagePath(_numServerDown)),"Other pages are not rewritten.");
			string index=File.ReadAllText(Path.Combine(_folderExport,"index.html"));
			StringAssert.Contains(index,">Fire Drill</a></li>");
			StringAssert.Contains(File.ReadAllText(Path.Combine(_folderExport,"search-data.js")),"meet in the parking lot");
		}

		[TestMethod]
		public void Archive_RemovesThePageAndFullExportMarksLinksToIt() {
			WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			WikiPages.Archive("Server Down",1);
			WikiExportResult wikiExportResult=WikiStaticExport.ExportPages(_folderExport,new List<string> { "Server Down" },_dateTimeExport);
			Assert.AreEqual(1,wikiExportResult.CountPageFilesRemoved);
			Assert.IsFalse(File.Exists(PagePath(_numServerDown)));
			Assert.IsFalse(File.ReadAllText(Path.Combine(_folderExport,"index.html")).Contains(">Server Down<"));
			WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			string home=File.ReadAllText(PagePath(_numHome));
			Assert.IsFalse(home.Contains(_numServerDown+".html"));
			Assert.IsTrue(Regex.IsMatch(home,"class=\"PageNotExists\" href=\"#\">Server Down</a>"),home);
		}

		[TestMethod]
		public void ExportAll_RemovesOnlyItsOwnStaleFiles() {
			WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			string copyLogo=WikiStaticExport.GetCopyName(Path.Combine(_folderWiki,"logo.png"));
			Assert.IsTrue(File.Exists(Path.Combine(_folderExport,"_files",copyLogo)));
			//Files the office put there.
			File.WriteAllText(Path.Combine(_folderExport,"pages","notes.txt"),"mine");
			File.WriteAllText(Path.Combine(_folderExport,"pages","readme.html"),"mine");
			File.WriteAllText(Path.Combine(_folderExport,"_files","keep.txt"),"mine");
			File.WriteAllText(Path.Combine(_folderExport,"keep.html"),"mine");
			//A page file whose page is gone, and an image no page shows any more.
			File.WriteAllText(Path.Combine(_folderExport,"pages","999999.html"),"stale");
			WikiScratchDb.SetContent(_numHome,"<h1>Home</h1>\nNo picture now.");
			WikiExportResult wikiExportResult=WikiStaticExport.ExportAll(_folderExport,_dateTimeExport);
			Assert.IsFalse(File.Exists(Path.Combine(_folderExport,"pages","999999.html")));
			Assert.AreEqual(1,wikiExportResult.CountPageFilesRemoved);
			Assert.IsFalse(File.Exists(Path.Combine(_folderExport,"_files",copyLogo)),"Copied by an earlier export and no longer used.");
			foreach(string path in new[] { @"pages\notes.txt",@"pages\readme.html",@"_files\keep.txt","keep.html" }) {
				Assert.IsTrue(File.Exists(Path.Combine(_folderExport,path)),"Left alone: "+path);
			}
			Assert.IsFalse(Directory.GetFiles(_folderExport,"*.tmp",SearchOption.AllDirectories).Any(),"No temporary files left.");
		}

		[TestMethod]
		public void TryExport_UnreachableFolderNeverThrows() {
			//A file where the folder should be: the folder cannot be created, as with a share that is down.
			string pathBlocked=Path.Combine(_folderRoot,"blocked_"+Guid.NewGuid().ToString("N").Substring(0,6));
			File.WriteAllText(pathBlocked,"not a folder");
			WikiExportResult wikiExportResult=WikiStaticExport.TryExport(pathBlocked,new List<string> { "Home" },isFullExport:false,_dateTimeExport);
			Assert.IsFalse(wikiExportResult.IsSuccess);
			Assert.AreSame(wikiExportResult,WikiStaticExport.LastResult);
			StringAssert.Contains(wikiExportResult.GetSummary(),pathBlocked);
			wikiExportResult=WikiStaticExport.TryExport(_folderRoot+"\\bad<name>",null,isFullExport:true,_dateTimeExport);
			Assert.IsFalse(wikiExportResult.IsSuccess);
			wikiExportResult=WikiStaticExport.TryExport(_folderExport,null,isFullExport:true,_dateTimeExport);
			Assert.IsTrue(wikiExportResult.IsSuccess,wikiExportResult.ErrorMessage);
			Assert.AreEqual(3,wikiExportResult.CountPagesWritten);
		}

		[TestMethod]
		public void Settings_RoundTripAndOffByDefault() {
			Assert.AreEqual("",WikiStaticExport.GetExportPath());
			Assert.IsFalse(WikiStaticExport.IsEnabled());
			Assert.IsTrue(WikiStaticExport.GetAutoExport(),"On when the row is missing.");
			Assert.IsFalse(WikiStaticExport.IsAutoExportOn(),"Nothing happens without a folder.");
			WikiStaticExport.ExportInBackground(new List<string> { "Home" },isFullExport:false);//Off: does nothing, starts no thread.
			Assert.IsTrue(WikiStaticExport.SaveSettings("  "+_folderExport+"  ",isAutoExport:false));
			Assert.AreEqual(_folderExport,WikiStaticExport.GetExportPath(),"Trimmed, and this workstation's cache is updated.");
			Assert.IsFalse(WikiStaticExport.GetAutoExport());
			Assert.IsFalse(WikiStaticExport.SaveSettings(_folderExport,isAutoExport:false),"No change.");
			Prefs.RefreshCache();
			Assert.AreEqual(_folderExport,WikiStaticExport.GetExportPath(),"Stored in the preference table.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+WikiStaticExport.PREF_AUTO_EXPORT+"'"));
			Assert.IsTrue(WikiStaticExport.SaveSettings("",isAutoExport:true));
			Prefs.RefreshCache();
			Assert.IsFalse(WikiStaticExport.IsEnabled());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+WikiStaticExport.PREF_EXPORT_PATH+"'"),"One row each.");
			string expectedDefault=Path.Combine(_folderAtoZ,"WikiReadOnly");
			Assert.AreEqual(expectedDefault.TrimEnd('\\'),WikiStaticExport.GetDefaultExportPath().TrimEnd('\\'));
			Assert.AreEqual(_folderWiki.TrimEnd('\\'),WikiStaticExport.GetWikiFolderOrNull().TrimEnd('\\'));
		}

		[TestMethod]
		public void ExportInBackground_WritesTheCopyAfterASave() {
			WikiStaticExport.SaveSettings(_folderExport,isAutoExport:true);
			WikiStaticExport.ExportInBackground(new List<string> { "Home" },isFullExport:false);
			DateTime dateTimeStop=DateTime.Now.AddSeconds(30);
			while(!File.Exists(PagePath(_numHome)) && DateTime.Now<dateTimeStop) {
				System.Threading.Thread.Sleep(100);
			}
			//Wait for the export lock, so the thread is done before the next test clears the tables.
			WikiStaticExport.TryExport(_folderExport,new List<string>(),isFullExport:false,_dateTimeExport);
			Assert.IsTrue(File.Exists(PagePath(_numHome)));
			Assert.IsTrue(File.Exists(Path.Combine(_folderExport,"index.html")));
			WikiStaticExport.SaveSettings("",isAutoExport:true);
		}
	}
}
