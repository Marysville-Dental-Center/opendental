// The HTML pages the program builds itself for its browser windows (ODWebView2.NavigateToHtml): text from the database or the user, such as
// patient data or labels, is always encoded, so it shows as text and never becomes markup.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace CodeBase {
	///<summary>Builds the HTML pages the program generates itself.</summary>
	public static class HtmlPage {
		///<summary>text as HTML text: &amp;, &lt;, &gt;, quotes and apostrophes are encoded. Null gives "".</summary>
		public static string Encode(string text) {
			return WebUtility.HtmlEncode(text??"");
		}

		///<summary>text as HTML text that keeps its line breaks (each one becomes &lt;br&gt;).</summary>
		public static string EncodeWithLineBreaks(string text) {
			string encoded=Encode((text??"").Replace("\r\n","\n").Replace("\r","\n"));
			return encoded.Replace("\n","<br>");
		}

		///<summary>A whole page: UTF-8, the encoded title, and bodyHtml as it is (the caller encodes the text it puts in, with Encode). bodyStyle, when
		///given, is the body's style attribute (encoded here).</summary>
		public static string Build(string bodyHtml,string title="",string bodyStyle="") {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("<!DOCTYPE html><html><head><meta charset=\"UTF-8\">");
			stringBuilder.Append("<title>"+Encode(title)+"</title></head>");
			if(string.IsNullOrEmpty(bodyStyle)) {
				stringBuilder.Append("<body>");
			}
			else {
				stringBuilder.Append("<body style=\""+Encode(bodyStyle)+"\">");
			}
			stringBuilder.Append(bodyHtml??"");
			stringBuilder.Append("</body></html>");
			return stringBuilder.ToString();
		}

		///<summary>A page that shows text (encoded, line breaks kept) in a plain font.</summary>
		public static string BuildText(string text,string title="") {
			return Build("<p>"+EncodeWithLineBreaks(text)+"</p>",title,"font-family:sans-serif");
		}

		///<summary>A page that shows an XML document as text: indented (IndentXml), encoded, so neither its markup nor the text in it becomes markup
		///of the page, in a fixed-width font below note (encoded too) when one is given. Nothing of the document runs: no stylesheet is applied, the
		///program has no XSLT in its own process, because a stylesheet that recurses without end overflows the stack, which ends the program
		///whatever catches it, and one that loops never returns.</summary>
		public static string BuildXml(string xml,string note="",string title="") {
			StringBuilder stringBuilder=new StringBuilder();
			if(!string.IsNullOrEmpty(note)) {
				stringBuilder.Append("<p>"+EncodeWithLineBreaks(note)+"</p>");
			}
			stringBuilder.Append("<pre style=\"white-space:pre-wrap;word-wrap:break-word\">"+Encode(IndentXml(xml))+"</pre>");
			return Build(stringBuilder.ToString(),title,"font-family:sans-serif");
		}

		///<summary>xml with one element per line, indented two spaces per level, without its XML declaration. The document type declaration is
		///skipped, so no entity expands and nothing external is read. A document that cannot be read that way (not well-formed, or it uses an entity
		///of its document type) is returned as it is. Null gives "".</summary>
		public static string IndentXml(string xml) {
			if(string.IsNullOrWhiteSpace(xml)) {
				return xml??"";
			}
			XmlReaderSettings xmlReaderSettings=new XmlReaderSettings();
			xmlReaderSettings.DtdProcessing=DtdProcessing.Ignore;
			xmlReaderSettings.XmlResolver=null;
			xmlReaderSettings.IgnoreWhitespace=true;
			XmlWriterSettings xmlWriterSettings=new XmlWriterSettings();
			xmlWriterSettings.Indent=true;
			xmlWriterSettings.IndentChars="  ";
			xmlWriterSettings.OmitXmlDeclaration=true;
			try {
				using(XmlReader xmlReader=XmlReader.Create(new StringReader(xml),xmlReaderSettings))
				using(StringWriter stringWriter=new StringWriter()) {
					using(XmlWriter xmlWriter=XmlWriter.Create(stringWriter,xmlWriterSettings)) {
						xmlWriter.WriteNode(xmlReader,true);
					}
					return stringWriter.ToString();
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//Not well-formed, or an entity of the skipped document type: shown as it came.
				return xml;
			}
		}
	}
}
