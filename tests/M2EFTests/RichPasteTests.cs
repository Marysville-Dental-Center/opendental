// v25_4-eforms-richtext-paste: RTF and HTML from the clipboard become a small FlowDocument section that keeps basic formatting. WPF code runs on
// an STA thread.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2EFTests {
	[TestClass]
	public class RichPasteTests {
		private const string RTF_WORD=@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fswiss Calibri;}{\f1\froman Times New Roman;}}"
			+@"{\colortbl;\red255\green0\blue0;\red0\green0\blue0;}"
			+@"\pard\f0\fs22\cf2 Normal text \b bold\b0  and \i italic\i0  and \ul under\ulnone  and \cf1 red\cf2  and \highlight1 marked\highlight0 .\par"
			+@"\pard\qc\f1\fs32\b Big heading\b0\fs22\par"
			+@"\pard\f0 Line one\line Line two\par}";

		private const string HTML_BROWSER="Version:0.9\r\nStartHTML:0000000105\r\nEndHTML:0000000999\r\nStartFragment:0000000141\r\nEndFragment:0000000900\r\n"
			+"<html><body>\r\n<!--StartFragment--><h1 style=\"font-family:Georgia\">Title</h1>"
			+"<p style=\"font-family:Arial;background-color:yellow;text-align:center\">Hello <b>bold</b> <i>italic</i> <u>under</u> "
			+"<span style=\"color:#0000ff;font-size:24px\">blue</span> <a href=\"https://example.invalid/\" style=\"color:#0645ad;text-decoration:underline\">link "
			+"text</a><img src=\"picture.png\"> end of the main paragraph with more words.</p>"
			+"<ul><li>one</li><li>two <strong>strong</strong></li></ul>"
			+"<ol type=\"a\"><li>first</li></ol>"
			+"<table><tr><td>cell a</td><td>cell b</td></tr><tr><td>cell c</td><td>cell d</td></tr></table>"
			+"<p>Line one<br>Line two &amp; more</p><script>alert('x')</script><style>p{color:red}</style>"
			+"<!--EndFragment-->\r\n</body></html>";

		private static Section ParseSection(string xaml) {
			return (Section)XamlReader.Parse(xaml);
		}

		private static List<Run> GetRuns(TextElement textElement) {
			List<Run> listRuns=new List<Run>();
			foreach(object child in LogicalTreeHelper.GetChildren(textElement)) {
				if(child is Run run) {
					listRuns.Add(run);
				}
				else if(child is TextElement textElementChild) {
					listRuns.AddRange(GetRuns(textElementChild));
				}
			}
			return listRuns;
		}

		///<summary>The first run that contains the text. Pieces with the same formatting are merged, so a run can hold more than the piece.</summary>
		private static Run FindRun(Section section,string text) {
			Run run=GetRuns(section).FirstOrDefault(x => x.Text.Contains(text));
			Assert.IsNotNull(run,"No run contains '"+text+"'.");
			return run;
		}

		[TestMethod]
		public void Rtf_KeepsBasicFormattingAndDropsFontsAndBackground() {
			Sta.Run(() => {
				string xaml=EFormRichPaste.ConvertRtfToXaml(RTF_WORD);
				Assert.IsNotNull(xaml);
				Assert.IsFalse(xaml.Contains("FontFamily"),"fonts map to the form's font: "+xaml);
				Assert.IsFalse(xaml.Contains("Background"),"background colors are dropped: "+xaml);
				Section section=ParseSection(xaml);
				Assert.AreEqual(FontWeights.Bold,FindRun(section,"bold").FontWeight);
				Assert.AreEqual(FontStyles.Italic,FindRun(section,"italic").FontStyle);
				Assert.IsTrue(FindRun(section,"under").TextDecorations.Any(x => x.Location==TextDecorationLocation.Underline));
				Assert.AreEqual(Colors.Red,((SolidColorBrush)FindRun(section,"red").Foreground).Color);
				Run runNormal=FindRun(section,"Normal");
				Assert.AreEqual(DependencyProperty.UnsetValue,runNormal.ReadLocalValue(TextElement.FontSizeProperty),"main size becomes the form's base");
				Assert.AreEqual(DependencyProperty.UnsetValue,runNormal.ReadLocalValue(TextElement.ForegroundProperty),"black is the default color");
				Run runHeading=FindRun(section,"Big heading");
				Assert.AreEqual(FontWeights.Bold,runHeading.FontWeight);
				Assert.IsTrue(runHeading.FontSize>16.5 && runHeading.FontSize<17.5,"16pt next to 11pt text is about 145%: "+runHeading.FontSize);
				Paragraph paragraphHeading=(Paragraph)runHeading.Parent;
				Assert.AreEqual(TextAlignment.Center,paragraphHeading.TextAlignment);
				Assert.AreEqual(new Thickness(0),paragraphHeading.Margin);
				Paragraph paragraphLines=section.Blocks.OfType<Paragraph>().Last();
				Assert.AreEqual(1,paragraphLines.Inlines.OfType<LineBreak>().Count(),"line breaks are kept");
			});
		}

		[TestMethod]
		public void Html_KeepsFormattingListsAndTableText() {
			Sta.Run(() => {
				string xaml=EFormRichPaste.ConvertHtmlToXaml(HTML_BROWSER);
				Assert.IsNotNull(xaml);
				Assert.IsFalse(xaml.Contains("FontFamily"),xaml);
				Assert.IsFalse(xaml.Contains("Background"),xaml);
				Assert.IsFalse(xaml.Contains("Hyperlink"),"links keep their text only: "+xaml);
				Assert.IsFalse(xaml.Contains("InlineUIContainer") || xaml.Contains("BlockUIContainer") || xaml.Contains("Image"),"images are dropped");
				Assert.IsFalse(xaml.Contains("<Table"),"tables are dropped");
				Assert.IsFalse(xaml.Contains("alert") || xaml.Contains("color:red"),"script and style content is not text");
				Section section=ParseSection(xaml);
				Run runTitle=FindRun(section,"Title");
				Assert.AreEqual(FontWeights.Bold,runTitle.FontWeight);
				Assert.AreEqual(23,runTitle.FontSize,0.01,"h1 is twice the main 16px size, so 200% of 11.5");
				Assert.AreEqual(FontWeights.Bold,FindRun(section,"bold").FontWeight);
				Assert.AreEqual(FontStyles.Italic,FindRun(section,"italic").FontStyle);
				Assert.IsTrue(FindRun(section,"under").TextDecorations.Any(x => x.Location==TextDecorationLocation.Underline));
				Run runBlue=FindRun(section,"blue");
				Assert.AreEqual(Color.FromRgb(0,0,255),((SolidColorBrush)runBlue.Foreground).Color);
				Assert.AreEqual(17.25,runBlue.FontSize,0.01,"24px next to 16px text is 150%");
				Run runLink=FindRun(section,"link text");
				Assert.AreEqual(DependencyProperty.UnsetValue,runLink.ReadLocalValue(TextElement.ForegroundProperty),"no link color");
				Assert.IsTrue(runLink.TextDecorations==null || runLink.TextDecorations.Count==0,"no link underline");
				Paragraph paragraphMain=(Paragraph)FindRun(section,"Hello").Parent;
				Assert.AreEqual(TextAlignment.Center,paragraphMain.TextAlignment);
				List<List> listLists=section.Blocks.OfType<List>().ToList();
				Assert.AreEqual(2,listLists.Count);
				Assert.AreEqual(TextMarkerStyle.Disc,listLists[0].MarkerStyle);
				Assert.AreEqual(2,listLists[0].ListItems.Count);
				Assert.AreEqual(FontWeights.Bold,GetRuns(listLists[0].ListItems.LastListItem).Single(x => x.Text=="strong").FontWeight);
				Assert.AreEqual(TextMarkerStyle.LowerLatin,listLists[1].MarkerStyle);
				string textAll=string.Join("|",section.Blocks.OfType<Paragraph>().Select(x => new TextRange(x.ContentStart,x.ContentEnd).Text));
				StringAssert.Contains(textAll,"cell a\tcell b");
				StringAssert.Contains(textAll,"cell c\tcell d");
				StringAssert.Contains(textAll,"Line two & more");
			});
		}

		[TestMethod]
		public void Html_FragmentMarkersAndOffsets() {
			Assert.AreEqual("<b>x</b>",EFormRichPaste.GetHtmlFragment("Version:0.9\r\n<html><body><!--StartFragment--><b>x</b><!--EndFragment--></body></html>"));
			string markup="<p>offsets</p>";
			string header="Version:0.9\r\nStartHTML:0000000000\r\nEndHTML:0000000000\r\nStartFragment:XXXXXXXXXX\r\nEndFragment:YYYYYYYYYY\r\n";
			int start=Encoding.UTF8.GetByteCount(header);
			string cfHtml=header.Replace("XXXXXXXXXX",start.ToString("D10")).Replace("YYYYYYYYYY",(start+Encoding.UTF8.GetByteCount(markup)).ToString("D10"))
				+markup;
			Assert.AreEqual(markup,EFormRichPaste.GetHtmlFragment(cfHtml));
			Assert.AreEqual("<i>plain markup</i>",EFormRichPaste.GetHtmlFragment("<i>plain markup</i>"));
		}

		[TestMethod]
		public void Html_ItemsWithoutTheirListAndUnclosedItems() {
			Sta.Run(() => {
				EFormRichPaste.RichDoc richDoc=EFormRichPaste.FromHtml("<li>alpha</li><li>beta");
				Assert.AreEqual(1,richDoc.ListBlocks.Count);
				Assert.AreEqual(2,((EFormRichPaste.RichList)richDoc.ListBlocks[0]).ListItems.Count);
				EFormRichPaste.RichDoc richDocUnclosed=EFormRichPaste.FromHtml("<ul><li>one<li>two</ul><p>after</p>");
				Assert.AreEqual(2,((EFormRichPaste.RichList)richDocUnclosed.ListBlocks[0]).ListItems.Count);
				Assert.AreEqual("one\ntwo\nafter",richDocUnclosed.GetText());
			});
		}

		[TestMethod]
		public void Css_ColorsAndSizes() {
			Sta.Run(() => {
				Assert.AreEqual(Color.FromRgb(255,0,0),EFormRichPaste.ParseColor("#f00"));
				Assert.AreEqual(Color.FromRgb(18,52,86),EFormRichPaste.ParseColor("#123456"));
				Assert.AreEqual(Color.FromRgb(1,2,3),EFormRichPaste.ParseColor("rgb(1, 2, 3)"));
				Assert.AreEqual(Color.FromRgb(0,128,0),EFormRichPaste.ParseColor("green"));
				Assert.IsNull(EFormRichPaste.ParseColor("inherit"));
				Assert.IsNull(EFormRichPaste.ParseColor("var(--x)"));
				Assert.AreEqual(16,EFormRichPaste.ParseCssFontSize("12pt",16).Value,0.001);
				Assert.AreEqual(20,EFormRichPaste.ParseCssFontSize("1.25em",16).Value,0.001);
				Assert.AreEqual(8,EFormRichPaste.ParseCssFontSize("50%",16).Value,0.001);
				Assert.AreEqual(18,EFormRichPaste.ParseCssFontSize("large",16).Value,0.001);
				Assert.IsNull(EFormRichPaste.ParseCssFontSize("calc(1em + 2px)",16));
			});
		}

		[TestMethod]
		public void FontSizes_AreRelativeAndClamped() {
			Assert.IsNull(EFormRichPaste.GetFontSizeKept(16,11.5/16),"main size");
			Assert.AreEqual(23,EFormRichPaste.GetFontSizeKept(32,11.5/16).Value,0.001);
			Assert.AreEqual(34.5,EFormRichPaste.GetFontSizeKept(200,11.5/16).Value,0.001,"at most 300%");
			Assert.AreEqual(5.75,EFormRichPaste.GetFontSizeKept(2,11.5/16).Value,0.001,"at least 50%");
			Assert.IsNull(EFormRichPaste.GetFontSizeKept(null,1));
		}

		[TestMethod]
		public void DataObject_PlainTextAndInternalCopiesUseTheNormalPaste() {
			Sta.Run(() => {
				DataObject dataObjectPlain=new DataObject();
				dataObjectPlain.SetData(DataFormats.UnicodeText,"just text");
				Assert.IsNull(EFormRichPaste.GetXamlForPaste(dataObjectPlain));
				DataObject dataObjectInternal=new DataObject();
				dataObjectInternal.SetData(DataFormats.Rtf,RTF_WORD);
				dataObjectInternal.SetData(DataFormats.Xaml,"<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"/>");
				Assert.IsNull(EFormRichPaste.GetXamlForPaste(dataObjectInternal),"copied inside the program");
				DataObject dataObjectWord=new DataObject();
				dataObjectWord.SetData(DataFormats.Rtf,RTF_WORD);
				dataObjectWord.SetData(DataFormats.UnicodeText,"Normal text bold");
				Assert.IsNotNull(EFormRichPaste.GetXamlForPaste(dataObjectWord));
				DataObject dataObjectBrowser=new DataObject();
				dataObjectBrowser.SetData(DataFormats.Html,HTML_BROWSER);
				Assert.IsNotNull(EFormRichPaste.GetXamlForPaste(dataObjectBrowser));
				DataObject dataObjectImageOnly=new DataObject();
				dataObjectImageOnly.SetData(DataFormats.Html,"<!--StartFragment--><img src=\"x.png\"><!--EndFragment-->");
				Assert.IsNull(EFormRichPaste.GetXamlForPaste(dataObjectImageOnly),"nothing readable left");
				Assert.IsNull(EFormRichPaste.GetXamlForPaste(null));
			});
		}

		[TestMethod]
		public void PastedSection_LoadsIntoALabelAndSavesAsALabel() {
			Sta.Run(() => {
				string xaml=EFormRichPaste.ConvertHtmlToXaml("<p>Hello <b>bold</b> world</p><ul><li>item</li></ul>");
				RichTextBox richTextBox=new RichTextBox();
				richTextBox.Document=EFormFields.DeserializeFlowDocument("<FlowDocument><Paragraph Margin=\"0,0,0,0\">Before </Paragraph></FlowDocument>");
				TextPointer textPointerEnd=richTextBox.Document.Blocks.FirstBlock.ContentEnd;
				richTextBox.Selection.Select(textPointerEnd,textPointerEnd);
				using(MemoryStream memoryStream=new MemoryStream(Encoding.UTF8.GetBytes(xaml))) {
					richTextBox.Selection.Load(memoryStream,DataFormats.Xaml);//the format the paste handler applies
				}
				string label=EFormFields.SerializeFlowDocument(richTextBox.Document);
				StringAssert.Contains(label,"Before");
				StringAssert.Contains(label,"FontWeight=\"Bold\"");
				StringAssert.Contains(label,"<List");
				FlowDocument flowDocumentReloaded=EFormFields.DeserializeFlowDocument(label);
				string text=new TextRange(flowDocumentReloaded.ContentStart,flowDocumentReloaded.ContentEnd).Text;
				StringAssert.Contains(text,"Hello bold world");
				StringAssert.Contains(text,"item");
			});
		}
	}
}
