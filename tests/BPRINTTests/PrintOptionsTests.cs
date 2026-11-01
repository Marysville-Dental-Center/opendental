// B-PRINT: the rules behind the Print Options window (Open Dental 25.3): the choice, its session memory, and printing a page range.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading;
using System.Windows.Documents;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using WpfControls;

namespace BPRINTTests {
	[TestClass]
	public class PrintOptionsTests {
		private static readonly List<string> _listPrinters=new List<string> { "Front Desk Laser","Back Office Inkjet","Microsoft Print to PDF" };

		[TestCleanup]
		public void TestCleanup() {
			PrintOptions.ClearRemembered();
		}

		[TestMethod]
		public void Choice_Errors() {
			PrintOptionsChoice printOptionsChoice=new PrintOptionsChoice { PrinterName="Front Desk Laser" };
			Assert.AreEqual("",printOptionsChoice.GetError());
			printOptionsChoice.PrinterName=" ";
			StringAssert.Contains(printOptionsChoice.GetError(),"printer");
			printOptionsChoice.PrinterName="Front Desk Laser";
			printOptionsChoice.Copies=0;
			StringAssert.Contains(printOptionsChoice.GetError(),"Copies");
			printOptionsChoice.Copies=PrintOptionsChoice.MAX_COPIES+1;
			StringAssert.Contains(printOptionsChoice.GetError(),"Copies");
			printOptionsChoice.Copies=2;
			printOptionsChoice.IsAllPages=false;
			printOptionsChoice.FromPage=3;
			printOptionsChoice.ToPage=2;
			Assert.AreNotEqual("",printOptionsChoice.GetError(),"The first page after the last.");
			printOptionsChoice.FromPage=0;
			Assert.AreNotEqual("",printOptionsChoice.GetError(),"Pages start at 1.");
			printOptionsChoice.FromPage=2;
			Assert.AreEqual("",printOptionsChoice.GetError());
			printOptionsChoice.IsAllPages=true;
			printOptionsChoice.FromPage=0;
			Assert.AreEqual("",printOptionsChoice.GetError(),"The range does not matter when printing every page.");
		}

		[TestMethod]
		public void Choice_PageRange() {
			PrintOptionsChoice printOptionsChoice=new PrintOptionsChoice { IsAllPages=false,FromPage=2,ToPage=3 };
			Assert.IsFalse(printOptionsChoice.IsPagePrinted(1));
			Assert.IsTrue(printOptionsChoice.IsPagePrinted(2));
			Assert.IsTrue(printOptionsChoice.IsPagePrinted(3));
			Assert.IsFalse(printOptionsChoice.IsPagePrinted(4));
			printOptionsChoice.IsAllPages=true;
			Assert.IsTrue(printOptionsChoice.IsPagePrinted(99));
		}

		[TestMethod]
		public void Choice_GoesIntoThePrinterSettings() {
			PrinterSettings printerSettings=new PrinterSettings();
			new PrintOptionsChoice { PrinterName="Front Desk Laser",Copies=3,IsCollated=false,IsAllPages=false,FromPage=2,ToPage=4 }.ApplyTo(printerSettings);
			Assert.AreEqual("Front Desk Laser",printerSettings.PrinterName);
			Assert.AreEqual(3,printerSettings.Copies);
			Assert.IsFalse(printerSettings.Collate);
			Assert.AreEqual(PrintRange.SomePages,printerSettings.PrintRange);
			Assert.AreEqual(2,printerSettings.FromPage);
			Assert.AreEqual(4,printerSettings.ToPage);
			new PrintOptionsChoice { PrinterName="Back Office Inkjet",Copies=1 }.ApplyTo(printerSettings);
			Assert.AreEqual(PrintRange.AllPages,printerSettings.PrintRange);
			Assert.IsTrue(printerSettings.Collate);
		}

		[TestMethod]
		public void Memory_KeepsPrinterCopiesAndCollation_PerSituation_ButNeverTheRange() {
			PrintOptions.Remember(PrintSituation.Statement,new PrintOptionsChoice { PrinterName="Back Office Inkjet",Copies=2,IsCollated=false,IsAllPages=false,FromPage=2,ToPage=3 });
			PrintOptionsChoice printOptionsChoice=PrintOptions.GetRemembered(PrintSituation.Statement);
			Assert.AreEqual("Back Office Inkjet",printOptionsChoice.PrinterName);
			Assert.AreEqual(2,printOptionsChoice.Copies);
			Assert.IsFalse(printOptionsChoice.IsCollated);
			Assert.IsTrue(printOptionsChoice.IsAllPages,"A page range belongs to one document.");
			Assert.IsNull(PrintOptions.GetRemembered(PrintSituation.Claim),"Each situation has its own memory.");
			printOptionsChoice.Copies=9;
			Assert.AreEqual(2,PrintOptions.GetRemembered(PrintSituation.Statement).Copies,"Callers get a copy.");
			PrintOptions.ClearRemembered();
			Assert.IsNull(PrintOptions.GetRemembered(PrintSituation.Statement));
		}

		[TestMethod]
		public void InitialChoice_RememberedPrinterFirst_ThenTheSituationsPrinter() {
			PrintOptionsChoice printOptionsChoice=PrintOptions.GetInitialChoice(PrintSituation.Rx,"Front Desk Laser",1,_listPrinters);
			Assert.AreEqual("Front Desk Laser",printOptionsChoice.PrinterName);
			Assert.AreEqual(1,printOptionsChoice.Copies);
			Assert.IsTrue(printOptionsChoice.IsAllPages);
			PrintOptions.Remember(PrintSituation.Rx,new PrintOptionsChoice { PrinterName="Back Office Inkjet",Copies=2 });
			Assert.AreEqual("Back Office Inkjet",PrintOptions.GetInitialChoice(PrintSituation.Rx,"Front Desk Laser",1,_listPrinters).PrinterName);
			Assert.AreEqual(2,PrintOptions.GetInitialChoice(PrintSituation.Rx,"Front Desk Laser",1,_listPrinters).Copies);
			PrintOptions.Remember(PrintSituation.Rx,new PrintOptionsChoice { PrinterName="Removed Printer",Copies=2 });
			Assert.AreEqual("Front Desk Laser",PrintOptions.GetInitialChoice(PrintSituation.Rx,"Front Desk Laser",1,_listPrinters).PrinterName,
				"A remembered printer that is gone is ignored.");
			Assert.AreEqual("Front Desk Laser",PrintOptions.GetInitialChoice(PrintSituation.Default,"Not Installed",1,_listPrinters).PrinterName,
				"A rule naming a printer that is not installed falls back to the first installed printer.");
			Assert.AreEqual(3,PrintOptions.GetInitialChoice(PrintSituation.Default,"Front Desk Laser",3,_listPrinters).Copies);
			Assert.AreEqual(1,PrintOptions.GetInitialChoice(PrintSituation.Default,"Front Desk Laser",0,_listPrinters).Copies);
		}

		///<summary>WPF printing (PrintOptionsWpf.TryPrintFixedDocument) without the Print Options window prints to the printer the rules pick with
		///one copy, as WinForms printing does. A choice remembered from any Print Options window (every WinForms print preview's Print button shows
		///one for the Default situation) only pre-fills the next window.</summary>
		[TestMethod]
		public void UnpromptedPrint_UsesTheRulesPrinterWithOneCopy_NeverTheRememberedChoice() {
			PrintOptions.Remember(PrintSituation.Default,new PrintOptionsChoice { PrinterName="Back Office Inkjet",Copies=3,IsCollated=false });
			PrintOptionsChoice printOptionsChoice=PrintOptions.GetChoiceToPrint(PrintSituation.Default,"Front Desk Laser",false,_listPrinters);
			Assert.AreEqual("Front Desk Laser",printOptionsChoice.PrinterName,"The printer from Setup, Printers, not the one remembered.");
			Assert.AreEqual(1,printOptionsChoice.Copies,"One copy, not the remembered three.");
			Assert.IsTrue(printOptionsChoice.IsCollated);
			Assert.IsTrue(printOptionsChoice.IsAllPages);
			//When the window is shown, it opens with the remembered choice.
			printOptionsChoice=PrintOptions.GetChoiceToPrint(PrintSituation.Default,"Front Desk Laser",true,_listPrinters);
			Assert.AreEqual("Back Office Inkjet",printOptionsChoice.PrinterName);
			Assert.AreEqual(3,printOptionsChoice.Copies);
			Assert.IsFalse(printOptionsChoice.IsCollated);
			//The same for another situation's rules.
			PrintOptions.Remember(PrintSituation.Rx,new PrintOptionsChoice { PrinterName="Microsoft Print to PDF",Copies=2 });
			printOptionsChoice=PrintOptions.GetChoiceToPrint(PrintSituation.Rx,"Back Office Inkjet",false,_listPrinters);
			Assert.AreEqual("Back Office Inkjet",printOptionsChoice.PrinterName);
			Assert.AreEqual(1,printOptionsChoice.Copies);
			//No printer from the rules: "" (the Windows default printer), never another installed printer.
			Assert.AreEqual("",PrintOptions.GetChoiceForUnpromptedPrint(null).PrinterName);
			Assert.AreEqual("",PrintOptions.GetChoiceToPrint(PrintSituation.Default,"",false,_listPrinters).PrinterName);
			Assert.AreEqual("Back Office Inkjet",PrintOptions.GetRemembered(PrintSituation.Default).PrinterName,"The memory itself is kept.");
		}

		[TestMethod]
		public void SituationText_IsTheSituationsDescription() {
			Assert.AreEqual("Statements",PrintOptions.GetSituationText(PrintSituation.Statement));
			Assert.AreEqual("Treatment Plans and Perio",PrintOptions.GetSituationText(PrintSituation.TPPerio));
		}

		#region Page range printing (System.Drawing)
		///<summary>A document that draws pageCount pages and counts the page handler's calls.</summary>
		private class CountingDocument:PrintDocument {
			public int PageCount;
			public int CountPrintPageCalls;
			public bool IsEndPrintCalled;

			protected override void OnPrintPage(PrintPageEventArgs e) {
				CountPrintPageCalls++;
				e.Graphics.DrawString("Page "+CountPrintPageCalls,SystemFonts.DefaultFont,Brushes.Black,10,10);
				e.HasMorePages=CountPrintPageCalls<PageCount;
			}

			protected override void OnEndPrint(PrintEventArgs e) {
				IsEndPrintCalled=true;
				base.OnEndPrint(e);
			}
		}

		private static void SkipWithoutPrinter() {
			if(PrintOptions.GetInstalledPrinters().Count==0) {
				Assert.Inconclusive("No printer is installed; page events need one (a print preview never sends anything to it).");
			}
		}

		[TestMethod]
		public void PageRange_OnlyThePagesInTheRangeReachThePrinter() {
			SkipWithoutPrinter();
			using(CountingDocument countingDocument=new CountingDocument { PageCount=5 }) {
				PreviewPrintController previewPrintController=new PreviewPrintController();
				PageRangePrintController pageRangePrintController=new PageRangePrintController(previewPrintController,2,3);
				countingDocument.PrintController=pageRangePrintController;
				countingDocument.Print();
				Assert.AreEqual(2,previewPrintController.GetPreviewPageInfo().Length,"Pages 2 and 3 were 'printed' (previewed).");
				Assert.AreEqual(2,pageRangePrintController.PagesPrinted);
				Assert.AreEqual(5,pageRangePrintController.PagesDrawn,
					"The handler drew every page for its page state (pages 1, 4 and 5 on scratch) and ended where it ends by itself.");
				Assert.AreEqual(5,countingDocument.CountPrintPageCalls);
				Assert.IsTrue(countingDocument.IsEndPrintCalled);
			}
		}

		[TestMethod]
		public void PageRange_PastTheEndPrintsWhatExists() {
			SkipWithoutPrinter();
			using(CountingDocument countingDocument=new CountingDocument { PageCount=2 }) {
				PreviewPrintController previewPrintController=new PreviewPrintController();
				PageRangePrintController pageRangePrintController=new PageRangePrintController(previewPrintController,2,9);
				countingDocument.PrintController=pageRangePrintController;
				countingDocument.Print();
				Assert.AreEqual(1,previewPrintController.GetPreviewPageInfo().Length);
				Assert.AreEqual(2,countingDocument.CountPrintPageCalls);
			}
		}

		///<summary>A document whose page handler applies the printer settings' page range itself, the way FormQuery.pd2_PrintPage and
		///FormReportComplex.pd2_PrintPage do: inside the first page event it lays out the pages before FromPage without drawing them, so the first
		///sheet it sends out is page FromPage, and it stops after ToPage. Each sheet records the document pages drawn on it.</summary>
		private class SelfFilteringDocument:PrintDocument,IRecordsPages {
			public int PageCount;
			private int _currentPage;
			private List<int> _listPagesOnSheet=new List<int>();

			public List<int> ListPagesOnLastSheet {
				get {
					return _listPagesOnSheet;
				}
			}

			protected override void OnBeginPrint(PrintEventArgs e) {
				_currentPage=1;
				base.OnBeginPrint(e);
			}

			private void DrawPage(PrintPageEventArgs e) {
				PrinterSettings printerSettings=e.PageSettings.PrinterSettings;
				bool isPrintablePage=printerSettings.PrintRange==PrintRange.AllPages
					|| (printerSettings.FromPage<=_currentPage && printerSettings.ToPage>=_currentPage);
				if(isPrintablePage) {
					e.Graphics.DrawString("Page "+_currentPage,SystemFonts.DefaultFont,Brushes.Black,10,10);
					_listPagesOnSheet.Add(_currentPage);
				}
				_currentPage++;
			}

			protected override void OnPrintPage(PrintPageEventArgs e) {
				_listPagesOnSheet=new List<int>();
				PrinterSettings printerSettings=e.PageSettings.PrinterSettings;
				do {
					DrawPage(e);
				} while(printerSettings.PrintRange!=PrintRange.AllPages && printerSettings.FromPage>=_currentPage && _currentPage<=PageCount);
				e.HasMorePages=_currentPage<=PageCount
					&& !(printerSettings.PrintRange!=PrintRange.AllPages && printerSettings.ToPage<_currentPage);
			}
		}

		///<summary>A document that reads the page range once when printing starts and then sends only those pages, the way RdlViewer.Print does.</summary>
		private class RangeAtStartDocument:PrintDocument,IRecordsPages {
			public int PageCount;
			private int _currentPage;
			private int _lastPage;
			private List<int> _listPagesOnSheet=new List<int>();

			public List<int> ListPagesOnLastSheet {
				get {
					return _listPagesOnSheet;
				}
			}

			protected override void OnBeginPrint(PrintEventArgs e) {
				_currentPage=1;
				_lastPage=PageCount;
				if(PrinterSettings.PrintRange==PrintRange.SomePages) {
					_currentPage=Math.Max(1,PrinterSettings.FromPage);
					_lastPage=Math.Min(PageCount,PrinterSettings.ToPage);
				}
				base.OnBeginPrint(e);
			}

			protected override void OnPrintPage(PrintPageEventArgs e) {
				e.Graphics.DrawString("Page "+_currentPage,SystemFonts.DefaultFont,Brushes.Black,10,10);
				_listPagesOnSheet=new List<int> { _currentPage };
				_currentPage++;
				e.HasMorePages=_currentPage<=_lastPage;
			}
		}

		///<summary>A document that ignores the page range and records each page it draws.</summary>
		private class IgnoringRangeDocument:PrintDocument,IRecordsPages {
			public int PageCount;
			private int _currentPage;
			private List<int> _listPagesOnSheet=new List<int>();

			public List<int> ListPagesOnLastSheet {
				get {
					return _listPagesOnSheet;
				}
			}

			protected override void OnBeginPrint(PrintEventArgs e) {
				_currentPage=1;
				base.OnBeginPrint(e);
			}

			protected override void OnPrintPage(PrintPageEventArgs e) {
				e.Graphics.DrawString("Page "+_currentPage,SystemFonts.DefaultFont,Brushes.Black,10,10);
				_listPagesOnSheet=new List<int> { _currentPage };
				_currentPage++;
				e.HasMorePages=_currentPage<=PageCount;
			}
		}

		private interface IRecordsPages {
			List<int> ListPagesOnLastSheet { get; }
		}

		///<summary>A document that describes in words what it drew on its last sheet.</summary>
		private interface IDescribesSheet {
			string LastSheetText { get; }
		}

		///<summary>A print preview controller that records, for each sheet that reaches it, the document pages drawn on that sheet.</summary>
		private class RecordingPreviewPrintController:PreviewPrintController {
			public List<string> ListSheets=new List<string>();

			public override void OnEndPage(PrintDocument document,PrintPageEventArgs e) {
				if(document is IDescribesSheet describesSheet) {
					ListSheets.Add(describesSheet.LastSheetText);
				}
				else {
					ListSheets.Add(string.Join(",",((IRecordsPages)document).ListPagesOnLastSheet));
				}
				base.OnEndPage(document,e);
			}
		}

		///<summary>A document modelled on User Query printing (FormQuery.pagePrinter and pd2_PrintPage): a title on the first page only
		///(_headerPrinted), rows, then totals and a summary. Like FormQuery it resets its state only when its handler reaches its own end (the summary),
		///never in OnBeginPrint; before each print or preview FormQuery.PrintReport resets only the page and row counters
		///(ResetLikePrintReport). Each page holds LINES_PER_PAGE lines; the title takes two.</summary>
		private class QueryLikeDocument:PrintDocument,IDescribesSheet {
			public const int LINES_PER_PAGE=10;
			public int RowCount;
			///<summary>FormQuery._totalPages, shown in labelTotPages; set when the handler reaches its end.</summary>
			public int TotalPages;
			private bool _headerPrinted;
			private int _linesPrinted;
			private int _pagesPrinted;
			private bool _tablePrinted;
			private bool _totalsPrinted;
			private bool _summaryPrinted;
			private List<string> _listOnSheet=new List<string>();

			public string LastSheetText {
				get {
					return string.Join(" ",_listOnSheet);
				}
			}

			///<summary>What FormQuery.PrintReport resets before it creates the printout.</summary>
			public void ResetLikePrintReport() {
				_pagesPrinted=0;
				_linesPrinted=0;
			}

			private void Draw(PrintPageEventArgs e,string text) {
				e.Graphics.DrawString(text,SystemFonts.DefaultFont,Brushes.Black,10,10+12*_listOnSheet.Count);
				_listOnSheet.Add(text);
			}

			protected override void OnPrintPage(PrintPageEventArgs e) {
				_listOnSheet=new List<string>();
				int linesLeft=LINES_PER_PAGE;
				Draw(e,"Page "+(_pagesPrinted+1));
				if(!_headerPrinted) {
					Draw(e,"Title");
					linesLeft-=2;
					_headerPrinted=true;
				}
				int rowFirst=_linesPrinted+1;
				while(linesLeft>0 && _linesPrinted<RowCount) {
					_linesPrinted++;
					linesLeft--;
					if(_linesPrinted==RowCount) {
						_tablePrinted=true;
					}
				}
				if(_linesPrinted>=rowFirst) {
					Draw(e,"Rows "+rowFirst+"-"+_linesPrinted);
				}
				if(RowCount==0) {
					_tablePrinted=true;
				}
				if(_tablePrinted && !_totalsPrinted && linesLeft>0) {
					Draw(e,"Totals");
					linesLeft--;
					_totalsPrinted=true;
				}
				if(_totalsPrinted && !_summaryPrinted && linesLeft>0) {
					Draw(e,"Summary");
					linesLeft--;
					_summaryPrinted=true;
				}
				_pagesPrinted++;
				if(_summaryPrinted) {
					e.HasMorePages=false;
					TotalPages=_pagesPrinted;
					_pagesPrinted=0;
					_linesPrinted=0;
					_headerPrinted=false;
					_tablePrinted=false;
					_totalsPrinted=false;
					_summaryPrinted=false;
				}
				else {
					e.HasMorePages=true;
				}
			}
		}

		///<summary>Prints every page the way PrinterL does when the Print Options window chose All, and returns what was on each sheet.</summary>
		private static List<string> PrintAllPages(PrintDocument printDocument) {
			RecordingPreviewPrintController recordingPreviewPrintController=new RecordingPreviewPrintController();
			printDocument.PrintController=recordingPreviewPrintController;
			new PrintOptionsChoice { PrinterName=printDocument.PrinterSettings.PrinterName,IsAllPages=true }.ApplyTo(printDocument.PrinterSettings);
			PageRangePrintController.ApplyTo(printDocument);
			printDocument.Print();
			Assert.AreEqual(recordingPreviewPrintController.ListSheets.Count,recordingPreviewPrintController.GetPreviewPageInfo().Length);
			return recordingPreviewPrintController.ListSheets;
		}

		///<summary>Prints the document the way PrinterL does after the Print Options window chose pages fromPage to toPage, and returns the document
		///pages on each sheet that reached the printer (here a print preview).</summary>
		private static List<string> PrintChosenPages(PrintDocument printDocument,int fromPage,int toPage) {
			RecordingPreviewPrintController recordingPreviewPrintController=new RecordingPreviewPrintController();
			printDocument.PrintController=recordingPreviewPrintController;
			new PrintOptionsChoice { PrinterName=printDocument.PrinterSettings.PrinterName,IsAllPages=false,FromPage=fromPage,ToPage=toPage }
				.ApplyTo(printDocument.PrinterSettings);
			PageRangePrintController.ApplyTo(printDocument);
			printDocument.Print();
			Assert.AreEqual(recordingPreviewPrintController.ListSheets.Count,recordingPreviewPrintController.GetPreviewPageInfo().Length);
			return recordingPreviewPrintController.ListSheets;
		}

		[TestMethod]
		public void PageRange_HandlerThatSkipsPagesItself_PrintsExactlyTheChosenPages() {
			SkipWithoutPrinter();
			using(SelfFilteringDocument selfFilteringDocument=new SelfFilteringDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "3","4","5" },PrintChosenPages(selfFilteringDocument,3,5),"Reports and User Query: pages 3 to 5, once each.");
			}
			using(SelfFilteringDocument selfFilteringDocument=new SelfFilteringDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "2" },PrintChosenPages(selfFilteringDocument,2,2),"A single page.");
			}
			using(SelfFilteringDocument selfFilteringDocument=new SelfFilteringDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "1","2" },PrintChosenPages(selfFilteringDocument,1,2));
			}
			using(SelfFilteringDocument selfFilteringDocument=new SelfFilteringDocument { PageCount=4 }) {
				CollectionAssert.AreEqual(new[] { "3","4" },PrintChosenPages(selfFilteringDocument,3,9),"Past the end: what exists.");
			}
		}

		///<summary>User Query resets its page state only when its handler reaches its own end. A page range must not cut the handler short, or the
		///next preview or print starts with the state the range print left behind (no title, or totals and summary right after page 1's rows).</summary>
		[TestMethod]
		public void PageRange_QueryLikeHandler_ARangePrintLeavesNoStateBehindForTheNextPrint() {
			SkipWithoutPrinter();
			string[] arrayAllSheets=new[] { "Page 1 Title Rows 1-8","Page 2 Rows 9-18","Page 3 Rows 19-28","Page 4 Rows 29-38","Page 5 Rows 39-40 Totals Summary" };
			using(QueryLikeDocument queryLikeDocument=new QueryLikeDocument { RowCount=40 }) {
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(arrayAllSheets,PrintAllPages(queryLikeDocument),"Five pages.");
				queryLikeDocument.TotalPages=0;
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(new[] { arrayAllSheets[0],arrayAllSheets[1] },PrintChosenPages(queryLikeDocument,1,2),"Pages 1-2 of 5.");
				Assert.AreEqual(5,queryLikeDocument.TotalPages,"The handler reached its own end (and reset its state) although only pages 1-2 printed.");
				//FormQuery.butPrint_Click then shows the preview again (PrintReport(true)), and the user prints everything.
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(arrayAllSheets,PrintAllPages(queryLikeDocument),"The title is on page 1 again and all five pages print.");
			}
			//A range that ends after the table and totals but before the summary (on page 6).
			string[] arrayAllSheetsSix=new[] { "Page 1 Title Rows 1-8","Page 2 Rows 9-18","Page 3 Rows 19-28","Page 4 Rows 29-38",
				"Page 5 Rows 39-47 Totals","Page 6 Summary" };
			using(QueryLikeDocument queryLikeDocument=new QueryLikeDocument { RowCount=47 }) {
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(arrayAllSheetsSix,PrintAllPages(queryLikeDocument),"Six pages.");
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(arrayAllSheetsSix.Take(5).ToArray(),PrintChosenPages(queryLikeDocument,1,5),"Pages 1-5 of 6.");
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(arrayAllSheetsSix,PrintAllPages(queryLikeDocument),
					"All six pages again, not page 1 with the totals and summary under its rows.");
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(new[] { arrayAllSheetsSix[2] },PrintChosenPages(queryLikeDocument,3,3),"Page 3 alone.");
				queryLikeDocument.ResetLikePrintReport();
				CollectionAssert.AreEqual(arrayAllSheetsSix,PrintAllPages(queryLikeDocument));
			}
		}

		[TestMethod]
		public void PageRange_HandlerThatReadsTheRangeAtStart_PrintsExactlyTheChosenPages() {
			SkipWithoutPrinter();
			using(RangeAtStartDocument rangeAtStartDocument=new RangeAtStartDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "3","4","5" },PrintChosenPages(rangeAtStartDocument,3,5),"RDL reports: pages 3 to 5, once each.");
			}
			using(RangeAtStartDocument rangeAtStartDocument=new RangeAtStartDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "2" },PrintChosenPages(rangeAtStartDocument,2,2));
			}
		}

		[TestMethod]
		public void PageRange_HandlerThatIgnoresTheRange_PrintsExactlyTheChosenPages() {
			SkipWithoutPrinter();
			using(IgnoringRangeDocument ignoringRangeDocument=new IgnoringRangeDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "3","4","5" },PrintChosenPages(ignoringRangeDocument,3,5));
			}
			using(IgnoringRangeDocument ignoringRangeDocument=new IgnoringRangeDocument { PageCount=7 }) {
				CollectionAssert.AreEqual(new[] { "2" },PrintChosenPages(ignoringRangeDocument,2,2));
			}
		}

		[TestMethod]
		public void PageRange_ApplyTo_TheControllerAloneAppliesTheRange_AndAReprintWithoutRangeGetsTheOwnController() {
			using(PrintDocument printDocument=new PrintDocument()) {
				PrintController printControllerOwn=new StandardPrintController();
				printDocument.PrintController=printControllerOwn;
				printDocument.PrinterSettings.PrintRange=PrintRange.SomePages;
				printDocument.PrinterSettings.FromPage=3;
				printDocument.PrinterSettings.ToPage=5;
				PageRangePrintController.ApplyTo(printDocument);
				PageRangePrintController pageRangePrintController=printDocument.PrintController as PageRangePrintController;
				Assert.IsNotNull(pageRangePrintController);
				Assert.AreSame(printControllerOwn,pageRangePrintController.PrintControllerInner);
				Assert.AreEqual(PrintRange.AllPages,printDocument.PrinterSettings.PrintRange,
					"Handlers that skip pages themselves must not apply the range a second time.");
				Assert.AreEqual(PrintRange.AllPages,printDocument.DefaultPageSettings.PrinterSettings.PrintRange,
					"The page events see the same settings.");
				//Printed again with All: the document's own controller comes back.
				PageRangePrintController.ApplyTo(printDocument);
				Assert.AreSame(printControllerOwn,printDocument.PrintController);
				//Printed again with another range: one wrapper around the own controller, never a wrapper around a wrapper.
				printDocument.PrinterSettings.PrintRange=PrintRange.SomePages;
				printDocument.PrinterSettings.FromPage=2;
				printDocument.PrinterSettings.ToPage=2;
				PageRangePrintController.ApplyTo(printDocument);
				printDocument.PrinterSettings.PrintRange=PrintRange.SomePages;
				PageRangePrintController.ApplyTo(printDocument);
				Assert.AreSame(printControllerOwn,((PageRangePrintController)printDocument.PrintController).PrintControllerInner);
				PageRangePrintController.ApplyTo(null);//no error
			}
		}
		#endregion Page range printing (System.Drawing)

		#region Page range printing (WPF)
		///<summary>Runs the action on an STA thread, as WPF documents need, and rethrows what it threw.</summary>
		private static void RunSta(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				throw new AssertFailedException(exception.Message,exception);
			}
		}

		private static FixedDocument NewFixedDocument(int pageCount) {
			FixedDocument fixedDocument=new FixedDocument();
			for(int i=0;i<pageCount;i++) {
				PageContent pageContent=new PageContent();
				FixedPage fixedPage=new FixedPage { Width=816,Height=1056,Tag=i+1 };
				pageContent.Child=fixedPage;
				fixedDocument.Pages.Add(pageContent);
			}
			return fixedDocument;
		}

		[TestMethod]
		public void WpfPageRange_ShowsOnlyThePagesInTheRange() {
			RunSta(() => {
				FixedDocument fixedDocument=NewFixedDocument(5);
				PageRangeDocumentPaginator pageRangeDocumentPaginator=new PageRangeDocumentPaginator(fixedDocument.DocumentPaginator,2,3);
				Assert.AreEqual(2,pageRangeDocumentPaginator.PageCount);
				Assert.IsTrue(pageRangeDocumentPaginator.IsPageCountValid);
				Assert.AreEqual(2,((FixedPage)pageRangeDocumentPaginator.GetPage(0).Visual).Tag);
				Assert.AreEqual(3,((FixedPage)pageRangeDocumentPaginator.GetPage(1).Visual).Tag);
				Assert.AreEqual(2,new PageRangeDocumentPaginator(fixedDocument.DocumentPaginator,4,9).PageCount,"Past the end: what exists.");
				Assert.AreEqual(0,new PageRangeDocumentPaginator(fixedDocument.DocumentPaginator,6,7).PageCount,"Nothing in the range.");
				Assert.AreSame(fixedDocument,pageRangeDocumentPaginator.Source);
			});
		}
		#endregion Page range printing (WPF)
	}
}
