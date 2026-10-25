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
using OpenDentBusiness;
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
				Assert.AreEqual(3,pageRangePrintController.PagesDrawn,"The handler drew page 1 for its page state, then printing stopped after page 3.");
				Assert.AreEqual(3,countingDocument.CountPrintPageCalls);
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
