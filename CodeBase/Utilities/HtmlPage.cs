// The HTML pages the program builds itself for its browser windows (ODWebView2.NavigateToHtml): text from the database or the user, such as
// patient data or labels, is always encoded, so it shows as text and never becomes markup.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Xsl;

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

		///<summary>Transforms xml with the XSLT stylesheet xsl into the HTML a browser shows (the browser itself no longer runs stylesheets). The
		///stylesheet may not run scripts, read other files (document(), xsl:import and xsl:include) or load external DTDs, so a stylesheet that came
		///with an email reaches nothing on this computer, and the text of the xml is encoded by the transform. Throws on invalid xml or xsl.</summary>
		public static string TransformXsl(string xml,string xsl) {
			XslCompiledTransform xslCompiledTransform=new XslCompiledTransform();
			using(XmlReader xmlReaderXsl=XmlReader.Create(new StringReader(xsl??""),GetReaderSettings())) {
				//XsltSettings.Default: no document() function and no embedded scripts. A null resolver: no xsl:import or xsl:include.
				xslCompiledTransform.Load(xmlReaderXsl,XsltSettings.Default,null);
			}
			using(XmlReader xmlReader=XmlReader.Create(new StringReader(xml??""),GetReaderSettings()))
			using(StringWriter stringWriter=new StringWriter()) {
				XmlWriterSettings xmlWriterSettings=xslCompiledTransform.OutputSettings.Clone();
				xmlWriterSettings.ConformanceLevel=ConformanceLevel.Auto;
				using(XmlWriter xmlWriter=XmlWriter.Create(stringWriter,xmlWriterSettings)) {
					xslCompiledTransform.Transform(xmlReader,null,xmlWriter,null);
				}
				return stringWriter.ToString();
			}
		}

		///<summary>An internal DTD subset is read (some stylesheets define entities there); nothing external is ever loaded, and entities may not
		///expand without limit.</summary>
		private static XmlReaderSettings GetReaderSettings() {
			XmlReaderSettings xmlReaderSettings=new XmlReaderSettings();
			xmlReaderSettings.DtdProcessing=DtdProcessing.Parse;
			xmlReaderSettings.XmlResolver=null;
			xmlReaderSettings.MaxCharactersFromEntities=1000000;
			return xmlReaderSettings;
		}
	}
}
