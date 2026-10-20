using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OpenDentBusiness {
	///<summary>Fills the merge fields of a letter merge template that is stored in the Office Open XML word processing format (.docx, .dotx, .docm,
	///.dotm), without needing a word processor. Used for chart letters. The result opens in any program that reads .docx files. Older binary
	///templates (.doc, .dot) need a word processor to merge and are not handled here.</summary>
	public static class DocxMailMerge {
		private static readonly XNamespace _w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
		private static readonly XNamespace _contentTypes="http://schemas.openxmlformats.org/package/2006/content-types";
		private static readonly XNamespace _packageRels="http://schemas.openxmlformats.org/package/2006/relationships";
		private static readonly XNamespace _xml="http://www.w3.org/XML/1998/namespace";
		private const string CONTENT_TYPE_DOCUMENT="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
		private const string CONTENT_TYPE_TEMPLATE="application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";
		private const string CONTENT_TYPE_DOCUMENT_MACRO="application/vnd.ms-word.document.macroEnabled.main+xml";
		private const string CONTENT_TYPE_TEMPLATE_MACRO="application/vnd.ms-word.template.macroEnabledTemplate.main+xml";
		///<summary>The parts of a word processing package whose text can hold merge fields.</summary>
		private static readonly Regex _regexTextPart=new Regex(@"^word/(document|header\d*|footer\d*|footnotes|endnotes)\.xml$",RegexOptions.IgnoreCase);
		private static readonly Regex _regexMergeField=new Regex(@"^\s*MERGEFIELD\s+(?:""(?<name>[^""]*)""|(?<name>[^\s\\]+))(?<switches>.*)$",
			RegexOptions.IgnoreCase|RegexOptions.Singleline);
		private static readonly Regex _regexSwitch=new Regex(@"\\(?<switch>[bf*])\s*(?:""(?<arg>[^""]*)""|(?<arg>[^\s\\]+))",RegexOptions.IgnoreCase);

		///<summary>True if the file name has one of the extensions this class can merge.</summary>
		public static bool IsSupportedTemplate(string fileName) {
			string ext=Path.GetExtension(fileName??"").ToLowerInvariant();
			return ext==".docx" || ext==".dotx" || ext==".docm" || ext==".dotm";
		}

		///<summary>The extension of a document merged from this template: .docm for macro-enabled templates, otherwise .docx.</summary>
		public static string GetOutputExtension(string templateFileName) {
			string ext=Path.GetExtension(templateFileName??"").ToLowerInvariant();
			if(ext==".docm" || ext==".dotm") {
				return ".docm";
			}
			return ".docx";
		}

		///<summary>Returns a new document: a copy of the template with every MERGEFIELD replaced by its value (field names are not case
		///sensitive), the mail merge data source removed, and a template turned into an ordinary document. Fields whose names are not in
		///dictValues are left empty and listed in listUnknownFields. Throws if the bytes are not a word processing package.</summary>
		public static byte[] Merge(byte[] templateBytes,IDictionary<string,string> dictValues,out List<string> listUnknownFields) {
			Dictionary<string,string> dictLookup=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			if(dictValues!=null) {
				foreach(KeyValuePair<string,string> pair in dictValues) {
					dictLookup[pair.Key]=pair.Value??"";
				}
			}
			HashSet<string> hashSetUnknown=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<ZipPart> listParts=ReadParts(templateBytes);
			if(!listParts.Any(x => x.Name.Equals("word/document.xml",StringComparison.OrdinalIgnoreCase))) {
				throw new ApplicationException(Lans.g("ChartLetters","The template is not a word processing document."));
			}
			for(int i=0;i<listParts.Count;i++) {
				ZipPart zipPart=listParts[i];
				if(_regexTextPart.IsMatch(zipPart.Name)) {
					XDocument xDocument=LoadXml(zipPart.Bytes);
					MergeFieldsInPart(xDocument,dictLookup,hashSetUnknown);
					zipPart.Bytes=SaveXml(xDocument);
				}
				else if(zipPart.Name.Equals("word/settings.xml",StringComparison.OrdinalIgnoreCase)) {
					XDocument xDocument=LoadXml(zipPart.Bytes);
					xDocument.Descendants(_w+"mailMerge").ToList().ForEach(x => x.Remove());
					zipPart.Bytes=SaveXml(xDocument);
				}
				else if(zipPart.Name.Equals("word/_rels/settings.xml.rels",StringComparison.OrdinalIgnoreCase)) {
					XDocument xDocument=LoadXml(zipPart.Bytes);
					xDocument.Descendants(_packageRels+"Relationship")
						.Where(x => ((string)x.Attribute("Type")??"").IndexOf("mailMerge",StringComparison.OrdinalIgnoreCase)>=0)
						.ToList().ForEach(x => x.Remove());
					zipPart.Bytes=SaveXml(xDocument);
				}
				else if(zipPart.Name=="[Content_Types].xml") {
					XDocument xDocument=LoadXml(zipPart.Bytes);
					foreach(XElement xElementOverride in xDocument.Descendants(_contentTypes+"Override")) {
						string contentType=(string)xElementOverride.Attribute("ContentType");
						if(contentType==CONTENT_TYPE_TEMPLATE) {
							xElementOverride.SetAttributeValue("ContentType",CONTENT_TYPE_DOCUMENT);
						}
						else if(contentType==CONTENT_TYPE_TEMPLATE_MACRO) {
							xElementOverride.SetAttributeValue("ContentType",CONTENT_TYPE_DOCUMENT_MACRO);
						}
					}
					zipPart.Bytes=SaveXml(xDocument);
				}
			}
			listUnknownFields=hashSetUnknown.OrderBy(x => x).ToList();
			return WriteParts(listParts);
		}

		///<summary>The names of the merge fields in the document, in order of first use, without duplicates.</summary>
		public static List<string> GetFieldNames(byte[] documentBytes) {
			List<string> listNames=new List<string>();
			foreach(ZipPart zipPart in ReadParts(documentBytes).Where(x => _regexTextPart.IsMatch(x.Name))) {
				XDocument xDocument=LoadXml(zipPart.Bytes);
				List<string> listInstructions=xDocument.Descendants(_w+"fldSimple").Select(x => (string)x.Attribute(_w+"instr")).ToList();
				listInstructions.AddRange(ScanComplexFields(xDocument.Descendants(_w+"r").ToList()).Select(x => x.Instruction.ToString()));
				foreach(string instruction in listInstructions) {
					MergeFieldInfo mergeFieldInfo=ParseInstruction(instruction);
					if(mergeFieldInfo!=null && !listNames.Contains(mergeFieldInfo.Name,StringComparer.OrdinalIgnoreCase)) {
						listNames.Add(mergeFieldInfo.Name);
					}
				}
			}
			return listNames;
		}

		///<summary>The visible text of the main document part: one line per paragraph, without field codes. Used to preview and test merges.</summary>
		public static string GetDocumentText(byte[] documentBytes) {
			ZipPart zipPart=ReadParts(documentBytes).FirstOrDefault(x => x.Name.Equals("word/document.xml",StringComparison.OrdinalIgnoreCase));
			if(zipPart==null) {
				return "";
			}
			XDocument xDocument=LoadXml(zipPart.Bytes);
			List<string> listLines=new List<string>();
			foreach(XElement xElementPara in xDocument.Descendants(_w+"p")) {
				StringBuilder stringBuilder=new StringBuilder();
				foreach(XElement xElement in xElementPara.Descendants()) {
					if(xElement.Name==_w+"t") {
						stringBuilder.Append(xElement.Value);
					}
					else if(xElement.Name==_w+"br" || xElement.Name==_w+"cr") {
						stringBuilder.Append("\n");
					}
					else if(xElement.Name==_w+"tab") {
						stringBuilder.Append("\t");
					}
				}
				listLines.Add(stringBuilder.ToString());
			}
			return string.Join("\n",listLines);
		}

		///<summary>A new, minimal .docx template: an optional heading paragraph, then one paragraph per field with the field's name and a
		///MERGEFIELD for it. Used to start a template without a word processor.</summary>
		public static byte[] CreateTemplate(string heading,IEnumerable<string> listFieldNames) {
			XElement xElementBody=new XElement(_w+"body");
			if(!string.IsNullOrEmpty(heading)) {
				xElementBody.Add(new XElement(_w+"p",MakeRuns(heading,null,false)));
			}
			foreach(string fieldName in listFieldNames??new List<string>()) {
				xElementBody.Add(new XElement(_w+"p",
					MakeRuns(fieldName+": ",null,false),
					new XElement(_w+"fldSimple",new XAttribute(_w+"instr"," MERGEFIELD "+fieldName+" "),
						MakeRuns("\u00AB"+fieldName+"\u00BB",null,false))));
			}
			xElementBody.Add(new XElement(_w+"sectPr"));
			XDocument xDocumentMain=new XDocument(new XDeclaration("1.0","UTF-8","yes"),
				new XElement(_w+"document",new XAttribute(XNamespace.Xmlns+"w",_w.NamespaceName),xElementBody));
			XDocument xDocumentTypes=new XDocument(new XDeclaration("1.0","UTF-8","yes"),
				new XElement(_contentTypes+"Types",
					new XElement(_contentTypes+"Default",new XAttribute("Extension","rels"),
						new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),
					new XElement(_contentTypes+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),
					new XElement(_contentTypes+"Override",new XAttribute("PartName","/word/document.xml"),
						new XAttribute("ContentType",CONTENT_TYPE_DOCUMENT))));
			XDocument xDocumentRels=new XDocument(new XDeclaration("1.0","UTF-8","yes"),
				new XElement(_packageRels+"Relationships",
					new XElement(_packageRels+"Relationship",new XAttribute("Id","rId1"),
						new XAttribute("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
						new XAttribute("Target","word/document.xml"))));
			List<ZipPart> listParts=new List<ZipPart>();
			listParts.Add(new ZipPart("[Content_Types].xml",SaveXml(xDocumentTypes)));
			listParts.Add(new ZipPart("_rels/.rels",SaveXml(xDocumentRels)));
			listParts.Add(new ZipPart("word/document.xml",SaveXml(xDocumentMain)));
			return WriteParts(listParts);
		}

		#region Fields
		///<summary>Replaces the MERGEFIELDs of one part, simple fields first, then fields made of field characters.</summary>
		private static void MergeFieldsInPart(XDocument xDocument,Dictionary<string,string> dictLookup,HashSet<string> hashSetUnknown) {
			//Innermost simple fields first, so an outer field never holds an element that was already replaced.
			List<XElement> listSimpleFields=xDocument.Descendants(_w+"fldSimple").Reverse().ToList();
			foreach(XElement xElementField in listSimpleFields) {
				if(xElementField.Parent==null) {
					continue;
				}
				MergeFieldInfo mergeFieldInfo=ParseInstruction((string)xElementField.Attribute(_w+"instr"));
				if(mergeFieldInfo==null) {
					continue;
				}
				string value=GetValue(mergeFieldInfo,dictLookup,hashSetUnknown);
				XElement xElementRunProps=xElementField.Descendants(_w+"rPr").FirstOrDefault();
				xElementField.ReplaceWith(MakeRuns(value,xElementRunProps,false));
			}
			List<XElement> listRuns=xDocument.Descendants(_w+"r").ToList();
			List<FieldFrame> listFrames=ScanComplexFields(listRuns);
			//Last field first, so an inner field is replaced before any field around it.
			int idxReplacedStart=int.MaxValue;
			foreach(FieldFrame fieldFrame in listFrames.OrderByDescending(x => x.IdxBegin)) {
				MergeFieldInfo mergeFieldInfo=ParseInstruction(fieldFrame.Instruction.ToString());
				if(mergeFieldInfo==null) {
					continue;
				}
				if(fieldFrame.IdxEnd>=idxReplacedStart) {
					continue;//A merge field that contains a merge field: not meaningful, leave it.
				}
				List<XElement> listRunsField=listRuns.GetRange(fieldFrame.IdxBegin,fieldFrame.IdxEnd-fieldFrame.IdxBegin+1);
				XElement xElementPara=listRunsField[0].Ancestors(_w+"p").FirstOrDefault();
				if(listRunsField.Any(x => x.Ancestors(_w+"p").FirstOrDefault()!=xElementPara)) {
					continue;//A field that crosses paragraphs is left as it is.
				}
				string value=GetValue(mergeFieldInfo,dictLookup,hashSetUnknown);
				XElement xElementRunProps=listRunsField[0].Element(_w+"rPr");
				if(fieldFrame.IdxSeparate>=0 && fieldFrame.IdxSeparate+1<fieldFrame.IdxEnd) {
					xElementRunProps=listRuns[fieldFrame.IdxSeparate+1].Element(_w+"rPr")??xElementRunProps;
				}
				listRunsField[0].AddBeforeSelf(MakeRuns(value,xElementRunProps,fieldFrame.IsInOuterInstruction));
				listRunsField.ForEach(x => x.Remove());
				idxReplacedStart=fieldFrame.IdxBegin;
			}
		}

		///<summary>Finds every field made of begin, separate and end field characters in the runs, which are in document order.</summary>
		private static List<FieldFrame> ScanComplexFields(List<XElement> listRuns) {
			List<FieldFrame> listFrames=new List<FieldFrame>();
			Stack<FieldFrame> stackOpen=new Stack<FieldFrame>();
			for(int i=0;i<listRuns.Count;i++) {
				foreach(XElement xElement in listRuns[i].Elements()) {
					if(xElement.Name==_w+"fldChar") {
						string type=(string)xElement.Attribute(_w+"fldCharType");
						if(type=="begin") {
							FieldFrame fieldFrame=new FieldFrame();
							fieldFrame.IdxBegin=i;
							fieldFrame.IsInOuterInstruction=stackOpen.Count>0 && stackOpen.Peek().IdxSeparate<0;
							stackOpen.Push(fieldFrame);
						}
						else if(type=="separate" && stackOpen.Count>0) {
							stackOpen.Peek().IdxSeparate=i;
						}
						else if(type=="end" && stackOpen.Count>0) {
							FieldFrame fieldFrame=stackOpen.Pop();
							fieldFrame.IdxEnd=i;
							listFrames.Add(fieldFrame);
						}
					}
					else if(xElement.Name==_w+"instrText" && stackOpen.Count>0 && stackOpen.Peek().IdxSeparate<0) {
						stackOpen.Peek().Instruction.Append(xElement.Value);
					}
				}
			}
			return listFrames;
		}

		///<summary>Null if the instruction is not a MERGEFIELD.</summary>
		private static MergeFieldInfo ParseInstruction(string instruction) {
			Match match=_regexMergeField.Match(instruction??"");
			if(!match.Success) {
				return null;
			}
			MergeFieldInfo mergeFieldInfo=new MergeFieldInfo();
			mergeFieldInfo.Name=match.Groups["name"].Value.Trim();
			if(mergeFieldInfo.Name=="") {
				return null;
			}
			foreach(Match matchSwitch in _regexSwitch.Matches(match.Groups["switches"].Value)) {
				string arg=matchSwitch.Groups["arg"].Value;
				switch(matchSwitch.Groups["switch"].Value) {
					case "b":
					case "B":
						mergeFieldInfo.TextBefore=arg;
						break;
					case "f":
					case "F":
						mergeFieldInfo.TextAfter=arg;
						break;
					case "*":
						mergeFieldInfo.ListFormats.Add(arg);
						break;
				}
			}
			return mergeFieldInfo;
		}

		private static string GetValue(MergeFieldInfo mergeFieldInfo,Dictionary<string,string> dictLookup,HashSet<string> hashSetUnknown) {
			string value;
			if(!dictLookup.TryGetValue(mergeFieldInfo.Name,out value)) {
				hashSetUnknown.Add(mergeFieldInfo.Name);
				value="";
			}
			value=(value??"").Replace("\r\n","\n").Replace("\r","\n");
			foreach(string format in mergeFieldInfo.ListFormats) {
				switch(format.ToLowerInvariant()) {
					case "upper":
						value=value.ToUpper(CultureInfo.CurrentCulture);
						break;
					case "lower":
						value=value.ToLower(CultureInfo.CurrentCulture);
						break;
					case "firstcap":
						if(value.Length>0) {
							value=char.ToUpper(value[0],CultureInfo.CurrentCulture)+value.Substring(1);
						}
						break;
					case "caps":
						value=Regex.Replace(value,@"\b\p{Ll}",x => x.Value.ToUpper(CultureInfo.CurrentCulture));
						break;
				}
			}
			if(value=="") {
				return "";
			}
			return mergeFieldInfo.TextBefore+value+mergeFieldInfo.TextAfter;
		}

		///<summary>One run holding the text, with line breaks for new lines, using a copy of the given run properties. Empty text gives no run.
		///Inside the instruction of another field, the text goes in as field code so the outer field still reads it.</summary>
		private static List<XElement> MakeRuns(string text,XElement xElementRunProps,bool isInstruction) {
			List<XElement> listRuns=new List<XElement>();
			if(string.IsNullOrEmpty(text)) {
				return listRuns;
			}
			XElement xElementRun=new XElement(_w+"r");
			if(xElementRunProps!=null) {
				xElementRun.Add(new XElement(xElementRunProps));
			}
			string[] arrayLines=text.Split('\n');
			for(int i=0;i<arrayLines.Length;i++) {
				if(i>0) {
					xElementRun.Add(new XElement(_w+"br"));
				}
				xElementRun.Add(new XElement(_w+(isInstruction ? "instrText" : "t"),new XAttribute(_xml+"space","preserve"),arrayLines[i]));
			}
			listRuns.Add(xElementRun);
			return listRuns;
		}
		#endregion Fields

		#region Package
		private static List<ZipPart> ReadParts(byte[] packageBytes) {
			if(packageBytes==null || packageBytes.Length==0) {
				throw new ApplicationException(Lans.g("ChartLetters","The document is empty."));
			}
			List<ZipPart> listParts=new List<ZipPart>();
			try {
				using(MemoryStream memoryStream=new MemoryStream(packageBytes,false))
				using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Read)) {
					foreach(ZipArchiveEntry zipArchiveEntry in zipArchive.Entries) {
						using(Stream stream=zipArchiveEntry.Open())
						using(MemoryStream memoryStreamEntry=new MemoryStream()) {
							stream.CopyTo(memoryStreamEntry);
							listParts.Add(new ZipPart(zipArchiveEntry.FullName,memoryStreamEntry.ToArray()));
						}
					}
				}
			}
			catch(InvalidDataException ex) {
				throw new ApplicationException(Lans.g("ChartLetters","The document is not in the .docx format."),ex);
			}
			return listParts;
		}

		///<summary>Writes the parts in their original order.</summary>
		private static byte[] WriteParts(List<ZipPart> listParts) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Create,true)) {
					foreach(ZipPart zipPart in listParts) {
						ZipArchiveEntry zipArchiveEntry=zipArchive.CreateEntry(zipPart.Name,CompressionLevel.Optimal);
						using(Stream stream=zipArchiveEntry.Open()) {
							stream.Write(zipPart.Bytes,0,zipPart.Bytes.Length);
						}
					}
				}
				return memoryStream.ToArray();
			}
		}

		private static XDocument LoadXml(byte[] bytes) {
			using(MemoryStream memoryStream=new MemoryStream(bytes,false)) {
				return XDocument.Load(memoryStream,LoadOptions.PreserveWhitespace);
			}
		}

		private static byte[] SaveXml(XDocument xDocument) {
			XmlWriterSettings xmlWriterSettings=new XmlWriterSettings();
			xmlWriterSettings.Encoding=new UTF8Encoding(false);
			xmlWriterSettings.OmitXmlDeclaration=xDocument.Declaration==null;
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(XmlWriter xmlWriter=XmlWriter.Create(memoryStream,xmlWriterSettings)) {
					xDocument.Save(xmlWriter);
				}
				return memoryStream.ToArray();
			}
		}
		#endregion Package

		private class ZipPart {
			public string Name;
			public byte[] Bytes;

			public ZipPart(string name,byte[] bytes) {
				Name=name;
				Bytes=bytes;
			}
		}

		private class FieldFrame {
			public int IdxBegin;
			public int IdxSeparate=-1;
			public int IdxEnd;
			public StringBuilder Instruction=new StringBuilder();
			///<summary>True when the field sits in the instruction (code) part of an outer field, for example a MERGEFIELD inside an IF.</summary>
			public bool IsInOuterInstruction;
		}

		private class MergeFieldInfo {
			public string Name;
			public string TextBefore="";
			public string TextAfter="";
			public List<string> ListFormats=new List<string>();
		}
	}
}
