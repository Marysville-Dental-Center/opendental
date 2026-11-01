// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace OpenDentBusiness {
	///<summary>Open Dental 25.4: pasting from Word, a browser or e-mail into an eForm label keeps basic formatting. The clipboard's RTF (Word,
	///WordPad, Outlook) or HTML (browsers) is turned into a small FlowDocument section that keeps bold, italic, underline, font size (relative to the
	///pasted text's main size, so normal text becomes the form's 11.5 base), text color, paragraphs, line breaks, and bulleted and numbered lists.
	///Images, tables (their text is kept, cells separated by tabs), hyperlinks (their text is kept, without link color or underline), fonts (the
	///form's font is used) and background colors are dropped. Plain text, and content copied from a WPF rich text box such as this editor, are left to
	///the normal paste. The behavior details are INFERRED; Open Dental publishes only the summary above.</summary>
	public class EFormRichPaste {
		///<summary>The size eForm labels treat as 100% (EFormField.ValueLabel).</summary>
		public const double FONT_SIZE_BASE=11.5;
		///<summary>Pasted sizes are kept between 50% and 300% of the base, the range of EFormField.FontScale.</summary>
		private const double FONT_SCALE_MIN=0.5;
		private const double FONT_SCALE_MAX=3.0;
		///<summary>The size a browser gives text with no size set.</summary>
		private const double HTML_FONT_SIZE_DEFAULT=16;

		#region Public entry points
		///<summary>The XAML (a Section) to paste instead of the clipboard content, or null to let the normal paste run: there is no RTF or HTML, the
		///content came from a WPF rich text box (it carries XAML), or nothing readable is left after the conversion.</summary>
		public static string GetXamlForPaste(IDataObject dataObject) {
			if(dataObject==null) {
				return null;
			}
			if(dataObject.GetDataPresent(DataFormats.Xaml) || dataObject.GetDataPresent(DataFormats.XamlPackage)) {
				return null;//copied inside this program (for example within the label editor): WPF's own paste keeps every detail
			}
			RichDoc richDoc=null;
			string rtf=GetString(dataObject,DataFormats.Rtf);
			if(!string.IsNullOrWhiteSpace(rtf)) {
				richDoc=FromRtf(rtf);
			}
			if(richDoc==null || richDoc.IsEmpty()) {
				string html=GetString(dataObject,DataFormats.Html);
				if(!string.IsNullOrWhiteSpace(html)) {
					richDoc=FromHtml(GetHtmlFragment(html));
				}
			}
			if(richDoc==null || richDoc.IsEmpty()) {
				return null;
			}
			return ToXaml(richDoc);
		}

		///<summary>Converts RTF to the pasted section's XAML. Null when nothing readable is left.</summary>
		public static string ConvertRtfToXaml(string rtf) {
			RichDoc richDoc=FromRtf(rtf);
			if(richDoc==null || richDoc.IsEmpty()) {
				return null;
			}
			return ToXaml(richDoc);
		}

		///<summary>Converts HTML (a clipboard "HTML Format" block or plain markup) to the pasted section's XAML. Null when nothing readable is left.
		///</summary>
		public static string ConvertHtmlToXaml(string html) {
			RichDoc richDoc=FromHtml(GetHtmlFragment(html));
			if(richDoc==null || richDoc.IsEmpty()) {
				return null;
			}
			return ToXaml(richDoc);
		}

		///<summary>Converts RTF to the kept content, for tests and other callers that want the structure.</summary>
		public static RichDoc FromRtf(string rtf) {
			FlowDocument flowDocument=new FlowDocument();
			TextRange textRange=new TextRange(flowDocument.ContentStart,flowDocument.ContentEnd);
			try {
				using MemoryStream memoryStream=new MemoryStream(Encoding.Default.GetBytes(rtf));
				textRange.Load(memoryStream,DataFormats.Rtf);
			}
			catch(Exception) {
				return null;//not RTF that WPF can read
			}
			return FromFlowDocument(flowDocument);
		}

		///<summary>The kept content of any FlowDocument (WPF's RTF converter makes one).</summary>
		public static RichDoc FromFlowDocument(FlowDocument flowDocument) {
			RichDoc richDoc=new RichDoc();
			AddBlocks(flowDocument.Blocks,richDoc.ListBlocks);
			return richDoc;
		}

		///<summary>The markup between the StartFragment and EndFragment markers of a clipboard "HTML Format" block. Falls back to the offsets in its
		///header, and then to the text itself when it is plain markup.</summary>
		public static string GetHtmlFragment(string cfHtml) {
			if(string.IsNullOrEmpty(cfHtml)) {
				return "";
			}
			int idxStartMarker=cfHtml.IndexOf("<!--StartFragment-->",StringComparison.OrdinalIgnoreCase);
			int idxEndMarker=cfHtml.IndexOf("<!--EndFragment-->",StringComparison.OrdinalIgnoreCase);
			if(idxStartMarker>=0 && idxEndMarker>idxStartMarker) {
				int idxStart=idxStartMarker+"<!--StartFragment-->".Length;
				return cfHtml.Substring(idxStart,idxEndMarker-idxStart);
			}
			Match matchStart=Regex.Match(cfHtml,@"StartFragment:(\d+)");
			Match matchEnd=Regex.Match(cfHtml,@"EndFragment:(\d+)");
			if(matchStart.Success && matchEnd.Success) {
				byte[] arrayBytes=Encoding.UTF8.GetBytes(cfHtml);
				int start=int.Parse(matchStart.Groups[1].Value);
				int end=int.Parse(matchEnd.Groups[1].Value);
				if(start>=0 && end>start && end<=arrayBytes.Length) {
					return Encoding.UTF8.GetString(arrayBytes,start,end-start);
				}
			}
			if(cfHtml.StartsWith("Version:",StringComparison.OrdinalIgnoreCase)) {
				int idxMarkup=cfHtml.IndexOf('<');
				if(idxMarkup>=0) {
					return cfHtml.Substring(idxMarkup);
				}
			}
			return cfHtml;
		}

		///<summary>Builds the Section that gets pasted and returns its XAML. Font sizes become relative to the main size of the pasted text.</summary>
		public static string ToXaml(RichDoc richDoc) {
			Section section=ToSection(richDoc);
			return XamlWriter.Save(section);
		}

		///<summary>Builds the Section that gets pasted.</summary>
		public static Section ToSection(RichDoc richDoc) {
			double scale=FONT_SIZE_BASE/GetMainFontSize(richDoc);
			List<RichBlock> listBlocks=TrimEmptyParagraphs(richDoc.ListBlocks);
			Section section=new Section();
			for(int i=0;i<listBlocks.Count;i++) {
				section.Blocks.Add(ToBlock(listBlocks[i],scale));
			}
			return section;
		}
		#endregion Public entry points

		#region Model
		///<summary>The content that is kept, before it becomes WPF elements.</summary>
		public class RichDoc {
			public List<RichBlock> ListBlocks=new List<RichBlock>();

			///<summary>True when no text is left.</summary>
			public bool IsEmpty() {
				return GetAllInlines(ListBlocks).All(x => x.IsLineBreak || string.IsNullOrWhiteSpace(x.Text));
			}

			///<summary>All text, paragraphs separated by line feeds. For tests and logs.</summary>
			public string GetText() {
				StringBuilder stringBuilder=new StringBuilder();
				AppendText(ListBlocks,stringBuilder);
				return stringBuilder.ToString().TrimEnd('\n');
			}

			private static void AppendText(List<RichBlock> listBlocks,StringBuilder stringBuilder) {
				for(int i=0;i<listBlocks.Count;i++) {
					if(listBlocks[i] is RichParagraph richParagraph) {
						for(int r=0;r<richParagraph.ListInlines.Count;r++) {
							stringBuilder.Append(richParagraph.ListInlines[r].IsLineBreak ? "\n" : richParagraph.ListInlines[r].Text);
						}
						stringBuilder.Append("\n");
					}
					if(listBlocks[i] is RichList richList) {
						for(int item=0;item<richList.ListItems.Count;item++) {
							AppendText(richList.ListItems[item],stringBuilder);
						}
					}
				}
			}
		}

		public abstract class RichBlock {
		}

		public class RichParagraph:RichBlock {
			public TextAlignment Alignment=TextAlignment.Left;
			public List<RichInline> ListInlines=new List<RichInline>();
		}

		public class RichList:RichBlock {
			public TextMarkerStyle MarkerStyle=TextMarkerStyle.Disc;
			///<summary>Each item holds its own blocks.</summary>
			public List<List<RichBlock>> ListItems=new List<List<RichBlock>>();
		}

		public class RichInline {
			public string Text="";
			public bool IsLineBreak;
			public bool IsBold;
			public bool IsItalic;
			public bool IsUnderline;
			///<summary>Null for the default (black) text color.</summary>
			public Color? ColorText;
			///<summary>Absolute size in DIPs, or null when unknown.</summary>
			public double? FontSize;

			public bool IsSameFormat(RichInline richInline) {
				return !IsLineBreak && !richInline.IsLineBreak && IsBold==richInline.IsBold && IsItalic==richInline.IsItalic
					&& IsUnderline==richInline.IsUnderline && Nullable.Equals(ColorText,richInline.ColorText) && Nullable.Equals(FontSize,richInline.FontSize);
			}
		}

		private static IEnumerable<RichInline> GetAllInlines(List<RichBlock> listBlocks) {
			for(int i=0;i<listBlocks.Count;i++) {
				if(listBlocks[i] is RichParagraph richParagraph) {
					foreach(RichInline richInline in richParagraph.ListInlines) {
						yield return richInline;
					}
				}
				if(listBlocks[i] is RichList richList) {
					foreach(List<RichBlock> listBlocksItem in richList.ListItems) {
						foreach(RichInline richInline in GetAllInlines(listBlocksItem)) {
							yield return richInline;
						}
					}
				}
			}
		}
		#endregion Model

		#region FlowDocument (RTF) to model
		private static void AddBlocks(IEnumerable<Block> enumerableBlocks,List<RichBlock> listBlocks) {
			foreach(Block block in enumerableBlocks.ToList()) {
				if(block is Paragraph paragraph) {
					RichParagraph richParagraph=new RichParagraph();
					richParagraph.Alignment=paragraph.TextAlignment;
					AddInlines(paragraph.Inlines,richParagraph.ListInlines,isInLink:false);
					listBlocks.Add(richParagraph);
					continue;
				}
				if(block is List list) {
					RichList richList=new RichList();
					richList.MarkerStyle=list.MarkerStyle==TextMarkerStyle.None ? TextMarkerStyle.Disc : list.MarkerStyle;
					foreach(ListItem listItem in list.ListItems.ToList()) {
						List<RichBlock> listBlocksItem=new List<RichBlock>();
						AddBlocks(listItem.Blocks,listBlocksItem);
						richList.ListItems.Add(listBlocksItem);
					}
					listBlocks.Add(richList);
					continue;
				}
				if(block is Section sectionInner) {
					AddBlocks(sectionInner.Blocks,listBlocks);
					continue;
				}
				if(block is Table table) {
					AddTable(table,listBlocks);
					continue;
				}
				//BlockUIContainer (images and other embedded objects): dropped.
			}
		}

		///<summary>A table keeps its text: one paragraph per row, cells separated by tabs.</summary>
		private static void AddTable(Table table,List<RichBlock> listBlocks) {
			foreach(TableRowGroup tableRowGroup in table.RowGroups.ToList()) {
				foreach(TableRow tableRow in tableRowGroup.Rows.ToList()) {
					RichParagraph richParagraph=new RichParagraph();
					List<TableCell> listTableCells=tableRow.Cells.ToList();
					for(int c=0;c<listTableCells.Count;c++) {
						if(c>0) {
							richParagraph.ListInlines.Add(new RichInline() { Text="\t" });
						}
						List<RichBlock> listBlocksCell=new List<RichBlock>();
						AddBlocks(listTableCells[c].Blocks,listBlocksCell);
						List<RichInline> listInlinesCell=GetAllInlines(listBlocksCell).Where(x => !x.IsLineBreak).ToList();
						for(int r=0;r<listInlinesCell.Count;r++) {
							richParagraph.ListInlines.Add(listInlinesCell[r]);
						}
					}
					if(richParagraph.ListInlines.Exists(x => !string.IsNullOrWhiteSpace(x.Text))) {
						listBlocks.Add(richParagraph);
					}
				}
			}
		}

		private static void AddInlines(InlineCollection inlineCollection,List<RichInline> listInlines,bool isInLink) {
			foreach(Inline inline in inlineCollection.ToList()) {
				if(inline is Run run) {
					if(string.IsNullOrEmpty(run.Text)) {
						continue;
					}
					RichInline richInline=new RichInline();
					richInline.Text=run.Text;
					richInline.IsBold=run.FontWeight.ToOpenTypeWeight()>=FontWeights.SemiBold.ToOpenTypeWeight();
					richInline.IsItalic=run.FontStyle!=FontStyles.Normal;
					richInline.FontSize=run.FontSize;
					if(!isInLink) {
						richInline.IsUnderline=HasUnderline(run);
						if(run.Foreground is SolidColorBrush solidColorBrush) {
							richInline.ColorText=GetColorKept(solidColorBrush.Color);
						}
					}
					listInlines.Add(richInline);
					continue;
				}
				if(inline is LineBreak) {
					listInlines.Add(new RichInline() { IsLineBreak=true });
					continue;
				}
				if(inline is Span span) {//also Bold, Italic, Underline and Hyperlink
					AddInlines(span.Inlines,listInlines,isInLink || span is Hyperlink);
					continue;
				}
				//InlineUIContainer (images) and anchored blocks (figures, floaters): dropped.
			}
		}

		///<summary>True when the run or one of its enclosing inlines or its paragraph is underlined.</summary>
		private static bool HasUnderline(Inline inline) {
			DependencyObject dependencyObject=inline;
			while(dependencyObject!=null) {
				TextDecorationCollection textDecorationCollection=null;
				if(dependencyObject is Inline inlineCur) {
					textDecorationCollection=inlineCur.TextDecorations;
				}
				else if(dependencyObject is Paragraph paragraph) {
					textDecorationCollection=paragraph.TextDecorations;
				}
				else {
					break;
				}
				if(textDecorationCollection!=null && textDecorationCollection.Any(x => x.Location==TextDecorationLocation.Underline)) {
					return true;
				}
				dependencyObject=(dependencyObject as TextElement)?.Parent;
			}
			return false;
		}

		///<summary>Null for black and near-black (the default text color); otherwise the color without transparency.</summary>
		private static Color? GetColorKept(Color color) {
			if(color.A==0) {
				return null;
			}
			if(color.R<24 && color.G<24 && color.B<24) {
				return null;
			}
			return Color.FromRgb(color.R,color.G,color.B);
		}
		#endregion FlowDocument (RTF) to model

		#region HTML to model
		private class HtmlFormat {
			public bool IsBold;
			public bool IsItalic;
			public bool IsUnderline;
			public Color? ColorText;
			public double FontSize=HTML_FONT_SIZE_DEFAULT;
			public bool IsInLink;
			public bool IsPreformatted;
			public bool IsSkipped;
			public TextAlignment Alignment=TextAlignment.Left;
			public string TagName="";
			///<summary>True when the element's start changed the structure (it was not inside skipped content), so its end must too.</summary>
			public bool IsStructural;

			public HtmlFormat CopyFor(string tagName) {
				HtmlFormat htmlFormat=(HtmlFormat)MemberwiseClone();
				htmlFormat.TagName=tagName;
				htmlFormat.IsStructural=false;
				return htmlFormat;
			}
		}

		private class HtmlBuilder {
			public RichDoc RichDoc=new RichDoc();
			public List<HtmlFormat> ListFormats=new List<HtmlFormat>() { new HtmlFormat() };
			///<summary>Where new blocks go: the document, or the item of the innermost open list.</summary>
			public List<List<RichBlock>> StackBlockTargets=new List<List<RichBlock>>();
			public List<RichList> StackLists=new List<RichList>();
			///<summary>The list made for items copied without their list element. Later such items join it until other content comes.</summary>
			public RichList ListImplicit;
			public RichParagraph ParagraphCur;
			///<summary>True right after a table cell started, so the next cell gets a tab first.</summary>
			public bool IsCellInRow;

			public HtmlBuilder() {
				StackBlockTargets.Add(RichDoc.ListBlocks);
			}

			public HtmlFormat FormatCur {
				get { return ListFormats[ListFormats.Count-1]; }
			}

			public List<RichBlock> BlockTarget {
				get { return StackBlockTargets[StackBlockTargets.Count-1]; }
			}

			///<summary>The next text starts a new paragraph.</summary>
			public void EndParagraph() {
				ParagraphCur=null;
			}

			///<summary>True while an item that has no list element of its own is open.</summary>
			public bool IsImplicitItemOpen() {
				return ListFormats.Exists(x => x.TagName=="li-implicit");
			}

			public RichParagraph GetParagraph() {
				if(ParagraphCur==null) {
					if(!IsImplicitItemOpen()) {
						ListImplicit=null;//content between items ends the implicit list
					}
					ParagraphCur=new RichParagraph();
					ParagraphCur.Alignment=FormatCur.Alignment;
					BlockTarget.Add(ParagraphCur);
				}
				return ParagraphCur;
			}

			public void AddText(string text) {
				HtmlFormat htmlFormat=FormatCur;
				if(htmlFormat.IsSkipped || string.IsNullOrEmpty(text)) {
					return;
				}
				if(htmlFormat.IsPreformatted) {
					string[] arrayLines=text.Replace("\r\n","\n").Split('\n');
					for(int i=0;i<arrayLines.Length;i++) {
						if(i>0) {
							AddLineBreak();
						}
						AddRun(arrayLines[i],htmlFormat);
					}
					return;
				}
				string textCollapsed=Regex.Replace(text,@"[ \t\r\n\f]+"," ");
				if(ParagraphCur==null || IsAtLineStart(ParagraphCur)) {
					textCollapsed=textCollapsed.TrimStart(' ');
				}
				else if(textCollapsed.StartsWith(" ") && EndsWithSpace(ParagraphCur)) {
					textCollapsed=textCollapsed.TrimStart(' ');
				}
				if(textCollapsed=="") {
					return;
				}
				AddRun(textCollapsed,htmlFormat);
			}

			private void AddRun(string text,HtmlFormat htmlFormat) {
				if(text=="") {
					return;
				}
				RichInline richInline=new RichInline();
				richInline.Text=text.Replace(' ',' ');
				richInline.IsBold=htmlFormat.IsBold;
				richInline.IsItalic=htmlFormat.IsItalic;
				richInline.IsUnderline=htmlFormat.IsUnderline && !htmlFormat.IsInLink;
				richInline.ColorText=htmlFormat.IsInLink ? null : htmlFormat.ColorText;
				richInline.FontSize=htmlFormat.FontSize;
				GetParagraph().ListInlines.Add(richInline);
			}

			public void AddLineBreak() {
				if(FormatCur.IsSkipped) {
					return;
				}
				GetParagraph().ListInlines.Add(new RichInline() { IsLineBreak=true });
			}

			private static bool IsAtLineStart(RichParagraph richParagraph) {
				return richParagraph.ListInlines.Count==0 || richParagraph.ListInlines[richParagraph.ListInlines.Count-1].IsLineBreak;
			}

			private static bool EndsWithSpace(RichParagraph richParagraph) {
				RichInline richInlineLast=richParagraph.ListInlines.LastOrDefault();
				return richInlineLast!=null && !richInlineLast.IsLineBreak && richInlineLast.Text.EndsWith(" ");
			}
		}

		private static readonly HashSet<string> _hashSetBlockTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"p","div","h1","h2","h3","h4","h5","h6","blockquote","pre","address","center","section","article","header","footer","main","nav",
			"aside","figure","figcaption","dl","dt","dd","table","tbody","thead","tfoot","tr","caption","form","fieldset","hr","body","html"
		};
		private static readonly HashSet<string> _hashSetSkippedTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"script","style","head","title","object","iframe","svg","noscript","template","select","textarea","button","math","video","audio","canvas"
		};
		private static readonly HashSet<string> _hashSetVoidTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"br","img","hr","meta","link","input","area","base","col","embed","param","source","track","wbr"
		};

		///<summary>Converts HTML markup (not a whole clipboard block; see GetHtmlFragment) to the kept content.</summary>
		public static RichDoc FromHtml(string html) {
			HtmlBuilder htmlBuilder=new HtmlBuilder();
			int idx=0;
			html=html??"";
			while(idx<html.Length) {
				int idxTag=html.IndexOf('<',idx);
				if(idxTag==-1) {
					htmlBuilder.AddText(WebUtility.HtmlDecode(html.Substring(idx)));
					break;
				}
				if(idxTag>idx) {
					htmlBuilder.AddText(WebUtility.HtmlDecode(html.Substring(idx,idxTag-idx)));
				}
				if(string.Compare(html,idxTag,"<!--",0,4,StringComparison.Ordinal)==0) {
					int idxEndComment=html.IndexOf("-->",idxTag+4,StringComparison.Ordinal);
					idx=idxEndComment==-1 ? html.Length : idxEndComment+3;
					continue;
				}
				int idxTagEnd=FindTagEnd(html,idxTag);
				if(idxTagEnd==-1) {
					htmlBuilder.AddText(WebUtility.HtmlDecode(html.Substring(idxTag)));
					break;
				}
				string tag=html.Substring(idxTag+1,idxTagEnd-idxTag-1);
				idx=idxTagEnd+1;
				if(tag.StartsWith("!") || tag.StartsWith("?")) {
					continue;//doctype and processing instructions
				}
				HandleTag(htmlBuilder,tag);
			}
			return htmlBuilder.RichDoc;
		}

		///<summary>The index of the closing angle bracket of a tag, skipping quoted attribute values. -1 when there is none.</summary>
		private static int FindTagEnd(string html,int idxTagStart) {
			char quote='\0';
			for(int i=idxTagStart+1;i<html.Length;i++) {
				char c=html[i];
				if(quote!='\0') {
					if(c==quote) {
						quote='\0';
					}
					continue;
				}
				if(c=='"' || c=='\'') {
					quote=c;
					continue;
				}
				if(c=='>') {
					return i;
				}
			}
			return -1;
		}

		private static void HandleTag(HtmlBuilder htmlBuilder,string tag) {
			bool isEnd=tag.StartsWith("/");
			string body=isEnd ? tag.Substring(1) : tag;
			bool isSelfClosing=body.EndsWith("/");
			if(isSelfClosing) {
				body=body.Substring(0,body.Length-1);
			}
			Match matchName=Regex.Match(body,@"^\s*([A-Za-z][A-Za-z0-9:_-]*)");
			if(!matchName.Success) {
				return;
			}
			string name=matchName.Groups[1].Value.ToLowerInvariant();
			int idxColon=name.IndexOf(':');
			if(idxColon>=0) {
				name=name.Substring(idxColon+1);//Word's o:p and similar
			}
			if(isEnd) {
				HandleEndTag(htmlBuilder,name);
				return;
			}
			Dictionary<string,string> dictAttributes=ParseAttributes(body.Substring(matchName.Length));
			if(htmlBuilder.FormatCur.IsSkipped) {
				if(!_hashSetVoidTags.Contains(name) && !isSelfClosing) {
					htmlBuilder.ListFormats.Add(htmlBuilder.FormatCur.CopyFor(name));
				}
				return;
			}
			if(name=="br") {
				htmlBuilder.AddLineBreak();
				return;
			}
			if(_hashSetVoidTags.Contains(name)) {
				if(name=="hr") {
					htmlBuilder.EndParagraph();
				}
				return;//images and other embedded content are dropped
			}
			if(name!="li" && htmlBuilder.ListImplicit!=null && !htmlBuilder.IsImplicitItemOpen()) {
				htmlBuilder.ListImplicit=null;//other content after items copied without their list
			}
			HtmlFormat htmlFormat=htmlBuilder.FormatCur.CopyFor(name);
			htmlFormat.IsStructural=true;
			if(_hashSetSkippedTags.Contains(name)) {
				htmlFormat.IsSkipped=true;
			}
			ApplyTagFormat(htmlFormat,name,dictAttributes);
			string style;
			if(dictAttributes.TryGetValue("style",out style)) {
				ApplyStyle(htmlFormat,style,htmlBuilder.FormatCur.FontSize);
			}
			if(name=="ul" || name=="ol") {
				htmlBuilder.EndParagraph();
				RichList richList=new RichList();
				richList.MarkerStyle=GetMarkerStyle(name,dictAttributes);
				htmlBuilder.BlockTarget.Add(richList);
				htmlBuilder.StackLists.Add(richList);
			}
			else if(name=="li") {
				htmlBuilder.EndParagraph();
				RichList richListTarget;
				if(htmlBuilder.StackLists.Count>0) {
					richListTarget=htmlBuilder.StackLists[htmlBuilder.StackLists.Count-1];
				}
				else {//an item copied without its list: a bulleted list holds it and the items that follow
					if(htmlBuilder.ListImplicit==null) {
						htmlBuilder.ListImplicit=new RichList();
						htmlBuilder.BlockTarget.Add(htmlBuilder.ListImplicit);
					}
					richListTarget=htmlBuilder.ListImplicit;
					htmlFormat.TagName="li-implicit";
				}
				List<RichBlock> listBlocksItem=new List<RichBlock>();
				richListTarget.ListItems.Add(listBlocksItem);
				htmlBuilder.StackBlockTargets.Add(listBlocksItem);
			}
			else if(name=="tr") {
				htmlBuilder.EndParagraph();
				htmlBuilder.IsCellInRow=false;
			}
			else if(name=="td" || name=="th") {
				if(htmlBuilder.IsCellInRow) {
					htmlBuilder.GetParagraph().ListInlines.Add(new RichInline() { Text="\t" });
				}
				htmlBuilder.IsCellInRow=true;
			}
			else if(_hashSetBlockTags.Contains(name)) {
				htmlBuilder.EndParagraph();
			}
			if(isSelfClosing) {
				HandleTagClosed(htmlBuilder,name);
				return;
			}
			htmlBuilder.ListFormats.Add(htmlFormat);
		}

		private static void HandleEndTag(HtmlBuilder htmlBuilder,string name) {
			int idxOpen=htmlBuilder.ListFormats.FindLastIndex(x => x.TagName==name || (name=="li" && x.TagName=="li-implicit"));
			if(idxOpen<1) {
				return;//never opened, or the root
			}
			for(int i=htmlBuilder.ListFormats.Count-1;i>=idxOpen;i--) {
				HtmlFormat htmlFormat=htmlBuilder.ListFormats[i];
				htmlBuilder.ListFormats.RemoveAt(i);
				if(htmlFormat.IsStructural) {//elements left open inside it are closed too
					HandleTagClosed(htmlBuilder,htmlFormat.TagName);
				}
			}
		}

		///<summary>The structure changes when an element ends.</summary>
		private static void HandleTagClosed(HtmlBuilder htmlBuilder,string name) {
			if(name=="li" || name=="li-implicit") {
				htmlBuilder.EndParagraph();
				if(htmlBuilder.StackBlockTargets.Count>1) {
					htmlBuilder.StackBlockTargets.RemoveAt(htmlBuilder.StackBlockTargets.Count-1);
				}
				return;
			}
			if(name=="ul" || name=="ol") {
				htmlBuilder.EndParagraph();
				if(htmlBuilder.StackLists.Count>0) {
					htmlBuilder.StackLists.RemoveAt(htmlBuilder.StackLists.Count-1);
				}
				return;
			}
			if(name=="tr") {
				htmlBuilder.EndParagraph();
				htmlBuilder.IsCellInRow=false;
				return;
			}
			if(_hashSetBlockTags.Contains(name)) {
				htmlBuilder.EndParagraph();
			}
		}

		private static Dictionary<string,string> ParseAttributes(string text) {
			Dictionary<string,string> dictAttributes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			MatchCollection matchCollection=Regex.Matches(text,@"([A-Za-z_:][A-Za-z0-9_:.-]*)\s*(?:=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+)))?");
			for(int i=0;i<matchCollection.Count;i++) {
				string name=matchCollection[i].Groups[1].Value;
				string value=matchCollection[i].Groups[2].Success ? matchCollection[i].Groups[2].Value
					: matchCollection[i].Groups[3].Success ? matchCollection[i].Groups[3].Value
					: matchCollection[i].Groups[4].Value;
				if(!dictAttributes.ContainsKey(name)) {
					dictAttributes[name]=WebUtility.HtmlDecode(value);
				}
			}
			return dictAttributes;
		}

		private static void ApplyTagFormat(HtmlFormat htmlFormat,string name,Dictionary<string,string> dictAttributes) {
			switch(name) {
				case "b":
				case "strong":
				case "th":
					htmlFormat.IsBold=true;
					break;
				case "i":
				case "em":
				case "cite":
				case "var":
				case "dfn":
					htmlFormat.IsItalic=true;
					break;
				case "u":
				case "ins":
					htmlFormat.IsUnderline=true;
					break;
				case "a":
					htmlFormat.IsInLink=true;
					break;
				case "pre":
					htmlFormat.IsPreformatted=true;
					break;
				case "center":
					htmlFormat.Alignment=TextAlignment.Center;
					break;
				case "h1":
				case "h2":
				case "h3":
				case "h4":
				case "h5":
				case "h6":
					htmlFormat.IsBold=true;
					double[] arrayScales=new double[] { 2,1.5,1.17,1,0.83,0.67 };
					htmlFormat.FontSize=HTML_FONT_SIZE_DEFAULT*arrayScales[name[1]-'1'];
					break;
				case "font":
					if(dictAttributes.TryGetValue("color",out string color)) {
						htmlFormat.ColorText=ParseColor(color)??htmlFormat.ColorText;
					}
					if(dictAttributes.TryGetValue("size",out string size)) {
						htmlFormat.FontSize=ParseFontTagSize(size)??htmlFormat.FontSize;
					}
					break;
			}
			if(dictAttributes.TryGetValue("align",out string align)) {
				htmlFormat.Alignment=ParseAlignment(align)??htmlFormat.Alignment;
			}
		}

		private static void ApplyStyle(HtmlFormat htmlFormat,string style,double fontSizeParent) {
			string[] arrayDeclarations=style.Split(';');
			for(int i=0;i<arrayDeclarations.Length;i++) {
				int idxColon=arrayDeclarations[i].IndexOf(':');
				if(idxColon<=0) {
					continue;
				}
				string property=arrayDeclarations[i].Substring(0,idxColon).Trim().ToLowerInvariant();
				string value=arrayDeclarations[i].Substring(idxColon+1).Trim().ToLowerInvariant().Replace("!important","").Trim();
				switch(property) {
					case "font-weight":
						if(value=="bold" || value=="bolder" || (int.TryParse(value,out int weight) && weight>=600)) {
							htmlFormat.IsBold=true;
						}
						else if(value=="normal" || value=="lighter" || (int.TryParse(value,out int weightNormal) && weightNormal<600)) {
							htmlFormat.IsBold=false;
						}
						break;
					case "font-style":
						htmlFormat.IsItalic=value.StartsWith("italic") || value.StartsWith("oblique");
						break;
					case "text-decoration":
					case "text-decoration-line":
						if(value.Contains("underline")) {
							htmlFormat.IsUnderline=true;
						}
						else if(value.StartsWith("none")) {
							htmlFormat.IsUnderline=false;
						}
						break;
					case "color":
						htmlFormat.ColorText=ParseColor(value)??htmlFormat.ColorText;
						break;
					case "font-size":
						htmlFormat.FontSize=ParseCssFontSize(value,fontSizeParent)??htmlFormat.FontSize;
						break;
					case "text-align":
						htmlFormat.Alignment=ParseAlignment(value)??htmlFormat.Alignment;
						break;
					case "display":
						if(value=="none") {
							htmlFormat.IsSkipped=true;
						}
						break;
				}
			}
		}

		private static TextMarkerStyle GetMarkerStyle(string name,Dictionary<string,string> dictAttributes) {
			string type="";
			dictAttributes.TryGetValue("type",out type);
			string style="";
			if(dictAttributes.TryGetValue("style",out style)) {
				Match match=Regex.Match(style,@"list-style(?:-type)?\s*:\s*([a-z-]+)",RegexOptions.IgnoreCase);
				if(match.Success) {
					type=match.Groups[1].Value.ToLowerInvariant();
				}
			}
			switch(type??"") {
				case "a":
				case "lower-alpha":
				case "lower-latin":
					return TextMarkerStyle.LowerLatin;
				case "A":
				case "upper-alpha":
				case "upper-latin":
					return TextMarkerStyle.UpperLatin;
				case "i":
				case "lower-roman":
					return TextMarkerStyle.LowerRoman;
				case "I":
				case "upper-roman":
					return TextMarkerStyle.UpperRoman;
				case "1":
				case "decimal":
					return TextMarkerStyle.Decimal;
				case "circle":
					return TextMarkerStyle.Circle;
				case "square":
					return TextMarkerStyle.Square;
				case "disc":
					return TextMarkerStyle.Disc;
			}
			return name=="ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;
		}

		private static TextAlignment? ParseAlignment(string value) {
			switch((value??"").Trim().ToLowerInvariant()) {
				case "center":
					return TextAlignment.Center;
				case "right":
					return TextAlignment.Right;
				case "justify":
					return TextAlignment.Justify;
				case "left":
					return TextAlignment.Left;
			}
			return null;
		}

		///<summary>CSS colors: #rgb, #rrggbb, rgb() and rgba(), and named colors. Null for anything else, including "inherit".</summary>
		public static Color? ParseColor(string value) {
			value=(value??"").Trim();
			if(value=="") {
				return null;
			}
			Match matchRgb=Regex.Match(value,@"^rgba?\(\s*(\d+)\s*[, ]\s*(\d+)\s*[, ]\s*(\d+)",RegexOptions.IgnoreCase);
			if(matchRgb.Success) {
				return Color.FromRgb(ClampByte(matchRgb.Groups[1].Value),ClampByte(matchRgb.Groups[2].Value),ClampByte(matchRgb.Groups[3].Value));
			}
			Match matchHex3=Regex.Match(value,@"^#([0-9a-f])([0-9a-f])([0-9a-f])$",RegexOptions.IgnoreCase);
			if(matchHex3.Success) {
				return Color.FromRgb(HexByte(matchHex3.Groups[1].Value+matchHex3.Groups[1].Value),HexByte(matchHex3.Groups[2].Value+matchHex3.Groups[2].Value),
					HexByte(matchHex3.Groups[3].Value+matchHex3.Groups[3].Value));
			}
			Match matchHex6=Regex.Match(value,@"^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$",RegexOptions.IgnoreCase);
			if(matchHex6.Success) {
				return Color.FromRgb(HexByte(matchHex6.Groups[1].Value),HexByte(matchHex6.Groups[2].Value),HexByte(matchHex6.Groups[3].Value));
			}
			if(!Regex.IsMatch(value,@"^[a-zA-Z]+$") || value.Equals("inherit",StringComparison.OrdinalIgnoreCase)
				|| value.Equals("transparent",StringComparison.OrdinalIgnoreCase) || value.Equals("currentcolor",StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			try {
				object objectColor=ColorConverter.ConvertFromString(value);
				if(objectColor is Color colorNamed) {
					return Color.FromRgb(colorNamed.R,colorNamed.G,colorNamed.B);
				}
			}
			catch(FormatException) {
			}
			return null;
		}

		private static byte ClampByte(string value) {
			int number=int.Parse(value,CultureInfo.InvariantCulture);
			return (byte)Math.Max(0,Math.Min(255,number));
		}

		private static byte HexByte(string value) {
			return byte.Parse(value,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
		}

		///<summary>CSS font sizes in DIPs (CSS pixels): px, pt, em, rem, %, and the keywords. Null for anything else.</summary>
		public static double? ParseCssFontSize(string value,double fontSizeParent) {
			value=(value??"").Trim().ToLowerInvariant();
			switch(value) {
				case "xx-small": return 9;
				case "x-small": return 10;
				case "small": return 13;
				case "medium": return 16;
				case "large": return 18;
				case "x-large": return 24;
				case "xx-large": return 32;
				case "smaller": return fontSizeParent/1.2;
				case "larger": return fontSizeParent*1.2;
			}
			Match match=Regex.Match(value,@"^(\d*\.?\d+)\s*(px|pt|em|rem|%)$");
			if(!match.Success) {
				return null;
			}
			double number=double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
			switch(match.Groups[2].Value) {
				case "px": return number;
				case "pt": return number*96/72;
				case "em": return number*fontSizeParent;
				case "rem": return number*HTML_FONT_SIZE_DEFAULT;
				case "%": return number*fontSizeParent/100;
			}
			return null;
		}

		///<summary>The old font tag sizes 1 to 7, also relative like "+1".</summary>
		private static double? ParseFontTagSize(string value) {
			value=(value??"").Trim();
			double[] arraySizes=new double[] { 10,13,16,18,24,32,48 };
			int size;
			if(value.StartsWith("+") || value.StartsWith("-")) {
				if(!int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int delta)) {
					return null;
				}
				size=3+delta;
			}
			else if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out size)) {
				return null;
			}
			size=Math.Max(1,Math.Min(7,size));
			return arraySizes[size-1];
		}
		#endregion HTML to model

		#region Model to WPF
		///<summary>The size most of the pasted text has (by characters). It becomes the base size. 16 (a browser's default) when no size is known.
		///</summary>
		public static double GetMainFontSize(RichDoc richDoc) {
			Dictionary<double,int> dictCharsBySize=new Dictionary<double,int>();
			foreach(RichInline richInline in GetAllInlines(richDoc.ListBlocks)) {
				if(richInline.IsLineBreak || richInline.FontSize==null || richInline.FontSize<=0 || string.IsNullOrWhiteSpace(richInline.Text)) {
					continue;
				}
				double size=Math.Round(richInline.FontSize.Value*2)/2;
				dictCharsBySize.TryGetValue(size,out int count);
				dictCharsBySize[size]=count+richInline.Text.Trim().Length;
			}
			if(dictCharsBySize.Count==0) {
				return HTML_FONT_SIZE_DEFAULT;
			}
			return dictCharsBySize.OrderByDescending(x => x.Value).ThenBy(x => x.Key).First().Key;
		}

		///<summary>Leading and trailing empty paragraphs are left out.</summary>
		private static List<RichBlock> TrimEmptyParagraphs(List<RichBlock> listBlocks) {
			List<RichBlock> listBlocksTrimmed=new List<RichBlock>(listBlocks);
			while(listBlocksTrimmed.Count>0 && IsEmptyParagraph(listBlocksTrimmed[0])) {
				listBlocksTrimmed.RemoveAt(0);
			}
			while(listBlocksTrimmed.Count>0 && IsEmptyParagraph(listBlocksTrimmed[listBlocksTrimmed.Count-1])) {
				listBlocksTrimmed.RemoveAt(listBlocksTrimmed.Count-1);
			}
			return listBlocksTrimmed;
		}

		private static bool IsEmptyParagraph(RichBlock richBlock) {
			if(!(richBlock is RichParagraph richParagraph)) {
				return false;
			}
			return richParagraph.ListInlines.All(x => x.IsLineBreak || string.IsNullOrWhiteSpace(x.Text));
		}

		private static Block ToBlock(RichBlock richBlock,double scale) {
			if(richBlock is RichList richList) {
				List list=new List();
				list.MarkerStyle=richList.MarkerStyle;
				for(int i=0;i<richList.ListItems.Count;i++) {
					ListItem listItem=new ListItem();
					List<RichBlock> listBlocksItem=TrimEmptyParagraphs(richList.ListItems[i]);
					if(listBlocksItem.Count==0) {
						listBlocksItem.Add(new RichParagraph());
					}
					for(int b=0;b<listBlocksItem.Count;b++) {
						listItem.Blocks.Add(ToBlock(listBlocksItem[b],scale));
					}
					list.ListItems.Add(listItem);
				}
				return list;
			}
			RichParagraph richParagraph=(RichParagraph)richBlock;
			Paragraph paragraph=new Paragraph();
			paragraph.Margin=new Thickness(0);//as the internal eForm labels
			if(richParagraph.Alignment!=TextAlignment.Left) {
				paragraph.TextAlignment=richParagraph.Alignment;
			}
			List<RichInline> listInlines=MergeInlines(richParagraph.ListInlines);
			for(int i=0;i<listInlines.Count;i++) {
				if(listInlines[i].IsLineBreak) {
					paragraph.Inlines.Add(new LineBreak());
					continue;
				}
				paragraph.Inlines.Add(ToRun(listInlines[i],scale));
			}
			return paragraph;
		}

		///<summary>Neighboring pieces with the same formatting become one run. Trailing spaces before a line break or the paragraph end are dropped.
		///</summary>
		private static List<RichInline> MergeInlines(List<RichInline> listInlines) {
			List<RichInline> listMerged=new List<RichInline>();
			for(int i=0;i<listInlines.Count;i++) {
				RichInline richInline=listInlines[i];
				if(!richInline.IsLineBreak && string.IsNullOrEmpty(richInline.Text)) {
					continue;
				}
				RichInline richInlineLast=listMerged.LastOrDefault();
				if(richInlineLast!=null && richInlineLast.IsSameFormat(richInline)) {
					richInlineLast.Text+=richInline.Text;
					continue;
				}
				RichInline richInlineCopy=new RichInline() {
					Text=richInline.Text,IsLineBreak=richInline.IsLineBreak,IsBold=richInline.IsBold,IsItalic=richInline.IsItalic,
					IsUnderline=richInline.IsUnderline,ColorText=richInline.ColorText,FontSize=richInline.FontSize
				};
				listMerged.Add(richInlineCopy);
			}
			for(int i=0;i<listMerged.Count;i++) {
				bool isLastOnLine=i==listMerged.Count-1 || listMerged[i+1].IsLineBreak;
				if(!listMerged[i].IsLineBreak && isLastOnLine) {
					listMerged[i].Text=listMerged[i].Text.TrimEnd(' ');
				}
			}
			listMerged.RemoveAll(x => !x.IsLineBreak && x.Text=="");
			return listMerged;
		}

		private static Run ToRun(RichInline richInline,double scale) {
			Run run=new Run(richInline.Text);
			if(richInline.IsBold) {
				run.FontWeight=FontWeights.Bold;
			}
			if(richInline.IsItalic) {
				run.FontStyle=FontStyles.Italic;
			}
			if(richInline.IsUnderline) {
				run.TextDecorations=TextDecorations.Underline;
			}
			if(richInline.ColorText!=null) {
				run.Foreground=new SolidColorBrush(richInline.ColorText.Value);
			}
			double? fontSize=GetFontSizeKept(richInline.FontSize,scale);
			if(fontSize!=null) {
				run.FontSize=fontSize.Value;
			}
			return run;
		}

		///<summary>The pasted size relative to the main size, on the form's scale, between 50% and 300%. Null when it is (almost) the base size, so
		///the run uses the label's size.</summary>
		public static double? GetFontSizeKept(double? fontSize,double scale) {
			if(fontSize==null || fontSize<=0) {
				return null;
			}
			double fontSizeScaled=fontSize.Value*scale;
			fontSizeScaled=Math.Max(FONT_SIZE_BASE*FONT_SCALE_MIN,Math.Min(FONT_SIZE_BASE*FONT_SCALE_MAX,fontSizeScaled));
			fontSizeScaled=Math.Round(fontSizeScaled,2);
			if(Math.Abs(fontSizeScaled-FONT_SIZE_BASE)<0.25) {
				return null;
			}
			return fontSizeScaled;
		}

		private static string GetString(IDataObject dataObject,string format) {
			try {
				if(!dataObject.GetDataPresent(format)) {
					return null;
				}
				object data=dataObject.GetData(format);
				if(data is string text) {
					return text;
				}
				if(data is MemoryStream memoryStream) {
					return Encoding.UTF8.GetString(memoryStream.ToArray()).TrimEnd('\0');
				}
			}
			catch(Exception) {
				//The clipboard can fail in COM; the normal paste then runs.
			}
			return null;
		}
		#endregion Model to WPF
	}
}
