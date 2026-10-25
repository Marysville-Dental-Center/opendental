// B-PRINT: the Print Options window replaces the Windows print dialog (Open Dental 25.3 "printer options window when printing from some areas").
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using OpenDentBusiness;

namespace OpenDental {
	public partial class PrinterL {
		///<summary>Shows the Print Options window for a print job whose printer settings already hold the printer the situation's rules picked.
		///Puts the user's printer, copies, collation and page range into the settings and remembers the choice for the situation for this session.
		///When the situation's printer is a virtual (print to file) printer and the user picks another printer, printing to a file is turned off.
		///Returns false if the user cancelled.</summary>
		private static bool ShowPrintOptions(PrinterSettings printerSettings,PrintSituation printSituation,Printer printerForSit) {
			List<string> listPrinters=PrintOptions.GetInstalledPrinters();
			PrintOptionsChoice printOptionsChoice=PrintOptions.GetInitialChoice(printSituation,printerSettings.PrinterName,printerSettings.Copies,listPrinters);
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
			printOptionsChoice.ApplyTo(printerSettings);
			if(printerSettings.PrintToFile && printerForSit!=null && printerForSit.IsVirtualPrinter && printOptionsChoice.PrinterName!=printerForSit.PrinterName) {
				printerSettings.PrintToFile=false;
			}
			return true;
		}

		///<summary>System.Drawing printing calls the page handler for every page whatever the page range says, so when the Print Options window chose
		///a range (PrintRange.SomePages), only those pages are sent to the printer (PageRangePrintController).</summary>
		private static void ApplyPageRange(PrintDocument printDocument) {
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
		}
	}
}
