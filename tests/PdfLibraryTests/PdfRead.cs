// Reads PDFs back for the tests: saves a document to bytes, reopens it with PdfReader, and extracts the text, fonts and images of a page.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace PdfLibraryTests {
	public static class PdfRead {
		///<summary>Saves the document (a PDF can be saved only once) and returns the file's bytes.</summary>
		public static byte[] Save(PdfDocument pdfDocument) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				pdfDocument.Save(memoryStream,false);
				return memoryStream.ToArray();
			}
		}

		///<summary>Opens saved bytes again with PdfReader. The stream is closed before the document is used, as BillingL does.</summary>
		public static PdfDocument Open(byte[] arrayBytes,PdfDocumentOpenMode pdfDocumentOpenMode=PdfDocumentOpenMode.ReadOnly) {
			using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
				return PdfReader.Open(memoryStream,pdfDocumentOpenMode);
			}
		}

		///<summary>The page's size in points, rounded, as "width x height".</summary>
		public static string Size(PdfPage pdfPage) {
			return Math.Round(pdfPage.Width.Point).ToString(CultureInfo.InvariantCulture)+" x "
				+Math.Round(pdfPage.Height.Point).ToString(CultureInfo.InvariantCulture);
		}

		///<summary>The text the page's content stream shows, one line per text-showing operator, decoded through each font's encoding: the ToUnicode
		///map of a Unicode (Type0) font, Windows-1252 for a simple font.</summary>
		public static string ExtractText(PdfPage pdfPage) {
			Dictionary<string,Func<string,CStringType,string>> dictDecoders=GetDecoders(pdfPage);
			StringBuilder stringBuilder=new StringBuilder();
			string fontName="";
			foreach(COperator cOperator in ReadOperators(pdfPage)) {
				string name=cOperator.Name;
				if(name=="Tf" && cOperator.Operands.Count>0 && cOperator.Operands[0] is CName cName) {
					fontName=cName.Name;
					continue;
				}
				if(name!="Tj" && name!="TJ" && name!="'" && name!="\"") {
					continue;
				}
				Func<string,CStringType,string> funcDecode=dictDecoders.TryGetValue(fontName,out Func<string,CStringType,string> func) ? func : DecodeAnsi;
				foreach(CString cString in GetStrings(cOperator.Operands)) {
					stringBuilder.Append(funcDecode(cString.Value,cString.CStringType));
				}
				stringBuilder.Append("\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>True when the page's text holds the expected text, ignoring white space: MigraDoc places each word (and each part of a
		///hyphenated word) with its own operator and draws no spaces.</summary>
		public static bool ContainsText(string pageText,string expected) {
			return Squash(pageText).Contains(Squash(expected));
		}

		private static string Squash(string text) {
			return new string(text.Where(x => !char.IsWhiteSpace(x)).ToArray());
		}

		///<summary>The font resources of the page by name ("/F0"): their BaseFont.</summary>
		public static Dictionary<string,string> GetFonts(PdfPage pdfPage) {
			Dictionary<string,string> dictFonts=new Dictionary<string,string>();
			PdfDictionary dictFontResources=pdfPage.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/Font");
			if(dictFontResources==null) {
				return dictFonts;
			}
			foreach(string key in dictFontResources.Elements.Keys) {
				dictFonts[key]=dictFontResources.Elements.GetDictionary(key)?.Elements.GetName("/BaseFont")??"";
			}
			return dictFonts;
		}

		///<summary>The image XObjects of the page: width, height, filter and whether it has a soft mask.</summary>
		public static List<PdfDictionary> GetImages(PdfPage pdfPage) {
			List<PdfDictionary> listImages=new List<PdfDictionary>();
			PdfDictionary dictXObjects=pdfPage.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/XObject");
			if(dictXObjects==null) {
				return listImages;
			}
			foreach(string key in dictXObjects.Elements.Keys) {
				PdfDictionary dictXObject=dictXObjects.Elements.GetDictionary(key);
				if(dictXObject!=null && dictXObject.Elements.GetName("/Subtype")=="/Image") {
					listImages.Add(dictXObject);
				}
			}
			return listImages;
		}

		///<summary>The names of the operators of the page's content, in order.</summary>
		public static List<string> GetOperatorNames(PdfPage pdfPage) {
			return ReadOperators(pdfPage).Select(x => x.Name).ToList();
		}

		///<summary>The operators of the page's content streams; none for a page without content.</summary>
		private static List<COperator> ReadOperators(PdfPage pdfPage) {
			if(!pdfPage.Elements.ContainsKey("/Contents") || pdfPage.Contents.Elements.Count==0) {
				return new List<COperator>();
			}
			return GetOperators(ContentReader.ReadContent(pdfPage)).ToList();
		}

		private static IEnumerable<COperator> GetOperators(CSequence cSequence) {
			foreach(CObject cObject in cSequence) {
				if(cObject is COperator cOperator) {
					yield return cOperator;
				}
				else if(cObject is CSequence cSequenceInner) {
					foreach(COperator cOperatorInner in GetOperators(cSequenceInner)) {
						yield return cOperatorInner;
					}
				}
			}
		}

		private static IEnumerable<CString> GetStrings(CSequence cSequence) {
			foreach(CObject cObject in cSequence) {
				if(cObject is CString cString) {
					yield return cString;
				}
				else if(cObject is CSequence cSequenceInner) {
					foreach(CString cStringInner in GetStrings(cSequenceInner)) {
						yield return cStringInner;
					}
				}
			}
		}

		private static Dictionary<string,Func<string,CStringType,string>> GetDecoders(PdfPage pdfPage) {
			Dictionary<string,Func<string,CStringType,string>> dictDecoders=new Dictionary<string,Func<string,CStringType,string>>();
			PdfDictionary dictFontResources=pdfPage.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/Font");
			if(dictFontResources==null) {
				return dictDecoders;
			}
			foreach(string key in dictFontResources.Elements.Keys) {
				PdfDictionary dictFont=dictFontResources.Elements.GetDictionary(key);
				PdfDictionary dictToUnicode=dictFont?.Elements.GetDictionary("/ToUnicode");
				if(dictFont==null || dictFont.Elements.GetName("/Subtype")!="/Type0" || dictToUnicode?.Stream==null) {
					dictDecoders[key]=DecodeAnsi;
					continue;
				}
				Dictionary<int,string> dictMap=ParseToUnicode(Encoding.ASCII.GetString(dictToUnicode.Stream.UnfilteredValue));
				dictDecoders[key]=(value,cStringType) => DecodeTwoByte(value,cStringType,dictMap);
			}
			return dictDecoders;
		}

		///<summary>A simple font's string: one byte per character, Windows-1252.</summary>
		private static string DecodeAnsi(string value,CStringType cStringType) {
			byte[] arrayBytes=value.Select(x => (byte)(x & 0xFF)).ToArray();
			return Encoding.GetEncoding(1252).GetString(arrayBytes);
		}

		///<summary>A Type0 (Identity-H) font's string: two bytes per glyph, mapped through the ToUnicode map.</summary>
		private static string DecodeTwoByte(string value,CStringType cStringType,Dictionary<int,string> dictMap) {
			List<int> listCodes=new List<int>();
			if(cStringType==CStringType.UnicodeString || cStringType==CStringType.UnicodeHexString) {
				listCodes.AddRange(value.Select(x => (int)x));
			}
			else {
				for(int i=0;i+1<value.Length;i+=2) {
					listCodes.Add(((value[i] & 0xFF)<<8)|(value[i+1] & 0xFF));
				}
			}
			return string.Concat(listCodes.Select(x => dictMap.TryGetValue(x,out string text) ? text : "?"));
		}

		///<summary>The bfchar and bfrange entries of a ToUnicode CMap.</summary>
		public static Dictionary<int,string> ParseToUnicode(string cmap) {
			Dictionary<int,string> dictMap=new Dictionary<int,string>();
			foreach(Match matchBlock in Regex.Matches(cmap,@"beginbfchar(.*?)endbfchar",RegexOptions.Singleline)) {
				foreach(Match match in Regex.Matches(matchBlock.Groups[1].Value,@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")) {
					dictMap[Convert.ToInt32(match.Groups[1].Value,16)]=HexToString(match.Groups[2].Value);
				}
			}
			foreach(Match matchBlock in Regex.Matches(cmap,@"beginbfrange(.*?)endbfrange",RegexOptions.Singleline)) {
				foreach(Match match in Regex.Matches(matchBlock.Groups[1].Value,@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")) {
					int codeFirst=Convert.ToInt32(match.Groups[1].Value,16);
					int codeLast=Convert.ToInt32(match.Groups[2].Value,16);
					int unicodeFirst=Convert.ToInt32(match.Groups[3].Value,16);
					for(int code=codeFirst;code<=codeLast;code++) {
						dictMap[code]=char.ConvertFromUtf32(unicodeFirst+code-codeFirst);
					}
				}
			}
			return dictMap;
		}

		private static string HexToString(string hex) {
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=0;i+3<hex.Length;i+=4) {
				stringBuilder.Append((char)Convert.ToInt32(hex.Substring(i,4),16));
			}
			return stringBuilder.ToString();
		}
	}
}
