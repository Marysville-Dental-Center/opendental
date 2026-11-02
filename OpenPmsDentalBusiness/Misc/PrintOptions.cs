// B-PRINT: the rules behind the Print Options window (Open Dental 25.3 "printer options window when printing from some areas").
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>What the user picked in the Print Options window: printer, copies, collation and page range.</summary>
	public class PrintOptionsChoice {
		public const int MAX_COPIES=999;
		///<summary>Name of an installed printer.</summary>
		public string PrinterName="";
		public int Copies=1;
		public bool IsCollated=true;
		///<summary>True to print every page; otherwise FromPage to ToPage (1-based, inclusive).</summary>
		public bool IsAllPages=true;
		public int FromPage=1;
		public int ToPage=1;

		///<summary>An error message for the user, or "" when the choice can be printed.</summary>
		public string GetError() {
			if(string.IsNullOrWhiteSpace(PrinterName)) {
				return "Please select a printer.";
			}
			if(Copies<1 || Copies>MAX_COPIES) {
				return "Copies must be between 1 and "+MAX_COPIES+".";
			}
			if(!IsAllPages) {
				if(FromPage<1 || ToPage<1) {
					return "Page numbers start at 1.";
				}
				if(FromPage>ToPage) {
					return "The first page must not come after the last page.";
				}
			}
			return "";
		}

		///<summary>True if the 1-based page is printed.</summary>
		public bool IsPagePrinted(int pageNumber) {
			return PrintOptions.IsPageInRange(pageNumber,IsAllPages,FromPage,ToPage);
		}

		///<summary>Puts the choice into the printer settings of a System.Drawing print job.</summary>
		public void ApplyTo(PrinterSettings printerSettings) {
			printerSettings.PrinterName=PrinterName;
			printerSettings.Copies=(short)Math.Max(1,Math.Min(MAX_COPIES,Copies));
			printerSettings.Collate=IsCollated;
			if(IsAllPages) {
				printerSettings.PrintRange=PrintRange.AllPages;
				return;
			}
			printerSettings.PrintRange=PrintRange.SomePages;
			printerSettings.FromPage=FromPage;
			printerSettings.ToPage=ToPage;
		}

		public PrintOptionsChoice Copy() {
			return (PrintOptionsChoice)MemberwiseClone();
		}
	}

	///<summary>Session memory and helpers for the Print Options window.</summary>
	public static class PrintOptions {
		///<summary>The last choice per print situation, for this session only. Page ranges are not remembered: they belong to one document.</summary>
		private static readonly Dictionary<PrintSituation,PrintOptionsChoice> _dictLastChoices=new Dictionary<PrintSituation,PrintOptionsChoice>();
		private static readonly object _lock=new object();

		///<summary>Remembers the printer, copies and collation chosen for the situation until the program closes.</summary>
		public static void Remember(PrintSituation printSituation,PrintOptionsChoice printOptionsChoice) {
			if(printOptionsChoice==null) {
				return;
			}
			PrintOptionsChoice printOptionsChoiceKept=printOptionsChoice.Copy();
			printOptionsChoiceKept.IsAllPages=true;
			printOptionsChoiceKept.FromPage=1;
			printOptionsChoiceKept.ToPage=1;
			lock(_lock) {
				_dictLastChoices[printSituation]=printOptionsChoiceKept;
			}
		}

		///<summary>The remembered choice for the situation, or null. A copy, so the caller may change it.</summary>
		public static PrintOptionsChoice GetRemembered(PrintSituation printSituation) {
			lock(_lock) {
				if(_dictLastChoices.TryGetValue(printSituation,out PrintOptionsChoice printOptionsChoice)) {
					return printOptionsChoice.Copy();
				}
			}
			return null;
		}

		///<summary>Forgets every remembered choice (log off, tests).</summary>
		public static void ClearRemembered() {
			lock(_lock) {
				_dictLastChoices.Clear();
			}
		}

		///<summary>The choice the window opens with: the remembered one for the situation when its printer is still installed, otherwise the
		///printer the situation's rules picked (printerNameFromRules), with one copy. The page range always starts as All.</summary>
		public static PrintOptionsChoice GetInitialChoice(PrintSituation printSituation,string printerNameFromRules,short copiesFromSettings,
			IList<string> listInstalledPrinters)
		{
			PrintOptionsChoice printOptionsChoice=GetRemembered(printSituation);
			if(printOptionsChoice!=null && listInstalledPrinters!=null && listInstalledPrinters.Contains(printOptionsChoice.PrinterName)) {
				return printOptionsChoice;
			}
			printOptionsChoice=new PrintOptionsChoice();
			printOptionsChoice.PrinterName=printerNameFromRules??"";
			if(listInstalledPrinters!=null && listInstalledPrinters.Count>0 && !listInstalledPrinters.Contains(printOptionsChoice.PrinterName)) {
				printOptionsChoice.PrinterName=listInstalledPrinters[0];
			}
			printOptionsChoice.Copies=Math.Max(1,(int)copiesFromSettings);
			return printOptionsChoice;
		}

		///<summary>The choice a WPF print starts from. When the Print Options window will be shown (isWindowShown), the window's starting values
		///(GetInitialChoice: the choice remembered for the situation, otherwise the rules' printer). Otherwise GetChoiceForUnpromptedPrint: a choice
		///remembered from the window only ever pre-fills the window, it never redirects a print that shows no window.</summary>
		public static PrintOptionsChoice GetChoiceToPrint(PrintSituation printSituation,string printerNameFromRules,bool isWindowShown,
			IList<string> listInstalledPrinters)
		{
			if(isWindowShown) {
				return GetInitialChoice(printSituation,printerNameFromRules,1,listInstalledPrinters);
			}
			return GetChoiceForUnpromptedPrint(printerNameFromRules);
		}

		///<summary>What a print without the Print Options window prints with: the printer the situation's rules picked (Setup, Printers; "" for the
		///Windows default), one copy, collated, every page. The same as WinForms printing (OpenPmsDental.PrinterL.SetPrinter) without the window. Never
		///the remembered choice.</summary>
		public static PrintOptionsChoice GetChoiceForUnpromptedPrint(string printerNameFromRules) {
			PrintOptionsChoice printOptionsChoice=new PrintOptionsChoice();
			printOptionsChoice.PrinterName=printerNameFromRules??"";
			printOptionsChoice.Copies=1;
			printOptionsChoice.IsCollated=true;
			printOptionsChoice.IsAllPages=true;
			return printOptionsChoice;
		}

		///<summary>True if the 1-based page is inside the range. Every page is when isAllPages is true.</summary>
		public static bool IsPageInRange(int pageNumber,bool isAllPages,int fromPage,int toPage) {
			if(isAllPages) {
				return true;
			}
			return pageNumber>=fromPage && pageNumber<=toPage;
		}

		///<summary>The names of the installed printers, in the order Windows lists them. Empty when none are installed or the spooler is down.</summary>
		public static List<string> GetInstalledPrinters() {
			List<string> listPrinters=new List<string>();
			try {
				foreach(string printerName in PrinterSettings.InstalledPrinters) {
					listPrinters.Add(printerName);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return listPrinters;
		}

		///<summary>The window title's situation text, for example "Statements".</summary>
		public static string GetSituationText(PrintSituation printSituation) {
			return printSituation.GetDescription();
		}
	}

	///<summary>Prints only the pages of a range. System.Drawing printing calls the document's PrintPage handler for every page whatever the
	///printer settings say, so this controller still lets the handler draw each page (its page state must advance), but pages outside the range
	///are drawn on a scratch bitmap and never reach the printer. The handler always runs to its own last page, also when the range ends earlier:
	///some handlers (User Query) reset their page state only when they reach their end, and a print cut short would leave that state behind for
	///the next print or preview.</summary>
	public class PageRangePrintController:PrintController {
		private readonly PrintController _printControllerInner;
		private readonly int _fromPage;
		private readonly int _toPage;
		private int _pageNumber;
		private bool _isCurrentPagePrinted;
		private Bitmap _bitmapScratch;
		private Graphics _graphicsScratch;

		///<summary>fromPage and toPage are 1-based and inclusive. printControllerInner does the real printing: pass the document's own
		///PrintController, so the usual "Printing page" status window stays.</summary>
		public PageRangePrintController(PrintController printControllerInner,int fromPage,int toPage) {
			_printControllerInner=printControllerInner;
			_fromPage=fromPage;
			_toPage=toPage;
		}

		///<summary>Sets up page-range printing for a document whose printer settings were just chosen (PrinterL.SetPrinter). When the settings ask for
		///some pages (PrintRange.SomePages), the document's PrintController is wrapped in a PageRangePrintController for FromPage to ToPage, and the
		///settings' PrintRange goes back to AllPages, so this controller is the only thing that leaves pages out. Some page handlers apply
		///FromPage/ToPage themselves (ReportComplex reports, User Query, RDL reports): they skip the earlier pages and send page FromPage first. With
		///both applying the range, pages 3-5 would print only page 5. With PrintRange at AllPages those handlers draw every page and the controller
		///sends FromPage to ToPage, the same as for a handler that ignores the range. A document printed again without a range gets its own
		///controller back.</summary>
		public static void ApplyTo(PrintDocument printDocument) {
			if(printDocument==null) {
				return;
			}
			//A document printed again keeps no range from the last time.
			PrintController printControllerBase=printDocument.PrintController;
			if(printControllerBase is PageRangePrintController pageRangePrintController) {
				printControllerBase=pageRangePrintController.PrintControllerInner;
			}
			if(printDocument.PrinterSettings.PrintRange!=PrintRange.SomePages) {
				if(printDocument.PrintController!=printControllerBase) {
					printDocument.PrintController=printControllerBase;
				}
				return;
			}
			printDocument.PrintController=new PageRangePrintController(printControllerBase,
				printDocument.PrinterSettings.FromPage,printDocument.PrinterSettings.ToPage);
			printDocument.PrinterSettings.PrintRange=PrintRange.AllPages;
		}

		///<summary>The controller that does the real printing.</summary>
		public PrintController PrintControllerInner {
			get {
				return _printControllerInner;
			}
		}

		///<summary>The number of pages the document drew so far, printed or not.</summary>
		public int PagesDrawn {
			get {
				return _pageNumber;
			}
		}

		///<summary>The number of pages that went to the printer.</summary>
		public int PagesPrinted { get; private set; }

		public override bool IsPreview {
			get {
				return _printControllerInner.IsPreview;
			}
		}

		public override void OnStartPrint(PrintDocument document,PrintEventArgs e) {
			_pageNumber=0;
			PagesPrinted=0;
			base.OnStartPrint(document,e);//The base class keeps the device mode the page events are built from.
			_printControllerInner.OnStartPrint(document,e);
		}

		public override Graphics OnStartPage(PrintDocument document,PrintPageEventArgs e) {
			_pageNumber++;
			_isCurrentPagePrinted=PrintOptions.IsPageInRange(_pageNumber,false,_fromPage,_toPage);
			if(_isCurrentPagePrinted) {
				PagesPrinted++;
				return _printControllerInner.OnStartPage(document,e);
			}
			DisposeScratch();
			//A tiny bitmap is enough: what is drawn is thrown away. It gets the printer's resolution and the printer's units (1/100 inch), so the
			//handler measures text the way it does on the printed pages and the page breaks stay where they would be.
			float dpiX=300;
			float dpiY=300;
			try {
				PrinterResolution printerResolution=e.PageSettings?.PrinterResolution;
				if(printerResolution!=null && printerResolution.X>0 && printerResolution.Y>0) {
					dpiX=printerResolution.X;
					dpiY=printerResolution.Y;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			_bitmapScratch=new Bitmap(8,8);
			_bitmapScratch.SetResolution(dpiX,dpiY);
			_graphicsScratch=Graphics.FromImage(_bitmapScratch);
			_graphicsScratch.PageUnit=GraphicsUnit.Inch;
			_graphicsScratch.PageScale=0.01f;
			return _graphicsScratch;
		}

		public override void OnEndPage(PrintDocument document,PrintPageEventArgs e) {
			if(_isCurrentPagePrinted) {
				_printControllerInner.OnEndPage(document,e);
			}
			else {
				DisposeScratch();
			}
			//No early stop after ToPage: the handler runs to its own last page, where handlers such as FormQuery.pd2_PrintPage reset their page
			//state for the next print or preview. Pages after the range are drawn on the scratch bitmap and thrown away, like those before it.
		}

		public override void OnEndPrint(PrintDocument document,PrintEventArgs e) {
			DisposeScratch();
			_printControllerInner.OnEndPrint(document,e);
			base.OnEndPrint(document,e);
		}

		private void DisposeScratch() {
			_graphicsScratch?.Dispose();
			_graphicsScratch=null;
			_bitmapScratch?.Dispose();
			_bitmapScratch=null;
		}
	}
}
