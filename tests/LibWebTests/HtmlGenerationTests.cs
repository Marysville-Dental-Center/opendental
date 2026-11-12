// The HTML the program builds for its browser windows: text from the database is encoded (CodeBase.HtmlPage), a CCD shows with the program's
// own stylesheet run in a child process (EHR-3) or as encoded XML, no stylesheet ever runs in the program's own process and none from an email
// runs anywhere, the CCD window keeps the document in memory (no file while it is shown) and only the files an older version left are deleted,
// the preference details page (FormWebBrowserPrefs.BuildHtml), and what a link in a wiki page opens (FormWiki.ParseLink). Synthetic data only.
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
using Mono.Cecil.Cil;
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

		///<summary>The one file of the program that may name an XSLT class: the stylesheet child (EHR-3, docs/port-notes/EHR-3.md).</summary>
		private const string FILE_XSLT_CHILD=@"OpenPmsDental\Main Modules\EhrRenderChild.cs";
		///<summary>The child's class in the built exe.</summary>
		private const string TYPE_XSLT_CHILD="OpenPmsDental.EhrRenderChild";

		///<summary>No XSLT stylesheet runs in the program's own process, and none that came with an email (a CCD's .xsl attachment) runs anywhere: a
		///stylesheet that recurses without end overflows the stack, and a StackOverflowException ends the process whatever catches it (checked with a
		///32-bit .NET Framework 4.8 process during the review); one that loops never returns. The program's own stylesheets (HL7's CDA.xsl and the
		///AAFP's ccr.xsl, EHR-3) run in exactly one place, a short-lived child process of the program's own exe ("/ehr-render", EhrRenderChild),
		///which EhrStylesheetStarter starts and kills after 15 seconds: an overflow or a loop there ends or stalls only the child, never the
		///program, and the child reads only the four embedded files. FormEhrSummaryOfCare.DisplayCCD never reads the attachment.</summary>
		[TestMethod]
		public void NoStylesheet_FromAnEmail_RunsInTheProgram() {
			Regex regexXslt=new Regex(@"\b(XslCompiledTransform|XslTransform|TransformXsl)\b");
			List<string> listFiles=TestPaths.GetShippedFiles(".cs");
			Assert.IsTrue(listFiles.Any(x => x.EndsWith(@"OpenPmsDental\Forms\FormEhrSummaryOfCare.cs",StringComparison.OrdinalIgnoreCase)),"The scan covers the program.");
			Assert.IsTrue(listFiles.Any(x => TestPaths.Relative(x).Equals(FILE_XSLT_CHILD,StringComparison.OrdinalIgnoreCase)),"The scan sees the child's file.");
			List<string> listViolations=new List<string>();
			int countAllowed=0;
			int countChild=0;
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
					if(relative.Equals(FILE_XSLT_CHILD,StringComparison.OrdinalIgnoreCase)) {
						countChild++;
						continue;
					}
					listViolations.Add(relative+"("+(i+1)+"): "+arrayLines[i].Trim());
				}
			}
			Assert.AreEqual(0,listViolations.Count,"XSLT in the program's process:\r\n"+string.Join("\r\n",listViolations));
			Assert.IsTrue(countAllowed>0,"The scan sees XSLT where it is allowed, so it would see it anywhere.");
			Assert.IsTrue(countChild>0,"The child names its XSLT class, and the scan sees it.");
			Assert.IsTrue(typeof(HtmlPage).GetMethod("TransformXsl")==null,"HtmlPage runs no stylesheet.");
			//The attachment's stylesheet is never read: the CCD shows with the program's own stylesheet, or as XML, with a line that says so.
			string source=TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryOfCare.cs");
			Assert.IsFalse(Regex.IsMatch(source,@"\bRead\w*\(\s*strAlterateFilPathXslCCD\b"),"DisplayCCD reads the email's stylesheet.");
			string sourceDisplay=GetDisplayCcdSource(source);
			Assert.AreEqual(2,Regex.Matches(sourceDisplay,@"\bstrAlterateFilPathXslCCD\b").Count,"The parameter, and its test for being empty.");
			StringAssert.Contains(sourceDisplay,"bool isStylesheetAttached=isCcd && !string.IsNullOrEmpty(strAlterateFilPathXslCCD);");
			StringAssert.Contains(sourceDisplay,"The stylesheet that came with this document was not run, so the document is shown as XML.");
			StringAssert.Contains(sourceDisplay,"The stylesheet that came with this document was not run. The document is shown with the program's own stylesheet.");
			StringAssert.Contains(TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryCcdEdit.cs"),"html=HtmlPage.BuildXml(xmlCcd,note);");
		}

		///<summary>The built assemblies the program is made of, read with Mono.Cecil (nothing is loaded): the libraries reference no type of
		///System.Xml.Xsl; in the exe, every method whose IL, signature or locals use one is the child's (EhrRenderChild or a compiler-generated type
		///nested in it), and so is every field of such a type; XslTransform, the old processor, is used nowhere.</summary>
		[TestMethod]
		public void BuiltAssemblies_XsltOnlyInTheChild() {
			foreach(string fileName in new[] { "CodeBase.dll","OpenPmsDentalBusiness.dll","WpfControlsOD.dll","DataConnectionBase.dll" }) {
				using(ModuleDefinition moduleDefinition=ReadModule(fileName)) {
					List<string> listXslt=moduleDefinition.GetTypeReferences().Where(x => x.Namespace=="System.Xml.Xsl").Select(x => x.FullName).ToList();
					Assert.AreEqual(0,listXslt.Count,fileName+" uses "+string.Join(", ",listXslt));
				}
			}
			using(ModuleDefinition moduleDefinition=ReadModule("OpenPmsDental.exe")) {
				List<string> listReferences=moduleDefinition.GetTypeReferences().Where(x => x.Namespace=="System.Xml.Xsl").Select(x => x.Name).ToList();
				CollectionAssert.Contains(listReferences,"XslCompiledTransform","The exe's child uses XslCompiledTransform, so the check would see it.");
				CollectionAssert.DoesNotContain(listReferences,"XslTransform","The old processor is used nowhere.");
				List<string> listViolations=new List<string>();
				int countChildMethods=0;
				foreach(TypeDefinition typeDefinition in GetAllTypes(moduleDefinition)) {
					bool isChild=IsChildType(typeDefinition);
					foreach(FieldDefinition fieldDefinition in typeDefinition.Fields) {
						if(IsXslt(fieldDefinition.FieldType) && !isChild) {
							listViolations.Add(fieldDefinition.FullName);
						}
					}
					foreach(MethodDefinition methodDefinition in typeDefinition.Methods) {
						if(!UsesXslt(methodDefinition)) {
							continue;
						}
						if(isChild) {
							countChildMethods++;
							continue;
						}
						listViolations.Add(methodDefinition.FullName);
					}
				}
				Assert.AreEqual(0,listViolations.Count,"XSLT outside the child:\r\n"+string.Join("\r\n",listViolations));
				Assert.IsTrue(countChildMethods>0,"The child's methods use XSLT, and the check sees them.");
			}
		}

		///<summary>One way in: EhrRenderChild.Run has exactly one caller, ProgramEntry.Main, whose first statement tests for /ehr-render and ends the
		///process with Environment.Exit before any Application, RenderOptions or ODInitialize call: the child shows no window and opens no database.
		///Nothing else of the program references the child's class.</summary>
		[TestMethod]
		public void Child_HasOneWayIn_FirstInMain() {
			using(ModuleDefinition moduleDefinition=ReadModule("OpenPmsDental.exe")) {
				List<string> listCallers=new List<string>();
				List<string> listOtherUses=new List<string>();
				foreach(TypeDefinition typeDefinition in GetAllTypes(moduleDefinition)) {
					if(IsChildType(typeDefinition)) {
						continue;
					}
					foreach(MethodDefinition methodDefinition in typeDefinition.Methods.Where(x => x.HasBody)) {
						foreach(Instruction instruction in methodDefinition.Body.Instructions) {
							if(instruction.Operand is MethodReference methodReference && methodReference.DeclaringType.FullName==TYPE_XSLT_CHILD) {
								if(methodReference.Name=="Run") {
									listCallers.Add(methodDefinition.DeclaringType.FullName+"."+methodDefinition.Name);
								}
								else {
									listOtherUses.Add(methodDefinition.FullName+": "+methodReference.Name);
								}
							}
						}
					}
				}
				CollectionAssert.AreEqual(new[] { "OpenPmsDental.ProgramEntry.Main" },listCallers,"EhrRenderChild.Run's callers.");
				Assert.AreEqual(0,listOtherUses.Count,string.Join("\r\n",listOtherUses));
			}
			string programEntry=TestPaths.ReadSource("OpenPmsDental","Main Modules","ProgramEntry.cs");
			Match match=Regex.Match(programEntry,@"static void Main\(string\[\] args\) \{\s*(//[^\n]*\n\s*)*"
				+Regex.Escape("if(args.Length==2 && args[0]==\"/ehr-render\") { Environment.Exit(EhrRenderChild.Run(args[1])); }"));
			Assert.IsTrue(match.Success,"The /ehr-render test is Main's first statement and ends in Environment.Exit.");
			string rest=programEntry.Substring(match.Index+match.Length);
			int indexRenderOptions=rest.IndexOf("RenderOptions.ProcessRenderMode");
			Assert.IsTrue(indexRenderOptions>=0 && Regex.IsMatch(rest.Substring(0,indexRenderOptions),@"^\s*System\.Windows\.Media\.$"),
				"RenderOptions comes right after it (owner decision O-34).");
			foreach(string text in new[] { "Application.","ODInitialize.","RenderOptions." }) {
				Assert.IsTrue(rest.Contains(text),text+" is after the test.");
				Assert.IsFalse(programEntry.Substring(0,match.Index).Contains(text),text+" is not before it.");
			}
			Assert.AreEqual("/ehr-render",OpenPmsDentalBusiness.EhrStylesheetStarter.ARG_RENDER,"The starter starts the child with the argument Main tests for.");
		}

		[TestMethod]
		public void CcdView_KeepsTheDocumentInMemory_AndWritesNoFile() {
			//The window gets the CCD's text and the page the stylesheet child made of it, not a file: no path field.
			Type typeForm=typeof(FormEhrSummaryCcdEdit);
			BindingFlags bindingFlags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
			Assert.IsNull(typeForm.GetField("StrXmlFilePath",bindingFlags),"FormEhrSummaryCcdEdit keeps no file path.");
			Assert.AreEqual(typeof(string),typeForm.GetField("_xmlCcd",bindingFlags)?.FieldType,"FormEhrSummaryCcdEdit keeps the text.");
			Assert.AreEqual(typeof(string),typeForm.GetField("_htmlRendered",bindingFlags)?.FieldType,"FormEhrSummaryCcdEdit keeps the page.");
			ConstructorInfo[] arrayConstructors=typeForm.GetConstructors();
			Assert.AreEqual(1,arrayConstructors.Length);
			CollectionAssert.AreEqual(new[] { "xmlCcd","htmlRendered","patCur","note" },arrayConstructors[0].GetParameters().Select(x => x.Name).ToArray());
			//Shown, reconciled and shown as raw XML from that text: the window reads no file and writes none.
			string sourceForm=TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryCcdEdit.cs");
			foreach(string text in new[] { "File.","Path.","FileStream","StreamReader","StreamWriter","GetTempFolderPath","using System.IO;" }) {
				Assert.IsFalse(sourceForm.Contains(text),"FormEhrSummaryCcdEdit.cs: "+text);
			}
			//The three reconcile buttons read the text: the limits again, then the strict reader they were counted with, never LoadXml's lenient one.
			Assert.AreEqual(3,Regex.Matches(sourceForm,Regex.Escape("XmlDocument xmlDocCcd=GetXmlDocumentForReconcile();")).Count,"The three reconcile buttons read the text.");
			int indexFormLimits=sourceForm.IndexOf("string reasonLimits=EhrStylesheetStarter.GetLimitError(_xmlCcd);");
			int indexFormLoad=sourceForm.IndexOf("return EhrStylesheetStarter.LoadXmlDocument(_xmlCcd);");
			Assert.IsTrue(indexFormLimits>=0 && indexFormLoad>indexFormLimits,"The reconcile buttons check the limits first.");
			Assert.IsFalse(Regex.IsMatch(sourceForm,@"\.LoadXml\("),"FormEhrSummaryCcdEdit uses LoadXml.");
			StringAssert.Contains(sourceForm,"string strCcd=_xmlCcd;");
			StringAssert.Contains(sourceForm,"html=BuildPage(_xmlCcd,_htmlRendered,_note);");
			//DisplayCCD hands the window the document and the page and writes them nowhere; it deletes only what an older version left.
			string sourceDisplay=GetDisplayCcdSource(TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryOfCare.cs"));
			foreach(string text in new[] { "File.","WriteAll","GetTempFolderPath","GetRandomTempFile","FileStream","StreamWriter" }) {
				Assert.IsFalse(sourceDisplay.Contains(text),"DisplayCCD: "+text);
			}
			StringAssert.Contains(sourceDisplay,"new FormEhrSummaryCcdEdit(doc.InnerXml,htmlRendered,patReconcile,note)");
			StringAssert.Contains(sourceDisplay,"DeleteOldCcdFiles(Path.Combine(Path.GetTempPath(),ProgramNames.TempFolderName),DateTime.Now.AddHours(-1));");
			//The limits are checked on the text before the program builds its own XmlDocument (InnerXml writes recursively), and the document is
			//built with the strict reader the limits were counted with, never with LoadXml's more lenient one.
			int indexLimits=sourceDisplay.IndexOf("EhrStylesheetStarter.GetLimitError(strXmlCCD)");
			int indexLoad=sourceDisplay.IndexOf("doc=EhrStylesheetStarter.LoadXmlDocument(strXmlCCD);");
			Assert.IsTrue(indexLimits>=0 && indexLoad>indexLimits,"The limits come first.");
			Assert.IsFalse(Regex.IsMatch(sourceDisplay,@"\.LoadXml\("),"DisplayCCD uses LoadXml.");
			//The starter sends the document through a pipe and writes no file either.
			string sourceStarter=GetClassSource(TestPaths.ReadSource("OpenPmsDentalBusiness","Data Interface","EhrSummaryCcds.cs"),"public class EhrStylesheetStarter {");
			foreach(string text in new[] { "File.","Path.GetTemp","FileStream","StreamWriter" }) {
				Assert.IsFalse(sourceStarter.Contains(text),"EhrStylesheetStarter: "+text);
			}
		}

		///<summary>A CCR never gets the three reconcile buttons, as before, when it showed as raw text only: they run the CCD's reconcile code
		///(EhrCCD.GetList...), which reads CDA templates. DisplayCCD gives the window the patient only for a CCD, and the window shows the buttons only
		///for a patient of the database; a document over the limits gets no patient either.</summary>
		[TestMethod]
		public void CcdView_ReconcileButtons_OnlyForACcd() {
			OpenPmsDentalBusiness.Patient patient=new OpenPmsDentalBusiness.Patient { PatNum=5 };
			Assert.IsFalse(FormEhrSummaryCcdEdit.IsReconcileShown(FormEhrSummaryOfCare.GetPatientReconcile(isCcr:true,patient)),"A CCR: no reconcile buttons.");
			Assert.IsTrue(FormEhrSummaryCcdEdit.IsReconcileShown(FormEhrSummaryOfCare.GetPatientReconcile(isCcr:false,patient)),"A CCD: the buttons.");
			Assert.AreSame(patient,FormEhrSummaryOfCare.GetPatientReconcile(isCcr:false,patient));
			Assert.IsFalse(FormEhrSummaryCcdEdit.IsReconcileShown(null),"No patient.");
			Assert.IsFalse(FormEhrSummaryCcdEdit.IsReconcileShown(new OpenPmsDentalBusiness.Patient()),"A patient not in the database.");
			//DisplayCCD decides with isCcr (the root element ContinuityOfCareRecord) and gives the window that patient; the window's Load hides the
			//buttons by IsReconcileShown; a document over the limits opens the window with no patient.
			string sourceDisplay=GetDisplayCcdSource(TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryOfCare.cs"));
			StringAssert.Contains(sourceDisplay,"bool isCcr=rootName==\"continuityofcarerecord\" || rootName==\"ccr:continuityofcarerecord\";");
			StringAssert.Contains(sourceDisplay,"Patient patReconcile=GetPatientReconcile(isCcr,patCur);");
			StringAssert.Contains(sourceDisplay,"new FormEhrSummaryCcdEdit(strXmlCCD,null,null,noteLimits)");
			Assert.AreEqual(2,Regex.Matches(sourceDisplay,@"new FormEhrSummaryCcdEdit\(").Count,"DisplayCCD opens the window in two places.");
			Assert.AreEqual(0,Regex.Matches(sourceDisplay,@"new FormEhrSummaryCcdEdit\([^)]*\bpatCur\b").Count,"Neither gets patCur itself.");
			string sourceForm=TestPaths.ReadSource("OpenPmsDental","Forms","FormEhrSummaryCcdEdit.cs");
			StringAssert.Contains(sourceForm,"if(!IsReconcileShown(_patCur)) {");
		}

		///<summary>The page the window shows: the rendered page with the note encoded as its first paragraph; without one, the indented XML view; a
		///document over the child's limits as plain encoded text, cut after 1 MB (no indentation, which grows with the depth squared).</summary>
		[TestMethod]
		public void CcdView_Page_RenderedXmlOrPlainText() {
			string htmlRendered="<html><head><title>t</title></head><body class=\"x\"><p>page</p></body></html>";
			string page=FormEhrSummaryCcdEdit.BuildPage(XML_CCD,htmlRendered,MARKUP_NAME);
			StringAssert.StartsWith(page,"<html><head><title>t</title></head><body class=\"x\"><p style=");
			StringAssert.Contains(page,">Jane &lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;Doe&quot; O&#39;Brien</p><p>page</p></body></html>");
			Assert.AreEqual(htmlRendered,FormEhrSummaryCcdEdit.BuildPage(XML_CCD,htmlRendered,""),"No note, the page as it came.");
			Assert.AreEqual(HtmlPage.BuildXml(XML_CCD,"note"),FormEhrSummaryCcdEdit.BuildPage(XML_CCD,null,"note"),"No page: the XML view.");
			//300,000 nested elements (2 MB): no indentation, and only the first 1 MB.
			string xmlDeep="<ClinicalDocument>"+string.Concat(Enumerable.Repeat("<a>",300000))+string.Concat(Enumerable.Repeat("</a>",300000))
				+"</ClinicalDocument>";
			string pageDeep=FormEhrSummaryCcdEdit.BuildPage(xmlDeep,null,"too deep");
			StringAssert.Contains(pageDeep,"<p>too deep<br>Only the first part of the document&#39;s text is shown.</p>");
			StringAssert.Contains(pageDeep,"&lt;ClinicalDocument&gt;&lt;a&gt;&lt;a&gt;");
			Assert.IsTrue(pageDeep.Length<xmlDeep.Length*2,"Not indented.");
			string xmlDtd="<!DOCTYPE d [<!ENTITY a \"aaaa\">]><d>&a;</d>";
			StringAssert.Contains(FormEhrSummaryCcdEdit.BuildPage(xmlDtd,null,"dtd"),"&lt;!DOCTYPE d [&lt;!ENTITY a &quot;aaaa&quot;&gt;]&gt;&lt;d&gt;&amp;a;&lt;/d&gt;",
				"A DTD document is shown as it came, its entity not expanded.");
		}

		#region Cecil and source helpers
		private static ModuleDefinition ReadModule(string fileName) {
			string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,fileName);
			Assert.IsTrue(File.Exists(path),fileName+" is next to the tests (a project reference).");
			return ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(path)));
		}

		private static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition moduleDefinition) {
			Stack<TypeDefinition> stackTypes=new Stack<TypeDefinition>(moduleDefinition.Types);
			while(stackTypes.Count>0) {
				TypeDefinition typeDefinition=stackTypes.Pop();
				yield return typeDefinition;
				foreach(TypeDefinition typeDefinitionNested in typeDefinition.NestedTypes) {
					stackTypes.Push(typeDefinitionNested);
				}
			}
		}

		///<summary>EhrRenderChild itself, a type nested in it, or a compiler-generated type nested in either (lambdas and their closures).</summary>
		private static bool IsChildType(TypeDefinition typeDefinition) {
			while(typeDefinition!=null) {
				if(typeDefinition.FullName==TYPE_XSLT_CHILD) {
					return true;
				}
				typeDefinition=typeDefinition.DeclaringType;
			}
			return false;
		}

		private static bool IsXslt(TypeReference typeReference) {
			while(typeReference!=null) {
				if(typeReference.Namespace=="System.Xml.Xsl") {
					return true;
				}
				if(typeReference is GenericInstanceType genericInstanceType && genericInstanceType.GenericArguments.Any(IsXslt)) {
					return true;
				}
				typeReference=typeReference.IsNested ? typeReference.DeclaringType : (typeReference as TypeSpecification)?.ElementType;
			}
			return false;
		}

		///<summary>True when the method's signature, locals or IL use a type or member of System.Xml.Xsl.</summary>
		private static bool UsesXslt(MethodDefinition methodDefinition) {
			if(IsXslt(methodDefinition.ReturnType) || methodDefinition.Parameters.Any(x => IsXslt(x.ParameterType))) {
				return true;
			}
			if(!methodDefinition.HasBody) {
				return false;
			}
			if(methodDefinition.Body.Variables.Any(x => IsXslt(x.VariableType))) {
				return true;
			}
			foreach(Instruction instruction in methodDefinition.Body.Instructions) {
				switch(instruction.Operand) {
					case TypeReference typeReference when IsXslt(typeReference):
						return true;
					case MemberReference memberReference when IsXslt(memberReference.DeclaringType):
						return true;
					case MethodReference methodReference when IsXslt(methodReference.ReturnType) || methodReference.Parameters.Any(x => IsXslt(x.ParameterType)):
						return true;
					case FieldReference fieldReference when IsXslt(fieldReference.FieldType):
						return true;
				}
			}
			return false;
		}

		private static string GetDisplayCcdSource(string source) {
			int indexStart=source.IndexOf("public static bool DisplayCCD(string strXmlCCD,Patient patCur,string strAlterateFilPathXslCCD)");
			int indexEnd=source.IndexOf("public static int DeleteOldCcdFiles(",indexStart);
			Assert.IsTrue(indexStart>=0 && indexEnd>indexStart,"DisplayCCD and DeleteOldCcdFiles are found.");
			return source.Substring(indexStart,indexEnd-indexStart);
		}

		///<summary>The text of a class from its declaration to the next top-level declaration of the file (or the end).</summary>
		private static string GetClassSource(string source,string declaration) {
			int indexStart=source.IndexOf(declaration);
			Assert.IsTrue(indexStart>=0,declaration);
			Match match=Regex.Match(source.Substring(indexStart+declaration.Length),@"\n\t(public|internal) (class|interface|enum) ");
			return match.Success ? source.Substring(indexStart,declaration.Length+match.Index) : source.Substring(indexStart);
		}
		#endregion Cecil and source helpers

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
