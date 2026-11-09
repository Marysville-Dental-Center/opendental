// Hostile documents and children for the stylesheet child (package EHR-3 of docs/specs/ehr-rebuild-plan.md, docs/port-notes/EHR-3.md): deep
// nesting, a DTD and its entities, oversize documents, scripts, javascript: links and file or share paths in a CCD's narrative and in a CCR's
// description texts, the resolver's closed list, and, through the starter's seam, children that stall or end with a stack overflow. The pages
// come from a real OpenPmsDental.exe /ehr-render started by EhrStylesheetStarter; the seam's children are in-memory fakes. Synthetic data only;
// the addresses are of the documentation range 192.0.2.x and nothing is fetched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class CdaHostileTests {
		[TestCleanup]
		public void Cleanup() {
			EhrStylesheetStarter.FuncStartChild=null;
		}

		#region Documents
		///<summary>A small CDA document whose one section's narrative is body (CDA narrative markup) and whose entries are entries.</summary>
		internal static string MakeCcd(string body,string entries="") {
			return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n"
				+"<ClinicalDocument xmlns=\"urn:hl7-org:v3\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
				+"<realmCode code=\"US\"/><typeId root=\"2.16.840.1.113883.1.3\" extension=\"POCD_HD000040\"/><id root=\"2.25.1001\"/>"
				+"<code code=\"TEST0001\" codeSystem=\"2.16.840.1.113883.6.1\" displayName=\"TEST document\"/><title>Hostile test document</title>"
				+"<effectiveTime value=\"20261102093000-0500\"/><confidentialityCode code=\"N\" codeSystem=\"2.16.840.1.113883.5.25\"/>"
				+"<languageCode code=\"en-US\"/>"
				+"<recordTarget><patientRole><id root=\"2.25.1002\" extension=\"1\"/><patient><name><given>Test</given><family>Patient</family></name>"
				+"<administrativeGenderCode code=\"F\" codeSystem=\"2.16.840.1.113883.5.1\"/><birthTime value=\"19800101\"/></patient></patientRole>"
				+"</recordTarget>"
				+"<author><time value=\"20261102\"/><assignedAuthor><id root=\"2.25.1003\"/><assignedPerson><name><given>Test</given><family>Author</family>"
				+"</name></assignedPerson></assignedAuthor></author>"
				+"<custodian><assignedCustodian><representedCustodianOrganization><id root=\"2.25.1004\"/><name>Example Test Clinic</name>"
				+"</representedCustodianOrganization></assignedCustodian></custodian>"
				+"<component><structuredBody><component><section><code code=\"TEST0002\" codeSystem=\"2.16.840.1.113883.6.1\"/><title>Narrative</title>"
				+"<text>"+body+"</text>"+entries+"</section></component></structuredBody></component></ClinicalDocument>";
		}

		///<summary>A small CCR whose social history, two result rows and plan of care (order and goal) carry the given Description/Text values, and
		///with one References/Reference per element of arrayLocationsXml, whose Locations/Location/Description/Text is that XML text as it is
		///(character references included).</summary>
		private static string MakeCcr(string textSocial,string textResult1,string textResult2,string textOrder,string textGoal,params string[] arrayLocationsXml) {
			string references="";
			if(arrayLocationsXml.Length>0) {
				references="<References>";
				for(int i=0;i<arrayLocationsXml.Length;i++) {
					references+="<Reference><ReferenceObjectID>RF000"+(i+1)+"</ReferenceObjectID><Type><Text>Test reference</Text></Type>"
						+"<Description><Text>Reference "+(i+1)+"</Text></Description>"
						+"<Locations><Location><Description><Text>"+arrayLocationsXml[i]+"</Text></Description></Location></Locations>"
						+"<Source><Actor><ActorID>AA0002</ActorID></Actor></Source></Reference>";
				}
				references+="</References>";
			}
			return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<ContinuityOfCareRecord xmlns=\"urn:astm-org:CCR\">"
				+"<CCRDocumentObjectID>TEST-CCR-0002</CCRDocumentObjectID><Language><Text>English</Text></Language><Version>V1.0</Version>"
				+"<DateTime><ExactDateTime>2025-03-14T09:26:53Z</ExactDateTime></DateTime><Patient><ActorID>AA0001</ActorID></Patient>"
				+"<From><ActorLink><ActorID>AA0002</ActorID></ActorLink></From><Body>"
				+"<SocialHistory><SocialHistoryElement><CCRDataObjectID>SH0001</CCRDataObjectID><Type><Text>Test</Text></Type>"
				+"<Description><Text>"+Escape(textSocial)+"</Text></Description></SocialHistoryElement></SocialHistory>"
				+"<Results><Result><CCRDataObjectID>RE0001</CCRDataObjectID><Description><Text>Test report</Text></Description>"
				+"<Test><CCRDataObjectID>RE0002</CCRDataObjectID><Description><Text>One</Text></Description><TestResult><Description><Text>"
				+Escape(textResult1)+"</Text></Description></TestResult></Test>"
				+"<Test><CCRDataObjectID>RE0003</CCRDataObjectID><Description><Text>Two</Text></Description><TestResult><Description><Text>"
				+Escape(textResult2)+"</Text></Description></TestResult></Test></Result></Results>"
				+"<PlanOfCare><Plan><CCRDataObjectID>PL0001</CCRDataObjectID><Type><Text>Treatment Recommendation</Text></Type>"
				+"<Description><Text>Test plan</Text></Description><OrderRequest><Description><Text>"+Escape(textOrder)+"</Text></Description>"
				+"<Goals><Goal><Description><Text>"+Escape(textGoal)+"</Text></Description></Goal></Goals></OrderRequest></Plan></PlanOfCare>"
				+"</Body><Actors><Actor><ActorObjectID>AA0001</ActorObjectID><Person><Name><CurrentName><Given>Test</Given><Family>Patient</Family>"
				+"</CurrentName></Name></Person></Actor><Actor><ActorObjectID>AA0002</ActorObjectID><Organization><Name>Example Test Clinic</Name>"
				+"</Organization></Actor></Actors>"+references+"</ContinuityOfCareRecord>";
		}

		private static string Escape(string text) {
			return text.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
		}

		///<summary>The URL attributes a browser reads.</summary>
		private static readonly string[] _arrayUrlAttributes={ "href","src","data","action","formaction","background","poster","xlink:href","srcset",
			"lowsrc","dynsrc","codebase","cite","longdesc","manifest" };

		///<summary>The checks every hostile page must pass, on its real tags (text the page shows, escaped, is not markup): no script element, no
		///event handler attribute, no script URL (javascript:, vbscript:, data:text/html) in any URL attribute, no CSS url() or expression(), and
		///nothing that loads by itself from a file, a share or the web: only a link (a href) may point elsewhere, and only to a place in the page
		///(#), the web (http://, https://) or an e-mail address (mailto:), never to a file or a share; the CCD window opens only http, https and
		///mailto links, in the default browser (FormEhrSummaryCcdEdit, WebView2Policy.OpenInSystemBrowser).</summary>
		private static void AssertHarmless(string html) {
			Assert.IsFalse(Regex.IsMatch(html,@"<script\b",RegexOptions.IgnoreCase),"A script element: "+Excerpt(html,"<script"));
			List<Match> listTags=Regex.Matches(html,@"<([a-zA-Z][\w:-]*)\b[^<>]*>").Cast<Match>().ToList();
			Assert.IsTrue(listTags.Count>10,"The page has its own markup.");
			foreach(Match matchTag in listTags) {
				string tag=matchTag.Value;
				string tagName=matchTag.Groups[1].Value.ToLowerInvariant();
				Assert.IsFalse(Regex.IsMatch(tag,@"\son\w+\s*=",RegexOptions.IgnoreCase),"An event handler attribute: "+tag);
				foreach(Match matchAttribute in Regex.Matches(tag,@"\s([\w:-]+)\s*=\s*(""[^""]*""|'[^']*')")) {
					string name=matchAttribute.Groups[1].Value.ToLowerInvariant();
					string value=System.Net.WebUtility.HtmlDecode(matchAttribute.Groups[2].Value.Trim('"','\''));
					//As a browser reads a URL: tabs, line breaks and other control characters dropped.
					string valueRead=new string(value.Where(x => x>' ').ToArray()).ToLowerInvariant();
					if(name=="style") {
						Assert.IsFalse(Regex.IsMatch(valueRead,@"url\(|expression\(|javascript:"),"CSS that loads or runs: "+tag);
					}
					if(!_arrayUrlAttributes.Contains(name)) {
						continue;
					}
					Assert.IsFalse(Regex.IsMatch(valueRead,@"(javascript|vbscript):|^data:text/html"),"A script URL: "+tag);
					if(tagName=="a" && name=="href") {
						//A link loads nothing by itself, and goes only to the page, the web or an e-mail address.
						Assert.IsTrue(Regex.IsMatch(valueRead,@"^(#|https?://|mailto:)"),"A link to a file, a share or another scheme: "+tag);
						continue;
					}
					Assert.IsFalse(valueRead.StartsWith(@"\\") || valueRead.StartsWith("//") || valueRead.StartsWith("/\\"),"A share or a network path: "+tag);
					Assert.IsFalse(Regex.IsMatch(valueRead,@"^(file|https?|ftp|smb):"),"A file or web address: "+tag);
					Assert.IsFalse(valueRead.Contains("192.0.2."),"An address of the hostile document: "+tag);
				}
			}
		}

		private static string Excerpt(string html,string text) {
			int index=html.IndexOf(text,StringComparison.OrdinalIgnoreCase);
			return index<0 ? "" : html.Substring(Math.Max(0,index-100),Math.Min(300,html.Length-Math.Max(0,index-100)));
		}
		#endregion Documents

		#region Limits
		///<summary>100,000 nested elements are refused before any child starts; the window then shows the text (FormEhrSummaryCcdEdit.BuildPage)
		///with the reason, and the program stays up.</summary>
		[TestMethod]
		public void DeepNesting_RefusedBeforeAnyChild() {
			string xml="<ClinicalDocument xmlns=\"urn:hl7-org:v3\">"+string.Concat(Enumerable.Repeat("<a>",100000))+string.Concat(Enumerable.Repeat("</a>",100000))
				+"</ClinicalDocument>";
			StringAssert.Contains(EhrStylesheetStarter.GetLimitError(xml),"nested more than 200 levels deep");
			using(CountingStarter countingStarter=new CountingStarter()) {
				Assert.IsNull(EhrStylesheetStarter.Render(EhrRenderKind.Ccd,xml,out string reason));
				StringAssert.Contains(reason,"nested more than 200 levels deep");
				Assert.AreEqual(0,countingStarter.CountStarted,"No child was started.");
			}
			string page=FormEhrSummaryCcdEdit.BuildPage(xml,null,"The document is nested more than 200 levels deep.");
			StringAssert.Contains(page,"The document is nested more than 200 levels deep.");
			StringAssert.Contains(page,"&lt;a&gt;&lt;a&gt;&lt;a&gt;");
			//200 levels are within the limits (the root is level 1); 201 are not.
			Assert.IsNull(EhrStylesheetStarter.GetLimitError(string.Concat(Enumerable.Repeat("<a>",200))+string.Concat(Enumerable.Repeat("</a>",200))));
			Assert.IsNotNull(EhrStylesheetStarter.GetLimitError(string.Concat(Enumerable.Repeat("<a>",201))+string.Concat(Enumerable.Repeat("</a>",201))));
		}

		///<summary>A document of 33 MB is refused before any child starts; 32 MB is the limit, counted in UTF-8 bytes.</summary>
		[TestMethod]
		public void Oversize_RefusedBeforeAnyChild() {
			string xml="<ClinicalDocument>"+new string('a',33*1024*1024)+"</ClinicalDocument>";
			using(CountingStarter countingStarter=new CountingStarter()) {
				Assert.IsNull(EhrStylesheetStarter.Render(EhrRenderKind.Ccd,xml,out string reason));
				Assert.AreEqual("The document is larger than 32 MB.",reason);
				Assert.AreEqual(0,countingStarter.CountStarted,"No child was started.");
			}
			//Non-ASCII text counts in bytes: 17 million two-byte characters are over the limit although fewer than 32 M characters.
			Assert.AreEqual("The document is larger than 32 MB.",EhrStylesheetStarter.GetLimitError("<a>"+new string('é',17*1024*1024)+"</a>"));
			Assert.AreEqual("The document is empty.",EhrStylesheetStarter.GetLimitError(""));
			Assert.AreEqual("The document is empty.",EhrStylesheetStarter.GetLimitError(null));
		}

		///<summary>A document type declaration is refused before any child starts: an entity expansion (a "billion laughs") and external entities
		///pointing to a file, a share or the web. The child refuses a DTD as well (DtdProcessing.Prohibit).</summary>
		[TestMethod]
		public void Dtd_EntityExpansionAndExternalEntities_Refused() {
			string xmlBomb="<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument [<!ENTITY a \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\">"
				+"<!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\"><!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\">"
				+"<!ENTITY e \"&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;\"><!ENTITY f \"&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;\">]>"
				+"<ClinicalDocument xmlns=\"urn:hl7-org:v3\"><title>&f;</title></ClinicalDocument>";
			List<string> listHostile=new List<string> {
				xmlBomb,
				"<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument SYSTEM \"file:///C:/Windows/win.ini\"><ClinicalDocument xmlns=\"urn:hl7-org:v3\"/>",
				"<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument [<!ENTITY s SYSTEM \"\\\\192.0.2.10\\share\\secret.xml\">]>"
					+"<ClinicalDocument xmlns=\"urn:hl7-org:v3\"><title>&s;</title></ClinicalDocument>",
				"<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument [<!ENTITY s SYSTEM \"http://192.0.2.10/secret.xml\">]>"
					+"<ClinicalDocument xmlns=\"urn:hl7-org:v3\"><title>&s;</title></ClinicalDocument>",
			};
			using(CountingStarter countingStarter=new CountingStarter()) {
				List<EhrRenderResult> listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,listHostile);
				Assert.AreEqual(0,countingStarter.CountStarted,"No child was started.");
				foreach(EhrRenderResult ehrRenderResult in listEhrRenderResults) {
					Assert.IsNull(ehrRenderResult.Html);
					Assert.AreEqual("The document has a document type declaration (DTD), which is not allowed.",ehrRenderResult.Reason);
				}
			}
			//Not well-formed for another reason: refused before any child as well (the limits fail closed), with the reader's reason.
			string xmlMalformed="<ClinicalDocument><a></ClinicalDocument>";
			StringAssert.StartsWith(EhrStylesheetStarter.GetLimitError(xmlMalformed),"The document is not well-formed XML: ");
			using(CountingStarter countingStarter=new CountingStarter()) {
				Assert.IsNull(EhrStylesheetStarter.Render(EhrRenderKind.Ccd,xmlMalformed,out string reason));
				StringAssert.StartsWith(reason,"The document is not well-formed XML: ");
				Assert.AreEqual(0,countingStarter.CountStarted,"No child was started.");
			}
		}

		///<summary>A character reference XML does not allow (&amp;#0;) before 100,000 nested elements. The reader of the limits stops at the
		///reference, before it has counted the depth, so the document is refused as not well-formed (the limits fail closed) before any child
		///starts, and the window shows its text, with no reconcile buttons (DisplayCCD gives it no patient). XmlDocument.LoadXml's legacy reader
		///accepts the reference: had the limits let the document through, the reconcile code (EhrCCD.GetList...) would have walked the 100,000 levels
		///recursively in the program's process, and the stack overflow would have ended the program. The program's own XmlDocument is read with the
		///strict reader (EhrStylesheetStarter.LoadXmlDocument), which refuses the reference too.</summary>
		[TestMethod]
		public void InvalidCharacterReference_BeforeDeepNesting_RefusedBeforeAnyChild() {
			string xml="<ClinicalDocument xmlns=\"urn:hl7-org:v3\">&#0;"+string.Concat(Enumerable.Repeat("<a>",100000))
				+string.Concat(Enumerable.Repeat("</a>",100000))+"</ClinicalDocument>";
			string reasonLimits=EhrStylesheetStarter.GetLimitError(xml);
			StringAssert.StartsWith(reasonLimits,"The document is not well-formed XML: ");
			using(CountingStarter countingStarter=new CountingStarter()) {
				Assert.IsNull(EhrStylesheetStarter.Render(EhrRenderKind.Ccd,xml,out string reason));
				Assert.AreEqual(reasonLimits,reason);
				List<EhrRenderResult> listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,new List<string> { xml });
				Assert.AreEqual(reasonLimits,listEhrRenderResults[0].Reason);
				Assert.AreEqual(0,countingStarter.CountStarted,"No child was started.");
			}
			//The window's view of a document over the limits: its text, plain and encoded, with the reason; not the indented XML view.
			string page=FormEhrSummaryCcdEdit.BuildPage(xml,null,reasonLimits);
			StringAssert.Contains(page,"<pre style=\"white-space:pre-wrap;word-wrap:break-word\">&lt;ClinicalDocument xmlns=&quot;urn:hl7-org:v3&quot;&gt;&amp;#0;"
				+"&lt;a&gt;&lt;a&gt;&lt;a&gt;");
			StringAssert.Contains(page,HtmlPage.EncodeWithLineBreaks(reasonLimits));
			//The strict reader refuses the reference; LoadXml's legacy reader accepts it (shown on a small document).
			string xmlSmall="<ClinicalDocument xmlns=\"urn:hl7-org:v3\">&#0;<a/></ClinicalDocument>";
			StringAssert.StartsWith(EhrStylesheetStarter.GetLimitError(xmlSmall),"The document is not well-formed XML: ");
			Expect.Throws<XmlException>(() => EhrStylesheetStarter.LoadXmlDocument(xmlSmall));
			XmlDocument xmlDocumentLegacy=new XmlDocument();
			xmlDocumentLegacy.LoadXml(xmlSmall);
			Assert.AreEqual("\0",xmlDocumentLegacy.DocumentElement.FirstChild.Value,"LoadXml took the reference as a character.");
			//A document within the limits reads alike with both.
			string xmlFine="<ClinicalDocument xmlns=\"urn:hl7-org:v3\"><a>&#x41;&amp;</a></ClinicalDocument>";
			Assert.IsNull(EhrStylesheetStarter.GetLimitError(xmlFine));
			XmlDocument xmlDocumentFine=new XmlDocument();
			xmlDocumentFine.LoadXml(xmlFine);
			Assert.AreEqual(xmlDocumentFine.InnerXml,EhrStylesheetStarter.LoadXmlDocument(xmlFine).InnerXml);
		}

		///<summary>The child's resolver serves exactly the four embedded files under ehr-res:/// and refuses everything else, so document() and the
		///stylesheet loader can read no file, share or web address, and no other name (read without running any stylesheet).</summary>
		[TestMethod]
		public void Resolver_ServesTheFourFilesOnly() {
			EhrRenderChild.ResourceResolver resourceResolver=new EhrRenderChild.ResourceResolver();
			foreach(string fileName in new[] { "CDA.xsl","cda_l10n.xml","cda_narrativeblock.xml","ccr.xsl" }) {
				Uri uri=EhrRenderChild.ResourceResolver.GetUri(fileName);
				Assert.AreEqual("ehr-res:///"+fileName,uri.AbsoluteUri);
				using(Stream stream=(Stream)resourceResolver.GetEntity(uri,null,typeof(Stream)))
				using(StreamReader streamReader=new StreamReader(stream,new UTF8Encoding(false))) {
					Assert.AreEqual(EhrStylesheetStarter.GetResourceText(fileName),streamReader.ReadToEnd(),fileName);
				}
			}
			//CDA.xsl's document('cda_l10n.xml') and ccr.xsl's document('') resolve against the stylesheet's own URI.
			Uri uriCda=EhrRenderChild.ResourceResolver.GetUri("CDA.xsl");
			Assert.AreEqual("ehr-res:///cda_l10n.xml",resourceResolver.ResolveUri(uriCda,"cda_l10n.xml").AbsoluteUri);
			Assert.AreEqual("ehr-res:///ccr.xsl",resourceResolver.ResolveUri(EhrRenderChild.ResourceResolver.GetUri("ccr.xsl"),"").AbsoluteUri);
			Assert.AreEqual("ehr-res:///cda_narrativeblock.xml",resourceResolver.ResolveUri(null,"cda_narrativeblock.xml").AbsoluteUri);
			foreach(string uriText in new[] { "file:///C:/Windows/win.ini",@"\\192.0.2.10\share\secret.xml","http://192.0.2.10/CDA.xsl",
				"https://192.0.2.10/cda_l10n.xml","ehr-res:///secret.xml","ehr-res://192.0.2.10/CDA.xsl","ehr-res:///CDA.xsl?x=1","ehr-res:///CDA.xsl#a",
				"ehr-res:///Resources/CDA.xsl","ehr-res:///ccd.xsl","ehr-res:///CDA.xsl/","ehr-res:///cda.xsl" })
			{
				Uri uri=resourceResolver.ResolveUri(uriCda,uriText);
				Assert.IsNull(EhrRenderChild.ResourceResolver.GetText(uri),uriText);
				Expect.Throws<XmlException>(() => resourceResolver.GetEntity(uri,null,typeof(Stream)),uriText);
			}
			//A relative path that climbs out of the folder stays under ehr-res:/// and is refused unless it names one of the four.
			Assert.AreEqual("ehr-res:///win.ini",resourceResolver.ResolveUri(uriCda,"../../win.ini").AbsoluteUri);
			Expect.Throws<XmlException>(() => resourceResolver.GetEntity(resourceResolver.ResolveUri(uriCda,"../../win.ini"),null,typeof(Stream)));
		}
		#endregion Limits

		#region Narrative and description texts
		///<summary>A CCD whose narrative holds script elements in two namespaces, event handler attributes, and images and links to a file, a share
		///and the web: the page has none of them, and the narrative's text is still there.</summary>
		[TestMethod]
		public void Ccd_ScriptsLinksAndImages_NotInThePage() {
			string body="<paragraph>Before <linkHtml href=\"file:///C:/Windows/win.ini\">link four</linkHtml> after</paragraph>"
				+"<paragraph><linkHtml href=\"\\\\192.0.2.10\\share\\a.txt\">link five</linkHtml></paragraph>"
				+"<paragraph><linkHtml href=\"http://192.0.2.10/a.html\" onclick=\"alert(4)\">link six</linkHtml></paragraph>"
				+"<paragraph><content onmouseover=\"alert(5)\" styleCode=\"Bold\">hover</content></paragraph>"
				+"<script>alert(6)</script>"
				+"<script xmlns=\"http://www.w3.org/1999/xhtml\">alert(7)</script>"
				+"<paragraph><renderMultiMedia referencedObject=\"MM1\"/><renderMultiMedia referencedObject=\"MM2\"/>"
				+"<renderMultiMedia referencedObject=\"MM3\"/></paragraph>"
				+"<table><tbody><tr><td><img xmlns=\"http://www.w3.org/1999/xhtml\" src=\"file:///C:/Windows/x.png\"/>cell</td></tr></tbody></table>";
			string entries="";
			string[] arrayReferences={ @"\\192.0.2.10\share\x.png","file:///C:/Windows/x.png","http://192.0.2.10/x.png" };
			for(int i=0;i<arrayReferences.Length;i++) {
				entries+="<entry><observationMedia classCode=\"OBS\" moodCode=\"EVN\" ID=\"MM"+(i+1)+"\"><id root=\"2.25.20"+i+"\"/>"
					+"<value xsi:type=\"ED\" mediaType=\"image/png\"><reference value=\""+Escape(arrayReferences[i])+"\"/></value></observationMedia></entry>";
			}
			string html=CdaGolden.Render(EhrRenderKind.Ccd,MakeCcd(body,entries));
			AssertHarmless(html);
			Assert.IsFalse(Regex.IsMatch(html,@"<img\b[^>]*\bsrc\s*=\s*""(?!data:)",RegexOptions.IgnoreCase),"No image from outside the page.");
			Assert.IsFalse(Regex.IsMatch(html,@"<iframe\b",RegexOptions.IgnoreCase),"No frame.");
			//The text of the narrative is still there.
			string text=CdaGolden.VisibleText(html);
			foreach(string visible in new[] { "Before","link four","after","link five","link six","hover","cell" }) {
				StringAssert.Contains(text,visible);
			}
		}

		///<summary>A CCD with a javascript: link or image reference (any case, with spaces, a tab entity): CDA.xsl either refuses the document
		///("Javascript injection attempt detected", the window then shows the XML view with that reason) or the page has no script URL.</summary>
		[TestMethod]
		public void Ccd_JavascriptUrls_RefusedOrHarmless() {
			List<string> listDocuments=new List<string>();
			foreach(string href in new[] { "javascript:alert(1)"," JaVaScRiPt:alert(2)","java&#x09;script:alert(3)","vbscript:msgbox(4)" }) {
				listDocuments.Add(MakeCcd("<paragraph>Before <linkHtml href=\""+href+"\">the link</linkHtml> after</paragraph>"));
			}
			listDocuments.Add(MakeCcd("<paragraph><renderMultiMedia referencedObject=\"MM1\"/></paragraph>",
				"<entry><observationMedia classCode=\"OBS\" moodCode=\"EVN\" ID=\"MM1\"><id root=\"2.25.300\"/><value xsi:type=\"ED\" mediaType=\"image/png\">"
				+"<reference value=\"javascript:alert(5)\"/></value></observationMedia></entry>"));
			int countRefused=0;
			List<EhrRenderResult> listEhrRenderResults;
			using(CountingStarter countingStarter=new CountingStarter()) {
				listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,listDocuments);
				Assert.AreEqual(1,countingStarter.CountStarted,"A document the stylesheet refuses does not end the child.");
			}
			foreach(EhrRenderResult ehrRenderResult in listEhrRenderResults) {
				if(ehrRenderResult.Html==null) {
					StringAssert.Contains(ehrRenderResult.Reason,"Javascript injection attempt detected");
					countRefused++;
					continue;
				}
				AssertHarmless(ehrRenderResult.Html);
			}
			Assert.IsTrue(countRefused>0,"CDA.xsl refuses a plain javascript: link.");
		}

		///<summary>A CCR whose social history, result and plan-of-care Description/Text hold a script element, a javascript: link and images from a
		///share and a file: the page shows them as text, never as markup.</summary>
		[TestMethod]
		public void Ccr_DescriptionTexts_ShownAsText() {
			string textScript="<script>alert(1)</script>";
			string textLink="<a href=\"javascript:alert(2)\">click</a>";
			string textShare="<img src=\"\\\\192.0.2.10\\share\\x.png\">";
			string textFile="<img src=\"file:///C:/Windows/x.png\" onerror=\"alert(3)\">";
			string textGoal="<iframe src=\"http://192.0.2.10/\"></iframe><b>goal</b>";
			string html=CdaGolden.Render(EhrRenderKind.Ccr,MakeCcr(textScript,textLink,textShare,textFile,textGoal));
			AssertHarmless(html);
			Assert.IsFalse(Regex.IsMatch(html,@"<(img|iframe|a)\b[^>]*192\.0\.2\.",RegexOptions.IgnoreCase),"No element from the texts.");
			Assert.IsFalse(Regex.IsMatch(html,@"<b>goal</b>"),"No bold from the texts.");
			//Each text shows as it was written, as text.
			string text=CdaGolden.VisibleText(html);
			foreach(string visible in new[] { textScript,textLink,textShare,textFile,textGoal }) {
				StringAssert.Contains(text,visible);
			}
		}

		///<summary>A CCR whose references' locations are a javascript: URL (also in mixed case with a tab character reference), a vbscript: URL, a
		///file URL and a share path: ccr.xsl writes each location as the href of a link, unchecked, and the page writer drops every one of those
		///hrefs, so the page has no script, file or share link; the location still shows as the link's text. A web link stays.</summary>
		[TestMethod]
		public void Ccr_ReferenceLocations_NoScriptFileOrShareLinks() {
			string[] arrayLocationsXml={ "javascript:alert(1)"," JaVa&#x09;ScRiPt:alert(2)","vbscript:x","file:///C:/Windows/win.ini",@"\\192.0.2.10\share\x.exe",
				"https://www.example.test/reference" };
			string html=CdaGolden.Render(EhrRenderKind.Ccr,MakeCcr("Test","One","Two","Order","Goal",arrayLocationsXml));
			AssertHarmless(html);
			List<string> listHrefs=Regex.Matches(html,@"<a\b[^>]*\shref\s*=\s*""([^""]*)""",RegexOptions.IgnoreCase).Cast<Match>()
				.Select(x => System.Net.WebUtility.HtmlDecode(x.Groups[1].Value)).ToList();
			Assert.IsTrue(listHrefs.Count>0,"The page has links.");
			foreach(string href in listHrefs) {
				string hrefRead=new string(href.Where(x => x>' ').ToArray()).ToLowerInvariant();
				Assert.IsFalse(hrefRead.StartsWith("file:") || hrefRead.StartsWith(@"\\") || hrefRead.StartsWith("//") || hrefRead.StartsWith("/\\"),"A file or share link: "+href);
				Assert.IsFalse(Regex.IsMatch(hrefRead,"(javascript|vbscript):"),"A script link: "+href);
			}
			CollectionAssert.Contains(listHrefs,"https://www.example.test/reference","A web link stays.");
			Assert.AreEqual(5,Regex.Matches(html,@"<a target=""_blank"">").Count,"The five other locations are links without an href.");
			//Each location still shows, as text.
			string text=CdaGolden.VisibleText(html);
			foreach(string visible in new[] { "javascript:alert(1)","vbscript:x","file:///C:/Windows/win.ini",@"\\192.0.2.10\share\x.exe","ScRiPt:alert(2)" }) {
				StringAssert.Contains(text,visible);
			}
		}

		///<summary>The CCR writer of the child writes text a stylesheet marks disable-output-escaping (which reaches a writer as WriteRaw) escaped;
		///the CCD writer writes it as it is (CDA.xsl's own CSS). The shipped ccr.xsl has no such attribute any more; this checks the writer itself,
		///without running a stylesheet.</summary>
		[TestMethod]
		public void PageWriter_WritesRawTextEscaped_ForACcr() {
			Assert.AreEqual("<td>&lt;script&gt;alert(1)&lt;/script&gt;&lt;b&gt;x&lt;/b&gt; &amp; text</td>",WriteThroughPageWriter(isRawEscaped:true,xmlWriter => {
				xmlWriter.WriteStartElement("td");
				xmlWriter.WriteRaw("<script>alert(1)</script>");
				xmlWriter.WriteRaw("<b>x</b>".ToCharArray(),0,8);
				xmlWriter.WriteString(" & text");
				xmlWriter.WriteEndElement();
			}));
			Assert.AreEqual("<style>a&gt;b{}</style>",WriteThroughPageWriter(isRawEscaped:false,xmlWriter => {
				xmlWriter.WriteStartElement("style");
				xmlWriter.WriteRaw("a&gt;b{}");
				xmlWriter.WriteEndElement();
			}),"The CCD's raw text as it is.");
		}

		///<summary>The page writer drops an attribute through which a page loads something by itself (src, data, background, xlink:href, ...),
		///whatever the value is, unless it is a data: image; links and every other attribute stay. CDA.xsl's own sanitizer drops a URL with a colon,
		///but a share path (\\server\share\x.png) has none, and CDA.xsl wrote it as an image's src.</summary>
		[TestMethod]
		public void PageWriter_DropsSourcesOtherThanDataImages() {
			string html=WriteThroughPageWriter(isRawEscaped:false,xmlWriter => {
				xmlWriter.WriteStartElement("p");
				foreach(string value in new[] { @"\\192.0.2.10\share\x.png","file:///C:/x.png","http://192.0.2.10/x.png","//192.0.2.10/x.png","x.png",
					"javascript:alert(1)"," DATA:text/html,x","data:image/png;base64,AAAA"," Data:Image/gif;base64,BBBB" })
				{
					xmlWriter.WriteStartElement("img");
					xmlWriter.WriteAttributeString("alt","an image");
					xmlWriter.WriteAttributeString("src",value);
					xmlWriter.WriteEndElement();
				}
				xmlWriter.WriteStartElement("a");
				xmlWriter.WriteAttributeString("title","a share");
				xmlWriter.WriteAttributeString("href",@"\\192.0.2.10\share\a.txt");
				xmlWriter.WriteString("a link");
				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("object");
				xmlWriter.WriteStartAttribute("data");
				xmlWriter.WriteString("http://192.0.2.10/");
				xmlWriter.WriteCharEntity('x');
				xmlWriter.WriteEndAttribute();
				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("image");
				xmlWriter.WriteAttributeString("xlink","href","http://www.w3.org/1999/xlink","file:///C:/x.svg");
				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
			});
			Assert.AreEqual(9,Regex.Matches(html,@"<img alt=""an image""").Count,"Every image stays, without its source.");
			Assert.AreEqual(2,Regex.Matches(html,@"\ssrc=").Count,"Only the two data: images keep theirs.");
			StringAssert.Contains(html,"src=\"data:image/png;base64,AAAA\"");
			StringAssert.Contains(html,"src=\" Data:Image/gif;base64,BBBB\"");
			StringAssert.Contains(html,"<a title=\"a share\">a link</a>","The link to a share loses its href; its text stays.");
			StringAssert.Contains(html,"<object />");
			Assert.IsFalse(html.Contains("xlink:href"));
			Assert.IsTrue(EhrRenderChild.PageXmlWriter.IsAllowed("p","","title","","javascript:alert(1)"),"An attribute that loads nothing is kept.");
			Assert.IsFalse(EhrRenderChild.PageXmlWriter.IsAllowed("img","","SRC","","x.png"),"Any case.");
		}

		///<summary>The page writer keeps a link (the href of a or area, a cite) only when it goes to a place in the page (#), to the web (http://,
		///https://) or to an e-mail address (mailto:), read as a browser reads it (control characters and spaces dropped, any case): a javascript:,
		///vbscript:, data:, file: or share link, or a relative one, loses its href and keeps its text. An href of any other element (link, image,
		///base) or in a namespace (xlink:href) loads by itself and is dropped unless it is a data: image.</summary>
		[TestMethod]
		public void PageWriter_KeepsOnlyLinksToThePageTheWebOrEmail() {
			string[] arrayKept={ "#AA0001","http://www.example.test/a?b=1","HTTPS://www.example.test/","mailto:test@example.test"," https://www.example.test/x" };
			string[] arrayDropped={ "javascript:alert(1)"," JaVa\tScRiPt:alert(2)","java\nscript:alert(3)","vbscript:x","data:text/html,<b>x</b>",
				"file:///C:/Windows/win.ini",@"\\192.0.2.10\share\x.exe","//192.0.2.10/x","/\\192.0.2.10/x","x.html","","http:\\\\192.0.2.10\\x",
				"ftp://192.0.2.10/x" };
			string html=WriteThroughPageWriter(isRawEscaped:true,xmlWriter => {
				xmlWriter.WriteStartElement("p");
				foreach(string value in arrayKept.Concat(arrayDropped)) {
					xmlWriter.WriteStartElement("a");
					xmlWriter.WriteAttributeString("target","_blank");
					xmlWriter.WriteAttributeString("href",value);
					xmlWriter.WriteString("link");
					xmlWriter.WriteEndElement();
				}
				xmlWriter.WriteStartElement("blockquote");
				xmlWriter.WriteAttributeString("cite","javascript:alert(4)");
				xmlWriter.WriteString("quote");
				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("link");
				xmlWriter.WriteAttributeString("rel","stylesheet");
				xmlWriter.WriteAttributeString("href","http://192.0.2.10/x.css");
				xmlWriter.WriteEndElement();
				xmlWriter.WriteStartElement("a");
				xmlWriter.WriteAttributeString("xlink","href","http://www.w3.org/1999/xlink","http://www.example.test/");
				xmlWriter.WriteAttributeString("ping","http://192.0.2.10/p");
				xmlWriter.WriteString("namespaced");
				xmlWriter.WriteEndElement();
				xmlWriter.WriteEndElement();
			});
			List<string> listHrefs=Regex.Matches(html,@"\shref=""([^""]*)""").Cast<Match>().Select(x => System.Net.WebUtility.HtmlDecode(x.Groups[1].Value)).ToList();
			CollectionAssert.AreEqual(arrayKept,listHrefs,"Only the links to the page, the web or an e-mail address keep their href.");
			Assert.AreEqual(arrayKept.Length+arrayDropped.Length,Regex.Matches(html,@"<a target=""_blank""[^>]*>link</a>").Count,"Every link keeps its text.");
			StringAssert.Contains(html,"<blockquote>quote</blockquote>");
			StringAssert.Contains(html,"<link rel=\"stylesheet\" />","The stylesheet link loads by itself: no href.");
			StringAssert.Contains(html,"<a>namespaced</a>","xlink:href and ping are dropped.");
			Assert.IsTrue(EhrRenderChild.PageXmlWriter.IsAllowed("AREA","","HREF","","#x"));
			Assert.IsFalse(EhrRenderChild.PageXmlWriter.IsAllowed("area","","href","","javascript:x"));
			Assert.IsFalse(EhrRenderChild.PageXmlWriter.IsAllowed("image","","href","","http://www.example.test/x.png"),"An image's href loads.");
			Assert.IsTrue(EhrRenderChild.PageXmlWriter.IsAllowed("image","","href","","data:image/png;base64,AAAA"));
			Assert.IsFalse(EhrRenderChild.PageXmlWriter.IsAllowed("a","","href","http://www.w3.org/1999/xlink","http://www.example.test/"),"A namespaced href.");
		}

		private static string WriteThroughPageWriter(bool isRawEscaped,Action<XmlWriter> actionWrite) {
			StringBuilder stringBuilder=new StringBuilder();
			XmlWriterSettings xmlWriterSettings=new XmlWriterSettings();
			xmlWriterSettings.OmitXmlDeclaration=true;
			xmlWriterSettings.ConformanceLevel=ConformanceLevel.Fragment;
			using(XmlWriter xmlWriterInner=XmlWriter.Create(stringBuilder,xmlWriterSettings)) {
				using(XmlWriter xmlWriter=new EhrRenderChild.PageXmlWriter(xmlWriterInner,isRawEscaped)) {
					actionWrite(xmlWriter);
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>The shipped ccr.xsl has no disable-output-escaping attribute (the copy marked as modified, E-11).</summary>
		[TestMethod]
		public void CcrStylesheet_HasNoDisableOutputEscaping() {
			string xsl=EhrStylesheetStarter.GetResourceText("ccr.xsl");
			Assert.IsFalse(Regex.IsMatch(xsl,@"<xsl:[^>]*disable-output-escaping",RegexOptions.Singleline));
			//CDA.xsl uses it once, on its own CSS text, so the CCD keeps the normal writer.
			Assert.AreEqual(1,Regex.Matches(EhrStylesheetStarter.GetResourceText("CDA.xsl"),@"disable-output-escaping=""yes""").Count);
		}
		#endregion Narrative and description texts

		#region Children through the seam
		///<summary>A child that stalls on a document is killed at 15 seconds; the reason says so, the next document of a batch gets a new child,
		///and the test process is unaffected.</summary>
		[TestMethod]
		public void Seam_StallingChild_KilledAt15Seconds() {
			Assert.AreEqual(15000,EhrStylesheetStarter.TIMEOUT_DOCUMENT_MS);
			List<FakeChild> listFakeChildren=new List<FakeChild>();
			EhrStylesheetStarter.FuncStartChild=() => {
				FakeChild fakeChild=listFakeChildren.Count==0 ? FakeChild.Stalling() : FakeChild.Answering("<html>page</html>");
				listFakeChildren.Add(fakeChild);
				return fakeChild;
			};
			Stopwatch stopwatch=Stopwatch.StartNew();
			List<EhrRenderResult> listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,new List<string> { "<a/>","<b/>" });
			stopwatch.Stop();
			Assert.IsTrue(stopwatch.Elapsed.TotalSeconds>=14.5 && stopwatch.Elapsed.TotalSeconds<30,"Killed at 15 seconds: "+stopwatch.Elapsed);
			Assert.AreEqual(2,listFakeChildren.Count,"A new child for the next document.");
			Assert.IsTrue(listFakeChildren[0].IsKilled,"The stalling child was killed.");
			Assert.IsTrue(listFakeChildren[0].IsDisposed);
			Assert.IsNull(listEhrRenderResults[0].Html);
			Assert.AreEqual("The stylesheet did not finish within 15 seconds, so its process was ended.",listEhrRenderResults[0].Reason);
			Assert.AreEqual("<html>page</html>",listEhrRenderResults[1].Html);
			Assert.IsTrue(listFakeChildren[1].IsInputClosed && listFakeChildren[1].IsDisposed,"The last child ends with the end of its input.");
			Assert.IsFalse(listFakeChildren[1].IsKilled);
			//The frame the child got: the kind, the length and the document.
			CollectionAssert.AreEqual(new byte[] { 1,4,0,0,0,(byte)'<',(byte)'b',(byte)'/',(byte)'>' },listFakeChildren[1].GetInput());
		}

		///<summary>Children that end with either stack-overflow exit code (COR_E_STACKOVERFLOW or STATUS_STACK_OVERFLOW, both seen from .NET
		///Framework processes) give a reason that names the code; any other non-zero code is reported alike; the test process is unaffected.</summary>
		[TestMethod]
		public void Seam_StackOverflowExitCodes_GiveTheReason() {
			foreach(int exitCode in new[] { unchecked((int)0x800703E9),unchecked((int)0xC00000FD),5 }) {
				FakeChild fakeChild=FakeChild.Crashing(exitCode,"Process is terminated due to StackOverflowException.");
				EhrStylesheetStarter.FuncStartChild=() => fakeChild;
				Assert.IsNull(EhrStylesheetStarter.Render(EhrRenderKind.Ccr,"<a/>",out string reason));
				StringAssert.StartsWith(reason,"The stylesheet process ended with exit code 0x"+exitCode.ToString("X8"));
				StringAssert.Contains(reason,"Process is terminated due to StackOverflowException.","The first line of its standard error.");
				Assert.IsTrue(fakeChild.IsDisposed);
			}
			StringAssert.Contains(EhrStylesheetStarter.GetExitReason(unchecked((int)0x800703E9)),"COR_E_STACKOVERFLOW, a stack overflow");
			StringAssert.Contains(EhrStylesheetStarter.GetExitReason(unchecked((int)0xC00000FD)),"STATUS_STACK_OVERFLOW, a stack overflow");
			Assert.AreEqual("The stylesheet process ended with exit code 0x00000005.",EhrStylesheetStarter.GetExitReason(5));
		}

		///<summary>A child that cannot be started gives every document of the batch the same reason, and the starter does not try again for each.</summary>
		[TestMethod]
		public void Seam_ChildThatCannotStart_GivesTheReason() {
			int countTried=0;
			EhrStylesheetStarter.FuncStartChild=() => {
				countTried++;
				throw new FileNotFoundException("The system cannot find the file specified");
			};
			List<EhrRenderResult> listEhrRenderResults=EhrStylesheetStarter.RenderBatch(EhrRenderKind.Ccd,new List<string> { "<a/>","<b/>" });
			Assert.AreEqual(1,countTried);
			foreach(EhrRenderResult ehrRenderResult in listEhrRenderResults) {
				Assert.AreEqual("The stylesheet process could not be started: The system cannot find the file specified",ehrRenderResult.Reason);
			}
		}

		///<summary>The frames: a kind or status byte and a little-endian length; a frame cut short or with a length out of range throws.</summary>
		[TestMethod]
		public void Frames_RoundTrip_AndBrokenFramesThrow() {
			byte[] arrayBody=new byte[70000];
			new Random(3).NextBytes(arrayBody);
			MemoryStream memoryStream=new MemoryStream();
			EhrStylesheetStarter.WriteFrame(memoryStream,2,arrayBody);
			memoryStream.Position=0;
			CollectionAssert.AreEqual(arrayBody,EhrStylesheetStarter.ReadFrame(memoryStream,100000,out byte kind));
			Assert.AreEqual(2,kind);
			Assert.IsNull(EhrStylesheetStarter.ReadFrame(memoryStream,100000,out kind),"The end of the stream.");
			memoryStream.Position=0;
			Expect.Throws<InvalidDataException>(() => EhrStylesheetStarter.ReadFrame(memoryStream,1000,out byte kindTooLong));
			Expect.Throws<EndOfStreamException>(() => EhrStylesheetStarter.ReadFrame(new MemoryStream(new byte[] { 1,2 }),1000,out byte kindShort));
			Expect.Throws<EndOfStreamException>(() => EhrStylesheetStarter.ReadFrame(new MemoryStream(new byte[] { 1,5,0,0,0,1 }),1000,out byte kindBody));
			Expect.Throws<InvalidDataException>(() => EhrStylesheetStarter.ReadFrame(new MemoryStream(new byte[] { 1,0,0,0,0x80 }),1000,out byte kindNegative));
		}
		#endregion Children through the seam
	}

	///<summary>A child of the starter's seam, in memory: it takes the frame and answers with a page, stalls until killed, or ends with an exit
	///code and a line on standard error.</summary>
	public class FakeChild:IEhrRenderChild {
		private readonly MemoryStream _memoryStreamInput=new MemoryStream();
		private Stream _streamOutput;
		private readonly ManualResetEvent _manualResetEventKilled=new ManualResetEvent(false);
		private int _exitCode;
		private string _errorText="";
		public bool IsKilled;
		public bool IsDisposed;
		public bool IsInputClosed;

		public static FakeChild Answering(string html) {
			FakeChild fakeChild=new FakeChild();
			MemoryStream memoryStream=new MemoryStream();
			EhrStylesheetStarter.WriteFrame(memoryStream,EhrStylesheetStarter.STATUS_HTML,new UTF8Encoding(false).GetBytes(html));
			memoryStream.Position=0;
			fakeChild._streamOutput=memoryStream;
			return fakeChild;
		}

		public static FakeChild Stalling() {
			FakeChild fakeChild=new FakeChild();
			fakeChild._streamOutput=new StallingStream(fakeChild._manualResetEventKilled);
			return fakeChild;
		}

		public static FakeChild Crashing(int exitCode,string errorText) {
			FakeChild fakeChild=new FakeChild();
			fakeChild._streamOutput=new MemoryStream();//The output ends before a reply.
			fakeChild._exitCode=exitCode;
			fakeChild._errorText=errorText+"\r\n   at System.Xml.Xsl.Runtime (synthetic)\r\n";
			return fakeChild;
		}

		public byte[] GetInput() {
			return _memoryStreamInput.ToArray();
		}

		public Stream Input {
			get {
				return new InputStream(this);
			}
		}

		public Stream Output {
			get {
				return _streamOutput;
			}
		}

		public string ErrorText {
			get {
				return _errorText;
			}
		}

		public int ExitCode {
			get {
				return _exitCode;
			}
		}

		public bool WaitForExit(int milliseconds) {
			return true;
		}

		public void Kill() {
			IsKilled=true;
			_manualResetEventKilled.Set();
		}

		public void Dispose() {
			IsDisposed=true;
		}

		///<summary>The fake's standard input: what is written is kept, closing it is recorded.</summary>
		private class InputStream:Stream {
			private readonly FakeChild _fakeChild;

			public InputStream(FakeChild fakeChild) {
				_fakeChild=fakeChild;
			}

			public override void Write(byte[] buffer,int offset,int count) {
				_fakeChild._memoryStreamInput.Write(buffer,offset,count);
			}

			public override void Close() {
				_fakeChild.IsInputClosed=true;
				base.Close();
			}

			public override bool CanRead { get { return false; } }
			public override bool CanSeek { get { return false; } }
			public override bool CanWrite { get { return true; } }
			public override long Length { get { throw new NotSupportedException(); } }
			public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
			public override void Flush() { }
			public override int Read(byte[] buffer,int offset,int count) { throw new NotSupportedException(); }
			public override long Seek(long offset,SeekOrigin origin) { throw new NotSupportedException(); }
			public override void SetLength(long value) { throw new NotSupportedException(); }
		}

		///<summary>An output that gives nothing until the child is killed, then ends.</summary>
		private class StallingStream:Stream {
			private readonly ManualResetEvent _manualResetEventKilled;

			public StallingStream(ManualResetEvent manualResetEventKilled) {
				_manualResetEventKilled=manualResetEventKilled;
			}

			public override int Read(byte[] buffer,int offset,int count) {
				_manualResetEventKilled.WaitOne();
				return 0;
			}

			public override bool CanRead { get { return true; } }
			public override bool CanSeek { get { return false; } }
			public override bool CanWrite { get { return false; } }
			public override long Length { get { throw new NotSupportedException(); } }
			public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
			public override void Flush() { }
			public override long Seek(long offset,SeekOrigin origin) { throw new NotSupportedException(); }
			public override void SetLength(long value) { throw new NotSupportedException(); }
			public override void Write(byte[] buffer,int offset,int count) { throw new NotSupportedException(); }
		}
	}
}
