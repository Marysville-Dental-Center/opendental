// The HTML the program builds for its browser windows: text from the database is encoded (CodeBase.HtmlPage), a CCD shows as encoded XML and no
// stylesheet ever runs in the program (HtmlPage.BuildXml), the CCD window keeps the document in memory (no file while it is shown) and only the
// files an older version left are deleted, the preference details page (FormWebBrowserPrefs.BuildHtml), and what a link in a wiki page opens
// (FormWiki.ParseLink). Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
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

		///<summary>A CCD-like document with a patient name that holds markup, and the stylesheet instruction CCDs carry.</summary>
		private const string XML_CCD="<?xml version=\"1.0\"?><?xml-stylesheet type=\"text/xsl\" href=\"ccd.xsl\"?><ClinicalDocument><recordTarget><name>"
			+"Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; Doe</name></recordTarget><title>Summary of care</title></ClinicalDocument>";

		[TestMethod]
		public void BuildXml_ShowsTheDocumentAsIndentedText_WithEverythingEncoded() {
			string html=HtmlPage.BuildXml(XML_CCD,MARKUP_NAME+"\r\nline 2","Summary <of> care");
			StringAssert.StartsWith(html,"<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>Summary &lt;of&gt; care</title>");
			StringAssert.Contains(html,"<p>Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;Doe&quot; O&#39;Brien<br>line 2</p>");
			//One element per line, two spaces per level; the document's markup is text of the page.
			StringAssert.Contains(html,"<pre style=\"white-space:pre-wrap;word-wrap:break-word\">&lt;?xml-stylesheet type=&quot;text/xsl&quot; "
				+"href=&quot;ccd.xsl&quot;?&gt;\r\n&lt;ClinicalDocument&gt;\r\n  &lt;recordTarget&gt;\r\n    &lt;name&gt;");
			//The patient's text stays escaped in the XML and is encoded again for the page.
			StringAssert.Contains(html,"&lt;name&gt;Jane &amp;lt;script&amp;gt;alert(1)&amp;lt;/script&amp;gt; &amp;amp; Doe&lt;/name&gt;");
			StringAssert.Contains(html,"\r\n  &lt;title&gt;Summary of care&lt;/title&gt;\r\n&lt;/ClinicalDocument&gt;</pre></body></html>");
			Assert.IsFalse(html.Contains("<script>"),"The patient's text never becomes markup.");
			Assert.IsFalse(html.Contains("<ClinicalDocument") || html.Contains("<name>") || html.Contains("<?xml"),"The document's markup never becomes markup.");
			Assert.IsFalse(html.Contains("&lt;?xml version"),"The XML declaration is left out.");
			StringAssert.Contains(HtmlPage.BuildXml(null),"<pre style=\"white-space:pre-wrap;word-wrap:break-word\"></pre>");
			Assert.IsFalse(HtmlPage.BuildXml(XML_CCD).Contains("<p>"),"No note, no note line.");
		}

		[TestMethod]
		public void BuildXml_ReadsNoOtherFile_ExpandsNoEntity_AndShowsBrokenXmlAsItCame() {
			string folder=TestPaths.NewTempFolder("xml");
			try {
				string pathSecret=Path.Combine(folder,"secret.xml");
				File.WriteAllText(pathSecret,"<secret>do not read</secret>");
				string uriSecret=new Uri(pathSecret).AbsoluteUri;
				//An external entity or DTD is never loaded: the document type is skipped, and a document that uses one of its entities shows as it came.
				string xmlEntity="<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument SYSTEM \""+uriSecret+"\" [<!ENTITY s SYSTEM \""+uriSecret+"\">]>"
					+"<ClinicalDocument><recordTarget><name>&s;</name></recordTarget></ClinicalDocument>";
				string html=HtmlPage.BuildXml(xmlEntity);
				Assert.IsFalse(html.Contains("do not read"),html);
				StringAssert.Contains(html,"&lt;name&gt;&amp;s;&lt;/name&gt;");
				string xmlDtd="<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument SYSTEM \""+uriSecret+"\"><ClinicalDocument><title>t</title></ClinicalDocument>";
				html=HtmlPage.BuildXml(xmlDtd);
				Assert.IsFalse(html.Contains("do not read"),html);
				StringAssert.Contains(html,"&lt;ClinicalDocument&gt;\r\n  &lt;title&gt;t&lt;/title&gt;","A document type it does not need is skipped.");
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			//Entities never expand, so a few hundred bytes cannot become megabytes.
			string xmlBomb="<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY a \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\">"
				+"<!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\"><!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\">"
				+"<!ENTITY e \"&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;\"><!ENTITY f \"&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;\">]>"
				+"<d>&f;</d>";
			Assert.IsTrue(HtmlPage.BuildXml(xmlBomb).Length<xmlBomb.Length*2+500,"No entity expanded.");
			//XML that is not well-formed shows as it came, encoded.
			StringAssert.Contains(HtmlPage.BuildXml("<a><b>x</a>"),"&lt;a&gt;&lt;b&gt;x&lt;/a&gt;</pre>");
			Assert.AreEqual("",HtmlPage.IndentXml(null));
		}

		///<summary>Assemblies and folders that may run XSLT, with the reason. Neither is reached by anything that comes from outside the office.</summary>
		private static readonly string[] _arrayFoldersXsltAllowed={
			"RdlEngine",//the report engine (third-party, LGPL) applies the stylesheet a report definition names (DataTransform), from the report's folder
			"DocumentationBuilder",//the developer tool that writes the preference documentation with the repository's own stylesheet; not shipped
		};

		///<summary>The program runs no XSLT stylesheet in its own process, so none that came with an email (a CCD's .xsl attachment): a stylesheet
		///that recurses without end overflows the stack, and a StackOverflowException ends the process whatever catches it (checked with a 32-bit
		///.NET Framework 4.8 process during the review); one that loops never returns. FormEhrSummaryOfCare.DisplayCCD never reads the attachment.</summary>
		[TestMethod]
		public void NoStylesheet_FromAnEmail_RunsInTheProgram() {
			Regex regexXslt=new Regex(@"\b(XslCompiledTransform|XslTransform|TransformXsl)\b");
			List<string> listFiles=TestPaths.GetShippedFiles(".cs");
			Assert.IsTrue(listFiles.Any(x => x.EndsWith(@"OpenPmsDental\Forms\FormEhrSummaryOfCare.cs",StringComparison.OrdinalIgnoreCase)),"The scan covers the program.");
			List<string> listViolations=new List<string>();
			int countAllowed=0;
			foreach(string path in listFiles) {
				string relative=TestPaths.Relative(path);
				string[] arrayLines=File.ReadAllLines(path);
				for(int i=0;i<arrayLines.Length;i++) {
					if(arrayLines[i].TrimStart().StartsWith("//") || !regexXslt.IsMatch(arrayLines[i])) {
						continue;
					}
					if(_arrayFoldersXsltAllowed.Any(x => relative.StartsWith(x+@"\",StringComparison.OrdinalIgnoreCase))) {
						countAllowed++;
						continue;
					}
					listViolations.Add(relative+"("+(i+1)+"): "+arrayLines[i].Trim());
				}
			}
			Assert.AreEqual(0,listViolations.Count,"XSLT in the program's process:\r\n"+string.Join("\r\n",listViolations));
			Assert.IsTrue(countAllowed>0,"The scan sees XSLT where it is allowed, so it would see it anywhere.");
			//The built assemblies the program is made of reference no XSLT processor either (read with Mono.Cecil, nothing is loaded).
			foreach(string fileName in new[] { "OpenPmsDental.exe","CodeBase.dll","OpenPmsDentalBusiness.dll","WpfControlsOD.dll","DataConnectionBase.dll" }) {
				string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,fileName);
				Assert.IsTrue(File.Exists(path),fileName+" is next to the tests (a project reference).");
				using(ModuleDefinition moduleDefinition=ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(path)))) {
					List<string> listXslt=moduleDefinition.GetTypeReferences()
						.Where(x => x.Namespace=="System.Xml.Xsl" && (x.Name=="XslCompiledTransform" || x.Name=="XslTransform"))
						.Select(x => x.FullName).ToList();
					Assert.AreEqual(0,listXslt.Count,fileName+" uses "+string.Join(", ",listXslt));
				}
			}
			Assert.IsNull(typeof(HtmlPage).GetMethod("TransformXsl"),"HtmlPage runs no stylesheet.");
			//The attachment's stylesheet is never read: the CCD shows as XML in its window, with a line that says so.
			string source=TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryOfCare.cs");
			Assert.IsFalse(Regex.IsMatch(source,@"\bRead\w*\(\s*strAlterateFilPathXslCCD\b"),"DisplayCCD reads the email's stylesheet.");
			StringAssert.Contains(source,"The stylesheet that came with this document was not run, so the document is shown as XML.");
			StringAssert.Contains(TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryCcdEdit.cs"),"html=HtmlPage.BuildXml(_xmlCcd,_note);");
		}

		[TestMethod]
		public void CcdView_KeepsTheDocumentInMemory_AndWritesNoFile() {
			//The window gets the CCD's text, not a file: no path field, and its constructor takes the XML.
			Type typeForm=typeof(FormEhrSummaryCcdEdit);
			BindingFlags bindingFlags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
			Assert.IsNull(typeForm.GetField("StrXmlFilePath",bindingFlags),"FormEhrSummaryCcdEdit keeps no file path.");
			Assert.AreEqual(typeof(string),typeForm.GetField("_xmlCcd",bindingFlags)?.FieldType,"FormEhrSummaryCcdEdit keeps the text.");
			ConstructorInfo[] arrayConstructors=typeForm.GetConstructors();
			Assert.AreEqual(1,arrayConstructors.Length);
			CollectionAssert.AreEqual(new[] { "xmlCcd","patCur","note" },arrayConstructors[0].GetParameters().Select(x => x.Name).ToArray());
			//Shown, reconciled and shown as raw XML from that text: the window reads no file and writes none.
			string sourceForm=TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryCcdEdit.cs");
			foreach(string text in new[] { "File.","Path.","FileStream","StreamReader","StreamWriter","GetTempFolderPath","using System.IO;" }) {
				Assert.IsFalse(sourceForm.Contains(text),"FormEhrSummaryCcdEdit.cs: "+text);
			}
			Assert.AreEqual(3,Regex.Matches(sourceForm,Regex.Escape("xmlDocCcd.LoadXml(_xmlCcd);")).Count,"The three reconcile buttons read the text.");
			StringAssert.Contains(sourceForm,"string strCcd=_xmlCcd;");
			//DisplayCCD hands the window the document and writes it nowhere; it deletes only what an older version left.
			string source=TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryOfCare.cs");
			int indexStart=source.IndexOf("public static bool DisplayCCD(string strXmlCCD,Patient patCur,string strAlterateFilPathXslCCD)");
			int indexEnd=source.IndexOf("public static int DeleteOldCcdFiles(",indexStart);
			Assert.IsTrue(indexStart>=0 && indexEnd>indexStart,"DisplayCCD and DeleteOldCcdFiles are found.");
			string sourceDisplay=source.Substring(indexStart,indexEnd-indexStart);
			foreach(string text in new[] { "File.","WriteAll","GetTempFolderPath","GetRandomTempFile","FileStream","StreamWriter" }) {
				Assert.IsFalse(sourceDisplay.Contains(text),"DisplayCCD: "+text);
			}
			StringAssert.Contains(sourceDisplay,"new FormEhrSummaryCcdEdit(doc.InnerXml,patCur,note)");
			StringAssert.Contains(sourceDisplay,"DeleteOldCcdFiles(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName),DateTime.Now.AddHours(-1));");
		}

		[TestMethod]
		public void DeleteOldCcdFiles_OnlyTheFilesAnOlderVersionLeft() {
			string folder=TestPaths.NewTempFolder("oldccd");
			try {
				string[] arrayOld={ "ccd.xml","ccd.xsl","ccr.xml","CCR.XSL" };
				string[] arrayKept={ "ccd.xml.bak","report.xml","ccd2.xml","ABC12320261102093000.html" };
				foreach(string name in arrayOld.Concat(arrayKept)) {
					File.WriteAllText(Path.Combine(folder,name),"<ClinicalDocument>Jane Doe</ClinicalDocument>");
				}
				Directory.CreateDirectory(Path.Combine(folder,"sub"));
				File.WriteAllText(Path.Combine(folder,"sub","ccd.xml"),"kept");
				//Written two hours ago, as an older version that ended with the window open left them.
				DateTime dateTimeOld=DateTime.Now.AddHours(-2);
				foreach(string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories)) {
					File.SetLastWriteTime(path,dateTimeOld);
				}
				//One in use stays, and nothing throws.
				string pathInUse=Path.Combine(folder,"ccr.xml");
				using(FileStream fileStream=new FileStream(pathInUse,FileMode.Open,FileAccess.Read,FileShare.None)) {
					Assert.AreEqual(3,FormEhrSummaryOfCare.DeleteOldCcdFiles(folder,DateTime.Now.AddHours(-1)));
					Assert.IsTrue(File.Exists(pathInUse));
				}
				Assert.AreEqual(1,FormEhrSummaryOfCare.DeleteOldCcdFiles(folder,DateTime.Now.AddHours(-1)),"Once it is free, it goes too.");
				foreach(string name in arrayOld) {
					Assert.IsFalse(File.Exists(Path.Combine(folder,name)),name);
				}
				foreach(string name in arrayKept) {
					Assert.IsTrue(File.Exists(Path.Combine(folder,name)),name);
				}
				Assert.IsTrue(File.Exists(Path.Combine(folder,"sub","ccd.xml")),"Only the folder itself, not its sub folders.");
				//A file an older version running now has just written stays.
				string pathRecent=Path.Combine(folder,"ccd.xml");
				File.WriteAllText(pathRecent,"<ClinicalDocument/>");
				Assert.AreEqual(0,FormEhrSummaryOfCare.DeleteOldCcdFiles(folder,DateTime.Now.AddHours(-1)));
				Assert.IsTrue(File.Exists(pathRecent));
				//A missing folder is not created; no folder, nothing.
				string folderMissing=Path.Combine(folder,"missing");
				Assert.AreEqual(0,FormEhrSummaryOfCare.DeleteOldCcdFiles(folderMissing,DateTime.Now));
				Assert.IsFalse(Directory.Exists(folderMissing));
				Assert.AreEqual(0,FormEhrSummaryOfCare.DeleteOldCcdFiles(null,DateTime.Now));
				Assert.AreEqual(0,FormEhrSummaryOfCare.DeleteOldCcdFiles("",DateTime.Now));
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
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
