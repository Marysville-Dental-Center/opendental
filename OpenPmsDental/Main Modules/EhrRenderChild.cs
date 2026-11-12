// The child process that runs the CCD and CCR display stylesheets (docs/specs/ehr-rebuild-plan.md, package EHR-3; docs/port-notes/EHR-3.md).
// This is the only file of the program that may name an XSLT class (tests/LibWebTests, HtmlGenerationTests): XSLT never runs in the program's
// own process, only here, in a short-lived "OpenPmsDental.exe /ehr-render" that EhrStylesheetStarter starts. Licensed under the GNU General
// Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Xsl;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Runs the program's own stylesheets on documents its parent sends: a CCD (any CDA R2 document) with HL7's CDA.xsl and a CCR with the
	///AAFP's ccr.xsl, both embedded in the business library. ProgramEntry.Main calls Run first, before any window, database, ODInitialize or
	///Application call, and ends the process with its result. A stylesheet that recurses without end overflows the stack, and one that loops never
	///returns: either ends or stalls only this process, which its parent kills after 15 seconds. The transform reads only the four embedded files
	///(ResourceResolver), never a file, a share or the web, and no stylesheet that came with a document or an e-mail ever runs.
	///Frames on standard input: a kind byte (EhrStylesheetStarter.KIND_CCD or KIND_CCR), the length as 4 bytes (little-endian) and the document as
	///UTF-8. Replies on standard output: a status byte (STATUS_HTML or STATUS_FAILED), the length and the page or the reason as UTF-8. The end of the
	///input ends the process with 0.</summary>
	public static class EhrRenderChild {
		///<summary>The stack of the thread that runs a transform: the templates recurse with the document's depth (at most 200 levels).</summary>
		private const int STACK_BYTES=16*1024*1024;
		///<summary>The watchdog ends the process after this long without progress, so a child whose parent died does not linger.</summary>
		private const int WATCHDOG_MS=20000;
		private const uint SEM_FAILCRITICALERRORS=0x0001;
		private const uint SEM_NOGPFAULTERRORBOX=0x0002;
		///<summary>Environment.TickCount at the last progress: a frame read or a reply written.</summary>
		private static int _tickLastProgress;
		///<summary>Each stylesheet is compiled once per process, on its first document.</summary>
		private static XslCompiledTransform _xslCompiledTransformCcd;
		private static XslCompiledTransform _xslCompiledTransformCcr;

		[DllImport("kernel32.dll")]
		private static extern uint SetErrorMode(uint uMode);

		///<summary>The child's whole life. protocolVersion is the second command-line argument. Returns the exit code: 0 at the end of the input,
		///EhrStylesheetStarter.EXIT_BAD_ARGUMENTS or EXIT_BROKEN_FRAME otherwise. A crash shows no Windows error-report dialog.</summary>
		public static int Run(string protocolVersion) {
			SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX);
			if(protocolVersion!=EhrStylesheetStarter.PROTOCOL_VERSION) {
				return EhrStylesheetStarter.EXIT_BAD_ARGUMENTS;
			}
			//Standard output carries the frames only (Console.OpenStandardOutput below): nothing written to Console.Out may reach it.
			Console.SetOut(TextWriter.Null);
			MarkProgress();
			Thread threadWatchdog=new Thread(Watchdog);
			threadWatchdog.IsBackground=true;
			threadWatchdog.Start();
			try {
				using(Stream streamIn=Console.OpenStandardInput())
				using(Stream streamOut=Console.OpenStandardOutput()) {
					Stream streamFrames=SkipUtf8Preamble(streamIn);
					if(streamFrames==null) {
						return EhrStylesheetStarter.EXIT_BROKEN_FRAME;
					}
					while(true) {
						byte[] arrayDocument=EhrStylesheetStarter.ReadFrame(streamFrames,EhrStylesheetStarter.MAX_DOCUMENT_BYTES,out byte kind);
						if(arrayDocument==null) {
							return 0;//The end of the input.
						}
						if(kind!=EhrStylesheetStarter.KIND_CCD && kind!=EhrStylesheetStarter.KIND_CCR) {
							return EhrStylesheetStarter.EXIT_BROKEN_FRAME;
						}
						MarkProgress();
						byte status;
						byte[] arrayReply=RenderOnLargeStack(kind,arrayDocument,out status);
						EhrStylesheetStarter.WriteFrame(streamOut,status,arrayReply);
						MarkProgress();
					}
				}
			}
			catch(Exception ex) {
				Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);
				return EhrStylesheetStarter.EXIT_BROKEN_FRAME;
			}
		}

		private static void MarkProgress() {
			Interlocked.Exchange(ref _tickLastProgress,Environment.TickCount);
		}

		///<summary>Ends the process after 20 seconds without progress. Its parent kills it after 15 seconds on one document, so this happens only
		///when the parent is gone while a transform stalls.</summary>
		private static void Watchdog() {
			while(true) {
				Thread.Sleep(1000);
				if(unchecked(Environment.TickCount-Interlocked.CompareExchange(ref _tickLastProgress,0,0))>WATCHDOG_MS) {
					Environment.Exit(EhrStylesheetStarter.EXIT_WATCHDOG);
				}
			}
		}

		///<summary>The stream the frames are read from. On a computer whose code page is UTF-8, .NET Framework's Process.StandardInput writes a UTF-8
		///byte order mark before the first frame. Kind bytes are 1 and 2, so a leading EF BB BF can only be that mark, and it is skipped. Null when
		///the input starts with EF but not with the whole mark (a broken frame).</summary>
		private static Stream SkipUtf8Preamble(Stream streamIn) {
			int first=streamIn.ReadByte();
			if(first==-1) {
				return streamIn;//No input: the first frame read finds the end.
			}
			if(first!=0xEF) {
				return new PrefixedStream(new byte[] { (byte)first },streamIn);
			}
			if(streamIn.ReadByte()==0xBB && streamIn.ReadByte()==0xBF) {
				return streamIn;
			}
			return null;
		}

		///<summary>Runs the transform on a thread with a 16 MB stack and waits for it. A stack overflow there ends the process; any other failure
		///becomes the reason, with status STATUS_FAILED.</summary>
		private static byte[] RenderOnLargeStack(byte kind,byte[] arrayDocument,out byte status) {
			byte statusThread=EhrStylesheetStarter.STATUS_FAILED;
			byte[] arrayThread=null;
			Thread thread=new Thread(() => {
				try {
					arrayThread=Render(kind,arrayDocument);
					statusThread=EhrStylesheetStarter.STATUS_HTML;
				}
				catch(Exception ex) {
					arrayThread=new UTF8Encoding(false).GetBytes(ex.Message);
					statusThread=EhrStylesheetStarter.STATUS_FAILED;
				}
			},STACK_BYTES);
			thread.Start();
			thread.Join();
			status=statusThread;
			return arrayThread??new byte[0];
		}

		///<summary>The page of one document, as UTF-8 without a byte order mark. The document is decoded as UTF-8 and read as text, so its declared
		///encoding is ignored and no code page is needed. Throws with the reason when the document is refused or the transform fails.</summary>
		private static byte[] Render(byte kind,byte[] arrayDocument) {
			string text=new UTF8Encoding(false,true).GetString(arrayDocument);
			if(text.Length>0 && text[0]=='﻿') {
				text=text.Substring(1);
			}
			//The parent checked the limits already; this process checks them again before any template runs.
			string reasonLimits=EhrStylesheetStarter.GetLimitError(text);
			if(reasonLimits!=null) {
				throw new InvalidDataException(reasonLimits);
			}
			bool isCcr=kind==EhrStylesheetStarter.KIND_CCR;
			XslCompiledTransform xslCompiledTransform=GetTransform(isCcr);
			XsltArgumentList xsltArgumentList=new XsltArgumentList();
			//Without a handler, XslCompiledTransform writes xsl:message text to the console, which is the frames' channel. Both stylesheets send
			//messages (CDA.xsl about a reference it refuses, ccr.xsl about a date pattern); they are dropped.
			xsltArgumentList.XsltMessageEncountered+=(sender,e) => { };
			if(!isCcr) {
				//CDA-core-xsl's security parameters: no external image, no PDF, no script (its default is true). Upstream issue #41 says the image
				//parameters are not used by the current code, whose sanitizer drops any URL with a colon; they are set anyway, and the tests check the
				//output.
				xsltArgumentList.AddParam("limit-external-images","","yes");
				xsltArgumentList.AddParam("external-image-whitelist","","");
				xsltArgumentList.AddParam("limit-pdf","","yes");
				xsltArgumentList.AddParam("useJavascript","","false");
			}
			//The reader the limits were counted with (no DTD, no resolver, characters checked), and at most 32 M characters.
			XmlReaderSettings xmlReaderSettings=EhrStylesheetStarter.GetXmlReaderSettings();
			xmlReaderSettings.MaxCharactersInDocument=EhrStylesheetStarter.MAX_DOCUMENT_BYTES;
			XmlWriterSettings xmlWriterSettings=xslCompiledTransform.OutputSettings.Clone();
			xmlWriterSettings.Encoding=new UTF8Encoding(false);
			xmlWriterSettings.CloseOutput=false;
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(XmlReader xmlReader=XmlReader.Create(new StringReader(text),xmlReaderSettings))
				using(XmlWriter xmlWriter=XmlWriter.Create(memoryStream,xmlWriterSettings))
				//ccr.xsl as upstream released it copied five Description/Text fields with disable-output-escaping, so a received CCR's text became
				//markup: the copy shipped has none, and the CCR's writer writes such raw text as escaped text anyway. CDA.xsl uses the attribute once,
				//on its own CSS text, so the CCD's raw text is written as it is. Both writers drop an image or other source a page would load by
				//itself unless it is a data: image: CDA.xsl's sanitizer drops a URL with a colon, but a share path (\\server\share\x.png) has none.
				//Both drop a link unless it goes to the page (#), the web or an e-mail address: ccr.xsl writes a CCR's reference locations as links.
				using(XmlWriter xmlWriterPage=new PageXmlWriter(xmlWriter,isRawEscaped:isCcr)) {
					xslCompiledTransform.Transform(xmlReader,xsltArgumentList,xmlWriterPage,new ResourceResolver());
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>The compiled stylesheet, loaded once from an XmlReader whose base URI is ehr-res:///CDA.xsl or ehr-res:///ccr.xsl, with document()
		///on (CDA.xsl reads its two companions, ccr.xsl its own date tables with document('')) and scripts off.</summary>
		private static XslCompiledTransform GetTransform(bool isCcr) {
			XslCompiledTransform xslCompiledTransform=isCcr ? _xslCompiledTransformCcr : _xslCompiledTransformCcd;
			if(xslCompiledTransform!=null) {
				return xslCompiledTransform;
			}
			string fileName=isCcr ? EhrStylesheetStarter.FILE_CCR_XSL : EhrStylesheetStarter.FILE_CDA_XSL;
			XmlReaderSettings xmlReaderSettings=new XmlReaderSettings();
			xmlReaderSettings.DtdProcessing=DtdProcessing.Prohibit;
			xmlReaderSettings.XmlResolver=null;
			xslCompiledTransform=new XslCompiledTransform();
			using(XmlReader xmlReader=XmlReader.Create(new StringReader(EhrStylesheetStarter.GetResourceText(fileName)),xmlReaderSettings,
				ResourceResolver.GetUri(fileName).AbsoluteUri))
			{
				xslCompiledTransform.Load(xmlReader,new XsltSettings(enableDocumentFunction:true,enableScript:false),new ResourceResolver());
			}
			if(isCcr) {
				_xslCompiledTransformCcr=xslCompiledTransform;
			}
			else {
				_xslCompiledTransformCcd=xslCompiledTransform;
			}
			return xslCompiledTransform;
		}

		///<summary>Serves exactly the four embedded files under ehr-res:///CDA.xsl, cda_l10n.xml, cda_narrativeblock.xml and ccr.xsl, and refuses every
		///other URI: a file, a share, the web, another name. It is passed to Load and to Transform, because document() resolves through the
		///resolver given to Transform.</summary>
		public class ResourceResolver:XmlResolver {
			public override ICredentials Credentials {
				set {
				}
			}

			///<summary>The URI a file is served under.</summary>
			public static Uri GetUri(string fileName) {
				return new Uri(EhrStylesheetStarter.URI_SCHEME+":///"+fileName);
			}

			///<summary>The text served for uri; null when it is not one of the four.</summary>
			public static string GetText(Uri uri) {
				if(uri==null || !uri.IsAbsoluteUri || uri.Scheme!=EhrStylesheetStarter.URI_SCHEME || uri.Host!="" || uri.Query!="" || uri.Fragment!="") {
					return null;
				}
				foreach(string fileName in new[] { EhrStylesheetStarter.FILE_CDA_XSL,EhrStylesheetStarter.FILE_CDA_L10N,
					EhrStylesheetStarter.FILE_CDA_NARRATIVEBLOCK,EhrStylesheetStarter.FILE_CCR_XSL })
				{
					if(uri.AbsolutePath=="/"+fileName) {
						return EhrStylesheetStarter.GetResourceText(fileName);
					}
				}
				return null;
			}

			///<summary>An absolute relativeUri stays as it is, so GetEntity refuses it unless it is one of the four. A relative one resolves against
			///baseUri, or against ehr-res:/// when there is none.</summary>
			public override Uri ResolveUri(Uri baseUri,string relativeUri) {
				if(Uri.TryCreate(relativeUri??"",UriKind.Absolute,out Uri uriAbsolute)) {
					return uriAbsolute;
				}
				if(baseUri==null || !baseUri.IsAbsoluteUri) {
					baseUri=new Uri(EhrStylesheetStarter.URI_SCHEME+":///");
				}
				return new Uri(baseUri,relativeUri??"");
			}

			public override object GetEntity(Uri absoluteUri,string role,Type ofObjectToReturn) {
				string text=GetText(absoluteUri);
				if(text==null) {
					throw new XmlException("Only the program's own stylesheet files can be read; refused: "+absoluteUri);
				}
				if(ofObjectToReturn!=null && ofObjectToReturn!=typeof(Stream) && ofObjectToReturn!=typeof(object)) {
					throw new XmlException("Unsupported entity type: "+ofObjectToReturn.Name);
				}
				return new MemoryStream(new UTF8Encoding(false).GetBytes(text),false);
			}
		}

		///<summary>The writer of the pages: writes through to another writer, with three exceptions. With isRawEscaped (a CCR), text the stylesheet
		///asks to write unescaped (disable-output-escaping, which reaches a writer as WriteRaw) is written escaped, as text, so a received document's
		///text never becomes markup of the page. An attribute through which a page would load something by itself (src, data, background, an href
		///of any element but a and area, ...) is dropped unless its value is a data: image, so a received document's image or object never comes
		///from a file, a share or the web. And a link (the href of a or area, a cite) is dropped unless it goes to a place in the page (#), to the
		///web (http://, https://) or to an e-mail address (mailto:), so the page has no javascript:, vbscript:, file: or share link, whatever the
		///stylesheet copied from the document (ccr.xsl writes a CCR's reference locations as links, unchecked); the CCD window opens only http,
		///https and mailto links anyway, in the default browser.</summary>
		public class PageXmlWriter:XmlWriter {
			///<summary>The attributes through which a browser loads something by itself, by their local name, whatever their prefix (xlink:href is
			///href below).</summary>
			private static readonly string[] _arrayLoadingAttributes={ "src","data","background","poster","srcset","lowsrc","dynsrc","codebase",
				"archive","classid","longdesc","manifest","formaction","action","ping" };
			///<summary>The elements whose href is a link, which loads nothing until it is clicked. Any other href (link, image, use, base, an xlink:href)
			///loads by itself.</summary>
			private static readonly string[] _arrayLinkElements={ "a","area" };
			///<summary>The beginnings a link may have, read as a browser reads it (IsAllowed).</summary>
			private static readonly string[] _arrayLinkStarts={ "#","http://","https://","mailto:" };
			private readonly XmlWriter _xmlWriter;
			private readonly bool _isRawEscaped;
			///<summary>The local name of the element whose start tag is being written, for its attributes.</summary>
			private string _elementLocalName="";
			///<summary>The attribute being held back until its value is known, or null.</summary>
			private string[] _arrayAttributeHeld;
			private StringBuilder _stringBuilderValueHeld;

			public PageXmlWriter(XmlWriter xmlWriter,bool isRawEscaped) {
				_xmlWriter=xmlWriter;
				_isRawEscaped=isRawEscaped;
			}

			///<summary>True when an attribute of this name (prefix, local name and namespace URI), on an element of this local name, may hold this
			///value: any value for an attribute that loads nothing and is no link; only a data: image for one that loads; only #, http://, https:// or
			///mailto: for a link. A value is read as a browser reads a URL: control characters and spaces dropped, any case.</summary>
			public static bool IsAllowed(string elementLocalName,string prefix,string localName,string ns,string value) {
				AttributeKind attributeKind=GetAttributeKind(elementLocalName,prefix,localName,ns);
				if(attributeKind==AttributeKind.Other) {
					return true;
				}
				StringBuilder stringBuilder=new StringBuilder();
				foreach(char c in value??"") {
					if(c>' ') {
						stringBuilder.Append(char.ToLowerInvariant(c));
					}
				}
				string valueRead=stringBuilder.ToString();
				if(attributeKind==AttributeKind.Loading) {
					return valueRead.StartsWith("data:image/");
				}
				foreach(string start in _arrayLinkStarts) {
					if(valueRead.StartsWith(start)) {
						return true;
					}
				}
				return false;
			}

			public override WriteState WriteState {
				get {
					return _xmlWriter.WriteState;
				}
			}

			public override XmlWriterSettings Settings {
				get {
					return _xmlWriter.Settings;
				}
			}

			public override string XmlLang {
				get {
					return _xmlWriter.XmlLang;
				}
			}

			public override XmlSpace XmlSpace {
				get {
					return _xmlWriter.XmlSpace;
				}
			}

			///<summary>What an attribute of this name does on an element of this local name. Names are compared in any case.</summary>
			private static AttributeKind GetAttributeKind(string elementLocalName,string prefix,string localName,string ns) {
				foreach(string nameLoading in _arrayLoadingAttributes) {
					if(string.Equals(localName,nameLoading,StringComparison.OrdinalIgnoreCase)) {
						return AttributeKind.Loading;
					}
				}
				if(string.Equals(localName,"href",StringComparison.OrdinalIgnoreCase)) {
					if(!string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(ns)) {
						return AttributeKind.Loading;//xlink:href and the like.
					}
					foreach(string elementLink in _arrayLinkElements) {
						if(string.Equals(elementLocalName,elementLink,StringComparison.OrdinalIgnoreCase)) {
							return AttributeKind.Link;
						}
					}
					return AttributeKind.Loading;
				}
				if(string.Equals(localName,"cite",StringComparison.OrdinalIgnoreCase)) {
					return AttributeKind.Link;
				}
				return AttributeKind.Other;
			}

			///<summary>What an attribute does, for the writer's checks.</summary>
			private enum AttributeKind {
				///<summary>Neither loads nor links: written as it is.</summary>
				Other,
				///<summary>The page loads its value by itself: kept only for a data: image.</summary>
				Loading,
				///<summary>A link: kept only to the page, the web or an e-mail address.</summary>
				Link,
			}

			public override void WriteStartAttribute(string prefix,string localName,string ns) {
				if(GetAttributeKind(_elementLocalName,prefix,localName,ns)!=AttributeKind.Other) {
					_arrayAttributeHeld=new string[] { prefix,localName,ns };
					_stringBuilderValueHeld=new StringBuilder();
					return;
				}
				_xmlWriter.WriteStartAttribute(prefix,localName,ns);
			}

			public override void WriteEndAttribute() {
				if(_arrayAttributeHeld==null) {
					_xmlWriter.WriteEndAttribute();
					return;
				}
				string[] arrayAttribute=_arrayAttributeHeld;
				string value=_stringBuilderValueHeld.ToString();
				_arrayAttributeHeld=null;
				_stringBuilderValueHeld=null;
				if(!IsAllowed(_elementLocalName,arrayAttribute[0],arrayAttribute[1],arrayAttribute[2],value)) {
					return;//Dropped: the page would load it by itself, or a link that is not to the page, the web or an e-mail address.
				}
				_xmlWriter.WriteStartAttribute(arrayAttribute[0],arrayAttribute[1],arrayAttribute[2]);
				_xmlWriter.WriteString(value);
				_xmlWriter.WriteEndAttribute();
			}

			public override void WriteString(string text) {
				if(_arrayAttributeHeld!=null) {
					_stringBuilderValueHeld.Append(text);
					return;
				}
				_xmlWriter.WriteString(text);
			}

			public override void WriteRaw(string data) {
				if(_arrayAttributeHeld!=null) {
					_stringBuilderValueHeld.Append(data);
					return;
				}
				if(_isRawEscaped) {
					_xmlWriter.WriteString(data);
					return;
				}
				_xmlWriter.WriteRaw(data);
			}

			public override void WriteRaw(char[] buffer,int index,int count) {
				WriteRaw(new string(buffer,index,count));
			}

			public override void WriteChars(char[] buffer,int index,int count) {
				WriteString(new string(buffer,index,count));
			}

			public override void WriteCharEntity(char ch) {
				if(_arrayAttributeHeld!=null) {
					_stringBuilderValueHeld.Append(ch);
					return;
				}
				_xmlWriter.WriteCharEntity(ch);
			}

			public override void WriteSurrogateCharEntity(char lowChar,char highChar) {
				if(_arrayAttributeHeld!=null) {
					_stringBuilderValueHeld.Append(highChar).Append(lowChar);
					return;
				}
				_xmlWriter.WriteSurrogateCharEntity(lowChar,highChar);
			}

			public override void WriteWhitespace(string ws) {
				if(_arrayAttributeHeld!=null) {
					_stringBuilderValueHeld.Append(ws);
					return;
				}
				_xmlWriter.WriteWhitespace(ws);
			}

			public override void WriteEntityRef(string name) {
				if(_arrayAttributeHeld!=null) {
					_stringBuilderValueHeld.Append("&"+name+";");
					return;
				}
				_xmlWriter.WriteEntityRef(name);
			}

			public override void WriteStartDocument() {
				_xmlWriter.WriteStartDocument();
			}

			public override void WriteStartDocument(bool standalone) {
				_xmlWriter.WriteStartDocument(standalone);
			}

			public override void WriteEndDocument() {
				_xmlWriter.WriteEndDocument();
			}

			public override void WriteDocType(string name,string pubid,string sysid,string subset) {
				_xmlWriter.WriteDocType(name,pubid,sysid,subset);
			}

			public override void WriteStartElement(string prefix,string localName,string ns) {
				_elementLocalName=localName??"";
				_xmlWriter.WriteStartElement(prefix,localName,ns);
			}

			public override void WriteEndElement() {
				_xmlWriter.WriteEndElement();
			}

			public override void WriteFullEndElement() {
				_xmlWriter.WriteFullEndElement();
			}

			public override void WriteCData(string text) {
				_xmlWriter.WriteCData(text);
			}

			public override void WriteComment(string text) {
				_xmlWriter.WriteComment(text);
			}

			public override void WriteProcessingInstruction(string name,string text) {
				_xmlWriter.WriteProcessingInstruction(name,text);
			}

			public override void WriteBase64(byte[] buffer,int index,int count) {
				_xmlWriter.WriteBase64(buffer,index,count);
			}

			public override void Flush() {
				_xmlWriter.Flush();
			}

			public override string LookupPrefix(string ns) {
				return _xmlWriter.LookupPrefix(ns);
			}

			///<summary>Flushes the writer it writes to; the caller closes that one.</summary>
			protected override void Dispose(bool disposing) {
				if(disposing) {
					_xmlWriter.Flush();
				}
				base.Dispose(disposing);
			}
		}

		///<summary>A few bytes already read, then the rest of a stream. Disposing it leaves the stream open.</summary>
		private class PrefixedStream:Stream {
			private readonly byte[] _arrayPrefix;
			private int _indexPrefix;
			private readonly Stream _stream;

			public PrefixedStream(byte[] arrayPrefix,Stream stream) {
				_arrayPrefix=arrayPrefix;
				_stream=stream;
			}

			public override int Read(byte[] buffer,int offset,int count) {
				if(_indexPrefix<_arrayPrefix.Length) {
					int countPrefix=Math.Min(count,_arrayPrefix.Length-_indexPrefix);
					Array.Copy(_arrayPrefix,_indexPrefix,buffer,offset,countPrefix);
					_indexPrefix+=countPrefix;
					return countPrefix;
				}
				return _stream.Read(buffer,offset,count);
			}

			public override bool CanRead {
				get {
					return true;
				}
			}

			public override bool CanSeek {
				get {
					return false;
				}
			}

			public override bool CanWrite {
				get {
					return false;
				}
			}

			public override long Length {
				get {
					throw new NotSupportedException();
				}
			}

			public override long Position {
				get {
					throw new NotSupportedException();
				}
				set {
					throw new NotSupportedException();
				}
			}

			public override void Flush() {
			}

			public override long Seek(long offset,SeekOrigin origin) {
				throw new NotSupportedException();
			}

			public override void SetLength(long value) {
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer,int offset,int count) {
				throw new NotSupportedException();
			}
		}
	}
}
