// The HTML the program builds for its browser windows: text from the database is encoded (CodeBase.HtmlPage), a CCD's display stylesheet runs
// in the program with no scripts or file access (HtmlPage.TransformXsl), the preference details page (FormWebBrowserPrefs.BuildHtml), and what a
// link in a wiki page opens (FormWiki.ParseLink). Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace LibWebTests {
	[TestClass]
	public class HtmlGenerationTests {
		private const string MARKUP_NAME="Jane <script>alert(1)</script> & \"Doe\" O'Brien";

		[TestMethod]
		public void Encode_TurnsMarkupIntoText() {
			Assert.AreEqual("Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;Doe&quot; O&#39;Brien",HtmlPage.Encode(MARKUP_NAME));
			Assert.AreEqual("",HtmlPage.Encode(null));
			Assert.AreEqual("a&lt;b<br>c<br>d",HtmlPage.EncodeWithLineBreaks("a<b\r\nc\nd"));
		}

		[TestMethod]
		public void Build_IsAWholeUtf8Page_WithTheTitleAndStyleEncoded() {
			string html=HtmlPage.Build("<p>body</p>",MARKUP_NAME,"font-size:12px\"><script>x()</script>");
			StringAssert.StartsWith(html,"<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>");
			StringAssert.Contains(html,"<title>Jane &lt;script&gt;");
			StringAssert.Contains(html,"<body style=\"font-size:12px&quot;&gt;&lt;script&gt;x()&lt;/script&gt;\">");
			StringAssert.Contains(html,"<p>body</p></body></html>");
			Assert.IsFalse(html.Contains("<script>"),"Nothing the caller passed as text becomes a tag.");
			string htmlText=HtmlPage.BuildText(MARKUP_NAME+"\r\nline 2");
			StringAssert.Contains(htmlText,"<p>Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;Doe&quot; O&#39;Brien<br>line 2</p>");
			Assert.IsFalse(htmlText.Contains("<script>"));
		}

		///<summary>A CCD-like document with a patient name that holds markup, and a stylesheet like the display stylesheets that come with CCDs.</summary>
		private const string XML_CCD="<?xml version=\"1.0\"?><ClinicalDocument><recordTarget><name>"
			+"Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; Doe</name></recordTarget><title>Summary of care</title></ClinicalDocument>";
		private const string XSL_CCD="<?xml version=\"1.0\"?><xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
			+"<xsl:output method=\"html\" indent=\"no\"/>"
			+"<xsl:template match=\"/\"><html><head><title><xsl:value-of select=\"ClinicalDocument/title\"/></title></head>"
			+"<body><h1>Patient: <xsl:value-of select=\"ClinicalDocument/recordTarget/name\"/></h1></body></html></xsl:template>"
			+"</xsl:stylesheet>";

		[TestMethod]
		public void TransformXsl_GivesHtml_WithThePatientsTextEncoded() {
			string html=HtmlPage.TransformXsl(XML_CCD,XSL_CCD);
			StringAssert.Contains(html,"<title>Summary of care</title>");
			StringAssert.Contains(html,"<h1>Patient: Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; Doe</h1>");
			Assert.IsFalse(html.Contains("<script>"),"The patient's text never becomes markup.");
		}

		[TestMethod]
		public void TransformXsl_RunsNoScriptAndReadsNoOtherFile() {
			string folder=TestPaths.NewTempFolder("xsl");
			try {
				string pathSecret=Path.Combine(folder,"secret.xml");
				File.WriteAllText(pathSecret,"<secret>do not read</secret>");
				string uriSecret=new Uri(pathSecret).AbsoluteUri;
				//document() is off.
				string xslDocument=XSL_CCD.Replace("<body>","<body><xsl:value-of select=\"document('"+uriSecret+"')/secret\"/>");
				AssertThrowsOrLeavesOut(() => HtmlPage.TransformXsl(XML_CCD,xslDocument),"do not read");
				//xsl:include and xsl:import are not resolved.
				string pathInclude=Path.Combine(folder,"include.xsl");
				File.WriteAllText(pathInclude,"<?xml version=\"1.0\"?><xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">"
					+"<xsl:template name=\"t\">included text</xsl:template></xsl:stylesheet>");
				string xslInclude=XSL_CCD.Replace("<xsl:output","<xsl:include href=\""+new Uri(pathInclude).AbsoluteUri+"\"/><xsl:output");
				AssertThrowsOrLeavesOut(() => HtmlPage.TransformXsl(XML_CCD,xslInclude),"included text");
				//An external entity in the document is not loaded.
				string xmlEntity="<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument [<!ENTITY s SYSTEM \""+uriSecret+"\">]>"
					+"<ClinicalDocument><recordTarget><name>&s;</name></recordTarget><title>t</title></ClinicalDocument>";
				AssertThrowsOrLeavesOut(() => HtmlPage.TransformXsl(xmlEntity,XSL_CCD),"do not read");
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			//Embedded script in the stylesheet is refused (XsltSettings.Default).
			string xslScript=XSL_CCD.Replace("<xsl:output","<msxsl:script xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\" language=\"C#\" implements-prefix=\"u\">"
				+"public string f(){return \"ran\";}</msxsl:script><xsl:output").Replace("<xsl:stylesheet ","<xsl:stylesheet xmlns:u=\"urn:u\" ");
			AssertThrowsOrLeavesOut(() => HtmlPage.TransformXsl(XML_CCD,xslScript.Replace("Patient: ","Patient: <xsl:value-of select=\"u:f()\"/>")),"ran");
			//Entities may not grow without limit (an internal subset is read, as some display stylesheets need).
			string xmlBomb="<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY a \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\">"
				+"<!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\"><!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\">"
				+"<!ENTITY e \"&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;\"><!ENTITY f \"&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;\">]>"
				+"<d>&f;</d>";
			try {
				HtmlPage.TransformXsl(xmlBomb,XSL_CCD);
				Assert.Fail("The entity expansion limit applies.");
			}
			catch(XmlException) {
			}
		}

		private static void AssertThrowsOrLeavesOut(Func<string> funcTransform,string textForbidden) {
			string html;
			try {
				html=funcTransform();
			}
			catch(Exception ex) when(ex is XsltException || ex is XmlException || ex is System.Security.SecurityException || ex is NotSupportedException) {
				return;
			}
			Assert.IsFalse(html.Contains(textForbidden),"The transform reached outside the document: "+html);
		}

		[TestMethod]
		public void PrefInfoPage_EncodesTheLabelAndName_AndKeepsTheDetailsMarkup() {
			string html=FormWebBrowserPrefs.BuildHtml(MARKUP_NAME,"See <a href='https://www.example.com/manual.html' target='_blank'>the manual</a>.<br/>",
				"Pref<Name>",15);
			StringAssert.Contains(html,"<p><b>Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;Doe&quot; O&#39;Brien</b></p>");
			StringAssert.Contains(html,"<a href='https://www.example.com/manual.html' target='_blank'>the manual</a>.<br/>");
			StringAssert.Contains(html,"Internal PrefName: Pref&lt;Name&gt;</p>");
			StringAssert.Contains(html,"<body style=\"font-family:sans-serif; font-size:15px\">");
			StringAssert.EndsWith(html,"</body></html>");
			Assert.IsFalse(FormWebBrowserPrefs.BuildHtml("Label","Details","",11).Contains("Internal PrefName"),"No name, no name line.");
		}

		[TestMethod]
		public void WikiLinks_PageFileFolderCloudAndWeb() {
			Assert.AreEqual(EnumWikiLink.WikiPage,FormWiki.ParseLink("wiki:Office%20Policies",out string target));
			Assert.AreEqual("Office Policies",target);
			Assert.AreEqual(EnumWikiLink.WikiPage,FormWiki.ParseLink("wiki:Two%C2%A0%C2%A0Spaces",out target));
			Assert.AreEqual("Two  Spaces",target,"MarkupEdit's no-break spaces become spaces again.");
			Assert.AreEqual(EnumWikiLink.File,FormWiki.ParseLink("wikifile:C:/Office/Forms/intake.pdf",out target));
			Assert.AreEqual(@"C:\Office\Forms\intake.pdf",target);
			Assert.AreEqual(EnumWikiLink.File,FormWiki.ParseLink(@"wikifile:\\192.0.2.10\share\a%20b.txt",out target));
			Assert.AreEqual(@"\\192.0.2.10\share\a b.txt",target);
			Assert.AreEqual(EnumWikiLink.Folder,FormWiki.ParseLink("folder:C:/Office/Forms",out target));
			Assert.AreEqual(@"C:\Office\Forms",target);
			Assert.AreEqual(EnumWikiLink.FileCloud,FormWiki.ParseLink("wikifilecloud:Wiki/notes.txt",out target));
			Assert.AreEqual("Wiki/notes.txt",target);
			Assert.AreEqual(EnumWikiLink.FolderCloud,FormWiki.ParseLink("foldercloud:Wiki/Forms",out target));
			Assert.AreEqual("Wiki/Forms",target);
			Assert.AreEqual(EnumWikiLink.Web,FormWiki.ParseLink("https://www.example.com/",out target));
			Assert.AreEqual("https://www.example.com/",target);
			Assert.AreEqual(EnumWikiLink.Web,FormWiki.ParseLink("mailto:office@example.com",out target));
			//The wiki never shows another page, a local file or a remote site itself, and never hands anything else to the shell.
			foreach(string uri in new[] { "file:///C:/Windows/notepad.exe","javascript:alert(1)","about:other","ms-settings:","www.example.com",null,"" }) {
				Assert.AreEqual(EnumWikiLink.None,FormWiki.ParseLink(uri,out target),uri??"null");
			}
		}

		[TestMethod]
		public void WikiLinks_AreTheOnesMarkupEditWrites() {
			string markupEdit=TestPaths.ReadSource("OpenPmsDentalBusiness","Db Multi Table","MarkupEdit.cs");
			foreach(string scheme in new[] { "\"wiki:\"","<a href=\\\"wikifile:","<a href=\\\"folder:","<a href=\\\"wikifilecloud:","<a href=\\\"foldercloud:" }) {
				StringAssert.Contains(markupEdit,scheme);
			}
		}
	}
}
