// The CCD and CCR stylesheets through the stylesheet child (package EHR-3 of docs/specs/ehr-rebuild-plan.md, docs/port-notes/EHR-3.md): golden
// pages of upstream's CCD-full.xml (a copy with its licensed codes replaced) and of a synthetic CCR with dates, recorded from the shipped
// stylesheets; readable dates; one child for a batch; non-ASCII text. Every page is made by a real OpenPmsDental.exe /ehr-render started by
// EhrStylesheetStarter from this folder: no XSLT runs in the test process, and no window is shown. EHR_RECORD_GOLDEN=1 records the golden pages
// again (the test is then inconclusive); a new golden page is reviewed before it is committed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace EhrTests {
	///<summary>The fixtures and golden pages under tests\EhrTests\Golden, read from the repository folder.</summary>
	public static class CdaGolden {
		public const string FOLDER=@"tests\EhrTests\Golden";

		public static string Path(string name) {
			return TestPaths.Combine(FOLDER+"\\"+name);
		}

		public static string ReadFixture(string name) {
			string path=Path(name);
			Assert.IsTrue(File.Exists(path),"Missing fixture: "+name);
			return File.ReadAllText(path,new UTF8Encoding(false));
		}

		///<summary>Line ends as \n, whatever the checkout made of them.</summary>
		public static string Normalize(string text) {
			return (text??"").Replace("\r\n","\n").Replace("\r","\n");
		}

		///<summary>Compares html with the golden page goldenName after normalizing line ends. With EHR_RECORD_GOLDEN=1, or when the page is missing,
		///records it and ends the test as inconclusive.</summary>
		public static void AssertMatches(string goldenName,string html) {
			Assert.IsNotNull(html,"No page for "+goldenName);
			string path=Path(goldenName);
			string actual=Normalize(html);
			if(Environment.GetEnvironmentVariable("EHR_RECORD_GOLDEN")=="1" || !File.Exists(path)) {
				File.WriteAllText(path,actual,new UTF8Encoding(false));
				Assert.Inconclusive("Recorded "+goldenName+": review it, then commit it.");
			}
			string expected=Normalize(File.ReadAllText(path,new UTF8Encoding(false)));
			if(expected==actual) {
				return;
			}
			int index=0;
			while(index<expected.Length && index<actual.Length && expected[index]==actual[index]) {
				index++;
			}
			int start=Math.Max(0,index-120);
			Assert.Fail(goldenName+" differs at character "+index+" (expected "+expected.Length+" characters, got "+actual.Length+").\r\nExpected: "
				+Excerpt(expected,start)+"\r\nActual:   "+Excerpt(actual,start));
		}

		private static string Excerpt(string text,int start) {
			if(start>=text.Length) {
				return "(end)";
			}
			return text.Substring(start,Math.Min(240,text.Length-start)).Replace("\n","\\n");
		}

		///<summary>The page made by the real child, or a failure with the reason.</summary>
		public static string Render(EhrRenderKind ehrRenderKind,string xml) {
			string html=EhrStylesheetStarter.Render(ehrRenderKind,xml,out string reason);
			Assert.IsNotNull(html,"Not rendered: "+reason);
			Assert.IsNull(reason);
			return html;
		}

		///<summary>The visible text of a page: tags removed, entities of the five XML characters decoded, white space collapsed.</summary>
		public static string VisibleText(string html) {
			string text=Regex.Replace(html,@"<style\b.*?</style>","",RegexOptions.Singleline|RegexOptions.IgnoreCase);
			text=Regex.Replace(text,@"<[^>]*>"," ");
			text=text.Replace("&lt;","<").Replace("&gt;",">").Replace("&quot;","\"").Replace("&#39;","'").Replace("&amp;","&");
			return Regex.Replace(text,@"\s+"," ");
		}
	}

	///<summary>Counts the real children a batch starts: wraps EhrStylesheetStarter.StartProcess in the seam, and puts the seam back.</summary>
	public class CountingStarter:IDisposable {
		public int CountStarted;

		public CountingStarter() {
			EhrStylesheetStarter.FuncStartChild=() => {
				CountStarted++;
				return EhrStylesheetStarter.StartProcess();
			};
		}

		public void Dispose() {
			EhrStylesheetStarter.FuncStartChild=null;
		}
	}

	[TestClass]
	public class CdaRenderTests {
		[TestCleanup]
		public void Cleanup() {
			EhrStylesheetStarter.FuncStartChild=null;
		}

		///<summary>Upstream's full CCD example (licensed codes replaced, marked as modified) through CDA.xsl of the pinned tag. The golden page was
		///recorded from this build's CDA.xsl, not taken from upstream's CCD-full.html, which a newer CDA.xsl made.</summary>
		[TestMethod]
		public void CcdFull_RendersAsRecorded() {
			string xml=CdaGolden.ReadFixture("CCD-full.modified.xml");
			StringAssert.Contains(xml,"MODIFIED COPY","The fixture says it is modified.");
			StringAssert.Contains(xml,"22977c4c788cccfca2de542436180e77b7c3f56d","The fixture names its source commit.");
			string html=CdaGolden.Render(EhrRenderKind.Ccd,xml);
			CdaGolden.AssertMatches("CCD-full.modified.html",html);
			//CDA.xsl's own page: the document's title and the patient, no script (useJavascript=false), nothing from the web.
			Assert.IsFalse(Regex.IsMatch(html,@"<script\b",RegexOptions.IgnoreCase),"No script element.");
			Assert.IsFalse(html.Contains("javascript:"),"No javascript: URL.");
			Assert.IsFalse(Regex.IsMatch(html,@"\bsrc\s*=\s*""(https?:|file:|\\\\)",RegexOptions.IgnoreCase),"Nothing loads from the web, a file or a share.");
		}

		///<summary>A synthetic CCR through ccr.xsl (the copy marked as modified): every date form of the fixture is readable, the non-ASCII name is
		///kept, and the page matches its golden page.</summary>
		[TestMethod]
		public void CcrWithDates_RendersReadableDates() {
			string xml=CdaGolden.ReadFixture("CCR-dates.xml");
			string html=CdaGolden.Render(EhrRenderKind.Ccr,xml);
			CdaGolden.AssertMatches("CCR-dates.html",html);
			string text=CdaGolden.VisibleText(html);
			StringAssert.Contains(text,"Zoë Ångström-Núñez","The name keeps its accents (UTF-8 both ways).");
			//The header's date, with its offset.
			StringAssert.Contains(text,"Fri Mar 14, 2025 at 09:26 AM UTC-05:00");
			//The problems' dates: a date, a time without a time zone, UTC, a year and month, a year (the default format, MMM dd, yyyy).
			StringAssert.Contains(text,"Onset: Jun 01, 2007");
			StringAssert.Contains(text,"Onset: Nov 23, 2008");
			StringAssert.Contains(text,"Onset: Feb 28, 2009");
			StringAssert.Contains(text,"Onset: Aug 01, 2010");
			StringAssert.Contains(text,"Onset: Jan 01, 2011");
			//The date of birth (a date only) and the report's date and time without a time zone.
			StringAssert.Contains(text,"Dec 31, 1980");
			StringAssert.Contains(text,"Jul 19, 2013 : 10:15 AM");
			//The derived work's footer (the AAFP's header asks a modified copy to say so).
			StringAssert.Contains(text,"derived from the one provided by the American Academy of Family Physicians");
		}

		///<summary>Several documents of one kind go through one child, so the stylesheet is compiled once; each page is the one a single render
		///makes.</summary>
		[TestMethod]
		public void Batch_UsesOneChild_ForEveryDocument() {
			string xml=CdaGolden.ReadFixture("CCD-full.modified.xml");
			string htmlSingle=CdaGolden.Render(EhrRenderKind.Ccd,xml);
			List<EhrRenderResult> listEhrRenderResults;
			using(CountingStarter countingStarter=new CountingStarter()) {
				listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,new List<string> { xml,xml,xml });
				Assert.AreEqual(1,countingStarter.CountStarted,"One child for the batch.");
			}
			Assert.AreEqual(3,listEhrRenderResults.Count);
			foreach(EhrRenderResult ehrRenderResult in listEhrRenderResults) {
				Assert.IsNull(ehrRenderResult.Reason);
				Assert.AreEqual(htmlSingle,ehrRenderResult.Html);
			}
		}

		///<summary>A batch whose second document the stylesheet refuses (CDA.xsl ends a document with a javascript: link) keeps going with the same
		///child: the refused one gets its reason, the others pages. A document that is not well-formed never reaches the child (the limits refuse
		///it first), so it takes no child either.</summary>
		[TestMethod]
		public void Batch_RefusedDocument_GetsItsReason_AndTheRestRender() {
			string xml=CdaGolden.ReadFixture("CCD-full.modified.xml");
			string xmlRefused=CdaHostileTests.MakeCcd("<paragraph>Before <linkHtml href=\"javascript:alert(1)\">the link</linkHtml> after</paragraph>");
			Assert.IsNull(EhrStylesheetStarter.GetLimitError(xmlRefused),"Within the limits: the child gets it.");
			List<EhrRenderResult> listEhrRenderResults;
			using(CountingStarter countingStarter=new CountingStarter()) {
				listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,new List<string> { xml,xmlRefused,"<ClinicalDocument><unclosed></ClinicalDocument>",xml });
				Assert.AreEqual(1,countingStarter.CountStarted,"A document the stylesheet refuses does not end the child.");
			}
			Assert.IsNotNull(listEhrRenderResults[0].Html);
			Assert.IsNull(listEhrRenderResults[1].Html);
			StringAssert.StartsWith(listEhrRenderResults[1].Reason,"The stylesheet could not show the document: ");
			StringAssert.Contains(listEhrRenderResults[1].Reason,"Javascript injection attempt detected");
			Assert.IsNull(listEhrRenderResults[2].Html);
			StringAssert.StartsWith(listEhrRenderResults[2].Reason,"The document is not well-formed XML: ");
			Assert.IsNotNull(listEhrRenderResults[3].Html);
		}

		///<summary>A frame of a kind the child does not know ends it with EXIT_BROKEN_FRAME: the reason names the exit code, the rest of the batch
		///gets a new child.</summary>
		[TestMethod]
		public void RealChild_BrokenFrame_EndsIt_AndANewChildTakesTheRest() {
			string xml=CdaGolden.ReadFixture("CCR-dates.xml");
			List<EhrRenderResult> listEhrRenderResults;
			using(CountingStarter countingStarter=new CountingStarter()) {
				listEhrRenderResults=EhrStylesheetStarter.RenderBatch((EhrRenderKind)9,new List<string> { xml,xml });
				Assert.AreEqual(2,countingStarter.CountStarted,"A child that ended is replaced for the next document.");
			}
			foreach(EhrRenderResult ehrRenderResult in listEhrRenderResults) {
				Assert.IsNull(ehrRenderResult.Html);
				StringAssert.Contains(ehrRenderResult.Reason,"exit code 0x00000004");
			}
		}

		///<summary>The program's own CCDs carry an xml-stylesheet instruction for ccd.xsl beside them; an instruction pointing anywhere else is
		///never followed either: the child runs its own stylesheet only.</summary>
		[TestMethod]
		public void StylesheetInstruction_IsNeverFollowed() {
			string xml=CdaGolden.ReadFixture("CCD-full.modified.xml").Replace("<?xml-stylesheet type=\"text/xsl\" href=\"CDA.xsl\"?>",
				"<?xml-stylesheet type=\"text/xsl\" href=\"http://192.0.2.10/evil.xsl\"?>");
			StringAssert.Contains(xml,"evil.xsl");
			string html=CdaGolden.Render(EhrRenderKind.Ccd,xml);
			Assert.IsFalse(html.Contains("evil"),"The instruction is not part of the page.");
			Assert.AreEqual(CdaGolden.Normalize(CdaGolden.Render(EhrRenderKind.Ccd,CdaGolden.ReadFixture("CCD-full.modified.xml"))),CdaGolden.Normalize(html),
				"The same page as with the original instruction.");
		}
	}
}
