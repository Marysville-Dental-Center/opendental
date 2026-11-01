// B-PRINT: printing WPF documents through the Print Options window (Open Dental 25.3).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Documents;
using CodeBase;
using OpenDental;
using OpenDentBusiness;

namespace WpfControls {
	///<summary>Prints a WPF FixedDocument to the printer the print situation's rules pick (Setup, Printers), showing the Print Options window when
	///the situation has Prompt on or the caller forces it (Print button of the print preview). This takes the place of the Windows print dialog
	///for WPF printing.</summary>
	public static class PrintOptionsWpf {
		///<summary>Returns true if the document was sent to the printer; false if the user cancelled or printing failed (a message is shown).</summary>
		public static bool TryPrintFixedDocument(FixedDocument fixedDocument,string description,PrintSituation printSituation=PrintSituation.Default,
			bool isPromptForced=false)
		{
			if(fixedDocument==null) {
				return false;
			}
			List<string> listPrinters=PrintOptions.GetInstalledPrinters();
			if(listPrinters.Count==0) {
				MsgBox.Show(Lang.g(nameof(PrintOptionsWpf),PrintoutErrorCode.NoInstalledPrinter.GetDescription()));
				return false;
			}
			string printerNameFromRules=GetPrinterNameForSituation(printSituation,out bool isPrompt);
			bool isWindowShown=isPrompt || isPromptForced;
			//The remembered choice only pre-fills the window. Without the window: the rules' printer, one copy, as WinForms printing does.
			PrintOptionsChoice printOptionsChoice=PrintOptions.GetChoiceToPrint(printSituation,printerNameFromRules,isWindowShown,listPrinters);
			if(isWindowShown) {
				FrmPrintOptions frmPrintOptions=new FrmPrintOptions();
				frmPrintOptions.PrintSituation_=printSituation;
				frmPrintOptions.PrintOptionsChoice_=printOptionsChoice;
				frmPrintOptions.ListPrinters=listPrinters;
				frmPrintOptions.ShowDialog();
				if(!frmPrintOptions.IsDialogOK) {
					return false;
				}
				printOptionsChoice=frmPrintOptions.PrintOptionsChoice_;
				PrintOptions.Remember(printSituation,printOptionsChoice);
			}
			try {
				System.Windows.Controls.PrintDialog printDialog=new System.Windows.Controls.PrintDialog();
				PrintQueue printQueue=FindPrintQueue(printOptionsChoice.PrinterName);
				if(printQueue!=null) {
					printDialog.PrintQueue=printQueue;
					printDialog.PrintTicket=printQueue.DefaultPrintTicket.Clone();
				}
				printDialog.PrintTicket.CopyCount=printOptionsChoice.Copies;
				printDialog.PrintTicket.Collation=printOptionsChoice.IsCollated ? Collation.Collated : Collation.Uncollated;
				DocumentPaginator documentPaginator=fixedDocument.DocumentPaginator;
				if(!printOptionsChoice.IsAllPages) {
					documentPaginator=new PageRangeDocumentPaginator(documentPaginator,printOptionsChoice.FromPage,printOptionsChoice.ToPage);
					if(documentPaginator.PageCount==0) {
						MsgBox.Show(Lang.g(nameof(PrintOptionsWpf),"The document has no pages in that range."));
						return false;
					}
				}
				printDialog.PrintDocument(documentPaginator,description??"");
			}
			catch(Exception ex) {
				MsgBox.Show(Lang.g(nameof(PrintOptionsWpf),"There was an error while trying to print.")+"\r\n"+ex.Message);
				return false;
			}
			return true;
		}

		///<summary>The printer the rules pick for the situation, as WinForms printing does (OpenDental.PrinterL): the Default situation's printer,
		///replaced by the situation's own printer when it has a row. "" means the Windows default printer. isPrompt is the Prompt setting of the
		///row that picked the printer.</summary>
		public static string GetPrinterNameForSituation(PrintSituation printSituation,out bool isPrompt) {
			isPrompt=false;
			string printerName="";
			try {
				Printer printer=Printers.GetForSit(PrintSituation.Default);
				if(printer!=null) {
					isPrompt=printer.DisplayPrompt;
					if(Printers.PrinterIsInstalled(printer.PrinterName)) {
						printerName=printer.PrinterName;
					}
				}
				if(printSituation!=PrintSituation.Default) {
					Printer printerSituation=Printers.GetForSit(printSituation);
					if(printerSituation!=null) {
						isPrompt=printerSituation.DisplayPrompt;
						if(Printers.PrinterIsInstalled(printerSituation.PrinterName)) {
							printerName=printerSituation.PrinterName;
						}
					}
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//No computer or printer rows (for example a database without the tables): use the Windows default.
			}
			if(printerName=="") {
				printerName=GetWindowsDefaultPrinter();
			}
			return printerName;
		}

		///<summary>The Windows default printer's name, or "" if there is none.</summary>
		public static string GetWindowsDefaultPrinter() {
			try {
				return new System.Drawing.Printing.PrinterSettings().PrinterName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>The print queue whose full name is the printer name (local printers and printer connections). Null if not found.</summary>
		public static PrintQueue FindPrintQueue(string printerName) {
			if(string.IsNullOrWhiteSpace(printerName)) {
				return null;
			}
			try {
				LocalPrintServer localPrintServer=new LocalPrintServer();
				PrintQueueCollection printQueueCollection=localPrintServer.GetPrintQueues(
					new[] { EnumeratedPrintQueueTypes.Local,EnumeratedPrintQueueTypes.Connections });
				foreach(PrintQueue printQueue in printQueueCollection) {
					if(string.Equals(printQueue.FullName,printerName,StringComparison.OrdinalIgnoreCase)
						|| string.Equals(printQueue.Name,printerName,StringComparison.OrdinalIgnoreCase))
					{
						return printQueue;
					}
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return null;
		}
	}

	///<summary>Shows only some pages of another paginator: fromPage to toPage, 1-based and inclusive. Pages past the end are left out.</summary>
	public class PageRangeDocumentPaginator:DocumentPaginator {
		private readonly DocumentPaginator _documentPaginatorInner;
		private readonly int _idxFirst;
		private readonly int _count;

		public PageRangeDocumentPaginator(DocumentPaginator documentPaginatorInner,int fromPage,int toPage) {
			_documentPaginatorInner=documentPaginatorInner;
			if(!_documentPaginatorInner.IsPageCountValid) {
				_documentPaginatorInner.ComputePageCount();
			}
			_idxFirst=Math.Max(0,fromPage-1);
			int idxLastExclusive=Math.Min(_documentPaginatorInner.PageCount,Math.Max(0,toPage));
			_count=Math.Max(0,idxLastExclusive-_idxFirst);
		}

		public override DocumentPage GetPage(int pageNumber) {
			return _documentPaginatorInner.GetPage(_idxFirst+pageNumber);
		}

		public override bool IsPageCountValid {
			get {
				return true;
			}
		}

		public override int PageCount {
			get {
				return _count;
			}
		}

		public override Size PageSize {
			get {
				return _documentPaginatorInner.PageSize;
			}
			set {
				_documentPaginatorInner.PageSize=value;
			}
		}

		public override IDocumentPaginatorSource Source {
			get {
				return _documentPaginatorInner.Source;
			}
		}
	}
}
