// Tests for package B-WIKI that need no database: the search limit setting, the read-only copy's HTML rewriting, index and search data.
// Synthetic content only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenDentBusiness;

namespace BWIKITests {
	///<summary>v25_2-wiki-search-limit: the per-user setting and the per-tier limit helpers.</summary>
	[TestClass]
	public class WikiSearchLimitRuleTests {
		[TestMethod]
		public void Default_IsOffWithOneHundred() {
			WikiSearchLimit wikiSearchLimit=new WikiSearchLimit();
			Assert.IsFalse(wikiSearchLimit.IsEnabled);
			Assert.AreEqual(100,wikiSearchLimit.Limit);
			Assert.AreEqual(0,wikiSearchLimit.GetLimitForSearch(),"Off means no limit.");
			Assert.AreEqual("0,100",wikiSearchLimit.ToValueString());
		}

		[TestMethod]
		public void Parse_RoundTripsAndClamps() {
			WikiSearchLimit wikiSearchLimit=WikiSearchLimit.Parse("1,250");
			Assert.IsTrue(wikiSearchLimit.IsEnabled);
			Assert.AreEqual(250,wikiSearchLimit.Limit);
			Assert.AreEqual(250,wikiSearchLimit.GetLimitForSearch());
			Assert.AreEqual("1,250",wikiSearchLimit.ToValueString());
			Assert.AreEqual(5000,WikiSearchLimit.Parse("1,99999").Limit,"Above 5000 is clamped.");
			Assert.AreEqual(1,WikiSearchLimit.Parse("1,0").Limit,"Below 1 is clamped.");
			Assert.AreEqual(1,WikiSearchLimit.Parse(" 1 , -7 ").Limit);
			Assert.IsFalse(WikiSearchLimit.Parse("0,40").IsEnabled);
			Assert.AreEqual(40,WikiSearchLimit.Parse("0,40").Limit);
		}

		[TestMethod]
		public void Parse_MalformedReadsAsDefault() {
			foreach(string value in new[] { null,"","   ","1","true,5","1,abc","2,10","1,2,3" }) {
				WikiSearchLimit wikiSearchLimit=WikiSearchLimit.Parse(value);
				Assert.IsFalse(wikiSearchLimit.IsEnabled,"Value: "+value);
				Assert.AreEqual(100,wikiSearchLimit.Limit,"Value: "+value);
			}
		}

		[TestMethod]
		public void Constructor_ClampsAndIsSameAsComparesBoth() {
			Assert.AreEqual(5000,new WikiSearchLimit(true,6000).Limit);
			Assert.IsTrue(new WikiSearchLimit(true,30).IsSameAs(new WikiSearchLimit(true,30)));
			Assert.IsFalse(new WikiSearchLimit(true,30).IsSameAs(new WikiSearchLimit(false,30)));
			Assert.IsFalse(new WikiSearchLimit(true,30).IsSameAs(new WikiSearchLimit(true,31)));
			Assert.IsFalse(new WikiSearchLimit().IsSameAs(null));
		}

		[TestMethod]
		public void LimitHelpers_NoLimitAtZero() {
			Assert.AreEqual("",WikiPages.GetSearchLimitClause(0));
			Assert.AreEqual("",WikiPages.GetSearchLimitClause(-3));
			Assert.AreEqual(" LIMIT 25",WikiPages.GetSearchLimitClause(25));
			List<string> listTitles=new List<string> { "a","b" };
			Assert.IsFalse(WikiPages.IsSearchLimitReached(listTitles,0));
			Assert.IsFalse(WikiPages.IsSearchLimitReached(listTitles,3));
			Assert.IsTrue(WikiPages.IsSearchLimitReached(listTitles,2));
			Assert.IsTrue(WikiPages.IsSearchLimitReached(listTitles,1));
		}
	}

	///<summary>v25_2-wiki-readonly-offline: the parts of the export that are plain text processing.</summary>
	[TestClass]
	public class WikiStaticExportRuleTests {
		///<summary>Shaped like the seed master page: a head with styles and the body placeholder, no html start tag.</summary>
		public const string MASTER_PAGE="<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\">\r\n<head>\r\n"
			+"<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />\r\n<style type=\"text/css\">\r\nbody{font-size:9pt;}\r\n"
			+".PageNotExists{border-bottom:1px dashed #000000;}\r\n</style>\r\n</head>\r\n@@@body@@@\r\n</html>";
		private static string _folderTemp;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_folderTemp=Path.Combine(Path.GetTempPath(),"bwiki_rules_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folderTemp);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			try {
				Directory.Delete(_folderTemp,true);
			}
			catch(Exception) {
				//Synthetic files only.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			//The renderer wraps every page in the master page. Set here so these tests need no database.
			WikiPages.WikiPageMaster=new WikiPage { PageTitle="_Master",PageContent=MASTER_PAGE };
		}

		private static WikiExportContext NewContext(string wikiFolder) {
			WikiExportContext context=new WikiExportContext();
			context.Folder=Path.Combine(_folderTemp,"out");
			context.PagesFolder=Path.Combine(context.Folder,WikiStaticExport.PAGES_FOLDER_NAME);
			context.FilesFolder=Path.Combine(context.Folder,WikiStaticExport.FILES_FOLDER_NAME);
			context.WikiFolder=wikiFolder;
			context.DateTimeExport=new DateTime(2026,10,24,9,30,0);
			context.DictTitleToPageNum["Home"]=5;
			context.DictTitleToPageNum["Server Down & Out"]=12;
			return context;
		}

		[TestMethod]
		public void RewriteAnchors_WikiLinksPointAtPageFiles() {
			WikiExportContext context=NewContext(null);
			string html="<p><a href=\"wiki:Home\">Home</a> <a href=\"wiki:server down &amp; out\">S</a> "
				+"<a class=\"PageNotExists\" href=\"wiki:INVALID WIKIPAGE LINK 1\">x</a> <a href=\"wiki:Archived Page\">y</a></p>";
			string result=WikiStaticExport.RewriteAnchors(html,context);
			StringAssert.Contains(result,"<a href=\"5.html\">Home</a>");
			StringAssert.Contains(result,"href=\"12.html\"","Titles match without regard to case, after decoding &amp;.");
			Assert.IsFalse(result.Contains("wiki:"),result);
			//Links to pages that are not in the copy are kept as marked dead links.
			Assert.AreEqual(2,Regex.Matches(result,"href=\"#\"").Count,result);
			Assert.AreEqual(2,Regex.Matches(result,"class=\"PageNotExists\"").Count,"One class was already there; it is not doubled.");
			StringAssert.Contains(result,"title=\"This page is not in the read-only copy.\"");
		}

		[TestMethod]
		public void RewriteAnchors_NonBreakingSpacesInTitlesMatch() {
			WikiExportContext context=NewContext(null);
			context.DictTitleToPageNum["Two  Spaces"]=33;
			string result=WikiStaticExport.RewriteAnchors("<a href=\"wiki:Two\u00A0\u00A0Spaces\">t</a>",context);
			StringAssert.Contains(result,"href=\"33.html\"");
		}

		[TestMethod]
		public void RewriteAnchors_FilesInsideTheWikiFolderAreCopiedOthersLinked() {
			string wikiFolder=Path.Combine(_folderTemp,"AtoZ","Wiki");
			Directory.CreateDirectory(Path.Combine(wikiFolder,"docs"));
			string fileInside=Path.Combine(wikiFolder,"docs","fire drill.txt");
			File.WriteAllText(fileInside,"synthetic");
			WikiExportContext context=NewContext(wikiFolder);
			string html="<a href=\"wikifile:"+fileInside.Replace("\\","/")+"\">file:x</a>"
				+"<a href=\"wikifile:C:/Office Docs/list.txt\">file:y</a>"
				+"<a href=\"wikifile://server/share/plan.pdf\">file:z</a>"
				+"<a href=\"folder:C:/Office Docs/\">folder:w</a>"
				+"<a href=\"wikifilecloud:AtoZ/x.jpg\">c</a><a href=\"foldercloud:AtoZ/\">d</a>"
				+"<a href=\"https://example.com/a?b=1&amp;c=2\">web</a><a href=\"#section\">bookmark</a>";
			string result=WikiStaticExport.RewriteAnchors(html,context);
			string copyName=WikiStaticExport.GetCopyName(fileInside);
			StringAssert.Contains(result,"href=\"../_files/"+copyName+"\"");
			Assert.IsTrue(context.DictFilesToCopy.ContainsKey(copyName));
			Assert.AreEqual(fileInside,context.DictFilesToCopy[copyName]);
			StringAssert.Contains(result,"href=\"file:///C:/Office%20Docs/list.txt\"","Outside the wiki folder: linked, not copied.");
			StringAssert.Contains(result,"href=\"file://server/share/plan.pdf\"");
			StringAssert.Contains(result,"href=\"file:///C:/Office%20Docs/\"");
			Assert.AreEqual(1,context.DictFilesToCopy.Count);
			Assert.AreEqual(2,Regex.Matches(result,"href=\"#\"").Count,"Both cloud links are disabled.");
			StringAssert.Contains(result,"href=\"https://example.com/a?b=1&amp;c=2\"","Web links are not touched.");
			StringAssert.Contains(result,"href=\"#section\"");
			Assert.IsFalse(Regex.IsMatch(result,"href=\"(wikifile|folder|wikifilecloud|foldercloud):"),result);
		}

		[TestMethod]
		public void BuildPageHtml_RendersImagesBannerAndTitle() {
			string wikiFolder=Path.Combine(_folderTemp,"AtoZ2","Wiki");
			Directory.CreateDirectory(wikiFolder);
			File.WriteAllBytes(Path.Combine(wikiFolder,"floor plan.png"),new byte[] { 1,2,3 });
			WikiExportContext context=NewContext(wikiFolder);
			WikiPage wikiPage=new WikiPage { WikiPageNum=5,PageTitle="Home",
				PageContent="<h1>Home</h1>\r\nSee the map:\r\n[[img:floor plan.png]]\r\nand [[img:missing.png]]\r\n[[keywords: main]]" };
			string html=WikiStaticExport.BuildPageHtml(context,wikiPage);
			string copyName=WikiStaticExport.GetCopyName(Path.Combine(wikiFolder,"floor plan.png"));
			StringAssert.Contains(html,"<img src=\"../_files/"+copyName+"\"");
			StringAssert.Contains(html,"[image not available: missing.png]");
			CollectionAssert.Contains(context.Result.ListFilesMissing,"missing.png");
			StringAssert.Contains(html,"<title>Home</title>");
			StringAssert.Contains(html,"Read-only copy exported "+context.DateTimeExport.ToShortDateString());
			StringAssert.Contains(html,"href=\"../index.html\"");
			StringAssert.Contains(html,".PageNotExists","The master page styles are kept.");
			Assert.IsTrue(html.IndexOf("odro-banner")>html.IndexOf("<body>"),"The banner is inside the body.");
			Assert.IsFalse(html.Contains("ODROIMAGETOKEN"),"No image token is left behind.");
			Assert.AreEqual(0,context.Result.ListPagesFailed.Count);
		}

		[TestMethod]
		public void BuildPageHtml_BrokenMarkupBecomesPlainText() {
			WikiExportContext context=NewContext(null);
			WikiPage wikiPage=new WikiPage { WikiPageNum=9,PageTitle="Broken",PageContent="<h1>Broken</h1>\r\n[[unclosed",
				PageContentPlainText="Broken unclosed <tag>" };
			string html=WikiStaticExport.BuildPageHtml(context,wikiPage);
			CollectionAssert.Contains(context.Result.ListPagesFailed,"Broken");
			StringAssert.Contains(html,"<pre");
			StringAssert.Contains(html,"Broken unclosed &lt;tag&gt;");
			StringAssert.Contains(html,"<title>Broken</title>");
			StringAssert.Contains(html,"odro-banner");
		}

		[TestMethod]
		public void InsertHeadAndBanner_HandlesMissingHeadAndExistingTitle() {
			string banner="<div class=\"odro-banner\">B</div>\n";
			string result=WikiStaticExport.InsertHeadAndBanner("<html><body><p>x</p></body></html>","A & B",banner);
			StringAssert.Contains(result,"<head>\n<meta charset=\"utf-8\">\n<title>A &amp; B</title>\n</head>");
			Assert.IsTrue(result.IndexOf("<head>")<result.IndexOf("<body>"));
			StringAssert.Contains(result,"<body>\n"+banner);
			result=WikiStaticExport.InsertHeadAndBanner("<head><title>Kept</title><meta charset=\"utf-8\"></head><body></body>","New",banner);
			Assert.AreEqual(1,Regex.Matches(result,"<title>").Count,"A title the master page already has is kept.");
			Assert.AreEqual(1,Regex.Matches(result,"charset").Count);
			result=WikiStaticExport.InsertHeadAndBanner("<p>no tags</p>","T",banner);
			Assert.IsTrue(result.StartsWith(banner),result);
		}

		[TestMethod]
		public void BuildIndexHtml_ListsPagesAndNeedsNothingOutside() {
			List<WikiPage> listWikiPages=new List<WikiPage> {
				new WikiPage { WikiPageNum=3,PageTitle="alpha <one>" },
				new WikiPage { WikiPageNum=5,PageTitle="Home" },
				new WikiPage { WikiPageNum=12,PageTitle="Server Down" },
			};
			string html=WikiStaticExport.BuildIndexHtml(listWikiPages,new DateTime(2026,10,24,9,30,0));
			StringAssert.Contains(html,"<li data-num=\"3\"><a href=\"pages/3.html\">alpha &lt;one&gt;</a></li>");
			Assert.IsTrue(html.IndexOf("pages/3.html")<html.IndexOf("pages/12.html"),"The list keeps the given (alphabetical) order.");
			StringAssert.Contains(html,"<p><a href=\"pages/5.html\">Home</a></p>","Home is linked at the top.");
			StringAssert.Contains(html,"<script src=\"search-data.js\"></script>");
			StringAssert.Contains(html,"Read-only copy exported");
			//Self-contained: no script, style or image from anywhere else.
			Assert.IsFalse(Regex.IsMatch(html,"(src|href)=\"(https?:)?//",RegexOptions.IgnoreCase),html);
			Assert.IsFalse(html.Contains("<link"),"No external style sheet.");
		}

		[TestMethod]
		public void BuildSearchDataJs_EscapesForAScriptFile() {
			List<WikiPage> listWikiPages=new List<WikiPage> {
				new WikiPage { WikiPageNum=7,PageTitle="Quote \" and </script>",PageContentPlainText="Call IT\u2028NOW 'please'" },
			};
			string js=WikiStaticExport.BuildSearchDataJs(listWikiPages);
			Assert.IsTrue(js.StartsWith("window.ODRO_PAGES="));
			Assert.IsTrue(js.TrimEnd().EndsWith(";"));
			Assert.IsFalse(js.Contains("</script>"),"Escaped so it can never end a script block.");
			Assert.IsFalse(js.Contains("\u2028"));
			string json=js.Substring("window.ODRO_PAGES=".Length).TrimEnd().TrimEnd(';');
			List<Dictionary<string,string>> listEntries=JsonConvert.DeserializeObject<List<Dictionary<string,string>>>(json);
			Assert.AreEqual(1,listEntries.Count);
			Assert.AreEqual("7",listEntries[0]["n"]);
			Assert.AreEqual("Quote \" and </script>",listEntries[0]["t"]);
			Assert.AreEqual("call it\u2028now 'please'",listEntries[0]["c"],"Content is lower case for the search.");
		}

		[TestMethod]
		public void GetCopyName_IsStableSafeAndUnique() {
			string nameA=WikiStaticExport.GetCopyName(@"C:\AtoZ\Wiki\a b+c.png");
			Assert.AreEqual(nameA,WikiStaticExport.GetCopyName(@"C:\AtoZ\Wiki\a b+c.png"),"Stable.");
			Assert.AreEqual(nameA.Substring(0,8),WikiStaticExport.GetCopyName(@"c:\atoz\wiki\A B+C.PNG").Substring(0,8),"The hash ignores case.");
			Assert.IsTrue(Regex.IsMatch(nameA,"^[0-9a-f]{8}_a_b_c\\.png$"),nameA);
			Assert.AreNotEqual(nameA,WikiStaticExport.GetCopyName(@"C:\AtoZ\Wiki\sub\a b+c.png"),"Same file name in another folder.");
		}

		[TestMethod]
		public void ToFileUri_LocalAndNetworkPaths() {
			Assert.AreEqual("file:///C:/A%20B/c.txt",WikiStaticExport.ToFileUri(@"C:\A B\c.txt"));
			Assert.AreEqual("file://server/share/x.pdf",WikiStaticExport.ToFileUri(@"\\server\share\x.pdf"));
			Assert.AreEqual("relative.txt",WikiStaticExport.ToFileUri("relative.txt"));
		}

		[TestMethod]
		public void ValidateExportPath_RefusesTheWikiAndAtoZFolders() {
			string atoZ=@"C:\OpenDentImages";
			string wiki=@"C:\OpenDentImages\Wiki";
			Assert.AreEqual("",WikiStaticExport.ValidateExportPath(@"C:\OpenDentImages\WikiReadOnly",wiki,atoZ));
			Assert.AreEqual("",WikiStaticExport.ValidateExportPath(@"\\backup-pc\share\Wiki",wiki,atoZ));
			Assert.AreEqual("",WikiStaticExport.ValidateExportPath(@"D:\WikiCopy",null,null));
			Assert.AreNotEqual("",WikiStaticExport.ValidateExportPath("",wiki,atoZ));
			Assert.AreNotEqual("",WikiStaticExport.ValidateExportPath(@"relative\folder",wiki,atoZ));
			Assert.AreNotEqual("",WikiStaticExport.ValidateExportPath(@"C:\OpenDentImages\Wiki",wiki,atoZ));
			Assert.AreNotEqual("",WikiStaticExport.ValidateExportPath(@"c:\opendentimages\wiki\copy\",wiki,atoZ));
			Assert.AreNotEqual("",WikiStaticExport.ValidateExportPath(@"C:\OpenDentImages\",wiki,atoZ));
			Assert.AreEqual("",WikiStaticExport.ValidateExportPath(@"C:\OpenDentImages\Wiki2",wiki,atoZ),"A sibling whose name starts the same is fine.");
		}

		[TestMethod]
		public void IsSameOrInside_ComparesWholeFolderNames() {
			Assert.IsTrue(WikiStaticExport.IsSameOrInside(@"C:\A\B",@"C:\A"));
			Assert.IsTrue(WikiStaticExport.IsSameOrInside(@"C:\A\",@"c:\a"));
			Assert.IsFalse(WikiStaticExport.IsSameOrInside(@"C:\AB",@"C:\A"));
			Assert.IsFalse(WikiStaticExport.IsSameOrInside(@"C:\",@"C:\A"));
		}

		[TestMethod]
		public void ReplaceImagesWithTokens_NumbersEachImage() {
			Dictionary<int,string> dictImageNames;
			string markup=WikiStaticExport.ReplaceImagesWithTokens("a [[img:x.png]] b [[img:sub\\y.jpg]] [[file:C:\\z.txt]]",out dictImageNames);
			Assert.AreEqual(2,dictImageNames.Count);
			Assert.AreEqual("x.png",dictImageNames[0]);
			Assert.AreEqual("sub\\y.jpg",dictImageNames[1]);
			Assert.IsFalse(markup.Contains("[[img:"));
			StringAssert.Contains(markup,"[[file:C:\\z.txt]]","Other markup is left for the renderer.");
		}
	}
}
