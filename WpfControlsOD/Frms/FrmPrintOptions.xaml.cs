// B-PRINT: the program's own Print Options window (Open Dental 25.3), used instead of the Windows print dialog.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Input;
using CodeBase;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
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

		public FrmPrintOptions() {
			InitializeComponent();
			Load+=FrmPrintOptions_Load;
			PreviewKeyDown+=FrmPrintOptions_PreviewKeyDown;
			radioAll.Click+=radioPages_Click;
			radioRange.Click+=radioPages_Click;
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
			butPrint.Focus();
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
