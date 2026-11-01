// B-PRINT: the program's own Print Options window (Open Dental 25.3), used instead of the Windows print dialog.
// M2-MISC (Open Dental 25.4, minor Print window enhancements): the rule that picked the printer and the printer's state.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Input;
using CodeBase;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace OpenPmsDental {
	///<summary>Lets the user pick the printer, number of copies, collation and page range before printing. Set PrintSituation_ and
	///PrintOptionsChoice_ (the starting values, see PrintOptions.GetInitialChoice) before ShowDialog. When IsDialogOK, PrintOptionsChoice_ holds the
	///user's choice. Set IsPageRangeAllowed to false where the caller cannot print part of a document.</summary>
	public partial class FrmPrintOptions:FrmODBase {
		public PrintSituation PrintSituation_=PrintSituation.Default;
		///<summary>In: the starting values. Out: the choice, when IsDialogOK.</summary>
		public PrintOptionsChoice PrintOptionsChoice_;
		public bool IsPageRangeAllowed=true;
		///<summary>Installed printers, filled at load. Tests and callers may set it first to avoid asking Windows again.</summary>
		public List<string> ListPrinters;
		///<summary>25.4: the printer the situation's rules pick on this computer (Setup, Printers), resolved at load. Null if the printer rows could
		///not be read; the rule line is then left empty.</summary>
		private PrinterChoice _printerChoiceRules;
		///<summary>The printer last chosen for this situation in this session (PrintOptions.Remember), or "".</summary>
		private string _printerNameLastUsed="";
		private string _windowsDefaultPrinterName="";
		///<summary>What Windows reports about each installed printer. Null until the background query answers.</summary>
		private Dictionary<string,PrinterOnlineStatus> _dictPrinterStatuses;

		public FrmPrintOptions() {
			InitializeComponent();
			Load+=FrmPrintOptions_Load;
			PreviewKeyDown+=FrmPrintOptions_PreviewKeyDown;
			radioAll.Click+=radioPages_Click;
			radioRange.Click+=radioPages_Click;
			comboPrinter.SelectionChangeCommitted+=comboPrinter_SelectionChangeCommitted;
		}

		private void FrmPrintOptions_Load(object sender,EventArgs e) {
			Lang.F(this);
			if(ListPrinters==null) {
				ListPrinters=PrintOptions.GetInstalledPrinters();
			}
			if(PrintOptionsChoice_==null) {
				PrintOptionsChoice_=PrintOptions.GetInitialChoice(PrintSituation_,"",1,ListPrinters);
			}
			for(int i=0;i<ListPrinters.Count;i++) {
				comboPrinter.Items.Add(ListPrinters[i],ListPrinters[i]);
				if(ListPrinters[i]==PrintOptionsChoice_.PrinterName) {
					comboPrinter.SelectedIndex=i;
				}
			}
			if(comboPrinter.SelectedIndex==-1 && comboPrinter.Items.Count>0) {
				comboPrinter.SelectedIndex=0;
			}
			labelSituation.Text=Lang.g(this,"Print situation")+": "+Lang.g("enumPrintSituation",PrintOptions.GetSituationText(PrintSituation_));
			textCopies.Value=Math.Max(1,PrintOptionsChoice_.Copies);
			checkCollate.Checked=PrintOptionsChoice_.IsCollated;
			radioAll.Checked=PrintOptionsChoice_.IsAllPages || !IsPageRangeAllowed;
			radioRange.Checked=!radioAll.Checked;
			textFromPage.Value=Math.Max(1,PrintOptionsChoice_.FromPage);
			textToPage.Value=Math.Max(1,PrintOptionsChoice_.ToPage);
			groupPages.IsEnabled=IsPageRangeAllowed;
			SetRangeEnabled();
			//25.4: which rule picked the printer, and the printer's state where Windows reports it.
			try {
				_printerChoiceRules=PrintSituationRules.ResolveForThisComputer(PrintSituation_);
			}
			catch(Exception ex) {
				ex.DoNothing();//No printer rows to read: the rule line stays empty, printing works as before.
			}
			_windowsDefaultPrinterName=PrintSituationRules.GetWindowsDefaultPrinterName();
			_printerNameLastUsed=PrintOptions.GetRemembered(PrintSituation_)?.PrinterName??"";
			SetPrinterInfo();
			FillPrinterStatusesInBackground();
			butPrint.Focus();
		}

		///<summary>Asks Windows for the printers' states on a background thread (a network printer can take a moment to answer, and the window must
		///not wait for it), then shows the selected printer's state.</summary>
		private void FillPrinterStatusesInBackground() {
			System.Windows.Threading.Dispatcher dispatcher=Dispatcher;
			System.Threading.Tasks.Task.Run(() => {//OpenDentBusiness.Task is the task table
				Dictionary<string,PrinterOnlineStatus> dictPrinterStatuses=PrinterStatuses.GetAll();//never throws; empty when WMI does not answer
				dispatcher.BeginInvoke(new Action(() => {
					_dictPrinterStatuses=dictPrinterStatuses;
					SetPrinterInfo();
				}));
			});
		}

		///<summary>Fills the rule line and the state line for the selected printer (25.4).</summary>
		private void SetPrinterInfo() {
			string printerName="";
			if(comboPrinter.SelectedIndex>-1) {
				printerName=comboPrinter.GetSelected<string>();
			}
			labelRule.Text="";
			if(_printerChoiceRules!=null) {
				labelRule.Text=PrintSituationRules.GetPrintWindowSourceText(_printerChoiceRules,printerName,_printerNameLastUsed,_windowsDefaultPrinterName);
			}
			labelStatus.Text="";
			string statusText=PrinterStatuses.GetText(PrinterStatuses.GetStatus(_dictPrinterStatuses,printerName));
			if(statusText!="") {
				labelStatus.Text=Lang.g(this,"Status")+": "+Lang.g(this,statusText);
			}
		}

		///<summary>Only the rule and state lines follow the printer. Copies, collation and the page range stay as the user set them (25.4: the
		///page range is kept when switching printers).</summary>
		private void comboPrinter_SelectionChangeCommitted(object sender,EventArgs e) {
			SetPrinterInfo();
		}

		private void SetRangeEnabled() {
			textFromPage.IsEnabled=radioRange.Checked;
			textToPage.IsEnabled=radioRange.Checked;
		}

		private void radioPages_Click(object sender,EventArgs e) {
			SetRangeEnabled();
		}

		private void FrmPrintOptions_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butPrint.IsAltKey(Key.P,e)) {
				butPrint_Click(this,new EventArgs());
			}
			if(butCancel.IsAltKey(Key.C,e)) {
				butCancel_Click(this,new EventArgs());
			}
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(comboPrinter.SelectedIndex==-1) {
				MsgBox.Show(this,"Please select a printer.");
				return;
			}
			if(!textCopies.IsValid()) {
				MsgBox.Show(this,"Please enter a number of copies from 1 to 999.");
				return;
			}
			if(radioRange.Checked && (!textFromPage.IsValid() || !textToPage.IsValid())) {
				MsgBox.Show(this,"Please enter the first and last page to print.");
				return;
			}
			PrintOptionsChoice printOptionsChoice=new PrintOptionsChoice();
			printOptionsChoice.PrinterName=comboPrinter.GetSelected<string>();
			printOptionsChoice.Copies=textCopies.Value;
			printOptionsChoice.IsCollated=checkCollate.Checked==true;
			printOptionsChoice.IsAllPages=radioAll.Checked;
			printOptionsChoice.FromPage=textFromPage.IsValid() ? textFromPage.Value : 1;
			printOptionsChoice.ToPage=textToPage.IsValid() ? textToPage.Value : 1;
			string error=printOptionsChoice.GetError();
			if(error!="") {
				MsgBox.Show(this,error);
				return;
			}
			PrintOptionsChoice_=printOptionsChoice;
			IsDialogOK=true;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			IsDialogOK=false;
		}
	}
}
